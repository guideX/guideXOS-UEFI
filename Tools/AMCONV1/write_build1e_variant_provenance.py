#!/usr/bin/env python3
"""Write a hash-linked BUILD1E provenance manifest for Phase 29-35 variants."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def git_output(repo: Path, *args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=repo, text=True).strip()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--evidence-root", type=Path, required=True)
    args = parser.parse_args()
    repo = args.repo_root.resolve()
    evidence = args.evidence_root.resolve()
    provenance = evidence / "provenance"
    build_record_copy_root = provenance / "build-records"
    build_record_copy_root.mkdir(parents=True, exist_ok=True)

    identity_manifest = json.loads(
        (provenance / "managed-identity-input.json").read_text(encoding="utf-8-sig")
    )
    head = git_output(repo, "rev-parse", "HEAD")
    branch = git_output(repo, "branch", "--show-current")
    if branch != identity_manifest["branch"] or head != identity_manifest["head"]:
        raise SystemExit("Repository branch/HEAD differ from the recorded artifact build baseline")

    inventory = list(csv.DictReader(
        (evidence / "reports" / "managed-artifacts-generated.csv").open(
            newline="", encoding="utf-8-sig"
        )
    ))
    inventory_by_variant = {(row["phase"], row["variant"]): row for row in inventory}
    if len(inventory) != 41 or len(inventory_by_variant) != 41:
        raise SystemExit("Expected 41 unique native-generator rows")

    tracked_paths = [
        item.decode("utf-8")
        for item in subprocess.check_output(
            ["git", "ls-files", "-z"], cwd=repo
        ).split(b"\0")
        if item
    ]
    common_inputs = [
        "Tools/Phase19/",
        "Tools/Phase23/",
        "Tools/Phase26/",
        "Tools/ManagedArtifacts/",
        "GuideXos.User/",
    ]
    root_inputs = {
        "build.ps1",
        "global.json",
        "NuGet.Config",
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
    }

    checkpoint_dir = provenance
    build_records: dict[str, tuple[dict, Path, dict]] = {}
    phase_inputs: dict[str, dict] = {}
    runtime_commit = git_output(repo / "out" / "rt", "rev-parse", "HEAD")
    runtime_dotnet = repo / "out" / "rt" / ".dotnet" / "dotnet.exe"
    managed_dotnet = Path.home() / ".dotnet" / "dotnet.exe"
    runtime_sdk_version = subprocess.check_output(
        [str(runtime_dotnet), "--version"], text=True
    ).strip()
    managed_sdk_version = subprocess.check_output(
        [str(managed_dotnet), "--version"], text=True
    ).strip()
    csc_dll = managed_dotnet.parent / "sdk" / managed_sdk_version / "Roslyn" / "bincore" / "csc.dll"
    roslyn_version = subprocess.check_output(
        [str(managed_dotnet), str(csc_dll), "-version"], text=True
    ).strip()
    private_ilc_record = repo / "out" / "dotnet" / "phase23-private-ilc" / "private-ilc-build.json"
    phase23_manifest = repo / "out" / "dotnet" / "phase23-runtime-pack" / "guidexos-runtime-pack.manifest.json"
    phase23_package = repo / "out" / "dotnet" / "phase23-runtime-pack" / "packages" / "runtime.guidexos-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg"
    private_ilc = json.loads(private_ilc_record.read_text(encoding="utf-8-sig"))
    private_ilc_identity = {
        "path": str(private_ilc_record),
        "sha256": sha256(private_ilc_record),
        "privateCompiler": private_ilc.get("privateCompiler"),
        "hostJitInterface": private_ilc.get("hostJitInterface"),
    }
    phase23_runtime_identity = {
        "runtimeSourceCommit": runtime_commit,
        "packManifest": {"path": str(phase23_manifest), "sha256": sha256(phase23_manifest)},
        "runtimePackage": {"path": str(phase23_package), "sha256": sha256(phase23_package)},
        "privateIlcBuildRecord": private_ilc_identity,
    }

    for number in range(29, 36):
        phase = f"Phase{number}"
        checkpoint_path = checkpoint_dir / f"phase{number}-checkpoint.json"
        checkpoint = json.loads(checkpoint_path.read_text(encoding="utf-8-sig"))
        record_ref = checkpoint.get("generatorBuildRecord") or checkpoint.get("buildRecord")
        if not record_ref:
            raise SystemExit(f"{phase} checkpoint does not identify its native-generator build record")
        record_path = Path(record_ref["path"])
        if not record_path.is_file() or sha256(record_path) != record_ref["sha256"]:
            raise SystemExit(f"{phase} build record fails checkpoint path/hash verification")
        record_copy = build_record_copy_root / f"{phase.lower()}-build-record.json"
        shutil.copyfile(record_path, record_copy)
        record_copy_hash = sha256(record_copy)
        if record_copy_hash != record_ref["sha256"]:
            raise SystemExit(f"{phase} evidence copy differs from the source build record")
        record = json.loads(record_path.read_text(encoding="utf-8-sig"))
        payloads = record.get("payloads", [])
        expected_count = sum(1 for key in inventory_by_variant if key[0] == phase)
        if len(payloads) != expected_count:
            raise SystemExit(f"{phase} record payload count does not match native generator inventory")

        project_dirs = {
            Path(payload["project"]).resolve().parent.relative_to(repo).as_posix()
            for payload in payloads
        }
        prefixes = common_inputs + [f"Tools/Phase{number}/"]
        selected = [
            path for path in tracked_paths
            if path in root_inputs
            or any(path.startswith(prefix) for prefix in prefixes)
            or any(path.startswith(project_dir + "/") or path == project_dir for project_dir in project_dirs)
        ]
        selected = sorted(
            path for path in selected
            if "__pycache__" not in path and not path.endswith(".pyc")
        )
        source_files = []
        for relative in selected:
            path = repo / Path(relative)
            if not path.is_file():
                raise SystemExit(f"Tracked source input is missing: {path}")
            source_files.append({"path": relative.replace("\\", "/"), "sha256": sha256(path)})
        canonical = json.dumps(source_files, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
        fingerprint = hashlib.sha256(canonical).hexdigest().upper()
        phase_inputs[phase] = {
            "sourceRevision": head,
            "sourceInputFingerprint": fingerprint,
            "sourceInputFileCount": len(source_files),
            "sourceRoots": prefixes + sorted(project_dirs),
            "sourceFiles": source_files,
        }
        build_records[phase] = (record, record_copy, checkpoint)

    variants = []
    for phase, (record, record_copy, checkpoint) in build_records.items():
        for payload in record["payloads"]:
            row = inventory_by_variant[(phase, payload["name"])]
            if row["build_equals_staged"] != "true" or row["descriptor_equals_executable"] != "true":
                raise SystemExit(f"Native generator did not accept build/stage identity for {phase}/{payload['name']}")
            output = Path(payload["path"])
            phase26_path = output.parents[1] / "phase26-managed-proof-build.json"
            user_build_path = output.parents[1] / "user-managed-proof-build.json"
            phase26 = json.loads(phase26_path.read_text(encoding="utf-8-sig"))
            user_build = json.loads(user_build_path.read_text(encoding="utf-8-sig"))
            runtime_manifest = Path(user_build["packManifest"])
            source_project = Path(payload["project"]).resolve()
            log_path = evidence / "logs" / f"{phase}-build-stage.log"
            log_text = log_path.read_text(encoding="utf-8", errors="replace")
            compiler_lines = sorted(set(
                line.split("=", 1)[1].strip()
                for line in log_text.splitlines()
                if line.startswith("compiler=")
            ))
            compiler_versions = sorted(set(
                line.split("=", 1)[1].strip()
                for line in log_text.splitlines()
                if line.startswith("compilerVersion=")
            ))
            sdk_lines = sorted(set(
                line.split("=", 1)[1].strip()
                for line in log_text.splitlines()
                if line.startswith("windowsSdkVersion=")
            ))
            staged_exe = Path(row["executable_path"])
            descriptor = Path(row["descriptor_path"])
            if sha256(output) != row["raw_sha256"] or sha256(staged_exe) != row["raw_sha256"]:
                raise SystemExit(f"Executable hash changed for {phase}/{payload['name']}")
            if sha256(descriptor) != row["descriptor_file_sha256"]:
                raise SystemExit(f"Descriptor hash changed for {phase}/{payload['name']}")

            variants.append({
                "phase": phase,
                "variant": payload["name"],
                "mode": payload.get("mode"),
                "branch": branch,
                "sourceRevision": head,
                "sourceInputFingerprint": phase_inputs[phase]["sourceInputFingerprint"],
                "sourceInputFileCount": phase_inputs[phase]["sourceInputFileCount"],
                "sourceProject": str(source_project),
                "builder": record.get("generatedBy"),
                "stager": f"Tools/{phase}/stage_{phase.lower()}_image.ps1",
                "target": phase26.get("target"),
                "targetOs": phase26.get("targetOs"),
                "managedExecution": phase26.get("managedExecution"),
                "managedBuildOutput": {
                    "path": str(output),
                    "length": payload["length"],
                    "sha256": row["raw_sha256"],
                },
                "stagedExecutable": {
                    "path": str(staged_exe),
                    "length": int(row["executable_bytes"]),
                    "sha256": row["raw_sha256"],
                    "buildEqualsStaged": row["build_equals_staged"] == "true",
                },
                "gxmi": {
                    "path": str(descriptor),
                    "sha256": row["descriptor_file_sha256"],
                    "flags": row["descriptor_flags"],
                    "descriptorArtifactSha256": row["descriptor_artifact_sha256"],
                    "descriptorEqualsExecutable": row["descriptor_equals_executable"] == "true",
                },
                "buildRecords": {
                    "phaseBuildRecord": {
                        "sourcePath": str(record_copy),
                        "sha256": sha256(record_copy),
                    },
                    "phase26Record": {
                        "path": str(phase26_path),
                        "sha256": sha256(phase26_path),
                        "phase23ArtifactIdentity": phase26.get("inheritedPhase23Artifact"),
                    },
                    "userManagedRecord": {
                        "path": str(user_build_path),
                        "sha256": sha256(user_build_path),
                    },
                },
                "runtimeInputs": {
                    "runtimeSourceRoot": str(repo / "out" / "rt"),
                    "runtimeSourceRevision": runtime_commit,
                    "phase23PackRoot": str(repo / "out" / "dotnet" / "phase23-runtime-pack"),
                    "adaptedRuntimePackManifest": {
                        "path": str(runtime_manifest),
                        "available": runtime_manifest.is_file(),
                        "sha256": sha256(runtime_manifest) if runtime_manifest.is_file() else None,
                    },
                    "phase23SourcePackManifest": phase23_runtime_identity["packManifest"],
                    "phase23RuntimePackage": phase23_runtime_identity["runtimePackage"],
                    "phase23ArtifactIdentity": phase26.get("inheritedPhase23Artifact"),
                    "privateIlcBuildRecord": private_ilc_identity,
                    "ilCompilerPackage": "Microsoft.DotNet.ILCompiler 9.0.0",
                    "coreLibAdaptation": {
                        **phase26.get("coreLibAdaptation", {}),
                        "available": Path(
                            phase26.get("coreLibAdaptation", {}).get("assembly", "")
                        ).is_file(),
                    },
                },
                "toolchainInputs": {
                    "dotnetSdkPath": str(managed_dotnet),
                    "dotnetSdk": managed_sdk_version,
                    "roslynCompiler": roslyn_version,
                    "runtimeBuildDotnetPath": str(runtime_dotnet),
                    "runtimeBuildSdk": runtime_sdk_version,
                    "nativeCompiler": compiler_lines,
                    "nativeCompilerVersions": compiler_versions,
                    "windowsSdkVersion": sdk_lines,
                    "linker": user_build.get("linker"),
                    "libraryCreator": user_build.get("libraryCreator"),
                },
                "phaseBuildStageLog": str(log_path),
            })

    if len(variants) != 41:
        raise SystemExit(f"Expected 41 variant entries, wrote {len(variants)}")
    manifest = {
        "schema": 1,
        "build": "BUILD1E",
        "branch": branch,
        "sourceRevision": head,
        "artifactInputManifest": str(provenance / "managed-identity-input.json"),
        "variantCount": len(variants),
        "runtimeInputs": phase23_runtime_identity,
        "runtimeSourceRevision": runtime_commit,
        "toolchain": {
            "managedDotnet": {"path": str(managed_dotnet), "version": managed_sdk_version},
            "roslyn": roslyn_version,
            "runtimeDotnet": {"path": str(runtime_dotnet), "version": runtime_sdk_version},
            "nativeCompiler": "MSVC x64 (version recorded per phase log)",
            "windowsSdk": "10.0.26100.0",
        },
        "phaseSourceInputs": phase_inputs,
        "variants": variants,
    }
    output = provenance / "variant-provenance.json"
    output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    summary = provenance / "variant-provenance.csv"
    fields = [
        "phase", "variant", "mode", "branch", "sourceRevision",
        "sourceInputFingerprint", "managedBuildSha256", "stagedSha256",
        "gxmiSha256", "buildEqualsStaged", "descriptorEqualsExecutable",
    ]
    with summary.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        for item in variants:
            writer.writerow({
                "phase": item["phase"],
                "variant": item["variant"],
                "mode": item["mode"],
                "branch": item["branch"],
                "sourceRevision": item["sourceRevision"],
                "sourceInputFingerprint": item["sourceInputFingerprint"],
                "managedBuildSha256": item["managedBuildOutput"]["sha256"],
                "stagedSha256": item["stagedExecutable"]["sha256"],
                "gxmiSha256": item["gxmi"]["sha256"],
                "buildEqualsStaged": item["stagedExecutable"]["buildEqualsStaged"],
                "descriptorEqualsExecutable": item["gxmi"]["descriptorEqualsExecutable"],
            })
    print(f"VARIANT_PROVENANCE={len(variants)}/{len(variants)}")
    print(f"SOURCE_FINGERPRINTS={len(phase_inputs)} phases")
    print(f"PROVENANCE_JSON={output}")
    print(f"PROVENANCE_CSV={summary}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
