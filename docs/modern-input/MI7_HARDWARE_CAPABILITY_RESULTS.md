# MI7 — Real Hardware xHCI Capability Qualification

## Outcome

**`SARAH_DT_DESCRIPTOR_CAPTURED; REVISED_READ_ONLY_PROBE_READY; PHYSICAL_QUALIFICATION_PENDING`**

Two operator-reported SARAH_DT runs are now on record. The first established
that the diagnostic launched and returned to the UEFI Shell. The second
captured a successful `GetBarAttributes()` response and the exact BAR0
descriptor below, but the then-current parser rejected it before capability
access. Source review established that EDK II reports the PCI BAR alignment
mask in `AddrRangeMax`. The bounded compatibility change passes hosted tests,
and a new isolated EFI image is ready for a physical retest. The revised image
has not been run on hardware, so physical capability-register qualification
remains pending. See
[MI7_SARAH_DT_BAR0_INVESTIGATION.md](MI7_SARAH_DT_BAR0_INVESTIGATION.md) for
the parser rule and test record.

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

### MI7-R2 isolated diagnostic

Build and hosted verification date: **2026-11-08 (America/Los_Angeles)**

| Item | Verified value |
| --- | --- |
| EFI application | `out\mi7-r2\guideXOS-XhciProbe.efi` |
| SHA-256 | `21828286BA483D2D03A8DF8A31A20281DE16F3F78D98287994A293D67577DF05` |
| Structural verification | PASS; AMD64 PE32+, EFI Application, no imports or COM descriptor |
| Loader build | PASS; output isolated under `out\mi7-r2-loader\` |
| Hosted regressions | PASS, 421 assertions across four suites |
| Physical run of this revision | None |

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

The SARAH_DT rows include the operator's reported first run. Missing details
remain pending; preserve the original image or a verbatim transcript when
available. ASUS VivoBook and AIDA_LPT remain pending owner evidence.

### SARAH_DT

| Field | Observation |
| --- | --- |
| Test date/time and time zone | Not recorded |
| Exact model / firmware version | Not reported |
| Launch method / dedicated media | Launched from UEFI Shell; medium not reported |
| Secure Boot or trust-policy result | Application launched; exact trust-policy state not recorded |
| Application launched / returned successfully | Yes; returned to UEFI Shell |
| Capture file or transcript | Not provided |
| xHCI controller count reported | 1 |
| PCI segment:bus:device.function | `0000:00:14.0` |
| Vendor / device IDs | `8086:06ED` |
| PCI class tuple | `0C/03/30` |
| First-run display | Width/base/length `0 / 0 / 0`; parser defaults, not firmware values |
| Descriptor type / payload length | `0x8A` / `0x002B` (QWORD Address Space) |
| Resource type / flags / granularity | Memory (`0`); general `0`; specific `0`; `32` bits |
| BAR0 base / maximum / length / translation | `0xE1020000` / `0xFFFF` / `0x10000` / `0` |
| Descriptor terminator / EFI status | `0x79 0x00` / `EFI_SUCCESS` on the second capture |
| BAR0 resource validation result and failure reason, if any | The captured image rejected it because it required inclusive maximum `base + length - 1` (`0xE102FFFF`). EDK II reports the BAR alignment mask here; `0xFFFF` is the mask for 64 KiB. Revised parser accepts this only with power-of-two mask/size agreement, aligned base, and checked aperture end. |
| Revised interpretation | EDK II alignment mask; checked aperture `[0xE1020000, 0xE102FFFF]` |
| Revised image status | Hosted tests pass; physical retest pending. No capability registers were read in the reported second run. |
| xHCI interface version / CAPLENGTH | Not read |
| Maximum slots / root ports / interrupters | Not read |
| Scratchpad-buffer requirement | Not read |
| Context size / 64-bit addressing support | Not read |
| DBOFF / RTSOFF and reported capability words | Not read |
| HCCPARAMS2, if reported for this interface version | Not read |
| First failed stage and exact EFI status, if any | Parser rejection; exact EFI status not displayed |
| Hang, firmware message, or recovery action | Returned to UEFI Shell; no hang reported |
| Confirmation that no controller or PCI write was attempted | Probe is read-only; no write was reported |

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

This record does not qualify the revised image's physical execution, Secure
Boot acceptance, controller capability values, or post-`ExitBootServices`
kernel MMIO. The second capture establishes a valid resource convention and
the prior software rejection cause, but not physical capability access. No
controller initialization is authorized by these observations.

**Proposed MI8 objective:** review the actual MI7 captures and use their
validated BAR and capability ranges to specify and test the C# kernel-side
resource handoff and read-only MMIO mapping requirements. Keep controller
reset, ownership handoff, DMA, enumeration, and HID initialization outside
that frontier until the evidence and a separate safety review support them.

## Run summary

- Outcome: `SARAH_DT_DESCRIPTOR_CAPTURED; PHYSICAL_RETEST_PENDING`
- Operator-reported physical evidence: SARAH_DT descriptor capture; no documented capability reads
- Revised image physically tested in this assignment: no
- Artifact: MI7-R2 `out\mi7-r2\guideXOS-XhciProbe.efi`, SHA-256 recorded above
- Hosted regressions: PASS, 421 assertions
- Loader build: PASS, isolated under `out\mi7-r2-loader\`
- QEMU: not used
- QAC admission: not requested
- Physical testing during this assignment: not performed
- GitHub push: not performed
