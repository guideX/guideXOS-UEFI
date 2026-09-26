#include "..\Phase26\guidexos_phase26_pal_contract.cpp"

extern "C" unsigned long long guidexos_pal_service_request(
    unsigned long long request, unsigned long long requestLength) {
    return guidexos_pal_syscall5(5, request, requestLength, 0, 0, 0);
}
