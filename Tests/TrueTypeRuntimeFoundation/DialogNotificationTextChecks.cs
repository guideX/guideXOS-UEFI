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
            CheckSizePropagationToShellSurfaces(fontBytes, bitmap);
            CheckFittedControlLabels(bitmapFont, fontBytes);
            CheckProductionMessageBox(bitmapFont, fontBytes);
            CheckProductionNotificationLifecycle(bitmapFont, fontBytes);

            Console.WriteLine("FONTS4 hosted layout and UI checks: " +
                              _checks + ", failures: " + _failures);
            return _failures;
        }

        private static IFont CreateProportionalBitmap() {
            return CreateProportionalBitmap(8);
        }

        private static IFont CreateProportionalBitmap(int size) {
            string charset = "";
            for (int code = 32; code <= 126; code++) charset += (char)code;
            Image atlas = new Image(charset.Length * size, size);
            for (int i = 0; i < charset.Length; i++) {
                char ch = charset[i];
                int inkWidth = ch == ' ' ? 0 :
                    ch == 'i' || ch == 'l' ? size / 4 :
                    ch == 'W' ? size - 1 : size / 2;
                if (ch != ' ' && inkWidth < 1) inkWidth = 1;
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
            int[] sizes = { 12, 14, 16, 18, 20, 24 };
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

        private static void CheckSizePropagationToShellSurfaces(
                byte[] fontBytes, IFont bitmap) {
            int[] sizes = { 12, 14, 16, 18, 20, 24 };
            ulong[] notificationHashes = new ulong[sizes.Length];
            const uint background = 0xFF19232Du;
            const string body =
                "Unsaved changes will be shown at the selected text size.";
            const string notificationText = "Changes are ready to save.";
            const int messageX = 8;
            const int messageScreenWidth = 330;
            const int shutdownNoX = 150;
            const int shutdownYesX = 240;
            const int shutdownButtonY = 92;
            const int shutdownButtonWidth = 80;
            const int shutdownButtonHeight = 28;
            const int saveButtonY = 92;
            const int saveButtonWidth = 90;
            const int saveButtonHeight = 28;
            const int saveButtonGap = 8;

            for (int i = 0; i < sizes.Length; i++) {
                TrueTypeFont renderer;
                SharedTextFont selected;
                bool loaded = TrueTypeFont.TryCreate(fontBytes, sizes[i],
                    out renderer);
                bool optedIn = SharedTextFont.TrySelectTrueType(renderer,
                    bitmap, out selected);
                int lineHeight = 0;
                bool haveLineHeight = selected != null &&
                    selected.GetLineHeight(out lineHeight);
                SharedTextMeasurement bodyMeasurement =
                    new SharedTextMeasurement();
                bool measuredBody = selected != null &&
                    selected.MeasureText(body, out bodyMeasurement);
                int messageBoxWidth;
                bool measuredMessageBox = ShellTextSurfaceRenderer
                    .MeasureMessageBoxWidth(selected, bitmap, body,
                        messageScreenWidth, messageX, out messageBoxWidth);
                int availableMessageBoxWidth = messageScreenWidth - messageX - 4;
                int expectedMessageBoxWidth = bodyMeasurement.Width >
                    availableMessageBoxWidth ? availableMessageBoxWidth :
                    bodyMeasurement.Width;

                Graphics notification = new Graphics(330, 100, background);
                bool notificationDrawn = ShellTextSurfaceRenderer
                    .DrawWrappedText(notification, selected, notificationText,
                        10, 10, 240, 0, 0xFFFFFFFFu, 10, 10, 240, 72,
                        SharedTextAlignment.Left);

                Graphics messageBox = new Graphics(330, 100, background);
                bool messageBoxDrawn = measuredMessageBox &&
                    ShellTextSurfaceRenderer.DrawWrappedText(messageBox,
                        selected, body, messageX, 8, messageBoxWidth, 0,
                        0xFFFFFFFFu, messageX, 8, messageBoxWidth, 72,
                        SharedTextAlignment.Left);

                Graphics shutdown = new Graphics(330, 130, background);
                bool shutdownBody = ShellTextSurfaceRenderer.DrawWrappedText(
                    shutdown, selected, body, 10, 10, 240, 0,
                    0xFFFFFFFFu, 10, 10, 240, 76,
                    SharedTextAlignment.Left);
                bool shutdownButtons = haveLineHeight &&
                    ShellTextSurfaceRenderer.DrawFixedText(shutdown, selected,
                        "No", shutdownNoX + 4,
                        shutdownButtonY + (shutdownButtonHeight - lineHeight) / 2,
                        shutdownButtonWidth - 8, shutdownNoX,
                        shutdownButtonY, shutdownButtonWidth,
                        shutdownButtonHeight, 0xFFFFFFFFu,
                        SharedTextAlignment.Center) &&
                    ShellTextSurfaceRenderer.DrawFixedText(shutdown, selected,
                        "Yes", shutdownYesX + 4,
                        shutdownButtonY + (shutdownButtonHeight - lineHeight) / 2,
                        shutdownButtonWidth - 8, shutdownYesX,
                        shutdownButtonY, shutdownButtonWidth,
                        shutdownButtonHeight, 0xFFFFFFFFu,
                        SharedTextAlignment.Center);

                Graphics saveChanges = new Graphics(330, 130, background);
                bool saveBody = ShellTextSurfaceRenderer.DrawWrappedText(
                    saveChanges, selected, body, 10, 10, 300, 0,
                    0xFFFFFFFFu, 10, 10, 300, 76,
                    SharedTextAlignment.Left);
                int centeredButtonY = saveButtonY +
                    (saveButtonHeight - lineHeight) / 2;
                bool saveButtons = haveLineHeight &&
                    ShellTextSurfaceRenderer.DrawFixedText(saveChanges,
                        selected, "Save", 14, centeredButtonY,
                        saveButtonWidth - 8, 10, saveButtonY,
                        saveButtonWidth, saveButtonHeight, 0xFFFFFFFFu,
                        SharedTextAlignment.Center) &&
                    ShellTextSurfaceRenderer.DrawFixedText(saveChanges,
                        selected, "Don't Save",
                        10 + saveButtonWidth + saveButtonGap + 4,
                        centeredButtonY, saveButtonWidth - 8,
                        10 + saveButtonWidth + saveButtonGap, saveButtonY,
                        saveButtonWidth, saveButtonHeight, 0xFFFFFFFFu,
                        SharedTextAlignment.Center) &&
                    ShellTextSurfaceRenderer.DrawFixedText(saveChanges,
                        selected, "Cancel",
                        10 + (saveButtonWidth + saveButtonGap) * 2 + 4,
                        centeredButtonY, saveButtonWidth - 8,
                        10 + (saveButtonWidth + saveButtonGap) * 2,
                        saveButtonY, saveButtonWidth, saveButtonHeight,
                        0xFFFFFFFFu, SharedTextAlignment.Center);

                notificationHashes[i] = notification.PixelHash();
                Check(loaded && optedIn && selected.FontSize == sizes[i] &&
                      selected.TrueTypeFontSize == sizes[i] &&
                      haveLineHeight && measuredBody &&
                      bodyMeasurement.Height == lineHeight *
                          bodyMeasurement.LineCount && measuredMessageBox &&
                      messageBoxWidth == expectedMessageBoxWidth &&
                      notificationDrawn && messageBoxDrawn && shutdownBody &&
                      shutdownButtons && saveBody && saveButtons &&
                      HasInkOnlyInsideClip(notification, background,
                          10, 10, 240, 72) &&
                      shutdownNoX == 150 && shutdownYesX == 240 &&
                      shutdownButtonWidth == 80 &&
                      shutdownButtonHeight == 28 && saveButtonWidth == 90 &&
                      saveButtonHeight == 28,
                      "MessageBox, Shutdown, Save Changes, and notification use " +
                      "the selected " + sizes[i] + " px font within fixed bounds");
            }
            Check(notificationHashes[0] != notificationHashes[1] &&
                  notificationHashes[1] != notificationHashes[2],
                  "notification output changes after repeated size selections");
        }

        private static void CheckFittedControlLabels(SharedTextFont unused,
                                                     byte[] fontBytes) {
            IFont bitmapOwner = CreateProportionalBitmap(20);
            SharedTextFont bitmap = SharedTextFont.CreateDefaultBitmap(
                bitmapOwner);
            Check(bitmap != null && bitmap.IsAvailable &&
                  bitmap.FontSize == 20,
                  "fixed-control fitting starts from the production bitmap size");
            CheckFittedControlLabel(bitmap, bitmapOwner, "Bitmap 20 px");

            int[] sizes = { 12, 14, 16, 18, 20, 24 };
            for (int i = 0; i < sizes.Length; i++) {
                TrueTypeFont renderer;
                SharedTextFont selected;
                bool loaded = TrueTypeFont.TryCreate(fontBytes, sizes[i],
                    out renderer);
                bool optedIn = SharedTextFont.TrySelectTrueType(renderer,
                    bitmapOwner, out selected);
                Check(loaded && optedIn && selected != null,
                      "fitted labels load TrueType " + sizes[i] + " px");
                if (loaded && optedIn && selected != null)
                    CheckFittedControlLabel(selected, bitmapOwner,
                        "TrueType " + sizes[i] + " px");
            }

            TrueTypeFont unicodeRenderer;
            SharedTextFont unicodeFont = null;
            bool unicodeLoaded = TrueTypeFont.TryCreate(fontBytes, 24,
                out unicodeRenderer) && SharedTextFont.TrySelectTrueType(
                    unicodeRenderer, bitmapOwner, out unicodeFont);
            if (unicodeLoaded) {
                SharedTextMeasurement prefix;
                bool measured = unicodeFont.MeasureText("A...", out prefix);
                string scalarFit = ShellTextSurfaceRenderer.TruncateToWidth(
                    unicodeFont, "A\U0001F600B", prefix.Width);
                Graphics scalarActual = new Graphics(100, 48, 0xFF101820u);
                Graphics scalarExpected = new Graphics(100, 48, 0xFF101820u);
                bool actualDrawn = ShellTextSurfaceRenderer.DrawFittedText(
                    scalarActual, unicodeFont, "A\U0001F600B", 8, 8,
                    prefix.Width, 8, 8, prefix.Width, 32, 0xFFFFFFFFu,
                    SharedTextAlignment.Left);
                bool expectedDrawn = unicodeFont.DrawText(scalarExpected, 8,
                    8, scalarFit, 0xFFFFFFFFu, 8, 8, prefix.Width, 32,
                    SharedTextAlignment.Left, prefix.Width);
                Check(measured && scalarFit == "A..." && actualDrawn &&
                      expectedDrawn && scalarActual.PixelHash() ==
                      scalarExpected.PixelHash() &&
                      !char.IsHighSurrogate(scalarFit[scalarFit.Length - 1]),
                      "fitted truncation keeps supplementary scalars intact");
            }
        }

        private static void CheckFittedControlLabel(SharedTextFont selected,
                                                    IFont bitmapOwner,
                                                    string description) {
            const string label = "Display Options";
            const int x = 12;
            const int y = 8;
            const int width = 60;
            const int height = 34;
            const uint background = 0xFF102030u;
            int lineHeight;
            bool hasLineHeight = selected.GetLineHeight(out lineHeight);
            int textY = selected.Provider == SharedFontProviderKind.BitmapAtlas
                ? y + (height - bitmapOwner.FontSize) / 2
                : y + (height - lineHeight) / 2;
            Graphics actual = new Graphics(140, 56, background);
            Graphics expected = new Graphics(140, 56, background);
            string fitted = ShellTextSurfaceRenderer.TruncateToWidth(
                selected, label, width);
            bool actualDrawn = ShellTextSurfaceRenderer.DrawTaskbarLabel(
                actual, selected, bitmapOwner, x, y, width, height, label);
            bool expectedDrawn = selected.DrawText(expected, x, textY, fitted,
                0xFFFFFFFFu, x, y, width, height, SharedTextAlignment.Left,
                width);
            Check(hasLineHeight && actualDrawn && expectedDrawn &&
                  fitted != label && fitted.EndsWith("...",
                      StringComparison.Ordinal) &&
                  actual.PixelHash() == expected.PixelHash() &&
                  HasInkOnlyInsideClip(actual, background, x, y, width,
                      height) && actual.InvalidDrawAttempts == 0,
                  description + " taskbar labels show a measured ellipsis in bounds");

            int startMenuY = selected.Provider ==
                    SharedFontProviderKind.BitmapAtlas
                ? y : y + (bitmapOwner.FontSize - lineHeight) / 2;
            Graphics actualStartMenu = new Graphics(140, 56, background);
            Graphics expectedStartMenu = new Graphics(140, 56, background);
            bool startMenuDrawn = ShellTextSurfaceRenderer.DrawStartMenuText(
                actualStartMenu, selected, bitmapOwner, x, y, label, x, y,
                width, height, true);
            bool expectedStartMenuDrawn = selected.DrawTextFitted(
                expectedStartMenu, x, startMenuY, label, 0xFFFFFFFFu,
                x, y, width, height, width, SharedTextAlignment.Left,
                SharedTextOverflowBehavior.Ellipsis);
            Check(startMenuDrawn && expectedStartMenuDrawn &&
                  actualStartMenu.PixelHash() ==
                      expectedStartMenu.PixelHash() &&
                  HasInkOnlyInsideClip(actualStartMenu, background, x, y,
                      width, height),
                  description + " Start Menu labels ellipsize inside their existing row");

            const int buttonWidth = 90;
            const int buttonHeight = 28;
            const int buttonX = 12;
            const int buttonY = 18;
            int buttonYText = buttonY + (buttonHeight - lineHeight) / 2;
            string buttonFit = ShellTextSurfaceRenderer.TruncateToWidth(
                selected, "Don't Save", buttonWidth - 8);
            SharedTextMeasurement buttonMeasure;
            bool buttonMeasured = selected.MeasureText("Don't Save",
                out buttonMeasure);
            bool buttonOverflow = buttonMeasured &&
                buttonMeasure.Width > buttonWidth - 8;
            Graphics actualButton = new Graphics(140, 56, background);
            Graphics expectedButton = new Graphics(140, 56, background);
            bool buttonDrawn = ShellTextSurfaceRenderer.DrawFittedText(
                actualButton, selected, "Don't Save", buttonX + 4,
                buttonYText, buttonWidth - 8, buttonX, buttonY,
                buttonWidth, buttonHeight, 0xFFFFFFFFu,
                SharedTextAlignment.Center);
            bool expectedButtonDrawn = selected.DrawText(expectedButton,
                buttonX + 4, buttonYText, buttonFit, 0xFFFFFFFFu,
                buttonX, buttonY, buttonWidth, buttonHeight,
                SharedTextAlignment.Center, buttonWidth - 8);
            Check(buttonDrawn && expectedButtonDrawn &&
                  (!buttonOverflow || buttonFit.EndsWith("...",
                      StringComparison.Ordinal)) &&
                  (buttonOverflow || buttonFit == "Don't Save") &&
                  actualButton.PixelHash() == expectedButton.PixelHash() &&
                  HasInkOnlyInsideClip(actualButton, background, buttonX,
                      buttonY, buttonWidth, buttonHeight),
                  description + " Save Changes labels ellipsize without resizing button hit regions");

            const int shutdownButtonWidth = 80;
            const int shutdownButtonHeight = 28;
            const int shutdownButtonY = 18;
            Graphics actualShutdown = new Graphics(190, 56, background);
            Graphics expectedShutdown = new Graphics(190, 56, background);
            bool shutdownLabelsDrawn = true;
            bool shutdownExpectedDrawn = true;
            string[] shutdownLabels = { "No", "Yes" };
            for (int i = 0; i < shutdownLabels.Length; i++) {
                int shutdownX = 12 + i * (shutdownButtonWidth + 8);
                int shutdownTextY = shutdownButtonY +
                    (shutdownButtonHeight - lineHeight) / 2;
                shutdownLabelsDrawn &=
                    ShellTextSurfaceRenderer.DrawFittedText(actualShutdown,
                        selected, shutdownLabels[i], shutdownX + 4,
                        shutdownTextY, shutdownButtonWidth - 8,
                        shutdownX, shutdownButtonY, shutdownButtonWidth,
                        shutdownButtonHeight, 0xFFFFFFFFu,
                        SharedTextAlignment.Center);
                shutdownExpectedDrawn &= selected.DrawText(expectedShutdown,
                    shutdownX + 4, shutdownTextY, shutdownLabels[i],
                    0xFFFFFFFFu, shutdownX, shutdownButtonY,
                    shutdownButtonWidth, shutdownButtonHeight,
                    SharedTextAlignment.Center, shutdownButtonWidth - 8);
            }
            Check(shutdownLabelsDrawn && shutdownExpectedDrawn &&
                  actualShutdown.PixelHash() == expectedShutdown.PixelHash() &&
                  HasInkOnlyInsideClip(actualShutdown, background, 12,
                      shutdownButtonY, shutdownButtonWidth * 2 + 8,
                      shutdownButtonHeight),
                  description + " Shutdown labels stay centered within their existing buttons");

            Graphics actualRight = new Graphics(140, 56, background);
            Graphics expectedRight = new Graphics(140, 56, background);
            bool rightDrawn = ShellTextSurfaceRenderer.DrawFittedText(
                actualRight, selected, label, x, textY, width, x, y, width,
                height, 0xFFFFFFFFu, SharedTextAlignment.Right);
            bool expectedRightDrawn = selected.DrawText(expectedRight, x,
                textY, fitted, 0xFFFFFFFFu, x, y, width, height,
                SharedTextAlignment.Right, width);
            Check(rightDrawn && expectedRightDrawn &&
                  actualRight.PixelHash() == expectedRight.PixelHash(),
                  description + " right-aligned fitting uses the same width metrics");
        }

        private static void CheckProductionMessageBox(SharedTextFont unused,
                                                       byte[] fontBytes) {
            IFont bitmapOwner = CreateProportionalBitmap(20);
            SharedTextFont bitmap = SharedTextFont.CreateDefaultBitmap(
                bitmapOwner);
            const string message =
                "A long message body should wrap to readable lines inside the " +
                "MessageBox window while keeping the visible text within the " +
                "window's content bounds. This hosted case exercises the " +
                "production MessageBox.Show and OnDraw implementation.";
            int[] sizes = { 12, 14, 16, 18, 20, 24 };

            CheckMessageBoxProvider(bitmap, bitmapOwner, message, 320, 240,
                "Bitmap 20 px");
            for (int i = 0; i < sizes.Length; i++) {
                TrueTypeFont renderer;
                SharedTextFont selected;
                bool loaded = TrueTypeFont.TryCreate(fontBytes, sizes[i],
                    out renderer);
                bool optedIn = SharedTextFont.TrySelectTrueType(renderer,
                    bitmapOwner, out selected);
                if (loaded && optedIn)
                    CheckMessageBoxProvider(selected, bitmapOwner, message,
                        sizes[i] == 24 ? 240 : 320,
                        sizes[i] == 24 ? 200 : 240,
                        "TrueType " + sizes[i] + " px");
                else
                    Check(false, "production MessageBox loads " + sizes[i] + " px");
            }

            Framebuffer.Graphics = new Graphics(320, 240, 0xFF102030u);
            WindowManager.font = bitmapOwner;
            WindowManager.ShellTextFont = null;
            WindowManager.ShellTextFontGeneration++;
            WindowManager.Windows.Clear();
            Window owner = new Window(20, 40, 120, 90);
            WindowManager.Windows.Add(owner);
            Desktop.msgbox = null;
            System.Windows.Forms.MessageBox.Show(message);
            Desktop.msgbox.OnDraw();
            Check(Desktop.msgbox.Visible &&
                  WindowManager.Windows[WindowManager.Windows.Count - 1] ==
                      Desktop.msgbox &&
                  Desktop.msgbox.Height > bitmapOwner.FontSize + 24 &&
                  HasInkOnlyInsideClip(Framebuffer.Graphics, 0xFF102030u,
                      Desktop.msgbox.X + 12, Desktop.msgbox.Y + 12,
                      Desktop.msgbox.Width - 24,
                      Desktop.msgbox.Height - 24) &&
                  Framebuffer.Graphics.InvalidDrawAttempts == 0,
                  "production MessageBox wraps long text through the bitmap fallback when no provider is selected");
            Desktop.msgbox.Visible = false;
            Check(!Desktop.msgbox.Visible,
                  "production MessageBox can be dismissed through its window visibility path");
        }

        private static void CheckMessageBoxProvider(SharedTextFont selected,
                                                     IFont bitmapOwner,
                                                     string message,
                                                     int screenWidth,
                                                     int screenHeight,
                                                     string description) {
            const uint background = 0xFF102030u;
            Framebuffer.Graphics = new Graphics(screenWidth, screenHeight,
                background);
            WindowManager.font = bitmapOwner;
            WindowManager.ShellTextFont = selected;
            WindowManager.ShellTextFontGeneration++;
            WindowManager.Windows.Clear();
            Window owner = new Window(18, 44, 110, 80);
            WindowManager.Windows.Add(owner);
            Desktop.msgbox = null;

            System.Windows.Forms.MessageBox.Show(message);
            Desktop.msgbox.OnDraw();

            int clipX = Desktop.msgbox.X + 12;
            int clipY = Desktop.msgbox.Y + 12;
            int clipWidth = Desktop.msgbox.Width - 24;
            int clipHeight = Desktop.msgbox.Height - 24;
            int lineHeight;
            bool metrics = selected.GetLineHeight(out lineHeight);
            Check(metrics && Desktop.msgbox.Visible &&
                  WindowManager.Windows[WindowManager.Windows.Count - 1] ==
                      Desktop.msgbox && Desktop.msgbox.BaseDrawCount == 1 &&
                  Desktop.msgbox.Width >= 200 &&
                  Desktop.msgbox.Width <= screenWidth - 24 &&
                  Desktop.msgbox.Height > lineHeight + 24 &&
                  Desktop.msgbox.X >= 0 &&
                  Desktop.msgbox.X + Desktop.msgbox.Width <= screenWidth &&
                  Desktop.msgbox.Y >= Desktop.msgbox.BarHeight &&
                  Desktop.msgbox.Y + Desktop.msgbox.Height <=
                      screenHeight - 40 && clipWidth > 0 && clipHeight > 0 &&
                  HasInkOnlyInsideClip(Framebuffer.Graphics, background,
                      clipX, clipY, clipWidth, clipHeight) &&
                  Framebuffer.Graphics.InvalidDrawAttempts == 0,
                  description + " production MessageBox wraps, fits, clips, and receives topmost focus");
            Desktop.msgbox.Visible = false;
            Check(!Desktop.msgbox.Visible,
                  description + " production MessageBox dismisses without changing its content bounds");
        }

        private static void CheckProductionNotificationLifecycle(
                SharedTextFont unused, byte[] fontBytes) {
            const uint background = 0xFF101820u;
            const string longMessage =
                "Repeated production notifications need provider-aware widths " +
                "and wrapping while moving into the desktop corner, and their " +
                "text must stay clipped inside each allocated toast.";
            IFont bitmapOwner = CreateProportionalBitmap(20);
            SharedTextFont bitmap = SharedTextFont.CreateDefaultBitmap(
                bitmapOwner);
            Framebuffer.Graphics = new Graphics(320, 240, background);
            WindowManager.font = bitmapOwner;
            WindowManager.ShellTextFont = bitmap;
            WindowManager.ShellTextFontGeneration = 1;
            Timer.Ticks = 100;
            NotificationManager.ResetForKernelBoot();
            Notify first = new Notify(longMessage);
            Notify second = new Notify(longMessage + " Second instance.");
            NotificationManager.Add(first);
            NotificationManager.Add(second);
            NotificationManager.Update();

            int maximumTextWidth = Framebuffer.Width -
                NotificationManager.Threshold;
            bool bitmapBounds = first.SWidth > 0 && first.SWidth <=
                maximumTextWidth && first.AnimationWidth == first.SWidth &&
                first.ani.MaximumValue == NotificationManager.Threshold +
                    first.AnimationWidth && NotificationManager.NotificationCount == 2;
            first.ani.Value = first.ani.MaximumValue / 2;
            Framebuffer.Graphics = new Graphics(320, 240, background);
            NotificationManager.Update();
            bool bitmapMovement = first.X == first.ani.Value;

            TrueTypeFont renderer24;
            SharedTextFont trueType24 = null;
            bool loaded24 = TrueTypeFont.TryCreate(fontBytes, 24,
                out renderer24) && SharedTextFont.TrySelectTrueType(renderer24,
                    bitmapOwner, out trueType24);
            if (!loaded24) {
                Check(false, "production notifications load TrueType 24 px");
                return;
            }

            WindowManager.ShellTextFont = trueType24;
            WindowManager.ShellTextFontGeneration++;
            first.DisposeUntil = 0;
            second.DisposeUntil = 0;
            first.ani.Value = first.ani.MaximumValue / 2;
            first.X = first.ani.Value;
            second.ani.Value = second.ani.MaximumValue / 2;
            second.X = second.ani.Value;
            Framebuffer.Graphics = new Graphics(320, 240, background);
            NotificationManager.Update();
            bool switchedBounds = first.SWidth <= maximumTextWidth &&
                first.AnimationWidth == first.SWidth &&
                first.SHeight >= trueType24.FontSize &&
                first.ani.MaximumValue == NotificationManager.Threshold +
                    first.AnimationWidth && first.X == first.ani.Value;
            first.ani.Value = first.ani.MaximumValue;
            first.X = first.ani.Value;
            second.ani.Value = second.ani.MaximumValue;
            second.X = second.ani.Value;
            Timer.Ticks = 200;
            Framebuffer.Graphics = new Graphics(320, 240, background);
            NotificationManager.Update();
            ulong trueType24Hash = Framebuffer.Graphics.PixelHash();

            Notify third = new Notify(longMessage + " Third instance.");
            NotificationManager.Add(third);
            Framebuffer.Graphics = new Graphics(320, 240, background);
            NotificationManager.Update();
            bool repeated = NotificationManager.NotificationCount == 3 &&
                third.AnimationWidth == third.SWidth &&
                third.ani.MaximumValue == NotificationManager.Threshold +
                    third.AnimationWidth;

            int[] sizes = { 12, 14, 16, 18, 20 };
            ulong previousHash = trueType24Hash;
            bool sizeSwitches = true;
            for (int i = 0; i < sizes.Length; i++) {
                TrueTypeFont renderer;
                SharedTextFont selected;
                if (!TrueTypeFont.TryCreate(fontBytes, sizes[i], out renderer) ||
                    !SharedTextFont.TrySelectTrueType(renderer, bitmapOwner,
                        out selected)) {
                    sizeSwitches = false;
                    continue;
                }

                WindowManager.ShellTextFont = selected;
                WindowManager.ShellTextFontGeneration++;
                Notify[] active = { first, second, third };
                for (int n = 0; n < active.Length; n++) {
                    active[n].DisposeUntil = 0;
                    active[n].ani.Value = active[n].ani.MaximumValue / 3;
                    active[n].X = active[n].ani.Value;
                }
                Framebuffer.Graphics = new Graphics(320, 240, background);
                NotificationManager.Update();
                for (int n = 0; n < active.Length; n++) {
                    if (active[n].AnimationWidth != active[n].SWidth ||
                        active[n].ani.MaximumValue !=
                            NotificationManager.Threshold +
                                active[n].AnimationWidth ||
                        active[n].SHeight < selected.FontSize)
                        sizeSwitches = false;
                    active[n].ani.Value = active[n].ani.MaximumValue;
                    active[n].X = active[n].ani.Value;
                }
                Timer.Ticks += 100;
                Framebuffer.Graphics = new Graphics(320, 240, background);
                NotificationManager.Update();
                ulong currentHash = Framebuffer.Graphics.PixelHash();
                if (currentHash == previousHash ||
                    Framebuffer.Graphics.InvalidDrawAttempts != 0)
                    sizeSwitches = false;
                previousHash = currentHash;
            }

            Check(bitmapBounds && bitmapMovement && switchedBounds && repeated &&
                  sizeSwitches,
                  "production notifications reflow, move, repeat, clip, and redraw after provider and size changes");

            first.X = first.ani.MaximumValue;
            first.DisposeUntil = Timer.Ticks;
            int beforeDismiss = NotificationManager.NotificationCount;
            Timer.Ticks++;
            Framebuffer.Graphics = new Graphics(320, 240, background);
            NotificationManager.Update();
            Check(beforeDismiss == 3 && NotificationManager.NotificationCount == 2,
                  "production notification timed dismissal removes an expired toast");
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
