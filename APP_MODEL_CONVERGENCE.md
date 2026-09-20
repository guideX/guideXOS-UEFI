# guideXOS App Model Convergence

**Status:** Phase 11 session-global text clipboard complete; Phase 10 remains accepted Outcome B
**Date:** 2026-09-19
**Scope:** guideXOS Server ↔ guideXOS C# UEFI application platform  
**Outcome:** Outcome B remains the accepted Phase 10 storage result — applications
request logical packaged resources and application-scoped storage through
bounded platform services; the live UEFI backend honestly reports persistent
writes/deletes unavailable while temporary storage remains real, bounded, and
AppId-scoped. Phase 11 adds the bounded session-global text clipboard service.

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

## 23. Phase 7 — application-centric taskbar projection

Phase 7 adds an application-entry projection without moving lifecycle or
Window ownership out of `ApplicationInstanceRegistry`.  The registry remains
authoritative for instance existence, generation-safe handles, lifecycle
state, owned Windows, activation, suspension/resume, and close policy.
`TaskbarApplicationEntryRegistry` is a bounded, read-only presentation cache:
it reconciles from the registry and each instance's owned-Window slots, and it
does not create instances, own Windows, or become a second source of truth.

### 23.1 Semantic groups and visual buttons

Each live application instance with at least one presentable owned Window
projects to exactly one semantic taskbar entry.  An entry is keyed by the
generation-safe `ApplicationInstanceHandle`, carries the descriptor/display
metadata and lifecycle flags, and contains the bounded owned-Window view.
Multiple same-descriptor launches therefore produce independent entries, not
one descriptor-wide group.  A single instance may own multiple Windows: they
remain separate visual taskbar Window buttons while sharing one semantic
application group and active-group state.  The renderer reconciles before
presentation and retains raw Window enumeration; Phase 7 does not collapse
buttons or add a picker.

The bounds are explicit: the registry has 32 instance slots and each instance
and projection entry has 8 owned-Window slots.  Reconciliation uses fixed
arrays and bounded scans, records attach/detach, reuse, suppression, stale
owner, multi-Window-group, and maximum-group-size diagnostics, and never
allocates a collection or application instance during projection.

### 23.2 Activation, focus, and deterministic target selection

Taskbar activation is an application-instance operation followed by an
explicit Window-focus operation.  It is distinct from arbitrary z-order or
raw focus changes: `MoveToEnd` remains lifecycle-neutral, while a real
taskbar/Window selection calls the semantic route.  Selecting another Window
within the active instance changes the selected Window without a redundant
deactivate/activate lifecycle transition.  Selecting a Window owned by another
instance activates that instance, deactivates the previous semantic owner,
and then focuses the selected Window.

When a taskbar route does not provide an explicit Window, target selection is
deterministic: retain the current presentable selection, otherwise the
most-recent presentable Window, otherwise the first presentable owned Window.
Minimized targets are restored before focus.  Invalid, stale, terminal,
unowned, hidden, or non-taskbar Windows are rejected with the bounded
lifecycle result; stale projection state is reconciled before rejection and
cannot retarget a reused handle.

Suspended activation resumes the instance through the lifecycle registry and
then focuses its target.  A backend that cannot safely resume returns
`Unsupported`; it is not reported as a successful activation.  Application
Close is routed with `ShellRequest` to the instance and is separate from a
Window title-bar close.  Closing one Window leaves the instance/group and its
other presentable Windows intact.  Closing the final Window follows the
instance's `CloseWhenLastWindowClosed` policy.  Reusable zero-Window
instances remain registered but are suppressed from the taskbar until a
presentation Window is relaunched or reattached; multi-instance launches
remain independently addressable.

### 23.3 Observation and future process compatibility

Task Manager is read-only.  Its summary/diagnostic path uses only
`ObservationCount` and `TryGetObservationAt`; it does not activate, close,
reorder, mutate lifecycle state, or alter Window ownership.  The observation
diagnostic snapshots registry, lifecycle, ownership, Window, and projection
counters before and after enumeration and requires no mutation.

The instance and lifecycle contracts remain suitable for a future Ring 3 or
process-backed implementation: the same generation-safe handle and semantic
operations can map activation, suspension/resume, and termination to an
isolated process backend.  Phase 7 itself adds no Ring 3 scheduler/process
freezing and keeps the current cooperative managed behavior.

Richer visual grouping, collapsed buttons, thumbnails, and a Window picker
are explicitly deferred.  The current UI continues to show separate Window
buttons; only the semantic application-entry projection and activation/close
routes are added.

### 23.4 Phase 7 proof markers and gates

The AppModel diagnostic preserves all Phase 6 markers and adds the grouping
self-test, projection, observation, and factory counters.  The grouping
self-test must emit a positive pass count with zero failures, and its cleanup
must leave no stale owners or projection entries.  The AppRuntime diagnostic
preserves the lifecycle proof and adds the bounded multi-Window/same-instance/
cross-instance proof, read-only observation proof, and strict cleanup gate.
The required proof forms are:

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

These are regression gates, not replacement diagnostics: a missing, stale,
or failing marker blocks the Phase 7 claim, while existing Phase 6 lifecycle,
factory, input, graphics, allocator, and context-menu checks remain required.

### 23.5 Phase 7 runtime acceptance (2026-09-17)

Phase 7 is accepted against a fresh NativeAOT/QEMU UEFI runtime image built
from the Phase 7 source.  The deterministic AppModel grouping self-test
reported `passed=16;failed=0`, and the AppRuntime grouping workload reported
positive proof for one-instance/two-Window grouping, separate Window buttons,
same-instance Window switching with activation delta `0`, distinct Calculator
instance handles and entries, cross-instance activation/deactivation, accepted
application-level Close, first-Window retention, final-Window policy,
zero-Window projection suppression, Console reuse, and Image Viewer reuse.
The runtime observation diagnostic remained read-only and reported no mutation.

The complete AppRuntime workload launched all 12 Start-visible applications,
completed 20 Start selections, 28 launches, and 30 closes, and exercised the
typed GXM and installer routes, six positive associations, five bounded
negative association failures, Console reuse, Image Viewer reuse, and cleanup.
It completed with factory fallbacks `0`, runtime faults `0`, stale taskbar
owners `0`, stale application ownership `0`, allocator corruption `0`, and
`ThreadPool.Locked=0`.  The final raw input counts were keyboard `11/11` and
mouse-left `49/49`, with zero dropped input bytes and valid graphics.

The independent regression matrix was also green:

| Validation | Result | Evidence |
| --- | --- | --- |
| AppModel / Phase 6 lifecycle | Pass | 12 descriptors, 12 factories, lifecycle `15/0`, grouping `16/0`, active `0`, stale `0` |
| AppRuntime | Pass | 20 selections / 28 launches / 30 closes; factories `28/0/4`; typed external `2`; typed shell `1` |
| NativeInput | Pass | keys `104/104`, mouse-left `54/54`, drops `0`, frames `1→300`, graphics valid |
| ContextMenu | Pass | desktop `104/104/104`, bad bounds `0`, taskbar `1/1/1`, right transitions `104/104`, raw `104/104` |
| Production continuous boot | Pass | non-diagnostic image; frame/timer progress; graphics, allocator, and ThreadPool gates valid |

The prior validation blocker was environmental/generated state, not a Phase 7
source regression.  A current bootloader-only rebuild found the C++ linker
targets available and linked successfully.  The old `-SkipBuild` ESP still
contained an AppModel diagnostic payload, so a requested continuous run
stopped at `SMAIN_DISPATCH_REASON=APP_MODEL`; this was the stale-ESP cause.
Every acceptance selector was subsequently run with a fresh build, and the
final image was rebuilt without a diagnostic selector.  No linker-target or
ESP-copy source redesign was required.

Two bounded validation fixes were required: the runtime grouping diagnostic
now reconciles the presentation projection before observing a freshly
reattached reusable Window, and the QMP workload holds mouse buttons long
enough for the slow blur-backed guest to observe both transitions, with an
explicit final release for ContextMenu.  These changes affect diagnostics and
validation only; application lifecycle, ownership, taskbar architecture, and
production defaults are unchanged.  No visual group-collapse work was added.

## 24. Phase 8 — application platform services audit

The Phase 8 audit was performed against the Phase 7 accepted C# App Model and
the reference Server tree on 2026-09-18.  The audit is source-located so the
service boundary follows existing authority and ownership rather than creating
a second application lifecycle system.

### 24.1 Authoritative source inventory

The Server tree provides the following relevant shapes:

* `D:\dev\guideXOSServer\notification_manager.h` and
  `notification_manager.cpp` define a synchronous, compositor-facing
  notification queue with `Add`, `Update`, `Snapshot`, `Clear`, and `Count`.
  Notifications contain presentation/animation state owned by the manager;
  there is no application-service context in this API.
* `D:\dev\guideXOSServer\app_manifest.h` and
  `app_manifest_validator.cpp` define manifest permissions and validate a
  fixed vocabulary including filesystem, window, input, log, and
  `system.settings` names.  The audit found no unified service-capability
  enforcement path that can be reused by this same-address-space C# phase.
  `PermissionDenied` therefore remains contract vocabulary only; Phase 8 does
  not invent enforcement semantics.
* `D:\dev\guideXOSServer\message_box.h`, `open_dialog.h`, and
  `save_dialog.h` expose process/dialog entry points.  Open and save dialogs
  own VFS navigation and callbacks; they are not bounded application services.
* `D:\dev\guideXOSServer\vfs.h` exposes the in-memory/reference VFS singleton,
  while `kernel/core/file_clipboard.h` exposes a synchronous file-operation
  clipboard with explicit operation/progress state.  Neither is an
  application-local storage or text-clipboard contract.
* `D:\dev\guideXOSServer\app_launch_target.h` and
  `app_launch_resolver.h/.cpp` define typed launch targets and shell/association
  resolution.  They are launch infrastructure, not a bounded shell/open
  service available to this phase.

The C# tree provides the following current consumers:

* `guideXOS/GUI/NotificationManager.cs` owns the managed toast list and
  renderer-facing `Notify` objects.  Calculator and DisplayOptions call
  `NotificationManager.Add` directly.
* `guideXOS/DefaultApps/Notepad.cs` owns `_wrap` directly and also owns its
  text, dirty state, undo/redo state, dialogs, and file callbacks.  The
  existing `guideXOS/OS/Configuration.cs` and `SystemMode.cs` are global
  persistent/system settings infrastructure and are intentionally not reused
  for session application settings.
* `guideXOS/DefaultApps/TaskManager.cs` reads `Timer.Ticks`,
  `Allocator.MemorySize`, `Allocator.MemoryInUse`, `ThreadPool.CPUUsage`, and
  `ThreadPool.ThreadCount` directly in its metrics path.  The implementation
  will replace only the selected metrics sample with copied system snapshots;
  observer, chart, and presentation state remain local.
* `guideXOS/GUI/OpenDialog.cs`, `SaveDialog.cs`, and `MessageBox.cs` are UI
  objects with callbacks and window ownership.  Notepad and DisplayOptions
  use open/save dialogs, and Notepad uses direct file I/O for document state.

### 24.2 Server-to-C# convergence matrix (Phase 8 historical snapshot)

This table records the Phase 8 boundary at the time dialogs and shell/open
were intentionally deferred.  Phase 9 supersedes its dialog and shell/open
rows with the final cohort and migration matrix in §25.2.

| Service | Server source | C# source | Classification | Common semantics | Backend difference | Migration value | Risk | Decision |
|---|---|---|---|---|---|---|---|---|
| Notifications | `notification_manager.h/.cpp`, `NotificationManager::Add/Clear` | `guideXOS/GUI/NotificationManager.cs`, Calculator, DisplayOptions | Same idea / different API | bounded, transient user-visible notification | Server queue stores source-less compositor notifications; C# stores renderer objects and animation fields | High; proves a shell/UI-backed typed service | Low if source tagging stays in the existing manager | Cohort |
| Session application settings | No authoritative Server application-settings service; manifest `system.settings` permission only | `guideXOS/OS/Configuration.cs`, `SystemMode.cs`, Notepad `_wrap` | Missing service / adjacent global settings | mutable session values addressed by application identity | Server evidence is permission metadata, while C# Configuration is global and persistence-aware | High; proves application-scoped mutable state without persistence | Medium; must not become global configuration or instance state | Cohort; fixed session-only store keyed by descriptor ID |
| System information | No bounded Server application-facing snapshot contract found; allocator/process/thread sources are backend internals | TaskManager direct `Timer`, `Allocator`, and `ThreadPool` reads | Missing service / raw backend access | read-only scalar OS/runtime metrics | Server has kernel/runtime sources; C# currently exposes globals directly to an app | High; proves copied kernel-backed values and future serialization shape | Medium; bounds and metric consistency must be explicit | Cohort; immutable snapshot |
| Dialogs | `message_box.h`, `open_dialog.h`, `save_dialog.h` | `GUI/MessageBox.cs`, `OpenDialog.cs`, `SaveDialog.cs` | Same broad feature / different ownership | user interaction with callback/result | Server dialogs are process entry points; C# dialogs own managed windows and callbacks | Low for this phase; no service contract is needed to prove the cohort | High; shell/VFS/UI lifetime and callback ownership are larger than the selected boundary | Deferred |
| App-local storage | `vfs.h`, filesystem and process-facing VFS users | Notepad and other apps use `FS.File`/`System.IO.File` directly | Backend exists / no bounded app-service contract | application file reads/writes | Server VFS is a filesystem singleton; C# callers use local filesystem adapters and paths | Low for this phase; persistence is explicitly deferred | High; requires identity, quotas, permissions, and persistence policy | Deferred |
| Resources | manifest entries and packaged `resources/` content, for example Server ResourceViewer sample | C# resource reads are direct file/image loads in app and GUI code | Related packaging / no service contract | read-only packaged assets | Server package metadata and VFS differ from C# embedded/managed asset access | Low for this phase | Medium; packaging and lifetime semantics are not settled | Deferred |
| Clipboard | `kernel/core/file_clipboard.h` and `file_clipboard.cpp` | No selected bounded application text/file service; shell/file surfaces use their own paths | Different feature / backend-owned operation state | transfer or copy/paste state | Server clipboard is file-operation state tied to VFS; it is not a text service | Low for this phase | High; mutation, progress, conflict, and ownership semantics are broad | Deferred |
| Shell/open services | `app_launch_target.h`, `app_launch_resolver.h/.cpp`, shell/object registry | `ApplicationFactories.cs`, launch resolver, associations, and direct dialog/file paths | Existing launch infrastructure / not a service cohort | resolve and dispatch a target | Server owns typed target resolution; C# already has App Model launch/factory routes | Low for this phase; Phase 7 launch authority is already accepted | High; expanding it would mix launch, association, shell, and service contracts | Deferred |

### 24.3 Audit decisions and bounds

The implementation will add a fixed C# `ApplicationServiceRegistry` with only
Notifications, application-scoped session settings, and System Information.
`ApplicationInstanceRegistry` remains authoritative for instance existence,
generation, descriptor identity, lifecycle, windows, and cleanup.  A service
context is only a validated capability-shaped value; every operation
revalidates its handle and descriptor identity.

The first implementation uses explicit bounds: descriptor/application IDs are
96 characters, notification title/body are 64/256 characters, settings have
32 application namespaces with 16 keys each, settings keys are 64 characters,
string values are 256 characters, system text fields are 32/32/16 characters,
and service diagnostics are 192 characters.  Settings are session-only and
keyed by stable descriptor ID, so two Notepad instances share the `wrap`
namespace while their documents, undo/redo state, dialogs, and transient
runtime state remain instance-local.  System information is returned as a
copied immutable scalar snapshot, never as an allocator, timer, thread-pool,
Desktop, WindowManager, framebuffer, or other kernel-global object.

The audit does not authorize persistence, IPC, Ring 3, generic dependency
injection, dialogs, app-local storage, resources, clipboard, or shell/open
service implementation in Phase 8.  Those remain inventoried and documented
until a smaller bounded contract and an independent authority decision exist.

### 24.4 Implemented application-service access model

The approved first cohort is implemented in
`guideXOS/OS/ApplicationServices.cs`,
`guideXOS/OS/ApplicationServiceRegistry.cs`, and
`guideXOS/OS/ApplicationServiceBackends.cs`:

* Phase 8 established the fixed table and exactly three registered services:
  `Notifications`, `Settings`, and `SystemInformation`.  Phase 9 extends the
  same registry to seven services; the Phase 8 count is retained here only as
  historical evidence.
* `ApplicationServiceContext` carries a generation-safe
  `ApplicationInstanceHandle`, stable descriptor/application identity, and a
  bounded capability-name copy.  It is a validated capability-shaped value,
  not a second authority or ownership registry.
* `ApplicationServiceAccess` exposes only typed service properties.  The
  adapters use abstract base contracts rather than interfaces because the
  custom bare-metal NativeAOT configuration does not support
  `RhpInitialDynamicInterfaceDispatch`; this preserves typed substitution
  without adding unsupported runtime dispatch.
* `ApplicationServiceResult` and `ApplicationServiceResult<T>` expose bounded
  result codes and 192-character diagnostics.  `PermissionDenied` remains
  vocabulary only; Phase 8 does not invent same-address-space permission
  enforcement from the capability field.

Every backend call revalidates the context against
`ApplicationInstanceRegistry`, including the live generation, descriptor ID,
and lifecycle state.  Stale, mismatched, terminated, failed, closing, and
suspended contexts are rejected.  The common validator allows the service
backend to impose a narrower mask: system information accepts Loading,
Initialized, Running, Activated, and Inactive; notifications and settings
accept Initialized, Running, Activated, and Inactive.  Thus factory-created
contexts may be carried while an instance is Loading, but notification and
settings calls do not become eligible until initialization has completed.

### 24.5 First-cohort contracts and migrations

Notifications are bounded to a 64-character title and 256-character body.
The C# adapter maps the request to the existing `NotificationManager`, adding
an internal stable application source tag so `Clear` removes only that
application's service-created notifications.  Renderer, animation, and
`Notify` objects remain manager-owned.  Calculator and Display Options now
receive typed service access from their factories and no longer call
`NotificationManager.Add` directly.

Session application settings are bounded to 32 application namespaces, 16
keys per namespace, 64-character keys, and 256-character strings.  The store
contains only Boolean, Int32, and bounded string values, is keyed by stable
descriptor/application identity, and is shared by all valid instances of that
identity.  It is cleared only by controlled diagnostic service reset and has
no filesystem, `Configuration`, `SystemMode`, or persistence path.  Notepad
loads and updates `wrap` through `ApplicationServiceAccess.Settings`; its
document, dirty flag, undo/redo stacks, dialogs, windows, and other transient
runtime state remain instance-local.

System information is returned as a copied immutable
`SystemInformationSnapshot` containing uptime ticks, memory total/usage,
thread count, CPU percentage, and bounded `guideXOS`/`Phase8`/`x86_64`
identity strings.  CPU is clamped to 0..100 and used memory is bounded by the
reported total.  The service never returns allocator, timer, thread-pool,
Desktop, WindowManager, framebuffer, or kernel-global references.  Task
Manager's selected metric path now consumes the snapshot; chart state,
rendering state, per-window owner observation, and allocator-detail counters
remain application-local diagnostics.

### 24.6 Runtime proof, regression gates, and deferrals

The AppModel diagnostic emits and gates:

```text
APP_MODEL_SERVICES_SELFTEST_OK=1
APP_MODEL_SERVICES_REGISTERED=3
APP_MODEL_SERVICES_DUPLICATE_REJECTED=1
APP_MODEL_SERVICES_STALE_REJECTED=<positive>
APP_MODEL_SERVICES_CLEANUP=1
```

The AppRuntime service diagnostic uses normal descriptor/factory launches for
Calculator, two Notepad instances, and Task Manager.  It emits:

```text
APP_RUNTIME_SERVICES_NOTIFICATION=PASS
APP_RUNTIME_SERVICES_SHARED_SETTINGS=PASS
APP_RUNTIME_SERVICES_SNAPSHOT=PASS
APP_RUNTIME_SERVICES_STALE_REJECTED=PASS
APP_RUNTIME_SERVICES_CLEANUP=PASS
APP_RUNTIME_SERVICES_STALE_CONTEXTS=0
APP_RUNTIME_SERVICES_RESULT=PASS
```

The service diagnostic restores its pre-diagnostic authoritative instance,
observation, window, and stale-ownership baselines.  The focused green
evidence is `serial_phase8_appmodel_green.txt` and
`serial_phase8_service_runtime_green2.txt`; the complete Phase 7 regression
matrix remains required before final Phase 8 acceptance.

The following remain inventoried and explicitly deferred: message/open/save
dialogs; app-local filesystem storage; packaged resources; clipboard; and a
shell/open service.  Persistence, IPC, Ring 3, and a generic dependency
injection framework remain outside this phase.  Existing App Model launch,
association, shell, dialog, file, and resource paths are not relabeled as
application services merely because they are adjacent to the cohort.

## 25. Phase 9 — dialog, file-picker, and shell/open service convergence

Phase 9 supersedes the Phase 8 deferral for dialogs, file pickers, and
shell/open behavior.  The implementation was performed only in
`D:\dev\guideXOSUEFI`; `D:\dev\guideXOSServer`,
`D:\dev\guideXOSServerV1.1_DOTNET_SUPPORT`, and `D:\dev\guideXOS` were
audited read-only.  The Phase 8 checkpoint was `c5d1594`.

The defining boundary is:

```text
application → ApplicationServiceContext → typed service → existing backend
```

The C# backend may still construct `Window` subclasses internally.  That
implementation fact is not part of the application contract, and no raw
`Window`, `WindowManager`, `Desktop`, renderer, framebuffer, filesystem
object, or allocator object crosses the service boundary.

### 25.1 Authoritative audit

The Server audit covered `message_box.h`, `open_dialog.h`, `save_dialog.h`,
`app_launch_target.h`, `app_launch_resolver.h/.cpp`, desktop-service and
shell-object/association paths, plus the manifest permission vocabulary.  The
Server dialog objects are UI/process entry points with ownership and callback
behavior; the open/save objects own VFS navigation and return selection via
callbacks.  Typed launch targets resolve application IDs, documents,
associations, shell objects, and actions, but are launch infrastructure rather
than an application-facing request-session API.  Server has manifest
permission names, but no unified capability enforcement path was found for
this cohort; `PermissionDenied` is therefore retained as result vocabulary
without inventing restrictions.

The C# audit found direct UI construction in the implementation layer:
`ApplicationServiceBackends.cs` constructs `MessageBox`, `SaveChangesDialog`,
`OpenDialog`, and `SaveDialog` behind the service adapters.  `Notepad.cs`
now requests dialogs and file selection through services.  `DisplayOptions`
uses the open-file service for its background picker.  `ComputerFiles` uses
the shell service for conceptual document opening while retaining its own
enumeration, drive, root, and navigation implementation.  Remaining direct
`Desktop` calls are shell-internal or filesystem/UI implementation paths, not
application-facing replacements for the new contracts.  The host-only
`AutoMountConfig` Windows Forms message boxes remain compatibility/host
diagnostic behavior and were not blindly rewritten as UEFI platform calls.

### 25.2 Convergence matrix

| Service | Server source | C# source | Classification | Shared semantics | Backend differences | Migration value | Risk | Decision |
|---|---|---|---|---|---|---|---|---|
| Informational dialog | `message_box.h` and message-box implementation | `ApplicationDialogServices.cs`, `ApplicationServiceBackends.cs`, `MessageBox.cs` | Existing feature / new platform boundary | bounded title/body, acknowledge, terminal result | Server process/UI entry point; C# creates a transient managed window | High; removes app knowledge of dialog implementation | Medium; input/modal cleanup | Cohort |
| Error dialog | `message_box.h` | same dialog adapter; Notepad error path | Existing feature / new platform boundary | bounded error message and terminal result | backend renderer and error styling differ | High; typed failure visibility | Medium | Cohort |
| Confirmation dialog | message-box/close-confirmation behavior | `SaveChangesDialog.cs` behind dialog service; Notepad dirty-close path | Existing feature / semantic ownership adaptation | accepted/rejected/cancelled/closed | Server callback/process ownership differs from C# transient window | High; proves lifecycle-sensitive interaction | High | Cohort |
| Open file | `open_dialog.h`, VFS/path navigation | `OpenFileRequest`, `CSharpApplicationOpenFileService`, `OpenDialog.cs` | Existing feature / adapter | bounded location request, selection or cancel | Server owns VFS dialog; C# owns a `Window` backend | High; Notepad and Display Options | High | Cohort |
| Save file | `save_dialog.h`, VFS/path navigation | `SaveFileRequest`, `CSharpApplicationSaveFileService`, `SaveDialog.cs` | Existing feature / adapter | bounded location/name request, destination or cancel | overwrite policy remains only where backend already supports it | High; Notepad Save/Save As | High | Cohort |
| Open document | `app_launch_target.h`, resolver, association/VFS path | `ApplicationShellOpenRequest.ForDocument`, `ApplicationShellServices.cs`, modern association adapter | Existing launch infrastructure / service projection | association resolution then typed App Model launch | C# GXM remains a typed external backend; Server may load a process/runtime target | High; shell/open proof and Computer Files | Medium | Cohort through existing App Model |
| Launch application | typed launch target/resolver and application registry | `ApplicationShellOpenRequest`, `CSharpApplicationShellService`, `ApplicationFactoryRegistry` | Existing modern launch path / service projection | stable ID or bounded alias, typed result and instance identity | Server loader/process versus C# managed factory/instance | High; no second launch mechanism | Medium | Cohort through existing App Model |
| Shell object/action | shell-object registry, typed launch target/action resolver | `ModernShellAdapter`, `ApplicationShellOpenRequest` | Existing shell routing / service projection | stable object/action target and typed result | Server shell object/action backend differs from C# Desktop/GXM/installer adapters | High; preserves typed shell actions | High; action-specific side effects | Cohort for existing typed targets |
| Open-with | no single bounded common contract established | no application-facing open-with service | Related shell feature / unresolved policy | would require handler choice and association UI | handler selection and persistence are not common today | Low for Phase 9 | High | Deferred |

### 25.3 Common service result and request-session model

Phase 9 uses the existing `ApplicationServiceRegistry` and
`ApplicationServiceContext`; it does not create a second registry.  The
registry exposes seven fixed services: Notifications, Settings,
SystemInformation, Dialogs, OpenFile, SaveFile, and Shell.

`ApplicationServiceResultCode` is bounded and typed.  It includes
`Success`, `Cancelled`, `NotFound`, `UnsupportedTarget`, `PermissionDenied`,
`ResourceUnavailable`, `InvalidRequest`, `InvalidContext`, `InvalidState`,
`BackendFailure`, and explicit `Conflict`.  `Conflict` is returned when an
owner already has one outstanding interactive request.  The request handle is
a fixed slot plus generation plus service ID; it is not a callback, window
reference, or backend pointer.  Terminal results are consumed after
observation, while pending and completed state is stored in a fixed session
table.

The application-facing dialog contract is:

```text
ApplicationDialogRequest
  Kind: Information | Error | Confirmation
  Title: max 64 characters
  Body: max 256 characters
  ButtonSet: Acknowledge | AcceptRejectCancel

ApplicationDialogResult
  Outcome: Accepted | Rejected | Cancelled | Closed | BackendFailure
```

Open and save requests return `ApplicationFileDialogResult`, not a dialog
object:

```text
OpenFileRequest
  StartingLocation: max 1024 characters

SaveFileRequest
  StartingLocation: max 1024 characters
  SuggestedFileName: max 128 characters

ApplicationFileDialogResult
  Outcome: Selected | Cancelled | BackendFailure
  SelectedPath: max 1024 characters
```

The outer service call still reports `InvalidRequest`, `InvalidContext`,
`Conflict`, `ResourceUnavailable`, and `BackendFailure` where applicable.
The current cohort intentionally does not invent overwrite-confirmation or
directory-selection semantics that are not shared by the audited backends.

The shell/open contract is:

```text
ApplicationShellOpenRequest
  TargetKind: ApplicationId | Alias | Document | ShellObject | TypedShellAction
  Target: max 1024 characters

ApplicationShellResult
  ResultCode: typed ApplicationServiceResultCode
  AppId: bounded stable ID when available
  InstanceHandle: generation-safe handle on success
  BoundedDiagnostic: max 192 characters
```

`ApplicationShellService` maps the request to the existing `LaunchRequest`,
`ApplicationDescriptorRegistry`, `ApplicationFactoryRegistry`, GXM typed
backend, association adapter, or typed shell-action adapter.  It never
creates an alternate application launch route.

### 25.4 Ownership, modal behavior, and lifecycle

The semantic owner of every service request is the generation-safe
`ApplicationInstanceHandle` in the requesting `ApplicationServiceContext`.
The C# WindowManager remains the graphical owner of the actual transient
window.  Service-created dialog/file-picker windows carry explicit transient
service-session metadata containing only the requester and request handle.
They are excluded from ordinary application content ownership, ordinary owned
window counts, taskbar application entries, final-window policy, and Task
Manager application-window observations.  This prevents a service dialog from
appearing as a second application window or changing application-centric
taskbar grouping.

Interactive creation requires the requester to be in Running or Activated.
The existing one-outstanding-interactive-request rule returns `Conflict` for a
second dialog/open/save request from the same owner.  Observing, cancelling,
or retrieving an already-issued request is a separate eligibility path:
Running, Activated, and Inactive are accepted.  Thus another application may
be activated while a request is pending, and a completed result remains
retrievable when the original requester becomes eligible again.  Suspended,
Closing, Failed, Terminated, stale-generation, and descriptor-mismatched
contexts are rejected deterministically.  A new request from an Inactive
owner must first reactivate the owner; the runtime Phase 9 proof exercises
this after shell launch changes foreground ownership.

The C# implementation is stateful/asynchronous underneath.  A service call
returns a fixed request handle; application code observes the handle during
its normal draw/input lifecycle.  No spin loop or lock is held while a user
interacts.  Cancel, close, backend failure, and requester termination complete
or clear the session deterministically.  Termination closes associated
transient windows, releases input capture, removes transient metadata, and
prevents stale result observation.  Dialog input is modal to the service
session, but the whole shell is not frozen; unrelated applications remain
usable.

### 25.5 C# and Server backends

The C# backend in `ApplicationServiceBackends.cs` creates the existing
`MessageBox`, `SaveChangesDialog`, `OpenDialog`, and `SaveDialog` only after a
validated service session is allocated.  It registers each window as a
transient/service-session window and maps callbacks and close events to typed
results.  This is an implementation adapter, not an application-visible GUI
contract.

The shell adapter in `ApplicationShellServices.cs` uses the existing modern
App Model for application IDs and aliases, the existing association resolver
for documents, the typed GXM loader for `.gxm`, and the existing typed shell
object/action adapter for shell targets.  C# remains same-address-space and
managed; no process boundary is implied.

The Server backend remains read-only in this phase.  Its message/open/save
objects and typed launch-target/resolver remain the authoritative semantic
references for future service adapters.  Server's VFS ownership, process
callbacks, and capability vocabulary are not copied into C# as raw objects.

### 25.6 Representative migrations

* Notepad removes application-facing direct dialog construction and direct
  message-box selection for Open, Save, Save As, error/information, and dirty
  confirmation flows.  It still owns document bytes, dirty state, undo/redo,
  filename/title state, and the existing filesystem write/read operations.
  Its Phase 8 wrap setting remains application-scoped session state.
* Computer Files uses Shell/Open for conceptual document opening and retains
  direct internal enumeration, root/drive navigation, and filesystem display
  behavior.  Compatibility construction without a service context preserves
  the bounded legacy fallback path.
* Display Options uses OpenFile for its background picker, including success
  and cancellation.  Rendering, background decoding, and color/effects
  state remain local implementation concerns.

No production Start item was added for diagnostics.  The application factory
path supplies service contexts to modern instances; compatibility fallback and
legacy backend counts remain zero.

### 25.7 TDD and runtime proof

The deterministic AppModel diagnostic proves bounds, `Conflict`, typed result
states, invalid/stale contexts, cancellation, file result propagation, shell
launch/open mapping, lifecycle rejection, and transient-window cleanup.  The
Phase 9 markers are:

```text
PHASE9_DIALOG_SELFTEST_OK=1
PHASE9_FILE_SERVICE_SELFTEST_OK=1
PHASE9_SHELL_SERVICE_SELFTEST_OK=1
PHASE9_ORPHAN_DIALOG_COUNT=0
PHASE9_STALE_SERVICE_CONTEXT_COUNT=0
```

The real-instance AppRuntime proof passes Notifications, shared session
settings, system information, Notepad open success/cancel, Save success,
confirmation cancellation, Display Options open success/cancel, a shell
application launch, and an associated GXM document open.  The final green
serial evidence is
`serial_uefi_validation_20260919_102235.txt`:

```text
APP_RUNTIME_PHASE9_OPEN_SUCCESS=1
APP_RUNTIME_PHASE9_OPEN_CANCEL=1
APP_RUNTIME_PHASE9_SAVE_RESULT=success
APP_RUNTIME_PHASE9_CONFIRMATION_RESULT=cancelled
APP_RUNTIME_PHASE9_SHELL_LAUNCH=1
APP_RUNTIME_PHASE9_DOCUMENT_OPEN=1
APP_RUNTIME_PHASE9_ORPHAN_DIALOG_COUNT=0
APP_RUNTIME_PHASE9_STALE_SERVICE_CONTEXT_COUNT=0
APP_RUNTIME_PHASE9_RUNTIME_OK=1
APP_RUNTIME_SERVICES_RESULT=PASS
APP_RUNTIME_NOTEPAD_SERVICE_DIAGNOSTIC=PASS
```

The runtime cleanup comparison returned active instances, observations,
WindowManager windows, and stale ownership to their pre-diagnostic baselines.
The full AppRuntime gate also reported compatibility fallback `0`, legacy
backend `0`, runtime faults `0`, allocator corruption `0`, valid graphics,
zero unexpected keyboard/mouse drops, and balanced mouse transitions.

### 25.8 Capability policy and future isolation

The existing service context remains capability-shaped and future-compatible,
but Phase 9 does not invent permission checks absent from the Server audit.
`PermissionDenied` is preserved for a future real policy result.  The current
same-address-space C# adapter validates identity, generation, descriptor, and
lifecycle only.

Every Phase 9 request and result is representable as bounded serializable
data: enum values, bounded strings, fixed handles, and scalar result fields.
A future isolated application process can issue equivalent requests over IPC
without changing its application-facing service API.  IPC, Ring 3, process
proxies, resource/package service, app-local storage, persistence, clipboard,
and open-with selection remain deferred.

### 25.9 Files, repository protection, and next phase

Phase 9 changed only the C# UEFI repository and this canonical document.  The
Server, Advanced Server, and Legacy repositories were not modified.  The
implementation is committed in the Phase 9 service, migration, runtime-proof,
and documentation commits; it is intentionally not pushed by this workflow.

Phase 10 is recorded below as the next bounded platform boundary. Clipboard,
IPC, Ring 3, writable-filesystem repair, general filesystem access, and
persistent settings remain outside the approved scope.

## 26. Phase 10 app-local storage, package identity, and resources

### 26.1 Audit and outcome

The Phase 10 audit reused the existing C# descriptor authority and compared it
with Server `AppManifest.id`, the Advanced Server NativeAOT lineage, and the
Historical Legacy tree. `ApplicationDescriptor.AppId` is already the stable
identity, so Phase 10 adds no package-ID type, second registry, or path-derived
identity. The Server, Advanced Server, and Legacy trees were read-only
references and were not modified.

The selected live UEFI backend is `RdskFS`. Its persistent write/delete paths
are no-op implementations and do not provide a safe verification result. No
already-safe writable backend was found in the selected path, so the honest
target is Outcome B: resource reads and the full typed storage contract are
implemented; temporary storage is real in-memory storage; persistent writes
and deletes return `ResourceUnavailable` without a temporary fallback or
false success.

The Calculator icon was audited as a shared shell/OS `Icons` asset rather than
a proven Calculator package-owned resource. It was not misclassified or
migrated. The runtime proof uses the bounded application-owned diagnostic
fixture `diagnostic.fixture` for `selftest.phase8.services`.

### 26.2 Approved contracts

Resources and Storage extend the existing `ApplicationServiceRegistry` as
service IDs 8 and 9. They reuse generation-safe `ApplicationServiceContext`,
the existing typed result vocabulary, and the descriptor `AppId`.

Resource access is logical-key-only: metadata and bounded chunk reads return
bounded byte arrays and no resource handles, paths, image objects, streams, or
renderer objects. Resource keys are bounded to 96 characters. Read chunks are
positive and bounded to 64 KiB. Negative offsets, offsets beyond the resource,
zero/over-bound chunk requests, invalid keys, and arithmetic overflow are
`InvalidRequest`. `Offset == Length` succeeds with zero bytes and
`EndOfResource = true`; an in-range partial read reports the exact byte count
and reaches end only when its returned range reaches the resource length.

Storage exposes only Exists, Read, Write, Delete, and bounded Enumerate over
`Persistent` or `Temporary` namespaces. Relative paths are limited to 192
characters with 64-character segments; absolute, drive/device, repeated or
trailing separators, dot/dot-dot traversal, controls, invalid filename
characters, and over-bound paths are rejected before any backend call.

Temporary storage is keyed by AppId, shared by same-AppId instances, isolated
between AppIds, and retained when one ApplicationInstance terminates. It is
cleared only by the explicit App Model reset/reboot boundary. Persistent
writes/deletes never redirect to temporary storage. Phase 9 Open/Save remains
the path for user-selected external documents, and Phase 8 settings remain
session-only.

### 26.3 Implementation and TDD evidence

The design/spec and plan were committed before implementation:

| Commit | Evidence |
| --- | --- |
| `ee58ba3` | Approved Phase 10 design/spec |
| `c3528d9` | Bounded resource/storage contracts and registry services |
| `bd0dfec` | Granular Phase 10 self-test and validation markers |
| `7a83882` | Stable QEMU validation process launch/reporting |

The red step referenced the Phase 10 contract from the registry self-test
before adding the types and produced the expected missing-symbol C# errors.
The green NativeAOT build then completed with the existing warning baseline.
The implementation is confined to `ApplicationServices.cs`,
`ApplicationServiceRegistry.cs`, `ApplicationResourceServices.cs`,
`ApplicationStorageServices.cs`, `Program.cs`, and the existing validation
harness; no filesystem backend repair or bootloader change was made.

The final AppModel guest proof emitted:

```text
PHASE10_RESOURCE_STORAGE_SELFTEST_OK=1
PHASE10_RESOURCE_CHUNK_SELFTEST_OK=1
PHASE10_STORAGE_PATH_CONFINEMENT_OK=1
PHASE10_STORAGE_PERSISTENT_UNAVAILABLE_OK=1
PHASE10_STORAGE_APP_SCOPE_OK=1
PHASE10_STORAGE_RESET_OK=1
APP_MODEL_SERVICES_REGISTERED=9
PHASE9_DIALOG_SELFTEST_OK=1
PHASE9_FILE_SERVICE_SELFTEST_OK=1
PHASE9_SHELL_SERVICE_SELFTEST_OK=1
PHASE9_ORPHAN_DIALOG_COUNT=0
PHASE9_STALE_SERVICE_CONTEXT_COUNT=0
APP_MODEL_COMPLETE
```

### 26.4 Phase 7–9 regression matrix

The existing focused matrix was rerun on 2026-09-19 with the current branch:

| Selector | Result evidence |
| --- | --- |
| `-AppModel` | `DIAGNOSTIC_COMPLETE`; App Model validation true; 12 descriptors, 12 factory registrations, 0 fallbacks; all Phase 9 and Phase 10 markers above |
| `-AppRuntime` | Current guest rerun reached the real launch/close/input workload with the Phase 9 service and factory markers; the complete accepted Phase 9 runtime reference remains `serial_uefi_validation_20260919_102235.txt` |
| `-Continuous -TimeoutSeconds 30` | 60 heartbeat frames; graphics valid; allocator corruption 0; ThreadPool locked 0 |
| `-NativeInput` | 104/104 key transitions, 54/54 left-button transitions, dropped input 0; graphics valid |
| `-ContextMenu` | Repeated `CONTEXT_MENU_BOUNDS=...,ok=1`, `CONTEXT_MENU_DRAWN=...,font=1`, activation/dismissal pairs, balanced input stats |
| `-Frames 300` | Checkpoints through frame 300, `MULTIFRAME_LAST_COMPLETED_FRAME=300`, `MULTIFRAME_COMPLETE` |

The matrix preserves the prior Phase 7–9 compatibility gates: compatibility
fallback 0, legacy backend 0, stale service context 0, stale ownership 0,
allocator corruption 0, valid graphics, and balanced input. The host harness
was hardened only to retain the existing console attachment and to treat an
empty early serial file as a normal failure result instead of throwing during
regex parsing.

### 26.5 Deferred scope and repository protection

Deferred: persistent settings, writable-filesystem repair, general filesystem
access, clipboard, IPC, Ring 3, package installation, and Calculator/shared
shell resource migration without a new ownership proof. Server,
Advanced Server, and Historical Legacy remain unchanged; the selected C#
branch is `codex/phase-10-app-local-storage`.

## 27. Phase 11 session-global text clipboard

### 27.1 Gate, baseline, and scope

Phase 10 was accepted as Outcome B before this phase began. Its read-only
`RdskFS` backend, typed `ResourceUnavailable` result for persistent writes and
deletes, real temporary storage, logical resources, and namespace confinement
were preserved. No writable persistent storage was introduced or made a
prerequisite. The accepted Phase 10 branch was fast-forwarded into the local
`main` at `1689e6e` before `codex/phase-11-clipboard` was created. A remote
fetch/synchronization attempt remained blocked by the configured GitHub SSH
remote rejecting the available key (`Permission denied (publickey)`); no
history was rewritten and no unrelated work was discarded.

Phase 11 is limited to `ApplicationServiceId.Clipboard`: one session-global
text value, service-owned copied contents, stable source `AppId` metadata from
the validated service context, immutable copied read snapshots, explicit
Clear, and deterministic `UInt64` generations. File/image/binary clipboard,
arbitrary shared data, Notepad selection commands, IPC, Ring 3, shared memory,
and writable filesystem repair remain out of scope. The diagnostic runtime
proof is authoritative because the current C# Notepad has no natural
Copy/Cut/Paste path.

### 27.2 Approved contract and invariants

Clipboard is appended as service ID `10`; existing service IDs `1` through `9`
remain unchanged. The write request accepts a non-null string of `0..65,536`
UTF-16 code units (`string.Length`), with empty text valid. A null request,
over-bound text, invalid context, or generation overflow returns the existing
typed failure vocabulary without mutating state.

`GetText` returns a successful snapshot for every valid context, including an
absent clipboard. Absence is `HasValue=false`, empty text is a present value
with `HasValue=true`, and each read returns copied snapshot data. A successful
write copies text and the validated writer `AppId`; no source instance,
handle, window, callback, or selection is retained. The service is global
across AppIds, and source termination does not clear it.

The initial/reset state is absent at generation `0`. Each successful write
advances once, including same-text and empty writes. Clear advances once only
when a value is present; repeated Clear while absent succeeds without changing
the generation. `UInt64.MaxValue` is never wrapped. App Model reset and reboot
clear text/source metadata and restore generation `0`.

### 27.3 Design, TDD, and implementation evidence

The design and implementation plan were committed before implementation:

| Commit | Evidence |
| --- | --- |
| `ccf9490` | Approved Phase 11 clipboard design/spec |
| `0426c36` | Phase 11 implementation plan |
| `96ea38f` | Typed Clipboard contract, registry projection, and service-owned backend |
| `2e74d5f` | Diagnostic self-tests, serial markers, and App Model validation gate |

The red contract build intentionally failed with the missing Clipboard enum
and contract types. The green build
`dotnet build guideXOS\guideXOS.csproj --no-restore -p:SkipISO=true -p:UefiDiagnosticMode=AppModel`
then completed with zero errors (the repository's existing warning baseline
remained). The implementation is confined to the C# UEFI repository; Server
and Legacy trees were not modified.

### 27.4 App Model proof

The final App Model run was
`.\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 300`, with serial
evidence `serial_uefi_validation_20260919_221228.txt`. It reported
`DIAGNOSTIC_COMPLETE`, App Model validation true, 12 descriptors, 12 factory
registrations, 0 compatibility fallbacks, and 10 registered application
services. The guest emitted all retained Phase 9 and Phase 10 markers plus:

```text
PHASE11_CLIPBOARD_CONTRACT_OK=1
PHASE11_CLIPBOARD_SELFTEST_OK=1
PHASE11_CLIPBOARD_GENERATION_OK=1
PHASE11_CLIPBOARD_LIFECYCLE_OK=1
PHASE11_CLIPBOARD_RESET_OK=1
APP_MODEL_COMPLETE
```

The diagnostic writes from one AppId and reads/replaces from another, retains
an earlier snapshot across replacement, terminates the source instance,
proves the value remains, writes present-empty text, clears explicitly,
checks repeated-Clear stability, and invokes the App Model reset hook.

### 27.5 Full Phase 7–10 regression matrix

The existing Phase 7–10 gates were rerun without reopening Phase 10 storage
architecture or persistence. The exact current-run evidence is:

| Selector | Result | Serial evidence |
| --- | --- | --- |
| `-AppModel -TimeoutSeconds 300` | `DIAGNOSTIC_COMPLETE`; App Model true; 12/12/0 descriptor/factory/fallback counts; Phase 9–11 markers green | `serial_uefi_validation_20260919_221228.txt` |
| `-AppRuntime -TimeoutSeconds 300` | `APP_RUNTIME_COMPLETE`; real launch/close/input workload; runtime faults 0; allocator corruption 0; graphics and input gates green | `serial_uefi_validation_20260919_221250.txt` |
| `-NativeInput -TimeoutSeconds 120` | `TIMEOUT_SUCCESS`; 208/104/104/0 keyboard IRQ/down/up/dropped, 837/279/172/0 mouse IRQ/packets/moves/dropped, 54/54 left transitions | `serial_uefi_validation_20260919_222016.txt` |
| `-ContextMenu -TimeoutSeconds 120` | `CONTEXT_MENU_COMPLETE`; 104/104/104/0 desktop opens/draws/good-bounds/bad-bounds; right-button 105/105; input balanced | `serial_uefi_validation_20260919_222229.txt` |
| `-Continuous -TimeoutSeconds 300` | `TIMEOUT_SUCCESS`; 600 heartbeat frames, timer 71→10260, valid graphics, stable stack | `serial_uefi_validation_20260919_223132.txt` |
| `-Frames 300 -TimeoutSeconds 120` | Guest emitted checkpoints through frame 300, `MULTIFRAME_LAST_COMPLETED_FRAME=300`, and `MULTIFRAME_COMPLETE` | `serial_uefi_validation_20260919_223702.txt` |

The AppRuntime run also retained the accepted launch/close, association,
shell, input, and memory-balance gates. The bounded Frames selector reports
its completion through guest markers; its host summary fields are not the
continuous dispatch fields and are not used as a Phase 11 clipboard gate.

### 27.6 Repository protection and next boundary

The final Phase 11 branch is `codex/phase-11-clipboard`. The worktree is
clean, `git diff --check` is clean, and all changes are confined to this UEFI
repository. Server, Advanced Server, and Historical Legacy remain unchanged.
Phase 10 remains complete as accepted Outcome B; any future writable
filesystem work requires a separate approved phase and is not part of
clipboard completion.
