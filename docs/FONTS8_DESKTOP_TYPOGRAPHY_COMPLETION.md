# FONTS8 Desktop Typography Completion

**Outcome A — TYPOGRAPHY_COMPLETION_ACCEPTED.** The verified clipping defects are corrected, production MessageBox and notification paths pass hosted coverage, all requested regressions pass, and the production kernel/ramdisk/ESP build and artifact audit succeed. Guest validation and installed-system persistence remain separate pending gates.

## Scope and preflight

- Repository: `D:\dev\guideXOSUEFI_FONT_SUPPORT`
- Branch: `FONT_SUPPORT`
- Starting HEAD: `7af27ae89aac71df8b626418b208093412689f02`
- The pre-existing untracked `Tests/UefiDesktopInteractionTests/bin/` and `obj/` directories were preserved.
- New large build outputs and evidence are under `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS8`.
- The FONTS7R1 acceptance report and its rendering evidence were reviewed. The existing bitmap fallback, TrueType parser/rasterizer/cache, provider selection, and six desktop sizes remain in place.

## Root causes and corrections

The remaining shell defects came from fixed text regions without a fit policy. At larger sizes, taskbar application labels, Start Menu entries, and the Save Changes “Don't Save” label were drawn into existing narrow rows or buttons and visually cut off. The English Shutdown labels (“No” and “Yes”) fit their existing buttons, but used the non-fitted rendering path. These were layout limits, not a rasterizer or cache failure.

`SharedTextFont.DrawTextFitted` now measures and draws with the same selected provider, preserving right alignment and the caller's clip rectangle. It adds a deterministic `...` only when a one-line label exceeds its available width. The fitted prefix ends at Unicode scalar boundaries and scans at most 4,096 scalars without allocating a temporary string or line buffer. The existing shared layout remains responsible for multiline wrapping.

Taskbar labels, Start Menu and context-menu rows, and fixed dialog labels now use that shared fit path. Save Changes keeps its button dimensions and hit regions; Bitmap retains its legacy pixels when the label already fits and uses ellipsis when it does not. Shutdown button labels are centered and clipped inside their original bounds. No taskbar, Start Menu, or dialog button hitbox was enlarged.

MessageBox previously kept a fixed height even when its body wrapped, and its bitmap drawing path did not use the shared wrapping layout. It now measures the selected provider, chooses a width capped at 640 px and the available screen width, sizes to the wrapped body, and clips the body to the work area above the taskbar. At very small displays, or for text longer than the available work area, the body remains clipped to the window bounds; scrolling was not added. If no shared provider is selected while the legacy bitmap font is available, MessageBox caches and uses a bitmap `SharedTextFont` fallback so the body still wraps.

The public `System.Windows.Forms.MessageBox.Show(string)` path now ensures the production window exists, places it at the top of the window list, and shows it. Existing visibility-based dismissal remains unchanged.

The notification manager already had provider-aware wrapping and sizing, but the UEFI frame paths did not call `NotificationManager.Update()`. Both active UEFI rendering paths now call it after taskbar drawing. Notification width and animation bounds are recomputed on provider, size, or available-width changes while preserving in-progress animation or completed timing. Existing notification producers remain the trigger; no production-only test notification was added.

## Text-fitting policies

| Text use | Policy |
|---|---|
| Fixed one-line labels | Provider-measured ellipsis when the text exceeds its existing content width; clip to the control bounds. |
| MessageBox and notification bodies | Explicit line wrapping with the selected provider's metrics; clip to the available window or toast rectangle. |
| Fitting labels | Draw unchanged when the whole label fits. Bitmap dialog labels retain legacy rendering in that case. |
| Overflow beyond the available display height | Clip within the bounded content area. No interactive geometry or scrolling behavior is changed. |

`SharedTextLayout` continues to handle proportional measurement, explicit line breaks, alignment, and wrapped drawing. Fixed-control fitting uses the same provider metrics for measurement and drawing, including half-open clipping rectangles and Unicode scalar-safe truncation.

## MessageBox and notification coverage

The hosted test project links the production `MessageBox.cs` and `NotificationManager.cs` implementations. It checks the public MessageBox show path, lazy window creation, visibility, topmost ordering, wrapped long text, screen bounds, bitmap fallback, and dismissal. The fallback test sets no selected provider while retaining the bitmap font and verifies that the long message wraps.

MessageBox rendering is checked at Bitmap 20 px and TrueType 12, 14, 16, 18, 20, and 24 px. TrueType 24 px also runs in a 240×200 framebuffer; the other MessageBox sizes use 320×240. Long bodies wrap and remain clipped to the window content rectangle. The tests confirm that the title area, body, and window stay within the work area.

The production `NotificationManager` tests use long messages and cover Bitmap 20 px, TrueType 24 px, and switches through TrueType 12, 14, 16, 18, and 20 px. They exercise repeated notifications, movement, clipping, animation reflow, provider switching, and timed dismissal. Existing boot and application notification producers are unchanged.

## Font-size verification matrix

The desktop TrueType selector supports 12, 14, 16, 18, 20, and 24 px. The production Bitmap atlas is configured at 20 px; it does not expose the six TrueType size choices.

| Surface | Bitmap 20 px | TrueType sizes | Geometry/result |
|---|---:|---|---|
| Taskbar application label (“Display Options”) | ✓ | 12, 14, 16, 18, 20, 24 | Ellipsis fits the existing taskbar button. |
| Start Menu and bounded menu rows | ✓ | 12, 14, 16, 18, 20, 24 | Ellipsis remains inside the existing row and hit bounds. |
| Save Changes button labels | ✓ | 12, 14, 16, 18, 20, 24 | “Don't Save” ellipsizes only when needed; button geometry stays fixed. |
| Shutdown “No” / “Yes” labels | ✓ | 12, 14, 16, 18, 20, 24 | Centered and clipped inside existing buttons. |
| Production MessageBox | ✓ | 12, 14, 16, 18, 20, 24 | Wrapped body; TrueType 24 px also tested at 240×200. |
| Production notifications | ✓ | 12, 14, 16, 18, 20, 24 | Long body, movement, clipping, and provider switching. |

Fixed-control cases compare production renderer pixels against the expected provider-fitted output and check ink stays inside the allocated clip. They also assert the relevant button and row dimensions remain unchanged. The MessageBox and notification rows exercise their actual production classes rather than only isolated glyph fixtures.

## Hosted verification

| Suite | Result |
|---|---:|
| FONTS1 deterministic raster and parser suite | 308 pass |
| FONTS2 hosted suite | 89 pass |
| FONTS3 shell text | 113 pass |
| FONTS4 layout and production-control coverage | 98 pass |
| Font preference configuration | 9 pass |
| FONTS6 production provider | 17 pass |
| **Hosted total** | **634 pass, 0 failures** |
| UEFI desktop interaction regression | 42 assertions pass |
| Task Manager memory/focus regression | Pass |
| Notification boot-reset regression | Pass |

The FONTS1 warm-draw pixel hash remains `0xEC1A69C86C9C5CC6`. Hosted logs are `evidence/hosted-true-type-regression.log`, `evidence/desktop-interaction-tests.log`, `evidence/taskmanager-focus-regression.log`, and `evidence/notification-boot-reset-regression.log` under the FONTS8 artifact directory.

## Production build, ESP, and managed artifact provenance

The production build ran from an isolated detached worktree at `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS8\build\candidate-source`, based on the starting HEAD with the FONTS8 source changes applied. The command was `build.ps1 -SkipBootloader -SkipManagedProofs`. It rebuilt the kernel and ramdisk and assembled the ESP. The unchanged accepted FONTS7R1 bootloader was reused and hash-verified; the managed proof payloads staged from the accepted FONTS7R1 build were not rebuilt.

The build used .NET SDK 10.0.401, bundled Python 3.12.14, and LLVM `llvm-objcopy`. It succeeded with existing SDK and repository compiler warnings, including the `net7.0` end-of-support and runtime-identifier inference diagnostics.

The managed audit contains 41 records and reports 41/41 agreement for descriptors, build outputs versus staged executables, the compiled kernel allowlist, and ramdisk staging. The candidate reuses the managed proof payloads staged in FONTS7R1; each audit row traces its build output back to the original FONTS7 source-built proof and verifies the executable, descriptor, compiled allowlist, and ramdisk copies. The proof packages were not rebuilt in FONTS8.

| ESP artifact | Bytes | SHA-256 |
|---|---:|---|
| `EFI/BOOT/BOOTX64.EFI` (reused) | 49,664 | `89EF110B0F317CA77BCDC91DC8F90840AC47AAA8AD14D9905B3D8910F3C1F593` |
| `kernel.elf` (rebuilt) | 4,767,744 | `CB1E49F3CE3128F3692C2243E7976D94C705D705E337CA85E436A46EEC529553` |
| `ramdisk.img` (rebuilt from accepted staged sources) | 57,053,414 | `C019F7783D454BD357F7D256141EAE85E07BA85C6AD9A38F4DEBFF3C6DF8A841` |

The ESP kernel hash equals the rebuilt kernel source hash. The ESP ramdisk hash equals both the rebuilt ramdisk and the accepted FONTS7R1 ramdisk hash. The detailed build log, artifact hashes, and 41-row audit are in `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS8\evidence`; the source patch and candidate outputs are under its `build` directory.

## Guest evidence and persistence boundary

The QAC coordinator was online, but its acceptance lane was exclusive and running an `APP_MODEL` managed guest when inspected. QAC showed zero foreign QEMU processes. No reservation was requested, and the existing guest was left untouched. Guest testing and screenshots are therefore pending; no standalone QEMU session was started.

FONTS7R1 established that LiveMode font preferences are session-only. This phase does not treat a LiveMode reboot as a persistence failure and does not add a persistence subsystem. Installed-system Save Settings persistence remains unverified because no writable installed UEFI guest was available.

## Recommended next milestone

When the QAC acceptance lane is available, qualify the rebuilt ESP at one small and one large TrueType size, prioritizing 24 px. Capture the taskbar label, Start Menu, Save Changes, Shutdown, MessageBox, and a naturally triggered notification. Keep writable installed-system Save Settings and reboot/restore validation as a separate acceptance gate.
