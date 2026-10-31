using guideXOS.FS;
using guideXOS.Misc;
using System;

namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// RAM disk bootstrap integration.
    ///
    /// UEFI loader already provides an initrd (bootInfo->RamdiskBase/RamdiskSize).
    /// This manager:
    /// - exposes the UEFI-provided ramdisk as Disk.Instance
    /// - mounts a filesystem for File API
    /// - optionally formats a secondary in-memory disk with MiniRamFs
    /// </summary>
    internal static unsafe class RamDiskManager {
        private static bool _initialized;

        public static Disk BootDisk { get; private set; }

        /// <summary>
        /// Initializes the boot ramdisk passed from the UEFI loader and mounts a filesystem.
        /// Call early during boot (UEFI EntryPoint after Ramdisk creation, before higher-level code uses File.*).
        /// </summary>
        public static void Initialize(UefiBootInfo* bootInfo) {
            if (_initialized) return;
            _initialized = true;

            if (bootInfo == null || !bootInfo->HasRamdisk || bootInfo->RamdiskBase == 0 || bootInfo->RamdiskSize == 0) {
                // No initrd provided - leave Disk/File as-is.
                return;
            }

            // Expose UEFI-provided ramdisk as the active disk.
            BootDisk = new Ramdisk((IntPtr)bootInfo->RamdiskBase,
                bootInfo->RamdiskSize);

            // Prefer existing behavior: auto-detect TAR/FAT/EXT2 in initrd.
            // EntryPoint currently skips AutoFS due to a noted hang; keep this behind a try/catch.
            try {
                new AutoFS();
            } catch {
                // Fallback: leave File.Instance unmodified. Callers can mount manually.
            }
        }

        /// <summary>
        /// Creates and mounts a fresh scratch RAM disk with MiniRamFs and makes it the active File API.
        /// This is optional; useful for /tmp-like storage if initrd is read-only.
        /// </summary>
        public static void CreateAndMountScratch(ulong sizeBytes) {
            // Allocate from kernel allocator; page-aligned by design.
            IntPtr ptr = Allocator.Allocate(sizeBytes, Allocator.AllocTag.FileBuffer);
            if (ptr == IntPtr.Zero) {
                // Kernel-friendly: fail silently (or add serial marker) rather than throwing.
                return;
            }

            // Point Disk.Instance to the new memory.
            var rd = new Ramdisk(ptr);

            // Format + mount MiniRamFs.
            var fs = new MiniRamFs(rd);
            fs.Format();
            fs.Mount();

            // Make it the OS file API.
            File.Instance = fs;
        }
    }
}
