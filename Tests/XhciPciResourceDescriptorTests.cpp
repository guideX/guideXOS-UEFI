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
    using guideXOS::pci_resource::TryParseSingleQwordMemoryResource;

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
          parsed.Maximum == 0x00000001FEC10FFFULL,
          "64-bit descriptor end and width decoded");

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
    Check(!TryParseSingleQwordMemoryResource(bytes, sizeof(bytes), &parsed),
          "cacheable resource attributes rejected");
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
