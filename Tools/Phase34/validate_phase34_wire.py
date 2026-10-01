#!/usr/bin/env python3
"""Check the managed and kernel Phase 34 resource wire contract."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCALARS = {"uint": 4, "ulong": 8}


def reject(message: str) -> None:
    raise ValueError(message)


def packed_body(source: str, name: str) -> str:
    match = re.search(
        r"\[StructLayout\(LayoutKind\.Sequential,\s*Pack\s*=\s*1\)\]"
        r"\s*(?:internal|public)\s+(?:unsafe\s+)?struct\s+" +
        re.escape(name) + r"\s*\{(.*?)\n\s*\}", source, re.DOTALL)
    if not match:
        reject(f"packed struct not found: {name}")
    return match.group(1)


def layout(source: str, name: str, key_bound: int) -> tuple[int, list[str]]:
    body = packed_body(source, name)
    size = 0
    fields: list[str] = []
    for kind, field in re.findall(
            r"\b(?:internal|public)\s+(uint|ulong)\s+(\w+)\s*;", body):
        size += SCALARS[kind]
        fields.append(field)
    fixed = re.search(r"fixed\s+byte\s+(\w+)\["
                      r"(?:GuideXosResources\.MaxResourceNameLength|"
                      r"ApplicationResourceRequest\.MaxResourceKeyLength)\]\s*;",
                      body)
    if not fixed:
        reject(f"fixed bounded ASCII key missing in {name}")
    fields.append(fixed.group(1))
    size += key_bound
    return size, fields


def response_layout(source: str, name: str) -> tuple[int, list[str]]:
    body = packed_body(source, name)
    fields: list[str] = []
    size = 0
    for kind, field in re.findall(
            r"\b(?:internal|public)\s+(uint|ulong)\s+(\w+)\s*;", body):
        fields.append(field)
        size += SCALARS[kind]
    return size, fields


def require(source: str, pattern: str, label: str) -> None:
    if not re.search(pattern, source):
        reject(f"contract mismatch: {label}")


def main() -> int:
    sdk_abi = (ROOT / "GuideXos.User/GuideXosInternalAbi.cs").read_text()
    sdk_api = (ROOT / "GuideXos.User/GuideXos.cs").read_text()
    kernel = (ROOT / "Kernel/Misc/Ring3Abi.cs").read_text()
    services = (ROOT / "guideXOS/OS/ApplicationServices.cs").read_text()
    resource_service = (ROOT / "guideXOS/OS/ApplicationResourceServices.cs").read_text()

    require(services, r"Resources\s*=\s*8", "Phase 10 Resources service ID 8")
    require(services, r"MaxResourceKeyLength\s*=\s*96", "96-character resource key bound")
    require(services, r"MaxChunkLength\s*=\s*64\s*\*\s*1024", "64 KiB Phase 10 read bound")
    require(sdk_abi, r"ResourcesService\s*=\s*8", "managed Resources service ID 8")
    require(sdk_abi, r"ResourceMetadataOperation\s*=\s*1", "metadata operation 1")
    require(sdk_abi, r"ResourceReadOperation\s*=\s*2", "read operation 2")
    require(kernel, r"ResourceMetadataOperation\s*=\s*1", "kernel metadata operation 1")
    require(kernel, r"ResourceReadOperation\s*=\s*2", "kernel read operation 2")
    require(sdk_api, r"TryReadBytes\s*\(\s*string\s+resourceName", "public typed read API")
    require(sdk_api, r"MaxResourcePayloadLength\s*=\s*64\s*\*\s*1024", "managed allocation ceiling")
    require(resource_service, r"Find\(context\.ApplicationId,\s*\n?\s*request\.ResourceKey\)", "application-scoped resource lookup")
    require(kernel, r"TryCreateContext\(owner", "kernel-derived ApplicationServiceContext")
    require(kernel, r"context\.ApplicationId\s*!=\s*requester\.ApplicationId", "scope derived from owning application")
    require(kernel, r"ValidateResourceWritableRange\(process", "whole destination range validation")
    require(kernel, r"ResourceNameLength\s*==\s*0", "kernel empty key rejection")
    require(kernel, r"ResourceNameLength\s*>\s*\n?\s*ApplicationResourceRequest\.MaxResourceKeyLength", "kernel oversize key rejection")
    require(sdk_api + sdk_abi, r"fixed\s+byte\s+ResourceName\[.*?MaxResourceNameLength\]", "bounded inline key field")

    sdk_size, sdk_fields = layout(sdk_abi, "GuideXosResourceRequestWire", 96)
    kernel_size, kernel_fields = layout(kernel, "Ring3ResourceRequest", 96)
    if sdk_size != 152 or kernel_size != 152 or sdk_fields != kernel_fields:
        reject(f"resource request layout mismatch: SDK {sdk_size}/{sdk_fields}; kernel {kernel_size}/{kernel_fields}")
    sdk_response_size, sdk_response_fields = response_layout(
        sdk_abi, "GuideXosResourceResponseWire")
    kernel_response_size, kernel_response_fields = response_layout(
        kernel, "Ring3ResourceResponse")
    if (sdk_response_size != 44 or kernel_response_size != 44 or
            sdk_response_fields != kernel_response_fields):
        reject("resource response layout mismatch; expected matching 44-byte headers")

    print("PHASE34_WIRE=service:8;operations:metadata-1/read-2")
    print("PHASE34_WIRE=key:ascii;max:96;payload:max:65536")
    print(f"PHASE34_WIRE=request:{sdk_size};response:{sdk_response_size}")
    print("PHASE34_WIRE=authority:kernel-derived;data:separate-copied-buffer")
    print("PHASE34_WIRE_LAYOUT_PASS=1")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"PHASE34_WIRE_LAYOUT_PASS=0: {error}", file=sys.stderr)
        raise SystemExit(1)
