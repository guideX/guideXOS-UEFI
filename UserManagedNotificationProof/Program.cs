using GuideXos;

namespace GuideXos.Phase29ManagedNotificationProof;

internal static class Program
{
    internal static int Main()
    {
#if GUIDEXOS_PHASE29_TITLE_FAILURE
        GuideXosResult titleFailure = GuideXosNotifications.TryShow(
            "01234567890123456789012345678901234567890123456789012345678901234",
            "bounded body", GuideXosNotificationSeverity.Information);
        return titleFailure.Status == GuideXosStatus.InvalidArgument ? 31 : 33;
#elif GUIDEXOS_PHASE29_INVALID_TYPE
        GuideXosResult invalidType = GuideXosNotifications.TryShow(
            "Phase 29", "Invalid raw notification type",
            GuideXosNotificationSeverity.Information);
        return invalidType.Status == GuideXosStatus.InvalidArgument ? 35 : 36;
#elif GUIDEXOS_PHASE29_BODY_FAILURE
        GuideXosResult bodyFailure = GuideXosNotifications.TryShow(
            "Phase 29", "01234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567012345678",
            GuideXosNotificationSeverity.Information);
        return bodyFailure.Status == GuideXosStatus.InvalidArgument ? 32 : 34;
#elif GUIDEXOS_PHASE29_FAILFAST
        GuideXosResult beforeFailFast = GuideXosNotifications.TryShow(
            "Phase 29", "Managed Ring 3 notification before FailFast",
            GuideXosNotificationSeverity.Information);
        if (!beforeFailFast.Succeeded)
            return 35;
        GuideXosProcess.FailFast(0x29);
        return 36;
#else
        GuideXosResult result = GuideXosNotifications.TryShow(
            "Phase 29", "Managed Ring 3 notification",
            GuideXosNotificationSeverity.Information);
        return result.Succeeded ? 29 : 30;
#endif
    }
}
