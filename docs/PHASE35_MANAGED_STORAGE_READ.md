# Phase 35 Managed Storage Read Audit

**Status: Outcome C / blocked before SDK implementation.** The selected
Phase 10 backend has no usable read primitive for its `Persistent` namespace.
`CSharpApplicationStorageService.Read` validates the caller and request, then
returns `ResourceUnavailable` for every persistent read before looking up a
value. The only populated namespace is `Temporary`, which Phase 10 explicitly
distinguishes and Phase 35 must not expose as app-local storage. No Phase 35
managed API, wire operation, artifact, or write authority was added.

This audit is based on `guideXOS/OS/ApplicationServices.cs`,
`ApplicationStorageServices.cs`, `ApplicationServiceRegistry.cs`,
`ApplicationResourceServices.cs`, `Kernel/Misc/Ring3Abi.cs`,
`Kernel/Misc/Ring3ProofPhase34.cs`, and the accepted Phase 10 contract in
`APP_MODEL_CONVERGENCE.md`.

## Live Phase 10 contract

| Property | Current behavior |
| --- | --- |
| Storage service ID | `9` (`ApplicationServiceId.Storage`, name `storage`) |
| Resource service ID | `8` (`resources`), a separate immutable package-data domain |
| Storage operations | `Exists`, `Read`, `Write`, `Delete`, bounded `Enumerate` |
| Namespaces | `Persistent = 0`, `Temporary = 1` |
| Selected implementation | `CSharpApplicationStorageService`, an in-memory adapter |
| Persistent backend status | The accepted Phase 10 audit identifies `RdskFS` persistent write/delete as no-op operations without safe verification; the adapter returns unavailable instead of claiming success. |
| Namespace capacity | 32 ApplicationIds |
| Entries per namespace | 64 |
| Persistent operations | After context and request validation, all return `ResourceUnavailable`; no temporary fallback |
| Temporary operations | In-memory operations are implemented for the five methods above |
| Production persistent writes | Not supported; write and delete return `ResourceUnavailable` |
| Backend path | No persistent path is read or written by this adapter. Its temporary backend is an in-memory array keyed by ApplicationId. |

The typed Phase 10 API has no numeric operation IDs and no storage operation
is currently routed through the Ring 3 syscall ABI. The existing public
`GuideXos.User` SDK contains the Phase 34 `GuideXosResources` API, but no
storage API. Consequently there is no Phase 35 wire request or response size
to report.

### Key/path rules

The Phase 10 contract calls the lookup value a relative path, not a logical
resource key. It is a .NET string. It must contain 1–192 UTF-16 code units;
each path segment must contain 1–64 code units. Both `/` and `\\` are
separators. Absolute/rooted forms, any `:`, empty segments, repeated or
trailing separators, `.` and `..` segments, control characters, and
`<`, `>`, `"`, `|`, `?`, `*` are rejected. Other characters, including
non-ASCII characters and spaces, are accepted by the current validator.
There is no canonicalization or case folding: the backend compares strings
code-unit by code-unit, so names are case-sensitive. There are no named
reserved keys; the `.` and `..` segment shapes are forbidden. Invalid paths
are rejected before a backend operation.

### Values and read semantics

`ApplicationStorageWriteRequest.MaxPayloadLength` is 65,536 bytes.
`ApplicationStorageReadRequest.MaxChunkLength` is also 65,536 bytes; a read
requires a nonnegative offset and a positive maximum length. Reads may be
chunked using offset and maximum-bytes requests. There is no metadata/query
size operation. The writer is the only current population path and caps each
value at 65,536 bytes, so that is the maximum value size represented by this
implementation. The backend also bounds each namespace to 64 entries but
does not define a byte quota for the namespace.

Empty values are valid: a zero-length write stores an empty byte array and a
read at offset zero succeeds with zero bytes and end-of-entry set. A missing
temporary entry is distinct and returns `NotFound`. An offset beyond the end
returns `InvalidRequest`; offset exactly at the end succeeds with an empty
terminal chunk. A short positive read is allowed and reports its returned
length and whether it reached the end. Persistent reads do not reach lookup or
these entry semantics; they return `ResourceUnavailable`.

The temporary backend copies input bytes when `Write` stores them. A read
copies the requested range out of the backend entry, then
`ApplicationStorageReadResult` copies that range again. These are kernel/App
Model copies only; no managed storage caller exists to test returned-array
mutation isolation.

Relevant live result codes are `Success`, `NotFound`, `InvalidRequest`,
`InvalidContext`, `Unsupported`, and `ResourceUnavailable`, from
`ApplicationServiceResultCode`. The live enum values are `Success=0`,
`InvalidContext=1`, `InvalidRequest=2`, `NotFound=3`,
`ResourceUnavailable=4`, `PermissionDenied=5`, `Unsupported=6`,
`Conflict=7`, `Cancelled=8`, `InvalidState=9`, `UnsupportedTarget=10`, and
`BackendFailure=11`. Invalid/stale service context is checked before storage
lookup. There is no managed enum because there is no managed storage API.

## Scope, lifetime, and reset

The adapter stores temporary namespace entries under `context.ApplicationId`
and compares ApplicationIds exactly. Two live instances with the same AppId
share the namespace; a different AppId cannot find the entry. Instance
termination does not clear the namespace. The Phase 10 self-test demonstrates
that a temporary entry remains after requester termination and is visible to
a replacement instance with the same AppId.

At the App Model layer, each operation calls
`ApplicationServiceRegistry.TryValidateContext` for service 9. That verifies
the registered service and capability, resolves the generation-safe
`ApplicationInstanceHandle`, checks that the context AppId still matches the
instance descriptor, and rejects invalid lifecycle states. The storage
adapter then derives its namespace lookup from that validated context AppId.
The Phase 35 Ring 3 rule would additionally require the kernel to derive the
currently scheduled process, owning ApplicationInstance and fresh service
context; no storage syscall path exists yet, so that managed/kernel path has
not been implemented or proven.

`ResetTemporaryForAppModel` clears every temporary namespace and entry.
`ResetForAppModel` invokes it, and the Phase 10 self-test verifies that
previously stored values are gone after reset. The App Model diagnostics also
reset this store during setup and cleanup. There is no disk-backed persistent
value, no reboot persistence promise, and no reboot-survival result. The
temporary adapter is volatile process memory; normal application termination
preserves it, while explicit App Model reset/reinitialization clears it.

## Fixture audit

No valid Phase 35 fixture exists in the Persistent namespace.

* The Phase 10 resource fixture `diagnostic.fixture` for
  `selftest.phase8.services` belongs to service 8 and is not storage.
* The storage self-test writes `state.bin` with bytes `01 02 03` in
  `Temporary`; it later resets that namespace.
* The termination-survival case writes `survive.bin` with byte `04` in
  `Temporary`; cleanup resets that namespace too.
* Persistent `state.bin` write, delete, read, exists and enumerate requests
  report `ResourceUnavailable` rather than seed or retrieve a value.

Therefore this audit selects no ApplicationId, persistent key, or persistent
value/hash. Reusing one of the temporary values would test a different
authority and reset contract than the one requested.

## Phase 35 decision

No `GuideXosStorage` or `GuideXosApplicationStorage` API was added. No
`TryWrite`, delete, clear, enumeration, temporary method, path method, storage
handle, AppId selector, or raw ABI API was added. The SDK search confirms the
absence of storage symbols. There is no Phase 35 artifact cohort, managed
allocation/copy proof, cross-scope syscall proof, stale-requester syscall
proof, reproducibility run, artifact admission, or Phase 35 runtime marker.

The missing prerequisite is a supported Phase 10 Persistent read backend and
a deterministic value created by a trusted setup path. Once that exists, the
smallest safe managed surface can be a bounded read-only method; its request
must carry only the bounded path and data-buffer description, while the
kernel derives the scheduled process, owner, AppId and fresh storage context.
Until then, returning `ResourceUnavailable` from a new managed API would not
prove that an application can read its own stored value, and exposing
Temporary would collapse two explicitly different Phase 10 namespaces.

Phase 36 writes remain out of scope. Persistent writes first need an actual
backend contract with bounds, copy-before-commit, failure atomicity, scope,
reset and persistence semantics.

## Validation record

* Repository preflight: `main` at `feaca8a1d27e725cc8141c0c952070c1eeee8b01`,
  two commits ahead of `origin/main`, with a clean root worktree before this
  audit document.
* Nested `out/rt`: HEAD `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, detached,
  65 pre-existing dirty/untracked paths. No runtime source edits were made.
* A normal `run_uefi_validation.ps1 -AppModel` build was started, but its
  prerequisite pipeline serially rebuilt the managed proof cohort and was
  stopped during Phase 30 staging after more than 12 minutes, before producing
  an App Model kernel. No source/runtime failure had been reported at the stop.
* A follow-up `-AppModel -SkipBuild` boot reused the existing production ESP,
  which entered `UEFI_CONTINUOUS_DESKTOP` rather than App Model mode. It was
  stopped at heartbeat frame 600 and provides no App Model or Phase 10 runtime
  regression evidence. The accepted Phase 10 runtime evidence remains the
  previously recorded App Model result in `APP_MODEL_CONVERGENCE.md`; it was
  not rerun for this audit.
* No full ordinary build, Phase 35 runtime proof, artifact validation, or
  broader Phase 26–34/App Model/Desktop regression cohort was run. There was
  no Phase 35 code to build, and the persistent-storage blocker was found in
  the authoritative service implementation before any SDK change.
