# FONTS1: Runtime TrueType Foundation

**Repository:** `D:\dev\guideXOSUEFI_FONT_SUPPORT`

**Branch:** `FONT_SUPPORT`

**Status:** Outcome B — hosted rendering and source compilation are verified; the managed-runtime build chain and QAC guest acceptance remain outstanding.

## Prior-work audit

The requested checkout is on `FONT_SUPPORT`, based on `origin/FONT_SUPPORT`, and was clean before this work. The pasted request named a different checkout (`guideXOSUEFI_FONTS`, branch `FONTS`); this implementation follows the repository and branch named by the active task and current workspace.

The current tree and branch history do not contain `ManagedSoftwareRasterizer.cs`, `ManagedPhase48FontRegistry`, or a Phase 47/48 TTF implementation. A search of the available refs and tracked sources found no Phase 47/48 font commits or active registration path. The similarly named source at `Kernel/Misc/TrueTypeFont.cs` is a separate legacy implementation and is not included by `Kernel/Kernel.projitems`. The compiled desktop source is `guideXOS/Kernel/Misc/TrueTypeFont.cs`; before FONTS1 it was an incomplete parser/rasterizer stub used by the demo.

The active desktop-wide text path remains the bitmap atlas:

- `guideXOS/GUI/WindowManager.cs` loads `Fonts/roboto/roboto_12pt_regular.png` into `IFont`; its generated bitmap fallback remains the failure path.
- `Kernel/Misc/IFont.cs` draws and measures atlas glyphs through `guideXOS.Graph.Graphics`. Normal windows, taskbar, Start Menu, and applications continue using this surface.
- `Kernel/Misc/BitFont.cs` is a separate named registry for packed BTF bitmap fonts. Startup registers the `Enludo` BTF font there; it is not the desktop atlas renderer.
- `UEFI_DIAGNOSTIC_FONT` in `guideXOS/Program.cs` probes the existing PNG-backed `IFont`, not TTF.
- `guideXOS/DefaultApps/TTFFontDemo.cs` is opt-in through the FConsole `fontdemo` command. It was the only active consumer of `TrueTypeFont` and did not establish a global desktop TTF path.

The packaged `Roboto-Regular.ttf` exists in both `Ramdisk/Fonts/roboto` and `Ramdisk_src/Fonts/roboto`; each copy is 146,004 bytes with SHA-256 `DBD285B518E398832F6F4A736109C355CE25A49546BFCE41BAB256C9EF7E56EB`. The PNG atlas copies are also identical (SHA-256 `EBB4218CC1C6CA9701D7341372CBF06F64E3DD04A2407A6AC96A353ADE66B322`). The supplied Roboto cmap is format 4; format 12 is covered by a synthetic hosted fixture.

The read-only Server `FONT_SUPPORT` sources were used as a design reference for bounded parsing and outline behavior. No Server files were changed, and the UEFI code has no Server dependency.

## Implementation

The runtime implementation is in `guideXOS/Kernel/Misc/TrueTypeFont.cs`. It owns a bounded copy of the font bytes, validates table ranges before use, lazily decodes glyph outlines, rasterizes them to grayscale coverage, and draws through the existing `Graphics.DrawPoint` clipping and alpha-compositing path. The `TrueTypeFont` class retains its existing public `DrawString` and `RenderChar` signatures; `RenderChar` adapts cached coverage to an `Image`. `GetGlyph` exposes coverage and metrics, and `DrawStringMeasured` adds a width-returning draw helper.

Supported input is a standalone TrueType sfnt face with `glyf` outlines (`0x00010000` or `true` sfnt signature). The parser validates the required `head`, `maxp`, `hhea`, `hmtx`, `loca`, `glyf`, and `cmap` tables, duplicate/overlapping tables, table alignment and range, cmap records, glyph offsets, and metrics. Unicode cmap formats 4 and 12 are accepted. It decodes simple contours, repeated point flags, quadratic curves, and composite components with scale/XY-scale/2×2 transforms, scaled offsets, point attachment, and `USE_MY_METRICS`. Composite instruction bytes are bounds-checked and skipped; font hint bytecode is never executed.

The working limits are enforced in code:

| Resource or behavior | Limit / support |
| --- | --- |
| Font bytes / tables / glyphs | 512 KiB / 64 / 16,384 |
| Requested pixel size | 1–72 px; proof sizes 12, 16, 20, 24, 32 px |
| Per-glyph outline | 1,024 points, 128 contours |
| Composite outlines | 32 components per glyph; nesting depth 8 |
| Raster bounds | 128 × 128 px per glyph |
| Glyph cache | 64 entries, 64 KiB coverage total; a glyph over 4 KiB is rendered but not cached |
| Curve and coverage sampling | 8 line segments per quadratic curve; 4×4 grayscale supersampling with nonzero winding fill |

Metrics include scaled ascent, descent, line gap and height, glyph advance, left side bearing, and baseline-relative glyph bounds. `MeasureString` and `DrawString` share the same advance values, support CR/LF lines and UTF-16 surrogate pairs, and clip before writing pixels. Missing scalar mappings fall back to `?`, then glyph zero. The demonstration retains bitmap labels and reports TTF loading failure through the existing bitmap font.

The renderer is opt-in. `WindowManager.font`, the boot text path, and protected desktop behavior were not globally redirected to TTF. The demo now uses one packaged TTF to draw 12, 20, and 32 px samples, plus text and clipping cases; its load-failure copy remains on `IFont`.

Changed files:

- `guideXOS/Kernel/Misc/TrueTypeFont.cs` — bounded parser, decoder, coverage rasterizer, metrics, cache, drawing API, and legacy image adapter.
- `guideXOS/DefaultApps/TTFFontDemo.cs` — multi-size Roboto proof and bitmap failure labels.
- `Tests/TrueTypeRuntimeFoundation/TrueTypeRuntimeFoundation.csproj`, `Program.cs`, and `.gitignore` — hosted parser, rendering, clipping, cache, malformed-input, repeat-pixel checks, and ignored build outputs.
- `docs/FONTS1_TRUETYPE_RUNTIME_FOUNDATION.md` — this audit and implementation record.

## Hosted verification

Command:

```powershell
dotnet run --project Tests\TrueTypeRuntimeFoundation\TrueTypeRuntimeFoundation.csproj --configuration Release --no-incremental
```

Result: **234 checks, 0 failures.** The cases cover the 12/16/20/24/32 px size matrix; real Roboto glyph coverage and advances; antialias coverage; format 4 and synthetic format 12 mapping; supplementary scalar fallback; empty space; composite cycle fallback; malformed/truncated headers and table/cmap/loca ranges; source/size limits; bounded cache pressure and warm hits; the legacy `RenderChar` image adapter; viewport clipping; alpha blending; and repeat-pixel output.

The hosted 12/20/32 px frame fixture has pixel hash `0xEC1A69C86C9C5CC6`. A 300-draw warm loop measured **58,155 ns/draw** in the final recorded run, with 11,797 cache hits, 39 misses/rasterizations, 39 entries, and 2,188 cached coverage bytes. This is a host measurement of a representative text draw, not a UEFI frame-rate claim; timing varied between runs on the host.

## Build status

`dotnet restore guideXOS\guideXOS.csproj --verbosity minimal` completed. The direct project compile:

```powershell
dotnet build guideXOS\guideXOS.csproj --configuration Release --no-restore -p:SkipISO=true
```

completed with **0 errors and 140 warnings** after supplying a temporary ignored compile-only `guideXOS/obj/ManagedArtifactIdentities.g.cs` stub because the expected generated artifact-identity source was absent. The stub was not part of the implementation and must not be used to produce a bootable image. Warnings include the project’s .NET 7 end-of-support and runtime identifier warnings plus existing nullable, obsolete, unused-field, and analyzer warnings.

The repository build pipeline did not produce a complete UEFI artifact:

- `build.ps1 -SkipBootloader -SkipRamdisk` stopped in the managed artifact preflight because ignored `out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json` was missing.
- After restoring `UserManagedProof`, `build.ps1 -SkipBootloader` progressed through some managed phases but stopped when the .NET runtime source checkout was missing at `out/rt` (`fatal: cannot change to '.../out/rt': No such file or directory`).

No QEMU was launched. The exclusive QAC baton policy was respected; no result here represents UEFI guest or hardware acceptance.

## Remaining gaps and guest validation

FONTS1 does not add CFF/OpenType outlines, TTC collections, variable fonts, hint execution, kerning, GPOS shaping, ligatures, mark positioning, bidirectional layout, color fonts, or LCD subpixel rendering. The bounded cache deliberately bypasses storage for individual coverage masks above 4 KiB. The packaged Roboto uses cmap 4, so cmap 12 is exercised with a structurally valid hosted fixture instead of a second packaged font. The separate `RenderChar` compatibility adapter allocates an `Image` copy on demand; callers needing repeated direct pixels should use `GetGlyph`.

Guest validation plan for the QAC lane when its exclusive baton is available:

1. Use the QAC-owned build/boot flow after the .NET Support acceptance lane releases QEMU; do not start another QEMU process.
2. Cold boot the produced UEFI image and verify the ordinary desktop still uses the PNG-backed bitmap path.
3. Open FConsole and run `fontdemo`. Capture the 12, 20, and 32 px output, uppercase/lowercase, digits, punctuation, `café`, `Ω`, and `£`, and both clipping edges.
4. Repeat redraws and capture serial output, screen image, and any crash/reset evidence. Confirm the bitmap labels/error fallback and desktop remain usable after closing the demo.
5. Let QAC stop and census the guest and attach the resulting evidence to the acceptance record. Do not call the hosted pixel hash a guest result.

## FONTS2 frontier

The next phase should add a small desktop font abstraction that can select either the current `IFont` atlas or this TTF renderer while exposing common measurement, line-height, and baseline semantics. Keep the atlas as default and fallback. First migrate one low-risk surface (a demo or one settings pane), then verify layout and hit-testing before titles, taskbar, Start Menu, and application controls move individually. Add a font registry/preferences layer only after the renderer-selection contract is stable. Treat shaping, bidi, kerning/GPOS, and broader Unicode coverage as separate scoped work rather than silently expanding the first integration.
