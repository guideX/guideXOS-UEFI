# guideXOS C# App Model Convergence — Phase 12

## Ring 3 process boundary and IPC architecture proposal

**Status:** Design proposal; implementation approval required before Phase 13.

**Audit date:** 2026-09-20

**Repository:** `D:\dev\guideXOSUEFI`

**Canonical architecture:** [`APP_MODEL_CONVERGENCE.md`](../APP_MODEL_CONVERGENCE.md)

This proposal records the live C# UEFI architecture and the smallest safe
follow-on proof. It does not implement Ring 3, a process manager, a syscall
subsystem, or GUI IPC.

## 1. Outcome

**Outcome C — architecture is viable, but the first user payload must be
native.** The current application image is a kernel-shaped NativeAOT composite:
it owns the managed heap, statics, runtime initialization, direct hardware
imports, and GUI objects. It is not a safe first isolated payload. A tiny
freestanding x86-64 payload with an explicitly defined ABI is the correct
Phase 13 candidate.

There is also one bounded prerequisite chain before that proof:

1. install and activate a real per-CPU TSS (`LTR`, valid `RSP0`, and reserved
   fault/entry stacks);
2. make the scheduler preserve a complete user return frame and process/CR3
   association;
3. create a process-owned page-table root with supervisor-only kernel mappings
   and explicit user mappings;
4. add one real user entry/return path and process-scoped fault termination;
5. only then exercise one bounded IPC/service request.

This is a prerequisite chain, not a reason to rewrite the scheduler or VM
subsystems wholesale.

## 2. Read-only preflight and baseline

The live Git state at audit start was:

| Field | Value |
|---|---|
| Branch | `main` |
| HEAD | `81f58d487926a8b1ad47ba90f9f6eef9a7b3b828` |
| Subject | `.gitignore changes and parity doc` |
| Upstream | `origin/main` |
| Ahead/behind | `0/0` |
| Worktree | clean |
| Untracked files | none at preflight |

The current source and canonical document contain the accepted Phase 11
clipboard contract and the Phase 10 resource/storage result. The Phase 11
record reports the bounded clipboard service, 10 registered services, and
green App Model/runtime/regression evidence in
`APP_MODEL_CONVERGENCE.md:2457-2568`. The current service registry constructs
notifications, settings, system information, dialogs, open/save, shell,
resources, storage, and clipboard in
`guideXOS/OS/ApplicationServiceRegistry.cs:67-90`.

The Phase 9/10/11 baseline remains present. The current build command

```text
dotnet build guideXOS\guideXOS.csproj --no-restore -p:SkipISO=true
```

completed with 0 errors and the existing warning baseline. No runtime source
or Git topology was changed by this verification.

## 3. Current scheduler and execution model

### 3.1 Scheduler

The active C# scheduler is a small global managed thread list in
`Kernel/Misc/Threading.cs:7-323`:

* `Thread` owns only a termination flag, an `IDT.IDTStackGeneric*`, a target
  CPU number, and an idle flag;
* `Thread.NewThread` allocates a raw managed allocator block for the frame and
  stack, initializes `CS=0x08`, `SS=0x10`, `RFLAGS=0x202`, and places a
  return-to-`ThreadPool.Terminate` address on the stack
  (`Threading.cs:17-32`);
* `ThreadPool.Threads` is one global `List<Thread>`;
* `Indexs[SMP.ThisCPU]` is the only per-CPU scheduler index;
* timer IRQ0 calls `ThreadPool.Schedule(stack)` after
  `SchedulingEnabled` is enabled (`Kernel/Misc/IDT.cs:368-405`);
* scheduling copies the current interrupt frame into the selected thread frame
  with `Native.Movsb`, then copies the selected frame back into the interrupt
  stack (`Threading.cs:270-322`);
* `ThreadPool.Locked` and `Locker` provide a coarse global scheduler lock;
  there is no process-aware run queue or address-space ownership.

`ThreadPool.Initialize` currently forces the UEFI bring-up path to a single
index array entry even when ACPI CPU information exists
(`Threading.cs:104-161`). The normal desktop depends on the current
IRQ0-driven scheduler and the existing regression gates, so the safe change is
to add process metadata to this model rather than replace it.

### 3.2 Thread context

`IDT.IDTStackGeneric` is a native/managed frame containing 15 general
registers, an error-code slot, a native vector slot, and the CPU return frame
(`Kernel/Misc/IDT.cs:73-108`). The native stubs in
`guideXOS/native_stubs.asm:297-426` construct this frame and return with
`iretq`.

The frame is sufficient as a starting representation for a user return frame,
because it already carries `RIP`, `CS`, `RFLAGS`, `RSP`, and `SS`. It is not
sufficient as-is for isolation because it has no process/CR3 reference, no
kernel-stack identity, no user-thread ID, no saved FS/GS base, and no defined
ownership for the stack memory.

### 3.3 Thread/process conflation

Current application code executes inside the single kernel NativeAOT image.
`Thread` is therefore a schedulable kernel execution record, not a process
thread. The current `Kernel/Misc/Process.cs` contains only an `AddressSpace`
placeholder and no process object, thread table, lifecycle, or exit state.

The new model must preserve this distinction:

```text
ApplicationInstance  = platform identity, lifecycle, AppId, windows, services
ApplicationProcess   = execution/isolation container, address space, threads
Thread               = schedulable register frame and stack within a process
```

Kernel threads and user threads may share the existing scheduler after the
minimum additions: `Process*`, `AddressSpace*`, privilege mode, kernel-stack
state, and a CR3 switch at dispatch. Kernel threads continue using the current
CPL0 path.

## 4. Privilege-transition audit

### 4.1 Existing support

`Kernel/Misc/GDT.cs:65-83` defines kernel code/data, user code/data, and a
64-bit TSS descriptor. User selectors are `0x1B` and `0x23`, with DPL 3
entries created at `GDT.cs:94-102`.

The TSS structure contains `RSP0` and seven IST slots, and
`GDT.SetKernelStack` writes `RSP0` (`GDT.cs:34-62`, `139-142`). The kernel
initialization allocates one 64 KiB stack and calls `SetKernelStack`
(`Kernel/Misc/EntryPoint.cs:124-138` and `390-398`).

`Kernel/Misc/IDT.cs:62-71` can raise a selected IDT gate to DPL 3. The current
boot path explicitly applies that to vector `0x80`.

`Native` exposes `ReadCR3`, `WriteCR3`, `ReadCR2`, `Invlpg`, GDT/IDT loading,
and interrupt control (`Kernel/Misc/Native.cs:26-90`).

### 4.2 Missing or unsafe pieces

The TSS is described and populated in memory, but `LTR` is deliberately not
called: `GDT.cs:134-136` says the native implementation is missing. There is
no per-CPU TSS or per-CPU `RSP0` update.

`SchedulerExtensions.EnterUserMode` calls `iret_to_user`
(`Kernel/Misc/SchedulerExtensions.cs:5-15`), but the only managed export is a
no-op diagnostic stub in `Kernel/Misc/UserModeStub.cs:4-10`. The checked-in
`guideXOS/native_stubs.asm` does not provide a real user transition.

All generated IDT gates are initially `0x8E` with selector `0x08`
(`guideXOS/native_stubs.asm:445-473`). The helper changes DPL but does not
create a process-aware entry path. The managed exception handler currently
prints diagnostic registers, shows a panic screen, and halts for all exceptions
(`Kernel/Misc/IDT.cs:300-360`). A user fault would therefore not yet be
contained.

There is no evidence of `SYSCALL/SYSRET`, `LSTAR`, `STAR`, `SFMASK`, `SWAPGS`,
or an `LTR` path in the C# UEFI tree. `GS` and `FS` are not used as a defined
per-CPU/user TLS boundary. SMEP/SMAP are not configured in this tree.

**Conclusion:** CPL3 descriptors exist as scaffolding; a complete CPL3 entry,
return, kernel-stack switch, and process fault path do not yet exist.

## 5. Memory-management audit

### 5.1 Boot and current page tables

The UEFI bootloader builds an identity-oriented page table and switches to it
before calling C# (`guideXOSBootLoader/paging.h:6-37` and
`guideXOSBootLoader/main.cpp:876-1053`). The loader maps the low 1 MiB,
kernel image, stack, boot information, ramdisk, ACPI, loader/trampoline, the
allocator window at `0x04000000` for 1 GiB, and low memory. Its default PTEs
are present and writable, with NX not set (`paging.h:13-23`,
`paging.cpp:120-122`). It does not create a user address space.

The C# UEFI entry deliberately does not call `PageTable.Initialize`; it relies
on the bootloader's CR3 (`Kernel/Misc/EntryPoint.cs:69-89`). Consequently,
`PageTable.PML4` is not populated by the active UEFI path. The legacy path does
initialize one shared PML4 and identity-maps from 4 KiB through 4 GiB
(`Kernel/Misc/PageTable.cs:10-25`).

### 5.2 Existing mapping API

`PageTable` has root-aware `GetPageOnRoot` and `MapOnRoot`, plus a `user` flag
that sets U/S on intermediate and leaf entries
(`Kernel/Misc/PageTable.cs:28-115`). This is useful proof material, but it is
not a process VM implementation:

* `MapUser` operates on the single static `PML4`;
* `Next` treats allocator virtual addresses as page-table addresses and does
  not separate physical frame allocation from mapping;
* `MapOnRoot` calls `Invlpg(PhysicalAddress)` even though `invlpg` takes a
  virtual address (`PageTable.cs:79-95` and `Native.cs:86-90`);
* there is no unmap, page-table ownership, page refcount, TLB shootdown, or
  root destruction path;
* copying the current PML4 is unsafe because it copies the whole identity
  mapping and all kernel/allocator/MMIO aliases.

`Kernel/Misc/Process.cs:5-19` confirms that `AddressSpace` is only a
placeholder: it allocates a page and copies the entire current PML4.

### 5.3 Allocator

`Kernel/Misc/Allocator.cs:9-171` is a bounded linear page-run allocator over a
1 GiB virtual/physical identity window. It tracks tags, owners, and corruption
guards, but it is not a physical-frame allocator and has no process ownership
or page permission metadata. Its existing `ThreadMeta`, `ThreadStack`,
`ExecImage`, and `ExecStack` tags can support later accounting but must not be
mistaken for isolation.

### 5.4 Required VM direction

Phase 13 needs a new process root built from explicit mappings:

* copy only supervisor kernel mappings that are valid in the new root;
* never clone the whole current PML4;
* allocate user pages from a frame allocator, zero them, and record ownership;
* map user code read/execute, read-only data read-only, writable data/heap
  read/write, and stack read/write;
* set U/S on every level of a user mapping and leave kernel mappings U/S=0;
* make PTE permissions explicit and use NX for non-code pages if EFER.NXE is
  enabled;
* reclaim all process page-table and user frames during exit.

The exact kernel mapping placement is a prerequisite because the active UEFI
image currently executes from low identity-mapped addresses. The preferred
long-term design is one shared supervisor-only high-half kernel mapping. A
short first proof may retain the existing kernel placement only if the process
root contains no user-accessible aliases to it and all kernel mappings remain
U/S=0.

## 6. Executable and runtime audit

### 6.1 Current C# image

`guideXOS/guideXOS.csproj:3-28` targets `net7.0`, x64, and the old
`Microsoft.DotNet.ILCompiler` alpha package. It sets `IlcSystemModule`,
`KMainWrapper`, native linker options, and directly includes native libraries
and `native_stubs.obj` (`guideXOS.csproj:107-139`). The image is a fixed-base
NativeAOT kernel composite, not a relocatable user executable.

The C# runtime initializes modules and GC statics before normal code runs
(`Kernel/Misc/EntryPoint.cs:69-83`). The image uses allocator-backed managed
objects, static constructors, global GUI state, direct `DllImport("*")`
hardware calls, and process-wide runtime state. None of these are currently
expressed as a user-mode PAL or per-process runtime instance.

### 6.2 Existing executable formats

`Kernel/Misc/GXMLoader.cs:7-28` recognizes a bounded single-image GXM/MUE
header. Its non-GUI path copies bytes into allocator memory, maps them user
accessible in the current root, creates a raw stack, and calls the no-op user
transition (`GXMLoader.cs:80-88`). It does not validate segment permissions,
relocations, address bounds, ownership, or process cleanup. It is not a safe
Ring 3 loader yet.

The PE-to-ELF scripts (`Tools/pe_to_elf.py`, `Tools/pe_to_elf_v2.py`) create
minimal ELF64 `PT_LOAD` records from PE sections and preserve fixed image
addresses. They do not provide relocations, a dynamic linker, TLS, a user
runtime, or process loading semantics. The generated image is suitable as a
kernel packaging bridge, not as evidence of a user loader.

### 6.3 First payload decision

The smallest realistic Phase 13 executable is a **freestanding native x86-64
test payload**, linked for a fixed user virtual address and containing:

* one entry function;
* a private stack supplied by the kernel;
* no managed runtime, heap, TLS, shared library, ELF relocation, or GUI;
* an `int 0x80` request using fixed-width ABI structures;
* an explicit exit request.

It should first request the system-information snapshot because that contract
is bounded and non-GUI. Clipboard is a later optional second call; it is a
real Phase 11 service but introduces shared session state and is not needed to
prove the first transport.

NativeAOT is a later candidate only after a separate user-mode PAL/runtime
contract exists for allocations, TLS/GS, thread creation, exception/unwind,
GC, static initialization, and service calls. A specially built NativeAOT
payload may then follow the same startup block and IPC ABI, but the current
kernel composite cannot be reused unchanged.

## 7. Proposed process object

Add a bounded kernel-owned `ApplicationProcess` concept. The eventual object
should contain:

```text
ProcessHandle            generation-safe typed handle
ApplicationInstanceHandle owning instance identity
AddressSpaceId           owned page-table root / CR3 record
ProcessLifecycleState    Created, Loading, Runnable, Exiting, Terminated, Failed
PrimaryThreadHandle      first user thread
ExitCode                 bounded normal/explicit exit code
Fault                    vector, RIP, error code, optional fault address
OwnedHandleTable         process-scoped typed handles
ServiceEndpoint          process-to-App-Model request endpoint
BoundedDiagnostics       counters and last bounded failure
```

The process owns execution resources and fault state. It does not own AppId,
descriptor metadata, taskbar identity, service authority, or application
lifecycle transitions.

The process table must be bounded and generation-safe. It must not expose a
C# object pointer, allocator address, CR3 value, or raw `Thread*` to user code.

## 8. ApplicationInstance, process, and thread relationship

The existing `ApplicationInstance` is already the semantic authority. It owns
descriptor identity, bounded launch context, lifecycle state, and owned Window
relationships (`guideXOS/OS/ApplicationInstance.cs:155-247`). Its handle packs
slot and generation (`ApplicationInstance.cs:60-116`), and lifecycle changes
notify the service registry (`ApplicationInstance.cs:282-289`). Window attach
and detach remain instance operations (`ApplicationInstance.cs:299-349`).

Phase 12 therefore defines:

```text
ApplicationInstance
  └── zero or one execution backend in the first implementation
        ├── InProcessBackend: current managed/kernel execution
        └── IsolatedProcessBackend: one ApplicationProcess
              └── one or more ProcessThread records
```

Future multi-process applications are permitted by the architecture but not
required by Phase 13. A process fault records bounded failure on the instance;
it does not become the lifecycle authority and does not corrupt the registry.

## 9. Handle model

Use typed generation-safe handles rather than a universal integer soup. The
existing `ApplicationInstanceHandle` and `ApplicationServiceRequestHandle`
show the desired slot/generation pattern
(`ApplicationInstance.cs:60-116`; `ApplicationServices.cs:68-126`).

The initial kernel handle types are:

```text
ProcessHandle       { type tag, slot, generation }
ThreadHandle        { type tag, slot, generation }
ServiceRequestHandle{ type tag, slot, generation }
```

An application-instance handle may be carried in a trusted startup snapshot or
diagnostic response, but user code cannot manufacture authority from it. The
kernel revalidates type, slot, generation, ownership, and lifecycle state on
every operation. A service request handle is valid only for its originating
process and service ID. Process exit increments generations and invalidates all
owned handles.

Window presentation endpoints, resource handles, and storage handles are
future typed capabilities. They are not raw `Window`, `WindowManager`, file
system, or renderer pointers.

## 10. Kernel/user ABI

The first ABI is versioned, little-endian, fixed-width, and independent of C#
object layout.

```text
ABI version:             u16 major, u16 minor; Phase 13 = 1.0
Calling convention:      x86-64 System V-like register convention for payload;
                         kernel entry is the architecture-defined int 0x80 frame
Request header:          u32 size, u16 version, u16 operation, u32 flags,
                         u32 requestId
Payload:                 fixed-width integers, enums, handles, offsets/lengths
Result header:           u32 size, i32 status, u32 requestId, u32 payloadLength
Maximum copied payload:  4096 bytes per request/response in Phase 13
```

The first operations are deliberately few:

```text
0  ABI_PROBE
1  SERVICE_CALL
2  PROCESS_EXIT
3  DEBUG_MARKER (diagnostic build only; bounded)
```

`SERVICE_CALL` contains a service ID, operation ID, bounded request bytes, and
an output capacity. The service registry remains the semantic dispatcher. The
ABI status vocabulary maps to the existing bounded result vocabulary:
`Success`, `InvalidContext`, `InvalidRequest`, `NotFound`,
`ResourceUnavailable`, `PermissionDenied`, `Unsupported`, `Conflict`,
`Cancelled`, `InvalidState`, `UnsupportedTarget`, and `BackendFailure`
(`ApplicationServices.cs:53-66`).

There are no managed references, object headers, delegates, callbacks, virtual
tables, `string` layouts, raw kernel addresses, or implicit pointer ownership
in the ABI. Unknown versions, operations, sizes, and flags are rejected.

## 11. Syscall/trap entry choice

### Option 1 — `syscall/sysret`

This is efficient and a good long-term AMD64 path, but it requires a correct
per-CPU entry design: `STAR`, `LSTAR`, `SFMASK`, kernel stack selection,
`RCX/R11` preservation, canonical return checks, `SWAPGS`/GS policy, and safe
handling of faults before a user frame is fully recorded. None of that is
implemented in the C# UEFI tree.

### Option 2 — software interrupt/call gate

`int 0x80` naturally creates an `iretq` return frame and matches the existing
IDT/native-stub architecture. It costs more cycles and requires a DPL 3 gate,
but it is easier to validate for one bounded proof. A call gate would add
descriptor complexity without helping the first service request.

### Option 3 — narrow trap entry for IPC/service calls

This keeps the kernel primitive set small and prevents a syscall-heavy API.
The current IDT path is close, but the current gate is generated as an
interrupt gate and the handler is panic-oriented. A real Phase 13 entry must
use a dedicated, audited vector path, a loaded TSS, a kernel stack, complete
register capture, pointer validation, and `iretq` return.

### Selection

Select **one narrow `int 0x80` IPC entry**, implemented first as a DPL 3
interrupt gate because it matches the existing `iretq` frame and masks nested
interrupts at entry. It supports only `ABI_PROBE`, `SERVICE_CALL`,
`PROCESS_EXIT`, and the bounded diagnostic marker. Do not expose hundreds of
syscalls. Do not add `syscall/sysret` until the per-CPU GS/TSS and user return
path are independently proven.

## 12. IPC and service dispatch

The transport is copy-in/copy-out:

```text
Ring 3 payload
  → int 0x80(SERVICE_CALL, user request range)
  → kernel validates process, ABI, handle, and user ranges
  → kernel copies request into bounded kernel storage
  → kernel derives ApplicationServiceContext from the process owner
  → ApplicationServiceRegistry dispatches the typed service
  → kernel bounds/copies the result to the user output range
  → iretq returns status and bytes copied
```

Use no shared request page, arbitrary shared memory, sockets, or zero-copy
transport in Phase 13. Copying is deterministic and makes user-memory lifetime
simple. A per-process bounded queue can be added later if asynchronous
dialogs/input need it, but it is not required for the first proof.

The existing service surface is already bounded and serializable in intent:
notifications, settings, system information, dialogs, open/save, shell/open,
resources, storage, and clipboard. The transport adapter converts ABI records
to existing C# value requests and converts results back; it does not expose
`ApplicationServiceAccess`, `ApplicationServiceContext`, Window objects, or
backend instances to the payload.

## 13. Service-context derivation and anti-spoofing

The user payload supplies only its process-owned service request. It does not
construct an `ApplicationServiceContext`.

The kernel performs:

```text
current thread → owning ApplicationProcess
             → owning ApplicationInstanceHandle
             → authoritative ApplicationInstanceRegistry lookup
             → ApplicationServiceRegistry.TryCreateContext(handle, ...)
             → service state/capability validation
```

The current C# registry already re-resolves the generation-safe handle,
compares the descriptor ID, and rejects stale/terminal contexts
(`ApplicationServiceRegistry.cs:233-295` and `1860-1890`). The isolated
adapter must invoke that authority with a kernel-derived handle, never with
user-provided AppId text.

This preserves the existing clipboard source rule: the service records the
validated source AppId, not a source object supplied by the caller. The same
rule applies to storage namespace and shell/document identity.

## 14. User-pointer validation

Create one kernel helper family and require every ABI operation to use it:

```text
TryValidateUserRead(process, address, length)
TryValidateUserWrite(process, address, length)
TryCopyFromUser(process, destination, userAddress, length)
TryCopyToUser(process, userAddress, source, length)
```

The helper rejects:

* null when a non-empty buffer is required;
* `address + length` overflow or wraparound;
* non-canonical x86-64 addresses;
* kernel-half addresses and supervisor-only mappings;
* unmapped or partially mapped pages;
* ranges lacking the requested read/write permission;
* lengths above the operation and global maximum;
* buffers that cross a process mapping boundary unexpectedly.

The current VM is not concurrent enough to claim a safe unmap/mutation model.
Phase 13 must either pin/lock the address-space mapping for the copy or keep
the process single-threaded and forbid unmap while a request is active. The
kernel must never dereference a user pointer before validation and must return
`InvalidRequest`/`EFAULT`-equivalent status rather than panic.

## 15. Lifecycle mapping

The App Model remains authoritative:

| App Model event | Isolated backend behavior |
|---|---|
| Registered → Loading | Create process, root, endpoint, and startup block. |
| Loading → Initialized | User entry reports `initialized` through bounded ABI. |
| Initialized → Running | Process is runnable and service calls are allowed by policy. |
| Activated | Deliver an App Model activation event later; no process identity change. |
| Inactive | Process remains runnable; service policy controls eligible calls. |
| Suspended | Phase 12 records cooperative rendezvous semantics only; no hard freeze required. |
| Closing | Send a bounded close/termination request; wait only within a bounded policy. |
| Normal process exit | Observe exit code, clean resources, then terminate or reuse instance by policy. |
| Fault | Mark process failed, stop its threads, clean resources, and record bounded fault state. |

The existing lifecycle explicitly says `Suspended` is cooperative application
quiescence rather than scheduler/process freezing
(`ApplicationInstance.cs:19-24`). Preserve that contract.

## 16. Fault containment

User-originated page fault, general protection fault, invalid opcode, divide
error, stack fault, bad syscall, or invalid pointer must take the process to
`Failed`/`Terminated` without entering the kernel panic path.

The bounded fault record contains:

```text
ProcessHandle
ApplicationInstanceHandle
vector
errorCode
RIP
optional fault address (CR2 for page fault)
```

The current `IDT.intr_handler` and `Panic.ShowEnhancedCrashScreen` remain the
kernel-fault path for CPL0 faults. The first change must classify the saved
`CS & 3` before choosing that path. A CPL3 fault queues process teardown and
returns only after its user threads are stopped or marked non-runnable. A
fault in the fault/teardown path is a kernel bug and may still use the kernel
panic path.

Unhandled managed exceptions are not a Phase 13 concern because the first
payload is native and freestanding. The later NativeAOT adapter must define a
bounded runtime failure translation.

## 17. UI, input, and window boundaries

Phase 13 is headless.

The compositor, `WindowManager`, `Window`, framebuffer, controls, renderer,
and raw input arrays remain kernel/App Model owned. An isolated payload never
receives those object pointers. Later stages are:

1. **Headless process:** system information and exit only.
2. **Presentation endpoint:** kernel allocates a typed, generation-safe window
   endpoint associated with the `ApplicationInstance`; the process receives
   only the endpoint handle.
3. **Bounded protocol:** request/response commands for create, draw, text,
   close, and event polling. The compositor remains kernel-side.

Input will eventually be delivered as bounded events in a process-owned queue:
key, character, pointer, focus, activation, and close. No interrupt state,
global input arrays, or raw device objects cross the boundary.

The authoritative relationship stays:

```text
ApplicationInstance → semantic Window/presentation ownership
Process             → execution source for requests/events
```

Process death deterministically detaches presentation endpoints and causes the
existing instance window cleanup path to run. Taskbar grouping remains based on
application-instance ownership, not PID.

## 18. Startup block and startup sequence

The kernel creates a fixed-width immutable startup block. It contains:

```text
u16 abiMajor, u16 abiMinor
u32 blockSize, u32 flags
ProcessHandle process
u64 applicationInstanceHandleValue (opaque snapshot, not authority)
u32 appIdLength + bounded UTF-8 AppId bytes
u32 argumentLength + bounded launch-argument bytes
u32 activationLength + bounded document/activation bytes
u32 endpointFlags
```

There are no kernel pointers, CR3 values, C# object addresses, delegates, or
unbounded strings. The block is copied into a read-only user mapping.

Startup sequence:

1. App Model creates the `ApplicationInstance` and selects an execution
   backend.
2. Kernel creates `ApplicationProcess` and generation-safe process handle.
3. Kernel allocates a process root and maps only approved supervisor mappings.
4. Kernel validates and maps the fixed native payload segments.
5. Kernel allocates user stack plus guard/unmapped page and maps startup block.
6. Kernel creates a primary thread with a complete CPL3 frame, user CS/SS,
   entry RIP, and user RSP.
7. Kernel installs the process endpoint and service owner relationship.
8. Scheduler dispatches the thread after TSS/RSP0 and fault entry are valid.
9. Payload reports `initialized`; App Model transitions the instance.
10. Payload reports `running`; normal service calls become eligible.

Any failure rolls back in reverse order: no stale instance process binding,
no taskbar owner, no service context, no open request, and no leaked user page.

## 19. Exit and cleanup sequence

Normal return, explicit `PROCESS_EXIT`, close termination, bad syscall, and
user fault all converge on one idempotent teardown path:

1. mark the process stopping and reject new syscalls;
2. stop or detach user threads, including the primary thread;
3. cancel outstanding user-originated service requests and mark their handles
   failed/cancelled;
4. detach process presentation endpoints and close transient dialogs according
   to App Model policy;
5. invalidate and reclaim process-owned handles;
6. unmap and release user code/data/heap/stack/startup and page-table frames;
7. mark the process `Terminated` or `Failed` with exit/fault data;
8. notify the owning `ApplicationInstance` through the lifecycle adapter;
9. clear instance/process association and apply reuse/termination policy;
10. release the bounded process record only after stale-handle generation is
    advanced.

Cleanup must not recursively launch UI, synchronously re-enter the service
registry, or wait on the current exiting thread. The existing window cleanup
and service-session rules remain the final authority for their objects.

## 20. Proposed initial x86-64 virtual layout

This is a no-ASLR, 4 KiB-page proposal for a 48-bit canonical address space.
The exact kernel base requires the kernel-placement prerequisite described in
Section 5.

| Region | Range | Permissions/owner |
|---|---:|---|
| Null guard | `0x0000000000000000–0x0000000000010000` | unmapped |
| User image | `0x0000000000400000–0x0000000000800000` | payload RX; page-aligned segments |
| User read-only data | `0x0000000000800000–0x0000000000C00000` | R, NX |
| User data/BSS | `0x0000000000C00000–0x0000000010000000` | RW, NX |
| User heap | `0x0000000010000000–0x0000000040000000` | RW, NX, bounded growth |
| User IPC/startup window | `0x0000000040000000–0x0000000040010000` | startup R; optional bounded ABI page |
| User stack guard | `0x00007FFF7FFE0000–0x00007FFF7FFF0000` | unmapped |
| User stack | `0x00007FFF7FFF0000–0x0000800000000000` | RW, NX, downward-growing |
| Kernel shared mapping | `0xFFFF800000000000–0xFFFF900000000000` | supervisor-only, shared |
| Kernel/device remainder | `0xFFFF900000000000–0xFFFFFFFFFFFFFFFF` | supervisor-only, explicit mappings |

The user stack top is below the canonical boundary and leaves one guard page.
The Phase 13 image is fixed at `0x400000`; no dynamic linker, shared library,
or ASLR is required. If the current low-linked kernel cannot yet use a high-half
mapping, the process root may use supervisor-only low kernel mappings as a
temporary implementation detail, but user U/S bits and aliases must be tested
before entering CPL3.

## 21. Security invariants

These become Phase 13 test assertions:

1. user pages cannot write or execute supervisor-only kernel pages;
2. every user pointer/range is validated before kernel access;
3. user code cannot forge process, instance, AppId, or service identity;
4. every handle is type- and generation-validated against its owning process;
5. a process fault cannot crash or panic the kernel;
6. process exit revokes all process handles and resources;
7. service requests and results remain bounded and versioned;
8. user code cannot directly call WindowManager, Desktop, framebuffer, or
   filesystem internals;
9. storage service operations remain confined to the owning AppId namespace;
10. clipboard remains an explicit session-global shared-state exception with
    validated source identity;
11. page tables, physical frames, and CR3 roots are kernel-owned;
12. no user mapping is executable and writable at the same time unless a
    later reviewed runtime explicitly requires it;
13. unknown ABI versions, operation numbers, flags, and lengths are rejected;
14. teardown is idempotent and cannot resurrect a stale process or instance.

## 22. Architecture comparison

| Architecture | Assessment |
|---|---|
| A — syscall-heavy kernel API | Lowest apparent service latency but duplicates the existing App Model, expands the security surface, couples applications to kernel internals, and makes Server convergence poor. Reject. |
| B — minimal kernel primitives + App Model IPC | Best semantic fit. It keeps service contracts above the kernel and makes in-process and isolated backends interchangeable. Adopt as the ownership principle. |
| C — hybrid: small syscall substrate + typed service dispatch | Best first transport. One narrow entry validates/copies a typed service request, while the App Model remains the service authority. Adopt as the implementation shape for B. |

**Recommended architecture:** **C as the transport realization of B** — a
minimal kernel substrate for process/address-space/thread/handle/copy/IPC
operations, plus typed dispatch into the existing App Model services. No POSIX
layer, no broad syscall namespace, and no GUI protocol in Phase 12.

This is compatible with Phases 1–11 because `ApplicationInstance`, bounded
handles, service IDs, result codes, storage confinement, and clipboard source
validation stay unchanged. It is also compatible with Server's semantic
separation without copying its hosted implementation or making C# depend on
Server internals.

## 23. Server comparison

The Modern Server has useful semantic precedents:

* `process.h`/`process.cpp` provide a process identity, exit/tombstone data,
  mailbox, lifecycle, and process table, although the hosted implementation
  uses dedicated host threads rather than isolated hardware address spaces;
* the Advanced Server has explicit kernel process/thread structures in
  `kernel/core/process.cpp` and an address-space/frame mapper in
  `kernel/core/address_space.cpp`;
* `kernel/arch/amd64/syscall.cpp` documents a `SYSCALL/SYSRET` direction,
  fixed-width `SyscallArgs`, status codes, and an eventual assembly entry;
  its current entry and fault handlers are still TODO/halt stubs, so they are
  architectural reference rather than drop-in implementation;
* the Advanced Server NativeAOT application bridge uses append-only fixed-size
  host tables and bounded callbacks in `kernel/core/nativeaot_application.cpp`.

The C# convergence should borrow the semantic boundaries — process identity,
address-space ownership, bounded transport, and application/service separation
— but not blindly port hosted `std::thread`, native table, allocator, or
Server-specific internals. Legacy remains historical reference only and is
not modified.

## 24. Exact Phase 13 proof proposal

After design approval, implement only this deterministic proof:

```text
boot normally
  → create one ApplicationInstance with a headless isolated backend
  → create one ApplicationProcess and one user address-space root
  → map one fixed native payload, read-only startup block, and user stack
  → enter CPL3 with distinct user RIP/RSP
  → issue ABI_PROBE and one SystemInformation SERVICE_CALL
  → return a bounded snapshot to user memory
  → reject one invalid syscall number
  → reject one invalid/null/overflowing user pointer
  → deliberately fault one user page
  → contain the fault and clean the process
  → verify process/address-space/handle/App Model cleanup
  → verify desktop heartbeat continues
```

Do not add GUI, clipboard, storage, filesystem, NativeAOT, shared memory,
dynamic linking, multi-process applications, or broad migration in this proof.

### Required bounded markers

```text
R3_PROCESS_CREATED=1
R3_ADDRESS_SPACE_CREATED=1
R3_USER_MAPPING_READY=1
R3_CPL3_ENTERED=1
R3_SYSCALL_ENTERED=1
R3_CALLER_VALIDATED=1
R3_SERVICE_DISPATCHED=1
R3_RESPONSE_COPIED=1
R3_INVALID_POINTER_REJECTED=1
R3_INVALID_SYSCALL_REJECTED=1
R3_USER_FAULT_CONTAINED=1
R3_PROCESS_EXITED=1
R3_ADDRESS_SPACE_RECLAIMED=1
R3_PROCESS_HANDLES_RECLAIMED=1
R3_APPMODEL_CLEANUP_COMPLETE=1
R3_KERNEL_HEARTBEAT_CONTINUES=1
```

The acceptance gate is: user CPL3 confirmed, distinct user stack confirmed,
user-only mappings confirmed, one safe IPC/service round trip confirmed,
invalid request paths rejected, deliberate user fault contained, all process
resources reclaimed, and the existing desktop/regression baseline unchanged.

## 25. Risks and blockers

* **TSS blocker:** no `LTR`, per-CPU TSS, or reliable `RSP0` entry path.
* **Page-table blocker:** active UEFI CR3 is bootloader-owned and
  `PageTable.PML4` is not initialized on that path.
* **Isolation blocker:** current loader maps broad identity ranges RW and
  executable; current `AddressSpace` clones the full root.
* **Fault blocker:** all current exceptions enter the panic/halt path.
* **ABI blocker:** no current process table, typed process handles, or
  user-pointer validation helpers.
* **Runtime blocker:** current NativeAOT image requires kernel globals, direct
  hardware imports, and the managed runtime; it is not a user payload.
* **SMP risk:** scheduler and TSS state are not per-CPU enough for a general
  multi-core user scheduler. Phase 13 should use the existing single-CPU
  proof configuration unless the current boot selects otherwise.
* **Address placement risk:** a shared high-half kernel mapping requires a
  deliberate kernel-link/bootloader mapping decision; do not hide that in an
  ad hoc PML4 copy.

No risk justifies changing Server or Legacy in this phase.

## 26. Documentation, files, and Git operations

### Documentation change

Added this one long-lived Phase 12 design/spec document. The canonical
`APP_MODEL_CONVERGENCE.md` was not rewritten and no debugging/milestone journal
was created.

### Exact files changed

```text
Docs/PHASE12_RING3_PROCESS_BOUNDARY_DESIGN.md
```

No C# source, assembly, bootloader, Server, or Legacy file was modified.

### Git operations performed

Read-only `status`, `log`, branch/upstream, source audits, Server/reference
audits, and a local `dotnet build --no-restore -p:SkipISO=true` verification.
No commit, fetch, push, rebase, reset, stash, branch, detached HEAD, or
worktree operation was performed. No branch/stash/worktree/detached HEAD was
created.

## 27. Approval gate and next action

**Stop here for design approval.** The recommended next action is to approve
or amend this proposal, then create a separate Phase 13 implementation plan
for the bounded native headless proof. No Ring 3 implementation should begin
until the TSS/entry, process-root, pointer-validation, and fault-containment
contracts are accepted.

The defining boundary remains:

> Ring 3 changes where an application executes; it must not redefine what a
> guideXOS application is.

## 28. Phase 13 implementation and acceptance result

Phase 13 was implemented as the bounded first native proof described above.
The result is **Outcome A for the first synchronous Ring 3 boundary proof**:
the native process enters real CPL3, completes a bounded ABI round trip,
rejects invalid input, exits normally, contains a deliberate user fault,
reclaims its resources, and returns to the ordinary UEFI desktop initialization
path. The general preemptive scheduler handoff of an arbitrary user thread is
still deliberately not claimed by this phase; the proof selector reports
`RING3_SCHEDULER_CONTEXT_SWITCHING=0` and uses one synchronous diagnostic
fixture on the existing bootstrap CPU.

### Implemented substrate

* GDT now installs a 64-bit available TSS and executes `ltr 0x28`. `RSP0` is
  updated to the active user thread's private kernel stack before entry.
  Boot markers prove TSS installation, TR load, RSP0 configuration, and stack
  validity.
* A fixed four-slot process table provides generation-safe handles. A handle
  contains a slot and generation; cleanup removes the slot and increments its
  generation, so stale handles resolve to null.
* `AddressSpace` clones the current CR3 root, tracks newly allocated page-table
  branches, maps deterministic user code/data/stack regions, and releases the
  tracked branches and backing pages. Shared supervisor branches are cloned
  before a user U/S bit is introduced.
* The fixed layout is: code
  `0x0000400000000000..0x0000400000001000`, writable data at
  `0x0000400000002000`, and stack
  `0x00007FFF00000000..0x00007FFF00010000`. The page-table walk rejects
  non-canonical, kernel, unmapped, partially mapped, overflowed, and
  over-maximum ranges. Code is user-readable and read-only; data and stack are
  user-writable; kernel mappings remain supervisor-only.
* The payload is freestanding x86-64 NASM in `guideXOS/native_stubs.asm`.
  It has no managed runtime, GC, libc, filesystem, GUI, or NativeAOT dependency.
  Existing repository NASM integration assembles it into the kernel image and
  copies it into the process-owned code page.
* The ABI is version 1 over the Phase 12-approved `int 0x80` gate. Operations
  are `Ping` (returns ABI version 1), `Exit`, `ValidateRead`, and
  `ValidateWrite`; status values are fixed-width primitives. Caller identity is
  derived from `ThreadPool.CurrentProcess`, never from a user-supplied handle.
  The proof emits `RING3_CR3_USER_ACTIVE=1` after switching to the private root
  and `RING3_CR3_KERNEL_RESTORED=1` before cleanup.

### Runtime proof

The final Ring 3 QEMU run emitted, in order, the TSS/TR/RSP0 markers, three
process creations, deterministic code/stack ranges, `RING3_ENTER_CPL=3`, a
successful Ping, six invalid-pointer rejections covering null, kernel,
cross-boundary, overflow, over-maximum, and read-only-write cases, an invalid
operation rejection, three normal cleanup sequences, a deliberate CPL3 page
fault, a bounded fault record, fault containment, three address-space/kernel
stack reclamations, and three stale-handle rejections. It then emitted:

```text
RING3_KERNEL_HEARTBEAT_CONTINUED=1
RING3_PROOF_COMPLETE=1
RING3_PROOF_RETURNED_TO_ENTRYPOINT=1
RING3_DESKTOP_CONTINUED=1
KERNELMAIN_ENTRY_RAW
[KERNELMAIN]
[BOOT_MODE] UEFI
```

The contained fault record in that run was vector `0x0E` (page fault), with
RIP `0x000040000000000A` and CR2 `0x00007FFF00010000`, the first unmapped byte
above the deterministic user stack.

The fault path distinguishes `(CS & 3) == 3` from CPL0 exceptions; only the
former is converted into a failed process. CPL0 faults retain the existing
kernel panic path. Normal exit and fault cleanup both terminate the user
thread before releasing its address-space branches, user pages, and kernel
stack, then invalidate the generation-safe handle.

The Ring 3 harness now waits for
`RING3_PROOF_RETURNED_TO_ENTRYPOINT=1`, rather than stopping at the internal
proof-complete marker. The ordinary production selector was also rerun after
the change: it reached `TIMEOUT_SUCCESS`, continuous desktop entry, graphics
validity, and advancing heartbeats (`last frame 300`, timer `5545`). The
AppModel selector reached `DIAGNOSTIC_COMPLETE` with App Model validation true,
12/12 descriptor/factory checks, fallback count 0, and legacy backend count 0.

The same AppModel run also passed the Phase 8 service self-test, Phase 9
dialog/file/shell self-tests, Phase 10 resource/chunk/storage self-tests, and
Phase 11 clipboard contract/self-test/generation/lifecycle/reset checks. It
reported zero orphan dialogs, zero stale service contexts, zero stale
application instances, zero stale taskbar projection entries, and zero legacy
backend calls. The focused NativeInput run reached `TIMEOUT_SUCCESS` with
graphics valid, `ThreadPool.Locked=0`, and zero keyboard or mouse drops. The
ContextMenu run reached `CONTEXT_MENU_COMPLETE` with graphics valid, zero input
drops, and zero bad-bounds observations. The AppRuntime run reached
`APP_RUNTIME_COMPLETE` with four heartbeats, graphics valid, zero input drops,
zero runtime faults, and zero factory fallbacks. The final Ring 3 serial log is
`phase13-ring3-serial.log` in the repository root.

### Scope boundary carried to Phase 14

This phase does not expose the process as a normal Start application, does not
add a service bridge, and does not enable general user-thread preemption. The
next slice is to integrate the proven process/address-space/TSS substrate with
the scheduler's ordinary context-switch path, then add a small copied service
request only if that integration remains bounded. Managed Ring 3 applications,
GUI ownership, IPC expansion, and runtime migration remain out of scope.
