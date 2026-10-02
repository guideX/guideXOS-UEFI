using guideXOS.FS;
using guideXOS.Misc;
using System.Runtime.InteropServices;

namespace guideXOS.Kernel.Drivers {
    // Extremely defensive USB Mass Storage (Bulk-Only Transport) driver.
    // It is isolated from the global FileSystem. It only becomes active when explicitly used
    // by the USB Files window. If anything fails, it aborts gracefully without affecting boot.
    public static unsafe class USBMSCBot {
        // Bulk-Only definitions
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CBW {
            public uint Signature;      // 'USBC' 0x43425355
            public uint Tag;            // echoed in CSW
            public uint DataTransferLength;
            public byte Flags;          // bit7: direction (1=IN, 0=OUT)
            public byte LUN;            // we use 0
            public byte CBLength;       // bytes in CB
            public fixed byte CB[16];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CSW {
            public uint Signature;      // 'USBS' 0x53425355
            public uint Tag;
            public uint DataResidue;
            public byte Status;         // 0=Passed,1=Failed,2=PhaseError
        }

        // Simple block device backed by BOT/SCSI. Read-only by default.
        public sealed class USBDisk : Disk {
            private readonly USBDevice _dev;
            private bool _ready;
            private uint _blockSize;
            private ulong _numBlocks;
            private uint _tag;
            private bool _initialized;

            public USBDevice Device => _dev;
            public bool IsReady => _ready && _initialized;
            public bool SupportsWrites => false;
            public uint LogicalBlockSize => _blockSize;
            public ulong TotalBlocks => _numBlocks;
            public override uint BlockSize => _blockSize;
            public override ulong BlockCount => IsReady ? _numBlocks : 0UL;
            public override DiskCapabilities Capabilities => IsReady
                ? DiskCapabilities.Readable
                : DiskCapabilities.None;
            public override bool IsAvailable => IsReady;

            public USBDisk(USBDevice dev) {
                _dev = dev;
                _blockSize = 512;
                _numBlocks = 0;
                _ready = false;
                _tag = 1;
                _initialized = false;
            }

            public bool Initialize() {
                // Sanity: only MSC BOT devices
                if (_dev == null) return false;
                if (!(_dev.Class == 0x08 && _dev.SubClass == 0x06 && _dev.Protocol == 0x50)) return false;
                // Ensure endpoints present
                if (_dev.EndpointIn == 0 || _dev.EndpointOut == 0) return false;

                // Reset BOT just in case
                BotReset();
                // Optional: Get Max LUN (ignore failures)
                GetMaxLun();

                // Poll unit ready a few times
                for (int i = 0; i < 5; i++) {
                    if (TestUnitReady()) { _ready = true; break; }
                    Timer.Sleep(20);
                }
                if (!_ready) return false;

                // Inquiry (optional)
                byte[] inq = new byte[36];
                fixed (byte* p = inq) SCSI_Inquiry(p, (ushort)inq.Length);

                // Read capacity
                if (!ReadCapacity(out _numBlocks, out _blockSize)) return false;
                if (_blockSize == 0 || _blockSize > 4096) { _blockSize = 512; }
                _initialized = true;
                return true;
            }

            private void FillCBW(CBW* cbw, byte* cmd, int cmdLen, int dataLen, bool dataIn) {
                cbw->Signature = 0x43425355; // 'USBC'
                cbw->Tag = _tag++;
                cbw->DataTransferLength = (uint)dataLen;
                cbw->Flags = (byte)(dataIn ? 0x80 : 0x00);
                cbw->LUN = 0;
                cbw->CBLength = (byte)cmdLen;
                // zero CB
                for (int i = 0; i < 16; i++) cbw->CB[i] = 0;
                for (int i = 0; i < cmdLen && i < 16; i++) cbw->CB[i] = cmd[i];
            }

            private bool BotReset() {
                // Class-specific request: Mass Storage Reset
                // bmRequestType=00100001b (Class,Interface,Host->Device) = 0x21
                // bRequest=FFh, wValue=0, wIndex=interface, wLength=0
                USBRequest req = default;
                req.RequestType = 0x21;
                req.Request = 0xFF;
                req.Value = 0;
                req.Index = 0; // assume interface 0
                req.Length = 0;
                return USB.SendAndReceive(_dev, &req, null, _dev.Parent);
            }

            private int GetMaxLun() {
                USBRequest req = default;
                req.RequestType = 0xA1; // IN | Class | Interface
                req.Request = 0xFE; // Get Max LUN
                req.Value = 0;
                req.Index = 0;
                req.Length = 1;
                byte val = 0;
                return USB.SendAndReceive(_dev, &req, &val, _dev.Parent) ? (val + 1) : 1;
            }

            private bool SendCBW(byte* cmd, int cmdLen, int dataLen, bool dataIn) {
                CBW cbw = default;
                FillCBW(&cbw, cmd, cmdLen, dataLen, dataIn);
                // allocate a temp buffer to pass pointer safely
                byte* tmp = stackalloc byte[sizeof(CBW)];
                Native.Movsb(tmp, (byte*)&cbw, (ulong)sizeof(CBW));
                return EHCI.BulkOut(_dev, tmp, sizeof(CBW));
            }

            private bool ReceiveCSW(out byte status) {
                status = 1;
                byte* tmp = stackalloc byte[sizeof(CSW)];
                bool ok = EHCI.BulkIn(_dev, tmp, sizeof(CSW));
                if (!ok) return false;
                CSW csw = *(CSW*)tmp;
                if (csw.Signature != 0x53425355) return false; // 'USBS'
                status = csw.Status;
                return status == 0; // success
            }

            private bool SCSI_Inquiry(byte* buffer, ushort len) {
                byte* cdb = stackalloc byte[6];
                for (int i = 0; i < 6; i++) cdb[i] = 0;
                cdb[0] = 0x12; // INQUIRY
                cdb[4] = (byte)len;
                if (!SendCBW(cdb, 6, len, true)) return false;
                if (!EHCI.BulkIn(_dev, buffer, len)) return false;
                byte st; if (!ReceiveCSW(out st)) return false;
                return true;
            }

            private bool TestUnitReady() {
                byte* cdb = stackalloc byte[6];
                for (int i = 0; i < 6; i++) cdb[i] = 0;
                cdb[0] = 0x00; // TEST UNIT READY
                if (!SendCBW(cdb, 6, 0, false)) return false;
                byte st; return ReceiveCSW(out st);
            }

            private bool ReadCapacity(out ulong numBlocks, out uint blockSize) {
                numBlocks = 0; blockSize = 512;
                byte* cdb = stackalloc byte[10];
                for (int i = 0; i < 10; i++) cdb[i] = 0;
                cdb[0] = 0x25; // READ CAPACITY(10)
                if (!SendCBW(cdb, 10, 8, true)) return false;
                uint* tmp = stackalloc uint[2];
                if (!EHCI.BulkIn(_dev, tmp, 8)) return false;
                byte st; if (!ReceiveCSW(out st)) return false;
                uint lastLBA = Swap32(tmp[0]);
                uint blksz = Swap32(tmp[1]);
                if (blksz == 0) blksz = 512;
                numBlocks = (ulong)lastLBA + 1UL;
                blockSize = blksz;
                return true;
            }

            private static uint Swap32(uint v) { return ((v & 0xFF) << 24) | ((v & 0xFF00) << 8) | ((v & 0xFF0000) >> 8) | ((v >> 24) & 0xFF); }

            private bool Read10(ulong lba, uint blocks, byte* buf) {
                if (blocks == 0) return true;
                if (blocks > 0xFFFF) blocks = 0xFFFF;
                byte* cdb = stackalloc byte[10];
                for (int i = 0; i < 10; i++) cdb[i] = 0;
                cdb[0] = 0x28; // READ(10)
                uint l = (uint)lba;
                cdb[2] = (byte)((l >> 24) & 0xFF);
                cdb[3] = (byte)((l >> 16) & 0xFF);
                cdb[4] = (byte)((l >> 8) & 0xFF);
                cdb[5] = (byte)(l & 0xFF);
                cdb[7] = (byte)((blocks >> 8) & 0xFF);
                cdb[8] = (byte)(blocks & 0xFF);
                int bytes = (int)(blocks * _blockSize);
                if (!SendCBW(cdb, 10, bytes, true)) return false;
                bool ok = EHCI.BulkIn(_dev, buf, bytes);
                byte st; bool ok2 = ReceiveCSW(out st);
                return ok && ok2;
            }

            protected override DiskIoResult ReadCore(ulong lba, uint blocks, byte* data) {
                if (!IsReady) return DiskIoResult.MediaUnavailable;
                const uint maxCommandBlocks = 128;
                while (blocks > 0) {
                    uint batch = blocks > maxCommandBlocks ? maxCommandBlocks : blocks;
                    if (!Read10(lba, batch, data)) return DiskIoResult.TransportFailure;
                    lba += batch;
                    blocks -= batch;
                    data += (ulong)batch * _blockSize;
                }
                return DiskIoResult.Success;
            }

            protected override DiskIoResult WriteCore(ulong lba, uint blocks, byte* data) => DiskIoResult.ReadOnly;
        }

        // Public helper to try create a USBDisk from a device with many safeguards
        public static USBDisk TryCreateDisk(USBDevice dev) {
            if (dev == null) return null;
            // Only support BOT SCSI transparent
            if (!(dev.Class == 0x08 && dev.SubClass == 0x06 && dev.Protocol == 0x50)) return null;
            // Endpoints must be present
            if (dev.EndpointIn == 0 || dev.EndpointOut == 0) return null;
            var d = new USBDisk(dev);
            if (!d.Initialize()) { d.Dispose(); return null; }
            return d;
        }
    }
}
