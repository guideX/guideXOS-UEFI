#!/usr/bin/env python3
"""Compare BUILD1E managed identities with the read-only BUILD1D CSV."""

from __future__ import annotations

import argparse
import csv
from pathlib import Path


def load_rows(path: Path) -> dict[tuple[str, str], dict[str, str]]:
    with path.open(newline="", encoding="utf-8-sig") as stream:
        rows = list(csv.DictReader(stream))
    indexed = {(row["phase"], row["variant"]): row for row in rows}
    if len(indexed) != len(rows):
        raise SystemExit(f"Duplicate phase/variant entries in {path}")
    return indexed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--build1e-csv", type=Path, required=True)
    parser.add_argument("--build1d-csv", type=Path, required=True)
    parser.add_argument("--output-csv", type=Path, required=True)
    args = parser.parse_args()

    current = load_rows(args.build1e_csv)
    reference = load_rows(args.build1d_csv)
    if set(current) != set(reference) or len(current) != 41:
        raise SystemExit("BUILD1D and BUILD1E must each describe the same 41 variants")

    args.output_csv.parent.mkdir(parents=True, exist_ok=True)
    fields = [
        "phase", "variant", "build1e_sha256", "build1d_sha256",
        "executable_identity_equal", "build1e_gxmi_sha256", "build1d_gxmi_sha256",
        "descriptor_file_identity_equal",
    ]
    same_exe = 0
    same_gxmi = 0
    with args.output_csv.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        for phase, variant in sorted(current):
            ours = current[(phase, variant)]
            other = reference[(phase, variant)]
            same_exe += ours["raw_sha256"] == other["raw_sha256"]
            same_gxmi += ours["descriptor_file_sha256"] == other["descriptor_file_sha256"]
            writer.writerow({
                "phase": phase,
                "variant": variant,
                "build1e_sha256": ours["raw_sha256"],
                "build1d_sha256": other["raw_sha256"],
                "executable_identity_equal": str(ours["raw_sha256"] == other["raw_sha256"]).lower(),
                "build1e_gxmi_sha256": ours["descriptor_file_sha256"],
                "build1d_gxmi_sha256": other["descriptor_file_sha256"],
                "descriptor_file_identity_equal": str(ours["descriptor_file_sha256"] == other["descriptor_file_sha256"]).lower(),
            })
    print(f"VARIANTS_COMPARED={len(current)}")
    print(f"EXECUTABLE_IDENTITIES_EQUAL={same_exe}/41")
    print(f"DESCRIPTOR_FILE_IDENTITIES_EQUAL={same_gxmi}/41")
    print(f"IDENTITY_DIFFERENCES={41 - same_exe}")
    print(f"COMPARISON_CSV={args.output_csv}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
