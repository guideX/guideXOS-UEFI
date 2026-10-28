using guideXOS.Misc;
using System.Collections.Generic;
namespace guideXOS.Kernel.Drivers {
    public class PCIDevice {
        public ushort Bus;
        public ushort Slot;
        public ushort Function;
        public ushort VendorID;

        public ushort DeviceID;

        public byte ClassID;
        public byte SubClassID;
        public byte ProgIF;
        public byte IRQ;

        public uint Bar0;
        public uint Bar1;
        public uint Bar2;
        public uint Bar3;
        public uint Bar4;
        public uint Bar5;

        //PCI Express
        public ushort Segment;
        public bool IsPCIEDevice;

        public void WriteRegister(ushort Register, ushort Value) {
            PCI.WriteRegister16(Bus, Slot, Function, (byte)Register, (ushort)(ReadRegister(Register) | Value));
        }

        public ushort ReadRegister(ushort Register) {
            return PCI.ReadRegister16(Bus, Slot, Function, (byte)Register);
        }
    }

    public static unsafe class PCI {
        public static List<PCIDevice> Devices;
        private static ulong _uefiMemoryMap;
        private static ulong _uefiMemoryMapEntryCount;
        private static ulong _uefiMemoryMapDescriptorSize;

        public static PCIDevice GetDevice(ushort VendorID, ushort DeviceID) {
            for (int i = 0; i < Devices.Count; i++) {
                if (
                    Devices[i] != null &&
                    Devices[i].VendorID == VendorID &&
                    Devices[i].DeviceID == DeviceID
                    ) {
                    return Devices[i];
                }
            }
            return null;
        }

        public static PCIDevice GetDevice(byte ClassID, byte SubClassID, byte ProgIF) {
            for (int i = 0; i < Devices.Count; i++) {
                if (
                    Devices[i] != null &&
                    Devices[i].ClassID == ClassID &&
                    Devices[i].SubClassID == SubClassID &&
                    Devices[i].ProgIF == ProgIF
                    ) {
                    return Devices[i];
                }
            }
            return null;
        }

        public static void Initialize() {
            Initialize(0, 0, 0);
        }

        public static void Initialize(ulong memoryMap,
                                      ulong memoryMapEntryCount,
                                      ulong memoryMapDescriptorSize) {
            _uefiMemoryMap = memoryMap;
            _uefiMemoryMapEntryCount = memoryMapEntryCount;
            _uefiMemoryMapDescriptorSize = memoryMapDescriptorSize;
            BootConsole.WriteLine("[PCI] INIT");
            
            Devices = new List<PCIDevice>();
          
            // Try a simple PCI read first - this might crash
            uint addr = 0x80000000; // Bus 0, Slot 0, Function 0, Offset 0
            Native.Out32(0xCF8, addr);
            uint val = Native.In32(0xCFC);
            
            if ((GetHeaderType(0x0, 0x0, 0x0) & 0x80) == 0) {
                // Debug: before CheckBus
                CheckBus(0);
            } else {
                for (ushort fn = 0; fn < 8; fn++) {
                    if (GetVendorID(0x0, 0x0, fn) != 0xFFFF)
                        break;

                    CheckBus(fn);
                }
            }

            PCIExpress.Initialize();
            BootConsole.WriteLine("[PCI] Devices enumerated");
            
        }

        /// <summary>
        /// Validates and maps only the bounded register span used by the
        /// Intel 825xx driver.  The resource must be a sized memory BAR whose
        /// full aperture is reserved as EFI MMIO before it is mapped.
        /// </summary>
        internal static bool TryMapIntel825xxBar0(PCIDevice device,
                                                  ulong requiredBytes,
                                                  out ulong baseAddress) {
            baseAddress = 0;
            if (device == null || device.Segment != 0 ||
                device.ClassID != 0x02 || device.SubClassID != 0x00)
                return false;

            // Keep an otherwise matched NIC inert even when its BAR or the
            // UEFI MMIO reservation is missing or malformed.
            ushort originalCommand = ReadRegister16(device.Bus, device.Slot,
                                                    device.Function, 0x04);
            WriteRegister16(device.Bus, device.Slot, device.Function, 0x04,
                            (ushort)(originalCommand & ~0x0007));

            if (_uefiMemoryMap == 0 || _uefiMemoryMapEntryCount == 0 ||
                _uefiMemoryMapDescriptorSize < 40 ||
                _uefiMemoryMapDescriptorSize > 4096 ||
                (ReadRegister8(device.Bus, device.Slot, device.Function, 0x0E) & 0x7F) != 0)
                return false;

            uint barLow = ReadRegister32(device.Bus, device.Slot,
                                         device.Function, 0x10);
            uint barHigh = 0;
            bool is64Bit = (barLow & 1U) == 0 &&
                           ((barLow >> 1) & 3U) == 2U;
            if (is64Bit) {
                barHigh = ReadRegister32(device.Bus, device.Slot,
                                         device.Function, 0x14);
            }
            if (!PciMmioResource.TryDecodeBar(barLow, barHigh,
                                              out ulong decodedBase,
                                              out bool decodedAs64Bit) ||
                decodedAs64Bit != is64Bit)
                return false;

            // PCI BAR sizing is performed with I/O, memory, and bus-master
            // decode disabled. Keep decode disabled until validation and map
            // installation succeed.
            WriteRegister32(device.Bus, device.Slot, device.Function,
                            0x10, 0xFFFFFFFFU);
            if (is64Bit)
                WriteRegister32(device.Bus, device.Slot, device.Function,
                                0x14, 0xFFFFFFFFU);
            uint maskLow = ReadRegister32(device.Bus, device.Slot,
                                          device.Function, 0x10);
            uint maskHigh = is64Bit
                ? ReadRegister32(device.Bus, device.Slot, device.Function, 0x14)
                : 0;

            if (is64Bit)
                WriteRegister32(device.Bus, device.Slot, device.Function,
                                0x14, barHigh);
            WriteRegister32(device.Bus, device.Slot, device.Function,
                            0x10, barLow);

            if (!PciMmioResource.TryDecodeSize(maskLow, maskHigh, is64Bit,
                                               out ulong barSize)) return false;
            if (!PciMmioResource.IsValidBarRange(decodedBase, barSize,
                                                 requiredBytes)) return false;
            if (!PciMmioResource.TryGetPageRange(decodedBase, requiredBytes,
                                                 out ulong mappedBase,
                                                 out ulong mappedLength)) return false;
            if (!PciMmioResource.IsRangeContained(decodedBase, barSize,
                                                  mappedBase, mappedLength)) return false;
            if (!PciMmioResource.IsCoveredByEfiMmioMap(
                    _uefiMemoryMap, _uefiMemoryMapEntryCount,
                    _uefiMemoryMapDescriptorSize, decodedBase, barSize))
                return false;

            if (!PageTable.MapMmioRange(decodedBase, requiredBytes))
                return false;

            // Memory decoding and DMA are enabled only once the validated
            // physical register window is present in the current page table.
            WriteRegister16(device.Bus, device.Slot, device.Function, 0x04,
                            (ushort)((originalCommand & ~0x0001) | 0x0006));
            baseAddress = decodedBase;
            return true;
        }

        public static void CheckBus(ushort Bus) {
            for (ushort slot = 0; slot < 32; slot++) {
                ushort vendorID = GetVendorID(Bus, slot, 0);
                if (vendorID == 0xFFFF) {
                    continue;
                }
                
                PCIDevice device = new PCIDevice();
                device.Bus = Bus;
                device.Slot = slot;
                device.Function = 0;
                device.VendorID = vendorID;
                device.Segment = 0;
                device.IsPCIEDevice = false;

                device.Bar0 = ReadRegister32(device.Bus, device.Slot, device.Function, 0x10);
                device.Bar1 = ReadRegister32(device.Bus, device.Slot, device.Function, 0x14);
                device.Bar2 = ReadRegister32(device.Bus, device.Slot, device.Function, 0x18);
                device.Bar3 = ReadRegister32(device.Bus, device.Slot, device.Function, 0x1C);
                device.Bar4 = ReadRegister32(device.Bus, device.Slot, device.Function, 0x20);
                device.Bar5 = ReadRegister32(device.Bus, device.Slot, device.Function, 0x24);

                device.ClassID = ReadRegister8(device.Bus, device.Slot, device.Function, 11);
                device.SubClassID = ReadRegister8(device.Bus, device.Slot, device.Function, 10);
                device.ProgIF = ReadRegister8(device.Bus, device.Slot, device.Function, 9);
                device.IRQ = (byte)(0x20 + ReadRegister8(device.Bus, device.Slot, device.Function, 60));

                device.DeviceID = ReadRegister16(device.Bus, device.Slot, device.Function, 2);

                Devices.Add(device);

                if (device.ClassID == 0x06 && device.SubClassID == 0x04) {
                    CheckBus(ReadRegister8(device.Bus, device.Slot, device.Function, 25));
                }
            }
        }

        public static void WriteRegister32(ushort Bus, ushort Slot, ushort Function, byte aRegister, uint Value) {
            uint xAddr = GetAddressBase(Bus, Slot, Function) | ((uint)(aRegister & 0xFC));
            Native.Out32(0xCF8, xAddr);
            Native.Out32(0xCFC, Value);
        }

        public static uint ReadRegister32(ushort Bus, ushort Slot, ushort Function, byte aRegister) {
            uint xAddr = PCI.GetAddressBase(Bus, Slot, Function) | ((uint)(aRegister & 0xFC));
            Native.Out32(0xCF8, xAddr);
            return Native.In32(0xCFC);
        }

        public static ushort ReadRegister16(ushort Bus, ushort Slot, ushort Function, byte aRegister) {
            uint xAddr = PCI.GetAddressBase(Bus, Slot, Function) | ((uint)(aRegister & 0xFC));
            Native.Out32(0xCF8, xAddr);
            return (ushort)(Native.In32(0xCFC) >> ((aRegister % 4) * 8) & 0xFFFF);
        }

        public static byte ReadRegister8(ushort Bus, ushort Slot, ushort Function, byte aRegister) {
            uint xAddr = PCI.GetAddressBase(Bus, Slot, Function) | ((uint)(aRegister & 0xFC));
            Native.Out32(0xCF8, xAddr);
            return ((byte)(Native.In32(0xCFC) >> ((aRegister % 4) * 8) & 0xFF));
        }

        public static void WriteRegister16(ushort Bus, ushort Slot, ushort Function, byte aRegister, ushort Value) {
            uint xAddr = GetAddressBase(Bus, Slot, Function) | ((uint)(aRegister & 0xFC));
            Native.Out32(0xCF8, xAddr);
            Native.Out16(0xCFC, Value);
        }

        public static ushort GetVendorID(ushort Bus, ushort Slot, ushort Function) {
            uint xAddr = GetAddressBase(Bus, Slot, Function) | 0x0 & 0xFC;
            Native.Out32(0xCF8, xAddr);
            return (ushort)(Native.In32(0xCFC) >> ((0x0 % 4) * 8) & 0xFFFF);
        }

        public static ushort GetHeaderType(ushort Bus, ushort Slot, ushort Function) {
            uint xAddr = GetAddressBase(Bus, Slot, Function) | 0xE & 0xFC;
            Native.Out32(0xCF8, xAddr);
            return (byte)(Native.In32(0xCFC) >> ((0xE % 4) * 8) & 0xFF);
        }

        public static uint GetAddressBase(ushort Bus, uint Slot, uint Function) {
            return (uint)(0x80000000 | (Bus << 16) | ((Slot & 0x1F) << 11) | ((Function & 0x07) << 8));
        }
    }
}
