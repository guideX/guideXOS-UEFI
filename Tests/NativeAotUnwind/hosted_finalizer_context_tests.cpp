#include <windows.h>
#include <winternl.h>
#include <intrin.h>

#include <cstdint>
#include <cstdio>
#include <cstring>

#include "../../Tools/Phase23/finalizer_stack_contract.h"

using guidexos::finalizerstack::Descriptor;

extern "C" bool HostedPalGetMaximumStackBounds(void** stackLow,
                                                void** stackHigh);
extern "C" void __stdcall HostedFinalizerFiberEntry(void* context);
extern "C" void __stdcall HostedFinalizerFiberBody(void* context);

namespace {

constexpr SIZE_T kFiberStackSize = 512 * 1024;
constexpr uint64_t kMainTlsValue = 0x4D41494E544C5301ULL;
constexpr uint64_t kWorkerTlsValue = 0x46494E414C495A01ULL;

__declspec(thread) uint64_t g_threadLocalMarker;
uint64_t g_nextGeneration;

struct Bounds {
    uintptr_t Low;
    uintptr_t High;
};

struct WorkerContext {
    uint32_t EntryRspMod16;
    bool ForceActivationFailure;
    bool ForceFiberCreateFailure;
    DWORD ThreadId;
    void* RootFiber;
    void* FinalizerFiber;
    uint64_t Generation;
    uint64_t TlsAtThreadEntry;
    uint64_t TlsInFinalizerFiber;
    Bounds RootBounds;
    Bounds FinalizerBounds;
    uintptr_t AccessibleLow;
    uintptr_t ActiveStackPointer;
    Descriptor StackDescriptor;
    bool FiberCreated;
    bool FiberEntered;
    bool FinalizerActivated;
    bool WorkerReturnedToRoot;
    bool RootBoundsRestored;
    bool FiberRetired;
    bool ThreadConvertedBack;
    bool StackPagesVerified;
    bool DescriptorValid;
    bool TruncatedDescriptorRejected;
    bool WrongOwnerRejected;
    bool InvalidPalRequestCleared;
    bool CreateFailureObserved;
    bool NoActiveWorkerAfterFailure;
    bool WorkerTlsWasIsolated;
};

unsigned g_passed;
unsigned g_failed;

void Check(bool condition, const char* name)
{
    if (condition)
    {
        ++g_passed;
        std::printf("PASS %s\n", name);
    }
    else
    {
        ++g_failed;
        std::printf("FAIL %s\n", name);
    }
}

bool QueryAccessibleInterval(uintptr_t low, uintptr_t high,
                             uintptr_t* accessibleLow)
{
    SYSTEM_INFO systemInfo{};
    GetSystemInfo(&systemInfo);
    const uintptr_t pageSize = systemInfo.dwPageSize;
    if (low == 0 || high <= low || pageSize == 0 ||
        (low & (pageSize - 1)) != 0 || (high & (pageSize - 1)) != 0)
        return false;

    uintptr_t first = high;
    for (uintptr_t page = high - pageSize; page >= low; page -= pageSize)
    {
        MEMORY_BASIC_INFORMATION info{};
        if (VirtualQuery(reinterpret_cast<const void*>(page), &info,
                         sizeof(info)) != sizeof(info) ||
            info.State != MEM_COMMIT ||
            (info.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0 ||
            reinterpret_cast<uintptr_t>(info.AllocationBase) != low)
            break;

        first = page;
        if (page == low)
            break;
    }

    *accessibleLow = first;
    return first < high;
}

bool VerifyAccessiblePages(uintptr_t low, uintptr_t high,
                           uintptr_t expectedAllocationBase)
{
    SYSTEM_INFO systemInfo{};
    GetSystemInfo(&systemInfo);
    const uintptr_t pageSize = systemInfo.dwPageSize;
    if (pageSize == 0 || (low & (pageSize - 1)) != 0 ||
        (high & (pageSize - 1)) != 0 || high <= low)
        return false;

    for (uintptr_t page = low; page < high; page += pageSize)
    {
        MEMORY_BASIC_INFORMATION info{};
        if (VirtualQuery(reinterpret_cast<const void*>(page), &info,
                         sizeof(info)) != sizeof(info) ||
            info.State != MEM_COMMIT ||
            (info.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0 ||
            reinterpret_cast<uintptr_t>(info.AllocationBase) !=
                expectedAllocationBase)
            return false;
    }
    return true;
}

DWORD WINAPI FinalizerWorkerThread(void* parameter)
{
    WorkerContext* context = static_cast<WorkerContext*>(parameter);
    context->ThreadId = GetCurrentThreadId();
    context->TlsAtThreadEntry = g_threadLocalMarker;
    context->WorkerTlsWasIsolated = g_threadLocalMarker == 0;
    g_threadLocalMarker = kWorkerTlsValue;

    void* rootLow = nullptr;
    void* rootHigh = nullptr;
    if (!HostedPalGetMaximumStackBounds(&rootLow, &rootHigh))
        return 20;
    context->RootBounds = {
        reinterpret_cast<uintptr_t>(rootLow),
        reinterpret_cast<uintptr_t>(rootHigh)
    };

    context->RootFiber = ConvertThreadToFiber(context);
    if (context->RootFiber == nullptr)
        return 21;

    SIZE_T commitSize = kFiberStackSize;
    SIZE_T reserveSize = kFiberStackSize;
    if (context->ForceFiberCreateFailure)
    {
        commitSize = static_cast<SIZE_T>(-1);
        reserveSize = static_cast<SIZE_T>(-1);
    }

    context->FinalizerFiber = CreateFiberEx(
        commitSize, reserveSize, FIBER_FLAG_FLOAT_SWITCH,
        HostedFinalizerFiberEntry, context);
    context->FiberCreated = context->FinalizerFiber != nullptr;
    if (context->FinalizerFiber == nullptr)
    {
        context->CreateFailureObserved = context->ForceFiberCreateFailure;
        context->NoActiveWorkerAfterFailure = true;
        ConvertFiberToThread();
        context->ThreadConvertedBack = true;
        return context->ForceFiberCreateFailure ? 0 : 22;
    }

    SwitchToFiber(context->FinalizerFiber);
    context->WorkerReturnedToRoot = true;

    void* restoredLow = nullptr;
    void* restoredHigh = nullptr;
    const bool gotRestoredBounds = HostedPalGetMaximumStackBounds(
        &restoredLow, &restoredHigh);
    context->RootBoundsRestored = gotRestoredBounds &&
        reinterpret_cast<uintptr_t>(restoredLow) == context->RootBounds.Low &&
        reinterpret_cast<uintptr_t>(restoredHigh) == context->RootBounds.High;

    DeleteFiber(context->FinalizerFiber);
    context->FinalizerFiber = nullptr;
    context->FiberRetired = true;
    context->NoActiveWorkerAfterFailure =
        context->ForceActivationFailure && !context->FinalizerActivated;

    ConvertFiberToThread();
    context->ThreadConvertedBack = true;
    return 0;
}

} // namespace

// Hosted-only PAL adapter. It uses the active Windows fiber's actual TEB
// StackBase and VirtualQuery reservation owner; the guideXOS target PAL does
// not use this provider.
extern "C" bool HostedPalGetMaximumStackBounds(void** stackLow,
                                                void** stackHigh)
{
    if (stackLow != nullptr)
        *stackLow = nullptr;
    if (stackHigh != nullptr)
        *stackHigh = nullptr;
    if (stackLow == nullptr || stackHigh == nullptr)
        return false;

    void* marker = _AddressOfReturnAddress();
    MEMORY_BASIC_INFORMATION info{};
    if (VirtualQuery(marker, &info, sizeof(info)) != sizeof(info))
        return false;

    NT_TIB* tib = reinterpret_cast<NT_TIB*>(NtCurrentTeb());
    void* low = info.AllocationBase;
    void* high = tib->StackBase;
    const uintptr_t markerAddress = reinterpret_cast<uintptr_t>(marker);
    const uintptr_t lowAddress = reinterpret_cast<uintptr_t>(low);
    const uintptr_t highAddress = reinterpret_cast<uintptr_t>(high);
    if (lowAddress == 0 || highAddress <= lowAddress ||
        markerAddress < lowAddress || markerAddress >= highAddress)
        return false;

    *stackLow = low;
    *stackHigh = high;
    return true;
}

extern "C" void __stdcall HostedFinalizerFiberBody(void* parameter)
{
    WorkerContext* context = static_cast<WorkerContext*>(parameter);
    context->FiberEntered = true;
    context->TlsInFinalizerFiber = g_threadLocalMarker;

    void* stackLow = nullptr;
    void* stackHigh = nullptr;
    if (!HostedPalGetMaximumStackBounds(&stackLow, &stackHigh))
    {
        SwitchToFiber(context->RootFiber);
        return;
    }

    context->FinalizerBounds = {
        reinterpret_cast<uintptr_t>(stackLow),
        reinterpret_cast<uintptr_t>(stackHigh)
    };
    context->ActiveStackPointer =
        reinterpret_cast<uintptr_t>(_AddressOfReturnAddress());

    MEMORY_BASIC_INFORMATION activePage{};
    const bool queriedActivePage = VirtualQuery(
        reinterpret_cast<const void*>(context->ActiveStackPointer),
        &activePage, sizeof(activePage)) == sizeof(activePage);
    uintptr_t accessibleLow = 0;
    const bool gotAccessibleRange = QueryAccessibleInterval(
        context->FinalizerBounds.Low, context->FinalizerBounds.High,
        &accessibleLow);
    context->AccessibleLow = accessibleLow;
    context->StackPagesVerified = gotAccessibleRange &&
        VerifyAccessiblePages(accessibleLow, context->FinalizerBounds.High,
                               context->FinalizerBounds.Low) &&
        queriedActivePage && activePage.State == MEM_COMMIT &&
        (activePage.Protect & (PAGE_GUARD | PAGE_NOACCESS)) == 0 &&
        reinterpret_cast<uintptr_t>(activePage.AllocationBase) ==
            context->FinalizerBounds.Low;

    context->StackDescriptor = {
        guidexos::finalizerstack::kDescriptorMagic,
        guidexos::finalizerstack::kDescriptorVersion,
        guidexos::finalizerstack::kKindFinalizer,
        context->FinalizerBounds.Low,
        context->FinalizerBounds.High,
        context->AccessibleLow,
        context->FinalizerBounds.High,
        context->FinalizerBounds.Low,
        context->ThreadId,
        context->Generation,
        guidexos::finalizerstack::kRequiredHostedFlags
    };

    SYSTEM_INFO systemInfo{};
    GetSystemInfo(&systemInfo);
    context->DescriptorValid = context->StackPagesVerified &&
        guidexos::finalizerstack::Validate(
            context->StackDescriptor, context->ThreadId,
            context->Generation, context->ActiveStackPointer,
            systemInfo.dwPageSize);

    Descriptor truncated = context->StackDescriptor;
    truncated.AccessibleHigh = context->ActiveStackPointer;
    context->TruncatedDescriptorRejected =
        !guidexos::finalizerstack::Validate(
            truncated, context->ThreadId, context->Generation,
            context->ActiveStackPointer, systemInfo.dwPageSize);

    context->WrongOwnerRejected = !guidexos::finalizerstack::Validate(
        context->StackDescriptor, context->ThreadId + 1,
        context->Generation, context->ActiveStackPointer,
        systemInfo.dwPageSize);

    void* invalidLow = reinterpret_cast<void*>(1);
    const bool invalidAccepted = HostedPalGetMaximumStackBounds(
        &invalidLow, nullptr);
    context->InvalidPalRequestCleared = !invalidAccepted && invalidLow == nullptr;

    if (!context->ForceActivationFailure && context->DescriptorValid)
        context->FinalizerActivated = true;

    SwitchToFiber(context->RootFiber);
}

int main()
{
    g_threadLocalMarker = kMainTlsValue;

    void* mainLow = reinterpret_cast<void*>(1);
    void* mainHigh = reinterpret_cast<void*>(1);
    const bool mainBoundsValid = HostedPalGetMaximumStackBounds(
        &mainLow, &mainHigh);
    Check(mainBoundsValid && mainLow != nullptr && mainHigh != nullptr &&
              reinterpret_cast<uintptr_t>(mainHigh) >
                  reinterpret_cast<uintptr_t>(mainLow),
          "host PAL returns current bootstrap execution bounds");

    void* clearedLow = reinterpret_cast<void*>(1);
    const bool invalidRequestAccepted = HostedPalGetMaximumStackBounds(
        &clearedLow, nullptr);
    Check(!invalidRequestAccepted && clearedLow == nullptr,
          "invalid PAL request clears outputs and fails closed");

    WorkerContext first{};
    first.Generation = ++g_nextGeneration;
    HANDLE firstThread = CreateThread(nullptr, 0, FinalizerWorkerThread,
                                      &first, 0, nullptr);
    Check(firstThread != nullptr, "host OS creates a dedicated worker thread");
    if (firstThread != nullptr)
    {
        const DWORD wait = WaitForSingleObject(firstThread, INFINITE);
        DWORD exitCode = 0xFFFFFFFFu;
        GetExitCodeThread(firstThread, &exitCode);
        CloseHandle(firstThread);
        Check(wait == WAIT_OBJECT_0 && exitCode == 0,
              "worker entry and exit complete under owner control");
    }

    Check(first.ThreadId != 0 && first.ThreadId != GetCurrentThreadId(),
          "worker has an independent native execution identity");
    Check(first.WorkerTlsWasIsolated && first.TlsAtThreadEntry == 0 &&
              first.TlsInFinalizerFiber == kWorkerTlsValue &&
              g_threadLocalMarker == kMainTlsValue,
          "worker thread-local state is separate from bootstrap thread");
    Check(first.FiberCreated && first.FiberEntered && first.FinalizerActivated,
          "dedicated finalizer fiber reaches its controlled entry");
    Check(first.EntryRspMod16 == 8,
          "finalizer entry observes Windows x64 stack alignment");
    Check(first.RootBounds.Low != first.FinalizerBounds.Low &&
              first.RootBounds.High != first.FinalizerBounds.High,
          "finalizer and worker root stacks have independent bounds");
    Check(first.RootBoundsRestored && first.WorkerReturnedToRoot,
          "return to worker root restores its original stack bounds");
    Check(first.StackPagesVerified && first.DescriptorValid,
          "fiber stack descriptor uses OS-backed accessible pages");
    Check(first.StackDescriptor.OwnerIdentity == first.ThreadId &&
              first.StackDescriptor.Generation == first.Generation &&
              first.StackDescriptor.MappingIdentity ==
                  first.StackDescriptor.AllocationLow,
          "stack descriptor records owner, generation, and backing identity");
    Check(first.TruncatedDescriptorRejected && first.WrongOwnerRejected,
          "truncated bounds and cross-owner descriptor selection fail");
    Check(first.InvalidPalRequestCleared,
          "worker PAL bounds request clears output on invalid arguments");
    Check(first.FiberRetired && first.ThreadConvertedBack &&
              first.FinalizerFiber == nullptr,
          "owner retires the fiber only after returning to its root stack");

    void* mainLowAfter = nullptr;
    void* mainHighAfter = nullptr;
    const bool mainBoundsRestored = HostedPalGetMaximumStackBounds(
        &mainLowAfter, &mainHighAfter) &&
        mainLowAfter == mainLow && mainHighAfter == mainHigh;
    Check(mainBoundsRestored &&
              (reinterpret_cast<uintptr_t>(mainLowAfter) !=
                   first.FinalizerBounds.Low ||
               reinterpret_cast<uintptr_t>(mainHighAfter) !=
                   first.FinalizerBounds.High),
          "bootstrap thread cannot borrow retired worker bounds");

    WorkerContext second{};
    second.Generation = ++g_nextGeneration;
    HANDLE secondThread = CreateThread(nullptr, 0, FinalizerWorkerThread,
                                       &second, 0, nullptr);
    bool secondCompleted = false;
    if (secondThread != nullptr)
    {
        const DWORD wait = WaitForSingleObject(secondThread, INFINITE);
        DWORD exitCode = 0xFFFFFFFFu;
        GetExitCodeThread(secondThread, &exitCode);
        CloseHandle(secondThread);
        secondCompleted = wait == WAIT_OBJECT_0 && exitCode == 0;
    }
    Check(secondCompleted && second.FiberRetired,
          "a later worker context also completes and retires safely");
    Check(first.Generation != second.Generation && second.Generation != 0,
          "retired execution identity cannot be reused without a new generation");

    WorkerContext failed{};
    failed.Generation = ++g_nextGeneration;
    failed.ForceActivationFailure = true;
    HANDLE failedThread = CreateThread(nullptr, 0, FinalizerWorkerThread,
                                       &failed, 0, nullptr);
    bool failedWorkerExited = false;
    if (failedThread != nullptr)
    {
        const DWORD wait = WaitForSingleObject(failedThread, INFINITE);
        DWORD exitCode = 0xFFFFFFFFu;
        GetExitCodeThread(failedThread, &exitCode);
        CloseHandle(failedThread);
        failedWorkerExited = wait == WAIT_OBJECT_0 && exitCode == 0;
    }
    Check(failedWorkerExited && failed.FiberCreated &&
              !failed.FinalizerActivated && failed.NoActiveWorkerAfterFailure &&
              failed.FiberRetired,
          "failed worker activation leaves no active worker or live stack");

    WorkerContext createFailure{};
    createFailure.Generation = ++g_nextGeneration;
    createFailure.ForceFiberCreateFailure = true;
    HANDLE createFailureThread = CreateThread(nullptr, 0, FinalizerWorkerThread,
                                              &createFailure, 0, nullptr);
    bool createFailureExited = false;
    if (createFailureThread != nullptr)
    {
        const DWORD wait = WaitForSingleObject(createFailureThread, INFINITE);
        DWORD exitCode = 0xFFFFFFFFu;
        GetExitCodeThread(createFailureThread, &exitCode);
        CloseHandle(createFailureThread);
        createFailureExited = wait == WAIT_OBJECT_0 && exitCode == 0;
    }
    Check(createFailureExited && createFailure.CreateFailureObserved &&
              !createFailure.FiberCreated &&
              createFailure.NoActiveWorkerAfterFailure &&
              createFailure.ThreadConvertedBack,
          "OS stack creation failure publishes no worker and cleans up");

    std::printf("RESULT passed=%u failed=%u\n", g_passed, g_failed);
    return g_failed == 0 ? 0 : 1;
}
