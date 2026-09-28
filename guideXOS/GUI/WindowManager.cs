using guideXOS.DefaultApps;
using guideXOS.FS;
using guideXOS.Misc;
using guideXOS.Kernel.Drivers;
using guideXOS.OS;
using Internal.Runtime.CompilerServices;
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
        // Windows is read by the frame loop and mutated by input, lifecycle,
        // and cleanup paths. Keep every structural mutation and the cleanup
        // pass under one reentrant lock so reverse scans cannot race another
        // removal or a z-order move.
        private static readonly object _cleanupClosedWindowsSync = new object();
        private static bool _cleanupClosedWindowsRunning;
        private static bool _cleanupClosedWindowsPending;
        private static int _nextWindowOwnerId;
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

            lock (_cleanupClosedWindowsSync) {
                // Safety: Check if Windows list is initialized
                if (Windows == null) {
                    Windows = new List<Window>();
                }

                // Remove ALL instances of this window (in case of duplicates)
                int removed = 0;
                for (int i = Windows.Count - 1; i >= 0; i--) {
                    if (i < Windows.Count && Windows[i] == window) {
                        Windows.RemoveAt(i);
                        removed++;
                        if (removed > 100) break;
                    }
                }

                // Add once at the end. Graphical z-order remains independent
                // from semantic application lifecycle ownership.
                Windows.Add(window);
            }
        }

        /// <summary>
        /// Add a newly constructed window and give it a unique allocator owner
        /// identity. The identity must not depend on z-order or List.IndexOf.
        /// </summary>
        internal static int RegisterWindow(Window window) {
            if (window == null) return 0;
            lock (_cleanupClosedWindowsSync) {
                if (Windows == null) Windows = new List<Window>();
                unchecked {
                    _nextWindowOwnerId++;
                    if (_nextWindowOwnerId <= 0) _nextWindowOwnerId = 1;
                }
                Windows.Add(window);
                return _nextWindowOwnerId;
            }
        }

        /// <summary>
        /// Dispose and remove a window as one serialized manager operation.
        /// Task Manager uses this instead of mutating the public list directly.
        /// </summary>
        internal static bool DisposeAndRemoveWindow(Window window) {
            if (window == null) return false;
            lock (_cleanupClosedWindowsSync) {
                if (Windows == null) return false;
                int found = -1;
                for (int i = Windows.Count - 1; i >= 0; i--) {
                    if (Windows[i] == window) {
                        found = i;
                        break;
                    }
                }
                if (found < 0) return false;
                window.Dispose();
                for (int i = Windows.Count - 1; i >= 0; i--) {
                    if (Windows[i] == window) Windows.RemoveAt(i);
                }
                return true;
            }
        }

        /// <summary>
        /// Resolve, dispose, and remove the selected window under the manager
        /// lock so a stale Task Manager row cannot target a different window.
        /// </summary>
        internal static Window DisposeAndRemoveWindowAt(int index,
                Window excludedWindow) {
            lock (_cleanupClosedWindowsSync) {
                if (Windows == null || index < 0 || index >= Windows.Count)
                    return null;
                Window window = Windows[index];
                if (window == null || window == excludedWindow) return null;
                window.Dispose();
                for (int i = Windows.Count - 1; i >= 0; i--) {
                    if (Windows[i] == window) Windows.RemoveAt(i);
                }
                return window;
            }
        }

        /// <summary>
        /// Applies user focus to one Window.  Semantic application-owned
        /// Windows activate through the bounded application projection;
        /// unattached compatibility Windows retain direct z-order behavior.
        /// </summary>
        public static void FocusWindow(Window window) {
            if (window == null) return;
            if (window.IsServiceSessionWindow) {
                MoveToEnd(window);
                return;
            }
            if (!window.ApplicationInstanceHandle.IsValid) {
                MoveToEnd(window);
                return;
            }
            ApplicationLifecycleResult result;
            TaskbarApplicationEntryRegistry.TryFocusWindow(
                window.ApplicationInstanceHandle, window, out result);
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
            if (MouseHandled) return;

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
                if (!w.IsServiceSessionWindow && w.ShowInStartMenu) {
                    result.Add(w);
                }
            }
            return result;
        }

        internal static bool RegisterTransientServiceWindow(Window window,
                ApplicationInstanceHandle owner,
                ApplicationServiceRequestHandle requestHandle) {
            if (window == null || Windows == null ||
                    Windows.IndexOf(window) < 0) return false;
            // A service dialog is foreground graphical UI.  Reuse the
            // existing bounded z-order normalization so a stale/duplicate
            // list reference cannot survive into the service-session table.
            MoveToEnd(window);
            return ApplicationServiceSessionTable.RegisterTransientWindow(
                window, owner, requestHandle);
        }

        internal static bool ReleaseTransientServiceWindow(Window window) {
            lock (_cleanupClosedWindowsSync) {
                bool released = ApplicationServiceSessionTable.ReleaseTransientWindow(window);
                if (window != null && Windows != null) {
                    // Dispose is the final graphical boundary. Remove every
                    // bounded duplicate reference before cleanup can rescan.
                    for (int i = Windows.Count - 1; i >= 0; i--) {
                        if (Windows[i] == window) Windows.RemoveAt(i);
                    }
                }
                return released;
            }
        }

        /// <summary>
        /// Taskbar presentation remains window-based, but an attached window
        /// must resolve to a live semantic instance before it can be activated.
        /// Legacy/unattached shell windows remain valid compatibility entries.
        /// </summary>
        internal static bool IsTaskbarEntryValid(Window window) {
            if (window != null && window.IsServiceSessionWindow) return false;
            if (window == null || !window.ApplicationInstanceHandle.IsValid) return true;
            ApplicationInstance instance;
            if (ApplicationInstanceRegistry.TryGet(
                    window.ApplicationInstanceHandle, out instance) &&
                    instance != null &&
                    instance.LifecycleState != ApplicationInstanceLifecycleState.Terminated &&
                    instance.LifecycleState != ApplicationInstanceLifecycleState.Failed &&
                    instance.OwnsWindow(window)) return true;
            ApplicationInstanceRegistry.RecordStaleOwnership();
            window.ClearApplicationInstance();
            return false;
        }
        
        /// <summary>
        /// Clean up windows that have been closed/faded out
        /// This should be called periodically (e.g., once per frame after drawing)
        /// </summary>
        public static void CleanupClosedWindows() {
            lock (_cleanupClosedWindowsSync) {
                // Cleanup is requested by both the frame loop and diagnostic
                // lifecycle paths. Serialize those callers so two reverse
                // scans cannot remove or dispose the same list entry at once.
                // Monitor locks are reentrant, so turn a nested same-thread
                // request into a second pass after the current pass completes.
                if (_cleanupClosedWindowsRunning) {
                    _cleanupClosedWindowsPending = true;
                    return;
                }

                _cleanupClosedWindowsRunning = true;
                try {
                    do {
                        _cleanupClosedWindowsPending = false;
                        CleanupClosedWindowsCore();
                    } while (_cleanupClosedWindowsPending);
                } finally {
                    _cleanupClosedWindowsRunning = false;
                    _cleanupClosedWindowsPending = false;
                }
            }
        }

        internal static int GetWindowCountSnapshot() {
            lock (_cleanupClosedWindowsSync) {
                return Windows == null ? 0 : Windows.Count;
            }
        }

        private static void CleanupClosedWindowsCore() {
#if UEFI_DIAGNOSTIC_RING3_PHASE32
            bool diagnosticCleanupStarted = false;
#endif
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
#if UEFI_DIAGNOSTIC_RING3_PHASE32
                    string cleanupIdentity = DescribeCleanupWindow(i, w);
                    if (!diagnosticCleanupStarted) {
                        Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_ENTRY;windows=" +
                            Windows.Count.ToString());
                        diagnosticCleanupStarted = true;
                    }
                    Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_SELECTED;" +
                        cleanupIdentity);
                    Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_REMOVE_BEGIN;" +
                        cleanupIdentity);
#endif
                    // Check if window has no ongoing animation
                    // A window with _animType == None and not visible is considered disposed
                    Windows.RemoveAt(i);
#if UEFI_DIAGNOSTIC_RING3_PHASE32
                    Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_REMOVE_END;" +
                        cleanupIdentity + ";windows=" + Windows.Count.ToString());
                    Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_OWNER_DETACH_BEGIN;" +
                        cleanupIdentity);
#endif
                    // FIXED: Dispose the window to free its resources
                    if (w != null) {
                        string closedTitle = w.Title ?? "";
                        string closedInstance = w.ApplicationInstanceHandle.IsValid
                            ? w.ApplicationInstanceHandle.ToString() : "";
                        if (!w.IsServiceSessionWindow) {
                            ApplicationInstanceRegistry.OnWindowClosed(w);
                        }
#if UEFI_DIAGNOSTIC_RING3_PHASE32
                        Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_OWNER_DETACH_END;" +
                            cleanupIdentity + ";ownerAfter=" +
                            w.ApplicationInstanceHandle.Value.ToString());
                        Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_DISPOSE_BEGIN;" +
                            cleanupIdentity + ";disposed=" +
                            (w.IsDisposed ? "1" : "0"));
#endif
                        w.Dispose();
#if UEFI_DIAGNOSTIC_RING3_PHASE32
                        Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_DISPOSE_END;" +
                            cleanupIdentity + ";disposed=" +
                            (w.IsDisposed ? "1" : "0"));
#endif
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
#if UEFI_DIAGNOSTIC_RING3_PHASE32
            if (diagnosticCleanupStarted) {
                Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_TASKBAR_RECONCILE_BEGIN;entries=" +
                    TaskbarApplicationEntryRegistry.EntryCount.ToString());
            }
#endif
            TaskbarApplicationEntryRegistry.Reconcile();
#if UEFI_DIAGNOSTIC_RING3_PHASE32
            if (diagnosticCleanupStarted) {
                Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_TASKBAR_RECONCILE_END;entries=" +
                    TaskbarApplicationEntryRegistry.EntryCount.ToString());
                Program.MarkUefiRing3Phase32("WINDOW_CLEANUP_EXIT;windows=" +
                    Windows.Count.ToString());
            }
#endif
        }

#if UEFI_DIAGNOSTIC_RING3_PHASE32
        private static unsafe string DescribeCleanupWindow(int index, Window window) {
            ulong objectAddress = Unsafe.As<Window, ulong>(ref window);
            ulong methodTable = objectAddress == 0 ? 0 : *(ulong*)objectAddress;
            ulong disposeTarget = methodTable == 0 ? 0 :
                *(ulong*)(methodTable + 0x50);
            ApplicationInstanceHandle ownerHandle = window.ApplicationInstanceHandle;
            ApplicationInstance owner = null;
            bool ownerLive = ownerHandle.IsValid &&
                ApplicationInstanceRegistry.TryGet(ownerHandle, out owner) &&
                owner != null;
            string appId = ownerLive ? owner.DescriptorId : "none";
            string lifecycle = ownerLive
                ? ApplicationInstanceLifecycle.Name(owner.LifecycleState) : "unresolved";
            string title = window.Title ?? "";
            if (title.Length > 64) title = title.Substring(0, 64);
            Thread currentThread = ThreadPool.CurrentThread;
            ulong threadAddress = currentThread == null ? 0 :
                Unsafe.As<Thread, ulong>(ref currentThread);
            Ring3Process currentProcess = ThreadPool.CurrentProcess;
            ulong processHandle = currentProcess == null ? 0 :
                currentProcess.Handle.Value;
            TaskbarApplicationEntry entry = null;
            bool taskbarEntry = ownerHandle.IsValid &&
                TaskbarApplicationEntryRegistry.TryGet(ownerHandle, out entry) &&
                entry != null;

            return "index=" + index.ToString() +
                ";serialObject=" + objectAddress.ToString() +
                ";type=" + CleanupWindowType(window) +
                ";typeTable=" + methodTable.ToString() +
                ";title=" + title +
                ";ownerId=" + window.OwnerId.ToString() +
                ";owner=" + ownerHandle.Value.ToString() +
                ";generation=" + ownerHandle.Generation.ToString() +
                ";appId=" + appId +
                ";lifecycle=" + lifecycle +
                ";ownsWindow=" + (ownerLive && owner.OwnsWindow(window) ? "1" : "0") +
                ";visible=" + (window.Visible ? "1" : "0") +
                ";minimized=" + (window.IsMinimized ? "1" : "0") +
                ";tombstoned=" + (window.IsTombstoned ? "1" : "0") +
                ";disposed=" + (window.IsDisposed ? "1" : "0") +
                ";methodTable=" + methodTable.ToString() +
                ";disposeTarget=" + disposeTarget.ToString() +
                ";activeApp=" + ApplicationInstanceRegistry.ActiveApplicationHandle.Value.ToString() +
                ";taskbarEntry=" + (taskbarEntry ? "1" : "0") +
                ";taskbarActive=" + (taskbarEntry && entry.IsActive ? "1" : "0") +
                ";taskbarActiveWindow=" + (taskbarEntry && entry.ActiveWindow == window ? "1" : "0") +
                ";taskbarMostRecentWindow=" + (taskbarEntry && entry.MostRecentWindow == window ? "1" : "0") +
                ";topmost=" + (Windows != null && Windows.Count > 0 &&
                    Windows[Windows.Count - 1] == window ? "1" : "0") +
                ";thread=" + threadAddress.ToString() +
                ";process=" + processHandle.ToString() +
                ";memory=" + Allocator.MemoryInUse.ToString() +
                ";freeInvalid=" + Allocator.FreeFailInvalidPtr.ToString() +
                ";freeNoPages=" + Allocator.FreeFailNoPages.ToString() +
                ";freeCorrupt=" + Allocator.FreeFailCorruptRun.ToString();
        }

        private static string CleanupWindowType(Window window) {
            if (window is Notepad) return "Notepad";
            if (window is Calculator) return "Calculator";
            if (window is ComputerFiles) return "ComputerFiles";
            if (window is TaskManager) return "TaskManager";
            if (window is GXMScriptWindow) return "GXMScriptWindow";
            return "Window";
        }
#endif
    }
}
