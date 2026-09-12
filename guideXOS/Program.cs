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
    private static ulong _lastIconCacheRefresh = 0;

    private static ulong GetUefiTimerTicks() {
        try {
            return Timer.Ticks;
        } catch {
            return 0;
        }
    }

    // Bounded UEFI desktop regression controls. The runner temporarily patches
    // these constants for a fresh test build and restores the source afterward.
    private const bool UEFI_ENABLE_UTINY_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME = true;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED = false;
    private const int UEFI_NORMAL_DESKTOP_BOUNDED_FRAME_TARGET = 0;

    // Deliberate phase boundary: the recovered normal desktop is still bounded
    // until sustained operation is explicitly validated in the next phase.
    // This is not a rendering workaround or a known graphics defect.
    private const bool UEFI_ENABLE_CONTINUOUS_DESKTOP = false;

    private static bool IsUefiMode =>
        BootConsole.CurrentMode == guideXOS.BootMode.UEFI;

    private static bool UseUefiNormalDesktopFirstFrame() {
        return IsUefiMode && UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME;
    }

    private static bool UseUefiNormalDesktopBoundedMode() {
        return IsUefiMode && UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED;
    }

    // USB/PS2 input remains outside this recovery pass. All UEFI paths skip
    // post-EBS input initialization until that subsystem is validated separately.
    internal static bool ShouldSkipEarlyUefiHardwareInitialization() {
        return IsUefiMode;
    }

    internal static bool ShouldSkipEarlyUefiInputInitialization() {
        return IsUefiMode;
    }

    internal static bool IsUefiMultiFrameActive() {
        return _uefiMultiFrameActive;
    }

    internal static int UefiMultiFrameCurrentFrame => _uefiMultiFrameCurrentFrame;
    internal static int UefiMultiFrameStage => _uefiMultiFrameStage;
    internal static int UefiMultiFrameSubstage => _uefiMultiFrameSubstage;
    internal static int UefiMultiFrameLastBoundary => _uefiMultiFrameLastBoundary;
    internal static ulong UefiMultiFrameStackLowWater => _uefiMultiFrameStackLowWater;
    internal static ulong UefiMultiFrameLastCodeAddress => _uefiMultiFrameLastCodeAddress;

    internal static void LogUefiMultiFrameFaultContext() {
        SerialBreadcrumb("UEFI_FRAME_FAULT_CONTEXT");
        SerialBreadcrumb("UEFI_FRAME_CURRENT=" + _uefiMultiFrameCurrentFrame.ToString());
        SerialBreadcrumb("UEFI_FRAME_LAST_COMPLETED=" + _uefiMultiFrameLastCompletedFrame.ToString());
        SerialBreadcrumb("UEFI_FRAME_STAGE=" + _uefiMultiFrameStage.ToString());
        SerialBreadcrumb("UEFI_FRAME_STAGE_NAME=" + UefiStageName(_uefiMultiFrameStage));
        SerialBreadcrumb("UEFI_FRAME_SUBSTAGE=" + _uefiMultiFrameSubstage.ToString());
        SerialBreadcrumb("UEFI_FRAME_LAST_BOUNDARY=" + _uefiMultiFrameLastBoundary.ToString());
        SerialBreadcrumb("UEFI_FRAME_LAST_CODE_DEC=" + _uefiMultiFrameLastCodeAddress.ToString());
        SerialBreadcrumb("UEFI_STACK_LOW_WATER_DEC=" + _uefiMultiFrameStackLowWater.ToString());
        SerialBreadcrumb("UEFI_TIMER_TICKS=" + GetUefiTimerTicks().ToString());
    }

    private static bool _uefiMultiFrameActive = false;
    private static int _uefiMultiFrameCurrentFrame = 0;
    private static int _uefiMultiFrameLastCompletedFrame = 0;
    private static int _uefiMultiFrameStage = 0;
    private static int _uefiMultiFrameSubstage = 0;
    private static int _uefiMultiFrameLastBoundary = 0;
    private static ulong _uefiMultiFrameStackLowWater = 0;
    private static ulong _uefiMultiFrameLastCodeAddress = 0;
    private static ulong _uefiMultiFrameStartTicks = 0;

    private static string UefiStageName(int stage) {
        return stage switch {
            1 => "BACKGROUND",
            2 => "DESKTOP",
            3 => "TASKBAR",
            4 => "WINDOWS",
            5 => "CURSOR",
            6 => "PRESENT",
            7 => "FRAME_END",
            _ => "UNKNOWN"
        };
    }

    private static void SetUefiFrameBreadcrumb(int stage, int substage, int boundary) {
        _uefiMultiFrameStage = stage;
        _uefiMultiFrameSubstage = substage;
        _uefiMultiFrameLastBoundary = boundary;
        if (Framebuffer.Graphics != null && (ulong)Framebuffer.OriginalVideoMemory != 0 &&
            (ulong)Framebuffer.Graphics.VideoMemory != (ulong)Framebuffer.OriginalVideoMemory) {
            SerialBreadcrumb("UEFI_GRAPHICS_POINTER_MISMATCH_STAGE=" + stage.ToString());
            SerialBreadcrumb("UEFI_GRAPHICS_POINTER_MISMATCH_BOUNDARY=" + boundary.ToString());
            SerialBreadcrumb("UEFI_GRAPHICS_POINTER_ACTUAL=" +
                ((ulong)Framebuffer.Graphics.VideoMemory).ToString());
            SerialBreadcrumb("UEFI_GRAPHICS_POINTER_EXPECTED=" +
                ((ulong)Framebuffer.OriginalVideoMemory).ToString());
        }
        ulong rsp = Native.ReadRSP();
        if (_uefiMultiFrameStackLowWater == 0 || rsp < _uefiMultiFrameStackLowWater)
            _uefiMultiFrameStackLowWater = rsp;
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
    /// Create a small procedural cursor for UEFI, where image-dependent paths
    /// remain disabled until they have a separate runtime validation.
    /// </summary>
    private static Image CreateFallbackCursor() {
        Image img = new Image(16, 16);
        if (img == null || img.RawData == null) return img;
        for (int i = 0; i < img.RawData.Length; i++) img.RawData[i] = 0;
        for (int y = 0; y < 16; y++) {
            for (int x = 0; x < 16; x++) {
                if (y < 12 && x < 8 && x <= y && x < (12 - y)) {
                    img.RawData[y * 16 + x] = unchecked((int)0xFFFFFFFF);
                } else if (y < 13 && x < 9 &&
                           (x == y + 1 || x == (11 - y) || (y == 11 && x <= 7))) {
                    img.RawData[y * 16 + x] = unchecked((int)0xFF000000);
                }
            }
        }
        return img;
    }

    public static void KMain() {
        string customCharset = null;
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            BootConsole.DrawDebugLines = false;
            BootConsole.WriteLine("[BOOT_MODE] UEFI");
            BootConsole.WriteLine("[INPUT] UEFI input disabled after ExitBootServices (separate validation)");

            BootConsole.WriteLine("[FS] Re-mounting filesystem for UEFI");
            if (Ramdisk.RawBasePointer != null) {
                try {
                    if (Disk.Instance == null) {
                        new Ramdisk((System.IntPtr)Ramdisk.RawBasePointer);
                    }
                    File.Instance = new RdskFS();
                    BootConsole.WriteLine("[FS] mounted");
                } catch {
                    BootConsole.WriteLine("[FS] mount failed (continuing)");
                }
            } else {
                BootConsole.WriteLine("[FS] no ramdisk available");
            }

            Cursor = CreateFallbackCursor();
            CursorMoving = Cursor;
            CursorBusy = Cursor;

            BootConsole.WriteLine("[WM] INIT");
            try {
                WindowManager.Initialize();
                BootConsole.WriteLine("[WM] initialized");
            } catch {
                BootConsole.WriteLine("[WM] initialization failed");
            }

            BootConsole.WriteLine("[DESKTOP] INIT");
            try {
                Desktop.Initialize();
                BootConsole.WriteLine("[DESKTOP] initialized");
            } catch {
                BootConsole.WriteLine("[DESKTOP] initialization failed");
            }

            BootConsole.WriteLine("[KMAIN] Calling SMain");
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
        BootConsole.WriteLine("[SMAIN] initialized");
        SMainSetup();

        if (!IsUefiMode) {
            SetupEscapeKeyHandler();
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=LEGACY");
            RenderLoop();
            return;
        }

        if (UEFI_ENABLE_UTINY_DIAGNOSTIC) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=TINY_UEFI");
            RenderLoopUefiTinyBypass();
            return;
        }

        if (UseUefiNormalDesktopFirstFrame()) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=NORMAL_DESKTOP_UEFI_FIRST_FRAME");
            RenderLoopUefiNormalDesktopFirstFrame();
            return;
        }

        if (UseUefiNormalDesktopBoundedMode()) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=MULTIFRAME_NORMAL_DESKTOP_UEFI");
            RenderLoopUefiNormalDesktopBounded();
            return;
        }

        if (UEFI_ENABLE_CONTINUOUS_DESKTOP) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=UEFI_CONTINUOUS_DESKTOP");
            RenderLoop();
            return;
        }

        SerialBreadcrumb("SMAIN_DISPATCH_REASON=UEFI_DEFAULT_RECOVERY");
        RenderLoopUefiDefaultRecovery();
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

        // UEFI setup deliberately keeps image-dependent and input subsystems
        // out of the post-EBS recovery path. Icons use their existing safe fallbacks.
        SetupIcons();

        // Context menus
        SetupContextMenus();

        // Widgets
        SetupWidgets();
    }

    /// <summary>
    /// UEFI-specific setup (framebuffer test, wallpaper, triple buffering)
    /// </summary>
    private static void SMainSetupUefi() {
        BootConsole.WriteLine("[BOOT_MODE] UEFI");
        Framebuffer.RecoverUefiState();
        Framebuffer.TripleBuffered = false;
        Framebuffer.EnsureGraphics();

        if (Framebuffer.Graphics == null || Framebuffer.Graphics.VideoMemory == null ||
            Framebuffer.Width <= 0 || Framebuffer.Height <= 0) {
            BootConsole.WriteLine("[FRAMEBUFFER] initialization failed");
            Wallpaper = null;
            return;
        }

        // UEFI uses the canonical framebuffer-backed Graphics object. The
        // direct solid fill avoids image-dependent wallpaper paths post-EBS.
        Framebuffer.Graphics.Clear(0xFF0D7D77u);
        Wallpaper = null;
        BootConsole.WriteLine("[FRAMEBUFFER] initialized");
        BootConsole.WriteLine("[UEFI] triple buffering disabled");
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

    private static void SerialBreadcrumb(string breadcrumb) {
        if (breadcrumb == null) return;
        for (int i = 0; i < breadcrumb.Length; i++) {
            SerialChar(breadcrumb[i]);
        }
        SerialChar('\n');
    }

    /// <summary>
    /// Main render loop - extracted from SMain to keep stack frames small
    /// </summary>
    /// <summary>
    /// Continuous rendering remains available for the next phase, but is
    /// deliberately unreachable from the current UEFI default dispatch.
    /// </summary>
    private static void RenderLoop() {
        if (IsUefiMode) {
            int uefiFrame = 0;
            for (;;) {
                uefiFrame++;
                try {
                    if (!RenderUefiDesktopFrame(uefiFrame)) {
                        SerialBreadcrumb("UEFI_CONTINUOUS_FRAME_FAULT=FRAMEBUFFER_INVALID");
                        Thread.Sleep(10);
                        continue;
                    }
                    Thread.Sleep(16);
                } catch {
                    SerialBreadcrumb("UEFI_CONTINUOUS_FRAME_FAULT=MANAGED_EXCEPTION");
                    Thread.Sleep(10);
                }
            }
        }

        int frameCounter = 0;
        int lastMouseX = Control.MousePosition.X;
        int lastMouseY = Control.MousePosition.Y;
        ulong lastMoveTick = GetUefiTimerTicks();

        for (;;) {
            frameCounter++;
            try {
                try { MouseEventDispatcher.Update(); } catch { }
                WindowManager.MouseHandled = false;
                try { WindowManager.InputAll(); } catch { }
                try { WindowManager.FlushPendingCreates(); } catch { }
                try { WAVPlayer.DoPlay(); } catch { }

                try {
                    if ((Control.MouseButtons & MouseButtons.Right) == MouseButtons.Right &&
                        !RightClicked && !WindowManager.MouseHandled) {
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

                try {
                    Native.Stosd(Framebuffer.Graphics.VideoMemory, 0,
                        (ulong)(Framebuffer.Width * Framebuffer.Height));
                } catch {
                    Thread.Sleep(1);
                    continue;
                }

                try { BackgroundRotationManager.Update(); } catch { }
                try { BackgroundRotationManager.DrawBackground(); } catch { }
                try {
                    Desktop.Update(_cachedDocumentIcon, _cachedFolderIcon,
                        _cachedImageIcon, _cachedAudioIcon, 48);
                } catch { }
                try { WindowManager.DrawAllExceptTaskManager(); } catch { }
                try {
                    if (Desktop.Taskbar != null) Desktop.Taskbar.DrawWorkspaceSwitcher();
                } catch { }
                try { WindowManager.DrawTaskManager(); } catch { }
                try { WindowManager.CleanupClosedWindows(); } catch { }

                try {
                    Image cursor = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left
                        ? CursorMoving : Cursor;
                    if (cursor != null && cursor.RawData != null) {
                        Framebuffer.Graphics.DrawImage(Control.MousePosition.X,
                            Control.MousePosition.Y, cursor);
                    }
                } catch { }

                try { Framebuffer.Update(); } catch {
                    Thread.Sleep(1);
                    continue;
                }

                int mx = Control.MousePosition.X;
                int my = Control.MousePosition.Y;
                if (mx != lastMouseX || my != lastMouseY) {
                    lastMouseX = mx;
                    lastMouseY = my;
                    lastMoveTick = GetUefiTimerTicks();
                    Thread.Sleep(0);
                } else {
                    ulong now = GetUefiTimerTicks();
                    Thread.Sleep(now >= lastMoveTick && now - lastMoveTick < 100 ? 0 : 1);
                }
            } catch {
                Thread.Sleep(10);
            }
        }
    }

    private static bool RenderUefiDesktopFrame(int frameCounter) {
        guideXOS.Graph.Graphics graphics = Framebuffer.Graphics;
        if (graphics == null || graphics.VideoMemory == null ||
            Framebuffer.Width <= 0 || Framebuffer.Height <= 0 ||
            graphics.Width != Framebuffer.Width || graphics.Height != Framebuffer.Height) {
            return false;
        }

        SetUefiFrameBreadcrumb(1, 0, 100);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        BackgroundRotationManager.DrawBackground();

        SetUefiFrameBreadcrumb(1, 1, 101);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        SetUefiFrameBreadcrumb(2, 0, 200);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        Desktop.Update(_cachedDocumentIcon, _cachedFolderIcon,
            _cachedImageIcon, _cachedAudioIcon, 48);

        SetUefiFrameBreadcrumb(2, 1, 201);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        SetUefiFrameBreadcrumb(3, 0, 300);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        if (Desktop.Taskbar != null) Desktop.Taskbar.DrawWorkspaceSwitcher();

        SetUefiFrameBreadcrumb(3, 1, 301);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        SetUefiFrameBreadcrumb(4, 0, 400);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        WindowManager.DrawAllExceptTaskManager();
        WindowManager.DrawTaskManager();
        WindowManager.CleanupClosedWindows();

        SetUefiFrameBreadcrumb(4, 1, 401);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        SetUefiFrameBreadcrumb(5, 0, 500);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        DrawUefiCursor();

        SetUefiFrameBreadcrumb(5, 1, 501);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        SetUefiFrameBreadcrumb(6, 0, 600);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        Framebuffer.Update();

        SetUefiFrameBreadcrumb(6, 1, 601);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        SetUefiFrameBreadcrumb(7, 0, 700);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        return true;
    }

    private static void RenderLoopUefiDefaultRecovery() {
        SerialBreadcrumb("UEFI_RECOVERY_DEFAULT");
        if (!RenderUefiDesktopFrame(1)) {
            SerialBreadcrumb("UEFI_DEFAULT_FRAME_FAULT=FRAMEBUFFER_INVALID");
            HaltAfterUefiDefault();
            return;
        }
        SerialBreadcrumb("UEFI_DEFAULT_FRAME_COMPLETE");
        HaltAfterUefiDefault();
    }

    private static void RenderLoopUefiNormalDesktopFirstFrame() {
        SerialBreadcrumb("NORMAL_FRAME_ENTER");
        if (!RenderUefiDesktopFrame(1)) {
            SerialBreadcrumb("NORMAL_FRAME_FAULT=FRAMEBUFFER_INVALID");
            HaltAfterNormalDesktopFirstFrame();
            return;
        }
        SerialBreadcrumb("NORMAL_FRAME_COMPLETE");
        HaltAfterNormalDesktopFirstFrame();
    }

    private static void EmitUefiMultiFrameCheckpoint(int frame, int target) {
        if (frame == 1 || frame == 10 || frame == 30 || frame == 60 ||
            frame == 120 || frame == 180 || frame == 240 || frame == target) {
            SerialBreadcrumb("MULTIFRAME_CHECKPOINT_FRAME=" + frame.ToString());
        }
    }

    private static void RenderLoopUefiNormalDesktopBounded() {
        int target = UEFI_NORMAL_DESKTOP_BOUNDED_FRAME_TARGET;
        if (target <= 0) {
            SerialBreadcrumb("MULTIFRAME_FAULT=INVALID_TARGET");
            HaltAfterUefiMultiFrame();
            return;
        }

        _uefiMultiFrameActive = true;
        _uefiMultiFrameCurrentFrame = 0;
        _uefiMultiFrameLastCompletedFrame = 0;
        _uefiMultiFrameStage = 0;
        _uefiMultiFrameSubstage = 0;
        _uefiMultiFrameLastBoundary = 0;
        _uefiMultiFrameStackLowWater = 0;
        _uefiMultiFrameLastCodeAddress = 0;
        _uefiMultiFrameStartTicks = GetUefiTimerTicks();
        SerialBreadcrumb("MULTIFRAME_BEGIN");
        SerialBreadcrumb("MULTIFRAME_TARGET=" + target.ToString());
        SerialBreadcrumb("MULTIFRAME_ENSURE_GRAPHICS_EVERY_FRAME=0");
        SerialBreadcrumb("MULTIFRAME_CANONICAL_REASSERT=0");

        if (Framebuffer.Graphics == null || Framebuffer.Graphics.VideoMemory == null) {
            SerialBreadcrumb("MULTIFRAME_FAULT=FRAMEBUFFER_INVALID");
            HaltAfterUefiMultiFrame();
            return;
        }

        for (int frame = 1; frame <= target; frame++) {
            _uefiMultiFrameCurrentFrame = frame;
            try {
                if (!RenderUefiDesktopFrame(frame)) {
                    SerialBreadcrumb("MULTIFRAME_FAULT=FRAMEBUFFER_INVALID");
                    HaltAfterUefiMultiFrame();
                    return;
                }
                _uefiMultiFrameLastCompletedFrame = frame;
                EmitUefiMultiFrameCheckpoint(frame, target);
                Thread.Sleep(16);
            } catch {
                SerialBreadcrumb("MULTIFRAME_FAULT=MANAGED_EXCEPTION");
                HaltAfterUefiMultiFrame();
                return;
            }
        }

        ulong endTicks = GetUefiTimerTicks();
        SerialBreadcrumb(endTicks >= _uefiMultiFrameStartTicks
            ? "MULTIFRAME_TIMER_TICKING=1"
            : "MULTIFRAME_TIMER_TICKING=0");
        SerialBreadcrumb("UEFI_STACK_LOW_WATER_DEC=" + _uefiMultiFrameStackLowWater.ToString());
        SerialBreadcrumb("UEFI_FRAME_LAST_CODE_DEC=" + _uefiMultiFrameLastCodeAddress.ToString());
        SerialBreadcrumb("MULTIFRAME_LAST_COMPLETED_FRAME=" +
            _uefiMultiFrameLastCompletedFrame.ToString());
        SerialBreadcrumb("MULTIFRAME_COMPLETE");
        _uefiMultiFrameActive = false;
        HaltAfterUefiMultiFrame();
    }

    private static void HaltAfterUefiDefault() {
        SerialBreadcrumb("UEFI_DEFAULT_HALT_ENTER");
        for (;;) {
            Native.Hlt();
        }
    }

    private static void HaltAfterUefiMultiFrame() {
        SerialBreadcrumb("MULTIFRAME_HALT_ENTER");
        for (;;) {
            Native.Hlt();
        }
    }

    private static void HaltAfterNormalDesktopFirstFrame() {
        SerialBreadcrumb("NORMAL_FRAME_HALT_ENTER");
        for (;;) {
            Native.Hlt();
        }
    }

    private static void RenderLoopUefiTinyBypass() {
        SerialBreadcrumb("UTINY_BEGIN");
        guideXOS.Graph.Graphics graphics = Framebuffer.Graphics;
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;
        if (graphics == null || graphics.VideoMemory == null || fbW <= 0 || fbH <= 0) {
            SerialBreadcrumb("UTINY_FAULT=FRAMEBUFFER_INVALID");
            HaltAfterUefiTiny();
            return;
        }

        DrawUefiTinyProofPattern(graphics, fbW, fbH, 1);
        bool sampleValid = graphics.GetPoint(1, 1) == 0xFFFF2020u &&
                           graphics.GetPoint(fbW / 2, fbH / 2) == 0xFF00FF00u &&
                           graphics.GetPoint(fbW - 1, fbH - 1) == 0xFFFFFFFFu;
        SerialBreadcrumb(sampleValid ? "UTINY_PIXEL_SAMPLE_VALID=1" :
                                       "UTINY_PIXEL_SAMPLE_VALID=0");
        Framebuffer.Update();
        SerialBreadcrumb("UTINY_PATTERN_DRAWN");
        SerialBreadcrumb("UTINY_COMPLETE");
        HaltAfterUefiTiny();
    }

    private static void HaltAfterUefiTiny() {
        SerialBreadcrumb("UTINY_HALT_ENTER");
        for (;;) {
            Native.Hlt();
        }
    }

    private static void DrawUefiTinyProofPattern(guideXOS.Graph.Graphics graphics,
                                                  int fbW, int fbH, int frameCounter) {
        if (graphics == null || graphics.VideoMemory == null || fbW <= 0 || fbH <= 0) {
            return;
        }

        graphics.Clear(0xFF0B3C4Cu);

        int redW = fbW / 3;
        int redH = fbH / 3;
        if (redW < 80) redW = fbW < 80 ? fbW : 80;
        if (redH < 80) redH = fbH < 80 ? fbH : 80;
        graphics.FillRectangle(0, 0, redW, redH, 0xFFFF2020u);

        int greenW = fbW / 3;
        int greenH = fbH / 4;
        if (greenW < 96) greenW = fbW < 96 ? fbW : 96;
        if (greenH < 96) greenH = fbH < 96 ? fbH : 96;
        int greenX = (fbW - greenW) / 2;
        int greenY = (fbH - greenH) / 2;
        graphics.FillRectangle(greenX, greenY, greenW, greenH, 0xFF00FF00u);

        int whiteW = fbW / 4;
        int whiteH = fbH / 4;
        if (whiteW < 96) whiteW = fbW < 96 ? fbW : 96;
        if (whiteH < 96) whiteH = fbH < 96 ? fbH : 96;
        graphics.FillRectangle(fbW - whiteW, fbH - whiteH, whiteW, whiteH, 0xFFFFFFFFu);

        int squareSize = fbW < fbH ? fbW / 12 : fbH / 12;
        if (squareSize < 12) squareSize = 12;
        if (squareSize > 32) squareSize = 32;
        if (squareSize > fbW) squareSize = fbW;
        if (squareSize > fbH) squareSize = fbH;

        int xSpan = fbW - squareSize;
        int ySpan = fbH - squareSize;
        if (xSpan < 1) xSpan = 1;
        if (ySpan < 1) ySpan = 1;

        int squareX = (frameCounter * 13) % xSpan;
        int squareY = (frameCounter * 7) % ySpan;
        graphics.FillRectangle(squareX, squareY, squareSize, squareSize, 0xFFFFFFFFu);
    }

    private static void DrawUefiCursor() {
        try {
            Image cursor = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left
                ? CursorMoving : Cursor;
            if (cursor != null && cursor.RawData != null) {
                Framebuffer.Graphics.DrawImage(Control.MousePosition.X,
                    Control.MousePosition.Y, cursor);
            }
        } catch {
            // UEFI input remains disabled; a cursor draw failure must not stop the frame.
        }
    }
    /// <summary>
    /// Refresh cached icons without exposing a partially updated set to Desktop.Update.
    /// </summary>
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
