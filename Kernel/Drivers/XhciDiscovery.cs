using System;
using System.Runtime.InteropServices;

namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// Read-only xHCI PCI and capability parsing. This code has no dependency
    /// on the kernel page table or MMIO accessors so the validation rules can
    /// be exercised with hosted synthetic data.
    /// </summary>
    public static class XhciDiscovery {
        public const ulong MaximumIdentityMappedAddress = 0x00007FFFFFFFFFFFUL;
        public const ulong CapabilityHeaderLength = 0x20UL;

        public enum BarStatus {
            Valid,
            Empty,
            IoSpace,
            UnsupportedMemoryType,
            MissingUpperBar,
            ZeroAddress,
            AddressNotCanonical
        }

        public enum CapabilityFailure {
            None,
            AccessUnavailable,
            ResourceTooSmall,
            ReadFailed,
            RegisterBlockMalformed,
            InvalidCapabilityLength,
            InvalidControllerVersion,
            InvalidControllerLimits,
            InvalidDoorbellOffset,
            InvalidRuntimeOffset,
            RegisterRangeOutOfBounds
        }

        public enum ResourceFailure {
            None,
            ContractMalformed,
            ContractUnsupported,
            ContractTruncated,
            DiscoveryIncomplete,
            ControllerMismatch,
            InvalidBdf,
            InvalidBarIndex,
            InvalidResourceType,
            InvalidAddressWidth,
            InvalidMemoryAttributes,
            MissingAperture,
            ZeroLength,
            RangeOverflow,
            UnsupportedTranslation,
            BarMismatch,
            InvalidBar,
            DuplicateController
        }

        public const uint ResourceContractMagic = 0x52505847U; // "GXPR"
        public const ushort ResourceContractVersion = 1;
        public const ushort FirmwareResourceEntrySize = 64;
        public const uint ResourceDiscoveryComplete = 1;
        public const uint ResourceDiscoveryNoPciIo = 2;
        public const uint ResourceDiscoveryFailed = 3;
        public const uint ResourceDiscoveryIncomplete = 4;
        public const uint ResourceDiscoveryTruncated = 5;
        public const uint MaximumFirmwareResources = 8;
        public const ulong PageSize = 4096;

        /// <summary>
        /// One firmware-owned descriptor copied out of EFI_PCI_IO_PROTOCOL.
        /// The layout is shared with the C++ loader's post-ExitBootServices
        /// handoff and is intentionally fixed at 64 bytes.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = FirmwareResourceEntrySize)]
        public struct FirmwareResourceEntry {
            public ushort Segment;
            public byte Bus;
            public byte Device;
            public byte Function;
            public byte BarIndex;
            public byte AddressWidth;
            public byte ResourceType;
            public byte GeneralFlags;
            public byte SpecificFlags;
            public ushort VendorId;
            public ushort DeviceId;
            public ushort Reserved;
            public ulong BarConfigurationBase;
            public ulong MmioBase;
            public ulong ApertureLength;
            public ulong ResourceMaximum;
            public ulong TranslationOffset;
            public ulong SupportedAttributes;
        }

        public struct MmioMappingPlan {
            public ulong FirstPhysicalPage;
            public ulong PageCount;
            public ulong FirstPageOffset;
            public ulong AccessLength;
        }

        public struct BarInfo {
            public ulong BaseAddress;
            public bool Is64Bit;
            public bool IsPrefetchable;
        }

        public struct Capabilities {
            public byte CapabilityLength;
            public ushort ControllerVersion;
            public byte MaximumDeviceSlots;
            public ushort AdvertisedInterruptResources;
            public byte RootPortCount;
            public uint HcsParams2;
            public uint HcsParams3;
            public uint HccParams1;
            public ushort ScratchpadBufferCount;
            public bool Uses64ByteContexts;
            public bool Supports64BitAddresses;
            public uint DoorbellOffset;
            public uint RuntimeRegisterOffset;
            public byte U1DeviceExitLatency;
            public ushort U2DeviceExitLatency;
        }

        /// <summary>A deliberately read-only MMIO seam for hosted fixtures.</summary>
        public interface IReadOnlyMmioReader {
            bool IsMapped { get; }
            bool TryRead32(ulong offset, out uint value);
        }

        public static bool IsXhciFunction(byte classCode, byte subclass,
                                          byte programmingInterface) =>
            classCode == 0x0C && subclass == 0x03 &&
            programmingInterface == 0x30;

        public static BarStatus TryParseMemoryBar(uint low, uint high,
                                                   bool hasUpperBar,
                                                   out BarInfo result) {
            result = new BarInfo();
            if (low == 0) return BarStatus.Empty;
            if ((low & 1U) != 0) return BarStatus.IoSpace;

            uint memoryType = (low >> 1) & 0x3U;
            bool is64Bit;
            if (memoryType == 0) {
                is64Bit = false;
            } else if (memoryType == 2) {
                if (!hasUpperBar) return BarStatus.MissingUpperBar;
                is64Bit = true;
            } else {
                return BarStatus.UnsupportedMemoryType;
            }

            ulong address = (ulong)(low & 0xFFFFFFF0U);
            if (is64Bit) address |= (ulong)high << 32;
            if (address == 0) return BarStatus.ZeroAddress;
            if (address > MaximumIdentityMappedAddress)
                return BarStatus.AddressNotCanonical;
            if ((address & 0xFUL) != 0) return BarStatus.UnsupportedMemoryType;

            result.BaseAddress = address;
            result.Is64Bit = is64Bit;
            result.IsPrefetchable = (low & 0x8U) != 0;
            return BarStatus.Valid;
        }

        /// <summary>
        /// Validate the complete physical mapping range against the x86-64
        /// lower canonical address window used by the current four-level page
        /// tables. The BAR aperture size must come from a trusted source.
        /// </summary>
        public static bool IsPhysicalRangeMappable(ulong baseAddress,
                                                    ulong length) {
            if (baseAddress == 0 || length == 0 ||
                baseAddress > MaximumIdentityMappedAddress) return false;
            ulong remaining = MaximumIdentityMappedAddress - baseAddress;
            return length - 1 <= remaining;
        }

        public static bool IsSubrangeWithinResource(ulong resourceLength,
                                                     ulong offset,
                                                     ulong accessLength) {
            if (resourceLength == 0 || accessLength == 0 ||
                offset > resourceLength) return false;
            return accessLength <= resourceLength - offset;
        }

        public static bool TryValidateResourceContract(uint magic,
                ushort version, ushort entrySize, uint entryCount,
                uint discoveryStatus, FirmwareResourceEntry[] entries,
                out ResourceFailure failure) {
            failure = ResourceFailure.None;
            if (magic != ResourceContractMagic || entrySize != FirmwareResourceEntrySize) {
                failure = ResourceFailure.ContractMalformed;
                return false;
            }
            if (version != ResourceContractVersion) {
                failure = ResourceFailure.ContractUnsupported;
                return false;
            }
            if (entryCount > MaximumFirmwareResources || entries == null ||
                entryCount > (uint)entries.Length) {
                failure = ResourceFailure.ContractTruncated;
                return false;
            }
            if (discoveryStatus != ResourceDiscoveryComplete) {
                failure = ResourceFailure.DiscoveryIncomplete;
                return false;
            }

            for (int i = 0; i < (int)entryCount; i++) {
                FirmwareResourceEntry current = entries[i];
                for (int j = 0; j < i; j++) {
                    FirmwareResourceEntry previous = entries[j];
                    if (current.Segment == previous.Segment &&
                        current.Bus == previous.Bus &&
                        current.Device == previous.Device &&
                        current.Function == previous.Function &&
                        current.BarIndex == previous.BarIndex) {
                        failure = ResourceFailure.DuplicateController;
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Check firmware provenance against the kernel's independently read
        /// PCI identity and BAR0 configuration. The supported translation is
        /// deliberately identity-only; other host/PCI address translations
        /// are reported and rejected.
        /// </summary>
        public static bool TryValidateFirmwareResource(
                FirmwareResourceEntry entry,
                ushort expectedSegment, byte expectedBus, byte expectedDevice,
                byte expectedFunction, ushort expectedVendorId,
                ushort expectedDeviceId, byte classCode, byte subclass,
                byte programmingInterface, uint bar0Low, uint bar0High,
                out ResourceFailure failure) {
            failure = ResourceFailure.None;
            if (entry.Segment != expectedSegment || entry.Bus != expectedBus ||
                entry.Device != expectedDevice || entry.Function != expectedFunction ||
                entry.VendorId != expectedVendorId || entry.DeviceId != expectedDeviceId ||
                !IsXhciFunction(classCode, subclass, programmingInterface)) {
                failure = ResourceFailure.ControllerMismatch;
                return false;
            }
            if (entry.Device > 31 || entry.Function > 7) {
                failure = ResourceFailure.InvalidBdf;
                return false;
            }
            if (entry.BarIndex != 0) {
                failure = ResourceFailure.InvalidBarIndex;
                return false;
            }
            if (entry.ResourceType != 0) {
                failure = ResourceFailure.InvalidResourceType;
                return false;
            }
            if (entry.AddressWidth != 32 && entry.AddressWidth != 64) {
                failure = ResourceFailure.InvalidAddressWidth;
                return false;
            }

            // Accept only ordinary, static, non-prefetchable memory resources.
            // The kernel maps this range UC regardless of any firmware cache
            // hint, but cacheable, write-combining, translated, or reserved
            // address-space descriptors are outside this contract.
            if ((entry.SpecificFlags & 0xF8) != 0 ||
                (entry.SpecificFlags & 0x06) != 0 ||
                (entry.GeneralFlags & 0xF0) != 0 || entry.Reserved != 0) {
                failure = ResourceFailure.InvalidMemoryAttributes;
                return false;
            }
            if (entry.ApertureLength == 0) {
                failure = ResourceFailure.ZeroLength;
                return false;
            }
            if (entry.MmioBase == 0 ||
                entry.MmioBase > 0xFFFFFFFFFFFFFFFFUL - (entry.ApertureLength - 1)) {
                failure = ResourceFailure.RangeOverflow;
                return false;
            }
            ulong computedMaximum = entry.MmioBase + entry.ApertureLength - 1;
            if (entry.ResourceMaximum != computedMaximum) {
                failure = ResourceFailure.MissingAperture;
                return false;
            }
            if (entry.MmioBase < 0x100000UL ||
                (entry.AddressWidth == 32 && entry.ResourceMaximum > 0xFFFFFFFFUL)) {
                failure = ResourceFailure.InvalidAddressWidth;
                return false;
            }
            if (entry.TranslationOffset != 0) {
                failure = ResourceFailure.UnsupportedTranslation;
                return false;
            }
            if (entry.BarConfigurationBase != entry.MmioBase) {
                failure = ResourceFailure.BarMismatch;
                return false;
            }

            BarStatus barStatus = TryParseMemoryBar(bar0Low, bar0High,
                hasUpperBar: true, out BarInfo bar);
            if (barStatus != BarStatus.Valid || bar.IsPrefetchable ||
                bar.Is64Bit != (entry.AddressWidth == 64) ||
                bar.BaseAddress != entry.MmioBase ||
                !IsPhysicalRangeMappable(entry.MmioBase, entry.ApertureLength)) {
                failure = ResourceFailure.InvalidBar;
                return false;
            }
            return true;
        }

        /// <summary>Build the page-aligned physical span needed for one bounded access.</summary>
        public static bool TryBuildMmioMappingPlan(ulong resourceBase,
                ulong resourceLength, ulong accessOffset, ulong accessLength,
                ulong pageSize, out MmioMappingPlan plan) {
            plan = new MmioMappingPlan();
            if (pageSize == 0 || (pageSize & (pageSize - 1)) != 0 ||
                !IsPhysicalRangeMappable(resourceBase, resourceLength) ||
                !IsSubrangeWithinResource(resourceLength, accessOffset, accessLength) ||
                resourceBase > MaximumIdentityMappedAddress - accessOffset) return false;

            ulong accessBase = resourceBase + accessOffset;
            if (accessBase > MaximumIdentityMappedAddress - (accessLength - 1)) return false;
            ulong lastByte = accessBase + accessLength - 1;
            ulong firstPage = accessBase & ~(pageSize - 1);
            ulong lastPage = lastByte & ~(pageSize - 1);
            ulong pageCount = ((lastPage - firstPage) / pageSize) + 1;
            plan.FirstPhysicalPage = firstPage;
            plan.PageCount = pageCount;
            plan.FirstPageOffset = accessBase - firstPage;
            plan.AccessLength = accessLength;
            return true;
        }

        /// <summary>Return a 4 KiB PTE selector for an exact UC PAT entry.</summary>
        public static bool TryBuildUncachedPteFlags(ulong patValue, out ulong flags) {
            const ulong present = 1UL;
            const ulong noExecute = 1UL << 63;
            const ulong writeThrough = 1UL << 3;
            const ulong cacheDisable = 1UL << 4;
            const ulong pat = 1UL << 7;
            flags = 0;
            for (int index = 0; index < 8; index++) {
                if (((patValue >> (index * 8)) & 0xFFUL) != 0) continue;
                flags = present | noExecute |
                    ((index & 1) != 0 ? writeThrough : 0) |
                    ((index & 2) != 0 ? cacheDisable : 0) |
                    ((index & 4) != 0 ? pat : 0);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Read and validate only the fixed capability header. A caller must
        /// supply a trusted resource length and a read-only access object.
        /// This method never writes to MMIO.
        /// </summary>
        public static bool TryReadCapabilities(IReadOnlyMmioReader reader,
                                                ulong resourceLength,
                                                out Capabilities capabilities,
                                                out CapabilityFailure failure) {
            capabilities = new Capabilities();
            failure = CapabilityFailure.None;
            if (reader == null) {
                failure = CapabilityFailure.AccessUnavailable;
                return false;
            }
            if (!reader.IsMapped) {
                failure = CapabilityFailure.AccessUnavailable;
                return false;
            }
            if (!IsSubrangeWithinResource(resourceLength, 0,
                                          CapabilityHeaderLength)) {
                failure = CapabilityFailure.ResourceTooSmall;
                return false;
            }

            // CAPLENGTH/HCIVERSION through RTSOFF occupy seven dwords at
            // offsets 0x00 through 0x18. Do not read HCCPARAMS2 at 0x1C.
            uint[] registers = new uint[7];
            for (int i = 0; i < registers.Length; i++) {
                if (!reader.TryRead32((ulong)i * 4UL, out registers[i])) {
                    failure = CapabilityFailure.ReadFailed;
                    return false;
                }
            }
            return TryDecodeCapabilities(registers, resourceLength,
                                         out capabilities, out failure);
        }

        /// <summary>Decode seven synthetic or previously read capability words.</summary>
        public static bool TryDecodeCapabilities(uint[] registers,
                                                  ulong resourceLength,
                                                  out Capabilities capabilities,
                                                  out CapabilityFailure failure) {
            capabilities = new Capabilities();
            failure = CapabilityFailure.None;
            if (registers == null || registers.Length < 7) {
                failure = CapabilityFailure.RegisterBlockMalformed;
                return false;
            }
            if (!IsSubrangeWithinResource(resourceLength, 0,
                                          CapabilityHeaderLength)) {
                failure = CapabilityFailure.ResourceTooSmall;
                return false;
            }

            byte capabilityLength = (byte)(registers[0] & 0xFFU);
            ushort version = (ushort)(registers[0] >> 16);
            if (capabilityLength < CapabilityHeaderLength ||
                (capabilityLength & 0x3) != 0 ||
                !IsSubrangeWithinResource(resourceLength, capabilityLength, 4)) {
                failure = CapabilityFailure.InvalidCapabilityLength;
                return false;
            }
            if (version == 0) {
                failure = CapabilityFailure.InvalidControllerVersion;
                return false;
            }

            uint hcs1 = registers[1];
            byte maximumSlots = (byte)(hcs1 & 0xFFU);
            ushort maximumInterrupters = (ushort)((hcs1 >> 8) & 0x7FFU);
            byte rootPorts = (byte)(hcs1 >> 24);
            if (maximumSlots == 0 || maximumInterrupters == 0 || rootPorts == 0) {
                failure = CapabilityFailure.InvalidControllerLimits;
                return false;
            }

            uint rawDoorbellOffset = registers[5];
            uint rawRuntimeOffset = registers[6];
            if ((rawDoorbellOffset & 0x3U) != 0) {
                failure = CapabilityFailure.InvalidDoorbellOffset;
                return false;
            }
            if ((rawRuntimeOffset & 0x1FU) != 0) {
                failure = CapabilityFailure.InvalidRuntimeOffset;
                return false;
            }

            uint doorbellOffset = rawDoorbellOffset & 0xFFFFFFFCU;
            uint runtimeOffset = rawRuntimeOffset & 0xFFFFFFE0U;
            if (doorbellOffset < capabilityLength) {
                failure = CapabilityFailure.InvalidDoorbellOffset;
                return false;
            }
            if (runtimeOffset < capabilityLength) {
                failure = CapabilityFailure.InvalidRuntimeOffset;
                return false;
            }

            ulong doorbellCount = (ulong)maximumSlots + 1UL;
            ulong doorbellBytes = doorbellCount * 4UL;
            ulong runtimeBytes = 0x20UL + (ulong)maximumInterrupters * 0x20UL;
            if (!IsSubrangeWithinResource(resourceLength, doorbellOffset,
                                          doorbellBytes) ||
                !IsSubrangeWithinResource(resourceLength, runtimeOffset,
                                          runtimeBytes)) {
                failure = CapabilityFailure.RegisterRangeOutOfBounds;
                return false;
            }

            uint hcs2 = registers[2];
            uint hcc1 = registers[4];
            capabilities.CapabilityLength = capabilityLength;
            capabilities.ControllerVersion = version;
            capabilities.MaximumDeviceSlots = maximumSlots;
            capabilities.AdvertisedInterruptResources = maximumInterrupters;
            capabilities.RootPortCount = rootPorts;
            capabilities.HcsParams2 = hcs2;
            capabilities.HcsParams3 = registers[3];
            capabilities.HccParams1 = hcc1;
            capabilities.ScratchpadBufferCount = (ushort)(
                (((hcs2 >> 27) & 0x1FU) << 5) |
                ((hcs2 >> 21) & 0x1FU));
            capabilities.Uses64ByteContexts = (hcc1 & (1U << 2)) != 0;
            capabilities.Supports64BitAddresses = (hcc1 & 1U) != 0;
            capabilities.DoorbellOffset = doorbellOffset;
            capabilities.RuntimeRegisterOffset = runtimeOffset;
            capabilities.U1DeviceExitLatency = (byte)(registers[3] & 0xFFU);
            capabilities.U2DeviceExitLatency = (ushort)(registers[3] >> 16);
            return true;
        }
    }
}
