#pragma once

// P23R5 staging foundation only. This validates mapped AMD64 PE ranges and
// ordinary version-1 unwind record bounds. It deliberately cannot construct
// or publish a NativeAOT code manager: GC/EH metadata, the runtime's failure
// channels, and registration visibility remain unqualified.

#include <cstddef>
#include <cstdint>
#include <limits>

namespace guidexos::registration {

enum class PreflightStatus : std::uint8_t {
    ValidatedForStaging,
    InvalidArgument,
    InvalidDosHeader,
    InvalidNtHeaders,
    UnsupportedMachine,
    UnsupportedOptionalHeader,
    InvalidImageBounds,
    InvalidSectionTable,
    OverlappingSections,
    InvalidCodeRange,
    OverlappingCodeRanges,
    InvalidExceptionDirectory,
    InvalidRuntimeFunction,
    InvalidUnwindInfo,
    UnsupportedUnwindInfo,
    UnsupportedUnwindOpcode,
};

struct MappedPeImage {
    const std::uint8_t* Data;
    std::size_t AccessibleSize;
    std::uintptr_t BaseAddress;
};

struct CodeRange {
    std::uintptr_t Start;
    std::uint32_t Size;
};

struct RegistrationRequest {
    MappedPeImage Image;
    CodeRange ManagedCode;
    CodeRange UnboxingStubs;
    const void* ClasslibFunctions;
    std::uint32_t ClasslibFunctionCount;
};

struct StagedRegistration {
    std::uintptr_t ImageBase;
    std::uint32_t ImageSize;
    std::uint32_t ExceptionDirectoryRva;
    std::uint32_t RuntimeFunctionCount;
    CodeRange ManagedCode;
    CodeRange UnboxingStubs;
    bool GcEhMetadataValidated;
    bool ManagerConstructionAllowed;
    bool PublicationAllowed;
};

namespace detail {

constexpr std::uint16_t kDosSignature = 0x5A4D;
constexpr std::uint32_t kNtSignature = 0x00004550;
constexpr std::uint16_t kAmd64Machine = 0x8664;
constexpr std::uint16_t kPe32PlusMagic = 0x020B;
constexpr std::uint32_t kSectionExecute = 0x20000000;
constexpr std::size_t kMaxSections = 96;
constexpr std::size_t kRuntimeFunctionSize = 12;
constexpr std::uint32_t kMaxRuntimeFunctions = 1u << 20;

struct Section {
    std::uint32_t VirtualAddress;
    std::uint32_t Size;
    std::uint32_t Characteristics;
};

inline bool Contains(std::size_t bound, std::uint64_t offset,
                     std::uint64_t size) noexcept {
    return offset <= bound && size <= static_cast<std::uint64_t>(bound) - offset;
}

inline bool Read16(const std::uint8_t* data, std::size_t bound,
                   std::uint64_t offset, std::uint16_t* value) noexcept {
    if (value == nullptr || !Contains(bound, offset, 2))
        return false;
    *value = static_cast<std::uint16_t>(data[offset]) |
             static_cast<std::uint16_t>(data[offset + 1] << 8);
    return true;
}

inline bool Read32(const std::uint8_t* data, std::size_t bound,
                   std::uint64_t offset, std::uint32_t* value) noexcept {
    if (value == nullptr || !Contains(bound, offset, 4))
        return false;
    *value = static_cast<std::uint32_t>(data[offset]) |
             (static_cast<std::uint32_t>(data[offset + 1]) << 8) |
             (static_cast<std::uint32_t>(data[offset + 2]) << 16) |
             (static_cast<std::uint32_t>(data[offset + 3]) << 24);
    return true;
}

inline bool IsRangeInSection(const Section* sections, std::size_t sectionCount,
                             std::uint32_t begin, std::uint32_t size,
                             bool requireExecutable) noexcept {
    if (size == 0 || begin > (std::numeric_limits<std::uint32_t>::max)() - size)
        return false;
    const std::uint32_t end = begin + size;
    for (std::size_t index = 0; index < sectionCount; ++index) {
        const Section& section = sections[index];
        if (begin >= section.VirtualAddress &&
            end <= section.VirtualAddress + section.Size &&
            (!requireExecutable || (section.Characteristics & kSectionExecute) != 0)) {
            return true;
        }
    }
    return false;
}

inline bool IsNonvolatileIntegerRegister(std::uint8_t reg) noexcept {
    return reg == 3 || reg == 5 || reg == 6 || reg == 7 ||
           (reg >= 12 && reg <= 15);
}

inline PreflightStatus ValidateUnwindInfo(const std::uint8_t* image,
                                          std::uint32_t imageSize,
                                          std::uint32_t unwindRva) noexcept {
    if ((unwindRva & 3u) != 0 || !Contains(imageSize, unwindRva, 4))
        return PreflightStatus::InvalidUnwindInfo;

    const std::uint8_t versionAndFlags = image[unwindRva];
    const std::uint8_t version = versionAndFlags & 0x07u;
    const std::uint8_t flags = versionAndFlags >> 3;
    if (version != 1 || flags != 0)
        return PreflightStatus::UnsupportedUnwindInfo;

    const std::uint8_t prologSize = image[unwindRva + 1];
    const std::uint8_t codeSlots = image[unwindRva + 2];
    const std::uint64_t paddedSlots = (static_cast<std::uint64_t>(codeSlots) + 1u) & ~1ull;
    const std::uint64_t recordSize = 4u + paddedSlots * 2u;
    if (!Contains(imageSize, unwindRva, recordSize))
        return PreflightStatus::InvalidUnwindInfo;

    std::uint8_t previousCodeOffset = 0xFF;
    std::uint16_t slot = 0;
    while (slot < codeSlots) {
        const std::uint64_t codeOffset = unwindRva + 4u + static_cast<std::uint64_t>(slot) * 2u;
        const std::uint8_t prologOffset = image[codeOffset];
        const std::uint8_t opAndInfo = image[codeOffset + 1];
        const std::uint8_t opcode = opAndInfo & 0x0Fu;
        const std::uint8_t info = opAndInfo >> 4;
        if (prologOffset > prologSize || prologOffset > previousCodeOffset)
            return PreflightStatus::InvalidUnwindInfo;
        previousCodeOffset = prologOffset;

        std::uint8_t extraSlots = 0;
        switch (opcode) {
        case 0: // UWOP_PUSH_NONVOL
            if (!IsNonvolatileIntegerRegister(info))
                return PreflightStatus::InvalidUnwindInfo;
            break;
        case 1: // UWOP_ALLOC_LARGE
            if (info > 1)
                return PreflightStatus::InvalidUnwindInfo;
            extraSlots = info == 0 ? 1 : 2;
            break;
        case 2: // UWOP_ALLOC_SMALL
            break;
        case 3: // UWOP_SET_FPREG
            if (info != 0)
                return PreflightStatus::InvalidUnwindInfo;
            break;
        case 4: // UWOP_SAVE_NONVOL
        case 5: // UWOP_SAVE_NONVOL_FAR
            if (!IsNonvolatileIntegerRegister(info))
                return PreflightStatus::InvalidUnwindInfo;
            extraSlots = opcode == 4 ? 1 : 2;
            break;
        case 8: // UWOP_SAVE_XMM128
        case 9: // UWOP_SAVE_XMM128_FAR
            if (info < 6 || info > 15)
                return PreflightStatus::InvalidUnwindInfo;
            extraSlots = opcode == 8 ? 1 : 2;
            break;
        case 10: // UWOP_PUSH_MACHFRAME
            return PreflightStatus::UnsupportedUnwindOpcode;
        default:
            return PreflightStatus::UnsupportedUnwindOpcode;
        }

        if (extraSlots > codeSlots - slot - 1)
            return PreflightStatus::InvalidUnwindInfo;
        slot = static_cast<std::uint16_t>(slot + 1u + extraSlots);
    }

    return PreflightStatus::ValidatedForStaging;
}

} // namespace detail

// Produces only a validation plan. It never allocates a manager, registers an
// unboxing range, or publishes runtime state. A successful result explicitly
// leaves GC/EH validation, manager construction, and publication disabled.
inline PreflightStatus PrepareRegistration(const RegistrationRequest& request,
                                          StagedRegistration* staged) noexcept {
    using namespace detail;
    if (staged == nullptr || request.Image.Data == nullptr ||
        request.Image.AccessibleSize == 0 ||
        request.Image.BaseAddress != reinterpret_cast<std::uintptr_t>(request.Image.Data) ||
        request.ManagedCode.Size == 0 || request.UnboxingStubs.Size == 0 ||
        (request.ClasslibFunctionCount != 0 && request.ClasslibFunctions == nullptr) ||
        request.Image.AccessibleSize > (std::numeric_limits<std::uint32_t>::max)()) {
        return PreflightStatus::InvalidArgument;
    }

    const std::uintptr_t imageBase = request.Image.BaseAddress;
    const std::uint64_t availableSize = request.Image.AccessibleSize;
    if (imageBase > (std::numeric_limits<std::uintptr_t>::max)() - availableSize)
        return PreflightStatus::InvalidImageBounds;

    std::uint16_t dosSignature = 0;
    std::uint32_t ntOffset = 0;
    if (!Read16(request.Image.Data, request.Image.AccessibleSize, 0, &dosSignature) ||
        dosSignature != kDosSignature ||
        !Read32(request.Image.Data, request.Image.AccessibleSize, 0x3C, &ntOffset)) {
        return PreflightStatus::InvalidDosHeader;
    }

    std::uint32_t ntSignature = 0;
    std::uint16_t machine = 0;
    std::uint16_t sectionCount = 0;
    std::uint16_t optionalSize = 0;
    if (!Read32(request.Image.Data, request.Image.AccessibleSize, ntOffset, &ntSignature) ||
        ntSignature != kNtSignature ||
        !Read16(request.Image.Data, request.Image.AccessibleSize, ntOffset + 4u, &machine) ||
        !Read16(request.Image.Data, request.Image.AccessibleSize, ntOffset + 6u, &sectionCount) ||
        !Read16(request.Image.Data, request.Image.AccessibleSize, ntOffset + 20u, &optionalSize)) {
        return PreflightStatus::InvalidNtHeaders;
    }
    if (machine != kAmd64Machine)
        return PreflightStatus::UnsupportedMachine;
    if (sectionCount == 0 || sectionCount > kMaxSections || optionalSize < 144)
        return PreflightStatus::UnsupportedOptionalHeader;

    const std::uint64_t optionalOffset = static_cast<std::uint64_t>(ntOffset) + 24u;
    const std::uint64_t sectionTableOffset = optionalOffset + optionalSize;
    const std::uint64_t sectionTableSize = static_cast<std::uint64_t>(sectionCount) * 40u;
    std::uint16_t optionalMagic = 0;
    std::uint32_t imageSize = 0;
    std::uint32_t headersSize = 0;
    std::uint32_t directoryCount = 0;
    std::uint32_t exceptionRva = 0;
    std::uint32_t exceptionSize = 0;
    if (!Read16(request.Image.Data, request.Image.AccessibleSize, optionalOffset, &optionalMagic) ||
        optionalMagic != kPe32PlusMagic ||
        !Read32(request.Image.Data, request.Image.AccessibleSize, optionalOffset + 56u, &imageSize) ||
        !Read32(request.Image.Data, request.Image.AccessibleSize, optionalOffset + 60u, &headersSize) ||
        !Read32(request.Image.Data, request.Image.AccessibleSize, optionalOffset + 108u, &directoryCount)) {
        return PreflightStatus::UnsupportedOptionalHeader;
    }
    if (directoryCount < 4 ||
        !Read32(request.Image.Data, request.Image.AccessibleSize, optionalOffset + 112u + 3u * 8u, &exceptionRva) ||
        !Read32(request.Image.Data, request.Image.AccessibleSize, optionalOffset + 116u + 3u * 8u, &exceptionSize)) {
        return PreflightStatus::UnsupportedOptionalHeader;
    }
    if (imageSize == 0 || imageSize > request.Image.AccessibleSize ||
        headersSize == 0 || headersSize > imageSize ||
        !Contains(request.Image.AccessibleSize, sectionTableOffset, sectionTableSize) ||
        sectionTableOffset + sectionTableSize > headersSize) {
        return PreflightStatus::InvalidImageBounds;
    }

    Section sections[kMaxSections]{};
    for (std::size_t index = 0; index < sectionCount; ++index) {
        const std::uint64_t offset = sectionTableOffset + index * 40u;
        std::uint32_t virtualSize = 0;
        std::uint32_t virtualAddress = 0;
        std::uint32_t rawSize = 0;
        std::uint32_t characteristics = 0;
        if (!Read32(request.Image.Data, request.Image.AccessibleSize, offset + 8u, &virtualSize) ||
            !Read32(request.Image.Data, request.Image.AccessibleSize, offset + 12u, &virtualAddress) ||
            !Read32(request.Image.Data, request.Image.AccessibleSize, offset + 16u, &rawSize) ||
            !Read32(request.Image.Data, request.Image.AccessibleSize, offset + 36u, &characteristics)) {
            return PreflightStatus::InvalidSectionTable;
        }
        const std::uint32_t sectionSize = virtualSize > rawSize ? virtualSize : rawSize;
        if (sectionSize == 0 || virtualAddress < headersSize ||
            virtualAddress > imageSize || sectionSize > imageSize - virtualAddress) {
            return PreflightStatus::InvalidSectionTable;
        }
        sections[index] = {virtualAddress, sectionSize, characteristics};
        for (std::size_t prior = 0; prior < index; ++prior) {
            const Section& other = sections[prior];
            if (virtualAddress < other.VirtualAddress + other.Size &&
                other.VirtualAddress < virtualAddress + sectionSize) {
                return PreflightStatus::OverlappingSections;
            }
        }
    }

    auto validateCodeRange = [&](const CodeRange& range) noexcept {
        if (range.Start < imageBase)
            return false;
        const std::uint64_t rva = range.Start - imageBase;
        if (rva >= imageSize || range.Size > imageSize - rva)
            return false;
        return IsRangeInSection(sections, sectionCount, static_cast<std::uint32_t>(rva),
                                range.Size, true);
    };
    if (!validateCodeRange(request.ManagedCode) || !validateCodeRange(request.UnboxingStubs))
        return PreflightStatus::InvalidCodeRange;

    const std::uint64_t managedRva = request.ManagedCode.Start - imageBase;
    const std::uint64_t unboxingRva = request.UnboxingStubs.Start - imageBase;
    if (managedRva < unboxingRva + request.UnboxingStubs.Size &&
        unboxingRva < managedRva + request.ManagedCode.Size) {
        return PreflightStatus::OverlappingCodeRanges;
    }

    if ((exceptionRva & 3u) != 0 || exceptionSize == 0 ||
        exceptionSize % kRuntimeFunctionSize != 0 ||
        !Contains(imageSize, exceptionRva, exceptionSize) ||
        !IsRangeInSection(sections, sectionCount, exceptionRva, exceptionSize, false)) {
        return PreflightStatus::InvalidExceptionDirectory;
    }

    const std::uint32_t runtimeFunctionCount = exceptionSize / kRuntimeFunctionSize;
    if (runtimeFunctionCount > kMaxRuntimeFunctions)
        return PreflightStatus::InvalidExceptionDirectory;
    std::uint32_t previousEnd = 0;
    for (std::uint32_t index = 0; index < runtimeFunctionCount; ++index) {
        const std::uint64_t entryOffset = static_cast<std::uint64_t>(exceptionRva) +
                                          static_cast<std::uint64_t>(index) * kRuntimeFunctionSize;
        std::uint32_t begin = 0;
        std::uint32_t end = 0;
        std::uint32_t unwindRva = 0;
        if (!Read32(request.Image.Data, imageSize, entryOffset, &begin) ||
            !Read32(request.Image.Data, imageSize, entryOffset + 4u, &end) ||
            !Read32(request.Image.Data, imageSize, entryOffset + 8u, &unwindRva) ||
            end <= begin || begin < previousEnd ||
            !IsRangeInSection(sections, sectionCount, begin, end - begin, true)) {
            return PreflightStatus::InvalidRuntimeFunction;
        }
        previousEnd = end;
        const PreflightStatus unwindStatus = ValidateUnwindInfo(request.Image.Data, imageSize, unwindRva);
        if (unwindStatus != PreflightStatus::ValidatedForStaging)
            return unwindStatus;
    }

    StagedRegistration candidate{};
    candidate.ImageBase = imageBase;
    candidate.ImageSize = imageSize;
    candidate.ExceptionDirectoryRva = exceptionRva;
    candidate.RuntimeFunctionCount = runtimeFunctionCount;
    candidate.ManagedCode = request.ManagedCode;
    candidate.UnboxingStubs = request.UnboxingStubs;
    candidate.GcEhMetadataValidated = false;
    candidate.ManagerConstructionAllowed = false;
    candidate.PublicationAllowed = false;
    *staged = candidate;
    return PreflightStatus::ValidatedForStaging;
}

} // namespace guidexos::registration
