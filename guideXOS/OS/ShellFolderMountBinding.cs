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
                case ShellFolderTargetFailure.InvalidPath:
                    return LaunchErrorCode.MalformedRequest;
                case ShellFolderTargetFailure.PermissionDenied:
                    return LaunchErrorCode.PermissionDenied;
                case ShellFolderTargetFailure.ResourceUnavailable:
                case ShellFolderTargetFailure.FilesystemUnavailable:
                case ShellFolderTargetFailure.MountUnavailable:
                    return LaunchErrorCode.ResourceUnavailable;
                case ShellFolderTargetFailure.MountInvalidated:
                case ShellFolderTargetFailure.IoFailure:
                case ShellFolderTargetFailure.BackendUnavailable:
                case ShellFolderTargetFailure.IndeterminateBackendFailure:
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
                    return ShellFolderDirectoryProbeResult.FilesystemUnavailable;
                try {
                    FileSystemDirectoryInspection inspection =
                        _fileSystem.InspectDirectory(path);
                    switch (inspection.Status) {
                        case FileSystemDirectoryInspectionStatus.Directory:
                            return ShellFolderDirectoryProbeResult.Directory;
                        case FileSystemDirectoryInspectionStatus.NotFound:
                            return ShellFolderDirectoryProbeResult.Missing;
                        case FileSystemDirectoryInspectionStatus.NotDirectory:
                            return ShellFolderDirectoryProbeResult.NotDirectory;
                        case FileSystemDirectoryInspectionStatus.AccessDenied:
                            return ShellFolderDirectoryProbeResult.PermissionDenied;
                        case FileSystemDirectoryInspectionStatus.FilesystemUnavailable:
                            return ShellFolderDirectoryProbeResult.FilesystemUnavailable;
                        case FileSystemDirectoryInspectionStatus.MountUnavailable:
                            return ShellFolderDirectoryProbeResult.MountUnavailable;
                        case FileSystemDirectoryInspectionStatus.MountInvalidated:
                            return ShellFolderDirectoryProbeResult.MountInvalidated;
                        case FileSystemDirectoryInspectionStatus.IoFailure:
                            return ShellFolderDirectoryProbeResult.IoFailure;
                        case FileSystemDirectoryInspectionStatus.InvalidPath:
                            return ShellFolderDirectoryProbeResult.InvalidPath;
                        default:
                            return ShellFolderDirectoryProbeResult.Indeterminate;
                    }
                } catch {
                    // Do not expose backend exception text to applications.
                    return ShellFolderDirectoryProbeResult.Indeterminate;
                }
            }
        }
    }
}
