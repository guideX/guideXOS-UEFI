using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.OS;
using FileInfo = guideXOS.FS.FileInfo;

internal static unsafe class Program {
    private static int _passed;
    private static int _failed;

    private static int Main(string[] args) {
        if (args == null || args.Length != 3) {
            Console.Error.WriteLine("Usage: AMCONV2FRdskV2Tests <v2> <v1> <empty-v2>");
            return 2;
        }

        byte[] v2 = System.IO.File.ReadAllBytes(args[0]);
        byte[] v1 = System.IO.File.ReadAllBytes(args[1]);
        byte[] emptyV2 = System.IO.File.ReadAllBytes(args[2]);

        TestV2Inspection(v2, emptyV2);
        TestV2OperationsAndFolderBinding(v2, v1);
        TestV1Compatibility(v1);

        Console.WriteLine("AMCONV2F_RDSK_V2_ASSERTIONS=" + _passed);
        Console.WriteLine("AMCONV2F_RDSK_V2_FAILURES=" + _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void TestV2Inspection(byte[] image, byte[] emptyImage) {
        CheckStatus(image, "", FileSystemDirectoryInspectionStatus.Directory,
            "a generator-produced v2 image explicitly contains root");
        CheckStatus(image, "Empty", FileSystemDirectoryInspectionStatus.Directory,
            "a named empty directory is present in v2 metadata");
        CheckStatus(image, "empty", FileSystemDirectoryInspectionStatus.NotFound,
            "v2 path lookup is case-sensitive");
        CheckStatus(image, "Nested", FileSystemDirectoryInspectionStatus.Directory,
            "a directory with no direct files is present");
        CheckStatus(image, "Nested/DeepEmpty",
            FileSystemDirectoryInspectionStatus.Directory,
            "a nested empty directory is present");
        CheckStatus(image, "WithFiles",
            FileSystemDirectoryInspectionStatus.Directory,
            "a directory containing files is present");
        CheckStatus(image, "WithFiles/Child/leaf.txt",
            FileSystemDirectoryInspectionStatus.NotDirectory,
            "a nested file is distinguished from a directory");
        CheckStatus(image, "TopFile.bin",
            FileSystemDirectoryInspectionStatus.NotDirectory,
            "a root file is distinguished from a directory");
        CheckStatus(image, "TopFile.bin/child",
            FileSystemDirectoryInspectionStatus.NotDirectory,
            "a file cannot be traversed as a directory parent");
        CheckStatus(image, "Missing", FileSystemDirectoryInspectionStatus.NotFound,
            "a complete valid v2 image proves a missing directory");
        CheckStatus(image, "A/../B", FileSystemDirectoryInspectionStatus.InvalidPath,
            "parent traversal is rejected before lookup");
        CheckStatus(image, "A//B", FileSystemDirectoryInspectionStatus.InvalidPath,
            "repeated separators are rejected");
        CheckStatus(image, "C:\\escape",
            FileSystemDirectoryInspectionStatus.InvalidPath,
            "backslash paths are rejected");
        CheckStatus(image, "caf\u00e9",
            FileSystemDirectoryInspectionStatus.Indeterminate,
            "the v2 ASCII subset rejects non-ASCII lookup without replacement");
        CheckStatus(emptyImage, "", FileSystemDirectoryInspectionStatus.Directory,
            "an empty-source v2 image still has one explicit root record");
        CheckStatus(emptyImage, "Empty",
            FileSystemDirectoryInspectionStatus.NotFound,
            "an absent directory is distinct from an explicit empty directory");

        CheckInvalidMutation(image, "checksum", bytes => bytes[bytes.Length - 1] ^= 1,
            "payload corruption without a checksum update is rejected");
        CheckInvalidMutation(image, "header flags", bytes => {
            WriteUInt32(bytes, 12, 1);
            RecomputeChecksum(bytes);
        }, "unknown header flags are rejected even with a correct checksum");
        CheckInvalidMutation(image, "header size", bytes => {
            WriteUInt32(bytes, 8, 63);
            RecomputeChecksum(bytes);
        }, "a noncanonical fixed header size is rejected");
        CheckInvalidMutation(image, "record table offset", bytes => {
            WriteUInt64(bytes, 36, ulong.MaxValue);
            RecomputeChecksum(bytes);
        }, "overflowing record table offsets are rejected before reading records");
        CheckInvalidMutation(image, "unknown version", bytes => {
            WriteUInt32(bytes, 4, 3);
        }, "unknown versions never fall back to v1 parsing");
        byte[] truncated = new byte[image.Length - 1];
        Array.Copy(image, truncated, truncated.Length);
        CheckStatus(truncated, "", FileSystemDirectoryInspectionStatus.Indeterminate,
            "a truncated image is rejected");
        CheckInvalidMutation(image, "excessive records", bytes => {
            WriteUInt32(bytes, 24, 4097);
            RecomputeChecksum(bytes);
        }, "record counts above the v2 bound are rejected");
        CheckInvalidMutation(image, "duplicate directory", bytes => {
            SetRecordName(bytes, FindRecord(bytes, "Spare"), "Empty");
            RecomputeChecksum(bytes);
        }, "duplicate directory siblings are rejected");
        CheckInvalidMutation(image, "file-directory collision", bytes => {
            SetRecordName(bytes, FindRecord(bytes, "Note"), "Data");
            RecomputeChecksum(bytes);
        }, "a file and directory cannot share one parent/name pair");
        CheckInvalidMutation(image, "invalid parent", bytes => {
            int directory = FindRecord(bytes, "DeepEmpty");
            WriteUInt32(bytes, directory + 4,
                (uint)FindRecordIndex(bytes, "Note"));
            RecomputeChecksum(bytes);
        }, "a directory cannot name a file as its parent");
        CheckInvalidMutation(image, "cyclic parent", bytes => {
            int record = FindRecord(bytes, "DeepEmpty");
            WriteUInt32(bytes, record + 4,
                (uint)FindRecordIndex(bytes, "DeepEmpty"));
            RecomputeChecksum(bytes);
        }, "self-parent and cyclic directory references are rejected");
        CheckInvalidMutation(image, "unsupported encoding", bytes => {
            int record = FindRecord(bytes, "Empty");
            bytes[record + 28] = 0xc3;
            RecomputeChecksum(bytes);
        }, "non-ASCII and malformed UTF-8 name bytes are rejected");
        CheckInvalidMutation(image, "offset overflow", bytes => {
            int record = FindRecord(bytes, "TopFile.bin");
            WriteUInt64(bytes, record + 8, ulong.MaxValue);
            RecomputeChecksum(bytes);
        }, "overflowing data offsets are rejected before pointer access");
        CheckInvalidMutation(image, "invalid data extent", bytes => {
            int record = FindRecord(bytes, "TopFile.bin");
            WriteUInt64(bytes, record + 16, (ulong)bytes.Length + 1);
            RecomputeChecksum(bytes);
        }, "file data extents outside the image are rejected");
        CheckInvalidMutation(image, "corrupted metadata", bytes => {
            int record = FindRecord(bytes, "Spare");
            bytes[record] = 9;
            RecomputeChecksum(bytes);
        }, "invalid record types are rejected even with a correct checksum");
        CheckInvalidMutation(image, "reserved record flags", bytes => {
            int record = FindRecord(bytes, "Spare");
            bytes[record + 1] = 1;
            RecomputeChecksum(bytes);
        }, "unknown record flags and reserved values are rejected");
        byte[] extended = new byte[image.Length + 1];
        Array.Copy(image, extended, image.Length);
        WriteUInt64(extended, 16, (ulong)extended.Length);
        RecomputeChecksum(extended);
        CheckStatus(extended, "", FileSystemDirectoryInspectionStatus.Indeterminate,
            "unclaimed trailing bytes are rejected by exact image-length validation");

        byte[] overlong = CreateOverlongPathFixture(emptyImage);
        CheckStatus(overlong, "", FileSystemDirectoryInspectionStatus.Indeterminate,
            "a checksum-correct image with a path over 1,023 bytes is rejected");
    }

    private static void TestV2OperationsAndFolderBinding(byte[] image,
                                                          byte[] replacementImage) {
        var imagePin = new PinnedRdskImage(image);
        var mounted = new RdskFS();
        FileSystemMountLifetime.SetActiveFileSystem(mounted);

        List<FileInfo> root = mounted.GetFiles("/");
        Check(ContainsEntry(root, "Empty", FileAttribute.Directory) &&
              ContainsEntry(root, "Nested", FileAttribute.Directory) &&
              ContainsEntry(root, "TopFile.bin", FileAttribute.Archive),
            "production GetFiles enumerates explicit v2 directories and files");
        DisposeEntries(root);

        List<FileInfo> empty = mounted.GetFiles("Empty");
        Check(empty.Count == 0 && mounted.InspectDirectory("Empty").Status ==
              FileSystemDirectoryInspectionStatus.Directory,
            "an empty listing still resolves as an existing directory");
        DisposeEntries(empty);

        List<FileInfo> withFiles = mounted.GetFiles("WithFiles");
        Check(ContainsEntry(withFiles, "Child", FileAttribute.Directory) &&
              ContainsEntry(withFiles, "inside.bin", FileAttribute.Archive),
            "production enumeration returns direct files and child directories");
        DisposeEntries(withFiles);

        List<FileInfo> rootWithZero = mounted.GetFiles("");
        FileInfo zeroEntry = FindEntry(rootWithZero, "Zero.bin");
        Check(zeroEntry != null && zeroEntry.Attribute == FileAttribute.Archive &&
              zeroEntry.Param1 == 0,
            "zero-length file metadata remains distinct from directory metadata");
        DisposeEntries(rootWithZero);

        List<FileInfo> nestedEmpty = mounted.GetFiles("Nested/DeepEmpty");
        Check(nestedEmpty.Count == 0 && mounted.InspectDirectory(
                "Nested/DeepEmpty").Status ==
                FileSystemDirectoryInspectionStatus.Directory,
            "a nested empty directory can be enumerated and inspected");
        DisposeEntries(nestedEmpty);

        byte[] payload = mounted.ReadAllBytes("TopFile.bin");
        Check(BytesEqual(payload, new byte[] { 0, 1, 2, 255, 13 }),
            "production ReadAllBytes returns a nonzero v2 payload");
        byte[] zero = mounted.ReadAllBytes("Zero.bin");
        Check(zero != null && zero.Length == 0,
            "a zero-length v2 file remains a file and reads as zero bytes");
        Check(BytesEqual(mounted.ReadAllBytes("WithFiles/inside.bin"),
                Encoding.ASCII.GetBytes("inside payload")),
            "nested v2 file retrieval follows explicit parent records");

        LaunchResult failure;
        LaunchRequest rootRequest = NewFolderRequest("/");
        bool rootBound = ShellFolderMountBinding.TryBindFolderRequest(
            rootRequest, out failure);
        Check(rootBound && failure == null && rootRequest.FileSystemMountLease != null &&
              rootRequest.FileSystemMountLease.FileSystem == mounted,
            "the production C# Root binding accepts a verified v2 root");

        LaunchRequest emptyRequest = NewFolderRequest("/Empty");
        bool emptyBound = ShellFolderMountBinding.TryBindFolderRequest(
            emptyRequest, out failure);
        Check(emptyBound && failure == null &&
              emptyRequest.FileSystemMountLease.FileSystem == mounted,
            "typed folder activation accepts an explicitly named empty directory");

        LaunchRequest filesRequest = NewFolderRequest("/WithFiles");
        bool filesBound = ShellFolderMountBinding.TryBindFolderRequest(
            filesRequest, out failure);
        Check(filesBound && failure == null &&
              filesRequest.FileSystemMountLease.FileSystem == mounted,
            "typed folder activation accepts a directory containing files");

        LaunchRequest fileRequest = NewFolderRequest("/TopFile.bin");
        bool fileBound = ShellFolderMountBinding.TryBindFolderRequest(
            fileRequest, out failure);
        Check(!fileBound && failure != null &&
              failure.ErrorCode == LaunchErrorCode.ResourceUnavailable &&
              mounted.LeaseCount == 3,
            "a file target fails folder activation without a lease leak");
        fileRequest.Dispose();

        LaunchRequest missingRequest = NewFolderRequest("/Missing");
        bool missingBound = ShellFolderMountBinding.TryBindFolderRequest(
            missingRequest, out failure);
        Check(!missingBound && failure != null &&
              failure.ErrorCode == LaunchErrorCode.ResourceUnavailable &&
              mounted.LeaseCount == 3,
            "a missing v2 path produces the existing typed unavailable result");
        missingRequest.Dispose();

        var replacementPin = new PinnedRdskImage(replacementImage);
        var replacement = new RdskFS();
        FileSystemMountLifetime.SetActiveFileSystem(replacement);
        mounted.Dispose();
        Check(!mounted.IsDisposed && mounted.LeaseCount == 3,
            "outstanding v2 folder leases defer disposal after mount replacement");

        FileSystem resolved;
        string resolvedPath;
        bool resolvedOk = ShellFolderMountBinding.TryResolveFolderRequest(
            emptyRequest, out resolved, out resolvedPath, out failure);
        Check(resolvedOk && resolved == mounted && resolvedPath == "Empty" &&
              FileSystemMountLifetime.ActiveFileSystem == replacement,
            "factory reinspection keeps the same leased v2 mount after replacement");
        if (resolvedPath != null) HostedDisposableExtensions.Dispose(resolvedPath);

        rootRequest.Dispose();
        emptyRequest.Dispose();
        filesRequest.Dispose();
        Check(mounted.IsDisposed && mounted.DisposalCount == 1,
            "the final v2 folder lease completes mount disposal exactly once");
        Check(FileSystemMountLifetime.ActiveFileSystem == replacement,
            "folder binding and release do not replace the global active mount");

        FileSystemMountLifetime.SetActiveFileSystem(null);
        replacement.Dispose();
        imagePin.Dispose();
        replacementPin.Dispose();

        byte[] corrupt = (byte[])image.Clone();
        corrupt[corrupt.Length - 1] ^= 1;
        var corruptPin = new PinnedRdskImage(corrupt);
        var corruptMount = new RdskFS();
        FileSystemMountLifetime.SetActiveFileSystem(corruptMount);
        List<FileInfo> rejectedEntries = corruptMount.GetFiles("/");
        Check(rejectedEntries.Count == 0 &&
              corruptMount.InspectDirectory("").Status ==
                FileSystemDirectoryInspectionStatus.Indeterminate,
            "a malformed image exposes no partial v2 directory listing");
        DisposeEntries(rejectedEntries);
        LaunchRequest malformedRoot = NewFolderRequest("/");
        bool malformedBound = ShellFolderMountBinding.TryBindFolderRequest(
            malformedRoot, out failure);
        Check(!malformedBound && failure != null &&
              failure.ErrorCode == LaunchErrorCode.BackendUnavailable &&
              corruptMount.LeaseCount == 0,
            "a malformed v2 image cannot produce a successful folder launch");
        malformedRoot.Dispose();
        FileSystemMountLifetime.SetActiveFileSystem(null);
        corruptMount.Dispose();
        corruptPin.Dispose();
    }

    private static void TestV1Compatibility(byte[] image) {
        var imagePin = new PinnedRdskImage(image);
        var mounted = new RdskFS();
        CheckStatus(image, "", FileSystemDirectoryInspectionStatus.Directory,
            "legacy v1 images remain readable");
        CheckStatus(image, "WithFiles", FileSystemDirectoryInspectionStatus.Directory,
            "v1 implicit parent directories remain recognized");
        CheckStatus(image, "Empty", FileSystemDirectoryInspectionStatus.NotFound,
            "v1 keeps its historical inability to encode named empty directories");
        Check(BytesEqual(mounted.ReadAllBytes("TopFile.bin"),
                new byte[] { 0, 1, 2, 255, 13 }),
            "production v1 file retrieval remains compatible");
        List<FileInfo> root = mounted.GetFiles("/");
        Check(ContainsEntry(root, "WithFiles", FileAttribute.Directory) &&
              ContainsEntry(root, "TopFile.bin", FileAttribute.Archive) &&
              !ContainsEntry(root, "Empty", FileAttribute.Directory),
            "v1 enumeration retains implicit directories and omits empty ones");
        DisposeEntries(root);
        mounted.Dispose();
        imagePin.Dispose();
    }

    private static void CheckInvalidMutation(byte[] source, string name,
            Action<byte[]> mutation, string label) {
        byte[] changed = (byte[])source.Clone();
        mutation(changed);
        CheckStatus(changed, "", FileSystemDirectoryInspectionStatus.Indeterminate,
            label + " [" + name + "]");
    }

    private static byte[] CreateOverlongPathFixture(byte[] emptyV2) {
        // This is a negative-only malformed fixture derived from the generated
        // empty image. Positive v2 images always come from ramdisk_builder.py.
        const int segmentLength = 205;
        const int nestedDirectories = 5;
        var records = new List<byte>();
        for (int i = 0; i < 28; i++) records.Add(emptyV2[64 + i]);
        byte[] name = new byte[segmentLength];
        for (int i = 0; i < name.Length; i++) name[i] = (byte)'A';
        for (int i = 1; i <= nestedDirectories; i++) {
            records.Add(1);
            records.Add(0);
            AddUInt16(records, segmentLength);
            AddUInt32(records, (uint)(i - 1));
            AddUInt64(records, 0);
            AddUInt64(records, 0);
            AddUInt32(records, 0);
            records.AddRange(name);
        }

        ulong imageLength = 64UL + (ulong)records.Count;
        byte[] result = new byte[(int)imageLength];
        Array.Copy(emptyV2, result, 64);
        for (int i = 0; i < records.Count; i++) result[64 + i] = records[i];
        WriteUInt32(result, 4, 2);
        WriteUInt32(result, 8, 64);
        WriteUInt32(result, 12, 0);
        WriteUInt64(result, 16, imageLength);
        WriteUInt32(result, 24, nestedDirectories + 1);
        WriteUInt32(result, 28, nestedDirectories + 1);
        WriteUInt32(result, 32, 0);
        WriteUInt64(result, 36, 64);
        WriteUInt64(result, 44, (ulong)records.Count);
        WriteUInt64(result, 52, imageLength);
        RecomputeChecksum(result);
        return result;
    }

    private static int FindRecord(byte[] image, string name) {
        uint count = ReadUInt32(image, 24);
        int cursor = (int)ReadUInt64(image, 36);
        for (uint i = 0; i < count; i++) {
            ushort nameLength = ReadUInt16(image, cursor + 2);
            string candidate = Encoding.ASCII.GetString(image, cursor + 28,
                nameLength);
            if (candidate == name) return cursor;
            cursor += 28 + nameLength;
        }
        throw new InvalidOperationException("Record was not found: " + name);
    }

    private static int FindRecordIndex(byte[] image, string name) {
        uint count = ReadUInt32(image, 24);
        int cursor = (int)ReadUInt64(image, 36);
        for (uint i = 0; i < count; i++) {
            ushort nameLength = ReadUInt16(image, cursor + 2);
            string candidate = Encoding.ASCII.GetString(image, cursor + 28,
                nameLength);
            if (candidate == name) return (int)i;
            cursor += 28 + nameLength;
        }
        throw new InvalidOperationException("Record was not found: " + name);
    }

    private static void SetRecordName(byte[] image, int recordOffset,
                                      string value) {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length != ReadUInt16(image, recordOffset + 2))
            throw new InvalidOperationException("Mutation must preserve record size");
        for (int i = 0; i < bytes.Length; i++) image[recordOffset + 28 + i] = bytes[i];
    }

    private static void CheckStatus(byte[] image, string path,
            FileSystemDirectoryInspectionStatus expected, string label) {
        FileSystemDirectoryInspection actual;
        fixed (byte* pointer = image) {
            actual = RdskFSDirectoryInspector.Inspect(pointer,
                (ulong)image.Length, path);
        }
        Check(actual.Status == expected,
            label + " (expected=" + expected + ", actual=" + actual.Status + ")");
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

    private static bool ContainsEntry(List<FileInfo> entries, string name,
                                      FileAttribute attribute) {
        for (int i = 0; i < entries.Count; i++) {
            if (entries[i].Name == name && entries[i].Attribute == attribute)
                return true;
        }
        return false;
    }

    private static FileInfo FindEntry(List<FileInfo> entries, string name) {
        for (int i = 0; i < entries.Count; i++) {
            if (entries[i].Name == name) return entries[i];
        }
        return null;
    }

    private static void DisposeEntries(List<FileInfo> entries) {
        for (int i = 0; i < entries.Count; i++) entries[i].Dispose();
        HostedDisposableExtensions.Dispose(entries);
    }

    private static bool BytesEqual(byte[] left, byte[] right) {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++) {
            if (left[i] != right[i]) return false;
        }
        return true;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset) {
        return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
    }

    private static uint ReadUInt32(byte[] bytes, int offset) {
        return (uint)bytes[offset] | ((uint)bytes[offset + 1] << 8) |
            ((uint)bytes[offset + 2] << 16) |
            ((uint)bytes[offset + 3] << 24);
    }

    private static ulong ReadUInt64(byte[] bytes, int offset) {
        return (ulong)ReadUInt32(bytes, offset) |
            ((ulong)ReadUInt32(bytes, offset + 4) << 32);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value) {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) {
        WriteUInt32(bytes, offset, (uint)value);
        WriteUInt32(bytes, offset + 4, (uint)(value >> 32));
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

    private static void AddUInt64(List<byte> bytes, ulong value) {
        AddUInt32(bytes, (uint)value);
        AddUInt32(bytes, (uint)(value >> 32));
    }

    private static void RecomputeChecksum(byte[] image) {
        image[60] = image[61] = image[62] = image[63] = 0;
        uint crc = 0xffffffffU;
        for (int i = 0; i < image.Length; i++) {
            crc ^= image[i];
            for (int bit = 0; bit < 8; bit++) {
                crc = (crc & 1U) != 0
                    ? (crc >> 1) ^ 0xedb88320U
                    : crc >> 1;
            }
        }
        WriteUInt32(image, 60, ~crc);
    }

    private static void Check(bool condition, string label) {
        if (condition) {
            _passed++;
            Console.WriteLine("AMCONV2F_RDSK_CHECK=PASS: " + label);
        } else {
            _failed++;
            Console.WriteLine("AMCONV2F_RDSK_CHECK=FAIL: " + label);
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
