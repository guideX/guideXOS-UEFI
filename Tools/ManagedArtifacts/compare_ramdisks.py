#!/usr/bin/env python3
"""Compare managed proof executables extracted from two exact RDSK images."""

from __future__ import annotations

import argparse
import csv
import hashlib
import importlib.util
import struct
import sys
from pathlib import Path


def load_payloads() -> tuple:
    module_path = Path(__file__).with_name("managed_artifacts.py")
    spec = importlib.util.spec_from_file_location("managed_artifacts", module_path)
    if spec is None or spec.loader is None:
        raise ValueError(f"Could not load payload manifest: {module_path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module.PAYLOADS


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def read_ramdisk(path: Path) -> dict[str, bytes]:
    data = path.read_bytes()
    if len(data) < 12 or data[:4] != b"RDSK" or struct.unpack_from("<I", data, 4)[0] != 1:
        raise ValueError(f"Ramdisk header is invalid: {path}")
    count = struct.unpack_from("<I", data, 8)[0]
    offset = 12
    entries: dict[str, bytes] = {}
    for _ in range(count):
        if offset + 2 > len(data):
            raise ValueError(f"Ramdisk entry path length is truncated: {path}")
        path_length = struct.unpack_from("<H", data, offset)[0]
        offset += 2
        if offset + path_length + 4 > len(data):
            raise ValueError(f"Ramdisk entry header is truncated: {path}")
        name = data[offset : offset + path_length].decode("utf-8")
        offset += path_length
        size = struct.unpack_from("<I", data, offset)[0]
        offset += 4
        if offset + size > len(data) or name in entries:
            raise ValueError(f"Ramdisk entry is truncated or duplicated: {name}")
        entries[name] = data[offset : offset + size]
        offset += size
    if offset != len(data):
        raise ValueError(f"Ramdisk has unaccounted trailing bytes: {path}")
    return entries


def timestamp_fields(image: bytes, label: str) -> tuple[int, int, list[int], list[int]]:
    if len(image) < 0x40:
        raise ValueError(f"PE image is too short: {label}")
    pe_offset = struct.unpack_from("<I", image, 0x3C)[0]
    if pe_offset + 24 > len(image) or image[pe_offset : pe_offset + 4] != b"PE\0\0":
        raise ValueError(f"PE header is invalid: {label}")
    section_count = struct.unpack_from("<H", image, pe_offset + 6)[0]
    optional_size = struct.unpack_from("<H", image, pe_offset + 20)[0]
    optional_offset = pe_offset + 24
    section_table = optional_offset + optional_size
    if section_table + section_count * 40 > len(image):
        raise ValueError(f"PE section table is truncated: {label}")
    if optional_size < 112 + 7 * 8 or struct.unpack_from("<H", image, optional_offset)[0] != 0x20B:
        raise ValueError(f"PE32+ debug directory is missing: {label}")

    directory_offset = optional_offset + 112 + 6 * 8
    debug_rva, debug_size = struct.unpack_from("<II", image, directory_offset)
    offsets: list[int] = []
    if debug_rva or debug_size:
        if debug_rva == 0 or debug_size < 28 or debug_size % 28:
            raise ValueError(f"PE debug directory is malformed: {label}")
        debug_raw: int | None = None
        for index in range(section_count):
            section_offset = section_table + index * 40
            virtual_size, virtual_address, raw_size, raw_pointer = struct.unpack_from(
                "<IIII", image, section_offset + 8
            )
            if virtual_address <= debug_rva < virtual_address + max(virtual_size, raw_size):
                debug_raw = raw_pointer + (debug_rva - virtual_address)
                break
        if debug_raw is None or debug_raw + debug_size > len(image):
            raise ValueError(f"PE debug directory is not section-backed: {label}")
        offsets = [debug_raw + index * 28 + 4 for index in range(debug_size // 28)]
    values = [struct.unpack_from("<I", image, offset)[0] for offset in offsets]
    return pe_offset + 8, struct.unpack_from("<I", image, pe_offset + 8)[0], offsets, values


def canonical_sha(image: bytes, label: str) -> tuple[str, int, list[int], list[int]]:
    coff_offset, coff_value, debug_offsets, debug_values = timestamp_fields(image, label)
    normalized = bytearray(image)
    normalized[coff_offset : coff_offset + 4] = b"\0\0\0\0"
    for offset in debug_offsets:
        normalized[offset : offset + 4] = b"\0\0\0\0"
    return sha256(normalized), coff_value, debug_offsets, debug_values


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--first", required=True, type=Path)
    parser.add_argument("--second", required=True, type=Path)
    parser.add_argument("--csv", required=True, type=Path)
    parser.add_argument("--require-raw-equal", action="store_true")
    args = parser.parse_args()

    first, second = read_ramdisk(args.first), read_ramdisk(args.second)
    rows = []
    payloads = load_payloads()
    for payload in payloads:
        name = payload.basename + ".exe"
        key = "Native/" + name
        left, right = first.get(key), second.get(key)
        if left is None or right is None:
            raise ValueError(f"Managed executable is missing from an RDSK: {key}")
        left_normalized, left_coff, left_offsets, left_values = canonical_sha(left, key)
        right_normalized, right_coff, right_offsets, right_values = canonical_sha(right, key)
        rows.append(
            {
                "phase": payload.phase,
                "variant": payload.variant,
                "executable": name,
                "bytes_first": len(left),
                "bytes_second": len(right),
                "raw_sha256_first": sha256(left),
                "raw_sha256_second": sha256(right),
                "raw_equal": str(left == right).lower(),
                "canonical_sha256_first": left_normalized,
                "canonical_sha256_second": right_normalized,
                "canonical_equal": str(left_normalized == right_normalized).lower(),
                "first_coff_timestamp": f"0x{left_coff:08X}",
                "second_coff_timestamp": f"0x{right_coff:08X}",
                "first_debug_timestamp_offsets": ";".join(f"0x{x:X}" for x in left_offsets),
                "second_debug_timestamp_offsets": ";".join(f"0x{x:X}" for x in right_offsets),
                "first_debug_timestamps": ";".join(f"0x{x:08X}" for x in left_values),
                "second_debug_timestamps": ";".join(f"0x{x:08X}" for x in right_values),
            }
        )

    if len(rows) != 22:
        raise ValueError(f"Expected 22 managed artifacts, found {len(rows)}")
    args.csv.parent.mkdir(parents=True, exist_ok=True)
    with args.csv.open("w", newline="", encoding="utf-8") as output:
        writer = csv.DictWriter(output, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)

    raw_matches = sum(row["raw_equal"] == "true" for row in rows)
    canonical_matches = sum(row["canonical_equal"] == "true" for row in rows)
    print(f"ARTIFACTS={len(rows)}")
    print(f"RAW_EQUAL={raw_matches}/{len(rows)}")
    print(f"CANONICAL_EQUAL={canonical_matches}/{len(rows)}")
    print(f"COMPARISON_CSV={args.csv}")
    if canonical_matches != len(rows) or (args.require_raw_equal and raw_matches != len(rows)):
        return 1
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, struct.error) as error:
        print(f"ramdisk comparison error: {error}", file=sys.stderr)
        raise SystemExit(2)
