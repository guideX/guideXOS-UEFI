using guideXOS.Graph;

namespace guideXOS.Misc {
    public enum SharedFontProviderKind {
        BitmapAtlas,
        TrueType
    }

    public enum SharedTextAlignment {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Vertical metrics use a top-left text-box coordinate system. Ascent is
    /// positive above the baseline, descent is signed (normally negative),
    /// and Baseline is the baseline's Y offset from the first line's top.
    /// </summary>
    public struct SharedFontMetrics {
        public int Ascent;
        public int Descent;
        public int LineGap;
        public int LineHeight;
        public int Baseline;
    }

    public struct SharedTextMeasurement {
        public int Width;
        public int Height;
        public int LineCount;
    }

    /// <summary>
    /// Stable, per-font measurement and drawing handle shared by desktop text
    /// surfaces. The handle owns no font data: bitmap atlas and TrueType
    /// lifetimes remain with their existing owners. Layout should retain and
    /// use the same handle for measurement and drawing.
    /// </summary>
    public sealed class SharedTextFont {
        private readonly IFont _bitmap;
        private readonly TrueTypeFont _trueType;
        private readonly SharedFontProviderKind _provider;
        private int _drawCalls;

        private SharedTextFont(IFont bitmap) {
            _bitmap = bitmap;
            _provider = SharedFontProviderKind.BitmapAtlas;
        }

        private SharedTextFont(TrueTypeFont trueType) {
            _trueType = trueType;
            _provider = SharedFontProviderKind.TrueType;
        }

        public SharedFontProviderKind Provider { get { return _provider; } }
        public int DrawCalls { get { return _drawCalls; } }
        public int TrueTypeFontSize {
            get { return _trueType == null ? 0 : _trueType.FontSize; }
        }
        public int TrueTypeRasterizations {
            get { return _trueType == null ? 0 : _trueType.Rasterizations; }
        }
        public int TrueTypeCacheHits {
            get { return _trueType == null ? 0 : _trueType.CacheHits; }
        }
        public int TrueTypeCacheMisses {
            get { return _trueType == null ? 0 : _trueType.CacheMisses; }
        }
        public int TrueTypeCacheEntryCount {
            get { return _trueType == null ? 0 : _trueType.CacheEntryCount; }
        }
        public int TrueTypeCacheBytes {
            get { return _trueType == null ? 0 : _trueType.CacheBytes; }
        }
        internal TrueTypeFont TrueTypeRenderer { get { return _trueType; } }

        public int FontSize {
            get {
                return _provider == SharedFontProviderKind.TrueType
                    ? (_trueType == null ? 0 : _trueType.FontSize)
                    : (_bitmap == null ? 0 : _bitmap.FontSize);
            }
        }

        public bool IsAvailable {
            get {
                return _provider == SharedFontProviderKind.TrueType
                    ? IsUsableTrueType(_trueType)
                    : _bitmap != null && _bitmap.IsValid;
            }
        }

        /// <summary>Resolve the production default to the existing bitmap atlas.</summary>
        internal static SharedTextFont CreateDefaultBitmap(IFont bitmap) {
            return bitmap == null ? null : new SharedTextFont(bitmap);
        }

        /// <summary>
        /// Explicitly select a hosted or diagnostic TrueType face. On any
        /// invalid handle or unsupported desktop size, selected receives the
        /// bitmap fallback and the method returns false.
        /// </summary>
        internal static bool TrySelectTrueType(TrueTypeFont trueType,
                                               IFont bitmapFallback,
                                               out SharedTextFont selected) {
            if (IsUsableTrueType(trueType)) {
                selected = new SharedTextFont(trueType);
                return true;
            }
            selected = bitmapFallback == null
                ? null : new SharedTextFont(bitmapFallback);
            return false;
        }

        private static bool IsUsableTrueType(TrueTypeFont font) {
            if (font == null || !font.IsValid) return false;
            switch (font.FontSize) {
                case 12:
                case 16:
                case 20:
                case 24:
                case 32:
                    return true;
                default:
                    return false;
            }
        }

        public bool GetFontMetrics(out SharedFontMetrics metrics) {
            metrics = new SharedFontMetrics();
            if (!IsAvailable) return false;

            if (_provider == SharedFontProviderKind.TrueType) {
                metrics.Ascent = _trueType.Ascent;
                metrics.Descent = _trueType.Descent;
                metrics.LineGap = _trueType.LineGap;
                metrics.LineHeight = _trueType.LineHeight;
                metrics.Baseline = _trueType.Ascent;
            } else {
                // Atlas cells are drawn from their top row and occupy exactly
                // FontSize rows, so their established baseline offset is the
                // bottom of the cell.
                metrics.Ascent = _bitmap.FontSize;
                metrics.Descent = 0;
                metrics.LineGap = 0;
                metrics.LineHeight = _bitmap.FontSize;
                metrics.Baseline = _bitmap.FontSize;
            }
            return metrics.LineHeight > 0;
        }

        public bool GetLineHeight(out int lineHeight) {
            SharedFontMetrics metrics;
            bool success = GetFontMetrics(out metrics);
            lineHeight = success ? metrics.LineHeight : 0;
            return success;
        }

        public bool GetBaseline(out int baseline) {
            SharedFontMetrics metrics;
            bool success = GetFontMetrics(out metrics);
            baseline = success ? metrics.Baseline : 0;
            return success;
        }

        public bool GetGlyphAdvance(int codePoint, out int advance) {
            advance = 0;
            if (!IsAvailable || codePoint < 0 || codePoint > 0x10FFFF ||
                (codePoint >= 0xD800 && codePoint <= 0xDFFF)) return false;
            if (_provider == SharedFontProviderKind.TrueType) {
                advance = _trueType.GetAdvanceWidth(codePoint);
            } else if (codePoint <= 0xFFFF) {
                advance = _bitmap.GetGlyphAdvance((char)codePoint);
            }
            return true;
        }

        /// <summary>
        /// Return the provider's pen advance and the ink width used by its
        /// measurement contract for one Unicode scalar. Bitmap measurement
        /// includes visible cell width because atlas padding may be negative;
        /// TrueType measurement follows its hmtx advances.
        /// </summary>
        internal bool GetGlyphLayoutMetrics(int codePoint, out int advance,
                                            out int inkWidth) {
            advance = 0;
            inkWidth = 0;
            if (!GetGlyphAdvance(codePoint, out advance)) return false;
            if (_provider == SharedFontProviderKind.TrueType) {
                inkWidth = advance;
            } else if (codePoint <= 0xFFFF) {
                inkWidth = _bitmap.GetGlyphWidth((char)codePoint);
            }
            return true;
        }

        public bool MeasureText(string text, out SharedTextMeasurement measurement) {
            return MeasureTextRange(text, 0, text == null ? 0 : text.Length,
                                    out measurement);
        }

        /// <summary>
        /// Measure a UTF-16 range without allocating a substring. This keeps
        /// single-line truncation and drawing on the same provider metrics.
        /// </summary>
        internal bool MeasureTextRange(string text, int start, int length,
                                       out SharedTextMeasurement measurement) {
            measurement = new SharedTextMeasurement();
            if (!IsAvailable || text == null || start < 0 || length < 0 ||
                start > text.Length - length) return false;

            int lineHeight;
            if (!GetLineHeight(out lineHeight)) return false;
            int lineCount = 1;
            long maxWidth = 0;
            int end = start + length;
            int lineStart = start;
            for (int i = start; i <= end; i++) {
                bool atEnd = i == end;
                char current = atEnd ? '\0' : text[i];
                if (!atEnd && current != '\r' && current != '\n') continue;

                int lineWidth = _provider == SharedFontProviderKind.TrueType
                    ? _trueType.MeasureString(text, lineStart, i - lineStart)
                    : _bitmap.MeasureString(text, lineStart, i - lineStart);
                if (lineWidth > maxWidth) maxWidth = lineWidth;
                if (!atEnd) {
                    if (current == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    lineStart = i + 1;
                    lineCount++;
                }
            }

            long height = (long)lineHeight * lineCount;
            measurement.Width = maxWidth > int.MaxValue ? int.MaxValue : (int)maxWidth;
            measurement.Height = height > int.MaxValue ? int.MaxValue : (int)height;
            measurement.LineCount = lineCount;
            return true;
        }

        /// <summary>
        /// Draw at a top-left first-line origin. Alignment is within
        /// alignmentWidth pixels beginning at x. Clipping uses half-open bounds.
        /// Bitmap colors remain those baked into the atlas; TrueType uses color.
        /// </summary>
        public bool DrawText(Graphics graphics, int x, int y, string text,
                             uint color, int clipX, int clipY,
                             int clipWidth, int clipHeight,
                             SharedTextAlignment alignment,
                             int alignmentWidth) {
            if (!IsAvailable || graphics == null || text == null ||
                clipWidth < 0 || clipHeight < 0 || alignmentWidth < 0) return false;

            SharedTextMeasurement measurement;
            if (!MeasureText(text, out measurement)) return false;
            long offset = 0;
            switch (alignment) {
                case SharedTextAlignment.Center:
                    offset = ((long)alignmentWidth - measurement.Width) / 2;
                    break;
                case SharedTextAlignment.Right:
                    offset = (long)alignmentWidth - measurement.Width;
                    break;
                case SharedTextAlignment.Left:
                default:
                    break;
            }
            long origin = (long)x + offset;
            if (origin < int.MinValue) origin = int.MinValue;
            if (origin > int.MaxValue) origin = int.MaxValue;

            if (_provider == SharedFontProviderKind.TrueType) {
                _trueType.DrawStringClipped(graphics, (int)origin, y, text,
                    color, clipX, clipY, clipWidth, clipHeight);
            } else {
                _bitmap.DrawStringClipped((int)origin, y, text, graphics,
                    clipX, clipY, clipWidth, clipHeight);
            }
            RecordDrawCall();
            return true;
        }

        public bool DrawText(Graphics graphics, int x, int y, string text,
                             uint color, int clipX, int clipY,
                             int clipWidth, int clipHeight) {
            return DrawText(graphics, x, y, text, color, clipX, clipY,
                clipWidth, clipHeight, SharedTextAlignment.Left, 0);
        }

        /// <summary>
        /// Draw a measured UTF-16 range without allocating a substring. The
        /// caller must keep start/length on Unicode scalar boundaries.
        /// </summary>
        internal bool DrawTextRange(Graphics graphics, int x, int y,
                                    string text, int start, int length,
                                    uint color, int clipX, int clipY,
                                    int clipWidth, int clipHeight) {
            if (!IsAvailable || graphics == null || text == null || start < 0 ||
                length < 0 || start > text.Length - length || clipWidth < 0 ||
                clipHeight < 0) return false;
            if (_provider == SharedFontProviderKind.TrueType) {
                _trueType.DrawStringRangeClipped(graphics, x, y, text, start,
                    length, color, clipX, clipY, clipWidth, clipHeight);
            } else {
                _bitmap.DrawStringRangeClipped(x, y, text, start, length,
                    graphics, clipX, clipY, clipWidth, clipHeight);
            }
            RecordDrawCall();
            return true;
        }

        internal void RecordLegacyBitmapDrawCall() {
            if (_provider != SharedFontProviderKind.BitmapAtlas) return;
            RecordDrawCall();
        }

        private void RecordDrawCall() {
            if (_drawCalls < int.MaxValue) _drawCalls++;
        }
    }

    /// <summary>
    /// Holds the selected shared provider for shell text surfaces. The bitmap
    /// atlas remains the default and is restored automatically if its owner is
    /// replaced. TrueType opt-in is delegated to SharedTextFont's existing
    /// validated selector; this object stores only the resulting shared handle.
    /// </summary>
    internal sealed class SharedTextFontSelection {
        private IFont _bitmapOwner;
        private SharedTextFont _selected;
        private bool _initialized;

        public int Generation { get; private set; }
        public SharedFontProviderKind RequestedProvider { get; private set; }
        public SharedFontProviderKind InitializedProvider { get; private set; }
        public bool HasInitializedProvider { get; private set; }
        public string FallbackReason { get; private set; }
        public SharedTextFont Selected { get { return _selected; } }
        public bool HasProviderBeenUsed {
            get { return _selected != null && _selected.DrawCalls > 0; }
        }
        public SharedFontProviderKind ProviderActuallyUsed {
            get { return _selected == null ? SharedFontProviderKind.BitmapAtlas : _selected.Provider; }
        }

        public SharedTextFont Resolve(IFont bitmap) {
            if (!_initialized || !object.ReferenceEquals(bitmap, _bitmapOwner)) {
                _initialized = true;
                _bitmapOwner = bitmap;
                _selected = SharedTextFont.CreateDefaultBitmap(bitmap);
                RequestedProvider = SharedFontProviderKind.BitmapAtlas;
                InitializedProvider = SharedFontProviderKind.BitmapAtlas;
                HasInitializedProvider = _selected != null && _selected.IsAvailable;
                FallbackReason = null;
                BumpGeneration();
            }
            return _selected;
        }

        public bool TrySelectTrueType(TrueTypeFont trueType, IFont bitmap,
                                      out SharedTextFont selected) {
            Resolve(bitmap);
            if (trueType != null && _selected != null &&
                    _selected.Provider == SharedFontProviderKind.TrueType &&
                    object.ReferenceEquals(_selected.TrueTypeRenderer,
                                           trueType)) {
                RequestedProvider = SharedFontProviderKind.TrueType;
                InitializedProvider = SharedFontProviderKind.TrueType;
                HasInitializedProvider = true;
                FallbackReason = null;
                selected = _selected;
                return true;
            }
            bool success = SharedTextFont.TrySelectTrueType(trueType, bitmap,
                                                              out selected);
            RequestedProvider = SharedFontProviderKind.TrueType;
            _selected = selected;
            HasInitializedProvider = selected != null && selected.IsAvailable;
            InitializedProvider = selected == null
                ? SharedFontProviderKind.BitmapAtlas : selected.Provider;
            FallbackReason = success ? null : GetTrueTypeFailure(trueType);
            if (success || selected != null) BumpGeneration();
            return success;
        }

        public bool TrySelectTrueType(byte[] fontData, int fontSize,
                                      IFont bitmap,
                                      out SharedTextFont selected) {
            Resolve(bitmap);
            if (_selected != null &&
                    _selected.Provider == SharedFontProviderKind.TrueType &&
                    _selected.TrueTypeFontSize == fontSize &&
                    _selected.TrueTypeRenderer.MatchesSourceBytes(fontData)) {
                RequestedProvider = SharedFontProviderKind.TrueType;
                InitializedProvider = SharedFontProviderKind.TrueType;
                HasInitializedProvider = true;
                FallbackReason = null;
                selected = _selected;
                return true;
            }
            TrueTypeFont parsed = new TrueTypeFont(fontData, fontSize);
            return TrySelectTrueType(parsed, bitmap, out selected);
        }

        public void SelectBitmap(IFont bitmap) {
            Resolve(bitmap);
            RequestedProvider = SharedFontProviderKind.BitmapAtlas;
            InitializedProvider = SharedFontProviderKind.BitmapAtlas;
            HasInitializedProvider = _selected != null && _selected.IsAvailable;
            FallbackReason = null;
            if (_selected == null ||
                    _selected.Provider != SharedFontProviderKind.BitmapAtlas ||
                    !object.ReferenceEquals(bitmap, _bitmapOwner)) {
                SharedTextFont next = SharedTextFont.CreateDefaultBitmap(bitmap);
                _selected = next;
                if (next != null) HasInitializedProvider = next.IsAvailable;
                BumpGeneration();
            }
        }

        private static string GetTrueTypeFailure(TrueTypeFont trueType) {
            if (trueType == null) return "font-initialization-failed";
            if (!trueType.IsValid) return trueType.LoadFailure ??
                "font-initialization-failed";
            return "unsupported-size";
        }

        private void BumpGeneration() {
            if (Generation < int.MaxValue) Generation++;
        }
    }
}
