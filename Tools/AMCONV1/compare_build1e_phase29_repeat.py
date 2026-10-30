#!/usr/bin/env python3
"""Check repeat Phase 29 build/stage identity and compare it with the first run."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


STAGED_NAMES = {
    "success": "guideXOS.Phase29ManagedNotificationProof",
    "title-failure": "guideXOS.Phase29NotificationTitleFailureProof",
    "body-failure": "guideXOS.Phase29NotificationBodyFailureProof",
    "invalid-type": "guideXOS.Phase29NotificationInvalidTypeProof",
    "failfast": "guideXOS.Phase29NotificationFailFastProof",
}


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def index(record: dict) -> dict[str, dict]:
    payloads = record.get("payloads", [])
    result = {payload["name"]: payload for payload in payloads}
    if len(result) != len(payloads) or set(result) != set(STAGED_NAMES):
        raise SystemExit("Each Phase 29 build record must contain the five expected variants")
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--original-build-record", type=Path, required=True)
    parser.add_argument("--repeat-build-record", type=Path, required=True)
    parser.add_argument("--original-stage", type=Path, required=True)
    parser.add_argument("--repeat-stage", type=Path, required=True)
    parser.add_argument("--output-json", type=Path, required=True)
    args = parser.parse_args()

    original = index(load(args.original_build_record))
    repeat = index(load(args.repeat_build_record))
    comparisons = []
    for name, stem in STAGED_NAMES.items():
        original_build = Path(original[name]["path"])
        repeat_build = Path(repeat[name]["path"])
        original_stage = args.original_stage / f"{stem}.exe"
        repeat_stage = args.repeat_stage / f"{stem}.exe"
        original_gxmi = args.original_stage / f"{stem}.gxmi"
        repeat_gxmi = args.repeat_stage / f"{stem}.gxmi"
        paths = (original_build, repeat_build, original_stage, repeat_stage, original_gxmi, repeat_gxmi)
        if not all(path.is_file() for path in paths):
            raise SystemExit(f"Missing repeat/build/stage evidence for Phase29/{name}")
        original_build_hash = sha256(original_build)
        repeat_build_hash = sha256(repeat_build)
        original_stage_hash = sha256(original_stage)
        repeat_stage_hash = sha256(repeat_stage)
        original_gxmi_hash = sha256(original_gxmi)
        repeat_gxmi_hash = sha256(repeat_gxmi)
        build_stage_equal = repeat_build_hash == repeat_stage_hash
        comparisons.append({
            "phase": "Phase29",
            "variant": name,
            "originalBuildSha256": original_build_hash,
            "repeatBuildSha256": repeat_build_hash,
            "originalStageSha256": original_stage_hash,
            "repeatStageSha256": repeat_stage_hash,
            "originalGxmiSha256": original_gxmi_hash,
            "repeatGxmiSha256": repeat_gxmi_hash,
            "repeatBuildEqualsStage": build_stage_equal,
            "repeatBuildEqualsOriginal": repeat_build_hash == original_build_hash,
            "repeatStageEqualsOriginal": repeat_stage_hash == original_stage_hash,
            "repeatGxmiEqualsOriginal": repeat_gxmi_hash == original_gxmi_hash,
        })
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    result = {
        "schema": 1,
        "phase": "Phase29",
        "variantCount": len(comparisons),
        "originalBuildRecord": str(args.original_build_record.resolve()),
        "repeatBuildRecord": str(args.repeat_build_record.resolve()),
        "comparisons": comparisons,
    }
    args.output_json.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"PHASE29_REPEAT_VARIANTS={len(comparisons)}")
    print(f"REPEAT_BUILD_STAGE_AGREEMENT={sum(item['repeatBuildEqualsStage'] for item in comparisons)}/{len(comparisons)}")
    print(f"REPEAT_BUILD_EQUALS_ORIGINAL={sum(item['repeatBuildEqualsOriginal'] for item in comparisons)}/{len(comparisons)}")
    print(f"REPEAT_STAGE_EQUALS_ORIGINAL={sum(item['repeatStageEqualsOriginal'] for item in comparisons)}/{len(comparisons)}")
    print(f"REPEAT_GXMI_EQUALS_ORIGINAL={sum(item['repeatGxmiEqualsOriginal'] for item in comparisons)}/{len(comparisons)}")
    print(f"REPEAT_COMPARISON_JSON={args.output_json}")
    return 0 if all(item["repeatBuildEqualsStage"] for item in comparisons) else 1


if __name__ == "__main__":
    raise SystemExit(main())
