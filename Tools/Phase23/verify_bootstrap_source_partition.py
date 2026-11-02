#!/usr/bin/env python3
"""Verify source-body preservation and object provenance for the P23R13 split."""

from __future__ import annotations

import argparse
import re
import sys
from collections import Counter
from pathlib import Path


THREAD_PATTERNS = [
    (r"^\s*void\s+Thread::ValidateExInfoStack\s*\(", False),
    (r"^\s*void\s+Thread::WaitForGC\s*\(", False),
    (r"^\s*void\s+Thread::EnablePreemptiveMode\s*\(", False),
    (r"^\s*void\s+Thread::DisablePreemptiveMode\s*\(", False),
    (r"^\s*void\s*\*\s+Thread::GetCurrentThreadPInvokeReturnAddress\s*\(", False),
    (r"^\s*PTR_ExInfo\s+Thread::GetCurExInfo\s*\(", False),
    (r"^\s*void\s+Thread::Construct\s*\(", False),
    (r"^\s*bool\s+Thread::IsInitialized\s*\(", False),
    (r"^\s*bool\s+Thread::IsGCSpecial\s*\(", False),
    (r"^\s*uint64_t\s+Thread::GetPalThreadIdForLogging\s*\(", False),
    (r"^\s*uint64_t\s+Thread::GetDeadThreadsNonAllocBytes\s*\(", False),
    (r"^\s*void\s+Thread::SetState\s*\(", False),
    (r"^\s*void\s+Thread::ClearState\s*\(", False),
    (r"^\s*bool\s+Thread::IsSuppressGcStressSet\s*\(", False),
    (r"^\s*void\s+Thread::SetSuppressGcStress\s*\(", False),
    (r"^\s*void\s+Thread::ClearSuppressGcStress\s*\(", False),
    (r"^\s*bool\s+Thread::IsHijackTarget\s*\(", False),
    (r"^\s*void\s+Thread::HijackCallback\s*\(", False),
    (r"^\s*void\s+Thread::HijackReturnAddress\s*\(", True),
    (r"^\s*void\s+Thread::HijackReturnAddressWorker\s*\(", False),
    (r"^\s*bool\s+Thread::InlineSuspend\s*\(", False),
    (r"^\s*void\s+Thread::Unhijack\s*\(", False),
    (r"^\s*void\s+Thread::CrossThreadUnhijack\s*\(", False),
    (r"^\s*void\s+Thread::UnhijackWorker\s*\(", False),
    (r"^\s*bool\s+Thread::IsHijacked\s*\(", False),
    (r"^\s*void\s*\*\s+Thread::GetHijackedReturnAddress\s*\(", False),
    (r"^\s*EXTERN_C\s+NOINLINE\s+void\s+FASTCALL\s+RhpWaitForGC2\s*\(", False),
    (r"^\s*EXTERN_C\s+NOINLINE\s+void\s+FASTCALL\s+RhpGcPoll2\s*\(", False),
]

STORE_PATTERNS = [
    (r"^\s*ThreadStore\s*\*\s+GetThreadStore\s*\(", False),
    (r"^\s*PTR_Thread\s+ThreadStore::GetSuspendingThread\s*\(", False),
    (r"^\s*ThreadStore::Iterator::Iterator\s*\(", False),
    (r"^\s*ThreadStore::Iterator::~Iterator\s*\(", False),
    (r"^\s*PTR_Thread\s+ThreadStore::Iterator::GetNext\s*\(", False),
    (r"^\s*ThreadStore::ThreadStore\s*\(", False),
    (r"^\s*ThreadStore::~ThreadStore\s*\(", False),
    (r"^\s*ThreadStore\s*\*\s+ThreadStore::Create\s*\(", False),
    (r"^\s*void\s+ThreadStore::Destroy\s*\(", False),
    (r"^\s*void\s+ThreadStore::AttachCurrentThread\s*\(", True),
    (r"^\s*void\s+ThreadStore::LockThreadStore\s*\(", False),
    (r"^\s*void\s+ThreadStore::UnlockThreadStore\s*\(", False),
]


def normalize(text: str) -> str:
    return text.replace("\r\n", "\n").replace("\r", "\n")


def find_brace_end(text: str, brace: int) -> int:
    depth = 0
    state = "code"
    index = brace
    while index < len(text):
        char = text[index]
        nxt = text[index + 1] if index + 1 < len(text) else ""
        if state == "code":
            if char == "/" and nxt == "/":
                state = "line"
                index += 2
                continue
            if char == "/" and nxt == "*":
                state = "block"
                index += 2
                continue
            if char == '"':
                state = "string"
                index += 1
                continue
            if char == "'":
                state = "char"
                index += 1
                continue
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0:
                    return index + 1
        elif state == "line":
            if char == "\n":
                state = "code"
        elif state == "block":
            if char == "*" and nxt == "/":
                state = "code"
                index += 2
                continue
        elif state in ("string", "char"):
            if char == "\\":
                index += 2
                continue
            if (state == "string" and char == '"') or (state == "char" and char == "'"):
                state = "code"
        index += 1
    raise ValueError("unbalanced function body")


def function_bodies(text: str, pattern: str) -> list[str]:
    matches = list(re.finditer(pattern, text, re.MULTILINE))
    bodies = []
    for match in matches:
        start = text.rfind("\n", 0, match.start()) + 1
        brace = text.find("{", match.end())
        if brace < 0:
            raise ValueError(f"opening brace missing for {pattern}")
        end = find_brace_end(text, brace)
        bodies.append(text[start:end].strip())
    return bodies


def verify_group(
    label: str,
    patterns: list[tuple[str, bool]],
    original: str,
    remaining: str,
    partition: str,
) -> int:
    moved_count = 0
    for pattern, allow_many in patterns:
        before = function_bodies(original, pattern)
        still_in_original = function_bodies(remaining, pattern)
        after = function_bodies(partition, pattern)
        if not before:
            raise ValueError(f"{label}: source definition missing: {pattern}")
        if not allow_many and len(before) != 1:
            raise ValueError(f"{label}: expected one source definition, got {len(before)}: {pattern}")
        if still_in_original:
            raise ValueError(f"{label}: definition remained in the combined TU: {pattern}")
        if Counter(before) != Counter(after):
            raise ValueError(f"{label}: moved body differs from source: {pattern}")
        moved_count += len(before)
    return moved_count


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-runtime-root", required=True, type=Path)
    parser.add_argument("--partitioned-runtime-root", required=True, type=Path)
    parser.add_argument("--link-map", required=True, type=Path)
    args = parser.parse_args()

    baseline = args.baseline_runtime_root.resolve()
    partitioned = args.partitioned_runtime_root.resolve()
    map_text = normalize(args.link_map.read_text(encoding="utf-8", errors="replace"))

    thread_count = verify_group(
        "Thread", THREAD_PATTERNS,
        normalize((baseline / "thread.cpp").read_text(encoding="utf-8")),
        normalize((partitioned / "thread.cpp").read_text(encoding="utf-8")),
        normalize((partitioned / "thread.bootstrap.cpp").read_text(encoding="utf-8")),
    )
    store_count = verify_group(
        "ThreadStore", STORE_PATTERNS,
        normalize((baseline / "threadstore.cpp").read_text(encoding="utf-8")),
        normalize((partitioned / "threadstore.cpp").read_text(encoding="utf-8")),
        normalize((partitioned / "threadstore.bootstrap.cpp").read_text(encoding="utf-8")),
    )

    source_inl = normalize((partitioned / "threadstore.inl").read_text(encoding="utf-8"))
    store_bootstrap = normalize((partitioned / "threadstore.bootstrap.cpp").read_text(encoding="utf-8"))
    if "#if defined(TARGET_GUIDEXOS)\nEXTERN_C __declspec(thread) RuntimeThreadLocals tls_CurrentThread;" not in source_inl:
        raise ValueError("GUIDEXOS TLS declaration does not remain an extern declaration")
    if store_bootstrap.count("EXTERN_C __declspec(selectany) __declspec(thread) RuntimeThreadLocals tls_CurrentThread;") != 1:
        raise ValueError("expected exactly one GUIDEXOS TLS definition in the bootstrap TU")

    required_map_origins = {
        "?Construct@Thread@@AEAAXXZ": "thread.bootstrap.cpp.obj",
        "?HijackCallback@Thread@@CAXPEAU_CONTEXT@@PEAX@Z": "thread.bootstrap.cpp.obj",
        "?Create@ThreadStore@@SAPEAV1@PEAVRuntimeInstance@@@Z": "threadstore.bootstrap.cpp.obj",
        "?AttachCurrentThread@ThreadStore@@SAXXZ": "threadstore.bootstrap.cpp.obj",
        "tls_CurrentThread": "threadstore.bootstrap.cpp.obj",
    }
    for symbol, expected_member in required_map_origins.items():
        matching_lines = [line for line in map_text.splitlines() if symbol in line]
        if not matching_lines or not any(expected_member in line for line in matching_lines):
            raise ValueError(f"link map does not attribute {symbol} to {expected_member}")

    print(f"PASS unchanged Thread function bodies: {thread_count}")
    print(f"PASS unchanged ThreadStore function bodies: {store_count}")
    print("PASS GUIDEXOS TLS has one bootstrap-object definition")
    print(f"PASS forced-link map object provenance: {len(required_map_origins)} symbols")
    print("SCOPE map provenance only; diagnostic executable must never be run")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"FAIL {error}", file=sys.stderr)
        raise SystemExit(1)
