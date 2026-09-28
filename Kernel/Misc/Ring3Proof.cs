using guideXOS.OS;
using guideXOS.GUI;
using Internal.Runtime.CompilerServices;
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
        private static bool _phase24Scheduled;
        private static bool _phase25Scheduled;
        private static bool _phase26Scheduled;
        private static bool _phase27Scheduled;
        private static bool _phase28Scheduled;
        private static bool _phase29Scheduled;
        private static bool _phase30Scheduled;
        private static bool _phase31Scheduled;
        private static bool _phase32Scheduled;

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

        // Phase 24 intentionally stops at the real kernel mapping boundary.
        // The exact Phase 23 image has no reviewed native-only bootstrap, so
        // this selector never dispatches wmain or any managed instruction.
        internal static void SchedulePhase24() {
            if (_phase24Scheduled) return;
            _phase24Scheduled = true;
            Marker("PHASE24_SCHEDULED=1");
            new Thread(&RunPhase24, 32768).Start(0);
        }

        internal static void SchedulePhase25() {
            if (_phase25Scheduled) return;
            _phase25Scheduled = true;
            Marker("PHASE25_SCHEDULED=1");
            new Thread(&RunPhase25, 131072).Start(0);
        }

        internal static void SchedulePhase26() {
            if (_phase26Scheduled) return;
            _phase26Scheduled = true;
            Marker("PHASE26_SCHEDULED=1");
            new Thread(&RunPhase26, 131072).Start(0);
        }

        private static bool RunOnePhase26Lifetime(out Ring3Process process) {
            process = null;
            Native.Cli();
            string failure;
            if (!Ring3Process.TryCreateManagedEntry(0, out process, out failure) ||
                process == null) {
                Marker("PHASE26_PROCESS_CREATE_FAILED=1");
                if (failure != null) Marker("PHASE26_PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool authorized = process.ManagedImage != null &&
                process.ManagedImage.TryEnterManagedEntry();
            bool started = process.StartManagedBootstrap();
            if (started) Native.Sti();
            int spins = 0;
            while (started && !process.IsTerminal && spins++ < 4000000)
                Native.Hlt();
            bool completed = process.IsTerminal;
            bool dispatched = process.SchedulerDispatches >= 1 &&
                process.SchedulerCr3Valid && process.SchedulerRsp0Valid;
            bool result = process.BootstrapResultSucceeded;
            bool mainReturned = process.BootstrapReturnCode == 42 &&
                process.ExitCode == 42;
            HexMarker("PHASE26_BOOTSTRAP_FLAGS=0x",
                (ulong)process.BootstrapResultFlags);
            HexMarker("PHASE26_BOOTSTRAP_RETURN=0x",
                (ulong)(uint)process.BootstrapReturnCode);
            HexMarker("PHASE26_EXIT_CODE=0x",
                (ulong)(uint)process.ExitCode);
            bool clean = process.Cleanup();
            bool staleHandle = !process.TryResolveHandle(process.Handle);
            Marker(scaffold ? "PHASE26_SCAFFOLD_PASS=1" :
                "PHASE26_SCAFFOLD_PASS=0");
            Marker(authorized ? "PHASE26_ENTRY_AUTHORIZED=1" :
                "PHASE26_ENTRY_AUTHORIZED=0");
            Marker(dispatched ? "PHASE26_DISPATCH_PASS=1" :
                "PHASE26_DISPATCH_PASS=0");
            Marker(completed && result ? "PHASE26_BOOTSTRAP_RESULT_PASS=1" :
                "PHASE26_BOOTSTRAP_RESULT_PASS=0");
            Marker(mainReturned ? "PHASE26_MAIN_RETURN_42=1" :
                "PHASE26_MAIN_RETURN_42=0");
            Marker(clean && staleHandle ? "PHASE26_LIFETIME_CLEAN=1" :
                "PHASE26_LIFETIME_CLEAN=0");
            return started && completed && scaffold && authorized && dispatched &&
                result && mainReturned && clean && staleHandle;
        }

        private static void RunPhase26() {
            Native.Cli();
            Marker("PHASE26_BEGIN=1");
            Marker("PHASE26_MANAGED_ENTRY_READY=0");
            bool first = RunOnePhase26Lifetime(out _);
            bool second = RunOnePhase26Lifetime(out _);
            bool third = RunOnePhase26Lifetime(out _);
            bool fourth = RunOnePhase26Lifetime(out _);
            Marker(first && second && third && fourth ?
                "PHASE26_REPEATED_LIFETIMES=4" :
                "PHASE26_REPEATED_LIFETIMES=0");
            Marker(ManagedImageDiagnostics.IsBalanced ?
                "PHASE26_MANAGED_CLEANUP_BALANCED=1" :
                "PHASE26_MANAGED_CLEANUP_BALANCED=0");
            Marker(NativeBootstrapDiagnostics.IsBalanced ?
                "PHASE26_BOOTSTRAP_CLEANUP_BALANCED=1" :
                "PHASE26_BOOTSTRAP_CLEANUP_BALANCED=0");
            Marker(Ring3ProcessTable.LiveCount == 0 &&
                   ThreadPool.LiveUserThreadCount == 0 &&
                   Ring3ProcessDiagnostics.IsBalanced ?
                "PHASE26_PROCESS_CLEANUP_BALANCED=1" :
                "PHASE26_PROCESS_CLEANUP_BALANCED=0");
            bool complete = first && second && third && fourth &&
                ManagedImageDiagnostics.IsBalanced &&
                NativeBootstrapDiagnostics.IsBalanced &&
                Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced;
            Marker(complete ? "RING3_PHASE26_COMPLETE=1" :
                "RING3_PHASE26_COMPLETE=0");
            Native.Sti();
        }

        internal static void SchedulePhase27() {
            if (_phase27Scheduled) return;
            _phase27Scheduled = true;
            Marker("PHASE27_SCHEDULED=1");
            new Thread(&RunPhase27, 131072).Start(0);
        }

        private static bool RunOnePhase27Lifetime(bool failureMode,
                                                   out Ring3Process process) {
            process = null;
            if (_owner == null) return false;
            Native.Cli();
            string failure;
            if (!Ring3Process.TryCreateManagedServiceEntry(
                    _owner.Handle.Value, failureMode, out process, out failure) ||
                process == null) {
                Native.Sti();
                Marker("PHASE27_PROCESS_CREATE_FAILED=1");
                if (failure != null) Marker("PHASE27_PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }
            Ring3ProcessHandle oldHandle = process.Handle;
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool authorized = process.ManagedImage != null &&
                process.ManagedImage.TryEnterManagedEntry();
            bool started = process.StartManagedBootstrap();
            if (started) Native.Sti();
            int spins = 0;
            while (started && !process.IsTerminal && spins++ < 6000000)
                Native.Hlt();

            bool completed = process.IsTerminal;
            bool dispatched = process.SchedulerDispatches >= 1 &&
                process.SchedulerCr3Valid && process.SchedulerRsp0Valid;
            bool resumed = process.TimerPreemptions > 0 &&
                process.SchedulerDispatches >= 2 && process.UserRspPreserved;
            bool service = failureMode ? process.ServiceRequestsSucceeded == 0 :
                process.ServiceRequestsSucceeded > 0;
            int expected = failureMode ? 21 : 27;
            bool result = process.BootstrapResultSucceeded;
            bool mainReturned = process.BootstrapReturnCode == expected &&
                process.ExitCode == expected;
            HexMarker("PHASE27_BOOTSTRAP_RETURN=0x",
                (ulong)(uint)process.BootstrapReturnCode);
            HexMarker("PHASE27_EXIT_CODE=0x", (ulong)(uint)process.ExitCode);
            bool clean = process.Cleanup();
            bool staleHandle = !process.TryResolveHandle(oldHandle);
            Marker(scaffold ? "PHASE27_SCAFFOLD_PASS=1" :
                "PHASE27_SCAFFOLD_PASS=0");
            Marker(authorized ? "PHASE27_ENTRY_AUTHORIZED=1" :
                "PHASE27_ENTRY_AUTHORIZED=0");
            Marker(dispatched ? "PHASE27_DISPATCH_PASS=1" :
                "PHASE27_DISPATCH_PASS=0");
            Marker(resumed ? "PHASE27_RESUME_PASS=1" :
                "PHASE27_RESUME_PASS=0");
            Marker(service ? (failureMode ? "PHASE27_TYPED_FAILURE_PASS=1" :
                              "PHASE27_SERVICE_REQUEST_PASS=1") :
                (failureMode ? "PHASE27_TYPED_FAILURE_PASS=0" :
                              "PHASE27_SERVICE_REQUEST_PASS=0"));
            Marker(completed && result && mainReturned ?
                "PHASE27_MAIN_RESULT_PASS=1" : "PHASE27_MAIN_RESULT_PASS=0");
            Marker(clean && staleHandle ? "PHASE27_LIFETIME_CLEAN=1" :
                "PHASE27_LIFETIME_CLEAN=0");
            return started && completed && scaffold && authorized &&
                dispatched && resumed && service && result && mainReturned &&
                clean && staleHandle;
        }

        private static void RunPhase27() {
            Native.Cli();
            Marker("PHASE27_BEGIN=1");
            bool owner = TryCreateOwner();
            ApplicationServiceContext context = null;
            ApplicationServiceResult contextResult;
            if (owner) {
                ApplicationServiceRegistry.TryCreateContext(_owner.Handle,
                    out context, out contextResult);
            }
            bool first = owner && RunOnePhase27Lifetime(false, out _);
            bool second = owner && RunOnePhase27Lifetime(false, out _);
            bool third = owner && RunOnePhase27Lifetime(false, out _);
            bool fourth = owner && RunOnePhase27Lifetime(false, out _);
            bool typedFailure = owner &&
                RunOnePhase27Lifetime(true, out _);
            ulong staleOwnerHandle = owner ? _owner.Handle.Value : 0UL;
            CleanupOwner();

            ApplicationInstance ignored;
            ApplicationInstance staleContextOwner;
            ApplicationServiceResult staleResult;
            bool staleApplication = staleOwnerHandle != 0 &&
                !ApplicationInstanceRegistry.TryGet(
                    ApplicationInstanceHandle.FromValue(staleOwnerHandle),
                    out ignored);
            bool staleContext = context != null &&
                !ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.SystemInformation,
                    out staleContextOwner, out staleResult) &&
                staleContextOwner == null;
            bool newOwner = TryCreateOwner();
            bool newGeneration = newOwner && _owner.Handle.Value != staleOwnerHandle;
            CleanupOwner();

            Marker(first && second && third && fourth ?
                "PHASE27_REPEATED_LIFETIMES=4" :
                "PHASE27_REPEATED_LIFETIMES=0");
            Marker(typedFailure ? "PHASE27_TYPED_FAILURE_RESULT=21" :
                "PHASE27_TYPED_FAILURE_RESULT=0");
            Marker(staleApplication && newGeneration ?
                "PHASE27_STALE_APPLICATION_INSTANCE_REJECTED=1" :
                "PHASE27_STALE_APPLICATION_INSTANCE_REJECTED=0");
            Marker(staleContext ? "PHASE27_STALE_SERVICE_CONTEXT_REJECTED=1" :
                "PHASE27_STALE_SERVICE_CONTEXT_REJECTED=0");
            Marker(ManagedImageDiagnostics.IsBalanced ?
                "PHASE27_MANAGED_CLEANUP_BALANCED=1" :
                "PHASE27_MANAGED_CLEANUP_BALANCED=0");
            Marker(NativeBootstrapDiagnostics.IsBalanced ?
                "PHASE27_BOOTSTRAP_CLEANUP_BALANCED=1" :
                "PHASE27_BOOTSTRAP_CLEANUP_BALANCED=0");
            Marker(Ring3ProcessTable.LiveCount == 0 &&
                   ThreadPool.LiveUserThreadCount == 0 &&
                   Ring3ProcessDiagnostics.IsBalanced ?
                "PHASE27_PROCESS_CLEANUP_BALANCED=1" :
                "PHASE27_PROCESS_CLEANUP_BALANCED=0");
            bool complete = first && second && third && fourth &&
                typedFailure && staleApplication && staleContext &&
                newGeneration && ManagedImageDiagnostics.IsBalanced &&
                NativeBootstrapDiagnostics.IsBalanced &&
                Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced;
            Marker(complete ? "RING3_PHASE27_COMPLETE=1" :
                "RING3_PHASE27_COMPLETE=0");
            Native.Sti();
        }

        internal static void SchedulePhase28() {
            if (_phase28Scheduled) return;
            _phase28Scheduled = true;
            Marker("PHASE28_SCHEDULED=1");
            new Thread(&RunPhase28, 131072).Start(0);
        }

        private static bool RunOnePhase28Lifetime(int payloadKind,
                                                   out Ring3Process process) {
            process = null;
            if (_owner == null) return false;
            Native.Cli();
            string failure;
            if (!Ring3Process.TryCreateManagedSdkEntry(
                    _owner.Handle.Value, payloadKind, out process, out failure) ||
                process == null) {
                Native.Sti();
                Marker("PHASE28_PROCESS_CREATE_FAILED=1");
                if (failure != null) Marker("PHASE28_PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }
            Ring3ProcessHandle oldHandle = process.Handle;
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool authorized = process.ManagedImage != null &&
                process.ManagedImage.TryEnterManagedEntry();
            bool started = process.StartManagedBootstrap();
            if (started) Native.Sti();
            int spins = 0;
            while (started && !process.IsTerminal && spins++ < 6000000)
                Native.Hlt();

            bool completed = process.IsTerminal;
            bool dispatched = process.SchedulerDispatches >= 1 &&
                process.SchedulerCr3Valid && process.SchedulerRsp0Valid;
            bool resumed = payloadKind <= 2 && process.TimerPreemptions > 0 &&
                process.SchedulerDispatches >= 2 && process.UserRspPreserved;
            ApplicationInstanceHandle processOwner =
                ApplicationInstanceHandle.FromValue(
                    process.OwningApplicationInstance);
            bool identity = payloadKind > 2 ||
                (process.ApplicationIdentityRequestsSucceeded > 0 &&
                 process.LastIdentityStableApplicationId != 0 &&
                 process.LastIdentityLifetimeToken != 0 &&
                 process.LastIdentityApplicationGeneration ==
                    processOwner.Generation &&
                 process.LastIdentityProcessGeneration ==
                    process.Handle.Generation);
            bool service = payloadKind > 2 ||
                (payloadKind == 1
                    ? process.ServiceRequestsSucceeded > 0
                    : process.ServiceRequestsSucceeded == 0);
            int expected = payloadKind == 1 ? 28 :
                (payloadKind == 2 ? 23 : (payloadKind == 3 ? 73 : -1));
            bool managedResult = payloadKind <= 2 &&
                process.BootstrapResultSucceeded &&
                process.BootstrapReturnCode == expected &&
                process.ExitCode == expected;
            bool terminalResult = payloadKind == 3 || payloadKind == 4
                ? process.ExitCode == expected
                : managedResult;
            bool state = process.State == Ring3ProcessState.Exiting ||
                process.State == Ring3ProcessState.Exited;
            bool clean = process.Cleanup();
            bool staleHandle = !process.TryResolveHandle(oldHandle);
            Marker(scaffold ? "PHASE28_SCAFFOLD_PASS=1" :
                "PHASE28_SCAFFOLD_PASS=0");
            Marker(authorized ? "PHASE28_ENTRY_AUTHORIZED=1" :
                "PHASE28_ENTRY_AUTHORIZED=0");
            Marker(dispatched ? "PHASE28_DISPATCH_PASS=1" :
                "PHASE28_DISPATCH_PASS=0");
            Marker(resumed ? "PHASE28_RESUME_PASS=1" :
                (payloadKind > 2 ? "PHASE28_RESUME_PASS=NA" :
                    "PHASE28_RESUME_PASS=0"));
            Marker(identity ? "PHASE28_IDENTITY_PASS=1" :
                "PHASE28_IDENTITY_PASS=0");
            Marker(service ? "PHASE28_SERVICE_PASS=1" :
                (payloadKind > 2 ? "PHASE28_SERVICE_PASS=NA" :
                    "PHASE28_SERVICE_PASS=0"));
            Marker(terminalResult && state ? "PHASE28_MAIN_RESULT_PASS=1" :
                "PHASE28_MAIN_RESULT_PASS=0");
            Marker(clean && staleHandle ? "PHASE28_LIFETIME_CLEAN=1" :
                "PHASE28_LIFETIME_CLEAN=0");
            return started && completed && scaffold && authorized &&
                dispatched && identity && service && terminalResult && state &&
                clean && staleHandle &&
                (payloadKind > 2 || resumed);
        }

        private static bool DistinctPhase28IdentityLifetimes(
                Ring3Process first, Ring3Process second) {
            return first != null && second != null &&
                first.LastIdentityLifetimeToken != 0 &&
                second.LastIdentityLifetimeToken != 0 &&
                first.LastIdentityLifetimeToken !=
                    second.LastIdentityLifetimeToken &&
                first.LastIdentityStableApplicationId ==
                    second.LastIdentityStableApplicationId &&
                first.LastIdentityApplicationGeneration ==
                    second.LastIdentityApplicationGeneration &&
                first.LastIdentityProcessGeneration != 0 &&
                second.LastIdentityProcessGeneration != 0 &&
                first.LastIdentityProcessGeneration !=
                    second.LastIdentityProcessGeneration;
        }

        private static void RunPhase28() {
            Native.Cli();
            Marker("PHASE28_BEGIN=1");
            bool owner = TryCreateOwner();
            ApplicationServiceContext context = null;
            ApplicationServiceResult contextResult;
            if (owner) {
                ApplicationServiceRegistry.TryCreateContext(_owner.Handle,
                    out context, out contextResult);
            }
            Ring3Process firstProcess = null;
            Ring3Process secondProcess = null;
            Ring3Process thirdProcess = null;
            Ring3Process fourthProcess = null;
            bool first = owner && RunOnePhase28Lifetime(1,
                out firstProcess);
            bool second = owner && RunOnePhase28Lifetime(1,
                out secondProcess);
            bool third = owner && RunOnePhase28Lifetime(1,
                out thirdProcess);
            bool fourth = owner && RunOnePhase28Lifetime(1,
                out fourthProcess);
            bool typedFailure = owner && RunOnePhase28Lifetime(2, out _);
            bool exit = owner && RunOnePhase28Lifetime(3, out _);
            bool failFast = owner && RunOnePhase28Lifetime(4, out _);
            Ring3Process replacementProcess = null;
            bool replacement = owner && RunOnePhase28Lifetime(1,
                out replacementProcess);
            bool repeatedIdentitiesDistinct =
                DistinctPhase28IdentityLifetimes(firstProcess, secondProcess) &&
                DistinctPhase28IdentityLifetimes(firstProcess, thirdProcess) &&
                DistinctPhase28IdentityLifetimes(firstProcess, fourthProcess) &&
                DistinctPhase28IdentityLifetimes(secondProcess, thirdProcess) &&
                DistinctPhase28IdentityLifetimes(secondProcess, fourthProcess) &&
                DistinctPhase28IdentityLifetimes(thirdProcess, fourthProcess);

            ulong staleOwnerHandle = owner ? _owner.Handle.Value : 0UL;
            ulong staleLifetimeToken = replacementProcess == null ? 0UL :
                replacementProcess.LastIdentityLifetimeToken;
            ulong stableApplicationId = replacementProcess == null ? 0UL :
                replacementProcess.LastIdentityStableApplicationId;
            CleanupOwner();

            ApplicationInstance ignored;
            ApplicationInstance staleContextOwner;
            ApplicationServiceResult staleResult;
            bool staleApplication = staleOwnerHandle != 0 &&
                !ApplicationInstanceRegistry.TryGet(
                    ApplicationInstanceHandle.FromValue(staleOwnerHandle),
                    out ignored);
            bool staleContext = context != null &&
                !ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.SystemInformation,
                    out staleContextOwner, out staleResult) &&
                staleContextOwner == null;
            bool newOwner = TryCreateOwner();
            bool newGeneration = newOwner && _owner.Handle.Value !=
                staleOwnerHandle;
            Ring3Process newGenerationProcess = null;
            bool newGenerationLifetime = newGeneration &&
                RunOnePhase28Lifetime(1, out newGenerationProcess) &&
                newGenerationProcess != null && staleLifetimeToken != 0 &&
                newGenerationProcess.LastIdentityLifetimeToken != 0 &&
                newGenerationProcess.LastIdentityLifetimeToken !=
                    staleLifetimeToken &&
                newGenerationProcess.LastIdentityStableApplicationId ==
                    stableApplicationId &&
                newGenerationProcess.LastIdentityApplicationGeneration ==
                    _owner.Handle.Generation &&
                newGenerationProcess.LastIdentityProcessGeneration != 0;
            CleanupOwner();

            Marker(first && second && third && fourth && replacement ?
                "PHASE28_REPEATED_LIFETIMES=4" :
                "PHASE28_REPEATED_LIFETIMES=0");
            Marker(repeatedIdentitiesDistinct ?
                "PHASE28_LIFETIME_IDENTITIES_DISTINCT=1" :
                "PHASE28_LIFETIME_IDENTITIES_DISTINCT=0");
            Marker(typedFailure ? "PHASE28_TYPED_FAILURE_RESULT=VersionMismatch" :
                "PHASE28_TYPED_FAILURE_RESULT=0");
            Marker(exit ? "PHASE28_EXIT_PASS=1" : "PHASE28_EXIT_PASS=0");
            Marker(failFast ? "PHASE28_FAILFAST_PASS=1" :
                "PHASE28_FAILFAST_PASS=0");
            Marker(newGenerationLifetime ?
                "PHASE28_NEW_GENERATION_IDENTITY_PASS=1" :
                "PHASE28_NEW_GENERATION_IDENTITY_PASS=0");
            Marker(staleApplication && newGeneration && newGenerationLifetime ?
                "PHASE28_STALE_IDENTITY_REJECTED=1" :
                "PHASE28_STALE_IDENTITY_REJECTED=0");
            Marker(staleContext ? "PHASE28_STALE_CONTEXT_REJECTED=1" :
                "PHASE28_STALE_CONTEXT_REJECTED=0");
            Marker(ManagedImageDiagnostics.IsBalanced ?
                "PHASE28_MANAGED_CLEANUP_BALANCED=1" :
                "PHASE28_MANAGED_CLEANUP_BALANCED=0");
            Marker(NativeBootstrapDiagnostics.IsBalanced ?
                "PHASE28_BOOTSTRAP_CLEANUP_BALANCED=1" :
                "PHASE28_BOOTSTRAP_CLEANUP_BALANCED=0");
            Marker(Ring3ProcessTable.LiveCount == 0 &&
                   ThreadPool.LiveUserThreadCount == 0 &&
                   Ring3ProcessDiagnostics.IsBalanced ?
                "PHASE28_PROCESS_CLEANUP_BALANCED=1" :
                "PHASE28_PROCESS_CLEANUP_BALANCED=0");
            bool complete = first && second && third && fourth &&
                typedFailure && exit && failFast && replacement &&
                repeatedIdentitiesDistinct && staleApplication && staleContext &&
                newGeneration && newGenerationLifetime &&
                ManagedImageDiagnostics.IsBalanced &&
                NativeBootstrapDiagnostics.IsBalanced &&
                Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced;
            Marker(complete ? "RING3_PHASE28_COMPLETE=1" :
                "RING3_PHASE28_COMPLETE=0");
            Native.Sti();
        }

        internal static void SchedulePhase29() {
            if (_phase29Scheduled) return;
            _phase29Scheduled = true;
            Marker("PHASE29_SCHEDULED=1");
            new Thread(&RunPhase29, 131072).Start(0);
        }

        private static bool RunOnePhase29Lifetime(int payloadKind,
                                                   out Ring3Process process) {
            process = null;
            if (_owner == null) return false;
            Native.Cli();
            string failure;
            if (!Ring3Process.TryCreateManagedNotificationEntry(
                    _owner.Handle.Value, payloadKind, out process, out failure) ||
                process == null) {
                Native.Sti();
                Marker("PHASE29_PROCESS_CREATE_FAILED=1");
                if (failure != null) Marker("PHASE29_PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }
            Ring3ProcessHandle oldHandle = process.Handle;
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool authorized = process.ManagedImage != null &&
                process.ManagedImage.TryEnterManagedEntry();
            bool started = process.StartManagedBootstrap();
            if (started) Native.Sti();
            int spins = 0;
            while (started && !process.IsTerminal && spins++ < 6000000)
                Native.Hlt();

            bool completed = process.IsTerminal;
            bool dispatched = process.SchedulerDispatches >= 1 &&
                process.SchedulerCr3Valid && process.SchedulerRsp0Valid;
            bool resumed = payloadKind != 4 && process.TimerPreemptions > 0 &&
                process.SchedulerDispatches >= 2 && process.UserRspPreserved;
            bool service = payloadKind == 1 || payloadKind == 4
                ? process.ServiceRequestsSucceeded > 0
                : process.ServiceRequestsSucceeded == 0;
            int expected = payloadKind == 1 ? 29 :
                (payloadKind == 2 ? 31 :
                (payloadKind == 3 ? 32 :
                (payloadKind == 5 ? 35 : -1)));
            bool managedResult = (payloadKind <= 3 || payloadKind == 5) &&
                process.BootstrapResultSucceeded &&
                process.BootstrapReturnCode == expected &&
                process.ExitCode == expected;
            bool terminalResult = payloadKind == 4 ? process.ExitCode == expected :
                managedResult;
            bool state = process.State == Ring3ProcessState.Exiting ||
                process.State == Ring3ProcessState.Exited;
            bool clean = process.Cleanup();
            bool staleHandle = !process.TryResolveHandle(oldHandle);
            Marker(scaffold ? "PHASE29_SCAFFOLD_PASS=1" : "PHASE29_SCAFFOLD_PASS=0");
            Marker(authorized ? "PHASE29_ENTRY_AUTHORIZED=1" : "PHASE29_ENTRY_AUTHORIZED=0");
            Marker(dispatched ? "PHASE29_DISPATCH_PASS=1" : "PHASE29_DISPATCH_PASS=0");
            Marker(resumed ? "PHASE29_RESUME_PASS=1" :
                (payloadKind == 4 ? "PHASE29_RESUME_PASS=NA" : "PHASE29_RESUME_PASS=0"));
            Marker(service ? "PHASE29_SERVICE_PASS=1" : "PHASE29_SERVICE_PASS=0");
            Marker(terminalResult && state ? "PHASE29_MAIN_RESULT_PASS=1" :
                "PHASE29_MAIN_RESULT_PASS=0");
            Marker(clean && staleHandle ? "PHASE29_LIFETIME_CLEAN=1" :
                "PHASE29_LIFETIME_CLEAN=0");
            return started && completed && scaffold && authorized && dispatched &&
                service && terminalResult && state && clean && staleHandle &&
                (payloadKind == 4 || payloadKind > 1 || resumed);
        }

        private static void RunPhase29() {
            Native.Cli();
            Marker("PHASE29_BEGIN=1");
            bool owner = TryCreateOwner();
            ApplicationServiceContext context = null;
            ApplicationServiceResult contextResult;
            if (owner) ApplicationServiceRegistry.TryCreateContext(
                _owner.Handle, out context, out contextResult);

            bool first = owner && RunOnePhase29Lifetime(1, out _);
            bool second = owner && RunOnePhase29Lifetime(1, out _);
            bool third = owner && RunOnePhase29Lifetime(1, out _);
            bool fourth = owner && RunOnePhase29Lifetime(1, out _);
            bool titleFailure = owner && RunOnePhase29Lifetime(2, out _);
            bool bodyFailure = owner && RunOnePhase29Lifetime(3, out _);
            bool invalidType = owner && RunOnePhase29Lifetime(5, out _);
            bool failFast = owner && RunOnePhase29Lifetime(4, out _);

            ulong staleOwnerHandle = owner ? _owner.Handle.Value : 0UL;
            CleanupOwner();
            ApplicationInstance ignored;
            ApplicationInstance staleContextOwner;
            ApplicationServiceResult staleResult;
            bool staleApplication = staleOwnerHandle != 0 &&
                !ApplicationInstanceRegistry.TryGet(
                    ApplicationInstanceHandle.FromValue(staleOwnerHandle),
                    out ignored);
            bool staleContext = context != null &&
                !ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.Notifications,
                    out staleContextOwner, out staleResult) &&
                staleContextOwner == null;

            bool newOwner = TryCreateOwner();
            bool replacement = newOwner && RunOnePhase29Lifetime(1, out _);
            CleanupOwner();

            Marker(first && second && third && fourth ?
                "PHASE29_REPEATED_LIFETIMES=4" :
                "PHASE29_REPEATED_LIFETIMES=0");
            Marker(titleFailure ? "PHASE29_TITLE_BOUND_FAILURE=1" :
                "PHASE29_TITLE_BOUND_FAILURE=0");
            Marker(bodyFailure ? "PHASE29_BODY_BOUND_FAILURE=1" :
                "PHASE29_BODY_BOUND_FAILURE=0");
            Marker(invalidType ? "PHASE29_INVALID_TYPE_REJECTED=1" :
                "PHASE29_INVALID_TYPE_REJECTED=0");
            Marker(failFast ? "PHASE29_FAILFAST_PASS=1" :
                "PHASE29_FAILFAST_PASS=0");
            Marker(replacement ? "PHASE29_REPLACEMENT_PASS=1" :
                "PHASE29_REPLACEMENT_PASS=0");
            Marker(staleApplication ? "PHASE29_STALE_APPLICATION_REJECTED=1" :
                "PHASE29_STALE_APPLICATION_REJECTED=0");
            Marker(staleContext ? "PHASE29_STALE_SERVICE_CONTEXT_REJECTED=1" :
                "PHASE29_STALE_SERVICE_CONTEXT_REJECTED=0");
            Marker(ManagedImageDiagnostics.IsBalanced ?
                "PHASE29_MANAGED_CLEANUP_BALANCED=1" :
                "PHASE29_MANAGED_CLEANUP_BALANCED=0");
            Marker(NativeBootstrapDiagnostics.IsBalanced ?
                "PHASE29_BOOTSTRAP_CLEANUP_BALANCED=1" :
                "PHASE29_BOOTSTRAP_CLEANUP_BALANCED=0");
            Marker(Ring3ProcessTable.LiveCount == 0 &&
                   ThreadPool.LiveUserThreadCount == 0 &&
                   Ring3ProcessDiagnostics.IsBalanced ?
                "PHASE29_PROCESS_CLEANUP_BALANCED=1" :
                "PHASE29_PROCESS_CLEANUP_BALANCED=0");
            bool complete = first && second && third && fourth &&
                titleFailure && bodyFailure && invalidType && failFast && replacement &&
                staleApplication && staleContext &&
                ManagedImageDiagnostics.IsBalanced &&
                NativeBootstrapDiagnostics.IsBalanced &&
                Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced;
            Marker(complete ? "RING3_PHASE29_COMPLETE=1" :
                "RING3_PHASE29_COMPLETE=0");
            Native.Sti();
        }

        internal static void SchedulePhase30() {
            if (_phase30Scheduled) return;
            _phase30Scheduled = true;
            Marker("PHASE30_SCHEDULED=1");
            new Thread(&RunPhase30, 131072).Start(0);
        }

        internal static void SchedulePhase31() {
            if (_phase31Scheduled) return;
            _phase31Scheduled = true;
            Marker("PHASE31_SCHEDULED=1");
            new Thread(&RunPhase31, 131072).Start(0);
        }

        private static bool TryCreatePhase31Owner() {
            if (_owner != null) return false;
            const string id = "selftest.phase31.requester";
            LaunchRequest request = LaunchRequest.ForAppId(id, null, null,
                LaunchActivationIntent.NewInstance);
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!ApplicationInstanceRegistry.TryBeginLaunch(id,
                    ApplicationInstancePolicy.MultiInstance, request,
                    out instance, out reused, out failure) ||
                instance == null || reused ||
                !ApplicationInstanceRegistry.TryCompleteLaunch(instance,
                    false, out failure)) {
                Marker("PHASE31_REQUESTER_INSTANCE_CREATED=0");
                return false;
            }
            _owner = instance;
            _ownerCreated = true;
            Marker("PHASE31_REQUESTER_INSTANCE_CREATED=1");
            Marker("PHASE31_REQUESTER_INSTANCE_RUNNING=1");
            return true;
        }

        private static bool RunOnePhase31Lifetime(int payloadKind,
                int expectedReturn, int expectedServiceRequests,
                out Ring3ProcessHandle oldHandle, out bool resumed,
                out bool mainResult) {
            oldHandle = default(Ring3ProcessHandle);
            resumed = false;
            mainResult = false;
            if (_owner == null) return false;

            Native.Cli();
            string failure;
            Ring3Process process;
            if (!Ring3Process.TryCreateManagedShellEntry(
                    _owner.Handle.Value, payloadKind, out process,
                    out failure) || process == null) {
                Native.Sti();
                Marker("PHASE31_PROCESS_CREATE_FAILED=1");
                if (failure != null)
                    Marker("PHASE31_PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }

            oldHandle = process.Handle;
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool authorized = process.ManagedImage != null &&
                process.ManagedImage.TryEnterManagedEntry();
            bool started = process.StartManagedBootstrap();
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
            bool failFast = payloadKind == 4;
            mainResult = failFast
                ? observedExitCode == -1
                : process.BootstrapResultSucceeded &&
                    process.BootstrapReturnCode == expectedReturn &&
                    observedExitCode == expectedReturn;
            bool state = process.State == Ring3ProcessState.Exiting ||
                process.State == Ring3ProcessState.Exited;
            bool clean = process.Cleanup();
            bool stale = !process.TryResolveHandle(oldHandle);

            Marker(scaffold ? "PHASE31_SCAFFOLD_PASS=1" :
                "PHASE31_SCAFFOLD_PASS=0");
            Marker(authorized ? "PHASE31_ENTRY_AUTHORIZED=1" :
                "PHASE31_ENTRY_AUTHORIZED=0");
            Marker(dispatched ? "PHASE31_DISPATCH_PASS=1" :
                "PHASE31_DISPATCH_PASS=0");
            Marker(resumed ? "PHASE31_RESUME_PASS=1" :
                "PHASE31_RESUME_PASS=0");
            NumberMarker("PHASE31_SERVICE_REQUESTS=", serviceRequests);
            if (failFast) {
                Marker("PHASE31_FAILFAST_EXIT=" + observedExitCode.ToString());
            } else {
                NumberMarker("PHASE31_MAIN_RETURN=", expectedReturn);
            }
            Marker(serviceRequests == expectedServiceRequests ?
                "PHASE31_SERVICE_COUNT_PASS=1" :
                "PHASE31_SERVICE_COUNT_PASS=0");
            Marker(mainResult && state
                ? (failFast ? "PHASE31_FAILFAST_REQUESTER_DIED=1" :
                    "PHASE31_MAIN_RESULT_PASS=1")
                : (failFast ? "PHASE31_FAILFAST_REQUESTER_DIED=0" :
                    "PHASE31_MAIN_RESULT_PASS=0"));
            Marker(clean && stale ? "PHASE31_LIFETIME_CLEAN=1" :
                "PHASE31_LIFETIME_CLEAN=0");
            return started && completed && scaffold && authorized &&
                dispatched && serviceRequests == expectedServiceRequests &&
                mainResult && state && clean && stale && resumed;
        }

        private static bool CheckPhase31StaleRequester(
                ulong oldOwnerHandle, ApplicationServiceContext oldContext,
                ApplicationServiceAccess oldAccess,
                Ring3ProcessHandle oldProcessHandle) {
            CleanupOwner();
            ApplicationInstance ignored;
            bool staleOwner = oldOwnerHandle != 0 &&
                !ApplicationInstanceRegistry.TryGet(
                    ApplicationInstanceHandle.FromValue(oldOwnerHandle),
                    out ignored);
            ApplicationServiceResult validation;
            bool staleContext = oldContext != null &&
                !ApplicationServiceRegistry.TryValidateContext(oldContext,
                    ApplicationServiceId.Shell, out ignored, out validation);
            ApplicationServiceResult<ApplicationServiceRequestHandle> retry =
                oldContext != null && oldAccess != null && oldAccess.Shell != null
                    ? oldAccess.Shell.Begin(oldContext,
                        ApplicationShellOpenRequest.ForApplicationId(
                            "gxos.builtin.calculator"))
                    : null;
            bool launchRejected = retry != null && !retry.Succeeded &&
                retry.Code == ApplicationServiceResultCode.InvalidContext;
            bool staleProcess = !oldProcessHandle.IsValid ||
                Ring3ProcessTable.Resolve(oldProcessHandle) == null;
            Marker(staleOwner ? "PHASE31_STALE_APP_INSTANCE_REJECTED=1" :
                "PHASE31_STALE_APP_INSTANCE_REJECTED=0");
            Marker(staleProcess ? "PHASE31_STALE_PROCESS_HANDLE_REJECTED=1" :
                "PHASE31_STALE_PROCESS_HANDLE_REJECTED=0");
            Marker(staleContext ? "PHASE31_STALE_SERVICE_CONTEXT_REJECTED=1" :
                "PHASE31_STALE_SERVICE_CONTEXT_REJECTED=0");
            Marker(launchRejected ? "PHASE31_STALE_LAUNCH_REJECTED=1" :
                "PHASE31_STALE_LAUNCH_REJECTED=0");
            return staleOwner && staleProcess && staleContext && launchRejected;
        }

        private static void RunPhase31() {
            Native.Cli();
            Marker("PHASE31_BEGIN=1");
            ApplicationServiceRegistry.Initialize();
            ApplicationDescriptorRegistry.Initialize();
            ApplicationFactoryRegistry.Initialize();
            const string targetId = "gxos.builtin.calculator";
            int initialTargets = ApplicationInstanceRegistry.CountByDescriptor(
                targetId);
            int factoryStart = ApplicationFactoryRegistry.FactoryLaunches;
            int fallbackStart =
                ApplicationFactoryRegistry.CompatibilityFallbackLaunches;
            int legacyStart = AppModelCompatibilityDiagnostics.LegacyBackendCalls;
            ApplicationInstance[] launchedTargets = new ApplicationInstance[6];
            bool all = initialTargets >= 0;
            int successfulReturns = 0;
            int targetIndex = 0;

            for (int i = 0; i < 4; i++) {
                int targetOrdinal = initialTargets + targetIndex;
                bool ownerCreated = TryCreatePhase31Owner();
                ApplicationServiceContext context = null;
                ApplicationServiceAccess access = null;
                ApplicationServiceResult contextResult;
                ulong ownerHandle = ownerCreated ? _owner.Handle.Value : 0UL;
                if (ownerCreated) {
                    ApplicationServiceRegistry.TryCreateContextAndAccess(
                        _owner.Handle, out context, out access,
                        out contextResult);
                }
                Ring3ProcessHandle processHandle = default(Ring3ProcessHandle);
                bool resumed = false;
                bool mainResult = false;
                bool ran = ownerCreated && RunOnePhase31Lifetime(1, 31, 1,
                    out processHandle, out resumed, out mainResult);
                ApplicationInstance target =
                    ApplicationInstanceRegistry.GetByDescriptorAt(targetId,
                        targetOrdinal);
                bool targetCreated = ran && target != null &&
                    target.LifecycleState ==
                        ApplicationInstanceLifecycleState.Activated &&
                    target.OwnedWindowCount > 0;
                bool targetPersists = targetCreated &&
                    ApplicationInstanceRegistry.TryGet(target.Handle,
                        out ApplicationInstance stillActive) &&
                    stillActive == target;
                bool stale = CheckPhase31StaleRequester(ownerHandle, context,
                    access, processHandle);
                bool distinct = targetCreated;
                for (int j = 0; j < targetIndex; j++)
                    distinct = distinct && launchedTargets[j] != target;
                if (targetCreated) launchedTargets[targetIndex++] = target;
                bool pass = ran && resumed && mainResult && targetCreated &&
                    targetPersists && stale && distinct;
                Marker(pass ? "PHASE31_SUCCESS_LIFETIME_PASS=1" :
                    "PHASE31_SUCCESS_LIFETIME_PASS=0");
                if (pass) successfulReturns++;
                all = all && pass;
            }

            bool invalidOwner = TryCreatePhase31Owner();
            int invalidCount = ApplicationInstanceRegistry.CountByDescriptor(
                targetId);
            int invalidFactoryCount = ApplicationFactoryRegistry.FactoryLaunches;
            ApplicationServiceContext invalidContext = null;
            ApplicationServiceAccess invalidAccess = null;
            ApplicationServiceResult invalidContextResult;
            ulong invalidOwnerHandle = invalidOwner ? _owner.Handle.Value : 0UL;
            Ring3ProcessHandle invalidProcess = default(Ring3ProcessHandle);
            bool invalidResumed = false;
            bool invalidMain = false;
            if (invalidOwner)
                ApplicationServiceRegistry.TryCreateContextAndAccess(
                    _owner.Handle, out invalidContext, out invalidAccess,
                    out invalidContextResult);
            bool invalidRan = invalidOwner && RunOnePhase31Lifetime(2, 41, 1,
                out invalidProcess, out invalidResumed, out invalidMain);
            bool invalidTyped = invalidRan && invalidMain &&
                ApplicationInstanceRegistry.CountByDescriptor(targetId) ==
                    invalidCount &&
                ApplicationFactoryRegistry.FactoryLaunches == invalidFactoryCount;
            bool invalidStale = CheckPhase31StaleRequester(invalidOwnerHandle,
                invalidContext, invalidAccess, invalidProcess);
            Marker(invalidTyped ? "PHASE31_INVALID_TARGET_NOT_FOUND=1" :
                "PHASE31_INVALID_TARGET_NOT_FOUND=0");
            all = all && invalidRan && invalidResumed && invalidTyped &&
                invalidStale;

            bool oversizeOwner = TryCreatePhase31Owner();
            int oversizeCount = ApplicationInstanceRegistry.CountByDescriptor(
                targetId);
            int oversizeFactoryCount = ApplicationFactoryRegistry.FactoryLaunches;
            ApplicationServiceContext oversizeContext = null;
            ApplicationServiceAccess oversizeAccess = null;
            ApplicationServiceResult oversizeContextResult;
            ulong oversizeOwnerHandle = oversizeOwner ? _owner.Handle.Value : 0UL;
            Ring3ProcessHandle oversizeProcess = default(Ring3ProcessHandle);
            bool oversizeResumed = false;
            bool oversizeMain = false;
            if (oversizeOwner)
                ApplicationServiceRegistry.TryCreateContextAndAccess(
                    _owner.Handle, out oversizeContext, out oversizeAccess,
                    out oversizeContextResult);
            bool oversizeRan = oversizeOwner && RunOnePhase31Lifetime(3, 42,
                0, out oversizeProcess, out oversizeResumed, out oversizeMain);
            bool oversizeNoBackend = oversizeRan && oversizeMain &&
                ApplicationInstanceRegistry.CountByDescriptor(targetId) ==
                    oversizeCount &&
                ApplicationFactoryRegistry.FactoryLaunches == oversizeFactoryCount;
            bool oversizeStale = CheckPhase31StaleRequester(
                oversizeOwnerHandle, oversizeContext, oversizeAccess,
                oversizeProcess);
            Marker(oversizeNoBackend ? "PHASE31_OVERSIZE_SDK_REJECTED=1" :
                "PHASE31_OVERSIZE_SDK_REJECTED=0");
            all = all && oversizeRan && oversizeResumed &&
                oversizeNoBackend && oversizeStale;

            int failFastOrdinal = initialTargets + targetIndex;
            bool failFastOwner = TryCreatePhase31Owner();
            ApplicationServiceContext failFastContext = null;
            ApplicationServiceAccess failFastAccess = null;
            ApplicationServiceResult failFastContextResult;
            ulong failFastOwnerHandle = failFastOwner ? _owner.Handle.Value : 0UL;
            Ring3ProcessHandle failFastProcess = default(Ring3ProcessHandle);
            bool failFastResumed = false;
            bool failFastMain = false;
            if (failFastOwner)
                ApplicationServiceRegistry.TryCreateContextAndAccess(
                    _owner.Handle, out failFastContext, out failFastAccess,
                    out failFastContextResult);
            bool failFastRan = failFastOwner && RunOnePhase31Lifetime(4, -1, 1,
                out failFastProcess, out failFastResumed, out failFastMain);
            ApplicationInstance failFastTarget =
                ApplicationInstanceRegistry.GetByDescriptorAt(targetId,
                    failFastOrdinal);
            bool failFastLaunchPersisted = failFastRan && failFastMain &&
                failFastTarget != null &&
                failFastTarget.LifecycleState ==
                    ApplicationInstanceLifecycleState.Activated;
            if (failFastLaunchPersisted)
                launchedTargets[targetIndex++] = failFastTarget;
            bool failFastStale = CheckPhase31StaleRequester(
                failFastOwnerHandle, failFastContext, failFastAccess,
                failFastProcess);
            Marker(failFastLaunchPersisted ?
                "PHASE31_FAILFAST_TARGET_PERSISTED=1" :
                "PHASE31_FAILFAST_TARGET_PERSISTED=0");
            all = all && failFastRan && failFastResumed && failFastMain &&
                failFastLaunchPersisted && failFastStale;

            int replacementOrdinal = initialTargets + targetIndex;
            bool replacementOwner = TryCreatePhase31Owner();
            ApplicationServiceContext replacementContext = null;
            ApplicationServiceAccess replacementAccess = null;
            ApplicationServiceResult replacementContextResult;
            ulong replacementOwnerHandle = replacementOwner ?
                _owner.Handle.Value : 0UL;
            Ring3ProcessHandle replacementProcess =
                default(Ring3ProcessHandle);
            bool replacementResumed = false;
            bool replacementMain = false;
            if (replacementOwner)
                ApplicationServiceRegistry.TryCreateContextAndAccess(
                    _owner.Handle, out replacementContext,
                    out replacementAccess, out replacementContextResult);
            bool replacementRan = replacementOwner &&
                RunOnePhase31Lifetime(1, 31, 1,
                    out replacementProcess, out replacementResumed,
                    out replacementMain);
            ApplicationInstance replacementTarget =
                ApplicationInstanceRegistry.GetByDescriptorAt(targetId,
                    replacementOrdinal);
            bool replacementSuccess = replacementRan && replacementMain &&
                replacementTarget != null && replacementResumed &&
                replacementTarget.LifecycleState ==
                    ApplicationInstanceLifecycleState.Activated;
            if (replacementSuccess) {
                launchedTargets[targetIndex++] = replacementTarget;
                successfulReturns++;
            }
            bool replacementStale = CheckPhase31StaleRequester(
                replacementOwnerHandle, replacementContext,
                replacementAccess, replacementProcess);
            Marker(replacementSuccess ? "PHASE31_REPLACEMENT_RETURN_31=1" :
                "PHASE31_REPLACEMENT_RETURN_31=0");
            all = all && replacementSuccess && replacementStale;

            // A fresh Ring 3 process carrying a kernel-stale owner handle must
            // receive InvalidContext and cannot enter the target factory.
            ulong staleOwnerValue = failFastOwnerHandle;
            Ring3Process staleProbe = null;
            string staleFailure;
            Native.Cli();
            bool staleProbeCreated = staleOwnerValue != 0 &&
                Ring3Process.TryCreateManagedShellEntry(staleOwnerValue, 5,
                    out staleProbe, out staleFailure) &&
                staleProbe != null;
            if (staleProbeCreated) Native.Sti();
            bool staleProbePass = false;
            if (staleProbeCreated) {
                Ring3ProcessHandle staleProbeHandle = staleProbe.Handle;
                bool scaffold = staleProbe.ManagedImage != null &&
                    staleProbe.ManagedImage.ValidateRuntimeScaffold();
                bool authorized = staleProbe.ManagedImage != null &&
                    staleProbe.ManagedImage.TryEnterManagedEntry();
                bool started = staleProbe.StartManagedBootstrap();
                if (started) Native.Sti();
                int spins = 0;
                while (started && !staleProbe.IsTerminal && spins++ < 6000000)
                    Native.Hlt();
                staleProbePass = started && staleProbe.IsTerminal &&
                    staleProbe.ServiceRequestsSucceeded == 1 &&
                    staleProbe.BootstrapResultSucceeded &&
                    staleProbe.BootstrapReturnCode == 32 &&
                    staleProbe.ExitCode == 32 && scaffold && authorized &&
                    staleProbe.Cleanup() &&
                    Ring3ProcessTable.Resolve(staleProbeHandle) == null;
            }
            Marker(staleProbePass ? "PHASE31_STALE_OWNER_ABI_REJECTED=1" :
                "PHASE31_STALE_OWNER_ABI_REJECTED=0");
            all = all && staleProbePass;

            bool cleanup = true;
            for (int i = 0; i < targetIndex; i++) {
                ApplicationInstance target = launchedTargets[i];
                if (target == null || !ApplicationInstanceRegistry.TryTerminate(
                        target, "Phase 31 diagnostic target cleanup")) {
                    cleanup = false;
                }
            }
            WindowManager.CleanupClosedWindows();
            bool balanced = cleanup &&
                ApplicationInstanceRegistry.CountByDescriptor(targetId) ==
                    initialTargets && ApplicationServiceRegistry.ActiveRequestCount == 0 &&
                _owner == null &&
                ApplicationFactoryRegistry.FactoryLaunches == factoryStart + 6 &&
                ApplicationFactoryRegistry.CompatibilityFallbackLaunches ==
                    fallbackStart &&
                AppModelCompatibilityDiagnostics.LegacyBackendCalls == legacyStart;
            Marker("PHASE31_SUCCESSFUL_RETURN_LIFETIMES=" +
                successfulReturns.ToString());
            Marker(targetIndex == 6 ? "PHASE31_TARGET_INSTANCE_COUNT=6" :
                "PHASE31_TARGET_INSTANCE_COUNT=FAIL");
            Marker(cleanup ? "PHASE31_TARGET_CLEANUP=1" :
                "PHASE31_TARGET_CLEANUP=0");
            Marker(balanced ? "PHASE31_APP_MODEL_BALANCED=1" :
                "PHASE31_APP_MODEL_BALANCED=0");
            Marker(Ring3ProcessTable.LiveCount == 0 &&
                   ThreadPool.LiveUserThreadCount == 0 &&
                   Ring3ProcessDiagnostics.IsBalanced ?
                "PHASE31_PROCESS_CLEANUP_BALANCED=1" :
                "PHASE31_PROCESS_CLEANUP_BALANCED=0");
            bool complete = all && successfulReturns >= 5 && targetIndex == 6 &&
                balanced && Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced &&
                ManagedImageDiagnostics.IsBalanced &&
                NativeBootstrapDiagnostics.IsBalanced;
            Marker(complete ? "RING3_PHASE31_COMPLETE=1" :
                "RING3_PHASE31_COMPLETE=0");
            Native.Sti();
        }

        internal static void SchedulePhase32() {
            if (_phase32Scheduled) return;
            _phase32Scheduled = true;
            Marker("PHASE32_SCHEDULED=1");
            new Thread(&RunPhase32, 131072).Start(0);
        }

        private static bool TryCreatePhase32Owner() {
            if (_owner != null) return false;
            const string id = "selftest.phase32.requester";
            LaunchRequest request = LaunchRequest.ForAppId(id, null, null,
                LaunchActivationIntent.NewInstance);
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!ApplicationInstanceRegistry.TryBeginLaunch(id,
                    ApplicationInstancePolicy.MultiInstance, request,
                    out instance, out reused, out failure) ||
                instance == null || reused ||
                !ApplicationInstanceRegistry.TryCompleteLaunch(instance,
                    false, out failure)) {
                Marker("PHASE32_REQUESTER_INSTANCE_CREATED=0");
                return false;
            }
            _owner = instance;
            _ownerCreated = true;
            Marker("PHASE32_REQUESTER_INSTANCE_CREATED=1");
            return true;
        }

        private static bool CheckPhase32KernelTargetLength(
                Ring3Process process, uint targetLength) {
            if (process == null) return false;
            Ring3ShellLaunchRequest request =
                default(Ring3ShellLaunchRequest);
            request.StructureVersion = (uint)Ring3Abi.AbiVersion;
            request.ServiceId = Ring3Abi.ShellService;
            request.OperationId = Ring3Abi.ShellOpenDocumentOperation;
            request.RequestLength = (uint)sizeof(Ring3ShellLaunchRequest);
            request.TargetLength = targetLength;
            request.ResponseCapacity =
                (uint)sizeof(Ring3ShellLaunchResponse);
            request.ResponseBuffer = Ring3Process.UserStackStart + 0x1000UL;
            Ring3ShellLaunchRequest* requestPointer = &request;
            return Ring3Abi.ValidateShellLaunchRequestForPhase32Proof(
                requestPointer) ==
                Ring3Abi.InvalidRequest;
        }

        private static bool CheckPhase32KernelBounds(Ring3Process process) {
            int factories = ApplicationFactoryRegistry.FactoryLaunches;
            int fallbacks =
                ApplicationFactoryRegistry.CompatibilityFallbackLaunches;
            int legacy = AppModelCompatibilityDiagnostics.LegacyBackendCalls;
            int notepads = ApplicationInstanceRegistry.CountByDescriptor(
                "gxos.builtin.notepad");
            int requests = process == null ? -1 :
                process.ServiceRequestsSucceeded;
            bool emptyRejected = CheckPhase32KernelTargetLength(process, 0);
            bool oversizeRejected = CheckPhase32KernelTargetLength(process,
                (uint)ApplicationShellOpenRequest.MaxTargetLength + 1U);
            bool noBackend = factories == ApplicationFactoryRegistry.FactoryLaunches &&
                fallbacks == ApplicationFactoryRegistry.CompatibilityFallbackLaunches &&
                legacy == AppModelCompatibilityDiagnostics.LegacyBackendCalls &&
                notepads == ApplicationInstanceRegistry.CountByDescriptor(
                    "gxos.builtin.notepad") &&
                requests == (process == null ? -1 :
                    process.ServiceRequestsSucceeded);
            Marker(emptyRejected ? "PHASE32_KERNEL_EMPTY_LENGTH_REJECTED=1" :
                "PHASE32_KERNEL_EMPTY_LENGTH_REJECTED=0");
            Marker(oversizeRejected ?
                "PHASE32_KERNEL_OVERSIZE_LENGTH_REJECTED=1" :
                "PHASE32_KERNEL_OVERSIZE_LENGTH_REJECTED=0");
            Marker(noBackend ? "PHASE32_KERNEL_INVALID_LENGTH_NO_BACKEND=1" :
                "PHASE32_KERNEL_INVALID_LENGTH_NO_BACKEND=0");
            return emptyRejected && oversizeRejected && noBackend;
        }

        private static bool RunOnePhase32Lifetime(int payloadKind,
                int expectedReturn, int expectedServiceRequests,
                bool kernelBounds, out Ring3ProcessHandle oldHandle,
                out bool resumed, out bool mainResult) {
            oldHandle = default(Ring3ProcessHandle);
            resumed = false;
            mainResult = false;
            if (_owner == null) return false;

            Native.Cli();
            string failure;
            Ring3Process process;
            if (!Ring3Process.TryCreateManagedOpenDocumentEntry(
                    _owner.Handle.Value, payloadKind, out process,
                    out failure) || process == null) {
                Native.Sti();
                Marker("PHASE32_PROCESS_CREATE_FAILED=1");
                if (failure != null)
                    Marker("PHASE32_PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }

            oldHandle = process.Handle;
            bool bounds = !kernelBounds || CheckPhase32KernelBounds(process);
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool authorized = process.ManagedImage != null &&
                process.ManagedImage.TryEnterManagedEntry();
            bool started = bounds && scaffold && authorized &&
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
                ? observedExitCode == -1 && serviceRequests == 1
                : process.BootstrapResultSucceeded &&
                    process.BootstrapReturnCode == expectedReturn &&
                    observedExitCode == expectedReturn;
            bool state = process.State == Ring3ProcessState.Exiting ||
                process.State == Ring3ProcessState.Exited;
            bool clean = process.Cleanup();
            bool stale = !process.TryResolveHandle(oldHandle);

            Marker(scaffold ? "PHASE32_SCAFFOLD_PASS=1" :
                "PHASE32_SCAFFOLD_PASS=0");
            Marker(authorized ? "PHASE32_ENTRY_AUTHORIZED=1" :
                "PHASE32_ENTRY_AUTHORIZED=0");
            Marker(dispatched ? "PHASE32_DISPATCH_PASS=1" :
                "PHASE32_DISPATCH_PASS=0");
            Marker(resumed ? "PHASE32_RESUME_PASS=1" :
                "PHASE32_RESUME_PASS=0");
            NumberMarker("PHASE32_SERVICE_REQUESTS=", serviceRequests);
            Marker(failFast ? "PHASE32_FAILFAST_EXIT=" +
                    observedExitCode.ToString() :
                "PHASE32_MAIN_RETURN=" + observedExitCode.ToString());
            Marker(serviceRequests == expectedServiceRequests ?
                "PHASE32_SERVICE_COUNT_PASS=1" :
                "PHASE32_SERVICE_COUNT_PASS=0");
            Marker(mainResult && state
                ? (failFast ? "PHASE32_FAILFAST_REQUESTER_DIED=1" :
                    "PHASE32_MAIN_RESULT_PASS=1")
                : (failFast ? "PHASE32_FAILFAST_REQUESTER_DIED=0" :
                    "PHASE32_MAIN_RESULT_PASS=0"));
            Marker(clean && stale ? "PHASE32_LIFETIME_CLEAN=1" :
                "PHASE32_LIFETIME_CLEAN=0");
            return started && completed && bounds && scaffold && authorized &&
                dispatched && serviceRequests == expectedServiceRequests &&
                mainResult && state && clean && stale && resumed;
        }

        private static bool CheckPhase32StaleRequester(
                ulong oldOwnerHandle, ApplicationServiceContext oldContext,
                ApplicationServiceAccess oldAccess,
                Ring3ProcessHandle oldProcessHandle) {
            CleanupOwner();
            ApplicationInstance ignored;
            bool staleOwner = oldOwnerHandle != 0 &&
                !ApplicationInstanceRegistry.TryGet(
                    ApplicationInstanceHandle.FromValue(oldOwnerHandle),
                    out ignored);
            ApplicationServiceResult validation;
            bool staleContext = oldContext != null &&
                !ApplicationServiceRegistry.TryValidateContext(oldContext,
                    ApplicationServiceId.Shell, out ignored, out validation);
            ApplicationServiceResult<ApplicationServiceRequestHandle> retry =
                oldContext != null && oldAccess != null &&
                    oldAccess.Shell != null
                    ? oldAccess.Shell.Begin(oldContext,
                        ApplicationShellOpenRequest.ForDocument(
                            "Scripts/notepad.gxm.txt"))
                    : null;
            bool documentRejected = retry != null && !retry.Succeeded &&
                retry.Code == ApplicationServiceResultCode.InvalidContext;
            bool staleProcess = !oldProcessHandle.IsValid ||
                Ring3ProcessTable.Resolve(oldProcessHandle) == null;
            Marker(staleOwner ? "PHASE32_STALE_APP_INSTANCE_REJECTED=1" :
                "PHASE32_STALE_APP_INSTANCE_REJECTED=0");
            Marker(staleProcess ? "PHASE32_STALE_PROCESS_HANDLE_REJECTED=1" :
                "PHASE32_STALE_PROCESS_HANDLE_REJECTED=0");
            Marker(staleContext ? "PHASE32_STALE_SERVICE_CONTEXT_REJECTED=1" :
                "PHASE32_STALE_SERVICE_CONTEXT_REJECTED=0");
            Marker(documentRejected ?
                "PHASE32_STALE_DOCUMENT_REQUEST_REJECTED=1" :
                "PHASE32_STALE_DOCUMENT_REQUEST_REJECTED=0");
            return staleOwner && staleProcess && staleContext &&
                documentRejected;
        }

        private static ApplicationInstance GetPhase32Target(
                int initialTargets, int index) {
            return ApplicationInstanceRegistry.GetByDescriptorAt(
                "gxos.builtin.notepad", initialTargets + index);
        }

        private static bool IsPhase32DocumentTarget(
                ApplicationInstance target) {
            if (target == null || target.DescriptorId !=
                    "gxos.builtin.notepad" || target.Document !=
                    "Scripts/notepad.gxm.txt" || target.OwnedWindowCount == 0 ||
                    target.LifecycleState !=
                    ApplicationInstanceLifecycleState.Activated) return false;
            LaunchRequest request = target.LaunchRequestContext;
            ApplicationFactory factory;
            return request != null && request.TargetKind ==
                    LaunchRequestTargetKind.FileOpen &&
                request.Document == "Scripts/notepad.gxm.txt" &&
                ApplicationFactoryRegistry.TryGet(target.DescriptorId,
                    out factory) && factory is NotepadApplicationFactory;
        }

        private static void MarkPhase32TargetTermination(string stage,
                int targetIndex, ApplicationInstance target,
                bool terminated) {
            if (target == null) {
                Program.MarkUefiRing3Phase32(
                    "TARGET_TERMINATE;stage=" + stage +
                    ";targetIndex=" + targetIndex.ToString() +
                    ";instance=none;terminated=" + (terminated ? "1" : "0"));
                return;
            }

            int ownedCount = target.OwnedWindowCount;
            if (ownedCount == 0) {
                Program.MarkUefiRing3Phase32(
                    "TARGET_TERMINATE;stage=" + stage +
                    ";targetIndex=" + targetIndex.ToString() +
                    ";instance=" + target.Handle.Value.ToString() +
                    ";generation=" + target.Handle.Generation.ToString() +
                    ";appId=" + target.DescriptorId +
                    ";lifecycle=" + ApplicationInstanceLifecycle.Name(
                        target.LifecycleState) +
                    ";owned=0;terminated=" + (terminated ? "1" : "0") +
                    ";memory=" + Allocator.MemoryInUse.ToString() +
                    ";freeInvalid=" + Allocator.FreeFailInvalidPtr.ToString() +
                    ";freeNoPages=" + Allocator.FreeFailNoPages.ToString() +
                    ";freeCorrupt=" + Allocator.FreeFailCorruptRun.ToString());
                return;
            }

            for (int ownedIndex = 0; ownedIndex < ownedCount; ownedIndex++) {
                Window window = target.GetOwnedWindowAt(ownedIndex);
                TaskbarApplicationEntry entry = null;
                bool taskbarEntry = window != null &&
                    TaskbarApplicationEntryRegistry.TryGet(target.Handle,
                        out entry) && entry != null;
                string title = window == null ? "" : window.Title ?? "";
                if (title.Length > 64) title = title.Substring(0, 64);
                int windowIndex = window == null || WindowManager.Windows == null
                    ? -1 : WindowManager.Windows.IndexOf(window);
                ulong windowObject = window == null ? 0UL :
                    Unsafe.As<Window, ulong>(ref window);
                int referenceWindowIndex = -1;
                if (windowObject != 0UL && WindowManager.Windows != null) {
                    for (int listIndex = 0;
                            listIndex < WindowManager.Windows.Count;
                            listIndex++) {
                        Window candidate = WindowManager.Windows[listIndex];
                        ulong candidateObject = candidate == null ? 0UL :
                            Unsafe.As<Window, ulong>(ref candidate);
                        if (candidateObject == windowObject) {
                            referenceWindowIndex = listIndex;
                            break;
                        }
                    }
                }
                bool topmost = window != null && WindowManager.Windows != null &&
                    WindowManager.Windows.Count > 0 &&
                    WindowManager.Windows[WindowManager.Windows.Count - 1] ==
                        window;
                Program.MarkUefiRing3Phase32(
                    "TARGET_TERMINATE;stage=" + stage +
                    ";targetIndex=" + targetIndex.ToString() +
                    ";instance=" + target.Handle.Value.ToString() +
                    ";generation=" + target.Handle.Generation.ToString() +
                    ";appId=" + target.DescriptorId +
                    ";lifecycle=" + ApplicationInstanceLifecycle.Name(
                        target.LifecycleState) +
                    ";ownedIndex=" + ownedIndex.ToString() +
                    ";windowIndex=" + windowIndex.ToString() +
                    ";windowReferenceIndex=" + referenceWindowIndex.ToString() +
                    ";windowObject=" + windowObject.ToString() +
                    ";windowType=Notepad;title=" + title +
                    ";ownerId=" + (window == null ? 0 :
                        window.OwnerId).ToString() +
                    ";windowOwner=" + (window == null ? 0UL :
                        window.ApplicationInstanceHandle.Value).ToString() +
                    ";visible=" + (window != null && window.Visible ? "1" : "0") +
                    ";minimized=" + (window != null && window.IsMinimized ?
                        "1" : "0") +
                    ";tombstoned=" + (window != null && window.IsTombstoned ?
                        "1" : "0") +
                    ";disposed=" + (window != null && window.IsDisposed ?
                        "1" : "0") +
                    ";topmost=" + (topmost ? "1" : "0") +
                    ";taskbarEntry=" + (taskbarEntry ? "1" : "0") +
                    ";taskbarActive=" + (taskbarEntry && entry.IsActive ?
                        "1" : "0") +
                    ";taskbarActiveWindow=" + (taskbarEntry &&
                        entry.ActiveWindow == window ? "1" : "0") +
                    ";taskbarRecentWindow=" + (taskbarEntry &&
                        entry.MostRecentWindow == window ? "1" : "0") +
                    ";activeApp=" +
                        ApplicationInstanceRegistry.ActiveApplicationHandle.Value.ToString() +
                    ";ownedCount=" + target.OwnedWindowCount.ToString() +
                    ";terminated=" + (terminated ? "1" : "0") +
                    ";memory=" + Allocator.MemoryInUse.ToString() +
                    ";freeInvalid=" + Allocator.FreeFailInvalidPtr.ToString() +
                    ";freeNoPages=" + Allocator.FreeFailNoPages.ToString() +
                    ";freeCorrupt=" + Allocator.FreeFailCorruptRun.ToString());
            }
        }

        private static void RunPhase32() {
            Native.Cli();
            Marker("PHASE32_BEGIN=1");
            ApplicationServiceRegistry.Initialize();
            ApplicationDescriptorRegistry.Initialize();
            ApplicationFactoryRegistry.Initialize();

            const string targetId = "gxos.builtin.notepad";
            int initialTargets = ApplicationInstanceRegistry.CountByDescriptor(
                targetId);
            int initialWindows = WindowManager.GetWindowCountSnapshot();
            int factoryStart = ApplicationFactoryRegistry.FactoryLaunches;
            int fallbackStart =
                ApplicationFactoryRegistry.CompatibilityFallbackLaunches;
            int legacyStart = AppModelCompatibilityDiagnostics.LegacyBackendCalls;
            ApplicationInstance[] launchedTargets =
                new ApplicationInstance[6];
            bool all = initialTargets >= 0;
            int successfulReturns = 0;
            int targetIndex = 0;

            for (int i = 0; i < 4; i++) {
                bool ownerCreated = TryCreatePhase32Owner();
                ApplicationServiceContext context = null;
                ApplicationServiceAccess access = null;
                ApplicationServiceResult contextResult;
                ulong ownerHandle = ownerCreated ? _owner.Handle.Value : 0UL;
                if (ownerCreated)
                    ApplicationServiceRegistry.TryCreateContextAndAccess(
                        _owner.Handle, out context, out access,
                        out contextResult);
                Ring3ProcessHandle processHandle =
                    default(Ring3ProcessHandle);
                bool resumed = false;
                bool mainResult = false;
                bool ran = ownerCreated && RunOnePhase32Lifetime(1, 32, 3,
                    i == 0, out processHandle, out resumed, out mainResult);
                ApplicationInstance target =
                    GetPhase32Target(initialTargets, targetIndex);
                bool targetCreated = ran && IsPhase32DocumentTarget(target);
                bool persists = targetCreated &&
                    ApplicationInstanceRegistry.TryGet(target.Handle,
                        out ApplicationInstance stillActive) &&
                    stillActive == target;
                bool stale = CheckPhase32StaleRequester(ownerHandle, context,
                    access, processHandle);
                bool distinct = targetCreated;
                for (int j = 0; j < targetIndex; j++)
                    distinct = distinct && launchedTargets[j] != target;
                if (targetCreated) launchedTargets[targetIndex++] = target;
                bool pass = ran && resumed && mainResult && targetCreated &&
                    persists && stale && distinct;
                Marker(pass ? "PHASE32_SUCCESS_LIFETIME_PASS=1" :
                    "PHASE32_SUCCESS_LIFETIME_PASS=0");
                if (pass) successfulReturns++;
                all = all && pass;
            }

            bool failFastOwner = TryCreatePhase32Owner();
            ApplicationServiceContext failFastContext = null;
            ApplicationServiceAccess failFastAccess = null;
            ApplicationServiceResult failFastContextResult;
            ulong failFastOwnerHandle = failFastOwner ?
                _owner.Handle.Value : 0UL;
            if (failFastOwner)
                ApplicationServiceRegistry.TryCreateContextAndAccess(
                    _owner.Handle, out failFastContext, out failFastAccess,
                    out failFastContextResult);
            Ring3ProcessHandle failFastProcess =
                default(Ring3ProcessHandle);
            bool failFastResumed = false;
            bool failFastMain = false;
            bool failFastRan = failFastOwner && RunOnePhase32Lifetime(2,
                -1, 1, false, out failFastProcess, out failFastResumed,
                out failFastMain);
            ApplicationInstance failFastTarget =
                GetPhase32Target(initialTargets, targetIndex);
            bool failFastPersists = failFastRan && failFastMain &&
                IsPhase32DocumentTarget(failFastTarget) &&
                ApplicationInstanceRegistry.TryGet(failFastTarget.Handle,
                    out ApplicationInstance failFastStillActive) &&
                failFastStillActive == failFastTarget;
            if (failFastPersists)
                launchedTargets[targetIndex++] = failFastTarget;
            bool failFastStale = CheckPhase32StaleRequester(
                failFastOwnerHandle, failFastContext, failFastAccess,
                failFastProcess);
            Marker(failFastPersists ?
                "PHASE32_FAILFAST_TARGET_PERSISTED=1" :
                "PHASE32_FAILFAST_TARGET_PERSISTED=0");
            all = all && failFastRan && failFastResumed && failFastMain &&
                failFastPersists && failFastStale;

            bool replacementOwner = TryCreatePhase32Owner();
            ApplicationServiceContext replacementContext = null;
            ApplicationServiceAccess replacementAccess = null;
            ApplicationServiceResult replacementContextResult;
            ulong replacementOwnerHandle = replacementOwner ?
                _owner.Handle.Value : 0UL;
            if (replacementOwner)
                ApplicationServiceRegistry.TryCreateContextAndAccess(
                    _owner.Handle, out replacementContext,
                    out replacementAccess, out replacementContextResult);
            Ring3ProcessHandle replacementProcess =
                default(Ring3ProcessHandle);
            bool replacementResumed = false;
            bool replacementMain = false;
            bool replacementRan = replacementOwner &&
                RunOnePhase32Lifetime(1, 32, 3, false,
                    out replacementProcess, out replacementResumed,
                    out replacementMain);
            ApplicationInstance replacementTarget =
                GetPhase32Target(initialTargets, targetIndex);
            bool replacementSuccess = replacementRan && replacementMain &&
                replacementResumed && IsPhase32DocumentTarget(replacementTarget);
            if (replacementSuccess) {
                launchedTargets[targetIndex++] = replacementTarget;
                successfulReturns++;
            }
            bool replacementStale = CheckPhase32StaleRequester(
                replacementOwnerHandle, replacementContext,
                replacementAccess, replacementProcess);
            Marker(replacementSuccess ? "PHASE32_REPLACEMENT_RETURN_32=1" :
                "PHASE32_REPLACEMENT_RETURN_32=0");
            all = all && replacementSuccess && replacementStale;

            ulong staleOwnerValue = failFastOwnerHandle;
            Ring3Process staleProbe = null;
            string staleFailure;
            Native.Cli();
            bool staleProbeCreated = staleOwnerValue != 0 &&
                Ring3Process.TryCreateManagedOpenDocumentEntry(
                    staleOwnerValue, 3, out staleProbe, out staleFailure) &&
                staleProbe != null;
            if (staleProbeCreated) Native.Sti();
            bool staleProbePass = false;
            if (staleProbeCreated) {
                Ring3ProcessHandle staleProbeHandle = staleProbe.Handle;
                bool scaffold = staleProbe.ManagedImage != null &&
                    staleProbe.ManagedImage.ValidateRuntimeScaffold();
                bool authorized = staleProbe.ManagedImage != null &&
                    staleProbe.ManagedImage.TryEnterManagedEntry();
                int factoriesBefore = ApplicationFactoryRegistry.FactoryLaunches;
                int targetsBefore = ApplicationInstanceRegistry.CountByDescriptor(
                    targetId);
                bool started = scaffold && authorized &&
                    staleProbe.StartManagedBootstrap();
                if (started) Native.Sti();
                int spins = 0;
                while (started && !staleProbe.IsTerminal &&
                        spins++ < 6000000) Native.Hlt();
                staleProbePass = started && staleProbe.IsTerminal &&
                    staleProbe.ServiceRequestsSucceeded == 1 &&
                    staleProbe.BootstrapResultSucceeded &&
                    staleProbe.BootstrapReturnCode == 32 &&
                    staleProbe.ExitCode == 32 && scaffold && authorized &&
                    ApplicationFactoryRegistry.FactoryLaunches ==
                        factoriesBefore &&
                    ApplicationInstanceRegistry.CountByDescriptor(targetId) ==
                        targetsBefore && staleProbe.Cleanup() &&
                    Ring3ProcessTable.Resolve(staleProbeHandle) == null;
            }
            Marker(staleProbePass ?
                "PHASE32_STALE_OWNER_OPEN_DOCUMENT_REJECTED=1" :
                "PHASE32_STALE_OWNER_OPEN_DOCUMENT_REJECTED=0");
            all = all && staleProbePass;

            Marker("PHASE32_TARGET_CLEANUP_BEGIN=1");
            bool cleanup = true;
            for (int i = 0; i < targetIndex; i++) {
                ApplicationInstance target = launchedTargets[i];
                Marker("PHASE32_TARGET_CLEANUP_INDEX=" + i.ToString());
                MarkPhase32TargetTermination("BEGIN", i, target, false);
                bool terminated = target != null &&
                    ApplicationInstanceRegistry.TryTerminate(target,
                        "Phase 32 diagnostic target cleanup");
                MarkPhase32TargetTermination("END", i, target, terminated);
                if (!terminated) {
                    cleanup = false;
                    Marker("PHASE32_TARGET_CLEANUP_ITEM=FAIL");
                } else {
                    Marker("PHASE32_TARGET_CLEANUP_ITEM=PASS");
                }
            }
            Marker("PHASE32_WINDOW_CLEANUP_BEGIN=1");
            int cleanupPasses = 0;
            int finalWindows;
            do {
                WindowManager.CleanupClosedWindows();
                cleanupPasses++;
                finalWindows = WindowManager.GetWindowCountSnapshot();
            } while (finalWindows != initialWindows && cleanupPasses < 8);
            Marker("PHASE32_WINDOW_CLEANUP_PASSES=" + cleanupPasses.ToString());
            Marker("PHASE32_WINDOW_CLEANUP_END=1");
            int finalTargets = ApplicationInstanceRegistry.CountByDescriptor(
                targetId);
            int finalFactories = ApplicationFactoryRegistry.FactoryLaunches;
            int finalFallbacks =
                ApplicationFactoryRegistry.CompatibilityFallbackLaunches;
            int finalLegacyCalls =
                AppModelCompatibilityDiagnostics.LegacyBackendCalls;
            int finalActiveRequests =
                ApplicationServiceRegistry.ActiveRequestCount;
            bool ownerReleased = _owner == null;
            bool targetsBalanced = finalTargets == initialTargets;
            bool windowsBalanced = finalWindows == initialWindows;
            bool factoriesBalanced = finalFactories == factoryStart + 11;
            bool fallbacksBalanced = finalFallbacks == fallbackStart;
            bool legacyBalanced = finalLegacyCalls == legacyStart;
            bool requestsBalanced = finalActiveRequests == 0;
            Marker("PHASE32_BALANCE_DETAIL=targets:" + initialTargets.ToString() +
                ":" + finalTargets.ToString() +
                ";windows:" + initialWindows.ToString() +
                ":" + finalWindows.ToString() +
                ";factories:" + factoryStart.ToString() +
                ":" + finalFactories.ToString() +
                ";fallbacks:" + fallbackStart.ToString() +
                ":" + finalFallbacks.ToString() +
                ";legacy:" + legacyStart.ToString() +
                ":" + finalLegacyCalls.ToString() +
                ";requests:" + finalActiveRequests.ToString() +
                ";ownerReleased:" + (ownerReleased ? "1" : "0") +
                ";targetsBalanced:" + (targetsBalanced ? "1" : "0") +
                ";windowsBalanced:" + (windowsBalanced ? "1" : "0") +
                ";factoriesBalanced:" + (factoriesBalanced ? "1" : "0") +
                ";fallbacksBalanced:" + (fallbacksBalanced ? "1" : "0") +
                ";legacyBalanced:" + (legacyBalanced ? "1" : "0") +
                ";requestsBalanced:" + (requestsBalanced ? "1" : "0"));
            bool balanced = cleanup && targetsBalanced && requestsBalanced &&
                ownerReleased && windowsBalanced && factoriesBalanced &&
                fallbacksBalanced && legacyBalanced;
            Marker("PHASE32_SUCCESSFUL_RETURN_LIFETIMES=" +
                successfulReturns.ToString());
            Marker(targetIndex == 6 ? "PHASE32_TARGET_INSTANCE_COUNT=6" :
                "PHASE32_TARGET_INSTANCE_COUNT=FAIL");
            Marker(cleanup ? "PHASE32_TARGET_CLEANUP=1" :
                "PHASE32_TARGET_CLEANUP=0");
            Marker(balanced ? "PHASE32_APP_MODEL_BALANCED=1" :
                "PHASE32_APP_MODEL_BALANCED=0");
            Marker(Ring3ProcessTable.LiveCount == 0 &&
                   ThreadPool.LiveUserThreadCount == 0 &&
                   Ring3ProcessDiagnostics.IsBalanced ?
                "PHASE32_PROCESS_CLEANUP_BALANCED=1" :
                "PHASE32_PROCESS_CLEANUP_BALANCED=0");
            bool complete = all && successfulReturns == 5 && targetIndex == 6 &&
                balanced && Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced &&
                ManagedImageDiagnostics.IsBalanced &&
                NativeBootstrapDiagnostics.IsBalanced;
            Marker(complete ? "RING3_PHASE32_COMPLETE=1" :
                "RING3_PHASE32_COMPLETE=0");
            Native.Sti();
        }

        private static bool RunOnePhase30Lifetime(int payloadKind,
                                                   out Ring3Process process) {
            process = null;
            if (_owner == null) return false;
            Native.Cli();
            string failure;
            if (!Ring3Process.TryCreateManagedClipboardEntry(
                    _owner.Handle.Value, payloadKind, out process, out failure) ||
                process == null) {
                Native.Sti();
                Marker("PHASE30_PROCESS_CREATE_FAILED=1");
                if (failure != null)
                    Marker("PHASE30_PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }
            Ring3ProcessHandle oldHandle = process.Handle;
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool authorized = process.ManagedImage != null &&
                process.ManagedImage.TryEnterManagedEntry();
            bool started = process.StartManagedBootstrap();
            if (started) Native.Sti();
            int spins = 0;
            while (started && !process.IsTerminal && spins++ < 6000000)
                Native.Hlt();

            bool completed = process.IsTerminal;
            bool dispatched = process.SchedulerDispatches >= 1 &&
                process.SchedulerCr3Valid && process.SchedulerRsp0Valid;
            bool resumed = payloadKind != 9 && process.TimerPreemptions > 0 &&
                process.SchedulerDispatches >= 2 && process.UserRspPreserved;
            bool service = process.ServiceRequestsSucceeded > 0;
            bool managedResult = payloadKind != 9 &&
                process.BootstrapResultSucceeded &&
                process.BootstrapReturnCode == 30 && process.ExitCode == 30;
            bool terminalResult = payloadKind == 9
                ? process.ExitCode == -1 : managedResult;
            bool state = process.State == Ring3ProcessState.Exiting ||
                process.State == Ring3ProcessState.Exited;
            bool clean = process.Cleanup();
            bool staleHandle = !process.TryResolveHandle(oldHandle);
            Marker(scaffold ? "PHASE30_SCAFFOLD_PASS=1" :
                "PHASE30_SCAFFOLD_PASS=0");
            Marker(authorized ? "PHASE30_ENTRY_AUTHORIZED=1" :
                "PHASE30_ENTRY_AUTHORIZED=0");
            Marker(dispatched ? "PHASE30_DISPATCH_PASS=1" :
                "PHASE30_DISPATCH_PASS=0");
            Marker(resumed ? "PHASE30_RESUME_PASS=1" :
                (payloadKind == 9 ? "PHASE30_RESUME_PASS=NA" :
                    "PHASE30_RESUME_PASS=0"));
            Marker(service ? "PHASE30_SERVICE_PASS=1" :
                "PHASE30_SERVICE_PASS=0");
            Marker(terminalResult && state ? "PHASE30_MAIN_RESULT_PASS=1" :
                "PHASE30_MAIN_RESULT_PASS=0");
            Marker(clean && staleHandle ? "PHASE30_LIFETIME_CLEAN=1" :
                "PHASE30_LIFETIME_CLEAN=0");
            return started && completed && scaffold && authorized &&
                dispatched && service && terminalResult && state && clean &&
                staleHandle && (payloadKind == 9 || resumed);
        }

        private static bool ReadPhase30ClipboardState(
                string expectedText, string expectedSource,
                ulong expectedGeneration, bool expectedHasValue) {
            if (_owner == null) return false;
            ApplicationServiceContext context;
            ApplicationServiceAccess access;
            ApplicationServiceResult contextResult;
            if (!ApplicationServiceRegistry.TryCreateContext(_owner.Handle,
                    out context, out contextResult) ||
                !ApplicationServiceRegistry.TryGetAccess(context, out access,
                    out contextResult) || access == null ||
                access.Clipboard == null) return false;
            ApplicationServiceResult<ApplicationClipboardSnapshot> read =
                access.Clipboard.GetText(context);
            bool pass = read.Succeeded && read.Value != null &&
                read.Value.HasValue == expectedHasValue &&
                read.Value.Text == (expectedHasValue ? expectedText : string.Empty) &&
                read.Value.SourceAppId ==
                    (expectedHasValue ? expectedSource : string.Empty) &&
                read.Value.Generation == expectedGeneration;
            Marker(pass ? "PHASE30_BACKEND_STATE_MATCHED=1" :
                "PHASE30_BACKEND_STATE_MATCHED=0");
            if (read.Succeeded && read.Value != null) {
                HexMarker("PHASE30_BACKEND_GENERATION=0x",
                    read.Value.Generation);
                HexMarker("PHASE30_BACKEND_TEXT_LENGTH=0x",
                    (ulong)read.Value.Text.Length);
                HexMarker("PHASE30_BACKEND_SOURCE_LENGTH=0x",
                    (ulong)read.Value.SourceAppId.Length);
            }
            return pass;
        }

        private static void RunPhase30() {
            Native.Cli();
            Marker("PHASE30_BEGIN=1");
            ApplicationServiceRegistry.ResetForAppModel();
            bool ownerA = TryCreateOwner("gxos.builtin.taskmanager");
            ApplicationServiceContext contextA = null;
            ApplicationServiceAccess accessA = null;
            ApplicationServiceResult contextResult;
            if (ownerA) {
                ApplicationServiceRegistry.TryCreateContext(_owner.Handle,
                    out contextA, out contextResult);
                ApplicationServiceRegistry.TryGetAccess(contextA, out accessA,
                    out contextResult);
            }
            string sourceA = contextA == null ? string.Empty :
                contextA.ApplicationId;
            bool first = ownerA && RunOnePhase30Lifetime(1, out _);
            bool second = ownerA && RunOnePhase30Lifetime(1, out _);
            bool third = ownerA && RunOnePhase30Lifetime(1, out _);
            bool fourth = ownerA && RunOnePhase30Lifetime(1, out _);
            bool writer = ownerA && RunOnePhase30Lifetime(3, out _);
            bool writerState = ownerA && ReadPhase30ClipboardState(
                "Phase 30 cross-process clipboard", sourceA, 5UL, true);
            bool failFast = ownerA && RunOnePhase30Lifetime(9, out _);
            bool failFastState = ownerA && ReadPhase30ClipboardState(
                "Phase 30 cross-process clipboard", sourceA, 6UL, true);

            ulong staleOwnerHandle = ownerA ? _owner.Handle.Value : 0UL;
            CleanupOwner();
            ApplicationInstance ignored;
            bool staleApplication = staleOwnerHandle != 0 &&
                !ApplicationInstanceRegistry.TryGet(
                    ApplicationInstanceHandle.FromValue(staleOwnerHandle),
                    out ignored);
            ApplicationServiceResult staleMutationResult = null;
            bool staleMutation = contextA != null && accessA != null &&
                !(accessA.Clipboard.SetText(contextA,
                    ApplicationClipboardWriteRequest.Create(
                        "stale clipboard mutation")).Succeeded);
            if (staleMutation)
                Marker("PHASE30_STALE_AUTHORITY_MUTATION_REJECTED=1");
            else
                Marker("PHASE30_STALE_AUTHORITY_MUTATION_REJECTED=0");
            ApplicationInstance staleContextOwner;
            bool staleContext = contextA != null &&
                !ApplicationServiceRegistry.TryValidateContext(
                    contextA, ApplicationServiceId.Clipboard,
                    out staleContextOwner, out staleMutationResult) &&
                staleContextOwner == null;

            bool ownerB = TryCreateOwner("gxos.builtin.calculator");
            ApplicationServiceContext contextB = null;
            if (ownerB) ApplicationServiceRegistry.TryCreateContext(
                _owner.Handle, out contextB, out contextResult);
            string sourceB = contextB == null ? string.Empty :
                contextB.ApplicationId;
            bool replacement = ownerB && RunOnePhase30Lifetime(2, out _);
            bool crossPersistence = ownerB && replacement &&
                ReadPhase30ClipboardState(
                    "Phase 30 cross-process clipboard", sourceA, 6UL, true);
            bool crossSource = crossPersistence && sourceA.Length > 0 &&
                sourceA != sourceB;
            bool malformed = ownerB && RunOnePhase30Lifetime(8, out _);
            bool malformedState = ownerB && malformed &&
                ReadPhase30ClipboardState(
                    "Phase 30 cross-process clipboard", sourceA, 6UL, true);
            bool overwrite = ownerB && RunOnePhase30Lifetime(4, out _);
            bool overwriteState = ownerB && overwrite &&
                ReadPhase30ClipboardState(
                    "Phase 30 overwrite value 2", sourceB, 7UL, true);
            bool oversize = ownerB && RunOnePhase30Lifetime(7, out _);
            bool oversizeState = ownerB && oversize &&
                ReadPhase30ClipboardState(
                    "Phase 30 oversize baseline", sourceB, 8UL, true);
            bool empty = ownerB && RunOnePhase30Lifetime(5, out _);
            bool emptyState = ownerB && empty &&
                ReadPhase30ClipboardState(string.Empty, sourceB, 9UL, true);
            bool clear = ownerB && RunOnePhase30Lifetime(6, out _);
            bool clearState = ownerB && clear &&
                ReadPhase30ClipboardState(string.Empty, string.Empty,
                    11UL, false);
            CleanupOwner();

            bool resetOwner = TryCreateOwner("gxos.builtin.console");
            ApplicationServiceContext resetContext = null;
            ApplicationServiceAccess resetAccess = null;
            bool reset = false;
            if (resetOwner && ApplicationServiceRegistry.TryCreateContext(
                    _owner.Handle, out resetContext, out contextResult) &&
                ApplicationServiceRegistry.TryGetAccess(resetContext,
                    out resetAccess, out contextResult)) {
                ApplicationServiceRegistry.ResetForAppModel();
                ApplicationServiceResult<ApplicationClipboardSnapshot> resetRead =
                    resetAccess.Clipboard.GetText(resetContext);
                reset = resetRead.Succeeded && resetRead.Value != null &&
                    !resetRead.Value.HasValue && resetRead.Value.Text.Length == 0 &&
                    resetRead.Value.SourceAppId.Length == 0 &&
                    resetRead.Value.Generation == 0UL;
            }
            Marker(reset ? "PHASE30_RESET_PASS=1" :
                "PHASE30_RESET_PASS=0");
            CleanupOwner();

            Marker(first && second && third && fourth ?
                "PHASE30_REPEATED_LIFETIMES=4" :
                "PHASE30_REPEATED_LIFETIMES=0");
            Marker(writerState ? "PHASE30_SET_PROOF=1" :
                "PHASE30_SET_PROOF=0");
            Marker(crossPersistence ? "PHASE30_CROSS_PROCESS_PERSISTENCE=1" :
                "PHASE30_CROSS_PROCESS_PERSISTENCE=0");
            Marker(crossSource ? "PHASE30_CROSS_PROCESS_SOURCE=1" :
                "PHASE30_CROSS_PROCESS_SOURCE=0");
            Marker(malformed && malformedState ?
                "PHASE30_MALFORMED_LENGTH_REJECTED=1" :
                "PHASE30_MALFORMED_LENGTH_REJECTED=0");
            Marker(overwrite && overwriteState ?
                "PHASE30_OVERWRITE_GENERATION=1" :
                "PHASE30_OVERWRITE_GENERATION=0");
            Marker(oversize && oversizeState ?
                "PHASE30_OVERSIZE_REJECTED=1" :
                "PHASE30_OVERSIZE_REJECTED=0");
            Marker(empty && emptyState ? "PHASE30_EMPTY_TEXT=1" :
                "PHASE30_EMPTY_TEXT=0");
            Marker(clear && clearState ? "PHASE30_CLEAR_PASS=1" :
                "PHASE30_CLEAR_PASS=0");
            Marker(failFast && failFastState ? "PHASE30_FAILFAST_PASS=1" :
                "PHASE30_FAILFAST_PASS=0");
            Marker(replacement ? "PHASE30_REPLACEMENT_PASS=1" :
                "PHASE30_REPLACEMENT_PASS=0");
            Marker(staleApplication ?
                "PHASE30_STALE_OWNER_REJECTED=1" :
                "PHASE30_STALE_OWNER_REJECTED=0");
            Marker(staleContext ?
                "PHASE30_STALE_SERVICE_CONTEXT_REJECTED=1" :
                "PHASE30_STALE_SERVICE_CONTEXT_REJECTED=0");
            Marker(ManagedImageDiagnostics.IsBalanced ?
                "PHASE30_MANAGED_CLEANUP_BALANCED=1" :
                "PHASE30_MANAGED_CLEANUP_BALANCED=0");
            Marker(NativeBootstrapDiagnostics.IsBalanced ?
                "PHASE30_BOOTSTRAP_CLEANUP_BALANCED=1" :
                "PHASE30_BOOTSTRAP_CLEANUP_BALANCED=0");
            Marker(Ring3ProcessTable.LiveCount == 0 &&
                   ThreadPool.LiveUserThreadCount == 0 &&
                   Ring3ProcessDiagnostics.IsBalanced && staleMutation ?
                "PHASE30_PROCESS_CLEANUP_BALANCED=1" :
                "PHASE30_PROCESS_CLEANUP_BALANCED=0");
            bool complete = first && second && third && fourth && writer &&
                writerState && failFast && failFastState && replacement &&
                crossPersistence && crossSource && malformed && malformedState &&
                overwrite && overwriteState && oversize && oversizeState &&
                empty && emptyState && clear && clearState && reset &&
                staleApplication && staleContext && staleMutation &&
                ManagedImageDiagnostics.IsBalanced &&
                NativeBootstrapDiagnostics.IsBalanced &&
                Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced;
            Marker(complete ? "RING3_PHASE30_COMPLETE=1" :
                "RING3_PHASE30_COMPLETE=0");
            Native.Sti();
        }

        private static void RunPhase24() {
            Native.Cli();
            Marker("PHASE24_BEGIN=1");
            Marker("PHASE24_MANAGED_ENTRY_READY=0");
            ManagedImageProcess first = null;
            bool all = true;
            ulong firstCr3 = 0;
            ulong firstGs = 0;
            for (uint generation = 1; generation <= 4; generation++) {
                ManagedImageProcess process;
                string failure;
                bool created = ManagedImageProcess.TryCreateFromRamdisk(
                    0, generation, out process, out failure);
                if (!created || process == null) {
                    Marker("PHASE24_MAPPING_RESULT=FAIL");
                    if (failure != null) Marker("PHASE24_MAPPING_REJECTED=" + failure);
                    all = false;
                    continue;
                }
                if (generation == 1) {
                    first = process;
                    firstCr3 = process.Space.RootPhysical;
                    firstGs = process.UserGsBase;
                }
                bool scaffold = process.ValidateRuntimeScaffold();
                int flsSlot;
                ulong flsValue;
                bool fls = scaffold && process.TryFlsAllocate(out flsSlot) &&
                    process.TryFlsSet(flsSlot, 0x50483234464C5355UL) &&
                    process.TryFlsGet(flsSlot, out flsValue) &&
                    flsValue == 0x50483234464C5355UL &&
                    process.TryFlsFree(flsSlot) &&
                    !process.TryFlsGet(flsSlot, out flsValue);
                bool entryRejected = !process.TryEnterManagedEntry();
                all = all && scaffold && fls && entryRejected && process.Cleanup();
                Marker(fls ? "PHASE24_FLS_SENTINEL_PASS=1" :
                             "PHASE24_FLS_SENTINEL_PASS=0");
                Marker(scaffold && fls && entryRejected ?
                    "PHASE24_SCAFFOLD_RESULT=PASS" :
                    "PHASE24_SCAFFOLD_RESULT=FAIL");
            }

            // Exercise the negative readiness boundary on a real mapped state
            // without ever executing or corrupting the image bytes.
            bool invalidGsRejected = false;
            bool invalidTlsRejected = false;
            ManagedImageProcess negative;
            string negativeFailure;
            if (ManagedImageProcess.TryCreateFromRamdisk(0, 5, out negative,
                                                         out negativeFailure)) {
                ulong savedGs = negative.UserGsBase;
                ulong savedTls = negative.TlsVectorPhysical;
                negative.UserGsBase = 0;
                invalidGsRejected = !negative.ValidateRuntimeScaffold();
                negative.UserGsBase = savedGs;
                negative.TlsVectorPhysical = 0;
                invalidTlsRejected = !negative.ValidateRuntimeScaffold();
                negative.TlsVectorPhysical = savedTls;
                all = all && negative.Cleanup();
            } else {
                all = false;
            }
            Marker(invalidGsRejected ? "PHASE24_INVALID_GS_REJECTED=1" :
                                        "PHASE24_INVALID_GS_REJECTED=0");
            Marker(invalidTlsRejected ? "PHASE24_INVALID_TLS_REJECTED=1" :
                                         "PHASE24_INVALID_TLS_REJECTED=0");
            Marker(first != null && firstCr3 != 0 && firstGs != 0 ?
                "PHASE24_PRIVATE_STATE_CREATED=1" : "PHASE24_PRIVATE_STATE_CREATED=0");
            Marker("PHASE24_NATIVE_BOOTSTRAP_EXECUTED=0");
            Marker("PHASE24_CPL3_BOOTSTRAP=0");
            Marker("PHASE24_SYSTEM_INFORMATION=0");
            Marker("PHASE24_BOOTSTRAP_EXIT=0");
            Marker(ManagedImageDiagnostics.ManagedEntryAttemptsRejected > 0 ?
                "PHASE24_MANAGED_ENTRY_REJECTIONS=1" :
                "PHASE24_MANAGED_ENTRY_REJECTIONS=0");
            Marker(ManagedImageDiagnostics.IsBalanced && all ?
                "PHASE24_MAPPING_LIFETIMES_PASS=1" :
                "PHASE24_MAPPING_LIFETIMES_PASS=0");
            Marker(ManagedImageDiagnostics.IsBalanced ?
                "PHASE24_RUNTIME_CLEANUP_BALANCED=1" :
                "PHASE24_RUNTIME_CLEANUP_BALANCED=0");
            Native.Sti();
        }

        private static bool RunOnePhase25Lifetime(bool deliberateFault,
                                                  bool requirePreemption,
                                                  out Ring3Process process) {
            process = null;
            if (_owner == null) return false;
            Native.Cli();
            string failure;
            if (!Ring3Process.TryCreateManagedBootstrap(
                    _owner.Handle.Value, deliberateFault, out process,
                    out failure) || process == null) {
                Marker("PHASE25_PROCESS_CREATE_FAILED=1");
                if (failure != null) Marker("PHASE25_PROCESS_CREATE_REJECTED=" + failure);
                return false;
            }

            bool managedEntryRejected = process.ManagedImage != null &&
                !process.TryAuthorizeUserEntry(
                    process.ManagedImage.ManagedEntryAddress);
            bool scaffold = process.ManagedImage != null &&
                process.ManagedImage.ValidateRuntimeScaffold();
            bool started = process.StartManagedBootstrap();
            if (started) Native.Sti();
            int spins = 0;
            while (started && !process.IsTerminal && spins++ < 4000000) {
                Native.Hlt();
            }

            bool completed = process.IsTerminal;
            bool stateOk = deliberateFault
                ? process.State == Ring3ProcessState.Failed
                : process.State == Ring3ProcessState.Exiting;
            bool dispatchOk = process.SchedulerDispatches >= 1 &&
                process.SchedulerCr3Valid && process.SchedulerRsp0Valid;
            bool resumeOk = !requirePreemption ||
                (process.TimerPreemptions > 0 &&
                 process.SchedulerDispatches >= 2 &&
                 process.UserRspPreserved);
            bool resultOk = deliberateFault
                ? (process.TryReadBootstrapResult() &&
                   (process.BootstrapResultFlags &
                    ManagedBootstrapResultContract.SetupFlags) ==
                        ManagedBootstrapResultContract.SetupFlags)
                : process.BootstrapResultSucceeded;
            HexMarker("PHASE25_BOOTSTRAP_FLAGS=0x",
                (ulong)process.BootstrapResultFlags);
            HexMarker("PHASE25_BOOTSTRAP_RETURN=0x",
                (ulong)(uint)process.BootstrapReturnCode);
            bool ownerOk = process.ManagedImage != null &&
                process.ManagedImage.OwnerApplication ==
                    _owner.Handle.Value;
            bool clean = process.Cleanup();
            bool staleHandle = !process.TryResolveHandle(process.Handle);
            Marker(managedEntryRejected ?
                "PHASE25_MANAGED_ENTRY_REJECTION_PASS=1" :
                "PHASE25_MANAGED_ENTRY_REJECTION_PASS=0");
            Marker(scaffold ? "PHASE25_SCAFFOLD_PASS=1" :
                "PHASE25_SCAFFOLD_PASS=0");
            Marker(dispatchOk ? "PHASE25_DISPATCH_PASS=1" :
                "PHASE25_DISPATCH_PASS=0");
            Marker(resumeOk ? "PHASE25_RESUME_PASS=1" :
                "PHASE25_RESUME_PASS=0");
            Marker(resultOk ? "PHASE25_BOOTSTRAP_RESULT_PASS=1" :
                "PHASE25_BOOTSTRAP_RESULT_PASS=0");
            Marker(clean && staleHandle ? "PHASE25_LIFETIME_CLEAN=1" :
                "PHASE25_LIFETIME_CLEAN=0");
            return started && completed && stateOk && managedEntryRejected &&
                scaffold && dispatchOk && resumeOk && resultOk && ownerOk &&
                clean && staleHandle;
        }

        private static bool RunPhase25NegativeLifetime(bool corruptStartup,
                                                        bool corruptGs) {
            if (_owner == null) return false;
            string failure;
            Ring3Process process;
            if (!Ring3Process.TryCreateManagedBootstrap(
                    _owner.Handle.Value, false, out process, out failure) ||
                process == null) return false;
            bool corrupted = corruptStartup
                ? process.CorruptManagedStartupVersionForTest()
                : process.CorruptManagedGsForTest();
            ulong entry = process.NativeBootstrap == null ? 0UL :
                process.NativeBootstrap.EntryAddress;
            bool rejected = corrupted && !process.TryAuthorizeUserEntry(entry);
            bool notStarted = process.State == Ring3ProcessState.Created;
            bool clean = process.Cleanup();
            Marker(rejected && notStarted ?
                (corruptStartup ? "PHASE25_INVALID_STARTUP_REJECTED=1" :
                                  "PHASE25_INVALID_GS_REJECTED=1") :
                (corruptStartup ? "PHASE25_INVALID_STARTUP_REJECTED=0" :
                                  "PHASE25_INVALID_GS_REJECTED=0"));
            return rejected && notStarted && clean;
        }

        private static void RunPhase25() {
            Native.Cli();
            Marker("PHASE25_BEGIN=1");
            Marker("PHASE25_MANAGED_ENTRY_READY=0");
            bool owner = TryCreateOwner();
            bool negativeStartup = owner && RunPhase25NegativeLifetime(true, false);
            bool negativeGs = owner && RunPhase25NegativeLifetime(false, true);
            bool first = owner && RunOnePhase25Lifetime(false, true, out _);
            bool second = owner && RunOnePhase25Lifetime(false, true, out _);
            bool fault = owner && RunOnePhase25Lifetime(true, false, out _);
            bool replacement = owner && RunOnePhase25Lifetime(false, true, out _);
            CleanupOwner();

            Marker(first && second && fault && replacement ?
                "PHASE25_REPEATED_LIFETIMES=4" :
                "PHASE25_REPEATED_LIFETIMES=0");
            Marker(negativeStartup ? "PHASE25_INVALID_STARTUP_PASS=1" :
                "PHASE25_INVALID_STARTUP_PASS=0");
            Marker(negativeGs ? "PHASE25_INVALID_GS_PASS=1" :
                "PHASE25_INVALID_GS_PASS=0");
            Marker(NativeBootstrapDiagnostics.IsBalanced ?
                "PHASE25_BOOTSTRAP_CLEANUP_BALANCED=1" :
                "PHASE25_BOOTSTRAP_CLEANUP_BALANCED=0");
            Marker(ManagedImageDiagnostics.IsBalanced ?
                "PHASE25_MANAGED_CLEANUP_BALANCED=1" :
                "PHASE25_MANAGED_CLEANUP_BALANCED=0");
            Marker(Ring3ProcessTable.LiveCount == 0 &&
                   ThreadPool.LiveUserThreadCount == 0 &&
                   Ring3ProcessDiagnostics.IsBalanced ?
                "PHASE25_PROCESS_CLEANUP_BALANCED=1" :
                "PHASE25_PROCESS_CLEANUP_BALANCED=0");
            Marker(ManagedImageDiagnostics.ManagedEntryAttemptsRejected > 0 ?
                "PHASE25_MANAGED_ENTRY_REJECTIONS=1" :
                "PHASE25_MANAGED_ENTRY_REJECTIONS=0");
            bool complete = first && second && fault && replacement &&
                negativeStartup && negativeGs &&
                NativeBootstrapDiagnostics.IsBalanced &&
                ManagedImageDiagnostics.IsBalanced &&
                Ring3ProcessTable.LiveCount == 0 &&
                ThreadPool.LiveUserThreadCount == 0 &&
                Ring3ProcessDiagnostics.IsBalanced &&
                ManagedImageDiagnostics.ManagedEntryAttemptsRejected > 0;
            Marker(complete ? "RING3_PHASE25_COMPLETE=1" :
                "RING3_PHASE25_COMPLETE=0");
            Native.Sti();
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
            return TryCreateOwner("gxos.builtin.taskmanager");
        }

        private static bool TryCreateOwner(string appId) {
            if (_owner != null)
                return _owner.DescriptorId == appId;
            AppLaunchResolver.InitializeDefaultDescriptors();
            ApplicationDescriptorRegistry.Initialize();
            ApplicationDescriptor descriptor;
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
