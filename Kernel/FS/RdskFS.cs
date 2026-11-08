using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Helpers;
using guideXOS.Misc;
using System.Collections.Generic;

namespace guideXOS.FS {
    /// <summary>
    /// Read-only RDSK filesystem. Each mount retains the exact image pointer
    /// and captured length supplied when the Ramdisk was created.
    /// </summary>
    internal unsafe class RdskFS : FileSystem {
        private readonly byte* _imageBase;
        private readonly ulong _imageSizeBytes;
        private readonly object _imageValidationSync = new object();
        private bool _imageValidationComplete;
        private bool _imageValid;
        private uint _imageVersion;
#if GUIDEXOS_RDSK_HOSTED_TESTS
        private int _imageValidationCountForHostedTests;
        internal int ImageValidationCountForHostedTests {
            get { return _imageValidationCountForHostedTests; }
        }
#endif

        public RdskFS() {
            BootConsole.WriteLine("[RdskFS] Constructor called");
            FileSystemType = FS_TYPE_RDSK;

            _imageBase = Ramdisk.RawBasePointer;
            _imageSizeBytes = Ramdisk.RawImageSizeBytes;
            if (_imageBase == null) {
                BootConsole.WriteLine("[RdskFS] ERROR: RawBasePointer is null!");
                return;
            }

            BootConsole.WriteLine("[RdskFS] Captured ramdisk pointer and size");
            BootConsole.WriteLine("[RdskFS] Constructor completed");
        }

        internal override FileSystemDirectoryInspection InspectDirectory(
                string path) {
            uint version;
            if (!TryGetValidatedVersion(out version)) {
                if (!FileSystemDirectoryInspection.IsValidRelativePath(path)) {
                    return FileSystemDirectoryInspection.Create(
                        FileSystemDirectoryInspectionStatus.InvalidPath);
                }
                return FileSystemDirectoryInspection.Create(
                    _imageBase == null
                        ? FileSystemDirectoryInspectionStatus.FilesystemUnavailable
                        : FileSystemDirectoryInspectionStatus.Indeterminate);
            }
            return RdskFSDirectoryInspector.InspectValidated(_imageBase,
                _imageSizeBytes, version, path);
        }

        public override List<FileInfo> GetFiles(string Directory) {
            List<FileInfo> result = new List<FileInfo>();
            if (Directory == null || _imageBase == null) return result;

            uint version;
            if (!TryGetValidatedVersion(out version)) return result;

            string directory;
            bool ownsDirectory;
            if (!TryNormalizeDirectory(Directory, out directory,
                    out ownsDirectory)) return result;

            if (version == RdskFSDirectoryInspector.Version2) {
                GetV2Files(directory, result);
            } else {
                GetV1Files(directory, result);
            }

            if (ownsDirectory) directory.Dispose();
            return result;
        }

        private void GetV2Files(string directory, List<FileInfo> result) {
            uint directoryIndex;
            if (!TryResolveV2Directory(directory, out directoryIndex)) return;

            uint recordCount = RdskFSDirectoryInspector.ReadUInt32(
                _imageBase, 24);
            ulong cursor = RdskFSDirectoryInspector.ReadUInt64(_imageBase, 36);
            for (uint i = 0; i < recordCount; i++) {
                byte kind = _imageBase[(int)cursor];
                ushort nameLength = RdskFSDirectoryInspector.ReadUInt16(
                    _imageBase, cursor + 2);
                uint parentIndex = RdskFSDirectoryInspector.ReadUInt32(
                    _imageBase, cursor + 4);
                ulong nameOffset = cursor + 28;

                if (i != 0 && parentIndex == directoryIndex) {
                    string name = DecodeAscii(nameOffset, nameLength);
                    if (name != null) {
                        FileInfo info = new FileInfo();
                        info.Name = name;
                        if (kind == 1) {
                            info.Attribute = FileAttribute.Directory;
                        } else {
                            info.Attribute = FileAttribute.Archive;
                            info.Param0 = RdskFSDirectoryInspector.ReadUInt64(
                                _imageBase, cursor + 8);
                            info.Param1 = RdskFSDirectoryInspector.ReadUInt64(
                                _imageBase, cursor + 16);
                        }
                        result.Add(info);
                    }
                }
                cursor = nameOffset + nameLength;
            }
        }

        private bool TryResolveV2Directory(string path,
                                           out uint directoryIndex) {
            directoryIndex = 0;
            if (path.Length == 0) return true;

            uint recordCount = RdskFSDirectoryInspector.ReadUInt32(
                _imageBase, 24);
            ulong recordsOffset = RdskFSDirectoryInspector.ReadUInt64(
                _imageBase, 36);
            uint parentIndex = 0;
            int segmentStart = 0;
            for (int i = 0; i <= path.Length; i++) {
                if (i < path.Length && path[i] != '/') continue;
                uint childIndex;
                byte childKind;
                if (!RdskFSDirectoryInspector.TryFindChild(_imageBase,
                        recordsOffset, recordCount, parentIndex, path,
                        segmentStart, i - segmentStart, out childIndex,
                        out childKind) || childKind != 1) return false;
                parentIndex = childIndex;
                segmentStart = i + 1;
            }
            directoryIndex = parentIndex;
            return true;
        }

        private void GetV1Files(string directory, List<FileInfo> result) {
            // RDSK v1 contains only file paths. Preserve its historical
            // synthesized direct-child directories for existing boot images.
            string prefix = directory;
            bool ownsPrefix = false;
            if (prefix.Length > 0 && prefix[prefix.Length - 1] != '/') {
                prefix = prefix + "/";
                ownsPrefix = true;
            }

            uint fileCount = RdskFSDirectoryInspector.ReadUInt32(_imageBase, 8);
            ulong offset = 12;
            for (uint i = 0; i < fileCount; i++) {
                ushort pathLength = RdskFSDirectoryInspector.ReadUInt16(
                    _imageBase, offset);
                offset += 2;
                string path = DecodeAscii(offset, pathLength);
                offset += pathLength;
                uint dataLength = RdskFSDirectoryInspector.ReadUInt32(
                    _imageBase, offset);
                offset += 4;
                ulong dataOffset = offset;

                if (path != null && IsUnderDirectory(path, prefix)) {
                    string child = directory.Length == 0 ? path :
                        path.Substring(prefix.Length);
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
                            info.Param0 = dataOffset;
                            info.Param1 = dataLength;
                        }
                        result.Add(info);
                    } else if (name != null) {
                        name.Dispose();
                    }

                    if (directory.Length != 0 && child != null) child.Dispose();
                }

                if (path != null) path.Dispose();
                offset += dataLength;
            }
            if (ownsPrefix) prefix.Dispose();
        }

        private static bool TryNormalizeDirectory(string input,
                out string path, out bool ownsPath) {
            path = input;
            ownsPath = false;
            if (input == "/") {
                path = string.Empty;
                return true;
            }
            if (input.Length > 1 && input[0] == '/' && input[1] == '/') {
                path = string.Empty;
                return false;
            }

            if (path.Length > 0 && path[0] == '/') {
                path = path.Substring(1);
                ownsPath = true;
            }
            if (path.Length > 0 && path[path.Length - 1] == '/') {
                string trimmed = path.Substring(0, path.Length - 1);
                if (ownsPath) path.Dispose();
                path = trimmed;
                ownsPath = true;
            }
            if (path.Length == 0) return true;
            if (!FileSystemDirectoryInspection.IsValidRelativePath(path) ||
                    !RdskFSDirectoryInspector.IsAsciiPath(path)) {
                if (ownsPath) path.Dispose();
                path = string.Empty;
                ownsPath = false;
                return false;
            }
            return true;
        }

        private string DecodeAscii(ulong offset, ushort length) {
            try {
#if GUIDEXOS_RDSK_HOSTED_TESTS
                return RdskFSHostedAscii.Decode(_imageBase + (int)offset,
                    length);
#else
                return string.FromASCII((nint)(_imageBase + (int)offset),
                    length);
#endif
            } catch {
                return null;
            }
        }

        private static bool IsUnderDirectory(string path, string directory) {
            if (directory == null || path == null ||
                    path.Length < directory.Length) return false;
            for (int i = 0; i < directory.Length; i++) {
                if (path[i] != directory[i]) return false;
            }
            return true;
        }

        private static bool ContainsResultName(List<FileInfo> result,
                                               string name) {
            for (int i = 0; i < result.Count; i++) {
                if (result[i] != null && result[i].Name != null &&
                        result[i].Name.Equals(name)) return true;
            }
            return false;
        }

        public override void Delete(string Name) { }

        public override byte[] ReadAllBytes(string Name) {
            if (Name == null || _imageBase == null) return null;
            uint version;
            if (!TryGetValidatedVersion(out version)) return null;
            return version == RdskFSDirectoryInspector.Version2
                ? ReadV2File(Name)
                : ReadV1File(Name);
        }

        /// <summary>
        /// Validate the captured read-only ramdisk once per mount. The image
        /// backing pointer and captured size are fixed for this mount, and
        /// RdskFS exposes no write operation, so repeating the complete v2
        /// CRC for every file read only repeats the same work.
        /// </summary>
        private bool TryGetValidatedVersion(out uint version) {
            lock (_imageValidationSync) {
                if (!_imageValidationComplete) {
                    _imageValid = RdskFSDirectoryInspector.ValidateImage(
                        _imageBase, _imageSizeBytes, out _imageVersion);
#if GUIDEXOS_RDSK_HOSTED_TESTS
                    _imageValidationCountForHostedTests++;
#endif
                    _imageValidationComplete = true;
                }
                version = _imageValid ? _imageVersion : 0;
                return _imageValid;
            }
        }

        private byte[] ReadV2File(string name) {
            string path = name;
            bool ownsPath = false;
            if (path.Length > 0 && path[0] == '/') {
                path = path.Substring(1);
                ownsPath = true;
            }
            if (!FileSystemDirectoryInspection.IsValidRelativePath(path) ||
                    !RdskFSDirectoryInspector.IsAsciiPath(path)) {
                if (ownsPath) path.Dispose();
                return null;
            }

            uint recordCount = RdskFSDirectoryInspector.ReadUInt32(
                _imageBase, 24);
            ulong recordsOffset = RdskFSDirectoryInspector.ReadUInt64(
                _imageBase, 36);
            uint parentIndex = 0;
            int segmentStart = 0;
            ulong foundDataOffset = 0;
            ulong foundDataLength = 0;
            bool foundFile = false;
            for (int i = 0; i <= path.Length; i++) {
                if (i < path.Length && path[i] != '/') continue;
                uint childIndex;
                byte childKind;
                if (!RdskFSDirectoryInspector.TryFindChild(_imageBase,
                        recordsOffset, recordCount, parentIndex, path,
                        segmentStart, i - segmentStart, out childIndex,
                        out childKind)) break;

                bool isLast = i == path.Length;
                if (!isLast && childKind != 1) break;
                if (isLast && childKind == 2) {
                    ulong cursor = recordsOffset;
                    for (uint record = 0; record <= childIndex; record++) {
                        ushort nameLength =
                            RdskFSDirectoryInspector.ReadUInt16(_imageBase,
                                cursor + 2);
                        if (record == childIndex) {
                            foundDataOffset =
                                RdskFSDirectoryInspector.ReadUInt64(_imageBase,
                                    cursor + 8);
                            foundDataLength =
                                RdskFSDirectoryInspector.ReadUInt64(_imageBase,
                                    cursor + 16);
                            foundFile = true;
                            break;
                        }
                        cursor += 28UL + nameLength;
                    }
                    break;
                }
                if (isLast) break;
                parentIndex = childIndex;
                segmentStart = i + 1;
            }
            if (ownsPath) path.Dispose();
            if (!foundFile || foundDataLength > int.MaxValue) return null;
            return CopyFileBytes(foundDataOffset, foundDataLength);
        }

        private byte[] ReadV1File(string name) {
            string searchPath = name;
            bool ownsPath = false;
            if (name.Length > 0 && name[0] == '/') {
                searchPath = name.Substring(1);
                ownsPath = true;
            }

            uint fileCount = RdskFSDirectoryInspector.ReadUInt32(_imageBase, 8);
            ulong offset = 12;
            for (uint i = 0; i < fileCount; i++) {
                ushort pathLength = RdskFSDirectoryInspector.ReadUInt16(
                    _imageBase, offset);
                offset += 2;
                string path = DecodeAscii(offset, pathLength);
                offset += pathLength;
                uint dataLength = RdskFSDirectoryInspector.ReadUInt32(
                    _imageBase, offset);
                offset += 4;
                ulong dataOffset = offset;
                bool matches = path != null &&
                    (path.Equals(searchPath) || path.Equals(name));
                if (path != null) path.Dispose();
                if (matches) {
                    if (ownsPath) searchPath.Dispose();
                    return CopyFileBytes(dataOffset, dataLength);
                }
                offset += dataLength;
            }
            if (ownsPath) searchPath.Dispose();
            return null;
        }

        private byte[] CopyFileBytes(ulong dataOffset, ulong dataLength) {
            byte[] buffer = new byte[(int)dataLength];
            byte* source = _imageBase + (int)dataOffset;
            fixed (byte* destination = buffer) {
                for (ulong i = 0; i < dataLength; i++) {
                    destination[(int)i] = source[(int)i];
                }
            }
            return buffer;
        }

        public override void WriteAllBytes(string Name, byte[] Content) { }
        public override void Format() { }
    }
}
