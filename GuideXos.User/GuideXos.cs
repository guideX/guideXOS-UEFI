using System;

namespace GuideXos
{
    public enum GuideXosStatus : uint
    {
        Success = 0,
        InvalidArgument = 1,
        InvalidBuffer = 2,
        Unsupported = 3,
        ResourceUnavailable = 4,
        InvalidState = 5,
        AccessDenied = 6,
        VersionMismatch = 7,
        TransportFailure = 8,
        ValidationFailed = 9,
    }

    public readonly struct GuideXosResult
    {
        private readonly GuideXosStatus _status;

        internal GuideXosResult(GuideXosStatus status)
        {
            _status = status;
        }

        public GuideXosStatus Status => _status;
        public bool Succeeded => _status == GuideXosStatus.Success;
        public bool Failed => !Succeeded;
    }

    public enum GuideXosArchitecture : uint
    {
        Unknown = 0,
        X64 = 1,
    }

    public enum GuideXosNotificationSeverity : uint
    {
        Information = 0,
        Error = 1,
    }

    public static class GuideXosRuntime
    {
        public static uint AbiVersion => GuideXosInternalAbi.GetAbiVersion();

        public static GuideXosResult TryGetAbiVersion(out uint version)
        {
            version = GuideXosInternalAbi.GetAbiVersion();
            return version == 0
                ? new GuideXosResult(GuideXosStatus.TransportFailure)
                : new GuideXosResult(GuideXosStatus.Success);
        }

        public static GuideXosResult CheckCompatibility(uint requiredVersion)
        {
            uint actual = GuideXosInternalAbi.GetAbiVersion();
            if (actual == 0)
                return new GuideXosResult(GuideXosStatus.TransportFailure);
            return actual == requiredVersion
                ? new GuideXosResult(GuideXosStatus.Success)
                : new GuideXosResult(GuideXosStatus.VersionMismatch);
        }
    }

    public readonly struct GuideXosApplicationIdentity
    {
        private readonly ulong _stableApplicationId;
        private readonly ulong _lifetimeToken;
        private readonly uint _applicationGeneration;
        private readonly uint _processGeneration;
        private readonly GuideXosArchitecture _architecture;

        internal GuideXosApplicationIdentity(ulong stableApplicationId,
                                              ulong lifetimeToken,
                                              uint applicationGeneration,
                                              uint processGeneration,
                                              GuideXosArchitecture architecture)
        {
            _stableApplicationId = stableApplicationId;
            _lifetimeToken = lifetimeToken;
            _applicationGeneration = applicationGeneration;
            _processGeneration = processGeneration;
            _architecture = architecture;
        }

        // This is a kernel-derived stable fingerprint, not a process handle or
        // an authorization capability. It is suitable for equality/grouping.
        public ulong StableApplicationId => _stableApplicationId;
        // This token identifies the current process lifetime but cannot be fed
        // back into any public SDK operation as authority.
        public ulong LifetimeToken => _lifetimeToken;
        public uint ApplicationGeneration => _applicationGeneration;
        public uint ProcessGeneration => _processGeneration;
        public GuideXosArchitecture Architecture => _architecture;
        public bool IsValid => _stableApplicationId != 0 &&
                                _lifetimeToken != 0 &&
                                _applicationGeneration != 0 &&
                                _processGeneration != 0 &&
                                _architecture != GuideXosArchitecture.Unknown;
    }

    public static unsafe class GuideXosApplication
    {
        public static GuideXosResult TryGetCurrent(
            out GuideXosApplicationIdentity identity)
        {
            identity = default;
            GuideXosResult compatible = GuideXosInternalAbi.RequireCompatible();
            if (compatible.Failed)
                return compatible;

            GuideXosIdentityWire wire;
            GuideXosResult result = GuideXosInternalAbi.TryGetIdentity(out wire);
            if (result.Failed)
                return result;
            if (wire.StructureVersion != GuideXosInternalAbi.AbiVersion ||
                wire.Size != (uint)sizeof(GuideXosIdentityWire) ||
                wire.Reserved != 0 || wire.StableApplicationId == 0 ||
                wire.LifetimeToken == 0 || wire.ApplicationGeneration == 0 ||
                wire.ProcessGeneration == 0 || wire.Architecture != 0x8664)
                return new GuideXosResult(GuideXosStatus.ValidationFailed);

            identity = new GuideXosApplicationIdentity(
                wire.StableApplicationId, wire.LifetimeToken,
                wire.ApplicationGeneration, wire.ProcessGeneration,
                GuideXosArchitecture.X64);
            return new GuideXosResult(GuideXosStatus.Success);
        }
    }

    public readonly struct GuideXosSystemInformation
    {
        internal GuideXosSystemInformation(ulong uptimeTicks,
                                           ulong totalMemoryBytes,
                                           ulong memoryInUseBytes,
                                           int threadCount,
                                           int cpuUsagePercent,
                                           GuideXosArchitecture architecture)
        {
            UptimeTicks = uptimeTicks;
            TotalMemoryBytes = totalMemoryBytes;
            MemoryInUseBytes = memoryInUseBytes;
            AvailableMemoryBytes = totalMemoryBytes - memoryInUseBytes;
            ThreadCount = threadCount;
            CpuUsagePercent = cpuUsagePercent;
            Architecture = architecture;
        }

        public ulong UptimeTicks { get; }
        public ulong TotalMemoryBytes { get; }
        public ulong MemoryInUseBytes { get; }
        public ulong AvailableMemoryBytes { get; }
        public int ThreadCount { get; }
        public int CpuUsagePercent { get; }
        public GuideXosArchitecture Architecture { get; }
    }

    public static unsafe class GuideXosSystem
    {
        public static GuideXosResult TryGetInformation(
            out GuideXosSystemInformation information)
        {
            information = default;
            GuideXosResult compatible = GuideXosInternalAbi.RequireCompatible();
            if (compatible.Failed)
                return compatible;

            GuideXosSystemInformationWire wire;
            GuideXosResult result = GuideXosInternalAbi.TryGetSystemInformation(
                out wire);
            if (result.Failed)
                return result;
            if (wire.StructureVersion != GuideXosInternalAbi.AbiVersion ||
                wire.Size != (uint)sizeof(GuideXosSystemInformationWire) ||
                wire.Reserved != 0 || wire.MemorySizeBytes == 0 ||
                wire.MemoryInUseBytes > wire.MemorySizeBytes ||
                wire.OsNameLength > 32 || wire.OsVersionLength > 32 ||
                wire.ArchitectureLength > 16 || wire.ThreadCount < 0 ||
                wire.CpuUsagePercent < 0 || wire.CpuUsagePercent > 100)
                return new GuideXosResult(GuideXosStatus.ValidationFailed);

            bool x64 = wire.ArchitectureLength == 6;
            if (x64)
            {
                byte* text = wire.Architecture;
                x64 = text[0] == (byte)'x' && text[1] == (byte)'8' &&
                      text[2] == (byte)'6' && text[3] == (byte)'_' &&
                      text[4] == (byte)'6' && text[5] == (byte)'4';
            }
            if (!x64)
                return new GuideXosResult(GuideXosStatus.ValidationFailed);

            information = new GuideXosSystemInformation(
                wire.UptimeTicks, wire.MemorySizeBytes,
                wire.MemoryInUseBytes, wire.ThreadCount,
                wire.CpuUsagePercent, GuideXosArchitecture.X64);
            return new GuideXosResult(GuideXosStatus.Success);
        }
    }

    public readonly struct GuideXosClipboardText
    {
        private readonly bool _hasValue;
        private readonly string _text;
        private readonly string _sourceApplicationId;
        private readonly ulong _generation;

        internal GuideXosClipboardText(bool hasValue, string text,
                                        string sourceApplicationId,
                                        ulong generation)
        {
            _hasValue = hasValue;
            _text = text ?? string.Empty;
            _sourceApplicationId = sourceApplicationId ?? string.Empty;
            _generation = generation;
        }

        public bool HasValue => _hasValue;
        public string Text => _text ?? string.Empty;
        public string SourceApplicationId => _sourceApplicationId ?? string.Empty;
        public ulong Generation => _generation;
    }

    public static unsafe class GuideXosClipboard
    {
        // Phase 11 defines the bound in UTF-16 code units, not encoded bytes.
        public const int MaxTextLength = 64 * 1024;
        public const int MaxApplicationIdLength = 96;

        public static GuideXosResult TrySetText(string text) =>
            GuideXosInternalAbi.TrySetClipboardText(text);

        public static GuideXosResult TryGetText(
            out GuideXosClipboardText clipboard)
        {
            clipboard = default;
            GuideXosResult compatible = GuideXosInternalAbi.RequireCompatible();
            if (compatible.Failed)
                return compatible;

            GuideXosClipboardResponseWire wire;
            GuideXosResult result = GuideXosInternalAbi.TryGetClipboardText(
                out wire);
            if (result.Failed)
                return result;
            if (wire.StructureVersion != GuideXosInternalAbi.AbiVersion ||
                wire.Size != (uint)sizeof(GuideXosClipboardResponseWire) ||
                (wire.HasValue != 0 && wire.HasValue != 1) ||
                wire.TextLength > MaxTextLength ||
                wire.SourceApplicationIdLength > MaxApplicationIdLength ||
                wire.Reserved != 0 ||
                (wire.HasValue == 0 &&
                 (wire.TextLength != 0 ||
                  wire.SourceApplicationIdLength != 0)))
                return new GuideXosResult(GuideXosStatus.ValidationFailed);

            string text = DecodeUtf16(wire.Text, wire.TextLength);
            string sourceApplicationId = DecodeUtf16(
                wire.SourceApplicationId, wire.SourceApplicationIdLength);
            clipboard = new GuideXosClipboardText(
                wire.HasValue != 0, text, sourceApplicationId,
                wire.Generation);
            return new GuideXosResult(GuideXosStatus.Success);
        }

        public static GuideXosResult TryClear() =>
            GuideXosInternalAbi.TryClearClipboard();

        private static string DecodeUtf16(byte* source, uint length)
        {
            if (source == null || length == 0)
                return string.Empty;
            char* text = stackalloc char[(int)length];
            for (int i = 0; i < (int)length; i++)
            {
                text[i] = (char)(source[i * 2] |
                    ((uint)source[(i * 2) + 1] << 8));
            }
            return new string(text, 0, (int)length);
        }
    }

    public static unsafe class GuideXosNotifications
    {
        public const int MaxTitleLength = 64;
        public const int MaxBodyLength = 256;

        public static GuideXosResult TryShow(
            string title, string body,
            GuideXosNotificationSeverity severity)
        {
            return GuideXosInternalAbi.TryShowNotification(
                title, body, severity);
        }
    }

    // Phase 9 Shell result values. Keep these numeric values aligned with
    // ApplicationServiceResultCode; callers receive no instance handle or
    // other kernel/App Model object.
    public enum GuideXosLaunchResultCode : uint
    {
        Success = 0,
        InvalidContext = 1,
        InvalidRequest = 2,
        NotFound = 3,
        ResourceUnavailable = 4,
        PermissionDenied = 5,
        Unsupported = 6,
        Conflict = 7,
        Cancelled = 8,
        InvalidState = 9,
        UnsupportedTarget = 10,
        BackendFailure = 11,
    }

    public readonly struct GuideXosLaunchResult
    {
        internal GuideXosLaunchResult(GuideXosLaunchResultCode code)
        {
            Code = code;
        }

        public GuideXosLaunchResultCode Code { get; }
        public bool Succeeded => Code == GuideXosLaunchResultCode.Success;
    }

    /// <summary>
    /// Typed wrapper over the existing Phase 9 Shell application-ID launch.
    /// The application ID is bounded in UTF-16 code units and copied before
    /// the synchronous service call. Launch authority stays in the App Model.
    /// </summary>
    public static class GuideXosShell
    {
        // This mirrors LaunchRequest.MaxTextLength (the existing Phase 9
        // stable-target bound) as a public wire contract value.
        public const int MaxApplicationIdLength = 1024;
        // Phase 9 uses this same bound for ApplicationShellOpenRequest and
        // LaunchRequest document values.
        public const int MaxDocumentLength = MaxApplicationIdLength;

        public static GuideXosResult TryLaunchApplication(
            string applicationId, out GuideXosLaunchResult launchResult)
        {
            launchResult = new GuideXosLaunchResult(
                GuideXosLaunchResultCode.InvalidRequest);
            return GuideXosInternalAbi.TryLaunchApplication(
                applicationId, out launchResult);
        }

        /// <summary>
        /// Requests that the existing Phase 9 Shell service open a document.
        /// The App Model resolves the association, target factory, and launch.
        /// </summary>
        public static GuideXosResult TryOpenDocument(
            string document, out GuideXosLaunchResult launchResult)
        {
            launchResult = new GuideXosLaunchResult(
                GuideXosLaunchResultCode.InvalidRequest);
            return GuideXosInternalAbi.TryOpenDocument(
                document, out launchResult);
        }
    }

    public static class GuideXosClock
    {
        public static ulong GetTimestamp() =>
            GuideXosInternalAbi.GetMonotonicTicks();

        public static ulong Frequency =>
            GuideXosInternalAbi.GetMonotonicFrequency();
    }

    public static class GuideXosProcess
    {
        public static void Exit(int code) =>
            GuideXosInternalAbi.Exit(code);

        public static void FailFast(int code) =>
            GuideXosInternalAbi.FailFast(code);
    }
}
