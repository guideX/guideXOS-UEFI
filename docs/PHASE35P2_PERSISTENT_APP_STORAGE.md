# Phase 35P2 — Persistent App-Local Storage

## Status

Implementation and guest proof are in progress. This report will record the final outcome after the Storage35P2 QEMU run, regressions, and ordinary build complete. The public managed storage surface remains unavailable; no Phase 36 write authority is included.

## Audited Phase 10 service behavior

Before this change, service 9 validated the generation-safe service context and the operation request, then every `Persistent` branch returned `ResourceUnavailable`. The service did not fall back to Temporary or RdskFS. Temporary remained an in-memory map keyed by exact ApplicationId and was cleared by App Model reset. Resource service 8 remained a separate immutable packaged-data service.

The existing path contract is retained: 1–192 UTF-16 units; segments are 1–64 units; paths must be relative and may not contain empty, `.` or `..` segments, colons, controls, or `< > \" | ? *`. Backslash and slash are treated as separators. Writes remain capped at 65,536 bytes and reads at 65,536 bytes per request.

## Volume policy and layering

Persistent uses only the explicitly identified Phase 35Q test volume. Selection requires exactly one AHCI device with serial `GX35Q0001`, 32,768 blocks of 512 bytes, readable and available media, a mounted FAT16 filesystem, volume ID `0x35355131`, and label `GX35Q TEST`. The implementation does not choose the first writable disk. If any identity or mount check fails, Persistent is unavailable.

The operation path is service 9 → `PersistentFatBackend` → FAT → `Disk` → AHCI. The backend constructs a private FAT instance and does not replace the boot `File.Instance`; no Persistent operation calls the AHCI driver directly. No host physical disk is attached.

## Namespace and confinement

The canonical root is `apps/persist`. Both components fit FAT's short-name limit in the current filesystem implementation. ApplicationId is authoritative logical identity obtained from the validated service context; the wire request has no ApplicationId field. The caller controls only the already-validated relative storage path.

Mapping is exact and injective over UTF-16 code units. An application directory is `A` plus a four-hex-digit UTF-16-unit length, followed by one 8-character uppercase-hex FAT 8.3 directory per two UTF-16 code units. Each value path is represented the same way with a `P` length prefix, ending in `VALUE.BIN`. Zero padding is disambiguated by the encoded length. The mapping preserves case, punctuation, Unicode, surrogate pairs, and even unpaired UTF-16 units; it is stable across process generations, builds, and reboot. Encoded components contain only uppercase hex and a fixed prefix, so caller separators and traversal text cannot become path separators or traversal components. FAT16 has no symlink or reparse-point facility.

The in-code collision set covers built-in IDs, punctuation, slash/backslash/control characters, similar IDs, case distinctions, Greek characters, emoji surrogate pairs, an unpaired surrogate, maximum-length IDs, and a near-collision. It verifies stable mapping and pairwise distinct paths.

Directory creation happens only on Write. Exists, Read, and Enumerate do not provision missing directories. Enumerate walks only the derived app directory, accepts only encoded path directories and `VALUE.BIN`, validates decoded paths again, is ordinal-sorted, and is bounded to 64 values, 128 entries per directory, and 8,192 visited nodes.

## Trusted fixture

The deterministic fixture is owned by `selftest.phase10.persistent`, at `state.bin`. Its 32 bytes are `0x35` through `0x54`; expected SHA-256 is `BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D`.

The trusted kernel/App Model diagnostic uses the same `PersistentFatBackend` write/read/sync path. If missing, it writes and verifies the fixture. If present, it verifies exact length, bytes, and SHA-256 without rewriting. A mismatch or I/O error fails the proof and is not silently repaired. The reboot harness requires a preflight `Verified` state with zero seed writes before requester verification.

## Operation semantics and durability

- Exists returns success/false for a missing file.
- Read returns `NotFound` for a missing file; an existing zero-length file returns success, zero bytes, and end-of-value. Reads use the existing offset/capacity contract, reject offsets beyond end and invalid/overflowing ranges, and copy only the bounded returned bytes.
- Write replaces the whole value, including same-size, shorter, and longer replacements. It copies the input, creates parent directories through FAT, writes via FAT, synchronizes FAT, and flushes the disk before returning success. Empty values are supported. Values above 65,536 bytes are rejected.
- Delete removes an existing file and flushes FAT metadata before success. A missing file returns `NotFound`.
- Enumerate is confined to the caller's AppId root and reports bounded, deterministic ordinal-sorted paths and lengths.

A failed write/delete/flush is returned as a typed service failure. Read-only media can be read if mounted; mutation returns `ResourceUnavailable`. Unavailable or unmounted media returns `ResourceUnavailable`.

Handles are scoped to an operation and disposed; no persistent handles are cached. Persistent data is not removed by process cleanup, ApplicationInstance cleanup, or App Model reset. Explicit Delete removes the value. FAT is non-journaled: orderly write plus explicit flush is covered; sudden power loss, torn sectors, and multi-sector atomic replacement are not claimed.

## Proof results

Pending Storage35P2 guest run and regressions. The final report will include exact seed-write count on reboot verification, requester replacement, reset and reboot results, cross-AppId and stale-context rejection, failure injection outcomes, fault/allocation counters, handle/request counts, Phase 10/34 and App Model results, and build/image hashes.

## Managed Phase 35 and Phase 36

No managed Persistent read API is being resumed in this change unless every required Phase 35P2 gate passes. Managed writes remain prohibited and Phase 36 remains a separate authority expansion.
