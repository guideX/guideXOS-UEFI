using guideXOS.Kernel.Drivers;
using guideXOS.Misc;

namespace guideXOS.Kernel.Tools {
    // Utility to compute SHA-256 of a USB mass storage device contents safely.
    public static unsafe class USBMassHash {
        // Computes SHA-256 over the first maxBytes bytes of the first registered USB disk.
        // If maxBytes is 0, hashes the full device. Returns null on failure.
        public static string ComputeSHA256(ulong maxBytes = 0) {
            var dev = USBStorage.GetFirst(); if (dev == null) return null;
            var disk = USBMSC.TryOpenDisk(dev); if (disk == null || !disk.IsReady) return null;
            ulong totalBytes = disk.TotalBlocks * disk.LogicalBlockSize;
            if (maxBytes != 0 && maxBytes < totalBytes) totalBytes = maxBytes;

            const int BufSize = 64 * 1024; // 64KB buffer
            byte* buf = (byte*)Allocator.Allocate((ulong)BufSize);
            if (buf == null) return null;

            SHA256Ctx ctx; SHA256.Init(&ctx);
            ulong offset = 0;
            while (offset < totalBytes) {
                int toRead = (int)((totalBytes - offset) > (ulong)BufSize ? BufSize : (totalBytes - offset));
                // Read is sector-based. Compute sector/count assuming 512B sectors.
                ulong sector = offset / 512UL;
                uint count = (uint)((toRead + 511) / 512);
                if (!disk.Read(sector, count, buf)) { Allocator.Free((nint)buf, "USBMassHash"); return null; }
                // Hash only the valid bytes (not full last sector rounding).
                int valid = (int)((offset + (ulong)(count * 512)) <= totalBytes ? (count * 512) : (totalBytes - offset));
                SHA256.Update(&ctx, buf, valid);
                offset += (ulong)valid;
            }
            byte* out32 = stackalloc byte[32];
            SHA256.Final(&ctx, out32);
            string hex = SHA256.ToHex(out32);
            Allocator.Free((nint)buf, "USBMassHash");
            return hex;
        }
    }
}
