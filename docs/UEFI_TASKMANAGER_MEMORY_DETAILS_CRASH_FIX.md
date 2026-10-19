# guideXOS C# UEFI — Task Manager Memory Details crash fix

**Date:** 2026-10-17 | **Classification:** Outcome B — FIX IMPLEMENTED, QUALIFICATION INCOMPLETE | **Release blocker:** Open; v0.2 release qualification is not complete.

**Latest R2 update (2026-10-18):** Outcome F. The production A–F matrix and UI paths pass. A 15-minute production idle control grew by 340 pages; the 25-cycle Task Manager run grew by 2,185 pages over a similar duration, leaving roughly 1,888 pages above a linear idle-rate comparison. A diagnostic 25-lifetime inventory reconciles every B0–B5 run/page count but does not resolve the logical owner of the production residual. The v0.2 blocker remains open; see `Docs/UEFI_TASKMANAGER_MEMORY_DETAILS_R2_ALLOCATOR.md` for full evidence.

## Repository state

- Repository: `D:\dev\guideXOSUEFI`
- Starting branch and HEAD: `main`, `8a2c4dd9f491450a0f22a4309950d32b98f2f203`; upstream `origin/main`; ahead 2, behind 0.
- Starting worktree: 129 tracked paths modified or deleted, plus three untracked paths: `Tools/Phase23/recover_source_patch_evidence.ps1`, `Tools/Phase23/runtime-source-cache-recovery.lock.json`, and `toolchain_probe.obj`. No `.phase` marker was found. `TaskManager.cs` was clean at preflight.
- During investigation, `main` advanced to `b0a50b924da0e1d76d50b2b58de01418d4b41d74` (also `origin/main`), incorporating the 129 pre-existing tracked changes. This concurrent commit was not created or altered as part of this fix. The three untracked paths were preserved.
- Validated code commit: `2cba6660137eb799b412dc1af28409658e5f2015`, on `main`, one commit ahead of `origin/main`. At that checkpoint, no tracked changes remained and the same three pre-existing untracked paths remained. This report is a documentation-only follow-up commit; no push was made.

## Investigation and root cause

The fourth tab is index 3 and is titled **Memory Details**. `OnDraw()` refreshes the system snapshot and dispatches to `DrawMemoryDetails()` when that tab is selected.

The inspected code showed an allocator lifetime defect in the Memory Details labels. `StringPool.GetNumber(ulong)` returns entries from its static `_numberStrings` cache for values up to 10,000; the percentage and memory-size helpers also return cached entries. Memory Details retained those returned objects and disposed them when refreshing labels and when disposing Task Manager. In this runtime, `Object.Dispose()` can free the object's exact allocator run. The static cache can therefore keep a reference to a freed string, which can be read again during a later redraw. This is a concrete stale-reference path consistent with the reported OS-level crash.

The original blue screen was not captured. No exception code, faulting instruction, stack, or access address is available, so the stale cached-string lifetime defect is the source-supported mechanism identified, not a claim about the uncaptured faulting instruction.

## Correction

`guideXOS/DefaultApps/TaskManager.cs` now formats Memory Details labels into privately owned strings rather than retaining StringPool results. It disposes and clears those owned fields on refresh and window close. The existing one-second label refresh remains. The snapshot is reset before querying the service, and heap values are shown as `N/A` unless the snapshot is bounded and has a positive memory size. Heap subtraction is then safe under the snapshot bounds. Uptime interval checks avoid unsigned underflow if the observed tick value moves backwards.

No tab was removed and no public service interface changed.

## Files changed

- `guideXOS/DefaultApps/TaskManager.cs` — ownership, disposal, snapshot validation, and safe unavailable-data presentation.
- `Tests/TaskManagerMemoryDetailsRegression.ps1` — deterministic source-level regression check.
- `Docs/UEFI_TASKMANAGER_MEMORY_DETAILS_CRASH_FIX.md` — this report.

## Validation evidence

- Regression script on the fixed source: **PASS** (owned labels, no StringPool use in the draw path, label disposal, snapshot bounds, and unavailable-data handling).
- Same script against `b0a50b9:guideXOS/DefaultApps/TaskManager.cs`: **FAIL as expected** at the assertion that Memory Details must not retain shared StringPool results. This confirms the check distinguishes the pre-fix source from the fix.
- `git diff --check`: **PASS**.
- Focused C# `CoreCompile`, with intermediate and output directories redirected to a temporary directory and the existing project assets supplied: **PASS**, exit code 0, no compile errors. This is not the complete release-mode NativeAOT build.

The regression is a source-level ownership check. This repository has no hosted Task Manager runtime harness in this path, so it does not launch the UI or prove the OS-level failure by execution.

## QEMU and release gates

One isolated diagnostic QEMU boot was attempted using copied ESP and firmware files and a separate QMP port. It reached `CONTINUOUS_DESKTOP_ENTER` and emitted frame markers through frame 300 followed by `INPUT_STATS`; the image was an input-diagnostic build that stopped after frame 300. No Task Manager launch or Memory Details interaction occurred, and no panic was captured. This was not a production candidate boot. The separate QEMU instance observed in the workspace was left running.

| Required evidence | Result |
| --- | --- |
| 25 Memory Details entry/exit cycles and 50 mixed tab switches | Not run |
| All tabs, close/reopen, and other applications verified at runtime | Not verified |
| OS stability after using Memory Details | Not verified |
| Complete uninstrumented release build | Not run |
| Production QEMU cold boots with Memory Details exercised | 0 of 3; the one diagnostic boot does not count |

The exact v0.2 crash release blocker is **not closed**. The ownership correction is implemented and has source-level regression and compile evidence, but UI runtime, kernel stability, complete production build, and three production cold-boot gates remain open.

## R1 production qualification follow-up — 2026-10-17

This section supersedes the preceding provisional QEMU and release-gate status. The full production build and three production UI boots are now complete. The release blocker remains open because allocator use grows substantially during the required stress run, and several secondary acceptance checks remain incomplete.

### Repository and containment

- Starting and ending branch: `main`; starting and ending HEAD: `95501977f95042413ce9f36ca9417ce39ce25367`. `origin/main` matched HEAD (0 ahead, 0 behind). No commit or push was made.
- The starting tracked changes were `guideXOS/DefaultApps/TaskManager.cs` and `Tests/TaskManagerMemoryDetailsRegression.ps1`. R1 also updates this report. The three pre-existing untracked paths remain: `Tools/Phase23/recover_source_patch_evidence.ps1`, `Tools/Phase23/runtime-source-cache-recovery.lock.json`, and `toolchain_probe.obj`.
- The dirty pinned `out\rt` checkout remains at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with its existing modifications. It was not cleaned or repatched. Full build and QEMU work used the ordinary-copy lane at `C:\gx`; the live `out\rt` artifact scan found no writes during qualification.
- The C:\gx `TaskManager.cs` source hash matched the workspace source. The build log is `C:\gx\out\logs\taskmanager-memorydetails-full-release-rerun.log`.

### Ownership audit and source regression

- `StringPool.GetNumber`, memory-size helpers, and related lookups return shared cached strings. The Memory Details draw path no longer obtains those values from StringPool or disposes pooled objects.
- The 14 Memory Details value fields are privately allocated. Numeric and formatted values use fresh strings; literal values are copied through `CreateOwnedMemoryLabel`. Refresh calls `DisposeMemoryDetailLabels` before assigning replacements, clears each field, and the Task Manager `Dispose` override releases the remaining labels on window close. Refresh occurs inside the draw path before the current frame reads the replacement fields; close reaches disposal through the normal Window lifecycle.
- Snapshot bounds and nonzero size are checked before heap subtraction and division. The shared overflow-safe percentage helper avoids multiplying untrusted values by 100, and cumulative page counts saturate instead of wrapping.
- `Tests\TaskManagerMemoryDetailsRegression.ps1`: **PASS** on the current source. It also rejects `b0a50b924da0e1d76d50b2b58de01418d4b41d74:guideXOS/DefaultApps/TaskManager.cs` at the specific assertion that Memory Details must not retain shared StringPool return values.
- Runtime invalid-snapshot injection was not performed. The safe `N/A` path is covered by source regression; no runtime `N/A` screenshot is available.

### Production build and image identity

Ran the complete `C:\gx\build.ps1` release build. It emitted `=== Build Complete! ===`, exit code 0, and zero build errors. Managed artifact admission and staging all agreed at 41/41: descriptor, build staging, ramdisk staging, and compiled allowlist. The production kernel, ramdisk, and EFI files staged into the ESP and copied into each formal QEMU boot directory have matching hashes:

| Component | SHA-256 |
| --- | --- |
| `kernel.elf` | `1875F96565320AA007A34316072F5E7E4BDD7E83D3B7C60B74DC1AE2E37B10DA` |
| `ramdisk.img` | `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8` |
| `EFI/BOOT/BOOTX64.EFI` | `8C0A01ACDEC39C61C139E4AD84DDFC8D110174951CECF07401F0635B888F7FAD` |

### Production QEMU interaction

Three clean QEMU boots used separate ESP copies and firmware-variable files from that production build. QMP mouse events opened Task Manager through Start > All Programs in every boot. Memory Details was visibly rendered, updated, and selected again after navigating away; Task Manager closed and the desktop remained responsive. Thus **3/3 production boots** passed the no-crash UI sequence. The first boot also completed **25/25** open/enter/exit cycles and **50/50** tab transitions across all four tabs.

Evidence is under `C:\gx\out\taskmanager-memorydetails-qualification`:

- `boot-1-stress-verified`: `qualification-summary.txt`, `serial-final.txt`, Start-menu and Task Manager screenshots, Memory Details screenshots for cycles 1, 5, 10, 15, 20, and 25, tab-transition screenshots, and the final healthy desktop capture.
- `boot-2-basic-verified` and `boot-3-basic-verified`: per-boot summaries, serial logs, Task Manager and Memory Details render/return screenshots, and healthy desktop captures.
- `boot-1-desktop-icon`: a separate production UI smoke boot; `desktop-files-icon-activated.png` shows Computer Files opened by double-clicking the Files desktop icon, and serial contains `DESKTOP_FILES_CLICK_ROUTED`.
- `boot-1-desktop-ui`: a separate startup/widget probe. Widgets initialized in serial, but the edge-reveal screenshots did not show the widget panel. The early desktop capture showed no notification toast. These widget and notification interactions are not counted as passed.

Across the three formal boot logs, fault-marker scan count was zero for #PF, #GP, #UD, #BP, panic, unhandled exception, corrupt run, and NativeAOT invalid-free markers. On the stress boot, free calls/successes were 50,391/50,391; invalid, no-pages, and corrupt frees were all 0. Each basic boot recorded 26,651/26,651 free calls/successes and the same zero failure counters. No obvious screen flicker or UI hang appeared in the captured run, though flicker was not measured with a dedicated detector.

### Memory stability result and decision

The required memory-stability gate **failed**. Allocator memory-in-use was 19,075,072 bytes before stress. It was 29,806,592 at cycle 5, 37,892,096 at cycle 10, 58,761,216 at cycle 15, 87,547,904 at cycle 20, and 117,137,408 at cycle 25. After the 50 tab transitions and final close it was 149,704,704 bytes, a net increase of 130,629,632 bytes (124.58 MiB). Invalid and corrupt frees remained zero, but that does not explain or clear the sustained allocation growth. The successful `Leak Exists FALSE` text in the UI is not evidence of allocator stability.

**R1 outcome: B — runtime UI tests completed without a crash, but release acceptance is incomplete and the memory-stability gate failed. The v0.2 Task Manager release blocker remains open.** The source ownership repair is verified and the exact production binary passed the requested UI operations, but this stress memory growth must be diagnosed before release acceptance. Invalid snapshot runtime injection, widgets/notification interaction, and bare-metal qualification also remain outstanding. No physical hardware was available. Owner checklist:

1. Boot the same kernel, ramdisk, and EFI images on bare metal and record their SHA-256 hashes.
2. Open Task Manager through the desktop, select Memory Details, navigate away and back, then close and reopen the window. Repeat 25 window cycles and 50 mixed tab transitions.
3. Capture Memory Details and the post-close desktop; record allocator/free counters and check serial/debug output for faults or invalid frees.

The changed tracked files are `guideXOS/DefaultApps/TaskManager.cs`, `Tests/TaskManagerMemoryDetailsRegression.ps1`, and this report. `git diff --check` passed. No commit was made because the acceptance result was not clean; nothing was pushed.

## R2 final qualification follow-up — 2026-10-18

R2 ran in an ordinary-copy sandbox at `C:\gxR2`; the live repository stayed on `main` at `b4af3c8fd92dcbd35ec594bd33e6b1d87d521d41`, with no live changes, commit, or push. The full unskipped build completed with exit code 0, `Build Complete`, 41/41 managed artifact agreement, and 9/9 Phase 35 modes.

Six independent production QEMU boots A–F and a separate production desktop smoke used one frozen candidate. Its hashes are: kernel `EF76CFBC6A78661EF0FBD4C09CAE9F400BF14B2969C53F66BECAED64B454E759`, ramdisk `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8`, and EFI `4B88FEB3CCB28D83EE0CF20645DDAC31B48146709BFC89F44AA547012F20F599`. Task Manager activation/close, Memory Details, 25 entry/exit cycles, 50 mixed switches, Start Menu, desktop Files icon, and widgets passed. No allocator free failures, page-map inconsistencies, or CPU fault/panic serial markers were recorded.

The R1 footer string leak was identified at the Processes-tab draw site and repaired by disposing only fresh owned formatting results after synchronous borrowed drawing. Other repeated redraw strings/arrays were also scoped or reused. The allocator statistic is `PageInUse × 4096`, a current live global-page measure. Despite the ownership fixes, E rose from 19,173,376 bytes / 4,681 pages to 28,123,136 bytes / 6,866 pages after 25 cycles and a 60-second cleanup wait: **+8,949,760 bytes / +2,185 pages**. It added 46 pages during the wait. The exact residual post-fix run owner is unresolved, so this is not a stability pass.

The diagnostic G build demonstrated runtime `N/A` for an invalid snapshot and visible recovery on the next valid snapshot. A separate production idle control and diagnostic retained-run inventory are also recorded; the inventory reconciles allocator data but does not identify the production residual’s logical owner. Bare-metal qualification remains unavailable. The final R2 classification is **Outcome F**, and the v0.2 release blocker remains **open**. Full results, scenario totals, diagnostic limitations, and evidence paths are in `Docs/UEFI_TASKMANAGER_MEMORY_DETAILS_R2_ALLOCATOR.md`.
