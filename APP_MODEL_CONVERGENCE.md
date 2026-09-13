# guideXOS App Model Convergence

**Status:** Architecture baseline and migration design  
**Date:** 2026-09-13  
**Scope:** guideXOS Server ↔ guideXOS C# UEFI application platform  
**Outcome:** Outcome A — convergence architecture defined

This document is the canonical design reference for converging the guideXOS
Server and guideXOS C# application models. It defines the common application
semantics that both operating systems should implement while preserving the
freedom to use different kernels, schedulers, allocators, executable formats,
loaders, and boot environments.

The document is intentionally an architecture and migration baseline. It does
not convert the C# runtime, change the Server runtime, or replace the proven
C# UEFI path in this phase.

## 1. Decision summary

The modern guideXOS application contract should be based on these decisions:

1. Application identity is an immutable, opaque, stable application ID. A
   display name and aliases are resolution data, not identity.
2. A registered application descriptor is separate from an application
   instance, and an application instance is separate from its windows.
3. Launching is a typed, UI-independent request producing a bounded result.
   A string-only launch API remains a compatibility facade, not the long-term
   contract.
4. Associations resolve to application IDs and pass the document explicitly in
   the launch request. Filesystem-specific lookup is an adapter behind this
   contract.
5. Shell objects have stable IDs and typed targets. A shell action is not
   silently treated as an application name.
6. Lifecycle is an application-instance contract. Window visibility and
   disposal are not a substitute for application lifecycle.
7. Server's manifest/registry, typed launch-target, lifecycle, identity,
   capability, and structured-failure concepts are the preferred architectural
   direction.
8. C#'s proven UEFI-safe lazy initialization, compatibility aliases, direct
   bounded association handlers, reusable viewer/player helpers, and GXM GUI
   integration are retained as implementation strengths.
9. The first implementation is a semantic adapter layer with no production
   launch-behavior change. It preserves all 12 current C# Start-visible
   applications, current aliases, associations, GXM behavior, and production
   defaults.

The common contract does not require Server and C# to use the same process
model. For example:

```text
Server: descriptor → loader → task/process → executable/application entry
C#:      descriptor → managed factory → application instance/window
```

Both implementations are conforming if they expose the same application-level
semantics and bounded outcomes.

## 2. Provenance and authority

### 2.1 C# repository preflight

| Field | Value |
| --- | --- |
| Repository | `D:\dev\guideXOSUEFI` |
| Branch | `main` |
| HEAD | `b5f4d1a7ad0b5474ac12382c363432d1a3a53289` |
| Subject | `The real UEFI path works end-to-end. No shared AppModel, window, allocator, input, graphics, or scheduler defect was found.` |
| Upstream | `origin/main` |
| Ahead/behind | `0 / 0` |
| Remote | `git@github.com:guideX/guideXOS-Legacy-UEFI.git` |
| Worktree | Clean at preflight |

The validated application-runtime changes are present in this HEAD. The
repository history includes the App Launch Runtime proof and the associated
AppModel, AppRuntime, native input, context-menu, continuous-boot, and
association validation work.

The compatibility baseline for this design is the proven runtime described by
the phase request: exactly 12 Start-visible built-ins; real Start-menu launch;
repeat launch/close; taskbar, ownership, and focus stability; `.txt`, `.png`,
`.gxm`, `.bmp`, `.wav`, and `.mue` behavior; aliases and shell objects; safe
negative cases; production continuous boot; allocator corruption zero;
`ThreadPool.Locked=0`; and valid graphics invariants.

### 2.2 Server authority selection

The authoritative modern App Model contract is the mainline Server checkout:

| Field | Value |
| --- | --- |
| Repository | `D:\dev\guideXOSServer` |
| Branch | `main` |
| HEAD | `0e6039c0e3f1fd218424ba7367c9e83bd8d73223` |
| Subject | `Fix ISO smoke CD attachment` |
| Upstream | `origin/main` |
| Ahead/behind | `0 / 0` |
| Remote | `git@github.com:guideX/guideXOS-Server.git` |
| Worktree | Pre-existing modifications: `ESP/build-identity.txt`, `ESP/ramdisk.img` |

This checkout contains the mainline App Model v1 / Phase 5A closeout and the
current manifest, registry, typed launch-target, shell-object, association,
desktop-service, and built-in metadata implementation. It is therefore the
authority for intended modern platform semantics.

The advanced execution-backend lineage was also inspected:

| Field | Value |
| --- | --- |
| Repository | `D:\dev\guideXOSServerV1.1_DOTNET_SUPPORT` |
| Branch | `v1.1_DOTNET_SUPPORT` |
| HEAD | `8055fb434996728d9e27fa8aca069a9b3d4a50c6` |
| Subject | `Integrate production NativeAOT composite application lifecycle` |
| Upstream | `origin/v1.1_DOTNET_SUPPORT` |
| Ahead/behind | `1 / 0` |
| Worktree | Clean |

This branch descends directly from the same Server App Model history and adds
the production NativeAOT/composite runtime lifecycle. It is used here as an
execution-backend reference, not as a competing App Model authority. In
particular, its `NativeAppRuntime` states and explicit application/process
identity are useful for the common lifecycle, while its composite image and
resident runtime are Server-specific implementation details.

Other Server worktrees were checked for provenance and were not selected as
the App Model authority:

| Worktree | Branch | Reason not selected |
| --- | --- | --- |
| `D:\dev\guideXOSServer_AARCH64` | `AARCH64_SUPPORT` | Architecture/platform work |
| `D:\dev\guideXOSServer_NAVIGATOR_IMPROVEMENTS` | `NAVIGATOR_GENERAL_IMPROVEMENTS` | Navigator/manifest provenance work |
| `D:\dev\guideXOSServer_NAVIGATOR_JS_SUPPORT` | `NAVIGATOR_JAVASCRIPT_SUPPORT` | Navigator JavaScript feature branch; pre-existing ramdisk/wallpaper changes |
| `D:\dev\guideXOSServerV0.2` | `v0.2` | Older release line |
| `D:\dev\guideXOSServerV0.3_SCI_FI_THEME` | `v0.3_SCI_FI_THEME` | Visual/theme branch |
| `D:\dev\guideXOSServerV0.5_DEVELOPER_STUDIO` | `v0.5_DEVELOPER_STUDIO` | Developer Studio/debugger branch; pre-existing untracked documentation |
| `D:\dev\guideXOSServerApps` | n/a | Not a Git repository |

The lineage is therefore:

```text
Server main / App Model v1 contract
        └── v1.1 DOTNET_SUPPORT / NativeAOT and composite lifecycle backend
```

### 2.3 Legacy provenance

| Field | Value |
| --- | --- |
| Repository | `D:\dev\guideXOS` |
| Branch | `main` |
| HEAD | `4ff1ac4026deeb0799a6b66df3d9998a2438d46b` |
| Subject | `guideXOS Server app model Migrations` |
| Upstream | `origin/main` |
| Worktree | Clean |
| Remotes | `github` → `guideXOS-Legacy.git`; `origin` → `gitlab.com/guideXOS` |

Legacy contains an earlier compatibility-oriented App Model migration surface.
It is recorded only as historical provenance. It is not the design authority
for this convergence, and it was not re-audited for parity.

## 3. Server App Model reverse engineering

### 3.1 Authoritative source locations

The primary Server sources and documents are:

- `app_manifest.h/.cpp`
- `app_manifest_loader.h/.cpp`
- `app_manifest_validator.h/.cpp`
- `app_registry.h/.cpp`
- `app_launch_target.h`
- `app_launch_resolver.h/.cpp`
- `built_in_app_metadata.h`
- `shell_object_registry.h`
- `desktop_service.h/.cpp`
- `process.h/.cpp`
- `lifecycle.h/.cpp`
- `task_manager.h/.cpp`
- `universal_app_loader.h/.cpp`
- `gxapp_loader.h/.cpp`
- `gxapp_container.h/.cpp`
- `gxm_loader.h/.cpp`
- `native_app_runtime.h/.cpp`
- `native_app_process_table.h/.cpp`
- `docs/APP_MODEL_CURRENT_STATE.md`
- `docs/APP_MODEL_PHASE2_TYPED_LAUNCH_TARGETS.md`
- `docs/GXAPP_FORMAT_SPEC.md`
- `docs/dotnet/NATIVEAOT_C109_PRODUCTION_COMPOSITE_APPLICATION_LIFECYCLE.md`
- `scripts/smoke-appmodel-phase5b-regression-closeout.ps1`
- `scripts/smoke-appmodel-phase5a-status.ps1`
- `scripts/smoke-appmodel-phase4a-built-in-registry.ps1`
- `scripts/smoke-appmodel-phase4b-file-association-v1.ps1`
- `scripts/smoke-appmodel-phase4c-shell-object-registry.ps1`
- `scripts/smoke-appmodel-phase4d-recent-programs.ps1`
- `scripts/smoke-startup-appmodel-regression.ps1`

### 3.2 Application identity and descriptors

Server manifests are schema-versioned records. The current schema v1 includes:

- immutable-looking application ID;
- display name, version, publisher, description, category;
- `AppKind` such as `BuiltIn`, `NativeElf`, `GXAppPackage`, `Service`,
  `HypervisorGuest`, or `Script`;
- icon/resource reference;
- minimum guideXOS version and supported architectures;
- one or more executable/runtime entries containing architecture, path,
  entry point, ABI, and runtime;
- declared permissions/capabilities;
- file associations containing extension, content type, and description;
- default-window hints;
- desktop registry hints used for canonical launch names, aliases, Start-menu
  visibility, recent-program policy, shell-object policy, and target handling.

Server also creates synthetic descriptors for built-ins from the shared
`built_in_app_metadata.h` table. This is important: the built-in metadata table
is descriptive and shared between hosted and bare-metal registration, while the
actual hosted and bare-metal launch backends remain separate.

The current Server namespace uses `gxos.builtin.*` for built-ins and separate
namespaces for samples and examples. Duplicate IDs are diagnosed, and source
precedence is represented in the registry rather than being left to an
unstructured list.

### 3.3 Discovery and registration

Server separates discovery from execution:

1. A registry scans configured application roots such as `/system/apps` and
   `/Apps` plus development/example sources.
2. A bounded manifest loader parses a manifest.
3. A validator checks schema, identity, kind, entry, architecture, relative
   paths, permissions, and association syntax.
4. The registry records valid apps, invalid apps, and duplicate IDs as separate
   results.
5. Built-ins are registered as synthetic manifest records from shared metadata.
6. A temporary development registration path exists with ownership and
   generation data so it can be unregistered safely.

This is stronger than a single static list because it makes installed,
development, built-in, and package identity explicit. It does not imply that
C# must implement package scanning before it can adopt the semantic contract.

### 3.4 Launch semantics

Server has both a typed target model and compatibility paths.

`LaunchTargetType` distinguishes built-in applications, manifest apps, native
ELF, GXApp packages, shell actions, legacy aliases, file open, cross-architecture
emulation, services, hypervisor guests, and scripts. `LaunchDispatchUsage`
records whether dispatch was typed, a legacy fallback, an explicitly blocked
unknown fallback, or a special-case fallback.

The mainline desktop launch path is approximately:

```text
shell/name request
  → typed target selection and alias resolution
  → shell action or registered application resolution
  → manifest strategy/entry resolution
  → native/process/runtime backend where supported
  → bounded failure or compatibility fallback
```

The current implementation is deliberately transitional. Active typed dispatch
owns the supported safe routes, while legacy fallback remains observable for
unsupported or drifted cases. Built-in execution is still partly hardcoded in
`DesktopService`; Native ELF support is partial/feature-gated; and the GXApp
execution pipeline is not yet a complete general runtime in Server main.

The C109 NativeAOT work demonstrates a more explicit execution contract:
logical application IDs dispatch through a single resident composite image and
managed runtime, with repeated launches reusing the image/runtime/thread/TLS/
heap and invalid IDs returning a bounded failure. That reuse is an implementation
choice; explicit identity, lifecycle, and result reporting are the portable
ideas.

### 3.5 Lifecycle

The production NativeAOT runtime exposes:

```text
Created → Prepared → Running → Suspended → Closing → Exited
                                      └──────→ Failed
```

Preparation validates the selected app, launch decision, image, ABI, identity,
permissions, environment, and runtime context. Cleanup is explicit, bounded,
idempotent, and closes owned windows before final exit. Runtime context records
application ID, runtime ID, display name, architecture, process ID, app
directory, permissions, arguments, environment, lifecycle state, exit code,
failure reason, timestamps, diagnostics, and cleanup status.

The broader Server OS lifecycle (`ColdStart` through `Interactive` and
`ShuttingDown`) is platform bootstrap lifecycle, not application lifecycle. The
two must not be conflated.

### 3.6 Ownership

Server separates:

- registered descriptor identity;
- application/runtime identity;
- process/task identity;
- runtime-owned windows and host-call resources;
- compositor/shell presentation;
- tombstone and exit diagnostics.

`ProcessSpec` and `ProcessTable` carry `appId`. Process tombstones retain
application identity and bounded termination information. The NativeAOT process
table separately tracks runtime IDs, lifecycle state, owned-window counts, and
completion statistics.

### 3.7 Associations

Server v1 associations are manifest-backed and shell-integrated. Current
behavior covers folders, text-like files, legacy image handling, and bounded
risky/unsupported fallbacks. The implementation has not yet completed the
general document-argument path for every handler; the source explicitly marks
image routing through a first-class typed file parameter as follow-up work.

The intended direction is nevertheless clear: an association selects an app
identity and a document target. It must not require the application to infer a
file from a UI click or global mutable state.

### 3.8 Shell integration

Server has a stable shell-object registry. It distinguishes virtual objects,
filesystem folders, and system panels, and records:

- stable shell-object ID;
- display name and aliases;
- canonical launch target;
- default handler application identity;
- whether active typed dispatch may own the route;
- system-only and destructive-risk flags;
- recent-program policy;
- typed target kind and canonical value.

Representative IDs include desktop, this-system/root, files, standard folders,
settings, control panel, and trash-open. Destructive trash operations are not
silently represented as an ordinary open action.

The Start/taskbar and filesystem routes are still transitional in the hosted
implementation: typed shell dispatch surrounds a mixture of typed handlers,
legacy aliases, and built-in service calls. This is a migration state, not a
reason to make shell names the application identity.

### 3.9 Platform services and errors

The demonstrated Server platform contracts include desktop/window services,
filesystem/document opening, notifications/message boxes, recent identity,
settings/control-panel shell routes, process/task identity, runtime host calls,
and declared permissions. Networking, clipboard, app-local storage, and full
package lifecycle are not yet a complete App Model v1 contract and should not be
invented as mandatory common fields without an implementation need.

Errors are currently represented by boolean/result paths plus bounded diagnostic
strings and shell notification presentation. This is serviceable but should be
formalized into a shared error code with an optional bounded diagnostic.

## 4. Current C# App Model reverse engineering

### 4.1 Current source locations

The principal C# sources are:

- `guideXOS/OS/AppModel.cs`
- `guideXOS/OS/App.cs`
- `guideXOS/GUI/Desktop.cs`
- `guideXOS/GUI/StartMenu.cs`
- `guideXOS/GUI/Taskbar.cs`
- `guideXOS/GUI/WindowBase.cs`
- `guideXOS/GUI/Window.cs`
- `guideXOS/GUI/WindowManager.cs`
- `guideXOS/GUI/GXMScriptWindow.cs`
- `Kernel/Misc/GXMLoader.cs`
- `Program.cs`
- `build.ps1`
- `run_uefi_validation.ps1`

### 4.2 Identity and registration

The C# model has an `AppDescriptor` with:

- `AppId`;
- `DisplayName`;
- `DispatchName`;
- `AppKind`;
- legacy aliases;
- an icon.

`AppLaunchResolver` lazily creates a static descriptor list. The current
Start-visible compatibility catalog contains 12 stable built-in IDs:

```text
gxos.builtin.calculator
gxos.builtin.files
gxos.builtin.console
gxos.builtin.devices
gxos.builtin.diskmanager
gxos.builtin.displayoptions
gxos.builtin.firewall
gxos.builtin.notepad
gxos.builtin.paint
gxos.builtin.taskmanager
gxos.builtin.imageviewer
gxos.builtin.wavplayer
```

`AppCollection` creates the same 12 visible application entries and initializes
the resolver. It currently combines descriptor enumeration and launch dispatch
through a hardcoded switch. The descriptor list is not a dynamic installed-app
registry, and the `App` object stores an arbitrary runtime object rather than an
explicit application instance record.

The C# model is intentionally lazy in the UEFI path: `Desktop.Initialize()`
sets safe shell state, and `Desktop.InitializeAppModel()` is called after the
appropriate boot transition. That sequencing is a valuable platform-specific
constraint and must remain intact.

### 4.3 Launch behavior

`AppCollection.Load(name)` resolves an ID, alias, display name, or dispatch name
and then switches on the dispatch name. It creates ordinary built-in windows,
reuses the global console where appropriate, and reuses the Image Viewer and WAV
Player helper windows. Successful launches update recent-program identity and
taskbar/start visibility through the resulting window.

This is a real, proven launch path, but the practical unit is currently a
window. There is no first-class application instance that can own zero, one, or
multiple windows.

### 4.4 Lifecycle and ownership

`Window` construction adds a window to `WindowManager.Windows`, sets visibility,
assigns an owner ID, and establishes allocator ownership. Closing/fading later
causes disposal and cleanup. `WindowManager` uses ordering for focus and
foreground behavior; `Taskbar` derives buttons from visible windows with
`ShowInTaskbar`.

This explains why the current baseline is sensitive to ownership and cleanup:
taskbar membership, focus, and application behavior are coupled to window
objects. The baseline has now proven that coupling safe for the covered cases,
but it remains the wrong long-term identity boundary.

### 4.5 Associations and shell objects

`FileAssociationRegistry` explicitly maps:

```text
.txt       → gxos.builtin.notepad
.png/.bmp  → gxos.builtin.imageviewer
.wav       → gxos.builtin.wavplayer
.gxm/.mue  → GXM handling
```

It normalizes extensions and returns bounded failure reasons. `Desktop.TryOpenAssociatedFile`
then performs the proven direct behavior: Notepad opens text, Image Viewer
opens decoded images, GXMLoader handles GXM/MUE, and WAV Player checks the audio
device before opening. Missing and unsupported cases are surfaced safely.

`ShellObjectRegistry` has explicit entries for Computer Files, Root, USB Drive,
and Install to Hard Drive. `Desktop.OnClick` resolves shell objects before
falling back to file/directory/application handling. This is behaviorally
useful, though its namespace and record shape differ from Server's richer shell
registry.

### 4.6 GXM and resources

`GXMLoader` validates bounded GXM/MUE headers, entry/image sizes, and GUI script
markers. GUI scripts create `GXMScriptWindow`, which is a `Window` subclass and
supports title, bounds, resize/taskbar/start/minimize/maximize/tombstone flags,
controls, application opening, and close/save/load callbacks. This is a real
application backend, not merely a name resolver.

Current built-in icons are resolved as C# `Image` objects and viewer/player
helpers are retained/recreated based on live `WindowManager` membership. That is
appropriate for the current UEFI backend, but the common contract should expose
resource keys/semantics rather than C# image object ownership.

## 5. Semantic comparison and platform decisions

The classifications below answer which behavior should become guideXOS
platform behavior. “Server superior” refers to the abstraction, not to every
current Server execution path. “C# superior” refers to a useful proven
compatibility behavior, not a demand that Server copy C# internals.

| Concern | Server | C# | Classification | Platform decision |
| --- | --- | --- | --- | --- |
| Stable identity | Manifest ID plus registry identity | Stable IDs in static descriptors | Same concept / Server superior | Use immutable opaque IDs; keep C# IDs through compatibility mapping |
| Display names and aliases | Registry resolution, canonical launch hints | Exact ID/name/alias matching | Same concept / hybrid | Preserve aliases; expose ambiguity and canonical ID in the common resolver |
| Descriptor richness | Version, publisher, kind, entries, permissions, associations, policy hints | Name, dispatch, kind, aliases, icon | Server superior | Adopt the richer descriptor shape incrementally |
| Discovery | Built-ins, manifests, packages, development sources | Static 12-entry list | Server superior | Separate registry from factories; dynamic discovery is later C# work |
| Built-in registration | Shared metadata plus synthetic manifests | `AppCollection` list | Server superior | Use metadata as the identity source; retain C# list as an adapter initially |
| Launch request | Typed targets plus compatibility names | String/name load | Server superior | Adopt typed request/result; keep `Load(string)` as a shim |
| Built-in execution | Partly hardcoded desktop dispatch | Hardcoded managed factory switch | Same concept / implementation detail | Hide both behind a launch backend; do not port internals |
| Native/runtime strategy | Manifest strategy and NativeAOT lifecycle | Managed factory/window backend | Platform specific | Common semantics only; each OS selects its backend |
| App lifecycle | Explicit runtime state and cleanup | Implicit window construction/disposal | Server superior | Add app-instance lifecycle around existing windows |
| App/window separation | Process/runtime/window ownership are distinct | Ordinary app is usually a `Window` | Server superior | Make one app instance capable of zero, one, or many windows |
| Process/task identity | `appId`, process ID, runtime ID, tombstones | No app process abstraction | Server superior | Add a C# instance/backend identity without requiring native processes |
| Associations | Manifest-backed v1, still incomplete for all typed document paths | Explicit, proven extension table and direct handlers | Hybrid | Common association contract; preserve C# handlers while moving selection into requests |
| Shell objects | Stable IDs, typed targets, policy flags | Stable IDs but fewer kinds and compatibility routes | Server superior | Adopt Server shell record semantics; retain C# Root/USB/installer adapters |
| Start/taskbar | Registry/desktop/compositor integration in transition | Derived from live windows | Same concept / Server superior | Shell presentation follows instance/window registration, not app identity alone |
| Resource identity | Package-relative icon/assets | In-memory `Image` and ramdisk assets | Server superior for contract | Common resource keys; C# resolves them to existing image assets |
| Errors | Bounded result plus diagnostic/notification | Boolean plus message box/notification | Server superior | Add error enum/result; preserve UI presentation as a shell concern |
| GXM | Package/loader direction, incomplete general execution | Proven UEFI GXMLoader and GUI script windows | C# superior for current coverage | Preserve C# GXM backend under the common GXM application class |
| Application catalog | Server metadata currently covers more built-ins | Exactly 12 proven Start-visible apps | Compatibility requirement | Keep the 12 C# IDs and behavior; catalog size is not a cross-OS invariant |
| Permissions/capabilities | Declarative manifest permissions | No equivalent enforcement layer | Server superior but not first milestone | Carry declarations; enforce only when C# backend can prove them |
| Package/install lifecycle | Manifest/package direction, partial | Installer shell route, no package registry | Server superior / needs design | Define package identity later; preserve current installer route unchanged |
| File paths | Backend/package paths | UEFI filesystem paths | Implementation detail | Documents are explicit targets; paths remain backend-owned |

### 5.1 Important identity discrepancies

The repositories do not currently have identical built-in catalogs or IDs. For
example, Server metadata uses `gxos.builtin.fileexplorer` while C# currently
uses `gxos.builtin.files`; C# also has `devices`, `firewall`, and `wavplayer`
entries that are not equivalent Server built-ins in the inspected metadata.

The common contract must not silently rename a proven C# ID. Application IDs are
opaque and compatibility-sensitive. The first migration therefore keeps every
current C# ID and alias, introduces an explicit cross-model mapping where a
Server canonical identity exists, and defers any canonical rename until a
versioned compatibility decision and runtime proof exist.

This is not a permanent second App Model. It is a compatibility mapping at the
identity boundary while both backends converge on one descriptor and launch
semantics.

### 5.2 The C# `AppKind` boundary

C# currently uses one enum for built-in applications, legacy aliases, GXM apps,
file associations, shell objects, and unknown values. Server separates app kind,
launch target type, association, and shell object.

The common model must separate these concepts. The current enum can remain as a
compatibility projection, but it should not receive new platform semantics.

## 6. Common guideXOS App Model contract

### 6.1 Application descriptor

The common descriptor is an immutable registration record with the following
semantic fields:

| Field | Required | Meaning |
| --- | --- | --- |
| `appId` | Yes | Opaque, stable identity; never derived from display text |
| `displayName` | Yes | User-facing name |
| `aliases` | No | Compatibility and user-facing resolution names |
| `version` | Yes | Descriptor/application version; `0.0.0` is not a substitute for absence |
| `icon` / resource key | No | Package-relative or platform resource reference |
| `applicationClass` | Yes | BuiltIn, Native, Managed/Composite, GXM, Package, Service, or equivalent |
| `capabilities` | No | Declarative permissions/capabilities, not automatically granted rights |
| `associations` | No | Extension/content-type/verb claims resolved by the registry |
| `launchEntries` | No | Backend execution entries and architecture/ABI information |
| `shellPolicy` | No | Start/taskbar/recent/document/folder visibility and handling hints |

`publisher`, description, category, minimum OS version, default-window hints,
and backend-specific metadata may remain descriptor extensions. They should not
be mandatory application-visible fields unless a platform service needs them.

A descriptor is not a window. `defaultWindow` is a hint for a backend, not an
identity or lifecycle requirement.

### 6.2 Launch request

The conceptual common request is:

```text
LaunchRequest {
    targetAppId?          // canonical identity when already known
    targetNameOrAlias?    // compatibility input; resolved before dispatch
    arguments[]
    document?             // explicit path/URI/document target
    verb                  // default/open or another declared action
    sourceShellObjectId?  // source context, not application identity
    activationIntent      // launch, activate-existing, new-instance, or policy default
}
```

The request is UI-independent. Start, taskbar, shell object, association, and
programmatic callers all construct the same conceptual request. A shell action
such as Install to Hard Drive is a typed action target; it is not forced into an
application ID merely because the current UI displays it in an app-like list.

The common resolver must return a canonical target or a bounded resolution
failure before backend loading. Name/alias resolution must report ambiguity
instead of choosing an arbitrary match.

### 6.3 Launch result and errors

The common result is conceptually:

```text
LaunchResult {
    success
    appId?
    instanceId?
    activationState
    errorCode
    boundedDiagnostic?
}
```

The minimum error vocabulary is:

```text
Success
NotFound
AmbiguousTarget
UnsupportedTarget
MalformedRequest
ResourceUnavailable
PermissionDenied
InitializationFailed
ActivationFailed
AlreadyTerminated
BackendUnavailable
```

An implementation may provide more detail internally, but the application and
shell contract must remain bounded and deterministic. The shell may render an
error using a notification, message box, or log. An error window is a
presentation choice, not the error protocol.

### 6.4 Lifecycle

The common application-instance lifecycle is:

```text
Registered → Loading → Initialized → Running → Activated
                                      │          │
                                      │          └→ Inactive/Suspended
                                      └────────────→ Closing → Terminated

Loading/Initialized/Running/Activated may transition to Failed.
```

`Registered` is descriptor availability, not an instance. `Loading` and
`Initialized` cover backend preparation. `Running` means the instance can make
progress. `Activated` means the shell has made it the active target, not that a
window must exist. `Inactive/Suspended` is optional for a backend that cannot
pause execution. `Closing` owns bounded cleanup; `Terminated` is final.

The Server NativeAOT states map naturally to the middle of this contract. C#
can implement the same observable states around managed factories without
creating a native process.

### 6.5 Application instance versus window

One application instance may own:

- zero windows, for a background or command-style app;
- one window, for a simple built-in;
- multiple windows, for a document app or tool.

The instance owns launch context, arguments, document state, app-local resources,
backend execution, and a collection of window handles. A window owns its bounds,
visibility, input routing, graphics resources, and close/activation events.

Window creation must not be the only way to establish application identity, and
window disposal must not be the only way to report application termination.

### 6.6 Ownership rules

| Object | Owner | Required behavior |
| --- | --- | --- |
| Descriptor | App registry | Immutable identity and metadata; outlives instances |
| App instance | App Model/runtime | Launch context, lifecycle, instance ID, app-local resources |
| Task/process/runtime | OS-specific execution backend | Execution isolation/hosting and termination state |
| Window | Window/compositor backend, referenced by instance | Visual/input state; may be zero-to-many per instance |
| Shell/taskbar entry | Shell | Presentation and activation routing; no ownership of app execution |
| Temporary buffer | Operation/backend | Bounded lifetime; never implied to be app-global |
| Document handle | Filesystem/document service and app instance | Explicitly passed and closed through the backend contract |

Each C# instance should eventually have a stable instance handle and generation
so stale taskbar/window references cannot act on a newly reused window slot.
This directly addresses the class of lifecycle issues recently repaired in the
UEFI runtime without changing allocator or scheduler behavior.

### 6.7 Associations

The common association record is conceptually:

```text
Association {
    extension? / contentType? / URI scheme?
    verb = open
    handlerAppId
    priority/default policy
}
```

Resolution normalizes the input, selects a handler, and constructs a launch
request whose `document` is explicit. A missing handler returns
`UnsupportedTarget` or a more specific `NotFound` result; it does not fall
through to an arbitrary application name.

The initial common compatibility set is the proven C# set: `.txt`, `.png`,
`.bmp`, `.wav`, `.gxm`, and `.mue`, including bounded failures for missing or
unsupported targets. MIME/content-type and URI-scheme support remain optional
extensions until a demonstrated need exists.

### 6.8 Shell objects

The common shell-object record should include:

```text
shellObjectId
displayName
aliases
targetKind = virtual | filesystem | systemPanel | action
canonicalTarget
defaultHandlerAppId?
shellPolicy
```

`Root`, `Computer Files/Files`, standard folders, settings, control panel,
trash-open, USB-volume discovery, and installer actions can all be represented
without pretending they are the same kind of target. Destructive actions must
remain explicit and policy-gated.

The Server namespace is the preferred future namespace. C# compatibility IDs
such as `gxos.shell.computerfiles`, Root, USB Drive, and
Install to Hard Drive remain adapter aliases until the shell migration is
proven.

### 6.9 Resources

The common resource contract exposes stable resource keys and package/application
local scope, not concrete `Image` objects or filesystem paths. An icon or asset
may be resolved from:

- a package-relative path on Server;
- a ramdisk/resource table on C#;
- a built-in resource provider on either OS.

Absolute paths, raw pointers, and renderer-owned image objects remain backend
details. Application-local storage and broader packaged assets can be added as
capabilities when implemented, but are not required for the first migration.

### 6.10 Platform services

The initial common service surface should cover only demonstrated, reusable
semantics:

- application launch/activation/termination;
- window creation and ownership;
- filesystem/document opening;
- bounded dialogs/notifications;
- resource/icon lookup;
- settings/system-panel shell routes;
- application identity and recent-program identity.

Networking, clipboard, persistent app-local storage, and update/uninstall
services remain capability-gated future contracts rather than speculative
mandatory APIs.

## 7. Server concepts C# should adopt

| Server reference | Current C# equivalent | Why adopt it | Compatibility impact | Migration complexity | C# approach |
| --- | --- | --- | --- | --- | --- |
| `app_manifest.*`, `AppManifest` | `AppDescriptor` in `AppModel.cs` | Separates stable identity from presentation and backend entry metadata | Keep current 12 IDs and aliases | Low/medium | Add an immutable common projection; keep the existing descriptor as a facade |
| `app_registry.*` | `AppCollection` static list | Separates discovery/registration from launch factories | `Desktop.Apps` remains valid | Medium | Introduce registry semantics without dynamic scanning first |
| `built_in_app_metadata.h` | Hardcoded `LoadDefaultApps()` | One metadata source can drive hosted/bare-metal registration and policy | Must map C#-only IDs explicitly | Medium | Add a C# metadata table preserving all 12 current identities |
| `app_launch_target.h`, resolver | Name-only `AppCollection.Load` | Typed targets prevent shell actions, files, aliases, and apps from collapsing | `Load(string)` continues to work | Low/medium | Add request/result records and route the old method through an adapter |
| `NativeAppRuntime` lifecycle | Window visibility/disposal | Explicit prepare/run/close/fail semantics are testable | No change to current window behavior initially | Medium | Add `ApplicationInstance` state around the current managed factory |
| `ProcessTable` app identity/tombstones | Window owner IDs and taskbar list | Stable app/instance identity avoids window-only ownership | Requires new diagnostic/activation handles | Medium | Use managed instance handles first; do not add a native process |
| Manifest permissions | None | Gives the common contract a future capability boundary | Do not enforce unsupported capabilities yet | Low to declare, high to enforce | Store declarations; return `PermissionDenied` only when enforcement exists |
| `shell_object_registry.h` | `ShellObjectRegistry` | Stable IDs and typed target/policy metadata | Preserve C# aliases and shell routes | Low/medium | Align records and add compatibility aliases |
| Server association records | `FileAssociationRegistry` | Association becomes independent of UI and filesystem internals | Preserve current extensions and direct handlers | Low | Add handler/verb/document semantics behind current resolver |
| Bounded result/diagnostic paths | `bool` plus notification/message box | Makes shell and tests deterministic | Keep existing UI error presentation | Low | Map common errors to existing `ShowOpenError`/notification paths |
| Package-relative resource semantics | C# `Image`/ramdisk assets | Prevents resource identity from being tied to renderer objects | Existing icons continue to resolve | Medium | Add resource keys and a C# provider adapter |

The Server process implementation, composite NativeAOT image, scheduler,
allocator, ELF loader, and privilege model are not on this adoption list. They
are backend implementations, not prerequisites for the common contract.

## 8. C# behavior worth preserving

The following C# behavior is valuable and should remain part of the converged
platform through an adapter or backend:

1. **UEFI-safe lazy initialization.** The application model is initialized at
   the safe post-EBS point. This sequencing must remain a C# backend invariant.
2. **Stable current IDs and compatibility aliases.** The proven 12-entry catalog
   and names such as `File Explorer`/`Computer Files` are user-visible and must
   not be renamed casually.
3. **Simple deterministic resolver behavior.** Exact normalized matching is a
   useful baseline. It should be extended with typed results, not discarded.
4. **Direct bounded file handlers.** Notepad, Image Viewer, WAV Player, and GXM
   behavior are already proven on the real UEFI path. They should become
   association backends behind the common request.
5. **Reusable viewer/player windows.** Reuse is useful for resource and focus
   behavior. It should be represented as instance/window activation policy,
   rather than hardcoded as global application identity.
6. **Shell-first routing.** Resolving shell objects before generic names is
   useful and should remain, with the richer Server shell record shape.
7. **GXM GUI-script integration.** `GXMLoader` and `GXMScriptWindow` already
   express a meaningful application backend with window flags and callbacks.
8. **Existing safe UI error presentation.** Notifications and message boxes are
   a good shell presentation for structured launch failures.
9. **Production defaults.** The current C# defaults and validation selectors
   should remain unchanged while the adapter is introduced.

## 9. C# API compatibility classification

| Current API/concept | Classification | Direction |
| --- | --- | --- |
| `AppDescriptor` | Retain behind common interface | Preserve construction compatibility; add immutable common projection and backend metadata |
| `AppLaunchResolver` | Retain as facade; adapt | Add typed resolution/request path; keep string resolution for callers |
| `AppLaunchResolution` | Retain as compatibility result | Map to common resolution/result and add structured error code later |
| `FileAssociationRegistry` | Retain and adapt | Keep proven extension behavior; return common handler/document request |
| `ShellObjectRegistry` | Retain and align | Preserve current IDs/aliases; add Server-style typed target and policy fields |
| `AppKind` | Compatibility shim; deprecate as platform enum | Split application class, association, shell target, and launch target concepts |
| `App` | Retain as compatibility wrapper | Stop using it as application identity; eventually remove arbitrary `AppObject` ownership |
| `AppCollection` | Retain as legacy facade | Split descriptor registry from instance manager/factory; keep `Load(string)` during migration |
| `App.AppObject` | Compatibility only; eventually remove | Replace with an explicit application instance/window relationship |
| `WindowBase.ShowInTaskbar` / `ShowInStartMenu` | Retain for UI backend | Derive policy from descriptor/instance where possible; do not treat these flags as identity |
| `WindowManager.Windows` | Retain as window backend | Stop exposing it as the application registry; add instance ownership references |
| `WindowManager.MoveToEnd` and cleanup | Retain | Continue to own focus/cleanup while adding stale-reference protection |
| `Desktop.Apps` | Retain facade | Route to the common launch service without changing initialization timing |
| `Desktop.OnClick` | Retain backend entry point | Convert resolved routes into common requests incrementally |
| `EnsureImageViewer`, `EnsureWavPlayer`, `EnsureMessageBox` | Retain helper behavior | Make helper lifetime/activation explicit in instance/window policy |
| `GXMLoader` / `GXMScriptWindow` | Retain GXM backend | Map GXM launches to common descriptors, requests, instances, and windows |

There should ultimately be one modern App Model. The listed shims are migration
surfaces, not a commitment to maintain two equal-status models indefinitely.

## 10. Backend boundary

| Concept | Shared contract | Server backend | C# backend |
| --- | --- | --- | --- |
| Descriptor/registration | Immutable descriptor and registry lookup | Manifest loader/validator, `AppRegistry`, synthetic built-ins | Static metadata adapter initially; later package/installed discovery if supported |
| Launching | `LaunchRequest` → `LaunchResult` | Typed target resolver, `DesktopService`, strategy resolver, process/runtime loader | Common adapter → managed factory and existing `AppCollection` switch |
| App instance | Identity, lifecycle, arguments, document, instance ID | `NativeAppRuntime`, process/runtime tables | New managed `ApplicationInstance` record around current windows/factories |
| Task/process | Execution identity and termination semantics | `ProcessTable`, Native ELF, NativeAOT/composite runtime | Managed host/factory initially; native process only if separately implemented |
| Window ownership | Zero-to-many windows owned by instance | Compositor/window service and host calls | `Window`/`WindowManager`, with instance back-reference added incrementally |
| Filesystem/document open | Association selects handler; document is explicit | Desktop filesystem launch and manifest association adapter | `FileAssociationRegistry` + `Desktop.TryOpenAssociatedFile` adapter |
| File association | Normalized extension/type/verb → handler app ID | Manifest association records and v1 policy | Current explicit extension table, later common association records |
| Shell object | Stable ID, target kind, handler, policy | `shell_object_registry.h` and desktop dispatch | `ShellObjectRegistry`, Root/USB/installer compatibility adapters |
| Taskbar/Start | Shell presentation of registered/active app/window | Desktop/compositor/task integration | Taskbar/Start derived from window backend, later instance-aware |
| Application termination | Closing → terminated/failed, bounded cleanup | Native runtime cleanup and process tombstones | Window/helper cleanup plus new instance state; no allocator/scheduler change |
| Resources | Resource key and app-local scope | Relative package files/resource provider | Ramdisk/built-in image provider |
| GXM | GXM application class and explicit document/args | GXM/GXApp loader where supported | `GXMLoader` and `GXMScriptWindow` |
| Errors | Stable error code and bounded diagnostic | Desktop/runtime result and notification | Existing bool/UI error mapped through compatibility adapter |

## 11. GXM compatibility strategy

GXM is a first-class application class in the common model, not merely a file
extension. A `.gxm` or `.mue` association creates a launch request with:

- a GXM application/package target or handler identity;
- the source document path as `document`;
- any explicit arguments;
- the source shell object if launched from a file view.

On C#, the GXM backend remains `GXMLoader`. GUI scripts create one application
instance that may own one or more `GXMScriptWindow` windows. Script window flags
remain backend behavior. Non-GUI GXM execution can continue through the current
bounded user-mode path.

On Server, GXApp/GXM package and loader work can implement the same descriptor,
request, lifecycle, and result contract when execution support is complete.
The common model does not require the binary format, interpreter, package
layout, or privilege boundary to match.

The first migration must preserve current `.gxm`/`.mue` success and failure
behavior exactly, including buffer disposal and bounded validation.

## 12. Existing C# compatibility strategy

### 12.1 Built-ins

Keep all 12 current C# descriptors, IDs, display names, aliases, icons, and
Start visibility. Introduce a metadata adapter so the common registry can
describe them without changing the `AppCollection` switch.

Where a Server descriptor has a corresponding concept but a different ID, use
an explicit compatibility mapping. Do not silently replace
`gxos.builtin.files` with `gxos.builtin.fileexplorer` or remove C#-only entries.

### 12.2 Aliases

Preserve current aliases, including `File Explorer` and other existing dispatch
names. The common resolver returns the canonical C# ID plus the matched alias.
Future cross-OS aliases may be added only when they do not create ambiguity.

### 12.3 Associations

Keep `.txt`, `.png`, `.bmp`, `.wav`, `.gxm`, and `.mue` behavior and route them
through the common request adapter. Retain the existing direct helpers until
the new app-instance lifecycle has equivalent proof.

### 12.4 Shell objects

Keep Computer Files, Root, USB Drive, and Install to Hard Drive routes. Map
them to common typed shell targets and preserve safe unavailable-USB and
installer failure behavior.

### 12.5 Legacy APIs

`AppCollection.Load(string)`, direct window construction, and window-derived
taskbar behavior remain supported during migration. They become compatibility
facades over the modern contract rather than a second source of truth.

## 13. Staged migration roadmap

### Phase 0 — architecture baseline (this phase)

**Scope:** Record authority, source comparison, common semantics, compatibility
decisions, and the first implementation boundary.

**Files/APIs:** This document only.

**Risk:** None to runtime behavior.

**Proof:** Source inspection, provenance, and existing C# baseline evidence.

**Rollback:** Delete the document only if the project explicitly replaces it;
no runtime rollback is required.

### Phase 1 — shared descriptors and launch requests (first implementation)

**Scope:** Add common semantic records and adapters without changing production
dispatch. The target is the smallest valuable implementation phase.

Likely C# surfaces:

- new common `ApplicationDescriptor` projection in `guideXOS/OS`;
- `LaunchRequest`, `LaunchResult`, and `LaunchErrorCode` records/enums;
- a registry adapter over the current 12 `AppDescriptor` entries;
- a launch-service facade that converts `Load(string)` into a request;
- an association adapter over `FileAssociationRegistry`;
- a shell-target adapter over `ShellObjectRegistry`.

The first adapter must continue to call the existing built-in switch and
existing direct file/GXM helpers. It must not alter production defaults.

**Compatibility risk:** Low if the old path remains the execution backend. The
main risks are ID/name drift, changed alias precedence, and accidental eager
initialization before the safe UEFI point.

**Required proof:**

- all 12 app IDs resolve;
- all existing aliases resolve to the same dispatch names;
- unknown/empty/ambiguous inputs fail deterministically;
- `.txt`, `.png`, `.bmp`, `.wav`, `.gxm`, and `.mue` resolve to the same handlers;
- shell Root, Computer Files, USB, and installer routes remain unchanged;
- existing `AppModel` and `AppRuntime` diagnostics pass;
- production continuous boot remains unchanged;
- allocator corruption remains zero, `ThreadPool.Locked=0`, and graphics
  invariants remain valid.

**Regression tests:** Extend host/self-tests for descriptors, aliases,
associations, shell targets, and result codes; rerun the existing UEFI
AppModel/AppRuntime/NativeInput/ContextMenu selectors and production continuous
boot.

**Checkpoint/rollback:** Keep the adapter in new files or a narrow facade. A
rollback removes the adapter wiring and leaves the proven `AppCollection`,
`Desktop`, resolver, and association implementations intact.

### Phase 2 — application-instance lifecycle

**Scope:** Introduce a stable C# application-instance handle, generation, and
state. Associate windows with an instance without changing rendering or window
geometry.

**Likely surfaces:** `AppCollection`, `Desktop`, `Window`, `WindowManager`,
`Taskbar`, helper windows, and runtime diagnostics.

**Risk:** Medium. Focus, taskbar cleanup, helper reuse, and close ordering are
the sensitive areas.

**Required proof:** Repeated launch/close/relaunch for all 12; multiple windows
for GXM; viewer/player reuse; stale taskbar activation rejection; focus return;
allocator zero; `ThreadPool.Locked=0`; graphics invariants; continuous boot.

**Checkpoint/rollback:** The old window-only ownership path remains available
behind the facade until instance-aware cleanup passes the full runtime matrix.

### Phase 3 — association and shell integration

**Scope:** Make `Desktop.OnClick` and file opening construct common requests with
explicit document and shell-object context.

**Likely surfaces:** `Desktop`, `FileAssociationRegistry`, `ShellObjectRegistry`,
`StartMenu`, filesystem navigation, installer routing.

**Risk:** Medium. Path normalization and fallback ordering can affect safe
negative cases.

**Required proof:** Every existing association success and negative case;
Root/Computer Files/USB/installer routes; explicit app activation from shell;
no arbitrary fallback for missing handlers; production defaults unchanged.

**Checkpoint/rollback:** Keep direct `TryOpenAssociatedFile` as the fallback
backend until every association has passed through the request adapter.

### Phase 4 — incremental built-in migration

**Scope:** Migrate simple built-ins first, such as Calculator and Notepad, then
the remaining ordinary windows and helper-backed applications.

**Likely surfaces:** individual built-in classes, factory registration, app
instance creation, window ownership, recent/taskbar policy.

**Risk:** Medium/high per application, especially Console, Files, Image Viewer,
WAV Player, and applications with special shell behavior.

**Required proof:** Per-app launch/close/focus/ownership plus the complete 12-app
matrix after every migration group.

**Checkpoint/rollback:** Migrate one descriptor at a time; retain its old
factory as a compatibility backend until proof passes.

### Phase 5 — external and GXM compatibility

**Scope:** Put GXM GUI and non-GUI launches behind the common instance/request
contract; then evaluate additional package or executable formats only where the
C# runtime can support them safely.

**Likely surfaces:** `GXMLoader`, `GXMScriptWindow`, file association adapter,
package metadata if introduced, managed/native backend boundary.

**Risk:** High for executable isolation, ABI, resource lifetime, and failure
cleanup. Do not imply that Server Native ELF or NativeAOT can be ported by
renaming a factory.

**Required proof:** GXM GUI flags, document launch, repeated close, malformed
input, buffer cleanup, and all existing app-runtime regressions. Any new
executable backend requires its own bounded loader, capability, and termination
proof.

**Checkpoint/rollback:** GXM remains independently selectable; a new package
backend is opt-in until its proof is complete.

### Phase 6 — deprecate and remove old App Model surfaces

**Scope:** After all behavior is covered, deprecate direct `AppCollection.Load`,
window-as-app assumptions, mixed `AppKind`, and arbitrary `AppObject` storage.

**Risk:** High if done before external callers and diagnostics migrate.

**Required proof:** No production caller depends on the compatibility surfaces;
all Start, shell, association, GXM, lifecycle, and continuous-boot tests pass.

**Checkpoint/rollback:** Remove one facade at a time only after repository-wide
reference scans and a clean validation matrix.

## 14. First recommended implementation milestone

The first implementation should be **Phase 1 — semantic descriptor and launch
request adapters**.

It is valuable because it establishes the common vocabulary and makes later
changes converge on one contract. It is safe because it does not replace the
proven launch factories, windows, associations, or UEFI initialization order.

The milestone is complete when:

1. The 12 current descriptors project into a common immutable semantic shape.
2. A typed launch request can be created from an ID, alias, Start selection,
   shell object, or association without changing the selected backend.
3. A typed result/error code maps back to the existing boolean and UI error
   paths.
4. The old `AppCollection.Load(string)` and current direct file/GXM routes are
   compatibility facades over the new request path, or are explicitly kept as
   the backend behind it.
5. All existing UEFI and production-default validation remains green.

No full application conversion should be included in this milestone.

## 15. Validation performed for this architecture phase

Read-only inspection covered:

- C# repository identity, branch, HEAD, upstream, ahead/behind, and worktree;
- presence of the validated C# AppModel/AppRuntime changes and validation logs;
- Server main and Server v1.1 identity, branch, HEAD, upstream, lineage, and
  worktree state;
- other Server worktrees to distinguish mainline App Model from feature,
  architecture, visual, and tooling branches;
- Legacy identity and clean worktree for minimal provenance;
- Server manifest, validator, registry, launch target/resolver, built-in
  metadata, shell registry, desktop service, process/lifecycle, GXM/GXApp,
  NativeAOT runtime, and App Model documentation;
- C# AppModel, App, Desktop, Start, Taskbar, Window, WindowManager, GXM, and
  validation entry points.

The architecture relies on the supplied and existing runtime proof for the
current C# baseline. It does not claim that this phase re-ran QEMU/UEFI.

## 16. Change control

- Server modified: **No**.
- Legacy modified: **No**.
- C# runtime/code modified: **No**.
- Tracked architecture document added: **Yes**, this file.
- Commit created: **No**.
- Push performed: **No**.

The next coding phase should begin from the same clean C# baseline and treat
this document as the design checkpoint before adding the Phase 1 adapter.

