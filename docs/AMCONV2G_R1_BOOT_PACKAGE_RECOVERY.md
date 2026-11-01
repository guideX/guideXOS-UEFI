# AMCONV2G-R1 Boot Chain Recovery and Offline UEFI Guest Package Qualification

**Outcome C — AMCONV2G_R1_BUILD_BLOCKED**

**Qualification date:** 2026-11-01
**Branch:** APP_MODEL

## Decision

The production UEFI loader was rebuilt from the checked-in APP_MODEL project and validated as an x64 EFI application. A clean authentic kernel build is blocked at native link by the unresolved NativeAOT helper RhpInitialDynamicInterfaceDispatch. The linker response file forces unresolved symbols, so the resulting PE image is explicitly rejected and was not converted to ELF or staged.

Two isolated partial ESP folders were retained. Each has the rebuilt loader and its already-qualified RDSK payload, but neither has kernel.elf. They are not bootable guest candidates and have no complete-package hash. No guest execution was attempted.

The shortest valid recovery is to resolve the kernel's NativeAOT runtime/target contract for RhpInitialDynamicInterfaceDispatch, or make the relevant kernel code compatible with the supported static bare-metal runtime design. Then perform a clean native link without unresolved-symbol forcing, convert that authentic kernel through the supported KMain map path, validate the ELF, and assemble the two isolated ESPs.

## Revision, inputs, and repository state

- Starting HEAD: 5fcbd8841ae715772a963b35a6d377cf5e63247e.
- Source HEAD used for this recovery: 5fcbd8841ae715772a963b35a6d377cf5e63247e.
- Branch remained APP_MODEL; no branch switch occurred.
- The existing AMCONV2G payloads were previously built and qualified at code commit 3ba2a104b1096f8c20c4460b50b348754bfc662b. Comparing that commit to the requested starting HEAD shows only the acceptance report was added; no source or build files changed between those snapshots. Candidate A was built as RDSK v1 from the production source plus the bounded fixture. Candidate B was built by the production builder in v2 mode from that same payload set. The prior acceptance report records the staged Native source and compiled identity input.
- Both supplied image hashes were recomputed before staging and matched:
  - Candidate A, out/amconv2g-20261101-r1/candidate-a-full-rdsk-v1.img: 56,431,471 bytes; SHA-256 B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E.
  - Candidate B, out/amconv2g-20261101-r1/candidate-b-full-rdsk-v2.img: 56,435,018 bytes; SHA-256 18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4.
- The tracked release ramdisk.img was not changed; its SHA-256 remains F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8.
- At start, the worktree contained six untracked test bin/obj directories under AMCONV2D, AMCONV2E, and AMCONV2F. They were preserved. At finish, the only tracked change is this report; those same untracked directories remain.
- The repository ESP initially contained only startup.nsh (29 bytes). It remains unchanged. Root-level kernel.elf and a current root ESP loader were absent.
- Generated build outputs, logs, audit CSVs, and partial candidate folders remain under the ignored out/amconv2g-20261101-r1 tree.

## Supported boot chain and dependency graph

The supported production pipeline is build.ps1:

1. Release/x64 builds guideXOSBootLoader/guideXOSBootLoader.vcxproj with the EFI application subsystem, efi_main entry point, EDK2 headers, and the checked-in NASM handoff trampoline.
2. A NativeAOT Release publish of guideXOS/guideXOS.csproj creates the native kernel PE image. The project targets net7.0/win-x64 and sets KMainWrapper as the NativeAOT/link entry contract.
3. Tools/pe_to_elf_v2.py converts the kernel PE to ELF64. The build pipeline uses guideXOS/Kernel.map when available and selects KMain as the exported kernel entry.
4. The selected RDSK image is built or explicitly selected and audited for its expected version and managed-artifact identities.
5. The script stages EFI/BOOT/BOOTX64.EFI, kernel.elf, and ramdisk.img at the ESP root. RDSK v1 is the default. RDSK v2 is opt-in through RamdiskVersion 2 and an explicit or separate v2 image path; the normal ramdisk.img path rejects v2.

The loader is a UEFI application, not the kernel. Its source opens root-level kernel.elf and ramdisk.img, parses ELF load segments, loads the RDSK into loader data pages, prepares BootInfo, exits Boot Services, and transfers to the kernel through BootHandoffTrampoline. The intended kernel is ELF64, little-endian, x86-64 ET_EXEC with an executable entry point inside a load segment.

The kernel entry contract is KMain/KMainWrapper using the Microsoft x64 ABI; BootInfo is passed in RCX. Kernel/Misc/EntryPoint.cs exports KMain, validates BootInfo, and enters Program.KMain. NativeAOT runtime code is linked into the kernel image. Managed application binaries and their .gxmi descriptors are payloads inside RDSK; they are not separate firmware-side runtime assets.

Phase 23's private guidexos-x64 NativeAOT 9.0 pack is for managed application proof binaries, not the kernel. Its verified manifest SHA-256 is 0DCB73786DA532CAE9D17341DF0FA644EE01BBE41BF0BC8E63A084C31E4792F4 and its target pack SHA-256 is 20E5CB565B6560231B5C462A3593FA1FBE145D0D03B5BC7BA83845FCB1F1649E. It depends on a private ILC source patch, a Phase 19 PAL contract, workstation GC, source adaptations, and a link shim. Phase 23 does not authorize or implement managed CPL3 execution, and it does not provide the kernel's missing helper. The Phase 23 library was not substituted into the kernel link.

The converter and loader have different validation depth. The converter emits an x86-64 ET_EXEC ELF and prefers KMain from the map. The loader's ELF source validates ELF magic, ELF64 class, load segments, and entry bounds. A complete offline validator must additionally assert ELF machine, ET_EXEC type, and that the entry is inside an executable load segment.

## Loader recovery

Built the checked-in loader project in Release/x64 using VS 18 Community MSBuild, toolset v143, repository EDK2 headers, and Tools/nasm.exe. Build outputs were redirected to the isolated recovery directory. No external loader was imported.

- Output: out/amconv2g-20261101-r1/recovered/loader/bin/guideXOSBootLoader.exe.
- Build result: success, exit 0.
- Size: 49,664 bytes.
- SHA-256: C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9.
- PE machine: AMD64, 0x8664.
- Optional header: PE32+, 0x020B.
- Subsystem: EFI Application, 10.
- Entry RVA 0x54E0 lies in executable .text.
- PE import directory: RVA 0, size 0; the executable uses UEFI firmware services and has no PE import table.
- Sections: .text, .rdata, .data, .pdata, .rsrc.
- Build warnings included MSVC D9025 option overrides and EDK2 C4162 _ReturnAddress diagnostics. There were no link errors.

The loader was copied to both partial layouts and its post-copy hash matched the build output. A first isolated build pass used a project-relative OutDir and wrote under the loader project; that generated tree was moved intact into the ignored recovery area after verifying both paths were inside this workspace. No source, pre-existing untracked output, or protected checkout was removed or overwritten.

## Kernel build and exact blocker

Attempted an isolated Release AppRuntime NativeAOT publish using the installed SDK 10.0.401 with the project's pinned .NET 7 NativeAOT compiler package, x64 VC build environment, and isolated intermediate/output directories. C# compilation completed with 137 warnings. The native link failed:

- guideXOS.obj: LNK2001 unresolved external symbol RhpInitialDynamicInterfaceDispatch.
- LNK4088: an image was emitted because /FORCE:UNRESOLVED was present; the linker warned that it may not run.
- LNK4281: x64 base-address optimization warning.
- MSBuild surfaced the link failure as MSB3077 and the publish exited 1.
- The response file is out/amconv2g-20261101-r1/recovered/kernel-native/obj/native/link.rsp; SHA-256 9D282B399730C339D0174FF5380E680CB1B8541C57587C1A69D4862645E76736. It includes /FORCE:UNRESOLVED, /INCLUDE:KMainWrapper, and /ENTRY:KMainWrapper. It contains no Phase 23 runtime pack library.

The forced-link PE is not a qualified kernel artifact. It was not passed to the converter and was not copied into either ESP. Root kernel.elf remains absent. Kernel/Drivers/Input/MouseInputManager.cs already documents the bare-metal NativeAOT limitation around RhpInitialDynamicInterfaceDispatch. The produced file is retained only as failed-build evidence under the isolated ignored output directory.

Historical files were inspected by executable headers, not filename:
- bin/Release/net7.0/win-x64/guideXOS.exe is x64 PE32+ with Windows console subsystem 3, a nonzero import directory, and no CLR directory. It is a hosted executable, not this UEFI kernel.
- The failed native publish output is x64 PE32+ with native subsystem 1 and no imports, but is rejected because the link had an unresolved symbol and /FORCE warning.
- kernel_py.elf and kernel_test.elf are structurally ELF64 little-endian x86-64 ET_EXEC images with entry points inside executable load segments. Their source/build provenance does not establish compatibility with the current APP_MODEL kernel, so they were not reused.
- kernel2.elf and kernel3.elf do not have ELF magic; they have identical hashes. Neither is a valid substitute.

This is an authentic build dependency failure, not a missing copy step or an ESP staging omission. A cleanly linked kernel is the blocking prerequisite.

## Partial ESP candidates and offline checks

The original RDSK payload candidates were preserved. The following isolated layouts were created. Each has exactly two payload files: the loader at EFI/BOOT/BOOTX64.EFI and the selected RDSK at root ramdisk.img.

| Candidate | Partial ESP path | Loader SHA-256 | Root ramdisk.img SHA-256 | Payload bytes |
|---|---|---|---|---:|
| A, v1 | out/amconv2g-20261101-r1/candidate-a-esp-partial-rdsk-v1/ESP | C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9 | B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E | 56,481,135 |
| B, v2 | out/amconv2g-20261101-r1/candidate-b-esp-partial-rdsk-v2/ESP | C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9 | 18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4 | 56,484,682 |

Both staged ramdisk hashes match their original source candidate. Both staged loader hashes match the rebuilt loader. Each layout is missing ESP/kernel.elf. Neither has a complete ESP tree hash, disk image hash, or guest-readiness result. The original root ESP was not used as a staging target.

Offline checks completed:

- Loader PE/COFF architecture, PE32+ format, EFI subsystem, executable entry section, and import directory were inspected.
- Input RDSK A and B hashes were reverified before use; post-copy hashes match.
- Production managed-artifact audits were run for expected v1 and v2. Each audit contained 41 managed artifacts and reported 41/41 matches for build-to-staging, descriptor-to-executable, compiled-allowlist-to-executable, and ramdisk-to-source identity checks.
- Existing AMCONV2G evidence records Candidate B's repeat build as byte-identical to the original v2 candidate. It was not rebuilt during R1.
- RDSK parser, generator, and hosted inspection suites are listed below.

Incomplete checks because kernel.elf is unavailable: loader/kernel path agreement in a complete package, kernel ELF architecture/type/entry verification, required-file completeness, whole-package structure and size, no-conflict audit across a complete ESP, deterministic complete-package repeat, and any guest behavior. These partial folders are not bootable disks or complete ESP candidates. Offline structural checks cannot prove UEFI boot.

## Compile and regression results

Isolated Release compile target summaries using SDK 10.0.401:

| Configuration | Errors | Warnings | Prior baseline |
|---|---:|---:|---:|
| AppModel | 0 | 133 | 0 errors / 135 warnings |
| AppRuntime | 0 | 137 | 0 errors / 139 warnings |

The AppRuntime compile target remains buildable and the existing UEFI_DIAGNOSTIC_APP_RUNTIME instrumentation is still present. The kernel NativeAOT publish also completed C# compilation with 137 warnings, but did not complete native linking.

Hosted regressions rerun in this recovery:

| Suite | Result |
|---|---|
| AMCONV1 source compatibility | 9/9 pass |
| AMCONV2B/C source checks | 25/25 pass |
| AMCONV2B/C folder and mount runtime | 68 assertions, 0 failures |
| AMCONV2D FAT inspection | 11 assertions, 0 failures; two existing CA2014 warnings |
| AMCONV2E RDSK v1 inspection | 32 assertions, 0 failures |
| AMCONV2F source checks | 9/9 pass |
| AMCONV2F v2 generator and rejection cases | 12 assertions pass |
| AMCONV2F reader/runtime, including v1 compatibility | 58 assertions, 0 failures |
| AMCONV2G packaging | 4/4 pass |
| Phase 33 wire regression | pass; request 2,084 bytes, response 16 bytes |
| Desktop interaction | 42 assertions pass using the AnyCPU test DLL under .NET 8.0.33 x86; the x64 apphost could not locate an installed .NET 8 x64 runtime |
| Task Manager Memory Details regression | checked-in script passed |

The audits and compile logs are under out/amconv2g-20261101-r1/recovered. No test modified protected source trees. No new test source or runtime subsystem code was added.

## Diagnostic coverage and later guest handoff

UEFI_DIAGNOSTIC_APP_RUNTIME remains a compile-time build variant. Its markers observe the existing production services and lifecycle code; they do not replace the production lifecycle. R1 did not observe any of these in a guest.

For a later authorized run, use the same accepted RDSK payloads and rebuild/package from the resolved APP_MODEL kernel source with UefiDiagnosticMode AppRuntime. Keep v1 at the normal default selection; choose v2 only through the explicit v2 option and the Candidate B input. Copy each completed production ESP into a unique run directory and re-hash every staged file. Do not use either partial directory listed above as a guest input.

Candidate payload references for that later run:

- Candidate A input: out/amconv2g-20261101-r1/candidate-a-full-rdsk-v1.img; SHA-256 B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E.
- Candidate B input: out/amconv2g-20261101-r1/candidate-b-full-rdsk-v2.img; SHA-256 18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4.
- Rebuilt loader input: out/amconv2g-20261101-r1/recovered/loader/bin/guideXOSBootLoader.exe; SHA-256 C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9.

Before any guest run, the responsible APP_MODEL operator must use the supported QAC status/list/inspection workflow to verify the current coordinator, exclusive acceptance reservation, owner, managed guest process, and time window. Require explicit current baton authorization for APP_MODEL. Prior reservation details in older reports are historical and are not current authorization. Do not launch QEMU directly or request/change a reservation as part of this report.

The checked-in run_qemu.ps1 requires a complete root ESP and invokes build.ps1 before launch. Its reviewed profile uses x86_64 QEMU, OVMF code and writable variables firmware, machine pc-q35-8.2, 1024 MB memory, serial stdio, and no-reboot. The local script expects firmware files under C:\Program Files\qemu and QEMU at C:\Program Files\qemu\qemu-system-x86_64.exe. It was not run. A QAC-managed run must use the current approved QAC workflow and isolated writable firmware variables for each run.

Proposed bounded acceptance runbook (time limits here are operational caps for R2, not previously established product SLAs):

1. Verify QAC ownership/authorization and record the current reservation metadata; verify both complete package manifests and hashes before admission.
2. Boot Candidate A with the AppRuntime diagnostic build. Allow at most 120 seconds for [KMAIN] INITIALIZE and APP_RUNTIME_MODEL_READY. Capture serial from process start.
3. Exercise shell root-folder activation, navigate the bounded fixture directory and back, create a second Computer Files instance, close each window, and verify instance ownership/termination. Allow at most 30 seconds for each requested lifecycle transition and cap the candidate run at 10 minutes.
4. For a mount replacement/disposal assertion, identify a production owner path that both replaces the active filesystem and calls Dispose on the retired filesystem while a Computer Files window holds a lease. Confirm the old generation remains valid for that window, new acquisitions use the replacement generation, and closing the old window emits FILES_MOUNT_RELEASE_END with leases=0 and valid=0. FileSystemMountLifetime.SetActiveFileSystem only swaps the active reference; the Disk Manager's current Set FS actions do not dispose the previous filesystem. If no genuine path requests disposal on the retired mount, record deferred disposal as not exercised/blocked, not passed. Do not simulate it with a synthetic lifecycle implementation.
5. Repeat the same sequence on Candidate B. Keep the v1 and v2 serial traces and screenshots separate.
6. Save the package manifest, QAC run/reservation metadata, serial log, screenshots, exact action timeline, and terminal process status. Any expired cap is a timeout, not a pass. Classify firmware/loader failure, kernel-transfer failure, managed-startup failure, lifecycle assertion failure, and timeout separately.

Expected serial markers include:

- Early handoff: raw K!, [KMAIN] INITIALIZE, [FRAMEBUFFER] INITIALIZE, SMAIN_DISPATCH_REASON=APP_RUNTIME, APP_RUNTIME_MODEL_READY=apps=...
- Shell/folder: SHELL_FOLDER_REQUEST, SHELL_FOLDER_RESULT, FOLDER_BIND_BEGIN, FOLDER_MOUNT_ACQUIRE, FOLDER_INSPECT, FOLDER_BIND_RESULT, and FOLDER_REINSPECT_BEGIN/END.
- App activation: LAUNCH_BACKEND=factory, LAUNCH_OK, FACTORY_LAUNCH, WINDOW_ATTACHED, and FILES_FOLDER_WINDOW.
- Navigation and cleanup: FILES_DIR_OPEN, APP_MODEL_INSTANCE_TERMINATED or INSTANCE_TERMINATED, FILES_MOUNT_RELEASE_BEGIN/END. Compare instance IDs, window owner IDs, mount generations, lease counts, and validity fields across each transition; capture screenshots at initial window, navigation, simultaneous instances, and post-close state.

A clean guest shutdown is the first cleanup choice. If the guest hangs, only the QAC reservation owner may stop the managed guest process after verifying its PID belongs to that reservation. Preserve serial, screenshots, package hashes, and QAC outcome before cleanup. Remove only APP_MODEL-owned per-run copies and writable variable-state files after evidence is saved; do not touch another reservation owner's guest or shared input images.

## Remaining blockers and recommended R2 scope

1. Resolve the kernel NativeAOT runtime dependency for RhpInitialDynamicInterfaceDispatch against a supported kernel target/runtime, or remove the unsupported dynamic interface dispatch from the kernel source using a valid static design. Do not add a fake runtime stub, force an unresolved link, or substitute the Phase 23 user-app pack.
2. Require a clean Release AppRuntime kernel publish with no unresolved link symbols or /FORCE-generated-image warning.
3. Convert the authentic kernel PE with pe_to_elf_v2.py and the KMain map. Verify ELF64, little-endian, ET_EXEC, EM_X86_64, executable entry bounds, and hash.
4. Add or use an isolated production ESP destination so the v1 and v2 packages can be staged without overwriting the root ESP or one another. Audit exact file set, version, hashes, and package repeat before QAC admission.
5. Confirm how the guest acceptance run can reach genuine retired-mount disposal. If no production owner path does so, keep that lifecycle assertion open or scope a narrowly reviewed ownership change before claiming full acceptance.
6. Only then run the bounded two-candidate QAC acceptance matrix above.

## Restrictions and completion

- No QEMU launch, QAC admission request, reservation change, or guest-process termination occurred.
- No protected checkout or global SDK was modified.
- No branch switch, merge, rebase, cherry-pick, or push occurred.
- The only tracked change in this task is this documentation report. The source/build HEAD at the end of technical qualification remains 5fcbd8841ae715772a963b35a6d377cf5e63247e; the report is committed separately as a local documentation-only commit.
- Ending source HEAD and the report commit will be recorded in the task completion message.
