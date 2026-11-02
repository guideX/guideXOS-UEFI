# FONTS4: Dialogs, Notifications, and Provider-Aware Text Layout

## Outcome

**Outcome B — hosted text-layout and UI-surface acceptance passed; source-project compilation, full build, and guest qualification remain blocked.** Bitmap remains the production default. TrueType is still an explicit opt-in through the existing shared selector.

## 1. Prior implementation inventory

- **FONTS1** added the bounded TrueType parser, rasterizer, provider metrics, Unicode replacement-glyph fallback, and the existing glyph coverage cache (64 entries / 64 KiB maximum). Its established 12/20/32 px render hash is `0xEC1A69C86C9C5CC6`.
- **FONTS2** added `SharedTextFont`, which holds the existing bitmap or TrueType provider and routes measurement and drawing through that provider. It does not own font bytes or a glyph cache. Production selects the bitmap atlas by default.
- **FONTS3** routed window title bars, taskbar text, and Start Menu text through the shared provider. It retained fixed control geometry and added bounded clipping for those surfaces. Its four bitmap shell hashes and the FONTS1 hash remain unchanged.
- Before FONTS4, notifications measured one bitmap-font line and drew it without a caller-owned text clip. `MessageBox`, `ShutdownDialog`, and `SaveChangesDialog` drew bodies and button labels directly with the bitmap atlas. Window titles already used the FONTS3 shared title renderer.

The prior FONTS1–FONTS3 source and hosted checks remain in the repository and continue to run from `Tests/TrueTypeRuntimeFoundation`.

## 2. Text-layout architecture

`Kernel/Misc/SharedTextLayout.cs` adds a reusable layout object over `SharedTextFont`:

- It reads provider glyph advances. For bitmap atlases, it also accounts for visible cell width so the layout matches the atlas measurement contract when padding changes the pen advance.
- It wraps at Unicode whitespace when possible, keeps long words moving by splitting only between Unicode scalar values, and recognizes LF, CR, and CRLF paragraph breaks.
- Line height comes from the selected provider metrics. The caller may add 0–64 px between lines. Each line is drawn from its own top-of-line origin, retaining the provider's ascent and baseline behavior.
- The fixed line buffers hold at most 128 lines. Layout scans at most 4,096 Unicode scalars and then caps wrapping work at 16,384 steps. It allocates no per-line strings and adds no persistent font or glyph cache.
- A zero-width layout produces no drawable lines. An overwide single scalar remains intact and is clipped by the supplied rectangle. Empty text has one empty line box when the width is positive.
- Text beyond the scalar or line limit is marked truncated. Drawing then clips the available lines to the supplied half-open rectangle; controls and dialogs do not expand to fit.

Range drawing was added to the existing bitmap and TrueType renderers so a wrapped line can be drawn without allocating a substring. Measurement and drawing continue to use the same provider instance and glyph semantics.

## 3. Integrated UI surfaces

- **Notifications:** `NotificationManager` now lays out its message body using the selected shared provider. The notification model has no title field, so there is no separate title to migrate. The body is clipped to the toast bounds; long messages wrap inside a width capped to the display. For a one-line bitmap notification, the old bitmap width, pixels, slide distance, color, border, and timer remain unchanged. The original animation travel width is kept separately from the bounded visible width, so a long wrapped toast retains its prior right-edge motion and timed dismissal.
- **MessageBox:** the existing window title continues through the shared FONTS3 title renderer. TrueType message text wraps and clips inside the existing window bounds. Its current bitmap-derived width and window geometry remain unchanged.
- **Shutdown confirmation:** the title continues through the shared window title renderer. TrueType body text wraps in the existing content area above the fixed Yes/No buttons. TrueType button labels are centered and clipped to those button rectangles. The bitmap drawing calls and input code remain as before.
- **Save Changes confirmation:** the title continues through the shared window title renderer. TrueType descriptive text wraps above the fixed Save / Don’t Save / Cancel buttons. TrueType button labels clip to their existing rectangles. Bitmap rendering and click handling remain unchanged.

The changes do not resize button hit regions, move dialog buttons, alter focus, or change dismissal callbacks. Dialog body clipping stops before button regions.

## 4. Wrapping and overflow semantics

Text drawing clips to the intersection of the requested bounds and the graphics viewport. Dialogs retain their existing dimensions. Toast content uses the visible width available on the screen. Button labels remain single-line and clip within their current button rectangles if a selected font is too large. Body text uses the reusable wrapper and clips vertically when the bounded line limit or the surface height is reached.

When a shared font provider or bitmap owner changes, notification layout is measured again using the current provider generation and display width. Dialog drawing resolves the current shared handle on every draw, so no previous provider's text width is retained.

## 5. Font and cache behavior

Bitmap remains the production default. TrueType selection remains opt-in and uses the existing accepted sizes: 12, 16, 20, 24, and 32 px. The hosted FONTS4 layout checks exercise 12, 16, 20, and 24 px.

Layout uses the provider already held by `SharedTextFont`; selecting an unavailable or malformed TrueType asset still falls back through the existing bitmap selector. Repeated layout does not parse font tables. Glyph rasterization remains in the FONTS1 provider cache, whose existing limits are verified by the repeated-layout checks. The layout scratch area is fixed-size and shared by the shell text renderer.

Kerning, ligatures, contextual shaping, bidirectional reordering, and complex-script shaping are not implemented. Layout preserves valid Unicode scalar boundaries but does not claim full Unicode line-breaking or typography behavior.

## 6. Hosted test results

The FONTS1–FONTS4 foundation suite passed from a unique temporary output directory:

```text
FONTS1 hosted checks: 234, failures: 0
FONTS2 hosted checks: 73, failures: 0
FONTS3 shell text checks: 109, failures: 0
FONTS4 hosted layout and UI checks: 28, failures: 0
```

FONTS4 checks proportional TrueType advances, word-boundary wrapping, explicit paragraphs and CRLF, line spacing, long unbroken input, the 4,096-scalar and 128-line bounds, narrow and empty rectangles, partial viewport clipping, supplementary scalar preservation, missing-glyph fallback, missing-font bitmap fallback, 12/16/20/24 px metrics, provider switching, deterministic repeated layout, and bounded glyph-cache reuse.

The UI surface checks compare bitmap notification and dialog pixels with the existing direct bitmap renderer. They also render wrapped TrueType notification and dialog text, clip fixed button labels, verify repeatable pixels, and confirm that renderer calls leave their supplied button rectangles unchanged. Existing lifecycle and interaction checks passed:

```text
UEFI desktop interaction tests: PASS (42 assertions)
Task Manager Memory Details regression: PASS
NotificationManager boot-reset regression: PASS
```

The Task Manager PowerShell regression was run with `pwsh`; Windows PowerShell 5.1 on this host does not resolve the script’s `[ulong]` cast.

## 7. Pixel regression results

- The FONTS1 deterministic 12/20/32 px hash remains **`0xEC1A69C86C9C5CC6`**.
- FONTS3 bitmap shell hashes remain unchanged: title `0x599B7282176E4F39`, taskbar label `0xA1E78F23185F50F9`, clock/date `0x77A6611F5B2E6544`, and Start Menu row `0x0C8FC1FFD90FE0F9`.
- FONTS4 bitmap toast and dialog fixtures match their direct bitmap-rendered pixels. Repeated TrueType toast and dialog rendering is deterministic in hosted graphics.

No golden hash was changed.

## 8. Build and guest qualification

| Qualification | State | Evidence |
| --- | --- | --- |
| Hosted shared-font and UI-renderer compilation | **Passed** | FONTS1–FONTS4 hosted suite compiled and passed. |
| Full guideXOS source-project compilation | **Blocked before C# compilation** | Direct `dotnet build` reports missing `guideXOS/obj/ManagedArtifactIdentities.g.cs` (`CS2001`). No generated metadata or identity substitute was created. |
| Full build / image generation | **Not run** | `out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json` and `out/rt` are absent. The missing source-project identity file also blocks the normal build. |
| Guest execution | **Not run** | FONTS3 records QAC's exclusive QEMU ownership; this work did not launch QEMU. |

The build dependency checks were repeated during FONTS4 and all three known dependencies remain absent. Guest and physical hardware acceptance remain separate gates.

## 9. Known limitations

- Full production-project compilation is still unqualified because the generated managed identity source is absent. Hosted checks compile the shared layout and renderer helpers, but they do not replace a complete guideXOS build.
- The notification data model supports one body string and has no separate title field.
- Bitmap dialog bodies keep their existing legacy draw behavior to preserve default pixels. Provider-aware word wrapping is used for TrueType dialog bodies; the reusable layout engine itself supports both providers.
- Content beyond the documented scalar, line, width, or clip limits is safely clipped or truncated without an ellipsis. The interface does not perform script shaping or full Unicode line-break analysis.
- QEMU and physical hardware behavior have not been verified in this phase.

## 10. Recommended FONTS5 frontier

Restore the real build identities, Phase 29 metadata, and runtime checkout, then compile and boot the full system under the authorized QAC workflow. After that gate, use the same fixed-bound helper for additional static dialog and application labels. Text inputs and caret placement should be considered only with a dedicated provider-aware input geometry test, keeping all existing hit regions stable. Continue to leave bitmap as the default until guest rendering and interactions are accepted.
