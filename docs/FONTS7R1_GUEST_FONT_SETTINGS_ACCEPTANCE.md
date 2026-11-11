# FONTS7R1 Guest Font Settings Acceptance

**Outcome B — partial acceptance.** Production Bitmap/TrueType switching and all six TrueType sizes were exercised in an authentic UEFI guest. The installed-system persistence gate remains guest-unverified because both authorized runs booted the LiveMode ESP. The shell/dialog gate is partial: the desktop and taskbar context menus, Start Menu, selected dialogs, desktop labels, taskbar label, and clock/date were reached; MessageBox and visible notifications were not.

## Scope and qualification baseline

- Repository: `D:\dev\guideXOSUEFI_FONT_SUPPORT`
- Branch: `FONT_SUPPORT`
- Starting HEAD: `9b91b33a216d4ab2a61d59ac4b1e3b37da543600`
- The preflight worktree had only the pre-existing untracked `Tests/UefiDesktopInteractionTests/bin/` and `obj/` directories. They were preserved and remain untracked.
- [FONTS6R1](FONTS6R1_GUEST_TRUETYPE_ACCEPTANCE.md) and [FONTS7](FONTS7_FONT_SETTINGS_AND_SHELL_CONSISTENCY.md) reports were reviewed. The accepted FONTS7 baseline remains applicable for its 566 hosted checks, 42 desktop interaction assertions, Task Manager/focus and notification regressions, source/ESP build, managed artifact agreement 41/41, and preserved FONTS1 pixel hash `0xEC1A69C86C9C5CC6`.
- The 42 desktop interaction assertions and unrelated Task Manager/focus suites were not repeated. This milestone changes only font-dependent shell measurement/layout and its focused hosted regressions.

New evidence is under `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS7R1`. Screenshot hashes are recorded in `evidence\screenshot-sha256.csv`.

## Targeted repairs

Guest inspection of the original FONTS7 candidate reproduced three layout defects at 24 px:

1. Desktop tile clipping came from calculating tile label clips and hit bounds with Bitmap metrics while drawing TrueType glyphs. Tile layout now uses the selected provider’s measurements.
2. The desktop context menu kept Bitmap-derived width, clipping “Performance Widget.” Its menu width now measures labels with the selected provider.
3. The 24 px taskbar clock/date used two lines in a 40 px bar and a Bitmap-derived reserved width. The TrueType clock now reserves selected-provider width and places the full time and date on one centered line when two lines do not fit.

Focused hosted checks cover 24 px tile bounds, context-menu width, and one-line clock/date pixels. No input routing, window geometry, fixed button dimensions, or persistence mechanism was changed.

## Build and artifact identity

The repaired candidate was built in an isolated detached worktree at `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS7R1\build\candidate-source`, based on the starting HEAD. The command was `build.ps1 -SkipBootloader -SkipManagedProofs`. The unchanged accepted bootloader and ramdisk were reused after source identity and hashes were verified; the kernel was rebuilt from the repaired source. The managed output reuse records are `evidence\reuse-managed-out-copy.log` and `evidence\reuse-managed-native-copy.log`.

The build completed successfully. Its managed artifact audit contains 41 rows and reports 41/41 agreement for descriptor, build/staging, RAM-disk staging, and compiled allowlist checks. The full build log is `evidence\build-repaired-candidate-final.log`; the audit is `build\candidate-source\out\h2c-managed-artifact-audit.csv`.

| Artifact | Original accepted FONTS7 SHA-256 | Repaired candidate SHA-256 |
|---|---|---|
| `BOOTX64.EFI` | `89EF110B0F317CA77BCDC91DC8F90840AC47AAA8AD14D9905B3D8910F3C1F593` | same |
| `kernel.elf` | `51A4BE4443652320349B23A381CEC8A7F936B442C1297EA8AEAF548E7CC1D8FF` | `B2E2CA9A63D929336B1D6D80C7C3D1D6C73930E0D776B09E7628C8E64592A757` |
| `ramdisk.img` | `C019F7783D454BD357F7D256141EAE85E07BA85C6AD9A38F4DEBFF3C6DF8A841` | same |
| OVMF code | `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A` | same |
| Candidate managed audit CSV | baseline audit: `CF4910F9A5B1658A85E8884756C70D9632A0401FFF83394D224017C9CF2D9B90` | `16FF5A2367F9D55DD2A4582649BCA297B3466FE239D2A14ECE8C7565121456FC` |

The pre-run artifact inventory is `evidence\artifact-hashes-pre-run.csv`; repaired candidate hashes and lengths are in `evidence\artifact-hashes-repaired-candidate.csv`. OVMF vars files were disposable per-run copies of the fresh template. Their template SHA-256 was `5D2AC383371B408398ACCEE7EC27C8C09EA5B74A0DE0CEEA6513388B15BE5D1E`; initial and post-run copy hashes are in `evidence\firmware-vars-run-hashes.csv`. Firmware changed each copy during boot, so the post-run files are not treated as system-settings persistence evidence.

The final hosted run passed **570 checks, 0 failures**: FONTS1 308, FONTS2 89, FONTS3 shell text 113, FONTS4 UI 34, production provider 17, and font preference configuration 9. The FONTS1 golden hash remains `0xEC1A69C86C9C5CC6`. The run log is `evidence\hosted-true-type-regression-final.log`. Existing configuration tests also verified unsupported size fallback to Bitmap defaults, unknown provider fallback while retaining a valid size, and partial font settings fallback.

## QAC authorization and guest identity

No standalone guest was launched. Each QEMU process was the direct child of an accepted QAC reservation, and QAC reported zero foreign processes.

| Run | Reservation / project | Verified owner | QEMU child | Result |
|---|---|---|---|---|
| Repaired font qualification | `fd3aa8e49968403cb176e38e234f54aa` / `guideXOSUEFI-FONTS7R1-repaired` | `LEON2_RDT\guideX` PID 26036 | PID 23048, launched `2026-11-11T17:13:37.8727699Z` | Healthy acceptance reservation; orderly guest reboot observed; released by QMP quit. |
| Taskbar context-menu coverage | `77f4ede75a4f4d3192182a5081668744` / `guideXOSUEFI-FONTS7R1-taskbar-context` | `LEON2_RDT\guideX` PID 11652 | PID 18984, launched `2026-11-11T17:31:19.8625544Z` | Healthy acceptance reservation; Bitmap and TrueType taskbar menu screenshots captured; released by QMP quit. |

Live and post-shutdown ownership records are `evidence\qac-repaired-live-{status,list,queue}.txt`, `evidence\qac-repaired-post-shutdown-{status,list,queue}.txt`, and `evidence\qac-taskbar-context-{live,pre-shutdown,post-shutdown}-{status,list,queue}.txt`. Both post-shutdown status files show 0 QEMU processes, 0 managed sessions, and 0 queued or blocked reservations. An earlier admission attempt was aborted before guest interaction when QAC’s census degraded and QEMU exited with an access violation; QAC auto-released it. That attempt is documented in `evidence\qac-post-abort-*.txt` and is not counted as qualification.

## Rendering gate — ACCEPTED

The repaired ESP booted in UEFI and reported LiveMode. A fresh configuration selected Bitmap at generation 1; the Fonts panel showed `Active: Bitmap 20 px`. Selecting TrueType changed the production provider, and serial diagnostics confirmed real drawing, glyph rasterization, and cache use rather than relying on the Settings selection state.

| TrueType size | Guest result | Screenshot |
|---:|---|---|
| 12 px | Active TrueType; 28 rasterizations, 28 cache entries, 1,541 cache bytes | `screenshots\repaired-truetype-12px.png` |
| 14 px | Active TrueType; 28 rasterizations, 28 cache entries, 1,951 cache bytes | `screenshots\repaired-truetype-14px.png` |
| 16 px | Active TrueType; 28 rasterizations, 28 cache entries, 2,453 cache bytes | `screenshots\repaired-truetype-16px.png` |
| 18 px | Active TrueType; 28 rasterizations, 28 cache entries, 2,987 cache bytes | `screenshots\repaired-truetype-18px.png` |
| 20 px | Active TrueType; 28 rasterizations, 28 cache entries, 3,750 cache bytes | `screenshots\repaired-truetype-20px.png` |
| 24 px | Active TrueType; 28 rasterizations, 28 cache entries, 5,299 cache bytes | `screenshots\repaired-truetype-24px.png` |

The complete provider/cache trace is `evidence\guest-serial-repaired.log`. Screenshots `repaired-font-settings-truetype.png`, `repaired-desktop-truetype-24px.png`, and `repaired-truetype-24px-context-menu.png` show the selected provider and rendered shell glyphs. At 24 px, all four desktop labels fit their tile bounds, the desktop context menu fully displays “Performance Widget,” and the taskbar clock/date show the full time and date on one line. The taskbar context menu also displayed “Task Manager” in TrueType 24 px within its existing row; see `screenshots\repaired-taskbar-context-menu-truetype-24px-high.png`.

Returning to Bitmap required no reboot. Repeated Bitmap → TrueType 16 → Bitmap → TrueType 12 → Bitmap changes advanced provider generations without fallback or a guest fault. `screenshots\repaired-bitmap-context-menu-after-switch.png` and `screenshots\repaired-taskbar-context-menu-bitmap.png` show Bitmap spacing restored. A later orderly LiveMode reboot from TrueType 18 px returned to Bitmap generation 1; see `repaired-live-mode-truetype-18px-before-reboot.png`, `repaired-start-menu-before-live-reboot.png`, and `repaired-live-mode-rebooted-bitmap-default.png`.

## Persistence gate — INSTALLED GUEST UNVERIFIED

The existing Settings mechanism was inspected. `RightMenu` exposes **Save Settings** only when `SystemMode.IsLiveMode` is false; that action calls the existing `Configuration.SaveConfiguration()`. The existing configuration stores font provider and size in `/etc/guidexos/config.ini`, and its loader normalizes invalid or partial font values. The guest serial log instead reported `[BOOT] System Mode: LiveMode (USB/CD)`, and the Fonts panel stated that LiveMode selections are session-only. No Save Settings action was available in this guest, so no successful installed save or persisted provider/size restore is claimed.

The orderly LiveMode reboot confirmed session-only behavior: TrueType 18 px was active before reboot; the next boot reported Bitmap active at generation 1. No writable installed UEFI guest was available. The repository’s existing HDInstaller writes an MBR/GRUB layout; it does not provide a ready disposable UEFI-installed guest for this run. A real installed-system save/reboot/restore test and guest-file invalid-value test remain open. Hosted parser fallback checks passed; no synthetic writes or invalid files were injected into the LiveMode ramdisk.

## UI coverage and observations

| Surface | Repaired candidate evidence | Result / limit |
|---|---|---|
| Desktop labels | Bitmap boot and post-reboot screenshots; TrueType 24 px desktop screenshot | Bitmap and TrueType glyphs render. The 24 px tile clip and hit bounds now use TrueType measurements; all four labels remain visible. |
| Window title bars | Fonts panel and Notepad screenshots from the source-matched FONTS7 guest | Provider propagation observed. Title text remained within its window bounds. Dialog/title coverage is reused from the original FONTS7 candidate because these paths were not changed by the targeted shell measurement repairs. |
| Taskbar application label | `screenshots\repaired-font-settings-truetype.png` and `repaired-truetype-24px.png` | TrueType reaches the taskbar label. At 24 px the long `Display Options` label is clipped/truncated by the existing fixed 140 px taskbar button. The button and hitbox were not resized. |
| Taskbar clock/date | `screenshots\repaired-truetype-24px-context-menu.png` | Full time and date are visible on one line at 24 px and remain inside the taskbar. |
| Start Menu | `screenshots\repaired-start-menu-before-live-reboot.png`; prior `screenshots\bitmap-start-menu-after-reboot.png` and `truetype-24px-start-menu.png` | Reached in TrueType 18 px on the repaired candidate and both providers in the prior FONTS7 guest. Longer entries and fixed buttons still truncate at larger sizes within their existing bounds. |
| Desktop context menu | `screenshots\repaired-bitmap-context-menu-after-switch.png`, `repaired-truetype-24px-context-menu.png` | Bitmap spacing is retained. TrueType spacing propagates, and the longest item fits after the provider-aware width repair. |
| Taskbar context menu | `screenshots\repaired-taskbar-context-menu-bitmap.png`, `repaired-taskbar-context-menu-truetype-24px-high.png` | Reached under both providers. “Task Manager” is readable and fits its current row at TrueType 24 px. No Task Manager launch/focus regression was investigated here. |
| MessageBox | Not reached | Hosted layout checks cover MessageBox text, but no safe production desktop action was identified in this guest run. |
| Shutdown and Save Changes dialogs | `screenshots\truetype-24px-shutdown-dialog.png`, `truetype-24px-save-changes-dialog.png` from the original FONTS7 guest | TrueType title/body rendering was visible and the dialogs were cancelled safely. The fixed “Don’t Save” button label remains clipped. No dialog implementation changed in the repaired candidate. |
| Notifications | Not visibly reached | App launches were attempted in the original guest, but screenshots show application surfaces rather than a notification. Source search found no production call to `NotificationManager.Update()` in the desktop path. Keep notification display integration separate from font acceptance. |

The taskbar label truncation and fixed Start Menu/dialog button clipping are existing control-width limits, recorded rather than expanded into general geometry changes. MessageBox and notifications remain explicit UI coverage gaps.

## Recommended FONTS8 scope

1. Boot a disposable writable UEFI-installed guest and prove the existing Save Settings action, orderly reboot, restored provider and size, actual TrueType drawing after restore, and invalid stored-value fallback.
2. Add a safe production route to exercise MessageBox and a visible notification, then verify both providers at representative sizes.
3. Decide whether the fixed taskbar app button and dialog/Start Menu labels should retain clipping or gain provider-aware truncation while preserving hitboxes. Keep Task Manager activation/focus work isolated unless separately scheduled.

**Gate summary:** Rendering **accepted**; installed persistence **unverified**; UI coverage **partial**. Outcome B.
