# FONTS6R1 Guest TrueType Acceptance

**Outcome A — `GUEST_TRUETYPE_ACCEPTED`.** The authentic guideXOS UEFI guest booted from the source-matched FONTS6 image, rendered through production TrueType with nonzero glyph rasterization and cache use, and switched back to Bitmap twice without rebooting. The Bitmap default remained active at boot. This result covers the required provider path; the surface table below records areas that were not reached or remain visibly incomplete.

## Run identity and scope

- Repository: `D:\dev\guideXOSUEFI_FONT_SUPPORT`
- Host: `LEON2_RDT`
- Branch: `FONT_SUPPORT`
- Starting HEAD: `22d5b167fe10869ae4402caaeea49ace470a5660`
- Qualification artifacts: `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS6R1`
- User-facing selector: desktop right-click → **Display Options** → **Fonts** → **Bitmap** or **TrueType**. Selection is session-only.

No production source or build changes were needed. The production/build source files in the checkout matched their FONTS6 detached build-worktree counterparts by SHA-256 before launch. The detached build worktree and historical FONTS6 artifacts were left untouched. The pre-existing untracked `Tests\UefiDesktopInteractionTests\bin\` and `obj\` directories were preserved.

## Build and artifact provenance

FONTS6 had already qualified 459 font checks, 42 desktop interaction assertions, Task Manager/focus and notification boot-reset regressions, and the authentic image build. Its managed artifact audit reports 41/41 agreement across descriptors, build outputs, staging, RAM disk, and compiled allowlist; the RAM disk contains 595 files. Those hosted/build results were reused because this retry made no production changes; no test suites were rerun.

The guest used a separate copy of `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS6\build\ESP` under the FONTS6R1 artifact folder. It matches the previously qualified build artifacts:

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `ESP\EFI\BOOT\BOOTX64.EFI` | 49,664 | `89EF110B0F317CA77BCDC91DC8F90840AC47AAA8AD14D9905B3D8910F3C1F593` |
| `ESP\kernel.elf` | 4,763,648 | `B30BDE8AB949E111D5961512B91C96F78D5815691139D9663CC5132C2A41E4DB` |
| `ESP\ramdisk.img` | 57,053,414 | `C019F7783D454BD357F7D256141EAE85E07BA85C6AD9A38F4DEBFF3C6DF8A841` |
| FONTS6 generated `ManagedArtifactIdentities.g.cs` | 23,204 | `A2D4477ACA82AC76E06C28877464AD7484CE5E167A702853D873AB812954FA84` |

The source identity file and 41-row audit remain in the FONTS6 build evidence. The copied audit is `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS6R1\evidence\h2c-managed-artifact-audit.csv`. Firmware code hash matches the installed QEMU EDK2 x64 image (`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`). The guest writable variables image was copied into the R1 firmware folder.

Existing regression evidence retained in R1 includes:

| Suite | Result |
| --- | --- |
| FONTS1 | 234 checks, 0 failures |
| FONTS2 | 73 checks, 0 failures |
| FONTS3 | 111 checks, 0 failures |
| FONTS4 | 28 checks, 0 failures |
| FONTS6 production selection | 13 checks, 0 failures |
| Font checks total | 459 checks, 0 failures |
| UEFI desktop interaction | 42 assertions passed |
| Task Manager/focus regression | Passed in the existing FONTS6 qualification |
| Task Manager Memory Details regression | Passed |
| NotificationManager boot-reset regression | Passed |

The FONTS1 pixel golden remains `0xEC1A69C86C9C5CC6`. Harness outputs are `font-harness-final.log`, `desktop-interaction-final.log`, `taskmanager-regression-final.log`, and `notification-regression-final.log` under the R1 evidence directory.

## QAC reservation and release

The live QAC dashboard and CLI showed the coordinator online, acceptance available, and zero QEMU/foreign QEMU processes, sessions, queued requests, or blocked requests before launch. QEMU was started only as the direct child of the QAC `run` command. No standalone QEMU was launched and no foreign process was terminated.

| Field | Value |
| --- | --- |
| QAC | guideXOS QAC 2.0.0 |
| Project / mode | `guideXOSUEFI-FONTS6R1` / `acceptance` |
| Reservation | `948eb0a60e0a461ea86126e823fd2a53` |
| Owner | `LEON2_RDT\guideX` (PID 10316) |
| Managed QEMU child | `C:\Program Files\qemu\qemu-system-x86_64.exe` (PID 27448) |
| Launch UTC | `2026-11-08T19:40:41.7598466+00:00` |
| QEMU | 11.0.0, `pc-q35-8.2`, 1024 MiB, 2 vCPUs, EDK2 x64 firmware |
| Exit | QMP `quit`; exit code 0 at `2026-11-08T20:00:02.4461150Z` |
| Release | QAC reported reservation released after managed child exit |
| Final coordinator state | 0 QEMU (0 foreign), 0 reservations/sessions, empty queue, 0 blocked |

Post-run QAC snapshots are `qac-post-run-status.txt`, `qac-post-run-list.txt`, `qac-post-run-queue.txt`, and `qac-session-lifecycle.txt` in R1 evidence. The guest serial log and QMP input/capture log are `guest-serial.log` and `qmp-input.log`.

## Guest boot and provider evidence

The guest booted in UEFI mode to the continuously updating desktop. Serial markers include `CONTINUOUS_DESKTOP_ENTER`, `DESKTOP_REAL_TEXT_RENDERED=1`, and `TASKBAR_REAL_TEXT_RENDERED=1`. At boot the production default was:

```text
[FONT_PROVIDER] requested=Bitmap initialized=Bitmap active=Bitmap used=not-yet fallback=none generation=1
[FONT_PROVIDER] used=Bitmap drawCalls=6 generation=1
```

The first TrueType selection loaded `Fonts/roboto/Roboto-Regular.ttf` (146,004 bytes) and reported:

```text
[FONT_PROVIDER] requested=TrueType initialized=TrueType active=TrueType used=not-yet fallback=none generation=2
[FONT_PROVIDER] used=TrueType drawCalls=8 rasterizations=30 cacheHits=121 cacheMisses=30 cacheEntries=30 cacheBytes=4070 generation=2
```

This confirms actual TrueType provider drawing, glyph rasterization, and cache reuse. It is stronger than the selection flag alone. Returning to Bitmap produced generation 3 with `requested=Bitmap initialized=Bitmap active=Bitmap`, `fallback=none`, and `used=Bitmap drawCalls=8`. A second switch cycle reached TrueType generation 4 (`rasterizations=30`, `cacheHits=121`, `cacheMisses=30`, `cacheEntries=30`, `cacheBytes=4065`) and Bitmap generation 5 with `used=Bitmap drawCalls=8`. No reboot was used between provider changes.

The guest remained responsive through a 7,200-frame heartbeat sample. At final serial sampling, allocator invalid frees and corrupt frees were zero, the thread pool was unlocked, and `INPUT_STATS_MOUSE_DROPPED=0`. QMP mouse input opened the production menus and settings page. These counters separate the failed app-launch attempt below from dropped guest input.

## Surface coverage and visual comparison

All screenshots are 1280×800 QMP captures stored under `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS6R1\evidence`.

| Surface | Guest evidence | Result |
| --- | --- | --- |
| Desktop tile labels | `bitmap-desktop-baseline.png`, `truetype-desktop.png`, `bitmap-return-desktop.png`, `bitmap-repeat-return-desktop.png` | Bitmap labels show wide spacing; TrueType labels are compact and proportionally spaced. Returning to Bitmap restores the spaced labels without stale TrueType text. |
| Window title bar | `bitmap-font-settings.png`, `truetype-font-settings.png`, `truetype-shutdown-dialog.png` | Display Options and Confirm Shutdown titles render with the selected provider; the TrueType forms are visibly proportionally spaced. |
| Taskbar application label | `truetype-font-settings.png` | The Display Options taskbar button uses compact TrueType text but is truncated to `Display Op...` within its fixed button width. |
| Clock and date | Desktop screenshots above | Clock and date remain fully visible at 1280×800 in both modes. |
| Start Menu | `bitmap-start-menu.png`, `truetype-start-menu-clear.png`, `bitmap-return-start-menu.png`, `bitmap-repeat-return-start-menu.png` | TrueType improves spacing/readability. Fixed-width entries still truncate, including forms such as `Computer...`, `Disk Mana...`, and `Recent Do...`; bottom buttons remain within their existing bounds with shortened labels. Bitmap spacing returns after both switch cycles. |
| All Programs list | `bitmap-all-programs.png` | Opened in Bitmap mode; app entries were visible, with narrow labels truncated. TrueType All Programs was not separately opened. |
| Desktop context menu | `bitmap-desktop-context-menu.png`, `truetype-desktop-context-menu.png` | Text remains widely spaced after TrueType is active. `guideXOS\GUI\RightMenu.cs` calls `WindowManager.font.DrawString` for these labels rather than the selected shared-text provider, matching the observed legacy bitmap rendering. |
| Taskbar context menu | `truetype-taskbar-context-menu-visible.png` | `Task Manager` is rendered with compact TrueType spacing. Selecting it closed the menu, but no Task Manager window appeared during the observation window. Record as an app navigation/launch gap, not a font rendering failure. |
| Available dialog | `truetype-shutdown-dialog.png`, `truetype-shutdown-cancelled.png` | The Confirm Shutdown text and buttons rendered in TrueType. **No** was selected; the guest stayed running. |
| MessageBox / Save Changes | — | Not visited. |
| Notification | — | No notification appeared during provider selection; no production notification was triggered in this run. |

The Display Options window opened at the desktop context-menu position extends below the visible desktop area in the captured layout. Its title and Fonts controls remained accessible, so it did not prevent the provider test. The menu and taskbar bounds were not changed during this retry. Start Menu truncation and the desktop context-menu provider mismatch are recorded for follow-up rather than hidden as passing coverage.

## Acceptance result and follow-up

The required guest gates passed: authentic UEFI boot, Bitmap default, production TrueType selection, TrueType drawing with nonzero rasterization and cache use, and two live returns to Bitmap with visible redraw. **Outcome A — `GUEST_TRUETYPE_ACCEPTED`.** The report does not claim unvisited MessageBox, Save Changes, or notification paths as passed.

FONTS7 follow-up should:

1. Route desktop context-menu text through the selected provider and verify it in the guest.
2. Revisit fixed-width Start Menu and taskbar label truncation, with control geometry kept separate from font selection behavior.
3. Exercise MessageBox and Save Changes dialogs and identify a real notification production path before claiming those surfaces.
4. Trace the Task Manager menu selection path; the guest accepted the click without dropped mouse input but did not show the application window in this run.

No push or merge was performed. This acceptance change is documentation-only.
