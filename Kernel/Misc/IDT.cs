using guideXOS;
using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Helpers;
using guideXOS.Misc;
using guideXOS.GUI;
using Internal.Runtime.CompilerServices;
using System.Runtime;
using System.Runtime.InteropServices;
using static Internal.Runtime.CompilerHelpers.InteropHelpers;

public static class IDT {
    [DllImport("*")]
    private static extern unsafe void set_idt_entries(void* idt);
#if UEFI_DIAGNOSTIC_FAULT_BYTES_PROBE
    [DllImport("*")]
    private static extern void TriggerFaultBytesProbe();
#endif

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct IDTEntry {
        public ushort BaseLow;
        public ushort Selector;
        public byte Reserved0;
        public byte Type_Attributes;
        public ushort BaseMid;
        public uint BaseHigh;
        public uint Reserved1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct IDTDescriptor {
        public ushort Limit;
        public ulong Base;
    }

    private static IDTEntry[] idt;
    public static IDTDescriptor idtr;


    public static bool Initialized { get; private set; }


    public static unsafe bool Initialize() {
        _interruptNestingByCpu = new int[256];
        _activeInterruptVectorByCpu = new int[256];
        for (int i = 0; i < _activeInterruptVectorByCpu.Length; i++)
            _activeInterruptVectorByCpu[i] = -1;

        idt = new IDTEntry[256];

        set_idt_entries(Unsafe.AsPointer(ref idt[0]));

        fixed (IDTEntry* _idt = idt) {
            idtr.Limit = (ushort)((sizeof(IDTEntry) * 256) - 1);
            idtr.Base = (ulong)_idt;
        }

        Native.Load_IDT(ref idtr);

        Initialized = true;
#if UEFI_DIAGNOSTIC_FAULT_BYTES_PROBE
        TriggerFaultBytesProbe();
#endif
        return true;
    }

    public static void Enable() {
        Native.Sti();
    }

    public static void Disable() {
        Native.Cli();
    }

    public static unsafe void AllowUserSoftwareInterrupt(byte vector) {
        if (!Initialized) return;
        // Set DPL=3 for the given vector gate to allow int from ring3
        fixed (IDTEntry* p = idt) {
            IDTEntry* e = &p[vector];
            // Type 0xEE? preserve type, set DPL bits (bits 5-6) to 3 and Present bit
            e->Type_Attributes = (byte)((e->Type_Attributes & 0x9F) | (3 << 5) | 0x80);
        }
        Native.Load_IDT(ref idtr);
    }

    public struct RegistersStack {
        public ulong rax;
        public ulong rcx;
        public ulong rdx;
        public ulong rbx;
        public ulong rbp;   // Added - was missing! Must match assembly PUSH_GPRS order
        public ulong rsi;
        public ulong rdi;
        public ulong r8;
        public ulong r9;
        public ulong r10;
        public ulong r11;
        public ulong r12;
        public ulong r13;
        public ulong r14;
        public ulong r15;
    }

    //https://os.phil-opp.com/returning-from-exceptions/
    public struct InterruptReturnStack {
        public ulong rip;
        public ulong cs;
        public ulong rflags;
        // The CPU pushes these two fields only on a privilege transition or
        // when the IDT gate selects an IST stack. Same-CPL frames contain only
        // RIP/CS/RFLAGS; forensic readers must derive the interrupted RSP.
        public ulong rsp;
        public ulong ss;
    }

    public struct IDTStackGeneric {
        public RegistersStack rs;
        public ulong errorCode;
        // Native ISR metadata kept between the managed error slot and the
        // CPU return frame. This keeps sizeof(IDTStackGeneric) equal to the
        // complete native frame used by the scheduler copy path.
        public ulong vectorSlot;
        public InterruptReturnStack irs;
    }

    // Throttled IRQ0 debug
    private static uint _irq0DebugCounter;

    // Counter for IRQ0 debug output
    private static uint _irq0Count;
    private static bool _faultBreadcrumbsActive;
    private static int[] _interruptNestingByCpu;
    private static int[] _activeInterruptVectorByCpu;

    private static void SerialWriteLiteral(string text) {
        if (text == null) return;
        for (int i = 0; i < text.Length; i++) {
            Native.Out8(0x3F8, (byte)text[i]);
        }
    }

    private static void SerialWriteLineLiteral(string text) {
        SerialWriteLiteral(text);
        Native.Out8(0x3F8, (byte)'\n');
    }

    private static void SerialWriteHex8(byte value) {
        for (int shift = 4; shift >= 0; shift -= 4) {
            int nibble = (value >> shift) & 0xF;
            byte c = (byte)(nibble < 10 ? ('0' + nibble) : ('A' + (nibble - 10)));
            Native.Out8(0x3F8, c);
        }
    }

    private static void SerialWriteHex64(ulong value) {
        for (int shift = 60; shift >= 0; shift -= 4) {
            int nibble = (int)((value >> shift) & 0xF);
            byte c = (byte)(nibble < 10 ? ('0' + nibble) : ('A' + (nibble - 10)));
            Native.Out8(0x3F8, c);
        }
    }

    private static void SerialWriteHexLine8(string label, byte value) {
        SerialWriteLiteral(label);
        SerialWriteLiteral("0x");
        SerialWriteHex8(value);
        Native.Out8(0x3F8, (byte)'\n');
    }

    private static void SerialWriteHexLine16(string label, ushort value) {
        SerialWriteLiteral(label);
        SerialWriteLiteral("0x");
        SerialWriteHex8((byte)(value >> 8));
        SerialWriteHex8((byte)value);
        Native.Out8(0x3F8, (byte)'\n');
    }

    private static void SerialWriteHexLine64(string label, ulong value) {
        SerialWriteLiteral(label);
        SerialWriteLiteral("0x");
        SerialWriteHex64(value);
        Native.Out8(0x3F8, (byte)'\n');
    }

    private static void SerialWriteExecutionContext(ulong interruptedRsp = 0) {
        SerialWriteHexLine64("ALLOCATOR_CURRENT_OWNER_ID=",
            unchecked((ulong)(long)Allocator.CurrentOwnerId));
        SerialWriteHexLine64("WINDOW_CLEANUP_RUNNING=",
            WindowManager.CleanupDiagnosticRunning ? 1UL : 0UL);
        SerialWriteHexLine64("WINDOW_CLEANUP_PENDING=",
            WindowManager.CleanupDiagnosticPending ? 1UL : 0UL);
        SerialWriteHexLine64("WINDOW_CLEANUP_PASS=",
            (ulong)(uint)WindowManager.CleanupDiagnosticPass);
        SerialWriteHexLine64("WINDOW_CLEANUP_CURRENT_OWNER_ID=",
            unchecked((ulong)(long)WindowManager.CleanupDiagnosticCurrentOwnerId));
        SerialWriteHexLine64("START_FOREGROUND_OPERATION_ID=",
            unchecked((ulong)(uint)StartMenu.ForegroundDiagnosticOperationId));
        SerialWriteHexLine64("START_FOREGROUND_ACTION=",
            unchecked((ulong)(uint)StartMenu.ForegroundDiagnosticAction));
        SerialWriteHexLine64("START_FOREGROUND_PHASE=",
            unchecked((ulong)(uint)StartMenu.ForegroundDiagnosticPhase));
        var liveWindows = WindowManager.Windows;
        SerialWriteHexLine64("WINDOW_COUNT=",
            liveWindows == null ? 0UL : (ulong)(uint)liveWindows.Count);
        if (liveWindows != null && liveWindows.Count > 0) {
            Window topmostWindow = liveWindows[liveWindows.Count - 1];
            if (topmostWindow != null) {
                SerialWriteHexLine64("WINDOW_TOPMOST_OWNER_ID=",
                    unchecked((ulong)(long)topmostWindow.OwnerId));
                guideXOS.OS.ApplicationInstanceHandle topmostOwner =
                    topmostWindow.ApplicationInstanceHandle;
                SerialWriteHexLine64("WINDOW_TOPMOST_APPLICATION_HANDLE=",
                    topmostOwner.Value);
                SerialWriteHexLine64("WINDOW_TOPMOST_APPLICATION_GENERATION=",
                    topmostOwner.Generation);
                SerialWriteHexLine64("WINDOW_TOPMOST_VISIBLE=",
                    topmostWindow.Visible ? 1UL : 0UL);
                SerialWriteHexLine64("WINDOW_TOPMOST_DISPOSED=",
                    topmostWindow.IsDisposed ? 1UL : 0UL);
            }
        }
        SerialWriteHexLine64("TASKBAR_PROJECTION_COUNT=",
            (ulong)(uint)TaskbarApplicationEntryRegistry.DiagnosticEntryCount);
        SerialWriteHexLine64("TASKBAR_STALE_OWNER_COUNT=",
            (ulong)(uint)TaskbarApplicationEntryRegistry.DiagnosticStaleOwnerCount);
        SerialWriteHexLine64("ALLOC_FREE_INVALID=",
            (ulong)(uint)Allocator.FreeFailInvalidPtr);
        SerialWriteHexLine64("ALLOC_FREE_CORRUPT=",
            (ulong)(uint)Allocator.FreeFailCorruptRun);
        SerialWriteHexLine64("ALLOC_FREE_NO_PAGES=",
            (ulong)(uint)Allocator.FreeFailNoPages);
        guideXOS.OS.ApplicationInstanceHandle activeApplication =
            guideXOS.OS.ApplicationInstanceRegistry.ActiveApplicationHandle;
        SerialWriteHexLine64("ACTIVE_APPLICATION_HANDLE=",
            activeApplication.Value);
        SerialWriteHexLine64("ACTIVE_APPLICATION_GENERATION=",
            activeApplication.Generation);
        // Read LocalAPIC-backed CPU identity only after the software state
        // above has been captured; fatal PF diagnostics must retain useful
        // context even if the APIC/MMIO read itself faults.
        SerialWriteHexLine64("CURRENT_CPU=", (ulong)(uint)SMP.ThisCPU);

        Thread thread = ThreadPool.CurrentThread;
        if (thread == null) {
            SerialWriteLineLiteral("SCHEDULER_THREAD_PRESENT=0");
            SerialWriteLineLiteral("CURRENT_PROCESS_PRESENT=0");
            return;
        }

        SerialWriteLineLiteral("SCHEDULER_THREAD_PRESENT=1");
        SerialWriteHexLine64("SCHEDULER_THREAD_CPU=",
            (ulong)(uint)thread.RunOnWhichCPU);
        SerialWriteHexLine64("SCHEDULER_THREAD_IS_USER=",
            thread.IsUserThread ? 1UL : 0UL);
        SerialWriteHexLine64("SCHEDULER_THREAD_TERMINATED=",
            thread.Terminated ? 1UL : 0UL);
        SerialWriteHexLine64("SCHEDULER_THREAD_KERNEL_STACK_BASE=",
            thread.KernelStackBase);
        SerialWriteHexLine64("SCHEDULER_THREAD_KERNEL_STACK_TOP=",
            thread.KernelStackTop);
        if (interruptedRsp == 0) interruptedRsp = Native.ReadRSP();
        SerialWriteHexLine64("INTERRUPTED_RSP_IN_KERNEL_STACK=",
            interruptedRsp >= thread.KernelStackBase &&
                interruptedRsp <= thread.KernelStackTop ? 1UL : 0UL);
        if (interruptedRsp >= thread.KernelStackBase &&
                interruptedRsp <= thread.KernelStackTop) {
            SerialWriteHexLine64("INTERRUPTED_STACK_USED_BYTES=",
                thread.KernelStackTop - interruptedRsp);
        }
        // Thread has no separate numeric ID. Its allocator-backed kernel stack
        // base is stable and unique for the lifetime of this thread.
        SerialWriteHexLine64("SCHEDULER_THREAD_ID=",
            thread.KernelStackBase);

        Ring3Process process = thread.OwnerProcess;
        if (process == null) {
            SerialWriteLineLiteral("CURRENT_PROCESS_PRESENT=0");
            return;
        }

        SerialWriteLineLiteral("CURRENT_PROCESS_PRESENT=1");
        SerialWriteHexLine64("CURRENT_PROCESS_HANDLE=", process.Handle.Value);
        SerialWriteHexLine64("CURRENT_PROCESS_GENERATION=",
            (ulong)process.Handle.Generation);
        SerialWriteHexLine8("CURRENT_PROCESS_STATE=", (byte)process.State);
        SerialWriteHexLine64("CURRENT_PROCESS_APPLICATION_OWNER=",
            process.OwningApplicationInstance);
    }

    private static unsafe void SerialWriteAbiGateInstructionWindow(ulong rip) {
        const ulong BytesBeforeRip = 32UL;
        const int WindowLength = 65;
        ulong start = rip >= BytesBeforeRip ? rip - BytesBeforeRip : 0;
        SerialWriteLineLiteral("ABI_GATE_CODE_WINDOW_BEGIN");
        SerialWriteHexLine64("ABI_GATE_CODE_WINDOW_BASE=", start);
        SerialWriteHexLine64("ABI_GATE_CODE_WINDOW_RIP_OFFSET=", BytesBeforeRip);
        for (int i = 0; i < WindowLength; i++) {
            ulong address = start + (ulong)i;
            if (address < start || !IsMapped(address)) {
                SerialWriteLiteral("??");
                continue;
            }
            SerialWriteHex8(*(byte*)address);
        }
        Native.Out8(0x3F8, (byte)'\n');
        SerialWriteLineLiteral("ABI_GATE_CODE_WINDOW_END");
    }

    private static unsafe void SerialWriteAbiGateStack(ulong rsp, ulong cpl) {
        const int StackQwordCount = 16;
        SerialWriteLineLiteral("ABI_GATE_STACK_BEGIN");
        if (cpl != 0 || rsp < 0x1000UL ||
                rsp > 0xFFFFFFFFFFFFFFFFUL -
                    ((ulong)StackQwordCount * 8UL) ||
                !IsSupervisorMapped(rsp)) {
            SerialWriteLineLiteral("ABI_GATE_STACK_UNAVAILABLE");
            SerialWriteLineLiteral("ABI_GATE_STACK_END");
            return;
        }

        ulong* words = (ulong*)rsp;
        for (int i = 0; i < StackQwordCount; i++) {
            ulong address = rsp + ((ulong)i * 8UL);
            if (address < rsp ||
                    !IsSupervisorMapped(address) ||
                    !IsSupervisorMapped(address + 7UL)) {
                SerialWriteLiteral("ABI_GATE_STACK_QWORD;index=");
                SerialWriteHex64((ulong)i);
                SerialWriteLiteral(";address=0x");
                SerialWriteHex64(address);
                SerialWriteLiteral(";value=UNMAPPED\n");
                continue;
            }
            SerialWriteLiteral("ABI_GATE_STACK_QWORD;index=");
            SerialWriteHex64((ulong)i);
            SerialWriteLiteral(";address=0x");
            SerialWriteHex64(address);
            SerialWriteLiteral(";value=0x");
            SerialWriteHex64(words[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }
        SerialWriteLineLiteral("ABI_GATE_STACK_END");
    }

    private static unsafe void SerialWriteAbiGateCodePage(ulong address) {
        const ulong Present = 1UL;
        const ulong Writable = 1UL << 1;
        const ulong User = 1UL << 2;
        const ulong LargePage = 1UL << 7;
        const ulong NoExecute = 1UL << 63;
        const ulong PhysicalAddressMask = 0x000FFFFFFFFFF000UL;
        ulong cr3 = Native.ReadCR3() & ~0xFFFUL;
        SerialWriteLineLiteral("ABI_GATE_CODE_PAGE_BEGIN");
        SerialWriteHexLine64("ABI_GATE_CODE_PAGE_ADDRESS=", address);
        SerialWriteHexLine64("ABI_GATE_CODE_PAGE_CR3=", cr3);

        ulong* pml4 = (ulong*)cr3;
        ulong pml4e = pml4[(address >> 39) & 0x1FFUL];
        SerialWriteHexLine64("ABI_GATE_CODE_PML4E=", pml4e);
        if ((pml4e & Present) == 0) {
            SerialWriteAbiGateCodePageMissing();
            return;
        }

        ulong* pdpt = (ulong*)(pml4e & PhysicalAddressMask);
        ulong pdpte = pdpt[(address >> 30) & 0x1FFUL];
        SerialWriteHexLine64("ABI_GATE_CODE_PDPTE=", pdpte);
        if ((pdpte & Present) == 0) {
            SerialWriteAbiGateCodePageMissing();
            return;
        }
        ulong effectiveWritable = pml4e & Writable;
        ulong effectiveUser = pml4e & User;
        bool effectiveNx = (pml4e & NoExecute) != 0 ||
            (pdpte & NoExecute) != 0;
        if ((pdpte & LargePage) != 0) {
            ulong physical = (pdpte & 0x000FFFFFC0000000UL) |
                (address & 0x3FFFFFFFUL);
            SerialWriteLineLiteral("ABI_GATE_CODE_PAGE_MAPPED=1");
            SerialWriteHexLine64("ABI_GATE_CODE_PAGE_PHYSICAL=", physical);
            SerialWriteHexLine64("ABI_GATE_CODE_PAGE_WRITABLE=",
                (effectiveWritable & (pdpte & Writable)) != 0 ? 1UL : 0UL);
            SerialWriteHexLine64("ABI_GATE_CODE_PAGE_USER=",
                (effectiveUser & (pdpte & User)) != 0 ? 1UL : 0UL);
            SerialWriteHexLine64("ABI_GATE_CODE_PAGE_EXECUTABLE=",
                effectiveNx ? 0UL : 1UL);
            SerialWriteLineLiteral("ABI_GATE_CODE_PAGE_END");
            return;
        }

        effectiveWritable &= pdpte & Writable;
        effectiveUser &= pdpte & User;
        ulong* pd = (ulong*)(pdpte & PhysicalAddressMask);
        ulong pde = pd[(address >> 21) & 0x1FFUL];
        SerialWriteHexLine64("ABI_GATE_CODE_PDE=", pde);
        if ((pde & Present) == 0) {
            SerialWriteAbiGateCodePageMissing();
            return;
        }
        effectiveWritable &= pde & Writable;
        effectiveUser &= pde & User;
        effectiveNx = effectiveNx || (pde & NoExecute) != 0;
        if ((pde & LargePage) != 0) {
            ulong physical = (pde & 0x000FFFFFFFE00000UL) |
                (address & 0x1FFFFFUL);
            SerialWriteLineLiteral("ABI_GATE_CODE_PAGE_MAPPED=1");
            SerialWriteHexLine64("ABI_GATE_CODE_PAGE_PHYSICAL=", physical);
            SerialWriteHexLine64("ABI_GATE_CODE_PAGE_WRITABLE=",
                effectiveWritable != 0 ? 1UL : 0UL);
            SerialWriteHexLine64("ABI_GATE_CODE_PAGE_USER=",
                effectiveUser != 0 ? 1UL : 0UL);
            SerialWriteHexLine64("ABI_GATE_CODE_PAGE_EXECUTABLE=",
                effectiveNx ? 0UL : 1UL);
            SerialWriteLineLiteral("ABI_GATE_CODE_PAGE_END");
            return;
        }

        ulong* pt = (ulong*)(pde & PhysicalAddressMask);
        ulong pte = pt[(address >> 12) & 0x1FFUL];
        SerialWriteHexLine64("ABI_GATE_CODE_PTE=", pte);
        if ((pte & Present) == 0) {
            SerialWriteAbiGateCodePageMissing();
            return;
        }
        effectiveWritable &= pte & Writable;
        effectiveUser &= pte & User;
        effectiveNx = effectiveNx || (pte & NoExecute) != 0;
        SerialWriteLineLiteral("ABI_GATE_CODE_PAGE_MAPPED=1");
        SerialWriteHexLine64("ABI_GATE_CODE_PAGE_PHYSICAL=",
            (pte & PhysicalAddressMask) | (address & 0xFFFUL));
        SerialWriteHexLine64("ABI_GATE_CODE_PAGE_FLAGS=", pte & 0xFFFUL);
        SerialWriteHexLine64("ABI_GATE_CODE_PAGE_WRITABLE=",
            effectiveWritable != 0 ? 1UL : 0UL);
        SerialWriteHexLine64("ABI_GATE_CODE_PAGE_USER=",
            effectiveUser != 0 ? 1UL : 0UL);
        SerialWriteHexLine64("ABI_GATE_CODE_PAGE_EXECUTABLE=",
            effectiveNx ? 0UL : 1UL);
        SerialWriteLineLiteral("ABI_GATE_CODE_PAGE_END");
    }

    private static void SerialWriteAbiGateCodePageMissing() {
        SerialWriteLineLiteral("ABI_GATE_CODE_PAGE_MAPPED=0");
        SerialWriteLineLiteral("ABI_GATE_CODE_PAGE_END");
    }

    private static unsafe void SerialWriteAbiGateDiagnostics(
            IDTStackGeneric* stack) {
        SerialWriteLineLiteral("ABI_GATE_DIAGNOSTICS_BEGIN");
        if (stack == null) {
            SerialWriteHexLine8("ABI_GATE_REASON_ENUM=", 1);
            SerialWriteLineLiteral("ABI_GATE_REASON=INT80_WITHOUT_SAVED_FRAME");
            SerialWriteHexLine64("CR3=", Native.ReadCR3());
            SerialWriteExecutionContext();
            SerialWriteLineLiteral("ABI_GATE_DIAGNOSTICS_END");
            return;
        }

        ulong cpl = stack->irs.cs & 3UL;
        if (cpl == 0) {
            SerialWriteHexLine8("ABI_GATE_REASON_ENUM=", 2);
            SerialWriteLineLiteral("ABI_GATE_REASON=KERNEL_ORIGINATED_INT80");
        } else {
            SerialWriteHexLine8("ABI_GATE_REASON_ENUM=", 3);
            SerialWriteLineLiteral("ABI_GATE_REASON=INT80_FROM_UNEXPECTED_CPL");
        }
        SerialWriteHexLine64("ABI_GATE_IRQ=", 0x80UL);
        SerialWriteHexLine64("ABI_GATE_RIP=", stack->irs.rip);
        SerialWriteHexLine64("ABI_GATE_CS=", stack->irs.cs);
        SerialWriteHexLine64("ABI_GATE_RFLAGS=", stack->irs.rflags);
        SerialWriteHexLine64("ABI_GATE_RSP=",
            GetInterruptedRsp(0x80, &stack->irs));
        SerialWriteHexLine64("ABI_GATE_CPU_FRAME_RAW_RSP_SLOT=",
            stack->irs.rsp);
        SerialWriteHexLine64("ABI_GATE_CPU_FRAME_RAW_SS_SLOT=",
            stack->irs.ss);
        SerialWriteHexLine64("CR3=", Native.ReadCR3());
        SerialWriteHexLine64("ABI_GATE_RAX_OPERATION_CANDIDATE=",
            stack->rs.rax);
        SerialWriteHexLine64("ABI_GATE_RDI_REQUEST_POINTER_CANDIDATE=",
            stack->rs.rdi);
        SerialWriteHexLine64("ABI_GATE_RSI_REQUEST_LENGTH_CANDIDATE=",
            stack->rs.rsi);
        SerialWriteHexLine64("ABI_GATE_RDX_ARGUMENT_CANDIDATE=",
            stack->rs.rdx);
        SerialWriteHexLine64("ABI_GATE_SYSCALL_OPERATION_ID_CANDIDATE=",
            stack->rs.rax);
        SerialWriteHexLine64("ABI_GATE_RBP=", stack->rs.rbp);
        ulong rbp = stack->rs.rbp;
        if ((rbp & 7UL) == 0 && rbp >= 0x1000UL &&
                rbp <= 0x00007FFFFFFFFFF0UL &&
                IsSupervisorMapped(rbp + 8UL)) {
            SerialWriteHexLine64("ABI_GATE_DIRECT_RETURN_ADDRESS_CANDIDATE=",
                *((ulong*)(rbp + 8UL)));
        } else {
            SerialWriteLineLiteral("ABI_GATE_DIRECT_RETURN_ADDRESS_CANDIDATE=UNAVAILABLE");
        }
        if (cpl == 0) {
            SerialWriteAbiGateInstructionWindow(stack->irs.rip);
            SerialWriteAbiGateCodePage(stack->irs.rip >= 2UL ?
                stack->irs.rip - 2UL : stack->irs.rip);
        }
        SerialWriteFaultFrameChain(stack->rs.rbp, cpl == 0);
        SerialWriteAbiGateStack(GetInterruptedRsp(0x80, &stack->irs), cpl);
        SerialWriteExecutionContext();
        SerialWriteLineLiteral("ABI_GATE_DIAGNOSTICS_END");
    }

    private static unsafe void SerialWriteFaultInstructionBytes(ulong rip) {
        SerialWriteLiteral("FAULT_BYTES=");
        for (int i = 0; i < 16; i++) {
            ulong address = rip + (ulong)i;
            if (!IsMapped(address)) {
                SerialWriteLiteral("??");
                continue;
            }
            SerialWriteHex8(*(byte*)address);
        }
        Native.Out8(0x3F8, (byte)'\n');
    }

    private static bool IsCanonicalAddress(ulong address) {
        ulong upper = address >> 48;
        ulong expectedUpper = ((address >> 47) & 1UL) == 0
            ? 0UL : 0xFFFFUL;
        return address != 0 && upper == expectedUpper;
    }

    private static unsafe void SerialWriteFaultCodeWindow(ulong rip) {
        const ulong BytesBeforeRip = 16UL;
        const int WindowLength = 32;
        SerialWriteLineLiteral("FAULT_CODE_WINDOW_BEGIN");
        SerialWriteHexLine64("FAULT_CODE_WINDOW_RIP=", rip);
        if (!IsCanonicalAddress(rip) || rip < BytesBeforeRip) {
            SerialWriteLineLiteral("FAULT_CODE_WINDOW_UNAVAILABLE=noncanonical-or-underflow");
            SerialWriteLineLiteral("FAULT_CODE_WINDOW_END");
            return;
        }

        ulong start = rip - BytesBeforeRip;
        SerialWriteHexLine64("FAULT_CODE_WINDOW_BASE=", start);
        SerialWriteHexLine64("FAULT_CODE_WINDOW_RIP_OFFSET=", BytesBeforeRip);
        SerialWriteLiteral("FAULT_CODE_WINDOW_BYTES=");
        for (int i = 0; i < WindowLength; i++) {
            ulong address = start + (ulong)i;
            if (address < start || !IsMapped(address)) {
                SerialWriteLiteral("??");
                continue;
            }
            SerialWriteHex8(*(byte*)address);
        }
        Native.Out8(0x3F8, (byte)'\n');
        SerialWriteLineLiteral("FAULT_CODE_WINDOW_END");
    }

    private static unsafe void SerialWriteIdtGate(ulong baseAddress,
            ushort limit, byte vector) {
        ulong offset = (ulong)vector * (ulong)sizeof(IDTEntry);
        if (offset + (ulong)sizeof(IDTEntry) - 1UL > limit ||
                baseAddress > 0xFFFFFFFFFFFFFFFFUL - offset ||
                !IsMapped(baseAddress + offset) ||
                !IsMapped(baseAddress + offset + (ulong)sizeof(IDTEntry) - 1UL)) {
            SerialWriteHexLine64("IDT_GATE_UNAVAILABLE_VECTOR=", vector);
            return;
        }

        IDTEntry* entry = (IDTEntry*)(baseAddress + offset);
        ulong target = entry->BaseLow |
            ((ulong)entry->BaseMid << 16) |
            ((ulong)entry->BaseHigh << 32);
        SerialWriteHexLine8("IDT_GATE_VECTOR=", vector);
        SerialWriteHexLine16("IDT_GATE_SELECTOR=", entry->Selector);
        SerialWriteHexLine8("IDT_GATE_PRESENT=",
            (byte)((entry->Type_Attributes >> 7) & 1));
        SerialWriteHexLine8("IDT_GATE_DPL=",
            (byte)((entry->Type_Attributes >> 5) & 3));
        SerialWriteHexLine8("IDT_GATE_TYPE=",
            (byte)(entry->Type_Attributes & 0xF));
        SerialWriteHexLine8("IDT_GATE_IST=", (byte)(entry->Reserved0 & 7));
        SerialWriteHexLine64("IDT_GATE_TARGET=", target);
    }

    private static unsafe void SerialWriteDescriptorForensics() {
        IDTDescriptor activeIdtr = default(IDTDescriptor);
        GDT.GDTDescriptor activeGdtr = default(GDT.GDTDescriptor);
        Native.Read_IDT(ref activeIdtr);
        Native.Read_GDT(ref activeGdtr);

        SerialWriteLineLiteral("DESCRIPTOR_FORENSICS_BEGIN");
        SerialWriteHexLine64("IDTR_ACTIVE_BASE=", activeIdtr.Base);
        SerialWriteHexLine16("IDTR_ACTIVE_LIMIT=", activeIdtr.Limit);
        SerialWriteHexLine64("IDTR_CACHED_BASE=", idtr.Base);
        SerialWriteHexLine16("IDTR_CACHED_LIMIT=", idtr.Limit);
        SerialWriteIdtGate(activeIdtr.Base, activeIdtr.Limit, 8);
        SerialWriteIdtGate(activeIdtr.Base, activeIdtr.Limit, 13);
        SerialWriteIdtGate(activeIdtr.Base, activeIdtr.Limit, 14);

        SerialWriteHexLine64("GDTR_ACTIVE_BASE=", activeGdtr.Base);
        SerialWriteHexLine16("GDTR_ACTIVE_LIMIT=", activeGdtr.Limit);
        SerialWriteHexLine64("GDTR_CACHED_BASE=", GDT.gdtr.Base);
        SerialWriteHexLine16("GDTR_CACHED_LIMIT=", GDT.gdtr.Limit);
        SerialWriteHexLine16("TASK_REGISTER_SELECTOR=", Native.Read_TR());
        if (activeGdtr.Limit >= 55 &&
                IsMapped(activeGdtr.Base) &&
                IsMapped(activeGdtr.Base + 55UL)) {
            ulong* entries = (ulong*)activeGdtr.Base;
            SerialWriteHexLine64("GDT_KERNEL_CODE=", entries[1]);
            SerialWriteHexLine64("GDT_KERNEL_DATA=", entries[2]);
            SerialWriteHexLine64("GDT_USER_CODE=", entries[3]);
            SerialWriteHexLine64("GDT_USER_DATA=", entries[4]);
            ulong tssLow = entries[5];
            ulong tssHigh = entries[6];
            SerialWriteHexLine64("GDT_TSS_LOW=", tssLow);
            SerialWriteHexLine64("GDT_TSS_HIGH=", tssHigh);
            ulong tssBase = ((tssLow >> 16) & 0xFFFFUL) |
                (((tssLow >> 32) & 0xFFUL) << 16) |
                (((tssLow >> 56) & 0xFFUL) << 24) |
                ((tssHigh & 0xFFFFFFFFUL) << 32);
            SerialWriteHexLine64("TSS_BASE=", tssBase);
            SerialWriteHexLine8("TSS_PRESENT=", (byte)((tssLow >> 47) & 1));
            SerialWriteHexLine8("TSS_TYPE=", (byte)((tssLow >> 40) & 0xF));
            if (IsCanonicalAddress(tssBase) && IsMapped(tssBase + 103UL)) {
                byte* tss = (byte*)tssBase;
                ulong rsp0 = *(uint*)(tss + 4) |
                    ((ulong)*(uint*)(tss + 8) << 32);
                SerialWriteHexLine64("TSS_RSP0=", rsp0);
                SerialWriteHexLine64("GDT_KERNEL_STACK_TOP=", GDT.KernelStackTop);
                for (int ist = 0; ist < 7; ist++) {
                    int offset = 36 + ist * 8;
                    ulong value = *(uint*)(tss + offset) |
                        ((ulong)*(uint*)(tss + offset + 4) << 32);
                    SerialWriteLiteral("TSS_IST");
                    SerialWriteHex8((byte)(ist + 1));
                    SerialWriteLiteral("=0x");
                    SerialWriteHex64(value);
                    Native.Out8(0x3F8, (byte)'\n');
                }
            } else {
                SerialWriteLineLiteral("TSS_MEMORY_UNAVAILABLE=1");
            }
        } else {
            SerialWriteLineLiteral("GDT_MEMORY_UNAVAILABLE=1");
        }
        SerialWriteLineLiteral("DESCRIPTOR_FORENSICS_END");
    }

    private static unsafe void SerialWriteExceptionSnapshot(ulong sequence,
            int irq, ulong errorCode, bool hasErrorCode,
            RegistersStack* regs, InterruptReturnStack* irs,
            int priorNestingDepth, int priorVector, uint cpuId) {
        SerialWriteLineLiteral("EXCEPTION_RECORD_BEGIN");
        SerialWriteHexLine64("EXCEPTION_SEQUENCE=", sequence);
        SerialWriteHexLine8("EXCEPTION_VECTOR=", (byte)irq);
        SerialWriteHexLine64("EXCEPTION_ERROR_CODE=", errorCode);
        SerialWriteHexLine64("EXCEPTION_ERROR_CODE_PRESENT=", hasErrorCode ? 1UL : 0UL);
        SerialWriteHexLine64("EXCEPTION_FRAME_SYNTHETIC_ERROR_SLOT=",
            regs == null ? 0UL : ((ulong*)regs)[15]);
        SerialWriteHexLine64("EXCEPTION_FRAME_VECTOR_SLOT=",
            regs == null ? 0UL : ((ulong*)regs)[16]);
        SerialWriteHexLine64("EXCEPTION_CPU_ERROR_OFFSET=",
            hasErrorCode ? 136UL : 0UL);
        SerialWriteHexLine64("EXCEPTION_CPU_FRAME_OFFSET=",
            hasErrorCode ? 144UL : 136UL);
        SerialWriteHexLine64("INTERRUPT_PARENT_DEPTH=",
            (ulong)(uint)priorNestingDepth);
        SerialWriteHexLine64("INTERRUPT_PARENT_VECTOR=",
            unchecked((ulong)(long)priorVector));
        SerialWriteHexLine64("CURRENT_CPU=", cpuId);
        SerialWriteHexLine64("CR2=", Native.ReadCR2());
        SerialWriteHexLine64("CR3=", Native.ReadCR3());

        if (irs != null) {
            bool pushedStack = CpuPushedRspSs(irq, irs);
            ulong rsp = GetInterruptedRsp(irq, irs);
            SerialWriteHexLine64("RIP=", irs->rip);
            SerialWriteHexLine64("CS=", irs->cs);
            SerialWriteHexLine64("RFLAGS=", irs->rflags);
            SerialWriteHexLine64("RSP=", rsp);
            SerialWriteHexLine64("SS_RSP_PUSHED=", pushedStack ? 1UL : 0UL);
            if (pushedStack)
                SerialWriteHexLine64("SS=", irs->ss);
            else
                SerialWriteLineLiteral("SS=not-pushed");
            SerialWriteHexLine64("FRAME_CPU_RIP_ADDRESS=", (ulong)irs);
        }

        if (regs != null) {
            SerialWriteHexLine64("REG_RAX=", regs->rax);
            SerialWriteHexLine64("REG_RCX=", regs->rcx);
            SerialWriteHexLine64("REG_RDX=", regs->rdx);
            SerialWriteHexLine64("REG_RBX=", regs->rbx);
            SerialWriteHexLine64("REG_RBP=", regs->rbp);
            SerialWriteHexLine64("REG_RSI=", regs->rsi);
            SerialWriteHexLine64("REG_RDI=", regs->rdi);
            SerialWriteHexLine64("REG_R8=", regs->r8);
            SerialWriteHexLine64("REG_R9=", regs->r9);
            SerialWriteHexLine64("REG_R10=", regs->r10);
            SerialWriteHexLine64("REG_R11=", regs->r11);
            SerialWriteHexLine64("REG_R12=", regs->r12);
            SerialWriteHexLine64("REG_R13=", regs->r13);
            SerialWriteHexLine64("REG_R14=", regs->r14);
            SerialWriteHexLine64("REG_R15=", regs->r15);
        }

        if (irq == 13) {
            SerialWriteHexLine8("GP_ERROR_EXTERNAL=", (byte)(errorCode & 1UL));
            SerialWriteHexLine8("GP_ERROR_IDT=", (byte)((errorCode >> 1) & 1UL));
            SerialWriteHexLine8("GP_ERROR_TI=", (byte)((errorCode >> 2) & 1UL));
            SerialWriteHexLine64("GP_ERROR_SELECTOR_INDEX=", errorCode >> 3);
            SerialWriteHexLine64("GP_ERROR_SELECTOR_VALUE=", errorCode & ~7UL);
            SerialWriteLineLiteral((errorCode & 2UL) != 0
                ? "GP_ERROR_SELECTOR_TABLE=IDT"
                : ((errorCode & 4UL) != 0
                    ? "GP_ERROR_SELECTOR_TABLE=LDT"
                    : "GP_ERROR_SELECTOR_TABLE=GDT_OR_NONSELECTOR"));
        } else if (irq == 14) {
            SerialWriteHexLine8("PF_PRESENT=", (byte)(errorCode & 1UL));
            SerialWriteHexLine8("PF_WRITE=", (byte)((errorCode >> 1) & 1UL));
            SerialWriteHexLine8("PF_USER=", (byte)((errorCode >> 2) & 1UL));
            SerialWriteHexLine8("PF_RESERVED=", (byte)((errorCode >> 3) & 1UL));
            SerialWriteHexLine8("PF_INSTRUCTION_FETCH=", (byte)((errorCode >> 4) & 1UL));
            SerialWriteHexLine8("PF_PROTECTION_KEY=", (byte)((errorCode >> 5) & 1UL));
            SerialWriteHexLine8("PF_SHADOW_STACK=", (byte)((errorCode >> 6) & 1UL));
            SerialWriteHexLine8("PF_SGX=", (byte)((errorCode >> 15) & 1UL));
        }
        SerialWriteLineLiteral("EXCEPTION_RECORD_END");
    }

    private static unsafe void SerialWritePageTableWalk(ulong virtualAddress) {
        const ulong Present = 1;
        const ulong LargePage = 1UL << 7;
        ulong cr3 = Native.ReadCR3() & ~0xFFFUL;
        SerialWriteHexLine64("CR3=", cr3);
        if (!IsCanonicalAddress(virtualAddress)) {
            SerialWriteHexLine64("PT_WALK_SKIPPED_NONCANONICAL=", virtualAddress);
            return;
        }

        ulong* pml4 = (ulong*)cr3;
        ulong pml4e = pml4[(virtualAddress >> 39) & 0x1FFUL];
        SerialWriteHexLine64("PT_PML4E=", pml4e);
        if ((pml4e & Present) == 0) return;

        ulong* pdpt = (ulong*)(pml4e & ~0xFFFUL);
        ulong pdpte = pdpt[(virtualAddress >> 30) & 0x1FFUL];
        SerialWriteHexLine64("PT_PDPTE=", pdpte);
        if ((pdpte & Present) == 0 || (pdpte & LargePage) != 0) return;

        ulong* pd = (ulong*)(pdpte & ~0xFFFUL);
        ulong pde = pd[(virtualAddress >> 21) & 0x1FFUL];
        SerialWriteHexLine64("PT_PDE=", pde);
        if ((pde & Present) == 0 || (pde & LargePage) != 0) return;

        ulong* pt = (ulong*)(pde & ~0xFFFUL);
        ulong pte = pt[(virtualAddress >> 12) & 0x1FFUL];
        SerialWriteHexLine64("PT_PTE=", pte);
        if (virtualAddress == 0x00000000000A0000UL) {
            SerialWriteHexLine64("TARGET_PHYS=", pte & ~0xFFFUL);
            SerialWriteHexLine64("TARGET_FLAGS=", pte & 0xFFFUL);
            SerialWriteLineLiteral((pte & Present) != 0 ?
                "TARGET_MAPPED=1" : "TARGET_MAPPED=0");
            SerialWriteLineLiteral((pte & Present) != 0 && (pte & (1UL << 63)) == 0 ?
                "TARGET_EXECUTABLE=1" : "TARGET_EXECUTABLE=0");
            SerialWriteLineLiteral("TARGET_CLASS=LOW_MEMORY_VGA_APERTURE");
        }
    }

    private static unsafe bool IsMapped(ulong virtualAddress) {
        ulong upper = virtualAddress >> 48;
        ulong expectedUpper = ((virtualAddress >> 47) & 1UL) == 0
            ? 0UL : 0xFFFFUL;
        if (virtualAddress == 0 || upper != expectedUpper) return false;
        ulong cr3 = Native.ReadCR3() & ~0xFFFUL;
        ulong* pml4 = (ulong*)cr3;
        ulong pml4e = pml4[(virtualAddress >> 39) & 0x1FFUL];
        if ((pml4e & 1) == 0) return false;
        ulong* pdpt = (ulong*)(pml4e & ~0xFFFUL);
        ulong pdpte = pdpt[(virtualAddress >> 30) & 0x1FFUL];
        if ((pdpte & 1) == 0) return false;
        if ((pdpte & (1UL << 7)) != 0) return true;
        ulong* pd = (ulong*)(pdpte & ~0xFFFUL);
        ulong pde = pd[(virtualAddress >> 21) & 0x1FFUL];
        if ((pde & 1) == 0) return false;
        if ((pde & (1UL << 7)) != 0) return true;
        ulong* pt = (ulong*)(pde & ~0xFFFUL);
        return (pt[(virtualAddress >> 12) & 0x1FFUL] & 1) != 0;
    }

    private static unsafe bool IsSupervisorMapped(ulong virtualAddress) {
        ulong upper = virtualAddress >> 48;
        ulong expectedUpper = ((virtualAddress >> 47) & 1UL) == 0
            ? 0UL : 0xFFFFUL;
        if (virtualAddress == 0 || upper != expectedUpper) return false;
        ulong cr3 = Native.ReadCR3() & ~0xFFFUL;
        const ulong Present = 1UL;
        const ulong User = 1UL << 2;
        const ulong LargePage = 1UL << 7;
        ulong* pml4 = (ulong*)cr3;
        ulong pml4e = pml4[(virtualAddress >> 39) & 0x1FFUL];
        if ((pml4e & (Present | User)) != Present) return false;
        ulong* pdpt = (ulong*)(pml4e & ~0xFFFUL);
        ulong pdpte = pdpt[(virtualAddress >> 30) & 0x1FFUL];
        if ((pdpte & (Present | User)) != Present) return false;
        if ((pdpte & LargePage) != 0) return true;
        ulong* pd = (ulong*)(pdpte & ~0xFFFUL);
        ulong pde = pd[(virtualAddress >> 21) & 0x1FFUL];
        if ((pde & (Present | User)) != Present) return false;
        if ((pde & LargePage) != 0) return true;
        ulong* pt = (ulong*)(pde & ~0xFFFUL);
        ulong pte = pt[(virtualAddress >> 12) & 0x1FFUL];
        return (pte & (Present | User)) == Present;
    }

    private static unsafe void SerialWriteStackNeighborhood(ulong rsp) {
        SerialWriteLineLiteral("STACK_WINDOW_BEGIN");
        if (rsp < 0x1000 || rsp > 0x00007FFFFFFFF000UL ||
            !IsMapped(rsp - 64) || !IsMapped(rsp + 64)) {
            SerialWriteLineLiteral("STACK_WINDOW_UNAVAILABLE");
            return;
        }

        ulong* p = (ulong*)rsp;
        SerialWriteHexLine64("STACK[-8]=", p[-8]);
        SerialWriteHexLine64("STACK[-7]=", p[-7]);
        SerialWriteHexLine64("STACK[-6]=", p[-6]);
        SerialWriteHexLine64("STACK[-5]=", p[-5]);
        SerialWriteHexLine64("STACK[-4]=", p[-4]);
        SerialWriteHexLine64("STACK[-3]=", p[-3]);
        SerialWriteHexLine64("STACK[-2]=", p[-2]);
        SerialWriteHexLine64("STACK[-1]=", p[-1]);
        SerialWriteHexLine64("STACK[+0]=", p[0]);
        SerialWriteHexLine64("STACK[+1]=", p[1]);
        SerialWriteHexLine64("STACK[+2]=", p[2]);
        SerialWriteHexLine64("STACK[+3]=", p[3]);
        SerialWriteHexLine64("STACK[+4]=", p[4]);
        SerialWriteHexLine64("STACK[+5]=", p[5]);
        SerialWriteHexLine64("STACK[+6]=", p[6]);
        SerialWriteHexLine64("STACK[+7]=", p[7]);
        SerialWriteHexLine64("STACK[+8]=", p[8]);
        SerialWriteLineLiteral("STACK_WINDOW_END");
    }

    private static unsafe void SerialWriteFaultFrameChain(ulong rbp,
            bool supervisorOnly = false) {
        SerialWriteLineLiteral("FAULT_FRAME_CHAIN_BEGIN");
        for (int i = 0; i < 8; i++) {
            if ((rbp & 7UL) != 0 || rbp < 0x1000UL ||
                    rbp > 0x00007FFFFFFFFFF0UL ||
                    !(supervisorOnly ? IsSupervisorMapped(rbp) : IsMapped(rbp)) ||
                    !(supervisorOnly ? IsSupervisorMapped(rbp + 8UL) :
                        IsMapped(rbp + 8UL))) {
                SerialWriteLiteral("FAULT_FRAME_CHAIN_STOP;frame=");
                SerialWriteHex64((ulong)i);
                SerialWriteLiteral(";reason=unmapped-or-unaligned\n");
                break;
            }

            ulong* frame = (ulong*)rbp;
            ulong previous = frame[0];
            ulong returnAddress = frame[1];
            SerialWriteLiteral("FAULT_FRAME;index=");
            SerialWriteHex64((ulong)i);
            SerialWriteLiteral(";rbp=0x");
            SerialWriteHex64(rbp);
            SerialWriteLiteral(";previous=0x");
            SerialWriteHex64(previous);
            SerialWriteLiteral(";return=0x");
            SerialWriteHex64(returnAddress);
            Native.Out8(0x3F8, (byte)'\n');

            if (previous <= rbp || previous - rbp > 0x10000UL) {
                SerialWriteLiteral("FAULT_FRAME_CHAIN_STOP;reason=nonmonotonic\n");
                break;
            }
            rbp = previous;
        }
        SerialWriteLineLiteral("FAULT_FRAME_CHAIN_END");
    }

    private static unsafe bool CpuPushedRspSs(int irq,
            InterruptReturnStack* irs) {
        if (irs == null) return false;
        if ((irs->cs & 3UL) != 0) return true;
        if (irq < 0 || irq >= 256 || idt == null || irq >= idt.Length)
            return false;
        fixed (IDTEntry* entries = idt)
            return (entries[irq].Reserved0 & 7) != 0;
    }

    private static unsafe ulong GetInterruptedRsp(int irq,
            InterruptReturnStack* irs) {
        if (irs == null) return 0;
        if (CpuPushedRspSs(irq, irs)) return irs->rsp;
        // A same-CPL frame starts at RIP and contains exactly three qwords.
        // The interrupted RSP is the address immediately above RFLAGS.
        return (ulong)irs + 3UL * sizeof(ulong);
    }

    private static unsafe void SerialWriteFaultBreadcrumbs(int irq, ulong errorCode, RegistersStack* regs, InterruptReturnStack* irs) {
        if (_faultBreadcrumbsActive) {
            SerialWriteLineLiteral("FAULT_DIAGNOSTIC_REENTRY");
            SerialWriteHexLine8("NESTED_VEC=", (byte)irq);
            SerialWriteHexLine64("NESTED_ERR=", errorCode);
            if (irq == 14)
                SerialWriteHexLine64("NESTED_CR2=", Native.ReadCR2());
            if (irs != null) {
                SerialWriteHexLine64("NESTED_RIP=", irs->rip);
                SerialWriteHexLine64("NESTED_CS=", irs->cs);
                SerialWriteHexLine64("NESTED_RSP=", GetInterruptedRsp(irq, irs));
            }
            if (regs != null) {
                SerialWriteHexLine64("NESTED_RBP=", regs->rbp);
                SerialWriteHexLine64("NESTED_RAX=", regs->rax);
            }
            for (;;) Native.Hlt();
        }
        _faultBreadcrumbsActive = true;

        switch (irq) {
            case 6:
                SerialWriteLineLiteral("CPU_FAULT_INVALID_OPCODE");
                break;
            case 14:
                SerialWriteLineLiteral("CPU_FAULT_PAGE_FAULT");
                break;
            case 13:
                SerialWriteLineLiteral("CPU_FAULT_GENERAL_PROTECTION");
                break;
            case 8:
                SerialWriteLineLiteral("CPU_FAULT_DOUBLE_FAULT");
                break;
        }

        SerialWriteHexLine8("VEC=", (byte)irq);
        SerialWriteHexLine64("ERR=", errorCode);

        // Emit code bytes before managed context and descriptor walks. If a
        // later diagnostic helper faults, the original instruction is already
        // durable in the serial log.
        if (irs != null && (irq == 6 || irq == 13 || irq == 14))
            SerialWriteFaultCodeWindow(irs->rip);
        if (irs != null)
            SerialWriteStackNeighborhood(GetInterruptedRsp(irq, irs));
        if (regs != null && irs != null)
            SerialWriteFaultFrameChain(regs->rbp, (irs->cs & 3UL) == 0);
        if (irq == 8 || irq == 13 || irq == 14)
            SerialWriteDescriptorForensics();
        SerialWriteExecutionContext(irs == null ? 0 : GetInterruptedRsp(irq, irs));

        if (irq == 14) {
            SerialWriteHexLine64("CR2=", Native.ReadCR2());
            SerialWriteHexLine8("PF_PRESENT=", (byte)(errorCode & 1UL));
            SerialWriteHexLine8("PF_WRITE=", (byte)((errorCode >> 1) & 1UL));
            SerialWriteHexLine8("PF_USER=", (byte)((errorCode >> 2) & 1UL));
            SerialWriteHexLine8("PF_RESERVED=", (byte)((errorCode >> 3) & 1UL));
            SerialWriteHexLine8("PF_INSTRUCTION_FETCH=",
                (byte)((errorCode >> 4) & 1UL));
            SerialWriteLineLiteral("PAGE_FAULT_RAW_CONTEXT_BEGIN");
            SerialWriteHexLine64("CR3=", Native.ReadCR3());
            if (irs != null) {
                SerialWriteHexLine64("RIP=", irs->rip);
                SerialWriteHexLine64("CS=", irs->cs);
                SerialWriteHexLine64("RFLAGS=", irs->rflags);
                SerialWriteHexLine64("RSP=", GetInterruptedRsp(irq, irs));
            }
            if (regs != null) {
                SerialWriteHexLine64("REG_RAX=", regs->rax);
                SerialWriteHexLine64("REG_RCX=", regs->rcx);
                SerialWriteHexLine64("REG_RDX=", regs->rdx);
                SerialWriteHexLine64("REG_RBX=", regs->rbx);
                SerialWriteHexLine64("REG_RBP=", regs->rbp);
                SerialWriteHexLine64("REG_RSI=", regs->rsi);
                SerialWriteHexLine64("REG_RDI=", regs->rdi);
                SerialWriteHexLine64("REG_R8=", regs->r8);
                SerialWriteHexLine64("REG_R9=", regs->r9);
                SerialWriteHexLine64("REG_R10=", regs->r10);
                SerialWriteHexLine64("REG_R11=", regs->r11);
                SerialWriteHexLine64("REG_R12=", regs->r12);
                SerialWriteHexLine64("REG_R13=", regs->r13);
                SerialWriteHexLine64("REG_R14=", regs->r14);
                SerialWriteHexLine64("REG_R15=", regs->r15);
            }
            SerialWriteLineLiteral("PAGE_FAULT_RAW_CONTEXT_END");
            if (irs != null) {
                SerialWriteFaultInstructionBytes(irs->rip);
                SerialWriteStackNeighborhood(GetInterruptedRsp(irq, irs));
            }
            if (regs != null)
                SerialWriteFaultFrameChain(regs->rbp, true);
            SerialWritePageTableWalk(Native.ReadCR2());
        }

        if (irs != null) {
            SerialWriteHexLine64("RIP=", irs->rip);
            SerialWriteHexLine64("CS=", irs->cs);
            SerialWriteHexLine64("RFLAGS=", irs->rflags);
            SerialWriteHexLine64("RSP=", GetInterruptedRsp(irq, irs));
            SerialWriteHexLine64("CPU_FRAME_RAW_RSP_SLOT=", irs->rsp);
            SerialWriteHexLine64("CPU_FRAME_RAW_SS_SLOT=", irs->ss);
            SerialWriteStackNeighborhood(GetInterruptedRsp(irq, irs));
            // Keep the vector 6 evidence path and also snapshot the interrupted
            // instruction for kernel page faults. PF CR2/RIP pairs in cleanup
            // diagnostics have been inconsistent with the preserved image's
            // instruction boundaries, so the bytes are needed to distinguish
            // a data fault at RIP from a bad frame or image mismatch.
            if (irq == 6 || irq == 14)
                SerialWriteFaultInstructionBytes(irs->rip);
        }

        if (regs != null) {
            SerialWriteHexLine64("REG_RAX=", regs->rax);
            SerialWriteHexLine64("REG_RCX=", regs->rcx);
            SerialWriteHexLine64("REG_RDX=", regs->rdx);
            SerialWriteHexLine64("REG_RBX=", regs->rbx);
            SerialWriteHexLine64("RBP=", regs->rbp);
            SerialWriteFaultFrameChain(regs->rbp);
            SerialWriteHexLine64("REG_RSI=", regs->rsi);
            SerialWriteHexLine64("REG_RDI=", regs->rdi);
            SerialWriteHexLine64("REG_R8=", regs->r8);
            SerialWriteHexLine64("REG_R9=", regs->r9);
            SerialWriteHexLine64("REG_R10=", regs->r10);
            SerialWriteHexLine64("REG_R11=", regs->r11);
            SerialWriteHexLine64("REG_R12=", regs->r12);
            SerialWriteHexLine64("REG_R13=", regs->r13);
            SerialWriteHexLine64("REG_R14=", regs->r14);
            SerialWriteHexLine64("REG_R15=", regs->r15);
        }

        if (irq != 14)
            SerialWritePageTableWalk(irs != null ? irs->rip : 0);
    }

    [RuntimeExport("intr_handler")]
    public static unsafe void intr_handler(int irq, IDTStackGeneric* stack) {
        uint cpuId = SMP.ThisCPU;
        int cpuSlot = (int)(cpuId & 0xFFU);
        int priorNestingDepth = _interruptNestingByCpu == null
            ? 0 : _interruptNestingByCpu[cpuSlot];
        int priorVector = _activeInterruptVectorByCpu == null
            ? -1 : _activeInterruptVectorByCpu[cpuSlot];
        if (_interruptNestingByCpu != null) {
            _interruptNestingByCpu[cpuSlot] = priorNestingDepth + 1;
            _activeInterruptVectorByCpu[cpuSlot] = irq;
        }

        // Prevent nested interrupts while inside managed interrupt handler.
        Native.Cli();

        if (irq == 0x80) {
            // The diagnostic ABI is intentionally usable only from CPL3.  A
            // kernel-originated software interrupt is not a caller identity.
            if (stack != null && (stack->irs.cs & 3UL) == 3UL) {
                Ring3Abi.Dispatch(stack);
                Ring3Process currentProcess;
                if (Ring3Process.TryGetCurrent(out currentProcess) &&
                    currentProcess.UserThread != null &&
                    currentProcess.UserThread.Terminated) {
                    if (ThreadPool.IsDirectUser) {
                        currentProcess.PrepareDirectReturn(stack);
                    } else if (ThreadPool.SchedulingEnabled) {
                        // Exit is terminal: never iret back to a user thread
                        // whose process has already published Exiting state.
                        ThreadPool.Schedule(stack);
                    }
                }
                RestoreInterruptContext(cpuSlot, priorNestingDepth, priorVector);
                return;
            }
            SerialWriteLineLiteral("RING3_ABI_KERNEL_GATE");
            SerialWriteAbiGateDiagnostics(stack);
            Panic.Error("Ring3 ABI gate invariant: int 0x80 must originate at CPL3");
            for (;;) Native.Hlt();
        }

        if (irq < 0x20) {
            // Compute correct location of InterruptReturnStack depending on whether the CPU pushed an error code
            InterruptReturnStack* irs;
            bool hasErrorCode = false;
            ulong actualErrorCode = 0;
            switch (irq) {
                case 8:
                case 10:
                case 11:
                case 12:
                case 13:
                case 14:
                case 17:
                case 21:
                case 29:
                case 30:
                    // isr_common always pushes a dummy errorCode slot.  For
                    // CPU error-code exceptions, the real error code follows
                    // that slot, then the CPU's return frame.
                    // native_stubs.asm adds a native-only vector slot between
                    // the dummy error slot and the CPU frame.
                    actualErrorCode = *((ulong*)(((byte*)stack) + sizeof(RegistersStack) + sizeof(ulong) + sizeof(ulong)));
                    irs = (InterruptReturnStack*)(((byte*)stack) + sizeof(RegistersStack) + sizeof(ulong) + sizeof(ulong) + sizeof(ulong));
                    hasErrorCode = true;
                    break;
                default:
                    // isr_common always leaves a dummy errorCode slot before
                    // the native-only vector slot and CPU return frame, even
                    // for no-error exceptions.
                    irs = (InterruptReturnStack*)(((byte*)stack) + sizeof(RegistersStack) + sizeof(ulong) + sizeof(ulong));
                    hasErrorCode = false;
                    break;
            }

            ulong exceptionSequence = Native.IncrementExceptionSequence();
            SerialWriteExceptionSnapshot(exceptionSequence, irq,
                actualErrorCode, hasErrorCode, &stack->rs, irs,
                priorNestingDepth, priorVector, cpuId);
            if (exceptionSequence > 1) {
                SerialWriteHexLine64("EXCEPTION_CONTEXT_INHERITED_FROM_SEQUENCE=", 1);
            }

            if (irs != null && (irs->cs & 3UL) == 3UL) {
                if (Ring3Process.HandleUserFault(irq, actualErrorCode, irs->rip,
                                                 irs->rsp,
                                                 irq == 14 ? Native.ReadCR2() : 0)) {
                    Ring3Process currentProcess;
                    if (Ring3Process.TryGetCurrent(out currentProcess)) {
                        if (ThreadPool.IsDirectUser) {
                            currentProcess.PrepareDirectReturn(stack);
                        } else if (ThreadPool.SchedulingEnabled) {
                            ThreadPool.Schedule(stack);
                        }
                    }
                    RestoreInterruptContext(cpuSlot, priorNestingDepth, priorVector);
                    return;
                }
                Panic.Error("CPL3 fault without a current process");
                for (;;) Native.Hlt();
            }

            // Only CPL0 faults reach the kernel panic path.  This distinction
            // is the containment boundary for the first native process proof.
            SerialWriteFaultBreadcrumbs(irq, actualErrorCode, &stack->rs, irs);

            if (Program.IsUefiMultiFrameActive()) {
                Program.LogUefiMultiFrameFaultContext();
            }

            // Display enhanced graphical panic screen
            InterruptReturnStack displayStack = *irs;
            displayStack.rsp = GetInterruptedRsp(irq, irs);
            Panic.ShowEnhancedCrashScreen(
                irq,
                actualErrorCode,
                hasErrorCode,
                &stack->rs,
                &displayStack,
                null
            );
            
            // This code should never be reached, but keep for safety
            for (; ; ) Native.Hlt();
        }

        if (irq == 0xFD) {
            Native.Cli();
            Native.Hlt();
            for (; ; ) Native.Hlt();
        }

        // Timer IRQ0 (PIC -> vector 0x20). Drive scheduler here.
        if (irq == 0x20) {
            // Debug: entering timer IRQ handler (no wait loop)
            _irq0Count++;
            bool logIrq0 = BootConsole.CurrentMode != guideXOS.BootMode.UEFI && _irq0Count <= 5;
            
            // Only log first few IRQ0s outside UEFI; in UEFI these interleave with
            // normal boot diagnostics and make the serial log hard to trust.
            if (logIrq0) {
                BootConsole.WriteLine("IRQ0");
            }
            
            // Update timer ticks
            Timer.OnInterrupt();
            
            // Debug: after Timer.OnInterrupt
            if (logIrq0) {
                BootConsole.WriteLine("TOK");
            }
            
            // Context switching is disabled during boot (SchedulingEnabled = false)
            // This just returns immediately without modifying the stack
            // A native context switch may not return to this handler, so acknowledge
            // the timer before handing control to the scheduler.
            Interrupts.EndOfInterrupt((byte)irq);
            ThreadPool.Schedule(stack);
            
            // Debug: after Schedule
            if (logIrq0) {
                BootConsole.WriteLine("SCH");
            }
            
            // Debug: after EOI, about to return
            if (logIrq0) {
                BootConsole.WriteLine("EOI");
            }
            
            RestoreInterruptContext(cpuSlot, priorNestingDepth, priorVector);
            return;
        }

        Interrupts.HandleInterrupt(irq);
        Interrupts.EndOfInterrupt((byte)irq);
        RestoreInterruptContext(cpuSlot, priorNestingDepth, priorVector);
    }

    private static void RestoreInterruptContext(int cpuSlot,
            int nestingDepth, int activeVector) {
        if (_interruptNestingByCpu == null ||
                _activeInterruptVectorByCpu == null) return;
        _interruptNestingByCpu[cpuSlot] = nestingDepth;
        _activeInterruptVectorByCpu[cpuSlot] = activeVector;
    }
}
