# guideXOS C# UEFI v0.2 RC2 Release Qualification

**Qualification date:** 2026-10-28 (QAC session timestamp: 2026-10-29 UTC)
**Outcome:** RC2_HOSTED_READY_GUEST_PENDING
**Publication:** Not authorized; nothing was pushed or published.

## Verdict

The production-configured RC2 payload and ISO were built from an isolated copy, the hosted regression gates passed, and the exact ISO passed archive and payload-identity checks. The ISO has not completed OVMF guest acceptance. Two managed launches stopped during legacy BIOS firmware loading. A third QAC-managed launch used the repository's pflash method, but QEMU became unresponsive before QMP produced its greeting. QAC marked the reservation degraded because its process census could not identify another QEMU process. That process was left untouched.

No guest acceptance step is counted as passed. This is Outcome B, not a guest regression determination: the guest could not be observed well enough to determine whether it booted. Under the release gate, RC2 is not yet ready for owner physical acceptance. No physical acceptance is claimed.

## Repository state and provenance

- Release integration began on branch main at 829f8d5, the recorded pre-integration base. The current repository state was treated as authoritative; accepted local BM2/BM3C/BM4A work was consolidated in the first commit below.
- The product-source and packaging-tool head before this report is 7e57a5e. This report is a documentation-only follow-up visible in repository history.
- The release integration and tooling commits are local on main. The branch was four commits ahead of origin/main before this report and will be five commits ahead after it. No reset, clean, stash, rebase, merge, branch switch, worktree, cherry-pick, push, or mass staging was used.
- The pre-existing BM2, BM3B, BM3C, BM4A, and RC1 ISO files remain in ISO and were not overwritten. The pre-existing test bin/obj directories remain untracked. The existing nested NativeAOT runtime work was preserved and not staged or modified.
- Tracked source and index state were clean before this report was added; after its commit, no tracked changes remain. The RC2 ISO is an intentional untracked release artifact; prior untracked ISO and test outputs remain preserved.

Local commits:

| Commit | Purpose |
| --- | --- |
| 901112a | Integrate accepted BM2 boot-boundary, BM3C static-base, and BM4A PCI/MMIO safety changes, with focused regressions and provenance notes. |
| 5221d74 | Correct Phase 31 native link-response lookup in qualification staging. |
| 442abea | Correct managed proof link-response paths in Phases 32–35. |
| 7e57a5e | Add the production UEFI RC2 ISO packager. |

The isolated candidate was copied from the product-source tree at 901112a. It also contained the later Phase 31–35 staging-script path fixes, which are build/qualification tooling changes and do not change kernel product code. The RC2 packaging helper is committed as 7e57a5e. The resulting ISO and payload hashes below identify the exact candidate artifact.

## Integrated fixes and production configuration

### BM2 boot-boundary reset

NotificationManager.ResetForKernelBoot uses direct boot-boundary replacement of notification state. The correction avoids dereferencing an invalid pre-existing managed reference and avoids broad disposal of the old object graph. NotificationBootResetRegression.ps1 passed.

### BM3C NativeAOT static base

The NativeAOT initializer publishes the static-base object expected by generated accessors, not a handle cell. The frozen 11-entry resolution array is retained. The project-local runtime correction is in the production source tree; unrelated NativeAOT runtime internals were not changed.

### BM4A PCI/MMIO safety

Intel825xx validates PCI BAR decoding, resource size and alignment, EFI MMIO coverage, and bounded page mapping before register access. Unsupported mappings are rejected safely, without an identity-mapping assumption or unsafe register write. The 29-check PCI/MMIO regression passed. This verifies safe rejection, not working networking on QEMU or other hardware.

### Diagnostic behavior and release metadata

The ordinary production build does not select the BM diagnostic modes. BM3C/BM4A tracing and diagnostic overlay paths are gated out of the production configuration; source/configuration audit found no selected synthetic fault injection. Diagnostic source and reports remain available for future BM work. Because the guest did not render, absence of overlays was not visually confirmed in QEMU.

The release splash/build identity is v0.2. The ISO filename and volume labels identify RC2. No unrelated artwork or UI redesign was made.

## Desktop launch and WAV Player audit

The source-defined desktop interaction is single click to select and double click to launch. Representative desktop targets resolve through the same application registry and launch handlers used by the Start menu. The hosted desktop interaction suite passed 42 assertions. Exact-ISO guest interaction remains unverified because QEMU acceptance did not reach a controllable guest.

WAV Player remains registered as gxos.builtin.wavplayer and is not silently removed or disabled. Source review found that initial construction creates the app/window and does not immediately start playback or access audio hardware. An independent launch-time kernel crash was not reproduced in the available hosted checks. Playback and unsupported-device behavior are not qualified; the owner should test opening the idle window and report any crash separately.

## Production build and managed artifact identity

- Build lane: ordinary-copy candidate at E:\guideXOS-RC2-qualification-20261028\candidate. The repository working tree and nested runtime were not used as the mutable build lane.
- The production root build ran with SkipRamdisk and SkipBootloader after the managed identity inputs and fresh RAMDISK were prepared. It generated the NativeAOT kernel and converted it to ELF64. Exit code was 0 with zero build errors.
- The build emitted existing/toolchain warnings: .NET 7 end-of-support, existing unused-field and duplicate-using warnings, CA2014 stackalloc warning, linker base-address warning, NASM zeroing warning, and mkisofs Rock Ridge standards warning. No warning was promoted to an error.
- NativeAOT kernel ELF: 4,726,784 bytes. EFI loader: 49,664 bytes.
- Fresh RAMDISK: 597 files, 57,132,084 bytes.
- Managed artifact set identity: 41 expected built-in artifacts. Descriptor agreement was 41/41; build staging agreement 41/41; RAMDISK staging agreement 41/41; compiled allowlist agreement 41/41. No release registry entry was silently dropped. Phase 35's nine payload-validation variants passed 9/9.
- The final RAMDISK and all three executable payloads were verified against their ISO-root and embedded-ESP copies. The hashes below are the content identity for this artifact set.

Hosted regression results:

| Regression | Result |
| --- | --- |
| NotificationBootResetRegression.ps1 | PASS |
| TaskManagerMemoryDetailsRegression.ps1 | PASS |
| UefiDesktopInteractionTests | PASS, 42 assertions |
| PciMmioResourceRegression | PASS, 29/29 checks |
| Phase 35 managed payload validation | PASS, 9/9 variants |

These are hosted tests. They do not substitute for UEFI guest or physical acceptance.

## ISO artifact and payload verification

**ISO:** D:\dev\guideXOSUEFI\ISO\guideXOS-CSharp-UEFI-v0.2-RC2.iso
**Size:** 196,562,944 bytes
**SHA-256:** 772812198F5DE04BDD71D10027C199868EB3BBFB83CEDF3934DC83520677BC4C

| Payload | Bytes | SHA-256 |
| --- | ---: | --- |
| Embedded EFIESP.IMG | 134,217,728 | 7D9758482DED250A5E45EA859A6A7EDB29CC6123C5FF3451D9D1B919AFF32E81 |
| EFI loader, BOOTX64.EFI | 49,664 | D390FD3ECD62BD04CA16D35D62BEF56AB1D440196F101D0B4BBE53048C519182 |
| Kernel ELF | 4,726,784 | 56557F9F49F691E4C4FC7B5DB9B65FC28E9A0195F563CCB52E50622C8CD4DB20 |
| RAMDISK | 57,132,084 | F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8 |

The extracted ISO contains BOOTX64.EFI, EFI\BOOT\BOOTX64.EFI, EFIESP.IMG, kernel.elf, and ramdisk.img. The root EFI loader copies match. Reading the embedded FAT ESP confirmed label GUIDEXOSRC2; loader, kernel, and RAMDISK hashes match the ISO-root copies exactly. The El Torito catalog contains a checksum-valid, bootable EFI platform entry with no-emulation media. 7z integrity verification returned “Everything is Ok.”

These checks establish package structure and payload identity. They do not establish a successful OVMF boot; that remains pending.

## QEMU/QAC result

QAC was online and idle before the attempts. All attempts were launched through the QAC client; no unmanaged QEMU was started.

1. The first QAC-managed launch exited with QEMU's “could not load PC BIOS” error while using the BIOS firmware option.
2. The retry with native Windows path separators exited with the same firmware-loading error.
3. The pflash retry used OVMF.fd (SHA-256 2DF617A6FD1BAE41EC7C0E3BCECA96C928A5A88D8DC198468D34733393AB2792) and the exact RC2 ISO. QAC granted reservation 71881549bff7423981e6216ac2dcb713 and launched QEMU PID 26864 at 2026-10-29T01:32:57.664Z. QAC then marked the reservation degraded because process census was incomplete; it reported PID 23712 with executable identity unavailable. QEMU accepted the QMP TCP connection but sent no QMP greeting within the 20-second client timeout. Windows reported the admitted QEMU process as unresponsive. The session was ended through its QAC terminal; QAC released the reservation, and no managed session remained. PID 23712 was left untouched.

No splash, desktop, application, cursor, keyboard, netinit, soak, warm reset, or cold-boot acceptance result was obtained. Do not count any of these as passed. When QAC can provide a healthy, non-degraded session, retry the exact ISO and capture a controllable screen/serial/QMP trace before running the acceptance sequence.

## Compatibility, limitations, and deferred work

- HP AIDA_LPT: the owner previously reported successful UEFI desktop, touchpad, applications, Display Options, Disk Manager, Task Manager, and safe netinit unsupported-NIC behavior on BM4A diagnostic builds. Those are prior physical results, not RC2 acceptance. RC2 physical acceptance remains pending.
- Network: no functional AIDA_LPT NIC support is claimed. The known safe unsupported result is acceptable only if it remains non-crashing on RC2.
- SARAH_DT: modern input was previously reported nonfunctional; xHCI/modern-input expansion is deferred.
- ASUS VivoBook: earlier BM2 input was reported nonfunctional.
- WAV Player: idle launch is source-reviewed, but guest launch and audio playback are unverified. Full audio support is deferred.
- Tombstoned-window list behavior remains a deferred defect unless owner testing shows release-critical impact.
- Broader NIC support, App Model convergence, more applications, and UI work remain outside RC2 scope.

## Owner USB preparation and physical acceptance

Use the established FAT32 removable-media procedure. Before preparing media, verify the ISO SHA-256 above. Extract EFIESP.IMG from the ISO and mount it read-only with the established image-mounting method. Format only a positively identified removable USB drive as FAT32, then copy the mounted ESP contents to the USB root, including EFI\BOOT\BOOTX64.EFI, kernel.elf, and ramdisk.img. Verify the copied payloads against the hashes above, safely eject, and boot the USB in UEFI mode. Do not select or alter the laptop's internal Windows disk.

On HP AIDA_LPT, record actual results for:

1. Boot RC2 twice; confirm v0.2 splash, no BM debug tiles, graphical desktop, no startup exception, and responsive built-in touchpad.
2. Open Start. Launch Calculator from a desktop shortcut and Start menu; perform a basic calculation. Confirm single click selects and double click launches desktop shortcuts.
3. Open Display Options, inspect the resolution list, close it, and reopen it.
4. Open Disk Manager for read-only inspection only.
5. Open Task Manager and visit Memory Details.
6. Open Console and run netinit. Accept a clean unsupported-NIC result; reject a crash, hang, or unsafe register access.
7. Open WAV Player and observe whether the idle window launches safely. Do not require sound if no supported audio device exists; reject a kernel crash.
8. Close and relaunch several applications, verify continued desktop responsiveness, and perform a 10–15 minute interactive stability check.
9. Reboot once and confirm a second clean boot.

Record device/model, firmware mode, exact ISO hash, dates, actual application outcomes, any exception text, and stability duration. Do not perform destructive disk operations.

## Release gate and publication

The hosted build, managed-artifact identity, focused regressions, and ISO structure checks pass. The QEMU/OVMF guest gate does not. RC2 is therefore hosted-ready with guest qualification pending, and is not yet declared ready for owner acceptance under the release gate. Physical testing must not be described as completed until the owner tests this exact hash and reports results. No final v0.2 release is accepted, the ISO has not been published, the public website was not modified, and no commit was pushed.
