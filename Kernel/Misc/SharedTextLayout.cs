using guideXOS.Graph;

namespace guideXOS.Misc {
    /// <summary>
    /// Bounded, reusable word wrapping for a SharedTextFont. The instance owns
    /// only fixed line-index buffers; glyph metrics and rasterization remain
    /// with the selected provider. A caller may reuse one instance for each
    /// draw, avoiding a second font or glyph cache.
    /// </summary>
    public sealed class SharedTextLayout {
        public const int MaximumInputScalars = 4096;
        public const int MaximumLines = 128;
        public const int MaximumLineSpacing = 64;
        public const int MaximumLayoutWork = 16384;

        private readonly int[] _starts = new int[MaximumLines];
        private readonly int[] _lengths = new int[MaximumLines];
        private readonly int[] _widths = new int[MaximumLines];
        private string _text;
        private SharedTextFont _font;
        private int _lineCount;
        private int _lineHeight;
        private int _lineSpacing;
        private int _width;
        private int _height;

        public int LineCount { get { return _lineCount; } }
        public int LineHeight { get { return _lineHeight; } }
        public int LineSpacing { get { return _lineSpacing; } }
        public int Width { get { return _width; } }
        public int Height { get { return _height; } }
        public bool WasTruncated { get; private set; }

        public bool TryLayout(SharedTextFont font, string text, int maxWidth,
                              int lineSpacing) {
            Clear();
            if (font == null || !font.IsAvailable || text == null ||
                maxWidth < 0 || lineSpacing < 0 ||
                lineSpacing > MaximumLineSpacing) return false;

            SharedFontMetrics metrics;
            if (!font.GetFontMetrics(out metrics) || metrics.LineHeight <= 0)
                return false;

            _font = font;
            _text = text;
            _lineHeight = metrics.LineHeight;
            _lineSpacing = lineSpacing;

            // A zero-width rectangle cannot show any glyph. Avoid manufacturing
            // an arbitrarily tall sequence of one-scalar lines for it.
            if (maxWidth == 0) {
                WasTruncated = text.Length != 0;
                return true;
            }

            int inputEnd;
            bool inputWasTruncated;
            if (!FindBoundedInputEnd(text, out inputEnd,
                                     out inputWasTruncated)) return false;
            WasTruncated = inputWasTruncated;

            if (inputEnd == 0) {
                if (!AddLine(0, 0, 0)) return false;
                FinishHeight();
                return true;
            }

            int position = 0;
            bool endsWithBreak = false;
            int work = 0;
            while (position < inputEnd) {
                if (IsLineBreak(text[position])) {
                    int next = ConsumeLineBreak(text, position, inputEnd);
                    if (!AddLine(position, 0, 0)) {
                        WasTruncated = true;
                        break;
                    }
                    position = next;
                    endsWithBreak = position == inputEnd;
                    continue;
                }

                int lineStart = position;
                int scan = position;
                long pen = 0;
                long maxInkRight = 0;
                int breakStart = -1;
                int breakNext = -1;
                long widthBeforeBreak = 0;
                bool completedLine = false;

                while (scan < inputEnd) {
                    if (++work > MaximumLayoutWork) {
                        WasTruncated = true;
                        position = scan;
                        completedLine = true;
                        break;
                    }

                    if (IsLineBreak(text[scan])) {
                        long lineWidth = GetMeasuredWidth(pen, maxInkRight);
                        if (!AddLine(lineStart, scan - lineStart, lineWidth)) {
                            WasTruncated = true;
                            position = inputEnd;
                        } else {
                            position = ConsumeLineBreak(text, scan, inputEnd);
                            endsWithBreak = position == inputEnd;
                        }
                        completedLine = true;
                        break;
                    }

                    int codePoint;
                    int nextScalar;
                    if (!ReadScalar(text, scan, inputEnd, out codePoint,
                                    out nextScalar)) return false;

                    int advance;
                    int inkWidth;
                    if (!font.GetGlyphLayoutMetrics(codePoint, out advance,
                                                     out inkWidth)) return false;
                    long nextPen = pen + advance;
                    long inkRight = pen + inkWidth;
                    long nextMaxInkRight = inkRight > maxInkRight
                        ? inkRight : maxInkRight;
                    long candidateWidth = GetMeasuredWidth(nextPen,
                                                            nextMaxInkRight);

                    if (candidateWidth > maxWidth) {
                        if (scan == lineStart) {
                            // Keep an overwide scalar intact, then let the
                            // caller-owned clip show only its visible portion.
                            if (!AddLine(lineStart, nextScalar - lineStart,
                                         candidateWidth)) {
                                WasTruncated = true;
                                position = inputEnd;
                            } else {
                                position = nextScalar;
                            }
                        } else if (breakStart >= lineStart) {
                            if (breakStart > lineStart &&
                                !AddLine(lineStart, breakStart - lineStart,
                                         widthBeforeBreak)) {
                                WasTruncated = true;
                                position = inputEnd;
                            } else {
                                position = breakNext;
                                while (position < inputEnd && work < MaximumLayoutWork &&
                                       !IsLineBreak(text[position])) {
                                    int whitespaceCodePoint;
                                    int whitespaceNext;
                                    if (!ReadScalar(text, position, inputEnd,
                                            out whitespaceCodePoint,
                                            out whitespaceNext) ||
                                        !IsWrapWhitespace(whitespaceCodePoint)) break;
                                    position = whitespaceNext;
                                    work++;
                                }
                                if (position >= inputEnd) endsWithBreak = false;
                            }
                        } else {
                            long lineWidth = GetMeasuredWidth(pen,
                                                              maxInkRight);
                            if (!AddLine(lineStart, scan - lineStart,
                                         lineWidth)) {
                                WasTruncated = true;
                                position = inputEnd;
                            } else {
                                // The scalar that overflowed is retried on the
                                // next line; scan is a scalar boundary.
                                position = scan;
                            }
                        }
                        endsWithBreak = false;
                        completedLine = true;
                        break;
                    }

                    if (IsWrapWhitespace(codePoint)) {
                        breakStart = scan;
                        breakNext = nextScalar;
                        widthBeforeBreak = GetMeasuredWidth(pen,
                                                            maxInkRight);
                    }
                    pen = nextPen;
                    maxInkRight = nextMaxInkRight;
                    scan = nextScalar;
                    endsWithBreak = false;
                }

                if (!completedLine) {
                    long lineWidth = GetMeasuredWidth(pen, maxInkRight);
                    if (!AddLine(lineStart, scan - lineStart, lineWidth)) {
                        WasTruncated = true;
                        break;
                    }
                    position = scan;
                    endsWithBreak = false;
                }

                if (work >= MaximumLayoutWork && position < inputEnd) {
                    WasTruncated = true;
                    break;
                }
            }

            if (endsWithBreak && inputEnd == text.Length) {
                if (_lineCount < MaximumLines) AddLine(inputEnd, 0, 0);
                else WasTruncated = true;
            }
            if (position < inputEnd) WasTruncated = true;
            FinishHeight();
            return true;
        }

        public int GetLineStart(int line) {
            return line < 0 || line >= _lineCount ? 0 : _starts[line];
        }

        public int GetLineLength(int line) {
            return line < 0 || line >= _lineCount ? 0 : _lengths[line];
        }

        public int GetLineWidth(int line) {
            return line < 0 || line >= _lineCount ? 0 : _widths[line];
        }

        public bool Draw(Graphics graphics, int x, int y, uint color,
                         int clipX, int clipY, int clipWidth, int clipHeight,
                         SharedTextAlignment alignment) {
            if (_font == null || _text == null || !_font.IsAvailable ||
                graphics == null || clipWidth < 0 || clipHeight < 0) return false;

            if (clipWidth == 0 || clipHeight == 0) return true;
            long clipRight = (long)clipX + clipWidth;
            long clipBottom = (long)clipY + clipHeight;
            for (int i = 0; i < _lineCount; i++) {
                long lineY = (long)y + (long)i *
                    ((long)_lineHeight + _lineSpacing);
                if (lineY >= clipBottom) break;
                if (lineY + _lineHeight <= clipY ||
                    lineY < int.MinValue || lineY > int.MaxValue) continue;

                long lineX = x;
                if (alignment == SharedTextAlignment.Center)
                    lineX += ((long)clipWidth - _widths[i]) / 2;
                else if (alignment == SharedTextAlignment.Right)
                    lineX += (long)clipWidth - _widths[i];
                if (lineX < int.MinValue) lineX = int.MinValue;
                if (lineX > int.MaxValue) lineX = int.MaxValue;

                _font.DrawTextRange(graphics, (int)lineX, (int)lineY,
                    _text, _starts[i], _lengths[i], color, clipX, clipY,
                    clipWidth, clipHeight);
            }
            return true;
        }

        private void Clear() {
            _font = null;
            _text = null;
            _lineCount = 0;
            _lineHeight = 0;
            _lineSpacing = 0;
            _width = 0;
            _height = 0;
            WasTruncated = false;
        }

        private bool FindBoundedInputEnd(string text, out int end,
                                         out bool truncated) {
            end = 0;
            truncated = false;
            int scalars = 0;
            while (end < text.Length && scalars < MaximumInputScalars) {
                int codePoint;
                int next;
                if (!ReadScalar(text, end, text.Length, out codePoint,
                                out next)) return false;
                end = next;
                scalars++;
            }
            truncated = end < text.Length;
            return true;
        }

        private bool AddLine(int start, int length, long width) {
            if (_lineCount >= MaximumLines) return false;
            _starts[_lineCount] = start;
            _lengths[_lineCount] = length;
            _widths[_lineCount] = width <= 0 ? 0 :
                width > int.MaxValue ? int.MaxValue : (int)width;
            if (_widths[_lineCount] > _width) _width = _widths[_lineCount];
            _lineCount++;
            return true;
        }

        private void FinishHeight() {
            long height = (long)_lineCount * _lineHeight +
                (long)(_lineCount > 0 ? _lineCount - 1 : 0) * _lineSpacing;
            _height = height > int.MaxValue ? int.MaxValue : (int)height;
        }

        private static long GetMeasuredWidth(long pen, long inkRight) {
            return pen > inkRight ? pen : inkRight;
        }

        private static bool ReadScalar(string text, int start, int end,
                                       out int codePoint, out int next) {
            codePoint = 0;
            next = start;
            if (text == null || start < 0 || start >= end || end > text.Length)
                return false;
            char first = text[start];
            codePoint = first;
            next = start + 1;
            if (char.IsHighSurrogate(first) && next < end &&
                char.IsLowSurrogate(text[next])) {
                codePoint = 0x10000 + ((first - 0xD800) << 10) +
                            (text[next] - 0xDC00);
                next++;
            }
            return true;
        }

        private static bool IsLineBreak(char value) {
            return value == '\r' || value == '\n';
        }

        private static int ConsumeLineBreak(string text, int start, int end) {
            if (text[start] == '\r' && start + 1 < end &&
                text[start + 1] == '\n') return start + 2;
            return start + 1;
        }

        private static bool IsWrapWhitespace(int codePoint) {
            return codePoint == 0x20 || (codePoint >= 0x09 && codePoint <= 0x0D) ||
                   codePoint == 0x85 || codePoint == 0xA0 || codePoint == 0x1680 ||
                   (codePoint >= 0x2000 && codePoint <= 0x200A) ||
                   codePoint == 0x2028 || codePoint == 0x2029 ||
                   codePoint == 0x202F || codePoint == 0x205F ||
                   codePoint == 0x3000;
        }
    }
}
