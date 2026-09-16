using guideXOS.DefaultApps;
using guideXOS.FS;
using guideXOS.Misc;
using guideXOS.Kernel.Drivers;
using guideXOS.OS;
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

        // The normal guideXOS text system is an IFont bitmap atlas. UEFI
        // selects the same atlas and object model; only the byte-to-Image
        // decode path changes to the post-EBS-safe PngLoader.
        internal const string FontResourcePath = "Fonts/roboto/roboto_12pt_regular.png";
        internal const string FontCharset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
        internal static bool RealFontEnabled { get; private set; }
        internal static bool FontUsingFallback { get; private set; }
        internal static int FontResourceBytes { get; private set; }
        internal static int FontResourceWidth { get; private set; }
        internal static int FontResourceHeight { get; private set; }
        internal static string FontFailureReason { get; private set; }
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
        private static ulong _cpuEpochTick;
        /// <summary>
        /// Initialize
        /// </summary>
        public static void Initialize() {
            Windows = new List<Window>();
            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                CloseButton = new Image(16, 16);
                MinimizeButton = new Image(16, 16);
                MaximizeButton = new Image(16, 16);
            } else {
                // Load window button images from ramdisk (Legacy mode).
                try { CloseButton = new PNG(File.ReadAllBytes("Images/Close.png")); } catch { CloseButton = new Image(16, 16); }
                try { MinimizeButton = new PNG(File.ReadAllBytes("Images/BlueVelvet/16/down.png")); } catch { MinimizeButton = new Image(16, 16); }
                try { MaximizeButton = new PNG(File.ReadAllBytes("Images/BlueVelvet/16/image.png")); } catch { MaximizeButton = new Image(16, 16); }
            }

            InitializeFont();
            MouseHandled = false;
            _pending = new List<PendingWindow>();
            _cpuEpochTick = 0;
        }

        private static Image LoadUefiFontImage(byte[] data, out string failure) {
            failure = null;
            if (data == null || data.Length == 0) {
                failure = "asset-unavailable";
                return null;
            }
            if (!PngLoader.Initialize()) {
                failure = "png-runtime-init";
                return null;
            }

            int width, height;
            if (!PngLoader.ValidateSignatureAndParseIHDR(data, out width, out height)) {
                failure = "png-header";
                return null;
            }
            Image result;
            if (!PngLoader.Load(data, out result) || result == null ||
                result.RawData == null || result.Width <= 0 || result.Height <= 0) {
                if (result != null) result.Dispose();
                failure = "png-decode";
                return null;
            }
            if (result.Width != width || result.Height != height) {
                result.Dispose();
                failure = "png-dimensions";
                return null;
            }
            return result;
        }

        private static Image CreateFallbackFont() {
            // This remains a real failure fallback only. It is deliberately
            // kept separate from the successful UEFI path so white blocks can
            // never be selected merely because BootMode is UEFI.
            Image simpleFontImg = new Image(260, 160);
            if (simpleFontImg != null && simpleFontImg.RawData != null) {
                for (int y = 0; y < simpleFontImg.Height; y++) {
                    for (int x = 0; x < simpleFontImg.Width; x++) {
                        simpleFontImg.RawData[y * simpleFontImg.Width + x] =
                            unchecked((int)0xFFFFFFFF);
                    }
                }
            }
            return simpleFontImg;
        }

        private static void InitializeFont() {
            RealFontEnabled = false;
            FontUsingFallback = true;
            FontResourceBytes = 0;
            FontResourceWidth = 0;
            FontResourceHeight = 0;
            FontFailureReason = null;

            Image fontImage = null;
            byte[] data = null;
            string failure = null;
            try {
                if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                    if (File.Instance == null) {
                        failure = "filesystem-unavailable";
                    } else {
                        data = File.Instance.ReadAllBytes(FontResourcePath);
                        fontImage = LoadUefiFontImage(data, out failure);
                    }
                } else {
                    data = File.ReadAllBytes(FontResourcePath);
                    // Preserve the established legacy native PNG compatibility
                    // path while using the same IFont atlas object afterward.
                    fontImage = new PNG(data);
                    if (fontImage == null || fontImage.RawData == null ||
                        fontImage.Width <= 0 || fontImage.Height <= 0) {
                        if (fontImage != null) fontImage.Dispose();
                        fontImage = null;
                        failure = "png-decode";
                    }
                }

                if (fontImage != null) {
                    IFont candidate = new IFont(fontImage, FontCharset, 20,
                                                true, 15, -5);
                    if (candidate.IsValid && candidate.HasGlyph('A') &&
                        candidate.HasGlyph('g') && candidate.HasGlyph('0')) {
                        font = candidate;
                        RealFontEnabled = true;
                        FontUsingFallback = false;
                        FontResourceBytes = data == null ? 0 : data.Length;
                        FontResourceWidth = fontImage.Width;
                        FontResourceHeight = fontImage.Height;
                        fontImage = null; // ownership transferred to IFont
                        BootConsole.WriteLine("[FONT] initialized");
                        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                            BootConsole.WriteLine("[DESKTOP] real text enabled");
                        }
                    } else {
                        failure = "atlas-geometry";
                    }
                }
            } catch {
                failure = failure ?? "exception";
            } finally {
                if (data != null) data.Dispose();
                if (fontImage != null) fontImage.Dispose();
            }

            if (RealFontEnabled) return;

            FontFailureReason = failure ?? "unknown";
            font = new IFont(CreateFallbackFont(), FontCharset, 20,
                             true, 11, -3);
            BootConsole.WriteLine("[FONT] fallback: " + FontFailureReason);
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
                    Desktop.LaunchDisplayOptions(pw.X, pw.Y, pw.W, pw.H);
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
            for (int i = 0; i < Windows.Count; i++) {
                var w = Windows[i];
                if (!w.Visible || w is guideXOS.DefaultApps.TaskManager) continue;

                if (!_perfTrackingEnabled) {
                    w.OnDraw();
                    continue;
                }

                Allocator.CurrentOwnerId = w.OwnerId;
                ulong t0 = Timer.Ticks;
                w.OnDraw();
                ulong t1 = Timer.Ticks;
                _ = t1 >= t0 ? t1 - t0 : 0UL;
                Allocator.CurrentOwnerId = 0;
            }

            if (_perfTrackingEnabled) UpdateCpuPercents();
        }

        /// <summary>
        /// Draw only Task Manager (always on top)
        /// </summary>
        public static void DrawTaskManager() {
            for (int i = 0; i < Windows.Count; i++) {
                var w = Windows[i];
                if (w.Visible && w is guideXOS.DefaultApps.TaskManager) {
                    w.OnDraw();
                    break;
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
        /// Taskbar presentation remains window-based, but an attached window
        /// must resolve to a live semantic instance before it can be activated.
        /// Legacy/unattached shell windows remain valid compatibility entries.
        /// </summary>
        internal static bool IsTaskbarEntryValid(Window window) {
            if (window == null || !window.ApplicationInstanceHandle.IsValid) return true;
            if (ApplicationInstanceRegistry.TryGet(
                    window.ApplicationInstanceHandle,
                    out ApplicationInstance ignored)) return true;
            ApplicationInstanceRegistry.RecordStaleOwnership();
            window.ClearApplicationInstance();
            return false;
        }
        
        /// <summary>
        /// Clean up windows that have been closed/faded out
        /// This should be called periodically (e.g., once per frame after drawing)
        /// </summary>
        public static void CleanupClosedWindows() {
            // FIXED: Remove windows that are no longer visible and dispose them properly
            for (int i = Windows.Count - 1; i >= 0; i--) {
                var w = Windows[i];
                // Docked widgets are owned and drawn by their persistent
                // WidgetContainer. They may be hidden while the container is
                // auto-hidden, but must not be disposed or removed from the
                // manager while that relationship is still live.
                if (w is DockableWidget dockedWidget &&
                    dockedWidget.DockedContainer != null) {
                    continue;
                }
                if (w is WidgetContainer) {
                    continue;
                }
                // Start is a shell-owned singleton referenced by Taskbar. It
                // is intentionally persistent while hidden; disposing and
                // removing it leaves Taskbar holding a stale window object.
                if (w is StartMenu) {
                    continue;
                }
                // Remove windows that are not visible and not animating (i.e., fully closed)
                if (!w.Visible && !w.IsMinimized && !w.IsTombstoned) {
                    // Check if window has no ongoing animation
                    // A window with _animType == None and not visible is considered disposed
                    Windows.RemoveAt(i);
                    // FIXED: Dispose the window to free its resources
                    if (w != null) {
                        string closedTitle = w.Title ?? "";
                        string closedInstance = w.ApplicationInstanceHandle.IsValid
                            ? w.ApplicationInstanceHandle.ToString() : "";
                        ApplicationInstanceRegistry.OnWindowClosed(w);
                        w.Dispose();
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                        Program.MarkUefiAppRuntime("WINDOW_CLOSED=title=" +
                            closedTitle + ";type=WINDOW" +
                            ";windows=" + Windows.Count.ToString() +
                            ";instance=" + closedInstance +
                            ";memory=" + Allocator.MemoryInUse.ToString() +
                            ";corrupt=" + Allocator.FreeFailCorruptRun.ToString() +
                            ";active=" + ApplicationInstanceRegistry.ActiveCount.ToString() +
                            ";stale=" + ApplicationInstanceRegistry.StaleOwnershipCount.ToString());
#endif
                    }
                }
            }
        }
    }
}
