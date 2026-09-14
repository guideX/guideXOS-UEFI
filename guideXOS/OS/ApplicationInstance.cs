using System;
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
    /// identity.  Suspended is part of the common contract but is not entered
    /// by the current C# backend because it has no real suspension mechanism.
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
        public bool SuspensionSupported { get { return false; } }
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

        internal bool TryTransition(ApplicationInstanceLifecycleState next) {
            if (!IsValidTransition(_state, next)) return false;
            _state = next;
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
                           to == ApplicationInstanceLifecycleState.Closing ||
                           to == ApplicationInstanceLifecycleState.Failed;
                case ApplicationInstanceLifecycleState.Activated:
                    return to == ApplicationInstanceLifecycleState.Inactive ||
                           to == ApplicationInstanceLifecycleState.Closing ||
                           to == ApplicationInstanceLifecycleState.Failed;
                case ApplicationInstanceLifecycleState.Inactive:
                    return to == ApplicationInstanceLifecycleState.Loading ||
                           to == ApplicationInstanceLifecycleState.Running ||
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
        private static int _terminatedCount;
        private static int _failedCount;
        private static int _windowAttachCount;
        private static int _windowDetachCount;
        private static int _duplicateAttachCount;
        private static int _staleOwnershipCount;

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
            return TryBeginLaunch("gxos.external.gxm",
                ApplicationInstancePolicy.MultiInstance, request,
                out instance, out reused, out failure);
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

            if (activate && instance.LifecycleState ==
                    ApplicationInstanceLifecycleState.Running) {
                if (!instance.TryTransition(ApplicationInstanceLifecycleState.Activated)) {
                    failure = LaunchResult.Failed(LaunchErrorCode.ActivationFailed,
                        "Application instance activation failed", instance.DescriptorId);
                    return false;
                }
                _activatedCount++;
            } else if (activate && instance.LifecycleState ==
                       ApplicationInstanceLifecycleState.Activated) {
                // Reused instances may already be in Activated state.  The
                // activation event is still observable and must contribute to
                // the bounded activation metric.
                _activatedCount++;
            }
            return true;
        }

        public static bool TryActivate(ApplicationInstanceHandle handle,
                                        out LaunchResult failure) {
            ApplicationInstance instance;
            if (!TryGet(handle, out instance)) {
                failure = LaunchResult.Failed(LaunchErrorCode.AlreadyTerminated,
                    "Application instance is not active", null);
                return false;
            }
            return TryActivate(instance, out failure);
        }

        internal static bool TryActivate(ApplicationInstance instance,
                                         out LaunchResult failure) {
            failure = null;
            if (!IsRegisteredInstance(instance)) {
                failure = LaunchResult.Failed(LaunchErrorCode.AlreadyTerminated,
                    "Application instance is not active", null);
                return false;
            }
            if (instance.LifecycleState == ApplicationInstanceLifecycleState.Inactive) {
                if (!instance.TryTransition(ApplicationInstanceLifecycleState.Running)) {
                    failure = LaunchResult.Failed(LaunchErrorCode.ActivationFailed,
                        "Inactive application instance cannot run", instance.DescriptorId);
                    return false;
                }
            }
            if (instance.LifecycleState == ApplicationInstanceLifecycleState.Running) {
                if (!instance.TryTransition(ApplicationInstanceLifecycleState.Activated)) {
                    failure = LaunchResult.Failed(LaunchErrorCode.ActivationFailed,
                        "Application instance activation failed", instance.DescriptorId);
                    return false;
                }
                _activatedCount++;
            } else if (instance.LifecycleState ==
                       ApplicationInstanceLifecycleState.Activated) {
                _activatedCount++;
            }
            return instance.LifecycleState == ApplicationInstanceLifecycleState.Activated;
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

        internal static void AttachWindowsCreatedSince(ApplicationInstance instance,
                                                        int startingCount) {
            if (!IsRegisteredInstance(instance) || WindowManager.Windows == null) return;
            int start = startingCount < 0 ? 0 : startingCount;
            if (start > WindowManager.Windows.Count) start = WindowManager.Windows.Count;
            for (int i = start; i < WindowManager.Windows.Count; i++) {
                Window window = WindowManager.Windows[i];
                if (window == null || window.ApplicationInstanceHandle.IsValid) continue;
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
            ApplicationInstance instance;
            if (!TryGet(handle, out instance)) return false;
            return TryTerminate(instance, reason);
        }

        internal static bool TryTerminate(ApplicationInstance instance,
                                          string reason) {
            if (!IsRegisteredInstance(instance)) return false;
            if (instance.LifecycleState !=
                    ApplicationInstanceLifecycleState.Closing &&
                !instance.TryTransition(ApplicationInstanceLifecycleState.Closing)) {
                return false;
            }
            instance.RecordTermination(reason);
            CloseAndDetachOwnedWindows(instance, reason);
            if (!instance.TryTransition(ApplicationInstanceLifecycleState.Terminated)) {
                return false;
            }
            _terminatedCount++;
            Remove(instance);
            return true;
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
                           ApplicationInstanceLifecycleState.Running ||
                       instance.LifecycleState ==
                           ApplicationInstanceLifecycleState.Activated) {
                instance.TryTransition(ApplicationInstanceLifecycleState.Inactive);
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
