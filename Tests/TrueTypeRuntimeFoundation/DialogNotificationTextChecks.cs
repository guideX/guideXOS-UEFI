using System;
using System.Drawing;
using guideXOS.Graph;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;

namespace guideXOS.FontTests {
    internal static class DialogNotificationTextChecks {
        private static int _checks;
        private static int _failures;

        public static int Run(byte[] fontBytes) {
            _checks = 0;
            _failures = 0;
            IFont bitmap = CreateProportionalBitmap();
            SharedTextFont bitmapFont = SharedTextFont.CreateDefaultBitmap(bitmap);
            Check(bitmapFont != null && bitmapFont.IsAvailable,
                  "FONTS4 bitmap provider is available");

            CheckWrappingAndParagraphs(fontBytes, bitmapFont);
            CheckBoundsAndUnicode(fontBytes, bitmapFont);
            CheckProvidersAndSizes(fontBytes, bitmap, bitmapFont);
            CheckNotificationAndDialogPixels(bitmap, bitmapFont, fontBytes);

            Console.WriteLine("FONTS4 hosted layout and UI checks: " +
                              _checks + ", failures: " + _failures);
            return _failures;
        }

        private static IFont CreateProportionalBitmap() {
            const int size = 8;
            string charset = "";
            for (int code = 32; code <= 126; code++) charset += (char)code;
            Image atlas = new Image(charset.Length * size, size);
            for (int i = 0; i < charset.Length; i++) {
                char ch = charset[i];
                int inkWidth = ch == ' ' ? 0 :
                    ch == 'i' || ch == 'l' ? 2 : ch == 'W' ? 7 : 4;
                for (int y = 1; y < size - 1; y++) {
                    for (int x = 0; x < inkWidth; x++) {
                        atlas.RawData[y * atlas.Width + i * size + x] =
                            unchecked((int)0xFFFFFFFFu);
                    }
                }
            }
            return new IFont(atlas, charset, size, false, 0, 0);
        }

        private static void CheckWrappingAndParagraphs(byte[] fontBytes,
                                                       SharedTextFont bitmap) {
            TrueTypeFont renderer;
            SharedTextFont trueType;
            bool loaded = TrueTypeFont.TryCreate(fontBytes, 16, out renderer);
            bool selected = SharedTextFont.TrySelectTrueType(renderer, null,
                                                               out trueType);
            Check(loaded && selected && trueType != null,
                  "FONTS4 TrueType provider loads for layout checks");
            if (!selected || trueType == null) return;

            int narrowAdvance;
            int wideAdvance;
            trueType.GetGlyphAdvance('i', out narrowAdvance);
            trueType.GetGlyphAdvance('W', out wideAdvance);
            Check(wideAdvance > narrowAdvance,
                  "layout uses proportional provider glyph advances");

            SharedTextLayout layout = new SharedTextLayout();
            SharedTextMeasurement wordMeasure;
            trueType.MeasureText("abc", out wordMeasure);
            bool wrapped = layout.TryLayout(trueType, "abc def", wordMeasure.Width,
                                             0);
            Check(wrapped && layout.LineCount == 2 &&
                  Slice("abc def", layout, 0) == "abc" &&
                  Slice("abc def", layout, 1) == "def" &&
                  layout.GetLineWidth(0) <= wordMeasure.Width &&
                  layout.GetLineWidth(1) <= wordMeasure.Width,
                  "word wrapping uses measured advances and breaks at spaces");

            const string paragraphs = "A\n\nB\r\nC";
            bool paragraphsLaidOut = layout.TryLayout(trueType, paragraphs,
                                                        200, 3);
            Check(paragraphsLaidOut && layout.LineCount == 4 &&
                  Slice(paragraphs, layout, 0) == "A" &&
                  Slice(paragraphs, layout, 1) == "" &&
                  Slice(paragraphs, layout, 2) == "B" &&
                  Slice(paragraphs, layout, 3) == "C" &&
                  layout.Height == layout.LineCount * layout.LineHeight + 9,
                  "multiple paragraphs, CRLF, empty lines, and line spacing are measured");

            string longWord = new string('W', 1200);
            bool longWordLaidOut = layout.TryLayout(trueType, longWord, 44, 0);
            Check(longWordLaidOut && layout.WasTruncated &&
                  layout.LineCount <= SharedTextLayout.MaximumLines &&
                  layout.LineCount > 1,
                  "long unbroken text splits safely within line and work bounds");

            string boundedText = new string('i',
                SharedTextLayout.MaximumInputScalars - 1) +
                "\U0001F600Z";
            bool boundedInput = layout.TryLayout(trueType, boundedText,
                int.MaxValue, 0);
            Check(boundedInput && layout.WasTruncated &&
                  layout.LineCount == 1 &&
                  layout.GetLineLength(0) ==
                      SharedTextLayout.MaximumInputScalars + 1 &&
                  char.IsHighSurrogate(boundedText[
                      layout.GetLineStart(0) + layout.GetLineLength(0) - 2]) &&
                  char.IsLowSurrogate(boundedText[
                      layout.GetLineStart(0) + layout.GetLineLength(0) - 1]),
                  "input work is capped without cutting a supplementary scalar");

            bool bitmapWrapped = layout.TryLayout(bitmap, "WWWWWW", 11, 0);
            Check(bitmapWrapped && layout.LineCount > 1 &&
                  layout.Width <= 14,
                  "bitmap wrapping uses the atlas's measured cell and advance widths");
        }

        private static void CheckBoundsAndUnicode(byte[] fontBytes,
                                                   SharedTextFont bitmap) {
            TrueTypeFont renderer;
            SharedTextFont trueType;
            TrueTypeFont.TryCreate(fontBytes, 16, out renderer);
            SharedTextFont.TrySelectTrueType(renderer, bitmap == null ? null :
                CreateBitmapFontForFallback(), out trueType);
            if (trueType == null ||
                trueType.Provider != SharedFontProviderKind.TrueType) {
                // The helper selection above is intentionally retried with the
                // fixture provider below if the TrueType parse is unavailable.
                SharedTextFont.TrySelectTrueType(renderer,
                    CreateBitmapFontForFallback(), out trueType);
            }

            SharedTextLayout layout = new SharedTextLayout();
            const string scalarText = "\U0001F600A";
            bool narrow = trueType != null && layout.TryLayout(trueType,
                scalarText, 1, 0);
            Check(narrow && layout.LineCount == 2 &&
                  layout.GetLineStart(0) == 0 &&
                  layout.GetLineLength(0) == 2 &&
                  layout.GetLineLength(1) == 1,
                  "narrow wrapping keeps a supplementary Unicode scalar intact");

            bool emptyRect = trueType != null && layout.TryLayout(trueType,
                scalarText, 0, 0);
            Check(emptyRect && layout.LineCount == 0 && layout.WasTruncated,
                  "zero-width layout is bounded and reports clipped content");

            bool emptyText = trueType != null && layout.TryLayout(trueType,
                string.Empty, 10, 0);
            Check(emptyText && layout.LineCount == 1 && layout.Width == 0 &&
                  layout.Height > 0,
                  "empty text has a deterministic empty line box");

            SharedTextMeasurement missingGlyph = new SharedTextMeasurement();
            bool missingGlyphMeasured = trueType != null &&
                trueType.MeasureText("Ω\U0001F600", out missingGlyph);
            Check(missingGlyphMeasured && missingGlyph.Width > 0,
                  "missing and supplementary glyphs use the provider fallback");

            bool bitmapMissingGlyph = bitmap != null &&
                layout.TryLayout(bitmap, "Ω", 20, 0) &&
                layout.LineCount == 1 && layout.Width == 0;
            Check(bitmapMissingGlyph,
                  "bitmap atlas missing glyphs retain their zero-width fallback behavior");

            Check(!layout.TryLayout(null, "text", 20, 0),
                  "a missing font handle is rejected without stale layout metrics");

            if (trueType == null) return;
            const uint background = 0xFF14202Au;
            Graphics partiallyClipped = new Graphics(80, 50, background);
            layout.TryLayout(trueType, "A partly clipped dialog message", 120, 0);
            bool clippedDraw = layout.Draw(partiallyClipped, -8, -6,
                0xFFFFFFFFu, -4, -3, 50, 28,
                SharedTextAlignment.Left);
            Check(clippedDraw && HasInkOnlyInsideClip(partiallyClipped,
                  background, -4, -3, 50, 28) &&
                  partiallyClipped.InvalidDrawAttempts == 0,
                  "partial offscreen clipping stays inside the supplied rectangle");

            Graphics emptyClip = new Graphics(40, 30, background);
            ulong emptyBefore = emptyClip.PixelHash();
            layout.TryLayout(trueType, "visible nowhere", 100, 0);
            bool emptyClipDraw = layout.Draw(emptyClip, 2, 2, 0xFFFFFFFFu,
                4, 4, 0, 20, SharedTextAlignment.Left);
            Check(emptyClipDraw && emptyClip.PixelHash() == emptyBefore &&
                  emptyClip.InvalidDrawAttempts == 0,
                  "empty clip rectangles are safe no-ops");
        }

        private static void CheckProvidersAndSizes(byte[] fontBytes,
                                                    IFont bitmap,
                                                    SharedTextFont bitmapFont) {
            int[] sizes = { 12, 16, 20, 24 };
            for (int i = 0; i < sizes.Length; i++) {
                TrueTypeFont renderer;
                SharedTextFont selected;
                bool loaded = TrueTypeFont.TryCreate(fontBytes, sizes[i],
                                                      out renderer);
                bool available = SharedTextFont.TrySelectTrueType(renderer,
                    bitmap, out selected);
                SharedTextLayout layout = new SharedTextLayout();
                bool laidOut = available && layout.TryLayout(selected,
                    "Sizing at " + sizes[i] + " pixels", 110, 1);
                Check(loaded && available && laidOut &&
                      layout.LineHeight > 0 && layout.Height >= layout.LineHeight,
                      "layout metrics are available at " + sizes[i] + " px");
            }

            SharedTextFontSelection selection = new SharedTextFontSelection();
            SharedTextFont current = selection.Resolve(bitmap);
            SharedTextLayout reusable = new SharedTextLayout();
            const string sample = "iiiiiiii";
            bool bitmapLayout = reusable.TryLayout(current, sample, 200, 0);
            int bitmapWidth = reusable.Width;
            int bitmapGeneration = selection.Generation;

            TrueTypeFont trueTypeRenderer;
            TrueTypeFont.TryCreate(fontBytes, 16, out trueTypeRenderer);
            SharedTextFont selectedTrueType;
            bool optedIn = selection.TrySelectTrueType(trueTypeRenderer,
                bitmap, out selectedTrueType);
            bool trueTypeLayout = optedIn && reusable.TryLayout(selectedTrueType,
                sample, 200, 0);
            int trueTypeWidth = reusable.Width;
            Check(bitmapLayout && trueTypeLayout &&
                  selection.Generation > bitmapGeneration &&
                  bitmapWidth != trueTypeWidth,
                  "provider changes replace stored measurement metrics");

            int warmedRasterizations = trueTypeRenderer == null ? 0 :
                trueTypeRenderer.Rasterizations;
            bool repeated = reusable.TryLayout(selectedTrueType, sample, 200, 0);
            bool deterministic = repeated && reusable.Width == trueTypeWidth;
            if (trueTypeRenderer != null) {
                deterministic &= trueTypeRenderer.Rasterizations ==
                    warmedRasterizations && trueTypeRenderer.CacheBytes <=
                    TrueTypeFont.MaximumCacheBytes;
            }
            Check(deterministic,
                  "repeated layout is deterministic and reuses the bounded glyph cache");

            selection.SelectBitmap(bitmap);
            bool fallbackLayout = reusable.TryLayout(selection.Resolve(bitmap),
                sample, 200, 0);
            Check(fallbackLayout && reusable.Width == bitmapWidth &&
                  selection.Generation > bitmapGeneration,
                  "switching back to bitmap clears TrueType-specific measurements");

            SharedTextFont missingFont;
            bool missingSelected = SharedTextFont.TrySelectTrueType(null,
                bitmap, out missingFont);
            Check(!missingSelected && missingFont != null &&
                  missingFont.Provider == SharedFontProviderKind.BitmapAtlas &&
                  reusable.TryLayout(missingFont, "fallback", 100, 0),
                  "unavailable TrueType assets select the bitmap fallback");
        }

        private static void CheckNotificationAndDialogPixels(IFont bitmap,
                                                              SharedTextFont bitmapFont,
                                                              byte[] fontBytes) {
            const uint background = 0xFF19232Du;
            const string notification = "A notification";
            int notificationWidth = bitmap.MeasureString(notification);
            int notificationHeight = bitmap.FontSize;
            Graphics legacyToast = new Graphics(220, 90, background);
            Graphics sharedToast = new Graphics(220, 90, background);
            DrawToastFrame(legacyToast, 80, 15, notificationWidth,
                           notificationHeight);
            DrawToastFrame(sharedToast, 80, 15, notificationWidth,
                           notificationHeight);
            bitmap.DrawString(95, 30, notification, legacyToast);
            bool bitmapNotification = ShellTextSurfaceRenderer.DrawWrappedText(
                sharedToast, bitmapFont, notification, 95, 30,
                notificationWidth, 0, 0xFFFFFFFFu, 95, 30,
                notificationWidth, notificationHeight,
                SharedTextAlignment.Left);
            Check(bitmapNotification && legacyToast.PixelHash() ==
                  sharedToast.PixelHash(),
                  "bitmap notification pixels preserve the legacy toast bounds");

            int messageWidth = bitmap.MeasureString("Dialog body");
            Graphics legacyDialog = new Graphics(180, 80, background);
            Graphics sharedDialog = new Graphics(180, 80, background);
            bitmap.DrawString(12, 10, "Dialog body", legacyDialog);
            bool bitmapDialog = ShellTextSurfaceRenderer.DrawWrappedText(
                sharedDialog, bitmapFont, "Dialog body", 12, 10,
                messageWidth, 0, 0xFFFFFFFFu, 12, 10, messageWidth,
                bitmap.FontSize, SharedTextAlignment.Left);
            Check(bitmapDialog && legacyDialog.PixelHash() ==
                  sharedDialog.PixelHash(),
                  "bitmap dialog body pixels preserve the legacy renderer");

            TrueTypeFont renderer;
            SharedTextFont trueType;
            bool loaded = TrueTypeFont.TryCreate(fontBytes, 24, out renderer);
            bool selected = SharedTextFont.TrySelectTrueType(renderer, bitmap,
                                                               out trueType);
            int dialogX = 20;
            int dialogY = 8;
            int buttonX = 90;
            int buttonY = 60;
            int buttonWidth = 84;
            int buttonHeight = 26;
            int originalButtonX = buttonX;
            int originalButtonY = buttonY;
            int originalButtonWidth = buttonWidth;
            int originalButtonHeight = buttonHeight;
            Graphics trueTypeDialog = new Graphics(220, 110, background);
            const string longBody =
                "A proportional dialog message that wraps within its fixed content bounds.";
            bool trueTypeBody = loaded && selected &&
                ShellTextSurfaceRenderer.DrawWrappedText(trueTypeDialog,
                    trueType, longBody, dialogX + 8, dialogY + 8, 112, 0,
                    0xFFFFFFFFu, dialogX + 8, dialogY + 8, 112, 46,
                    SharedTextAlignment.Left);
            int buttonLineHeight;
            bool trueTypeButton = trueType != null &&
                trueType.GetLineHeight(out buttonLineHeight) &&
                ShellTextSurfaceRenderer.DrawFixedText(trueTypeDialog,
                    trueType, "OK", buttonX + 4,
                    buttonY + (buttonHeight - buttonLineHeight) / 2,
                    buttonWidth - 8, buttonX, buttonY, buttonWidth,
                    buttonHeight, 0xFFFFFFFFu,
                    SharedTextAlignment.Center);
            Check(trueTypeBody && trueTypeButton &&
                  HasInkOnlyInsideTwoClips(trueTypeDialog, background,
                    dialogX + 8, dialogY + 8, 112, 46,
                    buttonX, buttonY, buttonWidth, buttonHeight) &&
                  trueTypeDialog.IntermediateBlendPixelCount > 0 &&
                  buttonX == originalButtonX && buttonY == originalButtonY &&
                  buttonWidth == originalButtonWidth &&
                  buttonHeight == originalButtonHeight,
                  "TrueType dialog bodies and labels clip without changing button geometry");

            Graphics trueTypeToast1 = new Graphics(180, 90, background);
            Graphics trueTypeToast2 = new Graphics(180, 90, background);
            bool toast1 = selected && ShellTextSurfaceRenderer.DrawWrappedText(
                trueTypeToast1, trueType, longBody, 12, 10, 96, 0,
                0xFFFFFFFFu, 12, 10, 96, 66, SharedTextAlignment.Left);
            bool toast2 = selected && ShellTextSurfaceRenderer.DrawWrappedText(
                trueTypeToast2, trueType, longBody, 12, 10, 96, 0,
                0xFFFFFFFFu, 12, 10, 96, 66, SharedTextAlignment.Left);
            Check(toast1 && toast2 && trueTypeToast1.PixelHash() ==
                  trueTypeToast2.PixelHash() &&
                  HasInkOnlyInsideClip(trueTypeToast1, background,
                    12, 10, 96, 66),
                  "TrueType notifications redraw deterministically inside their bounds");
        }

        private static IFont CreateBitmapFontForFallback() {
            return CreateProportionalBitmap();
        }

        private static string Slice(string text, SharedTextLayout layout,
                                    int line) {
            int start = layout.GetLineStart(line);
            int length = layout.GetLineLength(line);
            return text.Substring(start, length);
        }

        private static void DrawToastFrame(Graphics graphics, int x, int y,
                                           int textWidth, int textHeight) {
            int width = textWidth + 30;
            int height = textHeight + 30;
            Fill(graphics, x, y, width, height, 0xFF111111u);
            for (int px = x; px < x + width; px++) {
                graphics.DrawPoint(px, y, 0xFF222222u);
                graphics.DrawPoint(px, y + height - 1, 0xFF222222u);
            }
            for (int py = y; py < y + height; py++) {
                graphics.DrawPoint(x, py, 0xFF222222u);
                graphics.DrawPoint(x + width - 1, py, 0xFF222222u);
            }
            Fill(graphics, x, y, 5, height, 0xFF80B000u);
        }

        private static void Fill(Graphics graphics, int x, int y,
                                 int width, int height, uint color) {
            for (int py = y; py < y + height; py++) {
                for (int px = x; px < x + width; px++)
                    graphics.DrawPoint(px, py, color);
            }
        }

        private static bool HasInkOnlyInsideClip(Graphics graphics,
                                                 uint background,
                                                 int x, int y,
                                                 int width, int height) {
            long left = x > 0 ? x : 0;
            long top = y > 0 ? y : 0;
            long right = (long)x + width;
            long bottom = (long)y + height;
            if (right > graphics.Width) right = graphics.Width;
            if (bottom > graphics.Height) bottom = graphics.Height;
            bool inside = false;
            bool outside = false;
            for (int py = 0; py < graphics.Height; py++) {
                for (int px = 0; px < graphics.Width; px++) {
                    if (graphics.GetPixel(px, py) == background) continue;
                    if (px >= left && px < right && py >= top && py < bottom)
                        inside = true;
                    else
                        outside = true;
                }
            }
            return inside && !outside;
        }

        private static bool HasInkOnlyInsideTwoClips(Graphics graphics,
                                                     uint background,
                                                     int x1, int y1,
                                                     int width1, int height1,
                                                     int x2, int y2,
                                                     int width2, int height2) {
            long right1 = (long)x1 + width1;
            long bottom1 = (long)y1 + height1;
            long right2 = (long)x2 + width2;
            long bottom2 = (long)y2 + height2;
            bool inside = false;
            bool outside = false;
            for (int py = 0; py < graphics.Height; py++) {
                for (int px = 0; px < graphics.Width; px++) {
                    if (graphics.GetPixel(px, py) == background) continue;
                    bool inFirst = px >= x1 && px < right1 &&
                                   py >= y1 && py < bottom1;
                    bool inSecond = px >= x2 && px < right2 &&
                                    py >= y2 && py < bottom2;
                    if (inFirst || inSecond) inside = true;
                    else outside = true;
                }
            }
            return inside && !outside;
        }

        private static void Check(bool condition, string description) {
            _checks++;
            if (!condition) {
                _failures++;
                Console.Error.WriteLine("FONTS4 FAIL: " + description);
            }
        }
    }
}
