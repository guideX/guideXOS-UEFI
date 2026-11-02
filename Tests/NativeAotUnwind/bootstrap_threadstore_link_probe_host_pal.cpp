#include <cstdint>

#include "bootstrap_threadstore_link_probe_host_pal.h"
#include "bootstrap_threadstore_link_probe_winapi.h"

namespace {

uint32_t g_threadStoreFls = kHostFlSOutOfIndexes;
bool g_rejectHijackCallbackRegistration = false;
void* g_registeredHijackCallback = nullptr;

[[noreturn]] void FailFastHostPal()
{
    ExitProcess(90);
}

} // namespace

extern "C" bool HostPalPrepareThreadServices()
{
    if (g_threadStoreFls != kHostFlSOutOfIndexes)
        return false;

    g_threadStoreFls = FlsAlloc(nullptr);
    return g_threadStoreFls != kHostFlSOutOfIndexes;
}

extern "C" void HostPalRejectHijackCallbackRegistration(bool reject)
{
    g_rejectHijackCallbackRegistration = reject;
}

extern "C" void* HostPalGetAttachedThread()
{
    if (g_threadStoreFls == kHostFlSOutOfIndexes)
        return nullptr;
    return FlsGetValue(g_threadStoreFls);
}

extern "C" int32_t __stdcall PalRegisterHijackCallback(void* callback)
{
    if (callback == nullptr || g_rejectHijackCallbackRegistration)
        return 0;

    if (g_registeredHijackCallback != nullptr)
        return g_registeredHijackCallback == callback;

    g_registeredHijackCallback = callback;
    return 1;
}

extern "C" void __stdcall PalAttachThread(void* thread)
{
    if (thread == nullptr || g_threadStoreFls == kHostFlSOutOfIndexes ||
        FlsGetValue(g_threadStoreFls) != nullptr ||
        !FlsSetValue(g_threadStoreFls, thread))
        FailFastHostPal();
}

extern "C" bool __stdcall PalDetachThread(void* thread)
{
    if (g_threadStoreFls == kHostFlSOutOfIndexes ||
        FlsGetValue(g_threadStoreFls) != thread)
        return false;

    return FlsSetValue(g_threadStoreFls, nullptr) != 0;
}

extern "C" uint64_t PalGetCurrentOSThreadId()
{
    return static_cast<uint64_t>(GetCurrentThreadId());
}
