# guideXOS Application Platform Services — Phase 8 Design

**Date:** 2026-09-17

**Repository:** `D:\dev\guideXOSUEFI`

**Canonical architecture:** `APP_MODEL_CONVERGENCE.md`
**Reference trees:** `D:\dev\guideXOSServer` (read-only), `D:\dev\guideXOS` (historical read-only)

## Goal

Introduce a bounded application-facing platform-service boundary for the C# App Model, preserve `ApplicationInstanceRegistry` as the only instance authority, and migrate a small first cohort of useful services without changing Server, Legacy, launch/lifecycle ownership, persistence, IPC, or process isolation.

The approved first cohort is:

1. Notifications.
2. Session application settings keyed by stable descriptor/application identity.
3. System information.

Session settings are application-scoped, not instance-scoped: two live Notepad instances address the same Notepad settings namespace, while transient state such as document text, undo stacks, and windows remains on `ApplicationInstance` or the application implementation.

## Source evidence and scope

The current C# App Model already provides the required authority and lifecycle primitives in:

- `guideXOS/OS/ApplicationInstance.cs` — generation-safe handles, bounded registry, lifecycle state, ownership, termination, and observation.
- `guideXOS/OS/ApplicationFactories.cs` — the modern descriptor-to-factory boundary receiving an `ApplicationInstance`.
- `guideXOS/OS/ModernAppModel.cs` and `guideXOS/OS/AppModel.cs` — descriptors, capabilities, typed requests, compatibility diagnostics, and deterministic self-tests.
- `guideXOS/GUI/NotificationManager.cs` — the proven C# notification presentation backend.
- `guideXOS/OS/Configuration.cs` and `guideXOS/OS/SystemMode.cs` — existing settings/storage behavior, including the distinction between writable and non-writable system modes.
- `guideXOS/DefaultApps/Calculator.cs`, `Notepad.cs`, and `TaskManager.cs`, plus `guideXOS/GUI/DisplayOptions.cs` — representative direct service and kernel/global access.

The Server comparison will use the authoritative modern sources rather than names alone:

- `app_manifest.h/.cpp`, `app_registry.h/.cpp`, and manifest validation for identity and permission metadata.
- `notification_manager.h/.cpp` for notification semantics.
- `message_box.h/.cpp`, `open_dialog.h/.cpp`, and `save_dialog.h/.cpp` for user-interaction services that remain deferred.
- `app_launch_target.h`, `app_launch_resolver.h/.cpp`, `desktop_service.h/.cpp`, and shell registries for launch/shell semantics.
- VFS and kernel file-operation sources for filesystem and clipboard behavior.
- clock, task-manager, compositor, allocator, and runtime sources for system-information equivalents.

The implementation must produce a complete convergence matrix in `APP_MODEL_CONVERGENCE.md`, including deferred categories: dialogs, app-local storage, resources, clipboard, and shell/open services.

## Architecture

### Bounded registry, not dependency injection

`ApplicationServiceRegistry` is a fixed-capacity table of deterministic service identities. It owns registrations and backend bindings only; it does not own application instances, windows, launch requests, or lifecycle transitions. It exposes typed lookup methods for the selected services and rejects duplicate or missing registrations deterministically.

The registry is initialized as part of App Model setup before application factories are exercised. Its table is bounded and suitable for NativeAOT/UEFI paths. No dynamic global dictionary, service locator with arbitrary objects, or generic dependency-injection framework is introduced.

### Application service context

`ApplicationServiceContext` is a small value-like application-facing context created from a valid `ApplicationInstanceHandle`. It contains:

- the generation-safe instance handle;
- stable descriptor/application identity;
- a bounded snapshot of the descriptor capability metadata for future policy decisions.

It does not copy launch arguments, documents, windows, or other instance state. It is not an authority and cannot create, terminate, activate, or mutate an `ApplicationInstance`.

Every typed service call revalidates the context through `ApplicationInstanceRegistry.TryGet`, verifies that the resolved instance still has the same descriptor identity, and applies the approved lifecycle policy. The context is therefore safe to retain as a capability-shaped value while stale generations remain rejectable.

### Service access

Application factories receive the existing `ApplicationInstance`. They obtain a context through the service boundary and pass only the required typed service access/context into representative application objects. Application objects do not reach through the context to `Desktop`, `WindowManager`, framebuffer state, or kernel-global objects.

The public shape is conceptually:

```text
ApplicationInstance.Handle
    -> ApplicationServiceRegistry.TryCreateContext(handle)
    -> typed ApplicationServices access
    -> per-call context + lifecycle validation
    -> selected service backend
```

The existing factory launch path remains the only launch/lifecycle path.

## Result and error model

Selected services return bounded typed results rather than using null as machine state or requiring callers to interpret arbitrary message strings. The shared result vocabulary is limited to outcomes justified by the cohort:

- `Success`
- `NotSupported`
- `PermissionDenied` (reserved for an actual declared capability gate; no synthetic gate is added where Server does not enforce one)
- `ResourceUnavailable`
- `NotFound`
- `InvalidRequest`
- `InvalidContext`
- `InvalidState`
- `BackendFailure`

Diagnostics are bounded and for observability only. They are not the result discriminator.

## Lifecycle policy

For Phase 8, service access is valid only while the associated instance is in a non-terminal, non-suspended, non-closing state. The following are rejected:

- stale generation-safe handles;
- terminated instances;
- failed instances;
- closing instances;
- suspended instances.

Registered/loading/initialized/running/activated/inactive states remain eligible for the bounded service calls. Zero-window reusable instances remain eligible because service access is tied to application identity and instance validity, not to window count. No selected service silently continues after instance termination.

## Capability behavior

The descriptor and manifest capability/permission metadata will be inventoried and carried into the bounded context. The selected Server references do not expose a single enforced application-service capability policy for notifications, session settings, or the proposed system-information snapshot. Therefore Phase 8 will not invent one. Selected services will document no required capability and will not emit a fake denial merely because the descriptor has no unrelated permission string.

If the authoritative audit finds an existing, concrete Server-enforced capability for a selected service, the C# backend will map it to `PermissionDenied` with a focused self-test. Otherwise capability enforcement remains deferred while the context shape preserves future compatibility.

## First cohort contracts

### Notifications

Applications submit a bounded notification request containing application identity, bounded title/body text, and an informational or error severity. The C# backend adapts to the existing toast manager and its proven auto-dismiss behavior; the Server semantic reference is `NotificationManager::Add`, `Update`, `Clear`, and snapshot/dismissal behavior. A bounded clear operation is supported; renderer-specific notification objects remain private to the backend.

Representative migrations are Calculator and Display Options, whose current user-facing paths call `NotificationManager` directly.

### Session application settings

Settings use a bounded application namespace keyed by stable descriptor/application ID, not by instance handle. Access still requires a valid, current `ApplicationServiceContext`. Supported values are bounded primitive settings: boolean, bounded integer, and bounded string. Missing keys return `NotFound`; invalid key/value bounds return `InvalidRequest`.

This phase provides session-only semantics. No file persistence, registry persistence, global unbounded dictionary, or fabricated durable guarantee is added. The C# backend uses a bounded application-identity table and clears it during service-registry reset/self-test cleanup. The Server comparison will record its existing global/configuration settings as `SAME IDEA / DIFFERENT API` or `C# ONLY` where application scoping is absent.

Notepad will use the service for its wrap preference. Its document, dirty flag, undo/redo stacks, dialogs, and windows remain instance/application state and are not placed in the settings service.

### System information

Applications request a bounded read-only snapshot of stable scalar information such as uptime and currently available memory/thread/CPU metrics, subject to backend availability. The C# backend samples existing kernel/runtime counters and returns values, not raw allocator, timer, thread-pool, or framebuffer objects. The Server audit will map the equivalent clock/task-manager/runtime sources and explicitly distinguish application-facing semantics from implementation-only metrics.

Task Manager will consume the snapshot for the migrated metrics path. Rendering and chart state remain application-owned.

## Backend separation

The shared contract is source-compatible in meaning, not implementation-compatible in code:

- C# notifications use `guideXOS.GUI.NotificationManager`; Server notifications use its compositor-backed `NotificationManager`.
- C# session settings use a bounded in-memory application-keyed backend; Server’s existing configuration/persistence mechanisms remain a comparison reference and are not modified.
- C# system information reads current UEFI/runtime primitives; Server maps its own clock, runtime, task-manager, and kernel sources in a future backend.

No Server or Legacy source is modified by Phase 8.

## Diagnostics and verification

Deterministic self-tests will cover:

- registration and duplicate registration;
- missing typed lookup;
- valid context creation;
- stale-generation rejection;
- terminated/failed/closing/suspended rejection;
- successful notification, settings, and system-information calls;
- bounded invalid requests and backend failures where applicable;
- application-scoped settings shared by two instances of the same descriptor;
- distinct settings namespaces for different descriptors;
- cleanup of service state and diagnostic application instances.

A runtime diagnostic will use normal App Model factory launches for Calculator, Notepad, and Task Manager, exercise the selected services, close those instances through the existing lifecycle path, and verify no stale service context, stale instance ownership, window leakage, taskbar leakage, or alternate launch path.

The full Phase 7 regression matrix remains mandatory: AppModel, compatibility, lifecycle, taskbar/grouping, AppRuntime, NativeInput, ContextMenu, production Continuous, allocator, ThreadPool, graphics, input, fallback, legacy-backend, stale-instance, stale-ownership, and stale-taskbar gates.

## Deferred services and future isolation

Dialogs, app-local storage, resources, clipboard, and shell/open services are inventoried and classified but deferred unless the audit identifies a genuinely smaller bounded addition. Persistence, IPC, Ring 3, process address spaces, scheduler changes, renderer redesign, and UI redesign are out of scope.

The public service contracts carry only bounded values and generation-safe handles. They do not require shared address space, direct kernel object references, unrestricted filesystem paths, direct WindowManager access, or renderer ownership, so a future process boundary can proxy them without changing application-facing meaning.
