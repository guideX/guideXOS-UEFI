using System;

namespace guideXOS.Misc {
    internal static class Ring3ProcessDiagnostics {
        internal static int AddressSpacesCreated;
        internal static int AddressSpacesReclaimed;
        internal static int PageTablesCreated;
        internal static int PageTablesReclaimed;
        internal static int UserCodePagesCreated;
        internal static int UserCodePagesReclaimed;
        internal static int UserDataPagesCreated;
        internal static int UserDataPagesReclaimed;
        internal static int UserStackPagesCreated;
        internal static int UserStackPagesReclaimed;
        internal static int KernelStacksCreated;
        internal static int KernelStacksReclaimed;
        internal static int ProcessHandlesCreated;
        internal static int ProcessHandlesReclaimed;
        internal static int StaleHandleRejections;

        internal static int LiveAddressSpaces {
            get { return AddressSpacesCreated - AddressSpacesReclaimed; }
        }

        internal static int LiveKernelStacks {
            get { return KernelStacksCreated - KernelStacksReclaimed; }
        }

        internal static int LiveUserMappings {
            get {
                return (UserCodePagesCreated - UserCodePagesReclaimed) +
                       (UserDataPagesCreated - UserDataPagesReclaimed) +
                       (UserStackPagesCreated - UserStackPagesReclaimed);
            }
        }

        internal static void RecordPageTablesCreated(int count) {
            if (count > 0) PageTablesCreated += count;
        }

        internal static bool IsBalanced {
            get {
                return LiveAddressSpaces == 0 &&
                       PageTablesCreated == PageTablesReclaimed &&
                       UserCodePagesCreated == UserCodePagesReclaimed &&
                       UserDataPagesCreated == UserDataPagesReclaimed &&
                       UserStackPagesCreated == UserStackPagesReclaimed &&
                       LiveKernelStacks == 0 &&
                       ProcessHandlesCreated == ProcessHandlesReclaimed;
            }
        }
    }

    public unsafe class AddressSpace {
        public ulong* Pml4;
        public ulong RootPhysical => (ulong)Pml4 & PageTable.PageMask;
        private readonly ulong[] _ownedPageTables = new ulong[32];
        private int _ownedPageTableCount;
        private bool _released;

        public AddressSpace() {
            Pml4 = (ulong*)Allocator.Allocate(0x1000);
            if (Pml4 == null) return;
            Native.Movsb(Pml4, PageTable.CurrentRoot, 0x1000);
            Ring3ProcessDiagnostics.AddressSpacesCreated++;
        }

        internal int OwnedPageTableCount => _ownedPageTableCount;
        internal bool IsReleased => _released;

        public bool MapUser(ulong virtualAddress, ulong physicalAddress,
                            bool writable, bool executable) {
            if (Pml4 == null) return false;
            int before = _ownedPageTableCount;
            PageTable.MapOnRootTracked(Pml4, virtualAddress, physicalAddress,
                user: true, writable: writable, executable: executable,
                _ownedPageTables, ref _ownedPageTableCount);
            Ring3ProcessDiagnostics.RecordPageTablesCreated(
                _ownedPageTableCount - before);
            ulong translated;
            return PageTable.TryTranslateUser(Pml4, virtualAddress,
                                               writable, out translated) &&
                   (translated & PageTable.PageMask) ==
                   (physicalAddress & PageTable.PageMask);
        }

        internal bool ProtectUser(ulong virtualAddress, bool writable,
                                  bool executable) {
            return PageTable.SetUserPagePermissions(Pml4, virtualAddress,
                                                     writable, executable);
        }

        internal bool UnmapUser(ulong virtualAddress, out ulong physicalAddress) {
            return PageTable.UnmapUserPage(Pml4, virtualAddress,
                                            out physicalAddress);
        }

        public void Release() {
            if (_released) return;
            _released = true;
            for (int i = _ownedPageTableCount - 1; i >= 0; i--) {
                if (_ownedPageTables[i] != 0) {
                    Allocator.Free((IntPtr)_ownedPageTables[i]);
                    Ring3ProcessDiagnostics.PageTablesReclaimed++;
                }
                _ownedPageTables[i] = 0;
            }
            _ownedPageTableCount = 0;
            if (Pml4 != null) {
                Allocator.Free((IntPtr)Pml4);
                Pml4 = null;
                Ring3ProcessDiagnostics.AddressSpacesReclaimed++;
            }
        }
    }

    public enum Ring3ProcessState : byte {
        Created, Ready, Running, Exiting, Exited, Failed
    }

    public enum Ring3PayloadKind : byte {
        Success, InvalidInput, InvalidServiceBuffers, DeliberateFault,
        ManagedBootstrap
    }

    public readonly struct Ring3ProcessHandle {
        public readonly ulong Value;
        public Ring3ProcessHandle(ulong value) { Value = value; }
        public bool IsValid => Value != 0;
        public int Slot => (int)(Value & 0xFFFFUL) - 1;
        public uint Generation => (uint)(Value >> 32);
        public override string ToString() => Value.ToString();
    }

    public struct Ring3FaultRecord {
        public Ring3ProcessHandle Process;
        public ulong OwningApplication;
        public int Vector;
        public ulong Rip;
        public ulong ErrorCode;
        public ulong Cr2;
        public Ring3ProcessState State;
    }

    internal static class Ring3ProcessTable {
        internal const int Capacity = 4;
        internal static readonly Ring3Process[] Slots = new Ring3Process[Capacity];
        internal static readonly uint[] Generations = new uint[Capacity];

        internal static uint NextGeneration(uint current) {
            return current == 0xFFFFFFFFU ? 1U : current + 1U;
        }

        internal static bool Reserve(out int slot, out uint generation) {
            for (int i = 0; i < Capacity; i++) {
                if (Slots[i] == null) {
                    generation = NextGeneration(Generations[i]);
                    Generations[i] = generation;
                    slot = i;
                    return true;
                }
            }
            slot = -1;
            generation = 0;
            return false;
        }

        internal static Ring3Process Resolve(Ring3ProcessHandle handle) {
            int slot = handle.Slot;
            if (!handle.IsValid || handle.Generation == 0 ||
                slot < 0 || slot >= Capacity) {
                Ring3ProcessDiagnostics.StaleHandleRejections++;
                return null;
            }
            Ring3Process process = Slots[slot];
            if (process == null || process.Handle.Slot != handle.Slot ||
                process.Handle.Generation != handle.Generation ||
                process.Handle.Value != handle.Value) {
                Ring3ProcessDiagnostics.StaleHandleRejections++;
                return null;
            }
            return process;
        }

        internal static void Invalidate(Ring3Process process) {
            int slot = process.Handle.Slot;
            if (slot >= 0 && slot < Capacity && Slots[slot] == process) {
                Slots[slot] = null;
            }
        }

        internal static int LiveCount {
            get {
                int count = 0;
                for (int i = 0; i < Capacity; i++)
                    if (Slots[i] != null) count++;
                return count;
            }
        }

        internal static bool GenerationRolloverSelfTest() {
            return NextGeneration(0) == 1U &&
                   NextGeneration(1) == 2U &&
                   NextGeneration(0xFFFFFFFEU) == 0xFFFFFFFFU &&
                   NextGeneration(0xFFFFFFFFU) == 1U;
        }
    }

    public unsafe sealed class Ring3Process {
        public const ulong UserCodeStart = 0x0000400000000000UL;
        public const ulong UserDataStart = UserCodeStart + 0x2000UL;
        public const ulong UserStackStart = 0x00007FFF00000000UL;
        public const ulong UserStackSize = 0x10000UL;
        // NativeAOT startup performs GC/runtime registration before entering
        // managed Main.  Keep a bounded, process-private stack large enough
        // for that first native lifetime while retaining explicit low/high
        // bounds for the PAL and cleanup proofs.
        private const ulong Phase26UserStackSize = 0x800000UL;
        private const ulong PageSize = 0x1000UL;
        private const ulong KernelStackSize = 0x10000UL;

        public Ring3ProcessHandle Handle { get; private set; }
        public Ring3ProcessState State { get; private set; }
        public Ring3PayloadKind PayloadKind { get; private set; }
        public AddressSpace Space { get; private set; }
        public Thread UserThread { get; private set; }
        public ulong UserCodeEnd => UserCodeStart + PageSize;
        public ulong UserStackEnd => UserStackStart + _userStackSize;
        public int ExitCode { get; private set; }
        public Ring3FaultRecord Fault;
        public ulong OwningApplicationInstance { get; private set; }
        public int TimerPreemptions { get; private set; }
        public int SchedulerDispatches { get; private set; }
        public int ServiceRequestsSucceeded { get; private set; }
        public int ApplicationIdentityRequestsSucceeded { get; private set; }
        internal ulong LastIdentityStableApplicationId { get; private set; }
        internal ulong LastIdentityLifetimeToken { get; private set; }
        internal uint LastIdentityApplicationGeneration { get; private set; }
        internal uint LastIdentityProcessGeneration { get; private set; }
        public bool SchedulerCr3Valid { get; private set; }
        public bool SchedulerRsp0Valid { get; private set; }
        public bool UserRspPreserved { get; private set; }
        internal ManagedImageProcess ManagedImage { get; private set; }
        internal NativeBootstrapImage NativeBootstrap { get; private set; }
        public uint BootstrapResultFlags { get; private set; }
        public int BootstrapReturnCode { get; private set; }
        internal ulong UserCodePhysical => _userCodePhysical;
        internal ulong UserDataPhysical => _userDataPhysical;
        internal ulong UserStackPhysical => _userStackPhysical;
        internal bool IsCleaned => _cleaned;
        internal const ulong UserDataSentinelOffset = 0x300UL;

        private ulong _userCodePhysical;
        private ulong _userDataPhysical;
        private ulong _userStackPhysical;
        private ulong _userStackSize;
        private bool _cleaned;

        private Ring3Process(int slot, uint generation, Ring3PayloadKind kind) {
            Handle = new Ring3ProcessHandle(((ulong)generation << 32) |
                                            (ulong)(slot + 1));
            PayloadKind = kind;
            State = Ring3ProcessState.Created;
            OwningApplicationInstance = 0;
            _userStackSize = UserStackSize;
        }

        public bool IsTerminal => State == Ring3ProcessState.Exiting ||
                                  State == Ring3ProcessState.Exited ||
                                  State == Ring3ProcessState.Failed;

        private static void Marker(string text) {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++) Native.Out8(0x3F8, (byte)text[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void HexMarker(string label, ulong value) {
            Marker(label);
            for (int shift = 60; shift >= 0; shift -= 4) {
                int nibble = (int)((value >> shift) & 0xFUL);
                Native.Out8(0x3F8, (byte)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10));
            }
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void FreePage(ref ulong page) {
            if (page != 0) {
                Allocator.Free((IntPtr)page);
                page = 0;
            }
        }

        public static bool TryCreate(Ring3PayloadKind kind, out Ring3Process process) {
            return TryCreate(kind, 0, out process);
        }

        internal static bool TryCreateManagedBootstrap(
            ulong owningApplicationInstance, bool deliberateFault,
            out Ring3Process process, out string failure) {
            return TryCreateManagedBootstrap(owningApplicationInstance,
                deliberateFault, false, out process, out failure);
        }

        internal static bool TryCreateManagedEntry(
            ulong owningApplicationInstance, out Ring3Process process,
            out string failure) {
            return TryCreateManagedBootstrap(owningApplicationInstance, false,
                true, out process, out failure);
        }

        internal static bool TryCreateManagedServiceEntry(
            ulong owningApplicationInstance, bool failureMode,
            out Ring3Process process, out string failure) {
            return TryCreateManagedBootstrap(owningApplicationInstance, false,
                true, true, failureMode, out process, out failure);
        }

        internal static bool TryCreateManagedSdkEntry(
            ulong owningApplicationInstance, int payloadKind,
            out Ring3Process process, out string failure) {
            return TryCreateManagedBootstrap(owningApplicationInstance, false,
                true, false, false, payloadKind, 0,
                out process, out failure);
        }

        internal static bool TryCreateManagedNotificationEntry(
            ulong owningApplicationInstance, int payloadKind,
            out Ring3Process process, out string failure) {
            return TryCreateManagedBootstrap(owningApplicationInstance, false,
                true, false, false, 0, payloadKind, out process, out failure);
        }

        private static bool TryCreateManagedBootstrap(
            ulong owningApplicationInstance, bool deliberateFault, bool phase26,
            out Ring3Process process, out string failure) {
            return TryCreateManagedBootstrap(owningApplicationInstance,
                deliberateFault, phase26, false, false, 0,
                0,
                out process, out failure);
        }

        private static bool TryCreateManagedBootstrap(
            ulong owningApplicationInstance, bool deliberateFault, bool phase26,
            bool phase27, bool phase27Failure,
            out Ring3Process process, out string failure) {
            return TryCreateManagedBootstrap(owningApplicationInstance,
                deliberateFault, phase26, phase27, phase27Failure, 0,
                0,
                out process, out failure);
        }

        private static bool TryCreateManagedBootstrap(
            ulong owningApplicationInstance, bool deliberateFault, bool phase26,
            bool phase27, bool phase27Failure, int phase28Kind,
            int phase29Kind,
            out Ring3Process process, out string failure) {
            process = null;
            failure = null;
            int slot;
            uint generation;
            if (!Ring3ProcessTable.Reserve(out slot, out generation)) {
                failure = "PROCESS_TABLE_FULL";
                Marker("PHASE25_PROCESS_TABLE_FULL=1");
                return false;
            }

            Ring3Process candidate = new Ring3Process(
                slot, generation, Ring3PayloadKind.ManagedBootstrap);
            candidate.OwningApplicationInstance = owningApplicationInstance;
            Ring3ProcessTable.Slots[slot] = candidate;
            Ring3ProcessDiagnostics.ProcessHandlesCreated++;
            candidate.Space = new AddressSpace();
            if (candidate.Space.Pml4 == null) {
                failure = "ADDRESS_SPACE_FAILED";
                candidate.Cleanup();
                return false;
            }

            ManagedImageProcess managedImage;
            if (!ManagedImageProcess.TryCreateFromRamdisk(
                     owningApplicationInstance, generation, candidate.Space,
                     NativeBootstrapContract.ImageBase, phase26, phase27,
                     phase27Failure, phase28Kind, phase29Kind,
                     out managedImage, out failure) || managedImage == null) {
                if (failure != null) Marker(phase29Kind != 0 ?
                    "PHASE29_IMAGE_CREATE_REJECTED=" + failure :
                    (phase28Kind != 0 ?
                    "PHASE28_IMAGE_CREATE_REJECTED=" + failure :
                    (phase27 ?
                    "PHASE27_IMAGE_CREATE_REJECTED=" + failure :
                    (phase26 ? "PHASE26_IMAGE_CREATE_REJECTED=" + failure :
                        "PHASE25_IMAGE_CREATE_REJECTED=" + failure))));
                candidate.Cleanup();
                return false;
            }
            candidate.ManagedImage = managedImage;
            NativeBootstrapImage nativeBootstrap;
            if (!NativeBootstrapImage.TryCreateFromRamdisk(
                    candidate.Space, phase26,
                    phase27 || phase28Kind != 0 || phase29Kind != 0,
                    out nativeBootstrap, out failure) ||
                nativeBootstrap == null ||
                nativeBootstrap.EntryAddress !=
                    candidate.ManagedImage.NativeBootstrapAddress) {
                if (failure != null) Marker(phase29Kind != 0 ?
                    "PHASE29_BOOTSTRAP_CREATE_REJECTED=" + failure :
                    (phase28Kind != 0 ?
                    "PHASE28_BOOTSTRAP_CREATE_REJECTED=" + failure :
                    (phase27 ?
                    "PHASE27_BOOTSTRAP_CREATE_REJECTED=" + failure :
                    (phase26 ? "PHASE26_BOOTSTRAP_CREATE_REJECTED=" + failure :
                        "PHASE25_BOOTSTRAP_CREATE_REJECTED=" + failure))));
                candidate.Cleanup();
                return false;
            }
            candidate.NativeBootstrap = nativeBootstrap;
            candidate.ManagedImage.SetNativeBootstrapRange(
                nativeBootstrap.ImageBase, nativeBootstrap.ImageSize);
            if (phase26 && !candidate.ManagedImage.TryAuthorizeManagedEntry()) {
                failure = "MANAGED_ENTRY_PREREQUISITES_FAILED";
                candidate.Cleanup();
                return false;
            }

            candidate._userStackSize = phase26 ? Phase26UserStackSize : UserStackSize;
            candidate._userStackPhysical = (ulong)Allocator.Allocate(candidate._userStackSize);
            if (candidate._userStackPhysical == 0) {
                failure = "USER_STACK_FAILED";
                candidate.Cleanup();
                return false;
            }
            Ring3ProcessDiagnostics.UserStackPagesCreated +=
                (int)(candidate._userStackSize / PageSize);
            Native.Stosb((void*)candidate._userStackPhysical, 0, candidate._userStackSize);
            for (ulong offset = 0; offset < candidate._userStackSize; offset += PageSize) {
                if (!candidate.Space.MapUser(UserStackStart + offset,
                                             candidate._userStackPhysical + offset,
                                             writable: true, executable: false)) {
                    failure = "USER_STACK_MAP_FAILED";
                    candidate.Cleanup();
                    return false;
                }
            }
            candidate.UserThread = Thread.CreateUser(candidate,
                candidate.NativeBootstrap.EntryAddress,
                UserStackStart + candidate._userStackSize - 16, KernelStackSize);
            if (candidate.UserThread == null) {
                failure = "KERNEL_STACK_FAILED";
                candidate.Cleanup();
                return false;
            }
            if (phase26)
                HexMarker("PHASE26_INITIAL_USER_RSP=0x",
                    candidate.UserThread.Stack->irs.rsp);
            candidate.UserThread.UserGsBase = candidate.ManagedImage.UserGsBase;
            Native.Stosb(&candidate.UserThread.Stack->rs, 0,
                (ulong)sizeof(IDT.RegistersStack));
            candidate.UserThread.Stack->rs.rdi =
                ManagedImageContract.StartupBlockAddress;
            if (deliberateFault && !candidate.ManagedImage.SetBootstrapFaultMode()) {
                failure = "FAULT_MODE_SETUP_FAILED";
                candidate.Cleanup();
                return false;
            }
            if (!candidate.ManagedImage.ValidateRuntimeScaffold() ||
                !candidate.TryAuthorizeUserEntry(
                    candidate.NativeBootstrap.EntryAddress)) {
                failure = "DISPATCH_CONTRACT_FAILED";
                candidate.Cleanup();
                return false;
            }
            Marker(phase26 ? "PHASE26_PROCESS_CREATED=1" :
                "PHASE25_PROCESS_CREATED=1");
            HexMarker("PHASE25_PROCESS_HANDLE=0x", candidate.Handle.Value);
            HexMarker("PHASE25_PROCESS_SLOT=0x", (ulong)slot);
            HexMarker("PHASE25_PROCESS_GENERATION=0x", generation);
            HexMarker("PHASE25_PROCESS_CR3=0x", candidate.Space.RootPhysical);
            HexMarker("PHASE25_BOOTSTRAP_ENTRY=0x",
                candidate.NativeBootstrap.EntryAddress);
            HexMarker("PHASE25_USER_STACK_START=0x", UserStackStart);
            HexMarker("PHASE25_USER_STACK_END=0x", candidate.UserStackEnd);
            HexMarker("PHASE25_KERNEL_STACK_TOP=0x",
                candidate.UserThread.KernelStackTop);
            HexMarker("PHASE25_USER_GS_BASE=0x",
                candidate.UserThread.UserGsBase);
            HexMarker("PHASE25_OWNER_HANDLE=0x",
                owningApplicationInstance);
            process = candidate;
            return true;
        }

        internal bool StartManagedBootstrap() {
            if (ManagedImage == null || NativeBootstrap == null ||
                UserThread == null || State != Ring3ProcessState.Created)
                return false;
            if (!TryAuthorizeUserEntry(NativeBootstrap.EntryAddress)) return false;
            UserThread.Start(0);
            State = Ring3ProcessState.Ready;
            Marker(ManagedImage != null && ManagedImage.IsPhase26 ?
                "PHASE26_BOOTSTRAP_SCHEDULED=1" :
                "PHASE25_BOOTSTRAP_SCHEDULED=1");
            return true;
        }

        internal static bool TryCreate(Ring3PayloadKind kind,
                                       ulong owningApplicationInstance,
                                       out Ring3Process process) {
            process = null;
            int slot;
            uint generation;
            if (!Ring3ProcessTable.Reserve(out slot, out generation)) {
                Marker("RING3_PROCESS_TABLE_FULL=1");
                return false;
            }

            Ring3Process candidate = new Ring3Process(slot, generation, kind);
            candidate.OwningApplicationInstance = owningApplicationInstance;
            Ring3ProcessTable.Slots[slot] = candidate;
            Ring3ProcessDiagnostics.ProcessHandlesCreated++;
            candidate.Space = new AddressSpace();
            if (candidate.Space.Pml4 == null) {
                candidate.Cleanup();
                return false;
            }
            candidate._userCodePhysical = (ulong)Allocator.Allocate(PageSize);
            candidate._userDataPhysical = (ulong)Allocator.Allocate(PageSize);
            candidate._userStackPhysical = (ulong)Allocator.Allocate(UserStackSize);
            if (candidate._userCodePhysical != 0)
                Ring3ProcessDiagnostics.UserCodePagesCreated++;
            if (candidate._userDataPhysical != 0)
                Ring3ProcessDiagnostics.UserDataPagesCreated++;
            if (candidate._userStackPhysical != 0)
                Ring3ProcessDiagnostics.UserStackPagesCreated +=
                    (int)(UserStackSize / PageSize);
            if (candidate._userCodePhysical == 0 || candidate._userDataPhysical == 0 ||
                candidate._userStackPhysical == 0) {
                candidate.Cleanup();
                return false;
            }
            Native.Stosb((void*)candidate._userDataPhysical, 0, PageSize);
            Native.Stosb((void*)candidate._userStackPhysical, 0, UserStackSize);

            if (!candidate.Space.MapUser(UserCodeStart, candidate._userCodePhysical,
                                         writable: false, executable: true) ||
                !candidate.Space.MapUser(UserDataStart, candidate._userDataPhysical,
                                         writable: true, executable: false)) {
                candidate.Cleanup();
                return false;
            }
            for (ulong offset = 0; offset < UserStackSize; offset += PageSize) {
                if (!candidate.Space.MapUser(UserStackStart + offset,
                                             candidate._userStackPhysical + offset,
                                             writable: true, executable: false)) {
                    candidate.Cleanup();
                    return false;
                }
            }

            byte* payload = null;
            ulong payloadSize = 0;
            switch (kind) {
                case Ring3PayloadKind.Success:
                    if (owningApplicationInstance == 0) {
                        payload = Native.GetR3DirectPayloadStart();
                        payloadSize = Native.GetR3DirectPayloadSize();
                    } else {
                        payload = Native.GetR3PayloadStart();
                        payloadSize = Native.GetR3PayloadSize();
                    }
                    break;
                case Ring3PayloadKind.InvalidInput:
                    payload = Native.GetR3InvalidPayloadStart();
                    payloadSize = Native.GetR3InvalidPayloadSize();
                    break;
                case Ring3PayloadKind.InvalidServiceBuffers:
                    payload = Native.GetR3InvalidServicePayloadStart();
                    payloadSize = Native.GetR3InvalidServicePayloadSize();
                    break;
                case Ring3PayloadKind.DeliberateFault:
                    payload = Native.GetR3FaultPayloadStart();
                    payloadSize = Native.GetR3FaultPayloadSize();
                    break;
            }
            if (payload == null || payloadSize == 0 || payloadSize > PageSize) {
                candidate.Cleanup();
                return false;
            }
            Native.Movsb((void*)candidate._userCodePhysical, payload, payloadSize);

            candidate.UserThread = Thread.CreateUser(candidate,
                UserCodeStart, UserStackStart + UserStackSize - 16, KernelStackSize);
            if (candidate.UserThread == null) {
                candidate.Cleanup();
                return false;
            }
            candidate.UserThread.Start(0);
            candidate.State = Ring3ProcessState.Ready;
            Marker("RING3_PROCESS_CREATED=1");
            HexMarker("RING3_PROCESS_HANDLE=0x", candidate.Handle.Value);
            HexMarker("RING3_USER_CODE_START=0x", UserCodeStart);
            HexMarker("RING3_USER_CODE_END=0x", candidate.UserCodeEnd);
            HexMarker("RING3_USER_STACK_START=0x", UserStackStart);
            HexMarker("RING3_USER_STACK_END=0x", candidate.UserStackEnd);
            Marker("RING3_USER_ADDRESS_SPACE_CREATED=1");
            Marker("RING3_USER_CODE_MAPPED=1");
            Marker("RING3_USER_STACK_MAPPED=1");
            HexMarker("RING3_PROCESS_SLOT=0x", (ulong)slot);
            HexMarker("RING3_PROCESS_GENERATION=0x", generation);
            HexMarker("RING3_PROCESS_CR3=0x", candidate.Space.RootPhysical);
            HexMarker("RING3_USER_DATA_PHYSICAL=0x", candidate._userDataPhysical);
            HexMarker("RING3_USER_STACK_PHYSICAL=0x", candidate._userStackPhysical);
            if (candidate.UserThread != null) {
                HexMarker("RING3_KERNEL_STACK_BASE=0x",
                    candidate.UserThread.KernelStackBase);
                HexMarker("RING3_KERNEL_STACK_TOP=0x",
                    candidate.UserThread.KernelStackTop);
            }
            HexMarker("RING3_OWNER_HANDLE=0x",
                candidate.OwningApplicationInstance);
            process = candidate;
            return true;
        }

        internal static bool TryGetCurrent(out Ring3Process process) {
            process = ThreadPool.CurrentProcess;
            return process != null && process.UserThread == ThreadPool.CurrentThread;
        }

        internal bool TryAuthorizeUserEntry(ulong rip) {
            if (ManagedImage == null) return true;
            bool allowManagedEntryResume = ManagedImage.IsPhase26 &&
                SchedulerDispatches > 0;
            return ManagedImage.TryAuthorizeEntry(rip,
                allowManagedEntryResume);
        }

        internal void RejectUserDispatch(ulong rip) {
            if (ManagedImage == null) return;
            State = Ring3ProcessState.Failed;
            if (UserThread != null) UserThread.Terminated = true;
            Marker("PHASE25_USER_DISPATCH_REJECTED=1");
            HexMarker("PHASE25_REJECTED_RIP=0x", rip);
        }

        internal bool TryReadBootstrapResult() {
            if (ManagedImage == null) return false;
            uint flags;
            int returnCode;
            bool valid = ManagedImage.TryReadBootstrapResult(out flags,
                                                              out returnCode);
            BootstrapResultFlags = flags;
            BootstrapReturnCode = returnCode;
            return valid;
        }

        internal bool CorruptManagedStartupVersionForTest() {
            if (ManagedImage == null) return false;
            return ManagedImage.CorruptStartupVersionForTest();
        }

        internal bool CorruptManagedGsForTest() {
            if (ManagedImage == null) return false;
            return ManagedImage.CorruptGsForTest();
        }

        internal bool BootstrapResultSucceeded {
            get {
                int expectedReturn = ManagedImage != null &&
                    ManagedImage.IsPhase29TitleFailure ? 31 :
                    (ManagedImage != null && ManagedImage.IsPhase29BodyFailure ? 32 :
                    (ManagedImage != null && ManagedImage.IsPhase29InvalidType ? 35 :
                    (ManagedImage != null && ManagedImage.IsPhase29FailFast ? -1 :
                    (ManagedImage != null && ManagedImage.IsPhase29 ? 29 :
                    (ManagedImage != null && ManagedImage.IsPhase28TypedFailure ? 23 :
                    (ManagedImage != null && ManagedImage.IsPhase28 ? 28 :
                    (ManagedImage != null && ManagedImage.IsPhase27Failure ? 21 :
                    (ManagedImage != null && ManagedImage.IsPhase27 ? 27 : 42))))))));
                return TryReadBootstrapResult() &&
                    (ManagedImage != null && ManagedImage.IsPhase26 ?
                        BootstrapResultFlags == ManagedBootstrapResultContract.Phase26SuccessFlags &&
                        BootstrapReturnCode == expectedReturn :
                        BootstrapResultFlags == ManagedBootstrapResultContract.SuccessFlags);
            }
        }

        internal void RecordTimerPreemption(IDT.IDTStackGeneric* stack) {
            if (State == Ring3ProcessState.Exited ||
                State == Ring3ProcessState.Failed) return;
            TimerPreemptions++;
            if (ManagedImage != null && ManagedImage.IsPhase26 &&
                TimerPreemptions <= 12 && stack != null)
                HexMarker("PHASE26_PREEMPT_RSP=0x", stack->irs.rsp);
            if (TimerPreemptions == 1) {
                Marker("RING3_TIMER_PREEMPTED_CPL3=1");
            }
            if (stack != null && (stack->irs.cs & 3UL) == 3UL &&
                stack->irs.rsp >= UserStackStart &&
                stack->irs.rsp <= UserStackEnd) {
                UserRspPreserved = true;
                if (TimerPreemptions == 1)
                    Marker("RING3_USER_RSP_CAPTURED=1");
            }
        }

        internal void RecordSchedulerDispatch() {
            SchedulerDispatches++;
            ulong activeCr3 = Native.ReadCR3() & PageTable.PageMask;
            SchedulerCr3Valid = Space != null && activeCr3 == Space.RootPhysical;
            SchedulerRsp0Valid = UserThread != null &&
                                 GDT.KernelStackTop == UserThread.KernelStackTop;
            if (ManagedImage != null && ManagedImage.IsPhase26 &&
                SchedulerDispatches <= 12 && UserThread != null &&
                UserThread.Stack != null)
                HexMarker("PHASE26_DISPATCH_USER_RSP=0x",
                    UserThread.Stack->irs.rsp);
            if (SchedulerDispatches == 1) {
                if (UserThread != null && UserThread.Stack != null)
                    HexMarker("PHASE26_SELECTED_USER_RSP=0x",
                        UserThread.Stack->irs.rsp);
                Marker("RING3_USER_THREAD_SCHEDULED=1");
                Marker(SchedulerCr3Valid ?
                    "RING3_PROCESS_CR3_ACTIVATED=1" :
                    "RING3_PROCESS_CR3_ACTIVATED=0");
                Marker(SchedulerRsp0Valid ?
                    "RING3_TSS_RSP0_UPDATED=1" :
                    "RING3_TSS_RSP0_UPDATED=0");
            } else {
                Marker("RING3_USER_THREAD_RESUMED=1");
                if (SchedulerCr3Valid) Marker("RING3_RESUME_CR3_VALID=1");
                if (SchedulerRsp0Valid) Marker("RING3_RESUME_RSP0_VALID=1");
                if (UserThread != null && UserThread.Stack != null &&
                    UserThread.Stack->irs.rsp >= UserStackStart &&
                    UserThread.Stack->irs.rsp <= UserStackEnd) {
                    UserRspPreserved = true;
                    Marker("RING3_RESUME_USER_RSP_VALID=1");
                }
            }
        }

        internal static bool HandleUserFault(int vector, ulong errorCode, ulong rip,
                                              ulong userRsp, ulong cr2) {
            Ring3Process process;
            if (!TryGetCurrent(out process)) return false;
            process.State = Ring3ProcessState.Failed;
            process.Fault = new Ring3FaultRecord {
                Process = process.Handle,
                OwningApplication = process.OwningApplicationInstance,
                Vector = vector,
                Rip = rip,
                ErrorCode = errorCode,
                Cr2 = cr2,
                State = Ring3ProcessState.Failed
            };
            process.UserThread.Terminated = true;
            Marker("RING3_FAULT_ENTERED_CPL=3");
            HexMarker("RING3_FAULT_VECTOR=0x", (ulong)vector);
            HexMarker("RING3_FAULT_RIP=0x", rip);
            HexMarker("RING3_FAULT_CR2=0x", cr2);
            HexMarker("RING3_FAULT_RSP=0x", userRsp);
            Marker("RING3_FAULT_RECORD_CREATED=1");
            Marker("RING3_FAULT_CONTAINED=1");
            return true;
        }

        internal void Exit(int code) {
            if (State != Ring3ProcessState.Ready && State != Ring3ProcessState.Running)
                return;
            ExitCode = code;
            State = Ring3ProcessState.Exiting;
            if (UserThread != null) UserThread.Terminated = true;
            Marker("RING3_NORMAL_EXIT=1");
        }

        internal void PrepareDirectReturn(IDT.IDTStackGeneric* stack) {
            if (stack == null) return;
            stack->vectorSlot = ThreadPool.Ring0ContextSwitchMarker;
            // The native epilogue receives the stable selected-frame pointer
            // through the dummy error slot.  Direct user mode returns use the
            // active frame itself as that stable descriptor.
            stack->errorCode = (ulong)stack;
            stack->irs.rip = Native.GetR3ResumeStub();
            stack->irs.cs = GDT.KernelCodeSelector;
            stack->irs.rflags = 0x2;
            stack->irs.rsp = Native.GetR3ResumeStack();
            stack->irs.ss = GDT.KernelDataSelector;
        }

        internal void MarkRunning() {
            if (State == Ring3ProcessState.Ready) State = Ring3ProcessState.Running;
        }

        internal void RecordServiceRequestSuccess() {
            ServiceRequestsSucceeded++;
        }

        internal void RecordApplicationIdentitySuccess(ulong stableApplicationId,
                                                       ulong lifetimeToken,
                                                       uint applicationGeneration,
                                                       uint processGeneration) {
            ApplicationIdentityRequestsSucceeded++;
            LastIdentityStableApplicationId = stableApplicationId;
            LastIdentityLifetimeToken = lifetimeToken;
            LastIdentityApplicationGeneration = applicationGeneration;
            LastIdentityProcessGeneration = processGeneration;
        }

        public bool TryResolveHandle(Ring3ProcessHandle handle) =>
            Ring3ProcessTable.Resolve(handle) != null;

        internal ulong ReadUserDataSentinel() {
            return _userDataPhysical == 0 ? 0UL :
                *((ulong*)(_userDataPhysical + UserDataSentinelOffset));
        }

        internal void WriteUserDataSentinel(ulong value) {
            if (_userDataPhysical != 0)
                *((ulong*)(_userDataPhysical + UserDataSentinelOffset)) = value;
        }

        public bool Cleanup() {
            if (_cleaned) return true;
            if (UserThread != null && ThreadPool.IsProcessActive(this)) {
                // A process's RSP0 stack and CR3 cannot be reclaimed while a
                // CPU is still executing on them.  The scheduler must first
                // transfer to a kernel-owned frame.
                Marker("RING3_CLEANUP_ACTIVE_PROCESS_REJECTED=1");
                return false;
            }
            _cleaned = true;
            if (UserThread != null) UserThread.Terminated = true;
            State = State == Ring3ProcessState.Failed ?
                Ring3ProcessState.Failed : Ring3ProcessState.Exited;
            if (UserThread != null) {
                Thread userThread = UserThread;
                userThread.OwnerProcess = null;
                userThread.IsUserThread = false;
                ThreadPool.RemoveThread(userThread);
                bool remains = ThreadPool.ContainsThread(userThread);
                if (remains) {
                    Marker("RING3_STALE_USER_THREADS=1");
                    _cleaned = false;
                    return false;
                }
                if (userThread.Stack != null)
                    Allocator.Free((IntPtr)userThread.Stack);
                if (userThread.KernelStackBase != 0) {
                    Allocator.Free((IntPtr)userThread.KernelStackBase);
                    Ring3ProcessDiagnostics.KernelStacksReclaimed++;
                }
                userThread.Stack = null;
                userThread.KernelStackBase = 0;
                userThread.KernelStackSize = 0;
                userThread.KernelStackTop = 0;
                UserThread = null;
                Marker(!remains ? "RING3_STALE_USER_THREADS=0" :
                    "RING3_STALE_USER_THREADS=1");
            }
            if (NativeBootstrap != null) {
                NativeBootstrap.Cleanup();
                NativeBootstrap = null;
            }
            if (ManagedImage != null) {
                ManagedImage.Cleanup();
                ManagedImage = null;
            }
            if (Space != null) {
                Space.Release();
                Space = null;
            }
            if (_userCodePhysical != 0) {
                FreePage(ref _userCodePhysical);
                Ring3ProcessDiagnostics.UserCodePagesReclaimed++;
            }
            if (_userDataPhysical != 0) {
                FreePage(ref _userDataPhysical);
                Ring3ProcessDiagnostics.UserDataPagesReclaimed++;
            }
            if (_userStackPhysical != 0) {
                FreePage(ref _userStackPhysical);
                Ring3ProcessDiagnostics.UserStackPagesReclaimed +=
                    (int)(_userStackSize / PageSize);
            }
            OwningApplicationInstance = 0;
            ExitCode = 0;
            Fault = default(Ring3FaultRecord);
            TimerPreemptions = 0;
            SchedulerDispatches = 0;
            ServiceRequestsSucceeded = 0;
            ApplicationIdentityRequestsSucceeded = 0;
            SchedulerCr3Valid = false;
            SchedulerRsp0Valid = false;
            UserRspPreserved = false;
            BootstrapResultFlags = 0;
            Ring3ProcessTable.Invalidate(this);
            Ring3ProcessDiagnostics.ProcessHandlesReclaimed++;
            Marker("RING3_ADDRESS_SPACE_RECLAIMED=1");
            Marker("RING3_STALE_USER_MAPPINGS=0");
            Marker("RING3_KERNEL_STACK_RECLAIMED=1");
            Marker("RING3_PROCESS_HANDLE_INVALIDATED=1");
            return true;
        }
    }
}
