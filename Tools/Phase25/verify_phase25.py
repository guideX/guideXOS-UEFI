#!/usr/bin/env python3
"""Host-side GXBI and raw bootstrap validation; never executes the image."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

MAGIC = 0x49425847
VERSION = 1
MACHINE_AMD64 = 0x8664
PREFERRED_BASE = 0x0000401200000000
USER_LIMIT = 0x0000800000000000
HEADER = struct.Struct("<IIIIIIQIIIIIII32s")
REQUIRED_FLAGS = 0x1F


class Rejected(Exception):
    pass


def read_descriptor(data: bytes) -> dict:
    if len(data) != HEADER.size:
        raise Rejected("DESCRIPTOR_SIZE")
    values = HEADER.unpack(data)
    return {
        "magic": values[0], "version": values[1], "machine": values[2],
        "flags": values[3], "fileSize": values[4], "imageSize": values[5],
        "preferredBase": values[6], "entryOffset": values[7],
        "executableOffset": values[8], "executableSize": values[9],
        "startupVersion": values[10], "abiVersion": values[11],
        "importCount": values[12], "relocationCount": values[13],
        "sha256": values[14],
    }


def validate(image: bytes, descriptor: dict) -> None:
    if descriptor["magic"] != MAGIC or descriptor["version"] != VERSION:
        raise Rejected("IDENTITY")
    if descriptor["machine"] != MACHINE_AMD64:
        raise Rejected("MACHINE")
    if descriptor["flags"] & REQUIRED_FLAGS != REQUIRED_FLAGS:
        raise Rejected("FLAGS")
    if descriptor["fileSize"] != len(image) or descriptor["imageSize"] != (len(image) + 0xFFF) // 0x1000 * 0x1000:
        raise Rejected("SIZE")
    if descriptor["preferredBase"] != PREFERRED_BASE:
        raise Rejected("ADDRESS")
    if descriptor["imageSize"] == 0 or descriptor["preferredBase"] + descriptor["imageSize"] >= USER_LIMIT:
        raise Rejected("RANGE")
    if descriptor["entryOffset"] != 0 or descriptor["executableOffset"] != 0 or descriptor["executableSize"] != len(image):
        raise Rejected("EXECUTABLE_RANGE")
    if descriptor["startupVersion"] != 1 or descriptor["abiVersion"] != 1:
        raise Rejected("ABI")
    if descriptor["importCount"] != 0 or descriptor["relocationCount"] != 0:
        raise Rejected("IMPORTS_OR_RELOCATIONS")
    if descriptor["sha256"] != hashlib.sha256(image).digest():
        raise Rejected("ARTIFACT_HASH")
    # A raw freestanding image has no PE/ELF import or relocation containers.
    if image[:2] in (b"MZ", b"PK") or image[:4] == b"\x7fELF":
        raise Rejected("CONTAINER_FORMAT")


def negative_matrix(image: bytes, descriptor_bytes: bytes, descriptor: dict) -> list[dict[str, str]]:
    cases = []
    for name, offset, value in (
        ("wrong version", 4, 2),
        ("wrong preferred base", 24, 0x0000401300000000),
        ("nonzero imports", 52, 1),
        ("wrong hash", 64, 0),
    ):
        mutated = bytearray(descriptor_bytes)
        if offset == 24:
            struct.pack_into("<Q", mutated, offset, value)
        elif offset == 64:
            mutated[offset] ^= 0xFF
        else:
            struct.pack_into("<I", mutated, offset, value)
        try:
            validate(image, read_descriptor(bytes(mutated)))
        except Rejected as error:
            cases.append({"case": name, "result": "REJECTED", "reason": str(error)})
        else:
            raise AssertionError(name)
    return cases


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("descriptor", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    image = args.image.read_bytes()
    descriptor_bytes = args.descriptor.read_bytes()
    descriptor = read_descriptor(descriptor_bytes)
    validate(image, descriptor)
    result = {
        "result": "PASS",
        "format": "GXBI",
        "descriptorVersion": descriptor["version"],
        "architecture": "AMD64",
        "preferredUserVa": f"0x{descriptor['preferredBase']:016x}",
        "imageSize": descriptor["imageSize"],
        "entry": f"0x{descriptor['preferredBase'] + descriptor['entryOffset']:016x}",
        "sha256": hashlib.sha256(image).hexdigest().upper(),
        "importCount": descriptor["importCount"],
        "relocationCount": descriptor["relocationCount"],
        "permissions": "RX",
        "startupBlockVersion": descriptor["startupVersion"],
        "abiVersion": descriptor["abiVersion"],
        "negativeCases": negative_matrix(image, descriptor_bytes, descriptor),
        "instructionPath": [
            "RDI startup-block contract checks",
            "GS+0x58 TLS-vector load and slot-zero runtime-state check",
            "Phase 23 tls_CurrentThread/_tls_index location checks",
            "bounded user-owned FLS slot set/get/clear",
            "long register/stack sentinel loop for IRQ0 preemption",
            "Ping ABI query",
            "System Information copied service request and bounded response checks",
            "Exit(0) ABI",
        ],
        "managedEntry": "never called; wmain remains kernel-gated",
    }
    encoded = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8", newline="\n")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
