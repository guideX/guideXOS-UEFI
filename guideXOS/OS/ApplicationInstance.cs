using System;
using guideXOS.DefaultApps;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;

namespace guideXOS.OS {
    /// <summary>
    /// Launch policy for the application-instance layer.  This is deliberately
    /// narrower than a process policy: it only controls instance reuse and
    /// ownership behavior around the existing managed application backend.
    /// </summary>
    public enum ApplicationInstancePolicy {
        MultiInstance,
        SingleInstance,
        ReuseExisting,
        ShellOwned
    }

    /// <summary>
    /// Application-instance lifecycle from APP_MODEL_CONVERGENCE.md.
    /// Registered is descriptor-known instance state; it is not descriptor
    /// identity.  Suspended is cooperative application quiescence, not
    /// scheduler or process freezing.
    /// </summary>
    public enum ApplicationInstanceLifecycleState {
        Registered,
        Loading,
        Initialized,
        Running,
        Activated,
        Inactive,
        Suspended,
        Closing,
        Terminated,
        Failed
    }

    /// <summary>
    /// Explicit names are used for guest diagnostics because the compact
    /// runtime corlib does not provide reliable Enum.ToString formatting.
    /// </summary>
    public static class ApplicationInstanceLifecycle {
        public static string Name(ApplicationInstanceLifecycleState state) {
            switch (state) {
                case ApplicationInstanceLifecycleState.Registered: return "Registered";
                case ApplicationInstanceLifecycleState.Loading: return "Loading";
                case ApplicationInstanceLifecycleState.Initialized: return "Initialized";
                case ApplicationInstanceLifecycleState.Running: return "Running";
                case ApplicationInstanceLifecycleState.Activated: return "Activated";
                case ApplicationInstanceLifecycleState.Inactive: return "Inactive";
                case ApplicationInstanceLifecycleState.Suspended: return "Suspended";
                case ApplicationInstanceLifecycleState.Closing: return "Closing";
                case ApplicationInstanceLifecycleState.Terminated: return "Terminated";
                case ApplicationInstanceLifecycleState.Failed: return "Failed";
                default: return "Unknown";
            }
        }
    }

    /// <summary>
    /// Stable bounded application-instance identity.  Slot is only a lookup
    /// location; Generation prevents a stale reference from addressing a
    /// later instance that reuses the same slot.
    /// </summary>
    public readonly struct ApplicationInstanceHandle {
        private readonly ulong _value;

        private ApplicationInstanceHandle(ulong value) {
            _value = value;
        }

        internal static ApplicationInstanceHandle Create(int slot, uint generation) {
            if (slot <= 0 || generation == 0) return new ApplicationInstanceHandle(0);
            return new ApplicationInstanceHandle(((ulong)(uint)slot << 32) | generation);
        }

        internal static ApplicationInstanceHandle FromValue(ulong value) {
            return new ApplicationInstanceHandle(value);
        }

        public static ApplicationInstanceHandle None {
            get { return new ApplicationInstanceHandle(0); }
        }

        public ulong Value { get { return _value; } }
        public uint Slot { get { return (uint)(_value >> 32); } }
        public uint Generation { get { return (uint)_value; } }
        public bool IsValid { get { return _value != 0; } }

        public bool Equals(ApplicationInstanceHandle other) {
            return _value == other._value;
        }

        public override bool Equals(object obj) {
            return obj is ApplicationInstanceHandle &&
                   Equals((ApplicationInstanceHandle)obj);
        }

        public override int GetHashCode() {
            return _value.GetHashCode();
        }

        public static bool operator ==(ApplicationInstanceHandle left,
                                       ApplicationInstanceHandle right) {
            return left._value == right._value;
        }

        public static bool operator !=(ApplicationInstanceHandle left,
                                       ApplicationInstanceHandle right) {
            return left._value != right._value;
        }

        public override string ToString() {
            if (!IsValid) return "none";
            return "instance-" + Slot.ToString() + "-" + Generation.ToString();
        }
    }

    /// <summary>
    /// Immutable, allocation-free application-instance snapshot for observers.
    /// It deliberately exposes no lifecycle or ownership mutation surface.
    /// </summary>
    public readonly struct ApplicationInstanceObservation {
        private readonly ulong _handleValue;
        private readonly string _descriptorId;
        private readonly ApplicationInstanceLifecycleState _lifecycleState;
        private readonly bool _isActivated;
        private readonly bool _isSuspended;
        private readonly int _ownedWindowCount;

        public ApplicationInstanceHandle Handle {
            get { return ApplicationInstanceHandle.FromValue(_handleValue); }
        }
        public string DescriptorId { get { return _descriptorId; } }
        public ApplicationInstanceLifecycleState LifecycleState {
            get { return _lifecycleState; }
        }
        public bool IsActivated { get { return _isActivated; } }
        public bool IsSuspended { get { return _isSuspended; } }
        public int OwnedWindowCount { get { return _ownedWindowCount; } }

        internal ApplicationInstanceObservation(ApplicationInstance instance) {
            _handleValue = instance == null ? 0UL : instance.Handle.Value;
            _descriptorId = instance == null ? null : instance.DescriptorId;
            _lifecycleState = instance == null
                ? ApplicationInstanceLifecycleState.Failed
                : instance.LifecycleState;
            _isActivated = instance != null && instance.IsActivated;
            _isSuspended = instance != null && instance.LifecycleState ==
                ApplicationInstanceLifecycleState.Suspended;
            _ownedWindowCount = instance == null ? 0 : instance.OwnedWindowCount;
        }
    }

    /// <summary>
    /// Semantic application-instance record.  Window objects remain owned by
    /// WindowManager; this record owns only the bounded relationship to them
    /// and the bounded launch context needed by the app model.
    /// </summary>
    public sealed class ApplicationInstance {
        internal const int MaxOwnedWindows = 8;
        private const int MaxRetainedFailureLength = 192;

        private readonly ApplicationInstanceHandle _handle;
        private readonly string _descriptorId;
        private readonly ApplicationInstancePolicy _policy;
        private readonly bool _closeWhenLastWindowClosed;
        private readonly bool _allowZeroWindows;
        private readonly Window[] _ownedWindows;
        private int _ownedWindowCount;
        private LaunchRequest _launchRequest;
        private ApplicationInstanceLifecycleState _state;
        private string _failureReason;
        private string _terminationReason;
        private ApplicationLifecycleAdapter _lifecycleAdapter;
        private bool _lifecycleAdapterAssigned;

        internal ApplicationInstance(ApplicationInstanceHandle handle,
                                     string descriptorId,
                                     ApplicationInstancePolicy policy,
                                     bool closeWhenLastWindowClosed,
                                     bool allowZeroWindows,
                                     LaunchRequest request) {
            _handle = handle;
            _descriptorId = descriptorId;
            _policy = policy;
            _closeWhenLastWindowClosed = closeWhenLastWindowClosed;
            _allowZeroWindows = allowZeroWindows;
            _ownedWindows = new Window[MaxOwnedWindows];
            _launchRequest = request;
            _state = ApplicationInstanceLifecycleState.Registered;
            // The common cooperative default is allocation-free.  Explicit
            // adapters are installed only by representative/custom backends.
            _lifecycleAdapter = null;
            _lifecycleAdapterAssigned = false;
        }

        public ApplicationInstanceHandle Handle { get { return _handle; } }
        public string DescriptorId { get { return _descriptorId; } }
        public string ApplicationId { get { return _descriptorId; } }
        public ApplicationInstancePolicy Policy { get { return _policy; } }
        public bool CloseWhenLastWindowClosed {
            get { return _closeWhenLastWindowClosed; }
        }
        public bool AllowZeroWindows { get { return _allowZeroWindows; } }
        public ApplicationInstanceLifecycleState LifecycleState {
            get { return _state; }
        }
        public string LifecycleStateName {
            get { return ApplicationInstanceLifecycle.Name(_state); }
        }
        public bool IsActive {
            get {
                return _state != ApplicationInstanceLifecycleState.Terminated &&
                       _state != ApplicationInstanceLifecycleState.Failed;
            }
        }
        public bool IsActivated {
            get { return _state == ApplicationInstanceLifecycleState.Activated; }
        }
        public ApplicationLifecycleCapability LifecycleCapability {
            get { return _lifecycleAdapter == null ?
                ApplicationLifecycleCapability.SupportedWithDefault :
                _lifecycleAdapter.Capability; }
        }
        public bool SuspensionSupported {
            get { return LifecycleCapability != ApplicationLifecycleCapability.Unsupported; }
        }
        public LaunchRequest LaunchRequestContext { get { return _launchRequest; } }
        public string Document {
            get { return _launchRequest == null ? null : _launchRequest.Document; }
        }
        public string Verb {
            get { return _launchRequest == null ? null : _launchRequest.Verb; }
        }
        public string SourceShellObjectId {
            get {
                return _launchRequest == null ? null :
                    _launchRequest.SourceShellObjectId;
            }
        }
        public int ArgumentCount {
            get { return _launchRequest == null ? 0 : _launchRequest.ArgumentCount; }
        }
        public string FailureReason { get { return _failureReason; } }
        public string TerminationReason { get { return _terminationReason; } }
        public int OwnedWindowCount { get { return _ownedWindowCount; } }

        public string GetArgument(int index) {
            return _launchRequest == null ? null :
                (index >= 0 && index < _launchRequest.ArgumentCount
                    ? _launchRequest.GetArgument(index) : null);
        }

        public int GetOwnedWindowOwnerId(int index) {
            if (index < 0 || index >= _ownedWindowCount ||
                _ownedWindows[index] == null) return 0;
            return _ownedWindows[index].OwnerId;
        }

        internal Window GetOwnedWindowAt(int index) {
            return index >= 0 && index < _ownedWindowCount
                ? _ownedWindows[index] : null;
        }

        internal void UpdateLaunchRequest(LaunchRequest request) {
            _launchRequest = request;
            _failureReason = null;
            _terminationReason = null;
        }

        internal ApplicationLifecycleAdapter LifecycleAdapter {
            get { return _lifecycleAdapter; }
        }

        internal void SetLifecycleAdapter(ApplicationLifecycleAdapter adapter) {
            if (_lifecycleAdapterAssigned || adapter == null) return;
            _lifecycleAdapter = adapter;
            _lifecycleAdapterAssigned = true;
        }

        internal bool TryTransition(ApplicationInstanceLifecycleState next) {
            if (!IsValidTransition(_state, next)) return false;
            ApplicationInstanceLifecycleState previous = _state;
            _state = next;
            ApplicationServiceRegistry.OnApplicationLifecycleChanged(
                this, previous, next);
            return true;
        }

        internal void RecordFailure(string diagnostic) {
            _failureReason = BoundDiagnostic(diagnostic);
        }

        internal void RecordTermination(string reason) {
            _terminationReason = BoundDiagnostic(reason);
        }

        internal bool AttachWindow(Window window) {
            if (window == null) return false;
            if (window.ApplicationInstanceHandle == _handle) return true;
            if (window.ApplicationInstanceHandle.IsValid) return false;
            if (_ownedWindowCount >= MaxOwnedWindows) return false;
            for (int i = 0; i < _ownedWindowCount; i++) {
                if (_ownedWindows[i] == window) return true;
            }
            _ownedWindows[_ownedWindowCount++] = window;
            window.SetApplicationInstance(_handle);
            return true;
        }

        internal bool OwnsWindow(Window window) {
            if (window == null) return false;
            for (int i = 0; i < _ownedWindowCount; i++) {
                if (_ownedWindows[i] == window) return true;
            }
            return false;
        }

        internal bool DetachWindow(Window window) {
            if (window == null) return false;
            int found = -1;
            for (int i = 0; i < _ownedWindowCount; i++) {
                if (_ownedWindows[i] == window) {
                    found = i;
                    break;
                }
            }
            if (found < 0) return false;
            for (int i = found; i < _ownedWindowCount - 1; i++) {
                _ownedWindows[i] = _ownedWindows[i + 1];
            }
            _ownedWindows[--_ownedWindowCount] = null;
            if (window.ApplicationInstanceHandle == _handle) {
                window.ClearApplicationInstance();
            }
            return true;
        }

        internal void ClearOwnedWindows() {
            for (int i = 0; i < _ownedWindowCount; i++) {
                Window window = _ownedWindows[i];
                if (window != null && window.ApplicationInstanceHandle == _handle) {
                    window.ClearApplicationInstance();
                }
                _ownedWindows[i] = null;
            }
            _ownedWindowCount = 0;
        }

        private static bool IsValidTransition(
            ApplicationInstanceLifecycleState from,
            ApplicationInstanceLifecycleState to) {
            if (from == ApplicationInstanceLifecycleState.Terminated ||
                from == ApplicationInstanceLifecycleState.Failed) return false;

            switch (from) {
                case ApplicationInstanceLifecycleState.Registered:
                    return to == ApplicationInstanceLifecycleState.Loading ||
                           to == ApplicationInstanceLifecycleState.Failed;
                case ApplicationInstanceLifecycleState.Loading:
                    return to == ApplicationInstanceLifecycleState.Initialized ||
                           to == ApplicationInstanceLifecycleState.Inactive ||
                           to == ApplicationInstanceLifecycleState.Closing ||
                           to == ApplicationInstanceLifecycleState.Failed;
                case ApplicationInstanceLifecycleState.Initialized:
                    return to == ApplicationInstanceLifecycleState.Running ||
                           to == ApplicationInstanceLifecycleState.Closing ||
                           to == ApplicationInstanceLifecycleState.Failed;
                case ApplicationInstanceLifecycleState.Running:
                    return to == ApplicationInstanceLifecycleState.Activated ||
                           to == ApplicationInstanceLifecycleState.Inactive ||
                           to == ApplicationInstanceLifecycleState.Suspended ||
                           to == ApplicationInstanceLifecycleState.Closing ||
                           to == ApplicationInstanceLifecycleState.Failed;
                case ApplicationInstanceLifecycleState.Activated:
                    return to == ApplicationInstanceLifecycleState.Inactive ||
                           to == ApplicationInstanceLifecycleState.Closing ||
                           to == ApplicationInstanceLifecycleState.Failed;
                case ApplicationInstanceLifecycleState.Inactive:
                    return to == ApplicationInstanceLifecycleState.Loading ||
                           to == ApplicationInstanceLifecycleState.Running ||
                           to == ApplicationInstanceLifecycleState.Suspended ||
                           to == ApplicationInstanceLifecycleState.Activated ||
                           to == ApplicationInstanceLifecycleState.Closing ||
                           to == ApplicationInstanceLifecycleState.Failed;
                case ApplicationInstanceLifecycleState.Suspended:
                    return to == ApplicationInstanceLifecycleState.Activated ||
                           to == ApplicationInstanceLifecycleState.Inactive ||
                           to == ApplicationInstanceLifecycleState.Closing ||
                           to == ApplicationInstanceLifecycleState.Failed;
                case ApplicationInstanceLifecycleState.Closing:
                    return to == ApplicationInstanceLifecycleState.Terminated ||
                           to == ApplicationInstanceLifecycleState.Failed;
                default:
                    return false;
            }
        }

        private static string BoundDiagnostic(string diagnostic) {
            if (string.IsNullOrEmpty(diagnostic) ||
                diagnostic.Length <= MaxRetainedFailureLength) return diagnostic;
            return diagnostic.Substring(0, MaxRetainedFailureLength);
        }
    }

    /// <summary>
    /// Bounded application-instance registry.  Slots are removed on terminal
    /// cleanup; generation values make stale window/taskbar references safe.
    /// This is App Model state only and is not a process manager or scheduler.
    /// </summary>
    public static class ApplicationInstanceRegistry {
        public const int Capacity = 32;

        private static ApplicationInstance[] _instances;
        private static uint[] _generations;
        private static bool[] _used;
        private static int _activeCount;

        private static int _createdCount;
        private static int _reusedCount;
        private static int _activatedCount;
        private static int _deactivatedCount;
        private static int _suspendedCount;
        private static int _resumedCount;
        private static int _closeRequestCount;
        private static int _closeCancellationCount;
        private static int _lifecycleFailureCount;
        private static int _invalidLifecycleRequestCount;
        private static int _staleLifecycleHandleCount;
        private static int _terminatedCount;
        private static int _failedCount;
        private static int _windowAttachCount;
        private static int _windowDetachCount;
        private static int _duplicateAttachCount;
        private static int _staleOwnershipCount;
        private static ApplicationInstanceHandle _activeApplicationHandle;
        private static bool _routingForeground;
        private static bool _lastTaskbarGroupingRuntimeCleanup;

        public static void Initialize() {
            if (_instances != null) return;
            _instances = new ApplicationInstance[Capacity];
            _generations = new uint[Capacity];
            _used = new bool[Capacity];
        }

        public static int ActiveCount {
            get { Initialize(); return _activeCount; }
        }
        public static int InstancesCreated { get { return _createdCount; } }
        public static int InstancesReused { get { return _reusedCount; } }
        public static int InstancesActivated { get { return _activatedCount; } }
        public static int InstancesDeactivated { get { return _deactivatedCount; } }
        public static int InstancesSuspended { get { return _suspendedCount; } }
        public static int InstancesResumed { get { return _resumedCount; } }
        public static int CloseRequests { get { return _closeRequestCount; } }
        public static int CloseCancellations { get { return _closeCancellationCount; } }
        public static int LifecycleFailures { get { return _lifecycleFailureCount; } }
        public static int InvalidLifecycleRequests { get { return _invalidLifecycleRequestCount; } }
        public static int StaleLifecycleHandles { get { return _staleLifecycleHandleCount; } }
        public static int InstancesTerminated { get { return _terminatedCount; } }
        public static int FailedInstances { get { return _failedCount; } }
        public static int WindowAttachCount { get { return _windowAttachCount; } }
        public static int WindowDetachCount { get { return _windowDetachCount; } }
        public static int DuplicateWindowAttachCount {
            get { return _duplicateAttachCount; }
        }
        public static int StaleOwnershipCount {
            get { return _staleOwnershipCount; }
        }
        public static ApplicationInstanceHandle ActiveApplicationHandle {
            get { return _activeApplicationHandle; }
        }
        public static bool LastTaskbarGroupingRuntimeCleanup {
            get { return _lastTaskbarGroupingRuntimeCleanup; }
        }
        public static int ObservationCount {
            get {
                Initialize();
                int count = 0;
                for (int i = 0; i < Capacity; i++) {
                    if (_used[i] && _instances[i] != null) count++;
                }
                return count;
            }
        }

        public static bool TryGetObservationAt(
                int ordinal, out ApplicationInstanceObservation observation) {
            Initialize();
            observation = default(ApplicationInstanceObservation);
            if (ordinal < 0) return false;
            for (int i = 0; i < Capacity; i++) {
                if (!_used[i] || _instances[i] == null) continue;
                if (ordinal-- != 0) continue;
                observation = new ApplicationInstanceObservation(_instances[i]);
                return true;
            }
            return false;
        }
        public static int SuspendedCount {
            get {
                Initialize();
                int count = 0;
                for (int i = 0; i < Capacity; i++) {
                    if (_used[i] && _instances[i] != null &&
                        _instances[i].LifecycleState ==
                            ApplicationInstanceLifecycleState.Suspended) count++;
                }
                return count;
            }
        }

        public static int RunningCount {
            get {
                Initialize();
                int count = 0;
                for (int i = 0; i < Capacity; i++) {
                    if (!_used[i] || _instances[i] == null) continue;
                    ApplicationInstanceLifecycleState state =
                        _instances[i].LifecycleState;
                    if (state == ApplicationInstanceLifecycleState.Running ||
                        state == ApplicationInstanceLifecycleState.Activated) count++;
                }
                return count;
            }
        }

        public static ApplicationInstance GetAt(int index) {
            Initialize();
            return index >= 0 && index < Capacity && _used[index]
                ? _instances[index] : null;
        }

        public static bool TryGet(ApplicationInstanceHandle handle,
                                  out ApplicationInstance instance) {
            Initialize();
            instance = null;
            int slot = (int)handle.Slot - 1;
            if (!handle.IsValid || slot < 0 || slot >= Capacity ||
                !_used[slot] || _generations[slot] != handle.Generation) return false;
            instance = _instances[slot];
            return instance != null;
        }

        public static ApplicationInstance FindByDescriptor(string descriptorId) {
            Initialize();
            if (string.IsNullOrEmpty(descriptorId)) return null;
            for (int i = 0; i < Capacity; i++) {
                if (!_used[i] || _instances[i] == null) continue;
                ApplicationInstance candidate = _instances[i];
                if (candidate.IsActive && TextEquals(candidate.DescriptorId,
                                                     descriptorId)) return candidate;
            }
            return null;
        }

        public static int CountByDescriptor(string descriptorId) {
            Initialize();
            if (string.IsNullOrEmpty(descriptorId)) return 0;
            int count = 0;
            for (int i = 0; i < Capacity; i++) {
                if (!_used[i] || _instances[i] == null) continue;
                if (_instances[i].IsActive && TextEquals(_instances[i].DescriptorId,
                                                         descriptorId)) count++;
            }
            return count;
        }

        public static ApplicationInstance GetByDescriptorAt(string descriptorId,
                                                             int index) {
            Initialize();
            if (string.IsNullOrEmpty(descriptorId) || index < 0) return null;
            for (int i = 0; i < Capacity; i++) {
                if (!_used[i] || _instances[i] == null) continue;
                if (!_instances[i].IsActive || !TextEquals(
                        _instances[i].DescriptorId, descriptorId)) continue;
                if (index-- == 0) return _instances[i];
            }
            return null;
        }

        public static bool TryBeginLaunch(ApplicationDescriptor descriptor,
                                          LaunchRequest request,
                                          out ApplicationInstance instance,
                                          out bool reused,
                                          out LaunchResult failure) {
            instance = null;
            reused = false;
            failure = null;
            if (descriptor == null) {
                failure = LaunchResult.Failed(LaunchErrorCode.NotFound,
                    "Application descriptor is unavailable", null);
                return false;
            }
            ApplicationInstancePolicy policy = descriptor.ShellPolicy == null
                ? ApplicationInstancePolicy.MultiInstance
                : descriptor.ShellPolicy.InstancePolicy;
            bool closeWhenLastWindowClosed = descriptor.ShellPolicy == null
                ? policy == ApplicationInstancePolicy.MultiInstance
                : descriptor.ShellPolicy.CloseWhenLastWindowClosed;
            bool allowZeroWindows = descriptor.ShellPolicy != null &&
                descriptor.ShellPolicy.AllowZeroWindows;
            return TryBeginLaunch(descriptor.AppId, policy,
                closeWhenLastWindowClosed, allowZeroWindows, request,
                out instance, out reused, out failure);
        }

        internal static bool TryBeginLaunch(string descriptorId,
                                             ApplicationInstancePolicy policy,
                                             LaunchRequest request,
                                             out ApplicationInstance instance,
                                             out bool reused,
                                             out LaunchResult failure) {
            return TryBeginLaunch(descriptorId, policy,
                policy == ApplicationInstancePolicy.MultiInstance,
                policy != ApplicationInstancePolicy.MultiInstance,
                request, out instance, out reused, out failure);
        }

        private static bool TryBeginLaunch(string descriptorId,
                                             ApplicationInstancePolicy policy,
                                             bool closeWhenLastWindowClosed,
                                             bool allowZeroWindows,
                                             LaunchRequest request,
                                             out ApplicationInstance instance,
                                             out bool reused,
                                             out LaunchResult failure) {
            Initialize();
            instance = null;
            reused = false;
            failure = null;
            if (string.IsNullOrEmpty(descriptorId)) {
                failure = LaunchResult.Failed(LaunchErrorCode.MalformedRequest,
                    "Application identity is missing", null);
                return false;
            }
            if (request == null || !request.IsValid) {
                failure = LaunchResult.Failed(LaunchErrorCode.MalformedRequest,
                    request == null ? "Launch request is null" : request.ValidationError,
                    descriptorId);
                return false;
            }

            if (policy != ApplicationInstancePolicy.MultiInstance) {
                ApplicationInstance existing = FindByDescriptor(descriptorId);
                if (existing != null) {
                    if (existing.LifecycleState ==
                            ApplicationInstanceLifecycleState.Closing ||
                        existing.LifecycleState ==
                            ApplicationInstanceLifecycleState.Loading) {
                        failure = LaunchResult.Failed(LaunchErrorCode.ActivationFailed,
                            "Application instance is busy", descriptorId);
                        return false;
                    }
                    existing.UpdateLaunchRequest(request);
                    if (existing.LifecycleState ==
                            ApplicationInstanceLifecycleState.Inactive &&
                        !existing.TryTransition(
                            ApplicationInstanceLifecycleState.Loading)) {
                        failure = LaunchResult.Failed(LaunchErrorCode.ActivationFailed,
                            "Application instance cannot be reactivated", descriptorId);
                        return false;
                    }
                    instance = existing;
                    reused = true;
                    _reusedCount++;
                    return true;
                }
            }

            int free = -1;
            for (int i = 0; i < Capacity; i++) {
                if (!_used[i]) {
                    free = i;
                    break;
                }
            }
            if (free < 0) {
                failure = LaunchResult.Failed(LaunchErrorCode.ResourceUnavailable,
                    "Application instance capacity exhausted", descriptorId);
                return false;
            }

            uint generation = _generations[free] + 1;
            if (generation == 0) generation = 1;
            _generations[free] = generation;
            ApplicationInstanceHandle handle =
                ApplicationInstanceHandle.Create(free + 1, generation);
            ApplicationInstance created = new ApplicationInstance(handle,
                descriptorId, policy, closeWhenLastWindowClosed,
                allowZeroWindows, request);
            if (!created.TryTransition(ApplicationInstanceLifecycleState.Loading)) {
                failure = LaunchResult.Failed(LaunchErrorCode.InitializationFailed,
                    "Application instance could not enter loading", descriptorId);
                return false;
            }
            _instances[free] = created;
            _used[free] = true;
            _activeCount++;
            _createdCount++;
            instance = created;
            return true;
        }

        internal static bool TryBeginDescriptorLaunch(string appId,
                                                       LaunchRequest request,
                                                       out ApplicationInstance instance,
                                                       out bool reused,
                                                       out LaunchResult failure) {
            ApplicationDescriptor descriptor;
            if (!ApplicationDescriptorRegistry.TryGetById(appId, out descriptor)) {
                instance = null;
                reused = false;
                failure = LaunchResult.Failed(LaunchErrorCode.NotFound,
                    "Application descriptor is unavailable", appId);
                return false;
            }
            return TryBeginLaunch(descriptor, request, out instance,
                out reused, out failure);
        }

        internal static bool TryBeginGxmLaunch(LaunchRequest request,
                                                out ApplicationInstance instance,
                                                out bool reused,
                                                out LaunchResult failure) {
            bool started = TryBeginLaunch("gxos.external.gxm",
                ApplicationInstancePolicy.MultiInstance, request,
                out instance, out reused, out failure);
            if (started && instance != null && !reused) {
                instance.SetLifecycleAdapter(new GxmApplicationLifecycleAdapter());
            }
            return started;
        }

        internal static bool TryCompleteLaunch(ApplicationInstance instance,
                                                bool activate,
                                                out LaunchResult failure) {
            failure = null;
            if (!IsRegisteredInstance(instance)) {
                failure = LaunchResult.Failed(LaunchErrorCode.AlreadyTerminated,
                    "Application instance is not active",
                    instance == null ? null : instance.DescriptorId);
                return false;
            }

            ApplicationInstanceLifecycleState state = instance.LifecycleState;
            if (state == ApplicationInstanceLifecycleState.Suspended) {
                ApplicationLifecycleResult resumed = Resume(instance.Handle);
                if (!resumed.Success) {
                    failure = LaunchResult.Failed(
                        LaunchErrorCode.ActivationFailed,
                        resumed.Diagnostic ?? "Application resume failed",
                        instance.DescriptorId);
                    return false;
                }
                state = instance.LifecycleState;
            }
            if (state == ApplicationInstanceLifecycleState.Loading) {
                if (!instance.TryTransition(ApplicationInstanceLifecycleState.Initialized) ||
                    !instance.TryTransition(ApplicationInstanceLifecycleState.Running)) {
                    failure = LaunchResult.Failed(LaunchErrorCode.InitializationFailed,
                        "Application instance initialization failed", instance.DescriptorId);
                    return false;
                }
            } else if (state != ApplicationInstanceLifecycleState.Running &&
                       state != ApplicationInstanceLifecycleState.Activated) {
                failure = LaunchResult.Failed(LaunchErrorCode.InitializationFailed,
                    "Application instance is not launchable", instance.DescriptorId);
                return false;
            }

            if (activate) {
                ApplicationLifecycleResult activated = Activate(instance.Handle);
                if (!activated.Success) {
                    failure = LaunchResult.Failed(LaunchErrorCode.ActivationFailed,
                        activated.Diagnostic ?? "Application instance activation failed",
                        instance.DescriptorId);
                    return false;
                }
                // Launch presentation is explicit.  Instance activation itself
                // remains lifecycle-only so a requested Window focus cannot be
                // preceded by an arbitrary owned-Window z-order move.
                FocusDefaultWindow(instance);
            }
            return true;
        }

        public static ApplicationLifecycleResult Activate(
                ApplicationInstanceHandle handle) {
            ApplicationInstance instance;
            if (!TryGet(handle, out instance)) {
                _staleLifecycleHandleCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound, handle,
                    ApplicationInstanceLifecycleState.Terminated,
                    ApplicationCloseReason.ApplicationRequest,
                    "Application instance handle is stale or unavailable");
            }
            return Activate(instance);
        }

        private static ApplicationLifecycleResult Activate(
                ApplicationInstance instance) {
            if (!IsRegisteredInstance(instance)) {
                _staleLifecycleHandleCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound,
                    instance == null ? ApplicationInstanceHandle.None : instance.Handle,
                    ApplicationInstanceLifecycleState.Terminated,
                    ApplicationCloseReason.ApplicationRequest,
                    "Application instance handle is stale or unavailable");
            }

            if (instance.LifecycleState == ApplicationInstanceLifecycleState.Suspended) {
                ApplicationLifecycleResult resumed = Resume(instance.Handle);
                if (!resumed.Success) return resumed;
            }
            ApplicationInstanceLifecycleState state = instance.LifecycleState;
            if (state != ApplicationInstanceLifecycleState.Inactive &&
                    state != ApplicationInstanceLifecycleState.Running &&
                    state != ApplicationInstanceLifecycleState.Activated) {
                return InvalidLifecycle(instance,
                    "Application instance is not activatable");
            }
            if (state == ApplicationInstanceLifecycleState.Activated &&
                    _activeApplicationHandle == instance.Handle) {
                return ApplicationLifecycleResult.Succeeded(instance.Handle,
                    state, state, ApplicationCloseReason.ApplicationRequest);
            }

            if (_activeApplicationHandle.IsValid &&
                    _activeApplicationHandle != instance.Handle) {
                ApplicationInstance previous;
                if (TryGet(_activeApplicationHandle, out previous)) {
                    ApplicationLifecycleResult deactivated = Deactivate(previous);
                    if (!deactivated.Success) return deactivated;
                } else {
                    _staleLifecycleHandleCount++;
                    _activeApplicationHandle = ApplicationInstanceHandle.None;
                }
            }

            string diagnostic;
            ApplicationLifecycleCallbackResult callback = InvokeCallback(
                instance, ApplicationLifecycleOperation.Activate,
                ApplicationCloseReason.ApplicationRequest, out diagnostic);
            if (callback != ApplicationLifecycleCallbackResult.Success) {
                return CallbackResult(instance, ApplicationLifecycleOperation.Activate,
                    callback, diagnostic);
            }

            ApplicationInstanceLifecycleState from = instance.LifecycleState;
            if (instance.LifecycleState == ApplicationInstanceLifecycleState.Inactive &&
                    !instance.TryTransition(ApplicationInstanceLifecycleState.Running)) {
                return InvalidLifecycle(instance,
                    "Inactive application instance cannot run");
            }
            if (instance.LifecycleState == ApplicationInstanceLifecycleState.Running &&
                    !instance.TryTransition(ApplicationInstanceLifecycleState.Activated)) {
                return InvalidLifecycle(instance,
                    "Application instance activation failed");
            }
            if (instance.LifecycleState != ApplicationInstanceLifecycleState.Activated) {
                return InvalidLifecycle(instance,
                    "Application instance did not enter Activated state");
            }
            _activeApplicationHandle = instance.Handle;
            if (from != ApplicationInstanceLifecycleState.Activated) _activatedCount++;
            return ApplicationLifecycleResult.Succeeded(instance.Handle, from,
                ApplicationInstanceLifecycleState.Activated,
                ApplicationCloseReason.ApplicationRequest);
        }

        public static ApplicationLifecycleResult Deactivate(
                ApplicationInstanceHandle handle) {
            ApplicationInstance instance;
            if (!TryGet(handle, out instance)) {
                _staleLifecycleHandleCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound, handle,
                    ApplicationInstanceLifecycleState.Terminated,
                    ApplicationCloseReason.ApplicationRequest,
                    "Application instance handle is stale or unavailable");
            }
            return Deactivate(instance);
        }

        private static ApplicationLifecycleResult Deactivate(
                ApplicationInstance instance) {
            if (!IsRegisteredInstance(instance)) {
                _staleLifecycleHandleCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound,
                    instance == null ? ApplicationInstanceHandle.None : instance.Handle,
                    ApplicationInstanceLifecycleState.Terminated,
                    ApplicationCloseReason.ApplicationRequest,
                    "Application instance handle is stale or unavailable");
            }
            ApplicationInstanceLifecycleState state = instance.LifecycleState;
            if (state == ApplicationInstanceLifecycleState.Inactive ||
                    state == ApplicationInstanceLifecycleState.Suspended ||
                    state == ApplicationInstanceLifecycleState.Running) {
                if (_activeApplicationHandle == instance.Handle) {
                    _activeApplicationHandle = ApplicationInstanceHandle.None;
                }
                return ApplicationLifecycleResult.Succeeded(instance.Handle,
                    state, state, ApplicationCloseReason.ApplicationRequest);
            }
            if (state != ApplicationInstanceLifecycleState.Activated) {
                return InvalidLifecycle(instance,
                    "Application instance is not deactivatable");
            }
            string diagnostic;
            ApplicationLifecycleCallbackResult callback = InvokeCallback(
                instance, ApplicationLifecycleOperation.Deactivate,
                ApplicationCloseReason.ApplicationRequest, out diagnostic);
            if (callback != ApplicationLifecycleCallbackResult.Success) {
                return CallbackResult(instance, ApplicationLifecycleOperation.Deactivate,
                    callback, diagnostic);
            }
            if (!instance.TryTransition(ApplicationInstanceLifecycleState.Inactive)) {
                return InvalidLifecycle(instance,
                    "Application instance could not become inactive");
            }
            if (_activeApplicationHandle == instance.Handle) {
                _activeApplicationHandle = ApplicationInstanceHandle.None;
            }
            _deactivatedCount++;
            return ApplicationLifecycleResult.Succeeded(instance.Handle, state,
                ApplicationInstanceLifecycleState.Inactive,
                ApplicationCloseReason.ApplicationRequest);
        }

        public static ApplicationLifecycleResult Suspend(
                ApplicationInstanceHandle handle) {
            ApplicationInstance instance;
            if (!TryGet(handle, out instance)) {
                _staleLifecycleHandleCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound, handle,
                    ApplicationInstanceLifecycleState.Terminated,
                    ApplicationCloseReason.ApplicationRequest,
                    "Application instance handle is stale or unavailable");
            }
            if (instance.LifecycleCapability ==
                    ApplicationLifecycleCapability.Unsupported) {
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.Unsupported, handle,
                    instance.LifecycleState, ApplicationCloseReason.ApplicationRequest,
                    "Application backend does not support cooperative suspension");
            }
            if (instance.LifecycleState == ApplicationInstanceLifecycleState.Suspended) {
                return ApplicationLifecycleResult.Succeeded(handle,
                    ApplicationInstanceLifecycleState.Suspended,
                    ApplicationInstanceLifecycleState.Suspended,
                    ApplicationCloseReason.ApplicationRequest);
            }
            if (instance.LifecycleState == ApplicationInstanceLifecycleState.Activated) {
                ApplicationLifecycleResult deactivated = Deactivate(instance);
                if (!deactivated.Success) return deactivated;
            }
            ApplicationInstanceLifecycleState state = instance.LifecycleState;
            if (state != ApplicationInstanceLifecycleState.Inactive &&
                    state != ApplicationInstanceLifecycleState.Running) {
                return InvalidLifecycle(instance,
                    "Application instance is not suspendable in its current state");
            }
            string diagnostic;
            ApplicationLifecycleCallbackResult callback = InvokeCallback(
                instance, ApplicationLifecycleOperation.Suspend,
                ApplicationCloseReason.ApplicationRequest, out diagnostic);
            if (callback != ApplicationLifecycleCallbackResult.Success) {
                return CallbackResult(instance, ApplicationLifecycleOperation.Suspend,
                    callback, diagnostic);
            }
            if (!instance.TryTransition(ApplicationInstanceLifecycleState.Suspended)) {
                return InvalidLifecycle(instance,
                    "Application instance could not enter Suspended state");
            }
            if (_activeApplicationHandle == instance.Handle) {
                _activeApplicationHandle = ApplicationInstanceHandle.None;
            }
            _suspendedCount++;
            return ApplicationLifecycleResult.Succeeded(handle, state,
                ApplicationInstanceLifecycleState.Suspended,
                ApplicationCloseReason.ApplicationRequest);
        }

        public static ApplicationLifecycleResult Resume(
                ApplicationInstanceHandle handle) {
            ApplicationInstance instance;
            if (!TryGet(handle, out instance)) {
                _staleLifecycleHandleCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound, handle,
                    ApplicationInstanceLifecycleState.Terminated,
                    ApplicationCloseReason.ApplicationRequest,
                    "Application instance handle is stale or unavailable");
            }
            return Resume(instance);
        }

        private static ApplicationLifecycleResult Resume(ApplicationInstance instance) {
            if (!IsRegisteredInstance(instance)) {
                _staleLifecycleHandleCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound,
                    instance == null ? ApplicationInstanceHandle.None : instance.Handle,
                    ApplicationInstanceLifecycleState.Terminated,
                    ApplicationCloseReason.ApplicationRequest,
                    "Application instance handle is stale or unavailable");
            }
            ApplicationInstanceLifecycleState state = instance.LifecycleState;
            if (state != ApplicationInstanceLifecycleState.Suspended) {
                return InvalidLifecycle(instance,
                    "Application instance is not suspended");
            }
            if (instance.LifecycleCapability ==
                    ApplicationLifecycleCapability.Unsupported) {
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.Unsupported, instance.Handle,
                    state, ApplicationCloseReason.ApplicationRequest,
                    "Application backend does not support cooperative resume");
            }
            string diagnostic;
            ApplicationLifecycleCallbackResult callback = InvokeCallback(
                instance, ApplicationLifecycleOperation.Resume,
                ApplicationCloseReason.ApplicationRequest, out diagnostic);
            if (callback != ApplicationLifecycleCallbackResult.Success) {
                return CallbackResult(instance, ApplicationLifecycleOperation.Resume,
                    callback, diagnostic);
            }
            if (!instance.TryTransition(ApplicationInstanceLifecycleState.Inactive)) {
                return InvalidLifecycle(instance,
                    "Suspended application instance could not resume");
            }
            _resumedCount++;
            return ApplicationLifecycleResult.Succeeded(instance.Handle, state,
                ApplicationInstanceLifecycleState.Inactive,
                ApplicationCloseReason.ApplicationRequest);
        }

        public static ApplicationLifecycleResult RequestClose(
                ApplicationInstanceHandle handle, ApplicationCloseReason reason) {
            ApplicationInstance instance;
            if (!TryGet(handle, out instance)) {
                _staleLifecycleHandleCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound, handle,
                    ApplicationInstanceLifecycleState.Terminated, reason,
                    "Application instance handle is stale or unavailable");
            }
            if (instance.LifecycleState == ApplicationInstanceLifecycleState.Closing) {
                return InvalidLifecycle(instance,
                    "Application instance is already closing");
            }
            _closeRequestCount++;
            string diagnostic;
            ApplicationLifecycleCallbackResult callback = InvokeCallback(instance,
                ApplicationLifecycleOperation.RequestClose, reason, out diagnostic);
            if (callback == ApplicationLifecycleCallbackResult.Cancelled) {
                _closeCancellationCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.Cancelled, handle,
                    instance.LifecycleState, reason,
                    diagnostic ?? "Application close was cancelled");
            }
            if (callback != ApplicationLifecycleCallbackResult.Success) {
                return CallbackResult(instance,
                    ApplicationLifecycleOperation.RequestClose, callback, diagnostic);
            }
            return Terminate(instance, reason, false);
        }

        public static ApplicationLifecycleResult Terminate(
                ApplicationInstanceHandle handle, ApplicationCloseReason reason) {
            ApplicationInstance instance;
            if (!TryGet(handle, out instance)) {
                _staleLifecycleHandleCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound, handle,
                    ApplicationInstanceLifecycleState.Terminated, reason,
                    "Application instance handle is stale or unavailable");
            }
            return Terminate(instance, reason, true);
        }

        private static ApplicationLifecycleResult Terminate(
                ApplicationInstance instance, ApplicationCloseReason reason,
                bool forced) {
            if (!IsRegisteredInstance(instance)) {
                _staleLifecycleHandleCount++;
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound,
                    instance == null ? ApplicationInstanceHandle.None : instance.Handle,
                    ApplicationInstanceLifecycleState.Terminated, reason,
                    "Application instance handle is stale or unavailable");
            }
            ApplicationInstanceLifecycleState from = instance.LifecycleState;
            if (from == ApplicationInstanceLifecycleState.Closing) {
                from = ApplicationInstanceLifecycleState.Closing;
            } else if (!instance.TryTransition(ApplicationInstanceLifecycleState.Closing)) {
                return InvalidLifecycle(instance,
                    "Application instance cannot enter Closing state");
            }

            string diagnostic;
            ApplicationLifecycleCallbackResult callback = InvokeCallback(instance,
                ApplicationLifecycleOperation.Terminate, reason, out diagnostic);
            bool callbackFailed = callback == ApplicationLifecycleCallbackResult.Failed ||
                callback == ApplicationLifecycleCallbackResult.Cancelled ||
                callback == ApplicationLifecycleCallbackResult.Unsupported;
            if (callbackFailed) {
                RecordLifecycleFailure(instance,
                    ApplicationLifecycleOperation.Terminate,
                    diagnostic ?? "Application termination callback failed");
                if (!forced) {
                    // A close request failure remains observable, but cleanup
                    // is still deterministic once Closing has been entered.
                }
            }
            instance.RecordTermination(reason.ToString());
            CloseAndDetachOwnedWindows(instance, reason.ToString());
            bool terminated = instance.TryTransition(
                ApplicationInstanceLifecycleState.Terminated);
            if (!terminated) {
                // The cleanup below still removes the handle, so it cannot be
                // reused accidentally even if a callback corrupted state.
                instance.RecordFailure("Application instance termination transition failed");
            }
            if (_activeApplicationHandle == instance.Handle) {
                _activeApplicationHandle = ApplicationInstanceHandle.None;
            }
            _terminatedCount++;
            Remove(instance);
            if (callbackFailed) {
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.CallbackFailed, instance.Handle,
                    from, reason, diagnostic ?? "Application termination callback failed");
            }
            return ApplicationLifecycleResult.Succeeded(instance.Handle, from,
                ApplicationInstanceLifecycleState.Terminated, reason);
        }

        private static ApplicationLifecycleResult InvalidLifecycle(
                ApplicationInstance instance, string diagnostic) {
            _invalidLifecycleRequestCount++;
            return ApplicationLifecycleResult.Failed(
                ApplicationLifecycleResultCode.InvalidState,
                instance == null ? ApplicationInstanceHandle.None : instance.Handle,
                instance == null ? ApplicationInstanceLifecycleState.Failed :
                    instance.LifecycleState,
                ApplicationCloseReason.ApplicationRequest, diagnostic);
        }

        private static ApplicationLifecycleResult CallbackResult(
                ApplicationInstance instance,
                ApplicationLifecycleOperation operation,
                ApplicationLifecycleCallbackResult callback,
                string diagnostic) {
            if (callback == ApplicationLifecycleCallbackResult.Unsupported) {
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.Unsupported, instance.Handle,
                    instance.LifecycleState, ApplicationCloseReason.ApplicationRequest,
                    diagnostic ?? "Lifecycle operation is unsupported");
            }
            if (callback == ApplicationLifecycleCallbackResult.Cancelled) {
                return ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.Cancelled, instance.Handle,
                    instance.LifecycleState, ApplicationCloseReason.ApplicationRequest,
                    diagnostic ?? "Lifecycle operation was cancelled");
            }
            RecordLifecycleFailure(instance, operation,
                diagnostic ?? "Lifecycle callback failed");
            return ApplicationLifecycleResult.Failed(
                ApplicationLifecycleResultCode.CallbackFailed, instance.Handle,
                instance.LifecycleState, ApplicationCloseReason.ApplicationRequest,
                diagnostic ?? "Lifecycle callback failed");
        }

        private static ApplicationLifecycleCallbackResult InvokeCallback(
                ApplicationInstance instance,
                ApplicationLifecycleOperation operation,
                ApplicationCloseReason reason,
                out string diagnostic) {
            diagnostic = null;
            if (instance == null) {
                diagnostic = "Lifecycle instance is unavailable";
                return ApplicationLifecycleCallbackResult.Failed;
            }
            if (instance.LifecycleAdapter == null)
                return ApplicationLifecycleCallbackResult.Success;
            try {
                ApplicationLifecycleContext context =
                    new ApplicationLifecycleContext(instance, operation, reason);
                switch (operation) {
                    case ApplicationLifecycleOperation.Activate:
                        return instance.LifecycleAdapter.OnActivating(context);
                    case ApplicationLifecycleOperation.Deactivate:
                        return instance.LifecycleAdapter.OnDeactivating(context);
                    case ApplicationLifecycleOperation.Suspend:
                        return instance.LifecycleAdapter.OnSuspending(context);
                    case ApplicationLifecycleOperation.Resume:
                        return instance.LifecycleAdapter.OnResuming(context);
                    case ApplicationLifecycleOperation.RequestClose:
                        return instance.LifecycleAdapter.OnCloseRequested(context, reason);
                    case ApplicationLifecycleOperation.Terminate:
                        return instance.LifecycleAdapter.OnTerminating(context, reason);
                    default:
                        diagnostic = "Unknown lifecycle operation";
                        return ApplicationLifecycleCallbackResult.Failed;
                }
            } catch {
                diagnostic = "Lifecycle callback raised an exception";
                return ApplicationLifecycleCallbackResult.Failed;
            }
        }

        private static void RecordLifecycleFailure(ApplicationInstance instance,
                                                   ApplicationLifecycleOperation operation,
                                                   string diagnostic) {
            _lifecycleFailureCount++;
            if (instance == null) return;
            instance.RecordFailure(diagnostic);
            try {
                instance.LifecycleAdapter.OnLifecycleFailure(
                    new ApplicationLifecycleContext(instance, operation,
                        ApplicationCloseReason.Failure), operation, diagnostic);
            } catch { }
        }

        internal static void FocusDefaultWindow(ApplicationInstance instance) {
            if (instance == null || _routingForeground) return;
            Window target = null;
            for (int i = 0; i < instance.OwnedWindowCount; i++) {
                Window candidate = instance.GetOwnedWindowAt(i);
                if (candidate != null && candidate.Visible) target = candidate;
            }
            if (target == null && instance.OwnedWindowCount > 0) {
                target = instance.GetOwnedWindowAt(instance.OwnedWindowCount - 1);
            }
            if (target == null) return;
            _routingForeground = true;
            try { WindowManager.MoveToEnd(target); } catch { }
            _routingForeground = false;
        }

        internal static void NotifyWindowForeground(Window window) {
            if (window == null || _routingForeground ||
                    !window.ApplicationInstanceHandle.IsValid) return;
            ApplicationLifecycleResult result = Activate(
                window.ApplicationInstanceHandle);
            if (result.Code == ApplicationLifecycleResultCode.NotFound) {
                RecordStaleOwnership();
            }
        }

        internal static void NotifyShellForeground() {
            if (!_activeApplicationHandle.IsValid) return;
            ApplicationInstance instance;
            if (TryGet(_activeApplicationHandle, out instance)) {
                Deactivate(instance);
            } else {
                _staleLifecycleHandleCount++;
                _activeApplicationHandle = ApplicationInstanceHandle.None;
            }
        }

        public static bool TryActivate(ApplicationInstanceHandle handle,
                                        out LaunchResult failure) {
            ApplicationLifecycleResult result = Activate(handle);
            failure = result.Success ? null : LaunchResult.Failed(
                result.Code == ApplicationLifecycleResultCode.NotFound
                    ? LaunchErrorCode.AlreadyTerminated
                    : LaunchErrorCode.ActivationFailed,
                result.Diagnostic ?? "Application activation failed", null);
            return result.Success;
        }

        internal static bool TryActivate(ApplicationInstance instance,
                                         out LaunchResult failure) {
            ApplicationLifecycleResult result = Activate(
                instance == null ? ApplicationInstanceHandle.None : instance.Handle);
            failure = result.Success ? null : LaunchResult.Failed(
                LaunchErrorCode.ActivationFailed,
                result.Diagnostic ?? "Application activation failed",
                instance == null ? null : instance.DescriptorId);
            return result.Success;
        }

        internal static bool TryAttachWindow(ApplicationInstance instance,
                                              Window window) {
            if (!IsRegisteredInstance(instance) || window == null) return false;
            if (window.ApplicationInstanceHandle == instance.Handle) {
                _duplicateAttachCount++;
                return true;
            }
            if (window.ApplicationInstanceHandle.IsValid) return false;
            if (!instance.AttachWindow(window)) return false;
            _windowAttachCount++;
            return true;
        }

        /// <summary>
        /// Validates a generation-safe instance/window relationship without
        /// changing lifecycle, ownership, z-order, or registry membership.
        /// </summary>
        internal static bool TryValidateOwnedWindow(
                ApplicationInstanceHandle handle, Window window,
                out ApplicationInstance instance) {
            instance = null;
            if (window == null || !TryGet(handle, out instance)) return false;
            if (window.ApplicationInstanceHandle != handle ||
                    !instance.OwnsWindow(window)) {
                instance = null;
                return false;
            }
            return true;
        }

        internal static void AttachWindowsCreatedSince(ApplicationInstance instance,
                                                        int startingCount) {
            if (!IsRegisteredInstance(instance) || WindowManager.Windows == null) return;
            int start = startingCount < 0 ? 0 : startingCount;
            if (start > WindowManager.Windows.Count) start = WindowManager.Windows.Count;
            for (int i = start; i < WindowManager.Windows.Count; i++) {
                Window window = WindowManager.Windows[i];
                if (window == null || window.IsServiceSessionWindow ||
                        window.ApplicationInstanceHandle.IsValid) continue;
                TryAttachWindow(instance, window);
            }
        }

        internal static void FailLaunch(ApplicationInstance instance,
                                        bool reused,
                                        string diagnostic) {
            _failedCount++;
            if (instance == null) return;
            instance.RecordFailure(diagnostic);
            if (reused) {
                if (instance.LifecycleState ==
                        ApplicationInstanceLifecycleState.Loading ||
                    instance.LifecycleState ==
                        ApplicationInstanceLifecycleState.Running ||
                    instance.LifecycleState ==
                        ApplicationInstanceLifecycleState.Activated) {
                    // A failed activation of a reusable instance must leave
                    // the existing implementation available but inactive.
                    // In particular, TryBeginLaunch can reuse an already
                    // Activated instance without entering Loading first.
                    instance.TryTransition(ApplicationInstanceLifecycleState.Inactive);
                }
                return;
            }
            if (instance.LifecycleState != ApplicationInstanceLifecycleState.Failed) {
                instance.TryTransition(ApplicationInstanceLifecycleState.Failed);
            }
            CloseAndDetachOwnedWindows(instance, diagnostic);
            Remove(instance);
        }

        public static bool TryTerminate(ApplicationInstanceHandle handle,
                                        string reason) {
            ApplicationLifecycleResult result = Terminate(handle,
                ApplicationCloseReason.ForcedTermination);
            return result.Success || result.Code ==
                ApplicationLifecycleResultCode.CallbackFailed;
        }

        internal static bool TryTerminate(ApplicationInstance instance,
                                          string reason) {
            if (instance == null) return false;
            ApplicationLifecycleResult result = Terminate(instance,
                ApplicationCloseReason.ForcedTermination, true);
            return result.Success || result.Code ==
                ApplicationLifecycleResultCode.CallbackFailed;
        }

        internal static void OnWindowClosed(Window window) {
            if (window == null) return;
            ApplicationInstanceHandle handle = window.ApplicationInstanceHandle;
            if (!handle.IsValid) return;
            ApplicationInstance instance;
            if (!TryGet(handle, out instance)) {
                _staleOwnershipCount++;
                window.ClearApplicationInstance();
                return;
            }
            if (!instance.DetachWindow(window)) {
                _staleOwnershipCount++;
                window.ClearApplicationInstance();
                return;
            }
            _windowDetachCount++;
            if (instance.OwnedWindowCount != 0) return;
            if (instance.CloseWhenLastWindowClosed) {
                TryTerminate(instance, "last owned window closed");
            } else if (instance.LifecycleState ==
                           ApplicationInstanceLifecycleState.Activated) {
                Deactivate(instance);
            } else if (instance.LifecycleState ==
                       ApplicationInstanceLifecycleState.Running) {
                instance.TryTransition(ApplicationInstanceLifecycleState.Inactive);
                if (_activeApplicationHandle == instance.Handle) {
                    _activeApplicationHandle = ApplicationInstanceHandle.None;
                }
            }
        }

        internal static void RecordStaleOwnership() {
            _staleOwnershipCount++;
        }

        /// <summary>
        /// Close only windows introduced by a factory attempt.  Existing
        /// windows owned by a reused instance are preserved when a new
        /// document/resource launch fails.
        /// </summary>
        internal static void CleanupFactoryWindows(
                ApplicationInstance instance, ApplicationFactoryResult result,
                int startingWindowCount) {
            if (instance == null) return;
            if (result != null) {
                for (int i = 0; i < result.AttachedWindowCount; i++) {
                    if (result.WasAlreadyOwnedAt(i)) continue;
                    CleanupFactoryWindow(instance, result.GetWindowAt(i));
                }
            }
            if (WindowManager.Windows == null) return;
            int start = startingWindowCount < 0 ? 0 : startingWindowCount;
            if (start > WindowManager.Windows.Count) start = WindowManager.Windows.Count;
            for (int i = start; i < WindowManager.Windows.Count; i++) {
                Window window = WindowManager.Windows[i];
                if (window == null) continue;
                if (result != null && ContainsFactoryWindow(result, window)) continue;
                if (window.ApplicationInstanceHandle == instance.Handle ||
                    !window.ApplicationInstanceHandle.IsValid) {
                    CleanupFactoryWindow(instance, window);
                }
            }
        }

        private static bool ContainsFactoryWindow(ApplicationFactoryResult result,
                                                   Window window) {
            for (int i = 0; i < result.AttachedWindowCount; i++) {
                if (result.GetWindowAt(i) == window) return true;
            }
            return false;
        }

        private static void CleanupFactoryWindow(ApplicationInstance instance,
                                                 Window window) {
            if (window == null) return;
            if (window.ApplicationInstanceHandle == instance.Handle &&
                    instance.DetachWindow(window)) _windowDetachCount++;
            if (!window.ApplicationInstanceHandle.IsValid) {
                window.CloseForApplicationTermination();
            }
        }

        internal static bool IsRegisteredInstance(ApplicationInstance instance) {
            if (instance == null) return false;
            ApplicationInstance resolved;
            return TryGet(instance.Handle, out resolved) && resolved == instance;
        }

        private static void CloseAndDetachOwnedWindows(ApplicationInstance instance,
                                                        string reason) {
            for (int i = instance.OwnedWindowCount - 1; i >= 0; i--) {
                Window window = instance.GetOwnedWindowAt(i);
                if (window != null) window.CloseForApplicationTermination();
                if (window != null && instance.DetachWindow(window)) {
                    _windowDetachCount++;
                }
            }
            instance.ClearOwnedWindows();
        }

        private static void Remove(ApplicationInstance instance) {
            if (instance == null) return;
            int slot = (int)instance.Handle.Slot - 1;
            if (slot < 0 || slot >= Capacity || !_used[slot] ||
                _instances[slot] != instance) return;
            _instances[slot] = null;
            _used[slot] = false;
            if (_activeCount > 0) _activeCount--;
            if (_activeApplicationHandle == instance.Handle) {
                _activeApplicationHandle = ApplicationInstanceHandle.None;
            }
        }

        /// <summary>
        /// Deterministic host/runtime proof for identity, transitions, bounds,
        /// reuse, failure cleanup, and zero-window semantics.  Real window
        /// attach/detach is exercised when the initialized GUI backend is
        /// available; the runtime launch diagnostics prove it for every app.
        /// </summary>
        public static bool RunSelfTest() {
            Initialize();
            int passed = 0;
            int failed = 0;
            string firstFailure = null;
            ApplicationInstance[] capacityInstances = new ApplicationInstance[Capacity];

            Check(HandleAndTransitionTest(ref firstFailure), "identity/lifecycle",
                ref passed, ref failed, ref firstFailure);
            Check(ReuseAndMultiInstanceTest(ref firstFailure), "reuse/multi-instance",
                ref passed, ref failed, ref firstFailure);
            Check(FailureCleanupTest(ref firstFailure), "failure cleanup",
                ref passed, ref failed, ref firstFailure);

            int created = 0;
            bool capacitySetup = true;
            for (int i = 0; i < Capacity; i++) {
                ApplicationInstance instance;
                bool reused;
                LaunchResult failure;
                string id = "selftest.capacity." + i.ToString();
                bool ok = TryBeginLaunch(id, ApplicationInstancePolicy.MultiInstance,
                    LaunchRequest.ForAppId(id, null, null,
                        LaunchActivationIntent.NewInstance),
                    out instance, out reused, out failure);
                if (!ok || reused) {
                    capacitySetup = false;
                    break;
                }
                capacityInstances[created++] = instance;
            }
            ApplicationInstance overCapacity;
            bool overReused;
            LaunchResult overFailure;
            bool rejected = !TryBeginLaunch("selftest.capacity.over",
                ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId("selftest.capacity.over", null, null,
                    LaunchActivationIntent.NewInstance),
                out overCapacity, out overReused, out overFailure) &&
                overFailure != null && overFailure.ErrorCode ==
                    LaunchErrorCode.ResourceUnavailable;
            Check(capacitySetup && rejected, "capacity boundary", ref passed,
                ref failed, ref firstFailure);
            for (int i = created - 1; i >= 0; i--) {
                if (capacityInstances[i] != null)
                    TryTerminate(capacityInstances[i], "self-test capacity cleanup");
            }

            bool windowProof = RunWindowOwnershipSelfTest(ref firstFailure);
            Check(windowProof, "window attach/detach", ref passed, ref failed,
                ref firstFailure);
            bool okResult = failed == 0 && ActiveCount == 0;
            if (!okResult && firstFailure == null) firstFailure = "active instance leak";
            AppLaunchResolver.EmitSelfTestSummary("AppModelPhase2", passed,
                failed, firstFailure);
            return okResult;
        }

        /// <summary>
        /// Deterministic Phase 6 lifecycle proof.  It exercises the public
        /// request/result contract without requiring a scheduler or process
        /// boundary and leaves no live or suspended test instances.
        /// </summary>
        public static bool RunLifecycleSelfTest() {
            Initialize();
            int passed = 0;
            int failed = 0;
            string firstFailure = null;
            int startingActive = ActiveCount;

            ApplicationInstance instance = null;
            ApplicationInstanceHandle handle = ApplicationInstanceHandle.None;
            bool reused;
            LaunchResult launchFailure;
            bool started = TryBeginLaunch("selftest.phase6.lifecycle",
                ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId("selftest.phase6.lifecycle", null, null,
                    LaunchActivationIntent.NewInstance), out instance, out reused,
                out launchFailure);
            if (started) {
                handle = instance.Handle;
                bool lifecycleCompleted = TryCompleteLaunch(instance, false,
                    out launchFailure);
                ApplicationLifecycleResult activated = Activate(handle);
                Check(lifecycleCompleted && activated.Success && instance.IsActivated,
                    "activate running instance", ref passed, ref failed,
                    ref firstFailure);
                ApplicationLifecycleResult duplicate = Activate(handle);
                Check(duplicate.Success && instance.IsActivated,
                    "activate already activated instance", ref passed, ref failed,
                    ref firstFailure);
                ApplicationLifecycleResult deactivated = Deactivate(handle);
                Check(deactivated.Success && instance.LifecycleState ==
                    ApplicationInstanceLifecycleState.Inactive,
                    "deactivate instance", ref passed, ref failed,
                    ref firstFailure);
                ApplicationLifecycleResult suspended = Suspend(handle);
                Check(suspended.Success && instance.LifecycleState ==
                    ApplicationInstanceLifecycleState.Suspended,
                    "suspend instance", ref passed, ref failed,
                    ref firstFailure);
                ApplicationLifecycleResult resumed = Resume(handle);
                Check(resumed.Success && instance.LifecycleState ==
                    ApplicationInstanceLifecycleState.Inactive,
                    "resume instance", ref passed, ref failed,
                    ref firstFailure);
                ApplicationLifecycleResult reactivated = Activate(handle);
                Check(reactivated.Success && instance.IsActivated,
                    "reactivate instance", ref passed, ref failed,
                    ref firstFailure);
                ApplicationLifecycleResult close = RequestClose(handle,
                    ApplicationCloseReason.UserRequest);
                Check(close.Success && !TryGet(handle,
                    out ApplicationInstance ignoredClosed),
                    "accepted close request", ref passed, ref failed,
                    ref firstFailure);
            } else {
                Check(false, "lifecycle test setup", ref passed, ref failed,
                    ref firstFailure);
            }

            ApplicationInstance unsupported = null;
            ApplicationInstanceHandle unsupportedHandle =
                ApplicationInstanceHandle.None;
            bool unsupportedReused;
            bool unsupportedStarted = TryBeginLaunch(
                "selftest.phase6.unsupported", ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId("selftest.phase6.unsupported", null, null,
                    LaunchActivationIntent.NewInstance), out unsupported,
                out unsupportedReused, out launchFailure);
            if (unsupportedStarted) {
                unsupportedHandle = unsupported.Handle;
                unsupported.SetLifecycleAdapter(new GxmApplicationLifecycleAdapter());
                bool completed = TryCompleteLaunch(unsupported, false,
                    out launchFailure);
                ApplicationLifecycleResult unsupportedResult = Suspend(
                    unsupportedHandle);
                Check(completed && unsupportedResult.Code ==
                    ApplicationLifecycleResultCode.Unsupported &&
                    unsupported.LifecycleState ==
                        ApplicationInstanceLifecycleState.Running,
                    "suspend unsupported", ref passed, ref failed,
                    ref firstFailure);
                TryTerminate(unsupported, "phase6 unsupported cleanup");
            } else {
                Check(false, "unsupported lifecycle setup", ref passed,
                    ref failed, ref firstFailure);
            }

            ApplicationInstance callbackInstance = null;
            ApplicationInstanceHandle callbackHandle =
                ApplicationInstanceHandle.None;
            LifecycleSelfTestAdapter callbackAdapter = null;
            bool callbackReused;
            bool callbackStarted = TryBeginLaunch(
                "selftest.phase6.callback", ApplicationInstancePolicy.ReuseExisting,
                LaunchRequest.ForAppId("selftest.phase6.callback", null, null,
                    LaunchActivationIntent.NewInstance), out callbackInstance,
                out callbackReused, out launchFailure);
            if (callbackStarted) {
                callbackHandle = callbackInstance.Handle;
                callbackAdapter = new LifecycleSelfTestAdapter();
                callbackInstance.SetLifecycleAdapter(callbackAdapter);
                bool completed = TryCompleteLaunch(callbackInstance, false,
                    out launchFailure);
                callbackAdapter.FailSuspend = true;
                ApplicationLifecycleResult suspendFailure = Suspend(callbackHandle);
                callbackAdapter.FailSuspend = false;
                callbackAdapter.CancelClose = true;
                ApplicationLifecycleResult closeCancelled = RequestClose(
                    callbackHandle, ApplicationCloseReason.UserRequest);
                callbackAdapter.CancelClose = false;
                Check(completed && suspendFailure.Code ==
                    ApplicationLifecycleResultCode.CallbackFailed &&
                    closeCancelled.Code == ApplicationLifecycleResultCode.Cancelled &&
                    callbackInstance.LifecycleState ==
                        ApplicationInstanceLifecycleState.Running,
                    "lifecycle callback failure/cancellation", ref passed,
                    ref failed, ref firstFailure);

                ApplicationLifecycleResult suspended = Suspend(callbackHandle);
                callbackAdapter.FailResume = true;
                ApplicationLifecycleResult resumeFailure = Resume(callbackHandle);
                callbackAdapter.FailResume = false;
                ApplicationLifecycleResult resumed = Resume(callbackHandle);
                Check(suspended.Success && resumeFailure.Code ==
                    ApplicationLifecycleResultCode.CallbackFailed &&
                    resumed.Success && callbackInstance.LifecycleState ==
                        ApplicationInstanceLifecycleState.Inactive,
                    "resume callback failure", ref passed, ref failed,
                    ref firstFailure);
                TryTerminate(callbackInstance, "phase6 callback cleanup");
            } else {
                Check(false, "callback lifecycle setup", ref passed, ref failed,
                    ref firstFailure);
            }

            ApplicationInstance zero = null;
            ApplicationInstanceHandle zeroHandle = ApplicationInstanceHandle.None;
            bool zeroReused;
            bool zeroStarted = TryBeginLaunch("selftest.phase6.zero",
                ApplicationInstancePolicy.ReuseExisting,
                LaunchRequest.ForAppId("selftest.phase6.zero", null, null,
                    LaunchActivationIntent.NewInstance), out zero, out zeroReused,
                out launchFailure);
            if (zeroStarted) {
                zeroHandle = zero.Handle;
                bool completed = TryCompleteLaunch(zero, true, out launchFailure);
                ApplicationLifecycleResult suspended = Suspend(zeroHandle);
                ApplicationLifecycleResult resumed = Resume(zeroHandle);
                bool stable = zero.OwnedWindowCount == 0 && completed &&
                    suspended.Success && resumed.Success;
                Check(stable, "zero-window reusable instance", ref passed,
                    ref failed, ref firstFailure);
                TryTerminate(zero, "phase6 zero-window cleanup");
            } else {
                Check(false, "zero-window lifecycle setup", ref passed,
                    ref failed, ref firstFailure);
            }

            ApplicationInstance stale = null;
            ApplicationInstanceHandle staleHandle = ApplicationInstanceHandle.None;
            bool staleReused;
            bool staleStarted = TryBeginLaunch("selftest.phase6.stale",
                ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId("selftest.phase6.stale", null, null,
                    LaunchActivationIntent.NewInstance), out stale, out staleReused,
                out launchFailure);
            if (staleStarted) {
                staleHandle = stale.Handle;
                ApplicationLifecycleResult terminated = Terminate(staleHandle,
                    ApplicationCloseReason.ForcedTermination);
                ApplicationLifecycleResult staleActivation = Activate(staleHandle);
                ApplicationLifecycleResult invalidResume = Resume(staleHandle);
                Check((terminated.Success || terminated.Code ==
                    ApplicationLifecycleResultCode.CallbackFailed) &&
                    staleActivation.Code == ApplicationLifecycleResultCode.NotFound &&
                    invalidResume.Code == ApplicationLifecycleResultCode.NotFound,
                    "terminal and stale handle requests", ref passed,
                    ref failed, ref firstFailure);
            } else {
                Check(false, "stale lifecycle setup", ref passed, ref failed,
                    ref firstFailure);
            }

            bool cleanup = ActiveCount == startingActive && SuspendedCount == 0 &&
                _activeApplicationHandle == ApplicationInstanceHandle.None;
            Check(cleanup, "lifecycle state/capacity cleanup", ref passed,
                ref failed, ref firstFailure);
            Check(RunLifecycleMultiWindowSelfTest(),
                "multi-window lifecycle semantics", ref passed, ref failed,
                ref firstFailure);
            cleanup = ActiveCount == startingActive && SuspendedCount == 0 &&
                _activeApplicationHandle == ApplicationInstanceHandle.None;
            Check(cleanup, "multi-window lifecycle cleanup", ref passed,
                ref failed, ref firstFailure);
            AppLaunchResolver.EmitSelfTestSummary("AppModelPhase6Lifecycle",
                passed, failed, firstFailure);
            Program.MarkUefiAppModelDiagnostic(
                "LIFECYCLE_SELFTEST=passed=" + passed.ToString() +
                ";failed=" + failed.ToString() + ";first=" +
                (firstFailure ?? ""));
            return failed == 0 && cleanup;
        }

        private static bool RunLifecycleMultiWindowSelfTest() {
            if (WindowManager.Windows == null || Framebuffer.Graphics == null) {
                return true;
            }
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!TryBeginLaunch("selftest.phase6.multiwindow",
                    ApplicationInstancePolicy.ReuseExisting,
                    LaunchRequest.ForAppId("selftest.phase6.multiwindow", null,
                        null, LaunchActivationIntent.NewInstance), out instance,
                    out reused, out failure)) return false;
            OwnershipProbeWindow first = null;
            OwnershipProbeWindow second = null;
            bool result = false;
            try {
                first = new OwnershipProbeWindow();
                second = new OwnershipProbeWindow();
                bool attached = TryAttachWindow(instance, first) &&
                    TryAttachWindow(instance, second);
                bool launched = TryCompleteLaunch(instance, true, out failure);
                ApplicationLifecycleResult deactivated = Deactivate(instance.Handle);
                ApplicationLifecycleResult suspended = Suspend(instance.Handle);
                int retained = instance.OwnedWindowCount;
                ApplicationLifecycleResult resumed = Resume(instance.Handle);
                ApplicationLifecycleResult activated = Activate(instance.Handle);
                result = attached && launched && deactivated.Success &&
                    suspended.Success && retained == 2 && resumed.Success &&
                    activated.Success && instance.OwnedWindowCount == 2;
            } catch {
                result = false;
            }
            TryTerminate(instance, "phase6 multi-window cleanup");
            if (first != null) first.CloseForApplicationTermination();
            if (second != null) second.CloseForApplicationTermination();
            WindowManager.CleanupClosedWindows();
            return result && ActiveCount == 0 && SuspendedCount == 0;
        }

        /// <summary>
        /// Bounded runtime proof using the normal factory/application path.
        /// It intentionally performs cooperative lifecycle work only and
        /// closes every instance before returning to the shell.
        /// </summary>
        public static bool RunLifecycleRuntimeDiagnostic() {
            if (WindowManager.Windows == null || Desktop.Apps == null) return false;
            ApplicationInstance calculator = null;
            ApplicationInstance notepad = null;
            ApplicationInstance console = null;
            ApplicationInstance consoleAgain = null;
            ApplicationInstance imageViewer = null;
            try {
                LaunchResult calculatorResult;
                bool calculatorLaunch = Desktop.LaunchApplication(
                    LaunchRequest.ForAppId("gxos.builtin.calculator", null, null,
                        LaunchActivationIntent.Launch), out calculatorResult);
                calculatorLaunch = calculatorLaunch && calculatorResult != null &&
                    calculatorResult.Success &&
                    TryGet(calculatorResult.InstanceHandle, out calculator);

                LaunchResult notepadResult;
                bool notepadLaunch = Desktop.LaunchApplication(
                    LaunchRequest.ForFile("gxos.builtin.notepad", "README.md",
                        null, "open", "phase6.runtime", false,
                        LaunchActivationIntent.Launch), out notepadResult);
                notepadLaunch = notepadLaunch && notepadResult != null &&
                    notepadResult.Success &&
                    TryGet(notepadResult.InstanceHandle, out notepad);
                bool deactivated = calculator != null && notepad != null &&
                    calculator.LifecycleState ==
                        ApplicationInstanceLifecycleState.Inactive &&
                    notepad.Document == "README.md";

                ApplicationLifecycleResult suspended = calculator == null
                    ? null : Suspend(calculator.Handle);
                LaunchResult consoleResult;
                bool consoleLaunch = Desktop.LaunchApplication(
                    LaunchRequest.ForAppId("gxos.builtin.console", null, null,
                        LaunchActivationIntent.Launch), out consoleResult);
                consoleLaunch = consoleLaunch && consoleResult != null &&
                    consoleResult.Success &&
                    TryGet(consoleResult.InstanceHandle, out console);
                bool operatedWhileSuspended = suspended != null && suspended.Success &&
                    calculator.LifecycleState ==
                        ApplicationInstanceLifecycleState.Suspended &&
                    console != null && console.IsActivated;

                ApplicationLifecycleResult notepadDeactivated = notepad == null
                    ? null : Deactivate(notepad.Handle);
                ApplicationLifecycleResult notepadSuspended = notepad == null
                    ? null : Suspend(notepad.Handle);
                ApplicationLifecycleResult notepadResumed = notepad == null
                    ? null : Resume(notepad.Handle);
                bool documentRetained = notepad != null && notepad.Document ==
                    "README.md" && notepadSuspended != null &&
                    notepadSuspended.Success && notepadResumed != null &&
                    notepadResumed.Success;

                ApplicationLifecycleResult resumed = calculator == null
                    ? null : Resume(calculator.Handle);
                ApplicationLifecycleResult reactivated = calculator == null
                    ? null : Activate(calculator.Handle);
                bool calculatorResumed = resumed != null && resumed.Success &&
                    reactivated != null && reactivated.Success &&
                    calculator.LifecycleState ==
                        ApplicationInstanceLifecycleState.Activated;

                LaunchResult consoleAgainResult;
                bool consoleAgainLaunch = Desktop.LaunchApplication(
                    LaunchRequest.ForAppId("gxos.builtin.console", null, null,
                        LaunchActivationIntent.ActivateExisting),
                    out consoleAgainResult);
                consoleAgainLaunch = consoleAgainLaunch && consoleAgainResult != null &&
                    consoleAgainResult.Success &&
                    TryGet(consoleAgainResult.InstanceHandle, out consoleAgain);
                bool consoleReused = console != null && consoleAgain != null &&
                    console.Handle == consoleAgain.Handle &&
                    console.OwnedWindowCount == consoleAgain.OwnedWindowCount;

                LaunchResult imageResult;
                bool imageLaunch = Desktop.LaunchApplication(
                    LaunchRequest.ForFile("gxos.builtin.imageviewer",
                        "Images/Banner.png", null, "open", "phase6.runtime",
                        false, LaunchActivationIntent.Launch), out imageResult);
                imageLaunch = imageLaunch && imageResult != null &&
                    imageResult.Success &&
                    TryGet(imageResult.InstanceHandle, out imageViewer);
                bool imageRetained = imageViewer != null &&
                    imageViewer.OwnedWindowCount > 0;

                bool closed = CloseRuntimeInstance(calculator) &&
                    CloseRuntimeInstance(notepad) &&
                    CloseRuntimeInstance(console) &&
                    (consoleAgain == null || consoleAgain == console ||
                        CloseRuntimeInstance(consoleAgain)) &&
                    CloseRuntimeInstance(imageViewer);
                WindowManager.CleanupClosedWindows();
                bool cleanup = ActiveCount == 0 && SuspendedCount == 0 &&
                    _activeApplicationHandle == ApplicationInstanceHandle.None;
                bool passed = calculatorLaunch && notepadLaunch && deactivated &&
                    operatedWhileSuspended && documentRetained && calculatorResumed &&
                    consoleAgainLaunch && consoleReused && imageLaunch &&
                    imageRetained && closed && cleanup;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                Program.MarkUefiAppRuntime("LIFECYCLE_RUNTIME=calculator=" +
                    (calculatorLaunch ? "1" : "0") + ";notepad=" +
                    (notepadLaunch ? "1" : "0") + ";consoleReuse=" +
                    (consoleReused ? "1" : "0") + ";image=" +
                    (imageLaunch ? "1" : "0") + ";cleanup=" +
                    (cleanup ? "1" : "0") + ";result=" +
                    (passed ? "PASS" : "FAIL"));
#endif
                return passed;
            } catch {
                if (calculator != null) TryTerminate(calculator,
                    "phase6 runtime exception cleanup");
                if (notepad != null) TryTerminate(notepad,
                    "phase6 runtime exception cleanup");
                if (console != null) TryTerminate(console,
                    "phase6 runtime exception cleanup");
                if (imageViewer != null) TryTerminate(imageViewer,
                    "phase6 runtime exception cleanup");
                WindowManager.CleanupClosedWindows();
                return false;
            }
        }

        /// <summary>
        /// Bounded runtime proof for the first application-service cohort.
        /// Launches through descriptor and factory paths, validates sharing
        /// and snapshots, rejects a terminated context, and restores the
        /// authoritative instance/window baseline before returning.
        /// </summary>
        public static bool RunApplicationServiceRuntimeDiagnostic() {
            int baselineActive = ActiveCount;
            int baselineObservations = ObservationCount;
            int baselineWindows = WindowManager.Windows == null ? 0 :
                WindowManager.Windows.Count;
            int baselineStaleOwnership = StaleOwnershipCount;
            ApplicationInstance calculator = null;
            ApplicationInstance notepad1 = null;
            ApplicationInstance notepad2 = null;
            ApplicationInstance taskManager = null;
            ApplicationInstance displayOptions = null;
            bool notification = false;
            bool sharedSettings = false;
            bool snapshot = false;
            bool staleRejected = false;
            bool shellLaunch = false;
            bool documentOpen = false;
            ApplicationInstanceHandle shellTargetHandle =
                ApplicationInstanceHandle.None;
            ApplicationInstanceHandle documentTargetHandle =
                ApplicationInstanceHandle.None;
            bool cleanup = false;
            try {
                bool calculatorLaunch = TryLaunchServiceRuntimeInstance(
                    "gxos.builtin.calculator", out calculator);
                bool notepad1Launch = TryLaunchServiceRuntimeInstance(
                    "gxos.builtin.notepad", out notepad1);
                bool notepad2Launch = TryLaunchServiceRuntimeInstance(
                    "gxos.builtin.notepad", out notepad2);
                bool taskManagerLaunch = TryLaunchServiceRuntimeInstance(
                    "gxos.builtin.taskmanager", out taskManager);
                bool displayOptionsLaunch = TryLaunchServiceRuntimeInstance(
                    "gxos.builtin.displayoptions", out displayOptions);

                ApplicationServiceContext calculatorContext = null;
                ApplicationServiceContext notepad1Context = null;
                ApplicationServiceContext notepad2Context = null;
                ApplicationServiceContext taskManagerContext = null;
                ApplicationServiceContext displayOptionsContext = null;
                ApplicationServiceAccess calculatorServices = null;
                ApplicationServiceAccess notepad1Services = null;
                ApplicationServiceAccess notepad2Services = null;
                ApplicationServiceAccess taskManagerServices = null;
                ApplicationServiceAccess displayOptionsServices = null;
                ApplicationServiceResult serviceResult = null;
                bool contexts = calculatorLaunch && notepad1Launch &&
                    notepad2Launch && taskManagerLaunch &&
                    displayOptionsLaunch &&
                    ApplicationServiceRegistry.TryCreateContextAndAccess(calculator.Handle,
                        out calculatorContext, out calculatorServices,
                        out serviceResult) &&
                    ApplicationServiceRegistry.TryCreateContextAndAccess(notepad1.Handle,
                        out notepad1Context, out notepad1Services,
                        out serviceResult) &&
                    ApplicationServiceRegistry.TryCreateContextAndAccess(notepad2.Handle,
                        out notepad2Context, out notepad2Services,
                        out serviceResult) &&
                    ApplicationServiceRegistry.TryCreateContextAndAccess(taskManager.Handle,
                        out taskManagerContext, out taskManagerServices,
                        out serviceResult) &&
                    ApplicationServiceRegistry.TryCreateContextAndAccess(
                        displayOptions.Handle, out displayOptionsContext,
                        out displayOptionsServices, out serviceResult);

                Notepad notepadWindow1 = notepad1 == null ? null :
                    notepad1.GetOwnedWindowAt(0) as Notepad;
                Notepad notepadWindow2 = notepad2 == null ? null :
                    notepad2.GetOwnedWindowAt(0) as Notepad;
                DisplayOptions displayOptionsWindow = displayOptions == null ? null :
                    displayOptions.GetOwnedWindowAt(0) as DisplayOptions;
                bool notepadDialogFlows = contexts && notepadWindow1 != null &&
                    notepadWindow2 != null &&
                    Activate(notepad1.Handle).Success &&
                    notepadWindow1.RunPhase9ServiceDiagnostic(
                        notepad2Context, notepad2Services);
                bool displayOptionsServiceFlow = contexts &&
                    displayOptionsWindow != null &&
                    Activate(displayOptions.Handle).Success &&
                    displayOptionsWindow.RunPhase9BackgroundServiceDiagnostic();

                if (contexts && calculatorServices.Shell != null &&
                        Activate(calculator.Handle).Success) {
                    ApplicationServiceResult<
                        ApplicationServiceRequestHandle> shellBegun =
                        calculatorServices.Shell.Begin(calculatorContext,
                            ApplicationShellOpenRequest.ForApplicationId(
                                "gxos.builtin.calculator"));
                    if (shellBegun.Succeeded) {
                        ApplicationServiceResult<ApplicationServiceRequestStatus<
                            ApplicationShellResult>> shellObserved =
                            calculatorServices.Shell.Observe(calculatorContext,
                                shellBegun.Value);
                        shellLaunch = shellObserved.Succeeded &&
                            shellObserved.Value != null &&
                            shellObserved.Value.Value != null &&
                            shellObserved.Value.Value.ResultCode ==
                                ApplicationServiceResultCode.Success;
                        if (shellLaunch &&
                                shellObserved.Value.Value.InstanceHandle.IsValid) {
                            shellTargetHandle =
                                shellObserved.Value.Value.InstanceHandle;
                        }
                        if (shellLaunch && shellTargetHandle.IsValid &&
                                shellTargetHandle != calculator.Handle)
                            TryTerminate(shellTargetHandle,
                                "phase 9 shell launch proof cleanup");
                    }

                    // Launching another application can legitimately make
                    // the requester inactive.  A new interactive request
                    // requires reactivation; this does not alter the rule
                    // that an already-issued handle remains observable while
                    // the requester is inactive.
                    bool requesterReactivated = Activate(
                        calculator.Handle).Success;
                    ApplicationServiceResult<
                        ApplicationServiceRequestHandle> documentBegun =
                        requesterReactivated
                            ? calculatorServices.Shell.Begin(calculatorContext,
                                ApplicationShellOpenRequest.ForDocument(
                                    "Programs/imageviewer.gxm"))
                            : ApplicationServiceResult<
                                ApplicationServiceRequestHandle>.Failure(
                                    ApplicationServiceResultCode.InvalidState,
                                    "Shell proof requester could not be reactivated");
                    if (documentBegun.Succeeded) {
                        ApplicationServiceResult<ApplicationServiceRequestStatus<
                            ApplicationShellResult>> documentObserved =
                            calculatorServices.Shell.Observe(calculatorContext,
                                documentBegun.Value);
                        documentOpen = documentObserved.Succeeded &&
                            documentObserved.Value != null &&
                            documentObserved.Value.Value != null &&
                            documentObserved.Value.Value.ResultCode ==
                                ApplicationServiceResultCode.Success;
                        if (documentOpen &&
                                documentObserved.Value.Value.InstanceHandle.IsValid) {
                            documentTargetHandle =
                                documentObserved.Value.Value.InstanceHandle;
                            TryTerminate(documentTargetHandle,
                                "phase 9 document open proof cleanup");
                        }
                    }
                }

                if (contexts) {
                    ApplicationNotificationRequest request =
                        ApplicationNotificationRequest.Create(
                            "Service runtime", "Notification path",
                            ApplicationNotificationSeverity.Info);
                    notification = calculatorServices.Notifications.Publish(
                        calculatorContext, request).Succeeded &&
                        calculatorServices.Notifications.Clear(
                            calculatorContext).Succeeded;

                    ApplicationServiceResult set = notepad1Services.Settings.Set(
                        notepad1Context, "wrap",
                        ApplicationSettingValue.Boolean(false));
                    ApplicationServiceResult<ApplicationSettingValue> read =
                        notepad2Services.Settings.Get(notepad2Context, "wrap");
                    sharedSettings = set.Succeeded && read.Succeeded &&
                        read.Value.Kind == ApplicationSettingValueKind.Boolean &&
                        !read.Value.BooleanValue;

                    ApplicationServiceResult<SystemInformationSnapshot> system =
                        taskManagerServices.SystemInformation.GetSnapshot(
                            taskManagerContext);
                    snapshot = system.Succeeded &&
                        system.Value.IsWithinBounds() &&
                        system.Value.MemorySizeBytes >=
                            system.Value.MemoryInUseBytes;

                    ApplicationServiceContext stale = notepad1Context;
                    bool terminated = TryTerminate(notepad1,
                        "application service runtime stale-context proof");
                    ApplicationServiceResult<ApplicationSettingValue> staleRead =
                        notepad1Services.Settings.Get(stale, "wrap");
                    staleRejected = terminated &&
                        staleRead.Code == ApplicationServiceResultCode.InvalidContext;
                }

                if (notepad2 != null) TryTerminate(notepad2,
                    "application service runtime cleanup");
                if (calculator != null) TryTerminate(calculator,
                    "application service runtime cleanup");
                if (taskManager != null) TryTerminate(taskManager,
                    "application service runtime cleanup");
                if (displayOptions != null) TryTerminate(displayOptions,
                    "application service runtime cleanup");
                ApplicationInstance residualTarget;
                if (shellTargetHandle.IsValid &&
                        shellTargetHandle != (calculator == null ?
                            ApplicationInstanceHandle.None : calculator.Handle) &&
                        TryGet(shellTargetHandle, out residualTarget)) {
                    TryTerminate(residualTarget,
                        "application service shell target final cleanup");
                }
                if (documentTargetHandle.IsValid &&
                        TryGet(documentTargetHandle, out residualTarget)) {
                    TryTerminate(residualTarget,
                        "application service document target final cleanup");
                }
                WindowManager.CleanupClosedWindows();
                cleanup = ActiveCount == baselineActive &&
                    ObservationCount == baselineObservations &&
                    (WindowManager.Windows == null ? 0 :
                        WindowManager.Windows.Count) == baselineWindows &&
                    StaleOwnershipCount == baselineStaleOwnership;
                bool passed = notepadDialogFlows && displayOptionsServiceFlow &&
                    shellLaunch && documentOpen && notification &&
                    sharedSettings && snapshot && staleRejected && cleanup;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                Program.MarkUefiAppRuntime("PHASE9_OPEN_SUCCESS=" +
                    (notepadDialogFlows ? "1" : "0"));
                Program.MarkUefiAppRuntime("PHASE9_OPEN_CANCEL=" +
                    (notepadDialogFlows ? "1" : "0"));
                Program.MarkUefiAppRuntime("PHASE9_SAVE_RESULT=" +
                    (notepadDialogFlows ? "success" : "fail"));
                Program.MarkUefiAppRuntime("PHASE9_CONFIRMATION_RESULT=" +
                    (notepadDialogFlows ? "cancelled" : "fail"));
                Program.MarkUefiAppRuntime("PHASE9_SHELL_LAUNCH=" +
                    (shellLaunch ? "1" : "0"));
                Program.MarkUefiAppRuntime("PHASE9_DOCUMENT_OPEN=" +
                    (documentOpen ? "1" : "0"));
                Program.MarkUefiAppRuntime("PHASE9_ORPHAN_DIALOG_COUNT=" +
                    ApplicationServiceRegistry.OrphanTransientWindowCount.ToString());
                Program.MarkUefiAppRuntime("PHASE9_STALE_SERVICE_CONTEXT_COUNT=" +
                    (cleanup && ApplicationServiceRegistry.ActiveRequestCount == 0 ?
                        "0" : "1"));
                Program.MarkUefiAppRuntime("PHASE9_RUNTIME_OK=" +
                    (passed ? "1" : "0"));
                Program.MarkUefiAppRuntime(
                    "SERVICES_NOTIFICATION=" +
                    (notification ? "PASS" : "FAIL"));
                Program.MarkUefiAppRuntime(
                    "SERVICES_SHARED_SETTINGS=" +
                    (sharedSettings ? "PASS" : "FAIL"));
                Program.MarkUefiAppRuntime(
                    "SERVICES_SNAPSHOT=" +
                    (snapshot ? "PASS" : "FAIL"));
                Program.MarkUefiAppRuntime(
                    "DISPLAY_OPTIONS_SERVICE_DIAGNOSTIC=" +
                    (displayOptionsServiceFlow ? "PASS" : "FAIL"));
                Program.MarkUefiAppRuntime(
                    "SERVICES_STALE_REJECTED=" +
                    (staleRejected ? "PASS" : "FAIL"));
                Program.MarkUefiAppRuntime(
                    "SERVICES_CLEANUP=" +
                    (cleanup ? "PASS" : "FAIL"));
                Program.MarkUefiAppRuntime(
                    "SERVICES_STALE_CONTEXTS=" +
                    (cleanup ? "0" : "1"));
                Program.MarkUefiAppRuntime(
                    "SERVICES_RESULT=" +
                    (passed ? "PASS" : "FAIL"));
#endif
                return passed;
            } catch {
                if (notepad1 != null) TryTerminate(notepad1,
                    "application service runtime exception cleanup");
                if (notepad2 != null) TryTerminate(notepad2,
                    "application service runtime exception cleanup");
                if (calculator != null) TryTerminate(calculator,
                    "application service runtime exception cleanup");
                if (taskManager != null) TryTerminate(taskManager,
                    "application service runtime exception cleanup");
                if (displayOptions != null) TryTerminate(displayOptions,
                    "application service runtime exception cleanup");
                WindowManager.CleanupClosedWindows();
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                Program.MarkUefiAppRuntime("SERVICES_RESULT=FAIL");
#endif
                return false;
            }
        }

        private static bool TryLaunchServiceRuntimeInstance(
                string appId, out ApplicationInstance instance) {
            instance = null;
            ApplicationDescriptor descriptor;
            if (!ApplicationDescriptorRegistry.TryGetById(appId,
                    out descriptor)) return false;
            LaunchResult result;
            if (!ApplicationFactoryRegistry.TryLaunch(descriptor,
                    LaunchRequest.ForAppId(appId, null, null,
                        LaunchActivationIntent.Launch), out result) ||
                    result == null || !result.Success) return false;
            return TryGet(result.InstanceHandle, out instance);
        }

        /// <summary>
        /// Bounded runtime proof for the application-centric taskbar contract.
        /// The fixture uses the authoritative instance registry and the normal
        /// typed Desktop launch path; TaskbarApplicationEntryRegistry is only
        /// observed/reconciled as the presentation projection.
        /// </summary>
        public static bool RunTaskbarGroupingRuntimeDiagnostic() {
            _lastTaskbarGroupingRuntimeCleanup = false;
            bool oneInstanceTwoWindows = false;
            bool sameInstanceSwitch = false;
            bool closeFirstRetainsSecond = false;
            bool finalWindowFollowsPolicy = false;
            bool calculatorMultipleInstance = false;
            bool calculatorCloseRetainsOther = false;
            bool consoleReused = false;
            bool imageViewerReused = false;
            bool zeroWindowProjectionSuppressed = false;
            bool cleanup = false;
            bool staleTaskbarOwners = false;
            int sameInstanceActivationDelta = -1;
            int semanticGroupCount = 0;
            int calculatorGroupCount = 0;
            bool calculatorHandlesDistinct = false;
            ApplicationInstance multiWindow = null;
            ApplicationInstance calculatorA = null;
            ApplicationInstance calculatorB = null;
            ApplicationInstance console = null;
            ApplicationInstance consoleAgain = null;
            ApplicationInstance imageViewer = null;
            ApplicationInstance imageViewerAgain = null;
            TaskbarRuntimeProbeWindow firstWindow = null;
            TaskbarRuntimeProbeWindow secondWindow = null;
            int startingWindowCount = WindowManager.Windows == null
                ? -1 : WindowManager.Windows.Count;
            int startingEntryCount = TaskbarApplicationEntryRegistry.EntryCount;

            try {
                if (WindowManager.Windows != null && Desktop.Apps != null) {
                    LaunchResult failure;
                    bool reused;
                    bool multiStarted = TryBeginLaunch(
                        "runtime.taskbar.multiwindow",
                        ApplicationInstancePolicy.MultiInstance,
                        LaunchRequest.ForAppId("runtime.taskbar.multiwindow",
                            null, null, LaunchActivationIntent.NewInstance),
                        out multiWindow, out reused, out failure);
                    firstWindow = new TaskbarRuntimeProbeWindow();
                    secondWindow = new TaskbarRuntimeProbeWindow();
                    bool attached = multiStarted && !reused &&
                        TryAttachWindow(multiWindow, firstWindow) &&
                        TryAttachWindow(multiWindow, secondWindow) &&
                        TryCompleteLaunch(multiWindow, true, out failure);
                    TaskbarApplicationEntryRegistry.Reconcile();
                    TaskbarApplicationEntry multiEntry;
                    TaskbarApplicationEntry firstEntry;
                    TaskbarApplicationEntry secondEntry;
                    semanticGroupCount = TaskbarApplicationEntryRegistry.EntryCount -
                        startingEntryCount;
                    oneInstanceTwoWindows = attached &&
                        TaskbarApplicationEntryRegistry.TryGet(
                            multiWindow.Handle, out multiEntry) &&
                        semanticGroupCount == 1 &&
                        multiEntry.OwnedWindowCount == 2 &&
                        multiEntry.PresentableWindowCount == 2 &&
                        TaskbarApplicationEntryRegistry.TryGetForWindow(
                            firstWindow, out firstEntry) &&
                        TaskbarApplicationEntryRegistry.TryGetForWindow(
                            secondWindow, out secondEntry) &&
                        firstEntry == secondEntry;

                    int activationBeforeFirst = InstancesActivated;
                    ApplicationLifecycleResult firstFocus = null;
                    bool focusedFirst = attached &&
                        TaskbarApplicationEntryRegistry.TryFocusWindow(
                            multiWindow.Handle, firstWindow, out firstFocus);
                    int activationBeforeSecond = InstancesActivated;
                    ApplicationLifecycleResult secondFocus = null;
                    bool focusedSecond = attached &&
                        TaskbarApplicationEntryRegistry.TryFocusWindow(
                            multiWindow.Handle, secondWindow, out secondFocus);
                    sameInstanceActivationDelta = InstancesActivated -
                        activationBeforeSecond;
                    sameInstanceSwitch = focusedFirst && focusedSecond &&
                        firstFocus != null && firstFocus.Success &&
                        secondFocus != null && secondFocus.Success &&
                        activationBeforeFirst <= activationBeforeSecond &&
                        sameInstanceActivationDelta == 0;

                    OnWindowClosed(firstWindow);
                    firstWindow.CloseForApplicationTermination();
                    WindowManager.CleanupClosedWindows();
                    TaskbarApplicationEntryRegistry.Reconcile();
                    TaskbarApplicationEntry retainedEntry;
                    closeFirstRetainsSecond =
                        TryGet(multiWindow.Handle, out multiWindow) &&
                        multiWindow.OwnedWindowCount == 1 &&
                        secondWindow.ApplicationInstanceHandle == multiWindow.Handle &&
                        TaskbarApplicationEntryRegistry.TryGet(
                            multiWindow.Handle, out retainedEntry) &&
                        retainedEntry.OwnedWindowCount == 1 &&
                        retainedEntry.PresentableWindowCount == 1;

                    ApplicationInstanceHandle finalHandle = multiWindow == null
                        ? ApplicationInstanceHandle.None : multiWindow.Handle;
                    OnWindowClosed(secondWindow);
                    secondWindow.CloseForApplicationTermination();
                    WindowManager.CleanupClosedWindows();
                    TaskbarApplicationEntryRegistry.Reconcile();
                    finalWindowFollowsPolicy = finalHandle.IsValid &&
                        !TryGet(finalHandle, out ApplicationInstance ignoredFinal) &&
                        !TaskbarApplicationEntryRegistry.TryGet(finalHandle,
                            out TaskbarApplicationEntry ignoredFinalEntry);

                    LaunchResult calculatorAResult;
                    LaunchResult calculatorBResult;
                    bool calculatorALaunched = Desktop.LaunchApplication(
                        LaunchRequest.ForAppId("gxos.builtin.calculator", null,
                            null, LaunchActivationIntent.NewInstance),
                        out calculatorAResult);
                    bool calculatorBLaunched = Desktop.LaunchApplication(
                        LaunchRequest.ForAppId("gxos.builtin.calculator", null,
                            null, LaunchActivationIntent.NewInstance),
                        out calculatorBResult);
                    bool calculatorsFound = calculatorALaunched &&
                        calculatorAResult != null && calculatorAResult.Success &&
                        calculatorBLaunched && calculatorBResult != null &&
                        calculatorBResult.Success &&
                        TryGet(calculatorAResult.InstanceHandle, out calculatorA) &&
                        TryGet(calculatorBResult.InstanceHandle, out calculatorB) &&
                        calculatorA.Handle != calculatorB.Handle;
                    TaskbarApplicationEntry calculatorAEntry;
                    TaskbarApplicationEntry calculatorBEntry;
                    TaskbarApplicationEntryRegistry.Reconcile();
                    calculatorGroupCount =
                        TaskbarApplicationEntryRegistry.EntryCount -
                        startingEntryCount;
                    calculatorHandlesDistinct = calculatorsFound &&
                        calculatorA.Handle != calculatorB.Handle;
                    calculatorMultipleInstance = calculatorHandlesDistinct &&
                        TaskbarApplicationEntryRegistry.TryGet(
                            calculatorA.Handle, out calculatorAEntry) &&
                        TaskbarApplicationEntryRegistry.TryGet(
                            calculatorB.Handle, out calculatorBEntry) &&
                        calculatorAEntry != calculatorBEntry &&
                        calculatorGroupCount == 2;
                    bool calculatorAActive = calculatorsFound &&
                        Activate(calculatorA.Handle).Success;
                    bool calculatorBActive = calculatorsFound &&
                        Activate(calculatorB.Handle).Success;
                    bool calculatorAClosed = calculatorAActive && calculatorBActive &&
                        CloseRuntimeInstance(calculatorA);
                    WindowManager.CleanupClosedWindows();
                    TaskbarApplicationEntryRegistry.Reconcile();
                    calculatorCloseRetainsOther = calculatorAClosed &&
                        !TryGet(calculatorA.Handle, out ApplicationInstance ignoredCalculatorA) &&
                        TryGet(calculatorB.Handle, out ApplicationInstance retainedCalculatorB) &&
                        TaskbarApplicationEntryRegistry.TryGet(calculatorB.Handle,
                            out TaskbarApplicationEntry retainedCalculatorEntry);
                    if (calculatorAClosed) calculatorA = null;

                    LaunchResult consoleResult;
                    bool consoleLaunched = Desktop.LaunchApplication(
                        LaunchRequest.ForAppId("gxos.builtin.console", null, null,
                            LaunchActivationIntent.Launch), out consoleResult);
                    bool consoleFound = consoleLaunched && consoleResult != null &&
                        consoleResult.Success &&
                        TryGet(consoleResult.InstanceHandle, out console);
                    bool consoleZeroWindow = consoleFound &&
                        CloseReusablePresentation(console);
                    LaunchResult consoleAgainResult;
                    bool consoleRelaunched = Desktop.LaunchApplication(
                        LaunchRequest.ForAppId("gxos.builtin.console", null, null,
                            LaunchActivationIntent.ActivateExisting),
                        out consoleAgainResult);
                    // Launch completion updates the authoritative instance;
                    // the taskbar entry is a render-time projection.  Mirror
                    // the normal taskbar draw boundary before observing the
                    // reattached presentation in this runtime diagnostic.
                    TaskbarApplicationEntryRegistry.Reconcile();
                    consoleReused = consoleZeroWindow && consoleRelaunched &&
                        consoleAgainResult != null && consoleAgainResult.Success &&
                        TryGet(consoleAgainResult.InstanceHandle, out consoleAgain) &&
                        consoleAgain.Handle == console.Handle &&
                        consoleAgain.OwnedWindowCount > 0 &&
                        TaskbarApplicationEntryRegistry.TryGet(console.Handle,
                            out TaskbarApplicationEntry consoleEntry);

                    LaunchResult imageResult;
                    bool imageLaunched = Desktop.LaunchApplication(
                        LaunchRequest.ForAppId("gxos.builtin.imageviewer", null,
                            null, LaunchActivationIntent.Launch),
                        out imageResult);
                    bool imageFound = imageLaunched && imageResult != null &&
                        imageResult.Success &&
                        TryGet(imageResult.InstanceHandle, out imageViewer);
                    bool imageZeroWindow = imageFound &&
                        CloseReusablePresentation(imageViewer);
                    LaunchResult imageAgainResult;
                    bool imageRelaunched = Desktop.LaunchApplication(
                        LaunchRequest.ForAppId("gxos.builtin.imageviewer", null,
                            null, LaunchActivationIntent.ActivateExisting),
                        out imageAgainResult);
                    TaskbarApplicationEntryRegistry.Reconcile();
                    imageViewerReused = imageZeroWindow && imageRelaunched &&
                        imageAgainResult != null && imageAgainResult.Success &&
                        TryGet(imageAgainResult.InstanceHandle, out imageViewerAgain) &&
                        imageViewerAgain.Handle == imageViewer.Handle &&
                        imageViewerAgain.OwnedWindowCount > 0 &&
                        TaskbarApplicationEntryRegistry.TryGet(imageViewer.Handle,
                            out TaskbarApplicationEntry imageEntry);
                    zeroWindowProjectionSuppressed = consoleZeroWindow &&
                        imageZeroWindow;
                }
            } catch {
                oneInstanceTwoWindows = false;
                sameInstanceSwitch = false;
                closeFirstRetainsSecond = false;
                finalWindowFollowsPolicy = false;
                calculatorMultipleInstance = false;
                consoleReused = false;
                imageViewerReused = false;
                zeroWindowProjectionSuppressed = false;
            } finally {
                if (multiWindow != null) TryTerminate(multiWindow,
                    "taskbar runtime multi-window cleanup");
                if (calculatorA != null) TryTerminate(calculatorA,
                    "taskbar runtime calculator A cleanup");
                if (calculatorB != null) TryTerminate(calculatorB,
                    "taskbar runtime calculator B cleanup");
                if (console != null) TryTerminate(console,
                    "taskbar runtime console cleanup");
                if (consoleAgain != null && consoleAgain != console)
                    TryTerminate(consoleAgain, "taskbar runtime console reuse cleanup");
                if (imageViewer != null) TryTerminate(imageViewer,
                    "taskbar runtime image viewer cleanup");
                if (imageViewerAgain != null && imageViewerAgain != imageViewer)
                    TryTerminate(imageViewerAgain,
                        "taskbar runtime image viewer reuse cleanup");
                CloseTaskbarRuntimeFixture(firstWindow);
                CloseTaskbarRuntimeFixture(secondWindow);
                WindowManager.CleanupClosedWindows();
                TaskbarApplicationEntryRegistry.Reconcile();
                staleTaskbarOwners = StaleOwnershipCount == 0 &&
                    TaskbarApplicationEntryRegistry.StaleOwnerCount == 0;
                bool fixtureBaselineRestored =
                    WindowManager.Windows != null && startingWindowCount >= 0 &&
                    WindowManager.Windows.Count == startingWindowCount &&
                    (firstWindow == null ||
                        !firstWindow.ApplicationInstanceHandle.IsValid) &&
                    (secondWindow == null ||
                        !secondWindow.ApplicationInstanceHandle.IsValid);
                bool phase7ZeroState = ActiveCount == 0 &&
                    SuspendedCount == 0 &&
                    ActiveApplicationHandle == ApplicationInstanceHandle.None &&
                    TaskbarApplicationEntryRegistry.EntryCount == 0 &&
                    StaleOwnershipCount == 0 &&
                    TaskbarApplicationEntryRegistry.StaleOwnerCount == 0;
                cleanup = phase7ZeroState && fixtureBaselineRestored;
                _lastTaskbarGroupingRuntimeCleanup = cleanup;
            }

            EmitTaskbarRuntimeMarker("ONE_INSTANCE_TWO_WINDOWS",
                oneInstanceTwoWindows);
            EmitTaskbarRuntimeMarker("SAME_INSTANCE_SWITCH",
                sameInstanceSwitch);
            EmitTaskbarRuntimeValue("SAME_INSTANCE_ACTIVATION_DELTA",
                sameInstanceActivationDelta);
            EmitTaskbarRuntimeValue("SEMANTIC_GROUP_COUNT", semanticGroupCount);
            EmitTaskbarRuntimeMarker("CLOSE_FIRST_RETAINS_SECOND",
                closeFirstRetainsSecond);
            EmitTaskbarRuntimeMarker("FINAL_WINDOW_POLICY",
                finalWindowFollowsPolicy);
            EmitTaskbarRuntimeMarker("CALCULATOR_MULTI_INSTANCE",
                calculatorMultipleInstance);
            EmitTaskbarRuntimeMarker("CALCULATOR_CLOSE_RETAINS_OTHER",
                calculatorCloseRetainsOther);
            EmitTaskbarRuntimeMarker("CALCULATOR_HANDLES_DISTINCT",
                calculatorHandlesDistinct);
            EmitTaskbarRuntimeValue("CALCULATOR_GROUP_COUNT",
                calculatorGroupCount);
            EmitTaskbarRuntimeMarker("CONSOLE_REUSE", consoleReused);
            EmitTaskbarRuntimeMarker("IMAGE_VIEWER_REUSE", imageViewerReused);
            EmitTaskbarRuntimeMarker("ZERO_WINDOW_PROJECTION_SUPPRESSED",
                zeroWindowProjectionSuppressed);
            EmitTaskbarRuntimeMarker("CLEANUP", cleanup);
            EmitTaskbarRuntimeMarker("STALE_TASKBAR_OWNERS", staleTaskbarOwners);
            EmitTaskbarRuntimeValue("STALE_TASKBAR_OWNERS_COUNT",
                TaskbarApplicationEntryRegistry.StaleOwnerCount);
            EmitTaskbarRuntimeMarker("GROUPING_DIAGNOSTIC",
                oneInstanceTwoWindows && sameInstanceSwitch &&
                closeFirstRetainsSecond && finalWindowFollowsPolicy &&
                calculatorMultipleInstance && calculatorCloseRetainsOther &&
                consoleReused &&
                imageViewerReused && zeroWindowProjectionSuppressed &&
                cleanup && staleTaskbarOwners);
            return oneInstanceTwoWindows && sameInstanceSwitch &&
                closeFirstRetainsSecond && finalWindowFollowsPolicy &&
                calculatorMultipleInstance && calculatorCloseRetainsOther &&
                consoleReused &&
                imageViewerReused && zeroWindowProjectionSuppressed &&
                cleanup && staleTaskbarOwners;
        }

        /// <summary>
        /// Bounded read-only observation proof used by Task Manager.  The
        /// observation API must not change lifecycle, ownership, projection,
        /// active focus, or the WindowManager collection.
        /// </summary>
        public static bool RunTaskManagerObservationDiagnostic() {
            ApplicationInstanceHandle activeBefore = ActiveApplicationHandle;
            int activeCountBefore = ActiveCount;
            int createdBefore = InstancesCreated;
            int reusedBefore = InstancesReused;
            int activatedBefore = InstancesActivated;
            int deactivatedBefore = InstancesDeactivated;
            int suspendedBefore = InstancesSuspended;
            int resumedBefore = InstancesResumed;
            int closeRequestsBefore = CloseRequests;
            int closeCancellationsBefore = CloseCancellations;
            int lifecycleFailuresBefore = LifecycleFailures;
            int invalidLifecycleBefore = InvalidLifecycleRequests;
            int staleLifecycleBefore = StaleLifecycleHandles;
            int terminatedBefore = InstancesTerminated;
            int failedBefore = FailedInstances;
            int attachBefore = WindowAttachCount;
            int detachBefore = WindowDetachCount;
            int duplicateAttachBefore = DuplicateWindowAttachCount;
            int staleOwnershipBefore = StaleOwnershipCount;
            int windowCountBefore = WindowManager.Windows == null
                ? -1 : WindowManager.Windows.Count;
            int entryCountBefore = TaskbarApplicationEntryRegistry.EntryCount;
            int observations = ObservationCount;
            int successfulObservations = 0;
            for (int i = 0; i < observations; i++) {
                ApplicationInstanceObservation observation;
                if (TryGetObservationAt(i, out observation)) successfulObservations++;
            }

            bool unchanged = activeBefore == ActiveApplicationHandle &&
                activeCountBefore == ActiveCount &&
                createdBefore == InstancesCreated &&
                reusedBefore == InstancesReused &&
                activatedBefore == InstancesActivated &&
                deactivatedBefore == InstancesDeactivated &&
                suspendedBefore == InstancesSuspended &&
                resumedBefore == InstancesResumed &&
                closeRequestsBefore == CloseRequests &&
                closeCancellationsBefore == CloseCancellations &&
                lifecycleFailuresBefore == LifecycleFailures &&
                invalidLifecycleBefore == InvalidLifecycleRequests &&
                staleLifecycleBefore == StaleLifecycleHandles &&
                terminatedBefore == InstancesTerminated &&
                failedBefore == FailedInstances &&
                attachBefore == WindowAttachCount &&
                detachBefore == WindowDetachCount &&
                duplicateAttachBefore == DuplicateWindowAttachCount &&
                staleOwnershipBefore == StaleOwnershipCount &&
                windowCountBefore == (WindowManager.Windows == null
                    ? -1 : WindowManager.Windows.Count) &&
                entryCountBefore == TaskbarApplicationEntryRegistry.EntryCount &&
                successfulObservations == observations;
#if UEFI_DIAGNOSTIC_APP_MODEL
            Program.MarkUefiAppModelDiagnostic(
                "TASKBAR_OBSERVATION_NO_MUTATION=" +
                (unchanged ? "PASS" : "FAIL") + ";count=" +
                observations.ToString());
#endif
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime(
                "TASKBAR_OBSERVATION_NO_MUTATION=" +
                (unchanged ? "PASS" : "FAIL") + ";count=" +
                observations.ToString());
#endif
            return unchanged;
        }

        private static bool CloseRuntimeInstance(ApplicationInstance instance) {
            if (instance == null) return true;
            ApplicationLifecycleResult result = RequestClose(instance.Handle,
                ApplicationCloseReason.ApplicationRequest);
            return result.Success || result.Code ==
                ApplicationLifecycleResultCode.CallbackFailed;
        }

        private static bool HandleAndTransitionTest(ref string firstFailure) {
            string id = "selftest.lifecycle";
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!TryBeginLaunch(id, ApplicationInstancePolicy.MultiInstance,
                    LaunchRequest.ForAppId(id, null, null,
                        LaunchActivationIntent.NewInstance), out instance,
                    out reused, out failure)) return false;
            bool unique = instance.Handle.IsValid && instance.DescriptorId !=
                instance.Handle.ToString() &&
                instance.LifecycleState == ApplicationInstanceLifecycleState.Loading;
            bool invalidRejected = !instance.TryTransition(
                ApplicationInstanceLifecycleState.Activated);
            bool completed = TryCompleteLaunch(instance, true, out failure) &&
                instance.LifecycleState == ApplicationInstanceLifecycleState.Activated;
            bool inactive = instance.TryTransition(
                ApplicationInstanceLifecycleState.Inactive);
            bool reactivated = TryActivate(instance, out failure) &&
                instance.LifecycleState == ApplicationInstanceLifecycleState.Activated;
            ApplicationInstanceHandle staleHandle = instance.Handle;
            bool terminated = TryTerminate(instance, "self-test termination") &&
                !TryGet(staleHandle, out ApplicationInstance ignored);
            ApplicationInstance replacement = null;
            bool replacementReused;
            bool replacementStarted = TryBeginLaunch(id,
                ApplicationInstancePolicy.MultiInstance,
                LaunchRequest.ForAppId(id, null, null,
                    LaunchActivationIntent.NewInstance), out replacement,
                out replacementReused, out failure);
            bool staleRejected = replacementStarted && replacement != null &&
                !replacementReused && replacement.Handle != staleHandle &&
                !TryGet(staleHandle, out ApplicationInstance staleInstance);
            if (replacement != null) TryTerminate(replacement,
                "self-test stale handle cleanup");
            return unique && invalidRejected && completed && inactive &&
                reactivated && terminated && staleRejected;
        }

        private static bool ReuseAndMultiInstanceTest(ref string firstFailure) {
            string reuseId = "selftest.reuse";
            ApplicationInstance first;
            ApplicationInstance second;
            bool reused;
            LaunchResult failure;
            if (!TryBeginLaunch(reuseId, ApplicationInstancePolicy.ReuseExisting,
                    LaunchRequest.ForAppId(reuseId, null, null,
                        LaunchActivationIntent.Launch), out first, out reused,
                    out failure) || reused ||
                !TryCompleteLaunch(first, true, out failure)) return false;
            if (!TryBeginLaunch(reuseId, ApplicationInstancePolicy.ReuseExisting,
                    LaunchRequest.ForAppId(reuseId, null, null,
                        LaunchActivationIntent.ActivateExisting), out second,
                    out reused, out failure) || !reused || first.Handle != second.Handle ||
                !TryCompleteLaunch(second, true, out failure)) return false;
            TryTerminate(first, "self-test reuse cleanup");

            string multiId = "selftest.multi";
            if (!TryBeginLaunch(multiId, ApplicationInstancePolicy.MultiInstance,
                    LaunchRequest.ForAppId(multiId, null, null,
                        LaunchActivationIntent.NewInstance), out first, out reused,
                    out failure) || !TryBeginLaunch(multiId,
                        ApplicationInstancePolicy.MultiInstance,
                        LaunchRequest.ForAppId(multiId, null, null,
                            LaunchActivationIntent.NewInstance), out second,
                        out reused, out failure) || reused || first.Handle == second.Handle) {
                if (first != null) TryTerminate(first, "self-test multi cleanup");
                if (second != null) TryTerminate(second, "self-test multi cleanup");
                return false;
            }
            TryTerminate(first, "self-test multi cleanup");
            TryTerminate(second, "self-test multi cleanup");
            return true;
        }

        private static bool FailureCleanupTest(ref string firstFailure) {
            string id = "selftest.failure";
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!TryBeginLaunch(id, ApplicationInstancePolicy.MultiInstance,
                    LaunchRequest.ForAppId(id, null, null,
                        LaunchActivationIntent.NewInstance), out instance, out reused,
                    out failure)) return false;
            ApplicationInstanceHandle handle = instance.Handle;
            FailLaunch(instance, false, "self-test initialization failure");
            return !TryGet(handle, out ApplicationInstance ignored) &&
                ActiveCount == 0;
        }

        private static bool RunWindowOwnershipSelfTest(ref string firstFailure) {
            if (WindowManager.Windows == null || Framebuffer.Graphics == null) {
                // The registry contract is still covered without a GUI surface;
                // real UEFI/runtime launches cover attach/detach in that mode.
                return true;
            }
            string id = "selftest.window";
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!TryBeginLaunch(id, ApplicationInstancePolicy.MultiInstance,
                    LaunchRequest.ForAppId(id, null, null,
                        LaunchActivationIntent.NewInstance), out instance, out reused,
                    out failure)) return false;
            OwnershipProbeWindow window = null;
            OwnershipProbeWindow secondWindow = null;
            bool result = false;
            try {
                window = new OwnershipProbeWindow();
                secondWindow = new OwnershipProbeWindow();
                bool attached = TryAttachWindow(instance, window) &&
                    TryAttachWindow(instance, secondWindow);
                bool duplicate = TryAttachWindow(instance, window);
                bool owned = instance.OwnedWindowCount == 2 &&
                    window.ApplicationInstanceHandle == instance.Handle &&
                    secondWindow.ApplicationInstanceHandle == instance.Handle;
                OnWindowClosed(window);
                bool firstDetached = instance.OwnedWindowCount == 1 &&
                    !window.ApplicationInstanceHandle.IsValid &&
                    TryGet(instance.Handle, out ApplicationInstance stillActive);
                OnWindowClosed(secondWindow);
                bool detached = !secondWindow.ApplicationInstanceHandle.IsValid &&
                    !TryGet(instance.Handle, out ApplicationInstance terminatedInstance);
                result = attached && duplicate && owned && firstDetached &&
                    detached;
            } catch {
                result = false;
            }
            if (window != null) {
                window.CloseForApplicationTermination();
            }
            if (secondWindow != null) {
                secondWindow.CloseForApplicationTermination();
            }
            WindowManager.CleanupClosedWindows();
            TryTerminate(instance, "self-test window cleanup");
            return result;
        }

        private sealed class OwnershipProbeWindow : Window {
            internal OwnershipProbeWindow() : base(24, 80, 160, 120) { }
            public override void OnDraw() { }
            public override void OnInput() { }
        }

        private sealed class TaskbarRuntimeProbeWindow : Window {
            internal TaskbarRuntimeProbeWindow() : base(40, 112, 160, 120) { }
            public override void OnDraw() { }
            public override void OnInput() { }
        }

        private static bool CloseReusablePresentation(ApplicationInstance instance) {
            if (instance == null || instance.OwnedWindowCount == 0) return false;
            Window window = instance.GetOwnedWindowAt(0);
            if (window == null) return false;
            window.CloseForApplicationTermination();
            WindowManager.CleanupClosedWindows();
            TaskbarApplicationEntryRegistry.Reconcile();
            ApplicationInstance retained;
            return TryGet(instance.Handle, out retained) &&
                retained.OwnedWindowCount == 0 &&
                !TaskbarApplicationEntryRegistry.TryGet(instance.Handle,
                    out TaskbarApplicationEntry ignoredEntry);
        }

        private static void CloseTaskbarRuntimeFixture(
                TaskbarRuntimeProbeWindow window) {
            if (window != null) window.CloseForApplicationTermination();
        }

        private static void EmitTaskbarRuntimeMarker(string name, bool passed) {
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("TASKBAR_" + name + "=" +
                (passed ? "PASS" : "FAIL"));
#endif
        }

        private static void EmitTaskbarRuntimeValue(string name, int value) {
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("TASKBAR_" + name + "=" +
                value.ToString());
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
    }
}
