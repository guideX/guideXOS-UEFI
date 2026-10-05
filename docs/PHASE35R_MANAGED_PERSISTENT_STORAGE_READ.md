# Phase 35R: Managed Persistent Storage Read

Phase 35R adds one managed capability over the existing Phase 10 Persistent
backend: a Ring 3 application can copy a value from its own Persistent
application namespace into an independently owned managed `byte[]`.
Persistent writes, deletion, enumeration, Temporary storage, filesystem
operations, and volume selection remain outside the public SDK.

## Public API

```csharp
GuideXosStorage.TryReadBytes(
    string path,
    out byte[] value,
    out GuideXosStorageResultCode result)
```

The call reads from Persistent at offset zero. `path` is a relative path of 1
to 192 UTF-16 code units, with segments no longer than 64 units. Rooted paths,
empty segments, `.` and `..`, colons, control characters, and
`< > " | ? *` are rejected locally and checked again by the kernel.

The bounded `GuideXosStorageResultCode` maps Phase 10 service results 0–11:
Success, InvalidContext, InvalidRequest, NotFound, ResourceUnavailable,
PermissionDenied, Unsupported, Conflict, Cancelled, InvalidState,
UnsupportedTarget, and BackendFailure. InvalidBuffer (12) describes a Ring 3
user-buffer rejection. FAT and AHCI internal result values are not exposed.

There is no size query. Phase 10 values are capped at 65,536 bytes, so the SDK
uses one 65,536-byte `stackalloc` scratch buffer and issues one full-capacity read. It
returns `Array.Empty<byte>()` for an existing empty value, an exact-sized copy
for shorter values, and the scratch array only when the value itself is exactly
the maximum size. A full-capacity response that does not mark end-of-value is
rejected as ResourceUnavailable instead of being returned as truncated data.
Managed allocation failures map to ResourceUnavailable; no unmanaged fallback
is used.

## Ring 3 boundary

Syscall operation 7 has one meaning: read my Persistent value. Its packed
request is 436 bytes and its packed response is 24 bytes. The request carries
ABI version, operation id 1, record and path lengths, data and response
capacities, caller-owned response/data pointers, a zero-only offset, and a
fixed 192-unit UTF-16LE path. The response carries the service result, bytes
read, and end-of-value. Neither record contains an AppId, handle, context,
storage kind, root, volume, filesystem object, or kernel pointer.

Before reading, the kernel validates the request shape and path, then checks
that both complete output ranges are canonical, writable user memory in the
current process address space and that they do not overlap. The kernel derives
the process from the scheduled Ring 3 task, obtains its owning
`ApplicationInstance`, validates its descriptor `ApplicationId`, and creates
a fresh Storage context. Only Running and Activated instances may read;
Phase 35 does not expand the existing lifecycle rules. The operation fixes the
namespace to Persistent and the offset to zero. It copies bytes only after the
backend result and byte count pass validation.

The production path remains:

```text
Ring 3 app → dedicated syscall 7 → Phase 10 Storage service 9
           → Persistent FAT backend → FAT16 → disk → AHCI
```

Service 9's internal Read accepts a bounded count up to 65,536 and returns
BytesRead plus EndOfValue. A capacity of 31 for the 32-byte fixture returns 31
bytes with EndOfValue false; exact capacity 32 returns all bytes with
EndOfValue true. The managed API itself always requests the full 65,536-byte
bound, so its successful result is complete.

## Proof coverage

The separately compiled NativeAOT proof uses only `GuideXosStorage` for
managed reads and runs as `selftest.phase10.persistent`. It reads `state.bin`,
whose expected bytes are `0x35` through `0x54` and whose expected SHA-256 is
`BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D`. The
proof mutates its first returned array and rereads the value, checks a valid
missing path and an existing empty path, rejects six invalid paths before
service dispatch, and reads the internal 65,536-byte boundary diagnostic.
Trusted setup creates `empty.bin` when absent and a deterministic `max.bin`
value for the proof; both are removed after the run. A 65,537-byte write
request is rejected by Phase 10 before reaching the backend.

The success artifact performs one internal diagnostic pass using capacities
32 and 31 before exercising the public API. A separate malformed artifact
sends wrong record length, invalid operation, inconsistent path length,
zero capacity, traversal path, unmapped destination, and overflowing
destination requests through the same dedicated syscall. Those malformed
requests must fail before service lookup or data copy. Other artifacts prove
FailFast cleanup/replacement, stale-owner rejection, and caller-derived
cross-AppId isolation.

The boot proof runs four normal requester lifetimes and 25 additional
successfully cleaned lifetimes, checks a fresh requester after FailFast, and
repeats a managed read after App Model reset. Its reset control confirms
Temporary is cleared while Persistent remains available. The same-image reboot
run requires the fixture to be present on the second boot with zero fixture
seed writes, then launches a fresh managed requester against it.

## Artifact admission

Five variants use the existing H2c+ generated identity pipeline. Each final
canonical executable is paired with its GXMI descriptor, build staging,
generated kernel identity, compiled allowlist entry, and ramdisk staging.
Phase 35 variants have distinct proof flags. The verifier rejects executable
byte changes, descriptor digest changes, and wrong mode flags. The NativeAOT
map/link audit records PAL imports and compares dependency changes with the
Phase 34 resource-read artifact. The managed read surface introduces no file
stream, filesystem enumeration, network, reflection, or dynamic-loader API.

The executable serial evidence and artifact audit are recorded with the
Phase 35R closeout. Historical Phase 35 reports are left unchanged.

## Allocator follow-up

The preserved Phase 35R run remains Outcome F. Its exact 25-lifetime increase
is 850 pages, or 34 pages per successful requester. Existing diagnostics show
the same per-lifetime increase under the `Unknown` tag and in the one-page run
bucket. Tracked managed VM mappings, image pages, page tables, and user stacks
return to zero-live counts after cleanup. The per-requester SDK scratch is
stack allocated; each 32-byte successful result is one managed `byte[]`.

These observations do not yet identify the retained allocator runs or their
allocation callers. No-read, one-read, two-read, missing-read, raw-buffer, and
GC controls remain necessary before assigning a producer or selecting a fix.
See [Phase 35R1 Allocator Lifetime Closeout](PHASE35R1_ALLOCATOR_LIFETIME_CLOSEOUT.md)
for the evidence captured so far and the open gates.
