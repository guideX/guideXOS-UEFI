using guideXOS.DefaultApps;
using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
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

        public static void Initialize() {
            if (_initialized) return;
            _bindings = new Binding[Capacity];
            _initialized = true;

            // Registration order is part of the deterministic diagnostic
            // surface and follows the Phase 3 representative cohort.
            RegisterInitial("gxos.builtin.calculator",
                new CalculatorApplicationFactory());
            RegisterInitial("gxos.builtin.notepad",
                new NotepadApplicationFactory());
            RegisterInitial("gxos.builtin.console",
                new ConsoleApplicationFactory());
            RegisterInitial("gxos.builtin.imageviewer",
                new ImageViewerApplicationFactory());
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
        public static string InitializationFailure {
            get { Initialize(); return _initializationFailure; }
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

        public static bool RunSelfTest() {
            Initialize();
            int passed = 0;
            int failed = 0;
            string firstFailure = null;

            Check(Count == 4, "factory registration count", ref passed,
                ref failed, ref firstFailure);
            Check(IsRegistered("gxos.builtin.calculator") &&
                IsRegistered("gxos.builtin.notepad") &&
                IsRegistered("gxos.builtin.console") &&
                IsRegistered("gxos.builtin.imageviewer"),
                "cohort bindings", ref passed, ref failed, ref firstFailure);
            Check(!IsRegistered("gxos.builtin.files"),
                "missing factory fallback", ref passed, ref failed,
                ref firstFailure);
            ApplicationDescriptor missingFactoryDescriptor = null;
            LaunchResult missingFactoryResult = null;
            bool missingFactoryDispatch =
                ApplicationDescriptorRegistry.TryGetById(
                    "gxos.builtin.files", out missingFactoryDescriptor) &&
                !TryLaunch(missingFactoryDescriptor,
                    LaunchRequest.ForAppId(missingFactoryDescriptor.AppId,
                        null, null, LaunchActivationIntent.Launch),
                    out missingFactoryResult) && missingFactoryResult == null;
            Check(missingFactoryDispatch, "missing factory dispatch boundary",
                ref passed, ref failed, ref firstFailure);

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
            AppLaunchResolver.EmitSelfTestSummary("AppModelPhase3Factory",
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
            for (int i = 0; i < _count; i++) {
                if (TextEquals(_bindings[i].AppId, appId)) {
                    failure = LaunchResult.Failed(
                        LaunchErrorCode.BackendUnavailable,
                        "Duplicate application factory binding", appId);
                    return false;
                }
            }
            if (_count >= Capacity) {
                failure = LaunchResult.Failed(LaunchErrorCode.ResourceUnavailable,
                    "Application factory capacity exhausted", appId);
                return false;
            }
            _bindings[_count++] = new Binding { AppId = descriptor.AppId,
                Factory = factory };
            return true;
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

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            Calculator calculator = new Calculator(300, 500);
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

        public override bool TryCreateOrActivate(
                ApplicationDescriptor descriptor,
                ApplicationInstance instance,
                LaunchRequest request,
                out ApplicationFactoryResult result) {
            result = ApplicationFactoryResult.Succeeded(instance);
            Notepad notepad = new Notepad(360, 200);
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
}
