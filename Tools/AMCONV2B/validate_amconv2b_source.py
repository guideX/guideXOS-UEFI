#!/usr/bin/env python3
"""Run AMCONV2B source-contract assertions without claiming runtime acceptance."""

from __future__ import annotations

import argparse
import re
from pathlib import Path


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


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--repo-root", type=Path, default=Path(__file__).resolve().parents[2]
    )
    args = parser.parse_args()
    repo = args.repo_root.resolve()

    def read(relative: str) -> str:
        return (repo / relative).read_text(encoding="utf-8-sig")

    modern = read("guideXOS/OS/ModernAppModel.cs")
    app_model = read("guideXOS/OS/AppModel.cs")
    app_services = read("guideXOS/OS/ApplicationServices.cs")
    services = read("guideXOS/OS/ApplicationShellServices.cs")
    factories = read("guideXOS/OS/ApplicationFactories.cs")
    computer_files = read("guideXOS/DefaultApps/ComputerFiles.cs")
    folder_contract = read("guideXOS/OS/ShellFolderTargetPath.cs")

    compatibility = method_body(
        modern, r"internal static bool TryMapToCSharpShellId\s*\("
    )
    modern_shell = modern[modern.index("public static class ModernShellAdapter") :]
    files_factory_class = factories[
        factories.index("internal sealed class ComputerFilesApplicationFactory") :
    ]
    adapter = method_body(
        modern_shell,
        r"public static bool TryCreateLaunchRequest\s*\(\s*string input,",
    )
    service = method_body(services, r"private static LaunchResult OpenShellObject\s*\(")
    shell_folder_service = method_body(
        services,
        r"private static LaunchResult OpenShellFolder\s*\(",
    )
    folder_resolver = method_body(
        factories,
        r"internal static bool TryResolveFolderRequest\s*\(",
    )
    files_factory = method_body(
        files_factory_class,
        r"public override bool TryCreateOrActivate\s*\(\s*ApplicationDescriptor descriptor,\s*ApplicationInstance instance,\s*LaunchRequest request,",
    )
    folder_window = method_body(
        computer_files,
        r"internal ComputerFiles\s*\(int x, int y, int w, int h, FileSystem fs,\s*string folderTitle, string initialFolderPath,",
    )
    folder_validator = method_body(
        folder_contract,
        r"internal static bool TryValidateCanonicalPath\s*\(",
    )
    canonical_validator = method_body(
        folder_contract,
        r"internal static bool IsCanonicalPath\s*\(",
    )

    checks: list[tuple[str, bool]] = []
    checks.append(
        (
            "AMCONV1 Files aliases remain explicit",
            compatibility is not None
            and all(
                token in compatibility
                for token in (
                    '"gxos.shell.files"',
                    '"Files"',
                    '"File Manager"',
                    '"FileExplorer"',
                    "CSharpFilesShellId",
                )
            ),
        )
    )
    checks.append(
        (
            "typed operations distinguish application, folder, panel, virtual, volume, and action",
            "enum ApplicationShellOperation" in modern
            and all(
                token in modern
                for token in (
                    "LaunchApplication",
                    "OpenFolder",
                    "ActivateSystemPanel",
                    "OpenVirtualObject",
                    "OpenDeviceVolume",
                    "InvokeSystemAction",
                )
            )
            and all(
                token in adapter
                for token in (
                    "ShellObjectKind.BuiltInApp",
                    "ShellObjectKind.FileSystemLocation",
                    "ShellObjectKind.SystemAction",
                    "ShellObjectKind.DeviceVolume",
                    "ShellObjectKind.SystemPanel",
                    "ShellObjectKind.VirtualObject",
                )
            ),
        )
    )
    checks.append(
        (
            "Server root and Documents stay unsupported",
            compatibility is not None
            and '"gxos.shell.this-system"' not in compatibility
            and '"gxos.shell.documents"' not in compatibility
            and '"gxos.shell.this-system"' in modern
            and '"gxos.shell.documents"' in modern
            and "IsKnownUnsupportedServerObject" in adapter,
        )
    )
    checks.append(
        (
            "C# Root alone binds to the C# Files backend",
            adapter is not None
            and 'resolution.ShellId == "gxos.shell.root"' in adapter
            and 'handlerAppId = "gxos.builtin.files"' in adapter
            and '? "/" : resolution.Path' in adapter
            and "ApplicationShellOperation.OpenFolder" in adapter
            and folder_resolver is not None
            and 'request.SourceShellObjectId != "gxos.shell.root"' in folder_resolver
            and 'File.Instance' in folder_resolver,
        )
    )
    checks.append(
        (
            "folder activation is preflighted and remains separate from other operations",
            service is not None
            and "ApplicationShellOperation.OpenFolder" in service
            and "TryResolveFolderRequest" in service
            and "ActivateSystemPanel" in service
            and "OpenVirtualObject" in service
            and "OpenDeviceVolume" in service
            and "LaunchModern(request)" in service,
        )
    )
    checks.append(
        (
            "bounded shell service carries a separate typed folder value",
            "ShellFolder" in app_services
            and "ForShellFolder" in app_services
            and "ShellFolderTargetPath.IsCanonicalPath" in app_services
            and "ApplicationShellOpenTargetKind.ShellFolder" in services
            and shell_folder_service is not None
            and "TryCreateFolderLaunchRequest" in shell_folder_service
            and "TryResolveFolderRequest" in shell_folder_service
            and "LaunchModern(request)" in shell_folder_service,
        )
    )
    checks.append(
        (
            "folder backend validates the typed request and selected mount",
            folder_resolver is not None
            and "LaunchRequestTargetKind.ShellObject" in folder_resolver
            and "ApplicationShellTargetKind.FileSystem" in folder_resolver
            and 'request.TargetAppId != "gxos.builtin.files"' in folder_resolver
            and "TryResolveExistingDirectory" in folder_resolver,
        )
    )
    checks.append(
        (
            "typed folder projection accepts only C# Root and uses ForShellObject",
            "TryCreateFolderLaunchRequest" in modern_shell
            and 'resolution.ShellId != "gxos.shell.root"' in modern_shell
            and "LaunchRequest.ForShellObject" in modern_shell
            and '"gxos.builtin.files"' in modern_shell,
        )
    )
    checks.append(
        (
            "directory probe verifies directory attributes and releases listings",
            "FileAttribute.Directory" in factories
            and "_fileSystem.GetFiles(parent)" in factories
            and "entries[i].Dispose()" in factories
            and "entries.Dispose()" in factories,
        )
    )
    checks.append(
        (
            "folder factory borrows the mounted filesystem without replacing or owning it",
            files_factory is not None
            and "csharpFolderRequest" in files_factory
            and "folderFileSystem" in files_factory
            and "folderPath, false" in files_factory
            and "File.Instance =" not in files_factory
            and "new AutoFS" not in files_factory[files_factory.find("if (csharpFolderRequest)"):files_factory.find("} else if (driveRequest)")],
        )
    )
    checks.append(
        (
            "Computer Files starts at the supplied path without building a drive chooser",
            folder_window is not None
            and "_fs = fs" in folder_window
            and "_ownsFileSystem = ownsFileSystem" in folder_window
            and "_showDrives = false" in folder_window
            and "_currentPath =" in folder_window
            and "RefreshDrives" not in folder_window,
        )
    )
    checks.append(
        (
            "canonical path contract enforces UTF bounds and rejects traversal syntax",
            folder_validator is not None
            and canonical_validator is not None
            and "MaxUtf8Bytes" in folder_contract
            and "MaxUtf16CodeUnits" in canonical_validator
            and "IsValidShellText" in folder_contract
            and "canonicalPath[0] != '/'" in canonical_validator
            and "segmentLength == 2" in canonical_validator
            and "fileSystemPath = canonicalPath.Substring(1)" in folder_validator,
        )
    )
    checks.append(
        (
            "folder values never enter document associations",
            "LaunchRequest.ForFile" not in folder_resolver
            and "ModernFileAssociationAdapter" not in service
            and "LaunchRequest.ForShellObject" in adapter,
        )
    )
    checks.append(
        (
            "existing Hard Disk and USB volume factory branch remains available",
            "TryCreateDriveFileSystem" in factories
            and 'TextEquals(driveName, "Hard Disk")' in factories
            and 'const string usbPrefix = "USB Drive "' in factories,
        )
    )

    failed = 0
    for index, (name, passed) in enumerate(checks, start=1):
        state = "PASS" if passed else "FAIL"
        print(f"AMCONV2B_SOURCE_CHECK_{index:02d}={state}: {name}")
        failed += not passed
    print(f"AMCONV2B_SOURCE_CHECKS={len(checks) - failed}/{len(checks)}")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
