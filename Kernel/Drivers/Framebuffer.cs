using guideXOS.Graph;
using guideXOS.Misc;
using System.Windows.Forms;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// A framebuffer (frame buffer, or sometimes framestore) is a portion of random-access memory (RAM) containing a bitmap that drives a video display. It is a memory buffer containing data representing all the pixels in a complete video frame. Modern video cards contain framebuffer circuitry in their cores
    /// </summary>
    public static unsafe class Framebuffer {
        /// <summary>
        /// Width
        /// </summary>
        public static ushort Width;
        /// <summary>
        /// Height
        /// </summary>
        public static ushort Height;
        /// <summary>
        /// Original Width/Height from UEFI boot info. These are used to recover
        /// dimensions if managed static fields are cleared during early UEFI boot.
        /// </summary>
        public static ushort OriginalWidth;
        public static ushort OriginalHeight;
        public static UefiBootInfo* OriginalBootInfo;
        /// <summary>
        /// Video Memory - stored as a plain static field (NOT an auto-property)
        /// so the raw pointer value survives UEFI managed reference corruption.
        /// </summary>
        public static uint* VideoMemory;
        /// <summary>
        /// Original framebuffer physical address set during Initialize().
        /// This NEVER changes and is used by EnsureGraphics() to restore
        /// VideoMemory if it gets corrupted in UEFI mode.
        /// </summary>
        public static uint* OriginalVideoMemory;
        public static FramebufferSurfaceLayout GopSurfaceLayout;
        public static bool HasGopSurfaceLayout;
        /// <summary>
        /// First Buffer
        /// </summary>
        public static uint* FirstBuffer;
        /// <summary>
        /// Second Buffer
        /// </summary>
        public static uint* SecondBuffer;
        /// <summary>
        /// Graphics
        /// </summary>
        public static Graphics Graphics;
        /// <summary>
        /// Triple Buffered
        /// </summary>
        static bool _TripleBuffered = false;
        /// <summary>
        /// Since you enabled TripleBuffered you have to call Framebuffer.Graphics.Update() in order to make it display
        /// </summary>
        public static bool TripleBuffered {
            get {
                return _TripleBuffered;
            }
            set {
                if (Graphics == null) return;
                if (VideoMemory == null) return;
                if (Width == 0 || Height == 0) return;
                if (value && (FirstBuffer == null || SecondBuffer == null)) return;
                if (_TripleBuffered == value) return;

                Graphics.VideoMemory = value ? FirstBuffer : VideoMemory;
                if (Graphics.VideoMemory == null) return;
                Graphics.SetPhysicalSurfaceActive(!value && HasGopSurfaceLayout);
                Graphics.Clear(0x0);
                _TripleBuffered = value;
                if (!_TripleBuffered) {
                    Console.Clear();
                }
            }
        }

        /// <summary>
        /// Ensures the Graphics object exists and points at the real framebuffer.
        /// In UEFI mode, static managed references (like Graphics) can be zeroed
        /// and VideoMemory can be corrupted to a heap address. This method uses
        /// OriginalVideoMemory (set once during Initialize) as the authoritative
        /// framebuffer address.
        /// </summary>
        public static void EnsureGraphics() {
            RecoverUefiState();
            // STEP 1: If OriginalVideoMemory is set, always trust it over VideoMemory.
            // VideoMemory can get corrupted in UEFI mode; OriginalVideoMemory never changes.
            if ((ulong)OriginalVideoMemory != 0) {
                if ((ulong)VideoMemory != (ulong)OriginalVideoMemory) {
                    VideoMemory = OriginalVideoMemory;
                }
            }
            // Restore cached dimensions if the managed Graphics object survived but
            // its width/height fields were cleared during UEFI state loss.
            if (Graphics != null) {
                if (Width != 0 && Graphics.Width != Width) {
                    Graphics.Width = Width;
                }
                if (Height != 0 && Graphics.Height != Height) {
                    Graphics.Height = Height;
                }
            }
            // When buffering is enabled, Graphics must continue to target the
            // back buffer. VideoMemory remains the firmware-owned scanout
            // buffer and is only touched by Update().
            uint* renderMemory = TripleBuffered && FirstBuffer != null
                ? FirstBuffer : VideoMemory;

            // STEP 2: If Graphics survived and already points at the active
            // render buffer, we're done.
            if (Graphics != null && (ulong)Graphics.VideoMemory == (ulong)renderMemory) {
                Graphics.SetPhysicalSurfaceActive(
                    !TripleBuffered && HasGopSurfaceLayout);
                return;
            }
            // STEP 3: If Graphics exists but points elsewhere, fix it.
            if (Graphics != null && (ulong)renderMemory != 0) {
                Graphics.VideoMemory = renderMemory;
                Graphics.SetPhysicalSurfaceActive(
                    !TripleBuffered && HasGopSurfaceLayout);
                if (Width != 0 && Graphics.Width != Width) {
                    Graphics.Width = Width;
                }
                if (Height != 0 && Graphics.Height != Height) {
                    Graphics.Height = Height;
                }
                return;
            }
            // STEP 4: Recreate Graphics from scratch.
            if ((ulong)renderMemory == 0 || Width == 0 || Height == 0) {
                return;
            }
            Graphics = new Graphics(Width, Height, renderMemory);
            if (HasGopSurfaceLayout)
                Graphics.ConfigurePhysicalSurface(GopSurfaceLayout);
            Graphics.SetPhysicalSurfaceActive(
                !TripleBuffered && HasGopSurfaceLayout);
        }

        public static void SetBootInfo(UefiBootInfo* bootInfo) {
            OriginalBootInfo = bootInfo;
        }

        public static void RecoverUefiState() {
            if (OriginalBootInfo != null) {
                if ((ulong)OriginalVideoMemory == 0 && OriginalBootInfo->FramebufferBase != 0) {
                    OriginalVideoMemory = (uint*)OriginalBootInfo->FramebufferBase;
                }
                if (OriginalWidth == 0 && OriginalBootInfo->FramebufferWidth != 0) {
                    OriginalWidth = (ushort)OriginalBootInfo->FramebufferWidth;
                }
                if (OriginalHeight == 0 && OriginalBootInfo->FramebufferHeight != 0) {
                    OriginalHeight = (ushort)OriginalBootInfo->FramebufferHeight;
                }
            }

            if (OriginalWidth == 0 && Graphics != null && Graphics.Width > 0) {
                OriginalWidth = (ushort)Graphics.Width;
            }
            if (OriginalHeight == 0 && Graphics != null && Graphics.Height > 0) {
                OriginalHeight = (ushort)Graphics.Height;
            }

            if (Width == 0 && OriginalWidth != 0) {
                Width = OriginalWidth;
            }
            if (Height == 0 && OriginalHeight != 0) {
                Height = OriginalHeight;
            }
            if ((ulong)VideoMemory == 0 && (ulong)OriginalVideoMemory != 0) {
                VideoMemory = OriginalVideoMemory;
            }
        }

        public static void Update() {
            if (TripleBuffered) {
                if (VideoMemory == null || FirstBuffer == null || SecondBuffer == null) {
                    return;
                }
                if (HasGopSurfaceLayout) {
                    if (!FramebufferSurface.PresentChanged(VideoMemory,
                            GopSurfaceLayout, FirstBuffer, SecondBuffer))
                        return;
                } else {
                    ulong pixelCount = (ulong)Width * Height;
                    for (ulong i = 0; i < pixelCount; i++) {
                        if (FirstBuffer[i] != SecondBuffer[i])
                            VideoMemory[i] = FirstBuffer[i];
                    }
                }
                Native.Movsd(SecondBuffer, FirstBuffer,
                    (ulong)Width * Height);
            }
            //if (Graphics != null) Graphics?.Update();
            Graphics?.Update();
        }

        public static void Initialize(ushort XRes, ushort YRes, uint* FB) {
            InitializeCore(XRes, YRes, FB, default, false);
        }

        internal static bool InitializeUefi(UefiBootInfo* bootInfo,
            UefiBootMemoryHandoff* memoryHandoff) {
            if (bootInfo == null || memoryHandoff == null ||
                bootInfo->Magic != 0x49425847u || bootInfo->Version != 1 ||
                bootInfo->Size != sizeof(UefiBootInfo) ||
                !bootInfo->HasFramebuffer || bootInfo->FramebufferBase == 0 ||
                bootInfo->FramebufferWidth == 0 ||
                bootInfo->FramebufferHeight == 0 ||
                bootInfo->FramebufferWidth > 0xFFFFu ||
                bootInfo->FramebufferHeight > 0xFFFFu ||
                (memoryHandoff->FramebufferMappingFlags & 1u) == 0)
                return false;

            uint checksum = 0;
            uint* bootInfoWords = (uint*)bootInfo;
            uint bootInfoWordCount = (uint)(sizeof(UefiBootInfo) / sizeof(uint));
            for (uint i = 0; i < bootInfoWordCount; i++)
                checksum += bootInfoWords[i];
            if (checksum != 0) return false;

            if (!FramebufferSurface.TryCreateLayout(
                    bootInfo->FramebufferBase,
                    bootInfo->FramebufferWidth,
                    bootInfo->FramebufferHeight,
                    bootInfo->FramebufferPitch,
                    bootInfo->FramebufferSize,
                    memoryHandoff->GopPixelFormat,
                    memoryHandoff->GopRedMask,
                    memoryHandoff->GopGreenMask,
                    memoryHandoff->GopBlueMask,
                    memoryHandoff->GopReservedMask,
                    out FramebufferSurfaceLayout layout)) return false;

            OriginalBootInfo = bootInfo;
            GopSurfaceLayout = layout;
            HasGopSurfaceLayout = true;
            InitializeCore((ushort)bootInfo->FramebufferWidth,
                (ushort)bootInfo->FramebufferHeight,
                (uint*)bootInfo->FramebufferBase, layout, true);
            return Graphics != null && Graphics.VideoMemory != null &&
                (ulong)OriginalVideoMemory == bootInfo->FramebufferBase &&
                Graphics.HasPhysicalSurface;
        }

        private static void InitializeCore(ushort XRes, ushort YRes,
            uint* FB, FramebufferSurfaceLayout physicalLayout,
            bool hasPhysicalLayout) {
            Width = XRes;
            Height = YRes;
            OriginalWidth = XRes;
            OriginalHeight = YRes;
            HasGopSurfaceLayout = hasPhysicalLayout;
            GopSurfaceLayout = hasPhysicalLayout
                ? physicalLayout : default;
            _TripleBuffered = false;
            VideoMemory = FB; // Video memory must be set before any operation that might clear/draw
            OriginalVideoMemory = FB; // Permanent backup - NEVER overwritten
            ulong pixelCount = (ulong)XRes * YRes;
            if (pixelCount == 0 || pixelCount > 0xFFFFFFFFFFFFFFFFUL / 4UL) {
                Graphics = null;
                FirstBuffer = SecondBuffer = null;
                return;
            }
            FirstBuffer = (uint*)Allocator.Allocate(pixelCount * 4UL);
            SecondBuffer = (uint*)Allocator.Allocate(pixelCount * 4UL);
            if (FirstBuffer == null || SecondBuffer == null) {
                Graphics = null;
                return;
            }
            Native.Stosd(FirstBuffer, 0, pixelCount);
            Native.Stosd(SecondBuffer, 0, pixelCount);
            Control.MousePosition.X = XRes / 2;
            Control.MousePosition.Y = YRes / 2;
            Graphics = new Graphics(Width, Height, FB); // Ensure Graphics is created ONLY here
            if (hasPhysicalLayout &&
                !Graphics.ConfigurePhysicalSurface(physicalLayout)) {
                Graphics = null;
                return;
            }
            Graphics.SetPhysicalSurfaceActive(hasPhysicalLayout);
            Console.Clear();
        }
    }
}
