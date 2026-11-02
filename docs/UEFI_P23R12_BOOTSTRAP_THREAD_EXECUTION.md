# P23R12 — NativeAOT Bootstrap Thread Execution

**Outcome C — `P23R12_BOOTSTRAP_LINK_BLOCKED`.** The pinned WorkstationGC archive rebuilt and the 38- and 48-external failures reproduced against it. The hosted bootstrap harness compiled, but the genuine archive link still has 48 unresolved symbols. No accepted bootstrap executable was produced, so no NativeAOT initialization or thread code ran. The failure is at the archive-member link boundary: 35 members enter the attempted link, including runtime startup, GC, EH, and finalizer members outside the intended bootstrap path. The link-time optimization trial retained the same 48-symbol closure. Filling those dependencies with placeholders would violate the requested runtime boundary.

## Repository state and provenance

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`; branch: `BUILD_INFRA`.
- Starting HEAD: `794bdd7a41749d07f5c2ffb461bdae243967795c`.
- The checkout started clean and equal to `origin/BUILD_INFRA` (0 ahead, 0 behind). Ending HEAD is the local P23R12 report commit on `BUILD_INFRA` (1 ahead, 0 behind); its exact SHA is in the final execution summary.
- `out\rt` remained at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with all 65 starting porcelain entries preserved. The SHA-256 of the ordered status text before and after the work is `59610A7C32254E178B563A23AA2C4D5E0389FCF9873E1ED2CDBF21B76B724DF9`.
- The separate source tree used by the existing WorkstationGC build is `D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING`, also at the pinned revision. Its 67 starting status entries were left unchanged (ordered-status SHA-256 `C44455A60F75118825D1DF9792DFC371E12EC0425EB597D66A24DF1DDCBCE15C`). The P23R8 overlay is already applied there and passes `git apply --reverse --check --unidiff-zero`; the overlay patch SHA-256 is `FB67C4BD2325C2CE33D15E7BB667F1BF9E3B10D6293F4B6FC9944B0F771071A6`.
- All P23R12 build and test outputs are isolated beneath `out\dotnet\phase23r12-*`. No other checkout was edited. There was no branch switch, merge, rebase, reset, cherry-pick, or push.

## Link failure reproductions

Both failures were linked against the newly rebuilt `Runtime.WorkstationGC.lib` (SHA-256 `BAC72943DA254D8A6A6AB2689AA45CDA5C6BF3A424DC3D4F19646EE3C599D550`):

| Harness shape | Result | Command and output evidence |
|---|---:|---|
| P23R10 object set from `phase23r10-bootstrap-thread-store-tests-run6` | 38 unique unresolved externals; no executable | `out\dotnet\phase23r12-baseline38-fresh\reproduce.cmd`, `link_probe.log` (SHA-256 `77CC1364CFBF085312873374E9DBE04C4139DFD228139B23826E55584DD66050`) |
| P23R11 direct ThreadStore harness | 48 unique unresolved externals; no executable | `out\dotnet\phase23r12-bootstrap-threadstore-link-probe\build_link_probe.cmd`, `link_probe.log` (SHA-256 `91900E03FEF12B55D7F2CD1071F29C4206F51C11C2A0A2FB0F64F5D88A746FE0`), and `bootstrap-threadstore-link-probe-record.json` |

The full direct link command is preserved in `build_link_probe.cmd`. Its record contains the 48 distinct symbols. The log contains repeated references as well as the final `LNK1120: 48 unresolved externals` diagnostic.

To record object extraction, a separate diagnostic link used `/FORCE:UNRESOLVED` and emitted `out\dotnet\phase23r12-forced-extraction-diagnostic\link_diagnostic.map`. It produced a file named `DIAGNOSTIC_NEVER_RUN.exe` solely so LINK would emit the map. That file has unresolved addresses, is not a bootstrap executable, and was never run. Its map lists 35 unique `Runtime.WorkstationGC` archive members. The selected member names are saved in `selected-runtime-members.txt`; the full diagnostic command and log are in the same directory. This map is used only to identify member origins, not as runtime evidence.

The archive has 62 members and is 5,594,878 bytes. The fresh CMake/Ninja build log is `out\dotnet\phase23r12-workstationgc-rebuild1\runtime-build.log` (SHA-256 `807CA56E6E00DD4326B66A6DBA869E59F27CED87520F1BC8517A8BABD1EC0191`). It built `Runtime.WorkstationGC` from the pinned source tree with the existing GUIDEXOS target definitions. The archive includes the genuine `RuntimeInstance.cpp.obj`, `threadstore.cpp.obj`, `thread.cpp.obj`, and `guidexos/PalRedhawkStackBounds.cpp.obj`. Their SHA-256 values are respectively:

- `RuntimeInstance.cpp.obj`: `7C4615DD9A2E51A26272C47E0CEDD62BDDA76D4D731C65DFB3275F443D82CC1E`
- `threadstore.cpp.obj`: `B1E6B031C8B5FDA6A1C5AA8DA1076DFB9AD9C0037ADB417495E5BD62C7BEBAE8`
- `thread.cpp.obj`: `99AC82EC394A1B7087169BB889A62DB85C712BBE328DBA286AA6F3CA48CC7F16`
- `PalRedhawkStackBounds.cpp.obj`: `111706663DF4336EC70ADF7169DDEEEC159E19DD430F703310F9FEEEB1AE5B4B`

The forced diagnostic map attributes `RuntimeInstance::Initialize` to `RuntimeInstance.cpp.obj`, `ThreadStore::Create` and `ThreadStore::AttachCurrentThread` to `threadstore.cpp.obj`, `Thread::Construct` to `thread.cpp.obj`, and `PalGetMaximumStackBounds` to `PalRedhawkStackBounds.cpp.obj`. These are source-to-link-member origins only; none executed. A trial that recompiled the three core C++ files with `/GL /Ob0` and linked with `/LTCG` still reported 48 unique unresolved symbols. It did not close the link.

## Dependency classification

The 48 names are not 48 required implementations for construction. LINK reports undefined references from extracted object members before the narrow test can execute. The source call sites and extracted members place them in these roles:

| Role | Symbols | Source role and boundary |
|---|---|---|
| Runtime thread operations retained with `thread.cpp` and `threadstore.cpp` | `PalSleep`, `PalHijack`, `PalGetHijackTarget`, `PalCompatibleWaitAny`, `PalPrintFatalError`, `PalSwitchToThread`, `PalQueryPerformanceCounter`, `PalQueryPerformanceFrequency` | Sleep, hijacking, waits, failure reporting, suspension/yield, and stress-log timing are used by other thread/runtime methods. The immediate attach sequence uses none of these calls. `PalHijack` and its target are part of the later GC suspension contract. |
| General runtime exception, dispatch, and module paths | `RhExceptionHandling_FailedAllocation`, `RhThrowHwEx`, `RhThrowEx`, `RhRethrow`, `RhpCalculateStackTraceWorker`, `RhpCidResolve`, `PalGetModuleBounds`, `PopulateControlSegmentRegisters`, `PalGetModuleFileName`, `PalAreShadowStacksEnabled`, `PalGetModuleHandleFromPointer`, `RhpFailFastForPInvokeExceptionPreemp`, `RhpFailFastForPInvokeExceptionCoop` | These serve exception propagation, stack traces, interface dispatch, module inspection, and P/Invoke guards. They are not called by the four initial-thread functions. Their proper providers belong to a broader runtime/application link. |
| Standard runtime startup and generated configuration inputs | `PalInit`, `InitializeGCEventLock`, `g_requiredCpuFeatures`, `g_compilerEmbeddedSettingsBlob`, `g_compilerEmbeddedKnobsBlob` | `PalInit` and the GC event lock are used by `RhInitialize`; compiler settings and CPU features are generated application inputs. The harness calls `RuntimeInstance::Initialize` directly and must not enter `RhInitialize`. |
| Runtime configuration, process, time, and heap block services | `PalCopyTCharAsChar`, `PalGetProcessCpuCount`, `PalGetTickCount64`, `PalVirtualAlloc`, `PalVirtualFree` | Character conversion is used by environment/config handling; process count and tick count serve general runtime helpers. The allocation pair is referenced by `AllocHeap::_AllocNewBlock` and `AllocHeap::~AllocHeap` in the extracted runtime heap member. These are host platform services, but not the C++ `new (nothrow)` calls that create `RuntimeInstance` and `ThreadStore`, and not required by their initialization path. |
| GC initialization and target GC PAL | `InitializeGCSelector`, `PalStartBackgroundGCThread`, `guidexos_pal_thread_id`, `InitSupportedInstructionSet`, `IsSupportedInstructionSet`, `do_vxsort_avx2`, `do_vxsort_avx512`, `guidexos_pal_vm_reserve`, `guidexos_pal_vm_commit`, `guidexos_pal_vm_release`, `guidexos_pal_monotonic_ticks`, `guidexos_pal_monotonic_frequency`, `guidexos_pal_fail_fast` | These are GC selector, processor, virtual-memory, timing, and fail-fast services. The archive contains GC members that refer to them, but P23R12 must not initialize or run GC to resolve this group. Real source implementations exist in separate runtime/GC sources; they are not complete hosted providers for this narrow link. |
| Runtime event service | `PalCreateEventW` | This backs `CLREventStatic::CreateAutoEventNoThrow` in the extracted event member. It is a real synchronization service used by later runtime/GC work, not by initial-thread construction or attachment. |
| Finalization startup and generated managed method | `PalStartFinalizerThread`, `PalCreateLowMemoryResourceNotification`, `ProcessFinalizers` | These are reached from finalization initialization and the finalizer callback after GC initialization. They are not needed to create or attach the initial thread and remain disabled. `ProcessFinalizers` is an application-generated managed method, not a platform service to stub. |

The immediate path's corresponding services are present or already resolved in the attempted link: thread identity, `PalAttachThread`, `PalRegisterHijackCallback`, allocation, synchronization, the thread-local `tls_CurrentThread`, and the production stack-bounds provider. The existing host adapter uses Windows FLS for its PAL association and `GetCurrentThreadId` for identity. The callback registration code stores the registered callback, matching the registration portion of the Windows PAL; it does not establish that later GC hijacking works. No runtime behavior is claimed from these adapters because the executable did not link.

The failure is caused by the supported archive member boundary: extracting members for required symbols also exposes unrelated undefined references in those same members and in their transitive member closure. Thirty-five archive members appear in the forced diagnostic map, including `startup.cpp.obj`, `FinalizerHelpers.cpp.obj`, `EHHelpers.cpp.obj`, `gcwks.cpp.obj`, `gcenv.guidexos.cpp.obj`, `StackFrameIterator.cpp.obj`, and assembly dispatch/exception members. `/OPT:REF` and the tested `/LTCG` variation did not produce a safe narrow executable. No functions were replaced, and no symbols were marked as successfully provided.

## Stack and runtime execution results

The latest rebuilt production GUIDEXOS stack-bounds object passed the preserved descriptor/PAL suite: **30 passed, 0 failed**. The unsupported provider suite passed **4 passed, 0 failed**. These fixtures use a synthetic EFI memory-map entry and simulated page-table identity; they do not prove Windows allocation pages are UEFI physical pages or execute Thread code.

The P23R11 bootstrap harness and stack-switch assembly compiled against the fresh archive, but the standard link failed with 48 unresolved externals. Therefore:

- `RuntimeInstance::Initialize`: not executed.
- `ThreadStore::Create`: not executed.
- `ThreadStore::AttachCurrentThread`: not executed.
- Genuine `Thread::Construct`: not executed. The forced diagnostic map proves the intended symbol's archive origin only.
- Runtime association, ThreadStore membership, duplicate attachment, and current-thread lookup: not observed.
- Production stack bounds consumed by a runtime thread: not observed. The descriptor-to-PAL regression passed separately; the OS-backed bootstrap stack in the harness was never entered.
- NativeAOT failure injection and partial-cleanup behavior: not executed. The harness contains a ThreadStore callback-registration failure case, but it cannot run before link closure.

No `RhInitialize`, GC initialization/collection, finalizer processing, module registration, COFF manager, or managed code path was invoked. No placeholder export was added for any of them.

## Preserved regressions

| Suite | Result | Evidence scope |
|---|---:|---|
| Descriptor/PAL | 30/30 | Fresh target PAL object; hosted map and page-table checks are simulated. |
| Unsupported stack bounds | 4/4 | Fail-closed outputs and false result. |
| Bounded x64 unwind | 69/69 | Hosted bounded evaluator and PE preflight. |
| Windows-reference unwind | 69/69, 20 reference cases | Host `RtlVirtualUnwind` comparisons only. |
| Hosted worker context | 20/20 | Windows thread/fiber and separate OS-backed stack; no NativeAOT finalizer or ThreadStore execution. |

The descriptor, unsupported, and worker records are under `out\dotnet\phase23r12-stack-bounds-descriptor`, `out\dotnet\phase23r12-stack-bounds-unsupported`, and `out\dotnet\phase23r12-hosted-finalizer-context`. The bounded unwind runner emitted the two executable results in a unique Windows temporary directory.

## Phase status and P23R13 objective

Phase 23 remains **blocked**. The genuine initial-thread test did not cross the link boundary, and `RhRegisterOSModule`/the target COFF manager remain separate gates. The 20 worker-context checks still validate hosted stack ownership and identity only; they do not validate NativeAOT finalization.

Recommended P23R13 objective: create a reproducible, pinned runtime object partition that places the unchanged genuine `RuntimeInstance::Initialize`, `ThreadStore::Create`, `ThreadStore::AttachCurrentThread`, `Thread::Construct`, and their required TLS/lock code into linkable members without pulling unrelated startup, GC, EH, or finalizer code. Prove each linked body against the pinned source, then provide only the real host services that this partition actually reaches. Keep `RhInitialize`, GC, module publication, managed execution, and finalizer processing gated. Resume the attachment and failure-injection matrix only after that target produces a zero-unresolved standard link.

No Phase 29–35 identities or production ISO were generated. No production kernel/image was changed. Hosted builds/tests only: no QEMU, QAC admission, physical hardware, or push.
