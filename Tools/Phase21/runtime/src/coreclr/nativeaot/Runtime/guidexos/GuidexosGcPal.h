// Phase 21 guideXOS GC-facing subset of the bounded Phase 19 PAL.
#ifndef GUIDEXOS_GC_PAL_H
#define GUIDEXOS_GC_PAL_H

#include <cstdint>

extern "C"
{
    void* guidexos_pal_vm_reserve(uint64_t size, uint64_t alignment);
    int32_t guidexos_pal_vm_commit(void* address, uint64_t size, uint32_t protection);
    int32_t guidexos_pal_vm_release(void* address, uint64_t size);

    uint64_t guidexos_pal_thread_id(void);
    uint64_t guidexos_pal_monotonic_ticks(void);
    uint64_t guidexos_pal_monotonic_frequency(void);
    void guidexos_pal_fail_fast(uint32_t reason, void* context);
}

#endif // GUIDEXOS_GC_PAL_H
