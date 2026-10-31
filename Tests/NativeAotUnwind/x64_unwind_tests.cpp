#include "../../Tools/Phase23/x64_unwind_bounded.h"

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <functional>
#include <iostream>
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
    alignas(16) std::array<std::uint8_t, 0x400> Stack{};

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
bool CompareWindowsReference(Fixture& fixture, std::uint32_t beginRva,
                             std::uint32_t endRva, std::uint32_t unwindRva,
                             std::uint64_t controlPc,
                             const Context& original,
                             bool compareContextPointers = true) {
    Context actual = original;
    ContextPointers actualPointers{};
    UnwindResult actualResult{};
    const Status status = Unwind(
        fixture, kFunctionEntryRva, controlPc, &actual,
        &actualPointers, &actualResult);
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
        const bool integerMatches =
            reinterpret_cast<void*>(actualPointers.IntegerContext[index]) ==
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
#if GUIDEXOS_HAS_WINDOWS_UNWIND_REFERENCE
        Context original = fixture.MakeContext(sp);
        original.Rip = fixture.ImageBase() + kFunctionBeginRva + 0x10;
        CHECK(CompareWindowsReference(fixture, kFunctionBeginRva,
              kFunctionEndRva, kUnwindInfoRva, original.Rip, original, false));
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
