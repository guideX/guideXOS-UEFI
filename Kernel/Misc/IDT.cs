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

    private static unsafe void SerialWriteFaultBreadcrumbs(int irq, ulong errorCode, InterruptReturnStack* irs) {
        switch (irq) {
            case 14:
                SerialWriteLineLiteral("UTINY_FAULT_PF");
                break;
            case 13:
                SerialWriteLineLiteral("UTINY_FAULT_GP");
                break;
            case 8:
                SerialWriteLineLiteral("UTINY_FAULT_DF");
                break;
        }

        SerialWriteHexLine8("VEC=", (byte)irq);
        SerialWriteHexLine64("ERR=", errorCode);

        if (irq == 14) {
            SerialWriteHexLine64("CR2=", Native.ReadCR2());
        }

        if (irs != null) {
            SerialWriteHexLine64("RIP=", irs->rip);
        }
    }

    [RuntimeExport("intr_handler")]
    public static unsafe void intr_handler(int irq, IDTStackGeneric* stack) {
        // Prevent nested interrupts while inside managed interrupt handler.
        Native.Cli();

        if (irq < 0x20) {
            // Compute correct location of InterruptReturnStack depending on whether the CPU pushed an error code
            InterruptReturnStack* irs;
            bool hasErrorCode = false;
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
                    // Exceptions that push an error code: irs follows RegistersStack + errorCode
                    irs = (InterruptReturnStack*)(((byte*)stack) + sizeof(RegistersStack) + sizeof(ulong));
                    hasErrorCode = true;
                    break;
                default:
                    // No error code pushed: irs follows only RegistersStack
                    irs = (InterruptReturnStack*)(((byte*)stack) + sizeof(RegistersStack));
                    hasErrorCode = false;
                    break;
            }

            SerialWriteFaultBreadcrumbs(irq, stack->errorCode, irs);

            if (irq == 14) {
                BootConsole.WriteLine("UTINY_FAULT_PF");
            } else if (irq == 13) {
                BootConsole.WriteLine("UTINY_FAULT_GP");
            } else if (irq == 8) {
                BootConsole.WriteLine("UTINY_FAULT_DF");
            }

            // Display enhanced graphical panic screen
            Panic.ShowEnhancedCrashScreen(
                irq,
                stack->errorCode,
                hasErrorCode,
                &stack->rs,
                irs,
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
            ThreadPool.Schedule(stack);
            
            // Debug: after Schedule
            if (logIrq0) {
                BootConsole.WriteLine("SCH");
            }
            
            // Send EOI to APIC
            Interrupts.EndOfInterrupt((byte)irq);
            
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
