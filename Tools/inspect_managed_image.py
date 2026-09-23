#!/usr/bin/env python3
"""Inspect a Windows AMD64 NativeAOT PE without executing it.

The parser is deliberately dependency-free so the artifact inspection remains
reproducible on the guideXOS build host. It records PE sections, data
directories, imports, base relocations, TLS, unwind metadata, and map symbols.
It does not attempt to load or execute the image.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any


IMAGE_FILE_MACHINE_AMD64 = 0x8664
IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20B
IMAGE_DIRECTORY_ENTRY_EXPORT = 0
IMAGE_DIRECTORY_ENTRY_IMPORT = 1
IMAGE_DIRECTORY_ENTRY_EXCEPTION = 3
IMAGE_DIRECTORY_ENTRY_BASERELOC = 5
IMAGE_DIRECTORY_ENTRY_DEBUG = 6
IMAGE_DIRECTORY_ENTRY_TLS = 9
IMAGE_DIRECTORY_ENTRY_LOAD_CONFIG = 10
IMAGE_NUMBEROF_DIRECTORY_ENTRIES = 16
IMAGE_SCN_CNT_CODE = 0x00000020
IMAGE_SCN_CNT_INITIALIZED_DATA = 0x00000040
IMAGE_SCN_CNT_UNINITIALIZED_DATA = 0x00000080
IMAGE_SCN_MEM_EXECUTE = 0x20000000
IMAGE_SCN_MEM_READ = 0x40000000
IMAGE_SCN_MEM_WRITE = 0x80000000


def u16(data: bytes, offset: int) -> int:
    return struct.unpack_from("<H", data, offset)[0]


def u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def u64(data: bytes, offset: int) -> int:
    return struct.unpack_from("<Q", data, offset)[0]


def c_string(data: bytes, offset: int, limit: int = 4096) -> str:
    if offset < 0 or offset >= len(data):
        return ""
    end = data.find(b"\0", offset, min(len(data), offset + limit))
    if end < 0:
        end = min(len(data), offset + limit)
    return data[offset:end].decode("ascii", errors="replace")


def rva_to_file_offset(rva: int, sections: list[dict[str, Any]], headers_size: int) -> int | None:
    if rva < headers_size:
        return rva if rva < 0x100000 else None
    for section in sections:
        start = section["virtualAddress"]
        span = max(section["virtualSize"], section["rawSize"])
        if start <= rva < start + span:
            relative = rva - start
            if relative >= section["rawSize"]:
                return None
            return section["rawPointer"] + relative
    return None


def parse_pe(data: bytes) -> dict[str, Any]:
    if len(data) < 0x40 or data[:2] != b"MZ":
        raise ValueError("bad DOS magic")
    pe_offset = u32(data, 0x3C)
    if pe_offset + 24 > len(data) or data[pe_offset:pe_offset + 4] != b"PE\0\0":
        raise ValueError("bad PE signature")

    coff = pe_offset + 4
    machine = u16(data, coff)
    section_count = u16(data, coff + 2)
    optional_size = u16(data, coff + 16)
    optional = coff + 20
    magic = u16(data, optional)
    if magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC:
        raise ValueError(f"unsupported optional-header magic 0x{magic:x}")
    if optional + optional_size > len(data):
        raise ValueError("optional header outside file")

    entry_rva = u32(data, optional + 16)
    image_base = u64(data, optional + 24)
    section_alignment = u32(data, optional + 32)
    file_alignment = u32(data, optional + 36)
    image_size = u32(data, optional + 56)
    headers_size = u32(data, optional + 60)
    directory_count = min(u32(data, optional + 108), IMAGE_NUMBEROF_DIRECTORY_ENTRIES)
    directories: list[dict[str, Any]] = []
    directory_base = optional + 112
    for index in range(IMAGE_NUMBEROF_DIRECTORY_ENTRIES):
        rva = size = 0
        if index < directory_count and directory_base + index * 8 + 8 <= optional + optional_size:
            rva = u32(data, directory_base + index * 8)
            size = u32(data, directory_base + index * 8 + 4)
        directories.append({"index": index, "rva": rva, "size": size})

    section_base = optional + optional_size
    sections: list[dict[str, Any]] = []
    for index in range(section_count):
        offset = section_base + index * 40
        if offset + 40 > len(data):
            raise ValueError("section table outside file")
        name = data[offset:offset + 8].split(b"\0", 1)[0].decode("ascii", errors="replace")
        virtual_size = u32(data, offset + 8)
        virtual_address = u32(data, offset + 12)
        raw_size = u32(data, offset + 16)
        raw_pointer = u32(data, offset + 20)
        characteristics = u32(data, offset + 36)
        if raw_size and (raw_pointer + raw_size > len(data)):
            raise ValueError(f"section {name} raw range outside file")
        permissions = ""
        if characteristics & IMAGE_SCN_MEM_READ:
            permissions += "R"
        if characteristics & IMAGE_SCN_MEM_WRITE:
            permissions += "W"
        if characteristics & IMAGE_SCN_MEM_EXECUTE:
            permissions += "X"
        sections.append({
            "index": index,
            "name": name,
            "virtualAddress": virtual_address,
            "virtualSize": virtual_size,
            "rawPointer": raw_pointer,
            "rawSize": raw_size,
            "characteristics": f"0x{characteristics:08x}",
            "permissions": permissions,
            "containsCode": bool(characteristics & IMAGE_SCN_CNT_CODE),
            "initializedData": bool(characteristics & IMAGE_SCN_CNT_INITIALIZED_DATA),
            "uninitializedData": bool(characteristics & IMAGE_SCN_CNT_UNINITIALIZED_DATA),
            "zeroFillBytes": max(0, virtual_size - raw_size),
        })

    result: dict[str, Any] = {
        "format": "PE32+",
        "machine": f"0x{machine:04x}",
        "machineName": "AMD64" if machine == IMAGE_FILE_MACHINE_AMD64 else "unknown",
        "fileSize": len(data),
        "sha256": hashlib.sha256(data).hexdigest(),
        "imageBase": f"0x{image_base:016x}",
        "entryPointRva": f"0x{entry_rva:08x}",
        "entryPoint": f"0x{image_base + entry_rva:016x}",
        "sectionAlignment": section_alignment,
        "fileAlignment": file_alignment,
        "imageSize": image_size,
        "headersSize": headers_size,
        "sections": sections,
        "directories": directories,
        "imports": [],
        "relocations": {"present": False, "types": [], "blockCount": 0},
        "tls": {"present": False, "start": 0, "end": 0, "index": 0, "callbacks": 0},
        "runtimeEvidence": {
            "r2rOrManagedSections": [s["name"] for s in sections if s["name"].lower() in {".managed", ".modules", ".rgctx", ".didat"}],
            "unwindSection": next((s["name"] for s in sections if s["name"].lower() == ".pdata"), None),
            "writableStaticsSections": [s["name"] for s in sections if "W" in s["permissions"]],
            "bssSections": [s["name"] for s in sections if s["zeroFillBytes"] > 0 or s["uninitializedData"]],
        },
    }

    def directory(index: int) -> tuple[int, int]:
        item = directories[index]
        return item["rva"], item["size"]

    import_rva, import_size = directory(IMAGE_DIRECTORY_ENTRY_IMPORT)
    import_offset = rva_to_file_offset(import_rva, sections, headers_size) if import_rva else None
    if import_offset is not None and import_size:
        cursor = import_offset
        end = min(len(data), import_offset + import_size)
        while cursor + 20 <= end:
            original_first_thunk = u32(data, cursor)
            name_rva = u32(data, cursor + 12)
            first_thunk = u32(data, cursor + 16)
            if original_first_thunk == name_rva == first_thunk == 0:
                break
            name_offset = rva_to_file_offset(name_rva, sections, headers_size)
            dll_name = c_string(data, name_offset) if name_offset is not None else "<invalid>"
            thunk_rva = original_first_thunk or first_thunk
            thunk_offset = rva_to_file_offset(thunk_rva, sections, headers_size)
            names: list[str] = []
            if thunk_offset is not None:
                for item_index in range(4096):
                    item_offset = thunk_offset + item_index * 8
                    if item_offset + 8 > len(data):
                        break
                    value = u64(data, item_offset)
                    if value == 0:
                        break
                    if value & (1 << 63):
                        names.append(f"ordinal:{value & 0xffff}")
                    else:
                        hint_name_offset = rva_to_file_offset(value & 0x7fff_ffff_ffff_ffff, sections, headers_size)
                        names.append(c_string(data, (hint_name_offset or 0) + 2) if hint_name_offset is not None else "<invalid>")
            result["imports"].append({"dll": dll_name, "names": names})
            cursor += 20

    reloc_rva, reloc_size = directory(IMAGE_DIRECTORY_ENTRY_BASERELOC)
    reloc_offset = rva_to_file_offset(reloc_rva, sections, headers_size) if reloc_rva else None
    reloc_types: set[int] = set()
    block_count = 0
    if reloc_offset is not None and reloc_size:
        cursor = reloc_offset
        end = min(len(data), reloc_offset + reloc_size)
        while cursor + 8 <= end:
            page_rva = u32(data, cursor)
            block_size = u32(data, cursor + 4)
            if block_size < 8 or cursor + block_size > end:
                break
            block_count += 1
            for item_offset in range(cursor + 8, cursor + block_size, 2):
                item = u16(data, item_offset)
                reloc_types.add(item >> 12)
            cursor += block_size
    result["relocations"] = {
        "present": bool(reloc_size),
        "types": sorted(reloc_types),
        "blockCount": block_count,
        "directoryRva": f"0x{reloc_rva:08x}",
        "directorySize": reloc_size,
    }

    tls_rva, tls_size = directory(IMAGE_DIRECTORY_ENTRY_TLS)
    tls_offset = rva_to_file_offset(tls_rva, sections, headers_size) if tls_rva else None
    if tls_offset is not None and tls_size >= 40 and tls_offset + 40 <= len(data):
        start = u64(data, tls_offset)
        end = u64(data, tls_offset + 8)
        index_address = u64(data, tls_offset + 16)
        callbacks = u64(data, tls_offset + 24)
        callback_count = 0
        callback_offset = rva_to_file_offset(callbacks - image_base, sections, headers_size) if callbacks >= image_base else None
        if callback_offset is not None:
            for item_index in range(4096):
                value = u64(data, callback_offset + item_index * 8)
                if value == 0:
                    break
                callback_count += 1
        result["tls"] = {
            "present": True,
            "start": f"0x{start:016x}",
            "end": f"0x{end:016x}",
            "index": f"0x{index_address:016x}",
            "callbacks": callback_count,
        }

    result["directoriesSummary"] = {
        "export": directory(IMAGE_DIRECTORY_ENTRY_EXPORT),
        "import": directory(IMAGE_DIRECTORY_ENTRY_IMPORT),
        "exception": directory(IMAGE_DIRECTORY_ENTRY_EXCEPTION),
        "relocation": directory(IMAGE_DIRECTORY_ENTRY_BASERELOC),
        "debug": directory(IMAGE_DIRECTORY_ENTRY_DEBUG),
        "tls": directory(IMAGE_DIRECTORY_ENTRY_TLS),
        "loadConfig": directory(IMAGE_DIRECTORY_ENTRY_LOAD_CONFIG),
    }
    return result


def classify_symbol(name: str) -> str:
    lowered = name.lower()
    if any(token in lowered for token in ("tls", "threadstatic", "fls", "threadlocal")):
        return "tls helper"
    if any(token in lowered for token in ("newfast", "newarr", "alloc", "writebarrier")):
        return "allocation helper"
    if any(token in lowered for token in ("gc", "ephemeral", "finalizer", "suspend")):
        return "GC helper"
    if any(token in lowered for token in ("throw", "exception", "unwind", "eh")):
        return "exception helper"
    if any(token in lowered for token in ("startup", "module", "cctor", "classconstructor", "typemanager")):
        return "startup/module helper"
    if name.startswith("Rhp") or name.startswith("Rh" ):
        return "internal NativeAOT runtime code"
    if name.startswith("__") or name.startswith("R"):
        return "compiler-generated helper"
    return "generated native symbol"


def parse_length(value: str) -> int:
    try:
        return int(value, 0)
    except ValueError:
        return 0


def read_map_symbols(path: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    if not path.exists():
        return [], {}
    if path.suffix.lower() == ".xml":
        root = ET.parse(path).getroot()
        symbols: list[dict[str, Any]] = []
        kind_counts: dict[str, int] = {}
        total_bytes = 0
        for element in root:
            name = element.attrib.get("Name", "")
            if not name:
                continue
            length = parse_length(element.attrib.get("Length", "0"))
            kind = element.tag
            kind_counts[kind] = kind_counts.get(kind, 0) + 1
            total_bytes += length
            symbols.append({
                "kind": kind,
                "name": name,
                "length": length,
                "hash": element.attrib.get("Hash", ""),
                "classification": classify_symbol(name),
            })
        evidence = {
            "format": "NativeAOT map.xml",
            "entryCount": len(symbols),
            "totalBytes": total_bytes,
            "kindCounts": kind_counts,
            "moduleSections": [item for item in symbols if item["kind"] in {"ModulesSection", "TypeManagerIndirection", "Metadata"}],
            "tlsSections": [item for item in symbols if item["kind"] in {"TlsRoot", "TypeThreadStaticIndex"} or (item["kind"] == "ArrayOfEmbeddedData" and "ThreadStatic" in item["name"])],
            "gcSections": [item for item in symbols if item["kind"] in {"GCStatics", "ArrayOfEmbeddedPointers", "ArrayOfFrozenObjects"}],
            "writableStaticSections": [item for item in symbols if item["kind"] in {"WritableData", "NonGCStatics"}],
            "readyToRunHelpers": [item for item in symbols if item["kind"] == "ReadyToRunHelper"],
        }
        return symbols, evidence
    symbols: list[dict[str, str]] = []
    # MSVC link maps use section:offset, public name, and RVA+base columns.
    # Keep the simple-address form as a fallback for older hand-written maps.
    map_public = re.compile(
        r"^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(\S+)\s+([0-9A-Fa-f]{16})\s+(.*)$"
    )
    address_name = re.compile(r"^\s*([0-9A-Fa-f]{8,16})\s+(.+?)\s*$")
    for raw_line in path.read_text(errors="replace").splitlines():
        structured = map_public.match(raw_line)
        if structured:
            name = structured.group(1).strip()
            if name and not name.startswith("Absolute") and not name.startswith("entry point"):
                symbols.append({
                    "address": "0x" + structured.group(2).lower(),
                    "name": name,
                    "classification": classify_symbol(name),
                })
            continue
        match = address_name.match(raw_line)
        if not match:
            continue
        name = match.group(2).strip()
        if not name or name.startswith("Absolute") or name.startswith("entry point"):
            continue
        symbols.append({
            "address": "0x" + match.group(1).lower(),
            "name": name,
            "classification": classify_symbol(name),
        })
    return symbols, {
        "format": "MSVC linker map",
        "entryCount": len(symbols),
        "tlsSections": [item for item in symbols if "tls" in item["name"].lower() or "threadstatic" in item["name"].lower()],
        "gcSections": [item for item in symbols if any(token in item["name"].lower() for token in ("gcstatic", "frozenobject", "gcheap", "gcroot"))],
        "writableStaticSections": [item for item in symbols if any(token in item["name"].lower() for token in ("writabledata", "nongcstatic", "threadstatic"))],
        "readyToRunHelpers": [item for item in symbols if "readytorun" in item["name"].lower()],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("--map", dest="map_path", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--toolchain", default="Microsoft.DotNet.ILCompiler 9.0.0")
    args = parser.parse_args()

    data = args.image.read_bytes()
    report = parse_pe(data)
    report["toolchain"] = args.toolchain
    report["mapFile"] = str(args.map_path) if args.map_path else None
    helper_manifest, map_evidence = read_map_symbols(args.map_path) if args.map_path else ([], {})
    report["helperManifest"] = helper_manifest
    report["mapEvidence"] = map_evidence
    report["imageContract"] = {
        "format": "PE32+ section contract; loader descriptor is fixed-width and bounded",
        "preferredBase": f"0x{int(report['imageBase'], 16):016x}",
        "entryPointRva": report["entryPointRva"],
        "segmentCount": len(report["sections"]),
        "segments": [
            {
                "name": section["name"],
                "virtualOffset": f"0x{section['virtualAddress']:08x}",
                "fileOffset": f"0x{section['rawPointer']:08x}",
                "fileSize": section["rawSize"],
                "memorySize": (section["virtualSize"] + report["sectionAlignment"] - 1) // report["sectionAlignment"] * report["sectionAlignment"],
                "permissions": section["permissions"],
                "alignment": report["sectionAlignment"],
            }
            for section in report["sections"]
        ],
    }
    report["runtimeRequirements"] = {
        "tls": "PRIVATE_TLS_FLS_REQUIRED" if report["tls"]["present"] or map_evidence.get("tlsSections") else "NO_TLS_EVIDENCE",
        "gc": "PRIVATE_GC_REQUIRED" if map_evidence.get("gcSections") else "GC_RUNTIME_NOT_IDENTIFIED",
        "exceptions": "UNWIND_METADATA_AND_EXCEPTION_RUNTIME_REFERENCED" if report["directoriesSummary"]["exception"][1] else "NO_EXCEPTION_EVIDENCE",
        "startup": "MODULE_METADATA_GC_STATICS_FROZEN_OBJECTS_READYTORUN" if map_evidence else "UNKNOWN",
    }
    report["policy"] = {
        "preferredBase": "0x0000401000000000",
        "fixedBase": True,
        "relocationsAccepted": False,
        "nativeKernelImportsAccepted": False,
        "rwxAccepted": False,
    }
    encoded = json.dumps(report, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.write_text(encoded, encoding="utf-8", newline="\n")
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
