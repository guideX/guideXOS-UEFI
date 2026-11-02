#!/usr/bin/env python3
"""Validate the structural properties required by the standalone xHCI EFI app."""

import argparse
import hashlib
import json
import struct
from pathlib import Path


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def u16(data: bytes, offset: int) -> int:
    return struct.unpack_from("<H", data, offset)[0]


def u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def verify(path: Path) -> dict[str, object]:
    data = path.read_bytes()
    require(len(data) >= 0x40, "file is too small for a DOS header")
    require(data[:2] == b"MZ", "missing DOS MZ signature")
    pe_offset = u32(data, 0x3C)
    require(pe_offset + 24 <= len(data), "PE header is outside the file")
    require(data[pe_offset : pe_offset + 4] == b"PE\0\0", "missing PE signature")

    coff = pe_offset + 4
    machine = u16(data, coff)
    section_count = u16(data, coff + 2)
    optional_size = u16(data, coff + 16)
    characteristics = u16(data, coff + 18)
    optional = coff + 20
    require(optional + optional_size <= len(data), "truncated optional header")
    require(u16(data, optional) == 0x20B, "expected PE32+ optional header")
    entry_rva = u32(data, optional + 16)
    subsystem = u16(data, optional + 68)
    require(optional_size >= 120, "optional header lacks import-directory fields")
    import_rva = u32(data, optional + 112 + 8)
    import_size = u32(data, optional + 112 + 12)
    security_rva = u32(data, optional + 112 + 4 * 8)
    security_size = u32(data, optional + 112 + 4 * 8 + 4)
    com_rva = u32(data, optional + 112 + 14 * 8)
    com_size = u32(data, optional + 112 + 14 * 8 + 4)

    require(machine == 0x8664, f"expected AMD64 machine 0x8664, got 0x{machine:04X}")
    require(subsystem == 10, f"expected EFI_APPLICATION subsystem 10, got {subsystem}")
    require(entry_rva != 0, "entry point RVA is zero")
    require((characteristics & 0x0002) != 0, "image is not marked executable")
    require(import_rva == 0 and import_size == 0,
            "unexpected PE import table; standalone image must not import host/runtime DLLs")
    require(com_rva == 0 and com_size == 0,
            "unexpected COM descriptor; EFI image must not carry managed runtime metadata")

    section_offset = optional + optional_size
    entry_section = None
    executable_sections: list[str] = []
    for index in range(section_count):
        header = section_offset + index * 40
        require(header + 40 <= len(data), "truncated section table")
        name = data[header : header + 8].split(b"\0", 1)[0].decode("ascii", "replace")
        virtual_size = u32(data, header + 8)
        virtual_address = u32(data, header + 12)
        raw_size = u32(data, header + 16)
        section_flags = u32(data, header + 36)
        span = max(virtual_size, raw_size)
        if section_flags & 0x20000000:
            executable_sections.append(name)
            if virtual_address <= entry_rva < virtual_address + span:
                entry_section = name
    require(entry_section is not None,
            "entry point does not reside in an executable section")
    require(entry_section == ".text", f"entry point is in unexpected section {entry_section}")

    return {
        "path": str(path.resolve()),
        "size_bytes": len(data),
        "sha256": hashlib.sha256(data).hexdigest().upper(),
        "machine": f"0x{machine:04X} AMD64",
        "subsystem": f"{subsystem} EFI_APPLICATION",
        "coff_dll_characteristic": bool(characteristics & 0x2000),
        "entry_point_rva": f"0x{entry_rva:08X}",
        "entry_point_section": entry_section,
        "executable_sections": executable_sections,
        "import_directory": {"rva": import_rva, "size": import_size},
        "com_descriptor": {"rva": com_rva, "size": com_size},
        "certificate_table": {"file_offset": security_rva, "size": security_size},
        "signed": security_rva != 0 and security_size != 0,
        "result": "PASS",
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("efi", type=Path)
    args = parser.parse_args()
    try:
        result = verify(args.efi)
    except (OSError, ValueError, struct.error) as error:
        raise SystemExit(f"EFI validation failed: {error}") from error
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
