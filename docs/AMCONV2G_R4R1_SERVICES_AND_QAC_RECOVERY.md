# AMCONV2G-R4R1 — Services Diagnosis and QAC Recovery

| Field | Result |
|---|---|
| Outcome | `AMCONV2G_R4R1_SERVICES_REPAIRED_HOSTED` |
| Guest status | Candidate A's historical failure is attributed; the corrected diagnostic has not been guest-run. Candidate A/B acceptance remains pending. |
| Qualification date | 2026-11-04 |
| Branch | `APP_MODEL` |
| Starting HEAD | `a9327406571db0f1dbb45f8067b94575f9e64b6c` |
| Ending HEAD | This local report commit; its full SHA is included in the final handoff. |

## Decision

Candidate A booted to the real UEFI desktop during R4, but its old AppRuntime services diagnostic failed on Notepad's save path. The first failed aggregate was `APP_RUNTIME_NOTEPAD_SAVE_SUCCESS=FAIL`; the next marker, save cancellation, also failed. Source tracing identifies a diagnostic assumption error (classification C): the test coupled selecting a Save As path to successfully persisting bytes on Candidate A's read-only RDSK filesystem. The guest log reports the aggregate failure, not the filesystem return value or each conjunct. The source-defined RDSK write result is `Unsupported`, so that exact return must not be presented as observed guest output.

The diagnostic now tests only the typed SaveFile service's selected and cancelled outcomes. Persistence is explicitly marked `NOT_TESTED`. Production write behavior and service contracts are unchanged. A focused source-contract regression and the existing hosted suites pass; a rebuilt NativeAOT kernel and fresh offline Candidate A/B packages are ready. The old guest result remains a failure, and the corrected result awaits a separately authorized guest run.

## Candidate A evidence and attribution

Evidence is from the prior run under `out/amconv2g-20261104-r4-acceptance/`; see [the R4 guest results](AMCONV2G_R4_GUEST_ACCEPTANCE_RESULTS.md) for its complete account.

- **Boot and desktop:** serial reached `[KMAIN]`, initialized `RdskFS` and the desktop, and entered `UEFI_CONTINUOUS_DESKTOP`. Four `CONTINUOUS_HEARTBEAT_GRAPHICS_VALID=1` markers and a QMP framebuffer were recorded. The 1280×800 screenshot shows Files, Docs, Images, and Audio icons.
- **First Notepad failure:** `APP_RUNTIME_NOTEPAD_OPEN_SUCCESS=PASS`, `...OPEN_CANCEL=PASS`, then `APP_RUNTIME_NOTEPAD_SAVE_SUCCESS=FAIL` and `...SAVE_CANCEL=FAIL`. Confirmation, cross-owner rejection, and dialog cleanup subsequently passed.
- **Exact failed assertion as far as the evidence allows:** the old `saveSuccess` composite required the save service's `Completed` result **and** `_savedPath == "Programs/phase9.txt"`, `_fileName == "phase9.txt"`, `!_dirty`, and the updated title after `PollServiceRequests()` called the persistence path. Serial records only the composite marker, so it cannot identify which conjunct first became false. The source path shows that the diagnostic attempted persistence after the file dialog selected its path.
- **Cancellation dependency:** the following old save-cancel assertion required `_savedPath == "Programs/phase9.txt"`, implicitly relying on the preceding save having written successfully. That expectation is also invalid when the filesystem does not support writes.
- **Later steps and impact:** the diagnostic continued after the save failures: confirmation, cross-owner rejection, dialog cleanup, notification, shared settings, snapshot, display-options service, stale-request rejection, and service cleanup all emitted pass markers. The last successful services marker before the aggregate result was `APP_RUNTIME_SERVICES_STALE_CONTEXTS=0`; it was followed by `APP_RUNTIME_SERVICES_RESULT=FAIL` and `APP_RUNTIME_NOTEPAD_SERVICE_DIAGNOSTIC=FAIL`. `APP_RUNTIME_PHASE9_RUNTIME_OK=0`; shell launch and document-open values were 1, and orphan-dialog/stale-context counts were 0. The separate lifecycle diagnostic later reported `result=PASS`. Desktop execution continued and produced later valid graphics heartbeats. This is a failed diagnostic, not a fatal boot result.
- **Factory and instance scope:** 17 `APP_RUNTIME_FACTORY_LAUNCH` activity records, 16 termination records, and 11 successful launch-result markers were recorded. These counts establish activity, not a blanket factory/instance pass. The separate lifecycle diagnostic pass is the positive lifecycle result. No production Computer Files chooser/Open Root interaction, Root factory/window, or mount-lease marker was observed; Candidate B did not run.

### Source trace and root cause

The failure is **C — diagnostic assumption defect**. Candidate A mounts `new RdskFS()` in `guideXOS/Program.cs`. `Kernel/FS/FileSystem.cs` defines the typed `FileSystem.TryWriteAllBytes` default as `FatOperationResult.Unsupported`, and `RdskFS` does not override that operation. In the Notepad diagnostic path, no folder-operation lease is bound, so `SaveTo` uses the global `File.TryWriteAllBytes` path. The dialog service can validly complete with a selected path while the subsequent write is unsupported. Thus selection and persistence are separate outcomes.

This is not an initialization-order defect: `SMainSetup` calls `Desktop.InitializeAppModel()` and then `ApplicationServiceRegistry.Initialize()` at `guideXOS/Program.cs:1145-1146`; the AppRuntime diagnostic runs later in that setup. `AppCollection` initializes the instance, descriptor, and factory registries in `guideXOS/OS/App.cs`. The evidence does not indicate a NativeAOT/runtime compatibility defect or a production SaveFile contract violation. The live guest did not log the write result, so the specific `Unsupported` result is source-derived, not a captured runtime value.

### Correction

`guideXOS/DefaultApps/Notepad.cs` now observes the completed SaveFile request and checks the terminal `Selected` outcome and exact selected path without polling into `SaveTo` or asserting persisted editor state. The cancellation check observes terminal `Cancelled`/`Cancelled` state and verifies that `_savedPath` remains what it was before that request. Terminal handles and callbacks are cleared after observation. Diagnostic output now uses `NOTEPAD_SAVE_SELECTION` and explicitly emits `NOTEPAD_SAVE_PERSISTENCE=NOT_TESTED`; it no longer labels dialog selection as save success. `guideXOS/OS/ApplicationInstance.cs` and `run_uefi_validation.ps1` use the matching `PHASE9_SAVE_DIALOG_RESULT` / `phase9SaveDialogResult` name.

This preserves production file-write semantics, typed Root activation, Computer Files `MultiInstance` behavior, mount-lease ownership, Server alias rejection, generation-safe instance identity, and the existing typed error vocabulary. Existing Root UI instrumentation records root-action begin/result, shell request/result, factory launch, window attachment, folder binding/inspection, instance termination, and mount-release begin/end. Review found that an error window's bounds were logged when shown, but serial evidence did not confirm its dismissal. In the AppRuntime diagnostic build, `MessageBox.OnSetVisible(false)` now emits `ERROR_WINDOW_DISMISSED=1` only when the bounded `Desktop.ShowOpenError` path had marked that message box. No error text, memory address, or synthetic close event is emitted; the marker follows the real visibility transition.

## Verification evidence

### Builds

Release builds completed successfully with zero errors:

| Build | Result | Accepted baseline |
|---|---:|---:|
| AppModel | 0 errors, 137 warnings | 0 errors, 137 warnings |
| AppRuntime | 0 errors, 141 warnings | 0 errors, 141 warnings |

An additional default-mode compile also completed with 0 errors and 142 warnings. It was not the explicit AppModel diagnostic variant; the AppModel and AppRuntime rows above are the requested mode-to-mode baseline comparison, and both match their accepted warning counts exactly.

The NativeAOT x64 PE linked through the installed Visual Studio/MSVC environment without forced unresolved symbols (`/FORCE` was not used). The linker reported the inherited fixed-base `LNK4281` warning. The rebuilt image is AMD64 PE32+, Native subsystem, entry `KMainWrapper` at `0x100018B8`, image base `0x10000000`, with zero imports and relocations; its SHA-256 is `DB53F26C3FD7035BAB02D1B7D619FA20F26E8B03BC21BC639291406ECC9C3368`. The converted kernel ELF is 64-bit little-endian `ET_EXEC`, `EM_X86_64`, has 8 load segments, an entry inside an executable load segment at `0x1004DE3C`, and no dynamic/interpreter segments. The new ELF SHA-256 is `24FA8C323334614FA4800643575BEE1F77707AB2BCEB87BBD46C2EA62BD2BB9F` (4,890,624 bytes). No old R3 kernel identity was reused.

### Hosted and offline checks

These are hosted/source/package results, not guest acceptance:

| Check | Result |
|---|---:|
| AMCONV1 source compatibility | 9/9 |
| AMCONV2B folder/mount source contract | 25/25 |
| AMCONV2F source contract | 9/9 |
| AMCONV2B/C hosted folder and mount suite | 71 assertions, 0 failures |
| RDSK v2 hosted reader/folder suite | 58/58, 0 failures |
| RDSK v1 hosted inspection/binding | 32/32, 0 failures |
| FAT hosted inspection | 11/11, 0 failures |
| Desktop interaction | 42/42 |
| Production Root UI wiring | 21/21 |
| Focused services diagnostic regression | 11 assertions, 0 failures |
| AMCONV2G packaging tests | 4/4 |
| Phase 33 wire regression | Pass; request 2,084 bytes, response 16 bytes |
| Task Manager Memory Details regression | Pass |
| Managed artifact identity/staging | 41/41 agreements for each candidate |

The focused services regression is an executable source-contract check. It confirms SaveFile selection/cancellation observation, the absence of a persistence call in the selection assertion, cancellation path preservation, accurate markers, the RDSK unsupported-write source contract, production registry self-test coverage, bounded error-window display evidence, and dismissal evidence tied to the real hide transition. It does not claim to simulate the UEFI guest. Hosted suite artifacts and build outputs are isolated under `out/amconv2g-r4r1-20261104/`.

## Fresh offline package identities

Both packages were assembled in an isolated copy of the production `build.ps1`, using the final new kernel and retained RDSK v1/v2 source images. Managed artifact identity audits passed (41/41 for each package), production package validation and its check-only verification passed, and each manifest confirms exactly the EFI loader, kernel, and ramdisk. The accepted R3 candidate ESPs under `out/amconv2g-20261102-r3-final/` were not modified.

| Candidate | RDSK | Loader SHA-256 | Kernel SHA-256 | Ramdisk SHA-256 | ESP tree SHA-256 |
|---|---:|---|---|---|---|
| A | v1 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `24FA8C323334614FA4800643575BEE1F77707AB2BCEB87BBD46C2EA62BD2BB9F` | `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E` | `5E3192940FA9EC7CE5E63F6580D9D773F7D967116E9AC1AEE0E1C456D26A6A35` |
| B | v2 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `24FA8C323334614FA4800643575BEE1F77707AB2BCEB87BBD46C2EA62BD2BB9F` | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` | `0FCB88C7DD142F195433156110476943500EF2C6BF3D9DE437FD6CA85AFBE4F4` |

Manifests are `out/amconv2g-r4r1-20261104/packages-final/candidate-a/package-manifest.json` and `.../candidate-b/package-manifest.json`. They explicitly say offline package ready and `guest_boot_claimed: false`. No guest run was performed for these packages.

## QAC contract and observed live behavior

The QAC checkout at `D:\dev\guideXOS_QAC` was inspected read-only at HEAD `1ea193258690f9e7b39390b101a117166ef8bbdd`; it remained clean on `main`. Its README and implementation document a retained acceptance mode: `request` with `retainReservationOnChildExit: true`, followed by `prepareLaunch`, a direct QEMU child, `registerChild`, verified child exit, `finishChild` back to `Granted`, and final owner `release`. In checked-in source, `CompleteChild` rejects non-retained reservations, and the coordinator advertises `finishChild`.

The live coordinator used by R4 did not match that capability. R4 sent the retain field as `true`, but the grant and released snapshot recorded `retainReservationOnChildExit: false`; `finishChild` returned `UnknownCommand`. Candidate A was shut down via QMP at 12:59:24 UTC. QAC subsequently reported the registered child exited and the reservation `Released`; final status/list showed zero reservations, QEMU processes, foreign processes, queued requests, or blocked requests. The coordinator was not restarted or changed. The evidence means retained mode is **not available on the live coordinator used for R4**, not that `finishChild` is unsupported by all documented/source QAC versions.

For the next run, use the live-supported conservative flow: **two separate default, one-child acceptance reservations**. Do not request retained mode or call `finishChild`. For each candidate, the same owner/launcher process that requests admission must launch its direct QEMU child. Use the documented launch-intent and direct-child registration lifecycle. On verified child exit, the default reservation is released by QAC (`Registered child process exited.` in the observed contract). Keep the owner available until the exact reservation is inspected as `Released`; then verify `status`/`list` are clear before requesting the next one. If the live coordinator's response differs, do not launch the next child; stop and record the state for review.

The future authorization must explicitly cover two separately requested exclusive APP_MODEL acceptance reservations (Candidate A and Candidate B) and this cleanup sequence. Authorization for the earlier single Candidate A run is not assumed to persist.

## Revised Candidate A/B guest acceptance procedure

The current step-by-step procedure is also at the top of [the guest lifecycle acceptance document](AMCONV2G_GUEST_LIFECYCLE_ACCEPTANCE.md). Candidate packages are staged under the ignored output directory listed above.

1. Confirm the explicit authorization covers two sequential, separate acceptance requests. Before each request, read QAC `status` and `list`; require a healthy coordinator, no foreign QEMU, no census warning, and no conflicting reservation. If the request queues or blocks, do not launch; wait or cancel using the supported owner flow.
2. For Candidate A, have the same owner process request the default exclusive acceptance reservation, verify a `Granted` response with `retainReservationOnChildExit=false`, record its ID/owner, call `prepareLaunch`, start the direct QEMU child, and register that exact child. Do not use an unrelated QEMU process or a separate launcher parent.
3. Boot the fresh v1 package. Record firmware/kernel handoff, RDSK mount, desktop and framebuffer. Check the corrected services output: save selection/cancellation and service cleanup pass; persistence remains `NOT_TESTED`. A services pass does not substitute for the GUI lifecycle assertions.
4. Open Start → **Computer Files** and confirm the ordinary drive chooser. Click **Open Root**. Capture `FILES_ROOT_ACTION=begin`, `SHELL_FOLDER_REQUEST`, the shell result, folder binding/inspection, factory launch, `FILES_FOLDER_WINDOW`, and the result naming `gxos.builtin.files` and its instance handle. Navigate into a populated directory and back; close the window and confirm instance termination plus mount-release begin/end. If a typed error window appears, record `ERROR_WINDOW_BOUNDS`, the typed shell result, and `ERROR_WINDOW_DISMISSED=1` after closing it; do not count a failed Root activation as success.
5. Shut down Candidate A through its owned QMP endpoint, wait for and reap the exact registered child, then inspect its reservation until QAC reports `Released`. Verify `status`/`list` show zero reservations, managed/foreign QEMU, queued requests, blocked requests, and census warnings before closing the owner.
6. Recheck authorization and QAC availability. For Candidate B, acquire a new default exclusive acceptance reservation with a new ID and repeat the direct-child ownership, evidence, and cleanup steps using the fresh v2 package. Check boot, services diagnostic, and Root activation. Inspect `WithFiles` and its `Child`, `Empty` with no entries, and `Nested/DeepEmpty`; open two Root windows, close one while the other remains usable, then close the last and verify instance/lease cleanup.
7. Shut down Candidate B, wait/reap the registered child, verify that exact reservation is `Released`, and perform the same clear final QAC `status`/`list` checks. Preserve separate evidence and outcome for each candidate.

Keep mount replacement deferred: there is no supported production operation to replace the active filesystem and request disposal. Do not expand this acceptance into UTF-8, Server filesystem mappings, new storage drivers, or unrelated features. Guest-only claims remain pending until observed from these packages.

## Repository scope and local commit

All changes are confined to `D:\dev\guideXOSUEFI_APP_MODEL`. The protected QAC checkout was read-only. The three pre-existing untracked D/E/F test `bin/` and `obj/` directories were preserved. No branch switch, merge, rebase, QEMU launch, QAC reservation request, live guest manipulation, global SDK change, or push occurred. Only completed in-scope source, test, and documentation changes are included in the local commit.

- Local commit: this report commit; its full SHA is included in the final handoff.
- Nothing was pushed.
