#include <stdint.h>
#include <stdio.h>

#include "../Tools/XhciProbe/xhci_probe_core.h"

using namespace guideXOS::xhci_probe;

static uint32_t gAssertions = 0;
static uint32_t gFailures = 0;
static uint32_t gCases = 0;

static void Check(bool condition, const char* name) {
    ++gAssertions;
    if (!condition) {
        ++gFailures;
        printf("FAIL: %s\n", name);
    }
}

static void RunCase(const char* name, void (*test)()) {
    uint32_t before = gFailures;
    test();
    ++gCases;
    printf("%s: %s\n", gFailures == before ? "PASS" : "FAIL", name);
}

static const uint64_t kEfiNotFound = UEFI_ERROR_BIT | 14ULL;
static const uint64_t kEfiDeviceError = UEFI_ERROR_BIT | 7ULL;
static const uint64_t kEfiUnsupported = UEFI_ERROR_BIT | 3ULL;

struct FakePci {
    Location LocationValue;
    uint32_t Identity;
    uint32_t ClassRegister;
    uint32_t BarLow;
    uint32_t BarHigh;
    uint32_t FailConfigOffset;
    uint64_t ConfigFailureStatus;
    uint64_t OpenStatus;
    uint64_t LocationStatus;
    uint64_t BarStatus;
    BarResource Resource;
    uint32_t Registers[8];
    uint64_t FailMemOffset;
    uint64_t MemFailureStatus;
    uint32_t ConfigReadCount;
    uint32_t BarReadCount;
    uint32_t MemReadCount;
    uint64_t ReadOffsets[16];
};

struct FakeReporter {
    ProbeResult Results[MAX_CONTROLLERS_TO_PROBE + 1];
    uint32_t ResultCount;
    uint32_t SummaryCount;
    ProbeSummary Summary;
};

struct Fixture {
    FakePci Devices[300];
    uint64_t Count;
    FakeReporter Reporter;
};

static uint32_t XhciClassRegister() {
    return (0x0CU << 24) | (0x03U << 16) | (0x30U << 8);
}

static void SetValidRegisters(FakePci* pci, uint16_t version = 0x0110) {
    pci->Registers[0] = ((uint32_t)version << 16) | 0x20U;
    pci->Registers[1] = 32U | (8U << 8) | (4U << 24);
    pci->Registers[2] = (2U << 27) | (3U << 21);
    pci->Registers[3] = 0x00010002U;
    pci->Registers[4] = (1U << 2) | 1U;
    pci->Registers[5] = 0x100U;
    pci->Registers[6] = 0x200U;
    pci->Registers[7] = 0xAABBCCDDU;
}

static FakePci MakeValidPci(uint32_t ordinal = 0, bool is64Bit = false) {
    FakePci pci = {};
    pci.LocationValue.Segment = 0;
    pci.LocationValue.Bus = 0;
    pci.LocationValue.Device = 20 + ordinal;
    pci.LocationValue.Function = 0;
    pci.Identity = 0x12348086U;
    pci.ClassRegister = XhciClassRegister();
    uint64_t base = is64Bit ? 0x0000000180000000ULL : 0x80000000ULL;
    pci.BarLow = (uint32_t)(base & 0xFFFFFFF0ULL) | (is64Bit ? 4U : 0U);
    pci.BarHigh = (uint32_t)(base >> 32);
    pci.FailConfigOffset = UINT32_MAX;
    pci.FailMemOffset = UINT64_MAX;
    pci.MemFailureStatus = kEfiDeviceError;
    pci.Resource.DescriptorValid = true;
    pci.Resource.IsMemory = true;
    pci.Resource.AddressWidth = is64Bit ? 64 : 32;
    pci.Resource.Base = base;
    pci.Resource.Length = 0x1000;
    pci.Resource.Maximum = base + pci.Resource.Length - 1;
    pci.Resource.SupportedAttributes = 0x0200;
    SetValidRegisters(&pci);
    return pci;
}

static uint64_t FakeOpen(void* context, uint32_t index, ControllerOps* controller) {
    Fixture* fixture = (Fixture*)context;
    FakePci* pci = &fixture->Devices[index];
    if (IsErrorStatus(pci->OpenStatus)) return pci->OpenStatus;
    controller->Context = pci;
    controller->GetLocation = [](void* item, Location* location) -> uint64_t {
        FakePci* device = (FakePci*)item;
        if (IsErrorStatus(device->LocationStatus)) return device->LocationStatus;
        *location = device->LocationValue;
        return 0;
    };
    controller->ReadConfig32 = [](void* item, uint32_t offset,
                                  uint32_t* value) -> uint64_t {
        FakePci* device = (FakePci*)item;
        ++device->ConfigReadCount;
        if (offset == device->FailConfigOffset)
            return device->ConfigFailureStatus;
        switch (offset) {
        case 0: *value = device->Identity; break;
        case 8: *value = device->ClassRegister; break;
        case 0x10: *value = device->BarLow; break;
        case 0x14: *value = device->BarHigh; break;
        default: return kEfiUnsupported;
        }
        return 0;
    };
    controller->GetBar0 = [](void* item, BarResource* resource) -> uint64_t {
        FakePci* device = (FakePci*)item;
        ++device->BarReadCount;
        *resource = device->Resource;
        resource->HasGetBarAttributesStatus = true;
        resource->GetBarAttributesStatus = device->BarStatus;
        return device->BarStatus;
    };
    controller->ReadMem32 = [](void* item, uint64_t offset,
                               uint32_t* value) -> uint64_t {
        FakePci* device = (FakePci*)item;
        if (device->MemReadCount < sizeof(device->ReadOffsets) /
                                  sizeof(device->ReadOffsets[0]))
            device->ReadOffsets[device->MemReadCount] = offset;
        ++device->MemReadCount;
        if (offset == device->FailMemOffset) return device->MemFailureStatus;
        if ((offset & 3U) != 0 || offset > 0x1C) return kEfiUnsupported;
        *value = device->Registers[offset / 4];
        return 0;
    };
    return 0;
}

static void CaptureController(void* context, const ProbeResult* result) {
    FakeReporter* reporter = (FakeReporter*)context;
    if (reporter->ResultCount < MAX_CONTROLLERS_TO_PROBE + 1)
        reporter->Results[reporter->ResultCount++] = *result;
}

static void CaptureSummary(void* context, const ProbeSummary* summary) {
    FakeReporter* reporter = (FakeReporter*)context;
    reporter->Summary = *summary;
    ++reporter->SummaryCount;
}

static ProbeSummary RunFixture(Fixture* fixture) {
    PlatformOps platform = { fixture, fixture->Count, FakeOpen };
    ReporterOps reporter = {
        &fixture->Reporter, CaptureController, CaptureSummary
    };
    return RunProbe(platform, reporter);
}

static ProbeResult& OnlyResult(Fixture* fixture) {
    return fixture->Reporter.Results[0];
}

static void TestNoController() {
    Fixture fixture = {};
    fixture.Count = 2;
    fixture.Devices[0] = MakeValidPci();
    fixture.Devices[0].ClassRegister = 0x02000000U;
    fixture.Devices[1] = MakeValidPci(1);
    fixture.Devices[1].ClassRegister = 0x01000000U;
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 0, "no matching controller reported");
    Check(fixture.Reporter.ResultCount == 0, "no result emitted for non-xHCI functions");
    Check(summary.HandlesScanned == 2, "all bounded handles considered");
}

static void TestOneValidController() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 1 && summary.ControllersReported == 1,
          "one xHCI controller discovered and reported");
    Check(OnlyResult(&fixture).Stage == FailureStage::None,
          "valid controller completes");
    Check(OnlyResult(&fixture).BarUsable && OnlyResult(&fixture).HasCapabilities,
          "usable BAR and capability data returned");
}

static void TestMultipleControllers() {
    Fixture fixture = {};
    fixture.Count = 3;
    for (uint32_t i = 0; i < 3; ++i) fixture.Devices[i] = MakeValidPci(i);
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 3, "three controllers counted");
    Check(fixture.Reporter.ResultCount == 3, "three bounded controller records emitted");
    Check(fixture.Reporter.Results[2].PciLocation.Device == 22,
          "controller locations remain associated");
}

static void TestPciClassMismatch() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    fixture.Devices[0].ClassRegister = (0x0CU << 24) | (0x03U << 16) | (0x20U << 8);
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 0, "non-xHCI programming interface rejected");
    Check(fixture.Devices[0].BarReadCount == 0, "class mismatch does not query BAR");
    Check(fixture.Devices[0].MemReadCount == 0, "class mismatch does not read MMIO");
}

static void TestMissingBarResource() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    fixture.Devices[0].BarStatus = kEfiNotFound;
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 1, "controller remains reported after BAR failure");
    Check(OnlyResult(&fixture).Stage == FailureStage::GetBarAttributes,
          "missing resource reports GetBarAttributes stage");
    Check(OnlyResult(&fixture).FirmwareStatus == kEfiNotFound,
          "exact EFI_NOT_FOUND status preserved");
    Check(OnlyResult(&fixture).Bar.HasGetBarAttributesStatus &&
          OnlyResult(&fixture).Bar.GetBarAttributesStatus == kEfiNotFound,
          "GetBarAttributes status is available in the diagnostic record");
    Check(fixture.Devices[0].MemReadCount == 0, "no MMIO after missing resource");
}

static void TestMalformedResourceDescriptor() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    fixture.Devices[0].Resource.DescriptorValid = false;
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 1, "malformed descriptor controller reported");
    Check(OnlyResult(&fixture).Stage == FailureStage::MalformedBarDescriptor,
          "malformed descriptor rejected explicitly");
    Check(fixture.Devices[0].MemReadCount == 0, "malformed resource blocks MMIO");

    Fixture multiple = {};
    multiple.Count = 1;
    multiple.Devices[0] = MakeValidPci();
    multiple.Devices[0].Resource.DescriptorValid = false;
    multiple.Devices[0].Resource.HasQwordFields = true;
    multiple.Devices[0].Resource.HasListTail = true;
    multiple.Devices[0].Resource.ListTailByte0 = 0x8A;
    summary = RunFixture(&multiple);
    Check(summary.ControllersFound == 1 &&
          OnlyResult(&multiple).Stage == FailureStage::UnsupportedBarResourceList,
          "second QWORD descriptor is reported as a valid unsupported list shape");
    Check(multiple.Devices[0].MemReadCount == 0,
          "unsupported multi-range resource list blocks MMIO");
}

static void TestBar32And64Handling() {
    Fixture fixture = {};
    fixture.Count = 2;
    fixture.Devices[0] = MakeValidPci(0, false);
    fixture.Devices[0].Resource.SpecificFlags = 0x07;
    fixture.Devices[1] = MakeValidPci(1, true);
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 2, "32-bit and 64-bit BAR controllers found");
    Check(fixture.Reporter.Results[0].BarConfigurationWidth == 32,
          "32-bit BAR width decoded");
    Check(fixture.Reporter.Results[1].BarConfigurationWidth == 64,
          "64-bit BAR width decoded");
    Check(fixture.Reporter.Results[1].BarConfigurationBase == 0x180000000ULL,
          "upper BAR dword included in base");
    Check(fixture.Reporter.Results[0].Stage == FailureStage::None &&
          fixture.Reporter.Results[0].Bar.SpecificFlags == 0x07,
          "prefetchable cacheability flags pass without changing BAR validation");
    Check(fixture.Devices[1].ConfigReadCount == 4,
          "64-bit BAR reads BAR1 config dword while 32-bit does not");
}

static void TestResourceArithmeticOverflow() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci(0, true);
    fixture.Devices[0].Resource.Base = 0xFFFFFFFFFFFFFFF0ULL;
    fixture.Devices[0].Resource.Length = 0x40;
    fixture.Devices[0].Resource.Maximum = 0x2FULL;
    fixture.Devices[0].Resource.AddressWidth = 64;
    fixture.Devices[0].BarHigh = 0xFFFFFFFFU;
    fixture.Devices[0].BarLow = 0xFFFFFFF4U;
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 1, "overflowing resource controller reported");
    Check(OnlyResult(&fixture).Stage == FailureStage::InvalidBarResource,
          "overflowing base plus length rejected");
    Check(fixture.Devices[0].MemReadCount == 0, "overflow blocks MMIO");
}

static void TestInsufficientAperture() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    fixture.Devices[0].Resource.Length = 0x1F;
    fixture.Devices[0].Resource.Maximum =
        fixture.Devices[0].Resource.Base + 0x1E;
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 1, "small aperture controller reported");
    Check(OnlyResult(&fixture).Stage == FailureStage::InvalidBarResource,
          "aperture smaller than capability block rejected");
    Check(fixture.Devices[0].MemReadCount == 0, "small aperture causes no reads");
}

static void TestFailedMemRead() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    fixture.Devices[0].FailMemOffset = 0x0C;
    fixture.Devices[0].MemFailureStatus = kEfiDeviceError;
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 1, "read-failing controller reported");
    Check(OnlyResult(&fixture).Stage == FailureStage::ReadCapabilities,
          "failed Mem.Read stage reported");
    Check(OnlyResult(&fixture).FirmwareStatus == kEfiDeviceError,
          "exact Mem.Read EFI status preserved");
    Check(fixture.Devices[0].MemReadCount == 4,
          "read sequence stops at first failed dword");
}

static void TestValidCapabilityDecoding() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    ProbeSummary summary = RunFixture(&fixture);
    Capabilities caps = OnlyResult(&fixture).Capability;
    Check(summary.ControllersFound == 1 && OnlyResult(&fixture).HasCapabilities,
          "valid capability block accepted");
    Check(caps.ControllerVersion == 0x0110 && caps.CapabilityLength == 0x20,
          "version and CAPLENGTH decoded");
    Check(caps.MaximumDeviceSlots == 32 && caps.RootPortCount == 4 &&
          caps.AdvertisedInterrupters == 8, "controller limits decoded");
    Check(caps.HasHccParams2 && caps.HccParams2 == 0xAABBCCDDU,
          "HCCPARAMS2 read for xHCI 1.1");
    Check(fixture.Devices[0].ReadOffsets[7] == HCCPARAMS2_OFFSET,
          "optional HCCPARAMS2 access stays at 0x1C");

    Fixture older = {};
    older.Count = 1;
    older.Devices[0] = MakeValidPci();
    SetValidRegisters(&older.Devices[0], 0x0100);
    summary = RunFixture(&older);
    Check(summary.ControllersFound == 1 && older.Reporter.Results[0].HasCapabilities,
          "xHCI 1.0 capability block accepted");
    Check(!older.Reporter.Results[0].Capability.HasHccParams2 &&
          older.Devices[0].MemReadCount == 7,
          "HCCPARAMS2 is not read for pre-1.1 controllers");
}

static void TestMalformedCapabilityHeader() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    fixture.Devices[0].Registers[0] = 0x01100000U;
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 1, "malformed header controller reported");
    Check(OnlyResult(&fixture).Stage == FailureStage::MalformedCapabilityHeader,
          "zero CAPLENGTH rejected");
    Check(fixture.Devices[0].MemReadCount == 1,
          "malformed initial header stops subsequent reads");
}

static void TestScratchpadDecoding() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 1, "scratchpad controller found");
    Check(OnlyResult(&fixture).Capability.ScratchpadBufferCount == 67,
          "Hi and Lo scratchpad fields combined");
    Check(OnlyResult(&fixture).Capability.Uses64ByteContexts,
          "context-size capability decoded");
    Check(OnlyResult(&fixture).Capability.Supports64BitAddresses,
          "64-bit addressing capability decoded");
}

static void TestUnsupportedCapabilityOffset() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    fixture.Devices[0].Registers[5] = 0xFFFFFFFCU;
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 1, "invalid offset controller reported");
    Check(OnlyResult(&fixture).Stage == FailureStage::InvalidCapabilities,
          "doorbell extent beyond BAR rejected");
    Check(fixture.Devices[0].MemReadCount == 8,
          "no register outside capability header read");
}

static void TestReadOnlyEnforcement() {
    Fixture fixture = {};
    fixture.Count = 1;
    fixture.Devices[0] = MakeValidPci();
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 1, "read-only test controller found");
    Check(fixture.Devices[0].ConfigReadCount == 3,
          "only identity, class, and BAR0 PCI config reads used");
    Check(fixture.Devices[0].MemReadCount == 8,
          "only fixed capability dwords read");
    Check(fixture.Devices[0].BarReadCount == 1,
          "firmware resource query occurs once");
}

static void WriteLe64(uint8_t* bytes, uint64_t value) {
    for (uint32_t index = 0; index < 8; ++index)
        bytes[index] = (uint8_t)(value >> (index * 8));
}

struct FakeDescriptorLease {
    uint8_t Bytes[48];
    bool Allocated;
    uint32_t AcquireCount;
    uint32_t ReleaseCount;
    uint64_t GetStatus;
    uint64_t ReleaseStatus;
    uint64_t Attributes;
};

static void FillDescriptor(FakeDescriptorLease* lease, uint64_t base,
                           uint64_t length, uint64_t width) {
    lease->Bytes[0] = 0x8A;
    lease->Bytes[1] = 0x2B;
    lease->Bytes[2] = 0;
    lease->Bytes[3] = 0;
    lease->Bytes[4] = 0;
    lease->Bytes[5] = 0;
    WriteLe64(lease->Bytes + 6, width);
    WriteLe64(lease->Bytes + 14, base);
    WriteLe64(lease->Bytes + 22, base + length - 1);
    WriteLe64(lease->Bytes + 30, 0);
    WriteLe64(lease->Bytes + 38, length);
    lease->Bytes[46] = 0x79;
    lease->Bytes[47] = 0;
    lease->Allocated = true;
    lease->Attributes = 0x0200;
}

static uint64_t AcquireDescriptor(void* context, uint64_t* attributes,
                                  const uint8_t** bytes) {
    FakeDescriptorLease* lease = (FakeDescriptorLease*)context;
    ++lease->AcquireCount;
    *attributes = lease->Attributes;
    *bytes = lease->Allocated ? lease->Bytes : nullptr;
    return lease->GetStatus;
}

static uint64_t MockReleaseDescriptor(void* context, const uint8_t* bytes) {
    FakeDescriptorLease* lease = (FakeDescriptorLease*)context;
    Check(bytes == lease->Bytes, "descriptor release receives original allocation");
    ++lease->ReleaseCount;
    lease->Allocated = false;
    return lease->ReleaseStatus;
}

static void TestDescriptorCleanup() {
    FakeDescriptorLease lease = {};
    FillDescriptor(&lease, 0x80000000ULL, 0x1000, 32);
    BarResource resource = {};
    uint64_t status = TryGetBarResource(&lease, AcquireDescriptor,
                                        MockReleaseDescriptor, &resource);
    Check(!IsErrorStatus(status), "well-formed descriptor retrieval succeeds");
    Check(lease.AcquireCount == 1 && lease.ReleaseCount == 1 && !lease.Allocated,
          "firmware descriptor allocation is released exactly once");
    Check(resource.DescriptorValid && resource.Base == 0x80000000ULL,
          "descriptor copied before firmware buffer release");
    Check(resource.HasGetBarAttributesStatus &&
          resource.GetBarAttributesStatus == 0 &&
          resource.HasDescriptorHeader && resource.DescriptorType == 0x8A &&
          resource.DescriptorLength == 0x2B,
          "firmware query status and bounded descriptor header retained");
    Check(resource.HasQwordFields && resource.ResourceType == 0 &&
          resource.RawBase == 0x80000000ULL && resource.RawLength == 0x1000 &&
          resource.HasListTail && resource.ListTailByte0 == 0x79 &&
          resource.ListTailByte1 == 0,
          "raw QWORD fields and termination copied before firmware buffer release");

    FakeDescriptorLease malformed = {};
    FillDescriptor(&malformed, 0x80000000ULL, 0x1000, 32);
    malformed.Bytes[0] = 0x00;
    status = TryGetBarResource(&malformed, AcquireDescriptor,
                               MockReleaseDescriptor, &resource);
    Check(!IsErrorStatus(status) && !resource.DescriptorValid,
          "malformed descriptor is rejected by the shared parser");
    Check(resource.HasGetBarAttributesStatus &&
          resource.GetBarAttributesStatus == 0 &&
          resource.HasDescriptorHeader && resource.DescriptorType == 0,
          "malformed descriptor retains successful query status and raw type");
    Check(malformed.ReleaseCount == 1 && !malformed.Allocated,
          "malformed descriptor allocation is still released");

    FakeDescriptorLease unterminated = {};
    FillDescriptor(&unterminated, 0x80000000ULL, 0x1000, 32);
    unterminated.Bytes[46] = 0;
    status = TryGetBarResource(&unterminated, AcquireDescriptor,
                               MockReleaseDescriptor, &resource);
    Check(!IsErrorStatus(status) && !resource.DescriptorValid &&
          resource.HasListTail && resource.ListTailByte0 == 0,
          "unterminated descriptor is rejected with raw terminator retained");
    Check(unterminated.ReleaseCount == 1 && !unterminated.Allocated,
          "unterminated resource allocation is still released");

    FakeDescriptorLease failed = {};
    FillDescriptor(&failed, 0x80000000ULL, 0x1000, 32);
    failed.GetStatus = kEfiNotFound;
    status = TryGetBarResource(&failed, AcquireDescriptor,
                               MockReleaseDescriptor, &resource);
    Check(status == kEfiNotFound, "failed GetBarAttributes status is preserved");
    Check(resource.HasGetBarAttributesStatus &&
          resource.GetBarAttributesStatus == kEfiNotFound,
          "failed query EFI status is retained for diagnostics");
    Check(failed.ReleaseCount == 1 && !failed.Allocated,
          "returned allocation is released even when firmware reports failure");
}

static void TestContinueAfterControllerFailure() {
    Fixture fixture = {};
    fixture.Count = 3;
    fixture.Devices[0] = MakeValidPci(0);
    fixture.Devices[0].FailMemOffset = 0x04;
    fixture.Devices[1] = MakeValidPci(1);
    fixture.Devices[2] = MakeValidPci(2);
    fixture.Devices[2].OpenStatus = kEfiNotFound;
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == 2 && fixture.Reporter.ResultCount == 2,
          "both controllers reported");
    Check(fixture.Reporter.Results[0].Stage == FailureStage::ReadCapabilities,
          "first controller reports its failed read");
    Check(fixture.Reporter.Results[1].Stage == FailureStage::None,
          "second controller succeeds after first fails");
    Check(summary.EnumerationErrors == 1 &&
          summary.FirstEnumerationErrorStage == FailureStage::OpenProtocol &&
          summary.FirstEnumerationErrorStatus == kEfiNotFound,
          "firmware enumeration error stage and status retained");
}

static void TestBoundedIterationAndOutput() {
    Fixture fixture = {};
    fixture.Count = 10;
    for (uint32_t i = 0; i < 10; ++i) fixture.Devices[i] = MakeValidPci(i);
    ProbeSummary summary = RunFixture(&fixture);
    Check(summary.ControllersFound == MAX_CONTROLLERS_TO_PROBE,
          "controller probes stop at configured maximum");
    Check(summary.ControllersReported == MAX_CONTROLLERS_TO_PROBE &&
          fixture.Reporter.ResultCount == MAX_CONTROLLERS_TO_PROBE,
          "reported output remains capped at eight controllers");
    Check(summary.Truncated && summary.ControllerLimitReached &&
          summary.HandlesScanned == MAX_CONTROLLERS_TO_PROBE + 1,
          "ninth matching controller marks a bounded scan");

    Fixture handles = {};
    handles.Count = 300;
    for (uint32_t i = 0; i < 300; ++i) {
        handles.Devices[i] = MakeValidPci(i % 12);
        handles.Devices[i].ClassRegister = 0x02000000U;
    }
    summary = RunFixture(&handles);
    Check(summary.HandlesScanned == MAX_HANDLES_TO_SCAN && summary.Truncated,
          "PCI handle iteration stops at 256");
    Check(handles.Reporter.ResultCount == 0,
          "nonmatching handle output remains suppressed");
}

int main() {
    RunCase("no xHCI controller", TestNoController);
    RunCase("one valid controller", TestOneValidController);
    RunCase("multiple controllers", TestMultipleControllers);
    RunCase("PCI class mismatch", TestPciClassMismatch);
    RunCase("missing BAR0 resource", TestMissingBarResource);
    RunCase("malformed resource descriptor", TestMalformedResourceDescriptor);
    RunCase("32-bit and 64-bit BAR handling", TestBar32And64Handling);
    RunCase("resource arithmetic overflow", TestResourceArithmeticOverflow);
    RunCase("insufficient aperture", TestInsufficientAperture);
    RunCase("failed Mem.Read", TestFailedMemRead);
    RunCase("valid capability decoding", TestValidCapabilityDecoding);
    RunCase("malformed capability header", TestMalformedCapabilityHeader);
    RunCase("scratchpad and capability bits", TestScratchpadDecoding);
    RunCase("unsupported capability offset", TestUnsupportedCapabilityOffset);
    RunCase("read-only enforcement", TestReadOnlyEnforcement);
    RunCase("descriptor cleanup", TestDescriptorCleanup);
    RunCase("safe continuation after failure", TestContinueAfterControllerFailure);
    RunCase("bounded iteration and output", TestBoundedIterationAndOutput);
    printf("Hosted UEFI xHCI probe: %u cases, %u assertions, %u failures\n",
           gCases, gAssertions, gFailures);
    return gFailures == 0 ? 0 : 1;
}
