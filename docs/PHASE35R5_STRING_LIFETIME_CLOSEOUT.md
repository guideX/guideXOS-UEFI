# Phase 35R5: PersistentRead string lifetime investigation

Status: **Outcome F — string allocation and disposal sites for the four minimal-read survivors are not yet established. No production lifetime fix was made. Phase 35 remains incomplete and Phase 36 remains gated.**

## Preflight and preserved state

- Repository: `D:/dev/guideXOSUEFI`
- Branch: `main`
- Starting HEAD: `5fac894f3b70ab10491710fd8ea414a4d1f6a4ca`
- Subject: `...`
- Upstream: `origin/main`; ahead/behind: `0/0`
- Root worktree: already dirty at preflight. Existing R1–R4 tracked changes and the untracked R4 closeout were preserved.
- Nested `out/rt` HEAD: `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, detached HEAD, extensively dirty with preexisting changes. Preserved.
- No reset, clean, stash, rebase, branch, worktree, commit, or push was performed.

The new isolated diagnostic run is `out/phase35q/serial-dd4121123b7e49e892513ed9243303ba.log`. It was run with `Scripts/run_phase35q_storage_validation.ps1 -Phase35R2Matrix -SkipBuild -TimeoutSeconds 600` against the already staged diagnostic kernel. QEMU completed successfully, and the harness restored the fixture image before cleanup. This run did not rebuild the current dirty source.

## Runtime allocation and disposal contract

The active custom CoreLib `System.String` is sealed and declares no `Dispose` override. It inherits `System.Object.Dispose` from `Corlib/System/Object.cs`.

Its character-buffer constructor calls `StartupCodeHelpers.RhpNewArray` with the string EEType. The kernel override computes the object size, calls `malloc`, initializes the EEType and length, and returns that allocation address as the object reference. In this kernel, `malloc` enters `stdlib.malloc`, then `Allocator.Allocate`. This is not a tracing-GC allocation path.

When a dynamic string is disposed, virtual dispatch reaches `Object.Dispose`, which calls `Allocator.FreeManagedObjectIfAllocatorRun`. That method first checks the allocator page metadata and only calls `Free` when the reference is the start page of a live allocator run. The string object address is the returned `malloc` address itself: it is expected to be the allocator run start, not an interior or post-header pointer. A valid dynamic string allocation should therefore be releasable by this guarded path. Static/frozen strings and unrelated managed references are ignored by the guard. `stdlib.free` directly calls allocator free, but `String.Dispose` does not call `stdlib.free` directly.

**Expected reclamation model:** dynamically allocated kernel strings have explicit manual lifetime in this custom runtime. Their owner must dispose them exactly once when their last use ends. Desktop .NET GC assumptions do not apply to these `RhpNewArray` allocations.

## Audited source paths

- `Kernel/Misc/Ring3Abi.cs`: `TryValidatePersistentReadRequest` allocates a `char[]`, creates `relativePath` with `new string(characters)`, disposes the character array, and disposes the relative-path string on validation failure. On the accepted path, `HandlePersistentRead` disposes `relativePath` in its `finally` after service/backend use. This source audit shows the cleanup call exists, but no runtime disposal-address trace was captured for that exact string.
- `guideXOS/OS/PersistentFatBackend.cs`: `TryReadValue` obtains a `filePath` from `GetValueFilePath` and disposes that final string in a `finally`. `GetValueFilePath` creates a temporary `char[]`, creates the returned string, disposes the char array, and transfers the returned string to its caller. This source audit does not prove the exact runtime string identities in the observed retained pages.
- `Kernel/FS/FAT.cs`: `FindPath` strips leading slashes via `Substring`, splits the path, disposes each split component and the parts array, and disposes any separately owned stripped path in `finally`. Directory-entry strings from `ComposeShortName`/`AssembleLfn` are disposed after comparison. The source path therefore contains explicit cleanup for these visible temporary strings; exact string allocation and free outcomes were not observed.
- `Corlib/System/String.cs`: substring and split operations can allocate strings; `Split` returns an allocated array and separately allocated non-empty components. String concatenation creates a new string. The three/four-argument `String.Concat` helpers dispose intermediate strings and return the final allocation to the caller. These mechanics establish possible allocation behavior, not which objects survive the PersistentRead request.

No evidence in this run identifies a dynamically allocated AppId-encoding string or a separate `VALUE.BIN` string. The backend writes encoded path text into a `char[]` and constructs a final FAT path. A claim that those operations account for the four survivors would be speculation.

## Fresh matrix evidence

The serial run reports:

| Workload | Retained pages | Result |
|---|---:|---|
| No read | 0 | PASS |
| One successful read | 4 | PASS |
| Two successful reads | 8 | PASS |

The two-read result confirms linear request retention remains. The matrix sets `Allocator.DiagnosticProvenanceEnabled = false` before its minimal no-read/one-read/two-read cases. Consequently it does not emit a per-object string allocation/disposal ledger for those cases.

The detailed `PHASE35_RHP_NEW_ARRAY` records in this serial are from the earlier original-success workload, which performs six PersistentRead calls; they include numerous transient strings. They cannot be used as the four-object table for the later minimal matrix. In particular, the existing `helperReturn=0x100119F1` is the return address from `RhpNewArray` into its internal malloc wrapper, not its managed caller. No caller-into-`RhpNewArray` provenance or free event keyed by string object address was captured.

## Required survivor table (not yet evidenced)

The exact addresses, lengths, creation sites, and disposal histories for the four minimal successful-read survivors remain **unresolved**. The data available from this run is insufficient to populate these rows without inventing attribution.

| # | String address | Length | Creation site | Purpose | Dispose called? | Free result | Survives cleanup? |
|---|---:|---:|---|---|---|---|---|
| 1 | unresolved | unresolved | unresolved | unresolved | unobserved | unobserved | one retained page contribution, identity unproven |
| 2 | unresolved | unresolved | unresolved | unresolved | unobserved | unobserved | one retained page contribution, identity unproven |
| 3 | unresolved | unresolved | unresolved | unresolved | unobserved | unobserved | one retained page contribution, identity unproven |
| 4 | unresolved | unresolved | unresolved | unresolved | unobserved | unobserved | one retained page contribution, identity unproven |

The NotFound, locally rejected, and raw exact-buffer requests were not run as isolated string-ledger cases in this R5 pass. The decoded-path object has not been matched to an address, and its `Dispose` call/free result has not been observed. Creation-site symbol, source line, known-value/hash, allocator lookup, exact-run-start result, and after-process-cleanup status likewise remain open.

## Fix decision and next instrumentation

No allocator sweep, caching workaround, or production `Dispose` call was added. That avoids both freeing an aliased live string and hiding the ownership error, but leaves the confirmed leak in place.

The next diagnostic build must keep detailed provenance enabled for an isolated single-read case and add a bounded, non-allocating string ledger keyed by object address. It must record allocation length/size, request and owner identity, caller/site provenance, and exact-address disposal attempts with guarded allocator results, then report which recorded addresses remain live after request and process cleanup. If caller return-address symbolization remains unreliable, add narrowly placed creation-site IDs at proven string creation expressions. Only after that ledger produces a complete successful-read and NotFound table should call-site cleanup changes be considered.

The two prior owner-0 survivors (`object[]` and `int[]`) remain separate from this string investigation. This R5 run did not reclassify them or test whether they repeat.

## Phase decision

Outcome F applies: the exact creation and disposal sites are still unresolved, so Phase 35 acceptance does not resume. No post-fix one/two-read, NotFound, five/25/100-lifetime, 8-MiB allocation, reset, reboot, DSDT, artifact cohort, regression, or ordinary full-build claims are made. Phase 36 remains gated.
