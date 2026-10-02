using guideXOS.Misc;
using System;
/// <summary>
/// Allocator with page-level bookkeeping, tagging, and per-owner (window) accounting.
/// Adds overflow guards to prevent bogus huge allocation requests that arise from
/// corrupted length arithmetic (e.g. num*size overflow) and ensures owner counters
/// are decremented correctly.
/// </summary>
abstract unsafe class Allocator {
    /// <summary>
    /// Sync
    /// </summary>
    private static readonly object _sync = new object();
    /// <summary>
    /// Alloc Tag
    /// </summary>
    public enum AllocTag : byte { 
        /// <summary>
        /// Unknown
        /// </summary>
        Unknown = 0, 
        /// <summary>
        /// Thread Meta
        /// </summary>
        ThreadMeta = 1, 
        /// <summary>
        /// Thread Stack
        /// </summary>
        ThreadStack = 2, 
        /// <summary>
        /// Exec Image
        /// </summary>
        ExecImage = 3, 
        /// <summary>
        /// Exec Stack
        /// </summary>
        ExecStack = 4, 
        /// <summary>
        /// Image
        /// </summary>
        Image = 5, 
        /// <summary>
        /// Graphics Temp
        /// </summary>
        GraphicsTemp = 6, 
        /// <summary>
        /// File Buffer
        /// </summary>
        FileBuffer = 7, 
        /// <summary>
        /// Other
        /// </summary>
        Other = 8, 
        /// <summary>
        /// Count
        /// </summary>
        Count = 16 
    }
    /// <summary>
    /// Current OwnerID
    /// </summary>
    public static int CurrentOwnerId = 0; // 0 = kernel/unknown
    
    // Simplified owner tracking - replace Dictionary with parallel arrays
    private const int MAX_OWNERS = 1024; // Support up to 1024 concurrent windows/owners
    private static int[] _ownerIds;
    private static ulong[] _ownerPages;
    private static int _ownerCount;
    
    /// <summary>
    /// Page Size
    /// </summary>
    public const ulong PageSize = 4096; // 4 KiB
    //public const ulong PageSize = 8192; // 8 KiB
    /// <summary>
    /// Num Pages
    /// </summary>
    //public const int NumPages = 131072; // 512 MiB total
    public const int NumPages = 262144; // 1 GiB total
    /// <summary>
    /// Page Signature
    /// </summary>
    public const ulong PageSignature = 0x2E61666E6166696E;
    /// <summary>
    /// Info
    /// </summary>
    public struct Info {
        /// <summary>
        /// Start
        /// </summary>
        public IntPtr Start;
        /// <summary>
        /// Page In Use
        /// </summary>
        public ulong PageInUse;
        /// <summary>
        /// Pages
        /// </summary>
        public fixed ulong Pages[NumPages];
        /// <summary>
        /// Tags
        /// </summary>
        public fixed byte Tags[NumPages];
        /// <summary>
        /// Tag Live pages
        /// </summary>
        public fixed ulong TagLivePages[(int)AllocTag.Count];
        /// <summary>
        /// Owners
        /// </summary>
        public fixed int Owners[NumPages]; // owner id at run start page
    }
    /// <summary>
    /// Info
    /// </summary>
    public static Info _Info;
    
    /// <summary>
    /// Find or add owner index in parallel arrays
    /// </summary>
    private static int FindOrAddOwner(int ownerId) {
        if (ownerId == 0) return -1;
        
        // Search existing owners
        for (int i = 0; i < _ownerCount; i++) {
            if (_ownerIds[i] == ownerId) return i;
        }
        
        // Add new owner if we have space
        if (_ownerCount < MAX_OWNERS) {
            int idx = _ownerCount;
            _ownerIds[idx] = ownerId;
            _ownerPages[idx] = 0;
            _ownerCount++;
            return idx;
        }
        
        // If we're out of space, try to find and reuse a slot with 0 pages
        for (int i = 0; i < MAX_OWNERS; i++) {
            if (_ownerPages[i] == 0) {
                _ownerIds[i] = ownerId;
                _ownerPages[i] = 0;
                return i;
            }
        }
        
        return -1; // No space for more owners
    }
    
    /// <summary>
    /// Initialize
    /// </summary>
    /// <param name="start"></param>
    public static void Initialize(IntPtr start) => Initialize(start, 0, 0);

    /// <summary>
    /// Initialize the allocator and reserve a virtual-address interval that is
    /// occupied by the kernel's linked image. The allocator returns identity
    /// addresses, so this exclusion prevents those addresses from aliasing the
    /// bootloader's high-physical kernel mapping.
    /// </summary>
    public static void Initialize(IntPtr start, ulong reservedAddress,
                                  ulong reservedSize) {
        fixed (Info* pInfo = &_Info) Native.Stosb(pInfo, 0, (ulong)sizeof(Info));
        _Info.Start = start; 
        _Info.PageInUse = 0;

        ReserveAddressRange(start, reservedAddress, reservedSize);
        
        // Initialize owner tracking with simple arrays
        _ownerIds = new int[MAX_OWNERS];
        _ownerPages = new ulong[MAX_OWNERS];
        _ownerCount = 0;
    }

    private static void ReserveAddressRange(IntPtr arenaStartPointer,
                                            ulong reservedAddress,
                                            ulong reservedSize) {
        if (reservedAddress == 0 || reservedSize == 0) return;

        ulong reservedEnd = reservedAddress + reservedSize;
        if (reservedEnd < reservedAddress) return;

        ulong arenaStart = (ulong)arenaStartPointer;
        ulong arenaEnd = arenaStart + MemorySize;
        if (arenaEnd < arenaStart) return;

        ulong overlapStart = reservedAddress > arenaStart ? reservedAddress : arenaStart;
        ulong overlapEnd = reservedEnd < arenaEnd ? reservedEnd : arenaEnd;
        if (overlapStart >= overlapEnd) return;

        ulong firstPage = (overlapStart - arenaStart) / PageSize;
        ulong endOffset = overlapEnd - arenaStart;
        ulong endPage = endOffset / PageSize;
        if ((endOffset % PageSize) != 0) endPage++;
        if (endPage > (ulong)NumPages) endPage = (ulong)NumPages;

        for (ulong page = firstPage; page < endPage; page++)
            _Info.Pages[page] = PageSignature;
    }
    /// <summary>
    /// Memory In Use
    /// </summary>
    public static ulong MemoryInUse => _Info.PageInUse * PageSize;
    /// <summary>
    /// Memory Size
    /// </summary>
    public static ulong MemorySize => (ulong)NumPages * PageSize;
    /// <summary>
    /// Get Page Index Start
    /// </summary>
    /// <param name="ptr"></param>
    /// <returns></returns>
    private static long GetPageIndexStart(IntPtr ptr) {
        ulong p = (ulong)ptr; if (p < (ulong)_Info.Start) return -1; p -= (ulong)_Info.Start; if ((p % PageSize) != 0) return -1; return (long)(p / PageSize);
    }
    /// <summary>
    /// Zero Fill
    /// </summary>
    /// <param name="data"></param>
    /// <param name="size"></param>
    internal static unsafe void ZeroFill(IntPtr data, ulong size) { Native.Stosb((void*)data, 0, size); }
    /// <summary>
    /// Free Call Count
    /// </summary>
    private static ulong _freeCallCount = 0;
    /// <summary>
    /// Free Success Count
    /// </summary>
    private static ulong _freeSuccessCount = 0;
    /// <summary>
    /// Free Fail Invalid Ptr
    /// </summary>
    private static ulong _freeFailInvalidPtr = 0;
    /// <summary>
    /// Free Fail No Pages
    /// </summary>
    private static ulong _freeFailNoPages = 0;
    /// <summary>
    /// Free Fail Corrupt Run
    /// </summary>
    private static ulong _freeFailCorruptRun = 0;
#if UEFI_DIAGNOSTIC_RING3_PHASE32 || UEFI_DIAGNOSTIC_APP_RUNTIME || UEFI_DIAGNOSTIC_STORAGE35Q
    private const int FreeInvalidLogCapacity = 16;
    private static IntPtr[] _freeInvalidLoggedPointers;
    private static string[] _freeInvalidLoggedCallers;
    private static int _freeInvalidLoggedCount;
    private static bool _freeInvalidLoggerUnavailableReported;
#endif
    /// <summary>
    /// Free Call Count
    /// </summary>
    public static ulong FreeCallCount => _freeCallCount;
    /// <summary>
    /// Free Success Count
    /// </summary>
    public static ulong FreeSuccessCount => _freeSuccessCount;
    /// <summary>
    /// Free Fail Invalid Ptr
    /// </summary>
    public static ulong FreeFailInvalidPtr => _freeFailInvalidPtr;
    /// <summary>
    /// Free Fail No Pages
    /// </summary>
    public static ulong FreeFailNoPages => _freeFailNoPages;
    /// <summary>
    /// Free Fail Corrupt Run
    /// </summary>
    public static ulong FreeFailCorruptRun => _freeFailCorruptRun;

    // Object.Dispose is shared by GC-managed references and a few objects that
    // deliberately occupy one allocator run. Only release the latter; arbitrary
    // object addresses, frozen strings and interiors of GC segments are not
    // allocator allocations and must not affect invalid-free telemetry.
    internal static ulong FreeManagedObjectIfAllocatorRun(IntPtr pointer) {
        lock (_sync) {
            long page = GetPageIndexStart(pointer);
            if (page < 0 || page >= NumPages) return 0;
            ulong pages = _Info.Pages[page];
            if (pages == 0 || pages == PageSignature) return 0;
            return Free(pointer, "Object.Dispose");
        }
    }

#if UEFI_DIAGNOSTIC_RING3_PHASE32 || UEFI_DIAGNOSTIC_APP_RUNTIME || UEFI_DIAGNOSTIC_STORAGE35Q
    // Called after NativeAOT GC statics have been initialized. Allocator.Initialize
    // runs earlier during UEFI startup, so managed reference fields assigned there
    // would be cleared when InitializeModules prepares the GC static bases.
    internal static void InitializeInvalidFreeDiagnostics() {
        _freeInvalidLoggedPointers = new IntPtr[FreeInvalidLogCapacity];
        _freeInvalidLoggedCallers = new string[FreeInvalidLogCapacity];
        _freeInvalidLoggedCount = 0;
        _freeInvalidLoggerUnavailableReported = false;
        SerialWriteFreeInvalidText("ALLOC_INVALID_LOG_READY;capacity=16\n");
    }
#endif

    /// <summary>
    /// Free
    /// </summary>
    /// <param name="intPtr"></param>
    /// <returns></returns>
    internal static ulong Free(IntPtr intPtr) => Free(intPtr, "unknown");

    internal static ulong Free(IntPtr intPtr, string caller) =>
        Free(intPtr, caller, 0);

    internal static ulong Free(IntPtr intPtr, string caller,
                               ulong callerAddress) {
        lock (_sync) {
            _freeCallCount++; // Track every call
            
            long p = GetPageIndexStart(intPtr); 
            
            if (p < 0 || p >= NumPages) { // guard invalid start index
                _freeFailInvalidPtr++;
#if UEFI_DIAGNOSTIC_RING3_PHASE32 || UEFI_DIAGNOSTIC_APP_RUNTIME || UEFI_DIAGNOSTIC_STORAGE35Q
                LogInvalidFreeOnce(intPtr, caller, callerAddress);
#endif
                return 0;
            }
            
            ulong pages = _Info.Pages[p];
            
            if (pages != 0 && pages != PageSignature) {
                // Corruption guard: run length must fit inside array
                if (pages > (ulong)NumPages - (ulong)p) {
                    // Do not attempt to free; mark as corrupt
                    _freeFailCorruptRun++;
                    return 0;
                }
                // Tag accounting
                byte tag = _Info.Tags[p]; 
                if (tag < (byte)AllocTag.Count) _Info.TagLivePages[tag] -= pages; 
                _Info.Tags[p] = 0;
                
                // Owner accounting (do BEFORE clearing pages)
                int owner = _Info.Owners[p];
                
                if (owner != 0) {
                    // Find owner in array and decrement - with safety bounds check
                    bool found = false;
                    for (int i = 0; i < _ownerCount && i < MAX_OWNERS; i++) {
                        if (_ownerIds[i] == owner) {
                            ulong live = _ownerPages[i];
                            _ownerPages[i] = live > pages ? live - pages : 0UL;
                            found = true;
                            break;
                        }
                    }
                    // If owner not found in tracking array, it's okay - just continue with free
                }
                _Info.Owners[p] = 0;
                
                // Global usage
                _Info.PageInUse -= pages;
                
                Native.Stosb((void*)intPtr, 0, pages * PageSize);
                for (ulong i = 0; i < pages; i++) {
                    ulong idx = (ulong)p + i;
                    if (idx >= (ulong)NumPages) break; // extra safety
                    _Info.Pages[idx] = 0;
                }
                
                _freeSuccessCount++; // Track successful frees
                return pages * PageSize;
            }
            
            _freeFailNoPages++;
            return 0;
        }
    }
#if UEFI_DIAGNOSTIC_RING3_PHASE32 || UEFI_DIAGNOSTIC_APP_RUNTIME || UEFI_DIAGNOSTIC_STORAGE35Q
    private static void LogInvalidFreeOnce(IntPtr pointer, string caller,
                                          ulong callerAddress) {
        if (_freeInvalidLoggedPointers == null ||
                _freeInvalidLoggedCallers == null) {
            if (!_freeInvalidLoggerUnavailableReported) {
                _freeInvalidLoggerUnavailableReported = true;
                SerialWriteFreeInvalidText("ALLOC_INVALID_LOG_UNAVAILABLE\n");
            }
            return;
        }
        for (int i = 0; i < _freeInvalidLoggedCount; i++) {
            if (_freeInvalidLoggedPointers[i] == pointer &&
                    _freeInvalidLoggedCallers[i] == caller) return;
        }
        if (_freeInvalidLoggedCount >= FreeInvalidLogCapacity) return;
        int loggedIndex = _freeInvalidLoggedCount++;
        _freeInvalidLoggedPointers[loggedIndex] = pointer;
        _freeInvalidLoggedCallers[loggedIndex] = caller;

        ulong address = (ulong)pointer;
        ulong start = (ulong)_Info.Start;
        ulong offset = address >= start ? address - start : 0;
        string reason = address == 0 ? "null" :
            address < start ? "below-arena" :
            (offset % PageSize) != 0 ? "unaligned" :
            (offset / PageSize) >= (ulong)NumPages ? "past-arena" :
            "invalid-index";

        SerialWriteFreeInvalidText("ALLOC_FREE_INVALID;ptr=0x");
        SerialWriteFreeInvalidHex(address);
        SerialWriteFreeInvalidText(";reason=");
        SerialWriteFreeInvalidText(reason);
        SerialWriteFreeInvalidText(";caller=");
        SerialWriteFreeInvalidText(caller);
        SerialWriteFreeInvalidText(";callerIp=0x");
        SerialWriteFreeInvalidHex(callerAddress);
        SerialWriteFreeInvalidText(";currentOwner=0x");
        SerialWriteFreeInvalidHex(unchecked((ulong)CurrentOwnerId));
        SerialWriteFreeInvalidText(";arena=0x");
        SerialWriteFreeInvalidHex(start);
        SerialWriteFreeInvalidText(";offset=0x");
        SerialWriteFreeInvalidHex(offset);
        if (address >= start && (offset / PageSize) < (ulong)NumPages) {
            int pageIndex = (int)(offset / PageSize);
            SerialWriteFreeInvalidText(";pageState=0x");
            SerialWriteFreeInvalidHex(_Info.Pages[pageIndex]);
            SerialWriteFreeInvalidText(";allocTag=0x");
            SerialWriteFreeInvalidHex(_Info.Tags[pageIndex]);
            SerialWriteFreeInvalidText(";allocOwner=0x");
            SerialWriteFreeInvalidHex(unchecked((ulong)_Info.Owners[pageIndex]));
        }
        Native.Out8(0x3F8, (byte)'\n');
    }

    private static void SerialWriteFreeInvalidText(string text) {
        if (text == null) return;
        for (int i = 0; i < text.Length; i++)
            Native.Out8(0x3F8, (byte)text[i]);
    }

    private static void SerialWriteFreeInvalidHex(ulong value) {
        for (int shift = 60; shift >= 0; shift -= 4) {
            int nibble = (int)((value >> shift) & 0xFUL);
            byte c = (byte)(nibble < 10 ? ('0' + nibble) :
                ('A' + (nibble - 10)));
            Native.Out8(0x3F8, c);
        }
    }
#endif
    /// <summary>
    /// Allocate
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    internal static unsafe IntPtr Allocate(ulong size) => Allocate(size, AllocTag.Unknown);
    /// <summary>
    /// Suspicious Size
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    private static bool SuspiciousSize(ulong size) {
        return size > MemorySize && size > (MemorySize * 4); // Treat sizes far beyond physical memory as suspicious overflow/corruption.
    }
    /// <summary>
    /// Allocate
    /// </summary>
    /// <param name="size"></param>
    /// <param name="tag"></param>
    /// <returns></returns>
    internal static unsafe IntPtr Allocate(ulong size, AllocTag tag) {
        lock (_sync) {
            if (size == 0) size = 1;
            // Overflow / corruption guard: reject absurd sizes silently (return null) instead of panicking
            if (SuspiciousSize(size)) return IntPtr.Zero;
            if (size > MemorySize) { Panic.Error("Memory request too large: size=" + size.ToString() + ", total=" + MemorySize.ToString()); return IntPtr.Zero; }
            ulong pages = size > PageSize ? (size / PageSize) + ((size % PageSize) != 0 ? 1UL : 0) : 1UL;
            ulong i; bool found = false;
            for (i = 0; i < (ulong)NumPages; i++) {
                if (_Info.Pages[i] == 0) {
                    found = true;
                    for (ulong k = 0; k < pages; k++) {
                        ulong idx = i + k;
                        if (idx >= (ulong)NumPages || _Info.Pages[idx] != 0) { found = false; break; }
                    }
                    if (found) break;
                } else if (_Info.Pages[i] != PageSignature) {
                    ulong runPages = _Info.Pages[i]; if (runPages == 0 || runPages == PageSignature) continue; i += runPages - 1;
                }
            }
            if (!found) { Panic.Error("Out of memory: no free pages (in use=" + MemoryInUse.ToString() + "/" + MemorySize.ToString() + ", req=" + (pages * PageSize).ToString() + ")"); return IntPtr.Zero; }
            // Guard: ensure run fits
            if (pages > (ulong)NumPages - i) return IntPtr.Zero;
            for (ulong k = 0; k < pages; k++) _Info.Pages[i + k] = PageSignature;
            _Info.Pages[i] = pages; _Info.PageInUse += pages;
            byte t = (byte)tag; if (t >= (byte)AllocTag.Count) t = (byte)AllocTag.Unknown; _Info.Tags[i] = t; _Info.TagLivePages[t] += pages;
            
            // Owner accounting with simple array lookup
            int owner = CurrentOwnerId; 
            _Info.Owners[i] = owner;
            if (owner != 0) {
                int ownerIdx = FindOrAddOwner(owner);
                if (ownerIdx >= 0 && ownerIdx < MAX_OWNERS) {
                    _ownerPages[ownerIdx] += pages;
                }
                // If ownerIdx is -1, we couldn't track this owner (table full)
                // This is okay - allocation proceeds, just won't be in owner tracking
            }
            
            long baseAddr = (long)_Info.Start; long offset = (long)(i * PageSize); return new IntPtr((void*)(baseAddr + offset));
        }
    }
    /// <summary>
    /// Reallocate (camel-case) is expected by other code (stdlib, API)
    /// </summary>
    /// <param name="intPtr"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    public static IntPtr Reallocate(IntPtr intPtr, ulong size) {
        if (intPtr == IntPtr.Zero) return Allocate(size);
        if (size == 0) { Free(intPtr, "Allocator.Reallocate"); return IntPtr.Zero; }
        long p = GetPageIndexStart(intPtr); if (p == -1) return intPtr;
        ulong pages = size > PageSize ? (size / PageSize) + ((size % PageSize) != 0 ? 1UL : 0) : 1UL;
        if (_Info.Pages[p] == pages) return intPtr;
        byte tag = _Info.Tags[p]; IntPtr newptr = Allocate(size, (AllocTag)tag);
        if (newptr == IntPtr.Zero) return intPtr; // allocation failed; keep old block
        ulong oldBytes = _Info.Pages[p] * PageSize; ulong copyLen = size < oldBytes ? size : oldBytes;
        MemoryCopy(newptr, intPtr, copyLen); Free(intPtr, "Allocator.Reallocate"); return newptr;
    }
#pragma warning disable CS8500
    /// <summary>
    /// Clear Allocate
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="num"></param>
    /// <returns></returns>
    public static T* ClearAllocate<T>(int num) where T : struct { 
        return (T*)ClearAllocate(num, sizeof(T)); 
    }
#pragma warning restore CS8500
    /// <summary>
    /// Clear Allocate
    /// </summary>
    /// <param name="num"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    public static IntPtr ClearAllocate(int num, int size) {
        if (num < 0 || size < 0) return IntPtr.Zero;
        ulong unum = (ulong)num; ulong usize = (ulong)size;
        if (unum != 0 && usize > MemorySize / unum) return IntPtr.Zero; // multiplication overflow/too large
        ulong total = unum * usize; IntPtr ptr = Allocate(total); if (ptr == IntPtr.Zero) return IntPtr.Zero; ZeroFill(ptr, total); return ptr;
    }
    /// <summary>
    /// Memory Copy
    /// </summary>
    /// <param name="dst"></param>
    /// <param name="src"></param>
    /// <param name="size"></param>
    internal static unsafe void MemoryCopy(IntPtr dst, IntPtr src, ulong size) { Native.Movsb((void*)dst, (void*)src, size); }
    
    public static ulong GetTagBytes(AllocTag tag) { return _Info.TagLivePages[(int)tag] * PageSize; }
    
    public static ulong GetOwnerBytes(int ownerId) {
        if (ownerId == 0) return 0UL;
        lock (_sync) {
            // Direct array lookup - much simpler than Dictionary
            // Add bounds checking to prevent array access violations
            for (int i = 0; i < _ownerCount && i < MAX_OWNERS; i++) {
                if (_ownerIds[i] == ownerId) {
                    return _ownerPages[i] * PageSize;
                }
            }
            
            // Fallback: scan page run starts and accumulate pages owned by ownerId
            ulong pages = 0UL;
            for (int i = 0; i < NumPages; i++) {
                ulong run = _Info.Pages[i];
                if (run != 0 && run != PageSignature) {
                    // Guard corrupt run length
                    if (run > (ulong)NumPages - (ulong)i) break;
                    // This is a run start - check if owned by ownerId
                    if (_Info.Owners[i] == ownerId) pages += run;
                    // skip ahead by run-1 (loop will increment i++)
                    i += (int)(run - 1);
                }
            }
            return pages * PageSize;
        }
    }
    /// <summary>
    /// Snapshot structure for owner accounting (avoids depending on generic KeyValuePair in low-level kernel code)
    /// </summary>
    public struct OwnerSnapshot { public int OwnerId; public ulong Bytes; }
    /// <summary>
    /// Return a snapshot of current owner assignments and bytes. Used by diagnostic UI to find leaks.
    /// </summary>
    /// <returns></returns>
    public static OwnerSnapshot[] GetOwnerListSnapshot() {
        lock (_sync) {
            // Ensure we don't exceed bounds
            int count = _ownerCount < MAX_OWNERS ? _ownerCount : MAX_OWNERS;
            var arr = new OwnerSnapshot[count];
            for (int i = 0; i < count; i++) {
                arr[i].OwnerId = _ownerIds[i];
                arr[i].Bytes = _ownerPages[i] * PageSize;
            }
            return arr;
        }
    }
}
