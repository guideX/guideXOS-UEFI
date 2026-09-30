#!/usr/bin/env python3
"""Build a Phase 33 descriptor from a verified NativeAOT payload."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "Phase24"))
from build_phase24_descriptor import build as build_phase24  # noqa: E402


PHASE26_FLAG = 1 << 4
PHASE33_FLAGS = {
    "success": (1 << 25) | (1 << 30) | (1 << 31),
    "failfast": (1 << 26) | (1 << 30) | (1 << 31),
    "stale-owner": (1 << 27) | (1 << 30) | (1 << 31),
    "invalid-action": (1 << 28) | (1 << 30) | (1 << 31),
    "malformed": (1 << 29) | (1 << 30) | (1 << 31),
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("map", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("mode", choices=sorted(PHASE33_FLAGS))
    args = parser.parse_args()

    temporary = args.output.with_suffix(args.output.suffix + ".phase26")
    build_phase24(args.image, args.map, temporary)
    descriptor = bytearray(temporary.read_bytes())
    flags = int.from_bytes(descriptor[16:20], "little")
    flags |= PHASE26_FLAG | PHASE33_FLAGS[args.mode]
    descriptor[16:20] = flags.to_bytes(4, "little")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(descriptor)
    temporary.unlink()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
