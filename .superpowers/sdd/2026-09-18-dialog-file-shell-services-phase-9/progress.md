# SDD ledger — plan: docs/superpowers/plans/2026-09-18-dialog-file-shell-services-phase-9.md

Pre-flight: shared interfaces found between Tasks 1-2 (contract values consumed by request sessions), Tasks 2-3 (session cleanup consumed by transient-window metadata), Tasks 2/4/5/6 (request handles and lifecycle validation consumed by each service), Tasks 4/5 (transient-window cleanup), Tasks 6-8 (Shell/Open and service access consumed by application migrations), and Tasks 7-9 (runtime markers consumed by the final diagnostic). No shared-interface conflicts found; the approved spec is binding.

Task 1 RED: `run_uefi_validation.ps1 -AppModel` failed in the intended compile phase because the new service IDs, result code, and request contract types were absent.

Task 1 GREEN: AppModel validation completed with `APP_MODEL_SERVICES_SELFTEST_OK=1`; the new contract assertions passed and the existing Phase 8 self-test remained green.

Task 2 RED: AppModel build failed at the intended compile phase because fixed-session APIs referenced by the new lifecycle self-test were absent.

Task 2 GREEN: AppModel validation completed with `APP_MODEL_SERVICES_SELFTEST_OK=1`, `APP_MODEL_SERVICES_REGISTERED=7`, lifecycle self-tests passed, inactive observation and stale cleanup assertions passed, and compatibility/legacy counters remained at the existing baseline.
Task 1: complete (commits 0539daf..1ac98ff, tests: pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppModel -TimeoutSeconds 300 → Serial log: D:\dev\guideXOSUEFI\serial_uefi_validation_20260918_233014.txt)
Task 2: complete (commits 1ac98ff..d5e372d, tests: pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppModel -TimeoutSeconds 300 → Serial log: D:\dev\guideXOSUEFI\serial_uefi_validation_20260918_233445.txt)

Task 3 RED: AppModel build failed at the intended compile phase because transient service-window classification, session metadata, and WindowManager registration APIs were absent.

Task 3 GREEN: Fresh AppModel validation completed with lifecycle and taskbar/grouping self-tests passing, `APP_MODEL_SERVICES_SELFTEST_OK=1`, seven registered services, zero compatibility fallback, zero legacy backend calls, and no transient-session diagnostic breadcrumbs. The runtime investigation also verified service-window teardown through the existing WindowManager disposal boundary without changing ordinary owned-window/taskbar projections.
Task 3: complete (commits d5e372d..32a649d, tests: pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppModel -TimeoutSeconds 300 → Serial log: D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_000140.txt)

Task 4 RED: AppModel compilation initially exposed the missing dialog access, completion, and cleanup APIs required by the new dialog lifecycle self-test.

Task 4 GREEN: AppModel validation completed with `APP_MODEL_SERVICES_SELFTEST_OK=1`, seven registered services, zero compatibility fallback, zero legacy backend calls, and the Phase 8 service markers intact. The dialog self-test covers bounded request shapes, duplicate `Conflict`, accepted/rejected/closed/cancelled/backend-failure outcomes, inactive observation versus new-request rejection, and stale requester cleanup. AppRuntime completed with graphics invariants valid, allocator corruption 0, ThreadPool.Locked 0, balanced input, and zero dropped keyboard/mouse input.

Task 4: complete (commits 32a649d..pending, tests: pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 → serial_uefi_validation_20260919_001706.txt; pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppRuntime -TimeoutSeconds 180 → serial_uefi_validation_20260919_001751.txt)
Task 4: complete (commits 32a649d..f27763b, tests: pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 → Serial log: D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_002443.txt)

Task 5 RED: AppModel compilation failed in the intended phase because the new open/save service projections, typed file-dialog result, and deterministic completion helpers were absent.

Task 5 GREEN: AppModel validation completed with the Phase 8/App Model markers green and `APP_MODEL_SERVICES_SELFTEST_OK=1`. The file-dialog self-test covers bounded starting locations and suggested filenames, selected-path propagation, open/save cancellation, backend-failure results, and zero transient/orphan service windows. AppRuntime completed with graphics invariants valid, allocator corruption 0, balanced input, zero dropped keyboard/mouse input, zero compatibility fallbacks, and zero legacy backend calls.

Task 5: complete (commits f27763b..pending, tests: pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 → serial_uefi_validation_20260919_072910.txt; pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppRuntime -TimeoutSeconds 180 → serial_uefi_validation_20260919_073030.txt)
Task 5: complete (commits f27763b..1f83d5d, tests: pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 → Serial log: D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_073648.txt)
Task 6: complete (commits 1f83d5d..48764ee, tests: pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 → Serial log: D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_075215.txt)
Task 7: complete (commit 40ac33c, tests: pwsh -NoProfile -ExecutionPolicy Bypass -File ./run_uefi_validation.ps1 -AppRuntime -TimeoutSeconds 300 → APP_RUNTIME_COMPLETE; Phase 9 service markers PASS; Serial log: D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_082738.txt)
Task 8: started (base 40ac33c; Computer Files shell/document service routing and Display Options OpenFile migration)
Task 8 RED: AppRuntime first exposed the intended migration regressions: the Computer Files factory result was not reinitialized after service acquisition, and the existing association diagnostics were absent from the new Shell/Open backend.

Task 8 GREEN: Computer Files modern factory instances now receive ApplicationServiceContext/ApplicationServiceAccess and route document opens through Shell/Open; compatibility-only instances retain the old fallback. Display Options uses the OpenFile service with asynchronous polling and deterministic success/cancel cleanup proof. The Shell/Open adapter preserves association, file-result, GXM, and negative-probe diagnostics while dispatching through the existing modern App Model. AppRuntime completed with `APP_RUNTIME_COMPLETE`, application-service/dialog/file markers PASS, compatibility fallback 0, legacy backend 0, allocator corruption 0, ThreadPool.Locked 0, balanced input, and zero dropped input. Serial log: D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_085736.txt
Task 8: complete (commit 0981010; tests: `build.ps1 -Diagnostic AppRuntime -NoRun`; `run_uefi_validation.ps1 -AppRuntime -TimeoutSeconds 300` → `APP_RUNTIME_COMPLETE`; Serial log: `D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_085736.txt`)

Task 9 RED: the new Phase 9 runtime gate initially failed for two intentional
diagnostic reasons: a shell launch changed the requester to `Inactive` before
the next new request, and the positive document probe used the existing
calculator GXM path whose diagnostic-only negative probes launch an installer.
The service itself correctly returned lifecycle rejection and preserved typed
request ownership.

Task 9 GREEN: the runtime proof now reactivates the requester before creating
the next new interactive request, uses the valid `Programs/imageviewer.gxm`
association for positive document-open coverage, and performs bounded cleanup
of any target instances created by the shell adapter.  AppModel and AppRuntime
Phase 9 markers are green; Notepad and Display Options service diagnostics
pass; cleanup returns active instances, observations, WindowManager windows,
and stale ownership to baseline; orphan dialogs and stale service contexts are
zero.  Final AppRuntime evidence: `APP_RUNTIME_COMPLETE`,
`APP_RUNTIME_PHASE9_RUNTIME_OK=1`, compatibility fallback 0, legacy backend 0,
allocator corruption 0, valid graphics, zero dropped keyboard/mouse input,
and balanced mouse transitions. Serial log:
`D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_102235.txt`.

Task 9: complete (final Phase 9 commit; tests: `run_uefi_validation.ps1 -AppModel -TimeoutSeconds 300` → `DIAGNOSTIC_COMPLETE` / validation true, serial `D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_102203.txt`; `run_uefi_validation.ps1 -AppRuntime -SkipBuild -TimeoutSeconds 300` → `APP_RUNTIME_COMPLETE` / validation true, serial `D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_102235.txt`; `run_uefi_validation.ps1 -NativeInput -TimeoutSeconds 300` → `TIMEOUT_SUCCESS`, serial `D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_102854.txt`; `run_uefi_validation.ps1 -ContextMenu -TimeoutSeconds 300` → `CONTEXT_MENU_COMPLETE` / validation true, serial `D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_103407.txt`; `run_uefi_validation.ps1 -Continuous -TimeoutSeconds 300` → `TIMEOUT_SUCCESS`, serial `D:\dev\guideXOSUEFI\serial_uefi_validation_20260919_104109.txt`)
