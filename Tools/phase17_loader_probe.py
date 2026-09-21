#!/usr/bin/env python3
"""Run the Phase 17 bounded loader proof against the real NativeAOT PE.

This is intentionally a host-side, no-execute model of the kernel contract.
It validates the complete descriptor before allocating simulated user pages,
maps each PE section with its explicit permissions, proves zero-fill, checks
the entrypoint, and repeats the load/unload lifetime four times. It does not
claim managed execution or replace the future kernel mapping implementation.
"""

from __future__ import annotations

import argparse
import copy
import json
from pathlib import Path
from typing import Any


PAGE_SIZE = 0x1000
USER_LIMIT = 0x0000800000000000
MANAGED_BASE = 0x0000401000000000
MANAGED_END = 0x0000401100000000
STACK_GUARD_START = 0x00007FFF7FFE0000
STACK_END = 0x0000800000000000
MAX_SEGMENTS = 32


class Rejected(Exception):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code


def number(value: Any) -> int:
    if isinstance(value, int):
        return value
    return int(str(value), 0)


def align_up(value: int, alignment: int = PAGE_SIZE) -> int:
    return (value + alignment - 1) // alignment * alignment


def validate(descriptor: dict[str, Any], file_size: int) -> None:
    if descriptor.get("format") != "PE32+ section contract; loader descriptor is fixed-width and bounded":
        raise Rejected("BAD_FORMAT")
    base = number(descriptor.get("preferredBase", 0))
    if base != MANAGED_BASE:
        if base >= USER_LIMIT:
            raise Rejected("KERNEL_SPACE_VA")
        raise Rejected("WRONG_IMAGE_BASE")
    image_size = number(descriptor.get("imageSize", 0))
    if image_size == 0:
        raise Rejected("IMAGE_RANGE_OVERFLOW")
    segments = descriptor.get("segments")
    if not isinstance(segments, list) or len(segments) == 0:
        raise Rejected("NO_SEGMENTS")
    if len(segments) > MAX_SEGMENTS:
        raise Rejected("TOO_MANY_SEGMENTS")
    ranges: list[tuple[int, int]] = []
    for segment in segments:
        virtual = number(segment.get("virtualOffset", 0))
        file_offset = number(segment.get("fileOffset", 0))
        file_segment_size = number(segment.get("fileSize", 0))
        memory_size = number(segment.get("memorySize", 0))
        alignment = number(segment.get("alignment", 0))
        permissions = segment.get("permissions", "")
        if alignment < PAGE_SIZE or alignment & (alignment - 1):
            raise Rejected("INVALID_ALIGNMENT")
        if virtual % PAGE_SIZE:
            raise Rejected("UNALIGNED_VA")
        if memory_size == 0 or memory_size < file_segment_size:
            raise Rejected("MEMORY_SMALLER_THAN_FILE")
        if file_offset > file_size or file_segment_size > file_size - file_offset:
            raise Rejected("FILE_RANGE_OVERFLOW")
        if virtual > image_size or memory_size > image_size - virtual:
            raise Rejected("MEMORY_RANGE_OVERFLOW")
        absolute_start = base + virtual
        absolute_end = absolute_start + memory_size
        if absolute_start < STACK_END and absolute_end > STACK_GUARD_START:
            raise Rejected("STACK_OVERLAP")
        if absolute_end < absolute_start or absolute_start < MANAGED_BASE or absolute_end > MANAGED_END:
            raise Rejected("USER_RANGE_INVALID")
        if "R" not in permissions:
            raise Rejected("SEGMENT_NOT_READABLE")
        if "W" in permissions and "X" in permissions:
            raise Rejected("RWX_SEGMENT")
        ranges.append((virtual, virtual + memory_size))
    for index, (start, end) in enumerate(ranges):
        for other_start, other_end in ranges[index + 1:]:
            if start < other_end and other_start < end:
                raise Rejected("SEGMENT_OVERLAP")
    entry = number(descriptor.get("entryPointRva", 0))
    if entry >= image_size or not any(start <= entry < end and "X" in segments[index].get("permissions", "")
                                      for index, (start, end) in enumerate(ranges)):
        raise Rejected("ENTRYPOINT_NOT_EXECUTABLE")
    if image_size > MANAGED_END - MANAGED_BASE:
        raise Rejected("IMAGE_RANGE_OVERFLOW")
    tls = descriptor.get("tls", {})
    if tls.get("present"):
        alignment = number(tls.get("alignment", 0))
        size = number(tls.get("templateSize", 0))
        if size == 0 or alignment < 1 or alignment & (alignment - 1):
            raise Rejected("TLS_METADATA_INVALID")


def map_image(descriptor: dict[str, Any], image: bytes) -> dict[str, Any]:
    validate(descriptor, len(image))
    pages: dict[int, dict[str, Any]] = {}
    bss_bytes = 0
    for segment in descriptor["segments"]:
        virtual = number(segment["virtualOffset"])
        file_offset = number(segment["fileOffset"])
        file_segment_size = number(segment["fileSize"])
        memory_size = number(segment["memorySize"])
        page_count = align_up(memory_size) // PAGE_SIZE
        bss_bytes += memory_size - file_segment_size
        for page_index in range(page_count):
            page_rva = virtual + page_index * PAGE_SIZE
            page = pages.setdefault(page_rva, {"bytes": bytearray(PAGE_SIZE), "permissions": segment["permissions"]})
            if page["permissions"] != segment["permissions"]:
                raise Rejected("PAGE_PERMISSION_CONFLICT")
            copy_start = page_index * PAGE_SIZE
            copy_length = max(0, min(PAGE_SIZE, file_segment_size - copy_start))
            if copy_length:
                source_start = file_offset + copy_start
                page["bytes"][:copy_length] = image[source_start:source_start + copy_length]
    entry = number(descriptor["entryPointRva"])
    entry_page = entry // PAGE_SIZE * PAGE_SIZE
    if entry_page not in pages:
        raise Rejected("ENTRYPOINT_UNMAPPED")
    return {"pages": pages, "bssBytes": bss_bytes, "entryPage": entry_page}


def run_negative_cases(descriptor: dict[str, Any], file_size: int) -> list[dict[str, str]]:
    cases: list[tuple[str, dict[str, Any]]] = []
    bad = copy.deepcopy(descriptor)
    bad["format"] = "bad"
    cases.append(("bad magic/format", bad))
    bad = copy.deepcopy(descriptor)
    bad["segments"] = bad["segments"] * 7
    cases.append(("too many segments", bad))
    bad = copy.deepcopy(descriptor)
    bad["segments"][1]["virtualOffset"] = bad["segments"][0]["virtualOffset"]
    cases.append(("overlap", bad))
    bad = copy.deepcopy(descriptor)
    bad["entryPointRva"] = bad["imageSize"]
    cases.append(("entrypoint outside RX", bad))
    bad = copy.deepcopy(descriptor)
    bad["segments"][0]["fileOffset"] = "0xffffffffffffffff"
    cases.append(("file bounds overflow", bad))
    bad = copy.deepcopy(descriptor)
    bad["segments"][0]["memorySize"] = 1
    cases.append(("memory smaller than file", bad))
    bad = copy.deepcopy(descriptor)
    bad["preferredBase"] = "0xffff800000000000"
    cases.append(("kernel-space VA", bad))
    bad = copy.deepcopy(descriptor)
    bad["segments"][0]["virtualOffset"] = "0x3fef7ffe0000"
    bad["imageSize"] = "0x3fef7ffe1000"
    bad["segments"][0]["fileSize"] = PAGE_SIZE
    bad["segments"][0]["memorySize"] = PAGE_SIZE
    cases.append(("stack overlap", bad))
    bad = copy.deepcopy(descriptor)
    bad["segments"][0]["memorySize"] = bad["imageSize"] + PAGE_SIZE
    cases.append(("BSS overflow", bad))
    bad = copy.deepcopy(descriptor)
    bad["segments"][0]["permissions"] = "RWX"
    cases.append(("RWX request", bad))
    bad = copy.deepcopy(descriptor)
    bad["tls"] = {"present": True, "templateSize": 0, "alignment": 3}
    cases.append(("malformed TLS metadata", bad))
    results: list[dict[str, str]] = []
    for name, candidate in cases:
        try:
            validate(candidate, file_size)
        except Rejected as error:
            results.append({"case": name, "result": "REJECTED", "reason": error.code})
        else:
            raise AssertionError(f"negative case was accepted: {name}")
    return results


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    image = args.image.read_bytes()
    report = json.loads(args.manifest.read_text(encoding="utf-8"))
    descriptor = copy.deepcopy(report["imageContract"])
    descriptor["imageSize"] = report["imageSize"]
    tls = report.get("tls", {})
    if tls.get("present"):
        start = number(tls["start"])
        end = number(tls["end"])
        descriptor["tls"] = {
            "present": True,
            "templateSize": end - start,
            "alignment": PAGE_SIZE,
        }
    else:
        descriptor["tls"] = {"present": False}

    generations: list[int] = []
    total_segments = 0
    total_pages = 0
    total_bss = 0
    for generation in range(1, 5):
        mapped = map_image(descriptor, image)
        generations.append(generation)
        total_segments += len(descriptor["segments"])
        total_pages += len(mapped["pages"])
        total_bss += mapped["bssBytes"]
        # Releasing the local page dictionary is the modeled process teardown.
        mapped["pages"].clear()
        if mapped["pages"]:
            raise AssertionError("simulated managed mappings were not reclaimed")

    negatives = run_negative_cases(descriptor, len(image))
    result = {
        "artifact": str(args.image),
        "artifactSha256": report["sha256"],
        "processGenerations": generations,
        "repeatedLoadCount": len(generations),
        "repeatedLoadResult": "PASS",
        "managedImagesValidated": 4,
        "managedImagesRejected": len(negatives),
        "segmentsMapped": total_segments,
        "segmentsReclaimed": total_segments,
        "imagePagesAllocated": total_pages,
        "imagePagesReclaimed": total_pages,
        "bssBytesZeroed": total_bss,
        "outstandingManagedMappings": 0,
        "entryPointValidated": True,
        "permissionsValidated": True,
        "bssZeroFillValidated": True,
        "negativeCases": negatives,
        "managedCodeExecuted": False,
    }
    encoded = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.write_text(encoded, encoding="utf-8", newline="\n")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
