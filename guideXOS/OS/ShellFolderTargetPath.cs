namespace guideXOS.OS {
    internal enum ShellFolderDirectoryProbeResult {
        Directory,
        Missing,
        NotDirectory,
        PermissionDenied,
        MountUnavailable,
        MountInvalidated,
        IoFailure,
        BackendUnavailable
    }

    internal enum ShellFolderTargetFailure {
        None,
        MalformedRequest,
        ResourceUnavailable,
        PermissionDenied,
        MountUnavailable,
        MountInvalidated,
        IoFailure,
        BackendUnavailable
    }

    internal interface IShellFolderDirectoryProbe {
        ShellFolderDirectoryProbeResult Inspect(string fileSystemPath);
    }

    /// <summary>
    /// Shared path validation for typed folder targets. Values are canonical
    /// virtual paths; the returned path is relative to the selected C# mount.
    /// This helper does not select or own a filesystem.
    /// </summary>
    internal static class ShellFolderTargetPath {
        internal const int MaxUtf8Bytes = 1024;
        internal const int MaxUtf16CodeUnits = 1024;

        internal static bool IsValidShellText(string value) {
            return !string.IsNullOrEmpty(value) &&
                value.Length <= MaxUtf16CodeUnits &&
                HasValidBoundedUtf8(value, false);
        }

        internal static bool TryValidateCanonicalPath(string canonicalPath,
                                                       out string fileSystemPath) {
            fileSystemPath = null;
            if (!IsCanonicalPath(canonicalPath)) return false;

            if (canonicalPath == "/") {
                fileSystemPath = string.Empty;
                return true;
            }

            fileSystemPath = canonicalPath.Substring(1);
            return true;
        }

        internal static bool IsCanonicalPath(string canonicalPath) {
            if (string.IsNullOrEmpty(canonicalPath) ||
                    canonicalPath.Length > MaxUtf16CodeUnits ||
                    canonicalPath[0] != '/' ||
                    !HasValidBoundedUtf8(canonicalPath, true)) return false;
            if (canonicalPath == "/") return true;

            int segmentStart = 1;
            for (int i = 1; i <= canonicalPath.Length; i++) {
                if (i < canonicalPath.Length && canonicalPath[i] != '/') {
                    continue;
                }

                int segmentLength = i - segmentStart;
                if (segmentLength == 0 ||
                        (segmentLength == 1 && canonicalPath[segmentStart] == '.') ||
                        (segmentLength == 2 && canonicalPath[segmentStart] == '.' &&
                         canonicalPath[segmentStart + 1] == '.')) return false;

                segmentStart = i + 1;
            }
            return true;
        }

        internal static bool TryResolveExistingDirectory(
                string canonicalPath, IShellFolderDirectoryProbe probe,
                out string fileSystemPath, out ShellFolderTargetFailure failure) {
            fileSystemPath = null;
            failure = ShellFolderTargetFailure.None;
            if (!TryValidateCanonicalPath(canonicalPath, out fileSystemPath)) {
                failure = ShellFolderTargetFailure.MalformedRequest;
                return false;
            }
            if (probe == null) {
                failure = ShellFolderTargetFailure.MountUnavailable;
                fileSystemPath = null;
                return false;
            }

            ShellFolderDirectoryProbeResult result;
            try {
                result = probe.Inspect(fileSystemPath);
            } catch {
                result = ShellFolderDirectoryProbeResult.BackendUnavailable;
            }

            if (result == ShellFolderDirectoryProbeResult.Directory) return true;
            if (result == ShellFolderDirectoryProbeResult.PermissionDenied) {
                failure = ShellFolderTargetFailure.PermissionDenied;
            } else if (result == ShellFolderDirectoryProbeResult.Missing ||
                    result == ShellFolderDirectoryProbeResult.NotDirectory) {
                failure = ShellFolderTargetFailure.ResourceUnavailable;
            } else if (result == ShellFolderDirectoryProbeResult.MountUnavailable) {
                failure = ShellFolderTargetFailure.MountUnavailable;
            } else if (result == ShellFolderDirectoryProbeResult.MountInvalidated) {
                failure = ShellFolderTargetFailure.MountInvalidated;
            } else if (result == ShellFolderDirectoryProbeResult.IoFailure) {
                failure = ShellFolderTargetFailure.IoFailure;
            } else if (result == ShellFolderDirectoryProbeResult.BackendUnavailable) {
                failure = ShellFolderTargetFailure.BackendUnavailable;
            } else {
                failure = ShellFolderTargetFailure.BackendUnavailable;
            }
            fileSystemPath = null;
            return false;
        }

        private static bool HasValidBoundedUtf8(string value,
                                                bool rejectBackslash) {
            int byteCount = 0;
            for (int i = 0; i < value.Length; i++) {
                char c = value[i];
                if (c < ' ' || (c >= '\u007f' && c <= '\u009f') ||
                        (rejectBackslash && c == '\\'))
                    return false;

                if (c <= '\u007f') {
                    byteCount++;
                } else if (c <= '\u07ff') {
                    byteCount += 2;
                } else if (c >= '\ud800' && c <= '\udbff') {
                    if (i + 1 >= value.Length || value[i + 1] < '\udc00' ||
                            value[i + 1] > '\udfff') return false;
                    i++;
                    byteCount += 4;
                } else if (c >= '\udc00' && c <= '\udfff') {
                    return false;
                } else {
                    byteCount += 3;
                }

                if (byteCount > MaxUtf8Bytes) return false;
            }
            return true;
        }
    }
}
