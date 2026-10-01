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
    internal unsafe struct GuideXosNotificationRequestWire
    {
        internal uint StructureVersion;
        internal uint ServiceId;
        internal uint OperationId;
        internal uint RequestLength;
        internal ushort TitleLength;
        internal ushort BodyLength;
        internal uint Severity;
        internal uint Reserved;
        internal fixed byte Title[GuideXosNotifications.MaxTitleLength * 2];
        internal fixed byte Body[GuideXosNotifications.MaxBodyLength * 2];
    }

    // Phase 11 SetText wire record.  The complete bounded value is copied
    // into this fixed UTF-16LE buffer; no pointer is authoritative.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct GuideXosClipboardSetRequestWire
    {
        internal uint StructureVersion;
        internal uint ServiceId;
        internal uint OperationId;
        internal uint RequestLength;
        internal uint TextLength;
        internal uint Reserved;
        internal fixed byte Text[GuideXosClipboard.MaxTextLength * 2];
    }

    // Get uses the existing service-request envelope.  The response is a
    // fixed-width copied snapshot, including only value metadata and text.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct GuideXosClipboardResponseWire
    {
        internal uint StructureVersion;
        internal uint Size;
        internal uint HasValue;
        internal uint TextLength;
        internal uint SourceApplicationIdLength;
        internal uint Reserved;
        internal ulong Generation;
        internal fixed byte Text[GuideXosClipboard.MaxTextLength * 2];
        internal fixed byte SourceApplicationId[
            GuideXosClipboard.MaxApplicationIdLength * 2];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GuideXosClipboardClearRequestWire
    {
        internal uint StructureVersion;
        internal uint ServiceId;
        internal uint OperationId;
        internal uint RequestLength;
        internal uint Reserved;
    }

    // Phase 9 Shell launch request. UTF-16LE target text is copied inline and
    // the record contains no caller identity or App Model object authority.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct GuideXosShellLaunchRequestWire
    {
        internal uint StructureVersion;
        internal uint ServiceId;
        internal uint OperationId;
        internal uint RequestLength;
        internal uint TargetLength;
        internal uint ResponseCapacity;
        internal ulong ResponseBuffer;
        internal uint Reserved;
        internal fixed byte Target[GuideXosShell.MaxApplicationIdLength * 2];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GuideXosShellLaunchResponseWire
    {
        internal uint StructureVersion;
        internal uint Size;
        internal uint ResultCode;
        internal uint Reserved;
    }

    // Phase 10 uses a bounded ASCII key. Metadata and bytes are returned in
    // separate caller buffers so the SDK can allocate the exact known length.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct GuideXosResourceRequestWire
    {
        internal uint StructureVersion;
        internal uint ServiceId;
        internal uint OperationId;
        internal uint RequestLength;
        internal uint ResourceNameLength;
        internal uint DataCapacity;
        internal uint ResponseCapacity;
        internal uint Reserved;
        internal ulong ResponseBuffer;
        internal ulong DataBuffer;
        internal ulong ExpectedResourceLength;
        internal fixed byte ResourceName[GuideXosResources.MaxResourceNameLength];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GuideXosResourceResponseWire
    {
        internal uint StructureVersion;
        internal uint Size;
        internal uint ResultCode;
        internal uint Flags;
        internal ulong ResourceLength;
        internal ulong Offset;
        internal uint BytesRead;
        internal uint EndOfResource;
        internal uint Reserved;
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
        private const uint NotificationsService = 1;
        private const uint NotificationsPublishOperation = 1;
        private const uint ClipboardService = 10;
        private const uint ClipboardSetTextOperation = 1;
        private const uint ClipboardGetTextOperation = 2;
        private const uint ClipboardClearOperation = 3;
        private const uint ShellService = 7;
        private const uint ShellLaunchApplicationOperation = 1;
        private const uint ShellOpenDocumentOperation = 2;
        private const uint ShellOpenObjectOperation = 3;
        private const uint ResourcesService = 8;
        private const uint ResourceMetadataOperation = 1;
        private const uint ResourceReadOperation = 2;

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

        internal static GuideXosResult TryShowNotification(
            string title, string body,
            GuideXosNotificationSeverity severity)
        {
            body = body ?? string.Empty;
            if (title == null || title.Length == 0 ||
                title.Length > GuideXosNotifications.MaxTitleLength ||
                body.Length > GuideXosNotifications.MaxBodyLength ||
                (severity != GuideXosNotificationSeverity.Information &&
                 severity != GuideXosNotificationSeverity.Error))
                return new GuideXosResult(GuideXosStatus.InvalidArgument);

            GuideXosNotificationRequestWire request = default;
            request.StructureVersion = AbiVersion;
            request.ServiceId = NotificationsService;
            request.OperationId = NotificationsPublishOperation;
            request.RequestLength = (uint)sizeof(GuideXosNotificationRequestWire);
            request.TitleLength = (ushort)title.Length;
            request.BodyLength = (ushort)body.Length;
            request.Severity = (uint)severity;
#if GUIDEXOS_PHASE29_INVALID_TYPE
            // Internal diagnostic only: exercise the kernel's independent
            // enum validation without exposing an invalid public value.
            request.Severity = 99;
#endif
            request.Reserved = 0;

            byte* titleBytes = request.Title;
            for (int i = 0; i < title.Length; i++)
            {
                char value = title[i];
                titleBytes[(i * 2) + 0] = (byte)value;
                titleBytes[(i * 2) + 1] = (byte)(value >> 8);
            }

            byte* bodyBytes = request.Body;
            for (int i = 0; i < body.Length; i++)
            {
                char value = body[i];
                bodyBytes[(i * 2) + 0] = (byte)value;
                bodyBytes[(i * 2) + 1] = (byte)(value >> 8);
            }

            GuideXosNotificationRequestWire* requestPointer = &request;
            ulong result = InvokeServiceRequest(
                (ulong)(nuint)requestPointer,
                (ulong)sizeof(GuideXosNotificationRequestWire));
            return MapStatus(result);
        }

        internal static GuideXosResult TrySetClipboardText(string text)
        {
            if (text == null || text.Length > GuideXosClipboard.MaxTextLength)
                return new GuideXosResult(GuideXosStatus.InvalidArgument);

            GuideXosClipboardSetRequestWire request = default;
            request.StructureVersion = AbiVersion;
            request.ServiceId = ClipboardService;
            request.OperationId = ClipboardSetTextOperation;
            request.RequestLength = (uint)sizeof(
                GuideXosClipboardSetRequestWire);
            request.TextLength = (uint)text.Length;
            request.Reserved = 0;
#if GUIDEXOS_PHASE30_MALFORMED_LENGTH
            // Internal diagnostic only: the public API has already accepted a
            // bounded value, then deliberately sends an inconsistent length.
            request.TextLength = (uint)GuideXosClipboard.MaxTextLength + 1U;
#endif
            byte* textBytes = request.Text;
            for (int i = 0; i < text.Length; i++)
            {
                char value = text[i];
                textBytes[(i * 2) + 0] = (byte)value;
                textBytes[(i * 2) + 1] = (byte)(value >> 8);
            }

            GuideXosClipboardSetRequestWire* requestPointer = &request;
            ulong result = InvokeServiceRequest(
                (ulong)(nuint)requestPointer,
                (ulong)sizeof(GuideXosClipboardSetRequestWire));
            return MapStatus(result);
        }

        internal static GuideXosResult TryGetClipboardText(
            out GuideXosClipboardResponseWire response)
        {
            GuideXosClipboardResponseWire local = default;
            GuideXosServiceRequestWire request = default;
            request.StructureVersion = AbiVersion;
            request.ServiceId = ClipboardService;
            request.OperationId = ClipboardGetTextOperation;
            request.RequestLength = (uint)sizeof(GuideXosServiceRequestWire);
            request.ResponseCapacity = (uint)sizeof(
                GuideXosClipboardResponseWire);
            GuideXosServiceRequestWire* requestPointer = &request;
            GuideXosClipboardResponseWire* responsePointer = &local;
            request.ResponseBuffer = (ulong)(nuint)responsePointer;
            ulong result = InvokeServiceRequest(
                (ulong)(nuint)requestPointer,
                (ulong)sizeof(GuideXosServiceRequestWire));
            response = local;
            return MapStatus(result);
        }

        internal static GuideXosResult TryClearClipboard()
        {
            GuideXosClipboardClearRequestWire request = default;
            request.StructureVersion = AbiVersion;
            request.ServiceId = ClipboardService;
            request.OperationId = ClipboardClearOperation;
            request.RequestLength = (uint)sizeof(
                GuideXosClipboardClearRequestWire);
            request.Reserved = 0;
            GuideXosClipboardClearRequestWire* requestPointer = &request;
            ulong result = InvokeServiceRequest(
                (ulong)(nuint)requestPointer,
                (ulong)sizeof(GuideXosClipboardClearRequestWire));
            return MapStatus(result);
        }

        internal static GuideXosResult TryLaunchApplication(
            string applicationId,
            out GuideXosLaunchResult launchResult)
        {
            return TryShellTarget(applicationId,
                GuideXosShell.MaxApplicationIdLength,
                ShellLaunchApplicationOperation, out launchResult);
        }

        internal static GuideXosResult TryOpenDocument(
            string document,
            out GuideXosLaunchResult launchResult)
        {
            return TryShellTarget(document, GuideXosShell.MaxDocumentLength,
                ShellOpenDocumentOperation, out launchResult);
        }

        internal static GuideXosResult TryOpenShellObject(
            GuideXosShellObject shellObject,
            out GuideXosLaunchResult launchResult)
        {
            if (shellObject != GuideXosShellObject.ComputerFiles)
            {
                launchResult = new GuideXosLaunchResult(
                    GuideXosLaunchResultCode.InvalidRequest);
                return new GuideXosResult(GuideXosStatus.InvalidArgument);
            }
            return TryShellTarget("gxos.shell.computerfiles",
                GuideXosShell.MaxApplicationIdLength,
                ShellOpenObjectOperation, out launchResult);
        }

        internal static GuideXosResult TryReadResourceBytes(
            string resourceName, out byte[] data,
            out GuideXosResourceResultCode resourceResult)
        {
            data = null;
            resourceResult = GuideXosResourceResultCode.InvalidRequest;
            if (!IsValidResourceName(resourceName))
                return new GuideXosResult(GuideXosStatus.InvalidArgument);

            GuideXosResult compatible = RequireCompatible();
            if (compatible.Failed)
                return compatible;

            GuideXosResourceResponseWire metadata;
            GuideXosResult transport = InvokeResourceRequest(resourceName,
                ResourceMetadataOperation, 0, null, out metadata);
            if (transport.Failed)
                return transport;
            if (!IsValidResourceResponse(metadata))
                return new GuideXosResult(GuideXosStatus.ValidationFailed);

            resourceResult = (GuideXosResourceResultCode)metadata.ResultCode;
            if (resourceResult != GuideXosResourceResultCode.Success)
            {
                return metadata.Flags == 0 && metadata.ResourceLength == 0 &&
                       metadata.Offset == 0 && metadata.BytesRead == 0 &&
                       metadata.EndOfResource == 0
                    ? new GuideXosResult(GuideXosStatus.Success)
                    : new GuideXosResult(GuideXosStatus.ValidationFailed);
            }
            if (metadata.Flags != 1 || metadata.ResourceLength >
                    (ulong)GuideXosResources.MaxResourcePayloadLength ||
                metadata.ResourceLength > int.MaxValue)
            {
                resourceResult = GuideXosResourceResultCode.ResourceUnavailable;
                return new GuideXosResult(GuideXosStatus.Success);
            }

            byte[] copy = new byte[(int)metadata.ResourceLength];
            if (copy.Length == 0)
            {
                data = copy;
                resourceResult = GuideXosResourceResultCode.Success;
                return new GuideXosResult(GuideXosStatus.Success);
            }

            GuideXosResourceResponseWire read;
            transport = InvokeResourceRequest(resourceName,
                ResourceReadOperation, metadata.ResourceLength, copy,
                out read);
            if (transport.Failed)
                return transport;
            if (!IsValidResourceResponse(read))
                return new GuideXosResult(GuideXosStatus.ValidationFailed);

            resourceResult = (GuideXosResourceResultCode)read.ResultCode;
            if (resourceResult != GuideXosResourceResultCode.Success)
                return read.Flags == 0 && read.BytesRead == 0 &&
                       read.EndOfResource == 0
                    ? new GuideXosResult(GuideXosStatus.Success)
                    : new GuideXosResult(GuideXosStatus.ValidationFailed);
            if (read.Flags != 1 || read.ResourceLength != metadata.ResourceLength ||
                read.Offset != 0 || read.BytesRead != copy.Length ||
                read.EndOfResource != 1)
                return new GuideXosResult(GuideXosStatus.ValidationFailed);

            data = copy;
            return new GuideXosResult(GuideXosStatus.Success);
        }

        private static GuideXosResult InvokeResourceRequest(
            string resourceName, uint operationId,
            ulong expectedResourceLength, byte[] data,
            out GuideXosResourceResponseWire response)
        {
            GuideXosResourceRequestWire request = default;
            GuideXosResourceResponseWire local = default;
            request.StructureVersion = AbiVersion;
            request.ServiceId = ResourcesService;
            request.OperationId = operationId;
            request.RequestLength = (uint)sizeof(
                GuideXosResourceRequestWire);
            request.ResourceNameLength = (uint)resourceName.Length;
            request.ResponseCapacity = (uint)sizeof(
                GuideXosResourceResponseWire);
            request.ExpectedResourceLength = expectedResourceLength;
            if (data != null)
                request.DataCapacity = (uint)data.Length;

            byte* name = request.ResourceName;
            for (int i = 0; i < resourceName.Length; i++)
                name[i] = (byte)resourceName[i];

#if GUIDEXOS_PHASE34_MALFORMED
            // Diagnostic-only raw request corruption. Public validation has
            // accepted the key; the kernel must reject before resource lookup.
            if (operationId == ResourceMetadataOperation)
                request.OperationId = 99;
#endif

            GuideXosResourceRequestWire* requestPointer = &request;
            GuideXosResourceResponseWire* responsePointer = &local;
            request.ResponseBuffer = (ulong)(nuint)responsePointer;
            if (data == null)
            {
                ulong raw = InvokeServiceRequest(
                    (ulong)(nuint)requestPointer,
                    (ulong)sizeof(GuideXosResourceRequestWire));
                response = local;
                return MapStatus(raw);
            }

            fixed (byte* dataPointer = data)
            {
                request.DataBuffer = (ulong)(nuint)dataPointer;
                ulong raw = InvokeServiceRequest(
                    (ulong)(nuint)requestPointer,
                    (ulong)sizeof(GuideXosResourceRequestWire));
                response = local;
                return MapStatus(raw);
            }
        }

        private static bool IsValidResourceResponse(
            GuideXosResourceResponseWire response)
        {
            return response.StructureVersion == AbiVersion &&
                response.Size == (uint)sizeof(
                    GuideXosResourceResponseWire) &&
                response.ResultCode <= (uint)
                    GuideXosResourceResultCode.BackendFailure &&
                response.Reserved == 0 && response.Flags <= 1 &&
                response.EndOfResource <= 1;
        }

        private static bool IsValidResourceName(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length > GuideXosResources.MaxResourceNameLength ||
                value == "." || value == "..") return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') ||
                      (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') || c == '.' ||
                      c == '-' || c == '_')) return false;
            }
            return true;
        }

        private static GuideXosResult TryShellTarget(
            string targetText, int maximumLength, uint operationId,
            out GuideXosLaunchResult launchResult)
        {
            launchResult = new GuideXosLaunchResult(
                GuideXosLaunchResultCode.InvalidRequest);
            if (string.IsNullOrEmpty(targetText) ||
                targetText.Length > maximumLength)
                return new GuideXosResult(GuideXosStatus.InvalidArgument);

            GuideXosResult compatible = RequireCompatible();
            if (compatible.Failed)
                return compatible;

            GuideXosShellLaunchResponseWire response = default;
            GuideXosShellLaunchRequestWire request = default;
            request.StructureVersion = AbiVersion;
            request.ServiceId = ShellService;
            request.OperationId = operationId;
            request.RequestLength = (uint)sizeof(
                GuideXosShellLaunchRequestWire);
            request.TargetLength = (uint)targetText.Length;
            request.ResponseCapacity = (uint)sizeof(
                GuideXosShellLaunchResponseWire);
#if GUIDEXOS_PHASE33_RAW_INVALID_ACTION
            if (operationId == ShellOpenObjectOperation)
                targetText = "gxos.shell.installtoharddrive";
#elif GUIDEXOS_PHASE33_MALFORMED
            if (operationId == ShellOpenObjectOperation)
                operationId = 99;
#endif
            request.OperationId = operationId;
            request.TargetLength = (uint)targetText.Length;

            byte* target = request.Target;
            for (int i = 0; i < targetText.Length; i++)
            {
                char value = targetText[i];
                target[(i * 2) + 0] = (byte)value;
                target[(i * 2) + 1] = (byte)(value >> 8);
            }

            GuideXosShellLaunchRequestWire* requestPointer = &request;
            GuideXosShellLaunchResponseWire* responsePointer = &response;
            request.ResponseBuffer = (ulong)(nuint)responsePointer;
            ulong rawResult = InvokeServiceRequest(
                (ulong)(nuint)requestPointer,
                (ulong)sizeof(GuideXosShellLaunchRequestWire));
            GuideXosResult transport = MapStatus(rawResult);
            if (transport.Failed)
                return transport;

            if (response.StructureVersion != AbiVersion ||
                response.Size != (uint)sizeof(
                    GuideXosShellLaunchResponseWire) ||
                response.Reserved != 0 ||
                response.ResultCode > (uint)
                    GuideXosLaunchResultCode.BackendFailure)
                return new GuideXosResult(GuideXosStatus.ValidationFailed);

            launchResult = new GuideXosLaunchResult(
                (GuideXosLaunchResultCode)response.ResultCode);
            return new GuideXosResult(GuideXosStatus.Success);
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
