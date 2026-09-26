#!/usr/bin/env python3
"""Check that the managed and kernel Phase 30 clipboard wire layouts agree."""

from __future__ import annotations

import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCALARS = {"uint": 4, "ulong": 8}
BOUNDS = {
    "GuideXosClipboard.MaxTextLength": 65536,
    "GuideXosClipboard.MaxApplicationIdLength": 96,
    "ApplicationClipboardWriteRequest.MaxTextLength": 65536,
    "ApplicationServiceContext.MaxApplicationIdLength": 96,
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
        match_bound = re.fullmatch(r"([\w.]+)\*2", expression)
        if not match_bound or match_bound.group(1) not in BOUNDS:
            reject(f"unrecognized fixed buffer bound in {name}: {expression}")
        fields.append(field_name)
        return BOUNDS[match_bound.group(1)] * 2

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

    require(services, r"Clipboard\s*=\s*10\b", "clipboard service ID 10")
    require(services, r"MaxTextLength\s*=\s*64\s*\*\s*1024", "text bound 65536")
    require(services, r"MaxApplicationIdLength\s*=\s*96\b", "AppId bound 96")
    for source, label in ((sdk_abi, "managed ABI"), (kernel_abi, "kernel ABI")):
        require(source, r"ClipboardSetTextOperation\s*=\s*1\b", f"{label} SetText ID 1")
        require(source, r"ClipboardGetTextOperation\s*=\s*2\b", f"{label} GetText ID 2")
        require(source, r"ClipboardClearOperation\s*=\s*3\b", f"{label} Clear ID 3")
    require(sdk_abi, r"ClipboardService\s*=\s*10\b", "managed clipboard service ID 10")
    require(
        kernel_abi,
        r"ClipboardService\s*=\s*\(uint\)ApplicationServiceId\.Clipboard",
        "kernel derives service ID from App Model enum",
    )
    require(sdk_api, r"MaxTextLength\s*=\s*64\s*\*\s*1024", "public text bound 65536")
    require(sdk_api, r"MaxApplicationIdLength\s*=\s*96\b", "public AppId bound 96")

    pairs = (
        ("GuideXosServiceRequestWire", "Ring3ServiceRequest", 32),
        ("GuideXosClipboardSetRequestWire", "Ring3ClipboardSetRequest", 131096),
        ("GuideXosClipboardResponseWire", "Ring3ClipboardResponse", 131296),
        ("GuideXosClipboardClearRequestWire", "Ring3ClipboardClearRequest", 20),
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
        print(f"PHASE30_WIRE_LAYOUT_{managed_name}={expected}")

    print("PHASE30_WIRE_SERVICE=10;SET=1;GET=2;CLEAR=3")
    print("PHASE30_WIRE_BOUNDS=UTF16_CODE_UNITS:65536;APP_ID_CODE_UNITS:96")
    print("PHASE30_WIRE_LAYOUT_PASS=1")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"PHASE30_WIRE_LAYOUT_PASS=0: {error}", file=sys.stderr)
        raise SystemExit(1)
