# MI6 Physical UEFI xHCI Probe Procedure

**Status: not performed.** This procedure is for a later owner-run manual
qualification after reviewing the exact application hash and confirming that
the target firmware can launch it under its existing security policy.

## Scope and target order

The diagnostic reads PCI identity/class data, BAR0 resource metadata, and the
xHCI capability block through `EFI_PCI_IO_PROTOCOL`. It does not initialize
the controller or write PCI/xHCI registers. It does not require guideXOS, the
managed kernel, or keyboard/mouse input.

Qualify in this order:

1. `SARAH_DT`
2. `ASUS VivoBook`
3. `AIDA_LPT`, only if its existing UEFI boot environment supports an
   independent safe launch

Do not perform physical testing as part of MI6 implementation.

## Required artifact and security policy

Use the standalone application built by MI6:

```text
guideXOS-XhciProbe.efi
SHA-256: 5F6AC2A4927F9ABB7E1AC039B72BD3C00903BD2A6107D0599BC3C8FBF3A884F7
```

Confirm the file hash before copying it. The artifact is currently unsigned.
If Secure Boot or the machine's trust policy rejects it, stop. Do not disable
Secure Boot, enroll a key, change firmware security settings, or relax policy
for this procedure. A later run can use an artifact signed by a key already
trusted under the machine's current policy.

## Prepare a one-time launch

1. Keep the normal operating-system boot entry and a known recovery method
   available. Record the normal boot entry name and confirm it still boots.
2. Use a spare FAT32-formatted USB drive or another existing removable medium
   supported by the firmware. Back up anything on that medium before changing
   its files. Place the application at
   `\EFI\BOOT\BOOTX64.EFI`, or use a UEFI shell/file browser to launch it from
   its current filename.
3. Do not overwrite a normal OS bootloader or another existing
   `BOOTX64.EFI`. Use a separate blank test medium if that path is already in
   use.
4. Leave Secure Boot, xHCI mode, ownership, USB, and other firmware settings
   unchanged. Do not create a persistent firmware boot entry or change boot
   order.
5. Prefer the firmware's one-time **Boot from EFI file** menu or an already
   available UEFI shell. If using a shell, launch the file from its mapped
   filesystem (for example, `fs0:\EFI\BOOT\BOOTX64.EFI`).
6. The application writes to the firmware text console and keeps the final
   screen for five seconds. Arrange a camera before launch so the PCI
   controller identity, BAR0 lines, capability lines, and status can be
   captured. A serial capture path is not implemented.
7. Keep nonessential USB devices disconnected during the probe. Retain the
   removable boot medium needed to launch it. Do not attach a USB keyboard or
   mouse for the diagnostic's operation.

## Observe and record

For each run, record:

- Target name, exact model, firmware version, and date/time.
- The UEFI launch method and whether existing Secure Boot policy allowed it.
- The artifact SHA-256 used.
- A photo or transcript of the complete console output.
- PCI segment:bus:device.function, vendor/device IDs, class tuple, BAR0
  width/base/length/maximum/translation, and whether the resource was usable.
- xHCI version, CAPLENGTH, device slots, root ports, interrupters, scratchpad
  count, context-size and 64-bit-address flags, DBOFF, RTSOFF, and HCCPARAMS2
  when reported.
- Any exact EFI status and the first failure stage.
- Confirmation that no PCI or controller register write, reset, ownership
  change, DMA, enumeration, or HID action was attempted.

The application reports values only when read from that machine's firmware
protocols. A failed controller probe should stop at its first unsafe or failed
read, print the status, continue with the next bounded controller, and return
to firmware. Do not infer any expected value from another machine's capture.

## Return to the operating system

After the five-second display, the application returns to the UEFI shell or
firmware boot manager. Select the original normal operating-system entry from
the one-time boot menu. Do not alter persistent boot order. If the firmware
rejects the app, return to the same normal entry without changing its trust
settings. If the machine stalls, use its established power/recovery procedure
and select the retained normal boot entry; do not change xHCI mode or clear
firmware settings as a recovery step.

## Acceptance and limits

A useful capture establishes that this firmware instance exposed PCI
controller metadata and serviced the bounded read-only capability accesses.
It does not show that guideXOS can map or read the BAR after
`ExitBootServices`, nor that controller reset, ownership handoff, scratchpad
allocation, DMA, USB enumeration, or HID initialization is safe. Preserve each
machine's output independently for the MI7 review.
