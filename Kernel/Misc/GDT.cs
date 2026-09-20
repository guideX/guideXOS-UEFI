using Internal.Runtime.CompilerServices;
using System.Runtime.InteropServices;


static class GDT {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct GDTEntry {
        public ushort LimitLow;
        public ushort BaseLow;
        public byte BaseMid;
        public byte Access;
        public byte LimitHigh_Flags;
        public byte BaseHigh;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GDTDescriptor {
        public ushort Limit;
        public ulong Base;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct TSSEntry {
        public ushort LimitLow;
        public ushort BaseLow;
        public byte BaseMidLow;
        public byte Access;
        public byte LimitHigh_Flags;
        public byte BaseMidHigh;
        public uint BaseHigh;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct TSS {
        public uint Reserved0;
        public uint Rsp0Low;
        public uint Rsp0High;
        public uint Rsp1Low;
        public uint Rsp1High;
        public uint Rsp2Low;
        public uint Rsp2High;
        public uint Reserved1;
        public uint Reserved2;
        public uint Ist1Low;
        public uint Ist1High;
        public uint Ist2Low;
        public uint Ist2High;
        public uint Ist3Low;
        public uint Ist3High;
        public uint Ist4Low;
        public uint Ist4High;
        public uint Ist5Low;
        public uint Ist5High;
        public uint Ist6Low;
        public uint Ist6High;
        public uint Ist7Low;
        public uint Ist7High;
        public ulong Reserved3;
        public ushort Reserved4;
        public ushort IOMapBase;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct GDTS {
        public GDTEntry Empty;
        public GDTEntry KernelCode;
        public GDTEntry KernelData;
        public GDTEntry UserCode;
        public GDTEntry UserData;
        public TSSEntry TSS;
    }


    static TSS tss;
    static GDTS gdts;
    public static GDTDescriptor gdtr;
    public static ulong KernelStackTop { get; private set; }

    public const ushort KernelCodeSelector = 0x08;
    public const ushort KernelDataSelector = 0x10;
    public const ushort UserCodeSelector = 0x18 | 3; // RPL 3
    public const ushort UserDataSelector = 0x20 | 3; // RPL 3
    public const ushort TssSelector = 0x28; // occupies two entries

    static void FillEntry(ref GDTEntry e, uint baseAddr, uint limit, byte access, byte flags) {
        e.LimitLow = (ushort)(limit & 0xFFFF);
        e.BaseLow = (ushort)(baseAddr & 0xFFFF);
        e.BaseMid = (byte)((baseAddr >> 16) & 0xFF);
        e.Access = access;
        e.LimitHigh_Flags = (byte)(((limit >> 16) & 0x0F) | (flags & 0xF0));
        e.BaseHigh = (byte)((baseAddr >> 24) & 0xFF);
    }

    public static void Initialize() {
        // Kernel code: DPL=0, executable, readable
        FillEntry(ref gdts.KernelCode, 0, 0xFFFFF, 0x9A, 0xA0 | 0x0F);
        // Kernel data: DPL=0, writable
        FillEntry(ref gdts.KernelData, 0, 0xFFFFF, 0x92, 0xC0 | 0x0F);
        // User code: DPL=3, executable, readable
        FillEntry(ref gdts.UserCode, 0, 0xFFFFF, 0xFA, 0xA0 | 0x0F);
        // User data: DPL=3, writable
        FillEntry(ref gdts.UserData, 0, 0xFFFFF, 0xF2, 0xC0 | 0x0F);

        unsafe {
            fixed (TSS* _tss = &tss) {
                var addr = (ulong)_tss;
                gdts.TSS.LimitLow = (ushort)(Unsafe.SizeOf<TSS>() - 1);
                gdts.TSS.BaseLow = (ushort)(addr & 0xFFFF);
                gdts.TSS.BaseMidLow = (byte)((addr >> 16) & 0xFF);
                gdts.TSS.BaseMidHigh = (byte)((addr >> 24) & 0xFF);
                gdts.TSS.BaseHigh = (uint)(addr >> 32);
                gdts.TSS.Access = 0x89; // present, type 64-bit TSS (available)
                gdts.TSS.LimitHigh_Flags = 0x00;

                // Disable I/O for user-mode by default
                tss.IOMapBase = (ushort)Unsafe.SizeOf<TSS>();
            }
        }

        unsafe {
            fixed (GDTS* _gdts = &gdts) {
                gdtr.Limit = (ushort)(Unsafe.SizeOf<GDTS>() - 1);
                gdtr.Base = (ulong)_gdts;
            }
        }

        Native.Load_GDT(ref gdtr);
        
        // CRITICAL: Reload segment registers with the new GDT selectors
        // Without this, the CPU continues using UEFI's segment selectors (CS=0x38, SS=0x30)
        // which causes interrupt returns to fail
        Native.Reload_Segments();

        Native.Load_TR(TssSelector);
    }

    public static void SetKernelStack(ulong rsp0) {
        KernelStackTop = rsp0;
        tss.Rsp0Low = (uint)(rsp0 & 0xFFFF_FFFF);
        tss.Rsp0High = (uint)(rsp0 >> 32);
    }

    public static bool IsTaskRegisterLoaded() => Native.Read_TR() == TssSelector;
}
