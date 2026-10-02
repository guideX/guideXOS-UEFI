using guideXOS.FS;
using guideXOS.Misc;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace guideXOS.Kernel.Drivers {
    /// <summary>AHCI SATA discovery and bounded ATA DMA block I/O.</summary>
    public static unsafe class SATA {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HBA {
            public uint HostCapability;
            public uint GlobalHostControl;
            public uint InterruptStatus;
            public uint PortsImplemented;
            public uint Version;
            public uint CCCControl;
            public uint CCCPorts;
            public uint EnclosureManagementLocation;
            public uint EnclosureManagementControl;
            public uint HostCapabilitiesExtended;
            public uint BIOSHandoffControlStatus;
            public fixed byte Reserved0[0x74];
            public fixed byte Vendor[0x60];
            public HBAPort Ports;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HBAPort {
            public ulong CommandListBase;
            public ulong FISBaseAddress;
            public uint InterruptStatus;
            public uint InterruptEnable;
            public uint CommandStatus;
            public uint Reserved0;
            public uint TaskFileData;
            public uint Signature;
            public uint SataStatus;
            public uint SataControl;
            public uint SataError;
            public uint SataActive;
            public uint CommandIssue;
            public uint SataNotification;
            public uint FISSwitchControl;
            public fixed uint Reserved1[11];
            public fixed uint Vendor[4];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct HBACommandHeader {
            public ushort Flags;
            public ushort PRDTLength;
            public uint PRDByteCount;
            public ulong CommandTableBaseAddress;
            public fixed uint Reserved1[4];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct HBACommandTable {
            public fixed byte CommandFIS[64];
            public fixed byte ATAPICommand[16];
            public fixed byte Reserved[48];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct HBAPRDTEntry {
            public ulong DataBaseAddress;
            public uint Reserved;
            public uint ByteCountAndFlags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FIS_REG_H2D {
            public byte FISType;
            public byte PortMultiplierAndCommand;
            public byte Command;
            public byte FeatureLow;
            public byte LBA0;
            public byte LBA1;
            public byte LBA2;
            public byte Device;
            public byte LBA3;
            public byte LBA4;
            public byte LBA5;
            public byte FeatureHigh;
            public ushort Count;
            public byte IsochronousCommandCompletion;
            public byte Control;
            public fixed byte Reserved[4];
        }

        public enum SATAPortType : byte { NONE = 0, SATA = 1, SEMB = 2, PM = 3, ATAPI = 4 }

        public static HBA* Controller;
        public static List<SATADevice> Ports;
        private const uint HbaGlobalHostControlAhciEnable = 1U << 31;
        private const uint HbaCapabilities2BiosHandoff = 1U;
        private const uint HbaBiosOwned = 1U;
        private const uint HbaOsOwned = 2U;
        private const uint PortCmdStart = 1U;
        private const uint PortCmdFisReceiveEnable = 1U << 4;
        private const uint PortCmdFisReceiveRunning = 1U << 14;
        private const uint PortCmdCommandListRunning = 1U << 15;
        private const uint PortInterruptTaskFileError = 1U << 30;
        private const int PollLimit = 2_000_000;

        /// <summary>Discard discovery state left in RAM by a warm firmware reboot.</summary>
        public static void ResetForBoot() {
            Controller = null;
            Ports = null;
        }

        public static void Initialize() {
            if (Ports != null) return;
            Ports = new List<SATADevice>();
            if (PCI.Devices == null) return;

            for (int i = 0; i < PCI.Devices.Count; i++) {
                PCIDevice pci = PCI.Devices[i];
                if (pci == null || pci.ClassID != 0x01 || pci.SubClassID != 0x06 ||
                    (pci.Bar5 & 1U) != 0) continue;
                // The legacy config-space scan and PCIe ECAM scan can both
                // report the same function. Initialize each physical HBA once.
                if (WasEnumeratedEarlier(i, pci)) continue;

                uint bar = pci.Bar5 & 0xFFFFFFF0U;
                if (bar == 0) continue;
                // AHCI BAR5 is device MMIO. UEFI boot tables map the framebuffer
                // and PCI configuration window, but need not map a controller's
                // BAR. Cover the HBA header plus all 32 port-register blocks
                // before the first volatile register read.
                if (!guideXOS.PageTable.MapMmioRange((ulong)bar, 0x1100UL)) {
                    BootConsole.WriteLine("[AHCI] could not map controller MMIO BAR");
                    continue;
                }
                ushort commandRegister = pci.ReadRegister(0x04);
                pci.WriteRegister(0x04, (ushort)(commandRegister | 0x0006)); // memory space + bus mastering
                HBA* hba = (HBA*)(ulong)bar;
                if (!TakeOwnership(hba)) {
                    BootConsole.WriteLine("[AHCI] BIOS handoff timed out");
                    continue;
                }
                hba->GlobalHostControl = (hba->GlobalHostControl | HbaGlobalHostControlAhciEnable) & ~(1U << 1);
                Controller = hba;

                for (int portIndex = 0; portIndex < 32; portIndex++) {
                    if ((hba->PortsImplemented & (1U << portIndex)) == 0) continue;
                    HBAPort* port = &(&hba->Ports)[portIndex];
                    if ((port->SataStatus & 0x0FU) != 0x03U || port->Signature != 0x00000101U) continue;
                    SATADevice device = new SATADevice(pci, hba, port, portIndex);
                    if (device.Initialize()) {
                        Ports.Add(device);
                        BootConsole.WriteLine("[AHCI] SATA port " + portIndex.ToString() +
                            " model=" + device.Model + " serial=" + device.Serial +
                            " blockSize=" + device.BlockSize.ToString() +
                            " blocks=" + device.BlockCount.ToString() +
                            " writable=" + device.CanWrite.ToString() +
                            " flush=" + device.CanFlush.ToString());
                    } else {
                        BootConsole.WriteLine("[AHCI] SATA port " + portIndex.ToString() + " initialization failed");
                    }
                }
            }

            BootConsole.WriteLine("[AHCI] discovered SATA disks=" + Ports.Count.ToString());
        }

        private static bool WasEnumeratedEarlier(int index, PCIDevice device) {
            for (int i = 0; i < index; i++) {
                PCIDevice previous = PCI.Devices[i];
                if (previous != null && previous.Segment == device.Segment &&
                    previous.Bus == device.Bus && previous.Slot == device.Slot &&
                    previous.Function == device.Function) return true;
            }
            return false;
        }

        private static bool TakeOwnership(HBA* hba) {
            if ((hba->HostCapabilitiesExtended & HbaCapabilities2BiosHandoff) == 0) return true;
            hba->BIOSHandoffControlStatus |= HbaOsOwned;
            for (int i = 0; i < PollLimit; i++) {
                if ((hba->BIOSHandoffControlStatus & HbaBiosOwned) == 0) return true;
            }
            return false;
        }

        private static bool WaitCommandListStopped(HBAPort* port) {
            for (int i = 0; i < PollLimit; i++) {
                if ((port->CommandStatus & PortCmdCommandListRunning) == 0) return true;
            }
            return false;
        }

        private static bool WaitFisReceiveStopped(HBAPort* port) {
            for (int i = 0; i < PollLimit; i++) {
                if ((port->CommandStatus & PortCmdFisReceiveRunning) == 0) return true;
            }
            return false;
        }

        public sealed class SATADevice : Disk {
            private readonly PCIDevice _pci;
            private readonly HBA* _hba;
            private readonly HBAPort* _port;
            private readonly int _portIndex;
            private HBACommandHeader* _commandHeaders;
            private byte* _fisReceive;
            private ulong _commandTablePage;
            private ulong _blockCount;
            private uint _blockSize;
            private bool _ready;
            private bool _writeSupported;
            private byte _flushCommand;
            private int _commandActive;
            private readonly object _commandLock = new object();

            public SATAPortType PortType => SATAPortType.SATA;
            public string Model { get; private set; } = "unknown";
            public string Serial { get; private set; } = "unknown";
            public bool CanWrite => _ready && _writeSupported;
            public bool CanFlush => _ready && _flushCommand != 0;
            public override uint BlockSize => _blockSize == 0 ? 512U : _blockSize;
            public override ulong BlockCount => _ready ? _blockCount : 0UL;
            public override DiskCapabilities Capabilities {
                get {
                    if (!_ready) return DiskCapabilities.None;
                    DiskCapabilities result = DiskCapabilities.Readable;
                    if (_writeSupported) result |= DiskCapabilities.Writable;
                    if (_flushCommand != 0) result |= DiskCapabilities.FlushSupported;
                    return result;
                }
            }
            public override bool IsAvailable => _ready && (_port->SataStatus & 0x0FU) == 0x03U;

            internal SATADevice(PCIDevice pci, HBA* hba, HBAPort* port, int portIndex) {
                _pci = pci;
                _hba = hba;
                _port = port;
                _portIndex = portIndex;
                _blockSize = 512;
            }

            internal bool Initialize() {
                if (!ConfigurePort()) return false;
                byte* identify = stackalloc byte[512];
                for (int i = 0; i < 512; i++) identify[i] = 0;
                DiskIoResult result = ExecuteCommand(0xEC, 0, 1, false, identify, 512);
                if (result != DiskIoResult.Success) return false;

                // This transport always uses AHCI DMA PRDTs for data and IDENTIFY.
                // Do not advertise the device if ATA IDENTIFY says DMA is unavailable.
                if ((ReadU16(identify, 49) & (1U << 8)) == 0) return false;
                ushort commandSets83 = ReadU16(identify, 83);
                if ((commandSets83 & 0xC000U) != 0x4000U) return false;
                if ((commandSets83 & (1U << 10)) == 0) return false; // require LBA48
                _flushCommand = (commandSets83 & (1U << 13)) != 0 ? (byte)0xEA :
                    ((commandSets83 & (1U << 12)) != 0 ? (byte)0xE7 : (byte)0);
                _writeSupported = (ReadU16(identify, 49) & (1U << 8)) != 0;
                _blockCount = (ulong)ReadU16(identify, 100) |
                    ((ulong)ReadU16(identify, 101) << 16) |
                    ((ulong)ReadU16(identify, 102) << 32) |
                    ((ulong)ReadU16(identify, 103) << 48);
                if (_blockCount == 0) return false;

                ushort sectorInfo = ReadU16(identify, 106);
                if ((sectorInfo & 0xC000) == 0x4000 && (sectorInfo & (1U << 12)) != 0) {
                    uint logicalWords = (uint)ReadU16(identify, 117) |
                        ((uint)ReadU16(identify, 118) << 16);
                    if (logicalWords == 0 || logicalWords > 2048) return false;
                    _blockSize = logicalWords * 2U;
                } else {
                    _blockSize = 512;
                }
                Model = ReadAtaString(identify, 27, 20);
                Serial = ReadAtaString(identify, 10, 10);
                _ready = true;
                return true;
            }

            private bool ConfigurePort() {
                uint command = _port->CommandStatus;
                _port->CommandStatus = command & ~PortCmdStart;
                if (!WaitCommandListStopped(_port)) return false;
                _port->CommandStatus &= ~PortCmdFisReceiveEnable;
                if (!WaitFisReceiveStopped(_port)) return false;

                _commandHeaders = (HBACommandHeader*)Allocator.Allocate(4096);
                _fisReceive = (byte*)Allocator.Allocate(4096);
                _commandTablePage = (ulong)Allocator.Allocate(32UL * 4096UL);
                if (_commandHeaders == null || _fisReceive == null || _commandTablePage == 0) return false;
                Allocator.ZeroFill((IntPtr)_commandHeaders, 4096);
                Allocator.ZeroFill((IntPtr)_fisReceive, 4096);
                Allocator.ZeroFill((IntPtr)_commandTablePage, 32UL * 4096UL);

                _port->CommandListBase = (ulong)_commandHeaders;
                _port->FISBaseAddress = (ulong)_fisReceive;
                for (int slot = 0; slot < 32; slot++) {
                    _commandHeaders[slot].CommandTableBaseAddress = _commandTablePage + ((ulong)slot * 4096UL);
                }
                _port->InterruptStatus = 0xFFFFFFFFU;
                _port->InterruptEnable = 0;
                _port->SataError = 0xFFFFFFFFU;
                _port->CommandStatus |= PortCmdFisReceiveEnable;
                _port->CommandStatus |= PortCmdStart;
                return true;
            }

            private static ushort ReadU16(byte* data, int word) {
                int offset = word * 2;
                return (ushort)(data[offset] | (data[offset + 1] << 8));
            }

            private static string ReadAtaString(byte* data, int firstWord, int wordCount) {
                string value = string.Empty;
                for (int word = 0; word < wordCount; word++) {
                    int offset = (firstWord + word) * 2;
                    char first = (char)data[offset + 1];
                    char second = (char)data[offset];
                    if (first != '\0') value += first;
                    if (second != '\0') value += second;
                }
                int end = value.Length;
                while (end > 0 && value[end - 1] == ' ') end--;
                return end == value.Length ? value : value.Substring(0, end);
            }

            private DiskIoResult ExecuteCommand(byte command, ulong lba, ushort count,
                bool write, byte* data, uint byteCount) {
                lock (_commandLock) {
                    return ExecuteCommandLocked(command, lba, count, write, data, byteCount);
                }
            }

            private DiskIoResult ExecuteCommandLocked(byte command, ulong lba, ushort count,
                bool write, byte* data, uint byteCount) {
                if ((_port->SataStatus & 0x0FU) != 0x03U) return DiskIoResult.MediaUnavailable;
                if (_commandActive != 0) return DiskIoResult.TransportFailure;
                _commandActive = 1;
                try {
                    for (int wait = 0; wait < PollLimit; wait++) {
                        if ((_port->TaskFileData & 0x88U) == 0) break;
                        if (wait == PollLimit - 1) return DiskIoResult.TransportFailure;
                    }

                    int slot = FindFreeSlot();
                    if (slot < 0) return DiskIoResult.TransportFailure;
                    HBACommandHeader* header = &_commandHeaders[slot];
                    header->Flags = (ushort)(5U | (write ? (1U << 6) : 0U) | (1U << 10));
                    header->PRDTLength = byteCount == 0 ? (ushort)0 : (ushort)1;
                    header->PRDByteCount = 0;

                    byte* tableBytes = (byte*)header->CommandTableBaseAddress;
                    for (int i = 0; i < 4096; i++) tableBytes[i] = 0;
                    FIS_REG_H2D* fis = (FIS_REG_H2D*)tableBytes;
                    fis->FISType = 0x27;
                    fis->PortMultiplierAndCommand = 0x80;
                    fis->Command = command;
                    fis->LBA0 = (byte)lba;
                    fis->LBA1 = (byte)(lba >> 8);
                    fis->LBA2 = (byte)(lba >> 16);
                    fis->Device = (byte)(1U << 6);
                    fis->LBA3 = (byte)(lba >> 24);
                    fis->LBA4 = (byte)(lba >> 32);
                    fis->LBA5 = (byte)(lba >> 40);
                    fis->Count = count;

                    if (byteCount != 0) {
                        HBAPRDTEntry* prdt = (HBAPRDTEntry*)(tableBytes + sizeof(HBACommandTable));
                        prdt->DataBaseAddress = (ulong)data;
                        prdt->ByteCountAndFlags = (byteCount - 1U) | (1U << 31);
                    }

                    _port->InterruptStatus = 0xFFFFFFFFU;
                    _port->SataError = 0xFFFFFFFFU;
                    _port->CommandIssue = 1U << slot;
                    for (int wait = 0; wait < PollLimit; wait++) {
                        if ((_port->InterruptStatus & PortInterruptTaskFileError) != 0)
                            return DiskIoResult.TransportFailure;
                        if ((_port->CommandIssue & (1U << slot)) == 0) {
                            uint taskFile = _port->TaskFileData;
                            return (taskFile & (0x01U | 0x20U | 0x80U)) == 0
                                ? DiskIoResult.Success
                                : DiskIoResult.TransportFailure;
                        }
                    }
                    return DiskIoResult.TransportFailure;
                } finally {
                    _commandActive = 0;
                }
            }

            private int FindFreeSlot() {
                uint active = _port->SataActive | _port->CommandIssue;
                uint slotCount = ((_hba->HostCapability >> 8) & 0x1FU) + 1U;
                if (slotCount > 32) slotCount = 32;
                for (int slot = 0; slot < (int)slotCount; slot++) {
                    if ((active & (1U << slot)) == 0) return slot;
                }
                return -1;
            }

            protected override DiskIoResult ReadCore(ulong lba, uint count, byte* data) {
                const uint maxBlocksPerCommand = 128;
                while (count > 0) {
                    uint batch = count > maxBlocksPerCommand ? maxBlocksPerCommand : count;
                    ulong bytes = (ulong)batch * _blockSize;
                    if (bytes > 0xFFFFFFFFU) return DiskIoResult.InvalidRange;
                    DiskIoResult result = ExecuteCommand(0x25, lba, (ushort)batch, false, data, (uint)bytes);
                    if (result != DiskIoResult.Success) return result;
                    lba += batch;
                    count -= batch;
                    data += bytes;
                }
                return DiskIoResult.Success;
            }

            protected override DiskIoResult WriteCore(ulong lba, uint count, byte* data) {
                if (!_writeSupported) return DiskIoResult.ReadOnly;
                const uint maxBlocksPerCommand = 128;
                while (count > 0) {
                    uint batch = count > maxBlocksPerCommand ? maxBlocksPerCommand : count;
                    ulong bytes = (ulong)batch * _blockSize;
                    if (bytes > 0xFFFFFFFFU) return DiskIoResult.InvalidRange;
                    DiskIoResult result = ExecuteCommand(0x35, lba, (ushort)batch, true, data, (uint)bytes);
                    if (result != DiskIoResult.Success) return result;
                    lba += batch;
                    count -= batch;
                    data += bytes;
                }
                return DiskIoResult.Success;
            }

            protected override DiskIoResult FlushCore() {
                if (_flushCommand == 0) return DiskIoResult.FlushUnsupported;
                DiskIoResult result = ExecuteCommand(_flushCommand, 0, 0, false, null, 0);
                return result == DiskIoResult.Success ? result :
                    result == DiskIoResult.MediaUnavailable ? result : DiskIoResult.FlushFailure;
            }
        }
    }
}
