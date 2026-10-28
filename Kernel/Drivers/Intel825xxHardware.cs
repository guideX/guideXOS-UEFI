namespace guideXOS.Kernel.Drivers {
    /// <summary>Device-ID and class gate for the legacy Intel 825xx driver.</summary>
    internal static class Intel825xxHardware {
        internal static bool IsSupported(ushort vendorId, ushort deviceId,
                                         byte classId, byte subclassId) {
            if (vendorId != 0x8086 || classId != 0x02 || subclassId != 0x00)
                return false;

            switch (deviceId) {
                case 0x1000: case 0x1001: case 0x1004: case 0x1008:
                case 0x1009: case 0x100C: case 0x100D: case 0x100E:
                case 0x100F: case 0x1010: case 0x1011: case 0x1012:
                case 0x1013: case 0x1014: case 0x1015: case 0x1016:
                case 0x1017: case 0x1018: case 0x1019: case 0x101A:
                case 0x101D: case 0x101E: case 0x1026: case 0x1027:
                case 0x1028: case 0x1049: case 0x104A: case 0x104B:
                case 0x104C: case 0x104D: case 0x105E: case 0x105F:
                case 0x1060: case 0x1075: case 0x1076: case 0x1077:
                case 0x1078: case 0x1079: case 0x107A: case 0x107B:
                case 0x107C: case 0x107D: case 0x107E: case 0x107F:
                case 0x108A: case 0x108B: case 0x108C: case 0x1096:
                case 0x1098: case 0x1099: case 0x109A: case 0x10A4:
                case 0x10A7: case 0x10A9: case 0x10B5: case 0x10B9:
                case 0x10BA: case 0x10BB: case 0x10BC: case 0x10BD:
                case 0x10C4: case 0x10C5: case 0x10C9: case 0x10D3:
                case 0x10CB: case 0x10E5: case 0x10EA: case 0x10EB:
                case 0x10EF: case 0x10F0: case 0x10F5: case 0x1502:
                case 0x1503: case 0x150A: case 0x150E: case 0x1521:
                case 0x1533: case 0x157B: case 0x153A: case 0x153B:
                case 0x1559: case 0x155A: case 0x15A0: case 0x15A1:
                case 0x15A2: case 0x15A3: case 0x156F: case 0x1570:
                case 0x15B7: case 0x15B8: case 0x15BB: case 0x15D7:
                case 0x15E3:
                    return true;
                default:
                    return false;
            }
        }
    }
}
