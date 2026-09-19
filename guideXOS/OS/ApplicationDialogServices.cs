namespace guideXOS.OS {
    /// <summary>
    /// Application-facing asynchronous dialog service.  Implementations
    /// return only bounded request handles and dialog results; GUI windows
    /// remain backend details.
    /// </summary>
    public abstract class ApplicationDialogService {
        public abstract ApplicationServiceResult<ApplicationServiceRequestHandle>
            Begin(ApplicationServiceContext context,
                  ApplicationDialogRequest request);

        public abstract ApplicationServiceResult<
                ApplicationServiceRequestStatus<ApplicationDialogResult>> Observe(
                    ApplicationServiceContext context,
                    ApplicationServiceRequestHandle handle);

        public abstract ApplicationServiceResult Cancel(
                ApplicationServiceContext context,
                ApplicationServiceRequestHandle handle);
    }
}
