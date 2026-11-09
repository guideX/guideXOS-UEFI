# AMCONV2G-R4R4 — Guest Startup and Lifecycle Acceptance

| Field | Result |
|---|---|
| Outcome | `AMCONV2G_R4R4_GUEST_PARTIAL` |
| Detailed status | `GUEST_TESTED_WITH_WINDOWS_GPF; QAC_CLEAN` |
| Qualification date | 2026-11-08 |
| Branch | `APP_MODEL` |
| Starting HEAD | `872a7fd75985fea8897c4c43a7f2cc32a070122d` |
| Ending source HEAD | `872a7fd75985fea8897c4c43a7f2cc32a070122d` |

## Decision

The user explicitly authorized two sequential, separate, default single-child APP_MODEL acceptance reservations. Candidate A and then Candidate B were launched through the live QAC coordinator, each with a distinct reservation and managed direct QEMU child. Both guests booted into AppRuntime and the desktop. Candidate A's Computer Files chooser and Open Root action succeeded, then closing the chooser caused a kernel General Protection Fault. Candidate B's RDSK v2 startup stall did not recur; Computer Files, Open Root, populated-directory navigation, two Root instances, and both Root lease-release transitions were observed. Closing a Computer Files chooser then caused the same General Protection Fault. Neither candidate is a full lifecycle pass.

Both guests were shut down with QMP `quit`; each registered child exited with code 0. QAC reported each exact reservation `Released`, and post-release status/list plus the Windows process census showed no remaining QEMU process or reservation. The original R4R3 packages and manifests passed the validator's read-only `--check-only` mode after the guest runs and were not modified.

This is partial guest acceptance. Candidate B's desktop startup is guest-confirmed as recovered for this package, but the exact guest CRC validation count remains unobserved. Both candidate lifecycle runs stopped at chooser closure after a guest GPF; final chooser/mount teardown is unverified.

## Checkout and preservation

- Checkout: `D:\dev\guideXOSUEFI_APP_MODEL`.
- The requested branch, `APP_MODEL`, and starting HEAD both matched.
- The acceptance used isolated writable ESP copies and fresh OVMF variable-store copies under `out/amconv2g-r4r4-20261108/`; the original candidate ESPs and manifests were mounted neither directly nor modified.
- The existing untracked test outputs under `Tests/AMCONV2DFatInspectionTests/{bin,obj}`, `Tests/AMCONV2ERdskInspectionTests/{bin,obj}`, and `Tests/AMCONV2FRdskV2Tests/{bin,obj}` were preserved.
- The package validator passed in `--check-only` mode before and after guest execution. Source, candidate package files, and manifests were not modified. No build was run. Guest interaction artifacts are under the ignored `out/amconv2g-r4r4-20261108/` directory.
- This report is the only tracked change for this acceptance. It is committed locally after the report is complete; nothing is pushed.

## Authorization, QAC, and cleanup

- **Authorization:** explicit authorization covered Candidate A and Candidate B, two sequential, separate, exclusive default reservations, managed direct-child QEMU launch/registration, serial and screenshot capture, QMP shutdown, and cleanup verification.
- **Coordinator:** guideXOS QAC 2.0.0, PID `8204`.
- **Candidate A:** reservation `dc1424a1ec294be2a3a9b471c6d86ac2`; owner PID `6224`; QEMU PID `12264`; QMP port `52542`; request/grant at `2026-11-09T03:43:58Z`; QMP shutdown and child exit code 0 at `2026-11-09T03:49:29Z`. QAC inspection reported `Released`, `retainReservationOnChildExit=false`, reason `Managed child exited.`
- **Candidate B:** reservation `7508ebb7cdcb4e43a627ed2ca91adc00`; owner PID `19008`; QEMU PID `20520`; QMP port `52543`; request/grant at `2026-11-09T03:51:11Z`; QMP shutdown and child exit code 0 at `2026-11-09T04:09:22Z`. QAC inspection reported `Released`, `retainReservationOnChildExit=false`, reason `Managed child exited.`
- **Post-release checks:** after each candidate, QAC status/list reported zero QEMU processes, foreign QEMU processes, reservations, managed sessions, queued requests, and blocked requests; there were no census warnings. Windows showed zero `qemu-system-*.exe` processes, and the candidate's loopback QMP listener was closed.
- **Other workstream:** an initial read-only process snapshot showed QEMU PID `17580` as a child of a QAC CLI command for `guideXOSServer` acceptance while the coordinator list returned zero processes/reservations. No request or process action was made during that mismatch. Before Candidate A admission, the owner and QEMU process had exited; a fresh QAC status/list and Windows census were clear. That workstream was left untouched.

The two authorized reservations were separate and sequential. Candidate B was not requested until Candidate A's exact reservation was Released and the post-release checks were clear.

## R4R3 candidate identities

Both manifests are under `out/amconv2g-r4r3/packages/`. Each declares `diagnostic_mode: AppRuntime` and `guest_boot_claimed: false`. The current ESP contents were checked against their full manifest fields using the existing package validator's hash and ELF validation functions.

| Candidate | RDSK | Loader SHA-256 | Kernel SHA-256 | Ramdisk SHA-256 | ESP tree SHA-256 |
|---|---:|---|---|---|---|
| A | v1 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `B4625386E177A0749D647E7FD3A18C06FCE7D5C5D82CE8E838100D56D9F8AE05` | `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E` | `D8C516DE5637A0F825E140959F160013C030DE233EAF27E7037EFC9060A38CE9` |
| B | v2 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `B4625386E177A0749D647E7FD3A18C06FCE7D5C5D82CE8E838100D56D9F8AE05` | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` | `D29CD1DA68A9262A49AEB584F0BB2D6355F81305BAF28BAC748DC9CA59A25FB9` |

The loader and kernel are shared. Candidate A's ramdisk is 56,431,471 bytes; Candidate B's is 56,435,018 bytes. Both ESPs contain exactly `EFI/BOOT/BOOTX64.EFI`, `kernel.elf`, and `ramdisk.img`. The recomputed per-file hashes, byte counts, ELF fields, RDSK versions, and ESP tree hashes match the manifest values.

## Diagnostic and kernel build evidence

The Candidate A NativeAOT build log identifies the UEFI diagnostic mode as `AppRuntime`, and both candidate manifests record the same mode. The actual NativeAOT linker response file at `out/amconv2g-r4r3/package-pipeline/guideXOS/obj/x64/Release/net7.0/win-x64/native/link.rsp` contains no `/FORCE:UNRESOLVED`; the current and isolated `guideXOS.csproj` files also contain no unresolved-symbol forcing option. An ancillary `pp.xml` file in the isolated package tree does contain a `/FORCE:UNRESOLVED` `LinkerArg` line, so the broad tree search alone is not evidence of the effective invocation. The final response file confirms that option was absent from the accepted kernel link. This is offline build evidence only; no runtime diagnostic marker was observed during this phase.

## Historical comparison and actual guest evidence

The preserved R4R2 record reports that Candidate A passed its exercised guest cases with the then-current shared kernel. It reports that Candidate B stopped after `[BACKGROUND] loaded Backgrounds/tronporche.png`, before AppRuntime initialization or a usable desktop. The R4R3 report attributes the predicted startup workload to repeated whole-image CRC validation during icon loading and records the per-mount cache repair and hosted verification.

### Candidate A — RDSK v1

The isolated Candidate A package booted through the UEFI loader, mounted RDSK v1, completed AppRuntime, and reached the responsive desktop. Serial records `APP_RUNTIME_NOTEPAD_SAVE_SELECTION=PASS`, `APP_RUNTIME_NOTEPAD_SAVE_CANCEL=PASS`, `APP_RUNTIME_NOTEPAD_SAVE_PERSISTENCE=NOT_TESTED`, and `APP_RUNTIME_SERVICES_RESULT=PASS`. `SMAIN_DISPATCH_REASON=UEFI_CONTINUOUS_DESKTOP`, `DESKTOP_REAL_TEXT_RENDERED=1`, and `CONTINUOUS_HEARTBEAT_GRAPHICS_VALID=1` were observed. The initial framebuffer screenshot is `out/amconv2g-r4r4-20261108/candidate-a/desktop-initial.png`.

Start → Computer Files opened the production chooser. Open Root submitted `gxos.shell.root` with path `/`; serial records two `Directory` inspections, folder window generation 4 with leases 2, factory instance `instance-2-4`, and a successful shell result. The chooser and Root result evidence is in `computer-files-chooser.png`, `open-root.png`, and `serial.log`.

Closing the chooser caused a kernel panic on exception vector `0x0D` (General Protection Fault), at RIP `0x00000000100A3DD6`, with UEFI frame stage `WINDOWS` and last boundary `400`. The framebuffer is captured in `close-chooser.png`; serial evidence is in `serial-before-shutdown.log`. Root navigation and Root lease release were not reached after this fault.

### Candidate B — RDSK v2

The isolated Candidate B package mounted RDSK v2 and reached the real desktop. Serial reached `[BACKGROUND] loaded Backgrounds/flower.png`, `[ICON_GRAPHICS] start=...fallbacks=0`, `[PNG] image assets initialized`, `[DESKTOP] real icon assets enabled`, `APP_RUNTIME_MODEL_READY=apps=12`, and `SMAIN_DISPATCH_REASON=UEFI_CONTINUOUS_DESKTOP`. The corrected services diagnostic passed; save selection and cancellation passed, and persistence remained `NOT_TESTED`. A QAC inspection 32 seconds after launch already contained all listed startup milestones and a valid continuous-desktop heartbeat. The previous R4R2 stall after the wallpaper marker was not reproduced. This confirms startup recovery on the tested guest, but does not measure the exact CRC validation count. See `candidate-b/startup-observation.json`, `desktop-initial.png`, and `serial.log`.

The production Computer Files chooser and Open Root succeeded. The first Root request created `instance-2-4`, generation 4, at `/`; navigating into populated `Programs` displayed its files, and Up Level returned to the root listing. A second Open Root created a separate `instance-4-3`, generation 6. Screenshots include `computer-files-chooser.png`, `open-root.png`, `programs-directory.png`, `root-after-up-level.png`, and `second-root.png`.

Closing `instance-4-3` emitted `FILES_MOUNT_RELEASE_BEGIN/END` for generation 6, leases `2 → 1`, `valid=0`, then `INSTANCE_TERMINATED` with `active=3; stale=0`. The remaining Root instance `instance-2-4` stayed usable and displayed the populated Programs listing. Closing it emitted the equivalent release for generation 4, leases `2 → 1`, `valid=0`, then termination with `active=2; stale=0`. Evidence is in `close-one-root.png`, `surviving-root-programs.png`, `close-final-root.png`, and `serial.log`.

Closing a Computer Files chooser then caused the same General Protection Fault at RIP `0x00000000100A3DD6`, frame stage `WINDOWS`, boundary `400`. Serial records the chooser instance termination (`instance-1-6`, `active=1; stale=0`) before the panic. The panic framebuffer is `close-one-chooser.png`; details are in `serial-before-shutdown.log`. The remaining chooser was not closed, so final chooser teardown and total mount disposal were not verified.

Both Candidate B Root leases were released without stale owners. Full guest teardown did not pass because chooser closure panicked; the host QEMU process and QAC reservation were nevertheless cleanly shut down and released.

| Acceptance area | R4R4 result |
|---|---|
| UEFI loader, kernel entry, mount, AppRuntime, responsive desktop | Pass on Candidate A and B |
| Candidate A RDSK v1 boot and services diagnostic | Pass; corrected services result and read-only persistence classification observed |
| Candidate B RDSK v2 startup stall | Recovered on this guest; AppRuntime and desktop reached within the first 32 seconds after QAC grant |
| Guest CRC validation count/cache reuse | Not observed; exact guest call count remains unverified |
| Computer Files chooser and production Open Root | Open Root succeeded on both; chooser closure panicked on both |
| Populated directory navigation | Candidate B `Programs` and Up Level passed; empty/nested/missing-directory cases not run |
| Two Root instances and window ownership | Candidate B created two distinct Root instances; closing one left the other usable |
| Root mount lease release and stale ownership | Candidate B both Root releases observed (`2 → 1`, `valid=0`, `stale=0`); Candidate A not reached |
| Final chooser teardown and total mount disposal | Not verified; chooser close caused a GPF on both candidates |
| Corrected AppRuntime services diagnostics | Pass on both; persistence remains `NOT_TESTED` |
| Guest evidence | Serial logs, QAC records, QMP action logs, and screenshots under `out/amconv2g-r4r4-20261108/` |

The hosted evidence and source conclusions remain those documented in `AMCONV2G_R4R3_RDSK_V2_WALLPAPER_STALL.md`. They are not guest acceptance. Read-only RDSK persistence remains `NOT_TESTED`; no write or mutation behavior was exercised.

## Recommended next phase

Investigate the shared chooser-close General Protection Fault at RIP `0x00000000100A3DD6` using the captured Candidate A/B serial traces and frame context. The R4R4 guest result remains partial until chooser closure and final mount disposal complete without a kernel panic. Preserve the R4R3 packages and manifests; any later guest rerun should retain the documented QAC preflight, separate reservation, QMP shutdown, and exact release checks.
