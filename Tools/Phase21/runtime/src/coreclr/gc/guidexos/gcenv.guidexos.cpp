// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Phase 21 source-path proof for the workstation GC. This file is a semantic
// guideXOS environment skeleton, not a Windows synchronization emulation.

#include <atomic>
#include <cassert>
#include <cstddef>
#include <cstdint>
#include <new>

#include "common.h"
#include "gcenv.structs.h"
#include "gcenv.base.h"
#include "gcenv.os.h"
#include "gcenv.ee.h"
#include "gcenv.guidexos.inl"
#include "volatile.h"
#include "gcconfig.h"
#include "GuidexosGcPal.h"

GCSystemInfo g_SystemInfo;
uint32_t g_pageSizeGuidexosInl = 0;

namespace
{
    AffinitySet g_processAffinitySet;
    uint32_t g_totalProcessorCount = 1;

    [[noreturn]] void GuidexosUnsupported(const char* operation)
    {
        (void)operation;
        guidexos_pal_fail_fast(0x47585553u, nullptr);
        for (;;)
        {
            YieldProcessor();
        }
    }
}

bool GCToOSInterface::Initialize()
{
    g_pageSizeGuidexosInl = 4096;
    g_totalProcessorCount = 1;
    g_processAffinitySet.Add(0);
    g_SystemInfo.dwNumberOfProcessors = g_totalProcessorCount;
    g_SystemInfo.dwPageSize = g_pageSizeGuidexosInl;
    g_SystemInfo.dwAllocationGranularity = g_pageSizeGuidexosInl;
    return true;
}

void GCToOSInterface::Shutdown()
{
}

void* GCToOSInterface::VirtualReserve(size_t size, size_t alignment, uint32_t flags, uint16_t node)
{
    (void)node;
    if (flags != VirtualReserveFlags::None)
        return nullptr;
    return guidexos_pal_vm_reserve(static_cast<uint64_t>(size), static_cast<uint64_t>(alignment));
}

bool GCToOSInterface::VirtualRelease(void* address, size_t size)
{
    return address != nullptr && guidexos_pal_vm_release(address, static_cast<uint64_t>(size)) == 0;
}

bool GCToOSInterface::VirtualCommit(void* address, size_t size, uint16_t node)
{
    (void)node;
    return address != nullptr && guidexos_pal_vm_commit(address, static_cast<uint64_t>(size), 3) == 0;
}

void* GCToOSInterface::VirtualReserveAndCommitLargePages(size_t size, uint16_t node)
{
    (void)size;
    (void)node;
    return nullptr;
}

bool GCToOSInterface::VirtualDecommit(void* address, size_t size)
{
    (void)address;
    (void)size;
    return false;
}

bool GCToOSInterface::VirtualReset(void* address, size_t size, bool unlock)
{
    (void)address;
    (void)size;
    (void)unlock;
    return false;
}

bool GCToOSInterface::SupportsWriteWatch()
{
    return false;
}

void GCToOSInterface::ResetWriteWatch(void* address, size_t size)
{
    (void)address;
    (void)size;
}

bool GCToOSInterface::GetWriteWatch(bool resetState, void* address, size_t size, void** pageAddresses, uintptr_t* pageAddressesCount)
{
    (void)resetState;
    (void)address;
    (void)size;
    (void)pageAddresses;
    (void)pageAddressesCount;
    return false;
}

void GCToOSInterface::Sleep(uint32_t sleepMSec)
{
    if (sleepMSec != 0)
        GuidexosUnsupported("GCToOSInterface::Sleep");
}

void GCToOSInterface::YieldThread(uint32_t switchCount)
{
    (void)switchCount;
    YieldProcessor();
}

uint32_t GCToOSInterface::GetCurrentProcessorNumber()
{
    return 0;
}

bool GCToOSInterface::CanGetCurrentProcessorNumber()
{
    return false;
}

bool GCToOSInterface::SetCurrentThreadIdealAffinity(uint16_t srcProcNo, uint16_t dstProcNo)
{
    (void)srcProcNo;
    (void)dstProcNo;
    return false;
}

bool GCToOSInterface::GetCurrentThreadIdealProc(uint16_t* procNo)
{
    (void)procNo;
    return false;
}

uint64_t GCToOSInterface::GetCurrentThreadIdForLogging()
{
    return guidexos_pal_thread_id();
}

uint32_t GCToOSInterface::GetCurrentProcessId()
{
    return 0;
}

size_t GCToOSInterface::GetCacheSizePerLogicalCpu(bool trueSize)
{
    (void)trueSize;
    return 0;
}

bool GCToOSInterface::SetThreadAffinity(uint16_t procNo)
{
    (void)procNo;
    return false;
}

bool GCToOSInterface::BoostThreadPriority()
{
    return false;
}

const AffinitySet* GCToOSInterface::SetGCThreadsAffinitySet(uintptr_t configAffinityMask, const AffinitySet* configAffinitySet)
{
    (void)configAffinityMask;
    (void)configAffinitySet;
    return &g_processAffinitySet;
}

size_t GCToOSInterface::GetVirtualMemoryLimit()
{
    return 0;
}

size_t GCToOSInterface::GetVirtualMemoryMaxAddress()
{
    return 0;
}

uint64_t GCToOSInterface::GetPhysicalMemoryLimit(bool* is_restricted)
{
    if (is_restricted != nullptr)
        *is_restricted = false;
    return 0;
}

void GCToOSInterface::GetMemoryStatus(uint64_t restricted_limit, uint32_t* memory_load, uint64_t* available_physical, uint64_t* available_page_file)
{
    (void)restricted_limit;
    if (memory_load != nullptr)
        *memory_load = 0;
    if (available_physical != nullptr)
        *available_physical = 0;
    if (available_page_file != nullptr)
        *available_page_file = 0;
}

void GCToOSInterface::FlushProcessWriteBuffers()
{
    GuidexosUnsupported("GCToOSInterface::FlushProcessWriteBuffers");
}

void GCToOSInterface::DebugBreak()
{
}

int64_t GCToOSInterface::QueryPerformanceCounter()
{
    return static_cast<int64_t>(guidexos_pal_monotonic_ticks());
}

int64_t GCToOSInterface::QueryPerformanceFrequency()
{
    return static_cast<int64_t>(guidexos_pal_monotonic_frequency());
}

uint64_t GCToOSInterface::GetLowPrecisionTimeStamp()
{
    const uint64_t frequency = guidexos_pal_monotonic_frequency();
    return frequency == 0 ? 0 : (guidexos_pal_monotonic_ticks() * 1000) / frequency;
}

uint32_t GCToOSInterface::GetTotalProcessorCount()
{
    return g_totalProcessorCount;
}

bool GCToOSInterface::CanEnableGCNumaAware()
{
    return false;
}

bool GCToOSInterface::GetNumaInfo(uint16_t* total_nodes, uint32_t* max_procs_per_node)
{
    (void)total_nodes;
    (void)max_procs_per_node;
    return false;
}

bool GCToOSInterface::CanEnableGCCPUGroups()
{
    return false;
}

bool GCToOSInterface::GetCPUGroupInfo(uint16_t* total_groups, uint32_t* max_procs_per_group)
{
    (void)total_groups;
    (void)max_procs_per_group;
    return false;
}

bool GCToOSInterface::GetProcessorForHeap(uint16_t heap_number, uint16_t* proc_no, uint16_t* node_no)
{
    if (heap_number != 0)
        return false;
    if (proc_no != nullptr)
        *proc_no = 0;
    if (node_no != nullptr)
        *node_no = NUMA_NODE_UNDEFINED;
    return true;
}

bool GCToOSInterface::ParseGCHeapAffinitizeRangesEntry(const char** config_string, size_t* start_index, size_t* end_index)
{
    return ParseIndexOrRange(config_string, start_index, end_index);
}

bool CLRCriticalSection::Initialize()
{
    m_cs.state.store(0, std::memory_order_relaxed);
    return true;
}

void CLRCriticalSection::Destroy()
{
    m_cs.state.store(0, std::memory_order_relaxed);
}

void CLRCriticalSection::Enter()
{
    for (uint32_t attempt = 0; attempt != 4096; ++attempt)
    {
        uint32_t expected = 0;
        if (m_cs.state.compare_exchange_weak(expected, 1, std::memory_order_acquire, std::memory_order_relaxed))
            return;
        YieldProcessor();
    }
    GuidexosUnsupported("CLRCriticalSection::Enter blocking wait");
}

void CLRCriticalSection::Leave()
{
    m_cs.state.store(0, std::memory_order_release);
}

class GCEvent::Impl
{
public:
    Impl(bool manual, bool initialState)
        : state(initialState ? 1u : 0u), manual(manual)
    {
    }

    std::atomic<uint32_t> state;
    bool manual;
};

GCEvent::GCEvent()
    : m_impl(nullptr)
{
}

void GCEvent::CloseEvent()
{
    delete m_impl;
    m_impl = nullptr;
}

void GCEvent::Set()
{
    if (m_impl != nullptr)
        m_impl->state.store(1, std::memory_order_release);
}

void GCEvent::Reset()
{
    if (m_impl != nullptr)
        m_impl->state.store(0, std::memory_order_release);
}

uint32_t GCEvent::Wait(uint32_t timeout, bool alertable)
{
    (void)alertable;
    if (m_impl == nullptr)
        return WAIT_FAILED;

    if (m_impl->state.load(std::memory_order_acquire) != 0)
    {
        if (!m_impl->manual)
            m_impl->state.store(0, std::memory_order_release);
        return WAIT_OBJECT_0;
    }

    if (timeout == 0)
        return WAIT_TIMEOUT;

    GuidexosUnsupported("GCEvent::Wait blocking wait/wake");
}

bool GCEvent::CreateAutoEventNoThrow(bool initialState)
{
    m_impl = new (std::nothrow) Impl(false, initialState);
    return m_impl != nullptr;
}

bool GCEvent::CreateManualEventNoThrow(bool initialState)
{
    m_impl = new (std::nothrow) Impl(true, initialState);
    return m_impl != nullptr;
}

bool GCEvent::CreateOSAutoEventNoThrow(bool initialState)
{
    return CreateAutoEventNoThrow(initialState);
}

bool GCEvent::CreateOSManualEventNoThrow(bool initialState)
{
    return CreateManualEventNoThrow(initialState);
}
