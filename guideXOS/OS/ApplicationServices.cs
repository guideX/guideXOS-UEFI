using System;

namespace guideXOS.OS {
    /// <summary>
    /// Fixed application-platform service identifiers.  The numeric values are
    /// stable contract values; the registry does not infer services from
    /// arbitrary type names or construct a dependency graph.
    /// </summary>
    public enum ApplicationServiceId {
        Notifications = 1,
        Settings = 2,
        SystemInformation = 3,
        Dialogs = 4,
        OpenFile = 5,
        SaveFile = 6,
        Shell = 7,
        Resources = 8,
        Storage = 9,
        Clipboard = 10
    }

    public static class ApplicationServiceNames {
        public static string For(ApplicationServiceId id) {
            switch (id) {
                case ApplicationServiceId.Notifications: return "notifications";
                case ApplicationServiceId.Settings: return "settings";
                case ApplicationServiceId.SystemInformation: return "system-information";
                case ApplicationServiceId.Dialogs: return "dialogs";
                case ApplicationServiceId.OpenFile: return "open-file";
                case ApplicationServiceId.SaveFile: return "save-file";
                case ApplicationServiceId.Shell: return "shell";
                case ApplicationServiceId.Resources: return "resources";
                case ApplicationServiceId.Storage: return "storage";
                case ApplicationServiceId.Clipboard: return "clipboard";
                default: return "unknown";
            }
        }

        public static bool IsKnown(ApplicationServiceId id) {
            return id == ApplicationServiceId.Notifications ||
                   id == ApplicationServiceId.Settings ||
                   id == ApplicationServiceId.SystemInformation ||
                   id == ApplicationServiceId.Dialogs ||
                   id == ApplicationServiceId.OpenFile ||
                   id == ApplicationServiceId.SaveFile ||
                   id == ApplicationServiceId.Shell ||
                   id == ApplicationServiceId.Resources ||
                   id == ApplicationServiceId.Storage ||
                   id == ApplicationServiceId.Clipboard;
        }
    }

    public enum ApplicationServiceResultCode {
        Success = 0,
        InvalidContext,
        InvalidRequest,
        NotFound,
        ResourceUnavailable,
        PermissionDenied,
        Unsupported,
        Conflict,
        Cancelled,
        InvalidState,
        UnsupportedTarget,
        BackendFailure
    }

    /// <summary>
    /// Bounded, generation-safe identity for an application service request.
    /// The value contains no callback, window, renderer, or backend object.
    /// </summary>
    public readonly struct ApplicationServiceRequestHandle {
        private readonly ApplicationServiceId _serviceId;
        private readonly uint _slot;
        private readonly uint _generation;

        private ApplicationServiceRequestHandle(ApplicationServiceId serviceId,
                                                 uint slot, uint generation) {
            _serviceId = serviceId;
            _slot = slot;
            _generation = generation;
        }

        internal static ApplicationServiceRequestHandle Create(
                ApplicationServiceId serviceId, uint slot, uint generation) {
            if (!ApplicationServiceNames.IsKnown(serviceId) ||
                    slot == 0 || generation == 0) return Invalid;
            return new ApplicationServiceRequestHandle(serviceId, slot,
                generation);
        }

        public static ApplicationServiceRequestHandle Invalid {
            get { return new ApplicationServiceRequestHandle(0, 0, 0); }
        }

        public ApplicationServiceId ServiceId { get { return _serviceId; } }
        public uint Slot { get { return _slot; } }
        public uint Generation { get { return _generation; } }
        public bool IsValid {
            get { return _slot != 0 && _generation != 0 &&
                         ApplicationServiceNames.IsKnown(_serviceId); }
        }

        public bool Equals(ApplicationServiceRequestHandle other) {
            return _serviceId == other._serviceId && _slot == other._slot &&
                   _generation == other._generation;
        }

        public override bool Equals(object obj) {
            return obj is ApplicationServiceRequestHandle &&
                   Equals((ApplicationServiceRequestHandle)obj);
        }

        public override int GetHashCode() {
            return ((int)_serviceId * 397) ^ (int)_slot ^ (int)_generation;
        }

        public static bool operator ==(ApplicationServiceRequestHandle left,
                                       ApplicationServiceRequestHandle right) {
            return left.Equals(right);
        }

        public static bool operator !=(ApplicationServiceRequestHandle left,
                                       ApplicationServiceRequestHandle right) {
            return !left.Equals(right);
        }
    }

    public enum ApplicationServiceRequestState {
        Pending = 0,
        Completed,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Bounded status payload for observing a request session.  Concrete
    /// services provide the payload type; no GUI implementation type crosses
    /// this boundary.
    /// </summary>
    public sealed class ApplicationServiceRequestStatus<T> {
        private ApplicationServiceRequestStatus(
                ApplicationServiceRequestState state, T value) {
            State = state;
            Value = value;
        }

        public ApplicationServiceRequestState State { get; private set; }
        public T Value { get; private set; }
        public bool IsPending {
            get { return State == ApplicationServiceRequestState.Pending; }
        }
        public bool IsTerminal { get { return !IsPending; } }

        internal static ApplicationServiceRequestStatus<T> PendingStatus() {
            return new ApplicationServiceRequestStatus<T>(
                ApplicationServiceRequestState.Pending, default(T));
        }

        internal static ApplicationServiceRequestStatus<T> CompletedStatus(
                T value) {
            return new ApplicationServiceRequestStatus<T>(
                ApplicationServiceRequestState.Completed, value);
        }

        internal static ApplicationServiceRequestStatus<T> CancelledStatus(
                T value) {
            return new ApplicationServiceRequestStatus<T>(
                ApplicationServiceRequestState.Cancelled, value);
        }

        internal static ApplicationServiceRequestStatus<T> FailedStatus(
                T value) {
            return new ApplicationServiceRequestStatus<T>(
                ApplicationServiceRequestState.Failed, value);
        }
    }

    public enum ApplicationDialogKind {
        Information = 0,
        Error,
        Confirmation
    }

    public enum ApplicationDialogButtonSet {
        Acknowledge = 0,
        AcceptRejectCancel
    }

    public enum ApplicationDialogOutcome {
        Accepted = 0,
        Rejected,
        Cancelled,
        Closed,
        BackendFailure
    }

    public sealed class ApplicationDialogRequest {
        public const int MaxTitleLength = 64;
        public const int MaxBodyLength = 256;

        private ApplicationDialogRequest(ApplicationDialogKind kind,
                                         string title, string body,
                                         ApplicationDialogButtonSet buttons) {
            Kind = kind;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            ButtonSet = buttons;
            IsValid = IsValidKind(kind) && IsValidButtonSet(buttons) &&
                ApplicationServiceContext.IsBoundedText(
                    Title, MaxTitleLength, false) &&
                ApplicationServiceContext.IsBoundedText(
                    Body, MaxBodyLength, true) &&
                IsValidButtonCombination(kind, buttons);
        }

        public ApplicationDialogKind Kind { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public ApplicationDialogButtonSet ButtonSet { get; private set; }
        public bool IsValid { get; private set; }

        public static ApplicationDialogRequest Create(
                ApplicationDialogKind kind, string title, string body,
                ApplicationDialogButtonSet buttons) {
            return new ApplicationDialogRequest(kind, title, body, buttons);
        }

        private static bool IsValidKind(ApplicationDialogKind kind) {
            return kind == ApplicationDialogKind.Information ||
                   kind == ApplicationDialogKind.Error ||
                   kind == ApplicationDialogKind.Confirmation;
        }

        private static bool IsValidButtonSet(ApplicationDialogButtonSet buttons) {
            return buttons == ApplicationDialogButtonSet.Acknowledge ||
                   buttons == ApplicationDialogButtonSet.AcceptRejectCancel;
        }

        private static bool IsValidButtonCombination(
                ApplicationDialogKind kind, ApplicationDialogButtonSet buttons) {
            return (kind == ApplicationDialogKind.Confirmation &&
                    buttons == ApplicationDialogButtonSet.AcceptRejectCancel) ||
                   (kind != ApplicationDialogKind.Confirmation &&
                    buttons == ApplicationDialogButtonSet.Acknowledge);
        }
    }

    public sealed class ApplicationDialogResult {
        private ApplicationDialogResult(ApplicationDialogOutcome outcome) {
            Outcome = outcome;
        }

        public ApplicationDialogOutcome Outcome { get; private set; }

        internal static ApplicationDialogResult From(
                ApplicationDialogOutcome outcome) {
            return new ApplicationDialogResult(outcome);
        }
    }

    public sealed class OpenFileRequest {
        public const int MaxStartingLocationLength = 1024;

        private OpenFileRequest(string startingLocation) {
            StartingLocation = startingLocation ?? string.Empty;
            IsValid = ApplicationServiceContext.IsBoundedText(
                StartingLocation,
                MaxStartingLocationLength, true);
        }

        public string StartingLocation { get; private set; }
        public bool IsValid { get; private set; }

        public static OpenFileRequest Create(string startingLocation) {
            return new OpenFileRequest(startingLocation);
        }
    }

    public sealed class SaveFileRequest {
        public const int MaxStartingLocationLength = 1024;
        public const int MaxSuggestedFileNameLength = 128;

        private SaveFileRequest(string startingLocation, string fileName) {
            StartingLocation = startingLocation ?? string.Empty;
            SuggestedFileName = fileName ?? string.Empty;
            IsValid = ApplicationServiceContext.IsBoundedText(
                StartingLocation,
                    MaxStartingLocationLength, true) &&
                ApplicationServiceContext.IsBoundedText(
                    SuggestedFileName,
                    MaxSuggestedFileNameLength, true);
        }

        public string StartingLocation { get; private set; }
        public string SuggestedFileName { get; private set; }
        public bool IsValid { get; private set; }

        public static SaveFileRequest Create(string startingLocation,
                                             string fileName) {
            return new SaveFileRequest(startingLocation, fileName);
        }
    }

    public enum ApplicationShellOpenTargetKind {
        ApplicationId = 0,
        Alias,
        Document,
        ShellObject,
        TypedShellAction
    }

    public sealed class ApplicationShellOpenRequest {
        public const int MaxTargetLength = 1024;

        private ApplicationShellOpenRequest(
                ApplicationShellOpenTargetKind kind, string target) {
            TargetKind = kind;
            Target = target ?? string.Empty;
            IsValid = IsValidKind(kind) &&
                ApplicationServiceContext.IsBoundedText(
                    Target, MaxTargetLength, false);
        }

        public ApplicationShellOpenTargetKind TargetKind { get; private set; }
        public string Target { get; private set; }
        public bool IsValid { get; private set; }

        public static ApplicationShellOpenRequest ForApplicationId(
                string appId) {
            return new ApplicationShellOpenRequest(
                ApplicationShellOpenTargetKind.ApplicationId, appId);
        }

        public static ApplicationShellOpenRequest ForAlias(string alias) {
            return new ApplicationShellOpenRequest(
                ApplicationShellOpenTargetKind.Alias, alias);
        }

        public static ApplicationShellOpenRequest ForDocument(string path) {
            return new ApplicationShellOpenRequest(
                ApplicationShellOpenTargetKind.Document, path);
        }

        public static ApplicationShellOpenRequest ForShellObject(
                string shellObjectId) {
            return new ApplicationShellOpenRequest(
                ApplicationShellOpenTargetKind.ShellObject, shellObjectId);
        }

        public static ApplicationShellOpenRequest ForTypedShellAction(
                string actionId) {
            return new ApplicationShellOpenRequest(
                ApplicationShellOpenTargetKind.TypedShellAction, actionId);
        }

        private static bool IsValidKind(ApplicationShellOpenTargetKind kind) {
            return kind == ApplicationShellOpenTargetKind.ApplicationId ||
                   kind == ApplicationShellOpenTargetKind.Alias ||
                   kind == ApplicationShellOpenTargetKind.Document ||
                   kind == ApplicationShellOpenTargetKind.ShellObject ||
                   kind == ApplicationShellOpenTargetKind.TypedShellAction;
        }
    }

    /// <summary>
    /// Bounded service result for operations without a payload.
    /// </summary>
    public sealed class ApplicationServiceResult {
        public const int MaxDiagnosticLength = 192;

        public ApplicationServiceResultCode Code { get; private set; }
        public string BoundedDiagnostic { get; private set; }
        public bool Succeeded {
            get { return Code == ApplicationServiceResultCode.Success; }
        }

        private ApplicationServiceResult(ApplicationServiceResultCode code,
                                         string diagnostic) {
            Code = code;
            BoundedDiagnostic = BoundDiagnostic(diagnostic);
        }

        public static ApplicationServiceResult SuccessResult() {
            return new ApplicationServiceResult(
                ApplicationServiceResultCode.Success, null);
        }

        public static ApplicationServiceResult InvalidContextResult() {
            return Failure(ApplicationServiceResultCode.InvalidContext,
                "Application service context is stale or ineligible");
        }

        public static ApplicationServiceResult InvalidRequestResult() {
            return Failure(ApplicationServiceResultCode.InvalidRequest,
                "Application service request is invalid or exceeds its bound");
        }

        public static ApplicationServiceResult Failure(
                ApplicationServiceResultCode code, string diagnostic) {
            return new ApplicationServiceResult(code, diagnostic);
        }

        internal static string BoundDiagnostic(string diagnostic) {
            if (string.IsNullOrEmpty(diagnostic) ||
                    diagnostic.Length <= MaxDiagnosticLength) return diagnostic;
            return diagnostic.Substring(0, MaxDiagnosticLength);
        }
    }

    /// <summary>
    /// Bounded result carrying a service value.  The value is only meaningful
    /// when Succeeded is true; failure results never expose backend objects.
    /// </summary>
    public sealed class ApplicationServiceResult<T> {
        public ApplicationServiceResultCode Code { get; private set; }
        public T Value { get; private set; }
        public string BoundedDiagnostic { get; private set; }
        public bool Succeeded {
            get { return Code == ApplicationServiceResultCode.Success; }
        }

        private ApplicationServiceResult(ApplicationServiceResultCode code,
                                         T value, string diagnostic) {
            Code = code;
            Value = value;
            BoundedDiagnostic = ApplicationServiceResult.BoundDiagnostic(diagnostic);
        }

        public static ApplicationServiceResult<T> SuccessResult(T value) {
            return new ApplicationServiceResult<T>(
                ApplicationServiceResultCode.Success, value, null);
        }

        public static ApplicationServiceResult<T> Failure(
                ApplicationServiceResultCode code, string diagnostic) {
            return new ApplicationServiceResult<T>(code, default(T), diagnostic);
        }
    }

    /// <summary>
    /// Validated capability-shaped application identity.  This value is not an
    /// authority: every backend operation must revalidate its generation-safe
    /// handle against ApplicationInstanceRegistry.
    /// </summary>
    public sealed class ApplicationServiceContext {
        public const int MaxApplicationIdLength = 96;
        public const int MaxCapabilities = 10;
        public const int MaxCapabilityTextLength = 64;

        private readonly ApplicationInstanceHandle _instanceHandle;
        private readonly string _applicationId;
        private readonly string[] _capabilities;

        public ApplicationInstanceHandle InstanceHandle {
            get { return _instanceHandle; }
        }
        public string ApplicationId { get { return _applicationId; } }
        public int CapabilityCount { get { return _capabilities.Length; } }
        public bool IsValid {
            get { return _instanceHandle.IsValid &&
                         !string.IsNullOrEmpty(_applicationId); }
        }

        internal ApplicationServiceContext(ApplicationInstanceHandle handle,
                                           string applicationId,
                                           string[] capabilities) {
            _instanceHandle = handle;
            _applicationId = applicationId;
            _capabilities = CopyCapabilities(capabilities);
        }

        public string GetCapability(int index) {
            return index >= 0 && index < _capabilities.Length
                ? _capabilities[index] : null;
        }

        internal bool HasCapability(ApplicationServiceId serviceId) {
            string expected = ApplicationServiceNames.For(serviceId);
            for (int i = 0; i < _capabilities.Length; i++) {
                if (_capabilities[i] == expected) return true;
            }
            return false;
        }

        internal static bool IsBoundedText(string value, int maxLength,
                                           bool allowEmpty = true) {
            return value != null && value.Length <= maxLength &&
                   (allowEmpty || value.Length > 0);
        }

        private static string[] CopyCapabilities(string[] values) {
            if (values == null || values.Length == 0) return new string[0];
            int count = values.Length > MaxCapabilities
                ? MaxCapabilities : values.Length;
            string[] copy = new string[count];
            for (int i = 0; i < count; i++) {
                string value = values[i] ?? string.Empty;
                copy[i] = value.Length <= MaxCapabilityTextLength
                    ? value : value.Substring(0, MaxCapabilityTextLength);
            }
            return copy;
        }
    }

    public enum ApplicationNotificationSeverity {
        Info = 0,
        Error = 1
    }

    public sealed class ApplicationNotificationRequest {
        public const int MaxTitleLength = 64;
        public const int MaxBodyLength = 256;
        public const int MaxRenderedMessageLength = MaxTitleLength + 2 + MaxBodyLength;

        public string Title { get; private set; }
        public string Body { get; private set; }
        public ApplicationNotificationSeverity Severity { get; private set; }
        public bool IsValid { get; private set; }
        public string RenderedMessage {
            get {
                if (string.IsNullOrEmpty(Body)) return Title ?? string.Empty;
                if (string.IsNullOrEmpty(Title)) return Body;
                return Title + ": " + Body;
            }
        }

        private ApplicationNotificationRequest(string title, string body,
                                               ApplicationNotificationSeverity severity) {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            Severity = severity;
            IsValid = IsBoundedText(Title, MaxTitleLength, false) &&
                      IsBoundedText(Body, MaxBodyLength, true) &&
                      (severity == ApplicationNotificationSeverity.Info ||
                       severity == ApplicationNotificationSeverity.Error);
        }

        public static ApplicationNotificationRequest Create(
                string title, string body,
                ApplicationNotificationSeverity severity) {
            return new ApplicationNotificationRequest(title, body, severity);
        }

        public static bool TryCreate(
                string title, string body,
                ApplicationNotificationSeverity severity,
                out ApplicationNotificationRequest request) {
            request = new ApplicationNotificationRequest(title, body, severity);
            return request.IsValid;
        }

        private static bool IsBoundedText(string value, int maxLength,
                                          bool allowEmpty) {
            return value != null && value.Length <= maxLength &&
                   (allowEmpty || value.Length > 0);
        }
    }

    public enum ApplicationSettingValueKind {
        Boolean = 1,
        Int32 = 2,
        String = 3
    }

    /// <summary>
    /// Immutable bounded session-setting value.  References are limited to
    /// immutable strings; no application or kernel object can be stored.
    /// </summary>
    public readonly struct ApplicationSettingValue {
        public const int MaxStringLength = 256;

        private readonly ApplicationSettingValueKind _kind;
        private readonly int _booleanValue;
        private readonly int _int32Value;
        private readonly string _stringValue;

        public readonly ApplicationSettingValueKind Kind { get { return _kind; } }
        public readonly bool BooleanValue { get { return _booleanValue != 0; } }
        public readonly int Int32Value { get { return _int32Value; } }
        public readonly string StringValue { get { return _stringValue; } }
        public readonly bool IsValid {
            get {
                return _kind == ApplicationSettingValueKind.Boolean ||
                       _kind == ApplicationSettingValueKind.Int32 ||
                       (_kind == ApplicationSettingValueKind.String &&
                        _stringValue != null &&
                        _stringValue.Length <= MaxStringLength);
            }
        }

        private ApplicationSettingValue(ApplicationSettingValueKind kind,
                                        bool booleanValue, int int32Value,
                                        string stringValue) {
            _kind = kind;
            _booleanValue = booleanValue ? 1 : 0;
            _int32Value = int32Value;
            _stringValue = stringValue;
        }

        public static ApplicationSettingValue Boolean(bool value) {
            return new ApplicationSettingValue(
                ApplicationSettingValueKind.Boolean, value, 0, null);
        }

        public static ApplicationSettingValue Int32(int value) {
            return new ApplicationSettingValue(
                ApplicationSettingValueKind.Int32, false, value, null);
        }

        public static ApplicationSettingValue String(string value) {
            return new ApplicationSettingValue(
                ApplicationSettingValueKind.String, false, 0, value);
        }
    }

    /// <summary>
    /// Immutable scalar system snapshot.  It is safe to copy or serialize at
    /// a future process boundary because it contains no backend references.
    /// </summary>
    public readonly struct SystemInformationSnapshot {
        public const int MaxOsNameLength = 32;
        public const int MaxOsVersionLength = 32;
        public const int MaxArchitectureLength = 16;

        public readonly ulong UptimeTicks { get; }
        public readonly ulong MemorySizeBytes { get; }
        public readonly ulong MemoryInUseBytes { get; }
        public readonly int ThreadCount { get; }
        public readonly int CpuUsagePercent { get; }
        public readonly string OsName { get; }
        public readonly string OsVersion { get; }
        public readonly string Architecture { get; }

        internal SystemInformationSnapshot(ulong uptimeTicks,
                                           ulong memorySizeBytes,
                                           ulong memoryInUseBytes,
                                           int threadCount,
                                           int cpuUsagePercent,
                                           string osName,
                                           string osVersion,
                                           string architecture) {
            UptimeTicks = uptimeTicks;
            MemorySizeBytes = memorySizeBytes;
            MemoryInUseBytes = memoryInUseBytes;
            ThreadCount = threadCount;
            CpuUsagePercent = cpuUsagePercent;
            OsName = osName ?? string.Empty;
            OsVersion = osVersion ?? string.Empty;
            Architecture = architecture ?? string.Empty;
        }

        public static bool IsBoundedText(string value, int maxLength) {
            return value != null && value.Length <= maxLength;
        }

        public bool IsWithinBounds() {
            return MemoryInUseBytes <= MemorySizeBytes &&
                   ThreadCount >= 0 && CpuUsagePercent >= 0 &&
                   CpuUsagePercent <= 100 &&
                   IsBoundedText(OsName, MaxOsNameLength) &&
                   IsBoundedText(OsVersion, MaxOsVersionLength) &&
                   IsBoundedText(Architecture, MaxArchitectureLength);
        }
    }

    public abstract class ApplicationNotificationService {
        public abstract ApplicationServiceResult Publish(
            ApplicationServiceContext context,
            ApplicationNotificationRequest request);
        public abstract ApplicationServiceResult Clear(
            ApplicationServiceContext context);
    }

    public abstract class ApplicationSettingsService {
        public const int MaxApplicationNamespaces = 32;
        public const int MaxKeysPerApplication = 16;
        public const int MaxKeyLength = 64;
        public abstract ApplicationServiceResult<ApplicationSettingValue> Get(
            ApplicationServiceContext context, string key);
        public abstract ApplicationServiceResult Set(
            ApplicationServiceContext context, string key,
            ApplicationSettingValue value);
        public abstract ApplicationServiceResult Remove(
            ApplicationServiceContext context, string key);
    }

    public abstract class ApplicationSystemInformationService {
        public abstract ApplicationServiceResult<SystemInformationSnapshot> GetSnapshot(
            ApplicationServiceContext context);
    }

    public sealed class ApplicationResourceRequest {
        public const int MaxResourceKeyLength = 96;

        private ApplicationResourceRequest(string resourceKey) {
            ResourceKey = resourceKey ?? string.Empty;
            IsValid = ApplicationResourceKeyRules.IsValid(ResourceKey);
        }

        public string ResourceKey { get; private set; }
        public bool IsValid { get; private set; }

        public static ApplicationResourceRequest Create(string resourceKey) {
            return new ApplicationResourceRequest(resourceKey);
        }
    }

    public sealed class ApplicationClipboardWriteRequest {
        public const int MaxTextLength = 64 * 1024;

        private ApplicationClipboardWriteRequest(string text) {
            Text = text ?? string.Empty;
            IsValid = text != null && text.Length <= MaxTextLength;
        }

        public string Text { get; private set; }
        public bool IsValid { get; private set; }

        public static ApplicationClipboardWriteRequest Create(string text) {
            return new ApplicationClipboardWriteRequest(text);
        }
    }

    public sealed class ApplicationClipboardSnapshot {
        private ApplicationClipboardSnapshot(bool hasValue, string text,
                string sourceAppId, ulong generation) {
            HasValue = hasValue;
            Text = CopyText(text);
            SourceAppId = CopyText(sourceAppId);
            Generation = generation;
        }

        public bool HasValue { get; private set; }
        public string Text { get; private set; }
        public string SourceAppId { get; private set; }
        public ulong Generation { get; private set; }

        internal static ApplicationClipboardSnapshot Create(
                bool hasValue, string text, string sourceAppId,
                ulong generation) {
            return new ApplicationClipboardSnapshot(hasValue, text,
                sourceAppId, generation);
        }

        private static string CopyText(string source) {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            char[] chars = new char[source.Length];
            for (int i = 0; i < chars.Length; i++) chars[i] = source[i];
            return new string(chars);
        }
    }

    public sealed class ApplicationResourceReadRequest {
        public const int MaxChunkLength = 64 * 1024;

        private ApplicationResourceReadRequest(string resourceKey,
                long offset, int maximumBytes) {
            ResourceKey = resourceKey ?? string.Empty;
            Offset = offset;
            MaximumBytes = maximumBytes;
            IsValid = ApplicationResourceKeyRules.IsValid(ResourceKey) &&
                      offset >= 0 && maximumBytes > 0 &&
                      maximumBytes <= MaxChunkLength;
        }

        public string ResourceKey { get; private set; }
        public long Offset { get; private set; }
        public int MaximumBytes { get; private set; }
        public bool IsValid { get; private set; }

        public static ApplicationResourceReadRequest Create(
                string resourceKey, long offset, int maximumBytes) {
            return new ApplicationResourceReadRequest(resourceKey, offset,
                maximumBytes);
        }
    }

    public sealed class ApplicationResourceMetadata {
        private ApplicationResourceMetadata(string resourceKey, long length,
                bool readable) {
            ResourceKey = resourceKey ?? string.Empty;
            Length = length;
            IsReadable = readable;
        }

        public string ResourceKey { get; private set; }
        public long Length { get; private set; }
        public bool IsReadable { get; private set; }

        internal static ApplicationResourceMetadata Create(
                string resourceKey, long length, bool readable) {
            return new ApplicationResourceMetadata(resourceKey, length,
                readable);
        }
    }

    public sealed class ApplicationResourceReadResult {
        private ApplicationResourceReadResult(string resourceKey, long offset,
                byte[] bytes, int bytesRead, bool endOfResource) {
            ResourceKey = resourceKey ?? string.Empty;
            Offset = offset;
            Bytes = CopyBytes(bytes);
            BytesRead = bytesRead;
            EndOfResource = endOfResource;
        }

        public string ResourceKey { get; private set; }
        public long Offset { get; private set; }
        public byte[] Bytes { get; private set; }
        public int BytesRead { get; private set; }
        public bool EndOfResource { get; private set; }

        internal static ApplicationResourceReadResult Create(
                string resourceKey, long offset, byte[] bytes,
                int bytesRead, bool endOfResource) {
            return new ApplicationResourceReadResult(resourceKey, offset,
                bytes, bytesRead, endOfResource);
        }

        private static byte[] CopyBytes(byte[] source) {
            if (source == null || source.Length == 0) return new byte[0];
            byte[] copy = new byte[source.Length];
            for (int i = 0; i < source.Length; i++) copy[i] = source[i];
            return copy;
        }
    }

    public enum ApplicationStorageNamespace {
        Persistent = 0,
        Temporary = 1
    }

    public sealed class ApplicationStorageRequest {
        public const int MaxRelativePathLength = 192;
        public const int MaxPathSegmentLength = 64;

        private ApplicationStorageRequest(ApplicationStorageNamespace space,
                string relativePath) {
            Namespace = space;
            RelativePath = relativePath ?? string.Empty;
            IsValid = (space == ApplicationStorageNamespace.Persistent ||
                       space == ApplicationStorageNamespace.Temporary) &&
                      ApplicationStoragePathRules.IsValid(RelativePath);
        }

        public ApplicationStorageNamespace Namespace { get; private set; }
        public string RelativePath { get; private set; }
        public bool IsValid { get; private set; }

        public static ApplicationStorageRequest Create(
                ApplicationStorageNamespace space, string relativePath) {
            return new ApplicationStorageRequest(space, relativePath);
        }
    }

    public sealed class ApplicationStorageReadRequest {
        public const int MaxChunkLength = 64 * 1024;

        private ApplicationStorageReadRequest(
                ApplicationStorageNamespace space, string relativePath,
                long offset, int maximumBytes) {
            Namespace = space;
            RelativePath = relativePath ?? string.Empty;
            Offset = offset;
            MaximumBytes = maximumBytes;
            IsValid = ApplicationStorageRequest.Create(space, RelativePath).IsValid &&
                      offset >= 0 && maximumBytes > 0 &&
                      maximumBytes <= MaxChunkLength;
        }

        public ApplicationStorageNamespace Namespace { get; private set; }
        public string RelativePath { get; private set; }
        public long Offset { get; private set; }
        public int MaximumBytes { get; private set; }
        public bool IsValid { get; private set; }

        public static ApplicationStorageReadRequest Create(
                ApplicationStorageNamespace space, string relativePath,
                long offset, int maximumBytes) {
            return new ApplicationStorageReadRequest(space, relativePath,
                offset, maximumBytes);
        }
    }

    public sealed class ApplicationStorageWriteRequest {
        public const int MaxPayloadLength = 64 * 1024;

        private ApplicationStorageWriteRequest(
                ApplicationStorageNamespace space, string relativePath,
                byte[] payload) {
            Namespace = space;
            RelativePath = relativePath ?? string.Empty;
            Payload = CopyBytes(payload);
            IsValid = ApplicationStorageRequest.Create(space, RelativePath).IsValid &&
                      payload != null && payload.Length <= MaxPayloadLength;
        }

        public ApplicationStorageNamespace Namespace { get; private set; }
        public string RelativePath { get; private set; }
        public byte[] Payload { get; private set; }
        public bool IsValid { get; private set; }

        public static ApplicationStorageWriteRequest Create(
                ApplicationStorageNamespace space, string relativePath,
                byte[] payload) {
            return new ApplicationStorageWriteRequest(space, relativePath,
                payload);
        }

        private static byte[] CopyBytes(byte[] source) {
            if (source == null || source.Length == 0) return new byte[0];
            byte[] copy = new byte[source.Length];
            for (int i = 0; i < source.Length; i++) copy[i] = source[i];
            return copy;
        }
    }

    public sealed class ApplicationStorageEntry {
        private ApplicationStorageEntry(string relativePath, long length) {
            RelativePath = relativePath ?? string.Empty;
            Length = length;
        }

        public string RelativePath { get; private set; }
        public long Length { get; private set; }

        internal static ApplicationStorageEntry Create(string relativePath,
                long length) {
            return new ApplicationStorageEntry(relativePath, length);
        }
    }

    public sealed class ApplicationStorageReadResult {
        private ApplicationStorageReadResult(string relativePath, long offset,
                byte[] bytes, int bytesRead, bool endOfResource) {
            RelativePath = relativePath ?? string.Empty;
            Offset = offset;
            Bytes = CopyBytes(bytes);
            BytesRead = bytesRead;
            EndOfResource = endOfResource;
        }

        public string RelativePath { get; private set; }
        public long Offset { get; private set; }
        public byte[] Bytes { get; private set; }
        public int BytesRead { get; private set; }
        public bool EndOfResource { get; private set; }

        internal static ApplicationStorageReadResult Create(
                string relativePath, long offset, byte[] bytes,
                int bytesRead, bool endOfResource) {
            return new ApplicationStorageReadResult(relativePath, offset,
                bytes, bytesRead, endOfResource);
        }

        private static byte[] CopyBytes(byte[] source) {
            if (source == null || source.Length == 0) return new byte[0];
            byte[] copy = new byte[source.Length];
            for (int i = 0; i < source.Length; i++) copy[i] = source[i];
            return copy;
        }
    }

    public abstract class ApplicationResourceService {
        public abstract ApplicationServiceResult<ApplicationResourceMetadata>
            GetMetadata(ApplicationServiceContext context,
                ApplicationResourceRequest request);
        public abstract ApplicationServiceResult<ApplicationResourceReadResult>
            Read(ApplicationServiceContext context,
                ApplicationResourceReadRequest request);
    }

    public abstract class ApplicationStorageService {
        public abstract ApplicationServiceResult<bool> Exists(
            ApplicationServiceContext context, ApplicationStorageRequest request);
        public abstract ApplicationServiceResult<ApplicationStorageReadResult>
            Read(ApplicationServiceContext context,
                ApplicationStorageReadRequest request);
        public abstract ApplicationServiceResult Write(
            ApplicationServiceContext context,
            ApplicationStorageWriteRequest request);
        public abstract ApplicationServiceResult Delete(
            ApplicationServiceContext context, ApplicationStorageRequest request);
        public abstract ApplicationServiceResult<ApplicationStorageEntry[]> Enumerate(
            ApplicationServiceContext context,
            ApplicationStorageNamespace space);
        internal abstract void ResetTemporaryForAppModel();
    }

    public abstract class ApplicationClipboardService {
        public abstract ApplicationServiceResult SetText(
                ApplicationServiceContext context,
                ApplicationClipboardWriteRequest request);
        public abstract ApplicationServiceResult<ApplicationClipboardSnapshot>
            GetText(ApplicationServiceContext context);
        public abstract ApplicationServiceResult Clear(
                ApplicationServiceContext context);
        internal abstract void ResetForAppModel();
    }

    /// <summary>
    /// Typed service projection.  The registry owns the adapters and is the
    /// only code allowed to construct this value.
    /// </summary>
    public sealed class ApplicationServiceAccess {
        public ApplicationNotificationService Notifications { get; private set; }
        public ApplicationSettingsService Settings { get; private set; }
        public ApplicationSystemInformationService SystemInformation { get; private set; }
        public ApplicationDialogService Dialogs { get; private set; }
        public ApplicationOpenFileService OpenFile { get; private set; }
        public ApplicationSaveFileService SaveFile { get; private set; }
        public ApplicationShellService Shell { get; private set; }
        public ApplicationResourceService Resources { get; private set; }
        public ApplicationStorageService Storage { get; private set; }
        public ApplicationClipboardService Clipboard { get; private set; }

        internal ApplicationServiceAccess(
                ApplicationNotificationService notifications,
                ApplicationSettingsService settings,
                ApplicationSystemInformationService systemInformation,
                ApplicationDialogService dialogs,
                ApplicationOpenFileService openFile,
                ApplicationSaveFileService saveFile,
                ApplicationShellService shell,
                ApplicationResourceService resources,
                ApplicationStorageService storage,
                ApplicationClipboardService clipboard) {
            Notifications = notifications;
            Settings = settings;
            SystemInformation = systemInformation;
            Dialogs = dialogs;
            OpenFile = openFile;
            SaveFile = saveFile;
            Shell = shell;
            Resources = resources;
            Storage = storage;
            Clipboard = clipboard;
        }
    }
}
