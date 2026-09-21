#include "guidexos_nativeaot_pal_contract.h"

// These are linkable contract stubs, not a runtime implementation. They make
// the Phase 20 ABI explicit while failing closed and avoiding CRT/Win32 calls.

void* guidexos_pal_vm_reserve(guidexos_u64, guidexos_u64) { return 0; }
guidexos_i32 guidexos_pal_vm_commit(void*, guidexos_u64, guidexos_u32) { return -1; }
guidexos_i32 guidexos_pal_vm_protect(void*, guidexos_u64, guidexos_u32) { return -1; }
guidexos_i32 guidexos_pal_vm_release(void*, guidexos_u64) { return -1; }
guidexos_i32 guidexos_pal_vm_query(void*, guidexos_u64* base, guidexos_u64* size, guidexos_u32* protection)
{
    if (base) *base = 0;
    if (size) *size = 0;
    if (protection) *protection = 0;
    return -1;
}

guidexos_i32 guidexos_pal_tls_initialize(void*, guidexos_u64, guidexos_u64) { return -1; }
void* guidexos_pal_tls_current_block(void) { return 0; }
guidexos_i32 guidexos_pal_fls_alloc(void (*)(void*)) { return -1; }
void* guidexos_pal_fls_get(guidexos_i32) { return 0; }
guidexos_i32 guidexos_pal_fls_set(guidexos_i32, void*) { return -1; }
void guidexos_pal_fls_cleanup(void) { }

guidexos_u64 guidexos_pal_thread_id(void) { return 0; }
guidexos_i32 guidexos_pal_thread_stack_bounds(void** low, void** high)
{
    if (low) *low = 0;
    if (high) *high = 0;
    return -1;
}
void* guidexos_pal_thread_runtime_state(void) { return 0; }

guidexos_u64 guidexos_pal_monotonic_ticks(void) { return 0; }
guidexos_u64 guidexos_pal_monotonic_frequency(void) { return 0; }
guidexos_u64 guidexos_pal_system_time_ns(void) { return 0; }

guidexos_i32 guidexos_pal_random_bytes(void*, guidexos_u64) { return -1; }
void guidexos_pal_process_exit(guidexos_i32) { for (;;) { } }
void guidexos_pal_fail_fast(guidexos_u32, void*) { for (;;) { } }

void* guidexos_pal_memcpy(void* destination, const void* source, guidexos_u64 length)
{
    guidexos_u8* d = (guidexos_u8*)destination;
    const guidexos_u8* s = (const guidexos_u8*)source;
    for (guidexos_u64 i = 0; i < length; ++i) d[i] = s[i];
    return destination;
}

void* guidexos_pal_memmove(void* destination, const void* source, guidexos_u64 length)
{
    guidexos_u8* d = (guidexos_u8*)destination;
    const guidexos_u8* s = (const guidexos_u8*)source;
    if (d == s) return destination;
    if (d < s) {
        for (guidexos_u64 i = 0; i < length; ++i) d[i] = s[i];
    } else {
        for (guidexos_u64 i = length; i != 0; --i) d[i - 1] = s[i - 1];
    }
    return destination;
}

void* guidexos_pal_memset(void* destination, guidexos_u8 value, guidexos_u64 length)
{
    guidexos_u8* d = (guidexos_u8*)destination;
    for (guidexos_u64 i = 0; i < length; ++i) d[i] = value;
    return destination;
}

guidexos_i32 guidexos_pal_memcmp(const void* left, const void* right, guidexos_u64 length)
{
    const guidexos_u8* a = (const guidexos_u8*)left;
    const guidexos_u8* b = (const guidexos_u8*)right;
    for (guidexos_u64 i = 0; i < length; ++i) {
        if (a[i] < b[i]) return -1;
        if (a[i] > b[i]) return 1;
    }
    return 0;
}

guidexos_u64 guidexos_pal_strlen(const char* value)
{
    if (!value) return 0;
    guidexos_u64 length = 0;
    while (value[length] != 0) ++length;
    return length;
}
