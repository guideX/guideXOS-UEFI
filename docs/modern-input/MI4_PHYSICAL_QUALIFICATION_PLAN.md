# MI4 First Physical xHCI Capability Qualification

**Status: pending an authentic production image.** MI4 is classified `MI4_HOSTED_READY_BUILD_BLOCKED`; the steps below are for the owner after the upstream managed-artifact dependency is restored and a reviewed image is produced. Do not use an image built with the compile-only identity stub.

## Scope

The first run records the xHCI PCI identity and BAR0 metadata, maps the minimum capability-header access as uncached/read-only/NX, reads the fixed capability registers, and continues boot. It does not initialize the controller, program DMA, enumerate USB devices, or exercise HID input.

## Required image and machine

Before scheduling the hardware run, require all of the following:

1. Authentic Phase 19–23 and Phase 29 build inputs restored by their owner.
2. `guideXOS/obj/ManagedArtifactIdentities.g.cs` generated through the supported build process from authentic records.
3. A successful normal kernel production build and publish with no stub or build bypass.
4. A complete Release UEFI loader built from this branch, packaged with that kernel into a new isolated image, and hash recorded for the loader, kernel, and image.
5. A known-good normal boot option or recovery image retained, and the target machine has a usable COM1 serial capture path or an established firmware console capture method.

Do not overwrite the mainline RC1 ISO. MI4 has not generated a diagnostic image.

## Enable and disable

Create a one-time UEFI boot entry for the reviewed diagnostic loader, or launch the staged loader from a UEFI shell. Set the loader image's **LoadOptions** to this exact unquoted token:

```text
--xhci-capability-probe
```

The flag is read from UEFI `EFI_LOADED_IMAGE_PROTOCOL.LoadOptions`; putting it in the kernel command line or elsewhere will not enable the probe. The loader accepts the exact standalone token, with ordinary whitespace separators. Missing, malformed, odd-sized, or oversized option data leaves the probe disabled.

To disable it, remove the token from the one-time entry or choose the unchanged normal boot entry. No kernel rebuild is needed to toggle the option once the reviewed loader is installed. Keep the normal entry free of the token.

## Safe preparation

1. Use a non-critical test machine with an owner present at the console and a verified way to power-cycle it.
2. Record the board/model, firmware version, xHCI controller PCI identity if available from firmware tools, and current boot entry.
3. Disconnect removable USB storage and other external USB devices before power-on. Do not use a USB serial adapter connected to the target as the only log path; use a motherboard COM1/header or a separately verified capture method.
4. Leave firmware USB settings at their normal values. Do not change xHCI mode, handoff, security, or boot policy as part of this test.
5. Confirm the recovery entry boots without `--xhci-capability-probe`, and confirm the image hashes match the reviewed values before selecting the one-time diagnostic entry.
6. Capture the EFI loader console and the kernel serial output. The kernel `BootConsole` sends UEFI output to COM1; existing initialization configures 38400 baud, 8 data bits, no parity, one stop bit. Its transmitter-ready wait is blocking, so confirm the COM1 capture path works before the run.

## Expected output and behavior

The loader should print `[XHCI-PROBE] enabled by image LoadOptions` and the existing `[XHCI-RESOURCE] status=... entries=...` line. Kernel diagnostics run after `[SCHED] ThreadPool.Initialize complete` and before scheduler start. Kernel lines begin with `[USB][XHCI]` and include the PCI segment:bus:device.function and vendor:device IDs.

For each validated controller, expect a bounded sequence similar to:

```text
[USB][XHCI] controller=matched bar0=validated base=0x... aperture=0x... pci=.... id=....
[USB][XHCI] mmio-map=success attributes=uc,read-only,nx access=0x20 pci=.... id=....
[USB][XHCI] capability-read=hardware-mmio source=efi-pci-io base=0x... aperture=0x... pci=.... id=....
[USB][XHCI] capabilities version=0x.... caplength=0x.. slots=... interrupters=... ports=... scratchpads=... ctx64=... addr64=... dboff=0x... rtsoff=0x...
```

Values are platform-specific. This plan supplies no expected hardware values. A successful run should proceed to the ordinary guideXOS startup without USB enumeration or xHCI controller initialization. With the option omitted, the loader does not set the probe flag and kernel `[USB][XHCI]` capability diagnostics should not appear.

## Failure classification

Capture the first failure frontier and all following normal-startup output:

| Output marker | Meaning and action |
| --- | --- |
| `resource-handoff-unavailable` or `resource-handoff=unavailable` | Loader extension or memory-map range not trusted; no capability access. Check image pair and handoff provenance. |
| `resource-handoff-rejected reason=...` | Version, status, count, or descriptor contract rejected; no capability access. Preserve the full reason and firmware status. |
| `firmware-kernel-discovery-mismatch` / `pci-function-missing` / `pci-identity-mismatch` | Firmware and kernel PCI views disagree. Do not retry with relaxed identity checks. |
| `resource-rejected reason=...` | BAR0 or resource validation failed. Record the BDF, IDs, and reason. |
| `mmio-map=blocked` | Memory-map overlap or malformed descriptor; no capability read. |
| `mmio-map=failed reason=range-pat-or-page-table` | Mapping precondition failed, PAT is unsuitable, or a target PTE is already occupied. The mapping path fails closed and does not retag an existing leaf. |
| `capability-read=failed reason=...` | Read failed or capability fields/ranges were invalid. The probe returns to ordinary startup. |
| Valid `capabilities` line | Capability-only discovery completed. Save the full log and hashes; do not infer that controller initialization is safe. |

If a controller fails, the diagnostic should continue checking the next matched controller and then return to normal startup. Any loss of normal startup is a failed qualification even if capability output was printed.

## Evidence to retain

- SHA-256 of the exact loader, kernel, and isolated image used.
- Firmware/board model and version; boot entry name and the exact LoadOptions string.
- Complete EFI console and COM1 capture, including the first failure frontier if any.
- PCI BDF and IDs, BAR0 base/aperture and validation marker, mapping result, and capability output.
- Whether ordinary startup completed and whether the probe was absent on a subsequent normal boot with the token omitted.
- Confirmation that no xHCI initialization, DMA, enumeration, or HID activity was attempted.

## Recovery

If the diagnostic boot stalls or does not complete, power the machine off using the platform's normal recovery procedure. Select the retained normal UEFI entry or known-good recovery image with the token omitted. No controller state was written by the capability probe, so do not clear firmware settings or change xHCI mode as a recovery step. If normal boot also fails, stop testing and use the machine owner's established firmware/boot-media recovery procedure. Preserve logs and image hashes; do not repeat with modified mappings or relaxed validation.
