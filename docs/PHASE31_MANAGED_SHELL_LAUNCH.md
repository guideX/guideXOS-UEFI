# Phase 31 Managed Shell Launch

Phase 31 reaches **Outcome A**. A separately compiled NativeAOT Ring 3
application uses only the public `GuideXos.User` SDK to request launch of a
stable application ID through the existing Phase 9 Shell service. The kernel
derives the requester, the App Model resolves the target and invokes its typed
factory, and the managed caller receives only a copied result code. The
successful proof application's `Main` returns `31` after checking that code.

The request does not grant the managed caller launch authority. It carries no
caller identity, process handle, application-instance handle, service
context, target factory, or kernel pointer. The only pointer in the internal
record is a caller-owned response buffer, which is validated before writing.

## Existing Phase 9 contract

The implementation reuses `ApplicationServiceId.Shell` (`7`) and
`CSharpApplicationShellService`. The Shell service API is `Begin`, `Observe`,
and `Cancel`; this proof runs `Begin` followed by `Observe` on the same fresh
request. The request uses
`ApplicationShellOpenRequest.ForApplicationId(stableId)`. The service routes
that request through `LaunchRequest.ForAppId`, the descriptor resolver, and
`ApplicationFactoryRegistry.TryLaunch`.

The existing Phase 9 request model also has `ForAlias`, `ForDocument`,
`ForShellObject`, and `ForTypedShellAction`. Phase 31 exposes only stable-ID
launch. The internal Ring 3 transport operation is ID `1`; it is a bounded
transport selector, not a new launch backend. Alias launch, document open,
shell-object invocation, and typed actions remain outside the managed API in
this phase. The primary proof does not use compatibility translation or
fallback.

The authoritative stable-target bound is `LaunchRequest.MaxTextLength`,
1,024 UTF-16 code units; the existing Phase 9
`ApplicationShellOpenRequest.MaxTargetLength` is also 1,024. The SDK's
`GuideXosShell.MaxApplicationIdLength` publishes that same bound, and the
kernel independently checks against `LaunchRequest.MaxTextLength`. Empty or
overlong IDs are rejected, and strings are never truncated. The managed SDK
writes each `char` explicitly as UTF-16LE. The kernel copies the full bounded
request into kernel-owned storage and then decodes the target. No managed
string pointer is retained after the synchronous service call.

## Public SDK and internal wire

The public surface is:

```csharp
GuideXosResult transport = GuideXosShell.TryLaunchApplication(
    "gxos.builtin.calculator", out GuideXosLaunchResult launch);
```

`GuideXosLaunchResult` contains only `GuideXosLaunchResultCode` and
`Succeeded`. It exposes no target handle, App Model object, or diagnostic
pointer. Result values mirror the existing `ApplicationServiceResultCode`
values (`Success=0`, `InvalidContext=1`, `InvalidRequest=2`, `NotFound=3`,
through `BackendFailure=11`). Transport status is returned separately as a
`GuideXosResult`. Raw wire types are `internal` to `GuideXos.User`.

Both sides use sequential, packed records:

| Record | Contents | Size |
| --- | --- | ---: |
| Launch request | version, service ID, operation ID, total length, target length, response capacity, response-buffer address, reserved field, fixed 2,048-byte UTF-16LE target buffer | 2,084 bytes |
| Launch response | version, size, typed result code, reserved field | 16 bytes |

The target buffer is always inline and fixed-width. The request contains no
authority-bearing identity or object fields. The response contains no target
identity handle or kernel reference.

On every call the kernel derives the current `Ring3Process`, resolves that
process's `OwningApplicationInstance`, validates the live registry entry,
creates a fresh `ApplicationServiceContext`, validates Shell access, and
preserves the existing interaction lifecycle rule: the requester must be
`Running` or `Activated`. A stale or absent owner receives a typed
`InvalidContext` result. The caller cannot choose another requester.

For stable IDs the Shell service resolves the descriptor by ID and passes it
to the registered typed factory. The test target is
`gxos.builtin.calculator`, selected through `CalculatorApplicationFactory`.
The Calculator permits multiple instances, so each accepted request creates a
distinct `ApplicationInstance`; the normal factory creates a Calculator
window and the target reaches `Activated`. No constructor is called directly
by the Ring 3 dispatcher, and no legacy or compatibility fallback is used.

## Runtime proof

The Phase 31 diagnostic ran four successful requesters. Each launched the
Calculator, received `Success`, returned `31`, resumed after timer
preemption, and then exited. It then ran these bounded cases:

| Case | Managed result | Kernel/App Model evidence |
| --- | --- | --- |
| Four primary launches | `Main` returns `31` each | resolver success, typed factory, target instance, `Activated`, stale requester state rejected |
| Invalid stable ID | returns `41` only for typed `NotFound` | result code `3`; no factory, fallback, or target instance |
| ID one code unit over bound | returns `42` only for SDK `InvalidArgument` | rejected before service entry; zero backend requests; kernel also checks its own 1,024-unit bound |
| Launch then FailFast | requester exits with `-1` | launched target remains active under normal Shell semantics |
| Fresh replacement requester | returns `31` | new kernel-derived authority launches another distinct Calculator |
| Stale owner probe | returns `32` only for typed `InvalidContext` | stale ApplicationInstance cannot create Shell authority or enter the factory |

The proof observed five successful `31` returns (four primary requesters plus
the replacement), eight timer-resume passes across the runnable requesters,
six typed Calculator factory invocations, and six target instances. The
failfast-launched target persisted after requester termination. A direct stale
request through the old service context was rejected; the old process handle
no longer resolved and the old ApplicationInstance was absent from the live
registry.

The diagnostic terminated all six test targets through normal App Model
lifecycle cleanup. Target count returned to its initial value, active Shell
requests were zero, requester process/user-thread counts were zero, and
AppModel/process cleanup counters were balanced. Compatibility fallback and
legacy backend deltas were both zero.

## Artifact and regression evidence

All five NativeAOT modes were rebuilt and checked by
`Tools/Phase31/verify_phase31.py`. Each artifact is 706,560 bytes, has no PE
imports, no relocations, no TLS directory, no RWX section, and passes three
negative mutations (missing mode flag, descriptor hash mutation, and artifact
byte mutation). The success payload SHA-256 is
`C7F3D8E0195248084130280349F056AD878B5D3FFF38E67EDEF0D5673FB98EDD`.
Its descriptor uses the fixed-base managed image contract. The map contains
7,408 entries, 10 fewer than the Phase 30 control's 7,418. Relative to Phase
30, file size is 1,536 bytes smaller, GC-helper count is unchanged at 1,863,
exception-helper count is four lower (1,967 vs. 1,971), and TLS-helper count
is unchanged at 86. GC, TLS, and writable-static map-section counts are
unchanged (387, 38, and 386). The artifact has six sections, zero direct
kernel/Windows imports, and zero foreign link inputs. The only allowlisted PAL
helpers are `guidexos_pal_abi_version` and
`guidexos_pal_service_request`.

The image entry is `0x401000001430`. The map places generated
`Program.Main` at `0x401000065cf0` and `__managed__Main` at
`0x40100006b840`. The artifact still uses the same private GC, exception, and
TLS/statics runtime closure as Phase 30; it adds no filesystem, console,
networking, dynamic-loading, or Windows process API imports.

The retained Phase 25 bootstrap control is 845 bytes, SHA-256
`0DC31E472F4C3DA2581B1D8731CFEECCB9E64649A4F368036F2D38199136E618`.
Its descriptor is 92 bytes. The fresh Phase 25 diagnostic passed.

Fresh guest controls passed: Phase 30 (`30`, clipboard Set/Get), Phase 29
(`29`), Phase 28 (`28`), Phase 27 (`27`), Phase 26 (`42`), Phase 25, Phase
13, Phase 14 service IPC, and Phase 15. The combined App Model diagnostic
passed its instance, Phase 8 service, compatibility, lifecycle (15/15),
taskbar/grouping (16/16), Phase 9 Shell, Phase 10 storage, and Phase 11
clipboard self-tests.

Additional host-driven diagnostics:

* Production Continuous completed `TIMEOUT_SUCCESS` after 600 frames with
  valid graphics invariants, zero allocator corruption, and zero ThreadPool
  lock indicators.
* ContextMenu completed all 104 desktop menu opens with valid bounds/drawing,
  all 25 icon-size activations, 50 click-away dismissals, taskbar menu checks,
  and balanced right-button transitions.
* NativeInput completed `TIMEOUT_SUCCESS` with balanced 104 keyboard
  down/up pairs, balanced 54 mouse-button pairs, and zero dropped events. Its
  diagnostic reported `GUI key routed: False`; mouse routing and Start-menu
  opening passed.
* AppRuntime exercised factory launches, closes, service/lifecycle checks,
  and file routes, but the host QMP workload ended
  `INPUT_INJECTION_FAILED` while waiting for the guest's
  `APP_RUNTIME_FILES_DIR_OPEN=path=Scripts/` marker. This remains a harness
  caveat, not a Phase 31 launch failure.

The fresh Phase 30 proof uses the checkpoint payloads already committed at
`c9fe82f67bffa76907d56eeae984f0e63a8fbbc0`, the direct child of Phase 29
checkpoint `0c45632e3320c5403f54d78330c4915694cda6cf`. No duplicate Phase 30
commit was made; the existing Phase 30 commit subject is `...` and was left
unchanged. Phase 31 changes remain uncommitted on `main`; no push, branch,
stash, worktree, or detached checkout was created for this phase. The root
repository remained on `main`. Server and Legacy repositories were not
modified.

The NativeAOT build used the nested `out/rt` checkout at `9d5a6a9aa`. At
final inspection that checkout was detached and dirty (53 tracked paths plus
four untracked paths/directories); its status before Phase 31 was not captured,
so those toolchain edits cannot be attributed to this phase. No runtime source
file was intentionally edited as part of Phase 31.

## Phase 32 boundary

The next useful managed Shell addition is `OpenDocument` through the same
Phase 9 service, keeping its existing association and filesystem resolution
behind the App Model. Managed GUI, window, taskbar, arbitrary process, and
general command APIs remain out of scope.
