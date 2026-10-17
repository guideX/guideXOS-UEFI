# guideXOS C# UEFI — Task Manager Memory Details crash fix

**Date:** 2026-10-17 | **Classification:** Outcome B — FIX IMPLEMENTED, QUALIFICATION INCOMPLETE | **Release blocker:** Open; v0.2 release qualification is not complete.

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
