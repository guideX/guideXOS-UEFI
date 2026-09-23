# Phase 22 GUIDEXOS NativeAOT target build

Phase 22 extends the accepted Phase 21 source identity into the smallest
NativeAOT native build graph. The Windows machine remains the build host;
`guidexos` remains the target identity.

The first Phase 22 source seam is deliberately narrow: the CoreCLR source
normally adds the static single-file apphost to every non-cross build. That
host-only target pulls in `System.Net.Security.Native` and probes
`libgssapi_krb5`. It is not a NativeAOT runtime dependency, so the GUIDEXOS
target excludes only that apphost target.

Apply the Phase 22 source transform after the accepted Phase 21 transform:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Phase22\apply_phase22_patch.ps1
```

Build the native target subset:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Phase22\build_guidexos_native_runtime.ps1
```

The build records the exact CMake invocation, target identity, compile-command
inventory, installed target-native objects, and unresolved-link evidence under
`out/dotnet/phase22-native-runtime`.

The smallest proven native subset is `Runtime.WorkstationGC`. It builds the
NativeAOT runtime support, workstation GC, startup object dependencies, the
host-Windows MASM helper set, and the GUIDEXOS GC environment. It deliberately
does not build the broad `nativeaot` aggregate, which also selects apphost,
watchdog, Server GC, Vxsort variants, and unrelated host libraries.

The host/apphost dependency chain is:

```text
CoreCLR CMake -> static apphost -> System.Net.Security.Native
             -> src/native/corehost/apphost/static/extra_libs.cmake
             -> find_library(gssapi_krb5)
```

That chain is host-only and is excluded for `CLR_CMAKE_TARGET_GUIDEXOS`; no
`libgssapi_krb5` installation or substitute is used.

The private RID staging command is:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Phase22\stage_guidexos_runtime_pack.ps1
```

It produces a preliminary private package identity,
`runtime.guidexos-x64.Microsoft.DotNet.ILCompiler` version `9.0.0`, with an
empty RID import list. This is not an official Microsoft RID/package and is
not accepted as a usable runtime pack until the compiler, CoreLib, linker,
imports, and loader contract are all validated.

The first custom-payload command is:

```powershell
dotnet publish .\UserManagedProof\guideXOS.UserManagedProof.csproj -c Release `
  -p:Phase22Guidexos=true `
  -p:RuntimeIdentifierGraphPath=$PWD\Tools\Phase22\runtime-identifier.graph.json `
  -p:UseAppHost=false -p:DisableUnsupportedError=true `
  -p:IlcUseEnvironmentalTools=true --no-restore -v:minimal
```

The RID and private package are selected successfully, but the stock cached
ILCompiler fails closed at `--targetos:guidexos` with `Target OS 'guidexos' is
not supported`. The source chain is explicit: the private RID becomes
`_targetOS=guidexos` in `Microsoft.DotNet.ILCompiler.SingleEntry.targets`, the
NativeAOT targets pass `--targetos:guidexos`, and
`src/coreclr/tools/Common/CommandLineHelpers.cs` rejects the token. The
installed SDK is not patched and `guidexos` is not relabeled as Windows.

No installed SDK is modified, no `win-x64` asset is relabeled, and no managed
payload is accepted until a complete private runtime-pack manifest passes the
fail-closed verifier.
