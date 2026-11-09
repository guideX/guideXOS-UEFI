# guideXOS C# UEFI v0.2 RC3Q Guest and Security Closeout

**Date:** 2026-11-08
**Outcome:** `RC3_GUEST_PENDING` (C)
**Publication:** Not authorized. No push or publication occurred.

## 1. Disposition

The frozen RC3 ISO is intact and its ISO, ESP, EFI loader, kernel, and RAMDISK identities match the recorded candidate. No OVMF guest checks ran: QAC's exclusive acceptance lane was occupied by another project's verified session. This is **GUEST_PENDING**, not a guest pass or regression.

The production RAMDISK's NativeAOT app cohort was built with an affected-version .NET 9 package identity; the package archive is a build input, not an ISO-shipped package. Repository source and available payload/map inspection found no Brotli decoder call or reference and no demonstrated untrusted-data path to one. Existing final AOT link evidence is not sufficient to prove decoder absence conclusively, so the advisory is **not formally cleared for an owner publication decision**. No affected reachable decoder or exploitability was demonstrated. The next security evidence is specified in §5.

The Phase 23 link failure belongs to rebuilding the older standalone Phase 23 proof input. The two omitted legacy Phase 24 files are outside the 41-item release registry, and Phase 24 is an opt-in diagnostic mapping/teardown selector. No unresolved production link dependency was found.

## 2. Preservation, source, and QAC census

### Source and worktree

- Host: `LEON2_RDT`; authoritative checkout: `D:\dev\guideXOSUEFI`.
- Branch `main`, HEAD `652ad93932528a02861686d444257499bcd95a3d`, exactly equal to `origin/main` at preflight (ahead/behind `0/0`). Tracked source and index were clean.
- Pre-existing untracked artifacts included the RC1/RC2/RC3 and AIDA/BM diagnostic ISOs, `Tests/PciMmioResourceRegression/bin/` and `obj/`, and `docs/UEFI_AIDA3_BLUE_SCREEN_BOOT_LOCALIZATION.md`. They were preserved.
- Nested `out/rt` remained at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with pre-existing NativeAOT source edits and generated/test-file deletions. This qualification did not edit or restore it. No reset, clean, stash, branch change, merge, cherry-pick, rebase, or publication was performed.
- Existing `MODERN_INPUT`, `APP_MODEL`, `FONT_SUPPORT`, and `BUILD_INFRA` work and existing ISO/build artifacts were left untouched. No product source, runtime, or ISO was rebuilt or modified.

### QAC ownership and cleanup

QAC CLI `status`, `list`, and `queue` were used. At the first snapshot the coordinator was online (v2, PID 8204), with one managed QEMU and no foreign QEMU. Reservation `2cfb19b75c5c4556859566c88df6d7ed` was a running `guideXOSServer` acceptance session (verified owner PID 27916; QEMU PID 26568). Another verified `guideXOSServer` acceptance request was waiting in the coordinator queue. At the final snapshot, the same unrelated acceptance reservation remained active; the queue was empty, and QAC still reported one managed QEMU and zero foreign QEMU.

No RC3 reservation was submitted or granted, no RC3 QEMU process was started, and no unrelated reservation was modified, canceled, or released. Therefore there is no RC3 reservation to release; QAC/process cleanup for this qualification is **not applicable**. No unmanaged emulator was started. The admission gate was unavailable, so testing stopped here.

## 3. Frozen ISO and payload identity

The exact expected file was checked in place; no cached candidate was substituted.

| Artifact | Bytes | SHA-256 | Result |
| --- | ---: | --- | --- |
| `ISO/guideXOS-CSharp-UEFI-v0.2-RC3.iso` | 195,741,696 | `530F246B84B7D4F1200CAD22654A48B8A536D983007EEC1C0D52D4F9B48BDE8C` | Matches expected |
| Embedded `EFIESP.IMG` | 134,217,728 | `2ED8455AF4B2F170FEE52391373694EA0E6B16F8029DDC1DCF63245701B814A3` | Matches record |
| ISO-root `BOOTX64.EFI` and `EFI/BOOT/BOOTX64.EFI` | 50,688 each | `3532556FFFF783BE5F3B715FE17F7685C8520C74912260994356764E58D5BB4E` | Equal |
| ISO-root `kernel.elf` | 4,739,072 | `71F31F435112FD1F5CA932CBAAE537540EC0EA8C87336C8C2A414353630C5DE1` | Matches record |
| ISO-root `ramdisk.img` | 56,297,702 | `0805FA9A825124630764FC05E3116AD03A0FFCEFCB24B216862BC3880E10764F` | Matches record |

The EFI loader, kernel, and RAMDISK extracted from the embedded FAT ESP have the same hashes as their ISO-root copies. The ESP volume label is `GUIDEXOSRC3`. The ISO archive check passed (7-Zip exit 0); the El Torito validation entry has platform ID `0xEF`, a zero checksum, and signature `0x55AA`, and the default boot entry is bootable (`0x88`) with no-emulation media (`0x00`). These verify package structure and production-candidate identity; without a guest boot, they do **not** verify the rendered v0.2 splash or the visual absence of diagnostic overlays.

## 4. Guest qualification results

**Counts:** independent cold boots `0/3`; warm resets `0`; interactive soak `0 minutes`; QAC guest launches `0`. No guest acceptance step was attempted because QAC admission was occupied. No pass or failure is inferred.

| Required exact-ISO guest check | Result |
| --- | --- |
| v0.2 splash; no BM/AIDA overlays | Not run |
| Desktop initialization and continuing heartbeat | Not run |
| Keyboard and pointer in supported QEMU profile | Not run |
| Start menu and desktop shortcut launch | Not run |
| Calculator | Not run |
| Display Options, close and reopen | Not run |
| Disk Manager read-only inspection | Not run |
| Task Manager Memory Details | Not run |
| Console and safe `netinit` | Not run |
| WAV Player safe window construction | Not run |
| Window teardown and desktop responsiveness | Not run |
| Warm reset | Not run |
| Bounded interactive stability/soak | Not run |
| Unexpected kernel-fault observation | Not run |

The previous RC3 qualification's hosted regression/build observations remain historical evidence; they are not represented as guest results from this closeout.

## 5. GHSA-p3jp-9v83-76w9 assessment

The [official .NET advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-p3jp-9v83-76w9) lists `Microsoft.DotNet.ILCompiler` 9.0.0 as affected (`>=9.0.0, <9.0.22`) and 9.0.22 as patched. Its FAQ scopes impact to an application that uses an affected Brotli decoder, including one linked into NativeAOT, to decompress attacker-controlled data. The [.NET 9.0.22 release notes](https://github.com/dotnet/core/blob/main/release-notes/9.0/9.0.22/9.0.22.md) likewise identify Brotli decompression APIs as affected usage paths.

### Exact RC3 build identities

- The production kernel project restore in the isolated RC3 build copy records `Microsoft.DotNet.ILCompiler` and `runtime.win-x64.Microsoft.DotNet.ILCompiler`, both `7.0.0-alpha.1.22074.1`, for the `net7.0` kernel project. These are distinct from the .NET 9 advisory's affected version range. The kernel's source-level PNG path uses the repository's own `LodePNG.ZlibDecompress`, not the .NET Brotli API.
- The registered NativeAOT app cohort uses the private `guidexos-x64` toolchain at version `9.0.0`: `Microsoft.DotNet.ILCompiler` plus `runtime.guidexos-x64.Microsoft.DotNet.ILCompiler`. The preserved Phase 23 runtime-pack manifest records source commit `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; Phase 29–35 outputs carry Phase 26 build records and appear in the 41-artifact registry audit. The compiler is source-built and GUIDEXOS-patched, but its build package identity/version remains 9.0.0. Thus affected-version package presence is established as a build input for these AOT artifacts, not as a package shipped inside the ISO.

### Linkage and data reachability

- A source scan of the production kernel/application trees found no `BrotliDecoder`, `BrotliStream`, `BrotliEncoder`, `System.IO.Compression`, or HTTP automatic decompression use. The only matching production decompression routine is the custom PNG zlib/DEFLATE decoder.
- Preserved Phase 29–35 maps and printable strings from the shipped kernel and RAMDISK Native executables contained no Brotli decoder/API reference. The Phase 23 target package manifest stages a narrow set of target-native assets and does not list a Brotli library. The RC3 managed artifact audit reports 41/41 identity agreement for the registered artifacts.
- No production application invocation of Brotli decompression or attacker-controlled input flow to a Brotli decoder was demonstrated. Data that reaches the PNG loader uses its custom zlib path. Safe `netinit` behavior and network exposure remain guest-unverified; this is not evidence of a Brotli path.
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

1. When the current unrelated QAC acceptance reservation releases, obtain admission through QAC and qualify this exact ISO, beginning with up to three independent cold boots and recording the actual count. Do not bypass QAC ownership.
2. Obtain exact-build symbol-aware link closure to complete the Brotli linkage determination. If affected decoder reachability is established, stop release approval and qualify a patched 9.0.22-based candidate separately.
3. After guest success, obtain owner AIDA_LPT results against the same ISO hash. Physical acceptance and publication authorization remain with the owner.

No runtime upgrade, feature change, rebuild, new ISO, product-source commit, push, physical test, or publication was performed for this closeout.
