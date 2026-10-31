using System;
using System.Collections.Generic;

namespace guideXOS.FS {
    // Controlled filesystem resource for the production mount-lifetime
    // registry. The registry and lease implementation are linked from Kernel.
    internal sealed class FileSystem {
        private int _leaseCount;
        private bool _disposeRequested;
        private bool _disposed;
        private readonly Dictionary<string, List<FileEntry>> _entries =
            new Dictionary<string, List<FileEntry>>(StringComparer.Ordinal);

        internal int LeaseCount { get { return _leaseCount; } }
        internal int DisposalCount { get; private set; }
        internal int GetFilesCount { get; private set; }
        internal bool IsDisposed { get { return _disposed; } }
        internal bool ThrowOnGetFiles { get; set; }
        internal bool ReturnNullFromGetFiles { get; set; }
        internal bool CanAcquireMountLease {
            get { return !_disposeRequested && !_disposed; }
        }
        internal bool IsMountResourceAlive { get { return !_disposed; } }

        internal bool TryAddMountLease() {
            if (!CanAcquireMountLease || _leaseCount == int.MaxValue) return false;
            _leaseCount++;
            return true;
        }

        internal bool TryAddRetainedMountLease() {
            if (_disposed || _leaseCount <= 0 || _leaseCount == int.MaxValue)
                return false;
            _leaseCount++;
            return true;
        }

        internal bool ReleaseMountLease() {
            if (_leaseCount <= 0) return false;
            _leaseCount--;
            if (_leaseCount == 0 && _disposeRequested && !_disposed) {
                _disposed = true;
                return true;
            }
            return false;
        }

        internal bool RequestMountDisposal() {
            if (_disposed || _disposeRequested) return false;
            _disposeRequested = true;
            if (_leaseCount != 0) return false;
            _disposed = true;
            return true;
        }

        internal void DisposeMountResource() {
            _disposed = true;
            DisposalCount++;
        }

        internal void Dispose() {
            FileSystemMountLifetime.RequestDisposal(this);
        }

        internal void AddEntry(string parent, string name,
                               FileAttribute attribute) {
            List<FileEntry> entries;
            if (!_entries.TryGetValue(parent, out entries)) {
                entries = new List<FileEntry>();
                _entries[parent] = entries;
            }
            entries.Add(new FileEntry(name, attribute));
        }

        internal List<FileInfo> GetFiles(string path) {
            GetFilesCount++;
            if (ThrowOnGetFiles) throw new Exception("controlled I/O failure");
            if (ReturnNullFromGetFiles) return null;
            List<FileEntry> stored;
            List<FileInfo> result = new List<FileInfo>();
            if (!_entries.TryGetValue(path, out stored)) return result;
            for (int i = 0; i < stored.Count; i++) {
                result.Add(new FileInfo(stored[i].Name, stored[i].Attribute));
            }
            return result;
        }

        private sealed class FileEntry {
            internal readonly string Name;
            internal readonly FileAttribute Attribute;
            internal FileEntry(string name, FileAttribute attribute) {
                Name = name;
                Attribute = attribute;
            }
        }
    }

    internal enum FileAttribute { None, Directory }

    internal sealed class FileInfo {
        internal readonly string Name;
        internal readonly FileAttribute Attribute;
        internal bool IsDisposed { get; private set; }
        internal FileInfo(string name, FileAttribute attribute) {
            Name = name;
            Attribute = attribute;
        }
        public void Dispose() { IsDisposed = true; }
    }
}

namespace guideXOS.OS {
    using guideXOS.FS;

    internal static class HostedDisposableStringExtensions {
        internal static void Dispose(this string value) { }
        internal static void Dispose<T>(
                this System.Collections.Generic.List<T> value) { }
    }

    internal enum LaunchRequestTargetKind { ShellObject }
    internal enum ApplicationShellTargetKind { FileSystem }
    internal enum LaunchErrorCode {
        MalformedRequest,
        UnsupportedTarget,
        ResourceUnavailable,
        PermissionDenied,
        BackendUnavailable
    }

    internal sealed class LaunchResult {
        internal LaunchErrorCode ErrorCode { get; private set; }
        internal string AppId { get; private set; }
        private LaunchResult(LaunchErrorCode errorCode, string appId) {
            ErrorCode = errorCode;
            AppId = appId;
        }
        internal static LaunchResult Failed(LaunchErrorCode errorCode,
                                            string diagnostic,
                                            string appId) {
            return new LaunchResult(errorCode, appId);
        }
    }

    internal sealed class LaunchRequest {
        private FileSystemMountLease _lease;
        internal LaunchRequestTargetKind TargetKind { get; set; }
        internal ApplicationShellTargetKind ShellTargetKind { get; set; }
        internal string SourceShellObjectId { get; set; }
        internal string TargetAppId { get; set; }
        internal string ShellTargetValue { get; set; }
        internal FileSystemMountLease FileSystemMountLease {
            get { return _lease; }
        }
        internal bool AttachFileSystemMountLease(FileSystemMountLease lease) {
            if (_lease != null || lease == null || !lease.IsValid) return false;
            _lease = lease;
            return true;
        }
        internal void Dispose() {
            if (_lease == null) return;
            _lease.Dispose();
            _lease = null;
        }
    }
}
