using GuideXos;

namespace GuideXos.Phase34ManagedResourceReadProof;

internal static class Program
{
    private const string ResourceName = "diagnostic.fixture";
    private const string ExpectedContent =
        "guideXOS Phase 10 bounded application resource fixture";

    internal static int Main()
    {
#if GUIDEXOS_PHASE34_FAILFAST
        if (!ReadAndValidate(out _))
            return 43;
        GuideXosProcess.FailFast(0x34);
        return 44;
#elif GUIDEXOS_PHASE34_STALE_OWNER
        GuideXosResult stale = GuideXosResources.TryReadBytes(ResourceName,
            out byte[] data, out _);
        return stale.Status == GuideXosStatus.InvalidState && data == null
            ? 34
            : 45;
#elif GUIDEXOS_PHASE34_CROSS_SCOPE
        GuideXosResult crossScope = GuideXosResources.TryReadBytes(ResourceName,
            out byte[] data, out GuideXosResourceResultCode code);
        return crossScope.Succeeded && data == null &&
               code == GuideXosResourceResultCode.NotFound
            ? 34
            : 46;
#elif GUIDEXOS_PHASE34_MALFORMED
        // This diagnostic-only SDK build corrupts the wire operation after
        // public key validation. The kernel must reject the raw request
        // before it enters the Phase 10 resource backend.
        GuideXosResult malformed = GuideXosResources.TryReadBytes(
            ResourceName, out byte[] malformedData,
            out GuideXosResourceResultCode malformedCode);
        return malformed.Status == GuideXosStatus.InvalidArgument &&
               malformedData == null && malformedCode ==
                   GuideXosResourceResultCode.InvalidRequest
            ? 34
            : 47;
#else
        if (!CheckLocalRejections() || !CheckMissingResource())
            return 40;
        if (!ReadAndValidate(out byte[] first))
            return 41;
        if (!ReadAndValidate(out byte[] repeated) ||
            !Equal(first, repeated))
            return 42;
        return 34;
#endif
    }

    private static bool CheckLocalRejections()
    {
        GuideXosResult empty = GuideXosResources.TryReadBytes(string.Empty,
            out byte[] emptyData, out GuideXosResourceResultCode emptyCode);
        if (empty.Status != GuideXosStatus.InvalidArgument ||
            emptyData != null || emptyCode !=
                GuideXosResourceResultCode.InvalidRequest)
            return false;

        string oversizedName = new string('x',
            GuideXosResources.MaxResourceNameLength + 1);
        GuideXosResult oversized = GuideXosResources.TryReadBytes(
            oversizedName, out byte[] oversizedData, out _);
        if (oversized.Status != GuideXosStatus.InvalidArgument ||
            oversizedData != null)
            return false;

        GuideXosResult pathLike = GuideXosResources.TryReadBytes(
            "../outside", out byte[] pathData, out _);
        return pathLike.Status == GuideXosStatus.InvalidArgument &&
               pathData == null;
    }

    private static bool CheckMissingResource()
    {
        GuideXosResult result = GuideXosResources.TryReadBytes(
            "missing.fixture", out byte[] data,
            out GuideXosResourceResultCode code);
        return result.Succeeded && data == null &&
               code == GuideXosResourceResultCode.NotFound;
    }

    private static bool ReadAndValidate(out byte[] data)
    {
        GuideXosResult result = GuideXosResources.TryReadBytes(ResourceName,
            out data, out GuideXosResourceResultCode code);
        if (!result.Succeeded || code != GuideXosResourceResultCode.Success ||
            data == null || data.Length != ExpectedContent.Length)
            return false;

        for (int i = 0; i < ExpectedContent.Length; i++)
        {
            if (data[i] != (byte)ExpectedContent[i])
                return false;
        }
        return true;
    }

    private static bool Equal(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }
        return true;
    }
}
