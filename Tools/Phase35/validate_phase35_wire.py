#!/usr/bin/env python3
"""Audit Phase 35R's bounded managed/kernel Persistent-read ABI."""

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


def layout(source: str, name: str, fixed_expression: str,
           fixed_size: int) -> tuple[int, list[str]]:
    body = packed_body(source, name)
    size = 0
    fields = []
    for kind, field in re.findall(
            r"\b(?:internal|public)\s+(uint|ulong)\s+(\w+)\s*;", body):
        size += SCALARS[kind]
        fields.append(field)
    fixed = re.search(r"fixed\s+byte\s+(\w+)\[" + fixed_expression +
                      r"\]\s*;", body)
    if not fixed:
        reject(f"bounded fixed buffer missing in {name}")
    fields.append(fixed.group(1))
    return size + fixed_size, fields


def response_layout(source: str, name: str) -> tuple[int, list[str]]:
    body = packed_body(source, name)
    fields = []
    size = 0
    for kind, field in re.findall(
            r"\b(?:internal|public)\s+(uint|ulong)\s+(\w+)\s*;", body):
        fields.append(field)
        size += SCALARS[kind]
    return size, fields


def require(source: str, pattern: str, label: str) -> None:
    if not re.search(pattern, source, re.DOTALL):
        reject(f"contract mismatch: {label}")


def main() -> int:
    sdk_abi = (ROOT / "GuideXos.User/GuideXosInternalAbi.cs").read_text()
    sdk_api = (ROOT / "GuideXos.User/GuideXos.cs").read_text()
    kernel = (ROOT / "Kernel/Misc/Ring3Abi.cs").read_text()
    process = (ROOT / "Kernel/Misc/Process.cs").read_text()
    services = (ROOT / "guideXOS/OS/ApplicationServices.cs").read_text()
    storage = (ROOT / "guideXOS/OS/ApplicationStorageServices.cs").read_text()

    for source, pattern, label in [
        (services, r"Storage\s*=\s*9", "Storage service ID 9"),
        (services, r"Persistent\s*=\s*0", "Persistent selector value 0"),
        (services, r"MaxChunkLength\s*=\s*64\s*\*\s*1024", "64-KiB read cap"),
        (services, r"MaxRelativePathLength\s*=\s*192", "192-unit path cap"),
        (services, r"MaxPathSegmentLength\s*=\s*64", "64-unit segment cap"),
        (sdk_abi, r"PersistentReadOperation\s*=\s*1", "read-only ABI operation 1"),
        (kernel, r"PersistentStorageRead\s*=\s*7", "dedicated syscall 7"),
        (kernel, r"case\s+PersistentStorageRead:\s*stack->rs\.rax\s*=\s*DispatchPersistentStorageRead\(", "dedicated Persistent-read dispatcher"),
        (kernel, r"PersistentReadOperation\s*=\s*1", "kernel operation 1"),
        (sdk_api, r"public\s+static\s+class\s+GuideXosStorage", "public storage API type"),
        (sdk_api, r"public\s+static\s+GuideXosResult\s+TryReadBytes\s*\(\s*string\s+path", "public typed read method"),
        (sdk_abi, r"byte\*\s+scratch\s*=\s*stackalloc\s+byte\[GuideXosStorage\.MaxValueLength\]", "one bounded stack transfer buffer"),
        (sdk_abi, r"value\s*=\s*Array\.Empty<byte>\(\)", "empty/default output strategy"),
        (sdk_abi, r"new\s+byte\[\(int\)response\.BytesRead\]", "exact-sized result copy"),
        (kernel, r"TryCreateContext\(owner", "fresh context derived from process owner"),
        (kernel, r"context\.ApplicationId\s*==\s*requester\.DescriptorId\s*&&\s*requester\.ApplicationId\s*==\s*requester\.DescriptorId", "derived AppId matches validated descriptor"),
        (kernel, r"requester\.LifecycleState\s*!=\s*ApplicationInstanceLifecycleState\.Running", "Running lifecycle gate"),
        (kernel, r"requester\.LifecycleState\s*!=\s*\n?\s*ApplicationInstanceLifecycleState\.Activated", "Activated lifecycle gate"),
        (kernel, r"ValidateWritableUserRange\(process\.Space\.Pml4", "full writable user range validation"),
        (kernel, r"request->DataCapacity\s*>\s*\n?\s*ApplicationStorageReadRequest\.MaxChunkLength", "kernel capacity bound"),
        (kernel, r"request->Offset\s*!=\s*0", "fixed zero offset"),
        (kernel, r"ApplicationStorageNamespace\.Persistent", "fixed Persistent namespace"),
        (kernel, r"Native\.Movsb\(\(void\*\)request\.DataBuffer", "validated value copy to user buffer"),
        (kernel, r"ApplicationStoragePathRules\.IsValid", "Phase 10 path rules reused"),
    ]:
        require(source, pattern, label)

    api_body = re.search(r"public\s+static\s+class\s+GuideXosStorage\s*\{(.*?)\n\s*\}",
                         sdk_api, re.DOTALL)
    if not api_body:
        reject("public storage API block not found")
    public_constants = re.findall(
        r"public\s+const\s+\w+\s+(\w+)\s*=", api_body.group(1))
    public_methods = re.findall(
        r"public\s+static\s+[\w<>]+\s+(\w+)\s*\(",
        api_body.group(1))
    public_members = public_constants + public_methods
    if public_members != ["MaxRelativePathLength", "MaxPathSegmentLength",
                          "MaxValueLength", "TryReadBytes"]:
        reject(f"public storage surface is not read-only: {public_members}")
    if re.search(r"public\s+.*(?:Write|Delete|Clear|Enumerate|Temporary|Stream|Handle)",
                 api_body.group(1), re.IGNORECASE):
        reject("public mutating/handle/Temporary storage capability found")
    sdk_size, sdk_fields = layout(sdk_abi,
        "GuideXosPersistentReadRequestWire",
        r"GuideXosStorage\.MaxRelativePathLength\s*\*\s*2", 192 * 2)
    kernel_size, kernel_fields = layout(kernel,
        "Ring3PersistentReadRequest",
        r"ApplicationStorageRequest\.MaxRelativePathLength\s*\*\s*2",
        192 * 2)
    if sdk_size != 436 or kernel_size != 436 or sdk_fields != kernel_fields:
        reject(f"request layout mismatch: SDK {sdk_size}/{sdk_fields}; kernel {kernel_size}/{kernel_fields}")
    sdk_response = response_layout(sdk_abi,
        "GuideXosPersistentReadResponseWire")
    kernel_response = response_layout(kernel,
        "Ring3PersistentReadResponse")
    if sdk_response != kernel_response or sdk_response[0] != 24:
        reject("response layout mismatch; expected matching 24-byte response")

    print("PHASE35_WIRE=service:9;dedicated-syscall:7;operation:1")
    print("PHASE35_WIRE=path:utf16le;max:192;segment:64;capacity:65536")
    print(f"PHASE35_WIRE=request:{sdk_size};response:{sdk_response[0]}")
    print("PHASE35_WIRE=authority:process-owner-derived;scope:Persistent-only")
    print("PHASE35_WIRE_LAYOUT_PASS=1")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"PHASE35_WIRE_LAYOUT_PASS=0: {error}", file=sys.stderr)
        raise SystemExit(1)
