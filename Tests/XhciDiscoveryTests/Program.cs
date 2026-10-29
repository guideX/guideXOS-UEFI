using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using guideXOS.Kernel.Drivers;

internal static class Program {
    private static int _assertions;

    private static void Check(bool condition, string name) {
        _assertions++;
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
    }

    private static void Main() {
        IdentifiesXhciClassCode();
        KeepsCapabilityProbeDisabledByDefault();
        Parses32BitBar();
        Parses64BitBar();
        RejectsInvalidBars();
        ValidatesMappingAndResourceBounds();
        DecodesCapabilities();
        RejectsMalformedCapabilities();
        RejectsUnavailableMmioWithoutReading();
        ReadsOnlyCapabilityWords();
        ValidatesFirmwareResourceHandoff();
        ValidatesSharedHandoffAbiFixture();
        AssociatesMultipleControllersByIdentity();
        ParsesFirmwareBarResources();
        RejectsUnsafeFirmwareResources();
        PlansBoundedMmioMappings();
        ValidatesUncachedPageAttributes();
        RejectsAccessBeforeMapping();
        PreservesReadOnlyAccessContract();
        ContinuesAfterSyntheticProbeFailure();
        Console.WriteLine("xHCI discovery tests: PASS (" +
                          _assertions + " assertions)");
    }

    private static void KeepsCapabilityProbeDisabledByDefault() {
        Check(!XhciDiscovery.IsCapabilityProbeEnabled(0),
              "capability probe disabled when no boot option is present");
        Check(!XhciDiscovery.IsCapabilityProbeEnabled(0x40),
              "resource handoff flag alone does not enable the probe");
        Check(XhciDiscovery.IsCapabilityProbeEnabled(
                  XhciDiscovery.XhciCapabilityProbeBootFlag),
              "explicit capability probe boot flag enables the probe");
    }

    private static void IdentifiesXhciClassCode() {
        Check(XhciDiscovery.IsXhciFunction(0x0C, 0x03, 0x30),
              "xHCI PCI class/subclass/programming interface accepted");
        Check(!XhciDiscovery.IsXhciFunction(0x0C, 0x03, 0x20),
              "EHCI programming interface rejected");
        Check(!XhciDiscovery.IsXhciFunction(0x0C, 0x03, 0x00),
              "UHCI/OHCI programming interface rejected");
        Check(!XhciDiscovery.IsXhciFunction(0x0C, 0x02, 0x30),
              "wrong PCI subclass rejected");
        Check(!XhciDiscovery.IsXhciFunction(0x03, 0x03, 0x30),
              "wrong PCI class rejected");
    }

    private static void Parses32BitBar() {
        XhciDiscovery.BarInfo bar;
        XhciDiscovery.BarStatus status = XhciDiscovery.TryParseMemoryBar(
            0xE0000008U, 0, true, out bar);
        Check(status == XhciDiscovery.BarStatus.Valid, "32-bit memory BAR accepted");
        Check(bar.BaseAddress == 0xE0000000UL, "32-bit BAR base masks attributes");
        Check(!bar.Is64Bit, "32-bit BAR width reported");
        Check(bar.IsPrefetchable, "32-bit BAR prefetch bit reported");
    }

    private static void Parses64BitBar() {
        XhciDiscovery.BarInfo bar;
        XhciDiscovery.BarStatus status = XhciDiscovery.TryParseMemoryBar(
            0xFEC01004U, 1U, true, out bar);
        Check(status == XhciDiscovery.BarStatus.Valid, "64-bit memory BAR accepted");
        Check(bar.BaseAddress == 0x00000001FEC01000UL,
              "64-bit BAR address reconstructed from upper and lower dwords");
        Check(bar.Is64Bit, "64-bit BAR width reported");
        Check(!bar.IsPrefetchable, "non-prefetchable BAR reported");
    }

    private static void RejectsInvalidBars() {
        XhciDiscovery.BarInfo bar;
        Check(XhciDiscovery.TryParseMemoryBar(0, 0, true, out bar) ==
              XhciDiscovery.BarStatus.Empty, "empty BAR rejected");
        Check(XhciDiscovery.TryParseMemoryBar(0xE0000001U, 0, true, out bar) ==
              XhciDiscovery.BarStatus.IoSpace, "I/O BAR rejected");
        Check(XhciDiscovery.TryParseMemoryBar(0xE0000002U, 0, true, out bar) ==
              XhciDiscovery.BarStatus.UnsupportedMemoryType,
              "below-1-MiB memory BAR rejected");
        Check(XhciDiscovery.TryParseMemoryBar(0xE0000006U, 0, true, out bar) ==
              XhciDiscovery.BarStatus.UnsupportedMemoryType,
              "reserved memory BAR type rejected");
        Check(XhciDiscovery.TryParseMemoryBar(0xE0000004U, 0, false, out bar) ==
              XhciDiscovery.BarStatus.MissingUpperBar,
              "64-bit BAR without an upper slot rejected");
        Check(XhciDiscovery.TryParseMemoryBar(0x00000004U, 0, true, out bar) ==
              XhciDiscovery.BarStatus.ZeroAddress, "zero 64-bit BAR address rejected");
        Check(XhciDiscovery.TryParseMemoryBar(0x00001004U, 0x8000U, true, out bar) ==
              XhciDiscovery.BarStatus.AddressNotCanonical,
              "64-bit BAR outside lower canonical address range rejected");
    }

    private static void ValidatesMappingAndResourceBounds() {
        ulong max = XhciDiscovery.MaximumIdentityMappedAddress;
        Check(XhciDiscovery.IsPhysicalRangeMappable(0xE0000000UL, 0x1000UL),
              "ordinary MMIO range maps");
        Check(!XhciDiscovery.IsPhysicalRangeMappable(0, 0x1000UL),
              "zero physical base rejected");
        Check(!XhciDiscovery.IsPhysicalRangeMappable(0x1000UL, 0),
              "empty physical range rejected");
        Check(XhciDiscovery.IsPhysicalRangeMappable(max - 4095UL, 4096UL),
              "range ending at canonical maximum accepted");
        Check(!XhciDiscovery.IsPhysicalRangeMappable(max - 3UL, 8UL),
              "physical range past canonical maximum rejected");
        Check(XhciDiscovery.IsSubrangeWithinResource(0x10000UL, 0x20UL, 4UL),
              "bounded dword range accepted");
        Check(!XhciDiscovery.IsSubrangeWithinResource(0, 0, 4),
              "zero resource size rejected");
        Check(!XhciDiscovery.IsSubrangeWithinResource(0x10000UL, 0x10001UL, 1),
              "offset outside resource rejected");
        Check(!XhciDiscovery.IsSubrangeWithinResource(0x10000UL, 0xFFFFUL, 2),
              "access extending past resource rejected");
        Check(!XhciDiscovery.IsSubrangeWithinResource(0x10000UL, 0x10000UL, 1),
              "one-past-end offset rejected");
    }

    private static void DecodesCapabilities() {
        XhciDiscovery.Capabilities capabilities;
        XhciDiscovery.CapabilityFailure failure;
        Check(XhciDiscovery.TryDecodeCapabilities(ValidRegisters(), 0x10000UL,
              out capabilities, out failure), "synthetic capability header accepted");
        Check(failure == XhciDiscovery.CapabilityFailure.None,
              "successful capability decode has no failure");
        Check(capabilities.CapabilityLength == 0x20, "CAPLENGTH captured");
        Check(capabilities.ControllerVersion == 0x0100,
              "HCIVERSION captured");
        Check(capabilities.MaximumDeviceSlots == 32,
              "maximum device slots decoded");
        Check(capabilities.AdvertisedInterruptResources == 8,
              "advertised interrupter count decoded");
        Check(capabilities.RootPortCount == 4, "root-port count decoded");
        Check(capabilities.HcsParams2 == ValidRegisters()[2], "HCSPARAMS2 captured");
        Check(capabilities.HcsParams3 == ValidRegisters()[3], "HCSPARAMS3 captured");
        Check(capabilities.HccParams1 == 5, "HCCPARAMS1 captured");
        Check(capabilities.ScratchpadBufferCount == 101,
              "scratchpad buffer count combines high and low fields");
        Check(capabilities.Uses64ByteContexts, "64-byte context capability decoded");
        Check(capabilities.Supports64BitAddresses,
              "64-bit address capability decoded");
        Check(capabilities.DoorbellOffset == 0x1000,
              "doorbell offset decoded and aligned");
        Check(capabilities.RuntimeRegisterOffset == 0x2000,
              "runtime offset decoded and aligned");
        Check(capabilities.U1DeviceExitLatency == 10,
              "HCSPARAMS3 U1 latency decoded");
        Check(capabilities.U2DeviceExitLatency == 200,
              "HCSPARAMS3 U2 latency decoded");
    }

    private static void RejectsMalformedCapabilities() {
        XhciDiscovery.Capabilities capabilities;
        XhciDiscovery.CapabilityFailure failure;
        uint[] registers = ValidRegisters();
        registers[0] = (registers[0] & 0xFFFFFF00U) | 0x1CU;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidCapabilityLength,
              "CAPLENGTH below fixed header rejected");

        registers = ValidRegisters();
        registers[0] = (registers[0] & 0xFFFFFF00U) | 0x21U;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidCapabilityLength,
              "unaligned CAPLENGTH rejected");

        registers = ValidRegisters();
        registers[0] = 0x20U;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidControllerVersion,
              "zero HCIVERSION rejected");

        registers = ValidRegisters();
        registers[1] &= ~0xFFU;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidControllerLimits,
              "zero maximum slots rejected");

        registers = ValidRegisters();
        registers[1] &= ~(0x7FFU << 8);
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidControllerLimits,
              "zero interrupters rejected");

        registers = ValidRegisters();
        registers[1] &= 0x00FFFFFFU;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidControllerLimits,
              "zero root ports rejected");

        registers = ValidRegisters();
        registers[5] |= 1U;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidDoorbellOffset,
              "misaligned doorbell offset rejected");

        registers = ValidRegisters();
        registers[5] = 0x1CU;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidDoorbellOffset,
              "doorbell offset before operational registers rejected");

        registers = ValidRegisters();
        registers[5] = 0xFFFCU;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.RegisterRangeOutOfBounds,
              "doorbell array beyond BAR resource rejected");

        registers = ValidRegisters();
        registers[6] |= 1U;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidRuntimeOffset,
              "misaligned runtime offset rejected");

        registers = ValidRegisters();
        registers[6] = 0x10U;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidRuntimeOffset,
              "runtime offset before operational registers rejected");

        registers = ValidRegisters();
        registers[6] = 0xFFE0U;
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.RegisterRangeOutOfBounds,
              "runtime interrupter block beyond BAR resource rejected");

        registers = ValidRegisters();
        Check(!XhciDiscovery.TryDecodeCapabilities(registers, 0x20,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.InvalidCapabilityLength,
              "CAPLENGTH operational register outside resource rejected");

        Check(!XhciDiscovery.TryDecodeCapabilities(new uint[6], 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.RegisterBlockMalformed,
              "short capability register block rejected");
    }

    private static void RejectsUnavailableMmioWithoutReading() {
        XhciDiscovery.Capabilities capabilities;
        XhciDiscovery.CapabilityFailure failure;
        Check(!XhciDiscovery.TryReadCapabilities(null, 0x10000,
              out capabilities, out failure), "missing MMIO reader rejected");
        Check(failure == XhciDiscovery.CapabilityFailure.AccessUnavailable,
              "missing MMIO reader reports unsupported access");

        FakeReader reader = new FakeReader(ValidRegisters());
        Check(!XhciDiscovery.TryReadCapabilities(reader, 0x1F,
              out capabilities, out failure), "undersized resource rejected before read");
        Check(failure == XhciDiscovery.CapabilityFailure.ResourceTooSmall,
              "undersized resource has explicit failure");
        Check(reader.ReadCount == 0, "no MMIO reads issued for undersized resource");
    }

    private static void ReadsOnlyCapabilityWords() {
        FakeReader reader = new FakeReader(ValidRegisters());
        XhciDiscovery.Capabilities capabilities;
        XhciDiscovery.CapabilityFailure failure;
        Check(XhciDiscovery.TryReadCapabilities(reader, 0x10000,
              out capabilities, out failure), "read-only synthetic access succeeds");
        Check(reader.ReadCount == 7, "only seven requested capability dwords read");
        Check(reader.LastOffset == 0x18UL, "last read is RTSOFF at 0x18");

        FakeReader failingReader = new FakeReader(ValidRegisters(), failAtOffset: 0x0CUL);
        Check(!XhciDiscovery.TryReadCapabilities(failingReader, 0x10000,
              out capabilities, out failure), "inaccessible register read rejected");
        Check(failure == XhciDiscovery.CapabilityFailure.ReadFailed,
              "inaccessible register has explicit failure");
        Check(failingReader.ReadCount == 4, "reader stops on first failed dword");
    }

    private static void ValidatesFirmwareResourceHandoff() {
        XhciDiscovery.ResourceFailure failure;
        XhciDiscovery.FirmwareResourceEntry[] entries = { Valid32BitResource() };
        Check(Marshal.SizeOf<XhciDiscovery.FirmwareResourceEntry>() == 64,
              "firmware resource entry layout is exactly 64 bytes");
        Check(XhciDiscovery.TryValidateResourceContract(
              XhciDiscovery.ResourceContractMagic,
              XhciDiscovery.ResourceContractVersion,
              XhciDiscovery.FirmwareResourceEntrySize, 1,
              XhciDiscovery.ResourceDiscoveryComplete, entries, out failure),
              "complete versioned resource handoff accepted");
        Check(!XhciDiscovery.TryValidateResourceContract(0, 1, 64, 1, 1,
              entries, out failure) && failure == XhciDiscovery.ResourceFailure.ContractMalformed,
              "bad resource handoff signature rejected");
        Check(!XhciDiscovery.TryValidateResourceContract(
              XhciDiscovery.ResourceContractMagic, 2, 64, 1, 1,
              entries, out failure) && failure == XhciDiscovery.ResourceFailure.ContractUnsupported,
              "unsupported resource handoff version rejected");
        Check(!XhciDiscovery.TryValidateResourceContract(
              XhciDiscovery.ResourceContractMagic, 1, 63, 1, 1,
              entries, out failure) && failure == XhciDiscovery.ResourceFailure.ContractMalformed,
              "wrong firmware entry size rejected");
        Check(!XhciDiscovery.TryValidateResourceContract(
              XhciDiscovery.ResourceContractMagic, 1, 64, 2, 1,
              entries, out failure) && failure == XhciDiscovery.ResourceFailure.ContractTruncated,
              "entry count beyond copied storage rejected");
        Check(!XhciDiscovery.TryValidateResourceContract(
              XhciDiscovery.ResourceContractMagic, 1, 64, 9, 1,
              new XhciDiscovery.FirmwareResourceEntry[9], out failure) &&
              failure == XhciDiscovery.ResourceFailure.ContractTruncated,
              "entry count beyond contract maximum rejected");
        Check(!XhciDiscovery.TryValidateResourceContract(
              XhciDiscovery.ResourceContractMagic, 1, 64, 1,
              XhciDiscovery.ResourceDiscoveryIncomplete, entries, out failure) &&
              failure == XhciDiscovery.ResourceFailure.DiscoveryIncomplete,
              "partial firmware discovery rejected");
        Check(!XhciDiscovery.TryValidateResourceContract(
              XhciDiscovery.ResourceContractMagic, 2, 64, 1,
              XhciDiscovery.ResourceDiscoveryComplete, entries, out failure) &&
              failure == XhciDiscovery.ResourceFailure.ContractUnsupported,
              "handoff version mismatch rejected");

        XhciDiscovery.FirmwareResourceEntry duplicate = Valid32BitResource();
        Check(!XhciDiscovery.TryValidateResourceContract(
              XhciDiscovery.ResourceContractMagic, 1, 64, 2, 1,
              new[] { Valid32BitResource(), duplicate }, out failure) &&
              failure == XhciDiscovery.ResourceFailure.DuplicateController,
              "duplicate controller BAR association rejected");
    }

    private static void ValidatesSharedHandoffAbiFixture() {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures",
            "xhci_resource_handoff_v1.hex");
        string fixtureText = File.ReadAllText(fixturePath);
        string fixtureHex = string.Concat(fixtureText.Split(
            new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        byte[] fixture = Convert.FromHexString(fixtureHex);
        Check(fixture.Length == 16 + 2 * XhciDiscovery.FirmwareResourceEntrySize,
              "shared firmware handoff fixture has fixed header and two entries");
        Check(Marshal.SizeOf<XhciDiscovery.FirmwareResourceEntry>() == 64,
              "C# firmware resource entry size matches the loader ABI");
        Check(16 + XhciDiscovery.MaximumFirmwareResources *
                    Marshal.SizeOf<XhciDiscovery.FirmwareResourceEntry>() == 528,
              "C# handoff capacity matches the 528-byte loader contract");

        Check(ResourceEntryOffsetsMatch(),
              "all C# resource entry field offsets match the packed C++ layout");

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(fixture.AsSpan(0, 4));
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(fixture.AsSpan(4, 2));
        ushort entrySize = BinaryPrimitives.ReadUInt16LittleEndian(fixture.AsSpan(6, 2));
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(fixture.AsSpan(8, 4));
        uint status = BinaryPrimitives.ReadUInt32LittleEndian(fixture.AsSpan(12, 4));
        Check(magic == XhciDiscovery.ResourceContractMagic && version == 1 &&
              entrySize == 64 && count == 2 &&
              status == XhciDiscovery.ResourceDiscoveryComplete,
              "C# parses the shared C++ serialized handoff header");

        XhciDiscovery.FirmwareResourceEntry first =
            MemoryMarshal.Read<XhciDiscovery.FirmwareResourceEntry>(
                fixture.AsSpan(16, 64));
        XhciDiscovery.FirmwareResourceEntry second =
            MemoryMarshal.Read<XhciDiscovery.FirmwareResourceEntry>(
                fixture.AsSpan(80, 64));
        Check(first.Segment == 0 && first.Bus == 0 && first.Device == 20 &&
              first.Function == 0 && first.VendorId == 0x8086 &&
              first.DeviceId == 0x1234 && first.MmioBase == 0xE0000000UL &&
              first.ApertureLength == 0x10000UL,
              "shared fixture first 32-bit controller entry decoded at expected offsets");
        Check(second.Segment == 0 && second.Bus == 2 && second.Device == 3 &&
              second.Function == 1 && second.VendorId == 0x8086 &&
              second.DeviceId == 0x5678 &&
              second.MmioBase == 0x00000001FEC01000UL &&
              second.ApertureLength == 0x10000UL,
              "shared fixture second 64-bit controller entry decoded at expected offsets");
        Check(XhciDiscovery.TryValidateResourceContract(magic, version, entrySize,
                  count, status, new[] { first, second },
                  out XhciDiscovery.ResourceFailure failure),
              "shared fixture passes C# contract validation");
        Check(XhciDiscovery.TryValidateFirmwareResource(first, 0, 0, 20, 0,
                  0x8086, 0x1234, 0x0C, 0x03, 0x30, 0xE0000000U, 0,
                  out failure),
              "shared fixture first controller identity and BAR validate");
        Check(XhciDiscovery.TryValidateFirmwareResource(second, 0, 2, 3, 1,
                  0x8086, 0x5678, 0x0C, 0x03, 0x30, 0xFEC01004U, 1,
                  out failure),
              "shared fixture second controller identity and 64-bit BAR validate");
    }

    private static bool ResourceEntryOffsetsMatch() {
        Type type = typeof(XhciDiscovery.FirmwareResourceEntry);
        string[] fields = {
            nameof(XhciDiscovery.FirmwareResourceEntry.Segment),
            nameof(XhciDiscovery.FirmwareResourceEntry.Bus),
            nameof(XhciDiscovery.FirmwareResourceEntry.Device),
            nameof(XhciDiscovery.FirmwareResourceEntry.Function),
            nameof(XhciDiscovery.FirmwareResourceEntry.BarIndex),
            nameof(XhciDiscovery.FirmwareResourceEntry.AddressWidth),
            nameof(XhciDiscovery.FirmwareResourceEntry.ResourceType),
            nameof(XhciDiscovery.FirmwareResourceEntry.GeneralFlags),
            nameof(XhciDiscovery.FirmwareResourceEntry.SpecificFlags),
            nameof(XhciDiscovery.FirmwareResourceEntry.VendorId),
            nameof(XhciDiscovery.FirmwareResourceEntry.DeviceId),
            nameof(XhciDiscovery.FirmwareResourceEntry.Reserved),
            nameof(XhciDiscovery.FirmwareResourceEntry.BarConfigurationBase),
            nameof(XhciDiscovery.FirmwareResourceEntry.MmioBase),
            nameof(XhciDiscovery.FirmwareResourceEntry.ApertureLength),
            nameof(XhciDiscovery.FirmwareResourceEntry.ResourceMaximum),
            nameof(XhciDiscovery.FirmwareResourceEntry.TranslationOffset),
            nameof(XhciDiscovery.FirmwareResourceEntry.SupportedAttributes)
        };
        int[] expected = { 0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 14,
                           16, 24, 32, 40, 48, 56 };
        for (int i = 0; i < fields.Length; i++) {
            if (Marshal.OffsetOf(type, fields[i]).ToInt32() != expected[i]) return false;
        }
        return true;
    }

    private static void AssociatesMultipleControllersByIdentity() {
        XhciDiscovery.FirmwareResourceEntry first = Valid32BitResource();
        XhciDiscovery.FirmwareResourceEntry second = new() {
            Segment = 0, Bus = 2, Device = 3, Function = 1, BarIndex = 0,
            VendorId = 0x8086, DeviceId = 0x5678
        };
        XhciDiscovery.ControllerIdentity firstController = new() {
            Segment = 0, Bus = 0, Device = 20, Function = 0,
            VendorId = 0x8086, DeviceId = 0x1234,
            ClassCode = 0x0C, Subclass = 0x03, ProgrammingInterface = 0x30
        };
        XhciDiscovery.ControllerIdentity secondController = new() {
            Segment = 0, Bus = 2, Device = 3, Function = 1,
            VendorId = 0x8086, DeviceId = 0x5678,
            ClassCode = 0x0C, Subclass = 0x03, ProgrammingInterface = 0x30
        };

        Check(XhciDiscovery.MatchFirmwareResourcesToControllers(
                  new[] { first, second }, new[] { secondController, firstController }),
              "multiple controllers associate by PCI location and device identity independent of order");
        Check(!XhciDiscovery.MatchFirmwareResourcesToControllers(
                  new[] { first }, new[] { firstController, secondController }),
              "missing controller resource rejected");
        Check(!XhciDiscovery.MatchFirmwareResourcesToControllers(
                  new[] { first, first }, new[] { firstController, secondController }),
              "ambiguous duplicate resource association rejected");
        secondController.DeviceId++;
        Check(!XhciDiscovery.MatchFirmwareResourcesToControllers(
                  new[] { first, second }, new[] { firstController, secondController }),
              "controller device identity mismatch rejected");
    }

    private static void ParsesFirmwareBarResources() {
        XhciDiscovery.ResourceFailure failure;
        Check(XhciDiscovery.TryValidateFirmwareResource(Valid32BitResource(),
              0, 0, 20, 0, 0x8086, 0x1234, 0x0C, 0x03, 0x30,
              0xE0000000U, 0, out failure),
              "32-bit GetBarAttributes memory resource matches BAR0");

        XhciDiscovery.FirmwareResourceEntry resource = Valid32BitResource();
        resource.AddressWidth = 64;
        resource.BarConfigurationBase = 0x00000001FEC01000UL;
        resource.MmioBase = 0x00000001FEC01000UL;
        resource.ResourceMaximum = 0x00000001FEC10FFFUL;
        Check(XhciDiscovery.TryValidateFirmwareResource(resource,
              0, 0, 20, 0, 0x8086, 0x1234, 0x0C, 0x03, 0x30,
              0xFEC01004U, 1U, out failure),
              "64-bit firmware resource matches reconstructed BAR0/BAR1");

        resource = Valid32BitResource();
        Check(!XhciDiscovery.TryValidateFirmwareResource(resource,
              0, 0, 21, 0, 0x8086, 0x1234, 0x0C, 0x03, 0x30,
              0xE0000000U, 0, out failure) &&
              failure == XhciDiscovery.ResourceFailure.ControllerMismatch,
              "resource associated with another BDF rejected");
        resource = Valid32BitResource();
        Check(!XhciDiscovery.TryValidateFirmwareResource(resource,
              0, 0, 20, 0, 0x8086, 0x1235, 0x0C, 0x03, 0x30,
              0xE0000000U, 0, out failure) &&
              failure == XhciDiscovery.ResourceFailure.ControllerMismatch,
              "resource associated with another device ID rejected");

        resource = Valid32BitResource();
        resource.BarConfigurationBase += 0x1000;
        Check(!Validate32(resource, out failure) &&
              failure == XhciDiscovery.ResourceFailure.BarMismatch,
              "firmware resource base contradicting PCI BAR0 rejected");
    }

    private static void RejectsUnsafeFirmwareResources() {
        XhciDiscovery.ResourceFailure failure;
        XhciDiscovery.FirmwareResourceEntry resource = Valid32BitResource();
        resource.ApertureLength = 0;
        Check(!Validate32(resource, out failure) &&
              failure == XhciDiscovery.ResourceFailure.ZeroLength,
              "missing aperture length rejected");

        resource = Valid32BitResource();
        resource.MmioBase = 0xFFFFFFFFFFFFFF00UL;
        resource.BarConfigurationBase = resource.MmioBase;
        resource.ApertureLength = 0x200UL;
        Check(!Validate32(resource, out failure) &&
              failure == XhciDiscovery.ResourceFailure.RangeOverflow,
              "overflowing firmware resource range rejected");

        resource = Valid32BitResource();
        resource.ResourceType = 1;
        Check(!Validate32(resource, out failure) &&
              failure == XhciDiscovery.ResourceFailure.InvalidResourceType,
              "I/O resource descriptor rejected");

        resource = Valid32BitResource();
        resource.TranslationOffset = 0x1000;
        Check(!Validate32(resource, out failure) &&
              failure == XhciDiscovery.ResourceFailure.UnsupportedTranslation,
              "non-identity resource translation rejected");

        resource = Valid32BitResource();
        resource.ResourceMaximum++;
        Check(!Validate32(resource, out failure) &&
              failure == XhciDiscovery.ResourceFailure.MissingAperture,
              "contradictory descriptor maximum rejected");

        resource = Valid32BitResource();
        resource.SpecificFlags = 0x02;
        Check(!Validate32(resource, out failure) &&
              failure == XhciDiscovery.ResourceFailure.InvalidMemoryAttributes,
              "cacheable resource attributes rejected");

        resource = Valid32BitResource();
        resource.SpecificFlags = 0x40;
        Check(!Validate32(resource, out failure) &&
              failure == XhciDiscovery.ResourceFailure.InvalidMemoryAttributes,
              "reserved resource attributes rejected");

        resource = Valid32BitResource();
        resource.AddressWidth = 64;
        Check(!Validate32(resource, out failure) &&
              failure == XhciDiscovery.ResourceFailure.InvalidBar,
              "descriptor address width contradicting BAR type rejected");

        resource = Valid32BitResource();
        resource.GeneralFlags = 0x80;
        Check(!Validate32(resource, out failure) &&
              failure == XhciDiscovery.ResourceFailure.InvalidMemoryAttributes,
              "reserved general resource flags rejected");

        resource = Valid32BitResource();
        resource.Device = 32;
        Check(!XhciDiscovery.TryValidateFirmwareResource(resource,
              0, 0, 32, 0, 0x8086, 0x1234, 0x0C, 0x03, 0x30,
              0xE0000000U, 0, out failure) &&
              failure == XhciDiscovery.ResourceFailure.InvalidBdf,
              "out-of-range PCI device number rejected");
    }

    private static void PlansBoundedMmioMappings() {
        Check(XhciDiscovery.TryBuildMmioMappingPlan(0xE0000FF0UL,
              0x1000UL, 0, 0x20, XhciDiscovery.PageSize,
              out XhciDiscovery.MmioMappingPlan plan),
              "unaligned BAR capability window receives mapping plan");
        Check(plan.FirstPhysicalPage == 0xE0000000UL &&
              plan.FirstPageOffset == 0xFF0UL && plan.PageCount == 2,
              "mapping plan rounds page span and preserves BAR page offset");
        Check(!XhciDiscovery.TryBuildMmioMappingPlan(0xE0000000UL,
              0x1000UL, 0xFF0UL, 0x20UL, 4096,
              out plan), "access beyond firmware aperture gets no mapping plan");
        Check(!XhciDiscovery.TryBuildMmioMappingPlan(
              XhciDiscovery.MaximumIdentityMappedAddress - 1, 4, 0, 4, 4096,
              out plan), "mapping range overflow rejected before page rounding");
        Check(!XhciDiscovery.TryBuildMmioMappingPlan(0xE0000000UL,
              0x1000UL, 0, 0x20UL, 3000, out plan),
              "non-power-of-two page size rejected");
    }

    private static void ValidatesUncachedPageAttributes() {
        Check(XhciDiscovery.TryBuildUncachedPteFlags(
              0x0007040600070406UL, out ulong flags),
              "standard PAT table provides an exact UC encoding");
        Check((flags & 0x18UL) == 0x18UL && (flags & 0x80UL) == 0,
              "UC PAT selector is chosen without assuming a PAT bit");
        Check((flags & 0x2UL) == 0 && (flags & (1UL << 63)) != 0,
              "MMIO leaf is read-only and non-executable");
        Check(!XhciDiscovery.TryBuildUncachedPteFlags(
              0x0606060606060606UL, out flags),
              "PAT without an exact UC entry is rejected");
    }

    private static void RejectsAccessBeforeMapping() {
        XhciDiscovery.Capabilities capabilities;
        XhciDiscovery.CapabilityFailure failure;
        FakeReader reader = new FakeReader(ValidRegisters(), isMapped: false);
        Check(!XhciDiscovery.TryReadCapabilities(reader, 0x10000,
              out capabilities, out failure) &&
              failure == XhciDiscovery.CapabilityFailure.AccessUnavailable,
              "register access rejected before successful mapping");
        Check(reader.ReadCount == 0,
              "unmapped access rejection issues no synthetic reads");
    }

    private static void PreservesReadOnlyAccessContract() {
        FakeReader reader = new FakeReader(ValidRegisters());
        XhciDiscovery.Capabilities capabilities;
        XhciDiscovery.CapabilityFailure failure;
        Check(XhciDiscovery.TryReadCapabilities(reader, 0x10000,
              out capabilities, out failure),
              "synthetic register block remains readable");
        Check(reader.ReadCount == 7 && reader.WriteCount == 0,
              "synthetic capability path performs reads only");
        Check(typeof(XhciDiscovery.IReadOnlyMmioReader).GetMethod("TryWrite32") == null,
              "MMIO access interface exposes no write method");
    }

    private static void ContinuesAfterSyntheticProbeFailure() {
        FakeReader failedReader = new FakeReader(ValidRegisters(), failAtOffset: 0x0CUL);
        FakeReader succeedingReader = new FakeReader(ValidRegisters());
        XhciDiscovery.Capabilities capabilities;
        XhciDiscovery.CapabilityFailure failedReadFailure;
        XhciDiscovery.CapabilityFailure successFailure;
        bool firstSucceeded = XhciDiscovery.TryReadCapabilities(failedReader,
            0x10000, out capabilities, out failedReadFailure);
        bool secondSucceeded = XhciDiscovery.TryReadCapabilities(succeedingReader,
            0x10000, out capabilities, out successFailure);
        Check(!firstSucceeded && failedReadFailure == XhciDiscovery.CapabilityFailure.ReadFailed,
              "a failed synthetic controller read is isolated to that controller");
        Check(secondSucceeded && successFailure == XhciDiscovery.CapabilityFailure.None,
              "later controller capability probe can proceed after a rejected read");
        Check(failedReader.WriteCount == 0 && succeedingReader.WriteCount == 0,
              "success and failure paths do not write controller registers");
    }

    private static XhciDiscovery.FirmwareResourceEntry Valid32BitResource() {
        const ulong baseAddress = 0xE0000000UL;
        const ulong length = 0x10000UL;
        return new XhciDiscovery.FirmwareResourceEntry {
            Segment = 0,
            Bus = 0,
            Device = 20,
            Function = 0,
            BarIndex = 0,
            AddressWidth = 32,
            ResourceType = 0,
            GeneralFlags = 0x0F,
            SpecificFlags = 0x01,
            VendorId = 0x8086,
            DeviceId = 0x1234,
            Reserved = 0,
            BarConfigurationBase = baseAddress,
            MmioBase = baseAddress,
            ApertureLength = length,
            ResourceMaximum = baseAddress + length - 1,
            TranslationOffset = 0,
            SupportedAttributes = 0
        };
    }

    private static bool Validate32(XhciDiscovery.FirmwareResourceEntry entry,
            out XhciDiscovery.ResourceFailure failure) {
        return XhciDiscovery.TryValidateFirmwareResource(entry,
            0, 0, 20, 0, 0x8086, 0x1234, 0x0C, 0x03, 0x30,
            0xE0000000U, 0, out failure);
    }

    private static uint[] ValidRegisters() {
        uint[] registers = new uint[7];
        registers[0] = (0x0100U << 16) | 0x20U;
        registers[1] = 32U | (8U << 8) | (4U << 24);
        registers[2] = (3U << 27) | (5U << 21);
        registers[3] = 10U | (200U << 16);
        registers[4] = 5U;
        registers[5] = 0x1000U;
        registers[6] = 0x2000U;
        return registers;
    }

    private sealed class FakeReader : XhciDiscovery.IReadOnlyMmioReader {
        private readonly uint[] _registers;
        private readonly ulong _failAtOffset;
        private readonly bool _isMapped;

        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public ulong LastOffset { get; private set; }
        public bool IsMapped => _isMapped;

        public FakeReader(uint[] registers, ulong failAtOffset = ulong.MaxValue,
                bool isMapped = true) {
            _registers = registers;
            _failAtOffset = failAtOffset;
            _isMapped = isMapped;
        }

        public bool TryRead32(ulong offset, out uint value) {
            ReadCount++;
            LastOffset = offset;
            value = 0;
            if (offset == _failAtOffset || (offset & 3UL) != 0 ||
                offset / 4UL >= (ulong)_registers.Length) return false;
            value = _registers[(int)(offset / 4UL)];
            return true;
        }
    }
}
