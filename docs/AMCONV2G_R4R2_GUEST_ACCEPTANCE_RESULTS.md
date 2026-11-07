# AMCONV2G-R4R2 — Corrected Services and Real GUI Lifecycle Acceptance

| Field | Result |
|---|---|
| Outcome | AMCONV2G_R4R2_GUEST_PARTIAL |
| Detailed guest status | Authorized guest execution; Candidate A acceptance passed its exercised cases, Candidate B stopped before AppRuntime/desktop readiness |
| Date | 2026-11-07 |
| Branch | APP_MODEL |
| Starting HEAD | a6ce2d2ac977c9b6585584827abf5ffbdbff0fd2 |
| Ending source HEAD during acceptance | 217f77bf1ff922c80f5223f4efdb397fe47db190 |

## Summary

The user explicitly authorized QEMU on 2026-11-07, covering the mission's two sequential candidate runs. Candidate A (RDSK v1) booted through the real UEFI path. Its corrected AppRuntime services diagnostic passed, including SaveFile selection and cancellation while accurately classifying persistence as NOT_TESTED. The production Computer Files chooser opened from Start, Open Root submitted gxos.shell.root with path /, and two separately owned Root windows were created, independently closed, and released their mount leases. One surviving Root window remained usable for populated-directory navigation.

Candidate B (RDSK v2) reached kernel startup, mounted the filesystem, initialized the window manager and desktop shell, and began managed PNG asset setup. Serial output then stopped at the first wallpaper load, Backgrounds/tronporche.png. The framebuffer remained solid teal through an observation period of more than four minutes; a QMP click caused no visible response. Candidate B never emitted AppRuntime readiness or a usable desktop. It was shut down cleanly through its owned QMP endpoint.

This is partial acceptance, not QAC_GUEST_PENDING: authorized guest execution produced substantial Candidate A evidence, while Candidate B's required runtime and navigation cases remain unverified.

## Checkout, scope, and preservation

- The checkout is D:/dev/guideXOSUEFI_APP_MODEL, branch APP_MODEL.
- The requested starting source HEAD was a6ce2d2ac977c9b6585584827abf5ffbdbff0fd2. The first report-only commit was 217f77bf1ff922c80f5223f4efdb397fe47db190; source and accepted packages remained unchanged through both guest runs.
- The earlier blocked report was committed locally before QEMU authorization. This report replaces that provisional status with the observed R4R2 results. No push was made.
- Pre-existing untracked test output directories Tests/AMCONV2DFatInspectionTests/{bin,obj}, Tests/AMCONV2ERdskInspectionTests/{bin,obj}, and Tests/AMCONV2FRdskV2Tests/{bin,obj} were preserved.
- No feature, package, or source change was made. No test suite or rebuild was run. The existing candidate-package validator was run in check-only mode.

## Authorization and QAC lifecycle

The mission required explicit authorization for two separate default-mode acceptance reservations. The user supplied that authorization before any successful guest launch. The live QAC coordinator was version 2.0.0 (PID 5860). Each actual guest was launched as the direct child registered by the same reservation-owner process. No retained-child option was requested, and finishChild was never called.

| Run / event | Reservation | Owner PID | QEMU PID | Outcome |
|---|---|---:|---:|---|
| Initial Candidate A request, no child | 88c06e95565340b7bc59a2cf01d9ea25 | 7276 | — | Granted, then released before prepareLaunch; no QEMU process was created |
| Candidate A actual guest | cfbe003e2d594bb28b2f1eb5e40e8e6c | 27608 | 30304 | QMP quit accepted; child exited 0; reservation Released |
| Existing other-workstream lane occupancy while preparing B | 5b982bfa75f14424b899629b04cd871f | 8824 | 19364 | Left untouched; waited until the lane cleared before requesting Candidate B |
| Candidate B actual guest | 585e37effd8e4246a6e7410198c5c023 | 16720 | 11064 | QMP quit accepted; child exited 0; reservation Released |

The first A request is disclosed because the QAC response omitted the default false retention field, and the temporary owner script initially treated that omission as unsafe. It was released without calling prepareLaunch or creating a guest. The QAC contract/source confirmed that omitted retention defaults to false. Candidate A was then launched under a fresh reservation, cfbe003e2d594bb28b2f1eb5e40e8e6c.

Before Candidate B's successful request, QAC showed an active guideXOSServer reservation. No Candidate B request or child was created while that lane was occupied. After the other workstream exited, a fresh preflight showed the lane clear and the independent Candidate B reservation was granted.

Both actual guests were shut down using QMP quit from the owner process. The exact registered child exited with code 0; QAC marked each exact reservation Released with reason “Registered child process exited.” Post-release status and list snapshots for each candidate show zero reservations, managed sessions, QEMU processes, queued or blocked reservations, foreign QEMU processes, census warnings, and recovery requirement. The child PIDs were absent after shutdown.

The first childless A reservation and the B lane wait are lifecycle notes, not guest launches or interference with another workstream. Evidence is in out/amconv2g-r4r2-20261107/qac-owner-candidate-a.log, qac-owner-candidate-b.log, candidate-a-post-release-{status,list}.json, candidate-b-post-release-{status,list}.json, and each reservation request/inspect/released JSON file.

## Accepted package verification

The two R4R1 packages were used without rebuilding or changing manifests. The validator was run with --check-only against the accepted package root before launch and after execution. The isolated ESP copies used for QEMU were also hashed and matched the manifest ESP-tree hashes.

| Identity | Candidate A | Candidate B |
|---|---|---|
| Diagnostic mode | AppRuntime | AppRuntime |
| RDSK version | v1 | v2 |
| Kernel ELF SHA-256 | 24FA8C323334614FA4800643575BEE1F77707AB2BCEB87BBD46C2EA62BD2BB9F | 24FA8C323334614FA4800643575BEE1F77707AB2BCEB87BBD46C2EA62BD2BB9F |
| RDSK image SHA-256 | B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E | 18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4 |
| ESP tree SHA-256 | 5E3192940FA9EC7CE5E63F6580D9D773F7D967116E9AC1AEE0E1C456D26A6A35 | 0FCB88C7DD142F195433156110476943500EF2C6BF3D9DE437FD6CA85AFBE4F4 |
| Accepted EFI loader SHA-256 | C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9 | C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9 |

The check-only validation passed for both accepted manifests. Both isolated runtime ESP trees matched their respective accepted hashes. The validator command was:

    python Tools/AMCONV2G/validate_candidate_packages.py --output-root out/amconv2g-r4r1-20261104/packages-final --audit-a out/amconv2g-r4r1-20261104/package-pipeline-final/out/managed-artifacts-rdsk-candidate-a-v1.csv --audit-b out/amconv2g-r4r1-20261104/package-pipeline-final/out/managed-artifacts-rdsk-candidate-b-v2.csv --check-only

## Candidate A — RDSK v1

### Boot and diagnostics

Firmware/UEFI Boot Manager loaded and started the hard-disk EFI loader; serial shows guideXOS UEFI Bootloader, then KMAIN INITIALIZE at line 158. RdskFS initialized and the serial log emitted FS mounted at lines 244 and 254. The desktop initialized at line 260. AppRuntime ran to completion, then dispatched to the continuous UEFI desktop at lines 1844–1847. DESKTOP_REAL_TEXT_RENDERED=1 and repeated CONTINUOUS_HEARTBEAT_GRAPHICS_VALID=1 markers confirm GUI readiness. The initial QMP screenshot, candidate-a/desktop-initial.png, shows the running desktop.

The boot-time AppRuntime probes emitted PASS for unknown load, unknown ID, unknown app alias, unknown extension, malformed path, and unknown shell request. These generic probes do not establish the separate gxos.shell.this-system Server-root-alias case.

The corrected Notepad and application-services diagnostics emitted:

- APP_RUNTIME_NOTEPAD_OPEN_SUCCESS=PASS
- APP_RUNTIME_NOTEPAD_OPEN_CANCEL=PASS
- APP_RUNTIME_NOTEPAD_SAVE_SELECTION=PASS
- APP_RUNTIME_NOTEPAD_SAVE_PERSISTENCE=NOT_TESTED
- APP_RUNTIME_NOTEPAD_SAVE_CANCEL=PASS
- APP_RUNTIME_NOTEPAD_CONFIRMATION=PASS
- APP_RUNTIME_NOTEPAD_CROSS_OWNER=PASS
- APP_RUNTIME_NOTEPAD_DIALOG_CLEANUP=PASS
- APP_RUNTIME_PHASE9_SAVE_DIALOG_RESULT=selected
- APP_RUNTIME_PHASE9_SAVE_PERSISTENCE=not_tested
- APP_RUNTIME_PHASE9_CONFIRMATION_RESULT=cancelled
- APP_RUNTIME_PHASE9_RUNTIME_OK=1
- APP_RUNTIME_SERVICES_NOTIFICATION=PASS
- APP_RUNTIME_SERVICES_SHARED_SETTINGS=PASS
- APP_RUNTIME_SERVICES_SNAPSHOT=PASS
- APP_RUNTIME_DISPLAY_OPTIONS_SERVICE_DIAGNOSTIC=PASS
- APP_RUNTIME_SERVICES_STALE_REJECTED=PASS
- APP_RUNTIME_SERVICES_CLEANUP=PASS
- APP_RUNTIME_SERVICES_STALE_CONTEXTS=0
- APP_RUNTIME_SERVICES_RESULT=PASS
- APP_RUNTIME_NOTEPAD_SERVICE_DIAGNOSTIC=PASS

Selection and cancellation both passed. Persistence remained explicitly NOT_TESTED; the diagnostic did not claim a successful storage write. No unsupported RDSK write was attempted. No other services assertion failed in the recorded services result.

The factory/instance diagnostic emitted APP_RUNTIME_LIFECYCLE_RUNTIME=calculator=1;notepad=1;consoleReuse=1;image=1;cleanup=1;result=PASS and APP_RUNTIME_LIFECYCLE_DIAGNOSTIC=PASS. It also emitted APP_RUNTIME_TASKBAR_GROUPING_DIAGNOSTIC=PASS and related cleanup markers. Those startup diagnostics are separate from the production Computer Files UI interaction below.

### Production Computer Files and Open Root

The user-facing QMP input path was used for actual desktop interaction. A brief host-window focus attempt did not increment the guest input counters; that input limitation was kept separate. QMP control then worked: the guest recorded mouse packets and button transitions with zero dropped packets, and the actual GUI actions produced the expected production markers.

Start opened the normal Start menu. Selecting the regular Computer Files entry created the chooser through the production path:

- APP_RUNTIME_FACTORY_LAUNCH=app=gxos.builtin.files;instance=instance-1-6;reused=0;created=1;windows=1
- The chooser screenshot shows Desktop, Computer Files, Open Root, and the Hard Disk tile.

Clicking the visible Open Root row produced the real GUI action and shell request:

- APP_RUNTIME_FILES_ROOT_ACTION=begin;source=gxos.builtin.files;shell=gxos.shell.root;path=/
- APP_RUNTIME_SHELL_FOLDER_REQUEST=shell=gxos.shell.root;path=/
- APP_RUNTIME_FOLDER_MOUNT_ACQUIRE=result=acquired;generation=4;leases=1;current=1
- APP_RUNTIME_FOLDER_INSPECT=result=Directory;path=/
- APP_RUNTIME_FOLDER_BIND_RESULT=success=1;generation=4;leases=1
- APP_RUNTIME_FOLDER_REINSPECT_BEGIN=generation=4;leases=2
- APP_RUNTIME_FOLDER_INSPECT=result=Directory;path=/
- APP_RUNTIME_FOLDER_REINSPECT_END=generation=4;leases=1;valid=1
- APP_RUNTIME_FILES_FOLDER_WINDOW=path=/;generation=4;leases=2
- APP_RUNTIME_WINDOW_ATTACHED=app=gxos.builtin.files;instance=instance-2-4;owner=37;window-instance=instance-2-4;owned=1
- APP_RUNTIME_LAUNCH_OK identifies Computer Files, bounds 320,220,540,400, instance-2-4, state Activated, owned=1
- APP_RUNTIME_SHELL_FOLDER_RESULT=app=gxos.builtin.files;success=1;code=Success;instance=instance-2-4
- APP_RUNTIME_FILES_ROOT_ACTION=result reports success=1 and instance-2-4

The visible Root window opened and was usable. The screenshot candidate-a/open-root.png records this state.

### Repeated Root, ownership, navigation, and cleanup

A second Open Root activation created a distinct instance and owner rather than reusing the first:

- First Root: instance-2-4, owner 37.
- Second Root: instance-3-4, owner 38.
- Both windows were visibly present in candidate-a/repeated-open-root.png.
- The second Root's folder binding independently inspected / as a Directory and created another window with the same accepted application ID.

Closing the second Root emitted one release transition from generation 4, leases 4 to 3, followed by APP_RUNTIME_INSTANCE_TERMINATED for instance-3-4 with active=2 and stale=0. The remaining Root window stayed functional: it opened the populated Programs/ directory and returned with Up Level. Screenshots are candidate-a/close-one-root.png, remaining-root-navigation.png, and remaining-root-return.png.

Closing the remaining Root emitted one release transition from leases 2 to 1, followed by termination of instance-2-4 with active=1 and stale=0. The chooser then closed and instance-1-6 terminated with active=0 and stale=0. The final Root and chooser screenshots are candidate-a/close-final-root.png and close-computer-files-chooser.png.

The log shows one mount-release begin/end transition per Root instance, each with the released binding invalidated, and no stale instance owner. No double-disposal failure was observed. The survivor remained usable between the two Root closes; this is stronger lifecycle evidence than merely observing a new window.

### Missing path and error-window dismissal

Candidate A's root activation succeeded, so no typed error window was opened. ERROR_WINDOW_BOUNDS and ERROR_WINDOW_DISMISSED were not emitted. The missing-path scenario and explicit gxos.shell.this-system Server root alias rejection are not part of the generic AppRuntime negative probes recorded above; they were not executed by this AppRuntime guest. No result is claimed for the MessageBox hide-transition diagnostic.

## Candidate B — RDSK v2

Candidate B used the accepted RDSK v2 package and registered reservation 585e37effd8e4246a6e7410198c5c023. Serial shows UEFI bootloader/kernel handoff, KMAIN INITIALIZE, two FS mounted messages, desktop initialization, framebuffer initialization, cursor/grab image reads, and discovery of nine wallpapers. Its last serial line was:

    [BACKGROUND] loaded Backgrounds/tronporche.png

No further serial output appeared for more than four minutes. The QMP framebuffer captures candidate-b/desktop-initial.png, desktop-after-wait.png, and desktop-after-click.png all show a solid teal screen without usable desktop controls. A QMP click at 30,780 was accepted by QMP but produced no visible change. Since the guest never reported AppRuntime readiness or a usable desktop, guest input delivery could not be verified; this is not classified as a keyboard/mouse driver failure.

Candidate B emitted no SMAIN_DISPATCH_REASON, APP_RUNTIME_MODEL_READY, corrected Notepad service result, continuous-desktop heartbeat, Computer Files chooser, or Open Root marker. There is no on-guest Root listing or explicit RDSK-v2 integrity diagnostic in the captured serial. The package validator confirmed the v2 RDSK header and exact image/package hashes, and guest startup reached FS mounted, but the required Root inspection, WithFiles/Child, Empty, Nested/DeepEmpty, and repeated-Root cases were not reached. No writes or experimental filesystem operations were attempted.

The captured stall is bounded to observed startup behavior after loading Backgrounds/tronporche.png; its cause is not diagnosed here. Candidate B was stopped with the owner script's supported QMP quit, exited with code 0, and its reservation was verified Released. No other guest was terminated.

## Evidence index

All R4R2 run artifacts are under out/amconv2g-r4r2-20261107/.

- Candidate A serial: candidate-a/serial.log
- Candidate B serial: candidate-b/serial.log
- Guest QMP action log: qmp-actions.jsonl
- Candidate A screenshots: candidate-a/desktop-initial.png, start-menu.png, computer-files-start-launched.png, computer-files-chooser.png, open-root.png, repeated-open-root.png, close-one-root.png, remaining-root-navigation.png, remaining-root-return.png, close-final-root.png, close-computer-files-chooser.png
- Candidate B framebuffer captures: candidate-b/desktop-initial.png, desktop-after-wait.png, desktop-after-click.png
- QAC owner logs: qac-owner-candidate-a.log and qac-owner-candidate-b.log
- Reservation state and cleanup snapshots: candidate-a-post-release-status.json, candidate-a-post-release-list.json, candidate-b-post-release-status.json, candidate-b-post-release-list.json
- Admission wait evidence: candidate-b-occupied-lane-status.json and candidate-b-occupied-lane-list.json
- Candidate manifests used for the runs: candidate-a/package-manifest.json and candidate-b/package-manifest.json

The historical R4 services failures remain documented in AMCONV2G_R4_GUEST_ACCEPTANCE_RESULTS.md and AMCONV2G_R4R1_SERVICES_AND_QAC_RECOVERY.md. They are not overwritten or reclassified by this run; the corrected Candidate A R4R2 services result is independently observed as PASS.

## Remaining work and recommended next phase

1. Preserve the accepted Candidate B package and current serial/framebuffer evidence unchanged.
2. Investigate Candidate B's startup stall after the first wallpaper load, recording progress around managed PNG/background initialization. Do not assume a PNG, RDSK, or runtime root cause from the current evidence.
3. After a separately scoped fix or diagnostic plan, revalidate any new package and rerun Candidate B under a fresh default QAC reservation.
4. Execute the missing-path, Server root alias rejection, and real error-window dismissal scenario when a supported AppRuntime guest path can reach it.
5. Complete Candidate B's v2 directory and two-Root lifecycle acceptance only after its desktop reaches readiness.

Keep the R4R2 scope boundaries: no Server filesystem mappings, production mount replacement, UTF-8 RDSK support, storage writes, new input drivers, additional GUI features, or broad NativeAOT changes. No repair was attempted as part of this acceptance run.

\n