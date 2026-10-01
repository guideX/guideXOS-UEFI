#!/usr/bin/env python3
"""Verify Phase 34 NativeAOT resource artifacts and reject mutations."""

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

from phase34_flags import PHASE34_FLAGS  # noqa: E402

BASE_FLAGS = 0x0F
PHASE26 = 1 << 4


def validate_mode(image: bytes, descriptor_bytes: bytes, mode: str) -> dict:
    descriptor = read_descriptor(descriptor_bytes)
    validate(image, descriptor)
    if descriptor["flags"] != BASE_FLAGS | PHASE26 | PHASE34_FLAGS[mode]:
        raise Rejected("PHASE34_MODE_FLAGS")
    return descriptor


def negative_matrix(image: bytes, descriptor_bytes: bytes, mode: str) -> list[dict]:
    cases = []
    wrong_flags = bytearray(descriptor_bytes)
    flags = struct.unpack_from("<I", wrong_flags, 16)[0]
    struct.pack_into("<I", wrong_flags, 16, flags ^ PHASE34_FLAGS[mode])
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
    parser.add_argument("--mode", choices=sorted(PHASE34_FLAGS), default="success")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    image = args.image.read_bytes()
    descriptor_bytes = args.descriptor.read_bytes()
    descriptor = validate_mode(image, descriptor_bytes, args.mode)
    link = verify_map(args.map, args.link_response)
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
        print(f"PHASE34_ARTIFACT_VALIDATION_PASS=0: {error}", file=sys.stderr)
        raise SystemExit(1)
