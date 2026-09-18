namespace guideXOS.OS {
    /// <summary>
    /// Fixed application-platform service table.  ApplicationInstanceRegistry
    /// remains the only source of truth for instance existence and lifecycle;
    /// this class only validates access and projects typed adapters.
    /// </summary>
    public static class ApplicationServiceRegistry {
        public const int Capacity = 8;
        public const int SelectedServiceCount = 3;

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

        public static void Initialize() {
            if (_initialized) return;

            _entries = new ServiceEntry[Capacity];
            _registeredCount = 0;
            _duplicateRegistrationRejected = false;
            _staleContextRejections = 0;
            _invalidContextRejections = 0;
            _lastSettingsSelfTestFailure = "not-run";
            _access = new ApplicationServiceAccess(
                new CSharpApplicationNotificationService(),
                new CSharpApplicationSettingsService(),
                new CSharpApplicationSystemInformationService());
            _initialized = true;

            RegisterInitial(ApplicationServiceId.Notifications);
            RegisterInitial(ApplicationServiceId.Settings);
            RegisterInitial(ApplicationServiceId.SystemInformation);
        }

        /// <summary>
        /// Reset only service-table state and bounded service diagnostics.
        /// This never clears or reassigns ApplicationInstanceRegistry slots.
        /// </summary>
        public static void ResetForDiagnostics() {
            _entries = null;
            _access = null;
            _registeredCount = 0;
            _initialized = false;
            Initialize();
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
                       _access.SystemInformation != null;
            }
        }

        public static string LastSettingsSelfTestFailure {
            get { return _lastSettingsSelfTestFailure; }
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

        /// <summary>
        /// Deterministic registry/lifecycle proof.  The diagnostic uses the
        /// existing ApplicationInstanceRegistry launch and termination paths.
        /// </summary>
        public static bool RunSelfTest() {
            Initialize();
            int passed = 0;
            int failed = 0;
            string firstFailure = null;

            Check(RunContractSelfTest(), "contract bounds", ref passed,
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
            return failed == 0 && clean;
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
            _access = access ?? new ApplicationServiceAccess(null, null, null);
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
