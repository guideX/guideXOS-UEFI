# FONTS2: Shared Font Interface and First Desktop Integration

## Outcome

**Outcome A — `FONTS2_HOSTED_DESKTOP_INTEGRATION_ACCEPTED`.** The shared bitmap/TrueType contract is implemented, one production desktop surface uses it, bitmap remains the production default, hosted TrueType selection and bitmap fallback are verified, the FONTS1 hosted suite still passes, and the UEFI source project compiles without new warnings. This is hosted integration acceptance only. No bootable image or guest acceptance is claimed.

## Repository state

- Repository: `D:\dev\guideXOSUEFI_FONT_SUPPORT`
- Branch: `FONT_SUPPORT`
- Starting HEAD: `915ee319370ce50ce07620ccd346ef1cc0f4dfc7`
- Source implementation commit: `af0592dfb1f41afbeae53c541ff8f5cec409580f`
- Hosted qualification expansion commit: `7763a64e2326b760308e765a6380a854cd68fb85`
- Ending HEAD after the production and test changes: `7763a64e2326b760308e765a6380a854cd68fb85`
- The initial worktree was clean. The implementation was staged by explicit path and the staged diff passed `git diff --cached --check` before commit.
- No branch switch, reset, stash, rebase, merge, push, QEMU launch, QAC admission, or unrelated source change was performed.

## Architecture audit

### Existing production bitmap path

`guideXOS/GUI/WindowManager.cs` reads `Fonts/roboto/roboto_12pt_regular.png` in UEFI mode, creates the resident `guideXOS.Misc.IFont` atlas object, and selects its generated bitmap fallback when the resource is unavailable or invalid. `WindowManager.font` remains that object; FONTS2 does not change its initialization or default selection.

`Kernel/Misc/IFont.cs` maps each character to a `FontSize × FontSize` atlas cell. Its `GetGlyphWidth` finds the rendered cell width, `GetAdvance` applies the existing `Padding`, `MeasureString` reports the greater of the final pen position and visible ink extent, and `DrawString` copies the atlas pixels through `Graphics.DrawPoint`. The atlas owns the baked glyph color and alpha; callers do not tint it.

Most desktop text still calls `WindowManager.font.DrawString`, `MeasureString`, or reads `FontSize` directly. Those paths include the taskbar, Start Menu, window chrome, notifications, dialogs, and applications. Their geometry and control behavior were left alone in this phase.

### Existing TrueType path

`guideXOS/Kernel/Misc/TrueTypeFont.cs` is the FONTS1 renderer. It parses a bounded standalone `glyf` TrueType font, lazily rasterizes glyphs, and draws grayscale coverage through the same `Graphics.DrawPoint` clipping and alpha blend path. It exposes ascent, signed descent, line gap, line height, glyph advances, measurement, and drawing. Its drawing Y coordinate is the top of the first line; glyph pixels are placed using the ascent baseline and each glyph's signed bearings. Measurement and drawing both use the same hmtx-derived advances.

FONTS1's 64-entry/64-KiB glyph cache, 4-KiB per-glyph cache threshold, 512-KiB font-data limit, outline/raster bounds, malformed-table checks, and deterministic invalid-glyph fallback remain in place. Kerning, shaping, bidi, ligatures, hint execution, CFF, and color fonts remain unsupported; FONTS2 does not approximate them.

### Layout and input assumptions

The chosen surface is the UEFI desktop's four static image-tile labels: `FILES`, `DOCS`, `IMAGES`, and `AUDIO`, drawn from `guideXOS/GUI/Desktop.cs`. `BuildUefiIconLayout` already measures those labels and uses their width and font height to build `UefiDesktopIconGeometry`; that geometry also defines the pointer hit rectangle used by `HitTest`, drag clamping, selection, and activation. The text is static and has no cursor, truncation, or control-specific alignment rules, making this a small surface with an existing measured-layout seam.

Before FONTS2, the hit rectangle used `WindowManager.font.MeasureString(label)` and `WindowManager.font.FontSize`; drawing started at the computed `LabelX, LabelY`. FONTS2 obtains both dimensions from the selected shared handle and draws within the measured label rectangle. For the bitmap provider, widths, line height, label coordinates, and hit rectangle are unchanged. The shared draw clips ink to the half-open label rectangle, so longer text cannot paint outside the measured bounds. The surface has no separate text truncation policy.

## Shared interface and coordinates

`Kernel/Misc/SharedTextFont.cs` provides a small non-owning handle with these operations:

- `GetFontMetrics`, `GetLineHeight`, `GetBaseline`, and `GetGlyphAdvance`
- `MeasureText`, returning maximum line width, line count, and total line-box height
- `DrawText`, accepting a destination, color, half-open clipping rectangle, and left/center/right block alignment
- `CreateDefaultBitmap` and `TrySelectTrueType` for bounded provider selection

All text origins are the top-left of the first line box. The baseline is returned as a positive Y offset from that origin. Bitmap cells draw from their top row, so their baseline is the cell's bottom edge (`FontSize`). TrueType uses its scaled ascent as the baseline offset and retains its signed descent. Line boxes advance by the provider's line height. Multiline measurement uses the widest line; CR, LF, and CRLF line breaks are counted as expected. Alignment positions the measured text block within the supplied width.

The shared handle holds references to existing provider instances and owns no font bytes, atlas image, or glyph cache. The desktop caches only a bitmap handle, recreating it if `WindowManager.font` changes and clearing it during desktop initialization. The handle's provider is immutable, so layouts do not retain metrics from a previous provider. The selected tile surface rebuilds geometry from its current shared handle. Text range measurement does not allocate a substring.

## Bitmap compatibility and fallback

The bitmap adapter uses `IFont`'s existing cell scan, character lookup, baked pixels, alpha blend, and padding-based advances. It adds a single-character advance query and a clipped draw path; it does not replace or regenerate the atlas. The legacy bitmap default is selected in production. Missing atlas glyphs retain the existing zero-width behavior.

TrueType selection is explicit and only accepts a valid renderer at 12, 16, 20, 24, or 32 px. A failed selection returns `false` and, when available, the caller's bitmap fallback handle. It does not retry selection or recurse. With no usable fallback, metrics, measurement, and drawing report failure. No persistent setting, global preference, or end-user switch was added. The production tile surface currently asks for the bitmap default; hosted qualification passes a separately selected TrueType handle to the same tile label adapter.

## TrueType metric integration

The shared TrueType provider delegates advances, line metrics, and pixel rendering to the same `TrueTypeFont` instance at its existing requested size. `MeasureText` uses the renderer's range measurement, and `DrawText` uses its clipped draw entry point; the two paths do not use bitmap widths for TrueType layout. Empty glyphs such as spaces retain their advance, signed bearings remain in raster placement, missing Unicode scalars use FONTS1's replacement-glyph behavior, and an empty string measures as one empty line box. Unsupported shaping features are documented above rather than synthesized.

## Hosted qualification and pixel evidence

Commands and results:

```text
dotnet run --project Tests\TrueTypeRuntimeFoundation\TrueTypeRuntimeFoundation.csproj --configuration Release --no-incremental
FONTS1 hosted checks: 234, failures: 0
FONTS2 hosted checks: 73, failures: 0
Hosted multi-size TrueType pixel hash: 0xEC1A69C86C9C5CC6
```

The FONTS1 12/20/32-px Roboto fixture retains its established hash. A shared-interface render and the direct FONTS1 render are pixel-identical for that fixture; a second draw also has the same hash. The bitmap label fixture compares every pixel from the direct atlas draw with the shared bitmap draw and gets an identical hash. The FONTS2 checks additionally cover empty and multiline measurements, bitmap advances and fallback, all five qualified TrueType sizes, signed vertical metrics, explicit provider selection, invalid handles, fallback, centering and right alignment, repeated cache-backed draws, long strings, missing-scalar rendering, negative origins, four-edge clipping, and the measured tile geometry.

The selected desktop adapter and geometry were exercised with the hosted TrueType provider. The TrueType label bounds determine the tested hit rectangle; the label is hittable, and a point outside its measured horizontal bound is not. The bitmap layout test compares the pre-migration and shared geometry fields. The full existing desktop interaction suite also passed:

```text
dotnet run --project Tests\UefiDesktopInteractionTests\UefiDesktopInteractionTests.csproj --configuration Release --no-incremental
UEFI desktop interaction tests: PASS (42 assertions)
```

## Memory and runtime bounds

The shared wrapper creates no font data, glyph masks, or persistent layout cache. It uses the existing provider-owned atlas and TrueType data. TrueType retains the FONTS1 limits: 512-KiB source data, 16,384 glyphs, 1,024 outline points, 128 contours, 32 composite components, depth 8, 128 × 128 glyph raster bounds, and a cache of at most 64 entries / 64 KiB. The existing renderer skips caching individual masks larger than 4 KiB. Cache misses can allocate a bounded glyph mask once; warm shared draws reuse cached coverage. No font file access occurs in the draw path.

The wrapper uses project-local types and integer arithmetic; it adds no desktop framework dependency, reflection, threading, or runtime code generation.

## Source compilation and build boundary

The normal direct source build stops because the project includes `guideXOS/obj/ManagedArtifactIdentities.g.cs`, which is absent. For compile validation only, a temporary ignored stub with the required `Matches(byte[], uint)` signature was created, the existing source project was built, and that exact stub was removed in a `finally` cleanup. The stub returned `false`; it was not used to construct or admit an image and was not committed.

```text
dotnet build guideXOS\guideXOS.csproj --configuration Release --no-restore -p:SkipISO=true
Build succeeded.
140 Warning(s)
0 Error(s)
```

The FONTS1 report records the same 140-warning baseline. This compile emitted no warning from `SharedTextFont.cs`, `UefiDesktopLabelRenderer.cs`, or the new TrueType shared entry points. Existing warnings include the .NET 7 target/runtime notices and pre-existing source/analyzer warnings. This result is source compilation only, not a bootable-image build.

The existing full-build inputs remain absent: `out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json` and `out/rt`. No Phase 29 metadata, runtime checkout, or Build Infrastructure change was fabricated. No bootable image was built.

## Changed files and commits

Production implementation, commit `af0592dfb1f41afbeae53c541ff8f5cec409580f`:

- `Kernel/Kernel.projitems` — include the shared interface in the kernel source set.
- `Kernel/Misc/IFont.cs` — expose atlas advances and add bounded clipping and range measurement.
- `Kernel/Misc/SharedTextFont.cs` — shared provider, metric, measurement, alignment, and draw contract.
- `guideXOS/Kernel/Misc/TrueTypeFont.cs` — add range measurement and shared clipped draw support while preserving the original entry points.
- `guideXOS/GUI/Desktop.cs` — route only UEFI tile-label layout and drawing through the bitmap-default handle.
- `guideXOS/GUI/UefiDesktopLabelRenderer.cs` — shared measured tile-label layout/draw adapter used by production and hosted qualification.
- `Tests/TrueTypeRuntimeFoundation/Program.cs` — include FONTS2 qualification and hosted kernel stubs.
- `Tests/TrueTypeRuntimeFoundation/TrueTypeRuntimeFoundation.csproj` — link the production provider, adapter, and geometry source into hosted tests.

Hosted test expansion, commit `7763a64e2326b760308e765a6380a854cd68fb85`:

- `Tests/TrueTypeRuntimeFoundation/SharedTextFontChecks.cs` — 73 shared-interface, rendering, fallback, and desktop-layout assertions.

This report is committed as a follow-up documentation change. The production source and hosted tests are committed in the two changes above; the final repository HEAD includes this report.

## Remaining qualification gates

Bitmap remains the desktop-wide default. Other desktop surfaces have not migrated. Before any broader or default TrueType rollout, regenerate the real managed artifact identities, restore the Phase 29 metadata and runtime checkout, complete the actual image build, and conduct the separately authorized QEMU/QAC guest qualification. FONTS2 did not request QAC ownership or launch QEMU.
