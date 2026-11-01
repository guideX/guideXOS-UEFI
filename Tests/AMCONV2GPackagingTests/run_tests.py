#!/usr/bin/env python3
"""Exercise managed-artifact verification for generated RDSK v1 and v2 images."""

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BUILDER = ROOT / "Tools" / "ramdisk_builder.py"
sys.path.insert(0, str(ROOT / "Tools" / "ManagedArtifacts"))
from managed_artifacts import (  # noqa: E402
    read_v1_ramdisk_entries,
    read_v2_ramdisk_entries,
    verify_ramdisk_image,
)


class ManagedRamdiskAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls._temporary = tempfile.TemporaryDirectory(prefix="amconv2g-managed-")
        cls.root = Path(cls._temporary.name)
        cls.source = cls.root / "source"
        native = cls.source / "Native"
        native.mkdir(parents=True)
        (native / "app.exe").write_bytes(b"known application image")
        (native / "app.gxmi").write_bytes(b"known descriptor")
        (cls.source / "Empty").mkdir()
        (cls.source / "Nested" / "DeepEmpty").mkdir(parents=True)
        (cls.source / "WithFiles" / "Child").mkdir(parents=True)
        (cls.source / "WithFiles" / "inside.txt").write_bytes(b"inside payload")
        (cls.source / "Zero.bin").write_bytes(b"")

        cls.v1_image = cls.root / "candidate-v1.img"
        cls.v2_image = cls.root / "candidate-v2.img"
        subprocess.run(
            [sys.executable, "-B", str(BUILDER), str(cls.source), str(cls.v1_image)],
            check=True,
            capture_output=True,
            text=True,
        )
        subprocess.run(
            [sys.executable, "-B", str(BUILDER), str(cls.source), str(cls.v2_image),
             "--version", "2"],
            check=True,
            capture_output=True,
            text=True,
        )

    @classmethod
    def tearDownClass(cls) -> None:
        cls._temporary.cleanup()

    def test_v1_managed_artifact_bytes_match(self) -> None:
        entries = read_v1_ramdisk_entries(self.v1_image.read_bytes(), self.v1_image)
        self.assertEqual(entries["Native/app.exe"], b"known application image")
        rows = [{"executable_path": "Native/app.exe", "descriptor_path": "Native/app.gxmi"}]
        verify_ramdisk_image(
            self.v1_image, self.source / "Native", rows, expected_version=1
        )
        self.assertEqual(rows[0]["ramdisk_equals_source"], "true")

    def test_v2_records_payloads_and_managed_bytes_match(self) -> None:
        data = self.v2_image.read_bytes()
        entries = read_v2_ramdisk_entries(data, self.v2_image)
        self.assertIn("Native/app.exe", entries)
        self.assertIn("WithFiles/inside.txt", entries)
        self.assertIn("Zero.bin", entries)
        zero_offset, zero_length = entries["Zero.bin"]
        self.assertEqual(data[zero_offset : zero_offset + zero_length], b"")
        rows = [{"executable_path": "Native/app.exe", "descriptor_path": "Native/app.gxmi"}]
        verify_ramdisk_image(
            self.v2_image, self.source / "Native", rows, expected_version=2
        )
        self.assertEqual(rows[0]["ramdisk_equals_source"], "true")

    def test_v2_crc_mismatch_is_rejected(self) -> None:
        corrupt = bytearray(self.v2_image.read_bytes())
        corrupt[-1] ^= 1
        with self.assertRaisesRegex(ValueError, "CRC32 mismatch"):
            read_v2_ramdisk_entries(bytes(corrupt), self.v2_image)

    def test_expected_version_mismatch_is_rejected(self) -> None:
        rows = [{"executable_path": "Native/app.exe", "descriptor_path": "Native/app.gxmi"}]
        with self.assertRaisesRegex(ValueError, "does not match expected version"):
            verify_ramdisk_image(
                self.v2_image, self.source / "Native", rows, expected_version=1
            )


if __name__ == "__main__":
    unittest.main(verbosity=2)
