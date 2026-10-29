#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <fstream>
#include <string>
#include <vector>

#include "../guideXOSBootLoader/paging.h"

static unsigned int assertions = 0;

static void Check(bool condition, const char* message) {
    ++assertions;
    if (!condition) {
        fprintf(stderr, "FAIL: %s\n", message);
        exit(1);
    }
}

static uint64_t ReadLittleEndian64(const uint8_t* bytes) {
    uint64_t value = 0;
    for (unsigned int i = 0; i < 8; ++i)
        value |= (uint64_t)bytes[i] << (i * 8);
    return value;
}

int main(int argc, char** argv) {
    using guideXOS::paging::PciMmioResourceEntry;
    using guideXOS::paging::PciResourceHandoff;

    Check(argc == 2, "shared ABI fixture path supplied");
    std::ifstream input(argv[1]);
    Check(input.good(), "shared ABI fixture opened");
    std::vector<uint8_t> fixture;
    std::string token;
    while (input >> token) {
        char* end = nullptr;
        unsigned long value = strtoul(token.c_str(), &end, 16);
        Check(token.size() == 2 && end != token.c_str() && *end == '\0' &&
              value <= 0xFF, "fixture token is one byte of hexadecimal");
        fixture.push_back((uint8_t)value);
    }

    Check(fixture.size() == 144, "shared fixture contains two active entries");
    Check(ReadLittleEndian64(fixture.data() + 32) == 0xE0000000ULL &&
          ReadLittleEndian64(fixture.data() + 96) == 0x00000001FEC01000ULL,
          "shared fixture stores distinct 32-bit and 64-bit BAR addresses");

    PciResourceHandoff handoff = {};
    handoff.Magic = 0x52505847u;
    handoff.Version = 1;
    handoff.EntrySize = (uint16_t)sizeof(PciMmioResourceEntry);
    handoff.EntryCount = 2;
    handoff.DiscoveryStatus = 1;

    PciMmioResourceEntry& first = handoff.Entries[0];
    first.Bus = 0;
    first.Device = 20;
    first.AddressWidth = 32;
    first.GeneralFlags = 0x0F;
    first.SpecificFlags = 0x01;
    first.VendorId = 0x8086;
    first.DeviceId = 0x1234;
    first.BarConfigurationBase = 0xE0000000ULL;
    first.MmioBase = 0xE0000000ULL;
    first.ApertureLength = 0x10000ULL;
    first.ResourceMaximum = 0xE000FFFFULL;

    PciMmioResourceEntry& second = handoff.Entries[1];
    second.Bus = 2;
    second.Device = 3;
    second.Function = 1;
    second.AddressWidth = 64;
    second.GeneralFlags = 0x0F;
    second.SpecificFlags = 0x01;
    second.VendorId = 0x8086;
    second.DeviceId = 0x5678;
    second.BarConfigurationBase = 0x00000001FEC01000ULL;
    second.MmioBase = 0x00000001FEC01000ULL;
    second.ApertureLength = 0x10000ULL;
    second.ResourceMaximum = 0x00000001FEC10FFFULL;

    Check(memcmp(&handoff, fixture.data(), fixture.size()) == 0,
          "production C++ handoff struct serializes byte-for-byte to the shared C# fixture");
    printf("xHCI PCI resource handoff ABI tests: PASS (%u assertions)\n",
           assertions);
    return 0;
}
