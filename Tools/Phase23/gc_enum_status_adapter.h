#pragma once

// Internal adapter for ICodeManager::EnumGcRefs, whose pinned ABI returns
// void. A runtime caller can use this callback prefix without changing that
// ABI, inspect Status after the manager returns, and fail-stop before it lets
// an incomplete root scan continue.

#include <cstddef>
#include <cstdint>
#include <limits>

namespace guidexos::gc_contract {

enum class Status : std::uint8_t {
    Completed,
    FailedFrameUnwind,
    InvalidGcMetadata,
    InvalidRootLocation,
    InvalidStackBounds,
    CallbackFailure,
    InvalidArgument,
};

struct AddressRange {
    std::uintptr_t Begin;
    std::uintptr_t End;
};

using LegacyCallback = void (*)(void*, void**, std::uint32_t);
using CheckedCallback = bool (*)(void*, void**, std::uint32_t);

// The first field intentionally matches GCEnumContext::pCallback. Pass this
// object to the legacy manager through its existing GCEnumContext pointer.
struct CallbackContext {
    LegacyCallback pCallback;
    CheckedCallback Consumer;
    void* ConsumerContext;
    const AddressRange* WritableRanges;
    std::size_t WritableRangeCount;
    std::size_t RootCount;
    std::size_t CallbackCount;
    std::size_t FailAfterRootCount;
    Status CurrentStatus;
};

constexpr std::size_t NoInjectedFailure =
    (std::numeric_limits<std::size_t>::max)();

inline bool IsWritableRootSlot(const CallbackContext& context,
                               const void* slotAddress) {
    if (slotAddress == nullptr)
        return false;
    const std::uintptr_t address = reinterpret_cast<std::uintptr_t>(slotAddress);
    if ((address % alignof(void*)) != 0)
        return false;

    if (sizeof(void*) > (std::numeric_limits<std::uintptr_t>::max)() - address)
        return false;
    const std::uintptr_t end = address + sizeof(void*);

    for (std::size_t index = 0; index < context.WritableRangeCount; ++index) {
        const AddressRange& range = context.WritableRanges[index];
        if (range.Begin <= address && end <= range.End)
            return true;
    }
    return false;
}

inline void Dispatch(void* rawContext, void** slot, std::uint32_t flags) {
    CallbackContext* context = static_cast<CallbackContext*>(rawContext);
    if (context == nullptr)
        return;

    ++context->CallbackCount;
    if (context->CurrentStatus != Status::Completed)
        return;
    if (!IsWritableRootSlot(*context, slot)) {
        context->CurrentStatus = Status::InvalidRootLocation;
        return;
    }
    if (context->Consumer == nullptr ||
        context->RootCount == context->FailAfterRootCount) {
        context->CurrentStatus = Status::CallbackFailure;
        return;
    }
    if (!context->Consumer(context->ConsumerContext, slot, flags)) {
        context->CurrentStatus = Status::CallbackFailure;
        return;
    }

    ++context->RootCount;
}

inline CallbackContext MakeCallbackContext(
    CheckedCallback consumer, void* consumerContext,
    const AddressRange* writableRanges, std::size_t writableRangeCount,
    std::size_t failAfterRootCount = NoInjectedFailure) {
    CallbackContext context{};
    context.pCallback = Dispatch;
    context.Consumer = consumer;
    context.ConsumerContext = consumerContext;
    context.WritableRanges = writableRanges;
    context.WritableRangeCount = writableRangeCount;
    context.FailAfterRootCount = failAfterRootCount;
    context.CurrentStatus = consumer != nullptr &&
            (writableRanges != nullptr || writableRangeCount == 0)
        ? Status::Completed
        : Status::InvalidArgument;
    for (std::size_t index = 0;
         context.CurrentStatus == Status::Completed &&
         index < writableRangeCount;
         ++index) {
        if (writableRanges[index].Begin >= writableRanges[index].End)
            context.CurrentStatus = Status::InvalidArgument;
    }
    return context;
}

inline bool MayEnumerateFrame(const CallbackContext& context) {
    return context.CurrentStatus == Status::Completed;
}

inline void RecordUnwindFailure(CallbackContext* context) {
    if (context != nullptr && context->CurrentStatus == Status::Completed)
        context->CurrentStatus = Status::FailedFrameUnwind;
}

inline void RecordMetadataFailure(CallbackContext* context) {
    if (context != nullptr && context->CurrentStatus == Status::Completed)
        context->CurrentStatus = Status::InvalidGcMetadata;
}

inline void RecordStackBoundsFailure(CallbackContext* context) {
    if (context != nullptr && context->CurrentStatus == Status::Completed)
        context->CurrentStatus = Status::InvalidStackBounds;
}

} // namespace guidexos::gc_contract
