using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using guideXOS.Graph;
using guideXOS.Misc;

namespace guideXOS.FontTests {
    internal static class Program {
        private static int _checks;
        private static int _failures;

        private static int Main() {
            string fixture = Path.Combine(AppContext.BaseDirectory, "Roboto-Regular.ttf");
            byte[] fontBytes = File.ReadAllBytes(fixture);

            CheckValidSizeMatrix(fontBytes);
            CheckFormat4Cmap(fontBytes);
            CheckMalformedInputs(fontBytes);
            CheckCompositeCycle(fontBytes);
            CheckBoundsAndCache(fontBytes);
            CheckHostedRendering(fontBytes);
            int fonts2Failures = SharedTextFontChecks.Run(fontBytes);

            Console.WriteLine("FONTS1 hosted checks: " + _checks +
                              ", failures: " + _failures);
            return _failures == 0 && fonts2Failures == 0 ? 0 : 1;
        }

        private static void CheckValidSizeMatrix(byte[] fontBytes) {
            int[] sizes = new int[] { 12, 14, 16, 18, 20, 24, 32 };
            ulong[] hashes = new ulong[sizes.Length];
            for (int i = 0; i < sizes.Length; i++) {
                TrueTypeFont font;
                bool loaded = TrueTypeFont.TryCreate(fontBytes, sizes[i], out font);
                Check(loaded && font != null, "Roboto loads at " + sizes[i] + " px");
                if (!loaded) continue;

                Check(font.GlyphCount > 1000 && font.GlyphCount <= TrueTypeFont.MaximumGlyphs,
                      "bounded glyph count at " + sizes[i] + " px");
                Check(font.CmapFormat == 12 || font.CmapFormat == 4,
                      "Unicode cmap format " + font.CmapFormat + " selected");
                Check(font.Ascent > 0 && font.Descent < 0 && font.LineHeight >= sizes[i],
                      "vertical metrics at " + sizes[i] + " px");

                char[] sample = new char[] {
                    'A', 'a', 'g', '0', '9', '!', '?', ',', '.', '(', ')', 'é',
                    'Ω', '£'
                };
                for (int glyph = 0; glyph < sample.Length; glyph++) {
                    TrueTypeGlyph raster = font.GetGlyph(sample[glyph]);
                    Check(raster.Advance > 0,
                          "advance for U+" + ((int)sample[glyph]).ToString("X4") +
                          " at " + sizes[i] + " px");
                    if (sample[glyph] != ' ') {
                        Check(raster.Width > 0 && raster.Height > 0,
                              "outline bounds for U+" + ((int)sample[glyph]).ToString("X4") +
                              " at " + sizes[i] + " px");
                    }
                }

                TrueTypeGlyph space = font.GetGlyph(' ');
                Check(space.Width == 0 && space.Height == 0 && space.Advance > 0,
                      "empty space glyph keeps its advance at " + sizes[i] + " px");
                Check(font.GetGlyph(0x1F600).Advance > 0,
                      "supplementary Unicode scalar lookup falls back safely");

                TrueTypeGlyph curved = font.GetGlyph('S');
                bool hasInk = false;
                bool hasAntialias = false;
                for (int y = 0; y < curved.Height; y++) {
                    for (int x = 0; x < curved.Width; x++) {
                        byte coverage = curved.GetCoverage(x, y);
                        if (coverage != 0) hasInk = true;
                        if (coverage > 0 && coverage < 255) hasAntialias = true;
                    }
                }
                Check(hasInk, "S outline has filled coverage at " + sizes[i] + " px");
                Check(hasAntialias, "S outline has antialias coverage at " + sizes[i] + " px");
                hashes[i] = GlyphHash(curved);
            }

            for (int i = 1; i < hashes.Length; i++) {
                Check(hashes[i] != hashes[0],
                      "requested pixel size changes the rerasterized S bitmap: " + sizes[i]);
            }

            TrueTypeFont invalidSize;
            Check(!TrueTypeFont.TryCreate(fontBytes, 0, out invalidSize),
                  "zero pixel size rejected");
            Check(!TrueTypeFont.TryCreate(fontBytes, TrueTypeFont.MaximumPixelSize + 1,
                                          out invalidSize),
                  "over-limit pixel size rejected");
        }

        private static void CheckMalformedInputs(byte[] fontBytes) {
            for (int length = 0; length < 12; length++) {
                byte[] truncated = new byte[length];
                TrueTypeFont rejected;
                Check(!TrueTypeFont.TryCreate(truncated, 16, out rejected),
                      "truncated sfnt header rejected at length " + length);
            }

            byte[] invalidSignature = (byte[])fontBytes.Clone();
            invalidSignature[0] = 0x4F;
            invalidSignature[1] = 0x54;
            invalidSignature[2] = 0x54;
            invalidSignature[3] = 0x4F;
            CheckRejected(invalidSignature, "CFF/OTTO signature rejected");

            byte[] invalidTableCount = (byte[])fontBytes.Clone();
            WriteU16(invalidTableCount, 4, (ushort)(TrueTypeFont.MaximumTables + 1));
            CheckRejected(invalidTableCount, "table-count resource limit rejected");

            byte[] invalidTableOffset = (byte[])fontBytes.Clone();
            WriteU32(invalidTableOffset, 20, 0xFFFFFFFCu);
            CheckRejected(invalidTableOffset, "out-of-file table offset rejected");

            byte[] overlappingTables = (byte[])fontBytes.Clone();
            WriteU32(overlappingTables, 36, ReadU32(overlappingTables, 20));
            CheckRejected(overlappingTables, "overlapping table ranges rejected");

            byte[] duplicateTable = (byte[])fontBytes.Clone();
            WriteU32(duplicateTable, 28, ReadU32(duplicateTable, 12));
            CheckRejected(duplicateTable, "duplicate table tags rejected");

            byte[] badLoca = (byte[])fontBytes.Clone();
            int loca = FindTable(badLoca, "loca");
            int glyf = FindTable(badLoca, "glyf");
            int head = FindTable(badLoca, "head");
            int maxp = FindTable(badLoca, "maxp");
            int glyphCount = ReadU16(badLoca, maxp + 4);
            int locaFormat = ReadI16(badLoca, head + 50);
            int lastLoca = loca + (glyphCount * (locaFormat == 0 ? 2 : 4));
            if (locaFormat == 0) {
                WriteU16(badLoca, lastLoca,
                    (ushort)Math.Min(65535, (ReadU16(badLoca, lastLoca) + glyf + 1) / 2));
            } else {
                WriteU32(badLoca, lastLoca, (uint)(ReadU32(badLoca, lastLoca) + 1));
            }
            CheckRejected(badLoca, "loca value beyond glyf table rejected");

            byte[] badCmap = (byte[])fontBytes.Clone();
            int cmap = FindTable(badCmap, "cmap");
            WriteU16(badCmap, cmap + 2, (ushort)(TrueTypeFont.MaximumGlyphs + 1));
            CheckRejected(badCmap, "excessive cmap record count rejected");

            byte[] oversized = new byte[TrueTypeFont.MaximumFontBytes + 1];
            CheckRejected(oversized, "source byte limit rejected");
        }

        private static void CheckFormat4Cmap(byte[] fontBytes) {
            TrueTypeFont font;
            Check(TrueTypeFont.TryCreate(fontBytes, 16, out font),
                  "Roboto format 4 cmap loads");
            if (font == null) return;
            Check(font.CmapFormat == 4, "format 4 cmap is selected as fallback");
            Check(font.GetGlyph('A').Width > 0 && font.GetGlyph('\u00E9').Width > 0,
                  "format 4 cmap resolves ASCII and Latin extended glyphs");

            // The packaged Roboto build carries cmap 4 only. Reuse a valid
            // format-4 subtable's storage to form a bounded one-group format-12
            // fixture mapping U+1F600 to Roboto's real A outline.
            byte[] format12Fixture = (byte[])fontBytes.Clone();
            int cmap = FindTable(format12Fixture, "cmap");
            int record = cmap + 4;
            int subtable = cmap + (int)ReadU32(format12Fixture, record + 4);
            int glyphA = MapFormat4(format12Fixture, subtable, 'A');
            Check(ReadU16(format12Fixture, subtable) == 4 && glyphA > 0,
                  "format 12 fixture starts from Roboto's format 4 mapping");
            if (glyphA <= 0) return;
            WriteU16(format12Fixture, record, 3);
            WriteU16(format12Fixture, record + 2, 10);
            WriteU16(format12Fixture, subtable, 12);
            WriteU16(format12Fixture, subtable + 2, 0);
            WriteU32(format12Fixture, subtable + 4, 28);
            WriteU32(format12Fixture, subtable + 8, 0);
            WriteU32(format12Fixture, subtable + 12, 1);
            WriteU32(format12Fixture, subtable + 16, 0x1F600);
            WriteU32(format12Fixture, subtable + 20, 0x1F600);
            WriteU32(format12Fixture, subtable + 24, (uint)glyphA);

            TrueTypeFont format12;
            Check(TrueTypeFont.TryCreate(format12Fixture, 16, out format12),
                  "synthetic cmap format 12 is structurally accepted");
            if (format12 == null) return;
            Check(format12.CmapFormat == 12, "format 12 is preferred when present");
            Check(format12.GetGlyph(0x1F600).GlyphIndex == glyphA &&
                  format12.GetGlyph(0x1F600).Width > 0,
                  "format 12 maps a supplementary scalar to an actual outline");
        }

        private static void CheckCompositeCycle(byte[] fontBytes) {
            byte[] cyclic = (byte[])fontBytes.Clone();
            int maxp = FindTable(cyclic, "maxp");
            int head = FindTable(cyclic, "head");
            int loca = FindTable(cyclic, "loca");
            int glyf = FindTable(cyclic, "glyf");
            int glyphCount = ReadU16(cyclic, maxp + 4);
            int locaFormat = ReadI16(cyclic, head + 50);
            int compositeGlyph = -1;
            int compositeStart = -1;
            for (int glyph = 0; glyph < glyphCount; glyph++) {
                int start = ReadLoca(cyclic, loca, locaFormat, glyph);
                int end = ReadLoca(cyclic, loca, locaFormat, glyph + 1);
                if (end - start >= 14 && ReadI16(cyclic, glyf + start) < 0) {
                    compositeGlyph = glyph;
                    compositeStart = glyf + start;
                    break;
                }
            }
            Check(compositeGlyph >= 0, "Roboto includes a composite glyph fixture");
            if (compositeGlyph < 0) return;

            // The first component glyph ID follows the composite flags and is
            // always a 16-bit field, independent of the argument width.
            WriteU16(cyclic, compositeStart + 12, (ushort)compositeGlyph);
            TrueTypeFont font;
            Check(TrueTypeFont.TryCreate(cyclic, 16, out font),
                  "structurally valid font with lazy composite cycle loads");
            if (font == null) return;

            int codePoint = FindCodePointForGlyph(cyclic, compositeGlyph);
            Check(codePoint >= 0 && codePoint <= 0xFFFF,
                  "cyclic composite has a BMP cmap entry");
            if (codePoint >= 0 && codePoint <= 0xFFFF) {
                TrueTypeGlyph replacement = font.GetGlyph((char)codePoint);
                Check(font.RasterizationFailures == 1,
                      "composite cycle stops at bounded recursion/cycle check");
                Check(replacement.Width > 0 && replacement.Height > 0,
                      "cyclic glyph falls back to a visible .notdef outline");
            }
        }

        private static void CheckBoundsAndCache(byte[] fontBytes) {
            TrueTypeFont font;
            Check(TrueTypeFont.TryCreate(fontBytes, 24, out font),
                  "Roboto loads for cache-limit checks");
            if (font == null) return;

            for (int value = 32; value < 127; value++) font.GetGlyph((char)value);
            Check(font.CacheEntryCount <= TrueTypeFont.MaximumCacheEntries,
                  "cache entry count stays bounded under ASCII pressure");
            Check(font.CacheBytes <= TrueTypeFont.MaximumCacheBytes,
                  "cache byte budget stays bounded under ASCII pressure");

            TrueTypeFont cacheProbe;
            Check(TrueTypeFont.TryCreate(fontBytes, 16, out cacheProbe),
                  "Roboto loads for repeated-glyph cache probe");
            if (cacheProbe == null) return;
            TrueTypeGlyph first = cacheProbe.GetGlyph('R');
            int rasterizations = cacheProbe.Rasterizations;
            int hits = cacheProbe.CacheHits;
            TrueTypeGlyph repeated = cacheProbe.GetGlyph('R');
            Check(Object.ReferenceEquals(first, repeated),
                  "repeated glyph returns the cached coverage object");
            Check(cacheProbe.Rasterizations == rasterizations &&
                  cacheProbe.CacheHits == hits + 1,
                  "warm lookup does not rerasterize");
        }

        private static void CheckHostedRendering(byte[] fontBytes) {
            TrueTypeFont font12 = new TrueTypeFont(fontBytes, 12);
            TrueTypeFont font20 = new TrueTypeFont(fontBytes, 20);
            TrueTypeFont font32 = new TrueTypeFont(fontBytes, 32);
            Check(font12.IsValid && font20.IsValid && font32.IsValid,
                  "one packaged TTF creates 12/20/32 px renderers");
            if (!font12.IsValid || !font20.IsValid || !font32.IsValid) return;

            TrueTypeGlyph compatibilityGlyph = font12.GetGlyph('R');
            Image compatibilityImage = font12.RenderChar('R');
            Check(compatibilityImage.Width == compatibilityGlyph.Width &&
                  compatibilityImage.Height == compatibilityGlyph.Height &&
                  compatibilityImage.RawData.Length ==
                      compatibilityGlyph.Width * compatibilityGlyph.Height,
                  "legacy RenderChar still returns a bounded bitmap image");
            bool compatibilityInk = false;
            for (int i = 0; i < compatibilityImage.RawData.Length; i++) {
                if (((uint)compatibilityImage.RawData[i] >> 24) != 0) {
                    compatibilityInk = true;
                    break;
                }
            }
            Check(compatibilityInk, "legacy bitmap adapter keeps antialiased glyph ink");

            string text = "AaZz 0123456789 !@#$%&*()?,. café Ω £ \U0001F600";
            Graphics first = new Graphics(320, 96, 0xFF102030u);
            Graphics second = new Graphics(320, 96, 0xFF102030u);
            int measured = font12.MeasureString(text);
            int drawn = font12.DrawStringMeasured(first, 4, 4, text, 0xFFF5F5F5u);
            Check(drawn == measured && measured > 0,
                  "measurement and drawing share the same glyph advances");
            font20.DrawString(first, 4, 24, "Roboto: The quick brown fox", 0xFFFFFFFFu);
            font32.DrawString(first, 4, 52, "TTF", 0xFFFFFFFFu);
            font12.DrawString(second, -4, 4, "A clipped at left", 0xFFFFFFFFu);
            font20.DrawString(second, 284, 24, "right edge clipped safely", 0xFFFFFFFFu);

            Check(first.InvalidDrawAttempts == 0 && second.InvalidDrawAttempts == 0,
                  "left and right viewport clipping never emit out-of-range pixels");
            Check(first.ChangedPixelCount > 250,
                  "multi-size hosted fixture changes a deterministic pixel region");
            Check(first.IntermediateBlendPixelCount > 0,
                  "coverage alpha reaches the existing Graphics alpha blend path");

            Graphics repeat = new Graphics(320, 96, 0xFF102030u);
            font12.DrawString(repeat, 4, 4, text, 0xFFF5F5F5u);
            font20.DrawString(repeat, 4, 24, "Roboto: The quick brown fox", 0xFFFFFFFFu);
            font32.DrawString(repeat, 4, 52, "TTF", 0xFFFFFFFFu);
            Check(first.PixelHash() == repeat.PixelHash(),
                  "multi-size hosted rendering has a repeatable pixel hash");

            ulong fixtureHash = first.PixelHash();
            Check(fixtureHash == 0xEC1A69C86C9C5CC6UL,
                  "hosted multi-size pixel fixture matches its golden hash");
            int warmRasterizations = font12.Rasterizations;
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < 300; i++) {
                font12.DrawString(first, 4, 4, text, 0xFFF5F5F5u);
            }
            long elapsedTicks = Stopwatch.GetTimestamp() - start;
            double nsPerDraw = elapsedTicks * (1000000000.0 / Stopwatch.Frequency) / 300.0;
            Check(font12.Rasterizations == warmRasterizations,
                  "300 repeated draws are served from the glyph cache");
            Console.WriteLine("Hosted Roboto warm draw: 300 iterations, " +
                nsPerDraw.ToString("F0") + " ns/draw; cache hits=" +
                font12.CacheHits + ", misses=" + font12.CacheMisses +
                ", rasterizations=" + font12.Rasterizations +
                ", entries=" + font12.CacheEntryCount +
                ", bytes=" + font12.CacheBytes +
                ", pixel-hash=0x" + fixtureHash.ToString("X16"));
        }

        private static void CheckRejected(byte[] bytes, string description) {
            TrueTypeFont rejected;
            Check(!TrueTypeFont.TryCreate(bytes, 16, out rejected), description);
        }

        private static int FindCodePointForGlyph(byte[] data, int wantedGlyph) {
            int cmap = FindTable(data, "cmap");
            int records = ReadU16(data, cmap + 2);
            for (int i = 0; i < records; i++) {
                int record = cmap + 4 + i * 8;
                uint relative = ReadU32(data, record + 4);
                int subtable = cmap + (int)relative;
                int format = ReadU16(data, subtable);
                if (format == 4) {
                    for (int cp = 0; cp < 0xFFFF; cp++) {
                        if (MapFormat4(data, subtable, cp) == wantedGlyph) return cp;
                    }
                } else if (format == 12) {
                    int groupCount = (int)ReadU32(data, subtable + 12);
                    for (int groupIndex = 0; groupIndex < groupCount; groupIndex++) {
                        int group = subtable + 16 + groupIndex * 12;
                        uint start = ReadU32(data, group);
                        uint end = ReadU32(data, group + 4);
                        uint firstGlyph = ReadU32(data, group + 8);
                        if ((uint)wantedGlyph >= firstGlyph &&
                            (uint)wantedGlyph <= firstGlyph + end - start) {
                            uint cp = start + (uint)wantedGlyph - firstGlyph;
                            if (cp <= 0xFFFF) return (int)cp;
                        }
                    }
                }
            }
            return -1;
        }

        private static int MapFormat4(byte[] data, int offset, int cp) {
            int segments = ReadU16(data, offset + 6) / 2;
            int endBase = offset + 14;
            int startBase = endBase + segments * 2 + 2;
            int deltaBase = startBase + segments * 2;
            int rangeBase = deltaBase + segments * 2;
            for (int i = 0; i < segments; i++) {
                int end = ReadU16(data, endBase + i * 2);
                int start = ReadU16(data, startBase + i * 2);
                if (cp < start || cp > end) continue;
                int delta = ReadI16(data, deltaBase + i * 2);
                int range = ReadU16(data, rangeBase + i * 2);
                if (range == 0) return (cp + delta) & 0xFFFF;
                int address = rangeBase + i * 2 + range + (cp - start) * 2;
                int glyph = ReadU16(data, address);
                return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
            }
            return 0;
        }

        private static int FindTable(byte[] data, string name) {
            uint tag = ((uint)name[0] << 24) | ((uint)name[1] << 16) |
                       ((uint)name[2] << 8) | name[3];
            int count = ReadU16(data, 4);
            for (int i = 0; i < count; i++) {
                int record = 12 + i * 16;
                if (ReadU32(data, record) == tag) return (int)ReadU32(data, record + 8);
            }
            throw new InvalidDataException("missing table " + name);
        }

        private static int ReadLoca(byte[] data, int loca, int format, int glyph) {
            return format == 0
                ? ReadU16(data, loca + glyph * 2) * 2
                : (int)ReadU32(data, loca + glyph * 4);
        }

        private static ulong GlyphHash(TrueTypeGlyph glyph) {
            ulong hash = 14695981039346656037UL;
            hash = (hash ^ (uint)glyph.Width) * 1099511628211UL;
            hash = (hash ^ (uint)glyph.Height) * 1099511628211UL;
            for (int y = 0; y < glyph.Height; y++) {
                for (int x = 0; x < glyph.Width; x++) {
                    hash = (hash ^ glyph.GetCoverage(x, y)) * 1099511628211UL;
                }
            }
            return hash;
        }

        private static ushort ReadU16(byte[] bytes, int offset) {
            return (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
        }

        private static short ReadI16(byte[] bytes, int offset) {
            return unchecked((short)ReadU16(bytes, offset));
        }

        private static uint ReadU32(byte[] bytes, int offset) {
            return ((uint)ReadU16(bytes, offset) << 16) | ReadU16(bytes, offset + 2);
        }

        private static void WriteU16(byte[] bytes, int offset, ushort value) {
            bytes[offset] = (byte)(value >> 8);
            bytes[offset + 1] = (byte)value;
        }

        private static void WriteU32(byte[] bytes, int offset, uint value) {
            WriteU16(bytes, offset, (ushort)(value >> 16));
            WriteU16(bytes, offset + 2, (ushort)value);
        }

        private static void Check(bool condition, string description) {
            _checks++;
            if (!condition) {
                _failures++;
                Console.Error.WriteLine("FAIL: " + description);
            }
        }
    }
}

namespace System.Drawing {
    /// <summary>Hosted stand-in for the kernel's bounded ARGB Image surface.</summary>
    public sealed class Image {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int[] RawData { get; private set; }

        public Image(int width, int height) {
            Width = width;
            Height = height;
            RawData = new int[width * height];
        }
    }
}

namespace guideXOS.Kernel.Drivers {
    public static class Framebuffer {
        public static guideXOS.Graph.Graphics Graphics;
    }
}

public static class BootConsole {
    public static void WriteLine(string value) { }
}

namespace guideXOS.Graph {
    /// <summary>
    /// Hosted stand-in for guideXOS.Graph.Graphics. It enforces bounds and uses
    /// the same 0xFF channel interpolation as the production DrawPoint path.
    /// </summary>
    public sealed class Graphics {
        private readonly uint[] _pixels;
        private int _changedPixels;
        private int _intermediateBlendPixels;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int InvalidDrawAttempts { get; private set; }
        public int ChangedPixelCount { get { return _changedPixels; } }
        public int IntermediateBlendPixelCount { get { return _intermediateBlendPixels; } }

        public Graphics(int width, int height, uint background) {
            Width = width;
            Height = height;
            _pixels = new uint[width * height];
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = background;
        }

        public void DrawPoint(int x, int y, uint color, bool alphaBlending = false) {
            if (x < 0 || y < 0 || x >= Width || y >= Height) {
                InvalidDrawAttempts++;
                return;
            }
            int index = y * Width + x;
            uint result = color;
            if (alphaBlending) {
                int alpha = (int)((color >> 24) & 0xFF);
                int inverse = 255 - alpha;
                uint background = _pixels[index];
                int red = ((int)((color >> 16) & 0xFF) * alpha +
                           (int)((background >> 16) & 0xFF) * inverse) >> 8;
                int green = ((int)((color >> 8) & 0xFF) * alpha +
                             (int)((background >> 8) & 0xFF) * inverse) >> 8;
                int blue = ((int)(color & 0xFF) * alpha +
                            (int)(background & 0xFF) * inverse) >> 8;
                result = 0xFF000000u | ((uint)red << 16) |
                         ((uint)green << 8) | (uint)blue;
                if (alpha > 0 && alpha < 255) _intermediateBlendPixels++;
            }
            if (result != _pixels[index]) _changedPixels++;
            _pixels[index] = result;
        }

        public ulong PixelHash() {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < _pixels.Length; i++) {
                uint pixel = _pixels[i];
                hash = (hash ^ (byte)(pixel >> 24)) * 1099511628211UL;
                hash = (hash ^ (byte)(pixel >> 16)) * 1099511628211UL;
                hash = (hash ^ (byte)(pixel >> 8)) * 1099511628211UL;
                hash = (hash ^ (byte)pixel) * 1099511628211UL;
            }
            return hash;
        }

        public uint GetPixel(int x, int y) {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
            return _pixels[y * Width + x];
        }
    }
}
