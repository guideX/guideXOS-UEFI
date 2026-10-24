# BM3B — Display Options Pointer Capture

Date: 2026-10-24

Repository: `D:\dev\guideXOSUEFI`

Target: AIDA_LPT (HP)

Status: Outcome B — diagnostic image qualified in QEMU; physical retest pending.

## 1. Mission result

BM3B adds a fixed-size exception-record extension that preserves the interrupted RDI, reads CR4, calculates `RDI + 8` with an explicit unsigned-overflow flag, and records canonicality for both addresses using the active LA57 state. It does not alter `DisplayOptions` or `DisplayManager` production behavior. The accepted BM2 `NotificationManager.ResetForKernelBoot()` correction is included.

A QEMU-only fault exercised the actual load instruction at `0x1006AC35` with a controlled noncanonical RDI. The displayed and serial records agree. The normal QEMU run also booted the ISO, reached the desktop, opened Start → All Programs → Display Options, and continued its heartbeat through frame 3600 without a fault. This does not identify the physical AIDA_LPT value or establish a production repair.

## 2. Repository and artifact preservation

Read-only preflight found:

- Root HEAD `c838481ef0d1347b1b979741204ea3cff2a374b3`, branch `main`, upstream `origin/main`, divergence `0/0`.
- The root worktree had the pre-existing untracked BM2 and RC1 ISOs and the BM2/BM3A reports. They were preserved.
- Nested `out\rt` HEAD `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` had 58 dirty entries (54 modified, 4 untracked). The live nested runtime was not used for the build and its dirty state remains unchanged.
- `C:\BM2\kernel.elf` has the accepted BM2 kernel SHA-256 `6EF35B6825A3C7C57FA745B2FD276AC216942D78DCAAEB85B70827333F7D1A90`. `C:\BM2` is a non-Git sandbox. Its accepted `NotificationManager.cs` correction matches the source included here (SHA-256 `CCD345E1BC816692DE03B59D65ED54E44F63344FD3BC0E74FC18501C2C45F5CA`).
- The pre-existing root `kernel.elf` and `ESP\kernel.elf` remain at SHA-256 `144E6959A0C1A592CAE7B4F33188093385E4DBC9B7928CF352051B4EEEA42491`; root RAMDISK files remain `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8`; root `ESP\EFI\BOOT\BOOTX64.EFI` remains `54169F5CC887D66511E06E74A596F4CC0325C04D34FD095A3E67D7ECB9CAE971`.
- Protected BM2 and RC1 ISO hashes remain `F5EBB4B90840DAEFF803BAB9E5C9F8969AB3DCFA7C6942EF86781346D6E99A62` and `2A330E0F883C13EA9BE0E9171D20297C3DEA966334E569F5E5E500E915A983E8`. The existing `ISO\guideXOS.iso` also remains unchanged (SHA-256 `1FB747B254B1FC9329EF0BF76D72D8E3CE8290289ACA52BCE5FBD1391441FFFA`).

The full build ran from an ordinary contained copy at `E:\BM3B`; it did not overwrite the protected root build outputs. Existing BM1, BM2, and BM3A reports were left intact. No branch, Git worktree, reset, clean, stash, rebase, merge, cherry-pick, or push was used.

## 3. Exception-entry frame and original RDI capture

The normal desktop IDT uses the shared x86-64 entry in `guideXOS/native_stubs.asm`:

1. Each vector stub reserves an 8-byte native vector slot and writes the vector number there. These instructions do not modify RDI.
2. `isr_common` pushes a zero placeholder, then saves GPRs in reverse push order: `r15` through `r8`, `rdi`, `rsi`, `rbp`, `rbx`, `rdx`, `rcx`, `rax`.
3. The resulting memory order from the pointer passed to `intr_handler` is `rax, rcx, rdx, rbx, rbp, rsi, rdi, r8` through `r15`. `RDI` is at saved-frame offset `48`.
4. The managed handler takes RDI from `stack->rs.rdi`. It does not use the handler's live RDI. The save occurs before the managed handler, serial output, or framebuffer renderer can change the live register.
5. The stub reserves call space below the saved frame and retains the frame base before calling managed code.

For an error-code exception, including `#GP`, the handler reads the CPU error code at saved-frame offset `136` and the CPU return frame at offset `144`: RIP, CS, RFLAGS, then any hardware-pushed RSP/SS. For a no-error exception the return frame begins at offset `136`. The normal CPL0 `#GP` has no privilege transition. Every IDT entry is built with `Reserved0`/IST equal to zero, so it does not switch to an IST stack. The error-code and RIP offsets used by `intr_handler` agree with this layout.

The earlier BM1 diagnostic early-IDT path is separate from the normal desktop path. It also saves GPRs before calling its handler and uses IST zero.

## 4. Record extension and address calculation

The old `UefiBareMetalRecord` fields remain at their established offsets. BM3B appends a version-1 extension:

| Field | Offset | Size |
|---|---:|---:|
| `FaultRdi` | 144 | 8 |
| `FaultCr4` | 152 | 8 |
| `FaultEffectiveAddress` | 160 | 8 |
| `FaultCaptureFlags` | 168 | 4 |
| `FaultRecordVersion` | 172 | 4 |

The record grows from 144 to 176 bytes. The matching bootloader handoff grows from 16568 to 16600 bytes; C# and C++ size assertions and the diagnostic boot-info check were updated together. The extension version is `1`.

`Native.ReadCR4()` is a small native stub (`mov rax, cr4; ret`). `intr_handler` reads it immediately before recording the fault. LA57 is CR4 bit 12. The capture flags are: bit 0 LA57, bit 1 RDI canonical, bit 2 effective address canonical, bit 3 `RDI + 8` unsigned overflow.

The handler checks overflow before computing the architectural 64-bit effective address, uses an unchecked addition only after saving that overflow result, and never dereferences RDI or the effective address. Canonicality is checked as follows:

- LA57 clear: width 48; bits 63:48 must sign-extend bit 47.
- LA57 set: width 57; bits 63:57 must sign-extend bit 56.

The fault path uses fixed-size fields, scalar arithmetic, direct serial hex output, and direct framebuffer drawing. It does not allocate, format managed strings, or consult `AvailableResolutions`.

## 5. Fault display

The existing BM01–BM13 strip remains. The screen keeps the earlier exception fields and adds the RDI/CR4 fields. The labels are:

- `V`: vector; `E`: error code; `R`: RIP.
- `D`: RDI; `A`: effective address; `4`: CR4; `L`: LA57.
- `I`: RDI canonical; `N`: effective address canonical; `O`: unsigned overflow.
- `C`: CR2; `P`: CR3; `S`: interrupted RSP; `F`: RFLAGS.

The QEMU framebuffer capture at 1280×800 showed the full record in one screen; the last row ends at y=336, so the section fits a 768-pixel-high panel while leaving the milestone strip visible. CR3, RSP, and the other prior exception fields were retained.

## 6. QEMU register-capture validation

The isolated test booted the BM3B ISO with OVMF, `-cpu qemu64`, and one vCPU. GDB connected to that QEMU instance only. After stopping the guest, the test assigned:

```text
RIP = 0x1006AC35
RDI = 0x0200000000000000
```

It then resumed the guest. The CPU attempted the actual bytes `8B 5F 08` at that address and raised `#GP(0)` before dereferencing the noncanonical address. A GDB breakpoint at the native `Hlt` stub (`0x100018AC`) stopped the guest after the handler had recorded and rendered the fault. The synthetic assignment existed only in the GDB-controlled QEMU register state; the BM3B image contains no automatically triggered synthetic fault.

GDB showed CR4 `0x668` and CR3 `0x7E4AD000`. CR4 bit 12 was clear, so the active test mode was four-level paging. The record showed:

```text
V 000000000000000D
E 0000000000000000
R 000000001006AC35
D 0200000000000000
A 0200000000000008
4 0000000000000668
L 0
I 0
N 0
O 0
C 0000000000000000
P 000000007E4AD000
S 000000000027FAC0
F 0000000000010206
```

Both `RDI` and `RDI + 8` are noncanonical in 48-bit mode. The full serial evidence was:

```text
BM1F:0000000D:E:R:0000000000000000:000000001006AC35:0000000000000000:000000007E4AD000:000000000027FAC0:0000000000010206:D=0200000000000000:A=0200000000000008:4=0000000000000668:C=00000000:X=00000001
```

The framebuffer values and serial record agree with the GDB-injected register state. This validates the exception-entry RDI capture, CR4/LA57 read, effective-address calculation, canonical flags, and error-code frame for this QEMU run. It is a controlled instrumentation check, not evidence of the AIDA_LPT object's actual value. The contained lane retains the screenshot at `E:\BM3B\out\qemu-synthetic-fault.png` and the serial log at `E:\BM3B\out\qemu-synthetic-serial.log`.

## 7. Normal QEMU startup and interaction

The actual BM3B ISO was booted through OVMF. The run reached the bootloader, NativeAOT runtime, widget initialization, and desktop. At 1280×800, the Start menu opened, All Programs listed Display Options, and the Display Options window opened. Its normal first tab rendered without a fault.

After the interaction, the serial log continued through `CONTINUOUS_HEARTBEAT_FRAME=3600` with `GRAPHICS_VALID=1`, `ALLOCATOR_FREE_INVALID=0`, and `ALLOCATOR_FREE_CORRUPT=0`. The normal run contained zero `BM1F:` records and zero `EXCEPTION` records. The QEMU status remained running. The output showed no unexpected fault. The contained lane retains the Display Options screenshot at `E:\BM3B\out\normal3-display-options.png` and serial log at `E:\BM3B\out\qemu-normal3-serial.log`.

## 8. Build and artifact qualification

The contained build command was `build.ps1 -UefiDiagnosticMode BM1`. It completed with the `Build Complete!` marker and no build-error lines. The final managed-artifact audit reported:

- Managed artifacts: 41.
- Descriptor agreement: 41/41.
- Build/staging agreement: 41/41.
- RAMDISK staging agreement: 41/41.
- Compiled allowlist agreement: 41/41.

The staged payloads match their build outputs:

| Artifact | Size | SHA-256 |
|---|---:|---|
| `EFI\BOOT\BOOTX64.EFI` | 50,176 bytes | `502BC2F352B772E5CE0FEFECBAE1904EADD2001206822B93D8CA652AF53EA290` |
| `KERNEL.ELF` | 4,747,264 bytes | `67CD04E84F69C8C3FE32A412D922493E1426C1AB08A3FBCCEF69B79CBC224AFB` |
| `RAMDISK.IMG` | 57,132,084 bytes | `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8` |

The exact kernel used for symbolization is SHA-256 `67CD04E84F69C8C3FE32A412D922493E1426C1AB08A3FBCCEF69B79CBC224AFB`. It is an x86-64 ELF with no symbol table. `guideXOS\Kernel.map` (SHA-256 `180BC1100B73679C7991097BFEA18C76EED8918D7C379E7C783203B4CE978430`) maps `InitializeDisplayOptions` to `0x1006ABC8`; linked disassembly of the same ELF confirms `8B 5F 08` at `0x1006AC35`, offset `+0x6D`. The linked bytes also match the corresponding NativeAOT object code.

The builder produced a 196,583,424-byte ISO with volume label `GUIDEXOS_BM3B`. 7-Zip verified its FAT16 `BM1ESP.IMG` contents; extracted EFI, kernel, and RAMDISK hashes match the table above. OVMF booted the ISO and loaded the kernel and RAMDISK. The completed image in the project is:

`ISO\guideXOS-CSharp-UEFI-v0.2-BM3B-Diagnostic.iso`

SHA-256: `AF16C0D77FEFAAAB6921E61EB72547F992A4F06BD5C2287C79A76137C974F40C`.

The source ISO in the contained lane and the copied project ISO have matching hashes. The BM1, BM2, RC1, and other existing ISOs were not overwritten.

## 9. `AvailableResolutions` producer and ownership audit

The bounded source audit found:

- `DisplayManager.AvailableResolutions` is a getter-only static property initialized in `Kernel\Drivers\Display.cs:19` from a new 11-element `Resolution[]` (`Display.cs:20–22`). `Resolution` is a value type.
- The array is allocated by the managed static initializer and retained by the `DisplayManager` static root for the managed runtime lifetime.
- No explicit setter, later assignment, replacement, reset, clear, or disposal of this array was found. There is no array owner that calls `Dispose`; the array is GC-managed.
- The initial Display Options consumer is `DisplayOptions.InitializeDisplayOptions()` (`guideXOS\GUI\DisplayOptions.cs:86–87`). The linked code loads the static reference into RDI, tests for null, then reads the array length from `[RDI + 8]`.

This audit does not prove that the physical reference was valid at the time of the AIDA_LPT fault. It does not identify an overwrite, boot-boundary error, runtime-layout issue, or invalid producer. No producer fix has been selected without physical evidence.

## 10. Physical AIDA_LPT retest

Physical testing is owner-performed; no AIDA_LPT was available in this execution environment. Use the verified ISO hash above and the established FAT32 USB method:

1. Mount the BM3B ISO and open its embedded `BM1ESP.IMG` with 7-Zip.
2. Extract `EFI`, `KERNEL.ELF`, and `RAMDISK.IMG` to the root of a FAT32 USB drive.
3. Verify the extracted files against the hashes in section 8.
4. Boot AIDA_LPT using the UEFI removable-media entry. Do not modify internal disks.
5. Open Display Options once. If it faults, photograph the complete display record. Do not repeat the known crash to collect duplicate evidence.

If Display Options opens, record that behavior and confirm the USB files match these hashes before interpreting the change. If it faults, record the full `D`, `A`, `4`, `L`, `I`, and `N` lines along with the existing exception fields.

## 11. Conclusions and next action

QEMU establishes that the diagnostic captures a known RDI accurately and that the active four-level paging calculation classifies the injected RDI and effective address correctly. The real physical RDI remains unknown. The leading BM3A noncanonical-address explanation is still a hypothesis, not a conclusion. **Outcome B** applies: the diagnostic image is qualified in QEMU and awaits physical retest. v0.2 publication remains blocked pending bare-metal acceptance.

After the AIDA_LPT capture:

- If `A` is noncanonical, use that exact value to trace the `DisplayManager.AvailableResolutions` root and its managed lifetime before choosing a correction.
- If `A` is canonical, do not classify this as a stale/noncanonical pointer. Verify the photographed RIP and instruction bytes and continue the narrow architectural `#GP` investigation.
- If the display record disagrees with the expected frame, stop and repair the register capture first.
- If the crash moves or disappears, confirm the booted hashes and record that behavior without claiming a production fix.

The separate `netinit`, Audio icon, Disk Manager, and far-right RAM/system-information crashes remain reported and were not repaired here. No shared mechanism is claimed. The Task Manager tombstoning issue remains deferred.
