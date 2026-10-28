# guideXOS C# UEFI BM2 — Identical #GP Investigation

**Outcome B:** the faulting instruction and unsafe dereference are identified, the reset path is corrected in a copied build tree, and the corrected diagnostic ISO passes OVMF/QEMU. Physical retest on both laptops is still required; BM2 is not physically accepted.

## 1. Repository state and evidence protection

- Repository: `D:\dev\guideXOSUEFI`, branch `main`.
- Starting and ending HEAD: `c838481ef0d1347b1b979741204ea3cff2a374b3`.
- The repository worktree was clean before this task. The final worktree contains the requested new report and BM2 ISO as untracked artifacts; no source file in the repository was changed. No commit or push was made.
- The physically tested BM1 ISO remains at `C:\BM1\out\guideXOS-CSharp-UEFI-v0.2-BM1-Diagnostic-Final.iso`, SHA-256 `5C781CA5627D90DAE73DBA7A9716864381F8DBBA3675DA08C02BEF93FF287253`.
- Its embedded kernel ELF hash is `65B3A71B4C71E2865FB668CDFAD56E713ED8944380062E86F3083A601439F0D6`. The matching ELF was also found in its embedded FAT16 payload. The embedded EFI hash is `6363E453C87A2900005D7C25E3D2B54D9B43FC0EDA531A1F10BA7AFA6C960059`; the RAMDISK hash is `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8`.
- The existing project ISO `ISO\guideXOS.iso` was left untouched; its hash remains `1FB747B254B1FC9329EF0BF76D72D8E3CE8290289ACA52BCE5FBD1391441FFFA`. The prior BM1 report records the accepted RC1 hash as `2A330E0F883C13EA9BE0E9171D20297C3DEA966334E569F5E5E500E915A983E8` and says no matching ISO is present in the repository. No RC1 image was overwritten or rebuilt.
- All investigation edits and builds were performed in the ordinary copy `C:\BM2`. The BM1 ISO and pinned runtime in `C:\BM1` were not used as build outputs; the repository's runtime cache was not rebuilt.

## 2. Exact BM1 binary and symbolization

The faulting image is the BM1 ISO above, not a later QA kernel. Its root `kernel.elf` and embedded `BM1ESP.IMG\KERNEL.ELF` have the same SHA-256. The ELF is `ET_EXEC`, with linked entry `0x10045050`; the UEFI loader maps its linked virtual addresses to physical pages and keeps the kernel executing at those linked virtual addresses. Thus the reported RIP `0x10076170` is the linked VMA used for disassembly, not a guessed physical offset.

The exact ELF is stripped: it has no symbol table or debug information. Its executable `PT_LOAD` segment 2 begins at file offset/VMA `0x6000/0x10006000`; the fault lies in that segment. I used its exact bytes and `.pdata` function boundaries, plus direct call references and the matching source tree. I did not use the later normal-mode QA `Kernel.map` to assign a BM1 symbol.

The exact instruction is:

```text
0x10076170: 8B 79 10    mov edi, dword ptr [rcx + 0x10]
```

The containing runtime-function range is `0x10076158–0x10076203` (exclusive end), so the fault is at offset `+0x18`. Disassembly was inspected across the real function boundaries with more than 64 bytes on each side. Immediately before the load, the function obtains `NotificationManager` GC statics, loads the `Notifications` field at static-base offset `+8`, tests it for null, and then reads the list count at object offset `+0x10`. The source implementation is `guideXOS.GUI.NotificationManager.ResetForKernelBoot()` in `guideXOS\GUI\NotificationManager.cs:70–78`; NativeAOT generated this code in the managed kernel image. It is not the early exception assembly stub.

The reconstructed call path is the UEFI entry through `Program.KMain()` to the reset. `guideXOS\Program.cs:842–844` writes `[BOOT_MODE] UEFI`, calls `NotificationManager.ResetForKernelBoot()`, and then initializes the animator. The exact ELF's stripped call sites and function ranges agree with that order. A matching link map was not available, so the managed method identification is a source-backed reconstruction rather than a symbol-table lookup.

## 3. Why this instruction raises #GP

For the reported vector `0x0D` and error code `0`, the relevant cause at this ordinary memory load is a noncanonical effective linear address. `RCX` held the non-null `Notifications` reference; the instruction accessed `[RCX+0x10]`. If `RCX` itself was noncanonical, or if adding `0x10` crossed the active canonical-address boundary, the load raises #GP. An unmapped but canonical address would instead raise #PF.

The physical captures contain no `RCX`, so its exact value and the writer that made it invalid cannot be recovered from the supplied data. The source field has no explicit initializer, and the reset runs before the normal notification setup. The method nevertheless traversed and disposed objects from the old notification list before replacing the root. That is unsafe across a kernel/allocator reset: the related warm-reset code in `Program.cs` already documents that old-boot object references may no longer belong to the current allocator. The proximate root cause is therefore established: reset dereferenced an invalid old `Notifications` reference before discarding it. The deeper origin of the non-null corrupt reference (for example, stale heap state versus an earlier overwrite) is not proven by the available register dump.

This instruction performs no privileged operation, MSR access, feature-dependent operation, or alignment-sensitive vector access. In 64-bit mode the ordinary DS base is not the likely issue here; a bad canonical address gives the observed #GP, while a canonical but unmapped address gives #PF. CR2 being zero is consistent with this being #GP and does not identify its cause. The two CR3 values describe different page-table roots; they do not make a noncanonical pointer valid.

## 4. Last completed boot stage

The last definitely completed BM1 marker is **BM09.8**, recorded in `KernelMain()` immediately before `Program.KMain()` (`Kernel\Misc\EntryPoint.cs:858–860`). The fault occurs in the reset call before `Animator.Initialize()` returns and before the later BM10/desktop initialization markers. The desktop drawing path was not entered on the physical runs; the diagnostic fault renderer itself did draw the milestone strip and CPU state.

The small glyph reported beneath BM09 is not a BM09.5 stage. The fault handler writes `Substage = UINT32_MAX`; the strip renderer prints `Substage % 10`, which produces `5`. It is a fault sentinel rendered through the substage digit, not evidence that BM09.5 completed. The supplied attachment contains the register table but no screenshot files, so the marker interpretation is based on the implementation and the reported register values.

## 5. Hardware and QEMU comparison

The ASUS VivoBook and AIDA_LPT both reported vector `0x0D`, error `0`, RIP `0x10076170`, CR2 `0`, and RSP `0x27FD70`; their CR3 values differed (`0xA52DB000` and `0xAAB03000`). This repeatability points to the shared reset code and invalid reference rather than a framebuffer instruction. Since `RCX` was not captured, the precise reason the physical runs had a bad reference while QEMU did not remains unknown. Firmware memory maps and allocator placement can affect the contents and addresses of managed state; CPU feature flags, interrupt state, and framebuffer format do not explain this particular `mov` once a noncanonical address is supplied.

The existing BM1 QEMU log reached BM09.8, `SMAIN_DISPATCH_REASON=UEFI_CONTINUOUS_DESKTOP`, and frame-60 heartbeat. It did not reproduce the physical invalid-reference state. A first BM2 packaging attempt also used a later, non-matching EFI from the copied build tree; that image stopped at framebuffer validation with pixel-format failure code 3 and was discarded. The final candidate instead uses the exact EFI and RAMDISK extracted from the qualified BM1 ISO. With those matching inputs, the corrected candidate reached BM1:05.1, BM1:09.8, desktop dispatch, and `CONTINUOUS_HEARTBEAT_FRAME=60`, with valid graphics and zero allocator invalid-free/corruption counters. The successful serial log is `C:\BM2\out\bm2-qemu-serial-b.log`.

## 6. Correction and focused regression

Only `NotificationManager.ResetForKernelBoot()` was changed in the copied tree `C:\BM2\guideXOS\GUI\NotificationManager.cs`. It now assigns a fresh empty `List<Notify>` directly. It does not read, enumerate, or dispose the prior list or its notification/animation graph. The normal empty-list state is preserved, and the fault is not caught or masked.

The rebuilt caller was inspected in the produced ELF. NativeAOT inlined the reset into `Program.KMain()`: it obtains the notification manager static base, computes the field address, and stores the newly constructed list through the GC write barrier (`0x10006b51–0x10006b5d`). There is no load of the old `Notifications` field before that store. This is a focused post-build regression check of the exact unsafe precondition.

There is no kernel unit-test harness in this repository: the available automated targets cover desktop interaction and a Task Manager regression. Injecting a noncanonical pointer into a managed GC static would require an unsafe test-only kernel mutation, so I did not add that to production code. The focused disassembly check plus booting the corrected kernel through the formerly failing boundary is the regression evidence for this diagnostic build.

Build command in the copy: `C:\BM2\build.ps1 -UefiDiagnosticMode BM1 -SkipBootloader -SkipRamdisk` (exit 0). Managed artifact admission and staging both agreed on all 41 artifacts (`41/41`). The kernel SHA-256 is `6EF35B6825A3C7C57FA745B2FD276AC216942D78DCAAEB85B70827333F7D1A90`. The final image embeds that kernel with the exact BM1 EFI and RAMDISK hashes listed above.

## 7. Final ISO and owner retest

The final copied image is:

`D:\dev\guideXOSUEFI\ISO\guideXOS-CSharp-UEFI-v0.2-BM2-Diagnostic.iso`

SHA-256: `F5EBB4B90840DAEFF803BAB9E5C9F8969AB3DCFA7C6942EF86781346D6E99A62`.

I extracted the ISO's FAT16 El Torito image and verified the EFI, kernel, and RAMDISK hashes against the staged files. The ISO is a diagnostic image, not a release or physical-acceptance claim.

Owner checklist:

1. Verify the ISO SHA-256 above before preparing a USB. Use the already proven FAT32 removable-media layout: `EFI\BOOT\BOOTX64.EFI`, `KERNEL.ELF`, and `RAMDISK.IMG`.
2. Select the USB explicitly from the one-time boot menu on the ASUS VivoBook and AIDA_LPT. No internal disk needs to be flashed or modified.
3. For each laptop, record whether it passes BM09.8 and enters BM10/desktop. If it faults, capture vector, error, RIP, CR2, CR3, RSP, the screen, and (if available) RCX.

**Physical retest is required.** Outcome A must not be claimed until both previously failing laptops pass the boundary. Remaining release blockers are those physical results and, if a comparison to accepted RC1 is required, locating the RC1 image matching the documented hash; the current repository ISO does not match that hash.
