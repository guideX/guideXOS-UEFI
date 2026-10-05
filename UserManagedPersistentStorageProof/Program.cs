using GuideXos;

namespace GuideXos.Phase35ManagedPersistentReadProof;

internal static class Program
{
    internal static int Main()
    {
#if GUIDEXOS_PHASE35_NO_READ
        return 35;
#elif GUIDEXOS_PHASE35_ONE_READ
        return ReadFixture() ? 35 : 41;
#elif GUIDEXOS_PHASE35_TWO_READ
        if (!ReadFixture(out byte[] first)) return 41;
        first[0] ^= 0xFF;
        bool passed = ReadFixture(out byte[] second) && HasFixtureBytes(second);
        second = null;
        first = null;
        System.GC.Collect();
        return passed ? 35 : 42;
#elif GUIDEXOS_PHASE35_FAILFAST
        return ReadFixture() ? FailFast() : 43;
#elif GUIDEXOS_PHASE35_STALE_OWNER
        GuideXosResult stale = GuideXosStorage.TryReadBytes("state.bin",
            out byte[] staleData, out GuideXosStorageResultCode staleCode);
        return stale.Status == GuideXosStatus.InvalidState &&
               staleCode == GuideXosStorageResultCode.InvalidContext &&
               IsEmpty(staleData) ? 35 : 45;
#elif GUIDEXOS_PHASE35_CROSS_SCOPE
        GuideXosResult crossScope = GuideXosStorage.TryReadBytes("state.bin",
            out byte[] crossData, out GuideXosStorageResultCode crossCode);
        return crossScope.Succeeded &&
               crossCode == GuideXosStorageResultCode.NotFound &&
               IsEmpty(crossData) ? 35 : 46;
#elif GUIDEXOS_PHASE35_MALFORMED
        GuideXosResult malformed = GuideXosStorage.TryReadBytes("state.bin",
            out byte[] malformedData,
            out GuideXosStorageResultCode malformedCode);
        return malformed.Status == GuideXosStatus.InvalidArgument &&
               malformedCode == GuideXosStorageResultCode.InvalidRequest &&
               IsEmpty(malformedData) ? 35 : 47;
#else
        int invalidPathResult = CheckInvalidPaths();
        if (invalidPathResult != 0) return invalidPathResult;
        int missingResult = CheckMissing();
        if (missingResult != 0) return missingResult;
        if (!ReadFixture(out byte[] first))
        {
            first = null;
            System.GC.Collect();
            return 41;
        }
        first[0] ^= 0xFF;
        bool repeatPassed = ReadFixture(out byte[] second) &&
            HasFixtureBytes(second);
        second = null;
        first = null;
        System.GC.Collect();
        if (!repeatPassed) return 42;
        if (!CheckEmpty()) return 43;
        System.GC.Collect();
        return 35;
#endif
    }

#if !GUIDEXOS_PHASE35_STALE_OWNER && !GUIDEXOS_PHASE35_CROSS_SCOPE && !GUIDEXOS_PHASE35_MALFORMED
    private static bool ReadFixture()
    {
        bool passed = ReadFixture(out byte[] bytes) && HasFixtureBytes(bytes);
        bytes = null;
        System.GC.Collect();
        return passed;
    }

    private static bool ReadFixture(out byte[] bytes)
    {
        GuideXosResult result = GuideXosStorage.TryReadBytes("state.bin",
            out bytes, out GuideXosStorageResultCode code);
        return result.Succeeded &&
               code == GuideXosStorageResultCode.Success &&
               HasFixtureBytes(bytes);
    }
#endif

#if GUIDEXOS_PHASE35_FAILFAST
    private static int FailFast()
    {
        GuideXosProcess.FailFast(0x35);
        return 44;
    }
#endif

#if !GUIDEXOS_PHASE35_FAILFAST && !GUIDEXOS_PHASE35_STALE_OWNER && !GUIDEXOS_PHASE35_CROSS_SCOPE && !GUIDEXOS_PHASE35_MALFORMED
    private static int CheckInvalidPaths()
    {
        if (!LocallyRejected(string.Empty)) return 48;
        if (!LocallyRejected("/state.bin")) return 49;
        if (!LocallyRejected("../state.bin")) return 50;
        if (!LocallyRejected("bad?.bin")) return 51;
        string longPath = new string('x', 193);
        bool longRejected = LocallyRejected(longPath);
        longPath = null;
        string longSegment = new string('s', 65);
        bool segmentRejected = LocallyRejected(longSegment);
        longSegment = null;
        System.GC.Collect();
        if (!longRejected) return 52;
        if (!segmentRejected) return 53;
        return 0;
    }

    private static bool LocallyRejected(string path)
    {
        GuideXosResult result = GuideXosStorage.TryReadBytes(path,
            out byte[] bytes, out GuideXosStorageResultCode code);
        return result.Status == GuideXosStatus.InvalidArgument &&
               code == GuideXosStorageResultCode.InvalidRequest &&
               IsEmpty(bytes);
    }

    private static int CheckMissing()
    {
        GuideXosResult result = GuideXosStorage.TryReadBytes(
            "missing.phase35", out byte[] bytes,
            out GuideXosStorageResultCode code);
        if (!result.Succeeded) return 50 + (int)result.Status;
        if (code != GuideXosStorageResultCode.NotFound)
            return 60 + (int)code;
        return IsEmpty(bytes) ? 0 : 80;
    }

    private static bool CheckEmpty()
    {
        GuideXosResult result = GuideXosStorage.TryReadBytes("empty.bin",
            out byte[] bytes, out GuideXosStorageResultCode code);
        return result.Succeeded &&
               code == GuideXosStorageResultCode.Success &&
               IsEmpty(bytes);
    }

#endif

    private static bool HasFixtureBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 32) return false;
        for (int i = 0; i < bytes.Length; i++)
            if (bytes[i] != (byte)(0x35 + i)) return false;
        return true;
    }

    private static bool IsEmpty(byte[] bytes) => bytes != null && bytes.Length == 0;
}
