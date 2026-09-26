using GuideXos;

namespace GuideXos.Phase28ManagedSdkProof;

internal static class Program
{
    internal static int Main()
    {
#if GUIDEXOS_PHASE28_EXIT
        GuideXosProcess.Exit(73);
        return 99;
#elif GUIDEXOS_PHASE28_FAILFAST
        GuideXosProcess.FailFast(0x28);
        return 99;
#elif GUIDEXOS_PHASE28_TYPED_FAILURE
        GuideXosResult identityResult = GuideXosApplication.TryGetCurrent(
            out GuideXosApplicationIdentity application);
        if (!identityResult.Succeeded || !application.IsValid)
            return 24;
        GuideXosResult failure = GuideXosRuntime.CheckCompatibility(2);
        return failure.Status == GuideXosStatus.VersionMismatch ? 23 : 24;
#else
        GuideXosResult abi = GuideXosRuntime.TryGetAbiVersion(out uint version);
        if (!abi.Succeeded || version != 1)
            return 20;

        GuideXosResult identityResult = GuideXosApplication.TryGetCurrent(
            out GuideXosApplicationIdentity application);
        if (!identityResult.Succeeded || !application.IsValid ||
            application.Architecture != GuideXosArchitecture.X64)
            return 21;

        GuideXosResult systemResult = GuideXosSystem.TryGetInformation(
            out GuideXosSystemInformation system);
        if (!systemResult.Succeeded || system.TotalMemoryBytes == 0 ||
            system.MemoryInUseBytes > system.TotalMemoryBytes ||
            system.Architecture != GuideXosArchitecture.X64)
            return 22;

        ulong first = GuideXosClock.GetTimestamp();
        ulong frequency = GuideXosClock.Frequency;
        ulong second = GuideXosClock.GetTimestamp();
        if (frequency == 0 || second < first)
            return 26;

        return 28;
#endif
    }
}
