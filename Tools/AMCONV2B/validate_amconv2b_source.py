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
    folder_binding = read("guideXOS/OS/ShellFolderMountBinding.cs")

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
        folder_binding,
        r"internal static bool TryResolveFolderRequest\s*\(",
    )
    folder_binder = method_body(
        folder_binding,
        r"internal static bool TryBindFolderRequest\s*\(",
    )
    folder_request_validator = method_body(
        folder_binding,
        r"private static bool ValidateFolderRequest\s*\(",
    )
    prepared_folder_launch = method_body(
        services,
        r"private static LaunchResult LaunchPreparedFolderRequest\s*\(",
    )
    open_document = method_body(
        services,
        r"private static LaunchResult OpenDocument\s*\(",
    )
    document_reader = method_body(
        factories,
        r"internal static byte\[\] ReadDocumentBytes\s*\(",
    )
    mount_lifetime = read("Kernel/FS/FileSystemMountLifetime.cs")
    filesystem = read("Kernel/FS/FileSystem.cs")
    filesystem_inspection = read("Kernel/FS/FileSystemDirectoryInspection.cs")
    fat_filesystem = read("Kernel/FS/FAT.cs")
    auto_filesystem = read("Kernel/FS/AutoFS.cs")
    fat_runtime_tests = read("Tests/AMCONV2DFatInspectionTests/Program.cs")
    mount_tests = read("Tests/AMCONV2BFolderTargetTests/Program.cs")
    files_factory = method_body(
        files_factory_class,
        r"public override bool TryCreateOrActivate\s*\(\s*ApplicationDescriptor descriptor,\s*ApplicationInstance instance,\s*LaunchRequest request,",
    )
    folder_window = method_body(
        computer_files,
        r"internal ComputerFiles\s*\(int x, int y, int w, int h, FileSystem fs,\s*string folderTitle, string initialFolderPath,",
    )
    folder_ensure_entries = method_body(
        computer_files, r"private void EnsureEntries\s*\(\s*\)"
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
            and folder_request_validator is not None
            and 'request.SourceShellObjectId != "gxos.shell.root"' in folder_request_validator
            and 'request.FileSystemMountLease' in folder_resolver
            and 'lease.IsValid' in folder_resolver,
        )
    )
    checks.append(
        (
            "folder activation is preflighted and remains separate from other operations",
            service is not None
            and "ApplicationShellOperation.OpenFolder" in service
            and "LaunchPreparedFolderRequest" in service
            and "ActivateSystemPanel" in service
            and "OpenVirtualObject" in service
            and "OpenDeviceVolume" in service,
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
            and "LaunchPreparedFolderRequest" in shell_folder_service
            and prepared_folder_launch is not None
            and "TryBindFolderRequest" in prepared_folder_launch
            and "LaunchModern(request)" in prepared_folder_launch
            and "request.Dispose()" in prepared_folder_launch,
        )
    )
    checks.append(
        (
            "folder backend validates the typed request and selected mount",
            folder_resolver is not None
            and "LaunchRequestTargetKind.ShellObject" in folder_request_validator
            and "ApplicationShellTargetKind.FileSystem" in folder_request_validator
            and 'request.TargetAppId != "gxos.builtin.files"' in folder_request_validator
            and "TryInspectFolderPath" in folder_resolver
            and "lease.TryClone(out operationLease)" in folder_resolver
            and "operationLease.Dispose()" in folder_resolver
            and folder_binder is not None
            and "TryAcquireActive" in folder_binder
            and "AttachFileSystemMountLease" in folder_binder,
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
            "folder validation uses the retained filesystem's typed inspection API",
            "_fileSystem.InspectDirectory(path)" in folder_binding
            and "FileSystemDirectoryInspectionStatus.Directory" in folder_binding
            and "_fileSystem.GetFiles(" not in folder_binding,
        )
    )
    checks.append(
        (
            "folder factory borrows the mounted filesystem without replacing or owning it",
            files_factory is not None
            and "csharpFolderRequest" in files_factory
            and "folderFileSystem" in files_factory
            and "folderWindowLease" in files_factory
            and "instanceLease.TryClone" in files_factory
            and "File.Instance =" not in files_factory
            and "new AutoFS" not in files_factory[files_factory.find("if (csharpFolderRequest)"):files_factory.find("} else if (driveRequest)")],
        )
    )
    checks.append(
        (
            "Computer Files starts at the supplied path without building a drive chooser",
            folder_window is not None
            and "_fs = fs" in folder_window
            and "_mountLease = mountLease" in folder_window
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
    checks.append(
        (
            "active mount replacement and disposal are intercepted by the lifetime boundary",
            "public static FileSystem Instance {" in filesystem
            and "FileSystemMountLifetime.SetActiveFileSystem(value)" in filesystem
            and "public override void Dispose()" in filesystem
            and "FileSystemMountLifetime.RequestDisposal(this)" in filesystem
            and "resource.DisposeMountResource()" in mount_lifetime,
        )
    )
    checks.append(
        (
            "mount lease generations remain unique across replacement and final release",
            "_generation++" in mount_lifetime
            and "_generationExhausted" in mount_lifetime
            and "TryAddRetainedMountLease" in mount_lifetime
            and "lease.ReleaseMountLease()" not in mount_lifetime
            and "resource.ReleaseMountLease()" in mount_lifetime,
        )
    )
    checks.append(
        (
            "folder inspection maps malformed, missing, permission, and backend failures distinctly",
            "FolderFailureToLaunchError" in folder_binding
            and "ShellFolderTargetFailure.MalformedRequest" in folder_binding
            and "ShellFolderTargetFailure.PermissionDenied" in folder_binding
            and "ShellFolderTargetFailure.MountInvalidated" in folder_binding
            and "ShellFolderTargetFailure.IoFailure" in folder_binding
            and "ShellFolderTargetFailure.BackendUnavailable" in folder_binding
            and "ShellFolderTargetFailure.IndeterminateBackendFailure" in folder_binding,
        )
    )
    checks.append(
        (
            "folder-associated document reads preserve the source mount",
            "TryClone(out documentLease)" in computer_files
            and "ForDocumentFromMount" in computer_files
            and open_document is not None
            and "TakeFileSystemMountLease" in open_document
            and document_reader is not None
            and "lease.TryClone(out operationLease)" in document_reader
            and "operationLease.FileSystem.ReadAllBytes" in document_reader
            and "_sourceMountLease.TryClone(out operationLease)" in read("guideXOS/DefaultApps/Notepad.cs")
            and "operationLease.FileSystem.TryWriteAllBytes" in read("guideXOS/DefaultApps/Notepad.cs"),
        )
    )
    checks.append(
        (
            "launch request and shell-service request release mount ownership idempotently",
            "internal bool AttachFileSystemMountLease" in modern
            and "internal FileSystemMountLease TakeFileSystemMountLease" in modern
            and "if (_disposed) return" in modern
            and "if (_disposed) return" in app_services
            and "finally {\n                request.Dispose();" in services,
        )
    )
    checks.append(
        (
            "hosted lifetime regressions exercise replacement, shared owners, and cleanup",
            "replacement retires the old generation" in mount_tests
            and "two folder owners acquire independent leases" in mount_tests
            and "final release completes deferred disposal" in mount_tests
            and "repeated acquire/replace/release cycles" in mount_tests
            and "ShellFolderMountBinding.TryBindFolderRequest" in mount_tests
            and "production folder binding retains the inspected filesystem" in mount_tests
            and "invalidated folder request fails without falling back" in mount_tests
            and "reinspection stays on the request's mount after replacement" in mount_tests,
        )
    )
    checks.append(
        (
            "folder and document I/O retain operation leases through synchronous calls",
            folder_ensure_entries is not None
            and "ownerLease.TryClone(out operationLease)" in folder_ensure_entries
            and "_entriesCache = _fs.GetFiles(query)" in folder_ensure_entries
            and "must never fall back to a newly" in folder_ensure_entries
            and "if (operationLease != null) operationLease.Dispose()" in folder_ensure_entries,
        )
    )
    checks.append(
        (
            "filesystem inspection contract distinguishes all required typed outcomes",
            all(
                token in filesystem_inspection
                for token in (
                    "Directory",
                    "NotFound",
                    "NotDirectory",
                    "AccessDenied",
                    "FilesystemUnavailable",
                    "MountUnavailable",
                    "MountInvalidated",
                    "IoFailure",
                    "InvalidPath",
                    "Indeterminate",
                )
            )
            and "Status" in filesystem_inspection
            and "BackendCode" in filesystem_inspection
            and "FileSystemDirectoryInspectionStatus.Indeterminate" in filesystem,
        )
    )
    checks.append(
        (
            "FAT inspection uses typed path and disk outcomes without changing GetFiles",
            "DirResult result = FindPath(path)" in fat_filesystem
            and "_stickyIoResult" in fat_filesystem
            and "FileSystemDirectoryInspectionStatus.NotFound" in fat_filesystem
            and "FileSystemDirectoryInspectionStatus.NotDirectory" in fat_filesystem
            and "public override List<FileInfo> GetFiles(string Directory)" in fat_filesystem,
        )
    )
    checks.append(
        (
            "AutoFS delegates inspection to its selected implementation",
            "return _impl.InspectDirectory(path)" in auto_filesystem
            and "_impl.GetFiles(Directory)" in auto_filesystem,
        )
    )
    checks.append(
        (
            "hosted FAT inspection suite exercises production FAT and Disk code",
            "new FAT(disk, false)" in fat_runtime_tests
            and "CreateFat12Image()" in fat_runtime_tests
            and "DiskIoResult.TransportFailure" in fat_runtime_tests
            and "DiskIoResult.MediaUnavailable" in fat_runtime_tests
            and "FileSystemDirectoryInspectionStatus.NotFound" in fat_runtime_tests
            and "FileSystemDirectoryInspectionStatus.NotDirectory" in fat_runtime_tests,
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
