using System;

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
        private int _namespaceCount;
        private int _backendCallCount;

        internal int BackendCallCount { get { return _backendCallCount; } }

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
                return PersistentUnavailable<bool>();
            }
            StorageNamespace storage = FindNamespace(context.ApplicationId);
            return ApplicationServiceResult<bool>.SuccessResult(
                storage != null && FindEntry(storage, request.RelativePath) != null);
        }

        public override ApplicationServiceResult<ApplicationStorageReadResult>
                Read(ApplicationServiceContext context,
                    ApplicationStorageReadRequest request) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!TryValidate(context, out instance, out valid)) {
                return ApplicationServiceResult<ApplicationStorageReadResult>.Failure(
                    valid.Code, valid.BoundedDiagnostic);
            }
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult<ApplicationStorageReadResult>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Storage read request is invalid or exceeds its bound");
            }
            if (request.Namespace == ApplicationStorageNamespace.Persistent) {
                return PersistentUnavailable<ApplicationStorageReadResult>();
            }
            StorageNamespace storage = FindNamespace(context.ApplicationId);
            StorageEntry entry = storage == null ? null : FindEntry(storage,
                request.RelativePath);
            if (entry == null) {
                return ApplicationServiceResult<ApplicationStorageReadResult>.Failure(
                    ApplicationServiceResultCode.NotFound,
                    "Application storage entry was not found");
            }
            long length = entry.Bytes.Length;
            if (request.Offset > length) {
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
                return PersistentUnavailable();
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
                return PersistentUnavailable();
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
                return PersistentUnavailable<ApplicationStorageEntry[]>();
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
            return ApplicationServiceRegistry.TryValidateContext(context,
                ApplicationServiceId.Storage, out instance, out result);
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

        private ApplicationServiceResult<T> PersistentUnavailable<T>() {
            _backendCallCount++;
            return ApplicationServiceResult<T>.Failure(
                ApplicationServiceResultCode.ResourceUnavailable,
                "Persistent application storage is unavailable on the selected backend");
        }

        private ApplicationServiceResult PersistentUnavailable() {
            _backendCallCount++;
            return ApplicationServiceResult.Failure(
                ApplicationServiceResultCode.ResourceUnavailable,
                "Persistent application storage is unavailable on the selected backend");
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
