# guideXOS C# UEFI BM3A — Physical Crash Triage

**Outcome B — the Display Options instruction and source dereference are identified, but the physical register/object evidence needed to establish the corrupt value and its producer is missing.** No code or build artifact was changed. BM3B should add one bounded #GP register capture, then make a source correction only if that capture identifies the faulty owner or writer.

## 1. Scope and physical evidence

This is a read-only source and artifact review of the reported AIDA_LPT (HP) failures. The owner reports that AIDA_LPT reaches the graphical desktop, its touchpad works, and the Start menu launches apps. The ASUS VivoBook also reaches the desktop but currently lacks functional input. BM3A did not retest either machine.

| Scenario | Physical evidence supplied | BM3A disposition |
|---|---|---|
| Display Options | AIDA_LPT; vector `0x0D` (#GP), error `0`, RIP `0x1006A5C5`, CR2 `0`, CR3 `0xAAB03000`, RSP `0x27F790`. The diagnostic screen reportedly showed BM milestones, but no milestone value or GPR dump was included in the supplied text. | Exact BM2 instruction and source statement identified below. RDI is absent, so the invalid value and its producer are not proven. |
| `netinit` | Owner-reported whole-OS crash; no vector, error, RIP, registers, or authoritative serial capture supplied. | Source path reviewed only. No pass or root cause claimed. |
| Audio desktop icon | Owner-reported whole-OS crash when opening the icon; no vector/RIP/register capture supplied. | Source path reviewed only. No pass or root cause claimed. |
| Disk Manager | Owner-reported whole-OS crash when opening; no vector/RIP/register capture supplied. | Source path reviewed only. No pass or root cause claimed. |
| Far-right RAM/system-information area | Owner-reported crash on moving to or interacting with the area; exact coordinates/action and no vector/RIP/register capture supplied. | Mapped to the right-edge widget reveal, hover, update, and draw path. Exact hit target remains unconfirmed. |

The prior BM1 startup fault (`NotificationManager.ResetForKernelBoot`, RIP `0x10076170`) is a separate, previously diagnosed event. The owner reports that BM2 reaches the desktop on both laptops. The accepted BM2 correction is in the `C:\BM2` build copy and exact BM2 kernel, but **is not yet integrated into the live `guideXOS\GUI\NotificationManager.cs`**. Preserve and later integrate that correction and the accepted Task Manager lifetime fixes; BM3A made no changes to either.

## 2. Read-only preflight and evidence preservation

Preflight was taken on 2026-10-24 in `D:\dev\guideXOSUEFI`:

- Starting HEAD: `c838481ef0d1347b1b979741204ea3cff2a374b3`.
- Branch/upstream: `main` / `origin/main`, ahead/behind `0/0`.
- Root repository: no tracked changes; three pre-existing untracked files were the BM2 ISO, RC1 ISO, and BM2 report listed below.
- Nested `out\rt`: HEAD `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; 58 pre-existing status entries (54 modified, four untracked). They were left untouched.
- BM2 sandbox: `C:\BM2` exists; `D:\BM2` does not. Investigation artifacts there were read only. The live repository was not replaced from the sandbox.
- BM1 locations `C:\BM1` and `D:\BM1` were left untouched. The BM1 report records the physically tested ISO at `C:\BM1\out\guideXOS-CSharp-UEFI-v0.2-BM1-Diagnostic-Final.iso`, SHA-256 `5C781CA5627D90DAE73DBA7A9716864381F8DBBA3675DA08C02BEF93FF287253`.

Protected ISO/report evidence and observed hashes:

| Artifact | SHA-256 / state |
|---|---|
| `ISO\guideXOS-CSharp-UEFI-v0.2-BM2-Diagnostic.iso` | `F5EBB4B90840DAEFF803BAB9E5C9F8969AB3DCFA7C6942EF86781346D6E99A62` — matches the expected BM2 hash. |
| `ISO\guideXOS-CSharp-UEFI-v0.2-RC1.iso` | `2A330E0F883C13EA9BE0E9171D20297C3DEA966334E569F5E5E500E915A983E8` — matches the accepted RC1 hash. This file was present before BM3A and was not modified. The older BM2 report's statement that no matching RC1 ISO was present is stale; that report itself was preserved unchanged. |
| `docs\UEFI_BARE_METAL_BM2_GP_FAULT.md` | Pre-existing untracked BM2 report; preserved unchanged. |
| `C:\BM2\kernel.elf` and extracted `C:\BM2\out\bm2-iso-audit\fat\KERNEL.ELF` | `6EF35B6825A3C7C57FA745B2FD276AC216942D78DCAAEB85B70827333F7D1A90`. |

At the end, HEAD, branch, upstream, and nested runtime state were unchanged. Root tracked files remain unchanged. The only file added by BM3A is this report; root untracked files are the three pre-existing artifacts above plus this report. No reset, clean, stash, branch/worktree operation, source copy, build, test, ISO packaging, commit, or push was performed.

## 3. Exact BM2 kernel and symbol evidence

The expected BM2 ISO hash was independently verified. Its embedded FAT kernel matches the sandbox kernel at SHA-256 `6EF35B6825A3C7C57FA745B2FD276AC216942D78DCAAEB85B70827333F7D1A90` (4,747,264 bytes). The ELF is x86-64 `ET_EXEC`, entry `0x10045088`, and has no ELF section table or symbol/debug sections. The live repository's `kernel.elf` and `ESP\kernel.elf` instead hash to `144E6959A0C1A592CAE7B4F33188093385E4DBC9B7928CF352051B4EEEA42491` (4,718,592 bytes). They are not the BM2 binary and were not used to symbolize the physical RIP.

Matching BM2 build evidence is present in `C:\BM2`:

| Artifact | Identity |
|---|---|
| `C:\BM2\guideXOS\Kernel.map` | SHA-256 `09A5CD2AFB3F829443136D4BA444B36FAEF7CCA990D415DADDF57B1861D863A6`; maps `InitializeDisplayOptions` to `0x1006A558`. |
| `C:\BM2\guideXOS\obj\x64\Release\net7.0\win-x64\native\guideXOS.obj` | SHA-256 `163A2C8BAF0B6624B032E90C68494DE6F1702FF9F0A2AC20E9EE9A9561D0D87E`; NativeAOT x64 COFF object with `.managedcode$I`, `.debug$S`, and `.debug$T` sections and symbols. Its method instruction bytes match the linked ELF bytes at the map address. |
| `C:\BM2\guideXOS\obj\x64\Release\net7.0\win-x64\guideXOS.pdb` | SHA-256 `F4778CC5EB5F75DE11D0F0DD234DC72C2E9FD1E0E15DC4392BFE62D129AC6767`; portable managed PDB, not an ELF DWARF companion. |
| `C:\BM2\out\bm2-kernel-build.log` | SHA-256 `EF2084185C6571D364C32E97C7A940F81B700542EBD206ED6657523CCF2D17F1`; original BM2 build record. |

The source tree used for the method mapping is confirmed against the BM2 copy for the two relevant source files: live and `C:\BM2` `guideXOS\GUI\DisplayOptions.cs` both hash to `7B6F115A6AE7780A21606502CAFACF4ED19DD3D550650C299DC3FACC3012D40B`; live and sandbox `Kernel\Drivers\Display.cs` both hash to `71A65588D07ED1A542AD6B0D3AEAE7605D52DD3D0A6D2510D4CD13BD93ECC648`. The final ELF is stripped, so the method name comes from the matching BM2 map/object and the source line from the byte-identical source, not from symbols in the ELF itself.

## 4. Faulting instruction and control flow

The BM2 map starts `guideXOS_guideXOS_GUI_DisplayOptions__InitializeDisplayOptions` at `0x1006A558`. The reported RIP is offset `+0x6D` in that method. The exact linked bytes and adjacent instruction boundaries are:

```text
0x1006A5B3  call 0x10001367                  ; DisplayManager GC-static-base helper
0x1006A5B8  48 8B 78 10   mov rdi, [rax+0x10] ; load AvailableResolutions reference
0x1006A5BC  48 85 FF      test rdi, rdi
0x1006A5BF  0F 84 1F 01 00 00  je 0x1006A6E4 ; null -> empty-list path
0x1006A5C5  8B 5F 08      mov ebx, dword ptr [rdi+0x8] ; FAULT, array Length
0x1006A5C8  85 DB         test ebx, ebx
0x1006A5CA  0F 84 14 01 00 00  je 0x1006A6E4
0x1006A5D0  call 0x100010B5                  ; Framebuffer non-GC static-base helper, in Current getter path
```

The fault is a 32-bit scalar memory load from the array header, not a store, virtual call, mode-setting operation, or hand-written assembly. It is NativeAOT-generated machine code for C# `DisplayOptions.InitializeDisplayOptions()`, at `guideXOS\GUI\DisplayOptions.cs:87`, the `list.Length` part of `if (list != null && list.Length > 0)`. The preceding `test` and conditional branch show that this execution passed the null check. The source caller is the `DisplayOptions` constructor (`DisplayOptions.cs:61–72`), invoked by `DisplayOptionsApplicationFactory.TryCreateOrActivate` (`guideXOS\OS\ApplicationFactories.cs:1311–1368`; map entry `0x100300D0`).

The exact expression dereferences the value in **RDI** at effective address `RDI + 8`. RDI is loaded from offset `+0x10` in the `DisplayManager` GC-static base. The static field is the backing field for `DisplayManager.AvailableResolutions`. The BM2 map also names its frozen object at `0x1013BCE8`; that object has array length `11` at `+8` in the linked image. The expected object address is canonical and lies in the kernel's loadable image. The physical capture omitted RDI, so BM3A cannot determine whether the static slot held this expected pointer, another canonical pointer, or a noncanonical value.

**Last definitely completed operation:** the static field was loaded and its non-null check passed. The array length load at `0x1006A5C5` did not complete. Consequently, `DisplayManager.Current`, resolution comparison/label creation, background and font-list initialization, factory window registration, `WindowManager.MoveToEnd`, visibility, and the first window draw were not reached on this path.

## 5. Why the instruction can raise #GP(0)

At CPL0 in 64-bit mode, this is an ordinary unaligned-capable scalar load through the default DS segment. The applicable memory-address condition for `#GP(0)` is a **noncanonical effective linear address** produced by `RDI + 8`, using the active 48-bit or 57-bit canonical-address width. CR4.LA57 was not captured, so the active width is not known. DS base is ignored for this access in 64-bit mode. This instruction does not use a segment selector, privilege transition, control register, MSR, or CPU feature-specific operation. Scalar alignment does not explain this CPL0 `#GP`; a canonical but unmapped/protected address would normally raise `#PF`, and CR2 would then identify the faulting linear address. See the [Intel 64 and IA-32 Software Developer's Manual, Volume 2](https://www.intel.com/content/dam/www/public/us/en/documents/manuals/64-ia-32-architectures-software-developer-vol-2b-manual.pdf) and [Intel SDM Volume 1–4](https://cdrdv2-public.intel.com/874240/325462-090-sdm-vol-1-2abcd-3abcd-4.pdf) for the instruction, canonical-address, and alignment rules.

**Confirmed:** RIP is the `mov ebx, [rdi+8]` array-length read in the exact BM2 kernel; the non-null branch was taken; the array reference originates from the `DisplayManager` GC-static slot; the reported exception is vector `0x0D`, error `0`.

**Best-supported explanation, not yet proven:** the effective address was noncanonical, likely because the `AvailableResolutions` static slot/reference or the observed execution context was invalid. The frozen array expected at `0x1013BCE8` is valid in the image, so this is not evidence that the source's hardcoded resolution list itself is malformed.

**Not established:** the value of RDI, whether its effective address was noncanonical under this CPU's active width, who wrote the bad value, whether the static slot was corrupted, or whether an image/runtime-state mismatch occurred. `CR2=0` does not establish a `#GP` cause; the BM1 diagnostic fault record explicitly stores zero for CR2 on every non-`#PF` vector (`Kernel\Misc\IDT.cs:1111–1115`). The recorded CR3 is useful page-table context but cannot supply RDI or prove that the expected object was mapped in the physical run.

## 6. Display Options launch and object lifetime

There are two source routes to the same app factory:

1. Start-menu row activation calls `Desktop.LaunchApplication(appName)` (`guideXOS\GUI\StartMenu.cs:559`), which creates a `LaunchRequest` and dispatches through `Desktop.TryLaunchFactoryRequest` (`guideXOS\GUI\Desktop.cs:228, 265–301`). `AppModel` registers `gxos.builtin.displayoptions` at `guideXOS\OS\AppModel.cs:75`.
2. The desktop display context-menu route creates the same app ID in `Desktop.LaunchDisplayOptions` (`guideXOS\GUI\Desktop.cs:386–395`) and uses the same factory dispatcher.

`DisplayOptionsApplicationFactory.TryCreateOrActivate` first creates app services, reuses an app-owned window if present or invokes `new DisplayOptions(...)`, then adds the completed window to the app instance, moves it to the end of the window list, and makes it visible. The constructor invokes `InitializeDisplayOptions` before those final registration/visibility steps. The fault therefore occurs during construction, before initial drawing.

`DisplayManager.AvailableResolutions` in `Kernel\Drivers\Display.cs:19–22` is a static, hardcoded 11-entry `Resolution[]`, not a UEFI boot-services list or a live mode enumeration from the display controller. The next `DisplayManager.Current` access reads framebuffer width/height; later resolution changes call the SVGA path. No field reset, disposal, or writer of `AvailableResolutions` was found in the reviewed source. `list` is a stack-local reference into shared static GC state. The private `DisplayOptions` arrays/lists (`_resLabels`, background paths, thumbnails, font paths) are initialized after the faulting load and cannot be its producer. The fault does not establish a physical GPU/GOP failure.

The BM1 and BM2 issue is similar only at the broad level of an unsafe managed reference. BM1's `NotificationManager.ResetForKernelBoot` traversed the old static `Notifications` list and disposed its entries before replacement (`guideXOS\GUI\NotificationManager.cs:70–78`). The BM2 copy assigns a fresh list without reading or traversing the prior graph. In live source that old traversal remains; it should be integrated in a later source-change phase. Display Options instead reads a different static field owned by `DisplayManager`; source shows no boot reset or disposal of that array, and it is not a notification, Task Manager, window, or boot-info object. No source evidence establishes the same stale-reference defect or common writer. Phase 35R15's accepted managed-read/allocator gates and the Task Manager R3 ownership repair cover their stated workloads; they do not establish safety of this physical Display Options static read.

## 7. Bounded review of the other four paths

### `netinit`

`guideXOS\DefaultApps\FConsole.cs:875–913` handles the command. If `NETv4.Sender` is already non-null, it reports the existing network state and exits. Otherwise it calls `NETv4.Initialize()` (`Kernel\Network\NETv4.cs:90`), which resets network globals and creates ARP/TCP/UDP managed lists, then scans with `Intel825xx.Initialize()` and `RTL8111.Initialize()`.

The first hardware work enumerates `PCI.Devices` and reads vendor/device IDs. A matching driver accesses PCI registers/BARs and device registers, allocates DMA descriptor storage, and initializes the NIC. Those paths share allocator/runtime execution with all managed kernel code but do not read `DisplayManager.AvailableResolutions` or construct Display Options. No capture ties the crash to a list reset, PCI access, DMA allocation, or one driver. Network initialization remains a separate physical investigation.

### Audio desktop icon

The UEFI desktop's Audio icon targets `WAV Player` (`guideXOS\GUI\Desktop.cs:846–864`); the built-in factory is registered in `guideXOS\OS\ApplicationFactories.cs:253–254`. Device setup is already performed during boot (`guideXOS\Program.cs:997–999`: `Audio.Initialize`, `AC97.Initialize`, and `ES1371.Initialize`). A bare icon launch does not initialize the device: `WavPlayerApplicationFactory` reuses or obtains `Desktop.EnsureWavPlayer`, adds the window, and makes it visible. The no-device check applies to document activation, not a bare open (`ApplicationFactories.cs:1469–1518`).

If created, `WAVPlayer` builds its window/icons, resets static playback fields, and installs the timer interrupt callback at vector `0x20` (`guideXOS\DefaultApps\WAVPlayer.cs:21–37`). Playback later calls `Audio.snd_write`. This path shares app-factory/window/framebuffer execution with Display Options and has its own global player/audio/timer state. No evidence connects that state to the static resolution-array load or establishes a shared unsafe instruction.

### Disk Manager

`DiskManagerApplicationFactory` creates `new DiskManager(400, 300)` and only then registers, raises, and shows the window (`guideXOS\OS\ApplicationFactories.cs:1286–1308`). The constructor builds status and immediately calls `RefreshDisks` (`guideXOS\DefaultApps\DiskManager.cs:53–61`). Refresh inspects `Disk.Instance`, reads the system disk MBR via `ReadMBRForEntry`, calls `USBStorage.GetAll`, may open USB mass-storage devices, and reads their partition information. This makes physical storage reads part of opening the window. Format/partition controls exist deeper in the app but are not invoked by construction; BM3A performed no disk I/O and made no disk changes. This path shares app/window/rendering and allocator code with Display Options, but adds disk-controller and USB storage operations, with no known use of `AvailableResolutions`.

### Far-right RAM/system-information area

The source candidate is the `PerformanceWidget` plus the right-edge `WidgetContainer`. `PerformanceWidget` is positioned at `Framebuffer.Width - 160`, y `80` (`guideXOS\DockableWidgets\PerformanceWidget.cs:27–30`); `Program` creates it and adds it to a container positioned at `Framebuffer.Width - 220` (`guideXOS\Program.cs:1455–1460, 1523–1528`). Desktop updates call `WidgetContainer.UpdateAutoHide`; the container tests `Control.MousePosition` against a right-edge threshold and vertical band, then reveals/hides widgets and processes hover/input (`guideXOS\GUI\Desktop.cs:628–632, 1206–1209`; `WidgetContainer.cs:158–190, 460–490`).

On draw/update, `PerformanceWidget` reads `ThreadPool.CPUUsage`, `Allocator.MemorySize`, and `Allocator.MemoryInUse` and formats/draws the values (`PerformanceWidget.cs:84–154`). The Monitor widget also reads the same CPU and allocator metrics. This path shares window/widget, font, framebuffer, allocator-statistic, and managed runtime dependencies with Display Options. No specific hit target or captured instruction establishes whether reveal, hover, metric update, or drawing caused the reported crash.

### Common dependency matrix

| Path | Shared GUI/rendering dependencies | Unique state or operation | Evidence of the Display Options unsafe operation? |
|---|---|---|---|
| Display Options | App Model/factory, `Window`, `WindowManager`, font/framebuffer, managed allocator | `DisplayManager.AvailableResolutions` static GC reference | Yes: exact `list.Length` dereference at `0x1006A5C5`. |
| Audio icon | App Model/factory, reusable `Window`, `WindowManager`, font/framebuffer, managed allocator | Static WAV player state, timer callback, audio driver/output | No. |
| Disk Manager | App Model/factory, `Window`, `WindowManager`, font/framebuffer, managed allocator | IDE and USB enumeration and media reads during construction | No. |
| `netinit` | Console window/output and managed runtime/allocator | Static network lists, PCI scan, NIC registers, DMA descriptors | No. |
| Right-edge widget | `WidgetContainer`/widget draw path, font/framebuffer, allocator statistics | Mouse-edge hit testing, CPU/RAM metric updates | No. |

All five execute in the same NativeAOT kernel and use common runtime services. That is too broad to establish one root cause. No source or instruction evidence supports Outcome C (shared mechanism) or Outcome D (separate hardware-specific defects) at this point.

## 8. Narrow BM3B plan and physical priorities

### One diagnostic addition

Capture the interrupted **RDI** for vector `0x0D` in the fixed BM1 diagnostic fault record or emit it to serial before the early halt, and display/retain it with the existing vector/error/RIP/CR2/CR3/RSP values. Include CR4 (at least LA57 state) in that record. The existing `RegistersStack` already saves RDI (`Kernel\Misc\IDT.cs:86–101`), and the normal serial breadcrumb routine prints `REG_RDI`; however, the `UEFI_DIAGNOSTIC_BM1` branch calls `BareMetalMilestones.RecordFault` and halts at `IDT.cs:1108–1115` before the normal breadcrumb path. The fixed record currently stores no GPRs. Therefore BM3B needs only a small record/output addition, not a new interrupt stub or allocator instrumentation.

Reproduce the known Display Options trigger once on AIDA_LPT with the exact candidate identified by ISO hash. Preserve vector/error/RIP, RDI, CR4.LA57, CR3, RSP, code bytes, BM milestone, and ISO SHA. If `RDI + 8` is noncanonical, trace the static slot/reference and its initialization or last writer before changing application behavior. If the effective address is canonical, verify the exact running image, exception frame, active canonical width, and mapping; a canonical unmapped access would be expected to fault as `#PF`. Do not recreate the array, sweep allocator memory, or dispose unrelated objects without evidence.

### Follow-up and regression order

1. **Ordinary desktop interaction:** reproduce far-right edge reveal, hover, and draw independently; capture the same exception/register record and exact action. This is the reported accidental-interaction priority.
2. **Display Options and Audio:** verify Display Options opens and draws with resolution labels, and opening does not change the display mode. Separately verify the bare Audio icon opens/draws and the timer/player path remains valid; record captures independently.
3. **Disk Manager:** open only for read-only enumeration, capture any failure, and avoid format/partition controls and all internal-disk writes.
4. **Network:** run `netinit` last with vector/register capture because it reaches PCI/NIC programming and DMA setup; preserve failures independently from GUI scenarios.

These are follow-up regression boundaries, not BM3A test results. Preserve the BM2 NotificationManager fresh-list correction and the accepted Task Manager ownership/lifetime corrections in any later fix. Do not start a full qualification cycle for this triage.

## 9. Closeout

- Starting and ending HEAD: `c838481ef0d1347b1b979741204ea3cff2a374b3`.
- Files changed: only `docs\UEFI_BARE_METAL_BM3A_CRASH_TRIAGE.md`.
- Tracked source/build outputs: unchanged. Existing BM1, BM2, and RC1 artifacts and the pre-existing `out\rt` worktree state were preserved.
- Builds, tests, ISO packaging: none.
- Commit/push: none.
- Physical retest: none; the next evidence needed is AIDA_LPT's RDI and active LA57 state at the same Display Options fault.
