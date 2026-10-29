# MI5 Production Build and Diagnostic Image

## Outcome

**`MI5_UPSTREAM_BUILD_BLOCKED`**

The production build cannot start from this checkout's current inputs. The pinned .NET SDK is absent, the Phase 19–23 generated runtime inputs are absent, and the Phase 29–35 build records required for kernel identity generation are missing. No identity records or stubs were invented, and no production kernel or image was created.

## Checkout and changes

- Branch: `MODERN_INPUT`
- Starting HEAD: `151482903ca1dca760fdc66b85e729aa16456218`
- Pre-existing untracked directories preserved: `_mi2-validation-20261028/` and `guideXOS/artifacts/`
- Product source changes: none. This report is the only MI5 source-tree addition.
- New test outputs are isolated under ignored `out/mi5-validation/`.

## Supported identity generation

`build.ps1` builds and stages the managed payload cohort, then invokes `Tools/ManagedArtifacts/managed_artifacts.py` with `--stage generated`, the ramdisk source, and output paths `guideXOS/obj/ManagedArtifactIdentities.g.cs` and `out/h2c-managed-artifact-audit.csv`. The generated C# source is a build output; it is not a checked-in source file.

The generator registers **41 payload variants** across Phases 29–35. For every variant it reads the phase build record, the staged `.exe` and `.gxmi`, and the final build `.exe` named by the record. It verifies descriptor flags and digest, normalized PE timestamps, recorded size and SHA-256, and byte-for-byte equality between the final build output and staged executable. The phase records are generated evidence; the executables, descriptors, and identity source are generated build outputs. The generator does not accept a hand-written substitute.

The current checkout has no `out/dotnet` build records. The required records are:

| Phase | Required build record |
| --- | --- |
| 29 | `out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json` |
| 30 | `out/dotnet/phase30-managed-clipboard/phase30-managed-clipboard-build.json` |
| 31 | `out/dotnet/phase31-managed-shell/phase31-managed-shell-build.json` |
| 32 | `out/dotnet/phase32-managed-open-document/phase32-managed-open-document-build.json` |
| 33 | `out/dotnet/phase33-managed-shell-action/phase33-managed-shell-action-build.json` |
| 34 | `out/dotnet/phase34-managed-resource-read/phase34-managed-resource-read-build.json` |
| 35 | `out/dotnet/phase35-managed-persistent-read/phase35-managed-persistent-read-build.json` |

A read-only run of the supported identity generator stopped at the missing Phase 29 record. `guideXOS/obj/ManagedArtifactIdentities.g.cs` is absent, and no identity source or identity audit was emitted.

### Phase 19–23 dependency frontier

The repository contains the generation scripts, source contracts, and lock files, but this checkout does not contain the generated outputs and validation evidence those scripts consume:

| Stage | Required inputs or outputs | Classification and state |
| --- | --- | --- |
| Phase 19 | `guidexos_nativeaot_pal_contract.obj`, `.lib`, `phase19-manifest.json`, and `toolchain-record.json` | Generated PAL contract outputs and validation record; absent under this checkout's `out/dotnet/phase19-runtime-pack/`. |
| Phase 20 | Pinned-runtime source audit and package-boundary review | Validation evidence. Its repository documentation and source locks are present; its generated reports are not direct inputs to `build.ps1`'s Phase 23 pack staging. |
| Phase 21 | Clean runtime checkout, Phase 21 source transform record, GC/source audit outputs, and built GUIDEXOS runtime objects | The checkout is pinned to `dotnet/runtime` commit `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. The local `out/rt` checkout and generated records/objects are absent. |
| Phase 22 | Phase 22 source transform record and target-native runtime build outputs | Generated source evidence and runtime objects; absent. |
| Phase 23 | Phase 23 source transform record, private `ilc.exe` and build record, link shim, runtime-pack manifest, and three NuGet packages | Generated compiler/runtime outputs and provenance; absent under this checkout's `out/dotnet/phase23-*` paths. |
| Phase 29 | Phase 23 pack plus five notification payload build outputs and the Phase 29 build record | Final build outputs and provenance record are absent under `out/dotnet`; the tracked staged payload files exist, but their bytes do not match the candidate record found in the other checkout. |

`build.ps1` expects `out/rt` to be a Git checkout so it can verify status before applying Phase 21–23 transforms; it does not provision that checkout when it is absent. The pinned runtime source, patches, and lock files are available in the repository, but the required local checkout and generated evidence are not.

The supported build also requires .NET SDK **10.0.401** with roll-forward disabled by `UserManagedProof/global.json`. This host has only SDK **10.0.403**. The build preflight rejects SDK candidates that do not match the pin, and `Tools/build_phase19_runtime_pack.ps1` also checks for an exact 10.0.401 match. Running `dotnet --version` in `UserManagedProof/` confirmed that SDK resolution fails with “A compatible .NET SDK was not found.” Therefore the supported pipeline is not reproducible in this environment as configured. No SDK was installed and no pin was changed.

## External artifact check

The separate `D:\dev\guideXOSUEFI` checkout was inspected read-only. Its Phase 29 record names built executables in that checkout and records their hashes, but its values do not match this branch's staged files:

| Variant | Other-checkout record SHA-256 | This checkout staged SHA-256 |
| --- | --- | --- |
| success | `F95857C741553ED0E951EC48D5642C164FFE60B8FD3751375BD715F1A15ADFBF` | `275C4F166F6964478A68D08E61B22BAB23507CF2B07F32C1BB7EE90EE52B0F56` |
| title-failure | `C4971145688466B7F59385F7738D38D34EBE46C3510FFA262FFBACD9717DB342` | `0705F5990DD159FFE971768B795954899E7BEADD5F6FE7E5184422A03E70BC8D` |
| body-failure | `0A6DBAA0CE747EC6CBFE42FB0741B7E2A3A6B756A6B8AD7B479764D3D14B28BC` | `16D7C0FA03520308AB2C85D19CE38D717E3B4C8CB664F0B273293AFE1A411C68` |
| invalid-type | `07140CD111FC7A467B8F6CC8DFD8D93320B246EAA76A078FA9F2DE4778337CE0` | `BCF2D54F01707D08F6C0439606365CB4B9B9F58D28771D5E693E8D2AA3B68988` |
| failfast | `E5B7301054D1393AC6F678C8F9243E69C4DB0A1E5F69DCDD95DDB665AFC300E9` | `101606F5BD2786C851B7C9F5A8453E2F1871D89C910FB9DAFC3DBCBF70852C90` |

The supported collector, run with the other checkout's records and this checkout's ramdisk source, rejected the first Phase 29 executable because it differed byte-for-byte from its recorded build output. The other checkout's Phase 23 pack also refers to its own modified `out/rt` tree and source-patch records. No records, generated sources, or build artifacts were imported from that checkout.

## MI4 source review and hosted regression results

The MI4 source path remains gated as designed: the loader accepts the exact standalone LoadOptions token `--xhci-capability-probe`; without it the loader does not set the BootInfo probe flag. The kernel requires UEFI mode and that flag, and calls the probe after `ThreadPool.Initialize` completes. The thread-pool initialization enables NXE before the probe's NX mapping is used. The probe path validates and matches the firmware resource handoff, maps only the bounded capability-header span as uncached, read-only, and NX, then uses `TryRead32` for the seven fixed capability dwords. The reviewed path contains no xHCI register-write, controller reset, bus-master enablement, DMA programming, USB enumeration, or HID initialization.

These are source-level findings and hosted checks. The requested validation against real production loader/kernel artifacts could not be performed because no authentic kernel was built.

| Hosted suite | Result |
| --- | ---: |
| C# xHCI discovery and synthetic MMIO | PASS, 131 assertions |
| C++ PCI descriptor and exact-option parser | PASS, 26 assertions |
| C++ loader/kernel resource-handoff ABI | PASS, 149 assertions |
| **Total** | **PASS, 306 assertions** |

The C# suite built with zero warnings and zero errors. The ABI test emitted one `NULL` macro redefinition warning from the EDK II header versus the MSVC runtime header; all 149 assertions passed. The test outputs are in `out/mi5-validation/`. They use hosted or synthetic inputs and do not establish hardware MMIO behavior.

## Production build, image, and hardware status

- Authentic production kernel build: **not attempted**. The pinned SDK and generated Phase 19–23 and Phase 29–35 inputs are unavailable; the normal generator fails closed at the missing Phase 29 record.
- Production loader: MI4's prior loader build result remains the available evidence; the loader was not rebuilt because no loader defect was found.
- Diagnostic image: **not produced**. There is no image path or image hash, and no constituent MI5 build identities to record.
- Packaging: `build.ps1` assembles an `ESP` directory containing `EFI/BOOT/BOOTX64.EFI`, `kernel.elf`, and `ramdisk.img`. Its `-CreateISO` option only prints that external ISO tools are required; it does not create an ISO. `Tools/create_uefi_img.bat` creates a GRUB `efi.img`, not a guideXOS diagnostic disk image. No USB installation steps are claimed for an image that does not exist; the owner should select and validate a FAT32 UEFI ESP packaging method after the production artifacts are available.
- QEMU: not used. Physical hardware: not booted or accessed. GitHub push: none. QAC admission: not requested.

## Infrastructure handoff and next qualification

The managed-artifact/runtime build owner should restore the pinned SDK 10.0.401 and a clean `out/rt` checkout at commit `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, then produce the Phase 19 PAL pack and Phase 21–23 source/runtime evidence through the checked-in scripts. With the Phase 23 pack available, the supported Phase 29–35 builders must generate records and payloads that match the staged bytes; the 41-record identity generator must then complete without a stub. Only after the normal kernel production build and the loader/kernel/image hashes are verified should an isolated diagnostic image and media instructions be prepared.

For later owner-authorized hardware qualification, use [MI4_PHYSICAL_QUALIFICATION_PLAN.md](MI4_PHYSICAL_QUALIFICATION_PLAN.md). Keep the probe disabled by omitting `--xhci-capability-probe`; enable only with that exact token in the one-time UEFI image LoadOptions. Retain the normal recovery entry and continue to prohibit controller initialization, MMIO writes, DMA, enumeration, and HID work during capability-only qualification.
