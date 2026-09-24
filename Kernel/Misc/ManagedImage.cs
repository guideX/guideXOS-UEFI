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
        internal const ulong TlsVectorOffset = 0x58UL;
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
            fixed (byte* imagePtr = image) {
                byte* hash = stackalloc byte[32];
                SHA256.Compute(imagePtr, image.Length, hash);
                for (int i = 0; i < 32; i++) {
                    if (hash[i] != descriptor.Sha256[i]) {
                        failure = "ARTIFACT_HASH";
                        return false;
                    }
                }
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

        internal static bool IsBalanced {
            get {
                return ImagesMapped == ImagesReclaimed &&
                       ImagePagesAllocated == ImagePagesReclaimed &&
                       StartupBlocksCreated == StartupBlocksReclaimed &&
                       TlsBlocksCreated == TlsBlocksReclaimed &&
                       FlsTablesCreated == FlsTablesReclaimed &&
                       GsContextsCreated == GsContextsReclaimed;
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
        internal ulong OwnerApplication;
        internal uint Generation;
        internal ulong UserGsBase;
        internal bool ManagedEntryReady;
        internal bool IsMapped;
        internal int BssBytesZeroed;

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

        private bool MapImage(byte[] image) {
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
            startup.NativeBootstrapAddress = 0;
            startup.ManagedEntryAddress = Descriptor.ImageBase + Descriptor.ManagedEntryRva;
            startup.RuntimeMetadataBase = Descriptor.ImageBase + Descriptor.RuntimeMetadataRva;
            startup.RuntimeMetadataSize = Descriptor.RuntimeMetadataSize;
            startup.GsBlockAddress = ManagedImageContract.GsBlockAddress;
            startup.TlsVectorAddress = ManagedImageContract.TlsVectorAddress;
            startup.TlsIndex = 0;
            startup.FlsStateAddress = ManagedImageContract.FlsStateAddress;
            startup.UserRuntimeStateAddress = ManagedImageContract.RuntimeStateAddress;
            startup.PalVeneerAddress = 0;
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
            tlsVector[0] = ManagedImageContract.RuntimeStateAddress;
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
                ManagedEntryReady = false
            };
            process.Space = new AddressSpace();
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
            process = null;
            failure = null;
            if (File.Instance == null) {
                failure = "NO_FILESYSTEM";
                return false;
            }
            byte[] image = File.ReadAllBytes("Native/guideXOS.UserManagedProof.exe");
            byte[] descriptor = File.ReadAllBytes("Native/guideXOS.UserManagedProof.gxmi");
            if (image == null || descriptor == null) {
                failure = "PHASE24_IMAGE_NOT_STAGED";
                return false;
            }
            return TryCreate(image, descriptor, ownerApplication, generation,
                             out process, out failure);
        }

        internal bool TryEnterManagedEntry() {
            ManagedImageDiagnostics.ManagedEntryAttemptsRejected++;
            Marker("PHASE24_MANAGED_ENTRY_ATTEMPT_REJECTED=1");
            return IsMapped && ManagedEntryReady &&
                Descriptor != null && Descriptor.NativeBootstrapRva != 0 &&
                ValidateRuntimeScaffold();
        }

        internal bool ValidateRuntimeScaffold() {
            if (!IsMapped || Space == null || Descriptor == null ||
                ManagedEntryReady || Descriptor.NativeBootstrapRva != 0 ||
                UserGsBase != ManagedImageContract.GsBlockAddress ||
                StartupPhysical == 0 || RuntimePhysical == 0 ||
                GsPhysical == 0 || TlsVectorPhysical == 0 || FlsPhysical == 0)
                return false;
            if (*(ulong*)(GsPhysical + ManagedImageContract.TlsVectorOffset) !=
                ManagedImageContract.TlsVectorAddress ||
                *(ulong*)TlsVectorPhysical != ManagedImageContract.RuntimeStateAddress)
                return false;
            ulong ignored;
            return PageTable.TryTranslateUser(
                Space.Pml4, ManagedImageContract.GsBlockAddress, false, out ignored) &&
                PageTable.TryTranslateUser(
                Space.Pml4, ManagedImageContract.TlsVectorAddress, false, out ignored);
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
            if (Space != null) {
                Space.Release();
                Space = null;
            }
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
            Marker("PHASE24_MANAGED_IMAGE_RECLAIMED=1");
            Marker("PHASE24_STALE_MAPPINGS=0");
            Marker("PHASE24_STALE_TLS_FLS=0");
            Marker("PHASE24_STALE_GS=0");
            return true;
        }
    }
}
