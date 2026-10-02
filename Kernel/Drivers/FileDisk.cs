using guideXOS.FS;
using guideXOS.Misc;
using System;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// File-backed virtual disk for mounting .img files as disks
    /// </summary>
    public unsafe class FileDisk : Disk {
        private byte[] _imageData;
        private string _imagePath;

        /// <summary>
        /// Create a virtual disk from a disk image file
        /// </summary>
        /// <param name="imagePath">Path to the .img file (e.g., "/disks/test-fat32.img")</param>
        public FileDisk(string imagePath) {
            _imagePath = imagePath;
            
            // Read the entire image file into memory
            _imageData = File.ReadAllBytes(imagePath);
            
            if (_imageData == null || _imageData.Length == 0) {
                Console.WriteLine($"[FileDisk] Error: Failed to load image '{imagePath}'");
                return;
            }
            
            // Verify image size is multiple of 512 (sector size)
            if (_imageData.Length % 512 != 0) {
                Console.WriteLine($"[FileDisk] Warning: Image '{imagePath}' size ({_imageData.Length} bytes) is not a multiple of 512");
            }
            
            Console.WriteLine($"[FileDisk] Loaded '{imagePath}' ({_imageData.Length} bytes, {_imageData.Length / 512} sectors)");
        }

        public override uint BlockSize => 512;
        public override ulong BlockCount => _imageData == null ? 0UL : (ulong)_imageData.Length / 512UL;
        public override DiskCapabilities Capabilities => _imageData == null
            ? DiskCapabilities.None
            : DiskCapabilities.Readable | DiskCapabilities.Writable;

        protected override DiskIoResult ReadCore(ulong sector, uint count, byte* data) {
            if (_imageData == null) return DiskIoResult.MediaUnavailable;
            ulong byteOffset = sector * 512;
            ulong byteCount = (ulong)count * 512;
            
            // Bounds check
            if (byteOffset + byteCount > (ulong)_imageData.Length) {
                Console.WriteLine($"[FileDisk] Read beyond image bounds: sector {sector}, count {count}");
                return DiskIoResult.InvalidRange;
            }
            
            // Copy data from image buffer to destination
            fixed (byte* src = &_imageData[byteOffset]) {
                Native.Movsb(data, src, byteCount);
            }
            
            return DiskIoResult.Success;
        }

        protected override DiskIoResult WriteCore(ulong sector, uint count, byte* data) {
            if (_imageData == null) return DiskIoResult.MediaUnavailable;
            ulong byteOffset = sector * 512;
            ulong byteCount = (ulong)count * 512;
            
            // Bounds check
            if (byteOffset + byteCount > (ulong)_imageData.Length) {
                Console.WriteLine($"[FileDisk] Write beyond image bounds: sector {sector}, count {count}");
                return DiskIoResult.InvalidRange;
            }
            
            // Copy data from source to image buffer
            fixed (byte* dst = &_imageData[byteOffset]) {
                Native.Movsb(dst, data, byteCount);
            }
            
            return DiskIoResult.Success;
        }

        /// <summary>
        /// Save changes back to the image file (if filesystem supports writes)
        /// </summary>
        public void Sync() {
            try {
                File.WriteAllBytes(_imagePath, _imageData);
                Console.WriteLine($"[FileDisk] Synced changes to '{_imagePath}'");
            } catch {
                Console.WriteLine($"[FileDisk] Failed to sync changes to '{_imagePath}'");
            }
        }

        /// <summary>
        /// Get the size of the disk in sectors
        /// </summary>
        public ulong SectorCount => (ulong)_imageData.Length / 512;

        /// <summary>
        /// Get the size of the disk in bytes
        /// </summary>
        public ulong Size => (ulong)_imageData.Length;
    }
}
