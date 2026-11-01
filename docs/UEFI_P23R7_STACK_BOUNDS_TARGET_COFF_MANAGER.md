# P23R7 — Stack Bounds and Target COFF Manager

**Outcome C — `P23R7_RUNTIME_CONTRACT_BLOCKED`.** The target WorkstationGC archive now contains a GUIDEXOS `PalGetMaximumStackBounds` definition, but that definition deliberately returns failure because the kernel does not provide NativeAOT with authoritative per-thread stack descriptors. The genuine COFF manager, bounded target unwind, GC/EH failure reporting, safe preparation, and publication therefore remain unavailable. No manager or registration fallback was added.

## Checkout, commits, and preservation

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`.
- Branch: `BUILD_INFRA`.
- Starting HEAD: `df9825c7709615de1900ef65deb81ee930c74c5b`.
- Starting worktree: clean; `BUILD_INFRA` was three commits ahead and zero commits behind `origin/BUILD_INFRA`.
- Ending HEAD: the local commit containing this report. Its SHA is included in the final execution summary because a commit cannot include its own object ID in its tree.
- The original `out\rt` stayed detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. Its final status has the same 65 entries and SHA-256 `59610A7C32254E178B563A23AA2C4D5E0389FCF9873E1ED2CDBF21B76B724DF9` recorded at P23R6 start.
- Runtime source edits were applied only to `D:\dev\guideXOSUEFI_P23R7_RUNTIME_STAGING`, a separate copy of the P23R6 staging checkout. That copy remained at the pinned runtime commit. Its P23R6 build cache was moved to a clearly named preservation directory before a clean P23R7 configuration; the original P23R6 staging directory was not changed.
- No branch switch, merge, rebase, cherry-pick, reset, push, QEMU run, QAC admission, hardware test, production image build, or Phase 29–35 generation occurred.

Files committed for P23R7:

- `docs/UEFI_P23R7_STACK_BOUNDS_TARGET_COFF_MANAGER.md`
- `Tools/Phase23/runtime-overlay/p23r7_runtime.patch`
- `Tools/Phase23/run_stack_bounds_unsupported_tests.ps1`
- `Tests/NativeAotUnwind/stack_bounds_unsupported_tests.cpp`

## Stack-bounds ABI and provenance

The pinned declaration is `bool REDHAWK_PALAPI PalGetMaximumStackBounds(void** lowOut, void** highOut)`. On this x64 target `REDHAWK_PALAPI` is `__stdcall`. Success is expected to return the complete maximum stack allocation for the **calling OS thread** as a low address and an exclusive high address. The Windows implementation uses the stack allocation base and `NT_TIB::StackBase`; the Unix implementation obtains the current pthread's address and size. Neither implementation retrieves an arbitrary foreign thread's bounds from this ABI, and maximum allocation bounds do not by themselves identify committed or currently accessible pages.

`Thread::Construct` invokes this function when NativeAOT attaches an OS thread, stores the result in that `Thread`, and calls `RhFailFast` if the PAL call fails. The P23R6 iterator overlay obtains bounds from the `Thread*` being walked and carries that owner plus its low/high values in `REGDISPLAY`. Its checks use an ordered half-open interval and reject an owner mismatch or an out-of-range/misaligned caller SP. This avoids substituting the collector's current stack for a foreign thread's stack, but it does not supply the missing bounds or make the COFF manager's reads bounded.

The available guideXOS allocations are not connected to this PAL contract:

- The UEFI loader allocates 128 pages (512 KiB) for the handoff stack and passes `stackTop` to the trampoline. The current `BootInfo` does not carry the allocated stack's low/high pair, and the NativeAOT PAL has no validated path from `BootInfo` or the UEFI memory map to the current thread descriptor.
- Kernel code separately allocates 64 KiB interrupt/kernel stacks and per-thread stacks; SMP startup also reserves 1 MiB per CPU. These allocator-owned ranges are tracked by kernel code, not registered with NativeAOT's `ThreadStore`.
- NativeAOT's attach path is designed to discover the current OS thread's stack. GUIDEXOS has no corresponding thread/stack provider in its PAL, and the external `PalStartBackgroundGCThread`/`PalStartFinalizerThread` contracts do not provide stack descriptors to the runtime.

There is no verified bootstrap-thread provider, independently created managed-thread provider, or suspended foreign-thread lookup. The available stack allocations have known extents at their allocation sites, but their ownership, lifetime, mapping, and accessible-page information are not passed through to the runtime. Inferring a range from RSP would not establish that provenance.

The P23R7 overlay adds `guidexos/PalRedhawkStackBounds.cpp` and selects it only for `TARGET_GUIDEXOS`. It clears each non-null output and returns `false`. `Thread::Construct` consequently fails fast before NativeAOT can use invented bounds. This is an explicit unsupported path, not an implementation of authoritative bounds. Four hosted checks linked against the exact target-built object verify the false result and output clearing.

## Bounded unwinder integration

The P23R6 `REGDISPLAY` and `StackFrameIterator` changes remain in the isolated target build. The standalone bounded evaluator still validates explicit image and stack views, half-open ranges, alignment, overflow, supported x64 unwind forms, saved-register locations, and transactional caller-context updates. Its 69-case bounded suite includes nested frames, invalid/missing/foreign-owner bounds, underflow/overflow, invalid restore state, and no partial state after failure.

The evaluator is **not** called by the target `StackFrameIterator` or a target COFF manager. The P23R6 iterator checks wrap the legacy `ICodeManager::UnwindStackFrame` contract, whose AMD64 implementation in `CoffNativeCodeManager.cpp` uses `RtlVirtualUnwind`. That source is not selected for GUIDEXOS. Synthetic stack views do not prove real guard-page behavior, target page accessibility, or different live OS-thread stacks. Managed/native transition handling and target saved-root writes were not exercised.

## Genuine COFF manager and target archive

The pinned runtime adds `windows/CoffNativeCodeManager.cpp` only in the `CLR_CMAKE_TARGET_WIN32` source branch. The GUIDEXOS source branch does not select it. The genuine source has direct PE and GC/EH metadata reads; its AMD64 paths call `RtlVirtualUnwind` for outgoing-argument bounds, frame unwind, and return-address hijack calculations. The pinned manager's GC virtual returns `void`, and its EH virtuals return `bool`, where `false` does not distinguish exhaustion from malformed data. Compiling that source unchanged would therefore introduce Windows dependencies and restore unbounded reads. No simplified substitute was used.

The reproducible P23R7 runtime delta is [p23r7_runtime.patch](../Tools/Phase23/runtime-overlay/p23r7_runtime.patch). Forward and reverse `git apply --check` both passed against the P23R6 staging source.

The clean isolated `Runtime.WorkstationGC` build completed with MSVC 19.51. The archive is:

`D:\dev\guideXOSUEFI_P23R7_RUNTIME_STAGING\artifacts\obj\coreclr\guidexos.x64.Release\nativeaot\Runtime\Full\Runtime.WorkstationGC.lib`

- SHA-256: `28AE30DFFE89EE2EBE93D1A40F48A08165B3805CB7C9124C2BBFC42EF2EC05F0`.
- 62 archive members; `PalRedhawkStackBounds.cpp.obj` is present.
- Symbol inspection found one external definition of `PalGetMaximumStackBounds`. A referring runtime member also has an `UNDEF` entry, but the archive has the matching definition, so this PAL symbol is internally resolvable. The four-case hosted executable linked directly against that target-built object.
- No `CoffNativeCodeManager.cpp.obj`, `CoffNativeCodeManager` implementation symbol, or `RhRegisterOSModule` definition is present. `RhpRegisterOsModule` is present but is the separate OS-module-list insertion helper.
- This archive is not a target manager archive and not a final target link. The raw undefined-symbol report is `out\dotnet\phase23r7-stack-bounds-runtime-validated\Runtime.WorkstationGC.unresolved-lines.txt`. After matching undefined references against definitions in the archive, 85 distinct names remain external to this archive; their intended target linkage was not run or established.

The 85 external names are:

```text
__GSHandlerCheck, __ImageBase, __imp_GetEnabledXStateFeatures,
__imp_QueryPerformanceCounter, __imp_QueryPerformanceFrequency, __imp_SleepEx,
__isa_available, __report_rangecheckfailure, __security_check_cookie,
__security_cookie, _fltused, _stricmp, _tls_index, CloseHandle,
DeleteCriticalSection, DuplicateHandle, EnterCriticalSection,
FlushProcessWriteBuffers, g_compilerEmbeddedKnobsBlob,
g_compilerEmbeddedSettingsBlob, g_requiredCpuFeatures, GetCurrentProcess,
GetCurrentThread, GetEnvironmentVariableA, GetSystemTimeAsFileTime,
guidexos_pal_fail_fast, guidexos_pal_monotonic_frequency,
guidexos_pal_monotonic_ticks, guidexos_pal_thread_id, guidexos_pal_vm_commit,
guidexos_pal_vm_release, guidexos_pal_vm_reserve, InitializeCriticalSectionEx,
LeaveCriticalSection, memcmp, memcpy, memmove, memset,
PalAreShadowStacksEnabled, PalAttachThread, PalCompatibleWaitAny,
PalCopyTCharAsChar, PalCreateEventW, PalCreateLowMemoryResourceNotification,
PalDetachThread, PalFlushInstructionCache, PalGetCurrentOSThreadId,
PalGetHijackTarget, PalGetModuleBounds, PalGetModuleFileName,
PalGetModuleHandleFromPointer, PalGetProcessCpuCount, PalGetTickCount64,
PalHijack, PalInit, PalPrintFatalError, PalQueryPerformanceCounter,
PalQueryPerformanceFrequency, PalRegisterHijackCallback, PalSleep,
PalStartBackgroundGCThread, PalStartFinalizerThread, PalSwitchToThread,
PalVirtualAlloc, PalVirtualFree, PalVirtualProtect, PopulateControlSegmentRegisters,
ProcessFinalizers, RaiseFailFastException, ResetEvent,
RhExceptionHandling_FailedAllocation, RhpCalculateStackTraceWorker, RhpCidResolve,
RhpFailFastForPInvokeExceptionCoop, RhpFailFastForPInvokeExceptionPreemp,
RhRethrow, RhThrowEx, RhThrowHwEx, SetEvent, strcmp, strcpy, strstr, strtoul,
strtoull, WaitForSingleObjectEx
```

The initial build-wrapper invocation used a relative output path and emitted a misleading record from the copied P23R6 archive before the command ran. `out\dotnet\phase23r7-stack-bounds-runtime\run-validity.json` explicitly invalidates it. The successful evidence is the absolute-path build record and archive named above.

## GC, EH, preparation, and publication

No target GC failure-propagation change was made. The genuine target manager is absent, so invalid bounds, unwind errors, invalid GC metadata, invalid root locations, and callback failures still have no target manager result channel. The existing `gc_enum_status_adapter.h` remains a hosted adapter only. A failed target enumeration cannot yet be shown to stop collection safely.

No target EH status mechanism was added. The authentic host fixture still decodes one typed clause and associates one catch funclet among 470 found funclets, but it does not distinguish malformed metadata from normal enumeration exhaustion through a target manager and it does not test full exception propagation.

PE preflight remains a staging-only helper. It is not connected to target image/metadata extents or manager construction. No manager preparation transaction, unboxing-stub staging, rollback, reader synchronization, or publication tests were added. `RuntimeInstance::RegisterCodeManager` and the later fallible unboxing-stub registration retain the ordering/lifetime risk documented in P23R5. No production registration was attempted.

`RhRegisterOSModule` remains absent and the Phase 23 registration gate remains blocked. The archive's new PAL definition does not reopen that gate. No production kernel or image was built.

## Hosted test results

| Category | Result | Scope |
|---|---:|---|
| Standalone bounded unwind | 69 passed, 0 failed | Includes the existing PE preflight, status, bounds, nested-frame, and no-partial-update cases. |
| Windows unwind reference | 69 passed, 0 failed; 20 reference comparisons | Hosted `RtlVirtualUnwind` comparisons; not target manager evidence. |
| GUIDEXOS PAL fail-closed object | 4 passed, 0 failed | Exact object from the P23R7 WorkstationGC build; confirms unsupported return and cleared outputs. |
| Authentic GC fixture through genuine **host** COFF manager | 1 consumer run passed | Three GC snapshots (7, 6, and 1 roots); six writable slots updated; injected callback failure, invalid root, and failed-unwind guard checks passed. Target runtime integration is false. |
| Authentic EH fixture through genuine **host** COFF manager | 1 typed clause; 1 catch-funclet association out of 470 | No malformed-manager injection and no full exception propagation. |
| Genuine target-manager consumer | 0 | The target archive contains no COFF manager. |
| Target GC/EH failure-injection tests | 0 | No target manager or integrated result path exists. |
| Manager preparation/publication tests | 0 | No target manager is prepared or published. |

The bounded suites ran from `Tests/NativeAotUnwind/run_x64_unwind_tests.ps1`. The authentic host consumer result and evidence are under `out\dotnet\phase23r7-hosted-consumer-test`. The new fail-closed test can be rerun with `Tools/Phase23/run_stack_bounds_unsupported_tests.ps1 -SourceRoot <P23R7 staging root>`.

The host GC/EH results are limited to the pinned Windows host archive and retained NativeAOT fixture. They do not demonstrate target manager construction, a real compacting collection, target root enumeration, or exception dispatch.

## WorkstationGC, ServerGC, and Phase 23 status

- WorkstationGC: clean isolated archive build succeeded, with the new fail-closed PAL object.
- ServerGC: not built in P23R7. The P23R6 full-runtime attempt stopped on the missing `GCEventFireSizeAdaptationTuning_V1`, `GCEventFireSizeAdaptationFullGCTuning_V1`, and `GCEventFireSizeAdaptationSample_V1` functions. No dummy event functions were added.
- Phase 23: still blocked. `RhRegisterOSModule` is absent; no production registration or proof rebuild was attempted.
- Production kernel/image: not built.
- QEMU / QAC / physical hardware: not used.
- Push: none.

## Recommended P23R8 objective

Define a lifetime-safe GUIDEXOS thread-stack descriptor containing the exact low/high range, owner identity, and accessible-page provenance. Pass the UEFI bootstrap allocation into the kernel explicitly, and connect kernel-created and suspended foreign-thread descriptors to NativeAOT attachment and stack walking. Add hosted tests for distinct real allocations and inaccessible boundaries. Only after that contract is verified, adapt the genuine COFF manager to bounded image and stack views, give GC/EH explicit internal completion/error status, then prepare manager and unboxing metadata before one reader-safe publication point.
