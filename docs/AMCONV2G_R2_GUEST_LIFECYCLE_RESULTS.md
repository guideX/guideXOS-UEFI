# AMCONV2G-R2 Guest Lifecycle Results

**Outcome C — `AMCONV2G_R2_QAC_OR_BOOT_BLOCKED`**  
**Detailed status: `QAC_GUEST_PENDING`**  
**Qualification date:** 2026-11-02  
**Branch:** `APP_MODEL`  
**Starting HEAD:** `1a77c120c3f1c482c93eae40ffa3aeb314dfb9cb`  
**Ending source HEAD before this report:** `1a77c120c3f1c482c93eae40ffa3aeb314dfb9cb`

## Decision

No guest was launched. The current QAC coordinator reports that its acceptance lane is available, but this task has no separate explicit APP_MODEL guest-testing authorization. The task prompt expressly says it does not grant that authorization. No reservation was requested, and no QEMU process was started or touched. Guest execution therefore remains `QAC_GUEST_PENDING`.

Offline checks confirm that the two retained ESP candidates still match their accepted identities and are structurally boot-ready. They do not prove firmware handoff, kernel startup, managed runtime initialization, filesystem behavior in the guest, or application lifecycle behavior.

The source review found an additional production reachability boundary: the ordinary Computer Files UI is present, but no current production UI caller creates the typed C# Root folder request. The existing Root sidebar entries return to the drive chooser. The typed shell service path and factory binding exist, but the only `ForShellFolder` call site is in AppModel self-tests. Mount replacement and disposal also lack a production call path. These cases remain unverified and must not be inferred from hosted tests or desktop rendering.

## Checkout and preservation

- Required branch and starting HEAD both matched.
- At start, the only worktree entries were pre-existing untracked `bin/` and `obj/` directories under `Tests/AMCONV2DFatInspectionTests`, `Tests/AMCONV2ERdskInspectionTests`, and `Tests/AMCONV2FRdskV2Tests`. They were preserved.
- No source changes, builds, hosted test runs, branch changes, resets, merges, rebases, cherry-picks, or pushes occurred in this phase.
- Accepted ESP candidates and manifests were read only. The per-file and tree hashes below were recomputed without rewriting the manifests.

## QAC authorization and admission

Read the current QAC2 operating guidance in `D:\dev\guideXOS_QAC\README.md` and `D:\dev\guideXOS_QAC\docs\QAC_SHARED_QEMU_COORDINATION_PHASE4R2.md`. Read-only `status` and `list` requests to the live local coordinator returned:

- Coordinator: `guideXOS QAC 2.0.0`, PID `24548`, healthy.
- QAC-managed sessions: `0`; active reservations: `0`; queued: `0`; blocked: `0`.
- QEMU census: `0` processes, `0` foreign; no census warnings; no recovery state.
- Current exclusive reservation owner: none.
- APP_MODEL authorization: none present in the current task/session. QAC availability is not authorization.

The current QAC procedure admits acceptance work exclusively. Its managed-child path is `qac run --project APP_MODEL --mode acceptance -- <direct qemu-system-*.exe> <arguments>`. QAC requires a direct QEMU child, records and verifies its PID, process start time, and executable identity, and retains the reservation until child exit is verified. A multi-boot launcher must use the documented retained-acceptance workflow under the same owner process. The launcher must bind any loopback QMP endpoint to that verified child and use QMP for bounded graceful shutdown. QAC does not stop foreign QEMU processes.

No QAC request, reservation, QMP endpoint, QEMU process, or cleanup action was created in this task. There was nothing to release or clean up. Existing direct-launch scripts such as `run_uefi_validation.ps1` are not a substitute for QAC admission and, as checked in, build/use the root ESP rather than these immutable candidate directories.

## Accepted candidate identities

Both ESPs contain exactly `EFI/BOOT/BOOTX64.EFI`, `kernel.elf`, and `ramdisk.img`. Current hashes match the accepted manifests and expected package identities.

| Candidate | RDSK | Loader SHA-256 | Kernel SHA-256 | Ramdisk SHA-256 | ESP tree SHA-256 |
|---|---:|---|---|---|---|
| A | v1 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `55F7952552A9F0E97B3072E60A824ABD4A3DB18C1EF0B71EA71B8EC65950C33E` | `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E` | `3683CB9949405B0D2E4E820F1D667D9BE40054B762E389D330D6F721C8ED6CDB` |
| B | v2, explicit candidate | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `55F7952552A9F0E97B3072E60A824ABD4A3DB18C1EF0B71EA71B8EC65950C33E` | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` | `687A1670F9CEEA7437B586025A9A0A2997E0D528FCC287A7B2ACD2BC777F0909` |

The current package manifests are `out/amconv2g-20261101-r1r1-final/candidate-a/package-manifest.json` and `candidate-b/package-manifest.json`. Each reports `offline package ready for guest testing`, `guest_boot_claimed: false`, and diagnostic mode `AppRuntime`.

## Offline boot compatibility review

- Both package paths contain the expected UEFI fallback loader at `EFI/BOOT/BOOTX64.EFI`. Loader source opens root-level `kernel.elf` and `ramdisk.img`.
- Recomputed loader, kernel, and ramdisk hashes match both manifests. Candidate A's header is `RDSK` v1; Candidate B's is `RDSK` v2.
- Both current kernels independently parse as ELF64, little-endian, `ET_EXEC`, `EM_X86_64`, with eight bounded `PT_LOAD` segments. Entry `0x1004DD7C` lies in an executable load segment; neither `PT_DYNAMIC` nor `PT_INTERP` is present.
- The loader allocates the ELF span, copies each load segment relative to the minimum load virtual address, establishes the kernel virtual-to-physical mapping, identity-maps the physical image span, and enters through the computed physical entry. The linker uses `/fixed /base:0x10000000`; the PE has no relocation directory. `LNK4281` says this base is undesirable for x64 ASLR optimization. Source review shows the converter preserves the linked virtual addresses and the loader explicitly maps that virtual range to the allocated physical base, so the warning alone does not show a boot incompatibility. Firmware execution remains unverified.
- The production package manifest selects `AppRuntime`; `guideXOS.csproj` defines `UEFI_DIAGNOSTIC_APP_RUNTIME` for that mode. The loader initializes COM1 and emits boot serial output; AppRuntime breadcrumbs use the kernel serial path. `SerialDebugMarker` in `native_stubs.asm` is currently a no-op, so the expected raw `K!` marker is not an available acceptance signal. Use `[KMAIN] INITIALIZE` and subsequent breadcrumbs to identify kernel progress.

## Production UI and lifecycle reachability

- Ordinary Computer Files launch is wired through the Start menu's Computer Files row to `Desktop.LaunchApplication`, the real factory, registry, and window manager. A normal new Computer Files window exposes the drive chooser.
- The shell registry identifies `Root` as `gxos.shell.root`, a filesystem location. On the desktop, clicking Root in Home mode changes to the desktop filesystem view. In Computer Files, the Desktop and Computer Files-root sidebar entries resolve Root but only return to the drive chooser.
- `ApplicationShellService` has a typed folder path and `ComputerFilesApplicationFactory` handles typed folder requests. A source scan found no production UI call to `ApplicationShellOpenRequest.ForShellFolder`; the call site is confined to AppModel self-tests. Therefore the required UI action “open C# Root through the typed shell route” cannot be completed through the present production UI. No fake test-only launch pipeline was added.
- `FileSystemMountLifetime.SetActiveFileSystem` changes the active reference; `RequestDisposal` is a separate operation. No production call site for either operation was found in the current source tree. Existing window-retained leases and ordinary close behavior can be observed in guest diagnostics if reached, but replacement with outstanding leases and deferred disposal are not currently reachable through a supported production interaction.
- `UEFI_DIAGNOSTIC_APP_RUNTIME` contains serial observations for shell requests, folder inspection, factory creation, window attachment/ownership, instance termination, and mount lease counts. These are production components behind compile-time diagnostics; no such marker was observed in a guest during this phase.

## Guest acceptance matrix

| Assertion | Result |
|---|---|
| Candidate A UEFI firmware, loader, ELF, KMain, and managed startup | Pending; no guest run |
| Candidate B UEFI firmware, loader, ELF, KMain, and managed startup | Pending; no guest run |
| Candidate A RDSK v1 guest parsing and filesystem behavior | Pending; no guest run |
| Candidate B RDSK v2 guest parsing, explicit/empty directories, enumeration, and file contents | Pending; no guest run |
| Desktop, registry, production Computer Files launch, window ownership and close | Pending; no guest run |
| Typed C# Root activation through production UI | Not reachable in current UI; pending product route |
| Two independent windows, retained mount use, and instance cleanup | Pending; no guest run |
| Mount replacement, deferred disposal, and final lease release | No supported production replacement/disposal route found; pending |
| Guest keyboard/mouse focus and input delivery | Not attempted; no guest run |
| Serial trace, screenshots, graceful shutdown, QAC release | None captured; no guest run or reservation |

Actual guest pass/fail assertions: **0 performed**. No boot failure, GUI failure, input failure, filesystem failure, or application-model failure is claimed.

## Hosted evidence and regressions

No hosted regressions were rerun because no source repair was required or made. Prior accepted evidence remains separate: RDSK v1 inspection 32 assertions; RDSK v2 reader 58 assertions; folder/mount suite 71 assertions; FAT inspection 11 assertions; packaging tests 4/4; desktop interaction 42 assertions; and managed-artifact identity audit 41/41 for each candidate. These do not constitute guest acceptance. The abstract-base-class folder-probe repair has hosted evidence, but without guest execution it provides no UEFI evidence for that path and does not establish full NativeAOT cached interface-dispatch support.

## Evidence, cleanup, and next phase

- R2 serial trace and screenshot locations: none created.
- Source changes/builds/hosted tests: none.
- QAC admission and cleanup: not applicable; no reservation or QEMU process was created.
- Recommended next phase: obtain explicit APP_MODEL guest-testing authorization, then use the current QAC exclusive managed-child workflow with isolated per-run copies of the accepted ESPs. Before claiming the full requested UI lifecycle, provide a supported production interaction that submits the typed `gxos.shell.root` folder request. Keep mount replacement/deferred disposal pending until a genuine production owner path can safely request disposal while a window retains the old mount. Preserve RDSK v1 as the default and select v2 only for Candidate B.

This results report is intended as a separate local documentation commit after the unchanged source HEAD above. Nothing was pushed.
