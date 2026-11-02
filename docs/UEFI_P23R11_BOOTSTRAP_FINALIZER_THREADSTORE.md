# P23R11 — Bootstrap ThreadStore and Finalizer Execution Context

**Outcome C — `P23R11_RUNTIME_STARTUP_BLOCKED`.** The isolated WorkstationGC archive rebuilt, the existing regression suites passed, and a hosted Windows worker-fiber context passed focused ownership checks. The genuine NativeAOT bootstrap ThreadStore harness compiled but did not link: the fresh archive still has 48 unresolved externals. `Thread::Construct`, bootstrap ThreadStore attachment, and the NativeAOT finalizer callback therefore did not execute.

## Checkout and preservation

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`; branch: `BUILD_INFRA`.
- Starting HEAD: `1c34b841d8472fcdec34d8f2fa1d6ad9175d4d9e`.
- The branch started clean, 3 commits ahead and 0 behind `origin/BUILD_INFRA`. No branch switch, merge, rebase, reset, cherry-pick, or push occurred.
- The ending HEAD is the local commit containing this report and the listed implementation files. Its exact SHA is reported in the final execution summary because a Git commit cannot contain its own object ID.
- The protected runtime checkout `out\rt` remained pinned at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. Its 65 pre-existing status entries match the P23R9 preservation snapshot recorded in P23R10. No file under `out\rt` was written.
- Runtime sources were read from `D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING` at the pinned revision. Build and test outputs were isolated below `out\dotnet\phase23r11-*`; no staged runtime source was edited.
- `MODERN_INPUT`, `APP_MODEL`, mainline, release artifacts, and unrelated checkouts were not modified.

## Startup call graph and minimum boundary

The pinned runtime revision is `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`.

The pinned NativeAOT startup sequence is:

1. `RhInitialize` calls `PalInit`, then `InitDLL`.
2. `InitDLL` initializes interface dispatch when enabled, initializes the GC event lock and restricted callouts, then calls `RuntimeInstance::Initialize`.
3. `RuntimeInstance::Initialize` creates a `RuntimeInstance`, calls `ThreadStore::Create`, and publishes the instance only after ThreadStore creation succeeds. With `FEATURE_HIJACK`, `ThreadStore::Create` also requires `PalRegisterHijackCallback`. `InitDLL` then calls `InitializeGC`.
4. `InitializeGC` initializes the GC heap and calls `RhInitializeFinalization`.
5. `RhInitializeFinalization` creates finalizer events and calls `PalStartFinalizerThread(FinalizerStart, ...)`.
6. `FinalizerStart` attaches that native thread through `ThreadStore::AttachCurrentThread`, sets finalizer thread state, waits on the finalization event, and eventually calls generated managed `ProcessFinalizers`.
7. After `RhInitialize` returns, the NativeAOT bootstrapper gets the module handle, calls `RhRegisterOSModule`, then `InitializeModules` to create type managers and calls `__managed__Main`.

For an unattached current native thread, the genuine `ThreadStore::AttachCurrentThread` path calls `PalAttachThread`, then `Thread::Construct`, then inserts the initialized object under the ThreadStore lock. `Thread::Construct` records the OS thread ID, obtains PAL process/thread handles, attempts handle duplication (which may fail), and calls `PalGetMaximumStackBounds`; it fail-fasts if stack bounds are unavailable. It does not require a scheduler task or a general managed scheduler. `RuntimeInstance::Initialize` creates the ThreadStore but does not itself attach the current thread; the attach path is separate. The finalizer callback's attach call is explicit in pinned source. The initial application-thread attach was not behaviorally traced in this task.

The kernel path does not follow the supported sequence. `KMainWrapper` registers the P23R8 bootstrap descriptor and jumps to `KMain`; `Kernel/Misc/EntryPoint.cs` calls `StartupCodeHelpers.InitializeModules`. That helper uses `RhpRegisterOsModule` and type-manager creation. It does not call `RhInitialize` or the seven-argument `RhRegisterOSModule`. The pinned target archive has `RhpRegisterOsModule`, but it has no compatible `CoffNativeCodeManager.cpp` or `RhRegisterOSModule`. These symbols have different contracts; no alias or fake registration was added.

Source inspection shows a narrow pre-GC boundary is possible in principle: `RuntimeInstance::Initialize`/`ThreadStore::Create` and one current-thread attachment do not themselves call `InitializeGC` or register a managed module. That boundary was the target of the focused hosted probe. The probe could not link, so it does not establish that the boundary executes successfully. Calling the complete `RhInitialize` path would cross into GC initialization and finalizer-thread creation; it was not called or substituted for the narrow probe.

## Rebuilt runtime archive

The isolated Release `Runtime.WorkstationGC` archive was rebuilt from the pinned P23R8 staging tree with Ninja and MSVC 19.51.36260.0. Output:

`out\dotnet\phase23r11-workstationgc-rebuild1\build\nativeaot\Runtime\Full\Runtime.WorkstationGC.lib`

- Archive SHA-256: `3CCA536AC7CAADF77EE37D52DA79198C464CB8770E11EC5E6FDBAC02FFF55A32`.
- Archive member count: 62.
- It contains `thread.cpp.obj`, `threadstore.cpp.obj`, `guidexos/PalRedhawkStackBounds.cpp.obj`, `FinalizerHelpers.cpp.obj`, and `startup.cpp.obj`.
- The archive defines `PalGetMaximumStackBounds`, `ThreadStore::AttachCurrentThread`, and includes the real private `Thread::Construct` implementation. Its rebuilt target PAL object has SHA-256 `F050A14C13E6ECA8991E64AA81BB5CEB3221CD27F5461B51448D39CF39F91888`. The 30/30 descriptor/PAL regression used the target PAL object already staged under `D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING`; that object has SHA-256 `5BA72E6230488F91F1851EE04A5FCD5B9DECF7A3E5F46FB4E4E31DAABA7A97FA`.
- The archive still does not contain `CoffNativeCodeManager` or define `RhRegisterOSModule`. It is a runtime library, not a complete application or kernel link.

The archive build is successful evidence only for this isolated library target. It is not a full runtime, kernel, or image build.

## Bootstrap ThreadStore link probe

`Tests/NativeAotUnwind/bootstrap_threadstore_link_probe.cpp` uses the pinned runtime headers and the real archive implementation. It builds a hosted stack, validates it through the existing bootstrap descriptor builder, registers the descriptor through the production GUIDEXOS stack-bounds PAL object, and sets up a Windows FLS-backed host PAL association. Its intended checks include exact PAL bounds, actual `Thread::Construct` state, ThreadStore membership, current-thread identity, duplicate attachment, and failure cleanup after rejected hijack callback registration. It does not replace `Thread::Construct` or supply successful GC, EH, module, or runtime providers.

The hosted fixture uses a synthetic EFI memory-map record and a test page-table identity. `VirtualQuery` checks that the Windows allocation is committed and writable. This fixture does not establish EFI physical provenance or a guideXOS page-table mapping proof.

The reproducible runner is `Tools/Phase23/run_bootstrap_threadstore_link_probe.ps1`. It obtains the pinned WorkstationGC compile options from `compile_commands.json`, compiles the genuine harness, and attempts to link it against the fresh archive. The final run is retained under `out\dotnet\phase23r11-bootstrap-threadstore-link-probe-run5`; its link log SHA-256 is `57187737E4647D6BFEA79D8AB708C1A852EDD8964F2F94BFD7FA178BB72D88A7`.

The C++ harness and assembly compiled. The link failed with `LNK1120: 48 unresolved externals`; no executable was produced or run. Consequently, every runtime assertion in the harness is unexecuted evidence. In particular:

- Genuine `Thread::Construct`: **not executed**.
- Initial ThreadStore attachment/current-thread publication: **not executed**.
- Bootstrap bounds from `PalGetMaximumStackBounds` inside a runtime `Thread`: **not observed**. The PAL provider and descriptor contract passed their separate hosted regression only.
- ThreadStore creation failure cleanup and duplicate attach: **not executed**.
- Full `RhInitialize`, GC initialization, and managed finalization: **not executed**.

The direct probe's 48 unique unresolved externals group by owner as follows. The first 38 were also reproduced in the P23R10 link shape against this rebuilt archive; the last ten are additional dependencies pulled by the direct bootstrap harness and target runtime objects.

| Owner | Unresolved names |
|---|---|
| Runtime EH, dispatch, and stack trace (9) | `RhExceptionHandling_FailedAllocation`, `RhThrowHwEx`, `RhThrowEx`, `RhRethrow`, `RhpCidResolve`, `RhpCalculateStackTraceWorker`, `PopulateControlSegmentRegisters`, `RhpFailFastForPInvokeExceptionPreemp`, `RhpFailFastForPInvokeExceptionCoop` |
| PAL thread, synchronization, startup, and worker services (11) | `PalSleep`, `PalHijack`, `PalGetHijackTarget`, `PalCompatibleWaitAny`, `PalPrintFatalError`, `PalSwitchToThread`, `PalCreateEventW`, `PalStartBackgroundGCThread`, `PalCreateLowMemoryResourceNotification`; direct-probe additions: `PalInit`, `PalStartFinalizerThread` |
| PAL process, time, module, and memory services (11) | `PalQueryPerformanceCounter`, `PalQueryPerformanceFrequency`, `PalGetProcessCpuCount`, `PalGetTickCount64`, `PalGetModuleBounds`, `PalGetModuleFileName`, `PalAreShadowStacksEnabled`, `PalCopyTCharAsChar`, `PalVirtualAlloc`, `PalVirtualFree`; direct-probe addition: `PalGetModuleHandleFromPointer` |
| GUIDEXOS platform services (7, direct-probe additions) | `guidexos_pal_thread_id`, `guidexos_pal_vm_reserve`, `guidexos_pal_vm_commit`, `guidexos_pal_vm_release`, `guidexos_pal_monotonic_ticks`, `guidexos_pal_monotonic_frequency`, `guidexos_pal_fail_fast` |
| GC and processor support (6) | `?InitializeGCSelector@@YAHXZ`, `?InitializeGCEventLock@@YAXXZ`, `?InitSupportedInstructionSet@@YAXH@Z`, `?IsSupportedInstructionSet@@YA_NW4InstructionSet@@@Z`, `?do_vxsort_avx2@@YAXPEAPEAE0PEAE1@Z`, `?do_vxsort_avx512@@YAXPEAPEAE0PEAE1@Z` |
| Generated application/configuration inputs (4) | `ProcessFinalizers`, `g_compilerEmbeddedSettingsBlob`, `g_compilerEmbeddedKnobsBlob`, `g_requiredCpuFeatures` |

The P23R10-shaped reproduction reported 38 unresolved externals. The focused direct probe reported 48. These are link diagnostics, not 38 or 48 missing implementations that can safely be replaced by stubs: they span PAL services, runtime EH/dispatch, GC and CPU support, and generated managed application inputs. No dummy success provider was added. The direct probe's host FLS adapter only implements its explicit hosted thread-identity/association contract; it does not claim to implement the missing target PAL services.

The first blocker for this execution attempt is the unclosed link: the harness fails before any runtime code executes. The 48-name set is the linker's unresolved closure for this archive and harness; it is not proof that every name is directly called by `Thread::Construct`. It includes full-startup, GC, EH, and generated application dependencies. The current evidence therefore does not identify one symbol as the first runtime-time failure; a narrower link target must separate the genuine initial-thread dependency set from later runtime sections.

## Finalizer execution-context foundation

`Tools/Phase23/finalizer_stack_contract.h` defines an 88-byte hosted qualification descriptor with allocation and accessible intervals, mapping identity, owner identity, generation, and explicit hosted backing/fiber flags. Its validator rejects truncated ranges, invalid page alignment, wrong owner/generation, and a stack pointer outside the accessible interval. It is not wired into the production UEFI PAL and does not claim an EFI or kernel physical-page proof.

`Tests/NativeAotUnwind/hosted_finalizer_context_tests.cpp` uses a real Windows native worker thread, converts it to a fiber, creates a separate stack with `CreateFiberEx`, and switches to an assembly entry wrapper. `VirtualQuery` checks the OS allocation and committed accessible pages. The test checks x64 entry alignment, distinct root and finalizer stack intervals, return to the worker root stack, thread-local state isolation, owner/generation/mapping validation, rejection of invalid descriptors, retirement only after returning to the root stack, and no reuse without a new generation. Its activation-gate failure is deliberately injected after fiber entry to verify owner cleanup; a separate invalid-size `CreateFiberEx` call exercises an actual OS stack-creation failure.

The runner `Tools/Phase23/run_hosted_finalizer_context_tests.ps1` completed **20 passed, 0 failed** at `out\dotnet\phase23r11-hosted-finalizer-context-run8`. This is real hosted Windows execution-context and stack ownership evidence. It is not a NativeAOT finalizer thread: no `PalStartFinalizerThread`, `ThreadStore::AttachCurrentThread`, managed `ProcessFinalizers`, guideXOS stack allocator, UEFI memory map, or production PAL was exercised. Finalizer activation remains disabled.

## Hosted regressions and build/link results

| Check | Result | Scope |
|---|---:|---|
| Bootstrap descriptor/PAL | 30 passed, 0 failed | Production target PAL object; hosted descriptor fixture uses a synthetic EFI map and simulated page-table identity. `Thread::Construct` did not execute. Evidence: `out\dotnet\phase23r11-stack-bounds-descriptor-run1`. |
| Unsupported stack-bounds provider | 4 passed, 0 failed | Fail-closed behavior clears outputs and returns false. Evidence: `out\dotnet\phase23r11-stack-bounds-unsupported-run1`. |
| Bounded x64 unwind, bounded-only | 69 passed, 0 failed | Hosted bounded evaluator and PE preflight. |
| Bounded x64 unwind, Windows reference | 69 passed, 0 failed; 20 reference comparisons | Host reference coverage only; no target COFF manager. |
| Hosted finalizer worker-context foundation | 20 passed, 0 failed | Real Windows thread/fiber and OS-backed stack checks; no NativeAOT runtime attachment. Evidence: `out\dotnet\phase23r11-hosted-finalizer-context-run8`. |
| P23R10-shaped hosted link reproduction | 38 unresolved externals | Fresh archive; log at `out\dotnet\phase23r11-link-probe38\link_probe.log`. |
| Focused bootstrap ThreadStore link probe | 48 unresolved externals | Harness compiled, link failed, no executable; record/log at `out\dotnet\phase23r11-bootstrap-threadstore-link-probe-run5`. |

The four existing regression baselines remained green. No physical page tables, UEFI firmware, production scheduler stack, or target finalizer allocator was tested. No production kernel or ISO was built. No QEMU, QAC admission, or physical hardware was used.

## Safety gates and phase status

- No unbounded stack walking was restored; no failed root enumeration was converted to an empty root set; no exception exhaustion was treated as a metadata error.
- No incomplete code manager was published. No `RhRegisterOSModule` shim was introduced.
- `RhInitialize`, GC collection, finalizer processing, and NativeAOT finalizer ThreadStore attachment did not run.
- Phase 23 remains **blocked**: the target archive lacks the COFF code manager and `RhRegisterOSModule`, and the bounded unwind tests do not qualify GC-root enumeration or managed EH for the target runtime.
- No Phase 29–35 records, managed identities, production kernel, or release image were generated.
- No push occurred.

## P23R12 objective

Build a narrowly scoped, reproducible hosted link target from the pinned runtime that retains only the genuine `RuntimeInstance::Initialize`, `ThreadStore::Create`, `ThreadStore::AttachCurrentThread`, and `Thread::Construct` path. Satisfy only the PAL calls that path actually reaches with real host OS services; keep GC, finalizer startup, managed execution, and module publication disabled in that test. Execute it on the descriptor-validated bootstrap stack and record current-thread identity, exact bounds, ThreadStore membership, duplicate attach, and failure cleanup. Do not use unresolved-symbol forcing or dummy success exports.

In parallel, keep complete supported startup blocked until the target `RhRegisterOSModule`/COFF manager and the required GC/EH safety contract are qualified. Only after those gates, add a guideXOS-owned finalizer stack with verified backing/mapping/lifetime and attach its native thread through the real ThreadStore path. Do not expand into general scheduler or SMP work.
