namespace guideXOS.FS {
    /// <summary>
    /// Typed outcome for a non-mutating directory inspection. Backends must
    /// use Indeterminate when their existing storage API cannot distinguish
    /// absence from an I/O or parsing failure.
    /// </summary>
    internal enum FileSystemDirectoryInspectionStatus : byte {
        Directory,
        NotFound,
        NotDirectory,
        AccessDenied,
        FilesystemUnavailable,
        MountUnavailable,
        MountInvalidated,
        IoFailure,
        InvalidPath,
        Indeterminate
    }

    /// <summary>
    /// Inspection status and a bounded numeric backend code for developer
    /// diagnostics. The code is never copied into application-facing text.
    /// </summary>
    internal struct FileSystemDirectoryInspection {
        internal readonly FileSystemDirectoryInspectionStatus Status;
        internal readonly int BackendCode;

        internal FileSystemDirectoryInspection(
                FileSystemDirectoryInspectionStatus status, int backendCode) {
            Status = status;
            BackendCode = backendCode;
        }

        internal static FileSystemDirectoryInspection Create(
                FileSystemDirectoryInspectionStatus status) {
            return new FileSystemDirectoryInspection(status, 0);
        }

        internal static FileSystemDirectoryInspection Create(
                FileSystemDirectoryInspectionStatus status, int backendCode) {
            return new FileSystemDirectoryInspection(status, backendCode);
        }

        /// <summary>
        /// Accepts only the relative path shape produced by the typed shell
        /// folder validator. Empty string denotes the mounted root.
        /// </summary>
        internal static bool IsValidRelativePath(string path) {
            if (path == null || path.Length > 1023) return false;
            if (path.Length == 0) return true;

            int segmentStart = 0;
            int utf8Bytes = 0;
            for (int i = 0; i <= path.Length; i++) {
                if (i < path.Length) {
                    char c = path[i];
                    if (c < ' ' || (c >= '\u007f' && c <= '\u009f') ||
                            c == '\\') return false;
                    if (c == '/') {
                        if (!IsValidSegment(path, segmentStart, i - segmentStart))
                            return false;
                        segmentStart = i + 1;
                        utf8Bytes++;
                        continue;
                    }

                    if (c <= '\u007f') {
                        utf8Bytes++;
                    } else if (c <= '\u07ff') {
                        utf8Bytes += 2;
                    } else if (c >= '\ud800' && c <= '\udbff') {
                        if (i + 1 >= path.Length || path[i + 1] < '\udc00' ||
                                path[i + 1] > '\udfff') return false;
                        i++;
                        utf8Bytes += 4;
                    } else if (c >= '\udc00' && c <= '\udfff') {
                        return false;
                    } else {
                        utf8Bytes += 3;
                    }
                    if (utf8Bytes > 1023) return false;
                }
            }

            return IsValidSegment(path, segmentStart,
                path.Length - segmentStart);
        }

        private static bool IsValidSegment(string path, int start, int length) {
            if (length == 0 || (length == 1 && path[start] == '.') ||
                    (length == 2 && path[start] == '.' &&
                     path[start + 1] == '.')) return false;
            return true;
        }
    }
}
