#!/usr/bin/env python3
"""Read-only audit of reusable guideXOS managed proof artifacts.

This prototype deliberately has no materialization or cache-write operation.
The existing managed_artifacts.py generator remains the final authority for
the Phase 29-35 build/stage/descriptor/identity chain.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import struct
import subprocess
import sys
from functools import lru_cache
from pathlib import Path
from typing import Any

sys.dont_write_bytecode = True

from managed_artifacts import (  # noqa: E402
    DESCRIPTOR_DIGEST_OFFSET,
    DESCRIPTOR_HEADER_SIZE,
    PAYLOADS,
    VARIANT_MASK,
    pe_timestamp_offsets,
    read_generated_kernel_hashes,
)


PHASE_DIRS = {
    "Phase29": "phase29-managed-notification",
    "Phase30": "phase30-managed-clipboard",
    "Phase31": "phase31-managed-shell",
    "Phase32": "phase32-managed-open-document",
    "Phase33": "phase33-managed-shell-action",
    "Phase34": "phase34-managed-resource-read",
    "Phase35": "phase35-managed-persistent-read",
}

PHASE_BUILDERS = {
    "Phase29": "build_phase29_managed_notification.ps1",
    "Phase30": "build_phase30_managed_clipboard.ps1",
    "Phase31": "build_phase31_managed_shell.ps1",
    "Phase32": "build_phase32_managed_open_document.ps1",
    "Phase33": "build_phase33_managed_shell_action.ps1",
    "Phase34": "build_phase34_managed_resource_read.ps1",
    "Phase35": "build_phase35_managed_persistent_read.ps1",
}

PHASE21_RECORD = "out/dotnet/phase21-source-patch/phase21-source-patch-record.json"
PHASE22_RECORD = "out/dotnet/phase22-source-patch/phase22-source-patch-record.json"
PHASE23_RECORD = "out/dotnet/phase23-source-patch/phase23-ilc-source-patch-record.json"
PRIVATE_ILC_EXE = "out/dotnet/phase23-private-ilc/ilc-published/ilc.exe"
PRIVATE_ILC_RECORD = "out/dotnet/phase23-private-ilc/private-ilc-build.json"
RUNTIME_PACK_MANIFEST = "out/dotnet/phase23-runtime-pack/guidexos-runtime-pack.manifest.json"
RUNTIME_PACK_FILES = (
    "out/dotnet/phase23-runtime-pack/guidexos-runtime-pack.manifest.json",
    "out/dotnet/phase23-runtime-pack/guidexos_link_shim.obj",
    "out/dotnet/phase23-runtime-pack/packages/microsoft.dotnet.ilcompiler.9.0.0.nupkg",
    "out/dotnet/phase23-runtime-pack/packages/runtime.guidexos-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg",
    "out/dotnet/phase23-runtime-pack/packages/runtime.win-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg",
)

CONTRACT_SUFFIXES = {".asm", ".config", ".cpp", ".cs", ".csproj", ".h", ".json", ".py", ".ps1", ".targets", ".props"}
CONTRACT_ROOT_FILES = (
    "build.ps1",
    "Directory.Build.props",
    "global.json",
    "NuGet.config",
    "Tools/ManagedArtifacts/managed_artifacts.py",
    "Tools/Phase34/phase34_flags.py",
    "Tools/Phase35/phase35_flags.py",
    "Kernel/Misc/ManagedImage.cs",
    "Kernel/Misc/Ring3Abi.cs",
)
CONTRACT_TOOL_DIRS = (
    "Tools/Phase20",
    "Tools/Phase21",
    "Tools/Phase22",
    "Tools/Phase23",
    "Tools/Phase29",
    "Tools/Phase30",
    "Tools/Phase31",
    "Tools/Phase32",
    "Tools/Phase33",
    "Tools/Phase34",
    "Tools/Phase35",
)
CONTRACT_SOURCE_DIRS = (
    "GuideXos.User",
    "UserManagedNotificationProof",
    "UserManagedClipboardProof",
    "UserManagedShellProof",
    "UserManagedOpenDocumentProof",
    "UserManagedShellActionProof",
    "UserManagedResourceReadProof",
    "UserManagedPersistentStorageProof",
)


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def read_json(path: Path) -> dict[str, Any]:
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        raise ValueError(f"JSON record is not an object: {path}")
    return data


def git_metadata(root: Path) -> dict[str, str | None]:
    result: dict[str, str | None] = {"branch": None, "head": None}
    for key, args in (("branch", ["branch", "--show-current"]), ("head", ["rev-parse", "HEAD"])):
        try:
            proc = subprocess.run(
                ["git", "-C", str(root), *args],
                check=True,
                capture_output=True,
                text=True,
                encoding="utf-8",
            )
            value = proc.stdout.strip()
            result[key] = value or None
        except (OSError, subprocess.CalledProcessError):
            pass
    return result


def is_within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except (OSError, ValueError):
        return False


@lru_cache(maxsize=32)
def contract_files(root: Path) -> dict[str, str]:
    """Hash relevant, present build inputs without including generated outputs."""
    found: dict[str, str] = {}
    for relative in CONTRACT_ROOT_FILES:
        path = root / Path(relative)
        if path.is_file():
            found[relative.replace("\\", "/")] = sha256_file(path)
    for relative_dir in CONTRACT_TOOL_DIRS + CONTRACT_SOURCE_DIRS:
        directory = root / Path(relative_dir)
        if not directory.is_dir():
            continue
        for current, directories, filenames in os.walk(directory):
            directories[:] = [
                name for name in directories
                if name.lower() not in {"obj", "bin", "__pycache__", ".git"}
            ]
            current_path = Path(current)
            for filename in filenames:
                path = current_path / filename
                if path.suffix.lower() not in CONTRACT_SUFFIXES:
                    continue
                relative = path.relative_to(root).as_posix()
                found[relative] = sha256_file(path)
    return dict(sorted(found.items()))


def compare_contracts(source: Path, receiving: Path) -> dict[str, Any]:
    source_files = contract_files(source)
    receiving_files = contract_files(receiving)
    names = sorted(set(source_files) | set(receiving_files))
    differences = [
        {
            "path": name,
            "sourceSha256": source_files.get(name),
            "receivingSha256": receiving_files.get(name),
        }
        for name in names
        if source_files.get(name) != receiving_files.get(name)
    ]
    return {
        "compatible": bool(source_files) and not differences,
        "sourceFileCount": len(source_files),
        "receivingFileCount": len(receiving_files),
        "differentFiles": differences,
    }


def expected_record_path(root: Path, phase: str, record_name: str) -> Path:
    return root / "out" / "dotnet" / PHASE_DIRS[phase] / record_name


def validate_descriptor_and_image(
    payload: Any, exe_path: Path, descriptor_path: Path
) -> tuple[dict[str, Any], list[str]]:
    issues: list[str] = []
    image = exe_path.read_bytes()
    descriptor = descriptor_path.read_bytes()
    image_hash = sha256_bytes(image)
    descriptor_hash = sha256_bytes(descriptor)

    if len(descriptor) < DESCRIPTOR_HEADER_SIZE:
        issues.append("DESCRIPTOR_TRUNCATED")
        return {"executableSha256": image_hash, "descriptorSha256": descriptor_hash}, issues

    magic, version, target, machine, flags, file_size = struct.unpack_from("<IIIIII", descriptor, 0)
    section_count = struct.unpack_from("<I", descriptor, 48)[0]
    expected_length = DESCRIPTOR_HEADER_SIZE + section_count * 24
    if magic != 0x494D5847 or version != 1 or target != 0x47554944 or machine != 0x8664:
        issues.append("DESCRIPTOR_VERSION_OR_TARGET_UNSUPPORTED")
    if len(descriptor) != expected_length:
        issues.append("DESCRIPTOR_SECTION_TABLE_LENGTH_MISMATCH")
    if file_size != len(image):
        issues.append("DESCRIPTOR_EXECUTABLE_LENGTH_MISMATCH")
    if (flags & 0x1F) != 0x1F or (flags & VARIANT_MASK) != payload.proof_flags:
        issues.append("DESCRIPTOR_FLAGS_MISMATCH")
    descriptor_digest = descriptor[DESCRIPTOR_DIGEST_OFFSET : DESCRIPTOR_DIGEST_OFFSET + 32]
    if len(descriptor_digest) != 32 or descriptor_digest.hex().upper() != image_hash:
        issues.append("DESCRIPTOR_EXECUTABLE_HASH_MISMATCH")

    try:
        timestamp_offset, debug_timestamp_offsets = pe_timestamp_offsets(image, exe_path)
        timestamps = [struct.unpack_from("<I", image, timestamp_offset)[0]]
        timestamps.extend(struct.unpack_from("<I", image, offset)[0] for offset in debug_timestamp_offsets)
        normalized = bytearray(image)
        normalized[timestamp_offset : timestamp_offset + 4] = b"\0\0\0\0"
        for offset in debug_timestamp_offsets:
            normalized[offset : offset + 4] = b"\0\0\0\0"
        if any(timestamps) or sha256_bytes(normalized) != image_hash:
            issues.append("EXECUTABLE_TIMESTAMP_NOT_NORMALIZED")
    except (OSError, ValueError, struct.error):
        issues.append("EXECUTABLE_PE_IDENTITY_INVALID")

    return {
        "executableSha256": image_hash,
        "executableBytes": len(image),
        "descriptorSha256": descriptor_hash,
        "descriptorBytes": len(descriptor),
        "descriptorFlags": f"0x{flags:08X}",
    }, issues


def validate_artifacts(root: Path) -> dict[str, Any]:
    root = root.resolve()
    stage_root = root / "ramdisk_src" / "Native"
    identity_source = root / "guideXOS" / "obj" / "ManagedArtifactIdentities.g.cs"
    issues: list[str] = []
    rows: list[dict[str, Any]] = []
    records: dict[tuple[str, str], dict[str, Any]] = {}
    record_data: dict[str, dict[str, Any]] = {}

    expected_by_phase: dict[str, set[str]] = {}
    for payload in PAYLOADS:
        expected_by_phase.setdefault(payload.phase, set()).add(payload.build_mode)

    for payload in PAYLOADS:
        row_issues: list[str] = []
        record_path = expected_record_path(root, payload.phase, payload.record_name)
        if payload.phase not in record_data:
            try:
                data = read_json(record_path)
                expected_builder = f"Tools/{payload.phase}/{PHASE_BUILDERS[payload.phase]}"
                if data.get("schema") != 1:
                    issues.append(f"{payload.phase}:UNSUPPORTED_BUILD_RECORD_SCHEMA")
                if data.get("generatedBy") != expected_builder:
                    issues.append(f"{payload.phase}:BUILD_RECORD_PRODUCER_MISMATCH")
                if data.get("target") != "guidexos-x64" or data.get("targetOs") != "guidexos":
                    issues.append(f"{payload.phase}:BUILD_RECORD_TARGET_MISMATCH")
                entries = data.get("payloads")
                if not isinstance(entries, list):
                    raise ValueError("payloads is not an array")
                entry_map: dict[str, dict[str, Any]] = {}
                duplicate_names: set[str] = set()
                for entry in entries:
                    if isinstance(entry, dict) and isinstance(entry.get("name"), str):
                        if entry["name"] in entry_map:
                            duplicate_names.add(entry["name"])
                        entry_map[entry["name"]] = entry
                if duplicate_names:
                    issues.append(f"{payload.phase}:DUPLICATE_BUILD_RECORD_VARIANT")
                expected_names = expected_by_phase[payload.phase]
                if set(entry_map) != expected_names:
                    issues.append(f"{payload.phase}:BUILD_RECORD_VARIANT_SET_MISMATCH")
                record_data[payload.phase] = {
                    "record": data,
                    "entries": entry_map,
                    "path": record_path,
                }
            except FileNotFoundError:
                record_data[payload.phase] = {"error": "BUILD_RECORD_MISSING", "path": record_path}
            except (OSError, ValueError, json.JSONDecodeError) as error:
                record_data[payload.phase] = {"error": "BUILD_RECORD_INVALID", "detail": str(error), "path": record_path}

        phase_record = record_data[payload.phase]
        item = phase_record.get("entries", {}).get(payload.build_mode)
        if not item:
            row_issues.append(phase_record.get("error", "BUILD_RECORD_VARIANT_MISSING"))

        exe_path = stage_root / f"{payload.basename}.exe"
        descriptor_path = stage_root / f"{payload.basename}.gxmi"
        build_path: Path | None = None
        metadata: dict[str, Any] = {
            "phase": payload.phase,
            "variant": payload.variant,
            "executable": exe_path.name,
            "descriptor": descriptor_path.name,
            "record": phase_record["path"].relative_to(root).as_posix(),
            "recordSha256": sha256_file(phase_record["path"]) if phase_record["path"].is_file() else None,
        }

        if item:
            raw_build_path = item.get("path")
            if not isinstance(raw_build_path, str) or not raw_build_path:
                row_issues.append("BUILD_OUTPUT_PATH_MISSING")
            else:
                build_path = Path(raw_build_path)
                if not build_path.is_absolute():
                    build_path = root / build_path
                build_path = build_path.resolve()
                metadata["buildOutputPath"] = str(build_path)
                metadata["buildOutputPathExternal"] = not is_within(build_path, root)
                if not build_path.is_file():
                    row_issues.append("BUILD_OUTPUT_MISSING")
                else:
                    build_hash = sha256_file(build_path)
                    build_size = build_path.stat().st_size
                    try:
                        metadata["buildOutput"] = build_path.relative_to(root).as_posix()
                    except ValueError:
                        metadata["buildOutput"] = str(build_path)
                    metadata["buildOutputSha256"] = build_hash
                    metadata["buildOutputBytes"] = build_size
                    if not re.fullmatch(r"[0-9A-Fa-f]{64}", str(item.get("sha256", ""))):
                        row_issues.append("BUILD_RECORD_HASH_INVALID")
                    elif str(item.get("sha256", "")).upper() != build_hash:
                        row_issues.append("BUILD_RECORD_HASH_MISMATCH")
                    if item.get("length") != build_size:
                        row_issues.append("BUILD_RECORD_LENGTH_MISMATCH")

        if not exe_path.is_file():
            row_issues.append("STAGED_EXECUTABLE_MISSING")
        if not descriptor_path.is_file():
            row_issues.append("STAGED_DESCRIPTOR_MISSING")
        if exe_path.is_file() and descriptor_path.is_file():
            descriptor_metadata, descriptor_issues = validate_descriptor_and_image(
                payload, exe_path, descriptor_path
            )
            metadata.update(descriptor_metadata)
            row_issues.extend(descriptor_issues)
        if build_path and build_path.is_file() and exe_path.is_file():
            if sha256_file(build_path) != sha256_file(exe_path) or build_path.stat().st_size != exe_path.stat().st_size:
                row_issues.append("BUILD_STAGE_BYTE_MISMATCH")

        rows.append(
            {
                **metadata,
                "valid": not row_issues,
                "issues": sorted(set(row_issues)),
            }
        )

    identity_hashes: dict[int, str] = {}
    identity_issues: list[str] = []
    try:
        identity_hashes = read_generated_kernel_hashes(identity_source)
        for payload, row in zip(PAYLOADS, rows):
            if identity_hashes.get(payload.proof_flags) != row.get("executableSha256"):
                identity_issues.append(f"{payload.phase}/{payload.variant}:GENERATED_IDENTITY_MISMATCH")
    except (OSError, ValueError) as error:
        identity_issues.append(f"GENERATED_IDENTITY_INVALID:{error}")

    all_rows_valid = len(rows) == len(PAYLOADS) and all(row["valid"] for row in rows)
    native = {"status": "NOT_RUN", "detail": "preflight did not establish a complete artifact set"}
    if all_rows_valid and not identity_issues:
        tool = Path(__file__).with_name("managed_artifacts.py").resolve()
        command = [
            sys.executable,
            "-B",
            str(tool),
            "--root",
            str(root),
            "--stage",
            "generated",
            "--ramdisk-source",
            str(stage_root),
            "--identity-source",
            str(identity_source),
            "--kernel-built",
            "--audit-only",
        ]
        try:
            proc = subprocess.run(
                command,
                cwd=str(root),
                capture_output=True,
                text=True,
                encoding="utf-8",
                env={**os.environ, "PYTHONDONTWRITEBYTECODE": "1"},
            )
            native = {
                "status": "PASS" if proc.returncode == 0 else "FAIL",
                "exitCode": proc.returncode,
                "stdout": proc.stdout.strip(),
                "stderr": proc.stderr.strip(),
            }
        except OSError as error:
            native = {"status": "ERROR", "detail": str(error)}

    all_issues = sorted(set(issues + identity_issues))
    if native.get("status") not in ("PASS", "NOT_RUN"):
        all_issues.append("NATIVE_IDENTITY_GENERATOR_REJECTED")
    return {
        "root": str(root),
        "sourceIdentity": git_metadata(root),
        "requiredArtifactCount": len(PAYLOADS),
        "preflightValidCount": sum(1 for row in rows if row["valid"]),
        "identityValid": not identity_issues,
        "nativeGenerator": native,
        "verified": all_rows_valid and not issues and not identity_issues and native.get("status") == "PASS",
        "issues": all_issues,
        "artifacts": rows,
    }


def _git_status_sets(runtime_root: Path) -> tuple[set[str], set[str], list[str]]:
    proc = subprocess.run(
        ["git", "-C", str(runtime_root), "status", "--porcelain=v1", "--untracked-files=all", "-z"],
        check=True,
        capture_output=True,
    )
    tracked: set[str] = set()
    untracked: set[str] = set()
    unsupported: list[str] = []
    for raw in proc.stdout.split(b"\0"):
        if not raw:
            continue
        status = raw[:2].decode("ascii", errors="replace")
        relative = raw[3:].decode("utf-8", errors="replace").replace("\\", "/")
        if status == " M":
            tracked.add(relative)
        elif status == "??":
            untracked.add(relative)
        else:
            unsupported.append(f"{status} {relative}")
    return tracked, untracked, unsupported


def audit_runtime(root: Path, cache_root: Path | None = None) -> dict[str, Any]:
    root = root.resolve()
    cache_root = (cache_root or root).resolve()
    runtime_root = cache_root / "out" / "rt"
    lock_path = root / "Tools" / "Phase23" / "runtime-source.lock.json"
    recovery_path = root / "Tools" / "Phase23" / "runtime-source-cache-recovery.lock.json"
    result: dict[str, Any] = {
        "root": str(root),
        "cacheRoot": str(cache_root),
        "targetLocksCompatible": False,
        "runtimeSource": {"path": str(runtime_root), "verified": False},
        "patchEvidence": {"verified": False},
        "privateIlc": {"verified": False},
        "runtimePack": {"locallyPresent": False, "packageHashesBound": False},
        "localBuildPrerequisitesPresent": False,
        "shareEligible": False,
        "issues": [],
    }
    issues: list[str] = result["issues"]
    try:
        lock = read_json(lock_path)
        recovery = read_json(recovery_path)
        phase20 = read_json(root / "Tools" / "Phase20" / "runtime-source.lock.json")
        phase21 = read_json(root / "Tools" / "Phase21" / "runtime-source.lock.json")
        phase22 = read_json(root / "Tools" / "Phase22" / "runtime-source.lock.json")
    except (OSError, ValueError, json.JSONDecodeError) as error:
        issues.append(f"RUNTIME_LOCK_MISSING_OR_INVALID:{error}")
        result["status"] = "MISSING_OR_INVALID_LOCK"
        return result

    try:
        cache_locks = [
            read_json(cache_root / relative)
            for relative in (
                "Tools/Phase20/runtime-source.lock.json",
                "Tools/Phase21/runtime-source.lock.json",
                "Tools/Phase22/runtime-source.lock.json",
                "Tools/Phase23/runtime-source.lock.json",
                "Tools/Phase23/runtime-source-cache-recovery.lock.json",
            )
        ]
        target_locks = [
            read_json(root / relative)
            for relative in (
                "Tools/Phase20/runtime-source.lock.json",
                "Tools/Phase21/runtime-source.lock.json",
                "Tools/Phase22/runtime-source.lock.json",
                "Tools/Phase23/runtime-source.lock.json",
                "Tools/Phase23/runtime-source-cache-recovery.lock.json",
            )
        ]
        result["targetLocksCompatible"] = cache_locks == target_locks
        if not result["targetLocksCompatible"]:
            issues.append("RUNTIME_CACHE_LOCKS_DIFFER_FROM_RECEIVER")
    except (OSError, ValueError, json.JSONDecodeError) as error:
        issues.append(f"RUNTIME_CACHE_LOCKS_MISSING_OR_INVALID:{error}")

    expected_commit = str(recovery.get("runtimeCommit", ""))
    locks_match = (
        bool(re.fullmatch(r"[0-9a-fA-F]{40}", expected_commit))
        and lock.get("runtimeCommit") == expected_commit
        and phase20.get("source", {}).get("commit") == expected_commit
        and phase21.get("runtimeCommit") == expected_commit
        and phase22.get("runtimeCommit") == expected_commit
    )
    result["runtimeSource"]["expectedCommit"] = expected_commit or None
    if not locks_match:
        issues.append("RUNTIME_LOCK_COMMIT_MISMATCH")

    if not runtime_root.is_dir():
        issues.append("PINNED_RUNTIME_SOURCE_MISSING")
        result["status"] = "MISSING"
        return result

    try:
        actual_commit = subprocess.run(
            ["git", "-C", str(runtime_root), "rev-parse", "HEAD"],
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
        ).stdout.strip()
        tracked, untracked, unsupported = _git_status_sets(runtime_root)
    except (OSError, subprocess.CalledProcessError) as error:
        issues.append(f"PINNED_RUNTIME_GIT_INVALID:{error}")
        result["status"] = "INVALID"
        return result

    result["runtimeSource"]["actualCommit"] = actual_commit
    if actual_commit != expected_commit:
        issues.append("PINNED_RUNTIME_COMMIT_MISMATCH")
    expected_tracked = {str(entry["path"]).replace("\\", "/") for entry in recovery.get("trackedFiles", [])}
    expected_templates = {str(entry["path"]).replace("\\", "/") for entry in recovery.get("requiredTemplates", [])}
    expected_untracked = expected_templates | {str(path).replace("\\", "/") for path in recovery.get("allowedGeneratedArtifacts", [])}
    if tracked != expected_tracked or untracked != expected_untracked or unsupported:
        issues.append("PINNED_RUNTIME_STATUS_SET_MISMATCH")

    checked = 0
    for entry in recovery.get("trackedFiles", []) + recovery.get("requiredTemplates", []):
        relative = str(entry.get("path", "")).replace("/", os.sep)
        expected_hash = str(entry.get("sha256", "")).upper()
        path = runtime_root / relative
        if not path.is_file() or not re.fullmatch(r"[0-9A-F]{64}", expected_hash):
            issues.append(f"RUNTIME_LOCKED_FILE_MISSING_OR_INVALID:{entry.get('path', '')}")
            continue
        checked += 1
        if sha256_file(path) != expected_hash:
            issues.append(f"RUNTIME_LOCKED_FILE_HASH_MISMATCH:{entry.get('path', '')}")
    for relative in recovery.get("allowedGeneratedArtifacts", []):
        if not (runtime_root / str(relative).replace("/", os.sep)).is_file():
            issues.append(f"RUNTIME_GENERATED_ARTIFACT_MISSING:{relative}")
    result["runtimeSource"]["lockedFileCount"] = checked
    result["runtimeSource"]["verified"] = result["targetLocksCompatible"] and locks_match and actual_commit == expected_commit and tracked == expected_tracked and untracked == expected_untracked and not unsupported and not any(
        issue.startswith(("RUNTIME_LOCKED_FILE", "RUNTIME_GENERATED_ARTIFACT")) for issue in issues
    )

    patch_checks: list[bool] = []
    for relative, expected_phase in ((PHASE21_RECORD, 21), (PHASE22_RECORD, 22), (PHASE23_RECORD, 23)):
        path = cache_root / Path(relative)
        try:
            record = read_json(path)
            if expected_phase == 21:
                okay = record.get("schema") == 1 and record.get("phase") == 21 and record.get("target") == "guidexos-x64" and record.get("source", {}).get("commit") == expected_commit
            elif expected_phase == 22:
                okay = record.get("schema") == 1 and record.get("target") == "guidexos-x64" and record.get("targetOs") == "guidexos" and record.get("sourceCommit") == expected_commit
            else:
                expected_files = {str(path).replace("\\", "/") for path in lock.get("changedFiles", [])}
                found: dict[str, str] = {}
                for entry in record.get("changedFiles", []):
                    found[str(entry.get("path", "")).replace("\\", "/")] = str(entry.get("sha256", "")).upper()
                okay = (
                    record.get("schema") == 1
                    and record.get("target") == "guidexos-x64"
                    and record.get("targetOs") == "guidexos"
                    and record.get("sourceCommit") == expected_commit
                    and record.get("patchIdentity") == lock.get("ilcPatchIdentity")
                    and set(found) == expected_files
                )
                if okay:
                    for relative_path, expected_hash in found.items():
                        source_path = runtime_root / Path(relative_path)
                        if not source_path.is_file() or sha256_file(source_path) != expected_hash:
                            okay = False
                            break
            patch_checks.append(bool(okay))
            if not okay:
                issues.append(f"PHASE{expected_phase}_PATCH_RECORD_INVALID")
        except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError):
            patch_checks.append(False)
            issues.append(f"PHASE{expected_phase}_PATCH_RECORD_MISSING_OR_INVALID")
    result["patchEvidence"]["verified"] = len(patch_checks) == 3 and all(patch_checks) and result["runtimeSource"]["verified"]

    try:
        ilc_path = cache_root / Path(PRIVATE_ILC_EXE)
        ilc_record_path = cache_root / Path(PRIVATE_ILC_RECORD)
        ilc_record = read_json(ilc_record_path)
        ilc_hash = sha256_file(ilc_path)
        patch_record_hash = sha256_file(cache_root / Path(PHASE23_RECORD))
        private = ilc_record.get("privateCompiler", {})
        ilc_ok = (
            ilc_path.is_file()
            and ilc_record.get("schema") == 1
            and ilc_record.get("target") == "guidexos-x64"
            and ilc_record.get("targetOs") == "guidexos"
            and ilc_record.get("runtimeSourceCommit") == expected_commit
            and ilc_record.get("ilcPatchIdentity") == lock.get("ilcPatchIdentity")
            and ilc_record.get("ilcPatchRecordSha256", "").upper() == patch_record_hash
            and private.get("sha256", "").upper() == ilc_hash
            and private.get("length") == ilc_path.stat().st_size
        )
        if not ilc_ok:
            issues.append("PRIVATE_ILC_RECORD_OR_HASH_MISMATCH")
        result["privateIlc"] = {"verified": bool(ilc_ok), "sha256": ilc_hash if ilc_path.is_file() else None}
    except (OSError, ValueError, AttributeError, json.JSONDecodeError):
        issues.append("PRIVATE_ILC_MISSING_OR_INVALID")

    pack_files_present = all((cache_root / Path(relative)).is_file() for relative in RUNTIME_PACK_FILES)
    manifest_ok = False
    package_hashes_bound = False
    try:
        manifest = read_json(cache_root / Path(RUNTIME_PACK_MANIFEST))
        packages = [item for item in manifest.get("packages", []) if isinstance(item, dict)]
        package_ids = {str(item.get("id", "")) for item in packages}
        expected_packages = {
            "Microsoft.DotNet.ILCompiler": cache_root / "out/dotnet/phase23-runtime-pack/packages/microsoft.dotnet.ilcompiler.9.0.0.nupkg",
            "runtime.guidexos-x64.Microsoft.DotNet.ILCompiler": cache_root / "out/dotnet/phase23-runtime-pack/packages/runtime.guidexos-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg",
            "runtime.win-x64.Microsoft.DotNet.ILCompiler": cache_root / "out/dotnet/phase23-runtime-pack/packages/runtime.win-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg",
        }
        package_paths_match = all(
            item.get("version") == "9.0.0"
            and str(Path(str(item.get("path", ""))).resolve()) == str(expected_packages[str(item.get("id"))].resolve())
            for item in packages
            if str(item.get("id")) in expected_packages
        ) and package_ids == set(expected_packages)
        manifest_ok = (
            manifest.get("schema") == 2
            and manifest.get("packageIdentity") == "runtime.guidexos-x64.Microsoft.DotNet.ILCompiler"
            and manifest.get("target") == "guidexos-x64"
            and manifest.get("targetOs") == "guidexos"
            and manifest.get("architecture") == "x64"
            and manifest.get("version") == "9.0.0"
            and manifest.get("sourceCommit") == expected_commit
            and manifest.get("privateIlcPatchIdentity") == lock.get("ilcPatchIdentity")
            and package_paths_match
        )
        package_hashes_bound = all(
            isinstance(item.get("sha256"), str) and re.fullmatch(r"[0-9A-Fa-f]{64}", item["sha256"])
            for item in manifest.get("packages", []) if isinstance(item, dict)
        )
        if not manifest_ok:
            issues.append("RUNTIME_PACK_MANIFEST_IDENTITY_MISMATCH")
        if not package_hashes_bound:
            issues.append("RUNTIME_PACK_PACKAGE_HASHES_NOT_BOUND_BY_MANIFEST")
    except (OSError, ValueError, TypeError, json.JSONDecodeError):
        issues.append("RUNTIME_PACK_MANIFEST_MISSING_OR_INVALID")
    if not pack_files_present:
        issues.append("RUNTIME_PACK_REQUIRED_FILE_MISSING")
    result["runtimePack"] = {
        "locallyPresent": pack_files_present and manifest_ok,
        "manifestIdentityValid": manifest_ok,
        "packageHashesBound": package_hashes_bound,
        "shareEligible": pack_files_present and manifest_ok and package_hashes_bound,
    }
    cache_content_complete = (
        result["runtimeSource"]["verified"]
        and result["patchEvidence"]["verified"]
        and result["privateIlc"].get("verified", False)
        and result["runtimePack"]["locallyPresent"]
    )
    result["cacheContentComplete"] = cache_content_complete
    result["buildLocationCompatible"] = root == cache_root
    result["localBuildPrerequisitesPresent"] = cache_content_complete and result["buildLocationCompatible"]
    result["shareEligible"] = cache_content_complete and result["runtimePack"]["shareEligible"]
    if result["localBuildPrerequisitesPresent"] and not result["shareEligible"]:
        result["status"] = "LOCALLY_PRESENT_UNHASHED_PACKAGES"
    elif cache_content_complete and not result["buildLocationCompatible"]:
        result["status"] = "COMPATIBLE_CONTENT_NOT_MATERIALIZABLE_BY_CURRENT_BUILD"
    else:
        result["status"] = "VERIFIED" if result["shareEligible"] else "INCOMPLETE"
    return result


def compare_staged_payloads(source_root: Path, receiving_root: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    source_stage = source_root / "ramdisk_src" / "Native"
    receiving_stage = receiving_root / "ramdisk_src" / "Native"
    for payload in PAYLOADS:
        source_exe = source_stage / f"{payload.basename}.exe"
        source_descriptor = source_stage / f"{payload.basename}.gxmi"
        receiving_exe = receiving_stage / f"{payload.basename}.exe"
        receiving_descriptor = receiving_stage / f"{payload.basename}.gxmi"
        exists = all(path.is_file() for path in (source_exe, source_descriptor, receiving_exe, receiving_descriptor))
        source_exe_hash = sha256_file(source_exe) if source_exe.is_file() else None
        source_descriptor_hash = sha256_file(source_descriptor) if source_descriptor.is_file() else None
        receiving_exe_hash = sha256_file(receiving_exe) if receiving_exe.is_file() else None
        receiving_descriptor_hash = sha256_file(receiving_descriptor) if receiving_descriptor.is_file() else None
        same = exists and source_exe_hash == receiving_exe_hash and source_descriptor_hash == receiving_descriptor_hash
        rows.append(
            {
                "phase": payload.phase,
                "variant": payload.variant,
                "sameExecutable": bool(exists and source_exe_hash == receiving_exe_hash),
                "sameDescriptor": bool(exists and source_descriptor_hash == receiving_descriptor_hash),
                "samePair": bool(same),
                "sourceExecutableSha256": source_exe_hash,
                "receivingExecutableSha256": receiving_exe_hash,
                "sourceDescriptorSha256": source_descriptor_hash,
                "receivingDescriptorSha256": receiving_descriptor_hash,
            }
        )
    return rows


def audit(target: Path, candidates: list[Path]) -> dict[str, Any]:
    target = target.resolve()
    candidates_report: list[dict[str, Any]] = []
    for candidate_arg in candidates:
        candidate = candidate_arg.resolve()
        source = validate_artifacts(candidate)
        contract = compare_contracts(candidate, target)
        comparisons = compare_staged_payloads(candidate, target)
        runtime_candidate = audit_runtime(target, candidate)
        candidate_verified = source["verified"]
        all_pairs_match = len(comparisons) == len(PAYLOADS) and all(row["samePair"] for row in comparisons)
        eligible = candidate_verified and contract["compatible"] and all_pairs_match
        refused: list[str] = []
        if not candidate_verified:
            refused.append("SOURCE_CHAIN_NOT_ACCEPTED_BY_NATIVE_IDENTITY_GENERATOR")
        if not contract["compatible"]:
            refused.append("BUILD_CONTRACT_FINGERPRINT_MISMATCH")
        if not all_pairs_match:
            refused.append("RECEIVING_STAGE_DIFFERS_OR_IS_INCOMPLETE")
        for row in comparisons:
            if not candidate_verified:
                row["classification"] = "SAME_BYTES_BUT_SOURCE_UNVERIFIED" if row["samePair"] else "REBUILD_OR_REFUSE_VARIANT"
            elif not contract["compatible"]:
                row["classification"] = "BUILD_CONTRACT_MISMATCH"
            else:
                row["classification"] = "REUSABLE_CANDIDATE" if row["samePair"] else "REBUILD_VARIANT"
        candidates_report.append(
            {
                "root": str(candidate),
                "sourceIdentity": source["sourceIdentity"],
                "sourceArtifactAudit": source,
                "runtimeDependencyAudit": runtime_candidate,
                "buildContract": contract,
                "samePairCount": sum(1 for row in comparisons if row["samePair"]),
                "requiredArtifactCount": len(PAYLOADS),
                "fullSetReusableCandidate": eligible,
                "refusalReasons": refused,
                "artifacts": comparisons,
            }
        )
    runtime = audit_runtime(target)
    return {
        "schema": 1,
        "tool": "uefi_build_bootstrap.py",
        "mode": "read-only-audit",
        "materializationSupported": False,
        "target": {
            "root": str(target),
            "identity": git_metadata(target),
            "runtimeDependencies": runtime,
        },
        "requiredProofArtifactCount": len(PAYLOADS),
        "candidateCount": len(candidates_report),
        "candidates": candidates_report,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True, help="Receiving checkout to audit")
    parser.add_argument(
        "--candidate-root",
        type=Path,
        action="append",
        default=[],
        help="Existing checkout/cache candidate; may be supplied more than once",
    )
    args = parser.parse_args()
    if not args.candidate_root:
        parser.error("at least one --candidate-root is required; this prototype does not search or write a cache")
    report = audit(args.root, args.candidate_root)
    json.dump(report, sys.stdout, indent=2, sort_keys=True)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
