# Phase 25 native bootstrap boundary

Phase 25 freezes the Phase 23 NativeAOT input and proves the surrounding
runtime environment with a separate image.  The managed PE remains the exact
`C8D60ABE6D91917F4E236F435A8C2D4272386CEC830C07AFA691897A23DA195A` artifact;
its entrypoint is `wmain` at `0x0000401000001420`, and the kernel readiness bit
remains false.

## Two-image contract

`Tools/Phase25/bootstrap.asm` builds a raw x86-64 `guideXOS.Phase25Bootstrap.bin`
and the sidecar `GXBI v1` descriptor.  The bootstrap is 840 bytes in the
current deterministic build, has no imports or relocations, and is mapped as
private user RX pages.  The descriptor records the SHA-256, fixed address,
entry offset, executable range, startup-block version, and ABI version.

The initial user register contract is deliberately small:

* `RDI` = `0x0000401100000000`, the 144-byte startup block;
* `RSP` = `0x00007fff0000fff0`, in the process-owned NX stack;
* `GS` = `0x0000401100020000`, with `GS+0x58` pointing to the TLS vector;
* all other registers are cleared or initialized by the kernel/bootstrap.

The bootstrap validates the startup contract, actual `GS+0x58` memory, the
vector slot at `_tls_index == 0`, the frozen Phase 23 TLS locations, the
bounded 64-slot FLS state, a register/stack sentinel across a long IRQ0
workload, the existing Ping ABI, and the copied System Information service.
It then calls the existing `Exit(0)` ABI.  It has no path to `wmain`, `.CRT`,
NativeAOT module initialization, GC, or managed code.

## User map

The fixed map is:

| Region | Range | Permission |
| --- | --- | --- |
| Phase 23 PE | `0x0000401000000000`–`0x00004010000c7000` | section-specific RX/R/RW |
| startup/runtime blocks | `0x0000401100000000`–`0x0000401100051000` | user RW, NX |
| Phase 25 bootstrap | `0x0000401200000000`–`0x0000401200001000` | private user RX |
| future heap reservation | begins `0x0000401300000000` | not mapped in Phase 25 |
| user stack | `0x00007fff00000000`–`0x00007fff00010000` | user RW, NX |
| guard/fault boundary | `0x00007fff00010000` | unmapped |

The managed PE and bootstrap share the canonical Phase 13–15 `Ring3Process`
address space, process table slot/generation, scheduler thread, CR3, trusted
kernel stack, TSS RSP0 update, and ApplicationInstance owner.  The
`ManagedImageProcess` and `NativeBootstrapImage` classes own image/runtime
resources but are not a second process authority.

## Gates and cleanup

The kernel accepts only RIPs within the reviewed bootstrap executable range for
this process.  The managed `wmain` address is rejected while
`ManagedEntryReady == false`; the readiness state is kernel-owned and is never
set by user memory.  Scheduler transitions clear GS while running kernel code,
capture/apply only the selected user-thread GS value, and restore the process
GS before CPL3 resume.  Invalid startup and GS state are rejected before user
dispatch.  The controlled fault mode faults above the mapped user stack and is
handled by the existing CPL3 fault-to-replacement path.

Cleanup releases bootstrap pages, managed image pages, private statics/BSS,
startup/GS/TLS/FLS/runtime blocks, user and kernel stacks, page tables, the
process table handle, and the diagnostic ApplicationInstance.  Phase 25 emits
balanced counters for every one of these ownership classes.

The `Ring3Phase25` selector is the runtime proof entrypoint.  Host builds and
GXBI verification establish artifact and static contracts only; CPL3,
preemption, IPC, fault containment, and repeated-lifetime markers remain
unproven when no QEMU/hardware runner is available.
