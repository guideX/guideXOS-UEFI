# MI7 SARAH_DT BAR0 Resource Compatibility

## Outcome

**`SARAH_DT_DESCRIPTOR_CAPTURED; EDK2_ALIGNMENT_MASK_COMPATIBILITY_FIXED; PHYSICAL_RETEST_PENDING`**

Software update date: **2026-11-08 (America/Los_Angeles)**

The first SARAH_DT run launched the standalone diagnostic and returned to the
UEFI Shell, but it did not preserve the raw descriptor. The second physical
capture reported an authentic successful `GetBarAttributes()` response for
Intel xHCI `0000:00:14.0`, vendor/device `8086:06ED`. The image used for that
capture still rejected BAR0 before capability access. This revision adds the
bounded EDK II maximum-mask convention to the shared parser, standalone probe,
loader handoff, and kernel validator. The revised image is hosted-verified and
ready for a physical retest; it has not been run on SARAH_DT.

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

Verify the image SHA-256 before and after copying it to dedicated test media.
On SARAH_DT, use the existing one-time UEFI Shell launch procedure, leave
Secure Boot and firmware settings unchanged, and record continuous video
through return to the Shell. Confirm the output says the descriptor was
accepted as an `EDK2 alignment mask`, gives checked end `0xE102FFFF`, and
reports the bounded read-only capability values. Stop before capability
access if any resource or identity validation fails. This retest is the next
qualification step; no physical qualification of this revision is claimed.
