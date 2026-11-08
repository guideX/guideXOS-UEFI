#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "../guideXOSBootLoader/pci_resource_parser.h"

static unsigned int assertions = 0;

static void Check(bool condition, const char* message) {
    ++assertions;
    if (!condition) {
        fprintf(stderr, "FAIL: %s\n", message);
        exit(1);
    }
}

static size_t MakeUtf16Options(const char* text, uint16_t* output,
        size_t capacity) {
    size_t length = 0;
    while (text[length] != '\0' && length + 1 < capacity) {
        output[length] = (uint8_t)text[length];
        ++length;
    }
    output[length] = 0;
    return length + 1;
}

static void WriteLittleEndian64(uint8_t* bytes, uint64_t value) {
    for (unsigned int i = 0; i < 8; ++i)
        bytes[i] = (uint8_t)(value >> (i * 8));
}

static void UpdateChecksum(uint8_t* bytes) {
    bytes[47] = 0;
    uint8_t sum = 0;
    for (unsigned int i = 0; i < 48; ++i) sum = (uint8_t)(sum + bytes[i]);
    bytes[47] = (uint8_t)(0U - sum);
}

static void MakeResource(uint8_t* bytes, uint8_t width,
        uint64_t base, uint64_t length) {
    memset(bytes, 0, 48);
    bytes[0] = 0x8A;
    bytes[1] = 0x2B;
    bytes[3] = 0; // ACPI address-space resource: memory
    bytes[4] = 0x0F;
    bytes[5] = 0x01;
    WriteLittleEndian64(bytes + 6, width);
    WriteLittleEndian64(bytes + 14, base);
    WriteLittleEndian64(bytes + 22, base + length - 1);
    WriteLittleEndian64(bytes + 30, 0);
    WriteLittleEndian64(bytes + 38, length);
    bytes[46] = 0x79;
}

int main() {
    using guideXOS::pci_resource::QwordMemoryResource;
    using guideXOS::pci_resource::ContainsExactAsciiOption;
    using guideXOS::pci_resource::TryParseSingleQwordMemoryResource;

    static const char probeOption[] = "--xhci-capability-probe";
    uint16_t loadOptions[128];
    size_t optionCount = MakeUtf16Options(
        "--other\t--xhci-capability-probe\r--last", loadOptions,
        sizeof(loadOptions) / sizeof(loadOptions[0]));
    Check(ContainsExactAsciiOption(loadOptions, optionCount, probeOption,
          sizeof(probeOption) - 1),
          "exact opt-in token found among bounded UTF-16 options");
    optionCount = MakeUtf16Options(
        "--xhci-capability-probe-extra", loadOptions,
        sizeof(loadOptions) / sizeof(loadOptions[0]));
    Check(!ContainsExactAsciiOption(loadOptions, optionCount, probeOption,
          sizeof(probeOption) - 1),
          "extended token does not enable capability probe");
    optionCount = MakeUtf16Options(
        "prefix--xhci-capability-probe", loadOptions,
        sizeof(loadOptions) / sizeof(loadOptions[0]));
    Check(!ContainsExactAsciiOption(loadOptions, optionCount, probeOption,
          sizeof(probeOption) - 1),
          "embedded option substring does not enable capability probe");
    optionCount = MakeUtf16Options(
        "\"--xhci-capability-probe\"", loadOptions,
        sizeof(loadOptions) / sizeof(loadOptions[0]));
    Check(!ContainsExactAsciiOption(loadOptions, optionCount, probeOption,
          sizeof(probeOption) - 1),
          "quoted option does not match the exact diagnostic token");
    optionCount = MakeUtf16Options("--xhci-capability-probe", loadOptions,
        sizeof(loadOptions) / sizeof(loadOptions[0]));
    Check(!ContainsExactAsciiOption(loadOptions, 8, probeOption,
          sizeof(probeOption) - 1),
          "truncated UTF-16 option buffer is rejected");
    Check(!ContainsExactAsciiOption(nullptr, optionCount, probeOption,
          sizeof(probeOption) - 1),
          "missing UTF-16 option buffer is rejected");
    Check(!ContainsExactAsciiOption(loadOptions, optionCount, probeOption, 0),
          "empty option name is rejected");

    uint8_t bytes[48];
    QwordMemoryResource parsed = {};
    MakeResource(bytes, 32, 0xE0000000ULL, 0x10000ULL);
    Check(TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "32-bit QWORD memory descriptor accepted");
    Check(parsed.AddressWidth == 32 && parsed.Base == 0xE0000000ULL &&
          parsed.Maximum == 0xE000FFFFULL && parsed.Length == 0x10000ULL,
          "32-bit descriptor fields decoded");

    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    Check(TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "64-bit QWORD memory descriptor accepted");
    Check(parsed.AddressWidth == 64 && parsed.Base == 0x00000001FEC01000ULL &&
          parsed.Maximum == 0x00000001FEC10FFFULL &&
          parsed.MaximumEncoding ==
              guideXOS::pci_resource::ResourceMaximumEncoding::InclusiveEnd &&
          parsed.RangeEnd == 0x00000001FEC10FFFULL,
          "64-bit descriptor end and width decoded");

    // Exact descriptor reported by SARAH_DT at 0000:00:14.0.
    MakeResource(bytes, 32, 0xE1020000ULL, 0x10000ULL);
    bytes[4] = 0;
    bytes[5] = 0;
    WriteLittleEndian64(bytes + 22, 0xFFFFULL);
    Check(TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "exact SARAH_DT EDK II alignment-mask descriptor accepted");
    Check(parsed.GeneralFlags == 0 && parsed.SpecificFlags == 0 &&
          parsed.AddressWidth == 32 && parsed.Base == 0xE1020000ULL &&
          parsed.Maximum == 0xFFFFULL && parsed.Length == 0x10000ULL &&
          parsed.TranslationOffset == 0 && bytes[46] == 0x79 && bytes[47] == 0,
          "SARAH_DT descriptor bytes and decoded values match physical evidence");
    Check(parsed.MaximumEncoding ==
              guideXOS::pci_resource::ResourceMaximumEncoding::EDK2AlignmentMask &&
          parsed.RangeEnd == 0xE102FFFFULL,
          "SARAH_DT mask is bounded to the aligned 64 KiB aperture");

    MakeResource(bytes, 32, 0xE1020000ULL, 0x10000ULL);
    WriteLittleEndian64(bytes + 22, 0xFFFEULL);
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "non-contiguous EDK II alignment mask rejected");
    MakeResource(bytes, 32, 0xE1020000ULL, 0x8000ULL);
    WriteLittleEndian64(bytes + 22, 0xFFFFULL);
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "alignment mask and advertised aperture size mismatch rejected");
    MakeResource(bytes, 32, 0xE1021000ULL, 0x10000ULL);
    WriteLittleEndian64(bytes + 22, 0xFFFFULL);
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "misaligned EDK II mask aperture base rejected");
    guideXOS::pci_resource::ResourceMaximumEncoding ambiguousEncoding;
    uint64_t ambiguousEnd = 0;
    Check(!guideXOS::pci_resource::TryInterpretResourceMaximum(
              0, 0xFFFFULL, 0x10000ULL, &ambiguousEncoding, &ambiguousEnd),
          "maximum matching both interpretations rejected as ambiguous");
    Check(!guideXOS::pci_resource::TryInterpretResourceMaximum(
              0xFFFFFFFFFFFF0000ULL, 0x1FFFFULL, 0x20000ULL,
              &ambiguousEncoding, &ambiguousEnd),
          "overflowing alignment-mask aperture rejected");

    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    UpdateChecksum(bytes);
    Check(TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "valid nonzero ACPI end-tag checksum accepted");
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes) - 1, &parsed),
          "truncated resource template rejected");

    bytes[0] = 0x87;
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "non-QWORD descriptor rejected");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    bytes[1] = 0x2A;
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "malformed descriptor length rejected");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    bytes[46] = 0x00;
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "missing end tag rejected");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    bytes[46] = 0x7A;
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "wrong end-tag descriptor rejected");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    uint8_t trailing[49];
    memcpy(trailing, bytes, sizeof(bytes));
    trailing[48] = 0x00;
    Check(!TryParseSingleQwordMemoryResource(trailing, sizeof(trailing), &parsed),
          "trailing data after end tag rejected");
    uint8_t multipleRanges[94] = {};
    MakeResource(multipleRanges, 32, 0xE0000000ULL, 0x1000);
    MakeResource(multipleRanges + 46, 32, 0xE0001000ULL, 0x1000);
    Check(!TryParseSingleQwordMemoryResource(multipleRanges,
              sizeof(multipleRanges), &parsed),
          "multiple QWORD ranges fail closed outside the single-range contract");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    bytes[47] = 0x01;
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "invalid nonzero checksum rejected");

    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    bytes[3] = 1;
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "I/O resource rejected");
    MakeResource(bytes, 48, 0x00000001FEC01000ULL, 0x10000ULL);
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "unsupported address width rejected");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    bytes[4] |= 0x80;
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "reserved general flags rejected");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    bytes[5] = 0x02;
    Check(TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "cacheable memory resource accepted");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    bytes[5] = 0x04;
    Check(TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "write-combining memory resource accepted");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    bytes[5] = 0x07;
    Check(TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "read-write prefetchable memory resource accepted");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    bytes[5] = 0x40;
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "reserved specific flags rejected");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    WriteLittleEndian64(bytes + 30, 0x1000);
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "translated address range rejected");
    MakeResource(bytes, 32, 0xE0000000ULL, 0x200000000ULL);
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "32-bit descriptor extending above 4 GiB rejected");
    MakeResource(bytes, 64, 0xFFFFFFFFFFFFFF00ULL, 0x200ULL);
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "overflowing resource range rejected");
    MakeResource(bytes, 64, 0x00000001FEC01000ULL, 0x10000ULL);
    WriteLittleEndian64(bytes + 22, 0x00000001FEC10FF0ULL);
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "inconsistent inclusive maximum rejected");

    printf("xHCI PCI resource descriptor tests: PASS (%u assertions)\n", assertions);
    return 0;
}
