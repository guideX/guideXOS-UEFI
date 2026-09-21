#pragma once

// Phase 19 contract-only surface. These declarations intentionally use no
// Windows, CRT, C++ runtime, scheduler-object, or kernel-pointer types.

typedef unsigned char guidexos_u8;
typedef unsigned int guidexos_u32;
typedef signed int guidexos_i32;
typedef unsigned long long guidexos_u64;

#ifdef __cplusplus
extern "C" {
#endif

void* guidexos_pal_vm_reserve(guidexos_u64 size, guidexos_u64 alignment);
guidexos_i32 guidexos_pal_vm_commit(void* address, guidexos_u64 size, guidexos_u32 protection);
guidexos_i32 guidexos_pal_vm_protect(void* address, guidexos_u64 size, guidexos_u32 protection);
guidexos_i32 guidexos_pal_vm_release(void* address, guidexos_u64 size);
guidexos_i32 guidexos_pal_vm_query(void* address, guidexos_u64* base, guidexos_u64* size, guidexos_u32* protection);

guidexos_i32 guidexos_pal_tls_initialize(void* process_state, guidexos_u64 template_size, guidexos_u64 zero_size);
void* guidexos_pal_tls_current_block(void);
guidexos_i32 guidexos_pal_fls_alloc(void (*cleanup)(void*));
void* guidexos_pal_fls_get(guidexos_i32 slot);
guidexos_i32 guidexos_pal_fls_set(guidexos_i32 slot, void* value);
void guidexos_pal_fls_cleanup(void);

guidexos_u64 guidexos_pal_thread_id(void);
guidexos_i32 guidexos_pal_thread_stack_bounds(void** low, void** high);
void* guidexos_pal_thread_runtime_state(void);

guidexos_u64 guidexos_pal_monotonic_ticks(void);
guidexos_u64 guidexos_pal_monotonic_frequency(void);
guidexos_u64 guidexos_pal_system_time_ns(void);

guidexos_i32 guidexos_pal_random_bytes(void* buffer, guidexos_u64 length);
void guidexos_pal_process_exit(guidexos_i32 status);
void guidexos_pal_fail_fast(guidexos_u32 reason, void* context);

void* guidexos_pal_memcpy(void* destination, const void* source, guidexos_u64 length);
void* guidexos_pal_memmove(void* destination, const void* source, guidexos_u64 length);
void* guidexos_pal_memset(void* destination, guidexos_u8 value, guidexos_u64 length);
guidexos_i32 guidexos_pal_memcmp(const void* left, const void* right, guidexos_u64 length);
guidexos_u64 guidexos_pal_strlen(const char* value);

#ifdef __cplusplus
}
#endif
