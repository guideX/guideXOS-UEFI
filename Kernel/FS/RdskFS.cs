using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Helpers;
using guideXOS.Misc;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace guideXOS.FS {
    /// <summary>
    /// RDSK filesystem - custom ramdisk format used by guideXOS
    /// Format:
    ///   Magic: "RDSK" (4 bytes)
    ///   Version: 1 (4 bytes, little-endian)
    ///   FileCount: N (4 bytes, little-endian)
    ///   For each file:
    ///     PathLength: 2 bytes (little-endian)
    ///     Path: UTF-8 string
    ///     DataLength: 4 bytes (little-endian)
    ///     Data: raw bytes
    /// </summary>
    internal unsafe class RdskFS : FileSystem {

        public RdskFS()
        {
            BootConsole.WriteLine("[RdskFS] Constructor called");
            FileSystemType = FS_TYPE_RDSK;
            
            // Use the raw static pointer directly - it survives UEFI managed reference corruption
            byte* ptr = Ramdisk.RawBasePointer;
            if (ptr == null) {
                BootConsole.WriteLine("[RdskFS] ERROR: RawBasePointer is null!");
                return;
            }
            
            BootConsole.WriteLine("[RdskFS] Got raw pointer");
            
            // Validate RDSK header
            if (ptr[0] == (byte)'R' && ptr[1] == (byte)'D' && ptr[2] == (byte)'S' && ptr[3] == (byte)'K') {
                BootConsole.WriteLine("[RdskFS] Magic OK");
            } else {
                BootConsole.WriteLine("[RdskFS] WARNING: Invalid magic, may not work");
            }
            
            BootConsole.WriteLine("[RdskFS] Constructor completed");
        }

        public override List<FileInfo> GetFiles(string Directory) {
            List<FileInfo> result = new List<FileInfo>();
            if (Directory == null) return result;

            // The ramdisk has no directory records.  Reconstruct the direct
            // children of the requested directory from the packed paths.
            string directory = Directory;
            if (directory.Length > 0 && directory[0] == '/') {
                directory = directory.Substring(1);
            }
            if (directory.Length > 0 && directory[directory.Length - 1] != '/') {
                string withSlash = directory + "/";
                directory = withSlash;
            }

            byte* ptr = Ramdisk.RawBasePointer;
            if (ptr == null || ptr[0] != (byte)'R' || ptr[1] != (byte)'D' ||
                ptr[2] != (byte)'S' || ptr[3] != (byte)'K') {
                return result;
            }

            uint fileCount = *(uint*)(ptr + 8);
            if (fileCount == 0 || fileCount > 10000) {
                return result;
            }

            ulong offset = 12;
            const ulong MAX_RDSK_SIZE = 512 * 1024 * 1024;
            for (uint i = 0; i < fileCount; i++) {
                if (offset > MAX_RDSK_SIZE || offset + 2 > MAX_RDSK_SIZE) break;

                ushort pathLen = *(ushort*)(ptr + offset);
                offset += 2;
                if (pathLen == 0 || pathLen > 512 || offset + pathLen > MAX_RDSK_SIZE) break;

                bool pathValid = true;
                for (ushort j = 0; j < pathLen; j++) {
                    byte b = ptr[offset + j];
                    if (b == 0 || b > 127) {
                        pathValid = false;
                        break;
                    }
                }
                if (!pathValid) break;

                string path = null;
                try {
                    path = string.FromASCII((nint)(ptr + offset), pathLen);
                } catch {
                    break;
                }
                offset += pathLen;

                if (offset + 4 > MAX_RDSK_SIZE) {
                    if (path != null) path.Dispose();
                    break;
                }
                uint dataLen = *(uint*)(ptr + offset);
                offset += 4;
                if (dataLen > 100 * 1024 * 1024 || offset + dataLen > MAX_RDSK_SIZE) {
                    if (path != null) path.Dispose();
                    break;
                }

                if (path != null && IsUnderDirectory(path, directory)) {
                    // RDSK stores file paths only, so synthesize one entry for
                    // each direct child directory and retain normal file
                    // entries.  Without this, the real desktop can display a
                    // root file but can never navigate to nested fixtures.
                    string child = directory.Length == 0 ? path :
                        path.Substring(directory.Length);
                    int slash = child.IndexOf('/');
                    string name = slash >= 0 ? child.Substring(0, slash) :
                        path.Substring(path.LastIndexOf('/') + 1);

                    if (name != null && !ContainsResultName(result, name)) {
                        FileInfo info = new FileInfo();
                        info.Name = name;
                        if (slash >= 0) {
                            info.Attribute = FileAttribute.Directory;
                        } else {
                            info.Attribute = FileAttribute.Archive;
                            info.Param0 = offset;
                            info.Param1 = dataLen;
                        }
                        result.Add(info);
                    } else if (name != null) {
                        name.Dispose();
                    }

                    if (directory.Length != 0 && child != null) {
                        child.Dispose();
                    }
                }

                if (path != null) path.Dispose();
                offset += dataLen;
            }

            return result;
        }

        private static bool IsUnderDirectory(string path, string directory) {
            if (directory == null || path == null || path.Length < directory.Length) {
                return false;
            }
            for (int i = 0; i < directory.Length; i++) {
                if (path[i] != directory[i]) return false;
            }
            return true;
        }

        private static bool ContainsResultName(List<FileInfo> result, string name) {
            for (int i = 0; i < result.Count; i++) {
                if (result[i] != null && result[i].Name != null &&
                    result[i].Name.Equals(name)) return true;
            }
            return false;
        }
        
        public override void Delete(string Name) { }
        
        /// <summary>
        /// Read file by scanning RDSK on-demand (no pre-caching)
        /// </summary>
        public override byte[] ReadAllBytes(string Name) {
            if (Name == null) {
                return null;
            }
            
            // Normalize path (remove leading slash if present)
            string searchPath = Name;
            if (Name.Length > 0 && Name[0] == '/') {
                searchPath = Name.Substring(1);
            }
            
            // Use static raw pointer directly - survives UEFI managed reference corruption
            byte* ptr = Ramdisk.RawBasePointer;
            if (ptr == null) {
                return null;
            }
            
            // Check magic
            if (ptr[0] != (byte)'R' || ptr[1] != (byte)'D' || ptr[2] != (byte)'S' || ptr[3] != (byte)'K') {
                return null;
            }
            
            // Read file count carefully
            uint fileCount = *(uint*)(ptr + 8);
            
            // Safety: limit file count to prevent DoS
            if (fileCount == 0 || fileCount > 10000) {
                return null;
            }
            
            // Scan through files
            ulong offset = 12; // Start after header
            const ulong MAX_RDSK_SIZE = 512 * 1024 * 1024; // 512MB max size safety limit
            
            for (uint i = 0; i < fileCount; i++) {
                // Safety check: don't read past reasonable bounds
                if (offset > MAX_RDSK_SIZE || offset + 2 > MAX_RDSK_SIZE) {
                    break;
                }
                
                // Read path length (2 bytes)
                ushort pathLen = *(ushort*)(ptr + offset);
                offset += 2;
                
                // Validate path length
                if (pathLen == 0 || pathLen > 512) {
                    break;
                }
                
                // Safety: ensure we can read the path
                if (offset + pathLen > MAX_RDSK_SIZE) {
                    break;
                }
                
                // Validate path data before trying to create string
                bool pathValid = true;
                for (ushort j = 0; j < pathLen; j++) {
                    byte b = ptr[offset + j];
                    if (b == 0 || b > 127) {
                        pathValid = false;
                        break;
                    }
                }
                
                if (!pathValid) {
                    break;
                }
                
                // Read path
                string path = null;
                try {
                    path = string.FromASCII((nint)(ptr + offset), pathLen);
                } catch {
                    break;
                }
                offset += pathLen;
                
                // Safety: ensure we can read dataLen
                if (offset + 4 > MAX_RDSK_SIZE) {
                    if (path != null) path.Dispose();
                    break;
                }
                
                // Read data length (4 bytes)
                uint dataLen = *(uint*)(ptr + offset);
                offset += 4;
                
                // Validate data length
                if (dataLen > 100 * 1024 * 1024) {
                    if (path != null) path.Dispose();
                    break;
                }
                
                // Safety: ensure data exists within bounds
                if (offset + dataLen > MAX_RDSK_SIZE) {
                    if (path != null) path.Dispose();
                    break;
                }
                
                // Check if this is the file we want
                if (path != null && (path.Equals(searchPath) || path.Equals(Name))) {
                    // Copy file data
                    byte[] buffer = new byte[dataLen];
                    byte* src = ptr + offset;
                    
                    fixed (byte* dst = buffer) {
                        for (uint j = 0; j < dataLen; j++) {
                            dst[j] = src[j];
                        }
                    }
                    
                    path.Dispose();
                    return buffer;
                }
                
                if (path != null) path.Dispose();
                
                // Skip past data
                offset += dataLen;
            }
            
            return null;
        }
        
        public override void WriteAllBytes(string Name, byte[] Content) { }
        
        public override void Format() { }
    }
}
