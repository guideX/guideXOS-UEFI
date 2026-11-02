using System;
using System.Drawing;
using guideXOS.Graph;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;

namespace guideXOS.FontTests {
    internal static class SharedTextFontChecks {
        private static int _checks;
        private static int _failures;

        public static int Run(byte[] fontBytes) {
            _checks = 0;
            _failures = 0;
            Image atlasImage;
            IFont bitmap = CreateBitmapFont(out atlasImage);
            Check(bitmap.IsValid, "bitmap fixture is a valid atlas provider");

            SharedTextFont defaultFont = SharedTextFont.CreateDefaultBitmap(bitmap);
            Check(defaultFont != null && defaultFont.IsAvailable &&
                  defaultFont.Provider == SharedFontProviderKind.BitmapAtlas,
                  "default provider resolves to the existing bitmap atlas");
            Check(SharedTextFont.CreateDefaultBitmap(null) == null,
                  "missing default bitmap produces an unavailable handle");

            CheckBitmapCompatibility(bitmap, defaultFont);
            CheckBitmapClipping(bitmap, defaultFont);
            CheckTrueTypeMetricsAndSelection(fontBytes, bitmap);
            CheckTrueTypeRendering(fontBytes, bitmap);
            CheckFallback(bitmap);
            CheckDesktopTileLayout(bitmap, defaultFont, fontBytes);
            int shellTextFailures = ShellTextIntegrationChecks.Run(fontBytes);

            Console.WriteLine("FONTS2 hosted checks: " + _checks +
                              ", failures: " + _failures);
            return _failures + shellTextFailures;
        }

        private static IFont CreateBitmapFont(out Image image) {
            const int size = 8;
            string charset = "FILES DOCS IMAGES AUDIO?";
            image = new Image(charset.Length * size, size);
            for (int i = 0; i < charset.Length; i++) {
                if (charset[i] == ' ') continue;
                int cellX = i * size;
                for (int y = 0; y < size; y++) {
                    for (int x = 0; x < size; x++) {
                        if (((x + y + i) % 3) != 0)
                            image.RawData[y * image.Width + cellX + x] =
                                unchecked((int)0xFFFFFFFFu);
                    }
                }
            }
            return new IFont(image, charset, size, true, 6, -2);
        }

        private static void CheckBitmapCompatibility(IFont bitmap,
                                                     SharedTextFont shared) {
            const string label = "FILES";
            SharedTextMeasurement measured;
            Check(shared.MeasureText(label, out measured) &&
                  measured.Width == bitmap.MeasureString(label),
                  "bitmap measurement preserves atlas ink bounds and advances");

            SharedFontMetrics metrics;
            Check(shared.GetFontMetrics(out metrics) &&
                  metrics.LineHeight == bitmap.FontSize &&
                  metrics.Baseline == bitmap.FontSize && metrics.Descent == 0,
                  "bitmap top-left baseline and line-height remain atlas-sized");
            int bitmapLineHeight, bitmapBaseline;
            Check(shared.GetLineHeight(out bitmapLineHeight) &&
                  shared.GetBaseline(out bitmapBaseline) &&
                  bitmapLineHeight == metrics.LineHeight &&
                  bitmapBaseline == metrics.Baseline,
                  "bitmap line-height and baseline accessors agree with metrics");

            UefiDesktopIconGeometry geometry = UefiDesktopIconGeometry.Create(
                0, 40, 20, 30, measured.Width, metrics.LineHeight, 4);
            Graphics legacy = new Graphics(120, 80, 0xFF102030u);
            Graphics throughShared = new Graphics(120, 80, 0xFF102030u);
            bitmap.DrawString(geometry.LabelX, geometry.LabelY, label, legacy);
            bool drawn = UefiDesktopLabelRenderer.Draw(throughShared, shared,
                geometry, label);
            Check(drawn && legacy.PixelHash() == throughShared.PixelHash(),
                  "default tile label pixels match the direct bitmap path");

            int advance;
            Check(shared.GetGlyphAdvance('F', out advance) &&
                  advance == bitmap.GetGlyphAdvance('F'),
                  "bitmap glyph advance matches the atlas pen step");
            Check(shared.GetGlyphAdvance('~', out advance) && advance == 0,
                  "unsupported atlas characters preserve their zero-width fallback");

            SharedTextMeasurement lines;
            Check(shared.MeasureText("FILES\nDOCS", out lines) &&
                  lines.LineCount == 2 && lines.Height == metrics.LineHeight * 2 &&
                  lines.Width == Math.Max(bitmap.MeasureString("FILES"),
                                          bitmap.MeasureString("DOCS")),
                  "bitmap multi-line measurement reports max width and line height");
        }

        private static void CheckBitmapClipping(IFont bitmap,
                                                SharedTextFont shared) {
            const uint background = 0xFF102030u;
            const string multiLine = "MMMMMMMMMMMM\nMMMMMMMMMMMM\n" +
                                     "MMMMMMMMMMMM\nMMMMMMMMMMMM";
            Graphics direct = new Graphics(100, 70, background);
            Graphics clipped = new Graphics(100, 70, background);
            bitmap.DrawStringClipped(4, 10, multiLine, direct,
                30, 20, 35, 25);
            bool drawn = shared.DrawText(clipped, 4, 10, multiLine,
                0xFFFFFFFFu, 30, 20, 35, 25);
            bool changedInsideClip = false;
            bool changedOutsideClip = false;
            for (int y = 0; y < clipped.Height; y++) {
                for (int x = 0; x < clipped.Width; x++) {
                    bool inClip = x >= 30 && x < 65 && y >= 20 && y < 45;
                    bool changed = clipped.GetPixel(x, y) != background;
                    if (inClip && changed) changedInsideClip = true;
                    if (!inClip && changed) changedOutsideClip = true;
                }
            }
            Check(drawn && changedInsideClip && !changedOutsideClip &&
                  clipped.PixelHash() == direct.PixelHash(),
                  "bitmap clip edges match direct atlas rendering at all four bounds");
        }

        private static void CheckTrueTypeMetricsAndSelection(byte[] fontBytes,
                                                              IFont bitmap) {
            int[] sizes = { 12, 16, 20, 24, 32 };
            for (int i = 0; i < sizes.Length; i++) {
                TrueTypeFont renderer;
                bool loaded = TrueTypeFont.TryCreate(fontBytes, sizes[i], out renderer);
                SharedTextFont selected;
                bool usedTrueType = SharedTextFont.TrySelectTrueType(renderer,
                    bitmap, out selected);
                Check(loaded && usedTrueType && selected != null &&
                      selected.Provider == SharedFontProviderKind.TrueType &&
                      selected.FontSize == sizes[i],
                      "explicit TrueType selection accepts " + sizes[i] + " px");
                if (selected == null || renderer == null || !renderer.IsValid) continue;

                SharedFontMetrics metrics;
                Check(selected.GetFontMetrics(out metrics) &&
                      metrics.Ascent > 0 && metrics.Descent < 0 &&
                      metrics.Baseline == metrics.Ascent &&
                      metrics.LineHeight == renderer.LineHeight,
                      "TrueType signed ascent/descent and baseline at " + sizes[i] + " px");
                int lineHeight, baseline;
                Check(selected.GetLineHeight(out lineHeight) &&
                      selected.GetBaseline(out baseline) &&
                      lineHeight == metrics.LineHeight &&
                      baseline == metrics.Baseline,
                      "TrueType line-height and baseline accessors at " + sizes[i] + " px");
                Check(selected.FontSize == renderer.FontSize,
                      "requested TrueType size is retained at " + sizes[i] + " px");

                int advance;
                Check(selected.GetGlyphAdvance('R', out advance) &&
                      advance == renderer.GetAdvanceWidth('R'),
                      "TrueType glyph advance matches hmtx at " + sizes[i] + " px");

                SharedTextMeasurement empty;
                Check(selected.MeasureText(string.Empty, out empty) &&
                      empty.Width == 0 && empty.Height == metrics.LineHeight &&
                      empty.LineCount == 1,
                      "empty TrueType text has a safe empty line at " + sizes[i] + " px");

                SharedTextMeasurement sample;
                string text = "R Roboto";
                Check(selected.MeasureText(text, out sample) &&
                      sample.Width == renderer.MeasureString(text) &&
                      sample.Height == metrics.LineHeight,
                      "shared TrueType measurement uses renderer advances at " +
                      sizes[i] + " px");

                SharedTextMeasurement multiple;
                Check(selected.MeasureText("R\r\nRoboto", out multiple) &&
                      multiple.LineCount == 2 &&
                      multiple.Width == renderer.MeasureString("Roboto") &&
                      multiple.Height == metrics.LineHeight * 2,
                      "CRLF metrics use the selected TrueType line height at " +
                      sizes[i] + " px");
            }

            TrueTypeFont unsupportedSize = new TrueTypeFont(fontBytes, 13);
            SharedTextFont rejected;
            Check(!SharedTextFont.TrySelectTrueType(unsupportedSize, bitmap,
                    out rejected) && rejected != null &&
                  rejected.Provider == SharedFontProviderKind.BitmapAtlas,
                  "unqualified requested size selects the bitmap fallback");
        }

        private static void CheckTrueTypeRendering(byte[] fontBytes, IFont bitmap) {
            TrueTypeFont font12 = new TrueTypeFont(fontBytes, 12);
            TrueTypeFont font20 = new TrueTypeFont(fontBytes, 20);
            TrueTypeFont font32 = new TrueTypeFont(fontBytes, 32);
            SharedTextFont shared12;
            SharedTextFont shared20;
            SharedTextFont shared32;
            bool selected12 = SharedTextFont.TrySelectTrueType(font12, bitmap, out shared12);
            bool selected20 = SharedTextFont.TrySelectTrueType(font20, bitmap, out shared20);
            bool selected32 = SharedTextFont.TrySelectTrueType(font32, bitmap, out shared32);
            Check(selected12 && selected20 && selected32,
                  "hosted surface fixture explicitly opts into TrueType");
            if (!selected12 || !selected20 || !selected32) return;

            const uint background = 0xFF102030u;
            const string sample = "AaZz 0123456789 !@#$%&*()?,. café Ω £ \U0001F600";
            Graphics direct = new Graphics(320, 96, background);
            Graphics shared = new Graphics(320, 96, background);
            font12.DrawString(direct, 4, 4, sample, 0xFFF5F5F5u);
            font20.DrawString(direct, 4, 24, "Roboto: The quick brown fox", 0xFFFFFFFFu);
            font32.DrawString(direct, 4, 52, "TTF", 0xFFFFFFFFu);
            SharedTextMeasurement measured;
            bool measuredOk = shared12.MeasureText(sample, out measured);
            bool draw12 = shared12.DrawText(shared, 4, 4, sample,
                0xFFF5F5F5u, 0, 0, shared.Width, shared.Height);
            bool draw20 = shared20.DrawText(shared, 4, 24,
                "Roboto: The quick brown fox", 0xFFFFFFFFu,
                0, 0, shared.Width, shared.Height);
            bool draw32 = shared32.DrawText(shared, 4, 52, "TTF",
                0xFFFFFFFFu, 0, 0, shared.Width, shared.Height);
            Check(measuredOk && measured.Width == font12.MeasureString(sample) &&
                  draw12 && draw20 && draw32,
                  "shared TrueType draw and measure use the qualified renderer metrics");
            Check(shared.PixelHash() == direct.PixelHash() &&
                  shared.PixelHash() == 0xEC1A69C86C9C5CC6UL,
                  "shared multi-size TrueType output preserves the FONTS1 golden hash");

            Graphics repeat = new Graphics(320, 96, background);
            shared12.DrawText(repeat, 4, 4, sample, 0xFFF5F5F5u,
                0, 0, repeat.Width, repeat.Height);
            shared20.DrawText(repeat, 4, 24, "Roboto: The quick brown fox",
                0xFFFFFFFFu, 0, 0, repeat.Width, repeat.Height);
            shared32.DrawText(repeat, 4, 52, "TTF", 0xFFFFFFFFu,
                0, 0, repeat.Width, repeat.Height);
            Check(repeat.PixelHash() == shared.PixelHash(),
                  "shared TrueType repeated rendering is deterministic");

            int tileLabelWidth, tileLabelHeight;
            UefiDesktopLabelRenderer.TryMeasure(shared12, "FILES",
                out tileLabelWidth, out tileLabelHeight);
            UefiDesktopIconGeometry tile = UefiDesktopIconGeometry.Create(
                2, 54, 8, 30, tileLabelWidth, tileLabelHeight, 4);
            Graphics tilePixels = new Graphics(180, 80, background);
            bool tileDrawn = UefiDesktopLabelRenderer.Draw(tilePixels,
                shared12, tile, "FILES");
            UefiDesktopIconGeometry[] tiles = { tile };
            Check(tileDrawn && UefiDesktopIconGeometry.HitTest(tiles,
                  tile.LabelX, tile.LabelY) == tile.IconId,
                  "hosted TrueType desktop label renders inside measured hit geometry");
            Check(UefiDesktopIconGeometry.HitTest(tiles,
                  tile.HitX - 1, tile.LabelY) == -1,
                  "TrueType label geometry keeps its measured horizontal hit bound");

            Graphics clipped = new Graphics(100, 70, background);
            string clippedText = "MMMMMMMMMMMM\nMMMMMMMMMMMM\n" +
                                 "MMMMMMMMMMMM\nMMMMMMMMMMMM";
            bool clippedOk = shared12.DrawText(clipped, 4, 10,
                clippedText, 0xFFFFFFFFu, 30, 20, 35, 25);
            bool changedInsideClip = false;
            bool changedOutsideClip = false;
            for (int y = 0; y < clipped.Height; y++) {
                for (int x = 0; x < clipped.Width; x++) {
                    bool inClip = x >= 30 && x < 65 && y >= 20 && y < 45;
                    bool changed = clipped.GetPixel(x, y) != background;
                    if (inClip && changed) changedInsideClip = true;
                    if (!inClip && changed) changedOutsideClip = true;
                }
            }
            Check(clippedOk && changedInsideClip && !changedOutsideClip,
                  "TrueType clipping bounds all four edges of the text surface");

            Graphics negative = new Graphics(50, 35, background);
            bool negativeOk = shared12.DrawText(negative, -5, -4,
                "A partially offscreen", 0xFFFFFFFFu, 0, 0,
                negative.Width, negative.Height);
            Check(negativeOk && negative.InvalidDrawAttempts == 0 &&
                  negative.ChangedPixelCount > 0,
                  "negative text origin clips safely to the viewport");

            string longText = new string('W', 2000);
            SharedTextMeasurement longMeasurement;
            bool longMeasured = shared12.MeasureText(longText, out longMeasurement);
            Graphics longDraw = new Graphics(80, 40, background);
            bool longDrawn = shared12.DrawText(longDraw, 0, 0, longText,
                0xFFFFFFFFu, 0, 0, longDraw.Width, longDraw.Height);
            Check(longMeasured && longMeasurement.Width > 0 && longDrawn &&
                  longDraw.InvalidDrawAttempts == 0,
                  "long TrueType text remains bounded by destination clipping");

            int rasterizations = font12.Rasterizations;
            for (int i = 0; i < 4; i++)
                shared12.DrawText(repeat, 4, 4, sample, 0xFFF5F5F5u,
                    0, 0, repeat.Width, repeat.Height);
            Check(font12.Rasterizations == rasterizations && font12.CacheHits > 0,
                  "shared repeated draws reuse the existing bounded glyph cache");

            SharedTextMeasurement width;
            shared12.MeasureText("FILES", out width);
            int boxWidth = 100;
            int centeredX = 10 + (boxWidth - width.Width) / 2;
            Graphics centered = new Graphics(140, 50, background);
            Graphics expectedCenter = new Graphics(140, 50, background);
            shared12.DrawText(centered, 10, 5, "FILES", 0xFFFFFFFFu,
                0, 0, centered.Width, centered.Height,
                SharedTextAlignment.Center, boxWidth);
            shared12.DrawText(expectedCenter, centeredX, 5, "FILES",
                0xFFFFFFFFu, 0, 0, expectedCenter.Width, expectedCenter.Height);
            Check(centered.PixelHash() == expectedCenter.PixelHash(),
                  "center alignment matches the measured text box origin");

            Graphics rightAligned = new Graphics(140, 50, background);
            Graphics expectedRight = new Graphics(140, 50, background);
            shared12.DrawText(rightAligned, 10, 5, "FILES", 0xFFFFFFFFu,
                0, 0, rightAligned.Width, rightAligned.Height,
                SharedTextAlignment.Right, boxWidth);
            shared12.DrawText(expectedRight, 10 + boxWidth - width.Width, 5,
                "FILES", 0xFFFFFFFFu, 0, 0,
                expectedRight.Width, expectedRight.Height);
            Check(rightAligned.PixelHash() == expectedRight.PixelHash(),
                  "right alignment matches the measured text box origin");
        }

        private static void CheckFallback(IFont bitmap) {
            TrueTypeFont invalid = new TrueTypeFont(new byte[] { 0, 1, 2, 3 }, 16);
            SharedTextFont selected;
            bool result = SharedTextFont.TrySelectTrueType(invalid, bitmap,
                out selected);
            SharedTextMeasurement measurement;
            Check(!result && selected != null &&
                  selected.Provider == SharedFontProviderKind.BitmapAtlas &&
                  selected.MeasureText("FILES", out measurement) &&
                  measurement.Width == bitmap.MeasureString("FILES"),
                  "invalid TrueType resources fall back once to bitmap metrics");

            SharedTextFont noFallback;
            Check(!SharedTextFont.TrySelectTrueType(null, null, out noFallback) &&
                  noFallback == null,
                  "invalid font handle without a bitmap fails safely");
            SharedTextFont unavailable = SharedTextFont.CreateDefaultBitmap(
                new IFont(null, "", 20));
            SharedFontMetrics unavailableMetrics;
            SharedTextMeasurement unavailableMeasurement;
            Check(unavailable != null && !unavailable.IsAvailable &&
                  !unavailable.GetFontMetrics(out unavailableMetrics) &&
                  !unavailable.MeasureText("FILES", out unavailableMeasurement),
                  "unavailable bitmap handles report metric and measure failure");

            TrueTypeFont trueType = new TrueTypeFont(
                System.IO.File.ReadAllBytes(System.IO.Path.Combine(
                    AppContext.BaseDirectory, "Roboto-Regular.ttf")), 16);
            SharedTextFont bitmapAgain = SharedTextFont.CreateDefaultBitmap(bitmap);
            SharedTextMeasurement bitmapBefore;
            SharedTextMeasurement bitmapAfter;
            bitmapAgain.MeasureText("FILES", out bitmapBefore);
            SharedTextFont.TrySelectTrueType(trueType, bitmap, out selected);
            SharedTextMeasurement ttfMeasure;
            selected.MeasureText("FILES", out ttfMeasure);
            bitmapAgain.MeasureText("FILES", out bitmapAfter);
            Check(bitmapBefore.Width == bitmapAfter.Width &&
                  bitmapAgain.Provider == SharedFontProviderKind.BitmapAtlas &&
                  selected.Provider == SharedFontProviderKind.TrueType,
                  "provider changes use separate handles with no stale measurements");
        }

        private static void CheckDesktopTileLayout(IFont bitmap,
                                                   SharedTextFont shared,
                                                   byte[] fontBytes) {
            int measuredWidth, measuredHeight;
            bool valid = UefiDesktopLabelRenderer.TryMeasure(shared, "FILES",
                out measuredWidth, out measuredHeight);
            UefiDesktopIconGeometry geometry = UefiDesktopIconGeometry.Create(
                0, 48, 32, 30, valid ? measuredWidth : 0,
                valid ? measuredHeight : 0, 4);
            int oldBitmapWidth = bitmap.MeasureString("FILES");
            UefiDesktopIconGeometry legacy = UefiDesktopIconGeometry.Create(
                0, 48, 32, 30, oldBitmapWidth, bitmap.FontSize, 4);
            Check(valid && geometry.LabelX == legacy.LabelX &&
                  geometry.LabelY == legacy.LabelY &&
                  geometry.HitX == legacy.HitX &&
                  geometry.HitY == legacy.HitY &&
                  geometry.HitWidth == legacy.HitWidth &&
                  geometry.HitHeight == legacy.HitHeight,
                  "default bitmap desktop tile layout and hit rectangle are unchanged");

            TrueTypeFont ttf = new TrueTypeFont(fontBytes, 20);
            SharedTextFont optIn;
            bool selected = SharedTextFont.TrySelectTrueType(ttf, bitmap, out optIn);
            int ttfWidth = 0, ttfHeight = 0;
            bool ttfLayout = selected && UefiDesktopLabelRenderer.TryMeasure(
                optIn, "FILES", out ttfWidth, out ttfHeight);
            UefiDesktopIconGeometry ttfGeometry = UefiDesktopIconGeometry.Create(
                0, 48, 32, 30, ttfLayout ? ttfWidth : 0,
                ttfLayout ? ttfHeight : 0, 4);
            Check(ttfLayout && ttfGeometry.LabelWidth == ttfWidth &&
                  ttfGeometry.LabelHeight == ttfHeight &&
                  ttfGeometry.HitHeight == ttfGeometry.TileSize + 4 +
                                           ttfHeight,
                  "TrueType opt-in layout derives label and hit bounds from its own metrics");

            string[] labels = { "DOCS", "IMAGES", "AUDIO" };
            for (int i = 0; i < labels.Length; i++) {
                int labelWidth, labelHeight;
                bool labelMeasured = UefiDesktopLabelRenderer.TryMeasure(
                    optIn, labels[i], out labelWidth, out labelHeight);
                UefiDesktopIconGeometry labelGeometry =
                    UefiDesktopIconGeometry.Create(i + 1, 48, 32, 30,
                        labelMeasured ? labelWidth : 0,
                        labelMeasured ? labelHeight : 0, 4);
                Graphics labelPixels = new Graphics(200, 100, 0xFF102030u);
                bool labelDrawn = UefiDesktopLabelRenderer.Draw(labelPixels,
                    optIn, labelGeometry, labels[i]);
                UefiDesktopIconGeometry[] labelTiles = { labelGeometry };
                bool insideHit = UefiDesktopIconGeometry.HitTest(labelTiles,
                    labelGeometry.LabelX, labelGeometry.LabelY) == i + 1;
                bool outsideHit = labelGeometry.LabelWidth > labelGeometry.TileSize &&
                    UefiDesktopIconGeometry.HitTest(labelTiles,
                        labelGeometry.HitX - 1, labelGeometry.LabelY) == -1;
                bool inkOutsideLabel = false;
                for (int y = 0; y < labelPixels.Height; y++) {
                    for (int x = 0; x < labelPixels.Width; x++) {
                        if ((x < labelGeometry.LabelX ||
                             x >= labelGeometry.LabelX + labelGeometry.LabelWidth ||
                             y < labelGeometry.LabelY ||
                             y >= labelGeometry.LabelY + labelGeometry.LabelHeight) &&
                            labelPixels.GetPixel(x, y) != 0xFF102030u) {
                            inkOutsideLabel = true;
                        }
                    }
                }
                Check(labelMeasured && labelDrawn && insideHit && outsideHit &&
                      !inkOutsideLabel,
                      "TrueType tile layout clips and hit-tests label length " +
                      labels[i].Length);
            }
        }

        private static void Check(bool condition, string description) {
            _checks++;
            if (!condition) {
                _failures++;
                Console.Error.WriteLine("FONTS2 FAIL: " + description);
            }
        }
    }
}
