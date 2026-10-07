using guideXOS.FS;
using guideXOS.Misc;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System.Collections.Generic;

namespace guideXOS.OS {
    internal enum PersistentFixtureStatus {
        Unavailable,
        Seeded,
        Verified,
        Mismatch,
        Failed
    }

    /// <summary>
    /// The only production Persistent volume accepted by this phase. This is
    /// the explicitly configured Phase 35Q diagnostic AHCI image, not a
    /// first-writable-disk policy.
    /// </summary>
    internal sealed class PersistentFatBackend {
        // Both root components fit the FAT 8.3 namespace used by this phase's
        // mounted filesystem implementation.
        internal const string Root = "apps/persist";
        internal const string FixtureApplicationId = "selftest.phase10.persistent";
        internal const string FixturePath = "state.bin";
        internal const int MaxValueLength = 64 * 1024;
        internal const int MaxEntriesPerApplication = 64;

        private const string ExpectedSerial = "GX35Q0001";
        private const ulong ExpectedBlockCount = 32768UL;
        private const uint ExpectedBlockSize = 512U;
        private const uint ExpectedVolumeId = 0x35355131U;
        private const string ExpectedVolumeLabel = "GX35Q TEST";
        private const int MaxDirectoryEntries = 128;
        private const int MaxEnumerationNodes = 8192;

        private readonly FAT _fat;
        private readonly Disk _disk;
        private readonly bool _identityMatched;
        private int _namespaceDerivations;
        private int _seedWritesPerformed;
        private string _verifiedFixtureSha256 = string.Empty;
        private string _fixtureDiagnostic = "not-run";
        private string _lastMutationDiagnostic = "none";

        internal bool IsAvailable {
            get { return _fat != null && _fat.IsMounted && _identityMatched &&
                _disk != null && _disk.IsAvailable; }
        }
        internal bool CanRead {
            get { return IsAvailable && _disk != null &&
                (_disk.Capabilities & DiskCapabilities.Readable) != 0 &&
                _disk.IsAvailable; }
        }
        internal bool CanMutate {
            get { return CanRead && _disk != null &&
                (_disk.Capabilities & DiskCapabilities.Writable) != 0 &&
                (_disk.Capabilities & DiskCapabilities.FlushSupported) != 0; }
        }
        internal string SelectedSerial {
            get { return IsAvailable ? ExpectedSerial : string.Empty; }
        }
        internal string Filesystem {
            get { return IsAvailable ? _fat.FileSystemVariant : string.Empty; }
        }
        internal string VolumeLabel {
            get { return IsAvailable ? _fat.VolumeLabel : string.Empty; }
        }
        internal uint VolumeId {
            get { return IsAvailable ? _fat.VolumeId : 0; }
        }
        internal int NamespaceDerivations { get { return _namespaceDerivations; } }
        internal int SeedWritesPerformed { get { return _seedWritesPerformed; } }
        internal string VerifiedFixtureSha256 {
            get { return _verifiedFixtureSha256; }
        }
        internal string FixtureDiagnostic { get { return _fixtureDiagnostic; } }
        internal string LastMutationDiagnostic { get { return _lastMutationDiagnostic; } }

        private PersistentFatBackend(Disk disk, bool identityMatched) {
            _disk = disk;
            _identityMatched = identityMatched;
            if (disk != null) _fat = new FAT(disk, false);
        }

        internal static PersistentFatBackend OpenSelectedVolume() {
            if (SATA.Ports == null) return new PersistentFatBackend(null, false);
            SATA.SATADevice selected = null;
            int matchingSerials = 0;
            for (int i = 0; i < SATA.Ports.Count; i++) {
                SATA.SATADevice candidate = SATA.Ports[i];
                if (candidate == null || candidate.Serial != ExpectedSerial) continue;
                matchingSerials++;
                selected = candidate;
            }
            if (matchingSerials != 1 || selected == null ||
                    selected.BlockCount != ExpectedBlockCount ||
                    selected.BlockSize != ExpectedBlockSize ||
                    !selected.IsAvailable ||
                    (selected.Capabilities & DiskCapabilities.Readable) == 0) {
                return new PersistentFatBackend(null, false);
            }

            PersistentFatBackend backend = new PersistentFatBackend(selected, true);
            if (backend._fat == null || !backend._fat.IsMounted ||
                    backend._fat.FileSystemVariant != "FAT16" ||
                    backend._fat.VolumeId != ExpectedVolumeId ||
                    backend._fat.VolumeLabel != ExpectedVolumeLabel) {
                return new PersistentFatBackend(selected, false);
            }
            return new PersistentFatBackend(selected, true);
        }

        /// <summary>Test-only factory for a fault-injecting Disk proxy.</summary>
        internal static PersistentFatBackend OpenForTesting(Disk disk) {
            if (disk == null) return new PersistentFatBackend(null, false);
            PersistentFatBackend backend = new PersistentFatBackend(disk, true);
            if (backend._fat == null || !backend._fat.IsMounted ||
                    (disk.Capabilities & DiskCapabilities.Readable) == 0) {
                return new PersistentFatBackend(null, false);
            }
            return backend;
        }

        internal static bool ApplicationIdEncodingIsInjective() {
            string[] ids = new string[] {
                "gxos.builtin.calculator",
                "gxos.builtin.files",
                "gxos.builtin.notepad",
                "gxos.builtin.diskmanager",
                "gxos.builtin.console",
                "selftest.phase10.persistent",
                "app!#$%&'()+,-.;=@[]^_`{}~",
                "id/with\\path:and\u0000control",
                "a", "aa", "ab", "aA", "Aa",
                "CaseSensitive", "casesensitive",
                "unicode.\u03B1", "unicode.\u03B2",
                "emoji.\uD83D\uDE80", "unpaired.\uD800",
                Repeat('M', ApplicationServiceContext.MaxApplicationIdLength),
                Repeat('M', ApplicationServiceContext.MaxApplicationIdLength - 1) + "N"
            };
            for (int i = 0; i < ids.Length; i++) {
                string mapped = GetApplicationDirectory(ids[i]);
                if (mapped != GetApplicationDirectory(ids[i])) return false;
                if (!IsFATShortPath(mapped)) return false;
                for (int j = 0; j < i; j++) {
                    if (mapped == GetApplicationDirectory(ids[j])) return false;
                }
            }
            if (!IsFATShortPath(GetValueFilePath(
                    FixtureApplicationId, FixturePath))) return false;
            return true;
        }

        private static bool IsFATShortPath(string path) {
            if (string.IsNullOrEmpty(path)) return false;
            int baseLength = 0;
            int extensionLength = 0;
            bool inExtension = false;
            for (int i = 0; i <= path.Length; i++) {
                if (i == path.Length || path[i] == '/') {
                    if (baseLength <= 0 || baseLength > 8 ||
                            extensionLength > 3) return false;
                    baseLength = 0;
                    extensionLength = 0;
                    inExtension = false;
                    continue;
                }
                char c = path[i];
                if (c == '.' && !inExtension && baseLength > 0) {
                    inExtension = true;
                    continue;
                }
                if (!((c >= 'A' && c <= 'Z') ||
                        (c >= 'a' && c <= 'z') ||
                        (c >= '0' && c <= '9'))) return false;
                if (inExtension) extensionLength++;
                else baseLength++;
            }
            return true;
        }

        internal PersistentFixtureStatus EnsureTrustedFixture() {
            if (!IsAvailable) {
                _fixtureDiagnostic = "backend-unavailable";
                return PersistentFixtureStatus.Unavailable;
            }
            byte[] expected = PersistentStorageFixture.CreateBytes();
            uint length;
            FatOperationResult result = TryGetValueLength(
                FixtureApplicationId, FixturePath, out length);
            if (result == FatOperationResult.NotFound) {
                _seedWritesPerformed++;
                result = TryWriteValue(FixtureApplicationId, FixturePath, expected);
                if (result != FatOperationResult.Success) {
                    _fixtureDiagnostic = "seed-write-result=" +
                        ((byte)result).ToString() + ";detail=" +
                        _lastMutationDiagnostic;
                    return PersistentFixtureStatus.Failed;
                }
                byte[] seeded;
                int bytesRead;
                bool end;
                result = TryReadValue(FixtureApplicationId, FixturePath, 0,
                    MaxValueLength, out seeded, out bytesRead, out end);
                string seededSha256 = result == FatOperationResult.Success
                    ? PersistentStorageFixture.ComputeSha256(seeded) : string.Empty;
                bool seededCorrect = result == FatOperationResult.Success && end &&
                    bytesRead == expected.Length &&
                    PersistentStorageFixture.Equal(expected, seeded) &&
                    seededSha256 == PersistentStorageFixture.Sha256;
                if (seededCorrect) _verifiedFixtureSha256 = seededSha256;
                _fixtureDiagnostic = seededCorrect ? "seeded-verified" :
                    "seed-read-result=" + ((byte)result).ToString() +
                    ";bytes=" + bytesRead.ToString() +
                    ";end=" + (end ? "1" : "0") +
                    ";sha=" + seededSha256;
                return seededCorrect ? PersistentFixtureStatus.Seeded
                    : PersistentFixtureStatus.Failed;
            }
            if (result != FatOperationResult.Success || length != expected.Length) {
                _fixtureDiagnostic = "fixture-length-result=" +
                    ((byte)result).ToString() + ";length=" + length.ToString();
                return result == FatOperationResult.Success
                    ? PersistentFixtureStatus.Mismatch
                    : PersistentFixtureStatus.Failed;
            }

            byte[] actual;
            int count;
            bool atEnd;
            result = TryReadValue(FixtureApplicationId, FixturePath, 0,
                MaxValueLength, out actual, out count, out atEnd);
            if (result != FatOperationResult.Success) {
                _fixtureDiagnostic = "fixture-read-result=" +
                    ((byte)result).ToString();
                return PersistentFixtureStatus.Failed;
            }
            string actualSha256 = PersistentStorageFixture.ComputeSha256(actual);
            bool verified = atEnd && count == expected.Length &&
                PersistentStorageFixture.Equal(expected, actual) &&
                actualSha256 == PersistentStorageFixture.Sha256;
            if (verified) _verifiedFixtureSha256 = actualSha256;
            _fixtureDiagnostic = verified ? "verified" :
                "fixture-mismatch;bytes=" + count.ToString() +
                ";end=" + (atEnd ? "1" : "0") + ";sha=" + actualSha256;
            return verified ? PersistentFixtureStatus.Verified
                : PersistentFixtureStatus.Mismatch;
        }

        internal static string FixtureStatusName(PersistentFixtureStatus status) {
            switch (status) {
                case PersistentFixtureStatus.Unavailable: return "Unavailable";
                case PersistentFixtureStatus.Seeded: return "Seeded";
                case PersistentFixtureStatus.Verified: return "Verified";
                case PersistentFixtureStatus.Mismatch: return "Mismatch";
                case PersistentFixtureStatus.Failed: return "Failed";
                default: return "Unknown";
            }
        }

        internal FatOperationResult TryGetValueLength(string applicationId,
                string relativePath, out uint length) {
            length = 0;
            if (!CanRead) return FatOperationResult.MediaUnavailable;
            if (!IsValidIdentity(applicationId) ||
                    !ApplicationStoragePathRules.IsValid(relativePath))
                return FatOperationResult.InvalidPath;
            _namespaceDerivations++;
            string filePath = GetValueFilePath(applicationId, relativePath);
            try {
                return _fat.TryGetFileLength(filePath, out length);
            } finally {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
                Allocator.CurrentDiagnosticStringSite = 202;
#endif
                filePath.Dispose();
#if UEFI_DIAGNOSTIC_RING3_PHASE35
                Allocator.CurrentDiagnosticStringSite = 0;
#endif
            }
        }

        internal FatOperationResult TryReadValue(string applicationId,
                string relativePath, long offset, int maximumBytes,
                out byte[] bytes, out int bytesRead, out bool endOfValue) {
            bytes = null;
            bytesRead = 0;
            endOfValue = false;
            if (!CanRead) return FatOperationResult.MediaUnavailable;
            if (!IsValidIdentity(applicationId) ||
                    !ApplicationStoragePathRules.IsValid(relativePath) ||
                    offset < 0 || maximumBytes <= 0 || maximumBytes > MaxValueLength)
                return FatOperationResult.InvalidRange;

            _namespaceDerivations++;
            string filePath = GetValueFilePath(applicationId, relativePath);
            try {
            uint fileLength;
            Ring3Abi.Phase35DiagnosticOwnerBytes(
                "PHASE35_DIAG_READ_BEFORE_FAT_LENGTH=0x");
            FatOperationResult result = _fat.TryGetFileLength(filePath,
                out fileLength);
            Ring3Abi.Phase35DiagnosticOwnerBytes(
                "PHASE35_DIAG_READ_AFTER_FAT_LENGTH=0x");
            if (result != FatOperationResult.Success) {
                Ring3Abi.Phase35DiagnosticMarker(
                    "P35_DIAG_FAT_LENGTH_FAILED=1");
                Ring3Abi.Phase35DiagnosticValueMarker(
                    "P35_DIAG_FAT_LENGTH_RESULT=", (ulong)(byte)result);
                Ring3Abi.Phase35DiagnosticValueMarker(
                    "P35_DIAG_FAT_LENGTH_VALUE=", fileLength);
                Ring3Abi.Phase35DiagnosticMarker(
                    "P35_DIAG_APPLICATION_ID=" + applicationId);
                Ring3Abi.Phase35DiagnosticMarker(
                    "P35_DIAG_RELATIVE_PATH=" + relativePath);
                Ring3Abi.Phase35DiagnosticMarker(
                    "P35_DIAG_FILE_PATH=" + filePath);
                return result;
            }
            if (fileLength > MaxValueLength) return FatOperationResult.EntryLimitExceeded;
            if (offset > fileLength) return FatOperationResult.InvalidRange;
            if (offset == fileLength) {
                bytes = new byte[0];
                endOfValue = true;
                return FatOperationResult.Success;
            }

            uint remaining = fileLength - (uint)offset;
            bytesRead = remaining > (uint)maximumBytes
                ? maximumBytes : (int)remaining;
            bytes = new byte[bytesRead];
            int actualBytesRead;
            Ring3Abi.Phase35DiagnosticOwnerBytes(
                "PHASE35_DIAG_READ_BEFORE_FAT_RANGE=0x");
            result = _fat.TryReadRange(filePath, offset, bytes, 0, bytesRead,
                out actualBytesRead, out endOfValue);
            Ring3Abi.Phase35DiagnosticOwnerBytes(
                "PHASE35_DIAG_READ_AFTER_FAT_RANGE=0x");
            if (result != FatOperationResult.Success) {
                Ring3Abi.Phase35DiagnosticMarker(
                    "P35_DIAG_FAT_RANGE_FAILED=1");
                Ring3Abi.Phase35DiagnosticValueMarker(
                    "P35_DIAG_FAT_RANGE_RESULT=", (ulong)(byte)result);
                Ring3Abi.Phase35DiagnosticValueMarker(
                    "P35_DIAG_FAT_RANGE_BYTES=", (ulong)(uint)actualBytesRead);
                Ring3Abi.Phase35DiagnosticValueMarker(
                    "P35_DIAG_FAT_FILE_LENGTH=", fileLength);
                Ring3Abi.Phase35DiagnosticMarker(
                    "P35_DIAG_APPLICATION_ID=" + applicationId);
                Ring3Abi.Phase35DiagnosticMarker(
                    "P35_DIAG_RELATIVE_PATH=" + relativePath);
                Ring3Abi.Phase35DiagnosticMarker(
                    "P35_DIAG_FILE_PATH=" + filePath);
                return result;
            }
            if (actualBytesRead != bytesRead)
                return FatOperationResult.ReadFailure;
            return FatOperationResult.Success;
            } finally {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
                Allocator.CurrentDiagnosticStringSite = 202;
#endif
                filePath.Dispose();
#if UEFI_DIAGNOSTIC_RING3_PHASE35
                Allocator.CurrentDiagnosticStringSite = 0;
#endif
            }
        }

        internal FatOperationResult TryWriteValue(string applicationId,
                string relativePath, byte[] value) {
            if (!CanRead) return FatOperationResult.MediaUnavailable;
            if (!CanMutate) return (_disk.Capabilities & DiskCapabilities.Writable) == 0
                ? FatOperationResult.ReadOnly : FatOperationResult.FlushUnsupported;
            if (!IsValidIdentity(applicationId) ||
                    !ApplicationStoragePathRules.IsValid(relativePath) ||
                    value == null || value.Length > MaxValueLength)
                return FatOperationResult.InvalidRange;

            _namespaceDerivations++;
            string directory = GetValueDirectory(applicationId, relativePath);
            string filePath = directory + "/VALUE.BIN";
            FatOperationResult result = _fat.CreateDirectory(directory);
            if (result != FatOperationResult.Success) {
                _lastMutationDiagnostic = "create-directory=" +
                    ((byte)result).ToString() + ";componentIndex=" +
                    _fat.LastCreateDirectoryFailureIndex.ToString() +
                    ";componentCount=" + _fat.LastCreateDirectoryPartCount.ToString();
                FatOperationResult syncAfterFailure = _fat.TrySync();
                return syncAfterFailure == FatOperationResult.Success
                    ? result : syncAfterFailure;
            }
            List<FileInfo> parentEntries;
            FatOperationResult parentResult = _fat.TryGetFiles(directory,
                MaxDirectoryEntries, out parentEntries);
            if (parentResult != FatOperationResult.Success) {
                _lastMutationDiagnostic = "create-directory-verify=" +
                    ((byte)parentResult).ToString();
                return parentResult;
            }
            DisposeEntries(parentEntries);
            byte[] copy = CopyBytes(value);
            result = _fat.TryWriteAllBytes(filePath, copy);
            if (result != FatOperationResult.Success) {
                _lastMutationDiagnostic = "write-file=" +
                    ((byte)result).ToString();
                FatOperationResult syncAfterFailure = _fat.TrySync();
                return syncAfterFailure == FatOperationResult.Success
                    ? result : syncAfterFailure;
            }
            result = _fat.TrySync();
            _lastMutationDiagnostic = result == FatOperationResult.Success
                ? "write-sync=success" : "write-sync=" + ((byte)result).ToString();
            return result;
        }

        internal FatOperationResult TryDeleteValue(string applicationId,
                string relativePath) {
            if (!CanRead) return FatOperationResult.MediaUnavailable;
            if (!CanMutate) return (_disk.Capabilities & DiskCapabilities.Writable) == 0
                ? FatOperationResult.ReadOnly : FatOperationResult.FlushUnsupported;
            if (!IsValidIdentity(applicationId) ||
                    !ApplicationStoragePathRules.IsValid(relativePath))
                return FatOperationResult.InvalidPath;
            _namespaceDerivations++;
            string filePath = GetValueFilePath(applicationId, relativePath);
            uint length;
            FatOperationResult result = _fat.TryGetFileLength(filePath, out length);
            if (result != FatOperationResult.Success) return result;
            result = _fat.TryDelete(filePath);
            if (result != FatOperationResult.Success) return result;
            return _fat.TrySync();
        }

        internal FatOperationResult TryEnumerate(string applicationId,
                out ApplicationStorageEntry[] entries) {
            entries = null;
            if (!CanRead) return FatOperationResult.MediaUnavailable;
            if (!IsValidIdentity(applicationId)) return FatOperationResult.InvalidPath;
            _namespaceDerivations++;
            string appRoot = GetApplicationDirectory(applicationId);
            List<FileInfo> roots;
            FatOperationResult result = _fat.TryGetFiles(appRoot,
                MaxDirectoryEntries, out roots);
            if (result == FatOperationResult.NotFound) {
                entries = new ApplicationStorageEntry[0];
                return FatOperationResult.Success;
            }
            if (result != FatOperationResult.Success) return result;

            List<ApplicationStorageEntry> values =
                new List<ApplicationStorageEntry>();
            int visited = 1;
            try {
                for (int i = 0; i < roots.Count; i++) {
                    FileInfo rootEntry = roots[i];
                    int valueLength;
                    if ((rootEntry.Attribute & FileAttribute.Directory) == 0 ||
                            !TryParseLengthDirectory(rootEntry.Name, 'P', out valueLength) ||
                            valueLength <= 0 || valueLength >
                                ApplicationStorageRequest.MaxRelativePathLength) continue;
                    char[] decoded = new char[valueLength];
                    result = EnumerateEncodedPath(appRoot + "/" + rootEntry.Name,
                        valueLength, 0, decoded, values, ref visited);
                    if (result != FatOperationResult.Success) return result;
                }
            } finally {
                DisposeEntries(roots);
            }

            SortEntries(values);
            entries = values.ToArray();
            return FatOperationResult.Success;
        }

        private FatOperationResult EnumerateEncodedPath(string directory,
                int valueLength, int groupIndex, char[] decoded,
                List<ApplicationStorageEntry> values, ref int visited) {
            int groups = (valueLength + 1) / 2;
            if (groupIndex == groups) {
                uint fileLength;
                FatOperationResult fileResult = _fat.TryGetFileLength(
                    directory + "/VALUE.BIN", out fileLength);
                if (fileResult == FatOperationResult.NotFound) return FatOperationResult.Success;
                if (fileResult != FatOperationResult.Success) return fileResult;
                if (fileLength > MaxValueLength || values.Count >= MaxEntriesPerApplication)
                    return FatOperationResult.EntryLimitExceeded;
                string relativePath = new string(decoded);
                if (!ApplicationStoragePathRules.IsValid(relativePath))
                    return FatOperationResult.InvalidPath;
                values.Add(ApplicationStorageEntry.Create(relativePath, fileLength));
                return FatOperationResult.Success;
            }

            if (++visited > MaxEnumerationNodes)
                return FatOperationResult.EntryLimitExceeded;
            List<FileInfo> children;
            FatOperationResult result = _fat.TryGetFiles(directory,
                MaxDirectoryEntries, out children);
            if (result != FatOperationResult.Success) return result;
            try {
                for (int i = 0; i < children.Count; i++) {
                    FileInfo child = children[i];
                    if ((child.Attribute & FileAttribute.Directory) == 0) continue;
                    ushort[] codeUnits = new ushort[2];
                    if (!TryDecodeChunk(child.Name, codeUnits)) continue;
                    int start = groupIndex * 2;
                    for (int j = 0; j < 2 && start + j < valueLength; j++)
                        decoded[start + j] = (char)codeUnits[j];
                    result = EnumerateEncodedPath(directory + "/" + child.Name,
                        valueLength, groupIndex + 1, decoded, values, ref visited);
                    if (result != FatOperationResult.Success) return result;
                }
            } finally {
                DisposeEntries(children);
            }
            return FatOperationResult.Success;
        }

        private static string GetValueFilePath(string applicationId, string relativePath) {
            int length = ValueDirectoryLength(applicationId, relativePath) + 10;
            char[] path = new char[length];
            int offset = WriteValueDirectory(path, 0, applicationId,
                relativePath);
            offset = CopyText("/VALUE.BIN", path, offset);
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            uint previousSite = Allocator.CurrentDiagnosticStringSite;
            Allocator.CurrentDiagnosticStringSite = 201;
#endif
            string valuePath = new string(path, 0, offset);
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            Allocator.CurrentDiagnosticStringSite = previousSite;
#endif
            path.Dispose();
            return valuePath;
        }

        private static string GetValueDirectory(string applicationId, string relativePath) {
            char[] path = new char[ValueDirectoryLength(applicationId,
                relativePath)];
            int offset = WriteValueDirectory(path, 0, applicationId,
                relativePath);
            return new string(path, 0, offset);
        }

        private static string GetApplicationDirectory(string applicationId) {
            char[] path = new char[ApplicationDirectoryLength(applicationId)];
            int offset = WriteApplicationDirectory(path, 0, applicationId);
            return new string(path, 0, offset);
        }

        private static int ApplicationDirectoryLength(string applicationId) {
            return Root.Length + 1 + 5 + EncodedUtf16Length(applicationId.Length);
        }

        private static int ValueDirectoryLength(string applicationId,
                string relativePath) {
            return ApplicationDirectoryLength(applicationId) + 1 + 5 +
                EncodedUtf16Length(relativePath.Length);
        }

        private static int EncodedUtf16Length(int codeUnitCount) {
            return ((codeUnitCount + 1) / 2) * 9;
        }

        private static int WriteValueDirectory(char[] destination, int offset,
                string applicationId, string relativePath) {
            offset = WriteApplicationDirectory(destination, offset,
                applicationId);
            destination[offset++] = '/';
            offset = WriteLengthDirectory(destination, offset, 'P',
                relativePath.Length);
            return WriteUtf16Chunks(destination, offset, relativePath);
        }

        private static int WriteApplicationDirectory(char[] destination,
                int offset, string applicationId) {
            offset = CopyText(Root, destination, offset);
            destination[offset++] = '/';
            offset = WriteLengthDirectory(destination, offset, 'A',
                applicationId.Length);
            return WriteUtf16Chunks(destination, offset, applicationId);
        }

        private static int WriteLengthDirectory(char[] destination, int offset,
                char prefix, int length) {
            destination[offset++] = prefix;
            for (int shift = 12; shift >= 0; shift -= 4)
                destination[offset++] = HexDigit((length >> shift) & 0xF);
            return offset;
        }

        private static int WriteUtf16Chunks(char[] destination, int offset,
                string value) {
            for (int start = 0; start < value.Length; start += 2) {
                destination[offset++] = '/';
                for (int unit = 0; unit < 2; unit++) {
                    int index = start + unit;
                    ushort codeUnit = index < value.Length ? value[index] : (ushort)0;
                    destination[offset++] = HexDigit((codeUnit >> 12) & 0xF);
                    destination[offset++] = HexDigit((codeUnit >> 8) & 0xF);
                    destination[offset++] = HexDigit((codeUnit >> 4) & 0xF);
                    destination[offset++] = HexDigit(codeUnit & 0xF);
                }
            }
            return offset;
        }

        private static int CopyText(string source, char[] destination,
                int offset) {
            for (int i = 0; i < source.Length; i++)
                destination[offset++] = source[i];
            return offset;
        }

        private static bool TryParseLengthDirectory(string name, char prefix,
                out int length) {
            length = 0;
            if (name == null || name.Length != 5 || name[0] != prefix) return false;
            for (int i = 1; i < name.Length; i++) {
                int digit = HexValue(name[i]);
                if (digit < 0) return false;
                length = (length << 4) | digit;
            }
            return true;
        }

        private static bool TryDecodeChunk(string name, ushort[] codeUnits) {
            if (name == null || name.Length != 8 || codeUnits == null ||
                    codeUnits.Length < 2) return false;
            for (int unit = 0; unit < 2; unit++) {
                int offset = unit * 4;
                int a = HexValue(name[offset]);
                int b = HexValue(name[offset + 1]);
                int c = HexValue(name[offset + 2]);
                int d = HexValue(name[offset + 3]);
                if (a < 0 || b < 0 || c < 0 || d < 0) return false;
                codeUnits[unit] = (ushort)((a << 12) | (b << 8) | (c << 4) | d);
            }
            return true;
        }

        private static char HexDigit(int value) {
            return (char)(value < 10 ? '0' + value : 'A' + value - 10);
        }

        private static int HexValue(char value) {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'A' && value <= 'F') return value - 'A' + 10;
            return -1;
        }

        private static bool IsValidIdentity(string applicationId) {
            return !string.IsNullOrEmpty(applicationId) &&
                applicationId.Length <= ApplicationServiceContext.MaxApplicationIdLength;
        }

        private static string Repeat(char value, int count) {
            string result = string.Empty;
            for (int i = 0; i < count; i++) result += value;
            return result;
        }

        private static byte[] CopyBytes(byte[] source) {
            if (source == null || source.Length == 0) return new byte[0];
            byte[] result = new byte[source.Length];
            for (int i = 0; i < source.Length; i++) result[i] = source[i];
            return result;
        }

        private static void DisposeEntries(List<FileInfo> entries) {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null) entries[i].Dispose();
        }

        private static void SortEntries(List<ApplicationStorageEntry> entries) {
            for (int i = 1; i < entries.Count; i++) {
                ApplicationStorageEntry value = entries[i];
                int j = i - 1;
                while (j >= 0 && CompareOrdinal(entries[j].RelativePath,
                        value.RelativePath) > 0) {
                    entries[j + 1] = entries[j];
                    j--;
                }
                entries[j + 1] = value;
            }
        }

        private static int CompareOrdinal(string left, string right) {
            int common = left.Length < right.Length ? left.Length : right.Length;
            for (int i = 0; i < common; i++) {
                if (left[i] < right[i]) return -1;
                if (left[i] > right[i]) return 1;
            }
            if (left.Length < right.Length) return -1;
            if (left.Length > right.Length) return 1;
            return 0;
        }
    }

    internal static unsafe class PersistentStorageFixture {
        internal const string Sha256 =
            "BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D";

        internal static byte[] CreateBytes() {
            byte[] value = new byte[32];
            for (int i = 0; i < value.Length; i++) value[i] = (byte)(0x35 + i);
            return value;
        }

        internal static string ComputeSha256(byte[] value) {
            if (value == null) return string.Empty;
            byte* digest = stackalloc byte[32];
            fixed (byte* data = value)
                SHA256.Compute(data, value.Length, digest);
            string lower = SHA256.ToHex(digest);
            char[] upper = new char[lower.Length];
            for (int i = 0; i < lower.Length; i++) {
                char c = lower[i];
                upper[i] = c >= 'a' && c <= 'f' ? (char)(c - 32) : c;
            }
            return new string(upper);
        }

        internal static bool Equal(byte[] left, byte[] right) {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i]) return false;
            return true;
        }
    }
}
