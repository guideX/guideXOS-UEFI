# guideXOS C# UEFI BM1 Boot Investigation

**Status:** **Outcome B — diagnostic image qualified in OVMF; physical laptop
observations are pending.** No physical test has been performed.

## Outcome

The separately named BM1 diagnostic ISO passed the full copied-lane build and
artifact audit, booted through OVMF, displayed the desktop, advanced BM01–BM13,
and maintained the direct-framebuffer heartbeat. A separate QEMU-only
BM1FaultTest image displayed a red vector-14 fault screen with CR2 and CPU
context. No physical observations were supplied, so this does not localize the
HP or ASUS black screen and does not establish physical compatibility.

## Repository and release protection

- Repository: `D:\dev\guideXOSUEFI`; branch remains `main`.
- Starting HEAD: `1f0f8c22eaf3c074c46b40ed36018d2dfbf304aa`.
- Ending HEAD: the local BM1 diagnostic commit on `main`; its exact object ID is
  recorded in the completion message. The branch topology remained `main`.
- The accepted RC1 SHA supplied for this investigation is
  `2A330E0F883C13EA9BE0E9171D20297C3DEA966334E569F5E5E500E915A983E8`.
  No ISO in the repository has that hash. The existing
  `ISO\guideXOS.iso` hashes to
  `1FB747B254B1FC9329EF0BF76D72D8E3CE8290289ACA52BCE5FBD1391441FFFA` and has
  not been modified.
- Build lane: ordinary source copy at `C:\BM1`; the live repository's nested
  `out\rt` cache was not rebuilt. The `D:\BM1` copy attempt stopped when its
  volume ran out of space; the source tree and release artifacts were left
  intact. Phase 21–23 patch evidence was retained in the successful `C:\BM1`
  copy.
- No internal Windows disk or firmware boot record was accessed or changed.

## Boot-stage source map

The actual UEFI source order is:

1. `efi_main` locates GOP with `LocateProtocol` in
   [`main.cpp`](../guideXOSBootLoader/main.cpp#L582). It uses the firmware's
   current mode; this loader path contains no GOP `SetMode` or `QueryMode` call.
2. The loader draws an initial splash, loads the ELF kernel
   ([`main.cpp`](../guideXOSBootLoader/main.cpp#L639),
   [`elf.cpp`](../guideXOSBootLoader/elf.cpp#L100)), then loads the RAMDISK and
   fills BootInfo.
3. Before leaving Boot Services it allocates the stack and trampoline, builds
   the identity map, maps the kernel's linked virtual range, and maps the
   framebuffer uncached with PCD/PWT. It saves actual GOP pixel format and
   channel masks in the BM1 handoff.
4. It draws the final loader splash, prints `Boot splash complete`, and calls
   `ExitBootServicesWithMemoryMapInBuffer`.
5. After `ExitBootServices`, the loader uses direct memory/serial operations,
   fills the final memory-map and handoff fields, validates the jump target,
   then calls the trampoline. It does not call UEFI Boot Services after exit.
6. The trampoline switches to the supplied stack and CR3, then jumps to
   `KMainWrapper` with the Microsoft x64 ABI.
7. In BM1 mode, `KMainWrapper` installs a small assembly exception table before
   managed boot-info access. Kernel entry records BM01/BM02, checks BootInfo,
   initializes and reserves allocator ranges, and records BM03.
8. `StartupCodeHelpers.InitializeModules` initializes NativeAOT module state
   (BM04). The managed IDT is installed, and BM05 validates the GOP layout,
   the memory-map disposition, and every framebuffer page's identity mapping
   and writable/uncached flags. Complete descriptor coverage is recorded when
   present; a completely absent overlapping descriptor is recorded as
   `BM1M:NONE`, while partial coverage remains a hard stop. The GOP-provided
   range and each page mapping must still validate before kernel drawing.
9. Kernel graphics initialization and its screen clear are bracketed by
   BM06. `BootSplash.Initialize` is bracketed by BM07.
10. BM08 substage markers bracket interrupt-table setup, ACPI, APIC/PIC,
    timer/input, and PCI setup. In the BM1 build only, AHCI/SATA probing is
    skipped so it cannot issue ATA IDENTIFY or block I/O to an internal drive;
    BM08.9 records this bypass and BM09.0 records that startup continued.
    BM09 brackets thread-pool startup,
    interrupt enable, RAMDISK/filesystem setup, splash cleanup, and kernel/GUI
    entry. This narrows the initialization window that precedes the desktop.
11. UEFI desktop startup initializes `WindowManager` and `Desktop` (BM10), then
    calls `Framebuffer.RecoverUefiState`/`EnsureGraphics`, clears the desktop
    background, and switches to its back buffer (BM11). This is a software
    render-target transition; the post-EBS kernel cannot change GOP mode.
12. Widget state is recorded as BM10 substages. The first completed desktop
    frame records BM12; the next completed frame starts BM13's direct
    framebuffer heartbeat.

Relevant implementations are [`EntryPoint.cs`](../Kernel/Misc/EntryPoint.cs),
[`Framebuffer.cs`](../Kernel/Drivers/Framebuffer.cs),
[`BootSplash.cs`](../Kernel/Misc/BootSplash.cs),
[`Program.cs`](../guideXOS/Program.cs),
[`WindowManager.cs`](../guideXOS/GUI/WindowManager.cs), and
[`Desktop.cs`](../guideXOS/GUI/Desktop.cs).

The splash-to-desktop transition has destructive clears in
`Framebuffer.Initialize` and `SMainSetupUefi`. BM06 and BM11 record immediately
before and after the kernel framebuffer clear and desktop clear/back-buffer
transition respectively. The fixed handoff record retains the last stage if
later drawing erases its visible badge. Before BM05 proves that
framebuffer access is safe, BM01–BM04 are recorded in the fixed handoff record
and sent as allocation-free serial markers; they are not drawn speculatively.
The OVMF map did not describe the GOP MMIO range; the diagnostic records that
absence and proceeds only after the range bounds and all 1000 framebuffer page
mappings validate. The kernel then visibly updates the framebuffer through
BM13.

## BM1 markers and faults

The fixed stages are:

| ID | Placement and meaning |
|---|---|
| BM01 | Kernel entry reached; assembly emergency exception table is active. |
| BM02 | BootInfo header, checksum, map fields, and ABI sizes validated. |
| BM03 | Early allocator setup and boot-owned range reservations completed. |
| BM04 | NativeAOT modules initialized. |
| BM05 | GOP layout, descriptor coverage/disposition, and framebuffer page mappings validated. |
| BM06 | Kernel framebuffer wrapper initialized; screen clear bracketed. |
| BM07 | Kernel splash initialization bracketed. |
| BM08 | Kernel hardware initialization: 0/1 interrupt setup; 2/3 ACPI; 4/5 APIC/PIC and timer entry; 6 input/serial complete; 7/8 PCI entry/completion; 9 AHCI/SATA path entered (device probing is bypassed in BM1). |
| BM09 | Kernel runtime path: 0 SATA path returned/thread-pool entry; 1 thread-pool complete; 2 interrupts enabled; 3/4/5 RAMDISK/filesystem entry/mounted/unavailable; 6 splash cleanup; 7 kernel main; 8 GUI entry. |
| BM10 | Desktop setup: 0/1 WindowManager entry/return; 9 its caught failure; 2/3/4 Desktop entry/success/failure; 5 widgets visible; 6 none/unavailable; 7 widgets present but hidden. |
| BM11 | Desktop graphics recovery, clear, and back-buffer transition substages 0–3. |
| BM12 | First completed desktop draw and framebuffer update. |
| BM13 | Completed desktop frame heartbeat; green/yellow pulse changes every 32 frames. |

The direct renderer uses bounded rectangles, actual GOP byte pitch,
GOP pixel-format/channel masks, and hand-coded fixed glyphs. It performs no
per-stage allocation and does not depend on fonts or widgets. Before drawing,
it checks base, size, dimensions, pitch, format/masks, memory-map descriptor
coverage or absence, and the CR3 walk for every framebuffer page. A red fault
screen reports vector, error code, RIP, CR2, CR3, RSP, RFLAGS, and last stage.
The early assembly handler stores the same CPU context in a fixed record and
writes a serial line before halting. The managed handler records and halts as
well; no CPU exception is suppressed.

The BM1-only `BM1FaultTest` build intentionally reads an unmapped canonical
address after BM05. It is for an isolated QEMU guest only and must not be used
on the laptops.

## QEMU and artifact qualification

**Build command:** `C:\BM1\build.ps1 -UefiDiagnosticMode BM1`.

**Full-build result:** Final copied-lane build completed with exit code 0,
including loader, NativeAOT kernel, ELF conversion, RAMDISK, proof staging, and
ESP assembly. The final corrected-source log is
`C:\BM1\out\bm1-full-build-final-guardfix.log`. The earlier first full
attempt reached the kernel compile and failed because this repository's
custom Corlib does not expose numeric `MaxValue` fields; the BM1 bounds were
changed to fixed-width constants. The subsequent complete build of that source
passed. Phase 27–35 proof staging completed, including all nine Phase 35
artifact validations: success, fail-fast, stale-owner, cross-scope, malformed,
no-read, one-read, two-read, and not-found.

**Artifact admission/audit:** Passed. The generated CSV at
`C:\BM1\out\h2c-managed-artifact-audit.csv` reports:

- `MANAGED_ARTIFACTS=41`
- `DESCRIPTOR_AGREEMENT=41/41`
- `BUILD_STAGING_AGREEMENT=41/41`
- `RAMDISK_STAGING_AGREEMENT=41/41`
- `COMPILED_ALLOWLIST_AGREEMENT=41/41`

The final image was packaged from the staged ESP, then independently extracted.
The ISO root files and the files inside its El Torito FAT16 image matched the
staged EFI, kernel, and RAMDISK hashes byte-for-byte; the UEFI loader is at
`EFI\BOOT\BOOTX64.EFI` inside the FAT16 image. Its El Torito validation
catalog at LBA 36 advertises EFI platform `0xEF`, has checksum sum `0`,
validation signature `0xAA55`, and a bootable no-emulation entry.

The first packaging trial exposed two diagnostic-image-only defects: the
catalog advertised legacy platform `0x00`, and two FAT 8.3 names contained a
literal dot in the fixed-width name field. Both were corrected and rechecked
before the qualified image below was booted.

**Diagnostic ISO:**
`C:\BM1\out\guideXOS-CSharp-UEFI-v0.2-BM1-Diagnostic-Final.iso`.

| Payload | SHA-256 |
|---|---|
| `kernel.elf` | `65B3A71B4C71E2865FB668CDFAD56E713ED8944380062E86F3083A601439F0D6` |
| `ramdisk.img` | `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8` |
| `EFI\BOOT\BOOTX64.EFI` | `6363E453C87A2900005D7C25E3D2B54D9B43FC0EDA531A1F10BA7AFA6C960059` |
| ISO | `5C781CA5627D90DAE73DBA7A9716864381F8DBBA3675DA08C02BEF93FF287253` |

**OVMF/QEMU result:** Passed with QEMU 11.0.0, TCG, Q35, 2 GiB RAM, the
installed x86-64 OVMF code/vars pair, and the actual diagnostic ISO attached as
read-only CD-ROM. A separate vars copy was used for this final run. The serial
log is `C:\BM1\out\bm1-qemu-serial-final-image.log`; the captured desktop is
`C:\BM1\out\bm1-qemu-desktop-final-image.bmp` (PPM monitor capture converted
without resampling). BM01 through BM13 advanced in recorded source order;
`SMAIN_DISPATCH_REASON=UEFI_CONTINUOUS_DESKTOP`, desktop text and icon checks
passed, BM12 completed, BM13 heartbeat toggled, and frame 60 reported
`CONTINUOUS_HEARTBEAT_GRAPHICS_VALID=1`. Allocator free-invalid, no-pages, and
corrupt-run counters remained zero.

The OVMF GOP record was base `0x80000000`, size `0x003E8000` bytes, 1280×800,
byte pitch `0x1400`, pixel-format ID 1 (B8G8R8A8), 1000 mapped pages, and
effective PTE flags `0x1A` (writable plus write-through/cache-disable). OVMF's
memory map had no descriptor overlapping that GOP MMIO range (`BM1M:NONE`,
descriptor type `0xFFFFFFFF`, count 0). The diagnostic records this separately
from full descriptor coverage; it rejects partial coverage and still requires
the GOP bounds and every identity-mapped framebuffer page to validate. Direct
kernel framebuffer writes then advanced through the desktop heartbeat. This
proves the tested OVMF mapping path, not a general physical-laptop mapping rule.

A first OVMF trial using the earlier strict diagnostic stopped intentionally at
BM05 with descriptor failure code 4 and showed the unchanged loader splash.
That was a false rejection by the diagnostic's “must have a covering memory-map
descriptor” assumption, not an observed kernel exception or display failure.
The no-overlap disposition was then added and the final ISO passed the complete
boot.

**Controlled exception test:** A separate QEMU-only image was built with
`C:\BM1\build.ps1 -SkipBootloader -SkipRamdisk -UefiDiagnosticMode BM1FaultTest`
and packaged as
`C:\BM1\out\guideXOS-CSharp-UEFI-v0.2-BM1-FaultTest-Final.iso` (SHA-256
`5D61AE2B143199C5DABBAD3CC98F6433A8554BF7837A02CD521391DE5059F6E7`; its
kernel SHA-256 is
`18467AECA83D20ACE7118814397A33D732E03B2CB534ABA7B423FDE9C90387CF`). It
triggered the intentional unmapped read only after BM05 in an isolated QEMU
guest. The managed handler reported vector `0x0E`, error `0`, RIP
`0x10001922`, CR2 `0x700000000000`, CR3 `0x7DD22000`, RSP `0x27FF10`, and RFLAGS
`0x10006`, then halted. The serial record is
`C:\BM1\out\bm1-qemu-serial-final-fault.log`; the red fault screen is
`C:\BM1\out\bm1-qemu-fault-final.bmp`. This image is not for physical
hardware.

BM1 is opt-in through the build property and preprocessor guards. After fixing
the conditional handoff-struct boundary, the default-mode copied-lane build
(`C:\BM1\build.ps1 -SkipRamdisk`) completed with exit code 0, compiling the
normal loader and kernel while reusing the already staged RAMDISK. A separately
named normal-mode QA ISO at
`C:\BM1\out\guideXOS-CSharp-UEFI-v0.2-BM1-NormalMode-QA.iso` (SHA-256
`8B946B1F92A44DFD19FA7107972A86BF16FD1E267FFCBB1570BEBA9EAEBA36D5`) booted
through OVMF into `UEFI_CONTINUOUS_DESKTOP`, rendered the desktop, and reported
a healthy frame-60 heartbeat with zero allocator fault counters. Its serial
log and desktop capture are
`C:\BM1\out\bm1-qemu-serial-normal-qa.log` and
`C:\BM1\out\bm1-qemu-desktop-normal-qa.bmp`. The default NASM object contains
no BM1 entry, stage, or fault-trigger symbols. The accepted RC1 ISO was not
rebuilt or booted in this task; its original hash remains untouched.

Physical GOP resolutions, pixel formats, memory maps, ACPI tables, and device
initialization can differ from OVMF. Passing QEMU can qualify the diagnostic
image and instrumentation only; it cannot establish laptop compatibility.
BM1 deliberately skips AHCI/SATA device probing to avoid ATA IDENTIFY or block
I/O to internal Windows disks. Therefore this physical experiment does not
qualify the SATA initialization path.

## Owner physical test checklist

Run this only with the normal `BM1-Diagnostic.iso` after verifying its hash.
Do not use the separate `BM1-FaultTest.iso` on a laptop.

1. Verify the diagnostic ISO SHA-256 below before preparing a USB. Use the same
   USB preparation tool, mode, and safe boot procedure used for RC1. Select the
   USB explicitly and verify its model/capacity before writing; writing replaces
   the selected USB's contents. Enable that tool's read-back verification if it
   offers one.
2. Confirm the USB contains this diagnostic build using the tool's successful
   verification or, when the USB exposes a filesystem, compare the three
   payload hashes below. Do not select, mount, read, or write an internal
   Windows disk. BM1 skips AHCI/SATA probing and uses its RAMDISK for files.
3. Use the laptop's one-time UEFI boot menu to select the USB. Do not start an
   installer, change firmware settings, or update firmware.
4. Expect colored numbered badges BM01–BM13 at the top after framebuffer
   validation. The badge count can briefly reflect the source order around
   BM10/BM11; use the last fully drawn badge, not the numeric maximum alone.
   Photograph the last badge and heartbeat corner. Note the seconds from
   `Boot splash complete` to the last visible change.
5. If the screen becomes fully black, use a phone to photograph the panel and
   bezel. Record whether the panel is backlit, whether fans/keyboard lights
   remain on, if the laptop restarts or powers off, and how long it stays that
   way. Do not infer a framebuffer defect from black alone.
6. Report laptop model, last visible badge/substage, whether BM13 heartbeat
   movement was seen, any displayed fault values, elapsed time, and power or
   restart behavior. Never test the artificial fault on physical hardware.

**ISO path:** `C:\BM1\out\guideXOS-CSharp-UEFI-v0.2-BM1-Diagnostic-Final.iso`

**ISO SHA-256:**
`5C781CA5627D90DAE73DBA7A9716864381F8DBBA3675DA08C02BEF93FF287253`

**Payload SHA-256 values:**

- `EFI\BOOT\BOOTX64.EFI`: `6363E453C87A2900005D7C25E3D2B54D9B43FC0EDA531A1F10BA7AFA6C960059`
- `kernel.elf`: `65B3A71B4C71E2865FB668CDFAD56E713ED8944380062E86F3083A601439F0D6`
- `ramdisk.img`: `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8`

## Physical observations, cause, and next step

The initial report documents both laptops accepting UEFI USB boot and starting
ELF/RAMDISK loading; photographs show the splash and at least one
`Boot splash complete`, followed by a persistent black screen. This turn did
not run BM1 on physical hardware, so the exact last execution stage, laptop
fault vector/RIP, panel power state, and restart behavior remain unknown. The HP
candidate and ASUS VivoBook failures are unlocalized, and root cause is not
established. The only localized stop was the diagnostic's first OVMF BM05
descriptor-coverage false rejection, corrected before the final QEMU pass; it
does not explain the reported physical black screen.

After the owner returns the last visible marker (and power/restart behavior),
use that evidence to choose the smallest follow-up: handoff/BootInfo repair
before BM01–BM03, allocator/runtime repair between BM03–BM04, descriptor or
mapping/layout investigation at BM05, graphics wrapper or back-buffer
transition after BM06/BM11, or the subsystem indicated by BM08–BM13. A boot
that succeeds only in BM1 does not prove an AHCI defect because BM1 skips SATA
probing. Any SATA follow-up must use an empty or disposable device and must not
probe an internal Windows disk. Keep BM1 instrumentation out of the release
build. The accepted RC1 ISO, normal firmware boot records, and internal disks
remain unmodified.
