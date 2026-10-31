using System;
using guideXOS.FS;

internal static class Program {
    private static int _passed;
    private static int _failed;

    private static int Main() {
        var disk = new MemoryDisk(CreateFat12Image());
        var fat = new FAT(disk, false);

        Check(fat.IsMounted, "synthetic FAT12 image mounts");
        CheckStatus(fat, "", FileSystemDirectoryInspectionStatus.Directory,
            (int)FatOperationResult.Success, "mounted root exists");
        CheckStatus(fat, "Folder", FileSystemDirectoryInspectionStatus.Directory,
            (int)FatOperationResult.Success, "existing directory is reported");
        CheckStatus(fat, "File.txt", FileSystemDirectoryInspectionStatus.NotDirectory,
            (int)FatOperationResult.Success, "existing file is not a directory");
        CheckStatus(fat, "Missing", FileSystemDirectoryInspectionStatus.NotFound,
            (int)FatOperationResult.NotFound, "missing path is reported as missing");

        int readsBeforeInvalid = disk.ReadCalls;
        FileSystemDirectoryInspection invalid = fat.InspectDirectory("bad:name");
        Check(invalid.Status == FileSystemDirectoryInspectionStatus.InvalidPath &&
              disk.ReadCalls == readsBeforeInvalid,
              "invalid FAT path is rejected without disk I/O");

        var unavailableDisk = new MemoryDisk(CreateFat12Image());
        var unavailableFat = new FAT(unavailableDisk, false);
        unavailableDisk.Available = false;
        CheckStatus(unavailableFat, "Folder",
            FileSystemDirectoryInspectionStatus.FilesystemUnavailable,
            (int)DiskIoResult.MediaUnavailable,
            "media removal is not reported as a missing directory");

        var failedDisk = new MemoryDisk(CreateFat12Image());
        var failedFat = new FAT(failedDisk, false);
        failedDisk.FailedReadLba = 2;
        CheckStatus(failedFat, "Folder",
            FileSystemDirectoryInspectionStatus.IoFailure,
            (int)DiskIoResult.TransportFailure,
            "transport failure is not reported as a missing directory");

        var unsupportedDisk = new MemoryDisk(CreateFat12Image());
        unsupportedDisk.UnsupportedReadLba = 2;
        var unsupportedFat = new FAT(unsupportedDisk, false);
        CheckStatus(unsupportedFat, "Folder",
            FileSystemDirectoryInspectionStatus.Indeterminate,
            (int)DiskIoResult.Unsupported,
            "unsupported backend reads remain indeterminate");

        byte[] invalidImage = CreateFat12Image();
        invalidImage[510] = 0;
        invalidImage[511] = 0;
        var invalidMount = new FAT(new MemoryDisk(invalidImage), false);
        Check(!invalidMount.IsMounted, "unrecognized media does not mount as FAT");
        CheckStatus(invalidMount, "Folder",
            FileSystemDirectoryInspectionStatus.Indeterminate,
            (int)DiskIoResult.Unsupported,
            "unrecognized filesystem format remains indeterminate");

        Console.WriteLine("AMCONV2D_FAT_INSPECTION_ASSERTIONS=" + _passed);
        Console.WriteLine("AMCONV2D_FAT_INSPECTION_FAILURES=" + _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void CheckStatus(FAT fat, string path,
            FileSystemDirectoryInspectionStatus expected, int expectedCode,
            string label) {
        FileSystemDirectoryInspection result = fat.InspectDirectory(path);
        Check(result.Status == expected && result.BackendCode == expectedCode,
            label + " (status=" + result.Status + ", code=" +
            result.BackendCode + ")");
    }

    private static void Check(bool condition, string label) {
        if (condition) {
            _passed++;
            Console.WriteLine("AMCONV2D_FAT_CHECK=PASS: " + label);
        } else {
            _failed++;
            Console.WriteLine("AMCONV2D_FAT_CHECK=FAIL: " + label);
        }
    }

    private static byte[] CreateFat12Image() {
        byte[] image = new byte[512 * 100];
        image[0] = 0xeb;
        image[1] = 0x3c;
        image[2] = 0x90;
        PutU16(image, 11, 512);
        image[13] = 1;
        PutU16(image, 14, 1);
        image[16] = 1;
        PutU16(image, 17, 16);
        PutU16(image, 19, 100);
        image[21] = 0xf8;
        PutU16(image, 22, 1);
        image[510] = 0x55;
        image[511] = 0xaa;

        image[512] = 0xf8;
        image[513] = 0xff;
        image[514] = 0xff;

        int root = 2 * 512;
        PutAscii(image, root, "FOLDER  ");
        PutAscii(image, root + 8, "   ");
        image[root + 11] = 0x10;
        PutU16(image, root + 26, 2);

        int file = root + 32;
        PutAscii(image, file, "FILE    ");
        PutAscii(image, file + 8, "TXT");
        image[file + 11] = 0x20;
        PutU16(image, file + 26, 3);
        return image;
    }

    private static void PutU16(byte[] target, int offset, ushort value) {
        target[offset] = (byte)value;
        target[offset + 1] = (byte)(value >> 8);
    }

    private static void PutAscii(byte[] target, int offset, string value) {
        for (int i = 0; i < value.Length; i++)
            target[offset + i] = (byte)value[i];
    }

    private sealed unsafe class MemoryDisk : Disk {
        private readonly byte[] _image;
        internal bool Available = true;
        internal ulong FailedReadLba = ulong.MaxValue;
        internal ulong UnsupportedReadLba = ulong.MaxValue;
        internal int ReadCalls { get; private set; }

        internal MemoryDisk(byte[] image) { _image = image; }
        public override uint BlockSize { get { return 512; } }
        public override ulong BlockCount { get { return (ulong)_image.Length / 512; } }
        public override DiskCapabilities Capabilities {
            get { return DiskCapabilities.Readable; }
        }
        public override bool IsAvailable { get { return Available; } }

        protected override DiskIoResult ReadCore(ulong lba, uint count,
                byte* data) {
            ReadCalls++;
            if (lba == FailedReadLba) return DiskIoResult.TransportFailure;
            if (lba == UnsupportedReadLba) return DiskIoResult.Unsupported;
            ulong start = lba * BlockSize;
            ulong length = (ulong)count * BlockSize;
            for (ulong i = 0; i < length; i++) data[i] = _image[(int)(start + i)];
            return DiskIoResult.Success;
        }

        protected override DiskIoResult WriteCore(ulong lba, uint count,
                byte* data) {
            return DiskIoResult.ReadOnly;
        }
    }
}
