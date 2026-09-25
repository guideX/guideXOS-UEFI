#!/usr/bin/env python3
"""Build the sidecar contract for the independent Phase 25 bootstrap."""

from __future__ import annotations

import argparse
import hashlib
import struct
from pathlib import Path

MAGIC = 0x49425847  # GXBI, little endian
VERSION = 1
MACHINE_AMD64 = 0x8664
FLAGS = 0x1F  # fixed VA, no relocations, no imports, RX, managed image external
PREFERRED_BASE = 0x0000401200000000
STARTUP_VERSION = 1
ABI_VERSION = 1
MAX_IMAGE_SIZE = 0x10000
HEADER = struct.Struct("<IIIIIIQIIIIIII32s")


def build(image_path: Path, descriptor_path: Path) -> None:
    image = image_path.read_bytes()
    if not image or len(image) > MAX_IMAGE_SIZE:
        raise SystemExit("bootstrap size is outside the bounded contract")
    image_size = (len(image) + 0xFFF) & ~0xFFF
    digest = hashlib.sha256(image).digest()
    header = HEADER.pack(
        MAGIC,
        VERSION,
        MACHINE_AMD64,
        FLAGS,
        len(image),
        image_size,
        PREFERRED_BASE,
        0,              # entry offset
        0,              # executable offset
        len(image),     # executable size
        STARTUP_VERSION,
        ABI_VERSION,
        0,              # import count
        0,              # relocation count
        digest,
    )
    descriptor_path.parent.mkdir(parents=True, exist_ok=True)
    descriptor_path.write_bytes(header)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("descriptor", type=Path)
    args = parser.parse_args()
    build(args.image, args.descriptor)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
