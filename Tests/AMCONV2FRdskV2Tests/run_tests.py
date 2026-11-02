#!/usr/bin/env python3
"""Build real RDSK images with the supported generator, then run the hosted reader."""

from __future__ import annotations

import argparse
import importlib.util
import subprocess
import sys
import tempfile
from pathlib import Path


PROJECT = Path(__file__).resolve().parent
REPO = PROJECT.parents[1]
BUILDER = REPO / "Tools" / "ramdisk_builder.py"
CS_PROJ = PROJECT / "AMCONV2FRdskV2Tests.csproj"


def run(command: list[str], expected: int = 0) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(command, text=True, capture_output=True)
    if result.returncode != expected:
        raise RuntimeError(
            f"Command returned {result.returncode}, expected {expected}: {command}\n"
            f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
        )
    return result


def make_fixture(root: Path) -> Path:
    source = root / "source"
    (source / "Empty").mkdir(parents=True)
    (source / "Spare").mkdir()
    (source / "Nested" / "DeepEmpty").mkdir(parents=True)
    (source / "WithFiles" / "Child").mkdir(parents=True)
    (source / "Data").mkdir()
    (source / "TopFile.bin").write_bytes(bytes((0, 1, 2, 255, 13)))
    (source / "Zero.bin").write_bytes(b"")
    (source / "Note").write_bytes(b"collision fixture")
    (source / "WithFiles" / "inside.bin").write_bytes(b"inside payload")
    (source / "WithFiles" / "Child" / "leaf.txt").write_bytes(b"leaf")
    return source


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--artifacts-path",
        type=Path,
        help="isolate dotnet run outputs outside the test source directory",
    )
    arguments = parser.parse_args()

    if not BUILDER.is_file() or not CS_PROJ.is_file():
        raise RuntimeError("The RDSK v2 runner must be used from this checkout")

    with tempfile.TemporaryDirectory(prefix="amconv2f-", dir=PROJECT) as temp:
        root = Path(temp)
        source = make_fixture(root)
        image_a = root / "v2-a.img"
        image_b = root / "v2-b.img"
        legacy = root / "v1.img"
        empty_source = root / "empty-source"
        empty_source.mkdir()
        empty_image = root / "empty-v2.img"

        generator_assertions = 0
        first = run([sys.executable, "-B", str(BUILDER), str(source),
                     str(image_a), "--version", "2"])
        generator_assertions += 1
        second = run([sys.executable, "-B", str(BUILDER), str(source),
                      str(image_b), "--version", "2"])
        generator_assertions += 1
        run([sys.executable, "-B", str(BUILDER), str(source), str(legacy)])
        generator_assertions += 1
        run([sys.executable, "-B", str(BUILDER), str(empty_source),
             str(empty_image), "--version", "2"])
        generator_assertions += 1
        if image_a.read_bytes() != image_b.read_bytes():
            raise RuntimeError("Repeated RDSK v2 builds were not byte-identical")
        generator_assertions += 1
        print(first.stdout.strip())
        print(second.stdout.strip())

        invalid_source = root / "invalid-name"
        invalid_source.mkdir()
        (invalid_source / "caf\N{LATIN SMALL LETTER E WITH ACUTE}.txt").write_bytes(b"x")
        run([sys.executable, "-B", str(BUILDER), str(invalid_source),
             str(root / "invalid.img"), "--version", "2"], expected=1)
        if (root / "invalid.img").exists():
            raise RuntimeError("Generator left output after rejecting an invalid name")
        generator_assertions += 1
        spec = importlib.util.spec_from_file_location("rdsk_builder", BUILDER)
        if spec is None or spec.loader is None:
            raise RuntimeError("Cannot load the supported RDSK image builder")
        builder_module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(builder_module)
        for invalid_name in ("..", "a/b", "a\\b", "line\nfeed"):
            try:
                builder_module._encode_v2_name(invalid_name)
            except ValueError:
                generator_assertions += 1
                continue
            raise RuntimeError(f"Generator accepted invalid v2 name: {invalid_name!r}")
        try:
            builder_module._v2_child_path_length(900, b"x" * 200, False)
        except ValueError:
            generator_assertions += 1
        else:
            raise RuntimeError("Generator accepted an overlong canonical path")
        run([sys.executable, "-B", str(BUILDER), str(source),
             str(source / "nested-output.img"), "--version", "2"], expected=1)
        if (source / "nested-output.img").exists():
            raise RuntimeError("Generator wrote an output inside the source tree")
        generator_assertions += 1
        print(f"AMCONV2F_GENERATOR_ASSERTIONS={generator_assertions}")
        print("AMCONV2F_GENERATOR_REJECTION=PASS (non-ASCII, invalid components, "
              "overlong path, and output inside source tree)")

        hosted_command = ["dotnet", "run", "--project", str(CS_PROJ),
                          "--configuration", "Release"]
        if arguments.artifacts_path is not None:
            hosted_command.extend([
                "--artifacts-path", str(arguments.artifacts_path.resolve())
            ])
        hosted_command.extend(["--", str(image_a), str(legacy), str(empty_image)])
        hosted = run(hosted_command)
        print(hosted.stdout, end="")
        if hosted.stderr:
            print(hosted.stderr, file=sys.stderr, end="")

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError, subprocess.SubprocessError) as error:
        print(f"AMCONV2F_RUNNER_ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
