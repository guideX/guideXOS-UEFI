using System.Collections.Generic;

namespace guideXOS.FS {
    public enum FileAttribute : byte {
        ReadOnly = 0x01,
        Hidden = 0x02,
        System = 0x04,
        Directory = 0x10,
        Archive = 0x20
    }

    public sealed class FileInfo {
        internal string Name;
        internal FileAttribute Attribute;
        internal ulong Param0;
        internal ulong Param1;
        internal void Dispose() { }
    }

    public static class File {
        public static FileSystem Instance;
    }

    public abstract class FileSystem {
        internal const int SectorSize = 512;
        internal const int FS_TYPE_UNKNOWN = 0;
        internal const int FS_TYPE_RDSK = 1;
        internal const int FS_TYPE_TAR = 2;
        internal const int FS_TYPE_FAT = 3;

        public int FileSystemType;

        protected FileSystem() : this(true) { }
        protected FileSystem(bool registerAsDefault) {
            if (registerAsDefault) File.Instance = this;
        }

        internal virtual FileSystemDirectoryInspection InspectDirectory(
                string path) {
            if (!FileSystemDirectoryInspection.IsValidRelativePath(path)) {
                return FileSystemDirectoryInspection.Create(
                    FileSystemDirectoryInspectionStatus.InvalidPath);
            }
            return FileSystemDirectoryInspection.Create(
                FileSystemDirectoryInspectionStatus.Indeterminate);
        }

        public abstract List<FileInfo> GetFiles(string Directory);
        public abstract void Delete(string Name);
        public abstract byte[] ReadAllBytes(string Name);
        public abstract void WriteAllBytes(string Name, byte[] Content);
        public virtual FatOperationResult TryWriteAllBytes(string name,
                byte[] content) { return FatOperationResult.Unsupported; }
        public virtual FatOperationResult TryReadRange(string name,
                long offset, byte[] destination, int destinationOffset,
                int maximumBytes, out int bytesRead, out bool endOfFile) {
            bytesRead = 0;
            endOfFile = false;
            return FatOperationResult.Unsupported;
        }
        public virtual FatOperationResult TryDelete(string name) {
            return FatOperationResult.Unsupported;
        }
        public virtual FatOperationResult TrySync() {
            return FatOperationResult.Unsupported;
        }
        public virtual FatOperationResult TryFormat() {
            return FatOperationResult.Unsupported;
        }
        public abstract void Format();
    }

    internal static class HostedDisposableExtensions {
        internal static void Dispose(this string value) { }
        internal static void Dispose<T>(this T[] value) { }
        internal static void Dispose<T>(this List<T> value) { }
    }
}

namespace guideXOS.Misc {
    internal static class Panic {
        internal static void Error(string message) { throw new System.Exception(message); }
    }

    internal static unsafe class Native {
        internal static void Movsb(byte* destination, byte* source,
                ulong length) {
            for (ulong i = 0; i < length; i++) destination[i] = source[i];
        }
    }
}
