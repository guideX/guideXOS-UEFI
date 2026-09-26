# Phase 28 Managed User SDK

Phase 28 introduces the first reusable managed user-space SDK for isolated
NativeAOT applications. The project is `GuideXos.User` and targets the
repository-specific `guidexos-x64` runtime. A proof application in
`UserManagedSdkProof` references only this project and uses no raw Ring 3 ABI
types or operation numbers.

## Public surface

The intentionally small public surface is:

* `GuideXosRuntime`: ABI version discovery and `GuideXosResult` compatibility
  checks.
* `GuideXosApplication.TryGetCurrent`: the current application identity as an
  immutable `GuideXosApplicationIdentity` value.
* `GuideXosSystem.TryGetInformation`: a copied immutable
  `GuideXosSystemInformation` snapshot.
* `GuideXosClock.GetTimestamp` and `GuideXosClock.Frequency`: monotonic
  guideXOS ticks and their nonzero frequency.
* `GuideXosProcess.Exit` and `GuideXosProcess.FailFast`: current-process-only
  lifecycle operations.
* `GuideXosStatus` and `GuideXosResult`: bounded, non-exceptional status
  results.

There is no GUI, windowing, input, filesystem, networking, clipboard, shell,
notification, process-spawn, managed-thread, reflection, or dynamic-loading
API in this SDK.

## Boundary and wire contracts

Public calls are translated through private fixed-width structures in
`GuideXosInternalAbi.cs`. The current internal structures are:

* identity response: 40 bytes;
* service request: 32 bytes;
* System Information response: 128 bytes.

The PAL helpers are internal implementation details. The raw `int 0x80`
transport, operation numbers, service IDs, response padding, process-table
handles, `ApplicationServiceContext`, object addresses, and GS/TLS details are
not public SDK types. The kernel validates the request, copies the request and
response at the existing App Model boundary, and remains authoritative.

## Identity semantics

`GuideXosApplicationIdentity` contains a stable application fingerprint,
opaque lifetime token, application generation, process generation, and x64
architecture. The stable ID is a value for equality/grouping, not an
authorization capability. The lifetime token is never accepted as authority by
the public SDK.

The identity operation accepts only a writable response buffer. It has no
application-supplied owner, handle, pointer, or identity input. The kernel
derives the owner from the scheduled process, validates the current
`ApplicationInstance` generation, and copies a fixed 40-byte response out.
Teardown invalidates the process lifetime; a later generation receives a new
lifetime token. Reusing a stale instance or service context is rejected by the
same kernel registry/generation checks used by Phase 27.

The Phase 28 diagnostic verifies that four sequential process lifetimes for
the same application return distinct opaque lifetime values, and that a
replacement `ApplicationInstance` returns a fresh value while preserving the
stable application fingerprint. The values are compared only inside the
kernel diagnostic; they are not logged or accepted back as authority.

## System Information and time

System Information preserves the proven Phase 27 service-copy model. The SDK
validates structure version, exact response size, bounded text lengths,
architecture, memory ordering, thread count, and CPU percentage before
constructing its value type. No kernel-owned pointer or string crosses the
boundary.

The clock exposes monotonic ticks and frequency only. It does not claim wall
clock, `DateTime`, Windows QPC, or broad platform-time compatibility.

## Lifetime and errors

`Exit` terminates the current scheduled process with the supplied bounded code;
successful Exit does not return to its managed caller. `FailFast` terminates
the current process locally and leaves the kernel running. Both paths use the
existing process lifecycle and cleanup machinery; neither accepts a process
handle and neither can target another process.

Expected failures are returned as `GuideXosResult` values. The initial SDK
does not require managed exceptions for ABI mismatch, invalid buffers,
unsupported operations, invalid state, or transport failure. NativeAOT
exception support remains intentionally outside this contract.

The current proof is single-threaded. Managed threads, asynchronous services,
reflection, and general BCL/platform APIs remain unsupported until separately
proven for guideXOS.

## Allocation and replaceability

Identity, System Information, clock, Exit, and FailFast use stack-local fixed
width data and have no intentional managed allocation. A stable application
string is deliberately not exposed; the current identity is numeric and
allocation-free. The PAL and private wire layer can be replaced without
changing the application-facing API.

## Proof modes

`Ring3Phase28` runs four successful SDK lifetimes returning `28`, a typed ABI
compatibility failure returning `23`, an Exit diagnostic returning `73`, a
FailFast diagnostic, and a replacement SDK lifetime returning `28`. The
diagnostic also checks stale identity/context rejection and the Phase 27
cleanup counters. The raw Phase 27 managed System Information proof remains a
separate control and must still return `27`.
