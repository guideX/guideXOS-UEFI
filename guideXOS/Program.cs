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
    private enum SafeModeInputBackend {
        SAFE_INPUT_NONE,
        SAFE_INPUT_PS2_KEYBOARD,
        SAFE_INPUT_PS2_MOUSE,
        SAFE_INPUT_PS2_BOTH
    }
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
        public ulong LastCompletedFrame;
        public ulong TimerCounter;
        public ulong InputPollCounter;
        public ulong LastCompletedInputPoll;
        public ulong KeyboardEventCounter;
        public ulong MouseEventCounter;
        public ulong InputPollFaultCounter;
        public ulong InputPollTimeoutCounter;
        public ulong WatchdogSkipCounter;
        public ulong LastPollDurationTicks;
        public ulong MaxPollDurationTicks;
        public ulong LastInputStatusCode;
        public string LastInputResult;
        public string LastFramePhase;
        public string LastCompletedFramePhase;
        public string LastInputPhase;
        public string LastInputSubphase;
        public string LastPs2Operation;
        public string InputPollStatus;
        public string KeyboardStatus;
        public string MouseStatus;
        public string UsbHidStatus;
        public string Ps2Status;
        public string UefiInputStatus;
        public string TimerStatus;
        public string RenderStatus;
        public string ReadyStatus;
        public byte LastPs2StatusByte;
        public byte LastPs2DataByte;
        public ushort LastPs2Port;
        public byte LastPs2WriteValue;
        public int LastMousePacketByteCount;
        public byte LastMousePacketByte0;
        public byte LastMousePacketByte1;
        public byte LastMousePacketByte2;
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
        _safeModeDiagnostics.LastCompletedFrame = 0;
        _safeModeDiagnostics.TimerCounter = 0;
        _safeModeDiagnostics.InputPollCounter = 0;
        _safeModeDiagnostics.LastCompletedInputPoll = 0;
        _safeModeDiagnostics.KeyboardEventCounter = 0;
        _safeModeDiagnostics.MouseEventCounter = 0;
        _safeModeDiagnostics.InputPollFaultCounter = 0;
        _safeModeDiagnostics.InputPollTimeoutCounter = 0;
        _safeModeDiagnostics.WatchdogSkipCounter = 0;
        _safeModeDiagnostics.LastPollDurationTicks = 0;
        _safeModeDiagnostics.MaxPollDurationTicks = 0;
        _safeModeDiagnostics.LastInputStatusCode = 0;
        _safeModeDiagnostics.LastInputResult = "idle";
        _safeModeDiagnostics.LastFramePhase = "frame-enter";
        _safeModeDiagnostics.LastCompletedFramePhase = "none";
        _safeModeDiagnostics.LastInputPhase = "init-start";
        _safeModeDiagnostics.LastInputSubphase = "none";
        _safeModeDiagnostics.LastPs2Operation = "none";
        _safeModeDiagnostics.InputPollStatus = "INPUT POLL INIT";
        _safeModeDiagnostics.KeyboardStatus = "KEYBOARD: unavailable";
        _safeModeDiagnostics.MouseStatus = "MOUSE: unavailable";
        _safeModeDiagnostics.UsbHidStatus = "USB HID: not implemented";
        _safeModeDiagnostics.Ps2Status = "PS/2: probing";
        _safeModeDiagnostics.UefiInputStatus = "UEFI input: pre-exit only";
        _safeModeDiagnostics.TimerStatus = "TIMER: checking";
        _safeModeDiagnostics.RenderStatus = "RENDER LOOP ACTIVE";
        _safeModeDiagnostics.ReadyStatus = "SAFE MODE READY";
        _safeModeDiagnostics.LastPs2StatusByte = 0;
        _safeModeDiagnostics.LastPs2DataByte = 0;
        _safeModeDiagnostics.LastPs2Port = 0;
        _safeModeDiagnostics.LastPs2WriteValue = 0;
        _safeModeDiagnostics.LastMousePacketByteCount = 0;
        _safeModeDiagnostics.LastMousePacketByte0 = 0;
        _safeModeDiagnostics.LastMousePacketByte1 = 0;
        _safeModeDiagnostics.LastMousePacketByte2 = 0;
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

    private static void SetSafeModeFramePhase(string phase) {
        if (phase != null) {
            _safeModeDiagnostics.LastFramePhase = phase;
        }
    }

    private static void CompleteSafeModeFrame(string phase) {
        _safeModeDiagnostics.LastCompletedFrame = _safeModeDiagnostics.FrameCounter;
        if (phase != null) {
            _safeModeDiagnostics.LastCompletedFramePhase = phase;
        }
    }

    private static void SetSafeModeInputSubphase(string subphase) {
        if (subphase != null) {
            _safeModeDiagnostics.LastInputSubphase = subphase;
        }
    }

    private static void CompleteSafeModeInputPoll(string phase) {
        _safeModeDiagnostics.LastCompletedInputPoll = _safeModeDiagnostics.InputPollCounter;
        if (phase != null) {
            _safeModeDiagnostics.LastInputSubphase = phase;
        }
    }

    private static void SetSafeModePs2Operation(string operation) {
        if (operation != null) {
            _safeModeDiagnostics.LastPs2Operation = operation;
        }
    }

    private static void RecordSafeModePs2Read(ushort port, byte value, string operation) {
        _safeModeDiagnostics.LastPs2Port = port;
        _safeModeDiagnostics.LastPs2DataByte = value;
        if (port == 0x64) {
            _safeModeDiagnostics.LastPs2StatusByte = value;
        }
        SetSafeModePs2Operation(operation);
    }

    private static void RecordSafeModePs2Write(ushort port, byte value, string operation) {
        _safeModeDiagnostics.LastPs2Port = port;
        _safeModeDiagnostics.LastPs2WriteValue = value;
        SetSafeModePs2Operation(operation);
    }

    private static void RecordSafeModeMousePacketByte(int index, byte value) {
        _safeModeDiagnostics.LastMousePacketByteCount = index + 1;
        if (index == 0) {
            _safeModeDiagnostics.LastMousePacketByte0 = value;
        } else if (index == 1) {
            _safeModeDiagnostics.LastMousePacketByte1 = value;
        } else if (index == 2) {
            _safeModeDiagnostics.LastMousePacketByte2 = value;
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

    /*
     * SAFE MODE INPUT BACKEND DIAGNOSTIC SELECTOR
     *
     * Change ActiveSafeModeInputBackend here when bringing up UEFI Safe Mode input.
     * Keep this as a simple compile-time switch so the boot flow stays stable and the
     * active diagnostic mode is obvious near the top of Program.cs.
     *
     * Diagnostic modes:
     * 1. SAFE_INPUT_NONE
     *    - Disable safe-mode PS/2 input polling completely.
     *    - Use this when checking whether freezes or corruption happen without any input work.
     *
     * 2. SAFE_INPUT_PS2_KEYBOARD
     *    - Poll only the PS/2 keyboard path.
     *    - Use this when validating keyboard bytes without involving mouse packet handling.
     *
     * 3. SAFE_INPUT_PS2_MOUSE
     *    - Poll only the PS/2 mouse path.
     *    - Use this when validating mouse packets without involving keyboard decode logic.
     *
     * 4. SAFE_INPUT_PS2_BOTH
     *    - Poll both PS/2 keyboard and PS/2 mouse paths.
     *    - Use this for the normal safe-mode diagnostic configuration once isolated tests pass.
     */
    private const SafeModeInputBackend ActiveSafeModeInputBackend = SafeModeInputBackend.SAFE_INPUT_NONE;
    private const ulong SafeModeInputPollTimeoutCycles = 12000000;
    private const int SafeModeInputByteBudget = 32;
    private const int SafeModeKeyboardByteBudget = 16;
    private const int SafeModeMousePacketBudget = 4;
    private const int SafeModePs2WaitBudget = 512;
    private const int SafeModeCursorRestoreWidth = 16;
    private const int SafeModeCursorRestoreHeight = 20;
    private const int SafeModeCursorDrawWidth = 13;
    private const int SafeModeCursorDrawHeight = 18;
    private const int SafeModeDiagnosticsPanelMargin = 24;
    private const int SafeModeDiagnosticsPanelY = 82;
    private const int SafeModeDiagnosticsPanelWidth = 340;
    private const int SafeModeDiagnosticsPanelHeight = 222;
    private const bool SKIP_FIRST_FRAME_BACKGROUND_DRAW = true;
    private const bool SKIP_UEFI_BACKGROUND_DRAW_ALL_FRAMES = true;
    private const bool UEFI_STEADY_STATE_SERIAL_ONLY = false;

    // ---------------------------------------------------------------------------
    // UEFI SAFE-MODE STEADY-STATE FAST PATH — WHY IT EXISTS
    // ---------------------------------------------------------------------------
    // Post-ExitBootServices the managed heap is fragile. DrawUefiSafeModeDiagnostics()
    // performs dozens of .ToString() and string-concatenation calls every frame.
    // On frame 2 this caused an immediate crash (QEMU exit right after marker 'B').
    // The UEFI_STEADY_STATE_SERIAL_ONLY fast path skips every managed-allocation
    // operation on frame 2+ and replaces them with a single serial char sequence
    // [Fn:S] (S = steady-state serial-only).  It is a temporary stabilisation
    // mode.  Future work should replace diagnostics with non-allocating fixed-buffer
    // or status-field rendering so the fast path can eventually be retired.
    // ---------------------------------------------------------------------------
    //
    // UEFI_STEADY_STATE_SERIAL_ONLY  = true
    //   No visual per-frame rendering after frame 1.  Pure serial heartbeat only.
    //   Serial pattern: [Fn:S].  Safest option — proven to survive frame 2+.
    //
    // UEFI_STEADY_STATE_MINIMAL_RENDER = true
    //   Non-allocating heartbeat-only visual rendering after frame 1.
    //   Draws one 16x16 colour-toggling block via TryWriteUefiPixel.
    //   No managed strings, no desktop/WM/input calls, no diagnostics panel.
    //   Serial pattern: [Fn:M].  Next stabilisation milestone.
    //
    // Full desktop rendering remains disabled until the minimal render loop
    // survives multiple frames reliably.
    // ---------------------------------------------------------------------------

    // When true, frame 2+ uses the allocation-free heartbeat visual path instead
    // of the full render loop.  UEFI_STEADY_STATE_SERIAL_ONLY must be false.
    private const bool UEFI_STEADY_STATE_MINIMAL_RENDER = true;
    private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = false;

    // When true, DrawUefiSafeModeDiagnostics() is restricted to frame 1 only.
    // Default: true (safe).  Set false only for targeted diagnostics sessions.
    private const bool UEFI_DIAGNOSTICS_FRAME1_ONLY = true;
    // When true, DrawUefiSafeModeDiagnostics() runs every frame in the non-fast
    // path.  MUST be false whenever UEFI_STEADY_STATE_SERIAL_ONLY is true,
    // because the diagnostics method allocates managed strings every call.
    private const bool UEFI_DRAW_DIAGNOSTICS_EACH_FRAME = false;
    private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = true;
    private const bool UEFI_ENABLE_SAFE_NORMAL_DESKTOP_FIRST_FRAME = false;
    private const bool UEFI_TINY_RENDER_LOOP_ENTRY_ONLY = false;
    private const bool UEFI_TINY_RENDER_LOOP_MINIMAL_GRAPHICS = true;
    private const bool NORMAL_DESKTOP_UEFI_STEP_PROBE = false;
    internal const bool NORMAL_DESKTOP_UEFI_PROBE_SAFE_PLACEHOLDERS_UNTIL_STEP10 = false;
    internal const bool NORMAL_DESKTOP_UEFI_PROBE_SAFE_FONT_PLACEHOLDER = false;
    private const bool NORMAL_DESKTOP_UEFI_PROBE_SKIP_WINDOW_TRAVERSAL = false;
    private const bool NORMAL_DESKTOP_UEFI_PROBE_SKIP_CURSOR_DRAW = false;
    private const bool NORMAL_DESKTOP_UEFI_PROBE_CURSOR_PLACEHOLDER = false;
    private const bool NORMAL_DESKTOP_UEFI_PROBE_STEP10_RED_FILLRECT_PLACEHOLDER = false;
    private const bool NORMAL_DESKTOP_UEFI_PROBE_STEP10_GREEN_FILLRECT_PLACEHOLDER = false;
    private const bool NORMAL_DESKTOP_UEFI_PROBE_STEP10_WHITE_FILLRECT_PLACEHOLDER = false;
    // TinyUEFI proof-pattern heartbeat is useful during bring-up, but keep it
    // opt-in so the serial log stays readable by default.
    private const bool UEFI_TINY_RENDER_HEARTBEAT_ENABLED = false;
    private const int UEFI_TINY_RENDER_HEARTBEAT_PERIOD_FRAMES = 30;
    private const bool UEFI_STEADY_STATE_HEARTBEAT_ENABLED = false;

    private const bool SKIP_BACKGROUND_DRAW = false;
    private const bool SKIP_UI_DRAW = false;
    private const bool SKIP_DESKTOP_DRAW = false;
    private const bool SKIP_ICON_DRAW = false;
    private const bool SKIP_TASKBAR_DRAW = false;
    private const bool SKIP_WINDOWMANAGER_DRAW = false;
    private const bool SKIP_DIAGNOSTICS_DRAW = false;
    private const bool SKIP_CURSOR_DRAW = false;
    private const bool SKIP_PRESENT = false;
    private const bool FIRST_FRAME_SERIAL_ONLY = true;
    private const bool USE_MINIMAL_UEFI_WALLPAPER = true;

    // ---------------------------------------------------------------------------
    // RENDER-LOOP PROLOGUE DIAGNOSTIC MODES
    // ---------------------------------------------------------------------------
    // UEFI_RENDER_LOOP_PROLOGUE_HALT = true
    //   After printing the earliest safe render-loop breadcrumb, enter an infinite
    //   busy-wait.  No rendering, no input, no allocations.
    //   If QEMU stays alive and serial shows the halt marker, the prologue entry is
    //   stable.  If QEMU exits before the halt marker, the fault is in the first
    //   statements of RenderLoop() itself.
    //
    // UEFI_RENDER_LOOP_PROLOGUE_ONLY = false (default)
    //   When enabled: execute only frame counter + constant serial markers, skip
    //   all rendering/input/WM/desktop/framebuffer.  Enable only after the halt
    //   test passes.
    // ---------------------------------------------------------------------------
    private const bool UEFI_RENDER_LOOP_PROLOGUE_HALT = false;
    private const bool UEFI_RENDER_LOOP_PROLOGUE_ONLY = false;
    private const bool UEFI_RENDER_LOOP_SAFE_PROLOGUE = true;

    private static bool UseSafeNormalDesktopUefiMode() {
        return BootConsole.CurrentMode == guideXOS.BootMode.UEFI &&
               UEFI_ENABLE_SAFE_NORMAL_DESKTOP_FIRST_FRAME;
    }

    private static bool SafeModeKeyboardEnabled =>
        ActiveSafeModeInputBackend == SafeModeInputBackend.SAFE_INPUT_PS2_KEYBOARD ||
        ActiveSafeModeInputBackend == SafeModeInputBackend.SAFE_INPUT_PS2_BOTH;

    private static bool SafeModeMouseEnabled =>
        ActiveSafeModeInputBackend == SafeModeInputBackend.SAFE_INPUT_PS2_MOUSE ||
        ActiveSafeModeInputBackend == SafeModeInputBackend.SAFE_INPUT_PS2_BOTH;

    private static string GetSafeModeBackendLabel() {
        switch (ActiveSafeModeInputBackend) {
            case SafeModeInputBackend.SAFE_INPUT_NONE:
                return "NONE";
            case SafeModeInputBackend.SAFE_INPUT_PS2_KEYBOARD:
                return "PS2_KEYBOARD";
            case SafeModeInputBackend.SAFE_INPUT_PS2_MOUSE:
                return "PS2_MOUSE";
            default:
                return "PS2_BOTH";
        }
    }

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

        CompleteSafeModeInputPoll("input-exit");

        _safeModeDiagnostics.KeyboardStatus = _safeModeDiagnostics.KeyboardAvailable
            ? "KEYBOARD READY"
            : (SafeModeKeyboardEnabled ? "KEYBOARD UNAVAILABLE" : "KEYBOARD DISABLED");
        _safeModeDiagnostics.MouseStatus = _safeModeDiagnostics.MouseAvailable
            ? "MOUSE READY"
            : (SafeModeMouseEnabled ? "MOUSE UNAVAILABLE" : "MOUSE DISABLED");
        _safeModeDiagnostics.UsbHidStatus = _safeModeDiagnostics.UsbHidImplemented
            ? "USB HID ACTIVE"
            : "USB HID NOT IMPLEMENTED";
        _safeModeDiagnostics.Ps2Status = ActiveSafeModeInputBackend == SafeModeInputBackend.SAFE_INPUT_NONE
            ? "PS2 INPUT DISABLED"
            : ActiveSafeModeInputBackend == SafeModeInputBackend.SAFE_INPUT_PS2_KEYBOARD
                ? "PS2 KEYBOARD POLLING"
                : ActiveSafeModeInputBackend == SafeModeInputBackend.SAFE_INPUT_PS2_MOUSE
                    ? "PS2 MOUSE POLLING"
                    : (_uefiInputInitialized ? "PS2 KEYBOARD MOUSE" : "PS2 PROBING");
        _safeModeDiagnostics.UefiInputStatus = _safeModeDiagnostics.InputUsingUefiBootServices
            ? "UEFI INPUT PRE EXIT"
            : ("UEFI INPUT PRE EXIT ONLY " + GetSafeModeBackendLabel());
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
            bool useSafeNormalDesktopUefi = UseSafeNormalDesktopUefiMode();
            // In UEFI mode, disable debug lines to prevent graphical corruption
            BootConsole.DrawDebugLines = false;
            BootConsole.WriteLine("[BOOT_MODE] UEFI");
            if (!useSafeNormalDesktopUefi) {
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
            } else {
                BootConsole.WriteLine("[SMAIN] SAFE normal desktop mode - skipping mouse, keyboard, and USB initialization");
            }

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

            if (!useSafeNormalDesktopUefi) {
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
            } else {
                BootConsole.WriteLine("[SMAIN] SAFE normal desktop mode - skipping cursor, window manager, and desktop initialization");
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

        // [DIAG] Patch-active marker: if this does NOT appear in QEMU serial output,
        // a stale/wrong kernel image is being booted.
        SerialBreadcrumb("[DIAG] RENDER_LOOP_PROLOGUE_PATCH_ACTIVE");
        SerialBreadcrumb("SMAIN_BEFORE_RENDERLOOP_DISPATCH");

        // Enter the main render loop (in a separate method to keep stack frames small)
        bool isUefi = BootConsole.CurrentMode == guideXOS.BootMode.UEFI;
        bool useSafeNormalUefiDesktop = isUefi && UEFI_ENABLE_SAFE_NORMAL_DESKTOP_FIRST_FRAME;
        bool useTinyUefi = isUefi && UEFI_USE_TINY_RENDER_LOOP_BYPASS && !useSafeNormalUefiDesktop && !UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH;
        bool useNormalUefiDesktopStepProbe = isUefi && UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH && NORMAL_DESKTOP_UEFI_STEP_PROBE;
        bool useNormalUefiDesktop = isUefi && UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH && !NORMAL_DESKTOP_UEFI_STEP_PROBE;
        LogUefiRenderDispatchDiagnostics(useNormalUefiDesktopStepProbe, useTinyUefi, useSafeNormalUefiDesktop, useNormalUefiDesktop);
        SerialBreadcrumb(useSafeNormalUefiDesktop ? "SMAIN_DISPATCH_REASON=SAFE_NORMAL_DESKTOP_UEFI" :
                         useTinyUefi ? "SMAIN_DISPATCH_REASON=TINY_UEFI" :
                         useNormalUefiDesktopStepProbe ? "SMAIN_DISPATCH_REASON=NORMAL_DESKTOP_UEFI_STEP_PROBE" :
                         useNormalUefiDesktop ? "SMAIN_DISPATCH_REASON=NORMAL_DESKTOP_UEFI" :
                         isUefi ? "SMAIN_DISPATCH_REASON=FULL_UEFI" :
                         "SMAIN_DISPATCH_REASON=LEGACY");

        if (useSafeNormalUefiDesktop) {
            SerialBreadcrumb("SMAIN_DISPATCH_SAFE_NORMAL_DESKTOP_UEFI");
            RenderLoopSafeNormalDesktopFirstFrame();
        } else if (useTinyUefi) {
            SerialBreadcrumb("SMAIN_DISPATCH_TINY_UEFI");
            RenderLoopUefiTinyBypass();
        } else if (useNormalUefiDesktopStepProbe) {
            SerialBreadcrumb("SMAIN_DISPATCH_NORMAL_DESKTOP_UEFI_STEP_PROBE");
            RenderLoopNormalDesktopStepProbe();
        } else if (useNormalUefiDesktop) {
            SerialBreadcrumb("SMAIN_DISPATCH_NORMAL_DESKTOP_UEFI");
            RenderLoop();
        } else {
            if (isUefi) {
                SerialBreadcrumb("SMAIN_DISPATCH_FULL_RENDERLOOP_UEFI");
            } else {
                SerialBreadcrumb("SMAIN_DISPATCH_FULL_RENDERLOOP");
            }
            RenderLoop();
        }
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

        if (UseSafeNormalDesktopUefiMode()) {
            BootConsole.WriteLine("[SMAIN] SAFE normal desktop mode - skipping icons, context menus, and widgets");
            return;
        }

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
        LogUefiFramebufferSnapshot("SMAIN_FB_BEFORE_REPAIR");
        Framebuffer.RecoverUefiState();
        LogUefiFramebufferSnapshot("SMAIN_FB_AFTER_REPAIR");
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
        LogUefiFramebufferSnapshot("SMAIN_GFX_BEFORE_ENSURE");
        Framebuffer.EnsureGraphics();
        LogUefiFramebufferSnapshot("SMAIN_GFX_AFTER_ENSURE");
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
            BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_ENTER");
            BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_VALIDATE_FB_ENTER");
            bool fbValid = Framebuffer.Graphics != null && Framebuffer.Graphics.VideoMemory != null && Framebuffer.Width > 0 && Framebuffer.Height > 0;
            if (!fbValid) {
                BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_VALIDATE_FB_FAIL");
                Wallpaper = null;
            }
            BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_VALIDATE_FB_EXIT");

            if (fbValid && USE_MINIMAL_UEFI_WALLPAPER) {
                BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_MINIMAL_ENTER");
                DrawUefiFillRect(Framebuffer.Graphics.VideoMemory, Framebuffer.Width, Framebuffer.Height, Framebuffer.Width, (ulong)Framebuffer.Width * (ulong)Framebuffer.Height, 0, 0, Framebuffer.Width, Framebuffer.Height, 0xFF0D7D77u);
                Wallpaper = null;
                BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_MINIMAL_EXIT");
                BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_EXIT");
                BootConsole.WriteLine("[SMAIN] Wallpaper created");
                BootConsole.WriteLine("[SAFE_MODE] UEFI minimal desktop initialized");
                return;
            }

            BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_ALLOC_ENTER");
            Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height);
            BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_ALLOC_EXIT");
            if (Wallpaper == null) {
                BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_ALLOC_NULL");
            } else if (Wallpaper.RawData == null) {
                BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_RAW_NULL");
                Wallpaper = null;
            } else {
                BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_GRADIENT_ENTER");
                uint topColor = 0xFF5FD4C4;
                uint bottomColor = 0xFF0D7D77;
                int topR = (int)((topColor >> 16) & 0xFF);
                int topG = (int)((topColor >> 8) & 0xFF);
                int topB = (int)(topColor & 0xFF);
                int bottomR = (int)((bottomColor >> 16) & 0xFF);
                int bottomG = (int)((bottomColor >> 8) & 0xFF);
                int bottomB = (int)(bottomColor & 0xFF);
                int fbHeight = Framebuffer.Height;
                int fbWidth = Framebuffer.Width;
                int pixelCount = fbWidth * fbHeight;
                if (fbWidth <= 0 || fbHeight <= 0 || pixelCount <= 0) {
                    BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_GRADIENT_INVALID_DIMENSIONS");
                    Wallpaper = null;
                } else {
                    BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_PIXEL_LOOP_ENTER");
                    for (int y = 0; y < fbHeight; y++) {
                        if (y == 0) BootConsole.WriteLine("[SMAIN] WALLPAPER_ROW_0");
                        else if (y == 100) BootConsole.WriteLine("[SMAIN] WALLPAPER_ROW_100");
                        else if (y == 200) BootConsole.WriteLine("[SMAIN] WALLPAPER_ROW_200");
                        else if (y == 400) BootConsole.WriteLine("[SMAIN] WALLPAPER_ROW_400");
                        else if (y == fbHeight - 1) BootConsole.WriteLine("[SMAIN] WALLPAPER_ROW_LAST");
                        int t256 = (y * 256) / fbHeight;
                        int r = topR + ((bottomR - topR) * t256) / 256;
                        int g = topG + ((bottomG - topG) * t256) / 256;
                        int b = topB + ((bottomB - topB) * t256) / 256;
                        int color = unchecked((int)(0xFF000000 | (uint)(r << 16) | (uint)(g << 8) | (uint)b));
                        int rowBase = y * fbWidth;
                        for (int x = 0; x < fbWidth; x++) {
                            Wallpaper.RawData[rowBase + x] = color;
                        }
                    }
                    BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_PIXEL_LOOP_EXIT");
                    BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_ASSIGN_ENTER");
                    BootConsole.WriteLine("[SMAIN] Wallpaper gradient created");
                    BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_ASSIGN_EXIT");
                }
                BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_GRADIENT_EXIT");
            }
            BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_EXIT");
        } catch {
            BootConsole.WriteLine("[SMAIN] UEFI_WALLPAPER_FAILSOFT");
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

    private static void SerialBreadcrumb(string breadcrumb) {
        if (breadcrumb == null) return;
        for (int i = 0; i < breadcrumb.Length; i++) {
            SerialChar(breadcrumb[i]);
        }
        SerialChar('\n');
    }

    private static void SerialWriteUnsigned(ulong value) {
        char* digits = stackalloc char[21];
        int len = 0;
        do {
            digits[len++] = (char)('0' + (value % 10UL));
            value /= 10UL;
        } while (value != 0 && len < 21);
        while (len-- > 0) {
            SerialChar(digits[len]);
        }
    }

    private static void SerialWriteHex(ulong value) {
        SerialChar('0');
        SerialChar('x');
        bool started = false;
        for (int shift = 60; shift >= 0; shift -= 4) {
            int nibble = (int)((value >> shift) & 0xFUL);
            if (nibble != 0 || started || shift == 0) {
                started = true;
                SerialChar((char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10)));
            }
        }
    }

    private static void LogNormalDesktopStepProbeState(string phase, guideXOS.Graph.Graphics graphics, uint color) {
        UefiBootInfo* bootInfo = Framebuffer.OriginalBootInfo;
        uint fbW = bootInfo != null ? bootInfo->FramebufferWidth : Framebuffer.Width;
        uint fbH = bootInfo != null ? bootInfo->FramebufferHeight : Framebuffer.Height;
        uint pitchBytes = bootInfo != null ? bootInfo->FramebufferPitch : 0;
        uint pitchPixels = pitchBytes != 0 ? pitchBytes / 4u : 0u;
        ulong gfxVideoMemory = graphics != null ? (ulong)graphics.VideoMemory : 0UL;
        ulong fbVideoMemory = (ulong)Framebuffer.VideoMemory;
        ulong originalVideoMemory = (ulong)Framebuffer.OriginalVideoMemory;

        SerialBreadcrumb(phase);
        SerialBreadcrumb(graphics == null ? "NORM_STEP_003_GFX=NULL" : "NORM_STEP_003_GFX=OK");
        SerialBreadcrumb(graphics != null && graphics.VideoMemory != null ? "NORM_STEP_003_GFX_VM=SET" : "NORM_STEP_003_GFX_VM=NULL");
        SerialBreadcrumb("NORM_STEP_003_GFX_W");
        SerialWriteUnsigned(graphics != null ? (ulong)graphics.Width : 0UL);
        SerialChar('\n');
        SerialBreadcrumb("NORM_STEP_003_GFX_H");
        SerialWriteUnsigned(graphics != null ? (ulong)graphics.Height : 0UL);
        SerialChar('\n');
        SerialBreadcrumb("NORM_STEP_003_GFX_VM");
        SerialWriteHex(gfxVideoMemory);
        SerialChar('\n');
        SerialBreadcrumb("NORM_STEP_003_FB_VM");
        SerialWriteHex(fbVideoMemory);
        SerialChar('\n');
        SerialBreadcrumb("NORM_STEP_003_FB_W");
        SerialWriteUnsigned(fbW);
        SerialChar('\n');
        SerialBreadcrumb("NORM_STEP_003_FB_H");
        SerialWriteUnsigned(fbH);
        SerialChar('\n');
        SerialBreadcrumb("NORM_STEP_003_FB_PITCH_BYTES");
        SerialWriteUnsigned(pitchBytes);
        SerialChar('\n');
        SerialBreadcrumb("NORM_STEP_003_FB_PITCH_PIXELS");
        SerialWriteUnsigned(pitchPixels);
        SerialChar('\n');
        SerialBreadcrumb("NORM_STEP_003_ORIG_FB_VM");
        SerialWriteHex(originalVideoMemory);
        SerialChar('\n');
        SerialBreadcrumb("NORM_STEP_003_COLOR");
        SerialWriteHex(color);
        SerialChar('\n');
        SerialBreadcrumb(gfxVideoMemory == fbVideoMemory ? "NORM_STEP_003_VM_MATCH=1" : "NORM_STEP_003_VM_MATCH=0");
    }

    private static int ProbeNormalDesktopStep12MeasureString(IFont probeFont, string probeText) {
        SerialBreadcrumb("NORM_STEP_012_MEASURE_HELPER_ENTER");

        if (probeFont == null || probeText == null) {
            SerialBreadcrumb("NORM_STEP_012_MEASURE_HELPER_EXIT");
            return 0;
        }

        int width = 0;
        for (int i = 0; i < probeText.Length; i++) {
            if (i == 0) {
                SerialBreadcrumb("NORM_STEP_012_FIRST_CHAR_ACCESS_ENTER");
                char firstChar = probeText[0];
                _ = firstChar;
                SerialBreadcrumb("NORM_STEP_012_FIRST_CHAR_ACCESS_EXIT");
            }

            SerialBreadcrumb("NORM_STEP_012_GLYPH_LOOKUP_ENTER");
            int glyphWidth = probeFont.DrawChar(Framebuffer.Graphics, -1, -1, probeText[i]);
            SerialBreadcrumb("NORM_STEP_012_GLYPH_LOOKUP_EXIT");

            SerialBreadcrumb("NORM_STEP_012_WIDTH_ACCUMULATION_ENTER");
            width += glyphWidth + probeFont.Padding;
            SerialBreadcrumb("NORM_STEP_012_WIDTH_ACCUMULATION_EXIT");
        }

        SerialBreadcrumb("NORM_STEP_012_MEASURE_HELPER_EXIT");
        return width;
    }

    private static void ReportNormalDesktopStepProbeGraphicsCallsites() {
        SerialBreadcrumb("NORM_GFX_CALLSITE_REPORT_BEGIN");
        SerialBreadcrumb("NORM_GFX_CALLSITE=STEP_003 graphics.Clear(0xFF0B3C4Cu) via cached receiver guard");
        SerialBreadcrumb("NORM_GFX_CALLSITE=STEP_004 Framebuffer.Graphics.FillRectangle(16,16,32,32,0xFFFF2020u)");
        SerialBreadcrumb("NORM_GFX_CALLSITE=STEP_005 BackgroundRotationManager.DrawBackground() [direct Framebuffer.Graphics.DrawImage/FillRectangle inside helper]");
        SerialBreadcrumb("NORM_GFX_CALLSITE=STEP_006 Framebuffer.Graphics.FillRectangle(0,0,Framebuffer.Width,Framebuffer.Height,0xFF1E1E1Eu)");
        SerialBreadcrumb("NORM_GFX_CALLSITE=STEP_007 Framebuffer.Graphics.FillRectangle x2 in taskbar probe");
        SerialBreadcrumb("NORM_GFX_CALLSITE=STEP_008 Framebuffer.Graphics.FillRectangle/DrawRectangle/DrawImage in taskbar probe");
        SerialBreadcrumb("NORM_GFX_CALLSITE=STEP_010 Framebuffer.Graphics.DrawImage x3 in icon probe");
        SerialBreadcrumb("NORM_GFX_CALLSITE=STEP_011 WindowManager.DrawAllExceptTaskManager()/DrawTaskManager() [direct Framebuffer.Graphics calls in subpaths]");
        SerialBreadcrumb("NORM_GFX_CALLSITE=STEP_012 WindowManager.font.MeasureString()/DrawString() [split probe with A/B/C markers]");
        SerialBreadcrumb("NORM_GFX_CALLSITE=STEP_013 DrawUefiCursor() [no direct Framebuffer.Graphics receiver call]");
        SerialBreadcrumb("NORM_GFX_CALLSITE_REPORT_END");
    }

    private static uint GetFramebufferBitsPerPixel(UefiBootInfo* bootInfo) {
        if (bootInfo == null) return 0;
        switch (bootInfo->FramebufferFormat) {
            case FramebufferFormat.R8G8B8A8:
            case FramebufferFormat.B8G8R8A8:
                return 32;
            default:
                return 0;
        }
    }

    private static string GetFramebufferFormatLabel(UefiBootInfo* bootInfo) {
        if (bootInfo == null) return "UNKNOWN";
        switch (bootInfo->FramebufferFormat) {
            case FramebufferFormat.R8G8B8A8:
                return "R8G8B8A8";
            case FramebufferFormat.B8G8R8A8:
                return "B8G8R8A8";
            default:
                return "UNKNOWN";
        }
    }

    private static void LogUefiFramebufferSnapshot(string phase) {
        UefiBootInfo* bootInfo = Framebuffer.OriginalBootInfo;
        uint fbW = bootInfo != null ? bootInfo->FramebufferWidth : Framebuffer.Width;
        uint fbH = bootInfo != null ? bootInfo->FramebufferHeight : Framebuffer.Height;
        uint pitchBytes = bootInfo != null ? bootInfo->FramebufferPitch : 0;
        uint bpp = GetFramebufferBitsPerPixel(bootInfo);
        uint gfxW = Framebuffer.Graphics != null ? (uint)Framebuffer.Graphics.Width : 0u;
        uint gfxH = Framebuffer.Graphics != null ? (uint)Framebuffer.Graphics.Height : 0u;

        SerialBreadcrumb(phase);
        SerialBreadcrumb("SMAIN_FB_W");
        SerialWriteUnsigned(fbW);
        SerialChar('\n');
        SerialBreadcrumb("SMAIN_FB_H");
        SerialWriteUnsigned(fbH);
        SerialChar('\n');
        SerialBreadcrumb("SMAIN_FB_BPP");
        SerialWriteUnsigned(bpp);
        SerialChar('\n');
        SerialBreadcrumb("SMAIN_FB_PITCH");
        SerialWriteUnsigned(pitchBytes);
        SerialChar('\n');
        SerialBreadcrumb("SMAIN_GFX_W");
        SerialWriteUnsigned(gfxW);
        SerialChar('\n');
        SerialBreadcrumb("SMAIN_GFX_H");
        SerialWriteUnsigned(gfxH);
        SerialChar('\n');
    }

    private static void LogUefiRenderDispatchDiagnostics(bool emitVerbose, bool useTinyUefi, bool useSafeNormalDesktopUefi, bool useNormalUefiDesktop) {
        bool isUefi = BootConsole.CurrentMode == guideXOS.BootMode.UEFI;
        UefiBootInfo* bootInfo = Framebuffer.OriginalBootInfo;
        uint fbW = bootInfo != null ? bootInfo->FramebufferWidth : Framebuffer.Width;
        uint fbH = bootInfo != null ? bootInfo->FramebufferHeight : Framebuffer.Height;
        uint pitchBytes = bootInfo != null ? bootInfo->FramebufferPitch : 0;
        uint bpp = bootInfo != null && (bootInfo->FramebufferFormat == FramebufferFormat.R8G8B8A8 || bootInfo->FramebufferFormat == FramebufferFormat.B8G8R8A8) ? 32u : 0u;
        uint pitchPixels = (pitchBytes != 0 && bpp != 0) ? (pitchBytes / (bpp / 8u)) : 0;

        SerialBreadcrumb("SMAIN_DIAG_BEGIN");
        SerialBreadcrumb(isUefi ? "SMAIN_DIAG_UEFI_MODE=1" : "SMAIN_DIAG_UEFI_MODE=0");
        SerialBreadcrumb(UEFI_STEADY_STATE_SERIAL_ONLY ? "SMAIN_DIAG_SAFE_SERIAL_ONLY=1" : "SMAIN_DIAG_SAFE_SERIAL_ONLY=0");
        SerialBreadcrumb(UEFI_STEADY_STATE_MINIMAL_RENDER ? "SMAIN_DIAG_SAFE_MINIMAL_RENDER=1" : "SMAIN_DIAG_SAFE_MINIMAL_RENDER=0");
        SerialBreadcrumb(UEFI_USE_TINY_RENDER_LOOP_BYPASS ? "SMAIN_DIAG_TINY_BYPASS=1" : "SMAIN_DIAG_TINY_BYPASS=0");
        SerialBreadcrumb(UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH ? "SMAIN_DIAG_NORMAL_DESKTOP_GUARD=1" : "SMAIN_DIAG_NORMAL_DESKTOP_GUARD=0");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_STEP_PROBE ? "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE=1" : "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE=0");
        SerialBreadcrumb(useSafeNormalDesktopUefi ? "SMAIN_DIAG_SAFE_NORMAL_DESKTOP_FIRST_FRAME=1" : "SMAIN_DIAG_SAFE_NORMAL_DESKTOP_FIRST_FRAME=0");
        SerialBreadcrumb(emitVerbose ? "SMAIN_DIAG_VERBOSE=1" : "SMAIN_DIAG_VERBOSE=0");
        if (!emitVerbose) {
            SerialBreadcrumb(useTinyUefi ? "SMAIN_DIAG_REASON_TINY" : useSafeNormalDesktopUefi ? "SMAIN_DIAG_REASON_SAFE_NORMAL_DESKTOP" : useNormalUefiDesktop ? "SMAIN_DIAG_REASON_NORMAL_UefiDesktop" : isUefi ? "SMAIN_DIAG_REASON_FULL_UEFI" : "SMAIN_DIAG_REASON_LEGACY");
            SerialBreadcrumb("SMAIN_DIAG_END");
            return;
        }
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_SAFE_PLACEHOLDERS_UNTIL_STEP10 ? "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SAFE_PLACEHOLDERS_UNTIL_STEP10=1" : "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SAFE_PLACEHOLDERS_UNTIL_STEP10=0");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_SAFE_FONT_PLACEHOLDER ? "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SAFE_FONT_PLACEHOLDER=1" : "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SAFE_FONT_PLACEHOLDER=0");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_SKIP_WINDOW_TRAVERSAL ? "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SKIP_WINDOW_TRAVERSAL=1" : "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SKIP_WINDOW_TRAVERSAL=0");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_SKIP_CURSOR_DRAW ? "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SKIP_CURSOR_DRAW=1" : "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SKIP_CURSOR_DRAW=0");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_CURSOR_PLACEHOLDER ? "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_CURSOR_PLACEHOLDER=1" : "SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_CURSOR_PLACEHOLDER=0");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_STEP10_RED_FILLRECT_PLACEHOLDER ? "SMAIN_DIAG_NORMAL_DESKTOP_STEP10_RED_FILLRECT_PLACEHOLDER=1" : "SMAIN_DIAG_NORMAL_DESKTOP_STEP10_RED_FILLRECT_PLACEHOLDER=0");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_STEP10_GREEN_FILLRECT_PLACEHOLDER ? "SMAIN_DIAG_NORMAL_DESKTOP_STEP10_GREEN_FILLRECT_PLACEHOLDER=1" : "SMAIN_DIAG_NORMAL_DESKTOP_STEP10_GREEN_FILLRECT_PLACEHOLDER=0");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_STEP10_WHITE_FILLRECT_PLACEHOLDER ? "SMAIN_DIAG_NORMAL_DESKTOP_STEP10_WHITE_FILLRECT_PLACEHOLDER=1" : "SMAIN_DIAG_NORMAL_DESKTOP_STEP10_WHITE_FILLRECT_PLACEHOLDER=0");
        SerialBreadcrumb(UEFI_TINY_RENDER_LOOP_ENTRY_ONLY ? "SMAIN_DIAG_TINY_ENTRY_ONLY=1" : "SMAIN_DIAG_TINY_ENTRY_ONLY=0");
        SerialBreadcrumb(UEFI_TINY_RENDER_LOOP_MINIMAL_GRAPHICS ? "SMAIN_DIAG_TINY_MINIMAL_GRAPHICS=1" : "SMAIN_DIAG_TINY_MINIMAL_GRAPHICS=0");

        SerialBreadcrumb("SMAIN_DIAG_FB_BEGIN");
        SerialWriteUnsigned(fbW);
        SerialChar('x');
        SerialWriteUnsigned(fbH);
        SerialChar(' ');
        SerialWriteUnsigned(bpp);
        SerialChar('b');
        SerialChar('p');
        SerialChar('p');
        SerialChar(' ');
        SerialWriteUnsigned(pitchBytes);
        SerialChar('b');
        SerialChar('y');
        SerialChar('t');
        SerialChar('e');
        SerialChar('s');
        SerialChar(' ');
        SerialWriteUnsigned(pitchPixels);
        SerialChar('p');
        SerialChar('x');
        SerialChar('\n');

        SerialBreadcrumb("SMAIN_DIAG_GFX_BEGIN");
        SerialBreadcrumb(Framebuffer.Graphics == null ? "SMAIN_DIAG_GFX=NULL" : "SMAIN_DIAG_GFX=OK");
        if (Framebuffer.Graphics != null) {
            SerialBreadcrumb("SMAIN_DIAG_GFX_W");
            SerialWriteUnsigned((ulong)Framebuffer.Graphics.Width);
            SerialChar('\n');
            SerialBreadcrumb("SMAIN_DIAG_GFX_H");
            SerialWriteUnsigned((ulong)Framebuffer.Graphics.Height);
            SerialChar('\n');
            SerialBreadcrumb(Framebuffer.Graphics.VideoMemory == null ? "SMAIN_DIAG_GFX_VM=NULL" : "SMAIN_DIAG_GFX_VM=SET");
            SerialBreadcrumb("SMAIN_DIAG_GFX_VM");
            SerialWriteHex((ulong)Framebuffer.Graphics.VideoMemory);
            SerialChar('\n');
        }
        SerialBreadcrumb("SMAIN_DIAG_FB_VM");
        SerialWriteHex((ulong)Framebuffer.VideoMemory);
        SerialChar('\n');
        SerialBreadcrumb("SMAIN_DIAG_ORIG_FB_VM");
        SerialWriteHex((ulong)Framebuffer.OriginalVideoMemory);
        SerialChar('\n');

        SerialBreadcrumb(useTinyUefi ? "SMAIN_DIAG_REASON_TINY" :
                         useSafeNormalDesktopUefi ? "SMAIN_DIAG_REASON_SAFE_NORMAL_DESKTOP" :
                         useNormalUefiDesktop ? "SMAIN_DIAG_REASON_NORMAL_DESKTOP_UEFI" :
                         isUefi ? "SMAIN_DIAG_REASON_FULL_UEFI" :
                         "SMAIN_DIAG_REASON_LEGACY");
        SerialBreadcrumb("SMAIN_DIAG_END");
    }

    private static bool ShouldEmitExplicitFrameBreadcrumbs(int frameCounter) {
        return frameCounter >= 2 && frameCounter <= 5;
    }

    private static void SerialFrameBreadcrumb(int frameCounter, string phase) {
        if (!ShouldEmitExplicitFrameBreadcrumbs(frameCounter) || phase == null) return;
        SerialBreadcrumb("F" + frameCounter.ToString() + "_" + phase);
    }

    private static void SerialFramePhaseEnter(int frameCounter, string phase) {
        if (phase == null) return;
        SerialFrameBreadcrumb(frameCounter, phase + "_ENTER");
    }

    private static void SerialFramePhaseExit(int frameCounter, string phase) {
        if (phase == null) return;
        SerialFrameBreadcrumb(frameCounter, phase + "_EXIT");
    }

    private static void SerialFrameBackgroundSkipped(int frameCounter) {
        SerialFrameBreadcrumb(frameCounter, "BACKGROUND_SKIPPED");
    }

    private static void SerialFrameBackgroundBreadcrumb(int frameCounter, string phase) {
        if (phase == null) return;
        if (frameCounter == 1) {
            SerialBreadcrumb("F1_BG_" + phase);
            return;
        }
        SerialFrameBreadcrumb(frameCounter, "BG_" + phase);
    }

    private static void SerialFrameBackgroundReason(int frameCounter, string reason) {
        if (reason == null) return;
        SerialFrameBackgroundBreadcrumb(frameCounter, "REASON_" + reason);
    }

    private static void SerialBackgroundReason(string reason) {
        if (reason == null) return;
        SerialBreadcrumb("F1_BG_REASON_" + reason);
    }

    private static bool ShouldLogBackgroundRow(int row, int lastRow) {
        if (row == 0 || row == 100 || row == 200 || row == 400) return true;
        return lastRow >= 0 && row == lastRow;
    }

    private static bool ShouldSkipFirstFrameBackgroundDraw(int frameCounter) {
        return frameCounter == 1 && SKIP_FIRST_FRAME_BACKGROUND_DRAW;
    }

    private static bool ShouldSkipUefiBackgroundDraw(int frameCounter) {
        if (ShouldSkipFirstFrameBackgroundDraw(frameCounter)) return true;
        return frameCounter >= 2 && SKIP_UEFI_BACKGROUND_DRAW_ALL_FRAMES;
    }

    private static bool TryDrawUefiFirstFrameBackground(int frameCounter, bool emitFirstFrameBreadcrumbs) {
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_DISPATCH_ENTER");

        if (ShouldSkipFirstFrameBackgroundDraw(frameCounter)) {
            SerialBackgroundReason("FIRST_FRAME_SKIP_SWITCH");
            if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_DISPATCH_EXIT");
            return false;
        }

        try {
            DrawUefiHeartbeat(frameCounter);
            if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_DISPATCH_EXIT");
            return true;
        } catch {
            SerialBackgroundReason("DRAW_EXCEPTION");
            if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_DISPATCH_EXIT");
            return false;
        }
    }

    private static bool TryDrawUefiSteadyStateBackground(int frameCounter) {
        SerialFrameBackgroundBreadcrumb(frameCounter, "DISPATCH_ENTER");

        if (ShouldSkipUefiBackgroundDraw(frameCounter)) {
            SerialFrameBackgroundReason(frameCounter, frameCounter == 1 ? "FIRST_FRAME_SKIP_SWITCH" : "ALL_FRAMES_SKIP_SWITCH");
            SerialFrameBackgroundSkipped(frameCounter);
            SerialFrameBackgroundBreadcrumb(frameCounter, "DISPATCH_EXIT");
            return false;
        }

        try {
            DrawUefiHeartbeat(frameCounter);
            SerialFrameBackgroundBreadcrumb(frameCounter, "DISPATCH_EXIT");
            return true;
        } catch {
            SerialFrameBackgroundReason(frameCounter, "DRAW_EXCEPTION");
            SerialFrameBackgroundSkipped(frameCounter);
            SerialFrameBackgroundBreadcrumb(frameCounter, "DISPATCH_EXIT");
            return false;
        }
    }

    /// <summary>
    /// Main render loop - extracted from SMain to keep stack frames small
    /// </summary>
    private static void RenderLoop() {
        // RL_AFTER_ENTER_PRINT: we are now inside RenderLoop(), before any risky reads
        SerialBreadcrumb("RL_AFTER_ENTER_PRINT");

        bool isUefi = (BootConsole.CurrentMode == guideXOS.BootMode.UEFI);
        bool useSafeUefiPrologue = isUefi && UEFI_RENDER_LOOP_SAFE_PROLOGUE;

        // PROLOGUE HALT: if enabled, stay here forever — proves entry is stable
        if (UEFI_RENDER_LOOP_PROLOGUE_HALT) {
            SerialBreadcrumb("RL_PROLOGUE_HALT_ENTER");
            for (; ; ) {
                // Constant serial heartbeat; Native.Out8 is always safe
                Native.Out8(0x3F8, (byte)'.');
                // Busy-wait ~1M iterations as a rough delay
                for (int _h = 0; _h < 1000000; _h++) { }
            }
        }

        int lastMouseX = 0;
        int lastMouseY = 0;
        ulong lastMoveTick = 0;
        const ulong ActiveMoveMs = 100;
        int frameCounter = 0;

        if (useSafeUefiPrologue) {
            SerialBreadcrumb("RL_SAFE_PROLOGUE_ENTER");
            SerialBreadcrumb("RL_SAFE_SKIP_MOUSE_READ");
            SerialBreadcrumb("RL_SAFE_SKIP_RESET_DIAG");
        } else {
            // RL_BEFORE_MOUSE_READ: about to read Control.MousePosition (managed static)
            SerialBreadcrumb("RL_BEFORE_MOUSE_READ");
            lastMouseX = Control.MousePosition.X;
            lastMouseY = Control.MousePosition.Y;
            SerialBreadcrumb("RL_AFTER_MOUSE_READ");

            lastMoveTick = Timer.Ticks;

            SerialBreadcrumb("RL_BEFORE_RESET_DIAG");
            if (isUefi) {
                ResetSafeModeDiagnostics();
                SetSafeModeInputPhase("INIT-OK");
                SetSafeModeInputResult("READY", 0);
            }
            SerialBreadcrumb("RL_AFTER_RESET_DIAG");
        }

        SerialBreadcrumb("RL_BEFORE_LOOP");
        for (; ; ) {
            try {
                // RL_LOOP_TOP
                SerialBreadcrumb("RL_LOOP_TOP");

                // RL_BEFORE_FRAME_COUNTER
                SerialBreadcrumb("RL_BEFORE_FRAME_COUNTER");
                frameCounter++;
                SerialBreadcrumb("RL_AFTER_FRAME_COUNTER");

                if (!useSafeUefiPrologue || frameCounter > 1) {
                    SetSafeModeFramePhase("frame-enter");
                }

                if (isUefi && (!useSafeUefiPrologue || frameCounter > 1)) {
                    UpdateSafeModeRenderDiagnostics(frameCounter);
                }

                // PROLOGUE ONLY: execute frame counter + serial markers, skip all rendering
                if (UEFI_RENDER_LOOP_PROLOGUE_ONLY && isUefi) {
                    switch (frameCounter) {
                        case  1: SerialBreadcrumb("RL_PROLOGUE_ONLY_F1"); break;
                        case  2: SerialBreadcrumb("RL_PROLOGUE_ONLY_F2"); break;
                        case  3: SerialBreadcrumb("RL_PROLOGUE_ONLY_F3"); break;
                        case  4: SerialBreadcrumb("RL_PROLOGUE_ONLY_F4"); break;
                        case  5: SerialBreadcrumb("RL_PROLOGUE_ONLY_F5"); break;
                        case  6: SerialBreadcrumb("RL_PROLOGUE_ONLY_F6"); break;
                        case  7: SerialBreadcrumb("RL_PROLOGUE_ONLY_F7"); break;
                        case  8: SerialBreadcrumb("RL_PROLOGUE_ONLY_F8"); break;
                        case  9: SerialBreadcrumb("RL_PROLOGUE_ONLY_F9"); break;
                        case 10: SerialBreadcrumb("RL_PROLOGUE_ONLY_FA"); break;
                        default: break;
                    }
                    for (int _d = 0; _d < 500000; _d++) { }
                    continue;
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

                // RL_BEFORE_F_MARKER
                SerialBreadcrumb("RL_BEFORE_F_MARKER");
                if (debugFrame) { SerialChar('['); SerialChar('F'); SerialChar((char)('0' + (frameCounter % 10))); SerialChar(':'); }
                SerialBreadcrumb("RL_BEFORE_A_MARKER");
                if (debugFrame) { SerialChar('A'); SerialChar(']'); } // A = start
                SerialBreadcrumb("RL_AFTER_A_MARKER");

                bool shouldLog = (frameCounter == 1);

                // CRITICAL: Ensure Graphics object exists and points at the real framebuffer.
                // In UEFI mode, this is only safe during the first GUI frame; repeated
                // managed-reference repair can trip over post-ExitBootServices state.
                SetSafeModeFramePhase("render-enter");
                SerialBreadcrumb("RL_BEFORE_ENSURE_GRAPHICS");
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
                SerialBreadcrumb("RL_AFTER_ENSURE_GRAPHICS");
                SetSafeModeFramePhase("render-exit");

                SerialBreadcrumb("RL_BEFORE_B_MARKER");
                if (debugFrame) SerialChar('B'); // B = EnsureGraphics done
                SerialBreadcrumb("RL_AFTER_B_MARKER");

                // -----------------------------------------------------------------
                // UEFI steady-state minimal render path (frame 2+)
                // Allocation-free: no .ToString(), no string concat, no new objects,
                // no desktop/WM/input calls, no diagnostics panel.
                // Draws one 16x16 colour-toggling block as a visible heartbeat.
                // Serial pattern: [Fn:M] where n = frame digit (2-9, then 0).
                // -----------------------------------------------------------------
                if (isUefi && UEFI_STEADY_STATE_MINIMAL_RENDER && frameCounter > 1) {
                    // Serial breadcrumb: [Fn:M]
                    SerialChar('['); SerialChar('F');
                    // Fixed per-frame markers 2–10 without dynamic string allocation
                    switch (frameCounter) {
                        case  2: SerialChar('2'); break;
                        case  3: SerialChar('3'); break;
                        case  4: SerialChar('4'); break;
                        case  5: SerialChar('5'); break;
                        case  6: SerialChar('6'); break;
                        case  7: SerialChar('7'); break;
                        case  8: SerialChar('8'); break;
                        case  9: SerialChar('9'); break;
                        case 10: SerialChar('A'); break;
                        default: SerialChar((char)('0' + (frameCounter % 10))); break;
                    }
                    SerialChar(':'); SerialChar('M'); SerialChar(']');

                    // Heartbeat block: top-left 16x16, alternates white / teal every 30 frames
                    if (TryGetUefiFramebufferInfo(out uint* hbFb, out int hbW, out int hbH, out int hbPitch, out ulong hbMax)) {
                        uint hbColor = ((frameCounter / 30) & 1) == 0 ? 0xFF00D4C0u : 0xFFFFFFFFu;
                        int hbEndX = hbW < 16 ? hbW : 16;
                        int hbEndY = hbH < 16 ? hbH : 16;
                        for (int hy = 0; hy < hbEndY; hy++) {
                            ulong rowBase = (ulong)(uint)hy * (ulong)(uint)hbPitch;
                            for (int hx = 0; hx < hbEndX; hx++) {
                                ulong off = rowBase + (ulong)(uint)hx;
                                if (hbMax != 0 && off >= hbMax) break;
                                if (off <= (ulong)int.MaxValue) hbFb[(int)off] = hbColor;
                            }
                        }
                    }

                    Thread.Sleep(16);
                    continue;
                }

                // UEFI steady-state serial-only fast path (fallback / safest mode).
                // UEFI_STEADY_STATE_SERIAL_ONLY must be true and MINIMAL_RENDER false.
                // Serial pattern: [Fn:S].  No visual output at all.
                if (isUefi && UEFI_STEADY_STATE_SERIAL_ONLY && frameCounter > 1) {
                    if (debugFrame) {
                        SerialChar('['); SerialChar('F');
                        SerialChar((char)('0' + (frameCounter % 10)));
                        SerialChar(':'); SerialChar('S'); SerialChar(']');
                    }
                    Thread.Sleep(16);
                    continue;
                }

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

                if (isUefi) {
                    if (frameCounter == 1) SerialBreadcrumb("F1_BEGIN");

                    if (frameCounter == 1) SerialBreadcrumb("F1_UI_ENTER");
                    if (FIRST_FRAME_SERIAL_ONLY && frameCounter == 1) {
                        if (!SKIP_BACKGROUND_DRAW) {
                            SerialBreadcrumb("F1_BACKGROUND_ENTER");
                            if (!TryDrawUefiFirstFrameBackground(frameCounter, true)) {
                                SerialBreadcrumb("F1_BACKGROUND_SKIPPED");
                            }
                            SerialBreadcrumb("F1_BACKGROUND_EXIT");
                        }

                        if (!SKIP_UI_DRAW) {
                            SerialBreadcrumb("F1_UI_EXIT");
                        }

                        if (!SKIP_DESKTOP_DRAW) {
                            SerialBreadcrumb("F1_DESKTOP_ENTER");
                            SerialBreadcrumb("F1_DESKTOP_EXIT");
                        }

                        if (!SKIP_ICON_DRAW) {
                            SerialBreadcrumb("F1_ICONS_ENTER");
                            SerialBreadcrumb("F1_ICONS_EXIT");
                        }

                        if (!SKIP_TASKBAR_DRAW) {
                            SerialBreadcrumb("F1_TASKBAR_ENTER");
                            SerialBreadcrumb("F1_START_BUTTON_ENTER");
                            SerialBreadcrumb("F1_START_BUTTON_EXIT");
                            SerialBreadcrumb("F1_TASKBAR_EXIT");
                        }

                        if (!SKIP_WINDOWMANAGER_DRAW) {
                            SerialBreadcrumb("F1_WINDOWMANAGER_ENTER");
                            SerialBreadcrumb("F1_WINDOWMANAGER_EXIT");
                        }

                        if (!SKIP_DIAGNOSTICS_DRAW) {
                            SerialBreadcrumb("F1_DIAGNOSTICS_ENTER");
                            SerialBreadcrumb("F1_DIAGNOSTICS_EXIT");
                        }

                        if (!SKIP_CURSOR_DRAW) {
                            SerialBreadcrumb("F1_CURSOR_ENTER");
                            SerialBreadcrumb("F1_CURSOR_EXIT");
                        }

                        if (!SKIP_PRESENT) {
                            SerialBreadcrumb("F1_PRESENT_ENTER");
                            SerialBreadcrumb("F1_PRESENT_EXIT");
                        }

                        if (frameCounter == 1) SerialBreadcrumb("F1_END");
                        Thread.Sleep(16);
                        continue;
                    }

                    if (frameCounter == 1) SerialBreadcrumb("F1_BACKGROUND_ENTER");
                    if (frameCounter == 1 && !SKIP_BACKGROUND_DRAW) {
                        if (ShouldSkipFirstFrameBackgroundDraw(frameCounter)) {
                            SerialBackgroundReason("FIRST_FRAME_SKIP_SWITCH");
                            SerialBreadcrumb("F1_BACKGROUND_SKIPPED");
                        } else {
                            try {
                                DrawUefiReadyDesktop(false);
                            } catch {
                                SerialBackgroundReason("DRAW_EXCEPTION");
                                SerialBreadcrumb("F1_BACKGROUND_SKIPPED");
                            }
                        }
                    } else if (frameCounter == 1 && SKIP_BACKGROUND_DRAW) {
                        DrawUefiHeartbeat(frameCounter);
                    }
                    if (frameCounter == 1) SerialBreadcrumb("F1_BACKGROUND_EXIT");

                    if (frameCounter == 1) SerialBreadcrumb("F1_DESKTOP_ENTER");
                    if (!SKIP_DESKTOP_DRAW) {
                        // UEFI desktop draw is represented by the ready desktop render above.
                    }
                    if (frameCounter == 1) SerialBreadcrumb("F1_DESKTOP_EXIT");

                    if (frameCounter == 1) SerialBreadcrumb("F1_ICONS_ENTER");
                    if (frameCounter == 1) SerialBreadcrumb("F1_ICONS_EXIT");

                    if (frameCounter == 1) SerialBreadcrumb("F1_TASKBAR_ENTER");
                    if (frameCounter == 1) SerialBreadcrumb("F1_START_BUTTON_ENTER");
                    if (frameCounter == 1) SerialBreadcrumb("F1_START_BUTTON_EXIT");
                    if (frameCounter == 1) SerialBreadcrumb("F1_TASKBAR_EXIT");

                    if (frameCounter == 1) SerialBreadcrumb("F1_UI_EXIT");

                    if (frameCounter == 1) SerialBreadcrumb("F1_DIAGNOSTICS_ENTER");
                    if (!SKIP_DIAGNOSTICS_DRAW) {
                        // Guard: managed string allocations in DrawUefiSafeModeDiagnostics() are
                        // unsafe in the UEFI per-frame path. Only allow on frame 1, or when the
                        // per-frame diagnostic constant is explicitly enabled (default: false).
                        bool runDiag = UEFI_DRAW_DIAGNOSTICS_EACH_FRAME ||
                                       (UEFI_DIAGNOSTICS_FRAME1_ONLY && frameCounter == 1);
                        if (runDiag) {
                            DrawUefiSafeModeDiagnostics();
                        }
                    }
                    if (frameCounter == 1) SerialBreadcrumb("F1_DIAGNOSTICS_EXIT");
                }

                if (debugFrame) SerialChar('C'); // C = pre-mouse

                // SAFE_INPUT_NONE hard gate: skip all input-related updates when no input
                // backend is active. This covers mouse dispatcher, UEFI pointer polling,
                // window manager input pass, PS/2, Kbd2Mouse, and MouseInputManager update.
                bool inputActive = ActiveSafeModeInputBackend != SafeModeInputBackend.SAFE_INPUT_NONE;

                // Poll mouse input
                if (inputActive) {
                    try {
                        MouseEventDispatcher.Update();
                    } catch { }
                }

                if (isUefi) {
                    SetSafeModeFramePhase("input-enter");
                    if (inputActive) {
                        PollUefiInput();
                    }
                    SetSafeModeFramePhase("input-exit");
                }

                if (debugFrame) SerialChar('D'); // D = post-mouse

                // Per-frame input pass — skip when no input backend is active
                WindowManager.MouseHandled = false;
                if (inputActive) {
                    try {
                        WindowManager.InputAll();
                    } catch { }
                }

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
                        SerialFramePhaseEnter(frameCounter, "BACKGROUND");
                        TryDrawUefiSteadyStateBackground(frameCounter);
                        SerialFramePhaseExit(frameCounter, "BACKGROUND");
                    }
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('G'); }
                    if (isUefi) {
                        SerialFrameBackgroundReason(frameCounter, "DISPATCH_EXCEPTION");
                        SerialFrameBackgroundSkipped(frameCounter);
                        SerialFramePhaseExit(frameCounter, "BACKGROUND");
                    }
                }

                if (debugFrame) SerialChar('H'); // H = post-background

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
                    if (frameCounter == 1) SerialBreadcrumb("F1_CURSOR_ENTER");
                    if (!SKIP_CURSOR_DRAW) {
                        DrawUefiCursor();
                    }
                    if (frameCounter == 1) SerialBreadcrumb("F1_CURSOR_EXIT");
                }

                if (debugFrame) SerialChar('L'); // L = pre-update

                //refresh screen
                try {
                    if (isUefi && frameCounter == 1) SerialBreadcrumb("F1_PRESENT_ENTER");
                    if (!SKIP_PRESENT) {
                        SetSafeModeFramePhase("framebuffer-update");
                        Framebuffer.Update();
                    }
                    SetSafeModeFramePhase("frame-exit");
                    CompleteSafeModeFrame("frame-exit");
                    if (isUefi && frameCounter == 1) SerialBreadcrumb("F1_PRESENT_EXIT");
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
                    if (ShouldEmitExplicitFrameBreadcrumbs(frameCounter)) {
                        SerialFrameBreadcrumb(frameCounter, "BEGIN");
                    }

                    SerialFramePhaseEnter(frameCounter, "BACKGROUND");
                    if (UEFI_STEADY_STATE_HEARTBEAT_ENABLED) {
                        DrawUefiHeartbeat(frameCounter);
                    }
                    SerialFramePhaseExit(frameCounter, "BACKGROUND");
                    SerialFramePhaseEnter(frameCounter, "DESKTOP");
                    SerialFramePhaseExit(frameCounter, "DESKTOP");
                    SerialFramePhaseEnter(frameCounter, "ICONS");
                    SerialFramePhaseExit(frameCounter, "ICONS");
                    SerialFramePhaseEnter(frameCounter, "TASKBAR");
                    SerialFramePhaseExit(frameCounter, "TASKBAR");
                    SerialFramePhaseEnter(frameCounter, "WINDOWMANAGER");
                    SerialFramePhaseExit(frameCounter, "WINDOWMANAGER");
                    SerialFramePhaseEnter(frameCounter, "DIAGNOSTICS");
                    SerialFramePhaseExit(frameCounter, "DIAGNOSTICS");
                    SerialFramePhaseEnter(frameCounter, "CURSOR");
                    SerialFramePhaseExit(frameCounter, "CURSOR");
                    SerialFramePhaseEnter(frameCounter, "PRESENT");
                    SerialFramePhaseExit(frameCounter, "PRESENT");
                    SerialFramePhaseEnter(frameCounter, "TAIL");
                    if ((frameCounter & 0xFFFF) == 0) {
                        SerialChar('H'); SerialChar('B'); SerialChar('\n');
                    }
                    SerialFramePhaseExit(frameCounter, "TAIL");
                    if (ShouldEmitExplicitFrameBreadcrumbs(frameCounter)) {
                        SerialFrameBreadcrumb(frameCounter, "END");
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
    /// Minimal UEFI-only render loop bypass for prologue isolation.
    /// </summary>
    private static void RenderLoopUefiTinyBypass() {
        SerialBreadcrumb("UTINY_AFTER_ENTER");

        if (UEFI_TINY_RENDER_LOOP_ENTRY_ONLY) {
            SerialBreadcrumb("UTINY_ENTRY_ONLY");
            for (; ; ) {
                Native.Out8(0x3F8, (byte)'.');
                for (int _t = 0; _t < 1000000; _t++) { }
            }
        }

        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI && UEFI_TINY_RENDER_LOOP_MINIMAL_GRAPHICS) {
            SerialBreadcrumb("UTINY_GRAPHICS_ENTER");
            SerialBreadcrumb("UTINY_F1_A");
            SerialBreadcrumb("UTINY_BEFORE_ENSURE_GRAPHICS");
            Framebuffer.EnsureGraphics();
            SerialBreadcrumb("UTINY_AFTER_ENSURE_GRAPHICS");
            SerialBreadcrumb("UTINY_B");
            SerialBreadcrumb("UTINY_C1");
            int fbW = Framebuffer.Width;
            SerialBreadcrumb("UTINY_C2");
            int fbH = Framebuffer.Height;
            SerialBreadcrumb("UTINY_C3");
            var graphics = Framebuffer.Graphics;
            SerialBreadcrumb("UTINY_C4");
            bool hasGraphics = graphics != null;
            SerialBreadcrumb("UTINY_C5");
            bool hasVideoMemory = hasGraphics && graphics.VideoMemory != null;
            SerialBreadcrumb("UTINY_C6");
            bool hasDimensions = fbW > 0 && fbH > 0;
            SerialBreadcrumb("UTINY_C7");
            if (hasGraphics) {
                if (graphics.Width != fbW) graphics.Width = fbW;
                if (graphics.Height != fbH) graphics.Height = fbH;
            }
            if (hasGraphics && hasVideoMemory && hasDimensions) {
                SerialBreadcrumb("UTINY_C8");
                graphics.Clear(0xFF0D7D77u);
                SerialBreadcrumb("UTINY_C9");
                int hbW = fbW < 16 ? fbW : 16;
                SerialBreadcrumb("UTINY_C10");
                int hbH = fbH < 16 ? fbH : 16;
                SerialBreadcrumb("UTINY_C11");
                int hbX = 8;
                SerialBreadcrumb("UTINY_C12");
                int hbY = 8;
                SerialBreadcrumb("UTINY_C13");
                uint hbColor = 0xFF00D4C0u;
                SerialBreadcrumb("UTINY_C14");
                if (hbX + hbW > fbW) {
                    hbX = 0;
                }
                SerialBreadcrumb("UTINY_C15");
                if (hbY + hbH > fbH) {
                    hbY = 0;
                }
                SerialBreadcrumb("UTINY_C16");
                graphics.FillRectangle(hbX, hbY, hbW, hbH, hbColor);
                SerialBreadcrumb("UTINY_C17");
                DrawUefiTinyProofPattern(graphics, fbW, fbH, 1);
                SerialBreadcrumb("UTINY_PATTERN_FRAME1_DRAWN");
                SerialBreadcrumb("UTINY_PATTERN_SAMPLE_BEGIN");
                SerialBreadcrumb("UTINY_SAMPLE_TL");
                SerialWriteHex((ulong)graphics.GetPoint(0, 0));
                SerialChar('\n');
                SerialBreadcrumb("UTINY_SAMPLE_CENTER");
                SerialWriteHex((ulong)graphics.GetPoint(fbW / 2, fbH / 2));
                SerialChar('\n');
                SerialBreadcrumb("UTINY_SAMPLE_BR");
                SerialWriteHex((ulong)graphics.GetPoint(fbW - 1, fbH - 1));
                SerialChar('\n');
                SerialBreadcrumb("UTINY_PATTERN_SAMPLE_END");
            }
            SerialBreadcrumb("UTINY_F1_END");
            for (int frame = 2; ; frame++) {
                if (frame == 2) {
                    SerialBreadcrumb("UTINY_C18");
                    if (graphics != null && graphics.VideoMemory != null && fbW > 0 && fbH > 0) {
                        SerialBreadcrumb("UTINY_C19");
                        graphics.FillRectangle(12, 12, 16, 16, 0xFF36C2B4u);
                        SerialBreadcrumb("UTINY_C20");
                    }
                } else if (frame == 3) {
                    SerialBreadcrumb("UTINY_C21");
                    if (graphics != null && graphics.VideoMemory != null && fbW > 0 && fbH > 0) {
                        SerialBreadcrumb("UTINY_C22");
                        graphics.FillRectangle(12, 12, 16, 16, 0xFF00D4C0u);
                        SerialBreadcrumb("UTINY_C23");
                    }
                }
                if (graphics != null && graphics.VideoMemory != null && fbW > 0 && fbH > 0) {
                    DrawUefiTinyProofPattern(graphics, fbW, fbH, frame);
                }
                if (UEFI_TINY_RENDER_HEARTBEAT_ENABLED && (frame % UEFI_TINY_RENDER_HEARTBEAT_PERIOD_FRAMES) == 0) {
                    Native.Out8(0x3F8, (byte)'.');
                }
                for (int _t = 0; _t < 1000000; _t++) { }
            }
        }
    }

    /// <summary>
    /// Diagnostic UEFI normal-desktop probe.
    /// Runs the first-frame desktop operations one at a time with raw serial markers.
    /// </summary>
    private static void RenderLoopNormalDesktopStepProbe() {
        SerialBreadcrumb("NORM_PROBE_ENTER");
        WindowManager.ProbeSerialMode = true;

        bool graphicsReady = false;

        SerialBreadcrumb("NORM_STEP_001_ENTER");
        SerialBreadcrumb(Framebuffer.Graphics == null ? "NORM_STEP_001_GFX=NULL" : "NORM_STEP_001_GFX=OK");
        Framebuffer.EnsureGraphics();
        SerialBreadcrumb(Framebuffer.Graphics == null ? "NORM_STEP_001_AFTER_GFX=NULL" : "NORM_STEP_001_AFTER_GFX=OK");
        SerialBreadcrumb("NORM_STEP_001_EXIT");

        if (Framebuffer.Graphics == null || Framebuffer.Graphics.VideoMemory == null) {
            SerialBreadcrumb("NORM_STEP_GFX_ABORT");
            for (; ; ) {
                Native.Out8(0x3F8, (byte)'.');
                Thread.Sleep(16);
            }
        }

        graphicsReady = true;

        SerialBreadcrumb("NORM_STEP_002_ENTER");
        SerialBreadcrumb("NORM_STEP_002_GFX_REPAIR_BEGIN");
        if (Framebuffer.Width != 0 && Framebuffer.Graphics.Width != Framebuffer.Width) {
            Framebuffer.Graphics.Width = Framebuffer.Width;
        }
        if (Framebuffer.Height != 0 && Framebuffer.Graphics.Height != Framebuffer.Height) {
            Framebuffer.Graphics.Height = Framebuffer.Height;
        }
        SerialBreadcrumb("NORM_STEP_002_GFX_REPAIR_END");
        SerialBreadcrumb("NORM_STEP_002_EXIT");
        LogNormalDesktopStepProbeState("NORM_STEP_002_STATE", Framebuffer.Graphics, 0xFF0D7D77u);
        ReportNormalDesktopStepProbeGraphicsCallsites();

        SerialBreadcrumb("NORM_STEP_003_ENTER");
        SerialBreadcrumb("NORM_STEP_003_A_ENTER");
        guideXOS.Graph.Graphics graphics = Framebuffer.Graphics;
        LogNormalDesktopStepProbeState("NORM_STEP_003_A_STATE", graphics, 0xFF0D7D77u);
        SerialBreadcrumb("NORM_STEP_003_A_EXIT");

        SerialBreadcrumb("NORM_STEP_003_B_ENTER");
        if (graphics != null) {
            if (Framebuffer.Width != 0 && graphics.Width != Framebuffer.Width) {
                graphics.Width = Framebuffer.Width;
            }
            if (Framebuffer.Height != 0 && graphics.Height != Framebuffer.Height) {
                graphics.Height = Framebuffer.Height;
            }
        }
        LogNormalDesktopStepProbeState("NORM_STEP_003_B_STATE", graphics, 0xFF0D7D77u);
        SerialBreadcrumb("NORM_STEP_003_B_EXIT");

        SerialBreadcrumb("NORM_STEP_003_C_ENTER");
        SerialBreadcrumb("NORM_STEP_003_CLEAR_METHOD=Framebuffer.Graphics.Clear(uint)");
        SerialBreadcrumb("NORM_STEP_003_CLEAR_SOURCE=UEFI cached graphics receiver guard / NativeAOT receiver-call guard");
        LogNormalDesktopStepProbeState("NORM_STEP_003_C_STATE", graphics, 0xFF0D7D77u);
        SerialBreadcrumb("NORM_STEP_003_CLEAR_CALLSITE=graphics.Clear(0xFF0B3C4Cu)");
        graphics.Clear(0xFF0B3C4Cu);
        SerialBreadcrumb("NORM_STEP_003_C_EXIT");
        SerialBreadcrumb("NORM_STEP_003_EXIT");

        SerialBreadcrumb("NORM_STEP_004_ENTER");
        SerialBreadcrumb("NORM_STEP_004_CALLSITE=Framebuffer.Graphics.FillRectangle(16,16,32,32,0xFFFF2020u)");
        Framebuffer.Graphics.FillRectangle(16, 16, 32, 32, 0xFFFF2020u);
        SerialBreadcrumb("NORM_STEP_004_EXIT");

        SerialBreadcrumb("NORM_STEP_005_ENTER");
        SerialBreadcrumb(Program.Wallpaper == null ? "NORM_STEP_005_WALLPAPER=NULL" : "NORM_STEP_005_WALLPAPER=OK");
        SerialBreadcrumb("NORM_STEP_005_CALLSITE=BackgroundRotationManager.DrawBackground()");
        BackgroundRotationManager.DrawBackground();
        SerialBreadcrumb("NORM_STEP_005_EXIT");

        SerialBreadcrumb("NORM_STEP_006_ENTER");
        SerialBreadcrumb("NORM_STEP_006_CALLSITE=Framebuffer.Graphics.FillRectangle(0,0,Framebuffer.Width,Framebuffer.Height,0xFF1E1E1Eu)");
        Framebuffer.Graphics.FillRectangle(0, 0, Framebuffer.Width, Framebuffer.Height, 0xFF1E1E1Eu);
        SerialBreadcrumb("NORM_STEP_006_EXIT");

        SerialBreadcrumb("NORM_STEP_007_ENTER");
        SerialBreadcrumb(Desktop.Taskbar == null ? "NORM_STEP_007_TASKBAR=NULL" : "NORM_STEP_007_TASKBAR=OK");
        if (Desktop.Taskbar != null) {
            Desktop.Taskbar.DrawUefiStepProbeBackground();
        }
        SerialBreadcrumb("NORM_STEP_007_EXIT");

        SerialBreadcrumb("NORM_STEP_008_ENTER");
        if (Desktop.Taskbar != null) {
            Desktop.Taskbar.DrawUefiStepProbeStartButton();
        }
        SerialBreadcrumb("NORM_STEP_008_EXIT");

        SerialBreadcrumb("NORM_STEP_009_ENTER");
        SerialBreadcrumb(Desktop.Apps == null ? "NORM_STEP_009_APPS=NULL" : "NORM_STEP_009_APPS=OK");
        SerialBreadcrumb(WindowManager.Windows == null ? "NORM_STEP_009_WINDOWS=NULL" : "NORM_STEP_009_WINDOWS=OK");
        SerialBreadcrumb(Desktop.HomeMode ? "NORM_STEP_009_HOMEMODE=1" : "NORM_STEP_009_HOMEMODE=0");
        if (Desktop.Apps != null) {
            SerialBreadcrumb("NORM_STEP_009_APP_COUNT");
            SerialWriteUnsigned((ulong)Desktop.Apps.Length);
            SerialChar('\n');
        }
        SerialBreadcrumb("NORM_STEP_009_EXIT");

        SerialBreadcrumb("NORM_STEP_010_ENTER");
        ProbeNormalDesktopIconRenderWithoutPng();
        SerialBreadcrumb("NORM_STEP_010_EXIT");

        SerialBreadcrumb("NORM_STEP_011_ENTER");
        if (NORMAL_DESKTOP_UEFI_PROBE_SKIP_WINDOW_TRAVERSAL) {
            SerialBreadcrumb("NORM_STEP_011_SKIP_ZERO_WINDOWS");
            SerialBreadcrumb("NORM_STEP_011_EXIT");
        } else {
            SerialBreadcrumb("NORM_STEP_011_A_ENTER");
            var windows = WindowManager.Windows;
            SerialBreadcrumb(windows == null ? "NORM_STEP_011_WINDOWS=NULL" : "NORM_STEP_011_WINDOWS=OK");
            if (windows != null) {
                SerialBreadcrumb("NORM_STEP_011_WINDOWS_COUNT");
                SerialWriteUnsigned((ulong)windows.Count);
                SerialChar('\n');
                SerialBreadcrumb(windows.Count == 0 ? "NORM_STEP_011_WINDOWS_ZERO=1" : "NORM_STEP_011_WINDOWS_ZERO=0");
            }
            SerialBreadcrumb("NORM_STEP_011_A_EXIT");

            SerialBreadcrumb("NORM_STEP_011_B_ENTER");
            if (windows != null) {
                SerialBreadcrumb("NORM_STEP_011_B_TRAVERSAL_BEGIN");
                SerialBreadcrumb("NORM_STEP_011_B_FIRST_LOOP_CONDITION_ENTER");
                bool step11HasWindows = windows.Count > 0;
                if (windows.Count == 0) {
                    SerialBreadcrumb("NORM_STEP_011_B_FIRST_LOOP_CONDITION_FALSE");
                    SerialBreadcrumb("NORM_STEP_011_B_PER_WINDOW_BODY_SKIPPED_ZERO_WINDOWS");
                } else {
                    SerialBreadcrumb("NORM_STEP_011_B_FIRST_LOOP_CONDITION_TRUE");
                    SerialBreadcrumb("NORM_STEP_011_B_PER_WINDOW_BODY_ENTER");
                }
                SerialBreadcrumb("NORM_STEP_011_B_CALLSITE=WindowManager.DrawAllExceptTaskManager()");
                WindowManager.DrawAllExceptTaskManager();
                if (step11HasWindows) {
                    SerialBreadcrumb("NORM_STEP_011_B_PER_WINDOW_BODY_EXIT");
                }
                SerialBreadcrumb("NORM_STEP_011_B_TRAVERSAL_EXIT");
            } else {
                SerialBreadcrumb("NORM_STEP_011_B_WINDOWS_TRAVERSAL_SKIPPED");
            }
            SerialBreadcrumb("NORM_STEP_011_B_EXIT");

            SerialBreadcrumb("NORM_STEP_011_C_ENTER");
            if (windows != null) {
                SerialBreadcrumb("NORM_STEP_011_C_CALLSITE=WindowManager.DrawTaskManager()");
                WindowManager.DrawTaskManager();
                SerialBreadcrumb("NORM_STEP_011_C_CALLSITE_EXIT");
            } else {
                SerialBreadcrumb("NORM_STEP_011_C_SKIP_DRAW_TASK_MANAGER");
            }
            SerialBreadcrumb("NORM_STEP_011_C_EXIT");
            SerialBreadcrumb("NORM_STEP_011_EXIT");
        }

        SerialBreadcrumb("NORM_STEP_012_ENTER");
        var probeFont = WindowManager.font;
        const string probeText = "NORM STEP FONT";

        SerialBreadcrumb("NORM_STEP_012_A_ENTER");
        SerialBreadcrumb(probeFont == null ? "NORM_STEP_012_FONT=NULL" : "NORM_STEP_012_FONT=OK");
        SerialBreadcrumb(probeFont == null ? "NORM_STEP_012_TEXT_RENDERER=NULL" : "NORM_STEP_012_TEXT_RENDERER=OK");
        SerialBreadcrumb("NORM_STEP_012_A_EXIT");

        SerialBreadcrumb("NORM_STEP_012_B_ENTER");
        SerialBreadcrumb(probeText == null ? "NORM_STEP_012_TEXT=NULL" : "NORM_STEP_012_TEXT=OK");
        if (probeText != null) {
            SerialBreadcrumb("NORM_STEP_012_TEXT_LENGTH");
            SerialWriteUnsigned((ulong)probeText.Length);
            SerialChar('\n');
            if (probeText.Length > 0) {
                SerialBreadcrumb("NORM_STEP_012_TEXT_FIRST_CHAR_CODE");
                SerialWriteUnsigned((ulong)probeText[0]);
                SerialChar('\n');
            }
        }
        SerialBreadcrumb("NORM_STEP_012_B_EXIT");

        SerialBreadcrumb("NORM_STEP_012_C_ENTER");
        if (NORMAL_DESKTOP_UEFI_PROBE_SAFE_FONT_PLACEHOLDER) {
            SerialBreadcrumb("NORM_STEP_012_SAFE_FONT_PLACEHOLDER=1");
            var probeGraphics = Framebuffer.Graphics;
            if (probeGraphics != null && probeGraphics.VideoMemory != null && probeGraphics.Width > 0 && probeGraphics.Height > 0) {
                probeGraphics.FillRectangle(24, 24, 160, 24, 0xFF263238u);
            }
            SerialBreadcrumb("NORM_STEP_012_SAFE_FONT_PLACEHOLDER_EXIT");
        } else if (probeFont != null) {
            int probeTextW = ProbeNormalDesktopStep12MeasureString(probeFont, probeText);
            SerialBreadcrumb("NORM_STEP_012_DRAW_ENTER");
            probeFont.DrawString(24, 24, probeText, probeTextW > 0 ? probeTextW : 240, probeFont.FontSize);
            SerialBreadcrumb("NORM_STEP_012_DRAW_EXIT");
        }
        SerialBreadcrumb("NORM_STEP_012_C_EXIT");
        SerialBreadcrumb("NORM_STEP_012_EXIT");

        SerialBreadcrumb("NORM_STEP_013_ENTER");
        SerialBreadcrumb("NORM_STEP_013_A_ENTER");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_SKIP_CURSOR_DRAW ? "NORM_STEP_013_PROBE_SKIP_CURSOR_DRAW=1" : "NORM_STEP_013_PROBE_SKIP_CURSOR_DRAW=0");
        SerialBreadcrumb(SKIP_CURSOR_DRAW ? "NORM_STEP_013_GLOBAL_SKIP_CURSOR_DRAW=1" : "NORM_STEP_013_GLOBAL_SKIP_CURSOR_DRAW=0");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_SKIP_CURSOR_DRAW ? "NORM_STEP_013_CURSOR_ENABLED=0" : "NORM_STEP_013_CURSOR_ENABLED=1");
        SerialBreadcrumb(NORMAL_DESKTOP_UEFI_PROBE_CURSOR_PLACEHOLDER ? "NORM_STEP_013_CURSOR_PLACEHOLDER=1" : "NORM_STEP_013_CURSOR_PLACEHOLDER=0");
        SerialBreadcrumb(Cursor == null ? "NORM_STEP_013_CURSOR_IMAGE=NULL" : "NORM_STEP_013_CURSOR_IMAGE=OK");
        SerialBreadcrumb(CursorMoving == null ? "NORM_STEP_013_CURSOR_MOVING=NULL" : "NORM_STEP_013_CURSOR_MOVING=OK");
        SerialBreadcrumb(CursorBusy == null ? "NORM_STEP_013_CURSOR_BUSY=NULL" : "NORM_STEP_013_CURSOR_BUSY=OK");
        SerialBreadcrumb("NORM_STEP_013_A_EXIT");

        SerialBreadcrumb("NORM_STEP_013_B_ENTER");
        SerialBreadcrumb(Framebuffer.Width > 0 && Framebuffer.Height > 0 ? "NORM_STEP_013_FRAMEBUFFER_SIZE=OK" : "NORM_STEP_013_FRAMEBUFFER_SIZE=INVALID");
        SerialBreadcrumb(Control.MousePosition.X >= 0 && Control.MousePosition.X < Framebuffer.Width ? "NORM_STEP_013_CURSOR_X_BOUNDS=OK" : "NORM_STEP_013_CURSOR_X_BOUNDS=OUT");
        SerialBreadcrumb(Control.MousePosition.Y >= 0 && Control.MousePosition.Y < Framebuffer.Height ? "NORM_STEP_013_CURSOR_Y_BOUNDS=OK" : "NORM_STEP_013_CURSOR_Y_BOUNDS=OUT");
        SerialBreadcrumb("NORM_STEP_013_B_EXIT");

        SerialBreadcrumb("NORM_STEP_013_C_ENTER");
        if (NORMAL_DESKTOP_UEFI_PROBE_SKIP_CURSOR_DRAW) {
            SerialBreadcrumb("NORM_STEP_013_CURSOR_DRAW_SKIPPED");
        } else if (NORMAL_DESKTOP_UEFI_PROBE_CURSOR_PLACEHOLDER) {
            SerialBreadcrumb("NORM_STEP_013_CURSOR_PLACEHOLDER_ENTER");
            DrawUefiCursorPlaceholder();
            SerialBreadcrumb("NORM_STEP_013_CURSOR_PLACEHOLDER_EXIT");
        } else {
            SerialBreadcrumb("NORM_STEP_013_CURSOR_DRAW_ENTER");
            SerialBreadcrumb("NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_ENTER");
            DrawUefiCursor();
            SerialBreadcrumb("NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_EXIT");
            SerialBreadcrumb("NORM_STEP_013_CURSOR_DRAW_EXIT");
        }
        SerialBreadcrumb("NORM_STEP_013_C_EXIT");
        SerialBreadcrumb("NORM_STEP_013_EXIT");

        SerialBreadcrumb("NORM_STEP_014_ENTER");

        SerialBreadcrumb("NORM_STEP_014_A_ENTER");
        SerialBreadcrumb("NORM_STEP_014_FRAMEBUFFER_ACQUIRE_ENTER");
        var step14Graphics = Framebuffer.Graphics;
        SerialBreadcrumb(step14Graphics == null ? "NORM_STEP_014_FRAMEBUFFER_ACQUIRE=NULL" : "NORM_STEP_014_FRAMEBUFFER_ACQUIRE=OK");
        SerialBreadcrumb(step14Graphics != null && step14Graphics.VideoMemory != null ? "NORM_STEP_014_FRAMEBUFFER_READY=OK" : "NORM_STEP_014_FRAMEBUFFER_READY=INVALID");
        SerialBreadcrumb("NORM_STEP_014_FRAMEBUFFER_ACQUIRE_EXIT");
        SerialBreadcrumb("NORM_STEP_014_A_EXIT");

        SerialBreadcrumb("NORM_STEP_014_B_ENTER");
        SerialBreadcrumb(step14Graphics != null ? "NORM_STEP_014_PRESENT_TARGET=GRAPHICS" : "NORM_STEP_014_PRESENT_TARGET=NULL");
        SerialBreadcrumb("NORM_STEP_014_B_EXIT");

        SerialBreadcrumb("NORM_STEP_014_C_ENTER");
        SerialBreadcrumb("NORM_STEP_014_FLUSH_INVALIDATE_ENTER");
        SerialBreadcrumb("NORM_STEP_014_FLUSH_INVALIDATE_EXIT");
        SerialBreadcrumb("NORM_STEP_014_C_EXIT");

        SerialBreadcrumb("NORM_STEP_014_PRESENT_ENTER");
        SerialBreadcrumb("NORM_STEP_014_PRESENT_CALLSITE=Framebuffer.Update()");
        Framebuffer.Update();
        SerialBreadcrumb("NORM_STEP_014_PRESENT_EXIT");
        SerialBreadcrumb("NORM_STEP_014_FRAME_COMPLETE");
        SerialBreadcrumb("NORM_STEP_014_EXIT");

        if (graphicsReady) {
            SerialBreadcrumb("NORM_PROBE_FRAME1_DONE");
        }

        SerialBreadcrumb("NORM_STEP_014_LOOP_ENTER");
        for (;; ) {
            Native.Out8(0x3F8, (byte)'.');
            Thread.Sleep(16);
        }
    }

    private static void ProbeNormalDesktopIconRenderWithoutPng() {
        if (Framebuffer.Graphics == null || Framebuffer.Graphics.VideoMemory == null) {
            SerialBreadcrumb("NORM_STEP_010_GFX=NULL");
            return;
        }

        guideXOS.Graph.Graphics graphics = Framebuffer.Graphics;
        Image redIcon = CreateSolidProbeIcon(32, 32, 0xFFFF2020u);
        Image greenIcon = CreateSolidProbeIcon(32, 32, 0xFF00FF00u);
        Image whiteIcon = CreateSolidProbeIcon(32, 32, 0xFFFFFFFFu);

        SerialBreadcrumb("NORM_STEP_010_RED_IMAGE_ACCESS_ENTER");
        SerialBreadcrumb(redIcon == null ? "NORM_STEP_010_RED=NULL" : "NORM_STEP_010_RED=OK");
        SerialBreadcrumb("NORM_STEP_010_RED_IMAGE_ACCESS_EXIT");
        SerialBreadcrumb(greenIcon == null ? "NORM_STEP_010_GREEN=NULL" : "NORM_STEP_010_GREEN=OK");
        SerialBreadcrumb(whiteIcon == null ? "NORM_STEP_010_WHITE=NULL" : "NORM_STEP_010_WHITE=OK");

        if (redIcon != null) {
            SerialBreadcrumb("NORM_STEP_010_RED_IMAGE_BOUNDS_ENTER");
            int redIconWidth = redIcon.Width;
            int redIconHeight = redIcon.Height;
            SerialBreadcrumb("NORM_STEP_010_RED_IMAGE_BOUNDS_EXIT");
            SerialBreadcrumb("NORM_STEP_010_RED_DRAW_PATH_ENTER");
            SerialBreadcrumb("NORM_STEP_010_RED_DRAW_ENTER");
            SerialBreadcrumb("NORM_STEP_010_RED_DRAW_CALLSITE=graphics.DrawImage(80,80,redIcon) OR graphics.FillRectangle(80,80,redIconWidth,redIconHeight,0xFFFF2020u)");
            if (NORMAL_DESKTOP_UEFI_PROBE_STEP10_RED_FILLRECT_PLACEHOLDER) {
                SerialBreadcrumb("NORM_STEP_010_RED_DRAW_PLACEHOLDER=FillRectangle");
                graphics.FillRectangle(80, 80, redIconWidth, redIconHeight, 0xFFFF2020u);
            } else {
                SerialBreadcrumb("NORM_STEP_010_RED_DRAW_PLACEHOLDER=DrawImage");
                graphics.DrawImage(80, 80, redIcon);
            }
            SerialBreadcrumb("NORM_STEP_010_RED_DRAW_EXIT");
            SerialBreadcrumb("NORM_STEP_010_RED_DRAW_PATH_EXIT");
        }
        if (greenIcon != null) {
            SerialBreadcrumb("NORM_STEP_010_GREEN_DRAW_ENTER");
            SerialBreadcrumb("NORM_STEP_010_GREEN_A_ENTER");
            SerialBreadcrumb("NORM_STEP_010_GREEN_IMAGE=OK");
            int greenIconWidth = greenIcon.Width;
            int greenIconHeight = greenIcon.Height;
            SerialBreadcrumb("NORM_STEP_010_GREEN_DIMENSIONS_READ");
            SerialBreadcrumb("NORM_STEP_010_GREEN_A_EXIT");
            SerialBreadcrumb("NORM_STEP_010_GREEN_B_ENTER");
            guideXOS.Graph.Graphics greenGraphics = graphics;
            SerialBreadcrumb("NORM_STEP_010_GREEN_RECEIVER=CACHED");
            SerialBreadcrumb("NORM_STEP_010_GREEN_DRAW_OVERLOAD=DrawImage(int,int,Image,bool-default:true)");
            SerialBreadcrumb(greenIcon.RawData == null ? "NORM_STEP_010_GREEN_RAWDATA=NULL" : "NORM_STEP_010_GREEN_RAWDATA=OK");
            SerialBreadcrumb("NORM_STEP_010_GREEN_FIRST_PIXEL_READ_ENTER");
            int greenFirstPixel = (greenIcon.RawData != null && greenIcon.RawData.Length > 0) ? greenIcon.RawData[0] : 0;
            SerialBreadcrumb("NORM_STEP_010_GREEN_FIRST_PIXEL_READ_EXIT");
            SerialBreadcrumb("NORM_STEP_010_GREEN_FIRST_PIXEL_WRITE_ENTER");
            if (greenIcon.RawData != null && greenIcon.RawData.Length > 0) {
                greenIcon.RawData[0] = greenFirstPixel;
            }
            SerialBreadcrumb("NORM_STEP_010_GREEN_FIRST_PIXEL_WRITE_EXIT");
            SerialBreadcrumb("NORM_STEP_010_GREEN_B_EXIT");
            SerialBreadcrumb("NORM_STEP_010_GREEN_C_ENTER");
            if (NORMAL_DESKTOP_UEFI_PROBE_STEP10_GREEN_FILLRECT_PLACEHOLDER) {
                SerialBreadcrumb("NORM_STEP_010_GREEN_DRAW_PLACEHOLDER=FillRectangle");
                greenGraphics.FillRectangle(128, 80, greenIconWidth, greenIconHeight, 0xFF00FF00u);
            } else {
                SerialBreadcrumb("NORM_STEP_010_GREEN_DRAW_PLACEHOLDER=DrawImage");
                SerialBreadcrumb("NORM_STEP_010_GREEN_DRAW_CALLSITE=greenGraphics.DrawImage(128,80,greenIcon)");
                greenGraphics.DrawImage(128, 80, greenIcon);
            }
            SerialBreadcrumb("NORM_STEP_010_GREEN_C_EXIT");
            SerialBreadcrumb("NORM_STEP_010_GREEN_DRAW_EXIT");
        }
        if (whiteIcon != null) {
            SerialBreadcrumb("NORM_STEP_010_WHITE_DRAW_ENTER");
            SerialBreadcrumb("NORM_STEP_010_WHITE_A_ENTER");
            SerialBreadcrumb("NORM_STEP_010_WHITE_IMAGE=OK");
            int whiteIconWidth = whiteIcon.Width;
            int whiteIconHeight = whiteIcon.Height;
            SerialBreadcrumb("NORM_STEP_010_WHITE_DIMENSIONS_READ");
            SerialBreadcrumb(whiteIcon.RawData == null ? "NORM_STEP_010_WHITE_RAWDATA=NULL" : "NORM_STEP_010_WHITE_RAWDATA=OK");
            SerialBreadcrumb("NORM_STEP_010_WHITE_A_EXIT");
            SerialBreadcrumb("NORM_STEP_010_WHITE_B_ENTER");
            guideXOS.Graph.Graphics whiteGraphics = graphics;
            SerialBreadcrumb("NORM_STEP_010_WHITE_RECEIVER=CACHED");
            SerialBreadcrumb("NORM_STEP_010_WHITE_DRAW_OVERLOAD=DrawImage(int,int,Image,bool-default:true)");
            SerialBreadcrumb("NORM_STEP_010_WHITE_FIRST_PIXEL_READ_ENTER");
            int whiteFirstPixel = (whiteIcon.RawData != null && whiteIcon.RawData.Length > 0) ? whiteIcon.RawData[0] : 0;
            SerialBreadcrumb("NORM_STEP_010_WHITE_FIRST_PIXEL_READ_EXIT");
            SerialBreadcrumb("NORM_STEP_010_WHITE_FIRST_PIXEL_WRITE_ENTER");
            if (whiteIcon.RawData != null && whiteIcon.RawData.Length > 0) {
                whiteIcon.RawData[0] = whiteFirstPixel;
            }
            SerialBreadcrumb("NORM_STEP_010_WHITE_FIRST_PIXEL_WRITE_EXIT");
            SerialBreadcrumb("NORM_STEP_010_WHITE_B_EXIT");
            SerialBreadcrumb("NORM_STEP_010_WHITE_C_ENTER");
            if (NORMAL_DESKTOP_UEFI_PROBE_STEP10_WHITE_FILLRECT_PLACEHOLDER) {
                SerialBreadcrumb("NORM_STEP_010_WHITE_DRAW_PLACEHOLDER=FillRectangle");
                whiteGraphics.FillRectangle(176, 80, whiteIconWidth, whiteIconHeight, 0xFFFFFFFFu);
            } else {
                SerialBreadcrumb("NORM_STEP_010_WHITE_DRAW_PLACEHOLDER=DrawImage");
                SerialBreadcrumb("NORM_STEP_010_WHITE_DRAW_CALLSITE=whiteGraphics.DrawImage(176,80,whiteIcon)");
                whiteGraphics.DrawImage(176, 80, whiteIcon);
            }
            SerialBreadcrumb("NORM_STEP_010_WHITE_C_EXIT");
            SerialBreadcrumb("NORM_STEP_010_WHITE_DRAW_EXIT");
        }
    }

    private static Image CreateSolidProbeIcon(int width, int height, uint color) {
        Image img = new Image(width, height);
        if (img == null || img.RawData == null) return img;
        for (int i = 0; i < img.RawData.Length; i++) {
            img.RawData[i] = unchecked((int)color);
        }
        return img;
    }

    private static void DrawUefiTinyProofPattern(guideXOS.Graph.Graphics graphics, int fbW, int fbH, int frameCounter) {
        if (graphics == null || graphics.VideoMemory == null || fbW <= 0 || fbH <= 0) return;

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

    private static bool DrawSafeNormalDesktopFirstFrame(bool emitVerbose = false) {
        SerialBreadcrumb("SAFE_NORMAL_DESKTOP_FRAME_ENTER");
        var graphics = Framebuffer.Graphics;
        if (graphics == null || graphics.VideoMemory == null || Framebuffer.Width == 0 || Framebuffer.Height == 0) {
            SerialBreadcrumb("SAFE_NORMAL_DESKTOP_FRAME_FB_INVALID");
            return false;
        }

        uint* fb = graphics.VideoMemory;
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;
        int pitchPixels = graphics.Width > 0 ? graphics.Width : fbW;
        ulong maxPixels = (ulong)(uint)pitchPixels * (ulong)(uint)fbH;
        SerialBreadcrumb("SAFE_NORMAL_DESKTOP_FRAME_FB_READY");
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 0, 0, fbW, fbH, 0xFF0B3C4Cu);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 0, 0, fbW, 58, 0xFF182836u);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 0, 58, fbW, 2, 0xFF36C2B4u);

        int taskbarY = fbH > 44 ? fbH - 44 : 0;
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 0, taskbarY, fbW, 44, 0xFF151A22u);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 0, taskbarY - 2, fbW, 2, 0xFF36C2B4u);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 18, taskbarY + 10, 92, 24, 0xFF263241u);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 18, taskbarY + 10, 92, 1, 0xFF3E3E3Eu);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 18, taskbarY + 10, 1, 24, 0xFF3E3E3Eu);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 109, taskbarY + 10, 1, 24, 0xFF3E3E3Eu);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 18, taskbarY + 33, 92, 1, 0xFF3E3E3Eu);

        int textWidth = fbW > 300 ? 240 : (fbW > 48 ? fbW - 48 : fbW);
        if (textWidth > 0) {
            if (emitVerbose) SerialBreadcrumb("SAFE_NORMAL_DESKTOP_TEXT_PLACEHOLDER_ENTER");
            DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 24, 18, textWidth, 20, 0xFF263241u);
            DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 24, 18, textWidth, 2, 0xFF36C2B4u);
            if (emitVerbose) SerialBreadcrumb("SAFE_NORMAL_DESKTOP_TEXT_PLACEHOLDER_EXIT");
        }

        int cursorX = fbW > 48 ? fbW - 36 : 4;
        int cursorY = fbH > 48 ? fbH - 52 : 4;
        if (emitVerbose) SerialBreadcrumb("SAFE_NORMAL_DESKTOP_CURSOR_PLACEHOLDER_ENTER");
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, cursorX, cursorY, 12, 12, 0xFFFFFFFFu);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, cursorX + 3, cursorY + 3, 6, 6, 0xFF0B3C4Cu);
        if (emitVerbose) SerialBreadcrumb("SAFE_NORMAL_DESKTOP_CURSOR_PLACEHOLDER_EXIT");

        Framebuffer.Update();
        SerialBreadcrumb("SAFE_NORMAL_DESKTOP_FRAME_COMPLETE");
        return true;
    }

    private static void RenderLoopSafeNormalDesktopFirstFrame(bool emitVerbose = false) {
        try {
            if (!DrawSafeNormalDesktopFirstFrame(emitVerbose)) {
                SerialBreadcrumb("SAFE_NORMAL_DESKTOP_FAULT=FRAMEBUFFER_INVALID");
                for (; ; ) {
                    Native.Out8(0x3F8, (byte)'!');
                    Thread.Sleep(1000);
                }
            }

            SerialBreadcrumb("SAFE_NORMAL_DESKTOP_LOOP_ENTER");
            ulong heartbeatCounter = 0;
            for (; ; ) {
                if ((heartbeatCounter % 60UL) == 0UL) {
                    Native.Out8(0x3F8, (byte)'.');
                }
                heartbeatCounter++;
                Thread.Sleep(16);
            }
        } catch {
            SerialBreadcrumb("SAFE_NORMAL_DESKTOP_FAULT=EXCEPTION");
            for (; ; ) {
                Native.Out8(0x3F8, (byte)'!');
                Thread.Sleep(1000);
            }
        }
    }

    /// <summary>
    /// One-time UEFI ready screen drawn directly to GOP memory.
    /// </summary>
    private static void DrawUefiReadyDesktop(bool emitFirstFrameBreadcrumbs) {
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_VALIDATE_ENTER");
        if (!TryGetUefiFramebufferInfo(out uint* fb, out int fbW, out int fbH, out int pitchPixels, out ulong maxPixels)) {
            if (emitFirstFrameBreadcrumbs) SerialBackgroundReason("VALIDATE_FAIL");
            return;
        }
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_VALIDATE_EXIT");

        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BACKGROUND_ENTER");
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_FILL_ENTER");
        for (int y = 0; y < fbH; y++) {
            if (emitFirstFrameBreadcrumbs && ShouldLogBackgroundRow(y, fbH - 1)) {
                SerialBreadcrumb(y == (fbH - 1) ? "F1_BG_ROW_LAST" : ("F1_BG_ROW_" + y.ToString()));
            }
            int shade = fbH > 0 ? (y * 64) / fbH : 0;
            uint color = (uint)(0xFF102030 + (shade << 16) + (shade << 8));
            DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 0, y, fbW, 1, color);
        }
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_FILL_EXIT");

        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_HEADER_ENTER");
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 0, 0, fbW, 58, 0xFF182836u);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 0, 58, fbW, 2, 0xFF36C2B4u);
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_HEADER_EXIT");
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_ICONS_ENTER");
        DrawUefiTinyText(fb, fbW, fbH, pitchPixels, maxPixels, 24, 18, "GUIDEXOS SAFE MODE", 3, 0xFFFFFFFFu);
        DrawUefiTinyText(fb, fbW, fbH, pitchPixels, maxPixels, 24, 82, "RAMDISK OK", 2, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, pitchPixels, maxPixels, 24, 110, _safeModeDiagnostics.RenderStatus, 2, 0xFFCDEEEAu);
        DrawUefiTinyText(fb, fbW, fbH, pitchPixels, maxPixels, 24, 138, _safeModeDiagnostics.InputPollStatus, 2, 0xFFCDEEEAu);
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_ICONS_EXIT");
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BACKGROUND_EXIT");

        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_TASKBAR_ENTER");
        int iconY = fbH > 260 ? 210 : 150;
        DrawUefiLauncher(fb, fbW, fbH, pitchPixels, maxPixels, 36, iconY, 0xFF47D6C8u, "FILES");
        DrawUefiLauncher(fb, fbW, fbH, pitchPixels, maxPixels, 132, iconY, 0xFFFFD166u, "APPS");
        DrawUefiLauncher(fb, fbW, fbH, pitchPixels, maxPixels, 228, iconY, 0xFFFF6B6Bu, "SETUP");
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_START_BUTTON_ENTER");
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_TASKBAR_AREA_ENTER");
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 0, fbH - 44, fbW, 44, 0xFF151A22u);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 0, fbH - 46, fbW, 2, 0xFF36C2B4u);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, 18, fbH - 34, 92, 24, 0xFF263241u);
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_TASKBAR_AREA_EXIT");
        DrawUefiTinyText(fb, fbW, fbH, pitchPixels, maxPixels, 30, fbH - 28, "START", 2, 0xFFFFFFFFu);
        DrawUefiTinyText(fb, fbW, fbH, pitchPixels, maxPixels, fbW - 212, fbH - 28, _safeModeDiagnostics.ReadyStatus, 2, 0xFFCDEEEAu);
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_START_BUTTON_EXIT");
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_TASKBAR_EXIT");
        if (emitFirstFrameBreadcrumbs) SerialBreadcrumb("F1_BG_DONE");
    }

    private static void DrawUefiFillRect(uint* fb, int fbW, int fbH, int pitchPixels, ulong maxPixels, int x, int y, int w, int h, uint color) {
        if (fb == null || fbW <= 0 || fbH <= 0 || pitchPixels <= 0 || w <= 0 || h <= 0) return;
        if (x < 0 || y < 0) return;
        if (x >= fbW || y >= fbH) return;
        int x1 = x + w;
        int y1 = y + h;
        if (x1 > fbW) x1 = fbW;
        if (y1 > fbH) y1 = fbH;
        if (x1 <= x || y1 <= y) return;

        for (int yy = y; yy < y1; yy++) {
            ulong rowBase = (ulong)(uint)yy * (ulong)(uint)pitchPixels;
            for (int xx = x; xx < x1; xx++) {
                ulong offset = rowBase + (ulong)(uint)xx;
                if (maxPixels != 0 && offset >= maxPixels) continue;
                if (offset > int.MaxValue) continue;
                fb[(int)offset] = color;
            }
        }
    }

    private static void DrawUefiSafeModeDiagnostics() {
        if (!TryGetUefiFramebufferInfo(out uint* fb, out int fbW, out int fbH, out int pitchPixels, out ulong maxPixels)) return;

        int panelX = fbW > 820 ? fbW - (SafeModeDiagnosticsPanelWidth + SafeModeDiagnosticsPanelMargin) : SafeModeDiagnosticsPanelMargin;
        int panelY = SafeModeDiagnosticsPanelY;
        int panelW = fbW > 820 ? SafeModeDiagnosticsPanelWidth : fbW - (SafeModeDiagnosticsPanelMargin * 2);
        int panelH = SafeModeDiagnosticsPanelHeight;
        if (panelW < 180 || !TryGetClippedRect(fbW, fbH, panelX, panelY, panelW, panelH, 0, 0, fbW, fbH, out int drawX0, out int drawY0, out int drawX1, out int drawY1)) return;

        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, panelX, panelY, panelW, panelH, 0xCC1A2430u);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, panelX, panelY, panelW, 2, 0xFF36C2B4u);

        int textClipX = drawX0 + 10;
        int textClipY = drawY0 + 10;
        int textClipW = (drawX1 - drawX0) - 20;
        int textClipH = (drawY1 - drawY0) - 20;
        if (textClipW <= 0 || textClipH <= 0) return;

        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 10, "SAFE MODE DIAGNOSTICS", 1, 0xFFFFFFFFu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 28, "FRAME " + _safeModeDiagnostics.FrameCounter.ToString(), 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 42, "CFRAME " + _safeModeDiagnostics.LastCompletedFrame.ToString(), 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 56, "TICK " + _safeModeDiagnostics.TimerCounter.ToString(), 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 70, "POLL " + _safeModeDiagnostics.InputPollCounter.ToString(), 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 84, "CPOLL " + _safeModeDiagnostics.LastCompletedInputPoll.ToString(), 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 98, "KEY " + _safeModeDiagnostics.KeyboardEventCounter.ToString(), 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 112, "MOUSE " + _safeModeDiagnostics.MouseEventCounter.ToString(), 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 126, "FAULT " + _safeModeDiagnostics.InputPollFaultCounter.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 140, "TIMEOUT " + _safeModeDiagnostics.InputPollTimeoutCounter.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 154, "MAXPOLL " + _safeModeDiagnostics.MaxPollDurationTicks.ToString(), 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 168, "LASTPOLL " + _safeModeDiagnostics.LastPollDurationTicks.ToString(), 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 182, "CODE " + _safeModeDiagnostics.LastInputStatusCode.ToString(), 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 196, "FRAMEPH " + _safeModeDiagnostics.LastFramePhase, 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 210, "CFRAMEPH " + _safeModeDiagnostics.LastCompletedFramePhase, 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 224, "LAST " + _safeModeDiagnostics.LastInputResult, 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 238, "PHASE " + _safeModeDiagnostics.LastInputPhase, 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 252, "SUBPH " + _safeModeDiagnostics.LastInputSubphase, 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 266, "PS2OP " + _safeModeDiagnostics.LastPs2Operation, 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 280, "PS2STS " + _safeModeDiagnostics.LastPs2StatusByte.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 294, "PS2DAT " + _safeModeDiagnostics.LastPs2DataByte.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 308, "MPKTN " + _safeModeDiagnostics.LastMousePacketByteCount.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 322, "MPKT0 " + _safeModeDiagnostics.LastMousePacketByte0.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 336, "MPKT1 " + _safeModeDiagnostics.LastMousePacketByte1.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 350, "MPKT2 " + _safeModeDiagnostics.LastMousePacketByte2.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 364, "PS2PORT " + _safeModeDiagnostics.LastPs2Port.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 378, "PS2WR " + _safeModeDiagnostics.LastPs2WriteValue.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 392, "WATCHDOG " + _safeModeDiagnostics.WatchdogSkipCounter.ToString(), 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 406, _safeModeDiagnostics.TimerStatus, 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 420, _safeModeDiagnostics.KeyboardStatus, 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 434, _safeModeDiagnostics.MouseStatus, 1, 0xFFCDEEEAu, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 448, _safeModeDiagnostics.UefiInputStatus, 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 462, _safeModeDiagnostics.Ps2Status, 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, textClipX, panelY + 476, _safeModeDiagnostics.UsbHidStatus, 1, 0xFFFFD166u, textClipX, textClipY, textClipW, textClipH);
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
        int keyboardBytesProcessed = 0;
        int mousePacketsProcessed = 0;
        _safeModeDiagnostics.InputPollCounter++;
        SetSafeModeInputPhase("POLL-ENTER");
        SetSafeModeInputResult("POLLING", 0);
        SetSafeModeInputSubphase("ps2-poll-enter");

        if (ActiveSafeModeInputBackend == SafeModeInputBackend.SAFE_INPUT_NONE) {
            SetSafeModeInputSubphase("input-disabled");
            SetSafeModeInputResult("INPUT-DISABLED", 0);
            FinalizeSafeModeInputPoll(pollStart, "INPUT DISABLED");
            return;
        }

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

                SetSafeModeInputSubphase("ps2-status-read");
                byte status = Native.In8(0x64);
                RecordSafeModePs2Read(0x64, status, "read-status");
                if ((status & 0x01) == 0) {
                    SetSafeModeInputPhase("POLL-EXIT");
                    SetSafeModeInputSubphase("input-exit");
                    SetSafeModeInputResult("NO-DATA", 0);
                    FinalizeSafeModeInputPoll(pollStart, "INPUT POLL " + _safeModeDiagnostics.InputPollCounter.ToString());
                    return;
                }

                bool fromMouse = (status & 0x20) != 0;
                SetSafeModeInputSubphase("ps2-data-read");
                byte data = Native.In8(0x60);
                RecordSafeModePs2Read(0x60, data, fromMouse ? "read-mouse-data" : "read-keyboard-data");
                if (!_uefiAnyInputByteSeen) {
                    _uefiAnyInputByteSeen = true;
                    SerialChar('D'); SerialChar('A'); SerialChar('T'); SerialChar('\n');
                }

                if (fromMouse) {
                    if (!SafeModeMouseEnabled) {
                        SetSafeModeInputSubphase("mouse-disabled-skip");
                        SetSafeModeInputResult("MOUSE-DISABLED", data);
                        continue;
                    }

                    if (mousePacketsProcessed >= SafeModeMousePacketBudget && _uefiMousePhase == 0) {
                        SetSafeModeInputSubphase("mouse-cap-reached");
                        SetSafeModeInputResult("MOUSE-CAP", (ulong)mousePacketsProcessed);
                        FinalizeSafeModeInputPoll(pollStart, "INPUT POLL " + _safeModeDiagnostics.InputPollCounter.ToString());
                        return;
                    }

                    if (ProcessUefiPs2MouseByte(data)) {
                        mousePacketsProcessed++;
                    }
                } else {
                    if (!SafeModeKeyboardEnabled) {
                        SetSafeModeInputSubphase("keyboard-disabled-skip");
                        SetSafeModeInputResult("KEYBOARD-DISABLED", data);
                        continue;
                    }

                    if (keyboardBytesProcessed >= SafeModeKeyboardByteBudget) {
                        SetSafeModeInputSubphase("keyboard-cap-reached");
                        SetSafeModeInputResult("KEYBOARD-CAP", (ulong)keyboardBytesProcessed);
                        FinalizeSafeModeInputPoll(pollStart, "INPUT POLL " + _safeModeDiagnostics.InputPollCounter.ToString());
                        return;
                    }

                    keyboardBytesProcessed++;
                    ProcessUefiPs2KeyboardByte(data);
                }
            }

            SetSafeModeInputPhase("POLL-EXIT");
            SetSafeModeInputSubphase("input-exit");
            SetSafeModeInputResult("BYTE-BUDGET", (ulong)SafeModeInputByteBudget);
            FinalizeSafeModeInputPoll(pollStart, "INPUT POLL " + _safeModeDiagnostics.InputPollCounter.ToString());
        } catch {
            _safeModeDiagnostics.InputPollFaultCounter++;
            SetSafeModeInputPhase("POLL-ERROR");
            SetSafeModeInputSubphase("poll-error");
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
    private static bool ProcessUefiPs2MouseByte(byte data) {
        if (data == 0xFA || data == 0xFE) return false;

        if (_uefiMousePhase < 0 || _uefiMousePhase > 2) {
            _uefiMousePhase = 0;
            SetSafeModeInputSubphase("ps2-mouse-reset");
            SetSafeModeInputResult("MOUSE-PHASE-RESET", (ulong)data);
        }

        if (_uefiMousePhase == 0) {
            SetSafeModeInputSubphase("ps2-mouse-byte");
            RecordSafeModeMousePacketByte(0, data);
            if ((data & 0x08) == 0) {
                SetSafeModeInputResult("MOUSE-BAD-SYNC", (ulong)data);
                return false;
            }
            if ((data & 0xC0) != 0) {
                SetSafeModeInputResult("MOUSE-OVERFLOW", (ulong)data);
                return false;
            }
            _uefiMouseB0 = data;
            _uefiMousePhase = 1;
            return false;
        }
        if (_uefiMousePhase == 1) {
            SetSafeModeInputSubphase("ps2-mouse-byte");
            RecordSafeModeMousePacketByte(1, data);
            _uefiMouseB1 = data;
            _uefiMousePhase = 2;
            return false;
        }

        SetSafeModeInputSubphase("ps2-mouse-packet");
        RecordSafeModeMousePacketByte(2, data);
        byte b2 = data;
        _uefiMousePhase = 0;

        if ((_uefiMouseB0 & 0x08) == 0) {
            SetSafeModeInputResult("MOUSE-PACKET-DESYNC", (ulong)_uefiMouseB0);
            return false;
        }

        bool xOverflow = (_uefiMouseB0 & 0x40) != 0;
        bool yOverflow = (_uefiMouseB0 & 0x80) != 0;
        if (xOverflow || yOverflow) {
            SetSafeModeInputResult("MOUSE-PACKET-OVERFLOW", (ulong)_uefiMouseB0);
            return false;
        }

        int dx = _uefiMouseB1;
        int dy = b2;
        if ((_uefiMouseB0 & 0x10) != 0) dx -= 256;
        if ((_uefiMouseB0 & 0x20) != 0) dy -= 256;

        dx = Math.Clamp(dx, -32, 32);
        dy = Math.Clamp(dy, -32, 32);

        int maxX = Framebuffer.Width > 0 ? Framebuffer.Width - 1 : 0;
        int maxY = Framebuffer.Height > 0 ? Framebuffer.Height - 1 : 0;
        SetSafeModeInputSubphase("ps2-mouse-apply");
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
        return true;
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

    private static bool TryGetUefiFramebufferInfo(out uint* fb, out int fbW, out int fbH, out int pitchPixels, out ulong maxPixels) {
        Framebuffer.RecoverUefiState();
        fb = GetUefiFramebuffer();
        fbW = Framebuffer.Width;
        fbH = Framebuffer.Height;
        pitchPixels = fbW;
        maxPixels = 0;

        if (fb == null || fbW <= 0 || fbH <= 0) return false;

        UefiBootInfo* bootInfo = Framebuffer.OriginalBootInfo;
        if (bootInfo != null) {
            if (bootInfo->FramebufferBase == 0 || bootInfo->FramebufferWidth == 0 || bootInfo->FramebufferHeight == 0) return false;
            if (bootInfo->FramebufferPitch == 0 || (bootInfo->FramebufferPitch & 3u) != 0) return false;

            fbW = (int)bootInfo->FramebufferWidth;
            fbH = (int)bootInfo->FramebufferHeight;
            pitchPixels = (int)(bootInfo->FramebufferPitch / 4u);
            if (fbW <= 0 || fbH <= 0 || pitchPixels <= 0 || pitchPixels < fbW) return false;

            ulong requiredBytes = (ulong)(uint)pitchPixels * (ulong)(uint)fbH * 4UL;
            if (requiredBytes == 0) return false;
            if (bootInfo->FramebufferSize != 0) {
                if (requiredBytes > bootInfo->FramebufferSize) return false;
                maxPixels = bootInfo->FramebufferSize / 4UL;
            } else {
                maxPixels = (ulong)(uint)pitchPixels * (ulong)(uint)fbH;
            }

            return true;
        }

        if (pitchPixels < fbW) return false;
        maxPixels = (ulong)(uint)pitchPixels * (ulong)(uint)fbH;
        return maxPixels != 0;
    }

    private static bool TryGetClippedRect(int fbW, int fbH, int x, int y, int w, int h, int clipX, int clipY, int clipW, int clipH, out int x0, out int y0, out int x1, out int y1) {
        x0 = 0;
        y0 = 0;
        x1 = 0;
        y1 = 0;
        if (fbW <= 0 || fbH <= 0 || w <= 0 || h <= 0 || clipW <= 0 || clipH <= 0) return false;

        long left = x;
        long top = y;
        long right = left + w;
        long bottom = top + h;

        long clipLeft = clipX;
        long clipTop = clipY;
        long clipRight = clipLeft + clipW;
        long clipBottom = clipTop + clipH;

        if (clipLeft < 0) clipLeft = 0;
        if (clipTop < 0) clipTop = 0;
        if (clipRight > fbW) clipRight = fbW;
        if (clipBottom > fbH) clipBottom = fbH;
        if (clipRight <= clipLeft || clipBottom <= clipTop) return false;

        if (left < clipLeft) left = clipLeft;
        if (top < clipTop) top = clipTop;
        if (right > clipRight) right = clipRight;
        if (bottom > clipBottom) bottom = clipBottom;
        if (left >= right || top >= bottom) return false;

        x0 = (int)left;
        y0 = (int)top;
        x1 = (int)right;
        y1 = (int)bottom;
        return true;
    }

    private static bool TryWriteUefiPixel(uint* fb, int fbW, int fbH, int pitchPixels, ulong maxPixels, int x, int y, uint color) {
        if (fb == null || fbW <= 0 || fbH <= 0 || pitchPixels <= 0) return false;
        if ((uint)x >= (uint)fbW || (uint)y >= (uint)fbH) return false;

        ulong offset = ((ulong)(uint)y * (ulong)(uint)pitchPixels) + (ulong)(uint)x;
        if (maxPixels != 0 && offset >= maxPixels) return false;
        if (offset > int.MaxValue) return false;

        fb[(int)offset] = color;
        return true;
    }

    private static void DrawUefiCursor() {
        if (!TryGetUefiFramebufferInfo(out uint* fb, out int fbW, out int fbH, out int pitchPixels, out ulong maxPixels)) return;

        if (_uefiCursorEverDrawn && IsUefiCursorAreaRestorable(_uefiCursorLastX, _uefiCursorLastY, fbW, fbH)) {
            RestoreUefiCursorArea(fb, fbW, fbH, pitchPixels, maxPixels, _uefiCursorLastX, _uefiCursorLastY);
        }

        int x = Math.Clamp(Control.MousePosition.X, 0, fbW - 1);
        int y = Math.Clamp(Control.MousePosition.Y, 0, fbH - 1);
        bool pressed = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;
        uint fill = pressed ? 0xFFFFD166u : 0xFFFFFFFFu;
        uint edge = 0xFF000000u;

        for (int yy = 0; yy < SafeModeCursorDrawHeight; yy++) {
            for (int xx = 0; xx < SafeModeCursorDrawWidth; xx++) {
                bool outline = xx == 0 || yy == 0 || xx == yy || (yy > 8 && xx == 5) || (yy > 8 && yy < 15 && xx == 6);
                bool inside = xx < yy && xx < 9;
                if (!outline && !inside) continue;
                int px = x + xx;
                int py = y + yy;
                TryWriteUefiPixel(fb, fbW, fbH, pitchPixels, maxPixels, px, py, outline ? edge : fill);
            }
        }

        _uefiCursorLastX = x;
        _uefiCursorLastY = y;
        _uefiCursorEverDrawn = true;
    }

    private static void DrawUefiCursorPlaceholder() {
        var graphics = Framebuffer.Graphics;
        if (graphics == null || graphics.Width <= 0 || graphics.Height <= 0) return;

        int cursorX = Math.Clamp(Control.MousePosition.X, 0, Math.Max(0, graphics.Width - 8));
        int cursorY = Math.Clamp(Control.MousePosition.Y, 0, Math.Max(0, graphics.Height - 12));

        graphics.FillRectangle(cursorX, cursorY, 8, 8, 0xFFFFFFFFu);
        graphics.FillRectangle(cursorX, cursorY, 2, 12, 0xFF000000u);
    }

    private static bool IsUefiCursorAreaRestorable(int x, int y, int fbW, int fbH) {
        if (fbW <= 0 || fbH <= 0) return false;
        if (x <= -SafeModeCursorRestoreWidth || y <= -SafeModeCursorRestoreHeight) return false;
        if (x >= fbW || y >= fbH) return false;
        return true;
    }

    private static void RestoreUefiCursorArea(uint* fb, int fbW, int fbH, int pitchPixels, ulong maxPixels, int x, int y) {
        if (!TryGetClippedRect(fbW, fbH, x, y, SafeModeCursorRestoreWidth, SafeModeCursorRestoreHeight, 0, 0, fbW, fbH, out int startX, out int startY, out int endX, out int endY)) return;

        for (int yy = startY; yy < endY; yy++) {
            int shade = (yy * 64) / fbH;
            uint color = (uint)(0xFF102030 + (shade << 16) + (shade << 8));
            if (yy < 58) color = 0xFF182836u;
            if (yy >= fbH - 44) color = 0xFF151A22u;
            if ((yy >= 58 && yy < 60) || (yy >= fbH - 46 && yy < fbH - 44)) color = 0xFF36C2B4u;

            for (int xx = startX; xx < endX; xx++) {
                TryWriteUefiPixel(fb, fbW, fbH, pitchPixels, maxPixels, xx, yy, color);
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
    private static void DrawUefiLauncher(uint* fb, int fbW, int fbH, int pitchPixels, ulong maxPixels, int x, int y, uint color, string label) {
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, x, y, 58, 58, 0xFF203040u);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, x + 8, y + 8, 42, 42, color);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, x + 14, y + 18, 30, 4, 0xCCFFFFFFu);
        DrawUefiFillRect(fb, fbW, fbH, pitchPixels, maxPixels, x + 14, y + 30, 30, 4, 0xCCFFFFFFu);
        DrawUefiTinyText(fb, fbW, fbH, pitchPixels, maxPixels, x, y + 66, label, 2, 0xFFFFFFFFu);
    }

    private static void DrawUefiTinyText(uint* fb, int fbW, int fbH, int pitchPixels, ulong maxPixels, int x, int y, string text, int scale, uint color) {
        DrawUefiTinyTextClipped(fb, fbW, fbH, pitchPixels, maxPixels, x, y, text, scale, color, 0, 0, fbW, fbH);
    }

    private static void DrawUefiTinyTextClipped(uint* fb, int fbW, int fbH, int pitchPixels, ulong maxPixels, int x, int y, string text, int scale, uint color, int clipX, int clipY, int clipW, int clipH) {
        if (fb == null || text == null || scale <= 0 || clipW <= 0 || clipH <= 0) return;

        int glyphWidth = 5 * scale;
        int glyphHeight = 7 * scale;
        int advance = 6 * scale;
        int spaceAdvance = 4 * scale;
        long clipRight = (long)clipX + clipW;
        long clipBottom = (long)clipY + clipH;

        if (y < clipY || y >= clipBottom) return;
        if ((long)y + glyphHeight > clipBottom) return;

        int cursorX = x;
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (c == ' ') {
                if ((long)cursorX + spaceAdvance > clipRight) break;
                cursorX += spaceAdvance;
                continue;
            }

            if ((long)cursorX + glyphWidth > clipRight) break;
            DrawUefiGlyph(fb, fbW, fbH, pitchPixels, maxPixels, cursorX, y, c, scale, color, clipX, clipY, clipW, clipH);
            cursorX += advance;
        }
    }

    private static void DrawUefiGlyph(uint* fb, int fbW, int fbH, int pitchPixels, ulong maxPixels, int x, int y, char c, int scale, uint color, int clipX, int clipY, int clipW, int clipH) {
        for (int row = 0; row < 7; row++) {
            byte bits = UefiGlyphRow(c, row);
            for (int col = 0; col < 5; col++) {
                if ((bits & (1 << (4 - col))) == 0) continue;
                if (TryGetClippedRect(fbW, fbH, x + (col * scale), y + (row * scale), scale, scale, clipX, clipY, clipW, clipH, out int gx0, out int gy0, out int gx1, out int gy1)) {
                    for (int yy = gy0; yy < gy1; yy++) {
                        for (int xx = gx0; xx < gx1; xx++) {
                            TryWriteUefiPixel(fb, fbW, fbH, pitchPixels, maxPixels, xx, yy, color);
                        }
                    }
                }
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
        SerialFrameBackgroundBreadcrumb(frameCounter, "HEARTBEAT_ENTER");
        SerialFrameBackgroundBreadcrumb(frameCounter, "VALIDATE_ENTER");
        if (!TryGetUefiFramebufferInfo(out uint* fb, out int fbW, out int fbH, out int pitchPixels, out ulong maxPixels)) {
            SerialFrameBackgroundReason(frameCounter, "VALIDATE_FAIL");
            SerialFrameBackgroundBreadcrumb(frameCounter, "HEARTBEAT_EXIT");
            return;
        }
        SerialFrameBackgroundBreadcrumb(frameCounter, "VALIDATE_EXIT");

        if (Framebuffer.Graphics == null) {
            SerialFrameBackgroundReason(frameCounter, "GRAPHICS_NULL_DIRECT_FB_OK");
        }
        if (maxXOrYTooSmall(fbW, fbH)) {
            SerialFrameBackgroundReason(frameCounter, "SMALL_FB_REGION");
        }

        SerialFrameBackgroundBreadcrumb(frameCounter, "CLEAR_ENTER");
        SerialFrameBackgroundBreadcrumb(frameCounter, "CLEAR_EXIT");
        SerialFrameBackgroundBreadcrumb(frameCounter, "HEARTBEAT_BOUNDS_ENTER");
        uint color = ((frameCounter / 30) & 1) == 0 ? 0xFFFFFFFFu : 0xFFFF3040u;
        int maxY = fbH < 24 ? fbH : 24;
        int maxX = fbW < 24 ? fbW : 24;
        SerialFrameBackgroundBreadcrumb(frameCounter, "HEARTBEAT_BOUNDS_EXIT");

        SerialFrameBackgroundBreadcrumb(frameCounter, "FILL_ENTER");
        for (int y = 8; y < maxY; y++) {
            if (ShouldLogBackgroundRow(y - 8, (maxY - 1) - 8)) {
                int heartbeatRow = y - 8;
                SerialFrameBackgroundBreadcrumb(frameCounter, heartbeatRow == ((maxY - 1) - 8) ? "ROW_LAST" : ("ROW_" + heartbeatRow.ToString()));
            }
            for (int x = 8; x < maxX; x++) {
                TryWriteUefiPixel(fb, fbW, fbH, pitchPixels, maxPixels, x, y, color);
            }
        }
        SerialFrameBackgroundBreadcrumb(frameCounter, "FILL_EXIT");
        SerialFrameBackgroundBreadcrumb(frameCounter, "DONE");
        SerialFrameBackgroundBreadcrumb(frameCounter, "HEARTBEAT_EXIT");
    }

    private static bool maxXOrYTooSmall(int fbW, int fbH) {
        return fbW <= 8 || fbH <= 8;
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
