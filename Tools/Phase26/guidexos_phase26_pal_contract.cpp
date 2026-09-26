#include "..\Phase19\guidexos_nativeaot_pal_contract.h"

typedef unsigned long long u64;
typedef unsigned int u32;
typedef int i32;
typedef unsigned char u8;

extern "C" u64 guidexos_pal_syscall5(u64 operation, u64 arg1, u64 arg2,
                                      u64 arg3, u64 arg4, u64 arg5);

namespace {
    enum : u64 {
        VmReserve = 0x20,
        VmCommit = 0x21,
        VmProtect = 0x22,
        VmRelease = 0x23,
        VmQuery = 0x24,
        TlsInitialize = 0x30,
        TlsCurrentBlock = 0x31,
        FlsAlloc = 0x32,
        FlsGet = 0x33,
        FlsSet = 0x34,
        FlsCleanup = 0x35,
        ThreadId = 0x40,
        ThreadStackBounds = 0x41,
        ThreadRuntimeState = 0x42,
        MonotonicTicks = 0x50,
        MonotonicFrequency = 0x51,
        SystemTimeNs = 0x52,
        RandomBytes = 0x60,
        ProcessExit = 0x70,
        FailFast = 0x71,
        RuntimeDiagnostic = 0x72
    };

    static u64 Call(u64 operation, u64 a1 = 0, u64 a2 = 0, u64 a3 = 0,
                    u64 a4 = 0, u64 a5 = 0) {
        return guidexos_pal_syscall5(operation, a1, a2, a3, a4, a5);
    }
}

extern "C" {

void* guidexos_pal_vm_reserve(u64 size, u64 alignment) {
    return (void*)Call(VmReserve, size, alignment);
}
i32 guidexos_pal_vm_commit(void* address, u64 size, u32 protection) {
    return (i32)Call(VmCommit, (u64)address, size, protection);
}
i32 guidexos_pal_vm_protect(void* address, u64 size, u32 protection) {
    return (i32)Call(VmProtect, (u64)address, size, protection);
}
i32 guidexos_pal_vm_release(void* address, u64 size) {
    return (i32)Call(VmRelease, (u64)address, size);
}
i32 guidexos_pal_vm_query(void* address, u64* base, u64* size, u32* protection) {
    return (i32)Call(VmQuery, (u64)address, (u64)base, (u64)size,
                      (u64)protection);
}

i32 guidexos_pal_tls_initialize(void* process_state, u64 template_size,
                                u64 zero_size) {
    return (i32)Call(TlsInitialize, (u64)process_state, template_size,
                      zero_size);
}
void* guidexos_pal_tls_current_block(void) {
    return (void*)Call(TlsCurrentBlock);
}
i32 guidexos_pal_fls_alloc(void (*cleanup)(void*)) {
    return (i32)Call(FlsAlloc, (u64)cleanup);
}
void* guidexos_pal_fls_get(i32 slot) {
    return (void*)Call(FlsGet, (u64)(long long)slot);
}
i32 guidexos_pal_fls_set(i32 slot, void* value) {
    return (i32)Call(FlsSet, (u64)(long long)slot, (u64)value);
}
void guidexos_pal_fls_cleanup(void) {
    (void)Call(FlsCleanup);
}

u64 guidexos_pal_thread_id(void) {
    return Call(ThreadId);
}
i32 guidexos_pal_thread_stack_bounds(void** low, void** high) {
    return (i32)Call(ThreadStackBounds, (u64)low, (u64)high);
}
void* guidexos_pal_thread_runtime_state(void) {
    return (void*)Call(ThreadRuntimeState);
}

u64 guidexos_pal_monotonic_ticks(void) {
    return Call(MonotonicTicks);
}
u64 guidexos_pal_monotonic_frequency(void) {
    return Call(MonotonicFrequency);
}
u64 guidexos_pal_system_time_ns(void) {
    return Call(SystemTimeNs);
}

i32 guidexos_pal_random_bytes(void* buffer, u64 length) {
    return (i32)Call(RandomBytes, (u64)buffer, length);
}
void guidexos_pal_process_exit(i32 status) {
    (void)Call(ProcessExit, (u64)(long long)status);
    for (;;) { }
}
void guidexos_pal_fail_fast(u32 reason, void* context) {
    (void)Call(FailFast, reason, (u64)context);
    for (;;) { }
}
void guidexos_pal_runtime_diagnostic(u64 marker) {
    // Temporarily retain the diagnostic syscall while auditing the first
    // target-native P/Invoke fixups.  The resolver emits only a bounded sample;
    // the source-overlay breadcrumbs remain otherwise quiet.
    (void)Call(RuntimeDiagnostic, marker);
}

void* guidexos_pal_memcpy(void* destination, const void* source, u64 length) {
    volatile u8* d = (volatile u8*)destination;
    const volatile u8* s = (const volatile u8*)source;
    for (u64 i = 0; i < length; ++i) d[i] = s[i];
    return destination;
}
void* guidexos_pal_memmove(void* destination, const void* source, u64 length) {
    volatile u8* d = (volatile u8*)destination;
    const volatile u8* s = (const volatile u8*)source;
    if (d == s) return destination;
    if (d < s) for (u64 i = 0; i < length; ++i) d[i] = s[i];
    else for (u64 i = length; i != 0; --i) d[i - 1] = s[i - 1];
    return destination;
}
void* guidexos_pal_memset(void* destination, u8 value, u64 length) {
    volatile u8* d = (volatile u8*)destination;
    for (u64 i = 0; i < length; ++i) d[i] = value;
    return destination;
}
i32 guidexos_pal_memcmp(const void* left, const void* right, u64 length) {
    const volatile u8* a = (const volatile u8*)left;
    const volatile u8* b = (const volatile u8*)right;
    for (u64 i = 0; i < length; ++i) {
        if (a[i] < b[i]) return -1;
        if (a[i] > b[i]) return 1;
    }
    return 0;
}
u64 guidexos_pal_strlen(const char* value) {
    if (!value) return 0;
    const volatile char* text = (const volatile char*)value;
    u64 length = 0;
    while (text[length] != 0) ++length;
    return length;
}

} // extern "C"
