using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace guideXOS.Kernel.Drivers {
#pragma warning disable CS0649
    /// <summary>
    /// Advanced Configuration and Power Interface (ACPI) is an open standard that allows operating systems to configure and discover computer hardware components. It also enables power management, auto configuration, and status monitoring. ACPI was developed by Intel, Microsoft, and Toshiba, and was first released in December 1996
    /// </summary>
    public unsafe class ACPI {
        /// <summary>
        /// Device Located
        /// </summary>
        public static bool DeviceLocated {
            get {
                return _deviceLocated;
            }
        }
        /// <summary>
        /// Device Name
        /// </summary>
        public static string DeviceName {
            get {
                return _deviceName;
            }
        }
        /// <summary>
        /// Device Located
        /// </summary>
        private static bool _deviceLocated;
        /// <summary>
        /// Device Name
        /// </summary>
        private static string _deviceName;
        /// <summary>
        /// SLP TYPa
        /// </summary>
        private static short SLP_TYPa;
        /// <summary>
        /// SLP TYPb
        /// </summary>
        private static short SLP_TYPb;
        /// <summary>
        /// SLP EN
        /// </summary>
        private static short SLP_EN;
        /// <summary>
        /// FADT
        /// </summary>
        public static ACPI_FADT* FADT;
        /// <summary>
        /// MADT
        /// </summary>
        public static ACPI_MADT* MADT;
        /// <summary>
        /// IO APIC
        /// </summary>
        public static APIC_IO_APIC* IO_APIC;
        /// <summary>
        /// HPET
        /// </summary>
        public static ACPI_HPET* HPET;
        /// <summary>
        /// MCFG
        /// </summary>
        public static MCFGHeader* MCFG;

        /// <summary>
        /// List of detected Local APIC IDs (from MADT). Used by SMP + ThreadPool.
        /// </summary>
        public static List<byte> LocalAPIC_CPUIDs;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ACPI_RSDP {
            public fixed sbyte Signature[8];
            public byte Checksum;
            public fixed sbyte OEMID[6];
            public byte Revision;
            public uint RsdtAddress;
            // ACPI 2.0+ extension.  The bootloader prefers this RSDP when
            // both ACPI configuration-table GUIDs are published.
            public uint Length;
            public ulong XsdtAddress;
            public byte ExtendedChecksum;
            public fixed byte Reserved[3];
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ACPI_HEADER {
            public fixed sbyte Signature[4];
            public uint Length;
            public byte Revision;
            public byte Checksum;
            public fixed byte OEMID[6];
            public fixed sbyte OEMTableID[8];
            public uint OEMRevision;
            public uint CreatorID;
            public uint CreatorRevision;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct APIC_HEADER {
            public APIC_TYPE Type;
            public byte Length;
        }

        public enum APIC_TYPE : byte {
            LocalAPIC,
            IOAPIC,
            InterruptOverride
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MCFGHeader {
            public ACPI_HEADER Header;
            public ulong Reserved;
            public MCFGEntry Entry0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MCFGEntry {
            public ulong BaseAddress;
            public ushort Segment;
            public byte StartBus;
            public byte EndBus;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct APIC_LOCAL_APIC {
            public APIC_HEADER Header;
            public byte AcpiProcessorId;
            public byte ApicId;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct APIC_IO_APIC {
            public APIC_HEADER Header;
            public byte IOApicId;
            public byte Reserved;
            public uint IOApicAddress;
            public uint GlobalSystemInterruptBase;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct APIC_INTERRUPT_OVERRIDE {
            public APIC_HEADER Header;
            public byte Bus;
            public byte Source;
            public uint Interrupt;
            public ushort Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ACPI_HPET {
            public ACPI_HEADER Header;
            public byte HardwareRevisionID;
            public byte Attribute;
            public ushort PCIVendorID;
            public ACPI_HPET_ADDRESS_STRUCTURE Addresses;
            public byte HPETNumber;
            public ushort MinimumTick;
            public byte PageProtection;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ACPI_HPET_ADDRESS_STRUCTURE {
            public byte AddressSpaceID;
            public byte RegisterBitWidth;
            public byte RegisterBitOffset;
            public byte Reserved;
            public ulong Address;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ACPI_FADT {
            public ACPI_HEADER Header;

            public uint FirmwareCtrl;
            public uint Dsdt;

            public byte Reserved;

            public byte PreferredPowerManagementProfile;
            public ushort SCI_Interrupt;
            public uint SMI_CommandPort;
            public byte AcpiEnable;
            public byte AcpiDisable;
            public byte S4BIOS_REQ;
            public byte PSTATE_Control;
            public uint PM1aEventBlock;
            public uint PM1bEventBlock;
            public uint PM1aControlBlock;
            public uint PM1bControlBlock;
            public uint PM2ControlBlock;
            public uint PMTimerBlock;
            public uint GPE0Block;
            public uint GPE1Block;
            public byte PM1EventLength;
            public byte PM1ControlLength;
            public byte PM2ControlLength;
            public byte PMTimerLength;
            public byte GPE0Length;
            public byte GPE1Length;
            public byte GPE1Base;
            public byte CStateControl;
            public ushort WorstC2Latency;
            public ushort WorstC3Latency;
            public ushort FlushSize;
            public ushort FlushStride;
            public byte DutyOffset;
            public byte DutyWidth;
            public byte DayAlarm;
            public byte MonthAlarm;
            public byte Century;

            public ushort BootArchitectureFlags;

            public byte Reserved2;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ACPI_MADT {
            public ACPI_HEADER Header;
            public uint LocalAPICAddress;
            public uint Flags;
        }

        private static unsafe ACPI_RSDP* GetRSDP() {
            byte* p = (byte*)0xE0000;
            byte* end = (byte*)0xFFFFF;

            while (p < end) {
                ulong signature = *(ulong*)p;

                if (signature == 0x2052545020445352) // 'RSD PTR '
                {
                    return (ACPI_RSDP*)p;
                }

                p += 16;
            }

            return null;
        }

        private static unsafe bool IsValidRsdp(ACPI_RSDP* rsdp) {
            if (rsdp == null) return false;
            // Signature "RSD PTR "
            if (*(ulong*)rsdp != 0x2052545020445352) return false;
            return true;
        }

        /// <summary>
        /// Initialize ACPI using an explicit RSDP physical address (UEFI-friendly).
        /// 
        /// UEFI bootloaders can locate the RSDP via EFI configuration tables and
        /// pass it to the kernel in BootInfo. Scanning 0xE0000-0xFFFFF is a BIOS-era
        /// technique and is not reliable under UEFI.
        /// </summary>
        public static void InitializeFromRsdp(ulong rsdpPhys) {
            FADT = null;
            MADT = null;
            IO_APIC = null;
            HPET = null;
            MCFG = null;

            ACPI.LocalAPIC_CPUIDs = new List<byte>();

            ACPI_RSDP* rsdp = (ACPI_RSDP*)rsdpPhys;
            if (!IsValidRsdp(rsdp)) {
                BootConsole.WriteLine("[ACPI] Invalid RSDP passed from bootloader");
                _deviceLocated = false;
                _deviceName = "[ACPI] Not Present";
                return;
            }

            ACPI_HEADER* root = null;
            bool usingXsdt = false;

            if (rsdp->Revision >= 2 && rsdp->Length >= 36 && rsdp->XsdtAddress != 0) {
                ACPI_HEADER* xsdt = (ACPI_HEADER*)rsdp->XsdtAddress;
                if (xsdt != null && *(uint*)xsdt == 0x54445358 && // 'XSDT'
                    xsdt->Length >= sizeof(ACPI_HEADER)) {
                    root = xsdt;
                    usingXsdt = true;
                }
            }

            // ACPI 1.0 firmware and malformed/unavailable XSDTs use RSDT.
            if (root == null && rsdp->RsdtAddress != 0) {
                ACPI_HEADER* rsdt = (ACPI_HEADER*)rsdp->RsdtAddress;
                if (rsdt != null && *(uint*)rsdt == 0x54445352 && // 'RSDT'
                    rsdt->Length >= sizeof(ACPI_HEADER)) {
                    root = rsdt;
                }
            }

            if (root == null) {
                BootConsole.WriteLine("[ACPI] XSDT/RSDT not present/invalid");
                _deviceLocated = false;
                _deviceName = "[ACPI] Not Present";
                return;
            }

            uint entrySize = usingXsdt ? sizeof(ulong) : sizeof(uint);
            int entryCount = (int)((root->Length - sizeof(ACPI_HEADER)) / entrySize);
            byte* entries = (byte*)root + sizeof(ACPI_HEADER);

            BootConsole.WriteLine(usingXsdt ? "[ACPI] Using XSDT" : "[ACPI] Using RSDT");
            for (int i = 0; i < entryCount; i++) {
                ulong address = usingXsdt
                    ? ((ulong*)entries)[i]
                    : ((uint*)entries)[i];
                if (address != 0)
                    ParseDT((ACPI_HEADER*)address);
            }

            _deviceLocated = true;
            _deviceName = "[ACPI] ACPI";
            BootConsole.WriteLine("[ACPI] ACPI Initialized (RSDP provided)");
        }

        public static void Initialize() {
            FADT = null;
            MADT = null;
            IO_APIC = null;
            HPET = null;
            MCFG = null;

            ACPI.LocalAPIC_CPUIDs = new List<byte>();
            ACPI_RSDP* rsdp = GetRSDP();
            if (!IsValidRsdp(rsdp)) {
                BootConsole.WriteLine("[ACPI] RSDP not found in legacy scan range");
                _deviceLocated = false;
                _deviceName = "[ACPI] Not Present";
                return;
            }

            ACPI_HEADER* rsdt = (ACPI_HEADER*)rsdp->RsdtAddress;

            if (rsdt != null && *(uint*)rsdt == 0x54445352) //RSDT
            {
                uint* p = (uint*)(rsdt + 1);
                uint* end = (uint*)((byte*)rsdt + rsdt->Length);

                while (p < end) {
                    uint address = *p++;
                    ParseDT((ACPI_HEADER*)address);
                }
            }
            _deviceLocated = true;
            _deviceName = "[ACPI] ACPI";
            BootConsole.WriteLine("[ACPI] ACPI Initialized");
        }

        private static void ParseDT(ACPI_HEADER* hdr) {
            if (hdr == null || hdr->Length < sizeof(ACPI_HEADER)) return;

            if (*(uint*)hdr->Signature == 0x50434146) {
                FADT = (ACPI_FADT*)hdr;

                if (*(uint*)FADT->Dsdt == 0x54445344) //DSDT
                {
                    byte* S5Addr = (byte*)FADT->Dsdt + sizeof(ACPI_HEADER);
                    int dsdtLength = *((int*)FADT->Dsdt + 1) - sizeof(ACPI_HEADER);

                    while (0 < dsdtLength--) {
                        if (*(uint*)S5Addr == 0x5f35535f) //_S5_
                            break;
                        S5Addr++;
                    }

                    if (dsdtLength > 0) {
                        if ((*(S5Addr - 1) == 0x08 || (*(S5Addr - 2) == 0x08 && *(S5Addr - 1) == '\\')) && *(S5Addr + 4) == 0x12) {
                            S5Addr += 5;
                            S5Addr += ((*S5Addr & 0xC0) >> 6) + 2;
                            if (*S5Addr == 0x0A)
                                S5Addr++;
                            SLP_TYPa = (short)(*(S5Addr) << 10);
                            S5Addr++;
                            if (*S5Addr == 0x0A)
                                S5Addr++;
                            SLP_TYPb = (short)(*(S5Addr) << 10);
                            SLP_EN = 1 << 13;

                            return;
                        }
                    }
                }
            } else if (*(uint*)hdr->Signature == 0x43495041) {
                MADT = (ACPI_MADT*)hdr;

                byte* p = (byte*)(MADT + 1);
                byte* end = (byte*)MADT + MADT->Header.Length;
                while (p < end) {
                    APIC_HEADER* header = (APIC_HEADER*)p;
                    APIC_TYPE type = header->Type;
                    byte length = header->Length;

                    if (type == APIC_TYPE.LocalAPIC) {
                        APIC_LOCAL_APIC* pic = (APIC_LOCAL_APIC*)p;
                        if ((pic->Flags & 1) ^ ((pic->Flags >> 1) & 1)) {
                            ACPI.LocalAPIC_CPUIDs.Add(pic->ApicId);
                        }
                    } else if (type == APIC_TYPE.IOAPIC) {
                        APIC_IO_APIC* ioapic = (APIC_IO_APIC*)p;
                        if (IO_APIC == null) {
                            IO_APIC = ioapic;
                        }
                    } else if (type == APIC_TYPE.InterruptOverride) {
                        APIC_INTERRUPT_OVERRIDE* ovr = (APIC_INTERRUPT_OVERRIDE*)p;
                    }

                    p += length;
                }
            } else if (*(uint*)hdr->Signature == 0x54455048) {
                HPET = (ACPI_HPET*)hdr;
            } else if (*(uint*)hdr->Signature == 0x4746434D) {
                MCFG = (MCFGHeader*)hdr;
            }
        }

        public static uint RemapIRQ(uint irq) {
            if (MADT == null) return irq;
            byte* p = (byte*)(MADT + 1);
            byte* end = (byte*)MADT + MADT->Header.Length;

            while (p < end) {
                APIC_HEADER* header = (APIC_HEADER*)p;
                APIC_TYPE type = header->Type;
                byte length = header->Length;

                if (type == APIC_TYPE.InterruptOverride) {
                    APIC_INTERRUPT_OVERRIDE* ovr = (APIC_INTERRUPT_OVERRIDE*)p;

                    if (ovr->Source == irq) {
                        return ovr->Interrupt;
                    }
                }

                p += length;
            }

            return irq;
        }

        public static bool TryGetInterruptOverride(uint source, out uint interrupt, out ushort flags) {
            interrupt = source;
            flags = 0;
            if (MADT == null) return false;

            byte* p = (byte*)(MADT + 1);
            byte* end = (byte*)MADT + MADT->Header.Length;
            while (p < end) {
                APIC_HEADER* header = (APIC_HEADER*)p;
                if (header->Length < 2) break;
                if (header->Type == APIC_TYPE.InterruptOverride) {
                    APIC_INTERRUPT_OVERRIDE* ovr = (APIC_INTERRUPT_OVERRIDE*)p;
                    if (ovr->Source == source) {
                        interrupt = ovr->Interrupt;
                        flags = ovr->Flags;
                        return true;
                    }
                }
                p += header->Length;
            }
            return false;
        }

        public static void Shutdown() {
            Native.Out16((ushort)FADT->PM1aControlBlock, (ushort)(SLP_TYPa | SLP_EN));
            Native.Out16((ushort)FADT->PM1bControlBlock, (ushort)(SLP_TYPb | SLP_EN));
            Native.Hlt();
        }
    }
#pragma warning restore
}
