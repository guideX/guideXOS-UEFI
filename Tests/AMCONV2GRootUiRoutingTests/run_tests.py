#!/usr/bin/env python3
"""Check that Computer Files exposes and routes the typed C# Root command."""

from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def require(condition: bool, message: str, counts: list[int]) -> None:
    counts[0] += 1
    if not condition:
        raise AssertionError(message)


def main() -> int:
    files = (ROOT / "guideXOS" / "DefaultApps" / "ComputerFiles.cs").read_text(
        encoding="utf-8"
    )
    desktop = (ROOT / "guideXOS" / "GUI" / "Desktop.cs").read_text(
        encoding="utf-8"
    )
    shell = (ROOT / "guideXOS" / "OS" / "ApplicationShellServices.cs").read_text(
        encoding="utf-8"
    )
    factories = (ROOT / "guideXOS" / "OS" / "ApplicationFactories.cs").read_text(
        encoding="utf-8"
    )
    model = (ROOT / "guideXOS" / "OS" / "ModernAppModel.cs").read_text(
        encoding="utf-8"
    )
    instance = (ROOT / "guideXOS" / "OS" / "ApplicationInstance.cs").read_text(
        encoding="utf-8"
    )
    service_tests = (ROOT / "guideXOS" / "OS" / "ApplicationServiceRegistry.cs").read_text(
        encoding="utf-8"
    )

    checks = [0]
    route_start = files.index("private void OpenCSharpRootThroughShellService()")
    route_end = files.index("private bool TryOpenDocumentThroughService", route_start)
    route = files[route_start:route_end]
    input_start = files.index("// Left navigation clicks (below toolbar)")
    input_end = files.index("// Scrollbar drag start", input_start)
    input_route = files[input_start:input_end]
    draw_start = files.index("// Left column (below toolbar only)")
    draw_end = files.index("// Right content panel", draw_start)
    draw_route = files[draw_start:draw_end]

    require('"Open Root"' in draw_route,
            "Computer Files draws the Open Root navigation row", checks)
    require("if (clickEdge" in input_route and
            "OpenCSharpRootThroughShellService();" in input_route,
            "a fresh left-click edge activates the Open Root row", checks)
    require("_serviceContext == null" in route and
            "_services.Shell == null" in route,
            "the action requires the owning application service context", checks)
    require('ApplicationShellOpenRequest.ForShellFolder(\n' in route and
            '"gxos.shell.root", "/"' in route,
            "the action creates a typed C# Root folder request", checks)
    require("_services.Shell.Begin(_serviceContext, request)" in route,
            "the production shell service receives the GUI request", checks)
    require("_services.Shell.Observe(_serviceContext," in route,
            "the UI observes the typed service result", checks)
    require("result.ResultCode" in route and
            "result.BoundedDiagnostic" in route,
            "typed launch failures and bounded details reach the UI", checks)
    require("Desktop.ShowOpenError(X + 60, Y + 60, message)" in route and
            "internal static void ShowOpenError" in desktop,
            "failures use the existing closeable Computer Files error window", checks)
    require("finally" in route and "request.Dispose()" in route and
            "begun.Dispose()" in route and "observed.Dispose()" in route,
            "the action releases request and service result wrappers", checks)
    require("File.Instance" not in route and
            "Desktop.LaunchApplication" not in route,
            "the action does not bypass the service or mutate the active mount", checks)
    require("ForShellFolder" not in route or
            '"gxos.shell.this-system"' not in route,
            "the action does not submit a Server shell alias", checks)
    require("case ApplicationShellOpenTargetKind.ShellFolder:" in shell and
            "OpenShellFolder(request.Target," in shell,
            "typed folder requests stay on the production shell dispatch branch", checks)
    require('RegisterInitial("gxos.builtin.files",\n                new ComputerFilesApplicationFactory())' in factories and
            "new ComputerFiles(" in factories,
            "the existing Computer Files factory owns typed activation", checks)
    require('resolution.ShellId != "gxos.shell.root"' in model and
            'ApplicationShellOperation.OpenFolder' in model,
            "folder resolution admits only the verified C# Root operation", checks)
    require("files.ShellPolicy.InstancePolicy ==\n" in model and
            "ApplicationInstancePolicy.MultiInstance" in model,
            "the existing Computer Files MultiInstance policy remains in place", checks)
    require("public static int ActiveCount" in instance and
            "TryTerminate" in instance,
            "activation continues to use the existing instance registry lifecycle", checks)
    require("RunTypedRootServiceRouteSelfTest(context, access)" in service_tests and
            '"gxos.shell.root", "/"' in service_tests,
            "the production AppModel self-test sends the same typed Root request", checks)
    require("GetOwnedWindowAt(0) is ComputerFiles" in service_tests and
            "secondHandle == firstHandle" in service_tests,
            "the service self-test checks factory output and MultiInstance behavior", checks)
    require("__amconv2g_r3_missing_root_probe__.txt" in service_tests and
            "missingObserve.Value.Value.AppId !=\n                            \"gxos.builtin.files\"" in service_tests,
            "a failed folder probe remains a Computer Files error without document fallback", checks)
    require('"gxos.shell.this-system", "/"' in service_tests and
            "ApplicationServiceResultCode.UnsupportedTarget" in service_tests,
            "the service self-test rejects an unrelated Server root alias", checks)
    require("mount.MountLeaseCount == baselineMountLeases" in service_tests,
            "service self-test checks successful and failed request lease cleanup", checks)

    print(f"AMCONV2G_ROOT_UI_ROUTING_SOURCE_ASSERTIONS={checks[0]}/{checks[0]}")
    print("Scope: production source wiring only; no GUI event or factory execution is claimed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
