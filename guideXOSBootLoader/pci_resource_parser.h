#pragma once

#include <stddef.h>
#include <stdint.h>

namespace guideXOS {
namespace pci_resource {

enum class ResourceMaximumEncoding : uint16_t {
    InclusiveEnd = 0,
    EDK2AlignmentMask = 1
};

static const size_t QWORD_ADDRESS_DESCRIPTOR_SIZE = 46;
static const size_t END_TAG_SIZE = 2;
static const size_t SINGLE_QWORD_RESOURCE_SIZE =
    QWORD_ADDRESS_DESCRIPTOR_SIZE + END_TAG_SIZE;

struct QwordMemoryResource {
    uint8_t GeneralFlags;
    uint8_t SpecificFlags;
    uint8_t AddressWidth;
    ResourceMaximumEncoding MaximumEncoding;
    uint64_t Base;
    uint64_t Maximum;
    uint64_t RangeEnd;
    uint64_t Length;
    uint64_t TranslationOffset;
};

inline bool IsPowerOfTwo(uint64_t value) {
    return value != 0 && (value & (value - 1)) == 0;
}

// UEFI/ACPI describes AddrRangeMax as an inclusive end, while EDK II's
// PciIoGetBarAttributes reports PCI_BAR.Alignment here. Its value is a
// low-order alignment mask (for example, 0xFFFF for a 64 KiB BAR). Accept
// only one unambiguous interpretation, and retain the raw field for handoff.
inline bool TryInterpretResourceMaximum(uint64_t base, uint64_t maximum,
        uint64_t length, ResourceMaximumEncoding* encoding,
        uint64_t* rangeEnd) {
    if (encoding == nullptr || rangeEnd == nullptr || length == 0 ||
        base > (~(uint64_t)0) - (length - 1)) return false;

    const uint64_t computedEnd = base + length - 1;
    const bool inclusiveEnd = maximum == computedEnd;
    bool alignmentMask = false;
    if (maximum != ~(uint64_t)0) {
        const uint64_t maskSize = maximum + 1;
        alignmentMask = IsPowerOfTwo(maskSize) && length == maskSize &&
                        base % maskSize == 0;
    }

    if (inclusiveEnd == alignmentMask) return false;
    *encoding = inclusiveEnd ? ResourceMaximumEncoding::InclusiveEnd :
                               ResourceMaximumEncoding::EDK2AlignmentMask;
    *rangeEnd = computedEnd;
    return true;
}

inline uint64_t ReadLittleEndian64(const uint8_t* bytes) {
    uint64_t value = 0;
    for (size_t i = 0; i < 8; ++i)
        value |= (uint64_t)bytes[i] << (i * 8);
    return value;
}

// Match a single exact ASCII command-line token in firmware's UTF-16
// LoadOptions buffer. The caller owns byte-size validation and bounds the
// supplied unit count; a NUL terminates the option list.
inline bool ContainsExactAsciiOption(const uint16_t* loadOptions,
        size_t optionCount, const char* option, size_t optionLength) {
    if (loadOptions == nullptr || option == nullptr || optionLength == 0)
        return false;

    size_t index = 0;
    while (index < optionCount) {
        while (index < optionCount &&
               (loadOptions[index] == ' ' || loadOptions[index] == '\t' ||
                loadOptions[index] == '\r' || loadOptions[index] == '\n')) {
            ++index;
        }
        if (index >= optionCount || loadOptions[index] == 0) break;

        const size_t tokenStart = index;
        while (index < optionCount && loadOptions[index] != 0 &&
               loadOptions[index] != ' ' && loadOptions[index] != '\t' &&
               loadOptions[index] != '\r' && loadOptions[index] != '\n') {
            ++index;
        }
        if (index - tokenStart == optionLength) {
            size_t character = 0;
            while (character < optionLength &&
                   loadOptions[tokenStart + character] ==
                       (uint8_t)option[character]) {
                ++character;
            }
            if (character == optionLength) return true;
        }
        if (index < optionCount && loadOptions[index] == 0) break;
    }
    return false;
}

// Parse the single QWORD memory descriptor supported by the MI3 contract.
// UEFI permits a list of descriptors for a BAR; this bounded form rejects
// multiple descriptors rather than walking an unbounded firmware list.
inline bool TryParseSingleQwordMemoryResource(const uint8_t* bytes,
        size_t byteCount, QwordMemoryResource* result) {
    if (bytes == nullptr || result == nullptr ||
        byteCount != SINGLE_QWORD_RESOURCE_SIZE ||
        bytes[0] != 0x8A || bytes[1] != 0x2B || bytes[2] != 0 ||
        bytes[3] != 0 || bytes[46] != 0x79) return false;

    if (bytes[47] != 0) {
        uint8_t checksum = 0;
        for (size_t i = 0; i < SINGLE_QWORD_RESOURCE_SIZE; ++i)
            checksum = (uint8_t)(checksum + bytes[i]);
        if (checksum != 0) return false;
    }

    QwordMemoryResource parsed = {};
    parsed.GeneralFlags = bytes[4];
    parsed.SpecificFlags = bytes[5];
    uint64_t granularity = ReadLittleEndian64(bytes + 6);
    parsed.Base = ReadLittleEndian64(bytes + 14);
    parsed.Maximum = ReadLittleEndian64(bytes + 22);
    parsed.TranslationOffset = ReadLittleEndian64(bytes + 30);
    parsed.Length = ReadLittleEndian64(bytes + 38);

    if ((granularity != 32 && granularity != 64) ||
        (parsed.GeneralFlags & 0xF0) != 0 ||
        // For memory resources, bits 2:1 encode cacheability (including
        // write-combining and prefetchable). They are valid ACPI flags and do
        // not alter the address bounds validated below. Bits 7:3 remain
        // unsupported for this BAR resource profile.
        (parsed.SpecificFlags & 0xF8) != 0 ||
        parsed.Base < 0x100000ULL ||
        parsed.TranslationOffset != 0 ||
        !TryInterpretResourceMaximum(parsed.Base, parsed.Maximum,
            parsed.Length, &parsed.MaximumEncoding, &parsed.RangeEnd) ||
        parsed.RangeEnd > 0x00007FFFFFFFFFFFULL ||
        (granularity == 32 && parsed.RangeEnd > 0xFFFFFFFFULL)) return false;

    parsed.AddressWidth = (uint8_t)granularity;
    *result = parsed;
    return true;
}

} // namespace pci_resource
} // namespace guideXOS
