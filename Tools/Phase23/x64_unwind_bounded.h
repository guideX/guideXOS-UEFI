#pragma once

// Bounded evaluator for the version 1 Windows x64 UNWIND_INFO operations
// used by ordinary NativeAOT frames. This deliberately does not implement
// the RtlVirtualUnwind ABI: that ABI provides no image or stack bounds and
// no failure result. Callers must provide bounded memory spans and must
// identify epilogues before calling this evaluator.

#include <cstddef>
#include <cstdint>
#include <limits>

namespace guidexos::unwind {

constexpr std::uint32_t NoRuntimeFunction = 0xFFFFFFFFu;

enum class Status : std::uint8_t {
    Success,
    InvalidArgument,
    InvalidMetadata,
    MetadataOutOfBounds,
    StackOutOfBounds,
    AddressOverflow,
    NoFunction,
    UnsupportedVersion,
    UnsupportedHandler,
    UnsupportedChain,
    UnsupportedOpcode,
    UnsupportedEpilogue,
    MisalignedStack,
};

// Prefix/layout required by the pinned NativeAOT COFF manager's x64
// CONTEXT access: integer registers start at 0x78, RIP at 0xF8, and XMM0 at
// 0x1A0. The intervening bytes retain the Windows CONTEXT offsets.
struct alignas(16) Context {
    std::uint8_t ReservedToRax[0x78];
    std::uint64_t Rax;
    std::uint64_t Rcx;
    std::uint64_t Rdx;
    std::uint64_t Rbx;
    std::uint64_t Rsp;
    std::uint64_t Rbp;
    std::uint64_t Rsi;
    std::uint64_t Rdi;
    std::uint64_t R8;
    std::uint64_t R9;
    std::uint64_t R10;
    std::uint64_t R11;
    std::uint64_t R12;
    std::uint64_t R13;
    std::uint64_t R14;
    std::uint64_t R15;
    std::uint64_t Rip;
    std::uint8_t ReservedToXmm0[0xA0];
    struct XmmValue {
        std::uint64_t Low;
        std::uint64_t High;
    } Xmm[16];
    std::uint8_t ReservedAfterXmm[0x230];
};

struct RuntimeFunction {
    std::uint32_t BeginAddress;
    std::uint32_t EndAddress;
    std::uint32_t UnwindData;
};

// Windows x64 KNONVOLATILE_CONTEXT_POINTERS layout: all 16 floating-point
// pointers, followed by all 16 integer-register pointers (RAX through R15).
struct ContextPointers {
    void* FloatingContext[16];
    std::uint64_t* IntegerContext[16];
};

struct ImageView {
    std::uint64_t BaseAddress;
    const std::uint8_t* Data;
    std::size_t Size;
};

struct StackView {
    std::uint64_t BaseAddress;
    std::uint8_t* Data;
    std::size_t Size;
};

struct UnwindResult {
    std::uint64_t EstablisherFrame;
};

static_assert(offsetof(Context, Rax) == 0x78, "x64 CONTEXT RAX offset");
static_assert(offsetof(Context, Rsp) == 0x98, "x64 CONTEXT RSP offset");
static_assert(offsetof(Context, Rip) == 0xF8, "x64 CONTEXT RIP offset");
static_assert(offsetof(Context, Xmm) == 0x1A0, "x64 CONTEXT XMM offset");
static_assert(sizeof(Context) == 0x4D0, "x64 CONTEXT size");
static_assert(offsetof(ContextPointers, IntegerContext) == 0x80,
              "x64 KNONVOLATILE_CONTEXT_POINTERS integer offset");

namespace detail {

inline bool Add(std::uint64_t left, std::uint64_t right,
                std::uint64_t* result) {
    if (right > (std::numeric_limits<std::uint64_t>::max)() - left)
        return false;
    *result = left + right;
    return true;
}

inline bool ValidImage(const ImageView& image) {
    return image.Data != nullptr && image.Size != 0 &&
           image.Size <= (std::numeric_limits<std::uint64_t>::max)() - image.BaseAddress;
}

inline bool ValidStack(const StackView& stack) {
    return stack.Data != nullptr && stack.Size != 0 &&
           stack.Size <= (std::numeric_limits<std::uint64_t>::max)() - stack.BaseAddress;
}

inline bool ImageRange(const ImageView& image, std::uint64_t offset,
                       std::size_t length) {
    return offset <= image.Size && length <= image.Size - static_cast<std::size_t>(offset);
}

inline bool StackRange(const StackView& stack, std::uint64_t address,
                       std::size_t length, std::size_t alignment = 1) {
    if (address < stack.BaseAddress)
        return false;
    const std::uint64_t offset = address - stack.BaseAddress;
    if (offset > stack.Size || length > stack.Size - static_cast<std::size_t>(offset))
        return false;
    if (alignment > 1 &&
        ((address % alignment) != 0 ||
         (reinterpret_cast<std::uintptr_t>(stack.Data + offset) % alignment) != 0))
        return false;
    return true;
}

inline bool ReadImage8(const ImageView& image, std::uint64_t offset,
                       std::uint8_t* value) {
    if (!ImageRange(image, offset, 1))
        return false;
    *value = image.Data[offset];
    return true;
}

inline bool ReadImage16(const ImageView& image, std::uint64_t offset,
                        std::uint16_t* value) {
    if (!ImageRange(image, offset, 2))
        return false;
    *value = static_cast<std::uint16_t>(image.Data[offset]) |
             (static_cast<std::uint16_t>(image.Data[offset + 1]) << 8);
    return true;
}

inline bool ReadImage32(const ImageView& image, std::uint64_t offset,
                        std::uint32_t* value) {
    if (!ImageRange(image, offset, 4))
        return false;
    *value = static_cast<std::uint32_t>(image.Data[offset]) |
             (static_cast<std::uint32_t>(image.Data[offset + 1]) << 8) |
             (static_cast<std::uint32_t>(image.Data[offset + 2]) << 16) |
             (static_cast<std::uint32_t>(image.Data[offset + 3]) << 24);
    return true;
}

inline bool ReadImage64(const ImageView& image, std::uint64_t offset,
                        std::uint64_t* value) {
    std::uint32_t low = 0;
    std::uint32_t high = 0;
    if (!ReadImage32(image, offset, &low) ||
        !ReadImage32(image, offset + 4, &high))
        return false;
    *value = low | (static_cast<std::uint64_t>(high) << 32);
    return true;
}

inline bool ReadStack64(const StackView& stack, std::uint64_t address,
                        std::uint64_t* value) {
    if (!StackRange(stack, address, 8, 8))
        return false;
    const std::size_t offset = static_cast<std::size_t>(address - stack.BaseAddress);
    std::uint32_t low = 0;
    std::uint32_t high = 0;
    for (std::size_t index = 0; index < 4; ++index) {
        low |= static_cast<std::uint32_t>(stack.Data[offset + index]) << (index * 8);
        high |= static_cast<std::uint32_t>(stack.Data[offset + index + 4]) << (index * 8);
    }
    *value = low | (static_cast<std::uint64_t>(high) << 32);
    return true;
}

inline bool ReadStackXmm(const StackView& stack, std::uint64_t address,
                         Context::XmmValue* value) {
    if (!StackRange(stack, address, 16, 16))
        return false;
    const std::size_t offset = static_cast<std::size_t>(address - stack.BaseAddress);
    std::uint64_t parts[2]{};
    for (std::size_t half = 0; half < 2; ++half) {
        for (std::size_t index = 0; index < 8; ++index)
            parts[half] |= static_cast<std::uint64_t>(stack.Data[offset + half * 8 + index]) << (index * 8);
    }
    value->Low = parts[0];
    value->High = parts[1];
    return true;
}

inline std::uint64_t* IntegerRegister(Context* context, std::uint8_t number) {
    switch (number) {
        case 0: return &context->Rax;
        case 1: return &context->Rcx;
        case 2: return &context->Rdx;
        case 3: return &context->Rbx;
        case 4: return &context->Rsp;
        case 5: return &context->Rbp;
        case 6: return &context->Rsi;
        case 7: return &context->Rdi;
        case 8: return &context->R8;
        case 9: return &context->R9;
        case 10: return &context->R10;
        case 11: return &context->R11;
        case 12: return &context->R12;
        case 13: return &context->R13;
        case 14: return &context->R14;
        case 15: return &context->R15;
        default: return nullptr;
    }
}

inline bool IsNonvolatileIntegerRegister(std::uint8_t number) {
    return number == 3 || number == 5 || number == 6 || number == 7 ||
           (number >= 12 && number <= 15);
}

struct Operation {
    std::uint8_t CodeOffset;
    std::uint8_t Opcode;
    std::uint8_t Info;
    std::uint32_t Operand;
};

struct ParsedInfo {
    std::uint8_t PrologSize;
    std::uint8_t FrameRegister;
    std::uint8_t FrameOffset;
    std::uint8_t FrameSetupOffset;
    std::uint8_t OperationCount;
    Operation Operations[255];
};

inline Status ReadRuntimeFunction(const ImageView& image, std::uint32_t rva,
                                  RuntimeFunction* function) {
    if ((rva & 3u) != 0)
        return Status::InvalidMetadata;
    std::uint32_t begin = 0;
    std::uint32_t end = 0;
    std::uint32_t unwind = 0;
    if (!ReadImage32(image, rva, &begin) ||
        !ReadImage32(image, static_cast<std::uint64_t>(rva) + 4, &end) ||
        !ReadImage32(image, static_cast<std::uint64_t>(rva) + 8, &unwind))
        return Status::MetadataOutOfBounds;
    if (begin >= end || end > image.Size || unwind == 0 || (unwind & 3u) != 0)
        return Status::InvalidMetadata;
    function->BeginAddress = begin;
    function->EndAddress = end;
    function->UnwindData = unwind;
    return Status::Success;
}

inline Status ParseInfo(const ImageView& image, std::uint32_t unwindRva,
                        ParsedInfo* parsed) {
    std::uint8_t versionAndFlags = 0;
    std::uint8_t prologSize = 0;
    std::uint8_t codeSlots = 0;
    std::uint8_t frame = 0;
    if (!ReadImage8(image, unwindRva, &versionAndFlags) ||
        !ReadImage8(image, static_cast<std::uint64_t>(unwindRva) + 1, &prologSize) ||
        !ReadImage8(image, static_cast<std::uint64_t>(unwindRva) + 2, &codeSlots) ||
        !ReadImage8(image, static_cast<std::uint64_t>(unwindRva) + 3, &frame))
        return Status::MetadataOutOfBounds;

    const std::uint8_t version = versionAndFlags & 7u;
    const std::uint8_t flags = versionAndFlags >> 3;
    if (version != 1)
        return Status::UnsupportedVersion;
    if ((flags & ~7u) != 0 || ((flags & 4u) != 0 && (flags & 3u) != 0))
        return Status::InvalidMetadata;
    if ((flags & 4u) != 0)
        return Status::UnsupportedChain;
    if ((flags & 3u) != 0)
        return Status::UnsupportedHandler;

    parsed->PrologSize = prologSize;
    parsed->FrameRegister = frame & 0x0Fu;
    parsed->FrameOffset = frame >> 4;
    parsed->FrameSetupOffset = 0;
    parsed->OperationCount = 0;

    if (parsed->FrameRegister != 0 &&
        !IsNonvolatileIntegerRegister(parsed->FrameRegister))
        return Status::InvalidMetadata;
    if (parsed->FrameRegister == 0 && parsed->FrameOffset != 0)
        return Status::InvalidMetadata;

    std::uint16_t slot = 0;
    std::uint8_t previousOffset = 0xFF;
    bool sawFrameSetup = false;
    bool sawPush = false;
    while (slot < codeSlots) {
        std::uint8_t codeOffset = 0;
        std::uint8_t opAndInfo = 0;
        const std::uint64_t codeRva = static_cast<std::uint64_t>(unwindRva) + 4 + slot * 2;
        if (!ReadImage8(image, codeRva, &codeOffset) ||
            !ReadImage8(image, codeRva + 1, &opAndInfo))
            return Status::MetadataOutOfBounds;
        const std::uint8_t opcode = opAndInfo & 0x0Fu;
        const std::uint8_t info = opAndInfo >> 4;
        if (codeOffset > prologSize || codeOffset > previousOffset)
            return Status::InvalidMetadata;
        previousOffset = codeOffset;

        std::uint8_t extraSlots = 0;
        std::uint32_t operand = 0;
        switch (opcode) {
            case 0: // UWOP_PUSH_NONVOL
                if (!IsNonvolatileIntegerRegister(info))
                    return Status::InvalidMetadata;
                sawPush = true;
                break;
            case 1: // UWOP_ALLOC_LARGE
                if (info == 0) {
                    extraSlots = 1;
                    std::uint16_t value = 0;
                    if (slot + extraSlots >= codeSlots)
                        return Status::InvalidMetadata;
                    if (!ReadImage16(image, codeRva + 2, &value))
                        return Status::MetadataOutOfBounds;
                    operand = value;
                    if (operand == 0)
                        return Status::InvalidMetadata;
                } else if (info == 1) {
                    extraSlots = 2;
                    if (slot + extraSlots >= codeSlots)
                        return Status::InvalidMetadata;
                    if (!ReadImage32(image, codeRva + 2, &operand))
                        return Status::MetadataOutOfBounds;
                    if (operand == 0 || (operand & 7u) != 0)
                        return Status::InvalidMetadata;
                } else {
                    return Status::InvalidMetadata;
                }
                break;
            case 2: // UWOP_ALLOC_SMALL
                break;
            case 3: // UWOP_SET_FPREG
                if (info != 0 || parsed->FrameRegister == 0 || sawFrameSetup)
                    return Status::InvalidMetadata;
                sawFrameSetup = true;
                parsed->FrameSetupOffset = codeOffset;
                break;
            case 4: // UWOP_SAVE_NONVOL
                if (!IsNonvolatileIntegerRegister(info))
                    return Status::InvalidMetadata;
                extraSlots = 1;
                if (slot + extraSlots >= codeSlots)
                    return Status::InvalidMetadata;
                {
                    std::uint16_t value = 0;
                    if (!ReadImage16(image, codeRva + 2, &value))
                        return Status::MetadataOutOfBounds;
                    operand = value;
                }
                break;
            case 5: // UWOP_SAVE_NONVOL_FAR
                if (!IsNonvolatileIntegerRegister(info))
                    return Status::InvalidMetadata;
                extraSlots = 2;
                if (slot + extraSlots >= codeSlots)
                    return Status::InvalidMetadata;
                if (!ReadImage32(image, codeRva + 2, &operand))
                    return Status::MetadataOutOfBounds;
                if ((operand & 7u) != 0)
                    return Status::InvalidMetadata;
                break;
            case 6: // UWOP_EPILOG (version 2)
            case 7: // UWOP_SPARE_CODE
                return Status::UnsupportedOpcode;
            case 8: // UWOP_SAVE_XMM128
                if (info < 6 || info > 15)
                    return Status::InvalidMetadata;
                extraSlots = 1;
                if (slot + extraSlots >= codeSlots)
                    return Status::InvalidMetadata;
                {
                    std::uint16_t value = 0;
                    if (!ReadImage16(image, codeRva + 2, &value))
                        return Status::MetadataOutOfBounds;
                    operand = value;
                }
                break;
            case 9: // UWOP_SAVE_XMM128_FAR
                if (info < 6 || info > 15)
                    return Status::InvalidMetadata;
                extraSlots = 2;
                if (slot + extraSlots >= codeSlots)
                    return Status::InvalidMetadata;
                if (!ReadImage32(image, codeRva + 2, &operand))
                    return Status::MetadataOutOfBounds;
                if ((operand & 15u) != 0)
                    return Status::InvalidMetadata;
                break;
            case 10: // UWOP_PUSH_MACHFRAME is outside ordinary managed frames
                return Status::UnsupportedOpcode;
            default:
                return Status::UnsupportedOpcode;
        }

        Operation& operation = parsed->Operations[parsed->OperationCount++];
        operation.CodeOffset = codeOffset;
        operation.Opcode = opcode;
        operation.Info = info;
        operation.Operand = operand;
        slot = static_cast<std::uint16_t>(slot + 1 + extraSlots);
    }

    if ((parsed->FrameRegister != 0) != sawFrameSetup)
        return Status::InvalidMetadata;
    if (parsed->FrameRegister != 0) {
        for (std::uint16_t index = 0; index < parsed->OperationCount; ++index) {
            const Operation& operation = parsed->Operations[index];
            if ((operation.Opcode == 4 || operation.Opcode == 5 ||
                 operation.Opcode == 8 || operation.Opcode == 9) &&
                operation.CodeOffset <= parsed->FrameSetupOffset)
                return Status::InvalidMetadata;
        }
    }
    if (sawPush) {
        bool foundPush = false;
        for (std::uint16_t index = 0; index < parsed->OperationCount; ++index) {
            if (parsed->Operations[index].Opcode == 0) {
                foundPush = true;
            } else if (foundPush) {
                return Status::InvalidMetadata;
            }
        }
    }
    return Status::Success;
}

inline Status RestoreInteger(Context* context, ContextPointers* pointers,
                             const StackView& stack, std::uint8_t reg,
                             std::uint64_t address) {
    std::uint64_t value = 0;
    if (!ReadStack64(stack, address, &value))
        return StackRange(stack, address, 8) ? Status::MisalignedStack : Status::StackOutOfBounds;
    std::uint64_t* target = IntegerRegister(context, reg);
    if (target == nullptr)
        return Status::InvalidMetadata;
    *target = value;
    pointers->IntegerContext[reg] = reinterpret_cast<std::uint64_t*>(
        stack.Data + static_cast<std::size_t>(address - stack.BaseAddress));
    return Status::Success;
}

inline Status AdvanceRsp(Context* context, const StackView& stack,
                         std::uint64_t amount) {
    std::uint64_t next = 0;
    if (!Add(context->Rsp, amount, &next))
        return Status::AddressOverflow;
    if (!StackRange(stack, next, 0))
        return Status::StackOutOfBounds;
    context->Rsp = next;
    return Status::Success;
}

inline Status ApplyInfo(const ImageView& image, const StackView& stack,
                        const RuntimeFunction& function,
                        std::uint64_t controlPc, Context* context,
                        ContextPointers* pointers,
                        std::uint64_t* establisherFrame) {
    ParsedInfo parsed{};
    Status status = ParseInfo(image, function.UnwindData, &parsed);
    if (status != Status::Success)
        return status;

    const std::uint64_t functionStart = image.BaseAddress + function.BeginAddress;
    if (controlPc < functionStart || controlPc >= image.BaseAddress + function.EndAddress)
        return Status::InvalidArgument;
    const std::uint64_t pcOffset = controlPc - functionStart;
    const std::uint8_t unwindOffset = static_cast<std::uint8_t>(
        pcOffset < parsed.PrologSize ? pcOffset : parsed.PrologSize);
    std::uint64_t frameBase = context->Rsp;
    bool frameSetupExecuted = false;
    for (std::uint16_t index = 0; index < parsed.OperationCount; ++index) {
        const Operation& operation = parsed.Operations[index];
        if (operation.Opcode == 3 && operation.CodeOffset <= unwindOffset)
            frameSetupExecuted = true;
    }
    if (frameSetupExecuted) {
        const std::uint64_t* frame = IntegerRegister(context, parsed.FrameRegister);
        const std::uint64_t displacement = static_cast<std::uint64_t>(parsed.FrameOffset) * 16;
        if (frame == nullptr || *frame < displacement)
            return Status::AddressOverflow;
        frameBase = *frame - displacement;
        if (!StackRange(stack, frameBase, 0))
            return Status::StackOutOfBounds;
    }
    *establisherFrame = frameBase;

    for (std::uint16_t index = 0; index < parsed.OperationCount; ++index) {
        const Operation& operation = parsed.Operations[index];
        if (operation.CodeOffset > unwindOffset)
            continue;

        std::uint64_t address = 0;
        switch (operation.Opcode) {
            case 0: // UWOP_PUSH_NONVOL
                status = RestoreInteger(context, pointers, stack, operation.Info, context->Rsp);
                if (status != Status::Success)
                    return status;
                status = AdvanceRsp(context, stack, 8);
                if (status != Status::Success)
                    return status;
                break;
            case 1: { // UWOP_ALLOC_LARGE
                const std::uint64_t allocation = operation.Info == 0
                    ? static_cast<std::uint64_t>(operation.Operand) * 8
                    : operation.Operand;
                status = AdvanceRsp(context, stack, allocation);
                if (status != Status::Success)
                    return status;
                break;
            }
            case 2: // UWOP_ALLOC_SMALL
                status = AdvanceRsp(context, stack,
                    static_cast<std::uint64_t>(operation.Info + 1) * 8);
                if (status != Status::Success)
                    return status;
                break;
            case 3: { // UWOP_SET_FPREG
                const std::uint64_t* frame = IntegerRegister(context, parsed.FrameRegister);
                const std::uint64_t displacement = static_cast<std::uint64_t>(parsed.FrameOffset) * 16;
                if (frame == nullptr || *frame < displacement)
                    return Status::AddressOverflow;
                context->Rsp = *frame - displacement;
                if (!StackRange(stack, context->Rsp, 0))
                    return Status::StackOutOfBounds;
                break;
            }
            case 4: // UWOP_SAVE_NONVOL
                if (!Add(frameBase, static_cast<std::uint64_t>(operation.Operand) * 8, &address))
                    return Status::AddressOverflow;
                status = RestoreInteger(context, pointers, stack, operation.Info, address);
                if (status != Status::Success)
                    return status;
                break;
            case 5: // UWOP_SAVE_NONVOL_FAR
                if (!Add(frameBase, operation.Operand, &address))
                    return Status::AddressOverflow;
                status = RestoreInteger(context, pointers, stack, operation.Info, address);
                if (status != Status::Success)
                    return status;
                break;
            case 8: // UWOP_SAVE_XMM128
            case 9: { // UWOP_SAVE_XMM128_FAR
                const std::uint64_t displacement = operation.Opcode == 8
                    ? static_cast<std::uint64_t>(operation.Operand) * 16
                    : operation.Operand;
                if (!Add(frameBase, displacement, &address))
                    return Status::AddressOverflow;
                Context::XmmValue value{};
                if (!ReadStackXmm(stack, address, &value))
                    return StackRange(stack, address, 16) ? Status::MisalignedStack : Status::StackOutOfBounds;
                context->Xmm[operation.Info] = value;
                pointers->FloatingContext[operation.Info] =
                    stack.Data + static_cast<std::size_t>(address - stack.BaseAddress);
                break;
            }
            default:
                return Status::UnsupportedOpcode;
        }
    }
    return Status::Success;
}

} // namespace detail

// The table is a bounded .pdata array. Entries must be sorted and disjoint;
// a successful result returns the matching table-entry RVA and function data.
inline Status FindRuntimeFunction(const ImageView& image,
                                 std::uint32_t tableRva,
                                 std::uint32_t tableSize,
                                 std::uint64_t controlPc,
                                 RuntimeFunction* function,
                                 std::uint32_t* functionEntryRva) {
    if (function == nullptr || functionEntryRva == nullptr || !detail::ValidImage(image))
        return Status::InvalidArgument;
    if ((tableRva & 3u) != 0 || tableSize == 0 ||
        tableSize % sizeof(RuntimeFunction) != 0 ||
        !detail::ImageRange(image, tableRva, tableSize))
        return Status::MetadataOutOfBounds;
    if (controlPc < image.BaseAddress ||
        controlPc - image.BaseAddress >= image.Size)
        return Status::NoFunction;

    const std::uint32_t count = tableSize / sizeof(RuntimeFunction);
    std::uint32_t previousEnd = 0;
    for (std::uint32_t index = 0; index < count; ++index) {
        const std::uint64_t entryOffset = static_cast<std::uint64_t>(tableRva) +
                                          index * sizeof(RuntimeFunction);
        RuntimeFunction candidate{};
        const Status status = detail::ReadRuntimeFunction(
            image, static_cast<std::uint32_t>(entryOffset), &candidate);
        if (status != Status::Success)
            return status;
        if (index != 0 && candidate.BeginAddress < previousEnd)
            return Status::InvalidMetadata;
        previousEnd = candidate.EndAddress;
    }

    const std::uint64_t relativePc = controlPc - image.BaseAddress;
    std::uint32_t low = 0;
    std::uint32_t high = count;
    while (low < high) {
        const std::uint32_t middle = low + (high - low) / 2;
        RuntimeFunction candidate{};
        const std::uint32_t entryRva = static_cast<std::uint32_t>(
            static_cast<std::uint64_t>(tableRva) + middle * sizeof(RuntimeFunction));
        const Status status = detail::ReadRuntimeFunction(image, entryRva, &candidate);
        if (status != Status::Success)
            return status;
        if (relativePc < candidate.BeginAddress) {
            high = middle;
        } else if (relativePc >= candidate.EndAddress) {
            low = middle + 1;
        } else {
            *function = candidate;
            *functionEntryRva = entryRva;
            return Status::Success;
        }
    }
    return Status::NoFunction;
}

// Unwind one frame into a temporary context, committing only on success.
// ContextPointers identifies each supported save slot used by this evaluator;
// its far-save pointer behavior has not been proven equivalent to Windows.
// Set functionEntryRva to NoRuntimeFunction for a leaf frame. For a managed
// non-leaf frame, callers must have already bounded the PE image and stack,
// resolved the function entry, and established that controlPc is not in an
// epilogue. Handler data and chained records are rejected.
inline Status UnwindFrame(const ImageView& image, const StackView& stack,
                          std::uint32_t functionEntryRva,
                          std::uint64_t controlPc,
                          bool controlPcIsEpilogue,
                          Context* context,
                          ContextPointers* contextPointers,
                          UnwindResult* result) {
    if (context == nullptr || result == nullptr || !detail::ValidImage(image) ||
        !detail::ValidStack(stack))
        return Status::InvalidArgument;
    if (controlPcIsEpilogue)
        return Status::UnsupportedEpilogue;
    if (!detail::StackRange(stack, context->Rsp, 0))
        return Status::StackOutOfBounds;
    if ((context->Rsp & 7u) != 0)
        return Status::MisalignedStack;

    Context working = *context;
    ContextPointers workingPointers{};
    if (contextPointers != nullptr)
        workingPointers = *contextPointers;
    const std::uint64_t originalRsp = working.Rsp;
    std::uint64_t establisher = originalRsp;
    if (functionEntryRva != NoRuntimeFunction) {
        RuntimeFunction function{};
        Status status = detail::ReadRuntimeFunction(image, functionEntryRva, &function);
        if (status != Status::Success)
            return status;
        status = detail::ApplyInfo(image, stack, function, controlPc,
                                   &working, &workingPointers, &establisher);
        if (status != Status::Success)
            return status;
    }

    std::uint64_t returnAddress = 0;
    if (!detail::ReadStack64(stack, working.Rsp, &returnAddress))
        return detail::StackRange(stack, working.Rsp, 8)
            ? Status::MisalignedStack : Status::StackOutOfBounds;
    Status status = detail::AdvanceRsp(&working, stack, 8);
    if (status != Status::Success)
        return status;
    if (working.Rsp <= originalRsp)
        return Status::InvalidMetadata;

    working.Rip = returnAddress;
    *context = working;
    if (contextPointers != nullptr)
        *contextPointers = workingPointers;
    result->EstablisherFrame = establisher;
    return Status::Success;
}

} // namespace guidexos::unwind
