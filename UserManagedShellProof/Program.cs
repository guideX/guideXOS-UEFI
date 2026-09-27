using GuideXos;

namespace GuideXos.Phase31ManagedShellProof;

internal static class Program
{
    private const string CalculatorId = "gxos.builtin.calculator";

    internal static int Main()
    {
#if GUIDEXOS_PHASE31_FAILFAST
        GuideXosResult transport = GuideXosShell.TryLaunchApplication(
            CalculatorId, out GuideXosLaunchResult launch);
        if (transport.Failed || !launch.Succeeded)
            return 43;
        GuideXosProcess.FailFast(0x31);
        return 44;
#elif GUIDEXOS_PHASE31_INVALID_TARGET
        GuideXosResult invalid = GuideXosShell.TryLaunchApplication(
            "gxos.phase31.application.does.not.exist",
            out GuideXosLaunchResult missing);
        return invalid.Succeeded &&
               missing.Code == GuideXosLaunchResultCode.NotFound ? 41 : 43;
#elif GUIDEXOS_PHASE31_OVERSIZE
        string tooLong = new string('x',
            GuideXosShell.MaxApplicationIdLength + 1);
        GuideXosResult oversize = GuideXosShell.TryLaunchApplication(
            tooLong, out GuideXosLaunchResult rejected);
        return oversize.Status == GuideXosStatus.InvalidArgument &&
               rejected.Code == GuideXosLaunchResultCode.InvalidRequest ? 42 : 43;
#elif GUIDEXOS_PHASE31_STALE_OWNER
        GuideXosResult stale = GuideXosShell.TryLaunchApplication(
            CalculatorId, out GuideXosLaunchResult rejected);
        return stale.Succeeded &&
               rejected.Code == GuideXosLaunchResultCode.InvalidContext ? 32 : 33;
#else
        GuideXosResult result = GuideXosShell.TryLaunchApplication(
            CalculatorId, out GuideXosLaunchResult launch);
        return result.Succeeded && launch.Succeeded ? 31 : 32;
#endif
    }
}
