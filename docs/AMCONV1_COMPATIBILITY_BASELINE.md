# AMCONV1 Server/C# Compatibility Baseline

**Outcome:** `AMCONV1_PARTIAL`
**Scope:** Source audit and one bounded shell identity compatibility adapter.
**Validation rule:** No QEMU or guest execution in AMCONV1.

Hosted compilation did not reach C# compilation because the checkout lacks a
generated managed-artifact identity source and its Phase 29 build record.
The focused source checks passed; the built-in App Model guest suite remains
unexecuted under the no-QEMU rule.

## Repository provenance

| Checkout | Branch / HEAD | Use |
| --- | --- | --- |
| `D:\dev\guideXOSUEFI_APP_MODEL` | `APP_MODEL` / starting `829f8d565c10ad4d4aacfa96f8d49140dc768ba3` | Only checkout edited for this work |
| `D:\dev\guideXOSServer` | `main` / `080928710398ec02f728e573838d76fc0b0bfe49` | Read-only architecture reference; pre-existing untracked files observed |
| `D:\dev\guideXOSUEFI` | `main` / `829f8d565c10ad4d4aacfa96f8d49140dc768ba3` | Protected read-only mainline; pre-existing modifications and untracked validation artifacts observed |

The APP_MODEL checkout was clean before editing. The other two checkouts were
not modified. No branch switch, merge, rebase, cherry-pick, push, or QEMU run
was performed.

## Existing C# implementation and convergence history

This phase extends the existing model; it does not create another registry or
execution path.

- `guideXOS/OS/AppModel.cs` owns the lazy compatibility app catalog, aliases,
  six extension associations, and C# shell-object registry.
- `guideXOS/OS/ModernAppModel.cs` projects that catalog into immutable
  `ApplicationDescriptor` values, bounded `LaunchRequest` and `LaunchResult`
  contracts, association and shell adapters, and deterministic self-tests.
- `guideXOS/OS/ApplicationFactories.cs` and
  `guideXOS/OS/ApplicationInstance.cs` connect descriptors to the existing
  managed factories and generation-safe application instances. Instances own
  windows; windows do not define application identity.
- `guideXOS/OS/ApplicationServiceRegistry.cs` and `ApplicationServices.cs`
  provide typed, instance-scoped service access and bounded request sessions.
  Existing dialog, open/save file, storage, clipboard, resource, notification,
  and shell services are already present.
- `guideXOS/GUI/TaskbarApplicationEntry.cs` groups windows by instance and has
  regression assertions that selecting another window of the same instance
  does not create lifecycle activation/deactivation churn.
- `guideXOS/Program.cs` has opt-in App Model and App Runtime diagnostics. Its
  App Model suite includes descriptor, association, shell, instance, lifecycle,
  taskbar, factory, compatibility, and service self-tests.

The existing `docs/APP_MODEL_CONVERGENCE.md` records the prior migration
decisions and phases: typed descriptors and launch requests; instance and
window ownership; association/shell adapters; typed application services; and
managed user-service proofs through the later phases. The mainline C# source
also contains explicit Phase 27–35 managed service proof projects. AMCONV1
reuses those contracts and the current factories, compatibility facade, and
diagnostic suite.

## Compatibility matrix

| Contract | Status | Source-verified assessment |
| --- | --- | --- |
| Application IDs | **Partially compatible** | Both use stable `gxos.builtin.*` identifiers and aliases. Server File Explorer is `gxos.builtin.fileexplorer`; C# Computer Files is `gxos.builtin.files`. Server has both `gxos.builtin.settings` and `gxos.builtin.displayoptions`; C# exposes `gxos.builtin.displayoptions`. These are semantic overlaps, not interchangeable IDs. Evidence: Server `built_in_app_metadata.h`; C# `AppModel.cs` and `ModernAppModel.cs`. |
| Launch requests/results | **Partially compatible** | Server has `LaunchTarget` resolution kinds and `DesktopService::ResolveLaunchTarget`, but its desktop launch API still reports `bool` plus an error string. C# has bounded, UI-independent `LaunchRequest`, typed `LaunchResult`, and error codes. A shared semantic vocabulary is feasible; the structs are not ABI-compatible. Evidence: Server `app_launch_target.h`, `desktop_service.h/.cpp`; C# `ModernAppModel.cs`. |
| File associations | **Partially compatible** | Both associate extensions to an application identity and document target. Server associations are manifest/registry-backed, include content type and description, and track handler availability/defaults. C# currently has a static six-extension `.txt`, `.png`, `.bmp`, `.wav`, `.gxm`, `.mue` table; the first four select built-ins and the last two select the GXM backend. Handler IDs and package/runtime metadata differ. Evidence: Server `app_manifest.h`, `app_registry.cpp`; C# `AppModel.cs`, `ModernAppModel.cs`. |
| Shell objects | **Partially compatible** | Both use stable shell IDs and typed resolution. Their ID sets and type systems differ. Server has desktop, this-system, files, common folders, settings, control panel, and trash-open IDs with virtual/filesystem/system-panel targets. C# has computer-files, root, USB volume, and installer IDs with built-in/filesystem/device-volume/system-action kinds. AMCONV1 maps only the verified Server Files route. Evidence: Server `shell_object_registry.h`; C# `AppModel.cs`, `ModernAppModel.cs`. |
| Application lifecycle | **Partially compatible** | Both separate descriptor identity from runtime identity and report lifecycle. C# has a fixed-capacity, generation-safe `ApplicationInstanceRegistry` and callback lifecycle. Server process/native-runtime lifecycle carries process/runtime identity and its own states and cleanup. State names, runtime model, and lifecycle callbacks are not yet one shared contract. Evidence: Server `docs/APPMODEL_PHASE19_INSTANCE_LIFECYCLE.md`, `process.h`, and native runtime sources; C# `ApplicationInstance.cs`, `ApplicationLifecycle.cs`. |
| Window ownership | **Partially compatible** | C# instances own window references and the taskbar groups/selects their windows by generation-safe instance handle. Server associates owned windows/resources with process/runtime records and exposes counts, but its process/window identity is different. Same-instance window switching is explicitly asserted in the C# taskbar self-test. Evidence: Server process/runtime lifecycle sources; C# `ApplicationInstance.cs`, `TaskbarApplicationEntry.cs`. |
| Application services | **Partially compatible** | C# has typed dialog, file-dialog, resource, storage, clipboard, notification, and shell services with bounded sessions and instance ownership. Server exposes desktop, file/document, dialog, and runtime host-call services through its own process/runtime boundary; it does not expose the same service ID and request-handle contract. Evidence: Server `desktop_service.h/.cpp`, runtime host-call sources; C# `ApplicationServices.cs`, `ApplicationServiceRegistry.cs`. |
| Dialogs | **Partially compatible** | Both implement message, open, save, and save-changes UI. Server dialog entry points are static `Show`/`Launch` calls with window/process identity and callbacks. C# additionally exposes bounded request handles, observe/cancel operations, typed outcomes, and app-instance validation. UI, ownership, and completion contracts differ. Evidence: Server `message_box.h`, `open_dialog.h`, `save_dialog.h`, `save_changes_dialog.h`; C# `ApplicationDialogServices.cs`, `ApplicationFileDialogServices.cs`, `ApplicationServiceRegistry.cs`, `GUI/OpenDialog.cs`, `GUI/SaveDialog.cs`, `GUI/SaveChangesDialog.cs`. |
| Manifests/packages | **Partially compatible** | Server has a versioned manifest loader/validator with entry points, architecture, permissions, associations, window hints, and package/runtime fields. C# currently projects a static 12-entry catalog with version/resource/launch-entry metadata and does not scan Server manifests or run Server packages. The shared descriptor subset can converge; executable and package fields stay platform-specific. Evidence: Server `app_manifest.h`, `app_manifest_validator.cpp`, `app_registry.cpp`; C# `ModernAppModel.cs`. |
| Capabilities | **Partially compatible** | Server manifests declare permission names and runtime contexts retain them; validation bounds and checks the declarations. C# service contexts carry a bounded capability list and are checked against live instance identity/generation, while the current built-in descriptor projection advertises an empty capability list. The names and enforcement points are therefore not yet equivalent. Evidence: Server `app_manifest.h`, `app_manifest_validator.cpp`, runtime context; C# `ModernAppModel.cs`, `ApplicationServices.cs`, `ApplicationServiceRegistry.cs`. |

**Platform-specific by design:** Server native ELF/GXApp/package execution and
its process/runtime lifecycle are not binary-compatible with C# UEFI managed
factories, GXM execution, or post-`ExitBootServices` initialization. The common
contract is application semantics, not a shared executable format or runtime.

## AMCONV1 implementation

The Server registry confirms a real shell identity gap. Its `gxos.shell.files`
record is a `FileManager`/`AppSystemPanel` object handled by Server File
Explorer; the registry also accepts `Files`, `File Manager`, and `FileExplorer`
as aliases. C# previously recognized only `gxos.shell.computerfiles` and its
existing `Computer Files` / `File Explorer` aliases.

`ServerShellObjectCompatibilityAdapter` now translates those verified Server
identities to the existing C# `gxos.shell.computerfiles` object. Resolution
continues through `ShellObjectRegistry`, then emits the established
`LaunchRequest`/`LaunchResult` path for C# app ID `gxos.builtin.files`. The
original Server input remains available as `Input`/`MatchedAlias`; the returned
shell-object and source IDs use the C# canonical ID. No app registry or launch
backend was added.

The identity lookup uses bounded string comparisons and adds no table-copy
allocation, recursion, retained object reference, or static initialization.
The resulting target and request use the existing bounded shell/launch
contracts. Empty or overlong inputs return `MalformedRequest`; recognized
Server shell IDs that do not yet have an equivalent C# backend return
`UnsupportedTarget`; unknown IDs remain `NotFound`. This distinction prevents
unsupported targets from being silently treated as app names.

The Server IDs for desktop, this-system, documents, pictures, music, network,
settings, control panel, and trash-open are recognized as unsupported. Their
virtual, filesystem, system-panel, or trash semantics do not have a verified
equivalent launch route in the current C# shell registry/factories. The Server
File Explorer app ID (`gxos.builtin.fileexplorer`) also remains distinct from
the C# app ID (`gxos.builtin.files`); this shell bridge does not claim global app
ID aliasing.

`TryCreateTypedActionLaunchRequest` is now used by the existing typed-action
service path. It accepts only an `Action` target and returns
`UnsupportedTarget` for a normal shell object, preserving the C# installer
action boundary. Existing filesystem-location, virtual, and device-volume
resolution kinds remain separate at the shell registry; a device volume still
projects a filesystem target whose canonical value is the selected volume.

## Regression and validation

`ApplicationDescriptorRegistry.RunSelfTest` now asserts:

- the Server files ID and all three additional Server aliases resolve to the
  same C# shell ID and app ID;
- the C# canonical ID and legacy `File Explorer` name remain valid;
- repeated Server resolution leaves the descriptor count stable;
- filesystem-location, device-volume, virtual, and system-action kinds retain
  their existing target shapes;
- typed-action routing rejects a non-action object;
- a known unsupported Server object returns `UnsupportedTarget`, unknown
  identifiers return `NotFound`, and empty/oversized inputs return
  `MalformedRequest`.

The existing App Model suite also covers stable app IDs and aliases, all six
associations (`.txt`, `.png`, `.bmp`, `.wav`, `.gxm`, `.mue`), unknown
associations, shell objects, instances, lifecycle, taskbar grouping, factories,
compatibility accounting, and services. In particular,
`TaskbarApplicationEntryRegistry.RunSelfTest` checks that switching between
windows belonging to one instance does not trigger a false deactivate/reactivate
pair.

Validation performed:

- **Source contract checks: PASS (9 checks).** Verified the Server Files
  registry record and aliases, C# canonical shell/app identities, adapter
  request routing, typed-action guard, explicit error codes, all six existing
  association extensions, App Model diagnostic wiring, and same-instance
  taskbar invariant.
- **Existing Phase 33 typed shell wire check: PASS.**
  `Tools/Phase33/validate_phase33_wire.py` reported matching 2084-byte request
  layouts, a 16-byte response, and `PHASE33_WIRE_LAYOUT_PASS=1`.
- **Whitespace validation: PASS.** `git diff --check` reported no errors.
- **Hosted C# compile: BLOCKED before C# compilation.** The isolated
  `dotnet build guideXOS/guideXOS.csproj --configuration Release
  -target:Compile -p:UefiDiagnosticMode=AppModel` attempt stopped with
  `CS2001`: `guideXOS/obj/ManagedArtifactIdentities.g.cs` was missing. The
  repository generator, `Tools/ManagedArtifacts/managed_artifacts.py
  --stage generated`, could not create it because
  `out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json`
  is absent. No generated allowlist was fabricated.
- **Built-in App Model and runtime self-tests: NOT RUN.** The App Model,
  association, lifecycle, taskbar, factory, and service assertions execute in
  the UEFI diagnostic path. Running that path requires guest execution, which
  is prohibited for AMCONV1. Guest acceptance remains pending.
- **QEMU: NOT RUN**, as required. Hosted/static evidence is not guest
  acceptance evidence.

The isolated build output is under ignored `out/amconv1-20261028`; it is not
part of the commit.

## Remaining gaps and AMCONV2 recommendation

- Define a cross-platform app-ID alias/version policy before mapping
  `gxos.builtin.fileexplorer` to `gxos.builtin.files` or reconciling Settings
  identities.
- Add explicit C# target backends for Server filesystem folders, system panels,
  desktop virtual objects, and trash-open before accepting those IDs.
- Decide whether typed shell targets should include distinct device/volume and
  action values in the shared request vocabulary.
- Define the shared launch result/error mapping and cancellation/cleanup rules
  while keeping Server process and C# managed-instance identities separate.
- Reconcile manifest metadata and capability names with actual enforcement;
  do not add package scanning or speculative services to C# as part of the
  shell bridge.
- Align dialog ownership/completion at the service boundary, then compare
  Server `onClosed` behavior with C# cancellation and owner-close behavior.

**Recommended AMCONV2 scope:** design and test a shared shell-target value
contract for filesystem folders and system panels, starting with Server
`gxos.shell.this-system` and `/Documents` against the existing C# File Explorer
factory. Require an explicit mapping for target kind, canonical target value,
handler app ID, unsupported error, and resource-unavailable result before
enabling dispatch. This is the next narrow extension after AMCONV1's verified
Files object mapping.

## Commit record

The preflight starting HEAD is recorded above. The resulting ending HEAD and
local commit ID are reported in the AMCONV1 completion report accompanying
this document.
