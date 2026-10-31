namespace guideXOS.FS {
    /// <summary>
    /// Performs allocation-free, bounded directory inspection of an RDSK v1
    /// image. RDSK contains file paths only; directories are implicit parents
    /// of file paths and its root is implicit in a valid image.
    /// </summary>
    internal static unsafe class RdskFSDirectoryInspector {
        private const uint MaxFileCount = 10000;
        private const uint MaxFileBytes = 100 * 1024 * 1024;
        private const ulong MaxImageOffset = 512UL * 1024UL * 1024UL;

        internal static FileSystemDirectoryInspection Inspect(byte* image,
                ulong imageSizeBytes, string path) {
            if (!FileSystemDirectoryInspection.IsValidRelativePath(path)) {
                return Result(FileSystemDirectoryInspectionStatus.InvalidPath);
            }
            if (image == null) {
                return Result(
                    FileSystemDirectoryInspectionStatus.FilesystemUnavailable);
            }
            if (!IsAscii(path)) {
                // RdskFS's existing reader accepts ASCII path bytes only.
                return Result(FileSystemDirectoryInspectionStatus.Indeterminate);
            }
            if (imageSizeBytes < 12 || !HasRange(0, 12, imageSizeBytes) ||
                    image[0] != (byte)'R' || image[1] != (byte)'D' ||
                    image[2] != (byte)'S' || image[3] != (byte)'K' ||
                    ReadUInt32(image, 4) != 1) {
                return Result(FileSystemDirectoryInspectionStatus.Indeterminate);
            }

            uint fileCount = ReadUInt32(image, 8);
            if (fileCount > MaxFileCount) {
                return Result(FileSystemDirectoryInspectionStatus.Indeterminate);
            }

            int targetLength = path.Length;
            bool exactFile = false;
            bool descendantFile = false;
            bool ancestorFile = false;
            ulong offset = 12;

            for (uint i = 0; i < fileCount; i++) {
                if (!HasBoundedRange(offset, 2, imageSizeBytes)) {
                    return Result(
                        FileSystemDirectoryInspectionStatus.Indeterminate);
                }

                ushort pathLength = ReadUInt16(image, offset);
                offset += 2;
                if (pathLength == 0 || pathLength > 512 ||
                        !HasBoundedRange(offset, pathLength, imageSizeBytes) ||
                        !IsCanonicalRecordPath(image, offset, pathLength)) {
                    return Result(
                        FileSystemDirectoryInspectionStatus.Indeterminate);
                }

                if (MatchesExactPath(image, offset, pathLength, path)) {
                    exactFile = true;
                } else if (IsDescendantPath(image, offset, pathLength,
                               path)) {
                    descendantFile = true;
                } else if (IsAncestorFile(image, offset, pathLength, path)) {
                    ancestorFile = true;
                }

                offset += pathLength;
                if (!HasBoundedRange(offset, 4, imageSizeBytes)) {
                    return Result(
                        FileSystemDirectoryInspectionStatus.Indeterminate);
                }

                uint dataLength = ReadUInt32(image, offset);
                offset += 4;
                if (dataLength > MaxFileBytes ||
                        !HasBoundedRange(offset, dataLength, imageSizeBytes)) {
                    return Result(
                        FileSystemDirectoryInspectionStatus.Indeterminate);
                }
                offset += dataLength;
            }

            if (path.Length == 0) {
                return Result(FileSystemDirectoryInspectionStatus.Directory);
            }
            if ((exactFile && descendantFile) ||
                    (ancestorFile && descendantFile)) {
                // The image describes a file and a directory at the same
                // logical location. RDSK v1 has no rule for resolving this.
                return Result(FileSystemDirectoryInspectionStatus.Indeterminate);
            }
            if (descendantFile) {
                return Result(FileSystemDirectoryInspectionStatus.Directory);
            }
            if (exactFile || ancestorFile) {
                return Result(FileSystemDirectoryInspectionStatus.NotDirectory);
            }
            return Result(FileSystemDirectoryInspectionStatus.NotFound);
        }

        private static bool HasRange(ulong offset, ulong length,
                                     ulong imageSizeBytes) {
            return offset <= imageSizeBytes &&
                length <= imageSizeBytes - offset;
        }

        private static bool HasBoundedRange(ulong offset, ulong length,
                                            ulong imageSizeBytes) {
            return offset <= MaxImageOffset &&
                length <= MaxImageOffset - offset &&
                HasRange(offset, length, imageSizeBytes);
        }

        private static ushort ReadUInt16(byte* image, ulong offset) {
            int at = (int)offset;
            return (ushort)(image[at] | (image[at + 1] << 8));
        }

        private static uint ReadUInt32(byte* image, ulong offset) {
            int at = (int)offset;
            return (uint)image[at] | ((uint)image[at + 1] << 8) |
                ((uint)image[at + 2] << 16) |
                ((uint)image[at + 3] << 24);
        }

        private static bool IsAscii(string path) {
            for (int i = 0; i < path.Length; i++) {
                if (path[i] > '\u007f') return false;
            }
            return true;
        }

        private static bool IsCanonicalRecordPath(byte* image, ulong offset,
                                                   ushort length) {
            int segmentStart = 0;
            for (int i = 0; i <= length; i++) {
                if (i < length) {
                    byte value = image[(int)(offset + (ulong)i)];
                    if (value < 0x20 || value > 0x7e || value == (byte)'\\')
                        return false;
                    if (value != (byte)'/') continue;
                }

                int segmentLength = i - segmentStart;
                if (segmentLength == 0 ||
                        (segmentLength == 1 &&
                         image[(int)(offset + (ulong)segmentStart)] ==
                            (byte)'.') ||
                        (segmentLength == 2 &&
                         image[(int)(offset + (ulong)segmentStart)] ==
                            (byte)'.' &&
                         image[(int)(offset + (ulong)segmentStart + 1)] ==
                            (byte)'.')) return false;
                segmentStart = i + 1;
            }
            return true;
        }

        private static bool MatchesExactPath(byte* image, ulong offset,
                ushort pathLength, string path) {
            if (pathLength != path.Length) return false;
            for (int i = 0; i < path.Length; i++) {
                if (image[(int)(offset + (ulong)i)] != (byte)path[i])
                    return false;
            }
            return true;
        }

        private static bool IsDescendantPath(byte* image, ulong offset,
                ushort pathLength, string path) {
            int pathLengthChars = path.Length;
            if (pathLengthChars == 0 || pathLength <= pathLengthChars ||
                    image[(int)(offset + (ulong)pathLengthChars)] !=
                        (byte)'/') return false;
            return PrefixMatches(image, offset, path);
        }

        private static bool IsAncestorFile(byte* image, ulong offset,
                ushort pathLength, string path) {
            int entryLength = pathLength;
            if (entryLength >= path.Length ||
                    path[entryLength] != '/') return false;
            for (int i = 0; i < entryLength; i++) {
                if (image[(int)(offset + (ulong)i)] != (byte)path[i])
                    return false;
            }
            return true;
        }

        private static bool PrefixMatches(byte* image, ulong offset,
                                          string prefix) {
            for (int i = 0; i < prefix.Length; i++) {
                if (image[(int)(offset + (ulong)i)] != (byte)prefix[i])
                    return false;
            }
            return true;
        }

        private static FileSystemDirectoryInspection Result(
                FileSystemDirectoryInspectionStatus status) {
            return FileSystemDirectoryInspection.Create(status);
        }
    }
}
