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
    InvalidContext,
    InvalidMetadata,
    MetadataOutOfBounds,
    OutOfRangeCodeAccess,
    InvalidStackAccess,
    StackOutOfBounds = InvalidStackAccess,
    ArithmeticOverflow,
    AddressOverflow = ArithmeticOverflow,
    NoFunction,
    NoEpilogue,
    UnsupportedVersion,
    UnsupportedHandler,
    UnsupportedChain,
    UnsupportedUnwindEncoding,
    UnsupportedOpcode = UnsupportedUnwindEncoding,
    UnsupportedEpilogueForm,
    UnsupportedEpilogue = UnsupportedEpilogueForm,
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

// A validated executable image span, expressed as a half-open RVA range.
// Every function and instruction byte used by epilogue decoding must remain
// inside both this range and its RUNTIME_FUNCTION interval.
struct ExecutableCodeRange {
    std::uint32_t BeginRva;
    std::uint32_t EndRva;
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
                return Status::UnsupportedUnwindEncoding;
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
                return Status::UnsupportedUnwindEncoding;
            default:
                return Status::UnsupportedUnwindEncoding;
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
        return StackRange(stack, address, 8) ? Status::MisalignedStack : Status::InvalidStackAccess;
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
        return Status::ArithmeticOverflow;
    if (!StackRange(stack, next, 0))
        return Status::InvalidStackAccess;
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
            return Status::ArithmeticOverflow;
        frameBase = *frame - displacement;
        if (!StackRange(stack, frameBase, 0))
            return Status::InvalidStackAccess;
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
                    return Status::ArithmeticOverflow;
                context->Rsp = *frame - displacement;
                if (!StackRange(stack, context->Rsp, 0))
                    return Status::InvalidStackAccess;
                break;
            }
            case 4: // UWOP_SAVE_NONVOL
                if (!Add(frameBase, static_cast<std::uint64_t>(operation.Operand) * 8, &address))
                    return Status::ArithmeticOverflow;
                status = RestoreInteger(context, pointers, stack, operation.Info, address);
                if (status != Status::Success)
                    return status;
                break;
            case 5: // UWOP_SAVE_NONVOL_FAR
                if (!Add(frameBase, operation.Operand, &address))
                    return Status::ArithmeticOverflow;
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
                    return Status::ArithmeticOverflow;
                Context::XmmValue value{};
                if (!ReadStackXmm(stack, address, &value))
                    return StackRange(stack, address, 16) ? Status::MisalignedStack : Status::InvalidStackAccess;
                context->Xmm[operation.Info] = value;
                pointers->FloatingContext[operation.Info] =
                    stack.Data + static_cast<std::size_t>(address - stack.BaseAddress);
                break;
            }
            default:
                return Status::UnsupportedUnwindEncoding;
        }
    }
    return Status::Success;
}

struct CodeCursor {
    std::uint32_t Rva;
    std::uint8_t BytesRead;
};

inline Status ReadCode8(const ImageView& image,
                        const ExecutableCodeRange& codeRange,
                        const RuntimeFunction& function,
                        CodeCursor* cursor, std::uint8_t* value) {
    if (cursor->Rva < codeRange.BeginRva || cursor->Rva >= codeRange.EndRva ||
        cursor->Rva < function.BeginAddress || cursor->Rva >= function.EndAddress ||
        !ImageRange(image, cursor->Rva, 1))
        return Status::OutOfRangeCodeAccess;
    if (cursor->BytesRead >= 32)
        return Status::UnsupportedEpilogueForm;
    *value = image.Data[cursor->Rva];
    ++cursor->Rva;
    ++cursor->BytesRead;
    return Status::Success;
}

inline Status ReadCode16(const ImageView& image,
                         const ExecutableCodeRange& codeRange,
                         const RuntimeFunction& function,
                         CodeCursor* cursor, std::uint16_t* value) {
    std::uint8_t low = 0;
    std::uint8_t high = 0;
    Status status = ReadCode8(image, codeRange, function, cursor, &low);
    if (status != Status::Success)
        return status;
    status = ReadCode8(image, codeRange, function, cursor, &high);
    if (status != Status::Success)
        return status;
    *value = static_cast<std::uint16_t>(low) |
             (static_cast<std::uint16_t>(high) << 8);
    return Status::Success;
}

inline Status ReadCode32(const ImageView& image,
                         const ExecutableCodeRange& codeRange,
                         const RuntimeFunction& function,
                         CodeCursor* cursor, std::uint32_t* value) {
    std::uint32_t result = 0;
    for (std::uint32_t index = 0; index < 4; ++index) {
        std::uint8_t byte = 0;
        const Status status = ReadCode8(image, codeRange, function, cursor, &byte);
        if (status != Status::Success)
            return status;
        result |= static_cast<std::uint32_t>(byte) << (index * 8);
    }
    *value = result;
    return Status::Success;
}

inline bool ValidCodeRange(const ImageView& image,
                           const ExecutableCodeRange& codeRange,
                           const RuntimeFunction& function) {
    return codeRange.BeginRva < codeRange.EndRva &&
           codeRange.EndRva <= image.Size &&
           function.BeginAddress >= codeRange.BeginRva &&
           function.EndAddress <= codeRange.EndRva &&
           function.BeginAddress < function.EndAddress;
}

inline Status AddSigned(std::uint64_t base, std::int64_t displacement,
                        std::uint64_t* result) {
    if (displacement >= 0)
        return Add(base, static_cast<std::uint64_t>(displacement), result)
            ? Status::Success : Status::ArithmeticOverflow;
    const std::uint64_t magnitude =
        static_cast<std::uint64_t>(-(displacement + 1)) + 1;
    if (base < magnitude)
        return Status::ArithmeticOverflow;
    *result = base - magnitude;
    return Status::Success;
}

struct EpilogueSequence {
    bool HasStackRestore;
    bool UsesFrameRegister;
    std::uint64_t StackRestore;
    std::int64_t FrameDisplacement;
    std::uint8_t FrameRegister;
    std::uint8_t PopCount;
    std::uint8_t Pops[8];
    bool HasReturnAdjustment;
    std::uint16_t ReturnAdjustment;
};

inline bool IsRex(std::uint8_t byte) {
    return byte >= 0x40 && byte <= 0x4F;
}

inline Status ParseReturn(const ImageView& image,
                          const ExecutableCodeRange& codeRange,
                          const RuntimeFunction& function,
                          CodeCursor* cursor,
                          std::uint8_t firstByte,
                          std::uint8_t rex,
                          bool priorInstruction,
                          bool* isTailJump,
                          std::uint16_t* returnAdjustment) {
    std::uint8_t opcode = firstByte;
    if (opcode == 0xF3) {
        if (rex != 0)
            return Status::UnsupportedEpilogueForm;
        Status status = ReadCode8(image, codeRange, function, cursor, &opcode);
        if (status != Status::Success)
            return status;
        if (IsRex(opcode)) {
            rex = opcode;
            status = ReadCode8(image, codeRange, function, cursor, &opcode);
            if (status != Status::Success)
                return status;
        }
        if (opcode != 0xC2 && opcode != 0xC3)
            return priorInstruction ? Status::UnsupportedEpilogueForm
                                     : Status::NoEpilogue;
    }

    if (opcode == 0xC3 || opcode == 0xC2) {
        *isTailJump = false;
        *returnAdjustment = 0;
        if (opcode == 0xC2) {
            const Status status = ReadCode16(image, codeRange, function,
                                             cursor, returnAdjustment);
            if (status != Status::Success)
                return status;
            if ((*returnAdjustment & 7u) != 0)
                return Status::UnsupportedEpilogueForm;
        }
        return Status::Success;
    }

    if (opcode != 0xFF)
        return Status::UnsupportedEpilogueForm;
    if (rex != 0 && (rex & 0x04u) != 0)
        return Status::UnsupportedEpilogueForm;

    std::uint8_t modrm = 0;
    Status status = ReadCode8(image, codeRange, function, cursor, &modrm);
    if (status != Status::Success)
        return status;
    const std::uint8_t mod = modrm >> 6;
    const std::uint8_t group = (modrm >> 3) & 7u;
    const std::uint8_t rm = modrm & 7u;
    if (group != 4)
        return priorInstruction ? Status::UnsupportedEpilogueForm
                                : Status::NoEpilogue;
    if (mod != 0)
        return Status::UnsupportedEpilogueForm;

    if (rm == 4) {
        std::uint8_t sib = 0;
        status = ReadCode8(image, codeRange, function, cursor, &sib);
        if (status != Status::Success)
            return status;
        if ((sib & 7u) == 5u) {
            std::uint32_t ignoredDisplacement = 0;
            status = ReadCode32(image, codeRange, function, cursor,
                                &ignoredDisplacement);
            if (status != Status::Success)
                return status;
        }
    } else if (rm == 5) {
        std::uint32_t ignoredDisplacement = 0;
        status = ReadCode32(image, codeRange, function, cursor,
                            &ignoredDisplacement);
        if (status != Status::Success)
            return status;
    }
    *isTailJump = true;
    *returnAdjustment = 0;
    return Status::Success;
}

inline Status ParseEpilogue(const ImageView& image,
                            const ExecutableCodeRange& codeRange,
                            const RuntimeFunction& function,
                            std::uint64_t controlPc,
                            const ParsedInfo& parsed,
                            EpilogueSequence* sequence,
                            bool* isTailJump) {
    if (!ValidCodeRange(image, codeRange, function))
        return Status::InvalidMetadata;
    if (controlPc < image.BaseAddress ||
        controlPc - image.BaseAddress < function.BeginAddress ||
        controlPc - image.BaseAddress >= function.EndAddress)
        return Status::InvalidContext;
    const std::uint64_t codeOffset = controlPc - image.BaseAddress - function.BeginAddress;
    if (codeOffset <= parsed.PrologSize)
        return Status::NoEpilogue;

    *sequence = {};
    CodeCursor cursor{static_cast<std::uint32_t>(controlPc - image.BaseAddress), 0};
    bool sawInstruction = false;
    for (std::uint8_t instruction = 0; instruction < 10; ++instruction) {
        std::uint8_t first = 0;
        Status status = ReadCode8(image, codeRange, function, &cursor, &first);
        if (status != Status::Success)
            return status;

        if (first == 0xF3 || first == 0xC2 || first == 0xC3) {
            std::uint16_t retAdjust = 0;
            status = ParseReturn(image, codeRange, function, &cursor, first, 0,
                                 sawInstruction, isTailJump, &retAdjust);
            if (status != Status::Success)
                return status;
            sequence->HasReturnAdjustment = retAdjust != 0;
            sequence->ReturnAdjustment = retAdjust;
            return Status::Success;
        }

        std::uint8_t rex = 0;
        std::uint8_t opcode = first;
        if (IsRex(first)) {
            rex = first;
            status = ReadCode8(image, codeRange, function, &cursor, &opcode);
            if (status != Status::Success)
                return status;
        }

        if (opcode == 0xC2 || opcode == 0xC3) {
            std::uint16_t retAdjust = 0;
            status = ParseReturn(image, codeRange, function, &cursor, opcode,
                                 rex, sawInstruction, isTailJump, &retAdjust);
            if (status != Status::Success)
                return sawInstruction ? status : Status::NoEpilogue;
            sequence->HasReturnAdjustment = retAdjust != 0;
            sequence->ReturnAdjustment = retAdjust;
            return Status::Success;
        }

        if (opcode == 0x83 || opcode == 0x81) {
            if (sequence->HasStackRestore || sequence->PopCount != 0)
                return Status::UnsupportedEpilogueForm;
            if (rex != 0x48)
                return Status::UnsupportedEpilogueForm;
            std::uint8_t modrm = 0;
            status = ReadCode8(image, codeRange, function, &cursor, &modrm);
            if (status != Status::Success)
                return status;
            if (modrm != 0xC4)
                return sawInstruction ? Status::UnsupportedEpilogueForm
                                      : Status::NoEpilogue;
            std::uint64_t amount = 0;
            if (opcode == 0x83) {
                std::uint8_t immediate = 0;
                status = ReadCode8(image, codeRange, function, &cursor, &immediate);
                if (status != Status::Success)
                    return status;
                if (immediate == 0 || immediate >= 0x80)
                    return Status::UnsupportedEpilogueForm;
                amount = immediate;
            } else {
                std::uint32_t immediate = 0;
                status = ReadCode32(image, codeRange, function, &cursor, &immediate);
                if (status != Status::Success)
                    return status;
                if (immediate == 0 || immediate > 0x7FFFFFFFu)
                    return Status::UnsupportedEpilogueForm;
                amount = immediate;
            }
            if ((amount & 7u) != 0)
                return Status::UnsupportedEpilogueForm;
            sequence->HasStackRestore = true;
            sequence->StackRestore = amount;
            sawInstruction = true;
            continue;
        }

        if (opcode == 0x8D) {
            if (sequence->HasStackRestore || sequence->PopCount != 0)
                return Status::UnsupportedEpilogueForm;
            if ((rex & 0x08u) == 0 || (rex & 0x06u) != 0)
                return sawInstruction ? Status::UnsupportedEpilogueForm
                                      : Status::NoEpilogue;
            std::uint8_t modrm = 0;
            status = ReadCode8(image, codeRange, function, &cursor, &modrm);
            if (status != Status::Success)
                return status;
            const std::uint8_t mod = modrm >> 6;
            const std::uint8_t destination = (modrm >> 3) & 7u;
            const std::uint8_t rm = modrm & 7u;
            const std::uint8_t base = static_cast<std::uint8_t>(
                rm | ((rex & 0x01u) ? 8u : 0u));
            if (destination != 4 || (mod != 1 && mod != 2) || rm == 4 ||
                base != parsed.FrameRegister || parsed.FrameRegister == 0)
                return sawInstruction ? Status::UnsupportedEpilogueForm
                                      : Status::NoEpilogue;
            std::int64_t displacement = 0;
            if (mod == 1) {
                std::uint8_t immediate = 0;
                status = ReadCode8(image, codeRange, function, &cursor, &immediate);
                if (status != Status::Success)
                    return status;
                displacement = (immediate & 0x80u) != 0
                    ? static_cast<std::int64_t>(immediate) - 0x100
                    : static_cast<std::int64_t>(immediate);
            } else {
                std::uint32_t immediate = 0;
                status = ReadCode32(image, codeRange, function, &cursor, &immediate);
                if (status != Status::Success)
                    return status;
                displacement = (immediate & 0x80000000u) != 0
                    ? static_cast<std::int64_t>(immediate) - 0x100000000ll
                    : static_cast<std::int64_t>(immediate);
            }
            sequence->HasStackRestore = true;
            sequence->UsesFrameRegister = true;
            sequence->FrameRegister = base;
            sequence->FrameDisplacement = displacement;
            sawInstruction = true;
            continue;
        }

        if (opcode >= 0x58 && opcode <= 0x5F) {
            if (rex != 0 && (rex & 0x06u) != 0)
                return Status::UnsupportedEpilogueForm;
            const std::uint8_t reg = static_cast<std::uint8_t>(
                opcode - 0x58u + ((rex & 0x01u) ? 8u : 0u));
            if (!IsNonvolatileIntegerRegister(reg) || sequence->PopCount >= 8)
                return Status::UnsupportedEpilogueForm;
            for (std::uint8_t index = 0; index < sequence->PopCount; ++index) {
                if (sequence->Pops[index] == reg)
                    return Status::UnsupportedEpilogueForm;
            }
            sequence->Pops[sequence->PopCount++] = reg;
            sawInstruction = true;
            continue;
        }

        if (opcode == 0xFF) {
            if (sequence->HasStackRestore && sequence->PopCount == 0 &&
                !sawInstruction)
                return Status::UnsupportedEpilogueForm;
            std::uint16_t ignoredAdjustment = 0;
            status = ParseReturn(image, codeRange, function, &cursor, opcode,
                                 rex, sawInstruction, isTailJump,
                                 &ignoredAdjustment);
            return status;
        }

        return sawInstruction ? Status::UnsupportedEpilogueForm
                              : Status::NoEpilogue;
    }
    return Status::UnsupportedEpilogueForm;
}

inline Status ValidateEpilogueMetadata(const ParsedInfo& parsed,
                                       const EpilogueSequence& sequence,
                                       const Context& context) {
    std::uint8_t expectedPops[8]{};
    std::uint8_t expectedPopCount = 0;
    std::uint64_t allocationSize = 0;
    for (std::uint16_t index = 0; index < parsed.OperationCount; ++index) {
        const Operation& operation = parsed.Operations[index];
        if (operation.Opcode == 0) {
            if (expectedPopCount >= 8)
                return Status::InvalidMetadata;
            expectedPops[expectedPopCount++] = operation.Info;
        } else if (operation.Opcode == 1) {
            const std::uint64_t amount = operation.Info == 0
                ? static_cast<std::uint64_t>(operation.Operand) * 8
                : operation.Operand;
            if (!Add(allocationSize, amount, &allocationSize))
                return Status::ArithmeticOverflow;
        } else if (operation.Opcode == 2) {
            if (!Add(allocationSize,
                     static_cast<std::uint64_t>(operation.Info + 1) * 8,
                     &allocationSize))
                return Status::ArithmeticOverflow;
        }
    }

    if (sequence.HasStackRestore) {
        if (sequence.UsesFrameRegister) {
            if (sequence.FrameRegister != parsed.FrameRegister)
                return Status::UnsupportedEpilogueForm;
            Context registerContext = context;
            const std::uint64_t* frame = IntegerRegister(
                &registerContext, parsed.FrameRegister);
            const std::uint64_t frameDisplacement =
                static_cast<std::uint64_t>(parsed.FrameOffset) * 16;
            if (frame == nullptr || *frame < frameDisplacement)
                return Status::ArithmeticOverflow;
            std::uint64_t expectedTarget = *frame - frameDisplacement;
            std::uint64_t allocationBeforeFrame = 0;
            for (std::uint16_t index = 0; index < parsed.OperationCount; ++index) {
                const Operation& operation = parsed.Operations[index];
                if (operation.CodeOffset >= parsed.FrameSetupOffset)
                    continue;
                std::uint64_t amount = 0;
                if (operation.Opcode == 1) {
                    amount = operation.Info == 0
                        ? static_cast<std::uint64_t>(operation.Operand) * 8
                        : operation.Operand;
                } else if (operation.Opcode == 2) {
                    amount = static_cast<std::uint64_t>(operation.Info + 1) * 8;
                }
                if (!Add(allocationBeforeFrame, amount, &allocationBeforeFrame))
                    return Status::ArithmeticOverflow;
            }
            if (!Add(expectedTarget, allocationBeforeFrame, &expectedTarget))
                return Status::ArithmeticOverflow;
            std::uint64_t actualTarget = 0;
            const Status displacementStatus = AddSigned(
                *frame, sequence.FrameDisplacement, &actualTarget);
            if (displacementStatus != Status::Success)
                return displacementStatus;
            if (actualTarget != expectedTarget)
                return Status::UnsupportedEpilogueForm;
        } else if (allocationSize == 0 || sequence.StackRestore != allocationSize) {
            return Status::UnsupportedEpilogueForm;
        }
    }
    if (sequence.PopCount > expectedPopCount)
        return Status::UnsupportedEpilogueForm;
    const std::uint8_t firstExpected = expectedPopCount - sequence.PopCount;
    for (std::uint8_t index = 0; index < sequence.PopCount; ++index) {
        if (sequence.Pops[index] != expectedPops[firstExpected + index])
            return Status::UnsupportedEpilogueForm;
    }
    return Status::Success;
}

inline Status UnwindEpilogue(const ImageView& image,
                             const ExecutableCodeRange& codeRange,
                             const StackView& stack,
                             const RuntimeFunction& function,
                             std::uint64_t controlPc,
                             const ParsedInfo& parsed,
                             Context* context,
                             ContextPointers* contextPointers,
                             UnwindResult* result) {
    EpilogueSequence sequence{};
    bool isTailJump = false;
    Status status = ParseEpilogue(image, codeRange, function, controlPc,
                                  parsed, &sequence, &isTailJump);
    if (status != Status::Success)
        return status;
    status = ValidateEpilogueMetadata(parsed, sequence, *context);
    if (status != Status::Success)
        return status;

    Context working = *context;
    ContextPointers workingPointers{};
    if (contextPointers != nullptr)
        workingPointers = *contextPointers;
    const std::uint64_t entryRsp = working.Rsp;

    if (sequence.HasStackRestore) {
        if (sequence.UsesFrameRegister) {
            const std::uint64_t* frame = IntegerRegister(
                &working, sequence.FrameRegister);
            if (frame == nullptr)
                return Status::InvalidContext;
            status = AddSigned(*frame, sequence.FrameDisplacement,
                               &working.Rsp);
            if (status != Status::Success)
                return status;
            if (!StackRange(stack, working.Rsp, 0))
                return Status::InvalidStackAccess;
        } else {
            status = AdvanceRsp(&working, stack, sequence.StackRestore);
            if (status != Status::Success)
                return status;
        }
    }

    for (std::uint8_t index = 0; index < sequence.PopCount; ++index) {
        status = RestoreInteger(&working, &workingPointers, stack,
                                sequence.Pops[index], working.Rsp);
        if (status != Status::Success)
            return status;
        status = AdvanceRsp(&working, stack, 8);
        if (status != Status::Success)
            return status;
    }

    const std::uint64_t establisherFrame = parsed.FrameRegister != 0
        ? working.Rsp : entryRsp;
    std::uint64_t returnAddress = 0;
    if (!ReadStack64(stack, working.Rsp, &returnAddress))
        return StackRange(stack, working.Rsp, 8)
            ? Status::MisalignedStack : Status::InvalidStackAccess;
    std::uint64_t returnStackAdjustment = 8;
    if (!isTailJump &&
        !Add(returnStackAdjustment, sequence.ReturnAdjustment,
             &returnStackAdjustment))
        return Status::ArithmeticOverflow;
    status = AdvanceRsp(&working, stack, returnStackAdjustment);
    if (status != Status::Success)
        return status;
    working.Rip = returnAddress;

    *context = working;
    if (contextPointers != nullptr)
        *contextPointers = workingPointers;
    result->EstablisherFrame = establisherFrame;
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
// This overload has no code bytes and therefore fails closed when its caller
// identifies an epilogue. Use the ExecutableCodeRange overload below when
// epilogue recognition and simulation are required.
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
        return Status::UnsupportedEpilogueForm;
    if (!detail::StackRange(stack, context->Rsp, 0))
        return Status::InvalidStackAccess;
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
            ? Status::MisalignedStack : Status::InvalidStackAccess;
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

// Bounded view of the NativeAOT manager's register state. The Context and
// ContextPointers members deliberately match the x64 CONTEXT and
// KNONVOLATILE_CONTEXT_POINTERS layouts consumed by CoffNativeCodeManager.
struct CodeManagerFrameContext {
    Context Registers;
    ContextPointers RegisterLocations;
};

// This overload validates and inspects code only inside the caller-supplied
// executable range and the resolved RUNTIME_FUNCTION interval. A complete
// supported epilogue is simulated; ordinary body instructions fall back to
// unwind metadata. Unsupported epilogue candidates and bounded-read failures
// retain their explicit failure status.
inline Status UnwindFrame(const ImageView& image,
                          const ExecutableCodeRange& codeRange,
                          const StackView& stack,
                          std::uint32_t functionEntryRva,
                          std::uint64_t controlPc,
                          Context* context,
                          ContextPointers* contextPointers,
                          UnwindResult* result) {
    if (context == nullptr)
        return Status::InvalidContext;
    if (result == nullptr || !detail::ValidImage(image) ||
        !detail::ValidStack(stack))
        return Status::InvalidArgument;
    if (context->Rip != controlPc)
        return Status::InvalidContext;
    if (functionEntryRva == NoRuntimeFunction) {
        return UnwindFrame(image, stack, functionEntryRva, controlPc, false,
                           context, contextPointers, result);
    }

    RuntimeFunction function{};
    Status status = detail::ReadRuntimeFunction(
        image, functionEntryRva, &function);
    if (status != Status::Success)
        return status;
    if (controlPc < image.BaseAddress ||
        controlPc - image.BaseAddress < function.BeginAddress ||
        controlPc - image.BaseAddress >= function.EndAddress)
        return Status::InvalidContext;
    if (codeRange.BeginRva >= codeRange.EndRva ||
        codeRange.EndRva > image.Size)
        return Status::OutOfRangeCodeAccess;
    if (!detail::ValidCodeRange(image, codeRange, function))
        return Status::InvalidMetadata;

    detail::ParsedInfo parsed{};
    status = detail::ParseInfo(image, function.UnwindData, &parsed);
    if (status != Status::Success)
        return status;
    status = detail::UnwindEpilogue(image, codeRange, stack, function,
                                    controlPc, parsed, context,
                                    contextPointers, result);
    if (status != Status::NoEpilogue)
        return status;
    return UnwindFrame(image, stack, functionEntryRva, controlPc, false,
                       context, contextPointers, result);
}

// Explicit-result adapter for code-manager-shaped state. It never changes the
// caller's register snapshot or source pointers when validation/unwinding
// fails; it does not alter the legacy RtlVirtualUnwind ABI.
inline Status UnwindCodeManagerFrame(const ImageView& image,
                                     const ExecutableCodeRange& codeRange,
                                     const StackView& stack,
                                     std::uint32_t functionEntryRva,
                                     CodeManagerFrameContext* frame,
                                     UnwindResult* result) {
    if (frame == nullptr)
        return Status::InvalidContext;
    CodeManagerFrameContext working = *frame;
    const Status status = UnwindFrame(
        image, codeRange, stack, functionEntryRva, working.Registers.Rip,
        &working.Registers, &working.RegisterLocations, result);
    if (status != Status::Success)
        return status;
    *frame = working;
    return Status::Success;
}

} // namespace guidexos::unwind
