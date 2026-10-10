# Phase 35R10 NotFound String Lifetime Investigation

Date: 2026-10-09

## Outcome

**Outcome F — exact ownership and lifetime for all six retained objects is not established. No reclamation change is justified. Phase 35 remains incomplete and Phase 36 remains gated.**

This continuation audited the live R9 capture and source without changing kernel or managed source. The R9 evidence is sufficient to compare fingerprints and identify a strong diagnostic-path candidate, but it does not meet R10's same-key, three-layer, diagnostic-off, or producer-site gates. In particular, the isolated NotFound proof used `missing.phase35r6`, while the full-success payload used `missing.phase35`. Those keys differ, so path-derived fingerprints cannot be treated as a controlled same-key comparison.

## Preflight and preservation

- Root: `D:\dev\guideXOSUEFI`; branch `main`; starting HEAD `4b82358a2fd3714d2307d6550238436e70b6dcce`; ending HEAD `47839f17972d43eb22758bc8595d2c9bd80d8e47`; both subjects `...`; upstream `origin/main`; ahead/behind at end `0/0`.
- During the investigation, the live checkout advanced to the new `origin/main` commit `47839f1` (rename of `APP_MODEL_CONVERGENCE.md` to `docs/APP_MODEL_CONVERGENCE.md` and an R9 report update). I did not create that commit or alter its contents. I observed and preserved the new authoritative state. At initial preflight the root had the tracked deletion, modified R9 report, and untracked replacement described in the prior R9 amendment.
- Nested `out\rt` remains detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, with its pre-existing modifications and untracked files untouched.
- No source edit, build, guest run, reset, clean, stash, branch change, commit, or push occurred. R7, R8, and R9 reports were preserved.
- Evidence used: `out\phase35q\serial-5464552ef4b14b42b29dd36b0f91f6f3.log`; R9 diagnostic kernel and ramdisk hashes remain those recorded in the R9 report.

## Fingerprint comparison

All rows are `System.String` allocations through `RhpNewArray`, class `NativeRuntimeMalloc`, one-page runs, fallback site `0x191` / 401. Fingerprints below are `(length, requested bytes, hash)`.

| String | Full success | Isolated NotFound | Match? |
|---|---|---|---|
| 1 | `(51, 0x80, 94A661B97507E172)` | `(51, 0x80, 94A661B97507E172)` | Yes |
| 2 | `(50, 0x80, ADE827DD03D3AE1F)` | `(50, 0x80, ADE827DD03D3AE1F)` | Yes |
| 3 | `(38, 0x68, 7B75BBA2453D7996)` | `(40, 0x68, 8EBD40DD8DB592EE)` | No |
| 4 | `(251, 0x210, 2DB56378C1962B98)` | `(260, 0x220, E8FCED6A2CF131F9)` | No |
| 5 | `(1, 0x18, AF63AE4C86019E62)` | `(1, 0x18, AF63AE4C86019E62)` | Yes |
| 6 | `(36, 0x60, D82EC95D355533BA)` | `(36, 0x60, D82EC95D355533BA)` | Yes |

Exact overlap is **4 of 6**, counting every fingerprint field and type. Three fingerprints are now tied exactly to the live NotFound diagnostic branch by recomputing the ledger's FNV-1a hash over the emitted serial text:

| Captured object | Exact content | Producer / purpose | Owner and cleanup |
|---|---|---|---|
| `(51, 0x80, 94A661B97507E172)` | `P35_DIAG_APPLICATION_ID=selftest.phase10.persistent` | `PersistentFatBackend.TryReadValue`, FAT length failure diagnostic; identifies the application | Temporary concatenation passed directly to `Phase35DiagnosticMarker`; the method only writes characters to serial. The caller does not retain the concatenation in a local or dispose it. Intended lifetime is one marker call; missing cleanup is after the call. |
| `(40, 0x68, 8EBD40DD8DB592EE)` | `P35_DIAG_RELATIVE_PATH=missing.phase35r6` | Same branch; identifies the requested relative key | Same unowned marker argument and missing post-call `Dispose`. |
| `(260, 0x220, E8FCED6A2CF131F9)` | `P35_DIAG_FILE_PATH=` followed by the encoded FAT path for `missing.phase35r6` | Same branch; diagnostic rendering of the full encoded value path | The source `filePath` is separately owned and correctly disposed in `TryReadValue`'s `finally`; the concatenated marker argument is a distinct temporary and is not disposed after output. |

The full-success capture's matching length-51 value is expected because that workload also performs a NotFound read. The path-specific diagnostic strings differ in length and hash because the full-success and isolated requests use different keys.

The isolated proof's key is `missing.phase35r6` (UTF-16 length 17); the full-success key is `missing.phase35` (length 15). Both are locally valid according to the public storage path rules; no same-key validation was run across all three requested layers.

## Source audit findings

The live control flow is `Ring3Abi.PersistentRead` → `CSharpApplicationStorageService.Read` → `PersistentFatBackend.TryReadValue` → FAT `TryGetFileLength`. On the FAT `NotFound` return, `TryReadValue` emits three diagnostic messages formed with string concatenation: application ID, relative path, and encoded persistent file path. It then returns the typed `FatOperationResult.NotFound`; the `finally` disposes the locally owned `filePath`.

`CSharpApplicationStorageService.Read` converts the typed FAT result using `PersistentFailure<T>`, maps `NotFound` to `ApplicationServiceResultCode.NotFound`, and creates a diagnostic using `"Persistent storage operation failed: " + result.ToString()`. The service result's `BoundedDiagnostic` has no visible disposal path in `ApplicationServiceResult<T>`; however, this alone does not prove that any of the six observed retained strings are that diagnostic. `Ring3Abi.PersistentRead` disposes its `read` result in `finally`, but the result type has no `Dispose` method in the audited source. The path value created by `TryValidatePersistentReadRequest` is explicitly disposed by the ABI `finally`.

This source path and the exact serial-text hashes prove the producers, purposes, logical call owner, intended lifetime, and missing cleanup for these three objects. The R9 allocation site remains fallback `String.Ctor(char*, index, length)`, so pre-construction numeric site attribution still needs to be added for future captures. The other three objects have not been assigned exact producer sites or owners. The source audit therefore does **not** satisfy the R10 six-object root-cause threshold and does not establish whether the six-page retention exists with detailed diagnostics disabled.

## R10 gates not established by available evidence

- Direct backend-only, raw ABI, and public managed SDK NotFound captures using one identical key.
- Path validation and key hash recorded at each of those layers.
- Numeric site IDs captured before construction for the three proven diagnostic concatenations, and exact producer/owner/alias/cleanup analysis for the other three objects.
- Exact identities for the length-251 success and length-1 retained objects beyond type/length/hash.
- Diagnostic-off isolated NotFound slope; proof-minimal NotFound; post-fix NotFound/success/full payload; five- and 25-lifetime slopes; allocator/fault counters; 8-MiB allocation; remaining Phase 35 qualification.

No diagnostic or production string was manually disposed: the evidence does not yet prove object identities, owner contracts, or alias safety. The narrow next step is to instrument the actual concatenation/constructor callers above `RhpNewArray` and rerun backend, raw ABI, and public SDK controls using one fixed valid missing key, followed by a diagnostic-off control before considering any repair.

## Decision

- Starting/ending HEAD: `4b82358a2fd3714d2307d6550238436e70b6dcce` → `47839f17972d43eb22758bc8595d2c9bd80d8e47` (live checkout advanced during the task; not authored here).
- Rebuilt kernel hash: none; no rebuild occurred.
- Exact fingerprint overlap: 4/6; same missing key across captures: **No**.
- Direct backend / raw ABI / public SDK retained-string counts: not isolated in this continuation.
- Proof-minimal and diagnostic-off counts: not measured.
- Exact six owners and Dispose/free paths: unresolved.
- Post-fix retained pages and lifetime slopes: not applicable; no fix was made.
- Phase 35 decision: incomplete; continue R10 localization.
- Phase 36 gate: remains closed.
- Next action: run same-key three-layer, caller-site-instrumented, diagnostic-on/off captures, then decide whether a diagnostic-only cleanup or production ownership repair is supported.
