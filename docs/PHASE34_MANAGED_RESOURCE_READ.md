# Phase 34: Managed, scoped resource reads

## Result

Phase 34 adds a small read-only resource API to the public `GuideXos.User`
SDK. A NativeAOT application can name a bounded Phase 10 resource key, but it
cannot supply an application ID, package ID, path, stream, storage namespace,
or backing-store handle. The kernel derives the caller from the scheduled
Ring 3 process and obtains a fresh Phase 10 context for that process's owning
`ApplicationInstance` on every request.

The resource service remains authoritative for lookup and scope. The service
returns copied bytes, the kernel copies them into a validated caller buffer,
and the SDK returns a managed `byte[]`. No kernel or package pointer escapes.

## Audited Phase 10 contract

The live contract is in `guideXOS/OS/ApplicationServices.cs`,
`guideXOS/OS/ApplicationStorageServices.cs`,
`guideXOS/OS/ApplicationResourceServices.cs`, and
`guideXOS/OS/ApplicationServiceRegistry.cs`.

- `ApplicationServiceId.Resources` is service ID 8. Storage is a separate
  service, ID 9.
- Resource identity is an application-relative resource key. Lookup uses the
  validated context's `ApplicationId` together with the key. The resource
  API does not accept a path.
- A key is 1–96 ASCII characters. Allowed characters are ASCII letters,
  digits, `.`, `-`, and `_`; empty, `.` and `..` are invalid. Separators,
  colons, controls, and non-ASCII characters are rejected.
- `ApplicationResourceReadRequest.MaxChunkLength` is 65,536 bytes per read.
  Phase 10 has no total-resource-size constant. Phase 34 deliberately imposes
  a 65,536-byte total ceiling on this one-shot managed API.
- Reads carry a signed 64-bit offset and a positive 32-bit maximum byte
  count. The service may return fewer bytes at end of resource and reports
  the actual offset, count, key, and end flag.
- A missing key returns `ApplicationServiceResultCode.NotFound`. Invalid
  identities and read parameters return `InvalidRequest`. A stale context
  returns `InvalidContext`.
- `GetMetadata` and `Read` are synchronous calls. They do not create a
  persistent resource handle or retain a caller pointer. `Read` allocates a
  service-owned byte array, and `ApplicationResourceReadResult.Create`
  copies that array again.
- Resource lookup is not reset by the Phase 10 storage reset path. The
  current resource adapter is initialized with the App Model service and has
  no write, delete, enumeration, or per-caller resource state.
- Application service contexts contain an instance handle, application ID,
  and capabilities. The registry checks the instance generation and
  lifecycle state on use. Phase 34 reconstructs and validates this context
  for each syscall rather than keeping it in the process or SDK.

## Selected resource and scope

The existing deterministic Phase 10 adapter contains one diagnostic fixture:

| Field | Value |
| --- | --- |
| Application ID | `selftest.phase8.services` |
| Resource key | `diagnostic.fixture` |
| Length | 54 bytes |
| Bytes | ASCII/UTF-8-compatible bytes for `guideXOS Phase 10 bounded application resource fixture` |
| SHA-256 | `742D9840D8B8F1FC36C9B59AB85501C43DC016C2784C6272E8EEB078C92911DB` |

The live Phase 10 implementation stores this fixture as an in-memory
`ResourceEntry`; it is not opened from a filesystem or ramdisk. Its scope is
the `ApplicationId` attached to that entry. A request from a differently
identified application is resolved in that application's scope and returns
`NotFound`, matching Phase 10's existing vocabulary. Phase 10 defines no
shared-resource behavior.

## Public SDK contract

`GuideXosResources.TryReadBytes(string resourceName, out byte[] data,
out GuideXosResourceResultCode resourceResult)` is the only new public
resource operation. `GuideXosResourceResultCode` mirrors Phase 10's current
numeric result codes. `GuideXosResult` reports transport/ABI failures;
`resourceResult` reports the Phase 10 resource result. On any failed
`GuideXosResult` or non-success resource result, `data` is null.

The SDK locally rejects an empty, oversized, non-ASCII, path-like, or
otherwise disallowed key as `GuideXosStatus.InvalidArgument`. It allocates
exactly the metadata length only after checking it is at most 65,536 bytes.
An unreadable resource or a resource larger than this API's one-shot bound
returns `GuideXosResult.Success` with the typed resource result
`ResourceUnavailable` and a null data value.
For an empty resource it returns an empty managed array without issuing a
zero-length Phase 10 read. Text decoding is left to the application; the SDK
returns bytes and imposes no encoding.

The returned `byte[]` is a process-owned copy. The resource service's
temporary read array, the Phase 10 result copy, the ABI transfer, and the
managed array are separate storage. The service and kernel retain no pointer
to the managed array. This intentionally adds a bounded managed allocation;
it is not a zero-allocation API.

## Wire protocol and validation

The public SDK reaches the existing Ring 3 service-request transport. The
internal request is 152 packed bytes; the response is 44 packed bytes.

- Service 8, operation 1: metadata query.
- Service 8, operation 2: complete bounded read from offset zero.
- The request contains ABI version, service and operation IDs, exact record
  size, key length, response/data capacities, reserved bits, caller
  destination addresses, expected resource length, and a fixed 96-byte key.
- The response contains ABI version/size, Phase 10 result code, readable
  flag, resource length, offset, byte count, end-of-resource flag, and
  reserved bits.
- The record and capacity fields are 32-bit unsigned integers. Expected and
  returned resource lengths and offsets are 64-bit unsigned integers. The
  key length is a count of ASCII bytes, which equals the SDK's allowed string
  code-unit count.
- Resource name bytes are ASCII. Lengths and capacities are checked before
  backend lookup; arithmetic for the complete writable user range is checked
  for overflow. The entire response range and read destination must be
  writable before the resource service is called.
- The read operation obtains metadata again under the fresh context. It
  requires the revalidated length, expected length, and exact destination
  capacity to agree, then calls Phase 10 `Read` for the full resource at
  offset zero. Key, offset, byte count, and end state are checked before the
  first caller-buffer copy. A mismatch returns a typed failure and copies no
  resource bytes.
- The kernel validates the resource adapter's returned value and copies
  bytes to the user destination only after the service call completes. It
  stores no user pointer, application resource handle, or service context.

The query/read pair is synchronous and all-or-nothing for this API. A
resource that grows or changes between metadata and read produces a length
conflict; the caller must request it again. The fixture is immutable in the
current adapter, so this is defense in depth for later Phase 10 adapters.

## Isolation, invalid input, and lifetime

The wire format carries no App ID or package identity. The kernel obtains the
current scheduled process, resolves its `OwningApplicationInstance`, creates
a fresh `ApplicationServiceContext`, validates Resources access, and checks
that the context application ID still matches the resolved instance. Only
then does the Phase 10 adapter look up `(context.ApplicationId, key)`.
Resource callers must be in the App Model `Running` or `Activated` lifecycle
state. A service context is request-local: no stale process, instance, or
context is saved for a later read.

The success proof checks the exact fixture bytes, a missing key (`NotFound`),
empty and maximum-plus-one names, a path-like name, and repeated reads. The
diagnostic kernel proof also exercises an invalid raw operation ID, invalid
record size, empty and oversized raw keys, path-like key bytes, a correct
capacity, a one-byte-short capacity, an over-limit raw read, and invalid
destination ranges. It checks that invalid and stale requests do not increment
the resource adapter's lookup counter.

The cross-scope proof requests the fixture under a different application ID
and requires `NotFound`. The stale-owner proof schedules a managed requester
whose owning `ApplicationInstance` has already been terminated and requires
`GuideXosStatus.InvalidState` without backend lookup. The kernel proof also
keeps and reuses an old Phase 10 context after owner cleanup; metadata and
read calls must return `InvalidContext`. A fresh requester derives a new
context and can read again.

The FailFast variant reads and validates the bytes before terminating. The
kernel then verifies owner, process, request, and context cleanup; it launches
a replacement with fresh authority and requires another successful read.
The Phase 34 cohort runs four normal successful lifetimes and 25 additional
successful read lifetimes. Since Resources uses direct synchronous service
calls, expected outstanding resource handles and active requests are zero.

## Deliberately unexposed operations

Phase 34 does not expose app-local storage reads or writes, temporary storage,
storage mutation, directory enumeration, arbitrary file opens, streams,
resource writes, deletes, or cross-application sharing. Storage service 9
remains governed by its separate Phase 10 contract and APIs.

## NativeAOT and deterministic admission

Five separately compiled Phase 34 modes are staged through the existing
managed artifact generator: success, FailFast, stale owner, cross-scope, and
malformed raw request. The descriptor is generated from each normalized
executable and its NativeAOT map. The kernel allowlist identity is generated
from those descriptors; no digest is transcribed by hand.

The Phase 34 wire validator checks the request and response layout. The
artifact verifier checks exact mode flags and descriptor digest, map/link
response closure, absence of direct kernel imports, relocation and TLS
policies, and RWX sections. It also rejects executable-byte mutation,
descriptor-digest mutation, and wrong proof flags/mode. The compact build
archive retains each EXE, map, linker response, and build record while
discarding per-mode NativeAOT intermediates after capture.

The managed proof uses public `GuideXos.User` APIs only. Its source contains no
raw ABI calls. Its content check compares all 54 bytes directly; no implicit
UTF-8 decoder or filesystem API participates.

The first success image is 709,120 bytes. Its NativeAOT map is 1,086,751
bytes versus 1,079,642 bytes for the Phase 33 success map (+7,109 bytes).
The inspected success maps retain the same records as Phase 33: 35 GC helper
object rows, 530 rows containing `Exception`, 12 `ThreadStatic` rows, 25
`GCStaticBase` rows, 39 `System.IO` name matches, and four loader-related name
matches. Both maps have zero `FileStream` and network name matches. The new
`TryReadResourceBytes` methods appear in the Phase 34 application object.
`wmain` remains the native entry at RVA `0x1430`; managed `Program.Main` is at
RVA `0x660d0`. The Phase 34 linker map reports no foreign link inputs. The
image has six sections, zero imports, zero direct kernel imports, zero RWX
sections, no relocation directory, and no TLS directory.

## Runtime proof record

Fresh QEMU validation completed with `RING3_PHASE34_COMPLETE=1`.

- The first managed read returned 54 bytes. `Program.Main` compared every byte
  with the expected fixture and returned 34. The kernel logged a 54-byte copy
  into the caller buffer and copied the response record out.
- Four primary lifetimes returned `34`; the additional 25 resource-read
  lifetimes passed. A separate FailFast requester returned `-1` after a
  validated read; its requester, process, context, and requests were cleaned.
  A replacement requester derived fresh authority, re-read the fixture, and
  returned `34`.
- Missing resource returned typed `NotFound` (3) without a process fault.
  Empty, 97-character, and path-like names returned SDK `InvalidArgument`;
  the empty and oversized kernel records were also rejected before lookup.
- The cross-scope requester received `NotFound`. A stale owner request returned
  `InvalidState` before lookup, and reusing its old Phase 10 context returned
  `InvalidContext`.
- Kernel request validation accepted an exact 54-byte capacity and rejected a
  53-byte capacity, an over-limit read, an invalid operation, malformed record
  size, empty and 97-byte keys, and a path-like key. Zero, unmapped, and
  overflowed destination ranges were rejected. The malformed managed variant
  also issued a raw invalid operation through service 8; the kernel returned
  `InvalidRequest`, the SDK surfaced `InvalidArgument`, and the backend lookup
  delta remained zero.
- Repeated reads had identical bytes and length. The service retains no
  resource handles; active requests ended at zero. Cleanup balances were true,
  and `freeInvalid`, `freeCorrupt`, and `freeNoPages` were all zero. No #PF,
  #GP, #UD, or ABI panic marker appeared.
- The five Phase 34 executables were rebuilt twice from the same source. Their
  final lengths/digests were stable: success 709,120 bytes,
  `D1D7AFFBF5C705D01D41508F343AB5E7FFF522B3C8679C8508C1B448875D8893`;
  FailFast 708,096 bytes,
  `36603FEE21810E2FF13DAC4FF1693CFFC5AE36DBA7FB5A27E8F41F4704BFBF68`;
  stale owner 707,584 bytes,
  `527112F8CD3B9708809E1502FBB255B42E0C0A05430A279655C75EAB923F3EB1`;
  cross-scope 707,584 bytes,
  `67ECC8FB13020EE7C82C126CCB0E43B475FC0B05515D1BF9C7581ACDEB7BAAEA`;
  malformed 708,096 bytes,
  `C48FA1B4D24FCDBFAFF993CC54C636404B247E301C1922AE8EE6986915D05E74`.
  Executable, descriptor, generated admission identity, and ramdisk staging
  agreed for all 32 managed proof artifacts. Mutation checks rejected changed
  executable bytes, descriptor digest, and proof flags/mode.
- The final ordinary build completed with all managed identities generated
  before kernel compilation. It built the kernel, regenerated the ramdisk,
  staged the EFI image, and reported `Build Complete!`. The final ramdisk SHA-256
  was `6E327DA354445DF0AA4511017F679C626440E9A7DD995526D31678A75C6948B8`.
- Fresh regressions passed: Phase 33 returned 33 (30 successful managed Main
  returns), Phase 32 returned 32, Phase 31 returned 31, Phase 30 returned 30,
  Phase 29 returned 29, Phase 28 returned 28, Phase 27 returned 27, and Phase
  26 returned 42.
- App Model and Phase 10 regressions passed. Lifecycle was 15/15; grouping was
  16/16; stale taskbar projection and Legacy backend calls were zero. Phase 8,
  Phase 9, Phase 10 resource/chunk/storage, and Phase 11 clipboard checks were
  green. Bounded Start-menu and foreground/taskbar smokes each completed 2/2
  with graphics invariants true.

The managed byte array is an intentional bounded allocation. Phase 34 adds no
GC, exception, TLS, writable-static, filesystem, or networking helper closure
beyond the Phase 33 SDK baseline. The success artifact has zero Windows or
direct kernel imports, zero RWX sections, and no relocation or TLS directory.

## Security invariants

1. The current process and owning `ApplicationInstance` are kernel-derived.
2. The application ID used for lookup comes from a fresh validated App Model
   context, never from the managed request.
3. Resource identity is a bounded ASCII key, not a filesystem path.
4. Phase 10 performs the authoritative application-scoped lookup.
5. Kernel and package pointers do not cross into managed code.
6. Resource contents are copied, and no caller pointer is retained.
7. Stale owner/context requests fail before backend lookup.
8. A cross-scope key request returns Phase 10's `NotFound` result.
9. Malformed or unwritable output ranges fail before any resource copy.
10. Phase 34 exposes no filesystem, storage mutation, or resource mutation.
