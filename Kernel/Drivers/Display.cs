using guideXOS.Graph;
using guideXOS.FS;
using Internal.Runtime.CompilerServices;

namespace guideXOS.Kernel.Drivers {
    public struct Resolution {
        public ushort Width;
        public ushort Height;
        public Resolution(ushort w, ushort h) { Width = w; Height = h; }
        public override string ToString() => $"{Width}x{Height}";
    }

    public static unsafe class DisplayManager {
        // Cache SVGA device if available
        static VMWareSVGAII _svga;
        static bool _svgaChecked;

        public static Resolution Current => new Resolution(Framebuffer.Width, Framebuffer.Height);

        public static Resolution[] AvailableResolutions { get; } = new Resolution[] {
            new(800, 600), new(1024, 768), new(1152, 864), new(1280, 720), new(1280, 768), new(1280, 800),
            new(1366, 768), new(1440, 900), new(1600, 900), new(1680, 1050), new(1920, 1080)
        };

#if UEFI_DIAGNOSTIC_BM1
        // The BM3B NativeAOT layout puts _svga in the first GC-static slot
        // and the AvailableResolutions auto-property backing field in the
        // next slot. Taking these addresses does not read either reference.
        internal static unsafe void GetAvailableResolutionsStaticAddresses(
                out ulong staticBaseAddress, out ulong fieldAddress) {
            ulong firstReferenceSlot =
                (ulong)(nuint)Unsafe.AsPointer(ref _svga);
            staticBaseAddress = firstReferenceSlot >= (ulong)sizeof(nint)
                ? firstReferenceSlot - (ulong)sizeof(nint) : ~0UL;
            fieldAddress = firstReferenceSlot <=
                    ~0UL - (ulong)sizeof(nint)
                ? firstReferenceSlot + (ulong)sizeof(nint) : ~0UL;
        }
#endif

        static bool EnsureSvga() {
            if (_svgaChecked) return _svga != null;
            _svgaChecked = true;
            try {
                // Gate creation behind PCI presence to avoid faults on non-VMware hardware
                var dev = PCI.GetDevice(0x15AD, 0x0405);
                if (dev == null) { _svga = null; return false; }
                _svga = new VMWareSVGAII();
                if (_svga == null || _svga.Video_Memory == null) { _svga = null; return false; }
                return true;
            } catch { _svga = null; return false; }
        }

        public static bool TrySetResolution(ushort width, ushort height) {
            // Currently only supported on VMWare SVGA II path
            if (!EnsureSvga()) return false;
            try {
                // Pause scheduling to avoid other threads hitting framebuffer during mode set
                guideXOS.Misc.ThreadPool.Lock();

                _svga.SetMode(width, height, 32);
                if (_svga.Video_Memory == null) { guideXOS.Misc.ThreadPool.UnLock(); return false; }
                Framebuffer.Initialize(width, height, _svga.Video_Memory);
                if (Framebuffer.Graphics != null && Framebuffer.FirstBuffer != null)
                    Framebuffer.Graphics.VideoMemory = Framebuffer.FirstBuffer;

                guideXOS.Misc.ThreadPool.UnLock();
                return true;
            } catch {
                try { guideXOS.Misc.ThreadPool.UnLock(); } catch { }
                return false;
            }
        }

        const string ConfigFile = "display.cfg";

        static byte[] ToASCII(string s) {
            byte[] buffer = new byte[s.Length];
            for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)s[i];
            return buffer;
        }

        public static void SaveResolution(Resolution res) {
            try {
                var s = res.ToString();
                var bytes = ToASCII(s);
                File.WriteAllBytes(ConfigFile, bytes);
                s.Dispose();
            } catch { }
        }

        static bool TryParseUShort(string s, out ushort value) {
            value = 0;
            if (s == null || s.Length == 0) return false;
            int acc = 0;
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                if (c < '0' || c > '9') return false;
                acc = acc * 10 + (c - '0');
                if (acc > 65535) return false;
            }
            value = (ushort)acc;
            return true;
        }

        public static bool TryLoadSaved(out Resolution res) {
            res = default;
            try {
                if (!File.Exists(ConfigFile)) return false;
                var bytes = File.ReadAllBytes(ConfigFile);
                // Parse bytes as ASCII: [digits] 'x' [digits]
                int i = 0; int len = bytes.Length;
                int accW = 0; int accH = 0; bool seenX = false; bool haveW = false; bool haveH = false;
                while (i < len) {
                    byte b = bytes[i++];
                    if (b == (byte)'x' || b == (byte)'X') {
                        if (!haveW) { haveW = true; seenX = true; }
                        else break;
                    } else if (b >= (byte)'0' && b <= (byte)'9') {
                        if (!seenX) {
                            accW = accW * 10 + (b - (byte)'0');
                        } else {
                            accH = accH * 10 + (b - (byte)'0');
                            haveH = true;
                        }
                    } else {
                        // ignore other chars
                    }
                }
                if (haveW && haveH && accW > 0 && accH > 0 && accW <= 65535 && accH <= 65535) {
                    res = new Resolution((ushort)accW, (ushort)accH);
                    return true;
                }
            } catch { }
            return false;
        }

        public static bool ApplySavedResolution() {
            // Only attempt if SVGA is available to avoid touching files very early on non-VMware paths
            if (!EnsureSvga()) return false;
            if (TryLoadSaved(out var res)) {
                return TrySetResolution(res.Width, res.Height);
            }
            return false;
        }
    }
}
