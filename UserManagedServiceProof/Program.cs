using System.Runtime.InteropServices;

namespace guideXOS.UserManagedServiceProof;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct GuideXosServiceRequest
{
    internal uint StructureVersion;
    internal uint ServiceId;
    internal uint OperationId;
    internal uint RequestLength;
    internal ulong ResponseBuffer;
    internal uint ResponseCapacity;
    internal uint Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct GuideXosSystemInformationResponse
{
    internal uint StructureVersion;
    internal uint Size;
    internal ulong UptimeTicks;
    internal ulong MemorySizeBytes;
    internal ulong MemoryInUseBytes;
    internal int ThreadCount;
    internal int CpuUsagePercent;
    internal ushort OsNameLength;
    internal ushort OsVersionLength;
    internal ushort ArchitectureLength;
    internal ushort Reserved;
    internal fixed byte OsName[32];
    internal fixed byte OsVersion[32];
    internal fixed byte Architecture[16];
}

internal readonly struct GuideXosSystemInformationSnapshot
{
    internal readonly ulong UptimeTicks;
    internal readonly ulong TotalMemoryBytes;
    internal readonly ulong AvailableMemoryBytes;
    internal readonly int ThreadCount;
    internal readonly int CpuUsagePercent;
    internal readonly bool IsX64Platform;

    internal GuideXosSystemInformationSnapshot(
        ulong uptimeTicks, ulong totalMemoryBytes, ulong availableMemoryBytes,
        int threadCount, int cpuUsagePercent, bool isX64Platform)
    {
        UptimeTicks = uptimeTicks;
        TotalMemoryBytes = totalMemoryBytes;
        AvailableMemoryBytes = availableMemoryBytes;
        ThreadCount = threadCount;
        CpuUsagePercent = cpuUsagePercent;
        IsX64Platform = isX64Platform;
    }
}

internal static unsafe class GuideXosSystemInformation
{
    private const uint AbiVersion = 1;
    private const uint SystemInformationService = 3;
    private const uint SnapshotOperation = 1;
    private const uint ResponseSize = 128;

    // The native helper is a target-native import, not a Windows or kernel
    // import. It enters the existing operation-5 Ring 3 ABI exactly once.
    [DllImport("*", EntryPoint = "guidexos_pal_service_request",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong InvokeServiceRequest(ulong request,
                                                       ulong requestLength);

    internal static bool TryGetSnapshot(
        out GuideXosSystemInformationSnapshot snapshot)
    {
        snapshot = default;
        GuideXosServiceRequest request = default;
        GuideXosSystemInformationResponse response = default;

#if PHASE27_FAILURE
        request.StructureVersion = 2;
#else
        request.StructureVersion = AbiVersion;
#endif
        request.ServiceId = SystemInformationService;
        request.OperationId = SnapshotOperation;
        request.RequestLength = (uint)sizeof(GuideXosServiceRequest);
        request.ResponseBuffer = (ulong)(nuint)(&response);
        request.ResponseCapacity = (uint)sizeof(GuideXosSystemInformationResponse);

        ulong result = InvokeServiceRequest(
            (ulong)(nuint)(&request), (ulong)sizeof(GuideXosServiceRequest));
        if (result != 0)
            return false;

        if (response.StructureVersion != AbiVersion ||
            response.Size != ResponseSize ||
            response.Reserved != 0 ||
            response.MemorySizeBytes == 0 ||
            response.MemoryInUseBytes > response.MemorySizeBytes ||
            response.OsNameLength > 32 ||
            response.OsVersionLength > 32 ||
            response.ArchitectureLength > 16)
            return false;

        bool x64 = response.ArchitectureLength == 6;
        if (x64)
        {
            byte* architecture = response.Architecture;
            x64 = architecture[0] == (byte)'x' &&
                  architecture[1] == (byte)'8' &&
                  architecture[2] == (byte)'6' &&
                  architecture[3] == (byte)'_' &&
                  architecture[4] == (byte)'6' &&
                  architecture[5] == (byte)'4';
        }

        snapshot = new GuideXosSystemInformationSnapshot(
            response.UptimeTicks, response.MemorySizeBytes,
            response.MemorySizeBytes - response.MemoryInUseBytes,
            response.ThreadCount, response.CpuUsagePercent, x64);
        return true;
    }

    internal static bool Validate(
        GuideXosSystemInformationSnapshot snapshot)
    {
        return snapshot.TotalMemoryBytes != 0 &&
               snapshot.AvailableMemoryBytes <= snapshot.TotalMemoryBytes &&
               snapshot.ThreadCount >= 0 &&
               snapshot.CpuUsagePercent >= 0 &&
               snapshot.CpuUsagePercent <= 100 &&
               snapshot.IsX64Platform;
    }
}

internal static class Program
{
    internal static int Main()
    {
        if (!GuideXosSystemInformation.TryGetSnapshot(out var info))
            return 21;
        if (!GuideXosSystemInformation.Validate(info))
            return 22;
        return 27;
    }
}
