using guideXOS.FS;
using System;
using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    internal static class NativeBootstrapContract {
        internal const uint DescriptorMagic = 0x49425847; // GXBI
        internal const uint DescriptorVersion = 1;
        internal const uint MachineAmd64 = 0x8664;
        internal const uint FlagFixedBase = 1;
        internal const uint FlagNoRelocations = 2;
        internal const uint FlagNoImports = 4;
        internal const uint FlagExecutable = 8;
        internal const uint FlagManagedImageExternal = 16;
        internal const uint RequiredFlags = FlagFixedBase | FlagNoRelocations |
            FlagNoImports | FlagExecutable | FlagManagedImageExternal;
        internal const ulong ImageBase = 0x0000401200000000UL;
        internal const ulong ImageSizeLimit = 0x10000UL;
        internal const ulong UserLimit = PageTable.UserAddressLimit;
        internal const ulong PageSize = 0x1000UL;
        internal const ulong FutureHeapBase = 0x0000401300000000UL;
        internal const ulong FutureHeapSize = 0x0000001000000000UL;
        internal const uint StartupBlockVersion = 1;
        internal const uint AbiVersion = 1;
        internal const int DescriptorSize = 92;
        internal const int MaxPages = 16;
    }

    internal static class ManagedBootstrapResultContract {
        internal const uint Magic = 0x31524247; // GBR1
        internal const uint Version = 1;
        internal const uint Environment = 0x00000001;
        internal const uint PreemptionSentinel = 0x00000002;
        internal const uint GsTls = 0x00000004;
        internal const uint TlsVector = 0x00000008;
        internal const uint Fls = 0x00000010;
        internal const uint Ping = 0x00000020;
        internal const uint Ipc = 0x00000040;
        internal const uint Response = 0x00000080;
        internal const uint Exit = 0x00000100;
        internal const uint SetupFlags = Environment | GsTls | TlsVector | Fls;
        internal const uint SuccessFlags = 0x000001FF;
        internal const ulong ResultOffset = 0x300UL;
    }

    internal sealed class NativeBootstrapDescriptor {
        internal uint Version;
        internal uint Machine;
        internal uint Flags;
        internal uint FileSize;
        internal uint ImageSize;
        internal ulong PreferredBase;
        internal uint EntryOffset;
        internal uint ExecutableOffset;
        internal uint ExecutableSize;
        internal uint StartupBlockVersion;
        internal uint AbiVersion;
        internal uint ImportCount;
        internal uint RelocationCount;
        internal byte[] Sha256;
    }

    internal static unsafe class NativeBootstrapDescriptorReader {
        private static bool MatchesExpectedSha256(byte[] hash) {
            if (hash == null || hash.Length != 32) return false;
            return hash[0] == 0xFA && hash[1] == 0xE4 &&
                hash[2] == 0x25 && hash[3] == 0x24 &&
                hash[4] == 0x50 && hash[5] == 0x5D &&
                hash[6] == 0x18 && hash[7] == 0xF3 &&
                hash[8] == 0x1A && hash[9] == 0x59 &&
                hash[10] == 0xF6 && hash[11] == 0x2F &&
                hash[12] == 0x2B && hash[13] == 0xE3 &&
                hash[14] == 0x5C && hash[15] == 0x2B &&
                hash[16] == 0x3E && hash[17] == 0x85 &&
                hash[18] == 0xD6 && hash[19] == 0x11 &&
                hash[20] == 0xA5 && hash[21] == 0x20 &&
                hash[22] == 0x98 && hash[23] == 0xC0 &&
                hash[24] == 0x75 && hash[25] == 0xD3 &&
                hash[26] == 0x34 && hash[27] == 0xEC &&
                hash[28] == 0x2A && hash[29] == 0x9D &&
                hash[30] == 0x89 && hash[31] == 0xF3;
        }

        private static uint U32(byte[] data, int offset) {
            return (uint)data[offset] | ((uint)data[offset + 1] << 8) |
                   ((uint)data[offset + 2] << 16) | ((uint)data[offset + 3] << 24);
        }

        private static ulong U64(byte[] data, int offset) {
            return (ulong)U32(data, offset) | ((ulong)U32(data, offset + 4) << 32);
        }

        internal static bool TryRead(byte[] data,
                                     out NativeBootstrapDescriptor descriptor,
                                     out string failure) {
            descriptor = null;
            failure = null;
            if (data == null || data.Length != NativeBootstrapContract.DescriptorSize) {
                failure = "DESCRIPTOR_SIZE";
                return false;
            }
            if (U32(data, 0) != NativeBootstrapContract.DescriptorMagic) {
                failure = "DESCRIPTOR_MAGIC";
                return false;
            }
            descriptor = new NativeBootstrapDescriptor {
                Version = U32(data, 4),
                Machine = U32(data, 8),
                Flags = U32(data, 12),
                FileSize = U32(data, 16),
                ImageSize = U32(data, 20),
                PreferredBase = U64(data, 24),
                EntryOffset = U32(data, 32),
                ExecutableOffset = U32(data, 36),
                ExecutableSize = U32(data, 40),
                StartupBlockVersion = U32(data, 44),
                AbiVersion = U32(data, 48),
                ImportCount = U32(data, 52),
                RelocationCount = U32(data, 56),
                Sha256 = new byte[32]
            };
            for (int i = 0; i < 32; i++) descriptor.Sha256[i] = data[60 + i];
            return true;
        }

        internal static bool TryValidate(NativeBootstrapDescriptor descriptor,
                                         byte[] image, out string failure) {
            failure = null;
            if (descriptor == null || image == null) {
                failure = "NULL_INPUT";
                return false;
            }
            uint alignedSize = ((uint)image.Length + 0xFFFU) & ~0xFFFU;
            if (descriptor.Version != NativeBootstrapContract.DescriptorVersion ||
                descriptor.Machine != NativeBootstrapContract.MachineAmd64 ||
                (descriptor.Flags & NativeBootstrapContract.RequiredFlags) !=
                    NativeBootstrapContract.RequiredFlags ||
                descriptor.FileSize != (uint)image.Length ||
                descriptor.ImageSize != alignedSize ||
                descriptor.ImageSize == 0 ||
                descriptor.ImageSize > NativeBootstrapContract.ImageSizeLimit ||
                descriptor.PreferredBase != NativeBootstrapContract.ImageBase ||
                descriptor.EntryOffset != 0 || descriptor.ExecutableOffset != 0 ||
                descriptor.ExecutableSize != descriptor.FileSize ||
                descriptor.StartupBlockVersion != NativeBootstrapContract.StartupBlockVersion ||
                descriptor.AbiVersion != NativeBootstrapContract.AbiVersion ||
                descriptor.ImportCount != 0 || descriptor.RelocationCount != 0) {
                failure = "DESCRIPTOR_CONTRACT";
                return false;
            }
            bool hashMatches = MatchesExpectedSha256(descriptor.Sha256);
            if (!hashMatches) {
                failure = "ARTIFACT_HASH";
                return false;
            }
            ulong imageEnd = descriptor.PreferredBase + descriptor.ImageSize;
            if (imageEnd <= descriptor.PreferredBase ||
                imageEnd >= NativeBootstrapContract.UserLimit ||
                descriptor.PreferredBase < ManagedImageContract.FlsStateAddress + 0x1000UL ||
                imageEnd > NativeBootstrapContract.FutureHeapBase) {
                failure = "IMAGE_RANGE";
                return false;
            }
            return true;
        }
    }

    internal static class NativeBootstrapDiagnostics {
        internal static int ImagesMapped;
        internal static int ImagesReclaimed;
        internal static int PagesAllocated;
        internal static int PagesReclaimed;

        internal static bool IsBalanced {
            get {
                return ImagesMapped == ImagesReclaimed &&
                       PagesAllocated == PagesReclaimed;
            }
        }
    }

    internal unsafe sealed class NativeBootstrapImage {
        internal AddressSpace Space;
        internal NativeBootstrapDescriptor Descriptor;
        internal ulong[] PhysicalPages = new ulong[NativeBootstrapContract.MaxPages];
        internal int PageCount;
        internal bool IsMapped;

        internal ulong EntryAddress => Descriptor == null ? 0UL :
            Descriptor.PreferredBase + Descriptor.EntryOffset;
        internal ulong ImageBase => Descriptor == null ? 0UL : Descriptor.PreferredBase;
        internal uint ImageSize => Descriptor == null ? 0U : Descriptor.ImageSize;

        private static void Marker(string text) {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++) Native.Out8(0x3F8, (byte)text[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void Free(ref ulong page) {
            if (page != 0) {
                Allocator.Free((IntPtr)page);
                page = 0;
                NativeBootstrapDiagnostics.PagesReclaimed++;
            }
        }

        internal static bool TryCreateFromRamdisk(AddressSpace space,
                                                   out NativeBootstrapImage bootstrap,
                                                   out string failure) {
            bootstrap = null;
            failure = null;
            if (space == null || File.Instance == null) {
                failure = "NO_ADDRESS_SPACE_OR_FILESYSTEM";
                return false;
            }
            byte[] image = File.ReadAllBytes("Native/guideXOS.Phase25Bootstrap.bin");
            byte[] descriptor = File.ReadAllBytes("Native/guideXOS.Phase25Bootstrap.gxbi");
            if (image == null || descriptor == null) {
                failure = "PHASE25_BOOTSTRAP_NOT_STAGED";
                return false;
            }
            return TryCreate(space, image, descriptor, out bootstrap, out failure);
        }

        internal static bool TryCreate(AddressSpace space, byte[] image,
                                       byte[] descriptorBytes,
                                       out NativeBootstrapImage bootstrap,
                                       out string failure) {
            bootstrap = null;
            failure = null;
            NativeBootstrapDescriptor descriptor;
            if (!NativeBootstrapDescriptorReader.TryRead(descriptorBytes,
                    out descriptor, out failure) ||
                !NativeBootstrapDescriptorReader.TryValidate(descriptor, image,
                    out failure)) return false;

            bootstrap = new NativeBootstrapImage {
                Space = space,
                Descriptor = descriptor
            };
            int pages = (int)(descriptor.ImageSize / NativeBootstrapContract.PageSize);
            for (int i = 0; i < pages; i++) {
                ulong physical = (ulong)Allocator.Allocate(NativeBootstrapContract.PageSize);
                if (physical == 0) {
                    bootstrap.Cleanup();
                    bootstrap = null;
                    failure = "PAGE_ALLOC_FAILED";
                    return false;
                }
                NativeBootstrapDiagnostics.PagesAllocated++;
                Native.Stosb((void*)physical, 0, NativeBootstrapContract.PageSize);
                ulong sourceOffset = (ulong)i * NativeBootstrapContract.PageSize;
                ulong copy = (ulong)image.Length > sourceOffset
                    ? (ulong)image.Length - sourceOffset : 0UL;
                if (copy > NativeBootstrapContract.PageSize)
                    copy = NativeBootstrapContract.PageSize;
                if (copy != 0) {
                    fixed (byte* source = image) {
                        Native.Movsb((void*)physical, source + sourceOffset, copy);
                    }
                }
                if (!space.MapUser(descriptor.PreferredBase + sourceOffset,
                                   physical, writable: false, executable: true)) {
                    Allocator.Free((IntPtr)physical);
                    NativeBootstrapDiagnostics.PagesReclaimed++;
                    bootstrap.Cleanup();
                    bootstrap = null;
                    failure = "PAGE_MAP_FAILED";
                    return false;
                }
                bootstrap.PhysicalPages[bootstrap.PageCount++] = physical;
            }
            bootstrap.IsMapped = true;
            NativeBootstrapDiagnostics.ImagesMapped++;
            Marker("PHASE25_BOOTSTRAP_MAPPED=1");
            Marker("PHASE25_BOOTSTRAP_RX=1");
            Marker("PHASE25_BOOTSTRAP_IMPORTS=0");
            Marker("PHASE25_BOOTSTRAP_RELOCATIONS=0");
            return true;
        }

        internal bool Cleanup() {
            for (int i = 0; i < PageCount; i++) Free(ref PhysicalPages[i]);
            PageCount = 0;
            if (IsMapped) {
                IsMapped = false;
                NativeBootstrapDiagnostics.ImagesReclaimed++;
                Marker("PHASE25_BOOTSTRAP_RECLAIMED=1");
            }
            Space = null;
            Descriptor = null;
            return true;
        }
    }
}
