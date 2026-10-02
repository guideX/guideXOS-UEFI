using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Collections.Generic;

namespace guideXOS.Kernel.Tools {
    /// <summary>Disposable QEMU-only durability proof; never used by production storage.</summary>
    internal static unsafe class Storage35QProof {
        private const ulong ExpectedBlocks = 32768UL;
        private const uint ExpectedBlockSize = 512U;
        private const ulong StateLba = 32764UL;
        private const ulong OriginalLba = 32765UL;
        private const ulong TestLba = 32766UL;
        private const byte StateVersion = 1;
        private const int SectorBytes = 512;
        private const string ExpectedSerial = "GX35Q0001";
        private const string FatPath = "apps/35qapp/state.bin";

        public static void Run() {
            SATADeviceSelection selection = FindTestDisk();
            Disk disk = selection.Disk;
            byte[] state = new byte[SectorBytes];
            Require(ReadSector(disk, StateLba, state), "state-sector-read");
            byte stage = ReadStage(state);
            BootConsole.WriteLine("35Q_BOOT_STAGE=" + stage.ToString());
            BootConsole.WriteLine("35Q_SELECTED_DEVICE=" + selection.Serial +
                ",model=" + selection.Model + ",blocks=" + disk.BlockCount.ToString() +
                ",blockSize=" + disk.BlockSize.ToString() + ",writable=" +
                (((disk.Capabilities & DiskCapabilities.Writable) != 0) ? "1" : "0") +
                ",flush=" + (((disk.Capabilities & DiskCapabilities.FlushSupported) != 0) ? "1" : "0"));
            BootConsole.WriteLine("35Q_ALLOCATOR_COUNTS=freeInvalid=" + Allocator.FreeFailInvalidPtr.ToString() +
                ",freeCorrupt=" + Allocator.FreeFailCorruptRun.ToString() +
                ",freeNoPages=" + Allocator.FreeFailNoPages.ToString());

            RunTypedContractChecks(disk);
            RunInjectedFailureChecks(disk);

            if (stage == 0) RunRawWriteAndRequestReboot(disk, state);
            if (stage == 1) RunRawRebootAndRestore(disk, state);
            if (stage == 2) RunRawRestoreAndInitialFatWrite(disk, state);
            if (stage == 3) RunFatReplacementAtoB(disk, state);
            if (stage == 4) RunFatLengthChanges(disk, state);
            if (stage == 5) RunFatPostRebootAndFinish(disk, state);
            if (stage == 6) Fail("unexpected-complete-state", "fixture-already-complete");
            Fail("unknown-stage", stage.ToString());
        }

        private sealed class SATADeviceSelection {
            public SATA.SATADevice Disk;
            public string Serial;
            public string Model;
        }

        private static SATADeviceSelection FindTestDisk() {
            SATADeviceSelection selected = null;
            if (SATA.Ports == null) Fail("ahci-enumeration", "no-controller-list");
            for (int i = 0; i < SATA.Ports.Count; i++) {
                SATA.SATADevice candidate = SATA.Ports[i];
                if (candidate == null || candidate.Serial != ExpectedSerial) continue;
                if (candidate.BlockCount != ExpectedBlocks || candidate.BlockSize != ExpectedBlockSize ||
                    !candidate.CanWrite || !candidate.CanFlush) {
                    Fail("selected-device-identity-or-capability", candidate.Serial + ",blocks=" +
                        candidate.BlockCount.ToString() + ",blockSize=" + candidate.BlockSize.ToString() +
                        ",writable=" + candidate.CanWrite.ToString() + ",flush=" + candidate.CanFlush.ToString());
                }
                if (selected != null) Fail("ambiguous-device-identity", candidate.Serial);
                selected = new SATADeviceSelection { Disk = candidate, Serial = candidate.Serial, Model = candidate.Model };
            }
            if (selected == null) {
                string found = "none";
                for (int i = 0; i < SATA.Ports.Count; i++)
                    found += ";" + SATA.Ports[i].Serial + "/" + SATA.Ports[i].BlockCount.ToString();
                Fail("expected-test-image-not-found", found);
            }
            return selected;
        }

        private static void RunTypedContractChecks(Disk disk) {
            byte[] sector = new byte[SectorBytes];
            byte[] twoSectors = new byte[SectorBytes * 2];
            DiskIoResult pastEnd;
            DiskIoResult crossingEnd;
            DiskIoResult shortBuffer;
            DiskIoResult nullBuffer;
            DiskIoResult emptyAtEnd;
            DiskIoResult emptyPastEnd;
            fixed (byte* p = sector)
            fixed (byte* p2 = twoSectors) {
                pastEnd = disk.ReadBlocks(ExpectedBlocks, 1, p, (ulong)sector.Length);
                crossingEnd = disk.ReadBlocks(ExpectedBlocks - 1, 2, p2, (ulong)twoSectors.Length);
                shortBuffer = disk.ReadBlocks(0, 1, p, SectorBytes - 1U);
                nullBuffer = disk.ReadBlocks(0, 1, null, SectorBytes);
            }
            emptyAtEnd = disk.ReadBlocks(ExpectedBlocks, 0, null, 0);
            emptyPastEnd = disk.ReadBlocks(ExpectedBlocks + 1, 0, null, 0);
            RequireResult(pastEnd, DiskIoResult.InvalidRange, "lba-past-end");
            RequireResult(crossingEnd, DiskIoResult.InvalidRange, "transfer-crosses-end");
            RequireResult(shortBuffer, DiskIoResult.InvalidBuffer, "short-buffer");
            RequireResult(nullBuffer, DiskIoResult.InvalidBuffer, "null-buffer");
            RequireResult(emptyAtEnd, DiskIoResult.Success, "zero-count-at-capacity");
            RequireResult(emptyPastEnd, DiskIoResult.InvalidRange, "zero-count-past-capacity");
            BootConsole.WriteLine("35Q_RANGE_VALIDATION=PASS");

            ReadOnlyDiskProbe readOnly = new ReadOnlyDiskProbe();
            fixed (byte* p = sector) {
                RequireResult(readOnly.WriteBlocks(0, 1, p, SectorBytes), DiskIoResult.ReadOnly,
                    "read-only-write");
            }
            RequireResult(readOnly.Flush(), DiskIoResult.FlushUnsupported, "read-only-flush-fallback");
            USBMSCBot.USBDisk usbPolicyProbe = new USBMSCBot.USBDisk(null);
            Require(!usbPolicyProbe.SupportsWrites &&
                (usbPolicyProbe.Capabilities & DiskCapabilities.Writable) == 0,
                "usb-remains-read-only");
            BootConsole.WriteLine("35Q_READ_ONLY_CONTRACT=PASS");
            BootConsole.WriteLine("35Q_USB_WRITE_POLICY=READ_ONLY");
        }

        private static void RunInjectedFailureChecks(Disk disk) {
            byte[] oneSector = CreatePayload(SectorBytes, 0x31);
            for (int i = 0; i < 3; i++) {
                int failOnWrite = i == 0 ? 1 : (i == 1 ? 3 : 4);
                FaultInjectingDisk proxy = new FaultInjectingDisk(disk, failOnWrite, false, false);
                FAT fat = new FAT(proxy);
                Require(fat.IsMounted, "fault-fixture-fat-mount", fat.MountResult.ToString());
                FatOperationResult result = fat.TryWriteAllBytes("F35Q" + i.ToString() + ".BIN", oneSector);
                RequireResult(result, FatOperationResult.WriteFailure, "fat-write-failure-stage-" + i.ToString());
                RequireResult(fat.TrySync(), FatOperationResult.WriteFailure, "fat-sync-after-write-failure-" + i.ToString());
            }

            FaultInjectingDisk flushProxy = new FaultInjectingDisk(disk, 0, true, false);
            FAT flushFat = new FAT(flushProxy);
            Require(flushFat.IsMounted, "flush-fixture-fat-mount", flushFat.MountResult.ToString());
            RequireResult(flushFat.TryWriteAllBytes("F35QFL.BIN", oneSector), FatOperationResult.Success,
                "flush-fixture-write");
            RequireResult(flushFat.TrySync(), FatOperationResult.FlushFailure, "fat-flush-failure");

            FaultInjectingDisk readProxy = new FaultInjectingDisk(disk, 0, false, true);
            FAT readFat = new FAT(readProxy);
            Require(readFat.MountResult == DiskIoResult.TransportFailure,
                "fat-mount-read-failure", readFat.MountResult.ToString());
            BootConsole.WriteLine("35Q_INJECTED_FAILURES=FAT_TABLE,DATA,DIRECTORY,FLUSH,READ:PASS");
        }

        private static void RunRawWriteAndRequestReboot(Disk disk, byte[] state) {
            byte[] original = new byte[SectorBytes];
            byte[] saved = new byte[SectorBytes];
            Require(ReadSector(disk, TestLba, original), "raw-original-sector-read");
            Require(WriteSector(disk, OriginalLba, original), "raw-original-sector-save");
            RequireResult(disk.Flush(), DiskIoResult.Success, "raw-original-sector-save-flush");
            Require(ReadSector(disk, OriginalLba, saved), "raw-original-sector-save-readback");
            Require(Equal(original, saved), "raw-original-sector-save-exact");

            for (int cycle = 0; cycle < 25; cycle++) {
                byte[] pattern = CreateRawPattern(cycle);
                Require(WriteSector(disk, TestLba, pattern), "raw-cycle-write-" + cycle.ToString());
                RequireResult(disk.Flush(), DiskIoResult.Success, "raw-cycle-flush-" + cycle.ToString());
                byte[] readback = new byte[SectorBytes];
                Require(ReadSector(disk, TestLba, readback), "raw-cycle-read-" + cycle.ToString());
                Require(Equal(pattern, readback), "raw-cycle-exact-" + cycle.ToString());
            }
            BootConsole.WriteLine("35Q_RAW_WRITE_FLUSH_READ_CYCLES=25:PASS");
            SaveStage(disk, state, 1);
            BootConsole.WriteLine("35Q_REBOOT_REQUEST=RAW_VERIFY");
            Halt();
        }

        private static void RunRawRebootAndRestore(Disk disk, byte[] state) {
            byte[] expected = CreateRawPattern(24);
            byte[] afterReboot = new byte[SectorBytes];
            Require(ReadSector(disk, TestLba, afterReboot), "raw-post-reboot-read");
            Require(Equal(expected, afterReboot), "raw-post-reboot-exact-pattern");
            BootConsole.WriteLine("35Q_RAW_POST_REBOOT=PASS");

            byte[] original = new byte[SectorBytes];
            byte[] restored = new byte[SectorBytes];
            Require(ReadSector(disk, OriginalLba, original), "raw-original-backup-read");
            Require(WriteSector(disk, TestLba, original), "raw-restore-write");
            RequireResult(disk.Flush(), DiskIoResult.Success, "raw-restore-flush");
            Require(ReadSector(disk, TestLba, restored), "raw-restore-same-boot-readback");
            Require(Equal(original, restored), "raw-restore-same-boot-exact");
            BootConsole.WriteLine("35Q_RAW_RESTORE=PASS");
            SaveStage(disk, state, 2);
            BootConsole.WriteLine("35Q_REBOOT_REQUEST=RESTORE_VERIFY");
            Halt();
        }

        private static void RunRawRestoreAndInitialFatWrite(Disk disk, byte[] state) {
            byte[] original = new byte[SectorBytes];
            byte[] restored = new byte[SectorBytes];
            Require(ReadSector(disk, OriginalLba, original), "restore-backup-reboot-read");
            Require(ReadSector(disk, TestLba, restored), "restore-post-reboot-read");
            Require(Equal(original, restored), "restore-post-reboot-exact");
            BootConsole.WriteLine("35Q_RAW_RESTORE_POST_REBOOT=PASS");

            FAT fat = new FAT(disk);
            Require(fat.IsMounted, "fat-mount", fat.MountResult.ToString());
            BootConsole.WriteLine("35Q_FAT_MOUNT=PASS:" + fat.FileSystemVariant);
            RequireResult(fat.CreateDirectory("apps/35qapp"), FatOperationResult.Success, "fat-create-directories");
            RequireResult(fat.TrySync(), FatOperationResult.Success, "fat-directory-sync");
            byte[] valueA = CreatePayload(32768, 0x41);
            RequireResult(fat.TryWriteAllBytes(FatPath, valueA), FatOperationResult.Success, "fat-write-A");
            RequireResult(fat.TrySync(), FatOperationResult.Success, "fat-sync-A");
            RequireFatContent(fat, valueA, "fat-A-same-boot");
            BootConsole.WriteLine("35Q_FAT_FILE_A=32768," + Hash(valueA));
            SaveStage(disk, state, 3);
            BootConsole.WriteLine("35Q_REBOOT_REQUEST=FAT_A_VERIFY");
            Halt();
        }

        private static void RunFatReplacementAtoB(Disk disk, byte[] state) {
            FAT fat = MountFat(disk, "fat-A-post-reboot");
            byte[] valueA = CreatePayload(32768, 0x41);
            RequireFatContent(fat, valueA, "fat-A-post-reboot-exact");
            BootConsole.WriteLine("35Q_FAT_POST_REBOOT_A=PASS:" + Hash(valueA));

            byte[] valueB = CreatePayload(32768, 0xB7);
            RequireResult(fat.TryWriteAllBytes(FatPath, valueB), FatOperationResult.Success, "fat-overwrite-B");
            RequireResult(fat.TrySync(), FatOperationResult.Success, "fat-sync-B");
            RequireFatContent(fat, valueB, "fat-B-same-boot");
            BootConsole.WriteLine("35Q_FAT_OVERWRITE_B=32768," + Hash(valueB));
            SaveStage(disk, state, 4);
            BootConsole.WriteLine("35Q_REBOOT_REQUEST=FAT_B_VERIFY");
            Halt();
        }

        private static void RunFatLengthChanges(Disk disk, byte[] state) {
            FAT fat = MountFat(disk, "fat-B-post-reboot");
            byte[] valueB = CreatePayload(32768, 0xB7);
            RequireFatContent(fat, valueB, "fat-B-post-reboot-exact");
            BootConsole.WriteLine("35Q_FAT_POST_REBOOT_B=PASS:" + Hash(valueB));

            byte[] shorter = CreatePayload(1024, 0x53);
            RequireResult(fat.TryWriteAllBytes(FatPath, shorter), FatOperationResult.Success, "fat-shorter-write");
            RequireResult(fat.TrySync(), FatOperationResult.Success, "fat-shorter-sync");
            RequireFatContent(fat, shorter, "fat-shorter-readback");

            byte[] longer = CreatePayload(65536, 0xD5);
            RequireResult(fat.TryWriteAllBytes(FatPath, longer), FatOperationResult.Success, "fat-longer-write");
            RequireResult(fat.TrySync(), FatOperationResult.Success, "fat-longer-sync");
            RequireFatContent(fat, longer, "fat-longer-readback");
            BootConsole.WriteLine("35Q_FAT_LENGTH_CHANGE=1024->65536,PASS:" + Hash(longer));
            SaveStage(disk, state, 5);
            BootConsole.WriteLine("35Q_REBOOT_REQUEST=FAT_LENGTH_VERIFY");
            Halt();
        }

        private static void RunFatPostRebootAndFinish(Disk disk, byte[] state) {
            FAT fat = MountFat(disk, "fat-longer-post-reboot");
            byte[] longer = CreatePayload(65536, 0xD5);
            RequireFatContent(fat, longer, "fat-longer-post-reboot-exact");
            bool directoryFound = false;
            List<FileInfo> entries = fat.GetFiles("apps/35qapp");
            for (int i = 0; i < entries.Count; i++) {
                if (entries[i].Name == "STATE.BIN") directoryFound = true;
            }
            Require(directoryFound, "fat-directory-entry-post-reboot");
            BootConsole.WriteLine("35Q_FAT_DIRECTORY_POST_REBOOT=PASS");
            BootConsole.WriteLine("35Q_FAT_POST_REBOOT_LONGER=65536," + Hash(longer));
            SaveStage(disk, state, 6);
            BootConsole.WriteLine("35Q_COMPLETE=1");
            Halt();
        }

        private static FAT MountFat(Disk disk, string check) {
            FAT fat = new FAT(disk);
            Require(fat.IsMounted, check + "-mount", fat.MountResult.ToString());
            return fat;
        }

        private static void RequireFatContent(FAT fat, byte[] expected, string check) {
            FatOperationResult result = fat.TryReadAllBytes(FatPath, out byte[] actual);
            RequireResult(result, FatOperationResult.Success, check + "-read");
            if (Equal(expected, actual)) return;
            int firstMismatch = 0;
            int sharedLength = expected.Length < actual.Length ? expected.Length : actual.Length;
            while (firstMismatch < sharedLength && expected[firstMismatch] == actual[firstMismatch])
                firstMismatch++;
            Fail(check + "-bytes", "expectedLength=" + expected.Length.ToString() +
                ",actualLength=" + actual.Length.ToString() +
                ",expectedSha256=" + Hash(expected) + ",actualSha256=" + Hash(actual) +
                ",firstMismatch=" + firstMismatch.ToString() +
                ",expectedByte=" + (firstMismatch < expected.Length ? expected[firstMismatch].ToString() : "end") +
                ",actualByte=" + (firstMismatch < actual.Length ? actual[firstMismatch].ToString() : "end"));
        }

        private static void SaveStage(Disk disk, byte[] state, byte stage) {
            for (int i = 0; i < 8; i++) state[i] = (byte)"35QSTAT1"[i];
            state[8] = stage;
            state[9] = (byte)~stage;
            state[10] = StateVersion;
            Require(WriteSector(disk, StateLba, state), "stage-marker-write-" + stage.ToString());
            RequireResult(disk.Flush(), DiskIoResult.Success, "stage-marker-flush-" + stage.ToString());
        }

        private static byte ReadStage(byte[] state) {
            bool allZero = true;
            for (int i = 0; i < 16; i++) if (state[i] != 0) { allZero = false; break; }
            if (allZero) return 0;
            for (int i = 0; i < 8; i++) if (state[i] != (byte)"35QSTAT1"[i]) Fail("stage-marker-magic", "invalid");
            if (state[10] != StateVersion || (byte)(state[8] ^ state[9]) != 0xFF)
                Fail("stage-marker-check", "invalid");
            if (state[8] > 6) Fail("stage-marker-value", state[8].ToString());
            return state[8];
        }

        private static byte[] CreateRawPattern(int cycle) {
            byte[] pattern = new byte[SectorBytes];
            for (int i = 0; i < pattern.Length; i++)
                pattern[i] = (byte)((cycle * 31 + i * 17 + 0x35) & 0xFF);
            return pattern;
        }

        private static byte[] CreatePayload(int length, int salt) {
            byte[] payload = new byte[length];
            for (int i = 0; i < payload.Length; i++)
                payload[i] = (byte)((i * 31 + (i >> 8) * 13 + salt) & 0xFF);
            return payload;
        }

        private static string Hash(byte[] data) {
            SHA256Ctx context;
            fixed (byte* p = data) {
                SHA256.Init(&context);
                SHA256.Update(&context, p, data.Length);
            }
            byte* digest = stackalloc byte[32];
            SHA256.Final(&context, digest);
            return SHA256.ToHex(digest);
        }

        private static bool ReadSector(Disk disk, ulong lba, byte[] sector) {
            if (sector == null || sector.Length != SectorBytes) return false;
            fixed (byte* p = sector)
                return disk.ReadBlocks(lba, 1, p, (ulong)sector.Length) == DiskIoResult.Success;
        }

        private static bool WriteSector(Disk disk, ulong lba, byte[] sector) {
            if (sector == null || sector.Length != SectorBytes) return false;
            fixed (byte* p = sector)
                return disk.WriteBlocks(lba, 1, p, (ulong)sector.Length) == DiskIoResult.Success;
        }

        private static bool Equal(byte[] left, byte[] right) {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        private static void Require(bool condition, string check, string detail = "failed") {
            if (!condition) Fail(check, detail);
        }

        private static void RequireResult(DiskIoResult actual, DiskIoResult expected, string check) {
            if (actual != expected) Fail(check, "expected=" + expected.ToString() + ",actual=" + actual.ToString());
        }

        private static void RequireResult(FatOperationResult actual, FatOperationResult expected, string check) {
            if (actual != expected) Fail(check, "expected=" + expected.ToString() + ",actual=" + actual.ToString());
        }

        private static void Fail(string check, string detail) {
            BootConsole.WriteLine("35Q_FAIL=" + check + "," + detail);
            Halt();
        }

        private static void Halt() {
            for (;;) Native.Hlt();
        }

        private sealed class ReadOnlyDiskProbe : Disk {
            public override uint BlockSize => 512;
            public override ulong BlockCount => 2;
            public override DiskCapabilities Capabilities => DiskCapabilities.Readable;
            protected override DiskIoResult ReadCore(ulong lba, uint count, byte* data) {
                for (ulong i = 0; i < (ulong)count * BlockSize; i++) data[i] = 0;
                return DiskIoResult.Success;
            }
        }

        /// <summary>Sparse in-memory overlay used only to inject safe FAT I/O failures.</summary>
        private sealed class FaultInjectingDisk : Disk {
            private readonly Disk _inner;
            private readonly int _failWriteCall;
            private readonly bool _failFlush;
            private readonly bool _failFirstRead;
            private readonly Dictionary<ulong, byte[]> _overlay = new Dictionary<ulong, byte[]>();
            private int _writeCalls;
            private bool _readFailed;

            public FaultInjectingDisk(Disk inner, int failWriteCall, bool failFlush, bool failFirstRead) {
                _inner = inner;
                _failWriteCall = failWriteCall;
                _failFlush = failFlush;
                _failFirstRead = failFirstRead;
            }

            public override uint BlockSize => _inner.BlockSize;
            public override ulong BlockCount => _inner.BlockCount;
            public override DiskCapabilities Capabilities => _inner.Capabilities;
            public override bool IsAvailable => _inner.IsAvailable;

            protected override DiskIoResult ReadCore(ulong lba, uint count, byte* data) {
                if (_failFirstRead && !_readFailed) {
                    _readFailed = true;
                    return DiskIoResult.TransportFailure;
                }
                for (uint block = 0; block < count; block++) {
                    byte[] overlaySector;
                    byte* target = data + ((ulong)block * BlockSize);
                    ulong currentLba = lba + block;
                    if (_overlay.ContainsKey(currentLba)) {
                        overlaySector = _overlay[currentLba];
                        fixed (byte* source = overlaySector)
                            Native.Movsb(target, source, BlockSize);
                    } else {
                        DiskIoResult result = _inner.ReadBlocks(currentLba, 1, target, BlockSize);
                        if (result != DiskIoResult.Success) return result;
                    }
                }
                return DiskIoResult.Success;
            }

            protected override DiskIoResult WriteCore(ulong lba, uint count, byte* data) {
                _writeCalls++;
                if (_failWriteCall != 0 && _writeCalls == _failWriteCall)
                    return DiskIoResult.TransportFailure;
                for (uint block = 0; block < count; block++) {
                    byte[] copy = new byte[BlockSize];
                    fixed (byte* target = copy)
                        Native.Movsb(target, data + ((ulong)block * BlockSize), BlockSize);
                    _overlay[lba + block] = copy;
                }
                return DiskIoResult.Success;
            }

            protected override DiskIoResult FlushCore() => _failFlush
                ? DiskIoResult.FlushFailure
                : DiskIoResult.Success;
        }
    }
}
