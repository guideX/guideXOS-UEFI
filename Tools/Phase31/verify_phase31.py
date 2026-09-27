#!/usr/bin/env python3
"""Verify Phase 31 NativeAOT launch artifacts and reject mutations."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools" / "Phase24"))
sys.path.insert(0, str(ROOT / "Tools" / "Phase27"))
from verify_phase24 import Rejected, read_descriptor, validate  # noqa: E402
from verify_phase27 import verify_map  # noqa: E402

PHASE26 = 1 << 4
BASE_FLAGS = 0x0F
PHASE31 = {
    "success": 1 << 25,
    "invalid-target": 1 << 26,
    "oversize": 1 << 27,
    "failfast": 1 << 28,
    "stale-owner": 1 << 29,
}


def validate_mode(image: bytes, descriptor_bytes: bytes, mode: str) -> dict:
    descriptor = read_descriptor(descriptor_bytes)
    validate(image, descriptor)
    expected = BASE_FLAGS | PHASE26 | PHASE31[mode]
    if descriptor["flags"] != expected:
        raise Rejected("PHASE31_MODE_FLAGS")
    if descriptor["flags"] & sum(PHASE31.values()) != PHASE31[mode]:
        raise Rejected("PHASE31_MODE_FLAG_COLLISION")
    return descriptor


def negative_matrix(image: bytes, descriptor_bytes: bytes, mode: str) -> list[dict]:
    cases = []
    missing_mode = bytearray(descriptor_bytes)
    flags = struct.unpack_from("<I", missing_mode, 16)[0]
    struct.pack_into("<I", missing_mode, 16, flags & ~PHASE31[mode])
    cases_to_try = [("missing Phase 31 mode flag", image, bytes(missing_mode))]
    bad_hash = bytearray(descriptor_bytes)
    bad_hash[84] ^= 1
    cases_to_try.append(("descriptor hash mutation", image, bytes(bad_hash)))
    bad_image = bytearray(image)
    bad_image[0x1000] ^= 1
    cases_to_try.append(("artifact byte mutation", bytes(bad_image), descriptor_bytes))
    for name, candidate_image, candidate_descriptor in cases_to_try:
        try:
            validate_mode(candidate_image, candidate_descriptor, mode)
        except Rejected as error:
            cases.append({"case": name, "result": "REJECTED", "reason": str(error)})
        else:
            raise AssertionError(f"negative artifact case was accepted: {name}")
    return cases


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("descriptor", type=Path)
    parser.add_argument("map", type=Path)
    parser.add_argument("link_response", type=Path)
    parser.add_argument("--mode", choices=sorted(PHASE31), default="success")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    image = args.image.read_bytes()
    descriptor_bytes = args.descriptor.read_bytes()
    descriptor = validate_mode(image, descriptor_bytes, args.mode)
    link = verify_map(args.map, args.link_response)
    negatives = negative_matrix(image, descriptor_bytes, args.mode)
    result = {
        "result": "PASS",
        "mode": args.mode,
        "artifact": str(args.image),
        "artifactSize": len(image),
        "artifactSha256": hashlib.sha256(image).hexdigest().upper(),
        "descriptorFlags": f"0x{descriptor['flags']:08x}",
        "sectionCount": descriptor["sectionCount"],
        "directKernelImports": 0,
        "relocations": False,
        "tlsDirectory": False,
        "rwxSections": 0,
        "pal": link,
        "negativeCases": negatives,
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
        print(f"PHASE31_ARTIFACT_VALIDATION_PASS=0: {error}", file=sys.stderr)
        raise SystemExit(1)
