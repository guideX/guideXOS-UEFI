namespace guideXOS.FS {
    /// <summary>Result of a bounded block-device operation.</summary>
    public enum DiskIoResult : byte {
        Success = 0,
        Unsupported = 1,
        ReadOnly = 2,
        InvalidRange = 3,
        InvalidBuffer = 4,
        MediaUnavailable = 5,
        TransportFailure = 6,
        WriteFailure = 7,
        FlushUnsupported = 8,
        FlushFailure = 9
    }

    [System.Flags]
    public enum DiskCapabilities : byte {
        None = 0,
        Readable = 1,
        Writable = 2,
        FlushSupported = 4
    }

    /// <summary>
    /// Block device. Typed operations validate geometry and caller buffer length
    /// before invoking a transport. Legacy bool helpers remain for existing
    /// callers and return true only for Success.
    /// </summary>
    public abstract unsafe class Disk {
        public static Disk Instance;

        protected Disk() { }

        public virtual uint BlockSize => 512;
        public virtual ulong BlockCount => 0;
        public virtual DiskCapabilities Capabilities => DiskCapabilities.None;

        protected virtual DiskIoResult ReadCore(ulong lba, uint count, byte* data) => DiskIoResult.Unsupported;
        protected virtual DiskIoResult WriteCore(ulong lba, uint count, byte* data) => DiskIoResult.Unsupported;
        protected virtual DiskIoResult FlushCore() => DiskIoResult.FlushUnsupported;

        public DiskIoResult ReadBlocks(ulong lba, uint count, byte* data, ulong bufferLengthBytes) {
            DiskIoResult validation = ValidateRequest(lba, count, data, bufferLengthBytes);
            if (validation != DiskIoResult.Success) return validation;
            if (count == 0) return DiskIoResult.Success;
            if ((Capabilities & DiskCapabilities.Readable) == 0) return DiskIoResult.Unsupported;
            if (!IsAvailable) return DiskIoResult.MediaUnavailable;
            return ReadCore(lba, count, data);
        }

        public DiskIoResult WriteBlocks(ulong lba, uint count, byte* data, ulong bufferLengthBytes) {
            DiskIoResult validation = ValidateRequest(lba, count, data, bufferLengthBytes);
            if (validation != DiskIoResult.Success) return validation;
            if (count == 0) return DiskIoResult.Success;
            if ((Capabilities & DiskCapabilities.Writable) == 0) return DiskIoResult.ReadOnly;
            if (!IsAvailable) return DiskIoResult.MediaUnavailable;
            DiskIoResult result = WriteCore(lba, count, data);
            return result == DiskIoResult.TransportFailure ? DiskIoResult.WriteFailure : result;
        }

        public DiskIoResult Flush() {
            if ((Capabilities & DiskCapabilities.FlushSupported) == 0) return DiskIoResult.FlushUnsupported;
            if (!IsAvailable) return DiskIoResult.MediaUnavailable;
            return FlushCore();
        }

        /// <summary>False when the transport is absent or its media was removed.</summary>
        public virtual bool IsAvailable => true;

        private DiskIoResult ValidateRequest(ulong lba, uint count, byte* data, ulong bufferLengthBytes) {
            uint blockSize = BlockSize;
            if (blockSize == 0) return DiskIoResult.MediaUnavailable;
            if (count == 0) {
                // Empty requests are no-ops when LBA is at or before the end.
                return lba <= BlockCount && bufferLengthBytes == 0
                    ? DiskIoResult.Success : DiskIoResult.InvalidRange;
            }
            if (data == null) return DiskIoResult.InvalidBuffer;
            if ((ulong)count > 0xFFFFFFFFFFFFFFFFUL / blockSize) return DiskIoResult.InvalidRange;
            ulong byteCount = (ulong)count * blockSize;
            if (bufferLengthBytes < byteCount) return DiskIoResult.InvalidBuffer;
            ulong capacity = BlockCount;
            if (lba > capacity || (ulong)count > capacity - lba) return DiskIoResult.InvalidRange;
            return DiskIoResult.Success;
        }

        public bool Read(ulong sector, uint count, byte* data) {
            if (BlockSize == 0) return false;
            if ((ulong)count > 0xFFFFFFFFFFFFFFFFUL / BlockSize) return false;
            return ReadBlocks(sector, count, data, (ulong)count * BlockSize) == DiskIoResult.Success;
        }

        public bool Write(ulong sector, uint count, byte* data) {
            if (BlockSize == 0) return false;
            if ((ulong)count > 0xFFFFFFFFFFFFFFFFUL / BlockSize) return false;
            return WriteBlocks(sector, count, data, (ulong)count * BlockSize) == DiskIoResult.Success;
        }
    }
}
