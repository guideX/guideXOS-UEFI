# UEFI P2: Running application taskbar items

## Result

The UEFI-specific taskbar renderer had been left without a running-application draw loop. `DrawUEFITaskBar` reconciled `TaskbarApplicationEntryRegistry`, then painted the taskbar background, clock, and Start control; it never enumerated the reconciled entries. This is category A6: the UEFI revival left application taskbar drawing incomplete. The data and icon resolver already existed.

P2 restores one visible button per existing `TaskbarApplicationEntry` when it fits in the taskbar. It does not add a running-application registry or change application grouping, ownership, lifecycle, or close policy.

## State and render path

```text
ApplicationInstanceRegistry and owned, generation-validated windows
    -> TaskbarApplicationEntryRegistry.Reconcile()
    -> one TaskbarApplicationEntry per ApplicationInstance
    -> UEFI taskbar layout and hit test
    -> entry.GetFocusTarget().TaskbarIcon
    -> existing IconsPrivate PNG cache and Graphics.DrawImage()
```

`TaskbarApplicationEntryRegistry` remains the projection authority. It rebuilds from live `ApplicationInstance` slots and owned windows, suppresses entries without presentable windows, and removes terminal or stale entries. The renderer iterates its bounded capacity and calls `GetAt`; it does not keep another list of applications.

The item caption and renderer-neutral resource metadata come from the existing entry (`DisplayName`, `ResourceKey`). The icon is the `TaskbarIcon` already attached to an owned focus window by `AppCollection` or the existing compatibility launch path. Built-in launch metadata binds Calculator to `Icons.CalculatorIcon(32)`, Computer Files to `Icons.FolderIcon(32)`, Notepad to `Icons.NotepadIcon(32)`, and Task Manager to `Icons.ApplicationsIcon(32)`. `ResourceKey` is not used to introduce a second icon mapping.

One taskbar item represents one `ApplicationInstance`. Multiple windows owned by that instance remain one group and use the entry's selected focus target. Calculator's existing `MultiInstance` policy allows separate Calculator instances to remain separate items. P2 does not change either behavior.

## Button layout and appearance

The UEFI taskbar is 40 pixels high. The Start sprite remains at `(12, yTop + 4)` at its native 43×32 size. Application items begin at `x = 12 + 43 + 8 = 63`, leaving the Start hitbox untouched.

Buttons use a fixed 140×36 rectangle at `yTop + 2`, with 8 pixels between items. The 32×32 cached icon is drawn 6 pixels from the left edge and centered vertically; the caption starts 6 pixels after the icon. The existing bounded `DrawString` overload clips long captions to the remaining button width. The layout reserves 16 pixels before the clock/date text start and only draws complete buttons. If the active group falls beyond the visible slots, the final slot is reserved for it and the group that otherwise occupied that slot is hidden. Other overflow groups remain hidden until the available width grows; there is no scrolling or partial button.

Inactive buttons use the existing taskbar dark gray fill and border. Hover and active application state brighten the fill; the active application receives a stronger border. A held left press over a button darkens its fill. Active state is determined by `ApplicationInstanceRegistry.ActiveApplicationHandle`, not merely by an instance having reached its running/activated lifecycle state.

The icon is accepted only when its pixel buffer and positive dimensions are valid and fit within 32×32. An invalid or missing icon uses the existing cached `Icons.DocumentIcon(32)` fallback. Drawing uses `Graphics.DrawImage`'s alpha path and the original cached image; no PNG decode, resize, or pixel-buffer allocation occurs per frame. Repeated instances reuse the shared cached icon reference. Closing an application removes only its taskbar projection; shared catalog images remain owned by `IconsPrivate`.

## Input, activation, close, and stale state

Draw and hit testing use the same local `(x, y, width, height)` values. Hit rectangles are half-open at their right and bottom edges, match the drawn 140×36 button, and are disjoint from the Start sprite. A click between items or in overflow space has no taskbar-item target.

The renderer calls `TaskbarApplicationEntryRegistry.TryFocusWindow(handle, null, out result)`. That existing route reconciles first, generation-validates the handle and owned focus window, activates the application through `ApplicationInstanceRegistry`, restores a minimized focus window if needed, moves it to the existing top-of-window path, and records the selected owned window. The taskbar only marks mouse routing after successful activation. It does not directly edit lifecycle state, application Z-order, or active-process state. A bounded click latch prevents repeated activation from one held click.

Normal reconciliation drops terminal instances, invalid generations, and groups with no presentable windows. If the focus target or activation validation fails during rendering/input, the item is skipped or counted invalid and the next reconciliation removes/rebuilds it. The renderer retains no stale `ApplicationInstance` or `Window` references beyond those already held by the authoritative entry.

## Bounded diagnostics

`[TASKBAR_VISUAL]` reports group count, rendered buttons/icons, fallback icons, active count/handle, successful taskbar activations, hidden overflow count, and invalid entries. It writes only when these values change, so it does not add per-frame serial output. Rendered counts represent complete in-bounds button rectangles; icon count represents successfully drawable cached/fallback images.

The AppRuntime host workload checks that each Start-launched application produces one button and icon with zero fallback, closes that instance, then sees the projection return to zero. It clicks Calculator's taskbar button while its instance remains alive and requires the successful-activation count to increase for the same handle.

## Phase 26 build environment repair

The original full `build.ps1` failure occurred during the Phase 26 host-native NativeAOT proof build. Its `gcenv.guidexos.phase26.cpp` translation unit included the Visual Studio STL `yvals.h`; that header then included `crtdbg.h`, which is provided by the Windows SDK UCRT, not by the MSVC include directory. The compiler found `yvals.h` beside MSVC headers but the effective `INCLUDE` had no SDK/UCRT paths, so `crtdbg.h` was not found.

The child process's inherited `PATH` had been replaced with the bundled Python directory. It lacked `System32` and Windows PowerShell, which `vcvars64.bat` needs while discovering and initializing the Windows SDK. The build script had also called vcvars without pinning the SDK version. This was an incomplete host compilation environment, not a GUIDEXOS target dependency requiring a compatibility header.

`Tools/Phase26/build_phase26_managed_proof.ps1` now restores required system/PowerShell paths while preserving inherited entries, discovers an installed SDK containing both `ucrt\crtdbg.h` and `um\windows.h`, passes that SDK version to each `vcvars64.bat` invocation, and runs the compiler through generated command files that preserve the initialized environment. It records the inherited environment, selected SDK, compiler command, and effective toolchain environment under ignored `out` paths. No MSVC header or SDK file is changed, and no replacement debug header is supplied.

Recorded toolchain for this machine:

| Item | Value |
| --- | --- |
| Visual Studio | `C:\Program Files\Microsoft Visual Studio\18\Community` |
| MSVC | `14.51.36231` tool directory; compiler product `14.51.36257.0` (`19.51.36257`) |
| Compiler | `C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\bin\Hostx64\x64\cl.exe` (x64 host, x64 target) |
| Linker | `C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\bin\Hostx64\x64\link.exe` |
| Environment script | `C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat` |
| Windows SDK | `10.0.26100.0` |
| SDK include root | `C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0` |
| UCRT include root | `C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\ucrt` |

The effective include list includes the selected MSVC and Visual Studio headers, SDK `ucrt`, `um`, `shared`, `winrt`, and `cppwinrt` roots, plus the installed NETFX SDK root. In particular, `crtdbg.h` resolves under the selected UCRT root. An ordinary PowerShell caller can invoke `build.ps1`; it no longer needs to have been manually started from a Developer Command Prompt.

The invocation was captured at `out/dotnet/phase26-runtime-pack/_phase26-build/phase26-gc-command.txt`; the generated command file is `gcenv.phase26.cmd`. The compiler command uses the x64 `cl.exe`, `/TP`, the NativeAOT `-D...` feature set, runtime `-I` roots, `/Fo...gcenv.guidexos.phase26.obj`, `/Fd...gcenv.guidexos.phase26.pdb`, `/FS`, and `-c ...gcenv.guidexos.phase26.cpp /showIncludes`. The captured effective `INCLUDE` value was:

```text
C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\include
C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\ATLMFC\include
C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\VS\include
C:\Program Files (x86)\Windows Kits\10\include\10.0.26100.0\ucrt
C:\Program Files (x86)\Windows Kits\10\include\10.0.26100.0\um
C:\Program Files (x86)\Windows Kits\10\include\10.0.26100.0\shared
C:\Program Files (x86)\Windows Kits\10\include\10.0.26100.0\winrt
C:\Program Files (x86)\Windows Kits\10\include\10.0.26100.0\cppwinrt
C:\Program Files (x86)\Windows Kits\NETFXSDK\4.8\include\um
```

At script entry, captured `INCLUDE`, `WindowsSdkDir`, `UCRTVersion`, and VC environment selectors were unset; the inherited PATH was 79 characters and lacked both `System32` and `WindowsPowerShell`. This confirms the script was not relying on a previously configured Developer Command Prompt. MSVC's own include root supplied `yvals.h`; the missing Windows SDK UCRT root supplied `crtdbg.h`. The toolchain environment capture is reproducible with `vcvars64.bat 10.0.26100.0` and is written under the ignored Phase 26 build output.

Visual Studio discovery now uses the repository's shared `Tools/Find-VcVars64.ps1` helper. The main `build.ps1` and Phase 26 proof script both call it; Phase 26 derives the compiler and library-manager roots from that selected `vcvars64.bat` installation. This keeps the full build and proof build on the same installation selection rule.

The full build also exposed a stale Phase 32 source validator: it searched the kernel's positive validity predicate for rejection-shaped expressions (`TargetLength == 0` and `TargetLength > MaxTextLength`), while the current kernel correctly accepts only `TargetLength != 0 && TargetLength <= MaxTextLength`. `Tools/Phase32/validate_phase32_wire.py` now asserts those actual validity clauses. No SDK/kernel wire semantics changed.

## Validation record

The full `build.ps1` staging flow passed after the Phase 26 include-path repair. It regenerated the managed proof staging and ramdisk from the current sources, then assembled the EFI image. Later diagnostic and production kernel builds reused that freshly regenerated ramdisk. The final production Continuous comparison also used the regenerated ramdisk and confirmed triple buffering is enabled.

| Check | Result |
| --- | --- |
| App Model / lifecycle / taskbar grouping | `AppModel` completed. Lifecycle self-test: 15 passed, 0 failed. Taskbar grouping self-test: 16 passed, 0 failed. Projection ended empty with zero stale entries; observation did not mutate the registry. |
| AppRuntime application cohort | Calculator, Notepad, Computer Files, and 9 other built-ins each appeared with one button and icon, zero fallbacks, and disappeared on close. Repeated Calculator/Notepad close and relaunch completed three cycles. |
| Multi-instance and overflow | Two Calculator instances remained separate entries under the existing policy. Eight groups produced seven complete visible buttons/icons, zero fallbacks, one active, one hidden, and zero invalid entries. The active group occupied the reserved final slot when it overflowed. Clicking the first and rightmost visible buttons activated their existing instances; closing all groups returned counts to zero. |
| Taskbar production soak | Start launched Calculator, Notepad, and Computer Files. Three groups/buttons/icons were visible, fallbacks were zero, active state was reported, and clicking Calculator's button activated that same existing handle. Mouse events were balanced with zero dropped events. The host workload held for 120 seconds; the last guest heartbeat was frame 600. |
| Production visual state captures | QMP captured the active, hover, held-press, and selected taskbar states in `out/uefi-taskbar-*.ppm`. The 1280×800 production frame visibly shows Start and all three 32×32 application icons inside separate 140×36 buttons. Pixel samples show active border/fill `(96,96,96)/(58,58,58)`, hover border/fill `(85,85,85)/(58,58,58)`, held press fill `(38,38,38)`, and the released selected fill returning to `(58,58,58)`. The Notepad click changed active ownership to its existing instance. |
| AppRuntime host workload after taskbar checks | Guest remained graphics-valid with no runtime fault, and taskbar launch, activation, overflow, and removal checks had passed. The broader file-browser host workload stopped at the known marker mismatch: the host expects `Scripts/`, while this guest opens `Programs/`. This is a harness/workload expectation mismatch, not a taskbar failure. |
| NativeInput | `TIMEOUT_SUCCESS`; keyboard events 104 down / 104 up, zero dropped; mouse events balanced, zero dropped; Start menu opened. Graphics valid; allocator corruption 0, no-pages 0, `ThreadPool.Locked=0`. |
| ContextMenu | `CONTEXT_MENU_COMPLETE` at frame 1800. 104 menu opens/draws/good bounds, zero bad bounds. Taskbar context menu opened/drew/dismissed with good bounds. Mouse/keyboard events balanced; graphics valid; allocator corruption 0, no-pages 0, `ThreadPool.Locked=0`. |
| Fresh Phase 31 and Phase 32 controls | Both were rejected before the successful proof path with `ARTIFACT_HASH`; neither produced its success marker. The staged EXE and GXMI agree with each other and the host Phase 31 static verifier passes, but current freshly generated artifact hashes differ from the kernel's earlier pinned allowlist values in `Kernel/Misc/ManagedImage.cs`. The serial label `PHASE26_HASH_MATCH` is emitted by the shared flagged-image hash validator for Phase 31/32 descriptors; it does not mean the separately staged Phase 26 EXE was read for these checks. This is an existing managed-proof/toolchain pin mismatch, independent of taskbar rendering. It was not changed in P2. |
| Phase 31 post-proof fault | Not reached in this run. The reported earlier post-proof Calculator cleanup/frame fault remains separate; the current control failed before proof startup at the artifact hash gate. |
| Production Continuous, no apps | Fresh production kernel plus regenerated ramdisk. Triple buffering enabled; taskbar empty; last heartbeat frame 600 during the 300-second host timeout. Graphics remained valid, allocator corruption 0, no-pages 0, and `ThreadPool.Locked=0`. This mirrors the app soak's last guest heartbeat, so the limited heartbeat progression is not specific to running-app buttons. |
| Production display stability | No crash marker, allocator corruption, no-pages failure, or invalid-graphics marker occurred. The guest remained CPU-active in QEMU. The taskbar screenshots verify actual placement, icon presence, clipping, and color states. Flicker was not measured by video capture; redraw stability is supported by the 120-second app soak and repeated clean graphics/framebuffer invariants. |

Fresh Phase 31/32 controls are therefore **inconclusive as managed semantic regressions**: the proof code was never entered because the kernel hash pins predate the freshly staged binaries. Their static verifier/build artifacts are not evidence that those guest runtime proofs passed. Resolving the allowlist requires a separate managed-proof/toolchain provenance change and was kept out of this taskbar-focused P2.

## Remaining polish

- No scroll or overflow affordance is provided; overflow groups are hidden, with the active group taking the last slot if necessary.
- Missing-icon fallback is bounded and uses the shared document icon; a deliberately malformed application icon still needs a live negative test if an injectable test route is added later.
- Existing Phase 31 post-proof frame-fault behavior is tracked separately from this renderer unless a P2 run demonstrates a new taskbar-related fault.
- Desktop icon dragging/reordering, icon-size menu behavior, cursor artwork, splash artwork, and broader managed GUI work remain outside P2.
