#!/usr/bin/env python3
"""Fail-closed BUILD1C preflight and single-variant artifact verification.

The script never creates an executable or edits the production identity
generator. Build orchestration stays in Invoke-BUILD1C.ps1, which calls the
supported Phase 29 builder and stage script. This module checks their inputs
and validates the resulting single row with the existing native validator.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from typing import Any, Iterable


ROOT = Path(__file__).resolve().parents[2]
VARIANTS = ("success", "title-failure", "body-failure", "invalid-type", "failfast")
PHASE23_PACK_REL = "out/dotnet/phase23-runtime-pack"
PACKAGE_VERSION = "9.0.0"
REQUIRED_FREE_BYTES = 3 * 1024 * 1024 * 1024
INPUT_DIRS = (
    "Tools/Phase19",
    "Tools/Phase20",
    "Tools/Phase21",
    "Tools/Phase22",
    "Tools/Phase23",
    "Tools/Phase24",
    "Tools/Phase26",
    "Tools/Phase29",
    "Tools/ManagedArtifacts",
    "GuideXos.User",
    "UserManagedNotificationProof",
)
INPUT_FILES = (
    "Tools/Find-VcVars64.ps1",
    "Tools/inspect_managed_image.py",
    "Tools/Phase34/phase34_flags.py",
    "Tools/Phase35/phase35_flags.py",
)

sys.path.insert(0, str(ROOT / "Tools" / "ManagedArtifacts"))
import managed_artifacts as identities  # noqa: E402


class Build1CError(RuntimeError):
    """An input or output did not satisfy the BUILD1C contract."""


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise Build1CError(f"cannot read JSON evidence {path}: {exc}") from exc
    if not isinstance(value, dict):
        raise Build1CError(f"JSON evidence is not an object: {path}")
    return value


def stable_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def canonical_json_sha256(value: Any) -> str:
    encoded = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return sha256_bytes(encoded)


def require_hash(actual: str, expected: str, label: str) -> None:
    if actual.upper() != expected.upper():
        raise Build1CError(f"{label} hash mismatch: expected {expected.upper()}, found {actual.upper()}")


def require_commit(actual: str, expected: str, label: str = "source") -> None:
    if not re.fullmatch(r"[0-9a-fA-F]{40}", expected) or actual.lower() != expected.lower():
        raise Build1CError(f"{label} commit mismatch: expected {expected}, found {actual}")


def canonical_text_sha256(path: Path) -> str:
    data = path.read_bytes()
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise Build1CError(f"repository template is not UTF-8 text: {path}") from exc
    text = text.replace("\r\n", "\n")
    if "\r" in text:
        raise Build1CError(f"repository template has a lone carriage return: {path}")
    return sha256_bytes(text.encode("utf-8"))


def run_checked(args: list[str], *, cwd: Path | None = None) -> subprocess.CompletedProcess[str]:
    try:
        return subprocess.run(args, cwd=cwd, text=True, capture_output=True, check=True)
    except (OSError, subprocess.CalledProcessError) as exc:
        detail = getattr(exc, "stderr", "") or str(exc)
        raise Build1CError(f"command failed: {' '.join(args)}: {detail.strip()}") from exc


def git_text(root: Path, *args: str) -> str:
    return run_checked(["git", "-C", str(root), *args]).stdout.strip()


def parse_porcelain(lines: Iterable[str]) -> tuple[set[str], set[str]]:
    tracked: set[str] = set()
    untracked: set[str] = set()
    for line in lines:
        if len(line) < 4:
            raise Build1CError(f"unrecognized runtime git status entry: {line!r}")
        status = line[:2]
        path = line[3:].replace("\\", "/")
        if status == " M":
            tracked.add(path)
        elif status == "??":
            untracked.add(path)
        else:
            raise Build1CError(f"runtime cache has unsupported git status {status!r} for {path}")
    return tracked, untracked


def validate_runtime_tree(root: Path) -> dict[str, Any]:
    runtime_root = root / "out" / "rt"
    if not runtime_root.is_dir():
        raise Build1CError(f"required pinned runtime source is missing: {runtime_root}")

    lock_paths = {
        "phase20": root / "Tools/Phase20/runtime-source.lock.json",
        "phase21": root / "Tools/Phase21/runtime-source.lock.json",
        "phase22": root / "Tools/Phase22/runtime-source.lock.json",
        "phase23": root / "Tools/Phase23/runtime-source.lock.json",
        "recovery": root / "Tools/Phase23/runtime-source-cache-recovery.lock.json",
    }
    locks = {name: load_json(path) for name, path in lock_paths.items()}
    expected_commit = str(locks["phase20"].get("source", {}).get("commit", ""))
    commits = (
        expected_commit,
        str(locks["phase21"].get("runtimeCommit", "")),
        str(locks["phase22"].get("runtimeCommit", "")),
        str(locks["phase23"].get("runtimeCommit", "")),
        str(locks["recovery"].get("runtimeCommit", "")),
    )
    if not expected_commit or any(commit.lower() != expected_commit.lower() for commit in commits):
        raise Build1CError("Phase 20-23 runtime locks do not agree on one pinned source commit")
    actual_commit = git_text(runtime_root, "rev-parse", "HEAD")
    require_commit(actual_commit, expected_commit, "runtime source")

    status = run_checked([
        "git", "-C", str(runtime_root), "status", "--porcelain=v1", "--untracked-files=all"
    ]).stdout.rstrip("\r\n")
    actual_tracked, actual_untracked = parse_porcelain(status.splitlines())
    recovery = locks["recovery"]
    expected_tracked = {str(item["path"]).replace("\\", "/") for item in recovery["trackedFiles"]}
    templates = [item for item in recovery["requiredTemplates"]]
    template_paths = {str(item["path"]).replace("\\", "/") for item in templates}
    generated = {str(item).replace("\\", "/") for item in recovery["allowedGeneratedArtifacts"]}
    if actual_tracked != expected_tracked:
        raise Build1CError("runtime tracked-file status does not match the locked Phase 21/22/23 plus Phase 26 profile")
    if actual_untracked != template_paths | generated:
        raise Build1CError("runtime untracked-file status does not match the locked templates and allowed generated files")

    for item in recovery["trackedFiles"]:
        relative = str(item["path"]).replace("/", "\\")
        path = runtime_root / relative
        if not path.is_file():
            raise Build1CError(f"locked runtime source file is missing: {relative}")
        require_hash(sha256_file(path), str(item["sha256"]), f"runtime source {relative}")

    phase21_templates = {
        str(path).replace("\\", "/")
        for path in locks["phase21"].get("repositoryOwnedTemplates", [])
    }
    phase22_templates = {
        str(path).replace("\\", "/")
        for path in locks["phase22"].get("repositoryOwnedTemplates", [])
    }
    template_results = []
    for item in templates:
        relative = str(item["path"]).replace("\\", "/")
        expected = str(item["sha256"]).upper()
        template_root = root / ("Tools/Phase22/runtime" if relative in phase22_templates else "Tools/Phase21/runtime")
        repository_template = template_root / relative.replace("/", "\\")
        runtime_template = runtime_root / relative.replace("/", "\\")
        if not repository_template.is_file() or not runtime_template.is_file():
            raise Build1CError(f"required runtime patch template is missing: {relative}")
        require_hash(canonical_text_sha256(repository_template), expected, f"canonical repository template {relative}")
        require_hash(sha256_file(runtime_template), expected, f"runtime template {relative}")
        template_results.append({
            "path": relative,
            "sha256": expected,
            "repositoryTemplateCanonicalized": True,
            "runtimeCopyVerified": True,
        })

    for relative in generated:
        if not (runtime_root / relative.replace("/", "\\")).is_file():
            raise Build1CError(f"allowlisted runtime build output is missing: {relative}")

    patch_records = (
        ("phase21", root / "out/dotnet/phase21-source-patch/phase21-source-patch-record.json"),
        ("phase22", root / "out/dotnet/phase22-source-patch/phase22-source-patch-record.json"),
        ("phase23", root / "out/dotnet/phase23-source-patch/phase23-ilc-source-patch-record.json"),
    )
    patch_evidence = []
    for phase, path in patch_records:
        if not path.is_file():
            raise Build1CError(f"verified {phase} patch evidence is missing: {path}")
        record = load_json(path)
        record_commit = str(record.get("sourceCommit", record.get("source", {}).get("commit", "")))
        require_commit(record_commit, expected_commit, f"{phase} patch evidence")
        if phase == "phase23" and record.get("patchIdentity") != locks["phase23"].get("ilcPatchIdentity"):
            raise Build1CError("Phase 23 patch evidence has the wrong ILC patch identity")
        if phase == "phase23":
            for entry in record.get("changedFiles", []):
                path_in_runtime = runtime_root / str(entry["path"]).replace("/", "\\")
                if not path_in_runtime.is_file():
                    raise Build1CError(f"Phase 23 changed source is missing: {entry['path']}")
                require_hash(sha256_file(path_in_runtime), str(entry["sha256"]), f"Phase 23 source {entry['path']}")
        patch_evidence.append({
            "path": path.relative_to(root).as_posix(),
            "sha256": sha256_file(path),
            "recoveredFromVerifiedCache": bool(record.get("recoveredFromVerifiedCache", False)),
        })

    return {
        "commit": expected_commit,
        "gitStatusEntries": len(status.splitlines()) if status else 0,
        "lockedTrackedFileCount": len(expected_tracked),
        "lockedTemplateCount": len(template_results),
        "allowedGeneratedArtifactCount": len(generated),
        "templates": sorted(template_results, key=lambda value: value["path"]),
        "patchRecords": patch_evidence,
        "locks": [
            {"path": path.relative_to(root).as_posix(), "sha256": sha256_file(path)}
            for path in lock_paths.values()
        ],
    }


def _xml_identity(nupkg: Path) -> tuple[str, str, str, str | None]:
    try:
        with zipfile.ZipFile(nupkg) as archive:
            nuspecs = [name for name in archive.namelist() if name.lower().endswith(".nuspec")]
            if len(nuspecs) != 1:
                raise Build1CError(f"package must contain exactly one .nuspec: {nupkg.name}")
            xml_root = ET.fromstring(archive.read(nuspecs[0]))
            values: dict[str, list[str]] = {"id": [], "version": []}
            for node in xml_root.iter():
                leaf = node.tag.rsplit("}", 1)[-1].lower()
                if leaf in values and node.text:
                    values[leaf].append(node.text.strip())
            if len(values["id"]) != 1 or len(values["version"]) != 1:
                raise Build1CError(f"package .nuspec identity is missing or ambiguous: {nupkg.name}")
            ilc_names = [name for name in archive.namelist() if name.lower() == "tools/ilc.exe"]
            ilc_sha = sha256_bytes(archive.read(ilc_names[0])) if len(ilc_names) == 1 else None
            if len(ilc_names) > 1:
                raise Build1CError(f"package contains multiple tools/ilc.exe entries: {nupkg.name}")
            return values["id"][0], values["version"][0], nuspecs[0], ilc_sha
    except (OSError, zipfile.BadZipFile, ET.ParseError) as exc:
        raise Build1CError(f"cannot inspect NuGet package {nupkg}: {exc}") from exc


def validate_package_bundle(pack_root: Path, evidence_path: Path, expected_commit: str) -> dict[str, Any]:
    if not evidence_path.is_file():
        raise Build1CError(f"supplementary runtime package evidence is missing: {evidence_path}")
    manifest_path = pack_root / "guidexos-runtime-pack.manifest.json"
    if not manifest_path.is_file():
        raise Build1CError(f"Phase 23 runtime-pack manifest is missing: {manifest_path}")
    manifest = load_json(manifest_path)
    evidence = load_json(evidence_path)
    if manifest.get("target") != "guidexos-x64" or manifest.get("targetOs") != "guidexos":
        raise Build1CError("runtime-pack target identity is not guidexos-x64/guidexos")
    if manifest.get("stockWinX64Fallback") is not False or manifest.get("customSourceAdaptationComplete") is not True:
        raise Build1CError("runtime pack allows fallback or is not marked source-adapted")
    if manifest.get("managedCoreLibAdaptationComplete") not in (False, None):
        raise Build1CError("the Phase 23 input pack is unexpectedly pre-adapted as a Phase 26 pack")
    require_commit(str(manifest.get("sourceCommit", "")), expected_commit, "runtime-pack")
    require_hash(sha256_file(manifest_path), str(evidence.get("materializedManifestSha256", "")), "materialized runtime manifest")
    source_manifest = evidence_path.parent / "phase23-runtime-pack.source-manifest.json"
    if not source_manifest.is_file():
        raise Build1CError(f"original Phase 23 manifest snapshot is missing: {source_manifest}")
    require_hash(
        sha256_file(source_manifest),
        str(evidence.get("runtimePackSourceManifestSha256", "")),
        "original Phase 23 manifest snapshot",
    )

    manifest_packages = {Path(str(item.get("path", ""))).name: item for item in manifest.get("packages", [])}
    evidence_packages = {str(item.get("file", "")): item for item in evidence.get("packages", [])}
    if not manifest_packages or set(manifest_packages) != set(evidence_packages):
        raise Build1CError("runtime package evidence does not cover the exact manifest package set")
    verified_packages = []
    embedded_ilc_hashes: set[str] = set()
    for name, item in sorted(manifest_packages.items()):
        path = pack_root / "packages" / name
        if not path.is_file():
            raise Build1CError(f"required runtime package is missing: {path}")
        evidence_item = evidence_packages[name]
        actual_sha = sha256_file(path)
        require_hash(actual_sha, str(evidence_item.get("sha256", "")), f"runtime package {name}")
        if path.stat().st_size != int(evidence_item.get("length", -1)):
            raise Build1CError(f"runtime package length mismatch: {name}")
        package_id, version, nuspec, embedded_ilc = _xml_identity(path)
        if package_id != item.get("id") or version != item.get("version") or version != PACKAGE_VERSION:
            raise Build1CError(f"NuGet identity mismatch for {name}: {package_id} {version}")
        if package_id != evidence_item.get("packageId") or version != evidence_item.get("version") or nuspec != evidence_item.get("nuspec"):
            raise Build1CError(f"package .nuspec evidence mismatch: {name}")
        if embedded_ilc != evidence_item.get("embeddedIlcSha256"):
            raise Build1CError(f"embedded private compiler evidence mismatch: {name}")
        if embedded_ilc:
            embedded_ilc_hashes.add(embedded_ilc)
        verified_packages.append({"id": package_id, "version": version, "file": name, "length": path.stat().st_size, "sha256": actual_sha})

    private_record_path = Path(str(manifest.get("privateIlcBuildRecord", "")))
    if not private_record_path.is_file():
        raise Build1CError(f"private ILC build record is missing: {private_record_path}")
    private_record = load_json(private_record_path)
    require_commit(str(private_record.get("runtimeSourceCommit", "")), expected_commit, "private ILC")
    if private_record.get("ilcPatchIdentity") != manifest.get("privateIlcPatchIdentity"):
        raise Build1CError("private ILC patch identity does not match the runtime pack")
    private_exe_path = Path(str(private_record.get("privateCompiler", {}).get("path", "")))
    if not private_exe_path.is_file():
        raise Build1CError(f"private ILC executable is missing: {private_exe_path}")
    private_sha = sha256_file(private_exe_path)
    require_hash(private_sha, str(private_record.get("privateCompiler", {}).get("sha256", "")), "private ILC")
    if private_sha not in embedded_ilc_hashes:
        raise Build1CError("private ILC bytes do not match the compiler package")

    asset_evidence = {str(item.get("name")): item for item in evidence.get("runtimeAssets", [])}
    verified_assets = []
    for asset in manifest.get("assets", []):
        name = str(asset.get("name", ""))
        path = Path(str(asset.get("source", "")))
        if not path.is_file():
            raise Build1CError(f"required runtime-pack asset is missing: {path}")
        actual = sha256_file(path)
        require_hash(actual, str(asset.get("sha256", "")), f"runtime asset {name}")
        evidence_item = asset_evidence.get(name)
        if evidence_item is None:
            raise Build1CError(f"supplementary runtime evidence is missing asset {name}")
        require_hash(actual, str(evidence_item.get("sha256", "")), f"supplementary runtime asset {name}")
        verified_assets.append({"name": name, "sha256": actual, "length": path.stat().st_size})
    if set(asset_evidence) != {str(item.get("name")) for item in manifest.get("assets", [])}:
        raise Build1CError("supplementary runtime evidence contains an incomplete or extra asset set")

    return {
        "manifest": {"path": str(manifest_path), "sha256": sha256_file(manifest_path), "schema": manifest.get("schema")},
        "privateIlc": {"path": str(private_exe_path), "length": private_exe_path.stat().st_size, "sha256": private_sha},
        "packages": verified_packages,
        "assets": sorted(verified_assets, key=lambda item: item["name"]),
        "packageArchiveHashesAreSupplementaryMeasurements": True,
    }


def _version(path: Path, args: list[str], *, cwd: Path | None = None) -> str:
    result = run_checked([str(path), *args], cwd=cwd)
    return (result.stdout or result.stderr).strip().splitlines()[0]


def discover_toolchain(root: Path) -> dict[str, Any]:
    if os.name != "nt":
        raise Build1CError("BUILD1C managed payload builds require the supported Windows/MSVC host")
    system_root = Path(os.environ.get("SystemRoot", r"C:\Windows"))
    powershell = system_root / "System32/WindowsPowerShell/v1.0/powershell.exe"
    if not powershell.is_file():
        raise Build1CError(f"Windows PowerShell 5.1 is missing: {powershell}")
    find_helper = root / "Tools/Find-VcVars64.ps1"
    expression = f". '{str(find_helper).replace(chr(39), chr(39)+chr(39))}'; $p=Find-VcVars64 $null; if($p){{Write-Output $p}}"
    ps = run_checked([str(powershell), "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", expression])
    vcvars = Path(ps.stdout.strip().splitlines()[-1]) if ps.stdout.strip() else None
    if vcvars is None or not vcvars.is_file():
        raise Build1CError("Find-VcVars64 did not locate a supported x64 Visual Studio environment")
    vs_root = vcvars.parents[3]
    msvc_root = vs_root / "VC/Tools/MSVC"
    compilers = sorted(msvc_root.glob("*/bin/Hostx64/x64/cl.exe"), reverse=True)
    linkers = sorted(msvc_root.glob("*/bin/Hostx64/x64/link.exe"), reverse=True)
    librarians = sorted(msvc_root.glob("*/bin/Hostx64/x64/lib.exe"), reverse=True)
    if not compilers or not linkers or not librarians:
        raise Build1CError("selected Visual Studio installation lacks x64 cl/link/lib tools")
    sdk_root = Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) / "Windows Kits/10/Include"
    sdks = [p for p in sdk_root.iterdir() if (p / "ucrt/crtdbg.h").is_file() and (p / "um/windows.h").is_file()] if sdk_root.is_dir() else []
    sdks.sort(key=lambda path: tuple(int(part) for part in path.name.split(".")), reverse=True)
    if not sdks:
        raise Build1CError("a complete Windows SDK with UCRT headers is missing")
    nasm = Path(os.environ["NASM"]) if os.environ.get("NASM") and Path(os.environ["NASM"]).is_file() else root / "Tools/nasm.exe"
    if not nasm.is_file():
        found = shutil.which("nasm")
        nasm = Path(found) if found else nasm
    dotnet = shutil.which("dotnet")
    runtime_dotnet = root / "out/rt/.dotnet/dotnet.exe"
    if not dotnet or not runtime_dotnet.is_file() or not nasm.is_file():
        raise Build1CError("dotnet, the pinned runtime dotnet host, or NASM is missing")
    sdk_versions = run_checked([dotnet, "--list-sdks"]).stdout.splitlines()
    if not sdk_versions:
        raise Build1CError("dotnet SDK inventory is empty")
    ps_version = run_checked([str(powershell), "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()"]).stdout.strip()
    return {
        "powershell": {"path": str(powershell), "version": ps_version},
        "vcvars64": str(vcvars),
        "visualStudioRoot": str(vs_root),
        "cl": {"path": str(compilers[0]), "sha256": sha256_file(compilers[0]), "fileVersion": _file_product_version(compilers[0])},
        "link": {"path": str(linkers[0]), "sha256": sha256_file(linkers[0]), "fileVersion": _file_product_version(linkers[0])},
        "lib": {"path": str(librarians[0]), "sha256": sha256_file(librarians[0]), "fileVersion": _file_product_version(librarians[0])},
        "windowsSdk": sdks[0].name,
        "nasm": {"path": str(nasm), "sha256": sha256_file(nasm), "version": _version(nasm, ["-v"])},
        "dotnet": {"path": str(Path(dotnet).resolve()), "version": _version(Path(dotnet), ["--version"], cwd=root), "sdks": sdk_versions},
        "runtimeDotnet": {"path": str(runtime_dotnet), "version": _version(runtime_dotnet, ["--version"], cwd=root / "out/rt")},
        "python": {"path": str(Path(sys.executable).resolve()), "version": sys.version.split()[0]},
        "git": git_text(root, "--version"),
    }


def _file_product_version(path: Path) -> str:
    system_root = Path(os.environ.get("SystemRoot", r"C:\Windows"))
    powershell = system_root / "System32/WindowsPowerShell/v1.0/powershell.exe"
    escaped = str(path).replace("'", "''")
    expression = f"(Get-Item -LiteralPath '{escaped}').VersionInfo.ProductVersion"
    output = run_checked([str(powershell), "-NoProfile", "-Command", expression]).stdout.strip()
    if not output:
        raise Build1CError(f"could not read tool version: {path}")
    return output


def validate_toolchain(toolchain: dict[str, Any]) -> None:
    for key in ("powershell", "vcvars64", "cl", "link", "lib", "windowsSdk", "nasm", "dotnet", "runtimeDotnet", "python", "git"):
        if not toolchain.get(key):
            raise Build1CError(f"required build toolchain prerequisite is missing: {key}")
    for key in ("cl", "link", "lib", "nasm", "dotnet", "runtimeDotnet"):
        value = toolchain[key]
        path = Path(value["path"] if isinstance(value, dict) else str(value))
        if not path.is_file():
            raise Build1CError(f"required toolchain executable is missing: {key} ({path})")
    if not str(toolchain["powershell"].get("version", "")).startswith("5.1"):
        raise Build1CError("supported Windows PowerShell version 5.1 is required")


def ensure_fresh_workspace(workspace: Path) -> dict[str, Any]:
    if workspace.exists():
        if workspace.is_symlink():
            raise Build1CError(f"BUILD1C workspace may not be a symlink or junction: {workspace}")
        try:
            next(workspace.iterdir())
        except StopIteration:
            pass
        else:
            raise Build1CError(f"stale or partial BUILD1C outputs already occupy the workspace: {workspace}")
    probe = workspace if workspace.exists() else workspace.parent
    while not probe.exists() and probe != probe.parent:
        probe = probe.parent
    free = shutil.disk_usage(probe).free
    if free < REQUIRED_FREE_BYTES:
        raise Build1CError(f"insufficient free space: need {REQUIRED_FREE_BYTES} bytes, found {free}")
    return {"path": str(workspace), "exists": workspace.exists(), "freeBytes": free, "estimatedBuildBytes": REQUIRED_FREE_BYTES}


def discover_repo_state(root: Path) -> dict[str, Any]:
    branch = git_text(root, "branch", "--show-current")
    head = git_text(root, "rev-parse", "HEAD")
    status = run_checked(["git", "-C", str(root), "status", "--porcelain=v1", "--untracked-files=all"]).stdout.rstrip("\r\n")
    if branch != "BUILD_INFRA":
        raise Build1CError(f"required branch is BUILD_INFRA; found {branch}")
    return {"root": str(root), "branch": branch, "head": head, "dirtyState": status.splitlines() if status else []}


def input_fingerprint(root: Path) -> dict[str, Any]:
    files: dict[str, Path] = {}
    for relative in INPUT_FILES:
        path = root / relative
        if path.is_file():
            files[relative] = path
    for relative in INPUT_DIRS:
        directory = root / relative
        if not directory.is_dir():
            continue
        for path in directory.rglob("*"):
            if not path.is_file() or any(part in {"__pycache__", "bin", "obj"} for part in path.parts):
                continue
            rel = path.relative_to(root).as_posix()
            files[rel] = path
    items = [
        {"path": relative, "length": path.stat().st_size, "sha256": sha256_file(path)}
        for relative, path in sorted(files.items())
    ]
    return {"fileCount": len(items), "sha256": canonical_json_sha256(items), "files": items}


def preflight(root: Path, workspace: Path, pack_root: Path, package_evidence: Path, variant: str) -> dict[str, Any]:
    if variant not in VARIANTS:
        raise Build1CError(f"unsupported Phase 29 variant: {variant}")
    repo = discover_repo_state(root)
    runtime = validate_runtime_tree(root)
    package = validate_package_bundle(pack_root, package_evidence, runtime["commit"])
    toolchain = discover_toolchain(root)
    validate_toolchain(toolchain)
    workspace_evidence = ensure_fresh_workspace(workspace)
    protected = [Path(r"D:\dev\guideXOSUEFI"), Path(r"D:\dev\guideXOSUEFI_APP_MODEL"), Path(r"D:\dev\guideXOSUEFI_MODERN_INPUT")]
    full_workspace = workspace.resolve()
    for candidate in protected:
        try:
            full_workspace.relative_to(candidate.resolve())
        except ValueError:
            continue
        raise Build1CError(f"BUILD1C workspace must not be inside a protected checkout: {full_workspace}")
    return {
        "schema": 1,
        "ready": True,
        "mode": "preflight",
        "variant": variant,
        "repository": repo,
        "runtime": runtime,
        "runtimePack": package,
        "toolchain": toolchain,
        "workspace": workspace_evidence,
        "sourceInputs": input_fingerprint(root),
    }


def _phase29_record_path(workspace: Path) -> Path:
    return workspace / "build/phase29/phase29-managed-notification-build.json"


def _stage_basename(variant: str) -> str:
    payload = next((item for item in identities.PAYLOADS if item.phase == "Phase29" and item.variant == variant), None)
    if payload is None:
        raise Build1CError(f"native identity contract has no Phase 29 variant {variant}")
    return payload.basename


def verify_native_row(root: Path, workspace: Path, variant: str) -> dict[str, Any]:
    record_path = _phase29_record_path(workspace)
    if not record_path.is_file():
        raise Build1CError(f"complete Phase 29 build record is missing: {record_path}")
    record = load_json(record_path)
    if record.get("generatedBy") != "Tools/Phase29/build_phase29_managed_notification.ps1":
        raise Build1CError("Phase 29 record was not produced by the supported builder")
    entries = record.get("payloads")
    if not isinstance(entries, list) or len(entries) != 1 or entries[0].get("name") != variant:
        raise Build1CError(f"Phase 29 record is partial, stale, or selects a different variant: {record_path}")
    audit_root = workspace / "evidence/native-audit-root"
    copied_record = audit_root / "out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json"
    copied_record.parent.mkdir(parents=True, exist_ok=True)
    record_bytes = record_path.read_bytes()
    if copied_record.exists() and copied_record.read_bytes() != record_bytes:
        raise Build1CError("native audit record copy differs from the supported build record")
    if not copied_record.exists():
        copied_record.write_bytes(record_bytes)

    old_payloads = identities.PAYLOADS
    payload = next(item for item in old_payloads if item.phase == "Phase29" and item.variant == variant)
    try:
        identities.PAYLOADS = [payload]
        rows = identities.collect_rows(audit_root, workspace / "stage")
    except (OSError, ValueError, KeyError, struct.error) as exc:  # type: ignore[name-defined]
        raise Build1CError(f"native per-variant identity verification failed: {exc}") from exc
    finally:
        identities.PAYLOADS = old_payloads
    if len(rows) != 1:
        raise Build1CError(f"native per-variant verification returned {len(rows)} rows; expected one")
    row = rows[0]
    return {key: value for key, value in row.items()}


def _locate_link_response(build_root: Path) -> Path:
    sys.path.insert(0, str(ROOT / "Tools" / "Phase23"))
    import verify_guidexos_artifact as phase23_verifier  # noqa: E402

    candidates = sorted(build_root.rglob("*.rsp"))
    for path in candidates:
        try:
            phase23_verifier.validate_link(path.read_text(encoding="utf-8", errors="replace"))
        except Exception:
            continue
        return path
    raise Build1CError(f"no supported GUIDEXOS link response file was found under {build_root}")


def run_phase23_verifier(root: Path, workspace: Path, variant: str) -> dict[str, Any]:
    output_root = workspace / "build/phase29" / variant
    executable = Path(str(load_json(_phase29_record_path(workspace))["payloads"][0]["path"]))
    map_path = output_root / "guidexos.map"
    pack_manifest = workspace / "build/phase29" / f"{variant}-runtime-pack/guidexos-runtime-pack.manifest.json"
    if not map_path.is_file() or not pack_manifest.is_file():
        raise Build1CError("Phase 23 verification inputs are incomplete (map or Phase 26 runtime manifest missing)")
    link_rsp = _locate_link_response(workspace / "build/phase29")
    evidence_root = workspace / "evidence"
    evidence_root.mkdir(parents=True, exist_ok=True)
    inspection = evidence_root / f"{variant}-inspection.json"
    result_path = evidence_root / f"{variant}-phase23-verification.json"
    phase29_record = load_json(_phase29_record_path(workspace))
    phase26_record_path = output_root / "phase26-managed-proof-build.json"
    if not phase26_record_path.is_file():
        raise Build1CError(f"Phase 26 build record is missing: {phase26_record_path}")
    phase26_record = load_json(phase26_record_path)
    syscall_object = phase26_record.get("syscallObject", {})
    syscall_object_path = Path(str(syscall_object.get("path", "")))
    if not syscall_object_path.is_file():
        raise Build1CError("Phase 26 syscall object is missing; cannot validate phase-owned PAL helper")
    require_hash(sha256_file(syscall_object_path), str(syscall_object.get("sha256", "")), "Phase 26 syscall object")
    phase_helpers = sorted(set(str(item) for item in phase29_record.get("palHelper", [])) | {"guidexos_pal_syscall5"})
    inspect_script = root / "Tools/inspect_managed_image.py"
    verifier = root / "Tools/Phase23/verify_guidexos_artifact.py"
    inspect = run_checked([
        sys.executable, "-B", str(inspect_script), str(executable), "--map", str(map_path), "--output", str(inspection)
    ])
    verify_args = [
        sys.executable, "-B", str(verifier), str(executable), str(pack_manifest), str(inspection), str(map_path), str(link_rsp)
    ]
    for helper in phase_helpers:
        verify_args.extend(["--allowed-phase-helper", helper])
    verify_args.extend(["--allow-pal-subset", "--output", str(result_path)])
    verify = run_checked(verify_args)
    result = load_json(result_path)
    if result.get("result") != "PASS":
        raise Build1CError("Phase 23 artifact verifier did not return PASS")
    return {
        "result": result,
        "inspectionPath": str(inspection),
        "inspectionSha256": sha256_file(inspection),
        "linkResponsePath": str(link_rsp),
        "linkResponseSha256": sha256_file(link_rsp),
        "allowedPhaseHelpers": phase_helpers,
        "phase26SyscallObjectSha256": sha256_file(syscall_object_path),
        "verifierPath": str(verifier.relative_to(root).as_posix()),
        "verifierOutputSha256": sha256_file(result_path),
        "inspectStdout": inspect.stdout.strip(),
        "verifyStdout": verify.stdout.strip(),
    }


def run_full_native_audit(root: Path, workspace: Path) -> dict[str, Any]:
    audit_root = workspace / "evidence/native-audit-root"
    command = [
        sys.executable,
        "-B",
        str(root / "Tools/ManagedArtifacts/managed_artifacts.py"),
        "--root", str(audit_root),
        "--ramdisk-source", str(workspace / "stage"),
        "--stage", "before",
        "--audit-only",
    ]
    result = subprocess.run(command, text=True, capture_output=True)
    combined = (result.stdout + result.stderr).strip()
    log_path = workspace / "evidence/partial-full-set-native-audit.txt"
    log_path.write_text(combined + "\n", encoding="utf-8", newline="\n")
    if result.returncode == 0:
        return {"status": "PASS", "exitCode": 0, "logPath": str(log_path)}
    return {
        "status": "REJECTED_PARTIAL_SET",
        "exitCode": result.returncode,
        "detail": combined.splitlines()[-1] if combined else "native generator rejected the partial set",
        "logPath": str(log_path),
    }


def _source_revision(root: Path) -> dict[str, Any]:
    state = discover_repo_state(root)
    return {"revision": state["head"], "branch": state["branch"], "dirtyState": state["dirtyState"]}


def verify_and_record(root: Path, workspace: Path, pack_root: Path, package_evidence: Path, variant: str) -> dict[str, Any]:
    runtime = validate_runtime_tree(root)
    package = validate_package_bundle(pack_root, package_evidence, runtime["commit"])
    toolchain = discover_toolchain(root)
    validate_toolchain(toolchain)
    native_row = verify_native_row(root, workspace, variant)
    phase23 = run_phase23_verifier(root, workspace, variant)
    partial_native = run_full_native_audit(root, workspace)
    inputs = input_fingerprint(root)
    revision = _source_revision(root)
    record_path = _phase29_record_path(workspace)
    record_sha = sha256_file(record_path)
    basename = _stage_basename(variant)
    staged_exe = workspace / "stage" / f"{basename}.exe"
    descriptor = workspace / "stage" / f"{basename}.gxmi"
    build_exe = Path(native_row["build_output_path"])
    output = {
        "schema": 1,
        "recordType": "supplementary-build-provenance",
        "identityAuthority": "Tools/ManagedArtifacts/managed_artifacts.py",
        "variant": variant,
        "source": revision,
        "sourceInputFingerprint": inputs,
        "runtime": runtime,
        "runtimePackageEvidence": {
            "path": str(package_evidence),
            "sha256": sha256_file(package_evidence),
            "packageArchiveHashesAreSupplementaryMeasurements": True,
            "verifiedPack": package,
        },
        "toolchain": toolchain,
        "build": {
            "script": "Tools/Phase29/build_phase29_managed_notification.ps1",
            "scriptSha256": sha256_file(root / "Tools/Phase29/build_phase29_managed_notification.ps1"),
            "phase26ScriptSha256": sha256_file(root / "Tools/Phase26/build_phase26_managed_proof.ps1"),
            "stageScriptSha256": sha256_file(root / "Tools/Phase29/stage_phase29_image.ps1"),
            "variant": variant,
            "projectProperty": "Phase29Mode=" + str(load_json(record_path)["payloads"][0]["mode"]),
            "buildRecord": {"path": str(record_path.relative_to(workspace).as_posix()), "length": record_path.stat().st_size, "sha256": record_sha},
        },
        "artifact": {
            "buildOutput": {"path": str(build_exe.relative_to(workspace)), "length": build_exe.stat().st_size, "sha256": sha256_file(build_exe)},
            "stagedExecutable": {"path": str(staged_exe.relative_to(workspace)), "length": staged_exe.stat().st_size, "sha256": sha256_file(staged_exe)},
            "descriptor": {"path": str(descriptor.relative_to(workspace)), "length": descriptor.stat().st_size, "sha256": sha256_file(descriptor)},
            "map": {"path": str((workspace / "build/phase29" / variant / "guidexos.map").relative_to(workspace)), "sha256": sha256_file(workspace / "build/phase29" / variant / "guidexos.map")},
            "buildStageByteAgreement": build_exe.read_bytes() == staged_exe.read_bytes(),
            "nativeIdentityRow": native_row,
            "phase23StaticVerification": phase23,
            "fullSetNativeAudit": partial_native,
        },
        "limitations": [
            "This record supplements, and does not replace, the native managed-artifact identity generator.",
            "The Phase 23 production manifest has no package archive SHA-256 fields; archive hashes here are measured content identities, not independent authenticity claims.",
            "A one-variant build cannot satisfy the whole 41-variant identity set.",
        ],
    }
    if not output["artifact"]["buildStageByteAgreement"]:
        raise Build1CError("build and staged executable bytes do not agree")
    encoded = stable_json(output)
    evidence_path = workspace / "evidence" / f"phase29-{variant}-provenance.json"
    evidence_path.parent.mkdir(parents=True, exist_ok=True)
    if evidence_path.exists() and evidence_path.read_text(encoding="utf-8") != encoded:
        raise Build1CError(f"existing provenance record differs; refusing overwrite: {evidence_path}")
    if not evidence_path.exists():
        evidence_path.write_text(encoded, encoding="utf-8", newline="\n")
    return output


def _protected_workspace_check(workspace: Path) -> None:
    # Explicitly named read-only references from the BUILD1C task.
    protected = (
        Path(r"D:\dev\guideXOSUEFI"),
        Path(r"D:\dev\guideXOSUEFI_APP_MODEL"),
        Path(r"D:\dev\guideXOSUEFI_MODERN_INPUT"),
    )
    target = workspace.resolve()
    for path in protected:
        try:
            target.relative_to(path.resolve())
        except ValueError:
            continue
        raise Build1CError(f"BUILD1C workspace must not be inside protected checkout {path}")


def main() -> int:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="mode", required=True)
    for mode in ("preflight", "verify"):
        command = subparsers.add_parser(mode)
        command.add_argument("--root", type=Path, default=ROOT)
        command.add_argument("--workspace", type=Path, required=True)
        command.add_argument("--phase23-pack", type=Path, required=True)
        command.add_argument("--package-evidence", type=Path, required=True)
        command.add_argument("--variant", choices=VARIANTS, default="success")
    args = parser.parse_args()
    try:
        root = args.root.resolve()
        workspace = args.workspace.resolve()
        _protected_workspace_check(workspace)
        if args.mode == "preflight":
            report = preflight(root, workspace, args.phase23_pack.resolve(), args.package_evidence.resolve(), args.variant)
        else:
            report = verify_and_record(root, workspace, args.phase23_pack.resolve(), args.package_evidence.resolve(), args.variant)
        print(stable_json(report), end="")
        return 0
    except Build1CError as exc:
        print(stable_json({"schema": 1, "ready": False, "mode": args.mode, "error": str(exc)}), end="", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
