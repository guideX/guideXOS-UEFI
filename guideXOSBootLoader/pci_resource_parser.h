#pragma once

#include <stddef.h>
#include <stdint.h>

namespace guideXOS {
namespace pci_resource {

static const size_t QWORD_ADDRESS_DESCRIPTOR_SIZE = 46;
static const size_t END_TAG_SIZE = 2;
static const size_t SINGLE_QWORD_RESOURCE_SIZE =
    QWORD_ADDRESS_DESCRIPTOR_SIZE + END_TAG_SIZE;

struct QwordMemoryResource {
    uint8_t GeneralFlags;
    uint8_t SpecificFlags;
    uint8_t AddressWidth;
    uint64_t Base;
    uint64_t Maximum;
    uint64_t Length;
    uint64_t TranslationOffset;
};

inline uint64_t ReadLittleEndian64(const uint8_t* bytes) {
    uint64_t value = 0;
    for (size_t i = 0; i < 8; ++i)
        value |= (uint64_t)bytes[i] << (i * 8);
    return value;
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
        (parsed.SpecificFlags & 0xF8) != 0 ||
        (parsed.SpecificFlags & 0x06) != 0 ||
        parsed.Base < 0x100000ULL || parsed.Length == 0 ||
        parsed.Base > (~(uint64_t)0) - (parsed.Length - 1) ||
        parsed.Maximum != parsed.Base + parsed.Length - 1 ||
        parsed.TranslationOffset != 0 ||
        parsed.Maximum > 0x00007FFFFFFFFFFFULL ||
        (granularity == 32 && parsed.Maximum > 0xFFFFFFFFULL)) return false;

    parsed.AddressWidth = (uint8_t)granularity;
    *result = parsed;
    return true;
}

} // namespace pci_resource
} // namespace guideXOS
