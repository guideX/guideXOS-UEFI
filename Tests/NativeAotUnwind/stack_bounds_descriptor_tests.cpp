#include "../../Tools/Phase23/stack_bounds_contract.h"

#include <array>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <map>
#include <thread>
#include <vector>

using guidexos::stackbounds::Descriptor;
using guidexos::stackbounds::Failure;
using guidexos::stackbounds::MemoryMapView;

extern "C" bool __stdcall guidexos_pal_register_bootstrap_stack(
    const Descriptor* descriptor, const void* memoryMap,
    uint64_t memoryMapEntryCount, uint64_t memoryMapDescriptorSize,
    uint64_t activePageTableRoot);
extern "C" bool __stdcall PalGetMaximumStackBounds(
    void** ppStackLowOut, void** ppStackHighOut);

namespace {

int g_passed = 0;
int g_failed = 0;

#define CHECK(name, expression) \
    do { \
        if (expression) { \
            ++g_passed; \
            std::cout << "PASS " << name << '\n'; \
        } else { \
            ++g_failed; \
            std::cout << "FAIL " << name << '\n'; \
        } \
    } while (0)

struct EfiMemoryDescriptorFixture {
    uint32_t Type;
    uint32_t Padding;
    uint64_t PhysicalStart;
    uint64_t VirtualStart;
    uint64_t NumberOfPages;
    uint64_t Attribute;
};
static_assert(sizeof(EfiMemoryDescriptorFixture) == 40,
              "EFI descriptor fixture layout mismatch");

struct MemoryMapFixture {
    EfiMemoryDescriptorFixture Entry{};

    MemoryMapView View() const {
        return {reinterpret_cast<const uint8_t*>(&Entry), 1,
                sizeof(Entry)};
    }

    void Set(uint32_t type, uint64_t low, uint64_t pages) {
        Entry = {type, 0, low, 0, pages, 0};
    }
};

Descriptor MakeDescriptor(uint64_t low, uint64_t high,
                          uint64_t pageTableRoot = 0x12345000) {
    return {
        guidexos::stackbounds::kDescriptorMagic,
        guidexos::stackbounds::kDescriptorVersion,
        guidexos::stackbounds::kKindBootstrap,
        low,
        high,
        low,
        high,
        pageTableRoot,
        guidexos::stackbounds::kBootstrapOwnerIdentity,
        guidexos::stackbounds::kBootstrapGeneration,
        guidexos::stackbounds::kRequiredBootstrapFlags
    };
}

bool Validate(const Descriptor& descriptor, const MemoryMapFixture& map,
              uint64_t currentRoot = 0x12345000,
              uint64_t stackAddress = 0x3000,
              uint64_t owner = guidexos::stackbounds::kBootstrapOwnerIdentity,
              uint64_t generation = guidexos::stackbounds::kBootstrapGeneration) {
    return guidexos::stackbounds::Validate(
        &descriptor, map.View(), currentRoot, stackAddress, owner, generation)
        == Failure::None;
}

struct FakePageTables {
    std::map<uint64_t, uint64_t> Entries;

    static bool Read(void* context, uint64_t address, uint64_t* outValue) {
        auto* self = static_cast<FakePageTables*>(context);
        const auto found = self->Entries.find(address);
        if (found == self->Entries.end() || outValue == nullptr) return false;
        *outValue = found->second;
        return true;
    }

    void SetEntry(uint64_t table, uint64_t index, uint64_t value) {
        Entries[table + index * sizeof(uint64_t)] = value;
    }

    void MapTwoIdentityPages(uint64_t address) {
        (void)MapIdentityRange(address, address + 0x2000);
    }

    bool MapIdentityRange(uint64_t low, uint64_t high) {
        if (low == 0 || high <= low || (low & 0xFFF) != 0 ||
            (high & 0xFFF) != 0)
            return false;
        constexpr uint64_t kPresentWritable = 0x3;
        constexpr uint64_t kAddressMask = 0x000FFFFFFFFFF000ULL;
        const uint64_t root = 0x1000;
        const uint64_t pdpt = 0x2000;
        const uint64_t pd = 0x3000;
        const uint64_t firstPml4Index = (low >> 39) & 0x1FF;
        const uint64_t firstPdptIndex = (low >> 30) & 0x1FF;
        for (uint64_t address = low; address < high; address += 0x1000) {
            const uint64_t pml4Index = (address >> 39) & 0x1FF;
            const uint64_t pdptIndex = (address >> 30) & 0x1FF;
            if (pml4Index != firstPml4Index ||
                pdptIndex != firstPdptIndex)
                return false;
            const uint64_t pdIndex = (address >> 21) & 0x1FF;
            const uint64_t ptIndex = (address >> 12) & 0x1FF;
            const uint64_t pt = 0x4000 + pdIndex * 0x1000;
            SetEntry(root, pml4Index, pdpt | kPresentWritable);
            SetEntry(pdpt, pdptIndex, pd | kPresentWritable);
            SetEntry(pd, pdIndex, pt | kPresentWritable);
            SetEntry(pt, ptIndex,
                     (address & kAddressMask) | kPresentWritable);
        }
        return true;
    }
};

} // namespace

int main() {
    MemoryMapFixture map;
    map.Set(2, 0x1000, 5);
    Descriptor valid = MakeDescriptor(0x1000, 0x6000);

    CHECK("valid descriptor and loader-data allocation",
          Validate(valid, map));

    Descriptor built{};
    CHECK("production descriptor builder records exact bootstrap allocation",
          guidexos::stackbounds::CreateBootstrapDescriptorAfterPageWalk(
              &built, 0x1000, 0x5000, 0x12345000) &&
          built.AllocationLow == 0x1000 &&
          built.AllocationHigh == 0x6000 &&
          built.AccessibleLow == 0x1000 && built.AccessibleHigh == 0x6000 &&
          built.Flags == guidexos::stackbounds::kRequiredBootstrapFlags);
    Descriptor rejectedBuild = valid;
    CHECK("production descriptor builder clears and rejects overflow",
          !guidexos::stackbounds::CreateBootstrapDescriptorAfterPageWalk(
              &rejectedBuild, UINT64_MAX - 0xFFF, 0x2000, 0x12345000) &&
          rejectedBuild.Magic == 0 && rejectedBuild.Flags == 0);

    Descriptor empty = valid;
    empty.AccessibleHigh = empty.AccessibleLow;
    CHECK("zero-length accessible interval rejected",
          !Validate(empty, map));

    Descriptor reversed = valid;
    reversed.AllocationHigh = reversed.AllocationLow;
    CHECK("reversed allocation interval rejected",
          !Validate(reversed, map));

    Descriptor misaligned = valid;
    misaligned.AccessibleLow += 1;
    CHECK("misaligned accessible interval rejected",
          !Validate(misaligned, map));

    MemoryMapFixture overflowMap;
    overflowMap.Set(2, UINT64_MAX - 0xFFF, 2);
    CHECK("memory-map physical range overflow rejected",
          !Validate(valid, overflowMap));

    MemoryMapFixture missingMap;
    missingMap.Set(7, 0x1000, 5);
    CHECK("missing EfiLoaderData backing rejected",
          !Validate(valid, missingMap));

    Descriptor guarded = MakeDescriptor(0x1000, 0x6000);
    guarded.AccessibleLow = 0x2000;
    guarded.AccessibleHigh = 0x5000;
    guarded.Flags |= guidexos::stackbounds::kGuardPagesExcluded;
    CHECK("guard pages excluded from accessible interval",
          Validate(guarded, map, 0x12345000, 0x3000));

    Descriptor guardClaimMissing = guarded;
    guardClaimMissing.Flags &=
        ~guidexos::stackbounds::kGuardPagesExcluded;
    CHECK("accessible subrange without guard exclusion rejected",
          !Validate(guardClaimMissing, map, 0x12345000, 0x3000));

    Descriptor noWrite = valid;
    noWrite.Flags &= ~guidexos::stackbounds::kMappingWritable;
    CHECK("non-writable mapping rejected", !Validate(noWrite, map));

    Descriptor noPageWalk = valid;
    noPageWalk.Flags &= ~guidexos::stackbounds::kPageWalkVerified;
    CHECK("unverified page mapping rejected", !Validate(noPageWalk, map));

    CHECK("wrong page-table identity rejected",
          !Validate(valid, map, 0x54321000));
    CHECK("invalid thread owner identity rejected",
          !Validate(valid, map, 0x12345000, 0x3000,
                    guidexos::stackbounds::kBootstrapOwnerIdentity + 1));
    CHECK("stale descriptor generation rejected",
          !Validate(valid, map, 0x12345000, 0x3000,
                    guidexos::stackbounds::kBootstrapOwnerIdentity, 2));
    CHECK("active stack outside interval rejected",
          !Validate(valid, map, 0x12345000, valid.AccessibleHigh));

    FakePageTables tables;
    tables.MapTwoIdentityPages(0x12345000);
    CHECK("simulated present writable identity pages accepted",
          guidexos::stackbounds::IsIdentityWritableRange(
              0x1000, 0x12345000, 0x12347000,
              FakePageTables::Read, &tables));
    const uint64_t pdIndex = (0x12345000ULL >> 21) & 0x1FF;
    const uint64_t ptTable = 0x4000 + pdIndex * 0x1000;
    const uint64_t ptIndex = (0x12345000ULL >> 12) & 0x1FF;
    tables.Entries[ptTable + ptIndex * 8] = 0x12345000 | 1;
    CHECK("simulated read-only page rejected",
          !guidexos::stackbounds::IsIdentityWritablePage(
              0x1000, 0x12345000, FakePageTables::Read, &tables));
    tables.Entries[ptTable + ptIndex * 8] = 0x12345000 | 3;
    // Change the second PTE to read-only, then verify the full interval fails.
    tables.Entries[ptTable + (ptIndex + 1) * 8] = 0x12346000 | 1;
    CHECK("simulated inaccessible second page rejected",
          !guidexos::stackbounds::IsIdentityWritableRange(
              0x1000, 0x12345000, 0x12347000,
              FakePageTables::Read, &tables));

    void* initialLow = reinterpret_cast<void*>(1);
    void* initialHigh = reinterpret_cast<void*>(1);
    CHECK("PAL rejects before descriptor registration and clears outputs",
          !PalGetMaximumStackBounds(&initialLow, &initialHigh) &&
          initialLow == nullptr && initialHigh == nullptr);
    initialHigh = reinterpret_cast<void*>(1);
    CHECK("PAL clears a valid output when its peer pointer is null",
          !PalGetMaximumStackBounds(nullptr, &initialHigh) &&
          initialHigh == nullptr);

    uintptr_t stackAnchor = reinterpret_cast<uintptr_t>(&valid);
    const uint64_t allocationLow =
        (static_cast<uint64_t>(stackAnchor) - 0x10000) & ~0xFFFULL;
    const uint64_t allocationBytes = 0x20000;
    const uint64_t allocationHigh = allocationLow + allocationBytes;
    Descriptor hostedFixture{};
    FakePageTables hostedTables;
    const bool hostedPagesPresent =
        hostedTables.MapIdentityRange(allocationLow, allocationHigh);
    const bool hostedPageWalkVerified = hostedPagesPresent &&
        guidexos::stackbounds::IsIdentityWritableRange(
            0x1000, allocationLow, allocationHigh,
            FakePageTables::Read, &hostedTables);
    CHECK("simulated handoff stack pages are present before descriptor creation",
          hostedPageWalkVerified);
    CHECK("production descriptor-to-PAL fixture uses loader descriptor builder",
          hostedPageWalkVerified &&
          guidexos::stackbounds::CreateBootstrapDescriptorAfterPageWalk(
              &hostedFixture, allocationLow, allocationBytes, 0x1000));
    MemoryMapFixture hostedMap;
    hostedMap.Set(2, allocationLow,
                  (allocationHigh - allocationLow) / 0x1000);

    Descriptor badHandoff = hostedFixture;
    badHandoff.Generation++;
    CHECK("PAL rejects stale handoff without publishing it",
          !guidexos_pal_register_bootstrap_stack(
              &badHandoff, &hostedMap.Entry, 1, sizeof(hostedMap.Entry),
              hostedFixture.MappingIdentity));
    CHECK("PAL outputs remain clear after invalid handoff",
          !PalGetMaximumStackBounds(&initialLow, &initialHigh) &&
          initialLow == nullptr && initialHigh == nullptr);

    CHECK("production descriptor-to-PAL registration succeeds",
          guidexos_pal_register_bootstrap_stack(
              &hostedFixture, &hostedMap.Entry, 1, sizeof(hostedMap.Entry),
              hostedFixture.MappingIdentity));

    void* palLow = nullptr;
    void* palHigh = nullptr;
    const bool palSuccess =
        PalGetMaximumStackBounds(&palLow, &palHigh);
    CHECK("production PAL returns exact validated interval",
          palSuccess &&
          reinterpret_cast<uintptr_t>(palLow) == hostedFixture.AccessibleLow &&
          reinterpret_cast<uintptr_t>(palHigh) == hostedFixture.AccessibleHigh);

    Descriptor conflicting = hostedFixture;
    conflicting.MappingIdentity += 0x1000;
    CHECK("PAL rejects descriptor replacement after publication",
          !guidexos_pal_register_bootstrap_stack(
              &conflicting, &hostedMap.Entry, 1, sizeof(hostedMap.Entry),
              conflicting.MappingIdentity));

    bool workerResult = true;
    void* workerLow = reinterpret_cast<void*>(1);
    void* workerHigh = reinterpret_cast<void*>(1);
    std::thread worker([&]() {
        workerResult = PalGetMaximumStackBounds(&workerLow, &workerHigh);
    });
    worker.join();
    CHECK("foreign host thread cannot receive bootstrap bounds",
          !workerResult && workerLow == nullptr && workerHigh == nullptr);

    CHECK("bootstrap descriptor-to-PAL contract reports bounded only",
          palSuccess && g_failed == 0);
    std::cout << "RESULT passed=" << g_passed << " failed=" << g_failed
              << " mapping=simulated physicalMapping=notTested\n";
    return g_failed == 0 ? 0 : 1;
}
