# guideXOS Phase 7 — Application-Centric Taskbar Design

**Date:** 2026-09-16
**Repository:** `D:\dev\guideXOSUEFI`
**Canonical architecture:** `APP_MODEL_CONVERGENCE.md`

## Goal

Move taskbar ownership and lifecycle interaction from raw Window identity to
`ApplicationInstance` identity while preserving the existing taskbar's visual
window-button presentation.

The defining rule is:

> The taskbar represents running applications; Windows are members of those applications.

Phase 7 adds semantic grouping underneath the current renderer. One
`ApplicationInstance` has one semantic taskbar group even when it owns several
Windows. Each owned Window may still render as a separate taskbar button until
a later visual-grouping phase.

## Non-goals

- No taskbar visual redesign or one-button visual collapse.
- No thumbnail picker or elaborate window picker.
- No Ring 3, process isolation, allocator, scheduler, GXM, boot, or UEFI changes.
- No redesign of `WindowManager`, applications, Task Manager UI, Server, or Legacy.

## Current authoritative state

`ApplicationInstanceRegistry` remains the only authority for:

- application-instance existence and generation-safe handles;
- descriptor/application identity;
- lifecycle and active-application state;
- bounded Window ownership;
- instance policy, including reusable/zero-window/final-window behavior.

`WindowManager` remains the graphical Window owner and z-order authority.
Windows remain available for Window-specific diagnostics and operations.

The taskbar projection is never allowed to create an instance, mutate
ownership, transition lifecycle, or retain a terminal owner.

## Semantic taskbar projection

Add a bounded `TaskbarApplicationEntry` projection and a corresponding
`TaskbarApplicationEntryRegistry` in the GUI/shell layer.

Each entry is keyed by `ApplicationInstanceHandle` and carries only bounded
presentation state:

- generation-safe instance handle;
- descriptor/application ID;
- descriptor display/resource identity when resolvable;
- lifecycle snapshot, active flag, and suspended flag;
- owned-window count and presentable-window count;
- active/selected Window reference;
- most-recently-active valid Window reference;
- a fixed array of owned/presentable Window references for validation and
  diagnostics.

The entry registry uses a fixed array sized to
`ApplicationInstanceRegistry.Capacity` (32). Per-entry Window storage uses the
existing maximum owned-window bound (8). No dynamic dictionaries or unbounded
collections are introduced in the shell hot path.

Reconciliation rebuilds or refreshes the projection from authoritative registry
state. Rebuilding all entries from the same registry and WindowManager state
must produce equivalent semantic state. Entry reuse is an optimization only.

The renderer continues to enumerate `WindowManager.Windows` in its established
order. For each visible `ShowInTaskbar` Window, it resolves the owning entry,
retaining separate Window buttons while ensuring every button points to the
same semantic group for shared ownership.

## Entry creation and suppression policy

For normal built-in descriptors, taskbar visibility uses the descriptor's
existing `ApplicationShellPolicy.ShowInTaskbar` value and requires at least one
presentable owned Window.

Default behavior:

- one presentable Window: one semantic entry and one visible button;
- multiple presentable Windows: one semantic entry and multiple visible
  buttons;
- zero owned/presentable Windows: no visible entry;
- reusable or persistent zero-window instances remain registered and reusable,
  but do not leave a ghost taskbar button;
- a future explicit zero-window shell policy may opt into a visible entry, but
  no current built-in uses that behavior by default;
- failed, terminated, or stale instances cannot retain an entry.

When an instance temporarily has no presentable Window, its projection is
removed or suppressed while the authoritative reusable instance remains
`Inactive`/valid according to its policy. A later relaunch may recreate or
reattach a Window and rebuild the entry.

For descriptor identities without a resolvable descriptor, the projection uses
the existing Window presentation flags as a bounded compatibility observation;
this fallback does not become application identity or a second lifecycle
authority.

## Activation and focus

Taskbar activation starts from an instance handle and an optional selected
owned Window. The operation validates both the generation-safe handle and the
current ownership relationship before changing focus.

Selection order for an instance with multiple Windows is deterministic:

1. current active owned Window;
2. most recently active valid owned Window;
3. first valid presentable owned Window.

If the user clicked a particular Window button, that Window is the requested
selection after ownership validation.

Activation behavior:

1. reject stale, failed, or terminated handles and remove their projection;
2. if suspended, call the existing registry resume path first;
3. if resume is unsupported or fails, return a bounded lifecycle failure and
   preserve valid state and shell responsiveness;
4. activate the target `ApplicationInstance` through the existing lifecycle
   contract;
5. bring the selected Window forward through an explicit Window-focus route;
6. update the projection's active/most-recent Window metadata.

Application activation and Window focus are deliberately separate:

- switching between Windows owned by the same instance changes Window focus and
  z-order without application deactivation/reactivation or lifecycle callback
  churn;
- switching from instance A to instance B performs normal A deactivation and B
  activation;
- two instances with the same descriptor remain independent because all
  routing is keyed by generation-safe instance handle, never descriptor ID
  alone.

Arbitrary programmatic `WindowManager.MoveToEnd` calls do not implicitly become
lifecycle transitions. Callers that intentionally foreground an application
Window use an explicit instance-aware focus/activation helper.

## Closing semantics

Window-local close remains Window-local. Closing one Window detaches only that
Window from its owning instance. The semantic entry remains while another
presentable owned Window exists.

Closing the final Window follows the existing authoritative policy:

- ordinary instances with `CloseWhenLastWindowClosed` terminate;
- reusable/persistent instances with `AllowZeroWindows` remain registered and
  become inactive/suppressed as appropriate;
- final termination removes the projection and invalidates future operations
  through the generation-safe handle.

An explicit taskbar application Close route calls
`ApplicationInstanceRegistry.RequestClose(handle, ApplicationCloseReason.ShellRequest)`
once. It does not close an arbitrary Window first. Accepted close requests
terminate the instance and close all owned Windows through existing safe
cleanup. Cancelled requests leave lifecycle, ownership, and projection state
intact. Window title-bar close, minimize, restore, tombstone, and specific
Window focus remain Window-level operations.

## Task Manager observation

Task Manager remains observational. It may consume a read-only instance
enumeration/snapshot exposing:

- descriptor/application ID;
- instance handle;
- lifecycle state;
- active/suspended state;
- owned-window count.

Observation must not create instances, mutate ownership, activate/deactivate,
or change Window z-order. Existing Window enumeration remains available for
Window-specific diagnostics, and no full Task Manager UI redesign is required.

## Diagnostics and counters

Production logging remains disabled. Conditional counters cover:

- taskbar entries created, removed, reused;
- group Window attach/detach;
- application activations;
- same-instance Window switches;
- cross-instance switches;
- stale taskbar owners rejected;
- zero-window entries suppressed;
- multi-window groups observed;
- maximum Windows in one group;
- projection cleanup/leak checks.

The projection must report zero stale taskbar ownership after reconciliation and
must never retain a projection for a terminal instance.

## Deterministic self-tests

The Phase 7 grouping self-test is layered on top of the Phase 6 self-tests and
leaves no diagnostic fixtures behind. It proves:

- one instance/one Window maps to one entry;
- one instance/two Windows maps to one entry with two members;
- same-instance Window A → Window B selection changes focus only and does not
  add application activation/deactivation churn;
- closing Window A leaves Window B and the semantic group valid;
- closing the final Window follows final-window policy and removes/suppresses
  the entry;
- two instances of the same descriptor have distinct handles and entries;
- reusable zero-window instances stay valid but produce no visible projection;
- entry reuse is keyed by handle/generation and stale handles are rejected;
- terminated/failed instances leave no entry;
- suspended activation resumes before focus selection;
- unsupported resume returns a bounded failure;
- application-level close accepts or cancels exactly once according to the
  Phase 6 lifecycle contract;
- Task Manager observation does not mutate lifecycle, ownership, or focus;
- final diagnostics report zero active diagnostic instances, zero diagnostic
  Windows, zero stale ownership, and zero stale taskbar entries.

## Runtime proofs

The bounded runtime proof uses:

- a diagnostic-only multi-window fixture with one instance and two bounded
  Windows, not exposed through production Start;
- two real Calculator launches for independent same-descriptor instances;
- reusable Console and Image Viewer flows, plus WAV Player where available,
  to verify reuse and zero-window suppression.

The proof records:

- one semantic owner for the multi-window instance while two Window buttons may
  render;
- same-instance switch without lifecycle churn;
- one-Window close retaining the group and remaining Window;
- final-Window policy and projection removal/suppression;
- independent Calculator handles, activation, and close behavior;
- reusable-instance identity preservation and clean presentation recreation.

All fixtures are closed/terminated and reconciled before the diagnostic returns.

## Regression requirements

Phase 7 must preserve every Phase 6 guarantee and add grouping validation on
top of it:

- build;
- AppModel;
- compatibility self-test;
- lifecycle self-test and runtime lifecycle proof;
- taskbar/grouping self-test;
- multi-window and multiple-instance runtime proofs;
- AppRuntime;
- NativeInput;
- ContextMenu;
- production Continuous/boot.

Required final conditions include compatibility fallback `0`, legacy backend `0`,
allocator corruption `0`, `ThreadPool.Locked=0`, valid graphics invariants, no
unexpected input drops, zero stale instances, zero stale ownership, zero stale
taskbar owners, no group leak, timer/frame progress, no panic, and no CPU fault.

## Future backend compatibility

The taskbar consumes only generation-safe application-instance identity,
lifecycle results, ownership observations, and activation requests. It does not
depend on whether the backend is the current managed in-kernel application, GXM,
a future isolated Ring 3 process, or a future NativeAOT process. The same
projection and routing contract can therefore sit above a process-backed
`ApplicationInstance` without taskbar redesign.

## Expected implementation surfaces

The implementation is expected to remain bounded and focused on:

- a new taskbar-entry projection/registry file;
- `ApplicationInstanceRegistry` instance-aware Window activation and
  observation support;
- `Taskbar.cs` projection reconciliation and instance-aware button routing;
- `WindowManager`/`Window` explicit foreground integration where needed;
- Task Manager read-only instance observation;
- Program/runtime diagnostic wiring;
- `APP_MODEL_CONVERGENCE.md` Phase 7 documentation.

Server and Legacy trees are read-only references and must have no modifications.
