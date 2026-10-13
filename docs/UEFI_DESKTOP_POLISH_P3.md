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

The focused hosted project links the production interaction source directly and passed 42 assertions covering tile and label hit bounds, empty-space misses, selection/deselection, double-click timing and distance, drag threshold and offset, activation-vs-drag, work-area clamping, pointer capture, higher-priority input ownership, and cursor hotspots.

The ordinary full build completed successfully in the sandbox. Its configured Phase 27–35 proof cohorts passed, including the Phase 35 contracts; no Phase 36 work was started. A targeted `DesktopP3` diagnostic build also completed before the guest runs. The build logs and artifacts remain in the sandbox and were not copied to the live checkout.

The QEMU guest proof used the sandbox-built EFI image at 1280×800. It loaded both 28×28 cursor assets, selected by graphic and label, deselected on empty desktop, activated Notepad by double-click, moved and dropped DOCS, opened Start and desktop context menus, changed icon size to 32, opened and clicked the Performance widget, dismissed a context menu without activating the icon underneath, launched File Explorer from FILES, and captured the pointer at both screen corners. The final baseline counters were 4 selections, 1 deselection, 2 activations, 1 drag start/end, 11/11 left down/up, 1/1 right down/up, and 0 dropped mouse packets. All 19 expected screenshots were present in `C:\dev\guideXOSUEFI_P3_SANDBOX\out\desktop-p3-evidence`.

The opt-in stress workload exercised 100 selections, 50 deselections, 50 drag/drop cycles, 25 Notepad activations, 25 Start open/close cycles, and 300 seconds of cursor motion over icons, wallpaper, the widget, taskbar, Start, and screen edges. I fixed the harness pointer reset after the first measurement showed a 79-pixel residual at the right edge: it now sends enough bounded negative reports to return a 1280-pixel guest pointer to zero before absolute test moves. The corrected run reached the intended coordinates, closed File Explorer before the soak, and passed the runner's event, balanced-input, packet-drop, graphics-invariant, and screenshot checks.

## Stability and remaining verification

The stress run completed 12 heartbeats through frame 14,400. Stack low-water stayed at 2,620,160 bytes, graphics invariants stayed valid, dropped mouse packets stayed at zero, and allocator free-invalid, free-no-pages, and corruption counters stayed at zero. Left button transitions balanced at 318/318; right transitions balanced at 3/3. No startup `#BP`, PF, GP, or UD appeared in the serial log.

`Allocator.MemoryInUse` reports in-use allocator pages (`PageInUse × PageSize`), so it measures committed page usage rather than cumulative allocation volume or exact live managed objects. It rose from 19,152,896 bytes at frame 1 to 36,519,936 bytes at frame 14,400 during the full interaction workload. During the final 300-second cursor path it rose from 35,827,712 to 36,519,936 bytes, about 0.66 MiB. In those final heartbeats the desktop, window, cursor, background, and framebuffer stage deltas were zero; taskbar deltas were 4–8 KiB per 1,800-frame interval. This is a small residual page-growth signal, not a perfectly flat counter. It is close to earlier accepted idle/widget observations (about 0.52 MiB over three minutes), and the measurement does not identify a P3 cursor or desktop allocation source. The much larger transient window-stage increase during the interaction phase was paired with a 28.3 MiB decrease during input processing in that interval; memory settled before the cursor-only checkpoints.

The corrected stress summary reported 104/51 total selections/deselections (including the initial visual proof), 51/51 drag starts/ends, 27 activations, 26 Start opens, 20 fresh screenshots, 11,831 mouse packets, 11,189 mouse moves, and zero dropped mouse packets. The final screenshot shows the cursor over FILES, the selected DOCS tile, the Performance widget, and no lingering File Explorer window. It is at `C:\dev\guideXOSUEFI_P3_SANDBOX\out\desktop-p3-evidence\20-cursor-after-soak.ppm`. No separate physical/bare-metal run was available.

## Files changed

- `Kernel/Drivers/Input/MouseEventDispatcher.cs`
- `guideXOS/GUI/UefiDesktopInteraction.cs` (new production geometry and gesture logic)
- `guideXOS/GUI/Desktop.cs`
- `guideXOS/Program.cs`
- `guideXOS/guideXOS.csproj`
- `build.ps1`
- `run_uefi_validation.ps1` (P3 proof plus corrected QMP pointer reset)
- `Tests/UefiDesktopInteractionTests/Program.cs`
- `Tests/UefiDesktopInteractionTests/UefiDesktopInteractionTests.csproj`
- `Docs/UEFI_DESKTOP_POLISH_P3.md`

Manual hardware checklist:

1. Move the pointer to each screen corner and across light/dark content; confirm the arrow tip remains the click point and no trail remains.
2. Click a tile graphic and label, click another tile, then click empty desktop; confirm selection and deselection.
3. Double-click FILES and DOCS; verify File Explorer and Notepad open once.
4. Drag a tile slowly and quickly to each work-area edge; verify no teleport, no taskbar overlap, and stable release position.
5. Repeat clicks while windows, the widget, Start, and context menus are visible; verify only the topmost owner responds.
6. Right-click empty desktop and a tile, choose an icon size, then verify the new size persists through redraws.

The P3 changes do not alter the managed storage ABI, Persistent authorization, NativeAOT object ownership, storage read bounds, AHCI/FAT persistence, Ring 3 security, managed artifact admission, or the existing six-string cleanup. The only remaining desktop limitation is that icon positions are session-only; saving them requires a separately approved, supported UEFI settings contract.
