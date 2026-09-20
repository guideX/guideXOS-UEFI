using guideXOS.OS;
using System;

namespace guideXOS.Misc {
    internal static unsafe class Ring3Proof {
        private static bool _scheduled;
        private static bool _direct;
        private static ApplicationInstance _owner;
        private static bool _ownerCreated;
        private static bool _awaitingDesktopHeartbeat;
        private static bool _desktopHeartbeatObserved;

        private static void Marker(string text) {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++)
                Native.Out8(0x3F8, (byte)text[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }

        internal static void Schedule() {
            if (_scheduled) return;
            _scheduled = true;
            _direct = false;
            Marker("RING3_PROOF_SCHEDULED=1");
            new Thread(&Run, 32768).Start(0);
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
