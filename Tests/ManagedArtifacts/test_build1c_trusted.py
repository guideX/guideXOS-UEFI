"""Synthetic-only contract tests for the BUILD1C preflight/verifier."""

from __future__ import annotations

import hashlib
import json
import struct
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any


REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "Tools" / "ManagedArtifacts"))
sys.path.insert(0, str(REPO / "Tools" / "Phase23"))

import build1c_trusted as trusted  # noqa: E402
import managed_artifacts as identities  # noqa: E402
import verify_guidexos_artifact as phase23_verifier  # noqa: E402


def _sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _synthetic_pe(seed: int = 1) -> bytes:
    image = bytearray(512)
    image[0:2] = b"MZ"
    struct.pack_into("<I", image, 0x3C, 0x80)
    image[0x80:0x84] = b"PE\0\0"
    struct.pack_into("<H", image, 0x84, 0x8664)
    struct.pack_into("<H", image, 0x86, 0)
    struct.pack_into("<I", image, 0x88, 0)
    struct.pack_into("<H", image, 0x94, 240)
    struct.pack_into("<H", image, 0x98, 0x20B)
    image[450:458] = seed.to_bytes(8, "little")
    return bytes(image)


def _synthetic_gxmi(payload: Any, image: bytes) -> bytes:  # type: ignore[name-defined]
    descriptor = bytearray(identities.DESCRIPTOR_HEADER_SIZE)
    flags = 0x1F | payload.proof_flags
    struct.pack_into("<IIIIII", descriptor, 0, 0x494D5847, 1, 0x47554944, 0x8664, flags, len(image))
    struct.pack_into("<I", descriptor, 48, 0)
    descriptor[identities.DESCRIPTOR_DIGEST_OFFSET : identities.DESCRIPTOR_DIGEST_OFFSET + 32] = bytes.fromhex(_sha(image))
    return bytes(descriptor)


class Build1CFixture:
    def __init__(self, root: Path, variant: str = "success"):
        self.root = root
        self.workspace = root / "run"
        self.build_root = self.workspace / "build/phase29"
        self.stage = self.workspace / "stage"
        self.payload = next(item for item in identities.PAYLOADS if item.phase == "Phase29" and item.variant == variant)
        self.exe = self.build_root / variant / "publish" / f"{self.payload.basename}.exe"
        self.record = self.build_root / "phase29-managed-notification-build.json"
        self.workspace.mkdir(parents=True)
        self.exe.parent.mkdir(parents=True)
        self.stage.mkdir(parents=True)
        image = _synthetic_pe()
        self.exe.write_bytes(image)
        (self.stage / f"{self.payload.basename}.exe").write_bytes(image)
        (self.stage / f"{self.payload.basename}.gxmi").write_bytes(_synthetic_gxmi(self.payload, image))
        record = {
            "schema": 1,
            "generatedBy": "Tools/Phase29/build_phase29_managed_notification.ps1",
            "payloads": [{
                "name": variant,
                "mode": "Success",
                "path": str(self.exe),
                "length": len(image),
                "sha256": _sha(image),
                "project": "synthetic-only",
            }],
        }
        self.record.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")


class Build1CTrustedTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(prefix="build1c-synthetic-")
        self.root = Path(self.temp.name)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def test_missing_runtime_dependency_fails_closed(self) -> None:
        with self.assertRaisesRegex(trusted.Build1CError, "pinned runtime source is missing"):
            trusted.validate_runtime_tree(self.root)

    def test_wrong_source_commit_and_patch_hash_fail_closed(self) -> None:
        with self.assertRaisesRegex(trusted.Build1CError, "commit mismatch"):
            trusted.require_commit("1" * 40, "2" * 40, "runtime source")
        with self.assertRaisesRegex(trusted.Build1CError, "hash mismatch"):
            trusted.require_hash("A" * 64, "B" * 64, "Phase 23 patch")

    def test_incorrect_toolchain_prerequisite_fails_closed(self) -> None:
        toolchain = {
            "powershell": {"version": "5.1"},
            "vcvars64": "vcvars64.bat",
            "cl": None,
            "link": "link.exe",
            "lib": "lib.exe",
            "windowsSdk": "10.0",
            "nasm": "nasm.exe",
            "dotnet": "dotnet.exe",
            "runtimeDotnet": "runtime-dotnet.exe",
            "python": "python.exe",
            "git": "git.exe",
        }
        with self.assertRaisesRegex(trusted.Build1CError, "prerequisite is missing: cl"):
            trusted.validate_toolchain(toolchain)

    def test_missing_package_evidence_fails_closed(self) -> None:
        with self.assertRaisesRegex(trusted.Build1CError, "supplementary runtime package evidence is missing"):
            trusted.validate_package_bundle(self.root / "pack", self.root / "missing-evidence.json", "9" * 40)

    def test_stale_build_outputs_are_rejected(self) -> None:
        workspace = self.root / "run"
        workspace.mkdir()
        (workspace / "partial.log").write_text("interrupted", encoding="utf-8")
        with self.assertRaisesRegex(trusted.Build1CError, "stale or partial"):
            trusted.ensure_fresh_workspace(workspace)

    def test_mismatched_staged_payload_is_rejected(self) -> None:
        fixture = Build1CFixture(self.root)
        staged = fixture.stage / f"{fixture.payload.basename}.exe"
        changed = _synthetic_pe(seed=2)
        staged.write_bytes(changed)
        (fixture.stage / f"{fixture.payload.basename}.gxmi").write_bytes(_synthetic_gxmi(fixture.payload, changed))
        with self.assertRaisesRegex(trusted.Build1CError, "differs byte-for-byte"):
            trusted.verify_native_row(REPO, fixture.workspace, "success")

    def test_partial_or_interrupted_build_is_rejected(self) -> None:
        fixture = Build1CFixture(self.root)
        (fixture.stage / f"{fixture.payload.basename}.gxmi").unlink()
        with self.assertRaisesRegex(trusted.Build1CError, "pair is incomplete"):
            trusted.verify_native_row(REPO, fixture.workspace, "success")

    def test_valid_single_variant_native_provenance_row(self) -> None:
        fixture = Build1CFixture(self.root)
        row = trusted.verify_native_row(REPO, fixture.workspace, "success")
        self.assertEqual(row["phase"], "Phase29")
        self.assertEqual(row["variant"], "success")
        self.assertEqual(row["build_equals_staged"], "true")
        self.assertEqual(row["descriptor_equals_executable"], "true")

    def test_repeated_build_preflight_refuses_existing_workspace(self) -> None:
        workspace = self.root / "run"
        self.assertFalse(trusted.ensure_fresh_workspace(workspace)["exists"])
        workspace.mkdir()
        (workspace / "build-output").write_bytes(b"partial")
        with self.assertRaisesRegex(trusted.Build1CError, "stale or partial"):
            trusted.ensure_fresh_workspace(workspace)

    def test_deterministic_reporting(self) -> None:
        first = {"z": 2, "a": {"y": 1, "b": 3}}
        second = {"a": {"b": 3, "y": 1}, "z": 2}
        self.assertEqual(trusted.stable_json(first), trusted.stable_json(second))
        self.assertEqual(trusted.canonical_json_sha256(first), trusted.canonical_json_sha256(second))

    def test_phase23_verifier_reads_powershell_utf8_bom_json(self) -> None:
        path = self.root / "manifest.json"
        path.write_bytes(b"\xef\xbb\xbf{\"target\":\"guidexos-x64\"}\n")
        self.assertEqual(phase23_verifier.load_json(path)["target"], "guidexos-x64")

    def test_phase_map_accepts_only_explicit_phase_owned_pal_helpers(self) -> None:
        path = self.root / "guidexos.map"
        path.write_text("guidexos_pal_core\nguidexos_pal_helper\n", encoding="utf-8")
        manifest = {"phase19PalSymbols": ["guidexos_pal_core"]}
        with self.assertRaises(phase23_verifier.Rejected):
            phase23_verifier.validate_map(path, manifest)
        report = phase23_verifier.validate_map(path, manifest, ["guidexos_pal_helper"])
        self.assertEqual(report["palSymbols"], ["guidexos_pal_core", "guidexos_pal_helper"])

    def test_phase_specific_map_mode_allows_unused_manifest_exports(self) -> None:
        path = self.root / "subset.map"
        path.write_text("guidexos_pal_core\n", encoding="utf-8")
        manifest = {"phase19PalSymbols": ["guidexos_pal_core", "guidexos_pal_unused"]}
        with self.assertRaises(phase23_verifier.Rejected):
            phase23_verifier.validate_map(path, manifest)
        report = phase23_verifier.validate_map(path, manifest, require_all_symbols=False)
        self.assertEqual(report["palSymbols"], ["guidexos_pal_core"])


if __name__ == "__main__":
    unittest.main()
