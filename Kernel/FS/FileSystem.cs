using System.Collections.Generic;
namespace guideXOS.FS {
    /// <summary>
    /// File Info
    /// </summary>
    public class FileInfo {
        /// <summary>
        /// Name
        /// </summary>
        public string Name;
        /// <summary>
        /// Attributes
        /// </summary>
        public FileAttribute Attribute;
        /// <summary>
        /// Param 0
        /// </summary>
        public ulong Param0;
        /// <summary>
        /// Param 1
        /// </summary>
        public ulong Param1;
        /// <summary>
        /// Dispose
        /// </summary>
        public override void Dispose() {
            Name.Dispose();
            base.Dispose();
        }
    }
    /// <summary>
    /// File Attribute
    /// </summary>
    public enum FileAttribute : byte {
        ReadOnly = 0x01,
        Hidden = 0x02,
        System = 0x04,
        Directory = 0x10,
        Archive = 0x20,
    }
    /// <summary>
    /// File
    /// </summary>
    public static class File {
        /// <summary>
        /// Instance
        /// </summary>
        public static FileSystem Instance;
        /// <summary>
        /// Get Files
        /// </summary>
        /// <param name="Directory"></param>
        /// <returns></returns>
        public static List<FileInfo> GetFiles(string Directory) => Instance.GetFiles(Directory);
        /// <summary>
        /// Read All Bytes
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static byte[] ReadAllBytes(string name) {
            BootConsole.WriteLine("[File.ReadAllBytes] Called for: " + name);
            
            if (Instance == null) {
                BootConsole.WriteLine("[File.ReadAllBytes] ERROR: Instance is NULL!");
                return null;
            }
            
            BootConsole.WriteLine("[File.ReadAllBytes] FS Type: " + Instance.FileSystemType.ToString());
            
            // Use type flag instead of 'as' casts (more reliable in bare-metal)
            try {
                byte[] result = Instance.ReadAllBytes(name);
                if (result != null) {
                    BootConsole.WriteLine("[File.ReadAllBytes] Success, size: " + result.Length.ToString());
                } else {
                    BootConsole.WriteLine("[File.ReadAllBytes] Returned NULL");
                }
                return result;
            } catch {
                BootConsole.WriteLine("[File.ReadAllBytes] Exception in ReadAllBytes!");
                return null;
            }
        }
        /// <summary>
        /// Read All Bytes
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        //public static string ReadAllText(string name) => Instance.ReadAllText(name);
        /// <summary>
        /// Write All Bytes
        /// </summary>
        /// <param name="name"></param>
        /// <param name="content"></param>
        public static FatOperationResult WriteAllBytes(string name, byte[] content) => TryWriteAllBytes(name, content);
        public static FatOperationResult TryWriteAllBytes(string name, byte[] content) =>
            Instance == null ? FatOperationResult.NotMounted : Instance.TryWriteAllBytes(name, content);
        public static FatOperationResult TryDelete(string name) =>
            Instance == null ? FatOperationResult.NotMounted : Instance.TryDelete(name);
        public static FatOperationResult TrySync() =>
            Instance == null ? FatOperationResult.NotMounted : Instance.TrySync();
        public static FatOperationResult TryFormat() =>
            Instance == null ? FatOperationResult.NotMounted : Instance.TryFormat();
        /// <summary>
        /// Exists (directory scan)
        /// </summary>
        /// <param name="name"></param>
        public static bool Exists(string name) {
            string dir = "";
            string just = name;
            int last = name.LastIndexOf('/');
            if (last >= 0) {
                dir = name.Substring(0, last + 1);
                just = name.Substring(last + 1);
            }
            var list = GetFiles(dir);
            for (int i = 0; i < list.Count; i++) {
                if (list[i].Name == just) return true;
            }
            return false;
        }
    }
    /// <summary>
    /// File System
    /// </summary>
    public abstract class FileSystem {
        /// <summary>
        /// File system type identifier (workaround for type checking)
        /// </summary>
        public int FileSystemType;
        
        public const int FS_TYPE_UNKNOWN = 0;
        public const int FS_TYPE_RDSK = 1;
        public const int FS_TYPE_TAR = 2;
        public const int FS_TYPE_FAT = 3;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public FileSystem() {
            FileSystemType = FS_TYPE_UNKNOWN;
            File.Instance = this;
        }
        /// <summary>
        /// Sector Size
        /// </summary>
        public const int SectorSize = 512;
        /// <summary>
        /// Size to Sec
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static ulong SizeToSec(ulong size) {
            return ((size - (size % SectorSize)) / SectorSize) + ((size % SectorSize) != 0 ? 1ul : 0);
        }
        /// <summary>
        /// Is In Directory
        /// </summary>
        /// <param name="fname"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static bool IsInDirectory(string fname, string dir) {
            if (fname.Length < dir.Length) return false;
            for (int i = 0; i < fname.Length; i++) {
                if (i < dir.Length) {
                    if (dir[i] != fname[i]) return false;
                } else {
                    if (fname[i] == '/') return false;
                }
            }
            return true;
        }
        /// <summary>
        /// Get Files
        /// </summary>
        /// <param name="Directory"></param>
        /// <returns></returns>
        public abstract List<FileInfo> GetFiles(string Directory);
        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="Name"></param>
        public abstract void Delete(string Name);
        /// <summary>
        /// Read All Bytes
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public abstract byte[] ReadAllBytes(string Name);
        /// <summary>
        /// Write All Bytes
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="Content"></param>
        public abstract void WriteAllBytes(string Name, byte[] Content);
        /// <summary>Truthful write result; filesystems opt in when they can report operation status.</summary>
        public virtual FatOperationResult TryWriteAllBytes(string name, byte[] content) => FatOperationResult.Unsupported;
        public virtual FatOperationResult TryDelete(string name) => FatOperationResult.Unsupported;
        /// <summary>Flush all filesystem writes and the backing block device when supported.</summary>
        public virtual FatOperationResult TrySync() => FatOperationResult.Unsupported;
        /// <summary>Formats the active filesystem and reports write and flush failures.</summary>
        public virtual FatOperationResult TryFormat() => FatOperationResult.Unsupported;
        /// <summary>
        /// Format
        /// </summary>
        public abstract void Format();
    }
}
