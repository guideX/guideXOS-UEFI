using guideXOS.Graph;
using guideXOS.Kernel.Drivers;
using System.Drawing;

namespace guideXOS.Misc {
    /// <summary>
    /// Font style enumeration.
    /// </summary>
    public enum FontStyle {
        Normal = 0,
        Bold = 1,
        Italic = 2,
        BoldItalic = 3
    }

    /// <summary>
    /// The guideXOS bitmap-atlas font renderer.
    ///
    /// Each glyph occupies one FontSize x FontSize cell in an RGBA Image.
    /// The atlas is loaded once by WindowManager and remains resident; drawing
    /// a glyph only reads that already-decoded cell and composites it through
    /// the canonical Graphics receiver.
    /// </summary>
    internal class IFont {
        private readonly Image image;
        private readonly string charset;
        private readonly bool useFixedWidth;

        // Font variants (bold, italic, bold+italic).
        private IFont _boldVariant;
        private IFont _italicVariant;
        private IFont _boldItalicVariant;

        public int FontSize;
        public int CharWidth;
        public int Padding;

        /// <summary>
        /// Number of atlas columns. Malformed or uninitialized fonts report 0
        /// instead of dividing by zero.
        /// </summary>
        public int NumRow {
            get {
                if (image == null || image.Width <= 0 || FontSize <= 0) return 0;
                return image.Width / FontSize;
            }
        }

        /// <summary>
        /// True when the atlas has a usable backing buffer and cell geometry.
        /// </summary>
        public bool IsValid {
            get {
                return image != null && image.RawData != null &&
                       image.Width > 0 && image.Height > 0 &&
                       FontSize > 0 && NumRow > 0 &&
                       image.Height / FontSize > 0;
            }
        }

        public IFont(Image _img, string _charset, int size,
                     bool fixedWidth = false, int fixedCharWidth = 0,
                     int padding = 0) {
            image = _img;
            charset = _charset;
            FontSize = size > 0 ? size : 0;
            useFixedWidth = fixedWidth;
            CharWidth = fixedCharWidth > 0 ? fixedCharWidth : FontSize;
            Padding = padding;
        }

        /// <summary>
        /// Set font variants for bold/italic support.
        /// </summary>
        public void SetVariants(IFont bold, IFont italic, IFont boldItalic) {
            _boldVariant = bold;
            _italicVariant = italic;
            _boldItalicVariant = boldItalic;
        }

        /// <summary>
        /// Get a font variant.
        /// </summary>
        public IFont GetVariant(FontStyle style) {
            switch (style) {
                case FontStyle.Bold:
                    return _boldVariant ?? this;
                case FontStyle.Italic:
                    return _italicVariant ?? this;
                case FontStyle.BoldItalic:
                    return _boldItalicVariant ?? this;
                default:
                    return this;
            }
        }

        private bool TryGetGlyphCell(char chr, out int baseX, out int baseY) {
            baseX = 0;
            baseY = 0;
            if (!IsValid || charset == null) return false;

            int index = charset.IndexOf(chr);
            if (index < 0) return false;

            int columns = NumRow;
            int rows = image.Height / FontSize;
            long cellCount = (long)columns * rows;
            if (columns <= 0 || rows <= 0 || (long)index >= cellCount) return false;

            long cellX = (long)(index % columns) * FontSize;
            long cellY = (long)(index / columns) * FontSize;
            if (cellX < 0 || cellX > int.MaxValue ||
                cellY < 0 || cellY > int.MaxValue) return false;

            baseX = (int)cellX;
            baseY = (int)cellY;
            return baseX <= image.Width - FontSize &&
                   baseY <= image.Height - FontSize;
        }

        private bool TryGetPixel(int x, int y, out uint color) {
            color = 0;
            if (image == null || image.RawData == null ||
                x < 0 || y < 0 || x >= image.Width || y >= image.Height) {
                return false;
            }

            long pixelIndex = (long)y * image.Width + x;
            if (pixelIndex < 0 || pixelIndex >= image.RawData.Length) return false;
            color = (uint)image.RawData[(int)pixelIndex];
            return true;
        }

        private int GetGlyphRenderWidth(char chr, int baseX, int baseY) {
            if (chr == ' ') {
                return useFixedWidth ? CharWidth / 2 : FontSize / 2;
            }

            if (useFixedWidth) {
                int width = CharWidth;
                if (width < 0) width = 0;
                if (width > FontSize) width = FontSize;
                return width;
            }

            int renderWidth = FontSize;
            int consecutiveEmptyColumns = 0;
            bool hasContent = false;

            for (int w = 0; w < FontSize; w++) {
                int emptyPixels = 0;
                for (int h = 0; h < FontSize; h++) {
                    uint color;
                    if (!TryGetPixel(baseX + w, baseY + h, out color) ||
                        (color & 0xFF000000u) == 0) {
                        emptyPixels++;
                    } else {
                        hasContent = true;
                    }
                }

                if (emptyPixels == FontSize) {
                    if (hasContent) {
                        consecutiveEmptyColumns++;
                        if (consecutiveEmptyColumns >= 2) {
                            renderWidth = w - consecutiveEmptyColumns + 1;
                            break;
                        }
                    }
                } else {
                    consecutiveEmptyColumns = 0;
                }
            }

            if (renderWidth < 0) renderWidth = 0;
            if (renderWidth > FontSize) renderWidth = FontSize;
            return renderWidth;
        }

        /// <summary>
        /// Return the rendered cell width for a character without touching the
        /// framebuffer. This is also used by measurement, so draw and measure
        /// share exactly the same glyph geometry.
        /// </summary>
        public int GetGlyphWidth(char chr) {
            int baseX, baseY;
            if (chr == ' ') return GetGlyphRenderWidth(chr, 0, 0);
            if (!TryGetGlyphCell(chr, out baseX, out baseY)) return 0;
            return GetGlyphRenderWidth(chr, baseX, baseY);
        }

        /// <summary>
        /// Return whether the atlas contains a valid cell for a character.
        /// Space is a valid glyph even though it has no visible pixels.
        /// </summary>
        public bool HasGlyph(char chr) {
            int baseX, baseY;
            return TryGetGlyphCell(chr, out baseX, out baseY);
        }

        /// <summary>
        /// Return whether a glyph cell contains at least one non-transparent
        /// pixel. This is useful for bounded initialization diagnostics.
        /// </summary>
        public bool GlyphHasPixels(char chr) {
            int baseX, baseY;
            if (!TryGetGlyphCell(chr, out baseX, out baseY)) return false;

            for (int y = 0; y < FontSize; y++) {
                for (int x = 0; x < FontSize; x++) {
                    uint color;
                    if (TryGetPixel(baseX + x, baseY + y, out color) &&
                        (color & 0xFF000000u) != 0) {
                        return true;
                    }
                }
            }
            return false;
        }

        public int DrawChar(Graphics g, int X, int Y, char Chr) {
            int baseX, baseY;
            if (Chr == ' ') return GetGlyphRenderWidth(Chr, 0, 0);
            if (!TryGetGlyphCell(Chr, out baseX, out baseY)) return 0;

            int renderWidth = GetGlyphRenderWidth(Chr, baseX, baseY);
            if (g == null || X == -1 || Y == -1 || renderWidth <= 0) {
                return renderWidth;
            }

            for (int w = 0; w < renderWidth; w++) {
                for (int h = 0; h < FontSize; h++) {
                    uint color;
                    if (TryGetPixel(baseX + w, baseY + h, out color) &&
                        (color & 0xFF000000u) != 0) {
                        // Graphics.DrawPoint owns destination clipping and
                        // performs the existing alpha compositing behavior.
                        long dx = (long)X + w;
                        long dy = (long)Y + h;
                        if (dx >= int.MinValue && dx <= int.MaxValue &&
                            dy >= int.MinValue && dy <= int.MaxValue) {
                            g.DrawPoint((int)dx, (int)dy, color, true);
                        }
                    }
                }
            }

            return renderWidth;
        }

        private int GetAdvance(int glyphWidth) {
            if (glyphWidth <= 0) return 0;
            long advance = (long)glyphWidth + Padding;
            if (advance <= 0) return 0;
            return advance >= int.MaxValue ? int.MaxValue : (int)advance;
        }

        private static bool TryAdd(int origin, int offset, out int result) {
            long value = (long)origin + offset;
            if (value < int.MinValue || value > int.MaxValue) {
                result = 0;
                return false;
            }
            result = (int)value;
            return true;
        }

        private bool TryAdvanceLine(ref int h) {
            if (FontSize <= 0 || FontSize > int.MaxValue - h) return false;
            h += FontSize;
            return true;
        }

        public void DrawString(int X, int Y, string Str, Graphics g) {
            if (Str == null || g == null) return;
            int w = 0;
            int h = 0;
            for (int i = 0; i < Str.Length; i++) {
                int drawX, drawY;
                if (!TryAdd(X, w, out drawX) || !TryAdd(Y, h, out drawY)) return;
                int glyphWidth = DrawChar(g, drawX, drawY, Str[i]);
                w += GetAdvance(glyphWidth);
                if (w < 0) return;
            }
        }

        public void DrawString(int X, int Y, string Str) {
            DrawString(X, Y, Str, Framebuffer.Graphics);
        }

        /// <summary>
        /// Measure the horizontal bounds actually covered by DrawString. The
        /// right edge is based on the last glyph's rendered width, not merely
        /// its pen advance, so the normal atlas's negative padding cannot make
        /// ink extend beyond the measured result.
        /// </summary>
        public int MeasureString(string Str) {
            if (Str == null || !IsValid) return 0;

            int pen = 0;
            int maxRight = 0;
            for (int i = 0; i < Str.Length; i++) {
                int glyphWidth = GetGlyphWidth(Str[i]);
                if (glyphWidth > 0) {
                    long right = (long)pen + glyphWidth;
                    if (right > maxRight) {
                        maxRight = right > int.MaxValue ? int.MaxValue : (int)right;
                    }
                }

                int advance = GetAdvance(glyphWidth);
                if (advance > int.MaxValue - pen) {
                    pen = int.MaxValue;
                    break;
                }
                pen += advance;
            }

            return maxRight > pen ? maxRight : pen;
        }

        public void DrawString(int X, int Y, string Str,
                               int LineLimit = -1, int HeightLimit = -1) {
            if (Str == null || Framebuffer.Graphics == null) return;

            int w = 0;
            int h = 0;
            for (int i = 0; i < Str.Length; i++) {
                char chr = Str[i];
                if (chr == '\n') {
                    w = 0;
                    if (!TryAdvanceLine(ref h)) return;
                    if (HeightLimit != -1 && h >= HeightLimit) return;
                    continue;
                }

                int glyphWidth = GetGlyphWidth(chr);
                if (h != 0 && w == 0 && chr == ' ') continue;
                if (LineLimit != -1 && (long)w + FontSize > LineLimit) {
                    w = 0;
                    if (!TryAdvanceLine(ref h)) return;
                    if (HeightLimit != -1 && h >= HeightLimit) return;
                }

                int drawX, drawY;
                if (!TryAdd(X, w, out drawX) || !TryAdd(Y, h, out drawY)) return;
                DrawChar(Framebuffer.Graphics, drawX, drawY, chr);
                w += GetAdvance(glyphWidth);
                if (w < 0) return;
            }
        }

        // STYLED VARIANTS - Use these for bold/italic support.
        public void DrawStringStyled(int X, int Y, string Str, Graphics g,
                                     FontStyle style) {
            IFont targetFont = GetVariant(style);
            if (targetFont != null) targetFont.DrawString(X, Y, Str, g);
        }

        public void DrawStringStyled(int X, int Y, string Str, FontStyle style) {
            IFont targetFont = GetVariant(style);
            if (targetFont != null) targetFont.DrawString(X, Y, Str);
        }

        public int MeasureStringStyled(string Str, FontStyle style) {
            IFont targetFont = GetVariant(style);
            return targetFont == null ? 0 : targetFont.MeasureString(Str);
        }

        public void DrawStringStyled(int X, int Y, string Str,
                                     int LineLimit, int HeightLimit,
                                     FontStyle style) {
            IFont targetFont = GetVariant(style);
            if (targetFont == null) return;
            targetFont.DrawString(X, Y, Str, LineLimit, HeightLimit);
        }

        public void DiagnoseFont() {
            BootConsole.WriteLine("Font Diagnosis:");
            BootConsole.WriteLine("FontSize: " + FontSize.ToString());
            BootConsole.WriteLine("Image Width: " + (image == null ? 0 : image.Width).ToString() +
                                  ", Height: " + (image == null ? 0 : image.Height).ToString());
            BootConsole.WriteLine("NumRow: " + NumRow.ToString());
            BootConsole.WriteLine("Charset length: " + (charset == null ? 0 : charset.Length).ToString());
            BootConsole.WriteLine("Fixed Width Mode: " + useFixedWidth.ToString() +
                                  ", CharWidth: " + CharWidth.ToString());
            BootConsole.WriteLine("Has Bold: " + (_boldVariant != null).ToString());
            BootConsole.WriteLine("Has Italic: " + (_italicVariant != null).ToString());
            BootConsole.WriteLine("Has BoldItalic: " + (_boldItalicVariant != null).ToString());
        }
    }
}
