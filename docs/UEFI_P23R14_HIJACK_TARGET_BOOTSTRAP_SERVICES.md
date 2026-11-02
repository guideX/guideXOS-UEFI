# P23R14 — NativeAOT Hijack-Target Contract and Bootstrap Services

**Outcome C — `P23R14_HIJACK_ARCHITECTURE_BLOCKED`.** The pinned NativeAOT runtime requires a real callback registration, stable interrupted context, safe thread suspension, and a return-address target compatible with the active shadow-stack state. guideXOS does not currently expose that PAL capability or a verified CET ownership contract. P23R14 adds an explicit fail-closed target provider and tests it. The fresh archive still has 45 unresolved externals, so no standard bootstrap executable was produced and no NativeAOT `Thread::Construct` or ThreadStore attachment ran.

## Repository state and provenance

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`; branch: `BUILD_INFRA`.
- Starting HEAD: `6e99a38ce0457257874bcef6e57d465fe79e94c6`. The worktree was clean and the branch was equal to `origin/BUILD_INFRA` (0 ahead, 0 behind).
- Ending HEAD: the local P23R14 commit on `BUILD_INFRA`. Its exact SHA is returned in the final execution summary because a commit cannot contain its own object ID. The branch remains local and is not pushed.
- Pinned runtime revision: `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`.
- `out\rt` retained all 65 starting porcelain entries; ordered-status SHA-256: `41434BDE07D1EAA677C6B8E626CE63414012420506A160224DE2C18F144E813A`.
- `D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING` retained all 67 starting entries; ordered-status SHA-256: `2A122C605335703B8999F3FC6A7478888C73C5DAD200655929CAD8AA881CF25C`.
- All source changes and builds were isolated in the BUILD_INFRA checkout and `out\dotnet\phase23r14-hijack-target-contract-final-retry`. The runtime staging checkout and pinned `out\rt` tree were not modified. No other repository was changed.
- Prior evidence reviewed: `UEFI_P23R10_EXECUTION_CONTEXT_BRIDGE.md`, `UEFI_P23R12_BOOTSTRAP_THREAD_EXECUTION.md`, `UEFI_P23R13_BOOTSTRAP_LINK_PARTITIONING.md`, the P23R13 source-partition overlay, and its link-probe scripts/reports.
- P23R14 starts from the existing P23R13 source-partition copy. It does not split or rewrite any additional NativeAOT runtime function. The only runtime overlay is `Tools\Phase23\runtime-overlay\p23r14_guidexos_hijack_unsupported.patch` (SHA-256 `D2FD4AD0C77FCB4E8EB21B5A5285CD9FC662E92E15CF24E241BA92F0F9D6A49E`). The source-partition verifier passed: 29 `Thread` and 13 `ThreadStore` bodies are unchanged, GUIDEXOS TLS has one bootstrap-object definition, and the diagnostic map identifies five expected source symbols. This is source and map provenance only, not execution evidence.
- The inherited P23R13 partition patch SHA-256 is `8ED0E2762C77936E7C851BE9F6515F455D237EE63FBDFD10620EFCD29559A5B0`; the partitioned `thread.bootstrap.cpp` source SHA-256 is `521D6425DD604AE4480297E084DAD5818DD35C9DA9EFC07B45ADFA52523FE5C9`.

## Exact contract and call graph

The pinned `PalRedhawk.h` contract is:

```cpp
typedef void (*PalHijackCallback)(_In_ NATIVE_CONTEXT* pThreadContext, _In_opt_ void* pThreadToHijack);
REDHAWK_PALIMPORT void REDHAWK_PALAPI PalHijack(HANDLE hThread, _In_opt_ void* pThreadToHijack);
REDHAWK_PALIMPORT UInt32_BOOL REDHAWK_PALAPI PalRegisterHijackCallback(_In_ PalHijackCallback callback);
REDHAWK_PALIMPORT HijackFunc* REDHAWK_PALAPI PalGetHijackTarget(_In_ HijackFunc* defaultHijackTarget);
```

These declarations are under `FEATURE_HIJACK`; the PAL import/export and calling-convention macros in the header remain part of the platform ABI.

`CommonTypes.h` defines `HijackFunc` as `void HijackFunc()`: the target is a no-argument, no-return function. The runtime writes its address into a selected stack return-address slot; it does not call the target as an ordinary function. When the target returns, execution must return to the original caller. Therefore the pointer must be a valid executable routine with the expected return-address and stack semantics. Returning an arbitrary address or reporting success without a safe context and write path would violate the contract.

```mermaid
flowchart TD
  subgraph Init[ThreadStore initialization]
    RI[RuntimeInstance initialization] --> Create[ThreadStore::Create]
    Create --> Register[PalRegisterHijackCallback(Thread::HijackCallback)]
    Register -->|false| Reject[Create returns null; store is unusable]
    Register -->|true| Store[ThreadStore becomes usable]
    Store --> Attach[ThreadStore::AttachCurrentThread]
    Attach --> TLS[Get current Thread from runtime TLS]
    TLS --> Construct[PalAttachThread then Thread::Construct]
    Construct --> Bounds[Get OS thread id and current stack bounds]
    Bounds --> Insert[Insert into ThreadStore]
  end
  subgraph Later[Later GC suspension or target query]
    Suspend[ThreadStore::SuspendAllThreads] --> Hijack[Thread::Hijack]
    Hijack --> PAL[PalHijack suspends/activates selected OS thread]
    PAL --> Callback[Thread::HijackCallback with saved NATIVE_CONTEXT]
    Callback --> Select[PalGetHijackTarget(default RhpGcProbeHijack)]
    Select --> Write[StackFrameIterator finds safe return slot]
    Write --> Resume[Return through hijack target; restore original return]
    Query[Thread::IsHijackTarget] --> Select
  end
```

`ThreadStore::Create` allocates a store and, with `FEATURE_HIJACK`, requires callback registration before it can return a usable store. It does not invoke the callback. The normal attachment path is independent of target selection: it obtains the TLS `Thread`, calls `PalAttachThread`, enters `Thread::Construct`, records the native thread ID, attempts a duplicate thread handle, consumes `PalGetMaximumStackBounds`, then inserts the attached thread. The target query is used later from `Thread::HijackCallback` when suspension needs a return-address target and from `Thread::IsHijackTarget`; it is not called by `Thread::Construct` or attachment. The callback is not invoked during bootstrap attachment.

The current runtime CMake enables `FEATURE_HIJACK` on every non-WASM target. `ThreadStore::SuspendAllThreads` relies on hijacking when managed threads have not reached a transition frame. Disabling the feature would not be a supported way to retain the current GC suspension contract.

## Windows, Unix, guideXOS, and CET

The Windows PAL in `windows\PalRedhawkMinWin.cpp` stores the callback at registration. On the first `PalHijack`, it resolves `QueueUserAPC2`. When shadow stacks are active, it expects the special user APC with context data and resolves `RtlGetReturnAddressHijackTarget`; the pinned source asserts that Windows supplies a non-null return target. The APC callback receives the target thread's context and invokes the registered NativeAOT callback. If the special APC path is unavailable or rejected, the Windows implementation uses `SuspendThread`, captures control/integer registers with `GetThreadContext`, calls the callback, and resumes the thread. The host return target is initialized lazily during hijack setup; the runtime asks for the selected target in its later callback/target-query path. These facilities include Windows-specific thread state and return-address handling.

The pinned Unix PAL is a supported non-Windows implementation: it registers a signal handler, signals the selected pthread, and returns the supplied default target from `PalGetHijackTarget`. That provider relies on Unix pthread and signal context semantics; it is not a generic fallback for UEFI.

guideXOS has IRQ-driven context switching and scheduler-owned interrupt frames. Its scheduler selects kernel tasks and stacks, but the inspected code does not provide NativeAOT thread identity/TLS association for each scheduler task, a suspend-and-acknowledge operation for an arbitrary selected runtime thread, a stable saved `NATIVE_CONTEXT`, or a pinned target stack through a GC walk. The timer scheduler's context switch does not itself supply those PAL contracts. The inspected UEFI kernel sources do not configure or expose CR4.CET, `IA32_U_CET`, user shadow-stack ownership, or a safe shadow-stack-aware return-address write. The page-fault handler decodes the shadow-stack error-code bit for diagnostics; that is not CET initialization or ownership support. UEFI CET state must therefore remain **unknown** to the PAL.

The P23R14 Windows-host probe queried `ProcessUserShadowStackPolicy` (policy ordinal 15) and the processor's CPUID CET shadow-stack bit before reading SSP state. On this host the policy was disabled, CPUID reported CET_SS unsupported, and SSP was inactive. `QueueUserAPC2` and `RtlGetReturnAddressHijackTarget` exports were present. This validates only the current Windows host: the CET return-target path remains unverified. No mitigation policy was changed. Microsoft documents the process policy and the special APC context flags; the processor feature bit is specified in Intel's SDM. ([Windows shadow-stack policy](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-process_mitigation_user_shadow_stack_policy), [Windows APC flags](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/ne-processthreadsapi-queue_user_apc_flags), [Intel SDM](https://cdrdv2-public.intel.com/843836/325384-sdm-vol-3abcd-dec-24.pdf))

## Implemented target boundary

The isolated GUIDEXOS runtime overlay adds two sources to the target WorkstationGC CMake list:

- `PalRedhawkHijackRegistrationUnsupported.cpp` implements the exact registration ABI and always returns `UInt32_FALSE`. Thus `ThreadStore::Create` fails closed and cannot advertise a usable store.
- `PalRedhawkHijackUnsupported.cpp` implements `PalGetHijackTarget` as an explicit null unsupported result, calls the kernel fail-fast service if `PalHijack` is reached, and fail-fasts for `PalAreShadowStacksEnabled` because the target state is unknown. It does not report CET disabled, fabricate an executable target, modify return addresses, or claim successful suspension.

The null target is not a supported nullable interpretation of the runtime API. It is safe only behind the always-rejecting registration provider. If an invalid path bypasses registration, `PalHijack` fails fast. The actual runtime target still cannot initialize successfully on guideXOS until a real PAL capability exists. No Windows PAL adapter was added to the UEFI target, and no Windows NativeAOT PAL adapter was implemented in this phase. The separate Windows-host executable tests operating-system behavior without claiming to invoke `Thread::HijackCallback`.

## Hosted tests and provenance

| Qualification | Result | Evidence boundary |
|---|---:|---|
| Target hijack-provider behavior | 12 assertions, 0 failures | Fresh GUIDEXOS provider objects; valid/null/invalid defaults, rejected callback registration, no callback invocation, unchanged return-address sentinel, fail-fast fault injection. |
| Windows host policy/target/APC behavior | 15 assertions, 0 failures | Real `QueueUserAPC2` special APC with context on an OS-created worker; callback ran on the selected thread, received executable RIP and preserved parameter. Host CET return-target branch was not entered. |
| Descriptor/PAL | 30 passed, 0 failed | Fresh production stack-bounds object; hosted map/page checks are simulated. |
| Unsupported stack bounds | 4 passed, 0 failed | Fail-closed output and false-result checks. |
| Hosted worker/fiber context | 20 passed, 0 failed | Windows worker/fiber ownership only; no NativeAOT ThreadStore execution. |
| Bounded x64 unwind | 69 passed, 0 failed | Hosted bounded evaluator. |
| Windows-reference x64 unwind | 69 passed, 0 failed; 20 comparisons | Host `RtlVirtualUnwind` reference only. |
| Source-partition provenance | Pass | 29 unchanged `Thread` bodies, 13 unchanged `ThreadStore` bodies, five symbol/map provenance checks. |

The target-provider executable SHA-256 is `56C4C399FE22D69F84AEE93ED4A9A9558C53E1E339D36D4C716D7437562BFD66`; its source is `Tests\NativeAotUnwind\bootstrap_hijack_target_contract_tests.cpp`. The Windows-host executable SHA-256 is `931B5611BB78CDA062C247ECF88027B83007C21097895C727A52B251492B4EAD`; its source is `Tests\NativeAotUnwind\bootstrap_hijack_windows_host_tests.cpp`. These are test executables, not NativeAOT bootstrap images. No invalid or mismatched NativeAOT `NATIVE_CONTEXT` callback was injected: the target rejects registration, so that callback cannot safely run. The Windows test verifies context ownership for the OS-created target thread, but does not emulate a mismatched NativeAOT context or write a return address.

The fresh WorkstationGC archive SHA-256 is `CD4BCFD7AB4EAB1E7F1EBEFA622F5DFCAA5EF76C91A83C4315A4CDCBB8012ECE`. Key object hashes:

| Runtime object | SHA-256 |
|---|---|
| `RuntimeInstance.cpp.obj` | `872DCA95FE1A32F9243406E7BAC843EFBF3CDEAE818CAEE48A970B8F31439B2C` |
| `thread.cpp.obj` | `D91B39D44485A7EAA1D9830B1D5A392D7B059FE9F8447BAD9ADF5FDF7F9C3D7B` |
| `thread.bootstrap.cpp.obj` | `7A5756BACEA512704DB1005EA7B2EF6FCB84D4939827DDF4E6A90CA796223F5B` |
| `threadstore.bootstrap.cpp.obj` | `6B14E326F571FF765E8527805EB391A86CF61E2062AE62D15532F3B2FF3908CB` |
| `PalRedhawkHijackUnsupported.cpp.obj` | `0FEA23AF026BD8C45820145F19598A109C21443D7861A97EF853C9DAA3FD0D7D` |
| `PalRedhawkHijackRegistrationUnsupported.cpp.obj` | `8377B31431C87B3AE11E5E788B891D13022F28C4998E163560AE778467F5736B` |

The diagnostic `/FORCE:UNRESOLVED` image (`DIAGNOSTIC_NEVER_RUN.exe`, SHA-256 `EFDA6F65F8C42FD3DC74E9E72F777FAC10F16436A30473AC627644816E266E42`) exists only to emit map evidence and was not executed.

## Link result and all remaining external dependencies

The reproduced P23R13 standard-link baseline used archive SHA-256 `458069AF2E4EBE778C4830DDEE0683B850C33CEDC349106FE7C1B4C8BC28E255` and ended with `LNK1120`, 48 unresolved externals. The P23R14 fresh archive link ended with `LNK1120`, 45 unresolved externals. The three removed externals are `PalGetHijackTarget`, `PalHijack`, and `PalAreShadowStacksEnabled`. `PalRegisterHijackCallback` is supplied by the existing hosted link-probe PAL and was not one of the starting 48; the target archive also now contains the fail-closed provider. No ordinary `bootstrap_threadstore_link_probe.exe` was created. The forced map represented 31 runtime members; the normal-link error log identifies 22 members that emitted unresolved references. These are diagnostic membership indicators, not execution proof.

The following groups exhaust the 45 remaining names. For each group, the owner area and reachability classification apply to every listed external.

| Owning source or library area | Remaining externals | Reachability and provider status |
|---|---|---|
| Runtime thread, waits, diagnostics, and platform PAL imports (`thread.cpp`, synchronization/stress-log users, `PalRedhawkFunctions.h`, Windows/Unix PALs) | `PalCompatibleWaitAny`, `PalPrintFatalError`, `PalQueryPerformanceCounter`, `PalQueryPerformanceFrequency`, `PalSleep`, `PalSwitchToThread` | Real Windows/Unix services exist in their respective PALs. They are outside `Thread::Construct` and attachment; they serve later waits, suspension, timing, and diagnostics. guideXOS equivalents are not established by this phase. |
| PAL configuration, process, time, and runtime heap imports (`PalRedhawkFunctions.h`, configuration/allocator users) | `PalCopyTCharAsChar`, `PalGetProcessCpuCount`, `PalGetTickCount64`, `PalVirtualAlloc`, `PalVirtualFree` | These are real platform services in hosted PALs. The allocation calls belong to general runtime heap-block paths, not the `new (nothrow)` creation of RuntimeInstance/ThreadStore. No placeholder providers were added. |
| Module inspection and context/unwind helpers (runtime module, stack, and architecture support) | `PalGetModuleBounds`, `PalGetModuleFileName`, `PalGetModuleHandleFromPointer`, `PopulateControlSegmentRegisters` | Required by broader module inspection, unwind, or exception paths, not the initial attach sequence. Correct host implementations live in PAL/architecture code; guideXOS module and register-state providers remain outside this phase. |
| Exception, dispatch, and stack-trace runtime (`EHHelpers.cpp`, exception helpers/assembly, dispatch and stack-walk code) | `RhExceptionHandling_FailedAllocation`, `RhRethrow`, `RhThrowEx`, `RhThrowHwEx`, `RhpCalculateStackTraceWorker`, `RhpCidResolve`, `RhpFailFastForPInvokeExceptionCoop`, `RhpFailFastForPInvokeExceptionPreemp` | These are genuine runtime consumers, not OS imports to stub. They are not invoked by the narrow construction/attachment sequence and remain a separate EH/dispatch gate. |
| Runtime startup, GC event setup, CPU selection, and generated AOT inputs (`startup.cpp`, RuntimeInstance/GC startup, generated application configuration) | `PalInit`, `InitializeGCEventLock`, `InitSupportedInstructionSet`, `IsSupportedInstructionSet`, `g_requiredCpuFeatures`, `g_compilerEmbeddedKnobsBlob`, `g_compilerEmbeddedSettingsBlob` | `PalInit` and GC event initialization belong to complete runtime startup; CPU metadata and compiler blobs are application/generated inputs. These are not supplied by an arbitrary platform stub and were not needed to prove an attach because no executable linked. |
| GC selector/algorithms and guideXOS GC PAL (`gccommon.cpp`, `gcwks.cpp`, vector-sort sources, `gcenv.guidexos.cpp`, `GuidexosGcPal.h`) | `InitializeGCSelector`, `PalStartBackgroundGCThread`, `do_vxsort_avx2`, `do_vxsort_avx512`, `guidexos_pal_fail_fast`, `guidexos_pal_monotonic_frequency`, `guidexos_pal_monotonic_ticks`, `guidexos_pal_thread_id`, `guidexos_pal_vm_commit`, `guidexos_pal_vm_release`, `guidexos_pal_vm_reserve` | These belong to GC setup/algorithms and required target VM, time, identity, and fail-fast services. Real implementations must match guideXOS ownership and scheduling rules; none was invented to close this link. `guidexos_pal_fail_fast` is now called by the unsupported provider but remains an external target service. |
| Runtime event and finalization (`CLREventStatic`, GC finalization startup and generated managed entry point) | `PalCreateEventW`, `PalCreateLowMemoryResourceNotification`, `PalStartFinalizerThread`, `ProcessFinalizers` | Events and low-memory notifications are real OS/PAL services; finalizer startup needs a separate native thread and `ProcessFinalizers` is a generated managed method. They are not initial-thread attachment services and remain gated until GC/EH and managed module startup are valid. |

No remaining external was resolved by adding a success stub. The three-symbol reduction does not make the link usable: several unresolved groups represent later GC, exception, module, and finalizer consumers retained in the archive closure.

## Runtime execution, safety gates, and phase status

- `RuntimeInstance::Initialize`: **not executed**.
- `ThreadStore::Create`: **not executed**; the real target provider would reject callback registration and return failure.
- `ThreadStore::AttachCurrentThread`: **not executed**.
- Genuine `Thread::Construct`: **not executed**.
- Current-thread lookup and ThreadStore membership: **not observed**.
- Production stack bounds consumed by a NativeAOT `Thread`: **not observed**. The separate descriptor/PAL tests are not NativeAOT attachment.
- Failure cleanup in real ThreadStore initialization: **not executed**; only the provider's registration and fail-fast boundary was injected and tested.
- `RhInitialize`, GC/compaction/root relocation, exception propagation, finalization, `RhRegisterOSModule`, COFF-manager publication, and complete module registration remain unverified. No fake `RhRegisterOSModule` or incomplete manager was published.
- Phase 23 remains blocked. No Phase 29–35 records, managed identities, production kernel, or diagnostic image was generated.
- Hosted builds and tests only. No QEMU, QAC admission, or physical hardware was used. No push occurred.

## Proposed P23R15 objective

Implement and review one genuine guideXOS NativeAOT execution-context PAL contract: bind stable scheduler identity to the runtime thread, pin the selected stack for a walk, capture and resume the target `NATIVE_CONTEXT`, and provide a CET/shadow-stack-correct return-address target and restoration path. First prove whether the pinned runtime offers a supported GC mode that avoids hijacking; only use it if its source contract is explicit and verified. Keep registration fail-closed until callback delivery, return-address restoration, CET-state handling, and failure cleanup pass hosted behavior tests. Then link and execute the unmodified `ThreadStore::Create`, `AttachCurrentThread`, `Thread::Construct`, and current-thread lookup path. Keep GC/EH, finalization, and module publication gated until their independent requirements pass.
