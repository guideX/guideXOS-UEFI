using System;
using guideXOS.FS;
using guideXOS.Misc;

namespace guideXOS.OS {
    internal static class ApplicationResourceKeyRules {
        internal static bool IsValid(string value) {
            if (string.IsNullOrEmpty(value) ||
                    value.Length > ApplicationResourceRequest.MaxResourceKeyLength) {
                return false;
            }
            if (value == "." || value == "..") return false;
            for (int i = 0; i < value.Length; i++) {
                char c = value[i];
                if (c < 32 || c == 127 || c == '/' || c == '\\' ||
                        c == ':') return false;
                if (!IsAsciiLetter(c) && !IsAsciiDigit(c) && c != '.' &&
                        c != '-' && c != '_') return false;
            }
            return true;
        }

        private static bool IsAsciiLetter(char value) {
            return (value >= 'a' && value <= 'z') ||
                   (value >= 'A' && value <= 'Z');
        }

        private static bool IsAsciiDigit(char value) {
            return value >= '0' && value <= '9';
        }
    }

    internal static class ApplicationStoragePathRules {
        internal static bool IsValid(string value) {
            if (string.IsNullOrEmpty(value) ||
                    value.Length > ApplicationStorageRequest.MaxRelativePathLength) {
                return false;
            }
            if (value[0] == '/' || value[0] == '\\' ||
                    value.IndexOf(':') >= 0) return false;

            int segmentStart = 0;
            for (int i = 0; i <= value.Length; i++) {
                bool separator = i == value.Length || value[i] == '/' ||
                    value[i] == '\\';
                if (!separator) {
                    char c = value[i];
                    if (c < 32 || c == 127 || IsInvalidFileNameCharacter(c)) {
                        return false;
                    }
                    continue;
                }

                int segmentLength = i - segmentStart;
                if (segmentLength <= 0 ||
                        segmentLength > ApplicationStorageRequest.MaxPathSegmentLength) {
                    return false;
                }
                if (segmentLength == 1 && value[segmentStart] == '.') {
                    return false;
                }
                if (segmentLength == 2 && value[segmentStart] == '.' &&
                        value[segmentStart + 1] == '.') {
                    return false;
                }
                segmentStart = i + 1;
            }
            return true;
        }

        private static bool IsInvalidFileNameCharacter(char value) {
            return value == '<' || value == '>' || value == '"' ||
                   value == '|' || value == '?' || value == '*';
        }
    }

    internal sealed class CSharpApplicationStorageService :
            ApplicationStorageService {
        private const int MaxNamespaces = 32;
        private const int MaxEntriesPerNamespace = 64;

        private sealed class StorageEntry {
            internal string RelativePath;
            internal byte[] Bytes;
        }

        private sealed class StorageNamespace {
            internal string ApplicationId;
            internal readonly StorageEntry[] Entries =
                new StorageEntry[MaxEntriesPerNamespace];
            internal int Count;
        }

        private readonly StorageNamespace[] _namespaces =
            new StorageNamespace[MaxNamespaces];
        private readonly PersistentFatBackend _persistentBackend;
        private int _namespaceCount;
        private int _backendCallCount;
        private int _persistentExistsCount;
        private int _persistentReadCount;
        private int _persistentSuccessfulReadCount;
        private int _lastPersistentReadResultStage;
        private int _lastPersistentReadFatResult;
        private int _failedReadRequestCount;
        private int _persistentWriteCount;
        private int _persistentDeleteCount;
        private int _persistentEnumerateCount;
        private int _scopeRejectionCount;
        private int _staleContextRejectionCount;
        private int _storageIoFailureCount;
        private int _flushFailureCount;
        private PersistentFixtureStatus _fixtureStatus;

        internal CSharpApplicationStorageService() {
            _persistentBackend = PersistentFatBackend.OpenSelectedVolume();
            _fixtureStatus = _persistentBackend.EnsureTrustedFixture();
        }

        internal CSharpApplicationStorageService(
                PersistentFatBackend persistentBackend) {
            _persistentBackend = persistentBackend;
            _fixtureStatus = PersistentFixtureStatus.Unavailable;
        }

        internal int BackendCallCount { get { return _backendCallCount; } }
        internal bool PersistentBackendAvailable {
            get { return _persistentBackend != null && _persistentBackend.IsAvailable; }
        }
        internal bool PersistentBackendWritable {
            get { return _persistentBackend != null && _persistentBackend.CanMutate; }
        }
        internal string PersistentVolumeSerial {
            get { return _persistentBackend == null ? string.Empty :
                _persistentBackend.SelectedSerial; }
        }
        internal string PersistentFilesystem {
            get { return _persistentBackend == null ? string.Empty :
                _persistentBackend.Filesystem; }
        }
        internal string PersistentVolumeLabel {
            get { return _persistentBackend == null ? string.Empty :
                _persistentBackend.VolumeLabel; }
        }
        internal uint PersistentVolumeId {
            get { return _persistentBackend == null ? 0 :
                _persistentBackend.VolumeId; }
        }
        internal int PersistentSeedWritesPerformed {
            get { return _persistentBackend == null ? 0 :
                _persistentBackend.SeedWritesPerformed; }
        }
        internal string PersistentVerifiedFixtureSha256 {
            get { return _persistentBackend == null ? string.Empty :
                _persistentBackend.VerifiedFixtureSha256; }
        }
        internal PersistentFixtureStatus PersistentFixtureStatus {
            get { return _fixtureStatus; }
        }
        internal string PersistentFixtureStatusName {
            get { return PersistentFatBackend.FixtureStatusName(_fixtureStatus); }
        }
        internal string PersistentFixtureDiagnostic {
            get { return _persistentBackend == null ? "backend-null" :
                _persistentBackend.FixtureDiagnostic; }
        }
        internal string PersistentLastMutationDiagnostic {
            get { return _persistentBackend == null ? "backend-null" :
                _persistentBackend.LastMutationDiagnostic; }
        }
        internal int PersistentExistsCount { get { return _persistentExistsCount; } }
        internal int PersistentReadCount { get { return _persistentReadCount; } }
        internal int PersistentSuccessfulReadCount {
            get { return _persistentSuccessfulReadCount; }
        }
        internal int LastPersistentReadResultStage {
            get { return _lastPersistentReadResultStage; }
        }
        internal int LastPersistentReadFatResult {
            get { return _lastPersistentReadFatResult; }
        }
        internal int FailedReadRequestCount { get { return _failedReadRequestCount; } }
        internal int PersistentWriteCount { get { return _persistentWriteCount; } }
        internal int PersistentDeleteCount { get { return _persistentDeleteCount; } }
        internal int PersistentEnumerateCount { get { return _persistentEnumerateCount; } }
        internal int PersistentNamespaceDerivations {
            get { return _persistentBackend == null ? 0 :
                _persistentBackend.NamespaceDerivations; }
        }
        internal int ScopeRejectionCount { get { return _scopeRejectionCount; } }
        internal int StaleContextRejectionCount { get { return _staleContextRejectionCount; } }
        internal int StorageIoFailureCount { get { return _storageIoFailureCount; } }
        internal int FlushFailureCount { get { return _flushFailureCount; } }

        internal void RecordScopeRejectionDiagnostic() {
            _scopeRejectionCount++;
        }

        public override ApplicationServiceResult<bool> Exists(
                ApplicationServiceContext context,
                ApplicationStorageRequest request) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!TryValidate(context, out instance, out valid)) {
                return ApplicationServiceResult<bool>.Failure(
                    valid.Code, valid.BoundedDiagnostic);
            }
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult<bool>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Storage path is invalid or exceeds its bound");
            }
            if (request.Namespace == ApplicationStorageNamespace.Persistent) {
                _backendCallCount++;
                _persistentExistsCount++;
                uint length;
                FatOperationResult persistent = _persistentBackend == null
                    ? FatOperationResult.MediaUnavailable
                    : _persistentBackend.TryGetValueLength(context.ApplicationId,
                        request.RelativePath, out length);
                if (persistent == FatOperationResult.NotFound) {
                    if (_persistentBackend == null) return PersistentFailure<bool>(persistent);
                    return ApplicationServiceResult<bool>.SuccessResult(false);
                }
                if (persistent != FatOperationResult.Success)
                    return PersistentFailure<bool>(persistent);
                return ApplicationServiceResult<bool>.SuccessResult(true);
            }
            StorageNamespace storage = FindNamespace(context.ApplicationId);
            return ApplicationServiceResult<bool>.SuccessResult(
                storage != null && FindEntry(storage, request.RelativePath) != null);
        }

        public override ApplicationServiceResult<ApplicationStorageReadResult>
                Read(ApplicationServiceContext context,
                    ApplicationStorageReadRequest request) {
            _lastPersistentReadResultStage = -1;
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!TryValidate(context, out instance, out valid)) {
                _failedReadRequestCount++;
                ApplicationServiceResult<ApplicationStorageReadResult> failure =
                    ApplicationServiceResult<ApplicationStorageReadResult>.Failure(
                        valid.Code, valid.BoundedDiagnostic);
                if (valid != null) valid.Dispose();
                return failure;
            }
            if (valid != null) valid.Dispose();
            if (request == null || !request.IsValid) {
                _failedReadRequestCount++;
                return ApplicationServiceResult<ApplicationStorageReadResult>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Storage read request is invalid or exceeds its bound");
            }
            if (request.Namespace == ApplicationStorageNamespace.Persistent) {
                _backendCallCount++;
                _persistentReadCount++;
                _lastPersistentReadResultStage = 0;
                _lastPersistentReadFatResult = -1;
                byte[] persistentBytes = null;
                int persistentRead = 0;
                bool persistentEnd = false;
                FatOperationResult persistent = _persistentBackend == null
                    ? FatOperationResult.MediaUnavailable
                    : _persistentBackend.TryReadValue(context.ApplicationId,
                        request.RelativePath, request.Offset,
                        request.MaximumBytes, out persistentBytes,
                        out persistentRead, out persistentEnd);
                _lastPersistentReadFatResult = (int)persistent;
                if (persistent != FatOperationResult.Success)
                {
                    if (persistentBytes != null) persistentBytes.Dispose();
                    _failedReadRequestCount++;
                    return PersistentFailure<ApplicationStorageReadResult>(persistent);
                }
                _persistentSuccessfulReadCount++;
                _lastPersistentReadResultStage = 1;
                if (persistentBytes == null) {
                    Ring3Abi.Phase35DiagnosticMarker(
                        "P35_DIAG_PERSISTENT_SUCCESS_NULL_BYTES=1");
                    return ApplicationServiceResult<
                        ApplicationStorageReadResult>.Failure(
                            ApplicationServiceResultCode.BackendFailure,
                            "Persistent backend returned a null value");
                }
                ApplicationStorageReadResult readValue =
                    ApplicationStorageReadResult.Create(request.RelativePath,
                        request.Offset, persistentBytes, persistentRead,
                        persistentEnd);
                persistentBytes.Dispose();
                if (readValue == null) {
                    Ring3Abi.Phase35DiagnosticMarker(
                        "P35_DIAG_READ_VALUE_ALLOCATION_NULL=1");
                    return ApplicationServiceResult<
                        ApplicationStorageReadResult>.Failure(
                            ApplicationServiceResultCode.BackendFailure,
                        "Persistent read result allocation failed");
                }
                _lastPersistentReadResultStage = 2;
                ApplicationServiceResult<ApplicationStorageReadResult> result =
                    ApplicationServiceResult<ApplicationStorageReadResult>
                        .SuccessResult(readValue);
                if (result == null || result.Value == null) {
                    if (readValue != null) {
                        if (readValue.Bytes != null) readValue.Bytes.Dispose();
                        readValue.Dispose();
                    }
                    Ring3Abi.Phase35DiagnosticMarker(result == null
                        ? "P35_DIAG_SERVICE_RESULT_ALLOCATION_NULL=1"
                        : "P35_DIAG_SERVICE_RESULT_VALUE_NULL=1");
                    return ApplicationServiceResult<
                        ApplicationStorageReadResult>.Failure(
                            ApplicationServiceResultCode.BackendFailure,
                            "Persistent service result allocation failed");
                }
                _lastPersistentReadResultStage = 3;
                return result;
            }
            StorageNamespace storage = FindNamespace(context.ApplicationId);
            StorageEntry entry = storage == null ? null : FindEntry(storage,
                request.RelativePath);
            if (entry == null) {
                _failedReadRequestCount++;
                return ApplicationServiceResult<ApplicationStorageReadResult>.Failure(
                    ApplicationServiceResultCode.NotFound,
                    "Application storage entry was not found");
            }
            long length = entry.Bytes.Length;
            if (request.Offset > length) {
                _failedReadRequestCount++;
                return ApplicationServiceResult<ApplicationStorageReadResult>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Storage read offset is outside the entry");
            }
            if (request.Offset == length) {
                return ApplicationServiceResult<ApplicationStorageReadResult>.SuccessResult(
                    ApplicationStorageReadResult.Create(entry.RelativePath,
                        request.Offset, new byte[0], 0, true));
            }
            long remaining = length - request.Offset;
            int count = remaining > request.MaximumBytes
                ? request.MaximumBytes : (int)remaining;
            byte[] bytes = new byte[count];
            for (int i = 0; i < count; i++) {
                bytes[i] = entry.Bytes[(int)request.Offset + i];
            }
            return ApplicationServiceResult<ApplicationStorageReadResult>.SuccessResult(
                ApplicationStorageReadResult.Create(entry.RelativePath,
                    request.Offset, bytes, count,
                    request.Offset + count == length));
        }

        public override ApplicationServiceResult Write(
                ApplicationServiceContext context,
                ApplicationStorageWriteRequest request) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!TryValidate(context, out instance, out valid)) return valid;
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Storage write request is invalid or exceeds its bound");
            }
            if (request.Namespace == ApplicationStorageNamespace.Persistent) {
                _backendCallCount++;
                _persistentWriteCount++;
                byte[] payload = CopyBytes(request.Payload);
                FatOperationResult persistent = _persistentBackend == null
                    ? FatOperationResult.MediaUnavailable
                    : _persistentBackend.TryWriteValue(context.ApplicationId,
                        request.RelativePath, payload);
                return persistent == FatOperationResult.Success
                    ? ApplicationServiceResult.SuccessResult()
                    : PersistentFailure(persistent);
            }
            StorageNamespace storage = FindNamespace(context.ApplicationId);
            if (storage == null) {
                if (_namespaceCount >= _namespaces.Length) {
                    return ApplicationServiceResult.Failure(
                        ApplicationServiceResultCode.ResourceUnavailable,
                        "Application storage namespace capacity is exhausted");
                }
                storage = new StorageNamespace {
                    ApplicationId = context.ApplicationId
                };
                _namespaces[_namespaceCount++] = storage;
            }
            StorageEntry entry = FindEntry(storage, request.RelativePath);
            if (entry == null) {
                if (storage.Count >= storage.Entries.Length) {
                    return ApplicationServiceResult.Failure(
                        ApplicationServiceResultCode.ResourceUnavailable,
                        "Application storage entry capacity is exhausted");
                }
                entry = new StorageEntry {
                    RelativePath = request.RelativePath
                };
                storage.Entries[storage.Count++] = entry;
            }
            entry.Bytes = CopyBytes(request.Payload);
            return ApplicationServiceResult.SuccessResult();
        }

        public override ApplicationServiceResult Delete(
                ApplicationServiceContext context,
                ApplicationStorageRequest request) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!TryValidate(context, out instance, out valid)) return valid;
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Storage path is invalid or exceeds its bound");
            }
            if (request.Namespace == ApplicationStorageNamespace.Persistent) {
                _backendCallCount++;
                _persistentDeleteCount++;
                FatOperationResult persistent = _persistentBackend == null
                    ? FatOperationResult.MediaUnavailable
                    : _persistentBackend.TryDeleteValue(context.ApplicationId,
                        request.RelativePath);
                return persistent == FatOperationResult.Success
                    ? ApplicationServiceResult.SuccessResult()
                    : PersistentFailure(persistent);
            }
            StorageNamespace storage = FindNamespace(context.ApplicationId);
            StorageEntry entry = storage == null ? null : FindEntry(storage,
                request.RelativePath);
            if (entry == null) {
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.NotFound,
                    "Application storage entry was not found");
            }
            int index = 0;
            while (index < storage.Count && storage.Entries[index] != entry) {
                index++;
            }
            for (int i = index; i < storage.Count - 1; i++) {
                storage.Entries[i] = storage.Entries[i + 1];
            }
            storage.Entries[--storage.Count] = null;
            return ApplicationServiceResult.SuccessResult();
        }

        public override ApplicationServiceResult<ApplicationStorageEntry[]> Enumerate(
                ApplicationServiceContext context,
                ApplicationStorageNamespace space) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!TryValidate(context, out instance, out valid)) {
                return ApplicationServiceResult<ApplicationStorageEntry[]>.Failure(
                    valid.Code, valid.BoundedDiagnostic);
            }
            if (space != ApplicationStorageNamespace.Persistent &&
                    space != ApplicationStorageNamespace.Temporary) {
                return ApplicationServiceResult<ApplicationStorageEntry[]>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Storage namespace is invalid");
            }
            if (space == ApplicationStorageNamespace.Persistent) {
                _backendCallCount++;
                _persistentEnumerateCount++;
                ApplicationStorageEntry[] persistentEntries = null;
                FatOperationResult persistent = _persistentBackend == null
                    ? FatOperationResult.MediaUnavailable
                    : _persistentBackend.TryEnumerate(context.ApplicationId,
                        out persistentEntries);
                if (persistent != FatOperationResult.Success)
                    return PersistentFailure<ApplicationStorageEntry[]>(persistent);
                return ApplicationServiceResult<ApplicationStorageEntry[]>.SuccessResult(
                    persistentEntries);
            }
            StorageNamespace storage = FindNamespace(context.ApplicationId);
            int count = storage == null ? 0 : storage.Count;
            ApplicationStorageEntry[] entries =
                new ApplicationStorageEntry[count];
            for (int i = 0; i < count; i++) {
                StorageEntry entry = storage.Entries[i];
                entries[i] = ApplicationStorageEntry.Create(entry.RelativePath,
                    entry.Bytes == null ? 0 : entry.Bytes.Length);
            }
            return ApplicationServiceResult<ApplicationStorageEntry[]>.SuccessResult(
                entries);
        }

        internal override void ResetTemporaryForAppModel() {
            for (int i = 0; i < _namespaceCount; i++) {
                StorageNamespace storage = _namespaces[i];
                if (storage == null) continue;
                for (int j = 0; j < storage.Entries.Length; j++) {
                    storage.Entries[j] = null;
                }
                storage.Count = 0;
                _namespaces[i] = null;
            }
            _namespaceCount = 0;
            _backendCallCount = 0;
        }

        private bool TryValidate(ApplicationServiceContext context,
                out ApplicationInstance instance,
                out ApplicationServiceResult result) {
            bool valid = ApplicationServiceRegistry.TryValidateContext(context,
                ApplicationServiceId.Storage, out instance, out result);
            if (!valid && result.Code == ApplicationServiceResultCode.InvalidContext)
                _staleContextRejectionCount++;
            return valid;
        }

        private StorageNamespace FindNamespace(string applicationId) {
            for (int i = 0; i < _namespaceCount; i++) {
                StorageNamespace storage = _namespaces[i];
                if (storage != null && TextEquals(storage.ApplicationId,
                        applicationId)) return storage;
            }
            return null;
        }

        private static StorageEntry FindEntry(StorageNamespace storage,
                string relativePath) {
            for (int i = 0; i < storage.Count; i++) {
                StorageEntry entry = storage.Entries[i];
                if (entry != null && TextEquals(entry.RelativePath,
                        relativePath)) return entry;
            }
            return null;
        }

        private ApplicationServiceResult<T> PersistentFailure<T>(
                FatOperationResult result) {
            RecordPersistentFailure(result);
            return ApplicationServiceResult<T>.FailureOwnedDiagnostic(
                MapPersistentFailure(result), PersistentDiagnostic(result));
        }

        private ApplicationServiceResult PersistentFailure(
                FatOperationResult result) {
            RecordPersistentFailure(result);
            return ApplicationServiceResult.FailureOwnedDiagnostic(
                MapPersistentFailure(result), PersistentDiagnostic(result));
        }

        private void RecordPersistentFailure(FatOperationResult result) {
            if (result == FatOperationResult.ReadFailure ||
                    result == FatOperationResult.WriteFailure ||
                    result == FatOperationResult.TransportFailure) {
                _storageIoFailureCount++;
            }
            if (result == FatOperationResult.FlushFailure ||
                    result == FatOperationResult.FlushUnsupported) {
                _flushFailureCount++;
            }
        }

        private static ApplicationServiceResultCode MapPersistentFailure(
                FatOperationResult result) {
            switch (result) {
                case FatOperationResult.NotFound:
                    return ApplicationServiceResultCode.NotFound;
                case FatOperationResult.InvalidPath:
                case FatOperationResult.InvalidRange:
                case FatOperationResult.InvalidBuffer:
                    return ApplicationServiceResultCode.InvalidRequest;
                case FatOperationResult.Unsupported:
                    return ApplicationServiceResultCode.Unsupported;
                case FatOperationResult.NotMounted:
                case FatOperationResult.NoSpace:
                case FatOperationResult.ReadOnly:
                case FatOperationResult.FlushUnsupported:
                case FatOperationResult.MediaUnavailable:
                case FatOperationResult.EntryLimitExceeded:
                    return ApplicationServiceResultCode.ResourceUnavailable;
                default:
                    return ApplicationServiceResultCode.BackendFailure;
            }
        }

        private static string PersistentDiagnostic(FatOperationResult result) {
            return "Persistent storage operation failed: " + result.ToString();
        }

        private static byte[] CopyBytes(byte[] source) {
            if (source == null || source.Length == 0) return new byte[0];
            byte[] copy = new byte[source.Length];
            for (int i = 0; i < source.Length; i++) copy[i] = source[i];
            return copy;
        }

        private static bool TextEquals(string left, string right) {
            if (left == null || right == null || left.Length != right.Length) {
                return false;
            }
            for (int i = 0; i < left.Length; i++) {
                if (left[i] != right[i]) return false;
            }
            return true;
        }
    }
}
