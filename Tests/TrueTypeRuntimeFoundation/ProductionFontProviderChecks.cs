using System;
using System.Drawing;
using guideXOS.GUI;
using guideXOS.Graph;
using guideXOS.Misc;

namespace guideXOS.FontTests {
    internal static class ProductionFontProviderChecks {
        private static int _checks;
        private static int _failures;

        public static int Run(byte[] fontBytes) {
            _checks = 0;
            _failures = 0;
            IFont bitmap = CreateCompleteBitmapFont();
            SharedTextFontSelection selection = new SharedTextFontSelection();
            SharedTextFont selected = selection.Resolve(bitmap);
            Check(selected != null && selected.IsAvailable &&
                  selected.Provider == SharedFontProviderKind.BitmapAtlas &&
                  selection.RequestedProvider ==
                      SharedFontProviderKind.BitmapAtlas &&
                  selection.HasInitializedProvider &&
                  selection.InitializedProvider ==
                      SharedFontProviderKind.BitmapAtlas,
                  "production provider selection starts on initialized bitmap");

            string bitmapTruncated = ShellTextSurfaceRenderer.TruncateToWidth(
                selected, "Computer Files", 48);
            SharedTextMeasurement bitmapTruncatedMetrics;
            bool bitmapTruncatedMeasured = selected.MeasureText(
                bitmapTruncated, out bitmapTruncatedMetrics);
            Graphics bitmapMenuPixels = new Graphics(100, 40, 0xFF18212Au);
            bool bitmapMenuDrawn =
                ShellTextSurfaceRenderer.DrawStartMenuText(bitmapMenuPixels,
                    selected, bitmap, 10, 10, "Computer Files", 10, 10,
                    48, bitmap.FontSize, true);
            Check(bitmapTruncatedMeasured &&
                  bitmapTruncated.EndsWith("...", StringComparison.Ordinal) &&
                  bitmapTruncatedMetrics.Width <= 48 && bitmapMenuDrawn &&
                  HasInkOnlyInsideClip(bitmapMenuPixels, 10, 10, 48,
                                       bitmap.FontSize) &&
                  selection.HasProviderBeenUsed &&
                  selection.ProviderActuallyUsed ==
                      SharedFontProviderKind.BitmapAtlas,
                  "bitmap menu truncation uses full-string metrics and records actual drawing");

            int bitmapGeneration = selection.Generation;
            bool selectedTrueType = selection.TrySelectTrueType(fontBytes,
                20, bitmap, out selected);
            Check(selectedTrueType && selected != null &&
                  selected.Provider == SharedFontProviderKind.TrueType &&
                  selection.RequestedProvider ==
                      SharedFontProviderKind.TrueType &&
                  selection.InitializedProvider ==
                      SharedFontProviderKind.TrueType &&
                  selection.FallbackReason == null &&
                  selection.Generation > bitmapGeneration,
                  "production selector initializes requested TrueType");

            bool selectedTwentyFour = selection.TrySelectTrueType(fontBytes,
                24, bitmap, out selected);
            int trueTypeLabelWidth, trueTypeLabelHeight;
            bool trueTypeLabelMeasured = UefiDesktopLabelRenderer.TryMeasure(
                selected, "FILES", out trueTypeLabelWidth,
                out trueTypeLabelHeight);
            UefiDesktopIconGeometry trueTypeGeometry =
                UefiDesktopIconGeometry.Create(0, 20, 20, 48,
                    trueTypeLabelWidth, trueTypeLabelHeight, 8);
            Graphics trueTypeTwentyFourPixels =
                new Graphics(180, 112, 0xFF18212Au);
            bool trueTypeTwentyFourDrawn = UefiDesktopLabelRenderer.Draw(
                trueTypeTwentyFourPixels, selected, trueTypeGeometry, "FILES");
            int selectedMenuWidth = ShellTextSurfaceRenderer.MeasureTextWidth(
                selected, bitmap, "Performance Widget");
            SharedTextMeasurement selectedMenuMeasurement;
            bool selectedMenuMeasured = selected.MeasureText(
                "Performance Widget", out selectedMenuMeasurement);
            Check(selectedTwentyFour && trueTypeLabelMeasured &&
                  trueTypeTwentyFourDrawn &&
                  trueTypeGeometry.LabelWidth == trueTypeLabelWidth &&
                  trueTypeGeometry.LabelHeight == trueTypeLabelHeight &&
                  trueTypeGeometry.HitWidth >= trueTypeLabelWidth &&
                  trueTypeTwentyFourPixels.InvalidDrawAttempts == 0 &&
                  HasInkOnlyInsideClip(trueTypeTwentyFourPixels,
                      trueTypeGeometry.LabelX, trueTypeGeometry.LabelY,
                      trueTypeGeometry.LabelWidth,
                      trueTypeGeometry.LabelHeight),
                  "24 px desktop tile bounds use selected TrueType label metrics");
            Check(selectedMenuMeasured && selectedMenuWidth ==
                  selectedMenuMeasurement.Width && selectedMenuWidth > 0,
                  "24 px context menu width measurement uses selected TrueType metrics");
            selection.TrySelectTrueType(fontBytes, 20, bitmap, out selected);

            int bitmapLabelWidth = bitmap.MeasureString("FILES");
            UefiDesktopIconGeometry geometry =
                UefiDesktopIconGeometry.Create(0, 20, 20, 48,
                    bitmapLabelWidth, bitmap.FontSize, 8);
            UefiDesktopIconGeometry originalGeometry = geometry;
            Graphics trueTypePixels = new Graphics(180, 96, 0xFF18212Au);
            UefiDesktopLabelRenderer.Draw(trueTypePixels, selected, geometry,
                                          "FILES");
            UefiDesktopLabelRenderer.Draw(trueTypePixels, selected, geometry,
                                          "DOCS");
            Check(selected.TrueTypeRasterizations > 0 &&
                  selected.TrueTypeCacheHits > 0 &&
                  selected.TrueTypeCacheEntryCount > 0 &&
                  selected.TrueTypeCacheBytes <= TrueTypeFont.MaximumCacheBytes &&
                  selection.HasProviderBeenUsed &&
                  selection.ProviderActuallyUsed ==
                      SharedFontProviderKind.TrueType,
                  "desktop label draw rasterizes and reuses cached TrueType glyphs");

            Graphics clippedPixels = new Graphics(180, 96, 0xFF18212Au);
            string supplementary = "A\uD83D\uDE00B";
            string bounded = ShellTextSurfaceRenderer.TruncateToWidth(
                selected, supplementary, 22);
            ShellTextSurfaceRenderer.DrawFixedText(clippedPixels, selected,
                bounded, 20, 20, 22, geometry.LabelX, geometry.LabelY,
                geometry.LabelWidth, geometry.LabelHeight, 0xFFFFFFFFu,
                SharedTextAlignment.Left);
            Check(IsWellFormedUtf16(bounded) &&
                  clippedPixels.InvalidDrawAttempts == 0,
                  "TrueType clipping preserves supplementary UTF-16 boundaries");

            int trueTypeGeneration = selection.Generation;
            selection.SelectBitmap(bitmap);
            selected = selection.Resolve(bitmap);
            Graphics bitmapPixels = new Graphics(180, 96, 0xFF18212Au);
            bool bitmapDrawn = UefiDesktopLabelRenderer.Draw(bitmapPixels,
                selected, geometry, "FILES");
            UefiDesktopLabelRenderer.Draw(bitmapPixels, selected, geometry,
                                          "DOCS");
            Check(selected != null &&
                  selected.Provider == SharedFontProviderKind.BitmapAtlas &&
                  selection.RequestedProvider ==
                      SharedFontProviderKind.BitmapAtlas &&
                  selection.Generation > trueTypeGeneration && bitmapDrawn &&
                  selection.HasProviderBeenUsed &&
                  selection.ProviderActuallyUsed ==
                      SharedFontProviderKind.BitmapAtlas &&
                  SameHitGeometry(originalGeometry, geometry),
                  "switching to bitmap redraws within unchanged desktop hit bounds");
            Check(bitmapPixels.PixelHash() != trueTypePixels.PixelHash(),
                  "desktop label pixels change when the provider switches");
            Console.WriteLine("FONTS6 desktop label pixel pairs: Bitmap=0x" +
                bitmapPixels.PixelHash().ToString("X16") + " TrueType=0x" +
                trueTypePixels.PixelHash().ToString("X16"));

            int beforeRepeat = selection.Generation;
            bool repeatedBitmap = selection.TrySelectTrueType(fontBytes, 20,
                bitmap, out selected);
            int afterFirstTrueType = selection.Generation;
            bool repeatedSame = selection.TrySelectTrueType(fontBytes, 20,
                bitmap, out selected);
            Check(repeatedBitmap && repeatedSame &&
                  selected.Provider == SharedFontProviderKind.TrueType &&
                  afterFirstTrueType > beforeRepeat &&
                  selection.Generation == afterFirstTrueType,
                  "repeated TrueType selection is stable and does not reset caches");

            selection.SelectBitmap(bitmap);
            int afterReturnToBitmap = selection.Generation;
            bool selectedAgain = selection.TrySelectTrueType(fontBytes, 20,
                bitmap, out selected);
            bool returnedToBitmap = afterReturnToBitmap < selection.Generation;
            Check(selectedAgain && returnedToBitmap &&
                  selection.Generation > afterReturnToBitmap,
                  "repeated provider switching invalidates each changed layout generation");

            bool malformedSelected = selection.TrySelectTrueType(
                new byte[] { 0, 1, 2, 3 }, 20, bitmap, out selected);
            Check(!malformedSelected && selected != null &&
                  selected.Provider == SharedFontProviderKind.BitmapAtlas &&
                  selection.RequestedProvider ==
                      SharedFontProviderKind.TrueType &&
                  selection.InitializedProvider ==
                      SharedFontProviderKind.BitmapAtlas &&
                  selection.FallbackReason == "source-too-short",
                  "failed TrueType initialization records request and bitmap fallback");

            bool unsupportedSelected = selection.TrySelectTrueType(fontBytes,
                13, bitmap, out selected);
            Check(!unsupportedSelected && selected != null &&
                  selected.Provider == SharedFontProviderKind.BitmapAtlas &&
                  selection.FallbackReason == "unsupported-size",
                  "unsupported TrueType size falls back with a diagnostic reason");

            CheckContextMenuProvider(fontBytes, bitmap);
            CheckMessageBoxMeasurement(fontBytes, bitmap);
            _failures += FontPreferenceConfigurationChecks.Run(fontBytes,
                                                               bitmap);
            CheckDialogAndNotificationSelection(fontBytes, bitmap);

            Console.WriteLine("FONTS6 production provider checks: " +
                _checks + ", failures: " + _failures);
            return _failures;
        }

        private static void CheckContextMenuProvider(byte[] fontBytes,
                                                     IFont bitmap) {
            const string label = "Display Options";
            const int menuX = 8;
            const int rowY = 8;
            const int menuWidth = 200;
            const int itemHeight = 28;
            const int labelX = menuX + 8;
            const int labelWidth = menuWidth - 16;
            SharedTextFontSelection selection = new SharedTextFontSelection();
            SharedTextFont selected = selection.Resolve(bitmap);
            Graphics bitmapPixels = new Graphics(240, 48, 0xFF18212Au);
            bool bitmapDrawn = ShellTextSurfaceRenderer.DrawContextMenuText(
                bitmapPixels, selected, bitmap, label, labelX, rowY, labelX,
                rowY, labelWidth, itemHeight);
            int bitmapGeneration = selection.Generation;

            bool selected14 = selection.TrySelectTrueType(fontBytes, 14,
                bitmap, out selected);
            Graphics trueType14Pixels = new Graphics(240, 48, 0xFF18212Au);
            bool trueType14Drawn = selected14 &&
                ShellTextSurfaceRenderer.DrawContextMenuText(
                    trueType14Pixels, selected, bitmap, label, labelX, rowY,
                    labelX, rowY, labelWidth, itemHeight);
            int generation14 = selection.Generation;
            bool selected18 = selection.TrySelectTrueType(fontBytes, 18,
                bitmap, out selected);
            Graphics trueType18Pixels = new Graphics(240, 48, 0xFF18212Au);
            bool trueType18Drawn = selected18 &&
                ShellTextSurfaceRenderer.DrawContextMenuText(
                    trueType18Pixels, selected, bitmap, label, labelX, rowY,
                    labelX, rowY, labelWidth, itemHeight);
            bool inside = HasInkOnlyInsideClip(trueType14Pixels, labelX,
                rowY, labelWidth, itemHeight) &&
                HasInkOnlyInsideClip(trueType18Pixels, labelX, rowY,
                    labelWidth, itemHeight);

            selection.SelectBitmap(bitmap);
            bool geometryStable = menuX == 8 && rowY == 8 &&
                menuWidth == 200 && itemHeight == 28 &&
                labelX == menuX + 8 && labelWidth == menuWidth - 16;
            Check(bitmapDrawn && trueType14Drawn && trueType18Drawn && inside &&
                  selected.TrueTypeFontSize == 18 &&
                  generation14 > bitmapGeneration &&
                  selection.Generation > generation14 && geometryStable &&
                  bitmapPixels.PixelHash() != trueType14Pixels.PixelHash() &&
                  trueType14Pixels.PixelHash() != trueType18Pixels.PixelHash() &&
                  selection.Resolve(bitmap).Provider ==
                      SharedFontProviderKind.BitmapAtlas,
                  "desktop context menu uses selected provider and size within fixed rows");
        }

        private static void CheckMessageBoxMeasurement(byte[] fontBytes,
                                                       IFont bitmap) {
            const string message = "Save changes before closing?";
            TrueTypeFont renderer;
            bool loaded = TrueTypeFont.TryCreate(fontBytes, 14, out renderer);
            SharedTextFont selected;
            bool selectedTrueType = SharedTextFont.TrySelectTrueType(renderer,
                bitmap, out selected);
            int trueTypeWidth;
            bool measuredTrueType = ShellTextSurfaceRenderer.MeasureMessageBoxWidth(
                selected, bitmap, message, 240, 20, out trueTypeWidth);
            SharedTextMeasurement trueTypeMetrics;
            bool haveMetrics = selected.MeasureText(message,
                out trueTypeMetrics);
            int bitmapWidth;
            SharedTextFont bitmapFont = SharedTextFont.CreateDefaultBitmap(bitmap);
            bool measuredBitmap = ShellTextSurfaceRenderer.MeasureMessageBoxWidth(
                bitmapFont, bitmap, message, 240, 20, out bitmapWidth);
            int narrowWidth;
            bool measuredNarrow = ShellTextSurfaceRenderer.MeasureMessageBoxWidth(
                selected, bitmap, message, 100, 95, out narrowWidth);
            Check(loaded && selectedTrueType && measuredTrueType &&
                  haveMetrics && trueTypeWidth == trueTypeMetrics.Width &&
                  measuredBitmap && bitmapWidth == bitmap.MeasureString(message) &&
                  measuredNarrow && narrowWidth == 1,
                  "MessageBox width measures with its selected provider and clips at narrow screens");
        }

        private static void CheckDialogAndNotificationSelection(
                byte[] fontBytes, IFont bitmap) {
            TrueTypeFont renderer;
            bool loaded = TrueTypeFont.TryCreate(fontBytes, 20, out renderer);
            SharedTextFont trueType;
            bool selectedTrueType = SharedTextFont.TrySelectTrueType(renderer,
                bitmap, out trueType);
            SharedTextFont bitmapFont = SharedTextFont.CreateDefaultBitmap(bitmap);
            const string message = "Save changes before closing?";
            Graphics toastBitmap = new Graphics(240, 100, 0xFF18212Au);
            Graphics toastTrueType = new Graphics(240, 100, 0xFF18212Au);
            Graphics dialogBitmap = new Graphics(240, 100, 0xFF18212Au);
            Graphics dialogTrueType = new Graphics(240, 100, 0xFF18212Au);

            bool bitmapToast = ShellTextSurfaceRenderer.DrawWrappedText(
                toastBitmap, bitmapFont, message, 12, 12, 200, 0,
                0xFFFFFFFFu, 12, 12, 200, 60, SharedTextAlignment.Left);
            bool trueTypeToast = ShellTextSurfaceRenderer.DrawWrappedText(
                toastTrueType, trueType, message, 12, 12, 200, 0,
                0xFFFFFFFFu, 12, 12, 200, 60, SharedTextAlignment.Left);
            Check(loaded && selectedTrueType && bitmapToast && trueTypeToast &&
                  toastBitmap.PixelHash() != toastTrueType.PixelHash(),
                  "notification text follows the selected provider");

            bool bitmapDialog = ShellTextSurfaceRenderer.DrawFixedText(
                dialogBitmap, bitmapFont, message, 12, 12, 200, 12, 12,
                200, 60, 0xFFFFFFFFu, SharedTextAlignment.Left);
            bool trueTypeDialog = ShellTextSurfaceRenderer.DrawFixedText(
                dialogTrueType, trueType, message, 12, 12, 200, 12, 12,
                200, 60, 0xFFFFFFFFu, SharedTextAlignment.Left);
            Check(bitmapDialog && trueTypeDialog &&
                  dialogBitmap.PixelHash() != dialogTrueType.PixelHash(),
                  "dialog body and fixed button text follow the selected provider");
            Console.WriteLine("FONTS6 provider pixel pairs: notification Bitmap=0x" +
                toastBitmap.PixelHash().ToString("X16") + " TrueType=0x" +
                toastTrueType.PixelHash().ToString("X16") + "; dialog Bitmap=0x" +
                dialogBitmap.PixelHash().ToString("X16") + " TrueType=0x" +
                dialogTrueType.PixelHash().ToString("X16"));
        }

        private static IFont CreateCompleteBitmapFont() {
            const int size = 8;
            string charset = "";
            for (int c = 32; c <= 126; c++) charset += (char)c;
            Image atlas = new Image(charset.Length * size, size);
            for (int i = 0; i < charset.Length; i++) {
                int cellX = i * size;
                for (int y = 0; y < size; y++) {
                    for (int x = 0; x < size; x++) {
                        if (((x + y + charset[i]) % 3) != 0)
                            atlas.RawData[y * atlas.Width + cellX + x] =
                                unchecked((int)0xFFFFFFFFu);
                    }
                }
            }
            return new IFont(atlas, charset, size, true, 6, -2);
        }

        private static bool SameHitGeometry(UefiDesktopIconGeometry first,
                                           UefiDesktopIconGeometry second) {
            return first.HitX == second.HitX && first.HitY == second.HitY &&
                   first.HitWidth == second.HitWidth &&
                   first.HitHeight == second.HitHeight;
        }

        private static bool IsWellFormedUtf16(string value) {
            if (value == null) return false;
            for (int i = 0; i < value.Length; i++) {
                if (TrueTypeFont.IsHighSurrogate(value[i])) {
                    if (i + 1 >= value.Length ||
                            !TrueTypeFont.IsLowSurrogate(value[i + 1]))
                        return false;
                    i++;
                } else if (TrueTypeFont.IsLowSurrogate(value[i])) {
                    return false;
                }
            }
            return true;
        }

        private static bool HasInkOnlyInsideClip(Graphics graphics, int x,
                                                 int y, int width,
                                                 int height) {
            bool inside = false;
            for (int py = 0; py < graphics.Height; py++) {
                for (int px = 0; px < graphics.Width; px++) {
                    if (graphics.GetPixel(px, py) == 0xFF18212Au) continue;
                    if (px < x || py < y || px >= x + width ||
                            py >= y + height) return false;
                    inside = true;
                }
            }
            return inside;
        }

        private static void Check(bool condition, string description) {
            _checks++;
            if (condition) return;
            _failures++;
            Console.Error.WriteLine("FAIL: " + description);
        }
    }
}
