using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Drivers.Input;
using Internal.Runtime.CompilerHelpers;
using System;
using System.Runtime;
using System.Runtime.InteropServices;
namespace guideXOS.Misc {
    /// <summary>
    /// Entry Point
    /// </summary>
    internal static unsafe class EntryPoint {
        #region "DLLIMPORTS"
        /// <summary>
        /// Import the module table accessor from native_stubs.asm This returns the address of the __Module symbol which contains the NativeAOT module table
        /// </summary>
        /// <returns></returns>
        [DllImport("*", EntryPoint = "__modules_a")]
        private static extern IntPtr GetModulesPointer();
        /// <summary>
        /// Boot debug marker - writes 'K!' to serial and magenta pixel to framebuffer This is pure assembly with NO managed code overhead
        /// </summary>
        [DllImport("*")]
        private static extern void SerialDebugMarker();
        /// <summary>
        /// IMPORTANT: This forces KMainWrapper to be included in the final executable KMainWrapper is the REAL entry point that should beCalled by the bootloader It writes debug markers before calling KMain
        /// </summary>
        /// <param name="bootInfo"></param>
        [DllImport("*")]
        private static extern void KMainWrapper(UefiBootInfo* bootInfo);
        #endregion
        #region "VARIABLES"
        /// <summary>
        /// Static field to hold KMainWrapper address - this forces linker to include it
        /// </summary>
        private static IntPtr _kMainWrapperAddr;
        #endregion
        #region "METHODS"
        /// <summary>
        /// Helper to force KMainWrapper inclusion - MUST be called from reachable code
        /// </summary>
        private static void ForceIncludeKMainWrapper() {
            // Take address of KMainWrapper to force linker to include it
            _kMainWrapperAddr = (IntPtr)(delegate*<UefiBootInfo*, void>)&KMainWrapper;
        }

        private static void SerialRawLine(string text) {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++) {
                Native.Out8(0x3F8, (byte)text[i]);
            }
            Native.Out8(0x3F8, (byte)'\n');
        }
        /// <summary>
        /// NEW UEFI entry point called from UEFI bootloader
        /// This is the modern entry point that receives guideXOS::BootInfo
        /// NOTE: The UEFI bootloader uses Microsoft x64 ABI (parameter in RCX).
        /// This should match since we're building for Windows x64.
        /// </summary>
        /// <param name="bootInfo">UEFI boot information structure</param>
        [RuntimeExport("KMain")]
        public static void KMain(UefiBootInfo* bootInfo) {
            // CRITICAL: Output RAW debug marker FIRST before any managed code
            // This proves we reached the kernel entry point successfully
            SerialDebugMarker();
            
            // Simple framebuffer test - write colored pixels at top-left to prove we're alive
            // This is RAW memory access with no C# overhead
            ulong fbBase = bootInfo != null ? bootInfo->FramebufferBase : 0;
            if (fbBase != 0) {
                // Write BRIGHT RED pixels at (0,0) to signal kernel entry
                uint* fb0 = (uint*)fbBase;
                for (int i = 0; i < 40; i++) {
                    fb0[i] = 0x00FF0000; // RED = kernel entered!
                }
            }
            
            // Now it's safe to try BootConsole
            BootConsole.WriteLine("[KMAIN] INITIALIZE");
            BootConsole.WriteLine("[FRAMEBUFFER] INITIALIZE");
            
            // Validate bootInfo pointer
            if (bootInfo == null) {
                // Write YELLOW pixels to show null bootInfo
                if (fbBase != 0) {
                    uint* fb0 = (uint*)fbBase;
                    for (int i = 40; i < 80; i++) {
                        fb0[i] = 0x00FFFF00; // YELLOW = null bootInfo
                    }
                }
                for (;;) {
                    Native.Hlt();
                }
            }
            
            // Validate framebuffer
            if (bootInfo->FramebufferBase == 0) {
                for (;;) {
                    Native.Hlt();
                }
            }
            
            // Draw more pixels to show validation passed
            if (fbBase != 0) {
                uint* fb0 = (uint*)fbBase;
                // Skip first 40 pixels (already red), write white
                fb0[40] = 0x00FFFFFF; // Write WHITE pixels at (40,0)
                fb0[41] = 0x00FFFFFF;
                fb0[42] = 0x00FFFFFF;
                fb0[43] = 0x00FFFFFF;
                fb0[44] = 0x00FFFFFF;
            }
            uint* fb = (uint*)bootInfo->FramebufferBase;
            uint pitch = bootInfo->FramebufferPitch / 4;
            if (BootConsole.DrawDebugLines)
                for (uint x = 0; x < 200; x++)
                    fb[100 * pitch + x] = 0x00FF00FF; // Draw magenta line at y=100

            BootConsole.WriteLine("[ALLOCATOR] INITIALIZE");
            Allocator.Initialize((IntPtr)0x4000000);
            for (uint x = 0; x < 200; x++) 
                fb[110 * pitch + x] = 0x0000FF00; // Draw green line

            BootConsole.WriteLine("[MOD] INITIALIZE");
            IntPtr modulesPtr = GetModulesPointer(); // Get the module pointer from native code
            ulong modAddr = (ulong)modulesPtr; // Print the module pointer for debugging
            for (int shift = 28; shift >= 0; shift -= 4) { // Print 8 hex digits of the address
                int nibble = (int)((modAddr >> shift) & 0xF);
                char hexChar = (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)hexChar);
            }
            
            // Not yet Compatible with UEFI
            if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy)
                StartupCodeHelpers.InitializeModules(modulesPtr);

            if (BootConsole.DrawDebugLines) {
                // Draw cyan line to show modules initialized
                for (uint x = 0; x < 200; x++) {
                    fb[120 * pitch + x] = 0x0000FFFF;
                }
            }

            // Not yet Compatible with UEFI
            if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy)
                PageTable.Initialize();

            // SKIP PageTable.Initialize() - bootloader already set up identity mapping

            BootConsole.WriteLine("[ASC] INIT");
            ASC16.Initialize();
            BootConsole.WriteLine("[FBI] INIT");
            // Initialize framebuffer wrapper
            if (bootInfo->HasFramebuffer && bootInfo->FramebufferBase != 0) {
                Framebuffer.SetBootInfo(bootInfo);
                Framebuffer.Initialize(
                    (ushort)bootInfo->FramebufferWidth,
                    (ushort)bootInfo->FramebufferHeight,
                    (uint*)bootInfo->FramebufferBase
                );
                Framebuffer.Graphics.Clear(0x0); // Clear screen
            }
            BootConsole.WriteLine("[BS] INIT");
            BootSplash.Initialize("Team Nexgen", "guideXOS", "Version: 0.2 UEFI"); // Boot splash
            
            // Try allocating a simple array to test if runtime works
            try {
                uint[] testArr = new uint[4];
                testArr[0] = 0xDEADBEEF;
                testArr[1] = 0xCAFEBABE;
                if (testArr[0] == 0xDEADBEEF) {
                    BootConsole.WriteLine("[RUNTIME] ALLOCATE TEST PASS");
                }
            } catch {
            }

            BootConsole.WriteLine("[CONS] INITIALIZE");
            Console.Setup();
            
            BootConsole.WriteLine("[ARCH] INITIALIZE");
            DetectArchitecture();
            
            BootConsole.WriteLine("[IDT] INITIALIZE");
            IDT.Disable(); // Initialize GDT/IDT
            
            BootConsole.WriteLine("[GDT] INITIALIZE");
            GDT.Initialize();
            BootConsole.WriteLine("[KERNEL] SET STACK SPACE");
            {
                const ulong kStackSize = 64 * 1024;
                ulong rsp0 = (ulong)Allocator.Allocate(kStackSize) + kStackSize;
                GDT.SetKernelStack(rsp0);
            }
            
            BootConsole.WriteLine("[IDT] INIT");
            IDT.Initialize();
            IDT.AllowUserSoftwareInterrupt(0x80);
            
            BootConsole.WriteLine("[INTERRUPTS] INIT");
            Interrupts.Initialize();
            
            // Keep interrupts disabled until PIC is configured below.
            // IDT.Enable();
            SSE.enable_sse();
            Native.Cli(); // Disable interrupts for driver init
            if (bootInfo->AcpiRsdp != 0) {
                BootConsole.WriteLine("[ACPI] RSDP address available");
            }

            // ACPI is required for APIC discovery in UEFI mode.
            // Use the bootloader-provided RSDP under UEFI instead of legacy memory scanning.
            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                if (bootInfo->AcpiRsdp != 0) {
                    ACPI.InitializeFromRsdp(bootInfo->AcpiRsdp);
                } else {
                    BootConsole.WriteLine("[ACPI] WARNING: No RSDP provided by bootloader");
                }
            } else {
                ACPI.Initialize();
            }
#if UseAPIC
            BootConsole.WriteLine("[PIC] DISABLED");
            PIC.Disable();
            BootConsole.WriteLine("[Local APIC] INIT");
            LocalAPIC.Initialize();
            BootConsole.WriteLine("[Local APIC] INIT DONE");
            BootConsole.WriteLine("[IO APIC] INIT");
            IOAPIC.Initialize();
            BootConsole.WriteLine("[IO APIC] INIT DONE");
#else
            BootConsole.WriteLine("[PIC] ENABLED");
            PIC.Enable();
#endif
            
            BootConsole.WriteLine("[TIMER] INIT"); 
            Timer.Initialize();
            if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy)
                 Keyboard.Initialize();

            BootConsole.WriteLine("[SERIAL] INIT");
            Serial.Initialize();
            
            // PS/2 Controller initialization moved to capability-based detection in Program.KMain
            // This prevents unconditional PS/2 access on UEFI systems without PS/2 hardware
            if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                BootConsole.WriteLine("[PS2] Controller initialization deferred to capability detection");
            }
            
            BootConsole.WriteLine("[VMWARE] INIT");
            VMwareTools.Initialize();

            // Initialize UEFI mouse input if available (before other subsystems)
            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                bool skipEarlyUefiHardwareInit = Program.ShouldSkipEarlyUefiHardwareInitialization();
                // The UEFI bootloader exits boot services before entering KMain.
                // Mark this before touching any firmware protocol pointers from BootInfo.
                BootConsole.WriteLine("[EBS] Marking ExitBootServices as occurred");
                ExitBootServicesRules.MarkExitBootServices();
                BootConsole.WriteLine("[EBS] ExitBootServices marked");

                if (skipEarlyUefiHardwareInit) {
                    BootConsole.WriteLine("[INPUT] Skipping early UEFI mouse input and PCI init (safe/step probe)");
                } else {
                    BootConsole.WriteLine("[INPUT] Initializing UEFI mouse input");
                    try {
                        MouseInputManager.Initialize(bootInfo);
                        if (MouseInputManager.IsInitialized) {
                            BootConsole.WriteLine("[INPUT] MouseInputManager initialized");
                        }
                    } catch {
                        BootConsole.WriteLine("[INPUT] MouseInputManager initialization failed");
                    }
                }

            }

            if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy)
                 SMBIOS.Initialize();
            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI && Program.ShouldSkipEarlyUefiHardwareInitialization()) {
                BootConsole.WriteLine("[PCI] SKIPPED (safe/step probe)");
            } else {
                BootConsole.WriteLine("[PCI] INIT");
                PCI.Initialize();
            }

            if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                IDE.Initialize();
                SATA.Initialize();
                ThreadPool.Initialize();
            }

#if !UseAPIC
            Native.Out8(0x21, 0xFF); // Master PIC: mask all IRQs (IRQ 0-7)
            Native.Out8(0xA1, 0xFF); // Slave PIC: mask all IRQs (IRQ 8-15)
#endif

            BootConsole.WriteLine("[SCHED] ThreadPool.Initialize");
            ThreadPool.Initialize();
            
            // Debug: ThreadPool.Initialize returned
            Native.Out8(0x3F8, (byte)'T');
            Native.Out8(0x3F8, (byte)'P');
            Native.Out8(0x3F8, (byte)'R');
            Native.Out8(0x3F8, (byte)'E');
            Native.Out8(0x3F8, (byte)'T');
            Native.Out8(0x3F8, (byte)'\n');
            
            BootConsole.WriteLine("[SCHED] ThreadPool.Initialize complete");

#if !UseAPIC
            // Enable only timer IRQ (IRQ0 -> vector 0x20 with PIC remap) for scheduling.
            BootConsole.WriteLine("[PIC] Enabling IRQ0 (timer) only");
            // Ensure interrupts are disabled while changing masks
            Native.Cli();
            Interrupts.EnableInterrupt(0);

            // TEMP: for bring-up, do NOT allow IRQ0 to fire immediately upon STI.
            // Leave the masks configured, but keep IRQ0 masked at the PIC right before STI.
            Native.Out8(0x21, (byte)(Native.In8(0x21) | 0x01));

            BootConsole.WriteLine("[INTERRUPTS] STI (timer irq enabled)");
            BootConsole.WriteLine("[INTERRUPTS] STI done");

            // Assembly-only marker before enabling interrupts
            SerialDebugMarker();
            Native.Sti();
            
            // Assembly-only marker after STI
            SerialDebugMarker();

            // Immediately mask IRQ0 again to avoid being trapped in the IRQ0 handler during early boot.
            SerialDebugMarker();
            Native.Out8(0x21, (byte)(Native.In8(0x21) | 0x01));

            // Disable interrupts again to continue deterministic boot.
            SerialDebugMarker();
            Native.Cli();
            SerialDebugMarker();
#else
            // APIC mode: Local APIC timer drives vector 0x20. Do not touch PIC.
            BootConsole.WriteLine("[APIC] STI (Local APIC timer active)");
            Native.Sti();
#endif

            BootConsole.WriteLine("[BOOT] Post-STI continue");
            BootConsole.WriteLine("[BOOT] About to cleanup splash");

            // Give the system a moment to service IRQ0 and prove the IDT path works
            for (int i = 0; i < 50; i++) {
                Native.Nop();
            }

            //BootConsole.WriteLine("[STI_INIT]");
            // Native.Sti(); // Re-enable interrupts (timer enabled)
            //BootConsole.WriteLine("[CHECK_RAMDISK]");
            
            // UEFI: Initialize ramdisk and filesystem for File API support (PNG loading, etc.)
            if (bootInfo->HasRamdisk && bootInfo->RamdiskBase != 0) {
                BootConsole.WriteLine("[Initrd] Initializing Ramdisk");
                
                // Debug: Print ramdisk address
                ulong rdAddr = bootInfo->RamdiskBase;
                Native.Out8(0x3F8, (byte)'R');
                Native.Out8(0x3F8, (byte)'D');
                Native.Out8(0x3F8, (byte)'@');
                for (int shift = 60; shift >= 0; shift -= 4) {
                    int nibble = (int)((rdAddr >> shift) & 0xF);
                    char hexChar = (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
                    Native.Out8(0x3F8, (byte)hexChar);
                }
                Native.Out8(0x3F8, (byte)'\n');
                
                // CRITICAL DEBUG: Read first 16 bytes directly from bootInfo->RamdiskBase
                // This tests if the memory is correctly mapped BEFORE Ramdisk class is created
                byte* directPtr = (byte*)rdAddr;
                Native.Out8(0x3F8, (byte)'D');
                Native.Out8(0x3F8, (byte)'I');
                Native.Out8(0x3F8, (byte)'R');
                Native.Out8(0x3F8, (byte)':');
                for (int i = 0; i < 16; i++) {
                    byte b = directPtr[i];
                    char hi = (char)(((b >> 4) & 0xF) < 10 ? '0' + ((b >> 4) & 0xF) : 'A' + (((b >> 4) & 0xF) - 10));
                    char lo = (char)((b & 0xF) < 10 ? '0' + (b & 0xF) : 'A' + ((b & 0xF) - 10));
                    Native.Out8(0x3F8, (byte)hi);
                    Native.Out8(0x3F8, (byte)lo);
                }
                Native.Out8(0x3F8, (byte)'\n');
                
                // Check if it's RDSK
                if (directPtr[0] == (byte)'R' && directPtr[1] == (byte)'D' && 
                    directPtr[2] == (byte)'S' && directPtr[3] == (byte)'K') {
                    BootConsole.WriteLine("[Initrd] RDSK magic found directly!");
                } else {
                    BootConsole.WriteLine("[Initrd] WARNING: No RDSK magic at base address!");
                    // Try printing what we see as ASCII
                    Native.Out8(0x3F8, (byte)'A');
                    Native.Out8(0x3F8, (byte)'S');
                    Native.Out8(0x3F8, (byte)'C');
                    Native.Out8(0x3F8, (byte)':');
                    for (int i = 0; i < 4; i++) {
                        byte b = directPtr[i];
                        if (b >= 32 && b < 127) {
                            Native.Out8(0x3F8, b);
                        } else {
                            Native.Out8(0x3F8, (byte)'.');
                        }
                    }
                    Native.Out8(0x3F8, (byte)'\n');
                }
                
                try {
                    // CRITICAL: Set Disk.Instance BEFORE filesystem init
                    Disk.Instance = new Ramdisk((IntPtr)bootInfo->RamdiskBase);
                    BootConsole.WriteLine("[Initrd] Ramdisk initialized");
                    
                    // UEFI ramdisk uses custom RDSK format (not TAR)
                    BootConsole.WriteLine("[FS] Mounting RdskFS");
                    try {
                        File.Instance = new RdskFS();
                        BootConsole.WriteLine("[FS] RdskFS mounted");
                    } catch {
                        BootConsole.WriteLine("[FS] WARNING: RdskFS mount failed");
                    }
                } catch {
                    BootConsole.WriteLine("[Initrd] WARNING: Ramdisk initialization failed");
                }
            } else {
                BootConsole.WriteLine("[Initrd] WARNING: No ramdisk loaded!");
            }
            // SKIP boot splash animation - Timer.Sleep() might not work with masked interrupts
            // for (int i = 0; i < 120; i++) {
            //     BootSplash.Tick();
            // }
            //BootConsole.WriteLine("BOOTSPLASH_CLEANUP");
            SerialRawLine("POSTFS_A");
            BootSplash.Cleanup();
            SerialRawLine("POSTFS_B");

            // SKIP uptime assignment - Timer.Ticks might not work with masked interrupts
            // and this might trigger static initialization that hangs
            // guideXOS.DockableWidgets.Uptime.BootTimeTicks = Timer.Ticks;

            // SKIP SystemMode.DetectMode() - might access complex state
            // guideXOS.OS.SystemMode.DetectMode();

            // SKIP File.Exists and disk mounting - go straight to GUI
            // if (File.Exists("/boot/config.txt")) {
            //     Disk.Instance = IDE.Ports[0];
            //     File.Instance = new FAT();
            //     BootConsole.WriteLine("[BOOT] Mounted /dev/sda2 as root");
            // } else {
            //     BootConsole.WriteLine("[BOOT] Using default filesystem");
            // }

            // SKIP Configuration.Initialize() - might access files/complex state
            // guideXOS.OS.Configuration.Initialize();

            // Call main kernel entry - this returns after GUI is set up!
            //BootConsole.WriteLine("[CALLING_KERNEL_MAIN]");
            KernelMain();

            // From here on, scheduling is driven by vector 0x20.
#if !UseAPIC
            // Legacy PIC mode: unmask IRQ0 and enable interrupts.
            SerialDebugMarker();
            Native.Out8(0x21, (byte)(Native.In8(0x21) & 0xFE)); // unmask IRQ0
            SerialDebugMarker();
            Native.Sti();
            SerialDebugMarker();
#else
            // APIC mode: Local APIC timer already configured; just ensure interrupts are enabled.
            Native.Sti();
#endif

            ThreadPool.StartScheduling();

            // If we ever return here, stop.
            for (;;) {
                Native.Hlt();
            }
        }
        /// <summary>
        /// LEGACY Multiboot entry point (for GRUB2 compatibility)
        /// This is the old entry point that receives MultibootInfo
        /// </summary>
        /// <param name="Info"></param>
        /// <param name="Modules"></param>
        /// <param name="Trampoline"></param>
        [RuntimeExport("Entry")]
        public static void Entry(MultibootInfo* Info, IntPtr Modules, IntPtr Trampoline) {
            Allocator.Initialize((IntPtr)0x20000000);
            StartupCodeHelpers.InitializeModules(Modules);
            PageTable.Initialize();
            ASC16.Initialize();
            VBEInfo* info = (VBEInfo*)Info->VBEInfo;
            if (info->PhysBase != 0) {
                Framebuffer.Initialize(info->ScreenWidth, info->ScreenHeight, (uint*)info->PhysBase);
                Framebuffer.Graphics.Clear(0x0);
            } else {
                for (; ; ) Native.Hlt();
            }
            // Boot splash init
            BootSplash.Initialize("Team Nexgen", "guideXOS", "Version: 0.2");
            Console.Setup();

            // Detect and log architecture
            DetectArchitecture();

            IDT.Disable();
            GDT.Initialize();
            {
                const ulong kStackSize = 64 * 1024;
                ulong rsp0 = (ulong)Allocator.Allocate(kStackSize) + kStackSize;
                GDT.SetKernelStack(rsp0);
            }
            IDT.Initialize();
            IDT.AllowUserSoftwareInterrupt(0x80);
            Interrupts.Initialize();
            SSE.enable_sse();

            // Enable AVX if supported - COMMENTED OUT: CPUID native functions not yet implemented
            // if (CPUIDHelper.IsCPUIDSupported() && CPUIDHelper.HasAVX()) {
            //     BootConsole.WriteLine("[CPU] AVX supported - enabling");
            //     // AVX.init_avx();  // Uncomment when AVX initialization is implemented
            // }

            ACPI.Initialize();
#if UseAPIC
            PIC.Disable();
            LocalAPIC.Initialize();
            IOAPIC.Initialize();
#else
            PIC.Enable();
#endif
            Timer.Initialize();
            Keyboard.Initialize();
            Serial.Initialize();
            
            // PS/2 Controller and Mouse initialization moved to capability-based detection
            // in Program.KMain to allow proper detection and fallback
            BootConsole.WriteLine("[PS2] Controller initialization deferred to capability detection");
            
            VMwareTools.Initialize();
            SMBIOS.Initialize();
            PCI.Initialize();
            IDE.Initialize();
            SATA.Initialize();
            ThreadPool.Initialize();
            BootConsole.WriteLine($"[SMP] Trampoline: 0x{((ulong)Trampoline).ToString("x2")}");
            Native.Movsb((byte*)SMP.Trampoline, (byte*)Trampoline, 512);
            SMP.Initialize((uint)SMP.Trampoline);
            BootConsole.Write("[Initrd] Initrd: 0x");
            BootConsole.WriteLine((Info->Mods[0]).ToString("x2"));
            BootConsole.WriteLine("[Initrd] Initializing Ramdisk");
            new Ramdisk((IntPtr)(Info->Mods[0]));
            // Initialize filesystem: Auto-detect FAT (12/16/32) or TAR
            new AutoFS();
            // While we are still here (single core boot), animate splash a bit
            for (int i = 0; i < 120; i++) { // ~2 seconds at 60Hz
                BootSplash.Tick();
            }

            // Cleanup boot splash resources before transitioning to desktop
            BootSplash.Cleanup();

            // Record boot time for uptime tracking
            guideXOS.DockableWidgets.Uptime.BootTimeTicks = Timer.Ticks;

            // Detect system mode (LiveMode vs Installed)
            guideXOS.OS.SystemMode.DetectMode();

            if (File.Exists("/boot/config.txt")) {
                // Booting from HDD - switch to system partition
                Disk.Instance = IDE.Ports[0]; // Or SATA.Drives[0]
                File.Instance = new FAT();
                BootConsole.WriteLine("[BOOT] Mounted /dev/sda2 as root");
            } else {
                // Booting from USB/CD - use ramdisk
                BootConsole.WriteLine("[BOOT] Using ramdisk");
            }

            // Initialize configuration system (only works when not in LiveMode)
            guideXOS.OS.Configuration.Initialize();

            KernelMain();
        }
        /// <summary>
        /// Main kernel initialization (shared by both entry points)
        /// </summary>
        private static void KernelMain() {
            Native.Out8(0x3F8, (byte)'K');
            Native.Out8(0x3F8, (byte)'E');
            Native.Out8(0x3F8, (byte)'R');
            Native.Out8(0x3F8, (byte)'N');
            Native.Out8(0x3F8, (byte)'E');
            Native.Out8(0x3F8, (byte)'L');
            Native.Out8(0x3F8, (byte)'M');
            Native.Out8(0x3F8, (byte)'A');
            Native.Out8(0x3F8, (byte)'I');
            Native.Out8(0x3F8, (byte)'N');
            Native.Out8(0x3F8, (byte)'_');
            Native.Out8(0x3F8, (byte)'E');
            Native.Out8(0x3F8, (byte)'N');
            Native.Out8(0x3F8, (byte)'T');
            Native.Out8(0x3F8, (byte)'R');
            Native.Out8(0x3F8, (byte)'Y');
            Native.Out8(0x3F8, (byte)'_');
            Native.Out8(0x3F8, (byte)'R');
            Native.Out8(0x3F8, (byte)'A');
            Native.Out8(0x3F8, (byte)'W');
            Native.Out8(0x3F8, (byte)'\n');
            BootConsole.WriteLine("[KERNELMAIN]");
            BootConsole.NewLine();
            Program.KMain(); // Call the main OS initialization - this sets up GUI, drivers, etc.
        }
        /// <summary>
        /// Detect and validate system architecture
        /// </summary>
        private static void DetectArchitecture() {
            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI)
                BootConsole.WriteLine("[ARCH] DETECT");
            else
                BootConsole.WriteLine("=== Architecture Detection ===");
            int ptrSize2 = sizeof(nint); // 1. Pointer size check - use simple strings instead of interpolation
            if (ptrSize2 == 8) {
                if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                    BootConsole.WriteLine("[ARCH] Pointer Size: 8 bytes (64-bit)");
                    BootConsole.WriteLine("[ARCH] Running in 64-bit mode (AMD64)");
                } else if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                    BootConsole.WriteLine("[ARCH] Pointer Size: 8 bytes (64-bit)");
                    BootConsole.WriteLine("[ARCH] Running in 64-bit mode (AMD64)");
                }
            } else if (ptrSize2 == 4) {
                if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                    BootConsole.WriteLine("[ARCH] Pointer Size: 4 bytes (32-bit)");
                    BootConsole.WriteLine("[ARCH] Running in 32-bit mode");
                } else if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                    BootConsole.WriteLine("[ARCH] Pointer Size: 4 bytes (32-bit)");
                    BootConsole.WriteLine("[ARCH] Running in 32-bit mode");
                }
            } else {
                if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI)
                    BootConsole.WriteLine("[ARCH] Pointer Size: Unknown");
                else
                    BootConsole.WriteLine("[ARCH] Pointer Size: Unknown");
            }
            TestPointerIntegrity(); // 3. Test pointer arithmetic integrity
            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI)
                BootConsole.WriteLine("=== Architecture Detection Complete ===");
            else
                BootConsole.WriteLine("=== Architecture Detection Complete ===");
        }
        /// <summary>
        /// Test that pointer arithmetic works correctly (no truncation)
        /// </summary>
        private static void TestPointerIntegrity() {
            if (sizeof(nint) == 8) {
                ulong testValue = 0x123456789ABCDEF0UL; // Test 64-bit pointer handling
                void* testPtr = (void*)testValue;
                ulong recovered = (ulong)testPtr;
                if (recovered == testValue) {
                    BootConsole.WriteLine("[ARCH] Pointer integrity: PASS (64-bit pointers working)");
                } else {
                    // Avoid string interpolation in Panic - just use simple message
                    BootConsole.WriteLine("[ARCH] ERROR: Pointer truncation detected!");
                    Panic.Error("Pointer truncation detected!");
                }
            } else {
                BootConsole.WriteLine("[ARCH] Pointer integrity: SKIP (32-bit mode)");
            }
        }
        #endregion
    }
}
