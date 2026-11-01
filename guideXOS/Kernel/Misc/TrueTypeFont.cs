using System;
using System.Drawing;
using guideXOS.Graph;

namespace guideXOS.Misc {
    /// <summary>
    /// A read-only, tightly packed grayscale glyph produced from a TrueType
    /// outline. BearingX/BearingY are relative to the pen origin and baseline.
    /// </summary>
    public sealed class TrueTypeGlyph {
        private readonly byte[] _coverage;

        public int GlyphIndex { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Advance { get; private set; }
        public int BearingX { get; private set; }
        public int BearingY { get; private set; }
        public int LeftSideBearing { get; private set; }

        internal byte[] Coverage { get { return _coverage; } }

        internal TrueTypeGlyph(int glyphIndex, int width, int height,
                               int advance, int bearingX, int bearingY,
                               int leftSideBearing, byte[] coverage) {
            GlyphIndex = glyphIndex;
            Width = width;
            Height = height;
            Advance = advance;
            BearingX = bearingX;
            BearingY = bearingY;
            LeftSideBearing = leftSideBearing;
            _coverage = coverage ?? new byte[0];
        }

        public byte GetCoverage(int x, int y) {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
            int index = y * Width + x;
            return index >= 0 && index < _coverage.Length ? _coverage[index] : (byte)0;
        }
    }

    /// <summary>
    /// Bounded unhinted TrueType glyf renderer for the managed UEFI desktop.
    ///
    /// This implementation accepts one standalone TrueType sfnt face, validates
    /// all table ranges before retaining a private copy, decodes cmap 4/12 and
    /// simple/composite glyf outlines, and rasterizes quadratic contours into
    /// 8-bit coverage. It deliberately does not run font bytecode.
    ///
    /// DrawString takes a top-of-line Y coordinate. Measurement and drawing use
    /// the same hmtx advances. The renderer is opt-in; existing IFont bitmap
    /// atlas and boot text paths remain the deterministic desktop fallback.
    /// </summary>
    public class TrueTypeFont {
        public const int MinimumPixelSize = 1;
        public const int MaximumPixelSize = 72;
        public const int MaximumFontBytes = 512 * 1024;
        public const int MaximumTables = 64;
        public const int MaximumGlyphs = 16384;
        public const int MaximumOutlinePoints = 1024;
        public const int MaximumContours = 128;
        public const int MaximumCompositeComponents = 32;
        public const int MaximumCompositeDepth = 8;
        public const int MaximumGlyphDimension = 128;
        public const int MaximumCacheEntries = 64;
        public const int MaximumCacheBytes = 64 * 1024;
        public const int MaximumCachedGlyphBytes = 4096;

        private const int CurveSteps = 8;
        private const int SamplesPerAxis = 4;
        private const int MaximumCmapRecords = 64;
        private const int MaximumCmapGroups = 4096;
        private const int MaximumCmapSegments = 8192;
        private const int MaximumCoordinate = 1000000;

        private const uint TagHead = 0x68656164;
        private const uint TagMaxp = 0x6D617870;
        private const uint TagHhea = 0x68686561;
        private const uint TagHmtx = 0x686D7478;
        private const uint TagLoca = 0x6C6F6361;
        private const uint TagGlyf = 0x676C7966;
        private const uint TagCmap = 0x636D6170;

        private const ushort ArgWords = 0x0001;
        private const ushort ArgsAreXY = 0x0002;
        private const ushort RoundXYToGrid = 0x0004;
        private const ushort HaveScale = 0x0008;
        private const ushort MoreComponents = 0x0020;
        private const ushort HaveXYScale = 0x0040;
        private const ushort Have2x2 = 0x0080;
        private const ushort HaveInstructions = 0x0100;
        private const ushort UseMyMetrics = 0x0200;
        private const ushort ScaledComponentOffset = 0x0800;
        private const ushort UnscaledComponentOffset = 0x1000;

        private struct TableRecord {
            public uint Tag;
            public int Offset;
            public int Length;
        }

        private struct GlyphPoint {
            public double X;
            public double Y;
            public bool OnCurve;

            public GlyphPoint(double x, double y, bool onCurve) {
                X = x;
                Y = y;
                OnCurve = onCurve;
            }
        }

        private sealed class Outline {
            public readonly GlyphPoint[] Points = new GlyphPoint[MaximumOutlinePoints];
            public readonly int[] EndPoints = new int[MaximumContours];
            public int PointCount;
            public int ContourCount;
            public int MetricsGlyphIndex;
        }

        private struct Edge {
            public double X0;
            public double Y0;
            public double X1;
            public double Y1;
        }

        private struct Intersection {
            public double X;
            public int Direction;
        }

        private sealed class CacheEntry {
            public int GlyphIndex;
            public long LastUse;
            public TrueTypeGlyph Glyph;
        }

        private byte[] _data;
        private TableRecord[] _tables;
        private int _tableCount;
        private int[] _glyphOffsets;
        private ushort[] _advanceWidths;
        private short[] _leftSideBearings;
        private int _unitsPerEm;
        private int _numGlyphs;
        private int _numHMetrics;
        private short _ascentUnits;
        private short _descentUnits;
        private short _lineGapUnits;
        private int _locaFormat;
        private int _glyfOffset;
        private int _glyfLength;
        private int _cmapOffset;
        private int _cmapLength;
        private int _cmapFormat;
        private double _scale;
        private CacheEntry[] _cache;
        private long _cacheClock;
        private int _cacheBytes;
        private int _cacheCount;

        public int FontSize { get; private set; }
        public int Ascent { get; private set; }
        public int Descent { get; private set; }
        public int LineGap { get; private set; }
        public int LineHeight { get; private set; }
        public bool IsValid { get; private set; }
        public string LoadFailure { get; private set; }
        public int GlyphCount { get { return _numGlyphs; } }
        public int CmapFormat { get { return _cmapFormat; } }
        public int CacheEntryCount { get { return _cacheCount; } }
        public int CacheBytes { get { return _cacheBytes; } }
        public int CacheHits { get; private set; }
        public int CacheMisses { get; private set; }
        public int Rasterizations { get; private set; }
        public int RasterizationFailures { get; private set; }

        public TrueTypeFont(byte[] fontData, int fontSize = 16) {
            FontSize = fontSize;
            LoadFailure = "not-initialized";
            if (fontData == null || fontData.Length < 12) {
                LoadFailure = "source-too-short";
                return;
            }
            if (fontData.Length > MaximumFontBytes) {
                LoadFailure = "source-limit";
                return;
            }
            if (fontSize < MinimumPixelSize || fontSize > MaximumPixelSize) {
                LoadFailure = "size-limit";
                return;
            }

            // The renderer owns an immutable snapshot. Mutating the caller's
            // input after validation cannot invalidate a table or cached glyph.
            _data = new byte[fontData.Length];
            for (int i = 0; i < fontData.Length; i++) _data[i] = fontData[i];
            _tables = new TableRecord[MaximumTables];
            _cache = new CacheEntry[MaximumCacheEntries];
            _scale = (double)fontSize / 2048.0;

            if (!Parse()) return;

            _scale = (double)FontSize / _unitsPerEm;
            Ascent = ScaleCeiling(_ascentUnits);
            Descent = ScaleFloor(_descentUnits);
            LineGap = ScaleRounded(_lineGapUnits);
            int lineUnits = (int)_ascentUnits - _descentUnits + _lineGapUnits;
            LineHeight = ScaleCeiling(lineUnits);
            if (LineHeight < 1) LineHeight = FontSize;
            if (LineHeight > MaximumGlyphDimension * 2) {
                LoadFailure = "line-metric-limit";
                return;
            }

            IsValid = true;
            LoadFailure = null;
        }

        public static bool TryCreate(byte[] fontData, int fontSize,
                                     out TrueTypeFont font) {
            font = new TrueTypeFont(fontData, fontSize);
            if (font.IsValid) return true;
            font = null;
            return false;
        }

        private bool Parse() {
            uint signature = U32(0);
            if (signature != 0x00010000u && signature != 0x74727565u) {
                LoadFailure = "unsupported-sfnt-signature";
                return false;
            }

            int tableCount = U16(4);
            if (tableCount <= 0 || tableCount > MaximumTables ||
                !HasRange(0, 12 + tableCount * 16)) {
                LoadFailure = "table-directory-limit";
                return false;
            }
            _tableCount = tableCount;
            int directoryEnd = 12 + tableCount * 16;

            for (int i = 0; i < tableCount; i++) {
                int record = 12 + i * 16;
                uint tag = U32(record);
                uint rawOffset = U32(record + 8);
                uint rawLength = U32(record + 12);
                if (rawOffset > int.MaxValue || rawLength > int.MaxValue) {
                    LoadFailure = "table-range-overflow";
                    return false;
                }

                int offset = (int)rawOffset;
                int length = (int)rawLength;
                if (!HasRange(offset, length) ||
                    (length != 0 && (offset < directoryEnd || (offset & 3) != 0))) {
                    LoadFailure = "table-range-invalid";
                    return false;
                }

                for (int previous = 0; previous < i; previous++) {
                    TableRecord other = _tables[previous];
                    if (other.Tag == tag) {
                        LoadFailure = "duplicate-table";
                        return false;
                    }
                    if (length > 0 && other.Length > 0 &&
                        RangesOverlap(offset, length, other.Offset, other.Length)) {
                        LoadFailure = "overlapping-tables";
                        return false;
                    }
                }
                _tables[i].Tag = tag;
                _tables[i].Offset = offset;
                _tables[i].Length = length;
            }

            TableRecord head, maxp, hhea, hmtx, loca, glyf, cmap;
            if (!FindTable(TagHead, out head) || head.Length < 54 ||
                !FindTable(TagMaxp, out maxp) || maxp.Length < 6 ||
                !FindTable(TagHhea, out hhea) || hhea.Length < 36 ||
                !FindTable(TagHmtx, out hmtx) ||
                !FindTable(TagLoca, out loca) ||
                !FindTable(TagGlyf, out glyf) ||
                !FindTable(TagCmap, out cmap) || cmap.Length < 4) {
                LoadFailure = "required-table-missing-or-short";
                return false;
            }

            if (U32(head.Offset) != 0x00010000u ||
                U32(head.Offset + 12) != 0x5F0F3CF5u ||
                U32(maxp.Offset) != 0x00010000u ||
                U32(hhea.Offset) != 0x00010000u ||
                U16(cmap.Offset) != 0) {
                LoadFailure = "head-magic-invalid";
                return false;
            }
            _unitsPerEm = U16(head.Offset + 18);
            _locaFormat = I16(head.Offset + 50);
            _numGlyphs = U16(maxp.Offset + 4);
            _ascentUnits = I16(hhea.Offset + 4);
            _descentUnits = I16(hhea.Offset + 6);
            _lineGapUnits = I16(hhea.Offset + 8);
            _numHMetrics = U16(hhea.Offset + 34);

            if (_unitsPerEm < 16 || _unitsPerEm > 16384 ||
                _numGlyphs <= 0 || _numGlyphs > MaximumGlyphs ||
                _numHMetrics <= 0 || _numHMetrics > _numGlyphs ||
                (_locaFormat != 0 && _locaFormat != 1) ||
                (long)_ascentUnits - _descentUnits + _lineGapUnits <= 0) {
                LoadFailure = "font-metric-limit";
                return false;
            }

            long requiredHmtx = (long)_numHMetrics * 4L +
                                (long)(_numGlyphs - _numHMetrics) * 2L;
            long requiredLoca = (long)(_numGlyphs + 1) * (_locaFormat == 0 ? 2L : 4L);
            if (requiredHmtx > hmtx.Length || requiredLoca > loca.Length ||
                glyf.Length <= 0) {
                LoadFailure = "glyph-table-size-invalid";
                return false;
            }

            _glyfOffset = glyf.Offset;
            _glyfLength = glyf.Length;
            if (!ParseHorizontalMetrics(hmtx) || !ParseGlyphLocations(loca) ||
                !ParseCmap(cmap)) return false;
            return true;
        }

        private bool ParseHorizontalMetrics(TableRecord table) {
            _advanceWidths = new ushort[_numGlyphs];
            _leftSideBearings = new short[_numGlyphs];
            int cursor = table.Offset;
            ushort lastAdvance = 0;
            for (int i = 0; i < _numHMetrics; i++) {
                lastAdvance = U16(cursor);
                _advanceWidths[i] = lastAdvance;
                _leftSideBearings[i] = I16(cursor + 2);
                cursor += 4;
            }
            for (int i = _numHMetrics; i < _numGlyphs; i++) {
                _advanceWidths[i] = lastAdvance;
                _leftSideBearings[i] = I16(cursor);
                cursor += 2;
            }
            return true;
        }

        private bool ParseGlyphLocations(TableRecord table) {
            _glyphOffsets = new int[_numGlyphs + 1];
            int cursor = table.Offset;
            int previous = 0;
            for (int i = 0; i <= _numGlyphs; i++) {
                uint raw = _locaFormat == 0 ? (uint)(U16(cursor) * 2) : U32(cursor);
                if (raw > _glyfLength || raw > int.MaxValue || raw < previous) {
                    LoadFailure = "loca-offset-invalid";
                    return false;
                }
                previous = (int)raw;
                _glyphOffsets[i] = previous;
                cursor += _locaFormat == 0 ? 2 : 4;
            }
            return true;
        }

        private bool ParseCmap(TableRecord table) {
            int tableEnd = table.Offset + table.Length;
            int recordCount = U16(table.Offset + 2);
            if (recordCount <= 0 || recordCount > MaximumCmapRecords ||
                !HasRange(table.Offset + 4, recordCount * 8) ||
                table.Offset + 4 + recordCount * 8 > tableEnd) {
                LoadFailure = "cmap-record-limit";
                return false;
            }

            int format4Offset = -1;
            int format4Length = 0;
            int format12Offset = -1;
            int format12Length = 0;
            for (int i = 0; i < recordCount; i++) {
                int record = table.Offset + 4 + i * 8;
                int platform = U16(record);
                int encoding = U16(record + 2);
                uint relative = U32(record + 4);
                if (relative > int.MaxValue ||
                    (long)table.Offset + relative + 2 > tableEnd) continue;

                int subtable = table.Offset + (int)relative;
                int format = U16(subtable);
                bool unicode = platform == 0 ||
                               (platform == 3 && (encoding == 1 || encoding == 10));
                if (!unicode) continue;

                if (format == 12 && (platform == 0 || encoding == 10) &&
                    (long)subtable + 16 <= tableEnd) {
                    uint rawLength = U32(subtable + 4);
                    if (rawLength <= int.MaxValue &&
                        ValidateCmapFormat12(subtable, (int)rawLength) &&
                        format12Offset < 0) {
                        format12Offset = subtable;
                        format12Length = (int)rawLength;
                    }
                } else if (format == 4 && format4Offset < 0 &&
                           (long)subtable + 4 <= tableEnd) {
                    int length = U16(subtable + 2);
                    if (ValidateCmapFormat4(subtable, length)) {
                        format4Offset = subtable;
                        format4Length = length;
                    }
                }
            }

            if (format12Offset >= 0) {
                _cmapOffset = format12Offset;
                _cmapLength = format12Length;
                _cmapFormat = 12;
                return true;
            }
            if (format4Offset >= 0) {
                _cmapOffset = format4Offset;
                _cmapLength = format4Length;
                _cmapFormat = 4;
                return true;
            }

            LoadFailure = "unicode-cmap-unsupported-or-invalid";
            return false;
        }

        private bool ValidateCmapFormat4(int offset, int length) {
            if (length < 16 || !HasRange(offset, length) ||
                (long)offset + length > (long)_cmapTableEnd()) return false;
            int segCountX2 = U16(offset + 6);
            if (segCountX2 == 0 || (segCountX2 & 1) != 0) return false;
            int segments = segCountX2 / 2;
            if (segments > MaximumCmapSegments || 16L + 8L * segments > length) return false;

            int endBase = offset + 14;
            int startBase = endBase + 2 * segments + 2;
            int deltaBase = startBase + 2 * segments;
            int rangeBase = deltaBase + 2 * segments;
            int previousEnd = -1;
            int previousStart = -1;
            for (int i = 0; i < segments; i++) {
                int end = U16(endBase + i * 2);
                int start = U16(startBase + i * 2);
                int range = U16(rangeBase + i * 2);
                if (start > end || end <= previousEnd || start < previousStart) return false;
                previousEnd = end;
                previousStart = start;

                if (range != 0) {
                    long firstWord = (long)rangeBase + i * 2L + range;
                    long lastWordEnd = firstWord + (long)(end - start) * 2L + 2L;
                    long subtableEnd = (long)offset + length;
                    if (firstWord < offset || lastWordEnd > subtableEnd) return false;
                }
            }
            return U16(endBase + (segments - 1) * 2) == 0xFFFF;
        }

        private int _cmapTableEnd() {
            TableRecord table;
            return FindTable(TagCmap, out table) ? table.Offset + table.Length : 0;
        }

        private bool ValidateCmapFormat12(int offset, int length) {
            if (length < 16 || !HasRange(offset, length) ||
                (long)offset + length > (long)_cmapTableEnd()) return false;
            uint groupCount = U32(offset + 12);
            if (groupCount > MaximumCmapGroups ||
                16L + 12L * groupCount > length) return false;

            uint previousEnd = 0;
            for (int i = 0; i < (int)groupCount; i++) {
                int group = offset + 16 + i * 12;
                uint start = U32(group);
                uint end = U32(group + 4);
                uint firstGlyph = U32(group + 8);
                if (start > end || end > 0x10FFFFu ||
                    (i > 0 && start <= previousEnd)) return false;
                ulong lastGlyph = (ulong)firstGlyph + (ulong)(end - start);
                if (lastGlyph >= (ulong)_numGlyphs) return false;
                previousEnd = end;
            }
            return true;
        }

        private int FindGlyphIndex(int codePoint) {
            if (codePoint < 0 || codePoint > 0x10FFFF) return 0;
            if (_cmapFormat == 12) {
                int groups = (int)U32(_cmapOffset + 12);
                int low = 0;
                int high = groups - 1;
                while (low <= high) {
                    int middle = low + ((high - low) / 2);
                    int group = _cmapOffset + 16 + middle * 12;
                    uint start = U32(group);
                    uint end = U32(group + 4);
                    if ((uint)codePoint < start) high = middle - 1;
                    else if ((uint)codePoint > end) low = middle + 1;
                    else {
                        ulong glyph = (ulong)U32(group + 8) +
                                      (uint)codePoint - start;
                        return glyph < (ulong)_numGlyphs ? (int)glyph : 0;
                    }
                }
                return 0;
            }

            if (_cmapFormat == 4 && codePoint <= 0xFFFF) {
                int segments = U16(_cmapOffset + 6) / 2;
                int endBase = _cmapOffset + 14;
                int startBase = endBase + segments * 2 + 2;
                int deltaBase = startBase + segments * 2;
                int rangeBase = deltaBase + segments * 2;
                int low = 0;
                int high = segments - 1;
                while (low <= high) {
                    int middle = low + ((high - low) / 2);
                    int end = U16(endBase + middle * 2);
                    if (codePoint > end) {
                        low = middle + 1;
                        continue;
                    }
                    int start = U16(startBase + middle * 2);
                    if (codePoint < start) {
                        high = middle - 1;
                        continue;
                    }

                    int delta = I16(deltaBase + middle * 2);
                    int range = U16(rangeBase + middle * 2);
                    int glyph;
                    if (range == 0) {
                        glyph = (codePoint + delta) & 0xFFFF;
                    } else {
                        long glyphAddress = (long)rangeBase + middle * 2L +
                                            range + (codePoint - start) * 2L;
                        if (glyphAddress < _cmapOffset ||
                            glyphAddress + 2 > (long)_cmapOffset + _cmapLength) {
                            return 0;
                        }
                        glyph = U16((int)glyphAddress);
                        if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                    }
                    return glyph > 0 && glyph < _numGlyphs ? glyph : 0;
                }
            }
            return 0;
        }

        public TrueTypeGlyph GetGlyph(char character) {
            return GetGlyph((int)character);
        }

        /// <summary>
        /// Look up a Unicode scalar. This overload also exposes cmap format 12
        /// coverage outside the Basic Multilingual Plane.
        /// </summary>
        public TrueTypeGlyph GetGlyph(int codePoint) {
            if (!IsValid) return EmptyGlyph(0, FontSize / 2);
            int glyphIndex = FindGlyphIndex(codePoint);
            if (glyphIndex == 0 && codePoint != '?') glyphIndex = FindGlyphIndex('?');
            return GetGlyphByIndex(glyphIndex);
        }

        /// <summary>
        /// Retains the former bitmap-returning API for callers that consume a
        /// System.Drawing.Image. New code should use GetGlyph to avoid copying.
        /// </summary>
        public Image RenderChar(char character) {
            TrueTypeGlyph glyph = GetGlyph(character);
            Image image = new Image(glyph.Width, glyph.Height);
            for (int i = 0; i < glyph.Coverage.Length && i < image.RawData.Length; i++) {
                int alpha = glyph.Coverage[i];
                image.RawData[i] = (alpha << 24) | 0x00FFFFFF;
            }
            return image;
        }

        private TrueTypeGlyph GetGlyphByIndex(int glyphIndex) {
            if (glyphIndex < 0 || glyphIndex >= _numGlyphs) glyphIndex = 0;
            for (int i = 0; i < _cache.Length; i++) {
                CacheEntry entry = _cache[i];
                if (entry != null && entry.GlyphIndex == glyphIndex) {
                    CacheHits++;
                    entry.LastUse = ++_cacheClock;
                    return entry.Glyph;
                }
            }

            CacheMisses++;
            Rasterizations++;
            TrueTypeGlyph glyph;
            if (!TryRasterizeGlyph(glyphIndex, out glyph)) {
                RasterizationFailures++;
                if (glyphIndex != 0 && !TryRasterizeGlyph(0, out glyph)) {
                    glyph = EmptyGlyph(0, ScaleRounded(_advanceWidths[glyphIndex]));
                }
                else if (glyphIndex == 0) {
                    glyph = EmptyGlyph(0, ScaleRounded(_advanceWidths[0]));
                }
            }

            CacheGlyph(glyphIndex, glyph);
            return glyph;
        }

        private void CacheGlyph(int glyphIndex, TrueTypeGlyph glyph) {
            int bytes = glyph.Coverage.Length;
            if (bytes > MaximumCachedGlyphBytes) return;

            while (_cacheCount >= MaximumCacheEntries ||
                   _cacheBytes + bytes > MaximumCacheBytes) {
                int victim = -1;
                long oldest = long.MaxValue;
                for (int i = 0; i < _cache.Length; i++) {
                    if (_cache[i] != null && _cache[i].LastUse < oldest) {
                        oldest = _cache[i].LastUse;
                        victim = i;
                    }
                }
                if (victim < 0) return;
                _cacheBytes -= _cache[victim].Glyph.Coverage.Length;
                _cache[victim] = null;
                _cacheCount--;
            }

            for (int i = 0; i < _cache.Length; i++) {
                if (_cache[i] == null) {
                    CacheEntry entry = new CacheEntry();
                    entry.GlyphIndex = glyphIndex;
                    entry.Glyph = glyph;
                    entry.LastUse = ++_cacheClock;
                    _cache[i] = entry;
                    _cacheBytes += bytes;
                    _cacheCount++;
                    return;
                }
            }
        }

        private bool TryRasterizeGlyph(int glyphIndex, out TrueTypeGlyph glyph) {
            glyph = null;
            int[] visiting = new int[MaximumCompositeDepth];
            for (int i = 0; i < visiting.Length; i++) visiting[i] = -1;
            Outline outline;
            if (!TryDecodeGlyph(glyphIndex, 0, visiting, out outline)) return false;
            if (outline.PointCount == 0) {
                int metricsGlyph = outline.MetricsGlyphIndex;
                glyph = new TrueTypeGlyph(glyphIndex, 0, 0,
                    ScaleRounded(_advanceWidths[metricsGlyph]), 0, 0,
                    ScaleRounded(_leftSideBearings[metricsGlyph]), new byte[0]);
                return true;
            }

            double minX = outline.Points[0].X;
            double maxX = minX;
            double minY = outline.Points[0].Y;
            double maxY = minY;
            for (int i = 1; i < outline.PointCount; i++) {
                GlyphPoint point = outline.Points[i];
                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
            }

            int left = (int)Math.Floor(minX * _scale);
            int right = CeilingToInt(maxX * _scale);
            int bottom = (int)Math.Floor(minY * _scale);
            int top = CeilingToInt(maxY * _scale);
            int width = right - left;
            int height = top - bottom;
            if (width <= 0 || height <= 0) {
                int metricGlyph = outline.MetricsGlyphIndex;
                glyph = new TrueTypeGlyph(glyphIndex, 0, 0,
                    ScaleRounded(_advanceWidths[metricGlyph]), left, top,
                    ScaleRounded(_leftSideBearings[metricGlyph]), new byte[0]);
                return true;
            }
            if (width > MaximumGlyphDimension || height > MaximumGlyphDimension ||
                (long)width * height > MaximumGlyphDimension * MaximumGlyphDimension) {
                return false;
            }

            byte[] coverage;
            if (!RasterizeOutline(outline, left, top, width, height, out coverage)) {
                return false;
            }

            int sourceMetrics = outline.MetricsGlyphIndex;
            glyph = new TrueTypeGlyph(glyphIndex, width, height,
                ScaleRounded(_advanceWidths[sourceMetrics]), left, top,
                ScaleRounded(_leftSideBearings[sourceMetrics]), coverage);
            return true;
        }

        private bool TryDecodeGlyph(int glyphIndex, int depth, int[] visiting,
                                    out Outline outline) {
            outline = null;
            if (glyphIndex < 0 || glyphIndex >= _numGlyphs ||
                depth >= MaximumCompositeDepth) return false;

            for (int i = 0; i < depth; i++) {
                if (visiting[i] == glyphIndex) return false;
            }
            visiting[depth] = glyphIndex;
            try {
                int relativeStart = _glyphOffsets[glyphIndex];
                int relativeEnd = _glyphOffsets[glyphIndex + 1];
                if (relativeEnd < relativeStart || relativeEnd > _glyfLength) return false;
                Outline result = new Outline();
                result.MetricsGlyphIndex = glyphIndex;
                if (relativeStart == relativeEnd) {
                    outline = result;
                    return true;
                }

                int start = _glyfOffset + relativeStart;
                int end = _glyfOffset + relativeEnd;
                if (end - start < 10) return false;
                short contourCount = I16(start);
                short xMin = I16(start + 2);
                short yMin = I16(start + 4);
                short xMax = I16(start + 6);
                short yMax = I16(start + 8);
                if (xMin > xMax || yMin > yMax) return false;

                bool decoded = contourCount >= 0
                    ? TryDecodeSimpleGlyph(start, end, contourCount, result)
                    : TryDecodeCompositeGlyph(start, end, depth, visiting, result);
                if (!decoded) return false;
                outline = result;
                return true;
            } finally {
                visiting[depth] = -1;
            }
        }

        private bool TryDecodeSimpleGlyph(int glyphStart, int glyphEnd,
                                          int contourCount, Outline outline) {
            if (contourCount > MaximumContours) return false;
            if (contourCount == 0) return true;
            long endpointsEnd = (long)glyphStart + 10L + contourCount * 2L;
            if (endpointsEnd + 2 > glyphEnd) return false;

            int cursor = glyphStart + 10;
            int previousEnd = -1;
            for (int i = 0; i < contourCount; i++) {
                int endpoint = U16(cursor);
                if (endpoint <= previousEnd || endpoint >= MaximumOutlinePoints) return false;
                outline.EndPoints[i] = endpoint;
                previousEnd = endpoint;
                cursor += 2;
            }
            int pointCount = previousEnd + 1;
            if (pointCount <= 0 || pointCount > MaximumOutlinePoints ||
                !HasInGlyph(cursor, 2, glyphEnd)) return false;

            int instructionLength = U16(cursor);
            cursor += 2;
            if (!HasInGlyph(cursor, instructionLength, glyphEnd)) return false;
            cursor += instructionLength;
            byte[] flags = new byte[pointCount];
            int flagCount = 0;
            while (flagCount < pointCount) {
                if (!HasInGlyph(cursor, 1, glyphEnd)) return false;
                byte flag = _data[cursor++];
                flags[flagCount++] = flag;
                if ((flag & 0x08) != 0) {
                    if (!HasInGlyph(cursor, 1, glyphEnd)) return false;
                    int repeat = _data[cursor++];
                    if (repeat > pointCount - flagCount) return false;
                    for (int j = 0; j < repeat; j++) flags[flagCount++] = flag;
                }
            }

            int[] xs = new int[pointCount];
            int coordinate = 0;
            for (int i = 0; i < pointCount; i++) {
                byte flag = flags[i];
                int delta = 0;
                if ((flag & 0x02) != 0) {
                    if (!HasInGlyph(cursor, 1, glyphEnd)) return false;
                    int magnitude = _data[cursor++];
                    delta = (flag & 0x10) != 0 ? magnitude : -magnitude;
                } else if ((flag & 0x10) == 0) {
                    if (!HasInGlyph(cursor, 2, glyphEnd)) return false;
                    delta = I16(cursor);
                    cursor += 2;
                }
                coordinate += delta;
                if (coordinate < -MaximumCoordinate || coordinate > MaximumCoordinate) return false;
                xs[i] = coordinate;
            }

            int[] ys = new int[pointCount];
            coordinate = 0;
            for (int i = 0; i < pointCount; i++) {
                byte flag = flags[i];
                int delta = 0;
                if ((flag & 0x04) != 0) {
                    if (!HasInGlyph(cursor, 1, glyphEnd)) return false;
                    int magnitude = _data[cursor++];
                    delta = (flag & 0x20) != 0 ? magnitude : -magnitude;
                } else if ((flag & 0x20) == 0) {
                    if (!HasInGlyph(cursor, 2, glyphEnd)) return false;
                    delta = I16(cursor);
                    cursor += 2;
                }
                coordinate += delta;
                if (coordinate < -MaximumCoordinate || coordinate > MaximumCoordinate) return false;
                ys[i] = coordinate;
            }

            outline.PointCount = pointCount;
            outline.ContourCount = contourCount;
            for (int i = 0; i < pointCount; i++) {
                outline.Points[i] = new GlyphPoint(xs[i], ys[i], (flags[i] & 1) != 0);
            }
            return true;
        }

        private bool TryDecodeCompositeGlyph(int glyphStart, int glyphEnd, int depth,
                                             int[] visiting, Outline outline) {
            int cursor = glyphStart + 10;
            int componentCount = 0;
            bool hasInstructions = false;
            ushort flags;
            do {
                if (!HasInGlyph(cursor, 4, glyphEnd) ||
                    componentCount >= MaximumCompositeComponents) return false;
                flags = U16(cursor);
                int componentGlyph = U16(cursor + 2);
                cursor += 4;
                componentCount++;
                if ((flags & 0xE000) != 0 ||
                    ((flags & ScaledComponentOffset) != 0 &&
                     (flags & UnscaledComponentOffset) != 0)) return false;

                int transformKinds = ((flags & HaveScale) != 0 ? 1 : 0) +
                                     ((flags & HaveXYScale) != 0 ? 1 : 0) +
                                     ((flags & Have2x2) != 0 ? 1 : 0);
                if (transformKinds > 1) return false;

                bool words = (flags & ArgWords) != 0;
                bool xyArguments = (flags & ArgsAreXY) != 0;
                int argumentBytes = words ? 4 : 2;
                if (!HasInGlyph(cursor, argumentBytes, glyphEnd)) return false;
                int arg1;
                int arg2;
                if (words) {
                    arg1 = xyArguments ? I16(cursor) : U16(cursor);
                    arg2 = xyArguments ? I16(cursor + 2) : U16(cursor + 2);
                } else {
                    arg1 = xyArguments ? (sbyte)_data[cursor] : _data[cursor];
                    arg2 = xyArguments ? (sbyte)_data[cursor + 1] : _data[cursor + 1];
                }
                cursor += argumentBytes;

                double a = 1.0, b = 0.0, c = 0.0, d = 1.0;
                if ((flags & HaveScale) != 0) {
                    if (!HasInGlyph(cursor, 2, glyphEnd)) return false;
                    a = d = I16(cursor) / 16384.0;
                    cursor += 2;
                } else if ((flags & HaveXYScale) != 0) {
                    if (!HasInGlyph(cursor, 4, glyphEnd)) return false;
                    a = I16(cursor) / 16384.0;
                    d = I16(cursor + 2) / 16384.0;
                    cursor += 4;
                } else if ((flags & Have2x2) != 0) {
                    if (!HasInGlyph(cursor, 8, glyphEnd)) return false;
                    a = I16(cursor) / 16384.0;
                    b = I16(cursor + 2) / 16384.0;
                    c = I16(cursor + 4) / 16384.0;
                    d = I16(cursor + 6) / 16384.0;
                    cursor += 8;
                }

                Outline component;
                if (!TryDecodeGlyph(componentGlyph, depth + 1, visiting,
                                    out component)) return false;

                double tx = 0.0;
                double ty = 0.0;
                if (xyArguments) {
                    tx = arg1;
                    ty = arg2;
                    if ((flags & ScaledComponentOffset) != 0) {
                        double transformedX = tx * a + ty * c;
                        double transformedY = tx * b + ty * d;
                        tx = transformedX;
                        ty = transformedY;
                    }
                    if ((flags & RoundXYToGrid) != 0) {
                        tx = RoundAwayFromZero(tx);
                        ty = RoundAwayFromZero(ty);
                    }
                } else {
                    if (arg1 < 0 || arg1 >= outline.PointCount ||
                        arg2 < 0 || arg2 >= component.PointCount) return false;
                    GlyphPoint parentPoint = outline.Points[arg1];
                    GlyphPoint childPoint = component.Points[arg2];
                    tx = parentPoint.X - (childPoint.X * a + childPoint.Y * c);
                    ty = parentPoint.Y - (childPoint.X * b + childPoint.Y * d);
                }

                if (outline.PointCount + component.PointCount > MaximumOutlinePoints ||
                    outline.ContourCount + component.ContourCount > MaximumContours) return false;
                int pointBase = outline.PointCount;
                for (int i = 0; i < component.PointCount; i++) {
                    GlyphPoint point = component.Points[i];
                    double x = point.X * a + point.Y * c + tx;
                    double y = point.X * b + point.Y * d + ty;
                    if (double.IsNaN(x) || double.IsNaN(y) ||
                        x > MaximumCoordinate || x < -MaximumCoordinate ||
                        y > MaximumCoordinate || y < -MaximumCoordinate ||
                        Math.Abs(x) > MaximumCoordinate || Math.Abs(y) > MaximumCoordinate) {
                        return false;
                    }
                    outline.Points[outline.PointCount++] =
                        new GlyphPoint(x, y, point.OnCurve);
                }
                for (int i = 0; i < component.ContourCount; i++) {
                    int endpoint = pointBase + component.EndPoints[i];
                    if (endpoint < 0 || endpoint >= outline.PointCount) return false;
                    outline.EndPoints[outline.ContourCount++] = endpoint;
                }

                if ((flags & UseMyMetrics) != 0) {
                    outline.MetricsGlyphIndex = component.MetricsGlyphIndex;
                }
                hasInstructions |= (flags & HaveInstructions) != 0;
            } while ((flags & MoreComponents) != 0);

            if (componentCount == 0) return false;
            if (hasInstructions) {
                if (!HasInGlyph(cursor, 2, glyphEnd)) return false;
                int instructionLength = U16(cursor);
                cursor += 2;
                if (!HasInGlyph(cursor, instructionLength, glyphEnd)) return false;
            }
            return true;
        }

        private bool RasterizeOutline(Outline outline, int left, int top,
                                      int width, int height, out byte[] coverage) {
            coverage = null;
            Edge[] edges = new Edge[outline.PointCount * CurveSteps];
            int edgeCount = 0;
            int contourStart = 0;
            for (int contour = 0; contour < outline.ContourCount; contour++) {
                int contourEnd = outline.EndPoints[contour];
                int pointCount = contourEnd - contourStart + 1;
                if (pointCount <= 0 || pointCount > MaximumOutlinePoints) return false;

                GlyphPoint first = outline.Points[contourStart];
                GlyphPoint last = outline.Points[contourEnd];
                double startX;
                double startY;
                int cursor;
                int remaining;
                if (first.OnCurve) {
                    startX = first.X;
                    startY = first.Y;
                    cursor = contourStart + 1;
                    remaining = pointCount - 1;
                } else if (last.OnCurve) {
                    startX = last.X;
                    startY = last.Y;
                    cursor = contourStart;
                    remaining = pointCount - 1;
                } else {
                    startX = (last.X + first.X) * 0.5;
                    startY = (last.Y + first.Y) * 0.5;
                    cursor = contourStart;
                    remaining = pointCount;
                }

                double currentX = startX;
                double currentY = startY;
                while (remaining > 0) {
                    if (cursor > contourEnd) cursor = contourStart;
                    GlyphPoint point = outline.Points[cursor];
                    if (point.OnCurve) {
                        if (!AddLine(edges, ref edgeCount,
                            ToPixelX(currentX, left), ToPixelY(currentY, top),
                            ToPixelX(point.X, left), ToPixelY(point.Y, top))) return false;
                        currentX = point.X;
                        currentY = point.Y;
                        cursor++;
                        remaining--;
                        continue;
                    }

                    int nextIndex = cursor == contourEnd ? contourStart : cursor + 1;
                    GlyphPoint next = outline.Points[nextIndex];
                    if (next.OnCurve && remaining > 1) {
                        if (!AddQuadratic(edges, ref edgeCount,
                            currentX, currentY, point.X, point.Y, next.X, next.Y,
                            left, top)) return false;
                        currentX = next.X;
                        currentY = next.Y;
                        cursor = nextIndex + 1;
                        remaining -= 2;
                    } else if (next.OnCurve) {
                        if (!AddQuadratic(edges, ref edgeCount,
                            currentX, currentY, point.X, point.Y, startX, startY,
                            left, top)) return false;
                        currentX = startX;
                        currentY = startY;
                        remaining--;
                    } else {
                        double midpointX = (point.X + next.X) * 0.5;
                        double midpointY = (point.Y + next.Y) * 0.5;
                        if (!AddQuadratic(edges, ref edgeCount,
                            currentX, currentY, point.X, point.Y,
                            midpointX, midpointY, left, top)) return false;
                        currentX = midpointX;
                        currentY = midpointY;
                        cursor++;
                        remaining--;
                    }
                }

                if (Math.Abs(currentX - startX) > 0.000001 ||
                    Math.Abs(currentY - startY) > 0.000001) {
                    if (!AddLine(edges, ref edgeCount,
                        ToPixelX(currentX, left), ToPixelY(currentY, top),
                        ToPixelX(startX, left), ToPixelY(startY, top))) return false;
                }
                contourStart = contourEnd + 1;
            }

            if (edgeCount == 0) {
                coverage = new byte[width * height];
                return true;
            }

            int highWidth = width * SamplesPerAxis;
            int highHeight = height * SamplesPerAxis;
            byte[] samples = new byte[highWidth * highHeight];
            Intersection[] intersections = new Intersection[edgeCount];
            for (int y = 0; y < highHeight; y++) {
                double scanY = (y + 0.5) / SamplesPerAxis;
                int count = 0;
                for (int i = 0; i < edgeCount; i++) {
                    Edge edge = edges[i];
                    if ((edge.Y0 <= scanY && edge.Y1 > scanY) ||
                        (edge.Y1 <= scanY && edge.Y0 > scanY)) {
                        double x = edge.X0 + (scanY - edge.Y0) *
                                   (edge.X1 - edge.X0) / (edge.Y1 - edge.Y0);
                        intersections[count].X = x;
                        intersections[count].Direction = edge.Y1 > edge.Y0 ? 1 : -1;
                        count++;
                    }
                }
                HeapSort(intersections, count);
                int winding = 0;
                double spanStart = 0.0;
                for (int i = 0; i < count; i++) {
                    double x = intersections[i].X;
                    if (winding != 0 && x > spanStart) {
                        FillSampleSpan(samples, y * highWidth,
                            highWidth, spanStart, x);
                    }
                    winding += intersections[i].Direction;
                    spanStart = x;
                }
                if (winding != 0) return false;
            }

            coverage = new byte[width * height];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    int covered = 0;
                    int row0 = (y * SamplesPerAxis) * highWidth + x * SamplesPerAxis;
                    for (int sy = 0; sy < SamplesPerAxis; sy++) {
                        int row = row0 + sy * highWidth;
                        for (int sx = 0; sx < SamplesPerAxis; sx++) {
                            covered += samples[row + sx];
                        }
                    }
                    coverage[y * width + x] =
                        (byte)((covered * 255 + 8) / 16);
                }
            }
            return true;
        }

        private double ToPixelX(double x, int left) {
            return x * _scale - left;
        }

        private double ToPixelY(double y, int top) {
            return top - y * _scale;
        }

        private bool AddLine(Edge[] edges, ref int count,
                            double x0, double y0, double x1, double y1) {
            if (Math.Abs(x0 - x1) < 0.0000001 &&
                Math.Abs(y0 - y1) < 0.0000001) return true;
            if (count >= edges.Length) return false;
            edges[count].X0 = x0;
            edges[count].Y0 = y0;
            edges[count].X1 = x1;
            edges[count].Y1 = y1;
            count++;
            return true;
        }

        private bool AddQuadratic(Edge[] edges, ref int count,
                                  double x0, double y0, double cx, double cy,
                                  double x1, double y1, int left, int top) {
            double previousX = ToPixelX(x0, left);
            double previousY = ToPixelY(y0, top);
            double controlX = ToPixelX(cx, left);
            double controlY = ToPixelY(cy, top);
            double endX = ToPixelX(x1, left);
            double endY = ToPixelY(y1, top);
            for (int step = 1; step <= CurveSteps; step++) {
                double t = (double)step / CurveSteps;
                double oneMinusT = 1.0 - t;
                double nextX = oneMinusT * oneMinusT * ToPixelX(x0, left) +
                               2.0 * oneMinusT * t * controlX + t * t * endX;
                double nextY = oneMinusT * oneMinusT * ToPixelY(y0, top) +
                               2.0 * oneMinusT * t * controlY + t * t * endY;
                if (!AddLine(edges, ref count, previousX, previousY, nextX, nextY)) {
                    return false;
                }
                previousX = nextX;
                previousY = nextY;
            }
            return true;
        }

        private static void FillSampleSpan(byte[] samples, int rowOffset,
                                           int width, double x0, double x1) {
            int first = CeilingToInt(x0 * SamplesPerAxis - 0.5);
            int after = CeilingToInt(x1 * SamplesPerAxis - 0.5);
            if (first < 0) first = 0;
            if (after > width) after = width;
            for (int x = first; x < after; x++) samples[rowOffset + x] = 1;
        }

        private static void HeapSort(Intersection[] values, int count) {
            for (int start = count / 2 - 1; start >= 0; start--) {
                SiftDown(values, start, count);
            }
            for (int end = count - 1; end > 0; end--) {
                Intersection temporary = values[end];
                values[end] = values[0];
                values[0] = temporary;
                SiftDown(values, 0, end);
            }
        }

        private static void SiftDown(Intersection[] values, int root, int count) {
            while (root * 2 + 1 < count) {
                int child = root * 2 + 1;
                if (child + 1 < count && values[child].X < values[child + 1].X) child++;
                if (values[root].X >= values[child].X) return;
                Intersection temporary = values[root];
                values[root] = values[child];
                values[child] = temporary;
                root = child;
            }
        }

        private int ScaleRounded(int units) {
            return RoundAwayFromZero(units * _scale);
        }

        private int ScaleCeiling(int units) {
            return CeilingToInt(units * _scale);
        }

        private int ScaleFloor(int units) {
            return FloorToInt(units * _scale);
        }

        private static int RoundAwayFromZero(double value) {
            return (int)(value >= 0.0 ? value + 0.5 : value - 0.5);
        }

        private static int CeilingToInt(double value) {
            int truncated = (int)value;
            return value > truncated ? truncated + 1 : truncated;
        }

        private static int FloorToInt(double value) {
            int truncated = (int)value;
            return value < truncated ? truncated - 1 : truncated;
        }

        private static bool IsHighSurrogate(char value) {
            return value >= '\uD800' && value <= '\uDBFF';
        }

        private static bool IsLowSurrogate(char value) {
            return value >= '\uDC00' && value <= '\uDFFF';
        }

        private static int ToUtf32(char high, char low) {
            return 0x10000 + ((high - 0xD800) << 10) + (low - 0xDC00);
        }

        public int GetAdvanceWidth(char character) {
            return GetGlyph(character).Advance;
        }

        public int GetAdvanceWidth(int codePoint) {
            return GetGlyph(codePoint).Advance;
        }

        public int MeasureString(string text) {
            if (!IsValid || text == null) return 0;
            return MeasureString(text, 0, text.Length);
        }

        /// <summary>
        /// Measure a text range without allocating a substring. The returned
        /// width is the maximum line advance in the range.
        /// </summary>
        public int MeasureString(string text, int start, int length) {
            if (!IsValid || text == null || start < 0 || length < 0 ||
                start > text.Length - length) return 0;
            long line = 0;
            long maximum = 0;
            int end = start + length;
            for (int i = start; i < end; i++) {
                char character = text[i];
                if (character == '\r' || character == '\n') {
                    if (line > maximum) maximum = line;
                    line = 0;
                    if (character == '\r' && i + 1 < end && text[i + 1] == '\n') i++;
                    continue;
                }
                int codePoint = character;
                if (IsHighSurrogate(character) && i + 1 < end &&
                    IsLowSurrogate(text[i + 1])) {
                    codePoint = ToUtf32(character, text[i + 1]);
                    i++;
                }
                line += GetGlyph(codePoint).Advance;
                if (line > int.MaxValue) return int.MaxValue;
            }
            if (line > maximum) maximum = line;
            return maximum > int.MaxValue ? int.MaxValue : (int)maximum;
        }

        /// <summary>
        /// Draw text at the top of the first line. Grayscale coverage multiplies
        /// the foreground alpha; Graphics performs the existing desktop blend
        /// and clipping. Coordinates are widened before addition to avoid wrap.
        /// </summary>
        public void DrawString(Graphics graphics, int x, int y, string text,
                               uint color = 0xFFFFFFFFu) {
            DrawStringMeasured(graphics, x, y, text, color);
        }

        /// <summary>
        /// Draw text and return the maximum line advance. DrawString retains the
        /// original void-returning API for existing callers.
        /// </summary>
        public int DrawStringMeasured(Graphics graphics, int x, int y, string text,
                                      uint color = 0xFFFFFFFFu) {
            if (graphics == null) return 0;
            return DrawStringCore(graphics, x, y, text, color, 0, 0,
                                  graphics.Width, graphics.Height);
        }

        /// <summary>
        /// Draw text at the top of the first line, clipped to a half-open
        /// caller-owned rectangle. Measurement continues to use the same
        /// advances as DrawStringMeasured.
        /// </summary>
        public int DrawStringClipped(Graphics graphics, int x, int y,
                                     string text, uint color,
                                     int clipX, int clipY,
                                     int clipWidth, int clipHeight) {
            if (clipWidth < 0 || clipHeight < 0) return 0;
            return DrawStringCore(graphics, x, y, text, color, clipX, clipY,
                (long)clipX + clipWidth, (long)clipY + clipHeight);
        }

        private int DrawStringCore(Graphics graphics, int x, int y, string text,
                                   uint color, long clipLeft, long clipTop,
                                   long clipRight, long clipBottom) {
            if (!IsValid || graphics == null || text == null ||
                graphics.Width <= 0 || graphics.Height <= 0) return 0;

            long penX = x;
            long lineTop = y;
            long maximumLineWidth = 0;
            int foregroundAlpha = (int)((color >> 24) & 0xFF);
            uint rgb = color & 0x00FFFFFFu;
            for (int i = 0; i < text.Length; i++) {
                char character = text[i];
                if (character == '\r' || character == '\n') {
                    long lineWidth = penX - x;
                    if (lineWidth > maximumLineWidth) maximumLineWidth = lineWidth;
                    penX = x;
                    lineTop += LineHeight;
                    if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    continue;
                }

                int codePoint = character;
                if (IsHighSurrogate(character) && i + 1 < text.Length &&
                    IsLowSurrogate(text[i + 1])) {
                    codePoint = ToUtf32(character, text[i + 1]);
                    i++;
                }
                TrueTypeGlyph glyph = GetGlyph(codePoint);
                long baseline = lineTop + Ascent;
                long drawX = penX + glyph.BearingX;
                long drawY = baseline - glyph.BearingY;
                byte[] pixels = glyph.Coverage;
                for (int gy = 0; gy < glyph.Height; gy++) {
                    long destinationY = drawY + gy;
                    if (destinationY < 0 || destinationY >= graphics.Height ||
                        destinationY < clipTop || destinationY >= clipBottom) continue;
                    for (int gx = 0; gx < glyph.Width; gx++) {
                        byte coverage = pixels[gy * glyph.Width + gx];
                        if (coverage == 0 || foregroundAlpha == 0) continue;
                        long destinationX = drawX + gx;
                        if (destinationX < 0 || destinationX >= graphics.Width ||
                            destinationX < clipLeft || destinationX >= clipRight) continue;
                        uint alpha = (uint)(foregroundAlpha * coverage / 255);
                        if (alpha == 0) continue;
                        graphics.DrawPoint((int)destinationX, (int)destinationY,
                            (alpha << 24) | rgb, true);
                    }
                }

                penX += glyph.Advance;
                if (penX > int.MaxValue) penX = int.MaxValue;
                if (penX < int.MinValue) penX = int.MinValue;
            }
            long finalLineWidth = penX - x;
            if (finalLineWidth > maximumLineWidth) maximumLineWidth = finalLineWidth;
            return maximumLineWidth > int.MaxValue ? int.MaxValue :
                   maximumLineWidth < 0 ? 0 : (int)maximumLineWidth;
        }

        private bool FindTable(uint tag, out TableRecord result) {
            for (int i = 0; i < _tableCount; i++) {
                if (_tables[i].Tag == tag) {
                    result = _tables[i];
                    return true;
                }
            }
            result = new TableRecord();
            return false;
        }

        private static bool RangesOverlap(int aOffset, int aLength,
                                          int bOffset, int bLength) {
            long aEnd = (long)aOffset + aLength;
            long bEnd = (long)bOffset + bLength;
            return aOffset < bEnd && bOffset < aEnd;
        }

        private bool HasRange(int offset, int length) {
            return offset >= 0 && length >= 0 && (long)offset + length <= _data.Length;
        }

        private bool HasInGlyph(int offset, int length, int glyphEnd) {
            return offset >= 0 && length >= 0 &&
                   (long)offset + length <= glyphEnd &&
                   (long)offset + length <= _data.Length;
        }

        private ushort U16(int offset) {
            return (ushort)((_data[offset] << 8) | _data[offset + 1]);
        }

        private short I16(int offset) {
            return unchecked((short)U16(offset));
        }

        private uint U32(int offset) {
            return ((uint)U16(offset) << 16) | U16(offset + 2);
        }

        private static TrueTypeGlyph EmptyGlyph(int glyphIndex, int advance) {
            return new TrueTypeGlyph(glyphIndex, 0, 0, advance, 0, 0, 0, new byte[0]);
        }
    }
}
