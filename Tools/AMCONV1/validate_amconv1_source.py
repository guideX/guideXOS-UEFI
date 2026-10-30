#!/usr/bin/env python3
"""Repeat the nine documented AMCONV1 source-contract checks.

These checks inspect production sources and embedded test wiring. They do not
execute the UEFI App Model self-tests or claim hosted runtime acceptance.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--repo-root", type=Path, default=Path(__file__).resolve().parents[2]
    )
    parser.add_argument(
        "--server-root",
        type=Path,
        default=Path(__file__).resolve().parents[3] / "guideXOSServer",
    )
    args = parser.parse_args()
    repo = args.repo_root.resolve()
    server = args.server_root.resolve()

    def read(path: Path) -> str:
        return path.read_text(encoding="utf-8-sig")

    server_registry = read(server / "shell_object_registry.h")
    app_model = read(repo / "guideXOS/OS/AppModel.cs")
    modern = read(repo / "guideXOS/OS/ModernAppModel.cs")
    shell_services = read(repo / "guideXOS/OS/ApplicationShellServices.cs")
    program = read(repo / "guideXOS/Program.cs")
    taskbar = read(repo / "guideXOS/GUI/TaskbarApplicationEntry.cs")

    files_record = next(
        (
            line
            for line in server_registry.splitlines()
            if '"gxos.shell.files"' in line
        ),
        "",
    )
    files_aliases = re.search(
        r"kFilesAliases\[\]\s*=\s*\{(.*?)\};", server_registry, re.S
    )
    def method_body(source: str, signature: str) -> str | None:
        match = re.search(signature, source)
        if match is None:
            return None
        opening = source.find("{", match.end())
        if opening < 0:
            return None
        depth = 1
        for index in range(opening + 1, len(source)):
            if source[index] == "{":
                depth += 1
            elif source[index] == "}":
                depth -= 1
                if depth == 0:
                    return source[opening + 1 : index]
        return None

    csharp_map = method_body(modern, r"TryMapToCSharpShellId\s*\(")
    try_request = method_body(
        modern,
        r"public static bool TryCreateLaunchRequest\s*\(\s*string input,",
    )
    typed_action = method_body(
        modern, r"public static bool TryCreateTypedActionLaunchRequest\s*\("
    )
    association_block = method_body(
        app_model, r"public static void InitializeDefaultAssociations\s*\(\)"
    )
    taskbar_selftest = method_body(taskbar, r"public static bool RunSelfTest\s*\(\)")

    checks: list[tuple[str, bool]] = []
    checks.append(
        (
            "Server Files record and four registered aliases",
            '"gxos.shell.files"' in files_record
            and '"gxos.builtin.fileexplorer"' in files_record
            and files_aliases is not None
            and all(
                f'"{alias}"' in files_aliases.group(1)
                for alias in ("Files", "File Manager", "File Explorer", "FileExplorer")
            ),
        )
    )
    checks.append(
        (
            "C# canonical Files app and shell identity remain registered",
            'RegisterBuiltIn("gxos.builtin.files"' in app_model
            and '"gxos.shell.computerfiles"' in app_model
            and '"File Explorer"' in app_model,
        )
    )
    checks.append(
        (
            "Server identities map into the existing C# Files route",
            csharp_map is not None
            and all(
                token in csharp_map
                for token in (
                    '"gxos.shell.files"',
                    '"Files"',
                    '"File Manager"',
                    '"FileExplorer"',
                    "CSharpFilesShellId",
                )
            )
            and try_request is not None
            and "LaunchRequest.ForShellObject" in try_request
            and "ServerShellObjectCompatibilityAdapter.TryMapToCSharpShellId" in try_request
            and "ModernShellAdapter.TryCreateLaunchRequest" in shell_services
            and "LaunchModern(request)" in shell_services,
        )
    )
    checks.append(
        (
            "Alias provenance and repeated Server resolution remain stable",
            try_request is not None
            and "resolution.Input = input" in try_request
            and "resolution.MatchedAlias = input" in try_request
            and 'shellResolution.MatchedAlias == "gxos.shell.files"' in modern
            and "repeatedResolutionStable" in modern
            and "Count == descriptorCountBeforeRepeatedResolution" in modern,
        )
    )
    checks.append(
        (
            "Malformed, unsupported, and unknown inputs retain distinct error codes",
            try_request is not None
            and all(
                code in try_request
                for code in ("MalformedRequest", "UnsupportedTarget", "NotFound")
            )
            and 'shellFailure.ErrorCode == LaunchErrorCode.MalformedRequest' in modern
            and 'shellFailure.ErrorCode == LaunchErrorCode.UnsupportedTarget' in modern
            and 'shellFailure.ErrorCode == LaunchErrorCode.NotFound' in modern
            and "new char[LaunchRequest.MaxTextLength + 1]" in modern,
        )
    )
    checks.append(
        (
            "Typed-action service accepts only Action targets",
            typed_action is not None
            and "ApplicationShellTargetKind.Action" in typed_action
            and "LaunchErrorCode.UnsupportedTarget" in typed_action
            and "ModernShellAdapter.TryCreateTypedActionLaunchRequest" in shell_services,
        )
    )
    checks.append(
        (
            "All six existing file associations remain registered",
            association_block is not None
            and all(
                f'Register("{extension}"' in association_block
                for extension in (".txt", ".png", ".bmp", ".wav", ".gxm", ".mue")
            )
            and "FileAssociationRegistry.RunSelfTest()" in program,
        )
    )
    checks.append(
        (
            "App Model diagnostic path wires descriptor, lifecycle, and service suites",
            "#if UEFI_DIAGNOSTIC_APP_MODEL" in program
            and all(
                token in program
                for token in (
                    "ApplicationDescriptorRegistry.RunSelfTest()",
                    "ApplicationInstanceRegistry.RunSelfTest()",
                    "ApplicationInstanceRegistry.RunLifecycleSelfTest()",
                    "ApplicationServiceRegistry.RunSelfTest()",
                )
            ),
        )
    )
    checks.append(
        (
            "Taskbar regression retains same-instance no-lifecycle-churn assertion",
            taskbar_selftest is not None
            and "sameInstanceSwitchNoLifecycleChurn" in taskbar_selftest
            and '"same instance Window switch"' in taskbar_selftest,
        )
    )

    failed = 0
    for index, (name, passed) in enumerate(checks, start=1):
        state = "PASS" if passed else "FAIL"
        print(f"AMCONV1_SOURCE_CHECK_{index:02d}={state}: {name}")
        failed += not passed
    print(f"AMCONV1_SOURCE_CHECKS={len(checks) - failed}/{len(checks)}")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
