from __future__ import annotations
import hashlib
import json
import struct
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "Tools" / "ManagedArtifacts"))
import managed_artifacts as ids  # noqa: E402
import build1d_managed as build1d  # noqa: E402


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def synthetic_pe() -> bytes:
    image = bytearray(512)
    image[0:2] = b"MZ"
    struct.pack_into("<I", image, 0x3C, 0x80)
    image[0x80:0x84] = b"PE\0\0"
    struct.pack_into("<H", image, 0x84, 0x8664)
    struct.pack_into("<H", image, 0x86, 0)
    struct.pack_into("<I", image, 0x88, 0)
    struct.pack_into("<H", image, 0x94, 240)
    struct.pack_into("<H", image, 0x98, 0x20B)
    return bytes(image)


def gxmi(payload, image: bytes) -> bytes:
    data = bytearray(ids.DESCRIPTOR_HEADER_SIZE)
    flags = 0x1F | payload.proof_flags
    struct.pack_into("<IIIIII", data, 0, 0x494D5847, 1, 0x47554944, 0x8664, flags, len(image))
    struct.pack_into("<I", data, 48, 0)
    data[ids.DESCRIPTOR_DIGEST_OFFSET:ids.DESCRIPTOR_DIGEST_OFFSET + 32] = bytes.fromhex(sha(image))
    return bytes(data)


class Build1DManagedTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="build1d-synthetic-")
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def test_build_and_stage_scripts_use_shared_sha256_helper(self):
        scripts = [
            "Tools/Phase23/build_user_managed_proof.ps1",
            "Tools/Phase26/build_phase26_managed_proof.ps1",
        ]
        for phase in build1d.PHASES:
            scripts.extend((
                build1d.BUILDERS[phase],
                build1d.STAGERS[phase],
            ))
        helper = (REPO / "Tools/ManagedArtifacts/Get-ManagedSha256.ps1").read_text(encoding="utf-8")
        self.assertIn("function Get-ManagedSha256", helper)
        self.assertIn("ComputeHash($stream)", helper)
        for relative in scripts:
            with self.subTest(script=relative):
                source = (REPO / Path(relative)).read_text(encoding="utf-8")
                self.assertIn("Get-ManagedSha256", source)
                self.assertNotIn("Get-FileHash", source)

    def test_exact_variant_enumeration(self):
        report = build1d.plan()
        self.assertEqual(report["variantCount"], 41)
        self.assertEqual(report["phaseOrder"], list(build1d.PHASES))
        self.assertEqual([p["variantCount"] for p in report["phases"]], [5, 9, 5, 3, 5, 5, 9])
        variants = [v for phase in report["phases"] for v in phase["variants"]]
        self.assertEqual(len(variants), 41)
        self.assertIn("not-found", report["phases"][-1]["variants"])

    def test_phase_dependency_order(self):
        build1d.validate_order()
        with self.assertRaisesRegex(build1d.Build1DError, "invalid phase build order"):
            build1d.validate_order(reversed(build1d.PHASES))
        for phase in ("Phase32", "Phase33", "Phase34", "Phase35"):
            self.assertIn("Phase31 PAL contract and shim source",
                          build1d.plan()["phases"][build1d.PHASES.index(phase)]["dependencies"])

    def test_missing_variant_is_rejected(self):
        record = {"payloads": [{"name": "success"}]}
        with self.assertRaisesRegex(build1d.Build1DError, "missing or extra variants"):
            build1d.validate_payload_inventory("Phase29", record)

    def test_duplicate_variant_identifier_is_rejected(self):
        record = {"payloads": [{"name": "success"}, {"name": "success"}]}
        with self.assertRaisesRegex(build1d.Build1DError, "duplicate variant"):
            build1d.validate_payload_inventory("Phase29", record)

    def test_invalid_build_record_producer_is_rejected(self):
        phase = "Phase29"
        build_root = self.root / "build"
        stage = self.root / "stage"
        build_root.mkdir()
        (build_root / build1d.RECORDS[phase]).write_text(json.dumps({
            "generatedBy": "untrusted-builder", "target": "guidexos-x64",
            "targetOs": "guidexos", "payloads": [],
        }), encoding="utf-8")
        with self.assertRaisesRegex(build1d.Build1DError, "invalid build record producer"):
            build1d.verify_phase(phase, build_root, stage)

    def test_stage_mismatch_is_rejected(self):
        payload = next(p for p in ids.PAYLOADS if p.phase == "Phase29" and p.variant == "success")
        image = synthetic_pe()
        output = self.root / "build.exe"
        output.write_bytes(image)
        stage = self.root / "stage"
        stage.mkdir()
        (stage / f"{payload.basename}.exe").write_bytes(image + b"changed")
        (stage / f"{payload.basename}.gxmi").write_bytes(gxmi(payload, image))
        entry = {"path": str(output), "length": len(image), "sha256": sha(image)}
        with self.assertRaisesRegex(build1d.Build1DError, "stage differs from build output"):
            build1d.verify_pair(payload, entry, stage)

    def test_valid_synthetic_pair_passes(self):
        payload = next(p for p in ids.PAYLOADS if p.phase == "Phase29" and p.variant == "success")
        image = synthetic_pe()
        output = self.root / "build.exe"
        output.write_bytes(image)
        stage = self.root / "stage"
        stage.mkdir()
        (stage / f"{payload.basename}.exe").write_bytes(image)
        (stage / f"{payload.basename}.gxmi").write_bytes(gxmi(payload, image))
        row = build1d.verify_pair(payload, {"path": str(output), "length": len(image), "sha256": sha(image)}, stage)
        self.assertTrue(row["buildStageByteAgreement"])

    def test_checkpoint_promotion_recovers_safely(self):
        image = b"synthetic executable"
        descriptor = b"synthetic gxmi"
        source_exe, source_gxmi = self.root / "proof.exe", self.root / "proof.gxmi"
        source_exe.write_bytes(image)
        source_gxmi.write_bytes(descriptor)
        row = {
            "basename": "proof", "variant": "success",
            "buildOutputPath": str(source_exe),
            "stagedExecutablePath": str(source_exe),
            "stagedDescriptorPath": str(source_gxmi),
            "stagedExecutableSha256": sha(image),
            "stagedDescriptorSha256": sha(descriptor),
        }
        entry = {"status": "promotion_pending", "result": {"variants": [row]}}
        aggregate = self.root / "aggregate"
        build1d.recover_promotion(entry, aggregate)
        self.assertEqual(entry["status"], "completed")
        self.assertEqual((aggregate / "proof.exe").read_bytes(), image)
        self.assertEqual((aggregate / "proof.gxmi").read_bytes(), descriptor)

    def test_interrupted_build_checkpoint_is_not_resumable_as_clean(self):
        with self.assertRaisesRegex(build1d.Build1DError, "incomplete Phase30"):
            build1d.checkpoint_clean({"phases": {"Phase30": {"status": "building"}}})

    def test_verified_artifact_is_not_overwritten(self):
        source, destination = self.root / "source", self.root / "verified"
        source.write_bytes(b"new")
        destination.write_bytes(b"historical")
        with self.assertRaisesRegex(build1d.Build1DError, "refusing to replace"):
            build1d.safe_copy(destination, source, sha(b"new"))
        self.assertEqual(destination.read_bytes(), b"historical")

    def test_provenance_completeness_is_enforced(self):
        complete = {
            "phase": "Phase29", "source": {"head": "a" * 40},
            "sourceInputs": {"sha256": "b" * 64},
            "runtime": {"commit": "c" * 40},
            "runtimePackageEvidence": {"manifestSha256": "d" * 64},
            "toolchain": {"cl": {"path": "cl.exe"}, "powershell": {"path": "powershell.exe"}}, "build": {},
            "variants": [{
                "buildOutputSha256": "1" * 64, "stagedExecutableSha256": "2" * 64,
                "stagedDescriptorSha256": "3" * 64, "recordedBuildSha256": "4" * 64,
                "buildStageByteAgreement": True,
            }],
        }
        build1d.validate_provenance(complete)
        del complete["runtime"]
        with self.assertRaisesRegex(build1d.Build1DError, "incomplete provenance"):
            build1d.validate_provenance(complete)

    def test_cross_phase_output_collision_is_rejected(self):
        same = str(self.root / "same.exe")
        rows = [
            {"phase": "Phase29", "variant": "success", "buildOutputPath": same, "stagedExecutablePath": str(self.root / "a.exe")},
            {"phase": "Phase30", "variant": "success", "buildOutputPath": same, "stagedExecutablePath": str(self.root / "b.exe")},
        ]
        with self.assertRaisesRegex(build1d.Build1DError, "cross-phase output collision"):
            build1d.validate_isolation(rows)

    def test_full_set_readiness_requires_all_completed_rows(self):
        rows = []
        for payload in ids.PAYLOADS:
            rows.append({"phase": payload.phase, "variant": payload.variant})
        state = {"phaseOrder": list(build1d.PHASES), "phases": {}}
        for phase in build1d.PHASES:
            state["phases"][phase] = {
                "status": "completed",
                "result": {"variants": [r for r in rows if r["phase"] == phase]},
            }
        self.assertTrue(build1d.full_set_ready(state))
        state["phases"]["Phase35"]["result"]["variants"].pop()
        self.assertFalse(build1d.full_set_ready(state))

    def test_synthetic_fixtures_are_outside_repository(self):
        self.assertFalse(self.root.is_relative_to(REPO))


if __name__ == "__main__":
    unittest.main()
