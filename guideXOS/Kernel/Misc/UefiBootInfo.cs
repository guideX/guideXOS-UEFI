using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    /// <summary>
    /// UEFI Boot Information structure - matches C++ guideXOS::BootInfo
    /// This is the modern boot information format passed by the UEFI bootloader
    /// 
    /// ============================================================================
    /// STRUCT LAYOUT (Pack=1, all offsets verified against C++ guidexOSBootInfo.h)
    /// ============================================================================
    /// Header (28 bytes @ 0x00):
    ///   0x00: Magic (4)           - 'GXBI' (0x49425847)
    ///   0x04: Version (2)         - Version 1
    ///   0x06: Size (2)            - sizeof(BootInfo)
    ///   0x08: Flags (4)           - Bit flags for valid fields
    ///   0x0C: HeaderChecksum (4)  - 32-bit checksum
    ///   0x10: Reserved0 (4)
    ///   0x14: BootMode (4)        - UEFI boot mode
    ///   0x18: Reserved1 (4)
    /// Memory Map (24 bytes @ 0x1C):
    ///   0x1C: MemoryMap (8)
    ///   0x24: MemoryMapEntryCount (8)
    ///   0x2C: MemoryMapDescriptorSize (8)
    /// Framebuffer (36 bytes @ 0x34):
    ///   0x34: FramebufferBase (8)
    ///   0x3C: FramebufferSize (8)
    ///   0x44: FramebufferWidth (4)
    ///   0x48: FramebufferHeight (4)
    ///   0x4C: FramebufferPitch (4)
    ///   0x50: FramebufferFormat (4)
    ///   0x54: Reserved2 (4)
    /// ACPI (8 bytes @ 0x58):
    ///   0x58: AcpiRsdp (8)
    /// CommandLine (8 bytes @ 0x60):
    ///   0x60: CommandLine (8)
    /// Ramdisk (16 bytes @ 0x68):
    ///   0x68: RamdiskBase (8)
    ///   0x70: RamdiskSize (8)
    /// Input Protocols (24 bytes @ 0x78):
    ///   0x78: SimplePointerProtocol (8)
    ///   0x80: AbsolutePointerProtocol (8)
    ///   0x88: SimpleTextInputEx (8)
    /// Reserved (24 bytes @ 0x90):
    ///   0x90: Reserved[3] (24)
    /// ============================================================================
    /// Total: 0xA8 = 168 bytes
    /// ============================================================================
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UefiBootInfo {
        // Header (28 bytes @ 0x00)
        public uint Magic;              // 0x00: 'GXBI' (0x49425847)
        public ushort Version;          // 0x04: Version 1
        public ushort Size;             // 0x06: sizeof(UefiBootInfo)
        public uint Flags;              // 0x08: Bit flags for valid fields
        public uint HeaderChecksum;     // 0x0C: 32-bit checksum
        public uint Reserved0;          // 0x10
        public BootMode BootMode;       // 0x14: UEFI boot mode
        public uint Reserved1;          // 0x18

        // Memory Map (24 bytes @ 0x1C)
        public ulong MemoryMap;                // 0x1C: Pointer to EFI_MEMORY_DESCRIPTOR array
        public ulong MemoryMapEntryCount;      // 0x24: Number of entries
        public ulong MemoryMapDescriptorSize;  // 0x2C: Size of each descriptor

        // Framebuffer (36 bytes @ 0x34)
        public ulong FramebufferBase;          // 0x34: Physical address
        public ulong FramebufferSize;          // 0x3C: Size in bytes
        public uint FramebufferWidth;          // 0x44: Width in pixels
        public uint FramebufferHeight;         // 0x48: Height in pixels
        public uint FramebufferPitch;          // 0x4C: Bytes per scanline
        public FramebufferFormat FramebufferFormat; // 0x50: Pixel format
        public uint Reserved2;                 // 0x54

        // ACPI (8 bytes @ 0x58)
        public ulong AcpiRsdp;                 // 0x58: ACPI RSDP pointer

        // Command Line (8 bytes @ 0x60)
        public ulong CommandLine;              // 0x60: Kernel command line (optional)

        // Ramdisk (16 bytes @ 0x68)
        public ulong RamdiskBase;              // 0x68: Ramdisk physical address
        public ulong RamdiskSize;              // 0x70: Ramdisk size in bytes

        // Input Protocols (24 bytes @ 0x78)
        // ============================================================================
        // These pointers are obtained by the bootloader using LocateProtocol()
        // and passed to the kernel. They are ONLY valid BEFORE ExitBootServices.
        // After ExitBootServices, the kernel must switch to USB HID or native drivers.
        // ============================================================================
        public ulong SimplePointerProtocol;    // 0x78: EFI_SIMPLE_POINTER_PROTOCOL* (mouse)
        public ulong AbsolutePointerProtocol;  // 0x80: EFI_ABSOLUTE_POINTER_PROTOCOL* (touchscreen)
        public ulong SimpleTextInputEx;        // 0x88: EFI_SIMPLE_TEXT_INPUT_EX_PROTOCOL* (keyboard)

        // BootInfo v1 extensions (24 bytes @ 0x90). Reserved[0] is the kernel
        // physical backing start; [1] is its linked virtual start; [2] points
        // to UefiBootMemoryHandoff (span and zero-terminated page-table list).
        public fixed ulong Reserved[3];        // 0x90-0xA7

        /// <summary>
        /// Check if memory map is valid
        /// </summary>
        public bool HasMemoryMap => (Flags & 0x1) != 0;

        /// <summary>
        /// Check if framebuffer is valid
        /// </summary>
        public bool HasFramebuffer => (Flags & 0x2) != 0;

        /// <summary>
        /// Check if ramdisk is valid
        /// </summary>
        public bool HasRamdisk => (Flags & 0x4) != 0;

        /// <summary>
        /// Check if simple pointer protocol (mouse) is available
        /// </summary>
        public bool HasSimplePointer => (Flags & 0x8) != 0;

        /// <summary>
        /// Check if absolute pointer protocol (touchscreen) is available
        /// </summary>
        public bool HasAbsolutePointer => (Flags & 0x10) != 0;

        /// <summary>
        /// Check if text input ex protocol (keyboard) is available
        /// </summary>
        public bool HasTextInputEx => (Flags & 0x20) != 0;

        /// <summary>
        /// Convert to VBEInfo for legacy compatibility
        /// </summary>
        public VBEInfo ToVBEInfo() {
            return new VBEInfo {
                ScreenWidth = (ushort)FramebufferWidth,
                ScreenHeight = (ushort)FramebufferHeight,
                PhysBase = (uint)FramebufferBase,
                Pitch = (ushort)FramebufferPitch,
                BitsPerPixel = 32, // Assume 32-bit
                MemoryModel = 6,   // Direct color
            };
        }
    }

    /// <summary>
    /// Bootloader-owned allocator exclusions appended indirectly to BootInfo
    /// so the fixed-size v1 structure can retain its existing layout.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct UefiBootMemoryHandoff {
        public ulong KernelSpanBytes;
        public fixed ulong PageTablePages[2049];
    }

    /// <summary>
    /// Boot mode enumeration
    /// </summary>
    public enum BootMode : uint {
        Uefi = 1,  // UEFI boot
        // Bios = 2,  // Reserved for future BIOS support
    }

    /// <summary>
    /// Framebuffer pixel format
    /// </summary>
    public enum FramebufferFormat : uint {
        Unknown = 0,
        R8G8B8A8 = 1,  // RGBA
        B8G8R8A8 = 2,  // BGRA (most common)
    }
}
