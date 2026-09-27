using GuideXos;

namespace GuideXos.Phase32ManagedOpenDocumentProof;

internal static class Program
{
    private const string DocumentPath = "Scripts/notepad.gxm.txt";

    internal static int Main()
    {
#if GUIDEXOS_PHASE32_FAILFAST
        GuideXosResult failFastTransport = GuideXosShell.TryOpenDocument(
            DocumentPath, out GuideXosLaunchResult failFastLaunch);
        if (failFastTransport.Failed || !failFastLaunch.Succeeded)
            return 43;
        GuideXosProcess.FailFast(0x32);
        return 44;
#elif GUIDEXOS_PHASE32_STALE_OWNER
        GuideXosResult staleTransport = GuideXosShell.TryOpenDocument(
            DocumentPath, out GuideXosLaunchResult staleLaunch);
        return staleTransport.Succeeded &&
               staleLaunch.Code == GuideXosLaunchResultCode.InvalidContext
            ? 32
            : 33;
#else
        GuideXosResult unsupportedTransport = GuideXosShell.TryOpenDocument(
            "Scripts/unsupported.phase32", out GuideXosLaunchResult unsupported);
        if (!unsupportedTransport.Succeeded ||
            unsupported.Code != GuideXosLaunchResultCode.UnsupportedTarget)
            return 44;

        GuideXosResult missingTransport = GuideXosShell.TryOpenDocument(
            "Scripts/guideXOS-phase32-missing.txt",
            out GuideXosLaunchResult missing);
        if (!missingTransport.Succeeded ||
            missing.Code != GuideXosLaunchResultCode.ResourceUnavailable)
            return 45;

        string tooLong = new string('x',
            GuideXosShell.MaxDocumentLength + 1);
        GuideXosResult oversizeTransport = GuideXosShell.TryOpenDocument(
            tooLong, out GuideXosLaunchResult oversize);
        if (oversizeTransport.Status != GuideXosStatus.InvalidArgument ||
            oversize.Code != GuideXosLaunchResultCode.InvalidRequest)
            return 46;

        GuideXosResult emptyTransport = GuideXosShell.TryOpenDocument(
            string.Empty, out GuideXosLaunchResult empty);
        if (emptyTransport.Status != GuideXosStatus.InvalidArgument ||
            empty.Code != GuideXosLaunchResultCode.InvalidRequest)
            return 47;

        // Opening may change this requester's lifecycle to Inactive. Keep the
        // successful launch last, then return without issuing another request.
        GuideXosResult openTransport = GuideXosShell.TryOpenDocument(
            DocumentPath, out GuideXosLaunchResult openLaunch);
        if (openTransport.Failed || !openLaunch.Succeeded)
            return 43;

        return 32;
#endif
    }
}
