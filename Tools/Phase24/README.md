# Phase 24 real managed-image mapping boundary

Phase 24 adds the kernel-side, fail-closed mapping contract for the exact Phase
23 artifact.  The accepted input is:

`out/dotnet/phase23-user-managed-proof/publish/guideXOS.UserManagedProof.exe`

with SHA-256

`C8D60ABE6D91917F4E236F435A8C2D4272386CEC830C07AFA691897A23DA195A`.

`stage_phase24_image.ps1` copies that exact image and generates a bounded
`GXMI` descriptor into `ramdisk_src/Native`.  The descriptor is only a compact
cross-check.  The kernel re-parses and validates the untrusted PE bytes before
allocating any image page.  JSON, host pointers, PE section addresses, and
kernel pointers are never consumed by the kernel loader.

The loader implements the Phase 23 fixed-base PE contract:

- PE32+, AMD64, fixed base `0x0000401000000000`;
- bounded file/virtual ranges, canonical user addresses, alignment, overflow,
  overlap, entrypoint, import, relocation, and permission checks;
- `.text` and `.__manag` RX, `.rdata`/`.pdata`/`.CRT` R, `.data` RW;
- file-backed copying and zero-fill into process-owned pages;
- process-private startup block, X3 GS block, TLS vector, `_tls_index`, FLS
  table, runtime-thread state, and explicit managed-entry gate;
- deterministic ownership counters and teardown of every allocated page.

The exact Phase 23 PE entrypoint is `wmain` at RVA `0x1420`.  The accepted
Phase 23 image does not contain a reviewed native-only bootstrap symbol, and
its `.CRT` section is retained read-only but not invoked.  Consequently the
Phase 24 diagnostic selector maps and tears down the real image but refuses to
schedule it: `NativeBootstrapRva == 0` and `ManagedEntryReady == false` are
hard gates.  This is Outcome D, not a managed execution claim.  Phase 25 first
requires a separately reviewed toolchain artifact exposing a native bootstrap
without changing the accepted Phase 23 identity or jumping through `wmain`.

The staging step is invoked by the normal `build.ps1` ramdisk phase when the
Phase 23 artifact and map are present.  It does not embed the image in kernel
source and does not alter Server or Legacy.

For a deterministic local build, run:

```powershell
.\build.ps1 -SkipBootloader -SkipConversion -UefiDiagnosticMode Ring3Phase24
```

The selector emits mapping/scaffold/rejection/teardown markers over the normal
serial path.  A successful host build is not a managed-execution claim: the
exact Phase 23 image has no native bootstrap, so CPL3, PAL calls, System
Information, Exit, IPC, and managed code remain blocked by policy.
