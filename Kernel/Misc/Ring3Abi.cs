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

                case Exit:
                    stack->rs.rax = Success;
                    process.Exit((int)stack->rs.rdi);
                    break;

                default:
                    stack->rs.rax = InvalidOperation;
                    Marker("RING3_INVALID_OPERATION_REJECTED=1");
                    break;
            }
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
            response.StructureVersion = 1;
            response.Size = (uint)sizeof(Ring3SystemInformationResponse);
            response.UptimeTicks = result.Value.UptimeTicks;
            response.MemorySizeBytes = result.Value.MemorySizeBytes;
            response.MemoryInUseBytes = result.Value.MemoryInUseBytes;
            response.ThreadCount = result.Value.ThreadCount;
            response.CpuUsagePercent = result.Value.CpuUsagePercent;
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
            Native.Movsb((void*)request.ResponseBuffer, &response,
                (ulong)sizeof(Ring3SystemInformationResponse));
            process.RecordServiceRequestSuccess();
            Marker("RING3_SERVICE_RESPONSE_SERIALIZED=1");
            Marker("RING3_SERVICE_RESPONSE_COPIED_OUT=1");
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
