#pragma once

// Internal status-bearing adapter for the bounded x64 unwind evaluator.
// The caller supplies the stack bounds belonging to the thread being walked;
// the current/executing thread is intentionally not used as a substitute.

#include "x64_unwind_bounded.h"

#include <cstddef>
#include <cstdint>
#include <limits>

namespace guidexos::runtime_walk {

enum class Status : std::uint8_t {
    Success,
    InvalidFrame,
    StackBoundsUnavailable,
    InvalidStackBounds,
    WrongThreadStackBounds,
    StackAccessOutsidePermittedInterval,
    MalformedUnwindMetadata,
    UnsupportedUnwindEncoding,
    UnsupportedEpilogueForm,
    InvalidExecutableAddress,
    ArithmeticOverflow,
    InvalidContextRestoration,
};

// OwnerThread is the NativeAOT Thread whose GetStackBounds values were read.
// AccessibleLow/AccessibleHigh must come from a source that verifies committed
// stack memory; GetStackBounds alone supplies only the maximum allocation.
// MappingBase/MappingSize describe the corresponding address-to-byte mapping.
// Hosted tests can supply an equivalent bounded view over an allocated array.
struct ThreadStackBounds {
    const void* OwnerThread;
    std::uint64_t Low;
    std::uint64_t High;
    std::uint64_t AccessibleLow;
    std::uint64_t AccessibleHigh;
    std::uint64_t MappingBase;
    const std::uint8_t* Mapping;
    std::size_t MappingSize;
};

struct FrameState {
    unwind::Context Registers;
    unwind::ContextPointers RegisterLocations;
};

struct Request {
    const void* WalkedThread;
    // Diagnostic identity only. Stack ownership is checked against WalkedThread.
    const void* ExecutingThread;
    const ThreadStackBounds* StackBounds;
    unwind::ImageView Image;
    unwind::ExecutableCodeRange ExecutableCode;
    std::uint32_t FunctionEntryRva;
    std::uint64_t ControlPc;
    const FrameState* Current;
    FrameState* Caller;
    unwind::UnwindResult* Result;
};

namespace detail {

inline Status MapStatus(unwind::Status status) {
    using UnwindStatus = unwind::Status;
    switch (status) {
    case UnwindStatus::Success:
        return runtime_walk::Status::Success;
    case UnwindStatus::InvalidArgument:
        return Status::InvalidFrame;
    case UnwindStatus::InvalidContext:
        return Status::InvalidExecutableAddress;
    case UnwindStatus::InvalidMetadata:
    case UnwindStatus::MetadataOutOfBounds:
        return Status::MalformedUnwindMetadata;
    case UnwindStatus::OutOfRangeCodeAccess:
    case UnwindStatus::NoFunction:
        return Status::InvalidExecutableAddress;
    case UnwindStatus::InvalidStackAccess:
    case UnwindStatus::MisalignedStack:
        return Status::StackAccessOutsidePermittedInterval;
    case UnwindStatus::ArithmeticOverflow:
        return Status::ArithmeticOverflow;
    case UnwindStatus::UnsupportedVersion:
    case UnwindStatus::UnsupportedHandler:
    case UnwindStatus::UnsupportedChain:
    case UnwindStatus::UnsupportedUnwindEncoding:
        return Status::UnsupportedUnwindEncoding;
    case UnwindStatus::UnsupportedEpilogueForm:
        return Status::UnsupportedEpilogueForm;
    case UnwindStatus::NoEpilogue:
        return Status::InvalidFrame;
    }
    return Status::InvalidFrame;
}

inline bool Add(std::uint64_t left, std::uint64_t right,
                std::uint64_t* value) {
    if (right > (std::numeric_limits<std::uint64_t>::max)() - left)
        return false;
    *value = left + right;
    return true;
}

} // namespace detail

// Unwind one frame transactionally. Stack reads are restricted to the
// half-open interval [current RSP, AccessibleHigh). The caller must reject
// alternate-stack and transition contexts until they can provide their own
// verified bounds.
inline Status UnwindFrame(const Request& request) {
    if (request.WalkedThread == nullptr || request.StackBounds == nullptr ||
        request.Current == nullptr || request.Caller == nullptr ||
        request.Result == nullptr)
        return Status::InvalidFrame;

    const ThreadStackBounds& bounds = *request.StackBounds;
    if (bounds.OwnerThread == nullptr || bounds.Low == 0 || bounds.High == 0 ||
        bounds.AccessibleLow == 0 || bounds.AccessibleHigh == 0 ||
        bounds.Mapping == nullptr || bounds.MappingSize == 0)
        return Status::StackBoundsUnavailable;
    if (bounds.OwnerThread != request.WalkedThread)
        return Status::WrongThreadStackBounds;
    if (bounds.Low >= bounds.High || (bounds.Low & 7u) != 0 ||
        (bounds.High & 7u) != 0 ||
        bounds.AccessibleLow < bounds.Low ||
        bounds.AccessibleHigh > bounds.High ||
        bounds.AccessibleLow >= bounds.AccessibleHigh ||
        (bounds.AccessibleLow & 7u) != 0 ||
        (bounds.AccessibleHigh & 7u) != 0)
        return Status::InvalidStackBounds;

    std::uint64_t mappingEnd = 0;
    if (!detail::Add(bounds.MappingBase,
                     static_cast<std::uint64_t>(bounds.MappingSize),
                     &mappingEnd))
        return Status::ArithmeticOverflow;
    if (mappingEnd <= bounds.MappingBase ||
        bounds.MappingBase > bounds.AccessibleLow ||
        mappingEnd < bounds.AccessibleHigh)
        return Status::InvalidStackBounds;

    const FrameState current = *request.Current;
    if (current.Registers.Rip != request.ControlPc)
        return Status::InvalidFrame;
    if (current.Registers.Rsp < bounds.AccessibleLow ||
        current.Registers.Rsp >= bounds.AccessibleHigh ||
        (current.Registers.Rsp & 7u) != 0)
        return Status::StackAccessOutsidePermittedInterval;

    if (request.Image.Size >
        (std::numeric_limits<std::uint64_t>::max)() - request.Image.BaseAddress)
        return Status::ArithmeticOverflow;
    if (request.Image.Data == nullptr || request.Image.Size == 0)
        return Status::InvalidExecutableAddress;
    std::uint64_t executableBegin = 0;
    std::uint64_t executableEnd = 0;
    if (request.ExecutableCode.BeginRva >= request.ExecutableCode.EndRva ||
        request.ExecutableCode.EndRva > request.Image.Size)
        return Status::InvalidExecutableAddress;
    if (!detail::Add(request.Image.BaseAddress,
                     request.ExecutableCode.BeginRva, &executableBegin) ||
        !detail::Add(request.Image.BaseAddress,
                     request.ExecutableCode.EndRva, &executableEnd))
        return Status::ArithmeticOverflow;
    if (request.ControlPc < executableBegin || request.ControlPc >= executableEnd)
        return Status::InvalidExecutableAddress;

    const std::uint64_t mappingOffset64 =
        current.Registers.Rsp - bounds.MappingBase;
    if (mappingOffset64 > (std::numeric_limits<std::size_t>::max)())
        return Status::ArithmeticOverflow;
    const std::size_t mappingOffset = static_cast<std::size_t>(mappingOffset64);
    const std::uint64_t accessibleSize64 =
        bounds.AccessibleHigh - current.Registers.Rsp;
    if (accessibleSize64 > (std::numeric_limits<std::size_t>::max)() ||
        mappingOffset > bounds.MappingSize ||
        static_cast<std::size_t>(accessibleSize64) > bounds.MappingSize - mappingOffset)
        return Status::StackAccessOutsidePermittedInterval;

    unwind::StackView stack{
        current.Registers.Rsp,
        const_cast<std::uint8_t*>(bounds.Mapping + mappingOffset),
        static_cast<std::size_t>(accessibleSize64)};
    FrameState candidate = current;
    unwind::UnwindResult result{};
    const unwind::Status unwindStatus = unwind::UnwindFrame(
        request.Image, request.ExecutableCode, stack,
        request.FunctionEntryRva, request.ControlPc,
        &candidate.Registers, &candidate.RegisterLocations, &result);
    if (unwindStatus != unwind::Status::Success)
        return detail::MapStatus(unwindStatus);

    if (candidate.Registers.Rsp <= current.Registers.Rsp ||
        candidate.Registers.Rsp > bounds.AccessibleHigh ||
        (candidate.Registers.Rsp & 7u) != 0 ||
        candidate.Registers.Rip < executableBegin ||
        candidate.Registers.Rip >= executableEnd)
        return Status::InvalidContextRestoration;

    *request.Caller = candidate;
    *request.Result = result;
    return Status::Success;
}

} // namespace guidexos::runtime_walk
