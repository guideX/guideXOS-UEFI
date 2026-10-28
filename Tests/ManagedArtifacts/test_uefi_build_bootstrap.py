"""Synthetic-only tests for the read-only managed-artifact bootstrap audit."""

from __future__ import annotations

import concurrent.futures
import hashlib
import json
import shutil
import struct
import sys
import tempfile
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "Tools" / "ManagedArtifacts"))

import managed_artifacts as identities  # noqa: E402
import uefi_build_bootstrap as bootstrap  # noqa: E402


def _sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _synthetic_pe(seed: int) -> bytes:
    """Return a tiny PE32+ shape that is only suitable for rejection/contract tests."""
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


def _synthetic_gxmi(payload, image: bytes, version: int = 1) -> bytes:
    descriptor = bytearray(identities.DESCRIPTOR_HEADER_SIZE)
    flags = 0x1F | payload.proof_flags
    struct.pack_into("<IIIIII", descriptor, 0, 0x494D5847, version, 0x47554944, 0x8664, flags, len(image))
    struct.pack_into("<I", descriptor, 48, 0)
    descriptor[identities.DESCRIPTOR_DIGEST_OFFSET : identities.DESCRIPTOR_DIGEST_OFFSET + 32] = bytes.fromhex(_sha(image))
    return bytes(descriptor)


class BootstrapFixture:
    def __init__(self, base: Path, name: str):
        self.root = base / name
        self.root.mkdir(parents=True)
        (self.root / "SYNTHETIC_TEST_FIXTURE.txt").write_text(
            "Synthetic bytes for unit tests only; never a production proof artifact.\n",
            encoding="utf-8",
        )
        self._copy_contract_files()
        self.reset_artifacts()

    def reset_artifacts(self) -> None:
        self._write_artifacts()

    def _copy_contract_files(self) -> None:
        for relative in bootstrap.contract_files(REPO):
            source = REPO / relative
            destination = self.root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, destination)

    def _write_artifacts(self) -> None:
        stage = self.root / "ramdisk_src" / "Native"
        stage.mkdir(parents=True, exist_ok=True)
        record_payloads: dict[str, list[dict[str, object]]] = {}
        record_paths: dict[str, Path] = {}

        for index, payload in enumerate(identities.PAYLOADS, start=1):
            phase_dir = bootstrap.PHASE_DIRS[payload.phase]
            build_dir = self.root / "out" / "dotnet" / phase_dir / payload.build_mode / "publish"
            build_dir.mkdir(parents=True, exist_ok=True)
            build_path = build_dir / f"{payload.basename}.exe"
            image = _synthetic_pe(index)
            build_path.write_bytes(image)
            staged_exe = stage / f"{payload.basename}.exe"
            staged_exe.write_bytes(image)
            (stage / f"{payload.basename}.gxmi").write_bytes(_synthetic_gxmi(payload, image))

            record_payloads.setdefault(payload.phase, []).append(
                {
                    "name": payload.build_mode,
                    "mode": payload.build_mode,
                    "path": str(build_path.resolve()),
                    "length": len(image),
                    "sha256": _sha(image),
                    "project": str((self.root / f"{payload.phase}.synthetic.csproj").resolve()),
                }
            )
            record_paths[payload.phase] = (
                self.root
                / "out"
                / "dotnet"
                / phase_dir
                / payload.record_name
            )

        for phase, entries in record_payloads.items():
            builder = bootstrap.PHASE_BUILDERS[phase]
            record = {
                "schema": 1,
                "generatedBy": f"Tools/{phase}/{builder}",
                "target": "guidexos-x64",
                "targetOs": "guidexos",
                "sdkProject": str((self.root / "GuideXos.User/GuideXos.User.csproj").resolve()),
                "payloads": entries,
            }
            record_path = record_paths[phase]
            record_path.parent.mkdir(parents=True, exist_ok=True)
            record_path.write_text(json.dumps(record, indent=2), encoding="utf-8")

        rows = identities.collect_rows(self.root, stage)
        identity_path = self.root / "guideXOS" / "obj" / "ManagedArtifactIdentities.g.cs"
        identities.emit_csharp(rows, identity_path)


def _tree_snapshot(root: Path) -> dict[str, str]:
    return {
        path.relative_to(root).as_posix(): _sha(path.read_bytes())
        for path in root.rglob("*")
        if path.is_file()
    }


class UefiBuildBootstrapTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.temp = tempfile.TemporaryDirectory(prefix="uefi-bootstrap-synthetic-")
        cls.base = Path(cls.temp.name)
        cls.source = BootstrapFixture(cls.base, "source")
        cls.target = BootstrapFixture(cls.base, "target")

    def setUp(self) -> None:
        self.source.reset_artifacts()
        self.target.reset_artifacts()

    @classmethod
    def tearDownClass(cls) -> None:
        cls.temp.cleanup()

    def _report(self) -> dict:
        return bootstrap.audit(self.target.root, [self.source.root])

    def _source_row(self, phase: str, variant: str) -> dict:
        return next(
            row
            for row in bootstrap.validate_artifacts(self.source.root)["artifacts"]
            if row["phase"] == phase and row["variant"] == variant
        )

    def _source_paths(self, phase: str, variant: str) -> tuple[Path, Path, Path]:
        payload = next(p for p in identities.PAYLOADS if p.phase == phase and p.variant == variant)
        stage = self.source.root / "ramdisk_src" / "Native"
        record = bootstrap.expected_record_path(self.source.root, phase, payload.record_name)
        return (
            stage / f"{payload.basename}.exe",
            stage / f"{payload.basename}.gxmi",
            record,
        )

    def test_valid_matching_payload_and_provenance(self) -> None:
        report = self._report()["candidates"][0]
        self.assertTrue(report["sourceArtifactAudit"]["verified"])
        self.assertEqual(report["sourceArtifactAudit"]["nativeGenerator"]["status"], "PASS")
        self.assertTrue(report["buildContract"]["compatible"])
        self.assertTrue(report["fullSetReusableCandidate"])
        self.assertEqual(report["samePairCount"], len(identities.PAYLOADS))

    def test_missing_artifact_is_reported_and_refused(self) -> None:
        exe, _, _ = self._source_paths("Phase29", "success")
        exe.unlink()
        report = self._report()["candidates"][0]
        self.assertFalse(report["fullSetReusableCandidate"])
        row = next(row for row in report["sourceArtifactAudit"]["artifacts"] if row["phase"] == "Phase29" and row["variant"] == "success")
        self.assertIn("STAGED_EXECUTABLE_MISSING", row["issues"])

    def test_incorrect_executable_record_hash_is_rejected(self) -> None:
        _, _, record_path = self._source_paths("Phase29", "success")
        record = json.loads(record_path.read_text(encoding="utf-8"))
        record["payloads"][0]["sha256"] = "0" * 64
        record_path.write_text(json.dumps(record), encoding="utf-8")
        report = self._report()["candidates"][0]
        row = next(row for row in report["sourceArtifactAudit"]["artifacts"] if row["phase"] == "Phase29" and row["variant"] == "success")
        self.assertIn("BUILD_RECORD_HASH_MISMATCH", row["issues"])
        self.assertFalse(report["fullSetReusableCandidate"])

    def test_incorrect_descriptor_hash_is_rejected(self) -> None:
        _, descriptor, _ = self._source_paths("Phase29", "success")
        data = bytearray(descriptor.read_bytes())
        data[identities.DESCRIPTOR_DIGEST_OFFSET] ^= 0x01
        descriptor.write_bytes(data)
        report = self._report()["candidates"][0]
        row = next(row for row in report["sourceArtifactAudit"]["artifacts"] if row["phase"] == "Phase29" and row["variant"] == "success")
        self.assertIn("DESCRIPTOR_EXECUTABLE_HASH_MISMATCH", row["issues"])
        self.assertFalse(report["fullSetReusableCandidate"])

    def test_mismatched_build_and_stage_record_is_rejected(self) -> None:
        exe, descriptor, _ = self._source_paths("Phase29", "success")
        payload = next(p for p in identities.PAYLOADS if p.phase == "Phase29" and p.variant == "success")
        changed = _synthetic_pe(9001)
        exe.write_bytes(changed)
        descriptor.write_bytes(_synthetic_gxmi(payload, changed))
        report = self._report()["candidates"][0]
        row = next(row for row in report["sourceArtifactAudit"]["artifacts"] if row["phase"] == "Phase29" and row["variant"] == "success")
        self.assertIn("BUILD_STAGE_BYTE_MISMATCH", row["issues"])
        self.assertFalse(report["fullSetReusableCandidate"])

    def test_missing_runtime_dependency_is_reported(self) -> None:
        result = bootstrap.audit_runtime(self.target.root)
        self.assertEqual(result["status"], "MISSING")
        self.assertIn("PINNED_RUNTIME_SOURCE_MISSING", result["issues"])
        self.assertFalse(result["localBuildPrerequisitesPresent"])

    def test_unsupported_descriptor_version_is_rejected(self) -> None:
        _, descriptor, _ = self._source_paths("Phase29", "success")
        payload = next(p for p in identities.PAYLOADS if p.phase == "Phase29" and p.variant == "success")
        exe = self._source_paths("Phase29", "success")[0]
        descriptor.write_bytes(_synthetic_gxmi(payload, exe.read_bytes(), version=2))
        report = self._report()["candidates"][0]
        row = next(row for row in report["sourceArtifactAudit"]["artifacts"] if row["phase"] == "Phase29" and row["variant"] == "success")
        self.assertIn("DESCRIPTOR_VERSION_OR_TARGET_UNSUPPORTED", row["issues"])

    def test_branch_specific_payload_difference_only_stales_that_variant(self) -> None:
        payload = next(p for p in identities.PAYLOADS if p.phase == "Phase30" and p.variant == "reader")
        stage = self.target.root / "ramdisk_src" / "Native"
        exe = stage / f"{payload.basename}.exe"
        changed = _synthetic_pe(9020)
        exe.write_bytes(changed)
        (stage / f"{payload.basename}.gxmi").write_bytes(_synthetic_gxmi(payload, changed))
        report = self._report()["candidates"][0]
        self.assertFalse(report["fullSetReusableCandidate"])
        self.assertEqual(report["samePairCount"], len(identities.PAYLOADS) - 1)
        changed_row = next(row for row in report["artifacts"] if row["phase"] == "Phase30" and row["variant"] == "reader")
        same_row = next(row for row in report["artifacts"] if row["phase"] == "Phase30" and row["variant"] == "success")
        self.assertEqual(changed_row["classification"], "REBUILD_VARIANT")
        self.assertEqual(same_row["classification"], "REUSABLE_CANDIDATE")

    def test_corrupt_cache_entry_is_refused_even_if_name_matches(self) -> None:
        payload = next(p for p in identities.PAYLOADS if p.phase == "Phase31" and p.variant == "success")
        record_path = bootstrap.expected_record_path(self.source.root, payload.phase, payload.record_name)
        record = json.loads(record_path.read_text(encoding="utf-8"))
        item = next(entry for entry in record["payloads"] if entry["name"] == payload.build_mode)
        build_path = Path(item["path"])
        build_path.write_bytes(_synthetic_pe(9100))
        report = self._report()["candidates"][0]
        row = next(row for row in report["sourceArtifactAudit"]["artifacts"] if row["phase"] == "Phase31" and row["variant"] == "success")
        self.assertIn("BUILD_RECORD_HASH_MISMATCH", row["issues"])
        self.assertFalse(report["fullSetReusableCandidate"])

    def test_interrupted_partial_cache_is_refused(self) -> None:
        _, descriptor, _ = self._source_paths("Phase35", "not-found")
        descriptor.unlink()
        report = self._report()["candidates"][0]
        row = next(row for row in report["sourceArtifactAudit"]["artifacts"] if row["phase"] == "Phase35" and row["variant"] == "not-found")
        self.assertIn("STAGED_DESCRIPTOR_MISSING", row["issues"])
        self.assertFalse(report["fullSetReusableCandidate"])

    def test_repeated_invocation_is_deterministic_and_read_only(self) -> None:
        before = _tree_snapshot(self.base)
        first = json.dumps(bootstrap.audit(self.target.root, [self.source.root]), sort_keys=True)
        second = json.dumps(bootstrap.audit(self.target.root, [self.source.root]), sort_keys=True)
        after = _tree_snapshot(self.base)
        self.assertEqual(first, second)
        self.assertEqual(before, after)

    def test_parallel_read_only_audits_do_not_contaminate_each_other(self) -> None:
        other_target = self.base / "target-two"
        if not other_target.exists():
            BootstrapFixture(self.base, "target-two")
        else:
            fixture = object.__new__(BootstrapFixture)
            fixture.root = other_target
            fixture.reset_artifacts()
        before = _tree_snapshot(self.base)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(lambda root: bootstrap.audit(root, [self.source.root]), [self.target.root, other_target]))
        after = _tree_snapshot(self.base)
        self.assertTrue(all(result["candidates"][0]["sourceArtifactAudit"]["verified"] for result in results))
        self.assertEqual(before, after)

    def test_unsafe_reuse_is_refused_when_identity_source_is_missing(self) -> None:
        identity_source = self.source.root / "guideXOS" / "obj" / "ManagedArtifactIdentities.g.cs"
        identity_source.unlink()
        report = self._report()["candidates"][0]
        self.assertFalse(report["sourceArtifactAudit"]["verified"])
        self.assertFalse(report["fullSetReusableCandidate"])
        self.assertTrue(any(reason.startswith("GENERATED_IDENTITY_INVALID") for reason in report["sourceArtifactAudit"]["issues"]))


if __name__ == "__main__":
    unittest.main()
