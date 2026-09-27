using System.Runtime.InteropServices;
using guideXOS.OS;

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

    // The notification payload is copied in synchronously.  Text is explicit
    // UTF-16LE code-unit data with character counts; no user pointer is passed
    // into the App Model backend and no terminator is authoritative.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct Ring3NotificationRequest {
        public uint StructureVersion;
        public uint ServiceId;
        public uint OperationId;
        public uint RequestLength;
        public ushort TitleLength;
        public ushort BodyLength;
        public uint Severity;
        public uint Reserved;
        public fixed byte Title[ApplicationNotificationRequest.MaxTitleLength * 2];
        public fixed byte Body[ApplicationNotificationRequest.MaxBodyLength * 2];
    }

    // Phase 11 SetText is copied into a bounded kernel-owned allocation before
    // decoding. The fixed record deliberately contains no user pointer.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct Ring3ClipboardSetRequest {
        public uint StructureVersion;
        public uint ServiceId;
        public uint OperationId;
        public uint RequestLength;
        public uint TextLength;
        public uint Reserved;
        public fixed byte Text[ApplicationClipboardWriteRequest.MaxTextLength * 2];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct Ring3ClipboardResponse {
        public uint StructureVersion;
        public uint Size;
        public uint HasValue;
        public uint TextLength;
        public uint SourceApplicationIdLength;
        public uint Reserved;
        public ulong Generation;
        public fixed byte Text[ApplicationClipboardWriteRequest.MaxTextLength * 2];
        public fixed byte SourceApplicationId[
            ApplicationServiceContext.MaxApplicationIdLength * 2];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Ring3ClipboardClearRequest {
        public uint StructureVersion;
        public uint ServiceId;
        public uint OperationId;
        public uint RequestLength;
        public uint Reserved;
    }

    // Phase 9 Shell stable-application-ID launch. Only bounded copied target
    // data and an output buffer cross the ABI; caller identity is derived from
    // Ring3Process and no target handle/pointer is returned.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct Ring3ShellLaunchRequest {
        public uint StructureVersion;
        public uint ServiceId;
        public uint OperationId;
        public uint RequestLength;
        public uint TargetLength;
        public uint ResponseCapacity;
        public ulong ResponseBuffer;
        public uint Reserved;
        public fixed byte Target[LaunchRequest.MaxTextLength * 2];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Ring3ShellLaunchResponse {
        public uint StructureVersion;
        public uint Size;
        public uint ResultCode;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Ring3ApplicationIdentityResponse {
        public uint StructureVersion;
        public uint Size;
        public ulong StableApplicationId;
        public ulong LifetimeToken;
        public uint ApplicationGeneration;
        public uint ProcessGeneration;
        public uint Architecture;
        public uint Reserved;
    }

    internal static unsafe class Ring3Abi {
        private static bool _enteredMarker;
        internal const ulong AbiVersion = 1;
        internal const ulong Ping = 1;
        internal const ulong Exit = 2;
        internal const ulong ValidateRead = 3;
        internal const ulong ValidateWrite = 4;
        internal const ulong ServiceRequest = 5;
        internal const ulong ApplicationIdentity = 6;
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
        internal const uint NotificationsService =
            (uint)ApplicationServiceId.Notifications;
        internal const uint NotificationsPublishOperation = 1;
        internal const uint ClipboardService =
            (uint)ApplicationServiceId.Clipboard;
        internal const uint ClipboardSetTextOperation = 1;
        internal const uint ClipboardGetTextOperation = 2;
        internal const uint ClipboardClearOperation = 3;
        internal const uint ShellService =
            (uint)ApplicationServiceId.Shell;
        internal const uint ShellLaunchApplicationOperation = 1;

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

                case ApplicationIdentity:
                    stack->rs.rax = DispatchApplicationIdentity(process,
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
            if (requestLength > (ulong)sizeof(Ring3ClipboardSetRequest)) {
                Marker("RING3_SERVICE_INVALID_REQUEST_REJECTED=1");
                return InvalidRequest;
            }
            if (requestLength == (ulong)sizeof(Ring3NotificationRequest))
                return DispatchNotificationRequest(process, requestPointer,
                    requestLength);
            if (requestLength == (ulong)sizeof(Ring3ClipboardSetRequest))
                return DispatchClipboardSetRequest(process, requestPointer,
                    requestLength);
            if (requestLength == (ulong)sizeof(Ring3ClipboardClearRequest))
                return DispatchClipboardClearRequest(process, requestPointer,
                    requestLength);
            if (requestLength == (ulong)sizeof(Ring3ShellLaunchRequest))
                return DispatchShellLaunchRequest(process, requestPointer,
                    requestLength);
            if (requestLength != (ulong)sizeof(Ring3ServiceRequest)) {
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
                request.RequestLength != sizeof(Ring3ServiceRequest) ||
                request.Reserved != 0) {
                Marker("RING3_SERVICE_INVALID_REQUEST_REJECTED=1");
                return InvalidRequest;
            }

            if (request.ServiceId == ClipboardService &&
                request.OperationId == ClipboardGetTextOperation) {
                if (request.ResponseCapacity !=
                        (uint)sizeof(Ring3ClipboardResponse)) {
                    Marker("RING3_CLIPBOARD_INVALID_RESPONSE_REJECTED=1");
                    return InvalidRequest;
                }
                if (!ValidateClipboardWritableRange(process,
                        request.ResponseBuffer, request.ResponseCapacity)) {
                    Marker("RING3_CLIPBOARD_INVALID_RESPONSE_REJECTED=1");
                    return InvalidPointer;
                }
                Marker("RING3_CLIPBOARD_GET_REQUEST_COPIED_IN=1");
                return DispatchClipboardGetRequest(process, request);
            }

            if (request.ServiceId != SystemInformationService ||
                request.OperationId != SystemInformationSnapshotOperation) {
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

        private static ulong DispatchClipboardSetRequest(
                Ring3Process process, ulong requestPointer,
                ulong requestLength) {
            Marker("RING3_CLIPBOARD_SET_ABI_ENTERED=1");
            if (requestLength != (ulong)sizeof(Ring3ClipboardSetRequest) ||
                !ValidateClipboardReadableRange(process, requestPointer,
                    requestLength)) {
                Marker("RING3_CLIPBOARD_INVALID_REQUEST_REJECTED=1");
                return InvalidPointer;
            }

            byte* storage = (byte*)Allocator.Allocate(
                (ulong)sizeof(Ring3ClipboardSetRequest));
            if (storage == null) {
                Marker("RING3_CLIPBOARD_KERNEL_COPY_ALLOC_FAILED=1");
                return InvalidOperation;
            }
            try {
                Native.Movsb(storage, (void*)requestPointer,
                    (ulong)sizeof(Ring3ClipboardSetRequest));
                Marker("RING3_CLIPBOARD_SET_REQUEST_COPIED_IN=1");
                Ring3ClipboardSetRequest* request =
                    (Ring3ClipboardSetRequest*)storage;
                if (request->StructureVersion != AbiVersion ||
                    request->ServiceId != ClipboardService ||
                    request->OperationId != ClipboardSetTextOperation ||
                    request->RequestLength != sizeof(Ring3ClipboardSetRequest) ||
                    request->Reserved != 0 ||
                    request->TextLength >
                        ApplicationClipboardWriteRequest.MaxTextLength) {
                    Marker("RING3_CLIPBOARD_INVALID_REQUEST_REJECTED=1");
                    return InvalidRequest;
                }

                string text = DecodeClipboardText(request->Text,
                    request->TextLength);
                ApplicationServiceContext context;
                ApplicationServiceAccess access;
                ApplicationServiceResult contextResult;
                ApplicationInstance instance;
                if (!TryResolveClipboardAccess(process, out context,
                        out access, out contextResult, out instance)) {
                    return MapServiceResult(contextResult);
                }
                ApplicationServiceResult result = access.Clipboard.SetText(
                    context, ApplicationClipboardWriteRequest.Create(text));
                if (!result.Succeeded) {
                    Marker("RING3_CLIPBOARD_SET_BACKEND_ACCEPTED=0");
                    return MapServiceResult(result);
                }

                ApplicationServiceResult<ApplicationClipboardSnapshot> check =
                    access.Clipboard.GetText(context);
                bool sourceMatches = check.Succeeded && check.Value != null &&
                    check.Value.HasValue &&
                    check.Value.SourceAppId == instance.ApplicationId &&
                    check.Value.Text == text;
                Marker(sourceMatches ?
                    "RING3_CLIPBOARD_SOURCE_IDENTITY_MATCHED=1" :
                    "RING3_CLIPBOARD_SOURCE_IDENTITY_MATCHED=0");
                if (check.Succeeded && check.Value != null)
                    HexMarker("RING3_CLIPBOARD_GENERATION=0x",
                        check.Value.Generation);
                if (!sourceMatches) {
                    Marker("RING3_CLIPBOARD_SET_BACKEND_ACCEPTED=0");
                    return InvalidContext;
                }

                process.RecordServiceRequestSuccess();
                Marker("RING3_CLIPBOARD_SET_BACKEND_ACCEPTED=1");
                Marker("RING3_CLIPBOARD_RESPONSE_COPIED_OUT=1");
                return Success;
            } finally {
                Allocator.Free((System.IntPtr)storage);
            }
        }

        private static ulong DispatchClipboardGetRequest(
                Ring3Process process, Ring3ServiceRequest request) {
            Marker("RING3_CLIPBOARD_GET_ABI_ENTERED=1");
            ApplicationServiceContext context;
            ApplicationServiceAccess access;
            ApplicationServiceResult contextResult;
            ApplicationInstance instance;
            if (!TryResolveClipboardAccess(process, out context, out access,
                    out contextResult, out instance)) {
                return MapServiceResult(contextResult);
            }
            ApplicationServiceResult<ApplicationClipboardSnapshot> result =
                access.Clipboard.GetText(context);
            if (!result.Succeeded || result.Value == null) {
                Marker("RING3_CLIPBOARD_GET_BACKEND_ACCEPTED=0");
                return MapServiceResult(ApplicationServiceResult.Failure(
                    result.Code, result.BoundedDiagnostic));
            }

            byte* storage = (byte*)Allocator.Allocate(
                (ulong)sizeof(Ring3ClipboardResponse));
            if (storage == null) {
                Marker("RING3_CLIPBOARD_KERNEL_RESPONSE_ALLOC_FAILED=1");
                return InvalidOperation;
            }
            try {
                Native.Stosb(storage, 0, (ulong)sizeof(Ring3ClipboardResponse));
                Ring3ClipboardResponse* response =
                    (Ring3ClipboardResponse*)storage;
                ApplicationClipboardSnapshot snapshot = result.Value;
                response->StructureVersion = (uint)AbiVersion;
                response->Size = (uint)sizeof(Ring3ClipboardResponse);
                response->HasValue = snapshot.HasValue ? 1U : 0U;
                response->Generation = snapshot.Generation;
                response->TextLength = CopyUtf16Text(response->Text,
                    ApplicationClipboardWriteRequest.MaxTextLength,
                    snapshot.HasValue ? snapshot.Text : string.Empty);
                response->SourceApplicationIdLength = CopyUtf16Text(
                    response->SourceApplicationId,
                    ApplicationServiceContext.MaxApplicationIdLength,
                    snapshot.HasValue ? snapshot.SourceAppId : string.Empty);
                if (response->TextLength >
                        ApplicationClipboardWriteRequest.MaxTextLength ||
                    response->SourceApplicationIdLength >
                        ApplicationServiceContext.MaxApplicationIdLength) {
                    Marker("RING3_CLIPBOARD_GET_BACKEND_ACCEPTED=0");
                    return InvalidRequest;
                }
                Native.Movsb((void*)request.ResponseBuffer, response,
                    (ulong)sizeof(Ring3ClipboardResponse));
                process.RecordServiceRequestSuccess();
                Marker("RING3_CLIPBOARD_GET_BACKEND_ACCEPTED=1");
                Marker("RING3_CLIPBOARD_USER_BUFFER_COPY=1");
                Marker("RING3_CLIPBOARD_RESPONSE_COPIED_OUT=1");
                HexMarker("RING3_CLIPBOARD_GENERATION=0x",
                    response->Generation);
                HexMarker("RING3_CLIPBOARD_TEXT_LENGTH=0x",
                    response->TextLength);
                HexMarker("RING3_CLIPBOARD_SOURCE_LENGTH=0x",
                    response->SourceApplicationIdLength);
                return Success;
            } finally {
                Allocator.Free((System.IntPtr)storage);
            }
        }

        private static ulong DispatchClipboardClearRequest(
                Ring3Process process, ulong requestPointer,
                ulong requestLength) {
            Marker("RING3_CLIPBOARD_CLEAR_ABI_ENTERED=1");
            if (requestLength != (ulong)sizeof(Ring3ClipboardClearRequest) ||
                !PageTable.ValidateReadableUserRange(process.Space.Pml4,
                    requestPointer, requestLength)) {
                Marker("RING3_CLIPBOARD_INVALID_REQUEST_REJECTED=1");
                return InvalidPointer;
            }
            Ring3ClipboardClearRequest request =
                default(Ring3ClipboardClearRequest);
            Native.Movsb(&request, (void*)requestPointer,
                (ulong)sizeof(Ring3ClipboardClearRequest));
            Marker("RING3_CLIPBOARD_CLEAR_REQUEST_COPIED_IN=1");
            if (request.StructureVersion != AbiVersion ||
                request.ServiceId != ClipboardService ||
                request.OperationId != ClipboardClearOperation ||
                request.RequestLength != sizeof(Ring3ClipboardClearRequest) ||
                request.Reserved != 0) {
                Marker("RING3_CLIPBOARD_INVALID_REQUEST_REJECTED=1");
                return InvalidRequest;
            }

            ApplicationServiceContext context;
            ApplicationServiceAccess access;
            ApplicationServiceResult contextResult;
            ApplicationInstance instance;
            if (!TryResolveClipboardAccess(process, out context, out access,
                    out contextResult, out instance)) {
                return MapServiceResult(contextResult);
            }
            ApplicationServiceResult result = access.Clipboard.Clear(context);
            if (!result.Succeeded) {
                Marker("RING3_CLIPBOARD_CLEAR_BACKEND_ACCEPTED=0");
                return MapServiceResult(result);
            }
            process.RecordServiceRequestSuccess();
            Marker("RING3_CLIPBOARD_CLEAR_BACKEND_ACCEPTED=1");
            Marker("RING3_CLIPBOARD_RESPONSE_COPIED_OUT=1");
            return Success;
        }

        private static ulong DispatchShellLaunchRequest(
                Ring3Process process, ulong requestPointer,
                ulong requestLength) {
            Marker("RING3_SHELL_LAUNCH_ABI_ENTERED=1");
            if (requestLength != (ulong)sizeof(Ring3ShellLaunchRequest) ||
                !PageTable.ValidateReadableUserRange(process.Space.Pml4,
                    requestPointer, requestLength)) {
                Marker("RING3_SHELL_INVALID_REQUEST_REJECTED=1");
                return InvalidPointer;
            }

            Ring3ShellLaunchRequest* storage =
                (Ring3ShellLaunchRequest*)Allocator.Allocate(
                    (ulong)sizeof(Ring3ShellLaunchRequest));
            if (storage == null) return InvalidOperation;
            try {
                Native.Movsb(storage, (void*)requestPointer,
                    (ulong)sizeof(Ring3ShellLaunchRequest));
                Marker("RING3_SHELL_REQUEST_COPIED_IN=1");
                if (storage->StructureVersion != AbiVersion ||
                    storage->ServiceId != ShellService ||
                    storage->OperationId !=
                        ShellLaunchApplicationOperation ||
                    storage->RequestLength !=
                        sizeof(Ring3ShellLaunchRequest) ||
                    storage->TargetLength == 0 ||
                    storage->TargetLength > LaunchRequest.MaxTextLength ||
                    storage->ResponseCapacity !=
                        (uint)sizeof(Ring3ShellLaunchResponse) ||
                    storage->Reserved != 0) {
                    Marker("RING3_SHELL_INVALID_REQUEST_REJECTED=1");
                    return InvalidRequest;
                }
                if (!PageTable.ValidateWritableUserRange(process.Space.Pml4,
                        storage->ResponseBuffer,
                        storage->ResponseCapacity)) {
                    Marker("RING3_SHELL_INVALID_RESPONSE_REJECTED=1");
                    return InvalidPointer;
                }

                Ring3ShellLaunchResponse response =
                    default(Ring3ShellLaunchResponse);
                response.StructureVersion = (uint)AbiVersion;
                response.Size = (uint)sizeof(Ring3ShellLaunchResponse);
                response.ResultCode = (uint)
                    ApplicationServiceResultCode.BackendFailure;

                string target = DecodeShellTarget(storage->Target,
                    storage->TargetLength);
                if (target == null) {
                    Marker("RING3_SHELL_INVALID_REQUEST_REJECTED=1");
                    return InvalidRequest;
                }

                ApplicationServiceContext context;
                ApplicationServiceAccess access;
                ApplicationServiceResult contextResult;
                ApplicationInstance requester;
                if (!TryResolveShellAccess(process, out context, out access,
                        out contextResult, out requester)) {
                    response.ResultCode = (uint)(contextResult == null ?
                        ApplicationServiceResultCode.InvalidContext :
                        contextResult.Code);
                } else {
                    int factoryBefore = ApplicationFactoryRegistry.FactoryLaunches;
                    int fallbackBefore =
                        ApplicationFactoryRegistry.CompatibilityFallbackLaunches;
                    int legacyBefore = AppModelCompatibilityDiagnostics.LegacyBackendCalls;
                    ApplicationServiceResult<ApplicationServiceRequestHandle>
                        begun = access.Shell.Begin(context,
                            ApplicationShellOpenRequest.ForApplicationId(target));
                    if (!begun.Succeeded || !begun.Value.IsValid) {
                        response.ResultCode = (uint)(begun.Succeeded ?
                            ApplicationServiceResultCode.BackendFailure :
                            begun.Code);
                    } else {
                        ApplicationServiceResult<
                            ApplicationServiceRequestStatus<ApplicationShellResult>>
                            observed = access.Shell.Observe(context, begun.Value);
                        if (!observed.Succeeded || observed.Value == null) {
                            response.ResultCode = (uint)(observed.Succeeded ?
                                ApplicationServiceResultCode.BackendFailure :
                                observed.Code);
                        } else if (observed.Value.State !=
                                ApplicationServiceRequestState.Completed ||
                                observed.Value.Value == null) {
                            response.ResultCode = (uint)
                                ApplicationServiceResultCode.BackendFailure;
                        } else {
                            ApplicationShellResult launch =
                                observed.Value.Value;
                            response.ResultCode = (uint)launch.ResultCode;
                            Marker(launch.Succeeded ?
                                "RING3_SHELL_RESOLVER_SUCCESS=1" :
                                (launch.ResultCode ==
                                    ApplicationServiceResultCode.NotFound ?
                                    "RING3_SHELL_RESOLVER_NOT_FOUND=1" :
                                    "RING3_SHELL_RESOLVER_SUCCESS=0"));
                            bool factoryInvoked =
                                ApplicationFactoryRegistry.FactoryLaunches >
                                    factoryBefore;
                            Marker(factoryInvoked ?
                                "RING3_SHELL_TYPED_FACTORY_INVOKED=1" :
                                "RING3_SHELL_TYPED_FACTORY_INVOKED=0");
                            Marker(ApplicationFactoryRegistry.CompatibilityFallbackLaunches ==
                                    fallbackBefore ?
                                "RING3_SHELL_COMPATIBILITY_FALLBACK_DELTA=0" :
                                "RING3_SHELL_COMPATIBILITY_FALLBACK_DELTA=1");
                            Marker(AppModelCompatibilityDiagnostics.LegacyBackendCalls ==
                                    legacyBefore ?
                                "RING3_SHELL_LEGACY_BACKEND_DELTA=0" :
                                "RING3_SHELL_LEGACY_BACKEND_DELTA=1");

                            if (launch.Succeeded) {
                                ApplicationInstance targetInstance;
                                bool targetResolved =
                                    ApplicationInstanceRegistry.TryGet(
                                        launch.InstanceHandle,
                                        out targetInstance) &&
                                    targetInstance != null &&
                                    targetInstance.DescriptorId == target;
                                Marker(targetResolved ?
                                    "RING3_SHELL_TARGET_INSTANCE_CREATED=1" :
                                    "RING3_SHELL_TARGET_INSTANCE_CREATED=0");
                                if (targetResolved) {
                                    Marker(targetInstance.LifecycleState ==
                                            ApplicationInstanceLifecycleState.Running ||
                                        targetInstance.LifecycleState ==
                                            ApplicationInstanceLifecycleState.Activated ?
                                        "RING3_SHELL_TARGET_LIFECYCLE_RUNNING=1" :
                                        "RING3_SHELL_TARGET_LIFECYCLE_RUNNING=0");
                                    Marker("RING3_SHELL_TARGET_ID=" +
                                        targetInstance.DescriptorId);
                                }
                            }
                        }
                    }
                }

                Native.Movsb((void*)storage->ResponseBuffer, &response,
                    (ulong)sizeof(Ring3ShellLaunchResponse));
                process.RecordServiceRequestSuccess();
                Marker("RING3_SHELL_RESPONSE_COPIED_OUT=1");
                Marker("RING3_SHELL_TYPED_RESULT_CODE=" +
                    response.ResultCode.ToString());
                HexMarker("RING3_SHELL_TYPED_RESULT=0x",
                    response.ResultCode);
                return Success;
            } finally {
                Allocator.Free((System.IntPtr)storage);
            }
        }

        private static bool TryResolveShellAccess(
                Ring3Process process, out ApplicationServiceContext context,
                out ApplicationServiceAccess access,
                out ApplicationServiceResult result,
                out ApplicationInstance requester) {
            context = null;
            access = null;
            requester = null;
            result = ApplicationServiceResult.InvalidContextResult();
            Marker("RING3_SHELL_PROCESS_IDENTITY_DERIVED=1");
            ApplicationInstanceHandle owner =
                ApplicationInstanceHandle.FromValue(
                    process.OwningApplicationInstance);
            if (!owner.IsValid ||
                !ApplicationInstanceRegistry.TryGet(owner, out requester) ||
                requester == null) {
                Marker("RING3_SHELL_APP_MODEL_OWNER_DERIVED=0");
                result = ApplicationServiceResult.InvalidContextResult();
                return false;
            }
            Marker("RING3_SHELL_APP_MODEL_OWNER_DERIVED=1");
            if (!ApplicationServiceRegistry.TryCreateContext(owner,
                    out context, out result)) {
                Marker("RING3_SHELL_SERVICE_CONTEXT_DERIVED=0");
                return false;
            }
            ApplicationInstance resolved;
            if (!ApplicationServiceRegistry.TryValidateContext(context,
                    ApplicationServiceId.Shell, out resolved, out result) ||
                resolved != requester) {
                Marker("RING3_SHELL_SERVICE_CONTEXT_DERIVED=0");
                return false;
            }
            if (!ApplicationServiceRegistry.TryGetAccess(context, out access,
                    out result) || access == null || access.Shell == null) {
                Marker("RING3_SHELL_SERVICE_CONTEXT_DERIVED=0");
                return false;
            }
            Marker("RING3_SHELL_SERVICE_CONTEXT_DERIVED=1");
            Marker(requester.LifecycleState ==
                    ApplicationInstanceLifecycleState.Running ||
                requester.LifecycleState ==
                    ApplicationInstanceLifecycleState.Activated ?
                "RING3_SHELL_REQUESTER_LIFECYCLE_ALLOWED=1" :
                "RING3_SHELL_REQUESTER_LIFECYCLE_ALLOWED=0");
            return true;
        }

        private static string DecodeShellTarget(byte* source, uint length) {
            if (source == null || length == 0 ||
                    length > LaunchRequest.MaxTextLength) return null;
            char[] text = new char[(int)length];
            for (int i = 0; i < (int)length; i++) {
                text[i] = (char)(source[(i * 2) + 0] |
                    ((uint)source[(i * 2) + 1] << 8));
            }
            return new string(text);
        }

        // The general user ABI transfer cap is intentionally 64 KiB. The
        // authoritative Phase 11 clipboard value is bounded at 64 Ki UTF-16
        // code units, so its fixed Set/Get records are larger than one general
        // transfer. Validate those records in capped contiguous slices while
        // keeping the global transfer policy unchanged.
        private static bool ValidateClipboardReadableRange(
                Ring3Process process, ulong address, ulong length) {
            while (length != 0) {
                ulong chunk = length > PageTable.MaxUserTransfer ?
                    PageTable.MaxUserTransfer : length;
                if (!PageTable.ValidateReadableUserRange(process.Space.Pml4,
                        address, chunk)) return false;
                address += chunk;
                length -= chunk;
            }
            return true;
        }

        private static bool ValidateClipboardWritableRange(
                Ring3Process process, ulong address, ulong length) {
            while (length != 0) {
                ulong chunk = length > PageTable.MaxUserTransfer ?
                    PageTable.MaxUserTransfer : length;
                if (!PageTable.ValidateWritableUserRange(process.Space.Pml4,
                        address, chunk)) return false;
                address += chunk;
                length -= chunk;
            }
            return true;
        }

        private static bool TryResolveClipboardAccess(
                Ring3Process process, out ApplicationServiceContext context,
                out ApplicationServiceAccess access,
                out ApplicationServiceResult result,
                out ApplicationInstance instance) {
            context = null;
            access = null;
            instance = null;
            result = ApplicationServiceResult.InvalidContextResult();
            Marker("RING3_CLIPBOARD_PROCESS_IDENTITY_DERIVED=1");
            ApplicationInstanceHandle owner =
                ApplicationInstanceHandle.FromValue(
                    process.OwningApplicationInstance);
            if (!owner.IsValid ||
                !ApplicationInstanceRegistry.TryGet(owner, out instance) ||
                instance == null) {
                Marker("RING3_CLIPBOARD_PROCESS_IDENTITY_DERIVED=0");
                return false;
            }
            Marker("RING3_CLIPBOARD_APP_MODEL_OWNER_DERIVED=1");
            if (!ApplicationServiceRegistry.TryCreateContext(owner,
                    out context, out result)) {
                Marker("RING3_CLIPBOARD_SERVICE_CONTEXT_DERIVED=0");
                return false;
            }
            ApplicationInstance resolved;
            ApplicationServiceResult validation;
            if (!ApplicationServiceRegistry.TryValidateContext(context,
                    ApplicationServiceId.Clipboard, out resolved,
                    out validation) || resolved != instance) {
                result = validation;
                Marker("RING3_CLIPBOARD_SERVICE_CONTEXT_DERIVED=0");
                return false;
            }
            if (!ApplicationServiceRegistry.TryGetAccess(context,
                    out access, out result) || access == null ||
                access.Clipboard == null) {
                Marker("RING3_CLIPBOARD_SERVICE_CONTEXT_DERIVED=0");
                return false;
            }
            Marker("RING3_CLIPBOARD_SERVICE_CONTEXT_DERIVED=1");
            Marker("RING3_CLIPBOARD_SOURCE_IDENTITY_DERIVED=1");
            return true;
        }

        private static string DecodeClipboardText(byte* source, uint length) {
            if (source == null || length == 0) return string.Empty;
            char[] text = new char[(int)length];
            for (int i = 0; i < (int)length; i++) {
                text[i] = (char)(source[i * 2] |
                    ((uint)source[(i * 2) + 1] << 8));
            }
            return new string(text);
        }

        private static uint CopyUtf16Text(byte* destination, int capacity,
                                          string source) {
            if (destination == null || source == null ||
                    source.Length > capacity) return 0xffffffffU;
            for (int i = 0; i < source.Length; i++) {
                char value = source[i];
                destination[(i * 2) + 0] = (byte)value;
                destination[(i * 2) + 1] = (byte)(value >> 8);
            }
            return (uint)source.Length;
        }

        private static ulong DispatchNotificationRequest(
                Ring3Process process, ulong requestPointer,
                ulong requestLength) {
            Marker("RING3_NOTIFICATION_ABI_ENTERED=1");
            if (requestLength != (ulong)sizeof(Ring3NotificationRequest) ||
                !PageTable.ValidateReadableUserRange(process.Space.Pml4,
                    requestPointer, requestLength)) {
                Marker("RING3_NOTIFICATION_INVALID_REQUEST_REJECTED=1");
                return InvalidPointer;
            }

            Ring3NotificationRequest request = default(Ring3NotificationRequest);
            Native.Movsb(&request, (void*)requestPointer,
                (ulong)sizeof(Ring3NotificationRequest));
            Marker("RING3_NOTIFICATION_REQUEST_COPIED_IN=1");
            if (request.StructureVersion != AbiVersion ||
                request.ServiceId != NotificationsService ||
                request.OperationId != NotificationsPublishOperation ||
                request.RequestLength != sizeof(Ring3NotificationRequest) ||
                request.Reserved != 0 ||
                request.TitleLength == 0 ||
                request.TitleLength > ApplicationNotificationRequest.MaxTitleLength ||
                request.BodyLength > ApplicationNotificationRequest.MaxBodyLength ||
                (request.Severity != (uint)ApplicationNotificationSeverity.Info &&
                 request.Severity != (uint)ApplicationNotificationSeverity.Error)) {
                Marker("RING3_NOTIFICATION_INVALID_REQUEST_REJECTED=1");
                return InvalidRequest;
            }

            string title;
            string body;
            byte* titleBytes = request.Title;
            byte* bodyBytes = request.Body;
            title = DecodeNotificationText(titleBytes, request.TitleLength);
            body = DecodeNotificationText(bodyBytes, request.BodyLength);
            ApplicationNotificationRequest notification =
                ApplicationNotificationRequest.Create(title, body,
                    request.Severity == (uint)ApplicationNotificationSeverity.Error
                        ? ApplicationNotificationSeverity.Error
                        : ApplicationNotificationSeverity.Info);
            if (notification == null || !notification.IsValid) {
                Marker("RING3_NOTIFICATION_INVALID_REQUEST_REJECTED=1");
                return InvalidRequest;
            }

            // The payload contains no AppId, ApplicationInstance, process, or
            // service-context authority.  All ownership is derived from the
            // scheduled process record below.
            Marker("RING3_NOTIFICATION_PROCESS_IDENTITY_DERIVED=1");
            ApplicationInstanceHandle owner =
                ApplicationInstanceHandle.FromValue(
                    process.OwningApplicationInstance);
            ApplicationInstance instance;
            if (!owner.IsValid ||
                !ApplicationInstanceRegistry.TryGet(owner, out instance) ||
                instance == null) {
                Marker("RING3_NOTIFICATION_PROCESS_IDENTITY_DERIVED=0");
                return InvalidContext;
            }
            Marker("RING3_NOTIFICATION_APP_MODEL_OWNER_DERIVED=1");

            ApplicationServiceContext context;
            ApplicationServiceResult contextResult;
            if (!ApplicationServiceRegistry.TryCreateContext(owner,
                    out context, out contextResult)) {
                Marker("RING3_NOTIFICATION_SERVICE_CONTEXT_DERIVED=0");
                return InvalidContext;
            }
            ApplicationInstance resolved;
            ApplicationServiceResult validationResult;
            if (!ApplicationServiceRegistry.TryValidateContext(context,
                    ApplicationServiceId.Notifications, out resolved,
                    out validationResult) || resolved != instance) {
                Marker("RING3_NOTIFICATION_SERVICE_CONTEXT_DERIVED=0");
                return InvalidContext;
            }
            Marker("RING3_NOTIFICATION_SERVICE_CONTEXT_DERIVED=1");

            ApplicationServiceAccess access;
            if (!ApplicationServiceRegistry.TryGetAccess(context,
                    out access, out contextResult) || access == null ||
                access.Notifications == null) {
                Marker("RING3_NOTIFICATION_SERVICE_CONTEXT_DERIVED=0");
                return InvalidContext;
            }

            ApplicationServiceResult result = access.Notifications.Publish(
                context, notification);
            if (!result.Succeeded) {
                Marker("RING3_NOTIFICATION_BACKEND_ACCEPTED=0");
                return MapServiceResult(result);
            }
            process.RecordServiceRequestSuccess();
            Marker("RING3_NOTIFICATION_BACKEND_ACCEPTED=1");
            Marker("RING3_NOTIFICATION_RESPONSE_COPIED_OUT=1");
            return Success;
        }

        private static string DecodeNotificationText(byte* source, int length) {
            if (source == null || length <= 0) return string.Empty;
            char* text = stackalloc char[length];
            for (int i = 0; i < length; i++) {
                text[i] = (char)(source[i * 2] |
                    ((uint)source[(i * 2) + 1] << 8));
            }
            return new string(text, 0, length);
        }

        private static ulong MapServiceResult(ApplicationServiceResult result) {
            if (result == null) return InvalidOperation;
            switch (result.Code) {
                case ApplicationServiceResultCode.InvalidRequest:
                    return InvalidRequest;
                case ApplicationServiceResultCode.InvalidContext:
                case ApplicationServiceResultCode.InvalidState:
                    return InvalidContext;
                default:
                    return InvalidOperation;
            }
        }

        private static ulong DispatchApplicationIdentity(Ring3Process process,
                                                          ulong responsePointer,
                                                          ulong responseCapacity) {
            Marker("RING3_IDENTITY_ABI_ENTERED=1");
            if (responsePointer == 0 ||
                responseCapacity < (ulong)sizeof(Ring3ApplicationIdentityResponse) ||
                responseCapacity > 4096 ||
                !PageTable.ValidateWritableUserRange(process.Space.Pml4,
                    responsePointer, responseCapacity)) {
                Marker("RING3_IDENTITY_INVALID_RESPONSE_REJECTED=1");
                return InvalidPointer;
            }

            // The payload supplies only a writable destination. The scheduled
            // process record is the sole source of application identity.
            ApplicationInstanceHandle owner =
                ApplicationInstanceHandle.FromValue(
                    process.OwningApplicationInstance);
            ApplicationInstance instance;
            if (!owner.IsValid ||
                !ApplicationInstanceRegistry.TryGet(owner, out instance) ||
                instance == null || string.IsNullOrEmpty(instance.ApplicationId)) {
                Marker("RING3_IDENTITY_KERNEL_OWNER_REJECTED=1");
                return InvalidContext;
            }

            Ring3ApplicationIdentityResponse response =
                default(Ring3ApplicationIdentityResponse);
            response.StructureVersion = (uint)AbiVersion;
            response.Size = (uint)sizeof(Ring3ApplicationIdentityResponse);
            response.StableApplicationId = StableApplicationFingerprint(
                instance.ApplicationId);
            response.LifetimeToken = LifetimeFingerprint(owner.Value,
                                                        process.Handle.Value);
            response.ApplicationGeneration = owner.Generation;
            response.ProcessGeneration = process.Handle.Generation;
            response.Architecture = 0x8664;
            if (response.StableApplicationId == 0 ||
                response.LifetimeToken == 0 ||
                response.ApplicationGeneration == 0 ||
                response.ProcessGeneration == 0) {
                Marker("RING3_IDENTITY_KERNEL_OWNER_REJECTED=1");
                return InvalidContext;
            }
            Native.Movsb((void*)responsePointer, &response,
                (ulong)sizeof(Ring3ApplicationIdentityResponse));
            process.RecordApplicationIdentitySuccess(
                response.StableApplicationId, response.LifetimeToken,
                response.ApplicationGeneration, response.ProcessGeneration);
            Marker("RING3_IDENTITY_KERNEL_DERIVED=1");
            Marker("RING3_IDENTITY_RESPONSE_COPIED_OUT=1");
            return Success;
        }

        private static ulong StableApplicationFingerprint(string value) {
            ulong hash = 1469598103934665603UL;
            if (value == null) return 0;
            for (int i = 0; i < value.Length; i++) {
                char character = value[i];
                hash ^= (byte)(character & 0xFF);
                hash *= 1099511628211UL;
                hash ^= (byte)(character >> 8);
                hash *= 1099511628211UL;
            }
            return hash == 0 ? 1UL : hash;
        }

        private static ulong LifetimeFingerprint(ulong applicationHandle,
                                                 ulong processHandle) {
            ulong hash = 1469598103934665603UL;
            for (int shift = 0; shift < 64; shift += 8) {
                hash ^= (byte)(applicationHandle >> shift);
                hash *= 1099511628211UL;
            }
            for (int shift = 0; shift < 64; shift += 8) {
                hash ^= (byte)(processHandle >> shift);
                hash *= 1099511628211UL;
            }
            return hash == 0 ? 1UL : hash;
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
