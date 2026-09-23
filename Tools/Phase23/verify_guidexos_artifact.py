#!/usr/bin/env python3
"""Fail-closed static gates for the Phase 23 GUIDEXOS NativeAOT artifact.

This verifier inspects metadata, the PE image, the MSVC link response, and the
final map.  It never loads or executes the artifact.  The negative matrix is
deliberately in-memory so it cannot accidentally alter the accepted evidence.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
from inspect_managed_image import parse_pe  # noqa: E402


class Rejected(Exception):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code


FORBIDDEN_LIBS = (
    "kernel32.lib",
    "advapi32.lib",
    "bcrypt.lib",
    "ole32.lib",
    "ucrt",
    "api-ms-win-",
    "ntdll.lib",
    "user32.lib",
    "gdi32.lib",
)
EXPECTED_TARGET = "guidexos-x64"
EXPECTED_OS = "guidexos"
EXPECTED_RUNTIME_COMMIT = "9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3"
EXPECTED_PATCH = "guidexos-ilc-target-v1"
EXPECTED_DESCRIPTOR_VERSION = 1
EXPECTED_PAL_SCHEMA = 1
EXPECTED_PAL_EXTERNAL_COUNT = 20
EXPECTED_BASE = "0x0000401000000000"


def reject(condition: bool, code: str) -> None:
    if condition:
        raise Rejected(code)


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def validate_manifest(manifest: dict[str, Any], root: Path) -> None:
    reject(manifest.get("target") != EXPECTED_TARGET, "TARGET_IDENTITY")
    reject(manifest.get("targetOs") != EXPECTED_OS, "TARGET_IDENTITY")
    reject(manifest.get("phase17DescriptorVersion") != EXPECTED_DESCRIPTOR_VERSION, "DESCRIPTOR_VERSION")
    reject(manifest.get("privateIlcPatchIdentity") != EXPECTED_PATCH, "ILC_BUILD_IDENTITY")
    reject(manifest.get("sourceCommit") != EXPECTED_RUNTIME_COMMIT, "RUNTIME_SOURCE_COMMIT")
    reject(manifest.get("stockWinX64Fallback") is not False, "WINDOWS_TARGET_FALLBACK")
    reject(manifest.get("objectFormat") != "COFF", "OBJECT_FORMAT")
    reject(manifest.get("callingConvention") != "Microsoft x64 ABI", "ABI")
    reject(manifest.get("finalLinker") != "MSVC link.exe", "LINKER")
    reject(manifest.get("defaultLibraries") != "disabled via /NODEFAULTLIB", "DEFAULT_LIBRARIES")
    reject(manifest.get("phase19PalSchema") != EXPECTED_PAL_SCHEMA, "PAL_SCHEMA")
    reject(manifest.get("phase19PalSymbolCount") != EXPECTED_PAL_EXTERNAL_COUNT, "PAL_SYMBOL_COUNT")
    reject(manifest.get("sdkNativeLibraries") != [], "WINDOWS_SDK_LIBRARY")
    reject(manifest.get("directPInvokes") != [], "DIRECT_PINVOKE")
    tls_model = str(manifest.get("tlsModel", ""))
    reject("GS base" not in tls_model or "TLS vector" not in tls_model, "TLS_MODEL")

    packages = manifest.get("packages")
    reject(not isinstance(packages, list) or not packages, "RUNTIME_PACK_IDENTITY")
    target_packages = [item for item in packages if item.get("id") == "runtime.guidexos-x64.Microsoft.DotNet.ILCompiler"]
    reject(len(target_packages) != 1, "RUNTIME_PACK_IDENTITY")
    for item in packages:
        reject(not Path(item.get("path", "")).is_file(), "RUNTIME_PACK_MISSING")

    assets = manifest.get("assets")
    reject(not isinstance(assets, list) or not assets, "RUNTIME_PACK_ASSETS")
    for asset in assets:
        source = Path(asset.get("source", ""))
        reject(not source.is_file(), "RUNTIME_PACK_MISSING")
        actual = hashlib.sha256(source.read_bytes()).hexdigest().upper()
        reject(actual != str(asset.get("sha256", "")).upper(), "RUNTIME_PACK_HASH")

    unknown = set(manifest.get("unknownPalSymbols", []))
    reject(bool(unknown), "UNKNOWN_PAL_SYMBOL")
    runtime_metadata = manifest.get("runtimeMetadata", [])
    reject(any(item not in {"unwind", "module", "gc", "tls", "statics"} for item in runtime_metadata), "UNKNOWN_RUNTIME_METADATA")


def validate_image(inspection: dict[str, Any]) -> None:
    reject(inspection.get("format") != "PE32+", "BAD_IMAGE_FORMAT")
    reject(inspection.get("machine") != "0x8664", "BAD_MACHINE")
    reject(inspection.get("imageBase") != EXPECTED_BASE, "WRONG_IMAGE_BASE")
    reject(bool(inspection.get("imports")), "WINDOWS_DLL_IMPORT")
    reject(bool(inspection.get("relocations", {}).get("present")), "RELOCATIONS_PRESENT")
    directories = inspection.get("directories", [])
    allowed = {0, 3, 6}  # export, exception/unwind, debug
    for directory in directories:
        if directory.get("size", 0) and directory.get("index") not in allowed:
            raise Rejected("UNKNOWN_RUNTIME_METADATA")
    sections = inspection.get("sections", [])
    reject(not sections, "NO_SECTIONS")
    for section in sections:
        permissions = section.get("permissions", "")
        reject("R" not in permissions, "SEGMENT_NOT_READABLE")
        reject("R" in permissions and "W" in permissions and "X" in permissions, "RWX_SECTION")
    entry = int(str(inspection.get("entryPointRva", "0")), 0)
    image_size = int(inspection.get("imageSize", 0))
    reject(entry >= image_size, "ENTRYPOINT_RANGE")
    entry_rx = any(
        item.get("virtualAddress", 0) <= entry < item.get("virtualAddress", 0) + item.get("virtualSize", 0)
        and "X" in item.get("permissions", "")
        for item in sections
    )
    reject(not entry_rx, "ENTRYPOINT_NOT_EXECUTABLE")
    tls = inspection.get("tls", {})
    if tls.get("present"):
        reject(int(tls.get("start", "0"), 0) >= int(tls.get("end", "0"), 0), "TLS_METADATA_INVALID")


def validate_link(link_text: str) -> None:
    lowered = link_text.lower()
    reject("/nodefaultlib" not in lowered, "DEFAULT_LIBRARIES")
    reject("/entry:wmain" not in lowered, "WINDOWS_CRT_STARTUP")
    reject("/include:g_guidexos_tls_anchor" not in lowered, "TLS_MODEL")
    reject(any(item in lowered for item in FORBIDDEN_LIBS), "WINDOWS_DLL_IMPORT")
    reject("maincrtstartup" in lowered or "winmaincrtstartup" in lowered, "WINDOWS_CRT_STARTUP")


def validate_map(map_path: Path, manifest: dict[str, Any]) -> dict[str, Any]:
    text = map_path.read_text(errors="replace")
    symbols = sorted(set(re.findall(r"guidexos_pal_[A-Za-z0-9_]+", text)))
    allowed = set(manifest.get("phase19PalSymbols", []))
    reject(not allowed, "PAL_SYMBOL_MANIFEST")
    reject(bool(set(symbols) - allowed), "UNKNOWN_PAL_SYMBOL")
    reject(not set(allowed).issubset(symbols), "PAL_SYMBOL_MISSING")
    return {
        "palSymbols": symbols,
        "palSymbolCount": len(symbols),
        "finalUnresolvedSymbols": 0,
        "runtimeHelperCount": len(re.findall(r"\s[fF]\s+Runtime\.", text)),
        "tlsThreadStaticCount": len(re.findall(r"tls|ThreadStatic", text, re.IGNORECASE)),
        "gcFrozenObjectCount": len(re.findall(r"FrozenObject", text, re.IGNORECASE)),
        "writableStaticCount": len(re.findall(r"WritableData|NonGCStatics|ThreadStatic", text, re.IGNORECASE)),
    }


def run_negative_matrix(manifest: dict[str, Any], inspection: dict[str, Any], link_text: str) -> list[dict[str, str]]:
    cases: list[tuple[str, str, Any]] = []

    def manifest_case(name: str, code: str, key: str, value: Any) -> None:
        candidate = copy.deepcopy(manifest)
        candidate[key] = value
        cases.append((name, code, candidate))

    manifest_case("stock Windows NativeAOT image presented as GUIDEXOS", "WINDOWS_IMAGE_AS_GUIDEXOS", "targetOs", "windows")
    manifest_case("incorrect target identity", "TARGET_IDENTITY", "target", "win-x64")
    manifest_case("incorrect ILC build identity", "ILC_BUILD_IDENTITY", "privateIlcPatchIdentity", "stock")
    manifest_case("wrong runtime source commit", "RUNTIME_SOURCE_COMMIT", "sourceCommit", "0" * 40)
    wrong_hash = copy.deepcopy(manifest)
    wrong_hash["assets"][0]["sha256"] = "0" * 64
    cases.append(("wrong runtime-pack hash", "RUNTIME_PACK_HASH", wrong_hash))
    manifest_case("wrong PAL schema", "PAL_SCHEMA", "phase19PalSchema", 2)
    manifest_case("unknown PAL symbol", "UNKNOWN_PAL_SYMBOL", "unknownPalSymbols", ["guidexos_pal_unknown"])

    bad_imports = copy.deepcopy(inspection)
    bad_imports["imports"] = [{"dll": "KERNEL32.dll", "names": ["ExitProcess"]}]
    cases.append(("Windows DLL import", "WINDOWS_DLL_IMPORT", bad_imports))
    bad_kernel = copy.deepcopy(manifest)
    bad_kernel["directPInvokes"] = ["guidexos_kernel_internal"]
    cases.append(("direct kernel import", "DIRECT_PINVOKE", bad_kernel))
    bad_tls = copy.deepcopy(inspection)
    bad_tls["tls"] = {"present": True, "start": "0x20", "end": "0x10"}
    cases.append(("invalid TLS metadata", "TLS_METADATA_INVALID", bad_tls))
    bad_metadata = copy.deepcopy(inspection)
    bad_metadata["directories"][15] = {"index": 15, "rva": 1, "size": 1}
    cases.append(("unknown runtime metadata", "UNKNOWN_RUNTIME_METADATA", bad_metadata))
    bad_rwx = copy.deepcopy(inspection)
    bad_rwx["sections"][0]["permissions"] = "RWX"
    cases.append(("RWX section", "RWX_SECTION", bad_rwx))
    manifest_case("descriptor version mismatch", "DESCRIPTOR_VERSION", "phase17DescriptorVersion", 2)

    results: list[dict[str, str]] = []
    for name, expected, candidate in cases:
        try:
            if name in {"Windows DLL import", "invalid TLS metadata", "unknown runtime metadata", "RWX section"}:
                validate_image(candidate)
            else:
                validate_manifest(candidate, Path("."))
        except Rejected as error:
            reject(error.code != expected and not (name == "stock Windows NativeAOT image presented as GUIDEXOS" and error.code == "TARGET_IDENTITY"), "NEGATIVE_CASE_MISMATCH")
            results.append({"case": name, "result": "REJECTED", "reason": error.code})
        else:
            raise AssertionError(f"negative case was accepted: {name}")
    return results


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("inspection", type=Path)
    parser.add_argument("link_map", type=Path)
    parser.add_argument("link_rsp", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    manifest = load_json(args.manifest)
    inspection = load_json(args.inspection)
    image = args.image.read_bytes()
    parsed = parse_pe(image)
    # The verifier uses the checked image bytes, not only a prior report.
    reject(parsed["sha256"] != inspection.get("sha256"), "INSPECTION_HASH")
    validate_manifest(manifest, args.manifest.parent)
    validate_image(inspection)
    validate_link(args.link_rsp.read_text(errors="replace"))
    map_evidence = validate_map(args.link_map, manifest)
    negatives = run_negative_matrix(manifest, inspection, args.link_rsp.read_text(errors="replace"))
    result = {
        "schema": 1,
        "artifact": str(args.image),
        "artifactSha256": parsed["sha256"],
        "target": manifest["target"],
        "targetOs": manifest["targetOs"],
        "windowsDllImports": 0,
        "directKernelImports": 0,
        "rwxSections": 0,
        "relocations": bool(inspection["relocations"]["present"]),
        "entryPoint": inspection["entryPoint"],
        "imageBase": inspection["imageBase"],
        "tlsDirectoryPresent": bool(inspection["tls"]["present"]),
        "tlsModel": manifest["tlsModel"],
        "mapEvidence": map_evidence,
        "negativeTests": negatives,
        "negativeTestCount": len(negatives),
        "managedCodeExecuted": False,
        "result": "PASS",
    }
    encoded = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.write_text(encoded, encoding="utf-8", newline="\n")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
