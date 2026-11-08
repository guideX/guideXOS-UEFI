#include "guidexOSBootInfo.h"
#include "fb_console.h"
#include <stdint.h>

using guideXOS::BootInfo;

// Simple framebuffer-based early panic that does not rely on heap, CRT or UEFI
[[noreturn]] void guideXOS::guidexos_early_panic(const BootInfo* bi)
{
    if (bi && fb_console::Init(bi))
    {
        fb_console::Write("guideXOS BootInfo invalid. Halting.\n");
    }

    for (;;) {
        for (volatile uint64_t i = 0; i < 100000000ULL; ++i) { }
    }
}
