using System;
using System.Collections.Generic;

namespace guideXOS.OS {
    /// <summary>
    /// Common application class.  This is deliberately narrower than the
    /// legacy AppKind enum: aliases, file associations, and shell targets are
    /// request/registry concepts rather than application identities.
    /// </summary>
    public enum ApplicationClass {
        Unknown,
        BuiltIn,
        Native,
        Managed,
        Composite,
        Gxm,
        Package,
        Service
    }

    public enum LaunchRequestTargetKind {
        Application,
        FileOpen,
        GxmDocument,
        ShellObject
    }

    public enum LaunchActivationIntent {
        PolicyDefault,
        Launch,
        ActivateExisting,
        NewInstance
    }

    public enum LaunchActivationState {
        None,
        Created,
        Activated,
        Failed
    }

    /// <summary>
    /// Stable error vocabulary from APP_MODEL_CONVERGENCE.md.  The enum is
    /// shared by resolution and backend adapters; UI presentation remains a
    /// compatibility concern.
    /// </summary>
    public enum LaunchErrorCode {
        Success,
        NotFound,
        AmbiguousTarget,
        UnsupportedTarget,
        MalformedRequest,
        ResourceUnavailable,
        PermissionDenied,
        InitializationFailed,
        ActivationFailed,
        AlreadyTerminated,
        BackendUnavailable
    }

    public enum ApplicationShellTargetKind {
        Virtual,
        FileSystem,
        SystemPanel,
        Action
    }

    /// <summary>
    /// Start/taskbar/recent/document policy attached to a descriptor.  These
    /// values describe current C# behavior; they do not create a new lifecycle.
    /// </summary>
    public sealed class ApplicationShellPolicy {
        public bool ShowInStartMenu { get; private set; }
        public bool ShowInTaskbar { get; private set; }
        public bool RecordRecentPrograms { get; private set; }
        public bool AcceptsDocumentTargets { get; private set; }
        public bool AcceptsFolderTargets { get; private set; }

        public ApplicationShellPolicy(bool showInStartMenu,
                                      bool showInTaskbar,
                                      bool recordRecentPrograms,
                                      bool acceptsDocumentTargets,
                                      bool acceptsFolderTargets) {
            ShowInStartMenu = showInStartMenu;
            ShowInTaskbar = showInTaskbar;
            RecordRecentPrograms = recordRecentPrograms;
            AcceptsDocumentTargets = acceptsDocumentTargets;
            AcceptsFolderTargets = acceptsFolderTargets;
        }
    }

    public sealed class ApplicationLaunchEntry {
        public string Architecture { get; private set; }
        public string EntryPoint { get; private set; }
        public string Abi { get; private set; }
        public string Runtime { get; private set; }

        public ApplicationLaunchEntry(string architecture, string entryPoint,
                                      string abi, string runtime) {
            Architecture = architecture;
            EntryPoint = entryPoint;
            Abi = abi;
            Runtime = runtime;
        }
    }

    /// <summary>
    /// Common association metadata.  The existing FileAssociationRegistry is
    /// still the source of truth; this is its typed projection.
    /// </summary>
    public sealed class ApplicationAssociation {
        public string Extension { get; private set; }
        public string ContentType { get; private set; }
        public string Description { get; private set; }
        public string Verb { get; private set; }
        public string HandlerAppId { get; private set; }
        public ApplicationClass HandlerClass { get; private set; }

        public ApplicationAssociation(string extension, string contentType,
                                      string description, string verb,
                                      string handlerAppId,
                                      ApplicationClass handlerClass) {
            Extension = extension;
            ContentType = contentType;
            Description = description;
            Verb = string.IsNullOrEmpty(verb) ? "open" : verb;
            HandlerAppId = handlerAppId;
            HandlerClass = handlerClass;
        }
    }

    /// <summary>
    /// Immutable semantic projection of an existing C# AppDescriptor.  The
    /// legacy Image is intentionally not carried here; ResourceKey is the
    /// renderer-neutral identity used by the common contract.
    /// </summary>
    public sealed class ApplicationDescriptor {
        private readonly string[] _aliases;
        private readonly string[] _capabilities;
        private readonly ApplicationAssociation[] _associations;
        private readonly ApplicationLaunchEntry[] _launchEntries;

        public string AppId { get; private set; }
        public string DisplayName { get; private set; }
        public string Version { get; private set; }
        public string ResourceKey { get; private set; }
        public ApplicationClass ApplicationClass { get; private set; }
        public ApplicationShellPolicy ShellPolicy { get; private set; }
        public int AliasCount { get { return _aliases.Length; } }
        public int CapabilityCount { get { return _capabilities.Length; } }
        public int AssociationCount { get { return _associations.Length; } }
        public int LaunchEntryCount { get { return _launchEntries.Length; } }

        // Public snapshots are copies.  Shell/runtime hot paths use the
        // count/getter methods below and therefore do not allocate.
        public string[] Aliases { get { return CopyStrings(_aliases); } }
        public string[] Capabilities { get { return CopyStrings(_capabilities); } }
        public ApplicationAssociation[] Associations {
            get { return CopyAssociations(_associations); }
        }
        public ApplicationLaunchEntry[] LaunchEntries {
            get { return CopyLaunchEntries(_launchEntries); }
        }

        internal ApplicationDescriptor(string appId, string displayName,
                                       string version, string resourceKey,
                                       ApplicationClass applicationClass,
                                       string[] aliases, string[] capabilities,
                                       ApplicationAssociation[] associations,
                                       ApplicationLaunchEntry[] launchEntries,
                                       ApplicationShellPolicy shellPolicy) {
            AppId = appId;
            DisplayName = displayName;
            Version = version;
            ResourceKey = resourceKey;
            ApplicationClass = applicationClass;
            _aliases = CloneStrings(aliases);
            _capabilities = CloneStrings(capabilities);
            _associations = CloneAssociations(associations);
            _launchEntries = CloneLaunchEntries(launchEntries);
            ShellPolicy = shellPolicy;
        }

        public string GetAlias(int index) {
            return index >= 0 && index < _aliases.Length ? _aliases[index] : null;
        }

        public string GetCapability(int index) {
            return index >= 0 && index < _capabilities.Length
                ? _capabilities[index] : null;
        }

        public ApplicationAssociation GetAssociation(int index) {
            return index >= 0 && index < _associations.Length
                ? _associations[index] : null;
        }

        public ApplicationLaunchEntry GetLaunchEntry(int index) {
            return index >= 0 && index < _launchEntries.Length
                ? _launchEntries[index] : null;
        }

        private static string[] CloneStrings(string[] values) {
            if (values == null || values.Length == 0) return new string[0];
            string[] copy = new string[values.Length];
            for (int i = 0; i < values.Length; i++) copy[i] = values[i];
            return copy;
        }

        private static string[] CopyStrings(string[] values) {
            return CloneStrings(values);
        }

        private static ApplicationAssociation[] CloneAssociations(
            ApplicationAssociation[] values) {
            if (values == null || values.Length == 0)
                return new ApplicationAssociation[0];
            ApplicationAssociation[] copy = new ApplicationAssociation[values.Length];
            for (int i = 0; i < values.Length; i++) copy[i] = values[i];
            return copy;
        }

        private static ApplicationAssociation[] CopyAssociations(
            ApplicationAssociation[] values) {
            return CloneAssociations(values);
        }

        private static ApplicationLaunchEntry[] CloneLaunchEntries(
            ApplicationLaunchEntry[] values) {
            if (values == null || values.Length == 0)
                return new ApplicationLaunchEntry[0];
            ApplicationLaunchEntry[] copy = new ApplicationLaunchEntry[values.Length];
            for (int i = 0; i < values.Length; i++) copy[i] = values[i];
            return copy;
        }

        private static ApplicationLaunchEntry[] CopyLaunchEntries(
            ApplicationLaunchEntry[] values) {
            return CloneLaunchEntries(values);
        }
    }

    /// <summary>
    /// UI-independent launch request.  The request has bounded text and
    /// argument storage and has no reference to Window, framebuffer, or shell
    /// controls.
    /// </summary>
    public sealed class LaunchRequest {
        public const int MaxArguments = 16;
        public const int MaxTextLength = 1024;

        private readonly string[] _arguments;

        public string TargetAppId { get; private set; }
        public string TargetNameOrAlias { get; private set; }
        public string Document { get; private set; }
        public string Verb { get; private set; }
        public string SourceShellObjectId { get; private set; }
        public LaunchRequestTargetKind TargetKind { get; private set; }
        public ApplicationShellTargetKind ShellTargetKind { get; private set; }
        public string ShellTargetValue { get; private set; }
        public LaunchActivationIntent ActivationIntent { get; private set; }
        public bool IsValid { get; private set; }
        public string ValidationError { get; private set; }
        public int ArgumentCount { get { return _arguments.Length; } }
        public string[] Arguments { get { return CopyStrings(_arguments); } }

        public LaunchRequest(string targetAppId, string targetNameOrAlias,
                             string[] arguments, string document, string verb,
                             string sourceShellObjectId,
                             LaunchRequestTargetKind targetKind,
                             ApplicationShellTargetKind shellTargetKind,
                             string shellTargetValue,
                             LaunchActivationIntent activationIntent) {
            TargetAppId = targetAppId;
            TargetNameOrAlias = targetNameOrAlias;
            Document = document;
            Verb = string.IsNullOrEmpty(verb) ? "open" : verb;
            SourceShellObjectId = sourceShellObjectId;
            TargetKind = targetKind;
            ShellTargetKind = shellTargetKind;
            ShellTargetValue = shellTargetValue;
            ActivationIntent = activationIntent;
            _arguments = CloneStrings(arguments);
            IsValid = Validate(out string error);
            ValidationError = error;
        }

        public static LaunchRequest ForName(string name) {
            return new LaunchRequest(null, name, null, null, "open", null,
                LaunchRequestTargetKind.Application,
                ApplicationShellTargetKind.Virtual, null,
                LaunchActivationIntent.PolicyDefault);
        }

        public static LaunchRequest ForAppId(string appId, string[] arguments,
                                            string document,
                                            LaunchActivationIntent intent) {
            return new LaunchRequest(appId, null, arguments, document, "open", null,
                LaunchRequestTargetKind.Application,
                ApplicationShellTargetKind.Virtual, null, intent);
        }

        public static LaunchRequest ForFile(string appId, string document,
                                            string[] arguments, string verb,
                                            string sourceShellObjectId,
                                            bool gxm,
                                            LaunchActivationIntent intent) {
            return new LaunchRequest(appId, null, arguments, document, verb,
                sourceShellObjectId,
                gxm ? LaunchRequestTargetKind.GxmDocument : LaunchRequestTargetKind.FileOpen,
                ApplicationShellTargetKind.FileSystem, document, intent);
        }

        public static LaunchRequest ForShellObject(string shellObjectId,
                                                   string targetAppId,
                                                   string targetNameOrAlias,
                                                   ApplicationShellTargetKind targetKind,
                                                   string targetValue,
                                                   LaunchActivationIntent intent) {
            return new LaunchRequest(targetAppId, targetNameOrAlias, null, null,
                "open", shellObjectId, LaunchRequestTargetKind.ShellObject,
                targetKind, targetValue, intent);
        }

        private bool Validate(out string error) {
            error = null;
            if (!ValidText(TargetAppId) || !ValidText(TargetNameOrAlias) ||
                !ValidText(Document) || !ValidText(Verb) ||
                !ValidText(SourceShellObjectId) || !ValidText(ShellTargetValue)) {
                error = "Launch request text exceeds bound";
                return false;
            }
            if (_arguments.Length > MaxArguments) {
                error = "Launch request argument count exceeds bound";
                return false;
            }
            for (int i = 0; i < _arguments.Length; i++) {
                if (!ValidText(_arguments[i])) {
                    error = "Launch request argument exceeds bound";
                    return false;
                }
            }

            if (TargetKind == LaunchRequestTargetKind.Application) {
                if (string.IsNullOrEmpty(TargetAppId) &&
                    string.IsNullOrEmpty(TargetNameOrAlias)) {
                    error = "Application target is missing";
                    return false;
                }
            } else if (TargetKind == LaunchRequestTargetKind.FileOpen ||
                       TargetKind == LaunchRequestTargetKind.GxmDocument) {
                if (string.IsNullOrEmpty(Document)) {
                    error = "Document target is missing";
                    return false;
                }
            } else if (TargetKind == LaunchRequestTargetKind.ShellObject) {
                if (string.IsNullOrEmpty(SourceShellObjectId)) {
                    error = "Shell object source is missing";
                    return false;
                }
            }
            return true;
        }

        private static bool ValidText(string value) {
            return value == null || value.Length <= MaxTextLength;
        }

        private static string[] CloneStrings(string[] values) {
            if (values == null || values.Length == 0) return new string[0];
            string[] copy = new string[values.Length];
            for (int i = 0; i < values.Length; i++) copy[i] = values[i];
            return copy;
        }

        private static string[] CopyStrings(string[] values) {
            return CloneStrings(values);
        }
    }

    /// <summary>
    /// Typed/bounded launch result.  Phase 1 intentionally leaves InstanceId
    /// null because application-instance lifecycle is a Phase 2 concern.
    /// </summary>
    public sealed class LaunchResult {
        public bool Success { get; private set; }
        public string AppId { get; private set; }
        public string InstanceId { get; private set; }
        public LaunchActivationState ActivationState { get; private set; }
        public LaunchErrorCode ErrorCode { get; private set; }
        public string BoundedDiagnostic { get; private set; }

        private LaunchResult(bool success, string appId, string instanceId,
                             LaunchActivationState activationState,
                             LaunchErrorCode errorCode, string diagnostic) {
            Success = success;
            AppId = appId;
            InstanceId = instanceId;
            ActivationState = activationState;
            ErrorCode = errorCode;
            BoundedDiagnostic = BoundDiagnostic(diagnostic);
        }

        public static LaunchResult Succeeded(string appId) {
            return new LaunchResult(true, appId, null,
                LaunchActivationState.Activated, LaunchErrorCode.Success, null);
        }

        public static LaunchResult Failed(LaunchErrorCode errorCode,
                                          string diagnostic, string appId) {
            if (errorCode == LaunchErrorCode.Success)
                errorCode = LaunchErrorCode.InitializationFailed;
            return new LaunchResult(false, appId, null,
                LaunchActivationState.Failed, errorCode, diagnostic);
        }

        private static string BoundDiagnostic(string diagnostic) {
            if (string.IsNullOrEmpty(diagnostic)) return diagnostic;
            const int maxDiagnosticLength = 192;
            if (diagnostic.Length <= maxDiagnosticLength) return diagnostic;
            return diagnostic.Substring(0, maxDiagnosticLength);
        }
    }

    /// <summary>
    /// A typed shell target projected from the existing C# shell registry.
    /// </summary>
    public sealed class ShellObjectTarget {
        public string ShellObjectId { get; private set; }
        public string DisplayName { get; private set; }
        public ApplicationShellTargetKind TargetKind { get; private set; }
        public string CanonicalTarget { get; private set; }
        public string DefaultHandlerAppId { get; private set; }
        public bool SystemOnly { get; private set; }
        public bool RiskyDestructive { get; private set; }
        public bool RecordRecentPrograms { get; private set; }

        internal ShellObjectTarget(string shellObjectId, string displayName,
                                   ApplicationShellTargetKind targetKind,
                                   string canonicalTarget,
                                   string defaultHandlerAppId,
                                   bool systemOnly, bool riskyDestructive,
                                   bool recordRecentPrograms) {
            ShellObjectId = shellObjectId;
            DisplayName = displayName;
            TargetKind = targetKind;
            CanonicalTarget = canonicalTarget;
            DefaultHandlerAppId = defaultHandlerAppId;
            SystemOnly = systemOnly;
            RiskyDestructive = riskyDestructive;
            RecordRecentPrograms = recordRecentPrograms;
        }
    }

    /// <summary>
    /// Static metadata projection over the proven twelve-entry resolver.
    /// There is no dynamic package scan in Phase 1.
    /// </summary>
    public static class ApplicationDescriptorRegistry {
        private static List<ApplicationDescriptor> _descriptors;
        private static bool _valid;
        private static string _validationFailure;

        public static void Initialize() {
            if (_descriptors != null) return;
            _descriptors = new List<ApplicationDescriptor>();
            AppLaunchResolver.InitializeDefaultDescriptors();
            int count = AppLaunchResolver.DescriptorCount;
            for (int i = 0; i < count; i++) {
                AppDescriptor legacy = AppLaunchResolver.GetDescriptorAt(i);
                if (legacy == null) continue;
                _descriptors.Add(Project(legacy));
            }
            _valid = Validate(_descriptors, out _validationFailure);
        }

        public static int Count {
            get { Initialize(); return _descriptors.Count; }
        }

        public static bool IsValid {
            get { Initialize(); return _valid; }
        }

        public static string ValidationFailure {
            get { Initialize(); return _validationFailure; }
        }

        public static ApplicationDescriptor GetAt(int index) {
            Initialize();
            return index >= 0 && index < _descriptors.Count ? _descriptors[index] : null;
        }

        public static bool TryGetById(string appId, out ApplicationDescriptor descriptor) {
            Initialize();
            descriptor = null;
            if (string.IsNullOrEmpty(appId)) return false;
            for (int i = 0; i < _descriptors.Count; i++) {
                if (TextEqualsIgnoreCase(_descriptors[i].AppId, appId)) {
                    descriptor = _descriptors[i];
                    return true;
                }
            }
            return false;
        }

        public static bool TryResolve(LaunchRequest request,
                                      out ApplicationDescriptor descriptor,
                                      out string matchedAlias,
                                      out LaunchResult failure) {
            Initialize();
            descriptor = null;
            matchedAlias = null;
            failure = null;
            if (request == null || !request.IsValid) {
                failure = LaunchResult.Failed(LaunchErrorCode.MalformedRequest,
                    request == null ? "Launch request is null" : request.ValidationError,
                    null);
                return false;
            }
            if (request.TargetKind != LaunchRequestTargetKind.Application) {
                failure = LaunchResult.Failed(LaunchErrorCode.UnsupportedTarget,
                    "Request is not an application target", request.TargetAppId);
                return false;
            }
            string input = !string.IsNullOrEmpty(request.TargetAppId)
                ? request.TargetAppId : request.TargetNameOrAlias;
            AppLaunchResolution legacy = AppLaunchResolver.Resolve(input);
            if (!legacy.Success || !TryGetById(legacy.AppId, out descriptor)) {
                failure = LaunchResult.Failed(LaunchErrorCode.NotFound,
                    legacy.FailureReason ?? "No matching application descriptor", null);
                return false;
            }
            matchedAlias = legacy.MatchedAlias;
            return true;
        }

        public static bool RunSelfTest() {
            Initialize();
            int passed = 0;
            int failed = 0;
            string failure = null;

            string[] expectedIds = new string[] {
                "gxos.builtin.calculator",
                "gxos.builtin.files",
                "gxos.builtin.console",
                "gxos.builtin.devices",
                "gxos.builtin.diskmanager",
                "gxos.builtin.displayoptions",
                "gxos.builtin.firewall",
                "gxos.builtin.notepad",
                "gxos.builtin.paint",
                "gxos.builtin.taskmanager",
                "gxos.builtin.imageviewer",
                "gxos.builtin.wavplayer"
            };

            Check(Count == expectedIds.Length, "descriptor count", ref passed,
                ref failed, ref failure);
            for (int i = 0; i < expectedIds.Length; i++) {
                ApplicationDescriptor descriptor = GetAt(i);
                Check(descriptor != null && descriptor.AppId == expectedIds[i] &&
                      !string.IsNullOrEmpty(descriptor.Version) &&
                      !string.IsNullOrEmpty(descriptor.ResourceKey) &&
                      descriptor.ApplicationClass == ApplicationClass.BuiltIn,
                      "descriptor identity " + i.ToString(), ref passed,
                      ref failed, ref failure);
            }
            Check(IsValid, "descriptor validation", ref passed, ref failed,
                ref failure);

            LaunchRequest aliasRequest = LaunchRequest.ForName("File Explorer");
            ApplicationDescriptor aliasDescriptor;
            string matchedAlias;
            LaunchResult resolutionFailure;
            bool aliasOk = TryResolve(aliasRequest, out aliasDescriptor,
                out matchedAlias, out resolutionFailure) &&
                aliasDescriptor.AppId == "gxos.builtin.files" &&
                matchedAlias == "File Explorer";
            Check(aliasOk, "alias canonicalization", ref passed, ref failed,
                ref failure);

            LaunchRequest unknownRequest = LaunchRequest.ForAppId(
                "gxos.builtin.notreal", null, null,
                LaunchActivationIntent.Launch);
            bool unknownRejected = !TryResolve(unknownRequest, out aliasDescriptor,
                out matchedAlias, out resolutionFailure) &&
                resolutionFailure != null &&
                resolutionFailure.ErrorCode == LaunchErrorCode.NotFound;
            Check(unknownRejected, "unknown app bounded failure", ref passed,
                ref failed, ref failure);

            LaunchRequest unknownAliasRequest = LaunchRequest.ForName(
                "Definitely Not A Real App");
            bool unknownAliasRejected = !TryResolve(unknownAliasRequest,
                out aliasDescriptor, out matchedAlias, out resolutionFailure) &&
                resolutionFailure != null &&
                resolutionFailure.ErrorCode == LaunchErrorCode.NotFound;
            Check(unknownAliasRejected, "unknown alias bounded failure",
                ref passed, ref failed, ref failure);

            string[] arguments = new string[] { "--safe", "document.txt" };
            LaunchRequest documentRequest = LaunchRequest.ForFile(
                "gxos.builtin.notepad", "Programs/document.txt", arguments,
                "open", "gxos.shell.computerfiles", false,
                LaunchActivationIntent.Launch);
            Check(documentRequest.IsValid && documentRequest.Document == "Programs/document.txt" &&
                  documentRequest.ArgumentCount == 2 &&
                  documentRequest.Arguments[0] == "--safe" &&
                  documentRequest.SourceShellObjectId == "gxos.shell.computerfiles" &&
                  documentRequest.ActivationIntent == LaunchActivationIntent.Launch,
                  "document argument source preservation", ref passed,
                  ref failed, ref failure);

            LaunchRequest malformedRequest = LaunchRequest.ForName(null);
            ApplicationDescriptor ignoredDescriptor;
            bool malformedRejected = !TryResolve(malformedRequest,
                out ignoredDescriptor, out matchedAlias, out resolutionFailure) &&
                resolutionFailure != null &&
                resolutionFailure.ErrorCode == LaunchErrorCode.MalformedRequest;
            Check(malformedRejected, "malformed request bounded failure", ref passed,
                ref failed, ref failure);

            FileAssociationResolution association = FileAssociationRegistry.Resolve(".txt");
            Check(association.Success && association.AppId == "gxos.builtin.notepad",
                "association canonical handler", ref passed, ref failed,
                ref failure);

            ApplicationAssociation unknownAssociation;
            LaunchRequest unknownFileRequest;
            LaunchResult unknownAssociationFailure;
            bool unknownAssociationRejected =
                !ModernFileAssociationAdapter.TryCreateLaunchRequest(
                    "Programs/unknown.zzz", null, out unknownAssociation,
                    out unknownFileRequest, out unknownAssociationFailure) &&
                unknownAssociationFailure != null &&
                unknownAssociationFailure.ErrorCode == LaunchErrorCode.UnsupportedTarget;
            Check(unknownAssociationRejected,
                "unknown association bounded failure", ref passed,
                ref failed, ref failure);

            ShellObjectTarget shellTarget;
            LaunchRequest shellRequest;
            ShellObjectResolution shellResolution;
            LaunchResult shellFailure;
            bool shellOk = ModernShellAdapter.TryCreateLaunchRequest(
                "File Explorer", out shellTarget, out shellRequest,
                out shellResolution, out shellFailure) &&
                shellTarget.ShellObjectId == "gxos.shell.computerfiles" &&
                shellRequest.SourceShellObjectId == "gxos.shell.computerfiles" &&
                shellRequest.TargetAppId == "gxos.builtin.files";
            Check(shellOk, "shell typed request", ref passed, ref failed,
                ref failure);

            bool unknownShellRejected = !ModernShellAdapter.TryCreateLaunchRequest(
                "Unknown shell object", out shellTarget, out shellRequest,
                out shellResolution, out shellFailure) &&
                shellFailure != null &&
                shellFailure.ErrorCode == LaunchErrorCode.NotFound;
            Check(unknownShellRejected, "unknown shell bounded failure",
                ref passed, ref failed, ref failure);

            AppLaunchResolver.EmitSelfTestSummary("AppModelPhase1", passed,
                failed, failure);
            return failed == 0;
        }

        private static ApplicationDescriptor Project(AppDescriptor legacy) {
            string[] aliases = legacy.LegacyAliases ?? new string[0];
            ApplicationAssociation[] associations =
                ApplicationAssociationRegistry.GetForHandler(legacy.AppId);
            bool acceptsDocuments = associations.Length != 0;
            bool acceptsFolders = legacy.AppId == "gxos.builtin.files";
            return new ApplicationDescriptor(
                legacy.AppId,
                legacy.DisplayName,
                "1.0.0",
                ResourceKeyFor(legacy.AppId),
                ApplicationClass.BuiltIn,
                aliases,
                new string[0],
                associations,
                new ApplicationLaunchEntry[] {
                    new ApplicationLaunchEntry("x64", legacy.DispatchName,
                        "managed", "managed-csharp-uefi")
                },
                new ApplicationShellPolicy(true, true, true,
                    acceptsDocuments, acceptsFolders));
        }

        private static string ResourceKeyFor(string appId) {
            if (appId == "gxos.builtin.calculator") return "app.calculator";
            if (appId == "gxos.builtin.files") return "app.files";
            if (appId == "gxos.builtin.console") return "app.console";
            if (appId == "gxos.builtin.devices") return "app.devices";
            if (appId == "gxos.builtin.diskmanager") return "app.diskmanager";
            if (appId == "gxos.builtin.displayoptions") return "app.displayoptions";
            if (appId == "gxos.builtin.firewall") return "app.firewall";
            if (appId == "gxos.builtin.notepad") return "app.notepad";
            if (appId == "gxos.builtin.paint") return "app.paint";
            if (appId == "gxos.builtin.taskmanager") return "app.taskmanager";
            if (appId == "gxos.builtin.imageviewer") return "app.imageviewer";
            if (appId == "gxos.builtin.wavplayer") return "app.wavplayer";
            return "app.unknown";
        }

        private static bool Validate(List<ApplicationDescriptor> descriptors,
                                     out string failure) {
            failure = null;
            if (descriptors == null) {
                failure = "Descriptor list is null";
                return false;
            }
            for (int i = 0; i < descriptors.Count; i++) {
                ApplicationDescriptor descriptor = descriptors[i];
                if (descriptor == null || string.IsNullOrEmpty(descriptor.AppId) ||
                    string.IsNullOrEmpty(descriptor.DisplayName) ||
                    string.IsNullOrEmpty(descriptor.Version)) {
                    failure = "Malformed descriptor at index " + i.ToString();
                    return false;
                }
                for (int a = 0; a < descriptor.AliasCount; a++) {
                    if (string.IsNullOrEmpty(descriptor.GetAlias(a))) {
                        failure = "Malformed application alias";
                        return false;
                    }
                    for (int b = 0; b < a; b++) {
                        if (TextEqualsIgnoreCase(descriptor.GetAlias(a),
                                descriptor.GetAlias(b))) {
                            failure = "Duplicate application alias";
                            return false;
                        }
                    }
                }
                for (int j = 0; j < i; j++) {
                    if (TextEqualsIgnoreCase(descriptor.AppId,
                            descriptors[j].AppId)) {
                        failure = "Duplicate application ID";
                        return false;
                    }
                    for (int a = 0; a < descriptor.AliasCount; a++) {
                        for (int b = 0; b < descriptors[j].AliasCount; b++) {
                            if (TextEqualsIgnoreCase(descriptor.GetAlias(a),
                                    descriptors[j].GetAlias(b))) {
                                failure = "Duplicate application alias";
                                return false;
                            }
                        }
                    }
                }
            }
            return true;
        }

        private static bool TextEqualsIgnoreCase(string a, string b) {
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

        private static void Check(bool condition, string label, ref int passed,
                                  ref int failed, ref string failure) {
            if (condition) {
                passed++;
                return;
            }
            if (failure == null) failure = label;
            failed++;
        }
    }

    /// <summary>
    /// Typed association projection and document-request adapter.  It never
    /// opens a file; Desktop continues to own the existing direct handlers.
    /// </summary>
    public static class ApplicationAssociationRegistry {
        private static ApplicationAssociation[] _associations;

        private static void Initialize() {
            if (_associations != null) return;
            int count = FileAssociationRegistry.DescriptorCount;
            _associations = new ApplicationAssociation[count];
            for (int i = 0; i < count; i++) {
                FileAssociationDescriptor legacy =
                    FileAssociationRegistry.GetDescriptorAt(i);
                if (legacy == null) continue;
                ApplicationClass handlerClass = legacy.Kind == AppKind.GxmApp
                    ? ApplicationClass.Gxm : ApplicationClass.BuiltIn;
                _associations[i] = new ApplicationAssociation(
                    legacy.Extension, null, null, "open", legacy.AppId,
                    handlerClass);
            }
        }

        public static int Count {
            get { Initialize(); return _associations.Length; }
        }

        public static ApplicationAssociation GetAt(int index) {
            Initialize();
            return index >= 0 && index < _associations.Length
                ? _associations[index] : null;
        }

        public static bool TryGetByExtension(string extension,
                                             out ApplicationAssociation association) {
            association = null;
            FileAssociationResolution resolved =
                FileAssociationRegistry.Resolve(extension);
            if (!resolved.Success) return false;
            for (int i = 0; i < Count; i++) {
                ApplicationAssociation candidate = GetAt(i);
                if (candidate != null && TextEqualsIgnoreCase(
                        candidate.Extension, resolved.Extension)) {
                    association = candidate;
                    return true;
                }
            }
            return false;
        }

        private static bool TextEqualsIgnoreCase(string a, string b) {
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

        internal static ApplicationAssociation[] GetForHandler(string appId) {
            List<ApplicationAssociation> values =
                new List<ApplicationAssociation>();
            for (int i = 0; i < Count; i++) {
                ApplicationAssociation association = GetAt(i);
                if (association != null && association.HandlerAppId == appId)
                    values.Add(association);
            }
            ApplicationAssociation[] result =
                new ApplicationAssociation[values.Count];
            for (int i = 0; i < values.Count; i++) result[i] = values[i];
            return result;
        }
    }

    public static class ModernFileAssociationAdapter {
        public static bool TryCreateLaunchRequest(
            string document, string sourceShellObjectId,
            out ApplicationAssociation association,
            out LaunchRequest request, out LaunchResult failure) {
            association = null;
            request = null;
            failure = null;
            FileAssociationResolution legacy =
                FileAssociationRegistry.ResolvePath(document);
            if (!legacy.Success) {
                failure = LaunchResult.Failed(LaunchErrorCode.UnsupportedTarget,
                    legacy.FailureReason, null);
                return false;
            }
            if (!ApplicationAssociationRegistry.TryGetByExtension(
                    legacy.Extension, out association)) {
                failure = LaunchResult.Failed(LaunchErrorCode.UnsupportedTarget,
                    "Association metadata unavailable", legacy.AppId);
                return false;
            }
            bool gxm = association.HandlerClass == ApplicationClass.Gxm;
            request = LaunchRequest.ForFile(
                gxm ? null : association.HandlerAppId,
                document, null, association.Verb, sourceShellObjectId,
                gxm, LaunchActivationIntent.Launch);
            if (!request.IsValid) {
                failure = LaunchResult.Failed(LaunchErrorCode.MalformedRequest,
                    request.ValidationError, association.HandlerAppId);
                request = null;
                return false;
            }
            return true;
        }
    }

    public static class ModernShellAdapter {
        public static bool TryCreateLaunchRequest(
            string input, out ShellObjectTarget target,
            out LaunchRequest request, out ShellObjectResolution resolution,
            out LaunchResult failure) {
            target = null;
            request = null;
            resolution = ShellObjectRegistry.Resolve(input);
            failure = null;
            if (!resolution.Success) {
                failure = LaunchResult.Failed(LaunchErrorCode.NotFound,
                    resolution.FailureReason, null);
                return false;
            }

            ApplicationShellTargetKind targetKind;
            string canonicalTarget;
            switch (resolution.Kind) {
                case ShellObjectKind.FileSystemLocation:
                    targetKind = ApplicationShellTargetKind.FileSystem;
                    canonicalTarget = resolution.Path;
                    break;
                case ShellObjectKind.SystemAction:
                    targetKind = ApplicationShellTargetKind.Action;
                    canonicalTarget = resolution.ActionName;
                    break;
                case ShellObjectKind.DeviceVolume:
                    targetKind = ApplicationShellTargetKind.FileSystem;
                    canonicalTarget = resolution.DeviceName;
                    break;
                default:
                    targetKind = ApplicationShellTargetKind.Virtual;
                    canonicalTarget = resolution.DispatchName;
                    break;
            }
            target = new ShellObjectTarget(resolution.ShellId,
                resolution.DisplayName, targetKind, canonicalTarget,
                resolution.AppId, true, false, true);
            request = LaunchRequest.ForShellObject(
                resolution.ShellId, resolution.AppId,
                resolution.ActionName ?? resolution.DispatchName,
                targetKind, canonicalTarget,
                LaunchActivationIntent.Launch);
            if (!request.IsValid) {
                failure = LaunchResult.Failed(LaunchErrorCode.MalformedRequest,
                    request.ValidationError, resolution.AppId);
                request = null;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Compatibility boundary for AppCollection.  Resolution is modern and
    /// typed; dispatch remains the existing managed switch in AppCollection.
    /// </summary>
    public static class AppLaunchCompatibilityAdapter {
        public static LaunchResult Launch(AppCollection collection,
                                          LaunchRequest request) {
            AppLaunchResolution ignoredResolution;
            return Launch(collection, request, out ignoredResolution);
        }

        internal static LaunchResult Launch(AppCollection collection,
                                            LaunchRequest request,
                                            out AppLaunchResolution legacyResolution) {
            ApplicationDescriptor descriptor;
            LaunchResult failure;
            if (!TryResolveForLegacyCollection(collection, request,
                    out legacyResolution, out descriptor, out failure))
                return failure;
            return DispatchToLegacyBackend(collection, request, legacyResolution);
        }

        internal static bool TryResolveForLegacyCollection(
            AppCollection collection, LaunchRequest request,
            out AppLaunchResolution legacyResolution,
            out ApplicationDescriptor descriptor, out LaunchResult failure) {
            legacyResolution = null;
            descriptor = null;
            failure = null;
            if (request == null || !request.IsValid) {
                failure = LaunchResult.Failed(LaunchErrorCode.MalformedRequest,
                    request == null ? "Launch request is null" : request.ValidationError,
                    null);
                return false;
            }
            if (request.TargetKind != LaunchRequestTargetKind.Application) {
                failure = LaunchResult.Failed(LaunchErrorCode.UnsupportedTarget,
                    "Request is not an application target", request.TargetAppId);
                return false;
            }

            string input = !string.IsNullOrEmpty(request.TargetAppId)
                ? request.TargetAppId : request.TargetNameOrAlias;
            legacyResolution = AppLaunchResolver.Resolve(input);
            if (legacyResolution.Success) {
                if (!ApplicationDescriptorRegistry.TryGetById(
                        legacyResolution.AppId, out descriptor)) {
                    failure = LaunchResult.Failed(
                        LaunchErrorCode.InitializationFailed,
                        "Application descriptor projection unavailable",
                        legacyResolution.AppId);
                    return false;
                }
                return true;
            }

            // AppCollection.Add is an intentionally retained legacy extension
            // point.  It may still dispatch a legacy-only entry by its name,
            // but unknown stable IDs never fall through to that path.
            if (string.IsNullOrEmpty(request.TargetAppId) && collection != null &&
                collection.HasLegacyAppName(request.TargetNameOrAlias)) {
                return true;
            }

            failure = LaunchResult.Failed(LaunchErrorCode.NotFound,
                legacyResolution.FailureReason ?? "No matching application", null);
            return false;
        }

        internal static LaunchResult DispatchToLegacyBackend(
            AppCollection collection, LaunchRequest request,
            AppLaunchResolution legacyResolution) {
            if (collection == null)
                return LaunchResult.Failed(LaunchErrorCode.BackendUnavailable,
                    "Application collection is unavailable", null);
            bool launched = collection.LoadLegacyBackend(request,
                legacyResolution);
            if (launched) {
                return LaunchResult.Succeeded(
                    legacyResolution == null ? null : legacyResolution.AppId);
            }
            return LaunchResult.Failed(LaunchErrorCode.InitializationFailed,
                "Existing C# application backend rejected the launch",
                legacyResolution == null ? null : legacyResolution.AppId);
        }
    }
}
