# Phase 23 NativeAOT Module Registration Repair

## Outcome

**Outcome B — P23_CONTRACT_ESTABLISHED_IMPLEMENTATION_PENDING**

The producer/consumer ABI and upstream registration responsibilities are established. The target runtime does not include a compatible code manager. The existing GUIDEXOS unwind veneer is only a partial x64 implementation and is not safe evidence for GC and exception stack walking. I did not add a symbol-only adapter or change runtime code.

## Checkout and preservation

- Repository branch: BUILD_INFRA.
- Requested starting HEAD: 0c38bc19915beefcb130afbdccf7004e29aef156.
- Actual starting HEAD in the supplied checkout: fce8ae69513375ac4975d32c990076c8234b27cd, a local BUILD1D commit descended from the requested revision. I continued from the actual checkout without switching branches, resetting, merging, or rebasing.
- Ending HEAD for this task: the local commit that adds this report; its exact SHA is listed in the final response because a commit cannot contain its own SHA. The source base for that commit is fce8ae69513375ac4975d32c990076c8234b27cd. The report is the only file added by this task.
- The primary BUILD_INFRA working tree was clean at the start. The pinned runtime cache at out/rt was already modified by 58 tracked/untracked entries from prior phase overlays. The retained recovery checkout at E:\guideXOSUEFI_BUILD_INFRA_RECOVERY_20261029\source was detached at ac92e787c9d30861869f7841340d2a790cf1eb69 and already had 53 runtime/source status entries. Both caches and all prior evidence were inspected read-only and left unchanged.
- The previous recovery report remains the provenance record for the SDK, pinned runtime checkout, Phase 19–23 overlays, and failed proof log. No Phase 29–35 staged pair was used as evidence.

## Exact producer and consumer contract

The NativeAOT bootstrapper source in the pinned runtime declares this C-linkage call:

    bool RhRegisterOSModule(
        void* pModule,
        void* pvManagedCodeStartRange, uint32_t cbManagedCodeRange,
        void* pvUnboxingStubsStartRange, uint32_t cbUnboxingStubsRange,
        void** pClasslibFunctions, uint32_t nClasslibFunctions);

It calls the function after RhInitialize and before InitializeModules. The module argument is the loaded image base. The ranges describe the managed code and unboxing-stub regions. The class-library function table and its element count are passed to the code manager. A false return aborts startup. The call uses the Microsoft x64 ABI in this build; on x64, the source does not specify a separate stdcall convention.

This is distinct from the pinned runtime's RhpRegisterOsModule import. RuntimeImports.cs declares that import with one IntPtr argument and an IntPtr result. RuntimeInstance.cpp implements it by allocating an OsModuleEntry and adding the supplied OS module handle to the runtime's OS-module list; it returns the handle on success and null on allocation failure. It does not register a code manager, managed address range, unboxing stubs, PE exception data, or the class-library function table. RhpCreateTypeManager is a separate four-argument operation called later to create a TypeManager from each module header. Similar names do not make these calls ABI- or behavior-compatible.

The upstream Windows implementation of RhRegisterOSModule in CoffNativeCodeManager.cpp constructs a CoffNativeCodeManager from the PE image base, managed range, PE exception directory, and class-library functions. It registers the manager and managed range with RuntimeInstance, registers the unboxing-stub range, retains the manager for process lifetime, and returns success. The upstream Unix implementation has the same public parameter contract but constructs UnixNativeCodeManager for its target format and unwinding model. Neither implementation is a generic one-argument forwarding wrapper for RhpRegisterOsModule.

## Root cause and target compatibility

The GUIDEXOS target branch in the patched runtime Runtime/CMakeLists.txt adds the GUIDEXOS GC environment, but adds neither windows/CoffNativeCodeManager.cpp nor unix/UnixNativeCodeManager.cpp to FULL_RUNTIME_SOURCES. The target Runtime.WorkstationGC.lib therefore contains RhpCreateTypeManager and RhpRegisterOsModule but no RhRegisterOSModule and no CoffNativeCodeManager. The host archive does contain CoffNativeCodeManager and RhRegisterOSModule; it is a Windows host artifact and cannot establish target-pack inclusion.

The target archive at the retained recovery output has SHA-256 C62E0737D064B03FB5150F1789246BCCB0B69B089F72CF33C08FF84382C94273. A read-only dumpbin symbol scan found RhpCreateTypeManager and RhpRegisterOsModule, but no RhRegisterOSModule, CoffNativeCodeManager, or UnixNativeCodeManager. The host archive hash is 0E6A134AD4150CD604317A47860DAE82EB30AAE4D9CDB14144E06454E7BB1948; its symbol table includes the COFF manager and RhRegisterOSModule. This host/target split agrees with the prior Phase 23 link failure.

The phase shim also has no RhRegisterOSModule definition. The preserved proof log reports:

    bootstrapper.obj : error LNK2019: unresolved external symbol RhRegisterOSModule referenced in function wmain
    fatal error LNK1120: 1 unresolved externals

That log is SHA-256 596D45B49EFB2F77C3014EF54D0B73EFA70E83AE73A3DCED533BA787172F2C5D. The proof was not rerun because no compatible implementation was added.

## Required registration architecture and safety gap

The registration call must publish a code manager and managed range before InitializeModules creates type managers. RuntimeInstance currently stores a single code-manager pointer and one managed range. GC reference enumeration, method lookup, class-library function lookup, associated-data lookup, stack walking, and managed EH rely on that manager. Unboxing stubs are registered separately. The runtime and module entries are retained for the kernel/process lifetime; the inspected runtime has no corresponding unload path.

A loaded UEFI PE/COFF image can supply an image base and PE exception directory without Windows loader services. That makes the upstream COFF manager the relevant candidate for this target, but it calls RtlVirtualUnwind for x64 frame unwinding. The Phase 23 shim contains a custom image-local x64 unwind veneer, but its current implementation is explicitly bounded to the earlier managed proof and is not demonstrated equivalent to the Windows contract. In the inspected source it skips SAVE_XMM128 restoration, does not populate floating-point nonvolatile context pointers, ignores unsupported unwind operations, does not validate image/stack ranges or unwind-code bounds, and does not propagate malformed or unsupported metadata as failure. It also does not provide handler data. Linking the COFF manager against this veneer would therefore not establish correct GC or EH behavior.

The upstream registration helper also publishes the code-manager pointer before registering unboxing stubs. RegisterUnboxingStubs can fail after that publication, while RegisterCodeManager has no failure result or rollback operation. The GUIDEXOS adapter must validate module/range arithmetic and registration state, preserve manager ownership on all paths, and define fail-closed behavior for partial registration. This failure path needs an explicit design rather than inheriting an unsafe dangling-pointer possibility.

## Repair decision

No implementation was selected. Forwarding RhRegisterOSModule to RhpRegisterOsModule would only add an OS-module-list entry and would silently omit code-manager, managed-range, and unboxing-stub registration. Adding the upstream COFF source alone would rely on the incomplete unwind veneer and would not meet the required GC/EH contract. The safe next implementation is a reviewed GUIDEXOS PE/COFF registration path using either a fully qualified x64 unwind implementation or another supported runtime interface, with bounded PE metadata validation and atomic/fail-closed publication. This is a substantial runtime integration task, so the Phase 23 gate remains blocked.

## Hosted verification

- Confirmed the bootstrapper's seven-argument C ABI and call ordering in the pinned source.
- Confirmed the different one-argument RhpRegisterOsModule import and its OS-module-list behavior in RuntimeImports.cs and RuntimeInstance.cpp.
- Inspected the upstream Windows and Unix registration implementations and RuntimeInstance registration ownership.
- Inspected the target GUIDEXOS CMake source selection and the shim's unwind veneer.
- Inspected the host and target archive symbol tables separately. These checks are structural only; they do not verify GC, EH, or runtime stack walking.
- The preserved Phase 23 proof link fails with LNK2019/LNK1120 as stated above. It was not rerun in this task.
- No Phase 23 behavior tests were added or run because there is no safe target registration implementation to exercise. No Phase 23 proof verifier, image inspection, or loader probe was run on a newly produced artifact. The result is not a passing Phase 23 proof.

Relevant source/evidence hashes:

| Input | SHA-256 |
|---|---|
| Pinned runtime commit | 9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3 |
| Runtime bootstrapper main.cpp in retained pinned checkout | 0148E453929A19B85AB2B5777F1A3B452DEAF8E0251C5455AFCF85765A53F21B |
| Upstream CoffNativeCodeManager.cpp | 867ABAFD8430253358C4792DD6D53523D984F878260C38676C69F99EA04836E5 |
| Upstream UnixNativeCodeManager.cpp | A5050EEBB666BD05F6DC9C34262F46783B6CAC5FEF1456516691CF337720F3DD |
| RuntimeInstance.cpp | E65306D911B9BFA4C89AB007581EC26D5E30B08D31536D69ABBD84A6AB6E769A |
| Patched runtime Runtime/CMakeLists.txt | 37A9C70CC54EB3B1056CB649E7DDE08EB84D203AB58E97BBC0E428CB0C2C810E |
| Phase 23 GUIDEXOS link shim | FCE3F0566FDC415B6622AC33D67B18677539C133AF7D619DE26C2F3AE1D9D46D |
| Failed proof log | 596D45B49EFB2F77C3014EF54D0B73EFA70E83AE73A3DCED533BA787172F2C5D |
| Target Runtime.WorkstationGC.lib | C62E0737D064B03FB5150F1789246BCCB0B69B089F72CF33C08FF84382C94273 |

The runtime file hashes refer to the retained recovery runtime at the pinned commit after the documented Phase 21–23 overlays; they are source-inspection identifiers, not claims that the dirty primary cache is clean. No binary archive was edited.

## Downstream and required status

| Gate | Result |
|---|---|
| RhRegisterOSModule resolves in target pack | No |
| Real target module-registration behavior implemented | No |
| Phase 23 proof link and verification | Blocked; preserved proof link fails |
| Phase 29–35 records generated | No |
| ManagedArtifactIdentities.g.cs generated | No |
| Production kernel built | No |
| QEMU / QAC used | No / No |
| Changes pushed | No |

The 41 existing Phase 29–35 staged pairs were not admitted or used. Identity generation and the production kernel remain blocked by the Phase 23 prerequisite. The responsible next step remains in BUILD_INFRA: implement and qualify a complete GUIDEXOS COFF code-manager/unwind path, then rebuild the Phase 22/23 artifacts from a clean pinned checkout and rerun the supported Phase 23 link and verification gates. QEMU and QAC remain outside this hosted assignment.
