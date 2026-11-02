#pragma once

#include <atomic>
#include <stddef.h>
#include <stdint.h>

namespace guidexos {
namespace executioncontext {

constexpr uint32_t kUnknownCpu = UINT32_MAX;
constexpr uint64_t kBootstrapContextId = 0x4758454354580001ULL;

enum StackEvidence : uint64_t {
    kExactAllocation = 1ULL << 0,
    kEfiBackingVerified = 1ULL << 1,
    kMappingVerified = 1ULL << 2,
    kSupervisorWritable = 1ULL << 3,
    kLifetimeManaged = 1ULL << 4,
    kPermanentKernelPin = 1ULL << 5,
};

constexpr uint64_t kRequiredStackEvidence =
    kExactAllocation | kEfiBackingVerified | kMappingVerified |
    kSupervisorWritable | kLifetimeManaged;

struct StackDescriptor {
    uint64_t Identity;
    uint64_t Generation;
    uint64_t AllocationLow;
    uint64_t AllocationHigh;
    uint64_t AccessibleLow;
    uint64_t AccessibleHigh;
    uint64_t Evidence;
};

inline bool IsValidStackDescriptor(const StackDescriptor& descriptor,
                                   bool requirePermanentPin = false) {
    if (descriptor.Identity == 0 || descriptor.Generation == 0 ||
        descriptor.AllocationLow == 0 ||
        descriptor.AllocationHigh <= descriptor.AllocationLow ||
        descriptor.AccessibleLow < descriptor.AllocationLow ||
        descriptor.AccessibleHigh > descriptor.AllocationHigh ||
        descriptor.AccessibleHigh <= descriptor.AccessibleLow ||
        (descriptor.Evidence & kRequiredStackEvidence) !=
            kRequiredStackEvidence) {
        return false;
    }
    return !requirePermanentPin ||
        (descriptor.Evidence & kPermanentKernelPin) != 0;
}

struct StackHandle {
    uint64_t Identity;
    uint64_t Generation;
};

enum class RegistryResult : uint32_t {
    Success,
    AlreadyRegistered,
    InvalidArgument,
    MissingProvenance,
    StaleHandle,
    Busy,
    Unsupported,
    InvalidState,
    PendingReclamation,
    Reclaimable,
};

enum class StackState : uint32_t {
    Empty,
    Active,
    Retiring,
    Retired,
};

template <size_t Capacity>
class StackRegistry {
public:
    StackRegistry() = default;
    StackRegistry(const StackRegistry&) = delete;
    StackRegistry& operator=(const StackRegistry&) = delete;

    RegistryResult Register(const StackDescriptor& descriptor,
                            bool permanentPin) {
        if (descriptor.Identity == 0 || descriptor.Generation == 0)
            return RegistryResult::InvalidArgument;
        if (!IsValidStackDescriptor(descriptor, permanentPin))
            return RegistryResult::MissingProvenance;

        Lock();
        Slot* existing = FindByIdentity(descriptor.Identity);
        if (existing != nullptr) {
            if (existing->Descriptor.Generation == descriptor.Generation) {
                const bool same = SameDescriptor(existing->Descriptor,
                                                 descriptor);
                const bool active = existing->State == StackState::Active;
                Unlock();
                return same && active ? RegistryResult::AlreadyRegistered
                                      : RegistryResult::InvalidState;
            }
            if (descriptor.Generation < existing->Descriptor.Generation) {
                Unlock();
                return RegistryResult::StaleHandle;
            }
            if (existing->State != StackState::Retired ||
                existing->Pins != 0 || existing->PermanentPin) {
                Unlock();
                return RegistryResult::Busy;
            }
            InitializeSlot(*existing, descriptor, permanentPin);
            Unlock();
            return RegistryResult::Success;
        }

        Slot* freeSlot = FindReusableSlot();
        if (freeSlot == nullptr) {
            Unlock();
            return RegistryResult::Busy;
        }
        InitializeSlot(*freeSlot, descriptor, permanentPin);
        Unlock();
        return RegistryResult::Success;
    }

    RegistryResult Acquire(StackHandle handle) {
        if (handle.Identity == 0 || handle.Generation == 0)
            return RegistryResult::InvalidArgument;
        Lock();
        Slot* slot = FindByIdentity(handle.Identity);
        if (slot == nullptr || slot->Descriptor.Generation != handle.Generation) {
            Unlock();
            return RegistryResult::StaleHandle;
        }
        if (slot->State != StackState::Active) {
            Unlock();
            return RegistryResult::InvalidState;
        }
        if (slot->Pins == UINT32_MAX) {
            Unlock();
            return RegistryResult::Busy;
        }
        ++slot->Pins;
        Unlock();
        return RegistryResult::Success;
    }

    RegistryResult Release(StackHandle handle, bool* reclaimable = nullptr) {
        if (reclaimable != nullptr) *reclaimable = false;
        if (handle.Identity == 0 || handle.Generation == 0)
            return RegistryResult::InvalidArgument;
        Lock();
        Slot* slot = FindByIdentity(handle.Identity);
        if (slot == nullptr || slot->Descriptor.Generation != handle.Generation) {
            Unlock();
            return RegistryResult::StaleHandle;
        }
        const uint32_t permanent = slot->PermanentPin ? 1U : 0U;
        if (slot->Pins <= permanent) {
            Unlock();
            return RegistryResult::InvalidState;
        }
        --slot->Pins;
        const bool ready = slot->State == StackState::Retiring &&
            slot->Pins == 0;
        if (ready) slot->State = StackState::Retired;
        if (reclaimable != nullptr) *reclaimable = ready;
        Unlock();
        return ready ? RegistryResult::Reclaimable : RegistryResult::Success;
    }

    RegistryResult BeginRetirement(StackHandle handle,
                                   bool* reclaimable = nullptr) {
        if (reclaimable != nullptr) *reclaimable = false;
        if (handle.Identity == 0 || handle.Generation == 0)
            return RegistryResult::InvalidArgument;
        Lock();
        Slot* slot = FindByIdentity(handle.Identity);
        if (slot == nullptr || slot->Descriptor.Generation != handle.Generation) {
            Unlock();
            return RegistryResult::StaleHandle;
        }
        if (slot->PermanentPin) {
            Unlock();
            return RegistryResult::Unsupported;
        }
        if (slot->State != StackState::Active) {
            Unlock();
            return RegistryResult::InvalidState;
        }
        if (slot->Pins == 0) {
            slot->State = StackState::Retired;
            if (reclaimable != nullptr) *reclaimable = true;
            Unlock();
            return RegistryResult::Reclaimable;
        }
        slot->State = StackState::Retiring;
        Unlock();
        return RegistryResult::PendingReclamation;
    }

    RegistryResult GetDescriptor(StackHandle handle,
                                 StackDescriptor* descriptor) const {
        if (descriptor == nullptr || handle.Identity == 0 ||
            handle.Generation == 0) return RegistryResult::InvalidArgument;
        Lock();
        const Slot* slot = FindByIdentity(handle.Identity);
        if (slot == nullptr || slot->Descriptor.Generation != handle.Generation) {
            Unlock();
            return RegistryResult::StaleHandle;
        }
        if (slot->State != StackState::Active) {
            Unlock();
            return RegistryResult::InvalidState;
        }
        *descriptor = slot->Descriptor;
        Unlock();
        return RegistryResult::Success;
    }

    bool IsActive(StackHandle handle) const {
        StackDescriptor descriptor{};
        return GetDescriptor(handle, &descriptor) == RegistryResult::Success;
    }

    bool IsReclaimable(StackHandle handle) const {
        Lock();
        const Slot* slot = FindByIdentity(handle.Identity);
        const bool result = slot != nullptr &&
            slot->Descriptor.Generation == handle.Generation &&
            slot->State == StackState::Retired && slot->Pins == 0 &&
            !slot->PermanentPin;
        Unlock();
        return result;
    }

    uint32_t PinCount(StackHandle handle) const {
        Lock();
        const Slot* slot = FindByIdentity(handle.Identity);
        const uint32_t result = slot != nullptr &&
            slot->Descriptor.Generation == handle.Generation ? slot->Pins : 0U;
        Unlock();
        return result;
    }

private:
    struct Slot {
        StackDescriptor Descriptor{};
        StackState State = StackState::Empty;
        uint32_t Pins = 0;
        bool PermanentPin = false;
    };

    static bool SameDescriptor(const StackDescriptor& left,
                               const StackDescriptor& right) {
        return left.Identity == right.Identity &&
            left.Generation == right.Generation &&
            left.AllocationLow == right.AllocationLow &&
            left.AllocationHigh == right.AllocationHigh &&
            left.AccessibleLow == right.AccessibleLow &&
            left.AccessibleHigh == right.AccessibleHigh &&
            left.Evidence == right.Evidence;
    }

    void InitializeSlot(Slot& slot, const StackDescriptor& descriptor,
                        bool permanentPin) {
        slot.Descriptor = descriptor;
        slot.State = StackState::Active;
        slot.PermanentPin = permanentPin;
        slot.Pins = permanentPin ? 1U : 0U;
    }

    Slot* FindByIdentity(uint64_t identity) {
        for (size_t i = 0; i < Capacity; ++i) {
            if (_slots[i].State != StackState::Empty &&
                _slots[i].Descriptor.Identity == identity) return &_slots[i];
        }
        return nullptr;
    }

    const Slot* FindByIdentity(uint64_t identity) const {
        for (size_t i = 0; i < Capacity; ++i) {
            if (_slots[i].State != StackState::Empty &&
                _slots[i].Descriptor.Identity == identity) return &_slots[i];
        }
        return nullptr;
    }

    Slot* FindReusableSlot() {
        for (size_t i = 0; i < Capacity; ++i) {
            if (_slots[i].State == StackState::Empty ||
                (_slots[i].State == StackState::Retired &&
                 _slots[i].Pins == 0 && !_slots[i].PermanentPin)) {
                return &_slots[i];
            }
        }
        return nullptr;
    }

    void Lock() const {
        while (_lock.test_and_set(std::memory_order_acquire)) { }
    }

    void Unlock() const {
        _lock.clear(std::memory_order_release);
    }

    mutable std::atomic_flag _lock = ATOMIC_FLAG_INIT;
    Slot _slots[Capacity]{};
};

enum class ContextKind : uint32_t {
    KernelBootstrap,
    KernelScheduler,
    NativeAotThread,
};

enum class ContextLifecycle : uint32_t {
    Initializing,
    Runnable,
    Running,
    CooperativeSuspended,
    Interrupted,
    Terminating,
    Retired,
};

struct Identity {
    uint64_t Value;
    uint64_t Generation;
    uint32_t Cpu;
};

struct ContextRecord {
    Identity ExecutionIdentity;
    ContextKind Kind;
    ContextLifecycle Lifecycle;
    StackHandle Stack;
    uint64_t ThreadLocalState;
    bool NativeAotAttached;
};

template <size_t Capacity>
inline RegistryResult ValidateContextRecord(
        const ContextRecord& context,
        const StackRegistry<Capacity>& stackRegistry) {
    if (context.ExecutionIdentity.Value == 0 ||
        context.ExecutionIdentity.Generation == 0 ||
        context.ExecutionIdentity.Cpu == kUnknownCpu ||
        context.Stack.Identity == 0 || context.Stack.Generation == 0 ||
        context.Lifecycle == ContextLifecycle::Initializing ||
        context.Lifecycle == ContextLifecycle::Retired) {
        return RegistryResult::InvalidArgument;
    }
    return stackRegistry.IsActive(context.Stack) ? RegistryResult::Success
                                                  : RegistryResult::StaleHandle;
}

enum class CaptureKind : uint32_t {
    SchedulerInterrupt,
    CooperativeFullState,
    NativeAotFullState,
};

enum class CaptureResult : uint32_t {
    Success,
    SchedulerStateOnly,
    InvalidArgument,
    StaleContext,
    StackMismatch,
    MissingCpu,
    InvalidInstructionPointer,
    InvalidStackPointer,
    IncompleteRegisters,
    MissingExtendedState,
    Unsupported,
};

constexpr uint32_t kAllIntegerRegisters = 0xFFFFU;

struct Snapshot {
    uint64_t ContextIdentity;
    uint64_t ContextGeneration;
    uint64_t StackIdentity;
    uint64_t StackGeneration;
    uint32_t Cpu;
    CaptureKind Kind;
    uint64_t Rip;
    uint64_t Rsp;
    uint64_t Rflags;
    uint64_t Gpr[16];
    uint32_t SavedRegisterMask;
    uint64_t ExtendedStateMask;
};

inline CaptureResult ValidateSnapshot(const ContextRecord& context,
                                      const StackDescriptor& stack,
                                      const Snapshot& snapshot) {
    if (context.ExecutionIdentity.Value == 0 ||
        context.ExecutionIdentity.Generation == 0 ||
        context.ExecutionIdentity.Cpu == kUnknownCpu) {
        return CaptureResult::MissingCpu;
    }
    if (snapshot.ContextIdentity != context.ExecutionIdentity.Value ||
        snapshot.ContextGeneration != context.ExecutionIdentity.Generation) {
        return CaptureResult::StaleContext;
    }
    if (snapshot.StackIdentity != context.Stack.Identity ||
        snapshot.StackGeneration != context.Stack.Generation ||
        stack.Identity != context.Stack.Identity ||
        stack.Generation != context.Stack.Generation) {
        return CaptureResult::StackMismatch;
    }
    if (snapshot.Cpu != context.ExecutionIdentity.Cpu)
        return CaptureResult::MissingCpu;
    if (!IsValidStackDescriptor(stack)) return CaptureResult::Unsupported;
    if (snapshot.Rip == 0) return CaptureResult::InvalidInstructionPointer;
    if (snapshot.Rsp < stack.AccessibleLow ||
        snapshot.Rsp >= stack.AccessibleHigh) {
        return CaptureResult::InvalidStackPointer;
    }
    if ((snapshot.SavedRegisterMask & kAllIntegerRegisters) !=
        kAllIntegerRegisters) {
        return CaptureResult::IncompleteRegisters;
    }
    if (snapshot.Kind == CaptureKind::NativeAotFullState) {
        if (!context.NativeAotAttached) return CaptureResult::Unsupported;
        if (snapshot.ExtendedStateMask == 0)
            return CaptureResult::MissingExtendedState;
        return CaptureResult::Success;
    }
    if (snapshot.Kind == CaptureKind::CooperativeFullState) {
        if (snapshot.ExtendedStateMask == 0)
            return CaptureResult::MissingExtendedState;
        return CaptureResult::Success;
    }
    return CaptureResult::SchedulerStateOnly;
}

inline RegistryResult NativeAotSuspensionCapability(
        const ContextRecord& context) {
    (void)context;
    // No guideXOS stop-and-acknowledge, complete OS CONTEXT, or XSTATE
    // preservation contract is currently connected to ThreadStore.
    return RegistryResult::Unsupported;
}

inline RegistryResult NativeAotRestorationCapability(
        const ContextRecord& context) {
    (void)context;
    // The kernel IRQ epilogue restores its scheduler frame. The target PAL
    // does not implement PalRestoreContext for NativeAOT contexts.
    return RegistryResult::Unsupported;
}

} // namespace executioncontext
} // namespace guidexos
