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
        private static SharedTextLayout _wrappedLayout;

        internal static int MeasureTextWidth(SharedTextFont selected,
                                             IFont bitmap, string text) {
            if (text == null) return 0;
            if (selected != null && selected.Provider ==
                    SharedFontProviderKind.TrueType) {
                SharedTextMeasurement measurement;
                return selected.MeasureText(text, out measurement)
                    ? measurement.Width : 0;
            }
            return bitmap == null ? 0 : bitmap.MeasureString(text);
        }

        internal static bool TryMeasureTrueTypeTaskbarClock(
                SharedTextFont selected, string time, string date,
                int barHeight, out int reservedWidth, out int timeWidth,
                out int dateWidth, out int lineHeight, out bool singleLine) {
            reservedWidth = timeWidth = dateWidth = lineHeight = 0;
            singleLine = false;
            if (selected == null || selected.Provider !=
                    SharedFontProviderKind.TrueType || time == null ||
                    barHeight <= 0 || !selected.GetLineHeight(out lineHeight))
                return false;

            SharedTextMeasurement timeMeasurement;
            if (!selected.MeasureText(time, out timeMeasurement)) return false;
            timeWidth = timeMeasurement.Width;
            if (date == null) {
                reservedWidth = timeWidth;
                return true;
            }

            SharedTextMeasurement dateMeasurement;
            if (!selected.MeasureText(date, out dateMeasurement)) return false;
            dateWidth = dateMeasurement.Width;
            singleLine = (long)lineHeight * 2 > barHeight;
            if (singleLine) {
                const int gap = 6;
                long combinedWidth = (long)timeWidth + gap + dateWidth;
                reservedWidth = combinedWidth > int.MaxValue
                    ? int.MaxValue : (int)combinedWidth;
            } else {
                reservedWidth = timeWidth > dateWidth ? timeWidth : dateWidth;
            }
            return true;
        }

        /// <summary>
        /// Measure a provider-aware wrapped block with bounded, reusable line
        /// storage. The caller owns the returned scalar values, not the scratch
        /// layout or a font cache.
        /// </summary>
        internal static bool MeasureWrappedText(SharedTextFont selected,
                                                string text, int maxWidth,
                                                int lineSpacing,
                                                out SharedTextMeasurement measurement,
                                                out bool truncated) {
            measurement = new SharedTextMeasurement();
            truncated = false;
            if (selected == null) return false;
            if (_wrappedLayout == null) _wrappedLayout = new SharedTextLayout();
            if (!_wrappedLayout.TryLayout(selected, text, maxWidth,
                                          lineSpacing)) return false;
            measurement.Width = _wrappedLayout.Width;
            measurement.Height = _wrappedLayout.Height;
            measurement.LineCount = _wrappedLayout.LineCount;
            truncated = _wrappedLayout.WasTruncated;
            return true;
        }

        /// <summary>
        /// Draw a wrapped block inside a half-open caller-owned rectangle.
        /// Empty clips are successful no-ops. Line origins retain the selected
        /// provider's baseline metrics through SharedTextLayout.
        /// </summary>
        internal static bool DrawWrappedText(Graphics graphics,
                                             SharedTextFont selected,
                                             string text, int x, int y,
                                             int maxWidth, int lineSpacing,
                                             uint color, int clipX, int clipY,
                                             int clipWidth, int clipHeight,
                                             SharedTextAlignment alignment) {
            if (graphics == null || selected == null || text == null ||
                clipWidth < 0 || clipHeight < 0) return false;
            if (_wrappedLayout == null) _wrappedLayout = new SharedTextLayout();
            if (!_wrappedLayout.TryLayout(selected, text, maxWidth,
                                          lineSpacing)) return false;
            int viewportX, viewportY, viewportWidth, viewportHeight;
            if (!TryIntersectViewport(graphics, clipX, clipY, clipWidth,
                    clipHeight, out viewportX, out viewportY, out viewportWidth,
                    out viewportHeight)) return true;
            return _wrappedLayout.Draw(graphics, x, y, color, viewportX,
                viewportY, viewportWidth, viewportHeight, alignment);
        }

        /// <summary>
        /// Draw one unwrapped label in fixed control bounds. Alignment affects
        /// pixels only; this method never changes a control or hit rectangle.
        /// </summary>
        internal static bool DrawFixedText(Graphics graphics,
                                           SharedTextFont selected,
                                           string text, int x, int y,
                                           int alignmentWidth,
                                           int clipX, int clipY,
                                           int clipWidth, int clipHeight,
                                           uint color,
                                           SharedTextAlignment alignment) {
            if (graphics == null || selected == null || text == null ||
                clipWidth < 0 || clipHeight < 0) return false;
            int viewportX, viewportY, viewportWidth, viewportHeight;
            if (!TryIntersectViewport(graphics, clipX, clipY, clipWidth,
                    clipHeight, out viewportX, out viewportY, out viewportWidth,
                    out viewportHeight)) return true;
            return selected.DrawText(graphics, x, y, text, color, viewportX,
                viewportY, viewportWidth, viewportHeight, alignment,
                alignmentWidth);
        }

        /// <summary>
        /// Draw one fixed-control label with a provider-measured ellipsis when
        /// it exceeds the control's text region. The control clip and hit
        /// geometry remain owned by the caller.
        /// </summary>
        internal static bool DrawFittedText(Graphics graphics,
                                            SharedTextFont selected,
                                            string text, int x, int y,
                                            int alignmentWidth,
                                            int clipX, int clipY,
                                            int clipWidth, int clipHeight,
                                            uint color,
                                            SharedTextAlignment alignment) {
            if (graphics == null || selected == null || text == null ||
                clipWidth < 0 || clipHeight < 0) return false;
            int viewportX, viewportY, viewportWidth, viewportHeight;
            if (!TryIntersectViewport(graphics, clipX, clipY, clipWidth,
                    clipHeight, out viewportX, out viewportY, out viewportWidth,
                    out viewportHeight)) return true;
            return selected.DrawTextFitted(graphics, x, y, text, color,
                viewportX, viewportY, viewportWidth, viewportHeight,
                alignmentWidth, alignment,
                SharedTextOverflowBehavior.Ellipsis);
        }

        internal static bool MeasureMessageBoxWidth(SharedTextFont selected,
                                                     IFont bitmap,
                                                     string text,
                                                     int screenWidth,
                                                     int originX,
                                                     out int width) {
            width = 0;
            if (text == null) return false;
            if (selected != null && selected.Provider ==
                    SharedFontProviderKind.TrueType) {
                SharedTextMeasurement measurement;
                if (!selected.MeasureText(text, out measurement)) return false;
                int availableWidth = screenWidth - originX - 4;
                if (availableWidth < 0) availableWidth = 0;
                width = measurement.Width > availableWidth
                    ? availableWidth : measurement.Width;
                return true;
            }
            if (bitmap == null) return false;
            width = bitmap.MeasureString(text);
            return true;
        }

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
                    if (selected != null)
                        selected.RecordLegacyBitmapDrawCall();
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
                if (selected != null) {
                    return DrawFittedText(graphics, selected, text, x, textY,
                        width, x, y, width, height, 0xFFFFFFFFu,
                        SharedTextAlignment.Left);
                }
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
            return selected.DrawTextFitted(graphics, x, trueTypeY, text,
                0xFFFFFFFFu, viewportX, viewportY, viewportWidth,
                viewportHeight, width, SharedTextAlignment.Left,
                SharedTextOverflowBehavior.Ellipsis);
        }

        /// <summary>
        /// Draw a context-menu label inside its existing fixed row. Bitmap
        /// mode retains the legacy draw call; TrueType uses the shared
        /// provider's metrics and clipped renderer without changing row or
        /// hit-test geometry.
        /// </summary>
        internal static bool DrawContextMenuText(Graphics graphics,
                                                 SharedTextFont selected,
                                                 IFont bitmap,
                                                 string text,
                                                 int x, int rowY,
                                                 int clipX, int clipY,
                                                 int clipWidth,
                                                 int clipHeight) {
            if (graphics == null || text == null || clipWidth < 0 ||
                    clipHeight < 0) return false;
            if (selected == null ||
                    selected.Provider == SharedFontProviderKind.BitmapAtlas) {
                if (bitmap == null || bitmap.FontSize <= 0) return false;
                int textY = rowY + (clipHeight / 2) -
                            (bitmap.FontSize / 2);
                if (selected != null) {
                    return DrawFittedText(graphics, selected, text, x,
                        textY, clipWidth, clipX, clipY, clipWidth,
                        clipHeight, 0xFFFFFFFFu,
                        SharedTextAlignment.Left);
                }
                bitmap.DrawString(x, textY, text);
                return true;
            }

            int lineHeight;
            if (!selected.GetLineHeight(out lineHeight)) return false;
            int trueTypeY = rowY + (clipHeight - lineHeight) / 2;
            return DrawFittedText(graphics, selected, text, x, trueTypeY,
                clipWidth, clipX, clipY, clipWidth, clipHeight,
                0xFFFFFFFFu, SharedTextAlignment.Left);
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
                if (selected != null) selected.RecordLegacyBitmapDrawCall();
                if (date != null) {
                    int dateWidth = bitmap.MeasureString(date);
                    int dateX = graphics.Width - rightPadding - dateWidth;
                    bitmap.DrawString(dateX, timeY + bitmap.FontSize, date,
                                      graphics);
                }
                if (date != null && selected != null)
                    selected.RecordLegacyBitmapDrawCall();
                return true;
            }

            int measuredWidth, trueTypeTimeWidth, trueTypeDateWidth,
                lineHeight;
            bool singleLine;
            if (!TryMeasureTrueTypeTaskbarClock(selected, time, date,
                    barHeight, out measuredWidth, out trueTypeTimeWidth,
                    out trueTypeDateWidth, out lineHeight,
                    out singleLine)) return false;
            if (reservedWidth < measuredWidth) reservedWidth = measuredWidth;
            if (reservedWidth <= 0) return false;
            int textX = graphics.Width - rightPadding - reservedWidth;
            int clipHeight = barHeight;
            int viewportX, viewportY, viewportWidth, viewportHeight;
            if (!TryIntersectViewport(graphics, textX, yTop, reservedWidth,
                    clipHeight, out viewportX, out viewportY,
                    out viewportWidth, out viewportHeight)) return true;
            if (singleLine) {
                int groupWidth = trueTypeTimeWidth + 6 + trueTypeDateWidth;
                int groupX = textX + reservedWidth - groupWidth;
                int singleLineY = yTop + (barHeight - lineHeight) / 2;
                bool singleTimeDrawn = DrawFixedText(graphics, selected, time,
                    groupX, singleLineY, trueTypeTimeWidth, textX, yTop,
                    reservedWidth, clipHeight, 0xFFFFFFFFu,
                    SharedTextAlignment.Left);
                bool singleDateDrawn = DrawFixedText(graphics, selected, date,
                    groupX + trueTypeTimeWidth + 6, singleLineY,
                    trueTypeDateWidth, textX,
                    yTop, reservedWidth, clipHeight, 0xFFFFFFFFu,
                    SharedTextAlignment.Left);
                return singleTimeDrawn && singleDateDrawn;
            }

            int textY = yTop + (barHeight - (lineHeight *
                (date == null ? 1 : 2))) / 2;
            SharedTextAlignment timeAlignment =
                trueTypeTimeWidth > reservedWidth
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
                if (bounded && selected != null) {
                    return DrawFittedText(graphics, selected, text, x, y,
                        clipWidth, clipX, clipY, clipWidth, clipHeight,
                        0xFFFFFFFFu, SharedTextAlignment.Left);
                }
                if (bounded) {
                    int bitmapViewportX, bitmapViewportY,
                        bitmapViewportWidth, bitmapViewportHeight;
                    if (TryIntersectViewport(graphics, clipX, clipY,
                            clipWidth, clipHeight,
                            out bitmapViewportX, out bitmapViewportY,
                            out bitmapViewportWidth,
                            out bitmapViewportHeight)) {
                        bitmap.DrawStringClipped(x, y, text, graphics,
                            bitmapViewportX, bitmapViewportY,
                            bitmapViewportWidth, bitmapViewportHeight);
                    }
                } else {
                    bitmap.DrawString(x, y, text);
                }
                if (selected != null) selected.RecordLegacyBitmapDrawCall();
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
            if (bounded) {
                return selected.DrawTextFitted(graphics, x, textY, text,
                    0xFFFFFFFFu, viewportX, viewportY, viewportWidth,
                    viewportHeight, clipWidth, SharedTextAlignment.Left,
                    SharedTextOverflowBehavior.Ellipsis);
            }
            return selected.DrawText(graphics, x, textY, text,
                0xFFFFFFFFu, viewportX, viewportY, viewportWidth,
                viewportHeight);
        }

        public static string TruncateToWidth(SharedTextFont selected,
                                             string text, int maxWidth) {
            if (text == null || maxWidth <= 0 || selected == null)
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
                if (next < text.Length &&
                        TrueTypeFont.IsHighSurrogate(text[end]) &&
                        TrueTypeFont.IsLowSurrogate(text[next])) next++;
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
                if (end > 0 && TrueTypeFont.IsLowSurrogate(text[end]) &&
                        TrueTypeFont.IsHighSurrogate(text[end - 1])) end--;
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
