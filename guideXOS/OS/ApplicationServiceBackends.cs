using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;

namespace guideXOS.OS {
    /// <summary>
    /// Same-address-space notification adapter.  It exposes only the bounded
    /// request/result contract; Notify, Animation, and renderer state stay in
    /// NotificationManager.
    /// </summary>
    internal sealed class CSharpApplicationNotificationService :
            ApplicationNotificationService {
        public override ApplicationServiceResult Publish(
                ApplicationServiceContext context,
                ApplicationNotificationRequest request) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.Notifications,
                    out instance, out valid)) return valid;
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult.InvalidRequestResult();
            }
            NotificationLevel level =
                request.Severity == ApplicationNotificationSeverity.Error
                    ? NotificationLevel.Error : NotificationLevel.None;
            NotificationManager.AddForApplication(
                context.ApplicationId, request.RenderedMessage, level);
            return ApplicationServiceResult.SuccessResult();
        }

        public override ApplicationServiceResult Clear(
                ApplicationServiceContext context) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.Notifications,
                    out instance, out valid)) return valid;
            NotificationManager.ClearForApplication(context.ApplicationId);
            return ApplicationServiceResult.SuccessResult();
        }
    }

    /// <summary>
    /// Bounded in-memory settings store.  Namespaces are keyed by the stable
    /// descriptor/application identity, not by an ApplicationInstance handle,
    /// so separate instances of one application share session settings.
    /// </summary>
    internal sealed class CSharpApplicationSettingsService :
            ApplicationSettingsService {
        private sealed class SettingEntry {
            internal string Key;
            internal ApplicationSettingValue Value;
        }

        private sealed class ApplicationSettingsNamespace {
            internal string ApplicationId;
            internal readonly SettingEntry[] Entries =
                new SettingEntry[MaxKeysPerApplication];
            internal int Count;
        }

        private readonly ApplicationSettingsNamespace[] _namespaces =
            new ApplicationSettingsNamespace[MaxApplicationNamespaces];
        private int _namespaceCount;

        public override ApplicationServiceResult<ApplicationSettingValue> Get(
                ApplicationServiceContext context, string key) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.Settings,
                    out instance, out valid)) {
                return ApplicationServiceResult<ApplicationSettingValue>.Failure(
                    valid.Code, valid.BoundedDiagnostic);
            }
            if (!IsValidKey(key)) {
                return ApplicationServiceResult<ApplicationSettingValue>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Application setting key is invalid or exceeds its bound");
            }
            ApplicationSettingsNamespace settings = FindNamespace(
                context.ApplicationId);
            if (settings == null) {
                return ApplicationServiceResult<ApplicationSettingValue>.Failure(
                    ApplicationServiceResultCode.NotFound,
                    "Application setting was not found");
            }
            SettingEntry entry = FindEntry(settings, key);
            if (entry == null) {
                return ApplicationServiceResult<ApplicationSettingValue>.Failure(
                    ApplicationServiceResultCode.NotFound,
                    "Application setting was not found");
            }
            return ApplicationServiceResult<ApplicationSettingValue>.SuccessResult(
                entry.Value);
        }

        public override ApplicationServiceResult Set(
                ApplicationServiceContext context, string key,
                ApplicationSettingValue value) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.Settings,
                    out instance, out valid)) return valid;
            if (!IsValidKey(key) || !value.IsValid) {
                return ApplicationServiceResult.InvalidRequestResult();
            }
            ApplicationSettingsNamespace settings = FindNamespace(
                context.ApplicationId);
            if (settings == null) {
                if (_namespaceCount >= MaxApplicationNamespaces) {
                    return ApplicationServiceResult.Failure(
                        ApplicationServiceResultCode.ResourceUnavailable,
                        "Application settings namespace capacity is exhausted");
                }
                settings = new ApplicationSettingsNamespace {
                    ApplicationId = context.ApplicationId
                };
                _namespaces[_namespaceCount++] = settings;
            }
            SettingEntry entry = FindEntry(settings, key);
            if (entry != null) {
                entry.Value = value;
                return ApplicationServiceResult.SuccessResult();
            }
            if (settings.Count >= MaxKeysPerApplication) {
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.ResourceUnavailable,
                    "Application settings key capacity is exhausted");
            }
            settings.Entries[settings.Count++] = new SettingEntry {
                Key = key,
                Value = value
            };
            return ApplicationServiceResult.SuccessResult();
        }

        public override ApplicationServiceResult Remove(
                ApplicationServiceContext context, string key) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.Settings,
                    out instance, out valid)) return valid;
            if (!IsValidKey(key)) {
                return ApplicationServiceResult.InvalidRequestResult();
            }
            ApplicationSettingsNamespace settings = FindNamespace(
                context.ApplicationId);
            SettingEntry entry = settings == null ? null : FindEntry(settings, key);
            if (entry == null) {
                return ApplicationServiceResult.Failure(
                    ApplicationServiceResultCode.NotFound,
                    "Application setting was not found");
            }
            int index = 0;
            while (index < settings.Count && settings.Entries[index] != entry) {
                index++;
            }
            for (int i = index; i < settings.Count - 1; i++) {
                settings.Entries[i] = settings.Entries[i + 1];
            }
            settings.Entries[--settings.Count] = null;
            return ApplicationServiceResult.SuccessResult();
        }

        private ApplicationSettingsNamespace FindNamespace(string applicationId) {
            for (int i = 0; i < _namespaceCount; i++) {
                ApplicationSettingsNamespace candidate = _namespaces[i];
                if (candidate != null && TextEquals(candidate.ApplicationId,
                        applicationId)) return candidate;
            }
            return null;
        }

        private static SettingEntry FindEntry(
                ApplicationSettingsNamespace settings, string key) {
            for (int i = 0; i < settings.Count; i++) {
                SettingEntry entry = settings.Entries[i];
                if (entry != null && TextEquals(entry.Key, key)) return entry;
            }
            return null;
        }

        private static bool IsValidKey(string key) {
            return key != null && key.Length > 0 &&
                   key.Length <= MaxKeyLength;
        }

        private static bool TextEquals(string a, string b) {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Request-time copied scalar snapshot.  No allocator, timer, or
    /// ThreadPool object crosses the application service boundary.
    /// </summary>
    internal sealed class CSharpApplicationSystemInformationService :
            ApplicationSystemInformationService {
        public override ApplicationServiceResult<SystemInformationSnapshot>
                GetSnapshot(ApplicationServiceContext context) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.SystemInformation,
                    out instance, out valid)) {
                return ApplicationServiceResult<SystemInformationSnapshot>.Failure(
                    valid.Code, valid.BoundedDiagnostic);
            }

            ulong memorySize = Allocator.MemorySize;
            if (memorySize == 0) {
                return ApplicationServiceResult<SystemInformationSnapshot>.Failure(
                    ApplicationServiceResultCode.ResourceUnavailable,
                    "System memory total is unavailable");
            }
            ulong memoryInUse = Allocator.MemoryInUse;
            if (memoryInUse > memorySize) memoryInUse = memorySize;
            uint rawCpu = ThreadPool.CPUUsage;
            int cpu = rawCpu > 100 ? 100 : (int)rawCpu;
            int threadCount = ThreadPool.ThreadCount;
            if (threadCount < 0) threadCount = 0;

            SystemInformationSnapshot snapshot = new SystemInformationSnapshot(
                Timer.Ticks, memorySize, memoryInUse, threadCount, cpu,
                "guideXOS", "Phase8", "x86_64");
            return ApplicationServiceResult<SystemInformationSnapshot>.SuccessResult(
                snapshot);
        }
    }
}
