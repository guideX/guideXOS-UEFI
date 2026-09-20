using guideXOS.OS;
using System;

namespace guideXOS.Misc {
    internal static unsafe class Ring3Proof {
        private const ulong Phase15Sentinel = 0xA15A15A15A15A15UL;
        private static bool _scheduled;
        private static bool _direct;
        private static ApplicationInstance _owner;
        private static bool _ownerCreated;
        private static bool _awaitingDesktopHeartbeat;
        private static bool _desktopHeartbeatObserved;

        private sealed class Phase15Lifetime {
            internal Ring3Process Process;
            internal ApplicationInstance Owner;
            internal ApplicationServiceContext ServiceContext;
            internal Thread StaleThread;
            internal Ring3ProcessHandle ProcessHandle;
            internal ulong OwnerHandle;
            internal int Slot;
            internal uint Generation;
            internal ulong Cr3;
            internal ulong KernelStackBase;
            internal ulong KernelStackTop;
            internal ulong UserDataPhysical;
            internal ulong UserDataMappedPhysical;
            internal ulong UserStackPhysical;
            internal ulong InitialDataSentinel;
            internal ulong DataSentinelAfterRun;
            internal Ring3ProcessState State;
            internal bool ExpectedState;
            internal bool Completed;
            internal bool DispatchValid;
            internal bool ResumeValid;
            internal bool ServiceRequestSucceeded;
            internal bool OwnerMatchesProcess;
            internal bool OwnerDetached;
            internal bool CleanupComplete;
            internal bool FaultRecordValid;
            internal bool FaultStateCleared;
            internal bool ThreadCleanup;
            internal bool StaleHandleRejected;
            internal bool StaleServiceContextRejected;
        }

        private static void Marker(string text) {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++)
                Native.Out8(0x3F8, (byte)text[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void NumberMarker(string label, int value) {
            Marker(label + value.ToString());
        }

        private static void HexMarker(string label, ulong value) {
            Marker(label);
            for (int shift = 60; shift >= 0; shift -= 4) {
                int nibble = (int)((value >> shift) & 0xFUL);
                Native.Out8(0x3F8,
                    (byte)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10));
            }
            Native.Out8(0x3F8, (byte)'\n');
        }

        internal static void Schedule() {
            if (_scheduled) return;
            _scheduled = true;
            _direct = false;
            Marker("RING3_PROOF_SCHEDULED=1");
            new Thread(&Run, 32768).Start(0);
        }

        internal static void SchedulePhase15() {
            if (_scheduled) return;
            _scheduled = true;
            _direct = false;
            Marker("RING3_PHASE15_SCHEDULED=1");
            new Thread(&RunPhase15, 32768).Start(0);
        }

        internal static void ObserveDesktopHeartbeat() {
            if (!_awaitingDesktopHeartbeat || _desktopHeartbeatObserved) return;
            _desktopHeartbeatObserved = true;
            _awaitingDesktopHeartbeat = false;
            Marker("RING3_DESKTOP_HEARTBEAT_AFTER_CLEANUP=1");
        }

        // Retained as the Phase 13 regression selector. It deliberately
        // bypasses the timer scheduler and uses the old direct trampoline.
        internal static void RunDirect() {
            if (_scheduled) return;
            _scheduled = true;
            _direct = true;
            Marker("RING3_PROOF_SCHEDULED=1");
            Native.Cli();
            Marker("RING3_SCHEDULER_CONTEXT_SWITCHING=0");
            Run();
        }

        private static bool RunOneDirect(Ring3PayloadKind kind,
                                         bool expectFault) {
            Ring3Process process;
            if (!Ring3Process.TryCreate(kind, out process)) {
                Marker("RING3_PROCESS_CREATE_FAILED=1");
                return false;
            }
            Ring3ProcessHandle oldHandle = process.Handle;
            ThreadPool.BeginDirectUser(process.UserThread);
            Marker("RING3_CR3_USER_ACTIVE=1");
            Native.EnterR3AndReturn(Ring3Process.UserCodeStart,
                process.UserStackEnd - 16UL, 0x2UL);
            ThreadPool.EndDirectUser();
            Marker("RING3_CR3_KERNEL_RESTORED=1");
            bool stateOk = expectFault ? process.State == Ring3ProcessState.Failed :
                                         process.State == Ring3ProcessState.Exiting;
            process.Cleanup();
            if (!process.TryResolveHandle(oldHandle))
                Marker("RING3_STALE_HANDLE_REJECTED=1");
            else
                Marker("RING3_STALE_HANDLE_REJECTED=0");
            return stateOk;
        }

        private static bool TryCreateOwner() {
            if (_owner != null) return true;
            AppLaunchResolver.InitializeDefaultDescriptors();
            ApplicationDescriptorRegistry.Initialize();
            ApplicationDescriptor descriptor;
            const string appId = "gxos.builtin.taskmanager";
            if (!ApplicationDescriptorRegistry.TryGetById(appId,
                                                           out descriptor)) {
                Marker("RING3_APP_MODEL_OWNER_CREATED=0");
                return false;
            }

            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            LaunchRequest request = LaunchRequest.ForAppId(appId, null, null,
                LaunchActivationIntent.Launch);
            if (!ApplicationInstanceRegistry.TryBeginLaunch(descriptor, request,
                    out instance, out reused, out failure) || instance == null) {
                Marker("RING3_APP_MODEL_OWNER_CREATED=0");
                return false;
            }
            if (!reused && !ApplicationInstanceRegistry.TryCompleteLaunch(
                    instance, false, out failure)) {
                ApplicationInstanceRegistry.TryTerminate(instance.Handle,
                    "Ring 3 owner fixture initialization failed");
                Marker("RING3_APP_MODEL_OWNER_CREATED=0");
                return false;
            }
            _owner = instance;
            _ownerCreated = !reused;
            Marker("RING3_APP_MODEL_OWNER_CREATED=1");
            Marker("RING3_APP_MODEL_OWNER_RUNNING=1");
            return true;
        }

        private static bool RunOneScheduled(Ring3PayloadKind kind,
                                            bool expectFault,
                                            bool requirePreemption) {
            if (_owner == null) return false;
            Ring3Process process;
            // Process publication, address-space construction, and insertion
            // into the scheduler list are one bounded kernel critical section.
            // No timer may observe half-published process/thread state.
            Native.Cli();
            bool created = Ring3Process.TryCreate(kind, _owner.Handle.Value,
                                                   out process);
            Native.Sti();
            if (!created) {
                Marker("RING3_PROCESS_CREATE_FAILED=1");
                return false;
            }
            Ring3ProcessHandle oldHandle = process.Handle;
            Marker("RING3_SCHEDULED_USER_READY=1");
            int spins = 0;
            while (!process.IsTerminal && spins++ < 2000000)
                Native.Hlt();

            bool completed = process.IsTerminal;
            bool stateOk = expectFault ? process.State == Ring3ProcessState.Failed :
                                         process.State == Ring3ProcessState.Exiting;
            bool scheduleOk = process.SchedulerDispatches >= 1 &&
                              process.SchedulerCr3Valid &&
                              process.SchedulerRsp0Valid;
            bool resumeOk = !requirePreemption ||
                            (process.TimerPreemptions > 0 &&
                             process.SchedulerDispatches >= 2 &&
                             process.UserRspPreserved);
            if (requirePreemption && process.TimerPreemptions > 0)
                Marker("RING3_SCHEDULER_OBSERVED_USER=1");
            Marker(completed && stateOk && scheduleOk && resumeOk
                ? "RING3_SCHEDULED_RESULT=PASS"
                : "RING3_SCHEDULED_RESULT=FAIL");

            process.Cleanup();
            if (!process.TryResolveHandle(oldHandle))
                Marker("RING3_STALE_HANDLE_REJECTED=1");
            else
                Marker("RING3_STALE_HANDLE_REJECTED=0");
            Marker("RING3_USER_THREAD_NOT_REDISPATCHED=1");
            return completed && stateOk && scheduleOk && resumeOk;
        }

        private static void CleanupOwner() {
            if (_owner == null) return;
            ulong value = _owner.Handle.Value;
            if (_ownerCreated && ApplicationInstanceRegistry.TryTerminate(
                    _owner.Handle, "scheduled Ring 3 diagnostic complete")) {
                Marker("RING3_APP_MODEL_OWNER_DETACHED=1");
            } else if (!_ownerCreated) {
                Marker("RING3_APP_MODEL_OWNER_DETACHED=0");
            } else {
                Marker("RING3_APP_MODEL_OWNER_DETACHED=0");
            }
            _owner = null;
            Marker(value != 0 ? "RING3_STALE_SERVICE_CONTEXTS=0" :
                "RING3_STALE_SERVICE_CONTEXTS=1");
        }

        private static bool TryCreatePhase15Owner(
                out ApplicationInstance owner) {
            owner = null;
            bool reused;
            LaunchResult failure;
            LaunchRequest request = LaunchRequest.ForAppId(
                "selftest.phase15.ring3", null, null,
                LaunchActivationIntent.Launch);
            if (!ApplicationInstanceRegistry.TryBeginLaunch(
                    "selftest.phase15.ring3",
                    ApplicationInstancePolicy.MultiInstance,
                    request, out owner, out reused, out failure) ||
                    owner == null) {
                Marker("RING3_PHASE15_OWNER_CREATE_FAILED=1");
                return false;
            }
            if (!reused && !ApplicationInstanceRegistry.TryCompleteLaunch(
                    owner, false, out failure)) {
                ApplicationInstanceRegistry.TryTerminate(owner,
                    "Phase 15 owner initialization failed");
                owner = null;
                Marker("RING3_PHASE15_OWNER_CREATE_FAILED=1");
                return false;
            }
            HexMarker("RING3_PHASE15_OWNER_HANDLE=0x", owner.Handle.Value);
            return true;
        }

        private static bool RunPhase15Lifetime(
                Ring3PayloadKind kind, bool expectFault, bool requirePreemption,
                bool leaveLive, out Phase15Lifetime sample) {
            sample = new Phase15Lifetime();
            if (!TryCreatePhase15Owner(out sample.Owner)) return false;

            Native.Cli();
            bool created = Ring3Process.TryCreate(kind,
                sample.Owner.Handle.Value, out sample.Process);
            if (!created || sample.Process == null) {
                Native.Sti();
                ApplicationInstanceRegistry.TryTerminate(sample.Owner,
                    "Phase 15 process creation failed");
                sample.Owner = null;
                Marker("RING3_PHASE15_PROCESS_CREATE_FAILED=1");
                return false;
            }

            Ring3Process process = sample.Process;
            sample.ProcessHandle = process.Handle;
            sample.OwnerHandle = sample.Owner.Handle.Value;
            sample.Slot = process.Handle.Slot;
            sample.Generation = process.Handle.Generation;
            sample.Cr3 = process.Space == null ? 0UL : process.Space.RootPhysical;
            sample.UserDataPhysical = process.UserDataPhysical;
            sample.UserStackPhysical = process.UserStackPhysical;
            sample.StaleThread = process.UserThread;
            sample.KernelStackBase = process.UserThread == null
                ? 0UL : process.UserThread.KernelStackBase;
            sample.KernelStackTop = process.UserThread == null
                ? 0UL : process.UserThread.KernelStackTop;
            sample.InitialDataSentinel = process.ReadUserDataSentinel();
            sample.OwnerMatchesProcess =
                process.OwningApplicationInstance == sample.OwnerHandle;

            ulong translated;
            if (process.Space != null && PageTable.TryTranslateUser(
                    process.Space.Pml4, Ring3Process.UserDataStart, true,
                    out translated)) {
                sample.UserDataMappedPhysical = translated & PageTable.PageMask;
            }

            // Do not let the newly published user thread run before the
            // fixture records its initial private-page state.  Publication,
            // identity capture, and the initial sentinel snapshot form one
            // bounded diagnostic transaction; the first user instruction
            // begins only after interrupts are restored below.
            Native.Sti();

            Marker("RING3_PHASE15_LIFETIME_STARTED=1");
            HexMarker("RING3_PHASE15_LIFETIME_HANDLE=0x",
                sample.ProcessHandle.Value);
            HexMarker("RING3_PHASE15_LIFETIME_SLOT=0x", (ulong)sample.Slot);
            HexMarker("RING3_PHASE15_LIFETIME_GENERATION=0x",
                sample.Generation);

            int spins = 0;
            while (!process.IsTerminal && spins++ < 2000000) Native.Hlt();

            sample.Completed = process.IsTerminal;
            sample.State = process.State;
            sample.ExpectedState = expectFault
                ? process.State == Ring3ProcessState.Failed
                : process.State == Ring3ProcessState.Exiting;
            sample.DispatchValid = process.SchedulerDispatches >= 1 &&
                process.SchedulerCr3Valid && process.SchedulerRsp0Valid;
            sample.ResumeValid = !requirePreemption ||
                (process.TimerPreemptions > 0 &&
                 process.SchedulerDispatches >= 2 &&
                 process.UserRspPreserved);
            sample.ServiceRequestSucceeded =
                process.ServiceRequestsSucceeded > 0;
            sample.DataSentinelAfterRun = process.ReadUserDataSentinel();
            sample.FaultRecordValid = expectFault &&
                process.Fault.Process.Value == process.Handle.Value &&
                process.Fault.State == Ring3ProcessState.Failed;

            ApplicationServiceResult contextResult;
            ApplicationServiceRegistry.TryCreateContext(sample.Owner.Handle,
                out sample.ServiceContext, out contextResult);

            bool result = sample.Completed && sample.ExpectedState &&
                sample.DispatchValid && sample.ResumeValid &&
                sample.OwnerMatchesProcess &&
                (!expectFault || sample.FaultRecordValid) &&
                (kind != Ring3PayloadKind.Success ||
                    sample.ServiceRequestSucceeded);
            Marker(result ? "RING3_PHASE15_LIFETIME_RESULT=PASS" :
                "RING3_PHASE15_LIFETIME_RESULT=FAIL");

            if (!leaveLive) result = FinalizePhase15Lifetime(sample) && result;
            return result;
        }

        private static bool FinalizePhase15Lifetime(Phase15Lifetime sample) {
            if (sample == null) return false;
            bool processClean = sample.Process != null &&
                sample.Process.Cleanup();
            sample.CleanupComplete = processClean && sample.Process != null &&
                sample.Process.IsCleaned && sample.Process.UserThread == null &&
                sample.Process.Space == null &&
                sample.Process.OwningApplicationInstance == 0;
            sample.OwnerDetached = sample.Process == null ||
                sample.Process.OwningApplicationInstance == 0;
            sample.FaultStateCleared = sample.Process != null &&
                sample.Process.Fault.Process.Value == 0 &&
                sample.Process.ExitCode == 0;
            sample.StaleHandleRejected = sample.Process != null &&
                !sample.Process.TryResolveHandle(sample.ProcessHandle);
            sample.ThreadCleanup = sample.StaleThread == null ||
                (sample.StaleThread.Terminated &&
                 sample.StaleThread.OwnerProcess == null &&
                 !sample.StaleThread.IsUserThread &&
                 !ThreadPool.ContainsThread(sample.StaleThread) &&
                 sample.StaleThread.Stack == null &&
                 sample.StaleThread.KernelStackBase == 0);

            bool ownerTerminated = sample.Owner == null ||
                ApplicationInstanceRegistry.TryTerminate(sample.Owner,
                    "Phase 15 process lifetime cleanup");
            sample.Owner = null;
            if (sample.ServiceContext != null) {
                ApplicationInstance resolved;
                ApplicationServiceResult result;
                sample.StaleServiceContextRejected =
                    !ApplicationServiceRegistry.TryValidateContext(
                        sample.ServiceContext,
                        ApplicationServiceId.SystemInformation,
                        out resolved, out result) && resolved == null;
            } else {
                sample.StaleServiceContextRejected = true;
            }
            sample.Process = null;
            Marker(sample.CleanupComplete && sample.ThreadCleanup
                ? "RING3_PHASE15_CLEANUP_COMPLETE=1"
                : "RING3_PHASE15_CLEANUP_COMPLETE=0");
            return sample.CleanupComplete && sample.OwnerDetached &&
                ownerTerminated && sample.ThreadCleanup &&
                sample.StaleHandleRejected &&
                sample.StaleServiceContextRejected;
        }

        private static void RunPhase15() {
            Native.Cli();
            Marker("RING3_PHASE15_BEGIN=1");
            Marker("RING3_TSS_READY=1");
            Marker("RING3_TR_LOADED=1");
            Marker("RING3_RSP0_VALID=1");

            int baselineInstances = ApplicationInstanceRegistry.ActiveCount;
            int baselineStaleContexts =
                ApplicationServiceRegistry.StaleContextRejections;
            bool all = Ring3ProcessTable.GenerationRolloverSelfTest();
            Marker(all ? "RING3_GENERATION_ROLLOVER_SELFTEST=1" :
                "RING3_GENERATION_ROLLOVER_SELFTEST=0");

            Phase15Lifetime exitA;
            Phase15Lifetime exitB;
            bool exitACreated = RunPhase15Lifetime(
                Ring3PayloadKind.Success, false, true, false, out exitA);
            bool exitBCreated = RunPhase15Lifetime(
                Ring3PayloadKind.Success, false, true, true, out exitB);

            bool exitReuse = exitACreated && exitBCreated &&
                exitA.Slot == exitB.Slot &&
                exitA.Generation != exitB.Generation &&
                exitA.ProcessHandle.Value != exitB.ProcessHandle.Value &&
                exitA.OwnerHandle != exitB.OwnerHandle &&
                exitA.InitialDataSentinel == 0 &&
                exitA.DataSentinelAfterRun == Phase15Sentinel &&
                exitB.InitialDataSentinel == 0 &&
                exitB.UserDataMappedPhysical == exitB.UserDataPhysical &&
                exitB.DispatchValid && exitB.ResumeValid &&
                exitB.ServiceRequestSucceeded;

            bool staleProcessHandle = false;
            bool staleApplicationHandle = false;
            bool staleContext = false;
            bool staleThread = false;
            if (exitACreated && exitBCreated && exitB.Process != null) {
                Ring3Process resolvedA = Ring3ProcessTable.Resolve(
                    exitA.ProcessHandle);
                Ring3Process resolvedB = Ring3ProcessTable.Resolve(
                    exitB.ProcessHandle);
                staleProcessHandle = resolvedA == null &&
                    resolvedB == exitB.Process &&
                    Ring3ProcessTable.Slots[exitB.Slot] == exitB.Process;

                ApplicationInstance ignored;
                staleApplicationHandle =
                    !ApplicationInstanceRegistry.TryGet(
                        ApplicationInstanceHandle.FromValue(
                            exitA.OwnerHandle), out ignored) &&
                    ApplicationInstanceRegistry.TryGet(
                        ApplicationInstanceHandle.FromValue(
                            exitB.OwnerHandle), out ignored) &&
                    !ApplicationInstanceRegistry.TryTerminate(
                        ApplicationInstanceHandle.FromValue(
                            exitA.OwnerHandle),
                        "Phase 15 stale owner rejection");

                ApplicationServiceResult contextResult;
                ApplicationInstance contextOwner;
                staleContext = exitA.ServiceContext != null &&
                    !ApplicationServiceRegistry.TryValidateContext(
                        exitA.ServiceContext,
                        ApplicationServiceId.SystemInformation,
                        out contextOwner, out contextResult) &&
                    contextOwner == null;

                staleThread = exitA.StaleThread != null &&
                    exitA.StaleThread.Terminated &&
                    exitA.StaleThread.OwnerProcess == null &&
                    !exitA.StaleThread.IsUserThread &&
                    !ThreadPool.ContainsThread(exitA.StaleThread) &&
                    exitA.StaleThread != exitB.StaleThread;
            }
            Marker(staleProcessHandle ?
                "RING3_STALE_A_HANDLE_REJECTED=1" :
                "RING3_STALE_A_HANDLE_REJECTED=0");
            Marker(staleApplicationHandle ?
                "RING3_STALE_A_APP_HANDLE_REJECTED=1" :
                "RING3_STALE_A_APP_HANDLE_REJECTED=0");
            Marker(staleContext ?
                "RING3_STALE_A_SERVICE_CONTEXT_REJECTED=1" :
                "RING3_STALE_A_SERVICE_CONTEXT_REJECTED=0");
            Marker(staleThread ?
                "RING3_STALE_A_THREAD_REJECTED=1" :
                "RING3_STALE_A_THREAD_REJECTED=0");

            bool privateMemory = exitACreated && exitBCreated &&
                exitA.CleanupComplete && exitB.InitialDataSentinel == 0 &&
                exitB.UserDataMappedPhysical == exitB.UserDataPhysical &&
                (exitA.UserDataPhysical != exitB.UserDataPhysical ||
                 exitB.InitialDataSentinel != exitA.DataSentinelAfterRun);
            bool cr3Isolation = exitACreated && exitBCreated &&
                exitA.Cr3 != 0 && exitB.Cr3 != 0 &&
                exitB.DispatchValid && exitB.Cr3 != 0;
            bool stackIsolation = exitACreated && exitBCreated &&
                exitA.KernelStackTop != 0 && exitB.KernelStackTop != 0 &&
                exitB.KernelStackTop == exitB.StaleThread.KernelStackTop;
            Marker(privateMemory ?
                "RING3_CROSS_PROCESS_PRIVATE_MEMORY_REJECTED=1" :
                "RING3_CROSS_PROCESS_PRIVATE_MEMORY_REJECTED=0");
            Marker(cr3Isolation ? "RING3_CR3_GENERATION_SAFE=1" :
                "RING3_CR3_GENERATION_SAFE=0");
            Marker(stackIsolation ? "RING3_KERNEL_STACK_GENERATION_SAFE=1" :
                "RING3_KERNEL_STACK_GENERATION_SAFE=0");
            bool exitBFinalized = FinalizePhase15Lifetime(exitB);
            exitA.ServiceContext = null;
            exitB.ServiceContext = null;
            Marker(exitA.Slot == exitB.Slot ?
                "RING3_PHASE15_EXIT_SLOT_REUSED=1" :
                "RING3_PHASE15_EXIT_SLOT_REUSED=0");
            Marker(exitA.Generation != exitB.Generation ?
                "RING3_PHASE15_EXIT_GENERATION_CHANGED=1" :
                "RING3_PHASE15_EXIT_GENERATION_CHANGED=0");
            Marker(exitA.InitialDataSentinel == 0 &&
                exitB.InitialDataSentinel == 0 ?
                "RING3_PHASE15_EXIT_FRESH_DATA=1" :
                "RING3_PHASE15_EXIT_FRESH_DATA=0");
            Marker(exitA.DataSentinelAfterRun == Phase15Sentinel ?
                "RING3_PHASE15_EXIT_A_SENTINEL_WRITTEN=1" :
                "RING3_PHASE15_EXIT_A_SENTINEL_WRITTEN=0");
            Marker(exitB.UserDataMappedPhysical == exitB.UserDataPhysical ?
                "RING3_PHASE15_EXIT_B_MAPPING_VALID=1" :
                "RING3_PHASE15_EXIT_B_MAPPING_VALID=0");
            Marker(exitB.DispatchValid && exitB.ResumeValid &&
                exitB.ServiceRequestSucceeded ?
                "RING3_PHASE15_EXIT_B_EXECUTION_VALID=1" :
                "RING3_PHASE15_EXIT_B_EXECUTION_VALID=0");
            Marker(exitBFinalized ?
                "RING3_PHASE15_EXIT_B_CLEANUP_VALID=1" :
                "RING3_PHASE15_EXIT_B_CLEANUP_VALID=0");
            bool exitScenario = exitReuse && staleProcessHandle &&
                staleApplicationHandle && staleContext && staleThread &&
                privateMemory && cr3Isolation && stackIsolation &&
                exitBFinalized;
            Marker(exitScenario ? "RING3_A_EXIT_B_REUSE_PASS=1" :
                "RING3_A_EXIT_B_REUSE_PASS=0");
            all = all && exitScenario;

            Phase15Lifetime faultA;
            Phase15Lifetime faultB;
            bool faultACreated = RunPhase15Lifetime(
                Ring3PayloadKind.DeliberateFault, true, false, false,
                out faultA);
            bool faultBCreated = RunPhase15Lifetime(
                Ring3PayloadKind.Success, false, true, true, out faultB);
            bool faultToSuccess = faultACreated && faultBCreated &&
                faultA.FaultRecordValid && faultA.FaultStateCleared &&
                faultA.CleanupComplete && faultA.ThreadCleanup &&
                faultB.DispatchValid && faultB.ServiceRequestSucceeded &&
                faultB.OwnerMatchesProcess;
            bool faultBFinalized = FinalizePhase15Lifetime(faultB);
            faultA.ServiceContext = null;
            faultB.ServiceContext = null;
            Marker(faultToSuccess && faultBFinalized ?
                "RING3_A_FAULT_B_SUCCESS_PASS=1" :
                "RING3_A_FAULT_B_SUCCESS_PASS=0");
            Marker(faultToSuccess && faultA.FaultStateCleared ?
                "RING3_FAULT_STATE_RESET=1" :
                "RING3_FAULT_STATE_RESET=0");
            all = all && faultToSuccess && faultBFinalized;

            Ring3PayloadKind[] repeatedKinds = new Ring3PayloadKind[] {
                Ring3PayloadKind.InvalidInput,
                Ring3PayloadKind.InvalidServiceBuffers,
                Ring3PayloadKind.DeliberateFault,
                Ring3PayloadKind.Success
            };
            int repeatedCount = 0;
            bool repeated = true;
            for (int i = 0; i < repeatedKinds.Length; i++) {
                Phase15Lifetime generation;
                bool expectFault = repeatedKinds[i] ==
                    Ring3PayloadKind.DeliberateFault;
                bool one = RunPhase15Lifetime(repeatedKinds[i], expectFault,
                    repeatedKinds[i] == Ring3PayloadKind.Success, false,
                    out generation);
                repeated = repeated && one && generation.CleanupComplete &&
                    generation.ThreadCleanup && generation.StaleHandleRejected;
                if (one) repeatedCount++;
                generation.ServiceContext = null;
            }
            NumberMarker("RING3_PHASE15_REPEATED_GENERATIONS=", repeatedCount);
            Marker(repeated && repeatedCount == repeatedKinds.Length ?
                "RING3_PHASE15_REUSE_PASS=1" :
                "RING3_PHASE15_REUSE_PASS=0");
            all = all && repeated && repeatedCount == repeatedKinds.Length;

            bool appClean = ApplicationInstanceRegistry.ActiveCount ==
                baselineInstances && ApplicationInstanceRegistry.ObservationCount ==
                baselineInstances;
            bool serviceClean = ApplicationServiceRegistry.ActiveRequestCount == 0 &&
                ApplicationServiceRegistry.TransientWindowCount == 0 &&
                ApplicationServiceRegistry.OrphanTransientWindowCount == 0 &&
                ApplicationServiceRegistry.StaleContextRejections >
                    baselineStaleContexts;
            bool processClean = Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.LiveAddressSpaces == 0 &&
                Ring3ProcessDiagnostics.LiveUserMappings == 0 &&
                Ring3ProcessDiagnostics.LiveKernelStacks == 0 &&
                Ring3ProcessDiagnostics.IsBalanced;

            NumberMarker("RING3_PHASE15_ADDRESS_SPACES_CREATED=",
                Ring3ProcessDiagnostics.AddressSpacesCreated);
            NumberMarker("RING3_PHASE15_ADDRESS_SPACES_RECLAIMED=",
                Ring3ProcessDiagnostics.AddressSpacesReclaimed);
            NumberMarker("RING3_PHASE15_PAGE_TABLES_CREATED=",
                Ring3ProcessDiagnostics.PageTablesCreated);
            NumberMarker("RING3_PHASE15_PAGE_TABLES_RECLAIMED=",
                Ring3ProcessDiagnostics.PageTablesReclaimed);
            NumberMarker("RING3_PHASE15_KERNEL_STACKS_CREATED=",
                Ring3ProcessDiagnostics.KernelStacksCreated);
            NumberMarker("RING3_PHASE15_KERNEL_STACKS_RECLAIMED=",
                Ring3ProcessDiagnostics.KernelStacksReclaimed);
            int userPagesCreated =
                Ring3ProcessDiagnostics.UserCodePagesCreated +
                Ring3ProcessDiagnostics.UserDataPagesCreated +
                Ring3ProcessDiagnostics.UserStackPagesCreated;
            int userPagesReclaimed =
                Ring3ProcessDiagnostics.UserCodePagesReclaimed +
                Ring3ProcessDiagnostics.UserDataPagesReclaimed +
                Ring3ProcessDiagnostics.UserStackPagesReclaimed;
            NumberMarker("RING3_PHASE15_USER_CODE_PAGES_CREATED=",
                Ring3ProcessDiagnostics.UserCodePagesCreated);
            NumberMarker("RING3_PHASE15_USER_CODE_PAGES_RECLAIMED=",
                Ring3ProcessDiagnostics.UserCodePagesReclaimed);
            NumberMarker("RING3_PHASE15_USER_DATA_PAGES_CREATED=",
                Ring3ProcessDiagnostics.UserDataPagesCreated);
            NumberMarker("RING3_PHASE15_USER_DATA_PAGES_RECLAIMED=",
                Ring3ProcessDiagnostics.UserDataPagesReclaimed);
            NumberMarker("RING3_PHASE15_USER_STACK_PAGES_CREATED=",
                Ring3ProcessDiagnostics.UserStackPagesCreated);
            NumberMarker("RING3_PHASE15_USER_STACK_PAGES_RECLAIMED=",
                Ring3ProcessDiagnostics.UserStackPagesReclaimed);
            NumberMarker("RING3_PHASE15_USER_PAGES_CREATED=",
                userPagesCreated);
            NumberMarker("RING3_PHASE15_USER_PAGES_RECLAIMED=",
                userPagesReclaimed);
            NumberMarker("RING3_PHASE15_USER_MAPPINGS_LIVE=",
                Ring3ProcessDiagnostics.LiveUserMappings);
            NumberMarker("RING3_PHASE15_LIVE_PROCESS_HANDLES=",
                Ring3ProcessTable.LiveCount);
            NumberMarker("RING3_PHASE15_LIVE_USER_THREADS=",
                ThreadPool.LiveUserThreadCount);
            NumberMarker("RING3_PHASE15_DIAGNOSTIC_INSTANCES=",
                ApplicationInstanceRegistry.ActiveCount - baselineInstances);
            Marker(processClean ? "RING3_PHASE15_PROCESS_CLEANUP_BALANCED=1" :
                "RING3_PHASE15_PROCESS_CLEANUP_BALANCED=0");
            Marker(appClean ? "RING3_PHASE15_APP_MODEL_CLEAN=1" :
                "RING3_PHASE15_APP_MODEL_CLEAN=0");
            Marker(serviceClean ? "RING3_PHASE15_SERVICE_CLEAN=1" :
                "RING3_PHASE15_SERVICE_CLEAN=0");
            Marker(Allocator.FreeFailCorruptRun == 0 ?
                "RING3_PHASE15_ALLOCATOR_CORRUPTION=0" :
                "RING3_PHASE15_ALLOCATOR_CORRUPTION=1");
            Marker(Allocator.FreeFailNoPages == 0 ?
                "RING3_PHASE15_ALLOCATOR_EXHAUSTION=0" :
                "RING3_PHASE15_ALLOCATOR_EXHAUSTION=1");
            Marker(ThreadPool.Locked ? "RING3_PHASE15_THREADPOOL_LOCKED=1" :
                "RING3_PHASE15_THREADPOOL_LOCKED=0");

            bool final = all && processClean && appClean && serviceClean &&
                Ring3ProcessDiagnostics.StaleHandleRejections > 0 &&
                Allocator.FreeFailCorruptRun == 0 &&
                Allocator.FreeFailNoPages == 0 && !ThreadPool.Locked;
            Marker(final ? "RING3_PHASE15_COMPLETE=1" :
                "RING3_PHASE15_COMPLETE=0");
            _awaitingDesktopHeartbeat = true;
            int desktopSpins = 0;
            while (!_desktopHeartbeatObserved && desktopSpins++ < 2000000)
                Native.Hlt();
            Marker(_desktopHeartbeatObserved ?
                "RING3_PHASE15_DESKTOP_HEARTBEAT=1" :
                "RING3_PHASE15_DESKTOP_HEARTBEAT=0");
            Native.Sti();
            for (;;) Native.Hlt();
        }

        private static void Run() {
            if (!_direct) Native.Cli();
            Marker("RING3_PROOF_BEGIN=1");
            Marker("RING3_TSS_READY=1");
            Marker("RING3_TR_LOADED=1");
            Marker("RING3_RSP0_VALID=1");

            bool success;
            bool invalid;
            bool serviceInvalid;
            bool fault;
            if (_direct) {
                success = RunOneDirect(Ring3PayloadKind.Success, false);
                invalid = RunOneDirect(Ring3PayloadKind.InvalidInput, false);
                serviceInvalid = true;
                fault = RunOneDirect(Ring3PayloadKind.DeliberateFault, true);
            } else {
                bool owner = TryCreateOwner();
                if (!owner) Native.Sti();
                success = owner && RunOneScheduled(Ring3PayloadKind.Success,
                    false, true);
                invalid = owner && RunOneScheduled(Ring3PayloadKind.InvalidInput,
                    false, false);
                serviceInvalid = owner && RunOneScheduled(
                    Ring3PayloadKind.InvalidServiceBuffers, false, false);
                fault = owner && RunOneScheduled(
                    Ring3PayloadKind.DeliberateFault, true, false);
                CleanupOwner();
            }

            if (!_direct) {
                // Return once through the saved desktop context before
                // publishing the continuation marker. This makes desktop
                // survival an observed scheduler transition, not a claim
                // emitted by the proof thread while it still owns the CPU.
                _awaitingDesktopHeartbeat = true;
                int desktopSpins = 0;
                while (!_desktopHeartbeatObserved && desktopSpins++ < 2000000)
                    Native.Hlt();
            }
            Marker("RING3_KERNEL_HEARTBEAT_CONTINUED=1");
            bool desktopHeartbeat = _direct || _desktopHeartbeatObserved;
            Marker(desktopHeartbeat ?
                "RING3_DESKTOP_HEARTBEAT_RESULT=PASS" :
                "RING3_DESKTOP_HEARTBEAT_RESULT=FAIL");
            bool proofComplete = success && invalid && fault &&
                (_direct || serviceInvalid) && desktopHeartbeat;
            Marker(proofComplete ?
                "RING3_PROOF_COMPLETE=1" : "RING3_PROOF_COMPLETE=0");
            if (_direct) return;
            // Keep the diagnostic kernel thread as a normal scheduler
            // participant so the desktop and timer continue after the proof.
            for (;;) Native.Hlt();
        }
    }
}
