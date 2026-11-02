# AMCONV2G Guest Lifecycle Acceptance

## Current R3 acceptance status and procedure

**Current status:** `AMCONV2G_R3_UI_AND_PACKAGES_READY`; guest execution remains `QAC_GUEST_PENDING`.

R3 adds the production **Open Root** row to the existing Computer Files left navigation pane. The click submits `ApplicationShellOpenRequest.ForShellFolder("gxos.shell.root", "/")` through the current application shell service context. The updated Candidate A (RDSK v1) and Candidate B (RDSK v2) ESPs and full hashes are recorded in [the R3 report](AMCONV2G_R3_ROOT_UI_REACHABILITY.md). Both are offline-ready only; no guest boot or UI result is claimed.

Use this sequence in the next separately authorized QAC acceptance run:

1. Boot Candidate A through QAC and wait for desktop startup.
2. Open Start → **Computer Files** and confirm the ordinary drive chooser remains available.
3. Click **Open Root** in the Computer Files left pane. Confirm a root folder window or the existing typed error window.
4. Verify serial order and values for `FILES_ROOT_ACTION=begin`, `SHELL_FOLDER_REQUEST`, folder bind/inspection, `FACTORY_LAUNCH`, `FILES_FOLDER_WINDOW`, and `FILES_ROOT_ACTION=result`. Confirm the result identifies `gxos.builtin.files` and a new instance handle.
5. Navigate into a populated directory, return with **Up Level**, close the window, and verify instance termination plus mount-release markers.
6. Boot Candidate B and repeat ordinary Computer Files launch and **Open Root**.
7. Open `WithFiles` and confirm `inside.txt` and `Child`; open `Empty` and confirm it is listed with no entries; also inspect `Nested/DeepEmpty` and return to the parent.
8. Open a second Root window, close one while the other remains usable, then close the other and verify normal instance and lease release.
9. Shut down through QAC and verify guest exit and QAC cleanup.

The mouse path is the existing PS/2 → `MouseEventDispatcher` → `Control.MouseButtons` path; the new action needs only a left click. No R3 GUI event or service/factory/window lifecycle was executed. The full acceptance procedure must capture actual guest serial output and keep each assertion pending until it is observed.

Mount replacement remains deferred: no supported production operation was found to replace the active filesystem and request disposal. Hosted mount-lifetime replacement tests do not make that scenario guest-reachable.

The remainder of this file is the dated R1 acceptance record. Its package-readiness and UI-reachability status describes the earlier qualification point; R3 results and the current procedure above supersede it for the next run.

**Outcome C — `AMCONV2G_QAC_OR_EXECUTION_BLOCKED`**

**Detailed status: `QAC_GUEST_PENDING`**

**Qualification date: 2026-11-01**

## Decision

Hosted readiness and two full RDSK candidates are prepared. No guest was admitted or launched. QAC showed an active exclusive acceptance reservation owned by the `guideXOSServer` workstream, and APP_MODEL had no authorization or reservation. The current `ESP` directory also lacks `EFI/BOOT/BOOTX64.EFI` and `kernel.elf`, so neither RDSK candidate is a bootable package yet.

The v1 and v2 results below are hosted, source, and image-audit evidence only. They do not establish UEFI boot, Computer Files GUI behavior, application factory or instance lifecycle, or guest mount cleanup.

## Revision and repository protection

- Branch: `APP_MODEL`.
- Starting HEAD: `cec3a99b0b702720fcb46617c625feb18bba33c7` (the requested baseline).
- Ending code HEAD: `3ba2a104b1096f8c20c4460b50b348754bfc662b` (`feat: prepare AMCONV2G guest lifecycle acceptance`). The acceptance report is committed immediately after this code commit.
- At start, `APP_MODEL` was one local commit ahead of `origin/APP_MODEL`. No branch switch, reset, rebase, merge, or push occurred.
- The pre-existing untracked test `bin/` and `obj/` directories were preserved. New .NET test builds used isolated artifact roots under `out/amconv2g-20261101-r1/hosted-artifacts/`.
- Work was confined to this checkout. The QAC checkout was inspected read-only and remained on `main` with no working-tree changes. No other development checkout was changed.
- Generated images, logs, audit CSVs, and isolated build outputs remain under the ignored `out/amconv2g-20261101-r1/` directory; they are not committed.

## QAC authorization and admission

Read-only `status` and `list` queries to the existing QAC CLI returned:

- Coordinator: guideXOS QAC 2.0.0, PID `24548`, online.
- Active reservation: `d20a459368a44dbaae4b362a9a0d99b1`, project `guideXOSServer`, mode `acceptance`, state `Running`.
- Verified owner PID: `10140`; managed QEMU child PID: `15340`.
- QAC census: one managed QEMU, zero foreign QEMU processes.
- APP_MODEL authorization/grant: none.

The exclusive reservation belongs to another workstream. No QAC request was queued or made, no guest was interacted with, and no QEMU process was launched, stopped, or modified by this task. Guest acceptance remains `QAC_GUEST_PENDING` until APP_MODEL has explicit authorization and the lane is available.

## Candidate images and boot readiness

All candidate paths below are relative to the repository root and exist under `out/amconv2g-20261101-r1/`.

| Candidate | Image | Bytes | SHA-256 | Result |
|---|---|---:|---|---|
| A, RDSK v1 | `candidate-a-full-rdsk-v1.img` | 56,431,471 | `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E` | Generated with the production builder from a copy of current `ramdisk_src` plus the bounded fixture; v1 format and hosted compatibility verified. |
| B, RDSK v2 | `candidate-b-full-rdsk-v2.img` | 56,435,018 | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` | Production v2 builder and parser verified; fixture and managed Native payloads included. |
| B repeat | `candidate-b-full-repeat-rdsk-v2.img` | 56,435,018 | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` | Byte-identical repeat build confirms deterministic generation. |

Candidate B's production-validated header has magic `RDSK`, version 2, 64-byte header, zero flags, 622 records (22 directories including root and 600 files), records at byte 64 with a 27,126-byte table, and payload data at byte 27,190. The image length is exactly 56,435,018 bytes. Stored and recomputed CRC32 are both `5305C48E`. The production managed-artifact parser accepted the full record table and all 600 file entries.

The bounded fixture uses only ASCII names. It includes root files `TopFile.bin`, `Zero.bin`, and `Note`; explicit empty directories `Empty` and `Nested/DeepEmpty`; `WithFiles/inside.bin`; and `WithFiles/Child/leaf.txt`. `TopFile.bin` contains bytes `00 01 02 FF 0D`, `Zero.bin` is empty, and the nested files contain known fixture text. The full candidates also contain current production assets.

The tracked release `ramdisk.img` was not changed; its SHA-256 remains `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8`. The full UEFI packaging step was not run. `ESP` contains no current bootloader or staged kernel, so the files above are RDSK payload candidates, not bootable guest images. No guest result is claimed for Candidate A or B.

### Opt-in packaging and identity validation

`build.ps1` keeps RDSK v1 as the default. `-RamdiskVersion 2` selects a separate `ramdisk-v2.img` by default, and v2 is rejected if directed to the normal `ramdisk.img` path. An explicit image path is supported. The exact selected image is audited before it is staged, including an expected-version check so a skipped image build cannot package a v1/v2 mismatch. No identity-validation bypass was added.

The production managed-artifact audit used the read-only staged Native source at `E:\GUIDEXOSUEFI_APP_MODEL_BUILD1E\runs\managed-identity-input\Native` and the existing compiled identity source. Both candidates passed:

| Check | Candidate A v1 | Candidate B v2 |
|---|---:|---:|
| Managed artifacts | 41 | 41 |
| Descriptor agreement | 41/41 | 41/41 |
| Build/staging agreement | 41/41 | 41/41 |
| Ramdisk/staging agreement | 41/41 | 41/41 |
| Compiled allowlist agreement | 41/41 | 41/41 |

The compiled identity source SHA-256 is `D6DDF03058FA0690EF62F62FFFF016723C1514EB747FDF3D1D6AECD23CA682DD`. Audit CSVs are `managed-candidate-a-full-v1.csv` and `managed-candidate-b-full-v2.csv` in the artifact directory. The packaging integration passed source validation, parser checks, PowerShell syntax parsing, and direct audits; end-to-end ESP assembly remains unqualified.

## Hosted and source evidence

These are hosted/source checks, not guest acceptance.

| Area | Result |
|---|---:|
| AMCONV1 source contract | 9/9 |
| AMCONV2B/C/D source contract | 25/25 |
| AMCONV2F source contract | 9/9 |
| RDSK v2 generator assertions and rejection cases | 12/12 |
| RDSK v2 production reader and folder binding | 58/58, 0 failures |
| Folder target and mount lifetime | 68/68, 0 failures |
| FAT inspection | 11/11, 0 failures |
| RDSK v1 inspection and binding | 32/32, 0 failures |
| Desktop interaction | 42/42 |
| AMCONV2G managed packaging tests | 4/4 |
| Phase33 wire validation | Pass; request 2,084 bytes, response 16 bytes |
| Task Manager Memory Details regression | Pass using an output-only PowerShell compatibility copy |

The v2 runtime suite exercised the full Candidate B image, v1 compatibility, explicit and empty directories, known file contents, missing and file-as-folder targets, malformed and checksum-corrupt images, rejected path forms, and deferred mount disposal after replacement. The hosted tests do not instantiate the real guest desktop lifecycle.

The checked-in Task Manager regression script uses `[ulong]`, which the active PowerShell host did not resolve as a type accelerator and rejected before running assertions. An output-only copy replaced it with `[System.UInt64]` and passed; the checked-in script was not changed. The adjusted runner is retained under the artifact directory.

Release builds used isolated artifact roots:

- Release/AppModel: **0 errors, 135 warnings**, matching the established baseline.
- Release/AppRuntime diagnostic variant: **0 errors, 139 warnings**. The four-warning increase is four additional `CS0162` unreachable-code diagnostics in the existing compile-time UEFI dispatch branches in `guideXOS/Program.cs`; the files changed for this phase add no new compiler warning category. AppRuntime is the diagnostic build intended to expose lifecycle breadcrumbs.

The build logs are `release-appmodel-isolated.log` and `release-appruntime-isolated.log` in the artifact directory.

## Diagnostic-only lifecycle instrumentation

Instrumentation is guarded by `UEFI_DIAGNOSTIC_APP_RUNTIME` and is absent from the ordinary build. It observes the production shell service, folder binding, factory, `ApplicationInstanceRegistry`, Computer Files window, and mount lease. The markers cover shell request/result and canonical path, mount acquisition and generation, folder inspection and reinspection, factory instance creation/reuse, window attachment and owner identity, initial folder path, instance termination and stale counts, and mount release before/after disposal.

The narrow changes are in `Kernel/FS/FileSystem.cs`, `Kernel/FS/FileSystemMountLifetime.cs`, `guideXOS/OS/ApplicationShellServices.cs`, `guideXOS/OS/ShellFolderMountBinding.cs`, `guideXOS/OS/ApplicationFactories.cs`, `guideXOS/OS/ApplicationInstance.cs`, and `guideXOS/DefaultApps/ComputerFiles.cs`. AppRuntime compilation passed. These markers have not been observed in a guest; no serial transcript, screenshot, factory transition, window ownership, instance termination, or guest lease release is claimed.

## Guest acceptance status

| Acceptance item | Status |
|---|---|
| Candidate A UEFI boot and v1 Computer Files flow | Pending QAC admission and bootable package |
| Candidate B UEFI boot and v2 filesystem flow | Pending QAC admission and bootable package |
| Real shell service and factory activation | Not guest-tested |
| Window ownership and application instance termination | Not guest-tested |
| Guest mount leases, replacement, and final release | Not guest-tested |
| Guest screenshots, serial trace, shutdown, and cleanup | None captured; no guest was launched |
| Hosted parser, mount, and negative-case evidence | Passed as listed above |

## Recommended next step

Resume AMCONV2G only after APP_MODEL receives explicit QAC authorization and the acceptance lane is free. Assemble two bootable ESP packages through `build.ps1`, verify their staged hashes and diagnostic mode, then execute the bounded v1/v2 lifecycle matrix under a verified QAC reservation. Keep guest observations separate from the hosted evidence in this report. Defer AMCONV2H until AMCONV2G has guest evidence; then select one focused direction, RDSK v2 UTF-8 support or Server/C# namespace convergence, rather than combining them.

## Local commits

- Implementation, tooling, and tests: `3ba2a104b1096f8c20c4460b50b348754bfc662b`.
- This report is committed locally after the implementation commit. Nothing was pushed.
