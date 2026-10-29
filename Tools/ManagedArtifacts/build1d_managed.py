from __future__ import annotations
"""BUILD1D supported phase builds, checkpoints, provenance, and strict staging checks."""
import argparse, csv, hashlib, json, os, shutil, subprocess, sys, time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools" / "ManagedArtifacts"))
import managed_artifacts as ids  # noqa: E402
import build1c_trusted as trusted  # noqa: E402

PHASES = ("Phase29", "Phase30", "Phase31", "Phase32", "Phase33", "Phase34", "Phase35")
EXPECTED_START_HEAD = "ac92e787c9d30861869f7841340d2a790cf1eb69"
DOCS_ONLY_EXTENSION = "docs/UEFI_BUILD_INFRA_RECOVERY_20261029.md"
PHASE_DIRS = dict(zip(PHASES, (
    "phase29-managed-notification", "phase30-managed-clipboard", "phase31-managed-shell",
    "phase32-managed-open-document", "phase33-managed-shell-action",
    "phase34-managed-resource-read", "phase35-managed-persistent-read")))
RECORDS = dict(zip(PHASES, (
    "phase29-managed-notification-build.json", "phase30-managed-clipboard-build.json",
    "phase31-managed-shell-build.json", "phase32-managed-open-document-build.json",
    "phase33-managed-shell-action-build.json", "phase34-managed-resource-read-build.json",
    "phase35-managed-persistent-read-build.json")))
BUILDERS = dict(zip(PHASES, (
    "Tools/Phase29/build_phase29_managed_notification.ps1",
    "Tools/Phase30/build_phase30_managed_clipboard.ps1",
    "Tools/Phase31/build_phase31_managed_shell.ps1",
    "Tools/Phase32/build_phase32_managed_open_document.ps1",
    "Tools/Phase33/build_phase33_managed_shell_action.ps1",
    "Tools/Phase34/build_phase34_managed_resource_read.ps1",
    "Tools/Phase35/build_phase35_managed_persistent_read.ps1")))
STAGERS = {p: f"Tools/{p}/stage_{p.lower()}_image.ps1" for p in PHASES}
PROJECTS = dict(zip(PHASES, (
    "UserManagedNotificationProof", "UserManagedClipboardProof", "UserManagedShellProof",
    "UserManagedOpenDocumentProof", "UserManagedShellActionProof",
    "UserManagedResourceReadProof", "UserManagedPersistentStorageProof")))
EXTRA_DEPS = {p: (["Phase31 PAL contract and shim source"] if p in PHASES[3:] else []) for p in PHASES}
BY_PHASE = {p: tuple(x for x in ids.PAYLOADS if x.phase == p) for p in PHASES}


class Build1DError(RuntimeError): pass


def sha(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""): digest.update(block)
    return digest.hexdigest().upper()


def stable(value: Any) -> str: return json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def save(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(path.suffix + ".tmp")
    temp.write_text(stable(value), encoding="utf-8", newline="\n")
    os.replace(temp, path)


def load(path: Path) -> dict[str, Any]:
    try: value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError) as exc: raise Build1DError(f"invalid JSON evidence {path}: {exc}") from exc
    if not isinstance(value, dict): raise Build1DError(f"JSON evidence root is not an object: {path}")
    return value


def now() -> str: return datetime.now(timezone.utc).isoformat(timespec="seconds")


def inventory(phase: str) -> tuple[Any, ...]:
    if phase not in BY_PHASE: raise Build1DError(f"unknown phase: {phase}")
    return BY_PHASE[phase]


def validate_order(order: Iterable[str] = PHASES) -> None:
    if tuple(order) != PHASES: raise Build1DError(f"invalid phase build order; expected {PHASES}")


def plan() -> dict[str, Any]:
    validate_order()
    return {
        "schema": 1, "build": "BUILD1D", "branch": "BUILD_INFRA",
        "expectedStartingHead": EXPECTED_START_HEAD,
        "variantCount": len(ids.PAYLOADS), "phaseOrder": list(PHASES),
        "phases": [{
            "phase": p, "variants": [x.variant for x in inventory(p)],
            "variantCount": len(inventory(p)), "builder": BUILDERS[p], "stager": STAGERS[p],
            "project": PROJECTS[p],
            "dependencies": ["Phase 23 locked runtime source/patches/pack/private ILC",
                             "Phase 26 NativeAOT managed-proof builder"] + EXTRA_DEPS[p],
            "sourceInputs": [f"Tools/{p}", "Tools/Phase26", "Tools/Phase19", "Tools/Phase23",
                             "GuideXos.User", PROJECTS[p]] + (["Tools/Phase31"] if EXTRA_DEPS[p] else []),
            "toolchain": ["Windows PowerShell 5.1", "x64 MSVC and Windows SDK 10.0.26100.0",
                          "global .NET SDK plus pinned runtime dotnet host", "Python 3.12 and repository NASM"],
            "buildRecord": RECORDS[p],
            "buildRecordLocation": f"<buildOutputRoot>/{RECORDS[p]}",
            "buildOutputRoot": f"<E: isolated run>/runs/{p}/build" if p in PHASES[:3] else f"out/dotnet/{PHASE_DIRS[p]}",
            "stageRoot": f"<E: isolated run>/runs/{p}/stage",
            "payloads": [{
                "phase": x.phase,
                "variant": x.variant,
                "basename": x.basename,
                "expectedBuildExecutable": (
                    f"<buildOutputRoot>/payloads/{x.variant}/publish/{x.basename}.exe"
                    if p in {"Phase34", "Phase35"}
                    else f"<buildOutputRoot>/{x.variant}/publish/{x.basename}.exe"
                ),
                "buildExecutablePathSource": "payloads[].path in the supported build record",
                "expectedStagedExecutable": f"<stageRoot>/{x.basename}.exe",
                "expectedGxmiDescriptor": f"<stageRoot>/{x.basename}.gxmi",
                "proofFlags": f"0x{x.proof_flags:08X}",
                "phaseDependencies": EXTRA_DEPS[p],
            } for x in inventory(p)],
            "verification": ["supported stager and phase-specific wire/static verifier",
                             "record variant set, output SHA/length, normalized PE timestamps",
                             "GXMI header, flags, embedded executable digest, build/stage byte equality",
                             "unmodified native 41-row identity generator"],
        } for p in PHASES],
        "nativeIdentityGenerator": "Tools/ManagedArtifacts/managed_artifacts.py",
        "protectedCheckouts": [r"D:\dev\guideXOSUEFI", r"D:\dev\guideXOSUEFI_APP_MODEL",
                               r"D:\dev\guideXOSUEFI_MODERN_INPUT"],
        "notes": ["Sequential phase batches.", "Phases 32-35 enforce checkout-local out/dotnet roots.",
                  "Explicit E: stage destinations preserve historical ramdisk_src/Native payloads.",
                  "Phase 34 reclaims managed intermediates under out/dotnet but retains proof outputs and records.",
                  "No identity or release-manifest semantics are changed."]}


def git_state(root: Path = ROOT) -> dict[str, Any]:
    def get(*args: str) -> str:
        return subprocess.run(["git", "-C", str(root), *args], text=True, capture_output=True, check=True).stdout.strip()
    return {"branch": get("branch", "--show-current"), "head": get("rev-parse", "HEAD"),
            "dirtyState": get("status", "--porcelain=v1", "--untracked-files=all").splitlines()}


def assert_repo() -> dict[str, Any]:
    state = git_state()
    if state["branch"] != "BUILD_INFRA":
        raise Build1DError(f"unexpected repository baseline: {state['branch']} {state['head']}")
    if state["head"] != EXPECTED_START_HEAD:
        def git(*args: str) -> str:
            return subprocess.run(["git", "-C", str(ROOT), *args], text=True,
                                  capture_output=True, check=True).stdout.strip()
        parent = git("rev-parse", f"{state['head']}^")
        changed = git("diff-tree", "--no-commit-id", "--name-only", "-r", state["head"]).splitlines()
        if parent != EXPECTED_START_HEAD or changed != [DOCS_ONLY_EXTENSION]:
            raise Build1DError(f"unexpected repository baseline: {state['branch']} {state['head']}")
    return state


def source_files(phase: str, root: Path = ROOT) -> list[Path]:
    dirs = ["Tools/Phase19", "Tools/Phase20", "Tools/Phase21", "Tools/Phase22", "Tools/Phase23",
            "Tools/Phase26", "Tools/ManagedArtifacts", "Tools/inspect_managed_image.py",
            "Tools/Find-VcVars64.ps1", "GuideXos.User", PROJECTS[phase], f"Tools/{phase}"]
    if EXTRA_DEPS[phase]: dirs.append("Tools/Phase31")
    found: dict[str, Path] = {}
    for rel in sorted(set(dirs)):
        path = root / rel
        if path.is_file(): found[rel] = path
        elif path.is_dir():
            for child in path.rglob("*"):
                if child.is_file() and not any(x in {"__pycache__", "bin", "obj", ".git"} for x in child.parts):
                    found[child.relative_to(root).as_posix()] = child
    return [found[k] for k in sorted(found)]


def fingerprint(phase: str, root: Path = ROOT) -> dict[str, Any]:
    rows = [{"path": p.relative_to(root).as_posix(), "length": p.stat().st_size, "sha256": sha(p)}
            for p in source_files(phase, root)]
    digest = hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(",", ":")).encode()).hexdigest().upper()
    return {"fileCount": len(rows), "sha256": digest, "files": rows}


def validate_payload_inventory(phase: str, record: dict[str, Any]) -> list[dict[str, Any]]:
    entries = record.get("payloads")
    if not isinstance(entries, list) or any(not isinstance(x, dict) for x in entries):
        raise Build1DError(f"{phase}: invalid build record payload list")
    names = [str(x.get("name", "")) for x in entries]
    if len(set(names)) != len(names): raise Build1DError(f"{phase}: duplicate variant identifiers")
    expected = {x.variant for x in inventory(phase)}
    if set(names) != expected:
        raise Build1DError(f"{phase}: missing or extra variants; missing={sorted(expected-set(names))}, extra={sorted(set(names)-expected)}")
    return entries


def validate_isolation(rows: Iterable[dict[str, Any]]) -> None:
    seen, paths = set(), {}
    for row in rows:
        key = (row["phase"], row["variant"])
        if key in seen: raise Build1DError(f"duplicate variant identifier: {key}")
        seen.add(key)
        for field in ("buildOutputPath", "stagedExecutablePath"):
            path = os.path.normcase(str(Path(row[field]).resolve()))
            if path in paths: raise Build1DError(f"cross-phase output collision: {paths[path]} and {key}")
            paths[path] = str(key)


def verify_pair(payload: Any, entry: dict[str, Any], stage: Path) -> dict[str, Any]:
    import struct
    exe = Path(str(entry.get("path", "")))
    if not exe.is_file(): raise Build1DError(f"{payload.phase}/{payload.variant}: build output missing")
    image = exe.read_bytes()
    digest, length = hashlib.sha256(image).hexdigest().upper(), len(image)
    if digest != str(entry.get("sha256", "")).upper() or length != int(entry.get("length", -1)):
        raise Build1DError(f"{payload.phase}/{payload.variant}: invalid build record hash or length")
    staged, gxmi = stage / f"{payload.basename}.exe", stage / f"{payload.basename}.gxmi"
    if not staged.is_file() or not gxmi.is_file(): raise Build1DError(f"{payload.phase}/{payload.variant}: stage pair incomplete")
    if staged.read_bytes() != image: raise Build1DError(f"{payload.phase}/{payload.variant}: stage differs from build output")
    descriptor = gxmi.read_bytes()
    if len(descriptor) < ids.DESCRIPTOR_HEADER_SIZE: raise Build1DError(f"{payload.phase}/{payload.variant}: GXMI header truncated")
    magic, ver, target, machine, flags, size = struct.unpack_from("<IIIIII", descriptor, 0)
    sections = struct.unpack_from("<I", descriptor, 48)[0]
    if (magic, ver, target, machine) != (0x494D5847, 1, 0x47554944, 0x8664):
        raise Build1DError(f"{payload.phase}/{payload.variant}: GXMI header invalid")
    if len(descriptor) != ids.DESCRIPTOR_HEADER_SIZE + sections * 24 or size != length:
        raise Build1DError(f"{payload.phase}/{payload.variant}: GXMI size/length invalid")
    if (flags & 0x1F) != ids.BASE_FLAGS or (flags & ids.VARIANT_MASK) != payload.proof_flags:
        raise Build1DError(f"{payload.phase}/{payload.variant}: GXMI proof flags invalid")
    embedded = descriptor[ids.DESCRIPTOR_DIGEST_OFFSET:ids.DESCRIPTOR_DIGEST_OFFSET + 32].hex().upper()
    if embedded != digest: raise Build1DError(f"{payload.phase}/{payload.variant}: GXMI executable digest invalid")
    try: coff, debug = ids.pe_timestamp_offsets(image, exe)
    except ValueError as exc: raise Build1DError(f"{payload.phase}/{payload.variant}: invalid PE: {exc}") from exc
    timestamps = [int.from_bytes(image[o:o+4], "little") for o in [coff, *debug]]
    if any(timestamps): raise Build1DError(f"{payload.phase}/{payload.variant}: PE timestamps not normalized")
    return {"phase": payload.phase, "variant": payload.variant, "basename": payload.basename,
            "buildOutputPath": str(exe.resolve()), "buildOutputSha256": digest, "buildOutputLength": length,
            "stagedExecutablePath": str(staged.resolve()), "stagedExecutableSha256": digest,
            "stagedDescriptorPath": str(gxmi.resolve()), "stagedDescriptorSha256": hashlib.sha256(descriptor).hexdigest().upper(),
            "descriptorArtifactSha256": embedded, "descriptorFlags": f"0x{flags:08X}",
            "buildStageByteAgreement": True, "recordedBuildSha256": str(entry["sha256"]).upper(),
            "recordedBuildLength": length, "normalizedPeTimestamps": timestamps}


def verify_phase(phase: str, build_root: Path, stage: Path) -> dict[str, Any]:
    record_path = build_root / RECORDS[phase]
    if not record_path.is_file(): raise Build1DError(f"{phase}: build record missing: {record_path}")
    record = load(record_path)
    if record.get("generatedBy") != BUILDERS[phase]: raise Build1DError(f"{phase}: invalid build record producer")
    if record.get("target") != "guidexos-x64" or record.get("targetOs") != "guidexos":
        raise Build1DError(f"{phase}: invalid build target")
    entries = validate_payload_inventory(phase, record)
    by_name = {x["name"]: x for x in entries}
    rows = [verify_pair(x, by_name[x.variant], stage) for x in inventory(phase)]
    validate_isolation(rows)
    hashes = [x["buildOutputSha256"] for x in rows]
    if len(hashes) != len(set(hashes)): raise Build1DError(f"{phase}: duplicate executable hashes across variants")
    return {"phase": phase, "recordPath": str(record_path.resolve()), "recordSha256": sha(record_path),
            "generatedBy": record["generatedBy"], "buildRoot": str(build_root.resolve()),
            "stageRoot": str(stage.resolve()), "variantCount": len(rows), "variants": rows}


def validate_provenance(value: dict[str, Any]) -> None:
    needed = {"phase", "source", "sourceInputs", "runtime", "runtimePackageEvidence", "toolchain", "build", "variants"}
    missing = needed - value.keys()
    if missing: raise Build1DError(f"incomplete provenance fields: {sorted(missing)}")
    revision = value["source"].get("head", value["source"].get("revision", ""))
    if len(revision) != 40 or len(value["sourceInputs"].get("sha256", "")) != 64:
        raise Build1DError("provenance source revision/fingerprint invalid")
    if not value["runtime"].get("commit") or not value["runtimePackageEvidence"].get("manifestSha256"):
        raise Build1DError("provenance runtime identity incomplete")
    if not value["toolchain"].get("cl") or not value["toolchain"].get("powershell"):
        raise Build1DError("provenance toolchain identity incomplete")
    for row in value["variants"]:
        for key in ("buildOutputSha256", "stagedExecutableSha256", "stagedDescriptorSha256", "recordedBuildSha256"):
            digest = str(row.get(key, ""))
            if len(digest) != 64 or any(c not in "0123456789ABCDEFabcdef" for c in digest):
                raise Build1DError(f"invalid provenance hash {key}")
        if row.get("buildStageByteAgreement") is not True: raise Build1DError("provenance lacks build/stage equality")


def safe_copy(dst: Path, src: Path, digest: str) -> None:
    if dst.exists():
        if not dst.is_file() or sha(dst) != digest: raise Build1DError(f"refusing to replace different verified artifact: {dst}")
        return
    if not src.is_file() or sha(src) != digest: raise Build1DError(f"stage source changed before promotion: {src}")
    dst.parent.mkdir(parents=True, exist_ok=True)
    temp = dst.with_name(dst.name + ".building")
    shutil.copyfile(src, temp)
    if sha(temp) != digest:
        temp.unlink(missing_ok=True)
        raise Build1DError(f"aggregate stage hash mismatch: {dst}")
    os.replace(temp, dst)


def promote(result: dict[str, Any], aggregate: Path) -> None:
    for row in result["variants"]:
        exe, gxmi = Path(row["stagedExecutablePath"]), Path(row["stagedDescriptorPath"])
        safe_copy(aggregate / exe.name, exe, row["stagedExecutableSha256"])
        safe_copy(aggregate / gxmi.name, gxmi, row["stagedDescriptorSha256"])
        if (aggregate / exe.name).read_bytes() != Path(row["buildOutputPath"]).read_bytes():
            raise Build1DError(f"aggregate stage differs from build: {exe.name}")


def recover_promotion(entry: dict[str, Any], aggregate: Path) -> None:
    if entry.get("status") not in {"promotion_pending", "promoting"}:
        raise Build1DError("checkpoint phase is not at a recoverable promotion boundary")
    promote(entry["result"], aggregate)
    entry["status"], entry["completedAtUtc"] = "completed", now()


def checkpoint_clean(state: dict[str, Any]) -> None:
    for phase, entry in state.get("phases", {}).items():
        if entry.get("status") in {"building", "interrupted"}:
            raise Build1DError(f"checkpoint has incomplete {phase}: {entry['status']}")


def full_set_ready(state: dict[str, Any]) -> bool:
    if tuple(state.get("phaseOrder", ())) != PHASES: return False
    if any(state.get("phases", {}).get(p, {}).get("status") != "completed" for p in PHASES): return False
    rows = [r for p in PHASES for r in state["phases"][p]["result"]["variants"]]
    return len(rows) == len(ids.PAYLOADS) and {(r["phase"], r["variant"]) for r in rows} == {(x.phase, x.variant) for x in ids.PAYLOADS}


def source_provenance(phase: str, result: dict[str, Any], source: dict[str, Any],
                      fp: dict[str, Any], runtime: dict[str, Any], pack: Path) -> dict[str, Any]:
    value = {"schema": 1, "recordType": "BUILD1D supplementary phase provenance",
             "identityAuthority": "Tools/ManagedArtifacts/managed_artifacts.py", "phase": phase,
             "source": source, "sourceInputs": fp,
             "runtime": {"commit": runtime["runtime"]["commit"],
                         "lockedTrackedFileCount": runtime["runtime"]["lockedTrackedFileCount"],
                         "locks": runtime["runtime"]["locks"], "patchRecords": runtime["runtime"]["patchRecords"],
                         "runtimePackManifestSha256": runtime["manifestSha256"],
                         "privateIlc": runtime["package"]["privateIlc"]},
             "runtimePackageEvidence": {"path": str(runtime["evidencePath"]),
                                        "sha256": runtime["evidenceSha256"],
                                        "manifestSha256": runtime["manifestSha256"],
                                        "packages": runtime["package"]["packages"],
                                        "archiveHashesAreSupplementaryMeasurements": True},
             "toolchain": runtime["toolchain"],
             "build": {"builder": BUILDERS[phase], "builderSha256": sha(ROOT / BUILDERS[phase]),
                       "stager": STAGERS[phase], "stagerSha256": sha(ROOT / STAGERS[phase]),
                       "phase26BuilderSha256": sha(ROOT / "Tools/Phase26/build_phase26_managed_proof.ps1"),
                       "phase23Pack": str(pack), "recordPath": result["recordPath"],
                       "recordSha256": result["recordSha256"], "buildRoot": result["buildRoot"],
                       "stageRoot": result["stageRoot"]},
             "variants": result["variants"],
             "limitations": ["Supplementary evidence does not replace the native generator.",
                             "Package archive hashes are measured content identities, not external feed signatures."]}
    validate_provenance(value)
    return value


def runtime_info(pack: Path, evidence: Path) -> dict[str, Any]:
    runtime = trusted.validate_runtime_tree(ROOT)
    package = trusted.validate_package_bundle(pack, evidence, runtime["commit"])
    tools = trusted.discover_toolchain(ROOT)
    trusted.validate_toolchain(tools)
    return {"runtime": runtime, "package": package, "toolchain": tools,
            "evidencePath": str(evidence.resolve()), "evidenceSha256": sha(evidence),
            "manifestSha256": sha(pack / "guidexos-runtime-pack.manifest.json")}


def recover_failed_batch(state: dict[str, Any], scratch: Path, aggregate: Path,
                         runtime: dict[str, Any], pack: Path) -> None:
    failed = [(phase, entry) for phase, entry in state.get("phases", {}).items()
              if entry.get("status") == "failed"]
    if not failed: return
    if len(failed) != 1: raise Build1DError("checkpoint has multiple failed batches")
    phase, entry = failed[0]
    index = PHASES.index(phase)
    if any(state.get("phases", {}).get(p, {}).get("status") != "completed" for p in PHASES[:index]):
        raise Build1DError(f"cannot recover {phase} before earlier batches are complete")
    if any(p in state.get("phases", {}) for p in PHASES[index + 1:]):
        raise Build1DError(f"cannot recover {phase} after a later batch has started")

    source = entry.get("source", {})
    current_source = git_state()
    if source != current_source:
        raise Build1DError(f"{phase} source checkout state changed since the failed batch")
    if entry.get("buildRoot") != str(output_root(phase, scratch)):
        raise Build1DError(f"{phase} checkpoint build root is not the expected isolated root")
    if entry.get("stageRoot") != str((scratch / "runs" / phase / "stage")):
        raise Build1DError(f"{phase} checkpoint stage root is not the expected isolated root")
    before = entry.get("sourceInputsBefore", {})
    current_fp = fingerprint(phase)
    before_rows = {r["path"]: r for r in before.get("files", [])}
    current_rows = {r["path"]: r for r in current_fp["files"]}
    if set(before_rows) != set(current_rows):
        raise Build1DError(f"{phase} source file inventory changed since the failed batch")
    changed = [path for path in before_rows if before_rows[path] != current_rows[path]]
    if changed != ["Tools/ManagedArtifacts/build1d_managed.py"]:
        raise Build1DError(f"{phase} source inputs changed since the failed batch: {changed}")

    old_runtime = state.get("runtimeIdentity", {})
    if (old_runtime.get("manifestSha256") != runtime.get("manifestSha256") or
            old_runtime.get("evidenceSha256") != runtime.get("evidenceSha256") or
            old_runtime.get("runtime", {}).get("commit") != runtime.get("runtime", {}).get("commit") or
            old_runtime.get("toolchain") != runtime.get("toolchain")):
        raise Build1DError(f"{phase} runtime or toolchain identity changed since the failed batch")
    if entry.get("preflight", {}).get("runtimePackManifestSha256") != runtime.get("manifestSha256"):
        raise Build1DError(f"{phase} preflight runtime manifest changed since the failed batch")

    result = verify_phase(phase, Path(entry["buildRoot"]), Path(entry["stageRoot"]))
    provenance = source_provenance(phase, result, source, before, old_runtime, pack)
    prov_path = scratch / f"provenance/{phase}-provenance.json"
    save(prov_path, provenance)
    prior_error = entry.pop("error", "")
    entry.update({"status": "promotion_pending", "result": result,
                  "provenancePath": str(prov_path.resolve()), "provenanceSha256": sha(prov_path),
                  "buildLog": str((scratch / f"logs/{phase}-build.log").resolve()),
                  "stageLog": str((scratch / f"logs/{phase}-stage.log").resolve()),
                  "recoveredAtUtc": now(), "recoveredAfterError": prior_error})
    if phase == "Phase29":
        ref = Path(r"E:\guideXOSUEFI_BUILD_INFRA_BUILD1C\runs\phase29-success-verified\stage\guideXOS.Phase29ManagedNotificationProof.exe")
        success = next(r for r in result["variants"] if r["variant"] == "success")
        entry["build1cSuccessReferenceMatch"] = sha(ref) == success["buildOutputSha256"] if ref.is_file() else None
    save(scratch / "checkpoints/BUILD1D.checkpoint.json", state)
    entry["status"] = "promoting"
    save(scratch / "checkpoints/BUILD1D.checkpoint.json", state)
    promote(result, aggregate)
    entry["status"], entry["completedAtUtc"] = "completed", now()
    save(scratch / "checkpoints/BUILD1D.checkpoint.json", state)
    revalidate_completed(state, aggregate)


def revalidate_completed(state: dict[str, Any], aggregate: Path) -> None:
    for phase in PHASES:
        entry = state.get("phases", {}).get(phase)
        if not entry or entry.get("status") != "completed": continue
        result = entry["result"]
        current = verify_phase(phase, Path(result["buildRoot"]), Path(result["stageRoot"]))
        before = {r["variant"]: (r["buildOutputSha256"], r["stagedDescriptorSha256"]) for r in result["variants"]}
        after = {r["variant"]: (r["buildOutputSha256"], r["stagedDescriptorSha256"]) for r in current["variants"]}
        if before != after: raise Build1DError(f"previous {phase} outputs changed after a later build")
        for row in result["variants"]:
            for ext, field in (("exe", "stagedExecutableSha256"), ("gxmi", "stagedDescriptorSha256")):
                path = aggregate / f"{row['basename']}.{ext}"
                if not path.is_file() or sha(path) != row[field]: raise Build1DError(f"previous aggregate artifact changed: {path}")


def run_live(args: list[str], logfile: Path) -> None:
    logfile.parent.mkdir(parents=True, exist_ok=True)
    with logfile.open("w", encoding="utf-8", newline="\n") as log:
        proc = subprocess.Popen(args, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, encoding="utf-8", errors="replace", bufsize=1)
        assert proc.stdout is not None
        for line in proc.stdout:
            print(line, end="", flush=True)
            log.write(line)
        code = proc.wait()
    if code: raise Build1DError(f"command failed ({code}); see {logfile}: {' '.join(args)}")


def build_cmd(phase: str, pack: Path, output: Path, power: Path) -> list[str]:
    args = [str(power), "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            str(ROOT / BUILDERS[phase]), "-Phase23Pack", str(pack), "-OutputRoot", str(output)]
    if phase == "Phase29": args += ["-Variant", "all"]
    return args


def stage_cmd(phase: str, build: Path, stage: Path, power: Path) -> list[str]:
    return [str(power), "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            str(ROOT / STAGERS[phase]), "-BuildRoot", str(build), "-RamdiskSource", str(stage)]


def output_root(phase: str, scratch: Path) -> Path:
    return scratch / "runs" / phase / "build" if phase in PHASES[:3] else ROOT / "out" / "dotnet" / PHASE_DIRS[phase]


def copy_record(state: dict[str, Any], phase: str, audit: Path) -> None:
    src = Path(state["phases"][phase]["result"]["recordPath"])
    dst = audit / "out" / "dotnet" / PHASE_DIRS[phase] / RECORDS[phase]
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.exists(): raise Build1DError(f"native audit record collision: {dst}")
    shutil.copyfile(src, dst)
    if sha(src) != sha(dst): raise Build1DError(f"native audit record copy changed: {phase}")


def native_validate(state: dict[str, Any], scratch: Path, aggregate: Path) -> dict[str, Any]:
    audit = scratch / f"native-audit-root-{int(time.time())}"
    audit.mkdir(parents=True)
    for phase in PHASES: copy_record(state, phase, audit)
    expected = {f"{x.basename}.{ext}" for x in ids.PAYLOADS for ext in ("exe", "gxmi")}
    found = {p.name for p in aggregate.iterdir() if p.is_file()}
    if found != expected: raise Build1DError(f"stage inventory mismatch: missing={sorted(expected-found)} extra={sorted(found-expected)}")
    gen = ROOT / "Tools/ManagedArtifacts/managed_artifacts.py"
    source, repeat = scratch / "ManagedArtifactIdentities.BUILD1D.g.cs", scratch / "ManagedArtifactIdentities.BUILD1D.repeat.g.cs"
    csv1, csv2, csv3 = scratch / "reports/native.csv", scratch / "reports/native-repeat.csv", scratch / "reports/generated-audit.csv"
    base = [sys.executable, "-B", str(gen), "--root", str(audit), "--ramdisk-source", str(aggregate)]
    def call(extra: list[str], log: Path) -> None:
        proc = subprocess.run(base + extra, cwd=ROOT, text=True, capture_output=True, encoding="utf-8", errors="replace")
        log.parent.mkdir(parents=True, exist_ok=True)
        log.write_text(proc.stdout + proc.stderr, encoding="utf-8", newline="\n")
        print(proc.stdout, end="")
        if proc.returncode: raise Build1DError(f"native generator failed {proc.returncode}; {log}: {proc.stderr[-1000:]}")
    call(["--identity-source", str(source), "--report-csv", str(csv1), "--stage", "before"], scratch / "logs/native-generator.log")
    call(["--identity-source", str(repeat), "--report-csv", str(csv2), "--stage", "before"], scratch / "logs/native-generator-repeat.log")
    if source.read_bytes() != repeat.read_bytes(): raise Build1DError("native generated source is not stable for identical inputs")
    call(["--identity-source", str(source), "--report-csv", str(csv3), "--stage", "generated", "--audit-only", "--kernel-built"],
         scratch / "logs/generated-source-audit.log")
    with csv3.open("r", encoding="utf-8", newline="") as stream: rows = list(csv.DictReader(stream))
    if len(rows) != 41 or any(r.get("allowlist_equals_executable") != "true" for r in rows):
        raise Build1DError("generated identity source failed native 41-row hash audit")
    return {"status": "PASS", "variantCount": 41, "descriptorAgreement": "41/41",
            "buildStageAgreement": "41/41", "generator": str(gen.resolve()), "generatorSha256": sha(gen),
            "auditRoot": str(audit.resolve()), "aggregateStage": str(aggregate.resolve()),
            "identitySource": str(source.resolve()), "identitySourceSha256": sha(source),
            "identitySourceStable": True, "generatorLog": str((scratch / "logs/native-generator.log").resolve()),
            "nativeCsv": str(csv1.resolve()), "generatedSourceAuditCsv": str(csv3.resolve())}


def build_all(scratch: Path, pack: Path, evidence: Path) -> dict[str, Any]:
    scratch, pack, evidence = scratch.resolve(), pack.resolve(), evidence.resolve()
    scratch.mkdir(parents=True, exist_ok=True)
    save(scratch / "BUILD1D_BUILD_PLAN.json", plan())
    runtime = runtime_info(pack, evidence)
    assert_repo()
    state_path = scratch / "checkpoints/BUILD1D.checkpoint.json"
    if state_path.exists():
        state = load(state_path)
        checkpoint_clean(state)
        if state.get("repository", {}).get("head") != git_state()["head"]:
            raise Build1DError("checkpoint and source HEAD differ")
    else:
        state = {"schema": 1, "build": "BUILD1D", "createdAtUtc": now(), "repository": git_state(),
                 "phaseOrder": list(PHASES), "phase23Pack": str(pack), "packageEvidence": str(evidence),
                 "runtimeIdentity": runtime, "phases": {}}
        save(state_path, state)
    aggregate = scratch / "stage/Native"
    aggregate.mkdir(parents=True, exist_ok=True)
    recover_failed_batch(state, scratch, aggregate, runtime, pack)
    for phase in PHASES:
        old = state["phases"].get(phase)
        if old and old.get("status") in {"promotion_pending", "promoting"}:
            recover_promotion(old, aggregate)
            save(state_path, state)
            continue
        if old and old.get("status") == "completed":
            revalidate_completed(state, aggregate)
            continue
        if old: raise Build1DError(f"refusing to overwrite incomplete {phase} checkpoint: {old.get('status')}")
        if any(state["phases"].get(p, {}).get("status") != "completed" for p in PHASES[:PHASES.index(phase)]):
            raise Build1DError(f"{phase} cannot start before prior batches")
        revalidate_completed(state, aggregate)
        out = output_root(phase, scratch)
        run_root, stage = scratch / "runs" / phase, scratch / "runs" / phase / "stage"
        if out.exists() or stage.exists(): raise Build1DError(f"refusing stale build/stage output: {out if out.exists() else stage}")
        preflight = trusted.preflight(ROOT, scratch / "preflight" / f"{phase}-{int(time.time())}", pack, evidence, "success")
        if not preflight.get("ready"): raise Build1DError(f"{phase} trusted preflight failed")
        source, before = git_state(), fingerprint(phase)
        entry = {"phase": phase, "status": "building", "startedAtUtc": now(), "source": source,
                 "sourceInputsBefore": before, "buildRoot": str(out), "stageRoot": str(stage),
                 "preflight": {"runtimeCommit": preflight["runtime"]["commit"],
                               "runtimePackManifestSha256": preflight["runtimePack"]["manifest"]["sha256"],
                               "toolchain": preflight["toolchain"]}}
        state["phases"][phase] = entry
        save(state_path, state)
        try:
            run_root.mkdir(parents=True, exist_ok=True)
            power = Path(runtime["toolchain"]["powershell"]["path"])
            run_live(build_cmd(phase, pack, out, power), scratch / f"logs/{phase}-build.log")
            run_live(stage_cmd(phase, out, stage, power), scratch / f"logs/{phase}-stage.log")
            result = verify_phase(phase, out, stage)
            after, after_fp = git_state(), fingerprint(phase)
            if after["head"] != source["head"] or after_fp["sha256"] != before["sha256"]:
                raise Build1DError(f"{phase} source fingerprint changed during the batch")
            provenance = source_provenance(phase, result, source, before, runtime, pack)
            prov_path = scratch / f"provenance/{phase}-provenance.json"
            save(prov_path, provenance)
            entry.update({"status": "promotion_pending", "sourceInputsAfter": after_fp, "result": result,
                          "provenancePath": str(prov_path.resolve()), "provenanceSha256": sha(prov_path),
                          "buildLog": str((scratch / f"logs/{phase}-build.log").resolve()),
                          "stageLog": str((scratch / f"logs/{phase}-stage.log").resolve())})
            if phase == "Phase29":
                ref = Path(r"E:\guideXOSUEFI_BUILD_INFRA_BUILD1C\runs\phase29-success-verified\stage\guideXOS.Phase29ManagedNotificationProof.exe")
                success = next(r for r in result["variants"] if r["variant"] == "success")
                entry["build1cSuccessReferenceMatch"] = sha(ref) == success["buildOutputSha256"] if ref.is_file() else None
            save(state_path, state)
            entry["status"] = "promoting"
            save(state_path, state)
            promote(result, aggregate)
            entry["status"], entry["completedAtUtc"] = "completed", now()
            save(state_path, state)
            revalidate_completed(state, aggregate)
        except KeyboardInterrupt:
            entry["status"], entry["interruptedAtUtc"] = "interrupted", now()
            save(state_path, state)
            raise
        except Exception as exc:
            entry["status"], entry["failedAtUtc"], entry["error"] = "failed", now(), str(exc)
            save(state_path, state)
            raise
    if not full_set_ready(state): raise Build1DError("checkpoint does not contain exactly 41 variants")
    revalidate_completed(state, aggregate)
    state["fullNativeValidation"] = native_validate(state, scratch, aggregate)
    state["completedAtUtc"] = now()
    save(state_path, state)
    save(scratch / "BUILD1D_COMPLETE_MANAGED_PROOF_SET.json", {
        "outcome": "BUILD1D_FULL_PROOF_VERIFIED", "startingHead": EXPECTED_START_HEAD,
        "buildSourceHead": state["repository"]["head"],
        "endingHead": git_state()["head"], "branch": git_state()["branch"],
        "completedPhases": list(PHASES), "verifiedVariants": 41, "phases": state["phases"],
        "nativeIdentityGenerator": state["fullNativeValidation"], "evidenceRoot": str(scratch), "pushed": False})
    return state


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subs = parser.add_subparsers(dest="mode", required=True)
    p = subs.add_parser("plan")
    p.add_argument("--write-plan", type=Path)
    b = subs.add_parser("build")
    b.add_argument("--scratch", type=Path, required=True)
    b.add_argument("--phase23-pack", type=Path, required=True)
    b.add_argument("--package-evidence", type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.mode == "plan":
            result = plan()
            if args.write_plan: save(args.write_plan, result)
            print(stable(result), end="")
        else:
            result = build_all(args.scratch, args.phase23_pack, args.package_evidence)
            print(stable({"outcome": "BUILD1D_FULL_PROOF_VERIFIED", "verifiedVariantCount": 41,
                          "checkpoint": str((args.scratch / "checkpoints/BUILD1D.checkpoint.json").resolve()),
                          "report": str((args.scratch / "BUILD1D_COMPLETE_MANAGED_PROOF_SET.json").resolve())}), end="")
        return 0
    except KeyboardInterrupt:
        print("BUILD1D interrupted; checkpoint retains active phase state.", file=sys.stderr)
        return 130
    except (Build1DError, trusted.Build1CError, OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as exc:
        print(f"BUILD1D_ERROR: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
