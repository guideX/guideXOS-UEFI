using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using guideXOS;
using guideXOS.DefaultApps;
using guideXOS.DockableWidgets;
using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Drivers.Input;
using guideXOS.Misc;
using guideXOS.OS;
/// <summary>
/// Program
/// </summary>
unsafe class Program {
    /// <summary>
    /// Main
    /// </summary>
    static void Main() {
    }
    /// <summary>
    /// DLL Import
    /// </summary>
    [DllImport("*")]
    public static extern void test();
    #region "public variables"
    /// <summary>
    /// Wallpaper
    /// </summary>
    public static Image Wallpaper;
    /// <summary>
    /// Widgets Container
    /// </summary>
    public static WidgetContainer WidgetsContainer;
    /// <summary>
    /// Right Clicked
    /// </summary>
    public static bool RightClicked;
    /// <summary>
    /// FConsole
    /// </summary>
    public static FConsole FConsole;
    /// <summary>
    /// Right Menu
    /// </summary>
    public static RightMenu RightMenu;
    /// <summary>
    /// Perf Widget
    /// </summary>
    public static PerformanceWidget PerfWidget;
    /// <summary>
    /// Widget Context Menu
    /// </summary>
    public static WidgetContextMenu widgetContextMenu;
    #endregion
    #region "private variables"
    /// <summary>
    /// Cusor
    /// </summary>
    private static Image Cursor;
    /// <summary>
    /// Cursor Moving
    /// </summary>
    private static Image CursorMoving;
    /// <summary>
    /// Cursor Busy
    /// </summary>
    private static Image CursorBusy;
    /// <summary>
    /// Cached Document Icon
    /// </summary>
    private static Image _cachedDocumentIcon;
    /// <summary>
    /// Cached Folder Icon
    /// </summary>
    private static Image _cachedFolderIcon;
    /// <summary>
    /// Cached Image Icon
    /// </summary>
    private static Image _cachedImageIcon;
    /// <summary>
    /// Cached Audio Icon
    /// </summary>
    private static Image _cachedAudioIcon;
    /// <summary>
    /// Cached Icon Size
    /// </summary>
    private static int _cachedIconSize = 48;
    /// <summary>
    /// UEFI direct renderer toggle (debug-only)
    /// </summary>
    private static bool _useUefiDirectRenderer = false;
    /// <summary>
    /// Last Icon Cache Refresh
    /// </summary>
    private static ulong _lastIconCacheRefresh = 0;
    /// <summary>
    /// UEFI-safe polled input state
    /// </summary>
    private static bool _uefiInputInitialized = false;
    private static bool _uefiKeyboardExtended = false;
    private static bool _uefiKeyboardBreakPending = false;
    private static bool _uefiKeyboardSet2 = false;
    private static int _uefiMousePhase = 0;
    private static byte _uefiMouseB0 = 0;
    private static byte _uefiMouseB1 = 0;
    private static int _uefiCursorLastX = -1;
    private static int _uefiCursorLastY = -1;
    private static bool _uefiCursorEverDrawn = false;
    private static bool _uefiKeyboardSeen = false;
    private static bool _uefiMouseSeen = false;
    private static bool _uefiAnyInputByteSeen = false;
    private static bool _uefiSerialSeen = false;
    private static bool _uefiUnknownKeySeen = false;

    private sealed class SafeModeDiagnostics {
        public ulong FrameCounter;
        public ulong TimerCounter;
        public ulong InputPollCounter;
        public ulong KeyboardEventCounter;
        public ulong MouseEventCounter;
        public ulong InputPollFaultCounter;
        public ulong InputPollTimeoutCounter;
        public ulong WatchdogSkipCounter;
        public ulong LastPollDurationTicks;
        public ulong MaxPollDurationTicks;
        public ulong LastInputStatusCode;
        public string LastInputResult;
        public string LastInputPhase;
        public string InputPollStatus;
        public string KeyboardStatus;
        public string MouseStatus;
        public string UsbHidStatus;
        public string Ps2Status;
        public string UefiInputStatus;
        public string TimerStatus;
        public string RenderStatus;
        public string ReadyStatus;
        public bool KeyboardAvailable;
        public bool MouseAvailable;
        public bool TimerTickAvailable;
        public bool RenderingAlive;
        public bool InputUsingPs2Path;
        public bool InputUsingUefiBootServices;
        public bool UsbHidImplemented;
    }

    private static readonly SafeModeDiagnostics _safeModeDiagnostics = new SafeModeDiagnostics();

    private static void ResetSafeModeDiagnostics() {
        _safeModeDiagnostics.FrameCounter = 0;
        _safeModeDiagnostics.TimerCounter = 0;
        _safeModeDiagnostics.InputPollCounter = 0;
        _safeModeDiagnostics.KeyboardEventCounter = 0;
        _safeModeDiagnostics.MouseEventCounter = 0;
        _safeModeDiagnostics.InputPollFaultCounter = 0;
        _safeModeDiagnostics.InputPollTimeoutCounter = 0;
        _safeModeDiagnostics.WatchdogSkipCounter = 0;
        _safeModeDiagnostics.LastPollDurationTicks = 0;
        _safeModeDiagnostics.MaxPollDurationTicks = 0;
        _safeModeDiagnostics.LastInputStatusCode = 0;
        _safeModeDiagnostics.LastInputResult = "idle";
        _safeModeDiagnostics.LastInputPhase = "init-start";
        _safeModeDiagnostics.InputPollStatus = "INPUT POLL INIT";
        _safeModeDiagnostics.KeyboardStatus = "KEYBOARD: unavailable";
        _safeModeDiagnostics.MouseStatus = "MOUSE: unavailable";
        _safeModeDiagnostics.UsbHidStatus = "USB HID: not implemented";
        _safeModeDiagnostics.Ps2Status = "PS/2: probing";
        _safeModeDiagnostics.UefiInputStatus = "UEFI input: pre-exit only";
        _safeModeDiagnostics.TimerStatus = "TIMER: checking";
        _safeModeDiagnostics.RenderStatus = "RENDER LOOP ACTIVE";
        _safeModeDiagnostics.ReadyStatus = "SAFE MODE READY";
        _safeModeDiagnostics.KeyboardAvailable = false;
        _safeModeDiagnostics.MouseAvailable = false;
        _safeModeDiagnostics.TimerTickAvailable = false;
        _safeModeDiagnostics.RenderingAlive = false;
        _safeModeDiagnostics.InputUsingPs2Path = true;
        _safeModeDiagnostics.InputUsingUefiBootServices = false;
        _safeModeDiagnostics.UsbHidImplemented = false;
    }

    private static void SetSafeModeInputPhase(string phase) {
        if (phase != null) {
            _safeModeDiagnostics.LastInputPhase = phase;
        }
    }

    private static void SetSafeModeInputResult(string result, ulong statusCode) {
        if (result != null) {
            _safeModeDiagnostics.LastInputResult = result;
        }
        _safeModeDiagnostics.LastInputStatusCode = statusCode;
    }

    private static ulong GetSafeTimerTicks() {
        try {
            return Timer.Ticks;
        } catch {
            return 0;
        }
    }

    private static ulong GetSafeCycleCounter() {
        try {
            return Native.Rdtsc();
        } catch {
            return 0;
        }
    }

    private const ulong SafeModeInputPollTimeoutCycles = 12000000;
    private const int SafeModeInputByteBudget = 32;
    private const int SafeModePs2WaitBudget = 512;

    private static void UpdateSafeModeTimerState() {
        ulong ticks = GetSafeTimerTicks();
        _safeModeDiagnostics.TimerCounter = ticks;
        _safeModeDiagnostics.TimerTickAvailable = ticks != 0;
        _safeModeDiagnostics.TimerStatus = ticks != 0 ? "TIMER TICKING" : "TIMER STALLED ZERO";
    }

    private static void UpdateSafeModeRenderDiagnostics(int frameCounter) {
        _safeModeDiagnostics.FrameCounter = (ulong)frameCounter;
        _safeModeDiagnostics.RenderingAlive = true;
        _safeModeDiagnostics.RenderStatus = "RENDER LOOP ACTIVE";
        _safeModeDiagnostics.ReadyStatus = frameCounter >= 4 ? "SAFE MODE READY" : "SAFE MODE STARTING";
        UpdateSafeModeTimerState();
    }

    private static bool HasSafeModeInputTimedOut(ulong startCycles) {
        if (startCycles == 0) return false;
        ulong currentCycles = GetSafeCycleCounter();
        if (currentCycles == 0 || currentCycles < startCycles) return false;
        return (currentCycles - startCycles) > SafeModeInputPollTimeoutCycles;
    }

    private static void FinalizeSafeModeInputPoll(ulong startCycles, string statusText) {
        ulong endCycles = GetSafeCycleCounter();
        if (startCycles != 0 && endCycles >= startCycles) {
            ulong duration = endCycles - startCycles;
            _safeModeDiagnostics.LastPollDurationTicks = duration;
            if (duration > _safeModeDiagnostics.MaxPollDurationTicks) {
                _safeModeDiagnostics.MaxPollDurationTicks = duration;
            }
        }

        if (statusText != null) {
            _safeModeDiagnostics.InputPollStatus = statusText;
        }

        _safeModeDiagnostics.KeyboardStatus = _safeModeDiagnostics.KeyboardAvailable
            ? "KEYBOARD READY"
            : "KEYBOARD UNAVAILABLE";
        _safeModeDiagnostics.MouseStatus = _safeModeDiagnostics.MouseAvailable
            ? "MOUSE READY"
            : "MOUSE UNAVAILABLE";
        _safeModeDiagnostics.UsbHidStatus = _safeModeDiagnostics.UsbHidImplemented
            ? "USB HID ACTIVE"
            : "USB HID NOT IMPLEMENTED";
        _safeModeDiagnostics.Ps2Status = _safeModeDiagnostics.InputUsingPs2Path
            ? (_uefiInputInitialized ? "PS2 POLLING" : "PS2 PROBING")
            : "PS2 DISABLED";
        _safeModeDiagnostics.UefiInputStatus = _safeModeDiagnostics.InputUsingUefiBootServices
            ? "UEFI INPUT PRE EXIT"
            : "UEFI INPUT PRE EXIT ONLY";
    }
    #endregion
    /// <summary>
    /// USB Mouse Test
    /// </summary>
    /// <returns></returns>
    private static bool USBMouseTest() {
        HID.GetMouse(HID.Mouse, out _, out _, out var Buttons);
        return Buttons != MouseButtons.None;
    }
    /// <summary>
    /// USB Keyboard Test
    /// </summary>
    /// <returns></returns>
    private static bool USBKeyboardTest() {
        HID.GetKeyboard(HID.Keyboard, out var ScanCode, out _);
        return ScanCode != 0;
    }
    /// <summary>
    /// Test function to verify function calls work
    /// </summary>
    private static void TestFunction() {
        Native.Out8(0x3F8, (byte)'T');
        Native.Out8(0x3F8, (byte)'E');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'T');
        Native.Out8(0x3F8, (byte)'\n');
    }

    /// <summary>
    /// Load a PNG image using PngLoader (UEFI-safe).
    /// Returns null on any failure (no exceptions, no hangs).
    /// </summary>
    private static Image LoadPngSafe(string path) {
        byte[] data = null;
        try { data = File.ReadAllBytes(path); } catch { return null; }
        if (data == null || data.Length < 33) return null;
        Image result;
        if (PngLoader.Load(data, out result) && result != null)
            return result;
        return null;
    }

    /// <summary>
    /// Create a procedural arrow cursor (16x16).
    /// Used in UEFI mode where PNG decoding is unsafe.
    /// </summary>
    private static Image CreateFallbackCursor() {
        var img = new Image(16, 16);
        if (img == null || img.RawData == null) return img;
        for (int i = 0; i < 16 * 16; i++) {
            img.RawData[i] = 0;
        }
        for (int y = 0; y < 16; y++) {
            for (int x = 0; x < 16; x++) {
                if (y < 12 && x < 8 && x <= y && x < (12 - y)) {
                    img.RawData[y * 16 + x] = unchecked((int)0xFFFFFFFF);
                } else if (y < 13 && x < 9 && (x == y + 1 || x == (11 - y) || (y == 11 && x <= 7))) {
                    img.RawData[y * 16 + x] = unchecked((int)0xFF000000);
                }
            }
        }
        return img;
    }

    /// <summary>
    /// KMain
    /// </summary>
    public static void KMain() {
        string customCharset = null;
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            // In UEFI mode, disable debug lines to prevent graphical corruption
            BootConsole.DrawDebugLines = false;
            BootConsole.WriteLine("[BOOT_MODE] UEFI");
            BootConsole.WriteLine("[MOUSE_CAPABILITIES] INITIALIZE");
            // UEFI mode: mark uefi=true and disable PS/2 fallback.
            MouseCapabilityDetector.DetectAndInitialize(null, true, false);
            // Raw serial marker to confirm we returned from DetectAndInitialize
            Native.Out8(0x3F8, (byte)'M');
            Native.Out8(0x3F8, (byte)'C');
            Native.Out8(0x3F8, (byte)'D');
            Native.Out8(0x3F8, (byte)'O');
            Native.Out8(0x3F8, (byte)'K');
            Native.Out8(0x3F8, (byte)'\n');
            BootConsole.WriteLine("[MOUSE_CAPABILITIES] DEVICE CHOSEN: " + MouseCapabilityDetector.GetCapabilityName(MouseCapabilityDetector.PrimaryCapability));
            //try {
            BootConsole.WriteLine("[INPUT] INITIALIZE");
            Keyboard.Initialize();
            //BootConsole.WriteLine("[INPUT] Subscribing Kbd2Mouse");
            //Keyboard.OnKeyChanged += (sender, key) => Kbd2Mouse.OnKeyChanged(key);
            //} catch {
                //BootConsole.WriteLine("[INPUT] Keyboard Dispatcher Init Failed");
            //}

            // UEFI: do not attempt to initialize PS/2 keyboard hardware.
            // PS/2 controller/IRQ code is legacy-oriented and can stall in UEFI-only environments.
            BootConsole.WriteLine("[INPUT] Skipping PS/2 keyboard init (UEFI)");

            BootConsole.WriteLine("[USB] INIT");
            // USB stack is optional in UEFI mode - EHCI may panic if BAR0 is unmapped
            // Skip EHCI entirely in UEFI mode to avoid accessing unmapped MMIO addresses
            // that would cause page faults (uncatchable as C# exceptions)
            try {
                Hub.Initialize();
                HID.Initialize();
                // EHCI accesses PCI BAR0 MMIO which may not be identity-mapped in UEFI mode.
                // A page fault here is fatal (not catchable). Skip EHCI in UEFI.
                BootConsole.WriteLine("[USB] Skipping EHCI in UEFI mode (unmapped MMIO risk)");
                // USB.StartPolling(); // Skip polling without EHCI

                // After USB init, check if USB HID mouse is available
                MouseCapabilityDetector.CheckUsbHidMouse();
                BootConsole.WriteLine("[USB] Initialization sequence complete");
            } catch {
                BootConsole.WriteLine("[USB] Init failed (non-fatal, continuing)");
            }
            // Log final mouse detection result
            BootConsole.WriteLine("[INPUT] Mouse detection complete");
            BootConsole.WriteLine("[INPUT] Mouse enabled: " + MouseCapabilityDetector.MouseEnabled);

            // CRITICAL: Re-mount filesystem early.
            // In UEFI mode, ALL managed static references (File.Instance, Disk.Instance,
            // Ramdisk.Instance) get zeroed between EntryPoint.KMain and Program.KMain.
            // However, Ramdisk.RawBasePointer (a static byte*) survives because it's a
            // value type, not a managed reference. Use it to reconstruct everything.
            BootConsole.WriteLine("[FS] Re-mounting filesystem for UEFI");
            BootConsole.WriteLine("[FS] RawBasePointer = " + ((ulong)Ramdisk.RawBasePointer).ToString("x"));
            if (Ramdisk.RawBasePointer != null) {
                try {
                    // Reconstruct Ramdisk from surviving raw pointer
                    if (Disk.Instance == null) {
                        BootConsole.WriteLine("[FS] Reconstructing Ramdisk from RawBasePointer");
                        new Ramdisk((System.IntPtr)Ramdisk.RawBasePointer);
                        BootConsole.WriteLine("[FS] Ramdisk reconstructed");
                    }
                    // Now mount RdskFS (uses RawBasePointer directly)
                    File.Instance = new RdskFS();
                    BootConsole.WriteLine("[FS] Filesystem re-mounted OK");
                } catch {
                    BootConsole.WriteLine("[FS] Filesystem re-mount FAILED");
                }
            } else {
                BootConsole.WriteLine("[FS] FATAL: RawBasePointer is NULL - no ramdisk!");
            }

            BootConsole.WriteLine("[CURSOR] Creating cursor images");
            // Debug: Verify we're still in UEFI mode
            Native.Out8(0x3F8, (byte)'[');
            Native.Out8(0x3F8, (byte)'M');
            Native.Out8(0x3F8, (byte)'O');
            Native.Out8(0x3F8, (byte)'D');
            Native.Out8(0x3F8, (byte)'E');
            Native.Out8(0x3F8, (byte)'=');
            byte modeVal = (byte)(BootConsole.CurrentMode == guideXOS.BootMode.UEFI ? 'U' : 'L');
            Native.Out8(0x3F8, modeVal);
            Native.Out8(0x3F8, (byte)']');
            Native.Out8(0x3F8, (byte)'\n');
            // UEFI: Skip all PNG decoding for cursors.
            // Both LodePNG (managed port) and PngLoader hang during DEFLATE
            // decompression in the post-ExitBootServices environment, blocking
            // the entire GUI from initialising. Use procedural cursors instead.
            BootConsole.WriteLine("[CURSOR] About to call CreateFallbackCursor()");
            Cursor = CreateFallbackCursor();
            BootConsole.WriteLine("[CURSOR] CreateFallbackCursor() returned, assigning to CursorMoving");
            CursorMoving = Cursor;
            BootConsole.WriteLine("[CURSOR] CursorMoving assigned, assigning to CursorBusy");
            CursorBusy = Cursor;
            BootConsole.WriteLine("[CURSOR] Cursors created (procedural fallback)");
            BootConsole.WriteLine("[WM] INIT");
            try {
                WindowManager.Initialize();
                BootConsole.WriteLine("[WM] INIT complete");
            } catch {
                BootConsole.WriteLine("[WM] INIT FAILED!");
            }
            
            BootConsole.WriteLine("[DESKTOP] INIT");
            try {
                Desktop.Initialize();
                BootConsole.WriteLine("[DESKTOP] INIT complete");
            } catch {
                BootConsole.WriteLine("[DESKTOP] INIT FAILED!");
            }
            
            BootConsole.WriteLine("[KMAIN] Calling SMain");

            // UEFI: Skip BitFont file loading here — File.Instance is unreliable at this
            // stage (managed reference corruption). WindowManager.Initialize() already
            // creates a dummy IFont fallback that the taskbar can use.
            // BitFont from ramdisk can be loaded later in SMainSetupUefi after
            // File.Instance is re-mounted from the surviving Disk.Instance.

            // CRITICAL: Stop BootConsole from painting over framebuffer before entering GUI
            // From this point forward, all logging goes to serial only
            BootConsole.DrawDebugLines = false;
            
            SMain();
        } else if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            // RAW serial marker to prove we entered Legacy path
            Native.Out8(0x3F8, (byte)'[');
            Native.Out8(0x3F8, (byte)'L');
            Native.Out8(0x3F8, (byte)'E');
            Native.Out8(0x3F8, (byte)'G');
            Native.Out8(0x3F8, (byte)'A');
            Native.Out8(0x3F8, (byte)'C');
            Native.Out8(0x3F8, (byte)'Y');
            Native.Out8(0x3F8, (byte)']');
            Native.Out8(0x3F8, (byte)'\n');
            BootConsole.DrawDebugLines = true;

            BootConsole.WriteLine("[BOOT_MODE] LEGACY");
            Animator.Initialize();
            // Initialize legacy PS/2 input first so VirtualBox (default PS/2 devices) works out-of-the-box.
            // This provides keyboard IRQ1 (0x21) and mouse IRQ12 (0x2C) handling even without USB HID.
            try { PS2Keyboard.Initialize(); } catch { }
            try { PS2Mouse.Initialize(); PS2Mouse.EnableFullProcessing(); } catch { }
            // Initialize VMware absolute pointer backdoor if present (no-op on other hypervisors)
            try { VMwareTools.Initialize(); } catch { }
#if USBDebug
        Hub.Initialize();
        HID.Initialize();
        EHCI.Initialize();
        //USB.StartPolling();

        //Use qemu for USB debug
        //VMware won't connect virtual USB HIDs
        /*
        if (HID.Mouse == null)
        {
            Console.WriteLine("USB Mouse not present");
        }
        if (HID.Keyboard == null)
        {
            Console.WriteLine("USB Keyboard not present");
        }

        for(; ; )
        {
            if (HID.Mouse != null)
            {
                HID.GetMouseThings(HID.Mouse, out sbyte AxisX, out sbyte AxisY, out var Buttons);
                if (AxisX != 0 && AxisY != 0)
                {
                    Console.WriteLine($"X:{AxisX} Y:{AxisY}");
                }
            }
            if(HID.Keyboard != null) 
            {
                HID.GetKeyboard(HID.Keyboard, out var ScanCode, out var Key);
                if(ScanCode != 0)
                {
                    Console.WriteLine($"ScanCode:{ScanCode}");
                }
            }
        }
#else
            try {
                Hub.Initialize();
                HID.Initialize();
                EHCI.Initialize();
                USB.StartPolling();
            } catch { /* USB stack is optional; continue boot */ }

            try {
                /*
                if (HID.Mouse == null) {
                    Console.WriteLine("USB Mouse not present");
                }
                if (HID.Keyboard == null) {
                    Console.WriteLine("USB Keyboard not present");
                }
                */
            } catch { }
#endif
            //Sized width to 512
            try { Cursor = new PNG(File.ReadAllBytes("Images/Cursor.png")); } catch { Cursor = new Image(16, 16); }
            try { CursorMoving = new PNG(File.ReadAllBytes("Images/Grab.png")); } catch { CursorMoving = Cursor; }
            try { CursorBusy = new PNG(File.ReadAllBytes("Images/Busy.png")); } catch { CursorBusy = Cursor; }
            //try { Wallpaper = new PNG(File.ReadAllBytes("Images/tronporche.png")); } catch { Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height); }
            BitFont.Initialize();
            // FIXED: Added leading space to charset to match font image layout
            customCharset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            BitFont.RegisterBitFont(new BitFontDescriptor("Enludo", customCharset, File.ReadAllBytes("Fonts/enludo.btf"), 16));
            //Terminal = null;
            WindowManager.Initialize();
            Desktop.Initialize();
            Firewall.Initialize();
            Audio.Initialize();
            AC97.Initialize();
            if (AC97.DeviceLocated) Console.WriteLine("Device Located: " + AC97.DeviceName);
            ES1371.Initialize();
#if NETWORK
            Console.WriteLine("[NET] Initializing network subsystem...");
            try {
                NETv4.Initialize();
                Intel825xx.Initialize();
                RTL8111.Initialize();
            } catch {
                Console.WriteLine("[NET] Network driver initialization error");
            }

            // Only try DHCP if a network driver was found
            if (NETv4.Sender != null) {
                Console.WriteLine("[NET] Network driver found");
                Console.WriteLine("[NET] Skipping automatic DHCP (use 'netinit' command in console)");
                // Skip DHCP during boot to prevent hanging
                // User can run 'netinit' command in FConsole to configure network manually
            } else {
                Console.WriteLine("[NET] No network hardware detected");
            }
#endif
            // Apply saved display mode before wallpaper resize
            DisplayManager.ApplySavedResolution();
            // Load saved configuration (UI settings, window positions, recent files, etc.)
            guideXOS.OS.Configuration.LoadConfiguration();
            SMain();
        }
    }
#if NETWORK
    private static void Client_OnData(byte[] data) {
        for (int i = 0; i < data.Length; i++) {
            Console.Write((char)data[i]);
        }
        BootConsole.WriteLine(" ");
    }
    public static byte[] ToASCII(string s) {
        byte[] buffer = new byte[s.Length];
        for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)s[i];
        return buffer;
    }
#endif



    public static void SMain() {
        // CRITICAL: RAW serial output FIRST to prove we entered SMain
        // Use direct port I/O to bypass any potential issues with BootConsole
        Native.Out8(0x3F8, (byte)'[');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'M');
        Native.Out8(0x3F8, (byte)'A');
        Native.Out8(0x3F8, (byte)'I');
        Native.Out8(0x3F8, (byte)'N');
        Native.Out8(0x3F8, (byte)']');
        Native.Out8(0x3F8, (byte)'\n');

        // Run setup in a separate method to keep SMain's stack frame minimal
        SMainSetup();

        BootConsole.WriteLine("[SMAIN] Setup complete - entering main loop");

        // Add global Escape key handler to close active (topmost visible) window
        // Skip in UEFI mode (Keyboard not initialized)
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            SetupEscapeKeyHandler();
        }

        BootConsole.WriteLine("[SMAIN] Entering main render loop");

        // Enter the main render loop (in a separate method to keep stack frames small)
        RenderLoop();
    }

    /// <summary>
    /// Setup phase for SMain - extracted to keep SMain's stack frame small.
    /// NativeAOT allocates stack space for ALL local variables in a function's
    /// prologue. A giant function causes a giant sub rsp,N that can overflow
    /// the 512 KB UEFI stack.
    /// </summary>
    private static void SMainSetup() {
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            SMainSetupUefi();
        } else {
            SMainSetupLegacy();
        }

        FConsole = null; // Don't create console here - let it be created on-demand

        // Initialize icons
        SetupIcons();

        // UEFI: Skip BitFont file loading — File.Instance's managed vtable is
        // corrupted even after re-mount (FS Type shows garbage pointer).
        // File.ReadAllBytes will hang (infinite loop in corrupted dispatch).
        // WindowManager.Initialize() already creates a dummy IFont fallback.

        // Context menus
        SetupContextMenus();

        // Widgets
        SetupWidgets();
    }

    /// <summary>
    /// UEFI-specific setup (framebuffer test, wallpaper, triple buffering)
    /// </summary>
    private static void SMainSetupUefi() {
        BootConsole.WriteLine("[SMAIN] UEFI MODE");
        Framebuffer.RecoverUefiState();
        BootConsole.WriteLine("[SMAIN] UEFI mode - disabling triple buffering");
        Framebuffer.TripleBuffered = false;
        // CRITICAL: In UEFI mode, static managed object references (like Framebuffer.Graphics
        // and File.Instance) get zeroed between EntryPoint.KMain and Program.KMain.
        // Value-type fields (VideoMemory pointer, Width, Height) survive.
        // OriginalVideoMemory is the authoritative framebuffer address.
        BootConsole.WriteLine("[SMAIN] Framebuffer.Graphics is " + (Framebuffer.Graphics == null ? "NULL" : "OK"));
        BootConsole.WriteLine("[SMAIN] Framebuffer.VideoMemory = " + ((ulong)Framebuffer.VideoMemory).ToString("x"));
        BootConsole.WriteLine("[SMAIN] Framebuffer.OriginalVideoMemory = " + ((ulong)Framebuffer.OriginalVideoMemory).ToString("x"));
        // If VideoMemory got corrupted, restore from OriginalVideoMemory before EnsureGraphics
        if ((ulong)Framebuffer.OriginalVideoMemory != 0 && (ulong)Framebuffer.VideoMemory != (ulong)Framebuffer.OriginalVideoMemory) {
            BootConsole.WriteLine("[SMAIN] WARNING: VideoMemory corrupted! Restoring from OriginalVideoMemory");
            Framebuffer.VideoMemory = Framebuffer.OriginalVideoMemory;
        }
        Framebuffer.EnsureGraphics();
        BootConsole.WriteLine("[SMAIN] After EnsureGraphics: " + (Framebuffer.Graphics == null ? "NULL" : "OK"));
        if (Framebuffer.Graphics != null) {
            BootConsole.WriteLine("[SMAIN] Graphics.VideoMemory = " + ((ulong)Framebuffer.Graphics.VideoMemory).ToString("x"));
            BootConsole.WriteLine("[SMAIN] EnsureGraphics OK");
        } else {
            BootConsole.WriteLine("[SMAIN] ERROR: EnsureGraphics failed!");
        }
        // Create teal gradient wallpaper for UEFI desktop
        BootConsole.WriteLine("[SMAIN] Creating UEFI wallpaper");
        try {
            Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height);
            if (Wallpaper != null && Wallpaper.RawData != null) {
                uint topColor = 0xFF5FD4C4;
                uint bottomColor = 0xFF0D7D77;
                int topR = (int)((topColor >> 16) & 0xFF);
                int topG = (int)((topColor >> 8) & 0xFF);
                int topB = (int)(topColor & 0xFF);
                int bottomR = (int)((bottomColor >> 16) & 0xFF);
                int bottomG = (int)((bottomColor >> 8) & 0xFF);
                int bottomB = (int)(bottomColor & 0xFF);
                for (int y = 0; y < Framebuffer.Height; y++) {
                    int t256 = (y * 256) / Framebuffer.Height;
                    int r = topR + ((bottomR - topR) * t256) / 256;
                    int g = topG + ((bottomG - topG) * t256) / 256;
                    int b = topB + ((bottomB - topB) * t256) / 256;
                    int color = unchecked((int)(0xFF000000 | (uint)(r << 16) | (uint)(g << 8) | (uint)b));
                    int rowBase = y * Framebuffer.Width;
                    for (int x = 0; x < Framebuffer.Width; x++) {
                        Wallpaper.RawData[rowBase + x] = color;
                    }
                }
                BootConsole.WriteLine("[SMAIN] Wallpaper gradient created");
            }
        } catch {
            BootConsole.WriteLine("[SMAIN] Wallpaper creation failed - using null");
            Wallpaper = null;
        }
        BootConsole.WriteLine("[SMAIN] Wallpaper created");
        BootConsole.WriteLine("[SAFE_MODE] UEFI minimal desktop initialized");
    }

    /// <summary>
    /// Legacy-specific setup (wallpaper creation, triple buffering)
    /// </summary>
    private static void SMainSetupLegacy() {
        BootConsole.WriteLine("[SMAIN] Starting desktop rendering");
        Framebuffer.TripleBuffered = true;

        BootConsole.WriteLine("[SMAIN] Creating wallpaper");
        Image wall = Wallpaper;
        try {
            BootConsole.WriteLine("[SMAIN] Checking existing wallpaper");
            if (wall != null) {
                BootConsole.WriteLine("[SMAIN] Resizing existing wallpaper");
                Wallpaper = wall.ResizeImage(Framebuffer.Width, Framebuffer.Height);
                wall.Dispose();
            } else {
                BootConsole.WriteLine("[SMAIN] Creating default gradient wallpaper");
                BootConsole.WriteLine("[SMAIN] FB size: " + Framebuffer.Width.ToString() + "x" + Framebuffer.Height.ToString());
                Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height);
                if (Wallpaper == null) {
                    BootConsole.WriteLine("[SMAIN] ERROR: Wallpaper allocation returned null!");
                } else if (Wallpaper.RawData == null) {
                    BootConsole.WriteLine("[SMAIN] ERROR: Wallpaper.RawData is null!");
                } else {
                    BootConsole.WriteLine("[SMAIN] Image allocated, drawing gradient...");
                    int totalPixels = Framebuffer.Width * Framebuffer.Height;
                    int tealColor = unchecked((int)0xFF0D7D77);
                    for (int i = 0; i < totalPixels; i++) {
                        Wallpaper.RawData[i] = tealColor;
                    }
                    BootConsole.WriteLine("[SMAIN] Solid color fill complete");
                }
            }
        } catch {
            BootConsole.WriteLine("[SMAIN] Wallpaper creation exception - using solid color");
            try {
                Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height);
                if (Wallpaper != null && Wallpaper.RawData != null) {
                    int tealColor = unchecked((int)0xFF0D7D77);
                    for (int i = 0; i < Framebuffer.Width * Framebuffer.Height; i++) {
                        Wallpaper.RawData[i] = tealColor;
                    }
                }
            } catch {
                BootConsole.WriteLine("[SMAIN] Fallback wallpaper also failed!");
            }
        }
        BootConsole.WriteLine("[SMAIN] Wallpaper created");
    }

    /// <summary>
    /// Initialize desktop icons (shared by both boot modes)
    /// </summary>
    private static void SetupIcons() {
        BootConsole.WriteLine("[SMAIN] Setting up icons");
        BootConsole.WriteLine("[SMAIN] File.Instance = " + (File.Instance == null ? "NULL" : "OK"));
        BootConsole.WriteLine("[SMAIN] Disk.Instance = " + (Disk.Instance == null ? "NULL" : "OK"));
        // Re-mount if needed (should already be done in KMain, but safety check)
        if (File.Instance == null && Ramdisk.RawBasePointer != null) {
            BootConsole.WriteLine("[SMAIN] Re-mounting filesystem from RawBasePointer");
            try {
                if (Disk.Instance == null) {
                    new Ramdisk((System.IntPtr)Ramdisk.RawBasePointer);
                }
                File.Instance = new RdskFS();
                BootConsole.WriteLine("[SMAIN] Filesystem re-mounted OK");
            } catch {
                BootConsole.WriteLine("[SMAIN] Filesystem re-mount failed");
            }
        }
        if (File.Instance != null) {
            BootConsole.WriteLine("[SMAIN] Initializing background manager and icons");
            if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                BackgroundRotationManager.Initialize();
                guideXOS.Modules.ModuleManager.InitializeBuiltins();
                try {
                    RefreshCachedIcons();
                    _lastIconCacheRefresh = Timer.Ticks;
                    BootConsole.WriteLine("[SMAIN] Icons initialized");
                } catch {
                    BootConsole.WriteLine("[SMAIN] Icon initialization failed - using fallback");
                    _cachedDocumentIcon = new Image(48, 48);
                    _cachedFolderIcon = new Image(48, 48);
                    _cachedImageIcon = new Image(48, 48);
                    _cachedAudioIcon = new Image(48, 48);
                }
            } else {
                BootConsole.WriteLine("[SMAIN] UEFI mode - using fallback icons (no PNG)");
                _cachedDocumentIcon = new Image(48, 48);
                _cachedFolderIcon = new Image(48, 48);
                _cachedImageIcon = new Image(48, 48);
                _cachedAudioIcon = new Image(48, 48);
            }
        } else {
            BootConsole.WriteLine("[SMAIN] No filesystem - using fallback icons");
            _cachedDocumentIcon = new Image(48, 48);
            _cachedFolderIcon = new Image(48, 48);
            _cachedImageIcon = new Image(48, 48);
            _cachedAudioIcon = new Image(48, 48);
        }
    }

    /// <summary>
    /// Initialize context menus
    /// </summary>
    private static void SetupContextMenus() {
        BootConsole.WriteLine("[SMAIN] Creating context menus");
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            if (RightMenu == null) {
                RightMenu = new RightMenu();
                RightMenu.Visible = false;
            }
            widgetContextMenu = new WidgetContextMenu();
            widgetContextMenu.Visible = false;
            WindowManager.MoveToEnd(widgetContextMenu);
            BootConsole.WriteLine("[SMAIN] Context menus created (Legacy)");
        } else {
            BootConsole.WriteLine("[SMAIN] Context menus skipped (UEFI mode)");
            RightMenu = null;
            widgetContextMenu = null;
        }
    }

    /// <summary>
    /// Initialize widgets (Legacy only)
    /// </summary>
    private static void SetupWidgets() {
        BootConsole.WriteLine("[SMAIN] Creating widgets");
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            PerfWidget = new PerformanceWidget();
            PerfWidget.Visible = false;
            WindowManager.MoveToEnd(PerfWidget);

            var clockWidget = new guideXOS.DockableWidgets.Clock(
                PerfWidget.X,
                PerfWidget.Y + PerfWidget.Height + 10
            );
            clockWidget.Visible = false;
            WindowManager.MoveToEnd(clockWidget);

            var monitorWidget = new guideXOS.DockableWidgets.Monitor();
            monitorWidget.Visible = false;
            WindowManager.MoveToEnd(monitorWidget);

            var uptimeWidget = new guideXOS.DockableWidgets.Uptime(
                PerfWidget.X,
                PerfWidget.Y + PerfWidget.Height + clockWidget.PreferredHeight + 20
            );
            uptimeWidget.Visible = false;
            WindowManager.MoveToEnd(uptimeWidget);

            var widgetContainer = new WidgetContainer(
                Framebuffer.Width - 220,
                80
            );
            widgetContainer.AddWidget(PerfWidget);
            widgetContainer.AddWidget(clockWidget);
            widgetContainer.AddWidget(monitorWidget);
            widgetContainer.AddWidget(uptimeWidget);
            widgetContainer.Visible = UISettings.ShowWidgetsOnStartup;
            WindowManager.MoveToEnd(widgetContainer);

            Program.WidgetsContainer = widgetContainer;

            if (!UISettings.ShowWidgetsOnStartup) {
                var toggle = new WidgetToggleButton(Framebuffer.Width - 26, 6);
                WindowManager.MoveToEnd(toggle);
                toggle.Visible = true;
            }
            BootConsole.WriteLine("[SMAIN] Widgets created (Legacy)");
        } else {
            BootConsole.WriteLine("[SMAIN] Widgets skipped (UEFI mode)");
            PerfWidget = null;
            widgetContextMenu = null;
            Program.WidgetsContainer = null;
        }
    }

    /// <summary>
    /// Setup global Escape key handler (Legacy only)
    /// </summary>
    private static void SetupEscapeKeyHandler() {
        Keyboard.OnKeyChanged += (sender, key) => {
            try {
                if (key.Key == System.ConsoleKey.Escape && key.KeyState == System.ConsoleKeyState.Pressed) {
                    if (Desktop.Taskbar != null && Desktop.Taskbar.IsWorkspaceSwitcherVisible) {
                        Desktop.Taskbar.CloseWorkspaceSwitcher();
                        return;
                    }
                    for (int i = WindowManager.Windows.Count - 1; i >= 0; i--) {
                        var window = WindowManager.Windows[i];
                        if (window.Visible && !window.IsTombstoned) {
                            window.OnGlobalKey(key);
                            break;
                        }
                    }
                }
            } catch {
                // Ignore errors in global key handler to prevent crashes
            }
        };
    }

    /// <summary>
    /// Writes a single ASCII char to COM1 (serial port 0x3F8) for bare-metal debug.
    /// </summary>
    private static void SerialChar(char c) {
        Native.Out8(0x3F8, (byte)c);
    }

    /// <summary>
    /// Main render loop - extracted from SMain to keep stack frames small
    /// </summary>
    private static void RenderLoop() {
        int lastMouseX = Control.MousePosition.X;
        int lastMouseY = Control.MousePosition.Y;
        ulong lastMoveTick = Timer.Ticks;
        const ulong ActiveMoveMs = 100;
        int frameCounter = 0;
        bool isUefi = (BootConsole.CurrentMode == guideXOS.BootMode.UEFI);

        if (isUefi) {
            ResetSafeModeDiagnostics();
            SetSafeModeInputPhase("INIT-OK");
            SetSafeModeInputResult("READY", 0);
        }

        for (; ; ) {
            try {
                frameCounter++;

                if (isUefi) {
                    UpdateSafeModeRenderDiagnostics(frameCounter);
                }

                // In UEFI mode, optionally use simplified direct framebuffer rendering (debug-only)
                if (isUefi && _useUefiDirectRenderer) {
                    RenderFrameUefiDirect(frameCounter);
                    Thread.Sleep(16);
                    continue;
                }

                // Emit per-step serial markers for first 3 frames in UEFI mode
                // Format: [F<n>:<step>] where step is a single letter
                bool debugFrame = isUefi && (frameCounter <= 3);


                if (debugFrame) { SerialChar('['); SerialChar('F'); SerialChar((char)('0' + (frameCounter % 10))); SerialChar(':'); SerialChar('A'); SerialChar(']'); } // A = start

                bool shouldLog = (frameCounter == 1);

                // CRITICAL: Ensure Graphics object exists and points at the real framebuffer.
                // In UEFI mode, this is only safe during the first GUI frame; repeated
                // managed-reference repair can trip over post-ExitBootServices state.
                if (!isUefi || frameCounter == 1) {
                    Framebuffer.EnsureGraphics();

                    // UEFI safety: validate Graphics.VideoMemory points at the real framebuffer
                    // If OriginalVideoMemory is set, verify Graphics uses it (not a heap address)
                    if (isUefi && (ulong)Framebuffer.OriginalVideoMemory != 0) {
                        if (Framebuffer.Graphics != null && (ulong)Framebuffer.Graphics.VideoMemory != (ulong)Framebuffer.OriginalVideoMemory) {
                            // Graphics pointer was corrupted - fix it
                            Framebuffer.Graphics.VideoMemory = Framebuffer.OriginalVideoMemory;
                            Framebuffer.VideoMemory = Framebuffer.OriginalVideoMemory;
                        }
                    }
                }

                if (debugFrame) SerialChar('B'); // B = EnsureGraphics done

                if (shouldLog) {
                    // Use serial-only output to avoid BootConsole painting over the framebuffer
                    SerialChar('F'); SerialChar('1'); SerialChar('\n');
                }

                // Periodically refresh cached icons (skip in UEFI for now)
                if (!isUefi && UISettings.EnableDesktopIconCacheRefresh && File.Instance != null) {
                    ulong intervalMs = (ulong)UISettings.DesktopIconCacheRefreshIntervalMinutes * 60000UL;
                    if (Timer.Ticks - _lastIconCacheRefresh >= intervalMs) {
                        RefreshCachedIcons();
                        _lastIconCacheRefresh = Timer.Ticks;
                    }
                }

                // Update background rotation manager - Legacy only
                if (!isUefi) {
                    try {
                        BackgroundRotationManager.Update();
                    } catch { }
                }

                if (debugFrame) SerialChar('C'); // C = pre-mouse

                // Poll mouse input
                try {
                    MouseEventDispatcher.Update();
                } catch { }

                if (isUefi) {
                    PollUefiInput();
                }

                if (debugFrame) SerialChar('D'); // D = post-mouse

                // Per-frame input pass
                WindowManager.MouseHandled = false;
                try {
                    WindowManager.InputAll();
                } catch { }

                if (debugFrame) SerialChar('E'); // E = post-input

                // FlushPendingCreates - Legacy only
                if (!isUefi) {
                    try { WindowManager.FlushPendingCreates(); } catch { }
                }

                // Audio - Legacy only
                if (!isUefi) {
                    try { WAVPlayer.DoPlay(); } catch { }
                }

                if (debugFrame) SerialChar('F'); // F = pre-clear

                // UEFI Frame 1 diagnostics: Print framebuffer state
                if (isUefi && frameCounter == 1) {
                    SerialChar('['); SerialChar('F'); SerialChar('B'); SerialChar(':');
                    // Print Width
                    int w = Framebuffer.Width;
                    SerialChar((char)('0' + (w / 1000) % 10));
                    SerialChar((char)('0' + (w / 100) % 10));
                    SerialChar((char)('0' + (w / 10) % 10));
                    SerialChar((char)('0' + w % 10));
                    SerialChar('x');
                    // Print Height
                    int h = Framebuffer.Height;
                    SerialChar((char)('0' + (h / 1000) % 10));
                    SerialChar((char)('0' + (h / 100) % 10));
                    SerialChar((char)('0' + (h / 10) % 10));
                    SerialChar((char)('0' + h % 10));
                    SerialChar(']');
                }

                // Clear screen. In UEFI mode the GOP framebuffer is uncached MMIO;
                // full-frame rep stosd is prohibitively slow and can stall boot.
                if (!isUefi) {
                    try {
                        uint* vm = Framebuffer.Graphics.VideoMemory;
                        Native.Stosd(vm, 0x00000000, (ulong)(Framebuffer.Width * Framebuffer.Height));
                    } catch {
                        if (debugFrame) { SerialChar('!'); SerialChar('F'); SerialChar('\n'); }
                        Thread.Sleep(1);
                        continue;
                    }
                }

                if (debugFrame) SerialChar('G'); // G = post-clear, pre-background

                //draw background
                try {
                    if (!isUefi) {
                        BackgroundRotationManager.DrawBackground();
                    } else {
                        // UEFI: avoid full-screen MMIO blits during bring-up.
                        // The boot splash remains as the background while UI elements render.
                    }
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('G'); }
                }

                if (debugFrame) SerialChar('H'); // H = post-background

                if (isUefi && frameCounter == 1) {
                    SerialChar('U'); SerialChar('I');
                    DrawUefiReadyDesktop();
                    SerialChar('D'); SerialChar('\n');
                }

                // Context menu - skip in UEFI (RightMenu is null)
                if (!isUefi) {
                    try {
                        if ((Control.MouseButtons & MouseButtons.Right) == MouseButtons.Right && !RightClicked && !WindowManager.MouseHandled) {
                            RightClicked = true;
                            if (RightMenu != null) {
                                RightMenu.X = Control.MousePosition.X;
                                RightMenu.Y = Control.MousePosition.Y;
                                WindowManager.MoveToEnd(RightMenu);
                                RightMenu.Visible = true;
                            }
                        } else if ((Control.MouseButtons & MouseButtons.Right) != MouseButtons.Right) {
                            RightClicked = false;
                        }
                    } catch {
                        RightClicked = false;
                    }
                }

                if (debugFrame) SerialChar('I'); // I = pre-desktop

                // Draw desktop icons and taskbar
                try {
                    Desktop.Update(
                        _cachedDocumentIcon,
                        _cachedFolderIcon,
                        _cachedImageIcon,
                        _cachedAudioIcon,
                        48
                    );
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('I'); }
                }

                if (debugFrame) SerialChar('J'); // J = pre-windows

                // Draw windows
                try {
                    WindowManager.DrawAllExceptTaskManager();
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('J'); }
                }

                try {
                    if (Desktop.Taskbar != null) {
                        Desktop.Taskbar.DrawWorkspaceSwitcher();
                    }
                } catch { }

                try {
                    WindowManager.DrawTaskManager();
                } catch { }

                try {
                    WindowManager.CleanupClosedWindows();
                } catch { }

                if (isUefi) {
                    DrawUefiSafeModeDiagnostics();
                }

                if (debugFrame) SerialChar('K'); // K = pre-cursor

                // Draw cursor. UEFI uses a direct framebuffer cursor because the managed
                // cursor image path still depends on legacy graphics assumptions.
                if (!isUefi) {
                    try {
                        var img = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left ? CursorMoving : Cursor;
                        if (img != null && img.RawData != null) {
                            Framebuffer.Graphics.DrawImage(Control.MousePosition.X, Control.MousePosition.Y, img);
                        } else {
                            if (debugFrame) { SerialChar('!'); SerialChar('K'); }
                        }
                    } catch {
                        if (debugFrame) { SerialChar('x'); SerialChar('K'); }
                    }
                } else {
                    DrawUefiCursor();
                }

                if (debugFrame) SerialChar('L'); // L = pre-update

                //refresh screen
                try {
                    Framebuffer.Update();
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('L'); SerialChar('\n'); }
                    Thread.Sleep(1);
                    continue;
                }

                if (debugFrame) { SerialChar('Z'); SerialChar('\n'); } // Z = frame complete

                if (isUefi && frameCounter == 4) {
                    _safeModeDiagnostics.ReadyStatus = "SAFE MODE READY";
                    BootConsole.WriteLine("[SAFE_MODE] READY");
                    SerialChar('R'); SerialChar('E'); SerialChar('A'); SerialChar('D'); SerialChar('Y'); SerialChar('\n');
                }

                if (isUefi) {
                    DrawUefiHeartbeat(frameCounter);
                    if ((frameCounter & 0xFFFF) == 0) {
                        SerialChar('H'); SerialChar('B'); SerialChar('\n');
                    }
                }

                // Mouse responsiveness throttling
                int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
                if (mx != lastMouseX || my != lastMouseY) {
                    lastMouseX = mx; lastMouseY = my; lastMoveTick = Timer.Ticks;
                    Thread.Sleep(0);
                } else {
                    ulong age = (Timer.Ticks >= lastMoveTick) ? (Timer.Ticks - lastMoveTick) : 0UL;
                    if (age < ActiveMoveMs) Thread.Sleep(0); else Thread.Sleep(1);
                }
            } catch {
                if (isUefi && frameCounter <= 3) {
                    SerialChar('!'); SerialChar('!'); SerialChar('!'); SerialChar('\n');
                }
                Thread.Sleep(10);
            }
        }
    }

    /// <summary>
    /// Draw UEFI background (teal gradient) directly to framebuffer
    /// </summary>
    private static void DrawUefiBackground() {
        uint* fb = Framebuffer.Graphics.VideoMemory;
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;
        for (int y = 0; y < fbH; y++) {
            int t = (y * 128) / fbH;
            uint color = (uint)(0xFF0D7D77 + (t << 16) + (t << 8) + t);
            for (int x = 0; x < fbW; x++) {
                fb[y * fbW + x] = color;
            }
        }
    }
    /// <summary>
    /// One-time UEFI ready screen drawn directly to GOP memory.
    /// </summary>
    private static void DrawUefiReadyDesktop() {
        uint* fb = GetUefiFramebuffer();
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;
        if (fb == null || fbW <= 0 || fbH <= 0) return;

        for (int y = 0; y < fbH; y++) {
            int shade = (y * 64) / fbH;
            uint color = (uint)(0xFF102030 + (shade << 16) + (shade << 8));
            for (int x = 0; x < fbW; x++) {
                fb[y * fbW + x] = color;
            }
        }

        DrawUefiFillRect(fb, fbW, fbH, 0, 0, fbW, 58, 0xFF182836u);
        DrawUefiFillRect(fb, fbW, fbH, 0, fbH - 44, fbW, 44, 0xFF151A22u);
        DrawUefiFillRect(fb, fbW, fbH, 0, 58, fbW, 2, 0xFF36C2B4u);
        DrawUefiFillRect(fb, fbW, fbH, 0, fbH - 46, fbW, 2, 0xFF36C2B4u);

        DrawUefiTinyText(fb, fbW, fbH, 24, 18, "GUIDEXOS SAFE MODE", 3, 0xFFFFFFFFu);
        DrawUefiTinyText(fb, fbW, fbH, 24, 82, "RAMDISK OK", 2, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, 24, 110, _safeModeDiagnostics.RenderStatus, 2, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, 24, 138, _safeModeDiagnostics.InputPollStatus, 2, 0xFFCDEEEAu);

        int iconY = fbH > 260 ? 210 : 150;
        DrawUefiLauncher(fb, fbW, fbH, 36, iconY, 0xFF47D6C8u, "FILES");
        DrawUefiLauncher(fb, fbW, fbH, 132, iconY, 0xFFFFD166u, "APPS");
        DrawUefiLauncher(fb, fbW, fbH, 228, iconY, 0xFFFF6B6Bu, "SETUP");

        DrawUefiFillRect(fb, fbW, fbH, 18, fbH - 34, 92, 24, 0xFF263241u);
        DrawUefiTinyText(fb, fbW, fbH, 30, fbH - 28, "START", 2, 0xFFFFFFFFu);
        DrawUefiTinyText(fb, fbW, fbH, fbW - 212, fbH - 28, _safeModeDiagnostics.ReadyStatus, 2, 0xFFCDEEEAu);
    }

    private static void DrawUefiSafeModeDiagnostics() {
        uint* fb = GetUefiFramebuffer();
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;
        if (fb == null || fbW <= 0 || fbH <= 0) return;

        int x = fbW > 820 ? fbW - 372 : 24;
        int y = 82;
        int w = fbW > 820 ? 340 : fbW - 48;
        int h = 222;
        if (w < 180) return;

        DrawUefiFillRect(fb, fbW, fbH, x, y, w, h, 0xCC1A2430u);
        DrawUefiFillRect(fb, fbW, fbH, x, y, w, 2, 0xFF36C2B4u);

        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 10, "SAFE MODE DIAGNOSTICS", 1, 0xFFFFFFFFu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 28, "FRAME " + _safeModeDiagnostics.FrameCounter.ToString(), 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 42, "TICK " + _safeModeDiagnostics.TimerCounter.ToString(), 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 56, "POLL " + _safeModeDiagnostics.InputPollCounter.ToString(), 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 70, "KEY " + _safeModeDiagnostics.KeyboardEventCounter.ToString(), 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 84, "MOUSE " + _safeModeDiagnostics.MouseEventCounter.ToString(), 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 98, "FAULT " + _safeModeDiagnostics.InputPollFaultCounter.ToString(), 1, 0xFFFFD166u);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 112, "TIMEOUT " + _safeModeDiagnostics.InputPollTimeoutCounter.ToString(), 1, 0xFFFFD166u);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 126, "MAXPOLL " + _safeModeDiagnostics.MaxPollDurationTicks.ToString(), 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 140, "LASTPOLL " + _safeModeDiagnostics.LastPollDurationTicks.ToString(), 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 154, "CODE " + _safeModeDiagnostics.LastInputStatusCode.ToString(), 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 168, "LAST " + _safeModeDiagnostics.LastInputResult, 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 182, "PHASE " + _safeModeDiagnostics.LastInputPhase, 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 196, "WATCHDOG " + _safeModeDiagnostics.WatchdogSkipCounter.ToString(), 1, 0xFFFFD166u);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 210, _safeModeDiagnostics.TimerStatus, 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 224, _safeModeDiagnostics.KeyboardStatus, 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 238, _safeModeDiagnostics.MouseStatus, 1, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 252, _safeModeDiagnostics.UefiInputStatus, 1, 0xFFFFD166u);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 266, _safeModeDiagnostics.Ps2Status, 1, 0xFFFFD166u);
        DrawUefiTinyText(fb, fbW, fbH, x + 10, y + 280, _safeModeDiagnostics.UsbHidStatus, 1, 0xFFFFD166u);
    }

    private static uint* GetUefiFramebuffer() {
        return Framebuffer.OriginalVideoMemory != null
            ? Framebuffer.OriginalVideoMemory
            : (Framebuffer.VideoMemory != null
                ? Framebuffer.VideoMemory
                : (Framebuffer.Graphics != null ? Framebuffer.Graphics.VideoMemory : null));
    }
    private static void PollUefiInput() {
        if (!_uefiInputInitialized) {
            InitializeUefiInput();
        }

        ulong pollStart = GetSafeCycleCounter();
        _safeModeDiagnostics.InputPollCounter++;
        SetSafeModeInputPhase("POLL-ENTER");
        SetSafeModeInputResult("POLLING", 0);

        Framebuffer.RecoverUefiState();
        try {
            PollUefiSerialInput();

            for (int i = 0; i < SafeModeInputByteBudget; i++) {
                if (HasSafeModeInputTimedOut(pollStart)) {
                    _safeModeDiagnostics.InputPollTimeoutCounter++;
                    _safeModeDiagnostics.WatchdogSkipCounter++;
                    SetSafeModeInputPhase("POLL-TIMEOUT");
                    SetSafeModeInputResult("TIMEOUT", 1);
                    FinalizeSafeModeInputPoll(pollStart, "INPUT POLL TIMEOUT");
                    return;
                }

                byte status = Native.In8(0x64);
                if ((status & 0x01) == 0) {
                    SetSafeModeInputPhase("POLL-EXIT");
                    SetSafeModeInputResult("NO-DATA", 0);
                    FinalizeSafeModeInputPoll(pollStart, "INPUT POLL " + _safeModeDiagnostics.InputPollCounter.ToString());
                    return;
                }

                bool fromMouse = (status & 0x20) != 0;
                byte data = Native.In8(0x60);
                if (!_uefiAnyInputByteSeen) {
                    _uefiAnyInputByteSeen = true;
                    SerialChar('D'); SerialChar('A'); SerialChar('T'); SerialChar('\n');
                }

                if (fromMouse) {
                    ProcessUefiPs2MouseByte(data);
                } else {
                    ProcessUefiPs2KeyboardByte(data);
                }
            }

            SetSafeModeInputPhase("POLL-EXIT");
            SetSafeModeInputResult("BYTE-BUDGET", (ulong)SafeModeInputByteBudget);
            FinalizeSafeModeInputPoll(pollStart, "INPUT POLL " + _safeModeDiagnostics.InputPollCounter.ToString());
        } catch {
            _safeModeDiagnostics.InputPollFaultCounter++;
            SetSafeModeInputPhase("POLL-ERROR");
            SetSafeModeInputResult("FAULT", 2);
            FinalizeSafeModeInputPoll(pollStart, "INPUT POLL FAULT");
        }
    }

    private static void InitializeUefiInput() {
        SetSafeModeInputPhase("INIT-START");
        _uefiInputInitialized = true;
        _uefiMousePhase = 0;
        _uefiKeyboardExtended = false;
        _uefiKeyboardBreakPending = false;
        _uefiKeyboardSet2 = false;

        Framebuffer.RecoverUefiState();
        if (Framebuffer.Width > 0 && Framebuffer.Height > 0) {
            Control.MousePosition.X = Framebuffer.Width / 2;
            Control.MousePosition.Y = Framebuffer.Height / 2;
        }
        Control.MouseButtons = MouseButtons.None;

        UefiPs2Drain(64);
        bool configRead = UefiPs2ReadConfig(out byte config);
        _safeModeDiagnostics.InputUsingPs2Path = true;
        _safeModeDiagnostics.InputUsingUefiBootServices = false;
        _safeModeDiagnostics.UsbHidImplemented = false;
        if (configRead) {
            config &= 0xFC; // IRQs stay disabled; we poll instead.
            config &= unchecked((byte)~0x30); // Enable keyboard and aux clocks.
            config |= 0x40; // Translate keyboard set 2 to set 1 when supported.
            UefiPs2WriteConfig(config);
            SetSafeModeInputResult("CONFIG-OK", config);
        } else {
            SetSafeModeInputResult("CONFIG-MISS", 0x20);
        }

        if (UefiPs2WaitInputClear()) Native.Out8(0x64, 0xAE); // Enable keyboard port.
        if (UefiPs2WaitInputClear()) Native.Out8(0x64, 0xA8); // Enable auxiliary PS/2 mouse port.
        UefiPs2Drain(64);

        UefiPs2KeyboardWrite(0xF6); // Keyboard defaults.
        UefiPs2KeyboardWrite(0xF4); // Enable keyboard scanning.
        UefiPs2MouseWrite(0xF6); // Mouse defaults.
        UefiPs2MouseWrite(0xF4); // Enable mouse streaming packets.
        UefiPs2Drain(64);

        SerialChar('I'); SerialChar('N'); SerialChar('P');
        SerialChar(configRead ? 'C' : 'N');
        SerialChar('\n');
        SetSafeModeInputPhase("INIT-OK");
        FinalizeSafeModeInputPoll(0, "INPUT POLL INIT");
    }

    private static void ProcessUefiPs2KeyboardByte(byte scancode) {
        if (scancode == 0 || scancode == 0xFA || scancode == 0xFE) return;
        if (scancode == 0xE0) {
            _uefiKeyboardExtended = true;
            return;
        }
        if (scancode == 0xF0) {
            _uefiKeyboardBreakPending = true;
            _uefiKeyboardSet2 = true;
            return;
        }

        bool released;
        byte code;
        if (_uefiKeyboardBreakPending) {
            released = true;
            code = scancode;
            _uefiKeyboardBreakPending = false;
            _uefiKeyboardSet2 = true;
        } else if (_uefiKeyboardSet2) {
            released = false;
            code = scancode;
        } else {
            released = (scancode & 0x80) != 0;
            code = (byte)(scancode & 0x7F);
        }

        ConsoleKey key = MapUefiKeyboardCode(code, _uefiKeyboardExtended, _uefiKeyboardSet2);
        _uefiKeyboardExtended = false;

        if (key == ConsoleKey.None) {
            if (!_uefiUnknownKeySeen) {
                _uefiUnknownKeySeen = true;
                SerialChar('U'); SerialChar('N'); SerialChar('K'); SerialChar('\n');
            }
            return;
        }

        DispatchUefiKey(key, released ? ConsoleKeyState.Released : ConsoleKeyState.Pressed, scancode);
    }

    private static ConsoleKey MapUefiKeyboardCode(byte code, bool extended, bool set2) {
        if (set2) {
            if (extended) {
                switch (code) {
                    case 0x75: return ConsoleKey.Up;
                    case 0x72: return ConsoleKey.Down;
                    case 0x6B: return ConsoleKey.Left;
                    case 0x74: return ConsoleKey.Right;
                    case 0x5A: return ConsoleKey.Enter;
                    case 0x71: return ConsoleKey.Delete;
                }
            } else {
                switch (code) {
                    case 0x76: return ConsoleKey.Escape;
                    case 0x5A: return ConsoleKey.Enter;
                    case 0x29: return ConsoleKey.F1; // Space acts as left click.
                    case 0x05: return ConsoleKey.F1;
                    case 0x06: return ConsoleKey.F2;
                    case 0x1D: return ConsoleKey.Up;    // W
                    case 0x1C: return ConsoleKey.Left;  // A
                    case 0x1B: return ConsoleKey.Down;  // S
                    case 0x23: return ConsoleKey.Right; // D
                }
            }
        }

        if (extended) {
            switch (code) {
                case 0x48: return ConsoleKey.Up;
                case 0x50: return ConsoleKey.Down;
                case 0x4B: return ConsoleKey.Left;
                case 0x4D: return ConsoleKey.Right;
                case 0x1C: return ConsoleKey.Enter;
                case 0x53: return ConsoleKey.Delete;
            }
        } else {
            switch (code) {
                case 0x01: return ConsoleKey.Escape;
                case 0x1C: return ConsoleKey.Enter;
                case 0x39: return ConsoleKey.F1; // Space acts as left click.
                case 0x3B: return ConsoleKey.F1;
                case 0x3C: return ConsoleKey.F2;
                case 0x11: return ConsoleKey.Up;    // W
                case 0x1E: return ConsoleKey.Left;  // A
                case 0x1F: return ConsoleKey.Down;  // S
                case 0x20: return ConsoleKey.Right; // D
            }
        }

        return ConsoleKey.None;
    }
    private static void ProcessUefiPs2MouseByte(byte data) {
        if (data == 0xFA || data == 0xFE) return;

        if (_uefiMousePhase == 0) {
            if ((data & 0x08) == 0) return;
            if ((data & 0xC0) != 0) return;
            _uefiMouseB0 = data;
            _uefiMousePhase = 1;
            return;
        }
        if (_uefiMousePhase == 1) {
            _uefiMouseB1 = data;
            _uefiMousePhase = 2;
            return;
        }

        byte b2 = data;
        _uefiMousePhase = 0;

        int dx = _uefiMouseB1;
        int dy = b2;
        if ((_uefiMouseB0 & 0x10) != 0) dx -= 256;
        if ((_uefiMouseB0 & 0x20) != 0) dy -= 256;

        dx = Math.Clamp(dx, -32, 32);
        dy = Math.Clamp(dy, -32, 32);

        int maxX = Framebuffer.Width > 0 ? Framebuffer.Width - 1 : 0;
        int maxY = Framebuffer.Height > 0 ? Framebuffer.Height - 1 : 0;
        Control.MousePosition.X = Math.Clamp(Control.MousePosition.X + dx, 0, maxX);
        Control.MousePosition.Y = Math.Clamp(Control.MousePosition.Y - dy, 0, maxY);

        MouseButtons buttons = MouseButtons.None;
        if ((_uefiMouseB0 & 0x01) != 0) buttons |= MouseButtons.Left;
        if ((_uefiMouseB0 & 0x02) != 0) buttons |= MouseButtons.Right;
        if ((_uefiMouseB0 & 0x04) != 0) buttons |= MouseButtons.Middle;
        if (!_uefiMouseSeen && (dx != 0 || dy != 0 || buttons != MouseButtons.None)) {
            _uefiMouseSeen = true;
            SerialChar('M'); SerialChar('O'); SerialChar('U'); SerialChar('\n');
        }
        _safeModeDiagnostics.MouseAvailable = true;
        _safeModeDiagnostics.MouseEventCounter++;
        SetSafeModeInputPhase("MOUSE-EVENT");
        SetSafeModeInputResult("MOUSE", (ulong)_uefiMouseB0);
        Control.MouseButtons = buttons;
    }

    private static bool UefiPs2WaitInputClear() {
        for (int i = 0; i < SafeModePs2WaitBudget; i++) {
            if ((Native.In8(0x64) & 0x02) == 0) return true;
            Native.Nop();
        }
        _safeModeDiagnostics.InputPollTimeoutCounter++;
        SetSafeModeInputResult("WAIT-TIMEOUT", 0x64);
        return false;
    }

    private static void PollUefiSerialInput() {
        for (int i = 0; i < 8; i++) {
            if ((Native.In8(0x3FD) & 0x01) == 0) return;
            byte b = Native.In8(0x3F8);
            ConsoleKey key = ConsoleKey.None;
            switch (b) {
                case (byte)'w': case (byte)'W': key = ConsoleKey.Up; break;
                case (byte)'s': case (byte)'S': key = ConsoleKey.Down; break;
                case (byte)'a': case (byte)'A': key = ConsoleKey.Left; break;
                case (byte)'d': case (byte)'D': key = ConsoleKey.Right; break;
                case (byte)' ': case (byte)'j': case (byte)'J': key = ConsoleKey.F1; break;
                case (byte)'k': case (byte)'K': key = ConsoleKey.F2; break;
            }
            if (key == ConsoleKey.None) continue;
            if (!_uefiSerialSeen) {
                _uefiSerialSeen = true;
                SerialChar('S'); SerialChar('E'); SerialChar('R'); SerialChar('\n');
            }
            SetSafeModeInputPhase("KEY-EVENT");
            SetSafeModeInputResult("SERIAL", b);
            DispatchUefiKey(key, ConsoleKeyState.Pressed, b);
            DispatchUefiKey(key, ConsoleKeyState.Released, b);
        }
    }

    private static void DispatchUefiKey(ConsoleKey key, ConsoleKeyState state, byte scanCode) {
        var info = new ConsoleKeyInfo {
            Key = key,
            KeyChar = '\0',
            Modifiers = ConsoleModifiers.None,
            KeyState = state,
            ScanCode = scanCode
        };
        if (!_uefiKeyboardSeen) {
            _uefiKeyboardSeen = true;
            SerialChar('K'); SerialChar('E'); SerialChar('Y'); SerialChar('\n');
        }
        _safeModeDiagnostics.KeyboardAvailable = true;
        if (state == ConsoleKeyState.Pressed) {
            _safeModeDiagnostics.KeyboardEventCounter++;
        }
        SetSafeModeInputPhase("KEY-EVENT");
        SetSafeModeInputResult("KEY", scanCode);
        Keyboard.KeyInfo = info;
        Keyboard.InvokeOnKeyChanged(info);
        Kbd2Mouse.OnKeyChanged(info);
    }

    private static bool UefiPs2ReadConfig(out byte config) {
        config = 0;
        if (!UefiPs2WaitInputClear()) return false;
        Native.Out8(0x64, 0x20);
        for (int i = 0; i < SafeModePs2WaitBudget; i++) {
            if ((Native.In8(0x64) & 0x01) != 0) {
                config = Native.In8(0x60);
                return true;
            }
            Native.Nop();
        }
        return false;
    }

    private static void UefiPs2WriteConfig(byte config) {
        if (!UefiPs2WaitInputClear()) return;
        Native.Out8(0x64, 0x60);
        if (!UefiPs2WaitInputClear()) return;
        Native.Out8(0x60, config);
    }

    private static void UefiPs2KeyboardWrite(byte value) {
        if (!UefiPs2WaitInputClear()) return;
        Native.Out8(0x60, value);
        UefiPs2ReadAndDiscardAck(false);
    }

    private static void UefiPs2ReadAndDiscardAck(bool requireMouseSource) {
        for (int i = 0; i < SafeModePs2WaitBudget; i++) {
            byte status = Native.In8(0x64);
            if ((status & 0x01) != 0) {
                if (!requireMouseSource || (status & 0x20) != 0) {
                    Native.In8(0x60);
                    return;
                }
                Native.In8(0x60);
                return;
            }
            Native.Nop();
        }
    }
    private static void UefiPs2MouseWrite(byte value) {
        if (!UefiPs2WaitInputClear()) return;
        Native.Out8(0x64, 0xD4);
        if (!UefiPs2WaitInputClear()) return;
        Native.Out8(0x60, value);

        for (int i = 0; i < SafeModePs2WaitBudget; i++) {
            byte status = Native.In8(0x64);
            if ((status & 0x01) != 0) {
                Native.In8(0x60);
                return;
            }
            Native.Nop();
        }
    }

    private static void UefiPs2Drain(int maxBytes) {
        for (int i = 0; i < maxBytes; i++) {
            byte status = Native.In8(0x64);
            if ((status & 0x01) == 0) return;
            Native.In8(0x60);
        }
    }

    private static void DrawUefiCursor() {
        Framebuffer.RecoverUefiState();
        uint* fb = GetUefiFramebuffer();
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;
        if (fb == null || fbW <= 0 || fbH <= 0) return;

        if (_uefiCursorEverDrawn) {
            RestoreUefiCursorArea(fb, fbW, fbH, _uefiCursorLastX, _uefiCursorLastY);
        }

        int x = Math.Clamp(Control.MousePosition.X, 0, fbW - 1);
        int y = Math.Clamp(Control.MousePosition.Y, 0, fbH - 1);
        bool pressed = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;
        uint fill = pressed ? 0xFFFFD166u : 0xFFFFFFFFu;
        uint edge = 0xFF000000u;

        for (int yy = 0; yy < 18; yy++) {
            for (int xx = 0; xx < 13; xx++) {
                bool outline = xx == 0 || yy == 0 || xx == yy || (yy > 8 && xx == 5) || (yy > 8 && yy < 15 && xx == 6);
                bool inside = xx < yy && xx < 9;
                if (!outline && !inside) continue;
                int px = x + xx;
                int py = y + yy;
                if ((uint)px >= (uint)fbW || (uint)py >= (uint)fbH) continue;
                fb[py * fbW + px] = outline ? edge : fill;
            }
        }

        _uefiCursorLastX = x;
        _uefiCursorLastY = y;
        _uefiCursorEverDrawn = true;
    }

    private static void RestoreUefiCursorArea(uint* fb, int fbW, int fbH, int x, int y) {
        for (int yy = y; yy < y + 20; yy++) {
            if ((uint)yy >= (uint)fbH) continue;
            int shade = (yy * 64) / fbH;
            uint color = (uint)(0xFF102030 + (shade << 16) + (shade << 8));
            if (yy < 58) color = 0xFF182836u;
            if (yy >= fbH - 44) color = 0xFF151A22u;
            if ((yy >= 58 && yy < 60) || (yy >= fbH - 46 && yy < fbH - 44)) color = 0xFF36C2B4u;

            int row = yy * fbW;
            for (int xx = x; xx < x + 16; xx++) {
                if ((uint)xx >= (uint)fbW) continue;
                fb[row + xx] = color;
            }
        }
    }

    private static bool UefiCursorAreaTouchesUi(int x, int y, int fbW, int fbH) {
        int iconY = fbH > 260 ? 210 : 150;
        if (y < 170) return true;
        if (y + 20 >= fbH - 50) return true;
        if (x < 310 && y + 20 >= iconY && y <= iconY + 92) return true;
        return false;
    }
    private static void DrawUefiLauncher(uint* fb, int fbW, int fbH, int x, int y, uint color, string label) {
        DrawUefiFillRect(fb, fbW, fbH, x, y, 58, 58, 0xFF203040u);
        DrawUefiFillRect(fb, fbW, fbH, x + 8, y + 8, 42, 42, color);
        DrawUefiFillRect(fb, fbW, fbH, x + 14, y + 18, 30, 4, 0xCCFFFFFFu);
        DrawUefiFillRect(fb, fbW, fbH, x + 14, y + 30, 30, 4, 0xCCFFFFFFu);
        DrawUefiTinyText(fb, fbW, fbH, x, y + 66, label, 2, 0xFFFFFFFFu);
    }

    private static void DrawUefiFillRect(uint* fb, int fbW, int fbH, int x, int y, int w, int h, uint color) {
        int x0 = x < 0 ? 0 : x;
        int y0 = y < 0 ? 0 : y;
        int x1 = x + w;
        int y1 = y + h;
        if (x1 > fbW) x1 = fbW;
        if (y1 > fbH) y1 = fbH;
        if (x1 <= x0 || y1 <= y0) return;
        for (int yy = y0; yy < y1; yy++) {
            int row = yy * fbW;
            for (int xx = x0; xx < x1; xx++) {
                fb[row + xx] = color;
            }
        }
    }

    private static void DrawUefiTinyText(uint* fb, int fbW, int fbH, int x, int y, string text, int scale, uint color) {
        int cursorX = x;
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (c == ' ') {
                cursorX += 4 * scale;
                continue;
            }
            DrawUefiGlyph(fb, fbW, fbH, cursorX, y, c, scale, color);
            cursorX += 6 * scale;
        }
    }

    private static void DrawUefiGlyph(uint* fb, int fbW, int fbH, int x, int y, char c, int scale, uint color) {
        for (int row = 0; row < 7; row++) {
            byte bits = UefiGlyphRow(c, row);
            for (int col = 0; col < 5; col++) {
                if ((bits & (1 << (4 - col))) == 0) continue;
                DrawUefiFillRect(fb, fbW, fbH, x + col * scale, y + row * scale, scale, scale, color);
            }
        }
    }

    private static byte UefiGlyphRow(char c, int row) {
        switch (c) {
            case '-': return row == 3 ? (byte)0x0E : (byte)0x00;
            case '0': return row == 0 ? (byte)0x0E : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x13 : row == 3 ? (byte)0x15 : row == 4 ? (byte)0x19 : row == 5 ? (byte)0x11 : (byte)0x0E;
            case '1': return row == 0 ? (byte)0x04 : row == 1 ? (byte)0x0C : row == 2 ? (byte)0x04 : row == 3 ? (byte)0x04 : row == 4 ? (byte)0x04 : row == 5 ? (byte)0x04 : (byte)0x0E;
            case '2': return row == 0 ? (byte)0x0E : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x01 : row == 3 ? (byte)0x02 : row == 4 ? (byte)0x04 : row == 5 ? (byte)0x08 : (byte)0x1F;
            case '3': return row == 0 ? (byte)0x1E : row == 1 ? (byte)0x01 : row == 2 ? (byte)0x01 : row == 3 ? (byte)0x0E : row == 4 ? (byte)0x01 : row == 5 ? (byte)0x01 : (byte)0x1E;
            case '4': return row == 0 ? (byte)0x02 : row == 1 ? (byte)0x06 : row == 2 ? (byte)0x0A : row == 3 ? (byte)0x12 : row == 4 ? (byte)0x1F : row == 5 ? (byte)0x02 : (byte)0x02;
            case '5': return row == 0 ? (byte)0x1F : row == 1 ? (byte)0x10 : row == 2 ? (byte)0x10 : row == 3 ? (byte)0x1E : row == 4 ? (byte)0x01 : row == 5 ? (byte)0x01 : (byte)0x1E;
            case '6': return row == 0 ? (byte)0x06 : row == 1 ? (byte)0x08 : row == 2 ? (byte)0x10 : row == 3 ? (byte)0x1E : row == 4 ? (byte)0x11 : row == 5 ? (byte)0x11 : (byte)0x0E;
            case '7': return row == 0 ? (byte)0x1F : row == 1 ? (byte)0x01 : row == 2 ? (byte)0x02 : row == 3 ? (byte)0x04 : row == 4 ? (byte)0x08 : row == 5 ? (byte)0x08 : (byte)0x08;
            case '8': return row == 0 ? (byte)0x0E : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x11 : row == 3 ? (byte)0x0E : row == 4 ? (byte)0x11 : row == 5 ? (byte)0x11 : (byte)0x0E;
            case '9': return row == 0 ? (byte)0x0E : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x11 : row == 3 ? (byte)0x0F : row == 4 ? (byte)0x01 : row == 5 ? (byte)0x02 : (byte)0x1C;
            case 'A': return row == 0 ? (byte)0x0E : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x11 : row == 3 ? (byte)0x1F : row == 4 ? (byte)0x11 : row == 5 ? (byte)0x11 : (byte)0x11;
            case 'B': return row == 0 ? (byte)0x1E : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x11 : row == 3 ? (byte)0x1E : row == 4 ? (byte)0x11 : row == 5 ? (byte)0x11 : (byte)0x1E;
            case 'C': return row == 0 ? (byte)0x0F : row == 1 ? (byte)0x10 : row == 2 ? (byte)0x10 : row == 3 ? (byte)0x10 : row == 4 ? (byte)0x10 : row == 5 ? (byte)0x10 : (byte)0x0F;
            case 'D': return row == 0 ? (byte)0x1E : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x11 : row == 3 ? (byte)0x11 : row == 4 ? (byte)0x11 : row == 5 ? (byte)0x11 : (byte)0x1E;
            case 'E': return row == 0 ? (byte)0x1F : row == 1 ? (byte)0x10 : row == 2 ? (byte)0x10 : row == 3 ? (byte)0x1E : row == 4 ? (byte)0x10 : row == 5 ? (byte)0x10 : (byte)0x1F;
            case 'F': return row == 0 ? (byte)0x1F : row == 1 ? (byte)0x10 : row == 2 ? (byte)0x10 : row == 3 ? (byte)0x1E : row == 4 ? (byte)0x10 : row == 5 ? (byte)0x10 : (byte)0x10;
            case 'G': return row == 0 ? (byte)0x0F : row == 1 ? (byte)0x10 : row == 2 ? (byte)0x10 : row == 3 ? (byte)0x17 : row == 4 ? (byte)0x11 : row == 5 ? (byte)0x11 : (byte)0x0F;
            case 'I': return row == 0 ? (byte)0x1F : row == 1 ? (byte)0x04 : row == 2 ? (byte)0x04 : row == 3 ? (byte)0x04 : row == 4 ? (byte)0x04 : row == 5 ? (byte)0x04 : (byte)0x1F;
            case 'K': return row == 0 ? (byte)0x11 : row == 1 ? (byte)0x12 : row == 2 ? (byte)0x14 : row == 3 ? (byte)0x18 : row == 4 ? (byte)0x14 : row == 5 ? (byte)0x12 : (byte)0x11;
            case 'L': return row == 0 ? (byte)0x10 : row == 1 ? (byte)0x10 : row == 2 ? (byte)0x10 : row == 3 ? (byte)0x10 : row == 4 ? (byte)0x10 : row == 5 ? (byte)0x10 : (byte)0x1F;
            case 'M': return row == 0 ? (byte)0x11 : row == 1 ? (byte)0x1B : row == 2 ? (byte)0x15 : row == 3 ? (byte)0x15 : row == 4 ? (byte)0x11 : row == 5 ? (byte)0x11 : (byte)0x11;
            case 'N': return row == 0 ? (byte)0x11 : row == 1 ? (byte)0x19 : row == 2 ? (byte)0x15 : row == 3 ? (byte)0x13 : row == 4 ? (byte)0x11 : row == 5 ? (byte)0x11 : (byte)0x11;
            case 'O': return row == 0 ? (byte)0x0E : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x11 : row == 3 ? (byte)0x11 : row == 4 ? (byte)0x11 : row == 5 ? (byte)0x11 : (byte)0x0E;
            case 'P': return row == 0 ? (byte)0x1E : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x11 : row == 3 ? (byte)0x1E : row == 4 ? (byte)0x10 : row == 5 ? (byte)0x10 : (byte)0x10;
            case 'R': return row == 0 ? (byte)0x1E : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x11 : row == 3 ? (byte)0x1E : row == 4 ? (byte)0x14 : row == 5 ? (byte)0x12 : (byte)0x11;
            case 'S': return row == 0 ? (byte)0x0F : row == 1 ? (byte)0x10 : row == 2 ? (byte)0x10 : row == 3 ? (byte)0x0E : row == 4 ? (byte)0x01 : row == 5 ? (byte)0x01 : (byte)0x1E;
            case 'T': return row == 0 ? (byte)0x1F : row == 1 ? (byte)0x04 : row == 2 ? (byte)0x04 : row == 3 ? (byte)0x04 : row == 4 ? (byte)0x04 : row == 5 ? (byte)0x04 : (byte)0x04;
            case 'U': return row == 0 ? (byte)0x11 : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x11 : row == 3 ? (byte)0x11 : row == 4 ? (byte)0x11 : row == 5 ? (byte)0x11 : (byte)0x0E;
            case 'V': return row == 0 ? (byte)0x11 : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x11 : row == 3 ? (byte)0x11 : row == 4 ? (byte)0x0A : row == 5 ? (byte)0x0A : (byte)0x04;
            case 'X': return row == 0 ? (byte)0x11 : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x0A : row == 3 ? (byte)0x04 : row == 4 ? (byte)0x0A : row == 5 ? (byte)0x11 : (byte)0x11;
            case 'Y': return row == 0 ? (byte)0x11 : row == 1 ? (byte)0x11 : row == 2 ? (byte)0x0A : row == 3 ? (byte)0x04 : row == 4 ? (byte)0x04 : row == 5 ? (byte)0x04 : (byte)0x04;
            default: return 0;
        }
    }
    /// <summary>
    /// Tiny UEFI proof-of-life marker drawn directly to GOP memory.
    /// </summary>
    private static void DrawUefiHeartbeat(int frameCounter) {
        uint* fb = Framebuffer.OriginalVideoMemory != null
            ? Framebuffer.OriginalVideoMemory
            : (Framebuffer.VideoMemory != null
                ? Framebuffer.VideoMemory
                : (Framebuffer.Graphics != null ? Framebuffer.Graphics.VideoMemory : null));
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;

        if (fb == null || fbW <= 0 || fbH <= 0) {
            return;
        }

        uint color = ((frameCounter / 30) & 1) == 0 ? 0xFFFFFFFFu : 0xFFFF3040u;
        int maxY = fbH < 24 ? fbH : 24;
        int maxX = fbW < 24 ? fbW : 24;

        for (int y = 8; y < maxY; y++) {
            int row = y * fbW;
            for (int x = 8; x < maxX; x++) {
                fb[row + x] = color;
            }
        }
    }

    /// <summary>
    /// Debug-only UEFI direct framebuffer renderer (renders test pattern + cursor)
    /// </summary>
    private static void RenderFrameUefiDirect(int frameCounter) {
        if (frameCounter <= 5) {
            Native.Out8(0x3F8, (byte)'F');
            Native.Out8(0x3F8, (byte)'R');
            Native.Out8(0x3F8, (byte)('0' + (frameCounter % 10)));
            Native.Out8(0x3F8, (byte)'\n');
        }

        uint* fb = Framebuffer.Graphics != null ? Framebuffer.Graphics.VideoMemory : Framebuffer.VideoMemory;
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;

        if (fb == null || fbW <= 0 || fbH <= 0) {
            if (frameCounter == 1) {
                BootConsole.WriteLine("[SMAIN] ERROR: Invalid framebuffer!");
            }
            Thread.Sleep(10);
            return;
        }

        uint teal = 0xFF0D7D77;
        uint white = 0xFFFFFFFF;
        uint red = 0xFFFF0000;
        uint blue = 0xFF0000FF;

        int totalPixels = fbW * fbH;
        for (int i = 0; i < totalPixels; i++) {
            fb[i] = teal;
        }

        // White border
        for (int y = 0; y < 10 && y < fbH; y++)
            for (int x = 0; x < fbW; x++)
                fb[y * fbW + x] = white;
        for (int y = fbH - 10; y < fbH && y >= 0; y++)
            for (int x = 0; x < fbW; x++)
                fb[y * fbW + x] = white;
        for (int y = 0; y < fbH; y++)
            for (int x = 0; x < 10 && x < fbW; x++)
                fb[y * fbW + x] = white;
        for (int y = 0; y < fbH; y++)
            for (int x = fbW - 10; x < fbW && x >= 0; x++)
                fb[y * fbW + x] = white;

        // Center square
        int centerX = fbW / 2 - 50;
        int centerY = fbH / 2 - 50;
        for (int dy = 0; dy < 100 && (centerY + dy) < fbH; dy++)
            for (int dx = 0; dx < 100 && (centerX + dx) < fbW; dx++)
                fb[(centerY + dy) * fbW + (centerX + dx)] = blue;

        // Cursor
        int cx = Control.MousePosition.X;
        int cy = Control.MousePosition.Y;
        for (int dy = 0; dy < 16; dy++) {
            int py = cy + dy;
            if (py < 0 || py >= fbH) continue;
            for (int dx = 0; dx < 16; dx++) {
                int px = cx + dx;
                if (px < 0 || px >= fbW) continue;
                if (dy < 12 && dx < 8 && dx <= dy && dx < (12 - dy)) {
                    fb[py * fbW + px] = white;
                }
            }
        }

        // Heartbeat
        uint heartbeat = (frameCounter % 60 < 30) ? white : red;
        for (int dy = 20; dy < 40 && dy < fbH; dy++)
            for (int dx = 20; dx < 40 && dx < fbW; dx++)
                fb[dy * fbW + dx] = heartbeat;

        try {
            MouseEventDispatcher.Update();
        } catch { }
    }

    /// <summary>
    /// FIXED: Helper method to refresh cached icons and dispose old ones
    /// CRITICAL: Create new icons BEFORE disposing old ones to prevent Desktop.Update from receiving null/disposed icons
    /// </summary>
    private static void RefreshCachedIcons() {
        // Works in both Legacy and UEFI modes now (with managed PNG decoder)
        try {
            // STEP 1: Create new icons first
            Image newDocumentIcon = Icons.DocumentIcon(_cachedIconSize);
            Image newFolderIcon = Icons.FolderIcon(_cachedIconSize);
            Image newImageIcon = Icons.ImageIcon(_cachedIconSize);
            Image newAudioIcon = Icons.AudioIcon(_cachedIconSize);

            // STEP 2: Save old icons for disposal
            Image oldDocumentIcon = _cachedDocumentIcon;
            Image oldFolderIcon = _cachedFolderIcon;
            Image oldImageIcon = _cachedImageIcon;
            Image oldAudioIcon = _cachedAudioIcon;

            // STEP 3: Atomically swap to new icons (prevents Desktop.Update from seeing null)
            _cachedDocumentIcon = newDocumentIcon;
            _cachedFolderIcon = newFolderIcon;
            _cachedImageIcon = newImageIcon;
            _cachedAudioIcon = newAudioIcon;

            // STEP 4: Now safely dispose old icons (after swap is complete)
            if (oldDocumentIcon != null) oldDocumentIcon.Dispose();
            if (oldFolderIcon != null) oldFolderIcon.Dispose();
            if (oldImageIcon != null) oldImageIcon.Dispose();
            if (oldAudioIcon != null) oldAudioIcon.Dispose();
        } catch {
            // If icon creation fails, keep using old icons rather than having null icons
            BootConsole.WriteLine("Icon cache refresh failed - keeping old icons");
        }
    }
}
