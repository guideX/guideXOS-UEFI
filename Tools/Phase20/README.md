# Phase 20 NativeAOT source audit

Phase 20 pins the NativeAOT source required for a future `guidexos-x64`
runtime pack. The lock is `runtime-source.lock.json`; the source cache is the
ignored repository-local `out/rt` directory. The source provenance is
`https://github.com/dotnet/runtime.git` at commit
`9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, with no submodules.

Reproduce the pinned stock source baseline with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Phase20\build_pinned_runtime_source.ps1
powershell -ExecutionPolicy Bypass -File .\Tools\Phase20\phase20_source_audit.ps1
```

The source build uses the recorded Visual Studio x64 environment, MSVC,
CMake, and Ninja settings. It builds `Clr.NativeAOTRuntime` and produces the
ordinary Windows `aotsdk` libraries under the generated source checkout. It
does not produce a custom runtime pack and must not be consumed as one.

## Deliberate stop

The audit is **Outcome E - source adaptation expands beyond the approved
bounded PAL boundary**. The stock NativeAOT source selects the Windows PAL
under `WIN32`. Its `PalRedhawk` surface contains 41 imported operations,
including context, thread startup, event/wait, module/loader, thunk, VM,
timing, and fail-fast behavior. The Phase 19 contract contains exactly 20
external guideXOS symbols and no external synchronization symbols.

The Windows GC environment is not a narrow VM adapter. The pinned
`src/coreclr/gc/windows/gcenv.windows.cpp` is 1,486 lines with 53 GC OS
methods, covering synchronization/events, critical sections, virtual memory,
write-watch, affinity/NUMA, process-memory limits, timing, and thread
yield/priority behavior. The Windows packaging target also adds Windows SDK
libraries, `WindowsAPIs.txt`, and UCRT defaults. CoreLib has additional
Windows time, loader, process, COM, environment, and interop surfaces whose
reachability must be constrained by a target-specific build.

No external runtime source was modified. The estimated minimum adaptation is
at least 11 runtime/GC/build files plus targeted CoreLib build items and
roughly 1,500–3,000 changed lines before compile-driven iteration. That is a
broad runtime fork, so the phase stops before expanding the PAL contract,
emulating Windows synchronization, or producing a renamed stock pack.

`verify_guidexos_runtime_pack.ps1` is intentionally fail-closed. It rejects a
missing or foreign pack manifest and rejects any manifest that records a stock
`win-x64` fallback. Managed CPL3 entry is not attempted.
