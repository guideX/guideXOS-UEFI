# Application-Centric Taskbar Phase 7 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add bounded ApplicationInstance-keyed taskbar grouping and instance-aware activation/close semantics while preserving separate visual Window buttons.

**Architecture:** `ApplicationInstanceRegistry` remains authoritative for instance lifecycle and bounded Window ownership. A new `TaskbarApplicationEntryRegistry` rebuilds a bounded read-only projection keyed by `ApplicationInstanceHandle`; `Taskbar` keeps enumerating the existing WindowManager z-order and resolves each button to that projection. Explicit focus helpers separate application activation from Window focus so same-instance switching is lifecycle-idempotent.

**Tech Stack:** C# NativeAOT kernel, existing `WindowManager`/`Taskbar` shell, fixed arrays and bounded self-tests, UEFI serial diagnostics, PowerShell build/QEMU scripts.

**Spec:** `docs/superpowers/specs/2026-09-16-application-centric-taskbar-phase-7-design.md`

## Global Constraints

- `ApplicationInstanceRegistry` remains authoritative for instance existence, lifecycle, and Window ownership.
- `TaskbarApplicationEntry` is a read-only projection/cache keyed by generation-safe `ApplicationInstanceHandle`.
- One instance has one semantic taskbar group; its Windows may remain separate visual buttons.
- Same-instance Window switching must not create application deactivate/activate churn.
- Cross-instance switching must perform proper application deactivation/activation.
- Reusable zero-window instances remain valid but normally produce no visible taskbar entry.
- Taskbar application Close, where routed, uses Phase 6 application-level close-request semantics.
- All grouping structures are bounded by the 32-instance registry and 8-window instance limits.
- Task Manager remains observational and must not mutate lifecycle, ownership, or focus.
- Production logging remains disabled; conditional diagnostics are allowed.
- Server and Legacy trees are read-only and must not be modified.
- Preserve compatibility fallback `0`, legacy backend `0`, allocator corruption `0`, `ThreadPool.Locked=0`, valid graphics, and no unexpected input drops.

## File Map

- Create: `guideXOS/GUI/TaskbarApplicationEntry.cs` — bounded projection entries, reconciliation, routing facade, counters, and Phase 7 self-test.
- Modify: `guideXOS/OS/ApplicationInstance.cs` — instance-aware Window observation, target activation support, and lifecycle/grouping diagnostics integration.
- Modify: `guideXOS/GUI/Taskbar.cs` — projection reconciliation, semantic button lookup, active-group state, and application Close routing hook while keeping visual buttons separate.
- Modify: `guideXOS/GUI/WindowManager.cs` — explicit Window focus helper and stale projection cleanup integration; retain raw Window enumeration.
- Modify: `guideXOS/GUI/Window.cs` — notify the explicit focus path on a real user Window press without converting arbitrary z-order changes into lifecycle transitions.
- Modify: `guideXOS/GUI/StartMenu.cs` — route existing recent/Window activation through the same semantic focus helper when the Window is instance-owned.
- Modify: `guideXOS/DefaultApps/TaskManager.cs` — add read-only instance observation to existing diagnostics without changing the UI model or mutating state.
- Modify: `guideXOS/Program.cs` — run the grouping self-test in the AppModel diagnostic, emit bounded counters, and add runtime grouping proof markers.
- Modify: `APP_MODEL_CONVERGENCE.md` — document Phase 7 semantics, bounds, proofs, and deferred visual grouping.

---

### Task 1: Add failing Phase 7 grouping contract tests

**Files:**
- Modify: `guideXOS/Program.cs`
- Modify: `guideXOS/OS/ApplicationInstance.cs`
- Create: `guideXOS/GUI/TaskbarApplicationEntry.cs`

**Interfaces:**
- Produces the first failing contract calls for `TaskbarApplicationEntryRegistry.RunSelfTest()`, `TaskbarApplicationEntryRegistry.Reconcile()`, `TaskbarApplicationEntryRegistry.EntryCount`, `TryGetForWindow`, and `TryFocusWindow`.
- Uses the existing `ApplicationInstanceRegistry` lifecycle and `Window` ownership APIs.

- [ ] **Step 1: Add the AppModel diagnostic call before implementation**

Add the following branch to `RenderLoopUefiAppModelDiagnostic`, immediately after the Phase 6 lifecycle self-test and before factory validation:

```csharp
} else if (!TaskbarApplicationEntryRegistry.RunSelfTest()) {
    failure = "TASKBAR_APPLICATION_GROUPING";
```

Add the corresponding `TaskbarApplicationEntryRegistry` type reference without implementing the type yet.

- [ ] **Step 2: Build to verify the test gate fails for the missing production contract**

Run:

```powershell
dotnet publish guideXOS\guideXOS.csproj -c Release --no-restore -p:SkipISO=true
```

Expected: compilation fails because `TaskbarApplicationEntryRegistry` and its `RunSelfTest` contract do not yet exist. This is the intentional RED state; do not proceed on an unrelated compiler failure.

- [ ] **Step 3: Commit the failing test gate**

```powershell
git add guideXOS\Program.cs
git commit -m "test: add Phase 7 taskbar grouping gate"
```

The commit may contain only the diagnostic call and must not alter Server or Legacy.

---

### Task 2: Implement the bounded semantic projection and reconciliation

**Files:**
- Create: `guideXOS/GUI/TaskbarApplicationEntry.cs`
- Modify: `guideXOS/OS/ApplicationInstance.cs`

**Interfaces:**
- `TaskbarApplicationEntry` exposes read-only `InstanceHandle`, `DescriptorId`, `DisplayName`, `ResourceKey`, `LifecycleState`, `IsActive`, `IsSuspended`, `OwnedWindowCount`, `PresentableWindowCount`, `ActiveWindow`, and `MostRecentWindow` properties.
- `TaskbarApplicationEntryRegistry` exposes `Capacity`, `EntryCount`, bounded counters, `Initialize()`, `Reconcile()`, `GetAt(int)`, `TryGet(ApplicationInstanceHandle, out TaskbarApplicationEntry)`, `TryGetForWindow(Window, out TaskbarApplicationEntry)`, `TryFocusWindow(ApplicationInstanceHandle, Window, out ApplicationLifecycleResult)`, `RequestApplicationClose(ApplicationInstanceHandle)`, and `RunSelfTest()`.
- `ApplicationInstanceRegistry` exposes a read-only observation path and an internal target-window validation/focus primitive used by the projection.

- [ ] **Step 1: Write the failing projection assertions**

In `RunSelfTest`, define checks for these exact behaviors before writing the implementation body:

```csharp
Check(oneInstanceOneWindowEntry, "one instance one Window", ref passed, ref failed, ref firstFailure);
Check(multiWindowSingleEntry, "one instance multiple Windows one group", ref passed, ref failed, ref firstFailure);
Check(sameInstanceSwitchNoLifecycleChurn, "same instance Window switch", ref passed, ref failed, ref firstFailure);
Check(closeOneRetainsGroup, "close one Window retains group", ref passed, ref failed, ref firstFailure);
Check(closeFinalAppliesPolicy, "close final Window policy", ref passed, ref failed, ref firstFailure);
Check(zeroWindowReusableSuppressed, "reusable zero-window suppression", ref passed, ref failed, ref firstFailure);
Check(twoSameDescriptorEntriesIndependent, "same descriptor separate instances", ref passed, ref failed, ref firstFailure);
Check(staleEntryRejected, "stale taskbar entry", ref passed, ref failed, ref firstFailure);
Check(cleanup, "taskbar grouping cleanup", ref passed, ref failed, ref firstFailure);
```

Use fixed diagnostic Window fixtures only inside the self-test. Do not register a production Start-menu application for the fixture.

- [ ] **Step 2: Run the AppModel diagnostic/build to verify the new assertions fail**

Run the focused compile/diagnostic command from Task 1. Expected: the new contract is still incomplete and the grouping self-test cannot pass.

- [ ] **Step 3: Implement the fixed-size entry and registry**

Use `TaskbarApplicationEntry[ApplicationInstanceRegistry.Capacity]` and a fixed `Window[ApplicationInstance.MaxOwnedWindows]` per entry. Reconcile by scanning `ApplicationInstanceRegistry.GetAt(i)` and each instance's bounded owned-window accessors. Never allocate a `List`, dictionary, or new application instance during reconciliation.

For each live instance:

1. resolve descriptor display/resource identity through `ApplicationDescriptorRegistry` when available;
2. count owned and presentable taskbar Windows (`Visible && ShowInTaskbar`), retaining minimized-but-visible Windows as presentable;
3. suppress the entry when no presentable Window exists unless an explicit future policy allows zero-window visibility;
4. reuse an entry for the same generation-safe handle or create one in a free slot;
5. remove entries whose handle is no longer registered or whose instance is terminal;
6. update active/suspended snapshots and select current/most-recent/first valid Window deterministically;
7. record bounded attach/detach, create/remove/reuse, zero-window suppression, stale-owner, multi-window, and maximum-group-size counters.

- [ ] **Step 4: Add read-only instance observation**

Add a value-type observation surface in `ApplicationInstance.cs` with:

```csharp
public readonly struct ApplicationInstanceObservation {
    public ApplicationInstanceHandle Handle { get; }
    public string DescriptorId { get; }
    public ApplicationInstanceLifecycleState LifecycleState { get; }
    public bool IsActivated { get; }
    public bool IsSuspended { get; }
    public int OwnedWindowCount { get; }
}

public static int ObservationCount { get; }
public static bool TryGetObservationAt(
    int ordinal, out ApplicationInstanceObservation observation);
```

Enumerate only live registry slots, preserve registry order, and return false for invalid ordinals. No observation method may activate, deactivate, attach, detach, or create an instance.

- [ ] **Step 5: Run the focused build and AppModel self-test for GREEN**

Run:

```powershell
dotnet publish guideXOS\guideXOS.csproj -c Release --no-restore -p:SkipISO=true
```

Then run the existing UEFI `AppModel` diagnostic path and inspect the serial marker matching `TASKBAR_GROUPING_SELFTEST=passed=<positive>;failed=0;result=PASS` plus zero cleanup counters. Expected: Task 1 and Task 2 behavior pass while pre-existing AppModel checks remain unchanged.

- [ ] **Step 6: Commit the projection contract**

```powershell
git add guideXOS\GUI\TaskbarApplicationEntry.cs guideXOS\OS\ApplicationInstance.cs
git commit -m "feat: add bounded application taskbar projection"
```

---

### Task 3: Add instance-aware activation, Window focus, and close routing

**Files:**
- Modify: `guideXOS/GUI/TaskbarApplicationEntry.cs`
- Modify: `guideXOS/OS/ApplicationInstance.cs`
- Modify: `guideXOS/GUI/WindowManager.cs`
- Modify: `guideXOS/GUI/Window.cs`
- Modify: `guideXOS/GUI/StartMenu.cs`

**Interfaces:**
- `TaskbarApplicationEntryRegistry.TryFocusWindow` validates ownership, calls the existing lifecycle activation/resume path, then focuses the requested Window and records selection.
- `WindowManager.FocusWindow(Window)` is the explicit Window-level focus entry point. Attached Windows route through the semantic projection; unattached compatibility Windows retain direct z-order behavior.
- `ApplicationInstanceRegistry` keeps lifecycle activation idempotent for an already active handle and exposes only the bounded target-window validation needed by the focus route.

- [ ] **Step 1: Extend the self-test with suspended, cross-instance, and close-request assertions**

Add checks for:

```csharp
Check(suspendedActivationResumes, "suspended activation resumes", ref passed, ref failed, ref firstFailure);
Check(crossInstanceSwitchesLifecycle, "cross-instance switch", ref passed, ref failed, ref firstFailure);
Check(applicationCloseTargetsInstance, "application-level close", ref passed, ref failed, ref firstFailure);
Check(closeCancellationLeavesState, "application close cancellation", ref passed, ref failed, ref firstFailure);
Check(unsupportedResumeIsBounded, "unsupported resume", ref passed, ref failed, ref firstFailure);
```

Capture activation/deactivation counters before same-instance selection and assert they do not change. Capture them before cross-instance selection and assert exactly one prior-instance deactivation and one target activation occur.

- [ ] **Step 2: Run the grouping self-test to verify these new assertions fail**

Run the focused AppModel diagnostic. Expected: the new activation/focus behavior is not yet implemented, producing a targeted failure rather than a compiler error.

- [ ] **Step 3: Implement target Window selection and same-instance idempotence**

`TryFocusWindow(handle, window, out result)` must:

1. reject an invalid/stale handle or Window not owned by the instance;
2. call `ApplicationInstanceRegistry.Activate(handle)`, which resumes suspended supported instances first;
3. leave the active handle unchanged when the handle is already active;
4. call `WindowManager.MoveToEnd(window)` only after lifecycle success;
5. update `ActiveWindow` and `MostRecentWindow` in the projection;
6. increment same-instance or cross-instance counters without creating lifecycle churn;
7. return the existing bounded `ApplicationLifecycleResult` for unsupported resume, stale handle, invalid state, or callback failure.

When a button has no valid explicit target, use the approved policy: current active owned Window, then most recently active valid Window, then first valid presentable Window.

- [ ] **Step 4: Add explicit Window focus without changing arbitrary z-order calls**

Implement `WindowManager.FocusWindow(Window)` as the only generic user-focus route. In `Window.OnInput`, on the first left-button press while the Window is under the pointer, call `FocusWindow(this)` before continuing existing title-button/content behavior. Do not change `MoveToEnd` itself into an implicit lifecycle operation.

Update the existing StartMenu Window selection path to call `WindowManager.FocusWindow(window)` when the Window is instance-owned; keep direct behavior for unattached shell compatibility Windows.

- [ ] **Step 5: Implement the taskbar application Close route**

Implement `TaskbarApplicationEntryRegistry.RequestApplicationClose(handle)` as a thin call to:

```csharp
return ApplicationInstanceRegistry.RequestClose(
    handle, ApplicationCloseReason.ShellRequest);
```

Do not add a new visual menu/picker in this phase. The route must be available to future taskbar context actions and exercised directly by the deterministic/runtime proof. Existing Window title-bar close remains Window-local.

- [ ] **Step 6: Run focused GREEN verification and commit**

Run the focused build and AppModel diagnostic, then commit:

```powershell
git add guideXOS\GUI\TaskbarApplicationEntry.cs guideXOS\OS\ApplicationInstance.cs guideXOS\GUI\WindowManager.cs guideXOS\GUI\Window.cs guideXOS\GUI\StartMenu.cs
git commit -m "feat: route taskbar focus through application instances"
```

---

### Task 4: Integrate the projection into the existing taskbar renderer

**Files:**
- Modify: `guideXOS/GUI/Taskbar.cs`
- Modify: `guideXOS/GUI/WindowManager.cs`

**Interfaces:**
- `Taskbar.DrawUEFITaskBar` and the Legacy taskbar path call `TaskbarApplicationEntryRegistry.Reconcile()` before presentation work.
- Each existing Window button resolves through `TryGetForWindow`; attached stale/terminal owners are skipped and recorded, while unattached compatibility Windows retain current behavior.

- [ ] **Step 1: Add a failing renderer-level assertion to the self-test**

Assert that after reconciliation:

```csharp
entry.OwnedWindowCount == authoritativeInstance.OwnedWindowCount;
entry.PresentableWindowCount == visibleTaskbarWindowCount;
TaskbarApplicationEntryRegistry.EntryCount == expectedSemanticGroupCount;
```

Also assert two Calculator-like instances produce two entries even when their descriptor IDs match.

- [ ] **Step 2: Replace raw attached-window validation with projection lookup**

In the existing Legacy button loop, retain the loop, dimensions, icon/title drawing, pinned row, clock, and ordering. Replace only the ownership gate and activation body:

```csharp
TaskbarApplicationEntry entry;
bool semantic = TaskbarApplicationEntryRegistry.TryGetForWindow(w, out entry);
if (!semantic && w.ApplicationInstanceHandle.IsValid) continue;
```

On click, call `TryFocusWindow(entry.InstanceHandle, w, out lifecycle)` for semantic Windows, then preserve existing restore/visibility/z-order behavior. Keep raw behavior for unattached compatibility Windows.

- [ ] **Step 3: Make active state application-centric with minimal visual change**

Use `entry.IsActive` for the existing button fill/border decision. All buttons for an active instance may share the active-group treatment; the selected Window remains projection state. Do not introduce a new icon, picker, thumbnail, or taskbar layout.

- [ ] **Step 4: Reconcile stale groups during Window cleanup**

After `WindowManager.CleanupClosedWindows` detaches/disposes closed Windows, call the projection reconcile path once. Ensure terminal/failed instance removal cannot leave an entry and ensure reusable zero-window instances are suppressed rather than removed from the authoritative registry.

- [ ] **Step 5: Run AppModel and taskbar context smoke checks**

Run the AppModel UEFI diagnostic and the existing ContextMenu diagnostic. Confirm taskbar context-menu opening/dismissal markers remain present and no projection counter reports stale owners or group leaks.

- [ ] **Step 6: Commit the renderer integration**

```powershell
git add guideXOS\GUI\Taskbar.cs guideXOS\GUI\WindowManager.cs
git commit -m "feat: project application groups into taskbar buttons"
```

---

### Task 5: Add Task Manager observation and runtime grouping proofs

**Files:**
- Modify: `guideXOS/DefaultApps/TaskManager.cs`
- Modify: `guideXOS/Program.cs`
- Modify: `guideXOS/OS/ApplicationInstance.cs`
- Modify: `guideXOS/GUI/TaskbarApplicationEntry.cs`

**Interfaces:**
- Task Manager consumes `ApplicationInstanceRegistry.ObservationCount` and `TryGetObservationAt` only for read-only diagnostics/summary.
- Program emits `APP_MODEL_TASKBAR_*` markers under `UEFI_DIAGNOSTIC_APP_MODEL` and `APP_RUNTIME_TASKBAR_*` markers under `UEFI_DIAGNOSTIC_APP_RUNTIME`.

- [ ] **Step 1: Add failing runtime proof assertions**

Add a bounded `RunTaskbarGroupingRuntimeDiagnostic()` that asserts:

```csharp
oneInstanceTwoWindows && semanticGroupCount == 1;
sameInstanceSwitch && lifecycleActivationDelta == 0;
closeFirstRetainsSecond && finalCloseFollowsPolicy;
calculatorAHandle != calculatorBHandle && calculatorGroupCount == 2;
consoleReused && imageViewerReused && zeroWindowProjectionSuppressed;
cleanup && staleTaskbarOwners == 0;
```

Call it from the AppRuntime diagnostic after the existing lifecycle runtime proof and before final completion markers. Add the AppModel self-test counter summary without replacing existing Phase 6 markers.

- [ ] **Step 2: Run the runtime diagnostic to verify the proof is initially red**

Build the `AppRuntime` diagnostic variant and run the existing bounded QEMU/serial harness. Expected: the new markers are absent or report failure until the proof and marker wiring are complete.

- [ ] **Step 3: Implement the bounded multi-window fixture**

Use an internal diagnostic-only `Window` subclass in the same style as `OwnershipProbeWindow`. Create one `ReuseExisting` or `MultiInstance` test instance, attach exactly two Windows, reconcile, focus each in turn, close the first, verify the second remains owned/presentable, then close the second and assert final-window policy. Always clean up in `finally`, reconcile, and assert zero diagnostic instances/Windows/entries.

- [ ] **Step 4: Implement Calculator multiple-instance and reusable proofs**

Use `Desktop.LaunchApplication` with typed requests for two Calculator launches, retain both generation-safe handles, activate each and close only one, then verify the other remains registered and projected. Exercise Console and Image Viewer relaunch/reuse, and WAV Player when its existing resource path is available. Verify zero-window suppression after closing reusable presentation and clean reattachment on relaunch.

- [ ] **Step 5: Add read-only Task Manager observation**

Add a bounded instance-count/summary line or diagnostic field using only the observation API. Do not replace the Window list, call lifecycle methods, or reorder Windows. The test must snapshot the active handle and registry counters before/after observation and assert no mutation.

- [ ] **Step 6: Run runtime GREEN verification and commit**

Run AppModel and AppRuntime diagnostic builds/runs, inspect all new markers, and commit:

```powershell
git add guideXOS\DefaultApps\TaskManager.cs guideXOS\Program.cs guideXOS\OS\ApplicationInstance.cs guideXOS\GUI\TaskbarApplicationEntry.cs
git commit -m "test: prove taskbar grouping and instance observation"
```

---

### Task 6: Document Phase 7 and run the complete regression matrix

**Files:**
- Modify: `APP_MODEL_CONVERGENCE.md`
- No changes: `D:\dev\guideXOSServer`
- No changes: `D:\dev\guideXOS`

- [ ] **Step 1: Add the Phase 7 architecture documentation**

Document the exact implemented behavior:

- taskbar application-entry projection and authority boundary;
- one semantic group per instance with separate visual Window buttons;
- bounded group/window limits;
- current/most-recent/first Window selection;
- same-instance versus cross-instance activation;
- suspended activation/resume and unsupported failure;
- Window close versus application Close;
- reusable zero-window suppression and relaunch;
- multiple same-descriptor instances;
- Task Manager read-only observation;
- future Ring 3/process-backed instance compatibility;
- richer visual grouping/window picker deferred.

- [ ] **Step 2: Run whitespace and source/build verification**

Run:

```powershell
git diff --check HEAD~6..HEAD
dotnet publish guideXOS\guideXOS.csproj -c Release --no-restore -p:SkipISO=true
```

Expected: no whitespace errors and a successful NativeAOT publish.

- [ ] **Step 3: Run the full diagnostic matrix**

Run the repository's canonical build and UEFI diagnostic variants for:

```text
AppModel
AppRuntime
NativeInput
ContextMenu
production Continuous/boot
```

Run the lifecycle self-test/runtime proof and new grouping/multi-window runtime proof in their respective AppModel/AppRuntime serial logs. Preserve each serial log under the existing ignored `serial_*.txt` convention.

- [ ] **Step 4: Verify required counters from fresh logs**

Check fresh output for:

```text
TASKBAR_GROUPING_SELFTEST=passed=<positive>;failed=0;result=PASS
TASKBAR_GROUPING_RUNTIME=multiWindow=1;sameInstanceSwitch=1;crossInstanceSwitch=1;result=PASS
APP_MODEL_FACTORY_FALLBACKS=0
APP_MODEL_COMPAT_LEGACY_BACKEND_CALLS=0
ALLOCATOR_CORRUPT=0
ThreadPool.Locked=0
GRAPHICS_VALID=1
unexpected input drops=0
stale instances=0
stale ownership=0
stale taskbar owners=0
taskbar group leak=0
```

If a check fails, stop making completion claims and use the systematic-debugging workflow with a focused reproducer before continuing.

- [ ] **Step 5: Confirm source scope and final worktree**

Run:

```powershell
git status --short --branch
git diff --name-only origin/main...HEAD
git diff --check origin/main...HEAD
```

Confirm only the approved guideXOS/app-model files, `APP_MODEL_CONVERGENCE.md`, and the committed design/plan documents changed. Confirm Server and Legacy have no modifications.

- [ ] **Step 6: Commit documentation and final verified changes**

```powershell
git add APP_MODEL_CONVERGENCE.md
git commit -m "docs: document Phase 7 taskbar grouping semantics"
```

Run the final verification commands once more after this commit before reporting completion.

## Completion Report

The final report must include the 48 requested Phase 7 fields from the user specification, including outcome, repository/branch/HEAD, worktree, semantic entry/grouping behavior, activation/close/reuse/suspension results, all diagnostic/regression results, counters, cleanup, documentation, exact files, Server/Legacy modification status, commit/push status, future Ring 3 assessment, and recommended Phase 8.
