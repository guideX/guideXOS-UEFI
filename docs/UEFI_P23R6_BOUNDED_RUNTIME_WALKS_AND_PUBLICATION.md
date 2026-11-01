# P23R6 — Bounded Runtime Walks and Publication

**Outcome B — `P23R6_RUNTIME_SAFETY_PARTIAL`.** P23R6 adds a freestanding, status-bearing x64 frame-walk adapter that binds unwind reads to the identity and stack bounds of the thread being walked. It reuses the P23R3 bounded evaluator and passes new hosted fault cases. The adapter is not yet wired into NativeAOT's `ICodeManager` calls. GC/EH failure propagation, target COFF manager integration, and safe manager publication remain open, so production registration and Phase 23 remain disabled.

## Repository and preservation

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`
- Branch: `BUILD_INFRA`
- Starting HEAD: `25076262e66ac9d612f36a774f58ab0fd0f80291`
- Ending HEAD: the local commit containing this report; its exact SHA is in the final execution summary because a commit cannot contain its own ID.
- The root worktree was clean at task start. The only intended changes are this report, `Tools/Phase23/bounded_runtime_walk.h`, and the bounded unwind test source.
- Pinned runtime `out\rt` remained at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, detached, with its 65 pre-existing status entries (54 modified tracked files and 11 untracked entries). The SHA-256 of `git status --porcelain=v1 --untracked-files=all` joined with LF was `59610A7C32254E178B563A23AA2C4D5E0389FCF9873E1ED2CDBF21B76B724DF9` both before and after. No files in that checkout were changed, cleaned, reset, or overwritten.
- No branch switch, merge, rebase, reset, cherry-pick, push, or change to MODERN_INPUT, APP_MODEL, or later-phase shim copies occurred.

## Walked-thread stack provenance

The pinned NativeAOT `Runtime::Thread` stores per-thread `m_pStackLow` and `m_pStackHigh` in `RuntimeThreadLocals` (`out\rt\src\coreclr\nativeaot\Runtime\thread.h:109–110`). Its constructor calls `PalGetMaximumStackBounds` and fail-fasts if the PAL cannot provide bounds (`thread.cpp:287`); `Thread::GetStackBounds` returns those stored values (`thread.inl:31–35`). `StackFrameIterator` retains the `Thread*` passed for the walk (`StackFrameIterator.h`, `m_pThread`). This is the authoritative source for a foreign-thread walk; the current executor's bounds cannot be substituted.

The pinned PAL implementations obtain the range from the OS thread allocation: Windows uses the stack allocation base and TEB `StackBase` (`Runtime/windows/PalRedhawkCommon.cpp`); Unix uses pthread stack attributes (`Runtime/unix/PalRedhawkUnix.cpp`). The NativeAOT thread object is initialized for the attached OS thread, so the stored range follows that thread through worker-thread walks. Transition frames on the same native stack retain the same allocation bounds.

P23R6's adapter requires the caller to pass the walked thread identity, allocation bounds, and a separately verified accessible interval. It validates non-null, ordered, 8-byte-aligned bounds, mapping coverage, and that current `RSP` belongs to the accessible half-open interval. It creates a view starting at current `RSP` and ending at `AccessibleHigh`, so every evaluator stack read is checked against verified accessible memory. A foreign executor is allowed when the bounds owner matches the walked thread; bounds tagged for the executor are rejected. An alternate-stack context or transition to a separate stack falls outside this contract and is rejected by the range check.

**Integration limit:** the pinned runtime currently stores maximum allocation bounds, not a separately verified committed-memory interval. `Thread::GetStackBounds` is not passed into `CoffNativeCodeManager`, and no current runtime API supplies the adapter's required `AccessibleLow`/`AccessibleHigh` pair for a foreign thread. The adapter's explicit identity contract is hosted and target-shareable code, not proof that the current runtime call graph supplies the correct `Thread*` or accessible interval. The hosted current/foreign tests use distinct thread identity tokens over byte-array stack views; they do not create or suspend real OS threads. The adapter therefore cannot yet be called safely by production code. The runtime source also lacks a verified alternate-stack bound path to the manager.

## Status-bearing unwind interface

`Tools/Phase23/bounded_runtime_walk.h` introduces `guidexos::runtime_walk::UnwindFrame`. Its request includes the current register state and saved-register locations, image and executable-code views, runtime-function entry, walked-thread identity, stack bounds, output caller state, and establisher-frame result. It invokes the existing `guidexos::unwind::UnwindFrame` evaluator rather than rewriting it.

The adapter returns distinct outcomes for invalid frame, unavailable or invalid bounds, wrong-thread bounds, stack access outside the permitted interval, malformed metadata, unsupported unwind encoding, unsupported epilogue, invalid executable address, arithmetic overflow, and invalid restored context. It stages registers and saved-register locations locally, validates the restored `RSP`/`RIP`, and only then writes the caller state and result. Failure leaves both outputs unchanged.

The caller must pass image/code bounds already validated for the mapped target image. The P23R5 PE preflight remains the initial PE validation helper, but neither it nor this adapter is wired into runtime registration or `CoffNativeCodeManager` in this phase. In particular, this work does not establish loader mapping extent or validate GC/EH metadata. Hosted tests use a byte array whose entire declared accessible interval is mapped; they do not establish a PAL source for committed bounds on target threads.

## GC and EH failure handling

No production GC or EH consumer interface was changed.

- `ICodeManager::EnumGcRefs` remains `void` (`Runtime/ICodeManager.h:258–263`). `GcEnum.cpp` forwards the consumer call without a status channel, and the pinned COFF manager's GC decoder cannot report an ordinary failure to the collector. No fail-stop adapter was added. The runtime still needs an explicit terminal policy before an unsafe collection can continue.
- `EHEnumInit` and `EHEnumNext` remain `bool` (`ICodeManager.h:281–283`). In the pinned COFF manager, `false` still conflates no EH data/end-of-enumeration with malformed metadata; its EH varint and type-RVA reads remain unbounded. P23R6 did not add a bounded EH parser or claim error/exhaustion separation.
- The authentic hosted consumer regression still verifies successful behavior, but deliberately malformed unwind/GC/EH failure was not injected through that genuine manager. The new negative unwind cases exercise the bounded adapter only.

## Manager preparation and publication

P23R6 did not add a runtime publication API or rollback behavior. The P23R5 `PrepareRegistration` helper still performs bounded PE staging and deliberately leaves `ManagerConstructionAllowed` and `PublicationAllowed` false. It does not validate the full GC/EH metadata closure, prepare the genuine manager, or reserve all auxiliary allocations.

Pinned `RuntimeInstance::RegisterCodeManager` still asserts and assigns ordinary fields, while `RegisterUnboxingStubs` allocates and publishes a list node afterward. There is no rollback if the second step fails and no reader synchronization contract. No registration fault matrix was run because the current API can leave a visible dangling manager. Production `RhRegisterOSModule` remains unresolved/disabled; no placeholder export or unsafe publication was added.

## Target COFF manager evidence

No source overlay was applied to `out\rt`, and no target-compatible runtime archive was rebuilt. The retained GUIDEXOS target archive is:

`out\dotnet\phase23r4-gc-eh-fixture-reproduction\nuget-cache\runtime.guidexos-x64.microsoft.dotnet.ilcompiler\9.0.0\sdk\Runtime.WorkstationGC.lib`

Its SHA-256 remains `E486351F470C4562C217A24938BC070EBD8AE112CF6D7EF97770CA6A2056E50F`. `lib /list` and `dumpbin /linkermember:1` show `RhpRegisterOsModule`; they show neither `RhRegisterOSModule` nor `CoffNativeCodeManager`. The retained host archive is the genuine Windows manager used by the consumer regression, but it is not target-archive evidence.

The new adapter header is target-shareable C++17 code, but it is not compiled into the target archive or called by the genuine manager. Target-compatible COFF integration therefore remains unproven.

## Hosted behavioral results

`Tests/NativeAotUnwind/run_x64_unwind_tests.ps1` passed both hosted executables:

- **69/69 bounded-only tests passed.** This retains all 59 P23R5 cases and adds 10 status-bearing walk cases.
- **69/69 Windows-reference tests passed**, including the retained 20 `RtlVirtualUnwind` comparisons.
- New cases cover current and foreign walked-thread bounds, wrong-thread bound rejection, nested frames, unavailable/invalid bounds, stack underflow/overflow, malformed and unsupported metadata, unsupported epilogue, invalid save location, invalid executable address, arithmetic overflow, restored-context validation, and no partial output on failure.
- Final hosted executables were written under `%TEMP%\guideXOS_P23R3_unwind_c6d6d1dcae6e40b88bed2c19ea700aba`.

`Tools/Phase23/run_gc_eh_consumer_tests.ps1` also passed against the preserved authentic fixture and genuine host `CoffNativeCodeManager` archive (archive SHA-256 `0E6A134AD4150CD604317A47860DAE82EB30AAE4D9CDB14144E06454E7BB1948`):

- Authentic GC info reported 7 roots at `Outer+0x19`, 6 at `Outer+0x23`, and 1 at `Inner+0x10`.
- All six callback-provided writable root slots were updated and read back; the same register display unwound `Inner` to `Outer` and enumerated both frames.
- One typed EH clause was decoded, and one of the manager's 470 funclets was associated with `CatchFixture`.
- This is successful root-location and EH regression evidence. It is not a compacting GC, actual managed execution, status-bearing failure path, or target manager publication test.
- Final hosted consumer output is under `%TEMP%\guideXOS_P23R6_gc_eh_final_6fa59381ada64dc6acb457b0e79d2920`; the executable SHA-256 is `F315E0AB24E16E91CAC3BB321E404CD47443A22CDFA162515F91862029A94791`.

## Remaining boundaries and proof status

- **R13:** the authentic fixture still has no compiler-generated live R13 GC root. The retained R13 test is a diagnostic unwind-record overlay with authentic GC-info bytes; it is not authentic R13 root coverage.
- **Transition frames:** ordinary same-stack nested frames are covered by the bounded adapter. Reverse-P/Invoke and separate-stack/alternate-stack transitions are not; callers must reject those until per-transition bounds and status propagation are designed.
- **GC/EH failure injection:** not passed through the genuine consumer; no safe runtime failure channel has been integrated.
- **Manager preparation/publication:** no target manager was prepared or published; no allocation-stage, duplicate, overlap, or concurrent-reader fault injection was run.
- **`RhRegisterOSModule`:** absent from the genuine GUIDEXOS target archive. The production registration gate remains failing.
- **Phase 23 proof:** not passed.
- **Phase 29–35 generation:** not run or modified.
- **Production kernel/image:** not built.
- **QEMU, QAC admission, and physical hardware:** not run.
- **Push:** none.

## Exact next development frontier — P23R7

Integrate a per-walk context in the pinned runtime so `StackFrameIterator` supplies its actual `Thread*` and `GetStackBounds` to the target COFF manager without changing the external `ICodeManager` ABI. Build the genuine manager in an isolated GUIDEXOS runtime overlay with the bounded evaluator on the unwind path. Then give GC a status-bearing internal boundary with a deterministic fail-stop on unsafe enumeration, give EH distinct clause/exhaustion/error results, and stage manager plus unboxing/auxiliary allocations before any reader-visible publication. Prove each path and each pre-publication failure through the actual target-compatible implementation before enabling `RhRegisterOSModule` or attempting the Phase 23 proof.
