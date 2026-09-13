namespace guideXOS.FS {
    /// <summary>
    /// Detects filesystem in the attached Disk and instantiates appropriate FileSystem.
    /// Supports TAR (initrd) and FAT12/16/32 images.
    /// </summary>
    internal unsafe partial class AutoFS : FileSystem {
        /// <summary>
        /// Implementation
        /// </summary>
        private FileSystem _impl;
        /// <summary>
        /// Constructor
        /// </summary>
        public AutoFS(Disk disk) {
            // Peek at sector 0
            var buf = new byte[SectorSize];
            fixed (byte* p = buf) disk.Read(0, 1, p);
            if (LooksLikeRdsk(buf)) {
                _impl = new RdskFS();
            } else if (LooksLikeTar(buf)) {
                _impl = new TarFS(disk);
            } else if (LooksLikeFat(buf)) {
                _impl = new FAT(disk);
            } else if (LooksLikeExt(disk)) {
                _impl = new EXT2(disk);
            } else {
                // Fallback to TarFS for backward compatibility
                _impl = new TarFS(disk);
            }
        }
        public AutoFS() {
            // Peek at sector 0
            var buf = new byte[SectorSize];
            fixed (byte* p = buf) Disk.Instance.Read(0, 1, p);
            if (LooksLikeRdsk(buf)) {
                _impl = new RdskFS();
            } else if (LooksLikeTar(buf)) {
                _impl = new TarFS();
            } else if (LooksLikeFat(buf)) {
                _impl = new FAT();
            } else if (LooksLikeExt(buf)) {
                _impl = new EXT2();
            } else {
                // Fallback to TarFS for backward compatibility
                _impl = new TarFS();
            }
            // Set as active implementation
            File.Instance = _impl;
        }
        /// <summary>
        /// Looks like a TAR archive?
        /// </summary>
        /// <param name="sector0"></param>
        /// <returns></returns>
        private static bool LooksLikeTar(byte[] sector0) {
            // POSIX ustar magic at offset 257: "ustar\0" or "ustar\x00" and version "00"
            if (sector0.Length < 512) return false;
            return sector0[257] == (byte)'u' && sector0[258] == (byte)'s' && sector0[259] == (byte)'t' &&
                   sector0[260] == (byte)'a' && sector0[261] == (byte)'r';
        }

        private static bool LooksLikeRdsk(byte[] sector0) {
            return sector0 != null && sector0.Length >= 4 &&
                   sector0[0] == (byte)'R' && sector0[1] == (byte)'D' &&
                   sector0[2] == (byte)'S' && sector0[3] == (byte)'K';
        }
        /// <summary>
        /// Looks like a FAT filesystem?
        /// </summary>
        /// <param name="sector0"></param>
        /// <returns></returns>
        private static bool LooksLikeFat(byte[] sector0) {
            if (sector0.Length < 512) return false;
            // Signature 0x55AA at 510
            if (sector0[510] != 0x55 || sector0[511] != 0xAA) return false;
            ushort bytsPerSec = (ushort)(sector0[11] | (sector0[12] << 8));
            byte secPerClus = sector0[13];
            ushort rsvdSec = (ushort)(sector0[14] | (sector0[15] << 8));
            byte numFATs = sector0[16];
            if (bytsPerSec == 0) return false;
            if (secPerClus == 0) return false;
            if (numFATs == 0) return false;
            // Accept typical bytes per sector
            if (bytsPerSec != 512 && bytsPerSec != 1024 && bytsPerSec != 2048 && bytsPerSec != 4096) return false;
            // SecPerClus power-of-two up to 128
            if ((secPerClus & (secPerClus - 1)) != 0 || secPerClus > 128) return false;
            // Reserved sectors at least 1
            if (rsvdSec == 0) return false;
            return true;
        }

        private static bool LooksLikeExt(Disk disk)
        {
            var buf = new byte[SectorSize * 3];
            fixed (byte* p = buf) disk.Read(0, 3, p);
            return LooksLikeExt(buf);
        }

        private static bool LooksLikeExt(byte[] buf)
        {
            // ext magic number 0x53EF at offset 0x38 in superblock (which is at 1024 bytes from start)
            if (buf.Length < 2048) return false;
            return buf[1024 + 0x38] == 0x53 && buf[1024 + 0x39] == 0xEF;
        }
        /// <summary>
        /// Get Files
        /// </summary>
        /// <param name="Directory"></param>
        /// <returns></returns>
        public override System.Collections.Generic.List<FileInfo> GetFiles(string Directory) => _impl.GetFiles(Directory);
        /// <summary>
        /// Delete File
        /// </summary>
        /// <param name="Name"></param>
        public override void Delete(string Name) => _impl.Delete(Name);
        /// <summary>
        /// Read All Bytes
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public override byte[] ReadAllBytes(string Name) => _impl.ReadAllBytes(Name);
        /// <summary>
        /// Write All Bytes
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="Content"></param>
        public override void WriteAllBytes(string Name, byte[] Content) => _impl.WriteAllBytes(Name, Content);
        /// <summary>
        /// Format
        /// </summary>
        public override void Format() => _impl.Format();
    }
}
