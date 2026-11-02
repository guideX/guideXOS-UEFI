#include <Uefi.h>
#include <Protocol/PciIo.h>

#include "xhci_probe_core.h"

static EFI_GUID gEfiPciIoGuid = EFI_PCI_IO_PROTOCOL_GUID;

struct UefiPlatformContext {
    EFI_BOOT_SERVICES* BootServices;
    EFI_HANDLE* Handles;
};

struct UefiBarQueryContext {
    EFI_BOOT_SERVICES* BootServices;
    EFI_PCI_IO_PROTOCOL* Pci;
};

static EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL* gConsole = nullptr;
static EFI_BOOT_SERVICES* gBootServices = nullptr;

static_assert(sizeof(wchar_t) == sizeof(CHAR16),
              "UEFI console string literals must use 16-bit Windows wchar_t");

static void ConsoleWrite(const CHAR16* text) {
    if (gConsole != nullptr && gConsole->OutputString != nullptr && text != nullptr)
        (void)gConsole->OutputString(gConsole, (CHAR16*)text);
}

static void ConsoleWrite(const wchar_t* text) {
    ConsoleWrite((const CHAR16*)text);
}

static void ConsoleWriteHex(uint64_t value, uint32_t digits) {
    static const wchar_t hexDigits[] = L"0123456789ABCDEF";
    CHAR16 text[19];
    if (digits > 16) digits = 16;
    text[0] = (CHAR16)L'0';
    text[1] = (CHAR16)L'x';
    for (uint32_t index = 0; index < digits; ++index) {
        uint32_t shift = (digits - index - 1) * 4;
        text[index + 2] = (CHAR16)hexDigits[(value >> shift) & 0xFULL];
    }
    text[digits + 2] = 0;
    ConsoleWrite(text);
}

static void ConsoleWriteDecimal(uint32_t value) {
    CHAR16 digits[11];
    uint32_t count = 0;
    do {
        digits[count++] = (CHAR16)(L'0' + (value % 10));
        value /= 10;
    } while (value != 0 && count < sizeof(digits) / sizeof(digits[0]));
    CHAR16 text[12];
    for (uint32_t index = 0; index < count; ++index)
        text[index] = digits[count - index - 1];
    text[count] = 0;
    ConsoleWrite(text);
}

static void ConsoleWriteStatus(uint64_t status) {
    ConsoleWrite(L" EFI_STATUS=");
    ConsoleWriteHex(status, 16);
}

static const wchar_t* FailureStageName(guideXOS::xhci_probe::FailureStage stage) {
    using guideXOS::xhci_probe::FailureStage;
    switch (stage) {
    case FailureStage::None: return L"success";
    case FailureStage::OpenProtocol: return L"OpenProtocol";
    case FailureStage::GetLocation: return L"GetLocation";
    case FailureStage::ReadIdentity: return L"PCI identity read";
    case FailureStage::ReadClass: return L"PCI class read";
    case FailureStage::InvalidBdf: return L"invalid PCI location";
    case FailureStage::ReadBarConfiguration: return L"BAR0 configuration read";
    case FailureStage::InvalidBarConfiguration: return L"unsupported BAR0 configuration";
    case FailureStage::GetBarAttributes: return L"GetBarAttributes";
    case FailureStage::MalformedBarDescriptor: return L"malformed BAR0 resource descriptor";
    case FailureStage::InvalidBarResource: return L"invalid or undersized BAR0 resource";
    case FailureStage::ReadCapabilities: return L"EFI_PCI_IO_PROTOCOL.Mem.Read";
    case FailureStage::MalformedCapabilityHeader: return L"malformed capability header";
    case FailureStage::InvalidCapabilities: return L"invalid capability values or offsets";
    default: return L"unknown";
    }
}

static void PrintController(const guideXOS::xhci_probe::ProbeResult* result) {
    using namespace guideXOS::xhci_probe;
    ConsoleWrite(L"\r\nPCI controller: ");
    ConsoleWriteHex(result->PciLocation.Segment, 4);
    ConsoleWrite(L":");
    ConsoleWriteHex(result->PciLocation.Bus, 2);
    ConsoleWrite(L":");
    ConsoleWriteHex(result->PciLocation.Device, 2);
    ConsoleWrite(L".");
    ConsoleWriteHex(result->PciLocation.Function, 1);
    ConsoleWrite(L"\r\n  Vendor ID: ");
    ConsoleWriteHex(result->VendorId, 4);
    ConsoleWrite(L"  Device ID: ");
    ConsoleWriteHex(result->DeviceId, 4);
    ConsoleWrite(L"\r\n  PCI class/subclass/interface: ");
    ConsoleWriteHex(result->ClassCode, 2);
    ConsoleWrite(L"/");
    ConsoleWriteHex(result->Subclass, 2);
    ConsoleWrite(L"/");
    ConsoleWriteHex(result->ProgrammingInterface, 2);
    ConsoleWrite(L"\r\n  BAR0 firmware resource: ");
    ConsoleWrite(result->BarUsable ? L"usable" : L"unusable");
    if (result->HasBarInfo) {
        ConsoleWrite(L"  width=");
        ConsoleWriteDecimal(result->Bar.AddressWidth);
        ConsoleWrite(L"  base=");
        ConsoleWriteHex(result->Bar.Base, 16);
        ConsoleWrite(L"  length=");
        ConsoleWriteHex(result->Bar.Length, 16);
        ConsoleWrite(L"  max=");
        ConsoleWriteHex(result->Bar.Maximum, 16);
        ConsoleWrite(L"  translation=");
        ConsoleWriteHex(result->Bar.TranslationOffset, 16);
        ConsoleWrite(L"\r\n  BAR0 attributes: ");
        ConsoleWriteHex(result->Bar.SupportedAttributes, 16);
        ConsoleWrite(result->BarPrefetchable ? L"  prefetchable=yes" :
                                                  L"  prefetchable=no");
    } else if (result->BarConfigurationBase != 0) {
        ConsoleWrite(L"  config base=");
        ConsoleWriteHex(result->BarConfigurationBase, 16);
        ConsoleWrite(L"  config width=");
        ConsoleWriteDecimal(result->BarConfigurationWidth);
    }
    ConsoleWrite(L"\r\n");

    if (result->HasCapabilities) {
        const Capabilities& caps = result->Capability;
        ConsoleWrite(L"  Controller: xHCI ");
        ConsoleWriteHex(caps.ControllerVersion, 4);
        ConsoleWrite(L"  CAPLENGTH=");
        ConsoleWriteHex(caps.CapabilityLength, 2);
        ConsoleWrite(L"\r\n  Slots=");
        ConsoleWriteDecimal(caps.MaximumDeviceSlots);
        ConsoleWrite(L"  Root ports=");
        ConsoleWriteDecimal(caps.RootPortCount);
        ConsoleWrite(L"  Interrupters=");
        ConsoleWriteDecimal(caps.AdvertisedInterrupters);
        ConsoleWrite(L"  Scratchpads=");
        ConsoleWriteDecimal(caps.ScratchpadBufferCount);
        ConsoleWrite(L"\r\n  Context size: ");
        ConsoleWrite(caps.Uses64ByteContexts ? L"64-byte" : L"32-byte");
        ConsoleWrite(L"  64-bit addressing: ");
        ConsoleWrite(caps.Supports64BitAddresses ? L"yes" : L"no");
        ConsoleWrite(L"\r\n  DBOFF=");
        ConsoleWriteHex(caps.DoorbellOffset, 8);
        ConsoleWrite(L"  RTSOFF=");
        ConsoleWriteHex(caps.RuntimeRegisterOffset, 8);
        ConsoleWrite(L"\r\n  HCSPARAMS2=");
        ConsoleWriteHex(caps.HcsParams2, 8);
        ConsoleWrite(L"  HCSPARAMS3=");
        ConsoleWriteHex(caps.HcsParams3, 8);
        ConsoleWrite(L"  HCCPARAMS1=");
        ConsoleWriteHex(caps.HccParams1, 8);
        ConsoleWrite(L"\r\n");
        if (caps.HasHccParams2) {
            ConsoleWrite(L"  HCCPARAMS2=");
            ConsoleWriteHex(caps.HccParams2, 8);
            ConsoleWrite(L"\r\n");
        }
    }

    ConsoleWrite(L"  Status: ");
    if (result->Stage == FailureStage::None) {
        ConsoleWrite(L"success; read-only capability inspection complete\r\n");
    } else {
        ConsoleWrite(L"failed at ");
        ConsoleWrite(FailureStageName(result->Stage));
        if (IsErrorStatus(result->FirmwareStatus))
            ConsoleWriteStatus(result->FirmwareStatus);
        ConsoleWrite(L"\r\n");
    }
}

static void PrintSummary(const guideXOS::xhci_probe::ProbeSummary* summary) {
    ConsoleWrite(L"\r\nScan summary: handles=");
    ConsoleWriteDecimal(summary->HandlesScanned);
    ConsoleWrite(L" xHCI controllers=");
    ConsoleWriteDecimal(summary->ControllersFound);
    ConsoleWrite(L"\r\n");
    if (summary->ControllersFound == 0)
        ConsoleWrite(L"No xHCI controller was found in the scanned PCI handles.\r\n");
    if (summary->EnumerationErrors != 0) {
        ConsoleWrite(L"PCI enumeration errors: ");
        ConsoleWriteDecimal(summary->EnumerationErrors);
        ConsoleWrite(L"  first stage: ");
        ConsoleWrite(FailureStageName(summary->FirstEnumerationErrorStage));
        if (guideXOS::xhci_probe::IsErrorStatus(
                summary->FirstEnumerationErrorStatus))
            ConsoleWriteStatus(summary->FirstEnumerationErrorStatus);
        ConsoleWrite(L"\r\n");
    }
    if (summary->Truncated)
        ConsoleWrite(L"Scan bounded: additional PCI handles/controllers may remain.\r\n");
    ConsoleWrite(L"No PCI configuration or xHCI controller registers were written.\r\n");
    ConsoleWrite(L"Returning to UEFI firmware in 5 seconds.\r\n");
}

static void HoldConsoleBriefly() {
    if (gBootServices != nullptr && gBootServices->Stall != nullptr)
        (void)gBootServices->Stall(5000000);
}

static uint64_t UefiGetLocation(void* context,
                                guideXOS::xhci_probe::Location* location) {
    EFI_PCI_IO_PROTOCOL* pci = (EFI_PCI_IO_PROTOCOL*)context;
    if (pci == nullptr || pci->GetLocation == nullptr || location == nullptr)
        return EFI_INVALID_PARAMETER;
    UINTN segment = 0, bus = 0, device = 0, function = 0;
    EFI_STATUS status = pci->GetLocation(pci, &segment, &bus, &device, &function);
    if (EFI_ERROR(status)) return status;
    location->Segment = segment;
    location->Bus = bus;
    location->Device = device;
    location->Function = function;
    return status;
}

static uint64_t UefiReadConfig32(void* context, uint32_t offset,
                                uint32_t* value) {
    EFI_PCI_IO_PROTOCOL* pci = (EFI_PCI_IO_PROTOCOL*)context;
    if (pci == nullptr || pci->Pci.Read == nullptr || value == nullptr)
        return EFI_INVALID_PARAMETER;
    return pci->Pci.Read(pci, EfiPciIoWidthUint32, offset, 1, value);
}

static uint64_t UefiReadMem32(void* context, uint64_t offset,
                              uint32_t* value) {
    EFI_PCI_IO_PROTOCOL* pci = (EFI_PCI_IO_PROTOCOL*)context;
    if (pci == nullptr || pci->Mem.Read == nullptr || value == nullptr)
        return EFI_INVALID_PARAMETER;
    return pci->Mem.Read(pci, EfiPciIoWidthUint32, 0, offset, 1, value);
}

static uint64_t UefiGetBarDescriptor(void* context,
        uint64_t* supportedAttributes, const uint8_t** descriptorBytes) {
    UefiBarQueryContext* query = (UefiBarQueryContext*)context;
    if (query == nullptr || query->Pci == nullptr ||
        query->Pci->GetBarAttributes == nullptr ||
        query->BootServices == nullptr || supportedAttributes == nullptr ||
        descriptorBytes == nullptr) return EFI_INVALID_PARAMETER;
    VOID* resources = nullptr;
    UINT64 attributes = 0;
    EFI_STATUS status = query->Pci->GetBarAttributes(
        query->Pci, 0, &attributes, &resources);
    *supportedAttributes = attributes;
    *descriptorBytes = (const uint8_t*)resources;
    return status;
}

static uint64_t UefiReleaseDescriptor(void* context,
                                      const uint8_t* descriptorBytes) {
    UefiBarQueryContext* query = (UefiBarQueryContext*)context;
    if (query == nullptr || query->BootServices == nullptr ||
        descriptorBytes == nullptr) return EFI_INVALID_PARAMETER;
    return query->BootServices->FreePool((VOID*)descriptorBytes);
}

static uint64_t UefiGetBar0(void* context,
                            guideXOS::xhci_probe::BarResource* resource) {
    EFI_PCI_IO_PROTOCOL* pci = (EFI_PCI_IO_PROTOCOL*)context;
    if (pci == nullptr || gBootServices == nullptr)
        return EFI_INVALID_PARAMETER;
    UefiBarQueryContext query = { gBootServices, pci };
    return guideXOS::xhci_probe::TryGetBarResource(
        &query, UefiGetBarDescriptor, UefiReleaseDescriptor, resource);
}

static uint64_t UefiOpenController(void* context, uint32_t index,
                                   guideXOS::xhci_probe::ControllerOps* controller) {
    UefiPlatformContext* platform = (UefiPlatformContext*)context;
    if (platform == nullptr || platform->BootServices == nullptr ||
        platform->Handles == nullptr || controller == nullptr)
        return EFI_INVALID_PARAMETER;
    EFI_PCI_IO_PROTOCOL* pci = nullptr;
    EFI_STATUS status = platform->BootServices->HandleProtocol(
        platform->Handles[index], &gEfiPciIoGuid, (VOID**)&pci);
    if (EFI_ERROR(status)) return status;
    if (pci == nullptr) return EFI_DEVICE_ERROR;
    controller->Context = pci;
    controller->GetLocation = UefiGetLocation;
    controller->ReadConfig32 = UefiReadConfig32;
    controller->GetBar0 = UefiGetBar0;
    controller->ReadMem32 = UefiReadMem32;
    return EFI_SUCCESS;
}

static void UefiReportController(void*,
        const guideXOS::xhci_probe::ProbeResult* result) {
    PrintController(result);
}

static void UefiReportSummary(void*,
        const guideXOS::xhci_probe::ProbeSummary* summary) {
    PrintSummary(summary);
}

extern "C" EFI_STATUS EFIAPI efi_main(EFI_HANDLE, EFI_SYSTEM_TABLE* systemTable) {
    if (systemTable == nullptr || systemTable->BootServices == nullptr ||
        systemTable->ConOut == nullptr) return EFI_INVALID_PARAMETER;

    gConsole = systemTable->ConOut;
    gBootServices = systemTable->BootServices;
    if (gConsole->SetAttribute != nullptr)
        (void)gConsole->SetAttribute(gConsole, 0x07);
    if (gConsole->ClearScreen != nullptr)
        (void)gConsole->ClearScreen(gConsole);

    ConsoleWrite(L"guideXOS xHCI Diagnostic\r\n");
    ConsoleWrite(L"Read-only UEFI PCI probe; no controller initialization.\r\n");
    ConsoleWrite(L"Maximum scan: 256 PCI handles, 8 xHCI controllers.\r\n");

    EFI_HANDLE* handles = nullptr;
    UINTN handleCount = 0;
    EFI_STATUS status = gBootServices->LocateHandleBuffer(
        ByProtocol, &gEfiPciIoGuid, nullptr, &handleCount, &handles);
    if (status == EFI_NOT_FOUND) {
        ConsoleWrite(L"No EFI_PCI_IO_PROTOCOL handles are available.\r\n");
        ConsoleWrite(L"Returning to UEFI firmware in 5 seconds.\r\n");
        if (handles != nullptr) (void)gBootServices->FreePool(handles);
        HoldConsoleBriefly();
        return EFI_SUCCESS;
    }
    if (EFI_ERROR(status) || (handleCount != 0 && handles == nullptr)) {
        ConsoleWrite(L"PCI handle enumeration failed");
        ConsoleWriteStatus(status);
        ConsoleWrite(L"\r\nReturning to UEFI firmware in 5 seconds.\r\n");
        if (handles != nullptr) (void)gBootServices->FreePool(handles);
        HoldConsoleBriefly();
        return status;
    }

    UefiPlatformContext platformContext = { gBootServices, handles };
    guideXOS::xhci_probe::PlatformOps platform = {
        &platformContext, (uint64_t)handleCount, UefiOpenController
    };
    guideXOS::xhci_probe::ReporterOps reporter = {
        nullptr, UefiReportController, UefiReportSummary
    };
    (void)guideXOS::xhci_probe::RunProbe(platform, reporter);

    EFI_STATUS freeStatus = EFI_SUCCESS;
    if (handles != nullptr) freeStatus = gBootServices->FreePool(handles);
    if (EFI_ERROR(freeStatus)) {
        ConsoleWrite(L"Failed to release PCI handle array");
        ConsoleWriteStatus(freeStatus);
        ConsoleWrite(L"\r\n");
    }
    HoldConsoleBriefly();
    return EFI_SUCCESS;
}
