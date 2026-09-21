#!/usr/bin/env python3
"""Create the Phase 19 runtime/PAL dependency-control manifest.

This deliberately reports an honest gate when only the contract object exists.
It never converts the stock win-x64 import list into a guideXOS result.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path
from typing import Any


EXPECTED_STOCK_IMPORTS = 131
EXPECTED_STOCK_DLLS = 11
EXPECTED_EXTERNAL_PAL_SYMBOLS = 20
WINDOWS_MARKERS = ("windows", "kernel32", "advapi", "ole32", "bcrypt", "crt", "win32", "loadlibrary", "getprocaddress")


def run_git(root: Path, *args: str) -> str | None:
    try:
        result = subprocess.run(
            ["git", "-C", str(root), *args],
            check=True,
            capture_output=True,
            text=True,
        )
    except (OSError, subprocess.CalledProcessError):
        return None
    return result.stdout.strip()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def hex_number(value: Any) -> int | None:
    if value is None:
        return None
    try:
        return int(str(value), 0)
    except (TypeError, ValueError):
        return None


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stock-manifest", required=True, type=Path)
    parser.add_argument("--pal-contract", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--target", default="guidexos-x64")
    parser.add_argument("--repository", type=Path)
    parser.add_argument("--contract-source", type=Path)
    parser.add_argument("--contract-object", type=Path)
    parser.add_argument("--contract-archive", type=Path)
    args = parser.parse_args()

    stock = load_json(args.stock_manifest)
    contract = load_json(args.pal_contract)
    imports = stock.get("imports", [])
    import_count = sum(len(entry.get("names", [])) for entry in imports)
    dll_count = len(imports)
    external_symbols = [
        symbol
        for category in contract.get("categories", [])
        for symbol in category.get("symbols", [])
    ]
    symbol_names = [str(symbol.get("name", "")) for symbol in external_symbols]
    local_helpers = contract.get("localHelpers", [])
    forbidden = [name for name in symbol_names if any(marker in name.lower() for marker in WINDOWS_MARKERS)]

    if args.target != contract.get("targetIdentity"):
        raise ValueError(f"Target mismatch: {args.target} != {contract.get('targetIdentity')}")
    if import_count != EXPECTED_STOCK_IMPORTS or dll_count != EXPECTED_STOCK_DLLS:
        raise ValueError(
            f"Stock manifest changed: expected {EXPECTED_STOCK_IMPORTS}/{EXPECTED_STOCK_DLLS}, "
            f"got {import_count}/{dll_count}"
        )
    if len(symbol_names) != EXPECTED_EXTERNAL_PAL_SYMBOLS:
        raise ValueError(f"PAL contract symbol count changed: expected {EXPECTED_EXTERNAL_PAL_SYMBOLS}, got {len(symbol_names)}")
    if len(set(symbol_names)) != len(symbol_names):
        raise ValueError("PAL contract contains duplicate symbols")
    if forbidden:
        raise ValueError(f"PAL contract contains forbidden Windows-looking symbols: {forbidden}")

    repository = args.repository or args.stock_manifest.resolve().parents[2]
    status = run_git(repository, "status", "--short", "--branch")
    git = {
        "repository": str(repository),
        "branch": run_git(repository, "branch", "--show-current"),
        "head": run_git(repository, "rev-parse", "HEAD"),
        "subject": run_git(repository, "show", "-s", "--format=%s", "HEAD"),
        "upstream": run_git(repository, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}"),
        "aheadBehind": run_git(repository, "rev-list", "--left-right", "--count", "HEAD...@{upstream}"),
        "worktreeStatus": status,
        "detachedHead": not bool(run_git(repository, "branch", "--show-current")),
    }

    artifact_files: dict[str, Any] = {}
    for label, path in (
        ("source", args.contract_source),
        ("object", args.contract_object),
        ("archive", args.contract_archive),
    ):
        if path is not None and path.exists():
            artifact_files[label] = {"path": str(path), "sha256": sha256(path), "bytes": path.stat().st_size}
        else:
            artifact_files[label] = None

    map_evidence = stock.get("mapEvidence", {})
    tls = stock.get("tls", {})
    tls_start = hex_number(tls.get("start"))
    tls_end = hex_number(tls.get("end"))
    tls_template_bytes = tls_end - tls_start if tls_start is not None and tls_end is not None and tls_end >= tls_start else None
    output = {
        "schemaVersion": 1,
        "phase": 19,
        "generatedBy": "Tools/Phase19/phase19_runtime_audit.py",
        "outcome": "Outcome C - bounded CoreCLR/NativeAOT runtime source-build prerequisite",
        "status": "CONTRACT_BUILT_SOURCE_RUNTIME_REQUIRED",
        "target": {
            "identity": args.target,
            "architecture": contract.get("architecture"),
            "privateRepositoryTarget": True,
            "standardRidClaim": False,
            "freestandingSemantics": True,
        },
        "stockComparison": {
            "manifest": str(args.stock_manifest),
            "sha256": stock.get("sha256"),
            "importCount": import_count,
            "dllCount": dll_count,
            "dlls": [{"dll": entry.get("dll"), "count": len(entry.get("names", []))} for entry in imports],
            "helperCount": map_evidence.get("entryCount"),
            "writableStaticCount": len(map_evidence.get("writableStaticSections", [])),
            "gcEvidenceCount": len(map_evidence.get("gcSections", [])),
            "tlsEvidenceCount": len(map_evidence.get("tlsSections", [])),
            "readyToRunHelperCount": len(map_evidence.get("readyToRunHelpers", [])),
            "tlsTemplatePresent": tls.get("present"),
            "tlsTemplateBytes": tls_template_bytes,
            "exceptionDirectoryBytes": stock.get("directoriesSummary", {}).get("exception", [0, 0])[1],
            "rwxSections": [section.get("name") for section in stock.get("sections", []) if section.get("permissions") == "RWX"],
        },
        "customPayload": {
            "status": "NOT_BUILT",
            "reason": "The installed .NET 9 package selects Microsoft.NETCore.Native.Windows.targets, Windows SDK libraries, and Windows PAL/runtime objects. Source adaptation is required before a guideXOS artifact can be generated.",
            "managedEntry": "not attempted",
            "externalDependencyCount": None,
            "windowsDllImports": None,
            "comDependency": "not present in the PAL contract; custom payload not yet built",
            "dynamicLoaderDependency": "not admitted by the PAL contract; custom payload not yet built",
        },
        "palContract": {
            "path": str(args.pal_contract),
            "externalSymbolCount": len(symbol_names),
            "externalSymbols": symbol_names,
            "localHelperCount": len(local_helpers),
            "localHelpers": [helper.get("name") for helper in local_helpers],
            "categories": [category.get("name") for category in contract.get("categories", [])],
            "synchronizationExternalSymbols": contract.get("synchronization", {}).get("externalSymbols", []),
            "forbiddenWindowsLookingSymbols": forbidden,
        },
        "runtimeOwnership": contract.get("ownership"),
        "artifacts": artifact_files,
        "loaderCompatibility": {
            "descriptorContract": "Phase 17 descriptor remains the basis; custom image probe awaits custom payload",
            "customArtifactProbe": "not-run",
            "stockArtifactProbe": "run separately by Tools/phase17_loader_probe.py",
            "rwx": "contract policy rejects RWX; custom image not yet available",
            "bss": "contract policy retains deterministic zero-fill; custom image not yet available",
            "hostMapping": "not-run on custom image",
            "teardown": "contract-only; no pages allocated",
        },
        "negativeValidation": {
            "contractHasNoWindowsSymbolNames": not forbidden,
            "contractObjectUndefinedSymbols": "verified by build script with dumpbin /symbols",
            "stockWindowsDependenciesRemainInComparisonOnly": True,
            "customArtifactValidation": "not-run",
        },
        "git": git,
        "sourceBuildGate": {
            "required": True,
            "sourceAreas": [
                "src/coreclr/nativeaot/Runtime/Full",
                "src/coreclr/nativeaot/Runtime/windows",
                "src/coreclr/gc/windows/gcenv.windows.cpp",
                "src/libraries/System.Private.CoreLib/src/Interop/Windows",
                "eng/native and runtime-pack packaging targets",
            ],
            "largeForkApproved": False,
            "nextProof": "Build a pinned, small source adaptation; package guidexos-x64; rebuild Main() => 42; then inspect and run the existing no-execute loader probe.",
        },
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(output, stream, indent=2, ensure_ascii=False)
        stream.write("\n")
    print(json.dumps({"output": str(args.output), "outcome": output["outcome"], "status": output["status"]}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
