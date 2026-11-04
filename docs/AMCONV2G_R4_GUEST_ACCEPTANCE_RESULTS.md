# AMCONV2G-R4 — QAC Guest Acceptance Results

| Field | Result |
|---|---|
| Outcome | `AMCONV2G_R4_QAC_OR_EXECUTION_BLOCKED` |
| Guest status | Candidate A booted to desktop; production lifecycle acceptance incomplete |
| Qualification date | 2026-11-04 |
| Branch | `APP_MODEL` |

## Decision

The authorized run reached the Candidate A (RDSK v1) desktop and produced real guest serial and framebuffer evidence. The live coordinator granted APP_MODEL an exclusive acceptance reservation and registered the direct QEMU child. The coordinator did not honor `retainReservationOnChildExit: true`, and it does not implement the documented `finishChild` command. That prevents the required retained two-candidate workflow. Candidate A was shut down through its QMP endpoint, and QAC now confirms its reservation is released with no managed or foreign QEMU process. Candidate B was not launched.

The guest reached `KMAIN`, initialized the RDSK filesystem and desktop, entered the continuous desktop loop, and emitted valid graphics heartbeats. The boot-time AppRuntime diagnostics emitted factory/window/instance lifecycle markers, but the application-services diagnostic reported `APP_RUNTIME_SERVICES_RESULT=FAIL` (`APP_RUNTIME_NOTEPAD_SERVICE_DIAGNOSTIC=FAIL`, `APP_RUNTIME_PHASE9_RUNTIME_OK=0`). The production Computer Files drive chooser and Open Root interaction were not attempted. No Root factory/window or mount-lease markers were observed.

## Revision and preservation

- Starting branch: `APP_MODEL`.
- Source HEAD: `dace1f25c3ad7f9b2014f6a5b62ad44684ae6738` (the requested commit). No tracked source, tests, or production tooling files were changed. Run-only helper scripts and evidence are isolated under `out/amconv2g-20261104-r4-acceptance/`.
- The only pre-existing worktree entries were untracked `bin/` and `obj/` directories under the D, E, and F inspection tests. They were preserved.
- The accepted R3 candidate ESPs remained read-only inputs. QEMU used an isolated runtime copy of Candidate A and a per-run writable OVMF variables file. Candidate B was not copied or launched.
- Original candidate file hashes before and after the guest run match the accepted R3 identities:

| Candidate | RDSK | Loader SHA-256 | Kernel SHA-256 | Ramdisk SHA-256 | Accepted ESP tree SHA-256 |
|---|---:|---|---|---|---|
| A | v1 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `862B3A95420E1C02411061610487664100908A4F2FCB2311B633586A99BD7DF1` | `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E` | `C4991782ABE5B6A65CF2700562041D4E0529CA26BAF6BB2CB817D1639082BB79` |
| B | v2 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `862B3A95420E1C02411061610487664100908A4F2FCB2311B633586A99BD7DF1` | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` | `CBAC27CF8A23554B9FA86D8A6879F5C155D2A4603A1FFD4A4DB140D0EE61CE3B` |

No builds, package generation, offline preparation, or hosted suites were repeated. The run evidence and the QAC owner harness are under the ignored output directory `out/amconv2g-20261104-r4-acceptance/`. They do not alter the preserved candidate directories under `out/amconv2g-20261102-r3-final/`.

## QAC admission and cleanup

- Preflight at 2026-11-04 12:56:16 UTC: coordinator `guideXOS QAC 2.0.0`, PID `1696`, healthy; zero reservations, zero QEMU processes, zero foreign QEMU processes, and no census warning.
- Reservation `3beb3ebcf7d04d5a80591d776867e099` was immediately granted to APP_MODEL in exclusive acceptance mode. Owner PowerShell PID `17392` started direct `qemu-system-x86_64.exe` child PID `21136` at 12:56:17 UTC. QAC verified its executable, start time, and owner; the reservation was not degraded.
- The owner sent the retained-acceptance request field as `true`, but QAC recorded `retainReservationOnChildExit: false`. After the guest shutdown, QAC returned `UnknownCommand` for `finishChild`; the running coordinator's supported-command response omitted that operation. The running coordinator was not restarted or modified.
- Candidate A was shut down with QMP `quit` on loopback port `62074` at 12:59:24 UTC. The owner process exited after the unsupported `finishChild` response. QAC inspection then confirmed the session reservation state `Released`, reason `Registered child process exited.`, and owner liveness `Exited`.
- Final `qac list` and `qac status`: zero reservations, zero managed sessions, zero QEMU processes, zero foreign processes, zero queued or blocked requests. No QAC resources from this session remain held. The QAC coordinator itself was left running.

The documented R4 procedure requires the same verified owner to retain an exclusive acceptance lane between Candidate A and Candidate B. Using separate reservation requests would not follow that procedure, so the run stopped after Candidate A cleanup.

## Candidate A guest observations

QEMU used the existing `pc-q35-8.2` and OVMF code profile, a fresh writable variables file, 1024 MiB RAM, the isolated Candidate A ESP through the existing FAT directory drive, serial capture, `-no-reboot`, no NIC, and a loopback QMP endpoint. The guest reached a 1280×800 desktop; the screenshot shows the Files, Docs, Images, and Audio desktop icons.

| Acceptance area | Actual result |
|---|---|
| Firmware / EFI loader / kernel entry | Observed. Serial contains loader handoff, `[KMAIN] INITIALIZE`, and subsequent runtime startup. |
| Candidate A RDSK and filesystem startup | Observed. The isolated ESP's ramdisk hash matches the accepted RDSK v1 image; guest serial shows ramdisk initialization, `RdskFS` initialization, and `[FS] mounted`. |
| Desktop | Observed. `[DESKTOP] initialized`, `SMAIN_DISPATCH_REASON=UEFI_CONTINUOUS_DESKTOP`, `CONTINUOUS_DESKTOP_ENTER`, four `CONTINUOUS_HEARTBEAT_GRAPHICS_VALID=1` markers, and a QMP framebuffer capture. |
| Boot-time factory/window/application-instance diagnostics | Observed as AppRuntime diagnostic output: 17 `APP_RUNTIME_FACTORY_LAUNCH` markers, 16 `APP_RUNTIME_INSTANCE_TERMINATED` markers, and 11 successful `APP_RUNTIME_LAUNCH_RESULT` markers. These are startup diagnostics, not the requested Computer Files Open Root interaction. |
| AppRuntime application-services diagnostic | Failed: `APP_RUNTIME_SERVICES_RESULT=FAIL`, `APP_RUNTIME_NOTEPAD_SERVICE_DIAGNOSTIC=FAIL`, and `APP_RUNTIME_PHASE9_RUNTIME_OK=0`. Preserve this as a guest failure; no source repair was attempted in this acceptance session. |
| Ordinary Computer Files drive chooser | Not attempted. |
| Production Open Root click and typed shell request | Not attempted; no `FILES_ROOT_ACTION`, `SHELL_FOLDER_REQUEST`, or `SHELL_FOLDER_RESULT` marker. |
| Root folder factory, window, and application instance | Not observed; no `gxos.builtin.files` Root launch, `FILES_FOLDER_WINDOW`, or Root result marker. |
| Root mount lease acquire/release | Not observed; no folder mount or `FILES_MOUNT_RELEASE_*` marker. |
| Directory navigation, separate Root windows, close behavior | Not attempted. |
| Candidate B RDSK v2 guest parsing and lifecycle | Not run because the retained acceptance workflow was unavailable. |
| Guest keyboard/mouse event delivery | Not attempted. |
| Graceful guest exit and QAC release | Observed and verified as described above. |

Evidence files:

- Serial trace: `out/amconv2g-20261104-r4-acceptance/candidate-a/serial.log` — SHA-256 `6C46958D606EC4AC035F5C2FD373B07C24B63081B256E855AAD2828BF0F64A9D`.
- Desktop framebuffer: `out/amconv2g-20261104-r4-acceptance/candidate-a/desktop-before-stop.png` (1280×800) — SHA-256 `27D8E495A3597FE8C81A44E577F9050CA8058AE1C524DAC00987B99E53DE1899`.
- QAC owner log: `out/amconv2g-20261104-r4-acceptance/qac-owner.log`.
- QAC reservation snapshot: `out/amconv2g-20261104-r4-acceptance/qac-inspect-released.json`.
- QEMU launch metadata: `out/amconv2g-20261104-r4-acceptance/candidate-a-session.json`.

## Remaining acceptance work

The R4 guest matrix remains incomplete. Resume only after the QAC coordinator used by this workstation supports and reports retained acceptance ownership across sequential children, including `finishChild`. Then run the existing Candidate A → Candidate B procedure, capture the production Computer Files / Open Root route and mount-lease lifecycle, and investigate the observed AppRuntime services diagnostic failure as a separate guest result. No offline preparation needs to be repeated.

No source or test changes were made, and nothing was pushed. This report is the intended documentation-only change for the current run; the local report commit follows the unchanged source HEAD above.
