# Phase 33 — Managed Shell Object Invocation

## Result

**Outcome A:** a separately compiled NativeAOT Ring 3 requester used the public
`GuideXos.User` API to open the existing Computer Files Shell object through the
Phase 9 Shell service. The kernel derived caller authority, the existing Shell
resolver selected the Computer Files factory, and the App Model retained
ownership of the target application's lifecycle.

The public managed surface exposes one object value and one operation. It does
not expose command text, factories, service contexts, or kernel/UI handles.

## Phase 9 Shell audit

The live Phase 9 service is `ApplicationServiceId.Shell` (`7`). Its
`ApplicationShellOpenRequest` supports these five request kinds:

| Kind | Meaning |
|---|---|
| `ApplicationId` | Launch a stable application ID through the modern descriptor and factory registries. |
| `Alias` | Resolve a name or compatibility alias through the launch resolver. |
| `Document` | Resolve an association and dispatch the associated document request. |
| `ShellObject` | Resolve a registered Shell object through `ModernShellAdapter`. |
| `TypedShellAction` | Resolve a registered action name and invoke its typed backend. |

The Phase 9 request bound is `ApplicationShellOpenRequest.MaxTargetLength =
1024` UTF-16 code units. The request is copied into the session record; the
Ring 3 adapter copies the fixed wire record into kernel-owned storage and
decodes a kernel-owned string. The result is a typed `ApplicationShellResult`
with a `LaunchErrorCode`-derived result and bounded diagnostic. The Ring 3
surface returns only the copied result code.

### Registered Shell objects and actions

The current registry contains four descriptors and four object kinds, plus the
`Unknown` result kind:

| Shell ID | Kind | Parameters and current behavior | Normal managed exposure |
|---|---|---|---|
| `gxos.shell.computerfiles` | `BuiltInApp` | No parameter. Resolves to `gxos.builtin.files` and `ComputerFilesApplicationFactory`; opens the built-in app/window. Aliases: `Computer Files`, `File Explorer`. | Safe for this proof: fixed canonical ID, existing normal Shell authorization and factory path. |
| `gxos.shell.root` | `FileSystemLocation` | Registered path is the empty string (root). The legacy desktop route opens the Computer Files root view. The modern Shell service currently has no application handler for it and returns `UnsupportedTarget`. Alias: `Root`. | Not selected: modern backend is incomplete. |
| `gxos.shell.usbdrive` | `DeviceVolume` | Registered device name `USB Drive`; alias matching accepts the exact alias and names beginning `USB Drive` (for example, `USB Drive 0`). The legacy desktop route depends on an attached volume. There is no modern application handler, and the QEMU environment has no USB volume. | Not selected: device-dependent, modern backend is incomplete. |
| `gxos.shell.installtoharddrive` | `SystemAction` | No caller text parameter. Resolves to the single typed action name `HDInstaller`, whose backend opens the installer surface. | Not exposed: it is an installation/system action. The Phase 33 kernel gate rejects it before backend dispatch. |

The registered typed-action set currently contains only `HDInstaller`. In the
modern target-kind mapping, `BuiltInApp` maps to `Virtual`, filesystem locations
and device volumes map to `FileSystem`, and system actions map to `Action`. No
registered object currently maps to `SystemPanel`. Aliases participate in the
existing registry, but the Phase 33 public API uses only the canonical
Computer Files object ID.

### Begin, Observe, Cancel, and authorization

`CSharpApplicationShellService.Begin` validates the bounded request and
authorizes a new interaction only while the owner application is `Running` or
`Activated`. It creates a request session, dispatches the typed request
synchronously, converts the `LaunchResult`, and completes the session before
returning its request handle. An unexpected backend exception becomes a typed
backend failure.

`Observe` returns the terminal typed result and consumes the terminal session.
`Cancel` can cancel a pending Shell request, but returns `InvalidState` for a
terminal request. Because this adapter completes synchronously, the Ring 3
transport calls `Begin` and then `Observe` once; Phase 33 does not publish a
pending handle or cancellation API to managed callers.

The service context contains a generation-safe owner identity and capabilities.
The caller does not supply these fields on the wire. Existing-request checks
accept `Running`, `Activated`, and `Inactive` owners; new Shell interactions
remain limited to `Running` and `Activated`.

## Selected operation and public API

The new public surface is:

```csharp
public enum GuideXosShellObject : uint
{
    ComputerFiles = 1,
}

GuideXosShell.TryOpenShellObject(
    GuideXosShellObject shellObject,
    out GuideXosLaunchResult launchResult)
```

The enum contains only the supported managed-facing case. Unknown numeric
values return `GuideXosStatus.InvalidArgument` and an
`GuideXosLaunchResultCode.InvalidRequest` without entering the service. The
wrapper checks ABI compatibility, copies the canonical ID, and returns the
existing transport result plus typed Shell result. Raw ABI structs, service and
operation IDs, and transport functions remain internal to the SDK.

Computer Files is the smallest complete target: it is already registered, has
deterministic modern resolution, opens through the normal factory, needs no
caller-selected file path, and does not require elevated authority or direct
GUI-object access.

## Wire format and authority path

Phase 33 reuses the Phase 31/32 fixed Shell envelope:

| Field | Value |
|---|---|
| Shell service | `7` |
| Operation | `3` (`OpenShellObject`) |
| Canonical target | `gxos.shell.computerfiles` (24 UTF-16 code units) |
| Existing Phase 9 maximum | 1,024 UTF-16 code units |
| Request record | 2,084 bytes: 36-byte header and fixed 2,048-byte UTF-16LE target field |
| Response record | 16 bytes: structure version, size, typed result code, reserved field |
| Encoding | UTF-16LE code units; explicit length, no terminating NUL contract |

The request contains structure/service/operation versions, lengths, a response
buffer pointer, a zero reserved field, and bounded target data. It contains no
caller AppId, process handle, `ApplicationInstance` handle, Shell context,
factory ID/pointer, target object pointer, or launch authority. The kernel
validates the response buffer as writable user memory and copies only the fixed
typed response back.

For every service request, the kernel derives the current scheduled
`Ring3Process`; reads its kernel-owned `OwningApplicationInstance` value;
resolves that live owner through `ApplicationInstanceRegistry`; creates a
fresh `ApplicationServiceContext`; validates that the context resolves to the
same owner and the Shell service; and obtains the registered Shell access
object. The lifecycle gate remains `Running` or `Activated`. After this
identity and context derivation, operation 3 explicitly rejects any requester
outside those lifecycle states as `InvalidState`, then its allowlist admits
exactly `gxos.shell.computerfiles` before calling Phase 9. A caller cannot use
this operation to select another object, action, or factory.

The executed path is:

```text
GuideXosShell.TryOpenShellObject
  → copied operation-3 request
  → current Ring3Process and owner derived by kernel
  → fresh Shell service context and lifecycle authorization
  → existing Phase 9 Begin / Observe
  → ShellObjectRegistry and ModernShellAdapter
  → LaunchRequest(ShellObject)
  → ApplicationDescriptorRegistry
  → ApplicationFactoryRegistry / ComputerFilesApplicationFactory
  → copied typed result
```

The proof reached the resolver with `gxos.shell.computerfiles`,
`ShellObjectKind.BuiltInApp`, and `gxos.builtin.files`; the factory was
`ComputerFilesApplicationFactory`. Each successful target had one owned window
and reached `Activated`. The managed caller received only `Success`.

## Proof modes and runtime results

The separately compiled NativeAOT proof app uses only the public
`GuideXos.User` API. Its normal `Main` checks that enum value `99` is rejected
locally, requests Computer Files, checks both transport and typed result, and
returns `33` only on success. Other build modes exercise FailFast, stale owner,
raw invalid action, and malformed operation handling.

The fresh QEMU run (`out/phase33-validation-final-security.log`) completed with
`RING3_PHASE33_COMPLETE=1` and recorded:

| Gate | Result |
|---|---|
| Fresh successful requesters | Four independent initial requester lifetimes returned `33`. |
| Repeated target behavior | Four distinct Computer Files targets were retained simultaneously before normal cleanup; 25 additional action lifetimes each created and cleaned a target. The multi-instance behavior was preserved. |
| Total successful `Main` returns | 30: four initial requesters, 25 stress requesters, and the replacement requester. |
| Typed backend evidence | 31 factory launches total, all on `ComputerFilesApplicationFactory`; zero compatibility fallback and zero Legacy backend calls. |
| Requester exit | Each normal requester process and authority was destroyed; a later action required fresh authority. |
| FailFast | The action completed first, the requester terminated with `-1`, and the Activated Computer Files target persisted. |
| Replacement | A fresh requester invoked the action and returned `33`. |
| Stale process, owner, context, request | All rejected with no factory, fallback, or Legacy backend effect. A fresh requester remained successful. |
| Invalid enum | Public enum value `99` returned `InvalidArgument` locally and `InvalidRequest` as the out result; no service call was made for that value. |
| Raw invalid action | Operation 3 carrying `gxos.shell.installtoharddrive` first derived the process, live owner, fresh Shell context, and allowed requester lifecycle, then was rejected as `UnsupportedTarget` by the kernel object allowlist; no backend action occurred. |
| Malformed request | Operation ID `99` was rejected as `InvalidArgument` before Phase 9; service request count and backend effect were both zero. |
| App Model balance | Targets `0→0`, windows `6→6`, active Shell requests `0`, factory launches `0→31`, fallback delta `0`, Legacy delta `0`; requester authority released. |
| Process and allocator | Process/user-thread cleanup balanced; `freeInvalid=0`, `freeCorrupt=0`, `freeNoPages=0`. |
| Guest faults | No `#PF`, `#GP`, `#UD`, or ABI-gate panic appeared in the completed run. |

Target termination removes the `ApplicationInstance` synchronously. Window
manager removal runs in its serialized frame cleanup, so the proof checks
registry removal per target and checks the window count after the entire suite
has allowed normal cleanup to run. The final count returned to the initial
value without a leaked target or window.

## NativeAOT artifact and admission

All five proof modes target `guidexos-x64`; each canonical payload is 706,560
bytes. The success artifact is a six-section fixed-base AMD64 PE32+ image with
entry RVA `0x1430` (`wmain` VA `0x401000001430`), `Program.Main` at
`0x401000065d70`, and the generated managed Main wrapper at
`0x40100006b8d0`.

| Mode | Result expected by proof | Canonical SHA-256 | Descriptor flags |
|---|---:|---|---|
| success | `33` | `07C69ADB76089C1EDF30DC1B238377928D103BB97039CAD560B8D77F7F83D30A` | `0xC200001F` |
| failfast | `-1` | `42AC5498B225CA7652441075C2B8D17F3E1DF0E0930A5E3A1C83043F3DF90906` | `0xC400001F` |
| stale owner | `33` for expected `InvalidContext` | `DAAF886D66BC7E9054FD6DF1A63F0E703F8FC60B4369CEAA1931FAB1F6A97373` | `0xC800001F` |
| invalid action | `33` for expected `UnsupportedTarget` | `A99F2626BD3A3AF65D0F4E9B26C0A6DBED932E21E432CD4153110D6915414F54` | `0xD000001F` |
| malformed | `33` for expected `InvalidArgument` | `601B288BB32D80A7BA389F543088E37F49EE4133F4456A0BDE68B29708572A45` | `0xE000001F` |

The Phase 33 verifier reported zero PE imports, zero direct kernel imports,
zero foreign link inputs, no relocations, no TLS directory, and zero RWX
sections for every mode. The success payload's used PAL imports are
`guidexos_pal_abi_version` and `guidexos_pal_service_request`; the FailFast
variant additionally calls `guidexos_pal_fail_fast`. There are no Windows
process, dynamic loading, networking, or new filesystem dependencies. The map
retains the same 42 shared `System.IO`/path symbol records present in the Phase
32 runtime closure; Phase 33 adds none.

The Phase 32 success artifact in this build cohort is also 706,560 bytes. The
Phase 33 map has 7,412 symbol records, equal to Phase 32. The map/runtime audit
counts are unchanged from Phase 32: 282 allocation helpers, 1,863 GC helpers,
1,968 exception helpers, and 86 TLS helpers. GC / TLS / writable-static map
sections remain `387 / 38 / 386` (delta zero). Phase 33 changes the managed API
and proof entrypoint without changing the NativeAOT runtime closure.

Each mode passed strict descriptor, target, link-map, import, relocation, TLS,
and section checks. For all five modes, the verifier rejected all three
mutations: wrong proof-mode flags, descriptor digest mutation, and artifact
byte mutation. Two independent NativeAOT builds produced identical
timestamp-normalized canonical SHA-256 values for all five payloads; raw
compiler and Phase 26 patch hashes can differ because PE/debug timestamps are
normalized before descriptor creation.

The H2c admission pipeline stages each `.exe` / `.gxmi` pair and generates
kernel admission identities from the staged canonical executable. The full
Phase 33 diagnostic build reported 27 managed artifacts and agreement `27/27`
for descriptors, build staging, ramdisk staging, and compiled allowlist.

## Regression evidence

Fresh QEMU controls completed with `DIAGNOSTIC_COMPLETE`:

| Control | Result |
|---|---|
| Phase 32 | Five successful returns of `32`; OpenDocument fixture and negative cases passed; target cleanup, windows `6→6`, process cleanup, and App Model balance passed. |
| Phase 31 | Five successful returns of `31`, including replacement; Calculator resolved and activated through its typed factory; invalid-target, oversize, stale-owner and FailFast probes passed; six targets were cleaned to baseline. |
| Phase 30 | Repeated managed Clipboard lifetimes returned `30`; cross-process persistence, malformed/oversize/empty/clear, FailFast, replacement, stale-owner, and cleanup checks passed. |
| Phase 29 | Repeated managed Notifications lifetimes returned `29`; title/body bounds, invalid type, FailFast, replacement, stale-owner, and cleanup checks passed. |
| Phase 28 | Four repeated SDK lifetimes returned `28`; typed failure, exit, FailFast, new-generation identity, stale identity/context, and cleanup checks passed. |
| Phase 27 | Four successful managed service lifetimes returned `27`; typed failure and stale-owner/context probes passed with balanced cleanup. |
| Phase 26 | Four NativeAOT bootstrap lifetimes returned `42`; managed/bootstrap/process cleanup balanced. |

The fresh `AppModel` run passed with 12 modern descriptors, 12 factory
registrations, zero factory fallbacks, lifecycle self-test `15/15`, and taskbar
grouping self-test `16/16`. Compatibility facade counters were 3 calls, 2
modern translations, 0 Legacy backend calls, and 1 expected negative
compatibility failure. Stale taskbar projection was zero. The Phase 8
application-service self-test passed. Phase 9 dialog, file, and Shell service
self-tests passed; orphan dialog and stale service-context counts were zero.
Phase 10 storage and Phase 11 clipboard contract/self-tests all passed.

Desktop controls completed 100 Start-menu open/close cycles and 50 Calculator
foreground / Start / taskbar switches. Start normal, hover, and pressed art and
Files, Notepad, Calculator, and Task Manager icons reported `ok` with zero
fallbacks. Desktop icons and the Start icon were drawn, each taskbar sample had
the expected button/icon count and no hidden or invalid entries, activation
reached 50, graphics invariants were valid, and allocator free-error counters
remained zero.

## Security invariants and boundaries

- Caller identity comes from the current kernel-owned `Ring3Process`.
- The target is a bounded copied enum-to-ID value; it is not a backend pointer.
- Operation 3 admits only the canonical Computer Files object.
- The Phase 9 resolver, descriptor registry, factory registry, and lifecycle remain authoritative.
- No process, App Model instance, service context, factory, or window object crosses into managed code.
- Stale process, owner, and context are rejected; requester termination releases its service authority.
- Invalid enum, invalid raw action, and malformed operation fail closed without a backend effect.
- Strict H2c descriptor/hash admission remains enabled; byte and metadata mutations are rejected.
- The CPL0 `int 0x80` rejection and allocator/vector forensic diagnostics remain enabled; the managed caller uses the existing PAL service request path.

## Phase 34 boundary

The smallest next authority expansion is a design and proof for **managed
read-only resource access through the existing Phase 10 service**. It can
preserve application scoping and avoid introducing a picker, arbitrary shell
command, writable filesystem surface, or managed window-host boundary. Define
the public value and copy semantics against the live Phase 10 contract before
adding any new API.
