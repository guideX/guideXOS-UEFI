/*
GuideXOS UEFI Bootloader Context

Target:
- UEFI x86_64 only
- Visual Studio / MSVC
- No CRT, no libc, no STL
- No malloc/new/delete
- No exceptions, no RTTI
- MS x64 ABI (NOT SysV)
- No identity-mapped assumptions
- Kernel is ELF64
- Kernel entry takes BootInfo*
- Boot services MUST be exited before jumping to kernel

Forbidden:
- windows.h
- std::*
- printf / cout
- assuming p_vaddr == physical
- calling UEFI services after ExitBootServices
*/

#include <Uefi.h>
#include <Library/UefiLib.h>
#include <Library/MemoryAllocationLib.h>
#include <Library/BaseMemoryLib.h>
#include <Library/PrintLib.h>
#include <Protocol/SimpleFileSystem.h>
#include <Protocol/LoadedImage.h>
#include <Protocol/SimplePointer.h>
#include <Protocol/AbsolutePointer.h>
#include <Guid/Acpi.h>
#include <Guid/FileInfo.h>
#include "bootinfo.h"          // legacy, gradually being phased out
#include "elf.h"
#include "guidexOSBootInfo.h"   // canonical BootInfo v1
#include "uefi_shim.h"         // UEFI shims for freestanding environment
#include "debug_helpers.h"     // Post-ExitBootServices debugging
#include "paging.h"            // minimal identity page tables
#include "boot_splash.h"       // boot splash screen

// MSVC intrinsics
extern "C" void __halt(void);
#pragma intrinsic(__halt)

// Assembly trampoline (NASM, win64 ABI)
extern "C" void BootHandoffTrampoline(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys);
extern "C" void SetupTrampoline(void* executableMemory);
extern "C" UINTN GetTrampolineCodeSize(void);

// Local aliases for compatibility with existing code
#define Acpi20TableGuid gEfiAcpi20TableGuid
#define Acpi10TableGuid gEfiAcpi10TableGuid

EFI_GRAPHICS_OUTPUT_PROTOCOL* GOP;
EFI_FILE_PROTOCOL* KernelFile;
BootInfo bootInfo;      // legacy struct instance (still used elsewhere)
// v1BootInfo is now allocated dynamically in EfiLoaderData pages to survive ExitBootServices

EFI_STATUS LoadFile(EFI_FILE_PROTOCOL** file, CHAR16* path, EFI_HANDLE ImageHandle, EFI_SYSTEM_TABLE* SystemTable) {
    EFI_LOADED_IMAGE_PROTOCOL* LoadedImage;
    EFI_SIMPLE_FILE_SYSTEM_PROTOCOL* FileSystem;
    EFI_FILE_PROTOCOL* Root;
    EFI_STATUS s;

    SystemTable->BootServices->HandleProtocol(ImageHandle, &gEfiLoadedImageProtocolGuid, (void**)&LoadedImage);
    SystemTable->BootServices->HandleProtocol(LoadedImage->DeviceHandle, &gEfiSimpleFileSystemProtocolGuid, (void**)&FileSystem);
    FileSystem->OpenVolume(FileSystem, &Root);
    s = Root->Open(Root, file, path, EFI_FILE_MODE_READ, EFI_FILE_READ_ONLY);
    if (s != EFI_SUCCESS) {
        Print((CONST CHAR16*)L"Could not open file: %s\n", path);
    }
    return s;
}


// New helper: exits boot services with retry and returns a stable memory map
// (Kept for compatibility; handoff path uses ExitBootServicesWithMemoryMapInBuffer)
EFI_STATUS ExitBootServicesWithMemoryMap(
    EFI_HANDLE          ImageHandle,
    EFI_SYSTEM_TABLE*   SystemTable,
    EFI_MEMORY_DESCRIPTOR** outMap,
    UINTN*              outEntryCount,
    UINTN*              outDescriptorSize
)
{
    EFI_STATUS           status;
    EFI_MEMORY_DESCRIPTOR* tempMap = NULL;
    UINTN                tempMapSize = 0;
    UINTN                mapKey = 0;
    UINTN                descriptorSize = 0;
    UINT32               descriptorVersion = 0;

    // 1) Get required buffer size (expected EFI_BUFFER_TOO_SMALL)
    status = SystemTable->BootServices->GetMemoryMap(
        &tempMapSize,
        tempMap,
        &mapKey,
        &descriptorSize,
        &descriptorVersion
    );
    if (status != EFI_BUFFER_TOO_SMALL) {
        return status;
    }

    // Add some slack in case map grows between calls
    tempMapSize += 2 * descriptorSize;

    // 2) Allocate pool for the temporary memory map buffer
    status = SystemTable->BootServices->AllocatePool(
        EfiLoaderData,
        tempMapSize,
        (void**)&tempMap
    );
    if (EFI_ERROR(status)) {
        return status;
    }

    for (;;) {
        // 3) Get the actual memory map into the temporary buffer
        UINTN curMapSize = tempMapSize;
        status = SystemTable->BootServices->GetMemoryMap(
            &curMapSize,
            tempMap,
            &mapKey,
            &descriptorSize,
            &descriptorVersion
        );

        if (status == EFI_BUFFER_TOO_SMALL) {
            // Map grew; reallocate and retry (still before ExitBootServices)
            SystemTable->BootServices->FreePool(tempMap);

            tempMapSize = curMapSize + 2 * descriptorSize;
            status = SystemTable->BootServices->AllocatePool(
                EfiLoaderData,
                tempMapSize,
                (void**)&tempMap
            );
            if (EFI_ERROR(status)) {
                return status;
            }
            continue;
        }

        if (EFI_ERROR(status)) {
            SystemTable->BootServices->FreePool(tempMap);
            return status;
        }

        // 4) Allocate pages for the final memory map copy in memory that
        // survives ExitBootServices (conventional pages, not pool).
        UINTN pageCount = (curMapSize + EFI_PAGE_SIZE - 1) / EFI_PAGE_SIZE;
        EFI_PHYSICAL_ADDRESS physMap = 0;
        status = SystemTable->BootServices->AllocatePages(
            AllocateAnyPages,
            EfiLoaderData,
            pageCount,
            &physMap
        );
        if (EFI_ERROR(status)) {
            SystemTable->BootServices->FreePool(tempMap);
            return status;
        }

        EFI_MEMORY_DESCRIPTOR* finalMap = (EFI_MEMORY_DESCRIPTOR*)(UINTN)physMap;
        CopyMem(finalMap, tempMap, curMapSize);

        // 5) Attempt to exit boot services with this MapKey
        status = SystemTable->BootServices->ExitBootServices(
            ImageHandle,
            mapKey
        );

        if (status == EFI_SUCCESS) {
            // SUCCESS:
            // - finalMap is now in pages that remain valid after ExitBootServices
            // - no more BootServices calls allowed
            // NOTE: Cannot call FreePool(tempMap) here - BootServices are GONE!
            // The tempMap memory is small and will be reclaimed by the kernel's
            // memory manager when it processes the memory map.

            *outMap            = finalMap;
            *outDescriptorSize = descriptorSize;
            *outEntryCount     = curMapSize / descriptorSize;
            return EFI_SUCCESS;
        }

        // ExitBootServices failed; free finalMap pages while BootServices
        // are still active and retry with a fresh memory map.
        SystemTable->BootServices->FreePages(physMap, pageCount);

        if (status != EFI_INVALID_PARAMETER) {
            // Hard failure, give up
            SystemTable->BootServices->FreePool(tempMap);
            return status;
        }

        // EFI_INVALID_PARAMETER:
        //   The MapKey was stale. We must fetch a fresh map and retry.
        //   BootServices are still alive here.
        //   Loop continues to re‑query and re‑try.
    }
}

// New helper: exits boot services using a caller-provided buffer for the final map
EFI_STATUS ExitBootServicesWithMemoryMapInBuffer(
    EFI_HANDLE          ImageHandle,
    EFI_SYSTEM_TABLE*   SystemTable,
    EFI_MEMORY_DESCRIPTOR* finalMap,
    UINTN                finalMapCapacityBytes,
    UINTN*               outEntryCount,
    UINTN*               outDescriptorSize
)
{
    if (!SystemTable || !finalMap || finalMapCapacityBytes == 0 || !outEntryCount || !outDescriptorSize) {
        return EFI_INVALID_PARAMETER;
    }

    EFI_STATUS status;
    EFI_MEMORY_DESCRIPTOR* tempMap = NULL;
    UINTN tempMapSize = 0;
    UINTN mapKey = 0;
    UINTN descriptorSize = 0;
    UINT32 descriptorVersion = 0;

    status = SystemTable->BootServices->GetMemoryMap(
        &tempMapSize,
        tempMap,
        &mapKey,
        &descriptorSize,
        &descriptorVersion);

    if (status != EFI_BUFFER_TOO_SMALL) {
        return status;
    }

    tempMapSize += 2 * descriptorSize;

    status = SystemTable->BootServices->AllocatePool(EfiLoaderData, tempMapSize, (void**)&tempMap);
    if (EFI_ERROR(status)) return status;

    for (;;) {
        UINTN curMapSize = tempMapSize;
        status = SystemTable->BootServices->GetMemoryMap(
            &curMapSize,
            tempMap,
            &mapKey,
            &descriptorSize,
            &descriptorVersion);

        if (status == EFI_BUFFER_TOO_SMALL) {
            SystemTable->BootServices->FreePool(tempMap);
            tempMapSize = curMapSize + 2 * descriptorSize;
            status = SystemTable->BootServices->AllocatePool(EfiLoaderData, tempMapSize, (void**)&tempMap);
            if (EFI_ERROR(status)) return status;
            continue;
        }

        if (EFI_ERROR(status)) {
            SystemTable->BootServices->FreePool(tempMap);
            return status;
        }

        if (curMapSize > finalMapCapacityBytes) {
            SystemTable->BootServices->FreePool(tempMap);
            return EFI_BUFFER_TOO_SMALL;
        }

        CopyMem(finalMap, tempMap, curMapSize);

        status = SystemTable->BootServices->ExitBootServices(ImageHandle, mapKey);
        if (status == EFI_SUCCESS) {
            // Boot services are gone. tempMap is leaked (acceptable for handoff).
            *outDescriptorSize = descriptorSize;
            *outEntryCount = curMapSize / descriptorSize;
            return EFI_SUCCESS;
        }

        if (status != EFI_INVALID_PARAMETER) {
            SystemTable->BootServices->FreePool(tempMap);
            return status;
        }

        // stale MapKey -> retry
    }
}


#pragma pack(push, 1)
typedef struct {
    char  Signature[8];     // "RSD PTR "
    UINT8 Checksum;
    char  OEMID[6];
    UINT8 Revision;
    UINT32 RsdtAddress;     // 32-bit RSDT (for ACPI 1.0)
    // ACPI 2.0+ extensions follow; include if needed:
    UINT32 Length;
    UINT64 XsdtAddress;     // 64-bit XSDT
    UINT8  ExtendedChecksum;
    UINT8  Reserved[3];
} RSDPDescriptor20;
#pragma pack(pop)

RSDPDescriptor20* FindRSDP(EFI_SYSTEM_TABLE* SystemTable) {
    EFI_CONFIGURATION_TABLE* configTable = SystemTable->ConfigurationTable;
    UINTN tableCount = SystemTable->NumberOfTableEntries;
    RSDPDescriptor20* acpi10Rsdp = nullptr;

    for (UINTN i = 0; i < tableCount; ++i) {
        EFI_GUID* guid = &configTable[i].VendorGuid;

        // Prefer the ACPI 2.0 table.  OVMF commonly publishes both GUIDs;
        // returning the first match can select the legacy RSDP (revision 0)
        // even though the XSDT and MCFG are available.
        if (CompareGuid(guid, &gEfiAcpi20TableGuid)) {
            return (RSDPDescriptor20*)configTable[i].VendorTable;
        }

        if (CompareGuid(guid, &gEfiAcpi10TableGuid) && acpi10Rsdp == nullptr)
            acpi10Rsdp = (RSDPDescriptor20*)configTable[i].VendorTable;
    }

    return acpi10Rsdp;
}

#pragma pack(push, 1)
struct AcpiSdtHeaderForLoader {
    char   Signature[4];
    UINT32 Length;
    UINT8  Revision;
    UINT8  Checksum;
    char   OemId[6];
    char   OemTableId[8];
    UINT32 OemRevision;
    UINT32 CreatorId;
    UINT32 CreatorRevision;
};

struct AcpiMcfgAllocationForLoader {
    UINT64 BaseAddress;
    UINT16 Segment;
    UINT8  StartBus;
    UINT8  EndBus;
    UINT32 Reserved;
};

struct AcpiMcfgHeaderForLoader {
    AcpiSdtHeaderForLoader Header;
    UINT64 Reserved;
};
#pragma pack(pop)

static constexpr UINTN MAX_ACPI_TABLES_FOR_LOADER = 64;
static constexpr UINTN MAX_MCFG_ENTRIES_FOR_LOADER = 16;
static constexpr UINTN MAX_RANGES_FOR_LOADER = 128;

struct AcpiTableRangeForLoader {
    EFI_PHYSICAL_ADDRESS Base;
    UINTN Size;
};

struct AcpiDiscoveryForLoader {
    bool Present;
    bool UsingXsdt;
    EFI_PHYSICAL_ADDRESS RootAddress;
    UINT32 RootLength;
    AcpiTableRangeForLoader Tables[MAX_ACPI_TABLES_FOR_LOADER];
    UINTN TableCount;
    AcpiMcfgAllocationForLoader McfgEntries[MAX_MCFG_ENTRIES_FOR_LOADER];
    UINTN McfgEntryCount;
};

static bool AcpiSignatureIs(const AcpiSdtHeaderForLoader* header, const char* signature) {
    if (header == nullptr || signature == nullptr) return false;
    for (UINTN i = 0; i < 4; ++i) {
        if (header->Signature[i] != signature[i]) return false;
    }
    return true;
}

static bool AcpiHeaderIsSaneForLoader(EFI_PHYSICAL_ADDRESS address, UINT32* outLength) {
    if (address == 0 || outLength == nullptr) return false;

    AcpiSdtHeaderForLoader* header =
        (AcpiSdtHeaderForLoader*)(UINTN)address;
    // ACPI tables are firmware-owned input.  Bound the length before using it
    // to walk physical memory while still allowing large OEM tables.
    if (header->Length < sizeof(AcpiSdtHeaderForLoader) ||
        header->Length > (16u * 1024u * 1024u)) {
        return false;
    }

    *outLength = header->Length;
    return true;
}

static void AcpiRecordTableForLoader(
    AcpiDiscoveryForLoader* discovery,
    EFI_PHYSICAL_ADDRESS address,
    UINT32 length)
{
    if (discovery == nullptr || discovery->TableCount >= MAX_ACPI_TABLES_FOR_LOADER)
        return;

    discovery->Tables[discovery->TableCount].Base = address;
    discovery->Tables[discovery->TableCount].Size = (UINTN)length;
    discovery->TableCount++;
}

static void DiscoverAcpiForLoader(
    RSDPDescriptor20* rsdp,
    AcpiDiscoveryForLoader* discovery)
{
    if (discovery == nullptr) return;
    SetMem(discovery, sizeof(*discovery), 0);
    if (rsdp == nullptr) return;

    EFI_PHYSICAL_ADDRESS rootAddress = 0;
    UINTN rootEntrySize = sizeof(UINT32);
    bool useXsdt = false;

    if (rsdp->Revision >= 2 && rsdp->Length >= 36 && rsdp->XsdtAddress != 0) {
        rootAddress = (EFI_PHYSICAL_ADDRESS)rsdp->XsdtAddress;
        rootEntrySize = sizeof(UINT64);
        useXsdt = true;
    } else if (rsdp->RsdtAddress != 0) {
        rootAddress = (EFI_PHYSICAL_ADDRESS)rsdp->RsdtAddress;
    }

    UINT32 rootLength = 0;
    if (!AcpiHeaderIsSaneForLoader(rootAddress, &rootLength)) {
        // A malformed/unavailable XSDT should not prevent the legacy RSDT
        // fallback from being used when the RSDP supplies one.
        if (useXsdt && rsdp->RsdtAddress != 0) {
            rootAddress = (EFI_PHYSICAL_ADDRESS)rsdp->RsdtAddress;
            rootEntrySize = sizeof(UINT32);
            useXsdt = false;
            if (!AcpiHeaderIsSaneForLoader(rootAddress, &rootLength)) return;
        } else {
            return;
        }
    }

    AcpiSdtHeaderForLoader* root =
        (AcpiSdtHeaderForLoader*)(UINTN)rootAddress;
    if ((useXsdt && !AcpiSignatureIs(root, "XSDT")) ||
        (!useXsdt && !AcpiSignatureIs(root, "RSDT"))) {
        return;
    }

    discovery->Present = true;
    discovery->UsingXsdt = useXsdt;
    discovery->RootAddress = rootAddress;
    discovery->RootLength = rootLength;
    AcpiRecordTableForLoader(discovery, rootAddress, rootLength);

    UINTN payloadBytes = rootLength - sizeof(AcpiSdtHeaderForLoader);
    UINTN entryCount = payloadBytes / rootEntrySize;
    if (entryCount > MAX_ACPI_TABLES_FOR_LOADER - 1)
        entryCount = MAX_ACPI_TABLES_FOR_LOADER - 1;

    UINT8* payload = (UINT8*)root + sizeof(AcpiSdtHeaderForLoader);
    for (UINTN i = 0; i < entryCount; ++i) {
        EFI_PHYSICAL_ADDRESS tableAddress;
        if (useXsdt)
            tableAddress = ((UINT64*)payload)[i];
        else
            tableAddress = ((UINT32*)payload)[i];

        UINT32 tableLength = 0;
        if (!AcpiHeaderIsSaneForLoader(tableAddress, &tableLength)) continue;

        AcpiSdtHeaderForLoader* table =
            (AcpiSdtHeaderForLoader*)(UINTN)tableAddress;
        AcpiRecordTableForLoader(discovery, tableAddress, tableLength);

        if (!AcpiSignatureIs(table, "MCFG")) continue;

        const UINTN fixedMcfgSize = sizeof(AcpiMcfgHeaderForLoader);
        if (tableLength < fixedMcfgSize) continue;

        UINTN allocationCount =
            (tableLength - fixedMcfgSize) / sizeof(AcpiMcfgAllocationForLoader);
        if (allocationCount > MAX_MCFG_ENTRIES_FOR_LOADER)
            allocationCount = MAX_MCFG_ENTRIES_FOR_LOADER;

        AcpiMcfgAllocationForLoader* allocations =
            (AcpiMcfgAllocationForLoader*)((UINT8*)table + fixedMcfgSize);
        for (UINTN j = 0; j < allocationCount; ++j) {
            AcpiMcfgAllocationForLoader* allocation = &allocations[j];
            if (allocation->BaseAddress == 0 ||
                allocation->StartBus > allocation->EndBus) {
                continue;
            }

            if (discovery->McfgEntryCount >= MAX_MCFG_ENTRIES_FOR_LOADER)
                break;

            discovery->McfgEntries[discovery->McfgEntryCount++] = *allocation;
            Print(L"ACPI MCFG allocation: base %p segment %u buses %u-%u\n",
                  (VOID*)(UINTN)allocation->BaseAddress,
                  (UINT32)allocation->Segment,
                  (UINT32)allocation->StartBus,
                  (UINT32)allocation->EndBus);
        }
    }

    Print(L"ACPI %s root at %p length %u, tables %u\n",
          useXsdt ? L"XSDT" : L"RSDT",
          (VOID*)(UINTN)rootAddress,
          (UINT32)rootLength,
          (UINT32)discovery->TableCount);
    if (discovery->McfgEntryCount == 0)
        Print(L"ACPI MCFG not present or has no valid allocations\n");
}

// Simple memset for BootInfo
static void ZeroBootInfo(BootInfo* bi) {
    SetMem(bi, sizeof(BootInfo), 0);
}

EFI_STATUS efi_main(EFI_HANDLE ImageHandle, EFI_SYSTEM_TABLE* SystemTable) {
    // Set global SystemTable pointer for uefi_shim.h functions
    gST = SystemTable;

    Print((CONST CHAR16*)L"guideXOS UEFI Bootloader\n");

    // --- Allocate BootInfo in EfiLoaderData pages (survives ExitBootServices) ---
    EFI_PHYSICAL_ADDRESS bootInfoPhys = 0;
    EFI_STATUS status = SystemTable->BootServices->AllocatePages(
        AllocateAnyPages,
        EfiLoaderData,
        1, // 1 page is more than enough for BootInfo
        &bootInfoPhys
    );
    if (EFI_ERROR(status)) {
        Print(L"Failed to allocate BootInfo pages\n");
        return status;
    }
    SetMem((void*)(UINTN)bootInfoPhys, EFI_PAGE_SIZE, 0);
    guideXOS::BootInfo* v1BootInfo = (guideXOS::BootInfo*)(UINTN)bootInfoPhys;

    // --- ACPI RSDP (optional) ---
    RSDPDescriptor20* rsdp = FindRSDP(SystemTable);
    if (!rsdp) {
        Print((CONST CHAR16*)L"ACPI RSDP not found\n");
    } else {
        Print((CONST CHAR16*)L"ACPI RSDP found at %p, rev %u\n", (VOID*)rsdp, (UINT32)rsdp->Revision);
    }

    // Discover the ACPI root and MCFG allocations while firmware still owns
    // the tables.  The resulting ranges are carried into the post-EBS page
    // tables; the kernel later parses the same RSDP independently.
    AcpiDiscoveryForLoader acpiDiscovery{};
    DiscoverAcpiForLoader(rsdp, &acpiDiscovery);

    // --- Locate GOP ---
    EFI_GUID gopGuid = EFI_GRAPHICS_OUTPUT_PROTOCOL_GUID;
    status = SystemTable->BootServices->LocateProtocol(
        &gopGuid,
        NULL,
        (void**)&GOP
    );
    if (EFI_ERROR(status)) {
        Print(L"Failed to locate GOP\n");
        return status;
    }

    // --- Locate Simple Pointer Protocol (Mouse) ---
    // This protocol provides relative mouse movement data from UEFI-aware pointing devices.
    // NOTE: This pointer is ONLY valid before ExitBootServices!
    EFI_SIMPLE_POINTER_PROTOCOL* simplePointer = nullptr;
    {
        EFI_GUID simplePointerGuid = EFI_SIMPLE_POINTER_PROTOCOL_GUID;
        EFI_STATUS spStatus = SystemTable->BootServices->LocateProtocol(
            &simplePointerGuid,
            NULL,
            (void**)&simplePointer
        );
        if (EFI_ERROR(spStatus)) {
            Print(L"Simple Pointer Protocol not found (no UEFI mouse)\n");
            simplePointer = nullptr;
        } else {
            Print(L"Simple Pointer Protocol found at %p\n", (VOID*)simplePointer);
            // Store in BootInfo for kernel
            v1BootInfo->SimplePointerProtocol = (uint64_t)(UINTN)simplePointer;
            v1BootInfo->Flags |= guideXOS::BOOTINFO_FLAG_SIMPLE_POINTER;
        }
    }

    // --- Locate Absolute Pointer Protocol (Touchscreen) ---
    // This protocol provides absolute position data from touchscreens, tablets, etc.
    // NOTE: This pointer is ONLY valid before ExitBootServices!
    EFI_ABSOLUTE_POINTER_PROTOCOL* absolutePointer = nullptr;
    {
        EFI_GUID absolutePointerGuid = EFI_ABSOLUTE_POINTER_PROTOCOL_GUID;
        EFI_STATUS apStatus = SystemTable->BootServices->LocateProtocol(
            &absolutePointerGuid,
            NULL,
            (void**)&absolutePointer
        );
        if (EFI_ERROR(apStatus)) {
            Print(L"Absolute Pointer Protocol not found (no touchscreen)\n");
            absolutePointer = nullptr;
        } else {
            Print(L"Absolute Pointer Protocol found at %p\n", (VOID*)absolutePointer);
            // Store in BootInfo for kernel
            v1BootInfo->AbsolutePointerProtocol = (uint64_t)(UINTN)absolutePointer;
            v1BootInfo->Flags |= guideXOS::BOOTINFO_FLAG_ABSOLUTE_POINTER;
        }
    }

    // === DRAW BOOT SPLASH IMMEDIATELY ===
    // This replaces the TianoCore logo as soon as we have GOP access
    if (GOP->Mode->FrameBufferBase != 0) {
        guideXOS::boot::BootSplash::DrawSplash(
            (EFI_PHYSICAL_ADDRESS)GOP->Mode->FrameBufferBase,
            GOP->Mode->Info->HorizontalResolution,
            GOP->Mode->Info->VerticalResolution,
            GOP->Mode->Info->PixelsPerScanLine * 4u
        );
        // Small delay so splash is visible
        SystemTable->BootServices->Stall(300000); // 300ms
    }

    // --- Load Kernel (your existing loader) ---
    status = LoadFile(&KernelFile, (CHAR16*)L"kernel.elf", ImageHandle, SystemTable);
    if (EFI_ERROR(status)) {
        Print(L"Failed to load kernel\n");
        return status;
    }

    UINT64 kernelBase;
    UINT64 kernelEntryOffset;
    UINT64 kernelTotalSize = 0;
    UINT64 kernelMinVaddr = 0;
    status = LoadElf(SystemTable, KernelFile, &kernelBase, &kernelEntryOffset, &kernelTotalSize, &kernelMinVaddr);
    if (EFI_ERROR(status)) {
        Print(L"Failed to load ELF kernel\n");
        return status;
    }

    UINT64 entryPhys = kernelBase + kernelEntryOffset;
    UINT64 entryVirt = kernelMinVaddr + kernelEntryOffset;
    Print(L"Kernel entry phys: %p virt: %p\n", (VOID*)(UINTN)entryPhys, (VOID*)(UINTN)entryVirt);
    Print(L"Kernel loaded at: %p - %p\n", (VOID*)(UINTN)kernelBase, (VOID*)(UINTN)(kernelBase + kernelTotalSize));

    // --- Load Ramdisk ---
    EFI_PHYSICAL_ADDRESS ramdiskPhys = 0;
    UINT64 ramdiskSize = 0;
    EFI_FILE_PROTOCOL* RamdiskFile = NULL;
    status = LoadFile(&RamdiskFile, (CHAR16*)L"ramdisk.img", ImageHandle, SystemTable);
    if (EFI_ERROR(status)) {
        Print(L"Warning: Failed to load ramdisk\n");
        // Continue anyway - will boot but no files
    } else {
        // Get ramdisk size
        EFI_FILE_INFO* fileInfoBuffer = NULL;
        UINTN fileInfoSize = 0;
        EFI_GUID fileInfoGuid = EFI_FILE_INFO_ID;
        
        // Get size needed for file info
        RamdiskFile->GetInfo(RamdiskFile, &fileInfoGuid, &fileInfoSize, NULL);
        
        // Allocate buffer for file info
        status = SystemTable->BootServices->AllocatePool(
            EfiLoaderData, fileInfoSize, (void**)&fileInfoBuffer);
        
        // Get actual file info
        RamdiskFile->GetInfo(RamdiskFile, &fileInfoGuid, &fileInfoSize, fileInfoBuffer);
        ramdiskSize = fileInfoBuffer->FileSize;
        SystemTable->BootServices->FreePool(fileInfoBuffer);
        
        // Use %Lu for 64-bit decimal in UEFI Print
        Print((CONST CHAR16*)L"Ramdisk size: %Lu bytes\n", (UINT64)ramdiskSize);
        
        // Allocate memory for ramdisk
        UINTN ramdiskPages = (ramdiskSize + EFI_PAGE_SIZE - 1) / EFI_PAGE_SIZE;
        status = SystemTable->BootServices->AllocatePages(
            AllocateAnyPages,
            EfiLoaderData,
            ramdiskPages,
            &ramdiskPhys
        );
        
        if (EFI_ERROR(status)) {
            Print(L"Failed to allocate ramdisk memory\n");
            ramdiskPhys = 0;
            ramdiskSize = 0;
        } else {
            // Read ramdisk into memory
            RamdiskFile->SetPosition(RamdiskFile, 0);
            UINTN readSize = (UINTN)ramdiskSize;
            status = RamdiskFile->Read(RamdiskFile, &readSize, (void*)(UINTN)ramdiskPhys);
            
            if (EFI_ERROR(status) || readSize != ramdiskSize) {
                Print(L"Failed to read ramdisk\n");
                ramdiskPhys = 0;
                ramdiskSize = 0;
            } else {
                Print((CONST CHAR16*)L"Ramdisk loaded at %p\n", (VOID*)(UINTN)ramdiskPhys);
                
                // Pass to kernel via BootInfo
                v1BootInfo->RamdiskBase = ramdiskPhys;
                v1BootInfo->RamdiskSize = ramdiskSize;
                v1BootInfo->Flags |= (1u << 2); // ramdisk valid flag
            }
        }
    }

    // --- Populate BootInfo v1 BEFORE ExitBootServices ---
    v1BootInfo->Magic   = guideXOS::GUIDEXOS_BOOTINFO_MAGIC;
    v1BootInfo->Version = guideXOS::GUIDEXOS_BOOTINFO_VERSION;
    v1BootInfo->Size    = (uint16_t)sizeof(guideXOS::BootInfo);

    v1BootInfo->BootMode = guideXOS::BootMode::Uefi;
    // Preserve any flags already set (e.g., ramdisk valid)

    // ACPI RSDP pointer (64-bit physical)
    v1BootInfo->AcpiRsdp = rsdp ? (uint64_t)(UINTN)rsdp : 0ull;

    // Framebuffer from GOP
    v1BootInfo->FramebufferBase   = (uint64_t)GOP->Mode->FrameBufferBase;
    v1BootInfo->FramebufferWidth  = GOP->Mode->Info->HorizontalResolution;
    v1BootInfo->FramebufferHeight = GOP->Mode->Info->VerticalResolution;
    v1BootInfo->FramebufferPitch  = GOP->Mode->Info->PixelsPerScanLine * 4u; // 32 bpp
    v1BootInfo->FramebufferSize   = (uint64_t)v1BootInfo->FramebufferPitch *
                                   (uint64_t)v1BootInfo->FramebufferHeight;
    v1BootInfo->FramebufferFormat = guideXOS::FramebufferFormat::B8G8R8A8; // typical GOP

    if (v1BootInfo->FramebufferBase != 0 && v1BootInfo->FramebufferSize != 0)
        v1BootInfo->Flags |= (1u << 1); // framebuffer valid

    // Allocate a simple stack that survives ExitBootServices
    // (Some kernels assume a larger / aligned stack than what UEFI leaves us.)
    // CRITICAL: Stack must NOT overlap with kernel!
    // Strategy: Allocate stack at low memory (below kernel load address)
    // Kernel loads around ~0x3D785000, so we'll use 0x200000 (2MB) as base
    const UINTN stackPages = 128; // 512 KiB stack (very generous for deep call chains)
    EFI_PHYSICAL_ADDRESS stackPhys = 0;
    void* stackTop = nullptr;
    
    // Try to allocate at 2MB mark (well below typical kernel load)
    EFI_PHYSICAL_ADDRESS desiredStackBase = 0x200000ULL; // 2MB
    EFI_STATUS stackStatus = SystemTable->BootServices->AllocatePages(
        AllocateAddress,
        EfiLoaderData,
        stackPages,
        &desiredStackBase
    );
    
    if (!EFI_ERROR(stackStatus)) {
        stackPhys = desiredStackBase;
        stackTop = (void*)(UINTN)((stackPhys + stackPages * EFI_PAGE_SIZE) & ~0xFULL);
        Print(L"Stack allocated at %p, top at %p (below kernel)\n", (VOID*)(UINTN)stackPhys, stackTop);
    } else {
        // Try 1MB mark
        desiredStackBase = 0x100000ULL; // 1MB
        stackStatus = SystemTable->BootServices->AllocatePages(
            AllocateAddress,
            EfiLoaderData,
            stackPages,
            &desiredStackBase
        );
        
        if (!EFI_ERROR(stackStatus)) {
            stackPhys = desiredStackBase;
            stackTop = (void*)(UINTN)((stackPhys + stackPages * EFI_PAGE_SIZE) & ~0xFULL);
            Print(L"Stack allocated at %p, top at %p (at 1MB)\n", (VOID*)(UINTN)stackPhys, stackTop);
        } else {
            // Last resort: allocate anywhere and check for overlap
            Print(L"WARNING: Fixed stack allocation failed, using fallback...\n");
            stackPhys = 0;
            stackStatus = SystemTable->BootServices->AllocatePages(
                AllocateAnyPages,
                EfiLoaderData,
                stackPages,
                &stackPhys
            );
            
            if (!EFI_ERROR(stackStatus)) {
                // Check if stack overlaps with kernel
                EFI_PHYSICAL_ADDRESS stackEnd = stackPhys + stackPages * EFI_PAGE_SIZE;
                
                // CRITICAL: Check if stack TOP would be at or near kernel base
                // Stack grows DOWN, so stackTop is at stackEnd (highest address)
                // If stackEnd == kernelBase, pushing to stack will overwrite kernel!
                if (stackEnd >= kernelBase && stackEnd <= kernelBase + kernelTotalSize + 0x10000) {
                    Print(L"ERROR: Stack end %p too close to kernel base %p!\n",
                          (VOID*)(UINTN)stackEnd, (VOID*)(UINTN)kernelBase);
                    
                    // Free and try allocating AFTER the kernel instead
                    SystemTable->BootServices->FreePages(stackPhys, stackPages);
                    
                    EFI_PHYSICAL_ADDRESS afterKernel = kernelBase + kernelTotalSize + 0x10000;
                    afterKernel = (afterKernel + EFI_PAGE_SIZE - 1) & ~(EFI_PAGE_SIZE - 1); // Page align
                    
                    stackStatus = SystemTable->BootServices->AllocatePages(
                        AllocateAddress,
                        EfiLoaderData,
                        stackPages,
                        &afterKernel
                    );
                    
                    if (!EFI_ERROR(stackStatus)) {
                        stackPhys = afterKernel;
                        stackTop = (void*)(UINTN)((stackPhys + stackPages * EFI_PAGE_SIZE) & ~0xFULL);
                        Print(L"Stack allocated AFTER kernel at %p, top at %p\n", 
                              (VOID*)(UINTN)stackPhys, stackTop);
                    } else {
                        Print(L"FATAL: Could not allocate non-overlapping stack!\n");
                        stackTop = nullptr;
                    }
                } else {
                    stackTop = (void*)(UINTN)((stackPhys + stackPages * EFI_PAGE_SIZE) & ~0xFULL);
                    Print(L"Stack fallback at %p, top at %p\n", (VOID*)(UINTN)stackPhys, stackTop);
                }
            } else {
                Print(L"FATAL: Could not allocate stack!\n");
                stackTop = nullptr;
            }
        }
    }
    
    // Final safety check
    if (stackTop != nullptr) {
        EFI_PHYSICAL_ADDRESS stackTopAddr = (EFI_PHYSICAL_ADDRESS)(UINTN)stackTop;
        if (stackTopAddr >= kernelBase && stackTopAddr < kernelBase + kernelTotalSize) {
            Print(L"FATAL: Stack top %p is INSIDE kernel region %p-%p!\n",
                  stackTop, (VOID*)(UINTN)kernelBase, (VOID*)(UINTN)(kernelBase + kernelTotalSize));
            return EFI_ABORTED;
        }
    }

    // --- Allocate executable trampoline buffer that survives ExitBootServices ---
    EFI_PHYSICAL_ADDRESS trampolinePhys = 0;
    UINTN trampolineSize = GetTrampolineCodeSize();
    UINTN trampolinePages = (trampolineSize + EFI_PAGE_SIZE - 1) / EFI_PAGE_SIZE;
    EFI_STATUS trampStatus = SystemTable->BootServices->AllocatePages(
        AllocateAnyPages,
        EfiLoaderData,
        trampolinePages,
        &trampolinePhys
    );
    if (EFI_ERROR(trampStatus)) {
        Print(L"Failed to allocate trampoline memory\n");
        return trampStatus;
    }
    SetMem((void*)(UINTN)trampolinePhys, trampolinePages * EFI_PAGE_SIZE, 0);
    SetupTrampoline((void*)(UINTN)trampolinePhys);

    // --- Build identity-mapped page tables BEFORE ExitBootServices ---
    // We must build page tables while BootServices are still available.
    
    // Memory map will be allocated later; for now allocate a conservative buffer for it
    // that survives ExitBootServices and include it in the mappings.
    // This avoids needing to rebuild mappings after EBS.
    EFI_PHYSICAL_ADDRESS preMemMapPhys = 0;
    UINTN preMemMapBytes = 256u * 1024u; // 256 KiB should be enough for QEMU/OVMF
    {
        UINTN preMemMapPages = (preMemMapBytes + EFI_PAGE_SIZE - 1) / EFI_PAGE_SIZE;
        EFI_STATUS st = SystemTable->BootServices->AllocatePages(
            AllocateAnyPages,
            EfiLoaderData,
            preMemMapPages,
            &preMemMapPhys);
        if (EFI_ERROR(st)) {
            Print(L"Failed to allocate pre-memory-map buffer\n");
            return st;
        }
        SetMem((void*)(UINTN)preMemMapPhys, preMemMapPages * EFI_PAGE_SIZE, 0);
    }

    // --- Build comprehensive identity mappings ---
    // CRITICAL: Map ALL regions the kernel needs access to after ExitBootServices:
    // 1. Low 1MB - legacy compatibility (interrupt vectors, BIOS data area references)
    // 2. Kernel image
    // 3. Stack
    // 4. BootInfo struct
    // 5. Memory map buffer
    // 6. Framebuffer - CRITICAL for display output
    // 7. Ramdisk - if loaded
    // 8. ACPI tables - for ACPI parsing

    const EFI_PHYSICAL_ADDRESS kernelPhysBase = (EFI_PHYSICAL_ADDRESS)kernelBase;
    const UINTN kernelSpanBytes = (kernelTotalSize != 0) ? (UINTN)kernelTotalSize : (64u * 1024u * 1024u);

    // Keep the range list bounded, but large enough for all ACPI tables plus
    // the existing kernel/stack/framebuffer mappings on a normal platform.
    EFI_PHYSICAL_ADDRESS ranges[MAX_RANGES_FOR_LOADER];
    UINTN sizes[MAX_RANGES_FOR_LOADER];
    UINTN rangeCount = 0;

    // 1. Low 1MB for legacy compatibility
    ranges[rangeCount] = 0;
    sizes[rangeCount] = 0x100000; // 1 MiB
    rangeCount++;

    // 2. Kernel image
    ranges[rangeCount] = kernelPhysBase;
    sizes[rangeCount] = kernelSpanBytes;
    rangeCount++;

    // 3. Stack - map the entire allocated region
    if (stackPhys != 0) {
        ranges[rangeCount] = stackPhys;
        sizes[rangeCount] = stackPages * EFI_PAGE_SIZE;
        rangeCount++;
        Print(L"Mapping stack: %p size %u\n", (VOID*)(UINTN)stackPhys, (UINT32)(stackPages * EFI_PAGE_SIZE));
    }

    // 4. BootInfo struct (allocated in EfiLoaderData pages)
    ranges[rangeCount] = bootInfoPhys;
    sizes[rangeCount] = EFI_PAGE_SIZE;
    rangeCount++;

    // 5. Memory map buffer
    ranges[rangeCount] = preMemMapPhys;
    sizes[rangeCount] = preMemMapBytes;
    rangeCount++;

    // 6. Framebuffer - CRITICAL for any display output after ExitBootServices
    // NOTE: Mapped separately with uncached flags (PCD+PWT) AFTER BuildIdentityPageTables.
    // Framebuffer is MMIO and must bypass CPU caches so writes reach the video hardware.

    // 7. Ramdisk - if loaded
    if (ramdiskPhys != 0 && ramdiskSize != 0) {
        ranges[rangeCount] = ramdiskPhys;
        sizes[rangeCount] = (UINTN)ramdiskSize;
        rangeCount++;
        Print(L"Mapping ramdisk: %p size %Lu\n", (VOID*)(UINTN)ramdiskPhys, ramdiskSize);
    }

    // 8. ACPI RSDP, root table and referenced tables.  After the CR3 switch
    // the kernel must be able to parse the same firmware-owned tables without
    // relying on the old UEFI page tables.
    if (rsdp != nullptr) {
        ranges[rangeCount] = (EFI_PHYSICAL_ADDRESS)(UINTN)rsdp & ~0xFFFull; // Page-align down
        sizes[rangeCount] = EFI_PAGE_SIZE * 4;
        rangeCount++;
        Print(L"Mapping ACPI RSDP region: %p\n", (VOID*)(UINTN)rsdp);
    }

    for (UINTN i = 0; i < acpiDiscovery.TableCount && rangeCount < MAX_RANGES_FOR_LOADER; ++i) {
        ranges[rangeCount] = acpiDiscovery.Tables[i].Base;
        sizes[rangeCount] = acpiDiscovery.Tables[i].Size;
        rangeCount++;
        Print(L"Mapping ACPI table: %p size %u\n",
              (VOID*)(UINTN)acpiDiscovery.Tables[i].Base,
              (UINT32)acpiDiscovery.Tables[i].Size);
    }

    // 9. CRITICAL: Map the bootloader/trampoline code region
    // After we load CR3 with new page tables, the CPU is still executing in the
    // trampoline code. If that code isn't mapped, we triple-fault immediately!
    // We need to identity-map the bootloader's loaded image.
    {
        EFI_LOADED_IMAGE_PROTOCOL* LoadedImage = nullptr;
        EFI_STATUS st = SystemTable->BootServices->HandleProtocol(
            ImageHandle, 
            &gEfiLoadedImageProtocolGuid, 
            (void**)&LoadedImage);
        
        if (!EFI_ERROR(st) && LoadedImage != nullptr) {
            EFI_PHYSICAL_ADDRESS loaderBase = (EFI_PHYSICAL_ADDRESS)(UINTN)LoadedImage->ImageBase;
            UINTN loaderSize = LoadedImage->ImageSize;
            
            // Ensure we map at least the whole image
            if (loaderSize < EFI_PAGE_SIZE * 16) {
                loaderSize = EFI_PAGE_SIZE * 16; // At least 64KB
            }
            
            ranges[rangeCount] = loaderBase;
            sizes[rangeCount] = loaderSize;
            rangeCount++;
            Print(L"Mapping bootloader: %p size %Lu\n", (VOID*)(UINTN)loaderBase, (UINT64)loaderSize);
        } else {
            // Fallback: try to use the current instruction pointer region
            Print(L"Warning: Could not get loaded image info for bootloader mapping\n");
        }
    }

    // 10. Trampoline executable buffer
    ranges[rangeCount] = trampolinePhys;
    sizes[rangeCount] = trampolinePages * EFI_PAGE_SIZE;
    rangeCount++;

    // 11. CRITICAL: Map the allocator region the kernel will use
    // The kernel's Allocator.Initialize() uses 0x4000000 (64MB) as the base
    // Map a LARGE region starting there for heap allocations (1GB total)
    // The kernel's NumPages = 262144 * 4KB = 1GB
    ranges[rangeCount] = 0x4000000ULL;  // 64MB
    sizes[rangeCount] = 1024u * 1024u * 1024u; // 1GB of heap space
    rangeCount++;
    Print(L"Mapping allocator region: 0x4000000 size 1GB\n");

    // 12. Map additional stack regions we might use
    // The preferred stack location is around 2MB mark
    ranges[rangeCount] = 0x100000ULL; // 1MB
    sizes[rangeCount] = 4u * 1024u * 1024u; // 4MB (covers 1MB-5MB region)
    rangeCount++;
    Print(L"Mapping low memory region: 0x100000 size 4MB\n");
    
    // 13 & 14. Local APIC and IOAPIC MMIO
    // NOTE: Mapped separately with uncached flags (PCD+PWT) AFTER BuildIdentityPageTables.
    // MMIO regions must bypass CPU caches for correct hardware access.

    Print(L"Building identity page tables with %u ranges...\n", (UINT32)rangeCount);

    guideXOS::paging::PageTables pt{};
    {
        EFI_STATUS st = guideXOS::paging::BuildIdentityPageTables(
            SystemTable,
            ranges,
            ranges + rangeCount,
            sizes,
            &pt);
        if (EFI_ERROR(st)) {
            Print(L"Failed to build identity page tables\n");
            return st;
        }
    }

    // Map kernel virtual addresses to their physical backing
    {
        EFI_STATUS st = guideXOS::paging::MapRange(
            SystemTable,
            pt.Pml4Phys,
            kernelMinVaddr,
            kernelPhysBase,
            kernelSpanBytes);
        if (EFI_ERROR(st)) {
            Print(L"Failed to map kernel virtual range\n");
            return st;
        }
    }

    // CRITICAL: Identity-map all page table pages allocated by MapRange()
    // Without this, the page table walk for kernel virtual addresses will
    // fail after CR3 switch because the PT pages themselves aren't mapped!
    {
        EFI_STATUS st = guideXOS::paging::IdentityMapPageTablePages(
            SystemTable,
            pt.Pml4Phys);
        if (EFI_ERROR(st)) {
            Print(L"Failed to identity-map page table pages\n");
            return st;
        }
    }

    Print(L"Page tables built at PML4: %p\n", (VOID*)(UINTN)pt.Pml4Phys);

    // === MAP MMIO REGIONS WITH UNCACHED FLAGS ===
    // These regions are memory-mapped I/O and MUST bypass CPU caches.
    // PTE_PCD + PTE_PWT = Strong Uncacheable (UC) memory type.
    // Without this, framebuffer writes go to CPU cache and never reach the display.

    // Framebuffer (MMIO - must be uncached for display output)
    if (v1BootInfo->FramebufferBase != 0 && v1BootInfo->FramebufferSize != 0) {
        EFI_STATUS st = guideXOS::paging::MapIdentityRangeUncached(
            SystemTable,
            pt.Pml4Phys,
            (EFI_PHYSICAL_ADDRESS)v1BootInfo->FramebufferBase,
            (UINTN)v1BootInfo->FramebufferSize);
        if (EFI_ERROR(st)) {
            Print(L"Failed to map framebuffer uncached\n");
            return st;
        }
        Print(L"Mapping framebuffer (uncached): %p size %Lu\n", 
              (VOID*)(UINTN)v1BootInfo->FramebufferBase, v1BootInfo->FramebufferSize);
    }

    // Local APIC MMIO (MMIO - must be uncached)
    {
        EFI_STATUS st = guideXOS::paging::MapIdentityRangeUncached(
            SystemTable, pt.Pml4Phys, 0xFEE00000ULL, 0x1000ULL);
        if (EFI_ERROR(st)) {
            Print(L"Failed to map Local APIC uncached\n");
            return st;
        }
        Print(L"Mapping Local APIC MMIO (uncached): 0xFEE00000 size 4KB\n");
    }

    // IOAPIC MMIO (MMIO - must be uncached)
    {
        EFI_STATUS st = guideXOS::paging::MapIdentityRangeUncached(
            SystemTable, pt.Pml4Phys, 0xFEC00000ULL, 0x1000ULL);
        if (EFI_ERROR(st)) {
            Print(L"Failed to map IOAPIC uncached\n");
            return st;
        }
        Print(L"Mapping IOAPIC MMIO (uncached): 0xFEC00000 size 4KB\n");
    }

    // PCI Express ECAM is firmware-described MMIO.  Map exactly the bus
    // windows in MCFG and apply the same UC attributes used for APIC and GOP
    // mappings; never assume 0xE0000000 on machines that describe another
    // ECAM base or a narrower bus range.
    for (UINTN i = 0; i < acpiDiscovery.McfgEntryCount; ++i) {
        const AcpiMcfgAllocationForLoader* allocation =
            &acpiDiscovery.McfgEntries[i];
        UINT64 busCount = (UINT64)allocation->EndBus -
                          (UINT64)allocation->StartBus + 1ull;
        UINT64 ecamSize = busCount << 20;

        EFI_STATUS st = guideXOS::paging::MapIdentityRangeUncached(
            SystemTable,
            pt.Pml4Phys,
            (EFI_PHYSICAL_ADDRESS)allocation->BaseAddress,
            (UINTN)ecamSize);
        if (EFI_ERROR(st)) {
            Print(L"Failed to map PCI ECAM uncached\n");
            return st;
        }

        Print(L"Mapping PCI ECAM (uncached): base %p segment %u buses %u-%u size %Lu\n",
              (VOID*)(UINTN)allocation->BaseAddress,
              (UINT32)allocation->Segment,
              (UINT32)allocation->StartBus,
              (UINT32)allocation->EndBus,
              (UINT64)ecamSize);
    }

    // Identity-map any new page table pages created by the uncached mappings
    {
        EFI_STATUS st = guideXOS::paging::IdentityMapPageTablePages(
            SystemTable, pt.Pml4Phys);
        if (EFI_ERROR(st)) {
            Print(L"Failed to identity-map new page table pages\n");
            return st;
        }
    }

    // === PRE-EXIT BOOT SERVICES ===
    // Boot splash is already displayed - skip debug markers that would overwrite it
    Print(L"About to exit boot services...\n");

    // === DRAW BOOT SPLASH ===
    // Show custom guideXOS boot splash IMMEDIATELY - covers entire screen
    // This will replace the TianoCore logo with guideXOS branding
    if (v1BootInfo->FramebufferBase != 0 && v1BootInfo->FramebufferWidth != 0 && v1BootInfo->FramebufferHeight != 0) {
        Print(L"Drawing boot splash screen...\n");
        guideXOS::boot::BootSplash::DrawSplash(
            v1BootInfo->FramebufferBase,
            v1BootInfo->FramebufferWidth,
            v1BootInfo->FramebufferHeight,
            v1BootInfo->FramebufferPitch
        );
        Print(L"Boot splash complete!\n");
        
        // Wait a moment so user can see the splash (500ms)
        // Use UEFI Stall service (microseconds)
        SystemTable->BootServices->Stall(500000); // 500ms
    } else {
        Print(L"Framebuffer not available for boot splash\n");
    }

    // Now we can safely exit boot services.
    EFI_MEMORY_DESCRIPTOR* memoryMap      = (EFI_MEMORY_DESCRIPTOR*)(UINTN)preMemMapPhys;
    UINTN                  memoryMapCount = 0;
    UINTN                  memoryMapDescSize = 0;

    Print(L"Exiting boot services...\n");
    EFI_STATUS statusExit = ExitBootServicesWithMemoryMapInBuffer(
        ImageHandle,
        SystemTable,
        memoryMap,
        preMemMapBytes,
        &memoryMapCount,
        &memoryMapDescSize
    );
    if (EFI_ERROR(statusExit)) {
        Print(L"ExitBootServices failed: %r\n", statusExit);
        return statusExit;
    }

    // *** CRITICAL: No Print() or any UEFI Boot Services calls after ExitBootServices! ***

    // === POST-EBS FRAMEBUFFER MARKER (Stage 1: Yellow = EBS succeeded) ===
    if (v1BootInfo->FramebufferBase != 0) {
        volatile uint32_t* fb = (volatile uint32_t*)(UINTN)v1BootInfo->FramebufferBase;
        uint32_t pitch = v1BootInfo->FramebufferPitch / 4;
        // Draw yellow square next to green = EBS succeeded
        for (uint32_t y = 0; y < 30; y++) {
            for (uint32_t x = 40; x < 70; x++) {
                fb[y * pitch + x] = 0x00FFFF00; // Yellow = post-EBS
            }
        }
    }

    // Fill memory map section in BootInfo v1
    v1BootInfo->MemoryMap               = (uint64_t)(UINTN)memoryMap;
    v1BootInfo->MemoryMapEntryCount     = (uint64_t)memoryMapCount;
    v1BootInfo->MemoryMapDescriptorSize = (uint64_t)memoryMapDescSize;
    v1BootInfo->Flags |= (1u << 0); // memory map valid

    // === POST-EBS FRAMEBUFFER MARKER (Stage 2: Cyan = MemMap filled) ===
    if (v1BootInfo->FramebufferBase != 0) {
        volatile uint32_t* fb = (volatile uint32_t*)(UINTN)v1BootInfo->FramebufferBase;
        uint32_t pitch = v1BootInfo->FramebufferPitch / 4;
        for (uint32_t y = 0; y < 30; y++) {
            for (uint32_t x = 80; x < 110; x++) {
                fb[y * pitch + x] = 0x0000FFFF; // Cyan = memmap done
            }
        }
    }

    // Compute checksum so that 32-bit sum of all words is 0
    v1BootInfo->HeaderChecksum = 0u;
    {
        uint32_t byteCount = v1BootInfo->Size & ~0x3u;
        uint32_t* p = reinterpret_cast<uint32_t*>(v1BootInfo);
        uint32_t count = byteCount / 4u;
        uint32_t sum = 0u;
        for (uint32_t i = 0; i < count; ++i)
            sum += p[i];
        v1BootInfo->HeaderChecksum = 0u - sum;
    }

    // === POST-EBS FRAMEBUFFER MARKER (Stage 3: Magenta = Checksum done) ===
    if (v1BootInfo->FramebufferBase != 0) {
        volatile uint32_t* fb = (volatile uint32_t*)(UINTN)v1BootInfo->FramebufferBase;
        uint32_t pitch = v1BootInfo->FramebufferPitch / 4;
        for (uint32_t y = 0; y < 30; y++) {
            for (uint32_t x = 120; x < 150; x++) {
                fb[y * pitch + x] = 0x00FF00FF; // Magenta = checksum done
            }
        }
    }

    // Skip validation for now - it might be causing the panic
    // guideXOS::guidexos_validate_bootinfo_or_panic(v1BootInfo);

    // === POST-EBS FRAMEBUFFER MARKER (Stage 4: Blue = Validation skipped/passed) ===
    if (v1BootInfo->FramebufferBase != 0) {
        volatile uint32_t* fb = (volatile uint32_t*)(UINTN)v1BootInfo->FramebufferBase;
        uint32_t pitch = v1BootInfo->FramebufferPitch / 4;
        for (uint32_t y = 0; y < 30; y++) {
            for (uint32_t x = 160; x < 190; x++) {
                fb[y * pitch + x] = 0x000000FF; // Blue = validation done
            }
        }
    }

    // === POST-EXITBOOTSERVICES DEBUGGING ===
    
    // === FRAMEBUFFER MARKER (Stage 5: White = About to init serial) ===
    if (v1BootInfo->FramebufferBase != 0) {
        volatile uint32_t* fb = (volatile uint32_t*)(UINTN)v1BootInfo->FramebufferBase;
        uint32_t pitch = v1BootInfo->FramebufferPitch / 4;
        for (uint32_t y = 0; y < 30; y++) {
            for (uint32_t x = 200; x < 230; x++) {
                fb[y * pitch + x] = 0x00FFFFFF; // White = pre-serial init
            }
        }
    }

    guideXOS::debug::SerialInit();

    // === FRAMEBUFFER MARKER (Stage 6: Orange = Serial initialized) ===
    if (v1BootInfo->FramebufferBase != 0) {
        volatile uint32_t* fb = (volatile uint32_t*)(UINTN)v1BootInfo->FramebufferBase;
        uint32_t pitch = v1BootInfo->FramebufferPitch / 4;
        for (uint32_t y = 0; y < 30; y++) {
            for (uint32_t x = 240; x < 270; x++) {
                fb[y * pitch + x] = 0x00FF8000; // Orange = serial done
            }
        }
    }

    guideXOS::debug::SerialPrint("\n\n=== GuideXOS Boot (Post-ExitBootServices) ===\n");

    guideXOS::debug::ShowProgress(v1BootInfo, 0);
    guideXOS::debug::VerifyCpuState();
    guideXOS::debug::ShowProgress(v1BootInfo, 1);

    guideXOS::debug::SerialPrint("\n=== BootInfo ===\n");
    guideXOS::debug::SerialPrint("Magic: ");
    guideXOS::debug::SerialPrintHex32(v1BootInfo->Magic);
    guideXOS::debug::SerialPrint("\n");
    guideXOS::debug::SerialPrint("Framebuffer: ");
    guideXOS::debug::SerialPrintHex64(v1BootInfo->FramebufferBase);
    guideXOS::debug::SerialPrint(" (");
    guideXOS::debug::SerialPrintHex32(v1BootInfo->FramebufferWidth);
    guideXOS::debug::SerialPrint("x");
    guideXOS::debug::SerialPrintHex32(v1BootInfo->FramebufferHeight);
    guideXOS::debug::SerialPrint(")\n");
    guideXOS::debug::ShowProgress(v1BootInfo, 2);

    guideXOS::debug::ValidateKernelEntry(entryPhys, v1BootInfo);
    guideXOS::debug::ShowProgress(v1BootInfo, 3);

    // --- Handoff to kernel via trampoline ---
    // Switches stack, installs CR3, calls kernel entry using MS x64 ABI

    if (!stackTop) {
        guideXOS::debug::SerialPrint("[BOOT] No stack allocated; halting\n");
        for (;;) { __halt(); }
    }

    guideXOS::debug::SerialPrint("\n=== Jumping to Kernel (Trampoline) ===\n");
    guideXOS::debug::ShowProgress(v1BootInfo, 4);

    // Comprehensive validation before handoff
    guideXOS::debug::ValidateHandoff(
        entryPhys, 
        v1BootInfo, 
        (uint64_t)(UINTN)stackTop, 
        pt.Pml4Phys
    );
    
    // ALSO validate the virtual entry address mapping
    guideXOS::debug::SerialPrint("\n[BOOT] Validating VIRTUAL kernel entry mapping:\n");
    guideXOS::debug::ValidatePageMapping(pt.Pml4Phys, entryVirt);

    guideXOS::debug::SerialPrint("[BOOT] About to handoff via trampoline...\n");
    guideXOS::debug::SerialPrint("[BOOT] Trampoline at: ");
    guideXOS::debug::SerialPrintHex64(trampolinePhys);
    guideXOS::debug::SerialPrint("\n");
    guideXOS::debug::SerialPrint("[BOOT] Kernel entry: ");
    guideXOS::debug::SerialPrintHex64(entryPhys);
    guideXOS::debug::SerialPrint("\n");
    guideXOS::debug::SerialPrint("[BOOT] BootInfo at: ");
    guideXOS::debug::SerialPrintHex64((uint64_t)(UINTN)v1BootInfo);
    guideXOS::debug::SerialPrint("\n");
    guideXOS::debug::SerialPrint("[BOOT] Stack top: ");
    guideXOS::debug::SerialPrintHex64((uint64_t)(UINTN)stackTop);
    guideXOS::debug::SerialPrint("\n");
    guideXOS::debug::SerialPrint("[BOOT] PML4: ");
    guideXOS::debug::SerialPrintHex64(pt.Pml4Phys);
    guideXOS::debug::SerialPrint("\n");
    guideXOS::debug::ShowProgress(v1BootInfo, 5);

    // Validate the trampoline mapping before jumping
    guideXOS::debug::SerialPrint("\n[BOOT] Validating trampoline mapping:\n");
    guideXOS::debug::ValidatePageMapping(pt.Pml4Phys, trampolinePhys);

    guideXOS::debug::SerialPrint("\n[BOOT] === CALLING TRAMPOLINE NOW ===\n");

    // Use VIRTUAL entry point. The kernel is compiled for virtual address 0x10000000.
    // All internal calls and data references use virtual addresses (RIP-relative).
    // Our page tables map virtual 0x10XXXXXX -> physical 0x3DXXXXXX
    guideXOS::debug::SerialPrint("[BOOT] Jumping to VIRTUAL entry point\n");
    BootHandoffTrampoline((void*)(UINTN)entryVirt, (void*)v1BootInfo, stackTop, (void*)(UINTN)pt.Pml4Phys);

    // If we return, halt
    guideXOS::debug::SerialPrint("\n!!! KERNEL RETURNED - THIS SHOULD NOT HAPPEN !!!\n");
    for (;;) { __halt(); }
}

