# guideXOS UEFI Task Manager Memory Details R3 Ownership Qualification

**Current status — 2026-10-19:** **Outcome A — v0.2 Task Manager Memory Details blocker closed.** R3 identified and repaired the exact Task Manager object-run lifetime defect, classified all complete B0–B6 retained runs, and showed that the remaining production live-page increase is first-use/global cache capacity that plateaus and is reused. The full production build and requested QEMU regressions pass. No bare-metal qualification was run.

This resumed qualification supersedes the dated power-down checkpoint and the earlier provisional R1/R2 baseline below. Those historical sections and the separate R1/R2 reports are retained.

## Resumed final qualification — 2026-10-19

### 1. Outcome and root cause

The R2 +2,185-page 25-lifetime result was real current-live growth, but it was not one leak and did not identify its owners. R3 separated repeated allocations from post-cleanup retention. The R3 fixes removed several confirmed short-lived allocation defects and released a Task Manager managed object whose exact run had been retained past its documented application/window lifetime. Complete post-fix inventories contain no Task Manager window or Memory Details label at B6.

The remaining first-use growth belongs to process-lifetime global owners: StringPool caches, WorkspaceManager static lists/backing arrays, and the current Taskbar clock cache entry. They are bounded singleton/cache state. Old clock entries are disposed when the displayed minute changes. On the production mixed-tab workload, post-close current pages rose by +81, +100, +108, +110, then +110 over five window lifetimes; cycles 4 and 5 were identical at 4,825 live pages / 1,340 runs, with the same 211,837-page largest contiguous free run. Thus the growth plateaus and reusable capacity is not being consumed per Task Manager lifetime.

The previous defects and accepted repairs are:

| Evidence-backed producer / defect | Owner and intended lifetime | Repair |
|---|---|---|
| Task Manager managed window object remained live after `TaskManager.Dispose`; `Window.Dispose` is a `new` method and does not call `System.Object.Dispose`. The exact object run was 688 requested bytes / one page in R2. | Task Manager application/window instance; lifetime ends after its last window closes and shell references are removed. | Queue only disposed Task Manager objects during `WindowManager` cleanup. After application ownership is detached, the window is removed, taskbar registry is reconciled, and taskbar/Start Menu/workspace/show-desktop/compatibility references are cleared, call exact managed-object `Dispose` through `ReleaseDisposedWindowObjectRun`. No owner-wide speculative free is used. |
| Two `RecentProgramEntry[1]` arrays from duplicate-path `_programs.ToArray()` calls, one page each. | `RecentManager.AddProgram` call; should end after duplicate lookup. | Use direct indexed reads. R3’s two- and five-lifetime traces show the pair absent. |
| Taskbar clock formatting allocated a digit string and did not dispose it when adding a leading zero. Earlier traces showed the repeated formatted-string cohort. | Taskbar formatting temporary; ends after the cached display string is assembled. | `FormatClockComponent` disposes the owned digits temporary; callers dispose owned hour/minute strings; replaced cache values are disposed on clock change. |
| Task Manager redraw created and abandoned changing strings, snapshot results, chart buffers, sampling arrays, and lifecycle/factory result containers. | Respective draw, refresh, chart, request, or factory transaction scope. | Scope-owned temporaries/results are disposed at the end of use; chart resize/close releases buffers; fixed-capacity arrays replace growing history; retained Memory Details labels remain privately owned and are disposed on refresh/close. |

R3 did not find a repeating Task Manager refresh, tab-instance, window-surface, compositor, or application-instance run surviving B6. The R2 residual is best explained by this combination of real narrow lifetime defects plus bounded global first-use state and workload/background activity, not by equating the R2 page delta to one owner.

### 2. Live and sandbox state

| Location | Branch / HEAD | State |
|---|---|---|
| Live `D:\dev\guideXOSUEFI` | `main`; accepted R3 source and report committed locally on top of base `b4af3c8fd92dcbd35ec594bd33e6b1d87d521d41`; upstream was 0/0 before integration | Integration contains only reviewed source, regression, R2/R3 probe, and documentation files. No generated runtime/build artifacts copied. Not pushed. |
| R3 sandbox `C:\gxR3work` | `main` at base `b4af3c8fd92dcbd35ec594bd33e6b1d87d521d41`; upstream 0/0 | 30 root status entries at qualification close: 26 modified tracked files and 4 untracked files. All build/test evidence remains below `out\taskmanager-memorydetails-qualification-r3`. |
| R2 sandbox `C:\gxR2` | `main` at the same base; upstream 0/0 | 18 root status entries (16 modified, 2 untracked), preserved. |
| Nested runtime in live, R2, and R3 | `out\rt` HEAD `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` | Each had 58 existing status entries. They were not cleaned, reset, or integrated. |

The original and partially relocated validation-lane paths were left outside this work and were not modified during resumed R3 work. R1 and R2 reports/logs/images remain preserved. The local live commit does not push or alter the dirty nested `out\rt` checkout.

### 3. Production image identity and full build

Frozen final production candidate: `C:\gxR3work\out\taskmanager-memorydetails-qualification-r3\candidate-r3-full-production-clock-fix-20261019`.

| Artifact | SHA-256 |
|---|---|
| Kernel | `F20158F11303A0FA32A249D1DC4B634D4B95ED23F69D62B809E37C0BDEA17961` |
| Ramdisk | `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8` |
| EFI | `154991DE7B8A88D213E33C3514D09DEAD65C0769B39B2011BB0D672184DD0609` |

The complete unskipped `C:\gxR3work\build.ps1` run is recorded in `C:\gxR3work\out\taskmanager-memorydetails-qualification-r3\r3-full-production-build-clockfix-20261019.log`: **Build Complete**, exit 0, zero errors. Descriptor, build staging, ramdisk staging, and compiled allowlist agreed at **41/41**; the accepted Phase 35 registered mode audit remains **9/9**. Final focused regression log: `C:\gxR3work\out\taskmanager-memorydetails-qualification-r3\focused-regression-final-clockfix-20261019.log`, **PASS** for ownership, bounds, unavailable data, overflow arithmetic, and invalid-to-valid recovery source checks.

### 4. Allocator metric and capture quality

`Allocator.MemoryInUse` is `_Info.PageInUse * PageSize` (`PageSize = 4096`). A successful allocation increments current live pages by the whole run; a successful free decrements it by the whole run. The separate `AllocatedPageTotal` and `FreedPageTotal` are cumulative **page** counts. `LiveRunCount` is current live runs; maximum and interval-maximum values are high-water metrics, not current use. `PageSignature` marks reserved pages and is excluded from `PageInUse`; a page-map entry of zero is immediately reusable. The counter includes kernel, runtime, compositor, application, and other users of this global allocator, so unrelated background work affects whole-system snapshots. Negative B0–B6 deltas and production A1/A2 allocated-equals-freed totals prove that current-live pages can decrease.

R3 used fixed-size static numeric records and scopes: 65,536 run slots, numeric run/allocation-site/window/instance/lifetime IDs, address and run identity, requested bytes, page count, EEType/managed length/object kind, free sequence, dispose observations, and explicit dropped-record counters. The interval hot path does not format strings, emit per-allocation serial messages, or write files. Evidence is emitted after B6. All five single-tab lifetimes report `captureComplete=1`, `netCheck=1`, and zero dropped records. `R3_EMIT` reports zero allocated pages, freed pages, and live-page delta. The separate five-window mixed-tab diagnostic exceeded its capture ring after cycle 2 and explicitly marked cycles 3–5 incomplete; those rows are excluded from attribution. The production mixed-tab run is nondiagnostic and complete for the stability trend.

### 5. Diagnostic overhead calibration

Comparable short one-lifetime Memory Details workloads used production, R3 no-emission, and R3 deferred-emission images. Each completed at the same +53 post-cleanup live pages despite different boot baselines and gross traffic. The comparison isolates bounded instrumentation overhead; it does not treat differing allocation traffic as equal.

| Mode | Kernel SHA-256 | Allocated / freed pages | Post-cleanup live delta |
|---|---|---:|---:|
| Production | `5A992D4766F7E49F138847CB120BCE14EFD34F0DCEE554367AF4D159B0591946` | 5,496 / 5,443 | +53 |
| R3 provenance, no emission | `CD8954C7B6C87484761EA13E94967636ED290E8E74A3B40992571B419005DD83` | 6,059 / 6,006 | +53 |
| R3 provenance, deferred emission | `247D593D1C86812406D99DD2A839BA05811DDA5EA8BDA8326ADCF958259F4941` | 5,503 / 5,450 | +53 |

The diagnostic arrays are fixed static storage, so their boot footprint is not request/window-proportional. Deferred emission added zero pages. The preserved `calibration-noemit\D` run is invalid because the generic production parser decoded a probe snapshot at the wrong offsets; it is not used in this comparison or the trend.

### 6. Matched production controls and allocation/free trends

The A–F runs used the same frozen final production image, guest configuration, and fixture. Both idle controls ran for 786 seconds. No linearly extrapolated idle value was subtracted: the repeated idle controls each ended with zero net live growth and provide no positive idle slope to subtract.

| Scenario | Workload / boundary | Allocated / freed pages during measured workload | Live pages: start → peak/open → after cleanup | Final run / capacity result |
|---|---|---:|---:|---|
| A1 | Idle desktop, 786 s | 70 / 70 | 4,644 → 4,644 → 4,644 | 1,186 runs; boot high-water 5,739; 256,346 reusable pages; net 0 |
| A2 | Repeated idle desktop, 786 s | 65 / 65 | 4,702 → 4,702 → 4,702 | 1,244 runs; boot high-water 5,931; 256,288 reusable pages; net 0 |
| B | Processes tab open 786 s, close, +2 s cleanup | 80,109 / 80,056 | 4,715 → 5,128 → 4,768 | 1,283 final runs; boot high-water 6,092; 256,222 reusable pages; +53 net; 2 UI checks |
| C | Memory Details open 786 s, leave tab, close, +60 s cleanup | 12,467 / 12,414 | 4,715 → 5,142 → 4,768 | 1,283 final runs; boot high-water 6,092; 256,222 reusable pages; +53 net; 4 UI checks |
| D | Five Memory Details window lifetimes | 12,626 / 12,572 | 4,702 → five post-close samples: 4,755, 4,756, 4,756, 4,756, 4,756 | 1,271 final runs; boot high-water 5,931; 256,234 reusable pages; +54 net; cycles 3–5 stable; 22 UI checks |
| E | 25/25 Memory Details lifetimes (786 s), +60 s deferred cleanup | 62,953 / 62,899 | 4,715 baseline → 4,768 after cycle 1 → 4,769 after cycle 2 and through deferred cleanup | 1,284 final runs; boot high-water 6,092; 256,221 reusable pages; +54 net; 100 UI checks |
| F | 50 tab transitions in one Task Manager lifetime | 6,057 / 5,980 | 4,715 → 5,278 → 4,792 | 1,307 final runs; boot high-water 6,092; 256,198 reusable pages; +77 net; 52 UI checks |

The final production snapshots all retained 1,154 reserved pages and the same 211,837-page largest contiguous free run. “Reusable pages” above are free page-map entries; reserved pages are excluded from live pages.

The repeated five-window production mixed-tab run performed 50 transitions per window (250 total): baseline 4,715 pages / 1,257 runs; after each close + cleanup: 4,796 / 1,311, 4,815 / 1,330, 4,823 / 1,338, 4,825 / 1,340, and 4,825 / 1,340. Open cycles 4 and 5 both reached 5,317 pages / 1,384 runs. The B0→final interval allocated 29,657 pages and freed 29,547; final reusable pages were 256,165, boot high-water was 6,092, and largest contiguous free capacity stayed 211,837 pages. Allocator consistency errors and all three free-failure counters remained zero; 260 UI checks passed. The last two windows did not add live pages or runs after cleanup.

### 7. Complete B0–B6 lifecycle snapshots

The representative second lifetime in `C:\gxR3work\out\taskmanager-memorydetails-qualification-r3\lifecycle-five-post-clock-fix-single-tab-20261019\serial.log` separates creation, Memory Details activation, refresh, tab departure, close, and deferred cleanup. Allocated/freed columns are cumulative page totals relative to B0. The interval maximum is current-live high water for this interval; all boundaries had zero invalid/no-pages/corrupt frees and zero dropped records.

| Boundary | Allocated / freed pages | Live pages / runs | Interval max pages | Reusable pages | Successful free calls, cumulative |
|---|---:|---:|---:|---:|---:|
| B0 before creation | 0 / 0 | 5,317 / 1,229 | 5,317 | 250,823 | 27,235 |
| B1 after app/window creation | 376 / 4 | 5,689 / 1,265 | 5,689 | 250,451 | 27,239 |
| B2 after Memory Details activation | 623 / 817 | 5,123 / 1,255 | 5,690 | 251,017 | 27,496 |
| B3 after one refresh | 645 / 825 | 5,137 / 1,269 | 5,690 | 251,003 | 27,504 |
| B4 after leaving Memory Details | 661 / 841 | 5,137 / 1,269 | 5,690 | 251,003 | 27,520 |
| B5 after window close and owner/shell cleanup | 884 / 1,444 | 4,757 / 1,225 | 5,690 | 251,383 | 27,787 |
| B6 after deferred cleanup (2,008 ticks) | 884 / 1,444 | 4,757 / 1,225 | 5,690 | 251,383 | 27,787 |

B0→B6 therefore reconciles as 884 allocated pages − 1,444 freed pages = −560 live pages. The new-run inventory has 1 retained page less 561 freed baseline pages = −560. `netCheck=1`; the allocator counter demonstrably decreases. No Task Manager object, Memory Details label, per-window sampling array, chart buffer, or Task Manager factory/service result remains live at B6.

### 8. Retained-run inventory and logical owners

The first complete B0–B6 lifecycle retained 36 pages across 11 exact allocator runs, all process-global first-use state. The run IDs and addresses below are from this specific R3 probe boot; addresses can vary on other boots. These runs have no dispose observation because their real owner outlives one Task Manager window.

Each listed run recorded allocator operation 6 (`NativeRuntimeMalloc`), `FreeSeq=0`, `DisposeCount=0`, `WindowId=0`, and `InstanceId=0` at B6. The exact run identities therefore point to global managed allocations rather than the Task Manager window or application instance.

| Run ID / address | Pages / requested bytes | Object class | Actual producer and logical owner | B6 disposition / reason live |
|---|---:|---|---|---|
| 27934 / `0x5226000` | 1 / 832 | `string[101]` | `StringPool` static constructor (`0x10012DD8`) | Process-lifetime percentage cache; no dispose expected. |
| 27935 / `0x5296000` | 20 / 80,032 | `string[10001]` | `StringPool` static constructor (`0x10012DD8`) | Process-lifetime number cache; no dispose expected. |
| 27936 / `0x5259000` | 4 / 16,024 | `string[2000]` | `StringPool` static constructor (`0x10012DD8`) | Bounded process-lifetime memory-size cache; no dispose expected. |
| 27937 / `0x52AA000` | 4 / 16,024 | `ulong[2000]` | `StringPool` static constructor (`0x10012DD8`) | Keys for the bounded memory-size cache; no dispose expected. |
| 27938 / `0x5233000` | 1 / 24 | cached string `"0"` | `StringPool.GetNumber(int)`, numeric creation site 412 | Shared number cache; no dispose expected during normal process life. |
| 28436 / `0x5239000` | 1 / 32 | `List<Window>` object | `WorkspaceManager` static constructor (`0x1009F9D8`) | Global workspace keys list. |
| 28437 / `0x523A000` | 1 / 1,048 | `Window[128]` backing array | Same `WorkspaceManager` static constructor | Global workspace keys capacity. |
| 28438 / `0x523B000` | 1 / 32 | `List<int>` object | Same `WorkspaceManager` static constructor | Global workspace values list. |
| 28439 / `0x523C000` | 1 / 536 | `int[128]` backing array | Same `WorkspaceManager` static constructor | Global workspace values capacity. |
| 28440 / `0x523D000` | 1 / 32 | `List<Window>` object | Same `WorkspaceManager` static constructor | Global minimized-by-switch list. |
| 28441 / `0x523E000` | 1 / 536 | `Window[64]` backing array | Same `WorkspaceManager` static constructor | Global minimized-by-switch capacity. |

The only other newly retained runs over the five complete lifetimes were the single 32-byte current `_cachedTime` string owned by the global Taskbar clock: run 28956 at `0x52B6000` (lifetime 2), run 29582 at `0x5242000` (lifetime 3), and run 30553 at `0x52B6000` (lifetime 5), all site 1805. Cycle 4 crossed no minute boundary. Prior cached values were released on replacement; these records are the one current value at each B6. The total new-retained pages by completed lifetime are therefore **36, 1, 1, 0, 1**. No cycle adds a Task Manager/window-owned retained page.

Logical-owner classification at B6:

| Owner class | Observed result |
|---|---|
| Memory Details refresh | Refresh result and short-lived formatted values are disposed after use; no retained run. |
| Memory Details tab instance | Fourteen private label strings are refreshed/disposed by their owner; no label remains after close. StringPool-returned strings are never disposed by Task Manager. |
| Task Manager window | Window-owned runs and chart/sampling resources are released during dispose; exact managed window object is released after alias removal; absent at B6. |
| Task Manager application instance | Last-window-close policy detaches and terminates the instance; launch/factory/lifecycle result containers and service context are released; no retained instance run from the lifecycle inventory. |
| Shared compositor / window surfaces | Surfaces created for this window are released on close; no newly retained compositor/window-surface run in the complete B6 inventory. Existing shared baseline remains global. |
| Global runtime / global caches | StringPool and WorkspaceManager static caches/lists persist intentionally; one current Taskbar clock string is replaced on time change. All are bounded process-lifetime state. |
| Reusable allocator capacity | Freed map pages immediately become available. Production largest contiguous free capacity stayed fixed at 211,837 pages across repeated mixed-tab stress. |
| RecentManager / other | Duplicate lookup snapshots were removed; no repeated `RecentProgramEntry[1]` cohort remained in two/five-lifetime traces. |

### 9. Two- and five-lifetime attribution; plateau

Post-clock-fix single-tab R3 two-lifetime and five-lifetime runs have complete B0–B6 capture, zero dropped records, and `netCheck=1`. In the five-lifetime trace, B0→B6 allocated/freed/net/new-retained pages are:

| Lifetime | Allocated / freed pages | Whole-allocator net | New live pages, by identified owner |
|---|---:|---:|---|
| 1 | 914 / 1,438 | −524 | 36, first-use StringPool and WorkspaceManager global state |
| 2 | 884 / 1,444 | −560 | 1, current Taskbar clock cache value |
| 3 | 884 / 1,444 | −560 | 1, current Taskbar clock cache value |
| 4 | 879 / 1,439 | −560 | 0 |
| 5 | 884 / 1,444 | −560 | 1, current Taskbar clock cache value |

After the first-window initialization, there is no per-window retained-page sequence. The whole allocator may end lower than B0 because window cleanup also frees pre-existing baseline runs. The inventory and allocation/free totals reconcile separately; neither is inferred by subtracting an idle slope. Production single-tab 25-lifetime use is +53 after cycle 1, +54 after cycle 2, and remains +54 through cycle 25 and the extra 60-second cleanup. Production mixed-tab use reaches +110 by cycle 4 and remains +110 at cycle 5. The 211,837-page contiguous run remains intact, so the bounded retained cache state has not reduced the tested contiguous-allocation capacity.

### 10. Snapshot validity, desktop/UI, counters, and post-fix acceptance

- **Production Task Manager stress:** 25/25 Memory Details entry/exit cycles; 50/50 mixed tab transitions; five completed windows each with 50 transitions; all four Task Manager tabs visited; close/reopen completed. QEMU mouse input routed through the PS/2 mouse. Desktop smoke opened and closed the Files desktop icon, launched Task Manager from Start, closed it, and returned to a healthy desktop.
- **Invalid snapshot:** final-source diagnostic G run displayed `N/A` for an invalid snapshot and recovered on the next valid snapshot; Processes was reselected and close completed without faults. Summary: `C:\gxR3work\out\taskmanager-memorydetails-qualification-r3\invalid-snapshot-recovery-r3-final-source-20261019\G\summary.json`.
- **Desktop startup:** final production image smoke verified Files icon open/close, Start-menu Task Manager launch, Task Manager close, and desktop responsiveness. Summary: `C:\gxR3work\out\taskmanager-memorydetails-qualification-r3\desktop-smoke-r3-final-production-clock-fix-20261019\summary.json`.
- **Allocator/fault counters:** A–F, production mixed five-lifetime, and complete R3 single-tab lifecycle runs reported zero invalid-pointer, no-pages, or corrupt-run frees and zero allocator consistency errors. Serial fault scans report no unexpected CPU exceptions. Production mixed-tab run had 260 UI checks and no serial fault lines.
- **Crash-repair safety:** all 14 Memory Details labels remain private Task Manager-owned values; borrowed StringPool strings are not disposed. Refresh replaces owned labels safely; window close releases them. Snapshot size/bounds checks, overflow-safe percent math, and invalid-data `N/A` recovery remain in place. Focused regression passes.
- **Bare metal:** not run. R3 qualifies the requested QEMU production path; hardware qualification remains a separate status item.

### 11. Integration and final decision

The accepted R2/R3 changes and this report are integrated as a local commit on the existing live `main`. Review compared the sandbox against live base `b4af3c8fd92dcbd35ec594bd33e6b1d87d521d41`; only the 26 accepted tracked source/build/regression files and four R2/R3 report/probe files were transferred. Generated kernel, ramdisk, EFI, symbol-map, ESP, `out`, and runtime-cache artifacts were not copied. `git diff --check` passed before integration. The existing dirty nested `out\rt` state and all R1/R2 evidence were preserved. Nothing was pushed.

**Final v0.2 Task Manager Memory Details decision: blocker closed (Outcome A; the confirmed window-object lifetime defect was repaired, corresponding to Outcome D).** The full production build, artifact agreement, focused regression, invalid-snapshot recovery, desktop startup, 25-cycle stress, 50-transition stress, two/five-lifetime ownership traces, matched idle controls, and repeated mixed-tab plateau are green. There is no evidence of unbounded Task Manager-owned live growth after B6. Bare-metal qualification was not run.

## Historical power-down checkpoint — 2026-10-18 13:54 PDT

Work is stopped at a safe boundary. No build or lifecycle harness is running. The R3 QEMU from `lifecycle-five-report-fix` (PID 12040) was cleanly closed through QMP after its evidence had been saved. One older QEMU remains running from `C:\gxR2\out\taskmanager-memorydetails-qualification-r2\evidence-r13-string-sites\C` (PID 34980); it was left untouched with its R2 evidence. The two moved validation-lane paths were checked for presence only and were not read, changed, moved, or deleted.

### Exact checkout state

| Location | Branch / HEAD | Top-level state | Nested `out\rt` |
|---|---|---|---|
| Live `D:\dev\guideXOSUEFI` | `main`, `b4af3c8fd92dcbd35ec594bd33e6b1d87d521d41`, `origin/main` ahead 0 / behind 0 | Clean: 0 status entries | `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; 58 status entries, untouched |
| R2 `C:\gxR2` | `main`, same HEAD, ahead 0 / behind 0 | 18 entries: 16 modified, 2 untracked | Same nested HEAD; 58 status entries |
| R3 `C:\gxR3work` | `main`, same HEAD, ahead 0 / behind 0 | 25 entries: 21 modified, 4 untracked | Same nested HEAD; 58 status entries |

The R2 root modifications are `Corlib/System/StringPool.cs`, `Kernel/Misc/Allocator.cs`, `Kernel/Misc/EntryPoint.cs`, `Tests/TaskManagerMemoryDetailsRegression.ps1`, `build.ps1`, `docs/UEFI_TASKMANAGER_MEMORY_DETAILS_CRASH_FIX.md`, `guideXOS/DefaultApps/TaskManager.cs`, `guideXOS/GUI/{Desktop,StartMenu,Taskbar,WindowManager}.cs`, `guideXOS/OS/{ApplicationFactories,ApplicationInstance,ApplicationServiceBackends,Program}.cs`, and `guideXOS/guideXOS.csproj`. Its untracked files are `Docs/UEFI_TASKMANAGER_MEMORY_DETAILS_R2_ALLOCATOR.md` and `Kernel/Misc/TaskManagerMemoryR2Probe.cs`.

The exact R3 root modifications are `Corlib/System/StringPool.cs`, `Kernel/Misc/Allocator.cs`, `Kernel/Misc/EntryPoint.cs`, `Tests/TaskManagerMemoryDetailsRegression.ps1`, `build.ps1`, `docs/UEFI_TASKMANAGER_MEMORY_DETAILS_CRASH_FIX.md`, `guideXOS/BootConfig.cs`, `guideXOS/DefaultApps/TaskManager.cs`, `guideXOS/GUI/{Desktop,RecentManager,StartMenu,Taskbar,Window,WindowManager}.cs`, `guideXOS/OS/{AppModel,ApplicationFactories,ApplicationInstance,ApplicationServiceBackends,ApplicationServiceRegistry,Program}.cs`, and `guideXOS/guideXOS.csproj`. Its untracked files are `Docs/UEFI_TASKMANAGER_MEMORY_DETAILS_R2_ALLOCATOR.md`, this R3 report, `Kernel/Misc/TaskManagerMemoryR2Probe.cs`, and `Kernel/Misc/TaskManagerMemoryR3Probe.cs`. All source changes and the partial run/build evidence remain in `C:\gxR3work`; the R2 copy remains separately preserved. No reset, clean, stash, branch change, worktree, integration, commit, or push was performed.

The original validation lane `D:\dev\guideXOSUEFI-validation-lane-e9d8b6edb4794fbb92190cffbd05ce51` and the partial destination `C:\dev\guideXOSUEFI\guideXOSUEFI-validation-lane-e9d8b6edb4794fbb92190cffbd05ce51\out` both exist. Only their paths were checked. They remain outside the R3 working lane and untouched.

### R3 source work and retained evidence

R3 adds bounded, fixed-record allocator provenance and lifecycle snapshots, numeric creation-site and instance context, and deferred evidence emission to `Kernel/Misc/TaskManagerMemoryR3Probe.cs` and the allocator/entry-point changes. It also changes Taskbar visual-status reporting to emit literal/numeric components rather than transient concatenated strings, and changes duplicate lookup in `RecentManager.AddProgram` from two `_programs.ToArray()[i]` snapshots to direct indexed reads. No speculative free was added.

The exact repeated `RecentProgramEntry[1]` pair was two one-page, 48-byte arrays at site 1601, created by those two `ToArray()` calls. It repeated in completed lifetimes 2–5 before the fix and was absent in the subsequent two- and five-lifetime R3 runs. The prior Taskbar report-string cohort (20 strings, 968 requested bytes per completed window) is absent after the formatter change. These are narrow, evidence-supported removals; they do not resolve overall memory ownership.

The latest two- and five-lifetime probe runs are preserved at:

- `C:\gxR3work\out\taskmanager-memorydetails-qualification-r3\lifecycle-two-recentmanager-fix`
- `C:\gxR3work\out\taskmanager-memorydetails-qualification-r3\lifecycle-five-recentmanager-fix`

Both run summaries record the same image hashes: kernel `563328BA8F3DD21392240B31401EC4B66CB907C32D0B343DD25B14C145FA31FE`, ramdisk `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8`, EFI `CD8C4DA42C4C40A9EA693CCA5ED969C566081BBCBEA2049D6F3A746565265750`. The allocator virtual/physical bases are `0x10156F20` / `0x4C710F20`. Each run has zero serial fault lines, zero free-invalid/no-pages/corrupt-run failures, no allocator consistency errors, and UI state checks of 8/8 and 20/20 respectively. The targeted builds in `build-r3-recentmanager-fix.log` and `build-r3-noemit.log` both say `Build Complete`, `MANAGED_ARTIFACTS=41`, `DESCRIPTOR_AGREEMENT=41/41`, and `BUILD_STAGING_AGREEMENT=41/41`. These are contained diagnostic builds, not the final unskipped production build.

The R3 allocator metric is current `PageInUse`: successful allocation adds the run page count; successful free subtracts it; `PageSignature` reserved pages are excluded; a freed run is immediately reusable. The separate allocated/freed page totals are cumulative page counts, while high-water values are separate. Global/runtime/kernel/compositor/application allocations through this allocator are included, so background work can affect snapshots. The captured decreases demonstrate that the live counter can go down. Diagnostic deferred emission changed allocated/freed/live-page counters by zero.

The latest five-lifetime snapshots show B0-before-click to deferred-cleanup live pages of `5355→4839`, `5476→4930`, `5520→5020`, `5610→5064`, and `5654→5108`; current live-run counts at those endpoints are `1292→1307`, `1388→1398`, `1432→1488`, `1522→1532`, and `1566→1576`. Cumulative allocated/freed pages and peak live pages are in each `summary.json`. These net drops coexist with newly retained runs because unrelated/background frees occurred during the intervals; the whole-allocator net delta is not explained by the new-run inventory. The five-run diagnostic trend is not a matched production trend and does not prove a plateau.

The lifecycle capture has five allocator points per lifetime: before Task Manager click, window open, Memory Details, left Memory Details, and deferred cleanup. It does **not** yet separately capture all required B0–B6 boundaries: activation and refresh are conflated, and there is no separate post-window-close B5 snapshot. Inventory row counts for the latest two/five runs are 28/107 total across those runs; cycle-level retained pages in the five-run inventory are 41, 11, 58, 11, and 11. A 46-string unscoped cohort (fallback creator 401, 2,832 requested bytes) appears in lifetime 3, along with one 32-byte TaskbarClockFormat string (site 1805). A retained TaskManager object also remains after `DisposeCount=1` with no allocator free observed. Their logical owners, intended lifetimes, and safe cleanup remain unresolved.

The current `C:\gxR3work\ESP` staging was overwritten by the `TaskManagerMemoryR3ProbeNoEmit` calibration build. Use the preserved per-run images and summaries above for the accepted probe evidence. The partial run at `out\taskmanager-memorydetails-qualification-r3\calibration-noemit\D` is intentionally preserved but invalid: the generic production snapshot parser decoded the R3 probe layout at the wrong offsets and produced impossible values. Do not use those results. The no-emit build log itself completed successfully.

### Remaining work and release decision

Classify the retained TaskManager object and the lifetime-3 unscoped strings; trace remaining retained allocation sites (including sites 1101, 1102, 1201, 1301, 1302, 1401, and 1501) to actual producers and logical owners; establish alias/last-use and intended-lifetime safety before any free; complete separate B0–B6 captures; and correct the no-emit snapshot decoder before diagnostic-overhead calibration. Matched production A–F controls, repeated idle control, valid disabled/enabled/deferred-emission overhead comparison, full unskipped build, invalid-snapshot regression, fresh production acceptance matrix, live-source integration, `git diff --check`, local commit, and bare-metal status are still outstanding. Do not start another long 25-lifetime run until per-owner accounting and calibration are valid. The current overall classification is **Outcome F**; the v0.2 blocker remains open.

### Resume commands (do not execute as part of this checkpoint)

Start in the contained copy and always use a new output directory so no prior evidence is overwritten:

```powershell
Set-Location C:\gxR3work
```

After first correcting/validating the diagnostic-layout decoder, a fresh R3 probe image can be built with:

```powershell
.\build.ps1 -SkipBootloader -SkipRamdisk -UefiDiagnosticMode TaskManagerMemoryR3Probe
```

Then run short attribution lifetimes into unique directories (the existing names are already occupied):

```powershell
python .\out\taskmanager-memorydetails-qualification-r3\run_r3_lifecycle.py --candidate C:\gxR3work --output C:\gxR3work\out\taskmanager-memorydetails-qualification-r3\<new-unique-two-run-name> --allocator-base 0x10156F20 --cycles 2
python .\out\taskmanager-memorydetails-qualification-r3\run_r3_lifecycle.py --candidate C:\gxR3work --output C:\gxR3work\out\taskmanager-memorydetails-qualification-r3\<new-unique-five-run-name> --allocator-base 0x10156F20 --cycles 5
```

Do not feed `TaskManagerMemoryR3ProbeNoEmit` captures into the generic production-layout parser. Before any later integration, compare the R3 diff against live `b4af3c8`, review all R2/R3 source changes, transfer only accepted changes while preserving existing live state, and run `git diff --check` in the live checkout. Do not copy generated runtime artifacts into live `main` or touch either validation-lane path.

## 1. Outcome

Not classified yet. R2 production stress showed unassigned live growth. The R2 diagnostic inventory gives useful allocation-type evidence but is serial-heavy and lacks upstream allocation-site, window, and instance identity. R3 is being run in a contained copy.

## 2. Live and sandbox state

| Checkout | Branch / HEAD | State |
|---|---|---|
| Live `D:\dev\guideXOSUEFI` | `main`, `b4af3c8fd92dcbd35ec594bd33e6b1d87d521d41`; `origin/main`, ahead 0 / behind 0 | Root tracked and untracked status clean. Nested `out\rt` has 58 existing status entries and remains untouched. |
| R2 `C:\gxR2` | `main`, same HEAD; ahead 0 / behind 0 | 18 root status entries (16 modified, 2 untracked) preserved. R1/R2 reports, logs, images, and builds remain in place. |
| R3 build lane `C:\gxR3work` | ordinary copy of R2 source, same HEAD and copied R2 changes | Source copy is complete. Nested `out\rt` copy is in progress; no R3 build has started. |

The copied R2 source changes are not integrated into live `main`. No reset, clean, stash, branch change, worktree, merge, rebase, cherry-pick, or push was performed. A separate partial broad copy at `C:\gxR3` is not used for builds; it is left intact.

## 3. Exact production image hashes available from R2

R2's frozen production candidate is `C:\gxR2\out\taskmanager-memorydetails-qualification-r2\candidate-r2-final-production\candidate-manifest.txt`:

| Artifact | SHA-256 |
|---|---|
| Kernel | `EF76CFBC6A78661EF0FBD4C09CAE9F400BF14B2969C53F66BECAED64B454E759` |
| Ramdisk | `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8` |
| EFI | `4B88FEB3CCB28D83EE0CF20645DDAC31B48146709BFC89F44AA547012F20F599` |
| Kernel map | `A07A08B2D494CC9902390D368BB247C86B487FD0D23CECFB0A6F0C128A250B75` |

This candidate was built from the R2 sandbox at the live base HEAD, in production mode with the R2 diagnostic probe disabled. It passed the R2 production build and artifact audit (41/41 agreement; Phase 35 modes 9/9). R3 image hashes will be added after the contained build completes.

## 4. Allocator metric definition

`Allocator.MemoryInUse` reads `_Info.PageInUse * PageSize`. `_Info.PageInUse` increases by the full run page count after a successful allocation and decreases by the full run page count after a successful free. It is a current-live count, not a cumulative total or high-water mark. Global, kernel, runtime, compositor, and application runs using this allocator are included.

Free page-map entries become reusable immediately after `Free` clears the run. `PageSignature` entries are reserved and are excluded from `PageInUse`. R2 B0–B5 observations include negative live-page deltas and successful frees, proving the live counter can decrease. R2 did not maintain cumulative allocated-page and freed-page totals; `FreeSuccessCount` counts successful free calls/runs, not pages. R3 will add or otherwise capture true per-interval page totals before using them for allocation/free attribution.

## 5. Matched production trends and controls

R2's frozen production candidate and fixture produced:

| Scenario | Duration / workload | Live-page change |
|---|---|---:|
| A | Idle desktop, 900 s | +340 |
| E | 25 Task Manager Memory Details lifetimes, 786 s | +2,185 (including +46 pages in the final 60 s cleanup observation) |

Scaling the idle observation linearly to 786 s gives about +297 pages and a descriptive residual of about +1,888 pages. This is not a leak attribution: duration, workload, and background activity differ, and R2 did not establish that the idle rate is stable. R3 matched A–F measurements and any repeated idle control are pending.

## 6. Allocation, frees, and retention

R2's production snapshots establish net current-live growth only. The R2 Task Manager display's “Allocated Pages” and “Freed Pages” fields are not valid page totals: they derive from `PageInUse + FreeSuccessCount` and `FreeSuccessCount`, which counts runs rather than freed pages. R3 interval page counters and snapshot reconciliation are pending.

## 7. Non-allocating provenance capture

R2's provenance ledger is statically bounded, but its B0–B5 probe emitted about 2.7 MB of serial records during the 25-lifetime run and added roughly 590–639 pages between sampled boundaries. Its cross-cycle slope is contaminated. R3 capture will use fixed-size numeric records, run identity, source-site IDs, window/instance IDs, overflow counters, and deferred emission. Calibration is pending.

## 8. B0–B6 lifecycle capture

R2 captured B0–B5, with B2 combining Memory Details activation and its first refresh, and B4 marking `TaskManager.Dispose` before all window cleanup and taskbar reconciliation completed. R3 must split activation from refresh and capture after final owner cleanup at B5, then after deferred cleanup at B6. The complete R3 snapshot table is pending.

## 9. R2 retained-run inventory

R2 parsed 970 reconciled B5 rows across 25 lifetimes. Cycles 2–25 each report 33 one-page runs (33 pages): 7 managed objects, 6 arrays, and 20 strings. The R2 owner and caller fields do not identify product owners; the 20 strings have no R2-resolved upstream creator. Cycle 1 includes first-use allocations (82 rows / 107 pages).

The repeated 13-page managed-object/array cohort is materially smaller than the R2 production increase and cannot explain its residual alone. R3 will repeat a bounded two- and five-lifetime attribution before considering another long run.

## 10. Source-supported R2 producer candidates

These matches combine R2 EEType/size/array-length rows with source review. They are candidates until R3's source-site and owner IDs confirm each exact run.

| R2 retained type | Repeated count | Source-supported producer / intended owner | Suspected missing cleanup |
|---|---:|---|---|
| `TaskManager` object, 688 bytes | 1 | `TaskManagerApplicationFactory.TryCreateOrActivate`; the Task Manager window/application instance | `TaskManager.Dispose` disposes child owners and calls `Window.Dispose`; `Window.Dispose` is `new`, hides `System.Object.Dispose`, and does not release the exact managed object run. |
| `ApplicationServiceContext`, 40 bytes, plus `string[10]` | 1 + 1 | `ApplicationServiceRegistry.TryCreateContext`; stored by the Task Manager window | `TaskManager.Dispose` does not dispose its owned context. If an existing manager is reused, the newly created unused context also needs explicit disposal. Shared `ApplicationServiceAccess` is not owned by the window. |
| `ApplicationFactoryResult`, `Window[8]`, `bool[8]`, inner `LaunchResult` | 1 + 1 + 1 + 1 | Successful factory-launch transaction in `ApplicationFactoryRegistry.TryLaunch` | Factory result, its owned arrays, and its inner result have no disposal path after attach/activation processing. Window references are borrowed, not owned. |
| `LaunchRequest`, empty `string[0]`, returned `LaunchResult` | 1 + 1 + 1 | `Desktop.LaunchApplication(string)` convenience wrapper | The wrapper creates the request and discards the result without disposing either. The instance separately owns the canonical cloned request. |
| `AppLaunchResolution` | 1 | Temporary resolution in `ApplicationDescriptorRegistry.TryResolve` | The resolver result is projected into descriptor and alias fields, then lost without disposing the result object. Its strings are borrowed. |
| `RecentProgramEntry[1]` snapshots | 2 | Duplicate path in `RecentManager.AddProgram` | Two `_programs.ToArray()` calls create transaction-local arrays that are read and abandoned. Direct indexed reads avoid both allocations. |

R2 run IDs and addresses include the Task Manager run `29985` at `0x530B000` in cycle 2 and run `82473` at `0x5A34000` in cycle 25. Both request 688 bytes, occupy one page, and have no recorded free sequence. R3 will confirm these creators and identify other retained runs with allocation-site instrumentation.

## 11. Logical owners and alias safety

The Task Manager descriptor uses `ReuseExisting`, closes the application instance when its last window closes, and does not allow zero windows. That scope ends on window close. However, an object-run release must occur only after `WindowManager.Windows`, application-instance ownership, taskbar projections, workspace lists, show-desktop lists, and Start Menu window snapshots no longer hold or use the window. R3 is auditing and testing those aliases before changing window-object reclamation.

## 12. Remaining R3 evidence

The following sections will be completed with R3 measurements and reviewed changes:

- Diagnostic overhead calibration and provenance overflow results.
- Complete B0–B6 allocator snapshots and retained-run inventory.
- Exact R3 source-site producers and logical-owner table.
- Two- and five-lifetime retained pages per owner; reuse/plateau evidence.
- Any evidence-backed fix, alias/last-use review, and fresh production qualification.
- Invalid-snapshot result, free/fault/CPU counters, full build and artifact audit.
- Live-source integration decision, bare-metal status, outcome classification, and next narrow action.

R1 and R2 reports are preserved separately. R3 does not claim the v0.2 blocker is closed.
