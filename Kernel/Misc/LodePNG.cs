using guideXOS;
using System;
using System.Drawing;

namespace guideXOS.Misc {
    // ============================================================================
    // LEGACY MANAGED PNG LOADER - SUPERSEDED BY PngLoader
    // ============================================================================
    // WARNING: This class is a full-featured but complex PNG decoder that was
    // ported from the LodePNG C library. While it is pure managed code and does
    // not require native P/Invoke calls, it has several limitations:
    //
    // 1. COMPLEXITY: The decoder is large and may have edge cases that cause
    //    hangs or incorrect output in the constrained kernel environment.
    //
    // 2. MEMORY USAGE: Uses many temporary allocations during decoding which
    //    can cause fragmentation in the kernel's memory allocator.
    //
    // 3. NO PRELOAD SUPPORT: Cannot be used for boot-time asset preloading
    //    as it doesn't integrate with PngAssetPreloader.
    //
    // SAFE ALTERNATIVE: Use PngLoader (Kernel\Misc\PngLoader.cs) which is
    // specifically designed for the UEFI kernel environment with:
    //   - Minimal memory allocations
    //   - No recursion
    //   - No exceptions
    //   - Integration with PngAssetPreloader for boot-time loading
    //
    // This class is retained for compatibility but should not be used for
    // new code. It may be removed in a future version.
    // ============================================================================
    /// <summary>
    /// Pure managed C# port of LodePNG - PNG decoder
    /// Ported from: https://github.com/lvandeve/lodepng
    /// Supports: All standard PNG color types (grayscale, RGB, palette, with/without alpha)
    /// Supports: 1, 2, 4, 8, 16 bit depths
    /// Supports: Adam7 interlacing
    /// 
    /// DEPRECATED: Use PngLoader instead for UEFI-safe PNG decoding.
    /// </summary>
    [System.Obsolete("Use PngLoader for UEFI-safe PNG decoding. LodePNG is retained for legacy compatibility only.")]
    public unsafe class LodePNG : Image {
        
        #region Constants
        
        // Color types
        private const int LCT_GREY = 0;
        private const int LCT_RGB = 2;
        private const int LCT_PALETTE = 3;
        private const int LCT_GREY_ALPHA = 4;
        private const int LCT_RGBA = 6;
        
        // Filter types
        private const byte FILTER_NONE = 0;
        private const byte FILTER_SUB = 1;
        private const byte FILTER_UP = 2;
        private const byte FILTER_AVERAGE = 3;
        private const byte FILTER_PAETH = 4;
        
        // Huffman constants
        private const int FIRSTBITS = 9;
        private const int INVALIDSYMBOL = 65535;
        
        // DEFLATE constants
        private const int NUM_CODE_LENGTH_CODES = 19;
        private const int NUM_DEFLATE_CODE_SYMBOLS = 288;
        private const int NUM_DISTANCE_SYMBOLS = 32;
        private const int FIRST_LENGTH_CODE_INDEX = 257;
        private const int LAST_LENGTH_CODE_INDEX = 285;
        
        // Helper methods to get table values (avoid static readonly arrays)
        private static int GetLengthBase(int idx) {
            switch (idx) {
                case 0: return 3; case 1: return 4; case 2: return 5; case 3: return 6;
                case 4: return 7; case 5: return 8; case 6: return 9; case 7: return 10;
                case 8: return 11; case 9: return 13; case 10: return 15; case 11: return 17;
                case 12: return 19; case 13: return 23; case 14: return 27; case 15: return 31;
                case 16: return 35; case 17: return 43; case 18: return 51; case 19: return 59;
                case 20: return 67; case 21: return 83; case 22: return 99; case 23: return 115;
                case 24: return 131; case 25: return 163; case 26: return 195; case 27: return 227;
                case 28: return 258;
                default: return 3;
            }
        }
        
        private static int GetLengthExtra(int idx) {
            switch (idx) {
                case 0: case 1: case 2: case 3: case 4: case 5: case 6: case 7: return 0;
                case 8: case 9: case 10: case 11: return 1;
                case 12: case 13: case 14: case 15: return 2;
                case 16: case 17: case 18: case 19: return 3;
                case 20: case 21: case 22: case 23: return 4;
                case 24: case 25: case 26: case 27: return 5;
                case 28: return 0; // Code 285 (length 258) has 0 extra bits
                default: return 0;
            }
        }
        
        private static int GetDistanceBase(int idx) {
            switch (idx) {
                case 0: return 1; case 1: return 2; case 2: return 3; case 3: return 4;
                case 4: return 5; case 5: return 7; case 6: return 9; case 7: return 13;
                case 8: return 17; case 9: return 25; case 10: return 33; case 11: return 49;
                case 12: return 65; case 13: return 97; case 14: return 129; case 15: return 193;
                case 16: return 257; case 17: return 385; case 18: return 513; case 19: return 769;
                case 20: return 1025; case 21: return 1537; case 22: return 2049; case 23: return 3073;
                case 24: return 4097; case 25: return 6145; case 26: return 8193; case 27: return 12289;
                case 28: return 16385; case 29: return 24577;
                default: return 1;
            }
        }
        
        private static int GetDistanceExtra(int idx) {
            switch (idx) {
                case 0: case 1: case 2: case 3: return 0;
                case 4: case 5: return 1;
                case 6: case 7: return 2;
                case 8: case 9: return 3;
                case 10: case 11: return 4;
                case 12: case 13: return 5;
                case 14: case 15: return 6;
                case 16: case 17: return 7;
                case 18: case 19: return 8;
                case 20: case 21: return 9;
                case 22: case 23: return 10;
                case 24: case 25: return 11;
                case 26: case 27: return 12;
                case 28: case 29: return 13;
                default: return 0;
            }
        }
        
        private static int GetCLCLOrder(int idx) {
            switch (idx) {
                case 0: return 16; case 1: return 17; case 2: return 18; case 3: return 0;
                case 4: return 8; case 5: return 7; case 6: return 9; case 7: return 6;
                case 8: return 10; case 9: return 5; case 10: return 11; case 11: return 4;
                case 12: return 12; case 13: return 3; case 14: return 13; case 15: return 2;
                case 16: return 14; case 17: return 1; case 18: return 15;
                default: return 0;
            }
        }
        
        // Adam7 interlace pattern - use methods instead of arrays
        private static int GetAdam7IX(int pass) {
            switch (pass) {
                case 0: return 0; case 1: return 4; case 2: return 0; case 3: return 2;
                case 4: return 0; case 5: return 1; case 6: return 0;
                default: return 0;
            }
        }
        
        private static int GetAdam7IY(int pass) {
            switch (pass) {
                case 0: return 0; case 1: return 0; case 2: return 4; case 3: return 0;
                case 4: return 2; case 5: return 0; case 6: return 1;
                default: return 0;
            }
        }
        
        private static int GetAdam7DX(int pass) {
            switch (pass) {
                case 0: return 8; case 1: return 8; case 2: return 4; case 3: return 4;
                case 4: return 2; case 5: return 2; case 6: return 1;
                default: return 1;
            }
        }
        
        private static int GetAdam7DY(int pass) {
            switch (pass) {
                case 0: return 8; case 1: return 8; case 2: return 8; case 3: return 4;
                case 4: return 4; case 5: return 2; case 6: return 2;
                default: return 1;
            }
        }
        
        #endregion
        
        #region Huffman Tree
        
        private class HuffmanTree {
            public int[] Codes;
            public int[] Lengths;
            public int MaxBitLen;
            public int NumCodes;
            public byte[] TableLen;
            public ushort[] TableValue;
            
            public void Cleanup() {
                if (Codes != null) Codes.Dispose();
                if (Lengths != null) Lengths.Dispose();
                if (TableLen != null) TableLen.Dispose();
                if (TableValue != null) TableValue.Dispose();
            }
        }
        
        private static int ReverseBits(int bits, int numBits) {
            int result = 0;
            for (int i = 0; i < numBits; i++) {
                result = (result << 1) | ((bits >> i) & 1);
            }
            return result;
        }
        
        private static bool HuffmanTree_MakeTable(HuffmanTree tree) {
            int headsize = 1 << FIRSTBITS;
            int mask = headsize - 1;
            
            int[] maxlens = new int[headsize];
            
            // Compute maxlens: max total bit length of symbols sharing prefix in first table
            for (int i = 0; i < tree.NumCodes; i++) {
                int l = tree.Lengths[i];
                if (l <= FIRSTBITS) continue;
                int symbol = tree.Codes[i];
                int index = ReverseBits(symbol >> (l - FIRSTBITS), FIRSTBITS);
                if (index < headsize && l > maxlens[index]) {
                    maxlens[index] = l;
                }
            }
            
            // Compute total table size
            int size = headsize;
            for (int i = 0; i < headsize; i++) {
                int l = maxlens[i];
                if (l > FIRSTBITS) {
                    size += 1 << (l - FIRSTBITS);
                }
            }
            
            tree.TableLen = new byte[size];
            tree.TableValue = new ushort[size];
            
            // Initialize with invalid length
            for (int i = 0; i < size; i++) {
                tree.TableLen[i] = 16;
            }
            
            // Fill first table for long symbols
            int pointer = headsize;
            for (int i = 0; i < headsize; i++) {
                int l = maxlens[i];
                if (l <= FIRSTBITS) continue;
                tree.TableLen[i] = (byte)l;
                tree.TableValue[i] = (ushort)pointer;
                pointer += 1 << (l - FIRSTBITS);
            }
            
            maxlens.Dispose();
            
            // Fill tables for all symbols
            for (int i = 0; i < tree.NumCodes; i++) {
                int l = tree.Lengths[i];
                if (l == 0) continue;
                
                int symbol = tree.Codes[i];
                int reverse = ReverseBits(symbol, l);
                
                if (l <= FIRSTBITS) {
                    // Short symbol: fill all entries
                    int numEntries = 1 << (FIRSTBITS - l);
                    for (int j = 0; j < numEntries; j++) {
                        int index = reverse | (j << l);
                        if (index < size) {
                            tree.TableLen[index] = (byte)l;
                            tree.TableValue[index] = (ushort)i;
                        }
                    }
                } else {
                    // Long symbol: use secondary table
                    int index = reverse & mask;
                    int maxLen = tree.TableLen[index];
                    int tablePtrIndex = tree.TableValue[index];
                    reverse >>= FIRSTBITS;
                    int numEntries = 1 << (maxLen - l);
                    for (int j = 0; j < numEntries; j++) {
                        int idx = tablePtrIndex + (reverse | (j << (l - FIRSTBITS)));
                        if (idx < size) {
                            tree.TableLen[idx] = (byte)(l - FIRSTBITS);
                            tree.TableValue[idx] = (ushort)i;
                        }
                    }
                }
            }
            
            return true;
        }
        
        private static bool HuffmanTree_MakeFromLengths(HuffmanTree tree, int[] bitlen, int numCodes, int maxBitLen) {
            tree.Lengths = new int[numCodes];
            for (int i = 0; i < numCodes; i++) {
                tree.Lengths[i] = bitlen[i];
            }
            tree.NumCodes = numCodes;
            tree.MaxBitLen = maxBitLen;
            
            // Generate codes from lengths (RFC 1951)
            int[] blcount = new int[maxBitLen + 1];
            int[] nextcode = new int[maxBitLen + 1];
            
            for (int i = 0; i < numCodes; i++) {
                if (bitlen[i] <= maxBitLen) {
                    blcount[bitlen[i]]++;
                }
            }
            
            int code = 0;
            blcount[0] = 0;
            for (int bits = 1; bits <= maxBitLen; bits++) {
                code = (code + blcount[bits - 1]) << 1;
                nextcode[bits] = code;
            }
            
            tree.Codes = new int[numCodes];
            for (int n = 0; n < numCodes; n++) {
                int len = bitlen[n];
                if (len != 0) {
                    tree.Codes[n] = nextcode[len];
                    nextcode[len]++;
                    tree.Codes[n] &= (1 << len) - 1;
                }
            }
            
            blcount.Dispose();
            nextcode.Dispose();
            
            return HuffmanTree_MakeTable(tree);
        }
        
        #endregion
        
        #region Bit Reader
        
        private class BitReader {
            public byte[] Data;
            public int Size;
            public int Pos;     // Byte position
            public int BitPos;  // Bit position within current operation
            public int Buffer;
            public int BitsInBuffer;
            
            public BitReader(byte[] data, int offset, int size) {
                Data = data;
                Size = size;
                Pos = offset;
                BitPos = 0;
                Buffer = 0;
                BitsInBuffer = 0;
            }
            
            public void EnsureBits(int numBits) {
                while (BitsInBuffer < numBits && Pos < Data.Length) {
                    Buffer |= Data[Pos++] << BitsInBuffer;
                    BitsInBuffer += 8;
                }
            }
            
            public int PeekBits(int numBits) {
                return Buffer & ((1 << numBits) - 1);
            }
            
            public int ReadBits(int numBits) {
                EnsureBits(numBits);
                int result = Buffer & ((1 << numBits) - 1);
                Buffer >>= numBits;
                BitsInBuffer -= numBits;
                return result;
            }
            
            public void AdvanceBits(int numBits) {
                Buffer >>= numBits;
                BitsInBuffer -= numBits;
            }
            
            public void AlignToByte() {
                Buffer = 0;
                BitsInBuffer = 0;
            }
        }
        
        #endregion
        
        #region DEFLATE Decompression
        
        private static int HuffmanDecodeSymbol(BitReader reader, HuffmanTree tree) {
            reader.EnsureBits(FIRSTBITS);
            int index = reader.PeekBits(FIRSTBITS);
            int l = tree.TableLen[index];
            int value = tree.TableValue[index];
            
            if (l <= FIRSTBITS) {
                reader.AdvanceBits(l);
                return value;
            } else {
                // Secondary table lookup
                reader.AdvanceBits(FIRSTBITS);
                int extraBits = l - FIRSTBITS;
                reader.EnsureBits(extraBits);
                index = value + reader.PeekBits(extraBits);
                reader.AdvanceBits(tree.TableLen[index]);
                return tree.TableValue[index];
            }
        }
        
        private static bool GetFixedInflateTrees(out HuffmanTree treeLit, out HuffmanTree treeDist) {
            treeLit = new HuffmanTree();
            treeDist = new HuffmanTree();
            
            // Fixed literal/length tree
            int[] bitlenLit = new int[NUM_DEFLATE_CODE_SYMBOLS];
            for (int i = 0; i <= 143; i++) bitlenLit[i] = 8;
            for (int i = 144; i <= 255; i++) bitlenLit[i] = 9;
            for (int i = 256; i <= 279; i++) bitlenLit[i] = 7;
            for (int i = 280; i <= 287; i++) bitlenLit[i] = 8;
            
            if (!HuffmanTree_MakeFromLengths(treeLit, bitlenLit, NUM_DEFLATE_CODE_SYMBOLS, 15)) {
                bitlenLit.Dispose();
                return false;
            }
            bitlenLit.Dispose();
            
            // Fixed distance tree
            int[] bitlenDist = new int[NUM_DISTANCE_SYMBOLS];
            for (int i = 0; i < NUM_DISTANCE_SYMBOLS; i++) bitlenDist[i] = 5;
            
            if (!HuffmanTree_MakeFromLengths(treeDist, bitlenDist, NUM_DISTANCE_SYMBOLS, 15)) {
                bitlenDist.Dispose();
                treeLit.Cleanup();
                return false;
            }
            bitlenDist.Dispose();
            
            return true;
        }
        
        private static bool GetDynamicInflateTrees(BitReader reader, out HuffmanTree treeLit, out HuffmanTree treeDist) {
            treeLit = new HuffmanTree();
            treeDist = new HuffmanTree();
            
            int hlit = reader.ReadBits(5) + 257;
            int hdist = reader.ReadBits(5) + 1;
            int hclen = reader.ReadBits(4) + 4;
            
            // Read code length code lengths
            int[] bitlenCL = new int[NUM_CODE_LENGTH_CODES];
            for (int i = 0; i < hclen; i++) {
                bitlenCL[GetCLCLOrder(i)] = reader.ReadBits(3);
            }
            
            HuffmanTree treeCL = new HuffmanTree();
            if (!HuffmanTree_MakeFromLengths(treeCL, bitlenCL, NUM_CODE_LENGTH_CODES, 7)) {
                bitlenCL.Dispose();
                return false;
            }
            bitlenCL.Dispose();
            
            // Read literal/length and distance code lengths
            int[] bitlen = new int[hlit + hdist];
            int i2 = 0;
            
            while (i2 < hlit + hdist) {
                int code = HuffmanDecodeSymbol(reader, treeCL);
                
                if (code <= 15) {
                    bitlen[i2++] = code;
                } else if (code == 16) {
                    int repeat = reader.ReadBits(2) + 3;
                    int value = i2 > 0 ? bitlen[i2 - 1] : 0;
                    for (int j = 0; j < repeat && i2 < bitlen.Length; j++) {
                        bitlen[i2++] = value;
                    }
                } else if (code == 17) {
                    int repeat = reader.ReadBits(3) + 3;
                    for (int j = 0; j < repeat && i2 < bitlen.Length; j++) {
                        bitlen[i2++] = 0;
                    }
                } else if (code == 18) {
                    int repeat = reader.ReadBits(7) + 11;
                    for (int j = 0; j < repeat && i2 < bitlen.Length; j++) {
                        bitlen[i2++] = 0;
                    }
                } else {
                    treeCL.Cleanup();
                    bitlen.Dispose();
                    return false;
                }
            }
            
            treeCL.Cleanup();
            
            // Split into literal/length and distance
            int[] bitlenLit = new int[hlit];
            int[] bitlenDist = new int[hdist];
            
            for (int j = 0; j < hlit; j++) bitlenLit[j] = bitlen[j];
            for (int j = 0; j < hdist; j++) bitlenDist[j] = bitlen[hlit + j];
            
            bitlen.Dispose();
            
            if (!HuffmanTree_MakeFromLengths(treeLit, bitlenLit, hlit, 15)) {
                bitlenLit.Dispose();
                bitlenDist.Dispose();
                return false;
            }
            bitlenLit.Dispose();
            
            if (!HuffmanTree_MakeFromLengths(treeDist, bitlenDist, hdist, 15)) {
                bitlenDist.Dispose();
                treeLit.Cleanup();
                return false;
            }
            bitlenDist.Dispose();
            
            return true;
        }
        
        private static bool InflateHuffmanBlock(BitReader reader, byte[] output, ref int outPos, int maxOut, int btype) {
            BootConsole.WriteLine("[LodePNG] InflateHuffmanBlock btype=" + btype.ToString());
            HuffmanTree treeLit, treeDist;
            
            if (btype == 1) {
                BootConsole.WriteLine("[LodePNG] Building fixed trees");
                if (!GetFixedInflateTrees(out treeLit, out treeDist)) {
                    BootConsole.WriteLine("[LodePNG] Fixed tree build failed");
                    return false;
                }
            } else {
                BootConsole.WriteLine("[LodePNG] Building dynamic trees");
                if (!GetDynamicInflateTrees(reader, out treeLit, out treeDist)) {
                    BootConsole.WriteLine("[LodePNG] Dynamic tree build failed");
                    return false;
                }
            }
            
            BootConsole.WriteLine("[LodePNG] Trees built, decoding symbols");
            int symbolCount = 0;
            
            while (outPos < maxOut) {
                symbolCount++;
                // Limit iterations to prevent infinite loop
                if (symbolCount > 10000000) {
                    BootConsole.WriteLine("[LodePNG] Too many symbols, aborting");
                    treeLit.Cleanup();
                    treeDist.Cleanup();
                    return false;
                }
                
                int code = HuffmanDecodeSymbol(reader, treeLit);
                
                if (code < 256) {
                    // Literal
                    output[outPos++] = (byte)code;
                } else if (code == 256) {
                    // End of block
                    BootConsole.WriteLine("[LodePNG] End of block, symbols=" + symbolCount.ToString());
                    break;
                } else if (code >= FIRST_LENGTH_CODE_INDEX && code <= LAST_LENGTH_CODE_INDEX) {
                    // Length code
                    int lengthIdx = code - FIRST_LENGTH_CODE_INDEX;
                    int length = GetLengthBase(lengthIdx);
                    int extraBits = GetLengthExtra(lengthIdx);
                    if (extraBits > 0) {
                        length += reader.ReadBits(extraBits);
                    }
                    
                    // Distance code
                    int distCode = HuffmanDecodeSymbol(reader, treeDist);
                    if (distCode >= NUM_DISTANCE_SYMBOLS) {
                        treeLit.Cleanup();
                        treeDist.Cleanup();
                        return false;
                    }
                    
                    int distance = GetDistanceBase(distCode);
                    extraBits = GetDistanceExtra(distCode);
                    if (extraBits > 0) {
                        distance += reader.ReadBits(extraBits);
                    }
                    
                    // Copy bytes
                    if (distance > outPos) {
                        treeLit.Cleanup();
                        treeDist.Cleanup();
                        return false;
                    }
                    
                    int start = outPos;
                    int back = outPos - distance;
                    for (int j = 0; j < length && outPos < maxOut; j++) {
                        output[outPos++] = output[back++];
                    }
                } else {
                    treeLit.Cleanup();
                    treeDist.Cleanup();
                    return false;
                }
            }
            
            treeLit.Cleanup();
            treeDist.Cleanup();
            return true;
        }
        
        private static bool InflateNoCompression(BitReader reader, byte[] output, ref int outPos, int maxOut) {
            reader.AlignToByte();
            
            if (reader.Pos + 4 > reader.Data.Length) return false;
            
            int len = reader.Data[reader.Pos] | (reader.Data[reader.Pos + 1] << 8);
            int nlen = reader.Data[reader.Pos + 2] | (reader.Data[reader.Pos + 3] << 8);
            reader.Pos += 4;
            
            if ((len ^ nlen) != 0xFFFF) return false;
            
            for (int i = 0; i < len && outPos < maxOut && reader.Pos < reader.Data.Length; i++) {
                output[outPos++] = reader.Data[reader.Pos++];
            }
            
            return true;
        }
        
        private static byte[] Inflate(byte[] compressed, int offset, int size, int expectedSize) {
            BootConsole.WriteLine("[LodePNG] Inflate: allocating output");
            byte[] output = new byte[expectedSize];
            int outPos = 0;
            
            BootConsole.WriteLine("[LodePNG] Inflate: creating BitReader");
            BitReader reader = new BitReader(compressed, offset, size);
            
            bool lastBlock = false;
            int blockNum = 0;
            while (!lastBlock && outPos < expectedSize) {
                blockNum++;
                lastBlock = reader.ReadBits(1) == 1;
                int btype = reader.ReadBits(2);
                BootConsole.WriteLine("[LodePNG] Block " + blockNum.ToString() + " btype=" + btype.ToString() + " final=" + (lastBlock ? "1" : "0"));
                
                if (btype == 0) {
                    if (!InflateNoCompression(reader, output, ref outPos, expectedSize)) {
                        BootConsole.WriteLine("[LodePNG] InflateNoCompression failed");
                        output.Dispose();
                        return null;
                    }
                } else if (btype == 1 || btype == 2) {
                    if (!InflateHuffmanBlock(reader, output, ref outPos, expectedSize, btype)) {
                        BootConsole.WriteLine("[LodePNG] InflateHuffmanBlock failed");
                        output.Dispose();
                        return null;
                    }
                } else {
                    BootConsole.WriteLine("[LodePNG] Invalid block type");
                    output.Dispose();
                    return null;
                }
            }
            
            BootConsole.WriteLine("[LodePNG] Inflate complete, outPos=" + outPos.ToString());
            return output;
        }
        
        private static byte[] ZlibDecompress(byte[] data, int offset, int size, int expectedSize) {
            BootConsole.WriteLine("[LodePNG] ZlibDecompress: size=" + size.ToString() + " expected=" + expectedSize.ToString());
            if (size < 6) return null; // Need at least header + checksum
            
            // Check zlib header
            int cmf = data[offset];
            int flg = data[offset + 1];
            
            // Check compression method (must be 8 = deflate)
            if ((cmf & 0x0F) != 8) {
                BootConsole.WriteLine("[LodePNG] Bad CMF");
                return null;
            }
            
            // Check header checksum
            if ((cmf * 256 + flg) % 31 != 0) {
                BootConsole.WriteLine("[LodePNG] Bad header checksum");
                return null;
            }
            
            // Check for preset dictionary (not supported)
            if ((flg & 0x20) != 0) {
                BootConsole.WriteLine("[LodePNG] Preset dictionary not supported");
                return null;
            }
            
            BootConsole.WriteLine("[LodePNG] Calling Inflate");
            // Skip zlib header and decompress
            return Inflate(data, offset + 2, size - 6, expectedSize);
        }

        #endregion
        
        #region PNG Decoding
        
        private static uint Read32BE(byte[] data, int pos) {
            return ((uint)data[pos] << 24) | ((uint)data[pos + 1] << 16) | 
                   ((uint)data[pos + 2] << 8) | data[pos + 3];
        }
        
        private static bool ChunkTypeEquals(byte[] data, int pos, string type) {
            return data[pos] == (byte)type[0] && data[pos + 1] == (byte)type[1] &&
                   data[pos + 2] == (byte)type[2] && data[pos + 3] == (byte)type[3];
        }
        
        private static int GetBpp(int colorType, int bitDepth) {
            int numChannels = 1;
            switch (colorType) {
                case LCT_GREY: numChannels = 1; break;
                case LCT_RGB: numChannels = 3; break;
                case LCT_PALETTE: numChannels = 1; break;
                case LCT_GREY_ALPHA: numChannels = 2; break;
                case LCT_RGBA: numChannels = 4; break;
            }
            return (numChannels * bitDepth + 7) / 8;
        }
        
        private static int GetRawSizeIdat(int w, int h, int bpp) {
            // Size includes filter byte per row
            int lineBits = w * bpp * 8 + 8; // +8 for filter byte
            return ((lineBits + 7) / 8) * h;
        }
        
        private static int PaethPredictor(int a, int b, int c) {
            int p = a + b - c;
            int pa = p > a ? p - a : a - p;
            int pb = p > b ? p - b : b - p;
            int pc = p > c ? p - c : c - p;
            if (pa <= pb && pa <= pc) return a;
            if (pb <= pc) return b;
            return c;
        }
        
        private static void UnfilterScanline(byte[] recon, byte[] scanline, byte[] precon, int byteWidth, int filterType, int length) {
            switch (filterType) {
                case FILTER_NONE:
                    for (int i = 0; i < length; i++) recon[i] = scanline[i];
                    break;
                    
                case FILTER_SUB:
                    for (int i = 0; i < byteWidth && i < length; i++) recon[i] = scanline[i];
                    for (int i = byteWidth; i < length; i++) {
                        recon[i] = (byte)(scanline[i] + recon[i - byteWidth]);
                    }
                    break;
                    
                case FILTER_UP:
                    if (precon != null) {
                        for (int i = 0; i < length; i++) recon[i] = (byte)(scanline[i] + precon[i]);
                    } else {
                        for (int i = 0; i < length; i++) recon[i] = scanline[i];
                    }
                    break;
                    
                case FILTER_AVERAGE:
                    if (precon != null) {
                        for (int i = 0; i < byteWidth && i < length; i++) {
                            recon[i] = (byte)(scanline[i] + (precon[i] >> 1));
                        }
                        for (int i = byteWidth; i < length; i++) {
                            recon[i] = (byte)(scanline[i] + ((recon[i - byteWidth] + precon[i]) >> 1));
                        }
                    } else {
                        for (int i = 0; i < byteWidth && i < length; i++) recon[i] = scanline[i];
                        for (int i = byteWidth; i < length; i++) {
                            recon[i] = (byte)(scanline[i] + (recon[i - byteWidth] >> 1));
                        }
                    }
                    break;
                    
                case FILTER_PAETH:
                    if (precon != null) {
                        for (int i = 0; i < byteWidth && i < length; i++) {
                            recon[i] = (byte)(scanline[i] + precon[i]);
                        }
                        for (int i = byteWidth; i < length; i++) {
                            recon[i] = (byte)(scanline[i] + PaethPredictor(recon[i - byteWidth], precon[i], precon[i - byteWidth]));
                        }
                    } else {
                        for (int i = 0; i < byteWidth && i < length; i++) recon[i] = scanline[i];
                        for (int i = byteWidth; i < length; i++) {
                            recon[i] = (byte)(scanline[i] + recon[i - byteWidth]);
                        }
                    }
                    break;
            }
        }
        
        private static bool UnfilterScanlines(byte[] output, byte[] input, int w, int h, int bpp) {
            int byteWidth = (bpp + 7) / 8;
            int lineBytes = (w * bpp + 7) / 8;
            
            byte[] prevLine = null;
            byte[] currLine = new byte[lineBytes];
            int inPos = 0;
            int outPos = 0;
            
            for (int y = 0; y < h; y++) {
                if (inPos >= input.Length) {
                    currLine.Dispose();
                    return false;
                }
                
                byte filterType = input[inPos++];
                
                // Copy scanline data
                int toCopy = lineBytes;
                if (inPos + toCopy > input.Length) {
                    toCopy = input.Length - inPos;
                }
                
                byte[] scanline = new byte[lineBytes];
                for (int i = 0; i < toCopy; i++) {
                    scanline[i] = input[inPos + i];
                }
                inPos += lineBytes;
                
                // Unfilter
                UnfilterScanline(currLine, scanline, prevLine, byteWidth, filterType, lineBytes);
                scanline.Dispose();
                
                // Copy to output
                for (int i = 0; i < lineBytes && outPos < output.Length; i++) {
                    output[outPos++] = currLine[i];
                }
                
                // Swap lines
                if (prevLine != null) prevLine.Dispose();
                prevLine = currLine;
                currLine = new byte[lineBytes];
            }
            
            if (prevLine != null) prevLine.Dispose();
            currLine.Dispose();
            
            return true;
        }
        
        // Get Adam7 pass dimensions
        private static void Adam7GetPassValues(int w, int h, int pass, out int passW, out int passH) {
            passW = (w + GetAdam7DX(pass) - GetAdam7IX(pass) - 1) / GetAdam7DX(pass);
            passH = (h + GetAdam7DY(pass) - GetAdam7IY(pass) - 1) / GetAdam7DY(pass);
            if (passW < 0) passW = 0;
            if (passH < 0) passH = 0;
        }
        
        private static bool ProcessInterlaced(byte[] output, byte[] input, int w, int h, int bpp) {
            int byteWidth = (bpp + 7) / 8;
            int inPos = 0;
            
            byte[] temp = new byte[w * h * byteWidth];
            
            for (int pass = 0; pass < 7; pass++) {
                int passW, passH;
                Adam7GetPassValues(w, h, pass, out passW, out passH);
                
                if (passW == 0 || passH == 0) continue;
                
                int passLineBytes = (passW * bpp + 7) / 8;
                int passSize = passH * (passLineBytes + 1);
                
                byte[] passInput = new byte[passSize];
                for (int i = 0; i < passSize && inPos + i < input.Length; i++) {
                    passInput[i] = input[inPos + i];
                }
                inPos += passSize;
                
                byte[] passOutput = new byte[passW * passH * byteWidth];
                if (!UnfilterScanlines(passOutput, passInput, passW, passH, bpp)) {
                    passInput.Dispose();
                    passOutput.Dispose();
                    temp.Dispose();
                    return false;
                }
                passInput.Dispose();
                
                // Copy pass data to final image
                int pixelBytes = byteWidth;
                for (int y = 0; y < passH; y++) {
                    for (int x = 0; x < passW; x++) {
                        int outX = GetAdam7IX(pass) + x * GetAdam7DX(pass);
                        int outY = GetAdam7IY(pass) + y * GetAdam7DY(pass);
                        int srcIdx = (y * passW + x) * pixelBytes;
                        int dstIdx = (outY * w + outX) * pixelBytes;
                        
                        for (int b = 0; b < pixelBytes; b++) {
                            if (srcIdx + b < passOutput.Length && dstIdx + b < temp.Length) {
                                temp[dstIdx + b] = passOutput[srcIdx + b];
                            }
                        }
                    }
                }
                
                passOutput.Dispose();
            }
            
            // Copy to output
            for (int i = 0; i < temp.Length && i < output.Length; i++) {
                output[i] = temp[i];
            }
            temp.Dispose();
            
            return true;
        }
        
        private static int GetPixelValue(byte[] data, int bitDepth, int pixelIndex) {
            if (bitDepth == 8) {
                return data[pixelIndex];
            } else if (bitDepth == 16) {
                return (data[pixelIndex * 2] << 8) | data[pixelIndex * 2 + 1];
            } else if (bitDepth < 8) {
                int byteIndex = (pixelIndex * bitDepth) / 8;
                int bitIndex = (pixelIndex * bitDepth) % 8;
                int mask = (1 << bitDepth) - 1;
                return (data[byteIndex] >> (8 - bitDepth - bitIndex)) & mask;
            }
            return 0;
        }
        
        private void ConvertToARGB(byte[] rawData, int w, int h, int colorType, int bitDepth, 
                                   byte[] palette, byte[] transparency, int transLen) {
            Width = w;
            Height = h;
            Bpp = 4;
            RawData = new int[w * h];
            
            int bpp = GetBpp(colorType, bitDepth);
            int pixelBytes = (colorType == LCT_RGBA) ? 4 : 
                            (colorType == LCT_RGB) ? 3 :
                            (colorType == LCT_GREY_ALPHA) ? 2 : 1;
            
            int scale8 = (bitDepth == 16) ? 1 : (bitDepth < 8) ? (255 / ((1 << bitDepth) - 1)) : 1;
            
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    int idx = y * w + x;
                    int pixelOffset = idx * bpp;
                    uint r = 0, g = 0, b = 0, a = 255;
                    
                    switch (colorType) {
                        case LCT_GREY: {
                            int gray;
                            if (bitDepth == 16) {
                                gray = rawData[pixelOffset];
                            } else if (bitDepth == 8) {
                                gray = rawData[pixelOffset];
                            } else {
                                // Sub-byte depth
                                int pixelsPerByte = 8 / bitDepth;
                                int byteIdx = (y * ((w * bitDepth + 7) / 8)) + (x / pixelsPerByte);
                                int bitIdx = (x % pixelsPerByte) * bitDepth;
                                int mask = (1 << bitDepth) - 1;
                                gray = (rawData[byteIdx] >> (8 - bitDepth - bitIdx)) & mask;
                                gray = gray * 255 / ((1 << bitDepth) - 1);
                            }
                            r = g = b = (uint)gray;
                            
                            // Check for transparent color
                            if (transparency != null && transLen >= 2) {
                                int transGray = (transparency[0] << 8) | transparency[1];
                                if (bitDepth == 16 && gray == (transGray >> 8)) a = 0;
                                else if (bitDepth <= 8 && gray == transGray) a = 0;
                            }
                            break;
                        }
                        
                        case LCT_RGB: {
                            if (bitDepth == 16) {
                                r = rawData[pixelOffset];
                                g = rawData[pixelOffset + 2];
                                b = rawData[pixelOffset + 4];
                            } else {
                                r = rawData[pixelOffset];
                                g = rawData[pixelOffset + 1];
                                b = rawData[pixelOffset + 2];
                            }
                            
                            // Check for transparent color
                            if (transparency != null && transLen >= 6) {
                                int transR = (transparency[0] << 8) | transparency[1];
                                int transG = (transparency[2] << 8) | transparency[3];
                                int transB = (transparency[4] << 8) | transparency[5];
                                if (bitDepth == 16) {
                                    if ((int)r == (transR >> 8) && (int)g == (transG >> 8) && (int)b == (transB >> 8)) a = 0;
                                } else {
                                    if ((int)r == transR && (int)g == transG && (int)b == transB) a = 0;
                                }
                            }
                            break;
                        }
                        
                        case LCT_PALETTE: {
                            int palIdx;
                            if (bitDepth == 8) {
                                palIdx = rawData[pixelOffset];
                            } else {
                                int pixelsPerByte = 8 / bitDepth;
                                int byteIdx = (y * ((w * bitDepth + 7) / 8)) + (x / pixelsPerByte);
                                int bitIdx = (x % pixelsPerByte) * bitDepth;
                                int mask = (1 << bitDepth) - 1;
                                palIdx = (rawData[byteIdx] >> (8 - bitDepth - bitIdx)) & mask;
                            }
                            
                            if (palette != null && palIdx * 3 + 2 < palette.Length) {
                                r = palette[palIdx * 3];
                                g = palette[palIdx * 3 + 1];
                                b = palette[palIdx * 3 + 2];
                            }
                            
                            if (transparency != null && palIdx < transLen) {
                                a = transparency[palIdx];
                            }
                            break;
                        }
                        
                        case LCT_GREY_ALPHA: {
                            if (bitDepth == 16) {
                                r = g = b = rawData[pixelOffset];
                                a = rawData[pixelOffset + 2];
                            } else {
                                r = g = b = rawData[pixelOffset];
                                a = rawData[pixelOffset + 1];
                            }
                            break;
                        }
                        
                        case LCT_RGBA: {
                            if (bitDepth == 16) {
                                r = rawData[pixelOffset];
                                g = rawData[pixelOffset + 2];
                                b = rawData[pixelOffset + 4];
                                a = rawData[pixelOffset + 6];
                            } else {
                                r = rawData[pixelOffset];
                                g = rawData[pixelOffset + 1];
                                b = rawData[pixelOffset + 2];
                                a = rawData[pixelOffset + 3];
                            }
                            break;
                        }
                    }
                    
                    RawData[idx] = unchecked((int)((a << 24) | (r << 16) | (g << 8) | b));
                }
            }
        }
        
        #endregion
        
        #region Public API
        
        private void BuildFallback() {
            Width = 8;
            Height = 8;
            Bpp = 4;
            RawData = new int[64];
            for (int y = 0; y < 8; y++) {
                for (int x = 0; x < 8; x++) {
                    bool c = ((x ^ y) & 1) == 0;
                    RawData[y * 8 + x] = c ? unchecked((int)0xFFFF00FF) : unchecked((int)0xFF000000);
                }
            }
        }
        
        public LodePNG(byte[] data) {
            BootConsole.WriteLine("[LodePNG] Constructor");
            if (data == null || data.Length < 33) {
                BootConsole.WriteLine("[LodePNG] Data null or too small");
                BuildFallback();
                return;
            }
            
            try {
                BootConsole.WriteLine("[LodePNG] Calling Decode");
                if (!Decode(data)) {
                    BootConsole.WriteLine("[LodePNG] Decode returned false");
                    BuildFallback();
                } else {
                    BootConsole.WriteLine("[LodePNG] Decode succeeded");
                }
            } catch {
                BootConsole.WriteLine("[LodePNG] Exception in Decode");
                BuildFallback();
            }
        }
        
        private bool Decode(byte[] data) {
            BootConsole.WriteLine("[LodePNG] Decode: checking signature");
            // Check PNG signature
            if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47 ||
                data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A) {
                BootConsole.WriteLine("[LodePNG] Bad signature");
                return false;
            }
            
            BootConsole.WriteLine("[LodePNG] Signature OK, parsing chunks");
            int pos = 8;
            int width = 0, height = 0;
            int bitDepth = 0, colorType = 0, interlaceMethod = 0;
            byte[] palette = null;
            byte[] transparency = null;
            int transLen = 0;
            int idatTotalSize = 0;
            
            // First pass: parse chunks and count IDAT size
            int firstPassPos = pos;
            while (firstPassPos + 12 <= data.Length) {
                uint chunkLen = Read32BE(data, firstPassPos);
                if (chunkLen > 0x7FFFFFFF) break;
                
                if (ChunkTypeEquals(data, firstPassPos + 4, "IHDR")) {
                    width = (int)Read32BE(data, firstPassPos + 8);
                    height = (int)Read32BE(data, firstPassPos + 12);
                    bitDepth = data[firstPassPos + 16];
                    colorType = data[firstPassPos + 17];
                    interlaceMethod = data[firstPassPos + 20];
                } else if (ChunkTypeEquals(data, firstPassPos + 4, "IDAT")) {
                    idatTotalSize += (int)chunkLen;
                } else if (ChunkTypeEquals(data, firstPassPos + 4, "PLTE")) {
                    palette = new byte[chunkLen];
                    for (int i = 0; i < (int)chunkLen; i++) {
                        palette[i] = data[firstPassPos + 8 + i];
                    }
                } else if (ChunkTypeEquals(data, firstPassPos + 4, "tRNS")) {
                    transparency = new byte[chunkLen];
                    transLen = (int)chunkLen;
                    for (int i = 0; i < (int)chunkLen; i++) {
                        transparency[i] = data[firstPassPos + 8 + i];
                    }
                } else if (ChunkTypeEquals(data, firstPassPos + 4, "IEND")) {
                    break;
                }
                
                firstPassPos += 12 + (int)chunkLen;
            }
            
            // Validate
            BootConsole.WriteLine("[LodePNG] Parsed: " + width.ToString() + "x" + height.ToString() + " bd=" + bitDepth.ToString() + " ct=" + colorType.ToString());
            if (width <= 0 || height <= 0 || width > 8192 || height > 8192) {
                if (palette != null) palette.Dispose();
                if (transparency != null) transparency.Dispose();
                return false;
            }
            
            if (idatTotalSize <= 0) {
                if (palette != null) palette.Dispose();
                if (transparency != null) transparency.Dispose();
                return false;
            }
            
            // Validate color type and bit depth combination
            bool validCombo = false;
            switch (colorType) {
                case LCT_GREY:
                    validCombo = (bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8 || bitDepth == 16);
                    break;
                case LCT_RGB:
                case LCT_GREY_ALPHA:
                case LCT_RGBA:
                    validCombo = (bitDepth == 8 || bitDepth == 16);
                    break;
                case LCT_PALETTE:
                    validCombo = (bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8);
                    break;
            }
            
            if (!validCombo) {
                if (palette != null) palette.Dispose();
                if (transparency != null) transparency.Dispose();
                return false;
            }
            
            // Collect IDAT data
            BootConsole.WriteLine("[LodePNG] IDAT total size: " + idatTotalSize.ToString());
            byte[] compressedData = new byte[idatTotalSize];
            int compressedPos = 0;
            pos = 8;
            
            while (pos + 12 <= data.Length && compressedPos < idatTotalSize) {
                uint chunkLen = Read32BE(data, pos);
                
                if (ChunkTypeEquals(data, pos + 4, "IDAT")) {
                    for (int i = 0; i < (int)chunkLen && compressedPos < idatTotalSize; i++) {
                        compressedData[compressedPos++] = data[pos + 8 + i];
                    }
                } else if (ChunkTypeEquals(data, pos + 4, "IEND")) {
                    break;
                }
                
                pos += 12 + (int)chunkLen;
            }
            
            // Calculate expected decompressed size
            int bpp = GetBpp(colorType, bitDepth) * 8; // bits per pixel
            int expectedSize;
            
            if (interlaceMethod == 0) {
                expectedSize = GetRawSizeIdat(width, height, bpp);
            } else {
                // Adam7 interlaced
                expectedSize = 0;
                for (int pass = 0; pass < 7; pass++) {
                    int passW, passH;
                    Adam7GetPassValues(width, height, pass, out passW, out passH);
                    if (passW > 0 && passH > 0) {
                        expectedSize += GetRawSizeIdat(passW, passH, bpp);
                    }
                }
            }
            
            BootConsole.WriteLine("[LodePNG] Expected decompressed size: " + expectedSize.ToString());
            
            // Decompress
            BootConsole.WriteLine("[LodePNG] Decompressing...");
            byte[] rawPixels = ZlibDecompress(compressedData, 0, compressedData.Length, expectedSize);
            compressedData.Dispose();
            
            if (rawPixels == null) {
                BootConsole.WriteLine("[LodePNG] Decompression failed");
                if (palette != null) palette.Dispose();
                if (transparency != null) transparency.Dispose();
                return false;
            }
            
            BootConsole.WriteLine("[LodePNG] Decompression OK, processing scanlines");
            
            // Process scanlines
            int byteWidth = GetBpp(colorType, bitDepth);
            byte[] processedPixels = new byte[width * height * byteWidth];
            
            bool success;
            if (interlaceMethod == 0) {
                success = UnfilterScanlines(processedPixels, rawPixels, width, height, bpp);
            } else {
                success = ProcessInterlaced(processedPixels, rawPixels, width, height, bpp);
            }
            
            rawPixels.Dispose();
            
            if (!success) {
                BootConsole.WriteLine("[LodePNG] Scanline processing failed");
                processedPixels.Dispose();
                if (palette != null) palette.Dispose();
                if (transparency != null) transparency.Dispose();
                return false;
            }
            
            BootConsole.WriteLine("[LodePNG] Converting to ARGB");
            // Convert to ARGB
            ConvertToARGB(processedPixels, width, height, colorType, bitDepth, palette, transparency, transLen);
            
            processedPixels.Dispose();
            if (palette != null) palette.Dispose();
            if (transparency != null) transparency.Dispose();
            
            return true;
        }
        
        #endregion
    }
}
