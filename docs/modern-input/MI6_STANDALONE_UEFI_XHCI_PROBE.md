# MI6 — Standalone UEFI xHCI Hardware Capability Probe

## Outcome

**`MI6_STANDALONE_EFI_PROBE_READY`**

A standalone x86-64 UEFI application was built and structurally verified. The
mocked UEFI probe suite and the MI4 hosted regressions pass. No physical
controller capability values have been observed; physical qualification is
pending.

## Checkout and revision

- Checkout: `D:\dev\guideXOSUEFI_MODERN_INPUT`
- Branch: `MODERN_INPUT`
- Starting HEAD: `3e1b18a92342d8776eb9fbe72fccd009468099d6`
- Ending HEAD: the final local MI6 commit on this branch; its exact SHA is
  recorded in the final run report.
- The pre-existing untracked `_mi2-validation-20261028/` and
  `guideXOS/artifacts/` directories were preserved.
- No other checkout, release bootloader, ISO, kernel, or persistent firmware
  entry was modified.
- No push was performed.

## Standalone application architecture

`Tools/XhciProbe/guideXOS-XhciProbe.vcxproj` builds only
`Tools/XhciProbe/main.cpp` plus the small freestanding probe core. It uses the
repository EDK2 `MdePkg` headers and MSVC's x64 target. Its `efi_main` is an
`extern "C" EFI_STATUS EFIAPI` entry point; the project links with the EFI
Application subsystem and `/NODEFAULTLIB`.

The application does not link the C# kernel, NativeAOT, any Phase 23–35 build
outputs, `ManagedArtifactIdentities.g.cs`, the complete guideXOS loader, or
release-image code. It uses no CRT, managed runtime, allocation library,
kernel stack, or kernel memory manager. The only allocated firmware buffers
are the handle list and temporary `GetBarAttributes` descriptor lists; both
are returned with `FreePool`.

The program writes through `EFI_SYSTEM_TABLE.ConOut`, displays each result,
holds the final screen for a bounded five seconds, then returns to firmware.
It does not wait for keyboard or mouse input. Serial output is not implemented;
the firmware text console is the diagnostic output path.

## Firmware discovery and BAR validation

The probe locates handles for `EFI_PCI_IO_PROTOCOL`, scans at most 256 handles,
and inspects at most eight matching xHCI functions. It checks PCI class
`0x0C`, subclass `0x03`, and programming interface `0x30`. For each match it
reports the segment:bus:device.function, vendor/device IDs, and class tuple.
Enumeration errors are counted, the first EFI status is retained, and one
controller's failed probe does not stop later controllers.

PCI identity, class, BAR0 low, and (for a 64-bit BAR0) BAR1 configuration
dwords are read through `EFI_PCI_IO_PROTOCOL.Pci.Read`. No PCI configuration
write or attribute-enable call exists in the probe interface. BAR0 resource
metadata comes from `GetBarAttributes(pci, 0, ...)`; the physical BAR base is
never dereferenced by the application.

The shared bounded descriptor parser requires one supported QWORD memory
resource descriptor followed by the end tag. The probe checks descriptor
validity, memory type, 32/64-bit width agreement with the PCI BAR, nonzero base
and aperture, descriptor maximum, checked `base + length - 1` arithmetic,
zero translation offset, and a minimum 0x20-byte capability aperture. It
rejects BAR configuration types it does not support. Descriptor bytes are
copied into fixed-width values and their firmware pool allocation is released
on success, malformed input, or a firmware error that nevertheless returns a
buffer. `GetBarAttributes` supported attributes and the validated range are
printed for review.

Every register read uses `EFI_PCI_IO_PROTOCOL.Mem.Read` with BAR index 0,
32-bit width, and an aperture-relative offset. No code adds the BAR base to a
pointer or maps the physical address itself. CAPLENGTH and HCIVERSION are
checked before reading the remaining fixed capability dwords. The probe reads
HCSPARAMS1–3, HCCPARAMS1, DBOFF, and RTSOFF at offsets 0x04–0x18. It reads
HCCPARAMS2 at 0x1C only when HCIVERSION is at least 1.1. Each access is checked
against the validated BAR aperture. A failed firmware read prints its exact
EFI status and ends that controller's probe without retrying.

The decoder reports interface version, maximum device slots, root-port count,
advertised interrupters, scratchpad-buffer count, 32/64-byte context size,
64-bit addressing support, and the decoded doorbell/runtime offsets. It checks
CAPLENGTH, nonzero controller limits, alignment, and that the advertised
doorbell and runtime register extents fit within the aperture.

## Read-only safety boundary

The probe vtable contains only protocol discovery, PCI configuration reads,
BAR resource reads, memory reads, and reporting. There is no controller write
operation. The app does not call `ExitBootServices`, reset or start an xHCI
controller, change ownership, enable bus mastering or PCI decoding, set PCI
attributes, program DMA, touch operational registers, rings, or doorbells,
enumerate USB devices, or initialize HID input.

## Hosted validation

The new hosted suite passes **18 cases and 74 assertions**. It covers no
controller, one and multiple controllers, class mismatch, missing and
malformed BAR resources, 32/64-bit BARs, range overflow, short apertures,
failed reads and exact status preservation, capability decoding, malformed
headers, scratchpads, unsupported offsets, read-only operations, descriptor
cleanup, failure continuation, and bounded scan/output.

The MI4 regressions also pass unchanged:

| Suite | Result |
| --- | ---: |
| C# xHCI discovery and synthetic MMIO | PASS, 131 assertions |
| C++ PCI resource descriptor and option parser | PASS, 26 assertions |
| C++ firmware/kernel handoff ABI | PASS, 149 assertions |
| **All MI6 and MI4 hosted suites** | **PASS, 380 assertions** |

The new C++ test command was:

```powershell
g++ -std=c++17 -Wall -Wextra -Werror Tests\XhciStandaloneProbeTests.cpp `
  -o out\mi6\XhciStandaloneProbeTests.exe
out\mi6\XhciStandaloneProbeTests.exe
```

The MI4 hosted C# and C++ suites were built into isolated `out/mi6/` paths.
The ABI test used `-Iedk2-master\MdePkg\Include` and
`-Iedk2-master\MdePkg\Include\X64`. No existing validation output directories
were reused or changed.

## Genuine EFI build and PE/COFF verification

The standalone Release build succeeded with Visual Studio 18 Community
MSBuild 18.11.1 and MSVC 14.51 (compiler/linker 19.51.36260.0 /
14.51.36260.0), using this checkout's EDK2 headers. The only compiler warning
was C4162 from EDK2 `Base.h`'s unused `_ReturnAddress` intrinsic declaration;
the build had zero errors.

Build command:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' `
  'Tools\XhciProbe\guideXOS-XhciProbe.vcxproj' /t:Rebuild /m `
  /p:Configuration=Release /p:Platform=x64 `
  /p:OutDir='D:\dev\guideXOSUEFI_MODERN_INPUT\out\mi6\' `
  /p:IntDir='D:\dev\guideXOSUEFI_MODERN_INPUT\out\mi6\obj\' /verbosity:minimal
python Tools\XhciProbe\verify_efi.py out\mi6\guideXOS-XhciProbe.efi
```

The structural verifier reports:

- PE32+ AMD64 machine `0x8664`.
- EFI Application subsystem `10`.
- Entry point RVA `0x00003C30`, located in executable `.text`.
- Import directory empty; COM/.NET descriptor empty.
- The output is unsigned and has no certificate table.
- COFF `IMAGE_FILE_DLL` characteristic is set by the MSVC EFI image link;
  the verified image subsystem remains `EFI_APPLICATION`. The firmware
  application type is identified by the EFI subsystem, and returning from an
  EFI application returns control to firmware. See the
  [Microsoft `/SUBSYSTEM` reference](https://learn.microsoft.com/en-us/cpp/build/reference/subsystem?view=msvc-170)
  and [TianoCore EDK II image build specification](https://tianocore-docs.github.io/edk2-BuildSpecification/release-1.28/edk2-BuildSpecification-release-1.28.pdf).

Artifact path:

`D:\dev\guideXOSUEFI_MODERN_INPUT\out\mi6\guideXOS-XhciProbe.efi`

SHA-256:

`5F6AC2A4927F9ABB7E1AC039B72BD3C00903BD2A6107D0599BC3C8FBF3A884F7`

`out/` is ignored by Git; the built executable remains isolated from and does
not overwrite release artifacts. The verifier and source are committed.

## Physical qualification status

No physical machine was booted, no real PCI/xHCI capability value was read,
and no QEMU or QAC session was used. The unsigned application's launch and
console behavior remain unqualified on hardware. Follow
[MI6_PHYSICAL_TEST_PROCEDURE.md](MI6_PHYSICAL_TEST_PROCEDURE.md) for the
controlled owner-run procedure on SARAH_DT, the ASUS VivoBook, and AIDA_LPT
when its UEFI boot environment supports a safe one-time launch.

## Kernel-side follow-up

The existing C# xHCI discovery implementation remains unchanged. A successful
UEFI diagnostic would establish only that firmware can read the controller
through its PCI I/O protocol. It would not establish kernel access after
`ExitBootServices`. Kernel integration still needs its own verified resource
handoff, physical MMIO mapping and cache policy, memory-map conflict checks,
capability validation, scratchpad allocation, ownership handling, and reviewed
controller initialization before USB enumeration or HID work.

## Run record and proposed MI7

- Outcome: `MI6_STANDALONE_EFI_PROBE_READY`
- Starting HEAD: `3e1b18a92342d8776eb9fbe72fccd009468099d6`
- Ending HEAD: final local MI6 commit; exact SHA is in the final run report.
- Hosted tests: 380 total assertions passed (74 new, 306 MI4 regressions).
- EFI build: Release pass; PE/COFF structural checks pass.
- Controller writes: none.
- Hardware capability values: none read.
- Production kernel required: no.
- QEMU/QAC: not used.
- Physical tests: not performed.
- Push: not performed.

**Proposed MI7 objective:** execute this signed or already-trusted diagnostic
on SARAH_DT and the ASUS VivoBook, then AIDA_LPT if its firmware permits,
without changing Secure Boot or persistent boot order. Review the captured
BAR and capability values before planning any separate kernel MMIO or
controller-initialization phase.
