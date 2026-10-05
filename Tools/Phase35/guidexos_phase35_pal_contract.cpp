#include "..\Phase31\guidexos_phase31_pal_contract.cpp"

typedef unsigned long long phase35_u64;

extern "C" phase35_u64 guidexos_pal_syscall5(
    phase35_u64 operation, phase35_u64 arg1, phase35_u64 arg2,
    phase35_u64 arg3, phase35_u64 arg4, phase35_u64 arg5);

// Keep Persistent storage on its own Ring 3 operation. It cannot be used to
// select arbitrary Phase 10 service methods.
extern "C" phase35_u64 guidexos_pal_persistent_storage_read(
    phase35_u64 request, phase35_u64 requestLength) {
    return guidexos_pal_syscall5(7, request, requestLength, 0, 0, 0);
}
