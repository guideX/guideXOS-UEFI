# guideXOS C# UEFI — Integration H2c Closeout

**Outcome C: deterministic artifact admission is repaired; the ABI-gate producer remains unresolved.** Phase 33 stays paused. The stress run reproduced the ABI gate before the required matrix completed, so this report does not claim the gate or historical page fault is closed.

## Repository and preservation

- Actual starting HEAD: `7b3ca692b99972d0463f64110004626d0a426d26` on `main`, tracking `origin/main`; the starting working tree was clean. This is the newer committed state the user supplied; the pasted H2c note's expected starting HEAD (`996c300...`) was stale.
- The H2b allocator ownership fix is part of the starting commit and remains intact. No reset, stash, rebase, push, or destructive cleanup was performed.
- The protected `out/phase32-current-allowlisted.log` still hashes to `200CFBF0270C2FDE1B874711937460BA0F8E81FB5E750FB5A8DA9B8C29CAF00C`.
- `out/rt` remains detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, with 58 dirty/untracked entries observed. It was protected; no runtime source was intentionally edited.
- The adjacent `D:\dev\guideXOS` repository reports clean `main...origin/main`. `D:\dev\guideXOSServer` reports `main...origin/main [ahead 2]` with untracked files. Neither adjacent repository was modified by this task.

## Managed artifact identity

The before-and-after inventory is in [`UEFI_INTEGRATION_H2C_ARTIFACT_AUDIT.csv`](UEFI_INTEGRATION_H2C_ARTIFACT_AUDIT.csv): 22 `before` rows and 22 final `after` rows. The before audit established descriptor agreement `22/22`, staging agreement `22/22`, and compiled hard-coded allowlist agreement `0/22`. The stale allowlist was the mismatch; the descriptors already described the executable bytes.

The ordinary build now has this dependency order:

```text
Phase 29–32 proof compilation
  → normalize each final PE timestamp set
  → generate GXMI descriptor and stage executable/descriptor
  → audit all 22 staged build outputs and emit ManagedArtifactIdentities.g.cs
  → compile kernel against generated identities
  → build ramdisk and verify staged payload bytes
  → assemble ESP and verify copied kernel, ramdisk, and EFI loader
```

`ManagedArtifactIdentities.g.cs` is generated under `guideXOS/obj` before kernel compilation and is included by the project. The after-build audit reads the 22 generated flag/hash cases back from that source and compares each accepted hash with the final executable digest; it no longer assumes that `--kernel-built` means the hashes match. `ManagedImage` requires both the descriptor digest to match the generated kernel allowlist and the SHA-256 of the actual loaded executable to match that descriptor. It therefore does not trust an untrusted descriptor as proof of the bytes it describes. A mismatch still returns `ARTIFACT_HASH`.

The canonical identity is SHA-256 of the complete executable after zeroing only the PE COFF `TimeDateStamp` and each PE `IMAGE_DEBUG_DIRECTORY.TimeDateStamp`. The Phase 29–32 scripts normalize those fields before writing GXMI descriptors and staging. No other bytes are excluded. Since the normalized fields are already zero in final staged output, raw and canonical SHA-256 are equal.

Builds 3 and 4 compared `22/22` raw-equal and canonical-equal. The final ordinary build also compares to build 4 at `22/22` raw-equal and `22/22` canonical-equal. Final descriptor agreement, compiled allowlist agreement, build-to-staging agreement, and ramdisk-source agreement are each `22/22`. The final audit and reproducibility table are [`UEFI_INTEGRATION_H2C_ARTIFACT_AUDIT.csv`](UEFI_INTEGRATION_H2C_ARTIFACT_AUDIT.csv) and [`UEFI_INTEGRATION_H2C_REPRODUCIBILITY.csv`](UEFI_INTEGRATION_H2C_REPRODUCIBILITY.csv).

There is one current generated admission identity per proof variant; obsolete hard-coded Phase 29–32 hashes were removed rather than retained as fallback admissions. Three disposable negative controls remained rejected with `ARTIFACT_HASH`: one-byte executable mutation, GXMI digest mutation, and proof-mode-bit removal. Logs are `out/h2c-negative-exe-postfix.log`, `out/h2c-negative-descriptor-postfix.log`, and `out/h2c-negative-proof-mode-postfix.log`.

## Fresh managed controls

- **Phase 29:** fresh proof completed (`RING3_PHASE29_COMPLETE=1`), including FailFast, stale application/service-context rejection, and managed/bootstrap/process cleanup balance.
- **Phase 30:** fresh proof completed (`RING3_PHASE30_COMPLETE=1`), including malformed/oversize, clear, FailFast, stale-owner/service-context rejection, and cleanup balance.
- **Phase 31:** final executable was admitted. Four primary successful `31` returns and the replacement `31` completed; invalid-target and oversize cases returned `41` and `42`. FailFast, stale-owner rejection, target cleanup, app/process balance, and `RING3_PHASE31_COMPLETE=1` were recorded. No guest fault occurred. See `out/h2c-phase31-posthash-allocationfree.log`.
- **Phase 32:** final executable was admitted. Five successful `32` returns, negative cases, stale owner, FailFast/replacement, six target terminations, cleanup end, app/process balance, and `RING3_PHASE32_COMPLETE=1` were recorded. Cleanup counters showed `freeInvalid=0`, `freeNoPages=0`, and `freeCorrupt=0`. See `out/h2c-phase32-fresh.log`.

## ABI-gate reproduction and limits

The gate enforces that software interrupt `int 0x80` reaches the Ring 3 ABI only from CPL3. The H2c stress reproduced it once (`RING3_ABI_KERNEL_GATE`) during the tenth Start-menu opening, after nine Notepad open/close cycles. The reason is enum `0x02`, `KERNEL_ORIGINATED_INT80`: saved `CS=0x8` (CPL0), `RIP=0x27F656`, `RFLAGS=0x246`, `RSP=0x27F5E0`, and `CR3=0x3DD73000`. The caller-byte window ends with `CD 80`, consistent with an `int 0x80` instruction. Candidate saved values were `RAX=0`, `RDI=1`, `RSI=0x10113ED0`, and `RDX=1`.

At the panic, CPU 0's scheduler thread existed, was a kernel thread (`IS_USER=0`), and was not terminated; no current process was present. Allocator owner was `0x1000FFF`. Window cleanup was active in pass 1, pending was false, and cleanup owner was 0. The single captured frame had `RBP=0x27F690`, a nonmonotonic previous-frame value `0x487`, and return `0x1000933A`; that return maps near `Allocator.FindOrAddOwner` / `Allocator.Initialize`, but the broken frame chain does not establish that as the producer of the interrupt. The visible trigger path is Start toggle → foreground notification/cache rebuild. This is correlation, not a proven call chain. The code path that emitted the invariant remains unresolved, and the panic was preserved in `out/h2c-cleanup-stress-first-failure-preserved.log`.

The earlier vector-14 event remains distinct. In `out/h2b-cleanup-stress-recursive-lock-fault-preserved.log`, the first recorded PF had `RIP=0x1000148B`, `CR2=0x47E24E75`, `ERR=0`, `CS=8`, `RFLAGS=0x10046`, `RSP=0x27F790`, and `CR3=0x3DD73000`. `ERR=0` decodes as supervisor read of a non-present page, with no reserved-bit or instruction-fetch flag. The recorded PML4E (`0x3DD72023`) and PDPTE (`0x3D6B3023`) were present, while PDE was zero; the walk stops there, so no PTE exists. The RIP mapped to `__GetGCStaticBase_guideXOS_guideXOS_OS_ApplicationAssociationRegistry`. That historical log predates the new vector-14 byte window, so instruction bytes at RIP were not captured. H2c's new run recorded no PF before the ABI panic. The differing RIP, address, and run context provide no evidence that the PF and ABI gate share a cause.

## Stress, cleanup, allocator, and UI controls

| Matrix item | H2c result |
| --- | --- |
| Notepad | 9 complete open/close cycles; panic on the tenth Start-menu opening, before Calculator or later matrix cases |
| Calculator | 0 stress cycles |
| Mixed | 0/25 cycles |
| Phase32-style | 0/10 stress sequences; standalone Phase32 proof above passed |
| Ordinary desktop | Notepad was exercised for the 9 completed cycles; Calculator and Computer Files full launch/activate/close sequences were not run |
| Guest faults in H2c stress log | ABI gate 1; `#UD` 0, `#PF` 0, `#GP` 0 before the panic |
| Owner ID aliases | 48 selected owner IDs checked; no ID was shared by distinct serial window objects |

Cleanup second-pass behavior was dynamically exercised before the panic. The preserved log shows pass 1 active → nested cleanup requested and `pending=1` → pass 1 completes → second-pass probe enqueued → pass 2 begins → probe cleaned → `pending=0` → cleanup exits. The run had no lost or recursive list mutation evidence in that bounded scenario.

The H2b allocator ownership repair remains intact: ordinary GC objects and static strings are not passed to page-free; only the exact start of a live allocator-owned run may be released. In the interrupted H2c stress, boot, idle, and the first Notepad samples were all `freeInvalid=0`; observed `freeCorrupt` and `freeNoPages` remained zero through the failure. Later stress deltas (Notepad 10/50, Calculator, mixed) were not measurable because the run stopped. Fresh Phase31/32 cleanup diagnostics also showed zero allocator error counters. Do not infer unrun matrix values from those samples.

Fresh AppModel control passed lifecycle `15/15` and taskbar/grouping `16/16`, with zero stale taskbar projections. The boot artwork log reports Start, hover, pressed, Files, Notepad, and Calculator icons `ok` with zero fallbacks. The interrupted desktop stress had empty taskbar state and `stale=0` after completed cycles. Running buttons/icons and activation were observed during Phase31/taskbar diagnostics; complete E1–E5 close/removal coverage remains incomplete.

## Ordinary build and release decision

The requested ordinary `.\build.ps1` run reached `Build Complete!`: Phase 26 host SDK setup succeeded, all intended Phase 29–32 artifacts were regenerated, identity generation preceded kernel compilation, ramdisk creation completed, and ESP staging completed. The final ramdisk SHA-256 is `21292E9610FF76D2C33C3A89A1A91CBA43B661A08BBB663233C06E866ADF8E28`; `ESP\ramdisk.img` matches. `ESP\kernel.elf` and source kernel match at `3F14142B7E1AA3A0D6574E0B35D63EF8AEB237AE205299997729139BC758961E`. `ESP\EFI\BOOT\BOOTX64.EFI` matches the bootloader source at `D3B5C77A1B5A33B68F7ABB36BC3D0DCACF0DFC8E786BCFF891316BF8A6352A20`. Full log: `out/h2c-full-build-final.log`.

Phase 33 must remain paused. Resume only after the CPL0 `int 0x80` producer is identified and fixed, the historical PF is either reproduced and fixed or given an adequate complete forensic non-recurrence run, and the remaining stress/allocator matrix completes without faults or leaks. The next H2c step is to preserve this first-failure log, minimize the Start-menu-triggered ABI-gate scenario without suppressing the invariant, and instrument/re-run the remaining matrix after that producer is fixed.
