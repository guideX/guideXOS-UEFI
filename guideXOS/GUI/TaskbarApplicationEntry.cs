using guideXOS.OS;
using guideXOS.Kernel.Drivers;

namespace guideXOS.GUI {
    /// <summary>
    /// Bounded, read-only taskbar presentation state for one semantic
    /// application instance.  ApplicationInstanceRegistry remains the owner
    /// of lifecycle and Window ownership.
    /// </summary>
    internal sealed class TaskbarApplicationEntry {
        private readonly Window[] _windows;
        private ApplicationInstanceHandle _instanceHandle;
        private string _descriptorId;
        private string _displayName;
        private string _resourceKey;
        private ApplicationInstanceLifecycleState _lifecycleState;
        private bool _isActive;
        private bool _isSuspended;
        private int _ownedWindowCount;
        private int _presentableWindowCount;
        private Window _activeWindow;
        private Window _mostRecentWindow;

        internal TaskbarApplicationEntry() {
            _windows = new Window[ApplicationInstance.MaxOwnedWindows];
        }

        public ApplicationInstanceHandle InstanceHandle { get { return _instanceHandle; } }
        public string DescriptorId { get { return _descriptorId; } }
        public string DisplayName { get { return _displayName; } }
        public string ResourceKey { get { return _resourceKey; } }
        public ApplicationInstanceLifecycleState LifecycleState {
            get { return _lifecycleState; }
        }
        public bool IsActive { get { return _isActive; } }
        public bool IsSuspended { get { return _isSuspended; } }
        public int OwnedWindowCount { get { return _ownedWindowCount; } }
        public int PresentableWindowCount { get { return _presentableWindowCount; } }
        public Window ActiveWindow { get { return _activeWindow; } }
        public Window MostRecentWindow { get { return _mostRecentWindow; } }

        internal bool IsOccupied { get { return _instanceHandle.IsValid; } }

        internal bool ContainsWindow(Window window) {
            if (window == null) return false;
            for (int i = 0; i < _ownedWindowCount; i++) {
                if (_windows[i] == window) return true;
            }
            return false;
        }

        internal void Reset() {
            for (int i = 0; i < _ownedWindowCount; i++) _windows[i] = null;
            _instanceHandle = ApplicationInstanceHandle.None;
            _descriptorId = null;
            _displayName = null;
            _resourceKey = null;
            _lifecycleState = ApplicationInstanceLifecycleState.Terminated;
            _isActive = false;
            _isSuspended = false;
            _ownedWindowCount = 0;
            _presentableWindowCount = 0;
            _activeWindow = null;
            _mostRecentWindow = null;
        }

        internal void Rebuild(ApplicationInstance instance, string displayName,
                              string resourceKey, out int attached,
                              out int detached, out int staleOwners) {
            attached = 0;
            detached = 0;
            staleOwners = 0;
            if (instance == null) {
                Reset();
                return;
            }

            for (int i = 0; i < instance.OwnedWindowCount; i++) {
                Window candidate = instance.GetOwnedWindowAt(i);
                if (!IsValidOwner(instance, candidate)) {
                    if (candidate != null) staleOwners++;
                    continue;
                }
                if (!ContainsWindow(candidate)) attached++;
            }
            for (int i = 0; i < _ownedWindowCount; i++) {
                if (!IsValidOwner(instance, _windows[i])) detached++;
            }

            Window previousActive = _activeWindow;
            Window previousMostRecent = _mostRecentWindow;
            for (int i = 0; i < _ownedWindowCount; i++) _windows[i] = null;
            _ownedWindowCount = 0;
            _presentableWindowCount = 0;
            Window firstPresentable = null;
            for (int i = 0; i < instance.OwnedWindowCount; i++) {
                Window candidate = instance.GetOwnedWindowAt(i);
                if (!IsValidOwner(instance, candidate)) continue;
                if (_ownedWindowCount >= _windows.Length) break;
                _windows[_ownedWindowCount++] = candidate;
                if (IsPresentable(candidate)) {
                    _presentableWindowCount++;
                    if (firstPresentable == null) firstPresentable = candidate;
                }
            }

            _instanceHandle = instance.Handle;
            _descriptorId = instance.DescriptorId;
            _displayName = displayName;
            _resourceKey = resourceKey;
            _lifecycleState = instance.LifecycleState;
            _isActive = instance.IsActivated;
            _isSuspended = instance.LifecycleState ==
                ApplicationInstanceLifecycleState.Suspended;
            _activeWindow = IsPresentableAndContained(previousActive)
                ? previousActive : firstPresentable;
            _mostRecentWindow = IsPresentableAndContained(previousMostRecent)
                ? previousMostRecent : _activeWindow;
        }

        internal void RecordWindowSelection(Window window) {
            if (!IsPresentableAndContained(window)) return;
            _activeWindow = window;
            _mostRecentWindow = window;
        }

        private bool IsPresentableAndContained(Window window) {
            return ContainsWindow(window) && IsPresentable(window);
        }

        private static bool IsPresentable(Window window) {
            return window != null && window.Visible && window.ShowInTaskbar;
        }

        private static bool IsValidOwner(ApplicationInstance instance,
                                         Window window) {
            return instance != null && window != null &&
                window.ApplicationInstanceHandle == instance.Handle &&
                instance.OwnsWindow(window);
        }
    }

    /// <summary>Phase 7 projection behavior assertions.</summary>
    internal static class TaskbarApplicationEntryRegistry {
        private static TaskbarApplicationEntry[] _entries;
        private static int _entryCount;
        private static int _entriesCreated;
        private static int _entriesRemoved;
        private static int _entriesReused;
        private static int _windowAttachCount;
        private static int _windowDetachCount;
        private static int _zeroWindowSuppressionCount;
        private static int _staleOwnerCount;
        private static int _multiWindowGroupCount;
        private static int _maximumGroupSize;
        private static int _sameInstanceWindowSwitchCount;
        private static int _crossInstanceWindowSwitchCount;

        public static int Capacity { get { return ApplicationInstanceRegistry.Capacity; } }
        public static int EntryCount { get { Initialize(); return _entryCount; } }
        public static int EntriesCreated { get { return _entriesCreated; } }
        public static int EntriesRemoved { get { return _entriesRemoved; } }
        public static int EntriesReused { get { return _entriesReused; } }
        public static int WindowAttachCount { get { return _windowAttachCount; } }
        public static int WindowDetachCount { get { return _windowDetachCount; } }
        public static int ZeroWindowSuppressionCount {
            get { return _zeroWindowSuppressionCount; }
        }
        public static int StaleOwnerCount { get { return _staleOwnerCount; } }
        public static int MultiWindowGroupCount { get { return _multiWindowGroupCount; } }
        public static int MaximumGroupSize { get { return _maximumGroupSize; } }
        public static int SameInstanceWindowSwitchCount {
            get { return _sameInstanceWindowSwitchCount; }
        }
        public static int CrossInstanceWindowSwitchCount {
            get { return _crossInstanceWindowSwitchCount; }
        }

        public static void Initialize() {
            if (_entries != null) return;
            ApplicationInstanceRegistry.Initialize();
            ApplicationDescriptorRegistry.Initialize();
            _entries = new TaskbarApplicationEntry[ApplicationInstanceRegistry.Capacity];
            for (int i = 0; i < _entries.Length; i++) {
                _entries[i] = new TaskbarApplicationEntry();
            }
        }

        public static TaskbarApplicationEntry GetAt(int index) {
            Initialize();
            return index >= 0 && index < _entries.Length &&
                _entries[index].IsOccupied ? _entries[index] : null;
        }

        public static bool TryGet(ApplicationInstanceHandle handle,
                                  out TaskbarApplicationEntry entry) {
            Initialize();
            entry = null;
            ApplicationInstance instance;
            if (!handle.IsValid || !ApplicationInstanceRegistry.TryGet(handle,
                    out instance) || IsTerminal(instance)) return false;
            entry = FindEntry(handle);
            return entry != null;
        }

        public static bool TryGetForWindow(Window window,
                                            out TaskbarApplicationEntry entry) {
            entry = null;
            if (window == null || !window.ApplicationInstanceHandle.IsValid) {
                return false;
            }
            if (!TryGet(window.ApplicationInstanceHandle, out entry)) {
                ApplicationInstance instance;
                if (!ApplicationInstanceRegistry.TryGet(
                        window.ApplicationInstanceHandle, out instance)) {
                    _staleOwnerCount++;
                }
                return false;
            }
            if (entry.ContainsWindow(window)) return true;
            entry = null;
            return false;
        }

        /// <summary>
        /// Rebuilds presentation state exclusively from bounded authoritative
        /// instance and owned-window slots.  It allocates no collections.
        /// </summary>
        public static void Reconcile() {
            Initialize();
            RemoveInvalidEntries();
            for (int i = 0; i < ApplicationInstanceRegistry.Capacity; i++) {
                ApplicationInstance instance = ApplicationInstanceRegistry.GetAt(i);
                if (instance == null || IsTerminal(instance)) continue;
                int staleOwners;
                int presentable = CountPresentableWindows(instance, out staleOwners);
                _staleOwnerCount += staleOwners;
                TaskbarApplicationEntry entry = FindEntry(instance.Handle);
                if (presentable == 0) {
                    if (entry != null) RemoveEntry(entry);
                    _zeroWindowSuppressionCount++;
                    continue;
                }

                if (entry == null) {
                    entry = FindFreeEntry();
                    if (entry == null) continue;
                    _entryCount++;
                    _entriesCreated++;
                } else {
                    _entriesReused++;
                }
                string displayName = instance.DescriptorId;
                string resourceKey = instance.DescriptorId;
                ApplicationDescriptor descriptor;
                if (ApplicationDescriptorRegistry.TryGetById(instance.DescriptorId,
                        out descriptor) && descriptor != null) {
                    displayName = descriptor.DisplayName;
                    resourceKey = descriptor.ResourceKey;
                }
                int attached;
                int detached;
                entry.Rebuild(instance, displayName, resourceKey, out attached,
                    out detached, out staleOwners);
                _windowAttachCount += attached;
                _windowDetachCount += detached;
                _staleOwnerCount += staleOwners;
                if (entry.OwnedWindowCount > 1) _multiWindowGroupCount++;
                if (entry.OwnedWindowCount > _maximumGroupSize) {
                    _maximumGroupSize = entry.OwnedWindowCount;
                }
            }
        }

        public static bool TryFocusWindow(ApplicationInstanceHandle handle,
                                          Window window,
                                          out ApplicationLifecycleResult result) {
            Initialize();
            ApplicationInstance instance;
            if (!ApplicationInstanceRegistry.TryValidateOwnedWindow(handle,
                    window, out instance)) {
                result = ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.NotFound, handle,
                    ApplicationInstanceLifecycleState.Terminated,
                    ApplicationCloseReason.ShellRequest,
                    "Taskbar Window owner is unavailable");
                return false;
            }
            Reconcile();
            TaskbarApplicationEntry entry;
            if (!TryGet(handle, out entry) || !entry.ContainsWindow(window) ||
                    !window.Visible || !window.ShowInTaskbar) {
                result = ApplicationLifecycleResult.Failed(
                    ApplicationLifecycleResultCode.InvalidState, handle,
                    instance.LifecycleState, ApplicationCloseReason.ShellRequest,
                    "Taskbar Window is not presentable");
                return false;
            }
            bool sameInstance = ApplicationInstanceRegistry.ActiveApplicationHandle ==
                handle;
            result = ApplicationInstanceRegistry.Activate(handle);
            if (!result.Success) return false;
            WindowManager.MoveToEnd(window);
            entry.RecordWindowSelection(window);
            if (sameInstance) _sameInstanceWindowSwitchCount++;
            else _crossInstanceWindowSwitchCount++;
            return true;
        }

        public static ApplicationLifecycleResult RequestApplicationClose(
                ApplicationInstanceHandle handle) {
            return ApplicationInstanceRegistry.RequestClose(handle,
                ApplicationCloseReason.ShellRequest);
        }

        private static int CountPresentableWindows(ApplicationInstance instance,
                                                   out int staleOwners) {
            staleOwners = 0;
            int presentable = 0;
            for (int i = 0; i < instance.OwnedWindowCount; i++) {
                Window window = instance.GetOwnedWindowAt(i);
                if (window == null) continue;
                if (window.ApplicationInstanceHandle != instance.Handle ||
                        !instance.OwnsWindow(window)) {
                    staleOwners++;
                    continue;
                }
                if (window.Visible && window.ShowInTaskbar) presentable++;
            }
            return presentable;
        }

        private static void RemoveInvalidEntries() {
            for (int i = 0; i < _entries.Length; i++) {
                TaskbarApplicationEntry entry = _entries[i];
                if (!entry.IsOccupied) continue;
                ApplicationInstance instance;
                if (!ApplicationInstanceRegistry.TryGet(entry.InstanceHandle,
                        out instance) || IsTerminal(instance)) {
                    RemoveEntry(entry);
                }
            }
        }

        private static TaskbarApplicationEntry FindEntry(
                ApplicationInstanceHandle handle) {
            for (int i = 0; i < _entries.Length; i++) {
                TaskbarApplicationEntry entry = _entries[i];
                if (entry.IsOccupied && entry.InstanceHandle == handle) return entry;
            }
            return null;
        }

        private static TaskbarApplicationEntry FindFreeEntry() {
            for (int i = 0; i < _entries.Length; i++) {
                if (!_entries[i].IsOccupied) return _entries[i];
            }
            return null;
        }

        private static void RemoveEntry(TaskbarApplicationEntry entry) {
            if (entry == null || !entry.IsOccupied) return;
            entry.Reset();
            if (_entryCount > 0) _entryCount--;
            _entriesRemoved++;
        }

        private static bool IsTerminal(ApplicationInstance instance) {
            return instance == null || instance.LifecycleState ==
                ApplicationInstanceLifecycleState.Terminated ||
                instance.LifecycleState == ApplicationInstanceLifecycleState.Failed;
        }

        public static bool RunSelfTest() {
            int passed = 0;
            int failed = 0;
            string firstFailure = null;

            bool oneInstanceOneWindowEntry;
            bool multiWindowSingleEntry;
            bool sameInstanceSwitchNoLifecycleChurn;
            bool closeOneRetainsGroup;
            bool closeFinalAppliesPolicy;
            bool zeroWindowReusableSuppressed;
            bool twoSameDescriptorEntriesIndependent;
            bool staleEntryRejected;
            bool cleanup;
            RunProjectionAssertions(out oneInstanceOneWindowEntry,
                out multiWindowSingleEntry, out sameInstanceSwitchNoLifecycleChurn,
                out closeOneRetainsGroup, out closeFinalAppliesPolicy,
                out zeroWindowReusableSuppressed,
                out twoSameDescriptorEntriesIndependent, out staleEntryRejected,
                out cleanup);

            Check(oneInstanceOneWindowEntry, "one instance one Window", ref passed, ref failed, ref firstFailure);
            Check(multiWindowSingleEntry, "one instance multiple Windows one group", ref passed, ref failed, ref firstFailure);
            Check(sameInstanceSwitchNoLifecycleChurn, "same instance Window switch", ref passed, ref failed, ref firstFailure);
            Check(closeOneRetainsGroup, "close one Window retains group", ref passed, ref failed, ref firstFailure);
            Check(closeFinalAppliesPolicy, "close final Window policy", ref passed, ref failed, ref firstFailure);
            Check(zeroWindowReusableSuppressed, "reusable zero-window suppression", ref passed, ref failed, ref firstFailure);
            Check(twoSameDescriptorEntriesIndependent, "same descriptor separate instances", ref passed, ref failed, ref firstFailure);
            Check(staleEntryRejected, "stale taskbar entry", ref passed, ref failed, ref firstFailure);
            Check(cleanup, "taskbar grouping cleanup", ref passed, ref failed, ref firstFailure);

            EmitSelfTestSummary(passed, failed, firstFailure);
            return failed == 0 && cleanup;
        }

        private static void RunProjectionAssertions(
                out bool oneInstanceOneWindowEntry,
                out bool multiWindowSingleEntry,
                out bool sameInstanceSwitchNoLifecycleChurn,
                out bool closeOneRetainsGroup,
                out bool closeFinalAppliesPolicy,
                out bool zeroWindowReusableSuppressed,
                out bool twoSameDescriptorEntriesIndependent,
                out bool staleEntryRejected, out bool cleanup) {
            oneInstanceOneWindowEntry = false;
            multiWindowSingleEntry = false;
            sameInstanceSwitchNoLifecycleChurn = false;
            closeOneRetainsGroup = false;
            closeFinalAppliesPolicy = false;
            zeroWindowReusableSuppressed = false;
            twoSameDescriptorEntriesIndependent = false;
            staleEntryRejected = false;
            cleanup = false;
            if (WindowManager.Windows == null || Framebuffer.Graphics == null) {
                return;
            }

            ApplicationInstance primary = null;
            ApplicationInstance reusable = null;
            ApplicationInstance firstDuplicate = null;
            ApplicationInstance secondDuplicate = null;
            TaskbarProjectionProbeWindow firstWindow = null;
            TaskbarProjectionProbeWindow secondWindow = null;
            TaskbarProjectionProbeWindow duplicateFirstWindow = null;
            TaskbarProjectionProbeWindow duplicateSecondWindow = null;
            ApplicationInstanceHandle staleHandle = ApplicationInstanceHandle.None;
            int startingActive = ApplicationInstanceRegistry.ActiveCount;
            try {
                LaunchResult failure;
                bool reused;
                bool started = ApplicationInstanceRegistry.TryBeginLaunch(
                    "selftest.phase7.primary", ApplicationInstancePolicy.MultiInstance,
                    LaunchRequest.ForAppId("selftest.phase7.primary", null, null,
                        LaunchActivationIntent.NewInstance), out primary, out reused,
                    out failure);
                firstWindow = new TaskbarProjectionProbeWindow();
                bool attached = started && !reused &&
                    ApplicationInstanceRegistry.TryAttachWindow(primary, firstWindow) &&
                    ApplicationInstanceRegistry.TryCompleteLaunch(primary, true, out failure);
                Reconcile();
                TaskbarApplicationEntry primaryEntry;
                bool primaryFound = TryGet(primary == null
                    ? ApplicationInstanceHandle.None : primary.Handle, out primaryEntry);
                ApplicationInstanceObservation observation =
                    default(ApplicationInstanceObservation);
                bool observationMatches = ApplicationInstanceRegistry.ObservationCount > 0 &&
                    ApplicationInstanceRegistry.TryGetObservationAt(0, out observation) &&
                    observation.Handle == primary.Handle && observation.OwnedWindowCount == 1;
                oneInstanceOneWindowEntry = attached && primaryFound &&
                    EntryCount == 1 && primaryEntry.OwnedWindowCount == 1 &&
                    primaryEntry.PresentableWindowCount == 1 && observationMatches;

                secondWindow = new TaskbarProjectionProbeWindow();
                attached = ApplicationInstanceRegistry.TryAttachWindow(primary, secondWindow);
                Reconcile();
                multiWindowSingleEntry = attached && TryGet(primary.Handle,
                    out primaryEntry) && EntryCount == 1 &&
                    primaryEntry.OwnedWindowCount == 2 &&
                    primaryEntry.PresentableWindowCount == 2 &&
                    TryGetForWindow(firstWindow, out TaskbarApplicationEntry firstEntry) &&
                    TryGetForWindow(secondWindow, out TaskbarApplicationEntry secondEntry) &&
                    firstEntry == secondEntry;

                int activations = ApplicationInstanceRegistry.InstancesActivated;
                int deactivations = ApplicationInstanceRegistry.InstancesDeactivated;
                ApplicationLifecycleResult focusResult;
                bool selected = TryFocusWindow(primary.Handle, secondWindow,
                    out focusResult);
                sameInstanceSwitchNoLifecycleChurn = selected && focusResult.Success &&
                    TryGet(primary.Handle, out primaryEntry) &&
                    primaryEntry.ActiveWindow == secondWindow &&
                    primaryEntry.MostRecentWindow == secondWindow &&
                    activations == ApplicationInstanceRegistry.InstancesActivated &&
                    deactivations == ApplicationInstanceRegistry.InstancesDeactivated;

                ApplicationInstanceRegistry.OnWindowClosed(firstWindow);
                Reconcile();
                closeOneRetainsGroup = TryGet(primary.Handle, out primaryEntry) &&
                    primaryEntry.OwnedWindowCount == 1 &&
                    primaryEntry.PresentableWindowCount == 1 &&
                    primaryEntry.ActiveWindow == secondWindow;
                firstWindow.CloseForApplicationTermination();
                WindowManager.CleanupClosedWindows();

                staleHandle = primary.Handle;
                ApplicationInstanceRegistry.OnWindowClosed(secondWindow);
                Reconcile();
                closeFinalAppliesPolicy = !ApplicationInstanceRegistry.TryGet(
                    staleHandle, out ApplicationInstance ignoredPrimary) &&
                    !TryGet(staleHandle, out TaskbarApplicationEntry ignoredEntry);
                secondWindow.CloseForApplicationTermination();
                WindowManager.CleanupClosedWindows();

                bool reusableStarted = ApplicationInstanceRegistry.TryBeginLaunch(
                    "selftest.phase7.reusable", ApplicationInstancePolicy.ReuseExisting,
                    LaunchRequest.ForAppId("selftest.phase7.reusable", null, null,
                        LaunchActivationIntent.NewInstance), out reusable, out reused,
                    out failure) && !reused &&
                    ApplicationInstanceRegistry.TryCompleteLaunch(reusable, false,
                        out failure);
                Reconcile();
                zeroWindowReusableSuppressed = reusableStarted &&
                    ApplicationInstanceRegistry.TryGet(reusable.Handle,
                        out ApplicationInstance retainedReusable) &&
                    !TryGet(reusable.Handle, out TaskbarApplicationEntry ignoredReusable);

                bool firstDuplicateStarted = ApplicationInstanceRegistry.TryBeginLaunch(
                    "selftest.phase7.duplicate", ApplicationInstancePolicy.MultiInstance,
                    LaunchRequest.ForAppId("selftest.phase7.duplicate", null, null,
                        LaunchActivationIntent.NewInstance), out firstDuplicate, out reused,
                    out failure) && ApplicationInstanceRegistry.TryCompleteLaunch(
                        firstDuplicate, false, out failure);
                bool secondDuplicateStarted = ApplicationInstanceRegistry.TryBeginLaunch(
                    "selftest.phase7.duplicate", ApplicationInstancePolicy.MultiInstance,
                    LaunchRequest.ForAppId("selftest.phase7.duplicate", null, null,
                        LaunchActivationIntent.NewInstance), out secondDuplicate, out reused,
                    out failure) && ApplicationInstanceRegistry.TryCompleteLaunch(
                        secondDuplicate, false, out failure);
                duplicateFirstWindow = new TaskbarProjectionProbeWindow();
                duplicateSecondWindow = new TaskbarProjectionProbeWindow();
                bool duplicatesAttached = firstDuplicateStarted && secondDuplicateStarted &&
                    ApplicationInstanceRegistry.TryAttachWindow(firstDuplicate,
                        duplicateFirstWindow) &&
                    ApplicationInstanceRegistry.TryAttachWindow(secondDuplicate,
                        duplicateSecondWindow);
                Reconcile();
                twoSameDescriptorEntriesIndependent = duplicatesAttached &&
                    firstDuplicate.Handle != secondDuplicate.Handle &&
                    TryGet(firstDuplicate.Handle, out TaskbarApplicationEntry firstDuplicateEntry) &&
                    TryGet(secondDuplicate.Handle, out TaskbarApplicationEntry secondDuplicateEntry) &&
                    firstDuplicateEntry != secondDuplicateEntry && EntryCount == 2;

                staleEntryRejected = !TryGet(staleHandle,
                    out TaskbarApplicationEntry ignoredStale) &&
                    !TryGetForWindow(secondWindow,
                        out TaskbarApplicationEntry ignoredStaleWindow);
            } catch {
                oneInstanceOneWindowEntry = false;
                multiWindowSingleEntry = false;
                sameInstanceSwitchNoLifecycleChurn = false;
                closeOneRetainsGroup = false;
                closeFinalAppliesPolicy = false;
                zeroWindowReusableSuppressed = false;
                twoSameDescriptorEntriesIndependent = false;
                staleEntryRejected = false;
            } finally {
                if (primary != null) ApplicationInstanceRegistry.TryTerminate(primary,
                    "phase7 primary cleanup");
                if (reusable != null) ApplicationInstanceRegistry.TryTerminate(reusable,
                    "phase7 reusable cleanup");
                if (firstDuplicate != null) ApplicationInstanceRegistry.TryTerminate(
                    firstDuplicate, "phase7 duplicate cleanup");
                if (secondDuplicate != null) ApplicationInstanceRegistry.TryTerminate(
                    secondDuplicate, "phase7 duplicate cleanup");
                CloseFixture(firstWindow);
                CloseFixture(secondWindow);
                CloseFixture(duplicateFirstWindow);
                CloseFixture(duplicateSecondWindow);
                WindowManager.CleanupClosedWindows();
                Reconcile();
                cleanup = ApplicationInstanceRegistry.ActiveCount == startingActive &&
                    EntryCount == 0 && StaleOwnerCount == 0;
            }
        }

        private static void CloseFixture(TaskbarProjectionProbeWindow window) {
            if (window != null) window.CloseForApplicationTermination();
        }

        private sealed class TaskbarProjectionProbeWindow : Window {
            internal TaskbarProjectionProbeWindow() : base(32, 96, 160, 120) { }
            public override void OnDraw() { }
            public override void OnInput() { }
        }

        private static void EmitSelfTestSummary(int passed, int failed,
                                                string firstFailure) {
            Program.MarkUefiAppModelDiagnostic(
                "TASKBAR_GROUPING_SELFTEST=passed=" + passed.ToString() +
                ";failed=" + failed.ToString() + ";result=" +
                (failed == 0 ? "PASS" : "FAIL") + ";first=" +
                (firstFailure ?? ""));
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
    }
}
