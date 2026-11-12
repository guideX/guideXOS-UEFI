# FONTS8R1 Guest Completion and Integration Readiness

**Outcome B — partial guest acceptance.** QAC admitted an exclusive acceptance run and the authentic FONTS8 UEFI candidate booted. Actual guest pixels verified the Bitmap default, TrueType drawing at 20 px and 24 px, the 24 px desktop and context-menu surfaces, the Start Menu at both target sizes, the taskbar application label, the Save Changes and Shutdown dialogs, and a return to Bitmap. MessageBox and notification rendering were not verified; installed-system persistence remains open. The branch is unchanged except for this report.

## Gate summary

| Gate | Result | Evidence and remaining work |
|---|---|---|
| Typography | **Partial pass** | Screenshots and serial diagnostics confirm actual Bitmap and TrueType drawing. Desktop and taskbar labels stay within their layout bounds; some long labels use ellipses at 24 px, including the taskbar app label and Save Changes “Don't Save” button. Capture confirms the return to Bitmap. See the guest qualification section. |
| UI coverage | **Partial; MessageBox and notifications unverified** | Save Changes and Shutdown dialogs rendered and their safe response buttons worked. Calculator `2 + 3 =` and Visual Effects **Apply** exercised production notification producers, but no toast appearance was captured. The legacy MessageBox path was not reachable from the active UEFI app catalog. |
| Installed persistence | **Unverified; open** | No disposable writable UEFI-installed system or established safe UEFI installation workflow was available. LiveMode preferences remain session-only; no persistence was inferred from this run. |
| Integration | **Assessment complete; defer signoff** | The integration difference and the shared `Kernel/Kernel.projitems` overlap are documented below. Complete the guest UI gates and reconcile that manifest at integration time. |

## Preflight and candidate identity

- Repository: `D:\dev\guideXOSUEFI_FONT_SUPPORT`
- Branch and starting HEAD: `FONT_SUPPORT`, `6ef0379f9dc1ce323ee39d24d960b383a114aa2b`
- Protected mainline reference: `origin/main` at `a239589403d277adf8d089d714dbfc4e450e0c76`; it is an ancestor of this branch.
- The starting worktree was clean except for the pre-existing untracked `Tests/UefiDesktopInteractionTests/bin/` and `obj/` directories. They remain untouched. No production source, generated identity, packaged font asset, or previous acceptance artifact was changed.
- The existing FONTS8 completion report and hosted/build evidence, plus the FONTS7R1 guest report and screenshots, were reviewed. FONTS7R1 screenshots are historical context only; the FONTS8R1 guest screenshots below are the evidence for this run.

The detached FONTS8 build worktree is `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS8\build\candidate-source`, at `7af27ae89aac71df8b626418b208093412689f02`. The recorded `fonts8-production.patch` SHA-256 is `0FAE7500B727CC63B947AA5C8DC8C1E8D16DF291DE1BA86ACC80B6837AB4D547`; the artifact copy has that hash. I compared the 10 modified tracked source files and the added hosted-test stub against the current workspace after normalizing line endings; all 11 contents match. The 41-row managed artifact audit was re-read: all rows agree for build-to-stage, descriptor-to-executable, allowlist-to-executable, and ramdisk-to-source checks.

The isolated guest ESP payloads matched the accepted FONTS8 artifact manifest both before and after the run:

| ESP payload | Bytes | SHA-256 | Check |
|---|---:|---|---|
| `EFI/BOOT/BOOTX64.EFI` | 49,664 | `89EF110B0F317CA77BCDC91DC8F90840AC47AAA8AD14D9905B3D8910F3C1F593` | Match; reused FONTS7R1 loader |
| `kernel.elf` | 4,767,744 | `CB1E49F3CE3128F3692C2243E7976D94C705D705E337CA85E436A46EEC529553` | Match; root-build and ESP copies agree |
| `ramdisk.img` | 57,053,414 | `C019F7783D454BD357F7D256141AEE85E07BA85C6AD9A38F4DEBFF3C6DF8A841` | Match; root-build and ESP copies agree |

The post-run file-level hashes are in `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS8R1\evidence\post-run-esp-integrity.csv`. The complete preflight captures remain in that evidence directory.

## QAC admission and run closure

QAC 2.0.0 granted reservation `6bbdeab08f484f3dadff8d334ba71320` for project `guideXOSUEFI-FONTS8R1`, mode `acceptance`. The verified owner was `LEON2_RDT\guideX` PID 13436. Its direct child was `C:\Program Files\qemu\qemu-system-x86_64.exe`, PID 32312. The run used the accepted firmware code and variables template, machine `pc-q35-8.2`, 1024 MiB, loopback QMP, and the isolated ESP copy under `build\guest-run-001`; no standalone or unrelated QEMU process was launched or stopped.

QAC briefly reported process identity unavailable immediately after launch. A subsequent inspect returned `degraded=false`, the owner was verified, and QAC listed one managed process and zero foreign QEMU processes before the guest interactions. The QMP `quit` command closed the run at 2026-11-11 20:06:50 Pacific. QAC recorded QEMU exit code **0**, confirmed reservation release, and returned run exit code **0**. The final QAC status reported zero QEMU processes, zero reservations, zero managed sessions, and an empty queue; the independent local process inventory also found no `qemu-system-x86_64` process. Captures are in `evidence\qac-pre-exit-*.txt`, `evidence\qac-run-exit-release.txt`, and `evidence\qac-final-census.txt`.

## Guest typography qualification

The candidate booted through the authenticated UEFI path. The first provider diagnostic was `requested=Bitmap initialized=Bitmap active=Bitmap`, followed by actual Bitmap draw calls. The guest then switched to TrueType at 20 px and 24 px. Serial diagnostics report TrueType drawing, rasterizations, and cache hits at the selected generations; the screenshots show actual rendered glyphs in the UI rather than relying on the selected-provider flag alone. Mouse input diagnostics show no dropped mouse packets; keyboard diagnostics show no dropped key events. Allocator health counters remained at zero invalid frees and zero corruption reports in the retained heartbeat records.

Verified surfaces and observations:

All screenshots cited here and the serial/QMP logs are under `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS8R1\build\guest-run-001\`. Captures show containment and the deliberate ellipsis behavior; the guest run did not emit per-string advance measurements. Numeric text-fit conclusions therefore rely on the retained hosted checks plus visual pixel review.

- **Bitmap default and provider switching:** `bitmap-default.png` shows the initial desktop. `display-options-fonts-bitmap.png` records the Bitmap selection. TrueType was selected at 20 px and 24 px through the guest controls. After returning to Bitmap at 20 px, `bitmap-return-20.png` shows the active Bitmap provider and freshly rendered Bitmap desktop labels; no stale TrueType text was visible on the captured surface.
- **24 px desktop and menus:** `desktop-truetype-24.png`, `desktop-context-truetype-24.png`, and `taskbar-context-truetype-24-retry.png` show desktop labels, the desktop context menu, and the taskbar context menu. The taskbar context menu's `Task Manager` item is fully visible when opened at the bar's top edge. A right-click lower inside the bar placed most of that popup behind the bar; moving the test to the top edge exposed the whole menu, matching the menu's cursor-relative placement rule.
- **Taskbar application label:** `notepad-24.png` shows the real Notepad taskbar entry at 24 px. `Notep...` is ellipsized inside the fixed-width button and does not spill into adjacent controls. `taskbar-app-context-24-retry.png` shows the taskbar context menu above the active taskbar entry.
- **Start Menu at 20 px and 24 px:** `start-menu-20.png` and `start-menu-24.png` show real application labels at both sizes. Longer fixed-width entries, such as `Computer...`, `Disk Manag...`, and `Shutdown...`, are ellipsized within their slots; the captured text does not overlap adjacent entries. `start-menu-allprograms-24.png` also verifies the All Programs hitbox and its application list.
- **Shutdown dialog:** `shutdown-dialog-truetype-20.png` shows the title, question, and `No`/`Yes` buttons rendered at 20 px. Clicking **No** dismissed the dialog without shutting down the guest (`shutdown-no-result.png`).
- **Save Changes dialog:** a harmless one-character unsaved Notepad edit reached the production Save Changes confirmation. `save-changes-truetype-24.png` shows the title, body, and Save/Don't Save/Cancel controls at 24 px. The `Don't Save` label is ellipsized as `Don't...` inside its button. Clicking **Cancel** kept Notepad open (`save-changes-cancel-result.png`); reopening and choosing **Don't Save** closed it without saving (`notepad-discard-result.png`).
- **Input and rendering diagnostics:** `guest-serial.log` contains boot, provider-generation, cache, input, taskbar, and heartbeat records. `qmp-interactions.log` records the UI actions and screendumps. `guest-screenshot-hashes.csv` records byte lengths and SHA-256 hashes for the key screenshots.

The fixed-width app and dialog labels are contained by the updated layout, but the ellipsized Start Menu entries, taskbar label, and `Don't Save` button are partial text presentations. Treat those as visible constraints at 24 px, not as proof that the complete labels fit. No source edit was made because this assessment did not establish a single confirmed FONTS8 defect with a safe, isolated correction; these truncated labels remain follow-up review points if full-label display is a release requirement.

## MessageBox and notification qualification

The production notification paths were triggered through two real UI actions: Calculator `2 + 3 =` and Visual Effects **Apply**. The arithmetic result `5` is visible in `calculator-addition-notification-20.png`, and the Apply action returned to Display Options. The resulting toast was not visible in the retained captures. Notification appearance, selected-provider glyph drawing, timed dismissal, message wrapping/clipping, and provider-switch stale-render behavior are therefore **unverified**, not passed. No fake notification was injected and no production behavior was changed to force one to appear.

The production `System.Windows.Forms.MessageBox.Show` call sites found in `guideXOS\DefaultApps\VirtualDiskManager.cs` and `guideXOS\DefaultApps\AutoMountConfig.cs` belong to legacy app classes that are not exposed in the active UEFI app catalog (`guideXOS\OS\App.cs` and `guideXOS\OS\ApplicationFactories.cs`). The current Start Menu exposes the modern Disk Manager instead. I also opened Computer Files and selected a packaged `.ttf`; that modern window follows its application Shell-service path (`guideXOS\DefaultApps\ComputerFiles.cs`) and did not reach `Desktop.ShowOpenError` or display a MessageBox. The Notepad Save Changes surface is an application confirmation dialog, not the MessageBox implementation. There was no supported, ordinary UI action in this guest to exercise the production MessageBox renderer, so that surface remains **unverified**.

Because neither a notification toast nor the production MessageBox was visually verified, the combined UI guest gate does not pass even though Save Changes and Shutdown dialogs did render and respond.

## Installed-system persistence

No disk image for a disposable writable UEFI-installed guest was present in the repository or the FONTS7R1/FONTS8 artifact areas. The FONTS7R1 report also records that the established HDInstaller uses an MBR/GRUB layout and does not provide a ready UEFI-installed guest workflow. The authenticated guest was LiveMode, where the font panel states selections are session-only and Save Settings is unavailable. The installed persistence sequence was not attempted: select TrueType at 18 px, invoke the real Save Settings action, reboot normally, verify provider and size restoration, and confirm actual TrueType rendering after reboot. This gate remains open; no host disk was modified and no persistence subsystem was added.

## Hosted and build verification

No production source changed, so the accepted FONTS8 baseline was retained rather than rerun:

- 634 hosted font checks passing.
- 42 desktop interaction assertions passing.
- Task Manager/focus regression passing.
- Notification reset regression passing.
- FONTS1 pixel hash `0xEC1A69C86C9C5CC6`.
- Authentic kernel, ramdisk, and ESP build passing.
- Managed artifact agreement 41/41.

The build report records `NETSDK1138` for `net7.0` and `NETSDK1201` for RuntimeIdentifier/self-contained publish behavior. .NET 7 reached end of support on May 15, 2024; Microsoft's [lifecycle table](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core) lists .NET 10 LTS support through November 15, 2028. Track the target-framework/security-update question and make self-contained publish behavior explicit in release engineering separately from this font change. No CVE-specific advisory was identified in the reviewed build evidence, and this review did not run a dependency vulnerability scan.

## Integration-readiness assessment

`FONT_SUPPORT` is 15 commits ahead of protected `origin/main` (53 changed paths). The principal font-specific work is:

- FONTS1 runtime foundation: `915ee31`, `af0592d` — bounded TrueType parser/rasterizer in `guideXOS/Kernel/Misc/TrueTypeFont.cs`, `Kernel/Misc/IFont.cs`, shared font rendering, and hosted coverage.
- Shared shell/dialog integration: `82f814c`, `2c29f32`, `644ce09`, `22d5b16` — `SharedTextFont`, `SharedTextLayout`, provider selection, and changes across desktop, Start Menu, taskbar, windows, MessageBox, notifications, and fixed dialogs.
- Settings and shell consistency: `9b91b33`, `7af27ae` — Bitmap-default preference handling, configuration integration, provider-measured layout, and repairs for FONTS7R1 defects.
- FONTS8 completion: `6ef0379` — fixed-label fitting, MessageBox sizing/wrapping and display path, notification update/reflow, and focused hosted regression coverage.

Keep the font changes with their build and packaging dependencies: `Kernel/Kernel.projitems`, `guideXOS/guideXOS.csproj`, `build.ps1`, and the Phase26/Phase31–35 build, staging, and verification scripts. They include shared-font sources and preserve the authentic image and managed-artifact audit path. The branch adds no NuGet dependency or font asset. Roboto TTF assets are already present in the RAM-disk source trees and must remain packaged; Bitmap remains the default. No separate release override was identified.

The current `origin/APP_MODEL` and `origin/MODERN_INPUT` branches share no changed path with `FONT_SUPPORT` other than `Kernel/Kernel.projitems`. APP_MODEL adds filesystem/application-model compile includes there, MODERN_INPUT adds xHCI discovery, and FONT_SUPPORT adds the shared-font includes. A future integration must retain all three sets and recheck the then-current branch heads.

The FONTS8R1 guest run did not reproduce a new confirmed typography defect requiring a production change. It did leave actual notification and MessageBox rendering open, and it showed intentional ellipses on several long fixed-width labels at 24 px. No additional release configuration or asset change was identified. Font preferences still use the existing installed config path and default safely to Bitmap when values are absent or invalid.

**Recommendation:** keep `FONT_SUPPORT` local and defer integration signoff until the notification and MessageBox guest surfaces are either verified through legitimate UI paths or recorded as explicit release exceptions, the 24 px ellipsis requirements are accepted, and installed-system persistence is qualified separately. At integration, reconcile all three sets of `Kernel/Kernel.projitems` includes and retain the build/packaging support. No merge, rebase, cherry-pick, or push was performed.
