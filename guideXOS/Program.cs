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
    /// Clock widget retained by the normal widget container.
    /// </summary>
    public static Clock ClockWidget;
    /// <summary>
    /// Monitor widget retained by the normal widget container.
    /// </summary>
    public static Monitor MonitorWidget;
    /// <summary>
    /// Uptime widget retained by the normal widget container.
    /// </summary>
    public static Uptime UptimeWidget;
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
    private static bool _uefiRealIconMarkerEmitted;

    private static ulong GetUefiTimerTicks() {
        try {
            return Timer.Ticks;
        } catch {
            return 0;
        }
    }

    // UEFI desktop dispatch controls. Diagnostic builds select one of the
    // explicit modes below through the UefiDiagnosticMode MSBuild property.
#if UEFI_DIAGNOSTIC_FONT
    private const bool UEFI_ENABLE_FONT_DIAGNOSTIC = true;
    private const bool UEFI_ENABLE_PNG_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_UTINY_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED = false;
#elif UEFI_DIAGNOSTIC_PNG
    private const bool UEFI_ENABLE_FONT_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_PNG_DIAGNOSTIC = true;
    private const bool UEFI_ENABLE_UTINY_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED = false;
#elif UEFI_DIAGNOSTIC_TINY
    private const bool UEFI_ENABLE_FONT_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_PNG_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_UTINY_DIAGNOSTIC = true;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED = false;
#elif UEFI_DIAGNOSTIC_FIRST_FRAME
    private const bool UEFI_ENABLE_FONT_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_PNG_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_UTINY_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME = true;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED = false;
#elif UEFI_DIAGNOSTIC_FRAMES
    private const bool UEFI_ENABLE_FONT_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_PNG_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_UTINY_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED = true;
#else
    private const bool UEFI_ENABLE_FONT_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_PNG_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_UTINY_DIAGNOSTIC = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME = false;
    private const bool UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED = false;
#endif
#if UEFI_DIAGNOSTIC_BACKGROUND
    private const bool UEFI_ENABLE_BACKGROUND_DIAGNOSTIC = true;
#else
    private const bool UEFI_ENABLE_BACKGROUND_DIAGNOSTIC = false;
#endif
#if UEFI_DIAGNOSTIC_BACKGROUND_ROTATION
    private const bool UEFI_ENABLE_BACKGROUND_ROTATION_DIAGNOSTIC = true;
#else
    private const bool UEFI_ENABLE_BACKGROUND_ROTATION_DIAGNOSTIC = false;
#endif
#if UEFI_DIAGNOSTIC_APP_MODEL
    private const bool UEFI_ENABLE_APP_MODEL_DIAGNOSTIC = true;
#else
    private const bool UEFI_ENABLE_APP_MODEL_DIAGNOSTIC = false;
#endif
#if UEFI_DIAGNOSTIC_APP_RUNTIME
    private const bool UEFI_ENABLE_APP_RUNTIME_DIAGNOSTIC = true;
#else
    private const bool UEFI_ENABLE_APP_RUNTIME_DIAGNOSTIC = false;
#endif
#if UEFI_DIAGNOSTIC_INPUT || UEFI_DIAGNOSTIC_INPUT_STRESS
    private const bool UEFI_ENABLE_INPUT_DIAGNOSTIC_TARGET = true;
#else
    private const bool UEFI_ENABLE_INPUT_DIAGNOSTIC_TARGET = false;
#endif
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
    private const bool UEFI_ENABLE_WIDGET_DIAGNOSTIC = true;
#else
    private const bool UEFI_ENABLE_WIDGET_DIAGNOSTIC = false;
#endif
    private const int UEFI_NORMAL_DESKTOP_BOUNDED_FRAME_TARGET = 300;

    // The ordinary UEFI path is the recovered desktop. Diagnostic modes above
    // remain opt-in build variants and do not alter the production loop.
    private const bool UEFI_ENABLE_CONTINUOUS_DESKTOP = true;

    private const int UEFI_CONTINUOUS_HEARTBEAT_FIRST = 1;
    private const int UEFI_CONTINUOUS_HEARTBEAT_EARLY_1 = 60;
    private const int UEFI_CONTINUOUS_HEARTBEAT_EARLY_2 = 300;
    private const int UEFI_CONTINUOUS_HEARTBEAT_EARLY_3 = 600;
    private const int UEFI_CONTINUOUS_HEARTBEAT_INTERVAL = 1800;

    private static bool IsUefiMode =>
        BootConsole.CurrentMode == guideXOS.BootMode.UEFI;

    private static bool IsUefiWidgetDiagnostic =>
        IsUefiMode && UEFI_ENABLE_WIDGET_DIAGNOSTIC;

    private static bool UseUefiNormalDesktopFirstFrame() {
        return IsUefiMode && UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME;
    }

    private static bool UseUefiNormalDesktopBoundedMode() {
        return IsUefiMode && UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED;
    }

    private static bool IsUefiGraphicsInvariantValid() {
        guideXOS.Graph.Graphics graphics = Framebuffer.Graphics;
        if (graphics == null || graphics.VideoMemory == null ||
            Framebuffer.VideoMemory == null || Framebuffer.OriginalVideoMemory == null ||
            Framebuffer.Width <= 0 || Framebuffer.Height <= 0 ||
            graphics.Width != Framebuffer.Width || graphics.Height != Framebuffer.Height) {
            return false;
        }

        uint* expectedGraphicsMemory = Framebuffer.TripleBuffered &&
            Framebuffer.FirstBuffer != null
            ? Framebuffer.FirstBuffer : Framebuffer.OriginalVideoMemory;
        if ((ulong)Framebuffer.VideoMemory != (ulong)Framebuffer.OriginalVideoMemory ||
            (ulong)graphics.VideoMemory != (ulong)expectedGraphicsMemory) {
            return false;
        }

        if (Framebuffer.OriginalWidth != 0 &&
            graphics.Width != Framebuffer.OriginalWidth) {
            return false;
        }
        if (Framebuffer.OriginalHeight != 0 &&
            graphics.Height != Framebuffer.OriginalHeight) {
            return false;
        }
        return true;
    }

    // Early firmware-era hardware discovery remains guarded. Native PS/2 is
    // deliberately not guarded by BootMode and is initialized after APIC/IOAPIC
    // setup, before ExitBootServices-owned pointers are retired.
    internal static bool ShouldSkipEarlyUefiHardwareInitialization() {
        return IsUefiMode;
    }

    internal static bool ShouldSkipEarlyUefiInputInitialization() {
        return false;
    }

    private static bool _uefiGuiKeyRouted;
    private static bool _uefiGuiMouseRouted;
    private static bool _uefiDesktopFilesClickRouted;
    private static bool _uefiDesktopClickCandidateLogged;
    private static int _uefiDesktopClickEdgeCount;
    private static bool _uefiDesktopTextRendered;
    private static bool _uefiTaskbarTextRendered;
    private static bool _uefiLoginTextRendered;

    private static int _uefiWidgetUpdatePerformance;
    private static int _uefiWidgetUpdateClock;
    private static int _uefiWidgetUpdateMonitor;
    private static int _uefiWidgetUpdateUptime;
    private static bool _uefiWidgetDrawPerformance;
    private static bool _uefiWidgetDrawClock;
    private static bool _uefiWidgetDrawMonitor;
    private static bool _uefiWidgetDrawUptime;
    private static int _uefiWidgetHoverCount;
    private static int _uefiWidgetLeftClickCount;
    private static int _uefiWidgetRightClickCount;
    private static int _uefiWidgetDragStartCount;
    private static int _uefiWidgetDragEndCount;

    internal static void MarkUefiGuiKeyRouted() {
        if (!IsUefiMode || _uefiGuiKeyRouted) return;
        _uefiGuiKeyRouted = true;
        SerialBreadcrumb("INPUT_GUI_KEY_ROUTED");
    }

    internal static void MarkUefiGuiMouseRouted() {
        if (!IsUefiMode || _uefiGuiMouseRouted) return;
        _uefiGuiMouseRouted = true;
        SerialBreadcrumb("INPUT_GUI_MOUSE_ROUTED");
    }

    internal static void MarkUefiStartMenuOpened() {
#if UEFI_DIAGNOSTIC_INPUT || UEFI_DIAGNOSTIC_INPUT_STRESS || UEFI_DIAGNOSTIC_APP_RUNTIME
        if (!IsUefiMode) return;
        SerialBreadcrumb("START_MENU_OPENED");
#endif
    }

    internal static void MarkUefiAppRuntime(string breadcrumb) {
#if UEFI_DIAGNOSTIC_APP_RUNTIME
        if (!IsUefiMode || breadcrumb == null) return;
        SerialBreadcrumb("APP_RUNTIME_" + breadcrumb);
#endif
    }

    internal static void MarkUefiAppModelDiagnostic(string breadcrumb) {
#if UEFI_DIAGNOSTIC_APP_MODEL
        if (!IsUefiMode || breadcrumb == null) return;
        SerialBreadcrumb("APP_MODEL_" + breadcrumb);
#endif
    }

    internal static void MarkUefiDesktopFilesClickRouted() {
        if (!IsUefiMode || _uefiDesktopFilesClickRouted) return;
        _uefiDesktopFilesClickRouted = true;
        SerialBreadcrumb("DESKTOP_FILES_CLICK_ROUTED");
    }

    internal static void MarkUefiDesktopTextRendered() {
        if (!IsUefiMode || _uefiDesktopTextRendered || !WindowManager.RealFontEnabled) return;
        _uefiDesktopTextRendered = true;
        SerialBreadcrumb("DESKTOP_REAL_TEXT_RENDERED=1");
    }

    internal static void MarkUefiTaskbarTextRendered() {
        if (!IsUefiMode || _uefiTaskbarTextRendered || !WindowManager.RealFontEnabled) return;
        _uefiTaskbarTextRendered = true;
        SerialBreadcrumb("TASKBAR_REAL_TEXT_RENDERED=1");
    }

    internal static void MarkUefiLoginTextRendered() {
        if (!IsUefiMode || _uefiLoginTextRendered || !WindowManager.RealFontEnabled) return;
        _uefiLoginTextRendered = true;
        SerialBreadcrumb("INPUT_GUI_TEXT_RENDERED=1");
    }

    internal static void MarkUefiWidgetsInitialized(int count, bool visible) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode) return;
        SerialBreadcrumb("WIDGETS_INITIALIZED=1,count=" + count.ToString() +
            ",visible=" + (visible ? "1" : "0"));
#endif
    }

    internal static void MarkUefiWidgetInitialization(string name, bool ok,
                                                       int x, int y, int width, int height) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode || name == null) return;
        bool boundsOk = x >= 0 && y >= 0 && width > 0 && height > 0 &&
                        x + width <= Framebuffer.Width && y + height <= Framebuffer.Height;
        SerialBreadcrumb("WIDGET_INIT=" + name + ",ok=" + (ok ? "1" : "0") +
            ",bounds=" + (boundsOk ? "1" : "0") + ",x=" + x.ToString() +
            ",y=" + y.ToString() + ",w=" + width.ToString() +
            ",h=" + height.ToString());
        if (name == "PerformanceWidget" && ok) {
            SerialBreadcrumb("WIDGET_PROBE_TYPE=PerformanceWidget");
            SerialBreadcrumb("WIDGET_PROBE_INIT_OK");
        }
#endif
    }

    internal static void MarkUefiWidgetUpdated(string name) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode || name == null) return;
        int count = 0;
        if (name == "PerformanceWidget") count = ++_uefiWidgetUpdatePerformance;
        else if (name == "Clock") count = ++_uefiWidgetUpdateClock;
        else if (name == "Monitor") count = ++_uefiWidgetUpdateMonitor;
        else if (name == "Uptime") count = ++_uefiWidgetUpdateUptime;
        if (count == 1 || count == 2 || count == 3 || (count % 10) == 0) {
            SerialWidgetUpdate(name, count);
        }
        if (name == "PerformanceWidget" && count == 1) {
            SerialBreadcrumb("WIDGET_PROBE_UPDATE_OK");
        }
#endif
    }

    internal static void MarkUefiWidgetDrawn(string name, int x, int y,
                                             int width, int height) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode || name == null) return;
        bool already = name == "PerformanceWidget" ? _uefiWidgetDrawPerformance :
                       name == "Clock" ? _uefiWidgetDrawClock :
                       name == "Monitor" ? _uefiWidgetDrawMonitor : _uefiWidgetDrawUptime;
        if (already) return;
        if (name == "PerformanceWidget") _uefiWidgetDrawPerformance = true;
        else if (name == "Clock") _uefiWidgetDrawClock = true;
        else if (name == "Monitor") _uefiWidgetDrawMonitor = true;
        else _uefiWidgetDrawUptime = true;
        bool boundsOk = x >= 0 && y >= 0 && width > 0 && height > 0 &&
                        x + width <= Framebuffer.Width && y + height <= Framebuffer.Height;
        SerialBreadcrumb("WIDGET_DRAW=" + name + ",bounds=" + (boundsOk ? "1" : "0") +
            ",font=" + (WindowManager.RealFontEnabled ? "1" : "0") +
            ",x=" + x.ToString() + ",y=" + y.ToString() +
            ",w=" + width.ToString() + ",h=" + height.ToString());
        if (name == "PerformanceWidget") SerialBreadcrumb("WIDGET_PROBE_DRAW_OK");
#endif
    }

    internal static void MarkUefiWidgetHover(int index) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode) return;
        _uefiWidgetHoverCount++;
        if (_uefiWidgetHoverCount <= 3 || (_uefiWidgetHoverCount % 25) == 0) {
            SerialBreadcrumb("WIDGET_HOVER=" + index.ToString() +
                ",count=" + _uefiWidgetHoverCount.ToString());
        }
#endif
    }

    internal static void MarkUefiWidgetInput(string kind) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode || kind == null) return;
        int count = 0;
        if (kind == "LEFT") count = ++_uefiWidgetLeftClickCount;
        else if (kind == "RIGHT") count = ++_uefiWidgetRightClickCount;
        else if (kind == "DRAG_START") count = ++_uefiWidgetDragStartCount;
        else if (kind == "DRAG_END") count = ++_uefiWidgetDragEndCount;
        if (count <= 3 || (count % 25) == 0) {
            SerialBreadcrumb("WIDGET_INPUT=" + kind + ",count=" + count.ToString());
        }
#endif
    }

    internal static void MarkUefiWidgetMenuOpened(int x, int y, int width, int height,
                                                  string item) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode) return;
        SerialBreadcrumb("WIDGET_MENU_OPENED=" + x.ToString() + "," + y.ToString() +
            ",item=" + item);
        SerialBreadcrumb("WIDGET_MENU_BOUNDS=" + x.ToString() + "," + y.ToString() +
            "," + width.ToString() + "," + height.ToString() + ",ok=" +
            ((x >= 0 && y >= 0 && x + width <= Framebuffer.Width &&
              y + height <= Framebuffer.Height) ? "1" : "0"));
#endif
    }

    internal static void MarkUefiWidgetMenuDrawn(int x, int y, int width, int height) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode) return;
        SerialBreadcrumb("WIDGET_MENU_DRAWN=" + x.ToString() + "," + y.ToString() +
            "," + width.ToString() + "," + height.ToString() + ",font=" +
            (WindowManager.RealFontEnabled ? "1" : "0"));
#endif
    }

    internal static void MarkUefiWidgetMenuHover(bool hovered) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode) return;
        SerialBreadcrumb("WIDGET_MENU_HOVER=" + (hovered ? "1" : "0"));
#endif
    }

    internal static void MarkUefiWidgetMenuActivated(string command) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode || command == null) return;
        SerialBreadcrumb("WIDGET_MENU_ACTIVATED=" + command);
#endif
    }

    internal static void MarkUefiWidgetMenuDismissed(string reason) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode || reason == null) return;
        SerialBreadcrumb("WIDGET_MENU_DISMISSED=" + reason);
#endif
    }

    internal static void MarkUefiWidgetRuntimeFault(string stage, string name) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode) return;
        SerialBreadcrumb("WIDGET_RUNTIME_FAULT=" + (stage ?? "UNKNOWN") + "," +
            (name ?? "UNKNOWN"));
#endif
    }

    internal static void MarkUefiWidgetDesktopMenuOpened(int x, int y,
                                                          int width, int height) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode) return;
        SerialBreadcrumb("WIDGET_DESKTOP_MENU_OPENED=" + x.ToString() + "," +
            y.ToString() + "," + width.ToString() + "," + height.ToString());
#endif
    }

    internal static void MarkUefiWidgetTaskbarMenuOpened(int x, int y,
                                                          int width, int height) {
#if UEFI_DIAGNOSTIC_WIDGET || UEFI_DIAGNOSTIC_WIDGET_STRESS || UEFI_DIAGNOSTIC_WIDGET_SOAK
        if (!IsUefiMode) return;
        SerialBreadcrumb("WIDGET_TASKBAR_MENU_OPENED=" + x.ToString() + "," +
            y.ToString() + "," + width.ToString() + "," + height.ToString());
#endif
    }

    internal static void CloseWidgetContextMenu() {
        if (widgetContextMenu != null && widgetContextMenu.Visible) {
            widgetContextMenu.HideMenu();
        }
    }

    internal static void CloseOtherContextMenusForWidget() {
        if (RightMenu != null && RightMenu.Visible) RightMenu.Visible = false;
        if (Desktop.Taskbar != null) Desktop.Taskbar.CloseContextMenu();
    }

    internal static void MarkUefiContextMenuOpened(int x, int y, int width, int height) {
#if UEFI_DIAGNOSTIC_CONTEXT_MENU
        if (!IsUefiMode) return;
        SerialBreadcrumb("CONTEXT_MENU_OPENED=" + x.ToString() + "," + y.ToString());
        SerialBreadcrumb("CONTEXT_MENU_RIGHT_DOWN=" + PS2Mouse.RightDownCount.ToString());
        SerialBreadcrumb("CONTEXT_MENU_BOUNDS=" + x.ToString() + "," + y.ToString() + "," +
            width.ToString() + "," + height.ToString() + ",ok=" +
            ((x >= 0 && y >= 0 && x + width <= Framebuffer.Width &&
              y + height <= Framebuffer.Height) ? "1" : "0"));
#endif
    }

    internal static void MarkUefiContextMenuDrawn(int x, int y, int width, int height) {
#if UEFI_DIAGNOSTIC_CONTEXT_MENU
        if (!IsUefiMode) return;
        SerialBreadcrumb("CONTEXT_MENU_DRAWN=" + x.ToString() + "," + y.ToString() + "," +
            width.ToString() + "," + height.ToString() + ",font=" +
            (WindowManager.RealFontEnabled ? "1" : "0"));
#endif
    }

    internal static void MarkUefiContextMenuHover(int index) {
#if UEFI_DIAGNOSTIC_CONTEXT_MENU
        if (!IsUefiMode) return;
        SerialBreadcrumb("CONTEXT_MENU_HOVER_INDEX=" + index.ToString());
#endif
    }

    internal static void MarkUefiContextMenuActivated(string command) {
#if UEFI_DIAGNOSTIC_CONTEXT_MENU
        if (!IsUefiMode || command == null) return;
        SerialBreadcrumb("CONTEXT_MENU_ACTIVATED=" + command);
#endif
    }

    internal static void MarkUefiContextMenuLeftEdge() {
#if UEFI_DIAGNOSTIC_CONTEXT_MENU
        if (!IsUefiMode) return;
        SerialBreadcrumb("CONTEXT_MENU_LEFT_EDGE");
#endif
    }

    internal static void MarkUefiContextMenuDismissed(string reason) {
#if UEFI_DIAGNOSTIC_CONTEXT_MENU
        if (!IsUefiMode || reason == null) return;
        SerialBreadcrumb("CONTEXT_MENU_DISMISSED=" + reason);
        SerialBreadcrumb("CONTEXT_MENU_INPUT_STATS=" + PS2Mouse.RightDownCount.ToString() + "," +
                         PS2Mouse.RightUpCount.ToString());
#endif
    }

    internal static void MarkUefiTaskbarContextMenuOpened(int x, int y, int width, int height) {
#if UEFI_DIAGNOSTIC_CONTEXT_MENU
        if (!IsUefiMode) return;
        SerialBreadcrumb("TASKBAR_CONTEXT_MENU_OPENED=" + x.ToString() + "," + y.ToString());
        SerialBreadcrumb("TASKBAR_CONTEXT_MENU_RIGHT_DOWN=" + PS2Mouse.RightDownCount.ToString());
        SerialBreadcrumb("TASKBAR_CONTEXT_MENU_BOUNDS=" + x.ToString() + "," + y.ToString() + "," +
            width.ToString() + "," + height.ToString() + ",ok=" +
            ((x >= 0 && y >= 0 && x + width <= Framebuffer.Width &&
              y + height <= Framebuffer.Height) ? "1" : "0"));
#endif
    }

    internal static void MarkUefiTaskbarContextMenuDrawn(int x, int y, int width, int height) {
#if UEFI_DIAGNOSTIC_CONTEXT_MENU
        if (!IsUefiMode) return;
        SerialBreadcrumb("TASKBAR_CONTEXT_MENU_DRAWN=" + x.ToString() + "," + y.ToString() + "," +
            width.ToString() + "," + height.ToString() + ",font=" +
            (WindowManager.RealFontEnabled ? "1" : "0"));
#endif
    }

    internal static void MarkUefiTaskbarContextMenuDismissed(string reason) {
#if UEFI_DIAGNOSTIC_CONTEXT_MENU
        if (!IsUefiMode || reason == null) return;
        SerialBreadcrumb("TASKBAR_CONTEXT_MENU_DISMISSED=" + reason);
        SerialBreadcrumb("TASKBAR_CONTEXT_MENU_INPUT_STATS=" + PS2Mouse.RightDownCount.ToString() + "," +
                         PS2Mouse.RightUpCount.ToString());
#endif
    }

    internal static void MarkUefiDesktopClickCandidate(int x, int y) {
#if UEFI_DIAGNOSTIC_INPUT || UEFI_DIAGNOSTIC_INPUT_STRESS
        if (!IsUefiMode || _uefiDesktopClickCandidateLogged) return;
        _uefiDesktopClickCandidateLogged = true;
        SerialBreadcrumb("DESKTOP_CLICK_CANDIDATE=" + x.ToString() + "," + y.ToString());
#endif
    }

    internal static void MarkUefiDesktopClickEdge(int x, int y, bool handled) {
#if UEFI_DIAGNOSTIC_INPUT || UEFI_DIAGNOSTIC_INPUT_STRESS
        if (!IsUefiMode || _uefiDesktopClickEdgeCount >= 4) return;
        _uefiDesktopClickEdgeCount++;
        SerialBreadcrumb("DESKTOP_CLICK_EDGE=" + x.ToString() + "," + y.ToString() +
            ",handled=" + (handled ? "1" : "0"));
#endif
    }

    private static void SetupUefiDiagnosticInputTarget() {
        if (!IsUefiMode || !UEFI_ENABLE_INPUT_DIAGNOSTIC_TARGET) return;
#if UEFI_DIAGNOSTIC_INPUT || UEFI_DIAGNOSTIC_INPUT_STRESS
        LoginDialog probe = new LoginDialog();
        WindowManager.MoveToEnd(probe);
        probe.Visible = true;
        SerialBreadcrumb("INPUT_GUI_TARGET=LOGIN_DIALOG");
#endif
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
        uint* expectedGraphicsMemory = Framebuffer.TripleBuffered &&
            Framebuffer.FirstBuffer != null
            ? Framebuffer.FirstBuffer : Framebuffer.OriginalVideoMemory;
        if (Framebuffer.Graphics != null && (ulong)expectedGraphicsMemory != 0 &&
            (ulong)Framebuffer.Graphics.VideoMemory != (ulong)expectedGraphicsMemory) {
            SerialBreadcrumb("UEFI_GRAPHICS_POINTER_MISMATCH_STAGE=" + stage.ToString());
            SerialBreadcrumb("UEFI_GRAPHICS_POINTER_MISMATCH_BOUNDARY=" + boundary.ToString());
            SerialBreadcrumb("UEFI_GRAPHICS_POINTER_ACTUAL=" +
                ((ulong)Framebuffer.Graphics.VideoMemory).ToString());
            SerialBreadcrumb("UEFI_GRAPHICS_POINTER_EXPECTED=" +
                ((ulong)expectedGraphicsMemory).ToString());
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
            BootConsole.WriteLine("[INPUT] Native post-EBS path selected");

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

        // Escape is part of the normal Window contract. It is safe on UEFI now
        // that keyboard input reaches the same main-thread event pipeline.
        SetupEscapeKeyHandler();

        if (!IsUefiMode) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=LEGACY");
            RenderLoop();
            return;
        }

        if (UEFI_ENABLE_BACKGROUND_DIAGNOSTIC) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=BACKGROUND_PROBE");
            RenderLoopUefiBackgroundProbe();
            return;
        }

        if (UEFI_ENABLE_BACKGROUND_ROTATION_DIAGNOSTIC) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=BACKGROUND_ROTATION");
            RenderLoopUefiBackgroundRotation();
            return;
        }

        if (UEFI_ENABLE_APP_MODEL_DIAGNOSTIC) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=APP_MODEL");
            RenderLoopUefiAppModelDiagnostic();
            return;
        }

        if (UEFI_ENABLE_APP_RUNTIME_DIAGNOSTIC) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=APP_RUNTIME");
            SerialBreadcrumb("APP_RUNTIME_MODEL_READY=apps=" +
                (Desktop.Apps == null ? "0" : Desktop.Apps.Length.ToString()));
        }

        if (UEFI_ENABLE_FONT_DIAGNOSTIC) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=FONT_PROBE");
            RenderLoopUefiFontProbe();
            return;
        }

        if (UEFI_ENABLE_PNG_DIAGNOSTIC) {
            SerialBreadcrumb("SMAIN_DISPATCH_REASON=PNG_PROBE");
            RenderLoopUefiPngProbe();
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

        // UEFI setup keeps firmware-owned paths out of the post-EBS recovery
        // path, but its managed image assets are now safe to initialize here.
        SetupIcons();

        // The early UEFI Desktop.Initialize phase defers descriptor/icon
        // construction until the post-EBS managed asset path is ready.
        try {
            Desktop.InitializeAppModel();
            if (IsUefiMode) BootConsole.WriteLine("[APP_MODEL] initialized");
        } catch {
            if (IsUefiMode) BootConsole.WriteLine("[APP_MODEL] unavailable");
        }

#if UEFI_DIAGNOSTIC_APP_RUNTIME
        if (IsUefiMode && Desktop.Apps != null) {
            // Keep negative coverage bounded and before host-driven positive
            // interaction.  The missing-file probes intentionally use the
            // same Desktop.OnClick path as a real shell item.
            AppLaunchResolution unknownId =
                AppLaunchResolver.Resolve("gxos.builtin.notreal");
            AppLaunchResolution unknownAlias =
                AppLaunchResolver.Resolve("Definitely Not A Real App");
            FileAssociationResolution unknownExtension =
                FileAssociationRegistry.ResolvePath("fixture.unknown");
            ShellObjectResolution unknownShell =
                ShellObjectRegistry.Resolve("Unsupported Shell Object");
            SerialBreadcrumb("APP_RUNTIME_NEGATIVE_STAGE=RESOLVED");
            byte[] malformedPath = File.ReadAllBytes("malformed/path");
            SerialBreadcrumb("APP_RUNTIME_NEGATIVE_STAGE=MALFORMED_READ");
            if (malformedPath != null) malformedPath.Dispose();
            bool unknownLoad = !Desktop.Apps.Load("gxos.builtin.notreal");
            SerialBreadcrumb("APP_RUNTIME_NEGATIVE_UNKNOWN_LOAD=" +
                (unknownLoad ? "PASS" : "FAIL"));
            SerialBreadcrumb("APP_RUNTIME_NEGATIVE_STAGE=UNKNOWN_LOAD");
            SerialBreadcrumb("APP_RUNTIME_NEGATIVE_UNKNOWN_ID=" +
                (!unknownId.Success ? "PASS" : "FAIL"));
            SerialBreadcrumb("APP_RUNTIME_NEGATIVE_UNKNOWN_ALIAS=" +
                (!unknownAlias.Success ? "PASS" : "FAIL"));
            SerialBreadcrumb("APP_RUNTIME_NEGATIVE_UNKNOWN_EXTENSION=" +
                (!unknownExtension.Success ? "PASS" : "FAIL"));
            SerialBreadcrumb("APP_RUNTIME_NEGATIVE_MALFORMED_PATH=" +
                (malformedPath == null ? "PASS" : "FAIL"));
            SerialBreadcrumb("APP_RUNTIME_NEGATIVE_UNKNOWN_SHELL=" +
                (!unknownShell.Success ? "PASS" : "FAIL"));
            bool lifecycleRuntime =
                ApplicationInstanceRegistry.RunLifecycleRuntimeDiagnostic();
            SerialBreadcrumb("APP_RUNTIME_LIFECYCLE_DIAGNOSTIC=" +
                (lifecycleRuntime ? "PASS" : "FAIL"));
        }
#endif

        // Context menus
        SetupContextMenus();

        // Widgets
        SetupWidgets();

        if (IsUefiMode) {
            SetupUefiDiagnosticInputTarget();
            PS2Keyboard.EnableFullProcessing();
            PS2Mouse.EnableFullProcessing();
        }
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

        // Establish the known safe last-resort background. SetupIcons then
        // lets BackgroundRotationManager replace it with the managed asset.
        Framebuffer.Graphics.Clear(0xFF0D7D77u);
        // Render the desktop into the existing back buffer and publish only
        // completed frames. Drawing directly into the GOP framebuffer makes
        // the clear at the start of a frame visible while a window or the
        // Start menu is being composed.
        Framebuffer.TripleBuffered = true;
        Wallpaper = null;
        BootConsole.WriteLine("[FRAMEBUFFER] initialized");
        BootConsole.WriteLine("[UEFI] triple buffering enabled");
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
                if (RefreshCachedIcons()) {
                    _lastIconCacheRefresh = Timer.Ticks;
                    BootConsole.WriteLine("[SMAIN] Icons initialized");
                } else {
                    BootConsole.WriteLine("[SMAIN] Icon initialization failed - using fallback");
                    _cachedDocumentIcon = new Image(48, 48);
                    _cachedFolderIcon = new Image(48, 48);
                    _cachedImageIcon = new Image(48, 48);
                    _cachedAudioIcon = new Image(48, 48);
                }
            } else {
                BootConsole.WriteLine("[SMAIN] Initializing managed PNG image assets");
                bool pngReady = PngLoader.Initialize();
                if (pngReady) BackgroundRotationManager.Initialize();
                if (pngReady && RefreshCachedIcons() &&
                    HasVisiblePixels(_cachedDocumentIcon) &&
                    HasVisiblePixels(_cachedFolderIcon) &&
                    HasVisiblePixels(_cachedImageIcon) &&
                    HasVisiblePixels(_cachedAudioIcon)) {
                    _lastIconCacheRefresh = Timer.Ticks;
                    BootConsole.WriteLine("[PNG] image assets initialized");
                    BootConsole.WriteLine("[DESKTOP] real icon assets enabled");
                } else {
                    BootConsole.WriteLine("[SMAIN] PNG icon initialization failed - using fallback");
                    _cachedDocumentIcon = new Image(48, 48);
                    _cachedFolderIcon = new Image(48, 48);
                    _cachedImageIcon = new Image(48, 48);
                    _cachedAudioIcon = new Image(48, 48);
                }
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
        RightMenu = null;
        widgetContextMenu = null;
        try {
            RightMenu = new RightMenu();
            RightMenu.Visible = false;

            if (UISettings.EnableWidgetContextMenu) {
                widgetContextMenu = new WidgetContextMenu();
                widgetContextMenu.Visible = false;
                WindowManager.MoveToEnd(widgetContextMenu);
            }
            BootConsole.WriteLine("[CONTEXT_MENU] initialized");
        } catch {
            // Popup initialization is optional: the desktop and input path must
            // remain usable if a menu allocation or dependent asset fails.
            RightMenu = null;
            widgetContextMenu = null;
            BootConsole.WriteLine("[CONTEXT_MENU] unavailable");
        }
    }

    /// <summary>
    /// Initialize the normal dockable widget subsystem.
    /// </summary>
    private static void SetupWidgets() {
        BootConsole.WriteLine("[SMAIN] Creating widgets");
        PerfWidget = null;
        ClockWidget = null;
        MonitorWidget = null;
        UptimeWidget = null;
        // Preserve the widget's existing uptime semantics in both boot paths.
        Uptime.BootTimeTicks = Timer.Ticks;
        WidgetsContainer = null;
        int widgetCount = 0;
        int widgetX = Framebuffer.Width - 220;
        int widgetY = 80;

#if !UEFI_DIAGNOSTIC_WIDGET_ONLY_CLOCK && !UEFI_DIAGNOSTIC_WIDGET_ONLY_MONITOR && !UEFI_DIAGNOSTIC_WIDGET_ONLY_UPTIME
        try {
            PerfWidget = new PerformanceWidget();
            PerfWidget.Visible = false;
            WindowManager.MoveToEnd(PerfWidget);
            widgetCount++;
            MarkUefiWidgetInitialization("PerformanceWidget", true, PerfWidget.X,
                PerfWidget.Y, PerfWidget.Width, PerfWidget.Height);
            widgetX = PerfWidget.X;
            widgetY = PerfWidget.Y + PerfWidget.Height + 10;
        } catch {
            PerfWidget = null;
            MarkUefiWidgetInitialization("PerformanceWidget", false, 0, 0, 0, 0);
            BootConsole.WriteLine("[WIDGETS] PerformanceWidget skipped");
        }
#endif

#if !UEFI_DIAGNOSTIC_WIDGET_ONLY_PERFORMANCE && !UEFI_DIAGNOSTIC_WIDGET_ONLY_MONITOR && !UEFI_DIAGNOSTIC_WIDGET_ONLY_UPTIME
        try {
            ClockWidget = new Clock(widgetX, widgetY);
            ClockWidget.Visible = false;
            WindowManager.MoveToEnd(ClockWidget);
            widgetCount++;
            MarkUefiWidgetInitialization("Clock", true, ClockWidget.X,
                ClockWidget.Y, ClockWidget.Width, ClockWidget.Height);
        } catch {
            ClockWidget = null;
            MarkUefiWidgetInitialization("Clock", false, 0, 0, 0, 0);
            BootConsole.WriteLine("[WIDGETS] Clock skipped");
        }
#endif

#if !UEFI_DIAGNOSTIC_WIDGET_ONLY_PERFORMANCE && !UEFI_DIAGNOSTIC_WIDGET_ONLY_CLOCK && !UEFI_DIAGNOSTIC_WIDGET_ONLY_UPTIME
        try {
            MonitorWidget = new Monitor();
            MonitorWidget.Visible = false;
            WindowManager.MoveToEnd(MonitorWidget);
            widgetCount++;
            MarkUefiWidgetInitialization("Monitor", true, MonitorWidget.X,
                MonitorWidget.Y, MonitorWidget.Width, MonitorWidget.Height);
        } catch {
            MonitorWidget = null;
            MarkUefiWidgetInitialization("Monitor", false, 0, 0, 0, 0);
            BootConsole.WriteLine("[WIDGETS] Monitor skipped");
        }
#endif

#if !UEFI_DIAGNOSTIC_WIDGET_ONLY_PERFORMANCE && !UEFI_DIAGNOSTIC_WIDGET_ONLY_CLOCK && !UEFI_DIAGNOSTIC_WIDGET_ONLY_MONITOR
        try {
            int uptimeY = widgetY + (ClockWidget == null ? 0 : ClockWidget.PreferredHeight) + 20;
            UptimeWidget = new Uptime(widgetX, uptimeY);
            UptimeWidget.Visible = false;
            WindowManager.MoveToEnd(UptimeWidget);
            widgetCount++;
            MarkUefiWidgetInitialization("Uptime", true, UptimeWidget.X,
                UptimeWidget.Y, UptimeWidget.Width, UptimeWidget.Height);
        } catch {
            UptimeWidget = null;
            MarkUefiWidgetInitialization("Uptime", false, 0, 0, 0, 0);
            BootConsole.WriteLine("[WIDGETS] Uptime skipped");
        }
#endif

        if (widgetCount == 0) {
            BootConsole.WriteLine("[WIDGETS] unavailable");
            MarkUefiWidgetsInitialized(0, false);
            return;
        }

        try {
            var widgetContainer = new WidgetContainer(Framebuffer.Width - 220, 80);
            if (PerfWidget != null) widgetContainer.AddWidget(PerfWidget);
            if (ClockWidget != null) widgetContainer.AddWidget(ClockWidget);
            if (MonitorWidget != null) widgetContainer.AddWidget(MonitorWidget);
            if (UptimeWidget != null) widgetContainer.AddWidget(UptimeWidget);

            if (IsUefiWidgetDiagnostic) {
                UISettings.EnableAutoHideWidgets = false;
                UISettings.EnableAutoHideWidgetsVisuals = false;
                widgetContainer.ShowWidgets();
            } else {
                widgetContainer.Visible = UISettings.ShowWidgetsOnStartup;
            }
            WindowManager.MoveToEnd(widgetContainer);
            WidgetsContainer = widgetContainer;

            if (!widgetContainer.Visible && UISettings.EnableWidgetVisibilityToggle) {
                var toggle = new WidgetToggleButton(Framebuffer.Width - 26, 6);
                WindowManager.MoveToEnd(toggle);
                toggle.Visible = true;
            }
            BootConsole.WriteLine("[WIDGETS] initialized");
            MarkUefiWidgetsInitialized(widgetCount, widgetContainer.Visible);
        } catch {
            WidgetsContainer = null;
            BootConsole.WriteLine("[WIDGETS] container unavailable");
            MarkUefiWidgetsInitialized(widgetCount, false);
        }
    }

    /// <summary>
    /// Setup the global Escape key handler shared by legacy and UEFI paths.
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

    private static void SerialText(string text) {
        if (text == null) return;
        for (int i = 0; i < text.Length; i++) {
            SerialChar(text[i]);
        }
    }

    private static void SerialUnsigned(ulong value) {
        char* digits = stackalloc char[20];
        int length = 0;
        do {
            digits[length++] = (char)('0' + (value % 10));
            value /= 10;
        } while (value != 0);
        while (length > 0) SerialChar(digits[--length]);
    }

    private static void SerialWidgetUpdate(string name, int count) {
        SerialText("WIDGET_UPDATE=");
        SerialText(name);
        SerialText(",count=");
        SerialUnsigned((ulong)count);
        SerialChar('\n');

        SerialText("WIDGET_UPDATE_STATE=timer=");
        SerialUnsigned(Timer.Ticks);
        SerialText(",locked=");
        SerialChar(ThreadPool.Locked ? '1' : '0');
        SerialText(",locker=");
        SerialUnsigned((ulong)ThreadPool.Locker);
        SerialChar('\n');
    }

    private static void SerialBreadcrumb(string breadcrumb) {
        if (breadcrumb == null) return;
        SerialText(breadcrumb);
        SerialChar('\n');
    }

    /// <summary>
    /// Main render loop - extracted from SMain to keep stack frames small.
    /// </summary>
    private static void HandleContextMenuOpening() {
        try {
            bool rightDown = (Control.MouseButtons & MouseButtons.Right) == MouseButtons.Right;
            bool rightPressed = MouseEventDispatcher.WasPressedThisFrame(MouseButtons.Right);
            if ((rightPressed || (rightDown && !RightClicked)) &&
                !WindowManager.MouseHandled) {
                RightClicked = true;
                if (RightMenu != null) {
                    CloseWidgetContextMenu();
                    if (Desktop.Taskbar != null) Desktop.Taskbar.CloseContextMenu();
                    int x = Control.MousePosition.X;
                    int y = Control.MousePosition.Y;
                    RightMenu.ShowAt(x, y);
                    MarkUefiContextMenuOpened(RightMenu.X, RightMenu.Y,
                        RightMenu.Width, RightMenu.Height);
                    MarkUefiWidgetDesktopMenuOpened(RightMenu.X, RightMenu.Y,
                        RightMenu.Width, RightMenu.Height);
                }
            }
            RightClicked = rightDown;
        } catch {
            RightClicked = false;
        }
    }

    private static void RenderLoop() {
        if (IsUefiMode) {
            _uefiMultiFrameActive = true;
            _uefiMultiFrameCurrentFrame = 0;
            _uefiMultiFrameLastCompletedFrame = 0;
            _uefiMultiFrameStage = 0;
            _uefiMultiFrameSubstage = 0;
            _uefiMultiFrameLastBoundary = 0;
            _uefiMultiFrameStackLowWater = Native.ReadRSP();
            _uefiMultiFrameLastCodeAddress = 0;
            _uefiMultiFrameStartTicks = GetUefiTimerTicks();

            SerialBreadcrumb("CONTINUOUS_DESKTOP_ENTER");
            SerialBreadcrumb("CONTINUOUS_DESKTOP_FRAME_PATH=RenderUefiDesktopFrame");
            SerialBreadcrumb("CONTINUOUS_DESKTOP_TIMER_START=" +
                _uefiMultiFrameStartTicks.ToString());

            int uefiFrame = 0;
            for (;;) {
                uefiFrame++;
                _uefiMultiFrameCurrentFrame = uefiFrame;
                try {
                    if (!RenderUefiDesktopFrame(uefiFrame)) {
                        SerialBreadcrumb("CONTINUOUS_DESKTOP_FAULT=FRAMEBUFFER_INVALID");
                        LogUefiMultiFrameFaultContext();
                        HaltAfterUefiContinuous();
                        return;
                    }
                    _uefiMultiFrameLastCompletedFrame = uefiFrame;
                    if (!EmitUefiContinuousHeartbeat(uefiFrame)) {
                        LogUefiMultiFrameFaultContext();
                        HaltAfterUefiContinuous();
                        return;
                    }
                    Thread.Sleep(16);
                } catch {
                    SerialBreadcrumb("CONTINUOUS_DESKTOP_FAULT=MANAGED_EXCEPTION");
                    LogUefiMultiFrameFaultContext();
                    HaltAfterUefiContinuous();
                    return;
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
                MouseEventDispatcher.BeginFrame();
                try { MouseEventDispatcher.Update(); } catch { }
                WindowManager.MouseHandled = false;
                try { WindowManager.InputAll(); } catch { }
                try { WindowManager.FlushPendingCreates(); } catch { }
                try { WAVPlayer.DoPlay(); } catch { }

                HandleContextMenuOpening();

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

        // Native IRQ handlers only place raw bytes in bounded queues. Drain
        // them before normal window/desktop input so all GUI routing remains
        // on this rendering thread.
        PS2Keyboard.ProcessPendingInput(128);
        MouseEventDispatcher.BeginFrame();
        PS2Mouse.ProcessPendingInput(256);
        WindowManager.MouseHandled = false;
        WindowManager.InputAll();
        WindowManager.FlushPendingCreates();

        SetUefiFrameBreadcrumb(1, 0, 100);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        BackgroundRotationManager.Update();
        BackgroundRotationManager.DrawBackground();

        SetUefiFrameBreadcrumb(1, 1, 101);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        SetUefiFrameBreadcrumb(2, 0, 200);
        _uefiMultiFrameLastCodeAddress = Native.ReadCallSite();
        Desktop.Update(_cachedDocumentIcon, _cachedFolderIcon,
            _cachedImageIcon, _cachedAudioIcon, 48);
        // Let taskbar hit regions claim right-clicks before the desktop popup
        // is opened. Both routes still use the normal Window-based menus.
        HandleContextMenuOpening();
        EmitUefiRealIconMarker(graphics, frameCounter);

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

    private static void EmitUefiRealIconMarker(guideXOS.Graph.Graphics graphics, int frameCounter) {
        if (!IsUefiMode || _uefiRealIconMarkerEmitted || frameCounter != 1) return;
        _uefiRealIconMarkerEmitted = true;

        Image icon = _cachedFolderIcon;
        if (icon == null || icon.RawData == null) {
            SerialBreadcrumb("DESKTOP_REAL_ICON_RENDERED=0");
            return;
        }

        int sampleX = -1;
        int sampleY = -1;
        uint imagePixel = 0;
        for (int y = 0; y < icon.Height && sampleX < 0; y++) {
            for (int x = 0; x < icon.Width; x++) {
                uint pixel = icon.GetPixel(x, y);
                if ((byte)(pixel >> 24) != 0) {
                    sampleX = x;
                    sampleY = y;
                    imagePixel = pixel;
                    break;
                }
            }
        }

        if (sampleX < 0 || graphics == null || graphics.VideoMemory == null) {
            SerialBreadcrumb("DESKTOP_REAL_ICON_RENDERED=0");
            return;
        }

        // UEFI keeps the recovered tile at (48,96) and places the 48x48
        // image at (56,104).  Check the first nontransparent source pixel
        // against the canonical framebuffer result after Desktop.Update.
        uint framebufferPixel = graphics.GetPoint(56 + sampleX, 104 + sampleY);
        SerialBreadcrumb("DESKTOP_REAL_ICON_SAMPLE=" + imagePixel.ToString());
        SerialBreadcrumb("DESKTOP_REAL_ICON_FRAMEBUFFER_SAMPLE=" + framebufferPixel.ToString());
        SerialBreadcrumb("DESKTOP_REAL_ICON_RENDERED=" +
            (framebufferPixel != 0xFF263241u ? "1" : "0"));
    }

    private static bool ShouldEmitUefiContinuousHeartbeat(int frame) {
        return frame == UEFI_CONTINUOUS_HEARTBEAT_FIRST ||
               frame == UEFI_CONTINUOUS_HEARTBEAT_EARLY_1 ||
               frame == UEFI_CONTINUOUS_HEARTBEAT_EARLY_2 ||
               frame == UEFI_CONTINUOUS_HEARTBEAT_EARLY_3 ||
               (frame > UEFI_CONTINUOUS_HEARTBEAT_EARLY_3 &&
                frame % UEFI_CONTINUOUS_HEARTBEAT_INTERVAL == 0);
    }

    private static bool EmitUefiContinuousHeartbeat(int frame) {
        if (!ShouldEmitUefiContinuousHeartbeat(frame)) return true;

        ulong rsp = Native.ReadRSP();
        if (_uefiMultiFrameStackLowWater == 0 || rsp < _uefiMultiFrameStackLowWater)
            _uefiMultiFrameStackLowWater = rsp;

        bool graphicsValid = IsUefiGraphicsInvariantValid();
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_FRAME=" + frame.ToString());
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_TIMER=" +
            GetUefiTimerTicks().ToString());
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_STACK_LOW_WATER=" +
            _uefiMultiFrameStackLowWater.ToString());
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_GRAPHICS_VALID=" +
            (graphicsValid ? "1" : "0"));
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_ALLOCATOR_BYTES=" +
            Allocator.MemoryInUse.ToString());
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_ALLOCATOR_FREE_CALLS=" +
            Allocator.FreeCallCount.ToString());
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_ALLOCATOR_FREE_SUCCESS=" +
            Allocator.FreeSuccessCount.ToString());
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_ALLOCATOR_FREE_INVALID=" +
            Allocator.FreeFailInvalidPtr.ToString());
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_ALLOCATOR_FREE_NOPAGES=" +
            Allocator.FreeFailNoPages.ToString());
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_ALLOCATOR_FREE_CORRUPT=" +
            Allocator.FreeFailCorruptRun.ToString());
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_THREADPOOL_LOCKED=" +
            (ThreadPool.Locked ? "1" : "0"));
        SerialBreadcrumb("CONTINUOUS_HEARTBEAT_THREADPOOL_LOCKER=" +
            ThreadPool.Locker.ToString());
        if (PS2Keyboard.IrqCount != 0 || PS2Mouse.InterruptCount != 0) {
            SerialBreadcrumb("INPUT_STATS_KEY_IRQ=" + PS2Keyboard.IrqCount.ToString());
            SerialBreadcrumb("INPUT_STATS_KEY_DROPPED=" + PS2Keyboard.DroppedScancodeCount.ToString());
            SerialBreadcrumb("INPUT_STATS_KEY_DOWN=" + PS2Keyboard.KeyDownCount.ToString());
            SerialBreadcrumb("INPUT_STATS_KEY_UP=" + PS2Keyboard.KeyUpCount.ToString());
            SerialBreadcrumb("INPUT_STATS_MOUSE_IRQ=" + PS2Mouse.InterruptCount.ToString());
            SerialBreadcrumb("INPUT_STATS_MOUSE_DROPPED=" + PS2Mouse.DroppedByteCount.ToString());
            SerialBreadcrumb("INPUT_STATS_MOUSE_PACKETS=" + PS2Mouse.ProcessedPacketCount.ToString());
            SerialBreadcrumb("INPUT_STATS_MOUSE_MOVES=" + PS2Mouse.MoveEventCount.ToString());
            SerialBreadcrumb("INPUT_STATS_MOUSE_LEFT_DOWN=" + PS2Mouse.LeftDownCount.ToString());
            SerialBreadcrumb("INPUT_STATS_MOUSE_LEFT_UP=" + PS2Mouse.LeftUpCount.ToString());
            SerialBreadcrumb("INPUT_STATS_MOUSE_RIGHT_DOWN=" + PS2Mouse.RightDownCount.ToString());
            SerialBreadcrumb("INPUT_STATS_MOUSE_RIGHT_UP=" + PS2Mouse.RightUpCount.ToString());
        }
        if (!graphicsValid) {
            SerialBreadcrumb("CONTINUOUS_DESKTOP_FAULT=GRAPHICS_INVARIANT");
        }
        return graphicsValid;
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

    private static void HaltAfterUefiContinuous() {
        SerialBreadcrumb("CONTINUOUS_DESKTOP_HALT_ENTER");
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

    private static void RenderLoopUefiAppModelDiagnostic() {
        string failure = null;
        try {
            SerialBreadcrumb("APP_MODEL_BEGIN");
            if (Desktop.Apps == null) {
                failure = "APP_COLLECTION_UNAVAILABLE";
            } else if (!AppLaunchResolver.RunSelfTest()) {
                failure = "APP_RESOLUTION";
            } else if (!FileAssociationRegistry.RunSelfTest()) {
                failure = "FILE_ASSOCIATION";
            } else if (!ShellObjectRegistry.RunSelfTest()) {
                failure = "SHELL_OBJECT";
            } else if (!ApplicationDescriptorRegistry.RunSelfTest()) {
                failure = "MODERN_DESCRIPTOR_REQUEST";
            } else if (!ApplicationInstanceRegistry.RunSelfTest()) {
                failure = "APPLICATION_INSTANCE_LIFECYCLE";
            } else if (!ApplicationInstanceRegistry.RunLifecycleSelfTest()) {
                failure = "APPLICATION_LIFECYCLE_PHASE6";
            } else if (!TaskbarApplicationEntryRegistry.RunSelfTest()) {
                failure = "TASKBAR_APPLICATION_GROUPING";
            } else if (!ApplicationFactoryRegistry.RunSelfTest()) {
                failure = "APPLICATION_FACTORY";
            } else if (!AppModelCompatibilityDiagnostics.RunSelfTest(
                    Desktop.Apps)) {
                failure = "COMPATIBILITY_FACADE";
            } else {
                SerialBreadcrumb("APP_MODEL_APP_COUNT=" + Desktop.Apps.Length.ToString());
                SerialBreadcrumb("APP_MODEL_MODERN_DESCRIPTOR_COUNT=" +
                    ApplicationDescriptorRegistry.Count.ToString());
                SerialBreadcrumb("APP_MODEL_MODERN_DESCRIPTOR_VALID=" +
                    (ApplicationDescriptorRegistry.IsValid ? "1" : "0"));
                SerialBreadcrumb("APP_MODEL_ALIAS_OK=" +
                    (AppLaunchResolver.Resolve("File Explorer").Success ? "1" : "0"));
                SerialBreadcrumb("APP_MODEL_ASSOCIATION_OK=" +
                    (FileAssociationRegistry.ResolvePath("README.TXT").Success ? "1" : "0"));
                SerialBreadcrumb("APP_MODEL_SHELL_OK=" +
                    (ShellObjectRegistry.Resolve("USB Drive 0").Success ? "1" : "0"));
                SerialBreadcrumb("APP_MODEL_MODERN_REQUEST_OK=1");
                SerialBreadcrumb("APP_MODEL_INSTANCE_CAPACITY=" +
                    ApplicationInstanceRegistry.Capacity.ToString());
                SerialBreadcrumb("APP_MODEL_INSTANCE_CREATED=" +
                    ApplicationInstanceRegistry.InstancesCreated.ToString());
                SerialBreadcrumb("APP_MODEL_INSTANCE_REUSED=" +
                    ApplicationInstanceRegistry.InstancesReused.ToString());
                SerialBreadcrumb("APP_MODEL_INSTANCE_TERMINATED=" +
                    ApplicationInstanceRegistry.InstancesTerminated.ToString());
                SerialBreadcrumb("APP_MODEL_INSTANCE_ACTIVE=" +
                    ApplicationInstanceRegistry.ActiveCount.ToString());
                SerialBreadcrumb("APP_MODEL_INSTANCE_ATTACH=" +
                    ApplicationInstanceRegistry.WindowAttachCount.ToString());
                SerialBreadcrumb("APP_MODEL_INSTANCE_DETACH=" +
                    ApplicationInstanceRegistry.WindowDetachCount.ToString());
                SerialBreadcrumb("APP_MODEL_INSTANCE_STALE=" +
                    ApplicationInstanceRegistry.StaleOwnershipCount.ToString());
                SerialBreadcrumb("APP_MODEL_INSTANCE_SELFTEST_OK=1");
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_ACTIVE=" +
                    ApplicationInstanceRegistry.ActiveApplicationHandle.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_SUSPENDED=" +
                    ApplicationInstanceRegistry.SuspendedCount.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_ACTIVATIONS=" +
                    ApplicationInstanceRegistry.InstancesActivated.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_DEACTIVATIONS=" +
                    ApplicationInstanceRegistry.InstancesDeactivated.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_SUSPENDS=" +
                    ApplicationInstanceRegistry.InstancesSuspended.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_RESUMES=" +
                    ApplicationInstanceRegistry.InstancesResumed.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_CLOSE_REQUESTS=" +
                    ApplicationInstanceRegistry.CloseRequests.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_CLOSE_CANCELLATIONS=" +
                    ApplicationInstanceRegistry.CloseCancellations.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_FAILURES=" +
                    ApplicationInstanceRegistry.LifecycleFailures.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_INVALID=" +
                    ApplicationInstanceRegistry.InvalidLifecycleRequests.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_STALE_HANDLES=" +
                    ApplicationInstanceRegistry.StaleLifecycleHandles.ToString());
                SerialBreadcrumb("APP_MODEL_LIFECYCLE_SELFTEST_OK=1");
                SerialBreadcrumb("APP_MODEL_FACTORY_REGISTRATIONS=" +
                    ApplicationFactoryRegistry.FactoryRegistrations.ToString());
                SerialBreadcrumb("APP_MODEL_FACTORY_LAUNCHES=" +
                    ApplicationFactoryRegistry.FactoryLaunches.ToString());
                SerialBreadcrumb("APP_MODEL_FACTORY_FALLBACKS=" +
                    ApplicationFactoryRegistry.CompatibilityFallbackLaunches.ToString());
                SerialBreadcrumb("APP_MODEL_FACTORY_FAILURES=" +
                    ApplicationFactoryRegistry.FactoryFailures.ToString());
                SerialBreadcrumb("APP_MODEL_FACTORY_REUSED_ACTIVATIONS=" +
                    ApplicationFactoryRegistry.ReusedFactoryActivations.ToString());
                SerialBreadcrumb("APP_MODEL_FACTORY_WINDOWS_ATTACHED=" +
                    ApplicationFactoryRegistry.FactoryWindowsAttached.ToString());
                SerialBreadcrumb("APP_MODEL_TYPED_EXTERNAL_LAUNCHES=" +
                    ApplicationFactoryRegistry.TypedExternalLaunches.ToString());
                SerialBreadcrumb("APP_MODEL_TYPED_SHELL_ACTION_LAUNCHES=" +
                    ApplicationFactoryRegistry.TypedShellActionLaunches.ToString());
                SerialBreadcrumb("APP_MODEL_COMPAT_FACADE_CALLS=" +
                    AppModelCompatibilityDiagnostics.FacadeCalls.ToString());
                SerialBreadcrumb("APP_MODEL_COMPAT_MODERN_TRANSLATIONS=" +
                    AppModelCompatibilityDiagnostics.ModernTranslations.ToString());
                SerialBreadcrumb("APP_MODEL_COMPAT_LEGACY_BACKEND_CALLS=" +
                    AppModelCompatibilityDiagnostics.LegacyBackendCalls.ToString());
                SerialBreadcrumb("APP_MODEL_COMPAT_FAILURES=" +
                    AppModelCompatibilityDiagnostics.CompatibilityFailures.ToString());
                SerialBreadcrumb("APP_MODEL_COMPAT_SELFTEST_OK=1");
                SerialBreadcrumb("APP_MODEL_FACTORY_SELFTEST_OK=1");
            }
        } catch {
            failure = "EXCEPTION";
        }

        if (failure == null) SerialBreadcrumb("APP_MODEL_COMPLETE");
        else SerialBreadcrumb("APP_MODEL_FAIL=" + failure);
        SerialBreadcrumb("APP_MODEL_HALT_ENTER");
        for (;;) Native.Hlt();
    }

    /// <summary>
    /// Bounded proof of the normal managed wallpaper path. It probes the
    /// selected asset, then renders the manager-owned scaled image through the
    /// canonical framebuffer Graphics object.
    /// </summary>
    private static void RenderLoopUefiBackgroundProbe() {
        byte[] data = null;
        byte[] invalidData = null;
        byte[] impossibleDimensions = null;
        byte[] truncatedData = null;
        Image decoded = null;
        Image rejected = null;
        string failure = null;

        try {
            SerialBreadcrumb("BACKGROUND_PROBE_BEGIN");
            string path = BackgroundRotationManager.CurrentBackgroundPath;
            SerialBreadcrumb("BACKGROUND_PROBE_FILE=" + (path == null ? "NONE" : path));
            SerialBreadcrumb("BACKGROUND_MANAGER_INITIALIZED=" +
                (BackgroundRotationManager.IsInitialized ? "1" : "0"));
            SerialBreadcrumb("BACKGROUND_MANAGER_COUNT=" +
                BackgroundRotationManager.GetBackgroundCount().ToString());

            if (path == null || File.Instance == null) {
                failure = "FILE_UNAVAILABLE";
            } else {
                data = File.Instance.ReadAllBytes(path);
                if (data == null || data.Length == 0) failure = "FILE_READ";
            }

            int width = 0;
            int height = 0;
            byte bitDepth = 0;
            byte colorType = 0;
            byte interlace = 0;
            if (failure == null) {
                SerialBreadcrumb("BACKGROUND_PROBE_BYTES=" + data.Length.ToString());
                if (!PngLoader.TryGetPngInfo(data, out width, out height,
                    out bitDepth, out colorType, out interlace)) {
                    failure = "FORMAT";
                } else {
                    SerialBreadcrumb("BACKGROUND_PROBE_DIMENSIONS=" +
                        width.ToString() + "x" + height.ToString());
                    SerialBreadcrumb("BACKGROUND_PROBE_FORMAT=" +
                        (colorType == 2 ? "RGB8" : "RGBA8"));
                    SerialBreadcrumb("BACKGROUND_PROBE_ALPHA=" +
                        (colorType == 6 ? "PRESENT" : "NONE"));
                }
            }

            if (failure == null) {
                if (!PngLoader.Initialize() || !PngLoader.Load(data, out decoded) ||
                    decoded == null || decoded.RawData == null) {
                    failure = "DECODE";
                } else {
                    bool rgbOpaque = colorType != 2;
                    if (colorType == 2) {
                        rgbOpaque = true;
                        for (int y = 0; y < decoded.Height && rgbOpaque; y++) {
                            for (int x = 0; x < decoded.Width; x++) {
                                if ((byte)(decoded.GetPixel(x, y) >> 24) != 0xFF) {
                                    rgbOpaque = false;
                                    break;
                                }
                            }
                        }
                    }
                    SerialBreadcrumb("BACKGROUND_PROBE_RGB_OPAQUE=" +
                        (rgbOpaque ? "1" : "0"));
                    SerialBreadcrumb("BACKGROUND_PROBE_DECODE_OK");
                    if (!rgbOpaque) failure = "RGB_ALPHA";
                }
            }

            if (failure == null) {
                Image scaled = decoded.ResizeImage(Framebuffer.Width, Framebuffer.Height);
                if (scaled == null || scaled.RawData == null ||
                    scaled.Width != Framebuffer.Width ||
                    scaled.Height != Framebuffer.Height) {
                    if (scaled != null) scaled.Dispose();
                    failure = "SCALE";
                } else {
                    SerialBreadcrumb("BACKGROUND_PROBE_SCALE_DIMENSIONS=" +
                        scaled.Width.ToString() + "x" + scaled.Height.ToString());
                    scaled.Dispose();
                    SerialBreadcrumb("BACKGROUND_PROBE_SCALE_OK");
                }
            }

            if (failure == null) {
                guideXOS.Graph.Graphics graphics = Framebuffer.Graphics;
                Image active = Wallpaper;
                if (graphics == null || active == null || active.RawData == null ||
                    active.Width != Framebuffer.Width || active.Height != Framebuffer.Height) {
                    failure = "ACTIVE_BACKGROUND";
                } else {
                    graphics.Clear(0xFF0D7D77u);
                    BackgroundRotationManager.DrawBackground();
                    int centerX = Framebuffer.Width / 2;
                    int centerY = Framebuffer.Height / 2;
                    int lowerX = Framebuffer.Width - 1;
                    int lowerY = Framebuffer.Height - 1;
                    uint expectedUpper = active.GetPixel(0, 0);
                    uint expectedCenter = active.GetPixel(centerX, centerY);
                    uint expectedLower = active.GetPixel(lowerX, lowerY);
                    uint actualUpper = graphics.GetPoint(0, 0);
                    uint actualCenter = graphics.GetPoint(centerX, centerY);
                    uint actualLower = graphics.GetPoint(lowerX, lowerY);
                    SerialBreadcrumb("BACKGROUND_PROBE_PIXEL_UPPER_LEFT=" + actualUpper.ToString());
                    SerialBreadcrumb("BACKGROUND_PROBE_PIXEL_CENTER=" + actualCenter.ToString());
                    SerialBreadcrumb("BACKGROUND_PROBE_PIXEL_LOWER_RIGHT=" + actualLower.ToString());
                    bool renderOk = expectedUpper == actualUpper &&
                                    expectedCenter == actualCenter &&
                                    expectedLower == actualLower;
                    SerialBreadcrumb("BACKGROUND_PROBE_RENDER_OK=" +
                        (renderOk ? "1" : "0"));
                    Framebuffer.Update();
                    if (!renderOk) failure = "RENDER";
                }
            }

            // Negative controls exercise the same background candidate path;
            // none is published, so the valid active image remains intact.
            if (failure == null) {
                bool missingRejected = !BackgroundRotationManager.TryLoadPathForDiagnostic(
                    "Backgrounds/__missing_wallpaper__.png", out rejected);
                if (rejected != null) rejected.Dispose();
                rejected = null;
                SerialBreadcrumb("BACKGROUND_NEGATIVE_MISSING=" +
                    (missingRejected ? "PASS" : "FAIL"));
                if (!missingRejected) failure = "NEGATIVE_MISSING";
            }

            if (failure == null) {
                invalidData = new byte[8];
                bool invalidRejected = !BackgroundRotationManager.TryLoadDataForDiagnostic(
                    invalidData, out rejected);
                if (rejected != null) rejected.Dispose();
                rejected = null;
                invalidData.Dispose();
                invalidData = null;
                SerialBreadcrumb("BACKGROUND_NEGATIVE_INVALID=" +
                    (invalidRejected ? "PASS" : "FAIL"));
                if (!invalidRejected) failure = "NEGATIVE_INVALID";
            }

            if (failure == null) {
                impossibleDimensions = new byte[data.Length];
                for (int i = 0; i < data.Length; i++) impossibleDimensions[i] = data[i];
                impossibleDimensions[16] = 0x00;
                impossibleDimensions[17] = 0x20;
                impossibleDimensions[18] = 0x00;
                impossibleDimensions[19] = 0x00;
                bool impossibleRejected = !BackgroundRotationManager.TryLoadDataForDiagnostic(
                    impossibleDimensions, out rejected);
                if (rejected != null) rejected.Dispose();
                rejected = null;
                impossibleDimensions.Dispose();
                impossibleDimensions = null;
                SerialBreadcrumb("BACKGROUND_NEGATIVE_DIMENSIONS=" +
                    (impossibleRejected ? "PASS" : "FAIL"));
                if (!impossibleRejected) failure = "NEGATIVE_DIMENSIONS";
            }

            if (failure == null) {
                int truncatedLength = data.Length > 16 ? data.Length - 16 : 0;
                truncatedData = new byte[truncatedLength];
                for (int i = 0; i < truncatedLength; i++) truncatedData[i] = data[i];
                bool failedDecodeRejected = !BackgroundRotationManager.TryLoadDataForDiagnostic(
                    truncatedData, out rejected);
                if (rejected != null) rejected.Dispose();
                rejected = null;
                truncatedData.Dispose();
                truncatedData = null;
                SerialBreadcrumb("BACKGROUND_NEGATIVE_DECODE=" +
                    (failedDecodeRejected ? "PASS" : "FAIL"));
                if (!failedDecodeRejected) failure = "NEGATIVE_DECODE";
            }
        } catch {
            failure = "EXCEPTION";
        }

        if (rejected != null) rejected.Dispose();
        if (decoded != null) decoded.Dispose();
        if (data != null) data.Dispose();
        if (invalidData != null) invalidData.Dispose();
        if (impossibleDimensions != null) impossibleDimensions.Dispose();
        if (truncatedData != null) truncatedData.Dispose();

        if (failure == null) SerialBreadcrumb("BACKGROUND_PROBE_COMPLETE");
        else SerialBreadcrumb("BACKGROUND_PROBE_FAIL=" + failure);
        HaltAfterUefiBackgroundProbe();
    }

    private static void HaltAfterUefiBackgroundProbe() {
        SerialBreadcrumb("BACKGROUND_PROBE_HALT_ENTER");
        for (;;) Native.Hlt();
    }

    /// <summary>
    /// Bounded rotation proof. Each candidate is decoded and published before
    /// the next frame is rendered, while the ordinary desktop/input sequence
    /// remains active.
    /// </summary>
    private static void RenderLoopUefiBackgroundRotation() {
        string failure = null;
        int successful = 0;
        try {
            SerialBreadcrumb("BACKGROUND_ROTATION_BEGIN");
            string initial = BackgroundRotationManager.CurrentBackgroundPath;
            SerialBreadcrumb("BACKGROUND_ROTATION_INITIAL=" +
                (initial == null ? "NONE" : initial));
            if (initial == null || BackgroundRotationManager.GetBackgroundCount() < 2) {
                failure = "INSUFFICIENT_ASSETS";
            }

            for (int transition = 0; failure == null && transition < 5; transition++) {
                string before = BackgroundRotationManager.CurrentBackgroundPath;
                if (!BackgroundRotationManager.ForceRotateNext()) {
                    failure = "LOAD";
                    break;
                }

                string next = BackgroundRotationManager.CurrentBackgroundPath;
                SerialBreadcrumb("BACKGROUND_ROTATION_NEXT=" +
                    (next == null ? "NONE" : next));
                if (next == null || next == before || Wallpaper == null ||
                    Wallpaper.RawData == null || Wallpaper.Width != Framebuffer.Width ||
                    Wallpaper.Height != Framebuffer.Height) {
                    failure = "STATE";
                    break;
                }

                _uefiMultiFrameCurrentFrame = transition + 1;
                if (!RenderUefiDesktopFrame(transition + 1)) {
                    failure = "FRAME";
                    break;
                }

                uint expected = Wallpaper.GetPixel(0, 0);
                uint actual = Framebuffer.Graphics.GetPoint(0, 0);
                bool rendered = expected == actual;
                SerialBreadcrumb("BACKGROUND_ROTATION_RENDER_OK=" +
                    (rendered ? "1" : "0"));
                if (!rendered) {
                    failure = "RENDER";
                    break;
                }
                bool textRendered = HasRenderedUefiDesktopLabel();
                SerialBreadcrumb("BACKGROUND_ROTATION_TEXT_RENDER_OK=" +
                    (textRendered ? "1" : "0"));
                if (!textRendered) {
                    failure = "TEXT";
                    break;
                }
                successful++;
                SerialBreadcrumb("BACKGROUND_ROTATION_CHANGE_OK=1");
            }

            SerialBreadcrumb("BACKGROUND_ROTATION_SUCCESS_COUNT=" + successful.ToString());
        } catch {
            failure = "EXCEPTION";
        }

        if (failure == null && successful == 5) {
            SerialBreadcrumb("BACKGROUND_ROTATION_COMPLETE");
        } else {
            SerialBreadcrumb("BACKGROUND_ROTATION_FAIL=" +
                (failure == null ? "COUNT" : failure));
        }
        HaltAfterUefiBackgroundRotation();
    }

    private static bool HasRenderedUefiDesktopLabel() {
        if (!IsUefiMode || WindowManager.font == null ||
            !WindowManager.RealFontEnabled || Wallpaper == null ||
            Wallpaper.RawData == null || Framebuffer.Graphics == null) {
            return false;
        }

        const int tileX = 48;
        const int tileY = 96;
        const int tileSize = 64;
        const string label = "FILES";
        int labelWidth = WindowManager.font.MeasureString(label);
        int labelHeight = WindowManager.font.FontSize;
        int labelX = tileX + ((tileSize - labelWidth) / 2);
        int labelY = tileY + tileSize + 8;
        if (labelWidth <= 0 || labelHeight <= 0 || labelX < 0 || labelY < 0 ||
            labelX > Framebuffer.Width - labelWidth ||
            labelY > Framebuffer.Height - labelHeight) {
            return false;
        }

        for (int y = 0; y < labelHeight; y++) {
            for (int x = 0; x < labelWidth; x++) {
                int px = labelX + x;
                int py = labelY + y;
                if (Framebuffer.Graphics.GetPoint(px, py) != Wallpaper.GetPixel(px, py)) {
                    return true;
                }
            }
        }
        return false;
    }

    private static void HaltAfterUefiBackgroundRotation() {
        SerialBreadcrumb("BACKGROUND_ROTATION_HALT_ENTER");
        for (;;) Native.Hlt();
    }

    /// <summary>
    /// Bounded proof of the normal guideXOS bitmap-atlas font path. The probe
    /// renders through IFont.DrawString into Framebuffer.Graphics and compares
    /// the changed pixels with the IFont measurement; it does not introduce a
    /// diagnostic bitmap renderer.
    /// </summary>
    private static void RenderLoopUefiFontProbe() {
        string failure = null;
        const string text = "guideXOS";
        const uint probeBackground = 0xFF102030u;
        try {
            SerialBreadcrumb("FONT_PROBE_BEGIN");
            SerialBreadcrumb("FONT_PROBE_RESOURCE=" + WindowManager.FontResourcePath);
            SerialBreadcrumb("FONT_PROBE_BYTES=" + WindowManager.FontResourceBytes.ToString());

            IFont probeFont = WindowManager.font;
            bool initialized = WindowManager.RealFontEnabled &&
                               !WindowManager.FontUsingFallback &&
                               probeFont != null && probeFont.IsValid;
            if (!initialized) {
                failure = "INITIALIZATION";
            } else {
                SerialBreadcrumb("FONT_PROBE_INIT_OK");

                bool glyphA = probeFont.HasGlyph('A') && probeFont.GlyphHasPixels('A');
                bool glyphG = probeFont.HasGlyph('g') && probeFont.GlyphHasPixels('g');
                bool glyph0 = probeFont.HasGlyph('0') && probeFont.GlyphHasPixels('0');
                bool glyphSpace = probeFont.HasGlyph(' ');
                bool glyphPunctuation = probeFont.HasGlyph('.') && probeFont.GlyphHasPixels('.');
                SerialBreadcrumb("FONT_PROBE_GLYPH_A_OK=" + (glyphA ? "1" : "0"));
                SerialBreadcrumb("FONT_PROBE_GLYPH_g_OK=" + (glyphG ? "1" : "0"));
                SerialBreadcrumb("FONT_PROBE_GLYPH_0_OK=" + (glyph0 ? "1" : "0"));
                SerialBreadcrumb("FONT_PROBE_GLYPH_SPACE_OK=" + (glyphSpace ? "1" : "0"));
                SerialBreadcrumb("FONT_PROBE_GLYPH_PUNCT_OK=" +
                    (glyphPunctuation ? "1" : "0"));
                if (!glyphA || !glyphG || !glyph0 || !glyphSpace || !glyphPunctuation) {
                    failure = "GLYPH";
                }

                int measuredWidth = probeFont.MeasureString(text);
                int measuredHeight = probeFont.FontSize;
                SerialBreadcrumb("FONT_PROBE_MEASURE_WIDTH=" + measuredWidth.ToString());
                SerialBreadcrumb("FONT_PROBE_MEASURE_HEIGHT=" + measuredHeight.ToString());
                bool measureOk = measuredWidth > 0 && measuredHeight > 0;
                SerialBreadcrumb("FONT_PROBE_MEASURE_OK=" + (measureOk ? "1" : "0"));
                if (!measureOk && failure == null) failure = "MEASURE";

                guideXOS.Graph.Graphics graphics = Framebuffer.Graphics;
                int probeX = 32;
                int probeY = 32;
                int probeWidth = measuredWidth + probeFont.FontSize + 4;
                if (graphics == null || graphics.VideoMemory == null ||
                    probeWidth <= 0 || probeX + probeWidth > Framebuffer.Width ||
                    probeY + measuredHeight > Framebuffer.Height) {
                    failure = failure ?? "FRAMEBUFFER";
                } else {
                    graphics.FillRectangle(probeX, probeY, probeWidth,
                        measuredHeight, probeBackground);
                    SerialBreadcrumb("FONT_RENDER_TEXT=" + text);
                    probeFont.DrawString(probeX, probeY, text, graphics);

                    int minX = probeX + probeWidth;
                    int minY = probeY + measuredHeight;
                    int maxX = probeX - 1;
                    int maxY = probeY - 1;
                    int changed = 0;
                    for (int y = probeY; y < probeY + measuredHeight; y++) {
                        for (int x = probeX; x < probeX + probeWidth; x++) {
                            if (graphics.GetPoint(x, y) == probeBackground) continue;
                            changed++;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }

                    int renderedWidth = maxX >= minX ? maxX - minX + 1 : 0;
                    int renderedHeight = maxY >= minY ? maxY - minY + 1 : 0;
                    bool inside = changed > 0 && minX >= probeX &&
                                  maxX < probeX + measuredWidth &&
                                  minY >= probeY && maxY < probeY + measuredHeight;
                    int outsideX = probeX + measuredWidth;
                    bool outside = outsideX < probeX + probeWidth &&
                                   graphics.GetPoint(outsideX,
                                       probeY + measuredHeight / 2) == probeBackground;
                    bool renderOk = changed > 0 && renderedWidth <= measuredWidth &&
                                    renderedHeight <= measuredHeight && inside && outside;
                    SerialBreadcrumb("FONT_RENDER_NONBACKGROUND_PIXELS=" + changed.ToString());
                    SerialBreadcrumb("FONT_RENDER_BOUNDS=" + renderedWidth.ToString() +
                        "x" + renderedHeight.ToString());
                    SerialBreadcrumb("FONT_RENDER_INSIDE_BOUNDS=" + (inside ? "1" : "0"));
                    SerialBreadcrumb("FONT_RENDER_OUTSIDE_UNTOUCHED=" +
                        (outside ? "1" : "0"));
                    SerialBreadcrumb("FONT_RENDER_OK=" + (renderOk ? "1" : "0"));
                    if (!renderOk && failure == null) failure = "RENDER";
                }
            }
        } catch {
            failure = failure ?? "EXCEPTION";
        }

        if (failure == null) SerialBreadcrumb("FONT_PROBE_COMPLETE");
        else SerialBreadcrumb("FONT_PROBE_FAIL=" + failure);
        HaltAfterUefiFontProbe();
    }

    private static void HaltAfterUefiFontProbe() {
        SerialBreadcrumb("FONT_PROBE_HALT_ENTER");
        for (;;) Native.Hlt();
    }

    private static bool HasVisiblePixels(Image image) {
        if (image == null || image.RawData == null || image.Width <= 0 || image.Height <= 0) {
            return false;
        }
        for (int y = 0; y < image.Height; y++) {
            for (int x = 0; x < image.Width; x++) {
                if ((byte)(image.GetPixel(x, y) >> 24) != 0) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Bounded post-EBS proof of the managed PNG path.  This is selected only
    /// by the opt-in UEFI Png diagnostic build and never by normal boot.
    /// </summary>
    private static void RenderLoopUefiPngProbe() {
        const string path = "Images/BlueVelvet/48/folder.png";
        byte[] data = null;
        byte[] invalidSignature = null;
        byte[] truncatedHeader = null;
        byte[] impossibleDimensions = null;
        byte[] truncatedStream = null;
        Image decoded = null;
        string failure = null;

        try {
            SerialBreadcrumb("PNG_PROBE_BEGIN");
            SerialBreadcrumb("PNG_PROBE_FILE=" + path);

            if (File.Instance == null) {
                failure = "FILESYSTEM_UNAVAILABLE";
            } else {
                // RdskFS returns an owned copy.  The decoded Image below must
                // remain valid after this temporary file buffer is released.
                data = File.Instance.ReadAllBytes(path);
                if (data == null || data.Length == 0) {
                    failure = "FILE_READ";
                }
            }

            if (failure == null) {
                SerialBreadcrumb("PNG_PROBE_BYTES=" + data.Length.ToString());
                int width;
                int height;
                if (!PngLoader.ValidateSignatureAndParseIHDR(data, out width, out height)) {
                    failure = "IHDR";
                } else {
                    SerialBreadcrumb("PNG_PROBE_DIMENSIONS=" + width.ToString() + "x" + height.ToString());
                }
            }

            if (failure == null) {
                if (!PngLoader.Initialize() || !PngLoader.Load(data, out decoded) ||
                    decoded == null || decoded.RawData == null) {
                    failure = "DECODE";
                }
            }

            if (failure == null) {
                SerialBreadcrumb("PNG_PROBE_DECODE_OK");

                int transparentX = -1;
                int transparentY = -1;
                int partialX = -1;
                int partialY = -1;
                int opaqueX = -1;
                int opaqueY = -1;
                for (int y = 0; y < decoded.Height; y++) {
                    for (int x = 0; x < decoded.Width; x++) {
                        byte alpha = (byte)(decoded.GetPixel(x, y) >> 24);
                        if (alpha == 0 && transparentX < 0) {
                            transparentX = x;
                            transparentY = y;
                        } else if (alpha > 0 && alpha < 255 && partialX < 0) {
                            partialX = x;
                            partialY = y;
                        } else if (alpha == 255 && opaqueX < 0) {
                            opaqueX = x;
                            opaqueY = y;
                        }
                    }
                }

                SerialBreadcrumb("PNG_PROBE_ALPHA_TRANSPARENT=" + (transparentX >= 0 ? "1" : "0"));
                SerialBreadcrumb("PNG_PROBE_ALPHA_PARTIAL=" + (partialX >= 0 ? "1" : "0"));
                SerialBreadcrumb("PNG_PROBE_ALPHA_OPAQUE=" + (opaqueX >= 0 ? "1" : "0"));

                int sampleX = partialX >= 0 ? partialX : (opaqueX >= 0 ? opaqueX : 0);
                int sampleY = partialX >= 0 ? partialY : (opaqueY >= 0 ? opaqueY : 0);
                uint pixelSample = decoded.GetPixel(sampleX, sampleY);
                SerialBreadcrumb("PNG_PROBE_PIXEL_SAMPLE=" + pixelSample.ToString());

                guideXOS.Graph.Graphics graphics = Framebuffer.Graphics;
                if (graphics == null || graphics.VideoMemory == null ||
                    graphics.Width < decoded.Width + 16 || graphics.Height < decoded.Height + 16) {
                    failure = "GRAPHICS";
                } else {
                    const int drawX = 8;
                    const int drawY = 8;
                    graphics.Clear(0xFF010203u);
                    uint beforeVisible = graphics.GetPoint(drawX + sampleX, drawY + sampleY);
                    uint beforeTransparent = transparentX >= 0
                        ? graphics.GetPoint(drawX + transparentX, drawY + transparentY) : beforeVisible;
                    graphics.DrawImage(drawX, drawY, decoded);
                    uint afterVisible = graphics.GetPoint(drawX + sampleX, drawY + sampleY);
                    uint afterTransparent = transparentX >= 0
                        ? graphics.GetPoint(drawX + transparentX, drawY + transparentY) : beforeTransparent;
                    bool visibleChanged = afterVisible != beforeVisible;
                    bool transparentUnchanged = transparentX < 0 || afterTransparent == beforeTransparent;
                    SerialBreadcrumb("PNG_PROBE_RENDER_SAMPLE=" + afterVisible.ToString());
                    SerialBreadcrumb("PNG_PROBE_ALPHA_RENDER_OK=" +
                        (visibleChanged && transparentUnchanged ? "1" : "0"));
                    Framebuffer.Update();
                    if (!visibleChanged || !transparentUnchanged) failure = "RENDER";
                }
            }

            // Negative controls stay bounded and use the same public loader.
            if (failure == null) {
                invalidSignature = new byte[8];
                Image rejected = null;
                bool rejectedInvalid = !PngLoader.Load(invalidSignature, out rejected);
                if (rejected != null) rejected.Dispose();
                invalidSignature.Dispose();
                invalidSignature = null;
                SerialBreadcrumb("PNG_NEGATIVE_INVALID_SIGNATURE=" + (rejectedInvalid ? "PASS" : "FAIL"));
                if (!rejectedInvalid) failure = "NEGATIVE_SIGNATURE";
            }

            if (failure == null) {
                truncatedHeader = new byte[20];
                Image rejected = null;
                bool rejectedHeader = !PngLoader.Load(truncatedHeader, out rejected);
                if (rejected != null) rejected.Dispose();
                truncatedHeader.Dispose();
                truncatedHeader = null;
                SerialBreadcrumb("PNG_NEGATIVE_TRUNCATED_HEADER=" + (rejectedHeader ? "PASS" : "FAIL"));
                if (!rejectedHeader) failure = "NEGATIVE_HEADER";
            }

            if (failure == null) {
                impossibleDimensions = new byte[data.Length];
                for (int i = 0; i < data.Length; i++) impossibleDimensions[i] = data[i];
                impossibleDimensions[16] = 0x00;
                impossibleDimensions[17] = 0x20;
                impossibleDimensions[18] = 0x00;
                impossibleDimensions[19] = 0x00;
                Image rejected = null;
                bool rejectedDimensions = !PngLoader.Load(impossibleDimensions, out rejected);
                if (rejected != null) rejected.Dispose();
                impossibleDimensions.Dispose();
                impossibleDimensions = null;
                SerialBreadcrumb("PNG_NEGATIVE_IMPOSSIBLE_DIMENSIONS=" + (rejectedDimensions ? "PASS" : "FAIL"));
                if (!rejectedDimensions) failure = "NEGATIVE_DIMENSIONS";
            }

            if (failure == null) {
                int truncatedLength = data.Length > 16 ? data.Length - 16 : 0;
                truncatedStream = new byte[truncatedLength];
                for (int i = 0; i < truncatedLength; i++) truncatedStream[i] = data[i];
                Image rejected = null;
                bool rejectedStream = !PngLoader.Load(truncatedStream, out rejected);
                if (rejected != null) rejected.Dispose();
                truncatedStream.Dispose();
                truncatedStream = null;
                SerialBreadcrumb("PNG_NEGATIVE_TRUNCATED_STREAM=" + (rejectedStream ? "PASS" : "FAIL"));
                if (!rejectedStream) failure = "NEGATIVE_STREAM";
            }
        } catch {
            failure = "EXCEPTION";
        }

        if (decoded != null) decoded.Dispose();
        if (data != null) data.Dispose();
        if (invalidSignature != null) invalidSignature.Dispose();
        if (truncatedHeader != null) truncatedHeader.Dispose();
        if (impossibleDimensions != null) impossibleDimensions.Dispose();
        if (truncatedStream != null) truncatedStream.Dispose();

        if (failure == null) SerialBreadcrumb("PNG_PROBE_COMPLETE");
        else SerialBreadcrumb("PNG_PROBE_FAIL=" + failure);
        HaltAfterUefiPngProbe();
    }

    private static void HaltAfterUefiPngProbe() {
        SerialBreadcrumb("PNG_PROBE_HALT_ENTER");
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
    private static bool RefreshCachedIcons() {
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
            return true;
        } catch {
            // If icon creation fails, keep using old icons rather than having null icons
            BootConsole.WriteLine("Icon cache refresh failed - keeping old icons");
            return false;
        }
    }
}
