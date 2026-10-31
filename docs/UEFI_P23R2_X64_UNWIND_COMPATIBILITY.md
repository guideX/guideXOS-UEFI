# P23R2 — x64 Unwind Compatibility and NativeAOT Code-Manager Foundation

**Outcome B — `P23R2_PARTIAL_UNWIND_VERIFIED`**

This change adds and behaviorally verifies a bounded Windows x64 unwind evaluator for ordinary version 1 unwind records. It removes the old unbounded `RtlVirtualUnwind` veneer from the Phase 23 shim. The evaluator is not connected to the NativeAOT code manager, GC, exception dispatch, or module registration. Phase 23 remains blocked.

## Checkout and preservation

- Repository: `D:\dev\guideXOSUEFI_BUILD_INFRA`
- Branch: `BUILD_INFRA`
- Starting HEAD: `1e4f41600c34af8d94805100e702500824d3cdf8`
- Ending HEAD: the local P23R2 commit created for this work; its SHA is included in the final execution summary because a commit cannot contain its own object ID.
- The BUILD_INFRA worktree was clean at the start. The local branch was two commits ahead of `origin/BUILD_INFRA`.
- The pinned runtime cache at `out\rt` remained at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. It already had 58 modified tracked or untracked status entries from earlier overlays. It was inspected read-only and not changed.
- Existing Phase 26 and BUILD1D files, caches, generated artifacts, and Phase 29–35 identities were left unchanged. `MODERN_INPUT`, `APP_MODEL`, and `mainline` were not modified. No branch switch, merge, rebase, reset, cherry-pick, or push occurred.

## x64 contract established

The pinned Windows COFF manager consumes Windows x64 `RUNTIME_FUNCTION`, `UNWIND_INFO`, `CONTEXT`, and `KNONVOLATILE_CONTEXT_POINTERS` layouts. The `RtlVirtualUnwind` import uses the Microsoft x64 calling convention. Its return type is `PEXCEPTION_ROUTINE` (a pointer), and its eight parameters include an image base, control PC, function entry, context, handler-data output, establisher-frame output, and optional context pointers. The API returns a handler pointer; it has no separate validation-failure result and receives no image or stack bounds. Those ABI facts are also visible in the Windows SDK headers used by the hosted reference test and in [Microsoft's API reference](https://learn.microsoft.com/en-us/windows/win32/api/winnt/nf-winnt-rtlvirtualunwind).

The verified layout used by this implementation is:

| Structure member | x64 offset / size |
|---|---:|
| `CONTEXT.Rax` | `0x78` |
| `CONTEXT.Rsp` | `0x98` |
| `CONTEXT.Rip` | `0xF8` |
| `CONTEXT.Xmm0` | `0x1A0` |
| `sizeof(CONTEXT)` | `0x4D0` |
| `KNONVOLATILE_CONTEXT_POINTERS.IntegerContext` | `0x80` |
| `sizeof(KNONVOLATILE_CONTEXT_POINTERS)` | `0x100` |

The Windows x64 `.pdata` record is three 32-bit RVAs: begin, exclusive end, and unwind data. The version 1 `.xdata` header gives the prologue length, unwind-code slot count, frame register, frame offset, and flags. Unwind codes are stored in descending prologue-offset order. Leaf frames have no table entry and recover the caller return address from `[RSP]`. Frame-relative saves use the fixed stack base recovered from the frame register; dynamic stack movement makes that distinct from the current RSP. The establisher frame is the base of the fixed allocation.

The [Microsoft x64 exception-handling description](https://learn.microsoft.com/en-us/cpp/build/exception-handling-x64) also requires epilogue recognition by inspecting the code stream and simulating its remaining instructions. The bounded evaluator does not decode epilogues: its caller must identify them, and the evaluator rejects a known epilogue. It also rejects chained records and handler flags. These choices are explicit constraints, not support claims.

No Windows OS service is used by `x64_unwind_bounded.h`. The hosted test-only comparison calls `RtlVirtualUnwind` from `ntdll`; that dependency is excluded from the bounded-only test build and from the guideXOS target.

## Existing veneer audit

The same legacy implementation existed in the Phase 23 shim and in copied Phase 27–31 shims. Its functions classify as follows:

| Function / symbol | Classification | Finding |
|---|---|---|
| `guidexos_unwind_register` | Partially correct | Maps the x64 register numbers to GPR context fields, but callers do not validate that each opcode permits the selected register. |
| `guidexos_unwind_register_pointer` | Partially implemented | Provides integer context pointers only; no floating-point pointer path. |
| `guidexos_unwind_one` | Partially implemented / unsafe | Handles some pushes, allocations, frame setup, and integer saves; skips XMM saves, ignores unknown operations, and reads unbounded metadata and stack addresses. |
| `guidexos_rtl_virtual_unwind` | Incorrectly stubbed | Uses the wrong return type, cannot report validation failure, does not supply handler data, and returns an incorrect establisher-frame value. |
| `__imp_RtlVirtualUnwind` | Incorrect ABI declaration | Function-pointer return type is `u32` rather than `PEXCEPTION_ROUTINE`. |

The Phase 23 copy was removed from `Tools/Phase23/guidexos_link_shim.cpp`. The Phase 26 shim is an include wrapper over that Phase 23 file, so future Phase 26 rebuilds inherit the removal. The Phase 27, 28, 29, 30, and 31 shim files still contain copied legacy implementations. The Phase 27–31 builder scripts derive their generated shims from the Phase 23 source; Phase 32–35 builders also reuse the Phase 31 shim. Those copied sources and all generated artifacts were left untouched, and no Phase 26–35 cohort or identity was rebuilt. They must be reconciled against the bounded interface before those phases are rebuilt or used as evidence for runtime registration.

The Phase 23 implementation contained useful fragments for register numbering, leaf returns, common stack allocations, pushes, frame-register setup, and integer saves, but it was unsafe as a code-manager ABI implementation:

- Its `RtlVirtualUnwind` function pointer returned `u32`; the x64 ABI returns a pointer. Handler addresses could be truncated.
- Its context stopped at RIP. It did not model the full x64 context or restore saved XMM registers. It skipped `UWOP_SAVE_XMM128` and did not populate floating-point context pointers.
- It dereferenced image metadata and stack addresses without image bounds, stack bounds, checked arithmetic, or unwind-slot validation.
- Unknown and unsupported unwind operations were skipped instead of reported as failure.
- It ignored handler metadata and set handler data to null. Its chained-record recursion reused the original control PC and had no depth or bounds check.
- It did not establish a safe failure channel. It could not tell the code manager that an unwind had failed.
- It returned the stack pointer after consuming the return address as `EstablisherFrame`, rather than the fixed-frame base expected by the Windows x64 contract.

The Phase 23 old veneer is removed instead of being adapted to the same ABI without the required bounds and failure behavior. If the COFF manager is selected for the target before a reviewed adapter exists, its `__imp_RtlVirtualUnwind` dependency will remain unresolved and the link will fail.

## Implemented bounded functionality

`Tools/Phase23/x64_unwind_bounded.h` adds a freestanding C++17 evaluator that receives explicit image and stack spans. It validates checked address ranges and commits a context only after a complete successful unwind.

Supported operations and behavior:

- Validated, sorted, disjoint `.pdata` lookup with half-open function ranges.
- Leaf return-address recovery.
- Version 1 `UWOP_PUSH_NONVOL`, `UWOP_ALLOC_LARGE` forms 0 and 1, `UWOP_ALLOC_SMALL`, `UWOP_SET_FPREG`, `UWOP_SAVE_NONVOL`, `UWOP_SAVE_NONVOL_FAR`, `UWOP_SAVE_XMM128`, and `UWOP_SAVE_XMM128_FAR`.
- Partial-prologue unwinding using each operation's prologue offset.
- Frame-pointer-based and dynamic-stack recovery, including fixed-frame-base offsets for saved registers.
- Restored GPR and XMM values plus source-slot pointers in the Windows context-pointer layout. The far integer-save pointer case described below remains unqualified against Windows.
- Explicit rejection statuses for malformed bounds/metadata, invalid registers and operation order, misaligned or out-of-range stack slots, unsupported versions/opcodes, handlers, chains, and caller-classified epilogues.

Deliberately unsupported: chained unwind metadata, handler flags/data, version 2 epilogue-code records, machine frames, automated epilogue detection/simulation, unsafe memory probing, Windows ABI failure signaling, PE header parsing, managed-range ownership, transition-frame lookup, and code-manager registration. `FindRuntimeFunction` validates its supplied `.pdata` table but does not replace the code manager's managed-range ownership check. A caller must supply already bounded views; this change does not establish that the UEFI loader or runtime can safely construct those views.

## Hosted behavioral tests

Added `Tests/NativeAotUnwind/x64_unwind_tests.cpp` and `Tests/NativeAotUnwind/run_x64_unwind_tests.ps1`. The tests run against controlled image bytes, unwind records, and bounded stack memory. A Windows-hosted build also calls `RtlVirtualUnwind` directly on matching synthetic records for comparison. The bounded-only build uses no Windows API reference.

Results:

- Bounded-only build: **36 tests passed, 0 failed**.
- Windows-reference build: **36 tests passed, 0 failed**, including **8 successful `RtlVirtualUnwind` comparisons** for pushed registers, stack allocation, frame pointers, dynamic stack allocation, saved integer registers, and saved XMM values.
- Each build used `g++` with `-std=c++17 -Wall -Wextra -Werror`. Executables were created under a unique temporary directory, outside the repository.

The hosted Windows API restored the register and caller context for the valid `UWOP_SAVE_NONVOL_FAR` case but left `KNONVOLATILE_CONTEXT_POINTERS.IntegerContext[13]` null; the bounded evaluator reports the stack slot it used. The state comparison passes while context-pointer parity for this far-save form remains unresolved. This does not establish GC compatibility and is a required P23R3 investigation before integration.

Coverage includes leaf and non-leaf frames; nested calls; RSP allocation; pushed and saved nonvolatile registers; XMM saves; frame-pointer and dynamic-stack frames; prologue start, operation, and end boundaries; known-epilogue rejection; range lookup; malformed and truncated metadata; invalid stack and image bounds; overflow; and explicit rejection of unsupported encodings and chained records.

The tests establish behavior of this bounded evaluator on synthetic frames and compare the supported cases with Windows. They do not execute the NativeAOT code manager or a live guideXOS stack.

## Code-manager, GC, and exception assessment

The pinned runtime's Windows `CoffNativeCodeManager` looks up methods in the PE runtime-function table, reads GC and per-method EH data adjacent to unwind data, calls `RtlVirtualUnwind` for stack operations, and uses restored nonvolatile register locations while updating `REGDISPLAY`. `Thread::GcScanRootsWorker` walks managed frames through that code manager and handles transition frames and stack ranges separately. `StackFrameIterator` uses code-manager method lookup and unwind operations for stack walking.

The upstream manager is not selected for `CLR_CMAKE_TARGET_GUIDEXOS` in the runtime's `Runtime/CMakeLists.txt`; that branch adds the GUIDEXOS GC environment but does not add `windows/CoffNativeCodeManager.cpp` or `unix/UnixNativeCodeManager.cpp` to `FULL_RUNTIME_SOURCES`. The manager itself asserts that chained x64 unwind info is unsupported. It consumes additional metadata and lifetime/registration behavior that this evaluator does not implement.

The genuine upstream `RhRegisterOSModule` contract takes seven arguments: the module image, managed-code range, unboxing-stub range, and class-library function table. The Windows implementation parses the image exception directory, constructs and retains a COFF manager, registers the managed range, then registers unboxing stubs. `RegisterCodeManager` has no failure result, while unboxing-stub registration can fail after the code manager is published; a GUIDEXOS implementation therefore needs an explicit partial-registration and ownership design. Runtime registration happens before module initialization and the runtime retains manager state for process lifetime. `RhpRegisterOsModule` only adds a one-argument OS module handle to the runtime list and does not satisfy this contract.

Therefore:

- **Unwind behavior verified:** Yes, for the bounded subset and hosted test frames described above.
- **Code-manager integration verified:** No. The evaluator is not an `RtlVirtualUnwind` adapter and does not satisfy the manager's OS-facing contract.
- **GC enumeration verified:** No. GC-info decoding, `REGDISPLAY` integration, transition frames, and root enumeration were not run.
- **Exception unwinding verified:** No. Handler metadata, NativeAOT EH clauses, propagation, funclets, and epilogue handling were not exercised.

## Phase 23 boundary

`RhRegisterOSModule` remains unresolved in the target runtime. The GUIDEXOS CMake selection still omits the code manager that defines the genuine upstream function. `RhpRegisterOsModule` remains a different one-argument OS-module-list operation and is not a substitute. The existing recovery evidence records the Phase 23 `LNK2019`/`LNK1120` failure; this phase did not rebuild the target pack or proof because the required manager, bounded target adapter, PE validation, and GC/EH dependencies are still absent.

No registration function, dummy export, or success path was added. The failing link gate remains intact. The Phase 23 proof did not pass, and no production kernel was generated.

## Phase 23R3 objective

Build a target-facing PE/COFF code-manager path that can fail closed through the runtime's actual interface. It must validate image headers, `.pdata` and `.xdata` bounds, managed and unboxing-stub ranges, and current-thread stack bounds; implement Windows x64 prologue and epilogue unwinding plus every unwind form required by produced NativeAOT images; support or reject chained/handler forms based on the pinned manager's real requirements; and resolve registration ownership and partial-failure ordering. Then test the actual code manager with GC metadata and exception clauses before adding a genuine `RhRegisterOSModule` implementation or enabling production registration.

## Final gate summary

| Gate | Result |
|---|---|
| Outcome | `P23R2_PARTIAL_UNWIND_VERIFIED` |
| Bounded hosted unwind tests | 36/36 pass in each of two builds |
| Windows `RtlVirtualUnwind` comparisons | 8 pass |
| Code-manager integration | Not verified; not connected |
| GC enumeration | Not verified |
| Exception unwinding | Not verified |
| `RhRegisterOSModule` resolved | No |
| Phase 23 proof passed | No; known failing link gate remains |
| Production kernel generated | No |
| QEMU or hardware used | No |
| Changes pushed | No |
