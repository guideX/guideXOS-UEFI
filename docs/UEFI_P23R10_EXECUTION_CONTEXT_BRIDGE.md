# P23R10 — Scheduler/NativeAOT Execution-Context Bridge

**Outcome C — `P23R10_THREAD_INTEGRATION_BLOCKED`.** The bootstrap PAL contract remains valid and its hosted regressions pass, but the checked-in UEFI entry path does not execute the pinned NativeAOT initialization sequence. A hosted executable could not be linked from the WorkstationGC archive without inventing missing runtime/module providers. The standard `RhInitialize` path also starts a finalizer thread that needs a separately validated stack and a real thread-start service. No scheduler-to-ThreadStore bridge was published.

## Repository protection and build provenance

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`; branch: `BUILD_INFRA`.
- Starting HEAD: `cc58e5e8bab42b142271271c4fdfd9de72fcd417`.
- Ending HEAD: the local commit containing this report. Its exact SHA is given in the final execution summary because the commit cannot contain its own object ID.
- At start, the worktree was clean and `BUILD_INFRA` was two commits ahead and zero behind `origin/BUILD_INFRA`. No branch switch, merge, rebase, reset, cherry-pick, or push occurred.
- The protected `out\rt` checkout remained at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; its 65 full porcelain entries exactly match the saved P23R9 status (`7D48B6B71F2B7B53794738B6974A8D611E51F0780D757650D648B8239C0B35A1`). No file in `out\rt` was changed.
- Runtime source was read from `D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING`; it remained at the pinned revision with its pre-existing staged modifications. The P23R8 overlay passes `git apply --reverse --check --unidiff-zero`. No runtime source file was edited.
- All new build and test outputs were isolated beneath `out\dotnet\phase23r10-*`. MODERN_INPUT, APP_MODEL, protected mainline, and unrelated checkouts were not modified.

## Minimum threading requirement and pinned call graph

The pinned runtime is `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`.

`ThreadStore::AttachCurrentThread` obtains the calling native thread's `tls_CurrentThread`. If it is not already initialized, the runtime calls `PalAttachThread`, runs the real `Thread::Construct`, and only then inserts that object into the ThreadStore under its lock. `Thread::Construct` records `PalGetCurrentOSThreadId`, obtains native process/thread pseudo-handles, attempts `PalDuplicateHandle` (the duplicate may remain invalid), and calls `PalGetMaximumStackBounds`. Failure to obtain bounds calls `RhFailFast`. It does not ask the kernel scheduler for a task ID, stack, or saved context.

The supported C++ startup path is `RhInitialize` → `PalInit` → `InitDLL` → `RuntimeInstance::Initialize`/`ThreadStore::Create` → `InitializeGC`. `InitializeGC` calls `RhInitializeFinalization`; that starts `FinalizerStart` through `PalStartFinalizerThread`. The finalizer callback calls `ThreadStore::AttachCurrentThread` on the new native thread. Its stack must therefore have its own valid bounds. This is one runtime worker requirement, not a requirement for a complete pool of managed worker threads.

The guideXOS path currently differs: `KMainWrapper` in `guideXOS/native_stubs.asm` registers the P23R8 bootstrap descriptor and jumps directly to `KMain`. `Kernel/Misc/EntryPoint.cs` calls `StartupCodeHelpers.InitializeModules`. The pinned helper calls `RhpRegisterOsModule` and creates type managers; it does not call `RhInitialize` or `RhRegisterOSModule`. The pinned `RhpRegisterOsModule` accesses `GetRuntimeInstance()`, which is established by `RuntimeInstance::Initialize`. There is no source-level call to that initialization before the current `InitializeModules` call. The standard `Bootstrap/main.cpp` sequence that performs `RhInitialize`, `RhRegisterOSModule`, and `InitializeModules` is bypassed. The PAL descriptor by itself does not create a NativeAOT RuntimeInstance, ThreadStore, or attached current thread.

This resolves the bootstrap question narrowly: `Thread::Construct` itself does not require scheduler integration, and the P23R8 PAL can provide the bootstrap stack bounds when it is called on that stack. The current guideXOS startup does not reach a supported ThreadStore attachment sequence. The standard pinned `RhInitialize` path needs a second attached finalizer execution context before it completes. That context needs a real thread-start service and validated stack ownership; it need not imply a general managed scheduler or arbitrary suspended-thread walking.

### Dependency matrix

| Service or contract | Bootstrap ThreadStore construction | Phase 23 proof | Production single-threaded managed execution | Production multithreaded managed execution | GC suspension / foreign-thread walking |
|---|---|---|---|---|---|
| `RuntimeInstance` and ThreadStore initialized in supported order | Required | Required | Required | Required | Required |
| Current native-thread ID, `PalAttachThread`, ThreadStore TLS, and valid current-stack bounds | Required | Required for bootstrap attachment | Required | Required for each attached native thread | Required for every selected target |
| Pseudo-handles and `PalDuplicateHandle` | Construct uses them; duplicate failure is tolerated | Same | Same | Same | Thread handle must support the actual suspension mechanism |
| `FEATURE_HIJACK` callback registration | Required by this build's `ThreadStore::Create` | Required if the same runtime configuration is used | Required by this configuration | Required by this configuration | Actual hijack/suspend operations and saved contexts are additionally required |
| A finalizer worker with a separately validated stack | Not required by `Thread::Construct` alone | Required by the pinned standard `RhInitialize`/GC startup path | Required by the standard runtime startup path | Required | Required if the worker is included in suspension/walking |
| Per-CPU scheduler-to-ThreadStore association | Not required for a bootstrap-only attach | Not required if the proof remains on the bootstrap execution context; needed if managed work is switched among scheduler tasks | Not required for one native execution context | Required when scheduler tasks are distinct managed execution contexts | Required for target selection and current-context restoration |
| EFI backing proof, reuse-safe identity, and stack pin/deferred reclamation | Not required for the permanent P23R8 bootstrap allocation | Required only if the proof selects non-bootstrap scheduler stacks | Required for any managed execution on reclaimable scheduler stacks | Required for each selected scheduler stack | Required before inspecting any reclaimable foreign stack |
| Real suspension/acknowledgement and saved target context | Not required for construction | Required only if the proof walks a non-current thread | Not required for ordinary execution | Required for stop-the-world GC over multiple running contexts | Required |
| Real module registration, GC/EH consumers, and code manager | Not required for the isolated `Thread::Construct` algorithm | Required for a full GC/EH proof | Required for normal managed code and exception handling | Required | Required for stack walking and root reporting |

`Thread::Construct` initializes the native runtime's TLS-backed `Thread` record. Runtime thread-local/static state becomes usable through that record and subsequent runtime initialization; a kernel `Thread` object is not that TLS record. `PalGetMaximumStackBounds` reports the caller's stack only and cannot select another suspended target.

## Bootstrap `Thread::Construct` qualification

**`Thread::Construct` did not execute.** The target WorkstationGC archive was built, but a hosted link probe against it failed with 38 unresolved externals. The missing names span runtime-generated EH/dispatch/stack-trace providers, GC selectors and CPU-feature helpers, managed `ProcessFinalizers`, and remaining PAL imports. The runtime archive is not a complete NativeAOT executable or standalone ThreadStore fixture. Filling these with no-op or success stubs would invalidate the test and violate the runtime safety gates. No such providers were committed.

The next blocker after a complete link is also explicit in the call graph: the standard `RhInitialize` path starts the finalizer worker, while the target P23R8 PAL has only the immutable bootstrap descriptor. It correctly rejects a different worker stack. No thread-start adapter or validated second stack was available to satisfy that contract.

The hosted descriptor/PAL regression did consume the production target PAL interface and return the descriptor's exact interval. Its descriptor used the production loader builder but a synthetic EFI memory-map entry and simulated identity page tables around hosted memory. It did **not** consume a descriptor captured from a UEFI handoff, prove physical page provenance, or call `Thread::Construct`. Therefore authoritative UEFI bootstrap bounds were not consumed by a NativeAOT runtime thread in this phase.

## Scheduler identity and CPU-local selection

The required identities remain distinct:

1. A kernel scheduler task is the `guideXOS.Misc.Thread` object.
2. Its stack is an `Allocator` allocation owned and reclaimed by the kernel.
3. Current scheduler selection is made through `ThreadPool.CurrentThread` and `Indexs[SMP.ThisCPU]`.
4. NativeAOT current-thread identity is the runtime `Thread` reached through native-thread TLS (`tls_CurrentThread`) and the PAL's OS-thread ID.
5. Managed attachment is the successful `PalAttachThread` + `Thread::Construct` + ThreadStore insertion sequence.
6. A suspended target is an explicitly selected runtime thread plus its saved native context and stack lifetime; it cannot be inferred from the current scheduler slot.

P23R9's scheduler inspection remains applicable: timer context switches save an interrupt frame, select another scheduler task, and update TSS RSP0/CR3. They do not save or restore `tls_CurrentThread`, NativeAOT allocation state, managed thread statics, or transition-frame state. No scheduler ID/generation is available, and no NativeAOT association is published after attachment. No A → B → A scheduler/runtime switch test ran. No second scheduler was introduced.

The initialized UEFI path has a one-entry CPU index array indexed by the APIC ID, while AP startup/per-CPU TSS and current-context setup are incomplete. The current accessor is not a proven general SMP-local store. A universally shared mutable current managed-thread pointer would be unsafe, so no per-CPU bridge was added. The implementation does not establish that all future execution is single-core.

## Scheduler allocation provenance, identities, and lifetime

The P23R9 allocator trace remains unchanged. The loader identity-maps the 1 GiB allocator arena beginning at `0x04000000`. `Allocator` allocates page runs from its bitmap and scheduler tasks use those allocations for their stacks. `InitializeForUefi` reserves known ranges but does not derive free pages from EFI usable-memory descriptors. Thus:

- Allocation owner and reclamation owner: kernel `Allocator`; `Free` zeroes and returns pages to its bitmap.
- Virtual interval: allocator base plus requested stack bytes; the allocator tracks page runs, but scheduler stack descriptors do not capture the rounded run.
- Mapping: the broad arena is identity-mapped, but the scheduler does not re-walk actual per-stack mappings. A plausible identity address does not prove an EFI usable physical page.
- EFI provenance: absent for each scheduler stack; free-page ownership is not proven against the EFI map.
- Guard pages: no scheduler guard-page contract exists.
- Lifetime: a process cleanup can remove a task and free/zero its saved-context and stack allocations.

There is no stable scheduler stack ID with a generation, duplicate-registration protocol, owner validation, or replacement rejection. There is no pin/refcount, retirement state, or deferred-free path for a runtime walker. Generation tracking alone would not prevent use-after-free. Since those foundations are absent, no scheduler descriptor is exposed to NativeAOT and no lock-based pinning scheme was invented. P23R8's bootstrap allocation remains independently pinned for kernel lifetime.

## Attachment, detachment, suspension, and safety gates

Pinned source ordering is:

- Attach: retrieve the TLS `Thread`; `PalAttachThread`; construct and validate the current stack; then insert it in ThreadStore.
- Detach: `PalDetachThread`; remove the record from ThreadStore; call runtime detach/destroy.
- Current target selection: the caller's native TLS record. This is not a scheduler task lookup.

These paths were source-traced but not behaviorally executed in a hosted runtime. The intended hosted ThreadStore fixture was abandoned after the archive link proved it incomplete; this report does not claim a successful attach, failure rollback, duplicate-attach check, or detach test.

Foreign-thread walking remains disabled. There is no kernel suspension-and-acknowledgement mechanism, target-specific saved context, pinned target stack, or association to a ThreadStore object. Future support needs explicit target selection, stable suspension, saved-context capture, bounds owned by that target, descriptor pinning through the walk, correct GC mode/ThreadStore state, and release only after the consumer finishes. The existing bounded unwinder and GC-root validation remain fail-closed. No failed walk was reinterpreted as zero roots, no incomplete COFF manager was published, and no dummy `RhRegisterOSModule` was added.

## Tests, archive, and production builds

| Check | Result | Evidence/scope |
|---|---:|---|
| P23R8 descriptor-to-PAL regression using fresh P23R10 target PAL object | 30 passed, 0 failed | `run_stack_bounds_descriptor_tests.ps1`; hosted EFI map/page tables are synthetic. |
| Unsupported-PAL fail-closed regression | 4 passed, 0 failed | `run_stack_bounds_unsupported_tests.ps1`; output clearing and false result. |
| Bounded x64 unwind suite, bounded-only | 69 passed, 0 failed | `run_x64_unwind_tests.ps1`; test-only bounded evaluator and PE preflight. |
| Bounded x64 unwind suite, Windows reference | 69 passed, 0 failed; 20 reference comparisons | Host reference coverage, not target COFF-manager evidence. |
| Scheduler identity, generation, pinning, A → B → A, or ThreadStore attachment tests | Not run | No production bridge or linkable runtime fixture exists. |
| Foreign scheduler-thread walking | Disabled | No safe suspension, target identity, or stack pin. |

The isolated P23R10 `Runtime.WorkstationGC` archive built successfully with Ninja/MSVC 19.51.36231 from the P23R8 runtime staging tree. It contains 62 object members. Archive SHA-256: `427432AA25103E3D0307C4E178E7C8E93C0A8557404E6692D616B216B8FF80FA`. The target `PalRedhawkStackBounds.cpp.obj` used by the hosted regression has SHA-256 `DD62135683C308D23660015D31B739A97C91FF00982B00CF5573D7D9F5B5CA4C`. Symbol inspection found definitions for `PalGetMaximumStackBounds` and `guidexos_pal_register_bootstrap_stack`, and `RhpRegisterOsModule`; it found no `CoffNativeCodeManager` or `RhRegisterOSModule`.

The archive build is reported separately from full linking. A temporary hosted link probe against the archive failed with 38 unresolved externals and produced no executable. No loader or kernel source changed, so no production loader/kernel rebuild was required. No full production kernel or image was linked. No release artifact was overwritten.

## Phase 23 status and P23R11 frontier

Phase 23 remains **blocked**. The P23R8 bounds provider is still valid for the bootstrap stack, but current guideXOS startup bypasses the supported `RhInitialize`/module-registration sequence. The full pinned startup also requires a finalizer worker with its own verified stack. A ThreadStore algorithm test cannot be claimed until the actual target runtime consumer and platform providers link without substitute success behavior.

**P23R11 objective:** establish and review one supported guideXOS NativeAOT bootstrap sequence that creates `RuntimeInstance`/ThreadStore and GC before `InitializeModules`, then provide the single real finalizer execution context required by pinned startup with independently validated stack ownership and teardown. Execute the unmodified `ThreadStore::AttachCurrentThread`/`Thread::Construct` path on the bootstrap stack and verify the finalizer attach on its own stack. Keep ordinary scheduler tasks unattached until a per-CPU identity contract exists; keep foreign walking and GC/EH/code-manager publication gated. Do not regenerate Phase 29–35 identities or production artifacts from this incomplete evidence.

No QEMU, QAC admission, or physical hardware was used. No push occurred.
