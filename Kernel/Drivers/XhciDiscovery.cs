using System;

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
