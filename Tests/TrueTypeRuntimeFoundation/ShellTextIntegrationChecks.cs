using System;
using System.Drawing;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using guideXOS.Graph;

namespace guideXOS.FontTests {
    internal static class ShellTextIntegrationChecks {
        private static int _checks;
        private static int _failures;
        private static ulong _bitmapTitleHash;
        private static ulong _bitmapTaskbarHash;
        private static ulong _bitmapClockHash;
        private static ulong _bitmapMenuHash;

        public static int Run(byte[] fontBytes) {
            _checks = 0;
            _failures = 0;
            IFont bitmap = CreateCompleteBitmapFont();
            CheckBitmapCompatibility(bitmap);
            Console.WriteLine("FONTS3 bitmap shell hashes: title=0x" +
                _bitmapTitleHash.ToString("X16") + ", taskbar=0x" +
                _bitmapTaskbarHash.ToString("X16") + ", clock=0x" +
                _bitmapClockHash.ToString("X16") + ", start-menu=0x" +
                _bitmapMenuHash.ToString("X16"));
            CheckSelectionAndFallback(fontBytes, bitmap);
            CheckSurrogateBoundaries();
            CheckUnicodeTruncation(fontBytes, bitmap);
            CheckTrueTypeSurfaces(fontBytes, bitmap);
            Console.WriteLine("FONTS3 shell text checks: " + _checks +
                              ", failures: " + _failures);
            return _failures;
        }

        private static void CheckSurrogateBoundaries() {
            Check(!TrueTypeFont.IsHighSurrogate('\uD7FF') &&
                  TrueTypeFont.IsHighSurrogate('\uD800') &&
                  TrueTypeFont.IsHighSurrogate('\uDBFF') &&
                  !TrueTypeFont.IsHighSurrogate('\uDC00'),
                  "high-surrogate classifier matches the UTF-16 range");
            Check(!TrueTypeFont.IsLowSurrogate('\uDBFF') &&
                  TrueTypeFont.IsLowSurrogate('\uDC00') &&
                  TrueTypeFont.IsLowSurrogate('\uDFFF') &&
                  !TrueTypeFont.IsLowSurrogate('\uE000'),
                  "low-surrogate classifier matches the UTF-16 range");
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

        private static void CheckBitmapCompatibility(IFont bitmap) {
            SharedTextFont selected = SharedTextFont.CreateDefaultBitmap(bitmap);
            const uint background = 0xFF18212Au;
            const string title = "FILES";
            Graphics expectedTitle = new Graphics(180, 64, background);
            Graphics titlePixels = new Graphics(180, 64, background);
            bitmap.DrawString(46, 12, title, expectedTitle);
            bool titleDrawn = ShellTextSurfaceRenderer.DrawWindowTitle(
                titlePixels, selected, bitmap, title, 46, 12,
                30, 8, 120, 28);
            Check(titleDrawn && expectedTitle.PixelHash() ==
                  titlePixels.PixelHash(),
                  "bitmap window-title pixels match the previous direct draw");
            _bitmapTitleHash = expectedTitle.PixelHash();

            Graphics longTitlePixels = new Graphics(80, 36, background);
            string longTitle = new string('W', 80);
            bool longTitleDrawn = ShellTextSurfaceRenderer.DrawWindowTitle(
                longTitlePixels, selected, bitmap, longTitle,
                -12, 4, -4, 4, 45, 20);
            Check(longTitleDrawn && HasInkOnlyInsideClip(longTitlePixels,
                  background, -4, 4, 45, 20) &&
                  longTitlePixels.InvalidDrawAttempts == 0,
                  "bitmap long titles clip to the visible title region");

            const int buttonX = 26;
            const int buttonY = 10;
            const int buttonWidth = 74;
            const int buttonHeight = 24;
            Graphics expectedButton = new Graphics(180, 64, background);
            Graphics buttonPixels = new Graphics(180, 64, background);
            Framebuffer.Graphics = expectedButton;
            bitmap.DrawString(buttonX, buttonY +
                (buttonHeight - bitmap.FontSize) / 2, title,
                buttonWidth, bitmap.FontSize);
            Framebuffer.Graphics = buttonPixels;
            bool buttonDrawn = ShellTextSurfaceRenderer.DrawTaskbarLabel(
                buttonPixels, selected, bitmap, buttonX, buttonY,
                buttonWidth, buttonHeight, title);
            Check(buttonDrawn && expectedButton.PixelHash() ==
                  buttonPixels.PixelHash(),
                  "bitmap taskbar label pixels and line limits are unchanged");
            _bitmapTaskbarHash = expectedButton.PixelHash();

            const string time = "12:34";
            const string date = "11/02/2026";
            Graphics expectedClock = new Graphics(220, 88, background);
            Graphics clockPixels = new Graphics(220, 88, background);
            Framebuffer.Graphics = expectedClock;
            int timeX = expectedClock.Width - 12 - bitmap.MeasureString(time);
            int timeY = 48 + ((40 - bitmap.FontSize) / 2) -
                        (bitmap.FontSize / 2);
            bitmap.DrawString(timeX, timeY, time);
            int dateX = expectedClock.Width - 12 - bitmap.MeasureString(date);
            bitmap.DrawString(dateX, timeY + bitmap.FontSize, date);
            Framebuffer.Graphics = clockPixels;
            bool clockDrawn = ShellTextSurfaceRenderer.DrawTaskbarClock(
                clockPixels, selected, bitmap, time, date, 48, 40,
                Math.Max(bitmap.MeasureString(time),
                         bitmap.MeasureString(date)), 12);
            Check(clockDrawn && expectedClock.PixelHash() ==
                  clockPixels.PixelHash(),
                  "bitmap taskbar clock right-aligns both lines without clipping the date");
            _bitmapClockHash = expectedClock.PixelHash();

            const int menuX = 18;
            const int menuY = 20;
            const int menuWidth = 74;
            const int menuHeight = 16;
            Graphics expectedMenu = new Graphics(180, 64, background);
            Graphics menuPixels = new Graphics(180, 64, background);
            Framebuffer.Graphics = expectedMenu;
            bitmap.DrawString(menuX, menuY, title, menuWidth, menuHeight);
            Framebuffer.Graphics = menuPixels;
            bool menuDrawn = ShellTextSurfaceRenderer.DrawStartMenuText(
                menuPixels, selected, bitmap, menuX, menuY, title,
                menuX, menuY, menuWidth, menuHeight, true);
            Check(menuDrawn && expectedMenu.PixelHash() ==
                  menuPixels.PixelHash(),
                  "bitmap Start Menu row pixels and bounds are unchanged");
            _bitmapMenuHash = expectedMenu.PixelHash();
        }

        private static void CheckSelectionAndFallback(byte[] fontBytes,
                                                       IFont bitmap) {
            SharedTextFontSelection selection = new SharedTextFontSelection();
            SharedTextFont current = selection.Resolve(bitmap);
            Check(current != null && current.IsAvailable &&
                  current.Provider == SharedFontProviderKind.BitmapAtlas,
                  "shell text selection starts at the bitmap production default");

            SharedTextFont selected;
            bool selectedTrueType = selection.TrySelectTrueType(fontBytes, 16,
                bitmap, out selected);
            int trueTypeGeneration = selection.Generation;
            Check(selectedTrueType && selected != null &&
                  selected.Provider == SharedFontProviderKind.TrueType &&
                  selection.Resolve(bitmap) == selected,
                  "validated opt-in selects one shared TrueType handle");

            bool missingSelected = selection.TrySelectTrueType(null, 16,
                bitmap, out selected);
            Check(!missingSelected && selected != null &&
                  selected.Provider == SharedFontProviderKind.BitmapAtlas &&
                  selection.Generation > trueTypeGeneration,
                  "missing font bytes select the bitmap fallback safely");

            bool malformedSelected = selection.TrySelectTrueType(
                new byte[] { 0, 1, 2, 3 }, 16, bitmap, out selected);
            Check(!malformedSelected && selected != null &&
                  selected.IsAvailable &&
                  selected.Provider == SharedFontProviderKind.BitmapAtlas,
                  "malformed font bytes select a usable bitmap fallback");

            bool unsupportedSize = selection.TrySelectTrueType(fontBytes, 13,
                bitmap, out selected);
            Check(!unsupportedSize && selected != null &&
                  selected.Provider == SharedFontProviderKind.BitmapAtlas,
                  "unsupported shell size falls back without retaining stale metrics");

            selection.TrySelectTrueType(fontBytes, 20, bitmap, out selected);
            IFont replacementBitmap = new IFont(null, "", 8);
            SharedTextFont reset = selection.Resolve(replacementBitmap);
            Check(reset != null &&
                  reset.Provider == SharedFontProviderKind.BitmapAtlas &&
                  !reset.IsAvailable,
                  "replacing the bitmap owner clears the old TrueType handle");
            selection.Resolve(bitmap);
        }

        private static void CheckTrueTypeSurfaces(byte[] fontBytes,
                                                  IFont bitmap) {
            int[] sizes = { 12, 16, 20, 24, 32 };
            int[] widths = { 128, 320, 640 };
            const uint background = 0xFF17202Au;
            const string title = "Window Title: café Ω \U0001F600";
            const string taskbarLabel = "A long app label Ω";
            const string menuLabel = "Task Manager";

            for (int sizeIndex = 0; sizeIndex < sizes.Length; sizeIndex++) {
                TrueTypeFont renderer;
                bool loaded = TrueTypeFont.TryCreate(fontBytes,
                    sizes[sizeIndex], out renderer);
                SharedTextFontSelection selection =
                    new SharedTextFontSelection();
                SharedTextFont selected;
                bool selectedTrueType = selection.TrySelectTrueType(renderer,
                    bitmap, out selected);
                Check(loaded && selectedTrueType && selected != null &&
                      selected.Provider == SharedFontProviderKind.TrueType,
                      "shell surfaces opt in to TrueType at " + sizes[sizeIndex] + " px");
                if (!selectedTrueType || renderer == null) continue;

                SharedTextMeasurement firstMeasure;
                SharedTextMeasurement secondMeasure;
                bool firstMeasured = selected.MeasureText(title,
                    out firstMeasure);
                bool secondMeasured = selected.MeasureText(title,
                    out secondMeasure);
                Check(firstMeasured && secondMeasured &&
                      firstMeasure.Width == secondMeasure.Width &&
                      firstMeasure.Height == secondMeasure.Height &&
                      firstMeasure.LineCount == secondMeasure.LineCount,
                      "selected title measurement is deterministic at " +
                      sizes[sizeIndex] + " px");

                Graphics titleFirst = new Graphics(220, 64, background);
                Graphics titleRepeat = new Graphics(220, 64, background);
                ShellTextSurfaceRenderer.DrawWindowTitle(titleFirst, selected,
                    bitmap, title, 8, 6, 8, 4, 204, 28);
                ShellTextSurfaceRenderer.DrawWindowTitle(titleRepeat, selected,
                    bitmap, title, 8, 6, 8, 4, 204, 28);
                Check(titleFirst.PixelHash() == titleRepeat.PixelHash(),
                      "selected title pixels are deterministic at " +
                      sizes[sizeIndex] + " px");

                for (int dimensionIndex = 0; dimensionIndex < widths.Length;
                     dimensionIndex++) {
                    int width = widths[dimensionIndex];
                    int height = width == 128 ? 72 : (width == 320 ? 96 : 160);

                    int titleClipX = width == 128 ? -5 : 6;
                    int titleClipY = 4;
                    int titleClipWidth = width == 128 ? 92 : 138;
                    int titleClipHeight = 28;
                    Graphics titleGraphics = new Graphics(width, height,
                                                          background);
                    bool titleDrawn = ShellTextSurfaceRenderer.DrawWindowTitle(
                        titleGraphics, selected, bitmap, title,
                        titleClipX, titleClipY, titleClipX, titleClipY,
                        titleClipWidth, titleClipHeight);
                    Check(titleDrawn && HasInkOnlyInsideClip(titleGraphics,
                          background, titleClipX, titleClipY,
                          titleClipWidth, titleClipHeight) &&
                          titleGraphics.InvalidDrawAttempts == 0,
                          "TrueType window title clips safely on " + width +
                          " px display at " + sizes[sizeIndex] + " px");

                    int buttonX = width - 82;
                    int buttonY = height - 34;
                    int buttonWidth = 72;
                    int buttonHeight = 28;
                    Graphics taskbarGraphics = new Graphics(width, height,
                                                             background);
                    bool taskbarDrawn =
                        ShellTextSurfaceRenderer.DrawTaskbarLabel(
                            taskbarGraphics, selected, bitmap, buttonX,
                            buttonY, buttonWidth, buttonHeight,
                            taskbarLabel);
                    Check(taskbarDrawn && HasInkOnlyInsideClip(
                          taskbarGraphics, background, buttonX, buttonY,
                          buttonWidth, buttonHeight) &&
                          taskbarGraphics.InvalidDrawAttempts == 0,
                          "TrueType taskbar label stays in fixed button bounds on " +
                          width + " px display at " + sizes[sizeIndex] + " px");

                    int menuX = 12;
                    int menuY = 32;
                    int menuWidth = 90;
                    int menuHeight = 26;
                    Graphics menuGraphics = new Graphics(width, height,
                                                          background);
                    int savedMenuHitX = menuX;
                    int savedMenuHitY = menuY;
                    int savedMenuHitWidth = menuWidth;
                    int savedMenuHitHeight = menuHeight;
                    bool menuDrawn =
                        ShellTextSurfaceRenderer.DrawStartMenuText(
                            menuGraphics, selected, bitmap, menuX, menuY,
                            menuLabel, menuX, menuY, menuWidth, menuHeight,
                            true);
                    Check(menuDrawn && HasInkOnlyInsideClip(menuGraphics,
                          background, menuX, menuY, menuWidth, menuHeight) &&
                          menuGraphics.InvalidDrawAttempts == 0 &&
                          menuX == savedMenuHitX && menuY == savedMenuHitY &&
                          menuWidth == savedMenuHitWidth &&
                          menuHeight == savedMenuHitHeight,
                          "TrueType Start Menu label respects its fixed row bounds on " +
                          width + " px display at " + sizes[sizeIndex] + " px");

                    int clockWidth = bitmap.MeasureString("12:34");
                    clockWidth = Math.Max(clockWidth,
                        bitmap.MeasureString("11/02/2026"));
                    Graphics clockGraphics = new Graphics(width, height,
                                                           background);
                    bool clockDrawn =
                        ShellTextSurfaceRenderer.DrawTaskbarClock(
                            clockGraphics, selected, bitmap, "12:34",
                            "11/02/2026", height - 40, 40, clockWidth, 12);
                    int clockX = width - 12 - clockWidth;
                    Check(clockDrawn && HasInkOnlyInsideClip(clockGraphics,
                          background, clockX, height - 40, clockWidth, 40) &&
                          clockGraphics.InvalidDrawAttempts == 0,
                          "TrueType taskbar clock preserves reserved layout on " +
                          width + " px display at " + sizes[sizeIndex] + " px");
                    Check(taskbarGraphics.IntermediateBlendPixelCount > 0,
                          "TrueType taskbar text alpha blends at " +
                          sizes[sizeIndex] + " px");
                }

                Graphics warmDraw = new Graphics(220, 70, background);
                ShellTextSurfaceRenderer.DrawWindowTitle(warmDraw, selected,
                    bitmap, "Warm cache Ω", 5, 6, 5, 4, 190, 30);
                int warmRasterizations = renderer.Rasterizations;
                for (int draw = 0; draw < 5; draw++) {
                    ShellTextSurfaceRenderer.DrawWindowTitle(warmDraw,
                        selected, bitmap, "Warm cache Ω", 5, 6,
                        5, 4, 190, 30);
                }
                Check(renderer.Rasterizations == warmRasterizations &&
                      renderer.CacheHits > 0 &&
                      renderer.CacheBytes <= TrueTypeFont.MaximumCacheBytes,
                      "shell draws retain the bounded shared glyph cache at " +
                      sizes[sizeIndex] + " px");
            }
        }

        private static void CheckUnicodeTruncation(byte[] fontBytes,
                                                   IFont bitmap) {
            TrueTypeFont renderer;
            SharedTextFont selected;
            bool loaded = TrueTypeFont.TryCreate(fontBytes, 16,
                                                  out renderer);
            bool optedIn = SharedTextFont.TrySelectTrueType(renderer, bitmap,
                                                              out selected);
            Check(loaded && optedIn && selected != null,
                  "Start Menu truncation uses the opt-in TrueType provider");
            if (!optedIn || selected == null) return;

            string source = "A\U0001F600BCDEFG";
            SharedTextMeasurement target;
            bool targetMeasured = selected.MeasureText("A\U0001F600...",
                                                        out target);
            string truncated = ShellTextSurfaceRenderer.TruncateToWidth(
                selected, source, target.Width);
            SharedTextMeasurement actual;
            bool actualMeasured = selected.MeasureText(truncated, out actual);
            Check(targetMeasured && actualMeasured &&
                  truncated.IndexOf("\U0001F600") >= 0 &&
                  truncated.EndsWith("...", StringComparison.Ordinal) &&
                  actual.Width <= target.Width,
                  "bounded TrueType truncation preserves a supplementary Unicode scalar");

            string veryLong = new string('W', 100000);
            SharedTextMeasurement smallTarget;
            selected.MeasureText("WW...", out smallTarget);
            string bounded = ShellTextSurfaceRenderer.TruncateToWidth(
                selected, veryLong, smallTarget.Width);
            SharedTextMeasurement boundedMeasure;
            bool boundedMeasured = selected.MeasureText(bounded,
                                                         out boundedMeasure);
            Check(bounded.Length <= 259 &&
                  bounded.EndsWith("...", StringComparison.Ordinal) &&
                  boundedMeasured && boundedMeasure.Width <= smallTarget.Width,
                  "very long labels produce a bounded measured ellipsis result");
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

        private static void Check(bool condition, string description) {
            _checks++;
            if (!condition) {
                _failures++;
                Console.Error.WriteLine("FONTS3 FAIL: " + description);
            }
        }
    }
}
