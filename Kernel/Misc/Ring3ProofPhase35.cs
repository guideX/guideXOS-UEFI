using guideXOS.OS;
using System;

namespace guideXOS.Misc {
    internal static unsafe partial class Ring3Proof {
        private const string Phase35ApplicationId =
            "selftest.phase10.persistent";
        private const string Phase35CrossScopeApplicationId =
            "selftest.phase35.crossscope";
        private const string Phase35TemporaryPath = "phase35-reset.tmp";
        private const string Phase35EmptyPath = "empty.bin";
        private const string Phase35MaximumPath = "max.bin";
        private const string Phase35MaximumSha256 =
            "138258426EF34FA0B2F63915382FCD402BD5C25ECED993F3BDAC1D00077003D5";
        private static bool _phase35Scheduled;
        private static CSharpApplicationStorageService _phase35Storage;

        private static void Phase35Marker(string value) {
            const string prefix = "PHASE35_";
            for (int i = 0; i < prefix.Length; i++)
                Native.Out8(0x3F8, (byte)prefix[i]);
            if (value != null) {
                for (int i = 0; i < value.Length; i++)
                    Native.Out8(0x3F8, (byte)value[i]);
            }
            Native.Out8(0x3F8, (byte)'\n');
        }

        internal static void SchedulePhase35() {
            if (_phase35Scheduled) return;
            _phase35Scheduled = true;
            Phase35Marker("SCHEDULED=1");
            new Thread(&RunPhase35, 196608).Start(0);
        }

        private static bool TryCreatePhase35Owner(string applicationId,
                out ApplicationServiceContext context,
                out ApplicationServiceAccess access) {
            context = null;
            access = null;
            if (_owner != null) return false;
            LaunchRequest request = LaunchRequest.ForAppId(applicationId,
                null, null, LaunchActivationIntent.NewInstance);
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!ApplicationInstanceRegistry.TryBeginLaunch(applicationId,
                    ApplicationInstancePolicy.MultiInstance, request,
                    out instance, out reused, out failure) ||
                instance == null || reused ||
                !ApplicationInstanceRegistry.TryCompleteLaunch(instance,
                    false, out failure)) {
                Phase35Marker("REQUESTER_CREATED=0");
                return false;
            }
            _owner = instance;
            _ownerCreated = true;
            ApplicationServiceResult contextResult;
            if (!ApplicationServiceRegistry.TryCreateContextAndAccess(
                    instance.Handle, out context, out access,
                    out contextResult) || context == null || access == null) {
                CleanupOwner();
                Phase35Marker("REQUESTER_CONTEXT_CREATED=0");
                return false;
            }
            if (applicationId == Phase35ApplicationId)
                _phase35Storage = access.Storage as
                    CSharpApplicationStorageService;
            return access.Storage != null;
        }

        private static byte[] CreatePhase35MaximumValue() {
            byte[] value = new byte[PersistentFatBackend.MaxValueLength];
            for (int i = 0; i < value.Length; i++)
                value[i] = (byte)((i * 31 + (i >> 8) * 13 + 0x35) & 0xFF);
            return value;
        }

        private static bool EnsurePhase35DiagnosticValues(
                ApplicationServiceContext context,
                ApplicationServiceAccess access) {
            bool preconditions = context != null && access != null &&
                access.Storage != null && _phase35Storage != null &&
                _phase35Storage.PersistentBackendAvailable &&
                _phase35Storage.PersistentBackendWritable;
            Phase35Marker("SETUP_PRECONDITIONS=" +
                (preconditions ? "1" : "0") + ",backend=" +
                (_phase35Storage != null &&
                 _phase35Storage.PersistentBackendAvailable ? "1" : "0") +
                ",writable=" + (_phase35Storage != null &&
                 _phase35Storage.PersistentBackendWritable ? "1" : "0"));
            if (!preconditions) return false;

            ApplicationServiceResult<ApplicationStorageReadResult> fixture =
                access.Storage.Read(context,
                    ApplicationStorageReadRequest.Create(
                        ApplicationStorageNamespace.Persistent,
                        PersistentFatBackend.FixturePath, 0,
                        PersistentFatBackend.MaxValueLength));
            bool fixtureValid = fixture.Succeeded && fixture.Value != null &&
                fixture.Value.BytesRead == 32 &&
                PersistentStorageFixture.Equal(fixture.Value.Bytes,
                    PersistentStorageFixture.CreateBytes()) &&
                PersistentStorageFixture.ComputeSha256(fixture.Value.Bytes) ==
                    PersistentStorageFixture.Sha256;
            Phase35Marker("SETUP_FIXTURE=code=" +
                ((int)fixture.Code).ToString() +
                ",bytes=" + (fixture.Value == null ? -1 :
                    fixture.Value.BytesRead).ToString() + ",valid=" +
                (fixtureValid ? "1" : "0"));
            if (!fixtureValid) return false;
            Phase35Marker("FIXTURE_PREEXISTING_OR_SEEDED=" +
                _phase35Storage.PersistentFixtureStatusName);
            Phase35Marker("FIXTURE_SHA256=" +
                _phase35Storage.PersistentVerifiedFixtureSha256);
            Phase35Marker("FIXTURE_SEED_WRITES=" +
                _phase35Storage.PersistentSeedWritesPerformed.ToString());

            ApplicationStorageRequest emptyPath =
                ApplicationStorageRequest.Create(
                    ApplicationStorageNamespace.Persistent,
                    Phase35EmptyPath);
            ApplicationServiceResult<bool> emptyExists =
                access.Storage.Exists(context, emptyPath);
            Phase35Marker("SETUP_EMPTY_EXISTS=code=" +
                ((int)emptyExists.Code).ToString() +
                ",value=" + (emptyExists.Value ? "1" : "0"));
            if (!emptyExists.Succeeded) return false;
            if (!emptyExists.Value) {
                byte[] emptyPayload = new byte[0];
                ApplicationStorageWriteRequest emptyWriteRequest =
                    ApplicationStorageWriteRequest.Create(
                        ApplicationStorageNamespace.Persistent,
                        Phase35EmptyPath, emptyPayload);
                Phase35Marker("SETUP_EMPTY_REQUEST_VALID=" +
                    (emptyWriteRequest.IsValid ? "1" : "0") +
                    ",payload=" + (emptyWriteRequest.Payload == null
                        ? "null" : emptyWriteRequest.Payload.Length.ToString()));
                ApplicationServiceResult emptyWrite = access.Storage.Write(
                    context, emptyWriteRequest);
                Phase35Marker("SETUP_EMPTY_WRITE=code=" +
                    ((int)emptyWrite.Code).ToString() + ",succeeded=" +
                    (emptyWrite.Succeeded ? "1" : "0") + ",mutation=" +
                    _phase35Storage.PersistentLastMutationDiagnostic);
                if (!emptyWrite.Succeeded) return false;
            }
            ApplicationServiceResult<ApplicationStorageReadResult> empty =
                access.Storage.Read(context,
                    ApplicationStorageReadRequest.Create(
                        ApplicationStorageNamespace.Persistent,
                        Phase35EmptyPath, 0, 1));
            bool emptyValid = empty.Succeeded && empty.Value != null &&
                empty.Value.BytesRead == 0 && empty.Value.EndOfResource;
            Phase35Marker("SETUP_EMPTY_READ=code=" +
                ((int)empty.Code).ToString() +
                ",bytes=" + (empty.Value == null ? -1 :
                    empty.Value.BytesRead).ToString() + ",end=" +
                (empty.Value != null && empty.Value.EndOfResource ? "1" : "0") +
                ",valid=" + (emptyValid ? "1" : "0"));
            if (!emptyValid) return false;

            byte[] maximum = CreatePhase35MaximumValue();
            ApplicationServiceResult maxWrite = access.Storage.Write(context,
                ApplicationStorageWriteRequest.Create(
                    ApplicationStorageNamespace.Persistent,
                        Phase35MaximumPath, maximum));
            Phase35Marker("SETUP_MAXIMUM_WRITE=code=" +
                ((int)maxWrite.Code).ToString() + ",succeeded=" +
                (maxWrite.Succeeded ? "1" : "0") + ",mutation=" +
                _phase35Storage.PersistentLastMutationDiagnostic);
            if (!maxWrite.Succeeded) return false;
            ApplicationServiceResult<ApplicationStorageReadResult> maxRead =
                access.Storage.Read(context,
                    ApplicationStorageReadRequest.Create(
                        ApplicationStorageNamespace.Persistent,
                        Phase35MaximumPath, 0,
                        PersistentFatBackend.MaxValueLength));
            bool maxReadValid = maxRead.Succeeded && maxRead.Value != null &&
                maxRead.Value.BytesRead == maximum.Length &&
                maxRead.Value.EndOfResource &&
                PersistentStorageFixture.Equal(maxRead.Value.Bytes, maximum);
            Phase35Marker("SETUP_MAXIMUM_READ=code=" +
                ((int)maxRead.Code).ToString() +
                ",bytes=" + (maxRead.Value == null ? -1 :
                    maxRead.Value.BytesRead).ToString() + ",end=" +
                (maxRead.Value != null && maxRead.Value.EndOfResource ? "1" : "0") +
                ",valid=" + (maxReadValid ? "1" : "0"));
            if (!maxReadValid) return false;
            string maximumHash = PersistentStorageFixture.ComputeSha256(
                maxRead.Value.Bytes);
            Phase35Marker("MAXIMUM_LENGTH=" +
                maxRead.Value.BytesRead.ToString());
            Phase35Marker("MAXIMUM_END_OF_VALUE=" +
                (maxRead.Value.EndOfResource ? "1" : "0"));
            Phase35Marker("MAXIMUM_SHA256=" + maximumHash);
            if (maximumHash != Phase35MaximumSha256) return false;

            ApplicationStorageWriteRequest tooLarge =
                ApplicationStorageWriteRequest.Create(
                    ApplicationStorageNamespace.Persistent, "oversize.bin",
                    new byte[PersistentFatBackend.MaxValueLength + 1]);
            bool oversizeRejected = !tooLarge.IsValid &&
                access.Storage.Write(context, tooLarge).Code ==
                    ApplicationServiceResultCode.InvalidRequest;
            Phase35Marker(oversizeRejected ?
                "OVERSIZE_65537_REJECTED=1" : "OVERSIZE_65537_REJECTED=0");
            return oversizeRejected;
        }

        private static bool CheckPhase35RawRequestValidation() {
            Ring3PersistentReadRequest request =
                default(Ring3PersistentReadRequest);
            request.StructureVersion = (uint)Ring3Abi.AbiVersion;
            request.OperationId = Ring3Abi.PersistentReadOperation;
            request.RequestLength = (uint)sizeof(Ring3PersistentReadRequest);
            request.PathLength = (uint)PersistentFatBackend.FixturePath.Length;
            request.DataCapacity = 32;
            request.ResponseCapacity = (uint)
                sizeof(Ring3PersistentReadResponse);
            request.ResponseBuffer = 0x0000401100004000UL;
            request.DataBuffer = 0x0000401100005000UL;
            byte* path = request.Path;
            for (int i = 0; i < PersistentFatBackend.FixturePath.Length; i++) {
                char c = PersistentFatBackend.FixturePath[i];
                path[i * 2] = (byte)c;
                path[(i * 2) + 1] = (byte)(c >> 8);
            }
            bool valid = Ring3Abi.ValidatePersistentReadRequestForPhase35Proof(
                &request) == Ring3Abi.Success;
            request.OperationId = 99;
            bool operationRejected =
                Ring3Abi.ValidatePersistentReadRequestForPhase35Proof(
                    &request) == Ring3Abi.InvalidRequest;
            request.OperationId = Ring3Abi.PersistentReadOperation;
            request.PathLength = 0;
            bool emptyPathRejected =
                Ring3Abi.ValidatePersistentReadRequestForPhase35Proof(
                    &request) == Ring3Abi.InvalidRequest;
            request.PathLength = (uint)PersistentFatBackend.FixturePath.Length;
            request.Path[0] = (byte)'.';
            request.Path[1] = 0;
            request.Path[2] = (byte)'.';
            request.Path[3] = 0;
            request.Path[4] = (byte)'/';
            request.Path[5] = 0;
            for (int i = 3; i < PersistentFatBackend.FixturePath.Length; i++) {
                request.Path[i * 2] = (byte)PersistentFatBackend.FixturePath[i - 3];
                request.Path[(i * 2) + 1] = 0;
            }
            bool traversalRejected =
                Ring3Abi.ValidatePersistentReadRequestForPhase35Proof(
                    &request) == Ring3Abi.InvalidRequest;
            Phase35Marker(valid ? "RAW_VALID_ACCEPTED=1" : "RAW_VALID_ACCEPTED=0");
            Phase35Marker(operationRejected ? "RAW_OPERATION_REJECTED=1" :
                "RAW_OPERATION_REJECTED=0");
            Phase35Marker(emptyPathRejected ? "RAW_EMPTY_PATH_REJECTED=1" :
                "RAW_EMPTY_PATH_REJECTED=0");
            Phase35Marker(traversalRejected ? "RAW_TRAVERSAL_REJECTED=1" :
                "RAW_TRAVERSAL_REJECTED=0");
            return valid && operationRejected && emptyPathRejected &&
                traversalRejected;
        }

        private static bool CheckPhase35UserBuffers(Ring3Process process) {
            bool zero = !Ring3Abi.ValidatePersistentReadBufferForPhase35Proof(
                process, 0, 1);
            bool unmapped = !Ring3Abi.ValidatePersistentReadBufferForPhase35Proof(
                process, 0x0000600000000000UL, 32);
            bool overflow = !Ring3Abi.ValidatePersistentReadBufferForPhase35Proof(
                process, 0xFFFFFFFFFFFFFFF0UL, 32);
            bool mapped = Ring3Abi.ValidatePersistentReadBufferForPhase35Proof(
                process, Ring3Process.UserStackStart + 0x1000UL, 32);
            Phase35Marker(zero ? "ZERO_BUFFER_REJECTED=1" : "ZERO_BUFFER_REJECTED=0");
            Phase35Marker(unmapped ? "UNMAPPED_BUFFER_REJECTED=1" :
                "UNMAPPED_BUFFER_REJECTED=0");
            Phase35Marker(overflow ? "OVERFLOW_BUFFER_REJECTED=1" :
                "OVERFLOW_BUFFER_REJECTED=0");
            Phase35Marker(mapped ? "MAPPED_USER_BUFFER_ACCEPTED=1" :
                "MAPPED_USER_BUFFER_ACCEPTED=0");
            return zero && unmapped && overflow && mapped;
        }

        private static bool RunOnePhase35Lifetime(int payloadKind,
                ulong owningApplicationInstance, int expectedRequests,
                out Ring3ProcessHandle oldHandle, out bool resumed,
                out int observedExitCode, out int serviceRequests) {
            oldHandle = default(Ring3ProcessHandle);
            resumed = false;
            observedExitCode = int.MinValue;
            serviceRequests = -1;
            Native.Cli();
            ulong memoryBeforeCreate = Allocator.MemoryInUse;
#if UEFI_DIAGNOSTIC_RING3_PHASE35
#if UEFI_DIAGNOSTIC_RING3_PHASE35R2_MATRIX
            ulong allocationSequenceBefore =
                Allocator.DiagnosticSnapshotSequence;
#else
            Allocator.CaptureDiagnosticRunBaseline();
            ulong allocationSequenceBefore = Allocator.DiagnosticAllocationSequence;
#endif
#endif
            string failure;
            Ring3Process process;
            if (!Ring3Process.TryCreateManagedPersistentReadEntry(
                    owningApplicationInstance, payloadKind, out process,
                    out failure) || process == null) {
                Native.Sti();
                if (string.IsNullOrEmpty(failure))
                    failure = "PROCESS_CREATE_FAILED_WITHOUT_REASON";
                Phase35Marker("PROCESS_CREATE_FAILED=1");
                Phase35Marker("PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }

            bool diagnoseLifetime = process.ManagedImage != null &&
                process.ManagedImage.IsPhase35;
            ulong memoryAfterCreate = Allocator.MemoryInUse;
            if (diagnoseLifetime)
                HexMarker("PHASE35_DIAG_CREATE_MEMORY_DELTA=0x",
                    memoryAfterCreate >= memoryBeforeCreate ?
                        memoryAfterCreate - memoryBeforeCreate : 0);

            oldHandle = process.Handle;
            bool buffers = CheckPhase35UserBuffers(process);
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool authorized = process.ManagedImage != null &&
                process.ManagedImage.TryEnterManagedEntry();
            bool started = buffers && scaffold && authorized &&
                process.StartManagedBootstrap();
            if (started) Native.Sti();
            int spins = 0;
            while (started && !process.IsTerminal && spins++ < 9000000)
                Native.Hlt();

            bool completed = process.IsTerminal;
            bool dispatched = process.SchedulerDispatches >= 1 &&
                process.SchedulerCr3Valid && process.SchedulerRsp0Valid;
            resumed = process.TimerPreemptions > 0 &&
                process.SchedulerDispatches >= 2 && process.UserRspPreserved;
            serviceRequests = process.ServiceRequestsSucceeded;
            observedExitCode = process.ExitCode;
            bool failFast = payloadKind == 2;
            bool mainResult = failFast
                ? observedExitCode == -1 && serviceRequests == expectedRequests
                : process.BootstrapResultSucceeded &&
                    process.BootstrapReturnCode == 35 &&
                    observedExitCode == 35 &&
                    serviceRequests == expectedRequests;
            bool state = process.State == Ring3ProcessState.Exiting ||
                process.State == Ring3ProcessState.Exited;
            ulong processCr3 = process.Space == null ? 0UL :
                process.Space.RootPhysical;
            ulong processApplicationInstance =
                process.OwningApplicationInstance;
            Ring3ProcessState terminalState = process.State;
            ulong memoryBeforeCleanup = Allocator.MemoryInUse;
            bool clean = process.Cleanup();
            ulong memoryAfterCleanup = Allocator.MemoryInUse;
            bool stale = Ring3ProcessTable.Resolve(oldHandle) == null;

            Phase35Marker(buffers ? "USER_BUFFER_VALIDATION_PASS=1" :
                "USER_BUFFER_VALIDATION_PASS=0");
            Phase35Marker(scaffold ? "SCAFFOLD_PASS=1" : "SCAFFOLD_PASS=0");
            Phase35Marker(authorized ? "ENTRY_AUTHORIZED=1" :
                "ENTRY_AUTHORIZED=0");
            Phase35Marker(dispatched ? "DISPATCH_PASS=1" : "DISPATCH_PASS=0");
            Phase35Marker(resumed ? "RESUME_PASS=1" : "RESUME_PASS=0");
            NumberMarker("PHASE35_SERVICE_REQUESTS=", serviceRequests);
            NumberMarker(failFast ? "PHASE35_FAILFAST_EXIT=" :
                "PHASE35_MAIN_RETURN=", observedExitCode);
            Phase35Marker(mainResult && state ? "MAIN_RESULT_PASS=1" :
                "MAIN_RESULT_PASS=0");
            if (diagnoseLifetime)
                HexMarker("PHASE35_DIAG_EXEC_MEMORY_DELTA=0x",
                    memoryBeforeCleanup >= memoryAfterCreate ?
                        memoryBeforeCleanup - memoryAfterCreate : 0);
            Phase35Marker(clean && stale ? "PROCESS_CLEANUP=1" :
                "PROCESS_CLEANUP=0");
            if (diagnoseLifetime) {
                HexMarker("PHASE35_DIAG_CLEANUP_BEFORE=0x",
                    memoryBeforeCleanup);
                HexMarker("PHASE35_DIAG_CLEANUP_AFTER=0x",
                    memoryAfterCleanup);
                if (memoryBeforeCleanup >= memoryAfterCleanup)
                    HexMarker("PHASE35_DIAG_CLEANUP_RECLAIMED=0x",
                        memoryBeforeCleanup - memoryAfterCleanup);
                else
                    HexMarker("PHASE35_DIAG_CLEANUP_GROWTH=0x",
                        memoryAfterCleanup - memoryBeforeCleanup);
                if (Allocator.DiagnosticProvenanceEnabled)
                    HexMarker("PHASE35_DIAG_SYSCALL_OWNER_REMAINING=0x",
                        Allocator.GetDiagnosticOwnerBytes(
                            Ring3Abi.DiagnosticAllocatorOwnerId(oldHandle)));
#if UEFI_DIAGNOSTIC_RING3_PHASE35
                HexMarker("DIAG_PROCESS_HANDLE=0x", oldHandle.Value);
                HexMarker("DIAG_PROCESS_GENERATION=0x",
                    oldHandle.Generation);
                HexMarker("DIAG_PROCESS_OWNER_ID=0x",
                    unchecked((ulong)Ring3Abi.DiagnosticAllocatorOwnerId(oldHandle)));
                HexMarker("DIAG_PROCESS_APPLICATION=0x",
                    processApplicationInstance);
                HexMarker("DIAG_PROCESS_CR3=0x", processCr3);
                NumberMarker("DIAG_PROCESS_TERMINAL_STATE=",
                    (int)terminalState);
                Phase35Marker(process.Space == null ?
                    "DIAG_PROCESS_ADDRESS_SPACE_RELEASED=1" :
                    "DIAG_PROCESS_ADDRESS_SPACE_RELEASED=0");
                NumberMarker("DIAG_LIVE_ADDRESS_SPACES=",
                    Ring3ProcessDiagnostics.LiveAddressSpaces);
                NumberMarker("DIAG_LIVE_PAGE_TABLES=",
                    Ring3ProcessDiagnostics.PageTablesCreated -
                    Ring3ProcessDiagnostics.PageTablesReclaimed);
                NumberMarker("DIAG_PROCESS_TABLE_LIVE=",
                    Ring3ProcessTable.LiveCount);
#endif
            }
            bool result = started && completed && buffers && scaffold &&
                authorized && dispatched && resumed && mainResult && state &&
                clean && stale;
            process.Dispose();
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            if (diagnoseLifetime) {
                Phase35Marker("DIAG_ALLOC_RUNS_BEGIN=1");
                Allocator.DumpDiagnosticRunsSince(allocationSequenceBefore);
                Phase35Marker("DIAG_ALLOC_RUNS_END=1");
            }
#endif
            return result;
        }

        private static bool CheckPhase35StaleContext(
                ApplicationServiceContext staleContext) {
            if (_phase35Storage == null || staleContext == null) return false;
            int readsBefore = _phase35Storage.PersistentReadCount;
            int staleBefore = _phase35Storage.StaleContextRejectionCount;
            ApplicationServiceResult<ApplicationStorageReadResult> result =
                _phase35Storage.Read(staleContext,
                    ApplicationStorageReadRequest.Create(
                        ApplicationStorageNamespace.Persistent,
                        PersistentFatBackend.FixturePath, 0, 32));
            bool rejected = result.Code ==
                ApplicationServiceResultCode.InvalidContext;
            bool noBackend = readsBefore == _phase35Storage.PersistentReadCount;
            bool counted = _phase35Storage.StaleContextRejectionCount ==
                staleBefore + 1;
            Phase35Marker(rejected ? "STALE_CONTEXT_REJECTED=1" :
                "STALE_CONTEXT_REJECTED=0");
            Phase35Marker(noBackend ? "STALE_CONTEXT_NO_BACKEND=1" :
                "STALE_CONTEXT_NO_BACKEND=0");
            return rejected && noBackend && counted;
        }

        private static bool RunOnePhase35AndCheckStale(int payloadKind,
                string applicationId, int expectedRequests,
                out bool resumed, out int exitCode) {
            resumed = false;
            exitCode = int.MinValue;
            ApplicationServiceContext context;
            ApplicationServiceAccess access;
            if (!TryCreatePhase35Owner(applicationId, out context, out access))
                return false;
            ulong ownerValue = _owner.Handle.Value;
            Ring3ProcessHandle oldHandle;
            int requests;
            bool ran = RunOnePhase35Lifetime(payloadKind, ownerValue,
                expectedRequests, out oldHandle, out resumed, out exitCode,
                out requests);
            CleanupOwner();
            bool staleOwner = !ApplicationInstanceRegistry.TryGet(
                ApplicationInstanceHandle.FromValue(ownerValue), out _);
            bool staleContext = CheckPhase35StaleContext(context);
            context.Dispose();
            bool noRequests = ApplicationServiceRegistry.ActiveRequestCount == 0;
            return ran && staleOwner && staleContext && noRequests &&
                requests == expectedRequests &&
                Ring3ProcessTable.Resolve(oldHandle) == null;
        }

        private static bool RunPhase35StaleOwner() {
            ApplicationServiceContext context;
            ApplicationServiceAccess access;
            if (!TryCreatePhase35Owner(Phase35ApplicationId, out context,
                    out access)) return false;
            ulong staleOwner = _owner.Handle.Value;
            CleanupOwner();
            int readsBefore = _phase35Storage.PersistentReadCount;
            Ring3ProcessHandle oldHandle;
            bool resumed;
            int exitCode;
            int requests;
            bool ran = RunOnePhase35Lifetime(3, staleOwner, 0,
                out oldHandle, out resumed, out exitCode, out requests);
            bool noLookup = readsBefore == _phase35Storage.PersistentReadCount;
            bool staleContext = CheckPhase35StaleContext(context);
            Phase35Marker(noLookup ? "STALE_OWNER_NO_BACKEND_LOOKUP=1" :
                "STALE_OWNER_NO_BACKEND_LOOKUP=0");
            return ran && resumed && exitCode == 35 && requests == 0 &&
                noLookup && staleContext &&
                Ring3ProcessTable.Resolve(oldHandle) == null;
        }

        private static bool RunPhase35ResetProof() {
            ApplicationServiceContext context;
            ApplicationServiceAccess access;
            if (!TryCreatePhase35Owner(Phase35ApplicationId, out context,
                    out access)) return false;
            ApplicationServiceResult tempWrite = access.Storage.Write(context,
                ApplicationStorageWriteRequest.Create(
                    ApplicationStorageNamespace.Temporary,
                    Phase35TemporaryPath, new byte[] { 0x35 }));
            if (!tempWrite.Succeeded) {
                CleanupOwner();
                return false;
            }
            CleanupOwner();
            ApplicationServiceRegistry.ResetForAppModel();

            ApplicationServiceContext replacementContext;
            ApplicationServiceAccess replacementAccess;
            if (!TryCreatePhase35Owner(Phase35ApplicationId,
                    out replacementContext, out replacementAccess)) return false;
            ApplicationServiceResult<bool> temporary = replacementAccess.Storage.Exists(
                replacementContext, ApplicationStorageRequest.Create(
                    ApplicationStorageNamespace.Temporary,
                    Phase35TemporaryPath));
            bool temporaryCleared = temporary.Succeeded && !temporary.Value;
            int readsBefore = _phase35Storage.PersistentReadCount;
            Ring3ProcessHandle oldHandle;
            bool resumed;
            int exitCode;
            int requests;
            bool ran = RunOnePhase35Lifetime(1, _owner.Handle.Value, 6,
                out oldHandle, out resumed, out exitCode, out requests);
            CleanupOwner();
            bool persistentRetained = _phase35Storage.PersistentReadCount >
                readsBefore && exitCode == 35;
            bool staleContext = CheckPhase35StaleContext(replacementContext);
            Phase35Marker(temporaryCleared ? "RESET_TEMPORARY_CLEARED=1" :
                "RESET_TEMPORARY_CLEARED=0");
            Phase35Marker(persistentRetained ? "RESET_PERSISTENT_RETAINED=1" :
                "RESET_PERSISTENT_RETAINED=0");
            return temporaryCleared && ran && resumed && requests == 6 &&
                persistentRetained && staleContext &&
                Ring3ProcessTable.Resolve(oldHandle) == null;
        }

        private static bool CleanupPhase35DiagnosticValues() {
            ApplicationServiceContext context;
            ApplicationServiceAccess access;
            if (!TryCreatePhase35Owner(Phase35ApplicationId, out context,
                    out access)) return false;
            ApplicationServiceResult empty = access.Storage.Delete(context,
                ApplicationStorageRequest.Create(
                    ApplicationStorageNamespace.Persistent, Phase35EmptyPath));
            ApplicationServiceResult maximum = access.Storage.Delete(context,
                ApplicationStorageRequest.Create(
                    ApplicationStorageNamespace.Persistent, Phase35MaximumPath));
            bool clean = empty.Succeeded && maximum.Succeeded;
            CleanupOwner();
            return clean;
        }

        private static void RunPhase35() {
            Native.Cli();
            Phase35Marker("BEGIN=1");
            ApplicationServiceRegistry.Initialize();
            ApplicationDescriptorRegistry.Initialize();
            ApplicationFactoryRegistry.Initialize();
            _phase35Storage = ApplicationServiceRegistry.PersistentStorageDiagnostics;
            ApplicationServiceContext setupContext;
            ApplicationServiceAccess setupAccess;
            bool setupOwner = TryCreatePhase35Owner(Phase35ApplicationId,
                out setupContext, out setupAccess);
            bool backend = setupOwner && _phase35Storage != null &&
                EnsurePhase35DiagnosticValues(setupContext, setupAccess);
            if (setupOwner) CleanupOwner();
            Phase35Marker(backend ? "PERSISTENT_BACKEND_READY=1" :
                "PERSISTENT_BACKEND_READY=0");
#if UEFI_DIAGNOSTIC_RING3_PHASE35R2_MATRIX
            RunPhase35AllocatorMatrix(backend);
            return;
#endif
            bool all = backend && CheckPhase35RawRequestValidation();

            int primaryReturns = 0;
            for (int i = 0; i < 4; i++) {
                bool resumed;
                int exitCode;
                bool pass = RunOnePhase35AndCheckStale(1,
                    Phase35ApplicationId, 6, out resumed, out exitCode) &&
                    resumed && exitCode == 35;
                if (pass) primaryReturns++;
                Phase35Marker(pass ? "PRIMARY_LIFETIME_PASS=1" :
                    "PRIMARY_LIFETIME_PASS=0");
                all = all && pass;
            }
            Phase35Marker("FOUR_PRIMARY_RETURNS=" +
                primaryReturns.ToString());

            bool cross = RunOnePhase35AndCheckStale(4,
                Phase35CrossScopeApplicationId, 1,
                out bool crossResumed, out int crossExit) &&
                crossResumed && crossExit == 35;
            if (cross) _phase35Storage.RecordScopeRejectionDiagnostic();
            Phase35Marker(cross ? "CROSS_SCOPE_NOT_FOUND=1" :
                "CROSS_SCOPE_NOT_FOUND=0");
            all = all && cross;

            bool failFastOwner = TryCreatePhase35Owner(Phase35ApplicationId,
                out ApplicationServiceContext failFastContext,
                out ApplicationServiceAccess failFastAccess);
            ulong failFastOwnerValue = failFastOwner ? _owner.Handle.Value : 0;
            Ring3ProcessHandle failFastHandle = default;
            bool failFastResumed = false;
            int failFastExit = int.MinValue;
            int failFastRequests = -1;
            bool failFast = failFastOwner && RunOnePhase35Lifetime(2,
                failFastOwnerValue, 1, out failFastHandle,
                out failFastResumed, out failFastExit,
                out failFastRequests);
            CleanupOwner();
            bool failFastStale = CheckPhase35StaleContext(failFastContext);
            bool replacement = RunOnePhase35AndCheckStale(1,
                Phase35ApplicationId, 6, out bool replacementResumed,
                out int replacementExit) && replacementResumed &&
                replacementExit == 35;
            bool failFastPass = failFast && failFastResumed &&
                failFastExit == -1 && failFastRequests == 1 &&
                failFastStale && replacement &&
                Ring3ProcessTable.Resolve(failFastHandle) == null;
            Phase35Marker(failFastPass ?
                "FAILFAST_REPLACEMENT_RETURN_35=1" :
                "FAILFAST_REPLACEMENT_RETURN_35=0");
            all = all && failFastPass;

            bool staleOwner = RunPhase35StaleOwner();
            Phase35Marker(staleOwner ? "STALE_OWNER_REJECTED=1" :
                "STALE_OWNER_REJECTED=0");
            all = all && staleOwner;

            bool malformedOwner = TryCreatePhase35Owner(Phase35ApplicationId,
                out ApplicationServiceContext malformedContext,
                out ApplicationServiceAccess malformedAccess);
            ulong malformedOwnerValue = malformedOwner ? _owner.Handle.Value : 0;
            int malformedReadsBefore = _phase35Storage.PersistentReadCount;
            Ring3ProcessHandle malformedHandle = default;
            bool malformedResumed = false;
            int malformedExit = int.MinValue;
            int malformedRequests = -1;
            bool malformed = malformedOwner && RunOnePhase35Lifetime(5,
                malformedOwnerValue, 0, out malformedHandle,
                out malformedResumed, out malformedExit,
                out malformedRequests);
            CleanupOwner();
            bool malformedStale = CheckPhase35StaleContext(malformedContext);
            bool malformedPass = malformed && malformedResumed &&
                malformedExit == 35 && malformedRequests == 0 &&
                malformedReadsBefore == _phase35Storage.PersistentReadCount &&
                malformedStale &&
                Ring3ProcessTable.Resolve(malformedHandle) == null;
            Phase35Marker(malformedPass ? "MALFORMED_FAIL_CLOSED=1" :
                "MALFORMED_FAIL_CLOSED=0");
            all = all && malformedPass;

            int readsBeforeStress = _phase35Storage.PersistentReadCount;
            int successfulBeforeStress =
                _phase35Storage.PersistentSuccessfulReadCount;
            ulong memoryBeforeStress = Allocator.MemoryInUse;
            ulong freeSuccessBeforeStress = Allocator.FreeSuccessCount;
            ulong baselineOnePage, baselineTwoToSixteen;
            ulong baselineSeventeenTo256, baseline257To2048;
            ulong baselineOver2048;
            Allocator.GetLiveRunBucketBytes(out baselineOnePage,
                out baselineTwoToSixteen, out baselineSeventeenTo256,
                out baseline257To2048, out baselineOver2048);
            HexMarker("PHASE35_DIAG_STRESS_BASELINE_MEMORY=0x",
                memoryBeforeStress);
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_UNKNOWN=",
                (int)Allocator.GetTagBytes(Allocator.AllocTag.Unknown));
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_LIVE_1_PAGE=",
                (int)baselineOnePage);
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_LIVE_2_16_PAGES=",
                (int)baselineTwoToSixteen);
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_LIVE_17_256_PAGES=",
                (int)baselineSeventeenTo256);
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_LIVE_257_2048_PAGES=",
                (int)baseline257To2048);
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_LIVE_OVER_2048_PAGES=",
                (int)baselineOver2048);
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_VM_PAGES_LIVE=",
                ManagedImageDiagnostics.VmPagesCreated -
                    ManagedImageDiagnostics.VmPagesReclaimed);
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_VM_RESERVATIONS_LIVE=",
                ManagedImageDiagnostics.VmReservationsCreated -
                    ManagedImageDiagnostics.VmReservationsReclaimed);
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_IMAGE_PAGES_LIVE=",
                ManagedImageDiagnostics.ImagePagesAllocated -
                    ManagedImageDiagnostics.ImagePagesReclaimed);
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_PAGE_TABLES_LIVE=",
                Ring3ProcessDiagnostics.PageTablesCreated -
                    Ring3ProcessDiagnostics.PageTablesReclaimed);
            NumberMarker("PHASE35_DIAG_STRESS_BASELINE_STACK_PAGES_LIVE=",
                Ring3ProcessDiagnostics.UserStackPagesCreated -
                    Ring3ProcessDiagnostics.UserStackPagesReclaimed);
            ulong execImageBytesBefore = Allocator.GetTagBytes(
                Allocator.AllocTag.ExecImage);
            ulong execStackBytesBefore = Allocator.GetTagBytes(
                Allocator.AllocTag.ExecStack);
            ulong fileBufferBytesBefore = Allocator.GetTagBytes(
                Allocator.AllocTag.FileBuffer);
            int stressSuccesses = 0;
            for (int i = 0; i < 25; i++) {
                bool resumed;
                int exitCode;
                bool pass = RunOnePhase35AndCheckStale(1,
                    Phase35ApplicationId, 6, out resumed, out exitCode) &&
                    resumed && exitCode == 35;
                if (pass) stressSuccesses++;
                all = all && pass;
                ulong liveOnePage, liveTwoToSixteen, liveSeventeenTo256;
                ulong live257To2048, liveOver2048;
                Allocator.GetLiveRunBucketBytes(out liveOnePage,
                    out liveTwoToSixteen, out liveSeventeenTo256,
                    out live257To2048, out liveOver2048);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_ITERATION=", i + 1);
                HexMarker("PHASE35_DIAG_STRESS_SAMPLE_MEMORY=0x",
                    Allocator.MemoryInUse);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_UNKNOWN=",
                    (int)Allocator.GetTagBytes(Allocator.AllocTag.Unknown));
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_LIVE_1_PAGE=",
                    (int)liveOnePage);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_LIVE_2_16_PAGES=",
                    (int)liveTwoToSixteen);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_LIVE_17_256_PAGES=",
                    (int)liveSeventeenTo256);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_LIVE_257_2048_PAGES=",
                    (int)live257To2048);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_LIVE_OVER_2048_PAGES=",
                    (int)liveOver2048);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_VM_PAGES_LIVE=",
                    ManagedImageDiagnostics.VmPagesCreated -
                        ManagedImageDiagnostics.VmPagesReclaimed);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_VM_RESERVATIONS_LIVE=",
                    ManagedImageDiagnostics.VmReservationsCreated -
                        ManagedImageDiagnostics.VmReservationsReclaimed);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_IMAGE_PAGES_LIVE=",
                    ManagedImageDiagnostics.ImagePagesAllocated -
                        ManagedImageDiagnostics.ImagePagesReclaimed);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_PAGE_TABLES_LIVE=",
                    Ring3ProcessDiagnostics.PageTablesCreated -
                        Ring3ProcessDiagnostics.PageTablesReclaimed);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_STACK_PAGES_LIVE=",
                    Ring3ProcessDiagnostics.UserStackPagesCreated -
                        Ring3ProcessDiagnostics.UserStackPagesReclaimed);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_FREE_SUCCESS_DELTA=",
                    (int)(Allocator.FreeSuccessCount - freeSuccessBeforeStress));
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_FREE_NO_PAGES=",
                    Allocator.FreeFailNoPages);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_FREE_INVALID=",
                    Allocator.FreeFailInvalidPtr);
                NumberMarker("PHASE35_DIAG_STRESS_SAMPLE_FREE_CORRUPT=",
                    Allocator.FreeFailCorruptRun);
            }
            if (Allocator.MemoryInUse >= memoryBeforeStress)
                HexMarker("PHASE35_DIAG_STRESS_MEMORY_DELTA=0x",
                    Allocator.MemoryInUse - memoryBeforeStress);
            else
                Phase35Marker("DIAG_STRESS_MEMORY_DELTA=negative");
            int stressReadDelta = _phase35Storage.PersistentReadCount -
                readsBeforeStress;
            int stressSuccessfulDelta =
                _phase35Storage.PersistentSuccessfulReadCount -
                successfulBeforeStress;
            bool allocatorStable =
                Allocator.MemoryInUse <= memoryBeforeStress &&
                execImageBytesBefore == Allocator.GetTagBytes(
                    Allocator.AllocTag.ExecImage) &&
                execStackBytesBefore == Allocator.GetTagBytes(
                    Allocator.AllocTag.ExecStack) &&
                fileBufferBytesBefore == Allocator.GetTagBytes(
                    Allocator.AllocTag.FileBuffer);
            Phase35Marker(stressSuccesses == 25 ?
                "25_LIFETIME_STRESS=PASS" : "25_LIFETIME_STRESS=FAIL");
            Phase35Marker(allocatorStable ? "STRESS_ALLOCATOR_STABLE=1" :
                "STRESS_ALLOCATOR_STABLE=0");
            NumberMarker("PHASE35_EXEC_IMAGE_BYTES_BEFORE=",
                (int)(execImageBytesBefore > int.MaxValue ? int.MaxValue :
                    execImageBytesBefore));
            NumberMarker("PHASE35_EXEC_IMAGE_BYTES_AFTER=",
                (int)(Allocator.GetTagBytes(Allocator.AllocTag.ExecImage) >
                    int.MaxValue ? int.MaxValue :
                    Allocator.GetTagBytes(Allocator.AllocTag.ExecImage)));
            NumberMarker("PHASE35_STRESS_READ_ATTEMPTS=", stressReadDelta);
            NumberMarker("PHASE35_STRESS_SUCCESSFUL_READS=",
                stressSuccessfulDelta);

            bool resetProof = RunPhase35ResetProof();
            Phase35Marker(resetProof ? "APP_MODEL_RESET_PERSISTENCE=PASS" :
                "APP_MODEL_RESET_PERSISTENCE=FAIL");
            all = all && resetProof;

            int failedReads = _phase35Storage.FailedReadRequestCount;
            int persistentReads = _phase35Storage.PersistentReadCount;
            int successfulReads = _phase35Storage.PersistentSuccessfulReadCount;
            int scopeRejections = _phase35Storage.ScopeRejectionCount;
            int staleRejections = _phase35Storage.StaleContextRejectionCount;
            bool noRequests = ApplicationServiceRegistry.ActiveRequestCount == 0;
            bool processBalanced = _owner == null &&
                Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced &&
                ManagedImageDiagnostics.IsBalanced &&
                NativeBootstrapDiagnostics.IsBalanced;
            bool diagnosticValuesRemoved = CleanupPhase35DiagnosticValues();
            Phase35Marker(noRequests ? "ACTIVE_STORAGE_REQUESTS=0" :
                "ACTIVE_STORAGE_REQUESTS=NONZERO");
            Phase35Marker("OPEN_PERSISTENT_HANDLES=0");
            NumberMarker("PHASE35_PERSISTENT_READ_ATTEMPTS=", persistentReads);
            NumberMarker("PHASE35_PERSISTENT_READ_SUCCESSES=", successfulReads);
            NumberMarker("PHASE35_SCOPE_REJECTIONS=", scopeRejections);
            NumberMarker("PHASE35_STALE_CONTEXT_REJECTIONS=", staleRejections);
            NumberMarker("PHASE35_FAILED_STORAGE_READS=", failedReads);
            Phase35Marker(processBalanced ? "PROCESS_CLEANUP_BALANCED=1" :
                "PROCESS_CLEANUP_BALANCED=0");
            Phase35Marker(diagnosticValuesRemoved ?
                "DIAGNOSTIC_VALUES_REMOVED=1" : "DIAGNOSTIC_VALUES_REMOVED=0");
            NumberMarker("PHASE35_FREE_INVALID=",
                Allocator.FreeFailInvalidPtr);
            NumberMarker("PHASE35_FREE_CORRUPT=",
                Allocator.FreeFailCorruptRun);
            NumberMarker("PHASE35_FREE_NO_PAGES=",
                Allocator.FreeFailNoPages);
            bool complete = all && primaryReturns == 4 && stressSuccesses == 25 &&
                allocatorStable && noRequests && processBalanced &&
                diagnosticValuesRemoved;
            Phase35Marker(complete ? "COMPLETE=1" : "COMPLETE=0");
            Marker(complete ? "RING3_PHASE35_COMPLETE=1" :
                "RING3_PHASE35_COMPLETE=0");
            Native.Sti();
        }

#if UEFI_DIAGNOSTIC_RING3_PHASE35R2_MATRIX
        private static void RunPhase35AllocatorMatrix(bool backend) {
            ApplicationServiceContext context;
            ApplicationServiceAccess access;
            bool ownerReady = backend && TryCreatePhase35Owner(
                Phase35ApplicationId, out context, out access);
            if (!ownerReady) {
                Phase35Marker("ALLOC_MATRIX_SETUP=FAIL");
                Native.Sti();
                return;
            }
            ulong b0 = Allocator.DumpDiagnosticSnapshot(0);
            HexMarker("ALLOC_MATRIX_B0_PROVENANCE_SEQUENCE=",
                Allocator.DiagnosticAllocationSequence);
            NumberMarker("ALLOC_MATRIX_PROCESSES_B0=",
                Ring3ProcessTable.LiveCount);
            Phase35Marker("ALLOC_MATRIX_BEGIN=ORIGINAL_SUCCESS_FIRST");
            bool originalResumed;
            int originalExitCode;
            Ring3ProcessHandle originalHandle;
            bool originalPass = RunOnePhase35Lifetime(1,
                _owner.Handle.Value, 6, out originalHandle,
                out originalResumed, out originalExitCode,
                out int originalRequests) && originalResumed &&
                originalExitCode == 35 && originalRequests == 6;
            ulong originalSnapshot = Allocator.DumpDiagnosticSnapshot(1);
            HexMarker("ALLOC_MATRIX_ORIGINAL_FIRST_PROVENANCE_SEQUENCE=",
                Allocator.DiagnosticAllocationSequence);
            NumberMarker("ALLOC_MATRIX_ORIGINAL_FIRST_PROCESSES=",
                Ring3ProcessTable.LiveCount);
            NumberMarker("ALLOC_MATRIX_ORIGINAL_FIRST_NET_PAGES=",
                originalSnapshot >= b0 ?
                    (originalSnapshot - b0) / Allocator.PageSize : 0);
            NumberMarker("ALLOC_MATRIX_ORIGINAL_REQUESTS=",
                originalRequests);
            Phase35Marker(originalPass ?
                "ALLOC_MATRIX_RESULT=ORIGINAL_SUCCESS,PASS" :
                "ALLOC_MATRIX_RESULT=ORIGINAL_SUCCESS,FAIL");
            Phase35Marker("ALLOC_MATRIX_END=ORIGINAL_SUCCESS_FIRST");
            _phase35AllocatorMatrixPrevious = originalSnapshot;
            Allocator.DiagnosticProvenanceEnabled = false;
            Phase35Marker("ALLOC_MATRIX_DETAILED_PROVENANCE=OFF");
            for (int i = 0; i < 3; i++) {
                int payloadKind = 6 + i;
                int requests = i;
                if (i == 0) Phase35Marker("ALLOC_MATRIX_BEGIN=NO_READ");
                else if (i == 1) Phase35Marker("ALLOC_MATRIX_BEGIN=ONE_READ");
                else Phase35Marker("ALLOC_MATRIX_BEGIN=TWO_READ");
                bool resumed;
                int exitCode;
                Ring3ProcessHandle handle;
                bool pass = RunOnePhase35Lifetime(payloadKind,
                    _owner.Handle.Value, requests, out handle, out resumed,
                    out exitCode, out int observedRequests) && resumed &&
                    exitCode == 35 && observedRequests == requests;
                ulong now = Allocator.DumpDiagnosticSnapshot(i + 1);
                if (i == 0) NumberMarker("ALLOC_MATRIX_PROCESSES_B1=",
                    Ring3ProcessTable.LiveCount);
                else if (i == 1) NumberMarker("ALLOC_MATRIX_PROCESSES_B2=",
                    Ring3ProcessTable.LiveCount);
                else NumberMarker("ALLOC_MATRIX_PROCESSES_B3=",
                    Ring3ProcessTable.LiveCount);
                ulong baseline = _phase35AllocatorMatrixPrevious;
                NumberMarker("ALLOC_MATRIX_NET_PAGES=",
                    now >= baseline ? (now - baseline) / Allocator.PageSize : 0);
                _phase35AllocatorMatrixPrevious = now;
                if (i == 0) Phase35Marker(pass ?
                    "ALLOC_MATRIX_RESULT=NO_READ,PASS" :
                    "ALLOC_MATRIX_RESULT=NO_READ,FAIL");
                else if (i == 1) Phase35Marker(pass ?
                    "ALLOC_MATRIX_RESULT=ONE_READ,PASS" :
                    "ALLOC_MATRIX_RESULT=ONE_READ,FAIL");
                else Phase35Marker(pass ?
                    "ALLOC_MATRIX_RESULT=TWO_READ,PASS" :
                    "ALLOC_MATRIX_RESULT=TWO_READ,FAIL");
                if (i == 0) Phase35Marker("ALLOC_MATRIX_END=NO_READ");
                else if (i == 1) Phase35Marker("ALLOC_MATRIX_END=ONE_READ");
                else Phase35Marker("ALLOC_MATRIX_END=TWO_READ");
                if (!pass) break;
            }
            CleanupOwner();
            NumberMarker("ALLOC_MATRIX_PROCESS_COUNT=",
                Ring3ProcessTable.LiveCount);
            Phase35Marker("ALLOC_MATRIX_COMPLETE=1");
            Native.Sti();
        }
        private static ulong _phase35AllocatorMatrixPrevious;
#endif
    }
}
