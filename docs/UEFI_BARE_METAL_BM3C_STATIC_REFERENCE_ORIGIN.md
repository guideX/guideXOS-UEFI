# BM3C — DisplayManager Static Reference Origin

Date: 2026-10-25
Repository: `D:\dev\guideXOSUEFI`
Branch/starting HEAD: `main` / `829f8d565c10ad4d4aacfa96f8d49140dc768ba3`
Physical target: AIDA_LPT (HP)
Status: **Outcome B** — producer identified, correction passes focused QEMU testing, AIDA_LPT retest pending.

## 1. Finding

The 11-element `Resolution[]` is not being freed, replaced, or corrupted by `DisplayManager`. NativeAOT places that array in the kernel's frozen-object image and puts its reference in the `DisplayManager` preinitialized static data.

The fault is in the project's custom `Corlib.Internal.Runtime.CompilerHelpers.StartupCodeHelpers.InitializeStatics`. It allocated a one-word heap cell, put the static-base object reference in that cell, then published the **cell address** into a GC-static base slot. The generated NativeAOT accessor loads that slot once and treats the result as the static-base object. `DisplayOptions` then reads its `AvailableResolutions` field at offset `+16` from the wrong address. The QEMU pre-fix run showed that field read returning zero; a machine with different adjacent allocator contents can return a noncanonical value such as the BM3B AIDA_LPT RDI.

The correction publishes the static-base object reference directly. That is the representation expected by the generated accessors and used by the project's ordinary static field accesses. QEMU now observes the array at P2 and through P5, and Display Options renders all 11 resolutions.

The physical BM3B fault gives the exact use-site register and instruction, but AIDA_LPT did not have the new P0–P5 checkpoints. The QEMU runs prove the producer and its correction in the same compiled initialization/access path. The physical P2-to-RDI chain still needs confirmation by booting the corrected image on AIDA_LPT.

## 2. Preservation and build lane

Read-only Git preflight found root HEAD `829f8d565c10ad4d4aacfa96f8d49140dc768ba3` on `main`, tracking `origin/main`. The qualified RC1, BM2, and BM3B ISOs remain in `ISO\` with their starting hashes:

| Image | SHA-256 |
|---|---|
| BM2 | `F5EBB4B90840DAEFF803BAB9E5C9F8969AB3DCFA7C6942EF86781346D6E99A62` |
| BM3B | `AF16C0D77FEFAAAB6921E61EB72547F992A4F06BD5C2287C79A76137C974F40C` |
| RC1 | `2A330E0F883C13EA9BE0E9171D20297C3DEA966334E569F5E5E500E915A983E8` |

The original `D:\dev\guideXOSUEFI\out\rt` runtime was not used or edited. The build ran in the ordinary copy `E:\BM3C`; that copy retained the pinned runtime checkout and its pre-existing changes. No reset, stash, clean, branch change, worktree, rebase, merge, cherry-pick, or push was performed. Existing BM2/BM3A documentation and physical BM3B evidence were preserved.

## 3. Original BM3B fault and exact instruction

The physical AIDA_LPT record supplied for BM3B is:

| Register/value | Capture |
|---|---:|
| Vector | `0x0D` (`#GP`) |
| Error code | `0` |
| RIP | `0x1006AC35` |
| RDI | `0x0ECFA39531115428` |
| Effective address | `0x0ECFA39531115430` (`RDI + 8`) |
| CR4 | `0x668` |
| LA57 | `0` |

The exact BM3B kernel used for symbolization is SHA-256 `67CD04E84F69C8C3FE32A412D922493E1426C1AB08A3FBCCEF69B79CBC224AFB`. Its matching `Kernel.map` SHA-256 is `180BC1100B73679C7991097BFEA18C76EED8918D7C379E7C783203B4CE978430`. That map places `DisplayOptions.InitializeDisplayOptions` at `0x1006ABC8`; RIP is `+0x6D` in that function. The bytes at RIP are `8B 5F 08`, or `mov ebx, dword ptr [rdi+8]`.

The preceding generated code obtains the `DisplayManager` GC-static base, loads the `AvailableResolutions` backing-field reference at base offset `+16` into RDI, checks it for null, and reads the array length at `[rdi+8]`. Thus the physical RDI is the value loaded from the static field slot, and the faulting operand is the array-length read. This was verified against BM3B's own kernel and map; BM2 symbols were not reused.

## 4. NativeAOT field storage and true array producer

`Kernel\Drivers\Display.cs` declares the getter-only static auto-property `DisplayManager.AvailableResolutions` from an 11-element `new Resolution[]`. `Resolution` is two `ushort` fields, so each element is four bytes. There is no property setter, later replacement, or disposal path.

For this kernel image, the linked NativeAOT map identifies:

| Image object/storage | Linked address | Meaning |
|---|---:|---|
| `?__GCSTATICS@...DisplayManager@@` | `0x1047D640` | GC-static base cell consumed by the accessor |
| DisplayManager preinit blob | `0x100D5C28` | Static field values copied into the runtime base object |
| expected static-base EEType | `0x100D5D28` | `DisplayManager` static-base object type; base size `0x20` |
| `__FrozenObj_...AvailableResolutions_k__BackingField` | `0x1013CCE8` | AOT-frozen 11-element `Resolution[]` |
| array EEType | `0x100D3590` | Array type; QEMU memory shows length `0x0B` at object `+8` |

The array is therefore **not allocated from the kernel heap at boot**. It is a frozen NativeAOT image object. `InitializeStatics` allocates the per-type static-base object, then copies the preinitialized backing-field reference from `0x100D5C28` into its field at offset `+16`.

The generated accessor at `0x10001367` is a single-load accessor:

```asm
10001367: 48 8D 05 D2 C2 47 00    lea rax, [rip+0x47c2d2]  ; 0x1047D640
1000136E: 48 8B 00                mov rax, [rax]
10001371: C3                      ret
```

The caller then accesses the static-base object's fields directly. There is no second handle dereference in generated code.

## 5. Exact publisher defect and correction

`guideXOS\guideXOS.csproj` imports `Corlib\Corlib.projitems`, which compiles `Corlib\Internal\Runtime\CompilerHelpers\StartupCodeHelpers.cs` into the kernel. The linked map names the actual compiled methods `StartupCodeHelpers.InitializeStatics` at `0x1000F444` and `RhpNewFast` at `0x1000F0C8`. This project-local helper—not the similarly named source file under `out\rt`—is the static-base publisher used by this kernel.

Before correction, the helper did the following for every uninitialized GC-static base:

```csharp
var obj = RhpNewFast(staticBaseEEType);
copyPreinitializedFields(obj);
var handle = malloc(sizeof(IntPtr));
*(IntPtr*)handle = Unsafe.As<object, IntPtr>(ref obj);
*pBlock = handle;
```

The linked BM3C-v2 disassembly confirms the sequence: `InitializeStatics` calls `RhpNewFast`, allocates eight bytes through `malloc`, stores the object pointer in that allocation, and stores the allocation address into the GC-static cell. That allocation address is a one-word handle cell.

The generated accessor returns that handle-cell address as though it were the static-base object. The generated field load at `+16` then reads beyond the one-word cell. In QEMU-v2, the handle cell was at `0x4020000`, the static-base object behind it was at `0x401F000`, and the invalid field address was `0x4020010`. That address contained zero. The actual static-base object's `+16` field at `0x401F010` contained the valid frozen-array reference `0x1013CCE8`.

The reviewed correction in `Corlib\Internal\Runtime\CompilerHelpers\StartupCodeHelpers.cs` removes the handle allocation and publishes the object itself:

```csharp
*pBlock = Unsafe.As<object, IntPtr>(ref obj);
```

The BM3C-v3 linked disassembly at `0x1000F4C7–0x1000F4CC` now loads the `RhpNewFast` object pointer and writes it directly to `*pBlock`; it no longer calls `malloc(8)` for a wrapper. This matches the single-load accessor contract.

`RhpNewFast` allocates the 32-byte static-base object through `malloc`, which is the kernel's `stdlib.malloc` export to `Allocator.Allocate`. The custom kernel allocator does not move or sweep managed objects. This per-module static-base object has module/kernel lifetime, and no owner frees or disposes it. The 11-element array remains rooted by the corrected static-base field and resides in the loaded frozen-object image. The correction removes an unnecessary handle allocation while restoring the expected root representation.

## 6. P0–P5 checkpoint observations

BM3C stores five fixed-size records with numeric stage IDs. No dynamic strings or extra allocations were added to the pointer recorder. It records raw addresses and dereferences only canonical, page-translated, writable addresses. It never dereferences the captured noncanonical AIDA_LPT RDI.

Boot mode `1` is UEFI. The BM stage columns below are the current `Stage/Substage` values from the diagnostic handoff. P0 is before `InitializeModules`; P2 is immediately after it; P3 is after framebuffer initialization; P4 is at `DisplayOptions.InitializeDisplayOptions` entry; P5 is immediately before reading `DisplayManager.AvailableResolutions`.

**P1 is image evidence rather than a runtime checkpoint:** the map and QEMU memory show the frozen array at `0x1013CCE8`, EEType `0x100D3590`, length 11. There is no runtime `new Resolution[11]` allocation at these checkpoints.

### QEMU-v2, before the source correction

| Checkpoint | Boot/BM stage | Accessor base | Generated field slot | Slot value | First qword at base | Actual static object / field / array |
|---|---|---:|---:|---:|---:|---|
| P0 | UEFI, `3/0` | `0x100D5D2B` (tagged uninitialized EEType) | `0x100D5D3B` (computed only; not read) | not read | not read | not read |
| P2 | UEFI, `4/0` | `0x4020000` | `0x4020010` | `0` | `0x401F000` | EEType `0x100D5D28`; field `0x401F010` = `0x1013CCE8` |
| P3 | UEFI, `6/1` | `0x4020000` | `0x4020010` | `0` | `0x401F000` | same type, field, and array reference |
| P4 | UEFI, `13/0` | `0x4020000` | `0x4020010` | `0` | `0x401F000` | same values immediately before opening |
| P5 | UEFI, `13/0` | `0x4020000` | `0x4020010` | `0` | `0x401F000` | same values immediately before field read |

The v2 checkpoint flags were `0x8000FFFC` at P2–P5. They show the returned base and field address were canonical/mapped and that the address behind the handle cell contained the expected static-base EEType. The use-site field slot itself contained zero. Thus the first proven wrong state is **P2**, immediately after `InitializeModules` publishes the GC-static bases. It was already wrong before graphics initialization and before any Display Options instance existed. The array and static-base object remained present behind the extra indirection through P5.

### QEMU-v3, after the source correction

| Checkpoint | Boot/BM stage | Static-base object | Generated field slot | Slot value | First qword at base |
|---|---|---:|---:|---:|---:|
| P0 | UEFI, `3/0` | `0x100D5D2B` (tagged uninitialized EEType) | `0x100D5D3B` (computed only; not read) | not read | not read |
| P2 | UEFI, `4/0` | `0x4012000` | `0x4012010` | `0x1013CCE8` | `0x100D5D28` (expected static-base EEType) |
| P3 | UEFI, `6/1` | `0x4012000` | `0x4012010` | `0x1013CCE8` | `0x100D5D28` |
| P4 | UEFI, `13/0` | `0x4012000` | `0x4012010` | `0x1013CCE8` | `0x100D5D28` |
| P5 | UEFI, `13/0` | `0x4012000` | `0x4012010` | `0x1013CCE8` | `0x100D5D28` |

The second P4/P5 pair after close/reopen reported the same addresses and value. A QEMU monitor read of the **validated QEMU addresses** showed `[0x4012000] = 0x100D5D28`, `[0x4012010] = 0x1013CCE8`, and the array header at `0x1013CCE8` as EEType `0x100D3590`, length 11. The heap addresses differ between v2 and v3; the evidence is the corrected direct-object topology and stable lifetime, not equality of absolute allocator addresses.

The physical BM3B run has no P0–P5 rows. Its physical RDI is therefore not assigned a physical P2 value by inference alone. The exact source-to-use-site mapping, v2 reproduction, and v3 correction make the custom static-base publisher the proven code defect; physical confirmation of the corrected chain remains the AIDA_LPT acceptance run.

## 7. Ownership, lifetime, and BM1 comparison

The source audit found one `AvailableResolutions` initializer, no setter or later assignment, and no `Dispose` call. The array is frozen image data, not a temporary heap array. The owning `DisplayManager` GC-static base is intended to live for the kernel/module lifetime. QEMU-v3 observes that same direct base and array reference at P2, P3, P4, and P5, with no replacement.

BM2's `NotificationManager.ResetForKernelBoot()` remains in `guideXOS\Program.cs` before the UEFI desktop boot. It drops the prior notification list root without traversing or disposing old objects. That boot-state reset is distinct from the `InitializeStatics` representation error: the latter occurs during generic GC-static publication and is visible immediately at P2. This BM3C evidence does not retroactively claim that BM1's NotificationManager symptom had the same trigger. The corrected `InitializeStatics` applies to all module GC-static base entries, so all such fields now use the representation their generated accessors expect.

## 8. Source changes and bounded QEMU validation

Production correction:

- `Corlib\Internal\Runtime\CompilerHelpers\StartupCodeHelpers.cs`: publish the static-base object pointer directly instead of allocating and publishing a one-word handle cell.

Diagnostic checkpoints:

- `Kernel\Misc\BareMetalMilestones.cs`: five fixed-size pointer-chain records with range checks before reads.
- `Kernel\Misc\EntryPoint.cs`: P0, P2, and P3 placements and handoff-layout assertions.
- `Kernel\Drivers\Display.cs`: diagnostic-only static-address helper.
- `guideXOS\GUI\DisplayOptions.cs`: P4/P5 placements.
- `guideXOS\Kernel\Misc\UefiBootInfo.cs` and `guideXOSBootLoader\paging.h`: matching fixed-size record layout.

The contained build command was `build.ps1 -UefiDiagnosticMode BM1 -SkipRamdisk`. The existing qualified RAMDISK was preserved. The post-kernel managed-artifact audit reports 41 artifacts, with descriptor, build/staging, RAMDISK/staging, and compiled-allowlist agreement all `41/41`.

The corrected ISO was booted from OVMF in QEMU at 1280×800. The desktop appeared; Start → All Programs → Display Options opened; the Resolution tab rendered the full 11-entry list and highlighted `1280 x 800`; the window closed and reopened; the background tab rendered after reopening. No exception/fault record appeared. The heartbeat reached frame 600 with `GRAPHICS_VALID=1`, `ALLOCATOR_FREE_INVALID=0`, and `ALLOCATOR_FREE_CORRUPT=0`; input and the desktop continued updating after the reopen. No unrelated applications were tested.

Build artifacts and identity checks:

| Artifact | SHA-256 |
|---|---|
| BM3C-v3 `kernel.elf` | `A1296D932444342058B87501133461791C37115A0147F629C4AB925B2DE31F47` |
| `BOOTX64.EFI` | `BC988B6705E979614CF25C8083A61BC9D3A5A209282E910B226CA354DAC7EFDB` |
| `RAMDISK.IMG` | `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8` |
| final ISO | `6999A58EBF627BA65DC343A5D04FF1D099E6950A8266E50DD330212C04CEDFFE` |

The ISO is 196,587,520 bytes and is present at:

`D:\dev\guideXOSUEFI\ISO\guideXOS-CSharp-UEFI-v0.2-BM3C-Diagnostic.iso`

7-Zip verified the ISO root and embedded FAT16 `BM1ESP.IMG`. The root and embedded kernel, EFI loader, and RAMDISK hashes match the table. QEMU booted this same ISO hash. Existing BM2, BM3B, RC1, and `guideXOS.iso` files were not overwritten.

## 9. Physical acceptance and next action

Physical AIDA_LPT testing was not available in this execution environment. Use the BM3C ISO above for one focused test: boot to the desktop, open Display Options, verify the resolution list displays, close and reopen the window, and confirm the desktop remains responsive. If it faults, capture the complete BM fault record and register values, especially RDI, RIP, CR4, and the effective address. Do not combine this run with Disk Manager, audio, or networking tests.

Outcome B is complete: the static-base publisher is the defect, the corrected source passes the focused QEMU flow, and the physical retest is pending. No production change was made to the `DisplayOptions` dereference, no arbitrary display modes were added, and no object was globally freed. No push or publication occurred.
