using System;
using System.Runtime.InteropServices;

namespace GuideXos
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct GuideXosServiceRequestWire
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
    internal unsafe struct GuideXosSystemInformationWire
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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GuideXosIdentityWire
    {
        internal uint StructureVersion;
        internal uint Size;
        internal ulong StableApplicationId;
        internal ulong LifetimeToken;
        internal uint ApplicationGeneration;
        internal uint ProcessGeneration;
        internal uint Architecture;
        internal uint Reserved;
    }

    internal static unsafe class GuideXosInternalAbi
    {
        internal const uint AbiVersion = 1;
        private const ulong Success = 0;
        private const ulong InvalidOperation = unchecked((ulong)-38L);
        private const ulong InvalidPointer = unchecked((ulong)-14L);
        private const ulong InvalidRequest = unchecked((ulong)-22L);
        private const ulong InvalidContext = unchecked((ulong)-13L);
        private const uint SystemInformationService = 3;
        private const uint SystemInformationSnapshotOperation = 1;

        [DllImport("*", EntryPoint = "guidexos_pal_abi_version",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong InvokeAbiVersion();

        [DllImport("*", EntryPoint = "guidexos_pal_application_identity",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong InvokeApplicationIdentity(
            ulong response, ulong responseCapacity);

        [DllImport("*", EntryPoint = "guidexos_pal_service_request",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong InvokeServiceRequest(
            ulong request, ulong requestLength);

        [DllImport("*", EntryPoint = "guidexos_pal_monotonic_ticks",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong InvokeMonotonicTicks();

        [DllImport("*", EntryPoint = "guidexos_pal_monotonic_frequency",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong InvokeMonotonicFrequency();

        [DllImport("*", EntryPoint = "guidexos_pal_process_exit",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern void InvokeProcessExit(int code);

        [DllImport("*", EntryPoint = "guidexos_pal_fail_fast",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern void InvokeFailFast(uint reason, ulong context);

        internal static uint GetAbiVersion()
        {
            ulong value = InvokeAbiVersion();
            return value > uint.MaxValue ? 0U : (uint)value;
        }

        internal static GuideXosResult RequireCompatible()
        {
            return GetAbiVersion() == AbiVersion
                ? new GuideXosResult(GuideXosStatus.Success)
                : new GuideXosResult(GuideXosStatus.VersionMismatch);
        }

        internal static GuideXosResult TryGetIdentity(
            out GuideXosIdentityWire response)
        {
            GuideXosIdentityWire local = default;
            GuideXosIdentityWire* responsePointer = &local;
            ulong result = InvokeApplicationIdentity(
                (ulong)(nuint)responsePointer,
                (ulong)sizeof(GuideXosIdentityWire));
            response = local;
            return MapStatus(result);
        }

        internal static GuideXosResult TryGetSystemInformation(
            out GuideXosSystemInformationWire response)
        {
            GuideXosSystemInformationWire local = default;
            GuideXosServiceRequestWire request = default;
            request.StructureVersion = AbiVersion;
#if GUIDEXOS_PHASE28_TYPED_FAILURE
            // Diagnostic-only build variant. The public SDK surface does not
            // expose this switch; it proves typed ABI rejection safely.
            request.StructureVersion = 2;
#endif
            request.ServiceId = SystemInformationService;
            request.OperationId = SystemInformationSnapshotOperation;
            request.RequestLength = (uint)sizeof(GuideXosServiceRequestWire);
            request.ResponseBuffer = 0;
            request.ResponseCapacity = (uint)sizeof(GuideXosSystemInformationWire);
            GuideXosServiceRequestWire* requestPointer = &request;
            GuideXosSystemInformationWire* responsePointer = &local;
            request.ResponseBuffer = (ulong)(nuint)responsePointer;
            ulong result = InvokeServiceRequest(
                (ulong)(nuint)requestPointer,
                (ulong)sizeof(GuideXosServiceRequestWire));
            response = local;
            return MapStatus(result);
        }

        internal static ulong GetMonotonicTicks() => InvokeMonotonicTicks();
        internal static ulong GetMonotonicFrequency() => InvokeMonotonicFrequency();

        internal static void Exit(int code) => InvokeProcessExit(code);

        internal static void FailFast(int code) =>
            InvokeFailFast(unchecked((uint)code), 0);

        private static GuideXosResult MapStatus(ulong result)
        {
            if (result == Success) return new GuideXosResult(GuideXosStatus.Success);
            if (result == InvalidPointer) return new GuideXosResult(GuideXosStatus.InvalidBuffer);
            if (result == InvalidRequest) return new GuideXosResult(GuideXosStatus.InvalidArgument);
            if (result == InvalidContext) return new GuideXosResult(GuideXosStatus.InvalidState);
            if (result == InvalidOperation) return new GuideXosResult(GuideXosStatus.Unsupported);
            return new GuideXosResult(GuideXosStatus.TransportFailure);
        }
    }
}
