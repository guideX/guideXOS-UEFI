using guideXOS.GUI;
using guideXOS.OS;
using System;

namespace guideXOS.Misc {
    internal static unsafe partial class Ring3Proof {
        private static bool _phase33Scheduled;
        private const string Phase33RequesterId =
            "selftest.phase33.requester";
        private const string Phase33ObjectId =
            "gxos.shell.computerfiles";
        private const string Phase33TargetId = "gxos.builtin.files";

        private static void Phase33Marker(string value) {
            Marker("PHASE33_" + value);
        }

        internal static void SchedulePhase33() {
            if (_phase33Scheduled) return;
            _phase33Scheduled = true;
            Phase33Marker("SCHEDULED=1");
            new Thread(&RunPhase33, 131072).Start(0);
        }

        private static bool TryCreatePhase33Owner() {
            if (_owner != null) return false;
            LaunchRequest request = LaunchRequest.ForAppId(
                Phase33RequesterId, null, null,
                LaunchActivationIntent.NewInstance);
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!ApplicationInstanceRegistry.TryBeginLaunch(
                    Phase33RequesterId, ApplicationInstancePolicy.MultiInstance,
                    request, out instance, out reused, out failure) ||
                instance == null || reused ||
                !ApplicationInstanceRegistry.TryCompleteLaunch(instance,
                    false, out failure)) {
                Phase33Marker("REQUESTER_CREATED=0");
                return false;
            }
            _owner = instance;
            _ownerCreated = true;
            Phase33Marker("REQUESTER_CREATED=1");
            return true;
        }

        private static bool RunOnePhase33Lifetime(int payloadKind,
                ulong owningApplicationInstance, int expectedRequests,
                out Ring3ProcessHandle oldHandle, out bool resumed,
                out bool mainResult) {
            oldHandle = default(Ring3ProcessHandle);
            resumed = false;
            mainResult = false;
            Native.Cli();
            string failure;
            Ring3Process process;
            if (!Ring3Process.TryCreateManagedOpenDocumentEntry(
                    owningApplicationInstance, payloadKind, out process,
                    out failure) || process == null) {
                Native.Sti();
                Phase33Marker("PROCESS_CREATE_FAILED=1");
                if (failure != null)
                    Phase33Marker("PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }

            oldHandle = process.Handle;
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool authorized = process.ManagedImage != null &&
                process.ManagedImage.TryEnterManagedEntry();
            bool started = scaffold && authorized &&
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
            bool failFast = payloadKind == 5;
            mainResult = failFast
                ? observedExitCode == -1 && serviceRequests == 1
                : process.BootstrapResultSucceeded &&
                    process.BootstrapReturnCode == 33 &&
                    observedExitCode == 33;
            bool state = process.State == Ring3ProcessState.Exiting ||
                process.State == Ring3ProcessState.Exited;
            bool clean = process.Cleanup();
            bool stale = Ring3ProcessTable.Resolve(oldHandle) == null;

            Phase33Marker(scaffold ? "SCAFFOLD_PASS=1" : "SCAFFOLD_PASS=0");
            Phase33Marker(authorized ? "ENTRY_AUTHORIZED=1" : "ENTRY_AUTHORIZED=0");
            Phase33Marker(dispatched ? "DISPATCH_PASS=1" : "DISPATCH_PASS=0");
            Phase33Marker(resumed ? "RESUME_PASS=1" : "RESUME_PASS=0");
            NumberMarker("PHASE33_SERVICE_REQUESTS=", serviceRequests);
            Phase33Marker(failFast ? "FAILFAST_EXIT=" + observedExitCode.ToString() :
                "MAIN_RETURN=" + observedExitCode.ToString());
            Phase33Marker(serviceRequests == expectedRequests
                ? "SERVICE_COUNT_PASS=1" : "SERVICE_COUNT_PASS=0");
            Phase33Marker(mainResult && state
                ? (failFast ? "FAILFAST_REQUESTER_DIED=1" : "MAIN_RESULT_PASS=1")
                : (failFast ? "FAILFAST_REQUESTER_DIED=0" : "MAIN_RESULT_PASS=0"));
            Phase33Marker(clean && stale ? "PROCESS_CLEANUP=1" : "PROCESS_CLEANUP=0");
            return started && completed && scaffold && authorized && dispatched &&
                resumed && serviceRequests == expectedRequests && mainResult &&
                state && clean && stale;
        }

        private static ApplicationInstance GetPhase33Target(int index) {
            return ApplicationInstanceRegistry.GetByDescriptorAt(
                Phase33TargetId, index);
        }

        private static bool IsPhase33Target(ApplicationInstance target) {
            if (target == null || target.DescriptorId != Phase33TargetId ||
                    target.OwnedWindowCount <= 0 ||
                    target.LifecycleState !=
                        ApplicationInstanceLifecycleState.Activated) return false;
            LaunchRequest request = target.LaunchRequestContext;
            ApplicationFactory factory;
            return request != null &&
                request.TargetKind == LaunchRequestTargetKind.ShellObject &&
                request.SourceShellObjectId == Phase33ObjectId &&
                ApplicationFactoryRegistry.TryGet(target.DescriptorId,
                    out factory) && factory is ComputerFilesApplicationFactory;
        }

        private static bool CheckPhase33StaleRequester(ulong ownerValue,
                ApplicationServiceContext context, ApplicationServiceAccess access,
                Ring3ProcessHandle processHandle) {
            CleanupOwner();
            ApplicationInstance ignored;
            bool staleOwner = ownerValue != 0 &&
                !ApplicationInstanceRegistry.TryGet(
                    ApplicationInstanceHandle.FromValue(ownerValue), out ignored);
            ApplicationServiceResult validation;
            bool staleContext = context != null &&
                !ApplicationServiceRegistry.TryValidateContext(context,
                    ApplicationServiceId.Shell, out ignored, out validation) &&
                validation.Code == ApplicationServiceResultCode.InvalidContext;
            int factoriesBefore = ApplicationFactoryRegistry.FactoryLaunches;
            int targetsBefore = ApplicationInstanceRegistry.CountByDescriptor(
                Phase33TargetId);
            int fallbacksBefore =
                ApplicationFactoryRegistry.CompatibilityFallbackLaunches;
            int legacyBefore = AppModelCompatibilityDiagnostics.LegacyBackendCalls;
            ApplicationServiceResult<ApplicationServiceRequestHandle> retry =
                context != null && access != null && access.Shell != null
                    ? access.Shell.Begin(context,
                        ApplicationShellOpenRequest.ForShellObject(
                            Phase33ObjectId))
                    : null;
            bool staleRequest = retry != null && !retry.Succeeded &&
                retry.Code == ApplicationServiceResultCode.InvalidContext;
            bool staleProcess = !processHandle.IsValid ||
                Ring3ProcessTable.Resolve(processHandle) == null;
            bool noBackend = factoriesBefore ==
                    ApplicationFactoryRegistry.FactoryLaunches &&
                targetsBefore == ApplicationInstanceRegistry.CountByDescriptor(
                    Phase33TargetId) && fallbacksBefore ==
                    ApplicationFactoryRegistry.CompatibilityFallbackLaunches &&
                legacyBefore == AppModelCompatibilityDiagnostics.LegacyBackendCalls;
            Phase33Marker(staleOwner ? "STALE_APP_INSTANCE_REJECTED=1" :
                "STALE_APP_INSTANCE_REJECTED=0");
            Phase33Marker(staleProcess ? "STALE_PROCESS_REJECTED=1" :
                "STALE_PROCESS_REJECTED=0");
            Phase33Marker(staleContext ? "STALE_CONTEXT_REJECTED=1" :
                "STALE_CONTEXT_REJECTED=0");
            Phase33Marker(staleRequest ? "STALE_SERVICE_REQUEST_REJECTED=1" :
                "STALE_SERVICE_REQUEST_REJECTED=0");
            Phase33Marker(noBackend ? "STALE_NO_BACKEND_EFFECT=1" :
                "STALE_NO_BACKEND_EFFECT=0");
            return staleOwner && staleProcess && staleContext && staleRequest &&
                noBackend;
        }

        private static bool CleanupPhase33Target(
                ApplicationInstanceHandle handle) {
            if (!handle.IsValid) return false;
            if (!ApplicationInstanceRegistry.TryTerminate(handle,
                    "Phase 33 diagnostic target cleanup")) return false;
            WindowManager.CleanupClosedWindowsAndGetCountSnapshot();
            return !ApplicationInstanceRegistry.TryGet(handle,
                out ApplicationInstance ignored);
        }

        private static bool CheckPhase33KernelValidation() {
            Ring3ShellLaunchRequest request = default(Ring3ShellLaunchRequest);
            request.StructureVersion = (uint)Ring3Abi.AbiVersion;
            request.ServiceId = Ring3Abi.ShellService;
            request.OperationId = Ring3Abi.ShellOpenObjectOperation;
            request.RequestLength = (uint)sizeof(Ring3ShellLaunchRequest);
            request.TargetLength = 24;
            request.ResponseCapacity = (uint)sizeof(Ring3ShellLaunchResponse);
            request.ResponseBuffer = Ring3Process.UserStackStart + 0x1000UL;
            byte* target = request.Target;
            const string validId = Phase33ObjectId;
            for (int i = 0; i < validId.Length; i++) {
                target[i * 2] = (byte)validId[i];
                target[i * 2 + 1] = (byte)(validId[i] >> 8);
            }
            bool validAccepted = Ring3Abi.ValidateShellLaunchRequestForPhase33Proof(
                &request) == Ring3Abi.Success;
            request.OperationId = 99;
            bool invalidOperationRejected =
                Ring3Abi.ValidateShellLaunchRequestForPhase33Proof(&request) ==
                    Ring3Abi.InvalidRequest;
            request.OperationId = Ring3Abi.ShellOpenObjectOperation;
            request.RequestLength--;
            bool malformedSizeRejected =
                Ring3Abi.ValidateShellLaunchRequestForPhase33Proof(&request) ==
                    Ring3Abi.InvalidRequest;
            Phase33Marker(validAccepted ? "WIRE_VALID_ACCEPTED=1" :
                "WIRE_VALID_ACCEPTED=0");
            Phase33Marker(invalidOperationRejected ?
                "INVALID_OPERATION_REJECTED=1" : "INVALID_OPERATION_REJECTED=0");
            Phase33Marker(malformedSizeRejected ?
                "MALFORMED_SIZE_REJECTED=1" : "MALFORMED_SIZE_REJECTED=0");
            return validAccepted && invalidOperationRejected &&
                malformedSizeRejected;
        }

        private static bool RunPhase33NegativeLifetime(int kind,
                ulong ownerValue, int expectedServiceRequests,
                int factoriesBefore, int targetsBefore,
                out Ring3ProcessHandle processHandle) {
            processHandle = default(Ring3ProcessHandle);
            bool resumed;
            bool main;
            bool ran = RunOnePhase33Lifetime(kind, ownerValue,
                expectedServiceRequests, out processHandle, out resumed,
                out main);
            bool noBackend = factoriesBefore ==
                    ApplicationFactoryRegistry.FactoryLaunches &&
                targetsBefore == ApplicationInstanceRegistry.CountByDescriptor(
                    Phase33TargetId);
            Phase33Marker(noBackend ? "NEGATIVE_NO_BACKEND_EFFECT=1" :
                "NEGATIVE_NO_BACKEND_EFFECT=0");
            return ran && main && noBackend;
        }

        private static void RunPhase33() {
            Native.Cli();
            Phase33Marker("BEGIN=1");
            ApplicationServiceRegistry.Initialize();
            ApplicationDescriptorRegistry.Initialize();
            ApplicationFactoryRegistry.Initialize();

            int initialTargets = ApplicationInstanceRegistry.CountByDescriptor(
                Phase33TargetId);
            int initialWindows = WindowManager.GetWindowCountSnapshot();
            int factoryStart = ApplicationFactoryRegistry.FactoryLaunches;
            int fallbackStart =
                ApplicationFactoryRegistry.CompatibilityFallbackLaunches;
            int legacyStart = AppModelCompatibilityDiagnostics.LegacyBackendCalls;
            ApplicationInstance[] retainedTargets = new ApplicationInstance[6];
            bool all = initialTargets >= 0;
            int successReturns = 0;
            int createdTargets = 0;

            bool kernelValidation = CheckPhase33KernelValidation();
            all = all && kernelValidation;

            for (int i = 0; i < 4; i++) {
                bool ownerCreated = TryCreatePhase33Owner();
                ApplicationServiceContext context = null;
                ApplicationServiceAccess access = null;
                ApplicationServiceResult contextResult;
                ulong ownerValue = ownerCreated ? _owner.Handle.Value : 0;
                if (ownerCreated)
                    ApplicationServiceRegistry.TryCreateContextAndAccess(
                        _owner.Handle, out context, out access, out contextResult);
                Ring3ProcessHandle processHandle = default(Ring3ProcessHandle);
                bool resumed = false;
                bool main = false;
                bool ran = ownerCreated && RunOnePhase33Lifetime(4,
                    ownerValue, 1, out processHandle, out resumed, out main);
                ApplicationInstance target = GetPhase33Target(initialTargets + i);
                bool targetCreated = ran && IsPhase33Target(target);
                bool persists = targetCreated &&
                    ApplicationInstanceRegistry.TryGet(target.Handle,
                        out ApplicationInstance liveTarget) && liveTarget == target;
                bool stale = CheckPhase33StaleRequester(ownerValue, context,
                    access, processHandle);
                bool distinct = targetCreated;
                for (int j = 0; j < createdTargets; j++)
                    distinct = distinct && retainedTargets[j] != target;
                if (targetCreated) retainedTargets[createdTargets++] = target;
                bool pass = ran && resumed && main && targetCreated && persists &&
                    stale && distinct;
                Phase33Marker(pass ? "PRIMARY_LIFETIME_PASS=1" :
                    "PRIMARY_LIFETIME_PASS=0");
                if (pass) successReturns++;
                all = all && pass;
            }
            Phase33Marker(successReturns == 4 ? "FOUR_LIFETIMES=4" :
                "FOUR_LIFETIMES=FAIL");

            bool primaryCleanup = true;
            for (int i = 0; i < createdTargets; i++) {
                ApplicationInstance target = retainedTargets[i];
                ApplicationInstanceHandle targetHandle = target == null
                    ? ApplicationInstanceHandle.None : target.Handle;
                retainedTargets[i] = null;
                target = null;
                primaryCleanup = CleanupPhase33Target(targetHandle) &&
                    primaryCleanup;
            }
            Phase33Marker(primaryCleanup ? "PRIMARY_TARGET_CLEANUP=1" :
                "PRIMARY_TARGET_CLEANUP=0");
            createdTargets = 0;
            all = all && primaryCleanup;

            bool stress = true;
            for (int i = 0; i < 25; i++) {
                bool ownerCreated = TryCreatePhase33Owner();
                ApplicationServiceContext context = null;
                ApplicationServiceAccess access = null;
                ApplicationServiceResult contextResult;
                ulong ownerValue = ownerCreated ? _owner.Handle.Value : 0;
                if (ownerCreated)
                    ApplicationServiceRegistry.TryCreateContextAndAccess(
                        _owner.Handle, out context, out access, out contextResult);
                int targetCountBefore = ApplicationInstanceRegistry.CountByDescriptor(
                    Phase33TargetId);
                Ring3ProcessHandle processHandle = default(Ring3ProcessHandle);
                bool resumed = false;
                bool main = false;
                bool ran = ownerCreated && RunOnePhase33Lifetime(4,
                    ownerValue, 1, out processHandle, out resumed, out main);
                ApplicationInstance target = GetPhase33Target(targetCountBefore);
                bool targetCreated = ran && IsPhase33Target(target);
                ApplicationInstanceHandle targetHandle = targetCreated
                    ? target.Handle : ApplicationInstanceHandle.None;
                bool stale = CheckPhase33StaleRequester(ownerValue, context,
                    access, processHandle);
                target = null;
                bool targetClean = targetCreated &&
                    CleanupPhase33Target(targetHandle);
                bool pass = ran && resumed && main && targetCreated && stale && targetClean;
                stress = stress && pass;
                if (pass) successReturns++;
            }
            Phase33Marker(stress ? "25_ACTION_STRESS=PASS" :
                "25_ACTION_STRESS=FAIL");
            all = all && stress;

            bool failFastOwner = TryCreatePhase33Owner();
            ApplicationServiceContext failFastContext = null;
            ApplicationServiceAccess failFastAccess = null;
            ApplicationServiceResult failFastContextResult;
            ulong failFastOwnerValue = failFastOwner ? _owner.Handle.Value : 0;
            if (failFastOwner)
                ApplicationServiceRegistry.TryCreateContextAndAccess(
                    _owner.Handle, out failFastContext, out failFastAccess,
                    out failFastContextResult);
            int failFastTargetCount = ApplicationInstanceRegistry.CountByDescriptor(
                Phase33TargetId);
            Ring3ProcessHandle failFastProcess = default(Ring3ProcessHandle);
            bool failFastResumed = false;
            bool failFastMain = false;
            bool failFastRan = failFastOwner && RunOnePhase33Lifetime(5,
                failFastOwnerValue, 1, out failFastProcess,
                out failFastResumed, out failFastMain);
            ApplicationInstance failFastTarget = GetPhase33Target(
                failFastTargetCount);
            bool failFastPersists = failFastRan && failFastMain &&
                IsPhase33Target(failFastTarget) &&
                ApplicationInstanceRegistry.TryGet(failFastTarget.Handle,
                    out ApplicationInstance failFastLive) &&
                failFastLive == failFastTarget;
            if (failFastPersists) retainedTargets[createdTargets++] = failFastTarget;
            bool failFastStale = CheckPhase33StaleRequester(failFastOwnerValue,
                failFastContext, failFastAccess, failFastProcess);
            Phase33Marker(failFastPersists ? "FAILFAST_TARGET_PERSISTED=1" :
                "FAILFAST_TARGET_PERSISTED=0");
            all = all && failFastRan && failFastResumed && failFastMain &&
                failFastPersists && failFastStale;

            bool replacementOwner = TryCreatePhase33Owner();
            ApplicationServiceContext replacementContext = null;
            ApplicationServiceAccess replacementAccess = null;
            ApplicationServiceResult replacementContextResult;
            ulong replacementOwnerValue = replacementOwner ? _owner.Handle.Value : 0;
            if (replacementOwner)
                ApplicationServiceRegistry.TryCreateContextAndAccess(
                    _owner.Handle, out replacementContext, out replacementAccess,
                    out replacementContextResult);
            int replacementTargetCount = ApplicationInstanceRegistry.CountByDescriptor(
                Phase33TargetId);
            Ring3ProcessHandle replacementProcess = default(Ring3ProcessHandle);
            bool replacementResumed = false;
            bool replacementMain = false;
            bool replacementRan = replacementOwner && RunOnePhase33Lifetime(4,
                replacementOwnerValue, 1, out replacementProcess,
                out replacementResumed, out replacementMain);
            ApplicationInstance replacementTarget = GetPhase33Target(
                replacementTargetCount);
            bool replacementSuccess = replacementRan && replacementMain &&
                replacementResumed && IsPhase33Target(replacementTarget);
            if (replacementSuccess) {
                retainedTargets[createdTargets++] = replacementTarget;
                successReturns++;
            }
            bool replacementStale = CheckPhase33StaleRequester(
                replacementOwnerValue, replacementContext, replacementAccess,
                replacementProcess);
            Phase33Marker(replacementSuccess ? "REPLACEMENT_RETURN_33=1" :
                "REPLACEMENT_RETURN_33=0");
            all = all && replacementSuccess && replacementStale;

            int factoriesBeforeStale = ApplicationFactoryRegistry.FactoryLaunches;
            int targetsBeforeStale = ApplicationInstanceRegistry.CountByDescriptor(
                Phase33TargetId);
            Ring3ProcessHandle staleProcess;
            bool staleRan = RunOnePhase33Lifetime(6, failFastOwnerValue, 1,
                out staleProcess, out bool staleResumed, out bool staleMain);
            bool staleNoBackend = factoriesBeforeStale ==
                    ApplicationFactoryRegistry.FactoryLaunches &&
                targetsBeforeStale == ApplicationInstanceRegistry.CountByDescriptor(
                    Phase33TargetId);
            bool staleOwnerRejected = staleRan && staleMain && staleResumed &&
                staleNoBackend;
            Phase33Marker(staleOwnerRejected ? "STALE_REQUESTER_AUTHORITY_REJECTED=1" :
                "STALE_REQUESTER_AUTHORITY_REJECTED=0");
            all = all && staleOwnerRejected;

            bool invalidOwner = TryCreatePhase33Owner();
            ulong invalidOwnerValue = invalidOwner ? _owner.Handle.Value : 0;
            int factoriesBeforeInvalid = ApplicationFactoryRegistry.FactoryLaunches;
            int targetsBeforeInvalid = ApplicationInstanceRegistry.CountByDescriptor(
                Phase33TargetId);
            Ring3ProcessHandle invalidProcess = default(Ring3ProcessHandle);
            bool invalidAction = invalidOwner && RunPhase33NegativeLifetime(7,
                invalidOwnerValue, 1, factoriesBeforeInvalid,
                targetsBeforeInvalid, out invalidProcess);
            ApplicationServiceContext invalidContext = null;
            ApplicationServiceAccess invalidAccess = null;
            ApplicationServiceResult invalidContextResult;
            if (invalidOwner)
                ApplicationServiceRegistry.TryCreateContextAndAccess(
                    _owner.Handle, out invalidContext, out invalidAccess,
                    out invalidContextResult);
            bool invalidStale = CheckPhase33StaleRequester(invalidOwnerValue,
                invalidContext, invalidAccess, invalidProcess);
            Phase33Marker(invalidAction && invalidStale
                ? "INVALID_ACTION_REJECTED=1" : "INVALID_ACTION_REJECTED=0");
            all = all && invalidAction && invalidStale;

            bool malformedOwner = TryCreatePhase33Owner();
            ulong malformedOwnerValue = malformedOwner ? _owner.Handle.Value : 0;
            int factoriesBeforeMalformed = ApplicationFactoryRegistry.FactoryLaunches;
            int targetsBeforeMalformed = ApplicationInstanceRegistry.CountByDescriptor(
                Phase33TargetId);
            Ring3ProcessHandle malformedProcess = default(Ring3ProcessHandle);
            bool malformed = malformedOwner && RunPhase33NegativeLifetime(8,
                malformedOwnerValue, 0, factoriesBeforeMalformed,
                targetsBeforeMalformed, out malformedProcess);
            ApplicationServiceContext malformedContext = null;
            ApplicationServiceAccess malformedAccess = null;
            ApplicationServiceResult malformedContextResult;
            if (malformedOwner)
                ApplicationServiceRegistry.TryCreateContextAndAccess(
                    _owner.Handle, out malformedContext, out malformedAccess,
                    out malformedContextResult);
            bool malformedStale = CheckPhase33StaleRequester(
                malformedOwnerValue, malformedContext, malformedAccess,
                malformedProcess);
            Phase33Marker(malformed && malformedStale
                ? "MALFORMED_REQUEST_REJECTED=1" :
                    "MALFORMED_REQUEST_REJECTED=0");
            all = all && malformed && malformedStale;

            bool cleanup = true;
            for (int i = 0; i < createdTargets; i++) {
                ApplicationInstance target = retainedTargets[i];
                ApplicationInstanceHandle targetHandle = target == null
                    ? ApplicationInstanceHandle.None : target.Handle;
                retainedTargets[i] = null;
                target = null;
                bool targetCleanup = CleanupPhase33Target(targetHandle);
                Phase33Marker("FINAL_TARGET_CLEANUP=" + i.ToString() + ":" +
                    (targetCleanup ? "1" : "0"));
                cleanup = targetCleanup && cleanup;
            }
            int finalWindows = WindowManager.GetWindowCountSnapshot();
            int finalTargets = ApplicationInstanceRegistry.CountByDescriptor(
                Phase33TargetId);
            int finalRequests = ApplicationServiceRegistry.ActiveRequestCount;
            int finalFactories = ApplicationFactoryRegistry.FactoryLaunches;
            int finalFallbacks =
                ApplicationFactoryRegistry.CompatibilityFallbackLaunches;
            int finalLegacy = AppModelCompatibilityDiagnostics.LegacyBackendCalls;
            bool ownerReleased = _owner == null;
            bool targetsBalanced = finalTargets == initialTargets;
            bool windowsBalanced = finalWindows == initialWindows;
            bool factoriesBalanced = finalFactories == factoryStart + 31;
            bool fallbackBalanced = finalFallbacks == fallbackStart;
            bool legacyBalanced = finalLegacy == legacyStart;
            bool requestsBalanced = finalRequests == 0;
            Phase33Marker(cleanup ? "TARGET_CLEANUP_BALANCED=1" :
                "TARGET_CLEANUP_BALANCED=0");
            Phase33Marker(targetsBalanced ? "TARGET_COUNT_BALANCED=1" :
                "TARGET_COUNT_BALANCED=0");
            Phase33Marker(windowsBalanced ? "WINDOW_COUNT_BALANCED=1" :
                "WINDOW_COUNT_BALANCED=0");
            Phase33Marker(factoriesBalanced ? "FACTORY_COUNT_BALANCED=1" :
                "FACTORY_COUNT_BALANCED=0");
            Phase33Marker(fallbackBalanced ? "FALLBACK_COUNT_BALANCED=1" :
                "FALLBACK_COUNT_BALANCED=0");
            Phase33Marker(legacyBalanced ? "LEGACY_COUNT_BALANCED=1" :
                "LEGACY_COUNT_BALANCED=0");
            Phase33Marker(requestsBalanced ? "REQUEST_COUNT_BALANCED=1" :
                "REQUEST_COUNT_BALANCED=0");
            Phase33Marker(ownerReleased ? "REQUESTER_AUTHORITY_RELEASED=1" :
                "REQUESTER_AUTHORITY_RELEASED=0");
            Phase33Marker("BALANCE_DETAIL=targets:" + initialTargets.ToString() +
                ":" + finalTargets.ToString() + ";windows:" +
                initialWindows.ToString() + ":" + finalWindows.ToString() +
                ";factories:" + factoryStart.ToString() + ":" +
                finalFactories.ToString() + ";fallbacks:" + fallbackStart.ToString() +
                ":" + finalFallbacks.ToString() + ";legacy:" +
                legacyStart.ToString() + ":" + finalLegacy.ToString() +
                ";requests:" + finalRequests.ToString());
            Marker("PHASE33_FREE_INVALID=" + Allocator.FreeFailInvalidPtr.ToString());
            Marker("PHASE33_FREE_CORRUPT=" + Allocator.FreeFailCorruptRun.ToString());
            Marker("PHASE33_FREE_NO_PAGES=" + Allocator.FreeFailNoPages.ToString());
            bool balanced = cleanup && targetsBalanced && windowsBalanced &&
                factoriesBalanced && fallbackBalanced && legacyBalanced &&
                requestsBalanced && ownerReleased;
            NumberMarker("PHASE33_SUCCESSFUL_MAIN_RETURNS=", successReturns);
            Phase33Marker(successReturns == 30 ? "SUCCESSFUL_RETURNS=30" :
                "SUCCESSFUL_RETURNS=FAIL");
            Phase33Marker(balanced ? "APP_MODEL_BALANCED=1" :
                "APP_MODEL_BALANCED=0");
            bool processBalanced = Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced &&
                ManagedImageDiagnostics.IsBalanced &&
                NativeBootstrapDiagnostics.IsBalanced;
            Phase33Marker(processBalanced ? "PROCESS_CLEANUP_BALANCED=1" :
                "PROCESS_CLEANUP_BALANCED=0");
            bool complete = all && successReturns == 30 && balanced &&
                processBalanced && finalFallbacks - fallbackStart == 0 &&
                finalLegacy - legacyStart == 0;
            Phase33Marker(complete ? "COMPLETE=1" : "COMPLETE=0");
            Marker(complete ? "RING3_PHASE33_COMPLETE=1" :
                "RING3_PHASE33_COMPLETE=0");
            Native.Sti();
        }
    }
}
