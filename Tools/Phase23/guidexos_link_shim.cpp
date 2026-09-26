// Phase 23 GUIDEXOS runtime ABI veneer.
//
// The pinned NativeAOT runtime still contains a bounded set of legacy
// Redhawk/PAL and compiler-support symbol names.  This object is the single
// target-side adapter for those names.  It contains no Windows imports and
// forwards the platform primitives to the Phase 19 guideXOS PAL contract.
// Unsupported operations fail closed or return the documented non-success
// result; this object is not an execution authorization.

#include "..\Phase19\guidexos_nativeaot_pal_contract.h"

extern "C" void guidexos_pal_runtime_diagnostic(unsigned long long marker);

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
    u64 requested_size = size;
    if (size == 0) size = 1;
    void* address = guidexos_pal_vm_reserve(size, 0x1000);
    if (address == 0) {
        guidexos_pal_runtime_diagnostic(0x6000000000000000ULL |
            (requested_size & 0x0000FFFFFFFFFFFFULL));
        return 0;
    }
    if (guidexos_pal_vm_commit(address, size, 0x04) != 0) {
        guidexos_pal_runtime_diagnostic(0x6100000000000000ULL |
            (requested_size & 0x0000FFFFFFFFFFFFULL));
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
    // Temporary Phase 26 breadcrumb: preserve the bounded fatal message
    // prefix without introducing a host console dependency.
    u64 prefix = 0;
    if (message) {
        for (u64 i = 0; i < 8 && message[i] != 0; ++i)
            prefix |= ((u64)(u8)message[i]) << (i * 8);
    }
    guidexos_pal_runtime_diagnostic(0x7000000000000000ULL | prefix);
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
    static u32 guidexos_resolver_diagnostic_count = 0;
    if (guidexos_resolver_diagnostic_count < 32) {
        u64 prefix = 0;
        for (u32 i = 0; i < 6 && functionName[i] != 0; ++i)
            prefix |= ((u64)(u8)functionName[i]) << (i * 8);
        guidexos_pal_runtime_diagnostic(
            0xD000000000000000ULL |
            ((u64)guidexos_resolver_diagnostic_count++ << 48) | prefix);
    }
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

// The Windows NativeAOT COFF code manager calls the imported unwind entrypoint
// through __imp_RtlVirtualUnwind.  GUIDEXOS has no host kernel32/ntdll import
// surface, so provide the image-local x64 unwind required by the managed
// Phase 26 proof.  The code-manager object performs the PE/.pdata lookup; this
// veneer interprets the mapped UNWIND_INFO and restores the caller frame.
struct GuidexosUnwindContext {
    u8 Reserved0[0x78];
    u64 Rax;
    u64 Rcx;
    u64 Rdx;
    u64 Rbx;
    u64 Rsp;
    u64 Rbp;
    u64 Rsi;
    u64 Rdi;
    u64 R8;
    u64 R9;
    u64 R10;
    u64 R11;
    u64 R12;
    u64 R13;
    u64 R14;
    u64 R15;
    u64 Rip;
};

struct GuidexosRuntimeFunction {
    u32 BeginAddress;
    u32 EndAddress;
    u32 UnwindData;
};

struct GuidexosUnwindCode {
    u8 CodeOffset;
    u8 UnwindOpAndInfo;
};

// Windows RtlVirtualUnwind returns the locations of restored nonvolatile
// registers through this companion structure.  NativeAOT feeds those
// locations back into REGDISPLAY so GC liveness can continue to refer to the
// saved stack slots after a frame is unwound.  Keep the Windows x64 layout:
// sixteen floating-point pointers followed by sixteen integer-register
// pointers (RAX..R15).
struct GuidexosNonvolatileContextPointers {
    void* FloatingContext[16];
    u64* IntegerContext[16];
};

static u64* guidexos_unwind_register(GuidexosUnwindContext* registers, u8 number)
{
    switch (number) {
        case 0: return &registers->Rax;
        case 1: return &registers->Rcx;
        case 2: return &registers->Rdx;
        case 3: return &registers->Rbx;
        case 4: return &registers->Rsp;
        case 5: return &registers->Rbp;
        case 6: return &registers->Rsi;
        case 7: return &registers->Rdi;
        case 8: return &registers->R8;
        case 9: return &registers->R9;
        case 10: return &registers->R10;
        case 11: return &registers->R11;
        case 12: return &registers->R12;
        case 13: return &registers->R13;
        case 14: return &registers->R14;
        case 15: return &registers->R15;
        default: return 0;
    }
}

static u64** guidexos_unwind_register_pointer(
    GuidexosNonvolatileContextPointers* pointers, u8 number)
{
    if (pointers == 0 || number >= 16) return 0;
    return &pointers->IntegerContext[number];
}

static void guidexos_unwind_one(GuidexosUnwindContext* registers,
                                u64 imageBase, u64 controlPc,
                                const GuidexosRuntimeFunction* function,
                                GuidexosNonvolatileContextPointers* pointers)
{
    if (registers == 0 || function == 0) return;
    const u8* info = (const u8*)(imageBase + (function->UnwindData & ~3U));
    u8 flags = (u8)(info[0] >> 3);
    u8 prologSize = info[1];
    u8 codeCount = info[2];
    u8 frameRegister = (u8)(info[3] & 0x0F);
    u8 frameOffset = (u8)(info[3] >> 4);
    u64 prologOffset = 0;
    u64 functionStart = imageBase + function->BeginAddress;
    if (controlPc >= functionStart)
        prologOffset = controlPc - functionStart;
    if (prologOffset > prologSize) prologOffset = prologSize;

    const GuidexosUnwindCode* codes =
        (const GuidexosUnwindCode*)(info + sizeof(u32));
    // The entries are stored in descending prologue-offset order.  The
    // Windows unwinder consumes that order while reversing the prologue:
    // SAVE_* entries must be applied against the final RSP before the
    // preceding allocation and pushes are undone.  Extended operations use
    // the slots immediately following the operation entry.
    int index = 0;
    while (index < (int)codeCount) {
        const GuidexosUnwindCode* code = &codes[index];
        u8 operation = (u8)(code->UnwindOpAndInfo & 0x0F);
        u8 operationInfo = (u8)(code->UnwindOpAndInfo >> 4);
        if (code->CodeOffset <= prologOffset) {
            switch (operation) {
                case 0: { // UWOP_PUSH_NONVOL
                    u64* target = guidexos_unwind_register(registers, operationInfo);
                    if (target) {
                        u64* saved = (u64*)registers->Rsp;
                        *target = *saved;
                        u64** location = guidexos_unwind_register_pointer(pointers, operationInfo);
                        if (location) *location = saved;
                        registers->Rsp += 8;
                    }
                    break;
                }
                case 1: { // UWOP_ALLOC_LARGE
                    if (operationInfo == 0 && index + 1 < (int)codeCount) {
                        registers->Rsp += (u64)*(const u16*)&codes[index + 1] * 8ULL;
                        index++;
                    } else if (operationInfo == 1 && index + 2 < (int)codeCount) {
                        registers->Rsp += (u64)*(const u32*)&codes[index + 1];
                        index += 2;
                    }
                    break;
                }
                case 2: // UWOP_ALLOC_SMALL
                    registers->Rsp += (u64)(operationInfo + 1) * 8ULL;
                    break;
                case 3: { // UWOP_SET_FPREG
                    u64* frame = guidexos_unwind_register(registers, frameRegister);
                    if (frame) registers->Rsp = *frame - (u64)frameOffset * 16ULL;
                    break;
                }
                case 4: // UWOP_SAVE_NONVOL
                    if (index + 1 < (int)codeCount) {
                        u64* target = guidexos_unwind_register(registers, operationInfo);
                        u64* saved = (u64*)(registers->Rsp +
                            (u64)*(const u16*)&codes[index + 1] * 8ULL);
                        if (target) {
                            *target = *saved;
                            u64** location = guidexos_unwind_register_pointer(pointers, operationInfo);
                            if (location) *location = saved;
                        }
                        index++;
                    }
                    break;
                case 5: // UWOP_SAVE_NONVOL_FAR
                    if (index + 2 < (int)codeCount) {
                        u64* target = guidexos_unwind_register(registers, operationInfo);
                        u64* saved = (u64*)(registers->Rsp +
                            (u64)*(const u32*)&codes[index + 1]);
                        if (target) {
                            *target = *saved;
                            u64** location = guidexos_unwind_register_pointer(pointers, operationInfo);
                            if (location) *location = saved;
                        }
                        index += 2;
                    }
                    break;
                case 8: // UWOP_SAVE_XMM128
                    index++;
                    break;
                case 9: // UWOP_SAVE_XMM128_FAR
                    index += 2;
                    break;
                case 10: // UWOP_PUSH_MACHFRAME
                    registers->Rsp += operationInfo ? 0x28ULL : 0x18ULL;
                    break;
                default:
                    break;
            }
        }
        index++;
    }

    if (flags & 4) {
        const GuidexosRuntimeFunction* chained =
            (const GuidexosRuntimeFunction*)(codes + ((codeCount + 1) & ~1));
        guidexos_unwind_one(registers, imageBase, controlPc, chained, pointers);
        return;
    }
    if (flags & 3) {
        const u32* handler = (const u32*)(codes + ((codeCount + 1) & ~1));
        (void)handler;
    }
}

static u32 guidexos_rtl_virtual_unwind(u32, u64 imageBase, u64 controlPc,
                                       void* functionEntry, void* context,
                                       void** handlerData, u64* establisherFrame,
                                       void* contextPointers)
{
    GuidexosUnwindContext* registers = (GuidexosUnwindContext*)context;
    if (handlerData) *handlerData = 0;
    if (registers == 0 || registers->Rsp == 0) return 0;
    guidexos_pal_runtime_diagnostic(0x8100000000000000ULL |
        (controlPc & 0x0000FFFFFFFFFFFFULL));
    guidexos_pal_runtime_diagnostic(0x8200000000000000ULL |
        (registers->Rsp & 0x0000FFFFFFFFFFFFULL));
    if (functionEntry == 0) {
        registers->Rip = *(u64*)registers->Rsp;
        registers->Rsp += sizeof(u64);
    } else {
        guidexos_unwind_one(registers, imageBase, controlPc,
                            (const GuidexosRuntimeFunction*)functionEntry,
                            (GuidexosNonvolatileContextPointers*)contextPointers);
        registers->Rip = *(u64*)registers->Rsp;
        registers->Rsp += sizeof(u64);
    }
    guidexos_pal_runtime_diagnostic(0x8300000000000000ULL |
        (registers->Rip & 0x0000FFFFFFFFFFFFULL));
    guidexos_pal_runtime_diagnostic(0x8400000000000000ULL |
        (registers->Rsp & 0x0000FFFFFFFFFFFFULL));
    if (establisherFrame) *establisherFrame = registers->Rsp;
    return 0;
}

u32 (*__imp_RtlVirtualUnwind)(u32, u64, u64, void*, void*, void**, u64*, void*) =
    guidexos_rtl_virtual_unwind;

} // extern "C"
