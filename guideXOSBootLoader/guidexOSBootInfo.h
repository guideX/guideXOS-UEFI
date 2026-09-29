#pragma once

#include <stdint.h>

#pragma pack(push, 1)

namespace guideXOS
{
    // Boot mode (future‑proof if more modes ever appear)
    enum class BootMode : uint32_t
    {
        Uefi = 1,   // UEFI boot
        // Bios = 2 // reserved, not currently used
    };

    // Simple framebuffer pixel format (subset of what GOP can describe)
    enum class FramebufferFormat : uint32_t
    {
        Unknown = 0,
        R8G8B8A8,   // 32‑bit, little‑endian, RGBA
        B8G8R8A8,   // 32‑bit, little‑endian, BGRA
    };

    // ============================================================================
    // BootInfo Flags bit definitions
    // ============================================================================
    // Bit 0: Memory map valid
    // Bit 1: Framebuffer valid
    // Bit 2: Ramdisk valid
    // Bit 3: Simple Pointer Protocol valid (mouse)
    // Bit 4: Absolute Pointer Protocol valid (touchscreen)
    // Bit 5: Text Input Ex Protocol valid (keyboard)
    // ============================================================================
    static const uint32_t BOOTINFO_FLAG_MEMMAP           = (1u << 0);
    static const uint32_t BOOTINFO_FLAG_FRAMEBUFFER      = (1u << 1);
    static const uint32_t BOOTINFO_FLAG_RAMDISK          = (1u << 2);
    static const uint32_t BOOTINFO_FLAG_SIMPLE_POINTER   = (1u << 3);
    static const uint32_t BOOTINFO_FLAG_ABSOLUTE_POINTER = (1u << 4);
    static const uint32_t BOOTINFO_FLAG_TEXT_INPUT_EX    = (1u << 5);

    struct BootInfo
    {
        // Header (32 bytes)
        uint32_t Magic;              // 'GXBI' (0x49425847)
        uint16_t Version;            // Version 1
        uint16_t Size;               // sizeof(BootInfo)
        uint32_t Flags;              // Bit flags for valid fields
        uint32_t HeaderChecksum;     // 32-bit checksum
        uint32_t Reserved0;
        BootMode BootMode;           // UEFI boot mode
        uint32_t Reserved1;

        // Memory Map (24 bytes)
        uint64_t MemoryMap;                // Pointer to EFI_MEMORY_DESCRIPTOR array
        uint64_t MemoryMapEntryCount;      // Number of entries
        uint64_t MemoryMapDescriptorSize;  // Size of each descriptor

        // Framebuffer (40 bytes)
        uint64_t FramebufferBase;          // Physical address
        uint64_t FramebufferSize;          // Size in bytes
        uint32_t FramebufferWidth;         // Width in pixels
        uint32_t FramebufferHeight;        // Height in pixels
        uint32_t FramebufferPitch;         // Bytes per scanline
        FramebufferFormat FramebufferFormat; // Pixel format
        uint32_t Reserved2;

        // ACPI (8 bytes)
        uint64_t AcpiRsdp;                 // ACPI RSDP pointer

        // Command Line (8 bytes)
        uint64_t CommandLine;              // Kernel command line (optional)

        // Ramdisk (16 bytes)
        uint64_t RamdiskBase;              // Ramdisk physical address
        uint64_t RamdiskSize;              // Ramdisk size in bytes

        // ============================================================================
        // Input Protocols (24 bytes) - UEFI mouse/keyboard support
        // ============================================================================
        // These pointers are obtained by the bootloader using LocateProtocol()
        // and passed to the kernel. They are ONLY valid BEFORE ExitBootServices.
        // After ExitBootServices, the kernel must switch to USB HID or native drivers.
        // 
        // IMPORTANT: The kernel must NOT dereference these pointers after
        // ExitBootServices is called - doing so will cause undefined behavior.
        // ============================================================================
        uint64_t SimplePointerProtocol;    // EFI_SIMPLE_POINTER_PROTOCOL* (mouse)
        uint64_t AbsolutePointerProtocol;  // EFI_ABSOLUTE_POINTER_PROTOCOL* (touchscreen)
        uint64_t SimpleTextInputEx;        // EFI_SIMPLE_TEXT_INPUT_EX_PROTOCOL* (keyboard)

        // BootInfo v1 extensions (24 bytes) - reduced from 48 to accommodate input protocols.
        // Reserved[0] and [1] carry the kernel linked virtual-address start and span
        // so the identity-address allocator can exclude the kernel mapping; [2] stays reserved.
        uint64_t Reserved[3];
    };
}

#pragma pack(pop)

namespace guideXOS
{
    // Magic and version constants for BootInfo v1
    static const uint32_t GUIDEXOS_BOOTINFO_MAGIC   = 0x49425847; // 'GXBI'
    static const uint16_t GUIDEXOS_BOOTINFO_VERSION = 1;

    // Early panic: implemented in a .cpp file, infinite loop and/or framebuffer error.
    [[noreturn]] void guidexos_early_panic(const BootInfo* bi);

    // Compute 32-bit sum over BootInfo (Size rounded down to multiple of 4)
    static inline bool guidexos_bootinfo_checksum_valid(const BootInfo* bi)
    {
        if (!bi) return false;
        if (bi->Size == 0u) return false;
        uint32_t byteCount = bi->Size & ~0x3u; // round down to multiple of 4
        if (byteCount < sizeof(BootInfo)) return false;

        const uint32_t* p = reinterpret_cast<const uint32_t*>(bi);
        uint32_t count = byteCount / 4u;
        uint32_t sum = 0u;
        for (uint32_t i = 0; i < count; ++i)
            sum += p[i];
        return (sum == 0u);
    }

    // Very early, heap-less validation of BootInfo v1
    static inline void guidexos_validate_bootinfo_or_panic(const BootInfo* bi)
    {
        if (!bi) {
            guidexos_early_panic(nullptr);
        }

        // Basic size check before trusting any other fields
        if (bi->Size < sizeof(BootInfo)) {
            guidexos_early_panic(nullptr);
        }

        // 1. Magic, version, size (size equality is strict for v1)
        if (bi->Magic != GUIDEXOS_BOOTINFO_MAGIC) {
            guidexos_early_panic(bi);
        }
        if (bi->Version != GUIDEXOS_BOOTINFO_VERSION) {
            guidexos_early_panic(bi);
        }
        if (bi->Size != sizeof(BootInfo)) {
            guidexos_early_panic(bi);
        }

        // 2. Checksum / invariant
        if (!guidexos_bootinfo_checksum_valid(bi)) {
            guidexos_early_panic(bi);
        }

        // 3. Optional flags and pointer sanity checks
        // Framebuffer info valid?
        if (bi->Flags & BOOTINFO_FLAG_FRAMEBUFFER) {
            if (bi->FramebufferBase == 0u || bi->FramebufferSize == 0u) {
                guidexos_early_panic(bi);
            }
        }

        // Memory map valid?
        if (bi->Flags & BOOTINFO_FLAG_MEMMAP) {
            if (bi->MemoryMap == 0u ||
                bi->MemoryMapEntryCount == 0u ||
                bi->MemoryMapDescriptorSize == 0u) {
                guidexos_early_panic(bi);
            }
        }
    }
}
