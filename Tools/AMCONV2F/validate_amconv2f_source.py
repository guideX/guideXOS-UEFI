#!/usr/bin/env python3
"""Source-contract checks for the opt-in RDSK v2 integration."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path,
                        default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    root = args.repo_root.resolve()

    def read(relative: str) -> str:
        return (root / relative).read_text(encoding="utf-8-sig")

    inspector = read("Kernel/FS/RdskFSDirectoryInspector.cs")
    reader = read("Kernel/FS/RdskFS.cs")
    builder = read("Tools/ramdisk_builder.py")
    managed_artifacts = read("Tools/ManagedArtifacts/managed_artifacts.py")
    build_script = read("build.ps1")
    hosted_project = read("Tests/AMCONV2FRdskV2Tests/AMCONV2FRdskV2Tests.csproj")
    hosted_runner = read("Tests/AMCONV2FRdskV2Tests/run_tests.py")
    folder_binding = read("guideXOS/OS/ShellFolderMountBinding.cs")
    checks = [
        ("the production parser detects v1 and v2 and rejects unknown versions",
         "imageVersion == Version1" in inspector and
         "imageVersion == Version2" in inspector and
         "return false;" in inspector),
        ("v2 checks header, captured size, table, records, and checksum bounds",
         "declaredImageBytes != imageSizeBytes" in inspector and
         "recordsOffset != V2HeaderBytes" in inspector and
         "ComputeImageCrc32(image, imageSizeBytes)" in inspector),
        ("directory parent references and sibling order are validated",
         "parentIndex >= i" in inspector and
         "recordKinds[parentIndex]" in inspector and
         "CompareNames" in inspector),
        ("RDSK operations validate the retained pointer and captured image size",
         "ValidateImage(_imageBase" in reader and
         "_imageSizeBytes" in reader and
         "Inspect(_imageBase" in reader),
        ("production listing and reading retain distinct v1 and v2 paths",
         "GetV2Files" in reader and "GetV1Files" in reader and
         "ReadV2File" in reader and "ReadV1File" in reader),
        ("the build defaults to v1 and selects v2 only through a separate image path",
         "default=1" in builder and
         "[string]$RamdiskVersion = '1'" in build_script and
         "ramdisk-v2.img" in build_script and
         "if ($RamdiskVersion -eq '2')" in build_script and
         "@('--version', '2')" in build_script and
         "RDSK v2 cannot be written to the normal ramdisk.img path" in build_script and
         '"--expected-version", [int]$RamdiskVersion' in build_script and
         'parser.add_argument("--expected-version", type=int, choices=(1, 2))' in managed_artifacts),
        ("v2 names are strict ASCII and generator record order is stable",
         "errors=\"strict\"" in builder and
         "encoded_entries.sort(key=lambda pair: pair[0])" in builder),
        ("hosted tests compile production reader and binding with generated images",
         "../../Kernel/FS/RdskFS.cs" in hosted_project and
         "ShellFolderMountBinding.cs" in hosted_project and
         "ramdisk_builder.py" in hosted_runner),
        ("folder binding remains the existing typed C# Root route",
         "gxos.shell.root" in folder_binding and
         "TryInspectFolderPath" in folder_binding),
    ]

    failed = 0
    for index, (label, passed) in enumerate(checks, 1):
        state = "PASS" if passed else "FAIL"
        print(f"AMCONV2F_SOURCE_CHECK_{index:02d}={state}: {label}")
        failed += 0 if passed else 1
    print(f"AMCONV2F_SOURCE_CHECKS={len(checks) - failed}/{len(checks)}")
    return 1 if failed else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"AMCONV2F_SOURCE_ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
