# AMCONV2A Shared Typed Shell-Target Design

**Outcome:** `AMCONV2A_CONTRACT_READY` — source-level design only. The contract
is ready for a bounded implementation after the managed-artifact build
prerequisite is restored. This document does not claim compiled or guest-tested
behavior.

**APP_MODEL checkout:** `D:\dev\guideXOSUEFI_APP_MODEL`, branch `APP_MODEL`
**Starting HEAD:** `170553766943cb0f64daa025b6e0c6608a9e1f89`
**Server reference:** `D:\dev\guideXOSServer`, branch `main`,
`080928710398ec02f728e573838d76fc0b0bfe49` (read-only)
**AMCONV1 baseline:** `docs/AMCONV1_COMPATIBILITY_BASELINE.md` and
`docs/AMCONV1_HOSTED_QUALIFICATION.md`

## Scope and evidence boundary

AMCONV1 added a C# source adapter for the Server Files shell identity. Its
existing request resolves to the C# `gxos.shell.computerfiles` object and
`gxos.builtin.files` app. The baseline records the Server/C# app ID difference
and the missing C# folder, system-panel, virtual-object, and trash backends.
AMCONV2A defines a typed value model for those target classes and checks which
existing C# components can be reused. It does not extend that bridge in code.

The Server source exposes two related but distinct surfaces:

1. `shell_object_registry.h` declares stable shell IDs, aliases, target kinds,
   handler identities, and canonical target values.
2. `DesktopService::ResolveLaunchTarget` resolves user-facing launch labels
   such as `This System` and `Documents`. It does not itself look up a Server
   shell ID by that ID string. The launch path and its folder dispatcher then
   perform the actual operation.

The Server reference checkout had pre-existing untracked build and evidence
files. Its tracked source was inspected without changing or staging anything.
All conclusions below are source findings; no Server or C# code was executed
for this phase.

## 1. Source-verified compatibility matrix

### Server shell registry and behavior

`ShellObjectRegistryRecord` includes the canonical ID, display name, aliases,
source-side target kind/value, and a default handler identity. Its alias
comparisons are exact string comparisons (`shell_object_registry.h:141-168`).
The Server records are:

| Server canonical shell ID | Registered aliases (besides display name) | Server kind; target kind/value; default handler | Source behavior and C# finding |
|---|---|---|---|
| `gxos.shell.desktop` | `Desktop Home`, `Go to Desktop` | Desktop; `VirtualObject` / `DesktopHome`; no handler | A Server virtual desktop object. No equivalent C# shell object/backend. Unsupported. |
| `gxos.shell.this-system` | `Computer` | Root; `FilesystemFolder` / `/`; `gxos.builtin.fileexplorer` | Server label resolution sends `Computer` and `This System` to File Explorer at `/`. C# has `gxos.shell.root`, but it has empty path and no app handler; it does not implement this folder activation. The source also does not establish that Server VFS `/` and a C# filesystem root denote the same storage. Unsupported in current C#. |
| `gxos.shell.files` | `Files`, `File Manager`, `File Explorer`, `FileExplorer` | FileManager; `AppSystemPanel` / empty; `gxos.builtin.fileexplorer` | AMCONV1 handles this exact Server ID and three aliases (`Files`, `File Manager`, `FileExplorer`) by launching the C# Computer Files app. `File Explorer` also resolves through the pre-existing C# alias. This is a verified app-open bridge; it is not a general system-panel or folder-path backend. |
| `gxos.shell.documents` | `Documents` | Folder; `FilesystemFolder` / `/Documents`; `gxos.builtin.fileexplorer` | Server’s `Documents` label resolves to a shell action carrying `/Documents`; the launch path ensures the shared folder exists, then calls `OpenFolder`. C# has no Documents shell entry, no generic initial-folder path support, and no verified mapping from this Server VFS path to a C# mounted filesystem. Unsupported in current C#. |
| `gxos.shell.pictures` | `Pictures` | Folder; `FilesystemFolder` / `/Pictures`; `gxos.builtin.fileexplorer` | Same folder activation shape as Documents. No C# equivalent path target. Unsupported. |
| `gxos.shell.music` | `Music` | Folder; `FilesystemFolder` / `/Music`; `gxos.builtin.fileexplorer` | Same folder activation shape as Documents. No C# equivalent path target. Unsupported. |
| `gxos.shell.network` | `Network` | Folder; `FilesystemFolder` / `/Network`; `gxos.builtin.fileexplorer` | Same folder activation shape as Documents. No C# equivalent path target. Unsupported. |
| `gxos.shell.settings` | `System Settings`, `Display Options`, `Display Settings`, `Desktop Background`, `Wallpaper` | SystemPanel; `AppSystemPanel` / empty; `gxos.builtin.displayoptions` | The registry record names Display Options, but Server launch-label resolution separately maps `Settings` and `System Settings` to unified `gxos.builtin.settings`. C# has a `gxos.builtin.displayoptions` factory, but no `gxos.builtin.settings` entry or system-panel adapter. The name overlap does not establish equivalent Settings behavior. Unsupported pending an exact panel mapping. |
| `gxos.shell.control-panel` | `Control Panel` | SystemPanel; `AppSystemPanel` / empty; `gxos.builtin.controlpanel` | Server has a Control Panel launch path. C# has no matching app descriptor/factory or system-panel dispatch. Unsupported. |
| `gxos.shell.trash-open` | `Trash` | Trash; `VirtualObject` / empty; `gxos.builtin.trash` | Server Trash is a virtual object with its own behavior. C# has no Trash shell object/backend. Unsupported. |

Evidence: Server `shell_object_registry.h:19-38,44-115`;
`desktop_folder.h:31-58,130-202`; `app_launch_target_resolver.cpp:127-172,238-270`;
`desktop_service.cpp:1475-1480,3355-3462,5401-5478,5897-5937,6404-6465`;
`app_activation.h:10-40`; `app_registry.cpp:968-1054`;
`file_explorer.cpp:522-538,549-595,602-639,665-669`.

For the Server root and standard-folder labels, `ResolveLaunchTarget` emits a
`ShellAction` with a path parameter. The active and fallback shell-action
dispatchers call `OpenFolder`; Documents, Pictures, Music, and Network first
call `DesktopFolderResolver::EnsureExists`. `OpenFolder` normalizes/inspects
the virtual path, requires an existing directory, resolves a registered
folder-capable handler, and dispatches an owned `AppActivationContext`.
`FileExplorer::LaunchWithActivation` validates that context and the activated
path before spawning File Explorer. A raw path-shaped launch label instead
resolves as `FileOpen` (`app_launch_target_resolver.cpp:247-270` or
`desktop_service.cpp:3455-3462`); it is not the shell shortcut's folder
activation request. The folder shortcut must not be routed through document
association resolution.

### C# registry, request, and backend baseline

| C# source surface | Existing behavior relevant to the contract |
|---|---|
| `ShellObjectRegistry` | Registers `gxos.shell.computerfiles` (`Computer Files`, alias `File Explorer`) with app ID `gxos.builtin.files`; `gxos.shell.root` with `FileSystemLocation` and empty path; `gxos.shell.usbdrive` with `DeviceVolume`; and `gxos.shell.installtoharddrive` with `SystemAction`. No Server Documents, Pictures, Music, Network, Settings, Control Panel, desktop, or Trash IDs are registered (`guideXOS/OS/AppModel.cs:498-542`). |
| Target kinds | `ShellObjectKind` distinguishes `FileSystemLocation`, `DeviceVolume`, and `SystemAction`. `ApplicationShellTargetKind` declares `Virtual`, `FileSystem`, `SystemPanel`, and `Action`. The current `ModernShellAdapter` maps filesystem locations to `FileSystem`, system actions to `Action`, device volumes to `FileSystem` with a drive name as its value, and everything else to `Virtual`; it does not emit `SystemPanel` (`ModernAppModel.cs:61-66,1358-1385`). Device-volume identity is therefore not a filesystem folder path. |
| Launch request | `LaunchRequest.ForShellObject` already carries source shell ID, target app ID/name, target kind/value, arguments, and `LaunchActivationIntent`. `ForFile` is a separate request kind with a document path. Request text is bounded by 1,024 UTF-16 code units (`ModernAppModel.cs:258-365,368-405`). |
| Launch result | `LaunchResult` already provides typed success, app ID, generation-safe instance handle, activation state, error code, and bounded diagnostic. Error codes include `NotFound`, `UnsupportedTarget`, `MalformedRequest`, `ResourceUnavailable`, `PermissionDenied`, and backend/activation failures (`ModernAppModel.cs:47-59,478-562`). |
| AMCONV1 adapter | `ServerShellObjectCompatibilityAdapter` explicitly maps `gxos.shell.files`, `Files`, `File Manager`, and `FileExplorer` to `gxos.shell.computerfiles`; other known Server IDs/aliases are explicitly `UnsupportedTarget`, while unknown input is `NotFound`. The mapped request targets `gxos.builtin.files`, uses `LaunchRequestTargetKind.ShellObject`, C# shell ID `gxos.shell.computerfiles`, and `LaunchActivationIntent.Launch` (`ModernAppModel.cs:1286-1392`). |
| Shell dispatch | `CSharpApplicationShellService.OpenShellObject` routes typed actions separately, rejects a shell object with no `TargetAppId`, then sends a handler-backed shell request through `LaunchModern` and `ApplicationFactoryRegistry` (`ApplicationShellServices.cs:343-403,186-218`). |
| Computer Files backend | The `gxos.builtin.files` factory creates a default Computer Files window when there is no nonempty filesystem shell value. A nonempty `FileSystem` shell value is currently treated as a drive name (`Hard Disk` or `USB Drive N`) and opened using a drive-specific filesystem; other values return `ResourceUnavailable` (`ApplicationFactories.cs:1117-1218`). `ComputerFiles` has no constructor argument for a generic initial folder; the ordinary constructor starts at its drive chooser (`DefaultApps/ComputerFiles.cs:14-20,74-145`). |
| Filesystem namespace | Server shell values are paths in its VFS namespace. C# `File.GetFiles` delegates to the current static `File.Instance`, while the Computer Files UI can also construct per-drive `AutoFS` filesystems (`Kernel/FS/FileSystem.cs:44-54`; `Kernel/FS/AutoFS.cs:14-49,106-110`; `DefaultApps/ComputerFiles.cs:179-208,219-235`). C# boot can mount an `RdskFS` as `File.Instance` (`guideXOS/Program.cs:848-860`). No source establishes which C# mount corresponds to Server's VFS root or that both roots contain the same `/Documents`. |
| Document support | C# has `LaunchRequest.ForFile` and a file-association adapter for actual documents. Those request a file handler and preserve the document path; they do not open a directory (`ModernAppModel.cs:335-344,1164-1260`; `ApplicationShellServices.cs:220-269`). |
| Instance ownership | `gxos.builtin.files` is projected as `MultiInstance`, closes when its last window closes, and advertises `AcceptsFolderTargets`; `ComputerFilesApplicationFactory` nevertheless lacks generic path consumption. `ApplicationFactoryRegistry` passes the request to `ApplicationInstanceRegistry`, and the instance owns its request and windows (`ModernAppModel.cs:1026-1049`; `ApplicationFactories.cs:361-440`; `ApplicationInstance.cs:642-770`). Instance policy, not the spelling of `LaunchActivationIntent` alone, decides reuse. |

### Compatibility conclusions

1. **Target identity is namespaced, not globally interchangeable.** Server
   `gxos.builtin.fileexplorer` and C# `gxos.builtin.files` are distinct IDs.
   AMCONV1 supplies one explicit shell-object bridge; it does not establish a
   general app-ID alias rule. Server `gxos.shell.this-system` and C# Root are
   distinct canonical IDs with different path/handler data.
2. **Folder activation is not file opening.** Server folder targets carry a
   canonical directory path into a folder-capable File Explorer activation.
   C# `ForFile` carries a document path through association selection. Reusing
   that document route for `/Documents` would select the wrong semantics.
3. **C# has a usable file-manager factory, not a usable arbitrary-folder
   backend.** `gxos.builtin.files` and its factory can be reused after adding
   explicit path handling, validation, and ownership. Today, a nonempty shell
   filesystem value is parsed as a drive selector; it does not open
   `/Documents` or `/`. The checked-in sources also do not establish which C#
   `File.Instance` or per-drive filesystem corresponds to Server VFS `/`.
   Adding a path parameter alone would not establish filesystem identity.
4. **SystemPanel is a declared C# target kind without a current resolver or
   dispatch implementation.** The `Display Options` app/factory is a candidate
   component, but Server's shell record and Server's current unified Settings
   launch resolution do not agree on one handler ID. No Settings mapping is
   accepted by this design. Control Panel has no C# app backend.
5. **Resolution result vocabularies differ.** Server launch/folder APIs expose
   booleans and error strings and use Server process/runtime ownership. C# has
   typed `LaunchResult` and generation-safe application instances. They share
   concepts, not ABI or lifecycle identities.

## 2. Proposed shared shell-target contract

This is a bounded semantic value model. It is not a wire layout and does not
replace either registry or `LaunchRequest`.

```text
ShellTarget {
    CanonicalShellObjectId   // exact canonical ID from the source registry
    InputAlias               // raw request string, retained for diagnostics
    TargetKind               // FileSystemFolder | SystemPanel | VirtualObject
    CanonicalTargetValue     // kind-specific value; see below
    HandlerAppId             // optional, exact backend identity when verified
    Operation                // OpenFolder | LaunchApplication |
                             // ActivateSystemPanel | OpenVirtualObject
    LaunchActivationIntent   // existing C# lifecycle/policy hint; separate
}
```

### Identity and aliases

- Keep each registry's canonical ID stable and local. For Server inputs, the
  canonical IDs are the ten `gxos.shell.*` IDs in the Server matrix above. C#
  keeps its existing `gxos.shell.computerfiles`, `gxos.shell.root`,
  `gxos.shell.usbdrive`, and `gxos.shell.installtoharddrive` IDs.
- Accept only an ID or alias explicitly registered by that source registry or
  by an explicit compatibility adapter. Preserve `InputAlias` and the
  canonical source ID separately. Aliases do not make the underlying IDs
  aliases of one another.
- Preserve the existing AMCONV1 Files request: backend target app ID
  `gxos.builtin.files`, local C# shell ID `gxos.shell.computerfiles`, virtual
  app-launch value `Computer Files`, and intent `Launch`. Keep Server
  `gxos.builtin.fileexplorer` distinct.
- Existing comparators are case-sensitive. AMCONV2B should retain that
  behavior for current IDs and aliases; case folding is not implied by this
  contract.

### Target kinds, values, and operation

| Target kind | Canonical value | Operation | Handler rule |
|---|---|---|---|
| `FileSystemFolder` | Absolute, root-relative virtual filesystem path using `/`; canonical root is `/`. Examples are `/` and `/Documents`. It is not a host path, URI, device name, or document path. | `OpenFolder` | Require a backend that explicitly accepts folder activation and validates the path as an existing directory. The Server source handler is File Explorer. C# `gxos.builtin.files` is a possible implementation component only after its factory gains path-aware folder support. |
| `SystemPanel` | The canonical shell-object ID identifies the panel. Keep `CanonicalTargetValue` empty when the registry value is empty; do not synthesize a panel name from a dispatch label. | `ActivateSystemPanel` | Require a verified, panel-capable backend binding for that exact ID. An app descriptor with a similar display name is insufficient. |
| `SystemPanel` | Same identity/value rule. | `LaunchApplication` | Use only when the source behavior is explicitly an application launch. Server `gxos.shell.files` currently names `AppSystemPanel`, but its verified AMCONV1 behavior is to launch the File Explorer application. Preserve that special route rather than treating it as a generic panel activation. |
| `VirtualObject` | Exact registry `canonicalTargetValue` (for example `DesktopHome`); empty remains empty. | `OpenVirtualObject` or `LaunchApplication` only when the source behavior defines it | Require an explicit virtual-object backend. No current C# mapping exists for Server Desktop or Trash. |

The target kind describes what is being opened. `Operation` describes how it is
opened. `LaunchActivationIntent` remains the existing C# app-instance policy
hint and must not be overloaded to mean “open folder” or “activate panel.” For
example, a Documents request is `FileSystemFolder` + `OpenFolder`; it is not a
`FileOpen` document request and it is not an application-name launch.

### Canonical path and bounds

- Shared folder values use absolute VFS virtual paths, forward slashes, and
  root `/`. Do not pass a host filesystem path or a drive selector in this
  field.
- Server already normalizes virtual paths by replacing backslashes, anchoring
  relative values at `/`, and applying lexical normalization in
  `DesktopFolderResolver::NormalizeVirtualPath`. The folder dispatcher then
  checks the normalized path and directory type. AMCONV2B must keep the
  canonical values from the Server registry (`/`, `/Documents`, etc.) and
  reject any normalization that escapes the VFS root.
- For a shared shell-target string, use a conservative maximum of **1,024
  UTF-8 bytes** and valid text. Also retain C#'s existing 1,024 UTF-16-code-unit
  `LaunchRequest.MaxTextLength` guard. These are validation bounds, not a
  shared ABI. Server's existing 4,096-byte launch/folder limits remain valid
  for Server-only routes; the adapter must enforce the narrower shared bound
  before it constructs a C# request.
- Reject empty IDs, invalid encoding, control characters, overlong text,
  malformed kind/value combinations, and noncanonical path syntax as
  `MalformedRequest`. A known target with no capable backend is
  `UnsupportedTarget`; an unknown ID/alias remains `NotFound`.

### Backend capability and current support

The backend binding must state the operation it supports. A handler app ID by
itself is not proof of folder or panel capability. In particular,
C# `ApplicationShellPolicy.AcceptsFolderTargets` is current descriptor
metadata; the source factory still interprets a nonempty filesystem target as
a drive name. `ModernShellAdapter` must use explicit `SystemPanel` handling
instead of letting its current default branch coerce unknown kinds to
`Virtual`.

| Server target(s) | AMCONV2A classification | Reason |
|---|---|---|
| `gxos.shell.files` and its registered aliases | Supported as the existing AMCONV1 **application launch** only | Explicit Server-to-C# adapter and existing `gxos.builtin.files` factory path. No path or panel claim. |
| `gxos.shell.this-system` (`/`) | Unsupported in current C#; AMCONV2B investigation candidate | C# Root resolves with empty path and no handler. Generic folder activation is absent, and the Server VFS root has no verified C# filesystem mapping. |
| Documents, Pictures, Music, Network | Unsupported in current C#; AMCONV2B Documents investigation candidate | No C# registry entries or initial-folder support; existing Computer Files factory cannot consume these paths, and no Server-to-C# filesystem root mapping is established. |
| Settings | Unsupported pending exact backend mapping | Server shell registry points at `gxos.builtin.displayoptions`; Server label launch resolves unified `gxos.builtin.settings`; C# only has Display Options. |
| Control Panel, Desktop, Trash | Unsupported | No exact C# target/backend. |

`DeviceVolume` remains a separate C# concept and is outside this Server mapping.
Do not encode `USB Drive N`, `Hard Disk`, or an installer action as a
`FileSystemFolder` or `SystemPanel` to make it fit this contract.

### Compatibility with `LaunchRequest` and `LaunchResult`

For supported targets, construct the existing
`LaunchRequest.ForShellObject(...)`; do not add an ABI or a parallel registry.
Use `TargetAppId` for the **receiving C# backend's** canonical app ID, not the
Server's app ID. Use `ShellTargetKind.FileSystem` with the canonical path for a
folder target, `SystemPanel` for a verified panel, and the existing virtual
kind for the AMCONV1 Files app launch. Preserve `LaunchRequest.TargetKind` as
`ShellObject`. Do not use `ForFile` for a folder.

The existing `SourceShellObjectId` field carries source context; the adapter
must keep the current AMCONV1 Files value stable and preserve source canonical
IDs for new Server-only target requests. `TargetAppId` remains independently
bound to the verified C# backend. Retain the raw input and matched alias in
the resolution object for diagnostics. If an adapter cannot preserve both
source identity and backend identity without changing established AMCONV1
behavior, resolve that compatibility issue in tests before changing code.

Return the existing C# `LaunchResult` unchanged: success reports the selected
backend app ID and C# generation-safe instance handle; failure has no instance
handle and carries the mapped typed error. A Server PID, process/runtime ID,
folder-handler registration owner, or generation must never be represented as
a C# instance handle. The Server bool/error API is not ABI-compatible with
`LaunchResult`.

Recommended error distinctions:

| Condition | C# result |
|---|---|
| Empty, malformed, invalid encoding/path, over bound, or target/value mismatch | `MalformedRequest` |
| Unknown ID/alias | `NotFound` |
| Known source target lacking an explicitly supported C# operation | `UnsupportedTarget` |
| Canonical directory missing or no longer a directory | `ResourceUnavailable` |
| Filesystem denies inspection/open | `PermissionDenied` |
| A declared backend is unavailable at dispatch time | `BackendUnavailable` |
| Factory/window construction fails | `InitializationFailed` or `ActivationFailed`, according to the failing stage |
| Alias collision, if introduced by a registry edit | `AmbiguousTarget` |

None of these failures may fall through into `AppLaunchResolver` as an app
name, a document association, a virtual target, or a legacy arbitrary-string
launch.

## 3. Exact adapter integration points

1. **Server identity source —** `shell_object_registry.h`
   (`ShellObjectRegistryRecord`, `FindShellObjectRegistryRecordById`,
   `FindShellObjectRegistryRecordByAlias`). Treat its fields as the source
   descriptor; do not edit this protected Server source as part of AMCONV2B.
2. **C# Server compatibility map —**
   `ServerShellObjectCompatibilityAdapter` in
   `guideXOS/OS/ModernAppModel.cs:1286-1317`. Extend with explicit typed
   descriptors for the approved Server IDs and aliases. Keep known-but-
   unsupported IDs explicit so they return `UnsupportedTarget`, not `NotFound`
   or an app-name fallback.
3. **Typed target/request projection —** `ModernShellAdapter` in
   `guideXOS/OS/ModernAppModel.cs:1319-1392`. Replace the default-kind
   coercion with explicit target-kind and operation checks. Build
   `LaunchRequest.ForShellObject` only after validation and backend-capability
   resolution.
4. **Shell service dispatch —** `CSharpApplicationShellService.OpenShellObject`
   in `guideXOS/OS/ApplicationShellServices.cs:343-374`. Keep Action routing
   separate, validate that a typed target has a capable handler, then route
   through `LaunchModern` and `ApplicationFactoryRegistry`.
5. **Folder backend —** `ComputerFilesApplicationFactory` in
   `guideXOS/OS/ApplicationFactories.cs:1117-1218` plus the constructors/path
   initialization in `guideXOS/DefaultApps/ComputerFiles.cs:14-20,74-145`.
   Add an explicit folder-path branch separate from drive-name resolution.
   Validate the existing directory before creating a window; retain the
   current drive-selector branch unchanged.
6. **Instance and resource ownership —** existing
   `ApplicationFactoryRegistry.TryLaunch` and
   `ApplicationInstanceRegistry.TryBeginLaunch` (`ApplicationFactories.cs:361-440`;
   `ApplicationInstance.cs:642-770`). Keep each folder request owned by its
   app instance and each created window attached to that instance. If a new
   filesystem object is allocated, make its owner and disposal point explicit
   in the factory/window path.
7. **Diagnostics/tests —** extend the existing `ApplicationDescriptorRegistry`
   App Model self-test path (`ModernAppModel.cs:840-1018`) with the matrix and
   negative cases below. These assertions are not hosted acceptance until
   they compile and execute under the applicable acceptance environment.

No Server source change, second C# registry, new process model, or new launch
result struct is proposed.

## 4. Detailed AMCONV2B test cases

These are test specifications only; AMCONV2A did not add or run tests.

### Resolver identity and aliases

1. Resolve every Server canonical ID to the expected source ID, target kind,
   canonical value, default handler ID, and operation. Assert the values listed
   in the matrix exactly.
2. Resolve every registered alias to the same target descriptor and retain
   both raw input and matched alias. For `gxos.shell.files`, cover `Files`,
   `File Manager`, `File Explorer`, and `FileExplorer`; preserve the three
   explicitly bridged aliases plus C#'s existing `File Explorer` alias.
3. Verify canonical C# IDs and existing aliases remain valid. Assert no global
   alias between `gxos.builtin.fileexplorer` and `gxos.builtin.files`, or
   between `gxos.shell.this-system` and `gxos.shell.root`.
4. Verify comparisons preserve existing exact case behavior. Verify unknown
   nonempty IDs return `NotFound`; known unsupported Server IDs return
   `UnsupportedTarget`.
5. Verify repeated resolution does not grow the static app descriptor count,
   shell registry count, or retained request/session count.

### Typed folder requests

6. Resolve `gxos.shell.this-system` and `Computer` to a folder operation with
   canonical value `/`, Server handler identity
   `gxos.builtin.fileexplorer`, and no document association. Until the C# folder
   backend exists, assert `UnsupportedTarget` rather than opening the default
   drive chooser.
7. Resolve `gxos.shell.documents` and `Documents` to folder operation with
   canonical value `/Documents`. When the future backend is enabled, assert
   the request has `TargetKind=ShellObject`, `ShellTargetKind=FileSystem`,
   `ShellTargetValue=/Documents`, C# handler ID `gxos.builtin.files`, and
   `ActivationIntent=Launch`; assert the created Computer Files window opens
   that directory.
8. Assert `/Pictures`, `/Music`, and `/Network` remain explicitly unsupported
   until individually included in an approved implementation scope. Do not
   infer support merely because the folder factory is generalized later.
9. Assert an existing directory opens, a missing path and an existing
   non-directory return `ResourceUnavailable`, and permission/backend failures
   map to their typed errors without a generic app fallback.
10. Assert `ForFile`/file association is not used for a folder. Include a
    `.txt` file open regression so normal document dispatch remains unchanged.
11. Reject empty IDs, null requests, controls, invalid text, overlong ID/path,
    unsupported target kind, missing folder value, malformed absolute path,
    and a value that escapes the VFS root as `MalformedRequest`.
12. Verify the shared 1,024-byte UTF-8 text cap and C# 1,024 UTF-16-code-unit
    request cap at boundary, over-bound, and multibyte-string cases.

### Virtual and system-panel targets

13. Keep the AMCONV1 Files path unchanged: Server Files ID/aliases create the
    existing virtual shell-object app launch to `gxos.builtin.files`; no
    filesystem path is attached.
14. Assert `gxos.shell.settings` is rejected as unsupported until Server
    registry and Server label-launch behavior are reconciled with an exact C#
    panel backend. Assert Control Panel, Desktop, and Trash are unsupported.
15. Assert `SystemPanel` is never silently projected to `Virtual` or
    `FileSystem`; panel dispatch requires an explicit backend capability and
    exact canonical target identity.

### Lifecycle and results

16. On successful folder launch, verify the C# result contains
    `gxos.builtin.files`, a valid generation-safe instance handle, and a
    successful activation state; do not compare it to a Server PID.
17. With the existing `gxos.builtin.files` `MultiInstance` policy, open two
    different folder targets and verify they own separate instances and
    windows with the correct request path. Close each window and verify the
    owning instance releases its request and any filesystem resource exactly
    once.
18. Inject factory failure after request/instance creation; verify the
    instance and partially created window/filesystem are cleaned up and the
    result has no valid instance handle.
19. Verify capacity exhaustion maps to `ResourceUnavailable`; unsupported
    server object and malformed input failures allocate no application
    instance.
20. Verify service result mapping preserves `UnsupportedTarget`,
    `InvalidRequest`, `ResourceUnavailable`, and `PermissionDenied` through
    `ApplicationShellResult` (`ApplicationShellServices.cs:12-80`).
21. If a Server-to-C# filesystem binding is approved, verify `/` and
    `/Documents` are resolved against that exact mounted filesystem, not merely
    a similarly named drive. Verify launching/closing a folder target does not
    change the process-global `File.Instance` or dispose a filesystem still
    owned by another window. If no binding is approved, assert the Server
    filesystem targets remain `UnsupportedTarget` without invoking a factory.

## 5. Compatibility and lifecycle risks

- **Root is not already mapped.** C# `gxos.shell.root` has `Path=""`, no
  `AppId`, and therefore reaches `OpenShellObject`'s no-handler rejection.
  Mapping it to Server `/` would invent path equivalence.
- **Folder capability is not implemented by the declaration alone.** C# files
  advertises folder-target acceptance but the current factory treats any
  nonempty filesystem target as a drive name and cannot start at an arbitrary
  directory.
- **Handler IDs differ.** `gxos.builtin.fileexplorer` must not be silently
  canonicalized to `gxos.builtin.files`. AMCONV1 is a narrow explicit bridge
  for opening the File Explorer/Computer Files app. Extending that mapping to
  folders requires a separate folder-capability test.
- **Settings identity diverges within Server source.** The registry's
  `gxos.shell.settings` record names `gxos.builtin.displayoptions`, while the
  label resolver maps Settings to `gxos.builtin.settings`. C# contains only
  Display Options. Keep the entire Server target unsupported until a source
  owner establishes the intended backend identity and behavior.
- **Path vocabularies differ.** Server shell paths are absolute virtual VFS
  paths; C# document strings may be relative and `FileSystem` is also used to
  carry device names. Do not overload the new folder field with documents,
  host paths, or volume labels.
- **Filesystem identity is not established.** Server's `/` is the Server VFS
  root. C# `File.GetFiles` uses the current static `File.Instance`, while
  Computer Files can use separate `AutoFS` instances for drives. The checked-in
  sources do not identify a Server VFS mount that is the same storage as any
  C# mount. A path-aware constructor is insufficient; AMCONV2B must verify the
  source-to-backend filesystem binding or keep these folder targets
  `UnsupportedTarget`.
- **System-panel operations are not app aliases.** A C# factory that launches
  an application window does not, by itself, prove support for the Server
  `SystemPanel` target kind. Exact operations and side effects must be
  verified per panel.
- **Instance intent and instance policy differ.** The C# Files descriptor is
  `MultiInstance`. Existing `LaunchActivationIntent` is retained in the
  request, while registry reuse behavior is controlled by descriptor policy.
  Tests must check path-specific contexts and not infer folder-window reuse
  from an intent enum value.
- **Ownership does not cross runtimes.** Server carries handler registration
  owner/generation in folder activation and launches a process. C# instances
  own `LaunchRequest` and windows with generation-safe handles. Do not retain
  Server registry pointers/handles or convert a process ID to an instance
  handle.
- **The C# filesystem is mutable global state in some existing routes.**
  `AutoFS()` writes its implementation to `File.Instance`, while other
  Computer Files paths own a per-drive filesystem. A new folder branch must
  name the mount owner and avoid changing or disposing the process-global
  filesystem as a launch side effect.
- **Server folder creation behavior is side-effectful.** Its standard-folder
  route calls `EnsureExists(..., createIfMissing=true)` before opening. The C#
  adapter must not create a missing directory merely to imitate this behavior
  without an explicit filesystem policy. The bounded B scope should require
  an existing folder and report `ResourceUnavailable`; directory creation is
  a separate behavior decision.

## 6. Bounded AMCONV2B implementation plan

1. Restore and validate the managed-artifact build prerequisites listed below
   before any production C# edit. Preserve the current AMCONV1 source and
   compatibility assertions.
2. Verify, from source and an approved filesystem contract, which C# mount and
   path namespace correspond to Server VFS `/`. If no one-to-one binding can
   be established, keep every Server filesystem target explicitly
   `UnsupportedTarget`; do not map `/Documents` onto the current `File.Instance`
   or `AutoFS` by assumption.
3. If that filesystem binding is verified, add typed Server target descriptors
   for `gxos.shell.this-system` and `gxos.shell.documents` only. Keep their
   source IDs, aliases, path values, and Server handler identities exact. Keep
   other Server filesystem targets explicitly unsupported for this bounded
   phase.
4. Route the two verified targets through the existing `ModernShellAdapter`,
   `LaunchRequest.ForShellObject`, shell service, app factory registry, and
   `gxos.builtin.files` factory. Add a path-aware folder initialization path
   to Computer Files, separate from device-volume resolution. Require the
   directory to exist and be a directory; do not create it implicitly. Keep
   folder-specific filesystem ownership local to its app instance and avoid
   changing the process-global `File.Instance` as a side effect.
5. Keep `gxos.shell.files` on its existing AMCONV1 app-launch route. Keep all
   system-panel/virtual targets unsupported unless a later exact C# backend
   is source-verified. In particular, do not accept Settings or Control Panel
   as app-name aliases.
6. Implement the test cases above that cover supported and unsupported
   targets, malformed input, error mapping, and instance/resource cleanup.
7. Compile the C# App Model after the managed identity source is generated by
   the supported validated pipeline. Record hosted compile separately from
   source assertions and from any guest acceptance. Do not describe source
   checks as compiled implementation.

This plan does not include app-ID unification, general Server package
resolution, manifest scanning, a new shell registry, automatic creation of
standard folders, Settings/Control Panel mapping, Trash, desktop virtual
objects, or device-volume mapping.

## 7. Prerequisites before source changes and hosted acceptance

AMCONV1 hosted qualification is blocked before C# compilation because
`guideXOS/obj/ManagedArtifactIdentities.g.cs` is absent and the supported
generator requires validated managed build records. The AMCONV1 closeout says
the APP_MODEL checkout lacks the matching Phase 29-35 build records and Phase
23 runtime pack/source-patch evidence; the available external build records
do not match this checkout's staged payloads. Do not fabricate any identity
source or proof metadata.

Before AMCONV2B source changes or hosted acceptance:

1. Restore the pinned runtime source checkout and accepted Phase 21-23 patch
   evidence required by the repository's supported build pipeline.
2. Restore/build and stage all 41 managed proof variants for Phases 29-35,
   including matching EXE/GXMI payloads, build records, lengths, hashes, and
   stage equality required by `Tools/ManagedArtifacts/managed_artifacts.py`.
3. Generate `ManagedArtifactIdentities.g.cs` through the supported generator
   and verify it against those records; do not create it by hand.
4. Run the supported hosted App Model compile and record its result. Only then
   treat the implementation as compile-verified. Guest App Model diagnostics
   remain a separate acceptance gate and were not run or requested in AMCONV2A.
5. Reconfirm that the read-only Server reference still has the source snapshot
   used for this design before accepting any Server-side comparison.

AMCONV2A did not attempt the 41-variant build chain, hosted compilation,
QEMU, or QAC. It did not edit production code, regenerate managed identity
artifacts, modify protected mainline or Server sources, or change branches.

## 8. AMCONV2A completion record

- **Result:** `AMCONV2A_CONTRACT_READY`.
- **Starting HEAD:** `170553766943cb0f64daa025b6e0c6608a9e1f89`.
- **Ending HEAD:** this document's local commit; its hash is reported in the completion summary.
- **Document:** `docs/AMCONV2_TYPED_SHELL_TARGET_DESIGN.md`.
- **Production code modified:** No. Only this design document is added.
- **Verification:** source inspection and documentation review only; no build,
  guest, QEMU, or QAC acceptance claim.
