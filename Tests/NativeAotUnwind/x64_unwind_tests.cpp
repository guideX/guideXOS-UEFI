#include "../../Tools/Phase23/x64_unwind_bounded.h"

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <functional>
#include <initializer_list>
#include <iostream>
#include <limits>
#include <string>

#if defined(_WIN32) && defined(GUIDEXOS_UNWIND_WINDOWS_REFERENCE)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#define GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE 1
static_assert(offsetof(CONTEXT, Rax) == offsetof(guidexos::unwind::Context, Rax),
              "test context RAX ABI mismatch");
static_assert(offsetof(CONTEXT, Rsp) == offsetof(guidexos::unwind::Context, Rsp),
              "test context RSP ABI mismatch");
static_assert(offsetof(CONTEXT, Rip) == offsetof(guidexos::unwind::Context, Rip),
              "test context RIP ABI mismatch");
static_assert(offsetof(CONTEXT, Xmm0) == offsetof(guidexos::unwind::Context, Xmm),
              "test context XMM ABI mismatch");
static_assert(sizeof(CONTEXT) == sizeof(guidexos::unwind::Context),
              "test context size mismatch");
static_assert(offsetof(KNONVOLATILE_CONTEXT_POINTERS, IntegerContext) ==
                  offsetof(guidexos::unwind::ContextPointers, IntegerContext),
              "test context-pointer ABI mismatch");
#else
#define GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE 0
#endif

using namespace guidexos::unwind;

namespace {

constexpr std::uint32_t kFunctionEntryRva = 0x100;
constexpr std::uint32_t kFunctionBeginRva = 0x200;
constexpr std::uint32_t kFunctionEndRva = 0x240;
constexpr std::uint32_t kUnwindInfoRva = 0x300;

struct Fixture {
    alignas(16) std::array<std::uint8_t, 0x1000> Image{};
    alignas(16) std::array<std::uint8_t, 0x100000> Stack{};

    std::uint64_t ImageBase() const {
        return reinterpret_cast<std::uintptr_t>(Image.data());
    }

    std::uint64_t StackBase() const {
        return reinterpret_cast<std::uintptr_t>(Stack.data());
    }

    ImageView ImageSpan() const {
        return {ImageBase(), Image.data(), Image.size()};
    }

    StackView StackSpan() {
        return {StackBase(), Stack.data(), Stack.size()};
    }

    void Put8(std::size_t offset, std::uint8_t value) {
        Image[offset] = value;
    }

    void PutCodeBytes(std::size_t offset,
                      std::initializer_list<std::uint8_t> bytes) {
        for (std::uint8_t byte : bytes)
            Put8(offset++, byte);
    }

    void Put16(std::size_t offset, std::uint16_t value) {
        Put8(offset, static_cast<std::uint8_t>(value));
        Put8(offset + 1, static_cast<std::uint8_t>(value >> 8));
    }

    void Put32(std::size_t offset, std::uint32_t value) {
        for (std::size_t index = 0; index < 4; ++index)
            Put8(offset + index, static_cast<std::uint8_t>(value >> (index * 8)));
    }

    void PutStack64(std::size_t offset, std::uint64_t value) {
        for (std::size_t index = 0; index < 8; ++index)
            Stack[offset + index] = static_cast<std::uint8_t>(value >> (index * 8));
    }

    void PutStackXmm(std::size_t offset, std::uint64_t low, std::uint64_t high) {
        PutStack64(offset, low);
        PutStack64(offset + 8, high);
    }

    void PutFunction(std::uint32_t entryRva, std::uint32_t beginRva,
                     std::uint32_t endRva, std::uint32_t unwindRva) {
        Put32(entryRva, beginRva);
        Put32(entryRva + 4, endRva);
        Put32(entryRva + 8, unwindRva);
    }

    void PutDefaultFunction(std::uint32_t codeSlots = 0,
                            std::uint8_t prologSize = 0,
                            std::uint8_t frame = 0,
                            std::uint8_t versionAndFlags = 1) {
        PutFunction(kFunctionEntryRva, kFunctionBeginRva,
                    kFunctionEndRva, kUnwindInfoRva);
        Put8(kUnwindInfoRva, versionAndFlags);
        Put8(kUnwindInfoRva + 1, prologSize);
        Put8(kUnwindInfoRva + 2, static_cast<std::uint8_t>(codeSlots));
        Put8(kUnwindInfoRva + 3, frame);
    }

    void PutCode(std::uint32_t unwindRva, std::uint8_t slot,
                 std::uint8_t codeOffset, std::uint8_t opcode,
                 std::uint8_t info) {
        const std::size_t offset = unwindRva + 4 + slot * 2;
        Put8(offset, codeOffset);
        Put8(offset + 1, static_cast<std::uint8_t>((info << 4) | opcode));
    }

    Context MakeContext(std::size_t stackOffset) const {
        Context context{};
        context.Rsp = StackBase() + stackOffset;
        context.Rip = ImageBase() + kFunctionBeginRva + 0x10;
        return context;
    }
};

int g_testCount = 0;
int g_failCount = 0;
int g_windowsReferenceCount = 0;

#define CHECK(condition)                                                        \
    do {                                                                         \
        if (!(condition)) {                                                      \
            std::cerr << "    assertion failed at line " << __LINE__ << ": "   \
                      << #condition << '\n';                                    \
            return false;                                                        \
        }                                                                        \
    } while (false)

void Run(const char* name, const std::function<bool()>& test) {
    ++g_testCount;
    if (test()) {
        std::cout << "PASS " << name << '\n';
    } else {
        ++g_failCount;
        std::cout << "FAIL " << name << '\n';
    }
}

Status Unwind(Fixture& fixture, std::uint32_t entryRva,
              std::uint64_t controlPc, Context* context,
              ContextPointers* pointers, UnwindResult* result,
              bool inEpilogue = false) {
    return UnwindFrame(fixture.ImageSpan(), fixture.StackSpan(), entryRva,
                       controlPc, inEpilogue, context, pointers, result);
}

#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
void SeedCodeManagerContextPointers(Context& context,
                                    ContextPointers& pointers) {
    constexpr std::uint8_t nonvolatileRegisters[] = {3, 5, 6, 7, 12, 13, 14, 15};
    for (std::uint8_t reg : nonvolatileRegisters)
        pointers.IntegerContext[reg] = detail::IntegerRegister(&context, reg);
}

bool CompareWindowsReference(Fixture& fixture, std::uint32_t beginRva,
                             std::uint32_t endRva, std::uint32_t unwindRva,
                             std::uint64_t controlPc,
                             const Context& original,
                             bool compareContextPointers = true) {
    Context actual = original;
    ContextPointers actualPointers{};
    SeedCodeManagerContextPointers(actual, actualPointers);
    UnwindResult actualResult{};
    const ExecutableCodeRange codeRange{beginRva, endRva};
    const Status status = UnwindFrame(
        fixture.ImageSpan(), codeRange, fixture.StackSpan(),
        kFunctionEntryRva, controlPc, &actual, &actualPointers, &actualResult);
    if (status != Status::Success) {
        std::cerr << "    bounded evaluator returned " << static_cast<int>(status)
                  << " during Windows comparison\n";
        return false;
    }

    RUNTIME_FUNCTION function{};
    function.BeginAddress = beginRva;
    function.EndAddress = endRva;
    function.UnwindData = unwindRva;
    CONTEXT reference{};
    reference.Rax = original.Rax;
    reference.Rcx = original.Rcx;
    reference.Rdx = original.Rdx;
    reference.Rbx = original.Rbx;
    reference.Rbp = original.Rbp;
    reference.Rsi = original.Rsi;
    reference.Rdi = original.Rdi;
    reference.R8 = original.R8;
    reference.R9 = original.R9;
    reference.R10 = original.R10;
    reference.R11 = original.R11;
    reference.R12 = original.R12;
    reference.R13 = original.R13;
    reference.R14 = original.R14;
    reference.R15 = original.R15;
    reference.Rsp = original.Rsp;
    reference.Rip = original.Rip;
    reference.ContextFlags = CONTEXT_FULL;
    for (std::size_t index = 0; index < 16; ++index) {
        reference.FltSave.XmmRegisters[index].Low = original.Xmm[index].Low;
        reference.FltSave.XmmRegisters[index].High = original.Xmm[index].High;
    }
    PVOID handlerData = nullptr;
    DWORD64 establisher = 0;
    KNONVOLATILE_CONTEXT_POINTERS referencePointers{};
    constexpr std::uint8_t nonvolatileRegisters[] = {3, 5, 6, 7, 12, 13, 14, 15};
    Context* referenceContext = reinterpret_cast<Context*>(&reference);
    for (std::uint8_t reg : nonvolatileRegisters) {
        referencePointers.IntegerContext[reg] = reinterpret_cast<PDWORD64>(
            detail::IntegerRegister(referenceContext, reg));
    }
    RtlVirtualUnwind(UNW_FLAG_NHANDLER, fixture.ImageBase(), controlPc,
                     &function, &reference, &handlerData, &establisher,
                     &referencePointers);
    ++g_windowsReferenceCount;

    const bool matches = actual.Rax == reference.Rax && actual.Rbx == reference.Rbx &&
                         actual.Rbp == reference.Rbp && actual.Rsp == reference.Rsp &&
                         actual.Rip == reference.Rip &&
                         actualResult.EstablisherFrame == establisher;
    if (!matches) {
        std::cerr << "    reference mismatch: RIP " << std::hex << actual.Rip
                  << "/" << reference.Rip << " RSP " << actual.Rsp << "/"
                  << reference.Rsp << " RBX " << actual.Rbx << "/" << reference.Rbx
                  << " RBP " << actual.Rbp << "/" << reference.Rbp
                  << " establisher " << actualResult.EstablisherFrame << "/"
                  << establisher << std::dec << '\n';
    }
    bool pointersMatch = true;
    for (std::size_t index = 0; index < 16; ++index) {
        const std::uint64_t* actualRegister = detail::IntegerRegister(
            &actual, static_cast<std::uint8_t>(index));
        const std::uint64_t* referenceRegister = detail::IntegerRegister(
            referenceContext, static_cast<std::uint8_t>(index));
        const bool actualIsContext = actualPointers.IntegerContext[index] == actualRegister;
        const bool referenceIsContext = reinterpret_cast<std::uint64_t*>(
            referencePointers.IntegerContext[index]) == referenceRegister;
        const bool integerMatches = actualIsContext || referenceIsContext
            ? actualIsContext && referenceIsContext
            : reinterpret_cast<void*>(actualPointers.IntegerContext[index]) ==
              reinterpret_cast<void*>(referencePointers.IntegerContext[index]);
        const bool floatingMatches = actualPointers.FloatingContext[index] ==
            reinterpret_cast<void*>(referencePointers.FloatingContext[index]);
        if (compareContextPointers && (!integerMatches || !floatingMatches)) {
            std::cerr << "    context pointer mismatch at register " << index
                      << ": integer " << actualPointers.IntegerContext[index] << "/"
                      << referencePointers.IntegerContext[index] << " floating "
                      << actualPointers.FloatingContext[index] << "/"
                      << referencePointers.FloatingContext[index] << '\n';
        }
        pointersMatch = pointersMatch && integerMatches && floatingMatches;
    }
    return matches && (!compareContextPointers || pointersMatch) &&
           actual.Rcx == reference.Rcx && actual.Rdx == reference.Rdx &&
           actual.Rsi == reference.Rsi && actual.Rdi == reference.Rdi &&
           actual.R8 == reference.R8 && actual.R9 == reference.R9 &&
           actual.R10 == reference.R10 && actual.R11 == reference.R11 &&
           actual.R12 == reference.R12 && actual.R13 == reference.R13 &&
           actual.R14 == reference.R14 && actual.R15 == reference.R15 &&
           actual.Xmm[6].Low == reference.FltSave.XmmRegisters[6].Low &&
           actual.Xmm[6].High == static_cast<std::uint64_t>(
               reference.FltSave.XmmRegisters[6].High) &&
           actual.Xmm[15].Low == reference.FltSave.XmmRegisters[15].Low &&
           actual.Xmm[15].High == static_cast<std::uint64_t>(
               reference.FltSave.XmmRegisters[15].High);
}

bool WindowsFarSavePointerSetupBehavior(Fixture& fixture,
                                        std::uint32_t beginRva,
                                        std::uint32_t endRva,
                                        std::uint32_t unwindRva,
                                        const Context& original,
                                        std::size_t savedSlotOffset,
                                        std::uint64_t expectedR13) {
    RUNTIME_FUNCTION function{};
    function.BeginAddress = beginRva;
    function.EndAddress = endRva;
    function.UnwindData = unwindRva;
    CONTEXT emptyReference{};
    emptyReference.Rax = original.Rax;
    emptyReference.Rbx = original.Rbx;
    emptyReference.Rbp = original.Rbp;
    emptyReference.Rsi = original.Rsi;
    emptyReference.Rdi = original.Rdi;
    emptyReference.R12 = original.R12;
    emptyReference.R13 = original.R13;
    emptyReference.R14 = original.R14;
    emptyReference.R15 = original.R15;
    emptyReference.Rsp = original.Rsp;
    emptyReference.Rip = original.Rip;
    emptyReference.ContextFlags = CONTEXT_FULL;
    KNONVOLATILE_CONTEXT_POINTERS emptyPointers{};
    PVOID emptyHandlerData = nullptr;
    DWORD64 emptyEstablisher = 0;
    RtlVirtualUnwind(UNW_FLAG_NHANDLER, fixture.ImageBase(), original.Rip,
                     &function, &emptyReference, &emptyHandlerData,
                     &emptyEstablisher, &emptyPointers);
    ++g_windowsReferenceCount;

    CONTEXT reference{};
    reference.Rax = original.Rax;
    reference.Rbx = original.Rbx;
    reference.Rbp = original.Rbp;
    reference.Rsi = original.Rsi;
    reference.Rdi = original.Rdi;
    reference.R12 = original.R12;
    reference.R13 = original.R13;
    reference.R14 = original.R14;
    reference.R15 = original.R15;
    reference.Rsp = original.Rsp;
    reference.Rip = original.Rip;
    reference.ContextFlags = CONTEXT_FULL;
    KNONVOLATILE_CONTEXT_POINTERS pointers{};
    pointers.IntegerContext[13] = &reference.R13;
    PVOID handlerData = nullptr;
    DWORD64 establisher = 0;
    RtlVirtualUnwind(UNW_FLAG_NHANDLER, fixture.ImageBase(), original.Rip,
                     &function, &reference, &handlerData, &establisher,
                     &pointers);
    ++g_windowsReferenceCount;
    return emptyReference.R13 == expectedR13 &&
           emptyPointers.IntegerContext[13] == nullptr &&
           reference.R13 == expectedR13 &&
           pointers.IntegerContext[13] == &reference.R13 &&
           pointers.IntegerContext[13] != reinterpret_cast<PDWORD64>(
               fixture.Stack.data() + savedSlotOffset);
}
#endif

} // namespace

int main() {
    Run("context and context-pointer ABI layout", [] {
        CHECK(offsetof(Context, Rax) == 0x78);
        CHECK(offsetof(Context, Rip) == 0xF8);
        CHECK(offsetof(Context, Xmm) == 0x1A0);
        CHECK(sizeof(Context) == 0x4D0);
        CHECK(sizeof(ContextPointers) == 0x100);
        CHECK(offsetof(ContextPointers, IntegerContext) == 0x80);
        return true;
    });

    Run("leaf function pops caller return address", [] {
        Fixture fixture;
        const std::size_t sp = 0x100;
        const std::uint64_t returnPc = 0x123456789ABCDEF0ull;
        fixture.PutStack64(sp, returnPc);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, NoRuntimeFunction, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.Rip == returnPc);
        CHECK(context.Rsp == fixture.StackBase() + sp + 8);
        CHECK(result.EstablisherFrame == fixture.StackBase() + sp);
        return true;
    });

    Run("push nonvolatile restores RBX and its stack location", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(1, 1);
        fixture.PutCode(kUnwindInfoRva, 0, 1, 0, 3);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0xBADC0FFEE0DDF00Dull);
        fixture.PutStack64(sp + 8, 0x140001234ull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.Rbx == 0xBADC0FFEE0DDF00Dull);
        CHECK(context.Rip == 0x140001234ull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 16);
        CHECK(result.EstablisherFrame == fixture.StackBase() + sp);
        CHECK(pointers.IntegerContext[3] ==
              reinterpret_cast<std::uint64_t*>(fixture.Stack.data() + sp));
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + kFunctionBeginRva + 0x10;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva,
              fixture.ImageBase() + kFunctionBeginRva + 0x10, original));
#endif
        return true;
    });

    Run("UWOP_ALLOC_SMALL recovers stack allocation", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(1, 4);
        fixture.PutCode(kUnwindInfoRva, 0, 4, 2, 3); // 32 bytes
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 32, 0x140005678ull);
        Context context = fixture.MakeContext(sp);
        context.Rip = fixture.ImageBase() + kFunctionBeginRva + 4;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.Rsp == fixture.StackBase() + sp + 40);
        CHECK(context.Rip == 0x140005678ull);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + kFunctionBeginRva + 0x10;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("canonical far save beyond scaled unwind offset", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(6, 8);
        fixture.PutCode(kUnwindInfoRva, 0, 5, 5, 13); // R13 at RSP + 0x80000
        fixture.Put32(kUnwindInfoRva + 6, 0x80000);
        fixture.PutCode(kUnwindInfoRva, 3, 1, 1, 1); // allocation 0x80020
        fixture.Put32(kUnwindInfoRva + 12, 0x80020);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 0x80000, 0x9988776655443322ull);
        fixture.PutStack64(sp + 0x80020, 0x14000ABD1ull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.R13 == 0x9988776655443322ull);
        CHECK(pointers.IntegerContext[13] == reinterpret_cast<std::uint64_t*>(
              fixture.Stack.data() + sp + 0x80000));
        CHECK(*pointers.IntegerContext[13] == context.R13);
        CHECK(context.Rsp == fixture.StackBase() + sp + 0x80028);
        CHECK(context.Rip == 0x14000ABD1ull);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + kFunctionBeginRva + 0x10;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original, false));
        CHECK(WindowsFarSavePointerSetupBehavior(
              fixture, kFunctionBeginRva, kFunctionEndRva, kUnwindInfoRva,
              original, sp + 0x80000, 0x9988776655443322ull));
#endif
        return true;
    });

    Run("UWOP_ALLOC_LARGE scaled form", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(2, 4);
        fixture.PutCode(kUnwindInfoRva, 0, 4, 1, 0);
        fixture.Put16(kUnwindInfoRva + 6, 4); // 32 bytes
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 32, 0x140005679ull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.Rsp == fixture.StackBase() + sp + 40);
        CHECK(context.Rip == 0x140005679ull);
        return true;
    });

    Run("UWOP_ALLOC_LARGE unscaled form", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(3, 4);
        fixture.PutCode(kUnwindInfoRva, 0, 4, 1, 1);
        fixture.Put32(kUnwindInfoRva + 6, 32);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 32, 0x14000567Aull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.Rsp == fixture.StackBase() + sp + 40);
        CHECK(context.Rip == 0x14000567Aull);
        return true;
    });

    Run("frame-register and saved RBP unwind", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(3, 5, 0x25); // RBP, frame offset 2
        fixture.PutCode(kUnwindInfoRva, 0, 5, 3, 0); // set frame register
        fixture.PutCode(kUnwindInfoRva, 1, 4, 2, 3); // allocate 32 bytes
        fixture.PutCode(kUnwindInfoRva, 2, 1, 0, 5); // push RBP
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 32, 0x76543210ull);
        fixture.PutStack64(sp + 40, 0x14000ABCDull);
        Context context = fixture.MakeContext(sp);
        context.Rbp = fixture.StackBase() + sp + 32;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva,
                     fixture.ImageBase() + kFunctionBeginRva + 5,
                     &context, &pointers, &result) == Status::Success);
        CHECK(context.Rbp == 0x76543210ull);
        CHECK(context.Rip == 0x14000ABCDull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 48);
        CHECK(result.EstablisherFrame == fixture.StackBase() + sp);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rbp = fixture.StackBase() + sp + 32;
        original.Rip = fixture.ImageBase() + kFunctionBeginRva + 0x10;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("frame-register unwind recovers dynamic stack allocation", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(4, 8, 0x25);
        fixture.PutCode(kUnwindInfoRva, 0, 8, 4, 12); // R12 at fixed RSP + 0x38
        fixture.Put16(kUnwindInfoRva + 6, 7);
        fixture.PutCode(kUnwindInfoRva, 2, 4, 3, 0); // set RBP
        fixture.PutCode(kUnwindInfoRva, 3, 2, 2, 7); // allocate 64 bytes
        const std::size_t sp = 0x100; // current RSP is 32 bytes below fixed RSP
        fixture.PutStack64(sp + 0x58, 0xAABBCCDDEEFF0011ull);
        fixture.PutStack64(sp + 0x60, 0x14000ABDDull);
        Context context = fixture.MakeContext(sp);
        context.Rbp = fixture.StackBase() + sp + 0x40;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva,
              fixture.ImageBase() + kFunctionBeginRva + 0x10,
              &context, &pointers, &result) == Status::Success);
        CHECK(context.R12 == 0xAABBCCDDEEFF0011ull);
        CHECK(pointers.IntegerContext[12] ==
              reinterpret_cast<std::uint64_t*>(fixture.Stack.data() + sp + 0x58));
        CHECK(context.Rsp == fixture.StackBase() + sp + 0x68);
        CHECK(context.Rip == 0x14000ABDDull);
        CHECK(result.EstablisherFrame == fixture.StackBase() + sp + 0x20);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rbp = fixture.StackBase() + sp + 0x40;
        original.Rip = fixture.ImageBase() + kFunctionBeginRva + 0x10;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("saved nonvolatile register uses final stack position", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(3, 8);
        fixture.PutCode(kUnwindInfoRva, 0, 5, 4, 12); // save R12 at RSP + 16
        fixture.Put16(kUnwindInfoRva + 6, 2);
        fixture.PutCode(kUnwindInfoRva, 2, 1, 2, 5); // allocation 48
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 16, 0x1122334455667788ull);
        fixture.PutStack64(sp + 48, 0x14000ABCEull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.R12 == 0x1122334455667788ull);
        CHECK(pointers.IntegerContext[12] ==
              reinterpret_cast<std::uint64_t*>(fixture.Stack.data() + sp + 16));
        CHECK(context.Rsp == fixture.StackBase() + sp + 56);
        CHECK(context.Rip == 0x14000ABCEull);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + kFunctionBeginRva + 0x10;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("far saved nonvolatile register", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(6, 8);
        fixture.PutCode(kUnwindInfoRva, 0, 5, 5, 13); // R13 at RSP + 0x120
        fixture.Put32(kUnwindInfoRva + 6, 0x120);
        fixture.PutCode(kUnwindInfoRva, 3, 1, 1, 1); // allocation 0x140
        fixture.Put32(kUnwindInfoRva + 12, 0x140);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 0x120, 0x8877665544332211ull);
        fixture.PutStack64(sp + 0x140, 0x14000ABCFull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.R13 == 0x8877665544332211ull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 0x148);
        CHECK(context.Rip == 0x14000ABCFull);
        CHECK(pointers.IntegerContext[13] ==
              reinterpret_cast<std::uint64_t*>(fixture.Stack.data() + sp + 0x120));
        CHECK(*pointers.IntegerContext[13] == context.R13);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + kFunctionBeginRva + 0x10;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original, false));
        CHECK(WindowsFarSavePointerSetupBehavior(
              fixture, kFunctionBeginRva, kFunctionEndRva, kUnwindInfoRva,
              original, sp + 0x120, 0x8877665544332211ull));
#endif
        return true;
    });

    Run("saved XMM6 restores both halves and stack location", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(3, 8);
        fixture.PutCode(kUnwindInfoRva, 0, 5, 8, 6); // XMM6 at RSP + 16
        fixture.Put16(kUnwindInfoRva + 6, 1);
        fixture.PutCode(kUnwindInfoRva, 2, 1, 2, 5); // allocation 48
        const std::size_t sp = 0x100;
        fixture.PutStackXmm(sp + 16, 0x0102030405060708ull,
                            0x1112131415161718ull);
        fixture.PutStack64(sp + 48, 0x14000ABD0ull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.Xmm[6].Low == 0x0102030405060708ull);
        CHECK(context.Xmm[6].High == 0x1112131415161718ull);
        CHECK(pointers.FloatingContext[6] == fixture.Stack.data() + sp + 16);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + kFunctionBeginRva + 0x10;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("far saved XMM6 restores full vector from bounded stack", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(4, 5);
        fixture.PutCode(kUnwindInfoRva, 0, 5, 9, 6);
        fixture.Put32(kUnwindInfoRva + 6, 0x70);
        fixture.PutCode(kUnwindInfoRva, 3, 1, 2, 15); // allocation 128
        const std::size_t sp = 0x100;
        fixture.PutStackXmm(sp + 0x70, 0xABCDEF0123456789ull,
                            0x9988776655443322ull);
        fixture.PutStack64(sp + 0x80, 0x14000ABDEull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva,
              fixture.ImageBase() + kFunctionBeginRva + 0x10,
              &context, &pointers, &result) == Status::Success);
        CHECK(context.Xmm[6].Low == 0xABCDEF0123456789ull);
        CHECK(context.Xmm[6].High == 0x9988776655443322ull);
        CHECK(pointers.FloatingContext[6] == fixture.Stack.data() + sp + 0x70);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + kFunctionBeginRva + 0x10;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("nested managed frames unwind in sequence", [] {
        Fixture fixture;
        const std::uint32_t outerInfo = 0x300;
        const std::uint32_t innerInfo = 0x320;
        const std::uint32_t outerEntry = 0x100;
        const std::uint32_t innerEntry = 0x10C;
        fixture.PutFunction(outerEntry, 0x200, 0x220, outerInfo);
        fixture.Put8(outerInfo, 1);
        fixture.Put8(outerInfo + 1, 1);
        fixture.Put8(outerInfo + 2, 1);
        fixture.Put8(outerInfo + 3, 0);
        fixture.PutCode(outerInfo, 0, 1, 0, 3);
        fixture.PutFunction(innerEntry, 0x220, 0x240, innerInfo);
        fixture.Put8(innerInfo, 1);
        fixture.Put8(innerInfo + 1, 4);
        fixture.Put8(innerInfo + 2, 1);
        fixture.Put8(innerInfo + 3, 0);
        fixture.PutCode(innerInfo, 0, 4, 2, 1); // allocate 16 bytes
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 16, fixture.ImageBase() + 0x210);
        fixture.PutStack64(sp + 24, 0xCAFEBABEull);
        fixture.PutStack64(sp + 32, 0x140001999ull);
        RuntimeFunction function{};
        std::uint32_t entryRva = 0;
        CHECK(FindRuntimeFunction(fixture.ImageSpan(), 0x100, 24,
              fixture.ImageBase() + 0x225, &function, &entryRva) == Status::Success);
        CHECK(entryRva == innerEntry);
        Context context = fixture.MakeContext(sp);
        context.Rip = fixture.ImageBase() + 0x225;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, entryRva, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.Rip == fixture.ImageBase() + 0x210);
        CHECK(context.Rsp == fixture.StackBase() + sp + 24);
        CHECK(FindRuntimeFunction(fixture.ImageSpan(), 0x100, 24,
              context.Rip, &function, &entryRva) == Status::Success);
        CHECK(entryRva == outerEntry);
        CHECK(Unwind(fixture, entryRva, context.Rip, &context,
                     &pointers, &result) == Status::Success);
        CHECK(context.Rbx == 0xCAFEBABEull);
        CHECK(context.Rip == 0x140001999ull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 40);
        return true;
    });

    Run("prologue start boundary applies no operations", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(2, 5);
        fixture.PutCode(kUnwindInfoRva, 0, 5, 2, 1); // allocate 16
        fixture.PutCode(kUnwindInfoRva, 1, 1, 0, 3); // push RBX
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x140008888ull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva,
              fixture.ImageBase() + kFunctionBeginRva,
              &context, &pointers, &result) == Status::Success);
        CHECK(context.Rbx == 0);
        CHECK(context.Rsp == fixture.StackBase() + sp + 8);
        CHECK(context.Rip == 0x140008888ull);
        return true;
    });

    Run("prologue operation boundary applies only executed push", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(2, 5);
        fixture.PutCode(kUnwindInfoRva, 0, 5, 2, 1); // allocate 16
        fixture.PutCode(kUnwindInfoRva, 1, 1, 0, 3); // push RBX
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0xABCDEF00ull);
        fixture.PutStack64(sp + 8, 0x140008889ull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva,
              fixture.ImageBase() + kFunctionBeginRva + 1,
              &context, &pointers, &result) == Status::Success);
        CHECK(context.Rbx == 0xABCDEF00ull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 16);
        CHECK(context.Rip == 0x140008889ull);
        return true;
    });

    Run("prologue end boundary applies all completed operations", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(2, 5);
        fixture.PutCode(kUnwindInfoRva, 0, 5, 2, 1); // allocate 16
        fixture.PutCode(kUnwindInfoRva, 1, 1, 0, 3); // push RBX
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 16, 0xABCD1234ull);
        fixture.PutStack64(sp + 24, 0x14000888Aull);
        Context context = fixture.MakeContext(sp);
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva,
              fixture.ImageBase() + kFunctionBeginRva + 5,
              &context, &pointers, &result) == Status::Success);
        CHECK(context.Rbx == 0xABCD1234ull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 32);
        CHECK(context.Rip == 0x14000888Aull);
        return true;
    });

    Run("known epilogue location fails closed without context mutation", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(1, 1);
        fixture.PutCode(kUnwindInfoRva, 0, 1, 0, 3);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0xCAFEull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        ContextPointers pointersBefore = pointers;
        UnwindResult result{0x1111};
        CHECK(Unwind(fixture, kFunctionEntryRva,
              fixture.ImageBase() + kFunctionBeginRva + 0x10,
              &context, &pointers, &result, true) == Status::UnsupportedEpilogue);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        CHECK(std::memcmp(&pointers, &pointersBefore, sizeof(pointers)) == 0);
        CHECK(result.EstablisherFrame == 0x1111);
        return true;
    });

    Run("compiler-shaped epilogue restores allocation and nonvolatile pops", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(8, 16);
        fixture.PutCode(kUnwindInfoRva, 0, 16, 2, 3); // allocate 32
        fixture.PutCode(kUnwindInfoRva, 1, 14, 0, 15);
        fixture.PutCode(kUnwindInfoRva, 2, 12, 0, 14);
        fixture.PutCode(kUnwindInfoRva, 3, 10, 0, 13);
        fixture.PutCode(kUnwindInfoRva, 4, 8, 0, 12);
        fixture.PutCode(kUnwindInfoRva, 5, 6, 0, 7);  // RDI
        fixture.PutCode(kUnwindInfoRva, 6, 4, 0, 6);  // RSI
        fixture.PutCode(kUnwindInfoRva, 7, 2, 0, 5);  // RBP
        fixture.PutCodeBytes(0x220, {
            0x48, 0x83, 0xC4, 0x20, // add rsp, 32
            0x41, 0x5F,             // pop r15
            0x41, 0x5E,             // pop r14
            0x41, 0x5D,             // pop r13
            0x41, 0x5C,             // pop r12
            0x5F,                   // pop rdi
            0x5E,                   // pop rsi
            0x5D,                   // pop rbp
            0xC3                    // ret
        });
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 0x20, 0xF150000000000015ull);
        fixture.PutStack64(sp + 0x28, 0xF140000000000014ull);
        fixture.PutStack64(sp + 0x30, 0xF130000000000013ull);
        fixture.PutStack64(sp + 0x38, 0xF120000000000012ull);
        fixture.PutStack64(sp + 0x40, 0xF070000000000007ull);
        fixture.PutStack64(sp + 0x48, 0xF060000000000006ull);
        fixture.PutStack64(sp + 0x50, 0xF050000000000005ull);
        fixture.PutStack64(sp + 0x58, 0x140012345ull);
        Context context = fixture.MakeContext(sp);
        context.Rip = fixture.ImageBase() + 0x220;
        ContextPointers pointers{};
        UnwindResult result{};
        const ExecutableCodeRange codeRange{0x200, 0x240};
        CHECK(UnwindFrame(fixture.ImageSpan(), codeRange, fixture.StackSpan(),
              kFunctionEntryRva, context.Rip, &context, &pointers, &result) ==
              Status::Success);
        CHECK(context.R15 == 0xF150000000000015ull);
        CHECK(context.R14 == 0xF140000000000014ull);
        CHECK(context.R13 == 0xF130000000000013ull);
        CHECK(context.R12 == 0xF120000000000012ull);
        CHECK(context.Rdi == 0xF070000000000007ull);
        CHECK(context.Rsi == 0xF060000000000006ull);
        CHECK(context.Rbp == 0xF050000000000005ull);
        CHECK(context.Rip == 0x140012345ull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 0x60);
        CHECK(pointers.IntegerContext[15] == reinterpret_cast<std::uint64_t*>(
              fixture.Stack.data() + sp + 0x20));
        CHECK(pointers.IntegerContext[14] == reinterpret_cast<std::uint64_t*>(
              fixture.Stack.data() + sp + 0x28));
        CHECK(pointers.IntegerContext[13] == reinterpret_cast<std::uint64_t*>(
              fixture.Stack.data() + sp + 0x30));
        CHECK(pointers.IntegerContext[12] == reinterpret_cast<std::uint64_t*>(
              fixture.Stack.data() + sp + 0x38));
        CHECK(pointers.IntegerContext[7] == reinterpret_cast<std::uint64_t*>(
              fixture.Stack.data() + sp + 0x40));
        CHECK(pointers.IntegerContext[6] == reinterpret_cast<std::uint64_t*>(
              fixture.Stack.data() + sp + 0x48));
        CHECK(pointers.IntegerContext[5] == reinterpret_cast<std::uint64_t*>(
              fixture.Stack.data() + sp + 0x50));
        CHECK(result.EstablisherFrame == fixture.StackBase() + sp);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + 0x220;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("epilogue beginning at an intermediate pop", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(5, 8);
        fixture.PutCode(kUnwindInfoRva, 0, 8, 2, 3);
        fixture.PutCode(kUnwindInfoRva, 1, 6, 0, 15);
        fixture.PutCode(kUnwindInfoRva, 2, 4, 0, 14);
        fixture.PutCode(kUnwindInfoRva, 3, 2, 0, 13);
        fixture.PutCode(kUnwindInfoRva, 4, 1, 0, 12);
        fixture.PutCodeBytes(0x220, {
            0x48, 0x83, 0xC4, 0x20, 0x41, 0x5F, 0x41, 0x5E,
            0x41, 0x5D, 0x41, 0x5C, 0xC3
        });
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 0x20, 0xF150000000000015ull);
        fixture.PutStack64(sp + 0x28, 0xF140000000000014ull);
        fixture.PutStack64(sp + 0x30, 0xF130000000000013ull);
        fixture.PutStack64(sp + 0x38, 0xF120000000000012ull);
        fixture.PutStack64(sp + 0x40, 0x140012346ull);
        Context context = fixture.MakeContext(sp + 0x30);
        context.Rip = fixture.ImageBase() + 0x228; // pop r13; R15 and R14 ran
        context.R15 = 0xF150000000000015ull;
        context.R14 = 0xF140000000000014ull;
        ContextPointers pointers{};
        pointers.IntegerContext[15] = &context.R15;
        pointers.IntegerContext[14] = &context.R14;
        UnwindResult result{};
        const ExecutableCodeRange codeRange{0x200, 0x240};
        CHECK(UnwindFrame(fixture.ImageSpan(), codeRange, fixture.StackSpan(),
              kFunctionEntryRva, context.Rip, &context, &pointers, &result) ==
              Status::Success);
        CHECK(context.R15 == 0xF150000000000015ull);
        CHECK(context.R14 == 0xF140000000000014ull);
        CHECK(context.R13 == 0xF130000000000013ull);
        CHECK(context.R12 == 0xF120000000000012ull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 0x48);
        CHECK(context.Rip == 0x140012346ull);
        CHECK(pointers.IntegerContext[15] == &context.R15);
        CHECK(pointers.IntegerContext[14] == &context.R14);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp + 0x30);
        original.Rip = fixture.ImageBase() + 0x228;
        original.R15 = 0xF150000000000015ull;
        original.R14 = 0xF140000000000014ull;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("frame-pointer epilogue restores stack with LEA", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(3, 5, 0x25);
        fixture.PutCode(kUnwindInfoRva, 0, 5, 3, 0); // set RBP
        fixture.PutCode(kUnwindInfoRva, 1, 3, 2, 3); // allocate 32
        fixture.PutCode(kUnwindInfoRva, 2, 1, 0, 5); // push RBP
        fixture.PutCodeBytes(0x220, {
            0x48, 0x8D, 0x65, 0x00, // lea rsp, [rbp]
            0x5D,                   // pop rbp
            0xC3                    // ret
        });
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 0x20, 0xBEEFBEEFBEEFBEEFll);
        fixture.PutStack64(sp + 0x28, 0x140012347ull);
        Context context = fixture.MakeContext(sp - 0x80);
        context.Rbp = fixture.StackBase() + sp + 0x20;
        context.Rip = fixture.ImageBase() + 0x220;
        ContextPointers pointers{};
        UnwindResult result{};
        const ExecutableCodeRange codeRange{0x200, 0x240};
        CHECK(UnwindFrame(fixture.ImageSpan(), codeRange, fixture.StackSpan(),
              kFunctionEntryRva, context.Rip, &context, &pointers, &result) ==
              Status::Success);
        CHECK(context.Rbp == 0xBEEFBEEFBEEFBEEFll);
        CHECK(pointers.IntegerContext[5] == reinterpret_cast<std::uint64_t*>(
              fixture.Stack.data() + sp + 0x20));
        CHECK(context.Rip == 0x140012347ull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 0x30);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp - 0x80);
        original.Rbp = fixture.StackBase() + sp + 0x20;
        original.Rip = fixture.ImageBase() + 0x220;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("extended R13 frame register uses REX.B in LEA epilogue", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(3, 5, 0x2D); // R13, frame offset 2
        fixture.PutCode(kUnwindInfoRva, 0, 5, 3, 0);  // set frame register
        fixture.PutCode(kUnwindInfoRva, 1, 3, 2, 3);  // allocate 32
        fixture.PutCode(kUnwindInfoRva, 2, 1, 0, 13); // push R13
        fixture.PutCodeBytes(0x220, {
            0x49, 0x8D, 0x65, 0x00, // lea rsp, [r13]
            0x41, 0x5D,             // pop r13
            0xC3                    // ret
        });
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp - 8, 0xD130000000000013ull);
        fixture.PutStack64(sp, 0x14001234Aull);
        Context context = fixture.MakeContext(sp - 0x80);
        context.R13 = fixture.StackBase() + sp - 8;
        context.Rip = fixture.ImageBase() + 0x220;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240},
              fixture.StackSpan(), kFunctionEntryRva, context.Rip,
              &context, &pointers, &result) == Status::Success);
        CHECK(context.R13 == 0xD130000000000013ull);
        CHECK(pointers.IntegerContext[13] == reinterpret_cast<std::uint64_t*>(
              fixture.Stack.data() + sp - 8));
        CHECK(context.Rip == 0x14001234Aull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 8);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp - 0x80);
        original.R13 = fixture.StackBase() + sp - 8;
        original.Rip = fixture.ImageBase() + 0x220;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("REX.R LEA body instruction is not mistaken for stack restoration", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(3, 5, 0x25); // RBP, frame offset 2
        fixture.PutCode(kUnwindInfoRva, 0, 5, 3, 0); // set frame register
        fixture.PutCode(kUnwindInfoRva, 1, 3, 2, 3); // allocate 32
        fixture.PutCode(kUnwindInfoRva, 2, 1, 0, 5); // push RBP
        fixture.PutCodeBytes(0x220, {
            0x4C, 0x8D, 0x65, 0x00 // lea r12, [rbp], not lea rsp, [rbp]
        });
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp - 8, 0xBEEFBEEFBEEFBEEFll);
        fixture.PutStack64(sp, 0x14001234Bull);
        Context context = fixture.MakeContext(sp - 40);
        context.Rbp = fixture.StackBase() + sp - 8;
        context.Rip = fixture.ImageBase() + 0x220;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240},
              fixture.StackSpan(), kFunctionEntryRva, context.Rip,
              &context, &pointers, &result) == Status::Success);
        CHECK(context.Rbp == 0xBEEFBEEFBEEFBEEFll);
        CHECK(context.R12 == 0);
        CHECK(context.Rip == 0x14001234Bull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 8);
        return true;
    });

    Run("valid REP RET prefix and RET immediate", [] {
        {
            Fixture fixture;
            fixture.PutDefaultFunction();
            fixture.PutCodeBytes(0x220, {0xF3, 0xC3}); // rep ret
            const std::size_t sp = 0x100;
            fixture.PutStack64(sp, 0x140012348ull);
            Context context = fixture.MakeContext(sp);
            context.Rip = fixture.ImageBase() + 0x220;
            ContextPointers pointers{};
            UnwindResult result{};
            CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240},
                  fixture.StackSpan(), kFunctionEntryRva, context.Rip,
                  &context, &pointers, &result) == Status::Success);
            CHECK(context.Rip == 0x140012348ull);
            CHECK(context.Rsp == fixture.StackBase() + sp + 8);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
            Context original = fixture.MakeContext(sp);
            original.Rip = fixture.ImageBase() + 0x220;
            CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
                  kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        }
        {
            Fixture fixture;
            fixture.PutDefaultFunction();
            fixture.PutCodeBytes(0x220, {0xC2, 0x10, 0x00}); // ret 16
            const std::size_t sp = 0x100;
            fixture.PutStack64(sp, 0x140012349ull);
            Context context = fixture.MakeContext(sp);
            context.Rip = fixture.ImageBase() + 0x220;
            ContextPointers pointers{};
            UnwindResult result{};
            CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240},
                  fixture.StackSpan(), kFunctionEntryRva, context.Rip,
                  &context, &pointers, &result) == Status::Success);
            CHECK(context.Rip == 0x140012349ull);
            CHECK(context.Rsp == fixture.StackBase() + sp + 24);
        }
        return true;
    });

    Run("add RSP imm32 epilogue encoding", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(2, 7);
        fixture.PutCode(kUnwindInfoRva, 0, 7, 1, 0); // scaled allocation
        fixture.Put16(kUnwindInfoRva + 6, 19);       // 152 bytes
        fixture.PutCodeBytes(0x220, {
            0x48, 0x81, 0xC4, 0x98, 0x00, 0x00, 0x00, // add rsp, 0x98
            0xC3
        });
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 0x98, 0x14001234Eull);
        Context context = fixture.MakeContext(sp);
        context.Rip = fixture.ImageBase() + 0x220;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240},
              fixture.StackSpan(), kFunctionEntryRva, context.Rip,
              &context, &pointers, &result) == Status::Success);
        CHECK(context.Rip == 0x14001234Eull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 0xA0);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + 0x220;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("epilogue uses the supported indirect tail-jump form", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(1, 4);
        fixture.PutCode(kUnwindInfoRva, 0, 4, 2, 3); // allocate 32
        fixture.PutCodeBytes(0x220, {
            0x48, 0x83, 0xC4, 0x20, // add rsp, 32
            0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 // jmp qword ptr [rip]
        });
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 32, 0x14001234Aull);
        Context context = fixture.MakeContext(sp);
        context.Rip = fixture.ImageBase() + 0x220;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240},
              fixture.StackSpan(), kFunctionEntryRva, context.Rip,
              &context, &pointers, &result) == Status::Success);
        CHECK(context.Rip == 0x14001234Aull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 40);
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + 0x220;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original));
#endif
        return true;
    });

    Run("body instruction lookalike is not treated as an epilogue", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(1, 4);
        fixture.PutCode(kUnwindInfoRva, 0, 4, 2, 3); // allocate 32
        fixture.PutCodeBytes(0x220, {
            0x48, 0x83, 0xC0, 0x01, // add rax, 1
            0xC3
        });
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 32, 0x14001234Bull);
        Context context = fixture.MakeContext(sp);
        context.Rip = fixture.ImageBase() + 0x220;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240},
              fixture.StackSpan(), kFunctionEntryRva, context.Rip,
              &context, &pointers, &result) == Status::Success);
        CHECK(context.Rip == 0x14001234Bull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 40);
        return true;
    });

    Run("code-manager adapter commits successful register locations", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(1, 1);
        fixture.PutCode(kUnwindInfoRva, 0, 1, 0, 12); // push R12
        fixture.PutCodeBytes(0x220, {0x41, 0x5C, 0xC3});
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x1212121212121212ull);
        fixture.PutStack64(sp + 8, 0x14001234BULL);
        CodeManagerFrameContext frame{};
        frame.Registers = fixture.MakeContext(sp);
        frame.Registers.Rip = fixture.ImageBase() + 0x220;
        frame.RegisterLocations.IntegerContext[12] = &frame.Registers.R12;
        UnwindResult result{};
        CHECK(UnwindCodeManagerFrame(fixture.ImageSpan(), {0x200, 0x240},
              fixture.StackSpan(), kFunctionEntryRva, &frame, &result) ==
              Status::Success);
        CHECK(frame.Registers.R12 == 0x1212121212121212ull);
        CHECK(frame.RegisterLocations.IntegerContext[12] ==
              reinterpret_cast<std::uint64_t*>(fixture.Stack.data() + sp));
        CHECK(frame.Registers.Rsp == fixture.StackBase() + sp + 16);
        CHECK(frame.Registers.Rip == 0x14001234BULL);
        return true;
    });

    Run("unsupported epilogue and adapter failure propagate atomically", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(2, 5);
        fixture.PutCode(kUnwindInfoRva, 0, 5, 2, 3); // allocate 32
        fixture.PutCode(kUnwindInfoRva, 1, 2, 0, 12); // push R12
        fixture.PutCodeBytes(0x220, {
            0x48, 0x83, 0xC4, 0x20,
            0x41, 0x5C,
            0x90,                   // illegal instruction in epilogue suffix
            0xC3
        });
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 32, 0x1212121212121212ull);
        fixture.PutStack64(sp + 40, 0x14001234Cull);
        CodeManagerFrameContext frame{};
        frame.Registers = fixture.MakeContext(sp);
        frame.Registers.Rip = fixture.ImageBase() + 0x220;
        frame.RegisterLocations.IntegerContext[12] = &frame.Registers.R12;
        CodeManagerFrameContext before = frame;
        UnwindResult result{0x5555};
        CHECK(UnwindCodeManagerFrame(fixture.ImageSpan(), {0x200, 0x240},
              fixture.StackSpan(), kFunctionEntryRva, &frame, &result) ==
              Status::UnsupportedEpilogueForm);
        CHECK(std::memcmp(&frame, &before, sizeof(frame)) == 0);
        CHECK(result.EstablisherFrame == 0x5555);

        fixture.PutCodeBytes(0x220, {0xFF, 0x60, 0x08}); // jmp [rax+8], ModRM mod=01
        Context context = fixture.MakeContext(sp);
        context.Rip = fixture.ImageBase() + 0x220;
        Context beforeContext = context;
        ContextPointers pointers{};
        ContextPointers beforePointers = pointers;
        result.EstablisherFrame = 0x6666;
        CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240},
              fixture.StackSpan(), kFunctionEntryRva, context.Rip,
              &context, &pointers, &result) == Status::UnsupportedEpilogueForm);
        CHECK(std::memcmp(&context, &beforeContext, sizeof(context)) == 0);
        CHECK(std::memcmp(&pointers, &beforePointers, sizeof(pointers)) == 0);
        CHECK(result.EstablisherFrame == 0x6666);
        return true;
    });

    Run("truncated epilogue code and function-boundary reads fail closed", [] {
        const auto checkBoundary = [](std::uint32_t functionEnd,
                                      std::uint32_t codeEnd,
                                      std::initializer_list<std::uint8_t> bytes) {
            Fixture fixture;
            fixture.PutDefaultFunction(1, 4);
            fixture.PutCode(kUnwindInfoRva, 0, 4, 2, 3);
            fixture.PutFunction(kFunctionEntryRva, kFunctionBeginRva,
                                functionEnd, kUnwindInfoRva);
            fixture.PutCodeBytes(0x220, bytes);
            Context context = fixture.MakeContext(0x100);
            context.Rip = fixture.ImageBase() + 0x220;
            Context before = context;
            ContextPointers pointers{};
            UnwindResult result{0x7777};
            CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, codeEnd},
                  fixture.StackSpan(), kFunctionEntryRva, context.Rip,
                  &context, &pointers, &result) == Status::OutOfRangeCodeAccess);
            CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
            CHECK(result.EstablisherFrame == 0x7777);
            return true;
        };
        CHECK(checkBoundary(0x223, 0x223, {0x48, 0x83, 0xC4}));
        CHECK(checkBoundary(0x224, 0x240, {0x48, 0x83, 0xC4, 0x20}));
        return true;
    });

    Run("epilogue stack bounds, arithmetic, and context failures stay explicit", [] {
        {
            Fixture fixture;
            fixture.PutDefaultFunction(1, 4);
            fixture.PutCode(kUnwindInfoRva, 0, 4, 2, 3);
            fixture.PutCodeBytes(0x220, {0x48, 0x83, 0xC4, 0x20, 0xC3});
            const std::size_t sp = 0x100;
            fixture.PutStack64(sp + 32, 0x14001234Full);
            Context context = fixture.MakeContext(sp);
            context.Rip = fixture.ImageBase() + 0x220;
            Context before = context;
            ContextPointers pointers{};
            UnwindResult result{0x8888};
            StackView shortStack = fixture.StackSpan();
            shortStack.Size = sp + 39;
            CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240}, shortStack,
                  kFunctionEntryRva, context.Rip, &context, &pointers, &result) ==
                  Status::InvalidStackAccess);
            CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
            CHECK(result.EstablisherFrame == 0x8888);
        }
        {
            Fixture fixture;
            fixture.PutDefaultFunction(1, 4, 0x05); // RBP frame register
            fixture.PutCode(kUnwindInfoRva, 0, 4, 3, 0); // set frame register
            fixture.PutCodeBytes(0x220, {0x48, 0x8D, 0x65, 0x08, 0xC3});
            Context context = fixture.MakeContext(0x100);
            context.Rbp = (std::numeric_limits<std::uint64_t>::max)() - 7;
            context.Rip = fixture.ImageBase() + 0x220;
            Context before = context;
            ContextPointers pointers{};
            UnwindResult result{0x9999};
            CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240},
                  fixture.StackSpan(), kFunctionEntryRva, context.Rip,
                  &context, &pointers, &result) == Status::ArithmeticOverflow);
            CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
            CHECK(result.EstablisherFrame == 0x9999);
        }
        {
            Fixture fixture;
            fixture.PutDefaultFunction();
            Context context = fixture.MakeContext(0x100);
            context.Rip += 1;
            ContextPointers pointers{};
            UnwindResult result{};
            CHECK(UnwindFrame(fixture.ImageSpan(), {0x200, 0x240},
                  fixture.StackSpan(), kFunctionEntryRva,
                  fixture.ImageBase() + 0x220, &context, &pointers, &result) ==
                  Status::InvalidContext);
        }
        return true;
    });

    Run("successive epilogue frames preserve caller RIP and RSP", [] {
        Fixture fixture;
        fixture.PutFunction(0x100, 0x200, 0x240, 0x300);
        fixture.Put8(0x300, 1);
        fixture.Put8(0x301, 5);
        fixture.Put8(0x302, 2);
        fixture.Put8(0x303, 0);
        fixture.PutCode(0x300, 0, 5, 2, 3); // inner alloc 32
        fixture.PutCode(0x300, 1, 2, 0, 12); // inner push R12
        fixture.PutFunction(0x10C, 0x250, 0x280, 0x340);
        fixture.Put8(0x340, 1);
        fixture.Put8(0x341, 4);
        fixture.Put8(0x342, 1);
        fixture.Put8(0x343, 0);
        fixture.PutCode(0x340, 0, 4, 2, 1); // outer alloc 16
        fixture.PutCodeBytes(0x220, {
            0x48, 0x83, 0xC4, 0x20, 0x41, 0x5C, 0xC3
        });
        fixture.PutCodeBytes(0x270, {0x48, 0x83, 0xC4, 0x10, 0xC3});
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp + 32, 0x1212121212121212ull);
        fixture.PutStack64(sp + 40, fixture.ImageBase() + 0x270);
        fixture.PutStack64(sp + 64, 0x14001234Dull);
        Context context = fixture.MakeContext(sp);
        context.Rip = fixture.ImageBase() + 0x220;
        ContextPointers pointers{};
        UnwindResult result{};
        const ExecutableCodeRange codeRange{0x200, 0x280};
        CHECK(UnwindFrame(fixture.ImageSpan(), codeRange, fixture.StackSpan(),
              0x100, context.Rip, &context, &pointers, &result) == Status::Success);
        CHECK(context.Rip == fixture.ImageBase() + 0x270);
        CHECK(context.Rsp == fixture.StackBase() + sp + 48);
        CHECK(context.R12 == 0x1212121212121212ull);
        CHECK(UnwindFrame(fixture.ImageSpan(), codeRange, fixture.StackSpan(),
              0x10C, context.Rip, &context, &pointers, &result) == Status::Success);
        CHECK(context.Rip == 0x14001234Dull);
        CHECK(context.Rsp == fixture.StackBase() + sp + 72);
        return true;
    });

    Run("function lookup enforces half-open managed code range", [] {
        Fixture fixture;
        fixture.PutDefaultFunction();
        RuntimeFunction function{};
        std::uint32_t entryRva = 0;
        CHECK(FindRuntimeFunction(fixture.ImageSpan(), kFunctionEntryRva,
              sizeof(RuntimeFunction), fixture.ImageBase() + kFunctionBeginRva,
              &function, &entryRva) == Status::Success);
        CHECK(entryRva == kFunctionEntryRva);
        CHECK(FindRuntimeFunction(fixture.ImageSpan(), kFunctionEntryRva,
              sizeof(RuntimeFunction), fixture.ImageBase() + kFunctionEndRva,
              &function, &entryRva) == Status::NoFunction);
        CHECK(FindRuntimeFunction(fixture.ImageSpan(), kFunctionEntryRva,
              sizeof(RuntimeFunction), fixture.ImageBase() - 1,
              &function, &entryRva) == Status::NoFunction);
        return true;
    });

    Run("out-of-image .pdata table rejected", [] {
        Fixture fixture;
        RuntimeFunction function{};
        std::uint32_t entryRva = 0;
        CHECK(FindRuntimeFunction(fixture.ImageSpan(), 0xFFCu, 12,
              fixture.ImageBase() + kFunctionBeginRva,
              &function, &entryRva) == Status::MetadataOutOfBounds);
        return true;
    });

    Run("overlapping .pdata records rejected", [] {
        Fixture fixture;
        fixture.PutFunction(0x100, 0x200, 0x240, 0x300);
        fixture.PutFunction(0x10C, 0x230, 0x260, 0x320);
        fixture.PutDefaultFunction(0, 0);
        fixture.Put8(0x320, 1);
        fixture.Put8(0x321, 0);
        fixture.Put8(0x322, 0);
        fixture.Put8(0x323, 0);
        RuntimeFunction function{};
        std::uint32_t entryRva = 0;
        CHECK(FindRuntimeFunction(fixture.ImageSpan(), 0x100, 24,
              fixture.ImageBase() + 0x235, &function, &entryRva) ==
              Status::InvalidMetadata);
        return true;
    });

    Run("invalid function range rejected", [] {
        Fixture fixture;
        fixture.PutFunction(kFunctionEntryRva, 0x230, 0x220, kUnwindInfoRva);
        RuntimeFunction function{};
        std::uint32_t entryRva = 0;
        CHECK(FindRuntimeFunction(fixture.ImageSpan(), kFunctionEntryRva,
              sizeof(RuntimeFunction), fixture.ImageBase() + 0x235,
              &function, &entryRva) == Status::InvalidMetadata);
        return true;
    });

    Run("unwind metadata outside image rejected", [] {
        Fixture fixture;
        fixture.PutFunction(kFunctionEntryRva, kFunctionBeginRva,
                            kFunctionEndRva, 0xFFCu);
        fixture.Put8(0xFFC, 1);
        fixture.Put8(0xFFD, 0);
        fixture.Put8(0xFFE, 1);
        fixture.Put8(0xFFF, 0);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x14000AA01ull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::MetadataOutOfBounds);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("truncated unwind-code slots rejected", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(1, 1);
        fixture.PutCode(kUnwindInfoRva, 0, 1, 0, 3);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x14000AA02ull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        ImageView truncated = fixture.ImageSpan();
        truncated.Size = kUnwindInfoRva + 5;
        CHECK(UnwindFrame(truncated, fixture.StackSpan(), kFunctionEntryRva,
              context.Rip, false, &context, &pointers, &result) ==
              Status::MetadataOutOfBounds);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("invalid stack bounds rejected without mutation", [] {
        Fixture fixture;
        fixture.PutDefaultFunction();
        Context context = fixture.MakeContext(0x3F8);
        context.Rsp = fixture.StackBase() + fixture.Stack.size() + 8;
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::StackOutOfBounds);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("misaligned in-range stack pointer rejected", [] {
        Fixture fixture;
        fixture.PutDefaultFunction();
        Context context = fixture.MakeContext(0x101);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::MisalignedStack);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("return address beyond stack boundary rejected", [] {
        Fixture fixture;
        fixture.PutDefaultFunction();
        Context context = fixture.MakeContext(fixture.Stack.size());
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::StackOutOfBounds);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("unsupported version 2 rejected", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(0, 0, 0, 2);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x14000AA03ull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::UnsupportedVersion);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("exception-handler flags rejected", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(0, 0, 0, static_cast<std::uint8_t>(1 | (1 << 3)));
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x14000AA04ull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::UnsupportedHandler);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("chained unwind records rejected explicitly", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(0, 0, 0, static_cast<std::uint8_t>(1 | (4 << 3)));
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x14000AA05ull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::UnsupportedChain);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("unsupported machine frame opcode rejected", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(1, 1);
        fixture.PutCode(kUnwindInfoRva, 0, 1, 10, 0);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x14000AA06ull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::UnsupportedOpcode);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("volatile register in PUSH_NONVOL rejected", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(1, 1);
        fixture.PutCode(kUnwindInfoRva, 0, 1, 0, 1); // RCX is volatile
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x14000AA07ull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::InvalidMetadata);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("malformed code-offset order rejected", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(2, 8);
        fixture.PutCode(kUnwindInfoRva, 0, 1, 0, 3);
        fixture.PutCode(kUnwindInfoRva, 1, 5, 2, 1);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x14000AA08ull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::InvalidMetadata);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("invalid PUSH_NONVOL ordering rejected", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(2, 8);
        fixture.PutCode(kUnwindInfoRva, 0, 8, 0, 3); // push occurs after allocation
        fixture.PutCode(kUnwindInfoRva, 1, 1, 2, 1); // allocation
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x14000AA0Aull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::InvalidMetadata);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("malformed large-allocation operand count rejected", [] {
        Fixture fixture;
        fixture.PutDefaultFunction(1, 4);
        fixture.PutCode(kUnwindInfoRva, 0, 4, 1, 0);
        const std::size_t sp = 0x100;
        fixture.PutStack64(sp, 0x14000AA09ull);
        Context context = fixture.MakeContext(sp);
        Context before = context;
        ContextPointers pointers{};
        UnwindResult result{};
        CHECK(Unwind(fixture, kFunctionEntryRva, context.Rip, &context,
                     &pointers, &result) == Status::InvalidMetadata);
        CHECK(std::memcmp(&context, &before, sizeof(context)) == 0);
        return true;
    });

    Run("chained .pdata overlap and out-of-range IP fail closed", [] {
        Fixture fixture;
        fixture.PutDefaultFunction();
        fixture.PutFunction(0x10C, 0x250, 0x270, 0x320);
        fixture.Put8(0x320, 1);
        fixture.Put8(0x321, 0);
        fixture.Put8(0x322, 0);
        fixture.Put8(0x323, 0);
        RuntimeFunction function{};
        std::uint32_t entryRva = 0;
        CHECK(FindRuntimeFunction(fixture.ImageSpan(), kFunctionEntryRva, 24,
              fixture.ImageBase() + 0x300, &function, &entryRva) == Status::NoFunction);
        return true;
    });

    Run("checked image-base arithmetic rejects overflow", [] {
        Fixture fixture;
        ImageView invalid = fixture.ImageSpan();
        invalid.BaseAddress = (std::numeric_limits<std::uint64_t>::max)() - 4;
        RuntimeFunction function{};
        std::uint32_t entryRva = 0;
        CHECK(FindRuntimeFunction(invalid, kFunctionEntryRva,
              sizeof(RuntimeFunction), invalid.BaseAddress + kFunctionBeginRva,
              &function, &entryRva) == Status::InvalidArgument);
        return true;
    });

    std::cout << "RESULT " << (g_failCount == 0 ? "PASS" : "FAIL")
              << " tests=" << g_testCount
              << " failed=" << g_failCount
              << " windows_reference_cases=" << g_windowsReferenceCount << '\n';
    return g_failCount == 0 ? 0 : 1;
}
