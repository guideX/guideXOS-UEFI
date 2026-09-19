# guideXOS Phase 9 Dialog, File Picker, and Shell/Open Services Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add bounded asynchronous dialog, open-file, save-file, and Shell/Open services to the existing application service layer, migrate the approved representative applications, and prove lifecycle-safe runtime behavior without changing Server or Legacy.

**Architecture:** Extend `ApplicationServiceRegistry`, `ApplicationServiceContext`, and the existing typed result wrappers. Add fixed request sessions identified by generation-safe serializable handles; interactive services create sessions and applications observe them without blocking. The C# backend adapts existing dialog windows and modern App Model launch/association paths, while transient service-window metadata is kept separate from ordinary application content-window ownership.

**Tech Stack:** C#/.NET 7 NativeAOT-oriented UEFI project, existing `ApplicationInstanceRegistry`, existing `ModernAppModel` typed launch adapters, existing `WindowManager`, PowerShell/QEMU UEFI validation, deterministic in-guest self-tests.

**Spec:** `docs/superpowers/specs/2026-09-18-dialog-file-shell-services-phase-9-design.md`

## Global Constraints

- Preserve `ApplicationInstanceRegistry` as the authority for instance existence, generation, lifecycle, termination, and ordinary application-window ownership.
- Preserve `ApplicationServiceRegistry` as the only application service registry.
- Preserve the modern typed App Model as the only application launch path; do not add a second launcher or legacy fallback.
- Interactive service calls are asynchronous fixed sessions; no spin loops, blocking modal loops, or locks held while waiting for input.
- New user-interaction requests require `Running` or `Activated`; observation/cancellation of an already-issued request remains valid through temporary `Inactive` while the generation remains valid.
- Dialog windows are transient service-session windows and must not enter ordinary owned-content-window counts, final-window policy, taskbar grouping, or Task Manager application-window observations.
- Every public text field is bounded: title 64, body 256, starting/selected path 1024, suggested filename 128, shell target/action 1024, diagnostic 192.
- The fixed session table has no dynamic growth and returns `ResourceUnavailable` when capacity is exhausted.
- The shared service result vocabulary includes explicit `Conflict`, `Cancelled`, `InvalidState`, `UnsupportedTarget`, and `BackendFailure` values while retaining Phase 8 compatibility values.
- No Server, Advanced Server, or Legacy file may be modified.
- Do not implement persistence, app-local storage, clipboard, resources/package service, IPC, Ring 3, filesystem rewrite, GXM rewrite, allocator changes, scheduler changes, or boot changes.
- Preserve the existing Phase 8 notification, settings, and system-information behavior and diagnostics.

## Review Focus

- Inactive requester with a pending dialog: existing observation must remain valid, while a new request must be rejected; covered by Task 2 lifecycle tests.
- Service dialog counted as ordinary application content: content-window count, final-window policy, taskbar grouping, and Task Manager observation must remain unchanged; covered by Task 3 tests.
- Reused application slot with an old request handle: generation mismatch must reject observation after termination and relaunch; covered by Task 2 tests.
- Dialog close/Escape and requester termination: every path must complete or cancel the session and release the transient window; covered by Tasks 4 and 7.
- Shell association failure: `NotFound`, `UnsupportedTarget`, and backend failure must remain distinguishable rather than flattening to generic failure; covered by Task 6 tests.

## File Map

- Modify `guideXOS/OS/ApplicationServices.cs`: extend public bounded service IDs/results and add serializable request, dialog, file, and Shell/Open contract values.
- Modify `guideXOS/OS/ApplicationServiceRegistry.cs`: register the four services, validate create versus observe lifecycle, own the fixed request table, and perform generation/lifecycle cleanup.
- Modify `guideXOS/OS/ApplicationServiceBackends.cs`: retain Phase 8 adapters and add service-session dispatch glue only where shared validation is required.
- Create `guideXOS/OS/ApplicationServiceSessions.cs`: fixed request-session records, request status values, transient semantic owner metadata, and deterministic cleanup helpers.
- Create `guideXOS/OS/ApplicationDialogServices.cs`: bounded dialog service surface and C# dialog backend adapter.
- Create `guideXOS/OS/ApplicationFileDialogServices.cs`: bounded Open/Save service surfaces and C# file-dialog adapters.
- Create `guideXOS/OS/ApplicationShellServices.cs`: typed Shell/Open request mapping to the existing modern launch and association adapters.
- Modify `guideXOS/GUI/Window.cs`: expose only the minimal internal service-session classification hook needed to prevent ordinary content ownership accounting.
- Modify `guideXOS/GUI/WindowManager.cs`: register/deregister transient service-session windows and preserve existing graphical lifetime/input traversal.
- Modify `guideXOS/OS/ApplicationInstance.cs`: invoke service-session cleanup at lifecycle termination/close without adding transient windows to ordinary owned-window counts.
- Modify `guideXOS/DefaultApps/Notepad.cs`: replace direct MessageBox/OpenDialog/SaveDialog/SaveChangesDialog construction with service requests and observation.
- Modify `guideXOS/DefaultApps/ComputerFiles.cs`: route conceptual application/document/shell opens through Shell/Open service while retaining its internal filesystem browser.
- Modify `guideXOS/GUI/DisplayOptions.cs`: route background selection through Open File service while retaining color/effect controls.
- Modify `guideXOS/OS/ApplicationFactories.cs`: inject service access into Computer Files if its factory currently lacks it.
- Modify `guideXOS/OS/ApplicationInstance.cs`: extend the existing Phase 8 runtime diagnostic with Phase 9 request/session proofs.
- Modify `guideXOS/Program.cs`: emit bounded Phase 9 self-test/runtime markers without adding diagnostic applications to production Start.
- Modify `run_uefi_validation.ps1`: add a Phase 9 selector/marker path only if the existing selector structure cannot reuse AppModel/AppRuntime; retain all existing selectors.
- Modify `APP_MODEL_CONVERGENCE.md`: update status, Phase 9 audit/matrix/contracts/migrations/results/deferred scope after implementation.

## Task 1: Add the bounded Phase 9 contract vocabulary

**Files:**
- Modify: `guideXOS/OS/ApplicationServices.cs`
- Test: `guideXOS/OS/ApplicationServiceRegistry.cs` (RunSelfTest contract section)

**Interfaces:**
- Produces `ApplicationServiceId.Dialogs`, `OpenFile`, `SaveFile`, and `Shell`.
- Produces `ApplicationServiceResultCode.Cancelled`, `InvalidState`, `UnsupportedTarget`, `BackendFailure`, and explicit `Conflict`; retain existing `Unsupported` for Phase 8 compatibility.
- Produces bounded `ApplicationServiceRequestHandle`, `ApplicationServiceRequestState`, `ApplicationServiceRequestStatus`.
- Produces bounded `ApplicationDialogRequest/Kind/ButtonSet/Outcome`, `OpenFileRequest`, `SaveFileRequest`, and `ApplicationShellOpenRequest/TargetKind`.

- [ ] **Step 1: Write the failing contract self-test.**

Add deterministic assertions to `ApplicationServiceRegistry.RunSelfTest` that construct every new request at its exact maximum, reject one-character-over-bound values, distinguish `Conflict` from `InvalidContext`, and verify the dialog button/kind combinations. Reference the new types before adding them so the build fails because the Phase 9 contract is absent.

- [ ] **Step 2: Run the red test.**

Run: `.\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 300`.

Expected: build failure naming missing Phase 9 contract types/members, or an AppModel diagnostic failure naming the new contract assertion. Do not proceed on an unrelated build failure; correct the test setup first.

- [ ] **Step 3: Implement the minimal contract types.**

Extend the existing enums and add immutable bounded values. Constructors/factories copy or normalize only within the documented bound and expose `IsValid`; they never truncate user input. `ApplicationServiceRequestHandle` exposes only service ID, slot, generation, and validity. `ApplicationServiceRequestStatus` exposes state and a typed payload without any GUI/backend reference.

- [ ] **Step 4: Run the green test.**

Run the same AppModel command. Expected: `APP_MODEL_SERVICES_SELFTEST_OK=1`, the existing compatibility/lifecycle/taskbar/application-factory self-tests remain present, and the new contract assertions pass.

- [ ] **Step 5: Commit.**

Run: `git add guideXOS/OS/ApplicationServices.cs guideXOS/OS/ApplicationServiceRegistry.cs; git commit -m "feat: add Phase 9 service contracts"`.

## Task 2: Implement fixed request sessions and lifecycle separation

**Files:**
- Create: `guideXOS/OS/ApplicationServiceSessions.cs`
- Modify: `guideXOS/OS/ApplicationServiceRegistry.cs`
- Modify: `guideXOS/OS/ApplicationInstance.cs`
- Test: `guideXOS/OS/ApplicationServiceRegistry.cs` request-session section

**Interfaces:**
- `ApplicationServiceRegistry.BeginInteractiveRequest(context, serviceId, payload, out handle)`.
- `ApplicationServiceRegistry.BeginShellRequest(context, payload, out handle)`.
- `ApplicationServiceRegistry.TryObserveRequest(context, handle, out status)`.
- `ApplicationServiceRegistry.TryCancelRequest(context, handle)`.
- `ApplicationServiceRegistry.OnApplicationLifecycleChanged(instance, previous, current)`.
- `ApplicationServiceRegistry.OnApplicationTerminating(instance, reason)`.
- `ApplicationServiceSessionTable.CleanupForInstance(handle, reason)`.

- [ ] **Step 1: Write the failing request/lifecycle tests.**

Extend the deterministic self-test to create a real self-test application instance and assert:

    Running/Activated + valid request -> handle, Success
    second interactive request -> Conflict
    same handle observed while Pending -> Success + Pending
    Running -> Inactive, existing observe -> Success + Pending/Completed
    Inactive + new interactive request -> InvalidState
    Inactive -> Running, completed observe -> Success + original payload
    termination, then observe -> InvalidContext
    terminate + relaunch same slot -> old generation InvalidContext
    cancel existing handle -> Cancelled and deterministic release
    invalid handle/unknown slot -> InvalidRequest
    full fixed table -> ResourceUnavailable

Reference the session APIs before their implementation so the build is red for the intended missing behavior.

- [ ] **Step 2: Run the red test.**

Run the AppModel command from Task 1. Expected: compile errors for missing session APIs or a failing Phase 9 self-test assertion. The Phase 8 service assertions must still run far enough to show the failure is in the new request-session section.

- [ ] **Step 3: Implement the fixed session table.**

Use a fixed array of 16 session records in the registry/session helper. Each record stores service ID, owner `ApplicationInstanceHandle`, slot generation, request state, interactive classification, bounded payload/result storage, and transient-window metadata reference. Allocate the first free slot deterministically. Keep one active or unconsumed interactive session per owner; return `Conflict` for a second one. Shell sessions use the same bounded table and return `ResourceUnavailable` when no slot is available.

Implement separate validators:

    ValidateNewInteractiveContext -> only Running or Activated
    ValidateExistingRequestContext -> Running, Activated, or Inactive
    ValidateGeneration -> ApplicationInstanceRegistry.TryGet(handle)

Observation and cancellation must not require a new-request lifecycle state. Transition to Closing, Terminated, or Failed calls cleanup before the generation can be reused. Completed results remain in the slot through Inactive and are released only on consumption or deterministic cleanup.

- [ ] **Step 4: Wire lifecycle cleanup.**

Call `OnApplicationLifecycleChanged` from the existing lifecycle transition authority and `OnApplicationTerminating` from the existing termination path. Do not make `ApplicationServiceRegistry` authoritative for lifecycle; it only releases its sessions after the instance registry reports the transition.

- [ ] **Step 5: Run the green tests.**

Run the AppModel command again. Expected: all request/lifecycle assertions pass, `Conflict` is emitted for duplicate interactive requests, inactive observation succeeds, stale/reused-generation observation returns `InvalidContext`, and Phase 8 markers remain green.

- [ ] **Step 6: Commit.**

Run: `git add guideXOS/OS/ApplicationServiceSessions.cs guideXOS/OS/ApplicationServiceRegistry.cs guideXOS/OS/ApplicationInstance.cs; git commit -m "feat: add lifecycle-safe application service sessions"`.

## Task 3: Separate transient service windows from ordinary application content

**Files:**
- Modify: `guideXOS/GUI/Window.cs`
- Modify: `guideXOS/GUI/WindowManager.cs`
- Modify: `guideXOS/OS/ApplicationServiceSessions.cs`
- Modify: `guideXOS/OS/ApplicationInstance.cs`
- Test: `guideXOS/OS/ApplicationServiceRegistry.cs` plus existing application-window/taskbar self-tests

**Interfaces:**
- Internal `Window.ServiceSessionClassification` or equivalent bounded metadata, with no public Window reference in application service contracts.
- `WindowManager.RegisterTransientServiceWindow(window, owner, requestHandle)`.
- `WindowManager.ReleaseTransientServiceWindow(window)`.
- `ApplicationServiceSessionTable.TryGetTransientOwner(window, out metadata)`.

- [ ] **Step 1: Write the failing ownership tests.**

Add a self-test window registered as a service-session window and assert:

    ordinary owned-window count is unchanged
    final-window policy sees no extra content window
    TaskbarApplicationEntryRegistry sees no extra taskbar entry
    Task Manager application-window observation excludes the transient window
    semantic owner/request handle remains queryable by the session registry
    termination closes and releases the transient window
    orphan/transient count is zero after cleanup

Use the existing application instance and taskbar self-test counters instead of adding a second ownership authority.

- [ ] **Step 2: Run the red test.**

Run AppModel validation. Expected: the new classification assertion fails because the current ordinary `AttachWindow` path would count the probe window, or the new registration API is missing.

- [ ] **Step 3: Implement minimal transient metadata.**

Add an internal service-session classification recognized by `WindowManager` and the session table. Register the window in a fixed metadata array, set `ShowInTaskbar = false`, and keep it out of `ApplicationInstance.AttachWindow`, `OwnsWindow`, `ClearOwnedWindows`, ordinary final-window policy, and application-window enumeration. Retain graphical ownership in `WindowManager` so drawing/input/disposal continue through the existing path.

- [ ] **Step 4: Implement deterministic release.**

On completion/cancellation/termination, close or hide the window through its existing safe close path, remove the transient metadata, and allow normal WindowManager cleanup to dispose it. Releasing an already released window is idempotent and does not increment stale ownership counters.

- [ ] **Step 5: Run the green ownership tests.**

Run AppModel validation. Expected: ordinary content counts, final-window policy, taskbar grouping, and Task Manager observations remain unchanged; transient owner metadata is present only while the request is live; orphan and stale-owner counts remain zero.

- [ ] **Step 6: Commit.**

Run: `git add guideXOS/GUI/Window.cs guideXOS/GUI/WindowManager.cs guideXOS/OS/ApplicationServiceSessions.cs guideXOS/OS/ApplicationInstance.cs guideXOS/OS/ApplicationServiceRegistry.cs; git commit -m "feat: isolate service dialog window ownership"`.

## Task 4: Add dialog service and adapt existing C# dialog implementations

**Files:**
- Create: `guideXOS/OS/ApplicationDialogServices.cs`
- Modify: `guideXOS/OS/ApplicationServiceBackends.cs`
- Modify: `guideXOS/OS/ApplicationServiceRegistry.cs`
- Modify: `guideXOS/GUI/MessageBox.cs`
- Modify: `guideXOS/GUI/SaveChangesDialog.cs`
- Modify: `guideXOS/GUI/Desktop.cs` only for internal adapter hooks, not public application calls
- Test: `guideXOS/OS/ApplicationServiceRegistry.cs` dialog section

**Interfaces:**
- `ApplicationDialogService.Begin(context, ApplicationDialogRequest)` returns `ApplicationServiceResult<ApplicationServiceRequestHandle>`.
- `ApplicationDialogService.Observe(context, handle)` returns `ApplicationServiceResult<ApplicationServiceRequestStatus<ApplicationDialogResult>>`.
- `ApplicationDialogService.Cancel(context, handle)` returns `ApplicationServiceResult`.
- Internal `CSharpApplicationDialogService` creates/adapts Window subclasses and posts bounded completion to the session table.

- [ ] **Step 1: Write failing dialog tests.**

Add deterministic tests for Information, Error, and Confirmation request validation; Accepted/Rejected/Cancelled/Closed/BackendFailure mapping; create-state rejection; duplicate `Conflict`; close/Escape completion; inactive observation; and termination cleanup. The test must obtain the dialog through `ApplicationServiceAccess.Dialogs`, never by constructing a GUI class.

- [ ] **Step 2: Run the red test.**

Run AppModel validation. Expected: missing `Dialogs` access/backend or a failing dialog self-test assertion.

- [ ] **Step 3: Implement the service surface and adapter.**

Register `Dialogs`, validate request and new-request lifecycle, allocate a session, and adapt existing message/save-changes windows. Keep callbacks internal to the adapter; callbacks only complete the fixed session and release transient metadata. Set bounded title/body text, map acknowledgement to `Accepted`, Save to `Accepted`, Don’t Save to `Rejected`, Escape/cancel/close to the approved outcome, and backend construction errors to `BackendFailure`.

Use existing WindowManager z-order/input traversal. Do not add a global modal manager, spin loop, or lock-held wait. Click-away has no implicit result. Keep parent application lifecycle valid and leave other applications usable.

- [ ] **Step 4: Fix deterministic close/cancel paths.**

Add the smallest internal hooks needed so Escape, explicit Cancel, window close, and requester termination each complete the session exactly once. Preserve existing visual behavior and avoid changing shell-internal callers that are not yet migrated.

- [ ] **Step 5: Run green dialog tests and Phase 8 regression.**

Run: `.\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 300` and `.\run_uefi_validation.ps1 -AppRuntime -TimeoutSeconds 300`.

Expected: dialog self-tests pass, `ORPHAN_DIALOG_COUNT=0` or equivalent is emitted, Phase 8 service runtime markers remain green, compatibility fallback and legacy backend counters remain zero.

- [ ] **Step 6: Commit.**

Run: `git add guideXOS/OS/ApplicationDialogServices.cs guideXOS/OS/ApplicationServiceBackends.cs guideXOS/OS/ApplicationServiceRegistry.cs guideXOS/GUI/MessageBox.cs guideXOS/GUI/SaveChangesDialog.cs guideXOS/GUI/Desktop.cs guideXOS/GUI/Window.cs guideXOS/GUI/WindowManager.cs; git commit -m "feat: add asynchronous dialog service"`.

## Task 5: Add Open File and Save File services

**Files:**
- Create: `guideXOS/OS/ApplicationFileDialogServices.cs`
- Modify: `guideXOS/OS/ApplicationServiceBackends.cs`
- Modify: `guideXOS/OS/ApplicationServiceRegistry.cs`
- Modify: `guideXOS/GUI/OpenDialog.cs`
- Modify: `guideXOS/GUI/SaveDialog.cs`
- Test: `guideXOS/OS/ApplicationServiceRegistry.cs` file-dialog section

**Interfaces:**
- `ApplicationOpenFileService.Begin(context, OpenFileRequest)` returns a request handle.
- `ApplicationOpenFileService.Observe(context, handle)` returns bounded selected-path status.
- `ApplicationOpenFileService.Cancel(context, handle)` cancels deterministically.
- `ApplicationSaveFileService.Begin(context, SaveFileRequest)` returns a request handle.
- `ApplicationSaveFileService.Observe(context, handle)` returns bounded destination-path status.
- `ApplicationSaveFileService.Cancel(context, handle)` cancels deterministically.

- [ ] **Step 1: Write failing file-dialog tests.**

Add tests for exact title/path/filename bounds, valid starting location propagation, selected path propagation, open cancel, save cancel, invalid request, backend failure, and no overwrite-confirmation invention. Verify the result payload never contains `OpenDialog`, `SaveDialog`, `FileInfo`, or a callback.

- [ ] **Step 2: Run the red test.**

Run AppModel validation. Expected: missing Open/Save service members or a failing file-dialog assertion.

- [ ] **Step 3: Implement bounded Open/Save adapters.**

Register both services. Adapt existing dialogs internally, register each window as transient service-session metadata, and complete the request on selection/cancel/close. Preserve directory navigation and filesystem behavior. Open returns files only. Save preserves current `.txt` extension behavior and suggested filename handling. Reject root-only/empty invalid destinations with `InvalidRequest`; map unavailable enumeration/backend failures to `ResourceUnavailable` or `BackendFailure`.

- [ ] **Step 4: Make Save Escape/close complete cancellation.**

Add a single idempotent cancel hook to `SaveDialog`; no overwrite confirmation is added. Ensure the adapter releases transient session window for button cancel and Escape/window close.

- [ ] **Step 5: Run green tests.**

Run AppModel validation and focused AppRuntime validation. Expected: Open/Save contract tests pass, selected bounded paths are returned, cancel leaves no completed selection, and orphan/stale service-session counters remain zero.

- [ ] **Step 6: Commit.**

Run: `git add guideXOS/OS/ApplicationFileDialogServices.cs guideXOS/OS/ApplicationServiceBackends.cs guideXOS/OS/ApplicationServiceRegistry.cs guideXOS/GUI/OpenDialog.cs guideXOS/GUI/SaveDialog.cs; git commit -m "feat: add bounded open and save file services"`.

## Task 6: Add Shell/Open service using the existing modern App Model

**Files:**
- Create: `guideXOS/OS/ApplicationShellServices.cs`
- Modify: `guideXOS/OS/ApplicationServiceRegistry.cs`
- Modify: `guideXOS/OS/ApplicationServiceBackends.cs`
- Modify: `guideXOS/OS/ModernAppModel.cs` only if a small result/adapter seam is required; preserve existing launch semantics
- Test: `guideXOS/OS/ApplicationServiceRegistry.cs` Shell/Open section

**Interfaces:**
- `ApplicationShellService.Begin(context, ApplicationShellOpenRequest)` returns a request handle.
- `ApplicationShellService.Observe(context, handle)` returns bounded shell result status.
- `ApplicationShellService.Cancel(context, handle)` returns `InvalidState` after non-cancellable dispatch has completed.
- Internal mapping targets `LaunchRequest.ForAppId`, `ForName`, `ForFile`, `ForShellObject`, or the existing typed shell-action adapter.

- [ ] **Step 1: Write failing Shell/Open tests.**

Add deterministic tests for registered application ID launch, permitted alias launch, associated document open, shell-object routing, typed shell action routing, malformed target, not-found target, unsupported target, permission-denied result preservation, backend failure, and no compatibility/legacy increment. Assert the service calls the existing modern App Model adapter rather than a second launcher.

- [ ] **Step 2: Run the red test.**

Run AppModel validation. Expected: missing Shell service or failing Shell/Open assertions.

- [ ] **Step 3: Implement typed target mapping.**

Validate bounded targets, allocate a fixed request session, create the existing `LaunchRequest`, and dispatch through `ApplicationFactoryRegistry`/modern launch and association adapters. Map `LaunchErrorCode` to service codes without flattening `NotFound`, `UnsupportedTarget`, `PermissionDenied`, `ResourceUnavailable`, `InvalidRequest`, `InvalidState`, and `BackendFailure`. Preserve launched application ID and generation-safe instance handle in the bounded result.

- [ ] **Step 4: Run green Shell/Open tests.**

Run AppModel and AppRuntime validation. Expected: launch/open/action tests pass, existing App Model factory paths remain authoritative, compatibility fallback count is zero, legacy backend count is zero, and stale ownership remains zero.

- [ ] **Step 5: Commit.**

Run: `git add guideXOS/OS/ApplicationShellServices.cs guideXOS/OS/ApplicationServiceRegistry.cs guideXOS/OS/ApplicationServiceBackends.cs guideXOS/OS/ModernAppModel.cs; git commit -m "feat: add typed shell open service"`.

## Task 7: Migrate Notepad and prove end-to-end dialog/file flows

**Files:**
- Modify: `guideXOS/DefaultApps/Notepad.cs`
- Modify: `guideXOS/OS/ApplicationFactories.cs` only if service injection signatures need the existing factory binding update
- Modify: `guideXOS/OS/ApplicationInstance.cs` runtime diagnostic
- Modify: `guideXOS/Program.cs` runtime marker output
- Test: `guideXOS/OS/ApplicationInstance.cs` Phase 9 runtime diagnostic plus AppRuntime workload

**Interfaces:**
- Notepad stores only `ApplicationServiceContext`, `ApplicationServiceAccess`, and bounded request handles.
- Notepad uses `Dialogs`, `OpenFile`, and `SaveFile`; it retains direct `File.ReadAllBytes`/`File.WriteAllBytes` document persistence.

- [ ] **Step 1: Write failing migration/runtime assertions.**

Add a diagnostic fixture/assertion that launches two Notepad instances, requests Open/Save/confirmation through service access, and checks the result is applied only to the originating instance. Assert source no longer constructs `MessageBox`, `OpenDialog`, `SaveDialog`, or `SaveChangesDialog` and that direct filesystem calls remain for document bytes. Add Open success, Open cancel, Save success/cancel, confirmation result, title/filename, dirty-state, and cleanup markers.

- [ ] **Step 2: Run the red diagnostic.**

Run AppRuntime validation. Expected: the new marker assertions fail or compile references to new service members are absent; existing Phase 8 runtime markers should still identify the baseline path.

- [ ] **Step 3: Migrate Notepad request flows.**

Replace direct dialog fields/construction with request handles and a small per-instance observation method called from the existing application update/input path. Keep document state, undo/redo, dirty state, wrap setting, and multiple-instance behavior unchanged. When a request is pending, suppress only the originating Notepad’s conflicting commands; do not suppress other applications. Apply a path/result only after validating the originating context and request handle.

- [ ] **Step 4: Add bounded Notepad diagnostic workload.**

Use the existing runtime diagnostic mechanism to request a deterministic open path and save destination where the filesystem fixture is available, exercise cancellation, and exercise dirty close confirmation. Emit markers for requester identity, selected path, cancel, save state, dialog cleanup, and no extra instance creation. Do not add a production Start-visible diagnostic application.

- [ ] **Step 5: Run green Notepad proof.**

Run AppRuntime validation. Expected: open success and cancel, save success or deterministic bounded destination, confirmation result, filename/title and dirty-state updates, no extra instance, no orphan dialog, and no stale service context. Phase 8 runtime service markers must remain green.

- [ ] **Step 6: Commit.**

Run: `git add guideXOS/DefaultApps/Notepad.cs guideXOS/OS/ApplicationFactories.cs guideXOS/OS/ApplicationInstance.cs guideXOS/Program.cs; git commit -m "feat: migrate Notepad to dialog and file services"`.

## Task 8: Migrate Computer Files and Display Options

**Files:**
- Modify: `guideXOS/DefaultApps/ComputerFiles.cs`
- Modify: `guideXOS/GUI/DisplayOptions.cs`
- Modify: `guideXOS/OS/ApplicationFactories.cs`
- Test: `guideXOS/OS/ApplicationInstance.cs` runtime diagnostic and existing AppRuntime file/shell workload

**Interfaces:**
- Computer Files receives existing service context/access and uses only Shell/Open service for conceptual application/document/shell operations.
- Display Options uses Open File service for background selection and keeps color/effect controls direct.

- [ ] **Step 1: Write failing migration assertions.**

Add source/runtime assertions that Computer Files shell/document actions cross `ApplicationServiceAccess.Shell`, Display Options background selection crosses `OpenFile`, and internal filesystem enumeration/color/effect behavior remains unchanged. Assert shell launch result identity and background path propagation.

- [ ] **Step 2: Run the red tests.**

Run AppRuntime validation. Expected: migration markers are absent/failing while old direct routes remain.

- [ ] **Step 3: Implement the two migrations.**

Inject services through the existing factory pattern. Route only application-facing shell/document requests through `Shell`; retain `Desktop`/filesystem use where the application implements its own browser. Replace Display Options’ direct `OpenDialog` construction with `OpenFile` request/observe and preserve background decode/update behavior.

- [ ] **Step 4: Run green focused validation.**

Run AppRuntime validation with the existing Computer Files association/shell workload and AppModel validation. Expected: shell routing and background selection pass, no extra app instance is created by the service layer, and compatibility/legacy counters remain zero.

- [ ] **Step 5: Commit.**

Run: `git add guideXOS/DefaultApps/ComputerFiles.cs guideXOS/GUI/DisplayOptions.cs guideXOS/OS/ApplicationFactories.cs guideXOS/OS/ApplicationInstance.cs guideXOS/Program.cs; git commit -m "feat: migrate shell and background workflows to services"`.

## Task 9: Add Phase 9 runtime diagnostics and canonical convergence documentation

**Files:**
- Modify: `guideXOS/OS/ApplicationInstance.cs`
- Modify: `guideXOS/Program.cs`
- Modify: `run_uefi_validation.ps1` only if a dedicated Phase9 selector is required by existing validation contract
- Modify: `APP_MODEL_CONVERGENCE.md`
- Test: AppModel/AppRuntime and documentation/source audit

- [ ] **Step 1: Write failing diagnostic-gate assertions.**

Extend the bounded diagnostic to require:

    APP_MODEL_SERVICES_SELFTEST_OK=1
    PHASE9_DIALOG_SELFTEST_OK=1
    PHASE9_FILE_SERVICE_SELFTEST_OK=1
    PHASE9_SHELL_SERVICE_SELFTEST_OK=1
    PHASE9_RUNTIME_OK=1
    PHASE9_OPEN_SUCCESS=1
    PHASE9_OPEN_CANCEL=1
    PHASE9_SAVE_RESULT=success-or-bounded-cancel
    PHASE9_CONFIRMATION_RESULT=accepted-or-rejected-or-cancelled
    PHASE9_SHELL_LAUNCH=1
    PHASE9_DOCUMENT_OPEN=1
    PHASE9_ORPHAN_DIALOG_COUNT=0
    PHASE9_STALE_SERVICE_CONTEXT_COUNT=0

Also require compatibility fallback 0, legacy backend 0, stale instances/ownership/taskbar owners 0, allocator corruption 0, `ThreadPool.Locked=0`, valid graphics, balanced input, frame/timer progress, no panic, and no CPU fault.

- [ ] **Step 2: Run the red diagnostic gate.**

Run: `.\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 300` and `.\run_uefi_validation.ps1 -AppRuntime -TimeoutSeconds 300`.

Expected: the new Phase 9 marker requirements fail until all service self-tests and runtime proofs are wired.

- [ ] **Step 3: Implement bounded diagnostics.**

Reuse real application instances and existing marker conventions. Keep diagnostic-only request paths out of production Start. Emit exact bounded values, use no unbounded concatenation in hot loops, and make cleanup markers observable after requester termination and result consumption.

- [ ] **Step 4: Update the canonical convergence matrix.**

Change the canonical status from Phase 7 to Phase 9 once final gates pass. Add required rows for informational dialog, error dialog, confirmation dialog, open file, save file, open document, launch application, shell object/action, and Open-with (deferred by Server v1 scope). For each row record Server source, C# source, classification, shared semantics, backend differences, migration value, risk, and decision. Document ownership, create/observe lifecycle, modal semantics, bounds, capability policy, migrations, runtime results, and deferred services. Distinguish platform contract, C# backend, and Server backend.

- [ ] **Step 5: Run green diagnostics and documentation checks.**

Run both diagnostic commands and `git diff --check`. Expected: all new markers pass, all Phase 8 markers pass, canonical documentation contains no stale Phase 7 status, and no Server/Legacy path appears in the diff.

- [ ] **Step 6: Commit.**

Run: `git add guideXOS/OS/ApplicationInstance.cs guideXOS/Program.cs run_uefi_validation.ps1 APP_MODEL_CONVERGENCE.md; git commit -m "test: add Phase 9 service diagnostics and convergence record"`.

## Task 10: Run the complete regression matrix and perform final review

**Files:**
- No planned source changes; fix only findings with a new red test and a focused commit.
- Review: all commits from the Phase 9 baseline through `HEAD`.

- [ ] **Step 1: Run the required focused matrix.**

Run each command with fresh output captured in the repository’s existing serial-log convention:

    .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 300
    .\run_uefi_validation.ps1 -AppRuntime -TimeoutSeconds 300
    .\run_uefi_validation.ps1 -NativeInput -TimeoutSeconds 300
    .\run_uefi_validation.ps1 -ContextMenu -TimeoutSeconds 300
    .\run_uefi_validation.ps1 -Continuous -TimeoutSeconds 300
    .\run_uefi_validation.ps1 -Frames 300 -TimeoutSeconds 300

Run focused Notepad/Computer Files validation using the existing AppRuntime workload and inspect serial markers for both applications. Run the host build path used by `run_uefi_validation.ps1` independently if the matrix did not rebuild it.

- [ ] **Step 2: Compare every mandatory gate to output.**

Record exact results for host build, AppModel, compatibility self-test, lifecycle self-test, taskbar/grouping self-test, Phase 8 self-test/runtime diagnostic, Phase 9 self-test/runtime diagnostic, AppRuntime, NativeInput, ContextMenu, and production Continuous. Verify orphan dialogs, stale service contexts, stale instances/ownership/taskbar owners, allocator status, ThreadPool lock status, graphics/input status, compatibility fallback, and legacy backend count.

- [ ] **Step 3: Run final diff and repository protection checks.**

Run:

    git diff --check
    git status --short --branch
    git diff --name-only c5d159420f2071cd4a6eca493ab27fc858a74db5..HEAD

Expected: only guideXOSUEFI files and the committed design/plan/docs are changed; Server and Legacy remain untouched; no generated serial/build artifact is staged.

- [ ] **Step 4: Perform the required final review.**

Review the complete branch against the spec, plan Review Focus, and all recorded test output. Re-grade any issue by user-visible effect. Any Critical/Important finding gets a new failing test, minimal fix, full focused suite, and one commit. A Minor finding is recorded as deferred rather than silently changed. Record every plan conflict or implementation ruling with its cost if wrong.

- [ ] **Step 5: Commit only verified fixes.**

Every fix must pass `git diff --check` and the relevant focused command before commit. Do not claim completion from a diff alone.

- [ ] **Step 6: Prepare the final Phase 9 report.**

Use the user’s requested 52-field report. Include starting/ending HEAD and subjects, commit/push status, exact files changed, all audit/convergence details, every diagnostic result, all mandatory gates, Server/Legacy modification status, future IPC/Ring 3 assessment, and recommended Phase 10. State any remaining blocker as Outcome B/C/D/E rather than claiming Outcome A without fresh evidence.



