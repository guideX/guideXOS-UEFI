# MI7 SARAH_DT BAR0 Resource Compatibility

## Outcome

**`MI7_R3_GATE_DIAGNOSED; PCI_BAR_AGREEMENT_DETAIL_REQUIRED; READ_ONLY_DIAGNOSTIC_READY`**

Software update date: **2026-11-08 (America/Los_Angeles)**

The first SARAH_DT run launched the standalone diagnostic and returned to the
UEFI Shell, but it did not preserve the raw descriptor. The second physical
capture reported an authentic successful `GetBarAttributes()` response for
Intel xHCI `0000:00:14.0`, vendor/device `8086:06ED`. The MI7-R2 image parsed
that descriptor, then failed its post-parser BAR0 validation. MI7-R3 separates
the post-parser rejection stages and prints the PCI BAR configuration values
needed to resolve the remaining width/base comparison. No validation rule was
relaxed, and capability registers were not read in the third physical run.

## First physical run

| Field | Observation |
| --- | --- |
| Test date/time, firmware version, capture | Not recorded/provided |
| PCI handles scanned / Intel xHCI controllers | 14 / 1 |
| PCI location | `0000:00:14.0` |
| Vendor/device | `8086:06ED` |
| PCI class | `0C/03/30` |
| Failure stage | `malformed BAR0 resource descriptor` |
| Displayed width/base/length | `0 / 0 / 0` |
| Capability registers read | None |
| Application return | Returned to UEFI Shell |

The displayed zero BAR values were parser defaults: output fields were only
copied after parsing succeeded. The first run did not capture the raw resource
descriptor and did not display the exact EFI status.

## Second physical descriptor capture

The operator reported these raw values and `EFI_SUCCESS`:

| Field | Captured value |
| --- | ---: |
| Descriptor type / payload length | `0x8A` / `0x002B` |
| Resource type | Memory (`0`) |
| General / specific flags | `0` / `0` |
| Granularity | `32` |
| Address minimum | `0xE1020000` |
| Address maximum | `0x000000000000FFFF` |
| Translation | `0` |
| Address length | `0x10000` |
| End Tag / checksum | `0x79 0x00` |
| EFI status | `EFI_SUCCESS` |

Test time, exact firmware version, and recording were not supplied. The image
that produced this capture still rejected the descriptor, so it did not read
capability registers.

## Rejection cause and compatibility rule

The exact old rejection was in
`guideXOSBootLoader/pci_resource_parser.h`: it required
`parsed.Maximum == parsed.Base + parsed.Length - 1`. For this descriptor that
condition expected `0xE102FFFF`, while the raw maximum was `0xFFFF`.

TianoCore EDK II's `PciBusDxe/PciIo.c` assigns
`PciBar[BarIndex].Alignment` to `Descriptor->AddrRangeMax`; this is the BAR
alignment-mask convention ([EDK II source](https://github.com/tianocore/edk2/blob/master/MdeModulePkg/Bus/Pci/PciBusDxe/PciIo.c#L1882-L1885)).
For a 64 KiB BAR, `0xFFFF` is the expected low-bit mask.

The parser now accepts exactly one of these interpretations:

1. **Inclusive end:** raw maximum equals the checked inclusive end
   `base + length - 1`.
2. **EDK II alignment mask:** `maximum + 1` is a power of two, equals the
   advertised length, and the base is aligned to that size.

Both interpretations reject overflow, ambiguous values, and ranges beyond the
supported lower canonical address space. A 32-bit BAR must have a computed
aperture end at or below `0xFFFFFFFF`. The EDK II form also rejects
non-contiguous masks, length mismatches, and misaligned bases. For SARAH_DT,
the validated aperture is `[0xE1020000, 0xE102FFFF]`.

The loader preserves the raw maximum and a two-byte interpretation marker in
the existing handoff entry's formerly reserved field. The 64-byte entry and
version-1 handoff layout remain unchanged. The kernel recomputes the checked
range end and verifies the marker, BAR identity, width, resource type,
translation, attributes, aperture, and controller BDF/device identity before
mapping. The existing ACPI memory cacheability-bit support remains; reserved
flags stay rejected.

## Hosted validation

| Suite | Result |
| --- | ---: |
| C++ resource descriptor parser | PASS, 39 assertions |
| Standalone UEFI probe | PASS, 19 cases / 93 assertions |
| C++ firmware/kernel handoff ABI | PASS, 149 assertions |
| C# xHCI discovery and resource validation | PASS, 140 assertions |
| **Total** | **PASS, 421 assertions** |

Tests include the exact SARAH_DT descriptor and controller identity, both
maximum conventions, malformed/ambiguous masks, arithmetic overflow,
translation/resource/width/termination checks, unsupported descriptor-list
shapes, aperture bounds, and read-only behavior. Hosted capability reads use
fake register blocks.

## MI7-R3 post-parser failure analysis

The third SARAH_DT run accepted the descriptor and reported BAR base
`0xE1020000`, aperture `0x10000`, raw alignment mask `0xFFFF`, checked end
`0xE102FFFF`, and zero translation. It then reported
`invalid or undersized BAR0 resource` before any capability read.

The production route is `ProbeXhciController` in
`Tools/XhciProbe/xhci_probe_core.h`: after descriptor parsing it calls
`ValidateBarResource`, whose old compound predicate assigned one failure stage
to all checks. That stage could also be emitted by the immediately following
minimum-aperture check in `ReadAndDecodeCapabilities`; its `Length < 0x20`
condition is false for the captured 64 KiB aperture. The compound validator
could fail on: a non-memory resource; unsupported address width; reserved
general or specific flags; resource/PCI BAR width disagreement; zero or
mismatched base; base below 1 MiB; invalid/overflowing maximum-plus-length;
maximum interpretation-marker mismatch; nonzero translation; an end above
the lower canonical limit; a 32-bit range extending past 4 GiB; or aperture
shorter than `0x20` bytes. An invalid descriptor is classified before this
validator, and a malformed descriptor list is classified separately.

For the captured descriptor, the reported length, checked end, mask
shape/alignment, translation, flags, lower-canonical bound, and 32-bit end all
pass when replayed through the parser and complete validator. The remaining
live comparison inputs are the PCI BAR0 base and width read from configuration
offset `0x10` (and `0x14` when BAR0 is 64-bit). The third physical report did
not include those configuration values, and the old EFI formatter omitted
them after a descriptor had parsed successfully. Therefore the capture narrows
the rejection to PCI BAR base or width agreement, but cannot distinguish those
two checks.

The hosted end-to-end fixture reproduces the full reported descriptor and
identity. With a matching 32-bit BAR0 configuration at `0xE1020000`, the probe
passes the resource gate and performs eight bounded `Mem.Read` calls. Negative
fixtures with the same descriptor report separate width-disagreement and
base-disagreement stages, and perform no `Mem.Read` calls. Those negatives
show how the gate behaves; neither is claimed as the unrecorded SARAH_DT BAR
configuration. There is no aperture-size, xHCI identity, or required-register
offset explanation for the captured pre-capability failure.

MI7-R3 keeps base and width agreement strict. It adds a distinct failure stage
for each post-parser resource condition, prints PCI BAR0 configuration base
and width alongside the firmware range, and distinguishes an out-of-range
capability-register access from a failed firmware read. This exposes the exact
predicate on the next run without granting access when either BAR identity
check fails.

| Hosted suite | Result |
| --- | ---: |
| C++ resource descriptor parser | PASS, 39 assertions |
| Standalone UEFI probe | PASS, 21 cases / 125 assertions |
| C++ firmware/kernel handoff ABI | PASS, 149 assertions |
| C# xHCI discovery and resource validation | PASS, 140 assertions |
| **Total** | **PASS, 453 assertions** |

## MI7-R3 isolated diagnostic

| Item | Verified value |
| --- | --- |
| EFI image | `out\mi7-r3\guideXOS-XhciProbe.efi` |
| SHA-256 | `5FA36A84C4BE43DA9E3A6EA85A5DF64E20170D8D7FFA2203A6E35DFE1830A48C` |
| Structural result | PASS; AMD64 PE32+, EFI Application, entry in `.text`, no imports or COM descriptor |
| Hosted regressions | PASS, 453 assertions |
| QEMU / QAC / physical testing during MI7-R3 | Not performed |

The exact post-parser rejection (base mismatch versus width mismatch) remains
unresolved until the BAR configuration values are captured. No defect was
corrected because the supplied physical evidence does not establish that the
strict PCI BAR agreement check is wrong. The new image is a read-only
diagnostic for the next SARAH_DT run.

## Isolated build

| Item | Verified value |
| --- | --- |
| Checkout / branch | `D:\dev\guideXOSUEFI_MODERN_INPUT` / `MODERN_INPUT` |
| EFI image | `out\mi7-r2\guideXOS-XhciProbe.efi` |
| SHA-256 | `21828286BA483D2D03A8DF8A31A20281DE16F3F78D98287994A293D67577DF05` |
| Structural result | AMD64 PE32+, EFI Application, entry in `.text`, no imports or COM descriptor |
| Loader build | PASS, isolated under `out\mi7-r2-loader\` |

The diagnostic remains strictly read-only: no PCI configuration writes, BAR
sizing writes, attribute changes, controller reset or initialization, DMA, or
controller-register writes. No QEMU, QAC, or physical test of the revised
image was run for this assignment. No push was performed.

## Next hardware qualification step

Verify the MI7-R3 image SHA-256 before and after copying it to dedicated test
media. On SARAH_DT, use the existing one-time UEFI Shell launch procedure,
leave Secure Boot and firmware settings unchanged, and record continuous
video through return to the Shell. Capture the displayed PCI BAR0 config base
and width as well as the firmware descriptor. If validation passes, the image
will proceed only with bounded read-only `EFI_PCI_IO_PROTOCOL.Mem.Read`
capability access. Stop if any resource or identity validation fails. No
physical qualification of MI7-R3 is claimed.
