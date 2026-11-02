#include "../../Tools/Phase23/execution_context_contract.h"

#include <cstdio>

using namespace guidexos::executioncontext;

namespace {
int g_passed = 0;
int g_failed = 0;

void Check(const char* name, bool value) {
    if (value) {
        ++g_passed;
        std::printf("PASS %s\n", name);
    } else {
        ++g_failed;
        std::printf("FAIL %s\n", name);
    }
}

StackDescriptor MakeStack(uint64_t identity = 7,
                          uint64_t generation = 1) {
    return {
        identity,
        generation,
        0x100000,
        0x110000,
        0x101000,
        0x10F000,
        kRequiredStackEvidence
    };
}

Snapshot MakeSnapshot(const ContextRecord& context,
                      const StackDescriptor& stack) {
    Snapshot snapshot{};
    snapshot.ContextIdentity = context.ExecutionIdentity.Value;
    snapshot.ContextGeneration = context.ExecutionIdentity.Generation;
    snapshot.StackIdentity = stack.Identity;
    snapshot.StackGeneration = stack.Generation;
    snapshot.Cpu = context.ExecutionIdentity.Cpu;
    snapshot.Kind = CaptureKind::SchedulerInterrupt;
    snapshot.Rip = 0x400000;
    snapshot.Rsp = 0x108000;
    snapshot.Rflags = 0x202;
    snapshot.SavedRegisterMask = kAllIntegerRegisters;
    for (uint32_t i = 0; i < 16; ++i)
        snapshot.Gpr[i] = 0x1000ULL + i;
    return snapshot;
}
}

int main() {
    StackRegistry<2> registry;
    const StackDescriptor stack = MakeStack();
    const StackHandle handle{stack.Identity, stack.Generation};

    Check("register validated allocator stack descriptor",
        registry.Register(stack, false) == RegistryResult::Success);
    Check("duplicate descriptor is idempotent",
        registry.Register(stack, false) == RegistryResult::AlreadyRegistered);

    StackDescriptor withoutEfi = stack;
    withoutEfi.Evidence &= ~kEfiBackingVerified;
    Check("missing EFI backing evidence is rejected",
        registry.Register(withoutEfi, false) ==
            RegistryResult::MissingProvenance);

    StackDescriptor invalidAccessible = stack;
    invalidAccessible.AccessibleHigh = invalidAccessible.AllocationHigh + 0x1000;
    Check("accessible interval outside allocation is rejected",
        registry.Register(invalidAccessible, false) ==
            RegistryResult::MissingProvenance);

    Check("acquire descriptor lifetime pin",
        registry.Acquire(handle) == RegistryResult::Success &&
        registry.PinCount(handle) == 1);
    Check("stale generation cannot acquire a descriptor",
        registry.Acquire({stack.Identity, stack.Generation + 1}) ==
            RegistryResult::StaleHandle);

    bool reclaimable = true;
    Check("retirement waits while a stack is pinned",
        registry.BeginRetirement(handle, &reclaimable) ==
            RegistryResult::PendingReclamation && !reclaimable &&
        !registry.IsActive(handle) && !registry.IsReclaimable(handle));
    Check("retiring descriptor rejects new pins",
        registry.Acquire(handle) == RegistryResult::InvalidState);
    Check("last release completes deferred reclamation",
        registry.Release(handle, &reclaimable) == RegistryResult::Reclaimable &&
        reclaimable && registry.IsReclaimable(handle));
    Check("released pin cannot be released twice",
        registry.Release(handle) == RegistryResult::InvalidState);

    const StackDescriptor nextGeneration = MakeStack(7, 2);
    const StackHandle nextHandle{nextGeneration.Identity,
                                 nextGeneration.Generation};
    Check("retired identity may be registered with a newer generation",
        registry.Register(nextGeneration, false) == RegistryResult::Success);
    Check("stale generation is rejected after descriptor reuse",
        registry.Acquire(handle) == RegistryResult::StaleHandle);
    Check("unreferenced retirement is immediately reclaimable",
        registry.BeginRetirement(nextHandle, &reclaimable) ==
            RegistryResult::Reclaimable && reclaimable &&
        registry.IsReclaimable(nextHandle));

    StackRegistry<1> bootstrapRegistry;
    StackDescriptor bootstrap = MakeStack(0x4253500000000001ULL, 1);
    bootstrap.Evidence |= kPermanentKernelPin;
    const StackHandle bootstrapHandle{bootstrap.Identity,
                                      bootstrap.Generation};
    Check("verified bootstrap descriptor registers with permanent pin",
        IsValidStackDescriptor(bootstrap, true) &&
        bootstrapRegistry.Register(bootstrap, true) ==
            RegistryResult::Success &&
        bootstrapRegistry.PinCount(bootstrapHandle) == 1);
    Check("kernel-lifetime bootstrap descriptor cannot retire",
        bootstrapRegistry.BeginRetirement(bootstrapHandle) ==
            RegistryResult::Unsupported);

    ContextRecord context{};
    context.ExecutionIdentity = {kBootstrapContextId, 1, 0};
    context.Kind = ContextKind::KernelBootstrap;
    context.Lifecycle = ContextLifecycle::Running;
    context.Stack = bootstrapHandle;
    context.ThreadLocalState = 0;
    context.NativeAotAttached = false;
    Check("bootstrap context has stable identity and may be unattached",
        ValidateContextRecord(context, bootstrapRegistry) ==
            RegistryResult::Success && !context.NativeAotAttached &&
        context.ExecutionIdentity.Value == kBootstrapContextId);

    Snapshot snapshot = MakeSnapshot(context, bootstrap);
    Check("integer/control scheduler snapshot is explicitly limited",
        ValidateSnapshot(context, bootstrap, snapshot) ==
            CaptureResult::SchedulerStateOnly &&
        snapshot.Gpr[3] == 0x1003 && snapshot.Gpr[12] == 0x100C);

    Snapshot invalid = snapshot;
    invalid.ContextGeneration++;
    Check("stale context generation is rejected",
        ValidateSnapshot(context, bootstrap, invalid) ==
            CaptureResult::StaleContext);

    invalid = snapshot;
    invalid.StackIdentity++;
    Check("mismatched context and stack are rejected",
        ValidateSnapshot(context, bootstrap, invalid) ==
            CaptureResult::StackMismatch);

    invalid = snapshot;
    invalid.Cpu = 1;
    Check("mismatched CPU association is rejected",
        ValidateSnapshot(context, bootstrap, invalid) ==
            CaptureResult::MissingCpu);

    invalid = snapshot;
    invalid.Rip = 0;
    Check("invalid saved instruction pointer is rejected",
        ValidateSnapshot(context, bootstrap, invalid) ==
            CaptureResult::InvalidInstructionPointer);

    invalid = snapshot;
    invalid.Rsp = bootstrap.AccessibleHigh;
    Check("invalid saved stack pointer is rejected",
        ValidateSnapshot(context, bootstrap, invalid) ==
            CaptureResult::InvalidStackPointer);

    invalid = snapshot;
    invalid.SavedRegisterMask &= ~(1U << 12);
    Check("missing nonvolatile register state is rejected",
        ValidateSnapshot(context, bootstrap, invalid) ==
            CaptureResult::IncompleteRegisters);

    invalid = snapshot;
    invalid.Kind = CaptureKind::NativeAotFullState;
    invalid.ExtendedStateMask = 0;
    Check("unattached context cannot claim NativeAOT suspension state",
        ValidateSnapshot(context, bootstrap, invalid) ==
            CaptureResult::Unsupported);

    context.NativeAotAttached = true;
    Check("NativeAOT state without XSTATE remains incomplete",
        ValidateSnapshot(context, bootstrap, invalid) ==
            CaptureResult::MissingExtendedState);
    invalid.ExtendedStateMask = 1;
    Check("complete NativeAOT snapshot shape is accepted by validator",
        ValidateSnapshot(context, bootstrap, invalid) == CaptureResult::Success);

    context.ExecutionIdentity.Cpu = kUnknownCpu;
    Check("context without CPU identity is not publishable",
        ValidateContextRecord(context, bootstrapRegistry) ==
            RegistryResult::InvalidArgument);

    context.ExecutionIdentity.Cpu = 0;
    Check("foreign-thread suspension remains unsupported",
        NativeAotSuspensionCapability(context) == RegistryResult::Unsupported);
    Check("NativeAOT context restoration remains unsupported",
        NativeAotRestorationCapability(context) == RegistryResult::Unsupported);

    std::printf("RESULT passed=%d failed=%d\n", g_passed, g_failed);
    return g_failed == 0 ? 0 : 1;
}
