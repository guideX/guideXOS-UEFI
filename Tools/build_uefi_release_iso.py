#!/usr/bin/env python3
"""Package a staged UEFI ESP as a bootable UEFI El Torito ISO."""

from __future__ import annotations

import argparse
import math
import shutil
import struct
import subprocess
import tempfile
from pathlib import Path


SECTOR_SIZE = 512
SECTORS_PER_CLUSTER = 64  # 32 KiB clusters; FAT16 remains in its valid range.
TOTAL_SECTORS = (128 * 1024 * 1024) // SECTOR_SIZE
ROOT_ENTRIES = 512
ROOT_SECTORS = (ROOT_ENTRIES * 32) // SECTOR_SIZE
RESERVED_SECTORS = 1
FAT_COUNT = 2
EOC = 0xFFFF
DOS_DATE = ((2026 - 1980) << 9) | (10 << 5) | 20


def short_name(name: str) -> bytes:
    stem, dot, extension = name.upper().partition(".")
    if len(stem) > 8 or len(extension) > 3 or not stem:
        raise ValueError(f"Not an 8.3 FAT name: {name}")
    return stem.ljust(8).encode("ascii") + extension.ljust(3).encode("ascii")


def make_entry(name: str | bytes, attributes: int, first_cluster: int = 0,
               size: int = 0) -> bytes:
    entry = bytearray(32)
    entry[0:11] = name if isinstance(name, bytes) else short_name(name)
    entry[11] = attributes
    struct.pack_into("<H", entry, 14, 0)  # create time
    struct.pack_into("<H", entry, 16, DOS_DATE)
    struct.pack_into("<H", entry, 18, DOS_DATE)
    struct.pack_into("<H", entry, 22, 0)  # write time
    struct.pack_into("<H", entry, 24, DOS_DATE)
    struct.pack_into("<H", entry, 26, first_cluster)
    struct.pack_into("<I", entry, 28, size)
    return bytes(entry)


def make_dot_entry(dotdot: bool, cluster: int) -> bytes:
    name = b"..         " if dotdot else b".          "
    entry = bytearray(make_entry(name, 0x10, cluster))
    return bytes(entry)


def create_fat16_image(esp: Path, image: Path, fat_label: str) -> None:
    sources = {
        "KERNEL.ELF": esp / "kernel.elf",
        "RAMDISK.IMG": esp / "ramdisk.img",
        "BOOTX64.EFI": esp / "EFI" / "BOOT" / "BOOTX64.EFI",
    }
    for path in sources.values():
        if not path.is_file():
            raise FileNotFoundError(f"Required ESP file is missing: {path}")
    payloads = {name: path.read_bytes() for name, path in sources.items()}

    fat_sectors = 1
    while True:
        data_sectors = (TOTAL_SECTORS - RESERVED_SECTORS -
                        FAT_COUNT * fat_sectors - ROOT_SECTORS)
        cluster_count = data_sectors // SECTORS_PER_CLUSTER
        required_fat_sectors = math.ceil((cluster_count + 2) * 2 / SECTOR_SIZE)
        if required_fat_sectors == fat_sectors:
            break
        fat_sectors = required_fat_sectors
    if not 4085 <= cluster_count <= 65524:
        raise RuntimeError(f"FAT16 cluster count is outside range: {cluster_count}")

    fat = bytearray(fat_sectors * SECTOR_SIZE)
    struct.pack_into("<H", fat, 0, 0xFFF8)
    struct.pack_into("<H", fat, 2, EOC)
    next_cluster = 2

    def allocate(data: bytes) -> int:
        nonlocal next_cluster
        cluster_bytes = SECTORS_PER_CLUSTER * SECTOR_SIZE
        count = max(1, math.ceil(len(data) / cluster_bytes))
        if next_cluster + count - 2 >= cluster_count:
            raise RuntimeError("The UEFI ESP files do not fit in the FAT16 image")
        first = next_cluster
        for offset in range(count):
            cluster = first + offset
            successor = cluster + 1 if offset + 1 < count else EOC
            struct.pack_into("<H", fat, cluster * 2, successor)
        next_cluster += count
        return first

    # Allocate directories first so their starting clusters are stable.
    efi_cluster = allocate(b"")
    boot_cluster = allocate(b"")
    file_clusters = {name: allocate(data) for name, data in payloads.items()}

    boot_sector = bytearray(SECTOR_SIZE)
    boot_sector[0:3] = b"\xEB\x3C\x90"
    boot_sector[3:11] = b"GUIDEXOS"
    struct.pack_into("<H", boot_sector, 11, SECTOR_SIZE)
    boot_sector[13] = SECTORS_PER_CLUSTER
    struct.pack_into("<H", boot_sector, 14, RESERVED_SECTORS)
    boot_sector[16] = FAT_COUNT
    struct.pack_into("<H", boot_sector, 17, ROOT_ENTRIES)
    struct.pack_into("<H", boot_sector, 19, 0)
    boot_sector[21] = 0xF8
    struct.pack_into("<H", boot_sector, 22, fat_sectors)
    struct.pack_into("<H", boot_sector, 24, 63)
    struct.pack_into("<H", boot_sector, 26, 255)
    struct.pack_into("<I", boot_sector, 28, 0)
    struct.pack_into("<I", boot_sector, 32, TOTAL_SECTORS)
    boot_sector[36] = 0x80
    boot_sector[38] = 0x29
    struct.pack_into("<I", boot_sector, 39, 0x32524347)
    boot_sector[43:54] = fat_label.encode("ascii")
    boot_sector[54:62] = b"FAT16   "
    boot_sector[510:512] = b"\x55\xAA"

    root_entries = [
        make_entry("EFI", 0x10, efi_cluster),
        make_entry("KERNEL.ELF", 0x20, file_clusters["KERNEL.ELF"],
                   len(payloads["KERNEL.ELF"])),
        make_entry("RAMDISK.IMG", 0x20, file_clusters["RAMDISK.IMG"],
                   len(payloads["RAMDISK.IMG"])),
    ]
    efi_entries = [make_dot_entry(False, efi_cluster),
                   make_dot_entry(True, 0)]
    efi_entries.append(make_entry("BOOT", 0x10, boot_cluster))
    boot_entries = [make_dot_entry(False, boot_cluster),
                    make_dot_entry(True, efi_cluster),
                    make_entry("BOOTX64.EFI", 0x20,
                               file_clusters["BOOTX64.EFI"],
                               len(payloads["BOOTX64.EFI"]))]

    data_start_sector = (RESERVED_SECTORS + FAT_COUNT * fat_sectors +
                         ROOT_SECTORS)
    cluster_bytes = SECTORS_PER_CLUSTER * SECTOR_SIZE

    with image.open("wb") as stream:
        stream.truncate(TOTAL_SECTORS * SECTOR_SIZE)
        stream.seek(0)
        stream.write(boot_sector)
        stream.seek(RESERVED_SECTORS * SECTOR_SIZE)
        stream.write(fat)
        stream.write(fat)

        def write_directory(entries: list[bytes], fixed_root: bool = False,
                            cluster: int = 0) -> None:
            data = b"".join(entries) + b"\x00\x00\x00\x00"
            if fixed_root:
                stream.seek((RESERVED_SECTORS + FAT_COUNT * fat_sectors) *
                            SECTOR_SIZE)
            else:
                sector = data_start_sector + (cluster - 2) * SECTORS_PER_CLUSTER
                stream.seek(sector * SECTOR_SIZE)
            stream.write(data)

        write_directory(root_entries, fixed_root=True)
        write_directory(efi_entries, cluster=efi_cluster)
        write_directory(boot_entries, cluster=boot_cluster)

        for name, data in payloads.items():
            cluster = file_clusters[name]
            offset = 0
            while offset < len(data) or (not data and offset == 0):
                sector = data_start_sector + (cluster - 2) * SECTORS_PER_CLUSTER
                stream.seek(sector * SECTOR_SIZE)
                chunk = data[offset:offset + cluster_bytes]
                stream.write(chunk)
                if not data:
                    break
                offset += len(chunk)
                if offset < len(data):
                    cluster = cluster + 1


def mark_efi_el_torito(iso: Path) -> None:
    """Advertise the no-emulation FAT image as the EFI system partition."""
    sector_size = 2048
    catalog_lba = None
    with iso.open("r+b") as stream:
        for lba in range(16, 64):
            stream.seek(lba * sector_size)
            descriptor = stream.read(sector_size)
            if (len(descriptor) == sector_size and descriptor[0] == 0 and
                    descriptor[1:6] == b"CD001" and descriptor[6] == 1 and
                    descriptor[7:30] == b"EL TORITO SPECIFICATION"):
                catalog_lba = struct.unpack_from("<I", descriptor, 71)[0]
                break
        if catalog_lba is None:
            raise RuntimeError("mkisofs output has no El Torito boot record")

        catalog_offset = catalog_lba * sector_size
        stream.seek(catalog_offset)
        catalog = bytearray(stream.read(sector_size))
        if (len(catalog) != sector_size or catalog[0] != 1 or
                struct.unpack_from("<H", catalog, 30)[0] != 0xAA55):
            raise RuntimeError("mkisofs emitted an invalid El Torito catalog")
        if catalog[32] != 0x88 or catalog[33] != 0:
            raise RuntimeError("El Torito default entry is not bootable/no-emulation")

        catalog[1] = 0xEF
        struct.pack_into("<H", catalog, 28, 0)
        checksum = (-sum(struct.unpack_from("<16H", catalog, 0))) & 0xFFFF
        struct.pack_into("<H", catalog, 28, checksum)
        if sum(struct.unpack_from("<16H", catalog, 0)) & 0xFFFF:
            raise RuntimeError("Failed to checksum the EFI El Torito validation entry")
        stream.seek(catalog_offset)
        stream.write(catalog)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--esp", type=Path, required=True,
                        help="build.ps1 staged ESP directory")
    parser.add_argument("--output", type=Path, required=True,
                        help="separately named release ISO output")
    parser.add_argument("--mkisofs", type=Path, required=True,
                        help="repository Tools/mkisofs.exe")
    parser.add_argument("--label", default="GUIDEXOS_RC2",
                        help="ISO volume label (default: %(default)s)")
    parser.add_argument("--fat-label", default="GUIDEXOSRC2",
                        help="11-character FAT volume label (default: %(default)s)")
    args = parser.parse_args()
    try:
        fat_label_bytes = args.fat_label.encode("ascii")
    except UnicodeEncodeError:
        parser.error("--fat-label must contain ASCII characters")
    if len(fat_label_bytes) != 11 or args.fat_label != args.fat_label.upper():
        parser.error("--fat-label must be exactly 11 uppercase ASCII characters")

    esp = args.esp.resolve()
    output = args.output.resolve()
    mkisofs = args.mkisofs.resolve()
    if output.exists():
        raise FileExistsError(f"Refusing to overwrite existing release artifact: {output}")
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="uefiiso_", dir=output.parent) as temp:
        stage = Path(temp)
        efi_image = stage / "EFIESP.IMG"
        create_fat16_image(esp, efi_image, args.fat_label)

        # Keep the standalone ISO tree usable by tools that extract ISO files
        # to a FAT USB volume in ISO mode. The El Torito FAT16 image contains
        # the same UEFI loader and root kernel/ramdisk payload.
        boot_dir = stage / "EFI" / "BOOT"
        boot_dir.mkdir(parents=True)
        shutil.copy2(esp / "EFI" / "BOOT" / "BOOTX64.EFI",
                     boot_dir / "BOOTX64.EFI")
        shutil.copy2(esp / "EFI" / "BOOT" / "BOOTX64.EFI",
                     stage / "BOOTX64.EFI")
        shutil.copy2(esp / "kernel.elf", stage / "kernel.elf")
        shutil.copy2(esp / "ramdisk.img", stage / "ramdisk.img")
        command = [str(mkisofs), "-o", str(output), "-V", args.label,
                   "-relaxed-filenames", "-J", "-R", "-eltorito-alt-boot",
                   "-eltorito-boot", efi_image.name, "-no-emul-boot",
                   str(stage)]
        subprocess.run(command, check=True)
        mark_efi_el_torito(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
