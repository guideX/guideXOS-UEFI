# Phase 30 Managed Clipboard

Phase 30 reaches **Outcome A**: a separately compiled NativeAOT Ring 3
application used only the public `GuideXos.User` SDK to set and read the
existing Phase 11 session-global text clipboard. The managed `Main` compared
the copied text and returned `30`. No second clipboard backend was introduced.

The proof extends the managed SDK established in
[`docs/PHASE28_MANAGED_USER_SDK.md`](../docs/PHASE28_MANAGED_USER_SDK.md) and
reuses the Phase 29 service transport described in
[`Docs/PHASE29_MANAGED_NOTIFICATIONS.md`](PHASE29_MANAGED_NOTIFICATIONS.md).

## Authoritative Phase 11 contract

The live `ApplicationServices.cs` and
`CSharpApplicationClipboardService` implementation remain authoritative:

| Property | Contract |
| --- | --- |
| App Model service | `ApplicationServiceId.Clipboard`, ID `10` |
| Ring 3 wire operations | SetText `1`, GetText `2`, Clear `3` |
| Text bound | `65,536` UTF-16 code units (`string.Length`) |
| Encoding | Explicit UTF-16 little-endian code units; lengths, not terminators |
| Empty text | Valid present value (`HasValue=true`) |
| No value | Successful Get with `HasValue=false`, empty text/source |
| Source | Validated `ApplicationServiceContext.ApplicationId`, stored as metadata |
| Set generation | Every successful Set advances `UInt64` generation once |
| Clear generation | Advances only when a value was present; repeated clear is stable |
| Overflow | Mutating operation fails rather than wrapping `UInt64` |
| Process lifetime | Value and source survive application/process termination |
| App Model reset | Clears value and source and restores generation to zero |

The three Ring 3 operation selectors map directly to the Phase 11 service's
existing `SetText`, `GetText`, and `Clear` methods. They add transport
coverage, not a second service or new clipboard semantics.

Set copies both the bounded text and validated source AppId into the existing
service-owned state. The service retains no application instance, process
handle, service context, window, selection, callback, or user pointer. Source
AppId is attribution only and is not an authorization token.

## Public SDK surface

The public API is intentionally limited to copied text and typed results:

```csharp
GuideXosResult set = GuideXosClipboard.TrySetText(text);
GuideXosResult get = GuideXosClipboard.TryGetText(out GuideXosClipboardText value);
GuideXosResult clear = GuideXosClipboard.TryClear();
```

`GuideXosClipboardText` is a readonly value type with `HasValue`, `Text`,
`SourceApplicationId`, and `Generation`. Raw ABI records, process handles,
`ApplicationInstance`, and mutable `ApplicationServiceContext` objects are
internal and never appear in the public SDK.

`TrySetText` rejects null and values longer than `MaxTextLength` with
`GuideXosStatus.InvalidArgument`; it never truncates. Empty text is accepted.
`TryGetText` succeeds for both present and absent clipboard states and
validates response version, size, bounds, reserved fields, and absent-value
lengths before constructing the public snapshot.

## Wire and ownership boundary

All records are fixed-width, sequential, `Pack=1` layouts. The Get operation
uses the existing 32-byte service-request envelope.

| Record | Fields | Size |
| --- | --- | ---: |
| Set request | six `UInt32` header/length fields; fixed `65,536 * 2` byte text buffer | 131,096 bytes |
| Get request | existing service envelope with service ID, operation ID, and caller response buffer/capacity | 32 bytes |
| Get response | six `UInt32`, `UInt64` generation, fixed UTF-16 text and 96-code-unit source buffers | 131,296 bytes |
| Clear request | five `UInt32` fields | 20 bytes |

The raw request and response records live only in the SDK's internal ABI and
kernel Ring 3 ABI. The Set request contains no caller-supplied source identity.
The kernel resolves the current `Ring3Process`, derives its
`OwningApplicationInstance`, validates that instance, creates and validates a
fresh Clipboard `ApplicationServiceContext`, and calls the existing Phase 11
service.

Set copies the complete user request into temporary kernel-owned storage
before decoding. It checks the fixed request length, operation, reserved
field, and text bound before changing the backend. The backend then makes its
own text and AppId copies. Temporary kernel storage is freed before ABI return.
For Get, the kernel obtains the service snapshot, serializes copied text and
metadata into a temporary response, copies that response into the caller's
validated user buffer, and frees the temporary response. The clipboard's
kernel strings and pointers are never returned to user mode.

The largest fixed records exceed the general 64-KiB user-transfer validation
cap. Clipboard ranges are validated as contiguous chunks no larger than that
existing cap; the global transfer limit itself was not raised. The wire-layout
validator checks managed/kernel field names, order, bounds, and sizes before
staging.

Set uses a bounded stack-local wire record and copies UTF-16 code units from
the caller's managed string; it does not pin the string or retain its pointer.
Get receives a process-local fixed response and creates process-local managed
strings for `Text` and `SourceApplicationId`. Those two strings are normal
NativeAOT/GC allocations (the empty string uses the runtime's empty instance);
there is no cross-process object sharing. Get therefore has a 131,296-byte
fixed local response footprint in addition to its result strings. The proof
exercised the production NativeAOT string construction path.

## Runtime evidence

The fresh `Ring3Phase30` run completed with `RING3_PHASE30_COMPLETE=1`.
On the first managed lifetime:

1. `TrySetText("Phase 30 managed clipboard")` copied a 26-code-unit value;
2. the kernel derived `gxos.builtin.taskmanager` (24 code units) as source;
3. the existing backend accepted generation `1`;
4. Get copied the value, generation, and source metadata into the user response;
5. managed code constructed its local string, compared it with the original,
   and `Main` returned hexadecimal `0x1E` (`30`).

Four successful main lifetimes returned `30, 30, 30, 30`. The bounded
cross-lifetime checks also proved:

* A Task Manager application lifetime wrote `Phase 30 cross-process
  clipboard`; after its owner was detached, a fresh Calculator application
  lifetime read the same value at generation `6` with Task Manager's source
  AppId. The new process used its newly derived service context; it did not
  receive the writer's authority.
* A FailFast process wrote the same value and terminated with the expected
  FailFast result. The replacement lifetime read the persisted value and
  metadata. Process cleanup invalidated the old handles/context.
* Calculator overwrote the value with `Phase 30 overwrite value 2` at
  generation `7` and became the source. A 65,537-code-unit SDK Set returned
  `InvalidArgument` and left the generation-8 baseline value unchanged.
* The internal malformed-length proof sent a full fixed Set record whose
  `TextLength` claimed 65,537 code units. The kernel rejected it as
  `InvalidArgument`; the prior value and generation `6` remained unchanged.
* Setting the empty string produced a present empty value at generation `9`.
  Clear then advanced through generation `11` and produced an absent value
  with empty text/source. The final App Model reset cleared all state and
  returned generation to `0`.
* A captured stale service context could not mutate the session clipboard;
  the stale owner handle and service context were rejected. Managed image,
  bootstrap, process, and user-thread cleanup diagnostics balanced.

The final backend diagnostic was intentionally taken after reset:
`HasValue=false`, text/source lengths `0`, generation `0`. No clipboard
authority or process/service state remained outstanding.

## Artifact and dependency gate

The successful managed artifact is 708,096 bytes, SHA-256
`10857F774ADA6C9143D0B281FF4CCE6A5443954E417626AE77D48B0FD846AB4E`.
It is fixed-base AMD64 PE32+ with entry RVA `0x1430`; the map contains
`Program.Main` at VA `0x00004010000660D0`. Its Phase 26 native bootstrap is
486 bytes, SHA-256
`EBE9844E84F547008523156593693014869EF67D4D74CE8EDB68E129A87D83A2`.

Inspection found zero PE/Windows imports, zero direct kernel imports, zero
base relocations, no PE TLS directory, six sections, and zero RWX sections.
The PAL dependency set is the existing ABI-version and service-request pair
(two helpers). Relative to the Phase 29 artifact, the map grows by 11 entries:

| Map classification | Phase 29 | Phase 30 | Delta |
| --- | ---: | ---: | ---: |
| Allocation helpers | 282 | 282 | 0 |
| GC helpers | 1,863 | 1,863 | 0 |
| Exception helpers | 1,966 | 1,971 | +5 |
| Compiler-generated helpers | 1,038 | 1,037 | -1 |
| Generated native symbols | 1,868 | 1,874 | +6 |
| Internal NativeAOT runtime code | 108 | 109 | +1 |
| Startup/module helpers | 196 | 196 | 0 |
| TLS helpers | 86 | 86 | 0 |

Map symbols increase from 7,407 to 7,418. GC sections (387), TLS map entries
(38), and writable-static entries (386) are unchanged. The small helper delta
does not enable filesystem, console, registry, dynamic loading, Windows
interop, or broader globalization APIs. Exception-helper classifications are
map-symbol categories, not imported dependencies.

The host loader probe validated four map/teardown lifetimes, 24 mapped and
reclaimed segments, BSS zero-fill, permissions, and entrypoint; it rejected 11
negative descriptors and reported zero outstanding mappings. The real QEMU
loader additionally accepted the staged Phase 30 descriptors and payloads.
The wire validator reports Set/Get/Clear operation IDs `1/2/3`, service ID
`10`, and record sizes `32/131096/131296/20`.

## Reproduction and controls

```powershell
.\Tools\Phase30\build_phase30_managed_clipboard.ps1
.\Tools\Phase30\stage_phase30_image.ps1
.\build.ps1 -SkipBootloader -UefiDiagnosticMode Ring3Phase30
.\run_uefi_validation.ps1 -Ring3Phase30 -SkipBuild -TimeoutSeconds 300
```

Fresh controls after the Phase 30 kernel change:

* Phase 29 Notifications: four `29` lifetimes, backend acceptance, and
  `RING3_PHASE29_COMPLETE=1`.
* Phase 28 SDK: four `28` lifetimes and `RING3_PHASE28_COMPLETE=1`.
* Phase 27 System Information: four `27` lifetimes and
  `RING3_PHASE27_COMPLETE=1`.
* Phase 26 managed execution: four `42` lifetimes and
  `RING3_PHASE26_COMPLETE=1`.
* Phase 25 native bootstrap: `RING3_PHASE25_COMPLETE=1`.
* Phase 14 scheduler-managed service IPC: `RING3_PROOF_COMPLETE=1` and
  service requests copied through the existing ABI. Phase 15 completed with
  `RING3_PHASE15_COMPLETE=1`.
* Phase 13 direct boundary control reached
  `RING3_PROOF_RETURNED_TO_ENTRYPOINT=1`, invalid-pointer rejection, contained
  fault, reclamation, and stale-handle rejection. Its shared diagnostic
  `RING3_PROOF_COMPLETE` summary remained `0`; the direct-selector terminal
  marker is the accepted completion gate, as documented in
  `docs/PHASE12_RING3_PROCESS_BOUNDARY_DESIGN.md`.
* Fresh AppModel validation passed Compatibility, Lifecycle, taskbar/grouping,
  Phase 8 services, Phase 9, Phase 10, and Phase 11. Phase 11 clipboard
  contract, self-test, generation, lifecycle, and reset markers all passed.

Host/QMP workloads remain classified separately from managed clipboard:

* AppRuntime launched its guest application cohort (23 factories, zero
  factory failures or guest runtime faults), but its host validator ended in
  `INPUT_INJECTION_FAILED` because the expected QMP interaction marker was
  absent. This is not a clipboard failure.
* NativeInput completed in `TIMEOUT_SUCCESS`; fresh keyboard and mouse dropped
  packet counts were both zero.
* ContextMenu completed its validation, with balanced right-button input and
  healthy graphics.
* Production Continuous reached `TIMEOUT_SUCCESS` at frame 600 with valid
  graphics. The earlier QEMU-exit-after-60-frames caveat did not reproduce.

Phase 29 was already checkpointed at the preflight HEAD on `main`; Phase 30
changes are intentionally left uncommitted. No push, branch, worktree, stash,
or detached checkout was created. Server and Legacy repositories were not
modified. Phase 31 remains managed launch through the existing Phase 9 Shell
service; GUI, widgets, keyboard shortcuts, and shell launch are not part of
this phase.
