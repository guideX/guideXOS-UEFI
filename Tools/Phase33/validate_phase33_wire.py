#!/usr/bin/env python3
"""Check the public managed and kernel Phase 33 Shell object wire contract."""

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


def layout(source: str, name: str) -> tuple[int, list[str]]:
    body = packed_body(source, name)
    size = 0
    fields: list[str] = []
    for kind, field in re.findall(
            r"\b(?:internal|public)\s+(uint|ulong)\s+(\w+)\s*;", body):
        size += SCALARS[kind]
        fields.append(field)
    fixed = re.search(r"fixed\s+byte\s+(\w+)\["
                      r"(?:GuideXosShell\.MaxApplicationIdLength|"
                      r"LaunchRequest\.MaxTextLength)\s*\*\s*2\]\s*;",
                      body)
    if not fixed:
        reject(f"fixed Phase 9 bounded buffer missing in {name}")
    fields.append(fixed.group(1))
    size += 1024 * 2
    return size, fields


def require(source: str, pattern: str, label: str) -> None:
    if not re.search(pattern, source):
        reject(f"contract mismatch: {label}")


def main() -> int:
    sdk_abi = (ROOT / "GuideXos.User/GuideXosInternalAbi.cs").read_text()
    sdk_api = (ROOT / "GuideXos.User/GuideXos.cs").read_text()
    kernel = (ROOT / "Kernel/Misc/Ring3Abi.cs").read_text()
    services = (ROOT / "guideXOS/OS/ApplicationServices.cs").read_text()
    shell = (ROOT / "guideXOS/OS/ApplicationShellServices.cs").read_text()
    model = (ROOT / "guideXOS/OS/ModernAppModel.cs").read_text()

    require(services, r"Shell\s*=\s*7\b", "Phase 9 Shell ID 7")
    require(services, r"MaxTargetLength\s*=\s*1024\b", "Phase 9 target bound 1024")
    require(model, r"MaxTextLength\s*=\s*1024\b", "LaunchRequest bound 1024")
    require(sdk_abi, r"ShellOpenObjectOperation\s*=\s*3\b", "operation 3")
    require(kernel, r"ShellOpenObjectOperation\s*=\s*3\b", "kernel operation 3")
    require(sdk_api, r"enum\s+GuideXosShellObject\s*:\s*uint", "public bounded object enum")
    require(sdk_api, r"ComputerFiles\s*=\s*1", "ComputerFiles public case")
    require(sdk_api, r"TryOpenShellObject\s*\(\s*GuideXosShellObject", "typed public API")
    require(kernel, r"ForShellObject\(target\)", "kernel delegates to Phase 9 Shell object request")
    require(kernel, r"target\s*!=\s*Phase33ShellObjectId", "kernel object allowlist")
    require(shell, r"ModernShellAdapter\.TryCreateLaunchRequest", "existing Phase 9 Shell object resolver")
    require(model, r"gxos\.shell\.computerfiles", "live Computer Files object")
    require(sdk_abi, r"TryOpenShellObject\(\s*GuideXosShellObject", "internal typed transport")
    for source, label in ((sdk_abi, "managed ABI"), (kernel, "kernel ABI")):
        size, fields = layout(source, "GuideXosShellLaunchRequestWire" if label == "managed ABI" else "Ring3ShellLaunchRequest")
        if size != 2084:
            reject(f"{label} request size is {size}, expected 2084")
        print(f"PHASE33_{label.upper().replace(' ', '_')}_REQUEST_SIZE={size}")
    managed_response, managed_fields = layout_response(sdk_abi, "GuideXosShellLaunchResponseWire")
    kernel_response, kernel_fields = layout_response(kernel, "Ring3ShellLaunchResponse")
    if managed_response != 16 or kernel_response != 16 or managed_fields != kernel_fields:
        reject("Phase 9 typed Shell response layout does not match 16 bytes")
    print("PHASE33_WIRE=service:7;operation:3;object:gxos.shell.computerfiles")
    print("PHASE33_WIRE=target:utf16le;bound:1024-code-units;no-authority-fields")
    print("PHASE33_WIRE=existing-request:2084;existing-response:16")
    print("PHASE33_WIRE_LAYOUT_PASS=1")
    return 0


def layout_response(source: str, name: str) -> tuple[int, list[str]]:
    match = re.search(
        r"\[StructLayout\(LayoutKind\.Sequential,\s*Pack\s*=\s*1\)\]"
        r"\s*(?:internal|public)\s+(?:unsafe\s+)?struct\s+" +
        re.escape(name) + r"\s*\{(.*?)\n\s*\}", source, re.DOTALL)
    if not match:
        reject(f"packed struct not found: {name}")
    fields = re.findall(r"\b(?:internal|public)\s+uint\s+(\w+)\s*;", match.group(1))
    if len(fields) != 4:
        reject(f"response field count mismatch for {name}")
    return len(fields) * 4, fields


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"PHASE33_WIRE_LAYOUT_PASS=0: {error}", file=sys.stderr)
        raise SystemExit(1)
