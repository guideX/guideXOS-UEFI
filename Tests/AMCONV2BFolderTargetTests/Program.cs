using System;
using System.Collections.Generic;
using guideXOS.OS;

internal static class Program {
    private static int _passed;
    private static int _failed;

    private static int Main() {
        string path;
        Check(ShellFolderTargetPath.IsValidShellText("gxos.shell.files"),
              "canonical shell ID is valid shared text");
        Check(ShellFolderTargetPath.IsValidShellText(new string('a', 1024)),
              "1,024-byte shell identifier is accepted");
        Check(!ShellFolderTargetPath.IsValidShellText(new string('a', 1025)),
              "1,025-byte shell identifier is rejected");
        Check(ShellFolderTargetPath.IsValidShellText("File\\ Explorer"),
              "backslash is allowed in non-path shell text");
        Check(!ShellFolderTargetPath.IsValidShellText("Shell\u0001Target"),
              "control character is rejected in shell text");
        Check(!ShellFolderTargetPath.IsValidShellText("/" + new string('\u00e9', 512)),
              "shell text uses the shared UTF-8 byte bound");
        Check(!ShellFolderTargetPath.IsValidShellText("/" + '\ud83d'),
              "invalid surrogate is rejected in shell text");
        Check(ShellFolderTargetPath.TryValidateCanonicalPath("/", out path) &&
              path == string.Empty, "root maps to mounted filesystem root");
        Check(ShellFolderTargetPath.TryValidateCanonicalPath("/Documents", out path) &&
              path == "Documents", "absolute folder maps to relative C# path");
        Check(ShellFolderTargetPath.TryValidateCanonicalPath("/A/B", out path) &&
              path == "A/B", "nested folder is accepted");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath(null, out path),
              "null target is rejected");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath(string.Empty, out path),
              "empty target is rejected");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath("Documents", out path),
              "relative target is rejected");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath("C:/Documents", out path),
              "host or drive path is rejected");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath("/A\\B", out path),
              "backslash path is rejected");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath("//A", out path),
              "repeated separator is rejected");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath("/A/", out path),
              "trailing separator is rejected");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath("/./A", out path),
              "dot segment is rejected");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath("/A/../B", out path),
              "root escape segment is rejected");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath("/A\u0001B", out path),
              "control character is rejected");

        string utf8Boundary = "/" + new string('a', 1023);
        Check(ShellFolderTargetPath.TryValidateCanonicalPath(utf8Boundary, out path) &&
              path.Length == 1023, "1,024-byte UTF-8 boundary is accepted");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath(utf8Boundary + "a", out path),
              "1,025-byte UTF-8 target is rejected");
        string multibyteOverBound = "/" + new string('\u00e9', 512);
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath(multibyteOverBound, out path),
              "multibyte target uses UTF-8 byte bound");
        string validSurrogatePair = "/" + '\ud83d' + '\ude00';
        Check(ShellFolderTargetPath.TryValidateCanonicalPath(validSurrogatePair, out path),
              "valid surrogate pair is accepted");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath("/" + '\ud83d', out path),
              "unpaired surrogate is rejected");
        Check(!ShellFolderTargetPath.TryValidateCanonicalPath("/" + new string('a', 1024), out path),
              "1,025 UTF-16 code units are rejected");

        var probe = new FakeProbe();
        probe.Set("", ShellFolderDirectoryProbeResult.Directory);
        probe.Set("Existing", ShellFolderDirectoryProbeResult.Directory);
        probe.Set("file.txt", ShellFolderDirectoryProbeResult.NotDirectory);
        probe.Set("locked", ShellFolderDirectoryProbeResult.PermissionDenied);
        probe.Set("broken", ShellFolderDirectoryProbeResult.Unavailable);

        ShellFolderTargetFailure failure;
        Check(ShellFolderTargetPath.TryResolveExistingDirectory("/Existing", probe,
                  out path, out failure) && path == "Existing" &&
              failure == ShellFolderTargetFailure.None,
              "verified directory resolves successfully");
        Check(probe.LastPath == "Existing", "probe receives selected mount path");
        Check(!ShellFolderTargetPath.TryResolveExistingDirectory("/Missing", probe,
                  out path, out failure) && path == null &&
              failure == ShellFolderTargetFailure.ResourceUnavailable,
              "missing directory is unavailable");
        Check(!ShellFolderTargetPath.TryResolveExistingDirectory("/file.txt", probe,
                  out path, out failure) && path == null &&
              failure == ShellFolderTargetFailure.ResourceUnavailable,
              "file target is unavailable as a folder");
        Check(!ShellFolderTargetPath.TryResolveExistingDirectory("/locked", probe,
                  out path, out failure) &&
              failure == ShellFolderTargetFailure.PermissionDenied,
              "permission failure retains typed result");
        Check(!ShellFolderTargetPath.TryResolveExistingDirectory("/broken", probe,
                  out path, out failure) &&
              failure == ShellFolderTargetFailure.ResourceUnavailable,
              "filesystem failure retains typed result");

        int probesBeforeMalformed = probe.InspectCount;
        Check(!ShellFolderTargetPath.TryResolveExistingDirectory("/A/../B", probe,
                  out path, out failure) &&
              failure == ShellFolderTargetFailure.MalformedRequest &&
              probe.InspectCount == probesBeforeMalformed,
              "malformed target is rejected before filesystem inspection");
        Check(!ShellFolderTargetPath.TryResolveExistingDirectory("/", null,
                  out path, out failure) &&
              failure == ShellFolderTargetFailure.ResourceUnavailable,
              "missing filesystem probe is unavailable");

        Console.WriteLine("AMCONV2B_FOLDER_RUNTIME_ASSERTIONS=" + _passed);
        Console.WriteLine("AMCONV2B_FOLDER_RUNTIME_FAILURES=" + _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string name) {
        if (condition) {
            _passed++;
            Console.WriteLine("AMCONV2B_FOLDER_CHECK=PASS: " + name);
        } else {
            _failed++;
            Console.WriteLine("AMCONV2B_FOLDER_CHECK=FAIL: " + name);
        }
    }

    private sealed class FakeProbe : IShellFolderDirectoryProbe {
        private readonly Dictionary<string, ShellFolderDirectoryProbeResult> _results =
            new Dictionary<string, ShellFolderDirectoryProbeResult>(StringComparer.Ordinal);
        internal int InspectCount { get; private set; }
        internal string LastPath { get; private set; }

        internal void Set(string path, ShellFolderDirectoryProbeResult result) {
            _results[path] = result;
        }

        public ShellFolderDirectoryProbeResult Inspect(string fileSystemPath) {
            InspectCount++;
            LastPath = fileSystemPath;
            ShellFolderDirectoryProbeResult result;
            return _results.TryGetValue(fileSystemPath, out result)
                ? result : ShellFolderDirectoryProbeResult.Missing;
        }
    }
}
