#!/usr/bin/env python3
"""Audit managed proof identities and emit the kernel's compiled allowlist.

The GXMI descriptor is untrusted runtime input. This build tool checks it
against the finalized staged executable, then emits the same executable digest
as a compile-time kernel identity. Runtime admission remains strict.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import struct
import sys
from dataclasses import dataclass
from pathlib import Path


VARIANT_MASK = 0xFFFFF800
DESCRIPTOR_HEADER_SIZE = 116
DESCRIPTOR_DIGEST_OFFSET = 84
BASE_FLAGS = 0x1F


@dataclass(frozen=True)
class Payload:
    phase: str
    variant: str
    basename: str
    record_name: str
    build_mode: str
    proof_flags: int
    flag_name: str


PAYLOADS = (
    Payload("Phase29", "success", "guideXOS.Phase29ManagedNotificationProof", "phase29-managed-notification-build.json", "success", 1 << 11, "FlagPhase29Notification"),
    Payload("Phase29", "title-failure", "guideXOS.Phase29NotificationTitleFailureProof", "phase29-managed-notification-build.json", "title-failure", 1 << 12, "FlagPhase29TitleFailure"),
    Payload("Phase29", "body-failure", "guideXOS.Phase29NotificationBodyFailureProof", "phase29-managed-notification-build.json", "body-failure", 1 << 13, "FlagPhase29BodyFailure"),
    Payload("Phase29", "invalid-type", "guideXOS.Phase29NotificationInvalidTypeProof", "phase29-managed-notification-build.json", "invalid-type", 1 << 15, "FlagPhase29InvalidType"),
    Payload("Phase29", "failfast", "guideXOS.Phase29NotificationFailFastProof", "phase29-managed-notification-build.json", "failfast", 1 << 14, "FlagPhase29FailFast"),
    Payload("Phase30", "success", "guideXOS.Phase30ManagedClipboardProof", "phase30-managed-clipboard-build.json", "success", 1 << 16, "FlagPhase30Clipboard"),
    Payload("Phase30", "reader", "guideXOS.Phase30ClipboardReaderProof", "phase30-managed-clipboard-build.json", "reader", 1 << 17, "FlagPhase30Reader"),
    Payload("Phase30", "cross-writer", "guideXOS.Phase30ClipboardCrossWriterProof", "phase30-managed-clipboard-build.json", "cross-writer", 1 << 18, "FlagPhase30CrossWriter"),
    Payload("Phase30", "overwrite", "guideXOS.Phase30ClipboardOverwriteProof", "phase30-managed-clipboard-build.json", "overwrite", 1 << 19, "FlagPhase30Overwrite"),
    Payload("Phase30", "empty", "guideXOS.Phase30ClipboardEmptyProof", "phase30-managed-clipboard-build.json", "empty", 1 << 20, "FlagPhase30Empty"),
    Payload("Phase30", "clear", "guideXOS.Phase30ClipboardClearProof", "phase30-managed-clipboard-build.json", "clear", 1 << 21, "FlagPhase30Clear"),
    Payload("Phase30", "oversize", "guideXOS.Phase30ClipboardOversizeProof", "phase30-managed-clipboard-build.json", "oversize", 1 << 22, "FlagPhase30Oversize"),
    Payload("Phase30", "malformed-length", "guideXOS.Phase30ClipboardMalformedLengthProof", "phase30-managed-clipboard-build.json", "malformed-length", 1 << 23, "FlagPhase30MalformedLength"),
    Payload("Phase30", "failfast", "guideXOS.Phase30ClipboardFailFastProof", "phase30-managed-clipboard-build.json", "failfast", 1 << 24, "FlagPhase30FailFast"),
    Payload("Phase31", "success", "guideXOS.Phase31ManagedShellLaunchProof", "phase31-managed-shell-build.json", "success", 1 << 25, "FlagPhase31Launch"),
    Payload("Phase31", "invalid-target", "guideXOS.Phase31InvalidTargetProof", "phase31-managed-shell-build.json", "invalid-target", 1 << 26, "FlagPhase31InvalidTarget"),
    Payload("Phase31", "oversize", "guideXOS.Phase31OversizeTargetProof", "phase31-managed-shell-build.json", "oversize", 1 << 27, "FlagPhase31Oversize"),
    Payload("Phase31", "failfast", "guideXOS.Phase31FailFastProof", "phase31-managed-shell-build.json", "failfast", 1 << 28, "FlagPhase31FailFast"),
    Payload("Phase31", "stale-owner", "guideXOS.Phase31StaleOwnerProof", "phase31-managed-shell-build.json", "stale-owner", 1 << 29, "FlagPhase31StaleOwner"),
    Payload("Phase32", "success", "guideXOS.Phase32ManagedOpenDocumentProof", "phase32-managed-open-document-build.json", "success", 1 << 30, "FlagPhase32OpenDocument"),
    Payload("Phase32", "failfast", "guideXOS.Phase32OpenDocumentFailFastProof", "phase32-managed-open-document-build.json", "failfast", 1 << 31, "FlagPhase32FailFast"),
    Payload("Phase32", "stale-owner", "guideXOS.Phase32OpenDocumentStaleOwnerProof", "phase32-managed-open-document-build.json", "stale-owner", (1 << 30) | (1 << 31), "FlagPhase32StaleOwner"),
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def pe_timestamp_offsets(image: bytes, path: Path) -> tuple[int, list[int]]:
    if len(image) < 0x40:
        raise ValueError(f"PE is too short: {path}")
    pe_offset = struct.unpack_from("<I", image, 0x3C)[0]
    if pe_offset + 24 > len(image) or image[pe_offset : pe_offset + 4] != b"PE\0\0":
        raise ValueError(f"PE signature/header is invalid: {path}")
    section_count = struct.unpack_from("<H", image, pe_offset + 6)[0]
    optional_size = struct.unpack_from("<H", image, pe_offset + 20)[0]
    optional_offset = pe_offset + 24
    section_table = optional_offset + optional_size
    if section_table + section_count * 40 > len(image):
        raise ValueError(f"PE section table is truncated: {path}")
    if optional_size < 112 + 7 * 8 or struct.unpack_from("<H", image, optional_offset)[0] != 0x20B:
        raise ValueError(f"PE32+ debug data-directory entry is missing: {path}")

    debug_directory = optional_offset + 112 + 6 * 8
    debug_rva, debug_size = struct.unpack_from("<II", image, debug_directory)
    if debug_rva == 0 and debug_size == 0:
        return pe_offset + 8, []
    if debug_rva == 0 or debug_size < 28 or debug_size % 28:
        raise ValueError(f"PE debug directory is malformed: {path}")

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
        raise ValueError(f"PE debug directory is not section-backed: {path}")

    debug_timestamps = [debug_raw + index * 28 + 4 for index in range(debug_size // 28)]
    return pe_offset + 8, debug_timestamps


def read_legacy_kernel_hashes(source: Path) -> dict[str, str]:
    """Read the pre-H2c hand-maintained U32 allowlist for the before table."""
    text = source.read_text(encoding="utf-8-sig")
    found: dict[str, str] = {}
    pattern = re.compile(
        r"if\s*\(.*?ManagedImageContract\.(FlagPhase(?:29|30|31|32)\w+).*?\)\s*\{\s*return(.*?)\n\s*\}",
        re.S,
    )
    for match in pattern.finditer(text):
        words = [int(value, 16) for value in re.findall(r"U32\(hash,\s*\d+\)\s*==\s*0x([0-9A-Fa-f]+)U", match.group(2))]
        if len(words) == 8:
            found[match.group(1)] = b"".join(word.to_bytes(4, "little") for word in words).hex().upper()
    return found


def read_generated_kernel_hashes(source: Path) -> dict[int, str]:
    """Read generated identities so post-build audits verify the compiled input."""
    text = source.read_text(encoding="utf-8-sig")
    pattern = re.compile(
        r"// Phase(?:29|30|31|32) [\w-]+: ([0-9A-Fa-f]{64})\s+"
        r"case 0x([0-9A-Fa-f]{8})U:"
    )
    found: dict[int, str] = {}
    for digest, flags_text in pattern.findall(text):
        flags = int(flags_text, 16)
        if flags in found:
            raise ValueError(f"Generated kernel identities contain duplicate flags 0x{flags:08X}")
        found[flags] = digest.upper()
    expected = {payload.proof_flags for payload in PAYLOADS}
    if set(found) != expected:
        raise ValueError(
            f"Generated kernel identity source has {len(found)} variants; expected {len(expected)}"
        )
    return found


def read_build_payloads(root: Path) -> dict[tuple[str, str], dict]:
    result: dict[tuple[str, str], dict] = {}
    for payload in PAYLOADS:
        key = (payload.phase, payload.build_mode)
        if key in result:
            continue
        record = root / "out" / "dotnet" / {
            "Phase29": "phase29-managed-notification",
            "Phase30": "phase30-managed-clipboard",
            "Phase31": "phase31-managed-shell",
            "Phase32": "phase32-managed-open-document",
        }[payload.phase] / payload.record_name
        if not record.is_file():
            raise ValueError(f"Build record is missing: {record}")
        data = json.loads(record.read_text(encoding="utf-8-sig"))
        entries = data.get("payloads", [])
        item = next((entry for entry in entries if entry.get("name") == payload.build_mode), None)
        if item is None:
            raise ValueError(f"{payload.phase} build record has no mode {payload.build_mode!r}: {record}")
        result[key] = item
    return result


def collect_rows(root: Path, ramdisk_source: Path) -> list[dict[str, str]]:
    build_entries = read_build_payloads(root)
    rows: list[dict[str, str]] = []
    seen_flags: set[int] = set()
    for payload in PAYLOADS:
        exe_path = ramdisk_source / f"{payload.basename}.exe"
        gxmi_path = ramdisk_source / f"{payload.basename}.gxmi"
        if not exe_path.is_file() or not gxmi_path.is_file():
            raise ValueError(f"Staged executable/descriptor pair is incomplete: {exe_path}, {gxmi_path}")
        image = exe_path.read_bytes()
        descriptor = gxmi_path.read_bytes()
        if len(descriptor) < DESCRIPTOR_HEADER_SIZE:
            raise ValueError(f"GXMI descriptor is shorter than its header: {gxmi_path}")
        magic, version, target, machine, flags, file_size = struct.unpack_from("<IIIIII", descriptor, 0)
        section_count = struct.unpack_from("<I", descriptor, 48)[0]
        expected_descriptor_length = DESCRIPTOR_HEADER_SIZE + section_count * 24
        if magic != 0x494D5847 or version != 1 or target != 0x47554944 or machine != 0x8664:
            raise ValueError(f"GXMI contract header is invalid: {gxmi_path}")
        if len(descriptor) != expected_descriptor_length:
            raise ValueError(f"GXMI descriptor size does not match section table: {gxmi_path}")
        if file_size != len(image):
            raise ValueError(f"GXMI file size does not match executable: {gxmi_path}")
        if (flags & 0x1F) != BASE_FLAGS or (flags & VARIANT_MASK) != payload.proof_flags:
            raise ValueError(
                f"GXMI flags mismatch for {payload.basename}: got 0x{flags:08X}, "
                f"expected base 0x{BASE_FLAGS:08X} plus 0x{payload.proof_flags:08X}"
            )
        if payload.proof_flags in seen_flags:
            raise ValueError(f"Duplicate proof flag identity: 0x{payload.proof_flags:08X}")
        seen_flags.add(payload.proof_flags)

        timestamp_offset, debug_timestamp_offsets = pe_timestamp_offsets(image, exe_path)
        timestamp = struct.unpack_from("<I", image, timestamp_offset)[0]
        debug_timestamps = [
            struct.unpack_from("<I", image, offset)[0]
            for offset in debug_timestamp_offsets
        ]
        normalized_image = bytearray(image)
        normalized_image[timestamp_offset : timestamp_offset + 4] = b"\0\0\0\0"
        for offset in debug_timestamp_offsets:
            normalized_image[offset : offset + 4] = b"\0\0\0\0"
        raw_digest = sha256(image)
        normalized_digest = sha256(normalized_image)
        descriptor_digest = descriptor[DESCRIPTOR_DIGEST_OFFSET : DESCRIPTOR_DIGEST_OFFSET + 32]
        if len(descriptor_digest) != 32 or descriptor_digest.hex().upper() != raw_digest:
            raise ValueError(f"GXMI artifact SHA does not equal staged executable SHA: {exe_path}")
        if timestamp != 0 or any(debug_timestamps) or raw_digest != normalized_digest:
            raise ValueError(
                f"PE COFF/debug timestamps were not normalized before descriptor/hash generation: {exe_path}"
            )

        build_entry = build_entries[(payload.phase, payload.build_mode)]
        build_path = Path(build_entry["path"])
        if not build_path.is_file():
            raise ValueError(f"Final managed build output is missing: {build_path}")
        build_image = build_path.read_bytes()
        build_digest = sha256(build_image)
        recorded_digest = str(build_entry.get("sha256", "")).upper()
        recorded_size = int(build_entry.get("length", -1))
        if build_image != image:
            raise ValueError(f"Staged executable differs byte-for-byte from final build output: {exe_path}")
        if recorded_digest != build_digest or recorded_size != len(build_image):
            raise ValueError(f"Build record is stale for final executable: {build_path}")

        rows.append(
            {
                "phase": payload.phase,
                "variant": payload.variant,
                "executable_path": str(exe_path),
                "build_output_path": str(build_path),
                "executable_bytes": str(len(image)),
                "raw_sha256": raw_digest,
                "normalized_sha256": normalized_digest,
                "descriptor_path": str(gxmi_path),
                "descriptor_file_sha256": sha256(descriptor),
                "descriptor_artifact_sha256": descriptor_digest.hex().upper(),
                "descriptor_flags": f"0x{flags:08X}",
                "kernel_accepted_sha256": "",
                "build_equals_staged": "true",
                "descriptor_equals_executable": "true",
                "allowlist_equals_executable": "",
                "ramdisk_equals_source": "",
                "coff_timestamp": str(timestamp),
                "coff_timestamp_offset": f"0x{timestamp_offset:X}",
                "debug_directory_timestamp_offsets": ";".join(
                    f"0x{offset:X}" for offset in debug_timestamp_offsets
                ),
                "debug_directory_timestamps": ";".join(
                    f"0x{value:08X}" for value in debug_timestamps
                ),
                "normalized_fields": "PE COFF TimeDateStamp; IMAGE_DEBUG_DIRECTORY TimeDateStamp",
                "classification": "compiled allowlist stale" if raw_digest else "",
            }
        )
    if len(rows) != 22 or len(seen_flags) != 22:
        raise ValueError(f"Expected 22 unique managed proof identities; found {len(rows)}")
    return rows


def verify_ramdisk_image(image_path: Path, ramdisk_source: Path, rows: list[dict[str, str]]) -> None:
    data = image_path.read_bytes()
    if len(data) < 12 or data[:4] != b"RDSK" or struct.unpack_from("<I", data, 4)[0] != 1:
        raise ValueError(f"Ramdisk header is invalid: {image_path}")
    count = struct.unpack_from("<I", data, 8)[0]
    offset = 12
    entries: dict[str, bytes] = {}
    for _ in range(count):
        if offset + 2 > len(data):
            raise ValueError(f"Ramdisk entry path length is truncated: {image_path}")
        path_length = struct.unpack_from("<H", data, offset)[0]
        offset += 2
        if offset + path_length + 4 > len(data):
            raise ValueError(f"Ramdisk entry header is truncated: {image_path}")
        entry_path = data[offset : offset + path_length].decode("utf-8")
        offset += path_length
        size = struct.unpack_from("<I", data, offset)[0]
        offset += 4
        if offset + size > len(data):
            raise ValueError(f"Ramdisk entry data is truncated: {entry_path}")
        if entry_path in entries:
            raise ValueError(f"Ramdisk contains duplicate paths: {entry_path}")
        entries[entry_path] = data[offset : offset + size]
        offset += size
    if offset != len(data):
        raise ValueError(f"Ramdisk has trailing/unaccounted bytes: {image_path}")

    for row in rows:
        filename = Path(row["executable_path"]).name
        gxmi_name = Path(row["descriptor_path"]).name
        expected_exe = (ramdisk_source / filename).read_bytes()
        expected_gxmi = (ramdisk_source / gxmi_name).read_bytes()
        archive_exe = entries.get(f"Native/{filename}")
        archive_gxmi = entries.get(f"Native/{gxmi_name}")
        if archive_exe != expected_exe:
            raise ValueError(f"Ramdisk executable differs from staged file: Native/{filename}")
        if archive_gxmi != expected_gxmi:
            raise ValueError(f"Ramdisk descriptor differs from staged file: Native/{gxmi_name}")
        row["ramdisk_equals_source"] = "true"


def emit_csharp(rows: list[dict[str, str]], path: Path) -> None:
    case_rows: list[str] = []
    for payload, row in zip(PAYLOADS, rows):
        digest = bytes.fromhex(row["raw_sha256"])
        words = [int.from_bytes(digest[index : index + 4], "little") for index in range(0, 32, 4)]
        comparisons = " &&\n                ".join(
            f"ReadU32(hash, {index * 4}) == 0x{word:08X}U"
            for index, word in enumerate(words)
        )
        case_rows.append(
            f"            // {payload.phase} {payload.variant}: {row['raw_sha256']}\n"
            f"            case 0x{payload.proof_flags:08X}U:\n"
            f"                return {comparisons};"
        )
    content = """// Generated by Tools/ManagedArtifacts/managed_artifacts.py. Do not edit.
using System;

namespace guideXOS.Misc {
    internal static class ManagedArtifactIdentities {
        private const uint VariantMask = 0xFFFFF800U;

        internal static bool Matches(byte[] hash, uint descriptorFlags) {
            if (hash == null || hash.Length != 32) return false;
            switch (descriptorFlags & VariantMask) {
""" + "\n".join(case_rows) + """
                default:
                    return false;
            }
        }

        private static uint ReadU32(byte[] value, int offset) {
            return (uint)value[offset] |
                ((uint)value[offset + 1] << 8) |
                ((uint)value[offset + 2] << 16) |
                ((uint)value[offset + 3] << 24);
        }
    }
}
"""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def write_csv(
    rows: list[dict[str, str]],
    path: Path,
    stage: str,
    accepted: dict[str, str],
    kernel_built: bool,
) -> None:
    for payload, row in zip(PAYLOADS, rows):
        old = accepted.get(payload.flag_name, "")
        row["stage"] = stage
        if stage == "before":
            row["kernel_accepted_sha256"] = old
        elif kernel_built:
            row["kernel_accepted_sha256"] = old
        else:
            row["kernel_accepted_sha256"] = ""
        row["allowlist_equals_executable"] = (
            str(row["kernel_accepted_sha256"] == row["raw_sha256"]).lower()
            if row["kernel_accepted_sha256"]
            else ""
        )
        if stage == "before":
            row["classification"] = (
                "compiled allowlist stale" if row["kernel_accepted_sha256"] != row["raw_sha256"] else "match"
            )
        elif not kernel_built:
            row["classification"] = "kernel not built"
        else:
            row["classification"] = "match"
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = ["stage"] + [key for key in rows[0] if key != "stage"]
    with path.open("w", encoding="utf-8", newline="") as output:
        writer = csv.DictWriter(output, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--ramdisk-source", type=Path)
    parser.add_argument("--identity-source", type=Path)
    parser.add_argument("--report-csv", type=Path)
    parser.add_argument("--kernel-source", type=Path)
    parser.add_argument("--ramdisk-image", type=Path)
    parser.add_argument("--stage", choices=("before", "generated", "after"), default="after")
    parser.add_argument("--kernel-built", action="store_true")
    parser.add_argument("--audit-only", action="store_true")
    args = parser.parse_args()

    root = args.root.resolve()
    ramdisk_source = args.ramdisk_source or root / "ramdisk_src" / "Native"
    rows = collect_rows(root, ramdisk_source.resolve())
    if args.ramdisk_image:
        verify_ramdisk_image(args.ramdisk_image.resolve(), ramdisk_source.resolve(), rows)
    accepted: dict[str, str] = {}
    if args.kernel_source:
        accepted = read_legacy_kernel_hashes(args.kernel_source)
        if args.stage == "before" and len(accepted) != 22:
            raise ValueError(f"Expected 22 existing hard-coded identities, parsed {len(accepted)}")
    generated: dict[int, str] = {}
    if args.identity_source:
        if args.audit_only:
            generated = read_generated_kernel_hashes(args.identity_source)
        else:
            emit_csharp(rows, args.identity_source)
    if args.kernel_built and not generated:
        raise ValueError("A post-kernel audit requires --audit-only and the generated --identity-source")
    if generated:
        accepted = {
            payload.flag_name: generated[payload.proof_flags]
            for payload in PAYLOADS
        }
        for payload, row in zip(PAYLOADS, rows):
            row["kernel_accepted_sha256"] = generated[payload.proof_flags]
            row["allowlist_equals_executable"] = str(
                generated[payload.proof_flags] == row["raw_sha256"]
            ).lower()
        if args.kernel_built and any(row["allowlist_equals_executable"] != "true" for row in rows):
            raise ValueError("Generated compiled-input identities do not match all 22 final executables")
    if args.report_csv:
        write_csv(rows, args.report_csv, args.stage, accepted, args.kernel_built)
    matches = sum(
        row["raw_sha256"] == accepted.get(payload.flag_name)
        for payload, row in zip(PAYLOADS, rows)
    )
    print(f"MANAGED_ARTIFACTS={len(rows)}")
    print(f"DESCRIPTOR_AGREEMENT={sum(row['descriptor_equals_executable'] == 'true' for row in rows)}/{len(rows)}")
    print(f"BUILD_STAGING_AGREEMENT={sum(row['build_equals_staged'] == 'true' for row in rows)}/{len(rows)}")
    if args.ramdisk_image:
        print(f"RAMDISK_STAGING_AGREEMENT={sum(row['ramdisk_equals_source'] == 'true' for row in rows)}/{len(rows)}")
    if args.stage == "before" and accepted:
        print(f"COMPILED_ALLOWLIST_AGREEMENT={matches}/{len(rows)}")
    elif args.kernel_built:
        final_matches = sum(row["allowlist_equals_executable"] == "true" for row in rows)
        print(f"COMPILED_ALLOWLIST_AGREEMENT={final_matches}/{len(rows)}")
    if args.identity_source and not args.audit_only:
        print(f"GENERATED_IDENTITY_SOURCE={args.identity_source}")
    if args.report_csv:
        print(f"AUDIT_CSV={args.report_csv}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as error:
        print(f"managed artifact identity error: {error}", file=sys.stderr)
        raise SystemExit(1)
