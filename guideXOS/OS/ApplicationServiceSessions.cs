using guideXOS.GUI;

namespace guideXOS.OS {
    /// <summary>
    /// Internal fixed request record.  The public handle and status expose
    /// only bounded values; the payload is consumed by the selected adapter in
    /// this address space and is never returned as a backend object.
    /// </summary>
    internal sealed class ApplicationServiceSessionRecord {
        internal bool Active;
        internal ApplicationServiceRequestHandle Handle;
        internal ApplicationInstanceHandle Owner;
        internal ApplicationServiceId ServiceId;
        internal bool Interactive;
        internal ApplicationServiceRequestState State;
        internal object Payload;
        internal object Result;

        internal ApplicationServiceRequestStatus<object> CreateStatus() {
            switch (State) {
                case ApplicationServiceRequestState.Pending:
                    return ApplicationServiceRequestStatus<object>.PendingStatus();
                case ApplicationServiceRequestState.Completed:
                    return ApplicationServiceRequestStatus<object>.CompletedStatus(
                        Result);
                case ApplicationServiceRequestState.Cancelled:
                    return ApplicationServiceRequestStatus<object>.CancelledStatus(
                        Result);
                default:
                    return ApplicationServiceRequestStatus<object>.FailedStatus(
                        Result);
            }
        }

        internal void Clear() {
            Active = false;
            Handle = ApplicationServiceRequestHandle.Invalid;
            Owner = ApplicationInstanceHandle.None;
            ServiceId = 0;
            Interactive = false;
            State = ApplicationServiceRequestState.Failed;
            Payload = null;
            Result = null;
        }
    }

    /// <summary>
    /// Semantic owner metadata for a service-created transient window.  The
    /// WindowManager owns the graphical object; this record keeps only the
    /// bounded requester and request identity needed for lifecycle cleanup and
    /// result routing.
    /// </summary>
    internal sealed class ApplicationServiceTransientWindowMetadata {
        internal readonly ApplicationInstanceHandle Owner;
        internal readonly ApplicationServiceRequestHandle RequestHandle;

        internal ApplicationServiceTransientWindowMetadata(
                ApplicationInstanceHandle owner,
                ApplicationServiceRequestHandle requestHandle) {
            Owner = owner;
            RequestHandle = requestHandle;
        }
    }

    internal sealed class ApplicationServiceTransientWindowRecord {
        internal bool Active;
        internal Window Window;
        internal ApplicationInstanceHandle Owner;
        internal ApplicationServiceRequestHandle RequestHandle;

        internal void Clear() {
            Active = false;
            Window = null;
            Owner = ApplicationInstanceHandle.None;
            RequestHandle = ApplicationServiceRequestHandle.Invalid;
        }
    }

    /// <summary>
    /// Fixed-capacity request-session storage.  This is deliberately separate
    /// from ApplicationInstanceRegistry: it observes instance identity and
    /// generation but never becomes lifecycle authority.
    /// </summary>
    internal static class ApplicationServiceSessionTable {
        internal const int MaxSessions = 16;
        internal const int MaxTransientWindows = 16;

        private static readonly ApplicationServiceSessionRecord[] _sessions =
            CreateRecords();
        private static readonly ApplicationServiceTransientWindowRecord[]
            _transientWindows = CreateTransientRecords();
        private static uint _nextGeneration = 1;

        internal static int TransientWindowCount {
            get {
                int count = 0;
                for (int i = 0; i < _transientWindows.Length; i++) {
                    if (_transientWindows[i].Active) count++;
                }
                return count;
            }
        }

        internal static int ActiveSessionCount {
            get {
                int count = 0;
                for (int i = 0; i < _sessions.Length; i++) {
                    if (_sessions[i].Active) count++;
                }
                return count;
            }
        }

        internal static int OrphanTransientWindowCount {
            get {
                int count = 0;
                for (int i = 0; i < _transientWindows.Length; i++) {
                    ApplicationServiceTransientWindowRecord record =
                        _transientWindows[i];
                    if (!record.Active) continue;
                    ApplicationInstance instance;
                    ApplicationServiceSessionRecord session;
                    if (record.Window == null ||
                            !ApplicationInstanceRegistry.TryGet(record.Owner,
                                out instance) ||
                            !TryGet(record.RequestHandle, out session)) count++;
                }
                return count;
            }
        }

        internal static void Reset() {
            for (int i = 0; i < _transientWindows.Length; i++) {
                ApplicationServiceTransientWindowRecord record =
                    _transientWindows[i];
                if (!record.Active) continue;
                if (record.Window != null) {
                    record.Window.CloseForApplicationTermination();
                    record.Window.ClearServiceSession();
                }
                record.Clear();
            }
            for (int i = 0; i < _sessions.Length; i++) {
                _sessions[i].Clear();
            }
            _nextGeneration = 1;
        }

        internal static bool RegisterTransientWindow(
                Window window, ApplicationInstanceHandle owner,
                ApplicationServiceRequestHandle requestHandle) {
            if (window == null || !owner.IsValid || !requestHandle.IsValid) {
                return false;
            }
            ApplicationInstance instance;
            ApplicationServiceSessionRecord session;
            if (!ApplicationInstanceRegistry.TryGet(owner, out instance) ||
                    !TryGet(requestHandle, out session) ||
                    session.Owner != owner || session.State !=
                        ApplicationServiceRequestState.Pending) return false;
            for (int i = 0; i < _transientWindows.Length; i++) {
                ApplicationServiceTransientWindowRecord existing =
                    _transientWindows[i];
                if (existing.Active && existing.Window == window) return false;
            }
            int free = FindFreeTransientWindow();
            if (free < 0) return false;
            ApplicationServiceTransientWindowRecord record =
                _transientWindows[free];
            record.Active = true;
            record.Window = window;
            record.Owner = owner;
            record.RequestHandle = requestHandle;
            window.SetServiceSession(owner, requestHandle);
            return true;
        }

        internal static bool TryGetTransientOwner(Window window,
                out ApplicationServiceTransientWindowMetadata metadata) {
            metadata = null;
            if (window == null) return false;
            for (int i = 0; i < _transientWindows.Length; i++) {
                ApplicationServiceTransientWindowRecord record =
                    _transientWindows[i];
                if (record.Active && record.Window == window) {
                    metadata = new ApplicationServiceTransientWindowMetadata(
                        record.Owner, record.RequestHandle);
                    return true;
                }
            }
            return false;
        }

        internal static bool ReleaseTransientWindow(Window window) {
            if (window == null) return false;
            for (int i = 0; i < _transientWindows.Length; i++) {
                ApplicationServiceTransientWindowRecord record =
                    _transientWindows[i];
                if (!record.Active || record.Window != window) continue;
                record.Clear();
                window.ClearServiceSession();
                return true;
            }
            window.ClearServiceSession();
            return false;
        }

        internal static bool CloseTransientForRequest(
                ApplicationServiceRequestHandle requestHandle) {
            for (int i = 0; i < _transientWindows.Length; i++) {
                ApplicationServiceTransientWindowRecord record =
                    _transientWindows[i];
                if (!record.Active || record.RequestHandle != requestHandle) {
                    continue;
                }
                Window window = record.Window;
                if (window != null) window.CloseForApplicationTermination();
                if (window != null) ReleaseTransientWindow(window);
                else record.Clear();
                return true;
            }
            return false;
        }

        internal static bool ConsumeTerminal(
                ApplicationServiceRequestHandle requestHandle) {
            ApplicationServiceSessionRecord session;
            if (!TryGet(requestHandle, out session) ||
                    session.State == ApplicationServiceRequestState.Pending) {
                return false;
            }
            CloseTransientForRequest(requestHandle);
            session.Clear();
            return true;
        }

        internal static ApplicationServiceResult CancelPending(
                ApplicationServiceRequestHandle requestHandle, object value) {
            ApplicationServiceSessionRecord session;
            if (!TryGet(requestHandle, out session)) {
                return ApplicationServiceResult.InvalidContextResult();
            }
            if (session.State != ApplicationServiceRequestState.Pending) {
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.InvalidState,
                    "Application service request is already terminal");
            }
            session.State = ApplicationServiceRequestState.Cancelled;
            session.Result = value;
            return ApplicationServiceResult.SuccessResult();
        }

        internal static ApplicationServiceResult Begin(
                ApplicationInstanceHandle owner,
                ApplicationServiceId serviceId, object payload,
                bool interactive,
                out ApplicationServiceRequestHandle handle) {
            handle = ApplicationServiceRequestHandle.Invalid;
            if (!owner.IsValid || !ApplicationServiceNames.IsKnown(serviceId)) {
                return ApplicationServiceResult.InvalidRequestResult();
            }
            if (interactive && HasOutstandingInteractive(owner)) {
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.Conflict,
                    "An interactive application service request is outstanding");
            }

            int free = FindFree();
            if (free < 0) {
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.ResourceUnavailable,
                    "Application service request capacity is exhausted");
            }
            uint generation = _nextGeneration++;
            if (generation == 0) generation = _nextGeneration++;
            handle = ApplicationServiceRequestHandle.Create(
                serviceId, (uint)(free + 1), generation);
            ApplicationServiceSessionRecord session = _sessions[free];
            session.Active = true;
            session.Handle = handle;
            session.Owner = owner;
            session.ServiceId = serviceId;
            session.Interactive = interactive;
            session.State = ApplicationServiceRequestState.Pending;
            session.Payload = payload;
            session.Result = null;
            return ApplicationServiceResult.SuccessResult();
        }

        internal static bool TryGet(ApplicationServiceRequestHandle handle,
                                    out ApplicationServiceSessionRecord session) {
            session = null;
            if (!handle.IsValid || handle.Slot == 0 ||
                    handle.Slot > (uint)_sessions.Length) return false;
            ApplicationServiceSessionRecord candidate =
                _sessions[(int)handle.Slot - 1];
            if (!candidate.Active || candidate.Handle != handle) return false;
            session = candidate;
            return true;
        }

        internal static ApplicationServiceResult Complete(
                ApplicationServiceRequestHandle handle, object value) {
            ApplicationServiceSessionRecord session;
            if (!TryGet(handle, out session)) {
                return ApplicationServiceResult.InvalidContextResult();
            }
            if (session.State != ApplicationServiceRequestState.Pending) {
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.InvalidState,
                    "Application service request is already terminal");
            }
            session.Result = value;
            session.State = ApplicationServiceRequestState.Completed;
            return ApplicationServiceResult.SuccessResult();
        }

        internal static void Cancel(ApplicationServiceSessionRecord session) {
            if (session == null) return;
            session.State = ApplicationServiceRequestState.Cancelled;
            session.Result = null;
            session.Clear();
        }

        internal static int CleanupForInstance(
                ApplicationInstanceHandle owner, string reason) {
            int cleaned = 0;
            for (int i = 0; i < _transientWindows.Length; i++) {
                ApplicationServiceTransientWindowRecord record =
                    _transientWindows[i];
                if (!record.Active || record.Owner != owner) continue;
                if (record.Window != null) {
                    record.Window.CloseForApplicationTermination();
                    // Keep the Window's transient classification until the
                    // WindowManager removes and disposes it.  The session
                    // record itself is cleared now, so no new request can
                    // observe a terminating owner, while Dispose can still
                    // take the service-window cleanup path.
                    record.Clear();
                } else {
                    record.Clear();
                }
                cleaned++;
            }
            for (int i = 0; i < _sessions.Length; i++) {
                ApplicationServiceSessionRecord session = _sessions[i];
                if (session.Active && session.Owner == owner) {
                    session.Clear();
                    cleaned++;
                }
            }
            return cleaned;
        }

        private static bool HasOutstandingInteractive(
                ApplicationInstanceHandle owner) {
            for (int i = 0; i < _sessions.Length; i++) {
                ApplicationServiceSessionRecord session = _sessions[i];
                if (session.Active && session.Interactive &&
                        session.Owner == owner) return true;
            }
            return false;
        }

        private static int FindFree() {
            for (int i = 0; i < _sessions.Length; i++) {
                if (!_sessions[i].Active) return i;
            }
            return -1;
        }

        private static int FindFreeTransientWindow() {
            for (int i = 0; i < _transientWindows.Length; i++) {
                if (!_transientWindows[i].Active) return i;
            }
            return -1;
        }

        private static ApplicationServiceSessionRecord[] CreateRecords() {
            ApplicationServiceSessionRecord[] records =
                new ApplicationServiceSessionRecord[MaxSessions];
            for (int i = 0; i < records.Length; i++) {
                records[i] = new ApplicationServiceSessionRecord();
                records[i].Clear();
            }
            return records;
        }

        private static ApplicationServiceTransientWindowRecord[]
                CreateTransientRecords() {
            ApplicationServiceTransientWindowRecord[] records =
                new ApplicationServiceTransientWindowRecord[MaxTransientWindows];
            for (int i = 0; i < records.Length; i++) {
                records[i] = new ApplicationServiceTransientWindowRecord();
                records[i].Clear();
            }
            return records;
        }
    }
}
