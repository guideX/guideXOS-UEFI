using guideXOS.DefaultApps;
using guideXOS.FS;
using guideXOS.Misc;
using guideXOS.Kernel.Drivers;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace guideXOS.GUI {
    /// <summary>
    /// Window Manager
    /// </summary>
    internal static class WindowManager {
        /// <summary>
        /// Windows
        /// </summary>
        public static List<Window> Windows;
        /// <summary>
        /// Font
        /// </summary>
        public static IFont font;
        /// <summary>
        /// Close Button
        /// </summary>
        public static Image CloseButton;
        /// <summary>
        /// Minimize Button
        /// </summary>
        public static Image MinimizeButton;
        /// <summary>
        /// Maximize Button
        /// </summary>
        public static Image MaximizeButton;
        struct PendingWindow {
            public int Type;
            public int X,
                Y,
                W,
                H;
        }
        static List<PendingWindow> _pending;
        // Perf tracking toggled off by default (previous logic caused potential hang during early boot)
        private static bool _perfTrackingEnabled = false; // can be enabled later by TaskManager if desired
        public static bool ProbeSerialMode = false;
        //private static Dictionary<int, ulong> _drawMs; // accumulated ms per owner
        //private static Dictionary<int, int> _cpuPct;    // last computed percent
        private static ulong _cpuEpochTick;

        private static void ProbeSerialChar(char c) {
            Native.Out8(0x3F8, (byte)c);
        }

        private static void ProbeSerialBreadcrumb(string breadcrumb) {
            if (breadcrumb == null)
                return;
            for (int i = 0; i < breadcrumb.Length; i++) {
                ProbeSerialChar(breadcrumb[i]);
            }
            ProbeSerialChar('\n');
        }

        private static unsafe void ProbeSerialWriteUnsigned(ulong value) {
            char* digits = stackalloc char[21];
            int len = 0;
            do {
                digits[len++] = (char)('0' + (value % 10UL));
                value /= 10UL;
            } while (value != 0 && len < 21);
            while (len-- > 0) {
                ProbeSerialChar(digits[len]);
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        public static void Initialize() {
            Windows = new List<Window>();
            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                // UEFI: Skip PNG decoding — DEFLATE decompression hangs after
                // ExitBootServices.  Use plain placeholder images instead.
                CloseButton = new Image(16, 16);
                MinimizeButton = new Image(16, 16);
                MaximizeButton = new Image(16, 16);
                BootConsole.WriteLine("[FONT] UEFI mode - using placeholder font");
                Image simpleFontImg = new Image(260, 160);
                for (int y = 0; y < simpleFontImg.Height; y++) {
                    for (int x = 0; x < simpleFontImg.Width; x++) {
                        simpleFontImg.RawData[y * simpleFontImg.Width + x] = unchecked((int)0xFFFFFFFF);
                    }
                }
                font = new IFont(
                    simpleFontImg,
                    " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~",
                    20,
                    true,
                    11,
                    -3
                );
            } else {
            // Load window button images from ramdisk (Legacy mode)
            try { CloseButton = new PNG(File.ReadAllBytes("Images/Close.png")); } catch { CloseButton = new Image(16, 16); }
            try { MinimizeButton = new PNG(File.ReadAllBytes("Images/BlueVelvet/16/down.png")); } catch { MinimizeButton = new Image(16, 16); }
            try { MaximizeButton = new PNG(File.ReadAllBytes("Images/BlueVelvet/16/image.png")); } catch { MaximizeButton = new Image(16, 16); }
            try {
                PNG robotoBlack = new PNG(File.ReadAllBytes("Fonts/roboto/roboto_12pt_regular.png"));
                string charset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
                font = new IFont(
                    robotoBlack,
                    charset,
                    20,
                    true,
                    15,
                    -5
                );
            } catch {
                BootConsole.WriteLine("[FONT] Falling back to placeholder font");
                Image simpleFontImg = new Image(260, 160);
                for (int y = 0; y < simpleFontImg.Height; y++) {
                    for (int x = 0; x < simpleFontImg.Width; x++) {
                        simpleFontImg.RawData[y * simpleFontImg.Width + x] = unchecked((int)0xFFFFFFFF);
                    }
                }
                font = new IFont(
                    simpleFontImg,
                    " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~",
                    20,
                    true,
                    11,
                    -3
                );
            }
            }
            MouseHandled = false;
            _pending = new List<PendingWindow>();
            _cpuEpochTick = 0;
        }
        /// <summary>
        /// Enable performance tracking
        /// </summary>
        public static void EnablePerfTracking() {
            if (_perfTrackingEnabled)
                return;
            _cpuEpochTick = Timer.Ticks;
            _perfTrackingEnabled = true;
        }
        /// <summary>
        /// Disable performance tracking
        /// </summary>
        public static void DisablePerfTracking() {
            _perfTrackingEnabled = false;
            //_drawMs.Clear();
            //_cpuPct.Clear();
            _cpuEpochTick = 0;
        }
        /// <summary>
        /// Enqueue display options window creation after input phase
        /// </summary>
        public static void EnqueueDisplayOptions(int x, int y, int w, int h) {
            PendingWindow pw;
            pw.Type = 1;
            pw.X = x;
            pw.Y = y;
            pw.W = w;
            pw.H = h;
            _pending.Add(pw);
        }
        
        /// <summary>
        /// Enqueue TTF Font Demo window creation after input phase
        /// </summary>
        public static void EnqueueTTFFontDemo(int x, int y, int w, int h) {
            PendingWindow pw;
            pw.Type = 2; // TTF Demo
            pw.X = x;
            pw.Y = y;
            pw.W = w;
            pw.H = h;
            _pending.Add(pw);
        }
        
        /// <summary>
        /// Flush pending window creations
        /// </summary>
        public static void FlushPendingCreates() {
            if (_pending.Count == 0)
                return;
            for (int i = 0; i < _pending.Count; i++) {
                var pw = _pending[i];
                if (pw.Type == 1)
                    _ = new DisplayOptions(pw.X, pw.Y, pw.W, pw.H);
                else if (pw.Type == 2)
                    _ = new guideXOS.DefaultApps.TTFFontDemo(pw.X, pw.Y, pw.W, pw.H);
            }
            _pending.Clear();
        }
        /// <summary>
        /// Move to End - Ensures no duplicates in the window list
        /// </summary>
        /// <param name="window"></param>
        public static void MoveToEnd(Window window) {
            if (window == null)
                return;
            
            // Safety: Check if Windows list is initialized
            if (Windows == null) {
                Windows = new List<Window>();
            }
            
            // Remove ALL instances of this window (in case of duplicates)
            // Use a safe iteration approach to prevent index issues
            int removed = 0;
            for (int i = Windows.Count - 1; i >= 0; i--) {
                if (i < Windows.Count && Windows[i] == window) {
                    Windows.RemoveAt(i);
                    removed++;
                    // Safety: prevent infinite loop if something goes wrong
                    if (removed > 100) break;
                }
            }
            
            // Add once at the end
            Windows.Add(window);
        }
        /// <summary>
        /// Draw All
        /// </summary>
        public static void DrawAll() {
            // Basic draw (no timing unless enabled)
            for (int i = 0; i < Windows.Count; i++) {
                var w = Windows[i];
                if (!w.Visible)
                    continue;
                bool isTaskMgr = w is guideXOS.DefaultApps.TaskManager;
                if (!_perfTrackingEnabled || isTaskMgr) {
                    w.OnDraw();
                    continue;
                }
                Allocator.CurrentOwnerId = w.OwnerId;
                ulong t0 = Timer.Ticks;
                w.OnDraw();
                ulong t1 = Timer.Ticks;
                ulong dt = t1 >= t0 ? t1 - t0 : 0UL;
                //int owner = w.OwnerId; if (owner != 0) { if (_drawMs.ContainsKey(owner)) _drawMs[owner] += dt; else _drawMs.Add(owner, dt); }
                Allocator.CurrentOwnerId = 0;
            }
            if (_perfTrackingEnabled)
                UpdateCpuPercents();
        }

        /// <summary>
        /// Draw all windows except Task Manager (allows workspace switcher to be drawn on top)
        /// </summary>
        public static void DrawAllExceptTaskManager() {
            if (ProbeSerialMode) {
                ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_ENTER");
                var windows = Windows;
                ProbeSerialBreadcrumb(windows == null ? "NORM_STEP_011_DRAWALL_WINDOWS=NULL" : "NORM_STEP_011_DRAWALL_WINDOWS=OK");
                if (windows == null) {
                    ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_EXIT");
                    return;
                }

                int count = windows.Count;
                ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_COUNT");
                ProbeSerialWriteUnsigned((ulong)count);
                ProbeSerialChar('\n');
                ProbeSerialBreadcrumb(count == 0 ? "NORM_STEP_011_DRAWALL_ZERO=1" : "NORM_STEP_011_DRAWALL_ZERO=0");
                ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_LOOP_BEGIN");
                for (int i = 0; i < count; i++) {
                    ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_LOOP_CONDITION_TRUE");
                    var w = windows[i];
                    ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_WINDOW_BODY_ENTER");
                    if (!w.Visible) {
                        ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_WINDOW_BODY_SKIP_VISIBLE");
                        continue;
                    }
                    if (w is guideXOS.DefaultApps.TaskManager) {
                        ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_WINDOW_BODY_SKIP_TASKMANAGER");
                        continue;
                    }

                    ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_BEFORE_ONDRAW");
                    if (!_perfTrackingEnabled) {
                        w.OnDraw();
                        ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_AFTER_ONDRAW");
                        continue;
                    }
                    Allocator.CurrentOwnerId = w.OwnerId;
                    ulong t0 = Timer.Ticks;
                    w.OnDraw();
                    ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_AFTER_ONDRAW");
                    ulong t1 = Timer.Ticks;
                    ulong dt = t1 >= t0 ? t1 - t0 : 0UL;
                    Allocator.CurrentOwnerId = 0;
                }
                if (_perfTrackingEnabled)
                    UpdateCpuPercents();
                ProbeSerialBreadcrumb("NORM_STEP_011_DRAWALL_EXIT");
                return;
            }
            for (int i = 0; i < Windows.Count; i++) {
                var w = Windows[i];
                if (!w.Visible)
                    continue;
                // Skip Task Manager - it will be drawn later to stay on top
                if (w is guideXOS.DefaultApps.TaskManager)
                    continue;

                if (!_perfTrackingEnabled) {
                    w.OnDraw();
                    continue;
                }
                Allocator.CurrentOwnerId = w.OwnerId;
                ulong t0 = Timer.Ticks;
                w.OnDraw();
                ulong t1 = Timer.Ticks;
                ulong dt = t1 >= t0 ? t1 - t0 : 0UL;
                Allocator.CurrentOwnerId = 0;
            }
            if (_perfTrackingEnabled)
                UpdateCpuPercents();
        }

        /// <summary>
        /// Draw only Task Manager (always on top)
        /// </summary>
        public static void DrawTaskManager() {
            if (ProbeSerialMode) {
                ProbeSerialBreadcrumb("NORM_STEP_011_DRAWTASK_ENTER");
                var windows = Windows;
                ProbeSerialBreadcrumb(windows == null ? "NORM_STEP_011_DRAWTASK_WINDOWS=NULL" : "NORM_STEP_011_DRAWTASK_WINDOWS=OK");
                if (windows == null) {
                    ProbeSerialBreadcrumb("NORM_STEP_011_DRAWTASK_EXIT");
                    return;
                }

                int count = windows.Count;
                ProbeSerialBreadcrumb("NORM_STEP_011_DRAWTASK_COUNT");
                ProbeSerialWriteUnsigned((ulong)count);
                ProbeSerialChar('\n');
                ProbeSerialBreadcrumb(count == 0 ? "NORM_STEP_011_DRAWTASK_ZERO=1" : "NORM_STEP_011_DRAWTASK_ZERO=0");
                ProbeSerialBreadcrumb("NORM_STEP_011_DRAWTASK_LOOP_BEGIN");
                for (int i = 0; i < count; i++) {
                    ProbeSerialBreadcrumb("NORM_STEP_011_DRAWTASK_LOOP_CONDITION_TRUE");
                    var w = windows[i];
                    ProbeSerialBreadcrumb("NORM_STEP_011_DRAWTASK_WINDOW_BODY_ENTER");
                    if (!w.Visible) {
                        ProbeSerialBreadcrumb("NORM_STEP_011_DRAWTASK_WINDOW_BODY_SKIP_VISIBLE");
                        continue;
                    }
                    if (w is guideXOS.DefaultApps.TaskManager) {
                        ProbeSerialBreadcrumb("NORM_STEP_011_DRAWTASK_BEFORE_ONDRAW");
                        w.OnDraw();
                        ProbeSerialBreadcrumb("NORM_STEP_011_DRAWTASK_AFTER_ONDRAW");
                        break; // Only one Task Manager should exist
                    }
                }
                ProbeSerialBreadcrumb("NORM_STEP_011_DRAWTASK_EXIT");
                return;
            }
            for (int i = 0; i < Windows.Count; i++) {
                var w = Windows[i];
                if (!w.Visible)
                    continue;
                // Only draw Task Manager
                if (w is guideXOS.DefaultApps.TaskManager) {
                    w.OnDraw();
                    break; // Only one Task Manager should exist
                }
            }
        }

        /// <summary>
        /// Update CPU Percents
        /// </summary>
        private static void UpdateCpuPercents() {
            ulong now = Timer.Ticks;
            ulong elapsed = now >= _cpuEpochTick ? now - _cpuEpochTick : 0UL;
            if (elapsed < 1000UL)
                return;
            if (elapsed == 0)
                elapsed = 1;
            //for (int k = 0; k < _drawMs.Keys.Count; k++) {
            //int owner = _drawMs.Keys[k]; ulong ms = _drawMs[owner]; int pct = (int)((ms * 100UL) / elapsed); if (pct < 0) pct = 0; if (pct > 100) pct = 100; if (_cpuPct.ContainsKey(owner)) _cpuPct[owner] = pct; else _cpuPct.Add(owner, pct);
            //}
            //_drawMs.Clear(); _cpuEpochTick = now;
        }
        /// <summary>
        /// Input All
        /// </summary>
        public static void InputAll() {
            // First pass: Handle "always on top" windows like Task Manager
            // Task Manager should get input priority even if not at the end of the list
            for (int i = Windows.Count - 1; i >= 0; i--) {
                var w = Windows[i];
                if (!w.Visible)
                    continue;
                
                // Only process Task Manager in this pass
                if (!(w is guideXOS.DefaultApps.TaskManager))
                    continue;
                
                bool isUnderMouse = w.IsUnderMouse();
                
                if (_perfTrackingEnabled)
                    Allocator.CurrentOwnerId = w.OwnerId;
                    
                w.OnInput();
                
                if (_perfTrackingEnabled)
                    Allocator.CurrentOwnerId = 0;
                
                // If Task Manager is under the mouse and there's a click, consume it
                if (isUnderMouse && Control.MouseButtons != MouseButtons.None) {
                    MouseHandled = true;
                    return; // Stop all input processing - Task Manager consumed the input
                }
            }
            
            // Second pass: Process all other windows in reverse order (front to back)
            // Windows at the end of the list are on top (foreground)
            for (int i = Windows.Count - 1; i >= 0; i--) {
                var w = Windows[i];
                if (!w.Visible)
                    continue;
                
                // Skip Task Manager - already processed in first pass
                if (w is guideXOS.DefaultApps.TaskManager)
                    continue;
                
                // Check if this window is under the mouse
                bool isUnderMouse = w.IsUnderMouse();
                
                // If a window under the mouse has handled input (via MouseHandled flag),
                // stop processing input for windows behind it
                if (MouseHandled && isUnderMouse) {
                    break;
                }
                
                if (_perfTrackingEnabled)
                    Allocator.CurrentOwnerId = w.OwnerId;
                    
                w.OnInput();
                
                if (_perfTrackingEnabled)
                    Allocator.CurrentOwnerId = 0;
                
                // If this window is under the mouse and consumed the click, stop processing
                // This prevents windows behind it from receiving the click
                if (isUnderMouse && Control.MouseButtons != MouseButtons.None) {
                    MouseHandled = true;
                    break;
                }
            }
        }
        /// <summary>
        /// Has Window Moving
        /// </summary>
        public static bool HasWindowMoving = false;
        /// <summary>
        /// Mouse Handled (separate from HasWindowMoving)
        /// </summary>
        static bool _mouseHandled;
        public static bool MouseHandled {
            get => _mouseHandled;
            set => _mouseHandled = value;
        }
        /// <summary>
        /// Expose last CPU% for a window id
        /// </summary>
        public static int GetWindowCpuPct(int ownerId) {
            return 0;
        } // _perfTrackingEnabled && _cpuPct.ContainsKey(ownerId) ? _cpuPct[ownerId] : 0; }
        
        /// <summary>
        /// Get all windows that should appear in Start Menu
        /// </summary>
        public static List<Window> GetStartMenuWindows() {
            var result = new List<Window>();
            for (int i = 0; i < Windows.Count; i++) {
                var w = Windows[i];
                if (w.ShowInStartMenu) {
                    result.Add(w);
                }
            }
            return result;
        }
        
        /// <summary>
        /// Clean up windows that have been closed/faded out
        /// This should be called periodically (e.g., once per frame after drawing)
        /// </summary>
        public static void CleanupClosedWindows() {
            // FIXED: Remove windows that are no longer visible and dispose them properly
            for (int i = Windows.Count - 1; i >= 0; i--) {
                var w = Windows[i];
                // Remove windows that are not visible and not animating (i.e., fully closed)
                if (!w.Visible && !w.IsMinimized && !w.IsTombstoned) {
                    // Check if window has no ongoing animation
                    // A window with _animType == None and not visible is considered disposed
                    Windows.RemoveAt(i);
                    // FIXED: Dispose the window to free its resources
                    if (w != null) {
                        w.Dispose();
                    }
                }
            }
        }
    }
}
