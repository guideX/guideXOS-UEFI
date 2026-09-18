using guideXOS.GUI;

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
}
