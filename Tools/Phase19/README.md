# Phase 19 NativeAOT runtime-pack/PAL foundation

This directory records the first repository-owned guideXOS NativeAOT target
boundary. The private target identity is `guidexos-x64`: AMD64, freestanding
user-runtime semantics, and no claim that a standard .NET RID exists.

Run the contract build from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\build_phase19_runtime_pack.ps1 -Clean -BuildBaseline
```

The script pins SDK `10.0.401`, NativeAOT `9.0.0`, and runtime source commit
`9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. It verifies the installed package
hashes, compiles `guidexos_nativeaot_pal_contract.cpp` without CRT/Win32
references, archives that contract object, and generates
`out/dotnet/phase19-runtime-pack/phase19-manifest.json`.

This is intentionally a source-build gate, not a fake completed runtime pack.
The installed package chooses `Microsoft.NETCore.Native.Windows.targets`,
hard-codes Windows SDK libraries, and links Windows bootstrapper/GC/runtime
objects. The generated `Main() => 42` object also references Windows VM,
FLS/thread, timing, entropy, exception, loader, COM, and CRT facilities. A RID
rename cannot change those source-level dependencies.

The minimum bounded source adaptation is expected in these pinned runtime
areas:

* `src/coreclr/nativeaot/Runtime/Full` for bootstrap/runtime linkage;
* `src/coreclr/nativeaot/Runtime/windows` for PAL/time/thread/exception/module
  boundaries;
* `src/coreclr/gc/windows/gcenv.windows.cpp` for GC VM/synchronization/event
  ownership;
* NativeAOT/CoreLib Windows interop bindings; and
* `eng/native` and runtime-pack packaging targets for the private target.

The intended patch architecture is to replace the Windows PAL object family
and GC environment adapters with the contract in `pal-contract.json`, retain
workstation GC with process-private pages, remove Windows default library and
dynamic-loader/COM/event-log assumptions, and package a statically linked
`guidexos-x64` target. It must retain unwind metadata where required, keep
writable statics/TLS/GC state process-private, and use local memory helpers.

The design-stop rule remains active: do not maintain a large undocumented
runtime fork. Phase 20 should begin with a small pinned source patch set and a
review of the exact changed runtime files before producing a new managed
payload.

Phase 20 performed that source review against the pinned checkout. The
repository-owned result is recorded in `Tools/Phase20/runtime-source.lock.json`
and reproduced by `Tools/Phase20/phase20_source_audit.ps1`. The audit found
that the stock `WIN32` NativeAOT path imports 41 PAL operations and selects a
1,486-line Windows GC environment with synchronization, affinity/NUMA,
process-memory, write-watch, and Windows VM behavior. The Windows packaging
target also injects Windows SDK libraries and UCRT defaults. This is broader
than the 20-symbol contract, so Phase 20 stops before modifying runtime source
or producing a renamed stock pack; `guidexos-x64` cannot fall back to
`win-x64`. The fail-closed check is
`Tools/Phase20/verify_guidexos_runtime_pack.ps1`.
