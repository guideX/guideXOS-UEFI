using guideXOS.FS;
using System;
namespace guideXOS.Kernel.Drivers {
    public unsafe class Ramdisk : Disk {
        byte* ptr;
        
        // WORKAROUND: Static instance for direct access since RTTI is broken in UEFI mode
        public static Ramdisk Instance;
        
        // CRITICAL: Store the raw pointer in a static field to avoid GC/object relocation issues
        public static byte* RawBasePointer;

        // Size of ramdisk in bytes (for bounds checking)
        private readonly ulong _sizeBytes;

        // Some bootloaders place a header before the TAR. We auto-detect and skip it.
        private ulong _baseOffsetBytes;
        private bool _offsetDetected;

        public Ramdisk(IntPtr _ptr, ulong sizeBytes = 0) {
            BootConsole.WriteLine("[Ramdisk] Constructor called");
            
            ptr = (byte*)_ptr;
            
            // CRITICAL: Store in static field for reliable access
            RawBasePointer = ptr;
            
            BootConsole.WriteLine("[Ramdisk] ptr field set");

            _baseOffsetBytes = 0;
            _offsetDetected = false;

            // Try to find the ramdisk size from boot info if available; keep 0 when unknown.
            try {
                // Many environments store this in BootInfo; if not present it stays 0 (no bounds checks).
                // This is intentionally best-effort to avoid breaking boot if fields change.
                _sizeBytes = sizeBytes;
            } catch {
                _sizeBytes = 0;
            }
            
            // Test: Try to read first byte directly to verify memory is accessible
            if (ptr != null) {
                byte testByte = *ptr;
                if (testByte == 0) {
                    BootConsole.WriteLine("[Ramdisk] WARNING: First byte at ramdisk is ZERO");
                } else {
                    BootConsole.WriteLine("[Ramdisk] First byte OK (non-zero)");
                }
            } else {
                BootConsole.WriteLine("[Ramdisk] ERROR: ptr is null!");
            }
            
            // CRITICAL: Set both Instance pointers
            Disk.Instance = this;
            Instance = this; // Direct reference for TarFS
            
            BootConsole.WriteLine("[Ramdisk] Disk.Instance set");
        }

        public override uint BlockSize => 512;
        public override ulong BlockCount => _sizeBytes == 0 ? 0xFFFFFFFFFFFFFFFFUL / 512UL : _sizeBytes / 512UL;
        public override DiskCapabilities Capabilities => DiskCapabilities.Readable | DiskCapabilities.Writable;

        private static void CopyBytes(byte* dest, byte* src, ulong len) {
            for (ulong i = 0; i < len; i++) {
                dest[i] = src[i];
            }
        }

        private static bool HasUstarMagic(byte* block512) {
            // ustar is at offset 257..261 in the TAR header
            return block512[257] == (byte)'u' &&
                   block512[258] == (byte)'s' &&
                   block512[259] == (byte)'t' &&
                   block512[260] == (byte)'a' &&
                   block512[261] == (byte)'r';
        }

        private void EnsureTarOffsetDetected() {
            if (_offsetDetected || ptr == null) return;

            _offsetDetected = true;
            _baseOffsetBytes = 0;

            // Scan first few KB for a TAR header. Keep it tiny to avoid slowing boot.
            // We assume the TAR starts on a 512-byte boundary.
            const int maxSectorsToScan = 256; // 128KB
            byte* tmp = stackalloc byte[512];

            for (int s = 0; s < maxSectorsToScan; s++) {
                byte* src = ptr + ((ulong)s * 512ul);
                CopyBytes(tmp, src, 512);
                if (HasUstarMagic(tmp)) {
                    _baseOffsetBytes = (ulong)s * 512ul;
                    BootConsole.WriteLine("[Ramdisk] TAR header found at byte offset: " + _baseOffsetBytes.ToString());
                    return;
                }
            }

            BootConsole.WriteLine("[Ramdisk] WARNING: TAR magic not found in initial scan; using offset 0");
        }

        protected override DiskIoResult ReadCore(ulong sector, uint count, byte* p) {
            if (ptr == null) {
                return DiskIoResult.MediaUnavailable;
            }

            if (p == null) {
                return DiskIoResult.InvalidBuffer;
            }

            EnsureTarOffsetDetected();

            ulong byteOffset = _baseOffsetBytes + (sector * 512ul);
            ulong byteCount = (ulong)count * 512ul;

            if (byteCount == 0) return DiskIoResult.Success;

            // Bounds check only when size is known (>0)
            if (_sizeBytes != 0 && (byteOffset > _sizeBytes || byteCount > _sizeBytes - byteOffset)) {
                return DiskIoResult.InvalidRange;
            }
            
            // Calculate source address
            byte* src = ptr + byteOffset;

            CopyBytes(p, src, byteCount);
            
            return DiskIoResult.Success;
        }

        protected override DiskIoResult WriteCore(ulong sector, uint count, byte* p) {
            if (ptr == null) return DiskIoResult.MediaUnavailable;
            if (p == null) return DiskIoResult.InvalidBuffer;

            EnsureTarOffsetDetected();

            ulong byteOffset = _baseOffsetBytes + (sector * 512ul);
            ulong byteCount = (ulong)count * 512ul;

            if (byteCount == 0) return DiskIoResult.Success;
            if (_sizeBytes != 0 && (byteOffset > _sizeBytes || byteCount > _sizeBytes - byteOffset)) return DiskIoResult.InvalidRange;

            CopyBytes(ptr + byteOffset, p, byteCount);
            return DiskIoResult.Success;
        }

        /// <summary>
        /// Get raw pointer to ramdisk memory for direct access (used by RdskFS)
        /// </summary>
        public byte* GetRawPointer() {
            // Use static field to avoid GC/object relocation issues
            return RawBasePointer;
        }
    }
}
