using guideXOS.FS;

namespace guideXOS.OS {
    /// <summary>
    /// Acquires and inspects a C# folder target against one retained mount.
    /// This boundary is UI independent so mount selection and typed failures
    /// can be qualified in a hosted runner.
    /// </summary>
    internal static class ShellFolderMountBinding {
        internal static bool TryBindFolderRequest(LaunchRequest request,
                out LaunchResult failure) {
            failure = null;
            if (!ValidateFolderRequest(request, out failure)) return false;
            if (request.FileSystemMountLease != null) {
                return TryResolveFolderRequest(request, out _, out _, out failure);
            }

            FileSystemMountLease lease;
            if (FileSystemMountLifetime.TryAcquireActive(out lease) !=
                    FileSystemMountAcquireResult.Acquired || lease == null) {
                failure = LaunchResult.Failed(
                    LaunchErrorCode.ResourceUnavailable,
                    "The C# filesystem mount is unavailable",
                    request.TargetAppId);
                return false;
            }

            FileSystem fileSystem = lease.FileSystem;
            string path;
            if (!TryInspectFolderPath(request, fileSystem, out path,
                    out failure)) {
                lease.Dispose();
                return false;
            }
            if (!request.AttachFileSystemMountLease(lease)) {
                lease.Dispose();
                if (path != null && path.Length > 0) path.Dispose();
                failure = LaunchResult.Failed(
                    LaunchErrorCode.BackendUnavailable,
                    "The selected C# filesystem mount could not be retained",
                    request.TargetAppId);
                return false;
            }
            if (path != null && path.Length > 0) path.Dispose();
            return true;
        }

        internal static bool TryResolveFolderRequest(LaunchRequest request,
                out FileSystem fileSystem, out string fileSystemPath,
                out LaunchResult failure) {
            fileSystem = null;
            fileSystemPath = null;
            failure = null;
            if (!ValidateFolderRequest(request, out failure)) return false;

            FileSystemMountLease lease = request.FileSystemMountLease;
            if (lease == null || !lease.IsValid || lease.FileSystem == null) {
                failure = LaunchResult.Failed(
                    lease == null ? LaunchErrorCode.ResourceUnavailable :
                        LaunchErrorCode.BackendUnavailable,
                    lease == null
                        ? "The folder request has no retained C# mount"
                        : "The retained C# filesystem mount is no longer valid",
                    request.TargetAppId);
                return false;
            }
            FileSystemMountLease operationLease;
            if (!lease.TryClone(out operationLease) || operationLease == null) {
                failure = LaunchResult.Failed(
                    LaunchErrorCode.BackendUnavailable,
                    "The retained C# filesystem mount could not be used",
                    request.TargetAppId);
                return false;
            }
            try {
                fileSystem = operationLease.FileSystem;
                if (!TryInspectFolderPath(request, fileSystem,
                        out fileSystemPath, out failure)) {
                    fileSystem = null;
                    return false;
                }
                return true;
            } finally {
                operationLease.Dispose();
            }
        }

        private static bool ValidateFolderRequest(LaunchRequest request,
                                                   out LaunchResult failure) {
            failure = null;
            if (request == null ||
                    request.TargetKind != LaunchRequestTargetKind.ShellObject ||
                    request.ShellTargetKind != ApplicationShellTargetKind.FileSystem) {
                failure = LaunchResult.Failed(
                    LaunchErrorCode.MalformedRequest,
                    "Folder activation requires a typed shell filesystem target",
                    "gxos.builtin.files");
                return false;
            }
            if (request.SourceShellObjectId != "gxos.shell.root" ||
                    request.TargetAppId != "gxos.builtin.files") {
                failure = LaunchResult.Failed(
                    LaunchErrorCode.UnsupportedTarget,
                    "No verified C# filesystem binding exists for this shell target",
                    request.TargetAppId);
                return false;
            }
            return true;
        }

        private static bool TryInspectFolderPath(LaunchRequest request,
                FileSystem fileSystem, out string fileSystemPath,
                out LaunchResult failure) {
            fileSystemPath = null;
            failure = null;
            ShellFolderTargetFailure pathFailure;
            IShellFolderDirectoryProbe probe =
                new CSharpFileSystemFolderProbe(fileSystem);
            if (!ShellFolderTargetPath.TryResolveExistingDirectory(
                    request.ShellTargetValue, probe, out fileSystemPath,
                    out pathFailure)) {
                failure = LaunchResult.Failed(
                    FolderFailureToLaunchError(pathFailure),
                    "C# folder inspection failed",
                    request.TargetAppId);
                return false;
            }
            return true;
        }

        private static LaunchErrorCode FolderFailureToLaunchError(
                ShellFolderTargetFailure failure) {
            switch (failure) {
                case ShellFolderTargetFailure.MalformedRequest:
                    return LaunchErrorCode.MalformedRequest;
                case ShellFolderTargetFailure.PermissionDenied:
                    return LaunchErrorCode.PermissionDenied;
                case ShellFolderTargetFailure.ResourceUnavailable:
                case ShellFolderTargetFailure.MountUnavailable:
                    return LaunchErrorCode.ResourceUnavailable;
                case ShellFolderTargetFailure.MountInvalidated:
                case ShellFolderTargetFailure.IoFailure:
                case ShellFolderTargetFailure.BackendUnavailable:
                default:
                    return LaunchErrorCode.BackendUnavailable;
            }
        }

        private sealed class CSharpFileSystemFolderProbe :
                IShellFolderDirectoryProbe {
            private readonly FileSystem _fileSystem;

            internal CSharpFileSystemFolderProbe(FileSystem fileSystem) {
                _fileSystem = fileSystem;
            }

            public ShellFolderDirectoryProbeResult Inspect(string path) {
                if (_fileSystem == null || path == null)
                    return ShellFolderDirectoryProbeResult.BackendUnavailable;
                if (path.Length == 0) return InspectRoot();

                int slash = path.LastIndexOf('/');
                string parent = slash < 0 ? string.Empty :
                    path.Substring(0, slash);
                string leaf = path.Substring(slash + 1);
                try {
                    return InspectChild(parent, leaf);
                } finally {
                    if (parent.Length > 0) parent.Dispose();
                    leaf.Dispose();
                }
            }

            private ShellFolderDirectoryProbeResult InspectRoot() {
                try {
                    System.Collections.Generic.List<FileInfo> entries =
                        _fileSystem.GetFiles(string.Empty);
                    if (entries == null)
                        return ShellFolderDirectoryProbeResult.BackendUnavailable;
                    DisposeEntries(entries);
                    return ShellFolderDirectoryProbeResult.Directory;
                } catch {
                    return ShellFolderDirectoryProbeResult.IoFailure;
                }
            }

            private ShellFolderDirectoryProbeResult InspectChild(string parent,
                                                                  string leaf) {
                System.Collections.Generic.List<FileInfo> entries = null;
                try {
                    entries = _fileSystem.GetFiles(parent);
                    if (entries == null)
                        return ShellFolderDirectoryProbeResult.BackendUnavailable;
                    bool found = false;
                    bool directory = false;
                    for (int i = 0; i < entries.Count; i++) {
                        FileInfo entry = entries[i];
                        if (entry != null && entry.Name == leaf) {
                            found = true;
                            directory = entry.Attribute == FileAttribute.Directory;
                            break;
                        }
                    }
                    if (!found) return ShellFolderDirectoryProbeResult.Missing;
                    return directory
                        ? ShellFolderDirectoryProbeResult.Directory
                        : ShellFolderDirectoryProbeResult.NotDirectory;
                } catch {
                    return ShellFolderDirectoryProbeResult.IoFailure;
                } finally {
                    if (entries != null) DisposeEntries(entries);
                }
            }

            private static void DisposeEntries(
                    System.Collections.Generic.List<FileInfo> entries) {
                for (int i = 0; i < entries.Count; i++) {
                    if (entries[i] != null) entries[i].Dispose();
                }
                entries.Dispose();
            }
        }
    }
}
