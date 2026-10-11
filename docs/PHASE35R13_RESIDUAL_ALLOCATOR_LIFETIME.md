# Phase 35R13: Residual Allocator Lifetime

Date: 2026-10-10  
Result: **Outcome B** — the 8-page per-requester slope is identified and fixed; allocator stress is clean. Fresh reboot-delete verification and the remaining Phase 35 qualification are still open. Phase 35 remains incomplete, and Phase 36 remains gated.

## Summary

R12 closed the six request-local NotFound strings. R13 traced the remaining reported increase of `0xC8000` over 25 requester lifetimes:

```text
0xC8000 / 25 = 0x8000 bytes per lifetime = 8 x 4096-byte pages
```

The first diagnostic full requester measured 10 pages because it also initialized two process-table arrays for the first time. Those arrays accounted for two pages once. Every later full requester and each no-read, minimal-success, and NotFound-only control measured the same eight-page increase. The eight repeating pages were eight separate, small managed objects allocated as one-page runs. Their combined managed object size was `0x1D0` bytes; allocator run granularity, not object size, produced the 32-KiB slope.

The owner-based repair explicitly disposes the request, instance, and result wrappers at their proven ownership boundaries. It does not add a GC, owner sweep, or requester-ID free. The final contained run reports zero `B2-B0` pages in both full controls, no-read, minimal-success, NotFound-only, and the 35-lifetime sequence. The five- and 25-lifetime gates, allocator counters, fault scan, and 8-MiB allocation pass.

The seed boot's `PHASE35P2_REBOOT_DELETE=FAIL` is intentional: deletion is checked on the verification boot after a fixture has been seeded. A historical same-image run passed that second-boot check. The bounded fresh rerun passed the seed boot but faulted during verification-boot widget startup before it could emit the verification marker. A rerun using the R13 app-model files restored to the live baseline hit a different startup exception at the same point, so the current failure is not evidence of a delete/FAT regression. It still prevents fresh P2 qualification.

## Live preflight and containment

| Item | Captured value before source transfer |
|---|---|
| Live root | `D:\dev\guideXOSUEFI` |
| Branch / HEAD | `main` / `881c5b01c2d9d47801ca0f0baead0ee2f5fde981` |
| Subject | `...` |
| Upstream | `origin/main`, ahead 0, behind 0 |
| Root status | clean; no modified, deleted, or untracked files |
| Nested runtime | `D:\dev\guideXOSUEFI\out\rt` |
| Nested runtime HEAD | `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` |
| Nested runtime status | 58 entries |
| Sandbox | `D:\dev\guideXOSUEFI_Phase35R12_SANDBOX`, branch `main`, HEAD `0217f13798d4e07f7089da095641e482f8579c63` |
| Git worktrees created | none |

The live HEAD is a descendant of the R12 accepted HEAD `0217f13798d4e07f7089da095641e482f8579c63`. The inherited R12 source/script changes are `Kernel/Misc/Ring3Abi.cs`, `Scripts/run_phase35q_storage_validation.ps1`, `UserManagedPersistentStorageProof/Program.cs`, `guideXOS/OS/ApplicationServices.cs`, `guideXOS/OS/ApplicationStorageServices.cs`, and `guideXOS/OS/PersistentFatBackend.cs`. The R12 closeout is preserved.

The sandbox was refreshed from the current live source while preserving its local runtime, caches, logs, and test images. Its local R12 source snapshot and diagnostic changes were kept. The live checkout and `out/rt` were not used for diagnostic builds or guest runs. Only reviewed source, script, and documentation changes were transferred after sandbox validation; generated build outputs and test images were not transferred. The live root remains on `main`; no commit or topology change was made.

## Diagnostic design and operation plan

The contained mode is `Ring3Phase35R13`. It uses bounded numeric UART records for allocator runs, helper classes, EEType metadata, owner/generation, requester lifetime, operation, free state, and allocation-site totals. The managed helper IDs are `0` unknown/non-helper, `1` `RhpNewArray`, and `2` `RhpNewFast`. Allocator site `6` is `NativeRuntimeMalloc`.

The operation IDs used for the full requester are:

| ID | Operation |
|---:|---|
| 1 | Empty-key local invalid-path rejection |
| 2 | Absolute-path local invalid-path rejection |
| 3 | Parent-traversal local invalid-path rejection |
| 4 | Forbidden-character local invalid-path rejection |
| 5 | 193-character key local invalid-path rejection |
| 6 | 65-character segment local invalid-path rejection |
| 7 | `missing.phase35` NotFound read |
| 8 | First public `state.bin` read; mutate byte 0 |
| 9 | Second public `state.bin` read and fixture comparison |
| 10 | `empty.bin` read |
| 11 | Terminate the application owner |
| 12 | Reject a stale-context read after owner removal; do not call the backend |

The full workload therefore performs six local invalid-path checks, two raw bounds-proof reads, one NotFound read, two successful public state reads, one empty read, byte-0 mutation and fixture comparisons, one process create/cleanup, one application termination, and one stale-context rejection. The stale-context request is rejected before the backend. The first full-run markers were `R13_PERSISTENT_READS=6`, successes `5`, failures `2`; the counters include the stale-context failure marker and are not exclusive partitions of the six backend read count.

## Pre-fix measurement and retained-run table

Source log: `out/phase35q/serial-1e7134726bf341ca85dad7a4ee67ffcc.log`. Pre-fix kernel SHA-256: `82A23E6EDEF22DB4FCCEA7A0A1F9D19E642222493A3457145B0BBA57CC54A194`.

| Checkpoint | Bytes | Pages |
|---|---:|---:|
| B0 before requester | `61,837,312` | `15,097` |
| B1 after operations | `73,621,504` | `17,974` |
| B2Process after process cleanup | `61,874,176` | `15,106` |
| B2 after owner/context cleanup | `61,878,272` | `15,107` |
| B2 − B0 | `40,960` | **10** |

The first-touch 10 pages split into two global initialization pages and eight repeating pages. Scenario 2 and each of the three controls measured exactly eight pages. The first-touch arrays were `Ring3Process[]` (4 elements, `0x38` managed bytes) and `uint[]` (4 elements, `0x28` managed bytes); their combined `0x60` bytes occupied two one-page runs and they did not recur.

The table below includes every row that made up the first B2 delta. Each row was one EEType-described managed object at the start of a one-page allocator run, rather than a slab or multiple objects sharing a run. The raw pre-fix run's allocator owner/requester/generation fields were zero; the logical owners below were resolved from creation and disposal code. The final column gives the fixed numeric producer tag added to the contained R13 build. The pre-fix rows themselves predate those tags and therefore serialized `creationSite=0`; tagged allocations are present in the later final run. This is the only reason the captured baseline rows have a zero creation-site field.

| Run | Address | Pages | Managed bytes | Helper / allocator class | Type and producer site | Caller | Logical owner / lifetime | Operation; last use and cleanup |
|---|---:|---:|---:|---|---|---|---|---|
| `0x2B69D` | `0x7AC6000` | 1 | `0x60` | Fast / `NativeRuntimeMalloc` | `LaunchRequest`, 601 | `0x10012A72` | Request transfers to its `ApplicationInstance`; lifetime 1 | Setup / launch; disposed with its owning instance |
| `0x2B69E` | `0x7AFA000` | 1 | `0x18` | Array / `NativeRuntimeMalloc` | Empty `string[]` launch arguments, 602 | `0x10012B31` | `LaunchRequest`, then instance; lifetime 1 | Setup / launch; freed by request disposal |
| `0x2B69F` | `0x7AFB000` | 1 | `0x58` | Fast / `NativeRuntimeMalloc` | `ApplicationInstance`, 603 | `0x10012A72` | Registry's exact handle and generation; lifetime 1 | Setup / launch; unregistered, then disposed |
| `0x2B6A0` | `0x7AFC000` | 1 | `0x58` | Array / `NativeRuntimeMalloc` | Owned `Window[]` (length 8), 604 | `0x10012B31` | Owning instance; lifetime 1 | Setup / launch; freed by instance disposal |
| `0x2B6A5` | `0x7AFD000` | 1 | `0x20` | Fast / `NativeRuntimeMalloc` | Context-creation `ApplicationServiceResult`, 605 | `0x10012A72` | Ring3 application-service context; lifetime 1 | Setup / context creation; caller disposes result |
| `0x2B6A6` | `0x7AFE000` | 1 | `0x38` | Array / `NativeRuntimeMalloc` | `Ring3Process[]` (length 4), 608 | `0x10012B31` | Global process table; one-time initialization | Setup / process-table initialization; retained globally by design |
| `0x2B6A7` | `0x7B01000` | 1 | `0x28` | Array / `NativeRuntimeMalloc` | `uint[]` process generations (length 4), 608 | `0x10012B31` | Global process table; one-time initialization | Setup / process-table initialization; retained globally by design |
| `0x2CA36` | `0x7B02000` | 1 | `0x30` | Fast / `NativeRuntimeMalloc` | `ApplicationLifecycleResult`, 612 | `0x10012A72` | Termination caller; lifetime 1 | Operation 11; converted to status, then disposed |
| `0x2CA37` | `0x7B03000` | 1 | `0x30` | Fast / `NativeRuntimeMalloc` | Stale-context `ApplicationStorageReadRequest`, 606 | `0x10012A72` | Ring3 proof stale-context call; lifetime 1 | Operation 12; rejected without backend, then disposed |
| `0x2CA3A` | `0x7B05000` | 1 | `0x28` | Fast / `NativeRuntimeMalloc` | `ApplicationServiceResult<ApplicationStorageReadResult>`, 606 | `0x10012A72` | Ring3 proof stale-context call; lifetime 1 | Operation 12; rejected result disposed |
| **Total** |  | **10** | **`0x230`** |  | **10 distinct managed objects** |  | **2 one-time + 8 per requester** | **10 pages accounted; 0 unknown** |

The eight repeating object sizes sum to `0x1D0` bytes and each occupied a separate page. The two global arrays sum to `0x60` bytes. Together they account for `0x230` requested managed bytes and 10 pages. The two global arrays were already initialized before B0 in the final run (`R13_PROCESS_TABLE_INIT_BYTES=0x2000`), so the repeated-requester delta became eight pages there. The final tagged run emits producer IDs 601–613 from the managed creation scopes; the corresponding exact producer methods are `LaunchRequest.ForAppId`/argument cloning, `ApplicationInstanceRegistry` construction, instance window storage, service-context creation, process-table initialization, lifecycle termination, and the stale-context read path.

### Helper and allocator survivor reconciliation

For the first pre-fix B2 snapshot:

| Class | Allocations | Frees | Survivors | Pages |
|---|---:|---:|---:|---:|
| `RhpNewArray` | 4,923 | 4,919 | 4 | 4 |
| `RhpNewFast` | 99 | 93 | 6 | 6 |
| Unknown helper | 0 | 0 | 0 | 0 |
| `NativeRuntimeMalloc` site | 4,368 | 4,358 | 10 | 10 |
| Other allocator sites | balanced | balanced | 0 | 0 |

The helper survivor split is four array objects and six fast objects, matching the ten EEType rows. All ten runs were `NativeRuntimeMalloc`. The two first-use global arrays were the only bounded state; the other eight appeared on every full, no-read, minimal-success, and NotFound-only requester. This rules out the FAT/backend read path and the proof's byte-array comparisons as the source of the linear slope.

## Root cause and repair

The kernel's managed runtime uses the custom allocator and does not reclaim these objects with automatic GC. `System.Object.Dispose` previously documented GC reclamation instead of freeing managed objects in this runtime. The eight recurring objects were therefore left allocated after their last owner finished.

The repair is narrow and follows existing ownership:

- `LaunchRequest.Dispose` releases its owned argument array. A request transfers to its instance on successful launch; replaced and rejected requests are disposed by their caller.
- `ApplicationInstance` owns its fixed `Window[]` and transferred launch request. The registry removes the exact handle/generation before instance disposal. The instance refuses disposal while it remains registered.
- Service and lifecycle result wrappers are disposed after their status values are consumed. The stale-context request and typed result are disposed after rejection is checked.
- Ring3's owner alias is cleared before handle-based termination.
- The process-table arrays remain global and are not included in per-request cleanup.

No free is selected by diagnostic requester ID. No global sweep or general GC behavior was added. The changes preserve exact owner/generation checks and leave shared/global allocator ownership alone.

## Post-fix build and guest results

Final contained build mode: `Ring3Phase35R13`. Final artifact hashes:

| Artifact | SHA-256 |
|---|---|
| Kernel | `7344AA8AF181B121915250388D2D6ADA96C2F51A76B1FCD1ED027CA09F85959C` |
| Ramdisk | `55B2BB16A58D63CD713E67E3D9566DC6F3A915F1BFD2EF0B15256E942FD9289A` |
| EFI | `6B0B42A51BA212E703F3D67EDD74C823B01CB4C5173A423FE50FEED2F4DF1F33` |

The final audit-only pass reported a 41-artifact cohort, with descriptor agreement `41/41`, build-staging agreement `41/41`, ramdisk-staging agreement `41/41`, and compiled-allowlist agreement `41/41`.

Final guest log: `out/phase35q/serial-86eb699936364a959e41bdf2a73697fd.log`.

| Scenario | B0 pages | B1 pages | B2 pages | B2 − B0 | Accounted | Unknown |
|---|---:|---:|---:|---:|---:|---:|
| Full requester 1 | 14,521 | 17,395 | 14,521 | 0 | 0 | 0 |
| Full requester 2 | 14,521 | 17,395 | 14,521 | 0 | 0 | 0 |
| No read | 14,521 | 17,368 | 14,521 | 0 | 0 | 0 |
| Minimal successful `state.bin` read | 14,521 | 17,379 | 14,521 | 0 | 0 | 0 |
| NotFound only | 14,521 | 17,368 | 14,521 | 0 | 0 | 0 |

The pre-fix full requester had ten runs survive to B2. In the final build, the same B1-to-B2 cleanup boundary left no retained runs. Its `B2Process` measurement was 14,527 pages before the final request/context cleanup; B2 returned to the 14,521-page baseline. The process table's `0x2000` initialization occurred before B0. The R13 run ledger reported `count=0`, `pages=0`, `accounted=0`, `unexplained=0`, and `overaccounted=0` after cleanup. The final retained-run table was empty.

| Gate | Result |
|---|---|
| First and second full requester | `MAIN_RETURN=35` for each; both `B2-B0=0` |
| No-read, minimal-success, NotFound-only controls | Each `B2-B0=0` |
| Five-lifetime check | `R13_FIVE_LIFETIME_RETURNS=5`; pass marker `1` |
| 25-lifetime stress | `R13_TWENTY_FIVE_LIFETIME_RETURNS=25`; `PHASE35_25_LIFETIME_STRESS=PASS` |
| Full 35-lifetime R13 sequence | All measured deltas `0`; `PHASE35_R13_AUDIT_COMPLETE=1` |
| Allocator stability | `PHASE35_STRESS_ALLOCATOR_STABLE=1` |
| Active storage requests / persistent handles | `0 / 0` |
| Invalid / corrupt / no-pages frees | `0 / 0 / 0` |
| Page fault / general protection / invalid opcode / ABI panic | `0 / 0 / 0 / 0` |
| 8-MiB contiguous allocation | `0x800000` bytes; `PHASE35_R13_8MIB_ALLOCATION=PASS` |
| NotFound string ledger | `6/6` exact Dispose and accepted free; zero survivors |
| QEMU test image | Restored to pristine SHA-256 `2EFE22E39E112105854DBC4784887CE06A64A040EC98D50FB52DD7A7FE94B451` |

The final helper snapshots had zero survivors/pages for `RhpNewArray`, `RhpNewFast`, unknown helper, and every allocator site. In the full requester snapshot, the helper counters were balanced (`RhpNewArray`: 4,921 allocations/frees; `RhpNewFast`: 99 allocations/frees). The pre-fix eight-page repeating set did not recur. The result class is an application-model/service-context ownership defect, not reusable allocator capacity: the pre-fix control deltas repeated linearly, and the post-fix full and control deltas all stayed at zero.

## Bounded P2 reboot-delete follow-up

The P2 registry only runs the delete verification when `PersistentSeedWritesPerformed == 0` and the fixture status is `Verified`. On the first seed boot, `PHASE35P2_REBOOT_DELETE=FAIL` is the required marker because this is before reboot verification. The P2 harness expects the first marker to be `FAIL` and the same-image verification boot to emit `PASS`; the existing P2 document records that sequence, and historical log `serial-37bdeb751bb2477a9aaf97b1f261b829.log` contains both markers.

The bounded current rerun used the same test image across the prescribed resets. Its seed boot passed storage gates, reported fixture `Seeded` with one seed write and the expected `PHASE35P2_REBOOT_DELETE=FAIL`, then reached `APP_MODEL_COMPLETE`. The verification boot reached the Phase 35Q completion marker and faulted while creating widgets before the P2 verification marker: general-protection fault, vector `0x0D`, error code `0`, RIP `0x1000FE24`. Thus this rerun did not establish the verification boot's delete result.

To test whether the R13 ownership edits caused this, the nine changed app-model files were temporarily restored to their live-source snapshot in the sandbox and the P2 boot was repeated. The baseline seed boot again emitted the expected first-boot `FAIL`; its verification boot faulted in widget startup at a breakpoint, vector `0x03`, RIP `0x10007538`. The app-model files were then restored to the R13 snapshot and rebuilt. The differing exception with the same startup location indicates a current verification-boot startup problem independent of the R13 disposal edits. No FAT delete or flush change was made. The production delete contract is not classified as defective by these results; fresh same-image qualification remains open.

## Qualification still open

The final R13 kernel artifact hash changed from `BB81B582...` to `7344AA8A...` across consecutive builds from the same source snapshot and command. The earlier artifact copy was not preserved for byte-level comparison, so artifact reproducibility is unverified. This remains for the R7/Gate 39 qualification pass.

The post-stability R7 closure gates were not run because the fresh P2 verification boot did not reach `PHASE35P2_REBOOT_DELETE=PASS`. Outstanding gates are:

- Fresh same-image managed-read reboot with `seedWrites=0`, post-reboot return `35`, and exact fixture hash.
- DSDT default-memory and 1280-MiB runs.
- Artifact reproducibility and mutation rejection.
- Phase 26–34 controls, AppModel, Compatibility, and Phase 8–11.
- Lifecycle `15/15`, taskbar grouping `16/16`, and ordinary full build.

Phase 35 remains incomplete. Phase 36 remains gated. The next narrow action is to diagnose the verification-boot widget startup fault without bypassing the same-image delete gate, then rerun the current P2 same-image sequence and resume the listed R7 gates.

## Source transfer

After sandbox validation, the reviewed source/script/document changes were copied into the live `main` checkout. The copied source set is `Corlib/System/Object.cs`; `Kernel/Misc/Allocator.cs`, `Process.cs`, `Ring3Abi.cs`, `Ring3Proof.cs`, `Ring3ProofPhase33.cs`, and `Ring3ProofPhase35.cs`; `Scripts/run_phase35q_storage_validation.ps1`; `UserManagedPersistentStorageProof/Program.cs`; `build.ps1`; `guideXOS/OS/ApplicationFactories.cs`, `ApplicationInstance.cs`, `ApplicationLifecycle.cs`, `ApplicationServiceRegistry.cs`, `ApplicationServices.cs`, `ApplicationStorageServices.cs`, `ModernAppModel.cs`, and `PersistentFatBackend.cs`; and `guideXOS/guideXOS.csproj`. This report is `Docs/PHASE35R13_RESIDUAL_ALLOCATOR_LIFETIME.md`.

No `ramdisk.img`, generated executable, generated descriptor, test image, EFI, kernel, cache, or other sandbox build output was copied to live. After transfer, the live checkout remained on `main` at `881c5b01c2d9d47801ca0f0baead0ee2f5fde981`, with 18 tracked source/script files modified and this new report untracked. `git diff --check` reported no whitespace errors. The nested runtime remained at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with its original 58 status entries. The source transfer was hash-checked against the sandbox; no generated output was copied.
