# P23R5 — Target Code-Manager Integration

**Outcome C — `P23R5_RUNTIME_INTEGRATION_BLOCKED`.** P23R5 adds and tests an isolated PE staging preflight, but the pinned target runtime does not include the genuine COFF code manager, and its current unwind, GC, EH, and registration interfaces do not provide safe bounded failure or transactional publication. The preflight, bounded evaluator, and hosted consumer evidence are not connected to the target runtime. `RhRegisterOSModule` remains unresolved in the GUIDEXOS target archive and Phase 23 remains blocked.

## Checkout and preservation

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`
- Branch: `BUILD_INFRA`
- Starting HEAD: `6a7b3f3bf4e04dd634e80b5476b3fff935e3d223`
- Ending HEAD: the local commit containing this report; its SHA is included in the execution summary because a commit cannot contain its own object ID.
- The BUILD_INFRA worktree was clean before this task. P23R5 changes are limited to this report, a bounded PE preflight header, and added tests for that staging foundation. No runtime-source overlay was applied to `out\rt`.
- `out\rt` remained detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. Its status snapshot was identical after verification: 65 entries (54 modified tracked files and 11 untracked entries), SHA-256 `7D48B6B71F2B7B53794738B6974A8D611E51F0780D757650D648B8239C0B35A1`. Earlier P23R4 evidence records 58 entries; the current cache has 65 at P23R5 start. All 65 current entries were preserved.
- `D:\dev\guideXOSUEFI_MODERN_INPUT` was not modified; its HEAD remained `3e1b18a92342d8776eb9fbe72fccd009468099d6`.
- No branch switch, merge, rebase, reset, cherry-pick, or push occurred.

## Pinned runtime contracts

The source inspection was read-only in `out\rt` at the pinned commit and its existing Phase 21–23/26 overlay. The bootstrapper declares the seven-argument C ABI in `src/coreclr/nativeaot/Bootstrap/main.cpp:99–102`:

```cpp
bool RhRegisterOSModule(
    void* pModule,
    void* pvManagedCodeStartRange, uint32_t cbManagedCodeRange,
    void* pvUnboxingStubsStartRange, uint32_t cbUnboxingStubsRange,
    void** pClasslibFunctions, uint32_t nClasslibFunctions);
```

`main.cpp:182–203` calls `RhInitialize`, then `RhRegisterOSModule`, aborts if registration returns false, and calls `InitializeModules` afterward. `StartupCodeHelpers.cs:30–35` separately calls `RhpRegisterOsModule` while initializing type managers. The latter is a one-argument OS-module-list insertion in `RuntimeInstance.cpp:285–297`; it does not construct or register a code manager, managed range, unboxing range, exception table, or class-library function table.

The Windows target appends `windows/CoffNativeCodeManager.cpp` to `FULL_RUNTIME_SOURCES` at `Runtime/CMakeLists.txt:127`. The GUIDEXOS branch beginning at line 130 selects the GUIDEXOS GC environment and does not append that source. The genuine Unix manager is also not selected for GUIDEXOS. Therefore the existing GUIDEXOS `Runtime.WorkstationGC.lib` cannot supply the required implementation.

`CoffNativeCodeManager`'s constructor at `Runtime/windows/CoffNativeCodeManager.cpp:186–195` stores raw module, managed-range, runtime-function-table, and class-library pointers and counts. It performs no PE identity, range-arithmetic, section, `.pdata`, unwind-data, GC-info, or EH-info validation. `FindMethodInfo` checks the managed-code interval, then reads the runtime-function table and adjacent metadata without a bounded image view (`:254–292`).

`RuntimeInstance` supports one code manager. `m_CodeManager` and its managed range are ordinary fields (`RuntimeInstance.h:42–46`). `RegisterCodeManager` only asserts that the slot is empty and stores the manager and range (`RuntimeInstance.cpp:209–216`); it returns no status, takes no lock, and uses no atomic publication. `GetCodeManagerForAddress` reads those fields directly (`:101–108`). The bootstrapper orders registration before module initialization, but the pinned source does not give `RegisterCodeManager` a concurrent-reader exclusion or publication-ordering contract. A single-thread-only guarantee was not established.

The upstream registration body allocates a manager with `nothrow`, calls `RegisterCodeManager`, then calls the fallible `RegisterUnboxingStubs` (`CoffNativeCodeManager.cpp:1179–1208`). The latter allocates a list node and publishes it with an interlocked compare-exchange (`RuntimeInstance.cpp:224–257`). If that allocation fails, the helper returns false after the manager pointer has been published; the `NewHolder` then releases the manager while `RuntimeInstance` retains its pointer. There is no unregistration or rollback API. On success, the manager is retained for process lifetime. The existing order therefore permits stale manager visibility on the failure path.

## Runtime unwind, GC, and EH consumers

The pinned consumer contracts prevent wiring the current bounded evaluator into the target manager without a larger runtime adaptation:

| Consumer | Pinned behavior | Failure gap |
|---|---|---|
| Method lookup | `FindMethodInfo` checks the registered managed interval and finds a `.pdata` entry. | It has no bounded PE/image view for its table or per-method data. |
| Outgoing-argument bound | `GetConservativeUpperBoundForOutgoingArgs` calls `RtlVirtualUnwind` on AMD64 (`CoffNativeCodeManager.cpp:517–606`). | The API has no validation status or image/stack bounds; the method returns an address, not an error. |
| Frame unwind | `UnwindStackFrame` calls `RtlVirtualUnwind` and commits its resulting `RSP`, `RIP`, and saved-register locations (`:651–838`). | `ICodeManager::UnwindStackFrame` has a bool result, and `StackFrameIterator::NextInternal` fail-fasts on false (`StackFrameIterator.cpp:1565`), but the AMD64 COFF implementation returns true after the legacy call. `IsUnwindable` also returns true unconditionally (`CoffNativeCodeManager.cpp:842–845`). |
| Return-address hijack | `GetReturnAddressHijackInfo` unwinds and derives a writable return-address location (`:861–929`). | Its bool result cannot distinguish an invalid unwind from “not hijackable”; the legacy call provides no status. |
| GC roots | `GcEnum.cpp:125–143` invokes `ICodeManager::EnumGcRefs`, whose interface returns `void` (`ICodeManager.h:258–263`). The COFF manager's decoder failure only reaches `assert(false)` (`CoffNativeCodeManager.cpp:434–514`). | A release build has no status path to stop the scan before later roots are treated as valid. The pinned decoder and COFF metadata reads are not passed bounded end pointers. |
| EH metadata | `EHEnumInit`/`EHEnumNext` return bool (`ICodeManager.h:281–283`); consumers forward those values in `EHHelpers.cpp:40–57`. The COFF implementation uses unbounded `VarInt::ReadUnsigned` and direct type-RVA reads (`CoffNativeCodeManager.cpp:1038–1122`). | False already means “no EH data” or “no more clauses”; malformed-record failure has no separate result and can be mistaken for normal exhaustion. |

Transition frames and stack walking are handled by `StackFrameIterator` and `Thread::GcScanRootsWorker` (`thread.cpp:393–509`). The scanned `Thread` stores its own low/high stack bounds (`thread.h:243–244`, initialized in `thread.cpp:287–289`), while the code-manager virtual calls receive `MethodInfo` and `REGDISPLAY`, not the walked thread's bounded stack span. GC can walk a thread other than the collector thread, so a current-thread stack query is not an equivalent bound. The current manager interface does not carry the information needed by the bounded evaluator for every consumer.

The bounded P23R3 adapter already reports explicit status and commits context/register locations only on success. Its tests demonstrate failure atomicity for malformed/truncated metadata, out-of-range and invalid stack spans, unsupported unwind forms, and invalid context/restoration cases. That adapter is a freestanding test interface; the pinned `CoffNativeCodeManager`, GC walker, EH walker, and target PAL do not call it. Mapping an error to a null exception-handler pointer would not solve the missing status and was not attempted.

## P23R5 staging foundation

| File | Change |
|---|---|
| `Tools/Phase23/target_pe_preflight.h` | Adds bounded mapped-PE staging validation; never constructs or publishes a manager. |
| `Tests/NativeAotUnwind/x64_unwind_tests.cpp` | Extends the existing unwind executable with eight staging/preflight cases. |
| `docs/UEFI_P23R5_TARGET_CODEMANAGER_INTEGRATION.md` | Records the pinned contracts, evidence, blocker, and next phase. |

`Tools/Phase23/target_pe_preflight.h` adds a bounded validation plan for a caller-supplied mapped image span. It checks MZ/PE32+ AMD64 headers, optional-header and section-table bounds, image size, executable managed/unboxing ranges and overlap, exception-directory bounds/alignment, sorted non-overlapping `RUNTIME_FUNCTION` ranges, and version-1 unwind-header/slot bounds and supported opcode shapes. Traversal is capped at 96 PE sections and 1,048,576 runtime-function entries. `PrepareRegistration` returns a staged description only; every success keeps `GcEhMetadataValidated`, `ManagerConstructionAllowed`, and `PublicationAllowed` false.

The helper does not establish the loader's mapped-image extent, a guideXOS-specific image identity, the class-library table's accessible length, NativeAOT GC/EH metadata bounds, stack bounds for the walked thread, target manager ownership, or runtime reader synchronization. It is not included in `Runtime.WorkstationGC.lib` and does not replace or construct `CoffNativeCodeManager`. Eight added cases in the existing unwind test executable exercise valid staging, invalid PE identity, executable-range rejection and overlap, exception-directory rejection, overlapping function entries, truncated/unsupported unwind data, invalid arguments, and no partial staged result on failure. This is a validation foundation; publication remains deliberately disabled.

## Target archive and source provenance

`dumpbin` and `lib /list` were run against the retained P23R4 target and host package archives. The target archive is:

`out\dotnet\phase23r4-gc-eh-fixture-reproduction\nuget-cache\runtime.guidexos-x64.microsoft.dotnet.ilcompiler\9.0.0\sdk\Runtime.WorkstationGC.lib`

- SHA-256: `E486351F470C4562C217A24938BC070EBD8AE112CF6D7EF97770CA6A2056E50F`
- Contains `RhpRegisterOsModule`.
- Does not contain `RhRegisterOSModule` or a `CoffNativeCodeManager.cpp.obj` member.

The retained host archive used by the consumer harness is:

`out\dotnet\phase23r4-gc-eh-fixture-reproduction\nuget-cache\runtime.win-x64.microsoft.dotnet.ilcompiler\9.0.0\sdk\Runtime.WorkstationGC.lib`

- SHA-256: `0E6A134AD4150CD604317A47860DAE82EB30AAE4D9CDB14144E06454E7BB1948`
- Contains the genuine host `CoffNativeCodeManager.cpp.obj`, its constructor and virtual methods, and `RhRegisterOSModule`.

The host archive is not target evidence. The earlier fixture map's same-named registration symbol came from the incompatible prebuilt `guidexos_link_shim.obj`; that symbol was not used as target-manager evidence. No target-compatible manager object or new runtime dependency closure was built for P23R5.

No runtime overlay or patch was applied. The pinned cache was left unchanged because adding only the COFF source would expose unchecked unwind/GC/EH reads and the known post-publication unboxing-allocation failure. A reproducible target patch must first change the internal consumer contract and registration protocol; there is no safe partial patch to apply to this cache.

## Hosted verification

The existing P23R3 suite was rerun from `Tests/NativeAotUnwind/run_x64_unwind_tests.ps1`:

- Bounded-only: **59 passed, 0 failed** (the retained 51 cases plus 8 PE preflight cases).
- Windows-reference: **59 passed, 0 failed**, including **20** calls/comparisons against host `RtlVirtualUnwind`.
- Failure cases include malformed and truncated unwind metadata, invalid image and stack ranges, arithmetic overflow, invalid context/restoration, misaligned or out-of-range stack slots, unsupported versions/opcodes/handlers/chains, malformed operation order, transactional no-mutation behavior, PE bounds and identity failures, overlapping executable ranges and function records, and disabled publication after staging.
- Hosted unwind executables were isolated under `C:\Users\guideX\AppData\Local\Temp\guideXOS_P23R3_unwind_677c415b0ec44234b4c531f4e642a0fc`; the runner used `g++ -std=c++17 -Wall -Wextra -Werror`.

The retained authentic NativeAOT fixture was rerun through `Tools/Phase23/run_gc_eh_consumer_tests.ps1` with an isolated output directory in `%TEMP%`. **The hosted consumer run passed.** It constructed and exercised the genuine host COFF manager against the preserved PE/GC/EH fixture:

- Authentic GC info reported 7 roots at `Outer+0x19`, 6 at `Outer+0x23`, and 1 at `Inner+0x10`; all six callback-provided writable root slots were updated and read back.
- The same register display walked `Inner` to `Outer` and enumerated both frames.
- One typed catch clause was decoded at EH-info RVA `0x94f26`; the associated handler and target type were bounded within the image. The manager found 470 funclets and associated one with `CatchFixture`.
- The separate R13 probe used a test-only unwind-record overlay while keeping compiler-generated GC info. The manager selected and allowed a write through the recovered writable stack-save location. The fixture did not contain a compiler-generated live R13 GC root.

The tested fixture is `out\dotnet\phase23r4-gc-eh-fixture-reproduction\publish\guideXOS.NativeAotGcEhFixture.exe` (SHA-256 `D9EDE8AFC32D73504D718A9A752C921FE8E2294B0627B1CF276E846A64878DCD`); its map SHA-256 is `F8DCFF0B33258F2637006BDC1F9A9245C6B87F7F40E43AA15304864F8D9B55DF`. The hosted consumer source SHA-256 is `D64A798B377B500D03B2B1CABE5325B0CAA64EB60350EC7E57178A0398FCAB3F`. The MSVC 14.51 harness and result JSON were written to `C:\Users\guideX\AppData\Local\Temp\guideXOS_P23R5_gc_eh_ab44fd1962034392ab6a56e98549b361`.

These consumer results exercise the prebuilt host archive, not a target runtime archive, actual runtime registration, managed execution, a compacting collection, or exception propagation. The authentic GC/EH regression passed; malformed metadata and failed unwind were not injected into the genuine COFF manager because its current paths are unbounded and can publish or consume invalid-looking state.

## Failure and lifecycle fault matrix

| Injection point | P23R5 result |
|---|---|
| Malformed/truncated unwind info, unsupported opcode/version, invalid image or stack range, invalid context restoration | Bounded evaluator returns explicit failure and preserves input context; covered by the 51-case suites. |
| Failed unwind followed by GC root enumeration | Not safely injectable through the pinned manager. Its AMD64 unwind path cannot report failure, and GC enumeration has no result channel. No claim is made that runtime roots are suppressed after failure. |
| Invalid PE identity and overlapping managed/unboxing ranges | The P23R5 host preflight rejects malformed PE headers, ranges outside executable sections, and overlapping ranges. This helper is not linked into the target archive. |
| Truncated GC/EH metadata | No target constructor/preflight path exists in the archive; GC/EH truncation was not tested through the genuine manager. |
| Manager allocation/construction failure | The upstream registration source uses `new (nothrow)`, but no target registration entry point exists and no target fault injection was added. |
| Unboxing-stub allocation failure after code-manager registration | Source inspection confirms a published-manager lifetime hazard. No runtime invocation was made because this can leave a dangling pointer. |
| Duplicate registration, reader visibility, ordering, lookup after publication, lifetime | No target manager is registered; runtime publication safety is unproven. |
| Malformed EH record during lookup/enumeration | Not injected into the genuine manager because its varint/type reads lack bounds and its bool result conflates error and exhaustion. |

Successful construction is demonstrated only for the hosted host manager in the authentic consumer harness. Construction of a target manager and safe target publication are **not** demonstrated. The fail-closed production decision for this phase is to keep `RhRegisterOSModule` unresolved and the Phase 23 production link gate failing.

## R13 and qualification boundaries

The existing R13 saved-register behavior remains passing in the bounded suite and the separately labeled host-manager probe. The current evidence does not establish a pinned-toolchain-generated live R13 GC root. Full compacting-GC behavior, actual managed execution, reverse-P/Invoke transition coverage, EH propagation, cleanup/unwind, and malformed runtime metadata handling remain unqualified.

## Phase 23 and downstream status

| Gate | Result |
|---|---|
| Genuine COFF manager in GUIDEXOS target archive | **No** |
| Target manager constructed | **No**; host manager construction passed |
| Target manager publication proven safe | **No** |
| Target unwind failure propagated safely | **No**; bounded adapter status is not connected |
| Authentic GC roots enumerated and six slots updated | **Yes, hosted host-manager fixture only** |
| EH metadata exercised | **Yes, one typed clause and funclet association; no propagation** |
| Genuine `RhRegisterOSModule` behavior in target | **No; unresolved** |
| Phase 23 proof | **Blocked**; preserved unresolved `RhRegisterOSModule` link failure remains the baseline. No new proof build was attempted because no safe target implementation was available. |
| Phase 29–35 records or identities generated | **No** |
| Production kernel built | **No** |
| QEMU / QAC / physical hardware | **No / No / No** |
| Push | **No** |

## Exact P23R6 objective

Implement a target-only, status-bearing NativeAOT code-manager boundary that carries validated PE/GC/EH bounds and the walked thread's stack bounds through every unwind consumer. Make unwind, GC enumeration, and EH decoding failures terminate the affected operation before caller context or root locations are consumed. Add a staged manager/unboxing registration protocol with rollback or one immutable publication point, and prove the startup concurrency contract. Exercise deterministic failures and authentic GC/EH consumers through the target-compatible manager, then rebuild the target archive and pass the supported Phase 23 proof before enabling production registration.
