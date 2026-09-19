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
    /// Fixed-capacity request-session storage.  This is deliberately separate
    /// from ApplicationInstanceRegistry: it observes instance identity and
    /// generation but never becomes lifecycle authority.
    /// </summary>
    internal static class ApplicationServiceSessionTable {
        internal const int MaxSessions = 16;

        private static readonly ApplicationServiceSessionRecord[] _sessions =
            CreateRecords();
        private static uint _nextGeneration = 1;

        internal static void Reset() {
            for (int i = 0; i < _sessions.Length; i++) {
                _sessions[i].Clear();
            }
            _nextGeneration = 1;
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

        private static ApplicationServiceSessionRecord[] CreateRecords() {
            ApplicationServiceSessionRecord[] records =
                new ApplicationServiceSessionRecord[MaxSessions];
            for (int i = 0; i < records.Length; i++) {
                records[i] = new ApplicationServiceSessionRecord();
                records[i].Clear();
            }
            return records;
        }
    }
}
