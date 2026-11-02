#include <Windows.h>
#include <intrin.h>

#include <cstdint>
#include <cstdio>
#include <string>

namespace
{
int g_assertions = 0;
int g_failures = 0;

void Check(bool condition, const char* name)
{
    ++g_assertions;
    if (!condition)
    {
        ++g_failures;
        std::printf("FAIL %s\n", name);
    }
}

void DefaultHostHijackTarget()
{
}

bool IsExecutableAddress(const void* address)
{
    MEMORY_BASIC_INFORMATION information{};
    if (address == nullptr || VirtualQuery(address, &information, sizeof(information)) != sizeof(information))
        return false;
    if (information.State != MEM_COMMIT || (information.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
        return false;

    const DWORD protection = information.Protect & 0xff;
    return protection == PAGE_EXECUTE || protection == PAGE_EXECUTE_READ ||
        protection == PAGE_EXECUTE_READWRITE || protection == PAGE_EXECUTE_WRITECOPY;
}

using GetProcessMitigationPolicyFn = BOOL(WINAPI*)(HANDLE, int, PVOID, SIZE_T);
using QueueUserAPC2Fn = BOOL(WINAPI*)(PAPCFUNC, HANDLE, ULONG_PTR, ULONG);
using RtlGetReturnAddressHijackTargetFn = void*(NTAPI*)();
using HostHijackTarget = void (*)();

bool QueryHostShadowStackPolicy(bool* enabled)
{
    HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    auto query = reinterpret_cast<GetProcessMitigationPolicyFn>(
        GetProcAddress(kernel32, "GetProcessMitigationPolicy"));
    if (query == nullptr)
        return false;

    DWORD flags = 0;
    // PROCESS_MITIGATION_POLICY::ProcessUserShadowStackPolicy is ordinal 15.
    if (!query(GetCurrentProcess(), 15, &flags, sizeof(flags)))
        return false;

    *enabled = (flags & 1u) != 0;
    return true;
}

bool ProbeWindowsHostTargetSelection()
{
    bool policyEnabled = false;
    const bool policyAvailable = QueryHostShadowStackPolicy(&policyEnabled);
#if defined(_M_X64)
    int cpuInfo[4]{};
    __cpuid(cpuInfo, 0);
    const bool cpuidLeaf7Available = cpuInfo[0] >= 7;
    bool cpuSupportsShadowStacks = false;
    if (cpuidLeaf7Available)
    {
        __cpuidex(cpuInfo, 7, 0);
        cpuSupportsShadowStacks = (cpuInfo[2] & (1 << 7)) != 0;
    }
    const bool sspActive = cpuSupportsShadowStacks && _rdsspq() != 0;
#else
    const bool cpuSupportsShadowStacks = false;
    const bool sspActive = false;
#endif

    Check(policyAvailable, "Windows host shadow-stack policy query succeeds");
    if (!policyAvailable)
        return false;

    Check(policyEnabled == sspActive, "Windows mitigation policy agrees with active SSP state");
    const bool shadowStacksEnabled = policyEnabled && sspActive;

    HostHijackTarget selectedTarget = &DefaultHostHijackTarget;
    HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
    auto queueUserApc2 = reinterpret_cast<QueueUserAPC2Fn>(
        GetProcAddress(kernel32, "QueueUserAPC2"));
    auto getReturnTarget = reinterpret_cast<RtlGetReturnAddressHijackTargetFn>(
        GetProcAddress(ntdll, "RtlGetReturnAddressHijackTarget"));

    if (shadowStacksEnabled)
    {
        Check(queueUserApc2 != nullptr, "CET host provides special user APC service");
        Check(getReturnTarget != nullptr, "CET host provides Windows return-address hijack target API");
        if (queueUserApc2 == nullptr || getReturnTarget == nullptr)
        {
            selectedTarget = nullptr;
        }
        else
        {
            selectedTarget = reinterpret_cast<HostHijackTarget>(getReturnTarget());
        }
    }
    else
    {
        Check(selectedTarget == &DefaultHostHijackTarget,
            "non-CET Windows host retains the supplied default target");
    }

    Check(IsExecutableAddress(reinterpret_cast<const void*>(selectedTarget)),
        "selected Windows host target is an executable address");
    Check(!IsExecutableAddress(reinterpret_cast<const void*>(static_cast<uintptr_t>(1))),
        "invalid target address fails executable-address validation");

    std::printf("WINDOWS_CPU_CET_SS=%s\n", cpuSupportsShadowStacks ? "supported" : "unsupported");
    std::printf("WINDOWS_CET_POLICY=%s SSP_ACTIVE=%s QueueUserAPC2=%s RtlTarget=%s\n",
        policyEnabled ? "enabled" : "disabled",
        sspActive ? "active" : "inactive",
        queueUserApc2 != nullptr ? "available" : "missing",
        getReturnTarget != nullptr ? "available" : "missing");
    if (!shadowStacksEnabled)
        std::printf("CET_RETURN_TARGET_BEHAVIOR=UNVERIFIED (host process shadow stacks are disabled)\n");

    return !shadowStacksEnabled || selectedTarget != nullptr;
}

struct ApcObservation
{
    HANDLE ready;
    HANDLE stop;
    HANDLE completed;
    DWORD targetThreadId;
    DWORD callbackThreadId;
    volatile LONG callbackInvoked;
    volatile LONG contextPresent;
    volatile LONG contextInstructionPointerExecutable;
    volatile ULONG_PTR originalParameter;
};

struct ApcCallbackData
{
    ULONG_PTR Parameter;
    PCONTEXT ContextRecord;
    ULONG_PTR Reserved0;
    ULONG_PTR Reserved1;
};

constexpr ULONG kSpecialUserApcAndContextFlags = 0x00000001u | 0x00010000u;
constexpr ULONG_PTR kApcSentinelParameter = static_cast<ULONG_PTR>(0x23A14u);
ApcObservation* g_apcObservation = nullptr;

void NTAPI HostApcCallback(ULONG_PTR parameter)
{
    auto* data = reinterpret_cast<ApcCallbackData*>(parameter);
    ApcObservation* observation = g_apcObservation;
    if (observation != nullptr && data != nullptr)
    {
        observation->callbackThreadId = GetCurrentThreadId();
        observation->originalParameter = data->Parameter;
        if (data->ContextRecord != nullptr)
        {
            InterlockedExchange(&observation->contextPresent, 1);
#if defined(_M_X64)
            InterlockedExchange(&observation->contextInstructionPointerExecutable,
                IsExecutableAddress(reinterpret_cast<const void*>(data->ContextRecord->Rip)) ? 1 : 0);
#endif
        }
        InterlockedExchange(&observation->callbackInvoked, 1);
        SetEvent(observation->completed);
    }
}

DWORD WINAPI HostApcWorker(void* parameter)
{
    auto* observation = static_cast<ApcObservation*>(parameter);
    observation->targetThreadId = GetCurrentThreadId();
    SetEvent(observation->ready);
    while (WaitForSingleObject(observation->stop, 1) == WAIT_TIMEOUT)
        Sleep(1);
    return 0;
}

bool ProbeWindowsCallbackDelivery()
{
    HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    auto queueUserApc2 = reinterpret_cast<QueueUserAPC2Fn>(
        GetProcAddress(kernel32, "QueueUserAPC2"));
    if (queueUserApc2 == nullptr)
    {
        Check(false, "Windows host provides special user APC service for callback execution test");
        return false;
    }

    ApcObservation observation{};
    observation.ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    observation.stop = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    observation.completed = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (observation.ready == nullptr || observation.stop == nullptr || observation.completed == nullptr)
    {
        if (observation.ready != nullptr) CloseHandle(observation.ready);
        if (observation.stop != nullptr) CloseHandle(observation.stop);
        if (observation.completed != nullptr) CloseHandle(observation.completed);
        Check(false, "Windows host allocates APC worker synchronization objects");
        return false;
    }
    g_apcObservation = &observation;

    DWORD threadId = 0;
    HANDLE worker = CreateThread(nullptr, 0, HostApcWorker, &observation, 0, &threadId);
    if (worker == nullptr)
    {
        CloseHandle(observation.ready);
        CloseHandle(observation.stop);
        CloseHandle(observation.completed);
        g_apcObservation = nullptr;
        Check(false, "Windows host creates an OS-backed APC worker thread");
        return false;
    }

    const DWORD readyResult = WaitForSingleObject(observation.ready, 5000);
    Check(readyResult == WAIT_OBJECT_0, "Windows APC target thread reaches ready state");

    const BOOL queued = readyResult == WAIT_OBJECT_0
        ? queueUserApc2(&HostApcCallback, worker, kApcSentinelParameter, kSpecialUserApcAndContextFlags)
        : FALSE;
    const DWORD waitResult = queued ? WaitForSingleObject(observation.completed, 5000) : WAIT_FAILED;
    Check(queued != FALSE, "Windows special user APC with context queues successfully");
    Check(waitResult == WAIT_OBJECT_0, "Windows special user APC callback actually executes");
    if (waitResult == WAIT_OBJECT_0)
    {
        Check(observation.targetThreadId == threadId && observation.callbackThreadId == threadId,
            "special APC callback and context belong to the selected OS thread");
        Check(observation.callbackInvoked != 0, "callback execution is distinct from APC queueing");
        Check(observation.contextPresent != 0, "special APC supplies interrupted processor context");
        Check(observation.originalParameter == kApcSentinelParameter,
            "special APC preserves the caller parameter");
        Check(observation.contextInstructionPointerExecutable != 0,
            "captured Windows context RIP points into executable memory");
    }

    SetEvent(observation.stop);
    WaitForSingleObject(worker, 5000);
    CloseHandle(worker);
    CloseHandle(observation.ready);
    CloseHandle(observation.stop);
    CloseHandle(observation.completed);
    g_apcObservation = nullptr;
    return waitResult == WAIT_OBJECT_0;
}
}

int main()
{
    const bool targetSelectionPassed = ProbeWindowsHostTargetSelection();
    Check(targetSelectionPassed, "Windows host target selection probe passes");
    const bool callbackPassed = ProbeWindowsCallbackDelivery();
    Check(callbackPassed, "Windows host callback delivery probe passes");
    std::printf("RESULT assertions=%d failures=%d\n", g_assertions, g_failures);
    return g_failures == 0 ? 0 : 1;
}
