using GuideXos;

namespace GuideXos.Phase33ManagedShellActionProof;

internal static class Program
{
    internal static int Main()
    {
#if GUIDEXOS_PHASE33_FAILFAST
        GuideXosResult failFastTransport = GuideXosShell.TryOpenShellObject(
            GuideXosShellObject.ComputerFiles,
            out GuideXosLaunchResult failFastResult);
        if (failFastTransport.Failed || !failFastResult.Succeeded)
            return 43;
        GuideXosProcess.FailFast(0x33);
        return 44;
#elif GUIDEXOS_PHASE33_STALE_OWNER
        GuideXosResult staleTransport = GuideXosShell.TryOpenShellObject(
            GuideXosShellObject.ComputerFiles,
            out GuideXosLaunchResult staleResult);
        return staleTransport.Succeeded &&
               staleResult.Code == GuideXosLaunchResultCode.InvalidContext
            ? 33
            : 34;
#elif GUIDEXOS_PHASE33_INVALID_ACTION
        GuideXosResult invalidTransport = GuideXosShell.TryOpenShellObject(
            GuideXosShellObject.ComputerFiles,
            out GuideXosLaunchResult invalidResult);
        return invalidTransport.Succeeded &&
               invalidResult.Code == GuideXosLaunchResultCode.UnsupportedTarget
            ? 33
            : 35;
#elif GUIDEXOS_PHASE33_MALFORMED
        GuideXosResult malformedTransport = GuideXosShell.TryOpenShellObject(
            GuideXosShellObject.ComputerFiles,
            out GuideXosLaunchResult malformedResult);
        return malformedTransport.Status == GuideXosStatus.InvalidArgument
            ? 33
            : 36;
#else
        GuideXosResult invalidEnumTransport = GuideXosShell.TryOpenShellObject(
            (GuideXosShellObject)99, out GuideXosLaunchResult invalidEnum);
        if (invalidEnumTransport.Status != GuideXosStatus.InvalidArgument ||
            invalidEnum.Code != GuideXosLaunchResultCode.InvalidRequest)
            return 37;

        GuideXosResult openTransport = GuideXosShell.TryOpenShellObject(
            GuideXosShellObject.ComputerFiles,
            out GuideXosLaunchResult openResult);
        if (openTransport.Failed || !openResult.Succeeded)
            return 38;
        return 33;
#endif
    }
}
