using System;

namespace guideXOS.OS {
    /// <summary>
    /// Describes the strength of an application's lifecycle implementation.
    /// SupportedWithDefault is cooperative and intentionally lightweight; it
    /// does not freeze threads, timers, or the scheduler.
    /// </summary>
    public enum ApplicationLifecycleCapability {
        Unsupported,
        SupportedWithDefault,
        SupportedWithCustom
    }

    /// <summary>
    /// Result returned by one application lifecycle callback.
    /// </summary>
    public enum ApplicationLifecycleCallbackResult {
        Success,
        Unsupported,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Typed lifecycle operations.  The request is deliberately independent
    /// from Window, Taskbar, and other graphical controls.
    /// </summary>
    public enum ApplicationLifecycleOperation {
        Activate,
        Deactivate,
        Suspend,
        Resume,
        RequestClose,
        Terminate
    }

    /// <summary>
    /// Bounded close reason vocabulary for the current App Model.
    /// </summary>
    public enum ApplicationCloseReason {
        UserRequest,
        ShellRequest,
        ApplicationRequest,
        Shutdown,
        Failure,
        ForcedTermination
    }

    /// <summary>
    /// A small, typed lifecycle request.  It is useful to future isolated
    /// process backends without making the current managed backend a process
    /// manager.
    /// </summary>
    public sealed class ApplicationLifecycleRequest {
        private readonly ApplicationLifecycleOperation _operation;
        private readonly ApplicationInstanceHandle _handle;
        private readonly ApplicationCloseReason _closeReason;

        public ApplicationLifecycleRequest(
                ApplicationLifecycleOperation operation,
                ApplicationInstanceHandle handle,
                ApplicationCloseReason closeReason) {
            _operation = operation;
            _handle = handle;
            _closeReason = closeReason;
        }

        public ApplicationLifecycleOperation Operation { get { return _operation; } }
        public ApplicationInstanceHandle InstanceHandle { get { return _handle; } }
        public ApplicationCloseReason CloseReason { get { return _closeReason; } }
    }

    /// <summary>
    /// Bounded lifecycle result.  Callers can distinguish unsupported work,
    /// invalid state, cancellation, stale handles, and callback failures
    /// without interpreting a boolean or a shell diagnostic string.
    /// </summary>
    public sealed class ApplicationLifecycleResult {
        private readonly ApplicationLifecycleResultCode _code;
        private readonly ApplicationInstanceHandle _handle;
        private readonly ApplicationInstanceLifecycleState _fromState;
        private readonly ApplicationInstanceLifecycleState _toState;
        private readonly ApplicationCloseReason _closeReason;
        private readonly string _diagnostic;

        private ApplicationLifecycleResult(
                ApplicationLifecycleResultCode code,
                ApplicationInstanceHandle handle,
                ApplicationInstanceLifecycleState fromState,
                ApplicationInstanceLifecycleState toState,
                ApplicationCloseReason closeReason,
                string diagnostic) {
            _code = code;
            _handle = handle;
            _fromState = fromState;
            _toState = toState;
            _closeReason = closeReason;
            _diagnostic = diagnostic;
        }

        public ApplicationLifecycleResultCode Code { get { return _code; } }
        public bool Success {
            get { return _code == ApplicationLifecycleResultCode.Success; }
        }
        public ApplicationInstanceHandle InstanceHandle { get { return _handle; } }
        public ApplicationInstanceLifecycleState FromState { get { return _fromState; } }
        public ApplicationInstanceLifecycleState ToState { get { return _toState; } }
        public ApplicationCloseReason CloseReason { get { return _closeReason; } }
        public string Diagnostic { get { return _diagnostic; } }
        public string CodeName { get { return CodeNameOf(_code); } }

        public static ApplicationLifecycleResult Succeeded(
                ApplicationInstanceHandle handle,
                ApplicationInstanceLifecycleState fromState,
                ApplicationInstanceLifecycleState toState,
                ApplicationCloseReason closeReason) {
            return new ApplicationLifecycleResult(
                ApplicationLifecycleResultCode.Success, handle, fromState,
                toState, closeReason, null);
        }

        public static ApplicationLifecycleResult Failed(
                ApplicationLifecycleResultCode code,
                ApplicationInstanceHandle handle,
                ApplicationInstanceLifecycleState state,
                ApplicationCloseReason closeReason,
                string diagnostic) {
            return new ApplicationLifecycleResult(code, handle, state, state,
                closeReason, BoundDiagnostic(diagnostic));
        }

        public static string CodeNameOf(ApplicationLifecycleResultCode code) {
            switch (code) {
                case ApplicationLifecycleResultCode.Success: return "Success";
                case ApplicationLifecycleResultCode.Unsupported: return "Unsupported";
                case ApplicationLifecycleResultCode.InvalidState: return "InvalidState";
                case ApplicationLifecycleResultCode.Cancelled: return "Cancelled";
                case ApplicationLifecycleResultCode.NotFound: return "NotFound";
                case ApplicationLifecycleResultCode.CallbackFailed: return "CallbackFailed";
                default: return "Unknown";
            }
        }

        private static string BoundDiagnostic(string diagnostic) {
            if (string.IsNullOrEmpty(diagnostic) || diagnostic.Length <= 192) {
                return diagnostic;
            }
            return diagnostic.Substring(0, 192);
        }
    }

    public enum ApplicationLifecycleResultCode {
        Success,
        Unsupported,
        InvalidState,
        Cancelled,
        NotFound,
        CallbackFailed
    }

    /// <summary>
    /// Read-only context passed to lifecycle callbacks.  It exposes semantic
    /// application-instance data but no shell controls, taskbar objects, or
    /// raw scheduler/process handles.
    /// </summary>
    public sealed class ApplicationLifecycleContext {
        private readonly ApplicationInstance _instance;
        private readonly ApplicationLifecycleOperation _operation;
        private readonly ApplicationCloseReason _closeReason;

        internal ApplicationLifecycleContext(ApplicationInstance instance,
                                             ApplicationLifecycleOperation operation,
                                             ApplicationCloseReason closeReason) {
            _instance = instance;
            _operation = operation;
            _closeReason = closeReason;
        }

        public ApplicationInstance Instance { get { return _instance; } }
        public ApplicationInstanceHandle InstanceHandle {
            get { return _instance == null ? ApplicationInstanceHandle.None : _instance.Handle; }
        }
        public string ApplicationId {
            get { return _instance == null ? null : _instance.ApplicationId; }
        }
        public string Document {
            get { return _instance == null ? null : _instance.Document; }
        }
        public int OwnedWindowCount {
            get { return _instance == null ? 0 : _instance.OwnedWindowCount; }
        }
        public ApplicationInstanceLifecycleState State {
            get { return _instance == null ? ApplicationInstanceLifecycleState.Failed : _instance.LifecycleState; }
        }
        public ApplicationLifecycleOperation Operation { get { return _operation; } }
        public ApplicationCloseReason CloseReason { get { return _closeReason; } }
    }

    /// <summary>
    /// Safe default lifecycle adapter.  Cooperative suspension retains the
    /// application instance and its windows and performs no global runtime
    /// or scheduler operation.
    /// </summary>
    public class ApplicationLifecycleAdapter {
        public virtual ApplicationLifecycleCapability Capability {
            get { return ApplicationLifecycleCapability.SupportedWithDefault; }
        }

        public virtual ApplicationLifecycleCallbackResult OnActivating(
                ApplicationLifecycleContext context) {
            return ApplicationLifecycleCallbackResult.Success;
        }

        public virtual ApplicationLifecycleCallbackResult OnDeactivating(
                ApplicationLifecycleContext context) {
            return ApplicationLifecycleCallbackResult.Success;
        }

        public virtual ApplicationLifecycleCallbackResult OnSuspending(
                ApplicationLifecycleContext context) {
            return Capability == ApplicationLifecycleCapability.Unsupported
                ? ApplicationLifecycleCallbackResult.Unsupported
                : ApplicationLifecycleCallbackResult.Success;
        }

        public virtual ApplicationLifecycleCallbackResult OnResuming(
                ApplicationLifecycleContext context) {
            return Capability == ApplicationLifecycleCapability.Unsupported
                ? ApplicationLifecycleCallbackResult.Unsupported
                : ApplicationLifecycleCallbackResult.Success;
        }

        public virtual ApplicationLifecycleCallbackResult OnCloseRequested(
                ApplicationLifecycleContext context,
                ApplicationCloseReason reason) {
            return ApplicationLifecycleCallbackResult.Success;
        }

        public virtual ApplicationLifecycleCallbackResult OnTerminating(
                ApplicationLifecycleContext context,
                ApplicationCloseReason reason) {
            return ApplicationLifecycleCallbackResult.Success;
        }

        public virtual void OnLifecycleFailure(ApplicationLifecycleContext context,
                                               ApplicationLifecycleOperation operation,
                                               string diagnostic) {
        }
    }

    // Representative application adapters deliberately keep their policy
    // small.  Their windows and document/resource state remain owned by the
    // existing application implementation while the common registry owns
    // lifecycle transitions.
    internal sealed class CalculatorApplicationLifecycleAdapter : ApplicationLifecycleAdapter {
        public override ApplicationLifecycleCapability Capability {
            get { return ApplicationLifecycleCapability.SupportedWithCustom; }
        }
    }

    internal sealed class NotepadApplicationLifecycleAdapter : ApplicationLifecycleAdapter {
        public override ApplicationLifecycleCapability Capability {
            get { return ApplicationLifecycleCapability.SupportedWithCustom; }
        }
    }

    internal sealed class ConsoleApplicationLifecycleAdapter : ApplicationLifecycleAdapter {
        public override ApplicationLifecycleCapability Capability {
            get { return ApplicationLifecycleCapability.SupportedWithCustom; }
        }
    }

    internal sealed class ImageViewerApplicationLifecycleAdapter : ApplicationLifecycleAdapter {
        public override ApplicationLifecycleCapability Capability {
            get { return ApplicationLifecycleCapability.SupportedWithCustom; }
        }
    }

    internal sealed class LifecycleSelfTestAdapter : ApplicationLifecycleAdapter {
        internal bool FailSuspend;
        internal bool FailResume;
        internal bool CancelClose;

        public override ApplicationLifecycleCapability Capability {
            get { return ApplicationLifecycleCapability.SupportedWithCustom; }
        }

        public override ApplicationLifecycleCallbackResult OnSuspending(
                ApplicationLifecycleContext context) {
            return FailSuspend ? ApplicationLifecycleCallbackResult.Failed :
                ApplicationLifecycleCallbackResult.Success;
        }

        public override ApplicationLifecycleCallbackResult OnResuming(
                ApplicationLifecycleContext context) {
            return FailResume ? ApplicationLifecycleCallbackResult.Failed :
                ApplicationLifecycleCallbackResult.Success;
        }

        public override ApplicationLifecycleCallbackResult OnCloseRequested(
                ApplicationLifecycleContext context, ApplicationCloseReason reason) {
            return CancelClose ? ApplicationLifecycleCallbackResult.Cancelled :
                ApplicationLifecycleCallbackResult.Success;
        }
    }

    /// <summary>
    /// GXM execution is instance-aware but has no safe pause/resume primitive
    /// in the current managed backend.  It therefore participates in all
    /// common lifecycle operations except cooperative suspension.
    /// </summary>
    internal sealed class GxmApplicationLifecycleAdapter : ApplicationLifecycleAdapter {
        public override ApplicationLifecycleCapability Capability {
            get { return ApplicationLifecycleCapability.Unsupported; }
        }

        public override ApplicationLifecycleCallbackResult OnSuspending(
                ApplicationLifecycleContext context) {
            return ApplicationLifecycleCallbackResult.Unsupported;
        }

        public override ApplicationLifecycleCallbackResult OnResuming(
                ApplicationLifecycleContext context) {
            return ApplicationLifecycleCallbackResult.Unsupported;
        }
    }
}
