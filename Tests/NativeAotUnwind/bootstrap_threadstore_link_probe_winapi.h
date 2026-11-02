#pragma once

#include <cstddef>
#include <cstdint>

using HostWinBool = int32_t;
using HostWinDword = uint32_t;
using HostWinHandle = void*;
using HostWinSize = size_t;
using HostFlsCallback = void (__stdcall*)(void*);

struct HostMemoryBasicInformation
{
    void* BaseAddress;
    void* AllocationBase;
    HostWinDword AllocationProtect;
    uint16_t PartitionId;
    HostWinSize RegionSize;
    HostWinDword State;
    HostWinDword Protect;
    HostWinDword Type;
};

extern "C" uint32_t __stdcall FlsAlloc(HostFlsCallback callback);
extern "C" void* __stdcall FlsGetValue(uint32_t index);
extern "C" HostWinBool __stdcall FlsSetValue(uint32_t index, void* value);
extern "C" HostWinDword __stdcall GetCurrentThreadId();
extern "C" HostWinDword __stdcall GetLastError();
extern "C" HostWinHandle __stdcall GetCurrentProcess();
extern "C" __declspec(noreturn) void __stdcall ExitProcess(uint32_t exitCode);
extern "C" void* __stdcall VirtualAlloc(void* address, size_t size,
                                        uint32_t allocationType,
                                        uint32_t protect);
extern "C" size_t __stdcall VirtualQuery(const void* address,
                                         HostMemoryBasicInformation* info,
                                         size_t length);

constexpr uint32_t kHostFlSOutOfIndexes = 0xFFFFFFFFu;
constexpr uint32_t kHostMemCommit = 0x00001000u;
constexpr uint32_t kHostMemReserve = 0x00002000u;
constexpr uint32_t kHostPageNoAccess = 0x01u;
constexpr uint32_t kHostPageReadWrite = 0x04u;
constexpr uint32_t kHostPageGuard = 0x100u;
constexpr uint32_t kHostMemPageSize = 0x1000u;
