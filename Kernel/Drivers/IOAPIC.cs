using guideXOS.Misc;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// The Intel I/O Advanced Programmable Interrupt Controller is used to distribute external interrupts in a more advanced manner than that of the standard 8259 PIC
    /// </summary>
    public static unsafe class IOAPIC {
        private const int IOREGSEL = 0x00;
        private const int IOWIN = 0x10;

        private const int IOAPICID = 0x00;
        private const int IOAPICVER = 0x01;
        private const int IOAPICARB = 0x02;
        private const int IOREDTBL = 0x10;

        public static void Initialize() {
            if (ACPI.IO_APIC == null) {
                Panic.Error("[I/O APIC] Can't initialize I/O APIC");
                return;
            }
            uint value = In(IOAPICVER);
            uint count = ((value >> 16) & 0xFF) + 1;

            //Disable All Entries
            for (uint i = 0; i < count; ++i) {
                SetEntry((byte)i, 1 << 16);
            }
            //Console.WriteLine("[I/O APIC] I/O APIC Initialized");
        }

        public static uint In(byte reg) {
            MMIO.Out32((uint*)(ACPI.IO_APIC->IOApicAddress + IOREGSEL), reg);
            return MMIO.In32((uint*)(ACPI.IO_APIC->IOApicAddress + IOWIN));
        }

        public static void Out(byte reg, uint value) {
            MMIO.Out32((uint*)(ACPI.IO_APIC->IOApicAddress + IOREGSEL), reg);
            MMIO.Out32((uint*)(ACPI.IO_APIC->IOApicAddress + IOWIN), value);
        }

        public static void SetEntry(byte index, ulong data) {
            Out((byte)(IOREDTBL + index * 2), (uint)data);
            Out((byte)(IOREDTBL + index * 2 + 1), (uint)(data >> 32));
        }

        /// <summary>
        /// Configure an I/O APIC redirection entry for a given legacy IRQ line.
        /// 
        /// NOTE: This method expects a legacy IRQ number (0-15), not an IDT vector.
        /// The IDT vector should be chosen by the caller; GuideXOS commonly uses 0x20 for timer.
        /// </summary>
        public static void SetEntryForIrq(uint legacyIrq, byte vector = 0x20) {
            // Apply ACPI interrupt source overrides
            uint gsi = ACPI.RemapIRQ(legacyIrq);

            uint baseGsi = ACPI.IO_APIC->GlobalSystemInterruptBase;
            if (gsi < baseGsi) return;
            uint index = gsi - baseGsi;
            uint version = In(IOAPICVER);
            uint count = ((version >> 16) & 0xFF) + 1;
            if (index >= count) return;
            
            // Build a basic redirect entry:
            // bits 0-7: vector
            // bit 16: mask (0 = enabled)
            // deliver to BSP (destination in high dword, set later if needed)
            ulong entry = vector;
            ushort flags;
            uint overrideGsi;
            if (ACPI.TryGetInterruptOverride(legacyIrq, out overrideGsi, out flags)) {
                // MADT flags: 0x3 = active-low, 0xC = level-triggered.
                if ((flags & 0x3) == 0x3) entry |= 1UL << 13;
                if ((flags & 0xC) == 0xC) entry |= 1UL << 15;
            }
            SetEntry((byte)index, entry);
            if (vector == 0x21 || vector == 0x2C) {
                BootConsole.WriteLine("[IOAPIC] IRQ" + legacyIrq.ToString() +
                    " GSI" + gsi.ToString() +
                    (vector == 0x21 ? " VEC0x21" : " VEC0x2C"));
            }
        }

        public static void SetEntry(uint irq) {
            // Back-compat shim: existing callers pass an IDT vector like 0x20 (timer), 0x21 (keyboard), 0x2C (mouse)
            // These correspond to legacy IRQs:
            // - 0x20 = IRQ0 (timer) 
            // - 0x21 = IRQ1 (keyboard)
            // - 0x2C = IRQ12 (mouse) = 0x20 + 12
            
            if (irq >= 0x20 && irq < 0x30) {
                // This is an IDT vector in the remapped range (0x20-0x2F = IRQ0-15)
                uint legacyIrq = irq - 0x20;
                SetEntryForIrq(legacyIrq, (byte)irq);
                return;
            }

            // Otherwise assume the caller passed a legacy IRQ directly
            SetEntryForIrq(irq, (byte)(irq + 0x20));
        }
    }
}
