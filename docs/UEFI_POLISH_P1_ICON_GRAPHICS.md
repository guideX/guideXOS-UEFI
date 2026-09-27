# UEFI P1: Start and application icon graphics

## Result

The Start artwork was present in the UEFI ramdisk and decoded successfully. The UEFI-specific taskbar renderer omitted the image draw call and painted only its dark button rectangle. The normal Legacy-compatible renderer already selected the normal, hover, and pressed Start sprites.

The UEFI path now draws those existing sprites through `Icons` and `Graphics.DrawImage`, using their native 43×32 bounds for both drawing and hit testing. The pressed state now resolves `startmenubutton_down.png`; it previously reused the hover resource.

The four fixed UEFI desktop tiles and the application icons in Start already used decoded PNGs. The separate UEFI taskbar renderer does not draw running-window buttons or their icons. That taskbar omission remains bounded and is not a resource lookup or decoder failure.

## Current icon ownership and consumers

`IconsPrivate` is the existing size-indexed image catalog. `AppCollection.LoadDefaultApps` assigns each built-in application its icon from that catalog: Computer Files uses Folder, Notepad uses Notepad, Calculator uses Calculator, and Task Manager uses Applications. Start-menu rows draw those `AppCollection` icon references. The application descriptor `ResourceKey` identifies launch resources; it is not a separate icon registry.

The UEFI home desktop keeps its four existing tiles: FILES uses Folder, DOCS uses Documents, IMAGES uses Image, and AUDIO uses Music. Each tile draws a cached 48×48 image inside its existing 64×64 tile, inset by 8 pixels. The desktop does not expose Notepad or Calculator as separate home tiles; their application icons appear in Start.

The UEFI taskbar calls `TaskbarApplicationEntryRegistry.Reconcile`, but its drawing path currently has no loop that displays running application windows. The common `AppCollection` launch path still assigns each window's `TaskbarIcon`, and the Legacy taskbar renderer consumes it. The UEFI app-window icon consumer is absent, so this P1 restores the Start control sprite without adding or duplicating taskbar ownership.

## Assets and resource path

| Use | Resource path | Native size | Format |
| --- | --- | ---: | --- |
| Start normal | `Images/startmenubutton.png` | 43×32 | PNG, 8-bit RGBA |
| Start hover | `Images/startmenubutton_over.png` | 43×32 | PNG, 8-bit RGBA |
| Start pressed | `Images/startmenubutton_down.png` | 43×32 | PNG, 8-bit RGBA |
| Computer Files | `Images/BlueVelvet/32/folder.png` and `/48/folder.png` | 32×32 / 48×48 | PNG, 8-bit RGBA |
| Notepad | `Images/BlueVelvet/32/notepad.png` | 32×32 | PNG, 8-bit RGBA |
| Calculator | `Images/BlueVelvet/32/calculator.png` | 32×32 | PNG, 8-bit RGBA |
| Task Manager | `Images/BlueVelvet/32/applications.png` | 32×32 | PNG, 8-bit RGBA |
| Desktop tiles | `Images/BlueVelvet/48/{folder,documents,image,music}.png` | 48×48 | PNG, 8-bit RGBA |

The source files are under `ramdisk_src/Images`. The recursive ramdisk builder packs them into `ramdisk.img`; the built image contains the Start states and the representative icon paths. The UEFI loader reads them from the mounted `RdskFS` using the exact paths above. The Legacy reference assets have identical SHA-256 hashes for the Start states and representative icons checked here.

`IconsPrivate.LoadPngImage` reads the file once during icon-catalog initialization and uses the post-EBS-safe `PngLoader`. The catalog has five size buckets (16, 24, 32, 48, 128); each initializes 19 image fields, a bounded 95 PNG decode attempts at first use. This includes the same 43×32 Start state files in each size bucket. The normal UEFI taskbar reads cached references from the 32 bucket; it does not scan the filesystem or decode during a frame. The desktop's four common 48-pixel images are also cached references. The temporary ramdisk byte buffer is released after decode.

Missing files or failed decodes return a size-bounded empty `Image`, which the renderer can safely draw as transparent. Startup checks report the representative loaded buffers once. The runtime evidence below had zero icon fallbacks. There is no arbitrary icon-ID resolver in the current catalog, so an invalid-ID case does not exist; missing-resource fallback itself was reviewed but not injected in QEMU.

## Alpha, bounds, and render order

The assets use 8-bit RGBA PNG. `PngLoader` converts those pixels to the image buffer's ARGB order without premultiplying RGB. `Graphics.DrawImage` uses the existing straight-alpha blend path, leaving transparent pixels unchanged. The PNG diagnostic confirmed transparent, partial-alpha, and opaque samples and a successful alpha-composited render.

The Start control draws at its native 43×32 size at the current taskbar origin. Hover selects the over image; a left press within the same bounds selects the down image. The Start-menu click handler and menu ownership are unchanged. Application menu icons remain native 32×32. The UEFI desktop tiles remain native 48×48 with the existing 8-pixel inset.

The normal render order remains background, desktop tiles and taskbar, windows and popups, then cursor. The new sprite is drawn by the UEFI taskbar on the current `Framebuffer.Graphics` path; it does not bypass triple buffering or add a diagnostic-only draw.

## Runtime evidence

QEMU serial logs from the production AppRuntime and Continuous runs reported:

```text
[ICON_GRAPHICS] start=ok;hover=ok;pressed=ok;files=ok;notepad=ok;calculator=ok;taskmanager=ok;fallbacks=0
[ICON_GRAPHICS] desktop-drawn=files,docs,images,audio
[ICON_GRAPHICS] start-drawn=1;bounds=12,764,43x32
```

The AppRuntime guest opened Start 20 times and selected/launched Calculator, Notepad, and Computer Files from the app list; those windows closed through the existing lifecycle path. The taskbar grouping diagnostic reported `PASS`, zero stale owners, and successful cleanup. The overall host AppRuntime selector did not pass: it waited for a `Computer Files → Scripts` marker while the guest opened `Programs/`. The guest had already completed the icon, Start-menu, representative launch/close, and grouping checks; this is a host click/marker mismatch, not a graphics failure.

Other QEMU controls:

| Control | Result |
| --- | --- |
| PNG | `DIAGNOSTIC_COMPLETE`; decode and alpha render passed; invalid signature, truncated header, impossible dimensions, and truncated stream were rejected. |
| AppModel | `DIAGNOSTIC_COMPLETE`; 12 descriptors, 12 factories, zero fallbacks. |
| NativeInput | `TIMEOUT_SUCCESS`; graphics valid, keyboard and mouse input balanced, 54 left-button down/up pairs. |
| ContextMenu | `CONTEXT_MENU_COMPLETE`; 104 desktop opens/draws with valid bounds, 25 icon-size activations, 50 click-away dismissals, and one taskbar popup drawn/dismissed. |
| Production Continuous | `TIMEOUT_SUCCESS` after 120 seconds, frame 1800; graphics valid, triple buffering enabled, allocator corruption/no-pages failures zero, and `ThreadPool.Locked=0`. Allocator bytes rose from 18,554,880 to 18,919,424 over this run. |

The runtime harness does not save a guest screenshot, so visual evidence is the production draw marker and QEMU renderer/alpha diagnostics. The guest accepted the Start click and opened the Start menu. Hover and pressed asset selection are wired to the pointer/button state in the same UEFI draw function.

The Phase 31/32 managed controls remain independently unstable after their proof code. The fresh Phase 31 run hit a frame exception while drawing windows after the Ring 3 proof; a pre-polish log (`serial_uefi_validation_20260926_190123.txt`) has the same fault. The fresh Phase 32 run reached its OpenDocument/lifetime proof markers but did not reach the runner's completion marker; a repeat exited QEMU after the Phase 32 proof markers. An earlier pre-polish Phase 32 run (`serial_uefi_validation_20260926_185955.txt`) completed. These controls do not modify the icon cache or Start draw path and are recorded separately from the P1 graphics outcome.

The full validation script's initial rebuild stopped in its Phase 26 staging step because the overlay compiler could not locate the installed Windows SDK `crtdbg.h`. The kernel itself compiled and converted. Subsequent GUI/control kernels were built with the existing ramdisk reused (`-SkipRamdisk`), then assembled into the ESP; no runtime-source edits were made for this P1.

## Remaining polish

- Add the existing App Model-owned window icons to the UEFI taskbar if visible running-app buttons are in scope; the current UEFI taskbar does not draw app-window entries.
- Investigate the intermittent Phase 31/32 post-proof window-draw failure separately.
- Keep desktop icon dragging/reordering, icon-size command behavior, cursor artwork, and splash artwork as separate polish items.
