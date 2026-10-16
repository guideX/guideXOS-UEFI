# UEFI Desktop Polish P3

## Result

P3 restores the intended software cursor artwork and makes the four UEFI desktop tiles selectable, launchable, and draggable. Guest input is routed through the existing taskbar, window manager, widget, context-menu, and application services. Icon positions are clamped and retained for the current boot; durable persistence is deferred because UEFI has no existing safe desktop-layout save contract.

## Architecture and diagnosis

The UEFI mouse path is `PS2Mouse` pending events → `MouseEventDispatcher.Update` → `Control.MousePosition` and button state → the UEFI frame loop. Each frame lets the taskbar and `WindowManager.InputAll` claim input before the desktop. P3 records left press/release coordinates at the event edge so a short click cannot lose its position between rendered frames. Coordinates clamp to the actual framebuffer pixels, `0..Width-1` and `0..Height-1`.

The cursor is software-rendered. UEFI startup previously installed only a generated 16×16 fallback; it did not load the existing `Images/Cursor.png` and `Images/Grab.png` assets. It drew that fallback at the raw pointer coordinate through the ordinary image path. The intended 28×28 RGBA assets were present in the ramdisk but never selected by the UEFI path. P3 loads them after the PNG decoder initializes, preserves alpha, and subtracts the artwork's hotspot before drawing. The arrow hotspot is `(1,1)` and the grab hotspot is `(11,0)`. If decoding fails, the existing generated fallback remains available.

The desktop rendered four static UEFI tiles, but hit testing only had a special launch rectangle for FILES. DOCS, IMAGES, and AUDIO had no click targets, and the UEFI path had no selection or drag state. The frame loop also passed icon size `48` on every update, overriding a size selected through the existing context menu. P3 gives each tile one shared draw, selection, and hit rectangle; the rectangle covers the tile and its displayed label. The desktop draws once into the existing buffered frame, and draws one alpha-composited cursor after windows and taskbar, before `Framebuffer.Update`.

## Interaction policy

- A left press on a tile selects it. A press on empty desktop clears selection. The selection highlight uses the same bounds as hit testing.
- A second click on the same tile within 500 ms and 8 pixels activates it. A 5-pixel movement threshold starts a drag and cancels double-click recognition.
- Dragging preserves the pointer-to-icon offset and captures input through release. The completed position clamps the full tile-and-label bounds to the desktop work area above the taskbar.
- The existing `Desktop.OnClick` route launches FILES through File Explorer and DOCS through Notepad. IMAGES and AUDIO use their existing Image Viewer and WAV Player registrations.
- The desktop cannot begin a gesture if the taskbar, a visible window/widget, a moving window, or another handled surface owns the press. While a desktop drag is captured, other surfaces do not receive the same gesture.
- Right-click uses the latched press position, avoids opening the desktop menu over a visible window or the taskbar, and leaves menu ownership with the existing window/menu input path. The existing Icon Size submenu now works in UEFI because the cache follows the chosen size instead of resetting to 48 on every frame.
- UEFI custom icon positions are held in memory for the current boot. The existing configuration save path is not enabled for UEFI, and Phase 35 managed Persistent remains read-only. No storage writer was added; positions are not claimed to survive restart.

## Validation

### Revalidation at the current main head

The live checkout was `main` at `1e2dba921831364719eb816b4d76bdea0b89fca0`, one commit ahead of `origin/main`. The P3 implementation is already present in ancestor commit `3f1d155`; its production files, tests, and `build.ps1` matched the current live checkout. No product-code edits were needed for this revalidation.

- The focused hosted project passed **42 assertions** with `dotnet run --project Tests\UefiDesktopInteractionTests\UefiDesktopInteractionTests.csproj --configuration Release --artifacts-path out\p3-test-artifacts`. Output: `out\p3-focused-tests-current-head.log`.
- The ordinary full build completed with zero errors. Descriptor, build staging, ramdisk staging, and compiled allowlist agreement were all **41/41**. Log: `out\p3-full-build-current-head.log`. The known base Ring3Phase35 fixture-seeding caveat remains a separate documented issue; no Phase 36 work was started.
- The current-head continuous startup smoke returned `TIMEOUT_SUCCESS`: dispatch selected, Continuous entered, four heartbeats through frame 600, timer 634→16,752, graphics invariants true. Log: `out\p3-continuous-smoke.log`.
- A fresh **visible** `DesktopP3` QEMU baseline at 1280×800 on the current-head diagnostic EFI image returned `DESKTOP_P3_COMPLETE`. It captured all 19 expected frames, loaded both cursor assets, recorded four selections, one deselection, two activations, one drag start/end, zero dropped mouse packets, and valid graphics invariants. Log: `out\p3-visible-baseline-current-head.log`; serial: `serial_uefi_validation_20261015_184642.txt`; frames: `out\desktop-p3-evidence`.
- The protected live `out\rt` tree remained unchanged at **66,730 files / 2,152,293,540 bytes**. Existing user deletions and untracked `toolchain_probe.obj` were preserved.

The fresh baseline's last periodic mouse snapshot showed 5 downs and 4 ups because the final File Explorer activation followed that snapshot; the validator accepted the subsequent release evidence. The completed full stress run below recorded balanced 318/318 left-button transitions and is the stronger stuck-button check.

### Accepted full graphical stress proof

The completed 2026-10-13 P3 acceptance run used the same P3 production source now in `3f1d155` and the built-in QMP/framebuffer screenshot harness. It returned `DESKTOP_P3_COMPLETE` and `Desktop stress validation: True`. Its QEMU window was hidden, but guest interaction used real QMP mouse/keyboard input and captured guest framebuffer screenshots.

- 104/51 total selections/deselections, including baseline proof;
- 51/51 drag starts/drops and 27 icon activations;
- 26 Start opens, 25 requested Start open/close cycles;
- 300 seconds of cursor path motion across desktop, widget, taskbar, Start, and screen edges (344 path transitions);
- 20 screenshots, 11,831 mouse packets, 11,189 moves, zero dropped packets;
- 318/318 left and 3/3 right button transitions; graphics invariants true; stack low-water 2,620,160 bytes.

Runner log: `out\desktop-p3-memory-reset-fixed-runner.log`. Serial log: `out\desktop-p3-memory-reset-fixed-serial.log`. The current-head build and visible baseline above independently cover the later build-only changes in `1e2dba9`.

Exploratory same-day QMP timing changes in the ordinary-copy sandbox produced stress telemetry below the activation gate. Those edits were not transferred or counted as accepted proof; the completed original P3 stress run above remains the qualified stress result. The current P3 source and runner in the live checkout remain untouched by those experiments.

## Stability and remaining verification

The stress run completed 12 heartbeats through frame 14,400. Stack low-water stayed at 2,620,160 bytes, graphics invariants stayed valid, dropped mouse packets stayed at zero, and allocator free-invalid, free-no-pages, and corruption counters stayed at zero. Left button transitions balanced at 318/318; right transitions balanced at 3/3. No startup `#BP`, PF, GP, or UD appeared in the serial log.

`Allocator.MemoryInUse` reports in-use allocator pages (`PageInUse × PageSize`), so it measures committed page usage rather than cumulative allocation volume or exact live managed objects. It rose from 19,152,896 bytes at frame 1 to 36,519,936 bytes at frame 14,400 during the full interaction workload. During the final 300-second cursor path it rose from 35,827,712 to 36,519,936 bytes, about 0.66 MiB. In those final heartbeats the desktop, window, cursor, background, and framebuffer stage deltas were zero; taskbar deltas were 4–8 KiB per 1,800-frame interval. This is a small residual page-growth signal, not a perfectly flat counter. It is close to earlier accepted idle/widget observations (about 0.52 MiB over three minutes), and the measurement does not identify a P3 cursor or desktop allocation source. The much larger transient window-stage increase during the interaction phase was paired with a 28.3 MiB decrease during input processing in that interval; memory settled before the cursor-only checkpoints.

The corrected stress summary reported 104/51 total selections/deselections (including the initial visual proof), 51/51 drag starts/ends, 27 activations, 26 Start opens, 20 fresh screenshots, 11,831 mouse packets, 11,189 mouse moves, and zero dropped mouse packets. The final screenshot shows the cursor over FILES, the selected DOCS tile, the Performance widget, and no lingering File Explorer window. It is at `C:\dev\guideXOSUEFI_P3_SANDBOX\out\desktop-p3-evidence\20-cursor-after-soak.ppm`. No separate physical/bare-metal run was available.

## Changes and file list

### P3 implementation already committed in `3f1d155`

- `Kernel/Drivers/Input/MouseEventDispatcher.cs`
- `guideXOS/GUI/UefiDesktopInteraction.cs` (new production geometry and gesture logic)
- `guideXOS/GUI/Desktop.cs`
- `guideXOS/Program.cs`
- `guideXOS/guideXOS.csproj`
- `build.ps1`
- `run_uefi_validation.ps1` (P3 proof and bounded QMP pointer reset)
- `Tests/UefiDesktopInteractionTests/Program.cs`
- `Tests/UefiDesktopInteractionTests/UefiDesktopInteractionTests.csproj`

### Current revalidation change

- `docs/UEFI_DESKTOP_POLISH_P3.md` (this report update; evaluated source snapshot `1e2dba921831364719eb816b4d76bdea0b89fca0`).

No generated binaries, screenshots, or build outputs were copied into the live checkout. The report update is the only file to be committed for this revalidation.

Manual hardware checklist:

1. Move the pointer to each screen corner and across light/dark content; confirm the arrow tip remains the click point and no trail remains.
2. Click a tile graphic and label, click another tile, then click empty desktop; confirm selection and deselection.
3. Double-click FILES and DOCS; verify File Explorer and Notepad open once.
4. Drag a tile slowly and quickly to each work-area edge; verify no teleport, no taskbar overlap, and stable release position.
5. Repeat clicks while windows, the widget, Start, and context menus are visible; verify only the topmost owner responds.
6. Right-click empty desktop and a tile, choose an icon size, then verify the new size persists through redraws.

The P3 changes do not alter the managed storage ABI, Persistent authorization, NativeAOT object ownership, storage read bounds, AHCI/FAT persistence, Ring 3 security, managed artifact admission, or the existing six-string cleanup. The only remaining desktop limitation is that icon positions are session-only; saving them requires a separately approved, supported UEFI settings contract.
