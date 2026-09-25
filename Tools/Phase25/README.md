# Phase 25 independent native bootstrap

Phase 25 keeps the exact Phase 23 NativeAOT PE frozen and stages a separately
reviewed raw x86-64 image beside it:

* `guideXOS.UserManagedProof.exe` remains the `GXMI v1` managed image;
* `guideXOS.Phase25Bootstrap.bin` is a freestanding, import-free, relocation-
  free bootstrap;
* `guideXOS.Phase25Bootstrap.gxbi` is the bounded `GXBI v1` sidecar contract.

The bootstrap is loaded at `0x0000401200000000`, which is outside the Phase 23
image (`0x0000401000000000-0x00004010000c7000`), the Phase 24 runtime blocks
(`0x0000401100000000-0x0000401100051000`), the future heap reservation at
`0x0000401300000000`, and the user stack (`0x00007fff00000000-0x00007fff00010000`).
Its single page is user RX and is never shared globally.

Entry contract:

* `RDI` is the user virtual address of the 144-byte startup block;
* `RSP` is the top of the process-owned NX user stack minus 16 bytes;
* GS is the process-owned GS block base and `GS+0x58` is the TLS-vector pointer;
* all other registers are treated as unspecified until initialized.

The raw instruction path performs only startup identity checks, GS/TLS/FLS
checks, one long sentinel loop, the existing Ping and System Information ABI
calls, and `Exit(0)`.  A flag in the kernel-owned test setup turns the same
bootstrap into a deliberate CPL3 page-fault case.  It never enumerates `.CRT`,
calls `wmain`, or invokes NativeAOT/CRT/GC code.

Build and host-validate:

```powershell
.\Tools\Phase25\build_phase25_bootstrap.ps1
python .\Tools\Phase25\verify_phase25.py `
  .\out\dotnet\phase25-bootstrap\guideXOS.Phase25Bootstrap.bin `
  .\out\dotnet\phase25-bootstrap\guideXOS.Phase25Bootstrap.gxbi
```

The kernel selector is `Ring3Phase25`.  A host build and GXBI validation do not
claim CPL3 execution, timer preemption, IPC, or teardown runtime evidence when
QEMU is unavailable.
