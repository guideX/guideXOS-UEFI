# guideXOS App Model Convergence

**Status:** Phase 6 cooperative lifecycle enrichment implemented
**Date:** 2026-09-16
**Scope:** guideXOS Server ↔ guideXOS C# UEFI application platform  
**Outcome:** Outcome A — all registered built-ins remain factory-native;
activation, cooperative suspension, close semantics, and compatibility remain
contained behind typed boundaries

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
| HEAD | `17bc27f479459d7525f792ed61deee68652f3f3a` |
| Subject | `Phase 5` |
| Upstream | `origin/main` |
| Ahead/behind | `0 / 0` |
| Remote | `git@github.com:guideX/guideXOS-Legacy-UEFI.git` |
| Worktree | Clean at preflight |

The Phase 4 work starts from this clean, synchronized mainline HEAD. The
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

## 16. Change control for the architecture investigation

- Server modified: **No**.
- Legacy modified: **No**.
- C# runtime/code modified during the architecture investigation: **No**.
- Tracked architecture document added: **Yes**, this file.
- Commit created: **No**.
- Push performed: **No**.

The next coding phase should begin from the same clean C# baseline and treat
this document as the design checkpoint before adding the Phase 1 adapter.

## 17. Phase 1 implementation status — semantic descriptor and launch-request adapters

Phase 1 is implemented in the C# UEFI repository as a compatibility seam over
the existing runtime.  The following common concepts now exist:

- `ApplicationDescriptor` and `ApplicationDescriptorRegistry` project the
  canonical twelve legacy registrations into bounded, read-only semantic
  metadata with stable IDs, aliases, resource keys, application class,
  associations, launch-entry metadata, and shell policy.
- `LaunchRequest` carries an application ID or alias, bounded arguments,
  document, verb, source shell-object ID, target kind, typed shell target, and
  activation intent without referencing `Window`, renderer state, Start, or
  taskbar objects.
- `LaunchResult` and `LaunchErrorCode` provide the bounded result vocabulary
  from this document.  `InstanceId` remains intentionally unset because the
  application-instance lifecycle is deferred.
- `ApplicationAssociationRegistry`, `ModernFileAssociationAdapter`, and
  `ModernShellAdapter` project the existing association and shell registries
  into common request/target values.
- `AppLaunchCompatibilityAdapter` resolves modern application requests and
  dispatches them to the existing `AppCollection` backend.  The old
  `AppCollection.Load(string)` remains the public compatibility facade.

The following are compatibility adapters only and still use legacy behavior:

- Start ordering and the twelve `App` objects remain the existing C# list.
- Built-in creation, window ownership, taskbar registration, focus return,
  helper-window reuse, and visible error UI remain in the existing
  `AppCollection` and `Desktop` implementations.
- File opening still uses the existing direct Notepad, Image Viewer, WAV
  Player, and GXM helpers after the request is constructed.
- GXM still uses `GXMLoader` and `GXMScriptWindow`; it is represented as a
  typed GXM document target rather than moved to a new backend.
- Shell-object execution still uses the existing Computer Files, Root, USB,
  and installer routes.  The typed shell request records the route context but
  does not replace its execution policy.

Phase 1 validation adds deterministic projection, alias, association,
document/argument/source preservation, unknown-target, malformed-request, and
typed shell-request checks to the AppModel self-test.  It does not introduce
the application-instance lifecycle, descriptor discovery, process support,
window migration, or taskbar/Start redesign.  Those remain Phase 2 and later
work.  The implementation must be considered complete only after the full
AppModel, AppRuntime, NativeInput, ContextMenu, and production continuous-boot
matrix is green; a host-side selector failure is reported separately from a
guest runtime regression.

## 18. Phase 2 implementation status — application instances and lifecycle

Phase 2 is implemented as a bounded semantic layer over the existing C#
window/application backend.  The defining relationship is now:

`ApplicationDescriptor -> ApplicationInstance -> zero/one/multiple Window`

### 18.1 Instance identity and registry

- `ApplicationInstance` is a first-class record with stable descriptor ID,
  policy, bounded launch context, lifecycle state, activation state, failure
  and termination diagnostics, and bounded owned-window relationships.
- `ApplicationInstanceHandle` is a packed slot/generation value.  Its public
  semantics are stable-handle based; descriptor IDs and instance handles are
  distinct, and generation validation rejects stale references after slot
  reuse.
- `ApplicationInstanceRegistry` uses 32 fixed slots.  Terminal instances are
  removed from active lookup, while cumulative counters preserve bounded
  diagnostics for created, reused, activated, terminated, failed, attached,
  detached, duplicate, and stale-ownership events.
- Lookup supports handle lookup, first/by-descriptor lookup, bounded count, and
  indexed enumeration by descriptor.  The registry is App Model state only;
  it is not a process manager or scheduler.

### 18.2 Lifecycle contract

The implemented states are `Registered`, `Loading`, `Initialized`, `Running`,
`Activated`, `Inactive`, `Suspended`, `Closing`, `Terminated`, and `Failed`.
Normal launches transition through `Registered -> Loading -> Initialized ->
Running -> Activated`.  Reusable instances can return to `Inactive` after a
window closes and re-enter loading/running/activated on reuse.  Activation and
deactivation are application-instance operations, independent of raw window
focus.  An active instance is tracked separately from the focused window.
Close requests invoke the instance adapter before `Closing -> Terminated`;
forced termination always cleans owned windows and removes the handle.  A
cooperative suspension follows `Inactive -> Suspended -> Inactive` and retains
instance identity, window ownership, and application/document state.  It does
not stop scheduler threads, disable kernel timers, block interrupts, or freeze
the WindowManager.  Callback failures and invalid/stale handles produce
bounded typed results and leave a deterministic valid state.

### 18.3 Window and taskbar ownership

`WindowManager` remains the graphical window owner.  Each semantically managed
window can carry an `ApplicationInstanceHandle`; the instance registry owns
the bounded relationship and WindowManager continues to own ordering,
focus, drawing, fade-close, and disposal.  Duplicate attachment is harmless
and counted.  A window focus/z-order event routes through the semantic owner,
so a multi-window application changes application state once rather than once
per window.  A window close detaches its instance; ordinary multi-instance
applications terminate when their final window closes, while reusable
Console/Image Viewer/WAV Player instances may remain inactive with zero
windows.  Instance termination closes/detaches all owned windows safely.

The taskbar visual model remains unchanged.  Its existing window entries now
reject stale semantic owners before presentation/activation and route a live
entry through its `ApplicationInstanceHandle`.  Inactive instances activate;
suspended instances resume and then activate; terminated/failed handles are
rejected.  Legacy unattached shell windows remain compatible.  This preserves
current taskbar behavior without introducing grouping UI.

### 18.4 Compatibility backend and policies

`LaunchRequest -> descriptor resolution -> ApplicationInstanceRegistry ->
existing AppCollection/Desktop backend -> window attachment -> lifecycle
completion -> LaunchResult` is the active launch path.  Built-in constructors
were not converted wholesale, and the old boolean and named launch APIs
remain available as compatibility facades.

The descriptor projection records `MultiInstance` for ordinary recreated
applications (including Calculator and Notepad) and `ReuseExisting` for
Console, Image Viewer, and WAV Player.  Reuse is semantic instance reuse even
when the legacy backend must recreate a disposed helper window.  Console keeps
its existing `FConsole` behavior and activation ordering without duplicate
ownership.  Image Viewer and WAV Player keep their lazy/reusable desktop
helpers, with resource disposal remaining in their existing safe paths.

Shell Computer Files and installer windows are also attached to bounded shell
instances.  Root mode remains a desktop mode change rather than a fabricated
window instance.  A failed shell/window attach closes the new window and
removes or safely inactivates the instance.

### 18.5 GXM mapping

GXM launches use the dynamic application identity `gxos.external.gxm` with
`MultiInstance` policy.  The instance retains the bounded GXM launch request
and document/path context; GUI execution attaches the resulting
`GXMScriptWindow`, while non-GUI execution retains instance lifecycle context
without inventing a built-in descriptor.  `GXMLoader` and
`GXMScriptWindow` remain the execution backend.

### 18.6 Validation and deferred work

The AppModel diagnostic includes deterministic identity, lifecycle, invalid
transition, reuse, multi-instance, capacity, failure-cleanup, zero-window,
attach/detach, duplicate-attach, and stale-handle checks.  AppRuntime emits
bounded instance/result/ownership counters and exercises repeated Calculator,
Notepad, and Console launch/close cycles plus association, shell, and GXM
routes.

Deferred work is full per-built-in migration away from the compatibility
factory, actual suspension/resume, dynamic package discovery or process
isolation, and visual taskbar grouping.  Those are intentionally outside
Phase 2 and do not block the bounded instance/lifecycle contract.

### 18.7 Phase 2 validation evidence

The final Phase 2 evidence was collected from the real UEFI guest and the
normal build path:

- Host build: `dotnet build guideXOS\\guideXOS.csproj --no-restore` passed with
  0 errors and the repository's existing warning set.
- AppModel: `APP_MODEL_INSTANCE_SELFTEST_OK=1`, capacity `32`, active `0`,
  stale ownership `0`, attach/detach `1/1` in the deterministic guest proof.
- AppRuntime: `APP_RUNTIME_COMPLETE` and validation `True`; 20 Start
  selections/launches, 29 window closes, 20 instance launch markers, Console
  reuse on one handle, GXM and installer instance close routes, all six
  association probes, five negative file failures, graphics valid, balanced
  input, and allocator corruption `0`.
- NativeInput: `TIMEOUT_SUCCESS`; keyboard and mouse drops were `0`, with
  balanced key and left-button transitions.
- ContextMenu: `CONTEXT_MENU_COMPLETE`; desktop opens/draws/good-bounds were
  `104/104/104`, with bad-bounds `0`.
- Production continuous boot: `TIMEOUT_SUCCESS`; five heartbeats, advancing
  frame/timer values, valid graphics, allocator corruption `0`, and
  `ThreadPool.Locked=0`.

The bounded AppRuntime workload also serves as the Phase 2 stress pass: three
additional Calculator/Notepad pairs, two additional Console launches, shell
and association routes, and close cleanup completed without registry growth.
The final guest state had no stale semantic window owners; reusable helper
instances were allowed to remain inactive with zero windows as specified.

## 19. Phase 3 — instance-aware application factories

Phase 3 introduces the bounded construction seam required for incremental
built-in migration.  The App Model now resolves the stable application
descriptor and owns the `ApplicationInstance`; it no longer needs a
descriptor-specific Window constructor for the migrated cohort.

### 19.1 Factory contract

`ApplicationFactory.TryCreateOrActivate` receives exactly the descriptor,
the App Model-owned `ApplicationInstance`, and the bounded `LaunchRequest`.
It returns an `ApplicationFactoryResult`, whose common success/failure
vocabulary is the existing `LaunchResult` plus a bounded list of zero to
eight window candidates.  The public contract does not expose framebuffer,
taskbar, Start menu, shell-control, or UEFI-global state.  A factory may
return zero, one, or multiple windows; the registry attaches them and the
App Model remains authoritative for lifecycle transitions and activation.

`LaunchRequest.WithTargetAppId` canonicalizes name/alias launches at the
backend boundary without duplicating or expanding the bounded arguments,
document, verb, source-shell-object, or activation-intent context.

### 19.2 Registration and lifecycle

`ApplicationFactoryRegistry` is a deterministic 32-entry descriptor-to-backend
binding table, not a second application identity registry.  Startup binds the
four Phase 3 cohort IDs in fixed order and rejects missing descriptors,
duplicate bindings, incompatible application classes, and capacity overflow
with bounded `LaunchResult` failures.

The factory path is:

`descriptor -> LaunchRequest -> ApplicationInstance(Loading) -> factory ->
window ownership attach -> Initialized -> Running -> Activated -> LaunchResult`.

Factory failure closes and detaches windows introduced by the attempt, removes
new failed instances, and leaves a reused instance inactive while preserving
its pre-existing owned windows.  The registry does not introduce a competing
lifecycle state machine.  The existing compatibility backend remains an
explicit fallback whenever a descriptor has no registered factory and is not
deprecated-for-removal yet.

### 19.3 Migrated cohort

| Application | Factory behavior | Proof target |
|---|---|---|
| Calculator | Creates one ordinary window per launch | Independent multi-instance handles and close ownership |
| Notepad | Creates a new window and passes `LaunchRequest.Document` to `OpenFile` | Start and `.txt` association launches |
| Console | Reuses `Program.FConsole` under the reusable instance policy | One instance/window owner and repeated activation |
| Image Viewer | Lazily reuses `Desktop.imageViewer`, decodes the request document, and transfers image ownership safely | `.png` association, reuse, and resource lifetime |

All other current built-ins continue through the compatibility backend.  That
includes Computer Files, Devices, Disk Manager, Display Options, Firewall,
Paint, Task Manager, and WAV Player.  The old direct switch is retained as a
temporary adapter for both those applications and the migrated applications'
fallback path.

### 19.4 Associations, Start, and GXM

The association path for Notepad and Image Viewer is now:

`file -> FileAssociationRegistry -> stable app ID -> LaunchRequest(Document) ->
ApplicationInstance -> registered factory -> owned window`.

Computer Files supplies `gxos.shell.computerfiles` to the desktop handoff, so
the request retains its source-shell identity as well as its document, verb,
and activation intent.  The association/UI layer still has no constructor
knowledge of either migrated application.

The Start path uses the same factory selection after descriptor resolution;
Start retains only its existing display list and does not construct a
Notepad, Calculator, Console, or Image Viewer window.  GXM remains a separate
typed `gxos.external.gxm` backend in this phase.  Future work should choose
between a generic external-application factory, a GXM-specific factory, or a
typed launch backend after the built-in cohort is larger; GXM is intentionally
not rewritten here.

### 19.5 Diagnostics and deterministic coverage

Conditional AppRuntime markers expose factory backend selection, registrations,
factory launches, compatibility fallbacks, factory failures, reused factory
activations, and windows attached per factory launch.  The AppModel diagnostic
also runs the factory self-test for registration/duplicate/incompatible/missing
bindings, document propagation, zero-window success, reused and multi-instance
semantics, failed cleanup, multiple-window ownership, lifecycle completion, and
stale generation protection.

### 19.6 Next migration cohort

The next bounded cohort should be selected after the Phase 3 UEFI matrix is
green.  The natural candidates are Paint and WAV Player: Paint exercises an
ordinary document-capable window once its descriptor association contract is
made explicit, while WAV Player exercises another reusable lazy helper.  Shell
objects and GXM remain separate until their backend ownership boundaries are
reviewed.

### 19.7 Validation evidence

The post-edit Phase 3 matrix is green on the real UEFI/QEMU path.  The AppModel
run reports 12 descriptors, four deterministic factory registrations, the
instance and factory self-tests, zero stale ownership, and zero active
instances at completion.  The AppRuntime run launches all 12 Start entries and
records 23 successful launches: 15 factory launches, eight explicit
compatibility fallbacks, three expected factory failures, four reused factory
activations, and 15 attached factory windows.  The `.txt` and `.png` routes
carry their documents through the stable IDs and return `Activated` instances;
Console reuses one instance and one `FConsole` window.

NativeInput passed with zero dropped input and balanced keyboard/mouse state;
ContextMenu passed with 104/104 bounded desktop opens/draws and zero bad
bounds; and the production continuous boot passed with advancing frames/timer
and valid graphics invariants.  The Image Viewer proof is direct Start launch,
close, PNG association reopen/decode for both `Images/audiopause.png` and
`Images/audioplay.png`, close, and repeated failure-path probes.  All runs
reported allocator corruption zero, no runtime faults, and no stale taskbar or
instance ownership.

## 20. Phase 4 — complete built-in factory migration

Phase 4 moves the remaining eight registered built-ins behind the explicit
instance-aware factory boundary.  The normal built-in path is now:

`descriptor -> LaunchRequest -> ApplicationInstance -> ApplicationFactory ->
owned Window(s)`.

The compatibility adapter remains available only for legacy-only names,
historical callers, and an explicitly selected external compatibility route.
It is no longer selected when a registered built-in descriptor is resolved.
The descriptor/factory validation boundary returns a bounded
`BackendUnavailable` result for a missing or class-incompatible built-in
binding.

### 20.1 Built-in bindings and policies

| Application | Stable descriptor | Factory policy | Ownership/resource notes |
|---|---|---|---|
| Calculator | `gxos.builtin.calculator` | `MultiInstance`; close last window | One fresh ordinary window per launch |
| Computer Files | `gxos.builtin.files` | `MultiInstance`; close last window | Root and drive-specific filesystem objects are factory-owned; drive child windows own their per-window filesystem |
| Console | `gxos.builtin.console` | `ReuseExisting`; retain zero-window instance | Reuses the existing `FConsole` helper and one semantic owner |
| Devices | `gxos.builtin.devices` | `MultiInstance`; close last window | Existing device-management UI and failure behavior retained |
| Disk Manager | `gxos.builtin.diskmanager` | `MultiInstance`; close last window | Existing disk/resource probing retained |
| Display Options | `gxos.builtin.displayoptions` | `ReuseExisting`; close last window | Recreates after close; request bounds are bounded and optional |
| Firewall | `gxos.builtin.firewall` | `ReuseExisting`; retain zero-window instance | Persistent firewall helper is recreated if its window was removed |
| Notepad | `gxos.builtin.notepad` | `MultiInstance`; close last window | `LaunchRequest.Document` is passed to the existing document loader |
| Paint | `gxos.builtin.paint` | `MultiInstance`; close last window | Fresh ordinary window; Paint internals are unchanged |
| Task Manager | `gxos.builtin.taskmanager` | `ReuseExisting`; close last window | Recreates after close; task/runtime enumeration remains read-only to lifecycle |
| Image Viewer | `gxos.builtin.imageviewer` | `ReuseExisting`; retain zero-window instance | Lazy helper and decoded-image ownership remain in the existing viewer path |
| WAV Player | `gxos.builtin.wavplayer` | `ReuseExisting`; retain zero-window instance | Lazy helper; document activation fails boundedly without audio or a valid WAV |

### 20.2 Association and shell backends

All built-in file associations now resolve stable typed targets and enter the
factory path where applicable:

| Extension | Target |
|---|---|
| `.txt` | `gxos.builtin.notepad` factory |
| `.png`, `.bmp` | `gxos.builtin.imageviewer` factory |
| `.wav` | `gxos.builtin.wavplayer` factory |
| `.gxm`, `.mue` | typed external `gxos.external.gxm` backend |

Computer Files, File Explorer, Root, and USB Drive shell resolution retain
typed shell-object IDs.  Computer Files and drive children produce the same
descriptor/request handoff and no longer construct `ComputerFiles` in shell
navigation code.  `Install to Hard Drive` is a typed `HDInstaller` shell-action
backend with a shell-owned instance; it is intentionally not a built-in
descriptor because it is an action target rather than a Start-visible
application.

GXM remains a separate typed external backend and is not counted as a built-in
factory or compatibility fallback.

### 20.3 Failure and registry contract

Factory failures return bounded `LaunchResult` values.  A failed fresh launch
closes/detaches every window introduced by that attempt and removes its
instance.  A failed reusable launch leaves the stable instance inactive and
does not destroy pre-existing owned resources.  WAV Player specifically keeps
the reusable instance at zero windows after unavailable-device failure, and a
malformed/undecodable buffer is consumed without replacing the prior valid
song state.

The bounded factory registry has capacity 32 and exactly 12 built-in bindings.
Self-tests cover stable-ID lookup, duplicate rejection, descriptor/factory
class mismatch, missing built-in bindings, capacity overflow, typed-backend
distinction, document propagation, and failed cleanup.

### 20.4 Compatibility scope and diagnostics

Diagnostics distinguish `factory`, `typed-external`, `typed-shell-action`, and
`compatibility` backend selection.  The canonical AppRuntime workload is
expected to report zero compatibility fallbacks for normal built-in launches;
GXM and installer counts are reported separately.  Remaining compatibility
construction is limited to `App.LoadLegacyBackend` and explicit historical
compatibility callers.  Direct constructor dispatch is not part of the
registered built-in normal path.

### 20.5 Phase 4 validation and next target

The Phase 4 validation matrix covers the twelve real Start entries, repeated
launch/close and reuse, Paint, WAV failure semantics, Computer Files aliases
and filesystem navigation, Task Manager observation, all system-management
applications, all six association targets, shell routes, installer action,
registry failures, AppModel, AppRuntime, NativeInput, ContextMenu, and
production continuous boot.  The required invariants remain allocator
corruption zero, no exhaustion, `ThreadPool.Locked=0`, valid graphics, balanced
input, no stale instance ownership, and no stale taskbar entries.

Real suspend/resume remains deferred.  The next convergence target is removal
of the remaining legacy application construction surfaces after compatibility
callers have been inventoried and migrated; process isolation and scheduler,
allocator, boot, GXM, and UI redesign remain out of scope.

## 21. Phase 5 — legacy launch-surface inventory and containment

**Status:** Complete for the C# UEFI application layer
**Date:** 2026-09-15
**Scope:** guideXOS C# UEFI callers and compatibility boundary only; no Server or
Legacy source changes

Phase 5 inventories the remaining string-based and direct-construction launch
surfaces, migrates normal callers to typed requests and registered factories,
and makes the retained compatibility boundary measurable.  The migration does
not remove public legacy symbols or historical backends; it confines them to
explicit compatibility and internal implementation roles.

### 21.1 Inventory and disposition

| Surface | Classification | Phase 5 disposition |
|---|---|---|
| `AppCollection.Load(string)` | Compatibility facade | Retained as a public shim; resolves aliases, translates registered names to `LaunchRequest`, and reports bounded failures. |
| `AppLaunchCompatibilityAdapter` | Compatibility facade | Modern-first translation; legacy dispatch is reached only when a retained historical request cannot resolve to a registered factory. |
| `LoadLegacyBackend` and `DispatchToLegacyBackend` | Legitimate legacy compatibility | Retained behind the facade as the single historical backend switch. Direct normal built-in callers were removed. |
| Start menu, taskbar, taskbar menu, desktop, association, and shell launch routes | Modern production callers | Use `Desktop.LaunchApplication`, typed association requests, typed shell-object IDs, or typed shell actions. |
| FConsole `notepad`/`launchscript`, ModuleManager Notepad, and GXMScript `OPENAPP` | Modern production callers | Use the canonical launch helpers; external GXM execution uses the typed external backend. |
| `GXMLoader` in typed external backends and `GXMScriptWindow` | Internal implementation | Kept only below the typed GXM boundary or for the script window's own execution engine. No normal caller uses it as a launch API. |
| Application factory constructors | Internal implementation | Centralized in `ApplicationFactories`; normal callers select descriptors, policies, and requests rather than constructors. |
| `USBDrives`/`USBFiles` list objects | Internal shell implementation | Not registered applications; shell list rendering remains local, while launch/navigation routes use typed Computer Files requests. |
| Unknown-name compatibility probe in `Program` | Test-only | Deliberately exercises the retained facade failure path and is excluded from production launch accounting. |

The remaining direct constructor and loader matches are therefore classified as
internal implementation or compatibility code, not unresolved normal launch
callers.  No Server or Legacy source file was changed.

### 21.2 Retained APIs and future deprecation candidates

The public `App`, `AppCollection.Load`, `AppCollection.Add`, descriptor/name
aliases, and `AppKind`/`AppDescriptor` compatibility shapes remain available.
They are not removed in Phase 5 because historical consumers may still depend
on them.  The internal `LoadLegacyBackend` and
`DispatchToLegacyBackend` methods remain the only compatibility construction
boundary.  Future deprecation candidates are the string-only `Load` facade,
legacy descriptor aliases, and the adapter itself, after external consumers are
audited; the typed `LaunchRequest`/`LaunchResult`/instance/factory contract is
the intended replacement.

### 21.3 Compatibility counters and self-test

`AppModelCompatibilityDiagnostics` records facade calls, successful modern
translations, genuine legacy-backend invocations, and compatibility failures.
The AppModel self-test performs two successful historical loads and one invalid
load, verifies instance handles and cleanup, and asserts that no legacy backend
or compatibility fallback is used for the registered built-ins.  Its expected
fresh-run values are:

| Counter | Expected |
|---|---:|
| Compatibility facade calls | 3 |
| Modern translations | 2 |
| Legacy backend calls | 0 |
| Compatibility failures | 1 |

The one failure is intentional: it is the bounded rejection of the invalid
self-test name.  The canonical AppRuntime workload reports zero compatibility
fallbacks and zero legacy-backend markers for normal production launches.

### 21.4 Exact remaining legacy-backend scope

`DispatchToLegacyBackend` is reachable only from the compatibility adapter for
an unresolved retained historical route, such as a legacy extension entry or a
caller that cannot resolve a registered descriptor.  Registered built-in names
are validated against their factory bindings before any compatibility fallback;
the normal count is therefore zero.  The historical GXM Hello/Minimal demo
compatibility path remains an internal legacy-only helper and is not a normal
production application launch surface.

### 21.5 Typed external, shell, and installer coverage

`.gxm`/`.mue` association and script-generated GXM launches now enter the
typed external GXM backend with an explicit document/source identity.  Computer
Files, File Explorer, Root, and USB routes carry typed shell-object IDs.
`Install to Hard Drive` remains a typed shell action with a shell-owned
instance.  These routes are intentionally outside the twelve registered
built-in factory descriptors while still using the same bounded launch/result
and ownership semantics.

### 21.6 Phase 5 validation evidence

The current real UEFI/QEMU matrix is green:

| Validation | Result |
|---|---|
| AppModel | 12 descriptors / 12 factories / 0 fallbacks; compatibility 3 calls / 2 translations / 0 legacy / 1 expected failure; self-test passed |
| AppRuntime | 20 Start selections / 28 launches / 30 closes; 28 factory launches / 0 fallbacks / 4 expected missing-file failures; typed external 2; typed shell action 1; 0 runtime faults |
| NativeInput | 0 dropped keyboard events, 0 dropped mouse events, balanced 104/104 key and 54/54 left-button transitions |
| ContextMenu | 104/104 desktop opens/draws, 104/104 good bounds, 0 bad bounds; balanced 105/105 right-button transitions |
| Production continuous boot | Advancing heartbeats/timer, valid graphics invariants, no allocator corruption marker |

All runs ended without stale application-instance ownership.  The validation
logs are retained as `serial_phase5_appmodel_recheck3.txt`,
`serial_phase5_appruntime_recheck.txt`, `serial_phase5_nativeinput_recheck.txt`,
`serial_phase5_contextmenu.txt`, and `serial_phase5_continuous.txt`.

### 21.7 Phase 6 gate

Phase 5 is complete and the compatibility surface is contained.  Phase 6 may
proceed to lifecycle enrichment—especially richer instance state and explicit
activation/close semantics—without first removing the retained facade.  Removal
or deprecation of the facade should remain gated on an external-consumer audit.

## 22. Phase 6 — activation, suspension, and close semantics

Phase 6 turns the modeled lifecycle into a bounded runtime contract while
preserving the existing factory and WindowManager architecture.  The central
rule is:

> Current C# App Model suspension is cooperative application lifecycle
> suspension, not scheduler/process freezing.

### 22.1 Lifecycle contract types

`ApplicationLifecycleAdapter` is the small application-facing base contract.
It supplies safe defaults for `OnActivating`, `OnDeactivating`,
`OnSuspending`, `OnResuming`, `OnCloseRequested`, `OnTerminating`, and
`OnLifecycleFailure`.  Applications that do not need custom behavior inherit
the default adapter; they do not implement a large Window-facing interface.

`ApplicationLifecycleCapability` distinguishes `Unsupported`,
`SupportedWithDefault`, and `SupportedWithCustom`.  The representative
Calculator, Notepad, Console, and Image Viewer factories install bounded
custom adapters.  Their existing windows and document/resource state remain
the source of truth; lifecycle callbacks do not duplicate or dispose that
state.  The remaining built-ins use the allocation-free safe default behavior.

### 22.2 Request/result model

`ApplicationLifecycleRequest` carries an operation, generation-safe instance
handle, and bounded `ApplicationCloseReason`.  Registry operations return
`ApplicationLifecycleResult`, whose code is one of `Success`, `Unsupported`,
`InvalidState`, `Cancelled`, `NotFound`, or `CallbackFailed`.  Results also
carry the instance handle, from/to state, close reason, and a bounded
diagnostic.  This keeps lifecycle callers independent from graphical controls
and leaves room for a future isolated-process implementation.

### 22.3 Activation and deactivation

`ApplicationInstanceRegistry.ActiveApplicationHandle` tracks the semantic
foreground owner separately from the raw focused Window.  Activation is
idempotent for an already active instance.  Activating another instance first
deactivates the previous application, invokes the target adapter, transitions
it through `Running -> Activated` where necessary, and routes one appropriate
owned Window to the front.  Taskbar, shell, and factory routing report semantic
foreground ownership explicitly, so a multi-window application is activated
or deactivated as one instance.  Graphical z-order changes remain independent
from lifecycle state, and focus changes never terminate an application.

The Start shell calls `NotifyShellForeground`; it is a foreground owner but is
not represented as a fake built-in descriptor.  Closing an active application
clears the active handle and returns ownership to the shell/desktop.

### 22.4 Cooperative suspend and resume

Suspension may deactivate an active instance, invokes `OnSuspending`, and
transitions it to `Suspended` without hiding or destroying owned windows.
Resume is valid only from `Suspended`, invokes `OnResuming`, and returns the
instance to `Inactive`; a later activation is explicit.  The default adapter
does no global timer, interrupt, scheduler, or lock operation.  It retains
application state and leaves the operating system responsive.  A backend that
cannot provide a safe callback returns `Unsupported` and retains its valid
state.

GXM is currently classified as activation/deactivation/close/termination
capable but suspend/resume unsupported: `GXMScriptWindow` has no safe
execution pause/resume primitive in this managed backend.  The common result
contract reports `Unsupported` rather than pretending that GXM execution was
frozen.

### 22.5 Close reasons and termination

Close requests use only the bounded reasons `UserRequest`, `ShellRequest`,
`ApplicationRequest`, `Shutdown`, `Failure`, and `ForcedTermination`.  A
close callback may accept, cancel, or fail.  Accepted ordinary closes proceed
through the existing WindowManager fade/disposal cleanup; forced termination
always removes the instance and cleans all owned windows, even if its
termination callback reports failure.  Cancellation leaves the current
non-terminal state intact.

### 22.6 Taskbar and representative applications

Taskbar activation targets the semantic instance behind the clicked window.
Inactive entries activate, suspended entries resume and activate, and stale
or terminal handles are rejected.  No grouped multi-window presentation was
added.

The lifecycle self-test covers activation idempotence, deactivation,
supported and unsupported suspend, resume invalid-state handling, close
acceptance/cancellation, callback failure, stale handles, zero-window reuse,
multi-window retention, termination after suspension, and final registry
cleanup.  The bounded runtime diagnostic uses normal Calculator, Notepad,
Console, and Image Viewer factory launches: it checks Calculator deactivation
when Notepad becomes foreground, keeps the Notepad document through suspend /
resume, exercises Console reuse, validates Image Viewer ownership, and closes
everything before returning to the shell.  Task Manager remains an observer;
its enumeration path does not mutate lifecycle state.

### 22.7 Future process isolation compatibility

No Ring 3 or process isolation was implemented.  The public contract can map
`Suspend` and `Resume` to process/task suspension and `Terminate` to isolated
process termination in a future backend without changing callers.  The
current adapter boundary deliberately keeps those stronger mechanisms out of
the C# implementation.
