# MI3 — Firmware PCI Resource Handoff and Safe xHCI MMIO Access

## Outcome

**`MI3_MMIO_PATH_HOSTED_VERIFIED`**

The loader now obtains BAR0's assigned resource range from
`EFI_PCI_IO_PROTOCOL.GetBarAttributes`, copies a bounded versioned resource
contract into loader-owned storage, and the C# kernel validates the contract
against its own PCI scan before mapping or reading the xHCI capability header.
The kernel maps only the pages needed for that header as supervisor-only,
read-only, non-executable, and uncached. Hosted tests pass. No physical xHCI
register read was attempted, so hardware behavior remains unverified.

## Checkout and revision

- Checkout: `D:\dev\guideXOSUEFI_MODERN_INPUT`
- Branch: `MODERN_INPUT`
- Starting HEAD: `012e5a72b816b5e89e984c489ca7436a827c3552`
- Ending HEAD for the MI3 source implementation and tests, before this
  report-only commit:
  `89ef930bf6a432f9150640cfe59674e1b84560de`
- The report is committed after the source implementation; the final branch
  HEAD is recorded in the final run report accompanying this document.
- The protected `D:\dev\guideXOSUEFI` checkout was not modified.
- Existing `_mi2-validation-20261028/` and `guideXOS/artifacts/` directories were
  preserved. MI3 validation outputs were placed under `%TEMP%`.

## Firmware resource provenance

Before `ExitBootServices`, the loader locates handles exposing
`EFI_PCI_IO_PROTOCOL`, reads each function's location, vendor/device IDs,
class code, and BAR0 configuration (BAR1 as well when BAR0 is 64-bit). It
selects PCI class `0x0C`, subclass `0x03`, programming interface `0x30`, then
calls `GetBarAttributes` for BAR0. That API returns a caller-owned resource
descriptor list for the current BAR assignment; the loader frees the returned
pool allocation after copying its values. The [UEFI 2.10 PCI Bus Support
specification, §14.4.18](https://uefi.org/specs/UEFI/2.10/14_Protocols_PCI_Bus_Support.html)
defines the QWORD descriptor's starting address, ending address, translation
offset, length, and end tag.

The shared bounded parser accepts exactly one QWORD memory descriptor followed
by an end tag. It checks the descriptor type and length, optional end-tag
checksum, resource type, 32/64-bit width, address flags, nonzero length,
inclusive maximum, checked end arithmetic, lower canonical physical range,
and zero translation offset. A multi-descriptor list or unsupported attribute
fails the whole discovery contract. The loader separately reconstructs BAR0's
base from PCI configuration and requires it to equal the firmware descriptor's
base. It does not size a BAR by writing PCI configuration and does not use the
UEFI memory map to infer its aperture.

The trust boundary is UEFI's PCI I/O implementation: a successful
`GetBarAttributes` result must point to the descriptor template specified by
UEFI. The parser validates the fixed one-descriptor form and checksum where
present; the kernel then independently checks the copied values against its
PCI enumeration. A missing protocol, malformed descriptor, unsupported
translation, invalid BAR, or incomplete scan produces a non-complete status
and prevents capability reads.

## Handoff contract and compatibility

`BootInfo` remains version 1 and retains its existing size. The existing
`BootMemoryHandoff` prefix and `BootInfo.Reserved[2]` pointer are unchanged. A
fixed 528-byte `GXPR` version 1 extension follows that prefix in the loader's
static data. It contains a 16-byte header and storage for up to eight 64-byte
entries. Each entry records segment/BDF, vendor/device IDs, BAR index and
width, resource and memory attributes, BAR configuration base, MMIO base,
aperture length, inclusive maximum, translation offset, and supported
attributes. Compile-time size and offset assertions keep the C++ and C# layouts
aligned.

The loader sets BootInfo flag bit 6 only for the new extension. Older readers
ignore that bit and continue to use the unchanged prefix. No pointer into the
temporary `GetBarAttributes` pool survives `ExitBootServices`; only fixed-width
values are copied. At kernel entry, the extension is read only if the flag is
set and the complete extension lies inside an `EfiLoaderCode` or
`EfiLoaderData` memory-map descriptor. The kernel reserves its storage before
the allocator can reuse it.

The kernel rejects invalid signatures, versions, entry sizes, counts, status,
duplicate BDF/BAR keys, malformed addresses, inconsistent maxima, unsupported
widths or resource types, nonzero translations, and cacheable or reserved
memory attributes. It associates every entry with the kernel's own PCI scan,
including segment/BDF, vendor/device ID, xHCI class, and BAR0 base. Any mismatch
stops the entire resource path.

## MMIO mapping and capability reads

The path does not assume the BAR is already mapped. It computes the page-rounded
span for the fixed `0x20`-byte capability header and maps only those one or two
pages. The full firmware aperture bounds the requested access; page rounding
does not enlarge the pointer's read bounds. A conflict check rejects overlap
with any non-`EfiMemoryMappedIO` memory-map descriptor. The memory map is used
only for overlap safety, never as evidence of BAR size.

The new page-table mapper fails unless CPUID reports PAT and `IA32_PAT`
contains an exact UC entry. It installs identity leaves only where the target
PTEs are empty, rejects user or large-page intermediate mappings, and sets
present, supervisor, read-only, NX, and the selected UC PAT bits. It does not
retag an existing leaf mapping. The mapping is retained for kernel lifetime.
If PAT, page-table, address-range, page-alignment, or memory-map checks fail,
the reader is not created.

After mapping, a read-only adapter checks every aligned 32-bit read against
both the firmware aperture and the mapped `0x20`-byte access span. It uses one
native `mov` load per register so the managed optimizer cannot remove the
device read. The existing parser reads the seven dwords through RTSOFF at
`0x18`: CAPLENGTH/HCIVERSION, HCSPARAMS1–3, HCCPARAMS1, DBOFF, and RTSOFF. It
does not read HCCPARAMS2, operational registers, runtime registers, or
doorbells. Diagnostics identify a successful read as
`source=hardware-mmio`; synthetic hosted reads remain separate.

No controller ownership change, reset/start, PCI command-register change, bus
master enable, operational-register write, DMA allocation, ring setup, USB
enumeration, or HID installation was added. The MMIO interface exposes no
write method.

## Hosted validation

The .NET xHCI discovery suite passes **110 assertions**, retaining all 72 MI2
assertions and adding resource contract, BAR association, aperture, attributes,
mapping-plan, PAT, pre-map rejection, and read-only cases. It uses only fake
register blocks. The run command used isolated `%TEMP%` output paths:

```powershell
$out = Join-Path $env:TEMP 'guideXOS-mi3-hosted-tests-20261028'
dotnet run --project Tests\XhciDiscoveryTests\XhciDiscoveryTests.csproj `
  "-p:BaseOutputPath=$out\bin\" `
  "-p:BaseIntermediateOutputPath=$out\obj\" `
  "-p:MSBuildProjectExtensionsPath=$out\obj\" `
  '-p:DefaultItemExcludesInProjectFolder=obj/**'
```

The shared raw UEFI descriptor parser passes **19 assertions** in
`Tests/XhciPciResourceDescriptorTests.cpp`, compiled with
`g++ -std=c++17 -Wall -Wextra -Werror`; the executable was placed under
`%TEMP%`. It exercises 32/64-bit QWORD entries, decoded inclusive ranges,
checksum handling, malformed/truncated templates, I/O resources, invalid
widths, translations, address overflow, inconsistent maxima, and unsafe
attributes. Neither hosted harness accesses hardware.

## Build checks and remaining prerequisites

The normal command `dotnet build guideXOS\guideXOS.csproj -p:SkipISO=true`
fails with `CS2001` because
`guideXOS/obj/ManagedArtifactIdentities.g.cs` is absent. The supported
generator requires
`out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json`.
The required Phase 19–23 runtime pack, pinned runtime checkout, private
compiler, source-patch evidence, and PAL inputs are also absent from this
checkout. The supported generation pipeline therefore cannot produce the
identity source from available inputs; no record or identity stub was
fabricated for the normal build.

For compile diagnostics only, the kernel project compiled with a temporary
fail-closed `ManagedArtifactIdentities.Matches` stub under ignored `guideXOS/obj`
and isolated output directories. It completed with **0 errors and 142
warnings**, matching the MI2 baseline; no warnings came from MI3 C# source. The
stub was removed immediately after the compile. This does not replace the
normal build prerequisite or produce a production kernel image.

The environment has no `msbuild` or MSVC `cl`, so the UEFI loader project was
not built as a production binary. The changed loader translation units passed
a `g++ -fsyntax-only` check against this checkout's EDK2 headers. The modified
NASM native stub assembled successfully. `git diff --check` passed.

## Qualification and MI4

- No real hardware register reads were attempted.
- No QEMU session was launched.
- No physical qualification was performed.
- Nothing was pushed.

Recommended MI4 objective: restore the supported Phase 19–29 artifact inputs,
build the production loader/kernel, then qualify the read-only capability
path on one identified physical UEFI x86-64 platform. Capture the resource
contract status, BDF, BAR0 base/length, mapping result, and all seven capability
dwords. Confirm that unsupported firmware descriptors fail closed. Keep MI4
limited to read-only capability qualification; controller ownership changes,
initialization, DMA, and USB enumeration need separate reviewed phases.

## Run record

- Outcome: `MI3_MMIO_PATH_HOSTED_VERIFIED`
- Starting HEAD: `012e5a72b816b5e89e984c489ca7436a827c3552`
- Ending source HEAD: `89ef930bf6a432f9150640cfe59674e1b84560de`
- Hosted results: 110 .NET assertions and 19 C++ descriptor assertions passed
- Normal build: blocked by missing generated `ManagedArtifactIdentities.g.cs`
- Temporary-stub compile: 0 errors, 142 baseline warnings; stub removed
- Real hardware MMIO reads: no
- QEMU: not run
- Physical test: not run
- Push: no
