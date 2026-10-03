using guideXOS.Misc;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace guideXOS.FS {
    public enum FatOperationResult : byte {
        Success = 0,
        NotMounted = 1,
        InvalidPath = 2,
        NotFound = 3,
        NoSpace = 4,
        ReadFailure = 5,
        WriteFailure = 6,
        FlushUnsupported = 7,
        FlushFailure = 8,
        Unsupported = 9,
        ReadOnly = 10,
        InvalidRange = 11,
        InvalidBuffer = 12,
        MediaUnavailable = 13,
        TransportFailure = 14,
        EntryLimitExceeded = 15
    }

    /// <summary>
    /// Complete FAT12/16/32 filesystem with LFN support and sector caching.
    /// Supports read, write, create, delete, and format operations.
    /// </summary>
    internal unsafe class FAT : FileSystem {
        /// <summary>
        /// Fat Type
        /// </summary>
        enum FatType {
            /// <summary>
            /// FAT12
            /// </summary>
            FAT12, 
            /// <summary>
            /// FAT16
            /// </summary>
            FAT16, 
            /// <summary>
            /// FAT32
            /// </summary>
            FAT32
        }
        /// <summary>
        /// BPB Common
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BPB_Common {
            public fixed byte jmpBoot[3];
            public fixed byte OEMName[8];
            public ushort BytsPerSec; // 0x0B
            public byte SecPerClus;   // 0x0D
            public ushort RsvdSecCnt; // 0x0E
            public byte NumFATs;      // 0x10
            public ushort RootEntCnt; // 0x11 (FAT12/16)
            public ushort TotSec16;   // 0x13
            public byte Media;        // 0x15
            public ushort FATSz16;    // 0x16
            public ushort SecPerTrk;  // 0x18
            public ushort NumHeads;   // 0x1A
            public uint HiddSec;      // 0x1C
            public uint TotSec32;     // 0x20
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BPB_FAT32 {
            public uint FATSz32;      // 0x24
            public ushort ExtFlags;   // 0x28
            public ushort FSVer;      // 0x2A
            public uint RootClus;     // 0x2C
            public ushort FSInfo;     // 0x30
            public ushort BkBootSec;  // 0x32
            public fixed byte Reserved[12];
            public byte DrvNum;       // 0x40
            public byte Reserved1;    // 0x41
            public byte BootSig;      // 0x42
            public uint VolID;        // 0x43
            public fixed byte VolLab[11]; // 0x47
            public fixed byte FilSysType[8]; // 0x52 e.g. "FAT32   "
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct DirEntry {
            public fixed byte Name83[11];
            public byte Attr;
            public byte NTRes;
            public byte CrtTimeTenth;
            public ushort CrtTime;
            public ushort CrtDate;
            public ushort LstAccDate;
            public ushort FstClusHI;
            public ushort WrtTime;
            public ushort WrtDate;
            public ushort FstClusLO;
            public uint FileSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct LfnEntry {
            public byte Ord; // sequence number
            public fixed ushort Name1[5];
            public byte Attr; // 0x0F
            public byte Type;
            public byte Chksum;
            public fixed ushort Name2[6];
            public ushort FstClusLO;
            public fixed ushort Name3[2];
        }

        // Runtime fields
        private Disk _disk;
        private Disk disk => _disk ?? Disk.Instance;
        private FatType _type;
        private ushort _bytesPerSec;
        private byte _secPerClus;
        private ushort _rsvdSecCnt;
        private byte _numFATs;
        private uint _FATSz;
        private uint _rootDirSectors;
        private uint _firstDataSector;
        private uint _fatStart;
        private uint _firstRootDirSector; // FAT12/16 only
        private uint _rootCluster; // FAT32 root cluster
        private uint _clusterCount;

        // Simple sector cache - replaced Dictionary with parallel arrays
        private const int CacheCapacity = 1024; // sectors
        private ulong[] _cacheKeys;
        private byte[][] _cacheValues;
        private int _cacheCount;
        private int _lruHead; // simple circular buffer for LRU
        private bool _mounted;
        private DiskIoResult _stickyIoResult;
        private FatOperationResult _pendingMutationResult;
        private uint _volumeId;
        private string _volumeLabel = string.Empty;
        private int _lastCreateDirectoryFailureIndex = -1;
        private int _lastCreateDirectoryPartCount;

        public DiskIoResult MountResult { get; private set; } = DiskIoResult.MediaUnavailable;
        public DiskIoResult LastDiskResult => _stickyIoResult;
        public FatOperationResult LastOperationResult { get; private set; } = FatOperationResult.NotMounted;
        public bool IsMounted => _mounted;
        public uint VolumeId => _volumeId;
        public string VolumeLabel => _volumeLabel;
        internal int LastCreateDirectoryFailureIndex => _lastCreateDirectoryFailureIndex;
        internal int LastCreateDirectoryPartCount => _lastCreateDirectoryPartCount;

        public FAT(Disk disk) : this(disk, true) { }

        /// <summary>
        /// Mounts a FAT volume without changing the global File API mount.
        /// Persistent application storage uses this form so initrd resources
        /// remain available through File.Instance.
        /// </summary>
        internal FAT(Disk disk, bool registerAsDefault) : base(registerAsDefault) {
            this._disk = disk;
            InitializeCache();
            InitializeMount();
        }

        public FAT() {
            InitializeCache();
            InitializeMount();
        }

        private void InitializeCache() {
            _cacheKeys = new ulong[CacheCapacity];
            _cacheValues = new byte[CacheCapacity][];
            _cacheCount = 0;
            _lruHead = 0;
        }

        private void InitializeMount() {
            if (disk == null) { MountResult = DiskIoResult.MediaUnavailable; return; }
            if (disk.BlockSize != SectorSize) { MountResult = DiskIoResult.Unsupported; return; }
            var sec0 = ReadSectorsCached(0, 1);
            if (_stickyIoResult != DiskIoResult.Success) { MountResult = _stickyIoResult; return; }
            if (sec0 == null || sec0.Length < SectorSize || sec0[510] != 0x55 || sec0[511] != 0xAA) {
                MountResult = DiskIoResult.Unsupported;
                return;
            }
            fixed (byte* p = sec0) {
                BPB_Common* bpb = (BPB_Common*)p;
                _bytesPerSec = bpb->BytsPerSec;
                _secPerClus = bpb->SecPerClus;
                _rsvdSecCnt = bpb->RsvdSecCnt;
                _numFATs = bpb->NumFATs;
                uint totSec = bpb->TotSec16 != 0 ? bpb->TotSec16 : bpb->TotSec32;
                uint fatsz = bpb->FATSz16;
                if (fatsz == 0) {
                    BPB_FAT32* bpb32 = (BPB_FAT32*)(p + 0x24);
                    fatsz = bpb32->FATSz32;
                    _rootCluster = bpb32->RootClus;
                }
                ulong metadataSectors = (ulong)_rsvdSecCnt + ((ulong)_numFATs * fatsz);
                _rootDirSectors = (uint)(((ulong)bpb->RootEntCnt * 32UL + (_bytesPerSec == 0 ? 0UL : _bytesPerSec - 1UL)) / (_bytesPerSec == 0 ? 1U : _bytesPerSec));
                metadataSectors += _rootDirSectors;
                if (_bytesPerSec != SectorSize || _secPerClus == 0 ||
                    (_secPerClus & (_secPerClus - 1)) != 0 || _numFATs == 0 ||
                    fatsz == 0 || totSec == 0 || metadataSectors >= totSec ||
                    (ulong)totSec > disk.BlockCount) {
                    MountResult = DiskIoResult.Unsupported;
                    return;
                }
                uint dataSec = totSec - (uint)metadataSectors;
                uint countOfClusters = dataSec / _secPerClus;
                if (countOfClusters == 0) { MountResult = DiskIoResult.Unsupported; return; }
                _FATSz = fatsz;
                _clusterCount = countOfClusters;
                _type = countOfClusters < 4085 ? FatType.FAT12 : (countOfClusters < 65525 ? FatType.FAT16 : FatType.FAT32);
                if (_type == FatType.FAT32) {
                    _volumeId = *(uint*)(p + 0x43);
                    _volumeLabel = ReadVolumeLabel(p + 0x47);
                } else {
                    _volumeId = *(uint*)(p + 0x27);
                    _volumeLabel = ReadVolumeLabel(p + 0x2B);
                }
                _fatStart = _rsvdSecCnt;
                _firstDataSector = (uint)metadataSectors;
                _firstRootDirSector = (uint)(_rsvdSecCnt + (_numFATs * fatsz));
                if (_type != FatType.FAT32) _rootCluster = 0;
                _mounted = true;
                MountResult = DiskIoResult.Success;
                LastOperationResult = FatOperationResult.Success;
            }
        }

        private static string ReadVolumeLabel(byte* value) {
            string result = string.Empty;
            int length = 11;
            while (length > 0 && value[length - 1] == (byte)' ') length--;
            for (int i = 0; i < length; i++) result += (char)value[i];
            return result;
        }

        private void InvalidateSector(ulong lba) {
            // Invalidate every matching key. Older cache entries can share a
            // key after repeated write/read cycles, and leaving a newer copy
            // valid would hide committed directory or FAT updates.
            for (int i = 0; i < _cacheCount; i++) {
                if (_cacheKeys[i] == lba) {
                    _cacheValues[i] = null;
                }
            }
        }

        private byte[] ReadSectorsCached(ulong lba, uint count) {
            ulong byteCount = (ulong)count * SectorSize;
            if (byteCount > int.MaxValue) {
                _stickyIoResult = DiskIoResult.InvalidRange;
                return new byte[0];
            }
            if (disk == null || _stickyIoResult != DiskIoResult.Success) {
                if (disk == null) _stickyIoResult = DiskIoResult.MediaUnavailable;
                return new byte[(int)byteCount];
            }
            if (count == 1) {
                // Check cache first and remember an invalid slot for reuse.
                int reusableSlot = -1;
                for (int i = 0; i < _cacheCount; i++) {
                    if (_cacheKeys[i] == lba) {
                        if (_cacheValues[i] != null) return _cacheValues[i];
                        if (reusableSlot < 0) reusableSlot = i;
                    }
                }

                // Not in cache - read from disk
                var buf = new byte[SectorSize];
                fixed (byte* p = buf) _stickyIoResult = disk.ReadBlocks(lba, 1, p, (ulong)buf.Length);
                if (_stickyIoResult != DiskIoResult.Success) return buf;

                // Refresh an invalidated slot before growing or replacing the
                // circular cache, so the same sector does not accumulate keys.
                if (reusableSlot >= 0) {
                    _cacheValues[reusableSlot] = buf;
                } else if (_cacheCount < CacheCapacity) {
                    // Add new entry
                    _cacheKeys[_cacheCount] = lba;
                    _cacheValues[_cacheCount] = buf;
                    _cacheCount++;
                } else {
                    // Replace oldest (circular LRU)
                    _cacheKeys[_lruHead] = lba;
                    _cacheValues[_lruHead] = buf;
                    _lruHead = (_lruHead + 1) % CacheCapacity;
                }

                return buf;
            } else {
                // Multi-sector read - don't cache
                var buf = new byte[(int)byteCount];
                fixed (byte* p = buf) _stickyIoResult = disk.ReadBlocks(lba, count, p, (ulong)buf.Length);
                return buf;
            }
        }

        private DiskIoResult WriteSector(ulong lba, byte[] data) {
            if (data == null || data.Length < SectorSize) return _stickyIoResult = DiskIoResult.InvalidBuffer;
            if (disk == null) return _stickyIoResult = DiskIoResult.MediaUnavailable;
            if (_stickyIoResult != DiskIoResult.Success) return _stickyIoResult;
            fixed (byte* p = data) _stickyIoResult = disk.WriteBlocks(lba, 1, p, (ulong)data.Length);
            if (_stickyIoResult == DiskIoResult.Success) InvalidateSector(lba);
            return _stickyIoResult;
        }

        private void BeginOperation() {
            _stickyIoResult = DiskIoResult.Success;
            LastOperationResult = _mounted ? FatOperationResult.Success : FatOperationResult.NotMounted;
        }

        private bool StartOperation() {
            BeginOperation();
            return _mounted;
        }

        private FatOperationResult CurrentIoFailure(bool writing) {
            switch (_stickyIoResult) {
                case DiskIoResult.Unsupported: return FatOperationResult.Unsupported;
                case DiskIoResult.ReadOnly: return FatOperationResult.ReadOnly;
                case DiskIoResult.InvalidRange: return FatOperationResult.InvalidRange;
                case DiskIoResult.InvalidBuffer: return FatOperationResult.InvalidBuffer;
                case DiskIoResult.MediaUnavailable: return FatOperationResult.MediaUnavailable;
                case DiskIoResult.TransportFailure: return FatOperationResult.TransportFailure;
                case DiskIoResult.WriteFailure: return FatOperationResult.WriteFailure;
                case DiskIoResult.FlushUnsupported: return FatOperationResult.FlushUnsupported;
                case DiskIoResult.FlushFailure: return FatOperationResult.FlushFailure;
                default: return writing ? FatOperationResult.WriteFailure : FatOperationResult.ReadFailure;
            }
        }

        private uint FirstSectorOfCluster(uint n) { return (uint)((n - 2) * _secPerClus) + _firstDataSector; }

        private uint ReadFatEntry(uint cluster) {
            if (_stickyIoResult != DiskIoResult.Success) return 0;
            switch (_type) {
                case FatType.FAT12: {
                        uint fatOffset = cluster + (cluster / 2);
                        uint fatSec = (uint)(_fatStart + (fatOffset / _bytesPerSec));
                        int off = (int)(fatOffset % _bytesPerSec);
                        var sec = ReadSectorsCached(fatSec, 1);
                        if (_stickyIoResult != DiskIoResult.Success) return 0;
                        var nextSec = off == _bytesPerSec - 1 ? ReadSectorsCached(fatSec + 1, 1) : null;
                        if (_stickyIoResult != DiskIoResult.Success) return 0;
                        uint val = sec[off];
                        if (off == _bytesPerSec - 1) val |= (uint)(nextSec[0] << 8);
                        else val |= (uint)(sec[off + 1] << 8);
                        if ((cluster & 1) == 1) val >>= 4; else val &= 0x0FFF;
                        return val;
                    }
                case FatType.FAT16: {
                        uint fatOffset = cluster * 2u;
                        uint fatSec = (uint)(_fatStart + (fatOffset / _bytesPerSec));
                        int off = (int)(fatOffset % _bytesPerSec);
                        var sec = ReadSectorsCached(fatSec, 1);
                        if (_stickyIoResult != DiskIoResult.Success) return 0;
                        return (uint)(sec[off] | (sec[off + 1] << 8));
                    }
                default: {
                        uint fatOffset = cluster * 4u;
                        uint fatSec = (uint)(_fatStart + (fatOffset / _bytesPerSec));
                        int off = (int)(fatOffset % _bytesPerSec);
                        var sec = ReadSectorsCached(fatSec, 1);
                        if (_stickyIoResult != DiskIoResult.Success) return 0;
                        uint val = (uint)(sec[off] | (sec[off + 1] << 8) | (sec[off + 2] << 16) | (sec[off + 3] << 24));
                        return val & 0x0FFFFFFF;
                    }
            }
        }

        private bool WriteFatEntry(uint cluster, uint value) {
            if (_stickyIoResult != DiskIoResult.Success) return false;
            // Write to all FAT copies
            for (int fatCopy = 0; fatCopy < _numFATs; fatCopy++) {
                uint fatBase = (uint)(_fatStart + (fatCopy * _FATSz));
                switch (_type) {
                    case FatType.FAT12: {
                            uint fatOffset = cluster + (cluster / 2);
                            uint fatSec = (uint)(fatBase + (fatOffset / _bytesPerSec));
                            int off = (int)(fatOffset % _bytesPerSec);
                            var sec = ReadSectorsCached(fatSec, 1);
                            if (_stickyIoResult != DiskIoResult.Success) return false;
                            // get the two bytes
                            byte b0 = sec[off];
                            byte b1 = (off == _bytesPerSec - 1) ? ReadSectorsCached(fatSec + 1, 1)[0] : sec[off + 1];
                            if (_stickyIoResult != DiskIoResult.Success) return false;
                            uint cur = (uint)(b0 | (b1 << 8));
                            if ((cluster & 1) == 1) {
                                // odd cluster: high 12 bits
                                cur &= 0x000F;
                                cur |= (value & 0x0FFF) << 4;
                            } else {
                                // even cluster: low 12 bits
                                cur &= 0xF000;
                                cur |= (value & 0x0FFF);
                            }
                            // write back
                            sec[off] = (byte)(cur & 0xFF);
                            if (off == _bytesPerSec - 1) {
                                var sec2 = ReadSectorsCached(fatSec + 1, 1);
                                sec2[0] = (byte)((cur >> 8) & 0xFF);
                                if (WriteSector(fatSec + 1, sec2) != DiskIoResult.Success) return false;
                            } else {
                                sec[off + 1] = (byte)((cur >> 8) & 0xFF);
                            }
                            if (WriteSector(fatSec, sec) != DiskIoResult.Success) return false;
                            break;
                        }
                    case FatType.FAT16: {
                            uint fatOffset = cluster * 2u;
                            uint fatSec = (uint)(fatBase + (fatOffset / _bytesPerSec));
                            int off = (int)(fatOffset % _bytesPerSec);
                            var sec = ReadSectorsCached(fatSec, 1);
                            if (_stickyIoResult != DiskIoResult.Success) return false;
                            sec[off] = (byte)(value & 0xFF);
                            sec[off + 1] = (byte)((value >> 8) & 0xFF);
                            if (WriteSector(fatSec, sec) != DiskIoResult.Success) return false;
                            break;
                        }
                    default: {
                            uint fatOffset = cluster * 4u;
                            uint fatSec = (uint)(fatBase + (fatOffset / _bytesPerSec));
                            int off = (int)(fatOffset % _bytesPerSec);
                            var sec = ReadSectorsCached(fatSec, 1);
                            if (_stickyIoResult != DiskIoResult.Success) return false;
                            uint cur = (uint)(sec[off] | (sec[off + 1] << 8) | (sec[off + 2] << 16) | (sec[off + 3] << 24));
                            cur &= 0xF0000000; // upper 4 bits preserved
                            cur |= (value & 0x0FFFFFFF);
                            sec[off] = (byte)(cur & 0xFF);
                            sec[off + 1] = (byte)((cur >> 8) & 0xFF);
                            sec[off + 2] = (byte)((cur >> 16) & 0xFF);
                            sec[off + 3] = (byte)((cur >> 24) & 0xFF);
                            if (WriteSector(fatSec, sec) != DiskIoResult.Success) return false;
                            break;
                        }
                }
            }
            return true;
        }

        private bool IsEOC(uint clus) {
            switch (_type) {
                case FatType.FAT12: return clus >= 0x0FF8;
                case FatType.FAT16: return clus >= 0xFFF8;
                default: return clus >= 0x0FFFFFF8;
            }
        }

        private uint EOC() {
            switch (_type) {
                case FatType.FAT12: return 0x0FFF;
                case FatType.FAT16: return 0xFFFF;
                default: return 0x0FFFFFFF;
            }
        }

        private List<uint> GetClusterChain(uint start) {
            int maximumClusters = _clusterCount > int.MaxValue
                ? int.MaxValue : (int)_clusterCount;
            if (maximumClusters <= 0) return new List<uint>();
            int capacity = maximumClusters < 16 ? maximumClusters : 16;
            List<uint> chain = new List<uint>(new uint[capacity]);
            uint c = start;
            uint lastValidCluster = _clusterCount + 1U;
            int traversed = 0;
            while (c >= 2 && c <= lastValidCluster && !IsEOC(c) &&
                    traversed < maximumClusters) {
                for (int i = 0; i < chain.Count; i++) {
                    if (chain[i] == c) {
                        _stickyIoResult = DiskIoResult.InvalidRange;
                        return chain;
                    }
                }
                if (chain.Count == capacity) {
                    if (capacity >= maximumClusters) {
                        _stickyIoResult = DiskIoResult.InvalidRange;
                        return chain;
                    }
                    int nextCapacity = capacity > maximumClusters / 2
                        ? maximumClusters : capacity * 2;
                    uint[] grownBuffer = new uint[nextCapacity];
                    for (int i = 0; i < chain.Count; i++)
                        grownBuffer[i] = chain[i];
                    List<uint> grown = new List<uint>(grownBuffer);
                    grown.Count = chain.Count;
                    chain = grown;
                    capacity = nextCapacity;
                }
                chain.Add(c);
                traversed++;
                uint next = ReadFatEntry(c);
                if (_stickyIoResult != DiskIoResult.Success) return chain;
                if (next == 0) break;
                if (next == c || (next < 2 && !IsEOC(next)) ||
                        (next > lastValidCluster && !IsEOC(next))) {
                    _stickyIoResult = DiskIoResult.InvalidRange;
                    return chain;
                }
                c = next;
            }
            if (traversed >= maximumClusters && c >= 2 &&
                    c <= lastValidCluster && !IsEOC(c))
                _stickyIoResult = DiskIoResult.InvalidRange;
            return chain;
        }

        private void ReadCluster(uint cluster, byte[] dest, int destOffset) {
            uint firstSec = FirstSectorOfCluster(cluster);
            for (int i = 0; i < _secPerClus; i++) {
                var sec = ReadSectorsCached(firstSec + (uint)i, 1);
                fixed (byte* pSec = sec)
                fixed (byte* pDest = dest) {
                    Native.Movsb(pDest + destOffset + i * _bytesPerSec, pSec, (ulong)_bytesPerSec);
                }
            }
        }

        private bool ZeroCluster(uint cluster) {
            uint firstSec = FirstSectorOfCluster(cluster);
            var zero = new byte[_bytesPerSec];
            for (int i = 0; i < _secPerClus; i++) {
                if (WriteSector(firstSec + (uint)i, zero) != DiskIoResult.Success) return false;
            }
            return true;
        }

        private FatOperationResult ReadAllClusterChain(uint startCluster, uint fileSize, out byte[] content) {
            content = null;
            if (fileSize == 0) {
                content = new byte[0];
                return FatOperationResult.Success;
            }

            ulong clusterBytes = (ulong)_secPerClus * _bytesPerSec;
            if (clusterBytes == 0 || fileSize > int.MaxValue)
                return FatOperationResult.InvalidRange;
            ulong requiredClusters = ((ulong)fileSize + clusterBytes - 1UL) / clusterBytes;
            ulong bufferBytes = requiredClusters * clusterBytes;
            uint lastValidCluster = _clusterCount + 1U;
            if (requiredClusters == 0 || requiredClusters > _clusterCount ||
                bufferBytes > int.MaxValue || startCluster < 2 || startCluster > lastValidCluster)
                return FatOperationResult.InvalidRange;

            var buf = new byte[(int)bufferBytes];
            var visited = new List<uint>();
            uint cluster = startCluster;
            int offset = 0;
            for (ulong i = 0; i < requiredClusters; i++) {
                bool repeated = false;
                for (int visitedIndex = 0; visitedIndex < visited.Count; visitedIndex++) {
                    if (visited[visitedIndex] == cluster) {
                        repeated = true;
                        break;
                    }
                }
                if (cluster < 2 || cluster > lastValidCluster || repeated)
                    return FatOperationResult.InvalidRange;
                visited.Add(cluster);

                ReadCluster(cluster, buf, offset);
                if (_stickyIoResult != DiskIoResult.Success)
                    return CurrentIoFailure(false);
                offset += (int)clusterBytes;

                if (i + 1UL < requiredClusters) {
                    uint next = ReadFatEntry(cluster);
                    if (_stickyIoResult != DiskIoResult.Success)
                        return CurrentIoFailure(false);
                    if (next < 2 || next > lastValidCluster || IsEOC(next))
                        return FatOperationResult.InvalidRange;
                    cluster = next;
                }
            }

            if ((uint)buf.Length == fileSize) {
                content = buf;
                return FatOperationResult.Success;
            }
            content = new byte[(int)fileSize];
            fixed (byte* pSrc = buf)
            fixed (byte* pDst = content) {
                Native.Movsb(pDst, pSrc, fileSize);
            }
            return FatOperationResult.Success;
        }

        private static int AlignUp(int val, int align) { int m = val % align; return m == 0 ? val : val + (align - m); }

        private struct DirResult {
            public bool Found;
            public bool IsDirectory;
            public uint FirstCluster;
            public uint Size;
        }

        private static char ToChar(byte b) { return (char)b; }

        private string ComposeShortName(byte* name83) {
            int nameLen = 8; while (nameLen > 0 && name83[nameLen - 1] == (byte)' ') nameLen--;
            int extLen = 3; while (extLen > 0 && name83[8 + extLen - 1] == (byte)' ') extLen--;
            int resultLength = nameLen + (extLen > 0 ? extLen + 1 : 0);
            char[] chars = new char[resultLength];
            for (int i = 0; i < nameLen; i++) chars[i] = (char)name83[i];
            if (extLen > 0) {
                chars[nameLen] = '.';
                for (int i = 0; i < extLen; i++)
                    chars[nameLen + 1 + i] = (char)name83[8 + i];
            }
            return new string(chars, 0, resultLength);
        }

        private static string AppendUtf16(string s, ushort ch) { if (ch == 0xFFFF || ch == 0x0000) return s; return s + (char)ch; }

        private string AssembleLfn(LfnEntry* lfnParts, int count) {
            string name = string.Empty;
            for (int i = count - 1; i >= 0; i--) {
                var p = lfnParts + i;
                for (int j = 0; j < 5; j++) name = AppendUtf16(name, p->Name1[j]);
                for (int j = 0; j < 6; j++) name = AppendUtf16(name, p->Name2[j]);
                for (int j = 0; j < 2; j++) name = AppendUtf16(name, p->Name3[j]);
            }
            return name;
        }

        private uint GetDirStartCluster(uint dirCluster) { if (_type == FatType.FAT32) return dirCluster == 0 ? _rootCluster : dirCluster; return dirCluster; }

        private bool IterateDirectory(uint dirCluster, Func<string, bool, uint, uint, bool> onEntry) {
            if (_type == FatType.FAT32 || dirCluster >= 2) {
                var chain = GetClusterChain(GetDirStartCluster(dirCluster));
                for (int idx = 0; idx < chain.Count; idx++) { if (!IterateDirSectorRangeCluster(chain[idx], onEntry)) return false; }
                return true;
            } else {
                for (uint s = 0; s < _rootDirSectors; s++) { var sec = ReadSectorsCached(_firstRootDirSector + s, 1); fixed (byte* p = sec) { if (!IterateDirEntries(p, _bytesPerSec, onEntry)) return false; } }
                return true;
            }
        }

        private bool IterateDirSectorRangeCluster(uint cluster, Func<string, bool, uint, uint, bool> onEntry) {
            uint firstSec = FirstSectorOfCluster(cluster);
            for (int i = 0; i < _secPerClus; i++) { var sec = ReadSectorsCached(firstSec + (uint)i, 1); fixed (byte* p = sec) { if (!IterateDirEntries(p, _bytesPerSec, onEntry)) return false; } }
            return true;
        }

        private bool IterateDirEntries(byte* p, int bytes, Func<string, bool, uint, uint, bool> onEntry) {
            int count = bytes / 32; LfnEntry* lfnBuf = stackalloc LfnEntry[20]; int lfnCount = 0;
            for (int i = 0; i < count; i++) {
                byte first = p[i * 32]; if (first == 0x00) break; if (first == 0xE5) { lfnCount = 0; continue; }
                byte attr = p[i * 32 + 11]; if (attr == 0x0F) { var lfn = (LfnEntry*)(p + i * 32); lfnBuf[lfnCount++] = *lfn; continue; }
                var de = (DirEntry*)(p + i * 32); bool isDir = (de->Attr & 0x10) != 0;
                string name = lfnCount > 0 ? AssembleLfn(lfnBuf, lfnCount) : ComposeShortName(de->Name83);
                uint clus = ((uint)de->FstClusHI << 16) | de->FstClusLO; uint size = de->FileSize; lfnCount = 0;
                if (!onEntry(name, isDir, clus, size)) return false;
            }
            return true;
        }

        private static bool EqualsIgnoreCase(string a, string b) {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) { char ca = a[i]; char cb = b[i]; if (ca >= 'a' && ca <= 'z') ca = (char)(ca - 32); if (cb >= 'a' && cb <= 'z') cb = (char)(cb - 32); if (ca != cb) return false; }
            return true;
        }

        private DirResult FindPath(string path) {
            while (path.Length > 0 && path[0] == '/') path = path.Substring(1);
            if (path.Length == 0) return new DirResult { Found = true, IsDirectory = true, FirstCluster = _type == FatType.FAT32 ? _rootCluster : 0, Size = 0 };
            var parts = path.Split('/');
            uint current = _type == FatType.FAT32 ? _rootCluster : 0;
            uint finalSize = 0;
            bool finalIsDirectory = false;
            for (int i = 0; i < parts.Length; i++) {
                string part = parts[i]; bool last = i == parts.Length - 1; bool found = false;
                IterateDirectory(current, (name, isDir, clus, size) => {
                    if (!EqualsIgnoreCase(name, part)) return true;
                    if (!last && !isDir) return true;
                    found = true;
                    current = clus;
                    finalSize = size;
                    finalIsDirectory = isDir;
                    return false;
                });
                if (!found) return new DirResult { Found = false };
                if (!last && current < 2 && _type == FatType.FAT32)
                    return new DirResult { Found = false };
                string releasablePart = part;
                parts[i] = null;
                part = null;
                releasablePart.Dispose();
            }
            return new DirResult { Found = true, IsDirectory = finalIsDirectory, FirstCluster = current, Size = finalSize };
        }
        private struct EntryLoc { public bool Found; public ulong LBA; public int Index; public uint Cluster; public bool RootFixed; public DirEntry Entry; }

        private EntryLoc FindEntryLoc(uint dirCluster, string name, bool findFreeSlot, out bool exists) {
            exists = false; EntryLoc firstFree = default; firstFree.Found = false;
            if (_type == FatType.FAT32 || dirCluster >= 2) {
                var chain = GetClusterChain(GetDirStartCluster(dirCluster));
                for (int cidx = 0; cidx < chain.Count; cidx++) {
                    uint c = chain[cidx]; uint firstSec = FirstSectorOfCluster(c);
                    for (int i = 0; i < _secPerClus; i++) {
                        ulong lba = firstSec + (uint)i; var sec = ReadSectorsCached(lba, 1); fixed (byte* p = sec) {
                            int count = _bytesPerSec / 32; int lfnCount = 0; LfnEntry* lfnBuf = stackalloc LfnEntry[20];
                            for (int e = 0; e < count; e++) {
                                byte first = p[e * 32]; if (first == 0x00) { if (!firstFree.Found) { firstFree.Found = true; firstFree.LBA = lba; firstFree.Index = e; firstFree.Cluster = c; firstFree.RootFixed = false; } goto Done; }
                                if (first == 0xE5) { if (findFreeSlot && !firstFree.Found) { firstFree.Found = true; firstFree.LBA = lba; firstFree.Index = e; firstFree.Cluster = c; firstFree.RootFixed = false; } lfnCount = 0; continue; }
                                byte attr = p[e * 32 + 11]; if (attr == 0x0F) { var lfn = (LfnEntry*)(p + e * 32); lfnBuf[lfnCount++] = *lfn; continue; }
                                var de = (DirEntry*)(p + e * 32); string ename = lfnCount > 0 ? AssembleLfn(lfnBuf, lfnCount) : ComposeShortName(de->Name83); lfnCount = 0;
                                if (EqualsIgnoreCase(ename, name)) { exists = true; EntryLoc loc; loc.Found = true; loc.LBA = lba; loc.Index = e; loc.Cluster = c; loc.RootFixed = false; loc.Entry = *de; return loc; }
                            }
                        }
                    }
                }
            } else {
                for (uint s = 0; s < _rootDirSectors; s++) {
                    ulong lba = _firstRootDirSector + s; var sec = ReadSectorsCached(lba, 1); fixed (byte* p = sec) {
                        int count = _bytesPerSec / 32; int lfnCount = 0; LfnEntry* lfnBuf = stackalloc LfnEntry[20];
                        for (int e = 0; e < count; e++) {
                            byte first = p[e * 32]; if (first == 0x00) { if (!firstFree.Found) { firstFree.Found = true; firstFree.LBA = lba; firstFree.Index = e; firstFree.Cluster = 0; firstFree.RootFixed = true; } goto Done; }
                            if (first == 0xE5) { if (findFreeSlot && !firstFree.Found) { firstFree.Found = true; firstFree.LBA = lba; firstFree.Index = e; firstFree.Cluster = 0; firstFree.RootFixed = true; } lfnCount = 0; continue; }
                            byte attr = p[e * 32 + 11]; if (attr == 0x0F) { var lfn = (LfnEntry*)(p + e * 32); lfnBuf[lfnCount++] = *lfn; continue; }
                            var de = (DirEntry*)(p + e * 32); string ename = lfnCount > 0 ? AssembleLfn(lfnBuf, lfnCount) : ComposeShortName(de->Name83); lfnCount = 0;
                            if (EqualsIgnoreCase(ename, name)) { exists = true; EntryLoc loc; loc.Found = true; loc.LBA = lba; loc.Index = e; loc.Cluster = 0; loc.RootFixed = true; loc.Entry = *de; return loc; }
                        }
                    }
                }
            }
        Done:
            return firstFree;
        }

        private uint NextFreeCluster(uint start) {
            uint clusterCount = _clusterCount;
            if (clusterCount == 0) return 0;
            uint lastClusterNum = clusterCount + 1U;
            if (start < 2 || start > lastClusterNum) start = 2;
            uint firstOffset = start - 2U;
            for (uint scanned = 0; scanned < clusterCount; scanned++) {
                uint idx = 2U + ((firstOffset + scanned) % clusterCount);
                if (ReadFatEntry(idx) == 0) return idx;
                if (_stickyIoResult != DiskIoResult.Success) return 0;
            }
            // No free clusters available
            return 0;
        }

        private List<uint> AllocateClustersForSize(uint size) {
            List<uint> chain = new List<uint>();
            if (size == 0) return chain;
            int clustersNeeded = AlignUp((int)size, _secPerClus * _bytesPerSec) / (_secPerClus * _bytesPerSec);
            uint prev = 0; uint search = 2;
            for (int i = 0; i < clustersNeeded; i++) {
                uint c = NextFreeCluster(search); 
                if (c == 0) {
                    // Out of space - free what we allocated and return empty chain
                    if (chain.Count > 0) {
                        FreeClusterChain(chain[0]);
                    }
                    return new List<uint>();
                }
                // mark allocated
                if (!WriteFatEntry(c, EOC())) return new List<uint>();
                if (prev != 0 && !WriteFatEntry(prev, c)) return new List<uint>();
                chain.Add(c);
                prev = c; search = c + 1;
            }
            return chain;
        }

        private bool FreeClusterChain(uint start) {
            if (start < 2) return true;
            uint c = start;
            uint lastValidCluster = _clusterCount + 1U;
            for (uint visited = 0; visited < _clusterCount; visited++) {
                if (c < 2 || c > lastValidCluster) return false;
                uint next = ReadFatEntry(c);
                if (_stickyIoResult != DiskIoResult.Success || !WriteFatEntry(c, 0)) return false;
                if (IsEOC(next) || next == 0 || next == c) return true;
                c = next;
            }
            // A chain longer than the volume's cluster count is cyclic or corrupt.
            return false;
        }

        private bool WriteDataToChain(List<uint> chain, byte[] content) {
            int clusterBytes = _secPerClus * _bytesPerSec; int offset = 0; var scratch = new byte[clusterBytes];
            for (int i = 0; i < chain.Count; i++) {
                // prepare buffer
                int remaining = content.Length - offset; if (remaining < 0) remaining = 0; int toCopy = remaining > clusterBytes ? clusterBytes : remaining;
                // zero scratch
                for (int k = 0; k < clusterBytes; k++) scratch[k] = 0;
                if (toCopy > 0) {
                    fixed (byte* pSrc = content)
                    fixed (byte* pDst = scratch) {
                        Native.Movsb(pDst, pSrc + offset, (ulong)toCopy);
                    }
                }
                // write sectors
                uint firstSec = FirstSectorOfCluster(chain[i]);
                for (int s = 0; s < _secPerClus; s++) {
                    var slice = new byte[_bytesPerSec];
                    for (int b = 0; b < _bytesPerSec; b++) slice[b] = scratch[s * _bytesPerSec + b];
                    if (WriteSector(firstSec + (uint)s, slice) != DiskIoResult.Success) return false;
                }
                offset += toCopy;
                if (offset >= content.Length) break;
            }
            return true;
        }

        private bool WriteDirEntry(EntryLoc slot, string shortName, uint firstCluster, uint fileSize, byte attr) {
            var sec = ReadSectorsCached(slot.LBA, 1);
            if (_stickyIoResult != DiskIoResult.Success) return false;
            fixed (byte* p = sec) {
                DirEntry* de = (DirEntry*)(p + slot.Index * 32);
                // name
                for (int i = 0; i < 11; i++) de->Name83[i] = (byte)' ';
                int dot = shortName.LastIndexOf('.'); string name = shortName; string ext = "";
                if (dot >= 0) { name = shortName.Substring(0, dot); ext = shortName.Substring(dot + 1); }
                name = name.ToUpper(); ext = ext.ToUpper();
                for (int i = 0; i < name.Length && i < 8; i++) de->Name83[i] = (byte)name[i];
                for (int i = 0; i < ext.Length && i < 3; i++) de->Name83[8 + i] = (byte)ext[i];
                de->Attr = attr;
                de->FstClusHI = (ushort)((firstCluster >> 16) & 0xFFFF);
                de->FstClusLO = (ushort)(firstCluster & 0xFFFF);
                de->FileSize = fileSize;
            }
            return WriteSector(slot.LBA, sec) == DiskIoResult.Success;
        }

        private string GenerateShortName(string name) {
            // Simple 8.3 upper-case generator; strip invalid chars
            string n = name; int lastSlash = n.LastIndexOf('/'); if (lastSlash >= 0) n = n.Substring(lastSlash + 1);
            int dot = n.LastIndexOf('.'); string baseN = dot >= 0 ? n.Substring(0, dot) : n; string ext = dot >= 0 ? n.Substring(dot + 1) : "";
            string filtered = ""; for (int i = 0; i < baseN.Length; i++) { char c = baseN[i]; if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_') filtered += c; }
            string filteredExt = ""; for (int i = 0; i < ext.Length; i++) { char c = ext[i]; if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_') filteredExt += c; }
            if (filtered.Length > 8) filtered = filtered.Substring(0, 8);
            if (filteredExt.Length > 3) filteredExt = filteredExt.Substring(0, 3);
            // upper
            string up = ""; for (int i = 0; i < filtered.Length; i++) { char c = filtered[i]; if (c >= 'a' && c <= 'z') c = (char)(c - 32); up += c; }
            string e2 = ""; for (int i = 0; i < filteredExt.Length; i++) { char c = filteredExt[i]; if (c >= 'a' && c <= 'z') c = (char)(c - 32); e2 += c; }
            return e2.Length > 0 ? (up + "." + e2) : up;
        }

        private bool EnsureDirHasFreeSlot(uint dirCluster, ref EntryLoc slot) {
            if (slot.Found) return true;
            // need to expand directory if possible
            if (_type == FatType.FAT12 || _type == FatType.FAT16) {
                if (dirCluster == 0) {
                    // Root directory is full on FAT12/16 - cannot expand
                    return false;
                }
            }
            // FAT32 or subdir: append a new cluster
            uint lastCluster = 0; var chain = GetClusterChain(GetDirStartCluster(dirCluster)); if (chain.Count > 0) lastCluster = chain[chain.Count - 1];
            if (_stickyIoResult != DiskIoResult.Success) return false;
            uint newc = NextFreeCluster(2); 
            if (newc == 0) {
                // No free clusters available
                return false;
            }
            if (!WriteFatEntry(newc, EOC())) return false;
            if (lastCluster != 0 && !WriteFatEntry(lastCluster, newc)) return false;
            if (!ZeroCluster(newc)) return false;
            // first entry of new cluster
            slot.Found = true; slot.Cluster = newc; slot.RootFixed = false; slot.LBA = FirstSectorOfCluster(newc); slot.Index = 0;
            return true;
        } 

        public override List<FileInfo> GetFiles(string Directory) {
            // Normalize Directory to have trailing '/'
            string dir = Directory; if (dir.Length > 0 && dir[dir.Length - 1] == '/') dir = dir.Substring(0, dir.Length - 1);
            // Find directory cluster
            uint dirCluster = _type == FatType.FAT32 ? _rootCluster : 0;
            if (!string.IsNullOrEmpty(dir)) { var rr = FindPath(dir); if (!rr.Found) return new List<FileInfo>(); dirCluster = rr.FirstCluster; }
            List<FileInfo> list = new List<FileInfo>();
            IterateDirectory(dirCluster, (name, isDir, clus, size) => {
                // Skip '.' and '..'
                if (name == "." || name == "..") return true;
                var fi = new FileInfo(); fi.Name = name; if (isDir) fi.Attribute |= FileAttribute.Directory; fi.Param0 = clus; fi.Param1 = size; list.Add(fi); return true;
            });
            return list;
        }

        public override void Delete(string Name) {
            TryDelete(Name);
        }

        public override FatOperationResult TryDelete(string Name) {
            if (!StartOperation()) return LastOperationResult = FatOperationResult.NotMounted;
            _pendingMutationResult = FatOperationResult.Success;
            if (string.IsNullOrEmpty(Name)) return FinishMutation(FatOperationResult.InvalidPath);
            // Only files for now
            string parent = Name.LastIndexOf('/') >= 0 ? Name.Substring(0, Name.LastIndexOf('/')) : "";
            string just = Name.Substring(Name.LastIndexOf('/') + 1);
            uint parentCluster = _type == FatType.FAT32 ? _rootCluster : 0;
            if (parent.Length > 0) { 
                var pr = FindPath(parent); 
                if (!pr.Found) { 
                    return FinishMutation(_stickyIoResult == DiskIoResult.Success
                        ? FatOperationResult.NotFound : CurrentIoFailure(false));
                }
                parentCluster = pr.FirstCluster; 
            }
            if (_stickyIoResult != DiskIoResult.Success) return FinishMutation(CurrentIoFailure(false));
            bool exists; 
            var loc = FindEntryLoc(parentCluster, just, false, out exists); 
            if (!exists || !loc.Found) { 
                return FinishMutation(_stickyIoResult == DiskIoResult.Success
                    ? FatOperationResult.NotFound : CurrentIoFailure(false));
            }
            if (_stickyIoResult != DiskIoResult.Success) return FinishMutation(CurrentIoFailure(false));
            uint firstClus = ((uint)loc.Entry.FstClusHI << 16) | loc.Entry.FstClusLO; 
            if (firstClus >= 2 && !FreeClusterChain(firstClus))
                return FinishMutation(CurrentIoFailure(true));
            // mark deleted
            var sec = ReadSectorsCached(loc.LBA, 1); 
            if (_stickyIoResult != DiskIoResult.Success) return FinishMutation(CurrentIoFailure(false));
            sec[loc.Index * 32] = 0xE5; 
            if (WriteSector(loc.LBA, sec) != DiskIoResult.Success)
                return FinishMutation(CurrentIoFailure(true));
            parent.Dispose(); just.Dispose();
            return FinishMutation(FatOperationResult.Success);
        }

        public override byte[] ReadAllBytes(string Name) {
            FatOperationResult result = TryReadAllBytes(Name, out byte[] content);
            if (result != FatOperationResult.Success) {
                Panic.Error("FAT read failed: " + result.ToString());
                return null;
            }
            return content;
        }

        public override void WriteAllBytes(string Name, byte[] Content) {
            TryWriteAllBytes(Name, Content);
        }

        public FatOperationResult TryReadAllBytes(string Name, out byte[] content) {
            content = null;
            if (!StartOperation()) return LastOperationResult = FatOperationResult.NotMounted;
            if (string.IsNullOrEmpty(Name)) return LastOperationResult = FatOperationResult.InvalidPath;
            var res = FindPath(Name);
            if (_stickyIoResult != DiskIoResult.Success) return LastOperationResult = CurrentIoFailure(false);
            if (!res.Found) return LastOperationResult = FatOperationResult.NotFound;
            if (res.IsDirectory) return LastOperationResult = FatOperationResult.InvalidPath;
            if (res.FirstCluster < 2 && _type == FatType.FAT32)
                return LastOperationResult = FatOperationResult.InvalidPath;
            if (res.Size == 0) {
                content = new byte[0];
            } else if (res.FirstCluster < 2) {
                return LastOperationResult = FatOperationResult.InvalidPath;
            } else {
                FatOperationResult chainResult = ReadAllClusterChain(res.FirstCluster, res.Size, out content);
                if (chainResult != FatOperationResult.Success) return LastOperationResult = chainResult;
            }
            if (_stickyIoResult != DiskIoResult.Success) {
                content = null;
                return LastOperationResult = CurrentIoFailure(false);
            }
            return LastOperationResult = FatOperationResult.Success;
        }

        /// <summary>Reads a bounded byte range directly from the file's FAT chain.</summary>
        public override FatOperationResult TryReadRange(string Name, long offset,
                byte[] destination, int destinationOffset, int maximumBytes,
                out int bytesRead, out bool endOfFile) {
            bytesRead = 0;
            endOfFile = false;
            if (!StartOperation()) return LastOperationResult = FatOperationResult.NotMounted;
            if (string.IsNullOrEmpty(Name))
                return LastOperationResult = FatOperationResult.InvalidPath;
            if (offset < 0 || destination == null || destinationOffset < 0 ||
                    maximumBytes < 0 || destinationOffset > destination.Length - maximumBytes)
                return LastOperationResult = FatOperationResult.InvalidBuffer;

            DirResult file = FindPath(Name);
            if (_stickyIoResult != DiskIoResult.Success)
                return LastOperationResult = CurrentIoFailure(false);
            if (!file.Found) return LastOperationResult = FatOperationResult.NotFound;
            if (file.IsDirectory) return LastOperationResult = FatOperationResult.InvalidPath;
            if (offset > file.Size) return LastOperationResult = FatOperationResult.InvalidRange;
            if (offset == file.Size || maximumBytes == 0) {
                endOfFile = offset == file.Size;
                return LastOperationResult = FatOperationResult.Success;
            }
            if (file.FirstCluster < 2)
                return LastOperationResult = FatOperationResult.InvalidPath;

            ulong available = (ulong)file.Size - (ulong)offset;
            int targetBytes = available > (ulong)maximumBytes
                ? maximumBytes : (int)available;
            ulong clusterBytes = (ulong)_secPerClus * _bytesPerSec;
            uint lastValidCluster = _clusterCount + 1U;
            if (clusterBytes == 0 || clusterBytes > int.MaxValue ||
                    file.FirstCluster > lastValidCluster)
                return LastOperationResult = FatOperationResult.InvalidRange;

            ulong clusterIndex = (ulong)offset / clusterBytes;
            if (clusterIndex >= _clusterCount)
                return LastOperationResult = FatOperationResult.InvalidRange;
            int clusterOffset = (int)((ulong)offset % clusterBytes);
            var visited = new List<uint>();
            uint cluster = file.FirstCluster;
            for (ulong i = 0; i <= clusterIndex; i++) {
                if (cluster < 2 || cluster > lastValidCluster)
                    return LastOperationResult = FatOperationResult.InvalidRange;
                for (int j = 0; j < visited.Count; j++)
                    if (visited[j] == cluster)
                        return LastOperationResult = FatOperationResult.InvalidRange;
                visited.Add(cluster);
                if (i < clusterIndex) {
                    cluster = ReadFatEntry(cluster);
                    if (_stickyIoResult != DiskIoResult.Success)
                        return LastOperationResult = CurrentIoFailure(false);
                    if (cluster < 2 || cluster > lastValidCluster || IsEOC(cluster))
                        return LastOperationResult = FatOperationResult.InvalidRange;
                }
            }

            while (bytesRead < targetBytes) {
                int withinCluster = clusterOffset;
                int clusterRemaining = (int)clusterBytes - withinCluster;
                int copyBytes = targetBytes - bytesRead;
                if (copyBytes > clusterRemaining) copyBytes = clusterRemaining;
                FatOperationResult rangeResult = ReadClusterRange(cluster,
                    withinCluster, destination, destinationOffset + bytesRead,
                    copyBytes);
                if (rangeResult != FatOperationResult.Success)
                    return LastOperationResult = rangeResult;
                bytesRead += copyBytes;
                clusterOffset = 0;
                if (bytesRead < targetBytes) {
                    cluster = ReadFatEntry(cluster);
                    if (_stickyIoResult != DiskIoResult.Success)
                        return LastOperationResult = CurrentIoFailure(false);
                    if (cluster < 2 || cluster > lastValidCluster || IsEOC(cluster))
                        return LastOperationResult = FatOperationResult.InvalidRange;
                    for (int j = 0; j < visited.Count; j++)
                        if (visited[j] == cluster)
                            return LastOperationResult = FatOperationResult.InvalidRange;
                    visited.Add(cluster);
                }
            }
            endOfFile = (ulong)offset + (uint)bytesRead == file.Size;
            return LastOperationResult = FatOperationResult.Success;
        }

        private FatOperationResult ReadClusterRange(uint cluster, int clusterOffset,
                byte[] destination, int destinationOffset, int byteCount) {
            uint firstSector = FirstSectorOfCluster(cluster);
            int remaining = byteCount;
            int sourceOffset = clusterOffset;
            int outputOffset = destinationOffset;
            while (remaining > 0) {
                uint sectorIndex = (uint)(sourceOffset / _bytesPerSec);
                int sectorOffset = sourceOffset % _bytesPerSec;
                int copyBytes = _bytesPerSec - sectorOffset;
                if (copyBytes > remaining) copyBytes = remaining;
                byte[] sector = ReadSectorsCached(firstSector + sectorIndex, 1);
                if (_stickyIoResult != DiskIoResult.Success)
                    return CurrentIoFailure(false);
                for (int i = 0; i < copyBytes; i++)
                    destination[outputOffset + i] = sector[sectorOffset + i];
                sourceOffset += copyBytes;
                outputOffset += copyBytes;
                remaining -= copyBytes;
            }
            return FatOperationResult.Success;
        }

        /// <summary>Reads the bounded directory entry metadata for one file.</summary>
        public FatOperationResult TryGetFileLength(string name, out uint length) {
            length = 0;
            if (!StartOperation()) return LastOperationResult = FatOperationResult.NotMounted;
            if (string.IsNullOrEmpty(name))
                return LastOperationResult = FatOperationResult.InvalidPath;
            DirResult result = FindPath(name);
            if (_stickyIoResult != DiskIoResult.Success)
                return LastOperationResult = CurrentIoFailure(false);
            if (!result.Found) return LastOperationResult = FatOperationResult.NotFound;
            if (result.IsDirectory)
                return LastOperationResult = FatOperationResult.InvalidPath;
            length = result.Size;
            return LastOperationResult = FatOperationResult.Success;
        }

        /// <summary>
        /// Enumerates one directory with a caller supplied hard bound and
        /// preserves typed not-found and media-read failures.
        /// </summary>
        public FatOperationResult TryGetFiles(string directory, int maximumEntries,
                out List<FileInfo> entries) {
            entries = new List<FileInfo>();
            List<FileInfo> foundEntries = entries;
            if (!StartOperation()) return LastOperationResult = FatOperationResult.NotMounted;
            if (directory == null || maximumEntries <= 0)
                return LastOperationResult = FatOperationResult.InvalidPath;
            uint directoryCluster = _type == FatType.FAT32 ? _rootCluster : 0;
            if (directory.Length > 0) {
                DirResult result = FindPath(directory);
                if (_stickyIoResult != DiskIoResult.Success) {
                    DisposeEntries(entries);
                    return LastOperationResult = CurrentIoFailure(false);
                }
                if (!result.Found) {
                    DisposeEntries(entries);
                    return LastOperationResult = FatOperationResult.NotFound;
                }
                if (!result.IsDirectory) {
                    DisposeEntries(entries);
                    return LastOperationResult = FatOperationResult.InvalidPath;
                }
                directoryCluster = result.FirstCluster;
            }

            bool exceeded = false;
            IterateDirectory(directoryCluster, (name, isDirectory, cluster, size) => {
                if (name == "." || name == "..") return true;
                if (foundEntries.Count >= maximumEntries) {
                    exceeded = true;
                    return false;
                }
                FileInfo info = new FileInfo();
                info.Name = name;
                if (isDirectory) info.Attribute |= FileAttribute.Directory;
                info.Param0 = cluster;
                info.Param1 = size;
                foundEntries.Add(info);
                return true;
            });

            if (_stickyIoResult != DiskIoResult.Success) {
                DisposeEntries(foundEntries);
                entries = new List<FileInfo>();
                return LastOperationResult = CurrentIoFailure(false);
            }
            if (exceeded) {
                DisposeEntries(foundEntries);
                entries = new List<FileInfo>();
                return LastOperationResult = FatOperationResult.EntryLimitExceeded;
            }
            return LastOperationResult = FatOperationResult.Success;
        }

        private static void DisposeEntries(List<FileInfo> entries) {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++) {
                if (entries[i] != null) entries[i].Dispose();
            }
        }

        public override FatOperationResult TryWriteAllBytes(string Name, byte[] Content) {
            if (!StartOperation()) return FinishMutation(FatOperationResult.NotMounted);
            _pendingMutationResult = FatOperationResult.Success;
            if (string.IsNullOrEmpty(Name) || Content == null)
                return FinishMutation(FatOperationResult.InvalidPath);
            // resolve parent
            string parent = Name.LastIndexOf('/') >= 0 ? Name.Substring(0, Name.LastIndexOf('/')) : "";
            string just = Name.Substring(Name.LastIndexOf('/') + 1);
            uint parentCluster = _type == FatType.FAT32 ? _rootCluster : 0;
            if (parent.Length > 0) { 
                var pr = FindPath(parent); 
                if (!pr.Found) {
                    return FinishMutation(_stickyIoResult == DiskIoResult.Success
                        ? FatOperationResult.NotFound : CurrentIoFailure(false));
                }
                parentCluster = pr.FirstCluster; 
            }
            if (_stickyIoResult != DiskIoResult.Success) return FinishMutation(CurrentIoFailure(false));
            // locate entry or free slot
            bool exists; var loc = FindEntryLoc(parentCluster, just, true, out exists);
            if (_stickyIoResult != DiskIoResult.Success) return FinishMutation(CurrentIoFailure(false));
            if (!loc.Found && !exists) { 
                if (!EnsureDirHasFreeSlot(parentCluster, ref loc)) {
                    return FinishMutation(_stickyIoResult == DiskIoResult.Success
                        ? FatOperationResult.NoSpace : CurrentIoFailure(true));
                }
                if (!loc.Found) {
                    return FinishMutation(FatOperationResult.NoSpace);
                }
            }
            if (exists && (loc.Entry.Attr & 0x10) != 0)
                return FinishMutation(FatOperationResult.InvalidPath);
            uint oldFirstCluster = exists ? (((uint)loc.Entry.FstClusHI << 16) | loc.Entry.FstClusLO) : 0;
            uint firstClus = 0;
            List<uint> chain = AllocateClustersForSize((uint)Content.Length);
            if (_stickyIoResult != DiskIoResult.Success) return FinishMutation(CurrentIoFailure(true));
            if (Content.Length > 0 && chain.Count == 0) return FinishMutation(FatOperationResult.NoSpace);
            if (chain.Count > 0) firstClus = chain[0];
            if (Content.Length > 0 && !WriteDataToChain(chain, Content))
                return FinishMutation(CurrentIoFailure(true));
            string shortName = GenerateShortName(just);
            if (!WriteDirEntry(loc, shortName, firstClus, (uint)Content.Length, 0x20))
                return FinishMutation(CurrentIoFailure(true));
            if (exists) {
                if (oldFirstCluster >= 2 && !FreeClusterChain(oldFirstCluster))
                    return FinishMutation(CurrentIoFailure(true));
            }
            // cleanup
            parent.Dispose(); just.Dispose(); shortName.Dispose();
            return FinishMutation(FatOperationResult.Success);
        }

        private FatOperationResult FinishMutation(FatOperationResult result) {
            LastOperationResult = result;
            if (result != FatOperationResult.Success) _pendingMutationResult = result;
            return result;
        }

        /// <summary>
        /// Creates a directory on the FAT filesystem. Creates parent directories as needed.
        /// </summary>
        public FatOperationResult CreateDirectory(string path) {
            if (!StartOperation()) return FinishMutation(FatOperationResult.NotMounted);
            _pendingMutationResult = FatOperationResult.Success;
            _lastCreateDirectoryFailureIndex = -1;
            _lastCreateDirectoryPartCount = 0;
            if (path == null) return FinishMutation(FatOperationResult.InvalidPath);
            // Normalize: strip leading/trailing slashes
            while (path.Length > 0 && path[0] == '/') path = path.Substring(1);
            while (path.Length > 0 && path[path.Length - 1] == '/') path = path.Substring(0, path.Length - 1);
            if (path.Length == 0) return FinishMutation(FatOperationResult.InvalidPath);

            // Split into components and create each level
            var parts = path.Split('/');
            _lastCreateDirectoryPartCount = parts.Length;
            string built = "";
            for (int pi = 0; pi < parts.Length; pi++) {
                string component = parts[pi];
                if (component.Length == 0) continue;

                string parentPath = built;
                built = built.Length > 0 ? built + "/" + component : component;

                // Check if this level already exists
                var check = FindPath(built);
                if (_stickyIoResult != DiskIoResult.Success) return FinishMutation(CurrentIoFailure(false));
                if (check.Found) continue;

                // Resolve parent cluster
                uint parentCluster = _type == FatType.FAT32 ? _rootCluster : 0;
                if (parentPath.Length > 0) {
                    var pr = FindPath(parentPath);
                    if (!pr.Found) {
                        _lastCreateDirectoryFailureIndex = pi;
                        return FinishMutation(_stickyIoResult == DiskIoResult.Success
                            ? FatOperationResult.NotFound : CurrentIoFailure(false));
                    }
                    parentCluster = pr.FirstCluster;
                }

                // Find a free directory entry slot in the parent
                bool exists;
                var loc = FindEntryLoc(parentCluster, component, true, out exists);
                if (_stickyIoResult != DiskIoResult.Success) return FinishMutation(CurrentIoFailure(false));
                if (exists) continue; // already exists
                if (!loc.Found) {
                    if (!EnsureDirHasFreeSlot(parentCluster, ref loc))
                        return FinishMutation(_stickyIoResult == DiskIoResult.Success
                            ? FatOperationResult.NoSpace : CurrentIoFailure(true));
                    if (!loc.Found) return FinishMutation(FatOperationResult.NoSpace);
                }

                // Allocate one cluster for the new directory
                uint newCluster = NextFreeCluster(2);
                if (newCluster == 0) return FinishMutation(_stickyIoResult == DiskIoResult.Success
                    ? FatOperationResult.NoSpace : CurrentIoFailure(false));
                if (!WriteFatEntry(newCluster, EOC())) return FinishMutation(CurrentIoFailure(true));
                if (!ZeroCluster(newCluster)) return FinishMutation(CurrentIoFailure(true));

                // Write "." entry (points to self)
                uint dotSec = FirstSectorOfCluster(newCluster);
                var dotData = ReadSectorsCached(dotSec, 1);
                fixed (byte* p = dotData) {
                    DirEntry* dot = (DirEntry*)p;
                    for (int k = 0; k < 11; k++) dot->Name83[k] = (byte)' ';
                    dot->Name83[0] = (byte)'.';
                    dot->Attr = 0x10;
                    dot->FstClusHI = (ushort)((newCluster >> 16) & 0xFFFF);
                    dot->FstClusLO = (ushort)(newCluster & 0xFFFF);
                    dot->FileSize = 0;

                    // Write ".." entry (points to parent)
                    DirEntry* dotdot = (DirEntry*)(p + 32);
                    for (int k = 0; k < 11; k++) dotdot->Name83[k] = (byte)' ';
                    dotdot->Name83[0] = (byte)'.';
                    dotdot->Name83[1] = (byte)'.';
                    dotdot->Attr = 0x10;
                    dotdot->FstClusHI = (ushort)((parentCluster >> 16) & 0xFFFF);
                    dotdot->FstClusLO = (ushort)(parentCluster & 0xFFFF);
                    dotdot->FileSize = 0;
                }
                if (WriteSector(dotSec, dotData) != DiskIoResult.Success)
                    return FinishMutation(CurrentIoFailure(true));

                // Write the directory entry in the parent
                string shortName = GenerateShortName(component);
                if (!WriteDirEntry(loc, shortName, newCluster, 0, 0x10))
                    return FinishMutation(CurrentIoFailure(true));
                shortName.Dispose();
            }
            return FinishMutation(FatOperationResult.Success);
        }

        /// <summary>Flushes write-through FAT mutations to the device's durable-media command.</summary>
        public override FatOperationResult TrySync() {
            if (!_mounted || disk == null) return LastOperationResult = FatOperationResult.NotMounted;
            DiskIoResult result = disk.Flush();
            if (result == DiskIoResult.FlushUnsupported)
                return LastOperationResult = FatOperationResult.FlushUnsupported;
            if (result == DiskIoResult.MediaUnavailable)
                return LastOperationResult = FatOperationResult.MediaUnavailable;
            if (result != DiskIoResult.Success)
                return LastOperationResult = FatOperationResult.FlushFailure;
            if (_pendingMutationResult != FatOperationResult.Success)
                return LastOperationResult = _pendingMutationResult;
            return LastOperationResult = FatOperationResult.Success;
        }

        public string FileSystemVariant => _type == FatType.FAT12 ? "FAT12" :
            (_type == FatType.FAT16 ? "FAT16" : "FAT32");

        public override void Format() { TryFormat(); }

        /// <summary>Formats this disk and reports each sector-write and final-flush result.</summary>
        public override FatOperationResult TryFormat() {
            Disk target = disk;
            _stickyIoResult = DiskIoResult.Success;
            _pendingMutationResult = FatOperationResult.Success;
            _mounted = false;
            MountResult = DiskIoResult.MediaUnavailable;
            if (target == null) return FinishMutation(FatOperationResult.NotMounted);
            if (target.BlockSize != SectorSize || target.BlockCount < 4096UL ||
                target.BlockCount > 0xFFFFFFFFUL)
                return FinishMutation(FatOperationResult.Unsupported);
            if ((target.Capabilities & DiskCapabilities.Writable) == 0)
                return FinishMutation(FatOperationResult.ReadOnly);
            if ((target.Capabilities & DiskCapabilities.FlushSupported) == 0)
                return FinishMutation(FatOperationResult.FlushUnsupported);
            if (!target.IsAvailable) return FinishMutation(FatOperationResult.NotMounted);

            // Use actual device geometry; never guess a larger default capacity.
            ulong totalSectors = target.BlockCount;
            
            // Choose FAT type based on size
            bool useFAT32 = totalSectors > 65525 * 8; // > ~256MB
            ushort bytesPerSec = 512;
            byte secPerClus = 8; // 4KB clusters
            ushort rsvdSecCnt = (ushort)(useFAT32 ? 32 : 1);
            byte numFATs = 2;
            ushort rootEntCnt = (ushort)(useFAT32 ? 0 : 512);
            uint rootDirSectors = (uint)((rootEntCnt * 32 + (bytesPerSec - 1)) / bytesPerSec);
            
            // Calculate FAT size
            ulong tmpVal1 = totalSectors - (ulong)(rsvdSecCnt + rootDirSectors);
            ulong tmpVal2 = (ulong)((useFAT32 ? 128 : 256) * secPerClus) + numFATs;
            ulong fatSize = (tmpVal1 + tmpVal2 - 1UL) / tmpVal2;
            if (fatSize == 0 || fatSize > 0xFFFFFFFFUL)
                return FinishMutation(FatOperationResult.Unsupported);
            uint FATSz = (uint)fatSize;
            
            // Create boot sector
            byte[] bootSector = new byte[512];
            for (int i = 0; i < 512; i++) bootSector[i] = 0;
            
            fixed (byte* p = bootSector) {
                BPB_Common* bpb = (BPB_Common*)p;
                
                // Jump instruction
                bpb->jmpBoot[0] = 0xEB;
                bpb->jmpBoot[1] = 0x58;
                bpb->jmpBoot[2] = 0x90;
                
                // OEM name
                string oem = "GUIDEXOS";
                for (int i = 0; i < 8; i++) bpb->OEMName[i] = (byte)(i < oem.Length ? oem[i] : ' ');
                
                bpb->BytsPerSec = bytesPerSec;
                bpb->SecPerClus = secPerClus;
                bpb->RsvdSecCnt = rsvdSecCnt;
                bpb->NumFATs = numFATs;
                bpb->RootEntCnt = rootEntCnt;
                bpb->TotSec16 = (ushort)(totalSectors < 65536 && !useFAT32 ? totalSectors : 0);
                bpb->Media = 0xF8;
                bpb->FATSz16 = (ushort)(useFAT32 ? 0 : FATSz);
                bpb->SecPerTrk = 63;
                bpb->NumHeads = 255;
                bpb->HiddSec = 0;
                bpb->TotSec32 = (uint)(totalSectors >= 65536 || useFAT32 ? totalSectors : 0);
                
                if (useFAT32) {
                    BPB_FAT32* bpb32 = (BPB_FAT32*)(p + 0x24);
                    bpb32->FATSz32 = FATSz;
                    bpb32->ExtFlags = 0;
                    bpb32->FSVer = 0;
                    bpb32->RootClus = 2;
                    bpb32->FSInfo = 1;
                    bpb32->BkBootSec = 6;
                    bpb32->DrvNum = 0x80;
                    bpb32->BootSig = 0x29;
                    bpb32->VolID = 0x12345678;
                    
                    string label = "GUIDEXOS   ";
                    for (int i = 0; i < 11; i++) bpb32->VolLab[i] = (byte)(i < label.Length ? label[i] : ' ');
                    
                    string fstype = "FAT32   ";
                    for (int i = 0; i < 8; i++) bpb32->FilSysType[i] = (byte)(i < fstype.Length ? fstype[i] : ' ');
                }
                
                // Boot signature
                bootSector[510] = 0x55;
                bootSector[511] = 0xAA;
            }
            
            // Write boot sector
            if (WriteSector(0, bootSector) != DiskIoResult.Success)
                return FinishMutation(CurrentIoFailure(true));
            
            // Clear reserved sectors
            byte[] zeroSec = new byte[512];
            for (int i = 0; i < 512; i++) zeroSec[i] = 0;
            
            for (ulong s = 1; s < rsvdSecCnt; s++) {
                if (WriteSector(s, zeroSec) != DiskIoResult.Success)
                    return FinishMutation(CurrentIoFailure(true));
            }
            
            // Write FSInfo for FAT32
            if (useFAT32) {
                byte[] fsInfo = new byte[512];
                for (int i = 0; i < 512; i++) fsInfo[i] = 0;
                fsInfo[0] = (byte)'R';
                fsInfo[1] = (byte)'R';
                fsInfo[2] = (byte)'a';
                fsInfo[3] = (byte)'A';
                fsInfo[484] = 0xFF; 
                fsInfo[485] = 0xFF; 
                fsInfo[486] = 0xFF; 
                fsInfo[487] = 0xFF;
                fsInfo[488] = 0xFF; 
                fsInfo[489] = 0xFF; 
                fsInfo[490] = 0xFF; 
                fsInfo[491] = 0xFF;
                fsInfo[510] = 0x55;
                fsInfo[511] = 0xAA;
                if (WriteSector(1, fsInfo) != DiskIoResult.Success)
                    return FinishMutation(CurrentIoFailure(true));
            }
            
            // Initialize FAT tables
            ulong fatStart = rsvdSecCnt;
            for (int fatNum = 0; fatNum < numFATs; fatNum++) {
                for (uint i = 0; i < FATSz; i++) {
                    if (WriteSector(fatStart + (ulong)(fatNum * FATSz) + i, zeroSec) != DiskIoResult.Success)
                        return FinishMutation(CurrentIoFailure(true));
                }
            }
            
            // Write initial FAT entries
            byte[] fatFirst = new byte[512];
            for (int i = 0; i < 512; i++) fatFirst[i] = 0;
            
            if (useFAT32) {
                // Media descriptor
                fatFirst[0] = 0xF8; fatFirst[1] = 0xFF; fatFirst[2] = 0xFF; fatFirst[3] = 0x0F;
                // EOC for cluster 1
                fatFirst[4] = 0xFF; fatFirst[5] = 0xFF; fatFirst[6] = 0xFF; fatFirst[7] = 0x0F;
                // Root directory cluster (2)
                fatFirst[8] = 0xFF; fatFirst[9] = 0xFF; fatFirst[10] = 0xFF; fatFirst[11] = 0x0F;
            } else {
                // FAT12/16
                fatFirst[0] = 0xF8;
                fatFirst[1] = 0xFF;
                fatFirst[2] = 0xFF;
                if (!useFAT32 && totalSectors >= 4085) {
                    // FAT16
                    fatFirst[3] = 0xFF;
                }
            }
            
            // Write to all FAT copies
            for (int fatNum = 0; fatNum < numFATs; fatNum++) {
                if (WriteSector(fatStart + (ulong)(fatNum * FATSz), fatFirst) != DiskIoResult.Success)
                    return FinishMutation(CurrentIoFailure(true));
            }
            
            // Clear root directory area
            ulong firstRootSec = fatStart + (ulong)(numFATs * FATSz);
            if (useFAT32) {
                // Root is in data area - clear cluster 2
                ulong firstDataSector = firstRootSec;
                for (int i = 0; i < secPerClus; i++) {
                    if (WriteSector(firstDataSector + (ulong)i, zeroSec) != DiskIoResult.Success)
                        return FinishMutation(CurrentIoFailure(true));
                }
            } else {
                // Fixed root directory for FAT12/16
                for (uint i = 0; i < rootDirSectors; i++) {
                    if (WriteSector(firstRootSec + i, zeroSec) != DiskIoResult.Success)
                        return FinishMutation(CurrentIoFailure(true));
                }
            }
            
            // Re-initialize this FAT instance with the new filesystem
            _cacheKeys = new ulong[CacheCapacity];
            _cacheValues = new byte[CacheCapacity][];
            _cacheCount = 0;
            _lruHead = 0;
            
            var sec0 = ReadSectorsCached(0, 1);
            if (_stickyIoResult != DiskIoResult.Success)
                return FinishMutation(CurrentIoFailure(false));
            fixed (byte* p = sec0) {
                BPB_Common* bpb = (BPB_Common*)p;
                _bytesPerSec = bpb->BytsPerSec;
                _secPerClus = bpb->SecPerClus;
                _rsvdSecCnt = bpb->RsvdSecCnt;
                _numFATs = bpb->NumFATs;
                uint totSec = bpb->TotSec16 != 0 ? bpb->TotSec16 : bpb->TotSec32;
                uint fatsz = bpb->FATSz16;
                if (fatsz == 0) {
                    BPB_FAT32* bpb32 = (BPB_FAT32*)(p + 0x24);
                    fatsz = bpb32->FATSz32;
                    _rootCluster = bpb32->RootClus;
                }
                _FATSz = fatsz;
                uint rootEntCnt2 = bpb->RootEntCnt;
                _rootDirSectors = (uint)((rootEntCnt2 * 32 + (_bytesPerSec - 1)) / _bytesPerSec);
                uint dataSec = totSec - (uint)(_rsvdSecCnt + (_numFATs * fatsz) + _rootDirSectors);
                uint countOfClusters = dataSec / _secPerClus;
                _clusterCount = countOfClusters;
                _type = countOfClusters < 4085 ? FatType.FAT12 : (countOfClusters < 65525 ? FatType.FAT16 : FatType.FAT32);
                _fatStart = _rsvdSecCnt;
                _firstDataSector = (uint)(_rsvdSecCnt + (_numFATs * fatsz) + _rootDirSectors);
                _firstRootDirSector = (uint)(_rsvdSecCnt + (_numFATs * fatsz));
                if (_type != FatType.FAT32) _rootCluster = 0;
            }
            DiskIoResult flushResult = target.Flush();
            if (flushResult == DiskIoResult.FlushUnsupported)
                return FinishMutation(FatOperationResult.FlushUnsupported);
            if (flushResult == DiskIoResult.MediaUnavailable)
                return FinishMutation(FatOperationResult.MediaUnavailable);
            if (flushResult != DiskIoResult.Success)
                return FinishMutation(FatOperationResult.FlushFailure);
            _mounted = true;
            MountResult = DiskIoResult.Success;
            return FinishMutation(FatOperationResult.Success);
        }
    }
}
