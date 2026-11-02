#pragma once

#include <stdint.h>

namespace guidexos {
namespace finalizerstack {

constexpr uint64_t kDescriptorMagic = 0x31534653584F4758ULL;
constexpr uint64_t kDescriptorVersion = 1;
constexpr uint64_t kKindFinalizer = 2;

enum DescriptorFlag : uint64_t {
    kHostedOsBacking = 1ULL << 0,
    kDedicatedFiberStack = 1ULL << 1,
    kAccessiblePagesVerified = 1ULL << 2,
    kReleaseAfterWorkerExit = 1ULL << 3,
};

constexpr uint64_t kRequiredHostedFlags =
    kHostedOsBacking | kDedicatedFiberStack |
    kAccessiblePagesVerified | kReleaseAfterWorkerExit;

// Hosted qualification record. The OS fiber owns the backing; the owner
// thread must retire the fiber only after it has returned to its root stack.
// This record does not claim a guideXOS physical-page or UEFI mapping proof.
struct Descriptor {
    uint64_t Magic;
    uint64_t Version;
    uint64_t Kind;
    uint64_t AllocationLow;
    uint64_t AllocationHigh;
    uint64_t AccessibleLow;
    uint64_t AccessibleHigh;
    uint64_t MappingIdentity;
    uint64_t OwnerIdentity;
    uint64_t Generation;
    uint64_t Flags;
};

static_assert(sizeof(Descriptor) == 88, "finalizer stack descriptor ABI changed");

inline bool Validate(const Descriptor& value, uint64_t expectedOwner,
                     uint64_t expectedGeneration, uint64_t activeStackPointer,
                     uint64_t pageSize) {
    if (value.Magic != kDescriptorMagic ||
        value.Version != kDescriptorVersion ||
        value.Kind != kKindFinalizer || pageSize == 0 ||
        (pageSize & (pageSize - 1)) != 0) {
        return false;
    }
    if (value.AllocationLow == 0 || value.AllocationHigh <= value.AllocationLow ||
        value.AccessibleLow < value.AllocationLow ||
        value.AccessibleHigh > value.AllocationHigh ||
        value.AccessibleHigh <= value.AccessibleLow ||
        value.MappingIdentity != value.AllocationLow ||
        (value.AllocationLow & (pageSize - 1)) != 0 ||
        (value.AllocationHigh & (pageSize - 1)) != 0 ||
        (value.AccessibleLow & (pageSize - 1)) != 0 ||
        (value.AccessibleHigh & (pageSize - 1)) != 0) {
        return false;
    }
    if ((value.Flags & kRequiredHostedFlags) != kRequiredHostedFlags ||
        value.OwnerIdentity == 0 || value.OwnerIdentity != expectedOwner ||
        value.Generation == 0 || value.Generation != expectedGeneration) {
        return false;
    }
    return activeStackPointer >= value.AccessibleLow &&
           activeStackPointer < value.AccessibleHigh;
}

} // namespace finalizerstack
} // namespace guidexos
