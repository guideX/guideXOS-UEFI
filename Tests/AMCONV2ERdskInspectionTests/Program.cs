using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.OS;

internal static unsafe class Program {
    private static int _passed;
    private static int _failed;

    private static int Main() {
        byte[] image = CreateRdsk("root.txt", "Folder/child.txt",
            "Nested/Child/readme.md", "plain.txt");
        CheckStatus(image, "", FileSystemDirectoryInspectionStatus.Directory,
            "a complete RDSK image proves its implicit root");
        CheckStatus(image, "Folder", FileSystemDirectoryInspectionStatus.Directory,
            "a file path proves its implicit parent directory");
        CheckStatus(image, "Nested", FileSystemDirectoryInspectionStatus.Directory,
            "nested parent directory is detected");
        CheckStatus(image, "Nested/Child",
            FileSystemDirectoryInspectionStatus.Directory,
            "nested directory is detected without enumerating contents");
        CheckStatus(image, "plain.txt",
            FileSystemDirectoryInspectionStatus.NotDirectory,
            "an existing file is not reported as a directory");
        CheckStatus(image, "plain.txt/child",
            FileSystemDirectoryInspectionStatus.NotDirectory,
            "a file ancestor blocks a nested directory path");
        CheckStatus(image, "Missing", FileSystemDirectoryInspectionStatus.NotFound,
            "a complete scan proves a missing path");
        CheckStatus(image, "Nested/Empty",
            FileSystemDirectoryInspectionStatus.NotFound,
            "RDSK v1 cannot represent a named empty subdirectory");

        byte[] emptyImage = CreateRdsk();
        CheckStatus(emptyImage, "", FileSystemDirectoryInspectionStatus.Directory,
            "a valid zero-file RDSK image still has an empty root directory");
        CheckStatus(emptyImage, "Empty",
            FileSystemDirectoryInspectionStatus.NotFound,
            "an unencoded empty child directory is not guessed to exist");

        CheckStatus(image, "A/../B", FileSystemDirectoryInspectionStatus.InvalidPath,
            "path traversal is rejected before image inspection");
        CheckStatus(image, "A\\B", FileSystemDirectoryInspectionStatus.InvalidPath,
            "backslash paths are rejected");
        CheckStatus(image, "\u00e9", FileSystemDirectoryInspectionStatus.Indeterminate,
            "non-ASCII lookup remains indeterminate under the ASCII RdskFS reader");
        CheckStatus(null, "", FileSystemDirectoryInspectionStatus.FilesystemUnavailable,
            "a missing image pointer reports filesystem unavailable");
        CheckStatus(image, "", FileSystemDirectoryInspectionStatus.Indeterminate,
            "an unknown image length cannot prove the root", 0);

        byte[] truncated = new byte[image.Length - 1];
        Array.Copy(image, truncated, truncated.Length);
        CheckStatus(truncated, "", FileSystemDirectoryInspectionStatus.Indeterminate,
            "truncated record data is not misreported as a valid root");

        byte[] unsupportedVersion = (byte[])image.Clone();
        unsupportedVersion[4] = 2;
        CheckStatus(unsupportedVersion, "",
            FileSystemDirectoryInspectionStatus.Indeterminate,
            "unsupported RDSK versions remain indeterminate");

        byte[] invalidRecordPath = CreateRdsk("../escape");
        CheckStatus(invalidRecordPath, "",
            FileSystemDirectoryInspectionStatus.Indeterminate,
            "malformed packed paths invalidate the complete scan");

        byte[] conflictingKinds = CreateRdsk("A", "A/child.txt");
        CheckStatus(conflictingKinds, "A",
            FileSystemDirectoryInspectionStatus.Indeterminate,
            "conflicting file and directory records remain indeterminate");

        byte[] excessiveCount = (byte[])emptyImage.Clone();
        excessiveCount[8] = 0x11;
        excessiveCount[9] = 0x27;
        excessiveCount[10] = 0;
        excessiveCount[11] = 0;
        CheckStatus(excessiveCount, "",
            FileSystemDirectoryInspectionStatus.Indeterminate,
            "file counts above the existing 10,000-entry bound are rejected");

        byte[] unchanged = (byte[])image.Clone();
        using (var sha = System.Security.Cryptography.SHA256.Create()) {
            byte[] before = sha.ComputeHash(unchanged);
            fixed (byte* pointer = image) {
                RdskFSDirectoryInspector.Inspect(pointer,
                    (ulong)image.Length, "Folder");
            }
            byte[] after = sha.ComputeHash(image);
            Check(BytesEqual(before, after),
                "inspection leaves the RDSK image unchanged");
        }

        fixed (byte* pointer = image) {
            RdskFSDirectoryInspector.Inspect(pointer,
                (ulong)image.Length, "Folder");
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            FileSystemDirectoryInspection result =
                FileSystemDirectoryInspection.Create(
                    FileSystemDirectoryInspectionStatus.Indeterminate);
            for (int i = 0; i < 1000; i++) {
                result = RdskFSDirectoryInspector.Inspect(pointer,
                    (ulong)image.Length, "Folder");
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() -
                allocatedBefore;
            Check(result.Status == FileSystemDirectoryInspectionStatus.Directory &&
                  allocated == 0,
                "1,000 repeated inspections retain no state or allocate memory");
        }

        TestLeasedRootActivation(image);

        Console.WriteLine("AMCONV2E_RDSK_INSPECTION_ASSERTIONS=" + _passed);
        Console.WriteLine("AMCONV2E_RDSK_INSPECTION_FAILURES=" + _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void TestLeasedRootActivation(byte[] image) {
        var mountedImage = new PinnedRdskImage(image);
        var mounted = new RdskFS();
        FileSystemMountLifetime.SetActiveFileSystem(mounted);

        List<FileInfo> rootEntries = mounted.GetFiles("/");
        bool legacyListingPreserved = ContainsEntry(rootEntries, "Folder",
                FileAttribute.Directory) &&
            ContainsEntry(rootEntries, "root.txt", FileAttribute.Archive);
        Check(legacyListingPreserved,
            "production RdskFS GetFiles remains usable with the typed inspector");
        for (int i = 0; i < rootEntries.Count; i++) rootEntries[i].Dispose();
        HostedDisposableExtensions.Dispose(rootEntries);

        LaunchRequest root = NewFolderRequest("/");
        LaunchResult failure;
        bool bound = ShellFolderMountBinding.TryBindFolderRequest(root,
            out failure);
        Check(bound && failure == null && root.FileSystemMountLease != null &&
              root.FileSystemMountLease.FileSystem == mounted &&
              FileSystemMountLifetime.ActiveFileSystem == mounted,
            "C# Root uses the production RdskFS proof and leaves the active mount unchanged");

        LaunchRequest folder = NewFolderRequest("/Folder");
        bool folderBound = ShellFolderMountBinding.TryBindFolderRequest(folder,
            out failure);
        Check(folderBound && failure == null &&
              folder.FileSystemMountLease != null &&
              folder.FileSystemMountLease.FileSystem == mounted,
            "a nested typed folder binds against the production RdskFS instance");

        LaunchRequest missing = NewFolderRequest("/Missing");
        bool missingBound = ShellFolderMountBinding.TryBindFolderRequest(missing,
            out failure);
        Check(!missingBound && failure != null &&
              failure.ErrorCode == LaunchErrorCode.ResourceUnavailable &&
              mounted.LeaseCount == 2,
            "a missing typed folder fails without leaking the acquired mount lease");
        missing.Dispose();

        var replacementImage = new PinnedRdskImage(new byte[] {
            (byte)'R', (byte)'D', (byte)'S', (byte)'K'
        });
        var replacement = new RdskFS();
        FileSystemMountLifetime.SetActiveFileSystem(replacement);
        mounted.Dispose();
        Check(!mounted.IsDisposed && root.FileSystemMountLease.IsValid,
            "an outstanding Root lease keeps a replaced RDSK mount alive");
        FileSystem resolved;
        string resolvedPath;
        bool resolvedOnLease = ShellFolderMountBinding.TryResolveFolderRequest(
            root, out resolved, out resolvedPath, out failure);
        Check(resolvedOnLease && resolved == mounted && resolvedPath == string.Empty,
            "the retained Root request re-inspects the original RdskFS image");
        if (resolvedPath != null) HostedDisposableExtensions.Dispose(resolvedPath);
        bool folderOnLease = ShellFolderMountBinding.TryResolveFolderRequest(
            folder, out resolved, out resolvedPath, out failure);
        Check(folderOnLease && resolved == mounted && resolvedPath == "Folder",
            "folder reinspection cannot follow a replaced ramdisk pointer");

        if (resolvedPath != null) HostedDisposableExtensions.Dispose(resolvedPath);
        root.Dispose();
        root.Dispose();
        folder.Dispose();
        Check(mounted.LeaseCount == 0 && mounted.IsDisposed &&
              mounted.DisposalCount == 1,
            "multiple RDSK folder leases release once after their windows close");
        FileSystemMountLifetime.SetActiveFileSystem(null);
        mountedImage.Dispose();
        replacement.Dispose();
        replacementImage.Dispose();

        var invalidImage = new PinnedRdskImage(new byte[] {
            (byte)'R', (byte)'D', (byte)'S', (byte)'K'
        });
        var invalidMount = new RdskFS();
        FileSystemMountLifetime.SetActiveFileSystem(invalidMount);
        LaunchRequest invalidRoot = NewFolderRequest("/");
        bool invalidRootBound = ShellFolderMountBinding.TryBindFolderRequest(
            invalidRoot, out failure);
        Check(!invalidRootBound && failure != null &&
              failure.ErrorCode == LaunchErrorCode.BackendUnavailable &&
              invalidMount.LeaseCount == 0,
            "an indeterminate RDSK root is rejected and releases its lease");
        invalidRoot.Dispose();
        FileSystemMountLifetime.SetActiveFileSystem(null);
        invalidMount.Dispose();
        invalidImage.Dispose();

        Ramdisk.RawBasePointer = null;
        Ramdisk.RawImageSizeBytes = 0;
        var unavailableMount = new RdskFS();
        FileSystemMountLifetime.SetActiveFileSystem(unavailableMount);
        LaunchRequest unavailableRoot = NewFolderRequest("/");
        bool unavailableRootBound =
            ShellFolderMountBinding.TryBindFolderRequest(unavailableRoot,
                out failure);
        Check(!unavailableRootBound && failure != null &&
              failure.ErrorCode == LaunchErrorCode.ResourceUnavailable &&
              unavailableMount.LeaseCount == 0,
            "an unavailable RDSK image fails Root activation without a lease leak");
        unavailableRoot.Dispose();
        FileSystemMountLifetime.SetActiveFileSystem(null);
        unavailableMount.Dispose();
    }

    private static bool ContainsEntry(List<FileInfo> entries, string name,
                                      FileAttribute attribute) {
        for (int i = 0; i < entries.Count; i++) {
            if (entries[i].Name == name && entries[i].Attribute == attribute)
                return true;
        }
        return false;
    }

    private static LaunchRequest NewFolderRequest(string path) {
        return new LaunchRequest {
            TargetKind = LaunchRequestTargetKind.ShellObject,
            ShellTargetKind = ApplicationShellTargetKind.FileSystem,
            SourceShellObjectId = "gxos.shell.root",
            TargetAppId = "gxos.builtin.files",
            ShellTargetValue = path
        };
    }

    private static void CheckStatus(byte[] image, string path,
            FileSystemDirectoryInspectionStatus expected, string label,
            ulong? explicitSize = null) {
        FileSystemDirectoryInspection actual;
        if (image == null) {
            actual = RdskFSDirectoryInspector.Inspect(null, 0, path);
        } else {
            fixed (byte* pointer = image) {
                actual = RdskFSDirectoryInspector.Inspect(pointer,
                    explicitSize.HasValue ? explicitSize.Value :
                        (ulong)image.Length, path);
            }
        }
        Check(actual.Status == expected,
            label + " (expected=" + expected + ", actual=" + actual.Status + ")");
    }

    private static byte[] CreateRdsk(params string[] paths) {
        var bytes = new List<byte>();
        bytes.Add((byte)'R');
        bytes.Add((byte)'D');
        bytes.Add((byte)'S');
        bytes.Add((byte)'K');
        AddUInt32(bytes, 1);
        AddUInt32(bytes, (uint)paths.Length);
        for (int i = 0; i < paths.Length; i++) {
            byte[] path = Encoding.ASCII.GetBytes(paths[i]);
            AddUInt16(bytes, (ushort)path.Length);
            bytes.AddRange(path);
            AddUInt32(bytes, 0);
        }
        return bytes.ToArray();
    }

    private static void AddUInt16(List<byte> bytes, ushort value) {
        bytes.Add((byte)value);
        bytes.Add((byte)(value >> 8));
    }

    private static void AddUInt32(List<byte> bytes, uint value) {
        bytes.Add((byte)value);
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)(value >> 16));
        bytes.Add((byte)(value >> 24));
    }

    private static bool BytesEqual(byte[] left, byte[] right) {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++) {
            if (left[i] != right[i]) return false;
        }
        return true;
    }

    private static void Check(bool condition, string label) {
        if (condition) {
            _passed++;
            Console.WriteLine("AMCONV2E_RDSK_CHECK=PASS: " + label);
        } else {
            _failed++;
            Console.WriteLine("AMCONV2E_RDSK_CHECK=FAIL: " + label);
        }
    }

    private sealed class PinnedRdskImage : IDisposable {
        private GCHandle _pin;
        private readonly byte* _pointer;

        internal PinnedRdskImage(byte[] image) {
            if (image == null) throw new ArgumentNullException("image");
            _pin = GCHandle.Alloc(image, GCHandleType.Pinned);
            _pointer = (byte*)_pin.AddrOfPinnedObject();
            Ramdisk.RawBasePointer = _pointer;
            Ramdisk.RawImageSizeBytes = (ulong)image.Length;
        }

        public void Dispose() {
            if (Ramdisk.RawBasePointer == _pointer) {
                Ramdisk.RawBasePointer = null;
                Ramdisk.RawImageSizeBytes = 0;
            }
            if (_pin.IsAllocated) _pin.Free();
        }
    }
}
