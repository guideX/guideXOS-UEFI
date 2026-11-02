# FONTS3: Shell Typography Integration

## Outcome

**Outcome B — hosted shell integration accepted; full source and guest qualification remain blocked.** Window title bars and taskbar text now use the FONTS2 shared font contract. Start Menu text uses the same bounded adapter. The production selection remains the bitmap atlas; TrueType is an explicit opt-in. FONTS1 and FONTS2 hosted checks remain green. The normal source build stops at missing generated metadata, and no bootable image or QEMU result is claimed.

## Preflight and repository state

- Repository: `D:\dev\guideXOSUEFI_FONT_SUPPORT`
- Branch: `FONT_SUPPORT`
- Starting HEAD: `e8e2b9cb88cedc061cf7833382cc6b7fe5dc07b5`
- The only pre-existing worktree entries were untracked `Tests/UefiDesktopInteractionTests/bin/` and `Tests/UefiDesktopInteractionTests/obj/`. They were preserved. Hosted test and build outputs were redirected to new temporary directories under `$env:TEMP`.
- `docs/FONTS2_SHARED_FONT_INTERFACE_DESKTOP_INTEGRATION.md` was reviewed. This phase reuses `SharedTextFont`, its existing bitmap adapter, `TrySelectTrueType`, and the FONTS1 `TrueTypeFont` parser, rasterizer, and glyph cache. No parser or rasterizer was added.
- Phase 29 metadata `out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json` and `out/rt` were both absent at preflight and remain absent. Neither was generated or substituted.

## Integrated surfaces

| Surface | Integrated text | Bounds and layout behavior |
| --- | --- | --- |
| Window chrome | Window title in `Window.OnDraw` | TrueType uses ascent/baseline and line height from the selected provider. Text is centered in the title region when it fits; an overlong title starts at the region's left edge and is clipped before the title-button cluster. The clip is recalculated from the current window position and width on every draw. |
| UEFI taskbar | Application display names, clock, and date | App button rectangles and clock-reserved width retain their existing bitmap-derived dimensions. Selected-font text is vertically aligned and clipped inside those fixed regions. This does not change taskbar hit targets or visible-slot calculations. |
| Taskbar context menu | “Task Manager” row label | Draws inside the existing menu row rectangle; row geometry and input handling are unchanged. |
| Start Menu | Recent and all-program entries, right-column app labels, recent-document popup labels, Shutdown, power-menu, and All Programs button labels | Text is clipped to the existing row or button rectangles. `TruncateToWidth` uses selected TrueType measurements and avoids splitting UTF-16 surrogate pairs. Text metrics do not resize menu rows, buttons, or input hitboxes. The frame cache is invalidated when the selected shared provider changes. |

The legacy non-UEFI taskbar path and other application, notification, and dialog text remain on their existing bitmap calls.

## Selection, fallback, and rendering behavior

`WindowManager` owns one `SharedTextFontSelection` for these migrated shell surfaces. It stores the selected shared handle, not a second renderer or a separate global mode flag. Bitmap mode is selected by default. The internal `TrySelectShellTrueType(byte[], size)` entry point delegates to the existing `SharedTextFont.TrySelectTrueType` validation; `SelectBitmapShellTextFont()` restores the atlas. A failed parse, missing bytes, invalid renderer, or unsupported desktop size resolves to the bitmap fallback when available.

The shell selector accepts the existing qualified sizes: **12, 16, 20, 24, and 32 px**. The bitmap atlas remains its existing 20 px cell font. The shared interface continues to report ascent, signed descent, baseline, line height, and provider advances; TrueType measurement and drawing both use the same `TrueTypeFont` instance. Unicode fallback remains FONTS1's replacement-glyph behavior. Bitmap glyph color remains baked into the atlas; TrueType coverage continues through the canonical alpha-blending draw path.

The bitmap adapters preserve the previous draw calls for titles, taskbar labels and clock, and Start Menu rows in the tested fixtures. Window titles now also have an explicit title-bar clip for overlong text. All TrueType shell draws clip to the intersection of their surface rectangle and the display, so partially offscreen text cannot write beyond the viewport. Fixed taskbar and Start Menu geometry intentionally stays unchanged when a larger font is selected; sizes taller or wider than those existing boxes are clipped.

The shell does not read or parse a TTF during ordinary drawing. Opt-in parses the bounded font data once, and warm drawing uses the existing FONTS1 cache (64 entries / 64 KiB maximum, with the existing per-glyph cache threshold). The new adapter adds no persistent layout cache or per-frame font-data allocation. Start Menu truncation measures at most 256 Unicode scalars and creates only a bounded result.

## Hosted tests and pixel regressions

The FONTS1/FONTS2/FONTS3 hosted foundation suite was run from an isolated temporary output directory:

```powershell
$taskArtifactRoot = Join-Path $env:TEMP ('guideX-fonts3-' + [guid]::NewGuid().ToString('N'))
dotnet run --project Tests\TrueTypeRuntimeFoundation\TrueTypeRuntimeFoundation.csproj --configuration Release `
  --property:BaseOutputPath="$taskArtifactRoot\bin\" `
  --property:BaseIntermediateOutputPath="$taskArtifactRoot\obj\"
```

Result:

```text
FONTS1 hosted checks: 234, failures: 0
FONTS2 hosted checks: 73, failures: 0
FONTS3 shell text checks: 109, failures: 0
```

FONTS3 checks direct bitmap pixel identity for title, taskbar label, clock/date, and Start Menu row fixtures. The respective fixture hashes are:

| Bitmap fixture | Pixel hash |
| --- | --- |
| Window title | `0x599B7282176E4F39` |
| Taskbar label | `0xA1E78F23185F50F9` |
| Taskbar clock and date | `0x77A6611F5B2E6544` |
| Start Menu row | `0x0C8FC1FFD90FE0F9` |

The FONTS1 deterministic 12/20/32 px render hash remains **`0xEC1A69C86C9C5CC6`**. The FONTS2 desktop tile-label pixel comparison and bitmap geometry checks remain green. FONTS3 also checks explicit TrueType opt-in and fallback for missing, malformed, and unsupported-size font inputs; deterministic measurement; all five qualified sizes; 128, 320, and 640 px display widths; long titles; viewport-edge clipping; Unicode including a supplementary scalar; taskbar and Start Menu fixed bounds; alpha blending; and warm glyph-cache reuse.

The existing interaction suite was run with isolated outputs:

```powershell
$taskArtifactRoot = Join-Path $env:TEMP ('guideX-interaction-' + [guid]::NewGuid().ToString('N'))
dotnet run --project Tests\UefiDesktopInteractionTests\UefiDesktopInteractionTests.csproj --configuration Release `
  --property:BaseOutputPath="$taskArtifactRoot\bin\" `
  --property:BaseIntermediateOutputPath="$taskArtifactRoot\obj\"
```

It passed **42 assertions**, covering desktop selection, double-click activation, pointer capture, drag thresholds, clamping, and input arbitration. `Tests/TaskManagerMemoryDetailsRegression.ps1` also passed, including its window-focus and taskbar lifecycle source checks. The changed window and taskbar methods are drawing-only; `Window.OnInput`, resize handling, focus routing, and `Taskbar.HandleUefiInput` were not changed. The repository has no hosted end-to-end window resize or taskbar-click simulator, so those paths still need guest interaction coverage when QAC ownership permits.

The FONTS1 warm-host benchmark in the final suite run measured **54,169 ns/draw** over 300 draws, with 11,797 cache hits, 39 misses/rasterizations, 39 entries, and 2,188 cached coverage bytes. This host-only measurement is not a UEFI frame-rate claim. FONTS3 repeated surface draws caused no additional rasterizations after the test glyphs were warm.

## Build and QEMU boundary

The source build was attempted with output redirected to a unique temporary directory:

```powershell
dotnet build guideXOS\guideXOS.csproj --configuration Release --property:SkipISO=true `
  --property:BaseOutputPath="$taskArtifactRoot\bin\" `
  --property:BaseIntermediateOutputPath="$taskArtifactRoot\obj\"
```

It failed before compiling the source because `guideXOS/obj/ManagedArtifactIdentities.g.cs` is missing (`CS2001`). The build reported four existing SDK warnings and one error. The known Phase 29 metadata and `out/rt` runtime checkout are also absent. No substitutes were created. This is not source-compilation acceptance, an image build, or a bootable-image result.

QEMU was not launched. QAC retains exclusive QEMU ownership, and this task did not authorize a guest run.

## Remaining work and recommended FONTS4 frontier

- Restore the real generated managed artifact identities, Phase 29 metadata, and runtime checkout, then compile the complete source project and perform the normal image build.
- Add a hosted window and taskbar input harness or complete the separately authorized QAC guest checks for window dragging/resizing/focus and taskbar activation.
- Keep bitmap as the default until those build and interaction gates pass.
- Recommended FONTS4 scope: notifications and modal shell dialogs. Their variable-length body text needs provider-aware wrapping and measurement while keeping existing button hitboxes fixed. Continue to route both providers through the shared contract and qualify the dialogs at multiple sizes before changing any default.
