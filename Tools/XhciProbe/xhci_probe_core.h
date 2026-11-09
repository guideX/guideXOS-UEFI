#pragma once

#include <stddef.h>
#include <stdint.h>

#include "../../guideXOSBootLoader/pci_resource_parser.h"

namespace guideXOS {
namespace xhci_probe {

static const uint64_t UEFI_ERROR_BIT = 0x8000000000000000ULL;
static const uint64_t EFI_DEVICE_ERROR_VALUE = UEFI_ERROR_BIT | 7ULL;
static const uint64_t EFI_INVALID_PARAMETER_VALUE = UEFI_ERROR_BIT | 2ULL;
static const uint32_t MAX_HANDLES_TO_SCAN = 256;
static const uint32_t MAX_CONTROLLERS_TO_PROBE = 8;
static const uint64_t MINIMUM_CAPABILITY_APERTURE = 0x20;
static const uint32_t HCCPARAMS2_OFFSET = 0x1C;

inline bool IsErrorStatus(uint64_t status) {
    return (status & UEFI_ERROR_BIT) != 0;
}

// The standalone EFI image intentionally has no CRT. Volatile byte stores
// keep MSVC from lowering large aggregate zero-initialization to memset.
inline void ZeroBytes(void* buffer, size_t byteCount) {
    volatile uint8_t* bytes = (volatile uint8_t*)buffer;
    for (size_t index = 0; index < byteCount; ++index) bytes[index] = 0;
}

struct BarResource {
    bool DescriptorValid;
    bool IsMemory;
    bool HasDescriptorHeader;
    bool HasQwordFields;
    bool HasListTail;
    bool HasGetBarAttributesStatus;
    uint8_t AddressWidth;
    guideXOS::pci_resource::ResourceMaximumEncoding MaximumEncoding;
    uint8_t GeneralFlags;
    uint8_t SpecificFlags;
    uint8_t DescriptorType;
    uint8_t ResourceType;
    uint8_t ListTailByte0;
    uint8_t ListTailByte1;
    uint16_t DescriptorLength;
    uint64_t Base;
    uint64_t Maximum;
    uint64_t Length;
    uint64_t TranslationOffset;
    uint64_t SupportedAttributes;
    uint64_t GetBarAttributesStatus;
    uint64_t RawGranularity;
    uint64_t RawBase;
    uint64_t RawMaximum;
    uint64_t RawTranslationOffset;
    uint64_t RawLength;
};

typedef uint64_t (*GetBarDescriptor)(void* context,
                                     uint64_t* supportedAttributes,
                                     const uint8_t** descriptorBytes);
typedef uint64_t (*ReleaseDescriptor)(void* context,
                                      const uint8_t* descriptorBytes);

// GetBarAttributes returns a firmware-owned ACPI resource list. Parse it while
// the buffer is live, then release it on every path that returned a buffer.
inline uint64_t TryGetBarResource(void* context,
        GetBarDescriptor getDescriptor, ReleaseDescriptor releaseDescriptor,
        BarResource* result) {
    if (getDescriptor == nullptr || releaseDescriptor == nullptr ||
        result == nullptr) return EFI_INVALID_PARAMETER_VALUE;

    ZeroBytes(result, sizeof(*result));
    const uint8_t* bytes = nullptr;
    uint64_t attributes = 0;
    uint64_t status = getDescriptor(context, &attributes, &bytes);
    result->HasGetBarAttributesStatus = true;
    result->GetBarAttributesStatus = status;
    if (IsErrorStatus(status)) {
        if (bytes != nullptr) {
            (void)releaseDescriptor(context, bytes);
        }
        return status;
    }
    if (bytes == nullptr) return EFI_DEVICE_ERROR_VALUE;

    // GetBarAttributes does not return a buffer length. UEFI 2.10 permits one
    // or more QWORD descriptors followed by an End Tag. This probe supports a
    // bounded single-range form: a 43-byte QWORD payload plus a 2-byte tail.
    // Read the fixed header first; only access QWORD fields when the
    // descriptor advertises that complete payload.
    result->HasDescriptorHeader = true;
    result->DescriptorType = bytes[0];
    result->DescriptorLength = (uint16_t)bytes[1] |
                               ((uint16_t)bytes[2] << 8);
    if (result->DescriptorType == 0x8A &&
        result->DescriptorLength == 0x2B) {
        result->HasQwordFields = true;
        result->ResourceType = bytes[3];
        result->GeneralFlags = bytes[4];
        result->SpecificFlags = bytes[5];
        result->RawGranularity =
            guideXOS::pci_resource::ReadLittleEndian64(bytes + 6);
        result->RawBase =
            guideXOS::pci_resource::ReadLittleEndian64(bytes + 14);
        result->RawMaximum =
            guideXOS::pci_resource::ReadLittleEndian64(bytes + 22);
        result->RawTranslationOffset =
            guideXOS::pci_resource::ReadLittleEndian64(bytes + 30);
        result->RawLength =
            guideXOS::pci_resource::ReadLittleEndian64(bytes + 38);
        result->HasListTail = true;
        result->ListTailByte0 = bytes[46];
        result->ListTailByte1 = bytes[47];
    }

    guideXOS::pci_resource::QwordMemoryResource parsed = {};
    bool valid = guideXOS::pci_resource::TryParseSingleQwordMemoryResource(
        bytes, guideXOS::pci_resource::SINGLE_QWORD_RESOURCE_SIZE, &parsed);

    result->SupportedAttributes = attributes;
    uint64_t releaseStatus = releaseDescriptor(context, bytes);
    if (IsErrorStatus(releaseStatus)) return releaseStatus;

    result->DescriptorValid = valid;
    result->IsMemory = valid;
    if (valid) {
        result->AddressWidth = parsed.AddressWidth;
        result->GeneralFlags = parsed.GeneralFlags;
        result->SpecificFlags = parsed.SpecificFlags;
        result->Base = parsed.Base;
        result->Maximum = parsed.Maximum;
        result->MaximumEncoding = parsed.MaximumEncoding;
        result->Length = parsed.Length;
        result->TranslationOffset = parsed.TranslationOffset;
    }
    return status;
}

struct Location {
    uint64_t Segment;
    uint64_t Bus;
    uint64_t Device;
    uint64_t Function;
};

struct ControllerOps {
    void* Context;
    uint64_t (*GetLocation)(void* context, Location* location);
    uint64_t (*ReadConfig32)(void* context, uint32_t offset, uint32_t* value);
    uint64_t (*GetBar0)(void* context, BarResource* resource);
    uint64_t (*ReadMem32)(void* context, uint64_t offset, uint32_t* value);
};

struct PlatformOps {
    void* Context;
    uint64_t HandleCount;
    uint64_t (*OpenController)(void* context, uint32_t index,
                               ControllerOps* controller);
};

enum class FailureStage : uint8_t {
    None,
    OpenProtocol,
    GetLocation,
    ReadIdentity,
    ReadClass,
    InvalidBdf,
    ReadBarConfiguration,
    InvalidBarConfiguration,
    GetBarAttributes,
    MalformedBarDescriptor,
    UnsupportedBarResourceList,
    BarResourceNotMemory,
    InvalidBarResourceWidth,
    ReservedBarGeneralFlags,
    ReservedBarSpecificFlags,
    BarResourceWidthMismatch,
    MissingBarResourceBase,
    BarResourceBaseMismatch,
    BarResourceBaseBelowMmio,
    InvalidBarResourceRange,
    BarResourceMaximumEncodingMismatch,
    BarResourceTranslationNonzero,
    BarResourceRangeNonCanonical,
    BarResourceRangeExceeds32Bit,
    UndersizedBarResource,
    ReadCapabilities,
    CapabilityRegisterOutOfBounds,
    MalformedCapabilityHeader,
    InvalidCapabilities
};

struct Capabilities {
    uint8_t CapabilityLength;
    uint16_t ControllerVersion;
    uint8_t MaximumDeviceSlots;
    uint16_t AdvertisedInterrupters;
    uint8_t RootPortCount;
    uint32_t HcsParams2;
    uint32_t HcsParams3;
    uint32_t HccParams1;
    uint32_t HccParams2;
    bool HasHccParams2;
    uint16_t ScratchpadBufferCount;
    bool Uses64ByteContexts;
    bool Supports64BitAddresses;
    uint32_t DoorbellOffset;
    uint32_t RuntimeRegisterOffset;
};

struct ProbeResult {
    Location PciLocation;
    uint16_t VendorId;
    uint16_t DeviceId;
    uint8_t ClassCode;
    uint8_t Subclass;
    uint8_t ProgrammingInterface;
    bool HasBarInfo;
    bool BarUsable;
    BarResource Bar;
    uint64_t BarConfigurationBase;
    uint8_t BarConfigurationWidth;
    bool BarPrefetchable;
    bool HasCapabilities;
    Capabilities Capability;
    FailureStage Stage;
    uint64_t FirmwareStatus;
};

struct ProbeSummary {
    uint32_t HandlesScanned;
    uint32_t ControllersFound;
    uint32_t ControllersReported;
    uint32_t EnumerationErrors;
    uint64_t FirstEnumerationErrorStatus;
    FailureStage FirstEnumerationErrorStage;
    bool Truncated;
    bool ControllerLimitReached;
};

struct ReporterOps {
    void* Context;
    void (*OnController)(void* context, const ProbeResult* result);
    void (*OnSummary)(void* context, const ProbeSummary* summary);
};

inline bool IsXhciFunction(uint8_t classCode, uint8_t subclass,
                           uint8_t programmingInterface) {
    return classCode == 0x0C && subclass == 0x03 &&
           programmingInterface == 0x30;
}

inline bool RangeFits(uint64_t apertureLength, uint64_t offset,
                      uint64_t accessLength) {
    return accessLength != 0 && offset <= apertureLength &&
           accessLength <= apertureLength - offset;
}

inline void RecordEnumerationError(ProbeSummary* summary, FailureStage stage,
                                   uint64_t status) {
    ++summary->EnumerationErrors;
    if (summary->FirstEnumerationErrorStatus == 0) {
        summary->FirstEnumerationErrorStatus = status;
        summary->FirstEnumerationErrorStage = stage;
    }
}

inline void SetFailure(ProbeResult* result, FailureStage stage,
                       uint64_t status = 0) {
    result->Stage = stage;
    result->FirmwareStatus = status;
}

inline bool ParseBarConfiguration(uint32_t low, uint32_t high,
                                  uint64_t* base, uint8_t* width,
                                  bool* prefetchable) {
    if ((low & 1U) != 0) return false;
    uint32_t type = (low >> 1) & 3U;
    if (type == 0) {
        *width = 32;
    } else if (type == 2) {
        *width = 64;
    } else {
        return false;
    }
    *base = (uint64_t)(low & 0xFFFFFFF0U);
    if (*width == 64) *base |= (uint64_t)high << 32;
    *prefetchable = (low & 8U) != 0;
    return *base != 0;
}

inline FailureStage ValidateBarResource(const ProbeResult& result) {
    const BarResource& bar = result.Bar;
    guideXOS::pci_resource::ResourceMaximumEncoding maximumEncoding;
    uint64_t rangeEnd = 0;
    if (!bar.DescriptorValid) return FailureStage::MalformedBarDescriptor;
    if (!bar.IsMemory) return FailureStage::BarResourceNotMemory;
    if (bar.AddressWidth != 32 && bar.AddressWidth != 64)
        return FailureStage::InvalidBarResourceWidth;
    if ((bar.GeneralFlags & 0xF0U) != 0)
        return FailureStage::ReservedBarGeneralFlags;
    if ((bar.SpecificFlags & 0xF8U) != 0)
        return FailureStage::ReservedBarSpecificFlags;
    if (bar.AddressWidth != result.BarConfigurationWidth)
        return FailureStage::BarResourceWidthMismatch;
    if (bar.Base == 0) return FailureStage::MissingBarResourceBase;
    if (bar.Base != result.BarConfigurationBase)
        return FailureStage::BarResourceBaseMismatch;
    if (bar.Base < 0x100000ULL) return FailureStage::BarResourceBaseBelowMmio;
    if (!guideXOS::pci_resource::TryInterpretResourceMaximum(
            bar.Base, bar.Maximum, bar.Length, &maximumEncoding, &rangeEnd))
        return FailureStage::InvalidBarResourceRange;
    if (maximumEncoding != bar.MaximumEncoding)
        return FailureStage::BarResourceMaximumEncodingMismatch;
    if (bar.TranslationOffset != 0)
        return FailureStage::BarResourceTranslationNonzero;
    if (rangeEnd > 0x00007FFFFFFFFFFFULL)
        return FailureStage::BarResourceRangeNonCanonical;
    if (bar.AddressWidth == 32 && rangeEnd > 0xFFFFFFFFULL)
        return FailureStage::BarResourceRangeExceeds32Bit;
    if (bar.Length < MINIMUM_CAPABILITY_APERTURE)
        return FailureStage::UndersizedBarResource;
    return FailureStage::None;
}

inline bool ReadCapabilityRegister(const ControllerOps& controller,
        const ProbeResult& result, uint64_t offset, uint32_t* value,
        uint64_t* firmwareStatus) {
    if (!RangeFits(result.Bar.Length, offset, sizeof(uint32_t)) ||
        controller.ReadMem32 == nullptr) return false;
    uint64_t status = controller.ReadMem32(controller.Context, offset, value);
    if (IsErrorStatus(status)) {
        *firmwareStatus = status;
        return false;
    }
    return true;
}

inline bool ReadAndDecodeCapabilities(const ControllerOps& controller,
        ProbeResult* result) {
    if (result->Bar.Length < MINIMUM_CAPABILITY_APERTURE) {
        SetFailure(result, FailureStage::UndersizedBarResource);
        return false;
    }
    if (controller.ReadMem32 == nullptr) {
        SetFailure(result, FailureStage::ReadCapabilities,
                   EFI_DEVICE_ERROR_VALUE);
        return false;
    }

    uint32_t registers[8] = {};
    uint64_t status = 0;
    if (!ReadCapabilityRegister(controller, *result, 0, &registers[0], &status)) {
        SetFailure(result, status == 0 ? FailureStage::CapabilityRegisterOutOfBounds :
                                        FailureStage::ReadCapabilities, status);
        return false;
    }

    uint8_t capLength = (uint8_t)(registers[0] & 0xFFU);
    uint16_t version = (uint16_t)(registers[0] >> 16);
    if (capLength < MINIMUM_CAPABILITY_APERTURE || (capLength & 3U) != 0 ||
        version == 0 || !RangeFits(result->Bar.Length, capLength, 4)) {
        SetFailure(result, FailureStage::MalformedCapabilityHeader);
        return false;
    }

    for (uint32_t offset = 4; offset <= 0x18; offset += 4) {
        uint32_t index = offset / 4;
        status = 0;
        if (!ReadCapabilityRegister(controller, *result, offset,
                                    &registers[index], &status)) {
            SetFailure(result, status == 0 ? FailureStage::CapabilityRegisterOutOfBounds :
                                            FailureStage::ReadCapabilities,
                       status);
            return false;
        }
    }

    bool hasHccParams2 = version >= 0x0110;
    if (hasHccParams2) {
        status = 0;
        if (!ReadCapabilityRegister(controller, *result, HCCPARAMS2_OFFSET,
                                    &registers[7], &status)) {
            SetFailure(result, status == 0 ? FailureStage::CapabilityRegisterOutOfBounds :
                                            FailureStage::ReadCapabilities,
                       status);
            return false;
        }
    }

    uint32_t hcs1 = registers[1];
    uint8_t slots = (uint8_t)(hcs1 & 0xFFU);
    uint16_t interrupters = (uint16_t)((hcs1 >> 8) & 0x7FFU);
    uint8_t ports = (uint8_t)(hcs1 >> 24);
    uint32_t rawDoorbell = registers[5];
    uint32_t rawRuntime = registers[6];
    uint32_t doorbell = rawDoorbell & 0xFFFFFFFCU;
    uint32_t runtime = rawRuntime & 0xFFFFFFE0U;
    if (slots == 0 || interrupters == 0 || ports == 0 ||
        (rawDoorbell & 3U) != 0 || (rawRuntime & 0x1FU) != 0 ||
        doorbell < capLength || runtime < capLength) {
        SetFailure(result, FailureStage::InvalidCapabilities);
        return false;
    }

    uint64_t doorbellBytes = ((uint64_t)slots + 1ULL) * 4ULL;
    uint64_t runtimeBytes = 0x20ULL + (uint64_t)interrupters * 0x20ULL;
    if (!RangeFits(result->Bar.Length, doorbell, doorbellBytes) ||
        !RangeFits(result->Bar.Length, runtime, runtimeBytes)) {
        SetFailure(result, FailureStage::InvalidCapabilities);
        return false;
    }

    result->Capability.CapabilityLength = capLength;
    result->Capability.ControllerVersion = version;
    result->Capability.MaximumDeviceSlots = slots;
    result->Capability.AdvertisedInterrupters = interrupters;
    result->Capability.RootPortCount = ports;
    result->Capability.HcsParams2 = registers[2];
    result->Capability.HcsParams3 = registers[3];
    result->Capability.HccParams1 = registers[4];
    result->Capability.HccParams2 = registers[7];
    result->Capability.HasHccParams2 = hasHccParams2;
    result->Capability.ScratchpadBufferCount = (uint16_t)(
        ((((registers[2] >> 27) & 0x1FU) << 5) |
         ((registers[2] >> 21) & 0x1FU)));
    result->Capability.Uses64ByteContexts = (registers[4] & (1U << 2)) != 0;
    result->Capability.Supports64BitAddresses = (registers[4] & 1U) != 0;
    result->Capability.DoorbellOffset = doorbell;
    result->Capability.RuntimeRegisterOffset = runtime;
    result->HasCapabilities = true;
    SetFailure(result, FailureStage::None);
    return true;
}

inline bool ProbeXhciController(const ControllerOps& controller,
                                ProbeResult* result) {
    uint32_t barLow = 0;
    uint32_t barHigh = 0;
    uint64_t status = controller.ReadConfig32(controller.Context, 0x10, &barLow);
    if (IsErrorStatus(status)) {
        SetFailure(result, FailureStage::ReadBarConfiguration, status);
        return false;
    }
    uint8_t width = 0;
    uint64_t base = 0;
    bool prefetchable = false;
    uint32_t memoryType = (barLow >> 1) & 3U;
    if (memoryType == 2) {
        status = controller.ReadConfig32(controller.Context, 0x14, &barHigh);
        if (IsErrorStatus(status)) {
            SetFailure(result, FailureStage::ReadBarConfiguration, status);
            return false;
        }
    }
    if (!ParseBarConfiguration(barLow, barHigh, &base, &width, &prefetchable)) {
        SetFailure(result, FailureStage::InvalidBarConfiguration);
        return false;
    }
    result->BarConfigurationBase = base;
    result->BarConfigurationWidth = width;
    result->BarPrefetchable = prefetchable;

    if (controller.GetBar0 == nullptr) {
        SetFailure(result, FailureStage::GetBarAttributes,
                   EFI_DEVICE_ERROR_VALUE);
        return false;
    }
    status = controller.GetBar0(controller.Context, &result->Bar);
    if (IsErrorStatus(status)) {
        SetFailure(result, FailureStage::GetBarAttributes, status);
        return false;
    }
    result->HasBarInfo = true;
    if (!result->Bar.DescriptorValid) {
        SetFailure(result,
            result->Bar.HasQwordFields && result->Bar.HasListTail &&
            result->Bar.ListTailByte0 == 0x8A ?
                FailureStage::UnsupportedBarResourceList :
                FailureStage::MalformedBarDescriptor);
        return false;
    }
    FailureStage resourceFailure = ValidateBarResource(*result);
    if (resourceFailure != FailureStage::None) {
        SetFailure(result, resourceFailure);
        return false;
    }
    result->BarUsable = true;
    return ReadAndDecodeCapabilities(controller, result);
}

inline void ProcessHandle(const PlatformOps& platform, uint32_t index,
                          ProbeSummary* summary,
                          const ReporterOps& reporter) {
    ControllerOps controller = {};
    uint64_t status = platform.OpenController(platform.Context, index, &controller);
    if (IsErrorStatus(status)) {
        RecordEnumerationError(summary, FailureStage::OpenProtocol, status);
        return;
    }
    if (controller.GetLocation == nullptr || controller.ReadConfig32 == nullptr) {
        RecordEnumerationError(summary, FailureStage::OpenProtocol,
                               EFI_DEVICE_ERROR_VALUE);
        return;
    }

    Location location = {};
    status = controller.GetLocation(controller.Context, &location);
    if (IsErrorStatus(status)) {
        RecordEnumerationError(summary, FailureStage::GetLocation, status);
        return;
    }
    uint32_t identity = 0;
    status = controller.ReadConfig32(controller.Context, 0, &identity);
    if (IsErrorStatus(status)) {
        RecordEnumerationError(summary, FailureStage::ReadIdentity, status);
        return;
    }
    uint32_t classRegister = 0;
    status = controller.ReadConfig32(controller.Context, 8, &classRegister);
    if (IsErrorStatus(status)) {
        RecordEnumerationError(summary, FailureStage::ReadClass, status);
        return;
    }

    uint8_t programmingInterface = (uint8_t)(classRegister >> 8);
    uint8_t subclass = (uint8_t)(classRegister >> 16);
    uint8_t classCode = (uint8_t)(classRegister >> 24);
    if (!IsXhciFunction(classCode, subclass, programmingInterface)) return;

    if (summary->ControllersFound >= MAX_CONTROLLERS_TO_PROBE) {
        summary->Truncated = true;
        summary->ControllerLimitReached = true;
        return;
    }

    ++summary->ControllersFound;
    ProbeResult result;
    ZeroBytes(&result, sizeof(result));
    result.PciLocation = location;
    result.VendorId = (uint16_t)(identity & 0xFFFFU);
    result.DeviceId = (uint16_t)(identity >> 16);
    result.ClassCode = classCode;
    result.Subclass = subclass;
    result.ProgrammingInterface = programmingInterface;

    if (location.Segment > 0xFFFFULL || location.Bus > 0xFFULL ||
        location.Device > 31ULL || location.Function > 7ULL) {
        SetFailure(&result, FailureStage::InvalidBdf);
    } else {
        (void)ProbeXhciController(controller, &result);
    }
    if (reporter.OnController != nullptr)
        reporter.OnController(reporter.Context, &result);
    ++summary->ControllersReported;
}

inline ProbeSummary RunProbe(const PlatformOps& platform,
                             const ReporterOps& reporter) {
    ProbeSummary summary = {};
    if (platform.OpenController == nullptr) {
        summary.Truncated = true;
        if (reporter.OnSummary != nullptr)
            reporter.OnSummary(reporter.Context, &summary);
        return summary;
    }

    uint64_t limit = platform.HandleCount;
    if (limit > MAX_HANDLES_TO_SCAN) {
        limit = MAX_HANDLES_TO_SCAN;
        summary.Truncated = true;
    }
    for (uint32_t index = 0; index < (uint32_t)limit; ++index) {
        ++summary.HandlesScanned;
        ProcessHandle(platform, index, &summary, reporter);
        if (summary.ControllerLimitReached) break;
    }
    if (reporter.OnSummary != nullptr)
        reporter.OnSummary(reporter.Context, &summary);
    return summary;
}

} // namespace xhci_probe
} // namespace guideXOS
