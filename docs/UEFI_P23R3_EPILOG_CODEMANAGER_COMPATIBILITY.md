# P23R3 — x64 Epilogue and Code-Manager Compatibility

**Outcome B — `P23R3_EPILOG_FOUNDATION_VERIFIED`**

The bounded x64 evaluator now recognizes and simulates a tested subset of legal epilogues, and the R13 context-pointer result is understood for the tested Windows setup. The code-manager-shaped adapter returns explicit status and is covered by hosted tests. It is not wired into the runtime. GC root enumeration, exception handling, target registration, and failure behavior across all runtime consumers remain unverified, so Phase 23 stays gated.

## Checkout and preservation

- Repository: `D:\dev\guideXOSUEFI_BUILD_INFRA`
- Branch: `BUILD_INFRA`
- Starting HEAD: `6a7f8e745d9a7a1f8b7882c577715cb7797b9097`
- Ending HEAD: the local commit that adds this report. Its exact SHA is in the final execution summary because a commit cannot contain its own object ID.
- The requested starting HEAD matched the checkout. The primary worktree was clean before this task; the branch was four commits ahead of `origin/BUILD_INFRA`.
- The pinned runtime checkout at `out/rt` remained at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with its 58 pre-existing modified or untracked status entries. It was inspected read-only and remains unchanged.
- No branch switch, merge, rebase, reset, cherry-pick, or push occurred. No release artifact, generated cache, Phase 29–35 identity, Phase 23 shim, or Phase 27–31 shim was changed.

## R13 context-pointer investigation

`KNONVOLATILE_CONTEXT_POINTERS.IntegerContext[13]` describes the storage location for the recovered R13 value. The structure owns no storage. In the COFF manager, `REGDISPLAY_TO_CONTEXT` seeds the nonvolatile entries from the current `REGDISPLAY` pointers, copies values into a temporary `CONTEXT` when the input pointer is non-null, and writes the returned locations back to `REGDISPLAY`. A location into a stack save remains valid only while the corresponding stack memory remains live and addressable.

The original P23R2 comparison had zero-initialized context pointers. It reproduced a restored R13 with a null `IntegerContext[13]`, but that did not match the manager’s normal input setup. P23R3 tests both setups for `UWOP_SAVE_NONVOL_FAR` at offsets `0x120` and `0x80000`:

- With a zero-initialized pointer array, the tested Windows `RtlVirtualUnwind` restored R13 and left its pointer null.
- With `IntegerContext[13]` seeded to `&CONTEXT.R13`, it restored R13 and left that pointer at `&CONTEXT.R13`; it did not replace it with the far-saved stack slot.
- The bounded evaluator restores the same R13 value and reports the exact saved stack slot. The tests verify that dereferencing this stack-backed pointer yields the recovered value.

The original null-versus-stack mismatch came from test setup: the manager seeds the pointer to the current context register. With manager-like setup, a location mismatch remains: the bounded evaluator reports the far-save stack slot, while the tested Windows implementation leaves the pointer at the `CONTEXT.R13` field. The [RtlVirtualUnwind API reference](https://learn.microsoft.com/en-us/windows/win32/api/winnt/nf-winnt-rtlvirtualunwind) does not establish that Windows guarantees either pointer result for this encoding. The bounded stack-slot pointer is a conservative representation of the value’s actual source and is usable only while the supplied stack view remains valid. That matters to NativeAOT: the AMD64 `FEATURE_NATIVEAOT` `GcInfoDecoder::GetRegisterSlot` derives the object slot from `REGDISPLAY.pRax` plus the register index, and the NativeAOT path has no null-pointer fallback. A real GC-info fixture and root-enumeration run are still required before claiming this satisfies the runtime’s GC contract.

The R13 findings are based on hosted Windows `RtlVirtualUnwind` behavior and source inspection of the pinned runtime. They are not a guarantee about all Windows versions or a proof of GC compatibility.

## Bounded epilogue behavior

The [Microsoft x64 epilogue rules](https://learn.microsoft.com/en-us/cpp/build/prolog-and-epilog?view=msvc-170) and [x64 exception-handling description](https://learn.microsoft.com/en-us/cpp/build/exception-handling-x64) were used as the architectural reference. The new `ExecutableCodeRange` overload reads bytes only inside the caller-supplied half-open executable RVA range, the matching `RUNTIME_FUNCTION`, and the image span. It examines no more than 32 bytes or 10 instructions. A supported sequence may start at its first instruction or at a later instruction boundary, including a remaining pop or return.

Supported epilogue forms are:

- `add rsp, imm8` and `add rsp, imm32`, with positive, 8-byte-aligned amounts that match the allocation operations in the unwind record.
- `lea rsp, disp8/disp32[frame register]`, including REX.B extended frame registers. The frame register and target are checked against the unwind metadata.
- Pops of the nonvolatile integer registers represented by `UWOP_PUSH_NONVOL`, including R12–R15 and the REX.B encodings. The popped sequence must match a suffix of the unwind metadata’s push sequence.
- `ret`, aligned `ret imm16`, and `F3` return forms.
- Indirect memory tail jumps using `FF /4` with ModRM `mod=00`, including RIP-relative and supported SIB displacement forms.

The current pinned compiler output was inspected read-only in the existing PE x64 artifact `out/dotnet/phase35-managed-persistent-read/payloads/success/publish/guideXOS.Phase35ManagedPersistentReadProof.exe` (SHA-256 `41BE458D632D0F8389644286391DB1CEF7B1F49DE2C237463F6E580BB00866AC`). It contains `add rsp,0x20` followed by pops of R15, R14, R13, R12, RDI, RSI, and RBP and `ret` at VA `0x4010000022b6`; another epilogue uses `add rsp,0x98`, those pops plus RBX, and `ret` at VA `0x40100000294c`. These bytes informed the fixtures. The artifact was not rebuilt or used as Phase 35 proof.

Unsupported or rejected forms include:

- Direct register or direct relative tail jumps; indirect-jump encodings with ModRM `mod=01` or `mod=10`; malformed, truncated, out-of-range, or metadata-inconsistent sequences.
- Other instructions or unrecognized legacy prefixes mixed into an epilogue, negative or unaligned stack adjustments, and frame-register LEAs that do not match the unwind record.
- Version 2 `UWOP_EPILOG`, chained unwind records, handler flags/data, machine-frame opcodes, and other unsupported unwind encodings.

This is a bounded subset, not a general x64 instruction decoder. Unsupported candidates return an explicit error. A non-epilogue body instruction that the parser can identify as such falls back to ordinary unwind metadata; an unsupported sequence is never reported as a successful epilogue unwind. The existing overload without code bytes continues to reject a caller-classified epilogue.

## Bounds, failures, and adapter

The evaluator uses explicit image and stack spans, checked address arithmetic, half-open ranges, bounded metadata and code reads, and aligned stack-slot reads. It stages context and context-pointer changes in temporary values and commits them only on success. `UnwindCodeManagerFrameContext` mirrors the bounded `CONTEXT` and `KNONVOLATILE_CONTEXT_POINTERS` data the COFF manager consumes; `UnwindCodeManagerFrame` adapts that state and returns `Status` directly. Adapter failures leave the caller’s register state, pointer state, and result unchanged.

The internal statuses distinguish invalid context and metadata, out-of-range code access, invalid stack access, arithmetic overflow, unsupported unwind encoding, unsupported epilogue form, and success. `NoEpilogue` is an internal recognition result used to continue with ordinary metadata-based unwinding. The adapter is a testable C++ interface only: it does not change the legacy `RtlVirtualUnwind` signature, provide handler data, or register a production code manager. The legacy API returns an exception-handler pointer and has no separate validation-failure result; the upstream AMD64 manager also has other `RtlVirtualUnwind` call sites that do not accept this adapter’s `Status`.

## NativeAOT consumer compatibility matrix

| Consumer operation | Pinned manager/runtime behavior | P23R3 implementation and evidence | Remaining compatibility gap |
|---|---|---|---|
| Method lookup (`FindMethodInfo`) | Bounds the control PC by its managed range, then locates a `RUNTIME_FUNCTION` and reads per-method unwind/GC/EH data from the module image. | Bounded `.pdata` lookup validates sorted, disjoint records; code access requires a supplied executable range and function interval. Synthetic table and range tests pass. | No bounded PE-header/section parser or proof that the runtime supplies trustworthy mapped ranges and per-method COFF data bounds. |
| Frame unwind (`UnwindStackFrame`) | Calls `RtlVirtualUnwind`, then updates RSP, RIP, and the nonvolatile `REGDISPLAY` locations. The AMD64 branch does not receive an internal validation status from that call. | Version 1 unwind support plus the epilogue subset and explicit-status adapter are tested on synthetic frames; intermediate epilogue frames and multi-frame transitions are covered. | Adapter is not connected to `REGDISPLAY`, manager code, or runtime callers. A safe failure path must cover every caller, not only the adapter. |
| GC root enumeration (`EnumGcRefs`) | Decodes live GC slots and uses register pointers in `REGDISPLAY` to locate register-held roots. | The evaluator returns GPR and XMM save locations and tests saved values, pointer locations, and bounded backing storage. The pinned source confirms the AMD64 NativeAOT decoder dereferences the register slot pointer. | No real GC-info/safepoint fixture, `GcInfoDecoder::EnumerateLiveSlots`, collector callback, or lifetime test through an actual root scan. GC compatibility is unverified. |
| Outgoing-argument upper bound | On AMD64, may call `RtlVirtualUnwind` without context pointers and derives the bound from the resulting RSP. | The evaluator can return caller RSP for supported bounded frames. | The actual manager caller is untested and cannot receive this evaluator’s failure status through the current legacy call. |
| Return-address hijack info | Uses `RtlVirtualUnwind` to locate the return-address slot and reports its GC kind. | The bounded evaluator restores caller RIP/RSP for tested cases. | The manager operation and its return-address location contract are not exercised; unsupported states cannot safely be translated through the legacy call. |
| EH and funclet enumeration | Reads NativeAOT handler/EH data and uses code-manager method/funclet information during exception processing. | Handler flags and chained unwind records fail closed. No handler-data output or EH adapter is implemented. | No handler metadata, clauses, funclets, exception propagation, or cleanup/unwind test. Exception compatibility is unverified. |
| `IsUnwindable` | The upstream AMD64 manager returns true unconditionally. | The bounded evaluator can reject unsupported metadata and code forms. | Production integration must prevent an unconditional “unwindable” claim from contradicting reachable fail-closed cases. |
| Module registration | `RhRegisterOSModule` accepts the image, managed range, unboxing range, and class-library table; the Windows implementation publishes a COFF manager and then registers unboxing stubs. | No registration code was added. The separately testable adapter does not alter the external ABI. | GUIDEXOS target source selection still omits the upstream COFF manager; `RhRegisterOSModule` remains unresolved. `RegisterCodeManager` has no failure result, and unboxing registration can fail after manager publication, so ownership and partial-failure behavior need a separate design. |

The exact pinned-runtime inspection was read-only in `out/rt/src/coreclr/nativeaot/Runtime/windows/CoffNativeCodeManager.cpp`, `out/rt/src/coreclr/vm/gcinfodecoder.cpp`, the GUIDEXOS runtime source selection, and the Phase 23 registration repair report. `RhpRegisterOsModule` remains a different one-argument OS-module-list operation and is not a substitute for `RhRegisterOSModule`.

## Hosted behavioral verification

`Tests/NativeAotUnwind/run_x64_unwind_tests.ps1` builds with `g++ -std=c++17 -Wall -Wextra -Werror` into a unique system temporary directory and runs both the bounded-only and Windows-reference executables.

- Bounded-only: **51 passed, 0 failed**.
- Windows-reference: **51 passed, 0 failed**.
- Windows API evidence: **16 state comparisons** with `RtlVirtualUnwind`, plus **4 direct calls** in the two zero-seeded/manager-seeded R13 pointer probes; all passed.
- Coverage retains the P23R2 tests and adds compiler-shaped epilogues, intermediate pops, RBP and R13 frame-pointer LEAs, REX handling, imm8/imm32 stack restoration, return variants, legal indirect tail jumps, body lookalikes, bounded failures, adapter atomicity, and successive frames.

Windows comparisons are reference evidence for the tested host and synthetic inputs, not a complete correctness proof. No live NativeAOT code-manager, GC, or exception fixture was run.

## Phase 23 and next implementation frontier

`RhRegisterOSModule` remains unresolved in the GUIDEXOS target. Phase 23 did not pass, no target proof was rebuilt, and no production kernel was generated. No Phase 29–35 identity was regenerated. Phase 27–31 shim copies remain unchanged; before any later-phase synchronization, each consumer’s ABI, code-manager contract, and required unwind forms must be checked independently.

The next implementation phase must establish these contracts before production registration:

1. Build bounded PE and section parsing that supplies trusted managed-code, `.pdata`, `.xdata`, and stack ranges to the real manager.
2. Choose a failure channel for every manager operation that can unwind. Connect the explicit-status evaluator only after outgoing-argument bounds, return-address hijacking, stack walking, and `IsUnwindable` can all fail safely.
3. Exercise a real NativeAOT GC-info/safepoint fixture through `EnumGcRefs` and collector callbacks, validating each register and stack-slot address for its full lifetime.
4. Exercise handler data, EH clauses, funclets, and propagation, or explicitly reject those method forms before they become reachable.
5. Design manager ownership, publication ordering, and rollback for failure during unboxing-stub registration. Then implement a genuine target `RhRegisterOSModule`, rebuild the Phase 22/23 artifacts, and rerun the Phase 23 proof gate.

No QEMU, QAC, or physical hardware testing was performed. No changes were pushed.
