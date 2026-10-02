#!/usr/bin/env python3
"""Create a new, disposable 16 MiB FAT16 superfloppy for Phase 35Q."""

import hashlib
import struct
import sys
from pathlib import Path


SECTOR_SIZE = 512
TOTAL_SECTORS = 32768
VOLUME_SECTORS = TOTAL_SECTORS - 4  # reserve four raw diagnostic sectors
FAT_SECTORS = 128
ROOT_ENTRIES = 512
ROOT_SECTORS = ROOT_ENTRIES * 32 // SECTOR_SIZE
VOLUME_ID = 0x35355131


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: new_phase35q_fat16_image.py <new-image-path>", file=sys.stderr)
        return 2

    path = Path(sys.argv[1]).resolve()
    if path.exists():
        print(f"refusing to overwrite existing image: {path}", file=sys.stderr)
        return 2

    image = bytearray(TOTAL_SECTORS * SECTOR_SIZE)
    boot = memoryview(image)[:SECTOR_SIZE]
    boot[0:3] = b"\xEB\x3C\x90"
    boot[3:11] = b"GUIDEXOS"
    struct.pack_into("<H", boot, 11, SECTOR_SIZE)
    boot[13] = 1  # one sector per cluster
    struct.pack_into("<H", boot, 14, 1)  # reserved sectors
    boot[16] = 2  # mirrored FATs
    struct.pack_into("<H", boot, 17, ROOT_ENTRIES)
    struct.pack_into("<H", boot, 19, VOLUME_SECTORS)
    boot[21] = 0xF8
    struct.pack_into("<H", boot, 22, FAT_SECTORS)
    struct.pack_into("<H", boot, 24, 63)
    struct.pack_into("<H", boot, 26, 16)
    struct.pack_into("<I", boot, 28, 0)
    struct.pack_into("<I", boot, 32, 0)
    boot[36] = 0x80  # drive number
    boot[38] = 0x29  # extended boot signature
    struct.pack_into("<I", boot, 39, VOLUME_ID)
    boot[43:54] = b"GX35Q TEST "
    boot[54:62] = b"FAT16   "
    boot[510:512] = b"\x55\xAA"

    # FAT16 reserved entries: media descriptor and two reserved clusters.
    first_fat = SECTOR_SIZE
    second_fat = (1 + FAT_SECTORS) * SECTOR_SIZE
    for offset in (first_fat, second_fat):
        image[offset : offset + 4] = b"\xF8\xFF\xFF\xFF"

    cluster_count = VOLUME_SECTORS - 1 - (2 * FAT_SECTORS) - ROOT_SECTORS
    if not (4085 <= cluster_count < 65525):
        raise RuntimeError(f"fixture is not FAT16: cluster_count={cluster_count}")
    with path.open("xb") as stream:
        stream.write(image)
        stream.flush()

    digest = hashlib.sha256(path.read_bytes()).hexdigest().upper()
    print(
        f"created={path};bytes={len(image)};sectors={TOTAL_SECTORS};"
        f"volumeSectors={VOLUME_SECTORS};fat=FAT16;sha256={digest}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
