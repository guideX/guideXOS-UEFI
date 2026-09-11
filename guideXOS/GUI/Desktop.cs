using guideXOS.DefaultApps;
using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using guideXOS.OS;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace guideXOS.GUI {
    /// <summary>
    /// Desktop
    /// </summary>
    internal static class Desktop {
        // Helpers for fuzzy executable resolution
        private static bool HasDot(string s) {
            for (int i = 0; i < s.Length; i++)
                if (s[i] == '.') return true;
            return false;
        }
        private static bool StartsWithStr(string s, string pref) {
            int l = pref.Length;
            if (s.Length < l) return false;
            for (int i = 0; i < l; i++)
                if (s[i] != pref[i]) return false;
            return true;
        }
        /// <summary>
        /// Try Fuzzy Exec
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="token"></param>
        /// <param name="match"></param>
        /// <param name="ambiguous"></param>
        /// <returns></returns>
        private static bool TryFuzzyExec(string dir, string token, out string match, out bool ambiguous) {
            match = null;
            ambiguous = false;
            var list = File.GetFiles(dir);
            int matches = 0;
            if (list != null) {
                for (int i = 0; i < list.Count; i++) {
                    var fi = list[i];
                    if (fi.Attribute != FileAttribute.Directory) {
                        string nm = fi.Name;
                        if (nm.Length > token.Length + 1 && StartsWithStr(nm, token) && nm[token.Length] == '.') {
                            int len = nm.Length;
                            bool gxm = len >= 4 && nm[len - 1] == 'm' && nm[len - 2] == 'x' && nm[len - 3] == 'g' && nm[len - 4] == '.';
                            bool mue = len >= 4 && nm[len - 1] == 'e' && nm[len - 2] == 'u' && nm[len - 3] == 'm' && nm[len - 4] == '.';
                            if (gxm || mue) {
                                matches++;
                                match = nm;
                            }
                        }
                    }
                    fi.Dispose();
                }
                list.Dispose();
            }
            if (matches == 1) return true;
            if (matches > 1) {
                ambiguous = true;
            }
            return false;
        }

        /// <summary>
        /// Dir
        /// </summary>
        public static string Dir;
        /// <summary>
        /// Home mode: when true, show app icons and special desktop icons. When false, show real filesystem entries for Dir.
        /// </summary>
        public static bool HomeMode;
        /// <summary>
        /// Taskbar
        /// </summary>
        public static Taskbar Taskbar;
        /// <summary>
        /// Image Viewer
        /// </summary>
        public static ImageViewer imageViewer;
        /// <summary>
        /// Message Box
        /// </summary>
        public static MessageBox msgbox;
        /// <summary>
        /// Wav Player
        /// </summary>
        public static WAVPlayer wavplayer;
        /// <summary>
        /// Apps
        /// </summary>
        public static AppCollection Apps;
        /// <summary>
        /// File Explorer window
        /// </summary>
        static ComputerFiles compFiles;
        /// <summary>
        /// Is At Root
        /// </summary>
        public static bool IsAtRoot {
            get => Desktop.Dir.Length < 1;
        }
        
        // FIXED: Cache USB Drive labels to prevent per-frame allocations
        private static string[] _usbDriveLabels = null;
        private static int _lastUSBCount = -1;

        // Desktop horizontal scrolling
        private static int _scrollX = 0;
        private static bool _scrollDragActive = false;
        private static int _scrollDragStartX = 0;
        private static int _scrollDragStartScroll = 0;
        private static bool _prevLeftDown = false;
        private const int DesktopScrollbarH = 14;
        private const int DesktopScrollArrowW = 20;

        // Icon drag-and-drop
        private static bool _iconDragging = false;
        private static int _iconDragId = -1;
        private static int _iconDragOffsetX = 0;
        private static int _iconDragOffsetY = 0;
        private static int _iconDragStartMouseX = 0;
        private static int _iconDragStartMouseY = 0;
        private static bool _iconDragStarted = false; // true once mouse moved enough to be a real drag
        private const int IconDragThreshold = 5; // pixels of movement before drag starts

        // Custom icon positions: parallel arrays keyed by icon id
        private static List<int> _customPosIds = null;
        private static List<int> _customPosX = null;
        private static List<int> _customPosY = null;
        
        /// <summary>
        /// Initialize
        /// </summary>
        public static void Initialize() {
            if (BootConsole.CurrentMode == BootMode.UEFI) {
                Apps = null;
                IndexClicked = -1;
                Taskbar = new Taskbar(40, new Image(32, 32));
                Dir = "";
                HomeMode = true;
                imageViewer = null;
                msgbox = null;
                wavplayer = null;
                LastPoint.X = -1;
                LastPoint.Y = -1;
                _dirCacheDirty = true;
                _dirCacheFor = null;
                _dirCache = null;
                compFiles = null;
                _lastUSBCount = -1;
                _usbDriveLabels = null;
                _scrollX = 0;
                _scrollDragActive = false;
                _iconDragging = false;
                _iconDragId = -1;
                _iconDragStarted = false;
                _customPosIds = null;
                _customPosX = null;
                _customPosY = null;
                return;
            }

            Apps = new AppCollection();
            IndexClicked = -1;
            Taskbar = new Taskbar(40, Icons.TaskbarIcon(32));
            Dir = "";
            HomeMode = true;
            imageViewer = new ImageViewer(400, 400);
            msgbox = new MessageBox(100, 300);
            wavplayer = new WAVPlayer(450, 200);
            imageViewer.Visible = false;
            msgbox.Visible = false;
            wavplayer.Visible = false;
            LastPoint.X = -1;
            LastPoint.Y = -1;
            _dirCacheDirty = true;
            _dirCacheFor = null;
            _dirCache = null;
            compFiles = null;
            _lastUSBCount = -1;
            _usbDriveLabels = null;
            _scrollX = 0;
            _scrollDragActive = false;
            _iconDragging = false;
            _iconDragId = -1;
            _iconDragStarted = false;
            if (_customPosIds != null) { _customPosIds.Dispose(); _customPosIds = null; }
            if (_customPosX != null) { _customPosX.Dispose(); _customPosX = null; }
            if (_customPosY != null) { _customPosY.Dispose(); _customPosY = null; }
            _customPosIds = new List<int>();
            _customPosX = new List<int>();
            _customPosY = new List<int>();
        }
        /// <summary>
        /// Get custom position for an icon by its id. Returns true if found.
        /// </summary>
        private static bool GetCustomPos(int id, out int cx, out int cy) {
            cx = 0; cy = 0;
            if (_customPosIds == null) return false;
            for (int i = 0; i < _customPosIds.Count; i++) {
                if (_customPosIds[i] == id) {
                    cx = _customPosX[i];
                    cy = _customPosY[i];
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Set custom position for an icon by its id.
        /// </summary>
        private static void SetCustomPos(int id, int cx, int cy) {
            if (_customPosIds == null) {
                _customPosIds = new List<int>();
                _customPosX = new List<int>();
                _customPosY = new List<int>();
            }
            for (int i = 0; i < _customPosIds.Count; i++) {
                if (_customPosIds[i] == id) {
                    _customPosX[i] = cx;
                    _customPosY[i] = cy;
                    // Save positions to config if not in LiveMode
                    if (!OS.SystemMode.IsLiveMode && HomeMode) {
                        OS.Configuration.SaveConfiguration();
                    }
                    return;
                }
            }
            _customPosIds.Add(id);
            _customPosX.Add(cx);
            _customPosY.Add(cy);
            // Save positions to config if not in LiveMode
            if (!OS.SystemMode.IsLiveMode && HomeMode) {
                OS.Configuration.SaveConfiguration();
            }
        }
        /// <summary>
        /// Heuristic: if a USB MSC disk is present and marked ready, show installer icon.
        /// In future, this can check actual boot device id.
        /// </summary>
        private static bool IsBootFromUSB() {
            try {
                // If an IDE/SATA system disk exists and at least one USB MSC removable is present, assume live USB boot.
                bool hasIde = guideXOS.FS.Disk.Instance is guideXOS.Kernel.Drivers.IDEDevice;
                var dev = guideXOS.Kernel.Drivers.USBStorage.GetFirst();
                if (dev != null) {
                    var disk = guideXOS.Kernel.Drivers.USBMSC.TryOpenDisk(dev);
                    if (disk != null && disk.IsReady) return true;
                }
                // Fallback: if multiple USB storage devices, also suggest installer
                if (guideXOS.Kernel.Drivers.USBStorage.Count > 0 && !hasIde) return true;
            } catch { }
            return false;
        }
        /// <summary>
        /// Bar Height
        /// </summary>
        const int BarHeight = 40;
        static List<FileInfo> _dirCache;
        static string _dirCacheFor;
        static bool _dirCacheDirty;

        static void ClearDirCache() {
            if (_dirCache != null) {
                for (int i = 0; i < _dirCache.Count; i++) {
                    _dirCache[i].Dispose();
                }
                _dirCache.Dispose();
                _dirCache = null;
            }
            _dirCacheFor = null;
        }
        /// <summary>
        /// Get Directory Entries
        /// </summary>
        /// <returns></returns>
        static List<FileInfo> GetDirectoryEntries() {
            if (_dirCache == null || _dirCacheDirty || _dirCacheFor == null || _dirCacheFor != Dir) {
                // Dispose previous cache
                ClearDirCache();
                // Refresh cache for current Dir
                _dirCache = File.GetFiles(Dir);
                _dirCacheFor = Dir;
                _dirCacheDirty = false;
            }
            return _dirCache;
        }
        /// <summary>
        /// Is Mouse Over any Visible Window
        /// </summary>
        /// <returns></returns>
        static bool IsMouseOverAnyVisibleWindow() {
            for (int d = 0; d < WindowManager.Windows.Count; d++) {
                if (WindowManager.Windows[d].Visible && WindowManager.Windows[d].IsUnderMouse())
                    return true;
            }
            return false;
        }
        /*
        private static bool ShouldHideAppOnDesktop(string name) {
            // hide these app icons only on desktop
            // compare by lowercase name for safety
            string n = name;
            // Build a simple lowercase comparable copy
            int len = n.Length;
            bool match = false;
            // quick helpers
            bool Eq(string a) {
                if (a.Length != len) return false;
                for (int i = 0; i < len; i++) {
                    char ca = n[i]; if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32);
                    char cb = a[i]; if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32);
                    if (ca != cb) return false;
                }
                return true;
            }
            if (Eq("lock") || Eq("notepad") || Eq("task manager") || Eq("start menu") || Eq("monitor") || Eq("console") || Eq("paint") || Eq("clock") || Eq("calculator")) match = true;
            return match;
        }
        */
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="DocumentIcon32"></param>
        public static int IconSize = 48; // default desktop icon size changed from 32 to 48
        /// <summary>
        /// Set Icon Size
        /// </summary>
        /// <param name="size"></param>
        public static void SetIconSize(int size) {
            if (size != 16 && size != 24 && size != 32 && size != 48 && size != 128) return;
            IconSize = size;
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="documentIcon"></param>
        /// <param name="folderIcon"></param>
        /// <param name="imageIcon"></param>
        /// <param name="audioIcon"></param>
        /// <param name="iconSize"></param>
        public static void Update(Image documentIcon, Image folderIcon, Image imageIcon, Image audioIcon, int iconSize) {
            Program.LogUefiGraphicsState("DESKTOP_UPDATE_ENTRY", Framebuffer.Graphics);
            if (BootConsole.CurrentMode == BootMode.UEFI) {
                UpdateUefiSafe(documentIcon, folderIcon, imageIcon, audioIcon, iconSize);
                return;
            }

            UpdateLegacy(documentIcon, folderIcon, imageIcon, audioIcon, iconSize);
        }

        private static unsafe void UpdateUefiSafe(Image documentIcon, Image folderIcon, Image imageIcon, Image audioIcon, int iconSize) {
            var graphics = Framebuffer.Graphics;
            Program.LogUefiGraphicsState("DESKTOP_UPDATE_UEFI", graphics);
            if (graphics == null || graphics.VideoMemory == null || graphics.Width <= 0 || graphics.Height <= 0) {
                Program.NormalDesktopFrameBreadcrumb("NORMAL_FRAME_DESKTOP_GRAPHICS_INVALID");
                return;
            }

            SetIconSize(iconSize);
            Program.NormalDesktopFrameBreadcrumb("NORMAL_FRAME_DESKTOP_ICONS_BEGIN");
            DrawUefiFallbackIcon(graphics, folderIcon, "FILES", 48, 96, 0xFF47D6C8u);
            DrawUefiFallbackIcon(graphics, documentIcon, "DOCS", 152, 96, 0xFFFFD166u);
            DrawUefiFallbackIcon(graphics, imageIcon, "IMAGES", 256, 96, 0xFFFF6B6Bu);
            DrawUefiFallbackIcon(graphics, audioIcon, "AUDIO", 360, 96, 0xFF9B8AFBu);
            Program.NormalDesktopFrameBreadcrumb("NORMAL_FRAME_DESKTOP_ICONS_END");

            // This is the real UEFI Taskbar.Draw() path. Its UEFI branch is
            // already restricted to primitive drawing, cached RTC text, and a
            // procedural start-button placeholder; it does not load PNG assets.
            Program.NormalDesktopFrameBreadcrumb("NORMAL_FRAME_TASKBAR_BEGIN");
            if (Taskbar != null) {
                Taskbar.Draw();
            } else {
                Program.NormalDesktopFrameBreadcrumb("NORMAL_FRAME_TASKBAR_NULL");
            }
            Program.NormalDesktopFrameBreadcrumb("NORMAL_FRAME_TASKBAR_END");
        }

        private static void DrawUefiFallbackIcon(guideXOS.Graph.Graphics graphics, Image icon, string label, int x, int y, uint accent) {
            const int tileSize = 64;
            graphics.FillRectangle(x, y, tileSize, tileSize, 0xFF263241u);
            graphics.DrawRectangle(x, y, tileSize, tileSize, accent, 2);
            graphics.FillRectangle(x + 14, y + 18, 36, 28, accent);
            graphics.FillRectangle(x + 20, y + 14, 20, 8, accent);
            if (icon != null) {
                graphics.DrawImage(x + 8, y + 8, icon);
            }
            if (WindowManager.font != null && label != null) {
                int labelWidth = WindowManager.font.MeasureString(label);
                WindowManager.font.DrawString(x + ((tileSize - labelWidth) / 2), y + tileSize + 8, label);
            }
        }

        private static void UpdateLegacy(Image documentIcon, Image folderIcon, Image imageIcon, Image audioIcon, int iconSize) {
            SetIconSize(iconSize);
            var docIcon = documentIcon;
            var names = GetDirectoryEntries();
            int devide = 60; // could scale later
            int fw = docIcon.Width;
            int fh = docIcon.Height;
            int screenH = Framebuffer.Graphics.Height;
            int x = devide;
            int y = devide;
            bool leftDown = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;
            bool mouseOverWindow = IsMouseOverAnyVisibleWindow();
            bool mouseBlocked = WindowManager.HasWindowMoving || WindowManager.MouseHandled || mouseOverWindow;
            bool clickable = leftDown && !mouseBlocked;
            if (leftDown && mouseOverWindow) clickable = false;

            // --- Desktop horizontal scrolling ---
            int screenW = Framebuffer.Graphics.Width;
            // Compute total content width by doing a dry-run layout
            int totalContentW = ComputeTotalContentWidth(devide, fw, fh, screenH);
            int maxScrollX = totalContentW - screenW;
            if (maxScrollX < 0) maxScrollX = 0;

            // Mouse wheel scrolling on the desktop (only when not over a window)
            if (!mouseOverWindow) {
                int wheelDelta = PS2Mouse.DeltaZ;
                if (wheelDelta != 0) {
                    int scrollAmount = -wheelDelta * (fw + devide); // scroll one column per wheel notch
                    int ns = _scrollX + scrollAmount;
                    if (ns < 0) ns = 0;
                    if (ns > maxScrollX) ns = maxScrollX;
                    _scrollX = ns;
                }
            }

            // Horizontal scrollbar interaction (above taskbar)
            int sbY = screenH - BarHeight - DesktopScrollbarH;
            int sbAreaW = screenW - DesktopScrollArrowW * 2;
            int sbArrowLeftX = 0;
            int sbArrowRightX = screenW - DesktopScrollArrowW;
            int sbTrackX = DesktopScrollArrowW;
            bool clickEdge = leftDown && !_prevLeftDown;

            if (maxScrollX > 0 && !mouseOverWindow) {
                int mx = Control.MousePosition.X;
                int my = Control.MousePosition.Y;
                // Arrow button clicks
                if (clickEdge && my >= sbY && my <= sbY + DesktopScrollbarH) {
                    // Left arrow
                    if (mx >= sbArrowLeftX && mx < sbArrowLeftX + DesktopScrollArrowW) {
                        int ns = _scrollX - (fw + devide);
                        if (ns < 0) ns = 0;
                        _scrollX = ns;
                    }
                    // Right arrow
                    if (mx >= sbArrowRightX && mx < sbArrowRightX + DesktopScrollArrowW) {
                        int ns = _scrollX + (fw + devide);
                        if (ns > maxScrollX) ns = maxScrollX;
                        _scrollX = ns;
                    }
                }
                // Scrollbar thumb drag start
                if (clickEdge && my >= sbY && my <= sbY + DesktopScrollbarH && mx >= sbTrackX && mx < sbArrowRightX) {
                    _scrollDragActive = true;
                    _scrollDragStartX = mx;
                    _scrollDragStartScroll = _scrollX;
                }
                // Scrollbar thumb drag update
                if (_scrollDragActive && leftDown) {
                    int dx = Control.MousePosition.X - _scrollDragStartX;
                    int thumbW = sbAreaW;
                    if (totalContentW > screenW) {
                        thumbW = (sbAreaW * screenW) / totalContentW;
                        if (thumbW < 20) thumbW = 20;
                        if (thumbW > sbAreaW) thumbW = sbAreaW;
                    }
                    int scrollRange = sbAreaW - thumbW;
                    int ns = scrollRange > 0 ? _scrollDragStartScroll + (dx * maxScrollX) / scrollRange : 0;
                    if (ns < 0) ns = 0;
                    if (ns > maxScrollX) ns = maxScrollX;
                    _scrollX = ns;
                }
            }
            if (!leftDown) { _scrollDragActive = false; }
            _prevLeftDown = leftDown;

            // Clamp scroll if content shrunk
            if (_scrollX > maxScrollX) _scrollX = maxScrollX;
            if (_scrollX < 0) _scrollX = 0;

            // Apply scroll offset to starting x
            x -= _scrollX;

            // --- Icon drag movement tracking ---
            if (_iconDragging && _iconDragId != -1 && leftDown) {
                int dmx = Control.MousePosition.X - _iconDragStartMouseX;
                int dmy = Control.MousePosition.Y - _iconDragStartMouseY;
                if (!_iconDragStarted) {
                    if (dmx * dmx + dmy * dmy > IconDragThreshold * IconDragThreshold) {
                        _iconDragStarted = true;
                    }
                }
            }
            // Handle drag release - save position if drag was active
            if (_iconDragging && !leftDown) {
                if (_iconDragStarted && _iconDragId != -1) {
                    // Drag completed: store new position
                    int newX = Control.MousePosition.X - _iconDragOffsetX;
                    int newY = Control.MousePosition.Y - _iconDragOffsetY;
                    SetCustomPos(_iconDragId, newX, newY);
                }
                _iconDragging = false;
                _iconDragId = -1;
                _iconDragStarted = false;
            }



            if (HomeMode) {
                if (y + fh + devide > screenH - devide) {
                    y = devide;
                    x += fw + devide;
                }
                int cfId = Apps.Length;
                int cfDx = x, cfDy = y;
                int cfCx, cfCy;
                if (_iconDragging && _iconDragId == cfId && _iconDragStarted) {
                    cfDx = Control.MousePosition.X - _iconDragOffsetX;
                    cfDy = Control.MousePosition.Y - _iconDragOffsetY;
                } else if (GetCustomPos(cfId, out cfCx, out cfCy)) {
                    cfDx = cfCx; cfDy = cfCy;
                }
                ClickEvent("Computer Files", false, cfDx, cfDy, cfId, clickable, leftDown);
                uint col98 = UI.ButtonFillColor(cfDx, cfDy, folderIcon.Width, folderIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                Framebuffer.Graphics.DrawImage(cfDx, cfDy, folderIcon);
                int textWidth = WindowManager.font.MeasureString("Computer Files");
                int textX = cfDx + (folderIcon.Width / 2) - (textWidth / 2);
                WindowManager.font.DrawString(textX, cfDy + fh + 4, "Computer Files");
                y += documentIcon.Height + devide;

                // Auto-show installer icon in LIVE mode or when booting from USB media
                if (SystemMode.IsLiveMode || IsBootFromUSB()) {
                    if (y + fh + devide > screenH - devide) { y = devide; x += fw + devide; }
                    string installLabel = "Install to Hard Drive";
                    int instId = 50001;
                    int instDx = x, instDy = y;
                    int instCx, instCy;
                    if (_iconDragging && _iconDragId == instId && _iconDragStarted) {
                        instDx = Control.MousePosition.X - _iconDragOffsetX;
                        instDy = Control.MousePosition.Y - _iconDragOffsetY;
                    } else if (GetCustomPos(instId, out instCx, out instCy)) {
                        instDx = instCx; instDy = instCy;
                    }
                    ClickEvent(installLabel, false, instDx, instDy, instId, clickable, leftDown);
                    uint colInst = UI.ButtonFillColor(instDx, instDy, folderIcon.Width, folderIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                    Framebuffer.Graphics.DrawImage(instDx, instDy, docIcon);
                    int instW = WindowManager.font.MeasureString(installLabel);
                    int instX = instDx + (docIcon.Width / 2) - (instW / 2);
                    WindowManager.font.DrawString(instX, instDy + fh + 4, installLabel);
                    y += documentIcon.Height + devide;
                }
                // USB mass storage icons, one per connected device
                if (Kernel.Drivers.USBStorage.Count > 0) {
                    int count = Kernel.Drivers.USBStorage.Count;
                    
                    // FIXED: Regenerate cached labels only if USB count changed
                    if (_lastUSBCount != count) {
                        // Dispose old labels
                        if (_usbDriveLabels != null) {
                            for (int i = 0; i < _usbDriveLabels.Length; i++) {
                                if (_usbDriveLabels[i] != null) {
                                    _usbDriveLabels[i].Dispose();
                                }
                            }
                            _usbDriveLabels.Dispose();
                        }
                        
                        // Create new labels
                        _usbDriveLabels = new string[count];
                        for (int i = 0; i < count; i++) {
                            if (count == 1) {
                                _usbDriveLabels[i] = "USB Drive";
                            } else {
                                string uNum = (i + 1).ToString();
                                string temp = "USB Drive " + uNum;
                                _usbDriveLabels[i] = temp;
                                uNum.Dispose(); // FIXED: Dispose intermediate string
                                // Note: temp becomes the cached label, so don't dispose it
                            }
                        }
                        _lastUSBCount = count;
                    }
                    
                    for (int u = 0; u < count; u++) {
                        if (y + fh + devide > screenH - devide) {
                            y = devide;
                            x += fw + devide;
                        }
                        string label = _usbDriveLabels[u];
                        int usbId = 20000 + u;
                        int usbDx = x, usbDy = y;
                        int usbCx, usbCy;
                        if (_iconDragging && _iconDragId == usbId && _iconDragStarted) {
                            usbDx = Control.MousePosition.X - _iconDragOffsetX;
                            usbDy = Control.MousePosition.Y - _iconDragOffsetY;
                        } else if (GetCustomPos(usbId, out usbCx, out usbCy)) {
                            usbDx = usbCx; usbDy = usbCy;
                        }
                        ClickEvent(label, true, usbDx, usbDy, usbId, clickable, leftDown);
                        uint col = UI.ButtonFillColor(usbDx, usbDy, folderIcon.Width, folderIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                        Framebuffer.Graphics.DrawImage(usbDx, usbDy, folderIcon);
                        int labelWidth = WindowManager.font.MeasureString(label);
                        int labelX = usbDx + (folderIcon.Width / 2) - (labelWidth / 2);
                        WindowManager.font.DrawString(labelX, usbDy + fh + 4, label);
                        y += documentIcon.Height + devide;
                    }
                }
                if (y + fh + devide > screenH - devide) {
                    y = devide;
                    x += fw + devide;
                }
                int rootId = Apps.Length + 1;
                int rootDx = x, rootDy = y;
                int rootCx, rootCy;
                if (_iconDragging && _iconDragId == rootId && _iconDragStarted) {
                    rootDx = Control.MousePosition.X - _iconDragOffsetX;
                    rootDy = Control.MousePosition.Y - _iconDragOffsetY;
                } else if (GetCustomPos(rootId, out rootCx, out rootCy)) {
                    rootDx = rootCx; rootDy = rootCy;
                }
                ClickEvent("Root", true, rootDx, rootDy, rootId, clickable, leftDown);
                uint colRoot = UI.ButtonFillColor(rootDx, rootDy, folderIcon.Width, folderIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                Framebuffer.Graphics.DrawImage(rootDx, rootDy, folderIcon);
                int rootTextWidth = WindowManager.font.MeasureString("Root");
                int rootTextX = rootDx + (folderIcon.Width / 2) - (rootTextWidth / 2);
                WindowManager.font.DrawString(rootTextX, rootDy + fh + 4, "Root");
                y += documentIcon.Height + devide;
            }
            if (!HomeMode) {
                if (y + fh + devide > screenH - devide) {
                    y = devide;
                    x += fw + devide;
                }
                int deskId = -100;
                int deskDx = x, deskDy = y;
                int deskCx, deskCy;
                if (_iconDragging && _iconDragId == deskId && _iconDragStarted) {
                    deskDx = Control.MousePosition.X - _iconDragOffsetX;
                    deskDy = Control.MousePosition.Y - _iconDragOffsetY;
                } else if (GetCustomPos(deskId, out deskCx, out deskCy)) {
                    deskDx = deskCx; deskDy = deskCy;
                }
                ClickEvent("Desktop", false, deskDx, deskDy, deskId, clickable, leftDown);
                uint cDesk = UI.ButtonFillColor(deskDx, deskDy, folderIcon.Width, folderIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                Framebuffer.Graphics.DrawImage(deskDx, deskDy, folderIcon);
                int deskTextWidth = WindowManager.font.MeasureString("Desktop");
                int deskTextX = deskDx + (folderIcon.Width / 2) - (deskTextWidth / 2);
                WindowManager.font.DrawString(deskTextX, deskDy + fh + 4, "Desktop");
                y += fh + devide;
                for (int i = 0; i < names.Count; i++) {
                    if (y + fh + devide > screenH - devide) {
                        y = devide;
                        x += fw + devide;
                    }
                    string n = names[i].Name;
                    bool isDir = names[i].Attribute == FileAttribute.Directory;
                    int fileId = i + 1000;
                    int fileDx = x, fileDy = y;
                    int fileCx, fileCy;
                    if (_iconDragging && _iconDragId == fileId && _iconDragStarted) {
                        fileDx = Control.MousePosition.X - _iconDragOffsetX;
                        fileDy = Control.MousePosition.Y - _iconDragOffsetY;
                    } else if (GetCustomPos(fileId, out fileCx, out fileCy)) {
                        fileDx = fileCx; fileDy = fileCy;
                    }
                    ClickEvent(n, isDir, fileDx, fileDy, fileId, clickable, leftDown);
                    uint bg = UI.ButtonFillColor(fileDx, fileDy, documentIcon.Width, documentIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                    // Draw appropriate icon based on file type
                    if (n.EndsWith(".png") || n.EndsWith(".bmp")) {
                        Framebuffer.Graphics.DrawImage(fileDx, fileDy, imageIcon);
                    } else if (n.EndsWith(".wav")) {
                        Framebuffer.Graphics.DrawImage(fileDx, fileDy, audioIcon);
                    } else if (isDir) {
                        Framebuffer.Graphics.DrawImage(fileDx, fileDy, folderIcon);
                    } else {
                        Framebuffer.Graphics.DrawImage(fileDx, fileDy, docIcon);
                    }
                    // Draw text centered below icon, truncate if too long
                    string displayName = n;
                    int maxTextWidth = fw + 32;
                    int nameWidth = WindowManager.font.MeasureString(displayName);
                    if (nameWidth > maxTextWidth) {
                        int ellipsisWidth = WindowManager.font.MeasureString("...");
                        int availWidth = maxTextWidth - ellipsisWidth;
                        int charCount = 0;
                        for (int c = 0; c < displayName.Length; c++) {
                            int w = WindowManager.font.MeasureString(displayName.Substring(0, c + 1));
                            if (w > availWidth) break;
                            charCount = c + 1;
                        }
                        displayName = displayName.Substring(0, charCount) + "...";
                        nameWidth = WindowManager.font.MeasureString(displayName);
                    }
                    int nameX = fileDx + (documentIcon.Width / 2) - (nameWidth / 2);
                    WindowManager.font.DrawString(nameX, fileDy + fh + 4, displayName);
                    y += fh + devide;
                }
            }
            // Selection marquee unchanged...
            // Handle widget container auto-hide (must run even when hidden)
            if (Program.WidgetsContainer != null) {
                Program.WidgetsContainer.UpdateAutoHide();
            }
            // Draw horizontal scrollbar if content overflows
            if (maxScrollX > 0) {
                // Scrollbar track
                Framebuffer.Graphics.FillRectangle(0, sbY, screenW, DesktopScrollbarH, 0xFF1A1A1A);

                // Left arrow button
                int mxSb = Control.MousePosition.X;
                int mySb = Control.MousePosition.Y;
                bool hoverLeft = (mxSb >= sbArrowLeftX && mxSb < sbArrowLeftX + DesktopScrollArrowW && mySb >= sbY && mySb <= sbY + DesktopScrollbarH);
                Framebuffer.Graphics.FillRectangle(sbArrowLeftX, sbY, DesktopScrollArrowW, DesktopScrollbarH, hoverLeft ? 0xFF3A3A3A : 0xFF2A2A2A);
                Framebuffer.Graphics.DrawRectangle(sbArrowLeftX, sbY, DesktopScrollArrowW, DesktopScrollbarH, 0xFF3F3F3F, 1);
                // Left triangle
                int lcx = sbArrowLeftX + DesktopScrollArrowW / 2;
                int lcy = sbY + DesktopScrollbarH / 2;
                for (int r = 0; r < 4; r++) {
                    int ty = lcy - r; int by = lcy + r; int lx = lcx - 2 + r;
                    if (lx >= sbArrowLeftX && lx < sbArrowLeftX + DesktopScrollArrowW) {
                        Framebuffer.Graphics.FillRectangle(lx, ty, 1, by - ty + 1, 0xFFCCCCCC);
                    }
                }

                // Right arrow button
                bool hoverRight = (mxSb >= sbArrowRightX && mxSb < sbArrowRightX + DesktopScrollArrowW && mySb >= sbY && mySb <= sbY + DesktopScrollbarH);
                Framebuffer.Graphics.FillRectangle(sbArrowRightX, sbY, DesktopScrollArrowW, DesktopScrollbarH, hoverRight ? 0xFF3A3A3A : 0xFF2A2A2A);
                Framebuffer.Graphics.DrawRectangle(sbArrowRightX, sbY, DesktopScrollArrowW, DesktopScrollbarH, 0xFF3F3F3F, 1);
                // Right triangle
                int rcx = sbArrowRightX + DesktopScrollArrowW / 2;
                int rcy = sbY + DesktopScrollbarH / 2;
                for (int r = 0; r < 4; r++) {
                    int ty = rcy - r; int by = rcy + r; int rx = rcx + 2 - r;
                    if (rx >= sbArrowRightX && rx < sbArrowRightX + DesktopScrollArrowW) {
                        Framebuffer.Graphics.FillRectangle(rx, ty, 1, by - ty + 1, 0xFFCCCCCC);
                    }
                }

                // Thumb
                int thumbW = sbAreaW;
                if (totalContentW > screenW) {
                    thumbW = (sbAreaW * screenW) / totalContentW;
                    if (thumbW < 20) thumbW = 20;
                    if (thumbW > sbAreaW) thumbW = sbAreaW;
                }
                int thumbX = maxScrollX > 0 ? sbTrackX + ((sbAreaW - thumbW) * _scrollX) / maxScrollX : sbTrackX;
                bool hoverThumb = (mxSb >= thumbX && mxSb <= thumbX + thumbW && mySb >= sbY && mySb <= sbY + DesktopScrollbarH);
                Framebuffer.Graphics.FillRectangle(thumbX, sbY + 2, thumbW, DesktopScrollbarH - 4, hoverThumb ? 0xFF4F4F4F : 0xFF2F2F2F);
            }
            // Draw taskbar and handle Start Menu interactions
            Taskbar.Draw();
        // Right-click pinning for desktop items (apps/files)
            if ((Control.MouseButtons & MouseButtons.Right) == MouseButtons.Right) {
                int mx = Control.MousePosition.X;
                int my = Control.MousePosition.Y;
                // Only handle right-click pinning if not over a window (prevent crashes)
                if (!IsMouseOverAnyVisibleWindow()) {
                    int scanX = devide - _scrollX;
                    int scanY = devide;
                    int iconW = docIcon.Width;
                    int iconH = docIcon.Height;
                    if (HomeMode) {
                        // Computer Files first icon
                        if (mx >= scanX && mx <= scanX + iconW && my >= scanY && my <= scanY + iconH) {
                            PinnedManager.PinComputerFiles();
                        }
                    } else {
                        // Skip the "Desktop" entry at the top (it's drawn first in non-HomeMode)
                        scanY += fh + devide;
                        // iterate file entries in current directory
                        for (int i = 0; i < names.Count; i++) {
                            if (scanY + fh + devide > screenH - devide) {
                                scanY = devide;
                                scanX += fw + devide;
                            }
                            if (mx >= scanX && mx <= scanX + iconW && my >= scanY && my <= scanY + iconH) {
                                string fname = names[i].Name;
                                bool dir = names[i].Attribute == FileAttribute.Directory;
                                if (!dir) {
                                    PinnedManager.PinFile(fname, Dir + fname, docIcon);
                                }
                                break;
                            }
                            scanY += fh + devide;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Last Point
        /// </summary>
        public static Point LastPoint;
        /// <summary>
        /// Click Event
        /// </summary>
        /// <param name="name"></param>
        /// <param name="isDirectory"></param>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="i"></param>
        /// <param name="clickable"></param>
        /// <param name="leftDown"></param>
        private static bool _mouseClickLatch = false; // added latch to prevent repeated OnClick while mouse held
        private static void ClickEvent(string name, bool isDirectory, int X, int Y, int i, bool clickable, bool leftDown) {
            int iconW = Icons.DocumentIcon(IconSize).Width;
            int iconH = Icons.DocumentIcon(IconSize).Height;
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool overIcon = mx > X && mx < X + iconW && my > Y && my < Y + iconH;
            if (leftDown) {
                if (!WindowManager.HasWindowMoving && clickable && !ClickLock && !_mouseClickLatch && overIcon) {
                    if (!_iconDragging && _iconDragId == -1) {
                        // Begin potential drag
                        _iconDragId = i;
                        _iconDragOffsetX = mx - X;
                        _iconDragOffsetY = my - Y;
                        _iconDragStartMouseX = mx;
                        _iconDragStartMouseY = my;
                        _iconDragStarted = false;
                        _iconDragging = true;
                        IndexClicked = i;
                        _mouseClickLatch = true;
                    }
                }
            } else {
                // Mouse released - drag end is handled in Update(), here we just handle click
                if (_iconDragId == i && !_iconDragStarted) {
                    // No real drag happened: treat as click
                    OnClick(name, isDirectory, X, Y);
                }
                ClickLock = false;
                _mouseClickLatch = false;
            }
            // Draw selection highlight (skip for icon being dragged, it draws separately)
            if (IndexClicked == i && !(_iconDragging && _iconDragId == i && _iconDragStarted)) {
                int w = (int)(iconW * 1.5f);
                Framebuffer.Graphics.AFillRectangle(X + ((iconW / 2) - (w / 2)), Y, w, iconH * 2, 0x7F2E86C1);
            }
        }
        static bool ClickLock = false;
        static int IndexClicked;
        /// <summary>
        /// On Click
        /// </summary>
        /// <param name="name"></param>
        /// <param name="isDirectory"></param>
        /// <param name="itemX"></param>
        /// <param name="itemY"></param>
        public static void OnClick(string name, bool isDirectory, int itemX, int itemY) {
            ClickLock = true;
            // Special desktop controls
            if (name == "Root" && HomeMode) {
                HomeMode = false;
                _dirCacheDirty = true;
                IndexClicked = -1;
                _scrollX = 0;
                if (_customPosIds != null) _customPosIds.Clear();
                if (_customPosX != null) _customPosX.Clear();
                if (_customPosY != null) _customPosY.Clear();
                return;
            }
            // Launch HD installer from desktop icon
            if (name == "Install to Hard Drive" && HomeMode) {
                var installer = new guideXOS.DefaultApps.HDInstaller(itemX + 60, itemY + 60);
                WindowManager.MoveToEnd(installer);
                installer.Visible = true;
                IndexClicked = -1;
                return;
            }
            if (name == "Computer Files" && HomeMode) {
                // Always create a new ComputerFiles window (old one may have been disposed on close)
                compFiles = new ComputerFiles(300, 200, 540, 380);
                WindowManager.MoveToEnd(compFiles);
                return;
            }
            // Click on USB drive icon opens Computer Files too (when on Home desktop)
            if (HomeMode) {
                string usbPrefix = "USB Drive";
                bool isUsb = name.Length >= usbPrefix.Length;
                if (isUsb) {
                    for (int pi = 0; pi < usbPrefix.Length; pi++) {
                        if (name[pi] != usbPrefix[pi]) {
                            isUsb = false;
                            break;
                        }
                    }
                }
                // FIXED: NEVER dispose string literals - they are constants!
                if (isUsb) {
                    // Always create a new ComputerFiles window (old one may have been disposed)
                    compFiles = new ComputerFiles(300, 200, 540, 380);
                    WindowManager.MoveToEnd(compFiles);
                    return;
                }
            }
            if (name == "Desktop" && !HomeMode) {
                HomeMode = true;
                if (Dir.Length != 0) {
                    Dir.Dispose();
                }
                Dir = "";
                _dirCacheDirty = true;
                IndexClicked = -1;
                _scrollX = 0;
                if (_customPosIds != null) _customPosIds.Clear();
                if (_customPosX != null) _customPosX.Clear();
                if (_customPosY != null) _customPosY.Clear();
                return;
            }

            string devider = "/";
            string path = Dir + name;
            if (isDirectory) {
                string newd = Dir + name + devider;
                Dir.Dispose();
                Dir = newd;
                _dirCacheDirty = true;
                IndexClicked = -1;
                _scrollX = 0;
                if (_customPosIds != null) _customPosIds.Clear();
                if (_customPosX != null) _customPosX.Clear();
                if (_customPosY != null) _customPosY.Clear();
            } else if (name.EndsWith(".png")) {
                byte[] buffer = File.ReadAllBytes(path);
                PNG png = new(buffer);
                buffer.Dispose();
                imageViewer.SetImage(png);
                png.Dispose();
                WindowManager.MoveToEnd(imageViewer);
                imageViewer.Visible = true;
                RecentManager.AddDocument(path, Icons.ImageIcon(32));
            } else if (name.EndsWith(".bmp")) {
                byte[] buffer = File.ReadAllBytes(path);
                Bitmap png = new(buffer);
                buffer.Dispose();
                imageViewer.SetImage(png);
                png.Dispose();
                WindowManager.MoveToEnd(imageViewer);
                imageViewer.Visible = true;
                RecentManager.AddDocument(path, Icons.ImageIcon(32));
            } else if (name.EndsWith(".txt")) {
                // Open in Notepad
                var notepad = new Notepad(itemX + 40, itemY + 40);
                notepad.OpenFile(path);
                WindowManager.MoveToEnd(notepad);
                notepad.Visible = true;
                RecentManager.AddDocument(path, Icons.DocumentIcon(32));
            } else if (name.EndsWith(".gxm") || name.EndsWith(".mue")) {
                byte[] buffer = File.ReadAllBytes(path);
                string err;
                bool ok = GXMLoader.TryExecute(buffer, out err);
                if (!ok) {
                    msgbox.X = itemX + 60;
                    msgbox.Y = itemY + 60;
                    msgbox.SetText(err ?? "Failed to run executable");
                    WindowManager.MoveToEnd(msgbox);
                    msgbox.Visible = true;
                } else {
                    RecentManager.AddDocument(path, Icons.DocumentIcon(32));
                }
            } else if (name.EndsWith(".wav")) {
                if (Audio.HasAudioDevice) {
                    wavplayer.Visible = true;
                    byte[] buffer = File.ReadAllBytes(path);
                    unsafe {
                        fixed (char* ptr = name) wavplayer.Play(buffer, new string(ptr));
                    }
                    RecentManager.AddDocument(path, Icons.AudioIcon(32));
                } else {
                    msgbox.X = itemX + 75;
                    msgbox.Y = itemY + 75;
                    msgbox.SetText("Audio controller is unavailable!");
                    WindowManager.MoveToEnd(msgbox);
                    msgbox.Visible = true;
                }
            } else if (!Apps.Load(name)) {
                msgbox.X = itemX + 75;
                msgbox.Y = itemY + 75;
                msgbox.SetText("No application can open this file!");
                WindowManager.MoveToEnd(msgbox);
                msgbox.Visible = true;
            }
            path.Dispose();
            devider.Dispose();
        }
        /// <summary>
        /// Invalidate Directory Cache
        /// </summary>
        public static void InvalidateDirCache() {
            _dirCacheDirty = true;
        }

        /// <summary>
        /// Get icon positions for saving to configuration.
        /// Returns arrays of icon IDs and their X,Y positions.
        /// </summary>
        public static void GetIconPositions(out List<int> ids, out List<int> xs, out List<int> ys) {
            ids = _customPosIds;
            xs = _customPosX;
            ys = _customPosY;
        }
        
        /// <summary>
        /// Load icon positions from configuration.
        /// </summary>
        public static void LoadIconPositions(List<int> ids, List<int> xs, List<int> ys) {
            if (ids == null || xs == null || ys == null) return;
            if (ids.Count == 0) return;
            
            // Initialize lists if needed
            if (_customPosIds == null) {
                _customPosIds = new List<int>();
                _customPosX = new List<int>();
                _customPosY = new List<int>();
            } else {
                _customPosIds.Clear();
                _customPosX.Clear();
                _customPosY.Clear();
            }
            
            // Copy positions
            for (int i = 0; i < ids.Count; i++) {
                _customPosIds.Add(ids[i]);
                _customPosX.Add(xs[i]);
                _customPosY.Add(ys[i]);
            }
        }
        
        /// <summary>
        /// Compute total content width by simulating the column layout without drawing.
        /// </summary>
        private static int ComputeTotalContentWidth(int devide, int fw, int fh, int screenH) {
            int cx = devide;
            int cy = devide;
            int maxX = cx + fw; // at least one column
            if (HomeMode) {
                // Computer Files
                if (cy + fh + devide > screenH - devide) { cy = devide; cx += fw + devide; }
                cy += fh + devide;
                // Installer icon (LIVE mode or USB boot)
                if (SystemMode.IsLiveMode || IsBootFromUSB()) {
                    if (cy + fh + devide > screenH - devide) { cy = devide; cx += fw + devide; }
                    cy += fh + devide;
                }
                // USB drives
                if (Kernel.Drivers.USBStorage.Count > 0) {
                    for (int u = 0; u < Kernel.Drivers.USBStorage.Count; u++) {
                        if (cy + fh + devide > screenH - devide) { cy = devide; cx += fw + devide; }
                        cy += fh + devide;
                    }
                }
                // Root
                if (cy + fh + devide > screenH - devide) { cy = devide; cx += fw + devide; }
                cy += fh + devide;
                if (cx + fw > maxX) maxX = cx + fw;
            } else {
                // Desktop link
                if (cy + fh + devide > screenH - devide) { cy = devide; cx += fw + devide; }
                cy += fh + devide;
                // File entries
                var names = GetDirectoryEntries();
                for (int i = 0; i < names.Count; i++) {
                    if (cy + fh + devide > screenH - devide) { cy = devide; cx += fw + devide; }
                    cy += fh + devide;
                }
                if (cx + fw > maxX) maxX = cx + fw;
            }
            return maxX + devide; // add trailing padding
        }
    }
}
