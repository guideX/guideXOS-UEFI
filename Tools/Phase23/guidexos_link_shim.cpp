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

extern "C" void* _ReturnAddress(void);
#pragma intrinsic(_ReturnAddress)

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
    // NativeAOT uses zero-byte nothrow allocations for empty runtime records;
    // the PAL contract still requires a unique, page-backed non-null result.
    if (size == 0) size = 1;
    void* address = guidexos_pal_vm_reserve(size, 0x1000);
    if (address == 0) {
        return 0;
    }
    if (guidexos_pal_vm_commit(address, size, 0x04) != 0) {
        (void)guidexos_pal_vm_release(address, 0);
        return 0;
    }
    return address;
}

void* operator new(u64 size, const std::nothrow_t&) { return shim_alloc(size); }
void* operator new[](u64 size, const std::nothrow_t&) { return shim_alloc(size); }
void operator delete(void* address, u64) { if (address) (void)guidexos_pal_vm_release(address, 0); }
void operator delete[](void* address) { if (address) (void)guidexos_pal_vm_release(address, 0); }

extern "C" {

// The NativeAOT bootstrapper asks the PAL for the image containing its
// managed entrypoint.  GUIDEXOS has no host loader, so resolve that request
// directly to the linker-defined image base instead of returning the
// entrypoint address itself.
extern unsigned char __ImageBase;

volatile u64 __security_cookie = 0;
int _fltused = 0;
u32 _tls_index = 0;
__declspec(thread) u8 g_guidexos_tls_anchor = 0;

void __security_check_cookie(u64) { }
void __GSHandlerCheck(...) { }
void __report_rangecheckfailure() { }
int _purecall() { guidexos_pal_fail_fast(0, _ReturnAddress()); return 0; }

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
static u32 g_guidexos_last_error = 0;
u32 GetLastError() { return g_guidexos_last_error; }
void SetLastError(u32 value) { g_guidexos_last_error = value; }
u32 GetEnvironmentVariableW(const u16*, u16*, u32) { return 0; }
u32 GetModuleFileNameW(HANDLE, u16* buffer, u32 capacity)
{
    // NativeAOT asks for the process path while constructing the managed
    // command-line state.  GUIDEXOS has no host loader path, so expose a
    // bounded target-owned identity that is sufficient for Environment's
    // startup contract and never leaves the P/Invoke cell unresolved.
    static const u16 name[] = {
        'g','u','i','d','e','X','O','S','.',
        'P','h','a','s','e','2','6','M','a','n','a','g','e','d',
        'P','r','o','o','f','.', 'e','x','e'
    };
    const u32 length = (u32)(sizeof(name) / sizeof(name[0]));
    if (buffer == 0 || capacity == 0) return 0;
    if (capacity <= length) {
        for (u32 i = 0; i + 1 < capacity; ++i) buffer[i] = name[i];
        buffer[capacity - 1] = 0;
        return capacity;
    }
    for (u32 i = 0; i < length; ++i) buffer[i] = name[i];
    buffer[length] = 0;
    return length;
}

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

BOOL RaiseFailFastException(EXCEPTION_RECORD*, CONTEXT*, u32 flags)
{
    guidexos_pal_fail_fast(flags, _ReturnAddress());
    return 0;
}

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
// Phase 26 executes a single managed lifetime synchronously.  The NativeAOT
// workstation-GC startup still requires non-null event/thread identities even
// though no helper thread is scheduled in this proof.  The PAL wait/set shims
// below are bounded stubs, so these opaque identities cannot block the kernel.
HANDLE PalCreateEventW(void*, BOOL, BOOL, const u16*) { return (HANDLE)(u64)1; }
HANDLE PalCreateLowMemoryResourceNotification() { return 0; }
BOOL PalAreShadowStacksEnabled() { return 0; }
BOOL PalRegisterHijackCallback(void*) { return 1; }
void PalHijack(HANDLE, void*) { }
void* PalGetHijackTarget(void* target) { return target; }
HANDLE PalStartBackgroundGCThread(void*, void*, BOOL, const char*) { return (HANDLE)(u64)1; }
HANDLE PalStartFinalizerThread(void*, void*, BOOL, const char*) { return (HANDLE)(u64)1; }
BOOL PalGetMaximumStackBounds(void** low, void** high)
{
    return guidexos_pal_thread_stack_bounds(low, high) == 0;
}
void PalGetModuleBounds(HANDLE module, u8** low, u8** high) { if (low) *low = (u8*)module; if (high) *high = (u8*)module; }
int PalGetModuleFileName(const char** name, HANDLE) { if (name) *name = 0; return 0; }
HANDLE PalGetModuleHandleFromPointer(void*) { return (HANDLE)&__ImageBase; }
void PalPrintFatalError(const char* message)
{
    guidexos_pal_fail_fast(0, (void*)message);
}
char* PalCopyTCharAsChar(const char* value) { return (char*)value; }
static bool shim_name_is(const char* value, const char* expected)
{
    if (!value || !expected) return false;
    while (*value && *expected && *value == *expected) {
        ++value;
        ++expected;
    }
    return *value == 0 && *expected == 0;
}

// The first managed entry path needs one kernel32 P/Invoke cell while the
// NativeAOT resolver initializes its module table.  Keep resolution entirely
// inside the image: no host loader, filesystem lookup, or unbounded fallback.
HANDLE PalLoadLibrary(const char*) { return (HANDLE)(u64)1; }
void* GuidexosVirtualAlloc(void* address, u64 size, u32 allocationType, u32 protection);
int GuidexosVirtualFree(void* address, u64 size, u32 freeType);
int GuidexosVirtualProtect(void* address, u64 size, u32 protection, u32* oldProtection);
int GuidexosBCryptGenRandom(HANDLE algorithmProvider, u8* buffer, u32 length, u32 flags);
HANDLE GuidexosCreateEventExW(void* attributes, const u16* name, u32 flags, u32 access);
u32 GuidexosFormatMessageW(u32 flags, const void* source, u32 messageId,
                           u32 languageId, u16* buffer, u32 size, void* arguments);
u32 GuidexosGetConsoleOutputCP();
void GuidexosGetCurrentProcessorNumberEx(void* processorNumber);
HANDLE GuidexosGetStdHandle(u32 standardHandle);
i32 GuidexosGetThreadPriority(HANDLE thread);
BOOL GuidexosIsDebuggerPresent();
void* GuidexosLocalFree(void* address);
i32 GuidexosMultiByteToWideChar(u32 codePage, u32 flags, const char* source,
                                i32 sourceLength, u16* destination, i32 destinationLength);
void GuidexosSleep(u32 milliseconds);
u32 GuidexosWaitForMultipleObjectsEx(u32 count, const HANDLE* handles,
                                     BOOL waitAll, u32 milliseconds, BOOL alertable);
BOOL GuidexosWriteFile(HANDLE file, const void* buffer, u32 length,
                       u32* written, void* overlapped);
i32 GuidexosWideCharToMultiByte(u32 codePage, u32 flags, const u16* source,
                                i32 sourceLength, char* destination, i32 destinationLength,
                                const char* defaultChar, BOOL* usedDefaultChar);
void* PalGetProcAddress(HANDLE module, const char* functionName)
{
    // GUIDEXOS has no host loader module identity. NativeAOT still passes the
    // PAL's opaque module slot here, but target resolution is name-based and
    // must remain valid while that slot is zero during first-cell fixup.
    (void)module;
    if (functionName == 0) return 0;
    if (shim_name_is(functionName, "GetLastError"))
        return (void*)&GetLastError;
    if (shim_name_is(functionName, "SetLastError"))
        return (void*)&SetLastError;
    if (shim_name_is(functionName, "GetModuleFileNameW"))
        return (void*)&GetModuleFileNameW;
    if (shim_name_is(functionName, "GetProcAddress"))
        return (void*)&PalGetProcAddress;
    if (shim_name_is(functionName, "LoadLibraryExW"))
        return (void*)&PalLoadLibrary;
    if (shim_name_is(functionName, "GetEnvironmentVariableW"))
        return (void*)&GetEnvironmentVariableW;
    if (shim_name_is(functionName, "QueryPerformanceCounter"))
        return (void*)&QueryPerformanceCounter;
    if (shim_name_is(functionName, "QueryPerformanceFrequency"))
        return (void*)&QueryPerformanceFrequency;
    if (shim_name_is(functionName, "GetCurrentProcess"))
        return (void*)&GetCurrentProcess;
    if (shim_name_is(functionName, "GetCurrentThread"))
        return (void*)&GetCurrentThread;
    if (shim_name_is(functionName, "CloseHandle"))
        return (void*)&CloseHandle;
    if (shim_name_is(functionName, "SetEvent"))
        return (void*)&SetEvent;
    if (shim_name_is(functionName, "ResetEvent"))
        return (void*)&ResetEvent;
    if (shim_name_is(functionName, "FreeLibrary"))
        return (void*)&CloseHandle;
    if (shim_name_is(functionName, "VirtualAlloc"))
        return (void*)&GuidexosVirtualAlloc;
    if (shim_name_is(functionName, "VirtualFree"))
        return (void*)&GuidexosVirtualFree;
    if (shim_name_is(functionName, "VirtualProtect"))
        return (void*)&GuidexosVirtualProtect;
    if (shim_name_is(functionName, "BCryptGenRandom"))
        return (void*)&GuidexosBCryptGenRandom;
    if (shim_name_is(functionName, "CreateEventExW"))
        return (void*)&GuidexosCreateEventExW;
    if (shim_name_is(functionName, "FormatMessageW"))
        return (void*)&GuidexosFormatMessageW;
    if (shim_name_is(functionName, "GetConsoleOutputCP"))
        return (void*)&GuidexosGetConsoleOutputCP;
    if (shim_name_is(functionName, "GetCurrentProcessorNumberEx"))
        return (void*)&GuidexosGetCurrentProcessorNumberEx;
    if (shim_name_is(functionName, "GetStdHandle"))
        return (void*)&GuidexosGetStdHandle;
    if (shim_name_is(functionName, "GetThreadPriority"))
        return (void*)&GuidexosGetThreadPriority;
    if (shim_name_is(functionName, "IsDebuggerPresent"))
        return (void*)&GuidexosIsDebuggerPresent;
    if (shim_name_is(functionName, "LocalFree"))
        return (void*)&GuidexosLocalFree;
    if (shim_name_is(functionName, "MultiByteToWideChar"))
        return (void*)&GuidexosMultiByteToWideChar;
    if (shim_name_is(functionName, "Sleep"))
        return (void*)&GuidexosSleep;
    if (shim_name_is(functionName, "WaitForMultipleObjectsEx"))
        return (void*)&GuidexosWaitForMultipleObjectsEx;
    if (shim_name_is(functionName, "WriteFile"))
        return (void*)&GuidexosWriteFile;
    if (shim_name_is(functionName, "WideCharToMultiByte"))
        return (void*)&GuidexosWideCharToMultiByte;
    return 0;
}

// TARGET_GUIDEXOS CoreLib resolves the small startup P/Invoke closure through
// this target-owned callback.  It deliberately ignores the host module name:
// the image has no host loader and all accepted addresses are implemented in
// this same image.
void* guidexos_resolve_pinvoke(const char*, const char* functionName)
{
    if (!functionName) return 0;
    if (shim_name_is(functionName, "GetLastError"))
        return (void*)&GetLastError;
    if (shim_name_is(functionName, "SetLastError"))
        return (void*)&SetLastError;
    if (shim_name_is(functionName, "GetModuleFileNameW"))
        return (void*)&GetModuleFileNameW;
    if (shim_name_is(functionName, "GetEnvironmentVariableW"))
        return (void*)&GetEnvironmentVariableW;
    if (shim_name_is(functionName, "QueryPerformanceCounter"))
        return (void*)&QueryPerformanceCounter;
    if (shim_name_is(functionName, "QueryPerformanceFrequency"))
        return (void*)&QueryPerformanceFrequency;
    if (shim_name_is(functionName, "GetCurrentProcess"))
        return (void*)&GetCurrentProcess;
    if (shim_name_is(functionName, "GetCurrentThread"))
        return (void*)&GetCurrentThread;
    if (shim_name_is(functionName, "CloseHandle"))
        return (void*)&CloseHandle;
    if (shim_name_is(functionName, "SetEvent"))
        return (void*)&SetEvent;
    if (shim_name_is(functionName, "ResetEvent"))
        return (void*)&ResetEvent;
    if (shim_name_is(functionName, "FreeLibrary"))
        return (void*)&CloseHandle;
    if (shim_name_is(functionName, "VirtualAlloc"))
        return (void*)&GuidexosVirtualAlloc;
    if (shim_name_is(functionName, "VirtualFree"))
        return (void*)&GuidexosVirtualFree;
    if (shim_name_is(functionName, "VirtualProtect"))
        return (void*)&GuidexosVirtualProtect;
    if (shim_name_is(functionName, "BCryptGenRandom"))
        return (void*)&GuidexosBCryptGenRandom;
    if (shim_name_is(functionName, "CreateEventExW"))
        return (void*)&GuidexosCreateEventExW;
    if (shim_name_is(functionName, "FormatMessageW"))
        return (void*)&GuidexosFormatMessageW;
    if (shim_name_is(functionName, "GetConsoleOutputCP"))
        return (void*)&GuidexosGetConsoleOutputCP;
    if (shim_name_is(functionName, "GetCurrentProcessorNumberEx"))
        return (void*)&GuidexosGetCurrentProcessorNumberEx;
    if (shim_name_is(functionName, "GetStdHandle"))
        return (void*)&GuidexosGetStdHandle;
    if (shim_name_is(functionName, "GetThreadPriority"))
        return (void*)&GuidexosGetThreadPriority;
    if (shim_name_is(functionName, "IsDebuggerPresent"))
        return (void*)&GuidexosIsDebuggerPresent;
    if (shim_name_is(functionName, "LocalFree"))
        return (void*)&GuidexosLocalFree;
    if (shim_name_is(functionName, "MultiByteToWideChar"))
        return (void*)&GuidexosMultiByteToWideChar;
    if (shim_name_is(functionName, "Sleep"))
        return (void*)&GuidexosSleep;
    if (shim_name_is(functionName, "WaitForMultipleObjectsEx"))
        return (void*)&GuidexosWaitForMultipleObjectsEx;
    if (shim_name_is(functionName, "WriteFile"))
        return (void*)&GuidexosWriteFile;
    if (shim_name_is(functionName, "WideCharToMultiByte"))
        return (void*)&GuidexosWideCharToMultiByte;
    return 0;
}
void* GuidexosVirtualAlloc(void* address, u64 size, u32 allocationType, u32 protection)
{
    enum : u32 { MemCommit = 0x1000, MemReserve = 0x2000 };
    if (address != 0) {
        if ((allocationType & MemCommit) != 0 &&
            guidexos_pal_vm_commit(address, size, protection) != 0)
            return 0;
        return address;
    }
    address = guidexos_pal_vm_reserve(size, 0x1000);
    if (address == 0) return 0;
    if ((allocationType & MemCommit) != 0 &&
        guidexos_pal_vm_commit(address, size, protection) != 0) {
        (void)guidexos_pal_vm_release(address, 0);
        return 0;
    }
    return address;
}
int GuidexosVirtualFree(void* address, u64 size, u32 freeType)
{
    (void)freeType;
    return address == 0 || guidexos_pal_vm_release(address, size) == 0;
}
int GuidexosVirtualProtect(void* address, u64 size, u32 protection, u32* oldProtection)
{
    if (oldProtection) *oldProtection = 0x04;
    return guidexos_pal_vm_protect(address, size, protection) == 0;
}
int GuidexosBCryptGenRandom(HANDLE, u8* buffer, u32 length, u32)
{
    return (buffer == 0 && length != 0) ? 0xC000000D :
        guidexos_pal_random_bytes(buffer, length) == 0 ? 0 : 0xC0000225;
}
HANDLE GuidexosCreateEventExW(void*, const u16*, u32, u32)
{
    return (HANDLE)(u64)1;
}
u32 GuidexosFormatMessageW(u32, const void*, u32, u32, u16*, u32, void*)
{
    return 0;
}
u32 GuidexosGetConsoleOutputCP() { return 65001; }
void GuidexosGetCurrentProcessorNumberEx(void* processorNumber)
{
    if (processorNumber) {
        u8* bytes = (u8*)processorNumber;
        for (u32 i = 0; i < 4; ++i) bytes[i] = 0;
    }
}
HANDLE GuidexosGetStdHandle(u32) { return (HANDLE)(u64)-1; }
i32 GuidexosGetThreadPriority(HANDLE) { return 0; }
BOOL GuidexosIsDebuggerPresent() { return 0; }
void* GuidexosLocalFree(void*) { return 0; }
i32 GuidexosMultiByteToWideChar(u32, u32, const char*, i32, u16*, i32) { return 0; }
void GuidexosSleep(u32) { }
u32 GuidexosWaitForMultipleObjectsEx(u32, const HANDLE*, BOOL, u32, BOOL) { return 258; }
BOOL GuidexosWriteFile(HANDLE, const void*, u32, u32* written, void*)
{
    if (written) *written = 0;
    return 0;
}
i32 GuidexosWideCharToMultiByte(u32, u32, const u16*, i32, char*, i32,
                                const char*, BOOL*) { return 0; }
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

// No RtlVirtualUnwind import veneer is supplied for the GUIDEXOS target.
// Its Windows ABI has no validation-failure result and does not carry bounded
// image or stack views. The hosted, bounded evaluator is in
// x64_unwind_bounded.h; it is not a production code-manager adapter.

} // extern "C"
