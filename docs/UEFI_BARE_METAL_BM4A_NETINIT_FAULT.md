# BM4A `netinit` Fault Investigation and Qualification

- **Date:** 2026-10-26
- **Repository:** `D:\dev\guideXOSUEFI`
- **Branch:** `main`
- **Outcome:** **C — safe refusal when the BAR cannot be safely mapped; AIDA_LPT validation pending**

## 1. Starting Git and workspace state

- Start and end HEAD: `829f8d565c10ad4d4aacfa96f8d49140dc768ba3`.
- Branch: `main`, tracking `origin/main`; start divergence was `0 0`.
- No branch switch, worktree, stash, reset, clean, rebase, cherry-pick, history edit, or push was performed. No commit was created.
- The pre-existing BM3C tracked changes were retained: `Corlib/Internal/Runtime/CompilerHelpers/StartupCodeHelpers.cs`, `Kernel/Drivers/Display.cs`, `Kernel/Misc/BareMetalMilestones.cs`, `Kernel/Misc/EntryPoint.cs`, `guideXOS/GUI/DisplayOptions.cs`, `guideXOS/Kernel/Misc/UefiBootInfo.cs`, and `guideXOSBootLoader/paging.h`. `EntryPoint.cs` now also contains BM4A memory-map handoff changes; its earlier BM3C static-base work was preserved in the build and source tree.
- Pre-existing untracked BM2/BM3B/BM3C/RC1 ISO files and BM2/BM3A/BM3C reports were left intact. The BM4A ISO and report are new. Focused regression sources are new.
- The nested `out\rt` remained a dirty detached runtime checkout at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, with more than 50 pre-existing changed/untracked entries. It was not modified. Build and packaging ran in the contained ordinary-copy lane `E:\BM4A-qualification\candidate`.
- The BM2 NotificationManager boot reset remains in `guideXOS/Program.cs` (`NotificationManager.ResetForKernelBoot()`); that file was not changed by BM4A.

## 2. Exact BM3C binary and symbolization

The physical BM3C diagnostic ISO was verified at:

- ISO: `D:\dev\guideXOSUEFI\ISO\guideXOS-CSharp-UEFI-v0.2-BM3C-Diagnostic.iso`
- ISO SHA-256: `6999A58EBF627BA65DC343A5D04FF1D099E6950A8266E50DD330212C04CEDFFE`
- Root and embedded-ESP `kernel.elf` SHA-256: `A1296D932444342058B87501133461791C37115A0147F629C4AB925B2DE31F47`
- `BOOTX64.EFI` SHA-256: `BC988B6705E979614CF25C8083A61BC9D3A5A209282E910B226CA354DAC7EFDB`
- `ramdisk.img` SHA-256: `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8`

The stripped physical ELF did not carry a useful source map. I generated a map from a contained build of the BM3C source state at `E:\BM4A-qualification\baseline\guideXOS\Kernel.map` (SHA-256 `5E17D1C433D12138DD94491ECB2DEF8ACE75408342AAF0AC60C614FA138F1FCD`). I compared the generated executable bytes against the exact kernel extracted from the BM3C ISO: the relevant code region covering `Intel825xx.Initialize` and `Intel825xx.Reset` matched byte-for-byte. The map places `Intel825xx.Initialize` at `0x10012770` and `Intel825xx.Reset` at `0x100130C0`; it is correlated by executable bytes, not assumed to be a map from an unrelated newer kernel.

At RIP `0x100130D9`:

- Bytes: `C7 02 00 00 00 04`
- Instruction: `mov dword ptr [rdx], 0x04000000`
- Containing function: `Intel825xx.Reset`, offset `+0x19`.
- Width: 4-byte write. The effective address has no displacement, so `RDX` equals the faulting address `0xE1200000` at this instruction. The captured `RDI=0x1047D958` is not used by this instruction.
- Immediate caller: `Intel825xx.Initialize`, which calls `Reset()` before further register setup.
- Recoverable BM3C source locations: `Kernel/Network/Intel825xx.cs` in the BM3C tree: BAR-to-`IOBase` assignment at line 161, `Reset()` call at line 163, `Reset` at lines 214-217, and `WriteRegister`'s pointer write at lines 282-283. `Kernel/Misc/MMIO.cs` implements `Out32(uint* p, uint value)` as `*p = value`.

## 3. Physical exception and proven fault mechanism

| Field | BM3C AIDA_LPT capture |
|---|---:|
| Vector | `0x0E` page fault |
| Error code | `0x02` — supervisor write to a non-present page |
| RIP | `0x100130D9` |
| CR2 | `0x00000000E1200000` |
| CR3 | `0x00000000AAB03000` |
| CR4 | `0x0000000000000668` |
| LA57 | `0` |
| RDI | `0x000000001047D958` |

The actual `netinit` route is `FConsole.HandleCommand` (`guideXOS/DefaultApps/FConsole.cs`, `netinit` case at line 875) → `NETv4.Initialize()` (resets the sender and network state) → `Intel825xx.Initialize()` → `Reset()` → `WriteRegister(0, 1 << 26)` → `MMIO.Out32` pointer store. PCI enumeration happens during UEFI startup in `EntryPoint.KMain` through `PCI.Initialize()` / `PCI.CheckBus()`; `netinit` scans the resulting `PCI.Devices` list. If Intel initialization does not set a sender, the command tries RTL8111 and then prints `[NET] No supported NIC found`.

In the BM3C code, `Intel825xx.Initialize` chose an Intel device from the PCI list, assigned `IOBase = (uint)(device.Bar0 & ~0xF)`, and immediately used that physical BAR value as a pointer. It did not distinguish an I/O BAR from a memory BAR, decode a 64-bit BAR pair, size or range-check the resource, or install a page-table mapping. `Reset` wrote register offset zero, explaining why CR2 equals the BAR-derived base with no additional offset.

**Proven immediate cause:** the driver dereferenced an unchecked PCI BAR-derived address as a linear pointer. The active page tables did not have a present writable mapping for `0xE1200000`, as directly evidenced by the non-present page-fault error. The best-supported root cause is missing MMIO mapping/validation before register access. The physical PCI configuration-space dump is unavailable, so AIDA_LPT's exact vendor/device ID, BAR flags, BAR size, and memory-map descriptor coverage cannot be independently reconstructed from the exception screen alone. The patch also rejects a malformed or I/O BAR rather than assuming the BAR is valid MMIO.

## 4. PCI, BAR, and page-mapping findings

- The BM3C loader creates bounded identity mappings for the kernel's explicit boot/runtime ranges and applies uncached mappings for GOP framebuffer, APIC/IOAPIC, and firmware-described PCI ECAM. It does not map arbitrary discovered endpoint BARs. The NIC BAR was not mapped by the old driver before its first register store.
- The existing production `PageTable.MapMmioRange` is reused; no second paging implementation or global arbitrary PCI identity map was added. It maps only pages covering the validated register span into the current CR3 as writable, uncached (`PWT`/`PCD`), non-executable identity mappings, invalidates each translation, and rejects a present PTE that points at a different physical page. The mapping persists in the kernel page tables for the static driver's lifetime; there is no per-device unmap token in this existing API.
- `EntryPoint` validates and reserves the retained UEFI memory-map buffer after ExitBootServices, then passes its address, entry count, and descriptor stride to PCI code.
- The new BAR path checks standard header type, segment/class, memory-vs-I/O BAR flags, reserved memory types, 32-bit vs 64-bit BAR pairing, decoded base, size mask, power-of-two/alignment/range/canonical bounds, and that the full BAR aperture is covered by an EFI `EfiMemoryMappedIO` (type 11) descriptor. BAR sizing is performed with PCI I/O, memory, and bus-master decode disabled and the original BAR is restored. Only after validation and page-table mapping does it enable memory decode and bus mastering; I/O decode remains disabled. A failed validation leaves those three command bits clear.
- The Intel driver is selected only for its existing allowlist (93 vendor/device pairs) and Ethernet class/subclass `02/00`. A matched device must pass the bounded BAR map before `Reset` or any device-register pointer access. The RTL8111 path remains a separate fallback and was not broadened.
- `0xE1200000` is therefore a BAR0-derived candidate physical base in the BM3C driver path, not evidence of a kernel virtual allocation. The exception proves that the same numeric linear address was absent in the active page tables. The source proves the old driver treated BAR0 as that pointer; the laptop's actual BAR config words remain unavailable.

## 5. Focused repair and regressions

The repair establishes the resource contract at the earliest driver boundary: validate BAR0 and its aperture against the retained EFI MMIO map, map the limited register span through the existing page-table API, and only then enable decoding and touch registers. If validation or mapping fails, the controller remains disabled and initialization returns cleanly. No DMA subsystem, general network feature, audio code, or nested runtime change was introduced.

New focused regression project: `Tests/PciMmioResourceRegression`.

- Result: **29/29 passed** (`dotnet run --project Tests/PciMmioResourceRegression/PciMmioResourceRegression.csproj -c Release -p:UseSharedCompilation=false`).
- Synthetic fixtures cover supported/unsupported IDs and class, valid 32-bit and 64-bit BAR decoding, BAR size and alignment, invalid/missing/I/O/reserved BAR rejection, overflow/canonical bounds, page-span containment, EFI descriptor type/stride/count/range checks, and mapped-resource authorization.
- Fixture IDs/ranges are test data only; they are not presented as AIDA_LPT hardware evidence.
- Repeated initialization and guest failure handling were exercised in QEMU below. The positive hardware register path could not be exercised there because that OVMF map had no type-11 descriptor for its E1000 BAR.

## 6. Build, artifact admission, and BM4A image identity

The candidate was built in the contained copy lane, retaining the BM3C static-base changes. The existing diagnostic build setting `UefiDiagnosticMode=BM1` was retained for the storage-safe diagnostic path. Managed artifact admission passed: 41/41 manifest descriptors, build-stage entries, RAMDISK-stage entries, and compiled allowlist entries agreed.

Final image:

- `D:\dev\guideXOSUEFI\ISO\guideXOS-CSharp-UEFI-v0.2-BM4A-Diagnostic.iso`
- ISO SHA-256: `DA027057EC2ABA899BF5B69593A926CFEC34D006FC53D02FC3E136F751C31E2B`
- Embedded kernel SHA-256 (root ISO and FAT ESP): `CA2A65C353A7728D762A2FF1E2B37B6B4F3B855FB82C160048B42E84D93342F7`
- Kernel map SHA-256: `205B9F1128CE365A51761D2C2A9FECF7C609C6361410809D4A531035C8CD0E73`
- EFI loader SHA-256 (root and both ESP paths): `BC988B6705E979614CF25C8083A61BC9D3A5A209282E910B226CA354DAC7EFDB`
- RAMDISK SHA-256 (root ISO and FAT ESP): `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8`

The ISO passed `7z t`; its embedded FAT ESP was extracted and verified, and the kernel, RAMDISK, and EFI loader hashes matched the ISO-root copies. The image copied into the repository has the same SHA-256 as the candidate used for OVMF qualification. No previously qualified ISO was overwritten.

## 7. QEMU qualification

The exact candidate ISO was cold-booted under OVMF (`D:\dev\guideXOSServerV0.5_DEVELOPER_STUDIO\OVMF.fd`) with an explicit QEMU `e1000` device (`8086:100E`, class `02/00`) and 4 GiB guest RAM. This is an emulated fixture, not the AIDA_LPT controller.

- The E1000 appeared at `00:03.0`; BAR0 was `0xC1040000`, a 32-bit memory BAR. A sizing probe returned mask `0xFFFE0000`, a 128 KiB aperture; the original BAR value was restored.
- QEMU's retained EFI map had 105 descriptors of size 48 bytes, no type-11 descriptor, and no descriptor covering `0xC1040000` through `0xC105FFFF`. The BAR therefore failed the mapping authorization check before any register dereference. After `netinit`, PCI command register was `0x0000` (I/O, memory, and bus-master disabled).
- `netinit` reported the Intel BAR as invalid/unmapped, tried the RTL8111 fallback, then reported `[NET] No supported NIC found`. It was invoked twice; both attempts returned without a page fault or other exception. No DHCP or working network connection is claimed.
- The guest reached the initialized desktop, launched Console, and remained responsive after both attempts. Serial diagnostics reached desktop heartbeat frame 1800 with `GRAPHICS_VALID=1`. The session was exited through QMP and QEMU terminated cleanly.
- This qualifies safe rejection for the QEMU firmware/device mapping presented. It does not demonstrate the positive MMIO mapping path or prove AIDA_LPT's BAR/map layout.

## 8. Existing desktop and BM3C regression checks

On the final candidate ISO under OVMF:

- Desktop initialized and remained responsive; Start menu and All Programs opened.
- Console launched and accepted `netinit`.
- Display Options opened with all 11 resolutions, closed, and reopened.
- Disk Manager launched; the emulator reported disk size unavailable. No disk operation was performed.
- Task Manager's Memory Details view opened and displayed allocator/system information (including `Leak Exists FALSE` and zero invalid/no-page failures in the shown metrics).
- No startup or UI exception was observed. The BM2 `NotificationManager.ResetForKernelBoot()` startup correction remains in source and the boot completed.
- These checks also exercised the retained BM3C NativeAOT static-base correction. They do not replace physical AIDA_LPT regression testing.

## 9. Bounded Audio icon audit

The icon is registered as **WAV Player**, app ID `gxos.builtin.wavplayer`, dispatch/launch name `WAV Player`, and icon `Icons.AudioIcon(32)`. Depending on the selected icon theme, the resource is `icons/vlc.svg` or `Images/BlueVelvet/32/music.png`. `AppModel` has a built-in descriptor, `ApplicationFactories` registers `WavPlayerApplicationFactory`, and `guideXOS/DefaultApps/WAVPlayer.cs` provides the application. This is not a stale icon or missing target.

The factory has an explicit no-audio-device failure for document playback; a bare app launch can still create the player window. **Classification:** valid application with an owner-reported, not independently reproduced crash; exact crash cause remains pending a separate focused capture. No audio files or code were changed.

## 10. Physical acceptance, remaining blockers, and next step

**Physical AIDA_LPT status: pending owner test.** No physical PCI ID/BAR dump or post-repair laptop result is available. Network functionality is not accepted based on the QEMU safe return.

Owner retest with the BM4A diagnostic ISO:

1. Prepare USB from the embedded ESP image using the established FAT32 process; verify the copied boot files against the ESP contents.
2. Boot AIDA_LPT and confirm desktop readiness.
3. Open Console and run `netinit` once; capture all console output.
4. Confirm either working initialization or an explicit clean failure, then check that the desktop remains responsive.
5. Repeat once if the first attempt returns cleanly.
6. If an exception occurs, capture the diagnostic screen and exact vector, RIP, error code, and CR2. Do not perform destructive disk operations or change the installed Windows configuration.

Remaining v0.2 blockers are physical confirmation of AIDA_LPT's PCI ID/BAR/map coverage, a safe successful register-map path on that hardware (or a documented clean rejection if its mapping cannot be authorized), and actual network behavior if the supported controller initializes. The Audio crash is separate and remains untriaged.

**Recommended smallest next step:** run the focused owner retest and capture AIDA_LPT PCI identity plus `netinit` console/exception evidence. If the BAR is rejected cleanly, retain that result as safe-failure evidence; if mapping succeeds, verify desktop responsiveness and a real network result before claiming network acceptance.

## 11. Local commit status

No commit was made. BM3C and BM4A edits share `Kernel/Misc/EntryPoint.cs`, and the root contains pre-existing BM3C work that remains user-owned and uncommitted. The current mixed diff has been preserved for review rather than separated by rewriting or staging unrelated changes. Nothing was pushed.
