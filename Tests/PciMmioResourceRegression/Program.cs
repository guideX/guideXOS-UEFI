using System;
using guideXOS.Kernel.Drivers;

internal static unsafe class Program {
    private static int _passed;
    private static int _failed;

    private static void Check(string name, bool condition) {
        if (condition) {
            _passed++;
            Console.WriteLine("PASS " + name);
        } else {
            _failed++;
            Console.WriteLine("FAIL " + name);
        }
    }

    private static int Main() {
        // Synthetic PCI and EFI descriptor fixtures only; these values are
        // not claimed to describe the AIDA_LPT hardware.
        Check("32-bit memory BAR decode",
            PciMmioResource.TryDecodeBar(0x80000000U, 0,
                out ulong base32, out bool is64_32) &&
            base32 == 0x80000000UL && !is64_32);
        Check("32-bit BAR size and minimum span",
            PciMmioResource.TryDecodeSize(0xFFFF8000U, 0, false,
                out ulong size32) && size32 == 0x8000UL &&
            PciMmioResource.IsValidBarRange(0x80000000UL, size32, 0x5406UL));

        Check("64-bit memory BAR decode",
            PciMmioResource.TryDecodeBar(0x80000004U, 0x00000001U,
                out ulong base64, out bool is64_64) &&
            base64 == 0x180000000UL && is64_64);
        Check("64-bit BAR with high-address-only base decode",
            PciMmioResource.TryDecodeBar(0x00000004U, 0x00000001U,
                out ulong highOnlyBase, out bool highOnly64) &&
            highOnlyBase == 0x100000000UL && highOnly64);
        Check("64-bit BAR size",
            PciMmioResource.TryDecodeSize(0xFFFF8004U, 0xFFFFFFFFU, true,
                out ulong size64) && size64 == 0x8000UL);
        Check("64-bit BAR range is aligned and sufficient",
            PciMmioResource.IsValidBarRange(0x180000000UL, 0x8000UL, 0x5406UL));

        Check("zero BAR rejected",
            !PciMmioResource.TryDecodeBar(0, 0,
                out _, out _));
        Check("I/O BAR rejected",
            !PciMmioResource.TryDecodeBar(0x80000001U, 0,
                out _, out _));
        Check("below-1MB BAR rejected",
            !PciMmioResource.TryDecodeBar(0x80000002U, 0,
                out _, out _));
        Check("reserved BAR type rejected",
            !PciMmioResource.TryDecodeBar(0x80000006U, 0,
                out _, out _));
        Check("non-canonical 64-bit BAR rejected",
            !PciMmioResource.TryDecodeBar(0x80000004U, 0x00008000U,
                out _, out _));
        Check("undersized BAR rejected",
            !PciMmioResource.IsValidBarRange(0x80000000UL, 0x4000UL, 0x5406UL));
        Check("misaligned BAR rejected",
            !PciMmioResource.IsValidBarRange(0x80001000UL, 0x8000UL, 0x5406UL));
        Check("non-power-of-two BAR mask rejected",
            !PciMmioResource.TryDecodeSize(0xFFFFB000U, 0, false,
                out _));
        Check("absent BAR size mask rejected",
            !PciMmioResource.TryDecodeSize(0, 0, false, out _));

        Check("supported Intel Ethernet fixture",
            Intel825xxHardware.IsSupported(0x8086, 0x100E, 0x02, 0x00));
        Check("unknown Intel device is unsupported",
            !Intel825xxHardware.IsSupported(0x8086, 0xFFFF, 0x02, 0x00));
        Check("non-Intel vendor is unsupported",
            !Intel825xxHardware.IsSupported(0x10EC, 0x8168, 0x02, 0x00));
        Check("wrong class is unsupported",
            !Intel825xxHardware.IsSupported(0x8086, 0x100E, 0x01, 0x06));
        Check("non-Ethernet network class is unsupported",
            !Intel825xxHardware.IsSupported(0x8086, 0x100E, 0x02, 0x80));

        byte* descriptor = stackalloc byte[48];
        for (int i = 0; i < 48; i++) descriptor[i] = 0;
        *(uint*)descriptor = (uint)PciMmioResource.EfiMemoryMappedIo;
        *(ulong*)(descriptor + 8) = 0x80000000UL;
        *(ulong*)(descriptor + 24) = 8UL;
        ulong mapAddress = (ulong)descriptor;

        Check("EFI MMIO descriptor covers full synthetic BAR",
            PciMmioResource.IsCoveredByEfiMmioMap(mapAddress, 1, 48,
                0x80000000UL, 0x8000UL));
        Check("EFI MMIO descriptor rejects out-of-range window",
            !PciMmioResource.IsCoveredByEfiMmioMap(mapAddress, 1, 48,
                0x80000000UL, 0x9000UL));
        *(uint*)descriptor = 7;
        Check("ordinary RAM descriptor is not MMIO authority",
            !PciMmioResource.IsCoveredByEfiMmioMap(mapAddress, 1, 48,
                0x80000000UL, 0x8000UL));
        *(uint*)descriptor = (uint)PciMmioResource.EfiMemoryMappedIo;
        Check("invalid descriptor stride rejected",
            !PciMmioResource.IsCoveredByEfiMmioMap(mapAddress, 1, 39,
                0x80000000UL, 0x8000UL));
        Check("oversized descriptor count rejected",
            !PciMmioResource.IsCoveredByEfiMmioMap(mapAddress, 65537, 48,
                0x80000000UL, 0x8000UL));
        Check("missing map rejects mapping authority",
            !PciMmioResource.IsCoveredByEfiMmioMap(0, 1, 48,
                0x80000000UL, 0x8000UL));

        Check("required register span rounds to bounded pages",
            PciMmioResource.TryGetPageRange(0x80000000UL, 0x5406UL,
                out ulong pageBase, out ulong pageLength) &&
            pageBase == 0x80000000UL && pageLength == 0x6000UL &&
            PciMmioResource.IsRangeContained(0x80000000UL, 0x8000UL,
                pageBase, pageLength));
        Check("page range beyond BAR rejected",
            !PciMmioResource.IsRangeContained(0x80000000UL, 0x4000UL,
                0x80000000UL, 0x6000UL));
        Check("non-canonical page range rejected",
            !PciMmioResource.TryGetPageRange(0x00007FFFFFFFF000UL, 0x2000UL,
                out _, out _));

        Console.WriteLine("RESULT passed=" + _passed + " failed=" + _failed);
        return _failed == 0 ? 0 : 1;
    }

}
