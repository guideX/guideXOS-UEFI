# MI7 SARAH_DT BAR0 Resource Rejection Investigation

## Outcome

**`MI7_SOFTWARE_VALIDATION_DEFECT_FIXED; SARAH_DT_RAW_DESCRIPTOR_UNCONFIRMED`**

Investigation date: **2026-11-04 (America/Los_Angeles)**

The first physical run established that the standalone application launches
from the UEFI Shell and returns to it. The displayed zero BAR fields were
parser defaults, not values copied from the firmware descriptor. A standards
validity bug in the memory type-specific flag checks has been fixed in the
shared C++ parser, standalone probe, and kernel-side handoff validator. The
first run did not capture raw descriptor bytes, so it cannot establish that
this flag check was the specific reason SARAH_DT's BAR0 was rejected.

## First physical run: reported evidence

The operator reported:

| Field | Observation |
| --- | --- |
| PCI handles scanned | 14 |
| Intel xHCI controllers found | 1 |
| PCI location | `0000:00:14.0` |
| Vendor/device | `8086:06ED` |
| PCI class | `0C/03/30` |
| Failure stage | `malformed BAR0 resource descriptor` |
| Displayed width/base/length | `0 / 0 / 0` |
| Capability registers read | None |
| Application return | Returned to UEFI Shell |
| Test date/time, firmware version, capture | Not recorded/provided |

The descriptor bytes, End Tag, and exact EFI status were not printed by the
first build. In that source, `TryGetBarResource()` zero-initialized its output
and only copied width, base, maximum, and length after the parser succeeded.
The old display printed those output fields even after parser rejection.
Therefore the reported zero values are defaults left by rejected parsing, not
raw firmware values.

The failure stage also narrows the call result: the probe only reports
`MalformedBarDescriptor` after `GetBarAttributes()` returned a non-error
status, supplied a non-null resource pointer, and freeing that pointer did not
return an EFI error. The exact status was discarded by the old display, so the
physical capture does not prove it was exactly `EFI_SUCCESS`.

## UEFI 2.10 and ACPI audit

The call uses the EDK2 declaration of
`EFI_PCI_IO_PROTOCOL_GET_BAR_ATTRIBUTES`, whose `EFIAPI` function pointer
signature is `(This, UINT8 BarIndex, UINT64 *Supports, VOID **Resources)`.
The probe calls `GetBarAttributes(pci, 0, &attributes, &resources)` with both
optional outputs supplied and frees a non-null returned pool buffer through
`EFI_BOOT_SERVICES.FreePool()` on success, parser failure, and error-with-buffer
paths. The ABI, parameter types, BAR index, and ownership handling match UEFI
2.10 §14.4.18 ([UEFI 2.10 PCI Bus Support](https://uefi.org/specs/UEFI/2.10/14_Protocols_PCI_Bus_Support.html#efi-pci-io-protocolgetbarattributes)).

The UEFI descriptor table defines a QWORD address-space descriptor with a
43-byte payload: type at byte 0, little-endian payload length at bytes 1–2,
resource type at byte 3, flags at bytes 4–5, 64-bit granularity at bytes 6–13,
minimum at 14–21, maximum at 22–29, translation at 30–37, and length at
38–45. Its End Tag is two bytes (`0x79`, checksum) after the resource
descriptor. The existing byte offsets and little-endian decoding were correct;
byte-wise reads avoid alignment assumptions. The checksum rule was also
correct: zero means no checksum is required; a nonzero checksum must make the
sum of the resource list zero. The probe intentionally supports a single
QWORD range followed by End Tag. UEFI permits one or more QWORD descriptors,
so multiple ranges remain an unsupported, fail-closed form; the new probe
reports a second QWORD signature separately from an invalid End Tag.

The defect was in type-specific memory flags. ACPI defines bits 2:1 as memory
cacheability: non-cacheable, cacheable, write-combining, or cacheable and
prefetchable ([ACPI 6.4, Memory Resource Flags](https://uefi.org/htmlspecs/ACPI_Spec_6_4_html/06_Device_Configuration/Device_Configuration.html#resource-type-specific-flags)).
The previous parser rejected any of these bits with `(SpecificFlags & 0x06) !=
0`, which excluded standards-valid QWORD memory descriptors. The same
unnecessary rejection was present in the standalone validator and kernel
handoff validator. All three checks now accept bits 2:1, while bits 7:3 remain
rejected for this BAR contract.

The change does not relax BAR geometry or mapping checks. The probe still
requires memory type, width 32 or 64 matching PCI BAR0, matching base, minimum
base, nonzero length, checked inclusive maximum, exact `base + length - 1`,
zero translation, the supported lower canonical address range, a 32-bit
maximum for a 32-bit BAR, and at least a 0x20-byte capability aperture. No
capability access occurs unless the full resource validation succeeds.

`GetBarAttributes()` does not return a byte count. The new diagnostics copy
the EFI status and first three descriptor-header bytes, then copy fixed QWORD
fields only when the type is `0x8A` and the advertised payload length is the
complete standard `0x2B`. They also show the two bytes after that descriptor
to distinguish the End Tag from a following descriptor. The raw fields are
labelled separately from validated BAR values, and all fields are copied
before freeing the firmware pool.

## Hosted validation

All requested hosted regressions passed after the final source changes:

| Suite | Result |
| --- | ---: |
| C++ resource descriptor parser | PASS, 31 assertions |
| Standalone UEFI probe | PASS, 18 cases / 84 assertions |
| C++ firmware/kernel handoff ABI | PASS, 149 assertions |
| C# xHCI discovery and resource validation | PASS, 132 assertions |
| **Total** | **PASS, 397 assertions** |

Coverage includes valid 32- and 64-bit QWORD resources, all three nonzero
cacheability encodings, correct zero and nonzero End Tag checksums, truncated
and unterminated resources, wrong descriptor type/length, extra data, multiple
QWORD ranges outside the single-range contract, malformed flags, overflow,
base/width mismatch, short apertures, and descriptor cleanup. Existing BAR
bounds and read-only behavior assertions pass.

## Isolated EFI diagnostic image

The Release x64 standalone application was rebuilt outside release paths and
passed `Tools/XhciProbe/verify_efi.py`:

| Item | Value |
| --- | --- |
| Path | `D:\dev\guideXOSUEFI_MODERN_INPUT\out\mi7-investigation\guideXOS-XhciProbe.efi` |
| SHA-256 | `23388373FC0EAABFFBFDE319950F0F515AD683A0D306C292F6ABB28805104163` |
| Image | AMD64 PE32+, EFI Application subsystem 10 |
| Imports / COM descriptor | None |
| Signed | No |

The image remains read-only: no PCI configuration writes, BAR sizing writes,
attribute changes, xHCI reset or initialization, DMA, or controller register
writes were added. No QEMU, QAC, or physical test was run during this
investigation. No push was performed.

## Proposed second SARAH_DT test

Run the rebuilt image once from the UEFI Shell on SARAH_DT using the existing
safe one-time launch path and a dedicated test medium. Verify the file hash
before and after copying it. Leave Secure Boot, boot order, and firmware
settings unchanged. Record continuous video from launch through return to the
Shell and capture these displayed fields verbatim:

1. `GetBarAttributes EFI_STATUS`.
2. First descriptor type and payload length.
3. Resource type, general flags, specific flags, address granularity, raw
   minimum/base, maximum, translation offset, and length, when available.
4. The two bytes after the first descriptor and whether the parser accepted
   it, rejected it, or classified the list as multiple ranges.
5. Any validated BAR values, capability output if the BAR passes, and the
   final failure stage/status.

Do not proceed to capability or controller testing if BAR validation fails.
Preserve the complete capture and stop after this one diagnostic run if the
firmware reports an error or hangs. This second test will distinguish a valid
cacheability flag previously rejected by software from a malformed header,
unexpected list shape, invalid range, or other descriptor condition.
