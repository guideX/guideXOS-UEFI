# FONTS7 — Font Settings and Shell Consistency

**Repository:** `D:\dev\guideXOSUEFI_FONT_SUPPORT`
**Branch:** `FONT_SUPPORT`
**Starting commit:** `21d8018e333b78e9baac26ddd5250a63798dff99`
**Date:** 2026-11-08

## 1. Font configuration architecture

The bitmap atlas remains the production default. The Settings font panel now
selects either Bitmap or the packaged Roboto TrueType font, displays the active
provider and size, applies provider changes immediately, and can return to
Bitmap without restarting. A TrueType initialization failure leaves Bitmap
active and reports the fallback.

TrueType uses the existing `SharedTextFont` provider and cache. The desktop
Settings panel exposes 12, 14, 16, 18, 20, and 24 pixel sizes; the shared
renderer also validates its existing 32 pixel capability, but the panel does
not offer it. A selected TrueType size is remembered while Bitmap is active and
applied when TrueType is selected.

Startup loads preferences before `WindowManager.Initialize()`. The validated
preference is therefore applied after `InitializeFont()` and the Bitmap
provider selector are ready. This preserves the order required to restore a
saved TrueType selection without attempting to initialize it before the shell
font exists.

## 2. Persistence decision

The existing installed-system configuration file is `/etc/guidexos/config.ini`.
Font settings use its existing key/value format:

```ini
font_provider=bitmap
font_size=20
```

`font_provider` accepts `bitmap` or `truetype`; `font_size` accepts only a
supported shared-renderer size. Older files without these keys continue to
load with Bitmap and the default TrueType size of 20. Invalid or incomplete
settings normalize to Bitmap and a supported size. If the packaged font cannot
be read or initialized at startup, the active provider falls back to Bitmap.

Selections apply immediately. In installed mode, the existing desktop **Save
Settings** action persists them. LiveMode continues to skip configuration
loading and saving, so its selections are session-only. No new file format or
UEFI NVRAM write was added.

## 3. Shell and context-menu findings

FONTS6R1 evidence showed that TrueType could be selected and rasterized, but
the desktop context menu still had Bitmap-style spacing. Its labels, submenu
arrow, and marker drew directly through `WindowManager.font`, bypassing the
shared provider. The correction sends those strings through the shared text
surface renderer. Existing row height, menu width calculation, hit regions,
and click handling remain fixed; TrueType labels are clipped to the existing
row bounds.

MessageBox width measurement also used the Bitmap provider while its body used
the selected provider. It now measures with the selected provider and clamps
TrueType width to the display. Taskbar, Start Menu, clock, and window-title
surfaces already used the shared shell text path; hosted provider checks cover
their existing integration, so their layout code did not need changes.

## 4. Text sizing and dialog coverage

Hosted layout checks exercise 14, 18, and 24 pixel text through context-menu
rows, MessageBox, Shutdown, Save Changes, and notifications. They check shared
metrics, clipping, and size propagation while preserving fixed button
dimensions. Existing shared-font behavior continues to use font ascent and
descent for baseline placement and the glyph cache for repeated draws.

Fixed-width shell layouts do not grow automatically with the chosen font size.
Long strings can still clip or truncate in existing rows and controls; the
change keeps clipping deterministic and does not change button or menu hitbox
geometry. Proportional glyphs do not remove those layout limits.

## 5. Hosted regressions

The final hosted font run passed **566 checks** with zero failures:

- FONTS1: 308 checks; golden pixel hash remains `0xEC1A69C86C9C5CC6`.
- FONTS2: 89 checks.
- FONTS3 shell text: 111 checks.
- FONTS4 layout and UI: 34 checks.
- Production provider and dialog/notification checks: 15 checks.
- Font preference configuration and runtime restoration checks: 9 checks.

The UEFI desktop interaction suite passed **42 assertions**. Task Manager
memory/focus and notification boot-reset regressions also passed. Logs are
stored under `E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS7`.

## 6. Authentic build and artifact audit

**Status:** Full source build passed in an isolated detached worktree at
`E:\CodexArtifacts\guideXOSUEFI_FONT_SUPPORT\FONTS7\build\source`, based on
the starting commit plus the FONTS7 production changes. The pinned .NET SDK
10.0.401 is used. `build.ps1 -SkipBootloader` rebuilds managed proof artifacts,
generates kernel identities, publishes the FONTS7 kernel, converts it, builds
the ramdisk, and runs the managed artifact audit. The unchanged bootloader is
seeded from FONTS6 and hash-checked (`89EF110B0F317CA77BCDC91DC8F90840AC47AAA8AD14D9905B3D8910F3C1F593`).

The build exited 0. The 4.54 MiB `kernel.elf` SHA-256 is
`51A4BE4443652320349B23A381CEC8A7F936B442C1297EA8AEAF548E7CC1D8FF`; the
57,053,414-byte ramdisk SHA-256 is
`C019F7783D454BD357F7D256141EAE85E07BA85C6AD9A38F4DEBFF3C6DF8A841`. The
final audit contains 41 managed artifacts, with 41/41 agreement for descriptor
hashes, staged build outputs, ramdisk contents, and compiled allowlist.

The inherited `Microsoft.DotNet.ILCompiler` 9.0.0 dependency emitted NuGet
warning NU1903 with advisory `GHSA-p3jp-9v83-76w9`; it did not fail the build.
Build output and the audit CSV are under the FONTS7 artifact directory; prior
FONTS6 evidence was not modified.

## 7. Guest evidence

No FONTS7 guest test was run. This environment exposes no QAC reservation or
guest-runner tool, and no standalone QEMU instance was started. The reviewed
FONTS6R1 serial report showed TrueType activation/rasterization and two returns
to Bitmap. Its screenshots showed the old Bitmap-spaced context menu and old
font Settings panel; they are prior-iteration evidence, not FONTS7 acceptance.
Guest coverage remains outstanding for provider selection, two TrueType
sizes, Bitmap restoration, saved-setting reboot, and visual geometry across
the changed and already-shared shell surfaces.

## 8. Separate Task Manager observation

FONTS6R1 recorded that clicking the Task Manager context-menu item closed the
menu but did not open the Task Manager window. This remains a separate guest
interaction observation. No Task Manager activation or input-handling code was
changed because no typography regression was demonstrated.

## 9. Remaining layout limitations

The active provider is shared across migrated shell surfaces, but their
existing widths and row heights remain fixed. Larger sizes can reduce visible
text in narrow controls. This change does not add Unicode shaping, auto-growing
menus, or desktop-wide reflow.

## 10. Recommended FONTS8 frontier

Use an authorized QAC reservation to exercise installed Settings persistence
across reboot, LiveMode session-only behavior, 14 and 24 pixel TrueType text,
Bitmap restoration, missing-font fallback, and narrow display clipping. Capture
provider activation, actual glyph rendering, Settings behavior, and unchanged
hitbox geometry separately for the context menu, taskbar, Start Menu,
window-title, MessageBox, Shutdown, Save Changes, and notification surfaces.
Keep the Task Manager activation observation isolated unless a separate
reproducible input issue is scheduled.
