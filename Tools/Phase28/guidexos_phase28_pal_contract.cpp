#include "..\Phase27\guidexos_phase27_pal_contract.cpp"

typedef unsigned long long guidexos_phase28_u64;

extern "C" guidexos_phase28_u64 guidexos_pal_abi_version(void) {
    return guidexos_pal_syscall5(1, 0, 0, 0, 0, 0);
}

extern "C" guidexos_phase28_u64 guidexos_pal_application_identity(
    void* response, guidexos_phase28_u64 responseCapacity) {
    return guidexos_pal_syscall5(6, (guidexos_phase28_u64)response,
                                  responseCapacity, 0, 0, 0);
}
