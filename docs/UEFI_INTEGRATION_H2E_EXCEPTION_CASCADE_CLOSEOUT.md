# UEFI Integration H2e — Exception Cascade Closeout

Date: 2026-09-29

Repository: D:\dev\guideXOSUEFI

Branch at start: main
Starting HEAD: ececf48c46bb275c0534a5ea7f5fc307684b5560

## Result

**Outcome A — the first writer into the kernel image virtual range was captured and its allocator producer was fixed.** The required stress matrix and build gates passed. The historical uninstrumented #GP → #PF → reset sequence was not independently recaptured, so this report distinguishes it from the guarded first-fault capture.

Phase 33 remains paused, as requested. H2e closes the fault investigation and makes Phase 33 eligible for a separate release decision.

## Preserve H2d

The H2d closeout remains at Docs/UEFI_INTEGRATION_H2D_KERNEL_INT80_CLOSEOUT.md. Its source changes and conclusions were preserved; this work did not reopen the CPL0 int 0x80 investigation.

The authoritative H2d Notepad failure serial log remains out/h2d-cleanup-qemu-debug.serial.log, SHA-256:

    C331AA164C322D7EF386411A739A9C2990586B08324DCF354D8ED845CA550DC3

Its large raw QEMU debug stream was retained outside the repository at C:\Users\guideX\AppData\Local\Temp\h2e-retry4-evidence\h2d-cleanup-qemu-debug.serial.qemu-debug.log, 124,602,630 bytes, SHA-256:

    6BA65A575B1C7079C604320560E667673E4D66EFF5EFE2B9AEAEC9639CBBE919

## First-fault capture and historical-cascade distinction

The original H2d report described progress near Start operation 73, followed by #GP, #PF, then a reset believed to be a triple fault. That exact uninstrumented chain did not recur in H2e. The historical #GP therefore has no captured RIP, error code, instruction, or selector to decode.

The first authoritative H2e writer capture came from retry 5, SHA-256:

    44048739ADDFD29D244C54585441C6E035A476FA445A63C6BC495E8E79C932B3

This run temporarily marked the kernel image page read-only to catch its first writer. Consequently, exception sequence 1 below is a protection fault raised by the diagnostic watch on the offending write; it is not evidence that the same #PF occurred spontaneously in the uninstrumented historical run.

| Field | Captured value |
|---|---|
| Sequence / vector | 1 / vector 14 (#PF) |
| Error code | 0x3 |
| RIP / CS / RFLAGS | 0x1000E49A / 0x8 / 0x10202 |
| CR2 / CR3 | 0x10001000 / 0x3DD73000 |
| Instruction bytes at RIP | 88 10 |
| Decoded instruction | mov byte ptr [rax], dl |
| Operands | RAX=0x10001000, RDX=0; the helper attempted to store zero |
| Runtime code window | 90 90 48 8B C1 4D 63 C0 4C 03 C0 4C 3B C1 76 0A 88 10 48 FF C0 4C 3B C0 77 F6 C3 90 90 90 33 C0 |

The error bits decode as present-page protection violation, write, supervisor mode, no reserved-bit violation, and not an instruction fetch. Protection-key, shadow-stack, and SGX bits were clear.

The page walk recorded by the capture was:

| Level | Entry |
|---|---|
| PML4E | 0x3DD72023 |
| PDPTE | 0x3DD71023 |
| PDE | 0x3D811023 |
| PTE | 0x3D8C7021 |

The leaf was present, supervisor, and read-only. It mapped virtual page 0x10001000 to physical page 0x3D8C7000. The watch made it read-only to stop the write at its source; this does not show that the page had already been corrupted before the watch.

The runtime helper maps to StartupCodeHelpers.MemSet at approximately 0x1000E48C (fault offset +0xE). The caller path is StartMenu.BuildBackgroundBlurCache() in guideXOS/GUI/StartMenu.cs (method near line 191; temporary allocation near line 226; destination allocation near line 229). The exact ELF/map for this diagnostic build was not preserved, so the helper/source mapping is a best available symbol/source correlation, not an exact-build ELF symbolication. The exact runtime bytes and faulting instruction above are from the captured frame.

### Operation timeline

The watched run reached Start operation 33. Immediately before sequence 1 it logged:

1. APP_RUNTIME_START_BLUR_TMP_ALLOCATED;operation=33;...;memory=200892416
2. APP_RUNTIME_START_BLUR_DST_ALLOC_BEGIN;operation=33;...;memory=200925184
3. Sequence 1 attempted the MemSet write at 0x10001000.

The capture establishes the first invalid write and its allocation context. It does not establish that the user's earlier operation-73 event was the same exact instruction or exception sequence.

### CPU frame and stack

For this vector-14 capture, the exception structure contained 15 saved GPRs at offsets 0–119, the common stub's synthetic error slot at 120, the native vector slot at 128, the CPU-pushed page-fault error code at 136, and the CPU return frame beginning at 144. The return frame therefore held RIP, CS, and RFLAGS; this same-CPL, IST-zero entry did not push RSP or SS.

The earlier logger incorrectly read words at offsets where it assumed CPU-pushed RSP/SS existed. Its displayed RSP 0x27F908 and SS 0x10 were not valid interrupted-stack fields. With the corrected same-CPL frame rule, interrupted RSP is return-frame address plus three qwords: 0x27F8F0. It is canonical and 16-byte aligned, approximately 0x710 below the observed 0x280000 bootstrap-stack top estimate. A separately captured bootstrap stack bound/canary was unavailable, so this is not a proven bounds check.

The scheduler snapshot also reported a kernel thread with ID 0x489B000 and stack range 0x489B000–0x489F000. That is not the active stack for this exception: the interrupted RSP points to the bootstrap stack. Do not use the scheduler thread stack range as the exception-stack bound.

### Exception context

At sequence 1 the snapshot recorded CPU 0, interrupt-parent depth 0, parent vector -1, a non-user and non-terminated kernel scheduler thread, no current process, active application handle/generation 0, allocator owner 0, and cleanup not running with no pending second pass (pass number 1). It reported seven windows; the top window had owner 0x25, was disposed, and had application handle/generation 0. The taskbar projection count was zero.

No #GP was captured, so its error code and selector/index interpretation are N/A. The generic x86 #GP selector-code interpretation remains external bit 0, IDT bit 1, TI bit 2, and selector index in bits 3 and above; no observed value exists to apply those fields to.

## Secondary diagnostic exception and reset evidence

Sequence 2 in the same watched diagnostic log was another vector-14 fault during diagnostic re-entry after the first snapshot: error code 0, CR2 0xFFFFFFFF910BD418, RIP 0xA0003, CS 0x8, RFLAGS 0x10046. It was nested under sequence 1. This is a secondary diagnostic-handler fault, not proof that the original Notepad exception handler reached a double fault.

The guest remained running after the watched capture. No vector-8 entry, durable double-fault completion marker, CPU-reset marker, or QEMU reset event was recorded. Thus the H2e evidence does not prove a double or triple fault. The earlier reset report remains historical user-observed behavior without a matching captured host event in the preserved H2d serial log.

## ISR, IDT, GDT, and TSS audit

The vector 13 and 14 stubs use the shared ISR path. The audit confirmed the stub distinguishes CPU-error-code vectors from vectors without a CPU error code; the synthetic error slot and native vector slot are separate from the CPU-pushed error code. GPR push order is symmetric with the C# register layout. The handler's RIP/CS/RFLAGS offsets account for the extra CPU error code on vectors 13 and 14. The normal return path uses iretq; no swapgs path is used. No CR3 switch is performed by this common exception stub.

For vectors 8, 13, and 14, the recorded gates used selector 0x8, present=1, DPL=0, 64-bit interrupt gate type 0xE, and IST=0. The IDTR was base 0x4872010, limit 0xFFF; handler targets were vector 8=0x10001F76, vector 13=0x10001FEE, vector 14=0x10002006.

The GDTR was base 0x10483DF8, limit 0x37. Recorded code/data descriptors were 00AF9A000000FFFF, 00CF93000000FFFF, 00AFFA000000FFFF, and 00CFF2000000FFFF. The TSS descriptor was 10008B483D900067 / high word 0; TR=0x28; TSS base 0x10483D90, present=1, type=B, RSP0=0x4870000. IST1–IST7 were all zero. No descriptor corruption was indicated by the snapshot. Vector 8 therefore uses the common stack; there is no dedicated double-fault IST stack.

The runtime bytes around the watched write were captured, but an exact same-build ELF/map comparison was unavailable. The evidence proves the watched store targeted a read-only kernel-image mapping; it does not support a claim that the bytes had already diverged from disk before the guard trapped it.

## Producer and repair

The allocator returned identity-address values from an arena beginning at 0x04000000 with size 1 GiB. That numeric range overlaps the kernel's linked virtual interval beginning at 0x10001000. A returned allocation could therefore reuse a virtual address occupied by the linked kernel image; MemSet then wrote through that address into mapped kernel text.

The correction passes the bootloader's linked kernel virtual start and span through BootInfo v1 Reserved[0] and [1]. The allocator's new Initialize(start, reservedAddress, reservedSize) overload marks every overlapping 4 KiB page reserved with PageSignature without increasing PageInUse. The original one-argument initializer remains and delegates with an empty exclusion interval. The bootloader computes these fields before recomputing the BootInfo checksum.

The temporary write-watch guard was removed after capture. A source search found no BeginCodePageWriteWatch, CodePageWriteWatch, WRITEWATCH, or PAGE_WRITE_WATCH code remaining. Durable exception sequencing, frame decoding, context snapshots, IDT/GDT reads, and corrected same-CPL RSP/SS reporting remain available for future forensics.

The Notepad runner now treats primary-window closure plus an empty taskbar marker as the completion condition. It does not mistake the Unsaved changes dialog's close marker for primary Notepad completion; the dialog path remains enabled. The serial traces preserve per-operation markers and the captured fault snapshot, but no complete row-by-row ledger of every iteration's generation, child-dialog owner, and window/taskbar counts was retained. The matrix results below establish completion and cleanup counters; they should not be read as that missing detailed ledger.

The secondary vector-14 record names RIP 0xA0003, but no safely readable instruction-byte window or symbol was captured for that diagnostic re-entry fault.

## Validation matrix

| Gate | Result and evidence |
|---|---|
| Start-only | 100/100 passed. out/h2e-start-only-100.serial.log; SHA-256 2DCCCAF66ED1B0DBBB64845DBA4264C319CA4B766752068B2FDF3F7922F89AD3. |
| Calculator foreground switching | 50/50 passed; the same Calculator handle was restored (4294967302). out/h2e-foreground-50.serial.log; SHA-256 20AEBE46A1377DAB165B2F497FB95ACE2E7BD8502220AD75AFE2A2941C70B619. |
| Guarded cleanup matrix | 50 Notepad, 50 Calculator, 25 mixed, 25 Computer Files completed with CLEANUP_STRESS_COMPLETE. No guard hit, no #PF/#GP/#UD/ABI panic, and freeInvalid, freeNoPages, and freeCorrupt stayed zero in all 242 samples. Nested cleanup remained a serialized second pass. out/h2e-cleanup-stress-retry8.serial.log; SHA-256 B6EF3AD8251293FFD1E48B5948F002217CD40B5DDEF3D808C762C29769F8DE4C. |
| Notepad without the guard | 100/100 passed; zero exception records and all three allocator free-error counters remained zero. out/h2e-notepad-100-retry9.serial.log; SHA-256 A6CAAEE696F93EF5551A30A479D4EBF37B5F62560D6352FE487F3C91A77D88E0. |
| Phase 31 | Fresh run passed four primary 31 cases, replacement, cleanup balance, and five returns; target count 6. out/h2e-phase31.serial.log; SHA-256 6FE18189B9A7665DD6B4D7CA2A7DCC0F491566488FA438FD66F73FA39457430F. |
| Phase 32 | Fresh run passed five 32 cases, negative/stale-owner/FailFast/replacement cases, six target terminations, cleanup end, and resource balance. out/h2e-phase32.serial.log; SHA-256 D360DF5CFA4C984631D14010524AFFE22EE75A897C18D927190AD89A8003E44D. |
| App Model | Fresh run completed with 12 descriptor/factory registrations, fallback 0, and compatibility counters calls=3, translations=2, legacy backend=0, failures=1 (expected negative case). out/h2e-app-model.serial.log; SHA-256 AEC8DDEF209662C88D331982A1D0FF007A0AF86DDC06E11B3BAE6C0534A96C9F. |
| Lifecycle and grouping controls | Diagnostic assertions passed lifecycle 15/15 and taskbar grouping 16/16 with stale taskbar owners 0. Both were emitted before the extended host-driven Files-directory route stopped. |
| Extended AppRuntime host route | Incomplete as a host workflow: the first run opened Programs when Scripts was expected and reported INPUT_INJECTION_FAILED; the rerun did not produce the expected directory marker. The guest remained running, and these stops emitted no guest exception. Logs: out/h2e-app-runtime.serial.log SHA-256 FF2A1B1FC5AE553FDEA1B6950D1B7F026486A1FE027366EDC9C9123454664F9B; out/h2e-app-runtime-rerun.serial.log SHA-256 7038A70E6474A0F6936D3DAB1452411718BEB07D04336180B32622FEC610F55C. This additional navigation route remains unverified; its host input failure is not counted as a guest fault or as a failed lifecycle/grouping assertion. |
| Taskbar / desktop | Taskbar soak passed with left mouse down/up 11/11, zero drops/faults, Start opened 3 times, three visible icons/buttons, fallback 0, and existing-app activation. Inspected captures: out/uefi-taskbar-apps.png, out/uefi-taskbar-apps-hover.png, out/uefi-taskbar-apps-pressed.png, and out/uefi-taskbar-apps-selected.png (plus original PPM captures). Start artwork, hover, pressed/selected state, and taskbar icons were visible with no unexpected fallback. Log out/h2e-taskbar-soak.serial.log; SHA-256 396FF8ED643D41541254DF3145A857FD57B66E987261030A7FEA914ECE223367. |
| H2c artifact audit | The post-build CSV has 22 rows across Phases 29–32. Descriptor agreement 22/22, compiled allowlist agreement 22/22, build/staging agreement 22/22, and ramdisk/source agreement 22/22. All rows classify as matches. out/h2c-managed-artifact-audit.csv. |

The historical PF capture path was armed. The guarded fault was the only first-writer capture; no spontaneous PF or GP recurred in the completed post-fix cleanup matrix or the later unguarded 100-Notepad run.

## Full build and staged artifacts

The final .\build.ps1 completed with exit code 0 and “Build Complete!”. Phase26 found and used the Visual Studio C++ host environment. Managed payloads regenerated (MANAGED_ARTIFACTS=22); descriptor agreement and build/staging agreement were 22/22; ramdisk staging and compiled allowlist agreement were 22/22. The kernel was rebuilt after managed generation, then ramdisk and EFI staging completed.

| Artifact | Bytes | SHA-256 | Staged equality |
|---|---:|---|---|
| EFI bootloader ESP/EFI/BOOT/BOOTX64.EFI | 49,664 | F182BE73955ADF689EB08F108BB0E00F496E3BE60550C98463998AAB9C5E2AFE | Staging hash check passed |
| Kernel kernel.elf | 4,648,960 | BA06A812B35E2F20B7BE14C3757F00B2A6008230B1CF45B5C527C8D65E491403 | ESP/kernel.elf hash matches |
| Ramdisk ramdisk.img | 43,679,385 | 21292E9610FF76D2C33C3A89A1A91CBA43B661A08BBB663233C06E866ADF8E28 | ESP/ramdisk.img hash matches |

## Changed source and repository state

The root-repository source changes are:

- Kernel/Misc/Allocator.cs
- Kernel/Misc/EntryPoint.cs
- Kernel/Misc/IDT.cs
- Kernel/Misc/Native.cs
- guideXOS/GUI/TaskbarApplicationEntry.cs
- guideXOS/Kernel/Misc/UefiBootInfo.cs
- guideXOS/native_stubs.asm
- guideXOSBootLoader/guidexOSBootInfo.h
- guideXOSBootLoader/main.cpp
- run_uefi_validation.ps1
- this closeout document

The root repository started at ececf48c46bb275c0534a5ea7f5fc307684b5560, on main tracking origin/main, with no ahead/behind commits. The H2d CPL0 int 0x80 repair remains protected in history. The out/rt nested runtime repository was left untouched by git cleanup or commit; the final full build left its generated NativeAOT working tree modified. The legacy repository was clean. The server repository's pre-existing untracked tmp/startup-appmodel-regression-20260929-060500/ was left untouched.

No push was performed. Phase 33 remains paused.
