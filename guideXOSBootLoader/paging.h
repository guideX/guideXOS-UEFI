#pragma once

#include <Uefi.h>
#include <stdint.h>

// Minimal x86_64 paging builder for UEFI->kernel handoff.
// Builds identity-mapped 4KiB pages for a small set of physical ranges.
// Intended for QEMU/OVMF, long mode already enabled by firmware.

namespace guideXOS {
namespace paging
{
    // Page table entry flags
    static constexpr UINT64 PTE_P  = 1ull << 0;   // Present
    static constexpr UINT64 PTE_W  = 1ull << 1;   // Writable
    static constexpr UINT64 PTE_U  = 1ull << 2;   // User (keep 0 for kernel)
    static constexpr UINT64 PTE_PWT= 1ull << 3;
    static constexpr UINT64 PTE_PCD= 1ull << 4;
    static constexpr UINT64 PTE_A  = 1ull << 5;
    static constexpr UINT64 PTE_D  = 1ull << 6;
    static constexpr UINT64 PTE_PS = 1ull << 7;   // Large page (PD/PTD)
    static constexpr UINT64 PTE_G  = 1ull << 8;
    static constexpr UINT64 PTE_NX = 1ull << 63;  // No-execute (if supported/enabled)

    struct PageTables
    {
        EFI_PHYSICAL_ADDRESS Pml4Phys;
    };

    // BootInfo v1 keeps its fixed size. This loader-owned handoff carries the
    // kernel span, page-table list, GOP format masks, and fixed BM1 evidence.
    static constexpr UINTN MAX_PT_PAGES = 2048;
#if defined(UEFI_DIAGNOSTIC_BM1)
    struct BareMetalDiagnosticRecord
    {
        UINT32 Magic;
        UINT32 Stage;
        UINT32 Substage;
        UINT32 Sequence;
        UINT32 FaultVector;
        UINT32 FaultFlags;
        UINT32 HeartbeatFrames;
        UINT32 HeartbeatTicks;
        UINT64 FaultErrorCode;
        UINT64 FaultRip;
        UINT64 FaultCr2;
        UINT64 FaultCr3;
        UINT64 FaultRsp;
        UINT64 FaultRflags;
        UINT64 FramebufferCheckedBytes;
        UINT64 FramebufferDescriptorBase;
        UINT64 FramebufferDescriptorSize;
        UINT64 FramebufferDescriptorAttributes;
        UINT32 FramebufferDescriptorType;
        UINT32 FramebufferMapPagesChecked;
        UINT32 FramebufferMapLeafFlags;
        UINT32 FramebufferValidationFlags;
        UINT32 FramebufferFailureCode;
        UINT32 FramebufferDescriptorCount;
        UINT64 FramebufferDescriptorCoveredBytes;
        // BM3B v1 extension; keep all BM1 fields and offsets unchanged.
        UINT64 FaultRdi;
        UINT64 FaultCr4;
        UINT64 FaultEffectiveAddress;
        UINT32 FaultCaptureFlags;
        UINT32 FaultRecordVersion;
        // BM3C: five fixed 12-word pointer checkpoints (P0/P2/P3/P4/P5).
        UINT64 DisplayPointerCheckpoints[5][12];
    };
#endif

    struct BootMemoryHandoff
    {
        UINT64 KernelSpanBytes;
        UINT64 PageTablePages[MAX_PT_PAGES + 1];
#if defined(UEFI_DIAGNOSTIC_BM1)
        UINT32 GopPixelFormat;
        UINT32 GopRedMask;
        UINT32 GopGreenMask;
        UINT32 GopBlueMask;
        UINT32 GopReservedMask;
        UINT32 GopMetadataReserved;
        BareMetalDiagnosticRecord Diagnostics;
#endif
    };

#if defined(UEFI_DIAGNOSTIC_BM1)
    static_assert(sizeof(BareMetalDiagnosticRecord) == 656,
        "C# and UEFI BM3C diagnostic record layouts must match");
    static_assert(sizeof(BootMemoryHandoff) == 17080,
        "C# and UEFI BM3C handoff layouts must match");
#endif

    // Build a minimal identity-mapped page table set for the ranges provided.
    // All pages are mapped RW, executable (NX not set) to keep it minimal.
    EFI_STATUS BuildIdentityPageTables(
        EFI_SYSTEM_TABLE* SystemTable,
        const EFI_PHYSICAL_ADDRESS* rangesBegin,
        const EFI_PHYSICAL_ADDRESS* rangesEnd,
        /*in*/ const UINTN* sizesBegin,
        PageTables* out);

    // Helper to add a mapping for [physBase, physBase+size) as identity map
    EFI_STATUS MapIdentityRange(
        EFI_SYSTEM_TABLE* SystemTable,
        EFI_PHYSICAL_ADDRESS pml4Phys,
        EFI_PHYSICAL_ADDRESS physBase,
        UINTN sizeBytes);

    // Helper to add an UNCACHED identity mapping for MMIO regions (framebuffer, APIC, etc.)
    // Sets PTE_PCD | PTE_PWT to ensure writes go directly to hardware (no CPU caching).
    EFI_STATUS MapIdentityRangeUncached(
        EFI_SYSTEM_TABLE* SystemTable,
        EFI_PHYSICAL_ADDRESS pml4Phys,
        EFI_PHYSICAL_ADDRESS physBase,
        UINTN sizeBytes);

    // Helper to add a mapping for [virtBase, virtBase+size) -> physBase
    EFI_STATUS MapRange(
        EFI_SYSTEM_TABLE* SystemTable,
        EFI_PHYSICAL_ADDRESS pml4Phys,
        UINT64 virtBase,
        EFI_PHYSICAL_ADDRESS physBase,
        UINTN sizeBytes);

    // Identity-map all page table pages allocated so far.
    // MUST be called after MapRange() to ensure newly allocated PT pages are accessible.
    EFI_STATUS IdentityMapPageTablePages(
        EFI_SYSTEM_TABLE* SystemTable,
        EFI_PHYSICAL_ADDRESS pml4Phys);

    // The kernel allocator uses identity addresses. Publish the linked image
    // span and page-table backing pages so it can reserve both kernel aliases.
    BootMemoryHandoff* GetBootMemoryHandoff();
}
}
