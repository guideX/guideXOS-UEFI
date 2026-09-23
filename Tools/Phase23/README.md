# Phase 23 private GUIDEXOS ILCompiler

Phase 23 starts from the accepted Phase 22 runtime-source state and applies a
repository-owned, fail-closed transform to the pinned NativeAOT checkout in
`out/rt`. The transform gives ILCompiler an explicit `TargetOS.Guidexos`
identity while keeping Windows runtime semantics separate from the selected
initial ABI.

The initial ABI decision is deliberately narrow:

- target: `guidexos-x64` only;
- calling convention: Microsoft AMD64 register/stack ABI;
- object format: COFF objects and PE32+ final image;
- host: Windows/MSVC tools only;
- runtime semantics: guideXOS PAL/runtime boundary, not Windows APIs.

`IsWindows` remains false for GUIDEXOS. `IsWindowsAbiCompatible` and
`UsesCoffObjectFormat` are explicit compiler properties used only where the
initial COFF/PE ABI requires the corresponding layout or code-generation
choice. Windows-only P/Invoke, Win32 resources, CFG, and the Windows
command-line argument initializer remain gated on actual Windows.

Apply the source transform after the Phase 21 and Phase 22 transforms:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Phase23\apply_phase23_ilc_patch.ps1
```

The script verifies the pinned commit, the prior patch records, and the exact
combined source diff. It writes only generated evidence under
`out/dotnet/phase23-source-patch` and never modifies an installed SDK.

## Accepted Phase 23 result

The private compiler now accepts `--targetos:guidexos` as a distinct target
identity. `TargetOS.Guidexos` is not aliased to Windows; only the initial
AMD64 Microsoft ABI/COFF selection is shared. The target is Windows-hosted,
PE/COFF-shaped, and guideXOS-runtime-semantic.

The staged pack is produced by `stage_guidexos_private_pack.ps1` and consumes
the source-built Phase 22 Workstation GC, bootstrapper, Phase 19 PAL contract,
and the single `guidexos_link_shim.obj` target veneer. It has no SDK native
libraries, no direct P/Invokes, and no target `win-x64` fallback. The host-only
Windows RID package carries compiler-tool support only; the target runtime
assets are under `runtime.guidexos-x64.Microsoft.DotNet.ILCompiler`.

`build_user_managed_proof.ps1` successfully produces:

```text
out/dotnet/phase23-user-managed-proof/publish/guideXOS.UserManagedProof.exe
```

The current evidence artifact is PE32+ AMD64, fixed at
`0x0000401000000000`, with `wmain` at
`0x0000401000001420`, RX/R/RW sections, unwind metadata, no RWX, no base
relocations, no import directory, and no direct kernel imports. The current
artifact is 700,416 bytes with SHA-256
`C8D60ABE6D91917F4E236F435A8C2D4272386CEC830C07AFA691897A23DA195A`.
PE/COFF timestamps are not treated as a deterministic-output guarantee.

The runtime-owned TLS decision is X3: the user GS base contains a guideXOS
TLS vector at offset `0x58`, and `_tls_index` selects the process/thread-private
runtime block. The PE TLS directory is intentionally absent; the future
guideXOS loader/bootstrap owns bounded TLS construction and cleanup. The linked
map records 46 TLS/thread-static references, 30 frozen-object/GC records, 413
writable-static records, and 874 runtime-helper records. The 20-symbol Phase
19 external PAL schema is unchanged; the final map contains those 20 symbols
plus 5 local memory helpers.

GC decommit remains an explicit fail-closed `VirtualDecommit -> false` path;
the first `Main() => 42` proof emits no `guidexos_pal_vm_decommit` requirement,
so the PAL contract was not expanded or faked. The first-payload link also
adds no wait/wake symbols: the synchronization decision is M1 for this
non-executed, one-payload proof. General workstation-GC helper-thread and
blocking semantics remain a later runtime prerequisite.

Run the no-execute evidence gates with:

```powershell
python .\Tools\inspect_managed_image.py <artifact> --map <guidexos.map> --output <inspection.json>
python .\Tools\phase17_loader_probe.py <artifact> <inspection.json> --output <probe.json>
python .\Tools\Phase23\verify_guidexos_artifact.py <artifact> <pack-manifest> <inspection.json> <guidexos.map> <link.rsp>
```

The probe validates four simulated lifetimes, zero-fill, RX/R/RW permissions,
entrypoint bounds, and teardown. The Phase 23 verifier adds 13 fail-closed
identity, package, import, TLS, metadata, RWX, and descriptor-version negative
cases. Neither tool loads or executes managed code.

The remaining prerequisite before kernel loading is a reviewed guideXOS
bootstrap/loader implementation for the GS TLS vector, runtime-owned PAL
state, process-private GC pages, module metadata, and teardown. Phase 23 does
not modify `Kernel/`, does not execute this artifact, and does not authorize
managed CPL3 execution.
