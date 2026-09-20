using guideXOS;
using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Helpers;
using guideXOS.Misc;
using Internal.Runtime.CompilerServices;
using System.Runtime;
using System.Runtime.InteropServices;
using static Internal.Runtime.CompilerHelpers.InteropHelpers;

public static class IDT {
    [DllImport("*")]
    private static extern unsafe void set_idt_entries(void* idt);

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
        idt = new IDTEntry[256];

        set_idt_entries(Unsafe.AsPointer(ref idt[0]));

        fixed (IDTEntry* _idt = idt) {
            idtr.Limit = (ushort)((sizeof(IDTEntry) * 256) - 1);
            idtr.Base = (ulong)_idt;
        }

        Native.Load_IDT(ref idtr);

        Initialized = true;
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

    private static void SerialWriteHexLine64(string label, ulong value) {
        SerialWriteLiteral(label);
        SerialWriteLiteral("0x");
        SerialWriteHex64(value);
        Native.Out8(0x3F8, (byte)'\n');
    }

    private static unsafe void SerialWritePageTableWalk(ulong virtualAddress) {
        const ulong Present = 1;
        const ulong LargePage = 1UL << 7;
        ulong cr3 = Native.ReadCR3() & ~0xFFFUL;
        SerialWriteHexLine64("CR3=", cr3);

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
        if (virtualAddress == 0 || (virtualAddress >> 48) != 0) return false;
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

    private static unsafe ulong GetInterruptedRsp(InterruptReturnStack* irs) {
        if (irs == null) return 0;

        // A ring-0 exception does not push RSP/SS.  The CPU frame is still
        // RIP, CS, RFLAGS, so its original RSP is the address after those 3
        // qwords.  For a privilege transition, use the CPU-pushed RSP.
        if ((irs->cs & 3UL) == 0)
            return (ulong)((byte*)irs + 24);
        return irs->rsp;
    }

    private static unsafe void SerialWriteFaultBreadcrumbs(int irq, ulong errorCode, RegistersStack* regs, InterruptReturnStack* irs) {
        switch (irq) {
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

        if (irq == 14) {
            SerialWriteHexLine64("CR2=", Native.ReadCR2());
        }

        if (irs != null) {
            SerialWriteHexLine64("RIP=", irs->rip);
            SerialWriteHexLine64("RSP=", GetInterruptedRsp(irs));
            SerialWriteHexLine64("CPU_FRAME_RAW_RSP_SLOT=", irs->rsp);
            SerialWriteHexLine64("CPU_FRAME_RAW_SS_SLOT=", irs->ss);
            SerialWriteStackNeighborhood(GetInterruptedRsp(irs));
        }

        if (regs != null) {
            SerialWriteHexLine64("REG_RAX=", regs->rax);
            SerialWriteHexLine64("REG_RCX=", regs->rcx);
            SerialWriteHexLine64("REG_RDX=", regs->rdx);
            SerialWriteHexLine64("REG_RBX=", regs->rbx);
            SerialWriteHexLine64("RBP=", regs->rbp);
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

        SerialWritePageTableWalk(irq == 14 ? Native.ReadCR2() : (irs != null ? irs->rip : 0));
    }

    [RuntimeExport("intr_handler")]
    public static unsafe void intr_handler(int irq, IDTStackGeneric* stack) {
        // Prevent nested interrupts while inside managed interrupt handler.
        Native.Cli();

        if (irq == 0x80) {
            // The diagnostic ABI is intentionally usable only from CPL3.  A
            // kernel-originated software interrupt is not a caller identity.
            if (stack != null && (stack->irs.cs & 3UL) == 3UL) {
                Ring3Abi.Dispatch(stack);
                return;
            }
            Panic.Error("Kernel invoked the Ring3 ABI gate");
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

            if (irs != null && (irs->cs & 3UL) == 3UL) {
                if (Ring3Process.HandleUserFault(irq, actualErrorCode, irs->rip,
                                                 irq == 14 ? Native.ReadCR2() : 0,
                                                 stack)) {
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
            displayStack.rsp = GetInterruptedRsp(irs);
            if ((displayStack.cs & 3UL) == 0) displayStack.ss = 0;
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
            
            return;
        }

        Interrupts.HandleInterrupt(irq);
        Interrupts.EndOfInterrupt((byte)irq);
    }
}
