namespace guideXOS.FS {
    /// <summary>
    /// Bounded, version-aware inspection of RDSK v1 and v2 images. Version 1
    /// keeps its historical ASCII file-path interpretation. Version 2 uses
    /// explicit directory records and a deliberately restricted ASCII path
    /// subset that the UEFI CoreLib can decode without replacement behavior.
    /// </summary>
    internal static unsafe class RdskFSDirectoryInspector {
        internal const uint Version1 = 1;
        internal const uint Version2 = 2;

        internal const uint MaxV1FileCount = 10000;
        internal const uint MaxV2RecordCount = 4096;
        internal const uint MaxFileBytes = 100 * 1024 * 1024;
        internal const ulong MaxImageBytes = 512UL * 1024UL * 1024UL;
        internal const ushort MaxV1PathBytes = 512;
        internal const ushort MaxV2NameBytes = 255;
        internal const ushort MaxV2PathBytes = 1023;

        private const uint V2HeaderBytes = 64;
        private const uint V2RecordFixedBytes = 28;
        private const uint V2DirectoryRecord = 1;
        private const uint V2FileRecord = 2;
        private const uint RootParent = 0xffffffffU;
        private const ulong MaxV2RecordTableBytes = 4UL * 1024UL * 1024UL;

        /// <summary>
        /// Validates the complete declared image before any caller exposes a
        /// path, record, or payload. The returned version is nonzero only on
        /// success. V1 may contain unused trailing bytes for compatibility;
        /// v2 requires its declared image size to equal the captured size.
        /// </summary>
        internal static bool ValidateImage(byte* image, ulong imageSizeBytes,
                                           out uint version) {
            version = 0;
            if (image == null || imageSizeBytes < 8 ||
                    imageSizeBytes > MaxImageBytes ||
                    image[0] != (byte)'R' || image[1] != (byte)'D' ||
                    image[2] != (byte)'S' || image[3] != (byte)'K') {
                return false;
            }

            uint imageVersion = ReadUInt32(image, 4);
            if (imageVersion == Version1) {
                if (!ValidateV1(image, imageSizeBytes)) return false;
            } else if (imageVersion == Version2) {
                if (!ValidateV2(image, imageSizeBytes)) return false;
            } else {
                return false;
            }

            version = imageVersion;
            return true;
        }

        internal static FileSystemDirectoryInspection Inspect(byte* image,
                ulong imageSizeBytes, string path) {
            if (!FileSystemDirectoryInspection.IsValidRelativePath(path)) {
                return Result(FileSystemDirectoryInspectionStatus.InvalidPath);
            }
            if (image == null) {
                return Result(
                    FileSystemDirectoryInspectionStatus.FilesystemUnavailable);
            }

            uint version;
            if (!ValidateImage(image, imageSizeBytes, out version)) {
                return Result(FileSystemDirectoryInspectionStatus.Indeterminate);
            }
            if (!IsAscii(path)) {
                // Neither the legacy v1 reader nor the bounded v2 subset
                // silently decodes or replaces non-ASCII path characters.
                return Result(FileSystemDirectoryInspectionStatus.Indeterminate);
            }

            return version == Version1
                ? InspectV1(image, imageSizeBytes, path)
                : InspectV2(image, imageSizeBytes, path);
        }

        private static bool ValidateV1(byte* image, ulong imageSizeBytes) {
            if (imageSizeBytes < 12) return false;
            uint fileCount = ReadUInt32(image, 8);
            if (fileCount > MaxV1FileCount) return false;

            ulong offset = 12;
            for (uint i = 0; i < fileCount; i++) {
                if (!HasRange(offset, 2, imageSizeBytes)) return false;
                ushort pathLength = ReadUInt16(image, offset);
                offset += 2;
                if (pathLength == 0 || pathLength > MaxV1PathBytes ||
                        !HasRange(offset, pathLength, imageSizeBytes) ||
                        !IsCanonicalV1Path(image, offset, pathLength)) {
                    return false;
                }
                offset += pathLength;
                if (!HasRange(offset, 4, imageSizeBytes)) return false;
                uint dataLength = ReadUInt32(image, offset);
                offset += 4;
                if (dataLength > MaxFileBytes ||
                        !HasRange(offset, dataLength, imageSizeBytes)) {
                    return false;
                }
                offset += dataLength;
            }
            return true;
        }

        private static bool ValidateV2(byte* image, ulong imageSizeBytes) {
            // Do not read any version-2 field until the complete fixed header
            // is known to be within the captured image.
            if (imageSizeBytes < V2HeaderBytes ||
                    ReadUInt32(image, 8) != V2HeaderBytes ||
                    ReadUInt32(image, 12) != 0) return false;

            ulong declaredImageBytes = ReadUInt64(image, 16);
            uint recordCount = ReadUInt32(image, 24);
            uint directoryCount = ReadUInt32(image, 28);
            uint fileCount = ReadUInt32(image, 32);
            ulong recordsOffset = ReadUInt64(image, 36);
            ulong recordsBytes = ReadUInt64(image, 44);
            ulong dataOffset = ReadUInt64(image, 52);

            if (declaredImageBytes != imageSizeBytes ||
                    recordsOffset != V2HeaderBytes ||
                    recordCount == 0 || recordCount > MaxV2RecordCount ||
                    directoryCount == 0 || directoryCount > recordCount ||
                    fileCount != recordCount - directoryCount ||
                    recordsBytes < V2RecordFixedBytes ||
                    recordsBytes > MaxV2RecordTableBytes ||
                    recordsBytes > imageSizeBytes - recordsOffset ||
                    dataOffset != recordsOffset + recordsBytes ||
                    dataOffset > imageSizeBytes) {
                return false;
            }

            ulong recordsEnd = dataOffset;
            ulong cursor = recordsOffset;
            ulong nextDataOffset = dataOffset;
            uint seenDirectories = 0;
            uint seenFiles = 0;
            uint previousParent = 0;
            ushort previousNameLength = 0;
            ulong previousNameOffset = 0;
            byte* recordKinds = stackalloc byte[(int)MaxV2RecordCount];
            ushort* pathLengths = stackalloc ushort[(int)MaxV2RecordCount];

            for (uint i = 0; i < recordCount; i++) {
                if (cursor > recordsEnd ||
                        V2RecordFixedBytes > recordsEnd - cursor) {
                    return false;
                }

                byte kind = image[(int)cursor];
                byte flags = image[(int)(cursor + 1)];
                ushort nameLength = ReadUInt16(image, cursor + 2);
                uint parentIndex = ReadUInt32(image, cursor + 4);
                ulong fileOffset = ReadUInt64(image, cursor + 8);
                ulong fileLength = ReadUInt64(image, cursor + 16);
                uint reserved = ReadUInt32(image, cursor + 24);
                ulong nameOffset = cursor + V2RecordFixedBytes;

                if (flags != 0 || reserved != 0 ||
                        !HasRange(nameOffset, nameLength, recordsEnd)) {
                    return false;
                }

                if (i == 0) {
                    if (kind != V2DirectoryRecord || nameLength != 0 ||
                            parentIndex != RootParent || fileOffset != 0 ||
                            fileLength != 0) return false;
                    recordKinds[0] = (byte)kind;
                    pathLengths[0] = 0;
                    seenDirectories++;
                } else {
                    if ((kind != V2DirectoryRecord && kind != V2FileRecord) ||
                            nameLength == 0 || nameLength > MaxV2NameBytes ||
                            parentIndex >= i || recordKinds[parentIndex] !=
                                (byte)V2DirectoryRecord ||
                            parentIndex < previousParent ||
                            !IsValidV2Name(image, nameOffset, nameLength)) {
                        return false;
                    }

                    if (parentIndex == previousParent) {
                        if (CompareNames(image, previousNameOffset,
                                    previousNameLength, nameOffset,
                                    nameLength) >= 0) return false;
                    }

                    uint parentPathLength = pathLengths[parentIndex];
                    uint canonicalLength = parentPathLength + nameLength +
                        (parentIndex == 0 ? 0U : 1U);
                    if (canonicalLength > MaxV2PathBytes) return false;
                    pathLengths[i] = (ushort)canonicalLength;
                    recordKinds[i] = kind;
                    previousParent = parentIndex;
                    previousNameOffset = nameOffset;
                    previousNameLength = nameLength;

                    if (kind == V2DirectoryRecord) {
                        if (fileOffset != 0 || fileLength != 0) return false;
                        seenDirectories++;
                    } else {
                        if (fileLength > MaxFileBytes ||
                                fileOffset != nextDataOffset ||
                                nextDataOffset > imageSizeBytes ||
                                fileLength > imageSizeBytes - nextDataOffset) {
                            return false;
                        }
                        nextDataOffset += fileLength;
                        seenFiles++;
                    }
                }

                cursor = nameOffset + nameLength;
            }

            if (cursor != recordsEnd || nextDataOffset != imageSizeBytes ||
                    seenDirectories != directoryCount ||
                    seenFiles != fileCount) return false;

            return ComputeImageCrc32(image, imageSizeBytes) ==
                ReadUInt32(image, 60);
        }

        private static FileSystemDirectoryInspection InspectV1(byte* image,
                ulong imageSizeBytes, string path) {
            uint fileCount = ReadUInt32(image, 8);
            int targetLength = path.Length;
            bool exactFile = false;
            bool descendantFile = false;
            bool ancestorFile = false;
            ulong offset = 12;

            for (uint i = 0; i < fileCount; i++) {
                ushort pathLength = ReadUInt16(image, offset);
                offset += 2;
                if (MatchesExactPath(image, offset, pathLength, path)) {
                    exactFile = true;
                } else if (IsDescendantPath(image, offset, pathLength, path)) {
                    descendantFile = true;
                } else if (IsAncestorFile(image, offset, pathLength, path)) {
                    ancestorFile = true;
                }

                offset += pathLength;
                uint dataLength = ReadUInt32(image, offset);
                offset += 4 + dataLength;
            }

            if (path.Length == 0) {
                return Result(FileSystemDirectoryInspectionStatus.Directory);
            }
            if ((exactFile && descendantFile) ||
                    (ancestorFile && descendantFile)) {
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

        private static FileSystemDirectoryInspection InspectV2(byte* image,
                ulong imageSizeBytes, string path) {
            if (path.Length == 0) {
                return Result(FileSystemDirectoryInspectionStatus.Directory);
            }

            ulong recordsOffset = ReadUInt64(image, 36);
            uint recordCount = ReadUInt32(image, 24);
            uint parentIndex = 0;
            int segmentStart = 0;
            for (int i = 0; i <= path.Length; i++) {
                if (i < path.Length && path[i] != '/') continue;
                uint entryIndex;
                byte entryKind;
                if (!TryFindChild(image, recordsOffset, recordCount,
                        parentIndex, path, segmentStart, i - segmentStart,
                        out entryIndex, out entryKind)) {
                    return Result(FileSystemDirectoryInspectionStatus.NotFound);
                }

                bool isLast = i == path.Length;
                if (!isLast && entryKind != V2DirectoryRecord) {
                    return Result(
                        FileSystemDirectoryInspectionStatus.NotDirectory);
                }
                if (isLast) {
                    return Result(entryKind == V2DirectoryRecord
                        ? FileSystemDirectoryInspectionStatus.Directory
                        : FileSystemDirectoryInspectionStatus.NotDirectory);
                }
                parentIndex = entryIndex;
                segmentStart = i + 1;
            }
            return Result(FileSystemDirectoryInspectionStatus.Indeterminate);
        }

        /// <summary>Resolve one direct child in an already validated v2 image.</summary>
        internal static bool TryFindChild(byte* image, ulong recordsOffset,
                uint recordCount, uint parentIndex, string name,
                int nameStart, int nameLength, out uint childIndex,
                out byte childKind) {
            ulong cursor = recordsOffset;
            childIndex = 0;
            childKind = 0;
            for (uint i = 0; i < recordCount; i++) {
                byte kind = image[(int)cursor];
                ushort recordNameLength = ReadUInt16(image, cursor + 2);
                uint recordParent = ReadUInt32(image, cursor + 4);
                ulong nameOffset = cursor + V2RecordFixedBytes;
                if (i != 0 && recordParent == parentIndex &&
                        recordNameLength == nameLength &&
                        MatchesAsciiString(image, nameOffset, name,
                            nameStart, nameLength)) {
                    childIndex = i;
                    childKind = kind;
                    return true;
                }
                if (i != 0 && recordParent > parentIndex) return false;
                cursor = nameOffset + recordNameLength;
            }
            return false;
        }

        internal static bool IsAsciiPath(string path) {
            if (path == null) return false;
            for (int i = 0; i < path.Length; i++) {
                if (path[i] > '\u007f') return false;
            }
            return true;
        }

        internal static ushort ReadUInt16(byte* image, ulong offset) {
            int at = (int)offset;
            return (ushort)(image[at] | (image[at + 1] << 8));
        }

        internal static uint ReadUInt32(byte* image, ulong offset) {
            int at = (int)offset;
            return (uint)image[at] | ((uint)image[at + 1] << 8) |
                ((uint)image[at + 2] << 16) |
                ((uint)image[at + 3] << 24);
        }

        internal static ulong ReadUInt64(byte* image, ulong offset) {
            uint low = ReadUInt32(image, offset);
            uint high = ReadUInt32(image, offset + 4);
            return (ulong)low | ((ulong)high << 32);
        }

        private static bool HasRange(ulong offset, ulong length,
                                     ulong imageSizeBytes) {
            return offset <= imageSizeBytes &&
                length <= imageSizeBytes - offset;
        }

        private static uint ComputeImageCrc32(byte* image,
                                               ulong imageSizeBytes) {
            uint crc = 0xffffffffU;
            for (ulong i = 0; i < imageSizeBytes; i++) {
                uint value = i >= 60 && i < 64 ? 0U : image[(int)i];
                crc ^= value;
                for (int bit = 0; bit < 8; bit++) {
                    crc = (crc & 1U) != 0
                        ? (crc >> 1) ^ 0xedb88320U
                        : crc >> 1;
                }
            }
            return ~crc;
        }

        private static bool IsCanonicalV1Path(byte* image, ulong offset,
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
                        (segmentLength == 1 && image[(int)(offset +
                            (ulong)segmentStart)] == (byte)'.') ||
                        (segmentLength == 2 &&
                         image[(int)(offset + (ulong)segmentStart)] ==
                            (byte)'.' &&
                         image[(int)(offset + (ulong)segmentStart + 1)] ==
                            (byte)'.')) return false;
                segmentStart = i + 1;
            }
            return true;
        }

        private static bool IsValidV2Name(byte* image, ulong offset,
                                          ushort length) {
            if (length == 1 && image[(int)offset] == (byte)'.') return false;
            if (length == 2 && image[(int)offset] == (byte)'.' &&
                    image[(int)(offset + 1)] == (byte)'.') return false;
            for (int i = 0; i < length; i++) {
                byte value = image[(int)(offset + (ulong)i)];
                if (value < 0x20 || value > 0x7e || value == (byte)'/' ||
                        value == (byte)'\\') return false;
            }
            return true;
        }

        private static int CompareNames(byte* image, ulong leftOffset,
                ushort leftLength, ulong rightOffset, ushort rightLength) {
            int common = leftLength < rightLength ? leftLength : rightLength;
            for (int i = 0; i < common; i++) {
                byte left = image[(int)(leftOffset + (ulong)i)];
                byte right = image[(int)(rightOffset + (ulong)i)];
                if (left != right) return left < right ? -1 : 1;
            }
            if (leftLength == rightLength) return 0;
            return leftLength < rightLength ? -1 : 1;
        }

        private static bool MatchesAsciiString(byte* image, ulong offset,
                string value, int valueStart, int valueLength) {
            for (int i = 0; i < valueLength; i++) {
                if (image[(int)(offset + (ulong)i)] != value[valueStart + i])
                    return false;
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
            if (entryLength >= path.Length || path[entryLength] != '/')
                return false;
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

        private static bool IsAscii(string path) {
            for (int i = 0; i < path.Length; i++) {
                if (path[i] > '\u007f') return false;
            }
            return true;
        }

        private static FileSystemDirectoryInspection Result(
                FileSystemDirectoryInspectionStatus status) {
            return FileSystemDirectoryInspection.Create(status);
        }
    }
}
