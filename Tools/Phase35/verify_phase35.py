#!/usr/bin/env python3
"""Verify Phase 35 managed Persistent-read artifacts and reject mutations."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools" / "Phase24"))
sys.path.insert(0, str(ROOT / "Tools" / "Phase27"))
from verify_phase24 import Rejected, read_descriptor, validate  # noqa: E402
from verify_phase27 import verify_map  # noqa: E402

from phase35_flags import PHASE35_FLAGS  # noqa: E402

BASE_FLAGS = 0x0F
PHASE26 = 1 << 4


def map_metrics(map_path: Path) -> dict:
    text = map_path.read_text(errors="replace")
    lines = text.splitlines()
    symbol_lines = [line for line in lines if re.match(
        r"\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+\S+", line)]
    symbols = [match.group(1) for line in symbol_lines
               if (match := re.match(
                   r"\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(\S+)", line))]
    gc_helpers = [name for name in symbols if re.match(
        r"Rhp(?:Gc|New|Collect|GetGc|StartNoGC|EndNoGC)", name)]
    exception_helpers = [name for name in symbols if re.match(
        r"Rhp(?:Throw|Rethrow|CallCatch|CallFinally|CallFilter|EH|PInvokeException|GetClasslibFunction)",
        name)]
    pal_symbols = sorted({name for name in symbols
                          if name.startswith("guidexos_pal_")})
    tls_symbols = [name for name in symbols if re.search(
        r"tls|TLS|ThreadStatic|ThreadLocal|g_guidexos_tls", name)]
    tls_sections = [line.strip() for line in lines if ".tls$" in line]
    return {
        "mapBytes": map_path.stat().st_size,
        "mapRecords": len(symbol_lines),
        "gcHelperCount": len(gc_helpers),
        "gcHelpers": gc_helpers,
        "exceptionHelperCount": len(exception_helpers),
        "exceptionHelpers": exception_helpers,
        "tlsStaticSymbolCount": len(tls_symbols),
        "tlsStaticSymbols": tls_symbols,
        "tlsSections": tls_sections,
        "palSymbols": pal_symbols,
    }


def validate_mode(image: bytes, descriptor_bytes: bytes,
                  mode: str) -> dict:
    descriptor = read_descriptor(descriptor_bytes)
    validate(image, descriptor)
    expected = BASE_FLAGS | PHASE26 | PHASE35_FLAGS[mode]
    if descriptor["flags"] != expected:
        raise Rejected("PHASE35_MODE_FLAGS")
    return descriptor


def negative_matrix(image: bytes, descriptor_bytes: bytes,
                    mode: str) -> list[dict]:
    cases = []
    wrong_flags = bytearray(descriptor_bytes)
    flags = struct.unpack_from("<I", wrong_flags, 16)[0]
    struct.pack_into("<I", wrong_flags, 16, flags ^ 0x20000000)
    bad_hash = bytearray(descriptor_bytes)
    bad_hash[84] ^= 1
    bad_image = bytearray(image)
    bad_image[0x1000] ^= 1
    candidates = [
        ("wrong proof flags/mode", image, bytes(wrong_flags)),
        ("descriptor digest mutation", image, bytes(bad_hash)),
        ("artifact byte mutation", bytes(bad_image), descriptor_bytes),
    ]
    for name, candidate_image, candidate_descriptor in candidates:
        try:
            validate_mode(candidate_image, candidate_descriptor, mode)
        except Rejected as error:
            cases.append({"case": name, "result": "REJECTED",
                          "reason": str(error)})
        else:
            raise AssertionError(f"negative artifact case was accepted: {name}")
    return cases


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("descriptor", type=Path)
    parser.add_argument("map", type=Path)
    parser.add_argument("link_response", type=Path)
    parser.add_argument("--mode", choices=sorted(PHASE35_FLAGS), default="success")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    image = args.image.read_bytes()
    descriptor_bytes = args.descriptor.read_bytes()
    descriptor = validate_mode(image, descriptor_bytes, args.mode)
    map_text = args.map.read_text(errors="replace")
    if "guidexos_pal_persistent_storage_read" not in map_text:
        raise Rejected("PERSISTENT_READ_PAL_SYMBOL_MISSING")
    link = verify_map(args.map, args.link_response)
    metrics = map_metrics(args.map)
    if any(name in args.link_response.read_text(errors="replace").lower()
           for name in ("kernel32.lib", "user32.lib", "advapi32.lib",
                        "ole32.lib", "ntdll.lib", "ws2_32.lib")):
        raise Rejected("WINDOWS_LINK_INPUT")
    phase34_map = ROOT / "out" / "dotnet" / "phase34-managed-resource-read" / "payloads" / "success" / "guidexos.map"
    dependency_delta = None
    if phase34_map.is_file():
        baseline = map_metrics(phase34_map)
        dependency_delta = {
            key: metrics[key] - baseline[key]
            for key in ("mapBytes", "mapRecords", "gcHelperCount",
                        "exceptionHelperCount", "tlsStaticSymbolCount")
        }
    result = {
        "result": "PASS",
        "mode": args.mode,
        "artifact": str(args.image),
        "artifactSize": len(image),
        "artifactSha256": hashlib.sha256(image).hexdigest().upper(),
        "descriptorFlags": f"0x{descriptor['flags']:08x}",
        "sectionCount": descriptor["sectionCount"],
        "windowsImports": 0,
        "directKernelImports": 0,
        "relocations": False,
        "tlsDirectory": False,
        "rwxSections": 0,
        "pal": link,
        "persistentReadPalSymbol": "guidexos_pal_persistent_storage_read",
        "mapMetrics": metrics,
        "phase34DependencyDelta": dependency_delta,
        "negativeCases": negative_matrix(image, descriptor_bytes, args.mode),
        "managedCodeExecuted": False,
    }
    encoded = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8", newline="\n")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, Rejected, AssertionError) as error:
        print(f"PHASE35_ARTIFACT_VALIDATION_PASS=0: {error}", file=sys.stderr)
        raise SystemExit(1)
