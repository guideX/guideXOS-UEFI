# Phase 35P2 — Persistent App-Local Storage

## Status

Outcome A — the Phase 10 Persistent service is bound to a confined FAT16 namespace and passed requester replacement, App Model reset, same-image reboot, scope, stale-context, failure-propagation, and Temporary/Resource separation checks. The full ordinary build and required Phase 34-to-26 plus standalone App Model regressions passed. Managed Persistent storage remains unavailable to applications; managed Phase 35 was not resumed in this change, and Phase 36 writes are not included.

## Audited Phase 10 service behavior

Before this change, service 9 validated the generation-safe service context and the operation request, then every `Persistent` branch returned `ResourceUnavailable`. The service did not fall back to Temporary or RdskFS. Temporary remained an in-memory map keyed by exact ApplicationId and was cleared by App Model reset. Resource service 8 remained a separate immutable packaged-data service.

The existing path contract is retained: 1–192 UTF-16 units; segments are 1–64 units; paths must be relative and may not contain empty, `.` or `..` segments, colons, controls, or `< > \" | ? *`. Backslash and slash are treated as separators. Writes remain capped at 65,536 bytes and reads at 65,536 bytes per request.

## Volume policy and layering

Persistent uses only the explicitly identified Phase 35Q test volume. Selection requires exactly one AHCI device with serial `GX35Q0001`, 32,768 blocks of 512 bytes, readable and available media, a mounted FAT16 filesystem, volume ID `0x35355131`, and label `GX35Q TEST`. The implementation does not choose the first writable disk. If any identity or mount check fails, Persistent is unavailable.

The operation path is service 9 → `PersistentFatBackend` → FAT → `Disk` → AHCI. The backend constructs a private FAT instance and does not replace the boot `File.Instance`; no Persistent operation calls the AHCI driver directly. No host physical disk is attached.

## Namespace and confinement

The canonical root is `apps/persist`. Both components fit FAT's short-name limit in the current filesystem implementation. ApplicationId is authoritative logical identity obtained from the validated service context; the wire request has no ApplicationId field. The caller controls only the already-validated relative storage path.

Mapping is exact and injective over UTF-16 code units. The validated ApplicationId bound is 1–96 UTF-16 units; the current descriptor/model path only requires a nonempty bounded ID and does not impose a filesystem character set. An application directory is `A` plus a four-hex-digit UTF-16-unit length, followed by one 8-character uppercase-hex FAT 8.3 directory per two UTF-16 code units. Each value path is represented the same way with a `P` length prefix, ending in `VALUE.BIN`. Zero padding is disambiguated by the encoded length. The mapping preserves case, punctuation, Unicode, surrogate pairs, and even unpaired UTF-16 units; it is stable across process generations, builds, and reboot. The App Model's current descriptor/factory comparisons fold ASCII case when resolving IDs, while the storage encoding itself remains exact and distinct for case variants. Encoded components contain only uppercase hex and a fixed prefix, so caller separators and traversal text cannot become path separators or traversal components. FAT16 has no symlink or reparse-point facility.

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

The cleaned Storage35P2 build and full same-image QEMU validation passed. The harness ran the Phase 35Q raw/FAT persistence checks on a disposable 16 MiB AHCI image, including the six-storage-boot sequence and a final App Model verification boot on the same image. It restored and verified the original image baseline before cleanup.

The configured test volume was the unique AHCI device with serial `GX35Q0001`, geometry 32,768 × 512 bytes, FAT16, label `GX35Q TEST`, and volume ID `0x35355131` (decimal `892686641`). The backend rejected absent, duplicate, wrong-geometry, wrong-filesystem, wrong-label, and wrong-volume-ID candidates. It did not use an arbitrary writable disk. An unavailable backend maps to `ResourceUnavailable`; it does not fall back to Temporary or RdskFS. A mounted read-only backend permits reads and returns `ResourceUnavailable` for mutation. Failure-injected unavailable/unmountable backends returned `ResourceUnavailable` for Exists, Read, Write, Delete, and Enumerate.

The trusted fixture is `selftest.phase10.persistent/state.bin`, 32 bytes containing `0x35` through `0x54`, SHA-256 `BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D`. The initial diagnostic boot reported `Seeded, seedWrites=1`; the same-image verification boot reported `Verified, seedWrites=0` before its App Model read and returned the same digest. A present mismatch fails the proof without silently overwriting the data.

Both App Model boots reported backend available/writable, the selected volume, `apps/persist`, and the expected fixture digest. Namespace encoding, cross-scope isolation, stale-context rejection, replacement lifecycle, value and offset boundaries, Delete, Enumerate, reset persistence, Temporary separation/reset, and failure propagation passed. The reboot-delete assertion is intentionally only evaluated on the verification boot: it reported `FAIL` on the initial seed boot, where no prior boot's deletion is expected, and `PASS` after reboot. On verification, counters were `exists=34, read=44, write=16, delete=12, enumerate=4, namespace=112, scopeReject=1, staleReject=1, ioFailure=0, flushFailure=0, openHandles=0`. The first boot had one fewer Exists/Read/namespace derivation because it did not run the reboot-delete check.

The storage lifecycle cohort ran 25 replacement-instance iterations; each fresh requester performed Exists and an exact fixture Read. The bounded internal mutation set covered empty and one-byte values, same-size overwrite, shorter replacement, longer replacement, the 65,536-byte maximum, Delete/recreate, and delete persistence across the guest reboot. A 65,537-byte write was rejected. Reads proved offset zero, middle, exact end, beyond end, overflowed offset arithmetic, exact capacity, and short destination behavior. Missing values return `NotFound` for Read/Delete and `Success(false)` for Exists; an existing empty value reads as `Success` with zero bytes.

Failure-injection tests propagated FAT allocation/table, data, directory-entry, read, mount/read, and flush failures to typed service failures; injected flush failures did not return success. The read-only proxy read the fixture successfully while rejecting Write/Delete. No persistent file handles remained. Scope and stale-context counters each recorded one rejection, with no backend lookup for the stale context. Phase 10 Resource metadata/chunk/path, Temporary app-scope/reset, Phase 9 service, and Phase 11 clipboard checks passed in the same App Model diagnostic. `PHASE10_STORAGE_PERSISTENT_UNAVAILABLE_OK=0` is expected after binding: Persistent is now available, and its behavior is tested by the Phase 35P2 gates.

The reboot test does not claim power-loss atomicity. FAT is non-journaled; orderly filesystem sync plus explicit device flush and guest reboot persistence are proven, while sudden power loss, torn sectors, and crash-atomic multi-sector replacement are not.

## Final expanded proof and regressions

The final Storage35P2 kernel was rebuilt after adding the canonical built-in AppIds (`gxos.builtin.calculator`, `gxos.builtin.files`, `gxos.builtin.notepad`, `gxos.builtin.diskmanager`, and `gxos.builtin.console`) to the namespace collision set. The full same-image harness passed with this expanded set. The first App Model run seeded the fixture once and completed its lifecycle proof; after the guest reset, the verification boot reported `status=Verified`, `seedWrites=0`, and the exact expected fixture SHA-256 before running the storage checks. Both App Model runs reported `APP_MODEL_COMPLETE`, and the final image was restored to its original SHA-256 before cleanup.

On the verification boot, all Persistent operation gates passed: backend available/writable, namespace encoding, cross-scope denial, stale-context denial, lifecycle replacement, value and offset boundaries, durable Delete, failure propagation, scoped Enumerate, reset persistence, reboot Delete, Temporary separation, and Temporary reset clearing. Final diagnostics were `exists=34, read=44, write=16, delete=12, enumerate=4, namespace=112, scopeReject=1, staleReject=1, ioFailure=0, flushFailure=0, openHandles=0`. The service-context diagnostic reported zero active or leaked requests. The initial seed boot's `PHASE35P2_REBOOT_DELETE=FAIL` is expected because that boot has no prior reboot deletion to verify; the verification boot reported `PASS`.

The 25 requester-replacement iterations each performed Exists and exact fixture Read. The internal mutation tests covered empty, one-byte, small fixture, same-size overwrite, shorter replacement, longer replacement, maximum 65,536-byte value, rejected 65,537-byte value, Delete/recreate, and deletion persistence. Offset checks covered zero, middle, exact end, beyond end, arithmetic overflow, exact capacity, and short destination. Missing Exists returns `Success(false)`; missing Read/Delete returns `NotFound`; an existing empty file reads as `Success` with zero bytes. Write and Delete return success only after FAT sync and device flush.

Failure injection covered FAT data, FAT table, directory update, read/mount, unavailable media, read-only media, and flush. Mutations on unavailable or read-only media returned `ResourceUnavailable`; read-only media permitted reads. Injected write, FAT metadata, and flush errors propagated as typed service failures. There were no guest `#PF`, `#GP`, `#UD`, ABI panic, invalid-free, corrupt-free, or no-pages allocator diagnostics. No open file handles or service requests remained.

Phase 10 Resource metadata, chunks, path confinement, Temporary app scope, and reset controls passed. `PHASE10_STORAGE_PERSISTENT_UNAVAILABLE_OK=0` is expected in the Storage35P2 run because Persistent is now bound and tested; the standalone non-P2 App Model control still reported Persistent unavailable. Resource service 8 remained separate and its checks passed.

Required regression boots all completed with `DIAGNOSTIC_COMPLETE`, dispatch selected, and continuous execution entered. The guest return markers were Phase 34 → 34, Phase 33 → 33, Phase 32 → 32, Phase 31 → 31, Phase 30 → 30, Phase 29 → 29, Phase 28 → 28, Phase 27 pass, and Phase 26 → 42. The standalone App Model run completed with `APP_MODEL_COMPLETE`; lifecycle checks reported 15 passed / 0 failed and taskbar grouping reported 16 passed / 0 failed.

The ordinary `build.ps1` completed successfully. Managed artifact admission/staging reported 32 identities, descriptor agreement 32/32, and build-staging agreement 32/32. The ordinary build staged kernel SHA-256 was `FAC7115299097882195B868787DB664222A9685C907DCEACEF4A058F26BEFD82`, EFI SHA-256 was `15FDF42E60D72EFF1CC06D6DF97873AB782CCBE5AE516E948DF92EEBE57B33C3`, and ramdisk SHA-256 was `6E327DA354445DF0AA4511017F679C626440E9A7DD995526D31678A75C6948B8`. The Storage35P2 diagnostic kernel hash was `4EEE6FA87044829760ABCD07D8706B0BE1CFEC5B49FAD32429AF3D7F41421A7E`; the final standalone App Model diagnostic kernel hash was `53904011CD28A2BACB16161BC23DD2A15DCC5EAB401A6A0B56C58BA61AD25A0C`. Storage35P2-specific and per-regression diagnostic builds also completed successfully.

The disposable image began and ended at SHA-256 `2EFE22E39E112105854DBC4784887CE06A64A040EC98D50FB52DD7A7FE94B451`; the guest-written intermediate image hash was `7CEC5D464A3EA42F1421FC310F4DF171430718016350024F9324F1CA29178C47`. No host physical disk was attached.

Managed Phase 35 was not resumed here. The Persistent read blocker is closed by Outcome A and a read-only managed API can be a separate bounded follow-up. Managed Persistent writes and Phase 36 remain prohibited.

## Managed Phase 35 and Phase 36

Managed Phase 35 was not resumed in this change. The Persistent-read blocker is closed by Outcome A, so a public read-only managed API can proceed as a separate bounded follow-up. Managed Persistent writes remain prohibited; Phase 36 is a separate authority expansion.
