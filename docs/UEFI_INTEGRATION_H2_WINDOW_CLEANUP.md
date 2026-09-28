# UEFI Integration H2 — Window cleanup repair

## Current result

The Phase32 cleanup path now completes without a guest fault, detaches and disposes all six OpenDocument targets, reconciles the taskbar to zero target entries, and returns the window collection to its baseline of six. The latest authoritative Phase32 run is `serial_uefi_validation_20260927_171114.txt`; it reports five successful managed returns of `32`, six of six target cleanup checks, `windows:6:6`, `PHASE32_WINDOW_CLEANUP_END=1`, `PHASE32_APP_MODEL_BALANCED=1`, `PHASE32_PROCESS_CLEANUP_BALANCED=1`, and `RING3_PHASE32_COMPLETE=1`.

The runtime repair is accepted. One forensic limit remains: the original failing run did not capture instruction bytes at the faulting RIP, so the exact invalid opcode cannot be decoded retroactively. The corrected interrupt handler now records `FAULT_BYTES=` on any future vector 6. No vector 6, page fault, or general-protection fault occurred in the final Phase31/32 controls.

**Phase33 remains on hold** until the byte-level evidence gap is reviewed. The available caller and RIP evidence supports an invalid indirect control transfer, but it does not establish the exact bytes fetched at the original RIP.

## Original fault evidence

The preserved authoritative failing log is `out/phase32-current-allowlisted.log` (SHA-256 `200CFBF0270C2FDE1B874711937460BA0F8E81FB5E750FB5A8DA9B8C29CAF00C`). Its original fault record contains:

| Field | Recorded value |
|---|---|
| Vector | `0x06` (`#UD`) |
| RIP | `0x00000000000A02C6` |
| CS / RFLAGS | `0x8` / `0x10216` |
| RSP / RBP | `0x48C0D00` / `0x0` |
| CR3 / RAX | `0x3DD73000` / `0x50001FF` |
| PTE diagnostic | `0xA0023` |
| Last Phase32 stage | `WINDOWS`, frame 2, boundary 400; after `PHASE32_WINDOW_CLEANUP_BEGIN`, before cleanup-end and balance markers |
| Fault bytes | Not recorded by the original handler; unavailable from this log |

The saved caller return address `0x1008D808` maps to `WindowManager.CleanupClosedWindows`. The corresponding caller instruction at `0x1008D805` is an indirect virtual dispatch, `call qword ptr [rax+0x50]`, for `w.Dispose()` (current source call site: `guideXOS/GUI/WindowManager.cs`, line 695). The target RIP `0xA02C6` is outside the kernel ELF image and in low mapped memory. Together these facts support classification **C4: invalid control transfer through the virtual `Dispose` dispatch**. They do not distinguish whether the underlying bad target came from concurrent list/lifecycle mutation, aliased owner allocations, or both, and they do not supply an instruction decode at `0xA02C6`.

The original failure log did not preserve a selected window identity, current `ApplicationInstance`, or exact focus/taskbar row for the faulting object. The closest instrumented reproductions reached cleanup while disposing detached Notepad document targets. That is evidence about the repeated path, not proof of which exact object faulted in the original run.

The historical Phase31 failure was separate: `#PF` at RIP `0xA0007`, CR2 `0x1C4020000`. Its vector and address do not match Phase32's `#UD` at `0xA02C6`. The fresh Phase31 control now completes normally.

## Root cause and repair

Two concrete lifecycle hazards were found and repaired together:

1. `CleanupClosedWindows` could be called by both the frame loop and lifecycle/diagnostic paths while other code changed the shared `WindowManager.Windows` list. Reverse scans, removal, owner detach, and disposal therefore lacked one common serialization boundary. A fresh pre-fix run showed overlapping cleanup activity and list mutation around distinct threads.
2. Window allocator ownership depended on `List<Window>.IndexOf(this) + 1`. The local generic `IndexOf` compared with `==`; under this runtime's generic lowering, distinct `Window` references could resolve to index zero. Reproduction instrumentation showed duplicate owner IDs, so `FreeOwnerMemory` could alias another window's allocations.

`WindowManager` now owns a reentrant cleanup lock used by closed-window cleanup, registration, z-order changes, task-manager disposal/removal, and transient-window release. A nested same-thread cleanup request schedules another pass; concurrent callers serialize on the same lock. Task Manager now uses atomic manager removal helpers instead of mutating the public list directly.

Window allocator IDs are now monotonic and assigned by `RegisterWindow`; they do not depend on list position. `List<T>.IndexOf` now checks reference identity and then `Equals`, and `Object.ReferenceEquals` compares object addresses directly. The corrected Phase32 log shows distinct target owner IDs (10, 12, 14, 16, 17, and 19).

The Phase32 proof now bounds normal `CleanupClosedWindows` passes at eight and takes collection counts under the cleanup lock. It keeps performing the same disposal path until the original count is restored or the bound is exhausted. This does not skip cleanup or accept a leak; the final successful run reached baseline on its first pass.

### Lifecycle order and invariants

The preserved semantic order is:

1. Application termination marks each owned window closed, clears focus/ownership links, and detaches the window from the `ApplicationInstance`.
2. The serialized manager cleanup selects closed windows, removes each from the global list once, runs the existing owner-close check (which safely no-ops after prior detach), and calls `Dispose()`.
3. The manager reconciles taskbar projection and exits with no target taskbar entry or stale active-window relationship.

No cleanup is suppressed, no disposal is skipped, and no closed window is intentionally retained. The final Phase32 balance detail is `targets:0:0;windows:6:6;factories:0:11;fallbacks:0:0;legacy:0:0;requests:0;ownerReleased:1`, with all balance fields set to `1`. The final cleanup log records taskbar entries `0`, active application handle `0`, and detached target owner handles. The phase's allocator diagnostics show `freeNoPages=0` and `freeCorrupt=0`; `freeInvalid` remains nonzero desktop telemetry and is not claimed as fixed.

## Verification

| Control | Current result |
|---|---|
| Phase32 | Five successful `32` returns (four primary plus replacement); empty and oversized lengths rejected; unsupported association rejected; missing `.txt` returns typed `ResourceUnavailable`; stale process/owner/service/document requests rejected; requester FailFast exits `-1` while its target persists; all six targets terminate; cleanup-end, balance, and complete markers pass; no guest fault |
| Phase31 | Four primary `31` returns plus replacement; Calculator activated; invalid target and oversize cases pass; FailFast target persists; target cleanup and process balance pass; no historical post-proof fault |
| Phase30 | Four repeated successful lifetimes and current regenerated payload controls pass; clear, FailFast, replacement, and process cleanup pass |
| Phase29 | Four repeated successful lifetimes and current regenerated notification controls pass; FailFast, replacement, and process cleanup pass |
| Phase28 | Exit, FailFast, generation identity, and service controls pass; `RING3_PHASE28_COMPLETE=1` |
| Phase27 | Managed service and typed-failure controls pass; `RING3_PHASE27_COMPLETE=1` |
| Phase26 | Four managed entry runs return `42`; `RING3_PHASE26_COMPLETE=1` |
| AppModel / Lifecycle / grouping | AppModel passed; lifecycle 15/15 and taskbar/grouping 16/16; no stale taskbar entries |
| AppRuntime stress | Ten Calculator and ten Notepad open/close cycles passed; stale ownership, corrupt frees, and no-pages failures stayed zero. Its separate host route check still missed the expected nested `Scripts/` click after opening Computer Files; this was an input-harness marker mismatch, not a guest fault. |
| Production taskbar | Ordinary production ESP opened Calculator, Notepad, and Computer Files; three visible buttons/icons, zero hidden/invalid entries; activation switched among app handles; 120-second soak completed; graphics invariants held and dropped mouse input was zero. Start artwork and the taskbar/app icons were visually inspected in `out/uefi-taskbar-apps.ppm`. The serial log confirms continuous frames and graphics invariants but has no separate triple-buffer health marker. |

The production soak's first invocation ended at the outer timeout because its 120-second host-side sleep consumed almost all of the runner's 125-second timeout. `run_uefi_validation.ps1` now marks the bounded taskbar soak complete when that 120-second operation finishes. The rerun exited 0 with `TASKBAR_SOAK_COMPLETE`.

Static verification was rerun against the regenerated Phase31 and Phase32 payloads. All five Phase31 modes and all three Phase32 modes passed descriptor/map/hash checks; each mode also rejected its missing-mode-flag, descriptor-hash-mutation, and artifact-byte-mutation cases.

Allocator and graphics telemetry is reported conservatively. The long AppRuntime session showed allocator bytes rising during repeated GUI construction and a nonzero invalid-free counter; `freeNoPages`, `freeCorrupt`, input drops, graphics invariant failures, and ThreadPool lock indicators remained zero. This repair does not claim flat allocator-byte usage or a zero invalid-free counter.

## Build, staging, and preserved state

The ordinary `.\build.ps1` full build completed with exit code 0 and `Build Complete`. It regenerated `ramdisk.img` (559 files, 43,679,385 bytes) and staged the kernel and ramdisk into `ESP`. NativeAOT payload bytes for Phases29–32 changed during the full build, so only the corresponding measured SHA-256 pins were updated. The final Phase32 payload hashes are:

| Payload | SHA-256 |
|---|---|
| OpenDocument success | `5C1BD8AE88778A249AAD6E92746E6275178A5D93A885DB158A7F2F638FDBAC51` |
| FailFast | `69BDAFD77FA5DC6D081467880D02C539CEBD6537F17849483418103DC6CB02BC` |
| Stale owner | `0BEDA189068F546D1D7F5727B40A672E46935B2C7C5549D390F7D6041C789523` |

After the final diagnostic controls, the ordinary production kernel was rebuilt and staged. `kernel.elf` matches `ESP/kernel.elf`, and `ramdisk.img` matches `ESP/ramdisk.img`. Phase26's Visual C++/Windows SDK host environment remained available in the full-build log.

The original failure log above and `out/phase32-preexisting-phase30-backup` remain preserved. The nested `out\rt` checkout remains detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with its pre-existing modified and untracked files; no runtime source edits were made by H2. No files in Server or Legacy were changed. Validation serial logs, QMP captures, and NativeAOT intermediates remain under ignored `out`/root output paths; the root `ramdisk.img` and the regenerated Phase29–32 proof pairs are intentional staged build outputs.

## Phase33 recommendation

Keep Phase33 paused until the original #UD byte-level limitation is explicitly accepted or an exact fault is reproduced with the new `FAULT_BYTES` instrumentation. Operationally, Phase31 and Phase32 cleanup now complete, all requested cleanup balances pass, the production taskbar smoke is clean, and the ordinary build stages successfully.
