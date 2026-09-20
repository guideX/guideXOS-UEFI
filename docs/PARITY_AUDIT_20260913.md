# guideXOS Legacy -> C# UEFI parity audit

Audit date: 2026-09-13  
Primary tree: `D:\dev\guideXOSUEFI`  
Reference tree: `D:\dev\guideXOS`

## Scope and starting state

The audit compared the tracked source trees, recent history, entry points, and
the behavior-bearing desktop/runtime paths. Both repositories were clean at
the start. The Legacy tree was inspected only; it was not modified.

The UEFI starting point was `955236fe` (`The widgets are no longer implicated
in an unbounded allocation problem`) on `main`, tracking `origin/main` and
`0` commits ahead / `157` behind. Legacy was `4ff1ac4` (`guideXOS Server app
model Migrations`) on `main`, tracking `origin/main` and `0` ahead / `216`
behind.

The ending UEFI `HEAD` and subject are unchanged. The final live tracking
check reports `0` ahead / `0` behind for both repositories; the initial
preflight capture recorded UEFI as `0` ahead / `157` behind and Legacy as `0`
ahead / `216` behind. This discrepancy is retained rather than silently
normalized; no fetch, commit, or push was issued by this audit. Legacy ended
clean and UEFI ended with only the intended files listed below
modified/untracked.

## Functional parity matrix

| Area | Legacy reference | UEFI implementation | Status | Material difference / risk |
|---|---|---|---|---|
| Platform entry and handoff | BIOS/Multiboot/native bootstrap in `Kernel/Misc/Multiboot.cs`, `Tools/EntryPoint.asm`, and native support | UEFI/TianoCore loader in `guideXOSBootLoader/`, `guidexOSBootInfo.h`, `handoff_trampoline.asm`, and UEFI boot info | DIFFERENT BY DESIGN | The handoff mechanisms must remain platform-specific; compare only after managed entry. |
| Framebuffer, memory map, timers, CPU/interrupt setup | Legacy platform initialization and native paths | UEFI framebuffer recovery, EBS-owned memory, managed post-EBS graphics path | DIFFERENT BY DESIGN / UEFI BETTER for current UEFI path | Do not transplant firmware addresses or initialization order. |
| Allocator and core runtime | Shared allocator/runtime concepts | Shared concepts plus the validated UEFI path | PARITY | Current allocator/widget baseline is stable; no restoration need was found. |
| Input and device discovery | Legacy device paths and input handling | UEFI pointer/event rings, PS/2/USB discovery, and UEFI-safe input tests | UEFI BETTER / DIFFERENT BY DESIGN | Existing UEFI input path is a protected baseline. |
| Desktop/window manager | `guideXOS/GUI/Desktop.cs`, `WindowManager`, controls, menus, widgets | Same families plus UEFI-specific `UpdateUefi` and taskbar rendering | UEFI PARTIAL | UEFI used a fixed four-tile desktop and deliberately left `Desktop.Apps` null; this hid the mature shell launch path. |
| Built-in applications | `guideXOS/OS/App.cs` and `DefaultApps/` | Same built-in app set and windows | PARITY at inventory level; PARTIAL at launch integration | Most apps exist, but UEFI shell launch and file associations were not wired through a shared resolver. |
| App model and stable launch identity | `guideXOS/OS/App.cs` (`AppKind`, descriptors, resolver) | Restored in `guideXOS/OS/AppModel.cs`, adapted to UEFI-safe initialization | LEGACY ONLY -> RESTORED | The model is metadata/dispatch infrastructure; it does not invent a process model. |
| File associations | `FileAssociationRegistry` and `Desktop.OnClick` | Restored in `AppModel.cs` and UEFI-safe `Desktop.OnClick` | LEGACY ONLY -> RESTORED | PNG uses managed `PngLoader` on UEFI; native Legacy PNG remains only on the Legacy path. |
| Shell objects | `ShellObjectRegistry` and `Desktop.OnClick` | Restored in `AppModel.cs` and wired into `Desktop.OnClick` | LEGACY ONLY -> RESTORED | Root, Computer Files, USB volumes, and installer preserve the existing UEFI actions. |
| Start menu/taskbar | Mature `StartMenu` and taskbar path | Existing UEFI taskbar now opens `StartMenu` when the app model is available | UEFI PARTIAL -> IMPROVED | UEFI still retains its stable taskbar renderer and keyboard fallback. Full visual/layout parity is not claimed. |
| GXM and managed resources | GXM loader/window/script paths and filesystem resources | Same GXM paths plus UEFI ramdisk and managed PNG resources | PARITY / DIFFERENT BY DESIGN | External host packager/process assumptions are not runtime UEFI requirements. |
| Shutdown/restart and system UI | Existing GUI/system-action classes | Present where supported by UEFI shell path | PARTIAL / NEEDS RUNTIME PROOF | No boot-path rewrite was attempted. |
| Host tooling and extras | `Extras/Installer`, `Extras/MediaCreator`, `Extras/guideXOS.Web`, `GXM.Apps` | Not part of UEFI OS runtime | OBSOLETE / DO NOT PORT | These are host/development or BIOS-era packaging concerns, not a bounded UEFI usability gap. |

## Main findings

The largest real gap was not a missing built-in executable: the applications
were already present. The gap was that UEFI's early-safe desktop setup left
`Apps`, image viewer, message box, and WAV player unconstructed, while the
UEFI taskbar used its Start tile only for the on-screen keyboard. Consequently
the UEFI shell did not expose the Legacy-compatible application registry,
aliases, file associations, or shell-object contract.

The Legacy app-model migration at `guideXOS/OS/App.cs` (especially the
descriptor/resolver registries and `AppCollection.Load`) and the Legacy desktop
dispatcher at `guideXOS/GUI/Desktop.cs:775` were used as the behavioral
references. The UEFI adaptation is deliberately split out of `App.cs` so the
stable UEFI-safe PNG and post-EBS initialization order remain intact.

## Selected bounded restoration

Target: **Legacy-compatible app/shell model, file associations, and the
existing UEFI Start-menu route**.

This was selected over cosmetics, host-only tooling, and lower-level runtime
changes because it is immediately user-visible, applies above the platform
boundary, and provides the reusable source-level contract needed by future
shared Server/UEFI applications. The main risks were premature icon/resource
construction, native PNG use after EBS, and null window references. Those risks
are addressed by initializing the model after `SetupIcons`, lazily creating
viewer/message/audio windows, and routing UEFI PNG decoding through
`PngLoader`.

Changed UEFI files:

- `guideXOS/OS/AppModel.cs` — app descriptors/aliases, file associations,
  shell objects, and deterministic self-tests.
- `guideXOS/OS/App.cs` — resolver-backed built-in dispatch and lazy viewer/audio
  windows.
- `guideXOS/GUI/Desktop.cs` — resolver-backed shell/file dispatch and
  UEFI-safe associated-file opening.
- `guideXOS/GUI/Taskbar.cs` — Start tile opens the existing StartMenu when the
  model is ready, with the prior keyboard fallback retained.
- `guideXOS/GUI/StartMenu.cs` — guards the base-constructor virtual visibility
  callback and starts the menu hidden so the first Start-tile click opens it.
- `guideXOS/Program.cs` — post-EBS model initialization and opt-in AppModel
  diagnostic mode.
- `guideXOS/guideXOS.csproj`, `build.ps1`, `run_uefi_validation.ps1` — bounded
  `AppModel` diagnostic selector and completion marker.

## Deliberately remaining differences

- UEFI keeps the recovered fixed `FILES`, `DOCS`, `IMAGES`, and `AUDIO` desktop
  layout; full Legacy icon/layout parity is not part of this bounded change.
- UEFI keeps managed PNG decoding and its post-EBS resource order.
- BIOS/native bootstrap code is not synchronized with UEFI.
- There is no full process/ring-3/task-ownership lifecycle in either tree that
  can be safely claimed as restored by this phase; that is future design work.
- Legacy host extras and packaging projects remain unported.

## Validation record

The managed UEFI project builds successfully with the new source and
`git diff --check` is clean. Runtime proof completed as follows:

- `run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120`: pass;
  `APP_MODEL_APP_COUNT=12`, alias/association/shell markers all `=1`, and
  `APP_MODEL_COMPLETE` reached after post-EBS setup.
- Default `build.ps1`: pass; the final ESP was rebuilt without
  `UefiDiagnosticMode`.
- `run_uefi_validation.ps1 -Continuous -SkipBuild -TimeoutSeconds 30` against
  that final ESP: pass (`TIMEOUT_SUCCESS`), continuous dispatch entered,
  advancing heartbeats, and `Graphics invariants: True`.
- `run_uefi_validation.ps1 -NativeInput -SkipBuild -TimeoutSeconds 90`: pass
  through frame 300 with `Start menu opened: 1`, balanced keyboard/mouse
  down/up counts, zero dropped events, graphics valid, zero allocator-free
  corruption samples, and `ThreadPool.Locked=0`; the FILES tile route was also
  observed. The probe exposed and fixed the pre-construction
  virtual-visibility/blur-cache lifecycle. The harness now gives QEMU's
  redirected serial stream a short bounded settle window before evaluating the
  final marker; it does not extend the guest workload timeout.
- `run_uefi_validation.ps1 -ContextMenu -TimeoutSeconds 120`: pass;
  `CONTEXT_MENU_COMPLETE`, frame 600, 104 desktop menu opens/draws/good
  bounds, zero bad bounds, taskbar menu 1/1/1, and 105 right-button pairs.

Production widget defaults remain hidden and no diagnostic define is enabled
by default. The Start tile route is wired to the existing StartMenu and retains
the keyboard fallback; the input workload is the dedicated runtime proof for
opening and dismissing it.

This file is intentionally placed under the repository's existing `Docs`
directory. That directory is ignored by the current root `.gitignore`, so it is
left as a local audit artifact unless the project later chooses to track docs.
