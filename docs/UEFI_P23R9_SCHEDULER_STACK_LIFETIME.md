# P23R9 — Scheduler Stack Lifetime and Runtime Selection

**Outcome C — `P23R9_THREAD_ARCHITECTURE_BLOCKED`.** The kernel has allocator-backed scheduler stacks and task objects, but those objects are not NativeAOT `ThreadStore` threads. The current PAL can prove only the immutable UEFI bootstrap stack. There is no safe kernel-to-ThreadStore identity bridge, no validated per-allocation physical-memory provenance, and no walk pin/deferred-reclamation contract for scheduler stacks. Publishing scheduler descriptors or changing PAL selection would therefore claim guarantees the current scheduler and runtime do not provide.

No scheduler registration, generation, or pinning code was added. A registry disconnected from the real scheduler and NativeAOT thread state would only test synthetic ownership. P23R8 bootstrap behavior remains intact and fail-closed.

## Checkout, branch, and preservation

- Repository: `D:\dev\guideXOSUEFI_BUILD_INFRA`.
- Branch: `BUILD_INFRA`.
- Starting HEAD: `e668c6dbe2d3fad872253b17c0b63a88fc48e46d`.
- Starting worktree: clean; one local commit ahead of `origin/BUILD_INFRA`.
- Ending HEAD: the local P23R9 report commit. Its SHA is reported in the execution summary because the commit cannot contain its own object ID.
- No branch switch, merge, rebase, reset, cherry-pick, or push occurred.
- The active checkout `D:\dev\guideXOSUEFI_MODERN_INPUT` and all other checkouts were left unchanged.

The protected `out\rt` checkout remained at detached HEAD `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. Its current status has **65** entries, matching the P23R8 report. I re-read and saved the current porcelain status to `out\dotnet\p23r9-runtime-status-preservation.txt` (SHA-256 `7D48B6B71F2B7B53794738B6974A8D611E51F0780D757650D648B8239C0B35A1`); a final comparison confirmed all 65 entries still match. No file under `out\rt` was changed.

Runtime overlay checks were read-only against `D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING`, whose HEAD is the pinned runtime commit. The P23R8 overlay passes `git apply --reverse --check --unidiff-zero` there. The new P23R9 CMake build and test output were isolated under `out\dotnet\phase23r9-runtime-workstationgc`; no runtime source checkout was edited.

## Scheduler stack allocation and provenance

`guideXOS.Misc.Thread.NewThread` allocates two objects through `Allocator`: an `IDT.IDTStackGeneric` saved interrupt/context frame, and the actual kernel execution stack. The default kernel stack is 16 KiB; callers can choose another size. The stack base is returned by the allocator and the top is computed as `base + requestedSize`.

The allocator manages a 1 GiB arena beginning at physical/virtual address `0x04000000`. The UEFI loader identity-maps that whole arena, and the allocator returns `arenaStart + pageIndex * 4096`, so each allocation is contiguous in the arena and virtual addresses equal physical addresses under the loader page tables. There are no guard pages. Allocator metadata records a page run, but scheduler stack construction does not query that run length or re-walk the mapping. For a non-page-aligned requested size, `KernelStackTop` is based on the requested byte count rather than the page-rounded allocation end.

The broad identity mapping establishes translation and writable mappings for the arena. It does not establish that every physical page in the 64 MiB–1 GiB range is installed usable RAM. `Allocator.InitializeForUefi` reserves named boot/kernel ranges, then treats other unreserved bitmap pages as available; it does not seed its free-page map from EFI memory descriptors. This differs from the bootstrap stack, whose exact `AllocatePages` backing and every mapped page were validated before handoff. Scheduler allocation and mapping provenance are therefore insufficient for authoritative descriptors.

`Kernel.Misc.Thread` is the scheduler owner object. `ThreadPool.Threads` stores runnable tasks; `Start` assigns a CPU affinity and appends the object. `ThreadPool.CurrentThread` resolves `_directUserThread`, or `Threads[Index]`, where `Index` reads `Indexs[SMP.ThisCPU]`. On timer IRQ 0x20, `IDT.intr_handler` calls `ThreadPool.Schedule`. The scheduler saves the interrupted frame into the current object's `Stack`, selects a runnable entry, updates the TSS RSP0 and CR3 for that entry, and passes its saved context to the native interrupt epilogue for the return/switch.

The scheduler has no explicit bootstrap `Thread` object or bootstrap-to-scheduler descriptor transition. `ThreadPool.Initialize` creates an idle task and leaves the per-CPU index at zero; the bootstrap stack descriptor remains a separate PAL special case. The scheduler's logical `CurrentThread` can consequently name an idle/task object while execution is still returning through an interrupt frame captured from the bootstrap stack. No PAL hook consumes the scheduler's selection.

There is no numeric scheduler-thread ID or generation field. The diagnostics in `IDT.cs` use `KernelStackBase` as a live-allocation identifier. That address can be reused after `Allocator.Free`, so it is not a reuse-safe identity. The process-handle generation is a different identity and is not a scheduler-stack generation. Current UEFI initialization creates a one-entry `Indexs` array even though the accessor indexes it using the APIC ID; multi-CPU current-thread selection is not established by this path.

`BeginDirectUser` and `EndDirectUser` are temporary user-mode execution transitions; they set the current user task and TSS kernel stack but do not attach an OS thread to NativeAOT's ThreadStore. The 64 KiB kernel RSP0 stack allocated during entry-point setup is another distinct stack. The 512 KiB loader bootstrap stack is a third, loader-owned allocation. These ranges must not be conflated.

## Switching, interrupts, retirement, and lifetime

Timer scheduling preserves task contexts across switches by storing a native interrupt frame in each scheduler object's saved `Stack`. That is scheduler context preservation only; it does not save or restore NativeAOT's current `Thread*`, ThreadStore TLS, allocation context, transition-frame chain, or managed thread statics.

The IDT gates built by `guideXOS/native_stubs.asm` use IST index zero. Same-CPL kernel interrupts and exceptions therefore enter on the current kernel stack. A user-to-kernel privilege transition uses the TSS RSP0 selected by the scheduler. The TSS is a single static structure in `GDT.cs`, and SMP startup shares the GDT; there is no per-CPU TSS/IST descriptor set proven by this path. The handler disables interrupts and tracks nesting for diagnostics, but nested faults still do not create a scheduler-owned alternate stack contract. Fault handling on a kernel stack goes to the panic path.

Generic kernel task termination marks the current task terminated and parks it after requesting the legacy `Schedule_Next` hook. The process cleanup path marks a user task terminated, removes it from the scheduler list, then frees its saved-context allocation and kernel stack through `Allocator.Free`; `Free` zeroes the pages and returns them to the bitmap. `ThreadPool.RemoveThread` refuses removal only when the caller's `CurrentThread` is that object. `IsProcessActive` checks the caller CPU's current scheduler object and CR3. There is no cross-CPU walk pin, suspend-and-ack protocol, active-walker reference count, epoch/grace period, or deferred-free queue. A generation counter by itself would not protect an active walker from the zero-and-reuse behavior of `Free`.

## NativeAOT association and PAL behavior

The pinned declaration in `PalRedhawk.h` defines `PalGetMaximumStackBounds` as the maximum stack allocation for the calling OS thread. `Thread::Construct` records the OS thread ID, calls `PalGetMaximumStackBounds`, stores the returned bounds in that runtime `Thread`, and fail-fasts if the call fails. `ThreadStore::AttachCurrentThread` calls `PalAttachThread`, constructs the runtime `Thread`, and only then inserts it into the ThreadStore.

The scheduler's `guideXOS.Misc.Thread` is not that runtime `Thread`. Constructing or starting a scheduler task does not call `ThreadStore::AttachCurrentThread`, create a NativeAOT thread record, or establish a runtime TLS identity for it. Kernel task delegates do execute managed code, but the scheduler currently runs those stacks under the existing per-CPU/runtime attachment state. A timer switch does not make the task a distinct NativeAOT thread.

P23R8's 88-byte descriptor remains a valid special case for the BSP bootstrap stack: the loader records its exact `EfiLoaderData` allocation, verifies identity writable supervisor mappings, and the kernel reserves it for the kernel lifetime. The PAL publishes that descriptor atomically once. `PalGetMaximumStackBounds` clears outputs and returns the bootstrap interval only when its active return-address slot lies inside that descriptor. A scheduler stack outside the bootstrap interval therefore fails closed; the PAL does not return bootstrap bounds for it or provide foreign-thread lookup. The marker is an association check against a proven interval, not a range inferred from RSP. P23R9 did not change the PAL ABI or broaden its meaning.

Foreign/suspended scheduler-stack walking remains disabled. The P23R6 iterator overlay asks the selected NativeAOT `Thread` for that thread's stored bounds, which is the right owner direction for runtime-managed threads. No association maps a guideXOS scheduler object to that `Thread`, and no scheduler suspension/pinning contract keeps the stack alive during an external walk. Arbitrary stack inspection would be unsafe.

## Bounded unwind and GC/EH evidence

The bounded x64 evaluator remains hosted-only for explicit image, context, and stack views. It validates stack intervals and saved-register locations and commits caller state transactionally. P23R6's runtime iterator changes carry bounds from the walked NativeAOT `Thread`; they do not receive P23R9 scheduler descriptors. The successful evaluator tests do not prove a walk over a real scheduler stack, and the evaluator is not connected to a target COFF manager or GC consumer. No GC roots were fabricated or published by this work. Invalid selection is covered by the existing bounded evaluator's rejection/no-partial-context-update cases; scheduler-to-GC root behavior remains unverified.

No target `CoffNativeCodeManager` was selected or published. `RhRegisterOSModule` remains absent. The runtime archive still contains `RhpRegisterOsModule`, a separate OS-module-list helper, not a code-manager registration implementation. Target GC/EH failure propagation, manager preparation, and safe publication remain blocked as described by the P23R7/P23R8 evidence.

## Hosted validation

| Check | Result | Scope |
|---|---:|---|
| P23R6 bounded unwind suite, bounded-only | 69 passed, 0 failed | Existing hosted evaluator and PE preflight regressions. |
| P23R6 bounded unwind suite, Windows-reference | 69 passed, 0 failed; 20 reference cases | Host reference comparisons; not target manager evidence. |
| P23R8 descriptor/PAL suite using P23R8 object | 30 passed, 0 failed | Production descriptor builder and target PAL object; mappings simulated. |
| P23R9 descriptor/PAL suite using the P23R9-built PAL object | 30 passed, 0 failed | Exact object from the fresh P23R9 WorkstationGC build; mappings simulated. |
| Scheduler allocation/registration/switch/pin tests | 0 | No production scheduler registry or safe ThreadStore contract exists to test. |
| `Thread::Construct` / ThreadStore attachment | Not executed | The standalone PAL fixture is not `Thread::Construct` evidence. |
| Scheduler-stack unwind / GC consumer | Not executed | No selected descriptor reaches a target frame walker or manager. |

The P23R9 test record is `out\dotnet\phase23r9-runtime-workstationgc\pal-test-result\stack-bounds-descriptor-test-record.json`. Its exact target object hash is `2F9D0132B1162DF362DBB1470CCC50B614C20C5FA06F8825CA926216DB3B1F76`. Both descriptor runs state that physical page accessibility was simulated and `Thread::Construct` was not executed. No fake suspension test was used.

## Runtime overlay, archive, and linkage

No P23R9 runtime patch was needed or applied: doing so without the scheduler/ThreadStore bridge would only change the runtime side of an incomplete contract. The existing P23R8 overlay reverse-check passed against its staging source.

A fresh Ninja build was configured from the P23R8 runtime staging source in the isolated P23R9 output directory and built `Runtime.WorkstationGC` with MSVC 19.51.36260.0 / toolset 14.51.36231. The first build invocation omitted the VS developer environment and failed to find `stdint.h`; the retry through `vcvars64.bat` completed all 63 build steps. The P23R8 staging archive was not overwritten.

- Archive: `out\dotnet\phase23r9-runtime-workstationgc\build\nativeaot\Runtime\Full\Runtime.WorkstationGC.lib`.
- SHA-256: `87932E6663F6021E94DB61F283A0FD9CA8E18880D1CA0828EF780A991273E465`.
- 62 object members; includes the target `PalRedhawkStackBounds.cpp.obj` and bounded iterator changes.
- Symbol audit: one `PalGetMaximumStackBounds` definition plus an internal reference; no `CoffNativeCodeManager` or `RhRegisterOSModule`; `RhpRegisterOsModule` is present.
- After matching archive definitions against its undefined entries, 98 distinct names remain external to the static library. They include GUIDEXOS PAL/kernel imports, runtime services, CRT/compiler support, and Windows-shaped APIs such as `CloseHandle`, `DuplicateHandle`, and `GetCurrentThread`. These are archive-level dependencies, not 98 independent defects. There was no final target link, so which imports are satisfied by the final PAL/kernel and whether any Windows API dependency remains unsupported are not established.
- WorkstationGC built successfully. ServerGC was not built. No target kernel or production image link was attempted.

No loader or kernel sources changed, so no loader/kernel component build was needed. The isolated runtime archive build proves compilation only, not final target linkage.

## Phase 23 status and P23R10 recommendation

Phase 23 remains **blocked**. The bootstrap stack contract remains verified at hosted/source/build level, while scheduler-owned stack registration, lifetime-safe selection, real foreign-thread lookup, `Thread::Construct`, target frame walking, target GC/EH consumption, code-manager registration, and production image proof remain unverified. No QEMU, QAC admission, physical hardware, or push was used.

**Recommended P23R10 objective — define and prove the scheduler execution-context bridge before enabling NativeAOT stack walking.** First, give each CPU/task a stable owner token plus reuse generation and an explicit bootstrap-to-scheduler handoff. Derive allocation bounds from allocator-owned page runs and EFI-usable backing, validate accessible mappings, and add a per-CPU current-context publication that the PAL can query without a global mutable descriptor. Then add scheduler/runtime attachment semantics that save and restore the actual NativeAOT thread state, plus a suspend/pin/deferred-reclamation contract shared by termination and walkers. Keep scheduler stack lookup fail-closed until hosted tests exercise the production allocator, selection, retirement, active pin, generation reuse, and exact target PAL object. Only after `Thread::Construct` or a genuine hosted equivalent is executed should the target bounded frame-walker connection be considered. Continue to defer COFF-manager/GC/EH publication work until those stack and thread contracts are proven.
