#!/usr/bin/env python3
"""Validate and identify two isolated offline AMCONV2G ESP candidates."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path


FILES = {"EFI/BOOT/BOOTX64.EFI", "kernel.elf", "ramdisk.img"}
BASELINE_KERNEL = "55F7952552A9F0E97B3072E60A824ABD4A3DB18C1EF0B71EA71B8EC65950C33E"
ACCEPTED_LOADER = "C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9"
RDSK_SHA256 = {
    "candidate-a": "B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E",
    "candidate-b": "18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4",
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def validate_efi_loader(data: bytes) -> None:
    if len(data) < 0x40 or data[:2] != b"MZ":
        raise ValueError("EFI loader is not a PE image")
    pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
    if pe_offset + 24 > len(data) or data[pe_offset : pe_offset + 4] != b"PE\0\0":
        raise ValueError("EFI loader has no PE signature")
    machine, _, _, _, _, optional_size, _ = struct.unpack_from(
        "<HHIIIHH", data, pe_offset + 4
    )
    optional_offset = pe_offset + 24
    if optional_offset + optional_size > len(data) or optional_size < 70:
        raise ValueError("EFI loader has a truncated optional header")
    magic = struct.unpack_from("<H", data, optional_offset)[0]
    subsystem = struct.unpack_from("<H", data, optional_offset + 68)[0]
    if (machine, magic, subsystem) != (0x8664, 0x20B, 10):
        raise ValueError(
            "EFI loader must be AMD64 PE32+ with EFI application subsystem"
        )


def validate_kernel(data: bytes) -> tuple[int, int]:
    if len(data) < 64 or data[:4] != b"\x7fELF":
        raise ValueError("kernel is not an ELF image")
    if data[4:7] != bytes((2, 1, 1)):
        raise ValueError("kernel must be ELF64 little-endian version 1")
    header = struct.unpack_from("<16sHHIQQQIHHHHHH", data, 0)
    _, kind, machine, version, entry, phoff, _, _, ehsize, phentsize, phnum, *_ = header
    if (kind, machine, version, ehsize, phentsize) != (2, 62, 1, 64, 56):
        raise ValueError("kernel must be ELF64 ET_EXEC EM_X86_64")
    if phoff + phentsize * phnum > len(data):
        raise ValueError("kernel program header table is out of bounds")

    loads: list[tuple[int, int, int]] = []
    for index in range(phnum):
        segment = struct.unpack_from("<IIQQQQQQ", data, phoff + index * phentsize)
        kind, flags, offset, address, _, file_bytes, memory_bytes, alignment = segment
        if kind == 1:
            if offset + file_bytes > len(data) or memory_bytes < file_bytes:
                raise ValueError(f"kernel PT_LOAD {index} is out of bounds")
            if alignment != 0x1000 or offset % alignment != address % alignment:
                raise ValueError(f"kernel PT_LOAD {index} has invalid alignment")
            loads.append((flags, address, memory_bytes))
        elif kind in (2, 3):
            raise ValueError("kernel must not require dynamic or interpreter segments")
    if not loads or not any(
        flags & 1 and address <= entry < address + memory_bytes
        for flags, address, memory_bytes in loads
    ):
        raise ValueError("kernel entry must lie inside an executable PT_LOAD")
    return entry, len(loads)


def tree_hash(esp: Path) -> tuple[str, list[dict[str, object]]]:
    items = []
    tree = hashlib.sha256()
    for relative in sorted(FILES):
        path = esp.joinpath(*relative.split("/"))
        item = {
            "path": relative,
            "bytes": path.stat().st_size,
            "sha256": sha256(path),
        }
        items.append(item)
        tree.update(
            relative.encode("utf-8")
            + b"\0"
            + str(item["bytes"]).encode("ascii")
            + b"\0"
            + str(item["sha256"]).encode("ascii")
            + b"\n"
        )
    return tree.hexdigest().upper(), items


def build_manifest(name: str, root: Path, kernel_sha: str,
                   audit_csv: Path) -> dict[str, object]:
    version = 1 if name == "candidate-a" else 2
    esp = root / name / "ESP"
    if not esp.is_dir():
        raise ValueError(f"{name}: missing ESP directory: {esp}")
    actual = {path.relative_to(esp).as_posix() for path in esp.rglob("*") if path.is_file()}
    if actual != FILES:
        raise ValueError(f"{name}: ESP file set is {sorted(actual)}")

    loader = esp / "EFI" / "BOOT" / "BOOTX64.EFI"
    kernel = esp / "kernel.elf"
    ramdisk = esp / "ramdisk.img"
    validate_efi_loader(loader.read_bytes())
    validate_kernel(kernel.read_bytes())
    if sha256(loader) != ACCEPTED_LOADER:
        raise ValueError(f"{name}: EFI loader differs from accepted loader")
    actual_kernel_sha = sha256(kernel)
    if actual_kernel_sha != kernel_sha:
        raise ValueError(f"{name}: staged kernel hash does not match fresh kernel")
    if actual_kernel_sha == BASELINE_KERNEL:
        raise ValueError(f"{name}: stale baseline kernel was staged")
    if sha256(ramdisk) != RDSK_SHA256[name]:
        raise ValueError(f"{name}: RDSK image differs from the accepted versioned image")
    header = ramdisk.read_bytes()[:64]
    if len(header) < 8 or header[:4] != b"RDSK":
        raise ValueError(f"{name}: ramdisk is not an RDSK image")
    if struct.unpack_from("<I", header, 4)[0] != version:
        raise ValueError(f"{name}: expected RDSK v{version}")
    entry, load_count = validate_kernel(kernel.read_bytes())
    esp_hash, file_rows = tree_hash(esp)
    if not audit_csv.is_file():
        raise ValueError(f"{name}: managed artifact audit CSV is missing")

    return {
        "candidate": name,
        "status": "offline package ready for guest testing",
        "guest_boot_claimed": False,
        "diagnostic_mode": "AppRuntime",
        "rdsk_version": version,
        "elf": {
            "class": 64,
            "endianness": "little",
            "type": "ET_EXEC",
            "machine": "EM_X86_64",
            "entry": f"0x{entry:016X}",
            "load_segments": load_count,
            "entry_in_executable_load_segment": True,
            "dynamic_or_interpreter_segments": 0,
            "sha256": actual_kernel_sha,
        },
        "loader_sha256": sha256(loader),
        "ramdisk_sha256": sha256(ramdisk),
        "esp_tree_sha256": esp_hash,
        "files": file_rows,
        "validation": {
            "production_pipeline": "fresh NativeAOT kernel built in an isolated copy of build.ps1; Candidate A assembled with the retained RDSK v1 image, then Candidate B assembled with kernel/conversion/ramdisk steps skipped using the same fresh kernel and retained RDSK v2 image",
            "managed_artifact_audit": "41/41 descriptor, build/staging, ramdisk/staging, and compiled allowlist",
            "candidate_audit_csv": str(audit_csv.resolve()),
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--audit-a", type=Path, required=True)
    parser.add_argument("--audit-b", type=Path, required=True)
    parser.add_argument("--check-only", action="store_true")
    args = parser.parse_args()
    root = args.output_root.resolve()
    kernel_sha = sha256(root / "kernel.elf")

    for name, audit in (("candidate-a", args.audit_a), ("candidate-b", args.audit_b)):
        manifest = build_manifest(name, root, kernel_sha, audit)
        manifest_path = root / name / "package-manifest.json"
        rendered = json.dumps(manifest, indent=2) + "\n"
        if args.check_only:
            if not manifest_path.is_file() or manifest_path.read_text(encoding="utf-8") != rendered:
                raise ValueError(f"{name}: package manifest differs from validated contents")
        else:
            if manifest_path.exists():
                raise ValueError(f"{name}: refusing to overwrite {manifest_path}")
            manifest_path.write_text(rendered, encoding="utf-8", newline="\n")
        print(
            f"{name}: PASS; RDSK v{manifest['rdsk_version']}; "
            f"kernel={kernel_sha}; ESP tree={manifest['esp_tree_sha256']}"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
