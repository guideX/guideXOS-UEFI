#!/usr/bin/env python3
"""Build the bounded binary descriptor consumed by the Phase 24 loader.

The descriptor is not executable metadata and is never trusted by itself.  The
kernel validates the descriptor, then re-parses the PE bytes and requires both
representations to agree before allocating a user mapping.
"""

from __future__ import annotations

import argparse
import hashlib
import re
import struct
from pathlib import Path

import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from inspect_managed_image import parse_pe  # noqa: E402


MAGIC = 0x494D5847  # "GXMI" little-endian
VERSION = 1
TARGET_GUIDEXOS = 0x47554944  # "GUID"
MACHINE_AMD64 = 0x8664
FLAG_FIXED_BASE = 1 << 0
FLAG_NO_RELOCATIONS = 1 << 1
FLAG_X3_GS = 1 << 2
FLAG_MANAGED_ENTRY_BLOCKED = 1 << 3
PERM_R = 1 << 0
PERM_W = 1 << 1
PERM_X = 1 << 2

HEADER = struct.Struct("<IIIIIIQIIIIIIIIIIIII32s")
SECTION = struct.Struct("<IIIIIBBH")


def permissions(text: str) -> int:
    value = 0
    if "R" in text:
        value |= PERM_R
    if "W" in text:
        value |= PERM_W
    if "X" in text:
        value |= PERM_X
    return value


def symbol_rva(map_text: str, name: str, base: int) -> int:
    pattern = re.compile(
        rf"^\s*[0-9A-Fa-f]{{4}}:[0-9A-Fa-f]{{8}}\s+{re.escape(name)}\s+([0-9A-Fa-f]{{16}})\s",
        re.MULTILINE,
    )
    match = pattern.search(map_text)
    if not match:
        return 0
    address = int(match.group(1), 16)
    if address < base or address - base > 0xFFFFFFFF:
        return 0
    return address - base


def build(image_path: Path, map_path: Path, output_path: Path) -> None:
    image = image_path.read_bytes()
    report = parse_pe(image)
    base = int(report["imageBase"], 16)
    if base != 0x0000401000000000:
        raise SystemExit("Phase 24 requires the fixed Phase 23 image base")
    if report["machineName"] != "AMD64" or report["format"] != "PE32+":
        raise SystemExit("Phase 24 requires an AMD64 PE32+ image")

    map_text = map_path.read_text(errors="replace")
    tls_current_thread = symbol_rva(map_text, "tls_CurrentThread", base)
    tls_index = symbol_rva(map_text, "_tls_index", base)
    modules_a = symbol_rva(map_text, "__modules_a", base)
    modules_z = symbol_rva(map_text, "__modules_z", base)
    if not tls_current_thread or not tls_index or not modules_a or modules_z < modules_a:
        raise SystemExit("Phase 24 runtime symbols are incomplete")

    digest = hashlib.sha256(image).digest()
    # Native bootstrap is intentionally zero for the accepted Phase 23 image.
    # A non-zero value may only be introduced by a separately reviewed Phase 24
    # toolchain artifact; the kernel refuses to execute this descriptor as-is.
    header = HEADER.pack(
        MAGIC,
        VERSION,
        TARGET_GUIDEXOS,
        MACHINE_AMD64,
        FLAG_FIXED_BASE | FLAG_NO_RELOCATIONS | FLAG_X3_GS | FLAG_MANAGED_ENTRY_BLOCKED,
        len(image),
        base,
        int(report["imageSize"]),
        int(report["entryPointRva"], 16),
        int(report["sectionAlignment"]),
        int(report["fileAlignment"]),
        len(report["sections"]),
        1,  # PAL schema
        0x58,  # X3 GS -> TLS vector pointer
        tls_current_thread,
        tls_index,
        modules_a,
        modules_z - modules_a,
        0,  # native bootstrap RVA: intentionally unavailable in Phase 23
        int(report["entryPointRva"], 16),  # managed entry RVA
        digest,
    )

    sections = bytearray()
    for section in report["sections"]:
        sections += SECTION.pack(
            int(section["virtualAddress"]),
            int(section["virtualSize"]),
            int(section["rawPointer"]),
            int(section["rawSize"]),
            ((int(section["virtualSize"]) + 0xFFF) // 0x1000) * 0x1000,
            permissions(section["permissions"]),
            0,
            0,
        )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(header + sections)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("map", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    build(args.image, args.map, args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
