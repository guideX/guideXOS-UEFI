# AMCONV2G-R4R2 — Corrected Services and GUI Lifecycle Acceptance

| Field | Result |
|---|---|
| Outcome | `AMCONV2G_R4R2_BLOCKED` |
| Detailed guest status | `QAC_GUEST_PENDING` |
| Qualification date | 2026-11-04 |
| Branch | `APP_MODEL` |
| Starting HEAD | `a6ce2d2ac977c9b6585584827abf5ffbdbff0fd2` |
| Ending source HEAD | `a6ce2d2ac977c9b6585584827abf5ffbdbff0fd2` |

## Decision and authorization

No guest acceptance was performed. The supplied mission says it does not independently authorize QAC execution and requires explicit authorization for two sequential, independent reservations. No such authorization accompanied this request. Therefore no QAC coordinator request, reservation, QEMU launch, guest input, or shutdown operation was made. Candidate A and Candidate B remain unrun in R4R2.

The mission requires checking live QAC status and reservations after authorization. Since that authorization is absent, the live coordinator was not queried and its current state is unknown. No reservation IDs exist for this run. No reservation or guest was created by this execution, so there was no owned QEMU process or reservation to clean up; this does not assert that the workstation has no unrelated QEMU process or reservation.

The prior R4 failure remains historical evidence and is not overwritten or reclassified. Candidate A's old AppRuntime services output recorded `APP_RUNTIME_SERVICES_RESULT=FAIL`, `APP_RUNTIME_NOTEPAD_SERVICE_DIAGNOSTIC=FAIL`, and `APP_RUNTIME_PHASE9_RUNTIME_OK=0`. Its old Notepad markers included `APP_RUNTIME_NOTEPAD_SAVE_SUCCESS=FAIL` and `APP_RUNTIME_NOTEPAD_SAVE_CANCEL=FAIL`. R4R1 attributed those save assertions to a diagnostic assumption about persistence and prepared corrected packages, but those corrected diagnostics have not been observed in a guest.

## Checkout and preservation

- The checkout is `D:\dev\guideXOSUEFI_APP_MODEL`, branch `APP_MODEL`, at the requested starting HEAD `a6ce2d2ac977c9b6585584827abf5ffbdbff0fd2`.
- The only initial worktree entries were pre-existing untracked test output directories: `Tests/AMCONV2DFatInspectionTests/{bin,obj}`, `Tests/AMCONV2ERdskInspectionTests/{bin,obj}`, and `Tests/AMCONV2FRdskV2Tests/{bin,obj}`. They were preserved.
- No source, candidate package, or test output was changed. The only intended change in this phase is this results report. The report is committed locally; no push was made.
- No build or test suite was run. The existing candidate package validator was run in `--check-only` mode to verify staged package identity and manifest agreement.

## Candidate package verification

The exact R4R1 staged candidates under `out/amconv2g-r4r1-20261104/packages-final/` were checked without rebuilding or writing their manifests. `Tools/AMCONV2G/validate_candidate_packages.py --check-only` passed for both candidates and confirmed the manifest file set, staged hashes, ELF structure, loader identity, RDSK version, and ESP tree hash.

| Identity | Candidate A | Candidate B |
|---|---|---|
| RDSK version | v1 | v2 |
| Accepted kernel ELF SHA-256 | `24FA8C323334614FA4800643575BEE1F77707AB2BCEB87BBD46C2EA62BD2BB9F` | `24FA8C323334614FA4800643575BEE1F77707AB2BCEB87BBD46C2EA62BD2BB9F` |
| RDSK image SHA-256 | `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E` | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` |
| ESP tree SHA-256 | `5E3192940FA9EC7CE5E63F6580D9D773F7D967116E9AC1AEE0E1C456D26A6A35` | `0FCB88C7DD142F195433156110476943500EF2C6BF3D9DE437FD6CA85AFBE4F4` |
| Manifest | `candidate-a/package-manifest.json` | `candidate-b/package-manifest.json` |
| Manifest diagnostic mode | `AppRuntime` | `AppRuntime` |

Both manifests identify the same accepted loader hash, `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9`, and report `guest_boot_claimed: false`. The project configuration maps `UefiDiagnosticMode=AppRuntime` to `UEFI_DIAGNOSTIC_APP_RUNTIME`. No package was silently rebuilt or modified.

## Guest acceptance results

| Requested evidence | Candidate A | Candidate B |
|---|---|---|
| Firmware and loader execution | Not run | Not run |
| Kernel, AppRuntime, desktop, and GUI readiness | Not run | Not run |
| Factory, application-instance, and services self-tests | Not run | Not run |
| Corrected Notepad SaveFile selection/cancel/persistence diagnostics | Not run | Not run |
| Computer Files drive chooser and actual Open Root input | Not run | Not run |
| Typed `gxos.shell.root` request and shell dispatch | Not run | Not run |
| Folder mount lease and directory inspection | Not run | Not run |
| Production factory, instance registration, and visible Root window | Not run | Not run |
| Repeated Root, independent windows, close, and termination | Not run | Not run |
| Error window dismissal on the real hide transition | Not run | Not run |
| QMP shutdown and QAC reservation release | Not run | Not run |

No guest assertions were executed in R4R2. There are no R4R2 serial traces, screenshots, QMP captures, guest marker values, or reservation snapshots. The historical R4 evidence remains under `out/amconv2g-20261104-r4-acceptance/` and describes the earlier, uncorrected Candidate A run; it is not R4R2 evidence.

### Corrected services classification

The corrected source emits `NOTEPAD_SAVE_SELECTION=PASS|FAIL`, `NOTEPAD_SAVE_CANCEL=PASS|FAIL`, and `NOTEPAD_SAVE_PERSISTENCE=NOT_TESTED` under the AppRuntime diagnostic build. These are source-defined marker forms, not R4R2 observations. Accordingly, R4R2 has no result for whether selection passed, whether cancellation behaved correctly, whether another services assertion failed, or whether the diagnostic avoided claiming write success at runtime. Persistence is classified as `NOT_TESTED` by the corrected diagnostic source; no guest emission was captured here.

The missing-path and `gxos.shell.this-system` rejection cases are present in the existing typed Root service diagnostic coverage described by the R3 report, but neither scenario was executed in an R4R2 guest. The source also ties `ERROR_WINDOW_DISMISSED=1` to `MessageBox.OnSetVisible(false)` for the bounded open-error path. No real dismissal event was generated or observed in this phase.

### Production Computer Files lifecycle

No guest input focus was established and no mouse or keyboard action was attempted. Thus input delivery was not found to fail; it is unassessed. The ordinary drive chooser, Open Root click, actual GUI action receipt, `gxos.shell.root` submission with `/`, shell dispatch, retained mount inspection, existing Computer Files factory, instance registration, and visible usable Root window all remain unverified.

No repeated Root activation, two-instance ownership, independent window operation, close behavior, final-window termination, lease release, stale-owner check, or double-disposal check was performed. No diagnostic-only factory invocation was substituted for the GUI route.

### RDSK comparison

The verified packages differ in their RDSK image/version and resulting ESP tree hashes: Candidate A stages v1 and Candidate B stages v2. Both stage the same accepted kernel and loader and both select the AppRuntime diagnostic configuration. No UEFI recognition, integrity validation, directory listing, navigation, or lifecycle difference was observed because neither candidate ran. No persistence write was attempted.

## QAC cleanup and evidence

| Item | R4R2 result |
|---|---|
| Candidate A reservation ID | None; no reservation requested |
| Candidate B reservation ID | None; no reservation requested |
| QEMU child ownership / termination | No child launched by this execution |
| Reservation release / coordinator reconciliation | Not applicable to this execution; live state not queried |
| Serial, screenshot, QMP evidence | None created |
| Package validation evidence | Check-only validator output: both candidates passed with the expected kernel and ESP tree hashes |

The R4R1 documents describe the supported next-run pattern: two separate default-mode, exclusive, one-child reservations, with the first child and reservation fully verified released before acquiring a fresh reservation for Candidate B. Do not use retained-child mode or `finishChild`. Before launching anything, obtain explicit authorization covering both sequential reservations, then check the live coordinator and existing reservations. Stop if ownership, admission, shutdown, or release behavior differs from the documented procedure.

## Remaining work and next phase

R4R2 is blocked at the explicit authorization gate. After authorization, perform the two-candidate guest acceptance run using these unchanged packages, preserve separate evidence for each candidate, and stop before Candidate B if Candidate A cleanup cannot be verified. If Candidate A fails before basic boot and desktop readiness, preserve that evidence and assess whether a separate Candidate B run can provide meaningful information before proceeding.

Keep the existing scope boundaries: no Server filesystem mappings, production mount replacement, UTF-8 RDSK support, storage writes, new input drivers, additional GUI features, or broad NativeAOT changes. Any genuine guest defect must retain its failing artifact and evidence; a repair must be separately built and revalidated.
