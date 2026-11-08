# FONTS6 Production TrueType Activation

**Outcome B — production integration, hosted verification, and the authentic image build passed; guest TrueType rendering remains unverified.** The production UI now has a session-only Bitmap/TrueType choice and records provider selection and usage. No FONTS6 guest was started because a live QAC reservation could not be obtained.

- Repository: `D:\dev\guideXOSUEFI_FONT_SUPPORT`
- Branch: `FONT_SUPPORT`
- Starting revision: `d5e6c92bb0c99608fc319ec62a87d1d04b7afc43`
FONTS6 implementation commit before this report: `644ce09` (`feat(fonts): add opt-in production TrueType rendering`)

## 1. Root cause of the missing production caller

FONTS2–FONTS5 supplied `SharedTextFont`, its existing validated TrueType selector, and provider-aware desktop and shell drawing helpers. Production initialized the shared surfaces with the bitmap atlas, but no production UI or desktop initialization path called the TrueType selector. Hosted fixtures could opt in, while the actual desktop could not.

FONTS6 adds the missing call path in Display Options and makes the selection visible to the shared desktop and shell renderers. It does not add a font engine, font registry, or persistence format.

## 2. Selection architecture and changes

- `DisplayOptions` offers **Bitmap** and **TrueType** buttons. Bitmap remains the initial and failure fallback provider. The choice lasts for the current session; it is not saved across reboot.
- `WindowManager` owns one `SharedTextFontSelection`. The TrueType choice reads `Fonts/roboto/Roboto-Regular.ttf` and initializes the existing renderer at 20 px. If reading or initialization fails, Bitmap remains active and the selection records the failure reason.
- Each changed selection advances the font generation. Start Menu notices the generation and invalidates its frame cache. Other surfaces resolve the selected provider while drawing. Desktop tile layout and hit geometry continue to use bitmap metrics so a provider change does not move controls or change hitboxes.
- Selection diagnostics report requested, initialized, active, and actually used providers, fallback reason, and generation. After real shell drawing, an aggregate usage line reports draw calls; for TrueType it also reports rasterizations, cache hits/misses, entries, and bytes. It is emitted at most once per generation and is suppressed for TrueType until rasterization and cache reuse have occurred.

## 3. Desktop surfaces reached by the production code

The shared provider is used by desktop tile labels, window title bars, Start Menu labels, taskbar labels, clock/date, and taskbar context-menu text. MessageBox, Shutdown, and Save Changes drawing paths also query the selected provider. Notification text and layout are provider-aware in `NotificationManager`, but a source search found no production caller of `NotificationManager.Update()`; a real notification presentation path therefore remains unconfirmed.

The Settings button labels themselves remain on the bitmap font, and the desktop tile geometry remains bitmap-based. Hosted checks prove selection and rendering logic; they do not prove that any of these UEFI surfaces rendered through TrueType in a guest.

## 4. Hosted regression results

| Suite | Result |
| --- | ---: |
| FONTS1 runtime | 234 checks, 0 failures |
| FONTS2 shared interface | 73 checks, 0 failures |
| FONTS3 shell integration | 111 checks, 0 failures |
| FONTS4 dialogs and notifications | 28 checks, 0 failures |
| FONTS6 production selection | 13 checks, 0 failures |
| **Font checks total** | **459 checks, 0 failures** |
| Desktop interaction suite | 42 assertions passed |
| Task Manager/focus and notification boot-reset regressions | Passed |

The FONTS1 rendered-pixel golden is unchanged at `0xEC1A69C86C9C5CC6`. The added FONTS6 checks cover production selection, successful TrueType initialization, rasterization and cache reuse, fallback, repeated switching, stable desktop hit geometry, redraw generations, width clipping, and well-formed UTF-16 boundaries. The detailed hosted output is preserved at `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS6\font-harness-final.log`.

## 5. Authentic build status

Phase31 through Phase35 payload staging completed with all 27 static payload validations passing. Existing Phase31–35 ramdisk artifacts were copied to timestamped preservation folders before replacement; no prior evidence was moved or deleted. The final production pipeline then ran as `build.ps1 -SkipBootloader -SkipManagedProofs`: proof payloads were built/staged separately, then the kernel, RAM disk, managed-artifact audit, and ESP were produced. The existing bootloader was reused.

The kernel NativeAOT build succeeded with no errors. The RDSK parser reported 595 files. Managed artifact audit and identity checks agreed 41/41 across descriptors, build outputs, staging files, RAM disk entries, and the compiled kernel allowlist. The generated identity source and audit are in the isolated build worktree.

| ESP artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `EFI/BOOT/BOOTX64.EFI` | 49,664 | `89EF110B0F317CA77BCDC91DC8F90840AC47AAA8AD14D9905B3D8910F3C1F593` |
| `kernel.elf` | 4,763,648 | `B30BDE8AB949E111D5961512B91C96F78D5815691139D9663CC5132C2A41E4DB` |
| `ramdisk.img` | 57,053,414 | `C019F7783D454BD357F7D256141EAE85E07BA85C6AD9A38F4DEBFF3C6DF8A841` |
| Generated managed identity source | 23,204 | `A2D4477ACA82AC76E06C28877464AD7484CE5E167A702853D873AB812954FA84` |

The completed log is `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS6\build-full-final-retry1.log`; the 41-row audit CSV is `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS6\build\out\h2c-managed-artifact-audit.csv`. The first kernel attempt exposed stale font-cache reset names and a missing input namespace import; both source issues were corrected, and the retry produced the complete image. The successful build retains existing warnings, including the net7.0 support warning, RID behavior warning, `Microsoft.DotNet.ILCompiler` 9.0.0 NU1903 advisory, and existing compiler/analyzer warnings. There were no errors in the final build.

## 6. Guest evidence and screenshots

No FONTS6 guest boot, input, or screenshots were produced. The available FONTS5 QAC screenshots are a bitmap-only historical baseline, not evidence for FONTS6 TrueType activation. They remain in `D:\dev\guideXOSUEFI_FONT_SUPPORT_FONTS5_BUILD\out\FONTS5-guest\evidence\` and are referenced in `docs/FONTS5_BUILD_GUEST_QUALIFICATION.md`.

The UEFI desktop's TrueType renderer counters were therefore not observed in guest serial output. The production diagnostics are ready to record them, but a selector state, hosted pixel output, packaged TTF, or successful image build does not establish that the guest rendered TrueType glyphs. Dialog and notification guest reachability is also not established.

## 7. Bitmap-versus-TrueType comparison

Hosted pixel fixtures produced visibly distinct provider output:

| Surface fixture | Bitmap hash | TrueType hash |
| --- | --- | --- |
| Desktop label | `0x516CF4DDDD592FA5` | `0xCA072B25DA72092E` |
| Notification text | `0xD6B530CE181F1CF4` | `0xF0BC7E0EFCD0350D` |
| Dialog text | `0xD6B530CE181F1CF4` | `0xECA211AF908DE598` |

These hashes show that the hosted fixtures draw different pixels for the two providers. They are not guest screenshots or proof of production UEFI execution. The FONTS1 compatibility golden remains unchanged. The taskbar clock/date hosted golden changed from `0x77A6611F5B2E6544` to `0x4F3982E609B742C1` after correcting date placement against the taskbar's right edge; that difference was investigated rather than silently accepted.

## 8. Remaining clipping and typography issues

- Start Menu truncation now measures the full selected-provider string before choosing a bounded label, clips within its row, and preserves UTF-16 surrogate pairs.
- Taskbar date placement now uses the right-edge layout path; tile hit geometry remains tied to bitmap metrics.
- The bitmap atlas still shows unusually wide letter spacing in the historical guest screenshots. This was not changed because no FONTS6 guest comparison was available to distinguish atlas metrics from placement or scaling effects.
- FONTS6 has hosted clipping and dialog-bound tests, but no guest validation of title bars, narrow menu labels, taskbar bounds, or dialogs. The notification update path also still lacks a found production caller.

## 9. QAC reservation and cleanup evidence

No live QAC coordinator/reservation interface was available in this session, so no FONTS6 reservation could be obtained. The machine check found zero local `qemu-system-x86_64` processes; no QEMU process was started. Consequently there is no FONTS6 reservation ID, QAC release record, guest serial log, or guest screenshot to report. The prior FONTS5 reservation and cleanup records document only the historical FONTS5 run and do not authorize a FONTS6 guest run.

## 10. FONTS7 recommendations

1. Obtain an exclusive QAC reservation, boot the exact FONTS6 ESP, and capture screenshots plus serial logs first with Bitmap and then after selecting TrueType.
2. Require `[FONT_PROVIDER] requested=TrueType`, `initialized=TrueType`, `active=TrueType`, `used=TrueType`, and nonzero rasterization/cache-hit counters from the desktop render loop before accepting guest activation.
3. Exercise desktop labels, window title bars, Start Menu, taskbar labels, clock/date, taskbar context menu, MessageBox, Shutdown, and Save Changes in both modes. Record any unavailable surface and the exact access limitation.
4. Trace and exercise a real notification production path; add a hosted and guest regression proving its redraw and bounds after provider switching.
5. Compare the bitmap spacing and remaining text bounds in the guest at the same resolution. Change atlas metrics or control bounds only when a reproducible guest comparison identifies the cause.
6. Keep Bitmap as the default and verify the missing/corrupt-font fallback in the guest. Consider persistence only if an existing supported configuration path fits without adding a new persistence layer.

No push, merge, or changes to other UEFI branches were made.
