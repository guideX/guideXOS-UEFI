using System;

namespace guideXOS.Misc {
    public unsafe class AddressSpace {
        public ulong* Pml4;
        public ulong RootPhysical => (ulong)Pml4 & PageTable.PageMask;
        private readonly ulong[] _ownedPageTables = new ulong[32];
        private int _ownedPageTableCount;

        public AddressSpace() {
            Pml4 = (ulong*)Allocator.Allocate(0x1000);
            if (Pml4 == null) return;
            Native.Movsb(Pml4, PageTable.CurrentRoot, 0x1000);
        }

        public bool MapUser(ulong virtualAddress, ulong physicalAddress,
                            bool writable, bool executable) {
            if (Pml4 == null) return false;
            PageTable.MapOnRootTracked(Pml4, virtualAddress, physicalAddress,
                user: true, writable: writable, executable: executable,
                _ownedPageTables, ref _ownedPageTableCount);
            ulong translated;
            return PageTable.TryTranslateUser(Pml4, virtualAddress,
                                               writable, out translated) &&
                   (translated & PageTable.PageMask) ==
                   (physicalAddress & PageTable.PageMask);
        }

        public void Release() {
            for (int i = _ownedPageTableCount - 1; i >= 0; i--) {
                if (_ownedPageTables[i] != 0)
                    Allocator.Free((IntPtr)_ownedPageTables[i]);
                _ownedPageTables[i] = 0;
            }
            _ownedPageTableCount = 0;
            if (Pml4 != null) {
                Allocator.Free((IntPtr)Pml4);
                Pml4 = null;
            }
        }
    }

    public enum Ring3ProcessState : byte {
        Created, Ready, Running, Exiting, Exited, Failed
    }

    public enum Ring3PayloadKind : byte {
        Success, InvalidInput, InvalidServiceBuffers, DeliberateFault
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

        internal static bool Reserve(out int slot, out uint generation) {
            for (int i = 0; i < Capacity; i++) {
                if (Slots[i] == null) {
                    generation = ++Generations[i];
                    if (generation == 0) generation = ++Generations[i];
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
            if (!handle.IsValid || slot < 0 || slot >= Capacity) return null;
            Ring3Process process = Slots[slot];
            return process != null && process.Handle.Value == handle.Value ? process : null;
        }

        internal static void Invalidate(Ring3Process process) {
            int slot = process.Handle.Slot;
            if (slot >= 0 && slot < Capacity && Slots[slot] == process) {
                Slots[slot] = null;
                Generations[slot]++;
                if (Generations[slot] == 0) Generations[slot]++;
            }
        }
    }

    public unsafe sealed class Ring3Process {
        public const ulong UserCodeStart = 0x0000400000000000UL;
        public const ulong UserDataStart = UserCodeStart + 0x2000UL;
        public const ulong UserStackStart = 0x00007FFF00000000UL;
        public const ulong UserStackSize = 0x10000UL;
        private const ulong PageSize = 0x1000UL;
        private const ulong KernelStackSize = 0x10000UL;

        public Ring3ProcessHandle Handle { get; private set; }
        public Ring3ProcessState State { get; private set; }
        public Ring3PayloadKind PayloadKind { get; private set; }
        public AddressSpace Space { get; private set; }
        public Thread UserThread { get; private set; }
        public ulong UserCodeEnd => UserCodeStart + PageSize;
        public ulong UserStackEnd => UserStackStart + UserStackSize;
        public int ExitCode { get; private set; }
        public Ring3FaultRecord Fault;
        public ulong OwningApplicationInstance { get; private set; }
        public int TimerPreemptions { get; private set; }
        public int SchedulerDispatches { get; private set; }
        public bool SchedulerCr3Valid { get; private set; }
        public bool SchedulerRsp0Valid { get; private set; }
        public bool UserRspPreserved { get; private set; }

        private ulong _userCodePhysical;
        private ulong _userDataPhysical;
        private ulong _userStackPhysical;
        private bool _cleaned;

        private Ring3Process(int slot, uint generation, Ring3PayloadKind kind) {
            Handle = new Ring3ProcessHandle(((ulong)generation << 32) |
                                            (ulong)(slot + 1));
            PayloadKind = kind;
            State = Ring3ProcessState.Created;
            OwningApplicationInstance = 0;
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
            candidate.Space = new AddressSpace();
            if (candidate.Space.Pml4 == null) {
                candidate.Cleanup();
                return false;
            }
            candidate._userCodePhysical = (ulong)Allocator.Allocate(PageSize);
            candidate._userDataPhysical = (ulong)Allocator.Allocate(PageSize);
            candidate._userStackPhysical = (ulong)Allocator.Allocate(UserStackSize);
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
            process = candidate;
            return true;
        }

        internal static bool TryGetCurrent(out Ring3Process process) {
            process = ThreadPool.CurrentProcess;
            return process != null && process.UserThread == ThreadPool.CurrentThread;
        }

        internal void RecordTimerPreemption(IDT.IDTStackGeneric* stack) {
            if (State == Ring3ProcessState.Exited ||
                State == Ring3ProcessState.Failed) return;
            TimerPreemptions++;
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
            if (SchedulerDispatches == 1) {
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
                                              ulong cr2, IDT.IDTStackGeneric* stack) {
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
            stack->irs.rip = Native.GetR3ResumeStub();
            stack->irs.cs = GDT.KernelCodeSelector;
            stack->irs.rflags = 0x2;
            stack->irs.rsp = Native.GetR3ResumeStack();
            stack->irs.ss = GDT.KernelDataSelector;
        }

        internal void MarkRunning() {
            if (State == Ring3ProcessState.Ready) State = Ring3ProcessState.Running;
        }

        public bool TryResolveHandle(Ring3ProcessHandle handle) =>
            Ring3ProcessTable.Resolve(handle) != null;

        public void Cleanup() {
            if (_cleaned) return;
            _cleaned = true;
            if (UserThread != null) UserThread.Terminated = true;
            State = State == Ring3ProcessState.Failed ?
                Ring3ProcessState.Failed : Ring3ProcessState.Exited;
            if (UserThread != null) {
                Thread userThread = UserThread;
                userThread.OwnerProcess = null;
                userThread.IsUserThread = false;
                bool removed = ThreadPool.RemoveThread(userThread);
                if (userThread.Stack != null)
                    Allocator.Free((IntPtr)userThread.Stack);
                if (userThread.KernelStackBase != 0)
                    Allocator.Free((IntPtr)userThread.KernelStackBase);
                userThread.Stack = null;
                userThread.KernelStackBase = 0;
                UserThread = null;
                Marker(removed ? "RING3_STALE_USER_THREADS=0" :
                    "RING3_STALE_USER_THREADS=1");
            }
            if (Space != null) Space.Release();
            FreePage(ref _userCodePhysical);
            FreePage(ref _userDataPhysical);
            FreePage(ref _userStackPhysical);
            OwningApplicationInstance = 0;
            Ring3ProcessTable.Invalidate(this);
            Marker("RING3_ADDRESS_SPACE_RECLAIMED=1");
            Marker("RING3_STALE_USER_MAPPINGS=0");
            Marker("RING3_KERNEL_STACK_RECLAIMED=1");
            Marker("RING3_PROCESS_HANDLE_INVALIDATED=1");
        }
    }
}
