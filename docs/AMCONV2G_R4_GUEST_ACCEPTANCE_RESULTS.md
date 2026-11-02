# AMCONV2G-R4 — QAC Guest Acceptance Results

**Outcome C — `AMCONV2G_R4_QAC_OR_EXECUTION_BLOCKED`**
**Detailed status: `QAC_GUEST_PENDING`**
**Qualification date:** 2026-11-02
**Branch:** `APP_MODEL`

## Decision

No guest was admitted or launched. This conversation contained no separate, explicit authorization for APP_MODEL QAC guest testing, and the task instructions explicitly state that the request itself does not grant authorization. QAC admission, live coordinator queries, QEMU census, and guest interaction were therefore not attempted. The detailed guest status remains `QAC_GUEST_PENDING`.

Both preserved R3 offline ESP candidates were revalidated read-only against their existing manifests. Their loader, kernel, ramdisk, and ESP tree identities match the accepted values. This confirms offline package integrity only; it does not establish firmware boot, runtime startup, GUI behavior, application lifecycle, or cleanup in a guest.

## Revision identity and repository state

- Starting branch: `APP_MODEL`.
- Starting HEAD: `0a3dd6956009ff7ce600fae1debb3e5ce225bdd8`, matching the requested baseline.
- Ending tested source HEAD before this report-only commit: `0a3dd6956009ff7ce600fae1debb3e5ce225bdd8`; no source, test, or tooling changes were made.
- This report is recorded in a separate local documentation-only commit immediately after that source HEAD.
- The starting worktree contained only these untracked test output directories; they were preserved:
  - `Tests/AMCONV2DFatInspectionTests/bin/` and `obj/`
  - `Tests/AMCONV2ERdskInspectionTests/bin/` and `obj/`
  - `Tests/AMCONV2FRdskV2Tests/bin/` and `obj/`
- The four requested AMCONV2G reports/procedures were read. The AMCONV2G-R3 report already records the production Open Root source route and the pending guest assertions.
- QAC operating guidance was read from the QAC README and Phase 4R2 coordination procedure in the separate QAC checkout, which remained read-only.

## Authorization, admission, and cleanup

No separate APP_MODEL guest-test authorization was present in the current session. The QAC coordinator's current health, reservations, running QEMU processes, and owner were not queried because the authorization gate was not crossed. Historical QAC observations in earlier reports were not treated as current state.

No reservation was requested, no QEMU command was launched, no guest was interacted with, and no reservation or process was created by this run. There is no reservation ID, QEMU PID, serial log, screenshot, or cleanup action to report. No QAC resource was held by this run, so no release was required.

The documented admission path is an exclusive acceptance reservation with a direct `qemu-system-*.exe` child. Sequential candidate boots must remain under the same verified owner using the documented retained-acceptance workflow; graceful shutdown is through a QMP endpoint bound to the verified child. These are procedure notes only, not evidence of current QAC state or admission.

## Candidate package verification

The existing manifests under `out/amconv2g-20261102-r3-final/` report `offline package ready for guest testing`, `guest_boot_claimed: false`, and diagnostic mode `AppRuntime`. The read-only command below checked each staged ESP against its manifest, exact file set, accepted loader and ramdisk hashes, and ELF constraints; it did not rewrite either manifest or package:

```powershell
python Tools/AMCONV2G/validate_candidate_packages.py `
  --output-root out/amconv2g-20261102-r3-final `
  --audit-a out/amconv2g-20261102-r3-final/package-pipeline-seed/out/managed-artifacts-rdsk-candidate-a-v1.csv `
  --audit-b out/amconv2g-20261102-r3-final/package-pipeline-seed/out/managed-artifacts-rdsk-candidate-b-v2.csv `
  --check-only
```

Both candidates passed. Each ESP contains exactly `EFI/BOOT/BOOTX64.EFI`, `kernel.elf`, and `ramdisk.img`. The kernel is ELF64 little-endian `ET_EXEC`, `EM_X86_64`, has eight load segments, an executable entry at `0x1004DE14`, and no dynamic or interpreter segments.

| Candidate | RDSK | Loader SHA-256 | Kernel SHA-256 | Ramdisk SHA-256 | ESP tree SHA-256 |
|---|---:|---|---|---|---|
| A | v1 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `862B3A95420E1C02411061610487664100908A4F2FCB2311B633586A99BD7DF1` | `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E` | `C4991782ABE5B6A65CF2700562041D4E0529CA26BAF6BB2CB817D1639082BB79` |
| B | v2 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `862B3A95420E1C02411061610487664100908A4F2FCB2311B633586A99BD7DF1` | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` | `CBAC27CF8A23554B9FA86D8A6879F5C155D2A4603A1FFD4A4DB140D0EE61CE3B` |

Both packages declare `AppRuntime`; `guideXOS/guideXOS.csproj` maps that diagnostic mode to `UEFI_DIAGNOSTIC_APP_RUNTIME`. The accepted kernel hash is `862B3A95420E1C02411061610487664100908A4F2FCB2311B633586A99BD7DF1`. The prior reports document a Q35 8.2 / OVMF code plus writable variables profile, 1024 MiB RAM, serial capture, and no-reboot. That profile was not executed or independently verified against current firmware files in this run. The existing direct-launch validation script builds/uses the root ESP and is not a substitute for a QAC-managed launch of these immutable candidates.

## Guest acceptance results

No guest assertions were performed. Pass/fail count for guest assertions: **0 performed**. Candidate A was not launched, so Candidate B was not launched either.

| Acceptance area | Result |
|---|---|
| QAC authorization, admission, and reservation ownership | `QAC_GUEST_PENDING`; not attempted |
| Firmware start, EFI loader, ELF loading, kernel entry, NativeAOT/runtime initialization | Not observed; no guest run |
| Application services, desktop, GUI responsiveness, Computer Files availability | Not observed; no guest run |
| Guest keyboard/mouse focus or event delivery | Not attempted; no guest run |
| Ordinary drive chooser | Not attempted; no guest run |
| Production Open Root click and typed shell request | Not attempted; no guest run |
| Shell resolution, filesystem lease, root inspection, production factory, window activation | Not observed; no guest run |
| Instance identity, separate windows, repeated Root activation, close behavior, registry cleanup | Not observed; no guest run |
| Mount lease release, stale ownership, double disposal, restart behavior | Not observed; no guest run |
| RDSK v1/v2 guest parsing, enumeration, and navigation comparison | Not observed; no guest run |
| Missing path, file-as-directory, unsupported Server alias, typed error presentation | Not attempted; no guest run |
| Guest mount replacement and deferred disposal | Deferred; no supported production replacement/disposal path was identified in the prior R2/R3 review |
| Serial trace, screenshots, shutdown, QAC process/reservation release | None; no guest or reservation was created |

R3 source review documents that the real Computer Files **Open Root** row submits `ApplicationShellOpenRequest.ForShellFolder("gxos.shell.root", "/")` through the shell service, with the existing factory and `MultiInstance` registry handling the request. The corresponding click uses the existing PS/2 mouse path through `MouseEventDispatcher` and `Control.MouseButtons`. Prior Desktop P3 evidence documents QMP pointer input on this source family. None of these guest transitions or inputs were exercised in R4. The eventual GUI run must click inside the QEMU display first to establish guest focus as specified in the acceptance procedure. If event delivery fails, classify that separately as `INPUT_OR_UI_BLOCKED`.

## Source changes, commits, and remaining work

No source repair, rebuild, hosted regression, or test suite was run. The only intended repository artifact from this attempt is this acceptance results report. Candidate packages and pre-existing untracked test outputs were preserved. Nothing was pushed.

The blocker is the missing separate APP_MODEL guest-testing authorization. After authorization is provided, the operator must query current QAC health and ownership, establish the exclusive retained reservation, verify the chosen candidate and complete launch preflight, and run Candidate A before Candidate B. No current QAC availability or owner is asserted here.

AMCONV2H should remain deferred until AMCONV2G has meaningful guest lifecycle evidence. Then choose a focused next scope, such as RDSK v2 UTF-8 path support or Server/C# namespace convergence. The current strict ASCII pathname contract remains in force; this report makes no UTF-8 support claim.
