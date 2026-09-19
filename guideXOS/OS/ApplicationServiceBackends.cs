using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;

namespace guideXOS.OS {
    internal sealed class CSharpApplicationDialogService :
            ApplicationDialogService {
        public override ApplicationServiceResult<ApplicationServiceRequestHandle>
                Begin(ApplicationServiceContext context,
                      ApplicationDialogRequest request) {
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Dialog request is invalid or exceeds its bounds");
            }
            ApplicationServiceRequestHandle handle;
            ApplicationServiceResult begun =
                ApplicationServiceRegistry.BeginInteractiveRequest(context,
                    ApplicationServiceId.Dialogs, request, out handle);
            if (!begun.Succeeded) {
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    begun.Code, begun.BoundedDiagnostic);
            }
            if (!CreateWindow(context, request, handle)) {
                ApplicationServiceRegistry.CompleteDialogRequest(handle,
                    ApplicationDialogOutcome.BackendFailure);
                ApplicationServiceSessionTable.ConsumeTerminal(handle);
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    ApplicationServiceResultCode.BackendFailure,
                    "Dialog backend could not create a window");
            }
            return ApplicationServiceResult<ApplicationServiceRequestHandle>.SuccessResult(
                handle);
        }

        public override ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationDialogResult>> Observe(
                    ApplicationServiceContext context,
                    ApplicationServiceRequestHandle handle) {
            return ApplicationServiceRegistry.ObserveDialogRequest(context, handle);
        }

        public override ApplicationServiceResult Cancel(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle) {
            return ApplicationServiceRegistry.CancelDialogRequest(context, handle);
        }

        private static bool CreateWindow(ApplicationServiceContext context,
                ApplicationDialogRequest request,
                ApplicationServiceRequestHandle handle) {
            try {
                MessageBox message;
                if (request.Kind == ApplicationDialogKind.Confirmation) {
                    SaveChangesDialog confirmation = new SaveChangesDialog(null,
                        () => ApplicationServiceRegistry.CompleteDialogRequest(
                            handle, ApplicationDialogOutcome.Accepted),
                        () => ApplicationServiceRegistry.CompleteDialogRequest(
                            handle, ApplicationDialogOutcome.Rejected),
                        () => ApplicationServiceRegistry.CompleteDialogRequest(
                            handle, ApplicationDialogOutcome.Cancelled));
                    confirmation.SetServiceCloseCallback(() =>
                        ApplicationServiceRegistry.CompleteDialogRequest(handle,
                            ApplicationDialogOutcome.Cancelled));
                    return ShowTransientWindow(confirmation,
                        context.InstanceHandle, handle);
                }
                message = new MessageBox(100, 100);
                message.ConfigureService(request.Title, request.Body, () =>
                    ApplicationServiceRegistry.CompleteDialogRequest(handle,
                        ApplicationDialogOutcome.Accepted));
                return ShowTransientWindow(message, context.InstanceHandle,
                    handle);
            } catch {
                return false;
            }
        }

        internal static bool ShowTransientWindow(Window window,
                ApplicationInstanceHandle owner,
                ApplicationServiceRequestHandle handle) {
            if (!WindowManager.RegisterTransientServiceWindow(window, owner,
                    handle)) {
                if (window != null) window.CloseForApplicationTermination();
                return false;
            }
            window.Visible = true;
            WindowManager.MoveToEnd(window);
            return true;
        }
    }

    internal sealed class CSharpApplicationOpenFileService :
            ApplicationOpenFileService {
        public override ApplicationServiceResult<ApplicationServiceRequestHandle>
                Begin(ApplicationServiceContext context, OpenFileRequest request) {
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Open-file request is invalid or exceeds its bound");
            }
            ApplicationServiceRequestHandle handle;
            ApplicationServiceResult begun =
                ApplicationServiceRegistry.BeginInteractiveRequest(context,
                    ApplicationServiceId.OpenFile, request, out handle);
            if (!begun.Succeeded) {
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    begun.Code, begun.BoundedDiagnostic);
            }
            try {
                OpenDialog dialog = new OpenDialog(100, 100, 520, 360,
                    request.StartingLocation, path =>
                        ApplicationServiceRegistry.CompleteFileDialogRequest(
                            handle, ApplicationFileDialogOutcome.Selected, path));
                dialog.SetServiceCloseCallback(() =>
                    ApplicationServiceRegistry.CompleteFileDialogRequest(handle,
                        ApplicationFileDialogOutcome.Cancelled, string.Empty));
                if (!CSharpApplicationDialogService.ShowTransientWindow(dialog,
                        context.InstanceHandle, handle)) {
                    ApplicationServiceRegistry.CompleteFileDialogRequest(handle,
                        ApplicationFileDialogOutcome.BackendFailure, string.Empty);
                    ApplicationServiceSessionTable.ConsumeTerminal(handle);
                    return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                        ApplicationServiceResultCode.BackendFailure,
                        "Open-file backend could not register a dialog");
                }
            } catch {
                ApplicationServiceRegistry.CompleteFileDialogRequest(handle,
                    ApplicationFileDialogOutcome.BackendFailure, string.Empty);
                ApplicationServiceSessionTable.ConsumeTerminal(handle);
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    ApplicationServiceResultCode.BackendFailure,
                    "Open-file backend could not create a dialog");
            }
            return ApplicationServiceResult<ApplicationServiceRequestHandle>.SuccessResult(
                handle);
        }

        public override ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationFileDialogResult>>
                Observe(ApplicationServiceContext context,
                    ApplicationServiceRequestHandle handle) {
            return ApplicationServiceRegistry.ObserveFileDialogRequest(context,
                handle, ApplicationServiceId.OpenFile);
        }

        public override ApplicationServiceResult Cancel(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle) {
            return ApplicationServiceRegistry.CancelFileDialogRequest(context,
                handle, ApplicationServiceId.OpenFile);
        }
    }

    internal sealed class CSharpApplicationSaveFileService :
            ApplicationSaveFileService {
        public override ApplicationServiceResult<ApplicationServiceRequestHandle>
                Begin(ApplicationServiceContext context, SaveFileRequest request) {
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Save-file request is invalid or exceeds its bound");
            }
            ApplicationServiceRequestHandle handle;
            ApplicationServiceResult begun =
                ApplicationServiceRegistry.BeginInteractiveRequest(context,
                    ApplicationServiceId.SaveFile, request, out handle);
            if (!begun.Succeeded) {
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    begun.Code, begun.BoundedDiagnostic);
            }
            try {
                string suggested = request.SuggestedFileName;
                if (!string.IsNullOrEmpty(suggested) &&
                        suggested.IndexOf('.') < 0) {
                    suggested = suggested + ".txt";
                }
                SaveDialog dialog = new SaveDialog(100, 100, 520, 360,
                    request.StartingLocation, suggested, path =>
                        ApplicationServiceRegistry.CompleteFileDialogRequest(
                            handle, ApplicationFileDialogOutcome.Selected, path));
                dialog.SetServiceCloseCallback(() =>
                    ApplicationServiceRegistry.CompleteFileDialogRequest(handle,
                        ApplicationFileDialogOutcome.Cancelled, string.Empty));
                if (!CSharpApplicationDialogService.ShowTransientWindow(dialog,
                        context.InstanceHandle, handle)) {
                    ApplicationServiceRegistry.CompleteFileDialogRequest(handle,
                        ApplicationFileDialogOutcome.BackendFailure, string.Empty);
                    ApplicationServiceSessionTable.ConsumeTerminal(handle);
                    return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                        ApplicationServiceResultCode.BackendFailure,
                        "Save-file backend could not register a dialog");
                }
            } catch {
                ApplicationServiceRegistry.CompleteFileDialogRequest(handle,
                    ApplicationFileDialogOutcome.BackendFailure, string.Empty);
                ApplicationServiceSessionTable.ConsumeTerminal(handle);
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    ApplicationServiceResultCode.BackendFailure,
                    "Save-file backend could not create a dialog");
            }
            return ApplicationServiceResult<ApplicationServiceRequestHandle>.SuccessResult(
                handle);
        }

        public override ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationFileDialogResult>>
                Observe(ApplicationServiceContext context,
                    ApplicationServiceRequestHandle handle) {
            return ApplicationServiceRegistry.ObserveFileDialogRequest(context,
                handle, ApplicationServiceId.SaveFile);
        }

        public override ApplicationServiceResult Cancel(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle) {
            return ApplicationServiceRegistry.CancelFileDialogRequest(context,
                handle, ApplicationServiceId.SaveFile);
        }
    }

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
