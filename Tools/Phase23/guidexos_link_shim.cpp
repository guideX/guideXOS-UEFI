// Phase 23 GUIDEXOS runtime ABI veneer.
//
// The pinned NativeAOT runtime still contains a bounded set of legacy
// Redhawk/PAL and compiler-support symbol names.  This object is the single
// target-side adapter for those names.  It contains no Windows imports and
// forwards the platform primitives to the Phase 19 guideXOS PAL contract.
// Unsupported operations fail closed or return the documented non-success
// result; this object is not an execution authorization.

#include "..\Phase19\guidexos_nativeaot_pal_contract.h"

typedef unsigned char u8;
typedef unsigned int u32;
typedef unsigned long long u64;
typedef long long i64;
typedef int i32;
typedef unsigned short u16;
typedef void* HANDLE;
typedef int BOOL;

struct FILETIME { u32 dwLowDateTime; u32 dwHighDateTime; };
struct LARGE_INTEGER { i64 QuadPart; };
struct CRITICAL_SECTION { u64 opaque[8]; };
struct EXCEPTION_RECORD;
struct CONTEXT;

namespace std
{
    struct nothrow_t { };
    extern const nothrow_t nothrow = {};
}

static void* shim_alloc(u64 size)
{
    void* address = guidexos_pal_vm_reserve(size, 0x1000);
    if (address != 0)
        (void)guidexos_pal_vm_commit(address, size, 0x04);
    return address;
}

void* operator new(u64 size, const std::nothrow_t&) { return shim_alloc(size); }
void* operator new[](u64 size, const std::nothrow_t&) { return shim_alloc(size); }
void operator delete(void* address, u64) { if (address) (void)guidexos_pal_vm_release(address, 0); }
void operator delete[](void* address) { if (address) (void)guidexos_pal_vm_release(address, 0); }

extern "C" {

volatile u64 __security_cookie = 0;
int _fltused = 0;
u32 _tls_index = 0;
__declspec(thread) u8 g_guidexos_tls_anchor = 0;

void __security_check_cookie(u64) { }
void __GSHandlerCheck(...) { }
void __report_rangecheckfailure() { }

void* memcpy(void* destination, const void* source, u64 length) { return guidexos_pal_memcpy(destination, source, length); }
void* memmove(void* destination, const void* source, u64 length) { return guidexos_pal_memmove(destination, source, length); }
void* memset(void* destination, int value, u64 length) { return guidexos_pal_memset(destination, (u8)value, length); }
i32 memcmp(const void* left, const void* right, u64 length) { return guidexos_pal_memcmp(left, right, length); }

int strcmp(const char* left, const char* right)
{
    if (left == right) return 0;
    if (!left) return -1;
    if (!right) return 1;
    while (*left && *left == *right) { ++left; ++right; }
    return (u8)*left < (u8)*right ? -1 : ((u8)*left > (u8)*right ? 1 : 0);
}

int _stricmp(const char* left, const char* right) { return strcmp(left, right); }
char* strcpy(char* destination, const char* source)
{
    char* result = destination;
    if (!destination || !source) return destination;
    while ((*destination++ = *source++) != 0) { }
    return result;
}

const char* strstr(const char* value, const char* needle)
{
    if (!value || !needle) return 0;
    if (*needle == 0) return value;
    for (; *value; ++value) {
        const char* a = value;
        const char* b = needle;
        while (*a && *b && *a == *b) { ++a; ++b; }
        if (*b == 0) return value;
    }
    return 0;
}

u64 strtoull(const char* value, char** end, int base)
{
    if (!value) { if (end) *end = (char*)value; return 0; }
    while (*value == ' ' || *value == '\t') ++value;
    if (base == 0) base = (value[0] == '0' && (value[1] == 'x' || value[1] == 'X')) ? 16 : 10;
    if (base == 16 && value[0] == '0' && (value[1] == 'x' || value[1] == 'X')) value += 2;
    u64 result = 0;
    const char* first = value;
    while (*value) {
        int digit = (*value >= '0' && *value <= '9') ? *value - '0' :
                    (*value >= 'a' && *value <= 'f') ? *value - 'a' + 10 :
                    (*value >= 'A' && *value <= 'F') ? *value - 'A' + 10 : -1;
        if (digit < 0 || digit >= base) break;
        result = result * (u64)base + (u64)digit;
        ++value;
    }
    if (end) *end = (char*)(value == first ? first : value);
    return result;
}

unsigned long strtoul(const char* value, char** end, int base) { return (unsigned long)strtoull(value, end, base); }

BOOL CloseHandle(HANDLE) { return 1; }
BOOL DuplicateHandle(HANDLE, HANDLE, HANDLE, HANDLE* result, u32, BOOL, u32) { if (result) *result = 0; return 0; }
HANDLE GetCurrentProcess() { return (HANDLE)(u64)-1; }
HANDLE GetCurrentThread() { return (HANDLE)(u64)-2; }
BOOL FlushProcessWriteBuffers() { return 1; }
BOOL InitializeCriticalSectionEx(CRITICAL_SECTION*, u32, u32) { return 1; }
void DeleteCriticalSection(CRITICAL_SECTION*) { }
void EnterCriticalSection(CRITICAL_SECTION*) { }
void LeaveCriticalSection(CRITICAL_SECTION*) { }
BOOL SetEvent(HANDLE) { return 1; }
BOOL ResetEvent(HANDLE) { return 1; }
u32 WaitForSingleObjectEx(HANDLE, u32, BOOL) { return 258; }
u32 GetEnvironmentVariableA(const char*, char*, u32) { return 0; }

u64 GetEnabledXStateFeatures() { return 0; }
u32 SleepEx(u32, BOOL) { return 0; }

u64 __stdcall QueryPerformanceCounter(LARGE_INTEGER* value)
{
    if (value) value->QuadPart = (i64)guidexos_pal_monotonic_ticks();
    return 1;
}
u64 __stdcall QueryPerformanceFrequency(LARGE_INTEGER* value)
{
    if (value) value->QuadPart = (i64)guidexos_pal_monotonic_frequency();
    return 1;
}
u64 (*__imp_QueryPerformanceCounter)(LARGE_INTEGER*) = QueryPerformanceCounter;
u64 (*__imp_QueryPerformanceFrequency)(LARGE_INTEGER*) = QueryPerformanceFrequency;
u32 (*__imp_SleepEx)(u32, BOOL) = SleepEx;
u64 (*__imp_GetEnabledXStateFeatures)() = GetEnabledXStateFeatures;

void GetSystemTimeAsFileTime(FILETIME* value)
{
    u64 ticks = guidexos_pal_system_time_ns() / 100;
    if (value) { value->dwLowDateTime = (u32)ticks; value->dwHighDateTime = (u32)(ticks >> 32); }
}

BOOL RaiseFailFastException(EXCEPTION_RECORD*, CONTEXT*, u32) { guidexos_pal_fail_fast(0, 0); return 0; }

u32 PalGetProcessCpuCount() { return 1; }
BOOL PalInit() { return 1; }
void PalAttachThread(void*) { }
BOOL PalDetachThread(void*) { return 1; }
u64 PalGetCurrentOSThreadId() { return guidexos_pal_thread_id(); }
u64 PalQueryPerformanceCounter() { return guidexos_pal_monotonic_ticks(); }
u64 PalQueryPerformanceFrequency() { return guidexos_pal_monotonic_frequency(); }
u64 PalGetTickCount64() { return guidexos_pal_monotonic_ticks(); }
void PalSleep(u32) { }
BOOL PalSwitchToThread() { return 0; }
u32 PalCompatibleWaitAny(BOOL, u32, u32, HANDLE*, BOOL) { return 258; }
HANDLE PalCreateEventW(void*, BOOL, BOOL, const u16*) { return 0; }
HANDLE PalCreateLowMemoryResourceNotification() { return 0; }
BOOL PalAreShadowStacksEnabled() { return 0; }
BOOL PalRegisterHijackCallback(void*) { return 1; }
void PalHijack(HANDLE, void*) { }
void* PalGetHijackTarget(void* target) { return target; }
BOOL PalStartBackgroundGCThread(void*, void*, BOOL, const char*) { return 0; }
BOOL PalStartFinalizerThread(void*, void*, BOOL, const char*) { return 0; }
BOOL PalGetMaximumStackBounds(void** low, void** high) { if (low) *low = 0; if (high) *high = 0; return 0; }
void PalGetModuleBounds(HANDLE module, u8** low, u8** high) { if (low) *low = (u8*)module; if (high) *high = (u8*)module; }
int PalGetModuleFileName(const char** name, HANDLE) { if (name) *name = 0; return 0; }
HANDLE PalGetModuleHandleFromPointer(void* pointer) { return pointer; }
void PalPrintFatalError(const char*) { guidexos_pal_fail_fast(0, 0); }
char* PalCopyTCharAsChar(const char* value) { return (char*)value; }
HANDLE PalLoadLibrary(const char*) { return 0; }
void* PalGetProcAddress(HANDLE, const char*) { return 0; }
void* PalVirtualAlloc(u64 size, u32) { return shim_alloc(size); }
void PalVirtualFree(void* address, u64 size) { if (address) (void)guidexos_pal_vm_release(address, size); }
BOOL PalVirtualProtect(void* address, u64 size, u32 protection) { return guidexos_pal_vm_protect(address, size, protection) == 0; }
void PalFlushInstructionCache(void*, u64) { }

void PopulateControlSegmentRegisters(void*) { }
BOOL PalGetCompleteThreadContext(HANDLE, CONTEXT*) { return 0; }
BOOL PalSetThreadContext(HANDLE, CONTEXT*) { return 0; }
void PalRestoreContext(CONTEXT*) { }
CONTEXT* PalAllocateCompleteOSContext(u8** buffer) { if (buffer) *buffer = 0; return 0; }

void NativeRuntimeEventSource_LogExceptionThrown(const u16*, const u16*, void*, i32) { }
BOOL RhRegisterOSModule(...) { return 1; }

} // extern "C"
