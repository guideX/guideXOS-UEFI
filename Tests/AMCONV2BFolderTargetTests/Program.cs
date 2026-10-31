using System;
using System.Collections.Generic;
using guideXOS.FS;
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
        probe.Set("broken", ShellFolderDirectoryProbeResult.IoFailure);

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
              failure == ShellFolderTargetFailure.IoFailure,
              "filesystem I/O failure retains typed result");

        Check(FailureFor(ShellFolderDirectoryProbeResult.MountUnavailable) ==
                  ShellFolderTargetFailure.MountUnavailable,
              "unavailable mount retains typed result");
        Check(FailureFor(ShellFolderDirectoryProbeResult.MountInvalidated) ==
                  ShellFolderTargetFailure.MountInvalidated,
              "invalidated mount retains typed result");
        Check(FailureFor(ShellFolderDirectoryProbeResult.BackendUnavailable) ==
                  ShellFolderTargetFailure.BackendUnavailable,
              "unavailable filesystem backend retains typed result");

        TestMountLifetime();

        int probesBeforeMalformed = probe.InspectCount;
        Check(!ShellFolderTargetPath.TryResolveExistingDirectory("/A/../B", probe,
                  out path, out failure) &&
              failure == ShellFolderTargetFailure.MalformedRequest &&
              probe.InspectCount == probesBeforeMalformed,
              "malformed target is rejected before filesystem inspection");
        Check(!ShellFolderTargetPath.TryResolveExistingDirectory("/", null,
                  out path, out failure) &&
              failure == ShellFolderTargetFailure.MountUnavailable,
              "missing filesystem probe is unavailable");

        TestProductionFolderBinding();

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

    private static ShellFolderTargetFailure FailureFor(
            ShellFolderDirectoryProbeResult probeResult) {
        var probe = new FakeProbe();
        probe.Set("target", probeResult);
        string path;
        ShellFolderTargetFailure failure;
        ShellFolderTargetPath.TryResolveExistingDirectory("/target", probe,
            out path, out failure);
        return failure;
    }

    private static void TestProductionFolderBinding() {
        var selected = new FileSystem();
        selected.AddEntry(string.Empty, "Folder", FileAttribute.Directory);
        selected.AddEntry(string.Empty, "plain.txt", FileAttribute.None);
        FileSystemMountLifetime.SetActiveFileSystem(selected);

        LaunchRequest malformed = NewFolderRequest("/A/../B");
        LaunchResult failure;
        Check(!ShellFolderMountBinding.TryBindFolderRequest(malformed,
                  out failure) && failure != null &&
                  failure.ErrorCode == LaunchErrorCode.MalformedRequest &&
                  selected.GetFilesCount == 0 && selected.LeaseCount == 0,
              "production folder binding rejects traversal without filesystem I/O");
        malformed.Dispose();

        LaunchRequest request = NewFolderRequest("/Folder");
        bool bound = ShellFolderMountBinding.TryBindFolderRequest(request,
            out failure);
        FileSystemMountLease requestLease = request.FileSystemMountLease;
        Check(bound && failure == null && requestLease != null &&
                  requestLease.FileSystem == selected && selected.LeaseCount == 1,
              "production folder binding retains the inspected filesystem");

        var replacement = new FileSystem();
        replacement.AddEntry(string.Empty, "Folder", FileAttribute.Directory);
        FileSystemMountLifetime.SetActiveFileSystem(replacement);
        FileSystem resolvedFileSystem;
        string resolvedPath;
        Check(ShellFolderMountBinding.TryResolveFolderRequest(request,
                  out resolvedFileSystem, out resolvedPath, out failure) &&
                  resolvedFileSystem == selected && resolvedPath == "Folder" &&
                  replacement.GetFilesCount == 0,
              "production factory reinspection stays on the request's mount after replacement");
        if (resolvedPath != null) resolvedPath.Dispose();
        request.Dispose();
        Check(selected.LeaseCount == 0,
              "disposing the folder request releases its mount");
        int inspectionsBeforeInvalidatedRequest = selected.GetFilesCount;
        Check(!ShellFolderMountBinding.TryResolveFolderRequest(request,
                  out resolvedFileSystem, out resolvedPath, out failure) &&
                  resolvedFileSystem == null && resolvedPath == null &&
                  failure != null &&
                  failure.ErrorCode == LaunchErrorCode.ResourceUnavailable &&
                  selected.GetFilesCount == inspectionsBeforeInvalidatedRequest &&
                  replacement.GetFilesCount == 0,
              "invalidated folder request fails without falling back to the active mount");

        FileSystemMountLifetime.SetActiveFileSystem(selected);
        LaunchRequest missing = NewFolderRequest("/Missing");
        Check(!ShellFolderMountBinding.TryBindFolderRequest(missing,
                  out failure) && failure != null &&
                  failure.ErrorCode == LaunchErrorCode.ResourceUnavailable &&
                  selected.LeaseCount == 0,
              "production folder binding reports a missing directory and releases acquisition");
        missing.Dispose();

        LaunchRequest notDirectory = NewFolderRequest("/plain.txt");
        Check(!ShellFolderMountBinding.TryBindFolderRequest(notDirectory,
                  out failure) && failure != null &&
                  failure.ErrorCode == LaunchErrorCode.ResourceUnavailable &&
                  selected.LeaseCount == 0,
              "production folder binding rejects file entries as directories");
        notDirectory.Dispose();

        selected.ThrowOnGetFiles = true;
        LaunchRequest ioFailure = NewFolderRequest("/Folder");
        Check(!ShellFolderMountBinding.TryBindFolderRequest(ioFailure,
                  out failure) && failure != null &&
                  failure.ErrorCode == LaunchErrorCode.BackendUnavailable &&
                  selected.LeaseCount == 0,
              "production folder binding reports backend I/O failure and releases acquisition");
        ioFailure.Dispose();
        selected.ThrowOnGetFiles = false;

        selected.ReturnNullFromGetFiles = true;
        LaunchRequest backendFailure = NewFolderRequest("/Folder");
        Check(!ShellFolderMountBinding.TryBindFolderRequest(backendFailure,
                  out failure) && failure != null &&
                  failure.ErrorCode == LaunchErrorCode.BackendUnavailable &&
                  selected.LeaseCount == 0,
              "production folder binding reports unavailable enumeration backend");
        backendFailure.Dispose();
        selected.ReturnNullFromGetFiles = false;

        FileSystemMountLifetime.SetActiveFileSystem(null);
        LaunchRequest unavailableMount = NewFolderRequest("/Folder");
        Check(!ShellFolderMountBinding.TryBindFolderRequest(unavailableMount,
                  out failure) && failure != null &&
                  failure.ErrorCode == LaunchErrorCode.ResourceUnavailable,
              "production folder binding reports an unavailable active mount");
        unavailableMount.Dispose();

        FileSystemMountLifetime.SetActiveFileSystem(selected);
        LaunchRequest unsupported = NewFolderRequest("/Folder",
            "gxos.shell.this-system");
        Check(!ShellFolderMountBinding.TryBindFolderRequest(unsupported,
                  out failure) && failure != null &&
                  failure.ErrorCode == LaunchErrorCode.UnsupportedTarget &&
                  selected.LeaseCount == 0,
              "production folder binding does not map unsupported Server roots");
        unsupported.Dispose();

        FileSystemMountLifetime.SetActiveFileSystem(null);
        selected.Dispose();
        replacement.Dispose();
    }

    private static LaunchRequest NewFolderRequest(string path,
            string shellObjectId = "gxos.shell.root") {
        return new LaunchRequest {
            TargetKind = LaunchRequestTargetKind.ShellObject,
            ShellTargetKind = ApplicationShellTargetKind.FileSystem,
            SourceShellObjectId = shellObjectId,
            TargetAppId = "gxos.builtin.files",
            ShellTargetValue = path
        };
    }

    private static void TestMountLifetime() {
        FileSystemMountLease missingLease;
        Check(FileSystemMountLifetime.TryAcquireActive(out missingLease) ==
                  FileSystemMountAcquireResult.Unavailable && missingLease == null,
              "mount acquisition fails when no active filesystem exists");

        var first = new FileSystem();
        FileSystemMountLifetime.SetActiveFileSystem(first);
        FileSystemMountLease firstLease;
        FileSystemMountLease sharedLease;
        bool acquiredFirst = FileSystemMountLifetime.TryAcquireActive(
            out firstLease) == FileSystemMountAcquireResult.Acquired;
        bool acquiredShared = FileSystemMountLifetime.TryAcquireActive(
            out sharedLease) == FileSystemMountAcquireResult.Acquired;
        Check(acquiredFirst && acquiredShared && first.LeaseCount == 2,
              "two folder owners acquire independent leases on one mount");

        ulong firstGeneration = firstLease == null ? 0 : firstLease.Generation;
        var replacement = new FileSystem();
        FileSystemMountLifetime.SetActiveFileSystem(replacement);
        Check(firstLease != null && firstLease.IsValid &&
                  !firstLease.IsCurrentMount &&
                  FileSystemMountLifetime.ActiveFileSystem == replacement &&
                  firstGeneration != 0 &&
                  firstLease.Generation == firstGeneration,
              "replacement retires the old generation without invalidating leases");

        first.Dispose();
        Check(!first.IsDisposed && first.LeaseCount == 2 &&
                  firstLease.IsValid && sharedLease.IsValid,
              "mount disposal waits while folder owners retain leases");

        FileSystemMountLease clonedLease;
        Check(firstLease.TryClone(out clonedLease) &&
                  clonedLease != null && first.LeaseCount == 3,
              "an existing lease can retain a retired mount for a child operation");

        firstLease.Dispose();
        firstLease.Dispose();
        Check(first.LeaseCount == 2 && !first.IsDisposed,
              "repeated release is idempotent and cannot go negative");
        sharedLease.Dispose();
        Check(first.LeaseCount == 1 && !first.IsDisposed,
              "closing one owner leaves another owner's mount usable");
        clonedLease.Dispose();
        Check(first.LeaseCount == 0 && first.IsDisposed &&
                  first.DisposalCount == 1 && !clonedLease.IsValid,
              "final release completes deferred disposal exactly once");
        first.Dispose();
        Check(first.DisposalCount == 1,
              "repeated filesystem disposal does not dispose twice");

        replacement.Dispose();
        FileSystemMountLease unavailable;
        Check(FileSystemMountLifetime.TryAcquireActive(out unavailable) ==
                  FileSystemMountAcquireResult.Unavailable && unavailable == null,
              "a disposed active mount cannot be acquired again");

        bool cyclesClean = true;
        for (int i = 0; i < 64; i++) {
            var cycle = new FileSystem();
            FileSystemMountLifetime.SetActiveFileSystem(cycle);
            FileSystemMountLease cycleLease;
            if (FileSystemMountLifetime.TryAcquireActive(out cycleLease) !=
                    FileSystemMountAcquireResult.Acquired || cycleLease == null) {
                cyclesClean = false;
                break;
            }
            FileSystemMountLifetime.SetActiveFileSystem(null);
            cycleLease.Dispose();
            cycle.Dispose();
            if (cycle.LeaseCount != 0 || !cycle.IsDisposed ||
                    cycle.DisposalCount != 1) cyclesClean = false;
        }
        Check(cyclesClean,
              "repeated acquire/replace/release cycles leave no retained leases");
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
