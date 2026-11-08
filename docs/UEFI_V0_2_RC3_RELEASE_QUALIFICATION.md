# guideXOS C# UEFI v0.2 RC3 Release Qualification

**Outcome:** RC3_HOSTED_READY_GUEST_PENDING (Outcome B)
**Candidate:** `ISO/guideXOS-CSharp-UEFI-v0.2-RC3.iso`
**Qualification date:** 2026-11-08
**Publication:** Not authorized; nothing was pushed or published.

## 1. Summary

The production framebuffer corrections from AIDA2 are integrated into main, with the accepted BM2, BM3C, and BM4A source retained. A clean build in an isolated filesystem copy produced the loader, NativeAOT kernel, RAMDISK, embedded ESP, and RC3 ISO. The managed artifact registry was freshly rebuilt and audited at 41/41. Hosted framebuffer, desktop, notification-reset, Task Manager, and PCI checks passed.

The exact RC3 ISO has not been admitted to QAC because no QAC or QEMU coordination tool is available in this session. RC3 guest and owner physical acceptance therefore remain pending. The owner’s successful AIDA3 physical run is historical diagnostic evidence, not RC3 acceptance.

## 2. Repository and protected state

The workstation was LEON2_RDT. The protected checkout was D:\dev\guideXOSUEFI on main, initially at a239589403d277adf8d089d714dbfc4e450e0c76 with tracked files clean and origin/main aligned. The nested out/rt repository was already dirty at commit 9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3. RC3 compilation and packaging used the isolated copy E:\CodexArtifacts\guideXOSUEFI_RC3_clean_20261108; the protected nested runtime was not modified.

The integrated production/build changes are commit 5de2723caa96c9de444f94343b1de99d36215f99, parent a239589403d277adf8d089d714dbfc4e450e0c76. This qualification report is committed separately. At qualification close, main was two commits ahead of origin/main, with no tracked or staged changes. Existing diagnostic/release ISOs and preexisting untracked test outputs were preserved. The new RC3 ISO is the only new release candidate artifact in ISO.

The ISO references were rehashed:

| Reference | SHA-256 | Provenance |
| --- | --- | --- |
| RC2 production | `772812198F5DE04BDD71D10027C199868EB3BBFB83CEDF3934DC83520677BC4C` | ISO built from source revision `901112adc315bbc24a297c039d6f9880784652ba` |
| AIDA2 diagnostic | `599A57569D2A0B305BAA84AA06DC9F1172827F39AAF1BDD014AEDD992C9D83B2` | Isolated source copy `D:\dev\guideXOSUEFI_AIDA2_20261101`, based on the RC2 source plus the AIDA2 patch set; Release with `UefiDiagnosticMode=AIDA2` |
| AIDA3 diagnostic | `F62F10E73CAA29EA5EBA4DC9DD7EA325B0AFD9A95B7D49F6B0FA2557D4B376C9` | Isolated AIDA3 source copy derived from AIDA2; Release with BM1 diagnostics selected |

The owner reports that AIDA3 booted on AIDA_LPT with BM01–BM13 visible, a rendered desktop, wallpaper and taskbar, and functioning Display Options. The banded blue screen did not recur in that session. This does not prove the cause of the prior RC2 symptom or establish production/diagnostic execution equivalence.

## 3. Source review and production integration

The 15 reviewed framebuffer/UEFI implementation files in AIDA2 and AIDA3 had matching content. AIDA3 additionally selects BM1 stage overlays, heartbeat and exception diagnostics. Those paths remain gated by diagnostic build symbols and are not selected in the RC3 Release profile. The normal RC3 kernel and loader contain no selected AIDA/BM diagnostic mode or synthetic fault injection. AIDA/BM marker-string searches of the built EFI and kernel returned no matches.

The production integration is the reviewed AIDA2 implementation, not the complete AIDA3 diagnostic checkout:

- Framebuffer contract: `Kernel/Graph/FramebufferSurface.cs`, `Kernel/Graph/Graphics.cs`, and `Kernel/Drivers/Framebuffer.cs`.
- Shared handoff and project inclusion: `Kernel/Misc/EntryPoint.cs`, `guideXOS/Kernel/Misc/UefiBootInfo.cs`, and `Kernel/Kernel.projitems`.
- Loader validation and display: `guideXOSBootLoader/gop_framebuffer.h`, `main.cpp`, `paging.h`, `debug_helpers.h`, `boot_splash.h/.cpp`, `fb_console.h/.cpp`, and `guidexOSBootInfo_validation.cpp`.
- Hosted graphics regression: `Tests/FramebufferContractRegression/`.
- Clean-build and proof staging fixes: `build.ps1`, `guideXOS/guideXOS.csproj`, `Tools/build_phase19_runtime_pack.ps1`, `Tools/Phase22/build_guidexos_native_runtime.ps1`, `Tools/Phase26/build_phase26_managed_proof.ps1`, and the Phase31–35 stage/build/verifier scripts recorded in commit 5de2723.

Production behavior uses the reported GOP byte pitch for physical rows, while logical surfaces remain tightly packed. Checked offset/size arithmetic, clipping, bounds-safe reads and writes, complete clearing, scanline-padding preservation, and RGB/BGR/validated-mask packing are retained. Unsupported or malformed pixel formats and invalid geometry are rejected safely. The C#/C++ BootInfo ABI is 168 bytes; verified offsets include Size 0x06, MemoryMap 0x1c, FramebufferBase 0x34, FramebufferPitch 0x4c, and Reserved 0x90.

### Production versus AIDA3 diagnostic equivalence

| Area | Finding |
| --- | --- |
| GOP pitch, formats, clipping, framebuffer validation | Same reviewed AIDA2 production implementation in AIDA2 and AIDA3; integrated into RC3 |
| Loader-to-kernel BootInfo layout | Shared 168-byte layout and checked offsets; loader and kernel compile successfully |
| BM1 overlays, heartbeat, early diagnostic exception record | AIDA3-only selected instrumentation; RC3 profile does not select it |
| NativeAOT and desktop execution timing | Instrumentation changes code size, logging, and execution footprint; exact physical production equivalence is unknown until RC3 is run |
| Original blue/banded screen cause | Unresolved; no root cause is claimed |

## 4. Preserved bare-metal fixes

The integration starts from main at a239589 and does not alter the accepted NotificationManager boot reset, NativeAOT static-base publication, or PCI/MMIO safety implementation. BM2 retains direct safe notification-state replacement without traversing the prior managed graph. BM3C retains publication of the actual static-base object. BM4A retains decoded and bounded BAR checks, EFI MMIO eligibility, and safe refusal before register access through an unmapped BAR.

The hosted NotificationManager reset regression passed. The clean NativeAOT kernel publish and module initialization build path passed, but there is no standalone hosted execution environment for guideXOS target static-base behavior; target execution remains part of guest qualification. The PCI/MMIO regression passed 29/29 checks. Historical AIDA_LPT behavior for unsupported NICs was `netinit` followed by `no supported nic found`; RC3 guest behavior has not yet been observed.

## 5. Fresh managed proof and RAMDISK scope

The RC3 scratch lane freshly generated the registered managed proof cohort and its stage descriptors. The final managed-artifact audit reports 41/41 identity agreement across the generated artifact records, descriptors, source staging, compiled kernel allowlist, and RAMDISK. Phase27–35 proof outputs were rebuilt in the isolated lane; Phase31–35 staging/build path corrections use the current NativeAOT response-file location. The final RAMDISK contains 595 entries and has SHA-256 `0805FA9A825124630764FC05E3116AD03A0FFCEFCB24B216862BC3880E10764F`.

The RC2 reference RAMDISK has 597 entries. A whole-entry comparison found exactly two RC2-only files: `Native/guideXOS.UserManagedProof.exe` and `Native/guideXOS.UserManagedProof.gxmi`. They are the older fixed-identity Phase23 input for Phase24, outside the 41-item production registry. Phase24 is selected only by `UEFI_DIAGNOSTIC_RING3_PHASE24`; it is not enabled in the RC3 production profile and its accepted selector stops at mapping/teardown without dispatching managed entry.

The supported Phase23 image rebuild was attempted in the clean lane but did not link: `RhRegisterOSModule` is unresolved in the Phase23 package. No cached Phase23 binary or descriptor was copied into RC3. The resulting two-entry RAMDISK difference is explicit and limited to the disabled Phase24 diagnostic payload; all 41 registered RC3 artifacts and the compiled allowlist agree. The Phase23 rebuild limitation is recorded for follow-up if Phase24 diagnostic boot testing is needed.

## 6. Hosted regressions

| Check | Result |
| --- | --- |
| `FramebufferContractRegression` | PASS, 872 assertions covering padded/packed rows, non-power-of-two and 1366-wide geometry, pixel conversion, GOP validation, clipping, bounds, canaries, fill/clear, and logical/physical stride |
| `UefiDesktopInteractionTests` | PASS, 42 assertions |
| `PciMmioResourceRegression` | PASS, 29/29 checks |
| `NotificationBootResetRegression.ps1` | PASS; direct replacement, no old-graph traversal or disposal |
| `TaskManagerMemoryDetailsRegression.ps1` | PASS |
| Managed artifact audit | PASS, 41/41; descriptors and staged/compiled identities agree |

No physical WAV playback or target-side NativeAOT execution is claimed. Start menu, applications, Display Options, disk inspection, console, and input remain exact-RC3 guest/physical acceptance items.

## 7. Clean production build and candidate identity

The isolated production build used the reviewed source overlay corresponding to commit 5de2723, Release configuration, and no diagnostic mode. The clean loader, NativeAOT kernel publish, PE-to-ELF conversion, ESP assembly, fresh proof cohort, and RAMDISK checks completed without build errors. The release ISO builder verified the EFI El Torito entry; 7-Zip archive integrity testing passed. ISO-root EFI, kernel, and RAMDISK bytes match the embedded ESP payloads.

| Artifact | Size | SHA-256 |
| --- | ---: | --- |
| RC3 ISO | 195,741,696 bytes | `530F246B84B7D4F1200CAD22654A48B8A536D983007EEC1C0D52D4F9B48BDE8C` |
| EFI loader, `EFI/BOOT/BOOTX64.EFI` | 50,688 bytes | `3532556FFFF783BE5F3B715FE17F7685C8520C74912260994356764E58D5BB4E` |
| Kernel, `kernel.elf` | 4,739,072 bytes | `71F31F435112FD1F5CA932CBAAE537540EC0EA8C87336C8C2A414353630C5DE1` |
| RAMDISK, `ramdisk.img` | 56,297,702 bytes | `0805FA9A825124630764FC05E3116AD03A0FFCEFCB24B216862BC3880E10764F` |
| Embedded `EFIESP.IMG` | 134,217,728 bytes | `2ED8455AF4B2F170FEE52391373694EA0E6B16F8029DDC1DCF63245701B814A3` |

The ISO target did not exist during preflight; no existing RC2, AIDA2, or AIDA3 image was overwritten. The normal build emitted existing toolchain warnings, including NU1903 for Microsoft.DotNet.ILCompiler 9.0.0 (GHSA-p3jp-9v83-76w9), .NET 7 support warnings, nullable/analyzer warnings, and ISO tool warnings. NU1903 was not suppressed. Review the compiler advisory before any public release.

## 8. QAC, hardware, and acceptance status

No QAC/QEMU reservation tool was exposed in this Codex session. No guest was started outside QAC. Exact RC3 OVMF cold boots, application smoke, soak, reset, and serial/screenshot capture are pending. The AIDA3 physical run is owner-observed and does not satisfy RC3 physical acceptance.

| Hardware/profile | Evidence and limit |
| --- | --- |
| HP AIDA_LPT | Owner reports successful AIDA3 diagnostic boot and desktop; exact production RC3 not tested |
| SARAH_DT | Graphical desktop boot observed historically; modern input remains unqualified |
| ASUS VivoBook | Earlier graphical desktop boot observed; built-in input remains unqualified |
| Network and audio | No functional NIC or physical WAV playback claim; safe unsupported network refusal is the established behavior |

### Owner RC3 physical procedure

1. Verify the RC3 ISO SHA-256 above and extract its embedded `EFIESP.IMG`.
2. Copy EFI loader, kernel, and RAMDISK to a positively identified FAT32 removable USB and compare each hash with the table.
3. Boot AIDA_LPT through its UEFI removable-media entry without accessing or modifying its Windows disk.
4. Check normal splash, full desktop, no diagnostic overlays, built-in touchpad, Start menu, Calculator, shortcut launch, Display Options close/reopen, read-only Disk Manager, Task Manager Memory Details, Console, safe `netinit`, and WAV Player safe-launch behavior.
5. Confirm responsiveness for 10–15 minutes, reboot once, and verify a second desktop entry. Record owner-observed results and preserve photos/logs.

## 9. Remaining gates and disposition

The candidate is hosted-ready, not guest-qualified or physically accepted. The next smallest action is to reserve QAC and boot this exact ISO, capturing serial and screenshots while performing the bounded smoke tests. If QAC remains unavailable, retain the candidate and report GUEST_PENDING. After guest success, the owner must complete the physical procedure above. Review the ILCompiler advisory before publication and rebuild the optional Phase24 diagnostic payload only if that diagnostic profile is required.

No changes were pushed, merged from another development branch, or published. Existing AIDA, BM, RC1, and RC2 reports and artifacts remain preserved.
