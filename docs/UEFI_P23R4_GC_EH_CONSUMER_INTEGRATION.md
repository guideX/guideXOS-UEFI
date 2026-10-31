# P23R4 — NativeAOT GC/EH Consumer Integration

**Outcome: `P23R4_GC_CONSUMER_PARTIAL` (Outcome B).** A pinned NativeAOT GC-info fixture was consumed by the genuine Windows COFF code manager in a hosted harness. Live register and stack roots, callback slot writes, a multi-frame walk, typed EH metadata, catch-funclet association, and the code manager's R13 saved-slot behavior passed. Production failure handling and safe manager publication remain unimplemented, so the GUIDEXOS code-manager connection stays disabled and Phase 23 remains blocked.

## Repository state

- Checkout: `D:\dev\guideXOSUEFI_BUILD_INFRA`
- Branch: `BUILD_INFRA`
- Starting HEAD: `a80e735b8af87ddec9d361578be003ee8ba1d015`
- Ending HEAD: the local commit containing this report. Its object ID is reported in the task's final summary because a commit cannot contain its own ID.
- The pinned `out/rt` checkout remained at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, with the same 58 pre-existing status entries before and after fixture generation. The fixture build record verifies this.
- `MODERN_INPUT` was not modified. No branch switch, merge, rebase, reset, cherry-pick, or push occurred.

The three Phase 23 registration, R3, and unwind reports were read before implementation. The current P23R3 evaluator, adapter, and runner were retained.

## Pinned consumer contract

| Consumer requirement | Pinned NativeAOT behavior and P23R4 evidence | Remaining boundary |
|---|---|---|
| Context and `REGDISPLAY` | AMD64 `REGDISPLAY` carries pointers for integer registers, `SP`, `IP`, Windows `SSP`, and preserved XMM values. RSP has no register pointer. The real manager consumed the fixture with this layout. | P23R3's bounded adapter is not wired into the target runtime manager. |
| Instruction and stack ownership | The iterator supplies the current frame's `IP`/`SP`; the COFF manager finds the `RUNTIME_FUNCTION`, uses the main method start to compute the code offset, and writes the caller `IP`/`SP`. The hosted test verified an `Inner` to `Outer` walk. | The Windows API path does not provide an ordinary unwind-failure status. |
| Preserved registers and locations | The manager seeds `CONTEXT` and `KNONVOLATILE_CONTEXT_POINTERS` from `REGDISPLAY` and calls `RtlVirtualUnwind`. A test-only R13 unwind record made the actual manager recover R13 into the writable stack save slot and retain that location in `REGDISPLAY`. | No compiler-generated live R13 GC root appeared in this fixture; the R13 unwind probe is reported separately from authentic GC-info evidence. |
| GC-info lookup and safepoints | `FindMethodInfo` bounds the code address by the managed range and locates the method. `GetCodeOffset` advances past x64 unwind data, UBF flags, associated data, and EH RVA. `IsSafePoint` uses the pinned `GcInfoDecoder`. The harness verified generated offsets `0x19`, `0x23`, and `0x10`. | `EnumGcRefs` is `void`; decoder failure has no ordinary failure channel to the caller. |
| Root callback and slot ownership | `EnumGcRefs` decodes live slots and invokes the supplied callback. The fixture yielded RBX/RSI/RDI register roots and stack roots; six reported slots were overwritten and read back through the callback-provided addresses. | This is a simulated root relocation, not a compacting collection. No real GC ran. |
| EH data | `EHEnumInit` finds EHInfo through the method's UBF flags and relative RVA; `EHEnumNext` decodes varint clauses. The harness checked its data cursor at RVA `0x94f26`, decoded one typed catch clause, and checked try, handler, and target-type bounds. | EH varint reads are not bounded by an image view. Invalid-record rejection was not safely testable through this API. |
| Funclets and transition frames | `IsFunclet` and `GetMethodStartAddress` associated a generated catch funclet with `CatchFixture`. The normal nested-frame walk passed. `StackFrameIterator` separately handles funclet collapsing and transition frames. | No reverse-P/Invoke transition frame or complete exception dispatch was exercised. |
| Failure propagation | `StackFrameIterator::NextInternal` failfasts if `UnwindStackFrame` returns false. The Windows COFF implementation calls `RtlVirtualUnwind` and returns true; `IsUnwindable` also returns true unconditionally. The existing bounded evaluator does reject malformed/truncated inputs transactionally. | The legacy manager cannot report an invalid unwind to stop GC enumeration. No actual-manager malformed-image or “no roots after failed unwind” test is safe or meaningful without a status-bearing boundary. |
| Registration and lifetime | `RhRegisterOSModule` constructs the manager, calls `RegisterCodeManager`, then registers unboxing stubs. `RuntimeInstance::RegisterCodeManager` only asserts and stores the manager/range. Unboxing registration can fail after manager publication; `NewHolder` then releases a manager already referenced by `RuntimeInstance`. | There is no verified atomic manager-plus-unboxing publication or rollback protocol. P23R4 did not call registration. |

Pinned source anchors: `out/rt/src/coreclr/nativeaot/Runtime/windows/CoffNativeCodeManager.cpp` (method lookup at 375, GC enumeration at 434, unwind at 651, `IsUnwindable` at 842, EH enumeration at 1038/1073, module registration at 1179); `out/rt/src/coreclr/nativeaot/Runtime/StackFrameIterator.cpp:1565`; `out/rt/src/coreclr/vm/gcinfodecoder.cpp:1473`; and `out/rt/src/coreclr/nativeaot/Runtime/RuntimeInstance.cpp:209` and `:224`.

## Authentic fixture and provenance

`Tests/NativeAotUnwind/GcEhFixture/Program.cs` creates a mixed-live/dead `RootNode` fixture. `Outer` keeps seven object references alive at the nested `Inner` call, consumes a dead reference before that point, and later drops another reference after the nested call. `Inner` makes a nested call. `CatchFixture` and `ThrowingCall` produce an authentic typed catch and funclet. The test binary was never executed.

The reproducible output is under `out/dotnet/phase23r4-gc-eh-fixture-reproduction/`. The build used NativeAOT/compiler packages 9.0.0 from the private Phase 23 feed, target `guidexos-x64`, .NET SDK 10.0.403, and MSVC 14.51.36231. The package hashes are recorded in `gc-eh-fixture-build.json`; the runtime source was `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`.

| Preserved evidence | SHA-256 |
|---|---|
| NativeAOT PE fixture | `D9EDE8AFC32D73504D718A9A752C921FE8E2294B0627B1CF276E846A64878DCD` |
| Native object with generated code and GC/EH records | `1C5D85F7D45978836E5DEB38F78600F32559F618CA7ADACCA48EEE0DEFE67B0A` |
| ILC map XML | `2DFF0AE8633AA75AC63EB5B615C71671FFB1243857F534F6A078AAD61D81129E` |
| Linker map | `F8DCFF0B33258F2637006BDC1F9A9245C6B87F7F40E43AA15304864F8D9B55DF` |
| Host `Runtime.WorkstationGC.lib` containing the pinned COFF manager | `0E6A134AD4150CD604317A47860DAE82EB30AAE4D9CDB14144E06454E7BB1948` |

The host archive is the genuine Windows NativeAOT 9.0.0 COFF manager used for hosted consumer calls; it is not the GUIDEXOS target archive. The target `Runtime.WorkstationGC.lib` extracted from this fixture feed has SHA-256 `E486351F470C4562C217A24938BC070EBD8AE112CF6D7EF97770CA6A2056E50F` and contains `RhpRegisterOsModule`, but no `RhRegisterOSModule` or `CoffNativeCodeManager`.

The linker map for the metadata-only fixture does contain a `RhRegisterOSModule` symbol from the feed's prebuilt `guidexos_link_shim.obj` (SHA-256 `4A381D6F25A675BB86BE850C8F8C0C4FB7910F7D890DDE60B5281EE4BC89345C`). That binary does not match the checked-in `Tools/Phase23/guidexos_link_shim.cpp`, which has no such definition. It is an existing package/source mismatch, not a production code-manager export, and the fixture was never run. Therefore this PE link does not resolve the Phase 23 registration gate. The Phase 23 proof remains at the preserved `bootstrapper.obj` unresolved `RhRegisterOSModule` failure described in `docs/UEFI_PHASE23_MODULE_REGISTRATION_REPAIR.md`.

The fixture link also included the existing Phase 26 syscall veneer only to satisfy the target PAL `guidexos_pal_syscall5` import. Its source hash and object hash are in the build record. Neither shim nor fixture execution was used as registration or kernel evidence.

## Hosted results

### Retained bounded evaluator and Windows reference

`Tests/NativeAotUnwind/run_x64_unwind_tests.ps1` passed both hosted builds:

- Bounded-only: **51/51 passed**.
- Windows-reference build: **51/51 passed**, with **20** `RtlVirtualUnwind` reference comparisons.

The retained suite covers bounded image/stack reads, malformed/truncated unwind data, epilogues, rollback on failure, and the P23R3 R13 far-save probes. These are results for the bounded evaluator/reference suite, not authentic GC-info decoding.

### Genuine COFF manager with authentic GC-info

`Tools/Phase23/run_gc_eh_consumer_tests.ps1` called the host archive's actual `CoffNativeCodeManager` methods against the generated target PE and linker map. It verified:

- `Main`, `Outer`, `Inner`, `CatchFixture`, and `ThrowingCall` were identified at their generated RVAs.
- At `Outer` offset `0x19`, the manager reported seven roots: RBX, RSI, RDI, and four stack roots. At `0x23`, it reported six roots: RSI, RDI, and four stack roots. The count transition verifies dead/expired roots are omitted at later safepoints. `Inner` offset `0x10` reported one RBX root.
- Six GC callback-reported storage locations were updated and read back successfully.
- The real manager unwound the synthetic `Inner` frame to `Outer`, then enumerated roots in both frames from the same `REGDISPLAY` storage.
- One typed EH clause was decoded; the EH-info cursor, handler, and target type were inside the mapped image. The real manager found 470 funclets in the managed code range and associated one with `CatchFixture`.

This verifies root enumeration and writable slot behavior. It does not demonstrate a real compacting GC, managed execution, or exception propagation.

### R13 location probe

The authentic fixture did not allocate a live GC root to R13. A separately labeled probe therefore overlaid a valid test-only x64 unwind record on `Inner` while retaining and reusing its compiler-generated GC-info bytes. Through the actual COFF manager unwind method, the seeded R13 value was recovered, `REGDISPLAY.pR13` selected the writable stack save slot, and a write through that slot succeeded. This resolves the consumer location question for a saved R13: the GC decoder consumes the writable location in `REGDISPLAY.pR13`; its value must be the recovered caller R13. The probe is synthetic unwind metadata and is not counted as an authentic R13 GC root test.

The Windows-reference unit probe seeded `CONTEXT_POINTERS.R13` with a pointer to the `CONTEXT` field, while the manager probe seeded the external `REGDISPLAY.pR13` location. Those are different inputs; the manager probe confirms the stack-backed location the bounded evaluator reported for this saved-register case. No behavior was changed to imitate the different Windows test setup.

### Failure and publication behavior

The bounded evaluator's failure tests do not establish failure propagation through the pinned manager. Its Windows unwind interface returns no status, `IsUnwindable` is unconditional, and `EnumGcRefs` cannot report failure. EH metadata reads also have no bounded image argument. A null `HandlerData` from an unwind without handlers is not a validation signal and was not treated as one. Consequently P23R4 did not inject malformed metadata into the actual manager, did not continue GC enumeration after an invalid unwind, and did not test registration rollback by calling the non-transactional API.

The hosted harness constructs a manager directly and calls consumer methods. Registration helper link stubs abort if unexpectedly called; no code manager was published. No target registration patch or fallback was added.

## Phase 23 status and blockers

- `RhRegisterOSModule` in the GUIDEXOS target runtime archive: **absent**. The fixture map's same-named symbol is from the mismatched prebuilt shim object described above.
- Phase 23 proof: **not passed**; the existing unresolved-symbol failure is preserved.
- Production code-manager connection and publication: **disabled/not performed**.
- Production kernel or UEFI image: **not built**.
- QEMU, QAC admission, and physical hardware: **not run**.
- Phase 29–35 evidence: **not generated or changed**.
- Push: **none**.

P23R4 is partial because genuine GC-info was consumed and authentic GC, stack, and EH metadata paths were exercised, but status-bearing fail-closed behavior, safe registration, authentic R13 GC-root coverage, and full EH propagation remain open.

## Exact P23R5 objective

Implement a GUIDEXOS code-manager boundary that validates the PE managed range, `.pdata`/`UNWIND_INFO`, GC-info, and EH-info before use; provides status-bearing unwind, root-enumeration, and EH paths that stop before enumerating roots after any invalid read; and publishes the manager and unboxing-stub range atomically only after successful validation. Verify it with authentic NativeAOT fixtures that include a live R13 GC root, malformed and truncated metadata cases, root relocation updates, EH/funclet frame relationships, and registration rollback, then require the genuine Phase 23 proof to pass before enabling production registration.

## Changed source files

- `Tests/NativeAotUnwind/GcEhFixture/guideXOS.NativeAotGcEhFixture.csproj`
- `Tests/NativeAotUnwind/GcEhFixture/Program.cs`
- `Tests/NativeAotUnwind/code_manager_consumer_tests.cpp`
- `Tools/Phase23/build_nativeaot_gc_eh_fixture.ps1`
- `Tools/Phase23/run_gc_eh_consumer_tests.ps1`
- `docs/UEFI_P23R4_GC_EH_CONSUMER_INTEGRATION.md`
