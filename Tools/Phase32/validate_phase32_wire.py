#!/usr/bin/env python3
"""Check the public SDK and kernel Phase 32 OpenDocument wire contract."""

from __future__ import annotations

import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCALARS = {"uint": 4, "ulong": 8}
BOUNDS = {
    "GuideXosShell.MaxApplicationIdLength": 1024,
    "GuideXosShell.MaxDocumentLength": 1024,
    "LaunchRequest.MaxTextLength": 1024,
}


def reject(message: str) -> None:
    raise ValueError(message)


def block(source: str, name: str) -> str:
    pattern = re.compile(
        r"\[StructLayout\(LayoutKind\.Sequential,\s*Pack\s*=\s*1\)\]"
        r"\s*(?:internal|public)\s+(?:unsafe\s+)?struct\s+"
        + re.escape(name)
        + r"\s*\{(.*?)\n\s*\}",
        re.DOTALL,
    )
    match = pattern.search(source)
    if not match:
        reject(f"packed struct not found: {name}")
    return match.group(1)


def size_of(source: str, name: str) -> tuple[int, list[str]]:
    body = block(source, name)
    size = 0
    fields: list[str] = []
    for scalar, field in re.findall(
        r"\b(?:internal|public)\s+(uint|ulong)\s+(\w+)\s*;", body
    ):
        size += SCALARS[scalar]
        fields.append(field)

    def fixed_size(match: re.Match[str]) -> int:
        field_name = match.group(1)
        expression = re.sub(r"\s+", "", match.group(2))
        bound = re.fullmatch(r"([\w.]+)\*2", expression)
        if not bound or bound.group(1) not in BOUNDS:
            reject(f"unrecognized fixed buffer bound in {name}: {expression}")
        fields.append(field_name)
        return BOUNDS[bound.group(1)] * 2

    size += sum(
        fixed_size(match)
        for match in re.finditer(r"fixed\s+byte\s+(\w+)\[([^]]+)\]\s*;", body)
    )
    if not fields:
        reject(f"no recognized fields in {name}")
    return size, fields


def require(source: str, pattern: str, label: str) -> None:
    if re.search(pattern, source) is None:
        reject(f"contract value mismatch: {label}")


def main() -> int:
    sdk_abi = (ROOT / "GuideXos.User/GuideXosInternalAbi.cs").read_text()
    sdk_api = (ROOT / "GuideXos.User/GuideXos.cs").read_text()
    kernel_abi = (ROOT / "Kernel/Misc/Ring3Abi.cs").read_text()
    services = (ROOT / "guideXOS/OS/ApplicationServices.cs").read_text()
    launch = (ROOT / "guideXOS/OS/ModernAppModel.cs").read_text()

    require(services, r"Shell\s*=\s*7\b", "Shell service ID 7")
    require(services, r"MaxTargetLength\s*=\s*1024\b", "Phase 9 Shell bound 1024")
    require(launch, r"MaxTextLength\s*=\s*1024\b", "LaunchRequest bound 1024")
    require(sdk_api, r"MaxDocumentLength\s*=\s*MaxApplicationIdLength", "SDK document bound aliases the existing 1024-unit limit")
    require(sdk_api, r"TryOpenDocument\s*\(", "public typed OpenDocument API")
    for source, label in ((sdk_abi, "managed ABI"), (kernel_abi, "kernel ABI")):
        require(source, r"ShellOpenDocumentOperation\s*=\s*2\b", f"{label} OpenDocument operation 2")
        require(source, r"ShellLaunchApplicationOperation\s*=\s*1\b", f"{label} stable-ID operation 1")
    require(kernel_abi, r"ApplicationShellOpenRequest\.ForDocument\(target\)", "kernel dispatch uses Phase 9 ForDocument")
    require(kernel_abi, r"TargetLength\s*!=\s*0", "kernel rejects empty target length")
    require(kernel_abi, r"TargetLength\s*<=\s*LaunchRequest\.MaxTextLength", "kernel enforces maximum target length")
    require(sdk_abi, r"target\[\(i\s*\*\s*2\)\s*\+\s*0\]", "managed target writes UTF-16LE low byte")
    require(sdk_abi, r"target\[\(i\s*\*\s*2\)\s*\+\s*1\]", "managed target writes UTF-16LE high byte")
    require(sdk_abi, r"(?s)string\.IsNullOrEmpty\(targetText\).*targetText\.Length\s*>\s*maximumLength", "SDK rejects empty and overlong strings without truncation")

    pairs = (
        ("GuideXosShellLaunchRequestWire", "Ring3ShellLaunchRequest", 2084),
        ("GuideXosShellLaunchResponseWire", "Ring3ShellLaunchResponse", 16),
    )
    for managed_name, kernel_name, expected in pairs:
        managed_size, managed_fields = size_of(sdk_abi, managed_name)
        kernel_size, kernel_fields = size_of(kernel_abi, kernel_name)
        if managed_size != expected or kernel_size != expected:
            reject(
                f"{managed_name}/{kernel_name}: expected {expected}, "
                f"found managed={managed_size}, kernel={kernel_size}"
            )
        if managed_fields != kernel_fields:
            reject(
                f"field order/name mismatch: {managed_name}={managed_fields}; "
                f"{kernel_name}={kernel_fields}"
            )
        print(f"PHASE32_WIRE_LAYOUT_{managed_name}={expected}")

    if re.search(r"public\s+(?:unsafe\s+)?struct\s+GuideXosShell\w*Wire", sdk_abi):
        reject("raw Shell wire type is public")
    print("PHASE32_WIRE_SERVICE=7;OPEN_DOCUMENT=2;LAUNCH_APPLICATION_ID=1")
    print("PHASE32_WIRE_TARGET=UTF16LE_CODE_UNITS:1024")
    print("PHASE32_WIRE_AUTHORITY=NONE")
    print("PHASE32_WIRE_LAYOUT_PASS=1")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"PHASE32_WIRE_LAYOUT_PASS=0: {error}", file=sys.stderr)
        raise SystemExit(1)
