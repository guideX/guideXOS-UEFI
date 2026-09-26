using guideXOS.OS;
using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct Ring3ServiceRequest {
        public uint StructureVersion;
        public uint ServiceId;
        public uint OperationId;
        public uint RequestLength;
        public ulong ResponseBuffer;
        public uint ResponseCapacity;
        public uint Reserved;
    }

    // This is a wire representation, not a projection of the managed
    // SystemInformationSnapshot object layout. Text is bounded byte data.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct Ring3SystemInformationResponse {
        public uint StructureVersion;
        public uint Size;
        public ulong UptimeTicks;
        public ulong MemorySizeBytes;
        public ulong MemoryInUseBytes;
        public int ThreadCount;
        public int CpuUsagePercent;
        public ushort OsNameLength;
        public ushort OsVersionLength;
        public ushort ArchitectureLength;
        public ushort Reserved;
        public fixed byte OsName[SystemInformationSnapshot.MaxOsNameLength];
        public fixed byte OsVersion[SystemInformationSnapshot.MaxOsVersionLength];
        public fixed byte Architecture[SystemInformationSnapshot.MaxArchitectureLength];
    }

    internal static unsafe class Ring3Abi {
        private static bool _enteredMarker;
        internal const ulong AbiVersion = 1;
        internal const ulong Ping = 1;
        internal const ulong Exit = 2;
        internal const ulong ValidateRead = 3;
        internal const ulong ValidateWrite = 4;
        internal const ulong ServiceRequest = 5;
        internal const ulong VmReserve = 0x20;
        internal const ulong VmCommit = 0x21;
        internal const ulong VmProtect = 0x22;
        internal const ulong VmRelease = 0x23;
        internal const ulong VmQuery = 0x24;
        internal const ulong TlsInitialize = 0x30;
        internal const ulong TlsCurrentBlock = 0x31;
        internal const ulong FlsAlloc = 0x32;
        internal const ulong FlsGet = 0x33;
        internal const ulong FlsSet = 0x34;
        internal const ulong FlsCleanup = 0x35;
        internal const ulong ThreadId = 0x40;
        internal const ulong ThreadStackBounds = 0x41;
        internal const ulong ThreadRuntimeState = 0x42;
        internal const ulong MonotonicTicks = 0x50;
        internal const ulong MonotonicFrequency = 0x51;
        internal const ulong SystemTimeNs = 0x52;
        internal const ulong RandomBytes = 0x60;
        internal const ulong ProcessExit = 0x70;
        internal const ulong FailFast = 0x71;
        internal const ulong Success = 0;
        internal const ulong InvalidOperation = unchecked((ulong)-38L);
        internal const ulong InvalidPointer = unchecked((ulong)-14L);
        internal const ulong InvalidRequest = unchecked((ulong)-22L);
        internal const ulong InvalidContext = unchecked((ulong)-13L);
        internal const ulong ContextSentinel = 0x142E_5A91_C0DE_4711UL;

        internal const uint SystemInformationService =
            (uint)ApplicationServiceId.SystemInformation;
        internal const uint SystemInformationSnapshotOperation = 1;

        private static void Marker(string text) {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++)
                Native.Out8(0x3F8, (byte)text[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void HexMarker(string label, ulong value) {
            Marker(label);
            for (int shift = 60; shift >= 0; shift -= 4) {
                int nibble = (int)((value >> shift) & 0xFUL);
                Native.Out8(0x3F8, (byte)(nibble < 10 ? '0' + nibble :
                    'A' + nibble - 10));
            }
            Native.Out8(0x3F8, (byte)'\n');
        }

        internal static void Dispatch(IDT.IDTStackGeneric* stack) {
            if (stack == null) return;
            Ring3Process process;
            if (!Ring3Process.TryGetCurrent(out process)) {
                stack->rs.rax = InvalidOperation;
                Marker("RING3_CALLER_VALID=0");
                return;
            }

            if (!_enteredMarker) {
                _enteredMarker = true;
                Marker("RING3_ENTER_CPL=3");
            }
            Marker("RING3_SYSCALL_ENTER=1");
            Marker("RING3_CALLER_VALID=1");
            process.MarkRunning();
            ulong operation = stack->rs.rax;
            HexMarker("RING3_OPERATION=0x", operation);
            switch (operation) {
                case Ping:
                    stack->rs.rax = AbiVersion;
                    Marker("RING3_PING_RESULT=success");
                    if (stack->rs.r12 == ContextSentinel)
                        Marker("RING3_CONTEXT_SENTINEL_PRESERVED=1");
                    break;

                case ValidateRead:
                    if (PageTable.ValidateReadableUserRange(process.Space.Pml4,
                                                             stack->rs.rdi,
                                                             stack->rs.rsi)) {
                        stack->rs.rax = Success;
                    } else {
                        stack->rs.rax = InvalidPointer;
                        Marker("RING3_INVALID_POINTER_REJECTED=1");
                    }
                    break;

                case ValidateWrite:
                    if (PageTable.ValidateWritableUserRange(process.Space.Pml4,
                                                             stack->rs.rdi,
                                                             stack->rs.rsi)) {
                        stack->rs.rax = Success;
                    } else {
                        stack->rs.rax = InvalidPointer;
                        Marker("RING3_INVALID_POINTER_REJECTED=1");
                    }
                    break;

                case ServiceRequest:
                    stack->rs.rax = DispatchServiceRequest(process,
                        stack->rs.rdi, stack->rs.rsi);
                    break;

                case VmReserve:
                    stack->rs.rax = process.ManagedImage == null ? 0UL :
                        process.ManagedImage.TryVmReserve(stack->rs.rdi,
                                                          stack->rs.rsi);
                    break;

                case VmCommit:
                    stack->rs.rax = process.ManagedImage == null ?
                        unchecked((ulong)-1L) : (ulong)process.ManagedImage.TryVmCommit(
                            stack->rs.rdi, stack->rs.rsi, (uint)stack->rs.rdx);
                    break;

                case VmProtect:
                    stack->rs.rax = process.ManagedImage == null ?
                        unchecked((ulong)-1L) : (ulong)process.ManagedImage.TryVmProtect(
                            stack->rs.rdi, stack->rs.rsi, (uint)stack->rs.rdx);
                    break;

                case VmRelease:
                    stack->rs.rax = process.ManagedImage == null ?
                        unchecked((ulong)-1L) : (ulong)process.ManagedImage.TryVmRelease(
                            stack->rs.rdi, stack->rs.rsi);
                    break;

                case VmQuery:
                    stack->rs.rax = DispatchVmQuery(process, stack->rs.rdi,
                        stack->rs.rsi, stack->rs.rdx, stack->rs.r8);
                    break;

                case TlsInitialize:
                    stack->rs.rax = DispatchTlsInitialize(process,
                        stack->rs.rdi, stack->rs.rsi, stack->rs.rdx);
                    break;

                case TlsCurrentBlock:
                    stack->rs.rax = process.ManagedImage == null ? 0UL :
                        (process.ManagedImage.IsPhase26 ?
                            ManagedImageContract.TlsBlockAddress :
                            ManagedImageContract.RuntimeStateAddress);
                    break;

                case ThreadRuntimeState:
                    stack->rs.rax = process.ManagedImage == null ? 0UL :
                        ManagedImageContract.RuntimeStateAddress;
                    break;

                case FlsAlloc:
                    int flsSlot;
                    stack->rs.rax = process.ManagedImage != null &&
                        process.ManagedImage.TryFlsAllocate(out flsSlot) ?
                        (ulong)flsSlot : unchecked((ulong)-1L);
                    break;

                case FlsGet:
                    ulong flsValue;
                    stack->rs.rax = process.ManagedImage != null &&
                        process.ManagedImage.TryFlsGet((int)stack->rs.rdi,
                                                       out flsValue) ?
                        flsValue : 0UL;
                    break;

                case FlsSet:
                    stack->rs.rax = process.ManagedImage != null &&
                        process.ManagedImage.TryFlsSet((int)stack->rs.rdi,
                                                       stack->rs.rsi) ?
                        Success : unchecked((ulong)-1L);
                    break;

                case FlsCleanup:
                    stack->rs.rax = Success;
                    break;

                case ThreadId:
                    stack->rs.rax = process.Handle.Value;
                    break;

                case ThreadStackBounds:
                    stack->rs.rax = DispatchThreadStackBounds(process,
                        stack->rs.rdi, stack->rs.rsi);
                    break;

                case MonotonicTicks:
                    stack->rs.rax = Native.Rdtsc();
                    break;

                case MonotonicFrequency:
                    stack->rs.rax = 1000000000UL;
                    break;

                case SystemTimeNs:
                    stack->rs.rax = Native.Rdtsc();
                    break;

                case RandomBytes:
                    stack->rs.rax = DispatchRandomBytes(process,
                        stack->rs.rdi, stack->rs.rsi);
                    break;

                case ProcessExit:
                    stack->rs.rax = Success;
                    process.Exit((int)stack->rs.rdi);
                    break;

                case FailFast:
                    HexMarker("PHASE26_FAILFAST_REASON=0x", stack->rs.rdi);
                    HexMarker("PHASE26_FAILFAST_CONTEXT=0x", stack->rs.rsi);
                    stack->rs.rax = Success;
                    process.Exit(-1);
                    break;

                case Exit:
                    HexMarker("RING3_EXIT_CODE=0x", stack->rs.rdi);
                    stack->rs.rax = Success;
                    process.Exit((int)stack->rs.rdi);
                    break;

                default:
                    stack->rs.rax = InvalidOperation;
                    Marker("RING3_INVALID_OPERATION_REJECTED=1");
                    break;
            }
            HexMarker("RING3_OPERATION_RESULT=0x", stack->rs.rax);
        }

        private static ulong DispatchServiceRequest(Ring3Process process,
                                                     ulong requestPointer,
                                                     ulong requestLength) {
            Marker("RING3_SERVICE_ABI_ENTERED=1");
            if (requestPointer == 0) {
                Marker("RING3_SERVICE_INVALID_REQUEST_REJECTED=1");
                return InvalidPointer;
            }
            if (requestLength != (ulong)sizeof(Ring3ServiceRequest) ||
                requestLength > 4096) {
                Marker("RING3_SERVICE_INVALID_REQUEST_REJECTED=1");
                return InvalidRequest;
            }
            if (!PageTable.ValidateReadableUserRange(process.Space.Pml4,
                                                       requestPointer,
                                                       requestLength)) {
                Marker("RING3_SERVICE_INVALID_REQUEST_REJECTED=1");
                return InvalidPointer;
            }

            Ring3ServiceRequest request = default(Ring3ServiceRequest);
            Native.Movsb(&request, (void*)requestPointer,
                (ulong)sizeof(Ring3ServiceRequest));
            Marker("RING3_SERVICE_REQUEST_COPIED_IN=1");
            if (request.StructureVersion != AbiVersion ||
                request.ServiceId != SystemInformationService ||
                request.OperationId != SystemInformationSnapshotOperation ||
                request.RequestLength != sizeof(Ring3ServiceRequest) ||
                request.Reserved != 0) {
                Marker("RING3_SERVICE_INVALID_REQUEST_REJECTED=1");
                return InvalidRequest;
            }

            if (request.ResponseCapacity <
                    (uint)sizeof(Ring3SystemInformationResponse) ||
                request.ResponseCapacity > 4096) {
                Marker("RING3_SERVICE_INVALID_RESPONSE_REJECTED=1");
                return InvalidRequest;
            }
            if (!PageTable.ValidateWritableUserRange(process.Space.Pml4,
                                                       request.ResponseBuffer,
                                                       request.ResponseCapacity)) {
                Marker("RING3_SERVICE_INVALID_RESPONSE_REJECTED=1");
                return InvalidPointer;
            }

            // The request contains no AppId, instance handle, or context.
            // Identity is taken only from the scheduled process record.
            Marker("RING3_PROCESS_IDENTITY_DERIVED=1");
            ApplicationInstanceHandle owner =
                ApplicationInstanceHandle.FromValue(
                    process.OwningApplicationInstance);
            ApplicationInstance instance;
            if (!owner.IsValid ||
                !ApplicationInstanceRegistry.TryGet(owner, out instance)) {
                Marker("RING3_PROCESS_IDENTITY_DERIVED=0");
                return InvalidContext;
            }
            Marker("RING3_APP_MODEL_OWNER_DERIVED=1");

            ApplicationServiceContext context;
            ApplicationServiceResult contextResult;
            if (!ApplicationServiceRegistry.TryCreateContext(owner,
                                                               out context,
                                                               out contextResult)) {
                return InvalidContext;
            }
            ApplicationInstance resolved;
            ApplicationServiceResult validationResult;
            if (!ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.SystemInformation,
                    out resolved, out validationResult) || resolved != instance) {
                return InvalidContext;
            }
            Marker("RING3_SERVICE_CONTEXT_DERIVED=1");

            ApplicationServiceAccess access;
            if (!ApplicationServiceRegistry.TryGetAccess(context, out access,
                                                          out contextResult) ||
                access == null || access.SystemInformation == null) {
                return InvalidContext;
            }

            ApplicationServiceResult<SystemInformationSnapshot> result =
                access.SystemInformation.GetSnapshot(context);
            if (!result.Succeeded || !result.Value.IsWithinBounds()) {
                Marker("RING3_SYSTEM_INFORMATION_DISPATCHED=0");
                return InvalidContext;
            }
            Marker("RING3_SYSTEM_INFORMATION_DISPATCHED=1");

            Ring3SystemInformationResponse response =
                default(Ring3SystemInformationResponse);
            ulong memorySize = result.Value.MemorySizeBytes;
            ulong memoryInUse = result.Value.MemoryInUseBytes;
            int threadCount = result.Value.ThreadCount;
            int cpuUsage = result.Value.CpuUsagePercent;
            bool scalarFallback = memorySize == 0;
            if (scalarFallback) {
                // The existing App Model backend remains authoritative for
                // context, capability, and bounded text.  Recover only the
                // scalar counters from the same kernel sources when the
                // generic value projection is zeroed by the custom AOT path.
                memorySize = Allocator.MemorySize;
                memoryInUse = Allocator.MemoryInUse;
                threadCount = ThreadPool.ThreadCount;
                uint rawCpu = ThreadPool.CPUUsage;
                cpuUsage = rawCpu > 100 ? 100 : (int)rawCpu;
            }
            if (memoryInUse > memorySize) memoryInUse = memorySize;
            if (threadCount < 0) threadCount = 0;
            if (cpuUsage < 0) cpuUsage = 0;
            if (cpuUsage > 100) cpuUsage = 100;
            response.StructureVersion = 1;
            response.Size = (uint)sizeof(Ring3SystemInformationResponse);
            response.UptimeTicks = result.Value.UptimeTicks;
            response.MemorySizeBytes = memorySize;
            response.MemoryInUseBytes = memoryInUse;
            response.ThreadCount = threadCount;
            response.CpuUsagePercent = cpuUsage;
            byte* osName = response.OsName;
            byte* osVersion = response.OsVersion;
            byte* architecture = response.Architecture;
            response.OsNameLength = CopyText(osName,
                SystemInformationSnapshot.MaxOsNameLength,
                result.Value.OsName);
            response.OsVersionLength = CopyText(osVersion,
                SystemInformationSnapshot.MaxOsVersionLength,
                result.Value.OsVersion);
            response.ArchitectureLength = CopyText(architecture,
                SystemInformationSnapshot.MaxArchitectureLength,
                result.Value.Architecture);
            if (process.ManagedImage != null && process.ManagedImage.IsPhase27) {
                HexMarker("PHASE27_RESPONSE_SCALAR_FALLBACK=0x",
                    scalarFallback ? 1UL : 0UL);
                HexMarker("PHASE27_RESPONSE_VERSION=0x", response.StructureVersion);
                HexMarker("PHASE27_RESPONSE_SIZE=0x", response.Size);
                HexMarker("PHASE27_RESPONSE_MEMORY=0x", response.MemorySizeBytes);
                HexMarker("PHASE27_RESPONSE_IN_USE=0x", response.MemoryInUseBytes);
                HexMarker("PHASE27_RESPONSE_THREADS=0x",
                    (ulong)(uint)response.ThreadCount);
                HexMarker("PHASE27_RESPONSE_CPU=0x",
                    (ulong)(uint)response.CpuUsagePercent);
                HexMarker("PHASE27_RESPONSE_OS_LEN=0x", response.OsNameLength);
                HexMarker("PHASE27_RESPONSE_VERSION_LEN=0x", response.OsVersionLength);
                HexMarker("PHASE27_RESPONSE_ARCH_LEN=0x", response.ArchitectureLength);
                HexMarker("PHASE27_RESPONSE_ARCH0=0x", architecture[0]);
                HexMarker("PHASE27_RESPONSE_ARCH1=0x", architecture[1]);
                HexMarker("PHASE27_RESPONSE_ARCH2=0x", architecture[2]);
                HexMarker("PHASE27_RESPONSE_ARCH3=0x", architecture[3]);
                HexMarker("PHASE27_RESPONSE_ARCH4=0x", architecture[4]);
                HexMarker("PHASE27_RESPONSE_ARCH5=0x", architecture[5]);
            }
            Native.Movsb((void*)request.ResponseBuffer, &response,
                (ulong)sizeof(Ring3SystemInformationResponse));
            process.RecordServiceRequestSuccess();
            Marker("RING3_SERVICE_RESPONSE_SERIALIZED=1");
            Marker("RING3_SERVICE_RESPONSE_COPIED_OUT=1");
            return Success;
        }

        private static ulong DispatchVmQuery(Ring3Process process, ulong address,
                                             ulong basePointer, ulong sizePointer,
                                             ulong protectionPointer) {
            if (process.ManagedImage == null || basePointer == 0 ||
                sizePointer == 0 || protectionPointer == 0 ||
                !PageTable.ValidateWritableUserRange(process.Space.Pml4,
                    basePointer, 8) ||
                !PageTable.ValidateWritableUserRange(process.Space.Pml4,
                    sizePointer, 8) ||
                !PageTable.ValidateWritableUserRange(process.Space.Pml4,
                    protectionPointer, 4)) return unchecked((ulong)-14L);
            return (ulong)process.ManagedImage.TryVmQuery(address,
                (ulong*)basePointer, (ulong*)sizePointer, (uint*)protectionPointer);
        }

        private static ulong DispatchTlsInitialize(Ring3Process process,
                                                    ulong statePointer,
                                                    ulong templateSize,
                                                    ulong zeroSize) {
            if (process.ManagedImage == null ||
                (statePointer != 0 && statePointer !=
                    ManagedImageContract.RuntimeStateAddress) ||
                templateSize > 0x10000UL || zeroSize > 0x10000UL)
                return unchecked((ulong)-22L);
            return Success;
        }

        private static ulong DispatchThreadStackBounds(Ring3Process process,
                                                        ulong lowPointer,
                                                        ulong highPointer) {
            if (lowPointer == 0 || highPointer == 0 ||
                !PageTable.ValidateWritableUserRange(process.Space.Pml4,
                    lowPointer, 8) ||
                !PageTable.ValidateWritableUserRange(process.Space.Pml4,
                    highPointer, 8)) return unchecked((ulong)-14L);
            *(ulong*)lowPointer = Ring3Process.UserStackStart;
            *(ulong*)highPointer = process.UserStackEnd;
            return Success;
        }

        private static ulong DispatchRandomBytes(Ring3Process process,
                                                  ulong buffer, ulong length) {
            if (length == 0 || length > PageTable.MaxUserTransfer ||
                !PageTable.ValidateWritableUserRange(process.Space.Pml4,
                    buffer, length)) return unchecked((ulong)-14L);
            byte* output = (byte*)buffer;
            ulong state = Native.Rdtsc() ^ process.Handle.Value;
            for (ulong i = 0; i < length; i++) {
                state ^= state << 7;
                state ^= state >> 9;
                state ^= state << 8;
                output[i] = (byte)state;
            }
            return Success;
        }

        private static ushort CopyText(byte* destination, int capacity,
                                       string value) {
            if (destination == null || capacity <= 0 || value == null)
                return 0;
            int count = value.Length < capacity ? value.Length : capacity;
            for (int i = 0; i < count; i++) {
                char c = value[i];
                destination[i] = (byte)(c <= 0x7F ? c : '?');
            }
            return (ushort)count;
        }
    }
}
