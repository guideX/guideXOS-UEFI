using guideXOS.GUI;
using guideXOS.OS;
using System;

namespace guideXOS.Misc {
    internal static unsafe partial class Ring3Proof {
        private static bool _phase34Scheduled;
        private static CSharpApplicationResourceService _phase34ResourceService;
        private const string Phase34RequesterId = "selftest.phase8.services";
        private const string Phase34CrossScopeRequesterId =
            "selftest.phase34.crossscope";
        private const string Phase34ResourceName = "diagnostic.fixture";

        private static void Phase34Marker(string value) {
            Marker("PHASE34_" + value);
        }

        internal static void SchedulePhase34() {
            if (_phase34Scheduled) return;
            _phase34Scheduled = true;
            Phase34Marker("SCHEDULED=1");
            new Thread(&RunPhase34, 131072).Start(0);
        }

        private static bool TryCreatePhase34Owner(string applicationId,
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
                Phase34Marker("REQUESTER_CREATED=0");
                return false;
            }
            _owner = instance;
            _ownerCreated = true;
            ApplicationServiceResult contextResult;
            if (!ApplicationServiceRegistry.TryCreateContextAndAccess(
                    instance.Handle, out context, out access,
                    out contextResult) || context == null || access == null) {
                CleanupOwner();
                Phase34Marker("REQUESTER_CONTEXT_CREATED=0");
                return false;
            }
            _phase34ResourceService = access.Resources as
                CSharpApplicationResourceService;
            Phase34Marker("REQUESTER_CREATED=1");
            Phase34Marker(_phase34ResourceService != null
                ? "PHASE10_BACKEND_RESOLVED=1" : "PHASE10_BACKEND_RESOLVED=0");
            if (_phase34ResourceService == null) {
                CleanupOwner();
                return false;
            }
            return true;
        }

        private static bool RunOnePhase34Lifetime(int payloadKind,
                ulong owningApplicationInstance, int expectedRequests,
                int expectedLookups, out Ring3ProcessHandle oldHandle,
                out bool resumed, out bool mainResult) {
            oldHandle = default(Ring3ProcessHandle);
            resumed = false;
            mainResult = false;
            Native.Cli();
            string failure;
            Ring3Process process;
            if (!Ring3Process.TryCreateManagedResourceEntry(
                    owningApplicationInstance, payloadKind, out process,
                    out failure) || process == null) {
                Native.Sti();
                Phase34Marker("PROCESS_CREATE_FAILED=1");
                if (failure != null)
                    Phase34Marker("PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }

            oldHandle = process.Handle;
            bool wireValidation = CheckPhase34UserBufferValidation(process);
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool authorized = process.ManagedImage != null &&
                process.ManagedImage.TryEnterManagedEntry();
            int lookupsBefore = _phase34ResourceService == null ? -1 :
                _phase34ResourceService.LookupCount;
            bool started = wireValidation && scaffold && authorized &&
                process.StartManagedBootstrap();
            if (started) Native.Sti();
            int spins = 0;
            while (started && !process.IsTerminal && spins++ < 6000000)
                Native.Hlt();

            bool completed = process.IsTerminal;
            bool dispatched = process.SchedulerDispatches >= 1 &&
                process.SchedulerCr3Valid && process.SchedulerRsp0Valid;
            resumed = process.TimerPreemptions > 0 &&
                process.SchedulerDispatches >= 2 && process.UserRspPreserved;
            int serviceRequests = process.ServiceRequestsSucceeded;
            int observedExitCode = process.ExitCode;
            bool failFast = payloadKind == 2;
            mainResult = failFast
                ? observedExitCode == -1 && serviceRequests == 2
                : process.BootstrapResultSucceeded &&
                    process.BootstrapReturnCode == 34 && observedExitCode == 34;
            bool state = process.State == Ring3ProcessState.Exiting ||
                process.State == Ring3ProcessState.Exited;
            int lookupDelta = _phase34ResourceService == null ||
                    lookupsBefore < 0 ? -1 :
                _phase34ResourceService.LookupCount - lookupsBefore;
            bool lookupPass = lookupDelta == expectedLookups;
            bool clean = process.Cleanup();
            bool stale = Ring3ProcessTable.Resolve(oldHandle) == null;

            Phase34Marker(wireValidation ? "RAW_VALIDATION_PASS=1" :
                "RAW_VALIDATION_PASS=0");
            Phase34Marker(scaffold ? "SCAFFOLD_PASS=1" : "SCAFFOLD_PASS=0");
            Phase34Marker(authorized ? "ENTRY_AUTHORIZED=1" : "ENTRY_AUTHORIZED=0");
            Phase34Marker(dispatched ? "DISPATCH_PASS=1" : "DISPATCH_PASS=0");
            Phase34Marker(resumed ? "RESUME_PASS=1" : "RESUME_PASS=0");
            NumberMarker("PHASE34_SERVICE_REQUESTS=", serviceRequests);
            NumberMarker("PHASE34_RESOURCE_LOOKUP_DELTA=", lookupDelta);
            Phase34Marker(failFast ? "FAILFAST_EXIT=" + observedExitCode.ToString() :
                "MAIN_RETURN=" + observedExitCode.ToString());
            Phase34Marker(serviceRequests == expectedRequests
                ? "SERVICE_COUNT_PASS=1" : "SERVICE_COUNT_PASS=0");
            Phase34Marker(lookupPass ? "LOOKUP_COUNT_PASS=1" :
                "LOOKUP_COUNT_PASS=0");
            Phase34Marker(mainResult && state
                ? (failFast ? "FAILFAST_REQUESTER_DIED=1" : "MAIN_RESULT_PASS=1")
                : (failFast ? "FAILFAST_REQUESTER_DIED=0" : "MAIN_RESULT_PASS=0"));
            Phase34Marker(clean && stale ? "PROCESS_CLEANUP=1" :
                "PROCESS_CLEANUP=0");
            return started && completed && wireValidation && scaffold &&
                authorized && dispatched && resumed &&
                serviceRequests == expectedRequests && lookupPass &&
                mainResult && state && clean && stale;
        }

        private static bool CheckPhase34UserBufferValidation(
                Ring3Process process) {
            bool zeroRejected = !Ring3Abi.ValidateResourceBufferForPhase34Proof(
                process, 0, 1);
            bool unmappedRejected =
                !Ring3Abi.ValidateResourceBufferForPhase34Proof(process,
                    0x0000600000000000UL, 53);
            bool overflowRejected =
                !Ring3Abi.ValidateResourceBufferForPhase34Proof(process,
                    0xFFFFFFFFFFFFFFFFUL - 5UL, 53);
            Phase34Marker(zeroRejected ? "ZERO_BUFFER_REJECTED=1" :
                "ZERO_BUFFER_REJECTED=0");
            Phase34Marker(unmappedRejected ? "UNMAPPED_BUFFER_REJECTED=1" :
                "UNMAPPED_BUFFER_REJECTED=0");
            Phase34Marker(overflowRejected ? "OVERFLOW_BUFFER_REJECTED=1" :
                "OVERFLOW_BUFFER_REJECTED=0");
            return zeroRejected && unmappedRejected && overflowRejected;
        }

        private static bool CheckPhase34KernelValidation() {
            Ring3ResourceRequest request = default(Ring3ResourceRequest);
            request.StructureVersion = (uint)Ring3Abi.AbiVersion;
            request.ServiceId = Ring3Abi.ResourcesService;
            request.OperationId = Ring3Abi.ResourceMetadataOperation;
            request.RequestLength = (uint)sizeof(Ring3ResourceRequest);
            request.ResourceNameLength = (uint)Phase34ResourceName.Length;
            request.ResponseCapacity = (uint)sizeof(Ring3ResourceResponse);
            request.ResponseBuffer = 0x0000401100004000UL;
            byte* key = request.ResourceName;
            for (int i = 0; i < Phase34ResourceName.Length; i++)
                key[i] = (byte)Phase34ResourceName[i];

            bool validAccepted =
                Ring3Abi.ValidateResourceRequestForPhase34Proof(&request) ==
                    Ring3Abi.Success;
            request.ResourceNameLength = 0;
            bool emptyRejected =
                Ring3Abi.ValidateResourceRequestForPhase34Proof(&request) ==
                    Ring3Abi.InvalidRequest;
            request.ResourceNameLength =
                (uint)ApplicationResourceRequest.MaxResourceKeyLength + 1U;
            bool oversizeRejected =
                Ring3Abi.ValidateResourceRequestForPhase34Proof(&request) ==
                    Ring3Abi.InvalidRequest;
            request.ResourceNameLength = 10;
            request.ResourceName[0] = (byte)'.';
            request.ResourceName[1] = (byte)'.';
            request.ResourceName[2] = (byte)'/';
            request.ResourceName[3] = (byte)'o';
            request.ResourceName[4] = (byte)'u';
            request.ResourceName[5] = (byte)'t';
            request.ResourceName[6] = (byte)'s';
            request.ResourceName[7] = (byte)'i';
            request.ResourceName[8] = (byte)'d';
            request.ResourceName[9] = (byte)'e';
            bool rawPathRejected =
                Ring3Abi.ValidateResourceRequestForPhase34Proof(&request) ==
                    Ring3Abi.InvalidRequest;

            request.ResourceNameLength = (uint)Phase34ResourceName.Length;
            for (int i = 0; i < Phase34ResourceName.Length; i++)
                request.ResourceName[i] = (byte)Phase34ResourceName[i];
            request.OperationId = 99;
            bool invalidOperationRejected =
                Ring3Abi.ValidateResourceRequestForPhase34Proof(&request) ==
                    Ring3Abi.InvalidRequest;
            request.OperationId = Ring3Abi.ResourceMetadataOperation;
            request.RequestLength--;
            bool badSizeRejected =
                Ring3Abi.ValidateResourceRequestForPhase34Proof(&request) ==
                    Ring3Abi.InvalidRequest;

            request.RequestLength = (uint)sizeof(Ring3ResourceRequest);
            request.OperationId = Ring3Abi.ResourceReadOperation;
            request.ExpectedResourceLength = 53;
            request.DataBuffer = 0x0000401100005000UL;
            request.DataCapacity = 53;
            bool exactCapacityAccepted =
                Ring3Abi.ValidateResourceRequestForPhase34Proof(&request) ==
                    Ring3Abi.Success;
            request.DataCapacity = 52;
            bool shortCapacityRejected =
                Ring3Abi.ValidateResourceRequestForPhase34Proof(&request) ==
                    Ring3Abi.InvalidRequest;
            request.DataCapacity = 53;
            request.ExpectedResourceLength =
                (ulong)ApplicationResourceReadRequest.MaxChunkLength + 1UL;
            request.DataCapacity = (uint)request.ExpectedResourceLength;
            bool oversizeReadRejected =
                Ring3Abi.ValidateResourceRequestForPhase34Proof(&request) ==
                    Ring3Abi.InvalidRequest;

            int lookupsBefore = _phase34ResourceService == null ? -1 :
                _phase34ResourceService.LookupCount;
            int lookupsAfter = _phase34ResourceService == null ? -2 :
                _phase34ResourceService.LookupCount;
            bool noBackend = lookupsBefore == lookupsAfter;
            Phase34Marker(validAccepted ? "RAW_VALID_ACCEPTED=1" :
                "RAW_VALID_ACCEPTED=0");
            Phase34Marker(emptyRejected ? "KERNEL_EMPTY_KEY_REJECTED=1" :
                "KERNEL_EMPTY_KEY_REJECTED=0");
            Phase34Marker(oversizeRejected ? "KERNEL_OVERSIZE_KEY_REJECTED=1" :
                "KERNEL_OVERSIZE_KEY_REJECTED=0");
            Phase34Marker(rawPathRejected ? "KERNEL_PATHLIKE_KEY_REJECTED=1" :
                "KERNEL_PATHLIKE_KEY_REJECTED=0");
            Phase34Marker(invalidOperationRejected ?
                "KERNEL_INVALID_OPERATION_REJECTED=1" :
                "KERNEL_INVALID_OPERATION_REJECTED=0");
            Phase34Marker(badSizeRejected ? "KERNEL_BAD_SIZE_REJECTED=1" :
                "KERNEL_BAD_SIZE_REJECTED=0");
            Phase34Marker(exactCapacityAccepted ?
                "EXACT_CAPACITY_ACCEPTED=1" : "EXACT_CAPACITY_ACCEPTED=0");
            Phase34Marker(shortCapacityRejected ?
                "SHORT_CAPACITY_REJECTED=1" : "SHORT_CAPACITY_REJECTED=0");
            Phase34Marker(oversizeReadRejected ?
                "KERNEL_OVERSIZE_READ_REJECTED=1" :
                "KERNEL_OVERSIZE_READ_REJECTED=0");
            Phase34Marker(noBackend ? "MALFORMED_NO_BACKEND_LOOKUP=1" :
                "MALFORMED_NO_BACKEND_LOOKUP=0");
            return validAccepted && emptyRejected && oversizeRejected &&
                rawPathRejected && invalidOperationRejected && badSizeRejected &&
                exactCapacityAccepted && shortCapacityRejected &&
                oversizeReadRejected && noBackend;
        }

        private static bool CheckPhase34StaleRequester(ulong ownerValue,
                ApplicationServiceContext context,
                ApplicationServiceAccess access,
                Ring3ProcessHandle processHandle) {
            CleanupOwner();
            ApplicationInstance ignored;
            bool staleOwner = ownerValue != 0 &&
                !ApplicationInstanceRegistry.TryGet(
                    ApplicationInstanceHandle.FromValue(ownerValue), out ignored);
            ApplicationServiceResult validation;
            bool staleContext = context != null &&
                !ApplicationServiceRegistry.TryValidateContext(context,
                    ApplicationServiceId.Resources, out ignored,
                    out validation) &&
                validation.Code == ApplicationServiceResultCode.InvalidContext;
            int lookupsBefore = _phase34ResourceService == null ? -1 :
                _phase34ResourceService.LookupCount;
            ApplicationServiceResult<ApplicationResourceMetadata> metadata =
                context != null && access != null && access.Resources != null
                    ? access.Resources.GetMetadata(context,
                        ApplicationResourceRequest.Create(Phase34ResourceName))
                    : null;
            ApplicationServiceResult<ApplicationResourceReadResult> read =
                context != null && access != null && access.Resources != null
                    ? access.Resources.Read(context,
                        ApplicationResourceReadRequest.Create(
                            Phase34ResourceName, 0, 1))
                    : null;
            int lookupsAfter = _phase34ResourceService == null ? -2 :
                _phase34ResourceService.LookupCount;
            bool staleRequests = metadata != null && read != null &&
                metadata.Code == ApplicationServiceResultCode.InvalidContext &&
                read.Code == ApplicationServiceResultCode.InvalidContext;
            bool noBackend = lookupsBefore == lookupsAfter;
            bool staleProcess = !processHandle.IsValid ||
                Ring3ProcessTable.Resolve(processHandle) == null;
            bool noHandles = ApplicationServiceRegistry.ActiveRequestCount == 0;
            Phase34Marker(staleOwner ? "STALE_APP_INSTANCE_REJECTED=1" :
                "STALE_APP_INSTANCE_REJECTED=0");
            Phase34Marker(staleContext ? "STALE_CONTEXT_REJECTED=1" :
                "STALE_CONTEXT_REJECTED=0");
            Phase34Marker(staleRequests ? "STALE_REQUEST_REJECTED=1" :
                "STALE_REQUEST_REJECTED=0");
            Phase34Marker(staleProcess ? "STALE_PROCESS_REJECTED=1" :
                "STALE_PROCESS_REJECTED=0");
            Phase34Marker(noBackend ? "STALE_NO_BACKEND_LOOKUP=1" :
                "STALE_NO_BACKEND_LOOKUP=0");
            Phase34Marker(noHandles ? "RESOURCE_HANDLES=0" :
                "RESOURCE_HANDLES=NONZERO");
            return staleOwner && staleContext && staleRequests && staleProcess &&
                noBackend && noHandles;
        }

        private static bool RunOnePhase34AndCheckStale(int payloadKind,
                string applicationId, int expectedRequests,
                int expectedLookups, out bool resumed) {
            ApplicationServiceContext context;
            ApplicationServiceAccess access;
            if (!TryCreatePhase34Owner(applicationId, out context, out access)) {
                resumed = false;
                return false;
            }
            ulong ownerValue = _owner.Handle.Value;
            Ring3ProcessHandle processHandle;
            bool main;
            bool ran = RunOnePhase34Lifetime(payloadKind, ownerValue,
                expectedRequests, expectedLookups, out processHandle,
                out resumed, out main);
            bool stale = CheckPhase34StaleRequester(ownerValue, context,
                access, processHandle);
            return ran && main && stale;
        }

        private static void RunPhase34() {
            Native.Cli();
            Phase34Marker("BEGIN=1");
            ApplicationServiceRegistry.Initialize();
            ApplicationDescriptorRegistry.Initialize();
            ApplicationFactoryRegistry.Initialize();

            bool serviceProbe = TryCreatePhase34Owner(Phase34RequesterId,
                out ApplicationServiceContext probeContext,
                out ApplicationServiceAccess probeAccess);
            if (serviceProbe) CleanupOwner();
            bool all = serviceProbe && CheckPhase34KernelValidation();
            int successReturns = 0;
            bool resumed;
            for (int i = 0; i < 4; i++) {
                bool pass = RunOnePhase34AndCheckStale(1,
                    Phase34RequesterId, 5, 7, out resumed) && resumed;
                Phase34Marker(pass ? "PRIMARY_LIFETIME_PASS=1" :
                    "PRIMARY_LIFETIME_PASS=0");
                if (pass) successReturns++;
                all = all && pass;
            }
            Phase34Marker(successReturns == 4 ? "FOUR_LIFETIMES=4" :
                "FOUR_LIFETIMES=FAIL");

            bool crossScope = RunOnePhase34AndCheckStale(4,
                Phase34CrossScopeRequesterId, 1, 1, out resumed) && resumed;
            Phase34Marker(crossScope ? "CROSS_SCOPE_NOT_FOUND=1" :
                "CROSS_SCOPE_NOT_FOUND=0");
            all = all && crossScope;

            int stressSuccesses = 0;
            for (int i = 0; i < 25; i++) {
                bool pass = RunOnePhase34AndCheckStale(1,
                    Phase34RequesterId, 5, 7, out resumed) && resumed;
                if (pass) stressSuccesses++;
                all = all && pass;
            }
            Phase34Marker(stressSuccesses == 25 ?
                "25_RESOURCE_READ_LIFETIMES=PASS" :
                "25_RESOURCE_READ_LIFETIMES=FAIL");

            bool failFastOwner = TryCreatePhase34Owner(Phase34RequesterId,
                out ApplicationServiceContext failFastContext,
                out ApplicationServiceAccess failFastAccess);
            ulong failFastOwnerValue = failFastOwner ? _owner.Handle.Value : 0;
            Ring3ProcessHandle failFastProcess = default(Ring3ProcessHandle);
            bool failFastResumed = false;
            bool failFastMain = false;
            bool failFastRan = failFastOwner && RunOnePhase34Lifetime(2,
                failFastOwnerValue, 2, 3, out failFastProcess,
                out failFastResumed, out failFastMain);
            bool failFastStale = CheckPhase34StaleRequester(
                failFastOwnerValue, failFastContext, failFastAccess,
                failFastProcess);
            Phase34Marker(failFastRan && failFastStale && failFastResumed &&
                    failFastMain ? "FAILFAST_RESOURCE_CLEANUP=1" :
                    "FAILFAST_RESOURCE_CLEANUP=0");
            all = all && failFastRan && failFastStale && failFastResumed &&
                failFastMain;

            bool replacementResumed = false;
            bool replacement = RunOnePhase34AndCheckStale(1,
                Phase34RequesterId, 5, 7, out replacementResumed) &&
                replacementResumed;
            Phase34Marker(replacement ? "REPLACEMENT_RETURN_34=1" :
                "REPLACEMENT_RETURN_34=0");
            all = all && replacement;

            bool staleOwnerCreated = TryCreatePhase34Owner(Phase34RequesterId,
                out ApplicationServiceContext staleContext,
                out ApplicationServiceAccess staleAccess);
            ulong staleOwnerValue = staleOwnerCreated ? _owner.Handle.Value : 0;
            if (staleOwnerCreated) CleanupOwner();
            int staleLookupBefore = _phase34ResourceService == null ? -1 :
                _phase34ResourceService.LookupCount;
            Ring3ProcessHandle staleProcessHandle = default;
            bool staleResumed = false;
            bool staleMain = false;
            bool staleOwnerRan = staleOwnerCreated && RunOnePhase34Lifetime(3,
                staleOwnerValue, 0, 0, out staleProcessHandle,
                out staleResumed, out staleMain);
            bool staleOwnerRejected = staleOwnerRan && staleMain && staleResumed &&
                _phase34ResourceService != null &&
                staleLookupBefore == _phase34ResourceService.LookupCount;
            Phase34Marker(staleOwnerRejected ? "STALE_OWNER_REQUEST_REJECTED=1" :
                "STALE_OWNER_REQUEST_REJECTED=0");
            all = all && staleOwnerRejected;

            bool malformedOwner = TryCreatePhase34Owner(Phase34RequesterId,
                out ApplicationServiceContext malformedContext,
                out ApplicationServiceAccess malformedAccess);
            ulong malformedOwnerValue = malformedOwner ? _owner.Handle.Value : 0;
            Ring3ProcessHandle malformedProcess = default(Ring3ProcessHandle);
            bool malformedResumed = false;
            bool malformedMain = false;
            bool malformedRan = malformedOwner && RunOnePhase34Lifetime(5,
                // Its diagnostic SDK sends one raw invalid operation. The
                // kernel rejects it before counting success or looking up a
                // resource in Phase 10.
                malformedOwnerValue, 0, 0, out malformedProcess,
                out malformedResumed, out malformedMain);
            bool malformedStale = CheckPhase34StaleRequester(
                malformedOwnerValue, malformedContext, malformedAccess,
                malformedProcess);
            bool malformed = malformedRan && malformedMain && malformedResumed &&
                malformedStale;
            Phase34Marker(malformed ? "MALFORMED_REQUEST_REJECTED=1" :
                "MALFORMED_REQUEST_REJECTED=0");
            all = all && malformed;

            bool ownerReleased = _owner == null;
            bool noRequests = ApplicationServiceRegistry.ActiveRequestCount == 0;
            bool processBalanced = Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced &&
                ManagedImageDiagnostics.IsBalanced &&
                NativeBootstrapDiagnostics.IsBalanced;
            Phase34Marker(ownerReleased ? "REQUESTER_AUTHORITY_RELEASED=1" :
                "REQUESTER_AUTHORITY_RELEASED=0");
            Phase34Marker(noRequests ? "ACTIVE_REQUESTS=0" :
                "ACTIVE_REQUESTS=NONZERO");
            Phase34Marker(processBalanced ? "PROCESS_CLEANUP_BALANCED=1" :
                "PROCESS_CLEANUP_BALANCED=0");
            Marker("PHASE34_FREE_INVALID=" + Allocator.FreeFailInvalidPtr.ToString());
            Marker("PHASE34_FREE_CORRUPT=" + Allocator.FreeFailCorruptRun.ToString());
            Marker("PHASE34_FREE_NO_PAGES=" + Allocator.FreeFailNoPages.ToString());
            bool complete = all && successReturns == 4 &&
                stressSuccesses == 25 && ownerReleased && noRequests &&
                processBalanced;
            Phase34Marker(complete ? "COMPLETE=1" : "COMPLETE=0");
            Marker(complete ? "RING3_PHASE34_COMPLETE=1" :
                "RING3_PHASE34_COMPLETE=0");
            Native.Sti();
        }
    }
}
