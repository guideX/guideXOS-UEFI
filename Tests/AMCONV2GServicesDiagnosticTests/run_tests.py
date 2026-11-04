from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
passed = 0
failed = 0


def check(condition, message):
    global passed, failed
    if condition:
        passed += 1
        print("PASS: " + message)
    else:
        failed += 1
        print("FAIL: " + message)


notepad = (ROOT / "guideXOS" / "DefaultApps" / "Notepad.cs").read_text(
    encoding="utf-8-sig"
)
method_start = notepad.index("internal bool RunPhase9ServiceDiagnostic(")
method_end = notepad.index("private void CompleteDiagnosticMessage()", method_start)
method = notepad[method_start:method_end]
selection_start = method.index('_text = "Phase 9 save";')
cancel_start = method.index("string pathBeforeSaveCancel = _savedPath;", selection_start)
selection = method[selection_start:cancel_start]
cancel_end = method.index("Visible = false;", cancel_start)
cancel = method[cancel_start:cancel_end]

check("_services.SaveFile.Observe(_serviceContext, saveHandle)" in selection,
      "selected save outcome is observed through the production SaveFile service")
check("ApplicationServiceRequestState.Completed" in selection and
      "ApplicationFileDialogOutcome.Selected" in selection and
      '==\n                        "Programs/phase9.txt"' in selection,
      "the diagnostic checks terminal selected-path semantics")
check("PollServiceRequests()" not in selection and
      "CompleteDiagnosticMessage()" not in selection and "SaveTo(" not in selection,
      "the service contract test does not require a filesystem write")
check("_services.SaveFile.Observe(_serviceContext,\n                                cancelSaveHandle)" in cancel and
      "ApplicationServiceRequestState.Cancelled" in cancel and
      "ApplicationFileDialogOutcome.Cancelled" in cancel,
      "save cancellation is verified from its terminal service result")
check("_savedPath == pathBeforeSaveCancel" in cancel and
      '== "Programs/phase9.txt"' not in cancel,
      "cancel preserves the saved path that existed before that request")
check("return openSuccess && openCancel && saveSelection && saveCancel &&" in method,
      "service diagnostic outcome includes the corrected save assertions")
check('"NOTEPAD_SAVE_SELECTION="' in method and
      '"NOTEPAD_SAVE_PERSISTENCE=NOT_TESTED"' in method and
      '"NOTEPAD_SAVE_SUCCESS="' not in method,
      "guest breadcrumbs distinguish dialog selection from persistence")

filesystem = (ROOT / "Kernel" / "FS" / "FileSystem.cs").read_text(
    encoding="utf-8-sig"
)
rdsk = (ROOT / "Kernel" / "FS" / "RdskFS.cs").read_text(
    encoding="utf-8-sig"
)
program = (ROOT / "guideXOS" / "Program.cs").read_text(encoding="utf-8-sig")
check("TryWriteAllBytes(string name, byte[] content) => FatOperationResult.Unsupported"
      in filesystem and "TryWriteAllBytes" not in rdsk and
      "File.Instance = new RdskFS();" in program,
      "Candidate A's RDSK mount uses the production Unsupported write result")

registry = (ROOT / "guideXOS" / "OS" / "ApplicationServiceRegistry.cs").read_text(
    encoding="utf-8-sig"
)
file_dialog_test_start = registry.index("private static bool RunFileDialogServiceSelfTest(")
file_dialog_test_end = registry.index("private static bool RunShellServiceSelfTest(",
                                     file_dialog_test_start)
file_dialog_test = registry[file_dialog_test_start:file_dialog_test_end]
check('ApplicationFileDialogOutcome.Selected, "Programs/phase9.txt"' in file_dialog_test
      and "ApplicationFileDialogOutcome.Cancelled" in file_dialog_test
      and "TransientWindowCount == 0" in file_dialog_test,
      "production registry self-test covers selected, cancelled, and cleanup results")

desktop = (ROOT / "guideXOS" / "GUI" / "Desktop.cs").read_text(
    encoding="utf-8-sig"
)
message_box = (ROOT / "guideXOS" / "GUI" / "MessageBox.cs").read_text(
    encoding="utf-8-sig"
)
check("box.MarkDiagnosticOpenError();" in desktop and
      '"ERROR_WINDOW_BOUNDS=x="' in desktop,
      "error display records bounded visible-window evidence")
check("if (!value && _diagnosticOpenErrorPending)" in message_box and
      '"ERROR_WINDOW_DISMISSED=1"' in message_box and
      "_diagnosticOpenErrorPending = false;" in message_box,
      "error dismissal marker follows the real MessageBox hide transition")

print("AMCONV2G_APP_RUNTIME_SERVICES_ASSERTIONS=" + str(passed))
print("AMCONV2G_APP_RUNTIME_SERVICES_FAILURES=" + str(failed))
raise SystemExit(1 if failed else 0)
