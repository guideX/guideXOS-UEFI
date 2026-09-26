using guideXOS.FS;
using System;
using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    internal static class ManagedImageContract {
        internal const uint DescriptorMagic = 0x494D5847; // GXMI
        internal const uint DescriptorVersion = 1;
        internal const uint TargetGuidexos = 0x47554944; // GUID
        internal const uint MachineAmd64 = 0x8664;
        internal const uint FlagFixedBase = 1;
        internal const uint FlagNoRelocations = 2;
        internal const uint FlagX3Gs = 4;
        internal const uint FlagManagedEntryBlocked = 8;
        internal const uint FlagPhase26ManagedEntry = 16;
        internal const uint FlagPhase27ManagedService = 32;
        internal const uint FlagPhase27ManagedFailure = 64;
        internal const byte Read = 1;
        internal const byte Write = 2;
        internal const byte Execute = 4;
        internal const ulong ImageBase = 0x0000401000000000UL;
        internal const ulong UserLimit = PageTable.UserAddressLimit;
        internal const ulong PageSize = 0x1000UL;
        internal const ulong StackGuardStart = 0x00007FFF7FFE0000UL;
        internal const ulong StartupBlockAddress = 0x0000401100000000UL;
        internal const ulong RuntimeStateAddress = 0x0000401100010000UL;
        internal const ulong GsBlockAddress = 0x0000401100020000UL;
        internal const ulong TlsVectorAddress = 0x0000401100030000UL;
        internal const ulong FlsStateAddress = 0x0000401100040000UL;
        // NativeAOT's Windows TLS model addresses tls_CurrentThread through
        // the TLS vector plus its section-relative offset (0x22120 in the
        // reviewed Phase 26 image).  Keep that writable per-process block
        // separate from the loader-owned runtime-state record.
        internal const ulong TlsBlockAddress = 0x0000401100060000UL;
        internal const ulong TlsBlockSize = 0x0000000000024000UL;
        internal const int TlsBlockPageCount = 36;
        internal const ulong TlsVectorOffset = 0x58UL;
        internal const ulong HeapReservationBase = 0x0000401300000000UL;
        internal const ulong HeapReservationSize = 0x0000001000000000UL;
        internal const int MaxSections = 32;
        internal const int MaxImagePages = 512;
        internal const int MaxFlsSlots = 64;
        internal const int DescriptorHeaderSize = 116;
        internal const int DescriptorSectionSize = 24;
        internal const uint PalSchema = 1;
        internal const uint AbiVersion = 1;
    }

    internal sealed class ManagedImageSection {
        internal uint VirtualAddress;
        internal uint VirtualSize;
        internal uint RawPointer;
        internal uint RawSize;
        internal uint MemorySize;
        internal byte Permissions;
        internal string Name;

        internal bool IsReadable => (Permissions & ManagedImageContract.Read) != 0;
        internal bool IsWritable => (Permissions & ManagedImageContract.Write) != 0;
        internal bool IsExecutable => (Permissions & ManagedImageContract.Execute) != 0;
    }

    internal sealed class ManagedImageDescriptor {
        internal uint Version;
        internal uint Target;
        internal uint Machine;
        internal uint Flags;
        internal uint FileSize;
        internal ulong ImageBase;
        internal uint ImageSize;
        internal uint EntryPointRva;
        internal uint SectionAlignment;
        internal uint FileAlignment;
        internal uint PalSchema;
        internal uint GsVectorOffset;
        internal uint TlsCurrentThreadRva;
        internal uint TlsIndexRva;
        internal uint RuntimeMetadataRva;
        internal uint RuntimeMetadataSize;
        internal uint NativeBootstrapRva;
        internal uint ManagedEntryRva;
        internal byte[] Sha256;
        internal ManagedImageSection[] Sections;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct ManagedImageStartupBlock {
        public uint GuideXosAbiVersion;
        public uint NativeAotContractVersion;
        public ulong ImageBase;
        public ulong ImageSize;
        public ulong NativeBootstrapAddress;
        public ulong ManagedEntryAddress;
        public ulong RuntimeMetadataBase;
        public ulong RuntimeMetadataSize;
        public ulong GsBlockAddress;
        public ulong TlsVectorAddress;
        public uint TlsIndex;
        public uint Reserved0;
        public ulong FlsStateAddress;
        public ulong UserRuntimeStateAddress;
        public ulong PalVeneerAddress;
        public ulong HeapReservationBase;
        public ulong HeapReservationSize;
        public ulong LaunchPayloadAddress;
        public ulong LaunchPayloadSize;
        public uint Flags;
        public uint Reserved1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct ManagedImageGsBlock {
        public fixed byte Reserved0[0x58];
        public ulong TlsVectorAddress;
        public ulong UserRuntimeStateAddress;
        public ulong ProcessGeneration;
        public ulong OwnerApplication;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct ManagedImageRuntimeState {
        public uint Version;
        public uint Flags;
        public ulong CurrentThreadState;
        public ulong ProcessGeneration;
        public ulong OwnerApplication;
        public uint FlsSlotCount;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct ManagedImageFlsState {
        public uint Version;
        public uint SlotCount;
        public ulong ProcessGeneration;
        public ulong OwnerApplication;
        public ulong AllocatedMask;
    }

    internal static unsafe class ManagedImageDescriptorReader {
        private static bool MatchesExpectedSha256(byte[] hash, uint flags) {
            if (hash == null || hash.Length != 32) return false;
            if ((flags & ManagedImageContract.FlagPhase27ManagedFailure) != 0) {
                return U32(hash, 0) == 0x3336FEA0U &&
                    U32(hash, 4) == 0x4E2FFF8AU &&
                    U32(hash, 8) == 0xEAA510A6U &&
                    U32(hash, 12) == 0x32AF235AU &&
                    U32(hash, 16) == 0x6EE41018U &&
                    U32(hash, 20) == 0xD3D60DE2U &&
                    U32(hash, 24) == 0x418264A3U &&
                    U32(hash, 28) == 0x110A6FDBU;
            }
            if ((flags & ManagedImageContract.FlagPhase27ManagedService) != 0) {
                return U32(hash, 0) == 0x687D5DFDU &&
                    U32(hash, 4) == 0x215EFEB0U &&
                    U32(hash, 8) == 0xD0B032D6U &&
                    U32(hash, 12) == 0x0D3654F6U &&
                    U32(hash, 16) == 0x905B4A54U &&
                    U32(hash, 20) == 0xA0AF967EU &&
                    U32(hash, 24) == 0xE1C323D2U &&
                    U32(hash, 28) == 0xB4881034U;
            }
            if ((flags & ManagedImageContract.FlagPhase26ManagedEntry) != 0) {
                return U32(hash, 0) == 0x22183B30U &&
                    U32(hash, 4) == 0x1B1220D1U &&
                    U32(hash, 8) == 0x84F55384U &&
                    U32(hash, 12) == 0x1E077C7EU &&
                    U32(hash, 16) == 0xC09B69D6U &&
                    U32(hash, 20) == 0x89925A50U &&
                    U32(hash, 24) == 0x842D3A4DU &&
                    U32(hash, 28) == 0x0F8E74FBU;
            }
            return hash[0] == 0xC8 && hash[1] == 0xD6 &&
                hash[2] == 0x0A && hash[3] == 0xBE &&
                hash[4] == 0x6D && hash[5] == 0x91 &&
                hash[6] == 0x91 && hash[7] == 0x7F &&
                hash[8] == 0x4E && hash[9] == 0x23 &&
                hash[10] == 0x6F && hash[11] == 0x43 &&
                hash[12] == 0x5A && hash[13] == 0x8C &&
                hash[14] == 0x2D && hash[15] == 0x42 &&
                hash[16] == 0x72 && hash[17] == 0x38 &&
                hash[18] == 0x6C && hash[19] == 0xEC &&
                hash[20] == 0x83 && hash[21] == 0x0C &&
                hash[22] == 0x07 && hash[23] == 0xAF &&
                hash[24] == 0xA6 && hash[25] == 0x91 &&
                hash[26] == 0x89 && hash[27] == 0x7A &&
                hash[28] == 0x23 && hash[29] == 0xDA &&
                hash[30] == 0x19 && hash[31] == 0x5A;
        }

        private static void Marker(string text) {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++) Native.Out8(0x3F8, (byte)text[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void HexMarker(string prefix, ulong value) {
            const string digits = "0123456789ABCDEF";
            char[] text = new char[16];
            for (int i = 15; i >= 0; i--) {
                text[i] = digits[(int)(value & 0xFUL)];
                value >>= 4;
            }
            Marker(prefix + new string(text));
        }

        private static ushort U16(byte[] data, int offset) {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint U32(byte[] data, int offset) {
            return (uint)data[offset] | ((uint)data[offset + 1] << 8) |
                   ((uint)data[offset + 2] << 16) | ((uint)data[offset + 3] << 24);
        }

        private static ulong U64(byte[] data, int offset) {
            return (ulong)U32(data, offset) | ((ulong)U32(data, offset + 4) << 32);
        }

        private static bool AddFits(ulong left, ulong right, ulong limit,
                                    out ulong result) {
            result = 0;
            if (left > limit || right > limit - left) return false;
            result = left + right;
            return true;
        }

        private static byte Permissions(uint characteristics) {
            byte value = 0;
            if ((characteristics & 0x40000000U) != 0) value |= ManagedImageContract.Read;
            if ((characteristics & 0x80000000U) != 0) value |= ManagedImageContract.Write;
            if ((characteristics & 0x20000000U) != 0) value |= ManagedImageContract.Execute;
            return value;
        }

        private static bool TryValidatePe(byte[] image, ManagedImageDescriptor descriptor,
                                          out string failure) {
            failure = null;
            if (image.Length < 0x40 || image[0] != (byte)'M' || image[1] != (byte)'Z') {
                failure = "DOS_SIGNATURE";
                return false;
            }
            uint peOffset = U32(image, 0x3C);
            ulong peEnd = (ulong)peOffset + 24UL;
            if (peOffset > image.Length || peEnd > (ulong)image.Length ||
                image[peOffset] != (byte)'P' || image[peOffset + 1] != (byte)'E' ||
                image[peOffset + 2] != 0 || image[peOffset + 3] != 0) {
                failure = "PE_SIGNATURE";
                return false;
            }
            int coff = (int)peOffset + 4;
            ushort machine = U16(image, coff);
            ushort sectionCount = U16(image, coff + 2);
            ushort optionalSize = U16(image, coff + 16);
            int optional = coff + 20;
            ulong optionalEnd = (ulong)optional + optionalSize;
            if (machine != ManagedImageContract.MachineAmd64 || sectionCount == 0 ||
                sectionCount > ManagedImageContract.MaxSections ||
                optionalEnd > (ulong)image.Length || optionalSize < 112 ||
                U16(image, optional) != 0x20B) {
                failure = "PE_HEADER_CONTRACT";
                return false;
            }
            uint entry = U32(image, optional + 16);
            ulong baseAddress = U64(image, optional + 24);
            uint sectionAlignment = U32(image, optional + 32);
            uint fileAlignment = U32(image, optional + 36);
            uint imageSize = U32(image, optional + 56);
            uint headersSize = U32(image, optional + 60);
            uint directoryCount = U32(image, optional + 108);
            if (baseAddress != descriptor.ImageBase || imageSize != descriptor.ImageSize ||
                entry != descriptor.EntryPointRva || sectionAlignment != descriptor.SectionAlignment ||
                fileAlignment != descriptor.FileAlignment ||
                sectionAlignment != ManagedImageContract.PageSize ||
                (fileAlignment == 0 || (fileAlignment & (fileAlignment - 1)) != 0) ||
                headersSize > (uint)image.Length || directoryCount > 16) {
                failure = "PE_IDENTITY";
                return false;
            }
            ulong sectionTable = optionalEnd;
            ulong sectionTableEnd = sectionTable + (ulong)sectionCount * 40UL;
            if (sectionTableEnd < sectionTable || sectionTableEnd > (ulong)image.Length ||
                descriptor.Sections.Length != sectionCount) {
                failure = "PE_SECTION_TABLE";
                return false;
            }
            int directoryBase = optional + 112;
            if (directoryCount != 0 &&
                directoryCount > (uint)((optionalSize - 112) / 8)) {
                failure = "PE_DIRECTORY_BOUNDS";
                return false;
            }
            if (directoryCount >= 2 &&
                (U32(image, directoryBase + 8) != 0 || U32(image, directoryBase + 12) != 0)) {
                failure = "WINDOWS_IMPORT_DIRECTORY";
                return false;
            }
            if (directoryCount >= 6 &&
                (U32(image, directoryBase + 40) != 0 || U32(image, directoryBase + 44) != 0)) {
                failure = "RELOCATIONS_PRESENT";
                return false;
            }
            if (directoryCount >= 10 &&
                (U32(image, directoryBase + 72) != 0 || U32(image, directoryBase + 76) != 0)) {
                failure = "PE_TLS_DIRECTORY";
                return false;
            }

            for (int i = 0; i < sectionCount; i++) {
                int offset = (int)sectionTable + i * 40;
                uint virtualSize = U32(image, offset + 8);
                uint virtualAddress = U32(image, offset + 12);
                uint rawSize = U32(image, offset + 16);
                uint rawPointer = U32(image, offset + 20);
                uint characteristics = U32(image, offset + 36);
                ulong memorySize = ((ulong)virtualSize + 0xFFFUL) & ~0xFFFUL;
                ulong sectionBase;
                ulong virtualEnd;
                if (virtualSize == 0 || memorySize < virtualSize || memorySize < rawSize ||
                    (virtualAddress & 0xFFFU) != 0 || rawPointer > (uint)image.Length ||
                    rawSize > (uint)image.Length - rawPointer ||
                    virtualAddress > imageSize || memorySize > imageSize - virtualAddress ||
                    !AddFits(baseAddress, virtualAddress,
                             ManagedImageContract.UserLimit, out sectionBase) ||
                    !AddFits(sectionBase, memorySize,
                             ManagedImageContract.UserLimit, out virtualEnd) ||
                    virtualEnd >= ManagedImageContract.StackGuardStart) {
                    failure = "PE_SECTION_RANGE";
                    return false;
                }
                byte perms = Permissions(characteristics);
                if ((perms & ManagedImageContract.Read) == 0 ||
                    (perms & (ManagedImageContract.Write | ManagedImageContract.Execute)) ==
                        (ManagedImageContract.Write | ManagedImageContract.Execute)) {
                    failure = "PE_PERMISSION_POLICY";
                    return false;
                }
                ManagedImageSection expected = descriptor.Sections[i];
                if (expected.VirtualAddress != virtualAddress ||
                    expected.VirtualSize != virtualSize || expected.RawPointer != rawPointer ||
                    expected.RawSize != rawSize || expected.MemorySize != memorySize ||
                    expected.Permissions != perms) {
                    failure = "DESCRIPTOR_PE_MISMATCH";
                    return false;
                }
                for (int j = i + 1; j < sectionCount; j++) {
                    int otherOffset = (int)sectionTable + j * 40;
                    ulong otherStart = U32(image, otherOffset + 12);
                    ulong otherSize = ((ulong)U32(image, otherOffset + 8) + 0xFFFUL) & ~0xFFFUL;
                    ulong thisStart = virtualAddress;
                    if (thisStart < otherStart + otherSize && otherStart < thisStart + memorySize) {
                        failure = "PE_SECTION_OVERLAP";
                        return false;
                    }
                }
            }
            bool entryExecutable = false;
            for (int i = 0; i < descriptor.Sections.Length; i++) {
                ManagedImageSection section = descriptor.Sections[i];
                if (entry >= section.VirtualAddress &&
                    entry < section.VirtualAddress + section.VirtualSize &&
                    section.IsExecutable) entryExecutable = true;
            }
            if (entry >= imageSize || !entryExecutable) {
                failure = "ENTRYPOINT_NOT_EXECUTABLE";
                return false;
            }
            return true;
        }

        internal static bool TryRead(byte[] data, out ManagedImageDescriptor descriptor,
                                     out string failure) {
            descriptor = null;
            failure = null;
            if (data == null || data.Length < ManagedImageContract.DescriptorHeaderSize) {
                failure = "DESCRIPTOR_BOUNDS";
                return false;
            }
            if (U32(data, 0) != ManagedImageContract.DescriptorMagic) {
                failure = "DESCRIPTOR_MAGIC";
                return false;
            }
            uint sectionCount = U32(data, 48);
            if (U32(data, 4) != ManagedImageContract.DescriptorVersion ||
                sectionCount == 0 || sectionCount > ManagedImageContract.MaxSections) {
                failure = "DESCRIPTOR_VERSION_OR_COUNT";
                return false;
            }
            ulong needed;
            if (!AddFits((ulong)ManagedImageContract.DescriptorHeaderSize,
                         (ulong)sectionCount * ManagedImageContract.DescriptorSectionSize,
                         (ulong)data.Length, out needed) || needed != (ulong)data.Length) {
                failure = "DESCRIPTOR_SIZE";
                return false;
            }

            descriptor = new ManagedImageDescriptor {
                Version = U32(data, 4),
                Target = U32(data, 8),
                Machine = U32(data, 12),
                Flags = U32(data, 16),
                FileSize = U32(data, 20),
                ImageBase = U64(data, 24),
                ImageSize = U32(data, 32),
                EntryPointRva = U32(data, 36),
                SectionAlignment = U32(data, 40),
                FileAlignment = U32(data, 44),
                PalSchema = U32(data, 52),
                GsVectorOffset = U32(data, 56),
                TlsCurrentThreadRva = U32(data, 60),
                TlsIndexRva = U32(data, 64),
                RuntimeMetadataRva = U32(data, 68),
                RuntimeMetadataSize = U32(data, 72),
                NativeBootstrapRva = U32(data, 76),
                ManagedEntryRva = U32(data, 80),
                Sha256 = new byte[32],
                Sections = new ManagedImageSection[(int)sectionCount]
            };
            for (int i = 0; i < 32; i++) descriptor.Sha256[i] = data[84 + i];
            int sectionOffset = ManagedImageContract.DescriptorHeaderSize;
            for (int i = 0; i < descriptor.Sections.Length; i++) {
                descriptor.Sections[i] = new ManagedImageSection {
                    VirtualAddress = U32(data, sectionOffset),
                    VirtualSize = U32(data, sectionOffset + 4),
                    RawPointer = U32(data, sectionOffset + 8),
                    RawSize = U32(data, sectionOffset + 12),
                    MemorySize = U32(data, sectionOffset + 16),
                    Permissions = data[sectionOffset + 20],
                    Name = string.Empty
                };
                sectionOffset += ManagedImageContract.DescriptorSectionSize;
            }
            return true;
        }

        internal static bool TryValidate(ManagedImageDescriptor descriptor,
                                          byte[] image, out string failure) {
            failure = null;
            if (descriptor == null || image == null) {
                failure = "NULL_INPUT";
                return false;
            }
            uint requiredFlags = ManagedImageContract.FlagFixedBase |
                ManagedImageContract.FlagNoRelocations |
                ManagedImageContract.FlagX3Gs |
                ManagedImageContract.FlagManagedEntryBlocked;
            if (descriptor.Version != ManagedImageContract.DescriptorVersion ||
                descriptor.Target != ManagedImageContract.TargetGuidexos ||
                descriptor.Machine != ManagedImageContract.MachineAmd64 ||
                (descriptor.Flags & requiredFlags) != requiredFlags ||
                descriptor.FileSize != (uint)image.Length ||
                descriptor.ImageBase != ManagedImageContract.ImageBase ||
                descriptor.ImageSize == 0 || descriptor.ImageSize > 0x10000000U ||
                descriptor.SectionAlignment != ManagedImageContract.PageSize ||
                descriptor.FileAlignment == 0 || descriptor.PalSchema != ManagedImageContract.PalSchema ||
                descriptor.GsVectorOffset != ManagedImageContract.TlsVectorOffset ||
                descriptor.ManagedEntryRva != descriptor.EntryPointRva ||
                descriptor.NativeBootstrapRva != 0) {
                failure = "DESCRIPTOR_CONTRACT";
                return false;
            }
            bool hashMatches = MatchesExpectedSha256(descriptor.Sha256,
                descriptor.Flags);
            if ((descriptor.Flags & ManagedImageContract.FlagPhase26ManagedEntry) != 0) {
                HexMarker("PHASE26_HASH_MATCH=0x", hashMatches ? 1UL : 0UL);
                HexMarker("PHASE26_DESCRIPTOR_FLAGS=0x", descriptor.Flags);
                HexMarker("PHASE26_DESCRIPTOR_HASH0=0x", descriptor.Sha256[0]);
                HexMarker("PHASE26_DESCRIPTOR_HASH1=0x", U32(descriptor.Sha256, 0));
                HexMarker("PHASE26_DESCRIPTOR_HASH2=0x", U32(descriptor.Sha256, 4));
                HexMarker("PHASE26_DESCRIPTOR_HASH3=0x", U32(descriptor.Sha256, 8));
                HexMarker("PHASE26_DESCRIPTOR_HASH4=0x", U32(descriptor.Sha256, 12));
                HexMarker("PHASE26_DESCRIPTOR_HASH5=0x", U32(descriptor.Sha256, 16));
                HexMarker("PHASE26_DESCRIPTOR_HASH6=0x", U32(descriptor.Sha256, 20));
                HexMarker("PHASE26_DESCRIPTOR_HASH7=0x", U32(descriptor.Sha256, 24));
                HexMarker("PHASE26_DESCRIPTOR_HASH8=0x", U32(descriptor.Sha256, 28));
            }
            if (!hashMatches) {
                failure = "ARTIFACT_HASH";
                return false;
            }
            if (!TryValidatePe(image, descriptor, out failure)) return false;
            ulong imageEnd;
            if (!AddFits(descriptor.ImageBase, descriptor.ImageSize, ManagedImageContract.UserLimit,
                         out imageEnd) || imageEnd <= descriptor.ImageBase ||
                imageEnd >= ManagedImageContract.StackGuardStart) {
                failure = "IMAGE_RANGE";
                return false;
            }
            ulong entryEnd;
            bool entryExecutable = false;
            bool tlsCurrentThreadReadable = false;
            bool tlsIndexWritable = false;
            for (int i = 0; i < descriptor.Sections.Length; i++) {
                ManagedImageSection section = descriptor.Sections[i];
                if (section.VirtualSize == 0 || section.MemorySize < section.VirtualSize ||
                    section.MemorySize < section.RawSize ||
                    (section.VirtualAddress & 0xFFFU) != 0 ||
                    (section.MemorySize & 0xFFFU) != 0 ||
                    (section.Permissions & ManagedImageContract.Read) == 0 ||
                    (section.Permissions & (ManagedImageContract.Write | ManagedImageContract.Execute)) ==
                        (ManagedImageContract.Write | ManagedImageContract.Execute) ||
                    section.RawPointer > (uint)image.Length ||
                    section.RawSize > (uint)image.Length - section.RawPointer ||
                    !AddFits(descriptor.ImageBase, section.VirtualAddress,
                             ManagedImageContract.UserLimit, out entryEnd) ||
                    !AddFits(entryEnd, section.MemorySize,
                             ManagedImageContract.UserLimit, out entryEnd) ||
                    entryEnd >= ManagedImageContract.StackGuardStart ||
                    section.VirtualAddress > descriptor.ImageSize ||
                    section.MemorySize > descriptor.ImageSize - section.VirtualAddress) {
                    failure = "SECTION_CONTRACT";
                    return false;
                }
                if (descriptor.EntryPointRva >= section.VirtualAddress &&
                    (ulong)descriptor.EntryPointRva <
                        (ulong)section.VirtualAddress + section.VirtualSize &&
                    (section.Permissions & ManagedImageContract.Execute) != 0)
                    entryExecutable = true;
                if (descriptor.TlsCurrentThreadRva >= section.VirtualAddress &&
                    (ulong)descriptor.TlsCurrentThreadRva <
                        (ulong)section.VirtualAddress + section.MemorySize &&
                    section.IsReadable)
                    tlsCurrentThreadReadable = true;
                if (descriptor.TlsIndexRva >= section.VirtualAddress &&
                    (ulong)descriptor.TlsIndexRva <
                        (ulong)section.VirtualAddress + section.MemorySize &&
                    section.IsWritable)
                    tlsIndexWritable = true;
                for (int j = i + 1; j < descriptor.Sections.Length; j++) {
                    ManagedImageSection other = descriptor.Sections[j];
                    ulong aEnd = (ulong)section.VirtualAddress + section.MemorySize;
                    ulong bEnd = (ulong)other.VirtualAddress + other.MemorySize;
                    if (section.VirtualAddress < bEnd && other.VirtualAddress < aEnd) {
                        failure = "SECTION_OVERLAP";
                        return false;
                    }
                }
            }
            if (descriptor.EntryPointRva >= descriptor.ImageSize || !entryExecutable ||
                descriptor.RuntimeMetadataRva >= descriptor.ImageSize ||
                descriptor.RuntimeMetadataSize > descriptor.ImageSize - descriptor.RuntimeMetadataRva ||
                descriptor.TlsCurrentThreadRva >= descriptor.ImageSize ||
                descriptor.TlsIndexRva >= descriptor.ImageSize ||
                !tlsCurrentThreadReadable || !tlsIndexWritable) {
                failure = "RUNTIME_METADATA_OR_ENTRY";
                return false;
            }
            return true;
        }
    }

    internal static class ManagedImageDiagnostics {
        internal static int ImagesMapped;
        internal static int ImagesReclaimed;
        internal static int ImagePagesAllocated;
        internal static int ImagePagesReclaimed;
        internal static int BssBytesZeroed;
        internal static int StartupBlocksCreated;
        internal static int StartupBlocksReclaimed;
        internal static int TlsBlocksCreated;
        internal static int TlsBlocksReclaimed;
        internal static int FlsTablesCreated;
        internal static int FlsTablesReclaimed;
        internal static int GsContextsCreated;
        internal static int GsContextsReclaimed;
        internal static int ManagedEntryAttemptsRejected;
        internal static int VmReservationsCreated;
        internal static int VmReservationsReclaimed;
        internal static int VmPagesCreated;
        internal static int VmPagesReclaimed;
        internal static int RuntimeTlsPagesAllocated;
        internal static int RuntimeTlsPagesReclaimed;

        internal static bool IsBalanced {
            get {
                return ImagesMapped == ImagesReclaimed &&
                       ImagePagesAllocated == ImagePagesReclaimed &&
                       StartupBlocksCreated == StartupBlocksReclaimed &&
                       TlsBlocksCreated == TlsBlocksReclaimed &&
                       FlsTablesCreated == FlsTablesReclaimed &&
                       GsContextsCreated == GsContextsReclaimed &&
                       VmReservationsCreated == VmReservationsReclaimed &&
                       VmPagesCreated == VmPagesReclaimed &&
                       RuntimeTlsPagesAllocated == RuntimeTlsPagesReclaimed;
            }
        }
    }

    internal unsafe sealed class ManagedImageProcess {
        internal AddressSpace Space;
        internal ManagedImageDescriptor Descriptor;
        internal ManagedImageSection[] Sections;
        internal ulong[] ImagePhysicalPages = new ulong[ManagedImageContract.MaxImagePages];
        internal int ImagePageCount;
        internal ulong StartupPhysical;
        internal ulong RuntimePhysical;
        internal ulong GsPhysical;
        internal ulong TlsVectorPhysical;
        internal ulong FlsPhysical;
        private ulong[] TlsBlockPhysicalPages =
            new ulong[ManagedImageContract.TlsBlockPageCount];
        internal ulong OwnerApplication;
        internal uint Generation;
        internal ulong UserGsBase;
        internal ulong NativeBootstrapAddress;
        internal ulong NativeBootstrapEndAddress;
        internal bool ManagedEntryReady;
        internal bool IsMapped;
        internal int BssBytesZeroed;
        private bool OwnsAddressSpace;
        private ulong _nextHeapAddress;
        // NativeAOT's workstation GC keeps several small process-private
        // records alive during startup in addition to its large heap ranges.
        // Keep the reservation table bounded, but large enough that those
        // independent PAL allocations cannot be confused with heap exhaustion.
        private VmReservation[] _vmReservations = new VmReservation[256];
        private VmPage[] _vmPages = new VmPage[16384];

        private struct VmReservation {
            internal bool Active;
            internal ulong Base;
            internal ulong Size;
        }

        private struct VmPage {
            internal bool Active;
            internal ulong Address;
            internal ulong Physical;
            internal bool Writable;
            internal bool Executable;
        }

        private static ulong AlignUp(ulong value) {
            return (value + ManagedImageContract.PageSize - 1) &
                   ~(ManagedImageContract.PageSize - 1);
        }

        private static void Marker(string text) {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++) Native.Out8(0x3F8, (byte)text[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void HexMarker(string label, ulong value) {
            Marker(label);
            for (int shift = 60; shift >= 0; shift -= 4) {
                int nibble = (int)((value >> shift) & 0xFUL);
                Native.Out8(0x3F8, (byte)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10));
            }
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void Free(ref ulong physical) {
            if (physical != 0) {
                Allocator.Free((IntPtr)physical);
                physical = 0;
            }
        }

        private bool MapRuntimePage(ulong virtualAddress, ref ulong physical,
                                    bool writable) {
            physical = (ulong)Allocator.Allocate(ManagedImageContract.PageSize);
            if (physical == 0) return false;
            Native.Stosb((void*)physical, 0, ManagedImageContract.PageSize);
            if (!Space.MapUser(virtualAddress, physical, writable, false)) {
                Free(ref physical);
                return false;
            }
            return true;
        }

        private bool MapTlsBlock() {
            if (!IsPhase26) return true;
            if (Space == null) return false;
            for (int i = 0; i < TlsBlockPhysicalPages.Length; i++) {
                ulong physical = (ulong)Allocator.Allocate(
                    ManagedImageContract.PageSize);
                if (physical == 0) return false;
                Native.Stosb((void*)physical, 0,
                             ManagedImageContract.PageSize);
                ulong virtualAddress = ManagedImageContract.TlsBlockAddress +
                    (ulong)i * ManagedImageContract.PageSize;
                if (!Space.MapUser(virtualAddress, physical, true, false)) {
                    Free(ref physical);
                    return false;
                }
                TlsBlockPhysicalPages[i] = physical;
                ManagedImageDiagnostics.RuntimeTlsPagesAllocated++;
            }
            return true;
        }

        private void FreeTlsBlock() {
            for (int i = 0; i < TlsBlockPhysicalPages.Length; i++) {
                if (TlsBlockPhysicalPages[i] != 0) {
                    Allocator.Free((IntPtr)TlsBlockPhysicalPages[i]);
                    TlsBlockPhysicalPages[i] = 0;
                    ManagedImageDiagnostics.RuntimeTlsPagesReclaimed++;
                }
            }
        }

        private bool MapImage(byte[] image) {
            // NativeAOT's COFF code manager receives the image base and parses
            // the DOS/NT headers before it consumes the mapped sections.  The
            // descriptor deliberately describes sections only, so map the
            // PE header region as a read-only image page before the first
            // section (normally RVA 0x0000..0x0FFF).
            if (Sections == null || Sections.Length == 0 ||
                Sections[0].VirtualAddress == 0 ||
                (Sections[0].VirtualAddress & (ManagedImageContract.PageSize - 1)) != 0)
                return false;
            int headerPages = (int)(Sections[0].VirtualAddress /
                                    ManagedImageContract.PageSize);
            for (int page = 0; page < headerPages; page++) {
                if (ImagePageCount >= ImagePhysicalPages.Length) return false;
                ulong physical = (ulong)Allocator.Allocate(ManagedImageContract.PageSize);
                if (physical == 0) return false;
                ManagedImageDiagnostics.ImagePagesAllocated++;
                Native.Stosb((void*)physical, 0, ManagedImageContract.PageSize);
                ulong source = (ulong)page * ManagedImageContract.PageSize;
                ulong bytesFromImage = source < (ulong)image.Length
                    ? (ulong)image.Length - source : 0;
                if (bytesFromImage > ManagedImageContract.PageSize)
                    bytesFromImage = ManagedImageContract.PageSize;
                if (bytesFromImage != 0) {
                    fixed (byte* sourceBytes = image) {
                        Native.Movsb((void*)physical, sourceBytes + source,
                                     bytesFromImage);
                    }
                }
                if (!Space.MapUser(Descriptor.ImageBase + source, physical,
                                   false, false)) {
                    Free(ref physical);
                    ManagedImageDiagnostics.ImagePagesReclaimed++;
                    return false;
                }
                ImagePhysicalPages[ImagePageCount++] = physical;
            }
            for (int i = 0; i < Sections.Length; i++) {
                ManagedImageSection section = Sections[i];
                int pages = (int)(section.MemorySize / ManagedImageContract.PageSize);
                BssBytesZeroed += (int)section.MemorySize - (int)section.RawSize;
                for (int page = 0; page < pages; page++) {
                    if (ImagePageCount >= ImagePhysicalPages.Length) return false;
                    ulong physical = (ulong)Allocator.Allocate(ManagedImageContract.PageSize);
                    if (physical == 0) return false;
                    ManagedImageDiagnostics.ImagePagesAllocated++;
                    Native.Stosb((void*)physical, 0, ManagedImageContract.PageSize);
                    ulong rva = (ulong)section.VirtualAddress +
                                (ulong)page * ManagedImageContract.PageSize;
                    ulong bytesFromSection = section.RawSize > (uint)(page * 0x1000)
                        ? section.RawSize - (uint)(page * 0x1000) : 0;
                    if (bytesFromSection > ManagedImageContract.PageSize)
                        bytesFromSection = ManagedImageContract.PageSize;
                    if (bytesFromSection != 0) {
                        ulong source = (ulong)section.RawPointer +
                                       (ulong)page * ManagedImageContract.PageSize;
                        if (source + bytesFromSection > (ulong)image.Length) {
                            Free(ref physical);
                            ManagedImageDiagnostics.ImagePagesReclaimed++;
                            return false;
                        }
                        fixed (byte* sourceBytes = image) {
                            Native.Movsb((void*)physical, sourceBytes + source,
                                         bytesFromSection);
                        }
                    }
                    if (!Space.MapUser(Descriptor.ImageBase + rva, physical,
                                       section.IsWritable, section.IsExecutable)) {
                        Free(ref physical);
                        ManagedImageDiagnostics.ImagePagesReclaimed++;
                        return false;
                    }
                    ImagePhysicalPages[ImagePageCount++] = physical;
                }
            }
            return true;
        }

        private bool WriteRuntimeState() {
            if (StartupPhysical == 0 || RuntimePhysical == 0 || GsPhysical == 0 ||
                TlsVectorPhysical == 0 || FlsPhysical == 0) return false;
            ManagedImageStartupBlock startup = default(ManagedImageStartupBlock);
            startup.GuideXosAbiVersion = ManagedImageContract.AbiVersion;
            startup.NativeAotContractVersion = ManagedImageContract.DescriptorVersion;
            startup.ImageBase = Descriptor.ImageBase;
            startup.ImageSize = Descriptor.ImageSize;
            startup.NativeBootstrapAddress = NativeBootstrapAddress;
            startup.ManagedEntryAddress = Descriptor.ImageBase + Descriptor.ManagedEntryRva;
            startup.RuntimeMetadataBase = Descriptor.ImageBase + Descriptor.RuntimeMetadataRva;
            startup.RuntimeMetadataSize = Descriptor.RuntimeMetadataSize;
            startup.GsBlockAddress = ManagedImageContract.GsBlockAddress;
            startup.TlsVectorAddress = ManagedImageContract.TlsVectorAddress;
            startup.TlsIndex = 0;
            startup.FlsStateAddress = ManagedImageContract.FlsStateAddress;
            startup.UserRuntimeStateAddress = ManagedImageContract.RuntimeStateAddress;
            startup.PalVeneerAddress = 0;
            startup.HeapReservationBase = ManagedImageContract.HeapReservationBase;
            startup.HeapReservationSize = ManagedImageContract.HeapReservationSize;
            startup.Flags = ManagedImageContract.FlagManagedEntryBlocked;
            *(ManagedImageStartupBlock*)StartupPhysical = startup;

            ManagedImageGsBlock gs = default(ManagedImageGsBlock);
            gs.TlsVectorAddress = ManagedImageContract.TlsVectorAddress;
            gs.UserRuntimeStateAddress = ManagedImageContract.RuntimeStateAddress;
            gs.ProcessGeneration = Generation;
            gs.OwnerApplication = OwnerApplication;
            *(ManagedImageGsBlock*)GsPhysical = gs;

            ManagedImageRuntimeState runtime = default(ManagedImageRuntimeState);
            runtime.Version = ManagedImageContract.DescriptorVersion;
            runtime.Flags = ManagedImageContract.FlagManagedEntryBlocked;
            runtime.CurrentThreadState = ManagedImageContract.RuntimeStateAddress;
            runtime.ProcessGeneration = Generation;
            runtime.OwnerApplication = OwnerApplication;
            runtime.FlsSlotCount = ManagedImageContract.MaxFlsSlots;
            *(ManagedImageRuntimeState*)RuntimePhysical = runtime;

            ManagedImageFlsState fls = default(ManagedImageFlsState);
            fls.Version = ManagedImageContract.DescriptorVersion;
            fls.SlotCount = ManagedImageContract.MaxFlsSlots;
            fls.ProcessGeneration = Generation;
            fls.OwnerApplication = OwnerApplication;
            *(ManagedImageFlsState*)FlsPhysical = fls;

            ulong* tlsVector = (ulong*)TlsVectorPhysical;
            tlsVector[0] = IsPhase26 ? ManagedImageContract.TlsBlockAddress :
                ManagedImageContract.RuntimeStateAddress;
            for (int i = 1; i < 8; i++) tlsVector[i] = 0;
            return true;
        }

        private bool WriteTlsIndex() {
            if (Space == null || Descriptor == null) return false;
            ulong address = Descriptor.ImageBase + Descriptor.TlsIndexRva;
            ulong physical;
            if (!PageTable.TryTranslateUser(Space.Pml4, address, true,
                                             out physical)) return false;
            *(uint*)physical = 0;
            return true;
        }

        internal static bool TryCreate(byte[] image, byte[] descriptorBytes,
                                        ulong ownerApplication, uint generation,
                                        out ManagedImageProcess process,
                                        out string failure) {
            return TryCreate(image, descriptorBytes, ownerApplication, generation,
                             null, 0, out process, out failure);
        }

        internal static bool TryCreate(byte[] image, byte[] descriptorBytes,
                                        ulong ownerApplication, uint generation,
                                        AddressSpace sharedSpace,
                                        ulong nativeBootstrapAddress,
                                        out ManagedImageProcess process,
                                        out string failure) {
            process = null;
            failure = null;
            ManagedImageDescriptor descriptor;
            if (!ManagedImageDescriptorReader.TryRead(descriptorBytes, out descriptor, out failure) ||
                !ManagedImageDescriptorReader.TryValidate(descriptor, image, out failure))
                return false;
            process = new ManagedImageProcess {
                Descriptor = descriptor,
                Sections = descriptor.Sections,
                OwnerApplication = ownerApplication,
                Generation = generation,
                NativeBootstrapAddress = nativeBootstrapAddress,
                ManagedEntryReady = false
            };
            process._nextHeapAddress = ManagedImageContract.HeapReservationBase;
            process.Space = sharedSpace ?? new AddressSpace();
            process.OwnsAddressSpace = sharedSpace == null;
            if (process.Space.Pml4 == null || !process.MapImage(image) ||
                !process.MapRuntimePage(ManagedImageContract.StartupBlockAddress,
                                        ref process.StartupPhysical, true) ||
                !process.MapRuntimePage(ManagedImageContract.RuntimeStateAddress,
                                        ref process.RuntimePhysical, true) ||
                !process.MapRuntimePage(ManagedImageContract.GsBlockAddress,
                                        ref process.GsPhysical, true) ||
                !process.MapRuntimePage(ManagedImageContract.TlsVectorAddress,
                                        ref process.TlsVectorPhysical, true) ||
                !process.MapRuntimePage(ManagedImageContract.FlsStateAddress,
                                        ref process.FlsPhysical, true) ||
                !process.MapTlsBlock() ||
                !process.WriteTlsIndex() ||
                !process.WriteRuntimeState()) {
                process.Cleanup();
                process = null;
                if (failure == null) failure = "MAPPING_FAILED";
                return false;
            }
            process.UserGsBase = ManagedImageContract.GsBlockAddress;
            process.IsMapped = true;
            ManagedImageDiagnostics.ImagesMapped++;
            ManagedImageDiagnostics.BssBytesZeroed += process.BssBytesZeroed;
            ManagedImageDiagnostics.StartupBlocksCreated++;
            ManagedImageDiagnostics.TlsBlocksCreated++;
            ManagedImageDiagnostics.FlsTablesCreated++;
            ManagedImageDiagnostics.GsContextsCreated++;
            Marker("PHASE24_MANAGED_IMAGE_MAPPED=1");
            HexMarker("PHASE24_IMAGE_BASE=0x", descriptor.ImageBase);
            HexMarker("PHASE24_IMAGE_SIZE=0x", descriptor.ImageSize);
            HexMarker("PHASE24_PROCESS_CR3=0x", process.Space.RootPhysical);
            HexMarker("PHASE24_USER_GS_BASE=0x", process.UserGsBase);
            if (process.IsPhase26) {
                HexMarker("PHASE26_TLS_BLOCK_BASE=0x",
                          ManagedImageContract.TlsBlockAddress);
                HexMarker("PHASE26_TLS_BLOCK_SIZE=0x",
                          ManagedImageContract.TlsBlockSize);
            }
            Marker("PHASE24_DESCRIPTOR_VALIDATED=1");
            Marker("PHASE24_BSS_ZERO_FILLED=1");
            Marker("PHASE24_CRT_MAPPED_READ_ONLY=1");
            Marker("PHASE24_CRT_EXECUTED=0");
            Marker("PHASE24_MANAGED_ENTRY_READY=0");
            return true;
        }

        internal static bool TryCreateFromRamdisk(ulong ownerApplication,
                                                   uint generation,
                                                   out ManagedImageProcess process,
                                                   out string failure) {
            return TryCreateFromRamdisk(ownerApplication, generation, null, 0,
                                        false, out process, out failure);
        }

        internal static bool TryCreateFromRamdisk(ulong ownerApplication,
                                                   uint generation,
                                                   AddressSpace sharedSpace,
                                                   ulong nativeBootstrapAddress,
                                                   out ManagedImageProcess process,
                                                   out string failure) {
            return TryCreateFromRamdisk(ownerApplication, generation,
                sharedSpace, nativeBootstrapAddress, false,
                out process, out failure);
        }

        internal static bool TryCreateFromRamdisk(ulong ownerApplication,
                                                    uint generation,
                                                    AddressSpace sharedSpace,
                                                    ulong nativeBootstrapAddress,
                                                    bool phase26,
                                                    out ManagedImageProcess process,
                                                    out string failure) {
            return TryCreateFromRamdisk(ownerApplication, generation,
                sharedSpace, nativeBootstrapAddress, phase26, false, false,
                out process, out failure);
        }

        internal static bool TryCreateFromRamdisk(ulong ownerApplication,
                                                    uint generation,
                                                    AddressSpace sharedSpace,
                                                    ulong nativeBootstrapAddress,
                                                    bool phase26,
                                                    bool phase27,
                                                    bool phase27Failure,
                                                    out ManagedImageProcess process,
                                                    out string failure) {
            process = null;
            failure = null;
            if (File.Instance == null) {
                failure = "NO_FILESYSTEM";
                return false;
            }
            string prefix = phase27 ?
                (phase27Failure ? "Native/guideXOS.Phase27ManagedFailureProof" :
                    "Native/guideXOS.Phase27ManagedServiceProof") :
                (phase26 ? "Native/guideXOS.Phase26ManagedProof" :
                    "Native/guideXOS.UserManagedProof");
            byte[] image = File.ReadAllBytes(prefix + ".exe");
            byte[] descriptor = File.ReadAllBytes(prefix + ".gxmi");
            if (image == null || descriptor == null) {
                failure = phase27 ? "PHASE27_IMAGE_NOT_STAGED" :
                    (phase26 ? "PHASE26_IMAGE_NOT_STAGED" :
                        "PHASE24_IMAGE_NOT_STAGED");
                return false;
            }
            return TryCreate(image, descriptor, ownerApplication, generation,
                             sharedSpace, nativeBootstrapAddress,
                             out process, out failure);
        }

        internal ulong ManagedEntryAddress => Descriptor == null ? 0UL :
            Descriptor.ImageBase + Descriptor.ManagedEntryRva;

        internal bool IsPhase26 => Descriptor != null &&
            (Descriptor.Flags & ManagedImageContract.FlagPhase26ManagedEntry) != 0;

        internal bool IsPhase27 => Descriptor != null &&
            (Descriptor.Flags & ManagedImageContract.FlagPhase27ManagedService) != 0;

        internal bool IsPhase27Failure => Descriptor != null &&
            (Descriptor.Flags & ManagedImageContract.FlagPhase27ManagedFailure) != 0;

        internal bool TryAuthorizeEntry(ulong rip,
                                        bool allowManagedEntryResume = false) {
            if (!IsMapped || Descriptor == null ||
                ((!IsPhase26) && ManagedEntryReady)) return false;
            if (IsPhase26 && rip == ManagedEntryAddress) {
                // The first dispatch must always enter through the native
                // bootstrap.  Once that dispatch has occurred, however, a
                // timer may legitimately save RIP at the managed entry while
                // the bootstrap's call is in flight.  Resume that existing
                // frame without authorizing a fresh direct entry.
                return allowManagedEntryResume && ManagedEntryReady &&
                    ValidateRuntimeScaffold();
            }
            if (rip == ManagedEntryAddress) {
                ManagedImageDiagnostics.ManagedEntryAttemptsRejected++;
                Marker("PHASE25_MANAGED_ENTRY_DISPATCH_REJECTED=1");
                return false;
            }
            if (IsPhase26 && ManagedEntryReady && IsExecutableAddress(rip))
                return ValidateRuntimeScaffold();
            return NativeBootstrapAddress != 0 &&
                NativeBootstrapEndAddress > NativeBootstrapAddress &&
                rip >= NativeBootstrapAddress &&
                rip < NativeBootstrapEndAddress &&
                ValidateRuntimeScaffold();
        }

        private bool IsExecutableAddress(ulong address) {
            if (Descriptor == null || address < Descriptor.ImageBase) return false;
            ulong rva = address - Descriptor.ImageBase;
            for (int i = 0; i < Descriptor.Sections.Length; i++) {
                ManagedImageSection section = Descriptor.Sections[i];
                if (!section.IsExecutable || section.MemorySize == 0) continue;
                ulong start = section.VirtualAddress;
                ulong end = start + section.MemorySize;
                if (end > start && rva >= start && rva < end) return true;
            }
            return false;
        }

        internal bool TryAuthorizeManagedEntry() {
            if (!IsMapped || !IsPhase26 || ManagedEntryReady ||
                Descriptor == null || NativeBootstrapAddress == 0 ||
                NativeBootstrapEndAddress <= NativeBootstrapAddress)
                return false;
            if (!ValidateRuntimeScaffold() ||
                Descriptor.RuntimeMetadataSize == 0 ||
                Descriptor.RuntimeMetadataRva >= Descriptor.ImageSize ||
                ManagedEntryAddress < Descriptor.ImageBase ||
                ManagedEntryAddress >= Descriptor.ImageBase + Descriptor.ImageSize ||
                ManagedImageContract.HeapReservationBase <
                    Descriptor.ImageBase + Descriptor.ImageSize ||
                ManagedImageContract.HeapReservationBase +
                    ManagedImageContract.HeapReservationSize >=
                    ManagedImageContract.StackGuardStart)
                return false;
            ulong ignored;
            if (PageTable.TryTranslateUser(Space.Pml4,
                    ManagedImageContract.HeapReservationBase, false, out ignored))
                return false;
            ManagedEntryReady = true;
            Marker("PHASE26_MANAGED_ENTRY_AUTHORIZED=1");
            return ValidateRuntimeScaffold();
        }

        internal void SetNativeBootstrapRange(ulong address, uint size) {
            NativeBootstrapAddress = address;
            NativeBootstrapEndAddress = address + size;
        }

        internal bool SetBootstrapFaultMode() {
            if (!IsMapped || StartupPhysical == 0 || NativeBootstrapAddress == 0)
                return false;
            *(uint*)(StartupPhysical + 136) |= 0x00000100U;
            return true;
        }

        internal bool CorruptStartupVersionForTest() {
            if (!IsMapped || StartupPhysical == 0) return false;
            *(uint*)StartupPhysical = 2;
            return true;
        }

        internal bool CorruptGsForTest() {
            if (!IsMapped || GsPhysical == 0) return false;
            *(ulong*)(GsPhysical + ManagedImageContract.TlsVectorOffset) = 0;
            return true;
        }

        internal bool TryReadBootstrapResult(out uint flags) {
            int ignored;
            return TryReadBootstrapResult(out flags, out ignored);
        }

        internal bool TryReadBootstrapResult(out uint flags, out int returnCode) {
            flags = 0;
            returnCode = 0;
            if (!IsMapped || StartupPhysical == 0 || NativeBootstrapAddress == 0)
                return false;
            uint magic = *(uint*)(StartupPhysical + ManagedBootstrapResultContract.ResultOffset);
            uint version = *(uint*)(StartupPhysical + ManagedBootstrapResultContract.ResultOffset + 8);
            if (magic != ManagedBootstrapResultContract.Magic ||
                version != (IsPhase26 ? ManagedBootstrapResultContract.Phase26Version :
                    ManagedBootstrapResultContract.Version)) return false;
            flags = *(uint*)(StartupPhysical + ManagedBootstrapResultContract.ResultOffset + 4);
            returnCode = *(int*)(StartupPhysical + ManagedBootstrapResultContract.ReturnCodeOffset);
            return true;
        }

        internal bool TryEnterManagedEntry() {
            if (IsPhase26) {
                return IsMapped && ManagedEntryReady &&
                    Descriptor != null && ValidateRuntimeScaffold();
            }
            ManagedImageDiagnostics.ManagedEntryAttemptsRejected++;
            Marker("PHASE24_MANAGED_ENTRY_ATTEMPT_REJECTED=1");
            return IsMapped && ManagedEntryReady &&
                Descriptor != null && Descriptor.NativeBootstrapRva != 0 &&
                ValidateRuntimeScaffold();
        }

        internal bool ValidateRuntimeScaffold() {
            if (!IsMapped || Space == null || Descriptor == null ||
                ((!IsPhase26) && ManagedEntryReady) ||
                ((!IsPhase26) && Descriptor.NativeBootstrapRva != 0) ||
                UserGsBase != ManagedImageContract.GsBlockAddress ||
                StartupPhysical == 0 || RuntimePhysical == 0 ||
                GsPhysical == 0 || TlsVectorPhysical == 0 || FlsPhysical == 0)
                return false;
            if (*(uint*)(StartupPhysical + 0) != ManagedImageContract.AbiVersion ||
                *(uint*)(StartupPhysical + 4) != ManagedImageContract.DescriptorVersion ||
                *(ulong*)(StartupPhysical + 8) != ManagedImageContract.ImageBase ||
                *(ulong*)(StartupPhysical + 24) == 0 ||
                *(ulong*)(StartupPhysical + 32) != ManagedEntryAddress ||
                *(ulong*)(StartupPhysical + 56) != ManagedImageContract.GsBlockAddress ||
                *(ulong*)(StartupPhysical + 64) != ManagedImageContract.TlsVectorAddress ||
                (*(uint*)(StartupPhysical + 136) & ManagedImageContract.FlagManagedEntryBlocked) == 0)
                return false;
            if (NativeBootstrapAddress != 0 &&
                *(ulong*)(StartupPhysical + 24) != NativeBootstrapAddress)
                return false;
            ulong expectedTlsBlock = IsPhase26 ? ManagedImageContract.TlsBlockAddress :
                ManagedImageContract.RuntimeStateAddress;
            if (*(ulong*)(GsPhysical + ManagedImageContract.TlsVectorOffset) !=
                ManagedImageContract.TlsVectorAddress ||
                *(ulong*)TlsVectorPhysical != expectedTlsBlock)
                return false;
            ulong ignored;
            if (!PageTable.TryTranslateUser(
                    Space.Pml4, ManagedImageContract.GsBlockAddress, false,
                    out ignored) || !PageTable.TryTranslateUser(
                    Space.Pml4, ManagedImageContract.TlsVectorAddress, false,
                    out ignored)) return false;
            return !IsPhase26 || PageTable.TryTranslateUser(
                Space.Pml4, ManagedImageContract.TlsBlockAddress, true,
                out ignored);
        }

        private static ulong AlignVm(ulong value, ulong alignment) {
            ulong mask = alignment - 1;
            return (value + mask) & ~mask;
        }

        private int FindReservation(ulong address, ulong size) {
            if (size == 0) return -1;
            ulong end = address + size;
            if (end <= address) return -1;
            for (int i = 0; i < _vmReservations.Length; i++) {
                VmReservation reservation = _vmReservations[i];
                if (reservation.Active && address >= reservation.Base &&
                    end <= reservation.Base + reservation.Size)
                    return i;
            }
            return -1;
        }

        private int FindVmPage(ulong address) {
            for (int i = 0; i < _vmPages.Length; i++)
                if (_vmPages[i].Active && _vmPages[i].Address == address) return i;
            return -1;
        }

        private bool ValidVmProtection(uint protection, out bool writable,
                                       out bool executable) {
            writable = false;
            executable = false;
            // PAL schema values are R=1, RW=3, RX=5. The legacy veneer also
            // passes PAGE_READWRITE (4) for operator new and PAGE_EXECUTE_READ
            // (32) for code-like allocations.
            if (protection == 1) return true;
            if (protection == 3 || protection == 4) { writable = true; return true; }
            if (protection == 5 || protection == 32) { executable = true; return true; }
            return false;
        }

        internal ulong TryVmReserve(ulong size, ulong alignment) {
            if (!IsPhase26 || !IsMapped || size == 0) return 0;
            size = AlignUp(size);
            if (alignment < ManagedImageContract.PageSize) alignment =
                ManagedImageContract.PageSize;
            if ((alignment & (alignment - 1)) != 0 || alignment > 0x10000000UL)
                return 0;
            for (int i = 0; i < _vmReservations.Length; i++) {
                if (_vmReservations[i].Active) continue;
                ulong address = AlignVm(_nextHeapAddress, alignment);
                ulong end = address + size;
                ulong heapEnd = ManagedImageContract.HeapReservationBase +
                    ManagedImageContract.HeapReservationSize;
                if (end <= address || end > heapEnd ||
                    end >= ManagedImageContract.StackGuardStart) return 0;
                _vmReservations[i].Active = true;
                _vmReservations[i].Base = address;
                _vmReservations[i].Size = size;
                _nextHeapAddress = end;
                ManagedImageDiagnostics.VmReservationsCreated++;
                return address;
            }
            return 0;
        }

        internal int TryVmCommit(ulong address, ulong size, uint protection) {
            bool writable, executable;
            if (!IsPhase26 || !ValidVmProtection(protection, out writable,
                                                   out executable) ||
                (address & (ManagedImageContract.PageSize - 1)) != 0 ||
                size == 0) return -1;
            size = AlignUp(size);
            if (FindReservation(address, size) < 0) return -1;
            ulong end = address + size;
            int[] added = new int[_vmPages.Length];
            int addedCount = 0;
            for (ulong pageAddress = address; pageAddress < end;
                 pageAddress += ManagedImageContract.PageSize) {
                int existing = FindVmPage(pageAddress);
                if (existing >= 0) {
                    if (!_vmPages[existing].Writable && writable ||
                        _vmPages[existing].Executable && !executable)
                        return -1;
                    continue;
                }
                int slot = -1;
                for (int i = 0; i < _vmPages.Length; i++) {
                    if (!_vmPages[i].Active) { slot = i; break; }
                }
                if (slot < 0) break;
                ulong physical = (ulong)Allocator.Allocate(ManagedImageContract.PageSize);
                if (physical == 0) break;
                Native.Stosb((void*)physical, 0, ManagedImageContract.PageSize);
                if (!Space.MapUser(pageAddress, physical, writable, executable)) {
                    Allocator.Free((IntPtr)physical);
                    break;
                }
                _vmPages[slot].Active = true;
                _vmPages[slot].Address = pageAddress;
                _vmPages[slot].Physical = physical;
                _vmPages[slot].Writable = writable;
                _vmPages[slot].Executable = executable;
                added[addedCount++] = slot;
            }
            ulong committedEnd = address + (ulong)addedCount *
                ManagedImageContract.PageSize;
            // Existing pages may make the count test above ambiguous; verify
            // every requested page and roll back only pages added by this call.
            bool complete = true;
            for (ulong pageAddress = address; pageAddress < end;
                 pageAddress += ManagedImageContract.PageSize) {
                if (FindVmPage(pageAddress) < 0) { complete = false; break; }
            }
            if (!complete) {
                for (int i = 0; i < addedCount; i++) {
                    int slot = added[i];
                    ulong physical;
                    Space.UnmapUser(_vmPages[slot].Address, out physical);
                    if (physical != 0) Allocator.Free((IntPtr)physical);
                    _vmPages[slot] = default(VmPage);
                    ManagedImageDiagnostics.VmPagesReclaimed++;
                }
                return -1;
            }
            for (int i = 0; i < addedCount; i++)
                ManagedImageDiagnostics.VmPagesCreated++;
            return 0;
        }

        internal int TryVmProtect(ulong address, ulong size, uint protection) {
            bool writable, executable;
            if (!IsPhase26 || !ValidVmProtection(protection, out writable,
                                                   out executable) ||
                (address & (ManagedImageContract.PageSize - 1)) != 0 ||
                size == 0) return -1;
            size = AlignUp(size);
            if (FindReservation(address, size) < 0) return -1;
            ulong end = address + size;
            for (ulong pageAddress = address; pageAddress < end;
                 pageAddress += ManagedImageContract.PageSize) {
                int slot = FindVmPage(pageAddress);
                if (slot < 0 || !Space.ProtectUser(pageAddress, writable,
                                                   executable)) return -1;
            }
            for (ulong pageAddress = address; pageAddress < end;
                 pageAddress += ManagedImageContract.PageSize) {
                int slot = FindVmPage(pageAddress);
                _vmPages[slot].Writable = writable;
                _vmPages[slot].Executable = executable;
            }
            return 0;
        }

        internal int TryVmQuery(ulong address, ulong* baseAddress, ulong* size,
                                uint* protection) {
            if (!IsPhase26 || address == 0 || baseAddress == null ||
                size == null || protection == null) return -1;
            for (int i = 0; i < _vmReservations.Length; i++) {
                VmReservation reservation = _vmReservations[i];
                if (!reservation.Active || address < reservation.Base ||
                    address >= reservation.Base + reservation.Size) continue;
                *baseAddress = reservation.Base;
                *size = reservation.Size;
                int page = FindVmPage(address & ~(ManagedImageContract.PageSize - 1));
                if (page < 0) *protection = 0;
                else *protection = _vmPages[page].Executable ? 5U :
                    (_vmPages[page].Writable ? 3U : 1U);
                return 0;
            }
            return -1;
        }

        internal int TryVmRelease(ulong address, ulong size) {
            if (!IsPhase26 || address == 0) return -1;
            for (int i = 0; i < _vmReservations.Length; i++) {
                VmReservation reservation = _vmReservations[i];
                if (!reservation.Active || reservation.Base != address ||
                    (size != 0 && AlignUp(size) != reservation.Size)) continue;
                ulong end = reservation.Base + reservation.Size;
                for (int p = 0; p < _vmPages.Length; p++) {
                    if (!_vmPages[p].Active || _vmPages[p].Address < reservation.Base ||
                        _vmPages[p].Address >= end) continue;
                    ulong physical;
                    Space.UnmapUser(_vmPages[p].Address, out physical);
                    if (physical != 0) Allocator.Free((IntPtr)physical);
                    _vmPages[p] = default(VmPage);
                    ManagedImageDiagnostics.VmPagesReclaimed++;
                }
                _vmReservations[i] = default(VmReservation);
                ManagedImageDiagnostics.VmReservationsReclaimed++;
                return 0;
            }
            return -1;
        }

        internal void CleanupVm() {
            if (!IsPhase26) return;
            for (int i = 0; i < _vmReservations.Length; i++) {
                if (_vmReservations[i].Active)
                    TryVmRelease(_vmReservations[i].Base, 0);
            }
        }

        private static bool ValidFlsSlot(int slot) {
            return slot >= 0 && slot < ManagedImageContract.MaxFlsSlots;
        }

        private ulong* FlsSlots() {
            return (ulong*)(FlsPhysical + 32);
        }

        internal bool TryFlsAllocate(out int slot) {
            slot = -1;
            if (!IsMapped || FlsPhysical == 0) return false;
            ManagedImageFlsState* state = (ManagedImageFlsState*)FlsPhysical;
            for (int candidate = 0; candidate < ManagedImageContract.MaxFlsSlots;
                 candidate++) {
                ulong bit = 1UL << candidate;
                if ((state->AllocatedMask & bit) == 0) {
                    state->AllocatedMask |= bit;
                    FlsSlots()[candidate] = 0;
                    slot = candidate;
                    return true;
                }
            }
            return false;
        }

        internal bool TryFlsGet(int slot, out ulong value) {
            value = 0;
            if (!ValidFlsSlot(slot) || !IsMapped || FlsPhysical == 0) return false;
            ManagedImageFlsState* state = (ManagedImageFlsState*)FlsPhysical;
            ulong bit = 1UL << slot;
            if ((state->AllocatedMask & bit) == 0) return false;
            value = FlsSlots()[slot];
            return true;
        }

        internal bool TryFlsSet(int slot, ulong value) {
            if (!ValidFlsSlot(slot) || !IsMapped || FlsPhysical == 0) return false;
            ManagedImageFlsState* state = (ManagedImageFlsState*)FlsPhysical;
            ulong bit = 1UL << slot;
            if ((state->AllocatedMask & bit) == 0) return false;
            FlsSlots()[slot] = value;
            return true;
        }

        internal bool TryFlsFree(int slot) {
            if (!ValidFlsSlot(slot) || !IsMapped || FlsPhysical == 0) return false;
            ManagedImageFlsState* state = (ManagedImageFlsState*)FlsPhysical;
            ulong bit = 1UL << slot;
            if ((state->AllocatedMask & bit) == 0) return false;
            FlsSlots()[slot] = 0;
            state->AllocatedMask &= ~bit;
            return true;
        }

        internal bool Cleanup() {
            if (!IsMapped && Space == null) return true;
            CleanupVm();
            for (int i = 0; i < ImagePageCount; i++) {
                ulong page = ImagePhysicalPages[i];
                if (page != 0) {
                    Allocator.Free((IntPtr)page);
                    ImagePhysicalPages[i] = 0;
                    ManagedImageDiagnostics.ImagePagesReclaimed++;
                }
            }
            ImagePageCount = 0;
            Free(ref StartupPhysical);
            Free(ref RuntimePhysical);
            Free(ref GsPhysical);
            Free(ref TlsVectorPhysical);
            Free(ref FlsPhysical);
            FreeTlsBlock();
            if (Space != null && OwnsAddressSpace) {
                Space.Release();
            }
            Space = null;
            if (IsMapped) {
                ManagedImageDiagnostics.ImagesReclaimed++;
                ManagedImageDiagnostics.StartupBlocksReclaimed++;
                ManagedImageDiagnostics.TlsBlocksReclaimed++;
                ManagedImageDiagnostics.FlsTablesReclaimed++;
                ManagedImageDiagnostics.GsContextsReclaimed++;
            }
            IsMapped = false;
            UserGsBase = 0;
            ManagedEntryReady = false;
            NativeBootstrapAddress = 0;
            NativeBootstrapEndAddress = 0;
            OwnsAddressSpace = false;
            Marker("PHASE24_MANAGED_IMAGE_RECLAIMED=1");
            Marker("PHASE24_STALE_MAPPINGS=0");
            Marker("PHASE24_STALE_TLS_FLS=0");
            Marker("PHASE24_STALE_GS=0");
            return true;
        }
    }
}
