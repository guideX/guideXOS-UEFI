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
#if UEFI_DIAGNOSTIC_RING3_PHASE35
    internal static ulong CurrentOwnerGeneration;
    internal static byte CurrentAllocationLabel;
    internal static ulong CurrentDiagnosticRequest;
    internal static ulong CurrentDiagnosticLifetime;
    internal static ulong LastDiagnosticRequest;
    internal static bool DiagnosticProvenanceEnabled = true;
    // Phase 35R6 uses a pre-sized managed record array containing only scalar
    // values. Recording never constructs strings; labels are fixed literals.
    internal static uint CurrentDiagnosticStringSite;
    private const int StringLedgerCapacity = 4096;
    private struct StringLedgerRecord {
        internal ulong Address, Run, RequestedBytes, Request, AllocatorSequence;
        internal ulong CreationReturn, DisposeCount, DisposeSite, ContentHash;
        internal ulong FreeAttemptCount, FreeResult;
        internal uint Length, CreationSite;
        internal bool Live, GuardAccepted, ExactRunStart, HasContentHash;
    }
    private static StringLedgerRecord[] _stringLedger;
    private static int _stringLedgerCount;
    private static ulong _stringLedgerDropped;
    internal enum DiagnosticAllocationSite : byte {
        Unclassified = 0,
        PageTableWalk = 1,
        ManagedVmCommit = 2,
        KernelApiAllocate = 3,
        KernelApiReadAllBytes = 4,
        KernelApiReallocate = 5,
        NativeRuntimeMalloc = 6,
        NativeRuntimeRealloc = 7,
        NativeRuntimeCalloc = 8,
        NativeRuntimeKmalloc = 9,
        NativeRuntimeKcalloc = 10
    }
    private const int DiagnosticRunCapacity = 16384;
    private static ulong _allocationSequence;
    private static ulong _diagnosticSnapshotSequence;
    private static ulong _freeSequence;
    private static ulong _diagnosticRunRecordsDropped;
    private static ulong _diagnosticLifetimeSequence;
#endif
    
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
#if UEFI_DIAGNOSTIC_RING3_PHASE35
        public fixed ulong DiagnosticRunIds[16384];
        public fixed ulong DiagnosticRunAddresses[16384];
        public fixed ulong DiagnosticRunPages[16384];
        public fixed ulong DiagnosticRunOwnerGenerations[16384];
        public fixed ulong DiagnosticRunCallerHashes[16384];
        public fixed ulong DiagnosticRunFreeSequences[16384];
        public fixed int DiagnosticRunOwnerIds[16384];
        public fixed int DiagnosticRunCallerLines[16384];
        public fixed byte DiagnosticRunTags[16384];
        public fixed byte DiagnosticRunCallerLabels[16384];
        public fixed byte DiagnosticRunAllocationSites[16384];
        public fixed ulong DiagnosticRunVirtualAddresses[16384];
        public fixed ulong DiagnosticRunCr3[16384];
        public fixed ulong DiagnosticRunRequestedSizes[16384];
        public fixed ulong DiagnosticRunCallerAddresses[16384];
        public fixed ulong DiagnosticRunRequesterIds[16384];
        public fixed ulong DiagnosticRunLifetimeIds[16384];
        public fixed uint DiagnosticRunCreationSites[16384];
        public fixed byte DiagnosticRunManagedHelpers[16384];
        public fixed ulong DiagnosticRunEETypeAddresses[16384];
        public fixed uint DiagnosticPageRunSlots[NumPages];
        public fixed ulong DiagnosticPageOwnerGenerations[NumPages];
        public fixed ulong DiagnosticPageAllocationSequences[NumPages];
        public fixed byte DiagnosticPageCallerLabels[NumPages];
        public fixed byte DiagnosticBaselineOccupiedPages[NumPages];
        public fixed ulong DiagnosticBaselineRunIds[NumPages];
        public fixed uint DiagnosticBaselineRunPages[NumPages];
#endif
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
    /// Initialize the allocator and reserve the physical pages that back the
    /// kernel image. The allocator returns identity addresses, while the
    /// bootloader may map the image at a different linked virtual address.
    /// </summary>
    public static void Initialize(IntPtr start, ulong reservedPhysicalAddress,
                                  ulong reservedSize) {
        fixed (Info* pInfo = &_Info) Native.Stosb(pInfo, 0, (ulong)sizeof(Info));
        _Info.Start = start; 
        _Info.PageInUse = 0;

        ReserveAddressRange(start, reservedPhysicalAddress, reservedSize);
        
        // Initialize owner tracking with simple arrays
        _ownerIds = new int[MAX_OWNERS];
        _ownerPages = new ulong[MAX_OWNERS];
        _ownerCount = 0;
    }

    internal static void ReserveAddressRange(IntPtr arenaStartPointer,
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
#if UEFI_DIAGNOSTIC_RING3_PHASE32 || UEFI_DIAGNOSTIC_RING3_PHASE35 || UEFI_DIAGNOSTIC_APP_RUNTIME || UEFI_DIAGNOSTIC_STORAGE35Q
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
            if (page < 0 || page >= NumPages) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
                RecordStringDisposeGuard(pointer, false, 1);
#endif
                return 0;
            }
            ulong pages = _Info.Pages[page];
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            bool accepted = pages != 0 && pages != PageSignature;
            RecordStringDisposeGuard(pointer, accepted,
                pages == PageSignature ? 3UL : (pages == 0 ? 2UL :
                (accepted ? 0UL : 4UL)));
            if (!accepted) return 0;
            ulong result = Free(pointer, "Object.Dispose");
            RecordStringFreeResult(pointer, result);
            return result;
#else
            if (pages == 0 || pages == PageSignature) return 0;
            return Free(pointer, "Object.Dispose");
#endif
        }
    }

#if UEFI_DIAGNOSTIC_RING3_PHASE32 || UEFI_DIAGNOSTIC_RING3_PHASE35 || UEFI_DIAGNOSTIC_APP_RUNTIME || UEFI_DIAGNOSTIC_STORAGE35Q
    // Called after NativeAOT GC statics have been initialized. Allocator.Initialize
    // runs earlier during UEFI startup, so managed reference fields assigned there
    // would be cleared when InitializeModules prepares the GC static bases.
    internal static void InitializeInvalidFreeDiagnostics() {
        _freeInvalidLoggedPointers = new IntPtr[FreeInvalidLogCapacity];
        _freeInvalidLoggedCallers = new string[FreeInvalidLogCapacity];
        _freeInvalidLoggedCount = 0;
        _freeInvalidLoggerUnavailableReported = false;
        SerialWriteFreeInvalidText("ALLOC_INVALID_LOG_READY;capacity=16\n");
#if UEFI_DIAGNOSTIC_RING3_PHASE35
        _stringLedger = new StringLedgerRecord[StringLedgerCapacity];
        _stringLedgerCount = 0;
        _stringLedgerDropped = 0;
        CurrentDiagnosticStringSite = 0;
        SerialWriteFreeInvalidText("STRING_LEDGER_READY=1\n");
        SerialWriteFreeInvalidText("STRING_LEDGER_VERSION=R6-1\n");
        SerialWriteFreeInvalidText("ALLOC_PROVENANCE_READY=1;capacity=262144-live-runs;storage=static-per-page\n");
#endif
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
#if UEFI_DIAGNOSTIC_RING3_PHASE32 || UEFI_DIAGNOSTIC_RING3_PHASE35 || UEFI_DIAGNOSTIC_APP_RUNTIME || UEFI_DIAGNOSTIC_STORAGE35Q
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
#if UEFI_DIAGNOSTIC_RING3_PHASE35
                if (DiagnosticProvenanceEnabled)
                    RecordDiagnosticFree((int)p);
#endif
                
                if (owner > 0) {
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
#if UEFI_DIAGNOSTIC_RING3_PHASE35
                    _Info.DiagnosticPageRunSlots[idx] = 0;
                    _Info.DiagnosticPageOwnerGenerations[idx] = 0;
                    _Info.DiagnosticPageAllocationSequences[idx] = 0;
                    _Info.DiagnosticPageCallerLabels[idx] = 0;
#endif
                }
                
                _freeSuccessCount++; // Track successful frees
                return pages * PageSize;
            }
            
            _freeFailNoPages++;
            return 0;
        }
    }
#if UEFI_DIAGNOSTIC_RING3_PHASE32 || UEFI_DIAGNOSTIC_RING3_PHASE35 || UEFI_DIAGNOSTIC_APP_RUNTIME || UEFI_DIAGNOSTIC_STORAGE35Q
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

#if UEFI_DIAGNOSTIC_RING3_PHASE35
    // Called by the kernel CoreLib RhpNewArray override after it has initialized
    // the object header. This records only bounded scalar metadata and emits
    // fixed text; it does not ask the runtime to format a managed type name.
    internal static void RecordRhpNewArrayAllocation(IntPtr objectAddress,
            ulong eeType, ulong componentType, uint baseSize,
            ushort componentSize, int length, ushort arrayType,
            ushort componentElementType, bool componentIsObject,
            bool isString, byte rank, ulong objectBytes) {
        if (!DiagnosticProvenanceEnabled || objectAddress == IntPtr.Zero)
            return;

        ulong address = (ulong)objectAddress;
        lock (_sync) {
            if (address < (ulong)_Info.Start || address >=
                    (ulong)_Info.Start + MemorySize)
                return;
            ulong page = (address - (ulong)_Info.Start) / PageSize;
            uint encodedSlot = _Info.DiagnosticPageRunSlots[page];
            if (encodedSlot == 0) return;
            int slot = (int)encodedSlot - 1;
            ulong id = _Info.DiagnosticRunIds[slot];
            if (id == 0 || _Info.DiagnosticRunAddresses[slot] != address)
                return;
            _Info.DiagnosticRunCreationSites[slot] =
                CurrentDiagnosticStringSite;
            _Info.DiagnosticRunManagedHelpers[slot] = 1;
            _Info.DiagnosticRunEETypeAddresses[slot] = eeType;

            uint stringSite = CurrentDiagnosticStringSite;
            if (isString && stringSite == 0) stringSite = 402;
            if (isString) {
                _Info.DiagnosticRunCreationSites[slot] = stringSite;
            }
            if (isString && CurrentDiagnosticRequest != 0)
                RecordStringCreationNoLock(address, objectBytes,
                    unchecked((uint)length), CurrentDiagnosticRequest,
                    stringSite, id,
                    _Info.DiagnosticRunCallerAddresses[slot]);

        }
    }

    internal static void RecordRhpNewFastAllocation(IntPtr objectAddress,
            ulong eeTypeAddress) {
        if (!DiagnosticProvenanceEnabled || objectAddress == IntPtr.Zero)
            return;
        ulong address = (ulong)objectAddress;
        lock (_sync) {
            if (address < (ulong)_Info.Start || address >=
                    (ulong)_Info.Start + MemorySize) return;
            ulong page = (address - (ulong)_Info.Start) / PageSize;
            uint encodedSlot = _Info.DiagnosticPageRunSlots[page];
            if (encodedSlot == 0) return;
            int slot = (int)encodedSlot - 1;
            if (_Info.DiagnosticRunIds[slot] == 0 ||
                    _Info.DiagnosticRunAddresses[slot] != address) return;
            _Info.DiagnosticRunManagedHelpers[slot] = 2;
            _Info.DiagnosticRunCreationSites[slot] =
                CurrentDiagnosticStringSite;
            _Info.DiagnosticRunEETypeAddresses[slot] = eeTypeAddress;
        }
    }

    private static void RecordStringCreationNoLock(ulong address,
            ulong bytes, uint length, ulong request, uint site, ulong runId,
            ulong caller) {
        if (_stringLedger == null) return;
        for (int i = 0; i < _stringLedgerCount; i++) {
            if (_stringLedger[i].Address == address)
                _stringLedger[i].Live = false;
        }
        int index = -1;
        for (int i = 0; i < _stringLedgerCount; i++) {
            if (_stringLedger[i].Address == 0) {
                index = i;
                break;
            }
        }
        if (index < 0) {
            if (_stringLedgerCount >= StringLedgerCapacity) {
                _stringLedgerDropped++;
                return;
            }
            index = _stringLedgerCount++;
        }
        _stringLedger[index] = new StringLedgerRecord {
            Address = address, Run = address, RequestedBytes = bytes,
            Length = length, Request = request, CreationSite = site,
            CreationReturn = caller, AllocatorSequence = runId, Live = true
        };
        SerialWriteFreeInvalidText("STRING_CREATE;request=0x");
        SerialWriteFreeInvalidHex(request);
        SerialWriteFreeInvalidText(";address=0x");
        SerialWriteFreeInvalidHex(address);
        SerialWriteFreeInvalidText(";run=0x");
        SerialWriteFreeInvalidHex(address);
        SerialWriteFreeInvalidText(";bytes=0x");
        SerialWriteFreeInvalidHex(bytes);
        SerialWriteFreeInvalidText(";length=0x");
        SerialWriteFreeInvalidHex(length);
        SerialWriteFreeInvalidText(";site=0x");
        SerialWriteFreeInvalidHex(site);
        SerialWriteFreeInvalidText(";allocSeq=0x");
        SerialWriteFreeInvalidHex(runId);
        SerialWriteFreeInvalidText(";return=0x");
        SerialWriteFreeInvalidHex(caller);
        Native.Out8(0x3F8, (byte)'\n');
    }

    internal static void RecordManagedObjectDispose(IntPtr pointer) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
        lock (_sync) {
            if (_stringLedger == null) return;
            for (int i = _stringLedgerCount - 1; i >= 0; i--) {
                ref StringLedgerRecord record = ref _stringLedger[i];
                if (record.Address != (ulong)pointer) continue;
                record.DisposeCount++;
                record.DisposeSite = CurrentDiagnosticStringSite;
                SerialWriteFreeInvalidText("STRING_DISPOSE;request=0x");
                SerialWriteFreeInvalidHex(record.Request);
                SerialWriteFreeInvalidText(";address=0x");
                SerialWriteFreeInvalidHex(record.Address);
                SerialWriteFreeInvalidText(";allocSeq=0x");
                SerialWriteFreeInvalidHex(record.AllocatorSequence);
                SerialWriteFreeInvalidText(";site=0x");
                SerialWriteFreeInvalidHex(record.DisposeSite);
                SerialWriteFreeInvalidText(";count=0x");
                SerialWriteFreeInvalidHex(record.DisposeCount);
                Native.Out8(0x3F8, (byte)'\n');
                return;
            }
        }
#endif
    }

    private static void RecordStringDisposeGuard(IntPtr pointer,
            bool accepted, ulong reason) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
        if (_stringLedger == null) return;
        for (int i = _stringLedgerCount - 1; i >= 0; i--) {
            ref StringLedgerRecord record = ref _stringLedger[i];
            if (record.Address != (ulong)pointer) continue;
            record.GuardAccepted = accepted;
            long page = GetPageIndexStart(pointer);
            uint slot = page >= 0 && page < NumPages ?
                _Info.DiagnosticPageRunSlots[page] : 0;
            record.ExactRunStart = slot != 0 &&
                _Info.DiagnosticRunAddresses[slot - 1] == record.Address;
            SerialWriteFreeInvalidText("STRING_FREE_GUARD;request=0x");
            SerialWriteFreeInvalidHex(record.Request);
            SerialWriteFreeInvalidText(";address=0x");
            SerialWriteFreeInvalidHex(record.Address);
            SerialWriteFreeInvalidText(";allocSeq=0x");
            SerialWriteFreeInvalidHex(record.AllocatorSequence);
            SerialWriteFreeInvalidText(";run=0x");
            SerialWriteFreeInvalidHex(record.Run);
            SerialWriteFreeInvalidText(";accepted=0x");
            SerialWriteFreeInvalidHex(accepted ? 1UL : 0UL);
            SerialWriteFreeInvalidText(";exactRun=0x");
            SerialWriteFreeInvalidHex(record.ExactRunStart ? 1UL : 0UL);
            SerialWriteFreeInvalidText(";reason=0x");
            SerialWriteFreeInvalidHex(reason);
            Native.Out8(0x3F8, (byte)'\n');
            return;
        }
#endif
    }

    private static void RecordStringFreeResult(IntPtr pointer, ulong result) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
        if (_stringLedger == null) return;
        for (int i = _stringLedgerCount - 1; i >= 0; i--) {
            ref StringLedgerRecord record = ref _stringLedger[i];
            if (record.Address != (ulong)pointer) continue;
            record.FreeAttemptCount++;
            record.FreeResult = result;
            record.Live = result == 0;
            SerialWriteFreeInvalidText("STRING_FREE;request=0x");
            SerialWriteFreeInvalidHex(record.Request);
            SerialWriteFreeInvalidText(";address=0x");
            SerialWriteFreeInvalidHex(record.Address);
            SerialWriteFreeInvalidText(";allocSeq=0x");
            SerialWriteFreeInvalidHex(record.AllocatorSequence);
            SerialWriteFreeInvalidText(";run=0x");
            SerialWriteFreeInvalidHex(record.Run);
            SerialWriteFreeInvalidText(";result=0x");
            SerialWriteFreeInvalidHex(result);
            Native.Out8(0x3F8, (byte)'\n');
            // Keep completed records until their slot is reused. The request
            // snapshot runs after cleanup and still needs the old address.
            return;
        }
#endif
    }

    internal static void RecordStringContent(IntPtr pointer, char* content,
            int length) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
        if (_stringLedger == null || content == null || length < 0) return;
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < length; i++) {
            hash ^= content[i];
            hash *= 1099511628211UL;
        }
        for (int i = _stringLedgerCount - 1; i >= 0; i--) {
            ref StringLedgerRecord record = ref _stringLedger[i];
            if (record.Address != (ulong)pointer || !record.Live) continue;
            record.ContentHash = hash;
            record.HasContentHash = true;
            SerialWriteFreeInvalidText("STRING_VALUE;request=0x");
            SerialWriteFreeInvalidHex(record.Request);
            SerialWriteFreeInvalidText(";address=0x");
            SerialWriteFreeInvalidHex(record.Address);
            SerialWriteFreeInvalidText(";hash=0x");
            SerialWriteFreeInvalidHex(hash);
            Native.Out8(0x3F8, (byte)'\n');
            return;
        }
#endif
    }

    internal static void DumpStringLedger(ulong request, ulong stage) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
        if (_stringLedger == null) return;
        lock (_sync) {
            ulong live = 0;
            for (int i = 0; i < _stringLedgerCount; i++) {
                ref StringLedgerRecord record = ref _stringLedger[i];
                if (record.Address == 0) continue;
                if (record.Request != request) {
                    if (stage == 3 && !record.Live) record.Address = 0;
                    continue;
                }
                long page = GetPageIndexStart((IntPtr)record.Address);
                uint slot = page >= 0 && page < NumPages ?
                    _Info.DiagnosticPageRunSlots[page] : 0;
                bool isLive = slot != 0 && _Info.Pages[page] != 0 &&
                    _Info.Pages[page] != PageSignature &&
                    _Info.DiagnosticRunAddresses[slot - 1] == record.Address &&
                    _Info.DiagnosticRunIds[slot - 1] ==
                        record.AllocatorSequence;
                for (int later = i + 1; later < _stringLedgerCount; later++) {
                    if (_stringLedger[later].Address == record.Address) {
                        isLive = false;
                        break;
                    }
                }
                record.Live = isLive;
                if (!isLive) {
                    // Keep the record through both request and post-process
                    // snapshots. Once the post-process snapshot has been
                    // emitted, a completed record can safely release its
                    // bounded ledger slot for later requester lifetimes.
                    if (stage == 3) record.Address = 0;
                    continue;
                }
                live++;
                SerialWriteFreeInvalidText("STRING_LIVE;request=0x");
                SerialWriteFreeInvalidHex(request);
                SerialWriteFreeInvalidText(";stage=0x");
                SerialWriteFreeInvalidHex(stage);
                SerialWriteFreeInvalidText(";address=0x");
                SerialWriteFreeInvalidHex(record.Address);
                SerialWriteFreeInvalidText(";run=0x");
                SerialWriteFreeInvalidHex(record.Run);
                SerialWriteFreeInvalidText(";bytes=0x");
                SerialWriteFreeInvalidHex(record.RequestedBytes);
                SerialWriteFreeInvalidText(";length=0x");
                SerialWriteFreeInvalidHex(record.Length);
                SerialWriteFreeInvalidText(";site=0x");
                SerialWriteFreeInvalidHex(record.CreationSite);
                SerialWriteFreeInvalidText(";allocSeq=0x");
                SerialWriteFreeInvalidHex(record.AllocatorSequence);
                SerialWriteFreeInvalidText(";hash=0x");
                SerialWriteFreeInvalidHex(record.ContentHash);
                SerialWriteFreeInvalidText(";dispose=0x");
                SerialWriteFreeInvalidHex(record.DisposeCount);
                SerialWriteFreeInvalidText(";freeAttempts=0x");
                SerialWriteFreeInvalidHex(record.FreeAttemptCount);
                SerialWriteFreeInvalidText(";freeResult=0x");
                SerialWriteFreeInvalidHex(record.FreeResult);
                Native.Out8(0x3F8, (byte)'\n');
            }
            SerialWriteFreeInvalidText("STRING_LIVE_COUNT;request=0x");
            SerialWriteFreeInvalidHex(request);
            SerialWriteFreeInvalidText(";stage=0x");
            SerialWriteFreeInvalidHex(stage);
            SerialWriteFreeInvalidText(";count=0x");
            SerialWriteFreeInvalidHex(live);
            SerialWriteFreeInvalidText(";dropped=0x");
            SerialWriteFreeInvalidHex(_stringLedgerDropped);
            Native.Out8(0x3F8, (byte)'\n');
        }
#endif
    }
#endif
#endif
    /// <summary>
    /// Allocate
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    internal static unsafe IntPtr Allocate(ulong size) =>
        Allocate(size, AllocTag.Unknown);
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
        return Allocate(size, tag,
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            DiagnosticAllocationSite.Unclassified,
#endif
            0, 0);
    }

#if UEFI_DIAGNOSTIC_RING3_PHASE35
    internal static unsafe IntPtr Allocate(ulong size,
            DiagnosticAllocationSite site, ulong virtualAddress = 0,
            ulong cr3 = 0, ulong callerAddress = 0) =>
        Allocate(size, AllocTag.Unknown, site, virtualAddress, cr3,
            callerAddress);

    internal static unsafe IntPtr Reallocate(IntPtr intPtr, ulong size,
            DiagnosticAllocationSite site) {
        if (intPtr == IntPtr.Zero) return Allocate(size, site);
        if (size == 0) {
            Free(intPtr, "Allocator.Reallocate");
            return IntPtr.Zero;
        }
        long page = GetPageIndexStart(intPtr);
        if (page == -1) return intPtr;
        ulong pages = size > PageSize ?
            (size / PageSize) + ((size % PageSize) != 0 ? 1UL : 0) : 1UL;
        if (_Info.Pages[page] == pages) return intPtr;
        byte tag = _Info.Tags[page];
        IntPtr replacement = Allocate(size, (AllocTag)tag, site, 0, 0);
        if (replacement == IntPtr.Zero) return intPtr;
        ulong oldBytes = _Info.Pages[page] * PageSize;
        ulong copyLength = size < oldBytes ? size : oldBytes;
        MemoryCopy(replacement, intPtr, copyLength);
        Free(intPtr, "Allocator.Reallocate");
        return replacement;
    }
#endif

    private static unsafe IntPtr Allocate(ulong size, AllocTag tag,
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            DiagnosticAllocationSite site,
#endif
            ulong virtualAddress, ulong cr3, ulong callerAddress = 0) {
        string callerFile = "";
        int callerLine = 0;
        ulong requestedSize = size;
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
            if (owner > 0) {
                int ownerIdx = FindOrAddOwner(owner);
                if (ownerIdx >= 0 && ownerIdx < MAX_OWNERS) {
                    _ownerPages[ownerIdx] += pages;
                }
                // If ownerIdx is -1, we couldn't track this owner (table full)
                // This is okay - allocation proceeds, just won't be in owner tracking
            }
            
            long baseAddr = (long)_Info.Start; long offset = (long)(i * PageSize);
            IntPtr allocation = new IntPtr((void*)(baseAddr + offset));
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            if (site == DiagnosticAllocationSite.NativeRuntimeMalloc ||
                    site == DiagnosticAllocationSite.NativeRuntimeRealloc ||
                    site == DiagnosticAllocationSite.NativeRuntimeCalloc ||
                    site == DiagnosticAllocationSite.NativeRuntimeKmalloc ||
                    site == DiagnosticAllocationSite.NativeRuntimeKcalloc ||
                    site == DiagnosticAllocationSite.KernelApiAllocate ||
                    site == DiagnosticAllocationSite.KernelApiReadAllBytes ||
                    site == DiagnosticAllocationSite.KernelApiReallocate) {
                virtualAddress = (ulong)allocation;
                cr3 = Native.ReadCR3() & ~0xFFFUL;
            }
            RecordDiagnosticAllocation((ulong)allocation, pages, t, owner,
                callerFile, callerLine, site, virtualAddress, cr3,
                requestedSize, callerAddress);
#endif
            return allocation;
        }
    }
#if UEFI_DIAGNOSTIC_RING3_PHASE35
    private static ulong CallerFileHash(string path) {
        ulong hash = 14695981039346656037UL;
        if (path == null) return hash;
        for (int i = 0; i < path.Length; i++) {
            hash ^= path[i];
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private static void RecordDiagnosticAllocation(ulong address,
            ulong pages, byte tag, int owner, string callerFile,
            int callerLine, DiagnosticAllocationSite site,
            ulong virtualAddress, ulong cr3, ulong requestedSize,
            ulong callerAddress) {
        if (!DiagnosticProvenanceEnabled) return;
        ulong id = ++_allocationSequence;
        int slot = (int)((id - 1) % DiagnosticRunCapacity);
        if (_Info.DiagnosticRunIds[slot] != 0 &&
                _Info.DiagnosticRunFreeSequences[slot] == 0)
            _diagnosticRunRecordsDropped++;
        _Info.DiagnosticRunIds[slot] = id;
        _Info.DiagnosticRunAddresses[slot] = address;
        _Info.DiagnosticRunPages[slot] = pages;
        _Info.DiagnosticRunOwnerGenerations[slot] =
            owner == 0 ? 0UL : CurrentOwnerGeneration;
        _Info.DiagnosticRunCallerHashes[slot] = CallerFileHash(callerFile);
        _Info.DiagnosticRunFreeSequences[slot] = 0;
        _Info.DiagnosticRunOwnerIds[slot] = owner;
        _Info.DiagnosticRunCallerLines[slot] = callerLine;
        _Info.DiagnosticRunTags[slot] = tag;
        _Info.DiagnosticRunCallerLabels[slot] = CurrentAllocationLabel;
        _Info.DiagnosticRunAllocationSites[slot] = (byte)site;
        _Info.DiagnosticRunVirtualAddresses[slot] = virtualAddress;
        _Info.DiagnosticRunCr3[slot] = cr3;
        _Info.DiagnosticRunRequestedSizes[slot] = requestedSize;
        _Info.DiagnosticRunCallerAddresses[slot] = callerAddress;
        _Info.DiagnosticRunRequesterIds[slot] = CurrentDiagnosticRequest;
        _Info.DiagnosticRunLifetimeIds[slot] = CurrentDiagnosticLifetime;
        for (ulong page = 0; page < pages; page++)
            _Info.DiagnosticPageRunSlots[(address - (ulong)_Info.Start) /
                PageSize + page] = (uint)(slot + 1);
        ulong firstPage = (address - (ulong)_Info.Start) / PageSize;
        for (ulong page = 0; page < pages; page++) {
            ulong pageIndex = firstPage + page;
            _Info.DiagnosticPageOwnerGenerations[pageIndex] =
                owner == 0 ? 0UL : CurrentOwnerGeneration;
            _Info.DiagnosticPageAllocationSequences[pageIndex] = id;
            _Info.DiagnosticPageCallerLabels[pageIndex] =
                CurrentAllocationLabel;
        }
    }

    private static void RecordDiagnosticFree(int page) {
        uint storedSlot = _Info.DiagnosticPageRunSlots[page];
        if (storedSlot == 0) return;
        int slot = (int)storedSlot - 1;
        if (slot >= 0 && slot < DiagnosticRunCapacity &&
                _Info.DiagnosticRunAddresses[slot] ==
                    (ulong)_Info.Start + (ulong)page * PageSize &&
                _Info.DiagnosticRunFreeSequences[slot] == 0)
            _Info.DiagnosticRunFreeSequences[slot] = ++_freeSequence;
    }

    internal static ulong DiagnosticAllocationSequence =>
        _allocationSequence;
    internal static ulong BeginDiagnosticLifetime() {
        ulong previous = CurrentDiagnosticLifetime;
        CurrentDiagnosticLifetime = ++_diagnosticLifetimeSequence;
        return previous;
    }
    internal static void RestoreDiagnosticLifetime(ulong previous) {
        CurrentDiagnosticLifetime = previous;
    }
    internal static ulong DiagnosticSnapshotSequence =>
        _diagnosticSnapshotSequence;

    internal static void CaptureDiagnosticRunBaseline() {
        lock (_sync) {
            CaptureDiagnosticRunBaselineNoLock();
        }
    }

    private static void CaptureDiagnosticRunBaselineNoLock() {
        for (ulong page = 0; page < (ulong)NumPages; page++) {
            _Info.DiagnosticBaselineOccupiedPages[page] = 0;
            _Info.DiagnosticBaselineRunIds[page] = 0;
            _Info.DiagnosticBaselineRunPages[page] = 0;
        }
        for (ulong page = 0; page < (ulong)NumPages;) {
            ulong pages = _Info.Pages[page];
            if (pages == 0 || pages == PageSignature) { page++; continue; }
            if (pages > (ulong)NumPages - page) break;
            _Info.DiagnosticBaselineOccupiedPages[page] = 1;
            _Info.DiagnosticBaselineRunIds[page] =
                _Info.DiagnosticPageAllocationSequences[page];
            _Info.DiagnosticBaselineRunPages[page] = (uint)pages;
            page += pages;
        }
        _diagnosticSnapshotSequence = _allocationSequence;
    }

    internal static void DumpDiagnosticRunsSince(ulong sequence) {
        if (!DiagnosticProvenanceEnabled) return;
        lock (_sync) {
            ulong total = 0;
            ulong retainedPages = 0;
            for (ulong page = 0; page < (ulong)NumPages;) {
                ulong pages = _Info.Pages[page];
                if (pages == 0 || pages == PageSignature) { page++; continue; }
                if (pages > (ulong)NumPages - page) break;
                ulong id = _Info.DiagnosticPageAllocationSequences[page];
                ulong baselineId = _Info.DiagnosticBaselineRunIds[page];
                if (id <= sequence && id == baselineId &&
                        _Info.DiagnosticBaselineOccupiedPages[page] != 0) {
                    page += pages;
                    continue;
                }
                total++;
                retainedPages += pages;
                page += pages;
            }
            ulong freedBaselineRuns = 0;
            ulong freedBaselinePages = 0;
            for (ulong page = 0; page < (ulong)NumPages; page++) {
                ulong baselineId = _Info.DiagnosticBaselineRunIds[page];
                uint baselinePages = _Info.DiagnosticBaselineRunPages[page];
                if (baselinePages == 0) continue;
                ulong currentPages = _Info.Pages[page];
                if (currentPages == baselinePages &&
                        _Info.DiagnosticPageAllocationSequences[page] ==
                            baselineId) continue;
                freedBaselineRuns++;
                freedBaselinePages += baselinePages;
            }
            SerialWriteFreeInvalidText("PHASE35_ALLOC_RUN_TOTALS;count=0x");
            SerialWriteFreeInvalidHex(total);
            SerialWriteFreeInvalidText(";retainedPages=0x");
            SerialWriteFreeInvalidHex(retainedPages);
            SerialWriteFreeInvalidText(";freedBaselineRuns=0x");
            SerialWriteFreeInvalidHex(freedBaselineRuns);
            SerialWriteFreeInvalidText(";freedBaselinePages=0x");
            SerialWriteFreeInvalidHex(freedBaselinePages);
            SerialWriteFreeInvalidText(";netPages=0x");
            SerialWriteFreeInvalidHex(retainedPages >= freedBaselinePages ?
                retainedPages - freedBaselinePages : 0UL);
            Native.Out8(0x3F8, (byte)'\n');

        }
    }

    // Emits numeric-only rows from the fixed run ledger. This does not allocate
    // managed memory and is bounded by the allocator's fixed run capacity.
    internal static ulong DumpDiagnosticLiveRunsSince(ulong sequence,
            int snapshot) {
        if (!DiagnosticProvenanceEnabled) return 0;
        lock (_sync) return DumpDiagnosticLiveRunsSinceNoLock(sequence,
            snapshot);
    }

    internal static void DumpDiagnosticHelperTotalsSince(ulong sequence,
            int snapshot) {
        if (!DiagnosticProvenanceEnabled) return;
        lock (_sync) {
            for (byte helper = 0; helper <= 2; helper++) {
                ulong allocations = 0, frees = 0, survivors = 0, pages = 0;
                for (ulong id = sequence + 1; id <= _allocationSequence; id++) {
                    int slot = (int)((id - 1) % DiagnosticRunCapacity);
                    if (_Info.DiagnosticRunIds[slot] != id ||
                            _Info.DiagnosticRunManagedHelpers[slot] != helper)
                        continue;
                    allocations++;
                    if (_Info.DiagnosticRunFreeSequences[slot] != 0) frees++;
                    ulong address = _Info.DiagnosticRunAddresses[slot];
                    ulong page = (address - (ulong)_Info.Start) / PageSize;
                    bool live = page < (ulong)NumPages &&
                        _Info.DiagnosticPageAllocationSequences[page] == id &&
                        _Info.Pages[page] == _Info.DiagnosticRunPages[slot];
                    if (live) {
                        survivors++;
                        pages += _Info.DiagnosticRunPages[slot];
                    }
                }
                SerialWriteFreeInvalidText("R9_HELPER;phase=");
                SerialWriteFreeInvalidHex((ulong)snapshot);
                SerialWriteFreeInvalidText(";helper=0x");
                SerialWriteFreeInvalidHex(helper);
                SerialWriteFreeInvalidText(";allocations=0x");
                SerialWriteFreeInvalidHex(allocations);
                SerialWriteFreeInvalidText(";frees=0x");
                SerialWriteFreeInvalidHex(frees);
                SerialWriteFreeInvalidText(";survivors=0x");
                SerialWriteFreeInvalidHex(survivors);
                SerialWriteFreeInvalidText(";pages=0x");
                SerialWriteFreeInvalidHex(pages);
                Native.Out8(0x3F8, (byte)'\n');
            }
        }
    }

    private static ulong DumpDiagnosticLiveRunsSinceNoLock(ulong sequence,
            int snapshot) {
        ulong count = 0;
        ulong pagesTotal = 0;
        for (ulong page = 0; page < (ulong)NumPages;) {
            ulong pages = _Info.Pages[page];
            if (pages == 0 || pages == PageSignature) { page++; continue; }
            if (pages > (ulong)NumPages - page) break;
            ulong id = _Info.DiagnosticPageAllocationSequences[page];
            int slot = id == 0 ? -1 : (int)((id - 1) % DiagnosticRunCapacity);
            if (id > sequence && slot >= 0 &&
                    _Info.DiagnosticRunIds[slot] == id &&
                    _Info.DiagnosticRunAddresses[slot] ==
                        (ulong)_Info.Start + page * PageSize &&
                    _Info.DiagnosticRunPages[slot] == pages) {
                count++;
                pagesTotal += pages;
                SerialWriteFreeInvalidText("R9_RUN;phase=");
                SerialWriteFreeInvalidHex((ulong)snapshot);
                SerialWriteFreeInvalidText(";id=0x");
                SerialWriteFreeInvalidHex(id);
                SerialWriteFreeInvalidText(";address=0x");
                SerialWriteFreeInvalidHex(_Info.DiagnosticRunAddresses[slot]);
                SerialWriteFreeInvalidText(";pages=0x");
                SerialWriteFreeInvalidHex(pages);
                SerialWriteFreeInvalidText(";bytes=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunRequestedSizes[slot]);
                SerialWriteFreeInvalidText(";tag=0x");
                SerialWriteFreeInvalidHex(_Info.DiagnosticRunTags[slot]);
                SerialWriteFreeInvalidText(";class=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunAllocationSites[slot]);
                SerialWriteFreeInvalidText(";managedHelper=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunManagedHelpers[slot]);
                SerialWriteFreeInvalidText(";creationSite=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunCreationSites[slot]);
                SerialWriteFreeInvalidText(";callerLabel=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunCallerLabels[slot]);
                SerialWriteFreeInvalidText(";owner=0x");
                SerialWriteFreeInvalidHex(unchecked((ulong)
                    _Info.DiagnosticRunOwnerIds[slot]));
                SerialWriteFreeInvalidText(";generation=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunOwnerGenerations[slot]);
                SerialWriteFreeInvalidText(";requester=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunRequesterIds[slot]);
                SerialWriteFreeInvalidText(";lifetime=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunLifetimeIds[slot]);
                SerialWriteFreeInvalidText(";caller=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunCallerAddresses[slot]);
                ulong managedObject = 0;
                ulong eeTypeAddress = 0;
                ulong typeClass = 0;
                ulong managedLength = 0;
                ulong managedObjectBytes = 0;
                if (_Info.DiagnosticRunManagedHelpers[slot] != 0) {
                    managedObject = _Info.DiagnosticRunAddresses[slot];
                    ulong candidate =
                        _Info.DiagnosticRunEETypeAddresses[slot];
                    if (candidate >= 0x10000000UL &&
                            candidate < 0x11000000UL) {
                        Internal.Runtime.EEType* eeType =
                            (Internal.Runtime.EEType*)candidate;
                        eeTypeAddress = candidate;
                        if (eeType->IsString) typeClass = 1;
                        else if (eeType->IsArray) typeClass = 2;
                        else typeClass = 3;
                        if (eeType->IsString || eeType->IsArray) {
                            int length = *(int*)(managedObject +
                                (ulong)sizeof(ulong));
                            managedLength = unchecked((ulong)length);
                            managedObjectBytes = eeType->BaseSize +
                                unchecked((ulong)length) *
                                eeType->ComponentSize;
                            managedObjectBytes = (managedObjectBytes + 7UL) &
                                ~7UL;
                        } else {
                            managedObjectBytes = eeType->BaseSize;
                        }
                    }
                    eeTypeAddress = candidate;
                }
                SerialWriteFreeInvalidText(";managedObject=0x");
                SerialWriteFreeInvalidHex(managedObject);
                SerialWriteFreeInvalidText(";eeType=0x");
                SerialWriteFreeInvalidHex(eeTypeAddress);
                SerialWriteFreeInvalidText(";typeClass=0x");
                SerialWriteFreeInvalidHex(typeClass);
                SerialWriteFreeInvalidText(";managedLength=0x");
                SerialWriteFreeInvalidHex(managedLength);
                SerialWriteFreeInvalidText(";managedBytes=0x");
                SerialWriteFreeInvalidHex(managedObjectBytes);
                Native.Out8(0x3F8, (byte)'\n');
            }
            page += pages;
        }
        SerialWriteFreeInvalidText("R9_RUN_TOTAL;phase=");
        SerialWriteFreeInvalidHex((ulong)snapshot);
        SerialWriteFreeInvalidText(";lifetime=0x");
        SerialWriteFreeInvalidHex(CurrentDiagnosticLifetime);
        SerialWriteFreeInvalidText(";count=0x");
        SerialWriteFreeInvalidHex(count);
        SerialWriteFreeInvalidText(";pages=0x");
        SerialWriteFreeInvalidHex(pagesTotal);
        Native.Out8(0x3F8, (byte)'\n');
        if (snapshot == 2) DumpDiagnosticLiveRhpArraysNoLock(sequence);
        return pagesTotal;
    }

    private static void DumpDiagnosticLiveRhpArraysNoLock(ulong sequence) {
        int emitted = 0;
        for (ulong page = 0; page < (ulong)NumPages;) {
            ulong pages = _Info.Pages[page];
            if (pages == 0 || pages == PageSignature) { page++; continue; }
            if (pages > (ulong)NumPages - page) break;
            ulong id = _Info.DiagnosticPageAllocationSequences[page];
            if (id <= sequence) { page += pages; continue; }
            int slot = (int)((id - 1) % DiagnosticRunCapacity);
            if (id != 0 && _Info.DiagnosticRunIds[slot] == id &&
                    _Info.DiagnosticRunAllocationSites[slot] ==
                        (byte)DiagnosticAllocationSite.NativeRuntimeMalloc) {
                ulong address = (ulong)_Info.Start + page * PageSize;
                ulong eeTypeAddress = *(ulong*)address;
                // Kernel PE and EEType data share the fixed image range. This
                // bounds the probe before interpreting arbitrary malloc data.
                if (eeTypeAddress >= 0x10000000UL &&
                        eeTypeAddress < 0x11000000UL) {
                    Internal.Runtime.EEType* eeType =
                        (Internal.Runtime.EEType*)eeTypeAddress;
                    if (eeType->IsArray || eeType->IsString) {
                        if (emitted < 256) {
                            emitted++;
                            DumpRhpArrayMetadataNoLock(address, id, eeType);
                        }
                    }
                }
            }
            page += pages;
        }
        SerialWriteFreeInvalidText("PHASE35_RHP_ARRAY_LIVE_DUMP;emitted=0x");
        SerialWriteFreeInvalidHex((ulong)emitted);
        SerialWriteFreeInvalidText(";truncated=0x");
        SerialWriteFreeInvalidHex(emitted >= 256 ? 1UL : 0UL);
        Native.Out8(0x3F8, (byte)'\n');
    }

    private static void DumpRhpArrayMetadataNoLock(ulong address, ulong id,
            Internal.Runtime.EEType* eeType) {
        Internal.Runtime.EEType* componentType = eeType->RelatedParameterType;
        ushort componentCode = componentType == null ? (ushort)0 :
            (ushort)componentType->ElementType;
        bool componentIsObject = componentType != null &&
            Internal.Runtime.EEType.WellKnownEETypes.IsSystemObject(
                componentType);
        byte classification = 0;
        if (eeType->IsArray && eeType->IsSzArray && componentCode == 0x05 &&
                eeType->ComponentSize == 1) classification = 1;
        else if (eeType->IsArray && eeType->IsSzArray &&
                componentCode == 0x03 && eeType->ComponentSize == 2)
            classification = 2;
        else if (eeType->IsArray && eeType->IsSzArray && componentIsObject &&
                eeType->ComponentSize == 8) classification = 3;
        else if (eeType->IsArray && eeType->IsSzArray &&
                componentCode >= 0x02 && componentCode <= 0x0F)
            classification = 4;
        else if (eeType->IsArray) classification = 5;
        else if (eeType->IsString) classification = 6;

        int length = *(int*)(address + sizeof(ulong));
        ulong objectBytes = eeType->BaseSize +
            (ulong)length * eeType->ComponentSize;
        objectBytes = (objectBytes + 7UL) & ~7UL;
        int slot = (int)((id - 1) % DiagnosticRunCapacity);
        SerialWriteFreeInvalidText("PHASE35_RHP_ARRAY_LIVE;id=0x");
        SerialWriteFreeInvalidHex(id);
        SerialWriteFreeInvalidText(";object=0x");
        SerialWriteFreeInvalidHex(address);
        SerialWriteFreeInvalidText(";eeType=0x");
        SerialWriteFreeInvalidHex((ulong)eeType);
        SerialWriteFreeInvalidText(";componentType=0x");
        SerialWriteFreeInvalidHex((ulong)componentType);
        SerialWriteFreeInvalidText(";arrayType=0x");
        SerialWriteFreeInvalidHex((ushort)eeType->ElementType);
        SerialWriteFreeInvalidText(";componentTypeCode=0x");
        SerialWriteFreeInvalidHex(componentCode);
        SerialWriteFreeInvalidText(";componentSize=0x");
        SerialWriteFreeInvalidHex(eeType->ComponentSize);
        SerialWriteFreeInvalidText(";length=0x");
        SerialWriteFreeInvalidHex(unchecked((ulong)length));
        SerialWriteFreeInvalidText(";baseSize=0x");
        SerialWriteFreeInvalidHex(eeType->BaseSize);
        SerialWriteFreeInvalidText(";objectBytes=0x");
        SerialWriteFreeInvalidHex(objectBytes);
        SerialWriteFreeInvalidText(";rank=0x");
        SerialWriteFreeInvalidHex(eeType->IsArray ?
            (ulong)eeType->ArrayRank : 0UL);
        SerialWriteFreeInvalidText(";class=0x");
        SerialWriteFreeInvalidHex(classification);
        SerialWriteFreeInvalidText(";callerAddress=0x");
        SerialWriteFreeInvalidHex(_Info.DiagnosticRunCallerAddresses[slot]);
        SerialWriteFreeInvalidText(";owner=0x");
        SerialWriteFreeInvalidHex(unchecked((ulong)_Info.DiagnosticRunOwnerIds[slot]));
        SerialWriteFreeInvalidText(";generation=0x");
        SerialWriteFreeInvalidHex(
            _Info.DiagnosticRunOwnerGenerations[slot]);
        Native.Out8(0x3F8, (byte)'\n');
    }

    internal static void DumpDiagnosticRequestAllocations(ulong request,
            ulong firstSequence, ulong lastSequence) {
        if (!DiagnosticProvenanceEnabled) return;
#if UEFI_DIAGNOSTIC_RING3_PHASE35
        // The R2 matrix needs bounded aggregate and string-lifetime counters.
        // Per-allocation address rows overwhelm the finite serial capture and
        // prevent the guest harness from observing matrix completion. Keep
        // this disabled for every Phase 35 diagnostic mode.
        return;
#else
        lock (_sync) {
            for (ulong id = firstSequence; id <= lastSequence; id++) {
                int slot = (int)((id - 1) % DiagnosticRunCapacity);
                if (_Info.DiagnosticRunIds[slot] != id) continue;
                ulong address = _Info.DiagnosticRunAddresses[slot];
                ulong page = (address - (ulong)_Info.Start) / PageSize;
                bool live = page < (ulong)NumPages &&
                    _Info.DiagnosticPageAllocationSequences[page] == id &&
                    _Info.Pages[page] == _Info.DiagnosticRunPages[slot];
                SerialWriteFreeInvalidText("PHASE35_READ_ALLOC;request=0x");
                SerialWriteFreeInvalidHex(request);
                SerialWriteFreeInvalidText(";id=0x");
                SerialWriteFreeInvalidHex(id);
                SerialWriteFreeInvalidText(";physical=0x");
                SerialWriteFreeInvalidHex(address);
                SerialWriteFreeInvalidText(";pages=0x");
                SerialWriteFreeInvalidHex(_Info.DiagnosticRunPages[slot]);
                SerialWriteFreeInvalidText(";owner=0x");
                SerialWriteFreeInvalidHex(unchecked((ulong)
                    _Info.DiagnosticRunOwnerIds[slot]));
                SerialWriteFreeInvalidText(";generation=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunOwnerGenerations[slot]);
                SerialWriteFreeInvalidText(";site=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunAllocationSites[slot]);
                SerialWriteFreeInvalidText(";callerLabel=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunCallerLabels[slot]);
                SerialWriteFreeInvalidText(";virtual=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunVirtualAddresses[slot]);
                SerialWriteFreeInvalidText(";cr3=0x");
                SerialWriteFreeInvalidHex(_Info.DiagnosticRunCr3[slot]);
                SerialWriteFreeInvalidText(";requestBytes=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunRequestedSizes[slot]);
                SerialWriteFreeInvalidText(";callerAddress=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunCallerAddresses[slot]);
                SerialWriteFreeInvalidText(";freeSequence=0x");
                SerialWriteFreeInvalidHex(
                    _Info.DiagnosticRunFreeSequences[slot]);
                SerialWriteFreeInvalidText(live ? ";live=1\n" : ";live=0\n");
            }
        }
#endif
    }

    internal static ulong GetDiagnosticLiveRunCount() {
        lock (_sync) {
            ulong count = 0;
            for (ulong page = 0; page < (ulong)NumPages;) {
                ulong run = _Info.Pages[page];
                if (run == 0 || run == PageSignature) { page++; continue; }
                if (run > (ulong)NumPages - page) break;
                count++;
                page += run;
            }
            return count;
        }
    }

    internal static ulong DumpDiagnosticSnapshot(int label) {
        lock (_sync) {
            SerialWriteFreeInvalidText("PHASE35_ALLOC_SNAPSHOT;label=");
            SerialWriteFreeInvalidHex((ulong)label);
            SerialWriteFreeInvalidText(";bytes=0x");
            SerialWriteFreeInvalidHex(MemoryInUse);
            SerialWriteFreeInvalidText(";runs=0x");
            SerialWriteFreeInvalidHex(GetDiagnosticLiveRunCount());
            SerialWriteFreeInvalidText(";owners=0x");
            SerialWriteFreeInvalidHex((ulong)_ownerCount);
            ulong freePages = 0;
            for (int page = 0; page < NumPages; page++)
                if (_Info.Pages[page] == 0) freePages++;
            SerialWriteFreeInvalidText(";freePages=0x");
            SerialWriteFreeInvalidHex(freePages);
            Native.Out8(0x3F8, (byte)'\n');
            for (int tag = 0; tag < (int)AllocTag.Count; tag++) {
                ulong bytes = _Info.TagLivePages[tag] * PageSize;
                if (bytes == 0) continue;
                SerialWriteFreeInvalidText("PHASE35_ALLOC_TAG;label=0x");
                SerialWriteFreeInvalidHex((ulong)label);
                SerialWriteFreeInvalidText(";tag=0x");
                SerialWriteFreeInvalidHex((ulong)tag);
                SerialWriteFreeInvalidText(";bytes=0x");
                SerialWriteFreeInvalidHex(bytes);
                Native.Out8(0x3F8, (byte)'\n');
            }
            for (int ownerIndex = 0; ownerIndex < _ownerCount &&
                    ownerIndex < MAX_OWNERS; ownerIndex++) {
                ulong pages = 0;
                for (ulong page = 0; page < (ulong)NumPages;) {
                    ulong run = _Info.Pages[page];
                    if (run == 0 || run == PageSignature) {
                        page++;
                        continue;
                    }
                    if (run > (ulong)NumPages - page) break;
                    if (_Info.Owners[page] == _ownerIds[ownerIndex])
                        pages += run;
                    page += run;
                }
                SerialWriteFreeInvalidText("PHASE35_ALLOC_OWNER;label=0x");
                SerialWriteFreeInvalidHex((ulong)label);
                SerialWriteFreeInvalidText(";owner=0x");
                SerialWriteFreeInvalidHex(unchecked((ulong)
                    _ownerIds[ownerIndex]));
                SerialWriteFreeInvalidText(";pages=0x");
                SerialWriteFreeInvalidHex(pages);
                Native.Out8(0x3F8, (byte)'\n');
            }
            if (DiagnosticProvenanceEnabled)
                CaptureDiagnosticRunBaselineNoLock();
            return MemoryInUse;
        }
    }
#endif
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

    internal static void GetLiveRunBucketBytes(out ulong onePage,
            out ulong twoToSixteenPages, out ulong seventeenTo256Pages,
            out ulong bucket257To2048Pages, out ulong over2048Pages) {
        onePage = 0;
        twoToSixteenPages = 0;
        seventeenTo256Pages = 0;
        bucket257To2048Pages = 0;
        over2048Pages = 0;
        lock (_sync) {
            ulong page = 0;
            while (page < (ulong)NumPages) {
                ulong run = _Info.Pages[page];
                if (run == 0 || run == PageSignature) {
                    page++;
                    continue;
                }
                if (run > (ulong)NumPages - page) break;
                ulong bytes = run * PageSize;
                if (run == 1) onePage += bytes;
                else if (run <= 16) twoToSixteenPages += bytes;
                else if (run <= 256) seventeenTo256Pages += bytes;
                else if (run <= 2048) bucket257To2048Pages += bytes;
                else over2048Pages += bytes;
                page += run;
            }
        }
    }
    
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

    internal static ulong GetDiagnosticOwnerBytes(int ownerId) {
        if (ownerId >= 0) return 0UL;
        lock (_sync) {
            ulong pages = 0;
            for (int i = 0; i < NumPages; i++) {
                ulong run = _Info.Pages[i];
                if (run == 0 || run == PageSignature) continue;
                if (run > (ulong)NumPages - (ulong)i) break;
                if (_Info.Owners[i] == ownerId) pages += run;
                i += (int)(run - 1);
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
