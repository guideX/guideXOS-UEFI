#!/usr/bin/env python3
"""Build guideXOS RDSK images.

The default remains the legacy RDSK v1 format. Pass ``--version 2`` to build
the opt-in, checksummed RDSK v2 format documented in
``docs/AMCONV2F_RDSK_V2_FORMAT_AND_VALIDATION.md``.
"""

from __future__ import annotations

import argparse
import os
import struct
import sys
import tempfile
import zlib
from pathlib import Path


MAX_IMAGE_BYTES = 512 * 1024 * 1024
MAX_FILE_BYTES = 100 * 1024 * 1024
MAX_V2_RECORDS = 4096
MAX_V2_NAME_BYTES = 255
MAX_V2_PATH_BYTES = 1023
V2_HEADER_BYTES = 64
V2_RECORD_FIXED_BYTES = 28


def collect_files(root: str) -> list[tuple[str, str]]:
    """Recursively collect files for the historical v1 writer."""
    files: list[tuple[str, str]] = []
    for dirpath, _, filenames in os.walk(root):
        for filename in filenames:
            full_path = os.path.join(dirpath, filename)
            rel_path = os.path.relpath(full_path, root).replace("\\", "/")
            files.append((rel_path, full_path))
    return files


def build_ramdisk_v1(srcdir: str, outpath: str) -> None:
    """Write the unchanged RDSK v1 wire layout."""
    if not os.path.isdir(srcdir):
        raise ValueError(f"Source directory does not exist: {srcdir}")
    files = collect_files(srcdir)
    output = Path(outpath)
    output.parent.mkdir(parents=True, exist_ok=True)

    print(f"Building RDSK v1 with {len(files)} files...")
    total_content = 0
    with output.open("wb") as out:
        out.write(b"RDSK")
        out.write(struct.pack("<I", 1))
        out.write(struct.pack("<I", len(files)))
        for rel_path, full_path in files:
            with open(full_path, "rb") as source:
                data = source.read()
            path_bytes = rel_path.encode("utf-8")
            if len(path_bytes) > 0xffff or len(data) > 0xffffffff:
                raise ValueError(f"RDSK v1 record is too large: {rel_path}")
            out.write(struct.pack("<H", len(path_bytes)))
            out.write(path_bytes)
            out.write(struct.pack("<I", len(data)))
            out.write(data)
            total_content += len(data)
            print(f"  Added: {rel_path} ({len(data)} bytes)")

    print(f"RDSK v1 image created: {output} ({output.stat().st_size} bytes; "
          f"{total_content} payload bytes)")


def _encode_v2_name(name: str) -> bytes:
    try:
        encoded = name.encode("ascii", errors="strict")
    except UnicodeEncodeError as error:
        raise ValueError(f"RDSK v2 supports ASCII names only: {name!r}") from error
    if not encoded or len(encoded) > MAX_V2_NAME_BYTES:
        raise ValueError(f"RDSK v2 name length must be 1..{MAX_V2_NAME_BYTES}: {name!r}")
    if name in (".", ".."):
        raise ValueError(f"RDSK v2 does not allow dot path segments: {name!r}")
    if any(byte < 0x20 or byte > 0x7e or byte in (ord("/"), ord("\\"))
           for byte in encoded):
        raise ValueError(f"RDSK v2 name contains a prohibited character: {name!r}")
    return encoded


def _v2_child_path_length(parent_path_length: int, name: bytes,
                          parent_is_root: bool) -> int:
    path_length = parent_path_length + (0 if parent_is_root else 1) + len(name)
    if path_length > MAX_V2_PATH_BYTES:
        raise ValueError(
            f"RDSK v2 canonical path exceeds {MAX_V2_PATH_BYTES} bytes"
        )
    return path_length


def _is_inside(path: Path, directory: Path) -> bool:
    try:
        path.relative_to(directory)
        return True
    except ValueError:
        return False


def _collect_v2_records(source_root: Path) -> tuple[list[dict[str, object]], int, int]:
    records: list[dict[str, object]] = [{
        "kind": 1, "parent": 0xffffffff, "name": b"", "source": None,
        "size": 0, "path_length": 0,
    }]
    directories: list[tuple[int, Path, int]] = [(0, source_root, 0)]
    directory_count = 1
    file_count = 0
    directory_cursor = 0

    while directory_cursor < len(directories):
        parent_index, host_directory, parent_path_length = directories[directory_cursor]
        directory_cursor += 1
        try:
            entries = list(os.scandir(host_directory))
        except OSError as error:
            raise ValueError(f"Cannot enumerate source directory {host_directory}: {error}") from error

        encoded_entries: list[tuple[bytes, os.DirEntry[str]]] = []
        for entry in entries:
            encoded_entries.append((_encode_v2_name(entry.name), entry))
        encoded_entries.sort(key=lambda pair: pair[0])

        prior_name: bytes | None = None
        for name_bytes, entry in encoded_entries:
            if prior_name == name_bytes:
                raise ValueError(f"Duplicate sibling name in source tree: {entry.name!r}")
            prior_name = name_bytes

            if entry.is_symlink():
                raise ValueError(f"RDSK v2 source trees cannot contain symlinks: {entry.path}")
            name = entry.name
            try:
                path_length = _v2_child_path_length(parent_path_length,
                    name_bytes, parent_index == 0)
            except ValueError as error:
                raise ValueError(f"{error}: {name}") from error

            if entry.is_dir(follow_symlinks=False):
                kind = 1
                size = 0
                source_path: Path | None = None
                directory_count += 1
            elif entry.is_file(follow_symlinks=False):
                kind = 2
                try:
                    size = entry.stat(follow_symlinks=False).st_size
                except OSError as error:
                    raise ValueError(f"Cannot stat source file {entry.path}: {error}") from error
                if size < 0 or size > MAX_FILE_BYTES:
                    raise ValueError(f"RDSK v2 file exceeds {MAX_FILE_BYTES} bytes: {entry.path}")
                source_path = Path(entry.path)
                file_count += 1
            else:
                raise ValueError(f"RDSK v2 source contains a non-file entry: {entry.path}")

            record_index = len(records)
            records.append({
                "kind": kind,
                "parent": parent_index,
                "name": name_bytes,
                "source": source_path,
                "size": size,
                "path_length": path_length,
            })
            if kind == 1:
                directories.append((record_index, Path(entry.path), path_length))

            if len(records) > MAX_V2_RECORDS:
                raise ValueError(f"RDSK v2 record count exceeds {MAX_V2_RECORDS}")

    if file_count + directory_count != len(records):
        raise ValueError("Internal RDSK v2 record count mismatch")
    return records, directory_count, file_count


def _write_v2_file_payload(out, record: dict[str, object]) -> None:
    source_path = record["source"]
    expected_size = int(record["size"])
    if not isinstance(source_path, Path):
        raise ValueError("Internal RDSK v2 payload record has no source path")
    try:
        with source_path.open("rb") as source:
            remaining = expected_size
            while remaining:
                chunk = source.read(min(1024 * 1024, remaining))
                if not chunk:
                    raise ValueError(f"Source file changed while building image: {source_path}")
                out.write(chunk)
                remaining -= len(chunk)
            if source.read(1):
                raise ValueError(f"Source file grew while building image: {source_path}")
    except OSError as error:
        raise ValueError(f"Cannot read source file {source_path}: {error}") from error


def build_ramdisk_v2(srcdir: str, outpath: str) -> None:
    """Write a deterministic, explicit-directory RDSK v2 image."""
    source_root = Path(srcdir).resolve(strict=True)
    if not source_root.is_dir():
        raise ValueError(f"Source directory does not exist: {srcdir}")
    output = Path(outpath).resolve(strict=False)
    if _is_inside(output, source_root):
        raise ValueError("RDSK output must be outside the selected source tree")
    output.parent.mkdir(parents=True, exist_ok=True)

    records, directory_count, file_count = _collect_v2_records(source_root)
    table_size = sum(V2_RECORD_FIXED_BYTES + len(record["name"])
                     for record in records)
    if table_size > 4 * 1024 * 1024:
        raise ValueError("RDSK v2 record table exceeds 4 MiB")
    data_offset = V2_HEADER_BYTES + table_size
    image_size = data_offset + sum(int(record["size"]) for record in records)
    if image_size > MAX_IMAGE_BYTES:
        raise ValueError(f"RDSK v2 image exceeds {MAX_IMAGE_BYTES} bytes")

    cursor = data_offset
    table = bytearray()
    for record in records:
        kind = int(record["kind"])
        name = record["name"]
        file_offset = cursor if kind == 2 else 0
        file_length = int(record["size"]) if kind == 2 else 0
        table.extend(struct.pack("<BBHIQQI", kind, 0, len(name),
                                 int(record["parent"]), file_offset,
                                 file_length, 0))
        table.extend(name)
        if kind == 2:
            cursor += file_length

    header = bytearray(V2_HEADER_BYTES)
    struct.pack_into("<4sIIIQIIIQQQI", header, 0,
                     b"RDSK", 2, V2_HEADER_BYTES, 0,
                     image_size, len(records), directory_count, file_count,
                     V2_HEADER_BYTES, table_size, data_offset, 0)

    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(mode="w+b", dir=output.parent,
                prefix=f".{output.name}.", suffix=".tmp", delete=False) as out:
            temporary_path = Path(out.name)
            out.write(header)
            out.write(table)
            for record in records:
                if int(record["kind"]) == 2:
                    _write_v2_file_payload(out, record)
            out.flush()
            os.fsync(out.fileno())

        crc = 0
        with temporary_path.open("rb") as image:
            while True:
                chunk = image.read(1024 * 1024)
                if not chunk:
                    break
                crc = zlib.crc32(chunk, crc)
        with temporary_path.open("r+b") as image:
            image.seek(60)
            image.write(struct.pack("<I", crc & 0xffffffff))
            image.flush()
            os.fsync(image.fileno())
        os.replace(temporary_path, output)
        temporary_path = None
    finally:
        if temporary_path is not None:
            try:
                temporary_path.unlink()
            except FileNotFoundError:
                pass

    print(f"RDSK v2 image created: {output} ({image_size} bytes; "
          f"{directory_count} directories; {file_count} files)")


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a guideXOS RDSK image")
    parser.add_argument("source_directory")
    parser.add_argument("output_image")
    parser.add_argument("--version", type=int, choices=(1, 2), default=1,
                        help="image version (default: 1; version 2 is opt-in)")
    args = parser.parse_args()

    try:
        if args.version == 2:
            build_ramdisk_v2(args.source_directory, args.output_image)
        else:
            build_ramdisk_v1(args.source_directory, args.output_image)
    except (OSError, ValueError, struct.error) as error:
        print(f"Error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
