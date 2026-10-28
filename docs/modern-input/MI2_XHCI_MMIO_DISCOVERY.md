# MI2 — xHCI MMIO Discovery and Capability Validation

## Outcome

**`MI2_MMIO_FRONTIER_ESTABLISHED`**

MI2 adds checked parsing for xHCI PCI BAR0 and a bounded, read-only capability
header reader that is exercised with synthetic data. The UEFI diagnostic
validates the PCI class and BAR shape, then stops before mapping or reading the
controller. The current handoff does not provide a trusted per-BAR aperture
length, so MMIO register bounds cannot yet be established safely.

No xHCI operational, runtime, or doorbell register was accessed. This is not a
controller initialization or USB input result.

## Baseline and branch identity

- Checkout: `D:\dev\guideXOSUEFI_MODERN_INPUT`
- Branch: `MODERN_INPUT` (tracking `origin/MODERN_INPUT`, one commit ahead at
  MI2 start)
- MI2 starting HEAD: `796072eff3b509f0da2b6e7f54f3fc23bd3beeb3`
- Starting commit: `Add MI1 xHCI frontier diagnostics`
- Starting worktree: tracked files clean; pre-existing untracked
  `guideXOS/artifacts/` was present and preserved
- The MI1 report records its own pre-MI1 HEAD as `829f8d5...`; MI2 began from
  the subsequent MI1 commit shown above.
- The PCI scan reads BAR0–BAR5 into `PCIDevice`. MI1's UEFI diagnostic
  identifies xHCI by class `0x0C`, subclass `0x03`, programming interface
  `0x30`, but did not interpret BARs or access controller MMIO.
- The project is C# UEFI. No C++ Server driver was copied or modified.

## PCI BAR and MMIO frontier

`XhciDiscovery` validates the xHCI class tuple and parses BAR0 without writing
PCI configuration space. It handles 32-bit and 64-bit memory BARs, reconstructs
the 64-bit base from BAR0/BAR1, accepts the prefetch attribute, and rejects
I/O BARs, reserved/below-1-MiB memory types, empty or zero bases, a missing
upper BAR slot, and addresses outside the lower canonical range supported by
the current four-level identity map. Checked range helpers reject zero-sized,
out-of-resource, non-canonical, and overflowing physical ranges.

The existing `PageTable.MapMmioRange` can identity-map a supplied range as
uncached, writable, and non-executable. It does not discover or validate a PCI
BAR's assigned aperture length. The PCI enumeration path stores the BAR base
only; it does not retain resource sizing metadata. The UEFI memory-map handoff
does not associate a descriptor with an individual PCI BAR. Conventional BAR
size discovery would write all ones to PCI configuration space, which MI2
does not permit.

Because the aperture length is unknown, the production diagnostic deliberately
does not call `MapMmioRange`, form a BAR pointer, or invoke an MMIO accessor.
For each of up to eight unique xHCI functions it reports controller discovery,
BAR shape status, the unverified resource size, and that MMIO/capability reads
were not attempted. Additional candidates are counted as suppressed. Invalid
BAR configurations receive an explicit status.

## Capability validation

The hosted-testable reader accepts a caller-provided resource length and an
`IReadOnlyMmioReader` interface that exposes only `TryRead32`. It reads only the
seven requested capability dwords from offsets `0x00` through `0x18`: the
CAPLENGTH/HCIVERSION pair, HCSPARAMS1–3, HCCPARAMS1, DBOFF, and RTSOFF. It does
not read HCCPARAMS2 at `0x1C` or any operational, runtime, or doorbell register.

The decoder validates the fixed header extent, CAPLENGTH alignment and
operational-register bounds, nonzero controller version and slot/interrupter/
root-port counts, DBOFF and RTSOFF alignment and ordering, and the full
advertised doorbell and runtime-interrupter spans against the supplied resource
length. It decodes controller version, slots, interrupters, root ports,
scratchpad count, HCSPARAMS3 exit latencies, 64-byte context capability,
64-bit address capability, and the two offsets. The UEFI diagnostic cannot
invoke this reader until a trusted aperture bound and safe mapping are
available.

## Hosted tests

Run with:

```powershell
$out = Join-Path $PWD '_mi2-validation\hosted'
dotnet run --project Tests\XhciDiscoveryTests\XhciDiscoveryTests.csproj `
  "-p:BaseOutputPath=$out\bin" `
  "-p:BaseIntermediateOutputPath=$out\obj"
```

Result: **PASS (72 assertions)**. Synthetic cases cover the xHCI PCI class
tuple, 32-bit and 64-bit BAR interpretation, address reconstruction, invalid
BAR forms, canonical-address and range boundaries, capability decoding,
scratchpad count fields, malformed CAPLENGTH and controller limits, invalid or
out-of-range offsets, missing MMIO access, short resources, and early stop on a
failed read. The access seam has no write operation; successful reads stop at
RTSOFF (`0x18`). No guest or physical controller was used.

## Build investigation and limitations

The normal managed kernel build was attempted with isolated output paths and no
stub. It stopped with `CS2001` because
`guideXOS/obj/ManagedArtifactIdentities.g.cs` is absent. The generator in
`build.ps1` requires the Phase 29 build record
`out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json`.
The Phase 29 scripts are present and its staged `.exe`/`.gxmi` payloads exist,
but the Phase 23 runtime pack (`out/dotnet/phase23-runtime-pack`), pinned
runtime checkout (`out/rt`), Phase 23 private compiler, source-patch evidence,
and Phase 19 PAL object/archive inputs are absent. The normal build path cannot
reproduce the generated identity source from the current artifacts alone; it
requires a real upstream Phase 19–29 rebuild. That broader release-artifact
rebuild was not started, and no Phase 29 record or release gate was fabricated
or bypassed.

For C# compile coverage only, the kernel project was also built with a
temporary, fail-closed `ManagedArtifactIdentities.Matches` stub under ignored
`guideXOS/obj/`. The stub was explicitly marked incomplete, removed immediately
after compilation, and was not committed or used for publish, NativeAOT, or
acceptance. Result: **managed compile succeeded, 0 errors, 142 warnings**. The
count matches MI1's separately recorded 142-warning baseline; no warnings were
reported from the MI2 xHCI source or diagnostic. This compile-only result does
not establish a reproducible production build. A no-stub build still fails at
the missing generated source. The four SDK warnings from that early failure
(three .NET 7 support notices and one RID behavior notice) are separate from
the 142 C# warning baseline.

## Safety and remaining verification

- PCI BAR configuration was read only; no BAR assignment or command register
  was changed, and bus mastering was not enabled.
- No firmware ownership handoff, controller reset/start, DMA allocation or
  registration, command/event ring setup, USB enumeration, or HID work was
  added or performed.
- No QEMU guest execution, physical hardware test, GitHub push, mainline edit,
  or QAC/QEMU resource use occurred.
- Actual firmware BAR sizing, PCI memory-decode state, MMIO mapping, capability
  reads, and controller-reported values remain unverified on hardware.

The next initialization frontier is a trusted, non-destructive source for the
assigned xHCI BAR aperture length. Once available to the post-ExitBootServices
kernel, validate the entire physical range and capability-relative offsets,
map only that bounded range with the existing uncached MMIO mapper, and enable a
read-only register adapter. Then run physical reads on identified hardware
before any ownership change, operational-register write, DMA setup, or
controller start.

## MI3 proposal

Add a firmware-originated PCI resource handoff that records each xHCI BAR's
base, width, and assigned aperture length before ExitBootServices, without
probing by writing BAR registers. Validate that metadata in the C# kernel and
connect the existing tested capability reader to a bounded uncached MMIO
mapping. Keep controller initialization and input enumeration out of MI3 until
the read-only capability path is verified on physical hardware.

## Run record

- Outcome: `MI2_MMIO_FRONTIER_ESTABLISHED`
- Starting HEAD: `796072eff3b509f0da2b6e7f54f3fc23bd3beeb3`
- Ending HEAD before the MI2 local commit: recorded in the final run report
- Changed production files: `Kernel/Drivers/XhciDiscovery.cs`,
  `Kernel/Kernel.projitems`, `Kernel/Misc/USB.cs`
- Hosted tests: 72 assertions passed
- Compile-only validation: 0 errors, 142 warnings with temporary incomplete
  identity stub; ordinary build remains blocked at missing generated source
- QEMU: not run
- Physical hardware: not tested
- GitHub push: none
- Mainline/Server checkout: not modified
