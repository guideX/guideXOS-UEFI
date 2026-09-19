namespace guideXOS.OS {
    public enum ApplicationFileDialogOutcome {
        Selected = 0,
        Cancelled,
        BackendFailure
    }

    /// <summary>
    /// Bounded result for an open or save file request.  A selected path is a
    /// serializable value; no FileInfo, dialog, callback, or Window crosses
    /// the application service boundary.
    /// </summary>
    public sealed class ApplicationFileDialogResult {
        public const int MaxSelectedPathLength = 1024;

        private ApplicationFileDialogResult(
                ApplicationFileDialogOutcome outcome, string selectedPath) {
            Outcome = outcome;
            SelectedPath = selectedPath ?? string.Empty;
            IsValid = IsValidOutcome(outcome) &&
                ApplicationServiceContext.IsBoundedText(
                    SelectedPath, MaxSelectedPathLength, outcome !=
                        ApplicationFileDialogOutcome.Selected);
        }

        public ApplicationFileDialogOutcome Outcome { get; private set; }
        public string SelectedPath { get; private set; }
        public bool IsValid { get; private set; }

        internal static ApplicationFileDialogResult From(
                ApplicationFileDialogOutcome outcome, string selectedPath) {
            return new ApplicationFileDialogResult(outcome, selectedPath);
        }

        private static bool IsValidOutcome(ApplicationFileDialogOutcome outcome) {
            return outcome == ApplicationFileDialogOutcome.Selected ||
                   outcome == ApplicationFileDialogOutcome.Cancelled ||
                   outcome == ApplicationFileDialogOutcome.BackendFailure;
        }
    }

    public abstract class ApplicationOpenFileService {
        public abstract ApplicationServiceResult<ApplicationServiceRequestHandle>
            Begin(ApplicationServiceContext context, OpenFileRequest request);

        public abstract ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationFileDialogResult>>
            Observe(ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle);

        public abstract ApplicationServiceResult Cancel(
            ApplicationServiceContext context,
            ApplicationServiceRequestHandle handle);
    }

    public abstract class ApplicationSaveFileService {
        public abstract ApplicationServiceResult<ApplicationServiceRequestHandle>
            Begin(ApplicationServiceContext context, SaveFileRequest request);

        public abstract ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationFileDialogResult>>
            Observe(ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle);

        public abstract ApplicationServiceResult Cancel(
            ApplicationServiceContext context,
            ApplicationServiceRequestHandle handle);
    }
}
