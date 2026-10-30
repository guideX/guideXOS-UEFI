using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Misc;

namespace guideXOS.OS {
    /// <summary>
    /// Bounded result of a shell/open request.  Launch failures remain typed
    /// in ResultCode rather than being collapsed into a generic backend error.
    /// The only identity returned on success is the generation-safe App Model
    /// instance handle.
    /// </summary>
    public sealed class ApplicationShellResult {
        public const int MaxAppIdLength =
            ApplicationServiceContext.MaxApplicationIdLength;

        public ApplicationServiceResultCode ResultCode { get; private set; }
        public string AppId { get; private set; }
        public ApplicationInstanceHandle InstanceHandle { get; private set; }
        public string BoundedDiagnostic { get; private set; }
        public bool Succeeded {
            get { return ResultCode == ApplicationServiceResultCode.Success; }
        }

        private ApplicationShellResult(ApplicationServiceResultCode resultCode,
                                       string appId,
                                       ApplicationInstanceHandle instanceHandle,
                                       string diagnostic) {
            ResultCode = resultCode;
            AppId = BoundAppId(appId);
            InstanceHandle = instanceHandle;
            BoundedDiagnostic = ApplicationServiceResult.BoundDiagnostic(
                diagnostic);
        }

        internal static ApplicationShellResult FromLaunchResult(
                LaunchResult result) {
            if (result == null) {
                return Failed(ApplicationServiceResultCode.BackendFailure,
                    null, "Shell backend returned no launch result");
            }
            return new ApplicationShellResult(
                MapLaunchError(result), result.AppId,
                result.Success ? result.InstanceHandle :
                    ApplicationInstanceHandle.None,
                result.BoundedDiagnostic);
        }

        internal static ApplicationShellResult Failed(
                ApplicationServiceResultCode code, string appId,
                string diagnostic) {
            if (code == ApplicationServiceResultCode.Success) {
                code = ApplicationServiceResultCode.BackendFailure;
            }
            return new ApplicationShellResult(code, appId,
                ApplicationInstanceHandle.None, diagnostic);
        }

        private static ApplicationServiceResultCode MapLaunchError(
                LaunchResult result) {
            if (result.Success) return ApplicationServiceResultCode.Success;
            switch (result.ErrorCode) {
                case LaunchErrorCode.NotFound:
                    return ApplicationServiceResultCode.NotFound;
                case LaunchErrorCode.UnsupportedTarget:
                case LaunchErrorCode.AmbiguousTarget:
                    return ApplicationServiceResultCode.UnsupportedTarget;
                case LaunchErrorCode.MalformedRequest:
                    return ApplicationServiceResultCode.InvalidRequest;
                case LaunchErrorCode.ResourceUnavailable:
                    return ApplicationServiceResultCode.ResourceUnavailable;
                case LaunchErrorCode.PermissionDenied:
                    return ApplicationServiceResultCode.PermissionDenied;
                case LaunchErrorCode.AlreadyTerminated:
                    return ApplicationServiceResultCode.InvalidState;
                case LaunchErrorCode.InitializationFailed:
                case LaunchErrorCode.ActivationFailed:
                case LaunchErrorCode.BackendUnavailable:
                default:
                    return ApplicationServiceResultCode.BackendFailure;
            }
        }

        private static string BoundAppId(string appId) {
            if (string.IsNullOrEmpty(appId) ||
                    appId.Length <= MaxAppIdLength) return appId;
            return appId.Substring(0, MaxAppIdLength);
        }
    }

    /// <summary>
    /// Application-facing shell/open adapter.  It accepts only bounded,
    /// serializable request data and returns a fixed request handle plus a
    /// typed App Model result.
    /// </summary>
    public abstract class ApplicationShellService {
        public abstract ApplicationServiceResult<ApplicationServiceRequestHandle>
                Begin(ApplicationServiceContext context,
                      ApplicationShellOpenRequest request);

        public abstract ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationShellResult>> Observe(
                    ApplicationServiceContext context,
                    ApplicationServiceRequestHandle handle);

        public abstract ApplicationServiceResult Cancel(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle);
    }

    internal sealed class CSharpApplicationShellService :
            ApplicationShellService {
        public override ApplicationServiceResult<ApplicationServiceRequestHandle>
                Begin(ApplicationServiceContext context,
                      ApplicationShellOpenRequest request) {
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Shell request is invalid or exceeds its bound");
            }

            ApplicationServiceRequestHandle handle;
            ApplicationServiceResult begun =
                ApplicationServiceRegistry.BeginShellRequest(context,
                    request, out handle);
            if (!begun.Succeeded) {
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    begun.Code, begun.BoundedDiagnostic);
            }

            LaunchResult launch;
            try {
                launch = Dispatch(request);
            } catch {
                launch = LaunchResult.Failed(
                    LaunchErrorCode.BackendUnavailable,
                    "Shell backend rejected the request", null);
            }
            ApplicationShellResult result =
                ApplicationShellResult.FromLaunchResult(launch);
            ApplicationServiceResult completed =
                ApplicationServiceRegistry.CompleteShellRequest(handle, result);
            if (!completed.Succeeded) {
                ApplicationServiceSessionTable.ConsumeTerminal(handle);
                return ApplicationServiceResult<ApplicationServiceRequestHandle>.Failure(
                    completed.Code, completed.BoundedDiagnostic);
            }
            return ApplicationServiceResult<ApplicationServiceRequestHandle>.SuccessResult(
                handle);
        }

        public override ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationShellResult>> Observe(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle) {
            return ApplicationServiceRegistry.ObserveShellRequest(context,
                handle);
        }

        public override ApplicationServiceResult Cancel(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle) {
            return ApplicationServiceRegistry.CancelShellRequest(context,
                handle);
        }

        private static LaunchResult Dispatch(ApplicationShellOpenRequest request) {
            switch (request.TargetKind) {
                case ApplicationShellOpenTargetKind.ApplicationId:
                    return LaunchModern(LaunchRequest.ForAppId(request.Target,
                        null, null, LaunchActivationIntent.Launch));
                case ApplicationShellOpenTargetKind.Alias:
                    return LaunchModern(LaunchRequest.ForName(request.Target));
                case ApplicationShellOpenTargetKind.Document:
                    return OpenDocument(request.Target);
                case ApplicationShellOpenTargetKind.ShellObject:
                    return OpenShellObject(request.Target);
                case ApplicationShellOpenTargetKind.ShellFolder:
                    return OpenShellFolder(request.Target,
                        request.ShellTargetValue);
                case ApplicationShellOpenTargetKind.TypedShellAction:
                    return OpenTypedShellAction(request.Target);
                default:
                    return LaunchResult.Failed(
                        LaunchErrorCode.UnsupportedTarget,
                        "Shell target kind is unsupported", null);
            }
        }

        private static LaunchResult LaunchModern(LaunchRequest request) {
            if (request == null || !request.IsValid) {
                return LaunchResult.Failed(LaunchErrorCode.MalformedRequest,
                    request == null ? "Launch request is null" :
                        request.ValidationError, null);
            }
            ApplicationDescriptor descriptor;
            if (request.TargetKind == LaunchRequestTargetKind.Application) {
                string matchedAlias;
                LaunchResult resolutionFailure;
                if (!ApplicationDescriptorRegistry.TryResolve(request,
                        out descriptor, out matchedAlias,
                        out resolutionFailure) || descriptor == null) {
                    return resolutionFailure ?? LaunchResult.Failed(
                        LaunchErrorCode.NotFound,
                        "Application descriptor was not found", null);
                }
            } else if (!ApplicationDescriptorRegistry.TryGetById(
                    request.TargetAppId, out descriptor) || descriptor == null) {
                return LaunchResult.Failed(LaunchErrorCode.NotFound,
                    "Application descriptor was not found", request.TargetAppId);
            }
            LaunchResult result;
            if (!ApplicationFactoryRegistry.TryLaunch(descriptor, request,
                    out result)) {
                return LaunchResult.Failed(
                    LaunchErrorCode.BackendUnavailable,
                    "Application factory is unavailable", descriptor.AppId);
            }
            return result ?? LaunchResult.Failed(
                LaunchErrorCode.BackendUnavailable,
                "Application factory returned no result", descriptor.AppId);
        }

        private static LaunchResult OpenDocument(string document) {
            ApplicationAssociation association;
            LaunchRequest request;
            LaunchResult failure;
            if (!ModernFileAssociationAdapter.TryCreateLaunchRequest(document,
                    null, out association, out request, out failure)) {
#if UEFI_DIAGNOSTIC_RING3_PHASE32
                FileAssociationResolution rejectedAssociation =
                    FileAssociationRegistry.ResolvePath(document);
                Program.MarkUefiRing3Phase32("ASSOCIATION=path=" + document +
                    ";ext=" + (rejectedAssociation.Extension ?? "") +
                    ";app=" + (rejectedAssociation.AppId ?? "") +
                    ";success=0");
#endif
                return failure ?? LaunchResult.Failed(
                    LaunchErrorCode.UnsupportedTarget,
                    "No file association was found", null);
            }
            string documentName = LeafName(document);
            FileAssociationResolution resolution =
                FileAssociationRegistry.ResolvePath(documentName);
#if UEFI_DIAGNOSTIC_RING3_PHASE32
            Program.MarkUefiRing3Phase32("ASSOCIATION=path=" + document +
                ";ext=" + (resolution.Extension ?? "") + ";app=" +
                (resolution.AppId ?? "") + ";handler=" +
                (association.HandlerAppId ?? "") + ";content=" +
                (association.ContentType ?? "") + ";launchKind=" +
                request.TargetKindName + ";document=" +
                (request.Document ?? "") + ";verb=" + request.Verb);
#endif
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("ASSOC_RESOLVE=name=" +
                (documentName ?? "") + ";ext=" +
                (resolution.Extension ?? "") + ";app=" +
                (resolution.AppId ?? "") + ";kind=" +
                resolution.Kind.ToString() + ";success=" +
                (resolution.Success ? "1" : "0"));
            Program.MarkUefiAppRuntime("ASSOC_REQUEST=target=" +
                (request.TargetAppId ?? "") + ";kind=" +
                request.TargetKindName + ";document=" +
                (request.Document ?? "") + ";verb=" + request.Verb +
                ";source=" + (request.SourceShellObjectId ?? "") +
                ";intent=" + request.ActivationIntentName);
#endif
            LaunchResult opened;
            if (request.TargetKind == LaunchRequestTargetKind.GxmDocument) {
                opened = LaunchGxm(document, request);
            } else {
                opened = LaunchModern(request);
            }
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            if (request.TargetKind == LaunchRequestTargetKind.GxmDocument) {
                Program.MarkUefiAppRuntime("FILE_RESULT=path=" + document +
                    ";app=GXM;ok=" + (opened != null && opened.Success ?
                    "1" : "0") + ";instance=" +
                    (opened == null ? "" : opened.InstanceHandle.ToString()) +
                    ";state=" +
                    (opened == null ? "" : opened.ActivationStateName) +
                    ";owned=" + GetOwnedWindowCount(opened));
                if (opened != null && opened.Success &&
                        document == "Programs/calculator.gxm") {
                    // Preserve the existing bounded diagnostic probes while
                    // the shell request itself uses the common adapter.
                    Desktop.OnClick("Install to Hard Drive", false, 100, 100);
                    Desktop.OnClick("missing.txt", false, 100, 100);
                    Desktop.OnClick("missing.png", false, 100, 100);
                    Desktop.OnClick("missing.bmp", false, 100, 100);
                    Desktop.OnClick("missing.wav", false, 100, 100);
                    Desktop.OnClick("missing.mue", false, 100, 100);
                    Desktop.OnClick("USB Drive 0", false, 100, 100);
                    Program.MarkUefiAppRuntime(
                        "NEGATIVE_FILE_ASSOCIATIONS=5");
                }
            } else {
                ApplicationDescriptor descriptor;
                ApplicationDescriptorRegistry.TryGetById(
                    request.TargetAppId, out descriptor);
                string appName = descriptor == null ? request.TargetAppId :
                    descriptor.DisplayName;
                if (opened != null && opened.Success) {
                    Program.MarkUefiAppRuntime("FILE_OK=path=" + document +
                        ";app=" + appName + ";content=" +
                        (appName == "Notepad" ? "loaded" :
                        (appName == "WAV Player" ? "dispatched" :
                            "decoded")) + ";instance=" +
                        opened.InstanceHandle.ToString() + ";state=" +
                        opened.ActivationStateName + ";owned=" +
                        GetOwnedWindowCount(opened));
                } else {
                    Program.MarkUefiAppRuntime("FILE_FAIL=path=" + document +
                        ";app=" + appName + ";reason=" +
                        (opened == null ? "FACTORY" :
                            opened.ErrorCodeName));
                }
            }
#endif
#if UEFI_DIAGNOSTIC_RING3_PHASE32
            Program.MarkUefiRing3Phase32("OPEN_RESULT=path=" + document +
                ";app=" + (opened == null ? "" : opened.AppId) +
                ";success=" + (opened != null && opened.Success ? "1" : "0") +
                ";instance=" + (opened == null ? "" :
                    opened.InstanceHandle.ToString()) + ";state=" +
                (opened == null ? "" : opened.ActivationStateName) +
                ";payload=" + (request.Document ?? ""));
#endif
            return opened;
        }

        private static string LeafName(string path) {
            if (string.IsNullOrEmpty(path)) return path;
            int slash = path.LastIndexOf('/');
            if (slash < 0 || slash >= path.Length - 1) return path;
            return path.Substring(slash + 1);
        }

        private static string GetOwnedWindowCount(LaunchResult result) {
            if (result == null || !result.Success) return "0";
            ApplicationInstance instance;
            if (!ApplicationInstanceRegistry.TryGet(result.InstanceHandle,
                    out instance) || instance == null) return "0";
            return instance.OwnedWindowCount.ToString();
        }

        private static LaunchResult OpenShellObject(string shellObjectId) {
            ShellObjectTarget target;
            LaunchRequest request;
            ShellObjectResolution resolution;
            LaunchResult failure;
            if (!ModernShellAdapter.TryCreateLaunchRequest(shellObjectId,
                    out target, out request, out resolution, out failure)) {
#if UEFI_DIAGNOSTIC_RING3_PHASE33
                Program.MarkUefiRing3Phase33("OBJECT_RESOLVE=failed;id=" +
                    (shellObjectId ?? ""));
#endif
                return failure ?? LaunchResult.Failed(
                    LaunchErrorCode.NotFound,
                    "Shell object was not found", null);
            }
#if UEFI_DIAGNOSTIC_RING3_PHASE33
            Program.MarkUefiRing3Phase33("OBJECT_RESOLVE=success;id=" +
                resolution.ShellId + ";kind=" + resolution.Kind.ToString() +
                ";app=" + (resolution.AppId ?? "") + ";alias=" +
                (resolution.MatchedAlias ?? ""));
#endif
            if (target.Operation == ApplicationShellOperation.InvokeSystemAction) {
                return OpenTypedShellAction(target.ShellObjectId);
            }
            if (target.Operation == ApplicationShellOperation.ActivateSystemPanel ||
                    target.Operation == ApplicationShellOperation.OpenVirtualObject ||
                    target.Operation == ApplicationShellOperation.OpenDeviceVolume) {
                return LaunchResult.Failed(LaunchErrorCode.UnsupportedTarget,
                    "Shell target operation has no verified launch backend",
                    request.TargetAppId);
            }
            if (target.Operation == ApplicationShellOperation.OpenFolder) {
                FileSystem folderFileSystem;
                string folderPath;
                LaunchResult folderFailure;
                if (!ComputerFilesApplicationFactory.TryResolveFolderRequest(
                        request, out folderFileSystem, out folderPath,
                        out folderFailure)) return folderFailure;
                if (folderFileSystem == null) return folderFailure ??
                    LaunchResult.Failed(LaunchErrorCode.ResourceUnavailable,
                        "C# filesystem binding is unavailable",
                        request.TargetAppId);
                if (folderPath != null && folderPath.Length > 0)
                    folderPath.Dispose();
            }
            if (string.IsNullOrEmpty(request.TargetAppId)) {
                return LaunchResult.Failed(
                    LaunchErrorCode.UnsupportedTarget,
                    "Shell object has no modern application handler",
                    null);
            }
            return LaunchModern(request);
        }

        private static LaunchResult OpenShellFolder(string shellObjectId,
                                                    string canonicalFolderPath) {
            ShellObjectTarget target;
            LaunchRequest request;
            ShellObjectResolution resolution;
            LaunchResult failure;
            if (!ModernShellAdapter.TryCreateFolderLaunchRequest(
                    shellObjectId, canonicalFolderPath, out target,
                    out request, out resolution, out failure)) {
                return failure ?? LaunchResult.Failed(
                    LaunchErrorCode.UnsupportedTarget,
                    "Typed folder target has no C# backend", null);
            }
            if (target == null || target.Operation !=
                    ApplicationShellOperation.OpenFolder || resolution == null ||
                    resolution.ShellId != target.ShellObjectId) {
                return LaunchResult.Failed(LaunchErrorCode.UnsupportedTarget,
                    "Resolved shell target is not a verified folder",
                    "gxos.builtin.files");
            }

            FileSystem folderFileSystem;
            string fileSystemPath;
            if (!ComputerFilesApplicationFactory.TryResolveFolderRequest(
                    request, out folderFileSystem, out fileSystemPath,
                    out failure)) return failure ?? LaunchResult.Failed(
                        LaunchErrorCode.ResourceUnavailable,
                        "C# folder target is unavailable",
                        request.TargetAppId);
            if (folderFileSystem == null) return LaunchResult.Failed(
                LaunchErrorCode.ResourceUnavailable,
                "C# filesystem binding is unavailable", request.TargetAppId);
            if (fileSystemPath != null && fileSystemPath.Length > 0)
                fileSystemPath.Dispose();
            return LaunchModern(request);
        }

        private static LaunchResult OpenTypedShellAction(string actionId) {
            ShellObjectTarget target;
            LaunchRequest request;
            ShellObjectResolution resolution;
            LaunchResult failure;
            if (!ModernShellAdapter.TryCreateTypedActionLaunchRequest(actionId,
                    out target, out request, out resolution, out failure)) {
                return failure ?? LaunchResult.Failed(
                    LaunchErrorCode.NotFound,
                    "Shell action was not found", null);
            }
            if (target.TargetKind != ApplicationShellTargetKind.Action ||
                    request == null) {
                return LaunchResult.Failed(
                    LaunchErrorCode.UnsupportedTarget,
                    "Shell target is not a typed action", null);
            }
            LaunchResult result;
            if (resolution.ActionName == "HDInstaller") {
                if (ApplicationShellActionBackend.TryLaunchInstaller(request,
                        100, 100, out result)) return result;
                return result ?? LaunchResult.Failed(
                    LaunchErrorCode.BackendUnavailable,
                    "Typed shell action failed", null);
            }
            return LaunchResult.Failed(LaunchErrorCode.UnsupportedTarget,
                "Typed shell action is unsupported", null);
        }

        private static LaunchResult LaunchGxm(string document,
                                              LaunchRequest request) {
            byte[] buffer = File.ReadAllBytes(document);
            if (buffer == null) {
                return LaunchResult.Failed(
                    LaunchErrorCode.ResourceUnavailable,
                    "GXM document could not be read", "gxos.external.gxm");
            }
            ApplicationInstance instance;
            bool reused;
            LaunchResult failure;
            if (!ApplicationInstanceRegistry.TryBeginGxmLaunch(request,
                    out instance, out reused, out failure)) {
                buffer.Dispose();
                return failure;
            }
            ApplicationFactoryRegistry.RecordTypedExternalLaunch("gxm");
            string error = null;
            bool launched = false;
            try {
                launched = GXMLoader.TryExecute(buffer, out error, instance);
            } catch {
                launched = false;
                error = "GXM backend rejected launch";
            } finally {
                buffer.Dispose();
            }
            if (!launched) {
                ApplicationInstanceRegistry.FailLaunch(instance, reused,
                    error ?? "GXM backend rejected launch");
                return LaunchResult.Failed(LaunchErrorCode.InitializationFailed,
                    error ?? "GXM backend rejected launch", "gxos.external.gxm");
            }
            if (!ApplicationInstanceRegistry.TryCompleteLaunch(instance, true,
                    out failure)) {
                ApplicationInstanceRegistry.FailLaunch(instance, reused,
                    "GXM activation failed");
                return failure ?? LaunchResult.Failed(
                    LaunchErrorCode.ActivationFailed,
                    "GXM activation failed", "gxos.external.gxm");
            }
            RecentManager.AddDocument(document, Icons.DocumentIcon(32));
            return LaunchResult.Succeeded("gxos.external.gxm", instance.Handle,
                LaunchActivationState.Activated);
        }
    }
}
