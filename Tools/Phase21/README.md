# Phase 21 NativeAOT `GUIDEXOS` platform identity and GC boundary

Phase 21 establishes a real NativeAOT source path for guideXOS and isolates
the workstation GC OS boundary. It is a source-level boundary proof; it does
not create a Git branch and it does not produce a managed runtime pack.

The pinned runtime checkout is the ignored `out/rt` cache at the Phase 20
commit. `apply_phase21_patch.ps1` verifies that commit, verifies the expected
stock source-file hashes, applies deterministic source transforms, installs
only the repository-owned guideXOS runtime templates, and records the exact
runtime diff. A clean checkout is required before every application.

Apply the source path with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Phase21\apply_phase21_patch.ps1
```

Run the semantic GC inventory and source-selection checks with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Phase21\phase21_gc_inventory.ps1
powershell -ExecutionPolicy Bypass -File .\Tools\Phase21\phase21_source_audit.ps1
```

The source path uses `guidexos` as the target OS value and defines
`CLR_CMAKE_TARGET_GUIDEXOS`, `TARGET_GUIDEXOS`, `TargetsGuidexos`, and the
NativeAOT `TARGET_GUIDEXOS` managed-build constant. The Windows workstation is
still the build host; host MSVC/CMake behavior is retained only where it is a
toolchain requirement. Target source selection is based on
`CLR_CMAKE_TARGET_*`, never CMake's host `WIN32` variable.

The new `gcenv.guidexos.cpp` is intentionally bounded. It provides the
workstation GC interface shape without including Windows headers or copying
the Windows environment. VM reserve/commit/release and timing use existing
Phase 19 PAL declarations. Write-watch, large pages, NUMA, CPU groups,
affinity, process-memory telemetry, and blocking event waits fail closed or
report unsupported. Critical-section fast paths use process-local atomics.

The semantic inventory identifies that workstation GC needs event and lock
semantics, but a multi-threaded runtime needs a blocking address/value wait and
wake primitive. No Phase 19 PAL symbol is added in Phase 21. The proposed
contract result is Option 2 for Phase 22: one wait primitive and one wake
primitive, subject to implementation approval.

The source proof is deliberately below pack production. Packaging remains a
separate Phase 22 prerequisite and must not fall back to `win-x64` assets.

## Recorded result

The pinned source is `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. The patch
mechanism verifies that commit and the stock source hashes, requires a clean
checkout, applies exact-context transforms, copies four repository-owned
templates, and records the runtime diff under the ignored `out/dotnet`
evidence directory. The runtime source was applied from a clean checkout and
reapplied successfully from a second clean checkout. The patch changes eleven
tracked runtime files and adds four guideXOS-specific runtime files; the
tracked transform diff is 61 insertions and 10 deletions.

The platform identity is explicit at every native selection point:

- `guidexos` is a distinct `-os` value and `guidexos-x64` is never treated as
  `win-x64` or Unix.
- CMake defines `CLR_CMAKE_TARGET_GUIDEXOS` and native C++ defines
  `TARGET_GUIDEXOS`.
- managed build properties define `TargetsGuidexos` and NativeAOT consumes
  `TARGET_GUIDEXOS`.
- the Windows workstation remains the host (`CLR_CMAKE_HOST_WIN32`); host
  MSVC/CMake behavior is not used as the target runtime identity.
- the GUIDEXOS CMake branch selects `gcenv.guidexos.cpp` and the GUIDEXOS
  assembler-offset source. `gcenv.windows.cpp` and the Windows PAL source
  branch are excluded.

The isolated MSVC compile proof succeeds for `gcenv.guidexos.cpp` with
`TARGET_GUIDEXOS`, no `TARGET_WINDOWS`, no Windows GC source entry, and no
target-side `windows.h` dependency. The complete pinned
`build-runtime.cmd -component nativeaot -os guidexos -outputrid guidexos-x64`
configuration reaches CMake with the correct target identity but currently
stops in the host/apphost security-library path while requesting
`libgssapi_krb5`; it stops before the GUIDEXOS runtime target compiles. This is
recorded as a toolchain/packaging blocker, not accepted as a runtime pack.

## GC boundary decision

The inventory audits 53 semantic operations: 52 callable out-of-line methods
from `gcenv.windows.cpp` plus the inline page-size operation. The categories
are virtual memory (13), synchronization (13), processor topology (13),
runtime state (5), timing (5), identity (3), and diagnostics (1).

Workstation startup/correctness requires lifecycle, reserve/commit/decommit/
release, virtual-memory limits, processor count, timing, and the GC lock/event
semantics. Large pages, write watch, NUMA, CPU groups, affinity, priority
boosting, cache-size discovery, and Windows memory notifications are disabled
or deferred. Decommit is not reported as successful until a real PAL semantic
exists. The first one-thread payload must explicitly avoid helper-thread and
concurrent-suspension paths; no fake thread-store protocol is introduced.

The synchronization conclusion is Phase 22 Option 2, proposed but not yet
added to the Phase 19 contract. Uncontended lock ownership and event state can
remain process-local with acquire/release atomics and bounded spinning. If a
blocking path is proven necessary, the minimal semantic pair is:

```text
guidexos_pal_wait(address, expectedValue, timeout)
guidexos_pal_wake(address, count)
```

These are address/value wait and wake semantics, not HANDLEs, events,
critical-section objects, or Win32 wait APIs. Phase 19 therefore remains at
exactly 20 external symbols in Phase 21.

## Remaining Phase 22 prerequisites

Before a real `guidexos-x64` runtime pack can be produced, the runtime source
needs a target-native linker/object/archive model, explicit RID and asset
selection, a GUIDEXOS NativeAOT build-integration target, target CoreLib and
ILCompiler assets, completion of the GC decommit and any required wait/wake
semantics, and startup/TLS/FLS/fail-fast adaptations. The packaging verifier
must reject Windows target files, Windows DLL imports, and `win-x64` fallback.

The CoreLib audit found 52 Windows-suffixed files. The first `Main() => 42`
surface must keep loader, registry, filesystem, general environment, Windows
marshal, and Windows runtime-information paths unreachable or provide exact
GUIDEXOS abstractions later. GC monotonic time is already routed through the
Phase 19 declaration; entropy, full startup, TLS/FLS, and managed fail-fast
remain future work.

The raw machine-readable inventory is generated at
`out/dotnet/phase21-gc-inventory/gc-os-inventory.json`; source selection,
compile, platform reachability, and source-build records are generated under
the corresponding `out/dotnet/phase21-*` directories and are not committed.
