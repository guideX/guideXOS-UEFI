#!/usr/bin/env python3
"""Build a GXMI descriptor for the separately reviewed Phase 26 image."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "Phase24"))
from build_phase24_descriptor import build as build_phase24


PHASE26_FLAG = 1 << 4


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("map", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    # The Phase 24 parser remains the PE/layout authority. Phase 26 only
    # adds an explicit artifact identity bit; it never relaxes fixed-base,
    # no-import, no-relocation, or permission validation.
    temporary = args.output.with_suffix(args.output.suffix + ".phase24")
    build_phase24(args.image, args.map, temporary)
    descriptor = bytearray(temporary.read_bytes())
    flags = int.from_bytes(descriptor[16:20], "little")
    descriptor[16:20] = (flags | PHASE26_FLAG).to_bytes(4, "little")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(descriptor)
    temporary.unlink()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
