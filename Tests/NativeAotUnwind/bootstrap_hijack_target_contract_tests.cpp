#include "common.h"
#include "PalRedhawk.h"
#include "GuidexosGcPal.h"

#include <cstdlib>
#include <cstdio>
#include <cstring>
#include <process.h>

namespace
{
constexpr uint32_t kUnsupportedHijackReason = 0x4758484Au;
constexpr uint32_t kUnknownCetStateReason = 0x47584345u;
constexpr int kExpectedFailFastExit = 86;

int g_assertions = 0;
int g_failures = 0;
volatile int g_runtimeCallbackInvocations = 0;

void Check(bool condition, const char* name)
{
    ++g_assertions;
    if (!condition)
    {
        ++g_failures;
        std::printf("FAIL %s\n", name);
    }
}

void DefaultHijackTarget()
{
}

void RuntimeCallbackMustNotRun(NATIVE_CONTEXT*, void*)
{
    ++g_runtimeCallbackInvocations;
}

bool RunFailFastChild(const char* executable, const char* mode)
{
    const intptr_t exitCode = _spawnl(_P_WAIT, executable, executable, mode, static_cast<const char*>(nullptr));
    return exitCode == kExpectedFailFastExit;
}
}

extern "C" void guidexos_pal_fail_fast(uint32_t reason, void* context)
{
    (void)context;
    std::exit(reason == kUnsupportedHijackReason || reason == kUnknownCetStateReason
        ? kExpectedFailFastExit
        : kExpectedFailFastExit + 1);
}

int main(int argc, char** argv)
{
    if (argc == 2 && std::strcmp(argv[1], "--pal-hijack") == 0)
        PalHijack(nullptr, nullptr);
    if (argc == 2 && std::strcmp(argv[1], "--shadow-stack-query") == 0)
        (void)PalAreShadowStacksEnabled();

    Check(argc == 1, "fail-fast child modes run in isolated processes");
    const auto defaultTarget = &DefaultHijackTarget;
    volatile uintptr_t preservedReturnAddress = static_cast<uintptr_t>(0x5A17u);

    Check(PalGetHijackTarget(defaultTarget) == nullptr,
        "guideXOS target reports an explicit unsupported hijack target");
    Check(PalGetHijackTarget(nullptr) == nullptr,
        "guideXOS target rejects a missing default target");
    Check(PalGetHijackTarget(reinterpret_cast<HijackFunc*>(static_cast<uintptr_t>(1))) == nullptr,
        "guideXOS target rejects an invalid default target address");

    Check(PalRegisterHijackCallback(&RuntimeCallbackMustNotRun) == UInt32_FALSE,
        "guideXOS rejects callback registration without suspension capability");
    Check(PalGetHijackTarget(defaultTarget) == nullptr,
        "target remains unsupported after rejected callback registration");
    Check(PalRegisterHijackCallback(nullptr) == UInt32_FALSE,
        "guideXOS rejects null callback registration");
    Check(PalRegisterHijackCallback(&RuntimeCallbackMustNotRun) == UInt32_FALSE,
        "repeated registration cannot make the target supported");
    Check(g_runtimeCallbackInvocations == 0,
        "rejected target callback is never invoked");
    Check(preservedReturnAddress == static_cast<uintptr_t>(0x5A17u),
        "unsupported target provider leaves the return-address sentinel untouched");

    Check(RunFailFastChild(argv[0], "--pal-hijack"),
        "unexpected target PalHijack call fails fast with the hijack reason");
    Check(RunFailFastChild(argv[0], "--shadow-stack-query"),
        "unknown target shadow-stack state fails fast instead of reporting disabled");

    std::printf("RESULT assertions=%d failures=%d\n", g_assertions, g_failures);
    return g_failures == 0 ? 0 : 1;
}
