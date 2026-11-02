# MI7 — Real Hardware xHCI Capability Qualification

## Outcome

**`MI7_HARDWARE_EVIDENCE_PENDING`**

The MI6 diagnostic artifact, committed source revision, and physical test
procedure have been checked. No physical machine has been launched in this
work session and no real controller capability values have been supplied.
Hardware qualification is not accepted. This record contains readiness
information and the evidence checklist for the owner-run tests.

## Image identity and source verification

Verification date: **2026-11-02 (America/Los_Angeles)**

| Item | Verified value |
| --- | --- |
| Checkout | `D:\dev\guideXOSUEFI_MODERN_INPUT` |
| Branch | `MODERN_INPUT` |
| Source reference | `7ee9c1fc3420919b3fd277b71b422c14f749e066` |
| EFI application | `out\mi6\guideXOS-XhciProbe.efi` |
| SHA-256 | `5F6AC2A4927F9ABB7E1AC039B72BD3C00903BD2A6107D0599BC3C8FBF3A884F7` |
| Hash check | Matches the expected MI6 hash |
| EFI artifact rebuilt for MI7 | No |

The source reference includes `Tools/XhciProbe/main.cpp`,
`Tools/XhciProbe/xhci_probe_core.h`, its project and structural verifier, the
hosted probe tests, and the MI6 physical test procedure. This readiness review
did not rebuild the executable or run hosted tests. The probe source requests
a five-second UEFI `Stall` (5,000,000 microseconds) when the service is
available, but ignores the call's returned status; the source cannot guarantee
that every firmware will hold the final screen for the full interval. It has
no input wait or serial capture path. The physical procedure therefore
instructs the operator to start continuous camera video before launching, so
the output is captured even if it returns immediately.

## Owner-run launch checklist

Use the full [MI6 physical test procedure](MI6_PHYSICAL_TEST_PROCEDURE.md)
for safety and recording details. Run the machines in this order:

1. `SARAH_DT`
2. `ASUS VivoBook`
3. `AIDA_LPT`, only if its existing UEFI environment supports an independent,
   safe launch

For each machine:

1. Record the exact model and firmware version if shown. Confirm the usual OS
   boot entry and recovery method remain available.
2. Verify the repository artifact before copying it to the dedicated medium:

   ```powershell
   Get-FileHash -Algorithm SHA256 .\out\mi6\guideXOS-XhciProbe.efi
   ```

   It must match the MI6 SHA-256 above. After copying, hash the copy on the
   dedicated blank FAT32 medium as well and confirm it is identical. Do not
   replace an OS loader or overwrite a `BOOTX64.EFI` on any existing medium.
3. Start a continuous camera recording before using the firmware's one-time
   **Boot from EFI file** browser. Select `guideXOS-XhciProbe.efi` from the
   dedicated medium. If using an already available UEFI shell, run `map -r`,
   identify the test medium from its contents, then invoke the file by its
   actual mapping, for example `fsN:\guideXOS-XhciProbe.efi`. Use
   `fsN:\EFI\BOOT\BOOTX64.EFI` only when that fallback path was created on
   the dedicated blank medium. Do not assume the mapping is `fs0:`.
4. Leave Secure Boot and all firmware settings unchanged. If Secure Boot or
   another existing trust policy rejects the unsigned application, capture
   the rejection and stop on that machine. Do not disable Secure Boot, enroll
   a key, create a persistent NVRAM entry, or change boot order.
5. Capture the complete console from launch through return. The app does not
   need keyboard or mouse input once launched. If the output disappears
   immediately, preserve the continuous video and inspect its frames; do not
   depend on a pause prompt or an input device to hold the screen.
6. Record the exact result and return through the machine's normal one-time
   boot path. If it hangs or firmware reports an error, stop, use its normal
   recovery procedure, and preserve the capture. Do not retry a failed
   firmware operation before reviewing the failure.

The diagnostic is capability-only. Do not reset a controller, change
ownership, program DMA, enumerate devices, initialize HID, change firmware
settings, or write PCI configuration or xHCI registers.

## Per-machine evidence

All rows below are **pending owner evidence**. `Pending` does not mean a
successful launch, zero controllers, or a missing capability. Populate values
only from that machine's capture. Preserve the original image or a verbatim
transcript with each completed record.

### SARAH_DT

| Field | Observation |
| --- | --- |
| Test date/time and time zone | Pending |
| Exact model / firmware version | Pending |
| Launch method / dedicated media | Pending |
| Secure Boot or trust-policy result | Pending |
| Application launched / returned successfully | Pending |
| Capture file or transcript | Pending |
| xHCI controller count reported | Pending |
| PCI segment:bus:device.function | Pending |
| Vendor / device IDs | Pending |
| BAR0 width / base / length / maximum / translation / attributes | Pending |
| BAR0 resource validation result and failure reason, if any | Pending |
| xHCI interface version / CAPLENGTH | Pending |
| Maximum slots / root ports / interrupters | Pending |
| Scratchpad-buffer requirement | Pending |
| Context size / 64-bit addressing support | Pending |
| DBOFF / RTSOFF and reported capability words | Pending |
| HCCPARAMS2, if reported for this interface version | Pending |
| First failed stage and exact EFI status, if any | Pending |
| Hang, firmware message, or recovery action | Pending |
| Confirmation that no controller or PCI write was attempted | Pending |

### ASUS VivoBook

| Field | Observation |
| --- | --- |
| Test date/time and time zone | Pending |
| Exact model / firmware version | Pending |
| Launch method / dedicated media | Pending |
| Secure Boot or trust-policy result | Pending |
| Application launched / returned successfully | Pending |
| Capture file or transcript | Pending |
| xHCI controller count reported | Pending |
| PCI segment:bus:device.function | Pending |
| Vendor / device IDs | Pending |
| BAR0 width / base / length / maximum / translation / attributes | Pending |
| BAR0 resource validation result and failure reason, if any | Pending |
| xHCI interface version / CAPLENGTH | Pending |
| Maximum slots / root ports / interrupters | Pending |
| Scratchpad-buffer requirement | Pending |
| Context size / 64-bit addressing support | Pending |
| DBOFF / RTSOFF and reported capability words | Pending |
| HCCPARAMS2, if reported for this interface version | Pending |
| First failed stage and exact EFI status, if any | Pending |
| Hang, firmware message, or recovery action | Pending |
| Confirmation that no controller or PCI write was attempted | Pending |

### AIDA_LPT

| Field | Observation |
| --- | --- |
| Test date/time and time zone | Pending; test only if safe launch is supported |
| Exact model / firmware version | Pending |
| Launch method / dedicated media | Pending |
| Secure Boot or trust-policy result | Pending |
| Application launched / returned successfully | Pending |
| Capture file or transcript | Pending |
| xHCI controller count reported | Pending |
| PCI segment:bus:device.function | Pending |
| Vendor / device IDs | Pending |
| BAR0 width / base / length / maximum / translation / attributes | Pending |
| BAR0 resource validation result and failure reason, if any | Pending |
| xHCI interface version / CAPLENGTH | Pending |
| Maximum slots / root ports / interrupters | Pending |
| Scratchpad-buffer requirement | Pending |
| Context size / 64-bit addressing support | Pending |
| DBOFF / RTSOFF and reported capability words | Pending |
| HCCPARAMS2, if reported for this interface version | Pending |
| First failed stage and exact EFI status, if any | Pending |
| Hang, firmware message, or recovery action | Pending |
| Confirmation that no controller or PCI write was attempted | Pending |

## Compatibility analysis

No cross-machine comparison is possible until actual captures are supplied.
Do not fill in missing values from hosted fixtures, another machine, or
expected xHCI defaults. After captures arrive, compare BAR layout and validated
apertures, capability limits and flags, scratchpad counts, doorbell/runtime
offsets, and firmware resource compatibility. Differences alone do not imply
a controller fault. A firmware-mediated read does not demonstrate that the
C# kernel can map or read the same BAR after `ExitBootServices`.

## Limitations and MI8 objective

This record does not qualify physical execution, Secure Boot acceptance,
controller capability values, or post-`ExitBootServices` kernel MMIO. No
controller initialization is authorized by these observations.

**Proposed MI8 objective:** review the actual MI7 captures and use their
validated BAR and capability ranges to specify and test the C# kernel-side
resource handoff and read-only MMIO mapping requirements. Keep controller
reset, ownership handoff, DMA, enumeration, and HID initialization outside
that frontier until the evidence and a separate safety review support them.

## Run summary

- Outcome: `MI7_HARDWARE_EVIDENCE_PENDING`
- Physical machines tested: none
- Physical xHCI observations: none
- Artifact: MI6 `guideXOS-XhciProbe.efi`, SHA-256 recorded above
- Artifact rebuilt or source modified: no
- Hosted tests run in this MI7 review: no
- QEMU: not used
- QAC admission: not requested
- GitHub push: not performed
