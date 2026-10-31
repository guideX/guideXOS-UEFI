#include <windows.h>

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstring>
#include <cstdlib>
#include <fstream>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <utility>
#include <string>
#include <vector>

#pragma pack(push, 1)
struct T_RUNTIME_FUNCTION
{
    uint32_t BeginAddress;
    uint32_t EndAddress;
    uint32_t UnwindInfoAddress;
};
#pragma pack(pop)

class MethodInfo
{
    uintptr_t dummyPtrs[5];
    int32_t dummyInts[8];
};

class EHEnumState
{
    uintptr_t dummyPtrs[2];
    int32_t dummyInts[2];

public:
    uintptr_t Pointer(size_t index) const { return dummyPtrs[index]; }
};

enum EHClauseKind : int32_t
{
    EH_CLAUSE_TYPED = 0,
    EH_CLAUSE_FAULT = 1,
    EH_CLAUSE_FILTER = 2,
    EH_CLAUSE_UNUSED = 3,
};

struct EHClause
{
    EHClauseKind m_clauseKind;
    uint32_t m_tryStartOffset;
    uint32_t m_tryEndOffset;
    uint8_t* m_filterAddress;
    uint8_t* m_handlerAddress;
    void* m_pTargetType;
};

struct REGDISPLAY
{
    uintptr_t* pRax;
    uintptr_t* pRcx;
    uintptr_t* pRdx;
    uintptr_t* pRbx;
    uintptr_t* pRbp;
    uintptr_t* pRsi;
    uintptr_t* pRdi;
    uintptr_t* pR8;
    uintptr_t* pR9;
    uintptr_t* pR10;
    uintptr_t* pR11;
    uintptr_t* pR12;
    uintptr_t* pR13;
    uintptr_t* pR14;
    uintptr_t* pR15;
    uintptr_t SP;
    uintptr_t IP;
    uintptr_t SSP;
    struct alignas(16) Fp128 { uint64_t lo, hi; } Xmm[10];
};

using GCEnumCallback = void (*)(void*, void**, uint32_t);
struct GCEnumContext { GCEnumCallback pCallback; };
struct PInvokeTransitionFrame;

class CoffNativeCodeManager
{
    uintptr_t m_moduleBase;
    void* m_pvManagedCodeStartRange;
    uint32_t m_cbManagedCodeRange;
    T_RUNTIME_FUNCTION* m_pRuntimeFunctionTable;
    uint32_t m_nRuntimeFunctionTable;
    void** m_pClasslibFunctions;
    uint32_t m_nClasslibFunctions;

public:
    CoffNativeCodeManager(uintptr_t moduleBase, void* managedStart, uint32_t managedSize,
                          T_RUNTIME_FUNCTION* runtimeFunctions, uint32_t runtimeFunctionCount,
                          void** classlibFunctions, uint32_t classlibFunctionCount);
    ~CoffNativeCodeManager();

    virtual bool FindMethodInfo(void* controlPc, MethodInfo* methodInfo);
    virtual bool IsFunclet(MethodInfo* methodInfo);
    virtual void* GetFramePointer(MethodInfo* methodInfo, REGDISPLAY* registerSet);
    virtual bool IsSafePoint(void* address);
    virtual void EnumGcRefs(MethodInfo* methodInfo, void* safePointAddress,
                            REGDISPLAY* registerSet, GCEnumContext* callback,
                            bool isActiveStackFrame);
    virtual bool UnwindStackFrame(MethodInfo* methodInfo, uint32_t flags,
                                  REGDISPLAY* registerSet,
                                  PInvokeTransitionFrame** previousTransitionFrame);
    virtual bool IsUnwindable(void* address);
    virtual bool EHEnumInit(MethodInfo* methodInfo, void** methodStartAddress,
                            EHEnumState* state);
    virtual bool EHEnumNext(EHEnumState* state, EHClause* clause);
    virtual void* GetMethodStartAddress(MethodInfo* methodInfo);
    uint32_t GetCodeOffset(MethodInfo* methodInfo, void* address, uint8_t** gcInfo);
};

static_assert(sizeof(T_RUNTIME_FUNCTION) == 12);
static_assert(sizeof(MethodInfo) == 72);
static_assert(sizeof(EHEnumState) == 24);
static_assert(sizeof(REGDISPLAY) == 304);
static_assert(sizeof(CoffNativeCodeManager) == 64);

extern "C" void __stdcall RegisterCodeManager(void*, void*, uint32_t)
{
    std::abort(); // The hosted consumer test must never publish a manager.
}

extern "C" bool __stdcall RegisterUnboxingStubs(void*, uint32_t)
{
    std::abort(); // The hosted consumer test must never register unboxing stubs.
}

struct MappedImage
{
    std::vector<uint8_t> bytes;
    IMAGE_NT_HEADERS64 nt{};
    std::vector<IMAGE_SECTION_HEADER> sections;
    T_RUNTIME_FUNCTION* functions = nullptr;
    uint32_t functionCount = 0;
    uint32_t functionTableRva = 0;
    uint32_t codeStartRva = 0;
    uint32_t codeSize = 0;

    bool Load(const std::string& path)
    {
        std::ifstream stream(path, std::ios::binary);
        if (!stream) return false;
        std::vector<uint8_t> file((std::istreambuf_iterator<char>(stream)), {});
        if (file.size() < sizeof(IMAGE_DOS_HEADER)) return false;
        auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(file.data());
        if (dos->e_magic != IMAGE_DOS_SIGNATURE || dos->e_lfanew < 0) return false;
        const size_t ntOffset = static_cast<size_t>(dos->e_lfanew);
        if (ntOffset + sizeof(IMAGE_NT_HEADERS64) > file.size()) return false;
        const auto* fileNt = reinterpret_cast<const IMAGE_NT_HEADERS64*>(file.data() + ntOffset);
        if (fileNt->Signature != IMAGE_NT_SIGNATURE || fileNt->FileHeader.Machine != IMAGE_FILE_MACHINE_AMD64 ||
            fileNt->OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC) return false;
        ntCopy = *fileNt;
        if (ntCopy.OptionalHeader.SizeOfImage == 0 || ntCopy.OptionalHeader.SizeOfHeaders > file.size()) return false;
        bytes.assign(ntCopy.OptionalHeader.SizeOfImage, 0);
        std::memcpy(bytes.data(), file.data(), ntCopy.OptionalHeader.SizeOfHeaders);
        const size_t sectionOffset = ntOffset + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER) + ntCopy.FileHeader.SizeOfOptionalHeader;
        if (sectionOffset + static_cast<size_t>(ntCopy.FileHeader.NumberOfSections) * sizeof(IMAGE_SECTION_HEADER) > file.size()) return false;
        sections.resize(ntCopy.FileHeader.NumberOfSections);
        std::memcpy(sections.data(), file.data() + sectionOffset, sections.size() * sizeof(IMAGE_SECTION_HEADER));
        uint32_t minCode = UINT32_MAX;
        uint32_t maxCode = 0;
        for (const auto& section : sections)
        {
            const uint64_t rawEnd = static_cast<uint64_t>(section.PointerToRawData) + section.SizeOfRawData;
            const uint64_t imageEnd = static_cast<uint64_t>(section.VirtualAddress) + (std::max)(section.Misc.VirtualSize, section.SizeOfRawData);
            if (rawEnd > file.size() || imageEnd > bytes.size()) return false;
            if (section.SizeOfRawData != 0)
                std::memcpy(bytes.data() + section.VirtualAddress, file.data() + section.PointerToRawData, section.SizeOfRawData);
            if ((section.Characteristics & IMAGE_SCN_MEM_EXECUTE) != 0)
            {
                const uint32_t sectionStart = static_cast<uint32_t>(section.VirtualAddress);
                const uint32_t sectionEnd = sectionStart + static_cast<uint32_t>((std::max)(section.Misc.VirtualSize, section.SizeOfRawData));
                minCode = (std::min)(minCode, sectionStart);
                maxCode = (std::max)(maxCode, sectionEnd);
            }
        }
        if (minCode == UINT32_MAX || maxCode <= minCode) return false;
        codeStartRva = minCode;
        codeSize = maxCode - minCode;
        const auto& exception = ntCopy.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXCEPTION];
        if (exception.VirtualAddress == 0 || exception.Size == 0 || exception.Size % sizeof(T_RUNTIME_FUNCTION) != 0) return false;
        if (static_cast<uint64_t>(exception.VirtualAddress) + exception.Size > bytes.size()) return false;
        functions = reinterpret_cast<T_RUNTIME_FUNCTION*>(bytes.data() + exception.VirtualAddress);
        functionTableRva = exception.VirtualAddress;
        functionCount = exception.Size / sizeof(T_RUNTIME_FUNCTION);
        return functionCount != 0;
    }

private:
    IMAGE_NT_HEADERS64 ntCopy{};
};

struct MethodSymbol
{
    std::string name;
    uint32_t rva = 0;
    uint32_t preferredVa = 0;
};

static bool ReadMapAddress(const std::string& mapPath, const std::string& symbol,
                           uint64_t imageBase, MethodSymbol& result)
{
    std::ifstream stream(mapPath);
    std::string line;
    while (std::getline(stream, line))
    {
        if (line.find(symbol) == std::string::npos) continue;
        std::istringstream parser(line);
        std::string sectionOffset, name, address;
        if (!(parser >> sectionOffset >> name >> address)) continue;
        try
        {
            const uint64_t va = std::stoull(address, nullptr, 16);
            if (va < imageBase || va - imageBase > UINT32_MAX) return false;
            result = {symbol, static_cast<uint32_t>(va - imageBase), static_cast<uint32_t>(va)};
            return true;
        }
        catch (...) { return false; }
    }
    return false;
}

static T_RUNTIME_FUNCTION* FindFunction(MappedImage& image, uint32_t rva)
{
    for (uint32_t i = 0; i < image.functionCount; ++i)
        if (rva >= image.functions[i].BeginAddress && rva < image.functions[i].EndAddress)
            return &image.functions[i];
    return nullptr;
}

static size_t FrameSize(MappedImage& image, const T_RUNTIME_FUNCTION& function)
{
    if (static_cast<uint64_t>(function.UnwindInfoAddress) + 4 > image.bytes.size()) return SIZE_MAX;
    const uint8_t* info = image.bytes.data() + function.UnwindInfoAddress;
    const uint8_t count = info[2];
    if (static_cast<uint64_t>(function.UnwindInfoAddress) + 4 + static_cast<uint64_t>(count) * 2 > image.bytes.size()) return SIZE_MAX;
    size_t frame = 0;
    uint8_t slot = 0;
    while (slot < count)
    {
        const uint8_t opByte = info[4 + slot * 2 + 1];
        const uint8_t op = opByte & 0x0f;
        const uint8_t opInfo = opByte >> 4;
        switch (op)
        {
        case 0: frame += 8; ++slot; break;
        case 1:
            if (opInfo == 0 && slot + 1 < count)
            {
                const uint16_t scaled = *reinterpret_cast<const uint16_t*>(info + 4 + (slot + 1) * 2);
                frame += static_cast<size_t>(scaled) * 8;
                slot += 2;
            }
            else if (opInfo == 1 && slot + 2 < count)
            {
                const uint32_t allocation = *reinterpret_cast<const uint32_t*>(info + 4 + (slot + 1) * 2);
                frame += allocation;
                slot += 3;
            }
            else return SIZE_MAX;
            break;
        case 2: frame += static_cast<size_t>(opInfo) * 8 + 8; ++slot; break;
        case 3: ++slot; break;
        case 4: slot += 2; break;
        case 5: slot += 3; break;
        case 8: slot += 2; break;
        case 9: slot += 3; break;
        case 10: frame += opInfo == 0 ? 40 : 48; ++slot; break;
        default: return SIZE_MAX;
        }
    }
    return frame;
}

struct RootSlot
{
    void** address;
    uint32_t flags;
};

struct RootCapture
{
    GCEnumCallback pCallback = nullptr;
    std::vector<RootSlot> slots;
};

static void CaptureRoot(void* handle, void** slot, uint32_t flags)
{
    auto* capture = static_cast<RootCapture*>(handle);
    capture->slots.push_back({slot, flags});
}

struct WalkState
{
    std::array<uintptr_t, 15> gprs{};
    std::array<uintptr_t, 8192> stack{};
    REGDISPLAY registers{};

    WalkState()
    {
        for (size_t i = 0; i < gprs.size(); ++i) gprs[i] = 0x10000000u + i * 0x1000u;
        for (size_t i = 0; i < stack.size(); ++i) stack[i] = 0x20000000u + i * 0x1000u;
        registers.pRax = &gprs[0]; registers.pRcx = &gprs[1]; registers.pRdx = &gprs[2];
        registers.pRbx = &gprs[3]; registers.pRbp = &gprs[4]; registers.pRsi = &gprs[5];
        registers.pRdi = &gprs[6]; registers.pR8 = &gprs[7]; registers.pR9 = &gprs[8];
        registers.pR10 = &gprs[9]; registers.pR11 = &gprs[10]; registers.pR12 = &gprs[11];
        registers.pR13 = &gprs[12]; registers.pR14 = &gprs[13]; registers.pR15 = &gprs[14];
        registers.SP = reinterpret_cast<uintptr_t>(&stack[2048]);
        registers.IP = 0;
        registers.SSP = 0;
    }
};

static bool CaptureAt(CoffNativeCodeManager& manager, MethodInfo& info, WalkState& walk,
                      uintptr_t pc, bool active, RootCapture& capture)
{
    capture.pCallback = CaptureRoot;
    capture.slots.clear();
    walk.registers.IP = pc;
    manager.CoffNativeCodeManager::EnumGcRefs(&info, reinterpret_cast<void*>(pc), &walk.registers,
                                              reinterpret_cast<GCEnumContext*>(&capture), active);
    const uintptr_t registerStart = reinterpret_cast<uintptr_t>(walk.gprs.data());
    const uintptr_t registerEnd = registerStart + sizeof(walk.gprs);
    const uintptr_t stackStart = reinterpret_cast<uintptr_t>(walk.stack.data());
    const uintptr_t stackEnd = stackStart + sizeof(walk.stack);
    for (const auto& root : capture.slots)
    {
        const uintptr_t slot = reinterpret_cast<uintptr_t>(root.address);
        if (!((slot >= registerStart && slot < registerEnd) || (slot >= stackStart && slot < stackEnd)))
        {
            std::cerr << "root slot outside REGDISPLAY or synthetic stack: " << std::hex << slot << "\n";
            return false;
        }
    }
    return true;
}

static void Require(bool condition, const char* message)
{
    if (!condition) throw std::runtime_error(message);
}

int main(int argc, char** argv)
{
    try
    {
        Require(argc == 3, "usage: code_manager_consumer_tests.exe fixture.exe linker.map");
        MappedImage image;
        Require(image.Load(argv[1]), "failed to map fixture PE image");
        const size_t originalImageSize = image.bytes.size();
        image.bytes.reserve(originalImageSize + 128);
        image.functions = reinterpret_cast<T_RUNTIME_FUNCTION*>(image.bytes.data() + image.functionTableRva);
        auto* base = image.bytes.data();
        auto* manager = new CoffNativeCodeManager(reinterpret_cast<uintptr_t>(base),
            base + image.codeStartRva, image.codeSize, image.functions, image.functionCount, nullptr, 0);

        const uint64_t preferredBase = manager ? reinterpret_cast<const IMAGE_NT_HEADERS64*>(base +
            reinterpret_cast<const IMAGE_DOS_HEADER*>(base)->e_lfanew)->OptionalHeader.ImageBase : 0;
        const std::array<std::pair<const char*, const char*>, 5> targets = {{
            {"Main", "guideXOS_NativeAotGcEhFixture_GuideXos_NativeAotGcEhFixture_Program__Main"},
            {"Outer", "guideXOS_NativeAotGcEhFixture_GuideXos_NativeAotGcEhFixture_Program__Outer"},
            {"Inner", "guideXOS_NativeAotGcEhFixture_GuideXos_NativeAotGcEhFixture_Program__Inner"},
            {"CatchFixture", "guideXOS_NativeAotGcEhFixture_GuideXos_NativeAotGcEhFixture_Program__CatchFixture"},
            {"ThrowingCall", "guideXOS_NativeAotGcEhFixture_GuideXos_NativeAotGcEhFixture_Program__ThrowingCall"},
        }};

        std::vector<MethodSymbol> methods;
        for (const auto& [label, symbol] : targets)
        {
            MethodSymbol method;
            Require(ReadMapAddress(argv[2], symbol, preferredBase, method), "missing managed method symbol in linker map");
            MethodInfo info{};
            void* pc = base + method.rva;
            Require(manager->CoffNativeCodeManager::FindMethodInfo(pc, &info), "COFF code manager did not identify managed method");
            Require(manager->CoffNativeCodeManager::GetMethodStartAddress(&info) == pc, "method start address disagrees with linker symbol");
            Require(FindFunction(image, method.rva) != nullptr, "managed method has no runtime function entry");
            methods.push_back(method);
            std::cout << "METHOD " << label << " rva=0x" << std::hex << method.rva << std::dec << " identified=1\n";
        }

        bool sawRegisterRoot = false;
        bool sawStackRoot = false;
        std::vector<std::pair<uint32_t, size_t>> outerCounts;
        std::vector<uintptr_t> innerSafePoints;
        std::vector<uintptr_t> outerSafePoints;
        for (size_t methodIndex = 1; methodIndex <= 2; ++methodIndex)
        {
            const MethodSymbol& method = methods[methodIndex];
            T_RUNTIME_FUNCTION* function = FindFunction(image, method.rva);
            const uint32_t length = function->EndAddress - function->BeginAddress;
            MethodInfo info{};
            Require(manager->CoffNativeCodeManager::FindMethodInfo(base + method.rva, &info), "method lookup failed during safepoint scan");
            std::vector<uint32_t> safepoints;
            for (uint32_t offset = 0; offset < length; ++offset)
                if (manager->CoffNativeCodeManager::IsSafePoint(base + function->BeginAddress + offset))
                    safepoints.push_back(offset);
            Require(!safepoints.empty(), "managed fixture method has no decoded GC safepoint");
            for (uint32_t offset : safepoints)
            {
                WalkState walk;
                RootCapture capture;
                const uintptr_t pc = reinterpret_cast<uintptr_t>(base + function->BeginAddress + offset);
                Require(CaptureAt(*manager, info, walk, pc, true, capture), "GC decoder returned a root outside supplied frame storage");
                size_t regs = 0, stack = 0;
                const uintptr_t regStart = reinterpret_cast<uintptr_t>(walk.gprs.data());
                const uintptr_t regEnd = regStart + sizeof(walk.gprs);
                std::vector<size_t> registerIndices;
                for (const auto& root : capture.slots)
                {
                    const uintptr_t slot = reinterpret_cast<uintptr_t>(root.address);
                    if (slot >= regStart && slot < regEnd) { ++regs; sawRegisterRoot = true; }
                    else { ++stack; sawStackRoot = true; }
                    for (size_t registerIndex = 0; registerIndex < walk.gprs.size(); ++registerIndex)
                        if (root.address == reinterpret_cast<void**>(&walk.gprs[registerIndex]))
                            registerIndices.push_back(registerIndex);
                }
                std::cout << "GC " << (methodIndex == 1 ? "Outer" : "Inner")
                          << " offset=0x" << std::hex << offset << std::dec
                          << " roots=" << capture.slots.size() << " register=" << regs << " stack=" << stack
                          << " register_indices=";
                for (size_t i = 0; i < registerIndices.size(); ++i)
                    std::cout << (i == 0 ? "" : ",") << registerIndices[i];
                std::cout << "\n";
                if (methodIndex == 1) outerCounts.push_back({offset, capture.slots.size()});
                else innerSafePoints.push_back(pc);
                if (methodIndex == 1) outerSafePoints.push_back(pc);
            }
        }
        Require(sawRegisterRoot, "authentic fixture did not report a live register root");
        Require(sawStackRoot, "authentic fixture did not report a live stack root");
        Require(outerCounts.size() == 2 && outerCounts[0].second == 7 && outerCounts[1].second == 6,
                "authentic GC info did not drop dead references across successive safepoints");

        // Apply relocation-shaped writes through the real NativeAOT code-manager callback.
        MethodInfo outerInfo{};
        Require(manager->CoffNativeCodeManager::FindMethodInfo(base + methods[1].rva, &outerInfo), "Outer lookup failed");
        WalkState updateWalk;
        RootCapture updateCapture;
        Require(CaptureAt(*manager, outerInfo, updateWalk, outerSafePoints.back(), true, updateCapture), "root update enumeration failed");
        Require(!updateCapture.slots.empty(), "no live slots were available for a callback update");
        std::vector<uintptr_t> replacements;
        for (size_t i = 0; i < updateCapture.slots.size(); ++i)
        {
            const uintptr_t replacement = 0x7A000000u + i * 0x100u;
            *reinterpret_cast<uintptr_t*>(updateCapture.slots[i].address) = replacement;
            replacements.push_back(replacement);
        }
        for (size_t i = 0; i < updateCapture.slots.size(); ++i)
            Require(*reinterpret_cast<uintptr_t*>(updateCapture.slots[i].address) == replacements[i], "root-slot update did not modify reported storage");
        std::cout << "ROOT_UPDATE slots=" << updateCapture.slots.size() << " verified=1 compacting_gc=0\n";

        // Walk a synthetic but metadata-shaped Inner -> Outer stack. Method code and all GC/EH records remain compiler generated.
        MethodInfo innerInfo{};
        MethodInfo outerFrameInfo{};
        T_RUNTIME_FUNCTION* innerFunction = FindFunction(image, methods[2].rva);
        Require(manager->CoffNativeCodeManager::FindMethodInfo(base + methods[2].rva, &innerInfo), "Inner lookup failed before unwind");
        WalkState walk;
        uintptr_t* sp = reinterpret_cast<uintptr_t*>(walk.registers.SP);
        const size_t innerFrameSize = FrameSize(image, *innerFunction);
        Require(innerFrameSize != SIZE_MAX, "unsupported Inner x64 unwind encoding");
        const uintptr_t outerReturnPc = outerSafePoints.front();
        *(reinterpret_cast<uintptr_t*>(reinterpret_cast<uint8_t*>(sp) + innerFrameSize)) = outerReturnPc;
        const uintptr_t innerPc = innerSafePoints.front();
        RootCapture innerRoots;
        Require(CaptureAt(*manager, innerInfo, walk, innerPc, true, innerRoots), "Inner GC root walk failed");
        PInvokeTransitionFrame* transition = nullptr;
        Require(manager->CoffNativeCodeManager::UnwindStackFrame(&innerInfo, 0, &walk.registers, &transition), "COFF manager frame unwind returned failure");
        Require(walk.registers.IP == outerReturnPc, "frame unwind did not recover the seeded caller IP");
        Require(walk.registers.SP == reinterpret_cast<uintptr_t>(sp) + innerFrameSize + sizeof(uintptr_t), "frame unwind returned the wrong caller SP");
        Require(manager->CoffNativeCodeManager::FindMethodInfo(reinterpret_cast<void*>(walk.registers.IP), &outerFrameInfo), "caller frame was not identified after unwind");
        RootCapture outerRoots;
        Require(CaptureAt(*manager, outerFrameInfo, walk, walk.registers.IP, false, outerRoots), "caller-frame GC root walk failed");
        Require(!innerRoots.slots.empty() && !outerRoots.slots.empty(), "multi-frame root enumeration omitted a live frame");
        std::cout << "MULTIFRAME inner_roots=" << innerRoots.slots.size() << " outer_roots=" << outerRoots.slots.size() << " unwind=1\n";

        MethodInfo catchInfo{};
        const MethodSymbol& catchMethod = methods[3];
        Require(manager->CoffNativeCodeManager::FindMethodInfo(base + catchMethod.rva, &catchInfo), "catch method lookup failed");
        void* catchStart = nullptr;
        EHEnumState ehState{};
        Require(manager->CoffNativeCodeManager::EHEnumInit(&catchInfo, &catchStart, &ehState), "authentic EH info was not located");
        Require(catchStart == base + catchMethod.rva, "EH metadata associated with the wrong main method");
        auto* ehInfoCursor = reinterpret_cast<uint8_t*>(ehState.Pointer(1));
        Require(ehInfoCursor > base && ehInfoCursor <= base + image.bytes.size(), "EH handler-data cursor is outside the mapped image");
        uint32_t clauseCount = 0;
        EHClause clause{};
        while (manager->CoffNativeCodeManager::EHEnumNext(&ehState, &clause))
        {
            ++clauseCount;
            Require(clause.m_clauseKind == EH_CLAUSE_TYPED, "fixture emitted an unexpected EH clause kind");
            Require(clause.m_tryStartOffset < clause.m_tryEndOffset, "EH clause has invalid try bounds");
            Require(clause.m_handlerAddress >= base + image.codeStartRva && clause.m_handlerAddress < base + image.codeStartRva + image.codeSize, "EH handler address is outside executable code");
            Require(clause.m_pTargetType >= base && clause.m_pTargetType < base + image.bytes.size(), "EH target type is outside the mapped image");
        }
        Require(clauseCount == 1, "catch fixture did not decode exactly one typed handler");
        std::cout << "EH method=CatchFixture clauses=" << clauseCount << " eh_info_rva=0x"
                  << std::hex << static_cast<uintptr_t>(ehInfoCursor - base) << std::dec
                  << " handler_bounds=1 target_type_bounds=1 propagation=0\n";

        uint32_t funclets = 0;
        uint32_t associatedCatchFunclets = 0;
        for (uint32_t i = 0; i < image.functionCount; ++i)
        {
            const auto& function = image.functions[i];
            if (function.BeginAddress < image.codeStartRva || function.BeginAddress >= image.codeStartRva + image.codeSize) continue;
            MethodInfo candidate{};
            if (!manager->CoffNativeCodeManager::FindMethodInfo(base + function.BeginAddress, &candidate)) continue;
            if (!manager->CoffNativeCodeManager::IsFunclet(&candidate)) continue;
            ++funclets;
            if (manager->CoffNativeCodeManager::GetMethodStartAddress(&candidate) == catchStart) ++associatedCatchFunclets;
        }
        Require(funclets > 0 && associatedCatchFunclets > 0, "real manager did not associate the catch method with a funclet");
        std::cout << "FUNCLETS total=" << funclets << " associated_with_CatchFixture=" << associatedCatchFunclets << "\n";

        // Probe the pinned manager's R13 consumer state with a test-only, valid x64
        // UNWIND_INFO overlay. The GC info copied after it remains compiler generated.
        innerFunction = FindFunction(image, methods[2].rva);
        Require(innerFunction != nullptr, "Inner runtime function disappeared before the R13 probe");
        MethodInfo r13Info{};
        Require(manager->CoffNativeCodeManager::FindMethodInfo(base + innerFunction->BeginAddress, &r13Info), "Inner method lookup failed before the R13 probe");
        uint8_t* authenticGcInfo = nullptr;
        manager->CoffNativeCodeManager::GetCodeOffset(&r13Info, reinterpret_cast<void*>(innerSafePoints.front()), &authenticGcInfo);
        const size_t gcInfoOffset = static_cast<size_t>(authenticGcInfo - base);
        Require(gcInfoOffset < originalImageSize && originalImageSize - gcInfoOffset >= 64, "authentic Inner GC info tail is too short for the isolated R13 probe");

        const size_t syntheticUnwindRva = (originalImageSize + 3) & ~static_cast<size_t>(3);
        const size_t appendedSize = syntheticUnwindRva - originalImageSize + 12 + 1 + 64;
        image.bytes.resize(originalImageSize + appendedSize, 0);
        Require(image.bytes.data() == base, "image storage moved after code-manager construction");
        image.functions = reinterpret_cast<T_RUNTIME_FUNCTION*>(image.bytes.data() + image.functionTableRva);
        innerFunction = FindFunction(image, methods[2].rva);
        const uint32_t authenticUnwindRva = innerFunction->UnwindInfoAddress;
        uint8_t* syntheticUnwind = image.bytes.data() + syntheticUnwindRva;
        syntheticUnwind[0] = 1;       // version 1, no exception-handler flags
        syntheticUnwind[1] = 2;       // two-byte prologue
        syntheticUnwind[2] = 3;       // ALLOC_SMALL + SAVE_NONVOL + offset operand
        syntheticUnwind[3] = 0;
        syntheticUnwind[4] = 2; syntheticUnwind[5] = 0x42; // sub rsp, 40
        syntheticUnwind[6] = 1; syntheticUnwind[7] = 0xD4; // save R13 at [rsp + 0]
        syntheticUnwind[8] = 0; syntheticUnwind[9] = 0;
        syntheticUnwind[12] = 0;      // root-function UBF flags
        std::memcpy(syntheticUnwind + 13, authenticGcInfo, 64);
        innerFunction->UnwindInfoAddress = static_cast<uint32_t>(syntheticUnwindRva);

        WalkState r13Walk;
        const uintptr_t r13Stack = r13Walk.registers.SP;
        const uintptr_t savedR13 = 0xD1300000000013ull;
        const uintptr_t seededR13 = r13Walk.gprs[12];
        const uintptr_t callerPc = outerSafePoints.front();
        *reinterpret_cast<uintptr_t*>(r13Stack) = savedR13;
        *reinterpret_cast<uintptr_t*>(r13Stack + 40) = callerPc;
        r13Walk.registers.IP = reinterpret_cast<uintptr_t>(base + innerFunction->BeginAddress + 0x10);
        Require(manager->CoffNativeCodeManager::FindMethodInfo(reinterpret_cast<void*>(r13Walk.registers.IP), &r13Info), "test-only R13 unwind function was not identified");
        PInvokeTransitionFrame* r13Transition = nullptr;
        Require(manager->CoffNativeCodeManager::UnwindStackFrame(&r13Info, 0, &r13Walk.registers, &r13Transition), "test-only R13 code-manager unwind failed");
        const bool r13ContextLocationRetained = r13Walk.registers.pR13 == &r13Walk.gprs[12];
        const bool r13StackLocationSelected = r13Walk.registers.pR13 == reinterpret_cast<uintptr_t*>(r13Stack);
        const bool r13ValueRecovered = *r13Walk.registers.pR13 == savedR13;
        Require(r13ValueRecovered && r13StackLocationSelected, "pinned code manager did not select the writable R13 save slot with the recovered value");
        *r13Walk.registers.pR13 = 0xA13A000000000013ull;
        const bool r13LocationWritable = *r13Walk.registers.pR13 == 0xA13A000000000013ull;
        Require(r13LocationWritable, "recovered R13 REGDISPLAY location is not writable");
        std::cout << "R13_PROBE synthetic_unwind=1 value_recovered=1 context_location_retained=" << r13ContextLocationRetained
                  << " stack_save_location_selected=" << r13StackLocationSelected << " writable=1 seeded_value_before=0x"
                  << std::hex << seededR13 << " stack_save_value=0x" << savedR13 << std::dec << "\n";
        innerFunction->UnwindInfoAddress = authenticUnwindRva;

        delete manager;
        std::cout << "RESULT PASS authentic_code_manager_gc_eh=1 r13_probe=diagnostic\n";
        return 0;
    }
    catch (const std::exception& ex)
    {
        std::cerr << "RESULT FAIL " << ex.what() << "\n";
        return 1;
    }
}
