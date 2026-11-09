# guideXOS C# UEFI v0.2 RC3Q Guest and Security Closeout

**Date:** 2026-11-09
**Outcome:** `RC3_RELEASE_DEPENDENCY_BLOCKED` (E)
**Publication:** Not authorized. No push or publication occurred.

## 1. Disposition

The exact frozen RC3 ISO was admitted through QAC and completed three independent cold boots to the UEFI desktop. The v0.2 teal splash was captured and matched its expected gradient endpoints; no BM/AIDA diagnostic overlay appeared. Required application checks completed, including a 2 + 3 = 5 calculator interaction, Display Options close/reopen, read-only Disk Manager inspection, Task Manager Memory Details, safe netinit with -net none, WAV Player construction without playback, and window teardown. The first two runs each exceeded a 15-minute interactive soak with continuing heartbeat and no kernel fault, allocator corruption, or dropped input event.

A warm reset completed under QEMU TCG: OVMF booted the exact DVD again, the desktop initialized a second time, and heartbeat resumed. Two reset attempts under WHPX (one QMP system_reset, one guest Console reboot) instead ended QEMU with exit code 0 and WHPX: Unexpected VP exit code 4; neither produced a second desktop. No kernel fault was logged. The WHPX reset limitation is recorded in §4.

The production NativeAOT app cohort was built with affected-version Microsoft.DotNet.ILCompiler package identity 9.0.0. Source and available payload/map review found no Brotli decoder call/reference or demonstrated attacker-controlled path, but the final AOT link closure is incomplete. The advisory is not formally cleared for an owner publication decision. No affected reachable decoder or exploitability was demonstrated; the remaining release dependency is the exact-artifact link-closure evidence in §5.

**Disposition:** RC3_RELEASE_DEPENDENCY_BLOCKED (E), because the security link-closure dependency remains unresolved. Publication is not authorized, and physical AIDA_LPT acceptance remains pending.

The Phase 23 link failure belongs to the historical standalone proof rebuild. The two omitted Phase 24 legacy files are outside the 41-item release registry and are not an unresolved production dependency.

## 2. Preservation, source, and QAC census

### Source and worktree

- Host: LEON2_RDT; authoritative checkout: D:\dev\guideXOSUEFI.
- Production source base at preflight: 652ad93932528a02861686d444257499bcd95a3d, equal to origin/main. The prior report-only commit was e700f89b5996d9d5109312162564b3268d862320; at the start of this continuation, main was ahead of origin/main by one commit and behind by zero.
- Tracked product source and index were clean before the report update. Existing untracked RC1/RC2/RC3 and AIDA/BM diagnostic ISOs, PCI regression bin/ and obj/, and docs/UEFI_AIDA3_BLUE_SCREEN_BOOT_LOCALIZATION.md were preserved. Qualification evidence is under the ignored out/rc3q_guest_qualification_20261109/ directory.
- The nested out/rt checkout remained at 9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3 with its pre-existing NativeAOT source edits and generated/test-file deletions. MODERN_INPUT, APP_MODEL, FONT_SUPPORT, and BUILD_INFRA work was preserved.
- No product source, NativeAOT runtime, build output, or ISO was changed or rebuilt. No Git reset, clean, stash, branch switch, merge, cherry-pick, rebase, push, or publication occurred.

### QAC ownership and cleanup

QAC CLI status, list, and queue were used. Before the accepted guest runs, QAC was online (v2) with no managed or foreign QEMU, no reservation, and no queued request. All guest processes were launched as direct children through QAC mode acceptance; the ISO was read-only, and QEMU network mode was -net none.

Two initial QAC-managed launch attempts ended before guest boot and do not count as cold boots: reservation `ecc4304661684ae1a288047cbbd5c0c1` (SDL, child PID 21416) failed with process exit `0xC0000005`, and reservation `56793db0f2f64f04948444357285bcbe` (VNC port-number syntax error) exited before firmware. Both reservations were released. They did not modify another project's reservation or start an unmanaged emulator.

| Reservation | Child PID | QEMU accelerator | QEMU start–exit (UTC) | Result |
| --- | ---: | --- | --- | --- |
| 98b209cced774fcc80a16d822482e376 | 18444 | WHPX | 12:53:12–13:14:08 | Cold boot reached desktop; host QMP reset later exited with WHPX VP exit code 4; released |
| 1e72d9601d51415e994ed608212b31fc | 14188 | WHPX | 13:16:24–13:32:58 | Cold boot and interactive soak; guest Console reboot later exited with WHPX VP exit code 4; released |
| e6471ae8b72042c097f5ad858de06918 | 22132 | TCG | 13:34:53–13:37:57 | Cold boot and warm reset to second desktop; QMP quit exit 0; released |

The first run used loopback QMP port 61322. The second used loopback QMP 61323 and VNC 127.0.0.1:5901; the third used loopback QMP 61324 and VNC 127.0.0.1:5902. VNC remained bound to loopback. The third cold boot began paused with `-S` and continued through QMP. The splash screenshot was captured during its subsequent TCG warm reset; see §4.

After the third child exited, QAC reported 0 QEMU processes (0 foreign), 0 managed sessions, 0 reservations, 0 queued, and 0 blocked. All three accepted QAC reservations were released. The two WHPX exit messages occurred at reset; QEMU exited with code 0 and the guest serial logs contain no kernel fault.
## 3. Frozen ISO and payload identity

The exact expected file was checked in place; no cached candidate was substituted.

| Artifact | Bytes | SHA-256 | Result |
| --- | ---: | --- | --- |
| `ISO/guideXOS-CSharp-UEFI-v0.2-RC3.iso` | 195,741,696 | `530F246B84B7D4F1200CAD22654A48B8A536D983007EEC1C0D52D4F9B48BDE8C` | Matches expected |
| Embedded `EFIESP.IMG` | 134,217,728 | `2ED8455AF4B2F170FEE52391373694EA0E6B16F8029DDC1DCF63245701B814A3` | Matches record |
| ISO-root `BOOTX64.EFI` and `EFI/BOOT/BOOTX64.EFI` | 50,688 each | `3532556FFFF783BE5F3B715FE17F7685C8520C74912260994356764E58D5BB4E` | Equal |
| ISO-root `kernel.elf` | 4,739,072 | `71F31F435112FD1F5CA932CBAAE537540EC0EA8C87336C8C2A414353630C5DE1` | Matches record |
| ISO-root `ramdisk.img` | 56,297,702 | `0805FA9A825124630764FC05E3116AD03A0FFCEFCB24B216862BC3880E10764F` | Matches record |

The EFI loader, kernel, and RAMDISK extracted from the embedded FAT ESP have the same hashes as their ISO-root copies. The ESP volume label is `GUIDEXOSRC3`. The ISO archive check passed (7-Zip exit 0); the El Torito validation entry has platform ID `0xEF`, a zero checksum, and signature `0x55AA`, and the default boot entry is bootable (`0x88`) with no-emulation media (`0x00`). These checks establish package structure and candidate identity. Guest evidence for the rendered v0.2 splash and absence of diagnostic overlays is recorded in §4.

## 4. Guest qualification results

**Counts:** independent cold boots 3/3; UEFI desktop entries 3/3; warm resets 1 successful under TCG; interactive soak 20m55s (boot 1) and 16m33s (boot 2); completed QAC guest launches 3; unexpected kernel faults 0.

| Required exact-ISO guest check | Result |
| --- | --- |
| v0.2 splash; no BM/AIDA overlays | Pass. Captured teal gradient at RGB (95,212,196) top-left and (13,125,119) bottom-left, matching the bootloader splash implementation. Normal ELF loader progress text is visible over the gradient; no BM/AIDA overlay appeared. Screenshot: out/rc3q_guest_qualification_20261109/boot3-tcg/boot-splash-after-reset.png. |
| Desktop initialization and continuing heartbeat | Pass, 3/3 cold boots. Each reached [DESKTOP] initialized and CONTINUOUS_HEARTBEAT_FRAME=1. |
| Keyboard and pointer operation in QEMU | Pass. Console accepted keyboard commands; QMP PS/2 mouse movements/clicks opened Start, launched apps, operated calculator controls, and closed windows. Boot 2 ended with zero dropped keyboard and mouse events. |
| Start menu and desktop shortcut launching | Pass. Start and All Programs opened; Calculator and Console launched. Double-clicking the Files desktop shortcut opened Computer Files. |
| Calculator | Pass. Mouse-entered 2 + 3 = displayed 5. |
| Display Options, close and reopen | Pass. Window opened, closed through its title button, and reopened. |
| Disk Manager read-only inspection | Pass. The guest reported no available disk size and no volumes. The visible format, filesystem, and partition controls were not invoked. |
| Task Manager Memory Details | Pass. The Memory Details tab displayed. Snapshot showed failed invalid frees 0, failed no-pages frees 0, and Leak Exists: FALSE. |
| Console and safe netinit | Pass. With QEMU -net none, netinit initialized the stack, scanned for NICs, and returned No supported NIC found; no network was attached. |
| WAV Player safe window construction | Pass. Empty WAV Player window constructed through launch wav; no file was selected and playback was not started. |
| Window teardown and desktop responsiveness | Pass. WAV Player, Disk Manager, Display Options, Calculator, Task Manager, and Console windows closed; the desktop remained responsive and Start toggled open/closed afterward. |
| Warm reset | Pass under TCG. QMP system_reset returned to OVMF, booted the same DVD again, initialized a second desktop, and emitted heartbeat. Under WHPX, both QMP reset and guest Console reboot instead caused QEMU to exit with code 0 and WHPX: Unexpected VP exit code 4; neither was counted as a successful WHPX reset. |
| Bounded interactive stability/soak | Pass. Boot 1 ran 20m55s and boot 2 ran 16m33s with repeated heartbeat, graphics valid, zero invalid/no-pages/corrupt allocator frees, and zero dropped keyboard/mouse events. |
| Unexpected kernel faults | None observed in the three serial logs. WHPX emitted a host virtualization VP-exit warning during reset; it was not a guest kernel fault. |

The successful desktop and application checks are exact-ISO guest evidence. The first two WHPX runs exercised the long interactive soak; the third TCG run established the warm-reset path and second desktop entry. Boot 3 was shut down through QMP quit, followed by verified QAC reservation release.

## 5. GHSA-p3jp-9v83-76w9 assessment

The [official .NET advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-p3jp-9v83-76w9) lists `Microsoft.DotNet.ILCompiler` 9.0.0 as affected (`>=9.0.0, <9.0.22`) and 9.0.22 as patched. Its FAQ scopes impact to an application that uses an affected Brotli decoder, including one linked into NativeAOT, to decompress attacker-controlled data. The [.NET 9.0.22 release notes](https://github.com/dotnet/core/blob/main/release-notes/9.0/9.0.22/9.0.22.md) likewise identify Brotli decompression APIs as affected usage paths.

### Exact RC3 build identities

- The production kernel project restore in the isolated RC3 build copy records `Microsoft.DotNet.ILCompiler` and `runtime.win-x64.Microsoft.DotNet.ILCompiler`, both `7.0.0-alpha.1.22074.1`, for the `net7.0` kernel project. These are distinct from the .NET 9 advisory's affected version range. The kernel's source-level PNG path uses the repository's own `LodePNG.ZlibDecompress`, not the .NET Brotli API.
- The registered NativeAOT app cohort uses the private `guidexos-x64` toolchain at version `9.0.0`: `Microsoft.DotNet.ILCompiler` plus `runtime.guidexos-x64.Microsoft.DotNet.ILCompiler`. The preserved Phase 23 runtime-pack manifest records source commit `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; Phase 29–35 outputs carry Phase 26 build records and appear in the 41-artifact registry audit. The compiler is source-built and GUIDEXOS-patched, but its build package identity/version remains 9.0.0. Thus affected-version package presence is established as a build input for these AOT artifacts, not as a package shipped inside the ISO.

### Linkage and data reachability

- A source scan of the production kernel/application trees found no `BrotliDecoder`, `BrotliStream`, `BrotliEncoder`, `System.IO.Compression`, or HTTP automatic decompression use. The only matching production decompression routine is the custom PNG zlib/DEFLATE decoder.
- Preserved Phase 29–35 maps and printable strings from the shipped kernel and RAMDISK Native executables contained no Brotli decoder/API reference. The Phase 23 target package manifest stages a narrow set of target-native assets and does not list a Brotli library. The RC3 managed artifact audit reports 41/41 identity agreement for the registered artifacts.
- No production application invocation of Brotli decompression or attacker-controlled input flow to a Brotli decoder was demonstrated. Data that reaches the PNG loader uses its custom zlib path. Guest `netinit` safely reported no supported NIC while QEMU was configured with `-net none`; this does not qualify physical network exposure and is not evidence of a Brotli path.
- The final AOT outputs do not retain a complete symbolized linker closure for every embedded executable. Therefore the review found **no decoder linkage/reference or reachable decoder path in the available evidence**, but cannot make a conclusive binary-level absence claim from the available outputs alone. Package presence, linked decoder presence, and exploitability are separate findings; only the first is established for the .NET 9 app cohort.

No advisory exploitability was demonstrated, and no affected reachable decoder was established. Because the link closure is incomplete, the advisory is not formally cleared as a public-release security review. If exact link evidence shows an affected reachable decoder, treat that as a public-release security blocker and build a separately qualified patched candidate. The smallest additional evidence is a symbol-aware final-link map/closure for the 41 exact registered AOT artifacts (or equivalent retained linker trace) showing whether Brotli decoder entrypoints and their native support library are linked, paired with the existing application call-site/input review. Do not alter the frozen ISO to obtain it.

### Custom runtime update compatibility

The custom NativeAOT runtime does not modify a Brotli-specific source file in the reviewed runtime diff, but it is not stock .NET 9: it has the private `guidexos-x64` RID/target OS, a patched ILCompiler target contract, a custom runtime pack, and GUIDEXOS CoreLib interop/PAL, startup, thread, and GC adaptations. The compiler/runtime pack cannot be safely updated by changing a NuGet version alone. Compatibility of these source patches, package contents, CoreLib adaptation, linker/PAL contract, and the 41 artifact proofs with 9.0.22 has not been demonstrated. A patched compiler/runtime update requires a separately scoped source rebase and candidate qualification; it was not attempted here.

## 6. Phase 23 / Phase 24 disposition

The preserved `phase23-proof-rebuild.log` identifies the unresolved `RhRegisterOSModule` as LNK2019 in `bootstrapper.obj` while building `UserManagedProof/guideXOS.UserManagedProof.csproj`. This is the older standalone proof-image rebuild, not the production kernel project or its final production link. It does not establish a missing RC3 production dependency.

The two absent legacy files are `Native/guideXOS.UserManagedProof.exe` and `Native/guideXOS.UserManagedProof.gxmi`. The RC3 artifact audit contains 41 registered Phase 29–35 artifacts and neither file. `UEFI_DIAGNOSTIC_RING3_PHASE24` is an opt-in diagnostic selector; `Kernel/Misc/Ring3Proof.cs` explicitly stops at mapping/validation/teardown and does not dispatch managed entry. The omitted files are therefore intentionally outside the RC3 production set; they must not be restored just to satisfy the historical Phase 24 proof input. This disposition is based on the project selector, phase code, registry audit, and failed-project identity, not on an assumption that a production linker error is optional.

## 7. AIDA_LPT physical acceptance handoff

**Physical acceptance is pending.** The owner should test the exact SHA-256 in §3 and record actual results; no prior AIDA3 diagnostic result substitutes for this RC3 run.

- [ ] Clean production boot; confirm no blue bands.
- [ ] Full graphical desktop appears and remains responsive.
- [ ] Built-in touchpad operates.
- [ ] Open Start; launch applications from Start and desktop shortcuts.
- [ ] Calculator opens and performs a basic calculation.
- [ ] Display Options opens, closes, and reopens.
- [ ] Disk Manager is limited to read-only inspection.
- [ ] Task Manager opens Memory Details.
- [ ] Console opens; `netinit` completes safely, including a clean unsupported-NIC result if applicable.
- [ ] WAV Player constructs/opens its safe window.
- [ ] Close and reopen windows; confirm interactive responsiveness and stability for 10–15 minutes.
- [ ] Reboot and confirm a second clean desktop entry.

Record device/model, firmware mode, date, ISO SHA-256, each observed result, any fault text, and stability duration. Do not perform destructive disk operations.

Historical limitations remain: SARAH_DT's modern input is unqualified; ASUS VivoBook built-in input was previously reported nonfunctional. These are documentation only; Modern Input was not modified.

## 8. Remaining gates and smallest next step

1. Keep publication blocked until a symbol-aware final-link closure for the 41 exact registered NativeAOT artifacts, or an equivalent retained linker trace, establishes whether the affected Brotli decoder entrypoints and native support code are linked. Pair it with the already reviewed application call-site and input-flow evidence. If an affected reachable decoder is established, qualify a patched 9.0.22-based candidate separately; do not modify the frozen RC3 ISO.
2. Obtain owner AIDA_LPT results against the exact ISO SHA-256 in §3. Physical acceptance remains pending; prior AIDA3 diagnostics do not substitute for this run.
3. If WHPX reset support is a required host profile, investigate the host/QEMU Unexpected VP exit code 4 separately. The exact guest warm-reset path passed under TCG, and no kernel fault was observed.

The smallest next release-review step is the exact-artifact NativeAOT link-closure evidence. No runtime upgrade, feature change, rebuild, new ISO, product-source commit, push, physical test, or publication was performed for this closeout.
