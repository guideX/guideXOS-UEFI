using guideXOS.GUI;
using guideXOS.Kernel.Drivers;

namespace guideXOS.OS {
    /// <summary>
    /// Fixed application-platform service table.  ApplicationInstanceRegistry
    /// remains the only source of truth for instance existence and lifecycle;
    /// this class only validates access and projects typed adapters.
    /// </summary>
    public static class ApplicationServiceRegistry {
        public const int Capacity = 11;
        public const int SelectedServiceCount = 10;

        private sealed class ServiceEntry {
            internal ApplicationServiceId Id;
            internal bool Registered;
        }

        private static ServiceEntry[] _entries;
        private static int _registeredCount;
        private static bool _initialized;
        private static bool _duplicateRegistrationRejected;
        private static int _staleContextRejections;
        private static int _invalidContextRejections;
        private static ApplicationServiceAccess _access;
        private static string _lastSettingsSelfTestFailure;
        private static bool _lastDialogSelfTestPassed;
        private static bool _lastFileDialogSelfTestPassed;
        private static bool _lastShellSelfTestPassed;
        private static bool _lastResourceStorageSelfTestPassed;
        private static bool _lastResourceChunkSelfTestPassed;
        private static bool _lastStoragePathSelfTestPassed;
        private static bool _lastPersistentUnavailableSelfTestPassed;
        private static bool _lastStorageAppScopeSelfTestPassed;
        private static bool _lastStorageResetSelfTestPassed;
        private static bool _lastClipboardSelfTestPassed;
        private static bool _lastClipboardContractSelfTestPassed;
        private static bool _lastClipboardGenerationSelfTestPassed;
        private static bool _lastClipboardLifecycleSelfTestPassed;
        private static bool _lastClipboardResetSelfTestPassed;
        private static string _lastSelfTestFailure;

        public static void Initialize() {
            if (_initialized) return;

            _entries = new ServiceEntry[Capacity];
            _registeredCount = 0;
            _duplicateRegistrationRejected = false;
            _staleContextRejections = 0;
            _invalidContextRejections = 0;
            _lastSettingsSelfTestFailure = "not-run";
            _lastDialogSelfTestPassed = false;
            _lastFileDialogSelfTestPassed = false;
            _lastShellSelfTestPassed = false;
            _lastResourceStorageSelfTestPassed = false;
            _lastResourceChunkSelfTestPassed = false;
            _lastStoragePathSelfTestPassed = false;
            _lastPersistentUnavailableSelfTestPassed = false;
            _lastStorageAppScopeSelfTestPassed = false;
            _lastStorageResetSelfTestPassed = false;
            _lastClipboardSelfTestPassed = false;
            _lastClipboardContractSelfTestPassed = false;
            _lastClipboardGenerationSelfTestPassed = false;
            _lastClipboardLifecycleSelfTestPassed = false;
            _lastClipboardResetSelfTestPassed = false;
            _lastSelfTestFailure = "not-run";
            _access = new ApplicationServiceAccess(
                new CSharpApplicationNotificationService(),
                new CSharpApplicationSettingsService(),
                new CSharpApplicationSystemInformationService(),
                new CSharpApplicationDialogService(),
                new CSharpApplicationOpenFileService(),
                new CSharpApplicationSaveFileService(),
                new CSharpApplicationShellService(),
                new CSharpApplicationResourceService(),
                new CSharpApplicationStorageService(),
                new CSharpApplicationClipboardService());
            _initialized = true;

            RegisterInitial(ApplicationServiceId.Notifications);
            RegisterInitial(ApplicationServiceId.Settings);
            RegisterInitial(ApplicationServiceId.SystemInformation);
            RegisterInitial(ApplicationServiceId.Dialogs);
            RegisterInitial(ApplicationServiceId.OpenFile);
            RegisterInitial(ApplicationServiceId.SaveFile);
            RegisterInitial(ApplicationServiceId.Shell);
            RegisterInitial(ApplicationServiceId.Resources);
            RegisterInitial(ApplicationServiceId.Storage);
            RegisterInitial(ApplicationServiceId.Clipboard);
            ApplicationServiceSessionTable.Reset();
        }

        /// <summary>
        /// Reset only service-table state and bounded service diagnostics.
        /// This never clears or reassigns ApplicationInstanceRegistry slots.
        /// </summary>
        public static void ResetForDiagnostics() {
            ApplicationServiceSessionTable.Reset();
            _entries = null;
            _access = null;
            _registeredCount = 0;
            _initialized = false;
            Initialize();
            ResetForAppModel();
        }

        public static int RegisteredCount {
            get { Initialize(); return _registeredCount; }
        }

        public static bool DuplicateRegistrationRejected {
            get { Initialize(); return _duplicateRegistrationRejected; }
        }

        public static int StaleContextRejections {
            get { Initialize(); return _staleContextRejections; }
        }

        public static int InvalidContextRejections {
            get { Initialize(); return _invalidContextRejections; }
        }

        public static bool DiagnosticsClean {
            get {
                Initialize();
                return _registeredCount == SelectedServiceCount &&
                       _access != null && _access.Notifications != null &&
                       _access.Settings != null &&
                       _access.SystemInformation != null &&
                       _access.Dialogs != null &&
                       _access.OpenFile != null &&
                       _access.SaveFile != null &&
                       _access.Shell != null &&
                       _access.Resources != null &&
                       _access.Storage != null &&
                       _access.Clipboard != null;
            }
        }

        public static int ActiveRequestCount {
            get { Initialize(); return ApplicationServiceSessionTable.ActiveSessionCount; }
        }

        public static int TransientWindowCount {
            get { Initialize(); return ApplicationServiceSessionTable.TransientWindowCount; }
        }

        public static int OrphanTransientWindowCount {
            get { Initialize(); return ApplicationServiceSessionTable.OrphanTransientWindowCount; }
        }

        internal static void ResetTemporaryStorageForAppModel() {
            Initialize();
            if (_access != null && _access.Storage != null) {
                _access.Storage.ResetTemporaryForAppModel();
            }
        }

        internal static void ResetForAppModel() {
            Initialize();
            if (_access != null && _access.Storage != null) {
                _access.Storage.ResetTemporaryForAppModel();
            }
            if (_access != null && _access.Clipboard != null) {
                _access.Clipboard.ResetForAppModel();
            }
        }

        public static string LastSettingsSelfTestFailure {
            get { return _lastSettingsSelfTestFailure; }
        }

        public static bool LastDialogSelfTestPassed {
            get { return _lastDialogSelfTestPassed; }
        }

        public static bool LastFileDialogSelfTestPassed {
            get { return _lastFileDialogSelfTestPassed; }
        }

        public static bool LastShellSelfTestPassed {
            get { return _lastShellSelfTestPassed; }
        }

        public static bool LastResourceStorageSelfTestPassed {
            get { return _lastResourceStorageSelfTestPassed; }
        }

        public static bool LastResourceChunkSelfTestPassed {
            get { return _lastResourceChunkSelfTestPassed; }
        }

        public static bool LastStoragePathSelfTestPassed {
            get { return _lastStoragePathSelfTestPassed; }
        }

        public static bool LastPersistentUnavailableSelfTestPassed {
            get { return _lastPersistentUnavailableSelfTestPassed; }
        }

        public static bool LastStorageAppScopeSelfTestPassed {
            get { return _lastStorageAppScopeSelfTestPassed; }
        }

        public static bool LastStorageResetSelfTestPassed {
            get { return _lastStorageResetSelfTestPassed; }
        }

        public static bool LastClipboardSelfTestPassed {
            get { return _lastClipboardSelfTestPassed; }
        }

        public static bool LastClipboardContractSelfTestPassed {
            get { return _lastClipboardContractSelfTestPassed; }
        }

        public static bool LastClipboardGenerationSelfTestPassed {
            get { return _lastClipboardGenerationSelfTestPassed; }
        }

        public static bool LastClipboardLifecycleSelfTestPassed {
            get { return _lastClipboardLifecycleSelfTestPassed; }
        }

        public static bool LastClipboardResetSelfTestPassed {
            get { return _lastClipboardResetSelfTestPassed; }
        }

        public static string LastSelfTestFailure {
            get { return _lastSelfTestFailure; }
        }

        public static bool TryCreateContext(
                ApplicationInstanceHandle handle,
                out ApplicationServiceContext context,
                out ApplicationServiceResult result) {
            Initialize();
            context = null;
            ApplicationInstance instance;
            if (!TryResolveHandle(handle, out instance, out result)) return false;
            if (!IsBoundedApplicationId(instance.DescriptorId)) {
                _invalidContextRejections++;
                result = ApplicationServiceResult.InvalidContextResult();
                return false;
            }

            string[] capabilities = new string[_registeredCount];
            int capabilityCount = 0;
            for (int i = 0; i < Capacity; i++) {
                if (_entries[i] == null || !_entries[i].Registered) continue;
                if (capabilityCount < capabilities.Length) {
                    capabilities[capabilityCount++] =
                        ApplicationServiceNames.For(_entries[i].Id);
                }
            }
            if (capabilityCount != capabilities.Length) {
                string[] bounded = new string[capabilityCount];
                for (int i = 0; i < capabilityCount; i++) {
                    bounded[i] = capabilities[i];
                }
                capabilities = bounded;
            }

            context = new ApplicationServiceContext(
                instance.Handle, instance.DescriptorId, capabilities);
            result = ApplicationServiceResult.SuccessResult();
            return true;
        }

        public static bool TryGetAccess(
                ApplicationServiceContext context,
                out ApplicationServiceAccess access,
                out ApplicationServiceResult result) {
            Initialize();
            access = null;
            ApplicationInstance instance;
            if (!TryValidateCommonContext(context, out instance, out result)) {
                return false;
            }
            access = _access;
            result = ApplicationServiceResult.SuccessResult();
            return true;
        }

        public static bool TryCreateContextAndAccess(
                ApplicationInstanceHandle handle,
                out ApplicationServiceContext context,
                out ApplicationServiceAccess access,
                out ApplicationServiceResult result) {
            if (!TryCreateContext(handle, out context, out result)) {
                access = null;
                return false;
            }
            return TryGetAccess(context, out access, out result);
        }

        internal static ApplicationServiceResult BeginInteractiveRequest(
                ApplicationServiceContext context,
                ApplicationServiceId serviceId, object payload,
                out ApplicationServiceRequestHandle handle) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            handle = ApplicationServiceRequestHandle.Invalid;
            if (!TryValidateInteractiveRequestContext(context, serviceId,
                    out instance, out valid)) return valid;
            return ApplicationServiceSessionTable.Begin(
                context.InstanceHandle, serviceId, payload, true, out handle);
        }

        internal static ApplicationServiceResult BeginShellRequest(
                ApplicationServiceContext context, object payload,
                out ApplicationServiceRequestHandle handle) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            handle = ApplicationServiceRequestHandle.Invalid;
            if (!TryValidateInteractiveRequestContext(context,
                    ApplicationServiceId.Shell, out instance, out valid)) {
                return valid;
            }
            return ApplicationServiceSessionTable.Begin(
                context.InstanceHandle, ApplicationServiceId.Shell,
                payload, false, out handle);
        }

        internal static ApplicationServiceResult CompleteShellRequest(
                ApplicationServiceRequestHandle handle,
                ApplicationShellResult value) {
            ApplicationServiceSessionRecord session;
            if (!ApplicationServiceSessionTable.TryGet(handle, out session) ||
                    session.ServiceId != ApplicationServiceId.Shell) {
                return ApplicationServiceResult.InvalidContextResult();
            }
            if (value == null) {
                return ApplicationServiceResult.InvalidRequestResult();
            }
            return ApplicationServiceSessionTable.Complete(handle, value);
        }

        internal static ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationShellResult>>
                ObserveShellRequest(ApplicationServiceContext context,
                    ApplicationServiceRequestHandle handle) {
            ApplicationServiceSessionRecord session;
            ApplicationServiceResult valid =
                TryValidateExistingRequestContext(context, handle, out session);
            if (!valid.Succeeded || session.ServiceId !=
                    ApplicationServiceId.Shell) {
                return ApplicationServiceResult<
                    ApplicationServiceRequestStatus<ApplicationShellResult>>.Failure(
                        valid.Succeeded ? ApplicationServiceResultCode.InvalidContext :
                            valid.Code, valid.Succeeded ?
                            "Application service request is not a shell request" :
                            valid.BoundedDiagnostic);
            }
            if (session.Result != null &&
                    !(session.Result is ApplicationShellResult)) {
                return ApplicationServiceResult<
                    ApplicationServiceRequestStatus<ApplicationShellResult>>.Failure(
                        ApplicationServiceResultCode.BackendFailure,
                        "Shell backend returned an invalid result");
            }
            ApplicationServiceRequestStatus<ApplicationShellResult> status;
            switch (session.State) {
                case ApplicationServiceRequestState.Pending:
                    status = ApplicationServiceRequestStatus<
                        ApplicationShellResult>.PendingStatus();
                    break;
                case ApplicationServiceRequestState.Completed:
                    status = ApplicationServiceRequestStatus<
                        ApplicationShellResult>.CompletedStatus(
                            (ApplicationShellResult)session.Result);
                    break;
                case ApplicationServiceRequestState.Cancelled:
                    status = ApplicationServiceRequestStatus<
                        ApplicationShellResult>.CancelledStatus(
                            (ApplicationShellResult)session.Result);
                    break;
                default:
                    status = ApplicationServiceRequestStatus<
                        ApplicationShellResult>.FailedStatus(
                            (ApplicationShellResult)session.Result);
                    break;
            }
            if (session.State != ApplicationServiceRequestState.Pending) {
                ApplicationServiceSessionTable.ConsumeTerminal(handle);
            }
            return ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationShellResult>>.SuccessResult(
                    status);
        }

        internal static ApplicationServiceResult CancelShellRequest(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle) {
            ApplicationServiceResult valid =
                TryValidateExistingRequestContext(context, handle,
                    out ApplicationServiceSessionRecord session);
            if (!valid.Succeeded) return valid;
            if (session.ServiceId != ApplicationServiceId.Shell) {
                return ApplicationServiceResult.InvalidContextResult();
            }
            if (session.State != ApplicationServiceRequestState.Pending) {
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.InvalidState,
                    "Shell dispatch is already terminal and non-cancellable");
            }
            return ApplicationServiceSessionTable.CancelPending(handle,
                ApplicationShellResult.Failed(
                    ApplicationServiceResultCode.Cancelled, null,
                    "Shell request was cancelled"));
        }

        internal static ApplicationServiceResult TryObserveRequest(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle,
                out ApplicationServiceRequestStatus<object> status) {
            status = null;
            ApplicationServiceSessionRecord session;
            ApplicationServiceResult valid =
                TryValidateExistingRequestContext(context, handle,
                    out session);
            if (!valid.Succeeded) return valid;
            status = session.CreateStatus();
            return ApplicationServiceResult.SuccessResult();
        }

        internal static ApplicationServiceResult TryCancelRequest(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle) {
            ApplicationServiceSessionRecord session;
            ApplicationServiceResult valid =
                TryValidateExistingRequestContext(context, handle,
                    out session);
            if (!valid.Succeeded) return valid;
            ApplicationServiceSessionTable.Cancel(session);
            return ApplicationServiceResult.Failure(
                ApplicationServiceResultCode.Cancelled,
                "Application service request was cancelled");
        }

        internal static ApplicationServiceResult CompleteRequestForSelfTest(
                ApplicationServiceRequestHandle handle, object value) {
            return ApplicationServiceSessionTable.Complete(handle, value);
        }

        internal static ApplicationServiceResult CompleteDialogRequest(
                ApplicationServiceRequestHandle handle,
                ApplicationDialogOutcome outcome) {
            ApplicationServiceSessionRecord session;
            if (!ApplicationServiceSessionTable.TryGet(handle, out session) ||
                    session.ServiceId != ApplicationServiceId.Dialogs) {
                return ApplicationServiceResult.InvalidContextResult();
            }
            ApplicationServiceResult result =
                ApplicationServiceSessionTable.Complete(handle,
                    ApplicationDialogResult.From(outcome));
            if (result.Succeeded) {
                ApplicationServiceSessionTable.CloseTransientForRequest(handle);
            }
            return result;
        }

        internal static ApplicationServiceResult CompleteDialogRequestForSelfTest(
                ApplicationServiceRequestHandle handle,
                ApplicationDialogOutcome outcome) {
            return CompleteDialogRequest(handle, outcome);
        }

        internal static ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationDialogResult>>
                ObserveDialogRequest(ApplicationServiceContext context,
                    ApplicationServiceRequestHandle handle) {
            ApplicationServiceSessionRecord session;
            ApplicationServiceResult valid =
                TryValidateExistingRequestContext(context, handle, out session);
            if (!valid.Succeeded || session.ServiceId !=
                    ApplicationServiceId.Dialogs) {
                return ApplicationServiceResult<
                    ApplicationServiceRequestStatus<ApplicationDialogResult>>.Failure(
                        valid.Succeeded ? ApplicationServiceResultCode.InvalidContext :
                            valid.Code, valid.Succeeded ?
                            "Application service request is not a dialog" :
                            valid.BoundedDiagnostic);
            }
            if (session.Result != null &&
                    !(session.Result is ApplicationDialogResult)) {
                return ApplicationServiceResult<
                    ApplicationServiceRequestStatus<ApplicationDialogResult>>.Failure(
                        ApplicationServiceResultCode.BackendFailure,
                        "Dialog backend returned an invalid result");
            }
            ApplicationServiceRequestStatus<ApplicationDialogResult> status;
            switch (session.State) {
                case ApplicationServiceRequestState.Pending:
                    status = ApplicationServiceRequestStatus<
                        ApplicationDialogResult>.PendingStatus();
                    break;
                case ApplicationServiceRequestState.Completed:
                    status = ApplicationServiceRequestStatus<
                        ApplicationDialogResult>.CompletedStatus(
                            (ApplicationDialogResult)session.Result);
                    break;
                case ApplicationServiceRequestState.Cancelled:
                    status = ApplicationServiceRequestStatus<
                        ApplicationDialogResult>.CancelledStatus(
                            (ApplicationDialogResult)session.Result);
                    break;
                default:
                    status = ApplicationServiceRequestStatus<
                        ApplicationDialogResult>.FailedStatus(
                            (ApplicationDialogResult)session.Result);
                    break;
            }
            if (session.State != ApplicationServiceRequestState.Pending) {
                ApplicationServiceSessionTable.ConsumeTerminal(handle);
            }
            return ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationDialogResult>>.SuccessResult(
                    status);
        }

        internal static ApplicationServiceResult CancelDialogRequest(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle) {
            ApplicationServiceSessionRecord session;
            ApplicationServiceResult valid =
                TryValidateExistingRequestContext(context, handle, out session);
            if (!valid.Succeeded || session.ServiceId !=
                    ApplicationServiceId.Dialogs) {
                return valid.Succeeded
                    ? ApplicationServiceResult.InvalidContextResult() : valid;
            }
            ApplicationServiceResult cancelled =
                ApplicationServiceSessionTable.CancelPending(handle,
                    ApplicationDialogResult.From(
                        ApplicationDialogOutcome.Cancelled));
            if (cancelled.Succeeded) {
                ApplicationServiceSessionTable.CloseTransientForRequest(handle);
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.Cancelled,
                    "Dialog request was cancelled");
            }
            return cancelled;
        }

        internal static ApplicationServiceResult CompleteFileDialogRequest(
                ApplicationServiceRequestHandle handle,
                ApplicationFileDialogOutcome outcome, string selectedPath) {
            ApplicationServiceSessionRecord session;
            if (!ApplicationServiceSessionTable.TryGet(handle, out session) ||
                    !IsFileDialogService(session.ServiceId)) {
                return ApplicationServiceResult.InvalidContextResult();
            }
            ApplicationFileDialogResult value =
                ApplicationFileDialogResult.From(outcome, selectedPath);
            if (!value.IsValid) {
                return ApplicationServiceResult.InvalidRequestResult();
            }
            ApplicationServiceResult result =
                ApplicationServiceSessionTable.Complete(handle, value);
            if (result.Succeeded) {
                ApplicationServiceSessionTable.CloseTransientForRequest(handle);
            }
            return result;
        }

        internal static ApplicationServiceResult
                CompleteFileDialogRequestForSelfTest(
                    ApplicationServiceRequestHandle handle,
                    ApplicationFileDialogOutcome outcome,
                    string selectedPath) {
            return CompleteFileDialogRequest(handle, outcome, selectedPath);
        }

        internal static ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationFileDialogResult>>
                ObserveFileDialogRequest(ApplicationServiceContext context,
                    ApplicationServiceRequestHandle handle,
                    ApplicationServiceId serviceId) {
            ApplicationServiceSessionRecord session;
            ApplicationServiceResult valid =
                TryValidateExistingRequestContext(context, handle, out session);
            if (!valid.Succeeded || session.ServiceId != serviceId) {
                return ApplicationServiceResult<
                    ApplicationServiceRequestStatus<ApplicationFileDialogResult>>.Failure(
                        valid.Succeeded ? ApplicationServiceResultCode.InvalidContext :
                            valid.Code, valid.Succeeded ?
                            "Application service request is not this file dialog" :
                            valid.BoundedDiagnostic);
            }
            if (session.Result != null &&
                    !(session.Result is ApplicationFileDialogResult)) {
                return ApplicationServiceResult<
                    ApplicationServiceRequestStatus<ApplicationFileDialogResult>>.Failure(
                        ApplicationServiceResultCode.BackendFailure,
                        "File dialog backend returned an invalid result");
            }
            ApplicationServiceRequestStatus<ApplicationFileDialogResult> status;
            switch (session.State) {
                case ApplicationServiceRequestState.Pending:
                    status = ApplicationServiceRequestStatus<
                        ApplicationFileDialogResult>.PendingStatus();
                    break;
                case ApplicationServiceRequestState.Completed:
                    status = ApplicationServiceRequestStatus<
                        ApplicationFileDialogResult>.CompletedStatus(
                            (ApplicationFileDialogResult)session.Result);
                    break;
                case ApplicationServiceRequestState.Cancelled:
                    status = ApplicationServiceRequestStatus<
                        ApplicationFileDialogResult>.CancelledStatus(
                            (ApplicationFileDialogResult)session.Result);
                    break;
                default:
                    status = ApplicationServiceRequestStatus<
                        ApplicationFileDialogResult>.FailedStatus(
                            (ApplicationFileDialogResult)session.Result);
                    break;
            }
            if (session.State != ApplicationServiceRequestState.Pending) {
                ApplicationServiceSessionTable.ConsumeTerminal(handle);
            }
            return ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationFileDialogResult>>.SuccessResult(
                    status);
        }

        internal static ApplicationServiceResult CancelFileDialogRequest(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle,
                ApplicationServiceId serviceId) {
            ApplicationServiceSessionRecord session;
            ApplicationServiceResult valid =
                TryValidateExistingRequestContext(context, handle, out session);
            if (!valid.Succeeded || session.ServiceId != serviceId) {
                return valid.Succeeded
                    ? ApplicationServiceResult.InvalidContextResult() : valid;
            }
            ApplicationServiceResult cancelled =
                ApplicationServiceSessionTable.CancelPending(handle,
                    ApplicationFileDialogResult.From(
                        ApplicationFileDialogOutcome.Cancelled,
                        string.Empty));
            if (cancelled.Succeeded) {
                ApplicationServiceSessionTable.CloseTransientForRequest(handle);
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.Cancelled,
                    "File dialog request was cancelled");
            }
            return cancelled;
        }

        private static bool IsFileDialogService(ApplicationServiceId serviceId) {
            return serviceId == ApplicationServiceId.OpenFile ||
                   serviceId == ApplicationServiceId.SaveFile;
        }

        internal static void OnApplicationLifecycleChanged(
                ApplicationInstance instance,
                ApplicationInstanceLifecycleState previous,
                ApplicationInstanceLifecycleState current) {
            if (instance == null) return;
            if (current == ApplicationInstanceLifecycleState.Closing ||
                    current == ApplicationInstanceLifecycleState.Terminated ||
                    current == ApplicationInstanceLifecycleState.Failed) {
                ApplicationServiceSessionTable.CleanupForInstance(
                    instance.Handle, ApplicationInstanceLifecycle.Name(current));
            }
        }

        internal static void OnApplicationTerminating(
                ApplicationInstance instance, string reason) {
            if (instance == null) return;
            ApplicationServiceSessionTable.CleanupForInstance(
                instance.Handle, reason ?? "application termination");
        }

        public static bool TryValidateContext(
                ApplicationServiceContext context,
                ApplicationServiceId serviceId,
                out ApplicationInstance instance,
                out ApplicationServiceResult result) {
            Initialize();
            instance = null;
            result = ApplicationServiceResult.InvalidContextResult();
            if (!ApplicationServiceNames.IsKnown(serviceId) ||
                    !IsRegistered(serviceId)) {
                _invalidContextRejections++;
                result = ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.Unsupported,
                    "Application service is not registered");
                return false;
            }
            if (!TryValidateCommonContext(context, out instance, out result)) {
                return false;
            }
            if (context == null || !context.HasCapability(serviceId)) {
                _invalidContextRejections++;
                result = ApplicationServiceResult.InvalidContextResult();
                return false;
            }
            if (!IsServiceStateAllowed(serviceId, instance.LifecycleState)) {
                _invalidContextRejections++;
                result = ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.InvalidContext,
                    "Application service is unavailable in this lifecycle state");
                return false;
            }
            result = ApplicationServiceResult.SuccessResult();
            return true;
        }

        private static bool TryValidateInteractiveRequestContext(
                ApplicationServiceContext context,
                ApplicationServiceId serviceId,
                out ApplicationInstance instance,
                out ApplicationServiceResult result) {
            instance = null;
            result = ApplicationServiceResult.InvalidContextResult();
            if (!ApplicationServiceNames.IsKnown(serviceId) ||
                    !IsRegistered(serviceId)) {
                result = ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.Unsupported,
                    "Application service is not registered");
                return false;
            }
            if (!TryValidateCommonContext(context, out instance, out result)) {
                return false;
            }
            if (context == null || !context.HasCapability(serviceId)) {
                _invalidContextRejections++;
                result = ApplicationServiceResult.InvalidContextResult();
                return false;
            }
            if (instance.LifecycleState !=
                    ApplicationInstanceLifecycleState.Running &&
                    instance.LifecycleState !=
                    ApplicationInstanceLifecycleState.Activated) {
                _invalidContextRejections++;
                result = ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.InvalidState,
                    "New interaction requires a running application");
                return false;
            }
            result = ApplicationServiceResult.SuccessResult();
            return true;
        }

        private static ApplicationServiceResult TryValidateExistingRequestContext(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle,
                out ApplicationServiceSessionRecord session) {
            session = null;
            if (!handle.IsValid) {
                return ApplicationServiceResult.InvalidRequestResult();
            }
            if (!ApplicationServiceSessionTable.TryGet(handle, out session)) {
                _staleContextRejections++;
                return ApplicationServiceResult.InvalidContextResult();
            }
            ApplicationInstance instance;
            ApplicationServiceResult result;
            if (!TryValidateCommonContext(context, out instance, out result)) {
                return result;
            }
            if (context.InstanceHandle != session.Owner ||
                    context.ApplicationId != instance.DescriptorId) {
                _invalidContextRejections++;
                return ApplicationServiceResult.InvalidContextResult();
            }
            if (instance.LifecycleState !=
                    ApplicationInstanceLifecycleState.Running &&
                    instance.LifecycleState !=
                    ApplicationInstanceLifecycleState.Activated &&
                    instance.LifecycleState !=
                    ApplicationInstanceLifecycleState.Inactive) {
                _invalidContextRejections++;
                return ApplicationServiceResult.InvalidContextResult();
            }
            return ApplicationServiceResult.SuccessResult();
        }

        /// <summary>
        /// Deterministic registry/lifecycle proof.  The diagnostic uses the
        /// existing ApplicationInstanceRegistry launch and termination paths.
        /// </summary>
        public static bool RunSelfTest() {
            Initialize();
            _lastDialogSelfTestPassed = false;
            _lastFileDialogSelfTestPassed = false;
            _lastShellSelfTestPassed = false;
            _lastClipboardSelfTestPassed = false;
            _lastClipboardContractSelfTestPassed = false;
            _lastClipboardGenerationSelfTestPassed = false;
            _lastClipboardLifecycleSelfTestPassed = false;
            _lastClipboardResetSelfTestPassed = false;
            int passed = 0;
            int failed = 0;
            string firstFailure = null;

            Check(RunContractSelfTest(), "contract bounds", ref passed,
                ref failed, ref firstFailure);
            Check(RunPhase9ContractSelfTest(), "phase 9 contract bounds",
                ref passed, ref failed, ref firstFailure);
            Check(RunPhase10ContractSelfTest(), "phase 10 contract bounds",
                ref passed, ref failed, ref firstFailure);
            _lastClipboardContractSelfTestPassed =
                RunPhase11ContractSelfTest();
            Check(_lastClipboardContractSelfTestPassed,
                "phase 11 clipboard contract bounds", ref passed,
                ref failed, ref firstFailure);
            Check(!TryRegisterForSelfTest(ApplicationServiceId.Notifications),
                "duplicate service registration rejected", ref passed,
                ref failed, ref firstFailure);
            Check(!TryGetAccessForSelfTest((ApplicationServiceId)99),
                "missing service rejected", ref passed, ref failed,
                ref firstFailure);

            ApplicationInstance instance = null;
            ApplicationServiceContext context = null;
            ApplicationServiceResult result;
            bool reused;
            LaunchResult launchFailure;
            string id = "selftest.phase8.services";
            bool started = ApplicationInstanceRegistry.TryBeginLaunch(
                id, ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId(id, null, null,
                    LaunchActivationIntent.NewInstance),
                out instance, out reused, out launchFailure);
            if (started && instance != null) {
                bool completed = ApplicationInstanceRegistry.TryCompleteLaunch(
                    instance, false, out launchFailure);
                bool created = TryCreateContext(instance.Handle, out context,
                    out result);
                Check(completed && created && result.Succeeded,
                    "valid service context", ref passed, ref failed,
                    ref firstFailure);

                ApplicationInstance resolved;
                bool serviceValid = TryValidateContext(context,
                    ApplicationServiceId.SystemInformation, out resolved,
                    out result);
                Check(serviceValid && resolved == instance && result.Succeeded,
                    "valid system information context", ref passed,
                    ref failed, ref firstFailure);

                ApplicationServiceAccess access;
                bool accessCreated = TryGetAccess(context, out access,
                    out result);
                ApplicationNotificationRequest request =
                    ApplicationNotificationRequest.Create(
                        "Phase 8", "Service self-test",
                        ApplicationNotificationSeverity.Info);
                bool notification = accessCreated && access.Notifications != null &&
                    access.Notifications.Publish(context, request).Succeeded;
                Check(notification, "notification publish", ref passed,
                    ref failed, ref firstFailure);
                ApplicationNotificationRequest invalidRequest =
                    ApplicationNotificationRequest.Create(
                        Repeat('t', ApplicationNotificationRequest.MaxTitleLength + 1),
                        "body", ApplicationNotificationSeverity.Info);
                Check(accessCreated && access.Notifications.Publish(
                    context, invalidRequest).Code ==
                    ApplicationServiceResultCode.InvalidRequest,
                    "invalid notification request rejected", ref passed,
                    ref failed, ref firstFailure);
                Check(accessCreated && access.Notifications.Clear(context).Succeeded,
                    "application notification clear", ref passed, ref failed,
                    ref firstFailure);
                Check(RunSettingsSelfTest(), "application settings service",
                    ref passed, ref failed, ref firstFailure);
                Check(RunSystemInformationSelfTest(context,
                    accessCreated ? access : null),
                    "system information service", ref passed, ref failed,
                    ref firstFailure);
                Check(RunRequestSessionSelfTest(),
                    "request session lifecycle", ref passed, ref failed,
                    ref firstFailure);
                _lastDialogSelfTestPassed = RunDialogServiceSelfTest(
                    context, access);
                Check(_lastDialogSelfTestPassed,
                    "dialog service lifecycle", ref passed, ref failed,
                    ref firstFailure);
                _lastFileDialogSelfTestPassed = RunFileDialogServiceSelfTest(
                    context, access);
                Check(_lastFileDialogSelfTestPassed,
                    "file dialog service lifecycle", ref passed,
                    ref failed, ref firstFailure);
                _lastShellSelfTestPassed = RunShellServiceSelfTest(
                    context, access);
                Check(_lastShellSelfTestPassed,
                    "shell service lifecycle", ref passed,
                    ref failed, ref firstFailure);
                _lastResourceStorageSelfTestPassed =
                    RunResourceStorageSelfTest(context, access);
                Check(_lastResourceStorageSelfTestPassed,
                    "resource and storage services", ref passed,
                    ref failed, ref firstFailure);
                _lastClipboardSelfTestPassed =
                    RunClipboardSelfTest(context, access);
                Check(_lastClipboardSelfTestPassed,
                    "clipboard service", ref passed, ref failed,
                    ref firstFailure);
                Check(RunTransientServiceWindowSelfTest(),
                    "transient service window ownership", ref passed,
                    ref failed, ref firstFailure);

                bool suspended = instance.TryTransition(
                    ApplicationInstanceLifecycleState.Suspended);
                bool suspendedRejected = suspended &&
                    !TryValidateContext(context,
                        ApplicationServiceId.SystemInformation, out resolved,
                        out result) &&
                    result.Code == ApplicationServiceResultCode.InvalidContext;
                Check(suspendedRejected, "suspended context rejected",
                    ref passed, ref failed, ref firstFailure);

                bool resumed = instance.TryTransition(
                    ApplicationInstanceLifecycleState.Inactive);
                bool closing = resumed && instance.TryTransition(
                    ApplicationInstanceLifecycleState.Closing);
                bool closingRejected = closing &&
                    !TryValidateContext(context,
                        ApplicationServiceId.SystemInformation, out resolved,
                        out result) &&
                    result.Code == ApplicationServiceResultCode.InvalidContext;
                Check(closingRejected, "closing context rejected", ref passed,
                    ref failed, ref firstFailure);

                Check(ApplicationInstanceRegistry.TryTerminate(instance,
                        "service registry self-test cleanup"),
                    "service test cleanup", ref passed, ref failed,
                    ref firstFailure);

                bool staleRejected = !TryValidateContext(context,
                    ApplicationServiceId.SystemInformation, out resolved,
                    out result) &&
                    result.Code == ApplicationServiceResultCode.InvalidContext;
                Check(staleRejected, "stale service context rejected",
                    ref passed, ref failed, ref firstFailure);
                Check(accessCreated &&
                    access.Notifications.Publish(context, request).Code ==
                        ApplicationServiceResultCode.InvalidContext,
                    "stale notification context", ref passed, ref failed,
                    ref firstFailure);
            } else {
                Check(false, "service test instance launch", ref passed,
                    ref failed, ref firstFailure);
            }

            bool clean = ApplicationInstanceRegistry.ActiveCount == 0;
            Check(clean, "service registry instance cleanup", ref passed,
                ref failed, ref firstFailure);
            _lastSelfTestFailure = failed == 0 && clean ? "pass" :
                (firstFailure ?? "instance cleanup");
            return failed == 0 && clean;
        }

        private static bool RunResourceStorageSelfTest(
                ApplicationServiceContext context,
                ApplicationServiceAccess access) {
            if (context == null || access == null || access.Resources == null ||
                    access.Storage == null) return false;

            _lastResourceChunkSelfTestPassed = false;
            _lastStoragePathSelfTestPassed = false;
            _lastPersistentUnavailableSelfTestPassed = false;
            _lastStorageAppScopeSelfTestPassed = false;
            _lastStorageResetSelfTestPassed = false;
            ResetTemporaryStorageForAppModel();
            ApplicationInstance sharedFirst = null;
            ApplicationInstance sharedSecond = null;
            ApplicationInstance other = null;
            ApplicationInstance survivor = null;
            ApplicationInstance survivorRelaunched = null;
            ApplicationServiceContext sharedFirstContext = null;
            ApplicationServiceContext sharedSecondContext = null;
            ApplicationServiceContext otherContext = null;
            ApplicationServiceContext survivorContext = null;
            ApplicationServiceContext survivorRelaunchedContext = null;
            try {
                ApplicationResourceRequest resourceRequest =
                    ApplicationResourceRequest.Create("diagnostic.fixture");
                ApplicationServiceResult<ApplicationResourceMetadata> metadata =
                    access.Resources.GetMetadata(context, resourceRequest);
                if (!metadata.Succeeded || metadata.Value == null ||
                        metadata.Value.Length <= 0 || !metadata.Value.IsReadable) {
                    return false;
                }

                ApplicationResourceReadRequest firstReadRequest =
                    ApplicationResourceReadRequest.Create(
                        "diagnostic.fixture", 0, 8);
                ApplicationServiceResult<ApplicationResourceReadResult> firstRead =
                    access.Resources.Read(context, firstReadRequest);
                if (!firstRead.Succeeded || firstRead.Value == null ||
                        firstRead.Value.BytesRead <= 0 ||
                        firstRead.Value.BytesRead != firstRead.Value.Bytes.Length ||
                        firstRead.Value.EndOfResource) return false;

                ApplicationResourceReadRequest endRequest =
                    ApplicationResourceReadRequest.Create(
                        "diagnostic.fixture", metadata.Value.Length, 1);
                ApplicationServiceResult<ApplicationResourceReadResult> endRead =
                    access.Resources.Read(context, endRequest);
                if (!endRead.Succeeded || endRead.Value == null ||
                        endRead.Value.BytesRead != 0 ||
                        endRead.Value.Bytes.Length != 0 ||
                        !endRead.Value.EndOfResource) return false;

                ApplicationResourceReadRequest beyondRequest =
                    ApplicationResourceReadRequest.Create(
                        "diagnostic.fixture", metadata.Value.Length + 1, 1);
                if (access.Resources.Read(context, beyondRequest).Code !=
                        ApplicationServiceResultCode.InvalidRequest) return false;
                if (access.Resources.GetMetadata(context,
                        ApplicationResourceRequest.Create("missing.fixture")).Code !=
                        ApplicationServiceResultCode.NotFound) return false;
                _lastResourceChunkSelfTestPassed = true;

                CSharpApplicationStorageService storage =
                    access.Storage as CSharpApplicationStorageService;
                if (storage == null) return false;
                ApplicationStorageWriteRequest temporaryWrite =
                    ApplicationStorageWriteRequest.Create(
                        ApplicationStorageNamespace.Temporary, "state.bin",
                        new byte[] { 1, 2, 3 });
                if (!access.Storage.Write(context, temporaryWrite).Succeeded) {
                    return false;
                }
                ApplicationServiceResult<ApplicationStorageReadResult> shortRead =
                    access.Storage.Read(context,
                        ApplicationStorageReadRequest.Create(
                            ApplicationStorageNamespace.Temporary, "state.bin",
                            0, 2));
                if (!shortRead.Succeeded || shortRead.Value == null ||
                        shortRead.Value.BytesRead != 2 ||
                        shortRead.Value.Bytes.Length != 2 ||
                        shortRead.Value.EndOfResource) return false;
                ApplicationServiceResult<ApplicationStorageReadResult> endStorageRead =
                    access.Storage.Read(context,
                        ApplicationStorageReadRequest.Create(
                            ApplicationStorageNamespace.Temporary, "state.bin",
                            3, 1));
                if (!endStorageRead.Succeeded || endStorageRead.Value == null ||
                        endStorageRead.Value.BytesRead != 0 ||
                        !endStorageRead.Value.EndOfResource) return false;
                if (access.Storage.Read(context,
                        ApplicationStorageReadRequest.Create(
                            ApplicationStorageNamespace.Temporary, "state.bin",
                            4, 1)).Code !=
                        ApplicationServiceResultCode.InvalidRequest) return false;

                int backendCallsBeforeEscape = storage.BackendCallCount;
                ApplicationStorageRequest escape = ApplicationStorageRequest.Create(
                    ApplicationStorageNamespace.Persistent, "../escape");
                bool escapeRejected = !escape.IsValid &&
                    access.Storage.Exists(context, escape).Code ==
                        ApplicationServiceResultCode.InvalidRequest &&
                    storage.BackendCallCount == backendCallsBeforeEscape;
                if (!escapeRejected) return false;
                _lastStoragePathSelfTestPassed = true;

                ApplicationStorageRequest persistentPath =
                    ApplicationStorageRequest.Create(
                        ApplicationStorageNamespace.Persistent, "state.bin");
                if (access.Storage.Write(context,
                        ApplicationStorageWriteRequest.Create(
                            ApplicationStorageNamespace.Persistent, "state.bin",
                            new byte[] { 9 })).Code !=
                        ApplicationServiceResultCode.ResourceUnavailable ||
                    access.Storage.Delete(context, persistentPath).Code !=
                        ApplicationServiceResultCode.ResourceUnavailable) {
                    return false;
                }
                ApplicationServiceResult<ApplicationStorageReadResult> retained =
                    access.Storage.Read(context,
                        ApplicationStorageReadRequest.Create(
                            ApplicationStorageNamespace.Temporary, "state.bin",
                            0, 3));
                if (!retained.Succeeded || retained.Value == null ||
                        retained.Value.BytesRead != 3 ||
                        retained.Value.Bytes[0] != 1 ||
                        retained.Value.Bytes[1] != 2 ||
                        retained.Value.Bytes[2] != 3) return false;
                _lastPersistentUnavailableSelfTestPassed = true;

                bool sharedStarted = TryCreateServiceSelfTestInstance(
                    "selftest.phase10.shared", out sharedFirst,
                    out sharedFirstContext);
                bool secondStarted = TryCreateServiceSelfTestInstance(
                    "selftest.phase10.shared", out sharedSecond,
                    out sharedSecondContext);
                if (!sharedStarted || !secondStarted ||
                        !access.Storage.Write(sharedFirstContext,
                            ApplicationStorageWriteRequest.Create(
                                ApplicationStorageNamespace.Temporary,
                                "shared.bin", new byte[] { 7 })).Succeeded ||
                    !access.Storage.Exists(sharedSecondContext,
                        ApplicationStorageRequest.Create(
                            ApplicationStorageNamespace.Temporary,
                            "shared.bin")).Succeeded ||
                    !access.Storage.Exists(sharedSecondContext,
                        ApplicationStorageRequest.Create(
                            ApplicationStorageNamespace.Temporary,
                            "shared.bin")).Value) return false;
                if (!ApplicationInstanceRegistry.TryTerminate(sharedSecond,
                        "Phase 10 shared instance cleanup") ||
                    !access.Storage.Exists(sharedFirstContext,
                        ApplicationStorageRequest.Create(
                            ApplicationStorageNamespace.Temporary,
                            "shared.bin")).Succeeded ||
                    !access.Storage.Exists(sharedFirstContext,
                        ApplicationStorageRequest.Create(
                            ApplicationStorageNamespace.Temporary,
                            "shared.bin")).Value) return false;
                sharedSecond = null;

                bool survivorStarted = TryCreateServiceSelfTestInstance(
                    "selftest.phase10.survival", out survivor,
                    out survivorContext);
                if (!survivorStarted || !access.Storage.Write(survivorContext,
                        ApplicationStorageWriteRequest.Create(
                            ApplicationStorageNamespace.Temporary,
                            "survive.bin", new byte[] { 4 })).Succeeded ||
                    !ApplicationInstanceRegistry.TryTerminate(survivor,
                        "Phase 10 termination survival") ) return false;
                survivor = null;
                bool survivorRelaunch = TryCreateServiceSelfTestInstance(
                    "selftest.phase10.survival", out survivorRelaunched,
                    out survivorRelaunchedContext);
                if (!survivorRelaunch ||
                    !access.Storage.Exists(survivorRelaunchedContext,
                        ApplicationStorageRequest.Create(
                            ApplicationStorageNamespace.Temporary,
                            "survive.bin")).Succeeded ||
                    !access.Storage.Exists(survivorRelaunchedContext,
                        ApplicationStorageRequest.Create(
                            ApplicationStorageNamespace.Temporary,
                            "survive.bin")).Value) return false;

                bool otherStarted = TryCreateServiceSelfTestInstance(
                    "selftest.phase10.other", out other, out otherContext);
                if (!otherStarted) return false;
                ApplicationServiceResult<bool> otherExists = access.Storage.Exists(
                    otherContext, ApplicationStorageRequest.Create(
                        ApplicationStorageNamespace.Temporary, "state.bin"));
                if (!otherExists.Succeeded || otherExists.Value ||
                        access.Storage.Delete(otherContext,
                            ApplicationStorageRequest.Create(
                                ApplicationStorageNamespace.Temporary,
                                "state.bin")).Code !=
                            ApplicationServiceResultCode.NotFound) return false;
                _lastStorageAppScopeSelfTestPassed = true;

                ResetTemporaryStorageForAppModel();
                ApplicationServiceResult<bool> resetState = access.Storage.Exists(
                    context,
                        ApplicationStorageRequest.Create(
                            ApplicationStorageNamespace.Temporary,
                            "state.bin"));
                ApplicationServiceResult<bool> resetSurvivor = access.Storage.Exists(
                    survivorRelaunchedContext,
                        ApplicationStorageRequest.Create(
                            ApplicationStorageNamespace.Temporary,
                            "survive.bin"));
                if (!resetState.Succeeded || resetState.Value ||
                    !resetSurvivor.Succeeded || resetSurvivor.Value) return false;
                _lastStorageResetSelfTestPassed = true;
                return true;
            } finally {
                if (sharedSecond != null) ApplicationInstanceRegistry.TryTerminate(
                    sharedSecond, "Phase 10 shared cleanup");
                if (sharedFirst != null) ApplicationInstanceRegistry.TryTerminate(
                    sharedFirst, "Phase 10 shared cleanup");
                if (other != null) ApplicationInstanceRegistry.TryTerminate(
                    other, "Phase 10 isolation cleanup");
                if (survivor != null) ApplicationInstanceRegistry.TryTerminate(
                    survivor, "Phase 10 survival cleanup");
                if (survivorRelaunched != null) ApplicationInstanceRegistry.TryTerminate(
                    survivorRelaunched, "Phase 10 survival cleanup");
                ResetTemporaryStorageForAppModel();
            }
        }

        private static bool RunClipboardSelfTest(
                ApplicationServiceContext context,
                ApplicationServiceAccess access) {
            if (context == null || access == null || access.Clipboard == null) {
                return false;
            }

            _lastClipboardGenerationSelfTestPassed = false;
            _lastClipboardLifecycleSelfTestPassed = false;
            _lastClipboardResetSelfTestPassed = false;
            ResetForAppModel();

            ApplicationInstance source = null;
            ApplicationInstance reader = null;
            ApplicationServiceContext sourceContext = null;
            ApplicationServiceContext readerContext = null;
            try {
                ApplicationServiceResult<ApplicationClipboardSnapshot> initial =
                    access.Clipboard.GetText(context);
                if (!initial.Succeeded || initial.Value == null ||
                        initial.Value.HasValue || initial.Value.Text.Length != 0 ||
                        initial.Value.SourceAppId.Length != 0 ||
                        initial.Value.Generation != 0UL) return false;

                bool sourceStarted = TryCreateServiceSelfTestInstance(
                    "selftest.phase11.source", out source, out sourceContext);
                bool readerStarted = TryCreateServiceSelfTestInstance(
                    "selftest.phase11.reader", out reader, out readerContext);
                if (!sourceStarted || !readerStarted || sourceContext == null ||
                        readerContext == null) return false;

                ApplicationServiceResult sourceWrite = access.Clipboard.SetText(
                    sourceContext,
                    ApplicationClipboardWriteRequest.Create("source text"));
                ApplicationServiceResult<ApplicationClipboardSnapshot> firstRead =
                    access.Clipboard.GetText(readerContext);
                if (!sourceWrite.Succeeded || !firstRead.Succeeded ||
                        firstRead.Value == null || !firstRead.Value.HasValue ||
                        firstRead.Value.Text != "source text" ||
                        firstRead.Value.SourceAppId != sourceContext.ApplicationId ||
                        firstRead.Value.Generation != 1UL) return false;
                ApplicationClipboardSnapshot firstSnapshot = firstRead.Value;

                if (!ApplicationInstanceRegistry.TryTerminate(source,
                        "Phase 11 source termination proof")) return false;
                source = null;
                ApplicationServiceResult<ApplicationClipboardSnapshot> afterTermination =
                    access.Clipboard.GetText(readerContext);
                bool lifecycle = afterTermination.Succeeded &&
                    afterTermination.Value != null &&
                    afterTermination.Value.HasValue &&
                    afterTermination.Value.Text == "source text" &&
                    afterTermination.Value.SourceAppId ==
                        sourceContext.ApplicationId &&
                    afterTermination.Value.Generation == 1UL;
                _lastClipboardLifecycleSelfTestPassed = lifecycle;
                if (!lifecycle) return false;

                ApplicationServiceResult replacement = access.Clipboard.SetText(
                    readerContext,
                    ApplicationClipboardWriteRequest.Create("reader text"));
                ApplicationServiceResult<ApplicationClipboardSnapshot> secondRead =
                    access.Clipboard.GetText(context);
                bool immutable = replacement.Succeeded && secondRead.Succeeded &&
                    secondRead.Value != null && secondRead.Value.HasValue &&
                    secondRead.Value.Text == "reader text" &&
                    secondRead.Value.SourceAppId == readerContext.ApplicationId &&
                    secondRead.Value.Generation == 2UL &&
                    firstSnapshot.HasValue && firstSnapshot.Text == "source text" &&
                    firstSnapshot.SourceAppId == sourceContext.ApplicationId &&
                    firstSnapshot.Generation == 1UL;
                if (!immutable) return false;

                ApplicationServiceResult emptyWrite = access.Clipboard.SetText(
                    readerContext,
                    ApplicationClipboardWriteRequest.Create(string.Empty));
                ApplicationServiceResult<ApplicationClipboardSnapshot> emptyRead =
                    access.Clipboard.GetText(context);
                bool generation = emptyWrite.Succeeded && emptyRead.Succeeded &&
                    emptyRead.Value != null && emptyRead.Value.HasValue &&
                    emptyRead.Value.Text.Length == 0 &&
                    emptyRead.Value.SourceAppId == readerContext.ApplicationId &&
                    emptyRead.Value.Generation == 3UL;
                if (!generation) return false;

                ApplicationServiceResult clear = access.Clipboard.Clear(context);
                ApplicationServiceResult<ApplicationClipboardSnapshot> cleared =
                    access.Clipboard.GetText(readerContext);
                ApplicationServiceResult repeatedClear =
                    access.Clipboard.Clear(readerContext);
                ApplicationServiceResult<ApplicationClipboardSnapshot> repeated =
                    access.Clipboard.GetText(context);
                generation = generation && clear.Succeeded &&
                    cleared.Succeeded && cleared.Value != null &&
                    !cleared.Value.HasValue && cleared.Value.Text.Length == 0 &&
                    cleared.Value.SourceAppId.Length == 0 &&
                    cleared.Value.Generation == 4UL &&
                    repeatedClear.Succeeded && repeated.Succeeded &&
                    repeated.Value != null && !repeated.Value.HasValue &&
                    repeated.Value.Generation == 4UL;
                _lastClipboardGenerationSelfTestPassed = generation;
                if (!generation) return false;

                ResetForAppModel();
                ApplicationServiceResult<ApplicationClipboardSnapshot> reset =
                    access.Clipboard.GetText(readerContext);
                bool resetPassed = reset.Succeeded && reset.Value != null &&
                    !reset.Value.HasValue && reset.Value.Text.Length == 0 &&
                    reset.Value.SourceAppId.Length == 0 &&
                    reset.Value.Generation == 0UL;
                _lastClipboardResetSelfTestPassed = resetPassed;
                return resetPassed;
            } finally {
                if (source != null) ApplicationInstanceRegistry.TryTerminate(
                    source, "Phase 11 clipboard source cleanup");
                if (reader != null) ApplicationInstanceRegistry.TryTerminate(
                    reader, "Phase 11 clipboard reader cleanup");
                ResetForAppModel();
            }
        }

        private static bool TryCreateServiceSelfTestInstance(string id,
                out ApplicationInstance instance,
                out ApplicationServiceContext context) {
            instance = null;
            context = null;
            bool reused;
            LaunchResult failure;
            if (!ApplicationInstanceRegistry.TryBeginLaunch(
                    id, ApplicationInstancePolicy.MultiInstance,
                    LaunchRequest.ForAppId(id, null, null,
                        LaunchActivationIntent.NewInstance), out instance,
                    out reused, out failure) || instance == null) return false;
            if (!ApplicationInstanceRegistry.TryCompleteLaunch(instance, false,
                    out failure)) return false;
            ApplicationServiceResult result;
            return TryCreateContext(instance.Handle, out context, out result) &&
                result.Succeeded;
        }

        private static bool RunTransientServiceWindowSelfTest() {
            if (WindowManager.Windows == null || Framebuffer.Graphics == null ||
                    WindowManager.font == null) return true;

            int startingEntries = TaskbarApplicationEntryRegistry.EntryCount;
            int startingStaleOwnership =
                ApplicationInstanceRegistry.StaleOwnershipCount;
            ApplicationInstance instance = null;
            ApplicationServiceContext context = null;
            ApplicationServiceRequestHandle requestHandle =
                ApplicationServiceRequestHandle.Invalid;
            ServiceWindowProbe window = null;
            bool result = false;
            try {
                bool reused;
                LaunchResult failure;
                bool started = ApplicationInstanceRegistry.TryBeginLaunch(
                    "selftest.phase9.transient-window",
                    ApplicationInstancePolicy.MultiInstance,
                    LaunchRequest.ForAppId("selftest.phase9.transient-window",
                        null, null, LaunchActivationIntent.NewInstance),
                    out instance, out reused, out failure);
                bool completed = started &&
                    ApplicationInstanceRegistry.TryCompleteLaunch(instance,
                        false, out failure);
                ApplicationServiceResult serviceResult;
                bool contextCreated = completed &&
                    TryCreateContext(instance.Handle, out context,
                        out serviceResult);
                ApplicationServiceResult begun = contextCreated
                    ? BeginInteractiveRequest(context,
                        ApplicationServiceId.Dialogs, "transient", out requestHandle)
                    : ApplicationServiceResult.InvalidContextResult();
                int ownedBefore = instance == null ? -1 :
                    instance.OwnedWindowCount;
                ApplicationInstanceObservation beforeObservation =
                    new ApplicationInstanceObservation(instance);
                window = new ServiceWindowProbe();
                bool registered = begun.Succeeded &&
                    WindowManager.RegisterTransientServiceWindow(window,
                        instance.Handle, requestHandle);
                ApplicationServiceTransientWindowMetadata metadata;
                bool metadataVisible =
                    ApplicationServiceSessionTable.TryGetTransientOwner(
                        window, out metadata);
                TaskbarApplicationEntryRegistry.Reconcile();
                ApplicationInstanceObservation afterObservation =
                    new ApplicationInstanceObservation(instance);
                bool ordinaryProjectionUnchanged =
                    instance.OwnedWindowCount == ownedBefore &&
                    afterObservation.OwnedWindowCount ==
                        beforeObservation.OwnedWindowCount &&
                    TaskbarApplicationEntryRegistry.EntryCount == startingEntries &&
                    !TaskbarApplicationEntryRegistry.TryGetForWindow(window,
                        out TaskbarApplicationEntry ignoredEntry);
                bool semanticOwnership = registered && metadataVisible &&
                    metadata.Owner == instance.Handle &&
                    metadata.RequestHandle == requestHandle &&
                    window.IsServiceSessionWindow;
                bool terminated = instance != null &&
                    ApplicationInstanceRegistry.TryTerminate(instance,
                        "transient service window self-test cleanup");
                WindowManager.CleanupClosedWindows();
                TaskbarApplicationEntryRegistry.Reconcile();
                bool windowRemoved = window != null &&
                    !window.IsServiceSessionWindow;
                bool sessionsCleaned =
                    ApplicationServiceSessionTable.TransientWindowCount == 0 &&
                    ApplicationServiceSessionTable.OrphanTransientWindowCount == 0;
                bool staleUnchanged = ApplicationInstanceRegistry.StaleOwnershipCount ==
                    startingStaleOwnership;
                bool entriesUnchanged = TaskbarApplicationEntryRegistry.EntryCount ==
                    startingEntries;
                bool cleaned = terminated && windowRemoved && sessionsCleaned &&
                    staleUnchanged && entriesUnchanged;
                result = ordinaryProjectionUnchanged && semanticOwnership &&
                    cleaned;
            } catch {
                result = false;
            }
            if (window != null && window.IsServiceSessionWindow) {
                window.CloseForApplicationTermination();
                WindowManager.CleanupClosedWindows();
            }
            if (instance != null &&
                    ApplicationInstanceRegistry.TryGet(instance.Handle,
                        out ApplicationInstance retained)) {
                ApplicationInstanceRegistry.TryTerminate(retained,
                    "transient service window assertion cleanup");
            }
            ApplicationServiceSessionTable.Reset();
            return result;
        }

        private static bool RunDialogServiceSelfTest(
                ApplicationServiceContext context,
                ApplicationServiceAccess access) {
            if (context == null || access == null || access.Dialogs == null) {
                return false;
            }
            ApplicationDialogRequest information =
                ApplicationDialogRequest.Create(ApplicationDialogKind.Information,
                    "Information", "Bounded message",
                    ApplicationDialogButtonSet.Acknowledge);
            ApplicationDialogRequest invalidButtons =
                ApplicationDialogRequest.Create(ApplicationDialogKind.Error,
                    "Error", "Invalid button combination",
                    ApplicationDialogButtonSet.AcceptRejectCancel);
            if (!information.IsValid || invalidButtons.IsValid) return false;

            ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                access.Dialogs.Begin(context, information);
            if (!begun.Succeeded || !begun.Value.IsValid) return false;
            ApplicationServiceResult<ApplicationServiceRequestHandle> duplicate =
                access.Dialogs.Begin(context, information);
            if (duplicate.Code != ApplicationServiceResultCode.Conflict) {
                return false;
            }
            ApplicationServiceResult complete =
                CompleteDialogRequestForSelfTest(begun.Value,
                    ApplicationDialogOutcome.Accepted);
            ApplicationServiceResult<ApplicationServiceRequestStatus<
                ApplicationDialogResult>> observed =
                access.Dialogs.Observe(context, begun.Value);
            if (!complete.Succeeded || !observed.Succeeded ||
                    observed.Value == null ||
                    observed.Value.State != ApplicationServiceRequestState.Completed ||
                    observed.Value.Value == null ||
                    observed.Value.Value.Outcome !=
                        ApplicationDialogOutcome.Accepted) return false;

            ApplicationDialogRequest confirmation =
                ApplicationDialogRequest.Create(ApplicationDialogKind.Confirmation,
                    "Confirm", "Continue?",
                    ApplicationDialogButtonSet.AcceptRejectCancel);
            begun = access.Dialogs.Begin(context, confirmation);
            if (!begun.Succeeded) return false;
            ApplicationInstance owner;
            ApplicationServiceResult valid;
            bool contextValid = TryValidateContext(context,
                ApplicationServiceId.Dialogs, out owner, out valid);
            if (!contextValid || !valid.Succeeded || owner == null) return false;
            bool inactive = owner.TryTransition(
                ApplicationInstanceLifecycleState.Inactive);
            observed = access.Dialogs.Observe(context, begun.Value);
            ApplicationServiceResult<ApplicationServiceRequestHandle> inactiveBegin =
                access.Dialogs.Begin(context, confirmation);
            bool inactiveObserved = inactive && observed.Succeeded &&
                observed.Value != null &&
                observed.Value.State == ApplicationServiceRequestState.Pending;
            bool inactiveRejected = inactiveBegin.Code ==
                ApplicationServiceResultCode.InvalidState;
            bool running = owner.TryTransition(
                ApplicationInstanceLifecycleState.Running);
            if (!inactiveObserved || !inactiveRejected || !running) return false;

            complete = CompleteDialogRequestForSelfTest(begun.Value,
                ApplicationDialogOutcome.Rejected);
            observed = access.Dialogs.Observe(context, begun.Value);
            if (!complete.Succeeded || !observed.Succeeded ||
                    observed.Value == null || observed.Value.Value == null ||
                    observed.Value.Value.Outcome !=
                        ApplicationDialogOutcome.Rejected) return false;

            begun = access.Dialogs.Begin(context, information);
            if (!begun.Succeeded) return false;
            complete = CompleteDialogRequestForSelfTest(begun.Value,
                ApplicationDialogOutcome.Closed);
            observed = access.Dialogs.Observe(context, begun.Value);
            if (!complete.Succeeded || !observed.Succeeded ||
                    observed.Value == null || observed.Value.Value == null ||
                    observed.Value.Value.Outcome !=
                        ApplicationDialogOutcome.Closed) return false;

            begun = access.Dialogs.Begin(context, information);
            if (!begun.Succeeded) return false;
            ApplicationServiceResult cancelled = access.Dialogs.Cancel(
                context, begun.Value);
            observed = access.Dialogs.Observe(context, begun.Value);
            if (cancelled.Code != ApplicationServiceResultCode.Cancelled ||
                    !observed.Succeeded || observed.Value == null ||
                    observed.Value.State != ApplicationServiceRequestState.Cancelled ||
                    observed.Value.Value == null ||
                    observed.Value.Value.Outcome !=
                        ApplicationDialogOutcome.Cancelled) return false;

            begun = access.Dialogs.Begin(context, information);
            if (!begun.Succeeded) return false;
            complete = CompleteDialogRequestForSelfTest(begun.Value,
                ApplicationDialogOutcome.BackendFailure);
            observed = access.Dialogs.Observe(context, begun.Value);
            if (!complete.Succeeded || !observed.Succeeded ||
                    observed.Value == null ||
                    observed.Value.State != ApplicationServiceRequestState.Completed ||
                    observed.Value.Value == null ||
                    observed.Value.Value.Outcome !=
                        ApplicationDialogOutcome.BackendFailure) return false;

            ApplicationInstance cleanupOwner = null;
            ApplicationServiceContext cleanupContext = null;
            ApplicationServiceAccess cleanupAccess = null;
            ApplicationServiceResult cleanupResult =
                ApplicationServiceResult.InvalidContextResult();
            bool cleanupReused;
            LaunchResult cleanupFailure;
            bool cleanupStarted = ApplicationInstanceRegistry.TryBeginLaunch(
                "selftest.phase9.dialogs.cleanup",
                ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId("selftest.phase9.dialogs.cleanup", null,
                    null, LaunchActivationIntent.NewInstance), out cleanupOwner,
                out cleanupReused, out cleanupFailure);
            bool cleanupReady = cleanupStarted &&
                ApplicationInstanceRegistry.TryCompleteLaunch(cleanupOwner,
                    false, out cleanupFailure) &&
                TryCreateContextAndAccess(cleanupOwner.Handle, out cleanupContext,
                    out cleanupAccess, out cleanupResult);
            if (!cleanupReady || cleanupAccess == null ||
                    cleanupAccess.Dialogs == null) return false;
            ApplicationServiceResult<ApplicationServiceRequestHandle> cleanupBegun =
                cleanupAccess.Dialogs.Begin(cleanupContext, information);
            ApplicationInstanceHandle stale = cleanupOwner.Handle;
            if (!cleanupBegun.Succeeded ||
                    !ApplicationInstanceRegistry.TryTerminate(cleanupOwner,
                        "dialog service self-test cleanup")) return false;
            observed = cleanupAccess.Dialogs.Observe(cleanupContext,
                cleanupBegun.Value);
            return observed.Code == ApplicationServiceResultCode.InvalidContext &&
                ApplicationServiceSessionTable.TransientWindowCount == 0 &&
                ApplicationServiceSessionTable.OrphanTransientWindowCount == 0 &&
                stale.IsValid;
        }

        private static bool RunFileDialogServiceSelfTest(
                ApplicationServiceContext context,
                ApplicationServiceAccess access) {
            if (context == null || access == null ||
                    access.OpenFile == null || access.SaveFile == null) {
                return false;
            }
            OpenFileRequest openRequest = OpenFileRequest.Create("Programs/");
            OpenFileRequest invalidOpen = OpenFileRequest.Create(
                Repeat('p', OpenFileRequest.MaxStartingLocationLength + 1));
            SaveFileRequest saveRequest = SaveFileRequest.Create(
                "Programs/", "phase9.txt");
            SaveFileRequest invalidSave = SaveFileRequest.Create(
                "Programs/", Repeat('n',
                    SaveFileRequest.MaxSuggestedFileNameLength + 1));
            if (!openRequest.IsValid || invalidOpen.IsValid ||
                    !saveRequest.IsValid || invalidSave.IsValid) {
                return false;
            }

            ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                access.OpenFile.Begin(context, openRequest);
            if (!begun.Succeeded || !begun.Value.IsValid) {
                return false;
            }
            ApplicationServiceResult complete =
                CompleteFileDialogRequestForSelfTest(begun.Value,
                    ApplicationFileDialogOutcome.Selected,
                    "Programs/notepad.gxm");
            ApplicationServiceResult<ApplicationServiceRequestStatus<
                ApplicationFileDialogResult>> observed =
                access.OpenFile.Observe(context, begun.Value);
            if (!complete.Succeeded || !observed.Succeeded ||
                    observed.Value == null ||
                    observed.Value.State != ApplicationServiceRequestState.Completed ||
                    observed.Value.Value == null ||
                    observed.Value.Value.Outcome !=
                        ApplicationFileDialogOutcome.Selected ||
                    observed.Value.Value.SelectedPath !=
                        "Programs/notepad.gxm") {
                return false;
            }

            begun = access.OpenFile.Begin(context, openRequest);
            if (!begun.Succeeded) return false;
            ApplicationServiceResult cancelled = access.OpenFile.Cancel(
                context, begun.Value);
            observed = access.OpenFile.Observe(context, begun.Value);
            if (cancelled.Code != ApplicationServiceResultCode.Cancelled ||
                    !observed.Succeeded || observed.Value == null ||
                    observed.Value.State != ApplicationServiceRequestState.Cancelled ||
                    observed.Value.Value == null ||
                    observed.Value.Value.Outcome !=
                        ApplicationFileDialogOutcome.Cancelled ||
                    observed.Value.Value.SelectedPath.Length != 0) {
                return false;
            }

            begun = access.SaveFile.Begin(context, saveRequest);
            if (!begun.Succeeded) {
                return false;
            }
            complete = CompleteFileDialogRequestForSelfTest(begun.Value,
                ApplicationFileDialogOutcome.Selected, "Programs/phase9.txt");
            ApplicationServiceResult<ApplicationServiceRequestStatus<
                ApplicationFileDialogResult>> saveObserved =
                access.SaveFile.Observe(context, begun.Value);
            if (!complete.Succeeded || !saveObserved.Succeeded ||
                    saveObserved.Value == null || saveObserved.Value.Value == null ||
                    saveObserved.Value.Value.Outcome !=
                        ApplicationFileDialogOutcome.Selected ||
                    saveObserved.Value.Value.SelectedPath !=
                        "Programs/phase9.txt") {
                return false;
            }

            begun = access.SaveFile.Begin(context, saveRequest);
            if (!begun.Succeeded) return false;
            cancelled = access.SaveFile.Cancel(context, begun.Value);
            saveObserved = access.SaveFile.Observe(context, begun.Value);
            if (cancelled.Code != ApplicationServiceResultCode.Cancelled ||
                    !saveObserved.Succeeded || saveObserved.Value == null ||
                    saveObserved.Value.State != ApplicationServiceRequestState.Cancelled ||
                    saveObserved.Value.Value == null ||
                    saveObserved.Value.Value.Outcome !=
                        ApplicationFileDialogOutcome.Cancelled) {
                return false;
            }

            begun = access.SaveFile.Begin(context, saveRequest);
            if (!begun.Succeeded) return false;
            complete = CompleteFileDialogRequestForSelfTest(begun.Value,
                ApplicationFileDialogOutcome.BackendFailure, string.Empty);
            saveObserved = access.SaveFile.Observe(context, begun.Value);
            bool passed = complete.Succeeded && saveObserved.Succeeded &&
                saveObserved.Value != null &&
                saveObserved.Value.State == ApplicationServiceRequestState.Completed &&
                saveObserved.Value.Value != null &&
                saveObserved.Value.Value.Outcome ==
                    ApplicationFileDialogOutcome.BackendFailure &&
                ApplicationServiceSessionTable.TransientWindowCount == 0 &&
                ApplicationServiceSessionTable.OrphanTransientWindowCount == 0;
            return passed;
        }

        private static bool RunShellServiceSelfTest(
                ApplicationServiceContext context,
                ApplicationServiceAccess access) {
            if (context == null || access == null || access.Shell == null) {
                return false;
            }
            ApplicationShellOpenRequest request =
                ApplicationShellOpenRequest.ForApplicationId(
                    "gxos.builtin.calculator");
            ApplicationShellOpenRequest invalid =
                ApplicationShellOpenRequest.ForApplicationId(
                    Repeat('s', ApplicationShellOpenRequest.MaxTargetLength + 1));
            if (!request.IsValid || invalid.IsValid) return false;

            ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                access.Shell.Begin(context, request);
            if (!begun.Succeeded || !begun.Value.IsValid) return false;
            ApplicationServiceResult<ApplicationServiceRequestStatus<
                ApplicationShellResult>> observed =
                access.Shell.Observe(context, begun.Value);
            if (!observed.Succeeded || observed.Value == null ||
                    !observed.Value.IsTerminal || observed.Value.Value == null ||
                    observed.Value.Value.ResultCode !=
                        ApplicationServiceResultCode.Success) return false;
            if (observed.Value.Value.Succeeded &&
                    observed.Value.Value.InstanceHandle.IsValid &&
                    !ApplicationInstanceRegistry.TryTerminate(
                        observed.Value.Value.InstanceHandle,
                        "shell service self-test application cleanup")) {
                return false;
            }

            ApplicationShellOpenRequest alias =
                ApplicationShellOpenRequest.ForAlias("Calculator");
            begun = access.Shell.Begin(context, alias);
            if (!begun.Succeeded) return false;
            ApplicationServiceResult shellCancel = access.Shell.Cancel(
                context, begun.Value);
            observed = access.Shell.Observe(context, begun.Value);
            if (shellCancel.Code != ApplicationServiceResultCode.InvalidState ||
                    !observed.Succeeded || observed.Value == null ||
                    observed.Value.Value == null ||
                    observed.Value.Value.ResultCode !=
                        ApplicationServiceResultCode.Success) return false;
            if (observed.Value.Value.InstanceHandle.IsValid &&
                    !ApplicationInstanceRegistry.TryTerminate(
                        observed.Value.Value.InstanceHandle,
                        "shell service self-test alias cleanup")) {
                return false;
            }

            ApplicationShellOpenRequest unsupported =
                ApplicationShellOpenRequest.ForApplicationId(
                    "gxos.builtin.not-real");
            begun = access.Shell.Begin(context, unsupported);
            if (!begun.Succeeded) return false;
            observed = access.Shell.Observe(context, begun.Value);
            if (!observed.Succeeded || observed.Value == null ||
                    observed.Value.Value == null ||
                    observed.Value.Value.ResultCode !=
                        ApplicationServiceResultCode.NotFound) return false;

            ApplicationShellOpenRequest document =
                ApplicationShellOpenRequest.ForDocument("Programs/notepad.gxm");
            begun = access.Shell.Begin(context, document);
            if (!begun.Succeeded) return false;
            observed = access.Shell.Observe(context, begun.Value);
            if (!observed.Succeeded || observed.Value == null ||
                    observed.Value.Value == null ||
                    observed.Value.Value.ResultCode ==
                        ApplicationServiceResultCode.InvalidRequest) return false;
            if (observed.Value.Value.Succeeded &&
                    observed.Value.Value.InstanceHandle.IsValid &&
                    !ApplicationInstanceRegistry.TryTerminate(
                        observed.Value.Value.InstanceHandle,
                        "shell service self-test document cleanup")) {
                return false;
            }

            ApplicationShellOpenRequest shellObject =
                ApplicationShellOpenRequest.ForShellObject(
                    "gxos.shell.computerfiles");
            begun = access.Shell.Begin(context, shellObject);
            if (!begun.Succeeded) return false;
            observed = access.Shell.Observe(context, begun.Value);
            if (!observed.Succeeded || observed.Value == null ||
                    observed.Value.Value == null ||
                    observed.Value.Value.ResultCode !=
                        ApplicationServiceResultCode.Success) {
                return false;
            }
            if (observed.Value.Value.InstanceHandle.IsValid &&
                    !ApplicationInstanceRegistry.TryTerminate(
                        observed.Value.Value.InstanceHandle,
                        "shell service self-test object cleanup")) {
                return false;
            }

            ApplicationShellOpenRequest unknownObject =
                ApplicationShellOpenRequest.ForShellObject("shell.not-real");
            begun = access.Shell.Begin(context, unknownObject);
            if (!begun.Succeeded) return false;
            observed = access.Shell.Observe(context, begun.Value);
            if (!observed.Succeeded || observed.Value == null ||
                    observed.Value.Value == null ||
                    observed.Value.Value.ResultCode !=
                        ApplicationServiceResultCode.NotFound) return false;

            return true;
        }

        private sealed class ServiceWindowProbe : Window {
            internal ServiceWindowProbe() : base(40, 112, 160, 120) {
                ShowInTaskbar = false;
            }
            public override void OnDraw() { }
            public override void OnInput() { }
        }

        internal static bool TryRegisterForSelfTest(ApplicationServiceId id) {
            Initialize();
            return TryRegister(id);
        }

        internal static bool TryGetAccessForSelfTest(ApplicationServiceId id) {
            Initialize();
            ServiceEntry entry;
            return TryGetEntry(id, out entry);
        }

        internal static void SetTypedAccess(ApplicationServiceAccess access) {
            Initialize();
            _access = access ?? new ApplicationServiceAccess(null, null, null,
                null, null, null, null, null, null, null);
        }

        private static void RegisterInitial(ApplicationServiceId id) {
            TryRegister(id);
        }

        private static bool TryRegister(ApplicationServiceId id) {
            if (!ApplicationServiceNames.IsKnown(id)) return false;
            ServiceEntry existing;
            if (TryGetEntry(id, out existing)) {
                _duplicateRegistrationRejected = true;
                return false;
            }
            for (int i = 0; i < Capacity; i++) {
                if (_entries[i] != null && _entries[i].Registered) continue;
                _entries[i] = new ServiceEntry { Id = id, Registered = true };
                _registeredCount++;
                return true;
            }
            return false;
        }

        private static bool TryGetEntry(ApplicationServiceId id,
                                        out ServiceEntry entry) {
            entry = null;
            if (_entries == null) return false;
            for (int i = 0; i < Capacity; i++) {
                ServiceEntry candidate = _entries[i];
                if (candidate != null && candidate.Registered &&
                        candidate.Id == id) {
                    entry = candidate;
                    return true;
                }
            }
            return false;
        }

        private static bool IsRegistered(ApplicationServiceId id) {
            ServiceEntry entry;
            return TryGetEntry(id, out entry);
        }

        private static bool TryResolveHandle(
                ApplicationInstanceHandle handle,
                out ApplicationInstance instance,
                out ApplicationServiceResult result) {
            instance = null;
            result = ApplicationServiceResult.InvalidContextResult();
            if (!ApplicationInstanceRegistry.TryGet(handle, out instance) ||
                    instance == null) {
                _staleContextRejections++;
                return false;
            }
            if (IsCommonlyRejectedState(instance.LifecycleState)) {
                _invalidContextRejections++;
                return false;
            }
            result = ApplicationServiceResult.SuccessResult();
            return true;
        }

        private static bool TryValidateCommonContext(
                ApplicationServiceContext context,
                out ApplicationInstance instance,
                out ApplicationServiceResult result) {
            instance = null;
            result = ApplicationServiceResult.InvalidContextResult();
            if (context == null || !context.IsValid) {
                _invalidContextRejections++;
                return false;
            }
            if (!ApplicationInstanceRegistry.TryGet(context.InstanceHandle,
                    out instance) || instance == null) {
                _staleContextRejections++;
                return false;
            }
            if (!TextEquals(instance.DescriptorId, context.ApplicationId)) {
                _invalidContextRejections++;
                return false;
            }
            if (IsCommonlyRejectedState(instance.LifecycleState)) {
                _invalidContextRejections++;
                return false;
            }
            result = ApplicationServiceResult.SuccessResult();
            return true;
        }

        private static bool IsCommonlyRejectedState(
                ApplicationInstanceLifecycleState state) {
            return state == ApplicationInstanceLifecycleState.Suspended ||
                   state == ApplicationInstanceLifecycleState.Closing ||
                   state == ApplicationInstanceLifecycleState.Terminated ||
                   state == ApplicationInstanceLifecycleState.Failed;
        }

        private static bool IsServiceStateAllowed(
                ApplicationServiceId serviceId,
                ApplicationInstanceLifecycleState state) {
            if (serviceId == ApplicationServiceId.SystemInformation) {
                return state == ApplicationInstanceLifecycleState.Loading ||
                       state == ApplicationInstanceLifecycleState.Initialized ||
                       state == ApplicationInstanceLifecycleState.Running ||
                       state == ApplicationInstanceLifecycleState.Activated ||
                       state == ApplicationInstanceLifecycleState.Inactive;
            }
            if (serviceId == ApplicationServiceId.Notifications ||
                    serviceId == ApplicationServiceId.Settings) {
                return state == ApplicationInstanceLifecycleState.Initialized ||
                       state == ApplicationInstanceLifecycleState.Running ||
                       state == ApplicationInstanceLifecycleState.Activated ||
                       state == ApplicationInstanceLifecycleState.Inactive;
            }
            if (serviceId == ApplicationServiceId.Dialogs) {
                return state == ApplicationInstanceLifecycleState.Running ||
                       state == ApplicationInstanceLifecycleState.Activated ||
                       state == ApplicationInstanceLifecycleState.Inactive;
            }
            if (serviceId == ApplicationServiceId.OpenFile ||
                    serviceId == ApplicationServiceId.SaveFile) {
                return state == ApplicationInstanceLifecycleState.Running ||
                       state == ApplicationInstanceLifecycleState.Activated ||
                       state == ApplicationInstanceLifecycleState.Inactive;
            }
            if (serviceId == ApplicationServiceId.Shell) {
                return state == ApplicationInstanceLifecycleState.Running ||
                       state == ApplicationInstanceLifecycleState.Activated ||
                       state == ApplicationInstanceLifecycleState.Inactive;
            }
            if (serviceId == ApplicationServiceId.Resources ||
                    serviceId == ApplicationServiceId.Storage ||
                    serviceId == ApplicationServiceId.Clipboard) {
                return state == ApplicationInstanceLifecycleState.Initialized ||
                       state == ApplicationInstanceLifecycleState.Running ||
                       state == ApplicationInstanceLifecycleState.Activated ||
                       state == ApplicationInstanceLifecycleState.Inactive;
            }
            return false;
        }

        private static bool IsBoundedApplicationId(string value) {
            return value != null && value.Length > 0 &&
                   value.Length <= ApplicationServiceContext.MaxApplicationIdLength;
        }

        private static bool RunContractSelfTest() {
            int passed = 0;
            int failed = 0;
            string failure = null;

            Check(ApplicationNotificationRequest.TryCreate(
                    "title", "body",
                    ApplicationNotificationSeverity.Info, out _),
                "notification request shape", ref passed, ref failed,
                ref failure);
            Check(!ApplicationNotificationRequest.TryCreate(
                    "title",
                    Repeat('x', ApplicationNotificationRequest.MaxBodyLength + 1),
                    ApplicationNotificationSeverity.Info, out _),
                "notification body bound", ref passed, ref failed,
                ref failure);
            Check(ApplicationSettingValue.Boolean(true).IsValid &&
                  ApplicationSettingValue.Boolean(true).BooleanValue &&
                  ApplicationSettingValue.Int32(4).IsValid &&
                  ApplicationSettingValue.String("session").IsValid,
                "setting value kinds", ref passed, ref failed, ref failure);
            Check(!ApplicationSettingValue.String(
                    Repeat('x', ApplicationSettingValue.MaxStringLength + 1)).IsValid,
                "setting string bound", ref passed, ref failed, ref failure);
            Check(SystemInformationSnapshot.IsBoundedText(
                    "guideXOS", SystemInformationSnapshot.MaxOsNameLength),
                "system snapshot text bound", ref passed, ref failed,
                ref failure);
            SystemInformationSnapshot snapshot = new SystemInformationSnapshot(
                1, 100, 50, 2, 25, "guideXOS", "Phase8", "x86_64");
            Check(snapshot.IsWithinBounds(), "system snapshot shape",
                ref passed, ref failed, ref failure);
            return failed == 0;
        }

        private static bool RunPhase9ContractSelfTest() {
            int passed = 0;
            int failed = 0;
            string failure = null;

            Check(ApplicationServiceNames.IsKnown(ApplicationServiceId.Dialogs) &&
                  ApplicationServiceNames.IsKnown(ApplicationServiceId.OpenFile) &&
                  ApplicationServiceNames.IsKnown(ApplicationServiceId.SaveFile) &&
                  ApplicationServiceNames.IsKnown(ApplicationServiceId.Shell),
                "phase 9 service ids", ref passed, ref failed, ref failure);
            Check(ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.Conflict, "duplicate").Code ==
                  ApplicationServiceResultCode.Conflict &&
                  ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.Cancelled, "cancel").Code ==
                  ApplicationServiceResultCode.Cancelled,
                "phase 9 result vocabulary", ref passed, ref failed,
                ref failure);

            ApplicationDialogRequest info = ApplicationDialogRequest.Create(
                ApplicationDialogKind.Information, "Title", "Body",
                ApplicationDialogButtonSet.Acknowledge);
            Check(info.IsValid, "information dialog shape", ref passed,
                ref failed, ref failure);
            ApplicationDialogRequest confirmation =
                ApplicationDialogRequest.Create(
                    ApplicationDialogKind.Confirmation, "Confirm", "Body",
                    ApplicationDialogButtonSet.AcceptRejectCancel);
            Check(confirmation.IsValid, "confirmation dialog shape",
                ref passed, ref failed, ref failure);
            ApplicationDialogRequest invalidDialog =
                ApplicationDialogRequest.Create(
                    ApplicationDialogKind.Error,
                    Repeat('t', ApplicationDialogRequest.MaxTitleLength + 1),
                    "Body", ApplicationDialogButtonSet.Acknowledge);
            Check(!invalidDialog.IsValid, "dialog title bound", ref passed,
                ref failed, ref failure);

            OpenFileRequest open = OpenFileRequest.Create(
                Repeat('p', OpenFileRequest.MaxStartingLocationLength));
            Check(open.IsValid, "open path maximum", ref passed, ref failed,
                ref failure);
            Check(!OpenFileRequest.Create(Repeat('p',
                    OpenFileRequest.MaxStartingLocationLength + 1)).IsValid,
                "open path bound", ref passed, ref failed, ref failure);

            SaveFileRequest save = SaveFileRequest.Create(
                Repeat('p', SaveFileRequest.MaxStartingLocationLength),
                Repeat('n', SaveFileRequest.MaxSuggestedFileNameLength));
            Check(save.IsValid, "save request maximum", ref passed, ref failed,
                ref failure);
            Check(!SaveFileRequest.Create("start",
                    Repeat('n', SaveFileRequest.MaxSuggestedFileNameLength + 1)).IsValid,
                "save filename bound", ref passed, ref failed, ref failure);

            ApplicationShellOpenRequest app =
                ApplicationShellOpenRequest.ForApplicationId("gxos.test.app");
            ApplicationShellOpenRequest document =
                ApplicationShellOpenRequest.ForDocument("disk:/readme.txt");
            Check(app.IsValid && document.IsValid,
                "shell target shapes", ref passed, ref failed, ref failure);
            Check(!ApplicationShellOpenRequest.ForApplicationId(
                    Repeat('a', ApplicationShellOpenRequest.MaxTargetLength + 1)).IsValid,
                "shell target bound", ref passed, ref failed, ref failure);

            Check(!ApplicationServiceRequestHandle.Invalid.IsValid &&
                  ApplicationServiceRequestState.Pending !=
                  ApplicationServiceRequestState.Completed,
                "request handle/status shape", ref passed, ref failed,
                ref failure);
            return failed == 0;
        }

        private static bool RunPhase10ContractSelfTest() {
            int passed = 0;
            int failed = 0;
            string failure = null;

            Check(ApplicationServiceNames.IsKnown(ApplicationServiceId.Resources) &&
                  ApplicationServiceNames.IsKnown(ApplicationServiceId.Storage),
                "phase 10 service ids", ref passed, ref failed, ref failure);
            ApplicationResourceRequest resource =
                ApplicationResourceRequest.Create(
                    Repeat('r', ApplicationResourceRequest.MaxResourceKeyLength));
            Check(resource.IsValid, "resource key maximum", ref passed,
                ref failed, ref failure);
            Check(!ApplicationResourceRequest.Create(Repeat('r',
                    ApplicationResourceRequest.MaxResourceKeyLength + 1)).IsValid,
                "resource key bound", ref passed, ref failed, ref failure);
            Check(!ApplicationResourceRequest.Create(".").IsValid &&
                  !ApplicationResourceRequest.Create("..").IsValid,
                "resource key traversal shape", ref passed, ref failed,
                ref failure);
            ApplicationResourceReadRequest read =
                ApplicationResourceReadRequest.Create("fixture", 0,
                    ApplicationResourceReadRequest.MaxChunkLength);
            Check(read.IsValid, "resource read maximum", ref passed,
                ref failed, ref failure);
            Check(!ApplicationResourceReadRequest.Create("fixture", -1, 1).IsValid &&
                  !ApplicationResourceReadRequest.Create("fixture", 0, 0).IsValid &&
                  !ApplicationResourceReadRequest.Create("fixture", 0,
                      ApplicationResourceReadRequest.MaxChunkLength + 1).IsValid,
                "resource read bounds", ref passed, ref failed, ref failure);
            string maximumTemporaryPath = Repeat('p',
                    ApplicationStorageRequest.MaxPathSegmentLength) + "/" +
                Repeat('q', ApplicationStorageRequest.MaxPathSegmentLength) +"/"+
                Repeat('r', ApplicationStorageRequest.MaxRelativePathLength -
                    (ApplicationStorageRequest.MaxPathSegmentLength * 2) - 2);
            ApplicationStorageRequest temporary = ApplicationStorageRequest.Create(
                ApplicationStorageNamespace.Temporary,
                maximumTemporaryPath);
            Check(temporary.IsValid, "storage path maximum", ref passed,
                ref failed, ref failure);
            Check(!ApplicationStorageRequest.Create(
                    ApplicationStorageNamespace.Persistent, "../escape").IsValid &&
                  !ApplicationStorageRequest.Create(
                    ApplicationStorageNamespace.Persistent, "C:\\escape").IsValid,
                "storage path confinement shape", ref passed, ref failed,
                ref failure);
            ApplicationStorageWriteRequest write =
                ApplicationStorageWriteRequest.Create(
                    ApplicationStorageNamespace.Temporary, "state.bin",
                    new byte[ApplicationStorageWriteRequest.MaxPayloadLength]);
            Check(write.IsValid, "storage payload maximum", ref passed,
                ref failed, ref failure);
            Check(!ApplicationStorageWriteRequest.Create(
                    ApplicationStorageNamespace.Temporary, "state.bin",
                    new byte[ApplicationStorageWriteRequest.MaxPayloadLength + 1]).IsValid,
                "storage payload bound", ref passed, ref failed, ref failure);
            return failed == 0;
        }

        private static bool RunPhase11ContractSelfTest() {
            int passed = 0;
            int failed = 0;
            string failure = null;

            Check(ApplicationServiceNames.IsKnown(ApplicationServiceId.Clipboard) &&
                  ApplicationServiceNames.For(ApplicationServiceId.Clipboard) ==
                      "clipboard",
                "phase 11 clipboard service id", ref passed, ref failed,
                ref failure);
            ApplicationClipboardWriteRequest empty =
                ApplicationClipboardWriteRequest.Create(string.Empty);
            ApplicationClipboardWriteRequest maximum =
                ApplicationClipboardWriteRequest.Create(
                    Repeat('c', ApplicationClipboardWriteRequest.MaxTextLength));
            Check(empty.IsValid && maximum.IsValid && empty.Text.Length == 0 &&
                  maximum.Text.Length == ApplicationClipboardWriteRequest.MaxTextLength,
                "clipboard text maximum and empty value", ref passed,
                ref failed, ref failure);
            Check(!ApplicationClipboardWriteRequest.Create(null).IsValid &&
                  !ApplicationClipboardWriteRequest.Create(
                      Repeat('c', ApplicationClipboardWriteRequest.MaxTextLength + 1)).IsValid,
                "clipboard text rejection", ref passed, ref failed,
                ref failure);
            ApplicationClipboardSnapshot absent =
                ApplicationClipboardSnapshot.Create(false, string.Empty,
                    string.Empty, 0UL);
            Check(!absent.HasValue && absent.Text.Length == 0 &&
                  absent.SourceAppId.Length == 0 && absent.Generation == 0UL,
                "clipboard absent snapshot shape", ref passed, ref failed,
                ref failure);
            ApplicationClipboardSnapshot presentEmpty =
                ApplicationClipboardSnapshot.Create(true, string.Empty,
                    "selftest.clipboard", 1UL);
            Check(presentEmpty.HasValue && presentEmpty.Text.Length == 0 &&
                  presentEmpty.SourceAppId == "selftest.clipboard" &&
                  presentEmpty.Generation == 1UL,
                "clipboard present empty snapshot shape", ref passed,
                ref failed, ref failure);
            return failed == 0;
        }

        private static bool RunSettingsSelfTest() {
            ApplicationInstance first = null;
            ApplicationInstance second = null;
            ApplicationInstance other = null;
            ApplicationServiceContext firstContext = null;
            ApplicationServiceContext secondContext = null;
            ApplicationServiceContext otherContext = null;
            ApplicationServiceAccess firstAccess = null;
            ApplicationServiceResult result;
            bool firstReused;
            bool secondReused;
            bool otherReused;
            LaunchResult firstFailure;
            LaunchResult secondFailure;
            LaunchResult otherFailure;
            bool passed = true;
            string failure = "none";

            bool firstStarted = ApplicationInstanceRegistry.TryBeginLaunch(
                "selftest.settings.notepad", ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId("selftest.settings.notepad", null, null,
                    LaunchActivationIntent.NewInstance), out first, out firstReused,
                out firstFailure);
            bool secondStarted = ApplicationInstanceRegistry.TryBeginLaunch(
                "selftest.settings.notepad", ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId("selftest.settings.notepad", null, null,
                    LaunchActivationIntent.NewInstance), out second, out secondReused,
                out secondFailure);
            bool otherStarted = ApplicationInstanceRegistry.TryBeginLaunch(
                "selftest.settings.calculator", ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId("selftest.settings.calculator", null, null,
                    LaunchActivationIntent.NewInstance), out other, out otherReused,
                out otherFailure);

            bool completed = firstStarted && secondStarted && otherStarted &&
                ApplicationInstanceRegistry.TryCompleteLaunch(first, false,
                    out firstFailure) &&
                ApplicationInstanceRegistry.TryCompleteLaunch(second, false,
                    out secondFailure) &&
                ApplicationInstanceRegistry.TryCompleteLaunch(other, false,
                    out otherFailure);
            bool contexts = completed &&
                TryCreateContextAndAccess(first.Handle, out firstContext,
                    out firstAccess, out result) &&
                TryCreateContext(second.Handle, out secondContext, out result) &&
                TryCreateContext(other.Handle, out otherContext, out result);

            if (!firstStarted) failure = "first-launch";
            else if (!secondStarted) failure = "second-launch";
            else if (!otherStarted) failure = "other-launch";
            else if (!completed) failure = "completion";
            else if (!contexts) failure = "context";

            if (firstAccess == null || firstAccess.Settings == null) {
                passed = false;
                if (failure == "none") failure = "settings-access";
            } else if (contexts) {
                ApplicationServiceResult set = firstAccess.Settings.Set(
                    firstContext, "wrap", ApplicationSettingValue.Boolean(true));
                ApplicationServiceResult<ApplicationSettingValue> shared =
                    firstAccess.Settings.Get(secondContext, "wrap");
                ApplicationServiceResult<ApplicationSettingValue> isolated =
                    firstAccess.Settings.Get(otherContext, "wrap");
                ApplicationServiceResult<ApplicationSettingValue> missing =
                    firstAccess.Settings.Get(firstContext, "missing");
                ApplicationServiceResult invalidKey = firstAccess.Settings.Set(
                    firstContext,
                    Repeat('k', ApplicationSettingsService.MaxKeyLength + 1),
                    ApplicationSettingValue.Int32(1));
                ApplicationServiceResult invalidValue = firstAccess.Settings.Set(
                    firstContext, "invalid", ApplicationSettingValue.String(
                        Repeat('v', ApplicationSettingValue.MaxStringLength + 1)));
                passed = set.Succeeded && shared.Succeeded &&
                    shared.Value.BooleanValue &&
                    isolated.Code == ApplicationServiceResultCode.NotFound &&
                    missing.Code == ApplicationServiceResultCode.NotFound &&
                    invalidKey.Code == ApplicationServiceResultCode.InvalidRequest &&
                    invalidValue.Code == ApplicationServiceResultCode.InvalidRequest;
                if (!passed && failure == "none") {
                    if (!set.Succeeded) failure = "set";
                    else if (!shared.Succeeded) failure = "shared-code";
                    else if (shared.Value.Kind != ApplicationSettingValueKind.Boolean) failure = "shared-kind";
                    else if (!shared.Value.BooleanValue) failure = "shared-value";
                    else if (isolated.Code != ApplicationServiceResultCode.NotFound) failure = "isolated-code";
                    else if (missing.Code != ApplicationServiceResultCode.NotFound) failure = "missing-code";
                    else if (invalidKey.Code != ApplicationServiceResultCode.InvalidRequest) failure = "key-bound";
                    else failure = "value-bound";
                }
            } else {
                passed = false;
            }

            if (first != null) ApplicationInstanceRegistry.TryTerminate(first,
                "settings service self-test cleanup");
            if (second != null) ApplicationInstanceRegistry.TryTerminate(second,
                "settings service self-test cleanup");
            if (other != null) ApplicationInstanceRegistry.TryTerminate(other,
                "settings service self-test cleanup");
            _lastSettingsSelfTestFailure = passed ? "pass" : failure;
            return passed;
        }

        private static bool RunRequestSessionSelfTest() {
            ApplicationInstance instance = null;
            ApplicationServiceContext context = null;
            ApplicationServiceRequestHandle firstHandle =
                ApplicationServiceRequestHandle.Invalid;
            ApplicationServiceRequestHandle secondHandle =
                ApplicationServiceRequestHandle.Invalid;
            ApplicationServiceRequestStatus<object> status;
            ApplicationServiceResult result;
            bool reused;
            LaunchResult failure;
            string id = "selftest.phase9.sessions";
            bool started = ApplicationInstanceRegistry.TryBeginLaunch(
                id, ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId(id, null, null,
                    LaunchActivationIntent.NewInstance), out instance, out reused,
                out failure);
            bool completed = started && instance != null &&
                ApplicationInstanceRegistry.TryCompleteLaunch(instance, false,
                    out failure) &&
                TryCreateContext(instance.Handle, out context, out result);
            bool passed = completed;
            if (passed) {
                result = BeginInteractiveRequest(context,
                    ApplicationServiceId.Dialogs, "first", out firstHandle);
                passed = result.Succeeded && firstHandle.IsValid;
                result = BeginInteractiveRequest(context,
                    ApplicationServiceId.Dialogs, "duplicate", out secondHandle);
                passed = passed &&
                    result.Code == ApplicationServiceResultCode.Conflict;
                result = TryObserveRequest(context, firstHandle, out status);
                passed = passed && result.Succeeded && status != null &&
                    status.State == ApplicationServiceRequestState.Pending;

                bool inactive = instance.TryTransition(
                    ApplicationInstanceLifecycleState.Inactive);
                result = TryObserveRequest(context, firstHandle, out status);
                ApplicationServiceRequestHandle inactiveHandle;
                ApplicationServiceResult inactiveCreate =
                    BeginInteractiveRequest(context, ApplicationServiceId.Dialogs,
                        "inactive", out inactiveHandle);
                passed = passed && inactive && result.Succeeded &&
                    inactiveCreate.Code == ApplicationServiceResultCode.InvalidState;

                bool running = instance.TryTransition(
                    ApplicationInstanceLifecycleState.Running);
                passed = passed && running;
                result = CompleteRequestForSelfTest(firstHandle, "completed");
                result = TryObserveRequest(context, firstHandle, out status);
                passed = passed && result.Succeeded && status != null &&
                    status.State == ApplicationServiceRequestState.Completed &&
                    (string)status.Value == "completed";

                result = TryCancelRequest(context, firstHandle);
                passed = passed && result.Code ==
                    ApplicationServiceResultCode.Cancelled;
            }

            ApplicationInstanceHandle staleHandle =
                instance == null ? ApplicationInstanceHandle.None : instance.Handle;
            if (instance != null) {
                ApplicationInstanceRegistry.TryTerminate(instance,
                    "request session self-test cleanup");
            }
            result = TryObserveRequest(context, firstHandle, out status);
            passed = passed && result.Code ==
                ApplicationServiceResultCode.InvalidContext;
            return passed && staleHandle.IsValid;
        }

        private static bool RunSystemInformationSelfTest(
                ApplicationServiceContext context,
                ApplicationServiceAccess access) {
            if (context == null || access == null ||
                    access.SystemInformation == null) return false;
            ApplicationServiceResult<SystemInformationSnapshot> snapshot =
                access.SystemInformation.GetSnapshot(context);
            if (!snapshot.Succeeded || !snapshot.Value.IsWithinBounds()) {
                return false;
            }
            SystemInformationSnapshot copy = snapshot.Value;
            return copy.MemorySizeBytes >= copy.MemoryInUseBytes &&
                   copy.OsName.Length <= SystemInformationSnapshot.MaxOsNameLength &&
                   copy.OsVersion.Length <= SystemInformationSnapshot.MaxOsVersionLength &&
                   copy.Architecture.Length <= SystemInformationSnapshot.MaxArchitectureLength;
        }

        private static string Repeat(char value, int count) {
            char[] chars = new char[count];
            for (int i = 0; i < chars.Length; i++) chars[i] = value;
            return new string(chars);
        }

        private static void Check(bool condition, string name, ref int passed,
                                  ref int failed, ref string failure) {
            if (condition) {
                passed++;
                return;
            }
            failed++;
            if (failure == null) failure = name;
        }

        private static bool TextEquals(string a, string b) {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) {
                char ca = a[i];
                char cb = b[i];
                if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32);
                if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32);
                if (ca != cb) return false;
            }
            return true;
        }
    }
}
