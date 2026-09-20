namespace guideXOS.OS {
    internal sealed class CSharpApplicationResourceService :
            ApplicationResourceService {
        private sealed class ResourceEntry {
            internal string ApplicationId;
            internal string ResourceKey;
            internal byte[] Bytes;
        }

        private const string DiagnosticApplicationId =
            "selftest.phase8.services";
        private const string DiagnosticResourceKey = "diagnostic.fixture";
        private readonly ResourceEntry[] _entries = new ResourceEntry[1];

        internal CSharpApplicationResourceService() {
            _entries[0] = new ResourceEntry {
                ApplicationId = DiagnosticApplicationId,
                ResourceKey = DiagnosticResourceKey,
                Bytes = CreateDiagnosticBytes()
            };
        }

        public override ApplicationServiceResult<ApplicationResourceMetadata>
                GetMetadata(ApplicationServiceContext context,
                    ApplicationResourceRequest request) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.Resources,
                    out instance, out valid)) {
                return ApplicationServiceResult<ApplicationResourceMetadata>.Failure(
                    valid.Code, valid.BoundedDiagnostic);
            }
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult<ApplicationResourceMetadata>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Resource key is invalid or exceeds its bound");
            }
            ResourceEntry entry = Find(context.ApplicationId,
                request.ResourceKey);
            if (entry == null) {
                return ApplicationServiceResult<ApplicationResourceMetadata>.Failure(
                    ApplicationServiceResultCode.NotFound,
                    "Application resource was not found");
            }
            return ApplicationServiceResult<ApplicationResourceMetadata>.SuccessResult(
                ApplicationResourceMetadata.Create(entry.ResourceKey,
                    entry.Bytes.Length, true));
        }

        public override ApplicationServiceResult<ApplicationResourceReadResult>
                Read(ApplicationServiceContext context,
                    ApplicationResourceReadRequest request) {
            ApplicationInstance instance;
            ApplicationServiceResult valid;
            if (!ApplicationServiceRegistry.TryValidateContext(
                    context, ApplicationServiceId.Resources,
                    out instance, out valid)) {
                return ApplicationServiceResult<ApplicationResourceReadResult>.Failure(
                    valid.Code, valid.BoundedDiagnostic);
            }
            if (request == null || !request.IsValid) {
                return ApplicationServiceResult<ApplicationResourceReadResult>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Resource read request is invalid or exceeds its bound");
            }
            ResourceEntry entry = Find(context.ApplicationId,
                request.ResourceKey);
            if (entry == null) {
                return ApplicationServiceResult<ApplicationResourceReadResult>.Failure(
                    ApplicationServiceResultCode.NotFound,
                    "Application resource was not found");
            }
            long length = entry.Bytes.Length;
            if (request.Offset > length) {
                return ApplicationServiceResult<ApplicationResourceReadResult>.Failure(
                    ApplicationServiceResultCode.InvalidRequest,
                    "Resource read offset is outside the resource");
            }

            if (request.Offset == length) {
                return ApplicationServiceResult<ApplicationResourceReadResult>.SuccessResult(
                    ApplicationResourceReadResult.Create(entry.ResourceKey,
                        request.Offset, new byte[0], 0, true));
            }

            long remaining = length - request.Offset;
            int count = remaining > request.MaximumBytes
                ? request.MaximumBytes : (int)remaining;
            byte[] bytes = new byte[count];
            for (int i = 0; i < count; i++) {
                bytes[i] = entry.Bytes[(int)request.Offset + i];
            }
            return ApplicationServiceResult<ApplicationResourceReadResult>.SuccessResult(
                ApplicationResourceReadResult.Create(entry.ResourceKey,
                    request.Offset, bytes, count,
                    request.Offset + count == length));
        }

        private ResourceEntry Find(string applicationId, string resourceKey) {
            for (int i = 0; i < _entries.Length; i++) {
                ResourceEntry entry = _entries[i];
                if (entry != null && TextEquals(entry.ApplicationId,
                        applicationId) && TextEquals(entry.ResourceKey,
                        resourceKey)) return entry;
            }
            return null;
        }

        private static byte[] CreateDiagnosticBytes() {
            string text = "guideXOS Phase 10 bounded application resource fixture";
            byte[] bytes = new byte[text.Length];
            for (int i = 0; i < text.Length; i++) bytes[i] = (byte)text[i];
            return bytes;
        }

        private static bool TextEquals(string left, string right) {
            if (left == null || right == null || left.Length != right.Length) {
                return false;
            }
            for (int i = 0; i < left.Length; i++) {
                if (left[i] != right[i]) return false;
            }
            return true;
        }
    }
}
