# AMCONV2G-R1R1 NativeAOT Link Recovery and Offline Package Completion

**Outcome A — `AMCONV2G_R1R1_LINK_AND_PACKAGES_VERIFIED`**
**Qualification date:** 2026-11-01
**Branch:** `APP_MODEL`

## Result

The NativeAOT kernel now links without unresolved-symbol forcing. The produced PE was converted through the repository's `pe_to_elf_v2.py` and `KMain` map path into a structurally validated ELF64 kernel. Both RDSK v1 and explicit RDSK v2 ESP candidates were assembled in isolated output folders and passed offline package and identity checks.

Both candidates are **offline package ready for guest testing**. No UEFI guest was booted, so no firmware handoff, kernel startup, or guest lifecycle behavior is claimed. Focused hosted tests exercise the revised folder-probe dispatch through an abstract base class; they do not execute NativeAOT cached interface dispatch in the bare-metal kernel.

## Revisions and preservation

- Expected and verified starting HEAD: `27c3dbd68e1f79a664c67120b7d169243b8769d2`.
- Implementation and test commit (technical ending HEAD): `5f285e71e7336559ddad7f78f174409a1158d3c7` (`fix: align folder probe with bare-metal AOT dispatch`).
- This report is committed as a separate direct child of the implementation commit. Its commit is the repository's final HEAD and is recorded in the completion report; the source qualification HEAD above is kept distinct from that documentation-only commit.
- The rejected forced-link image from R1 remains rejected and was not used, converted, staged, or committed.
- Existing untracked `bin/` and `obj/` outputs under AMCONV2D, AMCONV2E, and AMCONV2F were preserved. New builds and test outputs were isolated under `out/amconv2g-20261101-r1r1-final/`.
- The repository root `ESP` was not modified. Previously retained partial candidates were not overwritten. The tracked release `ramdisk.img` was not modified.
- Work was confined to this checkout. No branch switch, merge, rebase, push, global SDK change, QEMU launch, or QAC request/admission occurred. Protected checkouts were not modified.

## Original link failure and symbol investigation

The R1 publish failed with `LNK2001: unresolved external symbol RhpInitialDynamicInterfaceDispatch` from generated `guideXOS.obj`. Inspection found the reference at the NativeAOT-generated interface-dispatch call associated with `ShellFolderMountBinding.CSharpFileSystemFolderProbe.TryInspectFolderPath`; the object also contained an interface dispatch map for that probe. The rejected R1 response file included `/FORCE:UNRESOLVED`, so its emitted PE was not a valid kernel candidate.

The compiler and runtime packages are pinned to `7.0.0-alpha.1.22074.1`. The corresponding runtime source was checked at runtimelab revision `04b092003db5ba207d4fa3f3becb7f01828bf16c`:

- [`StubDispatch.asm` at the pinned revision](https://github.com/dotnet/runtimelab/blob/04b092003db5ba207d4fa3f3becb7f01828bf16c/src/coreclr/nativeaot/Runtime/amd64/StubDispatch.asm) defines `RhpInitialDynamicInterfaceDispatch` as an alternate entry for `RhpInitialInterfaceDispatch`. That stub continues into the runtime slow-dispatch path and depends on `RhpCidResolve`.
- [`CachedInterfaceDispatch.cs` at the pinned revision](https://github.com/dotnet/runtimelab/blob/04b092003db5ba207d4fa3f3becb7f01828bf16c/src/coreclr/nativeaot/Runtime.Base/src/System/Runtime/CachedInterfaceDispatch.cs) exposes the runtime export used by the generated feature contract.
- [`CachedInterfaceDispatch.cpp` at the pinned revision](https://github.com/dotnet/runtimelab/blob/04b092003db5ba207d4fa3f3becb7f01828bf16c/src/coreclr/nativeaot/Runtime/CachedInterfaceDispatch.cpp) implements the corresponding runtime feature.

The symbol is not absent from the selected package: `Runtime.WorkstationGC.lib` contains a genuine `RhpInitialDynamicInterfaceDispatch` definition. Its SHA-256 is `DAFDBDC46BF83726D165A36E3C3DB5F670CB8F79A3032FD45E2A09022485C6C3`; `dumpbin /linkermember:2` and `/symbols` confirmed the member and definition. The customized `Microsoft.NETCore.Native.Windows.props` intentionally excludes the NativeAOT bootstrapper/runtime archive and Win32 libraries; `packages/README.md` records that customization. The kernel's exact `Corlib`, `Kernel`, and `guideXOS` inputs do not define `RhpCidResolve` or the cached-dispatch resolver.

A diagnostic attempt to link the whole workstation archive pulled in a large, unsuitable hosted-runtime dependency graph: 114 unresolved externals, duplicate helper/runtime conflicts, and Windows API/CRT dependencies, including the dispatch path's unresolved `RhpCidResolve`. Thus this is not a missing archive member, and the evidence does not indicate an ABI spelling mismatch. The failure is a compiler/runtime **feature-set and target mismatch**: this kernel's customized static bare-metal link does not provide the complete cached dynamic-interface-dispatch runtime contract. Adding the archive wholesale would not restore that contract safely. No symbol stub, guessed alias, forced extraction, or unresolved-link option was used in the repair.

## Repair

The internal `IShellFolderDirectoryProbe` seam became the abstract `ShellFolderDirectoryProbe` base class. The production C# filesystem probe and hosted fakes derive from it. The existing virtual inspection result and error mapping behavior is preserved. This keeps the internal seam as class dispatch for the bare-metal target instead of asking its NativeAOT output to use the unsupported cached-interface dispatch feature.

The focused regression calls two probe subclasses through base-class references and checks method selection, return values, repeated calls, and null-probe behavior. This verifies the managed seam behavior under the hosted test runtime. It does not prove execution of the cached interface dispatch helper in the UEFI kernel; the rebuilt kernel object no longer contains `RhpInitialDynamicInterfaceDispatch`, `RhpCidResolve`, or an interface dispatch map for this path.

The project file changes also:

- removed `/FORCE:UNRESOLVED` from NativeAOT linking;
- centralized the pinned kernel NativeAOT package version in `KernelNativeAotVersion`;
- added a configurable, correctly quoted `KernelLinkMapPath`; and
- guarded `EnsureNativeOutDir` when `SkipISO=true`, allowing isolated publish without invoking ISO output setup.

The NativeAOT package version was not upgraded or downgraded, and no runtime archive was added to the final kernel link.

## Authentic kernel publish and ELF validation

The isolated Release `win-x64` AppRuntime publish used the user-local .NET SDK `10.0.401`, Visual Studio 18 Community, and MSVC/linker `14.51.36260.0`, with the project-pinned NativeAOT compiler/runtime packages. Output, intermediate, native object, and map paths were redirected under `out/amconv2g-20261101-r1r1-final/`.

The final `link.rsp` contains no `/FORCE:UNRESOLVED`, no `Runtime.WorkstationGC.lib`, and no forced helper symbols. It links the generated object and normal native inputs with the fixed image base and `KMainWrapper` entry contract. The native PE publish succeeded with zero errors; the native linker emitted only `LNK4281` about the configured base `0x10000000`. Existing C# warnings remain; compile-only summaries are below.

The linked PE is AMD64 PE32+, Native subsystem, with entry `KMainWrapper` at `0x100018B8`, no import table, and no base-relocation directory because the image is fixed at `0x10000000`. Its SHA-256 is `0C5FF5C68178C3384214D401638BA25982FFF741C92D064216F892E9CC506D4E`. The output `guideXOS.obj` scan for `RhpInitialDynamicInterfaceDispatch`, `RhpCidResolve`, and `__InterfaceDispatchMap` is empty. The output map resolves `KMain` to `0x1004DD7C`.

The supported converter was run as:

```text
py -3 Tools/pe_to_elf_v2.py <publish>/native/guideXOS.exe <output>/kernel.elf --map <output>/Kernel.map --symbol KMain
```

It located the `KMain` function prologue at the map address and emitted `kernel.elf` (4,882,432 bytes; SHA-256 `55F7952552A9F0E97B3072E60A824ABD4A3DB18C1EF0B71EA71B8EC65950C33E`). Structural checks confirmed ELF64, little-endian, `ET_EXEC`, `EM_X86_64`, eight bounded `PT_LOAD` segments, valid page alignment/congruence, no `PT_DYNAMIC` or `PT_INTERP`, and an entry inside an executable load segment. The loader maps segments relative to the minimum virtual address and transfers to the fixed image entry under its existing contract. This is the repository's supported PE-to-ELF output, not a wrapper around an incompatible executable. Structural validation does not establish a successful firmware boot.

## Build warnings and regression results

Fresh isolated Release compile summaries using SDK `10.0.401`:

| Configuration | Errors | Warnings |
|---|---:|---:|
| AppModel | 0 | 137 |
| AppRuntime | 0 | 141 |

The established prior summaries were 133 and 137 warnings, respectively. Comparing warning diagnostics by file, line, and code showed the same source diagnostic set; AppRuntime has four additional existing `CS0162` diagnostics relative to AppModel. The current totals also include SDK restore/build notices (`NETSDK1138` and `NETSDK1201`). The source warning set did not gain a new diagnostic from this repair. NativeAOT publish C# warnings were existing diagnostics; the publish had zero errors and the only native-link warning was `LNK4281`.

| Regression | Result |
|---|---:|
| AMCONV1 source compatibility | 9/9 pass |
| AMCONV2B/C source validation | 25/25 pass |
| AMCONV2B/C folder and mount hosted suite | 71 assertions, 0 failures |
| AMCONV2D FAT inspection | 11 assertions, 0 failures |
| AMCONV2E RDSK v1 inspection | 32 assertions, 0 failures |
| AMCONV2F source validation | 9/9 pass |
| AMCONV2F generator and rejection tests | 12 assertions pass |
| AMCONV2F reader/runtime, including v1 compatibility | 58 assertions, 0 failures |
| AMCONV2G package tests | 4/4 pass |
| Desktop interaction | 42/42 pass using .NET 8.0.33 x86 runtime |
| Phase 33 wire regression | Pass; request 2,084 bytes, response 16 bytes |
| Task Manager Memory Details regression | Pass using an output-only PowerShell compatibility copy |
| Managed artifact identity audit, each candidate | 41 artifacts; 41/41 descriptor, build/staging, ramdisk/staging, and compiled allowlist checks |

The Task Manager regression's temporary compatibility copy used `[System.UInt64]` because the active PowerShell parser rejected `[ulong]`; the checked-in script was not changed. The desktop test used the installed x86 .NET 8.0.33 runtime because an x64 .NET 8 runtime was unavailable. Test/build artifact paths were isolated from the pre-existing untracked outputs.

Hosted tests and static object/link inspection are separate evidence from guest behavior. Actual NativeAOT cached interface dispatch execution, UEFI boot, application activation in the guest, and guest mount lease release remain untested.

## Offline ESP candidates

The production `build.ps1` was copied with its required support/data files into `out/amconv2g-20261101-r1r1-final/package-pipeline/`. The pipeline staged independently verified loader, kernel, and candidate ramdisk assets with bootloader, kernel conversion, and ramdisk build steps skipped; the supported assembly step and production managed-artifact audit ran. This isolated setup avoided changing the root ESP and retained each candidate separately. An output-only structural validator checked each exact file set, hashes, RDSK version, ELF properties, entry segment, and ESP tree hash.

Both candidate manifests report status `offline package ready for guest testing`, `guest_boot_claimed: false`, and diagnostic mode `AppRuntime`.

| Candidate | ESP directory | RDSK | Loader SHA-256 | Kernel SHA-256 | Ramdisk SHA-256 | ESP tree SHA-256 |
|---|---|---:|---|---|---|---|
| A | `out/amconv2g-20261101-r1r1-final/candidate-a/ESP` | v1 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `55F7952552A9F0E97B3072E60A824ABD4A3DB18C1EF0B71EA71B8EC65950C33E` | `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E` | `3683CB9949405B0D2E4E820F1D667D9BE40054B762E389D330D6F721C8ED6CDB` |
| B | `out/amconv2g-20261101-r1r1-final/candidate-b/ESP` | v2, explicitly selected | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `55F7952552A9F0E97B3072E60A824ABD4A3DB18C1EF0B71EA71B8EC65950C33E` | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` | `687A1670F9CEEA7437B586025A9A0A2997E0D528FCC287A7B2ACD2BC777F0909` |

Each ESP contains exactly `EFI/BOOT/BOOTX64.EFI`, `kernel.elf`, and `ramdisk.img`. The loader remains the previously accepted 49,664-byte image with SHA-256 `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9`. Candidate A's v1 ramdisk is 56,431,471 bytes; Candidate B's v2 ramdisk is 56,435,018 bytes. The production audit reported 41/41 matches for each candidate across the four identity/staging checks. The normal production default remains v1; Candidate B used the explicit v2 selection. The full manifest files are `candidate-a/package-manifest.json` and `candidate-b/package-manifest.json` alongside the ESP folders.

## Remaining guest assertions and recommended R2

No QEMU or QAC was used. The following still require an authorized guest acceptance phase:

- Firmware loading of `BOOTX64.EFI`, kernel ELF loading, transfer to `KMain`, and `AppRuntime` startup markers.
- Real shell/folder activation, navigation, factory/window ownership, instance termination, and mount lease behavior for both RDSK versions.
- Any actual UEFI execution of NativeAOT interface-dispatch behavior.
- Serial logs, screenshots, candidate boot outcome, and guest cleanup evidence.

AMCONV2G-R2 should perform the bounded guest acceptance matrix for these two immutable package candidates after the current QAC owner and authorization are verified. Keep RDSK v1 as the default and v2 explicitly selected. Compare v1/v2 serial traces and exercise real production activation and disposal paths; report any lifecycle path that cannot be reached as pending rather than passing it from hosted evidence.

## Local commits

- Implementation and regression: `5f285e71e7336559ddad7f78f174409a1158d3c7`.
- This report: committed locally as the next commit on `APP_MODEL`.
- Nothing was pushed.
