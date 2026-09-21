# guideXOS.UserManagedProof

This is the Phase 17 managed user-payload build. It is intentionally a
separate project and has no reference to the kernel project, hardware layer,
filesystem, networking, reflection, dynamic loading, or ABI implementation.

The pinned build identity is:

| Input | Value |
| --- | --- |
| SDK | .NET `10.0.401` (`UserManagedProof/global.json`) |
| Target | `net9.0`, `win-x64`, AMD64 |
| NativeAOT | `Microsoft.DotNet.ILCompiler` `9.0.0` |
| Optimization | size preference, deterministic, trimmed |
| Symbols | no debug symbols; map file requested for inspection |
| Base policy | fixed `0x0000401000000000`, `/fixed` |

Build from this directory with:

```powershell
dotnet publish .\guideXOS.UserManagedProof.csproj -c Release --no-restore
```

The generated native PE and map file are build outputs. They are intentionally
not checked into the repository. Run `Tools\inspect_managed_image.py` against
the generated PE to produce the local manifest used by the Phase 17 report.
