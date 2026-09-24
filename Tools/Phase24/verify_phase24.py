#!/usr/bin/env python3
"""Verify the Phase 24 source/descriptor contract without executing the PE."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import struct
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from inspect_managed_image import parse_pe  # noqa: E402


EXPECTED_SHA256 = "C8D60ABE6D91917F4E236F435A8C2D4272386CEC830C07AFA691897A23DA195A"
BASE = 0x0000401000000000
USER_LIMIT = 0x0000800000000000
MAGIC = 0x494D5847
HEADER = struct.Struct("<IIIIIIQIIIIIIIIIIIII32s")
SECTION = struct.Struct("<IIIIIBBH")
FIXED = 1
NO_RELOC = 2
X3 = 4
ENTRY_BLOCKED = 8


class Rejected(Exception):
    pass


def read_descriptor(data: bytes) -> dict:
    if len(data) < HEADER.size or len(data) != HEADER.size + struct.unpack_from("<I", data, 48)[0] * SECTION.size:
        raise Rejected("DESCRIPTOR_SIZE")
    values = HEADER.unpack_from(data)
    if values[0] != MAGIC:
        raise Rejected("DESCRIPTOR_MAGIC")
    sections = []
    offset = HEADER.size
    for _ in range(values[11]):
        sections.append(SECTION.unpack_from(data, offset))
        offset += SECTION.size
    return {
        "version": values[1], "target": values[2], "machine": values[3],
        "flags": values[4], "fileSize": values[5], "imageBase": values[6],
        "imageSize": values[7], "entryPointRva": values[8],
        "sectionAlignment": values[9], "fileAlignment": values[10],
        "sectionCount": values[11], "palSchema": values[12],
        "gsVectorOffset": values[13], "tlsCurrentThreadRva": values[14],
        "tlsIndexRva": values[15], "runtimeMetadataRva": values[16],
        "runtimeMetadataSize": values[17], "nativeBootstrapRva": values[18],
        "managedEntryRva": values[19], "sha256": values[20],
        "sections": sections,
    }


def validate(image: bytes, descriptor: dict) -> None:
    required = FIXED | NO_RELOC | X3 | ENTRY_BLOCKED
    if descriptor["version"] != 1 or descriptor["target"] != 0x47554944 or descriptor["machine"] != 0x8664:
        raise Rejected("IDENTITY")
    if descriptor["flags"] & required != required or descriptor["fileSize"] != len(image):
        raise Rejected("CONTRACT_FLAGS_OR_SIZE")
    if descriptor["imageBase"] != BASE or descriptor["sectionAlignment"] != 0x1000 or descriptor["palSchema"] != 1:
        raise Rejected("IMAGE_BASE_OR_ALIGNMENT")
    if descriptor["nativeBootstrapRva"] != 0 or descriptor["managedEntryRva"] != descriptor["entryPointRva"]:
        raise Rejected("BOOTSTRAP_POLICY")
    if descriptor["sha256"] != hashlib.sha256(image).digest():
        raise Rejected("ARTIFACT_HASH")
    parsed = parse_pe(image)
    if parsed["format"] != "PE32+" or parsed["machineName"] != "AMD64":
        raise Rejected("PE_IDENTITY")
    if int(parsed["imageBase"], 16) != descriptor["imageBase"] or int(parsed["imageSize"]) != descriptor["imageSize"]:
        raise Rejected("PE_DESCRIPTOR_MISMATCH")
    if parsed["imports"] or parsed["relocations"]["present"] or parsed["tls"]["present"]:
        raise Rejected("UNSUPPORTED_PE_DIRECTORY")
    if len(parsed["sections"]) != descriptor["sectionCount"]:
        raise Rejected("SECTION_COUNT")
    ranges = []
    entry_ok = False
    for pe, raw in zip(parsed["sections"], descriptor["sections"]):
        rva, virtual_size, raw_pointer, raw_size, memory_size, perms, _, _ = raw
        expected_perms = sum(bit for bit, letter in ((1, "R"), (2, "W"), (4, "X")) if letter in pe["permissions"])
        if (rva, virtual_size, raw_pointer, raw_size, memory_size, perms) != (
            pe["virtualAddress"], pe["virtualSize"], pe["rawPointer"], pe["rawSize"],
            (pe["virtualSize"] + 0xFFF) // 0x1000 * 0x1000, expected_perms):
            raise Rejected("SECTION_DESCRIPTOR_MISMATCH")
        if perms & 6 == 6:
            raise Rejected("RWX")
        end = rva + memory_size
        if end > descriptor["imageSize"] or BASE + end >= USER_LIMIT:
            raise Rejected("SECTION_RANGE")
        for start, other_end in ranges:
            if rva < other_end and start < end:
                raise Rejected("SECTION_OVERLAP")
        ranges.append((rva, end))
        if rva <= descriptor["entryPointRva"] < rva + virtual_size and perms & 4:
            entry_ok = True
    if not entry_ok:
        raise Rejected("ENTRYPOINT")
    if not (0 <= descriptor["runtimeMetadataRva"] < descriptor["imageSize"] and
            descriptor["runtimeMetadataSize"] <= descriptor["imageSize"] - descriptor["runtimeMetadataRva"] and
            0 < descriptor["tlsCurrentThreadRva"] < descriptor["imageSize"] and
            0 < descriptor["tlsIndexRva"] < descriptor["imageSize"]):
        raise Rejected("RUNTIME_METADATA")


def negative_matrix(image: bytes, descriptor_bytes: bytes, descriptor: dict) -> list[dict[str, str]]:
    cases = []
    fields = {
        "wrong descriptor version": (4, 2),
        "wrong target identity": (8, 0x57494E58),
        "wrong PAL schema": (52, 2),
        "invalid native bootstrap": (76, 0x1420),
        "invalid image base": (24, 0xFFFF800000000000),
    }
    for name, (offset, value) in fields.items():
        mutated = bytearray(descriptor_bytes)
        if offset == 24:
            struct.pack_into("<Q", mutated, offset, value)
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
    parsed = parse_pe(image)
    crt = next((item for item in parsed["sections"] if item["name"] == ".CRT"), None)
    result = {
        "result": "PASS",
        "artifact": str(args.image),
        "artifactSha256": hashlib.sha256(image).hexdigest().upper(),
        "exactPhase23Identity": hashlib.sha256(image).hexdigest().upper() == EXPECTED_SHA256,
        "descriptorVersion": descriptor["version"],
        "imageBase": f"0x{descriptor['imageBase']:016x}",
        "imageSize": descriptor["imageSize"],
        "imageRange": f"0x{descriptor['imageBase']:016x}-0x{descriptor['imageBase'] + descriptor['imageSize']:016x}",
        "entryPointRva": f"0x{descriptor['entryPointRva']:x}",
        "entryPoint": "wmain / managed NativeAOT entry (not executed)",
        "nativeBootstrapRva": descriptor["nativeBootstrapRva"],
        "managedEntryReady": False,
        "sections": [
            {"name": section["name"], "permissions": section["permissions"],
             "virtualSize": section["virtualSize"], "rawSize": section["rawSize"],
             "zeroFillBytes": section["zeroFillBytes"]}
            for section in parsed["sections"]
        ],
        "rwxSections": 0,
        "bssBytesZeroed": sum(section["zeroFillBytes"] for section in parsed["sections"]),
        "crt": {"present": crt is not None, "rawBytes": crt["rawSize"] if crt else 0,
                "executionPolicy": "mapped read-only, never invoked"},
        "tlsModel": "X3; GS+0x58 -> TLS vector; vector[_tls_index] -> user runtime state",
        "tlsCurrentThreadRva": f"0x{descriptor['tlsCurrentThreadRva']:x}",
        "tlsIndexRva": f"0x{descriptor['tlsIndexRva']:x}",
        "palSchema": descriptor["palSchema"],
        "directKernelImports": 0,
        "negativeCases": negative_matrix(image, descriptor_bytes, descriptor),
        "managedCodeExecuted": False,
        "kernelDiagnosticSelector": "UEFI_DIAGNOSTIC_RING3_PHASE24",
        "kernelBootstrapExecution": "BLOCKED: exact Phase 23 image has no native-only bootstrap RVA",
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
