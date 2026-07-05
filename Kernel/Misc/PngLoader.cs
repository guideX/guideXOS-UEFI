using guideXOS;
using System;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace guideXOS.Misc {
    /// <summary>
    /// Minimal PNG loader for GuideXOS UEFI kernel.
    /// 
    /// HARD CONSTRAINTS:
    /// - No exceptions (all errors return false)
    /// - No streams, no LINQ
    /// - No recursion
    /// - No dynamic reallocations (all buffers preallocated)
    /// - No external libraries
    /// - Fail via return false, never throw
    /// 
    /// SUPPORTED PNG SUBSET ONLY:
    /// - Color type 6 (RGBA) ONLY
    /// - Bit depth 8 ONLY
    /// - Non-interlaced ONLY
    /// - Ignores all ancillary chunks (only processes IHDR, IDAT, IEND)
    /// 
    /// Any PNG that doesn't match these exact requirements will fail.
    /// </summary>
    public unsafe class PngLoader {
        
        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        // We check each byte individually to avoid array allocation
        
        // Maximum image dimensions to prevent memory exhaustion
        // Assumption: 4096x4096 is a reasonable maximum for embedded use
        private const int MAX_WIDTH = 4096;
        private const int MAX_HEIGHT = 4096;
        
        // Maximum compressed data size (32MB should cover most PNGs)
        // Assumption: Compressed data won't exceed this for our use case
        private const int MAX_COMPRESSED_SIZE = 32 * 1024 * 1024;
        
        // Maximum iterations for DEFLATE decode loop to prevent infinite loops
        // Assumption: This should be enough for any valid PNG within our size limits
        private const int MAX_DECODE_ITERATIONS = 50000000;
        private const int MAX_INFLATE_SMOKE_SYMBOL_ITERATIONS = 8192;
        
        // Fixed Huffman table sizes
        // These are defined by DEFLATE spec and never change
        private const int LITLEN_TABLE_SIZE = 288;
        private const int DIST_TABLE_SIZE = 32;
        private const int CODELENGTH_TABLE_SIZE = 19;
        private const int FIRST_LENGTH_CODE_INDEX = 257;
        private const int LAST_LENGTH_CODE_INDEX = 285;
        
        // Preallocated work buffers - sizes based on DEFLATE maximum requirements
        // Assumption: These sizes cover all valid DEFLATE streams
        private static ushort[] _litLenTable;      // 32KB table for literal/length decode
        private static ushort[] _distTable;        // 32KB table for distance decode
        private static ushort[] _codeLenTable;     // Small table for code length decode
        private static int[] _litLenLengths;       // Code lengths for literal/length
        private static int[] _distLengths;         // Code lengths for distance
        private static int[] _codeLenLengths;      // Code lengths for code length alphabet
        private static int[] _allLengths;          // Combined lengths during dynamic decode
        private static int[] _blCount;             // Bit length counts for Huffman
        private static int[] _nextCode;            // Next code values for Huffman
        
        // Static initialization flag
        private static bool _initialized = false;

        /// <summary>
        /// Probe stages used by the UEFI cursor PNG investigation.
        /// </summary>
        public enum LoadProbeMode {
            None,
            AfterIhdr,
            AfterChunkScan,
            AfterIdatAggregation,
            DecompressPreMeta,
            DecompressNoop,
            DecompressBytesNoop,
            DecompressZlibHeader,
            DecompressOutputAlloc,
            DecompressDeflateHeader,
            DecompressHuffmanSetup,
            DecompressGateInlineReturn,
            DecompressGateHelperReturn,
            DecompressGateBoolOnly,
            DecompressGateNoStateCopy,
            DecompressPostGateFirstInstruction,
            DecompressAfterInputGate,
            DecompressPrepNoop,
            DecompressPrepMetadata,
            DecompressPrepBytes,
            DecompressPrepInline,
            DecompressPrepBoundary,
            DecompressPrepContext,
            DecompressInflateBoundary,
            DecompressInflateBitReader,
            DecompressInflateFirstSymbolDecode,
            DecompressInflateLiteralWrite,
            DecompressInflateLengthDistance,
            DecompressInflateOneStep,
            DecompressInflateSmoke,
            DecompressTinyBoundary,
            DecompressTinyBitReader,
            DecompressTinyFirstSymbol,
            DecompressTinyOneOp,
            DecompressTinyInflateSmoke,
            AfterDecompress,
            AfterImageCreate
        }

        private struct CursorInflateContext {
            public byte[] Input;
            public int InputLen;
            public int InPos;
            public int BitBuf;
            public int BitCount;
            public int BlockType;
        }

        private struct CursorPngTinyProbeContext {
            public byte[] Data;
            public byte[] CompressedData;
            public int CompressedPos;
            public int Width;
            public int Height;
            public byte BitDepth;
            public byte ColorType;
            public int IdatChunkCount;
            public int IdatTotalSize;
            public int ExpectedSize;
        }

        private static void ProbeBreadcrumb(string marker) {
            if (BootConsole.CurrentMode != guideXOS.BootMode.UEFI) return;
            Program.CursorImageProbeBreadcrumb(marker);
        }

        private static void ProbeValue(string marker, ulong value) {
            if (BootConsole.CurrentMode != guideXOS.BootMode.UEFI) return;
            Program.CursorImageProbeValue(marker, value);
        }
        
        /// <summary>
        /// Validate PNG signature and parse IHDR chunk.
        /// 
        /// This method performs early validation WITHOUT allocating any memory.
        /// Use this to quickly reject invalid or unsupported PNGs before
        /// committing resources.
        /// 
        /// Parameters:
        ///   pngData - Raw PNG file bytes (must not be null)
        ///   width   - Output: image width in pixels
        ///   height  - Output: image height in pixels
        /// 
        /// Returns:
        ///   true  - PNG signature valid and IHDR meets all requirements
        ///   false - Invalid signature, unsupported format, or any validation failure
        /// 
        /// Validation performed:
        ///   - PNG 8-byte signature at offset 0
        ///   - IHDR chunk present and correctly sized (13 bytes)
        ///   - Width > 0 and within MAX_WIDTH
        ///   - Height > 0 and within MAX_HEIGHT
        ///   - Bit depth == 8 (only supported depth)
        ///   - Color type == 6 (RGBA only)
        ///   - Compression method == 0 (deflate only)
        ///   - Filter method == 0 (standard PNG filtering)
        ///   - Interlace method == 0 (non-interlaced only)
        /// 
        /// Assumptions:
        ///   - pngData contains at least 33 bytes (8 sig + 8 chunk header + 13 IHDR + 4 CRC)
        ///   - Caller will use width/height only if method returns true
        ///   - No memory is allocated by this method
        /// </summary>
        public static bool ValidateSignatureAndParseIHDR(byte[] pngData, out int width, out int height) {
            // Initialize outputs to invalid values
            width = 0;
            height = 0;
            
            // Validate input is not null
            if (pngData == null) return false;
            
            // Minimum size: 8 (signature) + 8 (chunk header) + 13 (IHDR data) + 4 (CRC) = 33 bytes
            // This is the absolute minimum for a PNG with just IHDR (no IDAT/IEND, but we validate structure)
            if (pngData.Length < 33) return false;
            
            // ============================================================
            // PNG SIGNATURE VALIDATION
            // ============================================================
            // PNG signature is exactly 8 bytes: 89 50 4E 47 0D 0A 1A 0A
            // Each byte has specific meaning:
            //   [0] 0x89 - High bit set to detect transmission systems that don't support 8-bit
            //   [1] 0x50 - 'P' ASCII
            //   [2] 0x4E - 'N' ASCII  
            //   [3] 0x47 - 'G' ASCII
            //   [4] 0x0D - CR (carriage return) - detects CR-LF conversion
            //   [5] 0x0A - LF (line feed) - detects LF conversion
            //   [6] 0x1A - SUB (Ctrl-Z) - stops display under DOS
            //   [7] 0x0A - LF (line feed) - detects CR-LF conversion
            
            // Check byte 0: Must be 0x89 (high bit set, not ASCII)
            if (pngData[0] != 0x89) return false;
            
            // Check byte 1: Must be 0x50 ('P')
            if (pngData[1] != 0x50) return false;
            
            // Check byte 2: Must be 0x4E ('N')
            if (pngData[2] != 0x4E) return false;
            
            // Check byte 3: Must be 0x47 ('G')
            if (pngData[3] != 0x47) return false;
            
            // Check byte 4: Must be 0x0D (CR)
            if (pngData[4] != 0x0D) return false;
            
            // Check byte 5: Must be 0x0A (LF)
            if (pngData[5] != 0x0A) return false;
            
            // Check byte 6: Must be 0x1A (SUB/Ctrl-Z)
            if (pngData[6] != 0x1A) return false;
            
            // Check byte 7: Must be 0x0A (LF)
            if (pngData[7] != 0x0A) return false;
            
            // ============================================================
            // IHDR CHUNK PARSING
            // ============================================================
            // After signature, first chunk MUST be IHDR (per PNG specification)
            // Chunk structure:
            //   [8-11]  4 bytes: chunk data length (big-endian)
            //   [12-15] 4 bytes: chunk type ('IHDR' = 0x49484452)
            //   [16-28] 13 bytes: IHDR data
            //   [29-32] 4 bytes: CRC32 (we skip validation here)
            
            // Read chunk length at offset 8 (big-endian 32-bit)
            // IHDR data length MUST be exactly 13 bytes
            uint chunkLength = ((uint)pngData[8] << 24) |
                               ((uint)pngData[9] << 16) |
                               ((uint)pngData[10] << 8) |
                               (uint)pngData[11];
            
            // IHDR chunk data is always exactly 13 bytes (per PNG spec)
            if (chunkLength != 13) return false;
            
            // Read chunk type at offset 12 (big-endian 32-bit)
            // Must be 'IHDR' = 0x49484452
            uint chunkType = ((uint)pngData[12] << 24) |
                             ((uint)pngData[13] << 16) |
                             ((uint)pngData[14] << 8) |
                             (uint)pngData[15];
            
            // Validate chunk type is IHDR (0x49484452)
            // 'I' = 0x49, 'H' = 0x48, 'D' = 0x44, 'R' = 0x52
            if (chunkType != 0x49484452) return false;
            
            // ============================================================
            // IHDR DATA PARSING (13 bytes starting at offset 16)
            // ============================================================
            // IHDR data layout:
            //   [16-19] 4 bytes: width (big-endian)
            //   [20-23] 4 bytes: height (big-endian)
            //   [24]    1 byte:  bit depth
            //   [25]    1 byte:  color type
            //   [26]    1 byte:  compression method
            //   [27]    1 byte:  filter method
            //   [28]    1 byte:  interlace method
            
            // Read width at offset 16 (big-endian 32-bit)
            int w = (int)(((uint)pngData[16] << 24) |
                          ((uint)pngData[17] << 16) |
                          ((uint)pngData[18] << 8) |
                          (uint)pngData[19]);
            
            // Read height at offset 20 (big-endian 32-bit)
            int h = (int)(((uint)pngData[20] << 24) |
                          ((uint)pngData[21] << 16) |
                          ((uint)pngData[22] << 8) |
                          (uint)pngData[23]);
            
            // Read bit depth at offset 24
            byte bitDepth = pngData[24];
            
            // Read color type at offset 25
            byte colorType = pngData[25];
            
            // Read compression method at offset 26
            byte compressionMethod = pngData[26];
            
            // Read filter method at offset 27
            byte filterMethod = pngData[27];
            
            // Read interlace method at offset 28
            byte interlaceMethod = pngData[28];
            
            // ============================================================
            // VALIDATION
            // ============================================================
            
            // Validate width > 0
            // PNG spec allows up to 2^31-1, but we limit to MAX_WIDTH for memory safety
            if (w <= 0) return false;
            if (w > MAX_WIDTH) return false;
            
            // Validate height > 0
            // PNG spec allows up to 2^31-1, but we limit to MAX_HEIGHT for memory safety
            if (h <= 0) return false;
            if (h > MAX_HEIGHT) return false;
            
            // Validate bit depth == 8
            // We only support 8-bit samples (most common for RGBA)
            // Other valid PNG bit depths are 1, 2, 4, 16 but we reject them
            if (bitDepth != 8) return false;
            
            // Validate color type == 6 (RGBA)
            // Color type 6 = truecolor with alpha (RGBA)
            // Other valid types: 0=grayscale, 2=RGB, 3=palette, 4=grayscale+alpha
            // We only support type 6
            if (colorType != 6) return false;
            
            // Validate compression method == 0
            // PNG only defines compression method 0 (deflate)
            // Any other value is invalid per PNG spec
            if (compressionMethod != 0) return false;
            
            // Validate filter method == 0
            // PNG only defines filter method 0 (adaptive filtering with 5 filter types)
            // Any other value is invalid per PNG spec
            if (filterMethod != 0) return false;
            
            // Validate interlace method == 0 (non-interlaced)
            // Interlace method 0 = no interlacing
            // Interlace method 1 = Adam7 interlacing (not supported)
            // Any other value is invalid per PNG spec
            if (interlaceMethod != 0) return false;
            
            // ============================================================
            // SUCCESS - All validations passed
            // ============================================================
            width = w;
            height = h;
            return true;
        }
        
        /// <summary>
        /// Iterate through PNG chunks and collect IDAT compressed data.
        /// 
        /// This method performs a single pass through all PNG chunks after the signature,
        /// collecting compressed image data from IDAT chunks into a preallocated buffer.
        /// 
        /// Parameters:
        ///   pngData          - Raw PNG file bytes (must have valid signature, already validated)
        ///   compressedBuffer - Preallocated buffer to receive IDAT data (caller allocates)
        ///   bufferSize       - Size of compressedBuffer in bytes
        ///   bytesWritten     - Output: actual bytes written to compressedBuffer
        /// 
        /// Returns:
        ///   true  - Successfully iterated all chunks and found IEND
        ///   false - Error: bounds violation, missing IEND, buffer overflow, or invalid chunk
        /// 
        /// Chunk handling:
        ///   IHDR (0x49484452) - Already parsed by ValidateSignatureAndParseIHDR, skip data
        ///   IDAT (0x49444154) - Append chunk data to compressedBuffer
        ///   IEND (0x49454E44) - Stop iteration, return success
        ///   All others        - Skip (length + 4 bytes CRC)
        /// 
        /// Assumptions:
        ///   - PNG signature at offset 0-7 is valid (caller verified)
        ///   - IHDR is first chunk (PNG spec requirement)
        ///   - compressedBuffer is large enough (caller sized based on first pass or estimate)
        ///   - No memory allocation occurs in this method
        /// </summary>
        public static bool IterateChunks(byte[] pngData, byte[] compressedBuffer, int bufferSize, out int bytesWritten) {
            // Initialize output
            bytesWritten = 0;
            
            // ============================================================
            // INPUT VALIDATION
            // ============================================================
            
            // Validate pngData is not null
            if (pngData == null) return false;
            
            // Validate compressedBuffer is not null
            if (compressedBuffer == null) return false;
            
            // Validate bufferSize is positive and matches buffer
            if (bufferSize <= 0) return false;
            if (bufferSize > compressedBuffer.Length) return false;
            
            // Minimum PNG size: 8 (sig) + 25 (IHDR chunk) + 12 (IEND chunk) = 45 bytes
            // But we need at least one IDAT chunk too for valid image
            if (pngData.Length < 45) return false;
            
            // ============================================================
            // CHUNK ITERATION
            // ============================================================
            // PNG chunk structure (repeating after 8-byte signature):
            //   [0-3]   4 bytes: data length (big-endian uint32)
            //   [4-7]   4 bytes: chunk type (4 ASCII characters)
            //   [8...]  N bytes: chunk data (where N = length)
            //   [N+8..] 4 bytes: CRC32 checksum (we skip validation)
            //
            // Total chunk size = 4 (length) + 4 (type) + length (data) + 4 (CRC) = 12 + length
            
            // Start after 8-byte PNG signature
            int pos = 8;
            
            // Track if we found required chunks
            bool foundIHDR = false;
            bool foundIDAT = false;
            bool foundIEND = false;
            
            // Current write position in compressed buffer
            int writePos = 0;
            
            // Safety counter to prevent infinite loops on malformed data
            // Max chunks = file size / 12 (minimum chunk size) + some margin
            int maxChunks = (pngData.Length / 12) + 100;
            int chunkCount = 0;
            
            // Iterate through all chunks
            while (pos < pngData.Length) {
                // Safety: prevent infinite loop
                chunkCount++;
                if (chunkCount > maxChunks) return false;
                
                // --------------------------------------------------------
                // BOUNDS CHECK: Need at least 12 bytes for chunk header + CRC
                // --------------------------------------------------------
                // Minimum chunk structure: 4 (length) + 4 (type) + 0 (data) + 4 (CRC) = 12 bytes
                if (pos + 12 > pngData.Length) return false;
                
                // --------------------------------------------------------
                // READ CHUNK LENGTH (big-endian uint32 at offset pos)
                // --------------------------------------------------------
                uint chunkLength = ((uint)pngData[pos] << 24) |
                                   ((uint)pngData[pos + 1] << 16) |
                                   ((uint)pngData[pos + 2] << 8) |
                                   (uint)pngData[pos + 3];
                
                // Validate chunk length is reasonable
                // PNG spec allows up to 2^31-1, but we cap at 2GB for safety
                if (chunkLength > 0x7FFFFFFF) return false;
                
                // --------------------------------------------------------
                // BOUNDS CHECK: Verify entire chunk fits in file
                // --------------------------------------------------------
                // Total chunk size: 4 (length) + 4 (type) + chunkLength (data) + 4 (CRC)
                int totalChunkSize = 12 + (int)chunkLength;
                
                // Check for integer overflow
                if (totalChunkSize < 12) return false;  // Overflow occurred
                
                // Check chunk fits in remaining data
                if (pos + totalChunkSize > pngData.Length) return false;
                
                // --------------------------------------------------------
                // READ CHUNK TYPE (4 ASCII bytes at offset pos + 4)
                // --------------------------------------------------------
                // Read as big-endian uint32 for easy comparison
                uint chunkType = ((uint)pngData[pos + 4] << 24) |
                                 ((uint)pngData[pos + 5] << 16) |
                                 ((uint)pngData[pos + 6] << 8) |
                                 (uint)pngData[pos + 7];
                
                // --------------------------------------------------------
                // HANDLE CHUNK BY TYPE
                // --------------------------------------------------------
                
                // IHDR chunk: 0x49484452 ('I','H','D','R')
                if (chunkType == 0x49484452) {
                    // IHDR must be first chunk (per PNG spec)
                    if (foundIHDR) return false;  // Duplicate IHDR is invalid
                    if (foundIDAT) return false;  // IHDR after IDAT is invalid
                    
                    // IHDR data length must be exactly 13 bytes
                    if (chunkLength != 13) return false;
                    
                    // Mark IHDR as found
                    // Note: IHDR data already parsed by ValidateSignatureAndParseIHDR
                    // We just skip over it here
                    foundIHDR = true;
                    
                    // Advance past this chunk
                    pos += totalChunkSize;
                    continue;
                }
                
                // IDAT chunk: 0x49444154 ('I','D','A','T')
                if (chunkType == 0x49444154) {
                    // IDAT must come after IHDR
                    if (!foundIHDR) return false;
                    
                    // Mark that we found at least one IDAT
                    foundIDAT = true;
                    
                    // --------------------------------------------------------
                    // APPEND IDAT DATA TO COMPRESSED BUFFER
                    // --------------------------------------------------------
                    
                    // Calculate data start offset (after length + type)
                    int dataStart = pos + 8;
                    int dataLength = (int)chunkLength;
                    
                    // Check if we have room in the buffer
                    if (writePos + dataLength > bufferSize) {
                        // Buffer overflow - caller didn't allocate enough space
                        return false;
                    }
                    
                    // Copy IDAT data byte-by-byte to compressed buffer
                    // Note: We use explicit indexing, no memcpy or Array.Copy
                    for (int i = 0; i < dataLength; i++) {
                        compressedBuffer[writePos] = pngData[dataStart + i];
                        writePos++;
                    }
                    
                    // Advance past this chunk
                    pos += totalChunkSize;
                    continue;
                }
                
                // IEND chunk: 0x49454E44 ('I','E','N','D')
                if (chunkType == 0x49454E44) {
                    // IEND must come after at least one IDAT
                    if (!foundIDAT) return false;
                    
                    // IEND data length must be 0 (no data)
                    if (chunkLength != 0) return false;
                    
                    // Mark IEND as found - this ends the PNG
                    foundIEND = true;
                    
                    // Stop iteration - IEND is the last chunk
                    break;
                }
                
                // --------------------------------------------------------
                // ALL OTHER CHUNKS: Skip (ancillary or unknown)
                // --------------------------------------------------------
                // This includes: PLTE, tRNS, cHRM, gAMA, iCCP, sBIT, sRGB, 
                // bKGD, hIST, pHYs, sPLT, tIME, iTXt, tEXt, zTXt, etc.
                //
                // We simply skip: length(4) + type(4) + data(chunkLength) + CRC(4)
                // which is totalChunkSize bytes
                
                pos += totalChunkSize;
            }
            
            // ============================================================
            // FINAL VALIDATION
            // ============================================================
            
            // Must have found IHDR
            if (!foundIHDR) return false;
            
            // Must have found at least one IDAT
            if (!foundIDAT) return false;
            
            // Must have found IEND
            if (!foundIEND) return false;
            
            // Must have written some compressed data
            if (writePos == 0) return false;
            
            // ============================================================
            // SUCCESS
            // ============================================================
            bytesWritten = writePos;
            return true;
        }
        
        /// <summary>
        /// Calculate total IDAT data size by scanning chunks.
        /// 
        /// This is a first-pass method to determine how large the compressed
        /// data buffer needs to be before calling IterateChunks.
        /// 
        /// Parameters:
        ///   pngData    - Raw PNG file bytes
        ///   totalSize  - Output: total bytes of IDAT data across all IDAT chunks
        /// 
        /// Returns:
        ///   true  - Successfully scanned, totalSize is valid
        ///   false - Error: invalid PNG structure
        /// 
        /// Assumptions:
        ///   - PNG signature is valid
        ///   - No memory allocation
        /// </summary>
        public static bool CalculateIdatSize(byte[] pngData, out int totalSize) {
            totalSize = 0;
            
            if (pngData == null) return false;
            if (pngData.Length < 45) return false;
            
            int pos = 8;  // Start after signature
            int idatTotal = 0;
            bool foundIEND = false;
            
            // Safety limit
            int maxChunks = (pngData.Length / 12) + 100;
            int chunkCount = 0;
            
            while (pos + 12 <= pngData.Length) {
                chunkCount++;
                if (chunkCount > maxChunks) return false;
                
                // Read chunk length
                uint chunkLength = ((uint)pngData[pos] << 24) |
                                   ((uint)pngData[pos + 1] << 16) |
                                   ((uint)pngData[pos + 2] << 8) |
                                   (uint)pngData[pos + 3];
                
                if (chunkLength > 0x7FFFFFFF) return false;
                
                int totalChunkSize = 12 + (int)chunkLength;
                if (totalChunkSize < 12) return false;
                if (pos + totalChunkSize > pngData.Length) return false;
                
                // Read chunk type
                uint chunkType = ((uint)pngData[pos + 4] << 24) |
                                 ((uint)pngData[pos + 5] << 16) |
                                 ((uint)pngData[pos + 6] << 8) |
                                 (uint)pngData[pos + 7];
                
                // Count IDAT size
                if (chunkType == 0x49444154) {  // IDAT
                    // Check for overflow
                    if (idatTotal + (int)chunkLength < idatTotal) return false;
                    idatTotal += (int)chunkLength;
                    
                    // Cap at maximum
                    if (idatTotal > MAX_COMPRESSED_SIZE) return false;
                }
                // Stop at IEND
                else if (chunkType == 0x49454E44) {  // IEND
                    foundIEND = true;
                    break;
                }
                
                pos += totalChunkSize;
            }
            
            if (!foundIEND) return false;
            if (idatTotal == 0) return false;
            
            totalSize = idatTotal;
            return true;
        }
        
        /// <summary>
        /// Decompress zlib-wrapped DEFLATE data.
        /// 
        /// This is the main PUBLIC decompression entry point for the PNG pipeline.
        /// It handles zlib header validation and delegates to DEFLATE decoding.
        /// 
        /// MINIMAL ZLIB/DEFLATE DECOMPRESSOR FOR PNG USE ONLY
        /// ====================================================
        /// 
        /// HARD CONSTRAINTS (kernel/UEFI environment):
        ///   - No allocations of any kind (all buffers preallocated by caller)
        ///   - No recursion (iterative loops only)
        ///   - No exceptions (all errors return false)
        ///   - No streams or LINQ
        ///   - No dictionaries beyond what DEFLATE requires
        ///   - No gzip support (zlib wrapper only)
        ///   - No Adler-32 validation (we skip the trailing checksum)
        /// 
        /// SUPPORTED DEFLATE FEATURES:
        ///   - Block type 0: Stored (uncompressed) blocks
        ///   - Block type 1: Fixed Huffman codes
        ///   - Block type 2: Dynamic Huffman codes
        ///   - LZ77 back-references (length/distance pairs)
        /// 
        /// NOT IMPLEMENTED:
        ///   - Streaming API (single buffer in, single buffer out)
        ///   - Compression (decompression only)
        ///   - Generic zlib helpers
        ///   - Memory resizing
        ///   - CRC or checksum verification
        /// 
        /// Parameters:
        ///   compressed         - Zlib-wrapped compressed data (from concatenated IDAT chunks)
        ///   compressedLength   - Number of valid bytes in compressed buffer
        ///   output             - Preallocated output buffer for decompressed data
        ///   expectedOutputSize - Expected size of decompressed data (height * (width * 4 + 1))
        /// 
        /// Returns:
        ///   true  - Decompression successful, output buffer contains valid data
        ///   false - Decompression failed (invalid data, buffer too small, etc.)
        /// 
        /// Zlib format (RFC 1950):
        ///   [0]     CMF (Compression Method and Flags)
        ///           - Bits 0-3: Compression method (must be 8 = deflate)
        ///           - Bits 4-7: Compression info (window size log2 - 8)
        ///   [1]     FLG (Flags)
        ///           - Bits 0-4: FCHECK (header checksum)
        ///           - Bit 5: FDICT (preset dictionary flag, must be 0)
        ///           - Bits 6-7: FLEVEL (compression level, ignored)
        ///           - Constraint: (CMF * 256 + FLG) % 31 == 0
        ///   [2...]  DEFLATE compressed data (RFC 1951)
        ///   [N-4..] Adler-32 checksum (we skip validation)
        /// 
        /// DEFLATE format (RFC 1951):
        ///   Each block starts with:
        ///     - BFINAL (1 bit): 1 if this is the last block
        ///     - BTYPE (2 bits): block type
        ///       - 00: stored (no compression)
        ///       - 01: fixed Huffman codes
        ///       - 10: dynamic Huffman codes
        ///       - 11: reserved (error)
        /// 
        /// Assumptions:
        ///   - compressed buffer contains valid zlib data
        ///   - output buffer is EXACTLY expectedOutputSize bytes (preallocated)
        ///   - Initialize() has been called (static work buffers ready)
        ///   - No preset dictionary (FDICT = 0)
        ///   - Input is a single zlib stream (as used by PNG IDAT)
        /// </summary>
        public static bool InflateZlib(byte[] compressed, int compressedLength, byte[] output, int expectedOutputSize) {
            // ============================================================
            // PREREQUISITE: Static buffers must be initialized
            // ============================================================
            // Assumption: Caller has called Initialize() before this method.
            // The Huffman decode tables and work arrays are statically allocated
            // once and reused for all PNG decoding operations.
            if (!_initialized) return false;
            
            // ============================================================
            // INPUT VALIDATION
            // ============================================================
            // All bounds are checked strictly. Any violation returns false.
            
            // Validate compressed buffer exists
            if (compressed == null) return false;
            
            // Validate compressed length is positive and within buffer
            if (compressedLength <= 0) return false;
            if (compressedLength > compressed.Length) return false;
            
            // Minimum zlib stream size:
            //   2 bytes (header) + 1 byte (minimum DEFLATE) + 4 bytes (Adler-32) = 7 bytes
            // Assumption: Any valid PNG IDAT data will be larger than this.
            if (compressedLength < 7) return false;
            
            // Validate output buffer exists
            if (output == null) return false;
            
            // Validate expected output size is positive and within buffer
            // Assumption: Caller has computed exact expected size as:
            //   height * (width * bytesPerPixel + 1)  // +1 for filter byte per row
            if (expectedOutputSize <= 0) return false;
            if (expectedOutputSize > output.Length) return false;
            
            // ============================================================
            // ZLIB HEADER VALIDATION (RFC 1950)
            // ============================================================
            
            // Byte 0: CMF (Compression Method and Flags)
            //   Bits 0-3: CM (Compression Method) - must be 8 (deflate)
            //   Bits 4-7: CINFO (Compression Info) - log2(window size) - 8
            //             For deflate, valid range is 0-7 (window 256 to 32K)
            byte cmf = compressed[0];
            
            // Extract compression method from lower 4 bits
            // Assumption: PNG always uses deflate (method 8)
            int compressionMethod = cmf & 0x0F;
            if (compressionMethod != 8) return false;
            
            // Extract window size info from upper 4 bits
            // Assumption: We don't enforce window size limits since output buffer
            // is preallocated to exact expected size.
            int cinfo = (cmf >> 4) & 0x0F;
            if (cinfo > 7) return false;  // Invalid per RFC 1950
            
            // Byte 1: FLG (Flags)
            //   Bits 0-4: FCHECK - makes (CMF*256 + FLG) divisible by 31
            //   Bit 5: FDICT - preset dictionary flag (must be 0 for PNG)
            //   Bits 6-7: FLEVEL - compression level (informational, ignored)
            byte flg = compressed[1];
            
            // Check FDICT flag (bit 5)
            // Assumption: PNG never uses preset dictionaries
            bool fdict = (flg & 0x20) != 0;
            if (fdict) return false;
            
            // Validate header checksum
            // Constraint: (CMF * 256 + FLG) must be divisible by 31
            // This catches transmission errors in the header
            int headerCheck = cmf * 256 + flg;
            if (headerCheck % 31 != 0) return false;
            
            // ============================================================
            // DEFLATE DECOMPRESSION (RFC 1951)
            // ============================================================
            // The DEFLATE stream starts at offset 2 (after zlib header).
            // We use an explicit bit reader with:
            //   - bitBuf: holds accumulated bits (LSB-first)
            //   - bitCount: number of valid bits in bitBuf
            // Bits are consumed from LSB side.
            
            int inPos = 2;          // Current read position in compressed buffer
            int outPos = 0;         // Current write position in output buffer
            int bitBuf = 0;         // Bit accumulator (up to 32 bits)
            int bitCount = 0;       // Number of valid bits in bitBuf
            int blockIterations = 0; // Safety counter for block loop
            
            bool lastBlock = false;
            
            // Process DEFLATE blocks until BFINAL=1 or output full
            // Assumption: Valid PNG data will hit BFINAL=1 before output overflows
            while (!lastBlock && outPos < expectedOutputSize) {
                // Safety: prevent infinite loops on malformed data
                // Assumption: PNG won't have more than 10000 blocks
                blockIterations++;
                if (blockIterations > 10000) return false;
                
                // --------------------------------------------------------
                // READ BLOCK HEADER (3 bits: BFINAL + BTYPE)
                // --------------------------------------------------------
                
                // Ensure we have at least 3 bits for block header
                while (bitCount < 3) {
                    if (inPos >= compressedLength) return false;
                    bitBuf |= compressed[inPos++] << bitCount;
                    bitCount += 8;
                }
                
                // BFINAL (1 bit): 1 if this is the last block
                lastBlock = (bitBuf & 1) == 1;
                bitBuf >>= 1;
                bitCount--;
                
                // BTYPE (2 bits): block type
                int blockType = bitBuf & 3;
                bitBuf >>= 2;
                bitCount -= 2;
                
                // --------------------------------------------------------
                // PROCESS BLOCK BY TYPE
                // --------------------------------------------------------
                
                if (blockType == 0) {
                    // ====================================================
                    // BLOCK TYPE 0: STORED (no compression)
                    // ====================================================
                    // Format:
                    //   - Discard remaining bits to byte boundary
                    //   - LEN (2 bytes, little-endian): number of data bytes
                    //   - NLEN (2 bytes, little-endian): one's complement of LEN
                    //   - LEN bytes of literal data
                    
                    // Discard remaining bits in current byte (align to byte boundary)
                    // Assumption: Stored blocks always start byte-aligned after header
                    bitBuf = 0;
                    bitCount = 0;
                    
                    // Need 4 bytes for LEN and NLEN
                    if (inPos + 4 > compressedLength) return false;
                    
                    // Read LEN (little-endian 16-bit)
                    int len = compressed[inPos] | (compressed[inPos + 1] << 8);
                    inPos += 2;
                    
                    // Read NLEN (little-endian 16-bit)
                    int nlen = compressed[inPos] | (compressed[inPos + 1] << 8);
                    inPos += 2;
                    
                    // Validate: NLEN must be one's complement of LEN
                    // This catches corruption in the length fields
                    if ((len ^ 0xFFFF) != nlen) return false;
                    
                    // Validate: enough input data for literal bytes
                    if (inPos + len > compressedLength) return false;
                    
                    // Validate: enough output space for literal bytes
                    if (outPos + len > expectedOutputSize) return false;
                    
                    // Copy literal data byte-by-byte
                    // Assumption: No Array.Copy available in kernel environment
                    for (int i = 0; i < len; i++) {
                        output[outPos++] = compressed[inPos++];
                    }
                }
                else if (blockType == 1) {
                    // ====================================================
                    // BLOCK TYPE 1: FIXED HUFFMAN CODES
                    // ====================================================
                    // Uses predefined Huffman tables (RFC 1951 section 3.2.6):
                    //   Literal/Length codes:
                    //     0-143:   8-bit codes (00110000 - 10111111)
                    //     144-255: 9-bit codes (110010000 - 111111111)
                    //     256-279: 7-bit codes (0000000 - 0010111)
                    //     280-287: 8-bit codes (11000000 - 11000111)
                    //   Distance codes:
                    //     0-31:    5-bit codes (00000 - 11111)
                    
                    // Build fixed literal/length code lengths
                    // Assumption: _litLenLengths array is preallocated (288 entries)
                    for (int i = 0; i < LITLEN_TABLE_SIZE; i++) {
                        if (i < 144) _litLenLengths[i] = 8;
                        else if (i < 256) _litLenLengths[i] = 9;
                        else if (i < 280) _litLenLengths[i] = 7;
                        else _litLenLengths[i] = 8;
                    }
                    
                    // Build fixed distance code lengths (all 5-bit)
                    // Assumption: _distLengths array is preallocated (32 entries)
                    for (int i = 0; i < DIST_TABLE_SIZE; i++) {
                        _distLengths[i] = 5;
                    }
                    
                    // Build fast decode tables from code lengths
                    if (!BuildDecodeTable(_litLenLengths, LITLEN_TABLE_SIZE, 15, _litLenTable, 1 << 15)) return false;
                    if (!BuildDecodeTable(_distLengths, DIST_TABLE_SIZE, 15, _distTable, 1 << 15)) return false;
                    
                    // Decode Huffman-compressed data
                    if (!InflateHuffmanBlock(compressed, compressedLength, output, expectedOutputSize,
                                             ref inPos, ref outPos, ref bitBuf, ref bitCount)) {
                        return false;
                    }
                }
                else if (blockType == 2) {
                    // ====================================================
                    // BLOCK TYPE 2: DYNAMIC HUFFMAN CODES
                    // ====================================================
                    // Custom Huffman tables encoded in the stream.
                    // Format:
                    //   HLIT (5 bits): # of literal/length codes - 257 (257-286)
                    //   HDIST (5 bits): # of distance codes - 1 (1-32)
                    //   HCLEN (4 bits): # of code length codes - 4 (4-19)
                    //   Code length code lengths (HCLEN * 3 bits)
                    //   Literal/Length code lengths (HLIT codes, Huffman encoded)
                    //   Distance code lengths (HDIST codes, Huffman encoded)
                    
                    // Need 14 bits for header: HLIT(5) + HDIST(5) + HCLEN(4)
                    while (bitCount < 14) {
                        if (inPos >= compressedLength) return false;
                        bitBuf |= compressed[inPos++] << bitCount;
                        bitCount += 8;
                    }
                    
                    // HLIT: number of literal/length codes (257-286)
                    int hlit = (bitBuf & 0x1F) + 257;
                    bitBuf >>= 5;
                    bitCount -= 5;
                    
                    // HDIST: number of distance codes (1-32)
                    int hdist = (bitBuf & 0x1F) + 1;
                    bitBuf >>= 5;
                    bitCount -= 5;
                    
                    // HCLEN: number of code length codes (4-19)
                    int hclen = (bitBuf & 0x0F) + 4;
                    bitBuf >>= 4;
                    bitCount -= 4;
                    
                    // Validate counts
                    if (hlit > 286) return false;
                    if (hdist > 32) return false;
                    
                    // ------------------------------------------------
                    // READ CODE LENGTH CODE LENGTHS
                    // ------------------------------------------------
                    // The code length alphabet uses 19 symbols (0-18).
                    // Code lengths are transmitted in a specific order
                    // designed to put most common lengths first.
                    
                    // Order of code length code lengths (RFC 1951)
                    // Assumption: This order is fixed by the DEFLATE spec
                    int cl0 = 16, cl1 = 17, cl2 = 18, cl3 = 0;
                    int cl4 = 8, cl5 = 7, cl6 = 9, cl7 = 6;
                    int cl8 = 10, cl9 = 5, cl10 = 11, cl11 = 4;
                    int cl12 = 12, cl13 = 3, cl14 = 13, cl15 = 2;
                    int cl16 = 14, cl17 = 1, cl18 = 15;
                    
                    // Clear code length lengths array
                    for (int i = 0; i < CODELENGTH_TABLE_SIZE; i++) {
                        _codeLenLengths[i] = 0;
                    }
                    
                    // Read HCLEN code length code lengths (3 bits each)
                    for (int i = 0; i < hclen; i++) {
                        while (bitCount < 3) {
                            if (inPos >= compressedLength) return false;
                            bitBuf |= compressed[inPos++] << bitCount;
                            bitCount += 8;
                        }
                        
                        int codeLen = bitBuf & 7;
                        bitBuf >>= 3;
                        bitCount -= 3;
                        
                        // Store in correct position based on order
                        int orderIdx;
                        if (i == 0) orderIdx = cl0;
                        else if (i == 1) orderIdx = cl1;
                        else if (i == 2) orderIdx = cl2;
                        else if (i == 3) orderIdx = cl3;
                        else if (i == 4) orderIdx = cl4;
                        else if (i == 5) orderIdx = cl5;
                        else if (i == 6) orderIdx = cl6;
                        else if (i == 7) orderIdx = cl7;
                        else if (i == 8) orderIdx = cl8;
                        else if (i == 9) orderIdx = cl9;
                        else if (i == 10) orderIdx = cl10;
                        else if (i == 11) orderIdx = cl11;
                        else if (i == 12) orderIdx = cl12;
                        else if (i == 13) orderIdx = cl13;
                        else if (i == 14) orderIdx = cl14;
                        else if (i == 15) orderIdx = cl15;
                        else if (i == 16) orderIdx = cl16;
                        else if (i == 17) orderIdx = cl17;
                        else orderIdx = cl18;
                        
                        _codeLenLengths[orderIdx] = codeLen;
                    }
                    
                    // Build decode table for code length alphabet
                    if (!BuildDecodeTable(_codeLenLengths, CODELENGTH_TABLE_SIZE, 7, _codeLenTable, 1 << 7)) {
                        return false;
                    }
                    
                    // ------------------------------------------------
                    // DECODE LITERAL/LENGTH AND DISTANCE CODE LENGTHS
                    // ------------------------------------------------
                    // The code lengths are encoded using the code length alphabet.
                    // Special symbols:
                    //   0-15:  Literal code length value
                    //   16:    Copy previous length 3-6 times (2 extra bits)
                    //   17:    Repeat zero 3-10 times (3 extra bits)
                    //   18:    Repeat zero 11-138 times (7 extra bits)
                    
                    int totalCodes = hlit + hdist;
                    
                    // Clear combined lengths array
                    for (int i = 0; i < 320; i++) {
                        _allLengths[i] = 0;
                    }
                    
                    int idx = 0;
                    int safetyCounter = 0;
                    
                    while (idx < totalCodes) {
                        // Safety: prevent infinite loop
                        safetyCounter++;
                        if (safetyCounter > totalCodes + 1000) return false;
                        
                        // Ensure enough bits for decoding
                        while (bitCount < 15) {
                            if (inPos >= compressedLength) return false;
                            bitBuf |= compressed[inPos++] << bitCount;
                            bitCount += 8;
                        }
                        
                        // Decode code length symbol
                        int sym = DecodeSymbol(_codeLenTable, 7, ref bitBuf, ref bitCount);
                        if (sym < 0) return false;
                        
                        if (sym < 16) {
                            // Literal code length (0-15)
                            _allLengths[idx++] = sym;
                        }
                        else if (sym == 16) {
                            // Copy previous code length 3-6 times
                            while (bitCount < 2) {
                                if (inPos >= compressedLength) return false;
                                bitBuf |= compressed[inPos++] << bitCount;
                                bitCount += 8;
                            }
                            int repeatCount = 3 + (bitBuf & 3);
                            bitBuf >>= 2;
                            bitCount -= 2;
                            
                            if (idx == 0) return false;  // No previous value
                            int prevLen = _allLengths[idx - 1];
                            
                            for (int r = 0; r < repeatCount && idx < totalCodes; r++) {
                                _allLengths[idx++] = prevLen;
                            }
                        }
                        else if (sym == 17) {
                            // Repeat zero 3-10 times
                            while (bitCount < 3) {
                                if (inPos >= compressedLength) return false;
                                bitBuf |= compressed[inPos++] << bitCount;
                                bitCount += 8;
                            }
                            int repeatCount = 3 + (bitBuf & 7);
                            bitBuf >>= 3;
                            bitCount -= 3;
                            
                            for (int r = 0; r < repeatCount && idx < totalCodes; r++) {
                                _allLengths[idx++] = 0;
                            }
                        }
                        else if (sym == 18) {
                            // Repeat zero 11-138 times
                            while (bitCount < 7) {
                                if (inPos >= compressedLength) return false;
                                bitBuf |= compressed[inPos++] << bitCount;
                                bitCount += 8;
                            }
                            int repeatCount = 11 + (bitBuf & 0x7F);
                            bitBuf >>= 7;
                            bitCount -= 7;
                            
                            for (int r = 0; r < repeatCount && idx < totalCodes; r++) {
                                _allLengths[idx++] = 0;
                            }
                        }
                        else {
                            // Invalid symbol
                            return false;
                        }
                    }
                    
                    // ------------------------------------------------
                    // SPLIT INTO LITERAL/LENGTH AND DISTANCE TABLES
                    // ------------------------------------------------
                    
                    // Copy literal/length code lengths
                    for (int i = 0; i < hlit && i < LITLEN_TABLE_SIZE; i++) {
                        _litLenLengths[i] = _allLengths[i];
                    }
                    for (int i = hlit; i < LITLEN_TABLE_SIZE; i++) {
                        _litLenLengths[i] = 0;
                    }
                    
                    // Copy distance code lengths
                    for (int i = 0; i < hdist && i < DIST_TABLE_SIZE; i++) {
                        _distLengths[i] = _allLengths[hlit + i];
                    }
                    for (int i = hdist; i < DIST_TABLE_SIZE; i++) {
                        _distLengths[i] = 0;
                    }
                    
                    // Build fast decode tables
                    if (!BuildDecodeTable(_litLenLengths, LITLEN_TABLE_SIZE, 15, _litLenTable, 1 << 15)) return false;
                    if (!BuildDecodeTable(_distLengths, DIST_TABLE_SIZE, 15, _distTable, 1 << 15)) return false;
                    
                    // Decode Huffman-compressed data
                    if (!InflateHuffmanBlock(compressed, compressedLength, output, expectedOutputSize,
                                             ref inPos, ref outPos, ref bitBuf, ref bitCount)) {
                        return false;
                    }
                }
                else {
                    // ====================================================
                    // BLOCK TYPE 3: RESERVED (invalid)
                    // ====================================================
                    return false;
                }
            }
            
            // ============================================================
            // SUCCESS: Verify we produced expected output
            // ============================================================
            // Assumption: For PNG, we expect exactly expectedOutputSize bytes.
            // Some DEFLATE streams might produce less, but PNG requires exact size.
            if (outPos != expectedOutputSize) return false;
            
            return true;
        }
        
        /// <summary>
        /// Decode a Huffman-compressed block using pre-built tables.
        /// 
        /// This is called for both fixed (type 1) and dynamic (type 2) blocks
        /// after the appropriate decode tables have been built.
        /// 
        /// Handles:
        ///   - Literal bytes (symbols 0-255): output directly
        ///   - End of block (symbol 256): return success
        ///   - Length/distance pairs (symbols 257-285): LZ77 back-reference
        /// 
        /// Parameters:
        ///   input      - Compressed data buffer
        ///   inputLen   - Length of compressed data
        ///   output     - Output buffer (preallocated)
        ///   outputMax  - Maximum output size
        ///   inPos      - Current input position (updated)
        ///   outPos     - Current output position (updated)
        ///   bitBuf     - Bit accumulator (updated)
        ///   bitCount   - Bits in accumulator (updated)
        /// 
        /// Returns:
        ///   true  - Block decoded successfully (hit end-of-block symbol 256)
        ///   false - Error (invalid data, bounds violation, etc.)
        /// 
        /// Assumptions:
        ///   - _litLenTable and _distTable are already built
        ///   - All parameters are valid (caller validated)
        /// </summary>
        private static bool InflateHuffmanBlock(byte[] input, int inputLen, byte[] output, int outputMax,
            ref int inPos, ref int outPos, ref int bitBuf, ref int bitCount) {
            
            int iterations = 0;
            
            // Decode symbols until end-of-block or output full
            while (outPos < outputMax) {
                // Safety: prevent infinite loop on malformed data
                iterations++;
                if (iterations > MAX_DECODE_ITERATIONS) return false;
                
                // --------------------------------------------------------
                // ENSURE ENOUGH BITS FOR DECODING
                // --------------------------------------------------------
                // Maximum Huffman code length is 15 bits.
                // We try to keep at least 15 bits in the buffer.
                while (bitCount < 15 && inPos < inputLen) {
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }
                
                // --------------------------------------------------------
                // DECODE LITERAL/LENGTH SYMBOL
                // --------------------------------------------------------
                int sym = DecodeSymbol(_litLenTable, 15, ref bitBuf, ref bitCount);
                if (sym < 0) return false;
                
                if (sym < 256) {
                    // ====================================================
                    // LITERAL BYTE (symbols 0-255)
                    // ====================================================
                    // Output the byte directly
                    if (outPos >= outputMax) return false;
                    output[outPos++] = (byte)sym;
                }
                else if (sym == 256) {
                    // ====================================================
                    // END OF BLOCK (symbol 256)
                    // ====================================================
                    // Block complete, return success
                    return true;
                }
                else {
                    // ====================================================
                    // LENGTH/DISTANCE PAIR (symbols 257-285)
                    // ====================================================
                    // LZ77 back-reference: copy 'length' bytes from
                    // 'distance' bytes back in the output.
                    
                    // Get actual length from length code
                    int length = InflateGetLength(sym, ref bitBuf, ref bitCount, input, inputLen, ref inPos);
                    if (length < 0) return false;
                    
                    // Ensure enough bits for distance code
                    while (bitCount < 15 && inPos < inputLen) {
                        bitBuf |= input[inPos++] << bitCount;
                        bitCount += 8;
                    }
                    
                    // Decode distance symbol
                    int distSym = DecodeSymbol(_distTable, 15, ref bitBuf, ref bitCount);
                    if (distSym < 0) return false;
                    
                    // Get actual distance from distance code
                    int distance = InflateGetDistance(distSym, ref bitBuf, ref bitCount, input, inputLen, ref inPos);
                    if (distance < 0) return false;
                    
                    // Validate distance doesn't go before start of output
                    // Assumption: distance is always > 0 and <= outPos
                    if (distance > outPos) return false;
                    if (distance <= 0) return false;
                    
                    // Validate enough output space
                    if (outPos + length > outputMax) return false;
                    
                    // Copy from back-reference
                    // Note: Must copy byte-by-byte because source and dest may overlap
                    // (e.g., distance=1 repeats the previous byte 'length' times)
                    for (int i = 0; i < length; i++) {
                        output[outPos] = output[outPos - distance];
                        outPos++;
                    }
                }
            }
            
            // Output full but didn't hit end-of-block
            // This is an error for PNG (exact size expected)
            return false;
        }
        
        /// <summary>
        /// Get actual length value from length code (257-285).
        /// 
        /// Length codes encode lengths 3-258:
        ///   Codes 257-264: lengths 3-10 (no extra bits)
        ///   Codes 265-268: lengths 11-18 (1 extra bit each)
        ///   Codes 269-272: lengths 19-34 (2 extra bits each)
        ///   Codes 273-276: lengths 35-66 (3 extra bits each)
        ///   Codes 277-280: lengths 67-130 (4 extra bits each)
        ///   Codes 281-284: lengths 131-257 (5 extra bits each)
        ///   Code 285: length 258 (no extra bits)
        /// 
        /// Returns actual length, or -1 on error.
        /// </summary>
        private static int InflateGetLength(int code, ref int bitBuf, ref int bitCount, 
                                            byte[] input, int inputLen, ref int inPos) {
            // Validate code range
            if (code < 257 || code > 285) return -1;
            
            int idx = code - 257;
            
            // Calculate base length and extra bits based on code
            // Assumption: These values are fixed by DEFLATE spec (RFC 1951)
            int baseLen;
            int extraBits;
            
            if (idx < 8) {
                // Codes 257-264: lengths 3-10, 0 extra bits
                baseLen = 3 + idx;
                extraBits = 0;
            }
            else if (idx < 12) {
                // Codes 265-268: lengths 11-18, 1 extra bit
                baseLen = 11 + ((idx - 8) * 2);
                extraBits = 1;
            }
            else if (idx < 16) {
                // Codes 269-272: lengths 19-34, 2 extra bits
                baseLen = 19 + ((idx - 12) * 4);
                extraBits = 2;
            }
            else if (idx < 20) {
                // Codes 273-276: lengths 35-66, 3 extra bits
                baseLen = 35 + ((idx - 16) * 8);
                extraBits = 3;
            }
            else if (idx < 24) {
                // Codes 277-280: lengths 67-130, 4 extra bits
                baseLen = 67 + ((idx - 20) * 16);
                extraBits = 4;
            }
            else if (idx < 28) {
                // Codes 281-284: lengths 131-257, 5 extra bits
                baseLen = 131 + ((idx - 24) * 32);
                extraBits = 5;
            }
            else {
                // Code 285: length 258, 0 extra bits
                return 258;
            }
            
            // Read extra bits if needed
            if (extraBits > 0) {
                while (bitCount < extraBits) {
                    if (inPos >= inputLen) return -1;
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }
                
                int extra = bitBuf & ((1 << extraBits) - 1);
                bitBuf >>= extraBits;
                bitCount -= extraBits;
                
                return baseLen + extra;
            }
            
            return baseLen;
        }
        
        /// <summary>
        /// Get actual distance value from distance code (0-29).
        /// 
        /// Distance codes encode distances 1-32768:
        ///   Codes 0-3: distances 1-4 (no extra bits)
        ///   Codes 4-5: distances 5-8 (1 extra bit)
        ///   Codes 6-7: distances 9-16 (2 extra bits)
        ///   ... and so on up to ...
        ///   Codes 28-29: distances 16385-32768 (13 extra bits)
        /// 
        /// Returns actual distance, or -1 on error.
        /// </summary>
        private static int InflateGetDistance(int code, ref int bitBuf, ref int bitCount,
                                              byte[] input, int inputLen, ref int inPos) {
            // Validate code range
            if (code < 0 || code > 29) return -1;
            
            // Calculate base distance and extra bits
            // Assumption: These values are fixed by DEFLATE spec (RFC 1951)
            int baseDist;
            int extraBits;
            
            if (code < 4) {
                // Codes 0-3: distances 1-4, 0 extra bits
                baseDist = 1 + code;
                extraBits = 0;
            }
            else {
                // Codes 4-29: use formula
                // extraBits = (code - 2) / 2
                // baseDist = 1 + 2^(halfCode+1) + (oddBit * 2^halfCode)
                extraBits = (code - 2) >> 1;
                int halfCode = code >> 1;
                baseDist = 1 + (1 << (halfCode + 1));
                if ((code & 1) != 0) {
                    baseDist += 1 << halfCode;
                }
            }
            
            // Read extra bits if needed
            if (extraBits > 0) {
                while (bitCount < extraBits) {
                    if (inPos >= inputLen) return -1;
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }
                
                int extra = bitBuf & ((1 << extraBits) - 1);
                bitBuf >>= extraBits;
                bitCount -= extraBits;
                
                return baseDist + extra;
            }
            
            return baseDist;
        }
        
        /// <summary>
        /// Reconstruct PNG scanlines by applying filter reversal.
        /// 
        /// This method processes decompressed PNG image data and applies
        /// the inverse of PNG filtering to reconstruct the original RGBA pixels.
        /// 
        /// Parameters:
        ///   decompressed      - Decompressed data from zlib (filter byte + scanline data per row)
        ///   decompressedLen   - Number of valid bytes in decompressed buffer
        ///   rgbaOutput        - Preallocated output buffer for RGBA pixel data
        ///   width             - Image width in pixels
        ///   height            - Image height in pixels
        ///   prevScanline      - Preallocated work buffer for previous scanline (width * 4 bytes)
        ///   currScanline      - Preallocated work buffer for current scanline (width * 4 bytes)
        /// 
        /// Returns:
        ///   true  - Successfully reconstructed all scanlines
        ///   false - Error: invalid filter type, bounds violation, or data corruption
        /// 
        /// PNG Filter Types (per PNG specification):
        ///   0 = None    - No filtering, raw bytes
        ///   1 = Sub     - Each byte is difference from byte bpp positions left
        ///   2 = Up      - Each byte is difference from byte directly above
        ///   3 = Average - Each byte is difference from average of left and above
        ///   4 = Paeth   - Each byte is difference from Paeth predictor
        /// 
        /// Decompressed data format:
        ///   For each row (y = 0 to height-1):
        ///     [0]        1 byte:  filter type (0-4)
        ///     [1..W*4]   W*4 bytes: filtered scanline data (RGBA)
        ///   Total size = height * (width * 4 + 1)
        /// 
        /// Output format:
        ///   RGBA pixels in row-major order, 4 bytes per pixel (R, G, B, A)
        ///   Total size = width * height * 4
        /// 
        /// Assumptions:
        ///   - All buffers are preallocated by caller
        ///   - Color type is 6 (RGBA), bit depth is 8
        ///   - No memory allocation occurs in this method
        ///   - prevScanline and currScanline are at least (width * 4) bytes
        /// </summary>
        public static bool ReconstructScanlines(
            byte[] decompressed,
            int decompressedLen,
            byte[] rgbaOutput,
            int width,
            int height,
            byte[] prevScanline,
            byte[] currScanline) {
            
            // ============================================================
            // INPUT VALIDATION
            // ============================================================
            
            // Validate decompressed buffer
            if (decompressed == null) return false;
            if (decompressedLen <= 0) return false;
            
            // Validate output buffer
            if (rgbaOutput == null) return false;
            
            // Validate dimensions
            if (width <= 0) return false;
            if (height <= 0) return false;
            if (width > MAX_WIDTH) return false;
            if (height > MAX_HEIGHT) return false;
            
            // Validate work buffers
            if (prevScanline == null) return false;
            if (currScanline == null) return false;
            
            // ============================================================
            // SIZE CALCULATIONS
            // ============================================================
            
            // Bytes per pixel for RGBA (color type 6, bit depth 8)
            // 4 channels (R, G, B, A) * 1 byte each = 4 bytes
            int bpp = 4;
            
            // Bytes per scanline (excluding filter byte)
            int scanlineBytes = width * bpp;
            
            // Expected decompressed size: height * (1 filter byte + scanline bytes)
            int expectedDecompressedSize = height * (1 + scanlineBytes);
            
            // Expected output size: height * width * 4 bytes
            int expectedOutputSize = height * width * bpp;
            
            // Validate buffer sizes
            if (decompressedLen < expectedDecompressedSize) return false;
            if (rgbaOutput.Length < expectedOutputSize) return false;
            if (prevScanline.Length < scanlineBytes) return false;
            if (currScanline.Length < scanlineBytes) return false;
            
            // ============================================================
            // INITIALIZE PREVIOUS SCANLINE TO ZEROS
            // ============================================================
            // First row has no previous row, so we treat it as all zeros
            // This is required for Up, Average, and Paeth filters
            
            for (int i = 0; i < scanlineBytes; i++) {
                prevScanline[i] = 0;
            }
            
            // ============================================================
            // PROCESS EACH SCANLINE
            // ============================================================
            
            // Position in decompressed data
            int readPos = 0;
            
            // Position in output RGBA buffer
            int writePos = 0;
            
            // Process each row
            for (int y = 0; y < height; y++) {
                // --------------------------------------------------------
                // READ FILTER TYPE (1 byte at start of each row)
                // --------------------------------------------------------
                
                // Bounds check
                if (readPos >= decompressedLen) return false;
                
                // Read filter type byte
                byte filterType = decompressed[readPos];
                readPos++;
                
                // Validate filter type (must be 0-4)
                if (filterType > 4) return false;
                
                // --------------------------------------------------------
                // READ FILTERED SCANLINE DATA
                // --------------------------------------------------------
                
                // Bounds check for entire scanline
                if (readPos + scanlineBytes > decompressedLen) return false;
                
                // Copy filtered data to current scanline buffer
                for (int i = 0; i < scanlineBytes; i++) {
                    currScanline[i] = decompressed[readPos + i];
                }
                readPos += scanlineBytes;
                
                // --------------------------------------------------------
                // APPLY FILTER RECONSTRUCTION (reverse the filter)
                // --------------------------------------------------------
                
                // Filter type 0: None
                // No transformation needed - data is already raw
                if (filterType == 0) {
                    // Nothing to do
                }
                // Filter type 1: Sub
                // Recon(x) = Filt(x) + Recon(a)
                // where a = byte at position (x - bpp), or 0 if x < bpp
                else if (filterType == 1) {
                    // First bpp bytes have no left neighbor, so they stay as-is
                    // For remaining bytes, add the byte bpp positions to the left
                    for (int i = bpp; i < scanlineBytes; i++) {
                        // Recon(x) = Filt(x) + Recon(a)
                        // Note: currScanline[i - bpp] is already reconstructed at this point
                        currScanline[i] = (byte)(currScanline[i] + currScanline[i - bpp]);
                    }
                }
                // Filter type 2: Up
                // Recon(x) = Filt(x) + Recon(b)
                // where b = byte at same position in previous row
                else if (filterType == 2) {
                    for (int i = 0; i < scanlineBytes; i++) {
                        // Recon(x) = Filt(x) + Recon(b)
                        currScanline[i] = (byte)(currScanline[i] + prevScanline[i]);
                    }
                }
                // Filter type 3: Average
                // Recon(x) = Filt(x) + floor((Recon(a) + Recon(b)) / 2)
                // where a = byte at (x - bpp) or 0, b = byte above
                else if (filterType == 3) {
                    for (int i = 0; i < scanlineBytes; i++) {
                        // Get left neighbor (a): 0 if i < bpp, else already-reconstructed byte
                        int a = (i >= bpp) ? currScanline[i - bpp] : 0;
                        
                        // Get above neighbor (b): from previous scanline
                        int b = prevScanline[i];
                        
                        // Recon(x) = Filt(x) + floor((a + b) / 2)
                        currScanline[i] = (byte)(currScanline[i] + ((a + b) >> 1));
                    }
                }
                // Filter type 4: Paeth
                // Recon(x) = Filt(x) + PaethPredictor(Recon(a), Recon(b), Recon(c))
                // where a = left, b = above, c = upper-left
                else if (filterType == 4) {
                    for (int i = 0; i < scanlineBytes; i++) {
                        // Get left neighbor (a): 0 if i < bpp
                        int a = (i >= bpp) ? currScanline[i - bpp] : 0;
                        
                        // Get above neighbor (b): from previous scanline
                        int b = prevScanline[i];
                        
                        // Get upper-left neighbor (c): 0 if i < bpp
                        int c = (i >= bpp) ? prevScanline[i - bpp] : 0;
                        
                        // ------------------------------------------------
                        // PAETH PREDICTOR CALCULATION
                        // ------------------------------------------------
                        // p = a + b - c
                        // pa = |p - a|
                        // pb = |p - b|
                        // pc = |p - c|
                        // if pa <= pb && pa <= pc: return a
                        // else if pb <= pc: return b
                        // else: return c
                        
                        int p = a + b - c;
                        
                        // Calculate absolute differences
                        int pa = p - a;
                        if (pa < 0) pa = -pa;
                        
                        int pb = p - b;
                        if (pb < 0) pb = -pb;
                        
                        int pc = p - c;
                        if (pc < 0) pc = -pc;
                        
                        // Select predictor based on smallest distance
                        int predictor;
                        if (pa <= pb && pa <= pc) {
                            predictor = a;
                        } else if (pb <= pc) {
                            predictor = b;
                        } else {
                            predictor = c;
                        }
                        
                        // Recon(x) = Filt(x) + predictor
                        currScanline[i] = (byte)(currScanline[i] + predictor);
                    }
                }
                
                // --------------------------------------------------------
                // COPY RECONSTRUCTED RGBA DATA TO OUTPUT
                // --------------------------------------------------------
                
                // Bounds check for output
                if (writePos + scanlineBytes > expectedOutputSize) return false;
                
                // Copy RGBA bytes to output (already in correct order for RGBA)
                for (int i = 0; i < scanlineBytes; i++) {
                    rgbaOutput[writePos + i] = currScanline[i];
                }
                writePos += scanlineBytes;
                
                // --------------------------------------------------------
                // SWAP SCANLINE BUFFERS
                // --------------------------------------------------------
                // Current scanline becomes previous for next iteration
                // We do this by copying data (no pointer swap to avoid allocations)
                
                for (int i = 0; i < scanlineBytes; i++) {
                    prevScanline[i] = currScanline[i];
                }
            }
            
            // ============================================================
            // SUCCESS
            // ============================================================
            return true;
        }
        
        /// <summary>
        /// Convert RGBA buffer to ARGB int array for Image.RawData.
        /// 
        /// PNG stores pixels as RGBA (R, G, B, A in that byte order).
        /// Our Image class expects ARGB packed as 0xAARRGGBB int values.
        /// 
        /// Parameters:
        ///   rgbaInput   - Input RGBA byte buffer (4 bytes per pixel)
        ///   argbOutput  - Output ARGB int array (Image.RawData format)
        ///   pixelCount  - Number of pixels (width * height)
        /// 
        /// Returns:
        ///   true  - Successfully converted all pixels
        ///   false - Error: null buffers or size mismatch
        /// 
        /// Assumptions:
        ///   - rgbaInput has at least pixelCount * 4 bytes
        ///   - argbOutput has at least pixelCount elements
        ///   - No memory allocation
        /// </summary>
        public static bool ConvertRgbaToArgb(byte[] rgbaInput, int[] argbOutput, int pixelCount) {
            // Validate inputs
            if (rgbaInput == null) return false;
            if (argbOutput == null) return false;
            if (pixelCount <= 0) return false;
            
            // Validate buffer sizes
            int rgbaSize = pixelCount * 4;
            if (rgbaInput.Length < rgbaSize) return false;
            if (argbOutput.Length < pixelCount) return false;
            
            // Convert each pixel
            int rgbaPos = 0;
            for (int i = 0; i < pixelCount; i++) {
                // Read RGBA bytes
                byte r = rgbaInput[rgbaPos];
                byte g = rgbaInput[rgbaPos + 1];
                byte b = rgbaInput[rgbaPos + 2];
                byte a = rgbaInput[rgbaPos + 3];
                rgbaPos += 4;
                
                // Pack as ARGB: 0xAARRGGBB
                argbOutput[i] = (int)(((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b);
            }
            
            return true;
        }
        
        /// <summary>
        /// Convert RGBA pointer data to ARGB int array for Image.RawData.
        /// 
        /// This overload accepts a raw pointer to RGBA data, useful for
        /// preloaded assets stored in kernel-owned memory.
        /// 
        /// Parameters:
        ///   rgbaInput   - Pointer to input RGBA data (4 bytes per pixel)
        ///   pixelCount  - Number of pixels (width * height)
        ///   argbOutput  - Output ARGB int array (Image.RawData format)
        /// 
        /// Returns:
        ///   true  - Successfully converted all pixels
        ///   false - Error: null pointers or invalid count
        /// 
        /// Assumptions:
        ///   - rgbaInput points to at least pixelCount * 4 bytes
        ///   - argbOutput has at least pixelCount elements
        ///   - No memory allocation
        /// </summary>
        public static bool ConvertRgbaToArgb(byte* rgbaInput, int pixelCount, int[] argbOutput) {
            // Validate inputs
            if (rgbaInput == null) return false;
            if (argbOutput == null) return false;
            if (pixelCount <= 0) return false;
            if (argbOutput.Length < pixelCount) return false;
            
            // Convert each pixel
            int rgbaPos = 0;
            for (int i = 0; i < pixelCount; i++) {
                // Read RGBA bytes from pointer
                byte r = rgbaInput[rgbaPos];
                byte g = rgbaInput[rgbaPos + 1];
                byte b = rgbaInput[rgbaPos + 2];
                byte a = rgbaInput[rgbaPos + 3];
                rgbaPos += 4;
                
                // Pack as ARGB: 0xAARRGGBB
                argbOutput[i] = (int)(((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b);
            }
            
            return true;
        }
        
        /// <summary>
        /// Blit RGBA pixel data to a UEFI GOP framebuffer.
        /// 
        /// This method writes decoded PNG pixels directly to a GOP framebuffer,
        /// handling the conversion from PNG's RGBA format to the framebuffer's
        /// native pixel format (typically BGRA for UEFI GOP).
        /// 
        /// Parameters:
        ///   rgbaInput         - Input RGBA byte buffer from PNG (4 bytes per pixel: R, G, B, A)
        ///   framebuffer       - Pointer to GOP framebuffer base address
        ///   imageWidth        - Width of the source image in pixels
        ///   imageHeight       - Height of the source image in pixels
        ///   destX             - X coordinate in framebuffer to start drawing
        ///   destY             - Y coordinate in framebuffer to start drawing
        ///   fbWidth           - Framebuffer visible width in pixels
        ///   fbHeight          - Framebuffer visible height in pixels
        ///   pixelsPerScanLine - Framebuffer stride (may be > fbWidth for alignment)
        ///   useBgra           - true = BGRA framebuffer (UEFI GOP), false = RGBA framebuffer
        /// 
        /// Returns:
        ///   true  - Successfully blitted image to framebuffer
        ///   false - Error: null pointers, invalid dimensions, or out of bounds
        /// 
        /// GOP Framebuffer Format:
        ///   UEFI GOP typically uses BGRA format (PixelBlueGreenRedReserved8BitPerColor):
        ///     Byte 0: Blue
        ///     Byte 1: Green
        ///     Byte 2: Red
        ///     Byte 3: Reserved (we write Alpha here)
        ///   Packed as 32-bit: 0xAARRGGBB when read as little-endian uint32
        /// 
        /// Stride Handling:
        ///   GOP framebuffers often have stride (PixelsPerScanLine) > visible width
        ///   for memory alignment. We MUST use pixelsPerScanLine for row offsets,
        ///   NOT fbWidth. Failing to do this causes skewed/corrupted display.
        /// 
        /// Clipping:
        ///   If destX/destY plus image dimensions exceed framebuffer bounds,
        ///   we clip the image to fit within the visible framebuffer area.
        /// 
        /// Assumptions:
        ///   - rgbaInput has at least (imageWidth * imageHeight * 4) bytes
        ///   - framebuffer is valid and writable
        ///   - pixelsPerScanLine >= fbWidth
        ///   - No memory allocation occurs in this method
        /// </summary>
        public static bool BlitToFramebuffer(
            byte[] rgbaInput,
            uint* framebuffer,
            int imageWidth,
            int imageHeight,
            int destX,
            int destY,
            int fbWidth,
            int fbHeight,
            int pixelsPerScanLine,
            bool useBgra) {
            
            // ============================================================
            // INPUT VALIDATION
            // ============================================================
            
            // Validate input buffer
            if (rgbaInput == null) return false;
            
            // Validate framebuffer pointer
            if (framebuffer == null) return false;
            
            // Validate image dimensions
            if (imageWidth <= 0) return false;
            if (imageHeight <= 0) return false;
            
            // Validate framebuffer dimensions
            if (fbWidth <= 0) return false;
            if (fbHeight <= 0) return false;
            
            // Validate stride (must be at least as wide as visible width)
            if (pixelsPerScanLine < fbWidth) return false;
            
            // Validate input buffer size
            int requiredInputSize = imageWidth * imageHeight * 4;
            if (rgbaInput.Length < requiredInputSize) return false;
            
            // ============================================================
            // CLIPPING CALCULATIONS
            // ============================================================
            // Determine the actual region to blit, clipping to framebuffer bounds
            
            // Source start coordinates (in image)
            int srcStartX = 0;
            int srcStartY = 0;
            
            // Destination start coordinates (in framebuffer)
            int dstStartX = destX;
            int dstStartY = destY;
            
            // Clip left edge
            if (destX < 0) {
                srcStartX = -destX;
                dstStartX = 0;
            }
            
            // Clip top edge
            if (destY < 0) {
                srcStartY = -destY;
                dstStartY = 0;
            }
            
            // Calculate visible width after left clipping
            int visibleWidth = imageWidth - srcStartX;
            
            // Clip right edge
            if (dstStartX + visibleWidth > fbWidth) {
                visibleWidth = fbWidth - dstStartX;
            }
            
            // Calculate visible height after top clipping
            int visibleHeight = imageHeight - srcStartY;
            
            // Clip bottom edge
            if (dstStartY + visibleHeight > fbHeight) {
                visibleHeight = fbHeight - dstStartY;
            }
            
            // If nothing visible after clipping, return success (nothing to draw)
            if (visibleWidth <= 0) return true;
            if (visibleHeight <= 0) return true;
            
            // ============================================================
            // BLIT LOOP
            // ============================================================
            // Copy pixels row by row, converting RGBA to framebuffer format
            
            // Process each visible row
            for (int y = 0; y < visibleHeight; y++) {
                // Calculate source row start in RGBA input
                // Source row = srcStartY + y
                // Source offset = (srcStartY + y) * imageWidth * 4 + srcStartX * 4
                int srcRowOffset = ((srcStartY + y) * imageWidth + srcStartX) * 4;
                
                // Calculate destination row start in framebuffer
                // Destination row = dstStartY + y
                // CRITICAL: Use pixelsPerScanLine, NOT fbWidth, for stride!
                // Destination offset = (dstStartY + y) * pixelsPerScanLine + dstStartX
                int dstRowOffset = (dstStartY + y) * pixelsPerScanLine + dstStartX;
                
                // Process each visible pixel in this row
                for (int x = 0; x < visibleWidth; x++) {
                    // Read RGBA from input (PNG format)
                    int srcIdx = srcRowOffset + x * 4;
                    byte r = rgbaInput[srcIdx];
                    byte g = rgbaInput[srcIdx + 1];
                    byte b = rgbaInput[srcIdx + 2];
                    byte a = rgbaInput[srcIdx + 3];
                    
                    // Convert to framebuffer format
                    uint pixel;
                    
                    if (useBgra) {
                        // BGRA format (UEFI GOP PixelBlueGreenRedReserved8BitPerColor)
                        // Memory layout: B, G, R, A (byte order)
                        // As uint32 little-endian: 0xAARRGGBB
                        pixel = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                    } else {
                        // RGBA format (some framebuffers)
                        // Memory layout: R, G, B, A (byte order)
                        // As uint32 little-endian: 0xAABBGGRR
                        pixel = ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
                    }
                    
                    // Write pixel to framebuffer
                    int dstIdx = dstRowOffset + x;
                    framebuffer[dstIdx] = pixel;
                }
            }
            
            // ============================================================
            // SUCCESS
            // ============================================================
            return true;
        }
        
        /// <summary>
        /// Blit RGBA pixel data to a UEFI GOP framebuffer with alpha blending.
        /// 
        /// This is similar to BlitToFramebuffer but performs alpha blending
        /// with the existing framebuffer contents for semi-transparent pixels.
        /// 
        /// Parameters:
        ///   rgbaInput         - Input RGBA byte buffer from PNG (4 bytes per pixel)
        ///   framebuffer       - Pointer to GOP framebuffer base address
        ///   imageWidth        - Width of the source image in pixels
        ///   imageHeight       - Height of the source image in pixels
        ///   destX             - X coordinate in framebuffer to start drawing
        ///   destY             - Y coordinate in framebuffer to start drawing
        ///   fbWidth           - Framebuffer visible width in pixels
        ///   fbHeight          - Framebuffer visible height in pixels
        ///   pixelsPerScanLine - Framebuffer stride (may be > fbWidth)
        ///   useBgra           - true = BGRA framebuffer, false = RGBA framebuffer
        /// 
        /// Returns:
        ///   true  - Successfully blitted image with alpha blending
        ///   false - Error: null pointers, invalid dimensions, or out of bounds
        /// 
        /// Alpha Blending Formula:
        ///   out = src * alpha + dst * (255 - alpha)
        ///   where alpha is the source pixel's alpha channel (0-255)
        /// 
        /// Performance Note:
        ///   Alpha blending requires reading from framebuffer, which may be slow
        ///   on some systems. Use BlitToFramebuffer for fully opaque images.
        /// 
        /// Assumptions:
        ///   - Same as BlitToFramebuffer
        ///   - Framebuffer read is safe (not write-only memory)
        /// </summary>
        public static bool BlitToFramebufferAlpha(
            byte[] rgbaInput,
            uint* framebuffer,
            int imageWidth,
            int imageHeight,
            int destX,
            int destY,
            int fbWidth,
            int fbHeight,
            int pixelsPerScanLine,
            bool useBgra) {
            
            // ============================================================
            // INPUT VALIDATION (same as BlitToFramebuffer)
            // ============================================================
            
            if (rgbaInput == null) return false;
            if (framebuffer == null) return false;
            if (imageWidth <= 0) return false;
            if (imageHeight <= 0) return false;
            if (fbWidth <= 0) return false;
            if (fbHeight <= 0) return false;
            if (pixelsPerScanLine < fbWidth) return false;
            
            int requiredInputSize = imageWidth * imageHeight * 4;
            if (rgbaInput.Length < requiredInputSize) return false;
            
            // ============================================================
            // CLIPPING CALCULATIONS (same as BlitToFramebuffer)
            // ============================================================
            
            int srcStartX = 0;
            int srcStartY = 0;
            int dstStartX = destX;
            int dstStartY = destY;
            
            if (destX < 0) {
                srcStartX = -destX;
                dstStartX = 0;
            }
            
            if (destY < 0) {
                srcStartY = -destY;
                dstStartY = 0;
            }
            
            int visibleWidth = imageWidth - srcStartX;
            if (dstStartX + visibleWidth > fbWidth) {
                visibleWidth = fbWidth - dstStartX;
            }
            
            int visibleHeight = imageHeight - srcStartY;
            if (dstStartY + visibleHeight > fbHeight) {
                visibleHeight = fbHeight - dstStartY;
            }
            
            if (visibleWidth <= 0) return true;
            if (visibleHeight <= 0) return true;
            
            // ============================================================
            // ALPHA BLENDING BLIT LOOP
            // ============================================================
            
            for (int y = 0; y < visibleHeight; y++) {
                int srcRowOffset = ((srcStartY + y) * imageWidth + srcStartX) * 4;
                int dstRowOffset = (dstStartY + y) * pixelsPerScanLine + dstStartX;
                
                for (int x = 0; x < visibleWidth; x++) {
                    // Read source RGBA
                    int srcIdx = srcRowOffset + x * 4;
                    byte srcR = rgbaInput[srcIdx];
                    byte srcG = rgbaInput[srcIdx + 1];
                    byte srcB = rgbaInput[srcIdx + 2];
                    byte srcA = rgbaInput[srcIdx + 3];
                    
                    // Fast path: fully opaque pixel (no blending needed)
                    if (srcA == 255) {
                        uint pixel;
                        if (useBgra) {
                            pixel = ((uint)srcA << 24) | ((uint)srcR << 16) | ((uint)srcG << 8) | srcB;
                        } else {
                            pixel = ((uint)srcA << 24) | ((uint)srcB << 16) | ((uint)srcG << 8) | srcR;
                        }
                        framebuffer[dstRowOffset + x] = pixel;
                        continue;
                    }
                    
                    // Fast path: fully transparent pixel (skip entirely)
                    if (srcA == 0) {
                        continue;
                    }
                    
                    // Read destination pixel from framebuffer
                    int dstIdx = dstRowOffset + x;
                    uint dstPixel = framebuffer[dstIdx];
                    
                    // Extract destination RGB based on format
                    byte dstR, dstG, dstB;
                    if (useBgra) {
                        // BGRA format: 0xAARRGGBB
                        dstR = (byte)((dstPixel >> 16) & 0xFF);
                        dstG = (byte)((dstPixel >> 8) & 0xFF);
                        dstB = (byte)(dstPixel & 0xFF);
                    } else {
                        // RGBA format: 0xAABBGGRR
                        dstR = (byte)(dstPixel & 0xFF);
                        dstG = (byte)((dstPixel >> 8) & 0xFF);
                        dstB = (byte)((dstPixel >> 16) & 0xFF);
                    }
                    
                    // Alpha blend: out = src * alpha + dst * (255 - alpha)
                    // Using integer math: out = (src * alpha + dst * (255 - alpha)) / 255
                    // Optimized: out = dst + ((src - dst) * alpha) / 255
                    int alpha = srcA;
                    int invAlpha = 255 - alpha;
                    
                    int outR = (srcR * alpha + dstR * invAlpha) / 255;
                    int outG = (srcG * alpha + dstG * invAlpha) / 255;
                    int outB = (srcB * alpha + dstB * invAlpha) / 255;
                    
                    // Pack output pixel
                    uint outPixel;
                    if (useBgra) {
                        outPixel = (0xFF000000) | ((uint)outR << 16) | ((uint)outG << 8) | (uint)outB;
                    } else {
                        outPixel = (0xFF000000) | ((uint)outB << 16) | ((uint)outG << 8) | (uint)outR;
                    }
                    
                    framebuffer[dstIdx] = outPixel;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Initialize preallocated buffers.
        /// Must be called once before any Load() calls.
        /// Returns false if allocation fails.
        /// </summary>
        public static bool Initialize() {
            // Avoid double initialization
            if (_initialized) return true;
            
            // Allocate all work buffers upfront
            // Using 1 << 15 = 32768 for decode tables (covers 15-bit Huffman codes)
            _litLenTable = new ushort[1 << 15];
            if (_litLenTable == null) return false;
            
            _distTable = new ushort[1 << 15];
            if (_distTable == null) return false;
            
            _codeLenTable = new ushort[1 << 7];
            if (_codeLenTable == null) return false;
            
            _litLenLengths = new int[LITLEN_TABLE_SIZE];
            if (_litLenLengths == null) return false;
            
            _distLengths = new int[DIST_TABLE_SIZE];
            if (_distLengths == null) return false;
            
            _codeLenLengths = new int[CODELENGTH_TABLE_SIZE];
            if (_codeLenLengths == null) return false;
            
            // Max combined lengths: 286 (litlen) + 32 (dist) = 318
            _allLengths = new int[320];
            if (_allLengths == null) return false;
            
            _blCount = new int[16];
            if (_blCount == null) return false;
            
            _nextCode = new int[16];
            if (_nextCode == null) return false;
            
            _initialized = true;
            return true;
        }
        
        /// <summary>
        /// Load a PNG image into an Image object.
        /// </summary>
        public static bool Load(byte[] data, out Image result) {
            return Load(data, out result, LoadProbeMode.None);
        }

        public static bool Load(byte[] data, out Image result, LoadProbeMode probeMode) {
            result = null;
            bool entryBaselineProbe = probeMode == LoadProbeMode.AfterIhdr;
            ProbeBreadcrumb(entryBaselineProbe ? "PNGLOADER_ENTRY_BASELINE_ENTER" : "PNGLOADER_ENTER");
            try {
                if (entryBaselineProbe) {
                    return LoadEntryBaselineProbe(data, out result);
                }

                return LoadCore(data, out result, probeMode);
            } finally {
                ProbeBreadcrumb(entryBaselineProbe ? "PNGLOADER_ENTRY_BASELINE_EXIT" : "PNGLOADER_EXIT");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool LoadEntryBaselineProbe(byte[] data, out Image result) {
            result = null;

            if (!_initialized) return false;
            if (data == null) return false;

            ProbeBreadcrumb("PNGLOADER_ENTRY_BASELINE_BYTES_OK");
            ProbeValue("PNGLOADER_ENTRY_BASELINE_LEN", (ulong)data.Length);

            if (data.Length < 33) return false;

            if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47 ||
                data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A) {
                return false;
            }

            ProbeBreadcrumb("PNGLOADER_ENTRY_BASELINE_HEADER_OK");

            int width = (int)ReadBE32(data, 16);
            int height = (int)ReadBE32(data, 20);
            if (width <= 0 || width > MAX_WIDTH) return false;
            if (height <= 0 || height > MAX_HEIGHT) return false;

            ProbeBreadcrumb("PNGLOADER_ENTRY_BASELINE_IHDR_OK");
            ProbeValue("PNGLOADER_ENTRY_BASELINE_IHDR_WIDTH", (ulong)(uint)width);
            ProbeValue("PNGLOADER_ENTRY_BASELINE_IHDR_HEIGHT", (ulong)(uint)height);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool LoadTinyProbe(byte[] data, out Image result, LoadProbeMode probeMode) {
            result = null;

            if (!_initialized) return false;

            if (data == null) {
                ProbeBreadcrumb("PNGLOADER_BYTES_NULL");
                return false;
            }

            ProbeBreadcrumb("PNGLOADER_BYTES_OK");
            ProbeValue("PNGLOADER_BYTES_LENGTH", (ulong)data.Length);

            ProbeBreadcrumb("PNGLOADER_HEADER_ENTER");
            if (data.Length < 33) {
                ProbeBreadcrumb("PNGLOADER_HEADER_BAD");
                return false;
            }

            if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47 ||
                data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A) {
                ProbeBreadcrumb("PNGLOADER_HEADER_BAD");
                return false;
            }

            ProbeBreadcrumb("PNGLOADER_HEADER_OK");

            int pos = 8;
            int width = 0;
            int height = 0;
            int idatTotalSize = 0;
            int idatChunkCount = 0;

            ProbeBreadcrumb("PNGLOADER_IHDR_ENTER");
            if (pos + 12 > data.Length) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }

            uint chunkLen = ReadBE32(data, pos);
            if (chunkLen != 13) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }

            uint chunkType = ReadBE32(data, pos + 4);
            if (chunkType != 0x49484452) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }

            width = (int)ReadBE32(data, pos + 8);
            height = (int)ReadBE32(data, pos + 12);
            byte bitDepth = data[pos + 16];
            byte colorType = data[pos + 17];
            byte compressionMethod = data[pos + 18];
            byte filterMethod = data[pos + 19];
            byte interlaceMethod = data[pos + 20];

            if (width <= 0 || width > MAX_WIDTH) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (height <= 0 || height > MAX_HEIGHT) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (bitDepth != 8) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (colorType != 6) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (compressionMethod != 0) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (filterMethod != 0) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (interlaceMethod != 0) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }

            ProbeBreadcrumb("PNGLOADER_IHDR_OK");
            ProbeBreadcrumb("PNGLOADER_IHDR_DIMS");
            ProbeValue("PNGLOADER_IHDR_WIDTH", (ulong)(uint)width);
            ProbeValue("PNGLOADER_IHDR_HEIGHT", (ulong)(uint)height);
            ProbeValue("PNGLOADER_IHDR_BIT_DEPTH", (ulong)bitDepth);
            ProbeValue("PNGLOADER_IHDR_COLOR_TYPE", (ulong)colorType);

            const int tinyKnownIdatChunkOffset = 491;
            const int tinyKnownIdatPayloadSize = 555;
            const int tinyKnownIdatPayloadOffset = tinyKnownIdatChunkOffset + 8;
            const uint tinyKnownIdatType = 0x49444154;

            ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_ENTER");
            if (data.Length != 1070 ||
                ReadBE32(data, tinyKnownIdatChunkOffset) != (uint)tinyKnownIdatPayloadSize ||
                ReadBE32(data, tinyKnownIdatChunkOffset + 4) != tinyKnownIdatType) {
                ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                return false;
            }

            idatChunkCount = 1;
            idatTotalSize = tinyKnownIdatPayloadSize;
            ProbeBreadcrumb("PNGLOADER_CHUNK_TYPE_IDAT");
            ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");

            ProbeValue("PNGLOADER_IDAT_CHUNK_COUNT", (ulong)(uint)idatChunkCount);
            ProbeValue("PNGLOADER_IDAT_COMPRESSED_BYTES", (ulong)(uint)idatTotalSize);
            ProbeBreadcrumb("PNGLOADER_IDAT_BYTES_ENTER");

            byte[] compressedData = new byte[idatTotalSize];
            if (compressedData == null) {
                ProbeBreadcrumb("PNGLOADER_IDAT_BYTES_EMPTY");
                return false;
            }

            int compressedPos = 0;
            while (compressedPos < idatTotalSize) {
                compressedData[compressedPos] = data[tinyKnownIdatPayloadOffset + compressedPos];
                compressedPos++;
            }

            if (compressedPos != idatTotalSize || compressedPos <= 0) {
                compressedData.Dispose();
                ProbeBreadcrumb("PNGLOADER_IDAT_BYTES_EMPTY");
                return false;
            }
            ProbeBreadcrumb("PNGLOADER_IDAT_BYTES_OK");

            int bytesPerPixel = 4;
            int scanlineBytes = width * bytesPerPixel;
            int expectedSize = height * (scanlineBytes + 1);

            ProbeBreadcrumb("PNGLOADER_DECOMPRESS_PREMETA_ENTER");
            ProbeValue("PNGLOADER_DECOMPRESS_PREMETA_LEN", (ulong)(uint)compressedPos);
            ProbeValue("PNGLOADER_DECOMPRESS_PREMETA_EXPECTED_OUT_LEN", (ulong)(uint)expectedSize);
            ProbeBreadcrumb("PNGLOADER_DECOMPRESS_PREMETA_EXIT");

            CursorPngTinyProbeContext tinyContext = new CursorPngTinyProbeContext {
                Data = data,
                CompressedData = compressedData,
                CompressedPos = compressedPos,
                Width = width,
                Height = height,
                BitDepth = bitDepth,
                ColorType = colorType,
                IdatChunkCount = idatChunkCount,
                IdatTotalSize = idatTotalSize,
                ExpectedSize = expectedSize
            };

            ProbeBreadcrumb("PNGLOADER_DECOMPRESS_ENTER");
            bool tinySmokeOk = false;
            int tinySmokeLen = 0;
            if (probeMode == LoadProbeMode.DecompressTinyBoundary) {
                tinySmokeOk = PngDecompressTinyBoundaryProbe(ref tinyContext);
            } else if (probeMode == LoadProbeMode.DecompressTinyBitReader) {
                tinySmokeOk = PngDecompressTinyBitReaderProbe(ref tinyContext);
            } else if (probeMode == LoadProbeMode.DecompressTinyFirstSymbol) {
                tinySmokeOk = PngDecompressTinyFirstSymbolProbe(ref tinyContext);
            } else if (probeMode == LoadProbeMode.DecompressTinyOneOp) {
                tinySmokeOk = PngDecompressTinyOneOpProbe(ref tinyContext);
            } else if (probeMode == LoadProbeMode.DecompressTinyInflateSmoke) {
                tinySmokeOk = PngDecompressTinyInflateSmokeProbe(ref tinyContext, out tinySmokeLen);
            }
            compressedData.Dispose();
            if (tinySmokeOk) {
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_OK");
                ProbeValue("PNGLOADER_DECOMPRESSED_BYTES", (ulong)(uint)tinySmokeLen);
            } else {
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
            }
            ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
            return false;
        }

        private static bool LoadCore(byte[] data, out Image result, LoadProbeMode probeMode) {
            result = null;

            if (!_initialized) return false;

            if (data == null) {
                ProbeBreadcrumb("PNGLOADER_BYTES_NULL");
                return false;
            }

            ProbeBreadcrumb("PNGLOADER_BYTES_OK");
            ProbeValue("PNGLOADER_BYTES_LENGTH", (ulong)data.Length);

            ProbeBreadcrumb("PNGLOADER_HEADER_ENTER");
            if (data.Length < 33) {
                ProbeBreadcrumb("PNGLOADER_HEADER_BAD");
                return false;
            }
            if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47 ||
                data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A) {
                ProbeBreadcrumb("PNGLOADER_HEADER_BAD");
                return false;
            }
            ProbeBreadcrumb("PNGLOADER_HEADER_OK");

            int pos = 8;
            int width = 0;
            int height = 0;
            int idatTotalSize = 0;
            int idatChunkCount = 0;
            bool foundIHDR = false;
            bool foundIEND = false;

            ProbeBreadcrumb("PNGLOADER_IHDR_ENTER");
            if (pos + 12 > data.Length) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }

            uint chunkLen = ReadBE32(data, pos);
            if (chunkLen != 13) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }

            uint chunkType = ReadBE32(data, pos + 4);
            if (chunkType != 0x49484452) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }

            width = (int)ReadBE32(data, pos + 8);
            height = (int)ReadBE32(data, pos + 12);
            byte bitDepth = data[pos + 16];
            byte colorType = data[pos + 17];
            byte compressionMethod = data[pos + 18];
            byte filterMethod = data[pos + 19];
            byte interlaceMethod = data[pos + 20];

            if (width <= 0 || width > MAX_WIDTH) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (height <= 0 || height > MAX_HEIGHT) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (bitDepth != 8) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (colorType != 6) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (compressionMethod != 0) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (filterMethod != 0) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }
            if (interlaceMethod != 0) {
                ProbeBreadcrumb("PNGLOADER_IHDR_BAD");
                return false;
            }

            ProbeBreadcrumb("PNGLOADER_IHDR_OK");
            ProbeBreadcrumb("PNGLOADER_IHDR_DIMS");
            ProbeValue("PNGLOADER_IHDR_WIDTH", (ulong)(uint)width);
            ProbeValue("PNGLOADER_IHDR_HEIGHT", (ulong)(uint)height);
            ProbeValue("PNGLOADER_IHDR_BIT_DEPTH", (ulong)bitDepth);
            ProbeValue("PNGLOADER_IHDR_COLOR_TYPE", (ulong)colorType);

            ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_ENTER");
            pos = 8;
            while (pos + 12 <= data.Length) {
                uint scanChunkLen = ReadBE32(data, pos);
                if (scanChunkLen > 0x7FFFFFFF) {
                    ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                    return false;
                }

                int totalChunkSize = 12 + (int)scanChunkLen;
                if (totalChunkSize < 12 || pos + totalChunkSize > data.Length) {
                    ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                    return false;
                }

                uint scanChunkType = ReadBE32(data, pos + 4);
                if (scanChunkType == 0x49484452) {
                    ProbeBreadcrumb("PNGLOADER_CHUNK_TYPE_IHDR");
                    if (foundIHDR) {
                        ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                        return false;
                    }
                    if (idatChunkCount > 0) {
                        ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                        return false;
                    }
                    if (scanChunkLen != 13) {
                        ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                        return false;
                    }
                    foundIHDR = true;
                    pos += totalChunkSize;
                    continue;
                }

                if (scanChunkType == 0x49444154) {
                    ProbeBreadcrumb("PNGLOADER_CHUNK_TYPE_IDAT");
                    if (!foundIHDR) {
                        ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                        return false;
                    }
                    idatChunkCount++;
                    if (idatTotalSize + (int)scanChunkLen < idatTotalSize) {
                        ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                        return false;
                    }
                    idatTotalSize += (int)scanChunkLen;
                    if (idatTotalSize > MAX_COMPRESSED_SIZE) {
                        ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                        return false;
                    }
                    pos += totalChunkSize;
                    continue;
                }

                if (scanChunkType == 0x49454E44) {
                    ProbeBreadcrumb("PNGLOADER_CHUNK_TYPE_IEND");
                    if (!foundIHDR || idatChunkCount == 0) {
                        ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                        return false;
                    }
                    if (scanChunkLen != 0) {
                        ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");
                        return false;
                    }
                    foundIEND = true;
                    break;
                }

                pos += totalChunkSize;
            }
            ProbeBreadcrumb("PNGLOADER_CHUNK_SCAN_EXIT");

            if (!foundIHDR) return false;
            if (!foundIEND) return false;
            if (idatTotalSize <= 0) {
                ProbeBreadcrumb("PNGLOADER_IDAT_BYTES_EMPTY");
                return false;
            }

            ProbeValue("PNGLOADER_IDAT_CHUNK_COUNT", (ulong)(uint)idatChunkCount);
            ProbeValue("PNGLOADER_IDAT_COMPRESSED_BYTES", (ulong)(uint)idatTotalSize);

            if (probeMode == LoadProbeMode.AfterChunkScan) {
                return false;
            }

            ProbeBreadcrumb("PNGLOADER_IDAT_BYTES_ENTER");
            byte[] compressedData = new byte[idatTotalSize];
            if (compressedData == null) {
                ProbeBreadcrumb("PNGLOADER_IDAT_BYTES_EMPTY");
                return false;
            }

            pos = 8;
            int compressedPos = 0;
            while (pos + 12 <= data.Length && compressedPos < idatTotalSize) {
                uint copyChunkLen = ReadBE32(data, pos);
                uint copyChunkType = ReadBE32(data, pos + 4);

                if (copyChunkType == 0x49444154) {
                    int copyLen = (int)copyChunkLen;
                    if (compressedPos + copyLen > idatTotalSize) {
                        copyLen = idatTotalSize - compressedPos;
                    }

                    for (int i = 0; i < copyLen; i++) {
                        compressedData[compressedPos++] = data[pos + 8 + i];
                    }
                } else if (copyChunkType == 0x49454E44) {
                    break;
                }

                pos += 12 + (int)copyChunkLen;
            }

            if (compressedPos != idatTotalSize || compressedPos <= 0) {
                compressedData.Dispose();
                ProbeBreadcrumb("PNGLOADER_IDAT_BYTES_EMPTY");
                return false;
            }
            ProbeBreadcrumb("PNGLOADER_IDAT_BYTES_OK");

            if (probeMode == LoadProbeMode.AfterIdatAggregation) {
                compressedData.Dispose();
                return false;
            }

            int bytesPerPixel = 4;
            int scanlineBytes = width * bytesPerPixel;
            int expectedSize = height * (scanlineBytes + 1);

            if (probeMode == LoadProbeMode.DecompressPreMeta) {
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_PREMETA_ENTER");
                ProbeValue("PNGLOADER_DECOMPRESS_PREMETA_LEN", (ulong)(uint)compressedPos);
                ProbeValue("PNGLOADER_DECOMPRESS_PREMETA_EXPECTED_OUT_LEN", (ulong)(uint)expectedSize);
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_PREMETA_EXIT");
                compressedData.Dispose();
                return false;
            }

            if (probeMode == LoadProbeMode.DecompressNoop) {
                compressedData.Dispose();
                PngDecompressNoopProbe();
                return false;
            }

            if (probeMode == LoadProbeMode.DecompressBytesNoop) {
                PngDecompressBytesNoopProbe(compressedData);
                compressedData.Dispose();
                return false;
            }

            if (probeMode == LoadProbeMode.DecompressZlibHeader) {
                PngDecompressZlibHeaderProbe(compressedData, compressedPos);
                compressedData.Dispose();
                return false;
            }

            if (probeMode == LoadProbeMode.DecompressDeflateHeader) {
                PngDecompressDeflateHeaderProbe(compressedData, compressedPos);
                compressedData.Dispose();
                return false;
            }

            if (probeMode == LoadProbeMode.DecompressHuffmanSetup) {
                PngDecompressHuffmanSetupProbe(compressedData, compressedPos);
                compressedData.Dispose();
                return false;
            }

            if (probeMode == LoadProbeMode.DecompressGateInlineReturn) {
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_ENTER");
                return CursorPngProbeGateOnlyInline(
                    data,
                    width,
                    height,
                    bitDepth,
                    colorType,
                    idatChunkCount,
                    idatTotalSize,
                    expectedSize);
            }

            if (probeMode == LoadProbeMode.DecompressGateHelperReturn) {
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_ENTER");
                _ = CursorPngProbeGateHelperReturning(
                    data,
                    width,
                    height,
                    bitDepth,
                    colorType,
                    idatChunkCount,
                    idatTotalSize,
                    expectedSize);
                return false;
            }

            if (probeMode == LoadProbeMode.DecompressGateBoolOnly) {
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_ENTER");
                return CursorPngProbeGateBoolCallerAfter(
                    data,
                    width,
                    height,
                    bitDepth,
                    colorType,
                    idatChunkCount,
                    idatTotalSize,
                    expectedSize);
            }

            if (probeMode == LoadProbeMode.DecompressGateNoStateCopy) {
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_ENTER");
                return CursorPngProbeGateNoStateCopy(
                    data,
                    width,
                    height,
                    bitDepth,
                    colorType,
                    idatChunkCount,
                    idatTotalSize,
                    expectedSize);
            }

            if (probeMode == LoadProbeMode.DecompressOutputAlloc) {
                ProbeBreadcrumb("PNGDECOMP_OUTPUT_ALLOC_ENTER");
                ProbeValue("PNGDECOMP_OUTPUT_ALLOC_SIZE", (ulong)(uint)expectedSize);
                byte[] outputProbe = new byte[expectedSize];
                if (outputProbe == null) {
                    compressedData.Dispose();
                    ProbeBreadcrumb("PNGDECOMP_OUTPUT_ALLOC_NULL");
                    ProbeBreadcrumb("PNGDECOMP_OUTPUT_ALLOC_EXIT");
                    return false;
                }

                ProbeBreadcrumb("PNGDECOMP_OUTPUT_ALLOC_OK");
                outputProbe.Dispose();
                compressedData.Dispose();
                ProbeBreadcrumb("PNGDECOMP_OUTPUT_ALLOC_EXIT");
                return false;
            }

            if (probeMode == LoadProbeMode.DecompressTinyBoundary ||
                probeMode == LoadProbeMode.DecompressTinyBitReader ||
                probeMode == LoadProbeMode.DecompressTinyFirstSymbol ||
                probeMode == LoadProbeMode.DecompressTinyOneOp ||
                probeMode == LoadProbeMode.DecompressTinyInflateSmoke) {
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_ENTER");
                bool tinySmokeOk = PngDecompressTinyDispatchProbe(
                    data,
                    compressedData,
                    compressedPos,
                    width,
                    height,
                    bitDepth,
                    colorType,
                    idatChunkCount,
                    idatTotalSize,
                    expectedSize,
                    probeMode,
                    out int tinySmokeLen);
                compressedData.Dispose();
                if (tinySmokeOk) {
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_OK");
                    ProbeValue("PNGLOADER_DECOMPRESSED_BYTES", (ulong)(uint)tinySmokeLen);
                } else {
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                }
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                return false;
            }

            if (probeMode == LoadProbeMode.DecompressInflateSmoke) {
                if (!HasValidCursorPngInputGate(data, width, height, bitDepth, colorType, idatChunkCount, idatTotalSize, expectedSize)) {
                    compressedData.Dispose();
                    return false;
                }

                if (probeMode == LoadProbeMode.DecompressInflateSmoke) {
                    ProbeBreadcrumb("PNGDECOMP_OUTPUT_ALLOC_ENTER");
                    ProbeValue("PNGDECOMP_OUTPUT_ALLOC_SIZE", (ulong)(uint)expectedSize);
                    byte[] smokePixels = new byte[expectedSize];
                    if (smokePixels == null) {
                        compressedData.Dispose();
                        ProbeBreadcrumb("PNGDECOMP_OUTPUT_ALLOC_NULL");
                        ProbeBreadcrumb("PNGDECOMP_OUTPUT_ALLOC_EXIT");
                        return false;
                    }

                    ProbeBreadcrumb("PNGDECOMP_OUTPUT_ALLOC_OK");
                    ProbeBreadcrumb("PNGDECOMP_OUTPUT_ALLOC_EXIT");
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_ENTER");
                    bool smokeOk = PngDecompressInflateSmokeProbe(compressedData, compressedPos, smokePixels, expectedSize, out int smokeSize);
                    compressedData.Dispose();
                    smokePixels.Dispose();
                    if (!smokeOk) {
                        ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                        ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                        return false;
                    }

                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_OK");
                    ProbeValue("PNGLOADER_DECOMPRESSED_BYTES", (ulong)(uint)smokeSize);
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                    return false;
                }
            }

            if (probeMode == LoadProbeMode.DecompressAfterInputGate ||
                probeMode == LoadProbeMode.DecompressPrepNoop ||
                probeMode == LoadProbeMode.DecompressPrepMetadata ||
                probeMode == LoadProbeMode.DecompressPrepBytes ||
                probeMode == LoadProbeMode.DecompressPrepInline ||
                probeMode == LoadProbeMode.DecompressPrepBoundary ||
                probeMode == LoadProbeMode.DecompressPrepContext ||
                probeMode == LoadProbeMode.DecompressInflateBoundary ||
                probeMode == LoadProbeMode.DecompressInflateBitReader ||
                probeMode == LoadProbeMode.DecompressInflateFirstSymbolDecode ||
                probeMode == LoadProbeMode.DecompressInflateLiteralWrite ||
                probeMode == LoadProbeMode.DecompressInflateLengthDistance ||
                probeMode == LoadProbeMode.DecompressInflateOneStep ||
                probeMode == LoadProbeMode.DecompressPostGateFirstInstruction) {
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_ENTER");
                bool splitProbeOk = RunCursorInflateSplitProbe(
                    probeMode,
                    data,
                    compressedData,
                    compressedPos,
                    width,
                    height,
                    bitDepth,
                    colorType,
                    idatChunkCount,
                    idatTotalSize,
                    expectedSize);
                _ = splitProbeOk;
                return false;
            }

            byte[] rawPixels = new byte[expectedSize];
            if (rawPixels == null) {
                compressedData.Dispose();
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                return false;
            }

            ProbeBreadcrumb("PNGLOADER_DECOMPRESS_ENTER");
            bool decompressOk = DecompressZlib(compressedData, compressedPos, rawPixels, expectedSize, out int actualSize);
            compressedData.Dispose();

            if (!decompressOk) {
                rawPixels.Dispose();
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGLOADER_DECOMPRESS_OK");
            ProbeValue("PNGLOADER_DECOMPRESSED_BYTES", (ulong)(uint)actualSize);
            ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");

            if (probeMode == LoadProbeMode.AfterDecompress) {
                rawPixels.Dispose();
                return false;
            }

            ProbeBreadcrumb("PNGLOADER_IMAGE_CREATE_ENTER");
            Image img = new Image(width, height);
            if (img == null || img.RawData == null) {
                rawPixels.Dispose();
                ProbeBreadcrumb("PNGLOADER_IMAGE_NULL");
                ProbeBreadcrumb("PNGLOADER_IMAGE_CREATE_EXIT");
                return false;
            }
            ProbeBreadcrumb("PNGLOADER_IMAGE_OK");

            byte[] prevScanline = new byte[scanlineBytes];
            byte[] currScanline = new byte[scanlineBytes];
            if (prevScanline == null || currScanline == null) {
                rawPixels.Dispose();
                img.Dispose();
                ProbeBreadcrumb("PNGLOADER_IMAGE_NULL");
                ProbeBreadcrumb("PNGLOADER_IMAGE_CREATE_EXIT");
                return false;
            }

            for (int i = 0; i < scanlineBytes; i++) {
                prevScanline[i] = 0;
            }

            int rawPos = 0;
            for (int y = 0; y < height; y++) {
                if (rawPos >= actualSize) {
                    prevScanline.Dispose();
                    currScanline.Dispose();
                    rawPixels.Dispose();
                    img.Dispose();
                    ProbeBreadcrumb("PNGLOADER_IMAGE_NULL");
                    ProbeBreadcrumb("PNGLOADER_IMAGE_CREATE_EXIT");
                    return false;
                }

                byte filterType = rawPixels[rawPos++];

                for (int i = 0; i < scanlineBytes; i++) {
                    if (rawPos < actualSize) {
                        currScanline[i] = rawPixels[rawPos++];
                    } else {
                        currScanline[i] = 0;
                    }
                }

                bool filterOk = ApplyFilter(filterType, currScanline, prevScanline, scanlineBytes, bytesPerPixel);
                if (!filterOk) {
                    prevScanline.Dispose();
                    currScanline.Dispose();
                    rawPixels.Dispose();
                    img.Dispose();
                    ProbeBreadcrumb("PNGLOADER_IMAGE_NULL");
                    ProbeBreadcrumb("PNGLOADER_IMAGE_CREATE_EXIT");
                    return false;
                }

                int imgRowStart = y * width;
                for (int x = 0; x < width; x++) {
                    int pixelOffset = x * 4;
                    byte r = currScanline[pixelOffset + 0];
                    byte g = currScanline[pixelOffset + 1];
                    byte b = currScanline[pixelOffset + 2];
                    byte a = currScanline[pixelOffset + 3];
                    img.RawData[imgRowStart + x] = (int)(((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b);
                }

                byte[] temp = prevScanline;
                prevScanline = currScanline;
                currScanline = temp;
            }

            prevScanline.Dispose();
            currScanline.Dispose();
            rawPixels.Dispose();

            result = img;
            ProbeBreadcrumb("PNGLOADER_IMAGE_CREATE_EXIT");
            return true;
        }
        
        /// <summary>
        /// Read big-endian 32-bit unsigned integer.
        /// Assumption: pos + 3 is within bounds (caller must verify).
        /// </summary>
        private static uint ReadBE32(byte[] data, int pos) {
            return ((uint)data[pos] << 24) | 
                   ((uint)data[pos + 1] << 16) | 
                   ((uint)data[pos + 2] << 8) | 
                   data[pos + 3];
        }
        
        /// <summary>
        /// Apply PNG filter to unfilter a scanline.
        /// 
        /// Filter types (per PNG spec):
        ///   0 = None
        ///   1 = Sub (byte to left)
        ///   2 = Up (byte above)
        ///   3 = Average (average of left and above)
        ///   4 = Paeth (predictor of left, above, upper-left)
        /// 
        /// Returns false if filter type is invalid.
        /// </summary>
        private static bool ApplyFilter(byte filterType, byte[] curr, byte[] prev, int length, int bpp) {
            switch (filterType) {
                case 0: // None
                    // No transformation needed
                    return true;
                    
                case 1: // Sub
                    // Each byte is sum of current and byte bpp positions to the left
                    for (int i = bpp; i < length; i++) {
                        curr[i] = (byte)(curr[i] + curr[i - bpp]);
                    }
                    return true;
                    
                case 2: // Up
                    // Each byte is sum of current and byte directly above
                    for (int i = 0; i < length; i++) {
                        curr[i] = (byte)(curr[i] + prev[i]);
                    }
                    return true;
                    
                case 3: // Average
                    // Each byte is sum of current and floor(average of left and above)
                    for (int i = 0; i < length; i++) {
                        int left = (i >= bpp) ? curr[i - bpp] : 0;
                        int above = prev[i];
                        curr[i] = (byte)(curr[i] + (left + above) / 2);
                    }
                    return true;
                    
                case 4: // Paeth
                    // Each byte uses Paeth predictor
                    for (int i = 0; i < length; i++) {
                        int a = (i >= bpp) ? curr[i - bpp] : 0;   // Left
                        int b = prev[i];                           // Above
                        int c = (i >= bpp) ? prev[i - bpp] : 0;   // Upper-left
                        
                        // Paeth predictor
                        int p = a + b - c;
                        int pa = p - a; if (pa < 0) pa = -pa;
                        int pb = p - b; if (pb < 0) pb = -pb;
                        int pc = p - c; if (pc < 0) pc = -pc;
                        
                        int predictor;
                        if (pa <= pb && pa <= pc) {
                            predictor = a;
                        } else if (pb <= pc) {
                            predictor = b;
                        } else {
                            predictor = c;
                        }
                        
                        curr[i] = (byte)(curr[i] + predictor);
                    }
                    return true;
                    
                default:
                    // Invalid filter type
                    return false;
            }
        }

        private static bool HasValidCursorPngInputGate(byte[] data, int width, int height, byte bitDepth, byte colorType, int idatChunkCount, int idatTotalSize, int expectedSize) {
            ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_ENTER");

            if (data == null || data.Length != 1070) {
                ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_BAD_LEN");
                ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_EXIT");
                return false;
            }

            bool headerOk = data.Length >= 33 &&
                            data[0] == 0x89 &&
                            data[1] == 0x50 &&
                            data[2] == 0x4E &&
                            data[3] == 0x47 &&
                            data[4] == 0x0D &&
                            data[5] == 0x0A &&
                            data[6] == 0x1A &&
                            data[7] == 0x0A &&
                            ReadBE32(data, 8) == 13 &&
                            ReadBE32(data, 12) == 0x49484452;

            if (!headerOk) {
                ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_BAD_FORMAT");
                ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_EXIT");
                return false;
            }

            if (width != 28 || height != 28) {
                ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_BAD_DIMS");
                ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_EXIT");
                return false;
            }

            if (bitDepth != 8 || colorType != 6) {
                ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_BAD_FORMAT");
                ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_EXIT");
                return false;
            }

            if (idatChunkCount != 1 || idatTotalSize != 555 || expectedSize != 3164) {
                ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_BAD_IDAT");
                ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_OK");
            ProbeBreadcrumb("PNGDECOMP_INPUT_GATE_EXIT");
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CursorPngProbeGateOnlyInline(byte[] data, int width, int height, byte bitDepth, byte colorType, int idatChunkCount, int idatTotalSize, int expectedSize) {
            ProbeBreadcrumb("PNGDECOMP_GATE_INLINE_ENTER");
            if (data == null || data.Length != 1070) return false;
            if (data.Length < 33 ||
                data[0] != 0x89 ||
                data[1] != 0x50 ||
                data[2] != 0x4E ||
                data[3] != 0x47 ||
                data[4] != 0x0D ||
                data[5] != 0x0A ||
                data[6] != 0x1A ||
                data[7] != 0x0A ||
                ((uint)data[8] << 24 | (uint)data[9] << 16 | (uint)data[10] << 8 | (uint)data[11]) != 13 ||
                ((uint)data[12] << 24 | (uint)data[13] << 16 | (uint)data[14] << 8 | (uint)data[15]) != 0x49484452) {
                return false;
            }

            if (width != 28 || height != 28) return false;
            if (bitDepth != 8 || colorType != 6) return false;
            if (idatChunkCount != 1 || idatTotalSize != 555 || expectedSize != 3164) return false;
            ProbeBreadcrumb("PNGDECOMP_GATE_INLINE_OK");
            ProbeBreadcrumb("PNGDECOMP_GATE_INLINE_BEFORE_RETURN");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CursorPngProbeGateHelperReturning(byte[] data, int width, int height, byte bitDepth, byte colorType, int idatChunkCount, int idatTotalSize, int expectedSize) {
            ProbeBreadcrumb("PNGDECOMP_GATE_HELPER_ENTER");
            if (data == null || data.Length != 1070) return false;
            if (data.Length < 33 ||
                data[0] != 0x89 ||
                data[1] != 0x50 ||
                data[2] != 0x4E ||
                data[3] != 0x47 ||
                data[4] != 0x0D ||
                data[5] != 0x0A ||
                data[6] != 0x1A ||
                data[7] != 0x0A ||
                ReadBE32(data, 8) != 13 ||
                ReadBE32(data, 12) != 0x49484452) {
                return false;
            }

            if (width != 28 || height != 28 || bitDepth != 8 || colorType != 6 || idatChunkCount != 1 || idatTotalSize != 555 || expectedSize != 3164) {
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_GATE_HELPER_OK");
            ProbeBreadcrumb("PNGDECOMP_GATE_HELPER_RETURNING");
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CursorPngProbeGateOnlyBoolHelper(byte[] data, int width, int height, byte bitDepth, byte colorType, int idatChunkCount, int idatTotalSize, int expectedSize) {
            ProbeBreadcrumb("PNGDECOMP_GATE_BOOL_ENTER");
            if (data == null || data.Length != 1070) return false;
            if (data.Length < 33 ||
                data[0] != 0x89 ||
                data[1] != 0x50 ||
                data[2] != 0x4E ||
                data[3] != 0x47 ||
                data[4] != 0x0D ||
                data[5] != 0x0A ||
                data[6] != 0x1A ||
                data[7] != 0x0A ||
                ReadBE32(data, 8) != 13 ||
                ReadBE32(data, 12) != 0x49484452) {
                return false;
            }

            if (width != 28 || height != 28 || bitDepth != 8 || colorType != 6 || idatChunkCount != 1 || idatTotalSize != 555 || expectedSize != 3164) {
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_GATE_BOOL_OK");
            ProbeBreadcrumb("PNGDECOMP_GATE_BOOL_EXIT");
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CursorPngProbeGateBoolCallerAfter(byte[] data, int width, int height, byte bitDepth, byte colorType, int idatChunkCount, int idatTotalSize, int expectedSize) {
            _ = CursorPngProbeGateOnlyBoolHelper(data, width, height, bitDepth, colorType, idatChunkCount, idatTotalSize, expectedSize);
            ProbeBreadcrumb("PNGDECOMP_GATE_BOOL_CALLER_AFTER");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CursorPngProbeGateNoStateCopy(byte[] data, int width, int height, byte bitDepth, byte colorType, int idatChunkCount, int idatTotalSize, int expectedSize) {
            ProbeBreadcrumb("PNGDECOMP_GATE_NO_STATE_COPY_ENTER");
            _ = CursorPngProbeGateOnlyBoolHelper(data, width, height, bitDepth, colorType, idatChunkCount, idatTotalSize, expectedSize);
            ProbeBreadcrumb("PNGDECOMP_GATE_NO_STATE_COPY_AFTER");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CursorPngProbePostGateFirstInstruction(byte[] data, int width, int height, byte bitDepth, byte colorType, int idatChunkCount, int idatTotalSize, int expectedSize) {
            _ = CursorPngProbeGateOnlyBoolHelper(data, width, height, bitDepth, colorType, idatChunkCount, idatTotalSize, expectedSize);
            ProbeBreadcrumb("PNGDECOMP_POST_GATE_FIRST_INSTRUCTION");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool RunCursorInflateSplitProbe(
            LoadProbeMode probeMode,
            byte[] data,
            byte[] compressedData,
            int compressedPos,
            int width,
            int height,
            byte bitDepth,
            byte colorType,
            int idatChunkCount,
            int idatTotalSize,
            int expectedSize) {
            if (!HasValidCursorPngInputGate(data, width, height, bitDepth, colorType, idatChunkCount, idatTotalSize, expectedSize)) {
                compressedData.Dispose();
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                return false;
            }

            if (probeMode == LoadProbeMode.DecompressPostGateFirstInstruction) {
                ProbeBreadcrumb("PNGDECOMP_POST_GATE_FIRST_INSTRUCTION");
                compressedData.Dispose();
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                return false;
            }

            return RunCursorInflateSplitProbeAfterGate(probeMode, compressedData, compressedPos, expectedSize);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool RunCursorInflateSplitProbeAfterGate(
            LoadProbeMode probeMode,
            byte[] compressedData,
            int compressedPos,
            int expectedSize) {
            ProbeBreadcrumb("PNGDECOMP_AFTER_INPUT_GATE_ENTER");
            ProbeBreadcrumb("PNGDECOMP_PREP_ROUTE_ENTER");
            try {
                if (probeMode == LoadProbeMode.DecompressAfterInputGate) {
                    compressedData.Dispose();
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                    return false;
                }

                if (probeMode == LoadProbeMode.DecompressPrepNoop) {
                    PngInflatePrepNoopProbe();
                    compressedData.Dispose();
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                    return false;
                }

                if (probeMode == LoadProbeMode.DecompressPrepMetadata) {
                    PngInflatePrepMetadataProbe(28, 28, compressedPos, expectedSize);
                    compressedData.Dispose();
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                    return false;
                }

                if (probeMode == LoadProbeMode.DecompressPrepBytes) {
                    PngInflatePrepBytesProbe(compressedData, expectedSize);
                    compressedData.Dispose();
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                    return false;
                }

                if (probeMode == LoadProbeMode.DecompressPrepInline) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_INLINE_ENTER");
                    ProbeBreadcrumb("PNGDECOMP_PREP_INLINE_BEFORE_OUTPUT_ALLOC");
                    byte[] outputProbe = new byte[expectedSize];
                    ProbeBreadcrumb("PNGDECOMP_PREP_INLINE_AFTER_OUTPUT_ALLOC");
                    ProbeBreadcrumb("PNGDECOMP_PREP_INLINE_BEFORE_BITREADER_INIT");
                    CursorInflateContext inlineContext = new CursorInflateContext();
                    inlineContext.Input = compressedData;
                    inlineContext.InputLen = compressedData != null ? compressedData.Length : 0;
                    inlineContext.InPos = compressedPos;
                    inlineContext.BitBuf = 0;
                    inlineContext.BitCount = 0;
                    inlineContext.BlockType = 0;
                    ProbeBreadcrumb("PNGDECOMP_PREP_INLINE_AFTER_BITREADER_INIT");
                    ProbeBreadcrumb("PNGDECOMP_PREP_INLINE_BEFORE_TABLE_REFS");
                    var inlineLitLenTable = _litLenTable;
                    var inlineDistTable = _distTable;
                    _ = inlineLitLenTable;
                    _ = inlineDistTable;
                    ProbeBreadcrumb("PNGDECOMP_PREP_INLINE_AFTER_TABLE_REFS");
                    if (outputProbe != null) {
                        outputProbe.Dispose();
                    }
                    ProbeBreadcrumb("PNGDECOMP_PREP_INLINE_EXIT");
                    compressedData.Dispose();
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                    return false;
                }

                if (probeMode == LoadProbeMode.DecompressPrepBoundary) {
                    if (!TryPrepareCursorInflateBoundaryContext(compressedData, compressedPos, out CursorInflateContext boundaryContext)) {
                        ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_FAIL");
                        compressedData.Dispose();
                        ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                        ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                        return false;
                    }

                    ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_OK");
                    _ = boundaryContext;
                    compressedData.Dispose();
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                    return false;
                }

                if (probeMode == LoadProbeMode.DecompressPrepContext) {
                    if (!TryPrepareCursorInflateContext(compressedData, compressedPos, out CursorInflateContext prepFullInflateContext)) {
                        ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                        compressedData.Dispose();
                        ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                        ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                        return false;
                    }

                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_OK");
                    _ = prepFullInflateContext;
                    compressedData.Dispose();
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                    return false;
                }

                if (probeMode == LoadProbeMode.DecompressInflateBoundary || probeMode == LoadProbeMode.DecompressInflateBitReader) {
                    if (!TryPrepareCursorInflateBoundaryContext(compressedData, compressedPos, out CursorInflateContext boundaryContext)) {
                        if (probeMode == LoadProbeMode.DecompressInflateBitReader) {
                            ProbeBreadcrumb("PNGDECOMP_BITREADER_BAD");
                            ProbeBreadcrumb("PNGDECOMP_BITREADER_EXIT");
                        }
                        compressedData.Dispose();
                        ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                        ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                        return false;
                    }

                    if (probeMode == LoadProbeMode.DecompressInflateBoundary) {
                        PngDecompressInflateBoundaryProbe();
                        compressedData.Dispose();
                        ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                        ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                        return false;
                    }

                    _ = PngDecompressInflateBitReaderProbe(ref boundaryContext);
                    compressedData.Dispose();
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                    return false;
                }

                if (!TryPrepareCursorInflateContext(compressedData, compressedPos, out CursorInflateContext fullInflateContext)) {
                    compressedData.Dispose();
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                    ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                    return false;
                }

                if (probeMode == LoadProbeMode.DecompressInflateFirstSymbolDecode) {
                    _ = PngDecompressInflateFirstSymbolDecodeProbe(ref fullInflateContext, out int firstSymbol);
                    _ = firstSymbol;
                } else if (probeMode == LoadProbeMode.DecompressInflateLiteralWrite) {
                    if (PngDecompressInflateFirstSymbolDecodeProbe(ref fullInflateContext, out int literalSymbol) && literalSymbol < 256) {
                        _ = PngDecompressInflateLiteralWriteProbe(literalSymbol, expectedSize);
                    }
                } else if (probeMode == LoadProbeMode.DecompressInflateLengthDistance) {
                    if (PngDecompressInflateFirstSymbolDecodeProbe(ref fullInflateContext, out int lengthSymbol) && lengthSymbol > 255) {
                        _ = PngDecompressInflateLengthDistanceProbe(ref fullInflateContext, lengthSymbol);
                    }
                } else if (probeMode == LoadProbeMode.DecompressInflateOneStep) {
                    _ = PngDecompressInflateOneStepProbe(ref fullInflateContext, expectedSize, out int oneStepSymbol);
                    _ = oneStepSymbol;
                }

                compressedData.Dispose();
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_NULL");
                ProbeBreadcrumb("PNGLOADER_DECOMPRESS_EXIT");
                return false;
            } finally {
                ProbeBreadcrumb("PNGDECOMP_PREP_ROUTE_EXIT");
                ProbeBreadcrumb("PNGDECOMP_AFTER_INPUT_GATE_EXIT");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngInflatePrepNoopProbe() {
            ProbeBreadcrumb("PNGDECOMP_PREP_NOOP_ENTER");
            ProbeBreadcrumb("PNGDECOMP_PREP_NOOP_EXIT");
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngInflatePrepMetadataProbe(int width, int height, int compressedLen, int expectedOutLen) {
            ProbeBreadcrumb("PNGDECOMP_PREP_META_ENTER");
            if (width == 28 && height == 28) {
                ProbeBreadcrumb("PNGDECOMP_PREP_META_DIMS_OK");
            } else {
                ProbeBreadcrumb("PNGDECOMP_PREP_META_FAIL");
                ProbeBreadcrumb("PNGDECOMP_PREP_META_EXIT");
                return false;
            }

            if (compressedLen == 555 && expectedOutLen == 3164) {
                ProbeBreadcrumb("PNGDECOMP_PREP_META_LEN_OK");
            } else {
                ProbeBreadcrumb("PNGDECOMP_PREP_META_FAIL");
                ProbeBreadcrumb("PNGDECOMP_PREP_META_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_PREP_META_EXIT");
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngInflatePrepBytesProbe(byte[] compressedBytes, int expectedOutLen) {
            ProbeBreadcrumb("PNGDECOMP_PREP_BYTES_ENTER");
            if (compressedBytes == null) {
                ProbeBreadcrumb("PNGDECOMP_PREP_BYTES_NULL");
                ProbeBreadcrumb("PNGDECOMP_PREP_BYTES_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_PREP_BYTES_OK");
            ProbeValue("PNGDECOMP_PREP_BYTES_LEN", (ulong)(uint)compressedBytes.Length);
            _ = expectedOutLen;
            ProbeBreadcrumb("PNGDECOMP_PREP_BYTES_EXIT");
            return true;
        }

        private static bool TryPrepareCursorInflateContext(byte[] input, int inputLen, out CursorInflateContext context) {
            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_ENTER");
            context = new CursorInflateContext();

            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_VALIDATE_ENTER");
            if (input == null || inputLen < 2) {
                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                return false;
            }

            byte cmf = input[0];
            byte flg = input[1];
            if ((cmf & 0x0F) != 8 || (flg & 0x20) != 0 || ((cmf * 256 + flg) % 31) != 0) {
                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                return false;
            }
            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_VALIDATE_EXIT");

            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_ALLOC_ENTER");
            context = new CursorInflateContext();
            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_ALLOC_EXIT");

            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_BITREADER_ENTER");
            int inPos = 2;
            int bitBuf = 0;
            int bitCount = 0;

            if (bitCount < 1) {
                if (inPos >= inputLen) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                    return false;
                }
                bitBuf |= input[inPos++] << bitCount;
                bitCount += 8;
            }
            bool lastBlock = (bitBuf & 1) == 1;
            _ = lastBlock;
            bitBuf >>= 1;
            bitCount--;

            if (bitCount < 2) {
                if (inPos >= inputLen) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                    return false;
                }
                bitBuf |= input[inPos++] << bitCount;
                bitCount += 8;
            }

            int blockType = bitBuf & 3;
            bitBuf >>= 2;
            bitCount -= 2;
            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_BITREADER_EXIT");
            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_TABLES_ENTER");

            if (blockType == 1) {
                for (int i = 0; i < LITLEN_TABLE_SIZE; i++) {
                    if (i < 144) _litLenLengths[i] = 8;
                    else if (i < 256) _litLenLengths[i] = 9;
                    else if (i < 280) _litLenLengths[i] = 7;
                    else _litLenLengths[i] = 8;
                }

                for (int i = 0; i < DIST_TABLE_SIZE; i++) {
                    _distLengths[i] = 5;
                }

                if (!BuildDecodeTable(_litLenLengths, LITLEN_TABLE_SIZE, 15, _litLenTable, 1 << 15)) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                    return false;
                }
                if (!BuildDecodeTable(_distLengths, DIST_TABLE_SIZE, 15, _distTable, 1 << 15)) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                    return false;
                }
            } else if (blockType == 2) {
                while (bitCount < 14) {
                    if (inPos >= inputLen) {
                        ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                        ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                        return false;
                    }
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }

                int hlit = (bitBuf & 0x1F) + 257;
                bitBuf >>= 5;
                bitCount -= 5;

                int hdist = (bitBuf & 0x1F) + 1;
                bitBuf >>= 5;
                bitCount -= 5;

                int hclen = (bitBuf & 0x0F) + 4;
                bitBuf >>= 4;
                bitCount -= 4;

                if (hlit > 286 || hdist > 32) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                    return false;
                }

                int[] clOrder = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

                for (int i = 0; i < CODELENGTH_TABLE_SIZE; i++) {
                    _codeLenLengths[i] = 0;
                }

                for (int i = 0; i < hclen; i++) {
                    while (bitCount < 3) {
                        if (inPos >= inputLen) {
                            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                            return false;
                        }
                        bitBuf |= input[inPos++] << bitCount;
                        bitCount += 8;
                    }

                    _codeLenLengths[clOrder[i]] = bitBuf & 7;
                    bitBuf >>= 3;
                    bitCount -= 3;
                }

                if (!BuildDecodeTable(_codeLenLengths, CODELENGTH_TABLE_SIZE, 7, _codeLenTable, 1 << 7)) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                    return false;
                }

                int totalCodes = hlit + hdist;
                for (int i = 0; i < 320; i++) {
                    _allLengths[i] = 0;
                }

                int idx = 0;
                int safetyCounter = 0;
                while (idx < totalCodes) {
                    if (++safetyCounter > totalCodes + 1000) {
                        ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                        ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                        return false;
                    }

                    while (bitCount < 15) {
                        if (inPos >= inputLen) {
                            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                            return false;
                        }
                        bitBuf |= input[inPos++] << bitCount;
                        bitCount += 8;
                    }

                    int sym = DecodeSymbol(_codeLenTable, 7, ref bitBuf, ref bitCount);
                    if (sym < 0) {
                        ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                        ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                        return false;
                    }

                    if (sym < 16) {
                        _allLengths[idx++] = sym;
                    } else if (sym == 16) {
                        while (bitCount < 2) {
                            if (inPos >= inputLen) {
                                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                                return false;
                            }
                            bitBuf |= input[inPos++] << bitCount;
                            bitCount += 8;
                        }

                        int repeat = 3 + (bitBuf & 3);
                        bitBuf >>= 2;
                        bitCount -= 2;

                        if (idx == 0) {
                            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                            return false;
                        }

                        int prevLen = _allLengths[idx - 1];
                        for (int j = 0; j < repeat && idx < totalCodes; j++) {
                            _allLengths[idx++] = prevLen;
                        }
                    } else if (sym == 17) {
                        while (bitCount < 3) {
                            if (inPos >= inputLen) {
                                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                                return false;
                            }
                            bitBuf |= input[inPos++] << bitCount;
                            bitCount += 8;
                        }

                        int repeat = 3 + (bitBuf & 7);
                        bitBuf >>= 3;
                        bitCount -= 3;

                        for (int j = 0; j < repeat && idx < totalCodes; j++) {
                            _allLengths[idx++] = 0;
                        }
                    } else if (sym == 18) {
                        while (bitCount < 7) {
                            if (inPos >= inputLen) {
                                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                                return false;
                            }
                            bitBuf |= input[inPos++] << bitCount;
                            bitCount += 8;
                        }

                        int repeat = 11 + (bitBuf & 0x7F);
                        bitBuf >>= 7;
                        bitCount -= 7;

                        for (int j = 0; j < repeat && idx < totalCodes; j++) {
                            _allLengths[idx++] = 0;
                        }
                    } else {
                        ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                        ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                        return false;
                    }
                }

                for (int i = 0; i < hlit && i < LITLEN_TABLE_SIZE; i++) {
                    _litLenLengths[i] = _allLengths[i];
                }
                for (int i = hlit; i < LITLEN_TABLE_SIZE; i++) {
                    _litLenLengths[i] = 0;
                }

                for (int i = 0; i < hdist && i < DIST_TABLE_SIZE; i++) {
                    _distLengths[i] = _allLengths[hlit + i];
                }
                for (int i = hdist; i < DIST_TABLE_SIZE; i++) {
                    _distLengths[i] = 0;
                }

                if (!BuildDecodeTable(_litLenLengths, LITLEN_TABLE_SIZE, 15, _litLenTable, 1 << 15)) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                    return false;
                }
                if (!BuildDecodeTable(_distLengths, DIST_TABLE_SIZE, 15, _distTable, 1 << 15)) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                    ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                    return false;
                }
            } else {
                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_FAIL");
                ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
                return false;
            }
            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_TABLES_EXIT");

            context.Input = input;
            context.InputLen = inputLen;
            context.InPos = inPos;
            context.BitBuf = bitBuf;
            context.BitCount = bitCount;
            context.BlockType = blockType;
            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_OK");
            ProbeBreadcrumb("PNGDECOMP_PREP_CONTEXT_EXIT");
            return true;
        }

        private static bool TryPrepareCursorInflateBoundaryContext(byte[] input, int inputLen, out CursorInflateContext context) {
            ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_ENTER");
            context = new CursorInflateContext();

            if (input == null || inputLen < 2) {
                ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_FAIL");
                ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_EXIT");
                return false;
            }

            byte cmf = input[0];
            byte flg = input[1];
            if ((cmf & 0x0F) != 8 || (flg & 0x20) != 0 || ((cmf * 256 + flg) % 31) != 0) {
                ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_FAIL");
                ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_BEFORE_CALL");
            int inPos = 2;
            int bitBuf = 0;
            int bitCount = 0;

            if (bitCount < 1) {
                if (inPos >= inputLen) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_FAIL");
                    ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_EXIT");
                    return false;
                }
                bitBuf |= input[inPos++] << bitCount;
                bitCount += 8;
            }
            bool lastBlock = (bitBuf & 1) == 1;
            _ = lastBlock;
            bitBuf >>= 1;
            bitCount--;

            if (bitCount < 2) {
                if (inPos >= inputLen) {
                    ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_FAIL");
                    ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_EXIT");
                    return false;
                }
                bitBuf |= input[inPos++] << bitCount;
                bitCount += 8;
            }

            int blockType = bitBuf & 3;
            bitBuf >>= 2;
            bitCount -= 2;
            ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_AFTER_CALL");

            context.Input = input;
            context.InputLen = inputLen;
            context.InPos = inPos;
            context.BitBuf = bitBuf;
            context.BitCount = bitCount;
            context.BlockType = blockType;
            ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_OK");
            ProbeBreadcrumb("PNGDECOMP_PREP_BOUNDARY_EXIT");
            return true;
        }

        private static bool TryDecodeCursorFirstSymbol(ref CursorInflateContext context, out int symbol) {
            symbol = -1;

            while (context.BitCount < 15) {
                if (context.InPos >= context.InputLen) return false;
                context.BitBuf |= context.Input[context.InPos++] << context.BitCount;
                context.BitCount += 8;
            }

            symbol = DecodeSymbol(_litLenTable, 15, ref context.BitBuf, ref context.BitCount);
            return symbol >= 0;
        }

        private static bool TryDecodeCursorLengthDistance(ref CursorInflateContext context, int symbol, out int length, out int distanceSymbol, out int distance) {
            length = -1;
            distanceSymbol = -1;
            distance = -1;

            if (symbol < FIRST_LENGTH_CODE_INDEX || symbol > LAST_LENGTH_CODE_INDEX) {
                return false;
            }

            int inPos = context.InPos;
            int bitBuf = context.BitBuf;
            int bitCount = context.BitCount;

            length = GetLength(symbol, ref bitBuf, ref bitCount, context.Input, context.InputLen, ref inPos);
            if (length < 0) return false;

            while (bitCount < 15) {
                if (inPos >= context.InputLen) return false;
                bitBuf |= context.Input[inPos++] << bitCount;
                bitCount += 8;
            }

            distanceSymbol = DecodeSymbol(_distTable, 15, ref bitBuf, ref bitCount);
            if (distanceSymbol < 0) return false;

            distance = GetDistance(distanceSymbol, ref bitBuf, ref bitCount, context.Input, context.InputLen, ref inPos);
            if (distance < 0) return false;

            context.InPos = inPos;
            context.BitBuf = bitBuf;
            context.BitCount = bitCount;
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PngDecompressInflateBoundaryProbe() {
            ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_BOUNDARY_ENTER");
            ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_BOUNDARY_EXIT");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressTinyBoundaryProbe(ref CursorPngTinyProbeContext context) {
            ProbeBreadcrumb("PNGDECOMP_TINY_BOUNDARY_ENTER");
            if (!HasValidCursorPngInputGate(context.Data, context.Width, context.Height, context.BitDepth, context.ColorType, context.IdatChunkCount, context.IdatTotalSize, context.ExpectedSize)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_BOUNDARY_EXIT");
                return false;
            }

            if (!TryPrepareCursorInflateBoundaryContext(context.CompressedData, context.CompressedPos, out CursorInflateContext boundaryContext)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_BOUNDARY_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_TINY_BOUNDARY_AFTER_SETUP");
            _ = boundaryContext;
            ProbeBreadcrumb("PNGDECOMP_TINY_BOUNDARY_BEFORE_RETURN");
            ProbeBreadcrumb("PNGDECOMP_TINY_BOUNDARY_EXIT");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressTinyBitReaderProbe(ref CursorPngTinyProbeContext context) {
            ProbeBreadcrumb("PNGDECOMP_TINY_BITREADER_ENTER");
            if (!HasValidCursorPngInputGate(context.Data, context.Width, context.Height, context.BitDepth, context.ColorType, context.IdatChunkCount, context.IdatTotalSize, context.ExpectedSize)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_BITREADER_EXIT");
                return false;
            }

            if (!TryPrepareCursorInflateBoundaryContext(context.CompressedData, context.CompressedPos, out CursorInflateContext boundaryContext)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_BITREADER_EXIT");
                return false;
            }

            ProbeValue("PNGDECOMP_TINY_BITREADER_BYTEPOS", (ulong)(uint)boundaryContext.InPos);
            ProbeValue("PNGDECOMP_TINY_BITREADER_BITCOUNT", (ulong)(uint)boundaryContext.BitCount);
            ProbeBreadcrumb("PNGDECOMP_TINY_BITREADER_EXIT");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressTinyFirstSymbolProbe(ref CursorPngTinyProbeContext context) {
            ProbeBreadcrumb("PNGDECOMP_TINY_SYMBOL_ENTER");
            if (!HasValidCursorPngInputGate(context.Data, context.Width, context.Height, context.BitDepth, context.ColorType, context.IdatChunkCount, context.IdatTotalSize, context.ExpectedSize)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_SYMBOL_EXIT");
                return false;
            }

            if (!TryPrepareCursorInflateContext(context.CompressedData, context.CompressedPos, out CursorInflateContext symbolContext)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_SYMBOL_EXIT");
                return false;
            }

            if (!TryDecodeCursorFirstSymbol(ref symbolContext, out int symbol)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_SYMBOL_EXIT");
                return false;
            }

            ProbeValue("PNGDECOMP_TINY_SYMBOL_VALUE", (ulong)(uint)symbol);
            if (symbol < 256) {
                ProbeBreadcrumb("PNGDECOMP_TINY_SYMBOL_LITERAL");
            } else if (symbol == 256) {
                ProbeBreadcrumb("PNGDECOMP_TINY_SYMBOL_END");
            } else {
                ProbeBreadcrumb("PNGDECOMP_TINY_SYMBOL_LENGTH");
            }

            ProbeBreadcrumb("PNGDECOMP_TINY_SYMBOL_EXIT");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressTinyOneOpProbe(ref CursorPngTinyProbeContext context) {
            ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_ENTER");
            if (!HasValidCursorPngInputGate(context.Data, context.Width, context.Height, context.BitDepth, context.ColorType, context.IdatChunkCount, context.IdatTotalSize, context.ExpectedSize)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT");
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                return false;
            }

            if (!TryPrepareCursorInflateContext(context.CompressedData, context.CompressedPos, out CursorInflateContext opContext)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT");
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                return false;
            }

            if (!TryDecodeCursorFirstSymbol(ref opContext, out int firstSymbol)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT");
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                return false;
            }

            if (firstSymbol < 256) {
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_LITERAL");
                if (context.ExpectedSize <= 0) {
                    ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                    return false;
                }

                byte[] oneByteOutput = new byte[context.ExpectedSize];
                if (oneByteOutput == null) {
                    ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                    return false;
                }

                int outPos = 0;
                if (outPos >= context.ExpectedSize) {
                    ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                    return false;
                }

                oneByteOutput[outPos] = (byte)firstSymbol;
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_OK");
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                return false;
            }

            if (firstSymbol == 256) {
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT");
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                return false;
            }

            if (firstSymbol > 256) {
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_LENGTH");
                if (!TryDecodeCursorLengthDistance(ref opContext, firstSymbol, out int length, out int distanceSymbol, out int distance)) {
                    ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                    return false;
                }

                _ = distanceSymbol;
                if (length <= 0 || length > context.ExpectedSize || distance <= 0 || distance > context.ExpectedSize) {
                    ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                    return false;
                }

                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_OK");
                ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT");
            ProbeBreadcrumb("PNGDECOMP_TINY_ONEOP_EXIT");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressTinyInflateSmokeProbe(ref CursorPngTinyProbeContext context, out int smokeLen) {
            smokeLen = 0;
            ProbeBreadcrumb("PNGDECOMP_TINY_INFLATE_ENTER");
            if (!HasValidCursorPngInputGate(context.Data, context.Width, context.Height, context.BitDepth, context.ColorType, context.IdatChunkCount, context.IdatTotalSize, context.ExpectedSize)) {
                ProbeBreadcrumb("PNGDECOMP_TINY_INFLATE_BOUNDS_ABORT");
                ProbeBreadcrumb("PNGDECOMP_TINY_INFLATE_EXIT");
                return false;
            }

            byte[] smokeOutput = new byte[context.ExpectedSize];
            if (smokeOutput == null) {
                ProbeBreadcrumb("PNGDECOMP_TINY_INFLATE_BOUNDS_ABORT");
                ProbeBreadcrumb("PNGDECOMP_TINY_INFLATE_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_TINY_INFLATE_PROGRESS");
            bool smokeOk = DecompressZlib(context.CompressedData, context.CompressedPos, smokeOutput, context.ExpectedSize, MAX_INFLATE_SMOKE_SYMBOL_ITERATIONS, out int smokeActualLen);
            if (!smokeOk || smokeActualLen != context.ExpectedSize) {
                ProbeBreadcrumb("PNGDECOMP_TINY_INFLATE_BOUNDS_ABORT");
                ProbeBreadcrumb("PNGDECOMP_TINY_INFLATE_EXIT");
                return false;
            }

            smokeLen = smokeActualLen;
            ProbeBreadcrumb("PNGDECOMP_TINY_INFLATE_OK");
            ProbeBreadcrumb("PNGDECOMP_TINY_INFLATE_EXIT");
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressTinyDispatchProbe(
            byte[] data,
            byte[] compressedData,
            int compressedPos,
            int width,
            int height,
            byte bitDepth,
            byte colorType,
            int idatChunkCount,
            int idatTotalSize,
            int expectedSize,
            LoadProbeMode probeMode,
            out int smokeLen) {
            CursorPngTinyProbeContext context = new CursorPngTinyProbeContext {
                Data = data,
                CompressedData = compressedData,
                CompressedPos = compressedPos,
                Width = width,
                Height = height,
                BitDepth = bitDepth,
                ColorType = colorType,
                IdatChunkCount = idatChunkCount,
                IdatTotalSize = idatTotalSize,
                ExpectedSize = expectedSize
            };
            return PngDecompressTinyDispatchProbe(ref context, probeMode, out smokeLen);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressTinyDispatchProbe(
            ref CursorPngTinyProbeContext context,
            LoadProbeMode probeMode,
            out int smokeLen) {
            smokeLen = 0;
            if (probeMode == LoadProbeMode.DecompressTinyBoundary) {
                return PngDecompressTinyBoundaryProbe(ref context);
            }

            if (probeMode == LoadProbeMode.DecompressTinyBitReader) {
                return PngDecompressTinyBitReaderProbe(ref context);
            }

            if (probeMode == LoadProbeMode.DecompressTinyFirstSymbol) {
                return PngDecompressTinyFirstSymbolProbe(ref context);
            }

            if (probeMode == LoadProbeMode.DecompressTinyOneOp) {
                return PngDecompressTinyOneOpProbe(ref context);
            }

            if (probeMode == LoadProbeMode.DecompressTinyInflateSmoke) {
                return PngDecompressTinyInflateSmokeProbe(ref context, out smokeLen);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressInflateBitReaderProbe(ref CursorInflateContext context) {
            ProbeBreadcrumb("PNGDECOMP_BITREADER_ENTER");
            ProbeValue("PNGDECOMP_BITREADER_BYTEPOS", (ulong)(uint)context.InPos);
            ProbeValue("PNGDECOMP_BITREADER_BITCOUNT", (ulong)(uint)context.BitCount);
            ProbeBreadcrumb("PNGDECOMP_BITREADER_OK");
            ProbeBreadcrumb("PNGDECOMP_BITREADER_EXIT");
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressInflateFirstSymbolDecodeProbe(ref CursorInflateContext context, out int symbol) {
            ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_DECODE_ENTER");
            if (!TryDecodeCursorFirstSymbol(ref context, out symbol)) {
                ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_DECODE_EXIT");
                return false;
            }

            ProbeValue("PNGDECOMP_FIRST_SYMBOL_VALUE", (ulong)(uint)symbol);
            if (symbol < 256) {
                ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_LITERAL");
            } else if (symbol == 256) {
                ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_END");
            } else {
                ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_LENGTH");
            }

            ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_DECODE_EXIT");
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressInflateLiteralWriteProbe(int symbol, int expectedSize) {
            ProbeBreadcrumb("PNGDECOMP_LITERAL_WRITE_ENTER");
            if (symbol < 0 || symbol > 255 || expectedSize <= 0) {
                ProbeBreadcrumb("PNGDECOMP_LITERAL_WRITE_BOUNDS_BAD");
                ProbeBreadcrumb("PNGDECOMP_LITERAL_WRITE_EXIT");
                return false;
            }

            int outPos = 0;
            if (outPos < 0 || outPos >= expectedSize) {
                ProbeBreadcrumb("PNGDECOMP_LITERAL_WRITE_BOUNDS_BAD");
                ProbeBreadcrumb("PNGDECOMP_LITERAL_WRITE_EXIT");
                return false;
            }

            byte[] scratch = new byte[1];
            if (scratch == null) {
                ProbeBreadcrumb("PNGDECOMP_LITERAL_WRITE_BOUNDS_BAD");
                ProbeBreadcrumb("PNGDECOMP_LITERAL_WRITE_EXIT");
                return false;
            }

            scratch[0] = (byte)symbol;
            ProbeBreadcrumb("PNGDECOMP_LITERAL_WRITE_BOUNDS_OK");
            ProbeBreadcrumb("PNGDECOMP_LITERAL_WRITE_EXIT");
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressInflateLengthDistanceProbe(ref CursorInflateContext context, int symbol) {
            ProbeBreadcrumb("PNGDECOMP_LEN_DIST_ENTER");
            if (!TryDecodeCursorLengthDistance(ref context, symbol, out int length, out int distanceSymbol, out int distance)) {
                ProbeBreadcrumb("PNGDECOMP_LEN_DIST_EXIT");
                return false;
            }

            ProbeValue("PNGDECOMP_LENGTH_SYMBOL", (ulong)(uint)symbol);
            ProbeValue("PNGDECOMP_LENGTH_VALUE", (ulong)(uint)length);
            ProbeValue("PNGDECOMP_DISTANCE_SYMBOL", (ulong)(uint)distanceSymbol);
            ProbeValue("PNGDECOMP_DISTANCE_VALUE", (ulong)(uint)distance);
            ProbeBreadcrumb("PNGDECOMP_LEN_DIST_EXIT");
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressInflateOneStepProbe(ref CursorInflateContext context, int expectedSize, out int firstSymbol) {
            ProbeBreadcrumb("PNGDECOMP_ONE_STEP_ENTER");
            if (!TryDecodeCursorFirstSymbol(ref context, out firstSymbol)) {
                ProbeBreadcrumb("PNGDECOMP_ONE_STEP_BOUNDS_ABORT");
                ProbeBreadcrumb("PNGDECOMP_ONE_STEP_EXIT");
                return false;
            }

            ProbeValue("PNGDECOMP_FIRST_SYMBOL_VALUE", (ulong)(uint)firstSymbol);
            if (firstSymbol < 256) {
                ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_LITERAL");
            } else if (firstSymbol == 256) {
                ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_END");
            } else {
                ProbeBreadcrumb("PNGDECOMP_FIRST_SYMBOL_LENGTH");
            }

            if (firstSymbol < 256) {
                ProbeBreadcrumb("PNGDECOMP_ONE_STEP_LITERAL");
                if (expectedSize <= 0) {
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_EXIT");
                    return false;
                }

                byte[] scratch = new byte[1];
                if (scratch == null) {
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_EXIT");
                    return false;
                }

                scratch[0] = (byte)firstSymbol;
                ProbeBreadcrumb("PNGDECOMP_ONE_STEP_OK");
                ProbeBreadcrumb("PNGDECOMP_ONE_STEP_EXIT");
                return true;
            }

            if (firstSymbol > 255) {
                ProbeBreadcrumb("PNGDECOMP_ONE_STEP_BACKREF");
                if (!TryDecodeCursorLengthDistance(ref context, firstSymbol, out int length, out int distanceSymbol, out int distance)) {
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_EXIT");
                    return false;
                }

                _ = distanceSymbol;

                if (distance <= 0) {
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_EXIT");
                    return false;
                }

                byte[] scratch = new byte[expectedSize];
                if (scratch == null) {
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_EXIT");
                    return false;
                }

                int outPos = 0;
                if (distance > outPos) {
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_BOUNDS_ABORT");
                    ProbeBreadcrumb("PNGDECOMP_ONE_STEP_EXIT");
                    return false;
                }

                for (int i = 0; i < length && outPos < expectedSize; i++) {
                    scratch[outPos] = scratch[outPos - distance];
                    outPos++;
                }

                ProbeBreadcrumb("PNGDECOMP_ONE_STEP_OK");
                ProbeBreadcrumb("PNGDECOMP_ONE_STEP_EXIT");
                return true;
            }

            ProbeBreadcrumb("PNGDECOMP_ONE_STEP_BOUNDS_ABORT");
            ProbeBreadcrumb("PNGDECOMP_ONE_STEP_EXIT");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PngDecompressNoopProbe() {
            ProbeBreadcrumb("PNGDECOMP_NOOP_ENTER");
            ProbeBreadcrumb("PNGDECOMP_NOOP_EXIT");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PngDecompressBytesNoopProbe(byte[] compressedBytes) {
            ProbeBreadcrumb("PNGDECOMP_BYTES_NOOP_ENTER");
            if (compressedBytes == null) {
                ProbeBreadcrumb("PNGDECOMP_BYTES_NOOP_NULL");
                ProbeBreadcrumb("PNGDECOMP_BYTES_NOOP_EXIT");
                return;
            }

            ProbeBreadcrumb("PNGDECOMP_BYTES_NOOP_OK");
            ProbeValue("PNGDECOMP_BYTES_NOOP_LEN", (ulong)(uint)compressedBytes.Length);
            ProbeBreadcrumb("PNGDECOMP_BYTES_NOOP_EXIT");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressZlibHeaderProbe(byte[] input, int inputLen) {
            ProbeBreadcrumb("PNGDECOMP_ZLIB_HEADER_ENTER");
            if (input == null || inputLen < 2) {
                ProbeBreadcrumb("PNGDECOMP_ZLIB_LEN_BAD");
                ProbeBreadcrumb("PNGDECOMP_ZLIB_HEADER_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_ZLIB_LEN_OK");

            byte cmf = input[0];
            byte flg = input[1];
            ProbeValue("PNGDECOMP_ZLIB_CMF", (ulong)cmf);
            ProbeValue("PNGDECOMP_ZLIB_FLG", (ulong)flg);

            if ((cmf & 0x0F) == 8) {
                ProbeBreadcrumb("PNGDECOMP_ZLIB_METHOD_OK");
            } else {
                ProbeBreadcrumb("PNGDECOMP_ZLIB_METHOD_BAD");
            }

            if (((cmf * 256 + flg) % 31) == 0) {
                ProbeBreadcrumb("PNGDECOMP_ZLIB_FCHECK_OK");
            } else {
                ProbeBreadcrumb("PNGDECOMP_ZLIB_FCHECK_BAD");
            }

            if ((flg & 0x20) != 0) {
                ProbeBreadcrumb("PNGDECOMP_ZLIB_FDICT_SET");
            } else {
                ProbeBreadcrumb("PNGDECOMP_ZLIB_FDICT_CLEAR");
            }

            ProbeBreadcrumb("PNGDECOMP_ZLIB_HEADER_EXIT");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressDeflateHeaderProbe(byte[] input, int inputLen) {
            ProbeBreadcrumb("PNGDECOMP_DEFLATE_ENTER");
            ProbeBreadcrumb("PNGDECOMP_DEFLATE_FIRST_BLOCK_ENTER");

            if (input == null || inputLen < 2) {
                ProbeBreadcrumb("PNGDECOMP_DEFLATE_FIRST_BLOCK_EXIT");
                ProbeBreadcrumb("PNGDECOMP_DEFLATE_EXIT");
                return false;
            }

            byte cmf = input[0];
            byte flg = input[1];
            if ((cmf & 0x0F) != 8 || (flg & 0x20) != 0 || ((cmf * 256 + flg) % 31) != 0) {
                ProbeBreadcrumb("PNGDECOMP_DEFLATE_FIRST_BLOCK_EXIT");
                ProbeBreadcrumb("PNGDECOMP_DEFLATE_EXIT");
                return false;
            }

            int inPos = 2;
            int bitBuf = 0;
            int bitCount = 0;

            if (bitCount < 1) {
                if (inPos >= inputLen) {
                    ProbeBreadcrumb("PNGDECOMP_DEFLATE_FIRST_BLOCK_EXIT");
                    ProbeBreadcrumb("PNGDECOMP_DEFLATE_EXIT");
                    return false;
                }
                bitBuf |= input[inPos++] << bitCount;
                bitCount += 8;
            }
            int bfinal = bitBuf & 1;
            ProbeValue("PNGDECOMP_DEFLATE_BFINAL", (ulong)(uint)bfinal);
            bitBuf >>= 1;
            bitCount--;

            if (bitCount < 2) {
                if (inPos >= inputLen) {
                    ProbeBreadcrumb("PNGDECOMP_DEFLATE_FIRST_BLOCK_EXIT");
                    ProbeBreadcrumb("PNGDECOMP_DEFLATE_EXIT");
                    return false;
                }
                bitBuf |= input[inPos++] << bitCount;
                bitCount += 8;
            }
            int btype = bitBuf & 3;
            ProbeValue("PNGDECOMP_DEFLATE_BTYPE", (ulong)(uint)btype);
            bitBuf >>= 2;
            bitCount -= 2;

            ProbeBreadcrumb("PNGDECOMP_DEFLATE_FIRST_BLOCK_EXIT");
            ProbeBreadcrumb("PNGDECOMP_DEFLATE_EXIT");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressHuffmanSetupProbe(byte[] input, int inputLen) {
            ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_ENTER");

            if (input == null || inputLen < 2) {
                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                return false;
            }

            byte cmf = input[0];
            byte flg = input[1];
            if ((cmf & 0x0F) != 8 || (flg & 0x20) != 0 || ((cmf * 256 + flg) % 31) != 0) {
                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                return false;
            }

            int inPos = 2;
            int bitBuf = 0;
            int bitCount = 0;

            if (bitCount < 1) {
                if (inPos >= inputLen) {
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                    return false;
                }
                bitBuf |= input[inPos++] << bitCount;
                bitCount += 8;
            }
            bool lastBlock = (bitBuf & 1) == 1;
            _ = lastBlock;
            bitBuf >>= 1;
            bitCount--;

            if (bitCount < 2) {
                if (inPos >= inputLen) {
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                    return false;
                }
                bitBuf |= input[inPos++] << bitCount;
                bitCount += 8;
            }

            int blockType = bitBuf & 3;
            bitBuf >>= 2;
            bitCount -= 2;

            if (blockType == 1) {
                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_FIXED");
                for (int i = 0; i < LITLEN_TABLE_SIZE; i++) {
                    if (i < 144) _litLenLengths[i] = 8;
                    else if (i < 256) _litLenLengths[i] = 9;
                    else if (i < 280) _litLenLengths[i] = 7;
                    else _litLenLengths[i] = 8;
                }

                for (int i = 0; i < DIST_TABLE_SIZE; i++) {
                    _distLengths[i] = 5;
                }

                if (!BuildDecodeTable(_litLenLengths, LITLEN_TABLE_SIZE, 15, _litLenTable, 1 << 15)) {
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                    return false;
                }
                if (!BuildDecodeTable(_distLengths, DIST_TABLE_SIZE, 15, _distTable, 1 << 15)) {
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                    return false;
                }

                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_OK");
                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                return false;
            }

            if (blockType == 2) {
                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_DYNAMIC");

                while (bitCount < 14) {
                    if (inPos >= inputLen) {
                        ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                        ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                        return false;
                    }
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }

                int hlit = (bitBuf & 0x1F) + 257;
                bitBuf >>= 5;
                bitCount -= 5;

                int hdist = (bitBuf & 0x1F) + 1;
                bitBuf >>= 5;
                bitCount -= 5;

                int hclen = (bitBuf & 0x0F) + 4;
                bitBuf >>= 4;
                bitCount -= 4;

                if (hlit > 286 || hdist > 32) {
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                    return false;
                }

                int[] clOrder = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

                for (int i = 0; i < CODELENGTH_TABLE_SIZE; i++) {
                    _codeLenLengths[i] = 0;
                }

                for (int i = 0; i < hclen; i++) {
                    while (bitCount < 3) {
                        if (inPos >= inputLen) {
                            ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                            ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                            return false;
                        }
                        bitBuf |= input[inPos++] << bitCount;
                        bitCount += 8;
                    }

                    _codeLenLengths[clOrder[i]] = bitBuf & 7;
                    bitBuf >>= 3;
                    bitCount -= 3;
                }

                if (!BuildDecodeTable(_codeLenLengths, CODELENGTH_TABLE_SIZE, 7, _codeLenTable, 1 << 7)) {
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                    return false;
                }

                int totalCodes = hlit + hdist;
                for (int i = 0; i < 320; i++) {
                    _allLengths[i] = 0;
                }

                int idx = 0;
                int safetyCounter = 0;
                while (idx < totalCodes) {
                    if (++safetyCounter > totalCodes + 1000) {
                        ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                        ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                        return false;
                    }

                    while (bitCount < 15) {
                        if (inPos >= inputLen) {
                            ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                            ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                            return false;
                        }
                        bitBuf |= input[inPos++] << bitCount;
                        bitCount += 8;
                    }

                    int sym = DecodeSymbol(_codeLenTable, 7, ref bitBuf, ref bitCount);
                    if (sym < 0) {
                        ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                        ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                        return false;
                    }

                    if (sym < 16) {
                        _allLengths[idx++] = sym;
                    }
                    else if (sym == 16) {
                        while (bitCount < 2) {
                            if (inPos >= inputLen) {
                                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                                return false;
                            }
                            bitBuf |= input[inPos++] << bitCount;
                            bitCount += 8;
                        }

                        int repeat = 3 + (bitBuf & 3);
                        bitBuf >>= 2;
                        bitCount -= 2;

                        if (idx == 0) {
                            ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                            ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                            return false;
                        }

                        int prevLen = _allLengths[idx - 1];
                        for (int j = 0; j < repeat && idx < totalCodes; j++) {
                            _allLengths[idx++] = prevLen;
                        }
                    }
                    else if (sym == 17) {
                        while (bitCount < 3) {
                            if (inPos >= inputLen) {
                                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                                return false;
                            }
                            bitBuf |= input[inPos++] << bitCount;
                            bitCount += 8;
                        }

                        int repeat = 3 + (bitBuf & 7);
                        bitBuf >>= 3;
                        bitCount -= 3;

                        for (int j = 0; j < repeat && idx < totalCodes; j++) {
                            _allLengths[idx++] = 0;
                        }
                    }
                    else if (sym == 18) {
                        while (bitCount < 7) {
                            if (inPos >= inputLen) {
                                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                                return false;
                            }
                            bitBuf |= input[inPos++] << bitCount;
                            bitCount += 8;
                        }

                        int repeat = 11 + (bitBuf & 0x7F);
                        bitBuf >>= 7;
                        bitCount -= 7;

                        for (int j = 0; j < repeat && idx < totalCodes; j++) {
                            _allLengths[idx++] = 0;
                        }
                    }
                    else {
                        ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                        ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                        return false;
                    }
                }

                for (int i = 0; i < hlit && i < LITLEN_TABLE_SIZE; i++) {
                    _litLenLengths[i] = _allLengths[i];
                }
                for (int i = hlit; i < LITLEN_TABLE_SIZE; i++) {
                    _litLenLengths[i] = 0;
                }

                for (int i = 0; i < hdist && i < DIST_TABLE_SIZE; i++) {
                    _distLengths[i] = _allLengths[hlit + i];
                }
                for (int i = hdist; i < DIST_TABLE_SIZE; i++) {
                    _distLengths[i] = 0;
                }

                if (!BuildDecodeTable(_litLenLengths, LITLEN_TABLE_SIZE, 15, _litLenTable, 1 << 15)) {
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                    return false;
                }
                if (!BuildDecodeTable(_distLengths, DIST_TABLE_SIZE, 15, _distTable, 1 << 15)) {
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
                    ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                    return false;
                }

                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_OK");
                ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_BAD");
            ProbeBreadcrumb("PNGDECOMP_HUFFMAN_SETUP_EXIT");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PngDecompressInflateSmokeProbe(byte[] input, int inputLen, byte[] output, int outputMax, out int outputLen) {
            ProbeBreadcrumb("PNGDECOMP_INFLATE_ENTER");
            ProbeBreadcrumb("PNGDECOMP_INFLATE_FIRST_SYMBOL_ENTER");
            bool ok = DecompressZlib(input, inputLen, output, outputMax, out outputLen);
            if (!ok) {
                ProbeBreadcrumb("PNGDECOMP_INFLATE_EXIT");
                return false;
            }

            ProbeBreadcrumb("PNGDECOMP_INFLATE_FIRST_SYMBOL_EXIT");
            ProbeBreadcrumb("PNGDECOMP_INFLATE_PROGRESS");
            ProbeBreadcrumb("PNGDECOMP_INFLATE_OK");
            ProbeBreadcrumb("PNGDECOMP_INFLATE_EXIT");
            return true;
        }
        
        /// <summary>
        /// Decompress zlib-wrapped DEFLATE data.
        /// 
        /// Assumptions:
        /// - Input has valid zlib header (CMF, FLG bytes)
        /// - No preset dictionary (FDICT = 0)
        /// - Data is valid DEFLATE stream
        /// 
        /// Returns false on any error.
        /// </summary>
        private static bool DecompressZlib(byte[] input, int inputLen, byte[] output, int outputMax, out int outputLen) {
            return DecompressZlib(input, inputLen, output, outputMax, MAX_DECODE_ITERATIONS, out outputLen);
        }

        private static bool DecompressZlib(byte[] input, int inputLen, byte[] output, int outputMax, int maxIterations, out int outputLen) {
            outputLen = 0;
            
            if (input == null || inputLen < 6) return false;
            if (output == null || outputMax <= 0) return false;
            
            // Parse zlib header
            // CMF (Compression Method and flags)
            byte cmf = input[0];
            byte compressionMethod = (byte)(cmf & 0x0F);
            
            // FLG (Flags)
            byte flg = input[1];
            bool fdict = (flg & 0x20) != 0;
            
            // Validate
            if (compressionMethod != 8) return false;  // Must be deflate
            if (fdict) return false;                   // No preset dictionary support
            
            // Check header checksum
            if ((cmf * 256 + flg) % 31 != 0) return false;
            
            // Start deflate decode after 2-byte zlib header
            int inPos = 2;
            int outPos = 0;
            int bitBuf = 0;
            int bitCount = 0;
            int iterations = 0;
            
            bool lastBlock = false;
            
            while (!lastBlock && outPos < outputMax && inPos < inputLen) {
                // Safety: prevent infinite loops
                if (++iterations > maxIterations) return false;
                
                // Read BFINAL (1 bit)
                if (bitCount < 1) {
                    if (inPos >= inputLen) return false;
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }
                lastBlock = (bitBuf & 1) == 1;
                bitBuf >>= 1;
                bitCount--;
                
                // Read BTYPE (2 bits)
                if (bitCount < 2) {
                    if (inPos >= inputLen) return false;
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }
                int blockType = bitBuf & 3;
                bitBuf >>= 2;
                bitCount -= 2;
                
                if (blockType == 0) {
                    // Stored block (no compression)
                    // Discard remaining bits in current byte
                    bitBuf = 0;
                    bitCount = 0;
                    
                    if (inPos + 4 > inputLen) return false;
                    
                    int len = input[inPos] | (input[inPos + 1] << 8);
                    int nlen = input[inPos + 2] | (input[inPos + 3] << 8);
                    inPos += 4;
                    
                    // Verify length
                    if ((len ^ 0xFFFF) != nlen) return false;
                    
                    // Copy literal data
                    for (int i = 0; i < len; i++) {
                        if (inPos >= inputLen || outPos >= outputMax) return false;
                        output[outPos++] = input[inPos++];
                    }
                }
                else if (blockType == 1) {
                    // Fixed Huffman codes
                    bool ok = DecodeFixedHuffman(input, inputLen, output, outputMax, maxIterations, ref inPos, ref outPos, ref bitBuf, ref bitCount);
                    if (!ok) return false;
                }
                else if (blockType == 2) {
                    // Dynamic Huffman codes
                    bool ok = DecodeDynamicHuffman(input, inputLen, output, outputMax, maxIterations, ref inPos, ref outPos, ref bitBuf, ref bitCount);
                    if (!ok) return false;
                }
                else {
                    // Block type 3 is reserved/invalid
                    return false;
                }
            }
            
            outputLen = outPos;
            return true;
        }
        
        /// <summary>
        /// Decode a block using fixed Huffman codes.
        /// 
        /// Fixed codes are defined by DEFLATE spec:
        /// - Literals 0-143: 8-bit codes starting at 00110000
        /// - Literals 144-255: 9-bit codes starting at 110010000
        /// - Literals 256-279: 7-bit codes starting at 0000000
        /// - Literals 280-287: 8-bit codes starting at 11000000
        /// - Distances: All 5-bit codes
        /// </summary>
        private static bool DecodeFixedHuffman(byte[] input, int inputLen, byte[] output, int outputMax, int maxIterations,
            ref int inPos, ref int outPos, ref int bitBuf, ref int bitCount) {
            
            // Build fixed literal/length table
            for (int i = 0; i < LITLEN_TABLE_SIZE; i++) {
                if (i < 144) _litLenLengths[i] = 8;
                else if (i < 256) _litLenLengths[i] = 9;
                else if (i < 280) _litLenLengths[i] = 7;
                else _litLenLengths[i] = 8;
            }
            
            // Build fixed distance table (all 5-bit)
            for (int i = 0; i < DIST_TABLE_SIZE; i++) {
                _distLengths[i] = 5;
            }
            
            // Build decode tables
            if (!BuildDecodeTable(_litLenLengths, LITLEN_TABLE_SIZE, 15, _litLenTable, 1 << 15)) return false;
            if (!BuildDecodeTable(_distLengths, DIST_TABLE_SIZE, 15, _distTable, 1 << 15)) return false;
            
            // Decode symbols
            return DecodeHuffmanBlock(input, inputLen, output, outputMax, maxIterations, ref inPos, ref outPos, ref bitBuf, ref bitCount);
        }
        
        /// <summary>
        /// Decode a block using dynamic Huffman codes.
        /// 
        /// First reads the code length tables from the stream,
        /// then uses them to decode the actual data.
        /// </summary>
        private static bool DecodeDynamicHuffman(byte[] input, int inputLen, byte[] output, int outputMax, int maxIterations,
            ref int inPos, ref int outPos, ref int bitBuf, ref int bitCount) {
            
            // Read dynamic table header
            // Need 14 bits: HLIT(5) + HDIST(5) + HCLEN(4)
            while (bitCount < 14) {
                if (inPos >= inputLen) return false;
                bitBuf |= input[inPos++] << bitCount;
                bitCount += 8;
            }
            
            int hlit = (bitBuf & 0x1F) + 257;    // Number of literal/length codes (257-286)
            bitBuf >>= 5; bitCount -= 5;
            
            int hdist = (bitBuf & 0x1F) + 1;     // Number of distance codes (1-32)
            bitBuf >>= 5; bitCount -= 5;
            
            int hclen = (bitBuf & 0x0F) + 4;     // Number of code length codes (4-19)
            bitBuf >>= 4; bitCount -= 4;
            
            // Validate
            if (hlit > 286) return false;
            if (hdist > 32) return false;
            
            // Read code length code lengths (3 bits each)
            // These come in a specific order defined by DEFLATE spec
            int[] clOrder = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };
            
            // Clear code length lengths
            for (int i = 0; i < CODELENGTH_TABLE_SIZE; i++) {
                _codeLenLengths[i] = 0;
            }
            
            for (int i = 0; i < hclen; i++) {
                while (bitCount < 3) {
                    if (inPos >= inputLen) return false;
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }
                _codeLenLengths[clOrder[i]] = bitBuf & 7;
                bitBuf >>= 3;
                bitCount -= 3;
            }
            
            // Build code length decode table
            if (!BuildDecodeTable(_codeLenLengths, CODELENGTH_TABLE_SIZE, 7, _codeLenTable, 1 << 7)) return false;
            
            // Decode all literal/length and distance code lengths
            int totalCodes = hlit + hdist;
            
            // Clear all lengths
            for (int i = 0; i < 320; i++) {
                _allLengths[i] = 0;
            }
            
            int idx = 0;
            int safetyCounter = 0;
            
            while (idx < totalCodes) {
                if (++safetyCounter > totalCodes + 1000) return false;
                
                // Ensure we have enough bits
                while (bitCount < 15) {
                    if (inPos >= inputLen) return false;
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }
                
                int sym = DecodeSymbol(_codeLenTable, 7, ref bitBuf, ref bitCount);
                if (sym < 0) return false;
                
                if (sym < 16) {
                    // Literal code length
                    _allLengths[idx++] = sym;
                }
                else if (sym == 16) {
                    // Repeat previous length 3-6 times
                    while (bitCount < 2) {
                        if (inPos >= inputLen) return false;
                        bitBuf |= input[inPos++] << bitCount;
                        bitCount += 8;
                    }
                    int repeat = 3 + (bitBuf & 3);
                    bitBuf >>= 2;
                    bitCount -= 2;
                    
                    if (idx == 0) return false; // No previous value
                    int prevLen = _allLengths[idx - 1];
                    for (int i = 0; i < repeat && idx < totalCodes; i++) {
                        _allLengths[idx++] = prevLen;
                    }
                }
                else if (sym == 17) {
                    // Repeat 0 length 3-10 times
                    while (bitCount < 3) {
                        if (inPos >= inputLen) return false;
                        bitBuf |= input[inPos++] << bitCount;
                        bitCount += 8;
                    }
                    int repeat = 3 + (bitBuf & 7);
                    bitBuf >>= 3;
                    bitCount -= 3;
                    
                    for (int i = 0; i < repeat && idx < totalCodes; i++) {
                        _allLengths[idx++] = 0;
                    }
                }
                else if (sym == 18) {
                    // Repeat 0 length 11-138 times
                    while (bitCount < 7) {
                        if (inPos >= inputLen) return false;
                        bitBuf |= input[inPos++] << bitCount;
                        bitCount += 8;
                    }
                    int repeat = 11 + (bitBuf & 0x7F);
                    bitBuf >>= 7;
                    bitCount -= 7;
                    
                    for (int i = 0; i < repeat && idx < totalCodes; i++) {
                        _allLengths[idx++] = 0;
                    }
                }
                else {
                    return false;
                }
            }
            
            // Split into literal/length and distance code lengths
            for (int i = 0; i < hlit && i < LITLEN_TABLE_SIZE; i++) {
                _litLenLengths[i] = _allLengths[i];
            }
            for (int i = hlit; i < LITLEN_TABLE_SIZE; i++) {
                _litLenLengths[i] = 0;
            }
            
            for (int i = 0; i < hdist && i < DIST_TABLE_SIZE; i++) {
                _distLengths[i] = _allLengths[hlit + i];
            }
            for (int i = hdist; i < DIST_TABLE_SIZE; i++) {
                _distLengths[i] = 0;
            }
            
            // Build decode tables
            if (!BuildDecodeTable(_litLenLengths, LITLEN_TABLE_SIZE, 15, _litLenTable, 1 << 15)) return false;
            if (!BuildDecodeTable(_distLengths, DIST_TABLE_SIZE, 15, _distTable, 1 << 15)) return false;
            
            // Decode symbols
            return DecodeHuffmanBlock(input, inputLen, output, outputMax, maxIterations, ref inPos, ref outPos, ref bitBuf, ref bitCount);
        }
        
        /// <summary>
        /// Build a fast decode table using canonical Huffman codes.
        /// 
        /// The table is indexed by bit-reversed codes and stores:
        /// - Lower 4 bits: code length
        /// - Upper 12 bits: symbol value
        /// 
        /// This allows O(1) decode for codes up to maxBits length.
        /// </summary>
        private static bool BuildDecodeTable(int[] lengths, int count, int maxBits, ushort[] table, int tableSize) {
            // Clear bit length counts
            for (int i = 0; i < 16; i++) {
                _blCount[i] = 0;
            }
            
            // Count codes of each length
            for (int i = 0; i < count; i++) {
                int len = lengths[i];
                if (len > 0 && len <= 15) {
                    _blCount[len]++;
                }
            }
            
            // Calculate starting code for each length (canonical Huffman)
            int code = 0;
            _nextCode[0] = 0;
            for (int bits = 1; bits <= 15; bits++) {
                code = (code + _blCount[bits - 1]) << 1;
                _nextCode[bits] = code;
            }
            
            // Clear table
            for (int i = 0; i < tableSize; i++) {
                table[i] = 0;
            }
            
            // Assign codes and fill table
            for (int sym = 0; sym < count; sym++) {
                int len = lengths[sym];
                if (len == 0) continue;
                
                int c = _nextCode[len]++;
                
                // Bit-reverse the code
                int reversed = 0;
                for (int i = 0; i < len; i++) {
                    reversed = (reversed << 1) | ((c >> i) & 1);
                }
                
                // Fill all table entries that match this code
                // (codes are maxBits wide, but real code is only len bits)
                int fillCount = 1 << (maxBits - len);
                ushort entry = (ushort)((sym << 4) | len);
                
                for (int fill = 0; fill < fillCount; fill++) {
                    int idx = reversed | (fill << len);
                    if (idx < tableSize) {
                        table[idx] = entry;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Decode a single symbol using the decode table.
        /// Returns the symbol value, or -1 on error.
        /// </summary>
        private static int DecodeSymbol(ushort[] table, int maxBits, ref int bitBuf, ref int bitCount) {
            int mask = (1 << maxBits) - 1;
            ushort entry = table[bitBuf & mask];
            
            int len = entry & 0xF;
            int sym = entry >> 4;
            
            if (len == 0) return -1; // Invalid code
            
            bitBuf >>= len;
            bitCount -= len;
            
            return sym;
        }
        
        /// <summary>
        /// Decode a Huffman-compressed block using pre-built tables.
        /// Handles literal bytes and length/distance pairs.
        /// </summary>
        private static bool DecodeHuffmanBlock(byte[] input, int inputLen, byte[] output, int outputMax, int maxIterations,
            ref int inPos, ref int outPos, ref int bitBuf, ref int bitCount) {
            
            int iterations = 0;
            
            while (outPos < outputMax) {
                if (++iterations > maxIterations) return false;
                
                // Ensure we have enough bits for longest code (15 bits)
                while (bitCount < 15) {
                    if (inPos >= inputLen) {
                        // May be at end of stream - that's ok if we hit end-of-block
                        break;
                    }
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }
                
                // Decode literal/length symbol
                int sym = DecodeSymbol(_litLenTable, 15, ref bitBuf, ref bitCount);
                if (sym < 0) return false;
                
                if (sym < 256) {
                    // Literal byte
                    output[outPos++] = (byte)sym;
                }
                else if (sym == 256) {
                    // End of block
                    return true;
                }
                else {
                    // Length code (257-285)
                    int length = GetLength(sym, ref bitBuf, ref bitCount, input, inputLen, ref inPos);
                    if (length < 0) return false;
                    
                    // Decode distance
                    while (bitCount < 15) {
                        if (inPos >= inputLen) break;
                        bitBuf |= input[inPos++] << bitCount;
                        bitCount += 8;
                    }
                    
                    int distSym = DecodeSymbol(_distTable, 15, ref bitBuf, ref bitCount);
                    if (distSym < 0) return false;
                    
                    int distance = GetDistance(distSym, ref bitBuf, ref bitCount, input, inputLen, ref inPos);
                    if (distance < 0) return false;
                    
                    // Validate distance
                    if (distance > outPos) return false;
                    
                    // Copy from output buffer (LZ77 back-reference)
                    for (int i = 0; i < length; i++) {
                        if (outPos >= outputMax) return false;
                        output[outPos] = output[outPos - distance];
                        outPos++;
                    }
                }
            }
            
            return false; // Ran out of output space without end-of-block
        }
        
        /// <summary>
        /// Get length value from length code (257-285).
        /// Returns the actual length, or -1 on error.
        /// 
        /// Length codes encode lengths 3-258:
        /// - Codes 257-264: lengths 3-10 (no extra bits)
        /// - Codes 265-268: lengths 11-18 (1 extra bit)
        /// - etc.
        /// </summary>
        private static int GetLength(int code, ref int bitBuf, ref int bitCount, byte[] input, int inputLen, ref int inPos) {
            if (code < 257 || code > 285) return -1;
            
            int idx = code - 257;
            
            // Length base values (indexed by code - 257)
            // Assumption: These values are fixed by DEFLATE spec
            int baseLen;
            int extraBits;
            
            if (idx < 8) {
                baseLen = 3 + idx;
                extraBits = 0;
            }
            else if (idx < 12) {
                baseLen = 11 + ((idx - 8) << 1);
                extraBits = 1;
            }
            else if (idx < 16) {
                baseLen = 19 + ((idx - 12) << 2);
                extraBits = 2;
            }
            else if (idx < 20) {
                baseLen = 35 + ((idx - 16) << 3);
                extraBits = 3;
            }
            else if (idx < 24) {
                baseLen = 67 + ((idx - 20) << 4);
                extraBits = 4;
            }
            else if (idx < 28) {
                baseLen = 131 + ((idx - 24) << 5);
                extraBits = 5;
            }
            else {
                // Code 285 = length 258, no extra bits
                return 258;
            }
            
            if (extraBits > 0) {
                while (bitCount < extraBits) {
                    if (inPos >= inputLen) return -1;
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }
                int extra = bitBuf & ((1 << extraBits) - 1);
                bitBuf >>= extraBits;
                bitCount -= extraBits;
                return baseLen + extra;
            }
            
            return baseLen;
        }
        
        /// <summary>
        /// Get distance value from distance code (0-29).
        /// Returns the actual distance, or -1 on error.
        /// 
        /// Distance codes encode distances 1-32768:
        /// - Codes 0-3: distances 1-4 (no extra bits)
        /// - Codes 4-5: distances 5-8 (1 extra bit)
        /// - etc.
        /// </summary>
        private static int GetDistance(int code, ref int bitBuf, ref int bitCount, byte[] input, int inputLen, ref int inPos) {
            if (code < 0 || code > 29) return -1;
            
            // Distance base values (indexed by code)
            // Assumption: These values are fixed by DEFLATE spec
            int baseDist;
            int extraBits;
            
            if (code < 4) {
                baseDist = 1 + code;
                extraBits = 0;
            }
            else {
                // For codes 4-29:
                // Extra bits = (code - 2) / 2
                // Base = 1 + (1 << ((code/2) + 1)) + ((code & 1) << (code/2))
                extraBits = (code - 2) >> 1;
                int halfCode = code >> 1;
                baseDist = 1 + (1 << (halfCode + 1));
                if ((code & 1) != 0) {
                    baseDist += 1 << halfCode;
                }
            }
            
            if (extraBits > 0) {
                while (bitCount < extraBits) {
                    if (inPos >= inputLen) return -1;
                    bitBuf |= input[inPos++] << bitCount;
                    bitCount += 8;
                }
                int extra = bitBuf & ((1 << extraBits) - 1);
                bitBuf >>= extraBits;
                bitCount -= extraBits;
                return baseDist + extra;
            }
            
            return baseDist;
        }
        
        // ============================================================
        // DEBUG SELF-TEST METHODS
        // ============================================================
        
        /// <summary>
        /// Internal self-test for PNG filter reconstruction logic.
        /// 
        /// FOR DEBUG BUILDS ONLY.
        /// 
        /// This method tests all 5 PNG filter types (0-4) by:
        ///   1. Creating small synthetic scanlines (8 pixels wide, RGBA)
        ///   2. Manually applying each filter type to create "filtered" data
        ///   3. Running the reconstruction logic (ApplyFilter)
        ///   4. Verifying the reconstructed output matches the original input
        /// 
        /// Returns:
        ///   true  - All filter tests passed
        ///   false - At least one filter test failed
        /// 
        /// Test Parameters:
        ///   - Width: 8 pixels (32 bytes per scanline for RGBA)
        ///   - Height: 3 rows (tests Up/Average/Paeth with previous row)
        ///   - BPP: 4 (RGBA, 8-bit per channel)
        /// 
        /// Constraints:
        ///   - No allocations beyond fixed small arrays declared here
        ///   - No exceptions
        ///   - No external dependencies
        /// 
        /// Assumptions:
        ///   - ApplyFilter method is correctly implementing PNG filter reversal
        ///   - Test data is representative of real PNG pixel patterns
        /// </summary>
        internal static bool SelfTestFilters() {
            // ============================================================
            // TEST CONFIGURATION
            // ============================================================
            // Using 8 pixels wide (32 bytes for RGBA)
            // 3 rows to test filters that depend on previous row
            
            const int testWidth = 8;
            const int bpp = 4;  // RGBA
            const int scanlineBytes = testWidth * bpp;  // 32 bytes
            const int numRows = 3;
            
            // ============================================================
            // FIXED WORK BUFFERS (no dynamic allocation)
            // ============================================================
            // These are small fixed-size arrays for testing
            
            // Original unfiltered pixel data (what we want to recover)
            // Using stack-allocated style fixed arrays
            byte[] original0 = new byte[scanlineBytes];  // Row 0 original
            byte[] original1 = new byte[scanlineBytes];  // Row 1 original
            byte[] original2 = new byte[scanlineBytes];  // Row 2 original
            
            // Filtered data (simulating what PNG encoder would produce)
            byte[] filtered = new byte[scanlineBytes];
            
            // Reconstruction buffers
            byte[] prevScanline = new byte[scanlineBytes];
            byte[] currScanline = new byte[scanlineBytes];
            
            // Check allocations succeeded (kernel environment may fail)
            if (original0 == null) return false;
            if (original1 == null) return false;
            if (original2 == null) return false;
            if (filtered == null) return false;
            if (prevScanline == null) return false;
            if (currScanline == null) return false;
            
            // ============================================================
            // GENERATE SYNTHETIC TEST DATA
            // ============================================================
            // Create predictable but varied pixel values
            // Pattern: increasing values with some variation per channel
            
            for (int i = 0; i < scanlineBytes; i++) {
                // Row 0: base pattern
                original0[i] = (byte)((i * 7 + 13) & 0xFF);
                
                // Row 1: different pattern (for Up/Average/Paeth tests)
                original1[i] = (byte)((i * 11 + 29) & 0xFF);
                
                // Row 2: another pattern
                original2[i] = (byte)((i * 5 + 47) & 0xFF);
            }
            
            // ============================================================
            // TEST FILTER TYPE 0: NONE
            // ============================================================
            // Filter 0 = no transformation, data passes through unchanged
            
            // Clear previous scanline (first row has no previous)
            for (int i = 0; i < scanlineBytes; i++) {
                prevScanline[i] = 0;
            }
            
            // For filter 0, filtered data == original data
            for (int i = 0; i < scanlineBytes; i++) {
                filtered[i] = original0[i];
                currScanline[i] = filtered[i];
            }
            
            // Apply reconstruction (should be no-op for filter 0)
            if (!ApplyFilter(0, currScanline, prevScanline, scanlineBytes, bpp)) {
                return false;
            }
            
            // Verify reconstruction matches original
            for (int i = 0; i < scanlineBytes; i++) {
                if (currScanline[i] != original0[i]) {
                    return false;  // Filter 0 test failed
                }
            }
            
            // ============================================================
            // TEST FILTER TYPE 1: SUB
            // ============================================================
            // Filter 1: Filt(x) = Orig(x) - Orig(x - bpp)
            // Recon:    Orig(x) = Filt(x) + Recon(x - bpp)
            
            // Apply Sub filter to create filtered data
            for (int i = 0; i < scanlineBytes; i++) {
                if (i < bpp) {
                    // First bpp bytes: no left neighbor, filter value = original
                    filtered[i] = original0[i];
                } else {
                    // Subsequent bytes: difference from left neighbor
                    filtered[i] = (byte)(original0[i] - original0[i - bpp]);
                }
                currScanline[i] = filtered[i];
            }
            
            // Apply reconstruction
            if (!ApplyFilter(1, currScanline, prevScanline, scanlineBytes, bpp)) {
                return false;
            }
            
            // Verify reconstruction matches original
            for (int i = 0; i < scanlineBytes; i++) {
                if (currScanline[i] != original0[i]) {
                    return false;  // Filter 1 (Sub) test failed
                }
            }
            
            // ============================================================
            // TEST FILTER TYPE 2: UP
            // ============================================================
            // Filter 2: Filt(x) = Orig(x) - Orig_prev(x)
            // Recon:    Orig(x) = Filt(x) + Recon_prev(x)
            // 
            // This test uses row 1 with row 0 as previous
            
            // Set previous scanline to row 0 (already reconstructed)
            for (int i = 0; i < scanlineBytes; i++) {
                prevScanline[i] = original0[i];
            }
            
            // Apply Up filter to create filtered data
            for (int i = 0; i < scanlineBytes; i++) {
                // Difference from byte directly above
                filtered[i] = (byte)(original1[i] - original0[i]);
                currScanline[i] = filtered[i];
            }
            
            // Apply reconstruction
            if (!ApplyFilter(2, currScanline, prevScanline, scanlineBytes, bpp)) {
                return false;
            }
            
            // Verify reconstruction matches original row 1
            for (int i = 0; i < scanlineBytes; i++) {
                if (currScanline[i] != original1[i]) {
                    return false;  // Filter 2 (Up) test failed
                }
            }
            
            // ============================================================
            // TEST FILTER TYPE 3: AVERAGE
            // ============================================================
            // Filter 3: Filt(x) = Orig(x) - floor((Orig(x-bpp) + Orig_prev(x)) / 2)
            // Recon:    Orig(x) = Filt(x) + floor((Recon(x-bpp) + Recon_prev(x)) / 2)
            //
            // This test uses row 2 with row 1 as previous
            
            // Set previous scanline to row 1
            for (int i = 0; i < scanlineBytes; i++) {
                prevScanline[i] = original1[i];
            }
            
            // Apply Average filter to create filtered data
            // We need to track what the reconstructed values would be as we go
            // because the filter depends on already-filtered values to the left
            byte[] tempRecon = new byte[scanlineBytes];
            if (tempRecon == null) return false;
            
            for (int i = 0; i < scanlineBytes; i++) {
                int left = (i >= bpp) ? original2[i - bpp] : 0;
                int above = original1[i];
                int avg = (left + above) >> 1;
                filtered[i] = (byte)(original2[i] - avg);
                currScanline[i] = filtered[i];
            }
            
            // Apply reconstruction
            if (!ApplyFilter(3, currScanline, prevScanline, scanlineBytes, bpp)) {
                return false;
            }
            
            // Verify reconstruction matches original row 2
            for (int i = 0; i < scanlineBytes; i++) {
                if (currScanline[i] != original2[i]) {
                    return false;  // Filter 3 (Average) test failed
                }
            }
            
            // ============================================================
            // TEST FILTER TYPE 4: PAETH
            // ============================================================
            // Filter 4: Filt(x) = Orig(x) - PaethPredictor(Orig(x-bpp), Orig_prev(x), Orig_prev(x-bpp))
            // Recon:    Orig(x) = Filt(x) + PaethPredictor(Recon(x-bpp), Recon_prev(x), Recon_prev(x-bpp))
            //
            // Using row 2 with row 1 as previous (same setup as Average test)
            
            // prevScanline is still row 1 from Average test
            
            // Apply Paeth filter to create filtered data
            for (int i = 0; i < scanlineBytes; i++) {
                int a = (i >= bpp) ? original2[i - bpp] : 0;  // Left
                int b = original1[i];                          // Above
                int c = (i >= bpp) ? original1[i - bpp] : 0;  // Upper-left
                
                // Paeth predictor calculation
                int predictor = SelfTestPaethPredictor(a, b, c);
                
                filtered[i] = (byte)(original2[i] - predictor);
                currScanline[i] = filtered[i];
            }
            
            // Apply reconstruction
            if (!ApplyFilter(4, currScanline, prevScanline, scanlineBytes, bpp)) {
                return false;
            }
            
            // Verify reconstruction matches original row 2
            for (int i = 0; i < scanlineBytes; i++) {
                if (currScanline[i] != original2[i]) {
                    return false;  // Filter 4 (Paeth) test failed
                }
            }
            
            // ============================================================
            // TEST INVALID FILTER TYPE
            // ============================================================
            // Filter types > 4 should fail
            
            // Reset currScanline
            for (int i = 0; i < scanlineBytes; i++) {
                currScanline[i] = 0;
            }
            
            // Filter type 5 should return false
            if (ApplyFilter(5, currScanline, prevScanline, scanlineBytes, bpp)) {
                return false;  // Should have rejected invalid filter
            }
            
            // Filter type 255 should return false
            if (ApplyFilter(255, currScanline, prevScanline, scanlineBytes, bpp)) {
                return false;  // Should have rejected invalid filter
            }
            
            // ============================================================
            // ALL TESTS PASSED
            // ============================================================
            return true;
        }
        
        /// <summary>
        /// Paeth predictor calculation for self-test.
        /// 
        /// This is a standalone implementation used only for generating
        /// test data. It matches the PNG specification exactly.
        /// 
        /// Parameters:
        ///   a - Left neighbor (or 0 if at left edge)
        ///   b - Above neighbor (or 0 if at top edge)
        ///   c - Upper-left neighbor (or 0 if at top-left corner)
        /// 
        /// Returns:
        ///   The predicted value (a, b, or c) based on which is closest to p = a + b - c
        /// 
        /// Assumptions:
        ///   - a, b, c are byte values (0-255)
        ///   - No overflow concerns since we use int arithmetic
        /// </summary>
        private static int SelfTestPaethPredictor(int a, int b, int c) {
            // p = a + b - c
            int p = a + b - c;
            
            // Calculate absolute differences
            int pa = p - a;
            if (pa < 0) pa = -pa;
            
            int pb = p - b;
            if (pb < 0) pb = -pb;
            
            int pc = p - c;
            if (pc < 0) pc = -pc;
            
            // Return nearest neighbor
            if (pa <= pb && pa <= pc) {
                return a;
            } else if (pb <= pc) {
                return b;
            } else {
                return c;
            }
        }
        
        /// <summary>
        /// Extended self-test that runs multiple rounds with different data patterns.
        /// 
        /// FOR DEBUG BUILDS ONLY.
        /// 
        /// This method runs the basic filter self-test multiple times with
        /// additional edge case patterns to increase test coverage.
        /// 
        /// Returns:
        ///   true  - All extended tests passed
        ///   false - At least one test failed
        /// 
        /// Test patterns include:
        ///   - Standard varied data (SelfTestFilters)
        ///   - All zeros (edge case)
        ///   - All 0xFF (edge case)
        ///   - Alternating pattern
        ///   - Gradient pattern
        /// 
        /// Constraints:
        ///   - Same as SelfTestFilters
        /// </summary>
        internal static bool SelfTestFiltersExtended() {
            // Run basic self-test first
            if (!SelfTestFilters()) {
                return false;
            }
            
            // ============================================================
            // ADDITIONAL EDGE CASE TESTS
            // ============================================================
            
            const int testWidth = 8;
            const int bpp = 4;
            const int scanlineBytes = testWidth * bpp;
            
            // Work buffers
            byte[] original = new byte[scanlineBytes];
            byte[] prevRow = new byte[scanlineBytes];
            byte[] filtered = new byte[scanlineBytes];
            byte[] reconstructed = new byte[scanlineBytes];
            
            if (original == null) return false;
            if (prevRow == null) return false;
            if (filtered == null) return false;
            if (reconstructed == null) return false;
            
            // ============================================================
            // TEST: All zeros
            // ============================================================
            
            for (int i = 0; i < scanlineBytes; i++) {
                original[i] = 0;
                prevRow[i] = 0;
            }
            
            // Test all filter types with all-zero data
            for (byte filterType = 0; filterType <= 4; filterType++) {
                if (!SelfTestSingleFilter(filterType, original, prevRow, filtered, reconstructed, scanlineBytes, bpp)) {
                    return false;
                }
            }
            
            // ============================================================
            // TEST: All 0xFF
            // ============================================================
            
            for (int i = 0; i < scanlineBytes; i++) {
                original[i] = 0xFF;
                prevRow[i] = 0xFF;
            }
            
            for (byte filterType = 0; filterType <= 4; filterType++) {
                if (!SelfTestSingleFilter(filterType, original, prevRow, filtered, reconstructed, scanlineBytes, bpp)) {
                    return false;
                }
            }
            
            // ============================================================
            // TEST: Alternating 0x00 / 0xFF
            // ============================================================
            
            for (int i = 0; i < scanlineBytes; i++) {
                original[i] = (i & 1) == 0 ? (byte)0x00 : (byte)0xFF;
                prevRow[i] = (i & 1) == 0 ? (byte)0xFF : (byte)0x00;  // Inverted
            }
            
            for (byte filterType = 0; filterType <= 4; filterType++) {
                if (!SelfTestSingleFilter(filterType, original, prevRow, filtered, reconstructed, scanlineBytes, bpp)) {
                    return false;
                }
            }
            
            // ============================================================
            // TEST: Linear gradient
            // ============================================================
            
            for (int i = 0; i < scanlineBytes; i++) {
                original[i] = (byte)((i * 255) / (scanlineBytes - 1));
                prevRow[i] = (byte)(255 - ((i * 255) / (scanlineBytes - 1)));  // Inverse gradient
            }
            
            for (byte filterType = 0; filterType <= 4; filterType++) {
                if (!SelfTestSingleFilter(filterType, original, prevRow, filtered, reconstructed, scanlineBytes, bpp)) {
                    return false;
                }
            }
            
            // ============================================================
            // TEST: Per-channel patterns (RGBA specific)
            // ============================================================
            // Each channel has a different pattern
            
            for (int pixel = 0; pixel < testWidth; pixel++) {
                int offset = pixel * 4;
                original[offset + 0] = (byte)(pixel * 30);        // R: stepping
                original[offset + 1] = (byte)(255 - pixel * 30);  // G: inverse stepping
                original[offset + 2] = (byte)((pixel & 1) * 255); // B: alternating
                original[offset + 3] = (byte)0xFF;                // A: fully opaque
                
                prevRow[offset + 0] = (byte)((pixel + 1) * 25);
                prevRow[offset + 1] = (byte)(200 - pixel * 20);
                prevRow[offset + 2] = (byte)((pixel & 1) * 128);
                prevRow[offset + 3] = (byte)0x80;
            }
            
            for (byte filterType = 0; filterType <= 4; filterType++) {
                if (!SelfTestSingleFilter(filterType, original, prevRow, filtered, reconstructed, scanlineBytes, bpp)) {
                    return false;
                }
            }
            
            // All extended tests passed
            return true;
        }
        
        /// <summary>
        /// Test a single filter type with provided data.
        /// 
        /// Helper for extended self-tests. Applies the specified filter,
        /// reconstructs, and verifies the result matches the original.
        /// 
        /// Parameters:
        ///   filterType    - PNG filter type (0-4)
        ///   original      - Original unfiltered scanline data
        ///   prevRow       - Previous row data (for Up/Average/Paeth)
        ///   filtered      - Work buffer for filtered data
        ///   reconstructed - Work buffer for reconstructed data
        ///   length        - Scanline length in bytes
        ///   bpp           - Bytes per pixel (4 for RGBA)
        /// 
        /// Returns:
        ///   true  - Reconstruction matches original
        ///   false - Reconstruction failed or mismatched
        /// 
        /// Assumptions:
        ///   - All buffers are at least 'length' bytes
        ///   - filterType is 0-4
        /// </summary>
        private static bool SelfTestSingleFilter(
            byte filterType,
            byte[] original,
            byte[] prevRow,
            byte[] filtered,
            byte[] reconstructed,
            int length,
            int bpp) {
            
            // ============================================================
            // APPLY FILTER (simulating PNG encoder)
            // ============================================================
            
            if (filterType == 0) {
                // None: filtered = original
                for (int i = 0; i < length; i++) {
                    filtered[i] = original[i];
                }
            }
            else if (filterType == 1) {
                // Sub: filtered = original - left
                for (int i = 0; i < length; i++) {
                    int left = (i >= bpp) ? original[i - bpp] : 0;
                    filtered[i] = (byte)(original[i] - left);
                }
            }
            else if (filterType == 2) {
                // Up: filtered = original - above
                for (int i = 0; i < length; i++) {
                    filtered[i] = (byte)(original[i] - prevRow[i]);
                }
            }
            else if (filterType == 3) {
                // Average: filtered = original - floor((left + above) / 2)
                for (int i = 0; i < length; i++) {
                    int left = (i >= bpp) ? original[i - bpp] : 0;
                    int above = prevRow[i];
                    int avg = (left + above) >> 1;
                    filtered[i] = (byte)(original[i] - avg);
                }
            }
            else if (filterType == 4) {
                // Paeth: filtered = original - PaethPredictor(left, above, upper-left)
                for (int i = 0; i < length; i++) {
                    int a = (i >= bpp) ? original[i - bpp] : 0;  // Left
                    int b = prevRow[i];                          // Above
                    int c = (i >= bpp) ? prevRow[i - bpp] : 0;   // Upper-left
                    int predictor = SelfTestPaethPredictor(a, b, c);
                    filtered[i] = (byte)(original[i] - predictor);
                }
            }
            else {
                return false;  // Invalid filter type
            }
            
            // ============================================================
            // COPY FILTERED TO RECONSTRUCTED BUFFER
            // ============================================================
            
            for (int i = 0; i < length; i++) {
                reconstructed[i] = filtered[i];
            }
            
            // ============================================================
            // APPLY RECONSTRUCTION (ApplyFilter)
            // ============================================================
            
            if (!ApplyFilter(filterType, reconstructed, prevRow, length, bpp)) {
                return false;
            }
            
            // ============================================================
            // VERIFY RECONSTRUCTION MATCHES ORIGINAL
            // ============================================================
            
            for (int i = 0; i < length; i++) {
                if (reconstructed[i] != original[i]) {
                    return false;  // Mismatch at position i
                }
            }
            
            return true;
        }
    }
}
