# guideXOS Phase 9 Dialog, File Picker, and Shell/Open Services

**Status:** Approved design specification  
**Date:** 2026-09-18  
**Repository:** `D:\dev\guideXOSUEFI`  
**Baseline:** `c5d159420f2071cd4a6eca493ab27fc858a74db5` (`test: accept Phase 8 application services`)

## Goal

Applications request user interaction and shell behavior through the existing
`ApplicationServiceRegistry` and `ApplicationServiceContext`. They do not
construct `Window`, `MessageBox`, `OpenDialog`, `SaveDialog`, `Desktop`, or
other shell implementation objects.

Phase 9 adds a bounded first cohort:

1. Information, error, and confirmation dialogs.
2. Open-file requests.
3. Save-file requests.
4. Shell/application launch and associated-document open.

The C# implementation remains same-address-space in this phase. The public
contracts contain only bounded scalar values, enums, stable IDs, paths, and
generation-safe handles so a future isolated-process adapter can issue the
same requests without changing application-facing semantics.

## Architectural decisions

### Existing authorities remain authoritative

`ApplicationInstanceRegistry` remains the authority for instance existence,
generation, lifecycle, termination, and ordinary application-window
ownership. `ApplicationServiceRegistry` remains the only application service
registry. The modern typed App Model remains the only launch path. Phase 9
does not add a compatibility launch mechanism, a legacy backend, an IPC
transport, or a new modal process model.

### Fixed asynchronous request sessions

Interactive service calls create a fixed-capacity request session and return a
serializable `ApplicationServiceRequestHandle`. The application observes the
session through the service layer. The backend may complete a request later;
the application never waits in a spin loop and no service holds a lock while
waiting for input.

The request table is fixed-capacity and stores only bounded request state. A
request handle contains a slot and generation plus the service identity. It
does not contain a `Window`, callback, delegate, renderer object, filesystem
object, allocator object, or process object.

The dialog, open-file, and save-file services share one outstanding
interactive request per application instance. A second active or unconsumed
interactive request returns the explicit typed `Conflict` result. A completed
request remains observable until the application consumes it or lifecycle
cleanup deterministically dismisses it.

### Create versus observe lifecycle policy

Creating a new user-interaction request requires a generation-valid instance in
`Running` or `Activated`. `Loading`, `Initialized`, `Inactive`, `Suspended`,
`Closing`, `Terminated`, and `Failed` reject creation with a bounded service
result.

Observing, cancelling, or consuming an already-issued request is a separate
operation. A valid request remains associated with its original generation
while its requester temporarily becomes `Inactive`; an existing pending or
completed result can therefore be observed without making inactive
applications initiate new interaction. Completed results remain retained
until safe consumption when the requester is eligible again. A stale,
terminated, or failed context cannot observe or consume the request after
deterministic cleanup.

Transition to `Closing`, `Terminated`, or `Failed` cancels and releases all
request sessions for that generation. No request session survives generation
change. Reused instance slots cannot observe an earlier request.

## Bounded common contracts

### Service IDs and result vocabulary

The existing service ID enum is extended with `Dialogs`, `OpenFile`,
`SaveFile`, and `Shell`. The existing Phase 8 result type remains the common
result wrapper. Its result vocabulary retains all Phase 8 values and adds the
typed Phase 9 values needed by real behavior:

| Code | Meaning |
| --- | --- |
| `Success` | The request was accepted or the observed operation completed successfully. |
| `Cancelled` | The user or caller cancelled an operation where cancellation is meaningful. |
| `InvalidContext` | The context is stale, mismatched, terminated, failed, or otherwise not valid for the operation. |
| `InvalidState` | The context is valid but the operation is not legal in its current lifecycle/session state. |
| `InvalidRequest` | A request is malformed or exceeds a documented bound. |
| `NotFound` | The requested application, shell object, association, or path was not found. |
| `UnsupportedTarget` | The target kind or operation is known but not supported by the current backend. |
| `PermissionDenied` | A real backend permission/capability check denied the operation. |
| `ResourceUnavailable` | A bounded table, filesystem, or backend resource is unavailable. |
| `Conflict` | An outstanding interactive request or another explicitly conflicting session already exists. |
| `BackendFailure` | The selected backend failed after request validation. |
| `Unsupported` | Retained for Phase 8 compatibility; new Phase 9 target failures use `UnsupportedTarget`. |

Every diagnostic is capped at the existing 192-character bound. No result
exposes a backend object.

### Request handles and status

The implementation adds a bounded request-handle value and a typed status
payload:

```text
ApplicationServiceRequestHandle
  ServiceId
  Slot
  Generation

ApplicationServiceRequestState
  Pending
  Completed
  Cancelled
  Failed
```

An observation returns `ApplicationServiceResult<RequestStatus>` where the
status contains the state and one bounded service-specific payload. A pending
status is a successful observation of an existing request, not a failure.
The handle and status are immutable from the application’s perspective.

### Bounds

The first implementation uses these exact bounds:

| Value | Bound |
| --- | ---: |
| Dialog title | 64 characters |
| Dialog body | 256 characters |
| Starting location | 1024 characters |
| Selected path/result path | 1024 characters |
| Suggested filename | 128 characters |
| Shell target, alias, object ID, or action ID | 1024 characters |
| Diagnostic | 192 characters |
| Request sessions per instance | fixed table; no dynamic growth |

The 1024-character path/target bound is aligned with the existing typed
`LaunchRequest.MaxTextLength`. Inputs are rejected when they exceed their
bound; they are not silently accumulated or expanded in a hot path.

### Dialog contract

Dialog requests contain:

```text
ApplicationDialogRequest
  Kind: Information | Error | Confirmation
  Title: bounded, non-empty text
  Body: bounded text
  ButtonSet: Acknowledge | AcceptRejectCancel
```

Information and error dialogs use `Acknowledge`. Confirmation uses
`AcceptRejectCancel`, matching the existing save-changes behavior. Arbitrary
button labels are not part of this phase.

Dialog results contain:

```text
ApplicationDialogOutcome
  Accepted
  Rejected
  Cancelled
  Closed
  BackendFailure
```

The existing C# message box maps acknowledgement to `Accepted` and Escape or
window close to `Closed`/`Cancelled` according to the specific request. The
save-changes dialog maps Save to `Accepted`, Don’t Save to `Rejected`, and
Cancel/close to `Cancelled`. Backend construction or completion failure is
`BackendFailure`.

### Open-file contract

`OpenFileRequest` contains a bounded starting location. The first common
contract selects files only; directories may be navigated but are not returned
as selected values because neither audited backend currently exposes a
directory-selection operation. The result contains a bounded selected path or
one of `Cancelled`, `NotFound`, `ResourceUnavailable`, `InvalidRequest`, or
`BackendFailure`.

Filters and arbitrary file-type descriptions are deferred until both backends
provide a common, observable behavior. This avoids a contract field that one
backend silently ignores.

### Save-file contract

`SaveFileRequest` contains a bounded starting location and bounded suggested
filename. The result contains a bounded destination path or one of
`Cancelled`, `NotFound`, `ResourceUnavailable`, `InvalidRequest`, or
`BackendFailure`.

The existing extension behavior is preserved: the C# save backend adds `.txt`
when the selected name has no dot, and the Server behavior remains its own
backend adaptation. No overwrite-confirmation policy is invented in Phase 9;
the current audited implementations do not share one.

### Shell/Open contract

`ShellOpenRequest` is a typed target:

```text
ApplicationShellOpenTarget
  ApplicationId
  Alias
  AssociatedDocumentPath
  ShellObjectId
  TypedShellAction
```

The C# adapter converts the target to the existing `LaunchRequest` and
`ModernShellAdapter`/`ModernFileAssociationAdapter` paths. Application IDs,
aliases, document paths, shell object IDs, and action IDs remain bounded
serializable text. The service does not call a second launcher.

The result preserves the underlying typed outcome: `Success`, `NotFound`,
`UnsupportedTarget`, `PermissionDenied`, `ResourceUnavailable`,
`InvalidRequest`, `InvalidContext`, `InvalidState`, or `BackendFailure`, plus
the bounded launched application identity and generation-safe instance handle
when available.

## Dialog ownership and window semantics

Service-created dialog windows are graphical objects owned by `WindowManager`,
but they are not ordinary application content windows.

Each request session carries bounded semantic owner metadata: the requesting
`ApplicationInstanceHandle`, service ID, request handle, and transient-session
classification. The implementation uses an explicit transient/service
session ownership path (or equivalent metadata recognized by lifecycle and
window-management code). It does not add the dialog to the ordinary owned
content-window array used for final-window policy, application-window counts,
Task Manager observations, or taskbar grouping.

Dialog windows remain `ShowInTaskbar = false`, do not create application taskbar
entries, and do not alter ordinary application content-window counts. The
requester identity is still available for diagnostics, cancellation, and
cleanup. On requester termination or failure, the session closes/dismisses
the transient window and releases the semantic owner metadata before the
request slot is released.

This preserves the distinction:

```text
ApplicationInstanceRegistry → ordinary content ownership/lifecycle authority
ApplicationServiceRegistry  → semantic request/session owner
WindowManager               → graphical object lifetime and input routing
```

No lock is held while a dialog is visible. The parent application remains an
ordinary usable instance from the lifecycle registry’s perspective; the
visible service window receives the relevant topmost input, while other
applications remain usable. Click-away does not implicitly accept or cancel a
dialog. Escape and close follow the typed cancellation/closed mapping.

## Backend adaptation

### C# backend

The C# backend wraps the existing `MessageBox`, `OpenDialog`, `SaveDialog`,
and `SaveChangesDialog` implementations. Those classes remain internal GUI
implementation details. The adapter sets bounded text, registers transient
service-session metadata, maps callbacks and close/escape behavior into the
fixed request table, and removes the metadata during completion or cleanup.

The backend may add narrowly scoped hooks to make cancel/close completion
deterministic and to prevent a dialog from being counted as ordinary content.
It does not introduce a global modal loop or change the existing filesystem
implementation.

### Server backend

Server remains read-only in Phase 9. The audited semantics are represented by
the common contract: message boxes are acknowledgement dialogs, open dialogs
return selected file paths, save dialogs return destinations, and save-changes
maps to the typed confirmation outcomes. Server’s existing typed launch
resolver, association routing, shell registry, and result distinctions are
the authoritative shell semantics. Server capability names are documented as
future mapping vocabulary only; no capability restriction is invented where
the audit found no unified enforcement path.

## Representative migrations

### Notepad

Notepad requests Open, Save, Save As, dirty-document confirmation, and
bounded error/information messages through its service access. It retains
document bytes, dirty state, undo/redo, wrap session setting, multiple
instances, and direct filesystem I/O. It observes request handles and applies
results only to the originating instance.

### Computer Files

Computer Files retains its internal filesystem browser and navigation. When it
conceptually asks the OS to launch an application, open an associated document,
or invoke a shell target, it uses Shell/Open service access. Internal list
rendering and directory enumeration remain direct implementation behavior.

### Display Options

Display Options uses Open File service for background selection. Color picker,
effects, and other in-process presentation controls remain direct application
implementation.

GXM script editing, shell-internal menus/taskbar code, compatibility shims,
and unrelated backend-only message boxes remain classified as internal and are
not blindly migrated.

## Capability policy

The existing context carries bounded capability-shaped text and revalidates
the instance on every operation. The audit found Server manifest permission
vocabulary but no unified service enforcement path for these exact operations.
Phase 9 therefore does not reject dialogs, file pickers, or shell operations
based on invented capabilities. The service boundary and stable service IDs
remain ready for a future real policy mapping.

## Verification contract

Deterministic self-tests cover bounds, result vocabulary including `Conflict`,
request creation versus observation lifecycle, stale generation rejection,
duplicate interactive requests, cancel/close, selected path propagation,
typed shell launch/open, invalid targets, transient window classification,
cleanup after termination, and completed-result retention across `Inactive`.

The runtime diagnostic proves, with real application instances, that Phase 8
notifications/settings/system information still work; Notepad performs dialog
open/cancel/save/confirmation flows; Shell/Open launches or opens a target;
the requester identity remains correct; and no orphan dialog, stale service
context, stale ownership, compatibility fallback, or legacy backend appears.

The existing AppModel, compatibility, lifecycle, taskbar/grouping, AppRuntime,
NativeInput, ContextMenu, and production Continuous validations remain
required. Server and Legacy repositories remain unmodified.

## Deferred scope

Phase 9 does not implement app-local storage, persistent settings, clipboard,
resource/package services, IPC, Ring 3, filesystem replacement, GXM rewrite,
WindowManager redesign, taskbar visual redesign, allocator changes, scheduler
changes, or boot/BIOS changes.
