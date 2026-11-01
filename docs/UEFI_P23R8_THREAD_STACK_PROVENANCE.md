# P23R8 — UEFI Bootstrap Thread Stack Provenance

**Outcome A — `P23R8_BOOTSTRAP_STACK_CONTRACT_VERIFIED`**, within the hosted-only scope below. The loader now publishes a descriptor from its exact UEFI stack allocation, validates the final page tables and EFI memory map before handoff, and registers the descriptor with the target PAL before NativeAOT enters `KMain`. The hosted integration links the target-built PAL object and exercises the same descriptor builder used by the loader. It does not establish that a physical UEFI boot or final kernel link succeeds.

## Checkout and preservation

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`.
- Branch: `BUILD_INFRA`; starting HEAD: `f656ef557feeac5b625b3d05e84dd14375970b53`.
- Ending HEAD: the local P23R8 commit containing this report. Its SHA is in the final execution summary because a commit cannot contain its own object ID.
- Runtime source HEAD: `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`.
- `out\rt` remained pinned at that runtime HEAD with the same 65 status entries and status hash `59610A7C32254E178B563A23AA2C4D5E0389FCF9873E1ED2CDBF21B76B724DF9` recorded by P23R6/P23R7.
- P23R8 runtime edits and build outputs stayed in `D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING`. P23R7 staging and other checkouts were not modified. The P23R8 overlay applies and reverses cleanly against the P23R7/P23R8 staging copies.
- No branch switch, merge, rebase, reset, cherry-pick, push, QEMU, QAC, physical test, or Phase 29–35 record generation occurred.

The reproducible runtime delta is [p23r8_runtime.patch](../Tools/Phase23/runtime-overlay/p23r8_runtime.patch). It uses zero-context hunks and is checked with `git apply --unidiff-zero` in both forward and reverse directions. The target archive symbol audit and unresolved-name list are in [P23R8_RUNTIME_WORKSTATIONGC_SYMBOLS.txt](evidence/P23R8_RUNTIME_WORKSTATIONGC_SYMBOLS.txt) and [P23R8_RUNTIME_WORKSTATIONGC_DEPENDENCIES.txt](evidence/P23R8_RUNTIME_WORKSTATIONGC_DEPENDENCIES.txt).

## Bootstrap stack owner and lifetime

`guideXOSBootLoader/efi_main` is the owner. Before `ExitBootServices`, it requests 128 pages (512 KiB) as `EfiLoaderData`, first at 2 MiB, then 1 MiB, then at any available address. The returned allocation base and page count, rather than an RSP estimate, define the stack range. The top is the aligned high address; the stack grows down. The allocation has no guard pages, so the accessible interval equals the exact allocation interval.

The loader builds the kernel PML4 before `ExitBootServices`. Its identity-map ranges include the whole stack, BootInfo, the handoff record, and the memory-map buffer. After the final page-table additions, the shared x64 walker checks every 4 KiB stack page through that PML4. It requires present, writable, supervisor mappings whose physical page equals the virtual page. The loader sets the verified-mapping flag only after this walk succeeds.

The final EFI memory map is captured in a preallocated `EfiLoaderData` buffer and carried through BootInfo. The PAL checks that the descriptor’s complete allocation is covered by one or more `EfiLoaderData` descriptors. This corroborates the allocation; it is not used alone to infer its extent. Address addition, map byte-count multiplication, alignment, descriptor size, and interval ordering are checked.

`BootMemoryHandoff` used to be loader-image global data. It now resides in its own `EfiLoaderData` pages and those pages are explicitly mapped. The record survives `ExitBootServices`; its descriptor is copied into immutable PAL state before managed execution. The kernel allocator reserves the stack, handoff pages, page-table pages, BootInfo page, and populated memory-map bytes before allocating its owner arrays. The loader does not free the stack after `ExitBootServices`, and the kernel has no stack teardown path for this bootstrap allocation. It remains pinned for the kernel lifetime.

The trampoline switches to this stack and installs the new CR3 before jumping to `KMainWrapper`. The wrapper uses the same stack and registers the descriptor before jumping to `KMain`; it does not switch stacks. BM1’s early exception-table call also runs on this stack. Later kernel startup installs a separate 64 KiB TSS RSP0 stack, and the scheduler creates allocator-backed per-thread stacks that it can switch to from interrupt context. Those later stacks are not represented by this bootstrap descriptor.

## Descriptor and validation contract

`Tools/Phase23/stack_bounds_contract.h` defines an 88-byte descriptor with eleven 64-bit fields: magic, version, kind, allocation low/high, accessible low/high, mapping identity, owner identity, generation, and flags. All intervals are half-open `[low, high)`. The bootstrap owner is the BSP startup thread, with generation 1. Because this stack has no guard pages, accessible bounds exactly equal allocation bounds.

The loader calls `CreateBootstrapDescriptorAfterPageWalk` only after `ValidateIdentityWritableRange` succeeds. The helper records the exact allocation and required lifetime, `EfiLoaderData`, identity-map, present, writable, supervisor, and verified-page-walk flags. The runtime registration helper then checks the descriptor header, ranges, flags, owner and generation, final EFI map coverage, active CR3 identity, and its own active stack marker. A mismatch leaves the provider unregistered and the wrapper halts. The C# entry path repeats the active RSP/CR3 and layout checks before allocator initialization.

The shared handoff layout keeps `BootstrapStack` at offset 16,400. C++ static assertions passed in both normal and BM1 loader builds: the handoff is 16,488 bytes normally and 16,688 bytes in BM1. The 88-byte descriptor and C# layout checks compiled in both configurations.

## PAL behavior and NativeAOT boundary

The pinned runtime declares `bool __stdcall PalGetMaximumStackBounds(void** low, void** high)`. `Thread::Construct` calls it for the calling OS thread, stores the pair, and fail-fasts on failure. It is not an API for retrieving an arbitrary suspended thread’s bounds.

The GUIDEXOS PAL clears every non-null output first. It returns the validated accessible interval only when the caller’s return-address slot lies inside the immutable bootstrap descriptor. It returns `false` for absent or invalid registration, null output pointers, a different stack, a replacement descriptor, or a foreign host thread. The return-address slot is used only to associate the caller with an already-proven range; bounds are never inferred from RSP. Atomic release/acquire publication prevents readers from seeing a partially copied descriptor. The descriptor is never invalidated or reused.

The hosted test links the exact target-built `PalRedhawkStackBounds.cpp.obj`, calls the production loader descriptor builder after a simulated page walk, registers that descriptor through the production C ABI, and checks the exact PAL result and rejection paths. Thus the PAL precondition for `Thread::Construct` is satisfied for the supported bootstrap stack in the hosted fixture. `Thread::Construct` itself was not executed, and the final kernel link was not run; those are not claimed as end-to-end runtime attachment evidence.

## Managed workers and foreign threads

guideXOS has a kernel scheduler thread type that allocates kernel stacks from `Allocator` and switches among them. These are kernel-managed execution stacks, not NativeAOT `ThreadStore` OS threads. Their allocation, execution, suspension, and teardown are not connected to NativeAOT’s stack-bounds provider. The bootstrap PAL therefore does not certify them. A stack switch outside the bootstrap interval cannot get bootstrap bounds from a fresh PAL query, and the existing bounded iterator rejects an SP outside the bounds stored on the NativeAOT thread.

Foreign and suspended-thread lookup remains unsupported. The PAL answers only for its caller. The repository has no stable mapping from a suspended scheduler thread to a NativeAOT `Thread`, plus no descriptor pinning protocol for concurrent stack teardown or reuse. The hosted foreign-thread check confirms that another host thread cannot borrow the bootstrap bounds.

## Bounded unwinding, hosted tests, and build evidence

The existing P23R3–P23R7 bounded unwind work remains unchanged. Its hosted suite passed 69/69 tests in both bounded-only and Windows-reference runs; the reference run reported 20 comparison cases. These tests cover half-open stack bounds, nested frames, saved-register locations, invalid stack pointers, failure status, and no partial caller-context update. They are not a target COFF-manager run or a physical stack-read test.

The new descriptor-to-PAL suite passed **30/30** checks against the target-built PAL object. It covers exact descriptor creation, overflow and malformed ranges, absent or wrong backing memory, guard exclusion, mapping flags and simulated page-table permissions, CR3/owner/generation mismatch, invalid active-stack association, output clearing, invalid handoff non-publication, descriptor replacement, exact returned bounds, and foreign-thread rejection. The test record is [P23R8_STACK_BOUNDS_HOSTED_TEST.json](evidence/P23R8_STACK_BOUNDS_HOSTED_TEST.json). Hosted page accessibility is simulated; no physical page tables were exercised.

The loader compiled and linked in isolated normal and BM1 directories. The modified `native_stubs.asm` assembled to an isolated object. The managed project’s C# `Compile` target passed in normal and BM1 configurations using a temporary compile-only substitute for the absent generated `ManagedArtifactIdentities.g.cs`; no `Build` or `Publish` target ran. No release image or native kernel output was overwritten.

The isolated `Runtime.WorkstationGC` archive was rebuilt with MSVC 19.51.36231 from the pinned runtime plus the P23R8 overlay. It is 62 members and has SHA-256 `89D235302E382432DE7AD49DE90824DAF00159BFC49407CDFA94696254247F6A`. It contains one definition each of `PalGetMaximumStackBounds` and `guidexos_pal_register_bootstrap_stack`; the PAL’s runtime reference resolves inside the archive. The modified `native_stubs.obj` has one undefined reference to the registration helper, whose definition is present in the archive. A final kernel link was not run, so exact target-link resolution is not claimed. The archive has 98 distinct unresolved external names after matching its own definitions; the complete list is included in the dependency report.

`CoffNativeCodeManager.cpp.obj` is absent from the target archive. `RhRegisterOSModule` remains undefined, while `RhpRegisterOsModule` is a separate module-list helper. No dummy registration function or manager publication path was added. The target bounded iterator is not connected to a genuine target COFF manager. Target GC/EH failure reporting, manager preparation, and safe publication remain unproven; no GC/EH or code-manager publication changes were made. Phase 23 therefore remains unverified.

## P23R8 acceptance and next objective

P23R8 verifies the bootstrap allocation-to-descriptor-to-PAL contract at source, isolated-build, symbol, and hosted-integration level. The exact runtime PAL and descriptor builder are exercised; real UEFI page tables, `Thread::Construct`, a full kernel link, a boot, and physical memory behavior are not. Worker stacks, suspended foreign stacks, and later scheduler stack switches remain unsupported by this provider.

**Proposed P23R9 objective:** Register the existing allocator-backed scheduler stacks with stable thread identities and generations, publish and pin descriptors across context switches, and make NativeAOT stack walking select the descriptor for the thread actually being walked. Validate teardown, stack reuse, mappings, and suspend/resume lifetime before enabling those paths. Keep suspended foreign-thread walking unsupported until the kernel has a stable identity and pinning contract. Only then resume target COFF-manager and GC/EH work.
