#!/usr/bin/env python3
"""Verify the Phase 27 managed service payload without executing it."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from inspect_managed_image import parse_pe  # noqa: E402


BASE = 0x0000401000000000
USER_LIMIT = 0x0000800000000000
MAGIC = 0x494D5847
HEADER = struct.Struct("<IIIIIIQIIIIIIIIIIIII32s")
SECTION = struct.Struct("<IIIIIBBH")
FIXED = 1
NO_RELOC = 2
X3 = 4
ENTRY_BLOCKED = 8
PHASE26 = 16
PHASE27 = 32
PHASE27_FAILURE = 64
REQUEST_SIZE = 32
RESPONSE_SIZE = 128


class Rejected(Exception):
    pass


def read_descriptor(data: bytes) -> dict:
    if len(data) < HEADER.size:
        raise Rejected("DESCRIPTOR_SIZE")
    section_count = struct.unpack_from("<I", data, 48)[0]
    expected = HEADER.size + section_count * SECTION.size
    if len(data) != expected:
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


def permissions(text: str) -> int:
    return sum(bit for bit, letter in ((1, "R"), (2, "W"), (4, "X")) if letter in text)


def validate(image: bytes, descriptor: dict, failure: bool) -> dict:
    required = FIXED | NO_RELOC | X3 | ENTRY_BLOCKED | PHASE26 | PHASE27
    if failure:
        required |= PHASE27_FAILURE
    if descriptor["version"] != 1 or descriptor["target"] != 0x47554944 or descriptor["machine"] != 0x8664:
        raise Rejected("IDENTITY")
    if descriptor["flags"] & required != required:
        raise Rejected("CONTRACT_FLAGS")
    if bool(descriptor["flags"] & PHASE27_FAILURE) != failure:
        raise Rejected("FAILURE_MODE_FLAGS")
    if descriptor["fileSize"] != len(image) or descriptor["imageBase"] != BASE:
        raise Rejected("IMAGE_SIZE_OR_BASE")
    if descriptor["sectionAlignment"] != 0x1000 or descriptor["palSchema"] != 1:
        raise Rejected("ALIGNMENT_OR_PAL_SCHEMA")
    if descriptor["nativeBootstrapRva"] != 0 or descriptor["managedEntryRva"] != descriptor["entryPointRva"]:
        raise Rejected("BOOTSTRAP_POLICY")
    if descriptor["sha256"] != hashlib.sha256(image).digest():
        raise Rejected("ARTIFACT_HASH")

    parsed = parse_pe(image)
    if parsed["format"] != "PE32+" or parsed["machineName"] != "AMD64":
        raise Rejected("PE_IDENTITY")
    if int(parsed["imageBase"], 16) != BASE or int(parsed["imageSize"]) != descriptor["imageSize"]:
        raise Rejected("PE_DESCRIPTOR_MISMATCH")
    if int(parsed["entryPointRva"], 16) != descriptor["entryPointRva"]:
        raise Rejected("ENTRYPOINT_MISMATCH")
    if parsed["imports"] or parsed["relocations"]["present"] or parsed["tls"]["present"]:
        raise Rejected("UNSUPPORTED_PE_DIRECTORY")
    if len(parsed["sections"]) != descriptor["sectionCount"]:
        raise Rejected("SECTION_COUNT")

    ranges = []
    entry_ok = False
    rwx = 0
    for pe, raw in zip(parsed["sections"], descriptor["sections"]):
        rva, virtual_size, raw_pointer, raw_size, memory_size, perms, _, _ = raw
        expected = permissions(pe["permissions"])
        if (rva, virtual_size, raw_pointer, raw_size, memory_size, perms) != (
            pe["virtualAddress"], pe["virtualSize"], pe["rawPointer"], pe["rawSize"],
            (pe["virtualSize"] + 0xFFF) // 0x1000 * 0x1000, expected):
            raise Rejected("SECTION_DESCRIPTOR_MISMATCH")
        if perms & 6 == 6:
            rwx += 1
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
        raise Rejected("ENTRYPOINT_NOT_EXECUTABLE")
    if not (0 < descriptor["runtimeMetadataRva"] < descriptor["imageSize"] and
            descriptor["runtimeMetadataSize"] <= descriptor["imageSize"] - descriptor["runtimeMetadataRva"] and
            0 < descriptor["tlsCurrentThreadRva"] < descriptor["imageSize"] and
            0 < descriptor["tlsIndexRva"] < descriptor["imageSize"]):
        raise Rejected("RUNTIME_METADATA")
    return {
        "parsed": parsed,
        "rwxSections": rwx,
        "directKernelImports": len(parsed["imports"]),
    }


def verify_map(map_path: Path, link_response: Path) -> dict:
    map_text = map_path.read_text(errors="replace")
    symbol_pattern = re.compile(
        r"^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+guidexos_pal_service_request\s+([0-9A-Fa-f]{16})\s+f\s+",
        re.MULTILINE,
    )
    match = symbol_pattern.search(map_text)
    if not match:
        raise Rejected("PAL_SERVICE_SYMBOL_MISSING")
    helper_address = int(match.group(1), 16)
    if helper_address < BASE or helper_address - BASE >= 0x10000000:
        raise Rejected("PAL_SERVICE_SYMBOL_RANGE")
    response_text = link_response.read_text(errors="replace").lower()
    forbidden = [
        "kernel32.lib", "user32.lib", "advapi32.lib", "ole32.lib", "ntdll.lib",
        "ws2_32.lib", "kernel32.dll", "user32.dll", "advapi32.dll", "ole32.dll",
        "ntdll.dll", "ws2_32.dll",
    ]
    foreign = [name for name in forbidden if name in response_text]
    if foreign:
        raise Rejected("FOREIGN_LINK_INPUT:" + ",".join(foreign))
    return {
        "serviceHelperAddress": f"0x{helper_address:016x}",
        "serviceHelperRva": f"0x{helper_address - BASE:x}",
        "foreignLinkInputs": foreign,
    }


def negative_matrix(image: bytes, descriptor_bytes: bytes, descriptor: dict, failure: bool) -> list[dict[str, str]]:
    cases = []
    mutations = [("descriptor flags", 16, b"flags"), ("descriptor hash", 84, b"hash")]
    for name, offset, _ in mutations:
        mutated = bytearray(descriptor_bytes)
        if name == "descriptor flags":
            struct.pack_into("<I", mutated, offset, descriptor["flags"] & ~PHASE27)
        else:
            mutated[offset] ^= 0x01
        try:
            validate(image, read_descriptor(bytes(mutated)), failure)
        except Rejected as error:
            cases.append({"case": name, "result": "REJECTED", "reason": str(error)})
        else:
            raise AssertionError(name)
    mutated_image = bytearray(image)
    mutated_image[0x1000] ^= 0x01
    try:
        validate(bytes(mutated_image), descriptor, failure)
    except Rejected as error:
        cases.append({"case": "artifact byte", "result": "REJECTED", "reason": str(error)})
    else:
        raise AssertionError("artifact byte")
    return cases


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("descriptor", type=Path)
    parser.add_argument("map", type=Path)
    parser.add_argument("--link-response", type=Path, required=True)
    parser.add_argument("--failure", action="store_true")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    image = args.image.read_bytes()
    descriptor_bytes = args.descriptor.read_bytes()
    descriptor = read_descriptor(descriptor_bytes)
    evidence = validate(image, descriptor, args.failure)
    link = verify_map(args.map, args.link_response)
    negative = negative_matrix(image, descriptor_bytes, descriptor, args.failure)
    parsed = evidence["parsed"]
    result = {
        "result": "PASS",
        "mode": "failure" if args.failure else "success",
        "artifact": str(args.image),
        "artifactSha256": hashlib.sha256(image).hexdigest().upper(),
        "artifactSize": len(image),
        "descriptor": str(args.descriptor),
        "descriptorFlags": f"0x{descriptor['flags']:08x}",
        "imageBase": f"0x{descriptor['imageBase']:016x}",
        "imageSize": descriptor["imageSize"],
        "entryPointRva": f"0x{descriptor['entryPointRva']:x}",
        "sectionPermissions": {section["name"]: section["permissions"] for section in parsed["sections"]},
        "directKernelImports": evidence["directKernelImports"],
        "relocations": parsed["relocations"]["present"],
        "tlsDirectory": parsed["tls"]["present"],
        "rwxSections": evidence["rwxSections"],
        "pal": link,
        "abi": {"operation": 5, "requestSize": REQUEST_SIZE, "responseSize": RESPONSE_SIZE},
        "negativeCases": negative,
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
    raise SystemExit(main())
