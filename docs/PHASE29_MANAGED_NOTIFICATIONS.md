# Phase 29 Managed Notifications

Phase 29 proves that a separately compiled NativeAOT Ring 3 application can
submit a notification through the existing Phase 8 App Model Notifications
service using only the public `GuideXos.User` SDK. The application does not
know the raw syscall number, service ID, `ApplicationServiceContext`, process
table, application instance, or kernel-owned object address.

## Existing service contract

The proof uses `ApplicationServiceId.Notifications` (`1`) and the existing
publish operation (`1`). The existing service creates a transient,
desktop-visible, service-owned toast through `NotificationManager.AddForApplication`.
The service stores the stable application identity string as its source tag;
the user-facing payload is the rendered title/body message. The existing
manager auto-dismisses these toasts after its normal lifetime and supports
explicit `ClearForApplication`. Phase 29 adds no persistent notification
handle, notification registry, queue, or process-termination policy. In
particular, it does not claim automatic `ClearForApplication` on process
termination; lifecycle cleanup in this phase covers the process, managed
image, application instance, and service context.

The service accepts:

* title: non-empty UTF-16 text, at most 64 code units;
* body: UTF-16 text, at most 256 code units, including empty body;
* severity: `Information` or `Error`;
* result: an existing typed `GuideXosResult`, with no exception-based error
  contract.

The public surface is intentionally small:

```csharp
GuideXosResult result = GuideXosNotifications.TryShow(
    "Phase 29", "Managed Ring 3 notification",
    GuideXosNotificationSeverity.Information);
```

`GuideXosNotifications` and `GuideXosNotificationSeverity` are the only new
application-facing types. The implementation remains in the private
`GuideXosInternalAbi` layer.

## Wire record

The private notification request is a packed 668-byte record:

| Offset | Size | Field |
| ---: | ---: | --- |
| 0 | 4 | structure version |
| 4 | 4 | service ID |
| 8 | 4 | operation ID |
| 12 | 4 | exact request length (`668`) |
| 16 | 2 | title length in UTF-16 code units |
| 18 | 2 | body length in UTF-16 code units |
| 20 | 4 | severity |
| 24 | 4 | reserved, required zero |
| 28 | 128 | fixed UTF-16LE title buffer |
| 156 | 512 | fixed UTF-16LE body buffer |

Lengths are authoritative. The buffers contain no pointers and no terminator
is required. The SDK copies UTF-16 code units into the fixed buffers and the
kernel copies the complete record from readable user memory before decoding
it. The kernel rejects wrong version, service, operation, exact size, reserved
bits, lengths, and severity before constructing the existing
`ApplicationNotificationRequest`.

## Authority and kernel path

The syscall transport is the already-proven service-request operation. The
kernel performs the following sequence:

1. validate the user range and copy the complete bounded record;
2. derive the owning process from the scheduled Ring 3 process;
3. derive the `ApplicationInstance` from `process.OwningApplicationInstance`;
4. validate the live instance through `ApplicationInstanceRegistry`;
5. create and validate the existing `ApplicationServiceContext` for the
   Notifications service;
6. obtain service access and call the existing
   `ApplicationNotificationService.Publish` backend;
7. return the typed status through the existing response path.

No caller-supplied application ID, process handle, context, instance token,
object address, or pointer is accepted as authority. The invalid-type proof
uses an internal-only build symbol to send severity `99`; the public enum has
only the two supported values and cannot express that request.

## Proof modes and controls

The five NativeAOT images are independently compiled from the same public SDK
project:

* success: result `29`;
* title-bound failure: exact 65-code-unit title, result `InvalidArgument`;
* body-bound failure: exact 257-code-unit body, result `InvalidArgument`;
* invalid-type failure: internal severity `99`, result `InvalidArgument`;
* fail-fast: valid notification submission followed by local `FailFast(0x29)`.

The diagnostic runs four successful lifetimes, the bound and invalid-type
negative cases, fail-fast, stale application/context rejection, replacement
application lifetime, and cleanup balancing. The runtime evidence includes:
`RING3_NOTIFICATION_REQUEST_COPIED_IN=1`,
`RING3_NOTIFICATION_PROCESS_IDENTITY_DERIVED=1`,
`RING3_NOTIFICATION_APP_MODEL_OWNER_DERIVED=1`,
`RING3_NOTIFICATION_SERVICE_CONTEXT_DERIVED=1`,
`RING3_NOTIFICATION_BACKEND_ACCEPTED=1`,
`RING3_NOTIFICATION_RESPONSE_COPIED_OUT=1`,
`PHASE29_INVALID_TYPE_REJECTED=1`,
`PHASE29_MANAGED_CLEANUP_BALANCED=1`,
`PHASE29_BOOTSTRAP_CLEANUP_BALANCED=1`,
`PHASE29_PROCESS_CLEANUP_BALANCED=1`, and
`RING3_PHASE29_COMPLETE=1`.

The proof does not add a managed thread, async service, reflection, dynamic
loading, filesystem, networking, GUI, or input API to the SDK.

## Build and artifact integrity

`Tools/Phase29/build_phase29_managed_notification.ps1` produces and hashes
each NativeAOT image; `Tools/Phase29/stage_phase29_image.ps1` stages the
matching `.exe` and `.gxmi` pairs. Each descriptor records service `1`,
operation `1`, wire size `668`, title bound `64`, and body bound `256`. The
kernel allowlist in `Kernel/Misc/ManagedImage.cs` pins the final SHA-256 of
each proof image before it is executable.

The diagnostic selector is `Ring3Phase29` in `build.ps1` and
`run_uefi_validation.ps1`. The existing Phase 26 PAL closure remains the
only native dependency surface: ABI discovery and service-request transport.
The managed image imports no kernel symbols directly.
