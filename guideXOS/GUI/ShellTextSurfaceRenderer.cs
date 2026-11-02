using guideXOS.Graph;
using guideXOS.Misc;

namespace guideXOS.GUI {
    /// <summary>
    /// Small adapters for the shell text surfaces migrated to SharedTextFont.
    /// Bitmap mode deliberately follows each surface's previous IFont call
    /// shape so its production pixels and layout stay stable. TrueType mode
    /// uses the selected provider for both measurement and clipped drawing.
    /// </summary>
    internal static class ShellTextSurfaceRenderer {
        public static bool DrawWindowTitle(Graphics graphics,
                                           SharedTextFont selected,
                                           IFont bitmap,
                                           string text,
                                           int legacyX, int legacyY,
                                           int clipX, int clipY,
                                           int clipWidth, int clipHeight) {
            if (graphics == null || text == null) return false;
            if (selected == null ||
                    selected.Provider == SharedFontProviderKind.BitmapAtlas) {
                if (bitmap == null) return false;
                int bitmapClipX, bitmapClipY, bitmapClipWidth,
                    bitmapClipHeight;
                if (TryIntersectViewport(graphics, clipX, clipY, clipWidth,
                        clipHeight, out bitmapClipX, out bitmapClipY,
                        out bitmapClipWidth, out bitmapClipHeight)) {
                    bitmap.DrawStringClipped(legacyX, legacyY, text,
                        graphics, bitmapClipX, bitmapClipY,
                        bitmapClipWidth, bitmapClipHeight);
                }
                return true;
            }

            SharedTextMeasurement measurement;
            SharedFontMetrics metrics;
            if (!selected.MeasureText(text, out measurement) ||
                    !selected.GetFontMetrics(out metrics)) return false;

            SharedTextAlignment alignment = measurement.Width > clipWidth
                ? SharedTextAlignment.Left : SharedTextAlignment.Center;
            int titleY = clipY + (clipHeight - metrics.LineHeight) / 2;
            int viewportX, viewportY, viewportWidth, viewportHeight;
            if (!TryIntersectViewport(graphics, clipX, clipY, clipWidth,
                    clipHeight, out viewportX, out viewportY,
                    out viewportWidth, out viewportHeight)) return true;
            return selected.DrawText(graphics, clipX, titleY, text,
                0xFFFFFFFFu, viewportX, viewportY, viewportWidth,
                viewportHeight,
                alignment, clipWidth);
        }

        public static bool DrawTaskbarLabel(Graphics graphics,
                                            SharedTextFont selected,
                                            IFont bitmap,
                                            int x, int y,
                                            int width, int height,
                                            string text) {
            if (graphics == null || text == null || width <= 0 || height <= 0)
                return false;
            if (selected == null ||
                    selected.Provider == SharedFontProviderKind.BitmapAtlas) {
                if (bitmap == null || bitmap.FontSize <= 0) return false;
                int textY = y + (height - bitmap.FontSize) / 2;
                bitmap.DrawString(x, textY, text, width, bitmap.FontSize);
                return true;
            }

            int lineHeight;
            if (!selected.GetLineHeight(out lineHeight)) return false;
            int trueTypeY = y + (height - lineHeight) / 2;
            int viewportX, viewportY, viewportWidth, viewportHeight;
            if (!TryIntersectViewport(graphics, x, y, width, height,
                    out viewportX, out viewportY, out viewportWidth,
                    out viewportHeight)) return true;
            return selected.DrawText(graphics, x, trueTypeY, text,
                0xFFFFFFFFu, viewportX, viewportY, viewportWidth,
                viewportHeight,
                SharedTextAlignment.Left, width);
        }

        public static bool DrawTaskbarClock(Graphics graphics,
                                            SharedTextFont selected,
                                            IFont bitmap,
                                            string time, string date,
                                            int yTop, int barHeight,
                                            int reservedWidth,
                                            int rightPadding) {
            if (graphics == null || bitmap == null ||
                    bitmap.FontSize <= 0 || time == null || barHeight <= 0)
                return false;

            if (selected == null ||
                    selected.Provider == SharedFontProviderKind.BitmapAtlas) {
                int timeWidth = bitmap.MeasureString(time);
                int timeX = graphics.Width - rightPadding - timeWidth;
                int timeY = yTop + ((barHeight - bitmap.FontSize) / 2) -
                            (bitmap.FontSize / 2);
                bitmap.DrawString(timeX, timeY, time, graphics);
                if (date != null)
                    bitmap.DrawString(timeX, timeY + bitmap.FontSize, date,
                                      graphics);
                return true;
            }

            if (reservedWidth <= 0) return false;
            int lineHeight;
            if (!selected.GetLineHeight(out lineHeight)) return false;
            int textX = graphics.Width - rightPadding - reservedWidth;
            int textY = yTop + (barHeight - (lineHeight *
                (date == null ? 1 : 2))) / 2;
            int clipHeight = barHeight;
            int viewportX, viewportY, viewportWidth, viewportHeight;
            if (!TryIntersectViewport(graphics, textX, yTop, reservedWidth,
                    clipHeight, out viewportX, out viewportY,
                    out viewportWidth, out viewportHeight)) return true;
            SharedTextMeasurement timeMeasurement;
            if (!selected.MeasureText(time, out timeMeasurement)) return false;
            SharedTextAlignment timeAlignment = timeMeasurement.Width > reservedWidth
                ? SharedTextAlignment.Left : SharedTextAlignment.Right;
            bool timeDrawn = selected.DrawText(graphics, textX, textY, time,
                0xFFFFFFFFu, viewportX, viewportY, viewportWidth,
                viewportHeight,
                timeAlignment, reservedWidth);
            bool dateDrawn = true;
            if (date != null) {
                SharedTextMeasurement dateMeasurement;
                if (!selected.MeasureText(date, out dateMeasurement)) return false;
                SharedTextAlignment dateAlignment = dateMeasurement.Width >
                    reservedWidth ? SharedTextAlignment.Left :
                    SharedTextAlignment.Right;
                dateDrawn = selected.DrawText(graphics, textX,
                    textY + lineHeight, date, 0xFFFFFFFFu, viewportX,
                    viewportY, viewportWidth, viewportHeight, dateAlignment,
                    reservedWidth);
            }
            return timeDrawn && dateDrawn;
        }

        public static bool DrawStartMenuText(Graphics graphics,
                                             SharedTextFont selected,
                                             IFont bitmap,
                                             int x, int y, string text,
                                             int clipX, int clipY,
                                             int clipWidth, int clipHeight,
                                             bool bounded) {
            if (graphics == null || text == null) return false;
            if (selected == null ||
                    selected.Provider == SharedFontProviderKind.BitmapAtlas) {
                if (bitmap == null) return false;
                if (bounded) bitmap.DrawString(x, y, text, clipWidth, clipHeight);
                else bitmap.DrawString(x, y, text);
                return true;
            }

            int lineHeight;
            if (bitmap == null || bitmap.FontSize <= 0 ||
                    !selected.GetLineHeight(out lineHeight)) return false;
            int textY = y + (bitmap.FontSize - lineHeight) / 2;
            int drawClipX = bounded ? clipX : 0;
            int drawClipY = bounded ? clipY : 0;
            int drawClipWidth = bounded ? clipWidth : graphics.Width;
            int drawClipHeight = bounded ? clipHeight : graphics.Height;
            int viewportX, viewportY, viewportWidth, viewportHeight;
            if (!TryIntersectViewport(graphics, drawClipX, drawClipY,
                    drawClipWidth, drawClipHeight, out viewportX,
                    out viewportY, out viewportWidth,
                    out viewportHeight)) return true;
            return selected.DrawText(graphics, x, textY, text,
                0xFFFFFFFFu, viewportX, viewportY, viewportWidth,
                viewportHeight);
        }

        public static string TruncateToWidth(SharedTextFont selected,
                                             string text, int maxWidth) {
            if (text == null || maxWidth <= 0 || selected == null ||
                    selected.Provider != SharedFontProviderKind.TrueType)
                return string.Empty;
            if (text.Length == 0) return text;

            const string ellipsis = "...";
            SharedTextMeasurement ellipsisMeasurement;
            if (!selected.MeasureText(ellipsis, out ellipsisMeasurement) ||
                    ellipsisMeasurement.Width > maxWidth) return string.Empty;

            int end = 0;
            int scalarCount = 0;
            const int maximumPrefixScalars = 256;
            while (end < text.Length && scalarCount < maximumPrefixScalars) {
                int next = end + 1;
                if (next < text.Length && char.IsHighSurrogate(text[end]) &&
                        char.IsLowSurrogate(text[next])) next++;
                SharedTextMeasurement prefixMeasurement;
                if (!selected.MeasureTextRange(text, 0, next,
                        out prefixMeasurement) ||
                        prefixMeasurement.Width > maxWidth) break;
                end = next;
                scalarCount++;
                if (end == text.Length) return text;
                if ((long)prefixMeasurement.Width +
                        ellipsisMeasurement.Width > maxWidth) break;
            }

            while (true) {
                string prefix = end == 0 ? string.Empty :
                    text.Substring(0, end);
                string candidate = null;
                try {
                    candidate = prefix + ellipsis;
                    SharedTextMeasurement candidateMeasurement;
                    if (selected.MeasureText(candidate,
                            out candidateMeasurement) &&
                            candidateMeasurement.Width <= maxWidth) {
                        return candidate;
                    }
                } finally {
#if Kernel
                    if (prefix != null &&
                            !object.ReferenceEquals(prefix, text))
                        prefix.Dispose();
#endif
                }

#if Kernel
                if (candidate != null) candidate.Dispose();
#endif
                if (end == 0) return string.Empty;
                end--;
                if (end > 0 && char.IsLowSurrogate(text[end]) &&
                        char.IsHighSurrogate(text[end - 1])) end--;
            }
        }

        private static bool TryIntersectViewport(Graphics graphics,
                                                 int x, int y,
                                                 int width, int height,
                                                 out int clipX, out int clipY,
                                                 out int clipWidth,
                                                 out int clipHeight) {
            clipX = 0;
            clipY = 0;
            clipWidth = 0;
            clipHeight = 0;
            if (graphics == null || width <= 0 || height <= 0) return false;
            long left = x > 0 ? x : 0;
            long top = y > 0 ? y : 0;
            long right = (long)x + width;
            long bottom = (long)y + height;
            if (right > graphics.Width) right = graphics.Width;
            if (bottom > graphics.Height) bottom = graphics.Height;
            if (right <= left || bottom <= top) return false;
            clipX = (int)left;
            clipY = (int)top;
            clipWidth = (int)(right - left);
            clipHeight = (int)(bottom - top);
            return true;
        }
    }
}
