using guideXOS.DefaultApps;
using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Drawing;

namespace guideXOS.OS {
    /// <summary>
    /// Managed construction boundary for an application implementation.  The
    /// App Model supplies identity, instance state, and launch context; the
    /// factory returns bounded window ownership candidates.  Shell controls,
    /// framebuffer state, and UEFI globals are intentionally not part of this
    /// contract.
    /// </summary>
    public abstract class ApplicationFactory {
        public abstract ApplicationClass SupportedApplicationClass { get; }

        protected bool TryCreateApplicationServices(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                out ApplicationServiceContext context,
                out ApplicationServiceAccess services,
                out ApplicationFactoryResult failure) {
            context = null;
            services = null;
            failure = null;
            ApplicationServiceResult serviceResult = null;
            if (instance == null ||
                    !ApplicationServiceRegistry.TryCreateContextAndAccess(
                        instance.Handle, out context, out services,
                        out serviceResult) || services == null ||
                    services.Notifications == null) {
                failure = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    serviceResult == null ?
                        "Application services are unavailable" :
                        serviceResult.BoundedDiagnostic,
                    descriptor == null ? null : descriptor.AppId);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Optional bounded lifecycle behavior for a new application
        /// instance.  The base adapter is cooperative and safe by default.
        /// </summary>
        public virtual ApplicationLifecycleAdapter CreateLifecycleAdapter() {
            // Null selects the allocation-free cooperative default.  Factories
            // that need custom lifecycle behavior return an adapter explicitly.
            return null;
        }

        public abstract bool TryCreateOrActivate(
            ApplicationDescriptor descriptor,
            ApplicationInstance instance,
            LaunchRequest request,
            out ApplicationFactoryResult result);
    }

    /// <summary>
    /// Bounded result from an application factory.  LaunchResult remains the
    /// common result vocabulary; the factory result adds only the bounded set
    /// of window objects that the App Model must attach and activate.
    /// </summary>
    public sealed class ApplicationFactoryResult {
        private readonly Window[] _windows;
        private readonly bool[] _preexistingOwnership;
        private readonly ApplicationInstance _instance;
        private readonly LaunchResult _launchResult;
        private int _windowCount;

        private ApplicationFactoryResult(ApplicationInstance instance,
                                         LaunchResult launchResult) {
            _instance = instance;
            _launchResult = launchResult;
            _windows = new Window[ApplicationInstance.MaxOwnedWindows];
            _preexistingOwnership =
                new bool[ApplicationInstance.MaxOwnedWindows];
        }

        public bool Success {
            get { return _launchResult != null && _launchResult.Success; }
        }

        public LaunchResult LaunchResult { get { return _launchResult; } }

        public LaunchActivationState ActivationState {
            get {
                return _launchResult == null ? LaunchActivationState.Failed :
                    _launchResult.ActivationState;
            }
        }

        public LaunchErrorCode ErrorCode {
            get {
                return _launchResult == null ?
                    LaunchErrorCode.InitializationFailed : _launchResult.ErrorCode;
            }
        }

        public string BoundedDiagnostic {
            get { return _launchResult == null ? null : _launchResult.BoundedDiagnostic; }
        }

        public int AttachedWindowCount { get { return _windowCount; } }

        public static ApplicationFactoryResult Succeeded(
                ApplicationInstance instance) {
            if (instance == null) {
                return Failed(LaunchErrorCode.InitializationFailed,
                    "Application instance is unavailable", null);
            }
            return new ApplicationFactoryResult(instance,
                LaunchResult.Succeeded(instance.DescriptorId, instance.Handle,
                    LaunchActivationState.Created));
        }

        public static ApplicationFactoryResult Failed(
                LaunchErrorCode errorCode, string diagnostic, string appId) {
            return new ApplicationFactoryResult(null,
                LaunchResult.Failed(errorCode, diagnostic, appId));
        }

        /// <summary>
        /// Add one window candidate.  The method is internal so a public
        /// factory cannot bypass the bounded WindowManager/instance ownership
        /// protocol with arbitrary collections.
        /// </summary>
        internal bool AddWindow(Window window) {
            if (!Success || window == null) return false;
            for (int i = 0; i < _windowCount; i++) {
                if (_windows[i] == window) return true;
            }
            if (_windowCount >= _windows.Length) return false;
            _windows[_windowCount] = window;
            _preexistingOwnership[_windowCount] =
                _instance != null && _instance.OwnsWindow(window);
            _windowCount++;
            return true;
        }

        internal Window GetWindowAt(int index) {
            return index >= 0 && index < _windowCount ? _windows[index] : null;
        }

        internal bool WasAlreadyOwnedAt(int index) {
            return index >= 0 && index < _windowCount &&
                _preexistingOwnership[index];
        }
    }

    /// <summary>
    /// Deterministic bounded descriptor-to-factory binding table.  The table is
    /// a backend relationship of the existing descriptor registry, not a
    /// second application identity registry.
    /// </summary>
    public static class ApplicationFactoryRegistry {
        public const int Capacity = 32;

        private sealed class Binding {
            internal string AppId;
            internal ApplicationFactory Factory;
        }

        private static Binding[] _bindings;
        private static int _count;
        private static bool _initialized;
        private static string _initializationFailure;

        private static int _factoryLaunches;
        private static int _compatibilityFallbackLaunches;
        private static int _factoryFailures;
        private static int _reusedFactoryActivations;
        private static int _factoryWindowsAttached;
        private static int _typedExternalLaunches;
        private static int _typedShellActionLaunches;

        public static void Initialize() {
            if (_initialized) return;
            _bindings = new Binding[Capacity];
            _initialized = true;

            // Registration order is part of the deterministic diagnostic
            // surface and follows the canonical twelve-entry descriptor list.
            RegisterInitial("gxos.builtin.calculator",
                new CalculatorApplicationFactory());
            RegisterInitial("gxos.builtin.files",
                new ComputerFilesApplicationFactory());
            RegisterInitial("gxos.builtin.notepad",
                new NotepadApplicationFactory());
            RegisterInitial("gxos.builtin.console",
                new ConsoleApplicationFactory());
            RegisterInitial("gxos.builtin.devices",
                new DevicesApplicationFactory());
            RegisterInitial("gxos.builtin.diskmanager",
                new DiskManagerApplicationFactory());
            RegisterInitial("gxos.builtin.displayoptions",
                new DisplayOptionsApplicationFactory());
            RegisterInitial("gxos.builtin.firewall",
                new FirewallApplicationFactory());
            RegisterInitial("gxos.builtin.paint",
                new PaintApplicationFactory());
            RegisterInitial("gxos.builtin.taskmanager",
                new TaskManagerApplicationFactory());
            RegisterInitial("gxos.builtin.imageviewer",
                new ImageViewerApplicationFactory());
            RegisterInitial("gxos.builtin.wavplayer",
                new WavPlayerApplicationFactory());
        }

        public static int Count { get { Initialize(); return _count; } }
        public static int FactoryRegistrations { get { return Count; } }
        public static int FactoryLaunches { get { return _factoryLaunches; } }
        public static int CompatibilityFallbackLaunches {
            get { return _compatibilityFallbackLaunches; }
        }
        public static int FactoryFailures { get { return _factoryFailures; } }
        public static int ReusedFactoryActivations {
            get { return _reusedFactoryActivations; }
        }
        public static int FactoryWindowsAttached {
            get { return _factoryWindowsAttached; }
        }
        public static int TypedExternalLaunches {
            get { return _typedExternalLaunches; }
        }
        public static int TypedShellActionLaunches {
            get { return _typedShellActionLaunches; }
        }
        public static string InitializationFailure {
            get { Initialize(); return _initializationFailure; }
        }

        /// <summary>
        /// Validate the normal built-in dispatch boundary.  GXM and shell
        /// actions are intentionally outside this table and are reported by
        /// their own typed backend counters.
        /// </summary>
        public static bool ValidateBuiltInBindings(out LaunchResult failure) {
            Initialize();
            return ValidateBuiltInBindingsCore(_bindings, _count, out failure);
        }

        private static bool ValidateBuiltInBindingsCore(Binding[] bindings,
                                                         int count,
                                                         out LaunchResult failure) {
            failure = null;
            if (!ApplicationDescriptorRegistry.IsValid) {
                failure = LaunchResult.Failed(LaunchErrorCode.BackendUnavailable,
                    ApplicationDescriptorRegistry.ValidationFailure ??
                        "Application descriptor validation failed", null);
                return false;
            }
            for (int i = 0; i < ApplicationDescriptorRegistry.Count; i++) {
                ApplicationDescriptor descriptor =
                    ApplicationDescriptorRegistry.GetAt(i);
                if (descriptor == null ||
                        descriptor.ApplicationClass != ApplicationClass.BuiltIn)
                    continue;
                ApplicationFactory factory;
                if (!TryGetFrom(bindings, count, descriptor.AppId,
                        out factory)) {
                    failure = LaunchResult.Failed(
                        LaunchErrorCode.BackendUnavailable,
                        "Registered built-in descriptor has no factory binding",
                        descriptor.AppId);
                    return false;
                }
                if (factory.SupportedApplicationClass !=
                        descriptor.ApplicationClass) {
                    failure = LaunchResult.Failed(
                        LaunchErrorCode.BackendUnavailable,
                        "Built-in factory binding has an incompatible class",
                        descriptor.AppId);
                    return false;
                }
            }
            return true;
        }

        public static bool IsRegistered(string appId) {
            ApplicationFactory ignored;
            return TryGet(appId, out ignored);
        }

        public static bool TryGet(string appId, out ApplicationFactory factory) {
            Initialize();
            factory = null;
            if (string.IsNullOrEmpty(appId)) return false;
            for (int i = 0; i < _count; i++) {
                if (TextEquals(_bindings[i].AppId, appId)) {
                    factory = _bindings[i].Factory;
                    return factory != null;
                }
            }
            return false;
        }

        /// <summary>
        /// Register a backend binding with bounded failure reporting.  Built-in
        /// startup bindings use the same validation path, while public callers
        /// can only add a descriptor-compatible factory.
        /// </summary>
        public static bool TryRegister(string appId, ApplicationFactory factory,
                                       out LaunchResult failure) {
            Initialize();
            return TryRegisterCore(appId, factory, out failure);
        }

        /// <summary>
        /// If a factory is bound, launch it and return true even when the
        /// factory fails.  A false return means no binding exists and explicitly
        /// selects the compatibility backend.
        /// </summary>
        internal static bool TryLaunch(ApplicationDescriptor descriptor,
                                       LaunchRequest request,
                                       out LaunchResult result) {
            Initialize();
            result = null;
            if (descriptor == null) return false;

            ApplicationFactory factory;
            if (!TryGet(descriptor.AppId, out factory)) return false;

            // Name/alias launches are resolved by the App Model before this
            // backend boundary.  Store the stable descriptor identity in the
            // instance context without changing the bounded request payload.
            LaunchRequest factoryRequest = request == null ? null :
                request.WithTargetAppId(descriptor.AppId);

            _factoryLaunches++;
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!ApplicationInstanceRegistry.TryBeginLaunch(descriptor,
                    factoryRequest,
                    out instance, out reused, out failure)) {
                _factoryFailures++;
                result = failure;
                EmitFactoryFailure(descriptor, failure);
                return true;
            }

            if (!reused) {
                try {
                    ApplicationLifecycleAdapter lifecycleAdapter =
                        factory.CreateLifecycleAdapter();
                    if (lifecycleAdapter != null)
                        instance.SetLifecycleAdapter(lifecycleAdapter);
                } catch {
                    // Keep the safe allocation-free default if the optional
                    // hook is unavailable during factory setup.
                }
            }

            int startingWindowCount = WindowManager.Windows == null ? 0 :
                WindowManager.Windows.Count;
            ApplicationFactoryResult factoryResult = null;
            bool invoked = false;
            try {
                invoked = factory.TryCreateOrActivate(descriptor, instance,
                    factoryRequest, out factoryResult);
            } catch {
                invoked = false;
            }

            if (!invoked || factoryResult == null || !factoryResult.Success) {
                _factoryFailures++;
                CleanupFailedFactoryLaunch(instance, reused, factoryResult,
                    startingWindowCount, factoryResult == null ?
                        "Factory did not return a result" :
                        (factoryResult.BoundedDiagnostic ??
                            "Factory rejected the launch"));
                result = FactoryFailureResult(descriptor, factoryResult,
                    "Application factory rejected the launch");
                EmitFactoryFailure(descriptor, result);
                return true;
            }

            bool attached = AttachFactoryWindows(instance, factoryResult);
            if (!attached) {
                _factoryFailures++;
                CleanupFailedFactoryLaunch(instance, reused, factoryResult,
                    startingWindowCount, "Factory window ownership failed");
                result = LaunchResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Factory window ownership failed", descriptor.AppId);
                EmitFactoryFailure(descriptor, result);
                return true;
            }

            if (factoryResult.AttachedWindowCount == 0 &&
                !instance.AllowZeroWindows) {
                _factoryFailures++;
                CleanupFailedFactoryLaunch(instance, reused, factoryResult,
                    startingWindowCount,
                    "Factory returned no window for a windowed application");
                result = LaunchResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Factory returned no window for a windowed application",
                    descriptor.AppId);
                EmitFactoryFailure(descriptor, result);
                return true;
            }

            _factoryWindowsAttached += factoryResult.AttachedWindowCount;

            if (!ApplicationInstanceRegistry.TryCompleteLaunch(instance, true,
                    out failure)) {
                _factoryFailures++;
                CleanupFailedFactoryLaunch(instance, reused, factoryResult,
                    startingWindowCount, "Factory activation failed");
                result = failure ?? LaunchResult.Failed(
                    LaunchErrorCode.ActivationFailed,
                    "Factory activation failed", descriptor.AppId);
                EmitFactoryFailure(descriptor, result);
                return true;
            }

            if (reused) _reusedFactoryActivations++;
            EmitFactoryLaunchMarker(descriptor, instance, reused,
                factoryResult);
            result = LaunchResult.Succeeded(descriptor.AppId, instance.Handle,
                LaunchActivationState.Activated);
            return true;
        }

        internal static void RecordCompatibilityFallback(string appId) {
            Initialize();
            _compatibilityFallbackLaunches++;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("LAUNCH_BACKEND=compatibility;app=" +
                (appId ?? ""));
#endif
        }

        internal static void RecordTypedExternalLaunch(string backend) {
            Initialize();
            _typedExternalLaunches++;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("LAUNCH_BACKEND=typed-external;backend=" +
                (backend ?? ""));
#endif
        }

        internal static void RecordTypedShellActionLaunch(string action) {
            Initialize();
            _typedShellActionLaunches++;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime(
                "LAUNCH_BACKEND=typed-shell-action;action=" +
                (action ?? ""));
#endif
        }

        public static bool RunSelfTest() {
            Initialize();
            int passed = 0;
            int failed = 0;
            string firstFailure = null;

            string[] expectedBuiltInIds = new string[] {
                "gxos.builtin.calculator", "gxos.builtin.files",
                "gxos.builtin.console", "gxos.builtin.devices",
                "gxos.builtin.diskmanager", "gxos.builtin.displayoptions",
                "gxos.builtin.firewall", "gxos.builtin.notepad",
                "gxos.builtin.paint", "gxos.builtin.taskmanager",
                "gxos.builtin.imageviewer", "gxos.builtin.wavplayer"
            };
            Check(Count == expectedBuiltInIds.Length,
                "factory registration count", ref passed,
                ref failed, ref firstFailure);
            bool allBindings = true;
            for (int i = 0; i < expectedBuiltInIds.Length; i++) {
                if (!IsRegistered(expectedBuiltInIds[i])) allBindings = false;
            }
            Check(allBindings, "all built-in factory bindings", ref passed,
                ref failed, ref firstFailure);
            LaunchResult bindingFailure;
            Check(ValidateBuiltInBindings(out bindingFailure) &&
                bindingFailure == null, "missing built-in binding detection",
                ref passed, ref failed, ref firstFailure);
            Binding[] missingBindings = CopyBindings();
            int missingCount = _count;
            if (missingCount > 0) {
                missingBindings[0] = null;
                missingCount--;
                for (int i = 0; i < missingCount; i++) {
                    if (missingBindings[i] == null) {
                        missingBindings[i] = missingBindings[i + 1];
                    }
                }
            }
            LaunchResult missingProbeFailure;
            Check(!ValidateBuiltInBindingsCore(missingBindings, missingCount,
                out missingProbeFailure) && missingProbeFailure != null,
                "missing built-in binding probe", ref passed, ref failed,
                ref firstFailure);
            Binding[] mismatchBindings = CopyBindings();
            if (mismatchBindings.Length > 0) {
                mismatchBindings[0] = new Binding {
                    AppId = "gxos.builtin.calculator",
                    Factory = new FactorySelfTestProbe(ApplicationClass.Gxm,
                        true) };
            }
            LaunchResult mismatchProbeFailure;
            Check(!ValidateBuiltInBindingsCore(mismatchBindings, _count,
                out mismatchProbeFailure) && mismatchProbeFailure != null,
                "descriptor factory mismatch probe", ref passed, ref failed,
                ref firstFailure);
            Check(!IsRegistered("gxos.external.gxm"),
                "typed backend distinction", ref passed, ref failed,
                ref firstFailure);

            LaunchResult capacityFailure;
            Binding[] capacityBindings = new Binding[Capacity];
            int capacityCount = 0;
            bool capacityFilled = true;
            for (int i = 0; i < Capacity; i++) {
                if (!TryAppendBinding(capacityBindings, ref capacityCount,
                        "factory.capacity." + i.ToString(),
                        new FactorySelfTestProbe(ApplicationClass.BuiltIn,
                            true), out capacityFailure)) {
                    capacityFilled = false;
                    break;
                }
            }
            bool capacityRejected = capacityFilled && capacityCount == Capacity &&
                !TryAppendBinding(capacityBindings, ref capacityCount,
                    "factory.capacity.overflow",
                    new FactorySelfTestProbe(ApplicationClass.BuiltIn, true),
                    out capacityFailure) && capacityFailure != null &&
                capacityFailure.ErrorCode == LaunchErrorCode.ResourceUnavailable;
            Check(capacityRejected, "factory capacity boundary", ref passed,
                ref failed, ref firstFailure);

            LaunchResult duplicateFailure;
            bool duplicateRejected = !TryRegister(
                "gxos.builtin.calculator",
                new FactorySelfTestProbe(ApplicationClass.BuiltIn, true),
                out duplicateFailure) && duplicateFailure != null &&
                duplicateFailure.ErrorCode == LaunchErrorCode.BackendUnavailable;
            Check(duplicateRejected, "duplicate factory binding", ref passed,
                ref failed, ref firstFailure);

            LaunchResult incompatibleFailure;
            bool incompatibleRejected = !TryRegister(
                "gxos.builtin.calculator",
                new FactorySelfTestProbe(ApplicationClass.Gxm, true),
                out incompatibleFailure) && incompatibleFailure != null &&
                incompatibleFailure.ErrorCode == LaunchErrorCode.BackendUnavailable;
            Check(incompatibleRejected, "incompatible factory binding",
                ref passed, ref failed, ref firstFailure);

            ApplicationDescriptor console = null;
            ApplicationDescriptor calculator = null;
            bool descriptors =
                ApplicationDescriptorRegistry.TryGetById(
                    "gxos.builtin.console", out console) &&
                ApplicationDescriptorRegistry.TryGetById(
                    "gxos.builtin.calculator", out calculator);
            Check(descriptors, "factory test descriptors", ref passed,
                ref failed, ref firstFailure);

            if (descriptors) {
                ApplicationInstance instance;
                ApplicationInstance reusedInstance;
                bool reused;
                LaunchResult failure;
                FactorySelfTestProbe successProbe =
                    new FactorySelfTestProbe(ApplicationClass.BuiltIn, true);
                LaunchRequest documentRequest = LaunchRequest.ForFile(
                    "gxos.builtin.console", "Programs/factory.txt",
                    new string[] { "--factory" }, "open",
                    "gxos.shell.computerfiles", false,
                    LaunchActivationIntent.Launch);
                bool began = ApplicationInstanceRegistry.TryBeginLaunch(console,
                    documentRequest, out instance, out reused, out failure);
                bool context = began && instance.LaunchRequestContext != null &&
                    instance.LaunchRequestContext.TargetAppId == console.AppId &&
                    instance.LaunchRequestContext.TargetKind ==
                        LaunchRequestTargetKind.FileOpen &&
                    instance.Document == "Programs/factory.txt" &&
                    instance.Verb == "open" &&
                    instance.SourceShellObjectId == "gxos.shell.computerfiles" &&
                    instance.LaunchRequestContext.ActivationIntent ==
                        LaunchActivationIntent.Launch &&
                    instance.ArgumentCount == 1 &&
                    instance.GetArgument(0) == "--factory";
                ApplicationFactoryResult ignoredResult = null;
                bool probeSuccess = began && successProbe.TryCreateOrActivate(
                    console, instance, documentRequest, out ignoredResult) &&
                    ignoredResult != null && ignoredResult.Success &&
                    ignoredResult.AttachedWindowCount == 0;
                bool zeroWindowSuccess = began && probeSuccess &&
                    instance.AllowZeroWindows &&
                    ApplicationInstanceRegistry.TryCompleteLaunch(instance, true,
                        out failure) && instance.IsActivated;
                Check(context, "factory document propagation", ref passed,
                    ref failed, ref firstFailure);
                Check(zeroWindowSuccess, "zero-window factory result", ref passed,
                    ref failed, ref firstFailure);

                bool reusedLaunch = ApplicationInstanceRegistry.TryBeginLaunch(
                    console, LaunchRequest.ForAppId(console.AppId, null, null,
                        LaunchActivationIntent.ActivateExisting),
                    out reusedInstance, out reused, out failure) && reused &&
                    reusedInstance.Handle == instance.Handle &&
                    ApplicationInstanceRegistry.TryCompleteLaunch(reusedInstance,
                        true, out failure);
                Check(reusedLaunch, "reused-instance factory semantics",
                    ref passed, ref failed, ref firstFailure);
                ApplicationInstanceRegistry.TryTerminate(instance,
                    "factory self-test reuse cleanup");

                ApplicationInstance first = null;
                ApplicationInstance second = null;
                bool firstReused;
                bool secondReused;
                bool multiLaunch =
                    ApplicationInstanceRegistry.TryBeginLaunch(calculator,
                        LaunchRequest.ForAppId(calculator.AppId, null, null,
                            LaunchActivationIntent.NewInstance), out first,
                        out firstReused, out failure) &&
                    ApplicationInstanceRegistry.TryBeginLaunch(calculator,
                        LaunchRequest.ForAppId(calculator.AppId, null, null,
                            LaunchActivationIntent.NewInstance), out second,
                        out secondReused, out failure) && !firstReused &&
                    !secondReused && first.Handle != second.Handle;
                Check(multiLaunch, "multi-instance factory semantics", ref passed,
                    ref failed, ref firstFailure);
                if (first != null) ApplicationInstanceRegistry.TryTerminate(first,
                    "factory self-test multi cleanup");
                if (second != null) ApplicationInstanceRegistry.TryTerminate(second,
                    "factory self-test multi cleanup");

                ApplicationInstance failedInstance;
                bool failedReused;
                bool failureBegan = ApplicationInstanceRegistry.TryBeginLaunch(
                    calculator, LaunchRequest.ForAppId(calculator.AppId, null,
                        null, LaunchActivationIntent.NewInstance),
                    out failedInstance, out failedReused, out failure);
                ApplicationFactoryResult failedResult =
                    FactorySelfTestProbe.FailedResult(calculator.AppId);
                bool failureResult = failureBegan && !failedResult.Success;
                if (failureBegan) ApplicationInstanceRegistry.FailLaunch(
                    failedInstance, failedReused, failedResult.BoundedDiagnostic);
                Check(failureResult && failedInstance != null &&
                    !ApplicationInstanceRegistry.TryGet(failedInstance.Handle,
                        out ApplicationInstance ignoredFailedInstance),
                    "failed factory cleanup", ref passed, ref failed,
                    ref firstFailure);
            }

            bool noActiveInstances = ApplicationInstanceRegistry.ActiveCount == 0;
            Check(noActiveInstances, "factory self-test active cleanup", ref passed,
                ref failed, ref firstFailure);
            AppLaunchResolver.EmitSelfTestSummary("AppModelPhase4Factory",
                passed, failed, firstFailure);
            return failed == 0;
        }

        private static bool TryRegisterCore(string appId,
                                            ApplicationFactory factory,
                                            out LaunchResult failure) {
            failure = null;
            if (string.IsNullOrEmpty(appId) || factory == null) {
                failure = LaunchResult.Failed(LaunchErrorCode.MalformedRequest,
                    "Factory binding is incomplete", appId);
                return false;
            }

            ApplicationDescriptor descriptor;
            if (!ApplicationDescriptorRegistry.TryGetById(appId,
                    out descriptor)) {
                failure = LaunchResult.Failed(LaunchErrorCode.NotFound,
                    "Application descriptor is unavailable", appId);
                return false;
            }
            if (factory.SupportedApplicationClass !=
                    descriptor.ApplicationClass) {
                failure = LaunchResult.Failed(LaunchErrorCode.BackendUnavailable,
                    "Factory application class is incompatible", appId);
                return false;
            }
            return TryAppendBinding(_bindings, ref _count, descriptor.AppId,
                factory, out failure);
        }

        private static bool TryAppendBinding(Binding[] bindings, ref int count,
                                             string appId,
                                             ApplicationFactory factory,
                                             out LaunchResult failure) {
            failure = null;
            if (bindings == null || string.IsNullOrEmpty(appId) ||
                    factory == null) {
                failure = LaunchResult.Failed(LaunchErrorCode.MalformedRequest,
                    "Factory binding is incomplete", appId);
                return false;
            }
            for (int i = 0; i < count; i++) {
                if (bindings[i] != null && TextEquals(bindings[i].AppId,
                        appId)) {
                    failure = LaunchResult.Failed(
                        LaunchErrorCode.BackendUnavailable,
                        "Duplicate application factory binding", appId);
                    return false;
                }
            }
            if (count >= bindings.Length) {
                failure = LaunchResult.Failed(LaunchErrorCode.ResourceUnavailable,
                    "Application factory capacity exhausted", appId);
                return false;
            }
            bindings[count++] = new Binding { AppId = appId, Factory = factory };
            return true;
        }

        private static bool TryGetFrom(Binding[] bindings, int count,
                                       string appId,
                                       out ApplicationFactory factory) {
            factory = null;
            if (bindings == null || string.IsNullOrEmpty(appId)) return false;
            if (count > bindings.Length) count = bindings.Length;
            for (int i = 0; i < count; i++) {
                if (bindings[i] != null && TextEquals(bindings[i].AppId,
                        appId)) {
                    factory = bindings[i].Factory;
                    return factory != null;
                }
            }
            return false;
        }

        private static Binding[] CopyBindings() {
            Binding[] copy = new Binding[_count];
            for (int i = 0; i < _count; i++) copy[i] = _bindings[i];
            return copy;
        }

        private static void RegisterInitial(string appId,
                                            ApplicationFactory factory) {
            LaunchResult failure;
            if (!TryRegisterCore(appId, factory, out failure) &&
                    _initializationFailure == null) {
                _initializationFailure = failure == null ?
                    "Factory registration failed" : failure.BoundedDiagnostic;
            }
        }

        private static bool AttachFactoryWindows(ApplicationInstance instance,
                                                 ApplicationFactoryResult result) {
            for (int i = 0; i < result.AttachedWindowCount; i++) {
                if (!ApplicationInstanceRegistry.TryAttachWindow(instance,
                        result.GetWindowAt(i))) return false;
            }
            return true;
        }

        private static void CleanupFailedFactoryLaunch(
                ApplicationInstance instance, bool reused,
                ApplicationFactoryResult result, int startingWindowCount,
                string diagnostic) {
            ApplicationInstanceRegistry.CleanupFactoryWindows(instance, result,
                startingWindowCount);
            ApplicationInstanceRegistry.FailLaunch(instance, reused, diagnostic);
        }

        private static LaunchResult FactoryFailureResult(
                ApplicationDescriptor descriptor, ApplicationFactoryResult result,
                string fallbackDiagnostic) {
            if (result != null && result.LaunchResult != null) {
                LaunchErrorCode code = result.ErrorCode == LaunchErrorCode.Success
                    ? LaunchErrorCode.InitializationFailed : result.ErrorCode;
                return LaunchResult.Failed(code,
                    result.BoundedDiagnostic ?? fallbackDiagnostic,
                    descriptor.AppId);
            }
            return LaunchResult.Failed(LaunchErrorCode.InitializationFailed,
                fallbackDiagnostic, descriptor.AppId);
        }

        private static void EmitFactoryLaunchMarker(
                ApplicationDescriptor descriptor, ApplicationInstance instance,
                bool reused, ApplicationFactoryResult result) {
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Window diagnosticWindow = result.AttachedWindowCount == 0 ? null :
                result.GetWindowAt(0);
            Program.MarkUefiAppRuntime("LAUNCH_BACKEND=factory;app=" +
                descriptor.AppId + ";windows=" +
                result.AttachedWindowCount.ToString());
            Program.MarkUefiAppRuntime("LAUNCH_OK=id=" + descriptor.AppId +
                ";name=" + descriptor.DisplayName + ";type=" +
                (diagnosticWindow != null ? "WINDOW" : "NONE") +
                ";bounds=" + (diagnosticWindow == null ? "0,0,0,0" :
                    diagnosticWindow.X.ToString() + "," +
                    diagnosticWindow.Y.ToString() + "," +
                    diagnosticWindow.Width.ToString() + "," +
                    diagnosticWindow.Height.ToString()) +
                ";windows=" + (WindowManager.Windows == null ? "0" :
                    WindowManager.Windows.Count.ToString()) +
                ";instance=" + instance.Handle.ToString() +
                ";state=" + instance.LifecycleStateName + ";owned=" +
                instance.OwnedWindowCount.ToString());
            Program.MarkUefiAppRuntime("FACTORY_LAUNCH=app=" +
                descriptor.AppId + ";instance=" + instance.Handle.ToString() +
                ";reused=" + (reused ? "1" : "0") +
                ";windows=" + result.AttachedWindowCount.ToString());
#endif
        }

        private static void EmitFactoryFailure(ApplicationDescriptor descriptor,
                                               LaunchResult result) {
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("FACTORY_FAILURE=app=" +
                (descriptor == null ? "" : descriptor.AppId) + ";code=" +
                (result == null ? "InitializationFailed" :
                    result.ErrorCodeName));
#endif
        }

        private static void Check(bool condition, string label, ref int passed,
                                  ref int failed, ref string firstFailure) {
            if (condition) {
                passed++;
                return;
            }
            failed++;
            if (firstFailure == null) firstFailure = label;
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

        private sealed class FactorySelfTestProbe : ApplicationFactory {
            private readonly ApplicationClass _class;
            private readonly bool _success;

            internal FactorySelfTestProbe(ApplicationClass applicationClass,
                                           bool success) {
                _class = applicationClass;
                _success = success;
            }

            public override ApplicationClass SupportedApplicationClass {
                get { return _class; }
            }

            public override bool TryCreateOrActivate(
                    ApplicationDescriptor descriptor,
                    ApplicationInstance instance,
                    LaunchRequest request,
                    out ApplicationFactoryResult result) {
                result = _success ? ApplicationFactoryResult.Succeeded(instance) :
                    FailedResult(descriptor == null ? null : descriptor.AppId);
                return _success;
            }

            internal static ApplicationFactoryResult FailedResult(string appId) {
                return ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "factory self-test failure", appId);
            }
        }
    }

    internal sealed class CalculatorApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override ApplicationLifecycleAdapter CreateLifecycleAdapter() {
            return new CalculatorApplicationLifecycleAdapter();
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            ApplicationServiceContext serviceContext;
            ApplicationServiceAccess services;
            if (!TryCreateApplicationServices(descriptor, instance,
                    out serviceContext, out services, out result)) return false;
            result = ApplicationFactoryResult.Succeeded(instance);
            Calculator calculator = new Calculator(300, 500,
                serviceContext, services);
            if (!result.AddWindow(calculator)) {
                calculator.CloseForApplicationTermination();
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Calculator window registration failed", descriptor.AppId);
                return false;
            }
            WindowManager.MoveToEnd(calculator);
            calculator.Visible = true;
            return true;
        }
    }

    internal sealed class NotepadApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override ApplicationLifecycleAdapter CreateLifecycleAdapter() {
            return new NotepadApplicationLifecycleAdapter();
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            ApplicationServiceContext serviceContext;
            ApplicationServiceAccess services;
            if (!TryCreateApplicationServices(descriptor, instance,
                    out serviceContext, out services, out result)) return false;
            result = ApplicationFactoryResult.Succeeded(instance);
            Notepad notepad = new Notepad(360, 200,
                serviceContext, services);
            if (!result.AddWindow(notepad)) {
                notepad.CloseForApplicationTermination();
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Notepad window registration failed", descriptor.AppId);
                return false;
            }
            if (!string.IsNullOrEmpty(request.Document) &&
                    !notepad.OpenFile(request.Document)) {
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.ResourceUnavailable,
                    "Notepad document could not be opened", descriptor.AppId);
                return false;
            }
            WindowManager.MoveToEnd(notepad);
            notepad.Visible = true;
            return true;
        }
    }

    internal sealed class ConsoleApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override ApplicationLifecycleAdapter CreateLifecycleAdapter() {
            return new ConsoleApplicationLifecycleAdapter();
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            FConsole console = Program.FConsole;
            if (console == null || WindowManager.Windows == null ||
                    WindowManager.Windows.IndexOf(console) < 0) {
                console = new FConsole(160, 120);
                Program.FConsole = console;
            }
            if (!result.AddWindow(console)) {
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Console window registration failed", descriptor.AppId);
                return false;
            }
            console.Visible = true;
            WindowManager.MoveToEnd(console);
            return true;
        }
    }

    internal sealed class ImageViewerApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override ApplicationLifecycleAdapter CreateLifecycleAdapter() {
            return new ImageViewerApplicationLifecycleAdapter();
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            ImageViewer viewer = Desktop.EnsureImageViewer();
            if (!result.AddWindow(viewer)) {
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Image Viewer window registration failed", descriptor.AppId);
                return false;
            }

            if (!string.IsNullOrEmpty(request.Document)) {
                FileAssociationResolution association =
                    FileAssociationRegistry.ResolvePath(request.Document);
                byte[] buffer = File.ReadAllBytes(request.Document);
                if (buffer == null) {
                    result = ApplicationFactoryResult.Failed(
                        LaunchErrorCode.ResourceUnavailable,
                        "Image file read failed", descriptor.AppId);
                    return false;
                }
                Image decoded = null;
                try {
                    decoded = Desktop.DecodeDesktopImage(buffer,
                        association.Success && association.Extension == ".png");
                } finally {
                    buffer.Dispose();
                }
                if (decoded == null) {
                    result = ApplicationFactoryResult.Failed(
                        LaunchErrorCode.InitializationFailed,
                        "Image decode failed", descriptor.AppId);
                    return false;
                }
                try {
                    viewer.SetImage(decoded);
                    decoded = null;
                } catch {
                    if (decoded != null) decoded.Dispose();
                    result = ApplicationFactoryResult.Failed(
                        LaunchErrorCode.InitializationFailed,
                        "Image Viewer rejected the image", descriptor.AppId);
                    return false;
                }
            }
            WindowManager.MoveToEnd(viewer);
            viewer.Visible = true;
            return true;
        }
    }

    internal sealed class ComputerFilesApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            ComputerFiles files = null;
            FileSystem ownedFileSystem = null;
            try {
                bool driveRequest = request != null &&
                    request.TargetKind == LaunchRequestTargetKind.ShellObject &&
                    request.ShellTargetKind == ApplicationShellTargetKind.FileSystem &&
                    !string.IsNullOrEmpty(request.ShellTargetValue);
                if (driveRequest) {
                    string failure;
                    if (!TryCreateDriveFileSystem(request.ShellTargetValue,
                            out ownedFileSystem, out failure)) {
                        result = ApplicationFactoryResult.Failed(
                            LaunchErrorCode.ResourceUnavailable, failure,
                            descriptor.AppId);
                        return false;
                    }
                    files = new ComputerFiles(
                        FactoryRequestOptions.IntArgument(request, "--x=", 320),
                        FactoryRequestOptions.IntArgument(request, "--y=", 220),
                        540, 400,
                        ownedFileSystem, request.ShellTargetValue, true);
                    ownedFileSystem = null;
                } else {
                    files = new ComputerFiles(300, 200, 540, 380);
                }
                if (!result.AddWindow(files)) {
                    files.CloseForApplicationTermination();
                    result = ApplicationFactoryResult.Failed(
                        LaunchErrorCode.InitializationFailed,
                        "Computer Files window registration failed",
                        descriptor.AppId);
                    return false;
                }
                WindowManager.MoveToEnd(files);
                files.Visible = true;
                return true;
            } catch {
                if (files != null) files.Dispose();
                if (ownedFileSystem != null) ownedFileSystem.Dispose();
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Computer Files initialization failed", descriptor.AppId);
                return false;
            }
        }

        private static bool TryCreateDriveFileSystem(string driveName,
                                                     out FileSystem fileSystem,
                                                     out string failure) {
            fileSystem = null;
            failure = "Drive is unavailable";
            if (TextEquals(driveName, "Hard Disk")) {
                try {
                    fileSystem = new AutoFS();
                    return true;
                } catch {
                    return false;
                }
            }
            const string usbPrefix = "USB Drive ";
            if (driveName == null || driveName.Length <= usbPrefix.Length ||
                    !StartsWithIgnoreCase(driveName, usbPrefix)) return false;
            int number;
            if (!TryParsePositiveInteger(driveName, usbPrefix.Length,
                    out number)) return false;
            USBDevice[] devices;
            try { devices = USBStorage.GetAll(); } catch { return false; }
            int index = number - 1;
            if (devices == null || index < 0 || index >= devices.Length) return false;
            try {
                var disk = USBMSC.TryOpenDisk(devices[index]);
                if (disk == null || !disk.IsReady) return false;
                fileSystem = new AutoFS(disk);
                return true;
            } catch {
                if (fileSystem != null) {
                    fileSystem.Dispose();
                    fileSystem = null;
                }
                return false;
            }
        }

        private static bool TryParsePositiveInteger(string value, int start,
                                                    out int parsed) {
            parsed = 0;
            if (value == null || start < 0 || start >= value.Length) return false;
            for (int i = start; i < value.Length; i++) {
                char c = value[i];
                if (c < '0' || c > '9') return false;
                int digit = c - '0';
                if (parsed > 214748364 ||
                        (parsed == 214748364 && digit > 7)) return false;
                parsed = parsed * 10 + digit;
            }
            return parsed > 0;
        }

        private static bool StartsWithIgnoreCase(string value, string prefix) {
            if (value == null || prefix == null || value.Length < prefix.Length)
                return false;
            for (int i = 0; i < prefix.Length; i++) {
                char a = value[i];
                char b = prefix[i];
                if (a >= 'A' && a <= 'Z') a = (char)(a + 32);
                if (b >= 'A' && b <= 'Z') b = (char)(b + 32);
                if (a != b) return false;
            }
            return true;
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

    internal sealed class DevicesApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            Devices devices = new Devices(400, 300);
            if (!result.AddWindow(devices)) {
                devices.CloseForApplicationTermination();
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Devices window registration failed", descriptor.AppId);
                return false;
            }
            WindowManager.MoveToEnd(devices);
            devices.Visible = true;
            return true;
        }
    }

    internal sealed class DiskManagerApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            DiskManager manager = new DiskManager(400, 300);
            if (!result.AddWindow(manager)) {
                manager.CloseForApplicationTermination();
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Disk Manager window registration failed", descriptor.AppId);
                return false;
            }
            WindowManager.MoveToEnd(manager);
            manager.Visible = true;
            return true;
        }
    }

    internal sealed class DisplayOptionsApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            ApplicationServiceContext serviceContext;
            ApplicationServiceAccess services;
            if (!TryCreateApplicationServices(descriptor, instance,
                    out serviceContext, out services, out result)) return false;
            result = ApplicationFactoryResult.Succeeded(instance);
            DisplayOptions options = instance.GetOwnedWindowAt(0)
                as DisplayOptions;
            if (options == null) {
                options = new DisplayOptions(
                    FactoryRequestOptions.IntArgument(request, "--x=", 200),
                    FactoryRequestOptions.IntArgument(request, "--y=", 150),
                    FactoryRequestOptions.IntArgument(request, "--w=", 800),
                    FactoryRequestOptions.IntArgument(request, "--h=", 600),
                    serviceContext, services);
            }
            if (!result.AddWindow(options)) {
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Display Options window registration failed",
                    descriptor.AppId);
                return false;
            }
            WindowManager.MoveToEnd(options);
            options.Visible = true;
            return true;
        }
    }

    internal sealed class FirewallApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            FirewallWindow window = instance.GetOwnedWindowAt(0)
                as FirewallWindow;
            if (window == null) window = Firewall.EnsureWindow();
            if (!result.AddWindow(window)) {
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Firewall window registration failed", descriptor.AppId);
                return false;
            }
            WindowManager.MoveToEnd(window);
            window.Visible = true;
            return true;
        }
    }

    internal sealed class PaintApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            Paint paint = new Paint(500, 200);
            if (!result.AddWindow(paint)) {
                paint.CloseForApplicationTermination();
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Paint window registration failed", descriptor.AppId);
                return false;
            }
            WindowManager.MoveToEnd(paint);
            paint.Visible = true;
            return true;
        }
    }

    internal sealed class TaskManagerApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            TaskManager manager = instance.GetOwnedWindowAt(0)
                as TaskManager;
            if (manager == null) manager = new TaskManager(500, 500);
            if (!result.AddWindow(manager)) {
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Task Manager window registration failed", descriptor.AppId);
                return false;
            }
            WindowManager.MoveToEnd(manager);
            manager.Visible = true;
            return true;
        }
    }

    internal sealed class WavPlayerApplicationFactory : ApplicationFactory {
        public override ApplicationClass SupportedApplicationClass {
            get { return ApplicationClass.BuiltIn; }
        }

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            // A bare Start-menu launch preserves the existing player window
            // behavior even on machines without an audio controller.  A
            // document activation is the resource-dependent operation and
            // fails boundedly before creating/claiming the reusable helper.
            if (!Audio.HasAudioDevice && request != null &&
                    !string.IsNullOrEmpty(request.Document)) {
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.ResourceUnavailable,
                    "Audio device unavailable", descriptor.AppId);
                return false;
            }

            WAVPlayer player = instance.GetOwnedWindowAt(0) as WAVPlayer;
            if (player == null) player = Desktop.EnsureWavPlayer();
            if (!result.AddWindow(player)) {
                result = ApplicationFactoryResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "WAV Player window registration failed", descriptor.AppId);
                return false;
            }

            if (request != null && !string.IsNullOrEmpty(request.Document)) {
                byte[] buffer = File.ReadAllBytes(request.Document);
                if (buffer == null) {
                    result = ApplicationFactoryResult.Failed(
                        LaunchErrorCode.ResourceUnavailable,
                        "WAV document could not be read", descriptor.AppId);
                    return false;
                }
                if (!player.TryPlay(buffer, request.Document)) {
                    result = ApplicationFactoryResult.Failed(
                        LaunchErrorCode.InitializationFailed,
                        "WAV document could not be decoded", descriptor.AppId);
                    return false;
                }
            }
            WindowManager.MoveToEnd(player);
            player.Visible = true;
            return true;
        }
    }

    internal static class FactoryRequestOptions {
        internal static int IntArgument(LaunchRequest request, string prefix,
                                        int fallback) {
            if (request == null || prefix == null) return fallback;
            for (int i = 0; i < request.ArgumentCount; i++) {
                string value = request.GetArgument(i);
                if (value == null || value.Length <= prefix.Length ||
                        !StartsWithIgnoreCase(value, prefix)) continue;
                int parsed = 0;
                bool valid = true;
                for (int p = prefix.Length; p < value.Length; p++) {
                    char c = value[p];
                    if (c < '0' || c > '9') { valid = false; break; }
                    parsed = parsed * 10 + c - '0';
                }
                if (valid) return parsed;
            }
            return fallback;
        }

        private static bool StartsWithIgnoreCase(string value, string prefix) {
            if (value == null || prefix == null || value.Length < prefix.Length)
                return false;
            for (int i = 0; i < prefix.Length; i++) {
                char a = value[i];
                char b = prefix[i];
                if (a >= 'A' && a <= 'Z') a = (char)(a + 32);
                if (b >= 'A' && b <= 'Z') b = (char)(b + 32);
                if (a != b) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Typed backend for the installer shell action.  It is deliberately not
    /// a built-in descriptor because the action is not a normal application.
    /// </summary>
    internal static class ApplicationShellActionBackend {
        internal static bool TryLaunchInstaller(LaunchRequest request, int x,
                                                 int y, out LaunchResult result) {
            result = null;
            if (request == null) {
                request = LaunchRequest.ForShellObject(
                    "gxos.shell.installtoharddrive", null,
                    "Install to Hard Drive", ApplicationShellTargetKind.Action,
                    "HDInstaller", LaunchActivationIntent.Launch);
            }
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!ApplicationInstanceRegistry.TryBeginLaunch(
                    "gxos.shell.installer", ApplicationInstancePolicy.ShellOwned,
                    request, out instance, out reused, out failure)) {
                result = failure;
                return false;
            }
            HDInstaller installer = null;
            try {
                installer = new HDInstaller(x + 60, y + 60);
                if (!ApplicationInstanceRegistry.TryAttachWindow(instance,
                        installer)) {
                    if (!installer.ApplicationInstanceHandle.IsValid)
                        installer.CloseForApplicationTermination();
                    ApplicationInstanceRegistry.FailLaunch(instance, reused,
                        "Installer window ownership failed");
                    result = LaunchResult.Failed(
                        LaunchErrorCode.InitializationFailed,
                        "Installer window ownership failed",
                        "gxos.shell.installer");
                    return false;
                }
                WindowManager.MoveToEnd(installer);
                installer.Visible = true;
                if (!ApplicationInstanceRegistry.TryCompleteLaunch(instance, true,
                        out failure)) {
                    ApplicationInstanceRegistry.FailLaunch(instance, reused,
                        "Installer activation failed");
                    result = failure ?? LaunchResult.Failed(
                        LaunchErrorCode.ActivationFailed,
                        "Installer activation failed", "gxos.shell.installer");
                    return false;
                }
                ApplicationFactoryRegistry.RecordTypedShellActionLaunch(
                    "HDInstaller");
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                Program.MarkUefiAppRuntime("INSTALLER_INSTANCE_OK=instance=" +
                    instance.Handle.ToString() + ";state=" +
                    instance.LifecycleStateName + ";owned=" +
                    instance.OwnedWindowCount.ToString());
                Program.MarkUefiAppRuntime("INSTALLER_BOUNDS=x=" +
                    installer.X.ToString() + ";y=" + installer.Y.ToString() +
                    ";w=" + installer.Width.ToString());
#endif
                result = LaunchResult.Succeeded("gxos.shell.installer",
                    instance.Handle, LaunchActivationState.Activated);
                return true;
            } catch {
                if (installer != null &&
                        !installer.ApplicationInstanceHandle.IsValid)
                    installer.CloseForApplicationTermination();
                ApplicationInstanceRegistry.FailLaunch(instance, reused,
                    "Installer backend rejected the launch");
                result = LaunchResult.Failed(
                    LaunchErrorCode.InitializationFailed,
                    "Installer backend rejected the launch",
                    "gxos.shell.installer");
                return false;
            }
        }
    }
}
