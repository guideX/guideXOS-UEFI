# MI4 xHCI Boot-Chain and Probe Readiness

## Outcome

**`MI4_HOSTED_READY_BUILD_BLOCKED`**

The opt-in diagnostic path builds in the complete UEFI loader, the C# kernel sources compile in an isolated fail-closed stub pass, and all hosted xHCI checks pass. A normal kernel build still stops at the missing upstream-generated `guideXOS/obj/ManagedArtifactIdentities.g.cs`. No production kernel or ISO is available, so physical qualification is not authorized by this result.

Branch: `MODERN_INPUT`

Starting HEAD: `1ec202add2754665e681878582d85177c236d306`

Local changes only; no push.

## Boot-chain audit

The xHCI resource path is:

1. Before `ExitBootServices`, `DiscoverXhciPciResources` enumerates EFI PCI I/O handles, reads the PCI identity and BAR0, and obtains the firmware BAR resource attributes. It accepts only the supported single QWORD memory descriptor and copies fixed-width values into loader-owned storage. The scan is bounded to eight resources and marks incomplete/truncated discovery unusable.
2. The version 1 `PciResourceHandoff` follows the existing `BootMemoryHandoff` in the loader image. It is 528 bytes and contains an eight-entry capacity; each resource entry is 64 bytes. No EFI protocol pointer or temporary pool descriptor is retained.
3. BootInfo remains version 1 and 168 bytes. Flag bit 6 announces the appended resource contract; bit 7 now records the explicit capability-probe option. The loader computes the BootInfo checksum after setting flags.
4. The kernel checks the appended range against the firmware memory map, reads the resource extension only when bit 6 is present, and reserves the handoff ranges from allocator reuse. A missing or invalid extension produces a diagnostic and does not stop ordinary startup.
5. After kernel PCI enumeration, the diagnostic compares every firmware resource one-to-one with a kernel-discovered xHCI function using segment, bus, device, function, vendor, and device IDs. Duplicate, missing, or mismatched associations stop access before MMIO.
6. The opt-in report runs after `ThreadPool.Initialize`, which enables `EFER.NXE`, and before scheduler start. This ordering is required because the MMIO leaf entries carry NX.
7. Only the opt-in UEFI path validates BAR0 and the memory-map range, installs a bounded mapping, reads the capability header, validates it, logs the result, and returns to normal startup.

The original BootInfo layout and handoff prefix are unchanged. Older producers do not set the new probe flag, so a new kernel leaves the capability path dormant. The runtime path is opt-in without rebuilding the kernel: omit or include the exact loader image option `--xhci-capability-probe`.

## ABI and resource validation

`guideXOSBootLoader/paging.h` now asserts the entry size, alignment, every entry field offset, handoff size/header offsets, and adjacency to the legacy handoff. The C# entry uses an explicit 64-byte packed layout. The shared fixture `Tests/Fixtures/xhci_resource_handoff_v1.hex` is parsed by the C# suite and compared byte-for-byte against the production C++ handoff structure by the hosted ABI test.

The fixture covers two controllers in reverse association order and includes both a 32-bit BAR address and a 64-bit address above 4 GiB. The C# checks verify the fixed header, entries, field offsets, and exact BDF/device identity association. The match is one-to-one, bounded to eight entries, and rejects ambiguous duplicates, unsupported class codes, and missing or mismatched identities.

The existing resource validator rejects invalid BDFs, non-BAR0 entries, non-memory or cacheable attributes, zero/overflowing ranges, nonzero translations, resource/BAR disagreement, and 32/64-bit width disagreement. It checks the full aperture and lower canonical x86-64 address range before mapping.

## Read-only MMIO review

The new report call is gated by both UEFI boot mode and BootInfo flag bit 7. The firmware BAR metadata is the only source of the mapping range. The memory-map guard rejects malformed descriptor sizes/counts, zero or overflowing descriptor page ranges, and overlaps with any descriptor other than `EfiMemoryMappedIO`.

`PageTable.TryMapMmioAccess` maps only the identity address span required for the 32-byte capability-header access. It requires PAT support and a PAT entry whose encoding is UC, limits the plan to 16 pages, preflights all target leaves, refuses any occupied leaf, and installs supervisor, read-only, NX entries. It never retags an occupied identity leaf; if an earlier loader mapping overlaps the BAR, the probe stops before access. The source-level mapping inventory includes loader/kernel ranges, heap, framebuffer, APIC, IOAPIC, and ECAM. It does not scan arbitrary non-identity aliases, so target-specific page-table state remains unqualified until real hardware boots the authentic image. The returned mapping has stable lifetime for the rest of the boot.

The reader exposes only `TryRead32`. It reads seven dwords at offsets `0x00` through `0x18`; it does not read HCCPARAMS2 at `0x1C`, operational registers, runtime registers, or doorbells. Capability decoding validates CAPLENGTH, version, controller limits, doorbell/runtime offsets, and their ranges within the trusted BAR aperture. The runtime access uses the native scalar `ReadMmio32` load. No controller register write or controller initialization is present.

Each prerequisite failure logs its frontier and returns to normal startup. A failed controller does not prevent trying another matched controller. The diagnostic loop is bounded by the eight-resource handoff and performs no xHCI polling or retry. UEFI `BootConsole` routes kernel diagnostics to COM1; the existing `Serial.Initialize` setup is 38400 baud, 8 data bits, no parity, one stop bit. Its existing UART transmitter-ready wait remains blocking, so physical qualification requires a working serial path.

## Changes

- `guideXOSBootLoader/main.cpp`, `guidexOSBootInfo.h`, `pci_resource_parser.h`, and `paging.h`: exact bounded LoadOptions token parsing, the opt-in BootInfo flag, and ABI assertions.
- `guideXOS/Kernel/Misc/UefiBootInfo.cs`, `Kernel/Drivers/XhciDiscovery.cs`, `Kernel/Misc/EntryPoint.cs`, and `Kernel/Misc/USB.cs`: probe gating, strict one-to-one identity matching, fail-closed memory descriptor checks, and bounded result logging.
- `Tests/XhciDiscoveryTests/Program.cs` and its project file: shared fixture parsing, layout and multi-controller tests, disabled-by-default checks, handoff-version rejection, and synthetic failure continuation.
- `Tests/XhciPciResourceDescriptorTests.cpp`: exact option-token parser checks.
- `Tests/Fixtures/xhci_resource_handoff_v1.hex` and `Tests/XhciPciResourceHandoffAbiTests.cpp`: shared loader/kernel serialization fixture and production-struct byte comparison.

## Hosted qualification

| Suite | Result |
| --- | ---: |
| C# xHCI discovery and synthetic MMIO | PASS, 131 assertions |
| C++ PCI descriptor and option parser | PASS, 26 assertions |
| C++ production handoff ABI | PASS, 149 assertions |
| **Total** | **306 assertions** |

The hosted checks use synthetic descriptors and mock read-only MMIO. They do not access a real PCI function or xHCI register.

## Build and package evidence

**Loader full build:** Pass. Visual Studio 18 Community MSBuild 18.11.1, MSVC 14.44.35207, Release|x64, and the repository EDK2 headers compiled and linked the complete EFI application. There were five existing/toolchain warnings and zero errors. The isolated output is under `%TEMP%\guideXOS-mi4-loader`; the EFI executable is `bin\guideXOSBootLoader.exe`. These loader artifacts are retained there.

**Kernel normal build:** Blocked. `dotnet build guideXOS/guideXOS.csproj --configuration Release -p:SkipISO=true` with isolated output fails with `CS2001` because `guideXOS/obj/ManagedArtifactIdentities.g.cs` is absent. The supported build flow generates that source from managed artifact records; the required Phase 29 record `out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json` and upstream Phase 19–23 inputs are absent. The Phase 29/infrastructure workstream owns restoring those inputs. No identity data was regenerated, copied, or fabricated.

**Kernel isolated source compilation:** Pass, 0 errors and 142 warnings, using a temporary compile-only `ManagedArtifactIdentities.Matches` stub that always returns false. The build output is retained under `%TEMP%\guideXOS-mi4-kernel`. The stub was removed after compilation; the pass validates C# source integration only and is not a production kernel build, NativeAOT publish, or identity validation. The pre-existing ignored `guideXOS/native_stubs.obj` was restored byte-for-byte after the project build target regenerated it.

**Loader syntax check:** Covered by the full MSVC build.

**Kernel production build without stubs:** Blocked by the generated identity source.

**ISO/package:** Not generated.

**QEMU:** Not used.

**Physical hardware/registers:** Not accessed; no xHCI hardware values have been observed.

**Push:** None.

## Next steps and proposed MI5

1. The owner of the upstream managed-artifact workstream restores the authentic Phase 19–23 inputs and Phase 29 build record, then generates `ManagedArtifactIdentities.g.cs` through the supported build process.
2. Run the unmodified production build without stubs, publish the kernel, stage the successfully built loader and kernel into a new isolated UEFI image, and record hashes for the loader, kernel, and image.
3. Only after that image is reproducible and reviewed, perform the owner-executed capability-only procedure in `MI4_PHYSICAL_QUALIFICATION_PLAN.md`.
4. Proposed MI5 objective: complete the authentic production build/package and accept or reject the first physical capability-only capture. Keep controller initialization, DMA, enumeration, and HID work out of scope until that review.
