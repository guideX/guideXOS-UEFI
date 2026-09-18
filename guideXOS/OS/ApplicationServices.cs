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
        SystemInformation = 3
    }

    public static class ApplicationServiceNames {
        public static string For(ApplicationServiceId id) {
            switch (id) {
                case ApplicationServiceId.Notifications: return "notifications";
                case ApplicationServiceId.Settings: return "settings";
                case ApplicationServiceId.SystemInformation: return "system-information";
                default: return "unknown";
            }
        }

        public static bool IsKnown(ApplicationServiceId id) {
            return id == ApplicationServiceId.Notifications ||
                   id == ApplicationServiceId.Settings ||
                   id == ApplicationServiceId.SystemInformation;
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
        Conflict
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
        public const int MaxCapabilities = 8;
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
        private readonly bool _booleanValue;
        private readonly int _int32Value;
        private readonly string _stringValue;

        public readonly ApplicationSettingValueKind Kind { get { return _kind; } }
        public readonly bool BooleanValue { get { return _booleanValue; } }
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
            _booleanValue = booleanValue;
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

    public interface IApplicationNotificationService {
        ApplicationServiceResult Publish(
            ApplicationServiceContext context,
            ApplicationNotificationRequest request);
        ApplicationServiceResult Clear(ApplicationServiceContext context);
    }

    public interface IApplicationSettingsService {
        ApplicationServiceResult<ApplicationSettingValue> Get(
            ApplicationServiceContext context, string key);
        ApplicationServiceResult Set(
            ApplicationServiceContext context, string key,
            ApplicationSettingValue value);
        ApplicationServiceResult Remove(
            ApplicationServiceContext context, string key);
    }

    public interface IApplicationSystemInformationService {
        ApplicationServiceResult<SystemInformationSnapshot> GetSnapshot(
            ApplicationServiceContext context);
    }

    /// <summary>
    /// Typed service projection.  The registry owns the adapters and is the
    /// only code allowed to construct this value.
    /// </summary>
    public sealed class ApplicationServiceAccess {
        public IApplicationNotificationService Notifications { get; private set; }
        public IApplicationSettingsService Settings { get; private set; }
        public IApplicationSystemInformationService SystemInformation { get; private set; }

        internal ApplicationServiceAccess(
                IApplicationNotificationService notifications,
                IApplicationSettingsService settings,
                IApplicationSystemInformationService systemInformation) {
            Notifications = notifications;
            Settings = settings;
            SystemInformation = systemInformation;
        }
    }
}
