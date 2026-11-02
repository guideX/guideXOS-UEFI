#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "rhassert.h"
#include "slist.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "holder.h"
#include "Crst.h"
#include "threadstore.h"
#include "threadstore.inl"
#include "thread.inl"
#include "RuntimeInstance.h"
#include <cstdio>
#include <cstdint>
#include <cstring>

#include "bootstrap_threadstore_link_probe_host_pal.h"
#include "bootstrap_threadstore_link_probe_winapi.h"
#include "../../Tools/Phase23/stack_bounds_contract.h"

using guidexos::stackbounds::Descriptor;

extern "C" bool __stdcall guidexos_pal_register_bootstrap_stack(
    const Descriptor* descriptor,
    const void* memoryMap,
    uint64_t memoryMapEntryCount,
    uint64_t memoryMapDescriptorSize,
    uint64_t activePageTableRoot);
extern "C" bool __stdcall PalGetMaximumStackBounds(
    void** stackLow,
    void** stackHigh);
extern "C" void guidexos_call_on_stack(
    void* stackTop,
    void (*entry)(void*),
    void* arg);

namespace {

constexpr uint64_t kHostedPageTableRoot = 0x1000;
constexpr uint64_t kEfiLoaderDataType = 2;
constexpr uint64_t kEfiDescriptorSize = 40;
constexpr size_t kBootstrapStackSize = 512 * 1024;

struct HostedEfiMemoryDescriptor
{
    uint8_t Bytes[kEfiDescriptorSize];
};

struct HostedRun
{
    void* StackLow;
    void* StackHigh;
    Descriptor StackDescriptor;
    HostedEfiMemoryDescriptor MemoryMapEntry;
    unsigned ChecksPassed;
    unsigned ChecksFailed;
};

bool Check(HostedRun* run, bool condition, const char* name)
{
    if (condition)
    {
        ++run->ChecksPassed;
        std::printf("PASS %s\n", name);
        return true;
    }

    ++run->ChecksFailed;
    std::printf("FAIL %s\n", name);
    return false;
}

void WriteU32(uint8_t* bytes, uint32_t value)
{
    bytes[0] = static_cast<uint8_t>(value);
    bytes[1] = static_cast<uint8_t>(value >> 8);
    bytes[2] = static_cast<uint8_t>(value >> 16);
    bytes[3] = static_cast<uint8_t>(value >> 24);
}

void WriteU64(uint8_t* bytes, uint64_t value)
{
    WriteU32(bytes, static_cast<uint32_t>(value));
    WriteU32(bytes + 4, static_cast<uint32_t>(value >> 32));
}

bool EveryPageIsCommittedWritable(void* low, size_t bytes)
{
    const uintptr_t pageSize = kHostMemPageSize;
    const uintptr_t start = reinterpret_cast<uintptr_t>(low);
    const uintptr_t high = start + bytes;

    for (uintptr_t page = start; page < high; page += pageSize)
    {
        HostMemoryBasicInformation info{};
        if (VirtualQuery(reinterpret_cast<const void*>(page), &info, sizeof(info)) != sizeof(info) ||
            info.State != kHostMemCommit || info.AllocationBase != low ||
            (info.Protect & (kHostPageNoAccess | kHostPageGuard)) != 0)
            return false;
    }

    return true;
}

void VerifyThreadStore(HostedRun* run)
{
    const uint64_t nativeThreadId = PalGetCurrentOSThreadId();
    void* palLow = reinterpret_cast<void*>(1);
    void* palHigh = reinterpret_cast<void*>(1);
    const bool gotBounds = PalGetMaximumStackBounds(&palLow, &palHigh);
    Check(run, gotBounds && palLow == run->StackLow && palHigh == run->StackHigh,
          "production PAL returns exact hosted bootstrap bounds");

    const Thread* rawCurrent = ThreadStore::RawGetCurrentThread();
    Thread* current = ThreadStore::GetCurrentThread();
    Check(run, rawCurrent == current && HostPalGetAttachedThread() == current,
          "ThreadStore current thread matches host PAL association");

    void* threadLow = nullptr;
    void* threadHigh = nullptr;
    current->GetStackBounds(&threadLow, &threadHigh);
    Check(run, threadLow == run->StackLow && threadHigh == run->StackHigh,
          "actual Thread::Construct stored the production PAL bounds");
    Check(run, current->GetPalThreadIdForLogging() == nativeThreadId,
          "actual Thread::Construct captured the host OS thread identity");

    ThreadStore* threadStore = GetRuntimeInstance()->GetThreadStore();
    unsigned membershipCount = 0;
    bool currentIsMember = false;
    threadStore->LockThreadStore();
    {
        ThreadStore::Iterator iterator;
        Thread* member = nullptr;
        while ((member = iterator.GetNext()) != nullptr)
        {
            ++membershipCount;
            currentIsMember |= member == current;
        }
    }
    threadStore->UnlockThreadStore();
    Check(run, currentIsMember && membershipCount == 1,
          "ThreadStore contains exactly the attached bootstrap thread");

    ThreadStore::AttachCurrentThread();
    unsigned duplicateMembershipCount = 0;
    threadStore->LockThreadStore();
    {
        ThreadStore::Iterator iterator;
        while (iterator.GetNext() != nullptr)
            ++duplicateMembershipCount;
    }
    threadStore->UnlockThreadStore();
    Check(run, duplicateMembershipCount == 1 &&
                   ThreadStore::RawGetCurrentThread() == current,
          "repeat attachment does not duplicate or replace the thread");

    std::printf("RESULT passed=%u failed=%u\n", run->ChecksPassed, run->ChecksFailed);
    std::fflush(stdout);

    // This pre-GC fixture cannot call Thread::Detach: the real detach path
    // releases the allocation context through GCHeapUtilities. Keep the
    // permanent bootstrap association and stack alive until process exit.
    ExitProcess(run->ChecksFailed == 0 ? 0 : 3);
}

void BootstrapOnHostedStack(void* context)
{
    HostedRun* run = static_cast<HostedRun*>(context);
    Check(run, EveryPageIsCommittedWritable(run->StackLow, kBootstrapStackSize),
          "host OS backs every page of the dedicated bootstrap stack");

    const uint64_t stackLow = reinterpret_cast<uint64_t>(run->StackLow);
    bool descriptorCreated = guidexos::stackbounds::CreateBootstrapDescriptorAfterPageWalk(
        &run->StackDescriptor, stackLow, kBootstrapStackSize, kHostedPageTableRoot);
    Check(run, descriptorCreated, "production bootstrap descriptor builder accepts host allocation");

    uint8_t* memoryBytes = run->MemoryMapEntry.Bytes;
    std::memset(memoryBytes, 0, sizeof(run->MemoryMapEntry.Bytes));
    WriteU32(memoryBytes, static_cast<uint32_t>(kEfiLoaderDataType));
    WriteU64(memoryBytes + 8, stackLow);
    WriteU64(memoryBytes + 24, kBootstrapStackSize / guidexos::stackbounds::kPageSize);

    Descriptor rejected = run->StackDescriptor;
    rejected.Generation++;
    Check(run, !guidexos_pal_register_bootstrap_stack(
                       &rejected, &run->MemoryMapEntry, 1, kEfiDescriptorSize,
                       kHostedPageTableRoot),
          "invalid descriptor is rejected before publication");

    Check(run, guidexos_pal_register_bootstrap_stack(
                       &run->StackDescriptor, &run->MemoryMapEntry, 1,
                       kEfiDescriptorSize, kHostedPageTableRoot),
          "production PAL accepts the verified bootstrap descriptor");
    Check(run, guidexos_pal_register_bootstrap_stack(
                       &run->StackDescriptor, &run->MemoryMapEntry, 1,
                       kEfiDescriptorSize, kHostedPageTableRoot),
          "identical descriptor registration is idempotent");

    Descriptor replacement = run->StackDescriptor;
    replacement.Flags |= guidexos::stackbounds::kGuardPagesExcluded;
    Check(run, !guidexos_pal_register_bootstrap_stack(
                       &replacement, &run->MemoryMapEntry, 1,
                       kEfiDescriptorSize, kHostedPageTableRoot),
          "different descriptor cannot replace the active stack");

    void* invalidLow = reinterpret_cast<void*>(1);
    void* invalidHigh = reinterpret_cast<void*>(1);
    const bool invalidRequestAccepted = PalGetMaximumStackBounds(&invalidLow, nullptr);
    Check(run, !invalidRequestAccepted && invalidLow == nullptr,
          "invalid PAL request fails and clears each supplied output");

    VerifyThreadStore(run);
}

} // namespace

int main()
{
    HostedRun run{};
    run.StackLow = VirtualAlloc(nullptr, kBootstrapStackSize,
                                kHostMemReserve | kHostMemCommit,
                                kHostPageReadWrite);
    if (run.StackLow == nullptr)
    {
        std::fprintf(stderr, "VirtualAlloc for hosted bootstrap stack failed: %lu\n",
                     GetLastError());
        return 10;
    }
    run.StackHigh = static_cast<uint8_t*>(run.StackLow) + kBootstrapStackSize;

    if (!HostPalPrepareThreadServices())
    {
        std::fprintf(stderr, "Host PAL thread-service setup failed: %lu\n", GetLastError());
        return 11;
    }

    HostPalRejectHijackCallbackRegistration(true);
    if (RuntimeInstance::Initialize(nullptr) || GetRuntimeInstance() != nullptr ||
        HostPalGetAttachedThread() != nullptr)
    {
        std::fprintf(stderr, "RuntimeInstance failure published partial runtime/thread state.\n");
        return 12;
    }

    HostPalRejectHijackCallbackRegistration(false);
    if (!RuntimeInstance::Initialize(nullptr) || GetRuntimeInstance() == nullptr)
    {
        std::fprintf(stderr, "RuntimeInstance/ThreadStore creation failed.\n");
        return 13;
    }

    std::printf("PASS ThreadStore initialization failure left no published runtime/thread\n");
    ++run.ChecksPassed;
    guidexos_call_on_stack(run.StackHigh, BootstrapOnHostedStack, &run);
    return 14; // The attached fixture must exit on its own stack.
}
