# P23R6 — Bounded Runtime Walks and Publication

**Outcome B — `P23R6_RUNTIME_SAFETY_PARTIAL`.** Hosted bounded-unwind behavior and authentic host COFF GC/EH consumer behavior pass, including injected GC callback and root-location failures. An isolated GUIDEXOS runtime build compiles the walked-thread bounds checks into `Runtime.WorkstationGC.lib`. The runtime still has no selected target COFF manager, no verified GUIDEXOS stack-bounds PAL provider, no integrated GC/EH error channel, and no transactional manager publication. Production registration and Phase 23 remain disabled.

## Repository and preservation

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`, branch `BUILD_INFRA`.
- Assignment starting HEAD: `25076262e66ac9d612f36a774f58ab0fd0f80291`.
- An initial local P23R6 commit, `2753d9672cb3432a34bbfcd0e40cb7bca841c36c`, contains the bounded unwind adapter and its first 10 hosted cases. This report and the final changes are committed on top of it; the resulting HEAD is given in the final execution summary.
- The root worktree was clean at assignment start. P23R6 changes are limited to this report, hosted adapter/tests/reporting, and the isolated runtime patch described below. No changes were made to `MODERN_INPUT`, `APP_MODEL`, or later-phase shim copies.
- The original `out\rt` remains detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. Its original status remains 65 entries: 54 modified tracked files and 11 untracked files. SHA-256 of the sorted `git status --porcelain=v1 --untracked-files=all` lines joined by LF is `59610A7C32254E178B563A23AA2C4D5E0389FCF9873E1ED2CDBF21B76B724DF9`, unchanged at completion. No file in that checkout was cleaned, reset, or overwritten.
- Runtime edits were built in a detached staging checkout at `D:\dev\guideXOSUEFI_P23R6_RUNTIME_STAGING_LONG`, based on a copy of the preserved P23R5 `out\rt` snapshot. The reproducible six-file source delta is [p23r6_runtime.patch](../Tools/Phase23/runtime-overlay/p23r6_runtime.patch). Forward application checks against `out\rt` and reverse checks against the staging checkout both passed with `git apply --check --unidiff-zero -p1`. The patch is intended for an isolated copy of that exact P23R5 source snapshot, not for the preserved `out\rt` itself.
- No merge, reset, rebase, cherry-pick, branch switch, push, QEMU run, QAC admission, physical hardware test, Phase 29–35 generation, or production image build occurred.

## Files changed

The first local P23R6 commit adds `Tools/Phase23/bounded_runtime_walk.h` and the first 10 cases in `Tests/NativeAotUnwind/x64_unwind_tests.cpp`. This final change set adds:

- `Tools/Phase23/gc_enum_status_adapter.h` — hosted status/fail-stop boundary for the legacy `void` GC callback prefix.
- `Tests/NativeAotUnwind/code_manager_consumer_tests.cpp` — authentic host-manager failure injection and guarded root-slot checks.
- `Tools/Phase23/run_gc_eh_consumer_tests.ps1` — explicit JSON fields for the guarded GC failures and the unverified target integration.
- `Tools/Phase23/runtime-overlay/p23r6_runtime.patch` — isolated target source changes across `eng/Subsets.props`, `src/coreclr/CMakeLists.txt`, `src/coreclr/gc/vxsort/isa_detection.cpp`, `src/coreclr/nativeaot/Runtime/regdisplay.h`, `src/coreclr/nativeaot/Runtime/StackFrameIterator.cpp`, and `src/coreclr/nativeaot/Runtime/amd64/AsmOffsetsCpu.h`.

## Walked-thread stack provenance and runtime checks

In the pinned NativeAOT source, `Thread::Construct` calls `PalGetMaximumStackBounds` and fail-fasts if the call returns false (`src/coreclr/nativeaot/Runtime/thread.cpp`). The returned low/high values are stored on that `Thread`; `Thread::GetStackBounds` reads those stored fields (`thread.h`, `thread.inl`). The stack iterator is initialized with the `Thread*` being walked, and the isolated patch copies that thread identity and its low/high values into the target `REGDISPLAY`. This avoids substituting the executing thread's bounds when a foreign thread is walked.

The patch validates owner identity, nonzero ordered bounds, and aligned `RSP` before the manager call. After a successful frame step, it verifies that the manager preserved the owner and bounds and returned an aligned `RSP` inside the same half-open interval. Any violation fail-fasts before the iterator exposes the caller frame. The P/Invoke initialization path restores these fields after clearing `REGDISPLAY`. The larger target `REGDISPLAY` also changes assembler-checked layouts: `REGDISPLAY` size `0x150`, `StackFrameIterator` size `0x250`, `m_OriginalControlPC` at `0x240`, `m_pPreviousTransitionFrame` at `0x248`, `ExInfo` size `0x280`, and `m_notifyDebuggerSP` at `0x270`. `AsmOffsetsVerify.cpp` compiled successfully in the target workstation library build.

**Target provenance limit:** the pinned source has Windows and Unix implementations of `PalGetMaximumStackBounds`, but no GUIDEXOS implementation. `dumpbin /symbols` on the target workstation archive still reports `PalGetMaximumStackBounds` as an undefined external. A platform implementation may be supplied by a separately linked PAL, but this task did not verify one. The added iterator checks therefore compile into the target archive but do not prove that a target thread receives valid bounds. The current values describe maximum allocation bounds; the iterator does not verify page commitment/accessibility, bound every code-manager stack read, validate saved-root slots, or support alternate stacks. Bootstrap, worker, suspended-thread, and transition-frame target behavior was not executed.

## Status-bearing unwind behavior

`Tools/Phase23/bounded_runtime_walk.h` reuses the existing P23R3 x64 evaluator. Its request carries image/code ranges, a frame context, saved-register locations, walked-thread identity, stack bounds, and separate accessible-memory mapping bounds. It returns distinct failures for invalid frames, unavailable/invalid/wrong-thread bounds, out-of-range stack access, malformed metadata, unsupported encodings/epilogues, invalid code addresses, arithmetic overflow, and invalid restored context. It stages the caller context and register locations and commits them only after all checks pass.

Hosted tests exercise current-thread and foreign-thread identities, nested frames, bad bounds, underflow/overflow, malformed metadata, unsupported forms, invalid save locations, and failure atomicity. **The evaluator is not wired into `StackFrameIterator` or a target code manager.** The runtime patch adds bounds checks around the existing `ICodeManager::UnwindStackFrame` call; it does not make that legacy manager unwind bounded or transactional.

## GC and EH failure handling

The pinned `ICodeManager::EnumGcRefs` and `GCEnumContext` production path remains unchanged. The new `gc_enum_status_adapter.h` is a hosted adapter that preserves the legacy callback prefix, validates root-slot addresses against permitted ranges, latches invalid-root/callback/unwind status, and suppresses later callback forwarding after failure. The authentic host manager is called through this guard. Fault injection rejects a callback root and an invalid root location; a failed frame unwind blocks a later frame enumeration. This verifies the adapter boundary with authentic root production, but `targetRuntimeIntegration` is explicitly `false`: the adapter is not compiled into target `GcEnum.cpp` or the target COFF manager. A target collection still has no integrated status-bearing fail-stop policy for malformed GC metadata or lost roots.

The pinned EH virtuals remain `bool`. The authentic host fixture successfully returns one typed clause and associates a catch funclet, but normal end-of-enumeration is still not distinguished from malformed handler data, invalid associations, or failed unwind. No EH failure injection or bounded EH parser was added.

## Manager preparation and publication

P23R6 does not add manager preparation or publication code. P23R5's PE preflight still produces a staging description only; it leaves manager construction and publication disabled. Pinned `RuntimeInstance::RegisterCodeManager` still writes ordinary manager/range fields, followed by the fallible unboxing-stub allocation/publication. There is no rollback after the manager becomes visible and no established synchronization contract for readers. No allocation-stage, duplicate, overlap, or concurrent-reader failure matrix was run. No unsafe publication was added.

## Target runtime build and COFF manager evidence

The isolated target build used the pinned runtime plus the preserved P23R5 source overlay, VS 18 Community's MSVC 19.51, and a `VisualStudioVersion=17.0` compatibility override because the pinned build scripts accept the VS 2022 toolset label. The full `eng/build.ps1 -subset Clr.NativeAotRuntime -os guidexos -arch x64` attempt compiled and linked `Runtime.WorkstationGC.lib`, then failed in the separate ServerGC target because `gcsvr.cpp` references unavailable `GCEventFireSizeAdaptationTuning_V1`, `GCEventFireSizeAdaptationFullGCTuning_V1`, and `GCEventFireSizeAdaptationSample_V1` functions. A focused `cmake --build ... --target Runtime.WorkstationGC --config Release` then completed successfully after the final iterator change.

The resulting target-compatible archive is `D:\dev\guideXOSUEFI_P23R6_RUNTIME_STAGING_LONG\artifacts\obj\coreclr\guidexos.x64.Release\nativeaot\Runtime\Full\Runtime.WorkstationGC.lib`, SHA-256 `5E2552B0ED3B69B26F2EED429CD2200A8265B1BF991CF350A7B5E1488E984FAA`. Its member list includes `GcEnum.cpp`, `RuntimeInstance.cpp`, and the changed `StackFrameIterator.cpp`. It has no `CoffNativeCodeManager` or `RhRegisterOSModule` symbol/member. The genuine COFF manager source remains excluded by the GUIDEXOS CMake branch. The archive is runtime-library evidence only; it is not a final target link or image.

The preserved genuine host `Runtime.WorkstationGC.lib` used by the consumer tests is a separate Windows archive. Host archive evidence is not target archive evidence. `RhRegisterOSModule` remains absent from the GUIDEXOS archive; no placeholder export was added. `RhpRegisterOsModule` remains only the separate OS-module-list helper and does not register a code manager.

## Hosted behavioral results

`Tests/NativeAotUnwind/run_x64_unwind_tests.ps1` passed both hosted executables:

- **69/69 bounded-only cases passed.** This retains all 59 P23R5 cases plus 10 status-bearing walk cases.
- **69/69 Windows-reference cases passed**, including 20 `RtlVirtualUnwind` comparisons.
- The new cases cover foreign-thread bound ownership, nested frames, unavailable/invalid bounds, stack underflow/overflow, malformed/unsupported metadata, unsupported epilogues, invalid saved-register locations, invalid executable addresses, arithmetic overflow, restored-context validation, and no partial output on failure.

`Tools/Phase23/run_gc_eh_consumer_tests.ps1` passed against the authentic pinned Windows `CoffNativeCodeManager` in the host `runtime.win-x64` NativeAOT archive (SHA-256 `0E6A134AD4150CD604317A47860DAE82EB30AAE4D9CDB14144E06454E7BB1948`) and preserved fixture (SHA-256 `D9EDE8AFC32D73504D718A9A752C921FE8E2294B0627B1CF276E846A64878DCD`):

- Archive: `D:\dev\guideXOSUEFI_BUILD_INFRA\out\dotnet\phase23r4-gc-eh-fixture-reproduction\nuget-cache\runtime.win-x64.microsoft.dotnet.ilcompiler\9.0.0\sdk\Runtime.WorkstationGC.lib`.
- Fixture: `D:\dev\guideXOSUEFI_BUILD_INFRA\out\dotnet\phase23r4-gc-eh-fixture-reproduction\publish\guideXOS.NativeAotGcEhFixture.exe`.

- Authentic GC info enumerated 7 roots at `Outer+0x19`, 6 at `Outer+0x23`, and 1 at `Inner+0x10`.
- All six writable root slots were updated and read back. The multi-frame `Inner` to `Outer` walk succeeded.
- Guard output reports `callback_failure=1`, `invalid_root=1`, and `unwind_failure_blocks_next_frame=1`; the authentic manager was called. The JSON truthfully reports `targetRuntimeIntegration=false`.
- One typed EH clause and one association among 470 funclets were validated. Full exception propagation remains false.
- The R13 probe is synthetic unwind-record overlay evidence: it recovers a value and selects a writable stack-save location while retaining authentic GC-info bytes. No compiler-generated live R13 root is present in the fixture.
- The latest hosted executables and JSON/log evidence are under `%TEMP%\guideXOS_P23R3_unwind_e2895568cad949f0b946205290c9b872` and `%TEMP%\guideXOS_P23R6_gc_eh_final_aa58b48363fe462a961630e504788a3c`.

These are hosted adapter and host-manager consumer results. They do not establish target manager execution, managed execution, compacting collection, target EH failure behavior, or manager publication.

## Remaining boundaries and Phase 23 proof

- **R13:** still a diagnostic synthetic unwind-record overlay, not an authentic compiler-generated R13 GC root.
- **Transition frames:** only the iterator's per-thread bounds propagation is staged; no reverse-P/Invoke or alternate-stack behavior was executed. The bounded hosted adapter rejects contexts outside its supplied accessible interval.
- **GC/EH:** hosted GC guard failures are verified, but target GC status handling and EH error/exhaustion distinction remain unimplemented.
- **Publication:** manager construction, atomic publication, rollback, and publication fault injection remain unimplemented.
- **Registration:** the genuine target COFF manager and `RhRegisterOSModule` remain absent. Production registration is disabled.
- **Phase 23 proof:** not passed. Phase 29–35 generation, production kernel/image build, QEMU, QAC admission, physical hardware testing, and push were not performed.

## Exact next development frontier — P23R7

First provide and verify a GUIDEXOS PAL stack-bounds implementation backed by the target thread/stack allocator, including an accessible-memory limit for foreign walks. Then compile the genuine COFF manager into the target only after its image and metadata extents can be validated, and route AMD64 unwinding through the bounded evaluator with transactional context updates. Add internal GC status reporting with fail-stop before unsafe collection can resume, distinguish EH clause/exhaustion/error outcomes, then prepare manager and unboxing/auxiliary data completely before a single reader-safe publication. Test each negative path through the target-compatible consumers before enabling `RhRegisterOSModule` or attempting Phase 23 proof.
