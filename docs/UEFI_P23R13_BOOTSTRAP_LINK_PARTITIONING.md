# P23R13 — NativeAOT Bootstrap Link Partitioning

**Outcome C — `P23R13_IRREDUCIBLE_RUNTIME_DEPENDENCY` for this phase.** The pinned runtime rebuilt with an object-level source split. The standard bootstrap link still fails with the same 48 unresolved externals, and it produces no executable. The source and map evidence confirms that the genuine constructor and ThreadStore implementations are in dedicated objects, but this split did not close the link or reduce the unresolved set. In particular, `ThreadStore::Create` registers the real hijack callback, whose genuine body requires `PalGetHijackTarget`; its Windows implementation includes the CET/shadow-stack return-address target contract. A success-only shim or default-target provider without checking shadow-stack policy would not be safe. The broad transitive closure also retains GC event initialization, GC, EH, and runtime-startup members. Phase 23 remains blocked.

Outcome C here means that the requested path could not be safely linked with the available services and partition in P23R13. It does not claim that no future source partition can close the link.

## Repository state and scope

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`, branch `BUILD_INFRA`.
- Starting HEAD: `6fc7acce8576425994d0bc46692c5101c1449e9c`; it was one commit ahead of `origin/BUILD_INFRA` and had no worktree changes.
- Ending HEAD: the local P23R13 commit on `BUILD_INFRA`; its exact SHA is returned by `git rev-parse HEAD` and included in the final execution report. The branch remains local and is not pushed.
- The protected `out\rt` tree stayed at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with all 65 starting status entries. Its saved starting status file, `out\dotnet\p23r9-runtime-status-preservation.txt`, remains byte-identical (SHA-256 `7D48B6B71F2B7B53794738B6974A8D611E51F0780D757650D648B8239C0B35A1`).
- `D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING` stayed at the same pinned commit with all 67 pre-existing status entries. Its ordered-status SHA-256 is `C44455A60F75118825D1DF9792DFC371E12EC0425EB597D66A24DF1DDCBCE15C`, matching the P23R12 record. The previously applied P23R8 overlay still passes its reverse check.
- The source experiment used a copied runtime tree at `out\dotnet\phase23r13-runtime-partition-source`. No Git commands were run from that copy. Its only build inputs were the pinned staging source plus the P23R13 patch below.
- `MODERN_INPUT`, `APP_MODEL`, `out\rt`, and the staging tree were not edited. No branch switch, merge, rebase, reset, cherry-pick, or push occurred.

## Link experiments and counts

The P23R12 WorkstationGC archive had 62 members (SHA-256 `BAC72943DA254D8A6A6AB2689AA45CDA5C6BF3A424DC3D4F19646EE3C599D550`). Its standard probe reported 48 unresolved symbols; `link_probe.log` SHA-256 `91900E03FEF12B55D7F2CD1071F29C4206F51C11C2A0A2FB0F64F5D88A746FE0`. The `/FORCE:UNRESOLVED` diagnostic map SHA-256 was `9F8F501C403468051BAA321012713DBE4D93F493B8B7829DC01ECAB8209F5D55` and represented 35 runtime members; the diagnostic executable was never run.

P23R13 first rebuilt the unchanged staged runtime with `/Gy` and `/Gw` on `RuntimeInstance.cpp`, `threadstore.cpp`, and `thread.cpp`. The archive remained at 62 members (SHA-256 `020700FAACDA545E266842F4D117544738510E0983E66E39895A26DBB9C401CB`). Its standard link log SHA-256 is `2C9B1CD50D2BD1967065594629E2D37590485EBA1043A46C4D21FD8BF7AB83E9`; the forced diagnostic map SHA-256 is `24978B3EEE1CCBB6A56A554D592B0A3D5F189306D9D8332D2BEB3C4023F81C8F`. That map represented 29 members, six fewer than P23R12, but the standard unresolved set remained 48. The six map members absent after `/Gw` were `gcconfig.cpp.obj`, `gcscan.cpp.obj`, `gctoclreventsink.cpp.obj`, `handletablecore.cpp.obj`, `RestrictedCallouts.cpp.obj`, and `softwarewritewatch.cpp.obj`.

The source-partitioned Release build used the existing GUIDEXOS target definitions and `/Gy /Gw` on all five relevant units: `RuntimeInstance.cpp`, `thread.cpp`, `thread.bootstrap.cpp`, `threadstore.cpp`, and `threadstore.bootstrap.cpp`. The archive contains 64 members, including the two new translation units; SHA-256 `458069AF2E4EBE778C4830DDEE0683B850C33CEDC349106FE7C1B4C8BC28E255`. Compile database SHA-256: `726FD92622CF121C53923C9F44B0249DF0FD63AEBC51F8F5A0F9332B279B41B0`. Final build log: `out\dotnet\phase23r13-source-partition-build\runtime-rebuild5.log`, SHA-256 `87860B3FAD587FAC6BFAE40A3CFFF126E5E58DE42058B3DA1E27C4C5F5AC0995`.

The standard source-split link still ended with `LNK1120: 48 unresolved externals`; its map file is empty and `bootstrap_threadstore_link_probe.exe` does not exist. The diagnostic `/FORCE:UNRESOLVED` map represents 30 runtime object members. The normal-link error log names 22 distinct archive members that emitted unresolved references. These are evidence counts, not an exact count of members loaded by the failed standard link: the standard map is empty on failure, and the forced diagnostic image has different retention behavior. The standard link still names `threadstore.cpp.obj` in its unresolved diagnostics even though that object is absent from the forced map's retained module list.

For clarity, the comparable recorded numbers are:

| Build | Archive members | Forced-map runtime members | Standard unresolved symbols |
|---|---:|---:|---:|
| P23R12 pinned rebuild | 62 | 35 | 48 |
| P23R13 `/Gy /Gw`, original translation units | 62 | 29 | 48 |
| P23R13 source partition | 64 | 30 | 48 |

P23R13 did not achieve the requested dependency reduction at the standard link boundary. `/Gy` was already present in the Release compile configuration; adding `/Gw` removed six members from the diagnostic map but did not change any unresolved symbol. Earlier P23R12 trials also retained 48 unresolved symbols under `/GL /LTCG` and `/OPT:ICF`; `/WHOLEARCHIVE` increased the unique unresolved count to 50, adding `PalFlushInstructionCache` and `PalVirtualProtect`. `/OPT:REF` was retained in the standard link. No optimization setting was used to hide runtime instrumentation.

The source-partition archive was rebuilt with `call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat" x64` followed by `cmake --build "D:\dev\guideXOSUEFI_BUILD_INFRA\out\dotnet\phase23r13-partition-workstationgc" --target Runtime.WorkstationGC -- -v`. The standard and forced link commands, compiler command line, logs, maps, and member lists are under `out\dotnet\phase23r13-link-partitioning-source-split5`; the command-file SHA-256 is `437921390CF60770722E549269D0D41CAB20AB94A46EC646CE994C374F07923A`. The standard link log SHA-256 is `89A891F657D371C6BB3D1F7AB5749B2B5BF53BBE9F4B562563BBC377B85010B9`. The forced map SHA-256 is `CBC91936C5D74F939D8C7706C362333A1861E32C49744C3D53AF84D0647EC33D`. The forced diagnostic executable SHA-256 is `C7987AA71AB0DF4EA6E124714434716C42BB7B032785349C584B5846834F1006`; it is unresolved and was never run.

The link record deliberately distinguishes the forced-map module list from the normal link's unresolved-reference member list. It does not label the forced map count as a guaranteed normal-link extraction count.

## Classification of the P23R12 35-member map

Every row below appeared in the P23R12 forced diagnostic map. The last column records its appearance in the P23R13 source-split forced map. A map entry proves that code or data from the object appears in that diagnostic image; it does not prove the named behavior executed. A dash for `threadstore.cpp.obj` must be read with the note that the failed standard-link diagnostics still identify that member.

| Archive member | Role in the closure and disposition | P23R13 source-split forced map |
|---|---|---|
| `CachedInterfaceDispatch.cpp.obj` | Interface-dispatch cache and state. It belongs to the general managed dispatch path, not the initial native thread attachment. | Retained |
| `Crst.cpp.obj` | Runtime critical-section implementation; the ThreadStore lock is part of the real attach/list-membership invariant. | Retained |
| `DebugHeader.cpp.obj` | Runtime debug/global-value header tables; broad runtime diagnostics pulled with the closure. | Retained |
| `EHHelpers.cpp.obj` | Exception helpers, module lookup, and fail-fast/P/Invoke paths; broad EH dependencies are unresolved. | Retained |
| `ExceptionHandling.asm.obj` | Low-level exception and throw helpers referenced by runtime transition/GC assembly. | Retained |
| `FinalizerHelpers.cpp.obj` | Finalization initialization and finalizer entry code; finalizer startup remains outside this bootstrap proof. | Retained |
| `gccommon.cpp.obj` | Common collector/runtime services and GC event state, a real transitive runtime dependency. | Retained |
| `gcconfig.cpp.obj` | GC configuration state. `/Gw` removed this member from the map; the unresolved set did not change. | Removed by `/Gw` |
| `gceewks.cpp.obj` | Workstation GC-to-execution-engine bridge, including thread/GC integration. | Retained |
| `gcenv.ee.cpp.obj` | GC execution-engine adapters; includes GC thread creation and process queries. | Retained |
| `gcenv.guidexos.cpp.obj` | GUIDEXOS GC PAL adapters for VM, monotonic time, thread identity, and fail-fast; several providers remain unresolved. | Retained |
| `gceventstatus.cpp.obj` | GC event-status bookkeeping used by collector synchronization. | Retained |
| `gchandletable.cpp.obj` | GC handle-table implementation and related runtime state. | Retained |
| `gcheaputilities.cpp.obj` | Heap initialization utilities; its `InitializeGC` closure requires `InitializeGCSelector`. | Retained |
| `GCHelpers.cpp.obj` | Runtime GC entry points/helpers; GC startup and collection are not authorized by this phase. | Retained |
| `GcProbe.asm.obj` | GC probe/safepoint helpers with exception dependencies. | Retained |
| `gcscan.cpp.obj` | Collector stack/object scanning. `/Gw` removed it from the map without changing unresolved symbols. | Removed by `/Gw` |
| `gctoclreventsink.cpp.obj` | GC-to-CLR event sink. `/Gw` removed it from the map without changing unresolved symbols. | Removed by `/Gw` |
| `gcwks.cpp.obj` | Workstation collector implementation, global heap state, and GC event initializers. The forced map still contains a 0x38-byte `.CRT$XCU` range with seven GC event initializers; this is real initialization state, not assumed dead code. | Retained |
| `handletablecore.cpp.obj` | Core handle-table support. `/Gw` removed it from the map; unresolved symbols remained unchanged. | Removed by `/Gw` |
| `MiscHelpers.cpp.obj` | Common process/tick/yield and stack-trace helpers; its closure includes process count, yield, tick, and stack-trace references. | Retained |
| `objecthandle.cpp.obj` | Object-handle implementation and global handle state. | Retained |
| `PalRedhawkStackBounds.cpp.obj` | Target-built P23R8 descriptor-to-PAL provider; the intended `Thread::Construct` path needs this real bounds provider. | Retained |
| `RestrictedCallouts.cpp.obj` | Restricted runtime callouts. `/Gw` removed it from the map without changing unresolved symbols. | Removed by `/Gw` |
| `RuntimeInstance.cpp.obj` | Genuine `RuntimeInstance` implementation, including `RuntimeInstance::Initialize`; this is an intended bootstrap dependency. | Retained |
| `softwarewritewatch.cpp.obj` | Software write-watch GC implementation. `/Gw` removed it from the map; this did not alter the unresolved set. | Removed by `/Gw` |
| `StackFrameIterator.cpp.obj` | Runtime stack walking used by GC/hijack callback code; its full implementation carries broader unwind/module needs. | Retained |
| `startup.cpp.obj` | General `RhInitialize` startup and runtime configuration; startup/PAL/CPU-feature services remain unresolved. | Retained |
| `stressLog.cpp.obj` | Stress-log timing and runtime diagnostics; performance-counter providers remain unresolved. | Retained |
| `StubDispatch.asm.obj` | General interface dispatch stubs and exception dependencies. | Retained |
| `thread.cpp.obj` | Remaining combined Thread methods. The standard failed link reports unresolved `PalSleep`, `PalHijack`, `PalCompatibleWaitAny`, and `PalPrintFatalError` from this member. | Retained |
| `threadstore.cpp.obj` | Remaining combined ThreadStore/GC suspension methods. The standard failed link still reports `PalSwitchToThread` from this member; the forced map does not retain it. | Not represented; still named by normal-link errors |
| `UniversalTransition.asm.obj` | Native/managed transition helpers with general exception dependencies. | Retained |
| `WriteBarriers.asm.obj` | GC write barriers that reference exception helpers; the production GC path remains gated. | Retained |
| `yieldprocessornormalized.cpp.obj` | Time/counter based yield normalization; performance counter and tick services remain unresolved. | Retained |

The six `/Gw` removals show that global-data packaging can eliminate map-retained members, but they did not eliminate any of the 48 unresolved names. The source split added `thread.bootstrap.cpp.obj` and `threadstore.bootstrap.cpp.obj`; it did not turn the standard link into a smaller, zero-unresolved target.

## Source partition and provenance

The reproducible delta is [`p23r13_bootstrap_source_partition.patch`](../Tools/Phase23/runtime-overlay/p23r13_bootstrap_source_partition.patch), SHA-256 `8ED0E2762C77936E7C851BE9F6515F455D237EE63FBDFD10620EFCD29559A5B0`. It applies after the existing P23R8 overlay on the pinned staging tree; `git apply --check --whitespace=error` passed against that tree. It was not applied to the protected staging checkout.

The patch adds the new source files to `CMakeLists.txt` and relocates, without rewriting, 29 Thread definitions and 13 ThreadStore definitions. The verifier compares normalized function bodies from the pinned staging source with the partitioned source, confirms that those definitions left the original combined source files, checks that the GUIDEXOS TLS has one definition in the bootstrap unit, and checks diagnostic-map object provenance:

```powershell
python .\Tools\Phase23\verify_bootstrap_source_partition.py `
  --baseline-runtime-root 'D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING\src\coreclr\nativeaot\Runtime' `
  --partitioned-runtime-root 'D:\dev\guideXOSUEFI_BUILD_INFRA\out\dotnet\phase23r13-runtime-partition-source\src\coreclr\nativeaot\Runtime' `
  --link-map 'D:\dev\guideXOSUEFI_BUILD_INFRA\out\dotnet\phase23r13-link-partitioning-source-split5\forced_link.map'
```

Result: 29 Thread bodies and 13 ThreadStore bodies matched, one GUIDEXOS TLS definition was found in the bootstrap source, and the map attributed `Thread::Construct` and `Thread::HijackCallback` to `thread.bootstrap.cpp.obj`, and `ThreadStore::Create` and `ThreadStore::AttachCurrentThread` to `threadstore.bootstrap.cpp.obj`. The same map attributes `RuntimeInstance::Initialize` to `RuntimeInstance.cpp.obj` and `PalGetMaximumStackBounds` to the target-built `PalRedhawkStackBounds.cpp.obj`. The verifier output is saved as `out\dotnet\phase23r13-source-partition-build\source-partition-verification.log` (SHA-256 `D9503B340BFCE73EAD3B8E20AACB0B7492FC7CFC916556BE9BF10EA590E5BACF`). This establishes source and object provenance only; the image that emitted the map was never run.

The function definitions retain their original code, calling conventions, conditions, assertions, and validation. Global ownership was moved with the corresponding code: `Thread::s_DeadThreadsNonAllocBytes` is defined in the Thread bootstrap unit; `RhpTrapThreads`, `RhpSuspendingThread`, the Windows TLS/DAC breadcrumb, and the GUIDEXOS `tls_CurrentThread` definition are in the ThreadStore bootstrap unit. Under MSVC/GUIDEXOS, `threadstore.inl` now declares `tls_CurrentThread` as external; the other platform branch keeps its original select-any behavior. The TLS type, storage class, and runtime layout are unchanged. No runtime constructor or static-initializer body was rewritten.

The rebuilt object SHA-256 values are:

| Object | SHA-256 |
|---|---|
| `RuntimeInstance.cpp.obj` | `2F85685B9A5A376DCC45280C632E6CBE1B03CBDC403CE500E2C53EE1711B7B6F` |
| `thread.cpp.obj` | `FF87A1C582F27A37CB7330B7FBAA95973F81D8920C0320ED3FAC3E4C3973ADDC` |
| `thread.bootstrap.cpp.obj` | `3C9FA9737C98477AD028F3EF843FE35026799921FF851C8E97014BA87BDCAEBF` |
| `threadstore.cpp.obj` | `8ED7DD84DE0F9FDBEB69A78B39ECDB31233B5BF07B9487EBA15B4A2B5A2BBCB1` |
| `threadstore.bootstrap.cpp.obj` | `2EAA43DBBDEE88EE4BD9DEC540BCFBCFD01EAC2061947C16757EF9B71B839283` |
| `Crst.cpp.obj` | `AB04845CAB55D17CBC97592150FF5CF3AEC9B4F34A62D8ACB5BD9BCF5D2D67DB` |
| `PalRedhawkStackBounds.cpp.obj` | `401850C258955E1EE53C91FD02C1B50B75033F54D40B8030E6AA25AECEBF3A26` |

The function-body verifier is not behavioral equivalence evidence. The hosted runtime path did not link, so ThreadStore membership, attachment, duplicate-attach behavior, and runtime cleanup were not observed.

## Unresolved symbols and service boundary

The P23R13 `/Gw` link and source-split link have the same 48 unique unresolved names:

```text
PalSleep
PalHijack
PalGetHijackTarget
PalCompatibleWaitAny
PalPrintFatalError
PalSwitchToThread
PalQueryPerformanceCounter
PalQueryPerformanceFrequency
RhExceptionHandling_FailedAllocation
RhThrowHwEx
RhThrowEx
RhRethrow
InitializeGCSelector
PalGetProcessCpuCount
PalGetTickCount64
RhpCalculateStackTraceWorker
RhpCidResolve
PalGetModuleBounds
PopulateControlSegmentRegisters
PalGetModuleFileName
PalAreShadowStacksEnabled
PalGetModuleHandleFromPointer
RhpFailFastForPInvokeExceptionPreemp
RhpFailFastForPInvokeExceptionCoop
PalCopyTCharAsChar
g_compilerEmbeddedSettingsBlob
g_compilerEmbeddedKnobsBlob
PalInit
InitializeGCEventLock
g_requiredCpuFeatures
PalStartBackgroundGCThread
PalStartFinalizerThread
PalCreateLowMemoryResourceNotification
ProcessFinalizers
guidexos_pal_thread_id
InitSupportedInstructionSet
IsSupportedInstructionSet
do_vxsort_avx2
do_vxsort_avx512
PalCreateEventW
guidexos_pal_vm_reserve
guidexos_pal_vm_commit
guidexos_pal_vm_release
guidexos_pal_monotonic_ticks
guidexos_pal_monotonic_frequency
guidexos_pal_fail_fast
PalVirtualAlloc
PalVirtualFree
```

The responsibilities are distinct:

- **Initial thread path:** the intended real implementation uses `RuntimeInstance::Initialize`, `ThreadStore::Create`, `ThreadStore::AttachCurrentThread`, `Thread::Construct`, the target stack-bounds provider, the ThreadStore lock, current-thread TLS, OS identity, and callback registration.
- **Required callback/platform contract:** `ThreadStore::Create` passes `Thread::HijackCallback` to `PalRegisterHijackCallback`. The callback uses `PalGetHijackTarget`; the pinned Windows PAL returns a shadow-stack-aware target when one is available, otherwise the default target. Its initialization path consults the shadow-stack policy and supports the special user-mode APC mechanism. The P23R13 host adapter has real Windows FLS association, OS thread identity, attach/detach failure handling, and one-time callback registration, but it does not implement hijack-target selection or actual later GC hijacking. It therefore cannot complete this required PAL contract.
- **Broader EH/module/runtime services:** module bounds/lookup, control-segment population, exception propagation, P/Invoke fail-fast, dispatch resolution, runtime startup, and generated compiler configuration belong to general NativeAOT execution and are not implemented by placeholders here.
- **GC/finalization services:** GC selector and CPU dispatch, GC virtual memory/time/thread/fail-fast providers, events, background GC, finalization startup, and the generated `ProcessFinalizers` callback remain real runtime prerequisites. The retained `.CRT$XCU` GC event initialization is evidence of global runtime state; it was not suppressed.
- **Process/configuration services:** tick/count/time, virtual memory, character conversion, and performance counters are used in broad runtime helper members. Some are ordinary host services, but implementing them would not remove the separate EH, GC, startup, and callback requirements.

No unresolved name was defined by an empty, no-op, dummy-module, or success-only implementation. The standard executable did not link. The forced diagnostic executable is invalid and must remain unexecuted.

## Hosted adapters and execution result

The existing link-probe host adapter uses Windows FLS for the thread association, `GetCurrentThreadId` for native identity, fail-closes invalid attach/detach operations, and supports deterministic callback-registration rejection. It stores the registered callback but does not claim to implement future GC hijacking. The narrow harness also allocates a Windows-backed stack and calls the production target descriptor/PAL functions before its intended `RuntimeInstance` and ThreadStore sequence.

The host adapter and probe compiled. The normal link failed, so none of the probe's setup or NativeAOT calls ran. There is no accepted bootstrap executable. `Thread::Construct` did not execute; ThreadStore attachment and current-thread lookup did not succeed or fail at runtime; no actual ThreadStore membership was observed. The target PAL was not consumed by a NativeAOT `Thread`. The map and symbol records prove only which object would supply each function.

No NativeAOT callback-registration failure injection, invalid runtime stack descriptor, out-of-range runtime SP, missing runtime bounds, NativeAOT allocation/TLS failure, duplicate runtime attachment, current-thread corruption, constructor cleanup, or stale runtime association test ran. The link-probe source contains a callback-registration rejection case, but it could not execute before link closure. No cleanup claim is made.

## Regression qualifications after partitioning

| Test category | Result | Scope and evidence |
|---|---:|---|
| Descriptor/PAL | **30/30** | Re-run against the source-partition build's target PAL object (`5BA72E6230488F91F1851EE04A5FCD5B9DECF7A3E5F46FB4E4E31DAABA7A97FA`). Synthetic EFI-map/page-table and mapping fixtures; not physical UEFI proof. Test executable SHA-256 `5D980D1837346A064E9D08914417B2050AAFBB06D6BCEC6D75C0B80BC8DE6C44`. Record: `out\dotnet\phase23r13-post-partition-stack-bounds-descriptor`. |
| Unsupported bounds | **4/4** | Fail-closed result and output clearing. Test executable SHA-256 `F1EA984FA46CD2307A8549BDAB8B3E326688D1DE954D3EE1836ABB2B29498246`. Record: `out\dotnet\phase23r13-post-partition-stack-bounds-unsupported`. |
| Hosted worker/fiber context | **20/20** | Real Windows worker identity, TLS, OS-backed worker/fiber stacks, retirement, and host-side activation/create failure handling. This is not NativeAOT finalizer execution. Executable SHA-256 `1B54C7DABAD252F0EFF3A9C22D7AB53C1215CAF43C6F1912A524B36F5C76EA8D`. Record: `out\dotnet\phase23r13-post-partition-hosted-context`. |
| Bounded x64 unwind | **69/69** | Hosted bounded evaluator and PE preflight. Executable SHA-256 `BCB6E6E7E207E6D283A38BE6C31694B797D768A213E5D7891D5996FF9B2C6CBD`. |
| Windows-reference unwind | **69/69**, including **20/20** reference cases | Windows `RtlVirtualUnwind` reference comparisons only. Executable SHA-256 `BBEA496F5E1A1E9C43A68C140A679E2B5ECA22210A9D6125B740F65F71ECB372`. |

The bounded and Windows-reference runners both completed successfully after the partition. These regressions establish source/build continuity for the independent stack/unwind contracts, not behavior of the new Thread and ThreadStore objects. No NativeAOT thread failure injection or attachment test passed because no authentic test executable linked.

## Safety boundary and next phase

Phase 23 remains **blocked**. Keep full `RhInitialize`, GC collection, finalizer processing, managed execution, production COFF-manager publication, `RhRegisterOSModule`, unsafe EH propagation, foreign-thread stack walking, and scheduler-to-NativeAOT attachment disabled or gated. Do not generate Phase 29–35 identities or production images until Phase 23 passes its supported proof and verification requirements.

**P23R14 objective:** build a dependency-closed `Runtime.BootstrapThread` target from the unchanged source bodies, then make the standard link—not a forced diagnostic link—report zero unresolved symbols. First isolate the retained runtime startup/GC-event initialization and the old `thread.cpp`/`threadstore.cpp` error-producing members without dropping required static state. Implement the GUIDEXOS host/target hijack callback and target-selection contract with explicit CET/shadow-stack handling, or fail closed before `ThreadStore::Create` if that service cannot be supported. Keep module publication, GC execution, EH propagation, and managed code beyond the supported initialization boundary gated. Only after a zero-unresolved standard link should P23R14 run construction, attachment, current-thread lookup, and isolated failure/cleanup cases.

No production kernel or image was changed. No Phase 29–35 artifacts were generated. No QEMU, QAC admission, physical hardware, or push was used.
