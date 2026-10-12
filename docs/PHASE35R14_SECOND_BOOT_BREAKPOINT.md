# Phase 35R14: Second-Boot Widget Breakpoint

**Status: Outcome B.** The vector-3 startup failure is identified and repaired, and the managed same-image reboot proof now passes. Phase 35 remains open because the complete R7 regression/artifact matrix has not been freshly qualified. Phase 36 remains gated.

## Containment and source state

All builds and QEMU runs were confined to the ordinary-copy sandbox at `C:\dev\guideXOSUEFI_Phase35R13_SANDBOX`. No worktree or branch was created. The live checkout remained on `main` at `2cea5188cb98370d13f86f23b5e76bacb09ec48f`; its pre-existing 15 status entries and nested `out\rt` at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with 58 dirty entries were preserved during investigation.

Live protected output hashes at preflight were kernel/ESP kernel `3E8E448A9F7096CC9ED4A9856621155479773836042F54094A8482B0EEA6E43B`, ramdisk/ESP ramdisk `55B2BB16A58D63CD713E67E3D9566DC6F3A915F1BFD2EF0B15256E942FD9289A`, and BOOTX64 EFI `44819B9E31BEEE16520B02D67A7C1807725B467764E1E9E11B438E30CA9C193F`.

## Faulting image and exception

The preserved faulting ELF is `out\phase35q\r13-preserved\kernel.elf`, SHA-256 `CE939A2068DA85083BFEDBBDA9D551E1891C75F8B06D4A4136BBEB92F0B43161`. Boot 1 and boot 2 staged the same ELF. The corresponding ramdisk was `55B2BB16A58D63CD713E67E3D9566DC6F3A915F1BFD2EF0B15256E942FD9289A`; BOOTX64 EFI was `744D92EEA99101B8975E1D52948FE67919DCB28F9715C874D4349F8CF0749B78`. The managed artifact cohort was 41 entries. The faulting run used `Ring3Phase35R13`, QEMU 11.0.0, machine `pc-q35-8.2`, 1280 MiB RAM, OVMF code plus a fresh writable vars copy, a disposable 16 MiB FAT16 AHCI disk with serial `GX35Q0001`, and a QMP `system_reset` between boots. The reset reused the same AHCI image.

The fault runner is `out\phase35q\run_r13_manual_reboot.ps1`. Its QEMU arguments were `-machine pc-q35-8.2`; read-only pflash OVMF code; writable pflash OVMF vars; `-drive if=none,id=esp,format=raw,file=fat:rw:ESP -device ide-hd,drive=esp`; `-drive if=none,id=phase35q,format=raw,cache=writeback,file=<r13-same-image-UUID.img>`; `-device ahci,id=phase35q-ahci`; `-device ide-hd,drive=phase35q,bus=phase35q-ahci.0,serial=GX35Q0001`; `-m 1280M`; file-backed serial; `-no-shutdown`; `-d guest_errors,cpu_reset`; file-backed QEMU diagnostics; `-boot menu=off,splash-time=0`; `-display none`; and a localhost QMP server. The script sent QMP `system_reset` after boot 1. OVMF code source SHA-256 was `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`; vars template SHA-256 was `5D2AC383371B408398ACCEE7EC27C8C09EA5B74A0DE0CEEA6513388B15BE5D1E`. Each process used a fresh writable copy of the vars template, retained through the warm reset. The generator's pristine AHCI image SHA-256 was `2EFE22E39E112105854DBC4784887CE06A64A040EC98D50FB52DD7A7FE94B451`. The fault-specific image file was removed by its runner, so its post-run disk hash is unavailable.

Boot 2 raised vector `0x03` at RIP `0x10007538`. The exception record reports CS `0x08` (CPL 0), CR3 `0x4E423000`, RSP `0x27FCE0`, RBP `0x27FDC0`, and RFLAGS `0x206`. The recorded frame address for saved RIP is `0x27FCC8`; a complete validated frame-chain/backtrace was not captured. The fault was in kernel mode; no Ring 3 process or `ApplicationInstance` was active. The kernel thread identity recorded during the fault investigation was `0x49DC000`.

The last successful startup marker was `[WIDGETS] initialized`. Boot 2 then emitted the exception record; boot 1 continued through App Model initialization and the managed requester. The DSDT and ACPI root addresses were stable across the two boots, as were kernel, ramdisk, EFI, diagnostic mode, and the AHCI image. The first divergence was therefore after widget setup in reset-sensitive managed GUI state, not in storage, ACPI, or staging.

Fault bytes from the exact kernel image, `RIP-16` through `RIP+15`:

`00 90 48 83 C4 20 5B 5E 5F C3 E8 35 D5 00 00 CC 55 41 57 41 56 41 55 41 54 57 56 53 48 83 EC 58`

`RIP-1` is the single-byte `CC` (`INT3`). There is no `CD 03` or `0F 0B` encoding in this window. The saved RIP points at `0x10007538`, the next routine's `push rbp`, as expected for an `INT3` trap. The preceding instruction is a call at `0x10007532` to `0x10014A6C`, followed by the trap at `0x10007537`.

The fault-time map places `guideXOS_Program__SetupEscapeKeyHandler` at `0x100074BC`; the trap is at offset `0x7B` in that method, at the tail of its compiler-generated static delegate-cache guard. The source is `guideXOS/Program.cs`, `Program.SetupEscapeKeyHandler`, specifically the `Keyboard.OnKeyChanged +=` lambda (fault-time source around line 1427). The call chain is `Program.KMain` → `Program.SMain` → `SetupEscapeKeyHandler`. The guard's type-check failure takes the runtime/compiler-generated `INT3` path. This was not an explicit application `Debug.Break`, diagnostic trap, or widget assertion. The failed condition was a stale captureless-lambda cache/EEType value being rejected during the second kernel entry.

## Root cause and repair

QMP `system_reset` restarted firmware and the kernel while guest RAM retained reset-sensitive managed static state. The captureless lambda generated a static cached delegate guard that encountered stale state on boot 2. This is a warm-reset startup defect, not an allocator leak or Persistent backend defect.

The bounded fix makes the Escape handler lambda capture a local `ConsoleKey.Escape`, so NativeAOT emits a closure path instead of the stale static cache guard. The exception handler and breakpoint policy were not changed.

Two more reset-sensitive GUI roots became visible after the first guard was bypassed: the static Animator animation list retained stale entries, then NotificationManager's static notification list reached its old capacity during widget notifications. `Program.KMain` now resets NotificationManager and Animator at the UEFI boot boundary. `NotificationManager.ResetForKernelBoot` disposes existing notifications and installs a new list.

A separate App Model self-test collision then appeared in the full two-boot run. Its test wrote and cleaned `empty.bin` under the same AppId as the Phase 35 persistence fixture, deleting that legitimate file. The test now snapshots the initial Persistent entries and verifies cleanup returns to that exact baseline; its empty-value case uses the separate path `p2-empty.bin`. No Persistent storage backend or reboot-delete behavior changed.

## Repair evidence

The full contained run is `out\phase35q\r14-current-2bf8e777f0dc4c63bbc023c0e45474d3.serial.log`; its disposable AHCI image is `out\phase35q\r14-current-2bf8e777f0dc4c63bbc023c0e45474d3.img`. The image began at pristine SHA-256 `2EFE22E39E112105854DBC4784887CE06A64A040EC98D50FB52DD7A7FE94B451` and ended at `81DE9674AF5A90E16D6EFBBCFC8857CBADBAF89C2CA33F4F640715F2603EAF9B`. It uses one kernel hash on both boots: `800266154630938BE659C271A0AE71F08FC18D0B0AD0A96C4E70361760B10BE0`. Boot 1 reported fixture `Seeded`, `seedWrites=1`, SHA-256 `BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D`. After QMP reset, boot 2 reported `Verified`, `seedWrites=0`, the same fixture SHA, `[WIDGETS] initialized`, `APP_MODEL_COMPLETE`, and 35 `PHASE35_MAIN_RETURN=35` results. Each boot completed the R13 audit; vector-3 count was zero. The combined harness result was `two-audits`. The post-fix launch is captured by `out\phase35q\run_r14_current_kernel_reset_control.ps1` and uses the same Q35/OVMF/AHCI/QMP setup with that runner's temporary ESP and diagnostic paths.

Each boot also reported 25/25 lifetime stress, allocator-stable, zero active storage requests, zero open Persistent handles, zero invalid/corrupt/no-page frees, and a contiguous 8 MiB allocation pass. Each audit had 35 lifetime-pass markers and 35 managed returns. `PHASE35P2_REBOOT_DELETE=FAIL` on boot 1 is the expected pre-verification state; boot 2 emitted `PHASE35P2_REBOOT_DELETE=PASS`. The R13 NotFound-only lifetime passed on each boot. The separate exact six-string dispose/free count was accepted by R13; R14's combined serial log contains high-volume string census output and does not isolate those six events as a distinct counter.

A fresh single-boot 1024 MiB UEFI control reached `[ACPI] ACPI Initialized`, `[WIDGETS] initialized`, and `APP_MODEL_COMPLETE` with no vector 3. The default-memory and 1280 MiB DSDT runs both used the bounded R13 diagnostic kernel.

## DSDT mapping checks

At 1024 MiB: RSDP `0x3F77E014`; XSDT `0x3F77D0E8` (84 bytes); FADT `0x3F779000` (244 bytes); DSDT `0x3F77A000` (8461 bytes). At 1280 MiB: RSDP `0x4F77E014`; XSDT `0x4F77D0E8` (84 bytes); FADT `0x4F779000` (244 bytes); DSDT `0x4F77A000` (8461 bytes). The FADT is checked for a valid legacy DSDT pointer and, when that is zero, the extended X_DSDT pointer; the referenced table is validated as DSDT. The DSDT is mapped across three 4 KiB pages, rounded from its reported length. The RSDP region is four pages, XSDT one page, and FADT one page. ACPI initialized and widget startup completed at both memory sizes. No ACPI-wide catch-all mapping was added; the bootloader's separate pre-existing allocator and MMIO mappings are outside this DSDT check.

## Remaining qualification

Fresh same-image reboot, P2 reboot-delete, 25-lifetime allocator stress, lifecycle 15/15, taskbar grouping 16/16, Compatibility self-test, the Phase 9–11 service self-test markers, and zero unintended Legacy calls all pass in the combined two-boot log. The complete Phase 8–11 managed regression matrix, all managed boundary/isolation cases, fresh artifact reproducibility and mutation rejection remain open. The completed ordinary build and default-mode desktop smoke both pass. Do not close Phase 35 or start Phase 36 until the remaining gates pass.


The Phase 26–34 runtime controls have since been rerun individually in fresh QEMU guests at 1280 MiB. Each emitted its exact `RING3_PHASEnn_COMPLETE=1` marker, with no vector 3 or App Model failure:

| Phase | Kernel SHA-256 | Serial log |
|---|---|---|
| 26 | `4BBA41E92AEF7E4CD28929D3B089B499C11990DB8224D91BB63F401940A074EA` | `out\phase35q\r14-current-9429e19943844b08b76d4050581ea340.serial.log` |
| 27 | `E648AFF074AD22F8312FE095DBD2751977EF5C37836C4AD63757B1C338C8ACFE` | `out\phase35q\r14-current-6379c3c1b53a4910b8a71120a9d83b21.serial.log` |
| 28 | `00E79C6AD4785D9EDE5A955D8FE6A55E4A597CA9ED5C3B8203202AD16D8FF11B` | `out\phase35q\r14-current-10efd6799a3f4769b34deaf48230e9ab.serial.log` |
| 29 | `4D6CD5D1F4FF92856B0D5C8352D3897CA9908AC30FB7E3B2C4250739E7C676D1` | `out\phase35q\r14-current-81d7f717833c4c56b9ab50e8ac9fbd86.serial.log` |
| 30 | `B6C0807151DFA6BC9C07D07ADCF5BA9BE4DAEE6EE64777FB9691EE27D5F1E021` | `out\phase35q\r14-current-03f0dfa40e124524ac23e041bd21eea1.serial.log` |
| 31 | `CA3E4C960B0012C96F5121B2AC6A5E1EE74E77F5A9BE090FDFA4A671767F3730` | `out\phase35q\r14-current-472a7da72ca8402bb7a3985fba670fad.serial.log` |
| 32 | `ED1839630545B2EE94AE017263603C8020779EF25EC4E4CD1AD0504C8BC91335` | `out\phase35q\r14-current-3f05f59e426948548509012b87f7612e.serial.log` |
| 33 | `61A32FB8FFEFF1A3A395DD1E3C2F08358B336EBD093E780EB8C1384060F0D90B` | `out\phase35q\r14-current-6f14e44e0d3b4516904c07485f09af8b.serial.log` |
| 34 | `2FAD9BD32AF672AF59BA9DACD2F32F5A88F638B22428715C2CDF7C007B3DCD30` | `out\phase35q\r14-current-d87f81e503d44a76891fa4754b33e448.serial.log` |

After the isolated diagnostic-mode matrix, the sandbox's ordinary kernel, ramdisk, EFI, and symbol map were restored from the completed ordinary-build artifact set and checked against their recorded SHA-256 values. No product source changed after that completed build. The final ordinary kernel matches the healthy desktop smoke log below.

## Changed source

- `guideXOS/Program.cs`
- `guideXOS/GUI/NotificationManager.cs`
- `guideXOS/OS/ApplicationServiceRegistry.cs`
- `Docs/PHASE35R14_SECOND_BOOT_BREAKPOINT.md`

## Final ordinary build and normal-mode smoke

The requested ordinary `.\build.ps1` completed in the sandbox with `Build Complete`, zero reported build errors, and 194 compiler/linker warnings across the repeated managed payload builds. The final default-mode artifact identity is kernel/ESP kernel `432B345AFBE127936C98D377D5CBF6B4EE15A1A4557C9D53F88B318302C5074D`, ramdisk/ESP ramdisk `55B2BB16A58D63CD713E67E3D9566DC6F3A915F1BFD2EF0B15256E942FD9289A`, and BOOTX64 EFI `7F188287FCA1AC2874A3F40816A9FC3089468D692179EBCF7F621BC62AC43EB2`. The 41-row audit reports descriptor, build staging, ramdisk staging, and compiled allowlist agreement at 41/41.

The final ordinary EFI is not bitwise identical to the earlier diagnostic build EFI (`744D92EEA99101B8975E1D52948FE67919DCB28F9715C874D4349F8CF0749B78`). PE comparison found identical `.text` hashes; differences are limited to PE timestamp/checksum, CodeView PDB GUID, and related debug timestamps. This is consistent with build metadata changing. Only one completed ordinary build is qualified; the later redundant rebuild after the diagnostic matrix was stopped before completion. The mutation-rejection test also remains unrun, so full reproducibility remains unqualified.

A default-mode boot of the final kernel used a fresh disposable 16 MiB FAT16 AHCI image, QEMU 11.0.0 / q35 8.2, 1280 MiB, and fresh OVMF vars. The normal desktop reached `CONTINUOUS_HEARTBEAT_FRAME=300`; graphics were valid, allocator invalid/corrupt/no-page free counters were zero, widgets initialized once, and there was no vector 3 or App Model failure. The harness status was `normal-desktop-heartbeat-300` and QMP quit the guest cleanly. Normal mode does not emit the diagnostic `APP_MODEL_COMPLETE` marker; the smoke therefore ends at the healthy desktop heartbeat.

A separate cold QEMU process used the previously qualified image as input. The new process reached `[WIDGETS] initialized`, reported the existing fixture `Verified`, `seedWrites=0`, exact SHA-256 `BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D`, and `PHASE35P2_REBOOT_DELETE=PASS`. The diagnostic control continued into the full allocator proof instead of stopping at the requested App Model boundary; I stopped it after these markers, with no vector 3 or App Model failure observed. This is a fixture/startup control only; its later full audit is not counted as another completed regression run.

## Final decision and remaining R7 gates

**Outcome B.** The second-boot breakpoint is fixed and the managed same-image proof passes. Phase 35 remains incomplete. Phase 36 remains gated.

Still requiring fresh qualification: the complete Phase 8–11 runtime control matrix; remaining managed invalid-path/raw-request/buffer/isolation/FailFast/replacement/copy-isolation cases beyond the passing P2 boundary markers; exact six-string NotFound census in a dedicated fresh R14 counter; fresh artifact mutation rejection and a second ordinary build for reproducibility. The passing lifecycle 15/15, taskbar grouping 16/16, Compatibility self-test, zero Legacy-call markers, and Phase 26–34 controls do not close those separate matrices.

## Preserved fault and image identity details

The retained fault evidence is `out\phase35q\r13-same-image-7f45d1476c6d461496608a642b810066.serial.log` and `out\phase35q\kernel-r13-fault-disassembly.txt`. The faulting QEMU image was generated as a disposable 16 MiB FAT16 disk; its pristine SHA-256 was `2EFE22E39E112105854DBC4784887CE06A64A040EC98D50FB52DD7A7FE94B451`, volume serial `GX35Q0001`. The fault-time symbol map was `guideXOS\Kernel.map`; later builds regenerated that map, while the instruction bytes and symbolized fault result were retained in the files above.

The captured disassembly around the trap is:

| Address | Instruction |
|---|---|
| `0x10007529` | `nop` |
| `0x1000752A` | `add rsp, 0x20` |
| `0x1000752E` | `pop rbx` |
| `0x1000752F` | `pop rsi` |
| `0x10007530` | `pop rdi` |
| `0x10007531` | `ret` |
| `0x10007532` | `call 0x10014A6C` |
| `0x10007537` | `int3` |
| `0x10007538` | `push rbp` |

The QEMU firmware sources were `C:\Program Files\qemu\share\edk2-x86_64-code.fd` (SHA-256 `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`) and `edk2-i386-vars.fd` (template SHA-256 `5D2AC383371B408398ACCEE7EC27C8C09EA5B74A0DE0CEEA6513388B15BE5D1E`). Each run used a disposable writable copy of the vars template.

The successful two-boot R14 run's same AHCI image was pristine SHA-256 `2EFE22E39E112105854DBC4784887CE06A64A040EC98D50FB52DD7A7FE94B451` before boot 1 and `81DE9674AF5A90E16D6EFBBCFC8857CBADBAF89C2CA33F4F640715F2603EAF9B` after boot 2. Its kernel, ramdisk and EFI hashes were stable across both boots. The separate cold fixture control began from that final image hash and copied it into its own disposable AHCI file.
