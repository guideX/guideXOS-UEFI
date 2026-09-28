using guideXOS;
using System.Drawing;
using System.Runtime.InteropServices;
using guideXOS.Kernel.Drivers;

namespace guideXOS.Misc {
    // ============================================================================
    // LEGACY PNG LOADER - UNSAFE FOR UEFI ENVIRONMENT
    // ============================================================================
    // WARNING: This class uses native P/Invoke calls (lodepng_decode_memory) which
    // are NOT available after ExitBootServices in UEFI boot mode.
    //
    // UEFI CONSTRAINT: After ExitBootServices(), NO native library calls are allowed.
    // The UEFI runtime environment does not support dynamic linking or native code
    // execution beyond what the kernel itself provides.
    //
    // SAFE ALTERNATIVE: Use PngLoader (Kernel\Misc\PngLoader.cs) which is a pure
    // managed implementation that works in both legacy and UEFI boot modes.
    //
    // For boot-time PNG loading (before ExitBootServices), use PngAssetPreloader
    // to decode PNGs while UEFI services are still available, then retrieve the
    // decoded RGBA data after boot.
    //
    // This class is retained for legacy (GRUB/Multiboot) boot compatibility only.
    // In UEFI mode, this class will fall back to LodePNG (managed) which may have
    // stability issues. Prefer PngLoader for all new code.
    // ============================================================================
    [System.Obsolete("Use PngLoader for UEFI-safe PNG decoding. This class uses native P/Invoke which is unavailable after ExitBootServices.")]
    public unsafe class PNG : Image {
        public enum LodePNGColorType { LCT_GREY = 0, LCT_RGB = 2, LCT_PALETTE = 3, LCT_GREY_ALPHA = 4, LCT_RGBA = 6 }

        private static void BuildFallback(out int[] data, out int w, out int h) {
            w = 8; h = 8; data = new int[w * h];
            // simple checker pattern (magenta/black) to indicate failure
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    bool c = ((x ^ y) & 1) == 0;
                    data[y * w + x] = c ? unchecked((int)0xFFFF00FF) : unchecked((int)0xFF000000);
                }
            }
        }

        private static bool HasPngHeader(byte[] file) {
            if (file == null || file.Length < 33) return false; // need at least signature+IHDR header
            // 8-byte PNG signature
            if (file[0] != 0x89 || file[1] != 0x50 || file[2] != 0x4E || file[3] != 0x47 || file[4] != 0x0D || file[5] != 0x0A || file[6] != 0x1A || file[7] != 0x0A) return false;
            // Expect first chunk to be IHDR of length 13
            uint len = ((uint)file[8] << 24) | ((uint)file[9] << 16) | ((uint)file[10] << 8) | file[11];
            if (len != 13) return false;
            if (file[12] != (byte)'I' || file[13] != (byte)'H' || file[14] != (byte)'D' || file[15] != (byte)'R') return false;
            return true;
        }

        private static bool TryGetIHDRDims(byte[] file, out uint w, out uint h) {
            w = 0; h = 0; if (!HasPngHeader(file)) return false;
            int off = 16; // IHDR width/height start
            w = ((uint)file[off] << 24) | ((uint)file[off + 1] << 16) | ((uint)file[off + 2] << 8) | file[off + 3];
            h = ((uint)file[off + 4] << 24) | ((uint)file[off + 5] << 16) | ((uint)file[off + 6] << 8) | file[off + 7];
            return w != 0 && h != 0;
        }

        public PNG(byte[] file, LodePNGColorType type = LodePNGColorType.LCT_RGBA, uint bitDepth = 8) {
            // ============================================================
            // LEGACY PNG LOADING PATH
            // ============================================================
            // UEFI WARNING: This constructor may use unsafe native calls
            // in legacy boot mode. For UEFI boot, it delegates to LodePNG
            // (managed), but PngLoader is the preferred safe alternative.
            //
            // For new code, use:
            //   if (PngLoader.Load(data, out Image result)) { ... }
            // ============================================================
            
            // RAW serial output to bypass any BootConsole issues
            Native.Out8(0x3F8, (byte)'P');
            Native.Out8(0x3F8, (byte)'N');
            Native.Out8(0x3F8, (byte)'G');
            Native.Out8(0x3F8, (byte)'1');
            Native.Out8(0x3F8, (byte)'\n');
            
            // Validate input first
            if (file == null || file.Length < 33) {
                Native.Out8(0x3F8, (byte)'N');
                Native.Out8(0x3F8, (byte)'U');
                Native.Out8(0x3F8, (byte)'L');
                Native.Out8(0x3F8, (byte)'\n');
                BuildFallback(out RawData, out int fw, out int fh);
                Width = fw; Height = fh; Bpp = 4;
                return;
            }
            
            Native.Out8(0x3F8, (byte)'P');
            Native.Out8(0x3F8, (byte)'N');
            Native.Out8(0x3F8, (byte)'G');
            Native.Out8(0x3F8, (byte)'2');
            Native.Out8(0x3F8, (byte)'\n');
            
            // Pre-validate PNG header and get dimensions
            bool canDecode = TryGetIHDRDims(file, out uint ihdrW, out uint ihdrH);
            if (!canDecode) {
                Native.Out8(0x3F8, (byte)'H');
                Native.Out8(0x3F8, (byte)'D');
                Native.Out8(0x3F8, (byte)'R');
                Native.Out8(0x3F8, (byte)'X');
                Native.Out8(0x3F8, (byte)'\n');
                BuildFallback(out RawData, out int fw, out int fh);
                Width = fw; Height = fh; Bpp = 4;
                return;
            }
            
            Native.Out8(0x3F8, (byte)'P');
            Native.Out8(0x3F8, (byte)'N');
            Native.Out8(0x3F8, (byte)'G');
            Native.Out8(0x3F8, (byte)'3');
            Native.Out8(0x3F8, (byte)'\n');
            
            // UEFI mode: Use managed LodePNG decoder (no native code required)
            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                Native.Out8(0x3F8, (byte)'U');
                Native.Out8(0x3F8, (byte)'E');
                Native.Out8(0x3F8, (byte)'F');
                Native.Out8(0x3F8, (byte)'I');
                Native.Out8(0x3F8, (byte)'\n');
                
                try {
                    Native.Out8(0x3F8, (byte)'L');
                    Native.Out8(0x3F8, (byte)'P');
                    Native.Out8(0x3F8, (byte)'N');
                    Native.Out8(0x3F8, (byte)'G');
                    Native.Out8(0x3F8, (byte)'\n');
                    
                    var decoded = new LodePNG(file);
                    
                    Native.Out8(0x3F8, (byte)'D');
                    Native.Out8(0x3F8, (byte)'O');
                    Native.Out8(0x3F8, (byte)'N');
                    Native.Out8(0x3F8, (byte)'E');
                    Native.Out8(0x3F8, (byte)'\n');
                    
                    if (decoded.RawData != null && decoded.Width > 0 && decoded.Height > 0) {
                        Width = decoded.Width;
                        Height = decoded.Height;
                        Bpp = decoded.Bpp;
                        RawData = decoded.RawData;
                        Native.Out8(0x3F8, (byte)'O');
                        Native.Out8(0x3F8, (byte)'K');
                        Native.Out8(0x3F8, (byte)'\n');
                        return;
                    }
                    Native.Out8(0x3F8, (byte)'B');
                    Native.Out8(0x3F8, (byte)'A');
                    Native.Out8(0x3F8, (byte)'D');
                    Native.Out8(0x3F8, (byte)'\n');
                } catch {
                    Native.Out8(0x3F8, (byte)'E');
                    Native.Out8(0x3F8, (byte)'X');
                    Native.Out8(0x3F8, (byte)'C');
                    Native.Out8(0x3F8, (byte)'\n');
                }
                
                // Fallback to placeholder if decode fails
                Native.Out8(0x3F8, (byte)'F');
                Native.Out8(0x3F8, (byte)'B');
                Native.Out8(0x3F8, (byte)'K');
                Native.Out8(0x3F8, (byte)'\n');
                BuildFallback(out RawData, out int fw, out int fh);
                Width = fw; Height = fh; Bpp = 4;
                return;
            }
            
            // Legacy mode: Use native lodepng
            ulong pixelCount = (ulong)ihdrW * (ulong)ihdrH;
            if (pixelCount == 0 || pixelCount > (ulong)int.MaxValue || pixelCount * 4UL > Allocator.MemorySize / 2) {
                BuildFallback(out RawData, out int fw, out int fh);
                Width = fw; Height = fh; Bpp = 4;
                return;
            }

            fixed (byte* p = file) {
                uint* decoded; uint w, h;
                lodepng_decode_memory(out decoded, out w, out h, p, file.Length, type, bitDepth);
                if (decoded == null || w == 0 || h == 0 || w > 8192 || h > 8192) {
                    BuildFallback(out RawData, out int fw, out int fh);
                    Width = fw; Height = fh; Bpp = 4;
                    return;
                }
                ulong pc = (ulong)w * (ulong)h;
                if (pc > (ulong)int.MaxValue || pc * 4UL > Allocator.MemorySize / 2) {
                    Allocator.Free((System.IntPtr)decoded, "PNG");
                    BuildFallback(out RawData, out int fw2, out int fh2);
                    Width = fw2; Height = fh2; Bpp = 4;
                    return;
                }
                RawData = new int[pc];
                for (uint y2 = 0; y2 < h; y2++) {
                    uint rowOff = y2 * w;
                    for (uint x2 = 0; x2 < w; x2++) {
                        uint px = decoded[rowOff + x2];
                        RawData[(int)(rowOff + x2)] = (int)((px & 0xFF000000) | (NETv4.SwapLeftRight(px & 0x00FFFFFF)) >> 8);
                    }
                }
                Allocator.Free((System.IntPtr)decoded, "PNG");
                Width = (int)w; Height = (int)h; Bpp = 4;
            }
        }

        [DllImport("*")]
        public static extern void lodepng_decode_memory(out uint* _out, out uint w, out uint h, byte* _in, int insize, LodePNGColorType colortype, uint bitdepth);
    }
}
