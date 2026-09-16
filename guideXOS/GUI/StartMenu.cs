using guideXOS.Kernel.Drivers;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using guideXOS.DefaultApps;
using guideXOS.OS;
namespace guideXOS.GUI {
    /// <summary>
    /// Start Menu
    /// </summary>
    internal class StartMenu : Window {
        private static readonly int _x = 15;
        private static readonly int _y = 45;
        private static readonly int _x2 = 420; // wider to fit two columns
        private static readonly int _y2 = 680;

        private bool _powerMenuVisible = false;
        private const int Padding = 18; // increased left/right padding
        private const int Spacing = 58; // was 50

        private const int ShutdownBtnW = 100;
        private const int ShutdownBtnH = 28;
        private const int ArrowBtnW = 28;
        private const int ArrowBtnH = 28;
        private const int Gap = 6;

        private const int MenuItemH = 26;
        private const int MenuW = 120;
        private const int MenuPad = 6;

        // Scroll arrow button height (drawn at top/bottom of scrollbar track)
        private const int ScrollArrowH = 20;

        // Recent programs scrolling
        private int _scroll;
        private bool _scrollDrag;
        private int _scrollStartY;
        private int _scrollStartScroll;

        // Right column width
        private const int RightColW = 160;
        private const int RightColInnerPad = 6; // add inner padding to avoid text touching edge

        // Recent documents popout state
        private bool _docsPopupVisible = false;

        // All Programs view
        private bool _showAllPrograms = false;
        private List<int> _allProgramsOrder; // indices into Desktop.Apps sorted by name
        private List<Window> _allProgramsWindows; // windows with ShowInStartMenu = true

        // Cache for recent program entries to avoid ToArray() each frame
        private AppEntry[] _recentCache;
        private int _recentCacheCount;
        private ulong _recentCacheTick;

        // Cached blurred background for responsiveness (disabled for live blur)
        private Image _bgBlurCache;
        private bool _bgCacheReady;

        // Intelligent frame cache: redraw at most every ~120ms or when state changes
        private Image _frameCache;
        private ulong _frameCacheTick;
        private bool _frameDirty;
        private const ulong MinRedrawMs = 120;

        private bool _leftDownPrev; // edge detect
        private bool _constructed;

        public unsafe StartMenu() : base(_x, _y, _x2, _y2) {
            Title = "Start";
            BarHeight = 0;
            ShowInTaskbar = false; // do not show a taskbar button for Start menu
            ShowInStartMenu = false; // the shell popup is not an application entry
            ShowMaximize = false;
            ShowMinimize = false;
            _showAllPrograms = false;
            _frameDirty = true;

            // Window's base constructor invokes the virtual visibility hook
            // before this derived object is initialized. Do not build the
            // large blur cache from that callback, and keep a newly-created
            // menu hidden so the first taskbar click opens it.
            _constructed = true;
            _visible = false;
        }

        public override void OnSetVisible(bool value) {
            if (!_constructed) return;
            base.OnSetVisible(value);
            if (value) {
                // Start is a shell foreground owner, not a fake application
                // descriptor.  Deactivate the semantic app owner while it is
                // open and leave all application instances alive.
                ApplicationInstanceRegistry.NotifyShellForeground();
                // Always bring Start Menu to front when shown
                WindowManager.MoveToEnd(this);
                _leftDownPrev = false;
                _scrollDrag = false;
                // Rebuild background blur cache once
                _bgCacheReady = false;
                if (_bgBlurCache != null) { _bgBlurCache.Dispose(); _bgBlurCache = null; }
                BuildBackgroundBlurCache();
                // Invalidate frame cache
                if (_frameCache != null) { _frameCache.Dispose(); _frameCache = null; }
                _frameDirty = true;
            } else {
                // dispose caches when hidden to free memory
                if (_bgBlurCache != null) { _bgBlurCache.Dispose(); _bgBlurCache = null; }
                if (_frameCache != null) { _frameCache.Dispose(); _frameCache = null; }
                // A reopened Start menu begins in its normal recent-items
                // view.  Do not retain a transient all-programs/scroll state
                // from the previous activation.
                _showAllPrograms = false;
                _scroll = 0;
                _powerMenuVisible = false;
                _docsPopupVisible = false;
                _leftDownPrev = false;
                _bgCacheReady = false; _frameDirty = true;
            }
        }

        private void BuildBackgroundBlurCache() {
            // Capture current screen region under the menu and blur once
            int w = Width; int h = Height; if (w <= 0 || h <= 0) { _bgCacheReady = false; return; }
            var img = new Image(w, h);
            for (int yy = 0; yy < h; yy++) {
                int fbY = Y + yy;
                for (int xx = 0; xx < w; xx++) {
                    int fbX = X + xx;
                    img.RawData[yy * w + xx] = (int)Framebuffer.Graphics.GetPoint(fbX, fbY);
                }
            }
            // Box blur into temp buffers (horizontal + vertical), radius 3 to match previous look
            int radius = 3;
            int[] src = img.RawData; int[] tmp = new int[w * h]; int[] dst = new int[w * h];
            // Horizontal
            for (int yy = 0; yy < h; yy++) {
                int row = yy * w;
                for (int xx = 0; xx < w; xx++) {
                    int r = 0, g = 0, b = 0, a = 0, count = 0;
                    int xmin = xx - radius; if (xmin < 0) xmin = 0;
                    int xmax = xx + radius; if (xmax >= w) xmax = w - 1;
                    for (int k = xmin; k <= xmax; k++) {
                        int c = src[row + k];
                        a += (byte)(c >> 24);
                        r += (byte)(c >> 16);
                        g += (byte)(c >> 8);
                        b += (byte)(c);
                        count++;
                    }
                    a /= count; r /= count; g /= count; b /= count;
                    tmp[row + xx] = (int)((a << 24) | (r << 16) | (g << 8) | b);
                }
            }
            // Vertical
            for (int xx = 0; xx < w; xx++) {
                for (int yy = 0; yy < h; yy++) {
                    int r = 0, g = 0, b = 0, a = 0, count = 0;
                    int ymin = yy - radius; if (ymin < 0) ymin = 0;
                    int ymax = yy + radius; if (ymax >= h) ymax = h - 1;
                    for (int k = ymin; k <= ymax; k++) {
                        int c = tmp[k * w + xx];
                        a += (byte)(c >> 24);
                        r += (byte)(c >> 16);
                        g += (byte)(c >> 8);
                        b += (byte)(c);
                        count++;
                    }
                    a /= count; r /= count; g /= count; b /= count;
                    dst[yy * w + xx] = (int)((a << 24) | (r << 16) | (g << 8) | b);
                }
            }
            // write back blurred pixels
            for (int i = 0; i < dst.Length; i++) img.RawData[i] = dst[i];
            // swap into cache
            if (_bgBlurCache != null) _bgBlurCache.Dispose();
            _bgBlurCache = img; _bgCacheReady = true;
        }

        private struct AppEntry { public Image Icon; public string Name; }

        private void RefreshRecentCacheIfNeeded() {
            // Refresh at most once per 250ms to limit work
            if (Timer.Ticks == _recentCacheTick) return;
            _recentCacheTick = Timer.Ticks;
            var list = RecentManager.Programs;
            int count = list.Count;
            if (_recentCache == null || _recentCache.Length < count) _recentCache = new AppEntry[count];
            _recentCacheCount = count;
            // copy references without ToArray()
            for (int i = 0; i < count; i++) {
                var it = list[i];
                _recentCache[i].Icon = it.Icon ?? Icons.DocumentIcon(32);
                _recentCache[i].Name = it.Name;
            }
        }

        public override void OnInput() {
            base.OnInput();
            if (!Visible) return;

            // Close on Escape
            if (Keyboard.KeyInfo.Key == ConsoleKey.Escape) { _powerMenuVisible = false; _docsPopupVisible = false; Visible = false; return; }

            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            bool clickEdge = leftDown && !_leftDownPrev; // only act on new press

            int bottomY = Y + Height - Padding - ShutdownBtnH;
            int shutdownX = X + Width - Padding - ShutdownBtnW - ArrowBtnW - Gap;
            int arrowX = X + Width - Padding - ArrowBtnW;

            // Right column rect (fixed, not scrollable)
            int rcX = X + Width - Padding - RightColW;
            int rcY = Y + Padding;
            int rcW = RightColW;
            int rcH = Height - Padding * 2 - (ShutdownBtnH + Gap + Padding);

            // Recent/All Programs list rect (left side)
            int listX = X + Padding;
            int listY = Y + Padding;
            int listW = (rcX - Gap) - listX; // space left of right column
            int listH = rcH;

            // Mouse wheel scrolling
            if (mx >= listX && mx <= listX + listW && my >= listY && my <= listY + listH)
            {
                int scrollDelta = PS2Mouse.DeltaZ;
                if (scrollDelta != 0)
                {
                    int scrollAmount = -scrollDelta * Spacing; // scroll one item per wheel notch

                    int newScroll = _scroll + scrollAmount;

                    // Clamp scroll value
                    int total = (_showAllPrograms ? (Desktop.Apps.Length + (_allProgramsWindows != null ? _allProgramsWindows.Count : 0)) : RecentManager.Programs.Count) * Spacing;
                    int maxScroll = total - listH;
                    if (maxScroll < 0) maxScroll = 0;

                    if (newScroll < 0) newScroll = 0;
                    if (newScroll > maxScroll) newScroll = maxScroll;

                    if (newScroll != _scroll)
                    {
                        _scroll = newScroll;
                        _frameDirty = true;
                    }
                }
            }

            // All Programs toggle button area (bottom-left)
            int allBtnH = 28;
            int allBtnW = 140;
            int allBtnX = X + Padding;
            int allBtnY = bottomY; // align with shutdown row

#if UEFI_DIAGNOSTIC_APP_RUNTIME
            if (clickEdge) {
                Program.MarkUefiAppRuntime("START_CLICK_EDGE;x=" + mx.ToString() +
                    ";y=" + my.ToString() + ";all=" +
                    (_showAllPrograms ? "1" : "0"));
            }
#endif

            // Scrollbar hit - now wider (20px instead of 8px)
            int sbW = 20;
            int sbX = listX + listW - sbW;
            if (clickEdge) {
                // Power menu items (if visible) first so they capture clicks on overlay region
                if (_powerMenuVisible) {
                    int menuH = MenuPad * 2 + (MenuItemH * 2);
                    int menuW = MenuW;
                    int menuX = X + Width - Padding - menuW;
                    int menuY = bottomY - menuH - Gap;
                    if (mx >= menuX && mx <= menuX + menuW && my >= menuY && my <= menuY + menuH) {
                        int itemY = menuY + MenuPad;
                        if (my >= itemY && my < itemY + MenuItemH) { // Reboot
                            Power.Reboot(); _powerMenuVisible = false; Visible = false; return;
                        }
                        if (my >= itemY + MenuItemH && my < itemY + (2 * MenuItemH)) { // Log Off
                            // Simple logoff: hide all windows & show lock screen
                            for (int i = 0; i < WindowManager.Windows.Count; i++) WindowManager.Windows[i].Visible = false;
                            _powerMenuVisible = false; Lockscreen.Run(); Visible = false; return;
                        }
                    }
                }
                // Shutdown
                if (mx >= shutdownX && mx <= shutdownX + ShutdownBtnW && my >= bottomY && my <= bottomY + ShutdownBtnH) {
                    var dlg = new ShutdownDialog();
                    WindowManager.MoveToEnd(dlg);
                    dlg.Visible = true; Visible = false; _powerMenuVisible = false; return;
                }
                // Arrow toggle
                if (mx >= arrowX && mx <= arrowX + ArrowBtnW && my >= bottomY && my <= bottomY + ArrowBtnH) { _powerMenuVisible = !_powerMenuVisible; _frameDirty = true; return; }

                // All Programs button
                if (mx >= allBtnX && mx <= allBtnX + allBtnW && my >= allBtnY && my <= allBtnY + allBtnH) {
                    ToggleAllPrograms(); _frameDirty = true;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                    Program.MarkUefiAppRuntime("START_ALL_PROGRAMS=visible=" +
                        (_showAllPrograms ? "1" : "0"));
#endif
                    _leftDownPrev = leftDown;
                    return;
                }

                // Scroll arrow buttons at top and bottom of scrollbar
                int scrollArrowUpY = listY;
                int scrollArrowDnY = listY + listH - ScrollArrowH;
                int trackY = listY + ScrollArrowH;
                int trackH = listH - ScrollArrowH * 2;
                int totalForArrow = (_showAllPrograms ? (Desktop.Apps.Length + (_allProgramsWindows != null ? _allProgramsWindows.Count : 0)) : RecentManager.Programs.Count) * Spacing;
                int maxScrollArrow = totalForArrow - listH; if (maxScrollArrow < 0) maxScrollArrow = 0;
                // Up arrow click: scroll up one page
                if (mx >= sbX && mx <= sbX + sbW && my >= scrollArrowUpY && my < scrollArrowUpY + ScrollArrowH) {
                    int ns = _scroll - listH; if (ns < 0) ns = 0;
                    if (ns != _scroll) { _scroll = ns; _frameDirty = true; }
                    _leftDownPrev = leftDown;
                    return;
                }
                // Down arrow click: scroll down one page
                if (mx >= sbX && mx <= sbX + sbW && my >= scrollArrowDnY && my <= scrollArrowDnY + ScrollArrowH) {
                    int ns = _scroll + listH; if (ns > maxScrollArrow) ns = maxScrollArrow;
                    if (ns != _scroll) { _scroll = ns; _frameDirty = true; }
                    // The arrow branch returns before the common edge-state
                    // update below. Preserve the press edge while the host
                    // button is held so one click cannot repeat every frame.
                    _leftDownPrev = leftDown;
                    return;
                }
                // Scrollbar drag start (only in track between arrows)
                if (mx >= sbX && mx <= sbX + sbW && my >= trackY && my <= trackY + trackH) { _scrollDrag = true; _scrollStartY = my; _scrollStartScroll = _scroll; return; }

                // Click in right column
                if (mx >= rcX && mx <= rcX + rcW && my >= rcY && my <= rcY + rcH) {
                    int iy = rcY;
                    // Computer Files
                    int fh = Icons.FolderIcon(32).Height;
                    int fw = Icons.FolderIcon(32).Width;
                    // Make entire row clickable, not just icon
                    if (mx >= rcX && mx <= rcX + rcW && my >= iy && my <= iy + fh) {
                        Desktop.LaunchApplication("Computer Files");
                        Visible = false;
                        return;
                    }
                    iy += fh + 16;
                    // Disk Manager
                    int dwh = Icons.FolderIcon(32).Height; int dww = Icons.FolderIcon(32).Width;
                    // Make entire row clickable, not just icon
                    if (mx >= rcX && mx <= rcX + rcW && my >= iy && my <= iy + dwh) {
                        Desktop.LaunchApplication("Disk Manager");
                        Visible = false;
                        return;
                    }
                    iy += dwh + 16;
                    // Console
                    int cwh = Icons.DocumentIcon(32).Height; int cww = Icons.DocumentIcon(32).Width;
                    // Make entire row clickable, not just icon
                    if (mx >= rcX && mx <= rcX + rcW && my >= iy && my <= iy + cwh) {
                        Desktop.LaunchApplication("Console");
                        Visible = false;
                        return;
                    }
                    iy += cwh + 16;
                    // Recent Documents (toggle popup)
                    int iconW = Icons.DocumentIcon(32).Width;
                    int iconH = Icons.DocumentIcon(32).Height;
                    // Make entire row clickable, not just icon
                    if (mx >= rcX && mx <= rcX + rcW && my >= iy && my <= iy + iconH) { _docsPopupVisible = !_docsPopupVisible; _frameDirty = true; return; }

                    // USB Files entry (only if at least one USB MSC device is present)
                    iy += iconH + 16;
                    if (Kernel.Drivers.USBStorage.Count > 0) {
                        int ux = rcX; int uy = iy; int uw = Icons.FolderIcon(32).Width; int uh = Icons.FolderIcon(32).Height;
                        // Make entire row clickable, not just icon
                        if (mx >= rcX && mx <= rcX + rcW && my >= uy && my <= uy + uh) {
                            Desktop.LaunchComputerFilesDrive("USB Drive 1", 380,
                                220);
                            Visible = false;
                            return;
                        }
                        // Provide a second entry for a list view of all USB drives
                        int ux2 = rcX; int uy2 = iy + uh + 12; int uw2 = uw; int uh2 = uh;
                        // Make entire row clickable, not just icon
                        if (mx >= rcX && mx <= rcX + rcW && my >= uy2 && my <= uy2 + uh2) {
                            var list = new USBDrives(rcX - 280, rcY + 40, 420, 360);
                            WindowManager.MoveToEnd(list);
                            list.Visible = true;
                            Visible = false;
                            return;
                        }
                    }
                }

                // Right-click on list -> Pin to Taskbar
                if (Control.MouseButtons.HasFlag(MouseButtons.Right)) {
                    if (mx >= listX && mx <= listX + listW && my >= listY && my <= listY + listH) {
                        int cy = listY - _scroll; int cnt = _showAllPrograms ? Desktop.Apps.Length : RecentManager.Programs.Count;
                        for (int i = 0; i < cnt; i++) {
                            int ih; string appName = null;
                            if (_showAllPrograms) {
                                int ai = _allProgramsOrder != null && i < _allProgramsOrder.Count ? _allProgramsOrder[i] : i;
                                var icon = Desktop.Apps.Icon(ai) ?? Icons.DocumentIcon(32); ih = icon.Height;
                                if (my >= cy && my <= cy + ih) { appName = Desktop.Apps.Name(ai); }
                            } else {
                                RefreshRecentCacheIfNeeded(); if (i >= _recentCacheCount) break; var icon = _recentCache[i].Icon; ih = icon.Height;
                                if (my >= cy && my <= cy + ih) { appName = _recentCache[i].Name; }
                            }
                            if (appName != null) { PinnedManager.PinApp(appName, Icons.DocumentIcon(32)); _frameDirty = true; return; }
                            cy += Spacing;
                        }
                    }
                }

                // Click in list (Recent or All Programs)
                if (mx >= listX && mx <= listX + listW && my >= listY && my <= listY + listH) {
                    int count2 = _showAllPrograms ? (Desktop.Apps.Length + (_allProgramsWindows != null ? _allProgramsWindows.Count : 0)) : RecentManager.Programs.Count;
                    int y = listY - _scroll;
                    for (int i = 0; i < count2; i++) {
                        int ih;
                        int ix = listX;
                        int iy2 = y;
                        
                        if (_showAllPrograms) {
                            // Determine if this is a Desktop.App or a Window
                            if (i < Desktop.Apps.Length) {
                                // Regular Desktop.App
                                int ai = _allProgramsOrder != null && i < _allProgramsOrder.Count ? _allProgramsOrder[i] : i;
                                var icon = Desktop.Apps.Icon(ai) ?? Icons.DocumentIcon(32);
                                ih = icon.Height;
                                if (my >= iy2 && my <= iy2 + ih) {
                                    string appName = Desktop.Apps.Name(ai);
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                                    Program.MarkUefiAppRuntime("START_ROW=index=" +
                                        i.ToString() + ";name=" + appName);
                                    Program.MarkUefiAppRuntime("START_SELECT=name=" +
                                        appName + ";route=all-programs;index=" + i.ToString());
#endif
                                    Desktop.LaunchApplication(appName);
                                    appName.Dispose();
                                    Visible = false;
                                    return;
                                }
                            } else {
                                // Window with ShowInStartMenu = true
                                int windowIndex = i - Desktop.Apps.Length;
                                if (_allProgramsWindows != null && windowIndex < _allProgramsWindows.Count) {
                                    var window = _allProgramsWindows[windowIndex];
                                    var icon = window.TaskbarIcon ?? Icons.DocumentIcon(32);
                                    ih = icon.Height;
                                    if (my >= iy2 && my <= iy2 + ih) {
                                        // Show/activate the window
                                        window.Visible = true;
                                        WindowManager.MoveToEnd(window);
                                        Visible = false;
                                        return;
                                    }
                                }
                            }
                        } else {
                            // use cache
                            RefreshRecentCacheIfNeeded();
                            if (i >= _recentCacheCount) break;
                            var icon = _recentCache[i].Icon;
                            ih = icon.Height;
                            if (my >= iy2 && my <= iy2 + ih) {
                                Desktop.LaunchApplication(_recentCache[i].Name);
                                Visible = false;
                                return;
                            }
                        }
                        y += Spacing;
                    }
                }
            } else if (!leftDown) { _scrollDrag = false; }

            // Drag update
            if (_scrollDrag && leftDown) {
                int total = (_showAllPrograms ? Desktop.Apps.Length : RecentManager.Programs.Count) * Spacing;
                int maxScroll = total - listH; if (maxScroll < 0) maxScroll = 0;
                int dy = my - _scrollStartY;
                
                // Convert pixel delta to scroll value based on thumb position in track area (between arrows)
                int trackArea = listH - ScrollArrowH * 2;
                int thumbH = trackArea;
                if (total > listH && trackArea > 0) {
                    thumbH = (trackArea * listH) / total;
                    if (thumbH < 16) thumbH = 16;
                    if (thumbH > trackArea) thumbH = trackArea;
                }
                
                // Calculate scroll based on thumb movement
                int scrollRange = trackArea - thumbH;
                int newScroll = scrollRange > 0 ? (dy * maxScroll) / scrollRange : 0;
                newScroll = _scrollStartScroll + newScroll;
                
                if (newScroll < 0) newScroll = 0; 
                if (newScroll > maxScroll) newScroll = maxScroll;
                if (newScroll != _scroll) { _scroll = newScroll; _frameDirty = true; }
            }
            _leftDownPrev = leftDown;
        }

        public override void OnDraw() {
            if (!Visible) return;

            // Smart frame caching: reuse last frame if recent and no state change
            if (!_frameDirty && _frameCache != null) {
                ulong age = (ulong)(Timer.Ticks - _frameCacheTick);
                if (age < MinRedrawMs) { Framebuffer.Graphics.DrawImage(X, Y, _frameCache); return; }
            }

            // background blur cache or live blur
            if (_bgCacheReady && _bgBlurCache != null) {
                Framebuffer.Graphics.DrawImage(X, Y, _bgBlurCache);
                UIPrimitives.AFillRoundedRect(X, Y, Width, Height, 0x66222222, 4);
            } else {
                // live blur + lighter translucent tint so content beneath is visible
                Framebuffer.Graphics.BlurRectangle(X, Y, Width, Height, 3);
                UIPrimitives.AFillRoundedRect(X, Y, Width, Height, 0x66222222, 4);
            }

            // Mouse for hover effects
            int mouseX = Control.MousePosition.X; int mouseY = Control.MousePosition.Y;

            int bottomY = Y + Height - Padding - ShutdownBtnH;
            int shutdownX = X + Width - Padding - ShutdownBtnW - ArrowBtnW - Gap;
            int arrowX = X + Width - Padding - ArrowBtnW;

            // Fixed right column
            int rcX = X + Width - Padding - RightColW;
            int rcY = Y + Padding;
            int rcW = RightColW;
            int rcH = Height - Padding * 2 - (ShutdownBtnH + Gap + Padding);

            // Left list
            int listX = X + Padding;
            int listY = Y + Padding;
            int listW = (rcX - Gap) - listX;
            int listH = rcH;

            // Recent programs or All Programs
            int count = _showAllPrograms ? (Desktop.Apps.Length + (_allProgramsWindows != null ? _allProgramsWindows.Count : 0)) : RecentManager.Programs.Count;
            int y = listY - _scroll;
            
            // Scrollbar width to account for in hover area
            int sbW = 20;
            
            if (_showAllPrograms) {
                for (int i = 0; i < count; i++) {
                    Image icon;
                    string name;
                    bool isWindow = false;
                    
                    // Determine if this is a Desktop.App or a Window
                    if (i < Desktop.Apps.Length) {
                        // Regular Desktop.App
                        int ai = _allProgramsOrder != null && i < _allProgramsOrder.Count ? _allProgramsOrder[i] : i;
                        icon = Desktop.Apps.Icon(ai) ?? Icons.DocumentIcon(32);
                        name = Desktop.Apps.Name(ai);
                    } else {
                        // Window with ShowInStartMenu = true
                        int windowIndex = i - Desktop.Apps.Length;
                        if (_allProgramsWindows != null && windowIndex < _allProgramsWindows.Count) {
                            var window = _allProgramsWindows[windowIndex];
                            icon = window.TaskbarIcon ?? Icons.DocumentIcon(32);
                            name = window.Title;
                            isWindow = true;
                        } else {
                            continue; // Skip if index is out of range
                        }
                    }
                    
                    int ih = icon.Height; int iw = icon.Width;
                    
                    // Skip items that are completely outside the visible area
                    if (y + ih < listY || y > listY + listH) {
                        y += Spacing;
                        if (!isWindow) name.Dispose();
                        continue;
                    }
                    
                    // Hover effect backdrop (constrain to list region, avoid scrollbar overlap)
                    if (mouseX >= listX && mouseX <= listX + listW - sbW && mouseY >= y && mouseY <= y + ih && 
                        y >= listY && y + ih <= listY + listH) {
                        int hx = listX; // do not extend left
                        int hy = y - 2; // slightly tighter padding
                        int hw = listW - sbW - 4; // reserve space for scrollbar plus gap
                        if (hw < 0) hw = listW - sbW; 
                        int hh = ih + 4;
                        UIPrimitives.AFillRoundedRect(hx, hy, hw, hh, 0x333F7FBF, 6);
                        UIPrimitives.DrawRoundedRect(hx, hy, hw, hh, 0xFF3F7FBF, 1, 6);
                        // Accent bar aligned left inside highlight
                        Framebuffer.Graphics.FillRectangle(hx, hy, 3, hh, 0x883F7FBF);
                    }
                    Framebuffer.Graphics.DrawImage(listX, y, icon);
                    WindowManager.font.DrawString(listX + iw + 10, y + (ih / 2) - (WindowManager.font.FontSize / 2), name, listW - (iw + 22), WindowManager.font.FontSize);
                    y += Spacing;
                    if (!isWindow) name.Dispose();
                }
            } else {
                // use cached entries
                RefreshRecentCacheIfNeeded();
                int max = _recentCacheCount;
                for (int i = 0; i < max; i++) {
                    var icon = _recentCache[i].Icon;
                    var name = _recentCache[i].Name;
                    int ih = icon.Height; int iw = icon.Width;
                    
                    // Skip items that are completely outside the visible area
                    if (y + ih < listY || y > listY + listH) {
                        y += Spacing;
                        continue;
                    }
                    
                    if (mouseX >= listX && mouseX <= listX + listW - sbW && mouseY >= y && mouseY <= y + ih &&
                        y >= listY && y + ih <= listY + listH) {
                        int hx = listX; 
                        int hy = y - 2; 
                        int hw = listW - sbW - 4; // reserve space for scrollbar plus gap
                        if (hw < 0) hw = listW - sbW; 
                        int hh = ih + 4;
                        UIPrimitives.AFillRoundedRect(hx, hy, hw, hh, 0x333F7FBF, 6);
                        UIPrimitives.DrawRoundedRect(hx, hy, hw, hh, 0xFF3F7FBF, 1, 6);
                        Framebuffer.Graphics.FillRectangle(hx, hy, 3, hh, 0x883F7FBF);
                    }
                    Framebuffer.Graphics.DrawImage(listX, y, icon);
                    WindowManager.font.DrawString(listX + iw + 10, y + (ih / 2) - (WindowManager.font.FontSize / 2), name, listW - (iw + 22), WindowManager.font.FontSize);
                    y += Spacing;
                }
            }

            // Scrollbar for list
            // int sbW = 20; // Already declared above - Wider scrollbar (was 8)
            int sbX = listX + listW - sbW;
            Framebuffer.Graphics.FillRectangle(sbX, listY, sbW, listH, 0xFF1A1A1A);
            int total = count * Spacing;

            // Draw up/down arrow buttons at top and bottom of the scrollbar
            int arrowUpY = listY;
            int arrowDnY = listY + listH - ScrollArrowH;
            int trackTopY = listY + ScrollArrowH;
            int trackAreaH = listH - ScrollArrowH * 2;

            // Up arrow button
            bool hoverUp = (mouseX >= sbX && mouseX <= sbX + sbW && mouseY >= arrowUpY && mouseY < arrowUpY + ScrollArrowH);
            Framebuffer.Graphics.FillRectangle(sbX, arrowUpY, sbW, ScrollArrowH, hoverUp ? 0xFF3A3A3A : 0xFF2A2A2A);
            Framebuffer.Graphics.DrawRectangle(sbX, arrowUpY, sbW, ScrollArrowH, 0xFF3F3F3F, 1);
            // Draw up triangle (centered in button)
            int triCx = sbX + sbW / 2;
            int triCy = arrowUpY + ScrollArrowH / 2;
            for (int r = 0; r < 5; r++) {
                int lx = triCx - r; int rx = triCx + r; int ty = triCy + r - 2;
                if (ty >= arrowUpY && ty < arrowUpY + ScrollArrowH) {
                    Framebuffer.Graphics.FillRectangle(lx, ty, rx - lx + 1, 1, 0xFFCCCCCC);
                }
            }

            // Down arrow button
            bool hoverDn = (mouseX >= sbX && mouseX <= sbX + sbW && mouseY >= arrowDnY && mouseY <= arrowDnY + ScrollArrowH);
            Framebuffer.Graphics.FillRectangle(sbX, arrowDnY, sbW, ScrollArrowH, hoverDn ? 0xFF3A3A3A : 0xFF2A2A2A);
            Framebuffer.Graphics.DrawRectangle(sbX, arrowDnY, sbW, ScrollArrowH, 0xFF3F3F3F, 1);
            // Draw down triangle (centered in button)
            for (int r = 0; r < 5; r++) {
                int lx = triCx - r; int rx = triCx + r; int ty = arrowDnY + ScrollArrowH / 2 - r + 2;
                if (ty >= arrowDnY && ty < arrowDnY + ScrollArrowH) {
                    Framebuffer.Graphics.FillRectangle(lx, ty, rx - lx + 1, 1, 0xFFCCCCCC);
                }
            }

            if (total > listH && trackAreaH > 0) {
                int thumbH = (trackAreaH * listH) / total; if (thumbH < 16) thumbH = 16; if (thumbH > trackAreaH) thumbH = trackAreaH;
                int maxScroll = total - listH; if (maxScroll < 1) maxScroll = 1;
                int thumbY = ((trackAreaH - thumbH) * _scroll) / maxScroll; if (thumbY + thumbH > trackAreaH) thumbY = trackAreaH - thumbH;
                
                // Highlight scrollbar thumb on hover
                bool hoverThumb = (mouseX >= sbX && mouseX <= sbX + sbW && 
                                  mouseY >= trackTopY + thumbY && mouseY <= trackTopY + thumbY + thumbH);
                uint thumbColor = hoverThumb ? 0xFF4F4F4F : 0xFF2F2F2F;
                
                Framebuffer.Graphics.FillRectangle(sbX + 2, trackTopY + thumbY, sbW - 4, thumbH, thumbColor);
            }

            // Right column content (with padding and truncation) + hover rows
            int rcCursorY = rcY;
            int textMax = rcW - RightColInnerPad - (Icons.FolderIcon(32).Width + 8);
            // Computer Files icon + label
            //var cfIcon = Icons.FolderIcon;
            var cfIcon = Icons.FolderIcon(32);
            int rowH = cfIcon.Height; int rowW = rcW - 4;
            if (mouseX >= rcX && mouseX <= rcX + rcW && mouseY >= rcCursorY && mouseY <= rcCursorY + rowH) {
                UIPrimitives.AFillRoundedRect(rcX + 2, rcCursorY - 2, rowW, rowH + 4, 0x332A5B9A, 6);
                UIPrimitives.DrawRoundedRect(rcX + 2, rcCursorY - 2, rowW, rowH + 4, 0xFF3F7FBF, 1, 6);
                Framebuffer.Graphics.FillRectangle(rcX + 2, rcCursorY - 2, 3, rowH + 4, 0x883F7FBF);
            }
            Framebuffer.Graphics.DrawImage(rcX + RightColInnerPad, rcCursorY, cfIcon);
            string cfText = TruncateToWidth("Computer Files", textMax);
            WindowManager.font.DrawString(rcX + RightColInnerPad + cfIcon.Width + 8, rcCursorY + (cfIcon.Height / 2) - (WindowManager.font.FontSize / 2), cfText);
            rcCursorY += cfIcon.Height + 16;
            cfText.Dispose();

            // Disk Manager icon + label
            if (mouseX >= rcX && mouseX <= rcX + rcW && mouseY >= rcCursorY && mouseY <= rcCursorY + rowH) {
                UIPrimitives.AFillRoundedRect(rcX + 2, rcCursorY - 2, rowW, rowH + 4, 0x332A5B9A, 6);
                UIPrimitives.DrawRoundedRect(rcX + 2, rcCursorY - 2, rowW, rowH + 4, 0xFF3F7FBF, 1, 6);
                Framebuffer.Graphics.FillRectangle(rcX + 2, rcCursorY - 2, 3, rowH + 4, 0x883F7FBF);
            }
            Framebuffer.Graphics.DrawImage(rcX + RightColInnerPad, rcCursorY, cfIcon);
            string dmText = TruncateToWidth("Disk Manager", textMax);
            WindowManager.font.DrawString(rcX + RightColInnerPad + cfIcon.Width + 8, rcCursorY + (cfIcon.Height / 2) - (WindowManager.font.FontSize / 2), dmText);
            rcCursorY += cfIcon.Height + 16;
            dmText.Dispose();

            // Console icon + label
            var conIcon = Icons.DocumentIcon(32);
            int conRowH = conIcon.Height;
            if (mouseX >= rcX && mouseX <= rcX + rcW && mouseY >= rcCursorY && mouseY <= rcCursorY + conRowH) {
                UIPrimitives.AFillRoundedRect(rcX + 2, rcCursorY - 2, rowW, conRowH + 4, 0x332A5B9A, 6);
                UIPrimitives.DrawRoundedRect(rcX + 2, rcCursorY - 2, rowW, conRowH + 4, 0xFF3F7FBF, 1, 6);
                Framebuffer.Graphics.FillRectangle(rcX + 2, rcCursorY - 2, 3, conRowH + 4, 0x883F7FBF);
            }
            Framebuffer.Graphics.DrawImage(rcX + RightColInnerPad, rcCursorY, conIcon);
            string conText = TruncateToWidth("Console", textMax);
            WindowManager.font.DrawString(rcX + RightColInnerPad + conIcon.Width + 8, rcCursorY + (conIcon.Height / 2) - (WindowManager.font.FontSize / 2), conText);
            rcCursorY += conIcon.Height + 16;
            conText.Dispose();

            // Recent Documents with popout
            var docIcon = Icons.DocumentIcon(32);
            rowH = Icons.DocumentIcon(32).Height;
            if (mouseX >= rcX && mouseX <= rcX + rcW && mouseY >= rcCursorY && mouseY <= rcCursorY + rowH) {
                UIPrimitives.AFillRoundedRect(rcX + 2, rcCursorY - 2, rowW, rowH + 4, 0x332A5B9A, 6);
                UIPrimitives.DrawRoundedRect(rcX + 2, rcCursorY - 2, rowW, rowH + 4, 0xFF3F7FBF, 1, 6);
                Framebuffer.Graphics.FillRectangle(rcX + 2, rcCursorY - 2, 3, rowH + 4, 0x883F7FBF);
            }
            Framebuffer.Graphics.DrawImage(rcX + RightColInnerPad, rcCursorY, docIcon);
            string rdText = TruncateToWidth("Recent Documents", textMax);
            WindowManager.font.DrawString(rcX + RightColInnerPad + docIcon.Width + 8, rcCursorY + (docIcon.Height / 2) - (WindowManager.font.FontSize / 2), rdText);

            // USB Files indicator
            rcCursorY += docIcon.Height + 16;
            if (Kernel.Drivers.USBStorage.Count > 0) {
                if (mouseX >= rcX && mouseX <= rcX + rcW && mouseY >= rcCursorY && mouseY <= rcCursorY + cfIcon.Height) {
                    UIPrimitives.AFillRoundedRect(rcX + 2, rcCursorY - 2, rowW, cfIcon.Height + 4, 0x332A5B9A, 6);
                    UIPrimitives.DrawRoundedRect(rcX + 2, rcCursorY - 2, rowW, cfIcon.Height + 4, 0xFF3F7FBF, 1, 6);
                    Framebuffer.Graphics.FillRectangle(rcX + 2, rcCursorY - 2, 3, cfIcon.Height + 4, 0x883F7FBF);
                }
                Framebuffer.Graphics.DrawImage(rcX + RightColInnerPad, rcCursorY, cfIcon);
                string usbText = TruncateToWidth("USB Files", textMax);
                WindowManager.font.DrawString(rcX + RightColInnerPad + cfIcon.Width + 8, rcCursorY + (cfIcon.Height / 2) - (WindowManager.font.FontSize / 2), usbText);
                usbText.Dispose();
                rcCursorY += cfIcon.Height + 16;
                // Draw second item for list view
                if (mouseX >= rcX && mouseX <= rcX + rcW && mouseY >= rcCursorY && mouseY <= rcCursorY + cfIcon.Height) {
                    UIPrimitives.AFillRoundedRect(rcX + 2, rcCursorY - 2, rowW, cfIcon.Height + 4, 0x332A5B9A, 6);
                    UIPrimitives.DrawRoundedRect(rcX + 2, rcCursorY - 2, rowW, cfIcon.Height + 4, 0xFF3F7FBF, 1, 6);
                    Framebuffer.Graphics.FillRectangle(rcX + 2, rcCursorY - 2, 3, cfIcon.Height + 4, 0x883F7FBF);
                }
                Framebuffer.Graphics.DrawImage(rcX + RightColInnerPad, rcCursorY, cfIcon);
                string usbListText = TruncateToWidth("USB Drives", textMax);
                WindowManager.font.DrawString(rcX + RightColInnerPad + cfIcon.Width + 8, rcCursorY + (cfIcon.Height / 2) - (WindowManager.font.FontSize / 2), usbListText);
                usbListText.Dispose();
                rcCursorY += cfIcon.Height + 16;
            }

            // Popout panel to the right if visible (slightly translucent too)
            if (_docsPopupVisible) {
                int popX = rcX + rcW + 6;
                int popY = rcCursorY; // shift below the last item
                int popW = 260;
                int visibleDocs = 8;
                int popH = visibleDocs * (WindowManager.font.FontSize + 6) + 8;
                Framebuffer.Graphics.AFillRectangle(popX, popY, popW, popH, 0xCC262626);
                Framebuffer.Graphics.DrawRectangle(popX, popY, popW, popH, 0xFF3F3F3F, 1);
                int py = popY + 4;
                var docs = RecentManager.Documents;
                int dcount = docs.Count < visibleDocs ? docs.Count : visibleDocs;
                for (int i = 0; i < dcount; i++) {
                    var d = docs.ToArray()[i];
                    // simple filename display
                    string label = d.Path;
                    WindowManager.font.DrawString(popX + 6, py, label, popW - 12, WindowManager.font.FontSize);
                    py += WindowManager.font.FontSize + 6;
                    label.Dispose();
                }
            }
            rdText.Dispose();

            // Buttons at bottom-right
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            bool overShutdown = (mx >= shutdownX && mx <= shutdownX + ShutdownBtnW && my >= bottomY && my <= bottomY + ShutdownBtnH);
            bool overArrow = (mx >= arrowX && mx <= arrowX + ArrowBtnW && my >= bottomY && my <= bottomY + ArrowBtnH);
            uint btnBg = 0xFF2A2A2A; uint btnBgHover = 0xFF343434; uint border = 0xFF3F3F3F;
            Framebuffer.Graphics.FillRectangle(shutdownX, bottomY, ShutdownBtnW, ShutdownBtnH, overShutdown ? btnBgHover : btnBg);
            Framebuffer.Graphics.DrawRectangle(shutdownX, bottomY, ShutdownBtnW, ShutdownBtnH, border, 1);
            WindowManager.font.DrawString(shutdownX + 10, bottomY + (ShutdownBtnH / 2) - (WindowManager.font.FontSize / 2), "Shutdown");
            Framebuffer.Graphics.FillRectangle(arrowX, bottomY, ArrowBtnW, ArrowBtnH, overArrow ? btnBgHover : btnBg);
            Framebuffer.Graphics.DrawRectangle(arrowX, bottomY, ArrowBtnW, ArrowBtnH, border, 1);
            WindowManager.font.DrawString(arrowX + 8, bottomY + (ArrowBtnH / 2) - (WindowManager.font.FontSize / 2), ">");

            if (_powerMenuVisible) {
                int menuH = MenuPad * 2 + (MenuItemH * 2);
                int menuW = MenuW;
                int menuX = X + Width - Padding - menuW;
                int menuY = bottomY - menuH - Gap;
                // subtle translucency for menu as well
                Framebuffer.Graphics.AFillRectangle(menuX, menuY, menuW, menuH, 0xCC262626);
                Framebuffer.Graphics.DrawRectangle(menuX, menuY, menuW, menuH, border, 1);
                int itemY = menuY + MenuPad;
                bool hoverReboot = (mx >= menuX && mx <= menuX + menuW && my >= itemY && my < itemY + MenuItemH);
                bool hoverLogoff = (mx >= menuX && mx <= menuX + menuW && my >= itemY + MenuItemH && my < itemY + (2 * MenuItemH));
                if (hoverReboot) Framebuffer.Graphics.FillRectangle(menuX + 1, itemY, menuW - 2, MenuItemH, 0xFF313131);
                WindowManager.font.DrawString(menuX + 10, itemY + (MenuItemH / 2) - (WindowManager.font.FontSize / 2), "Reboot");
                if (hoverLogoff) Framebuffer.Graphics.FillRectangle(menuX + 1, itemY + MenuItemH, menuW - 2, MenuItemH, 0xFF313131);
                WindowManager.font.DrawString(menuX + 10, itemY + MenuItemH + (MenuItemH / 2) - (WindowManager.font.FontSize / 2), "Log Off");
            }

            // All Programs toggle button (bottom-left)
            int allBtnH = 28;
            int allBtnW = 140;
            int allBtnX = X + Padding;
            int allBtnY = bottomY;
            bool overAll = (mx >= allBtnX && mx <= allBtnX + allBtnW && my >= allBtnY && my <= allBtnY + allBtnH);
            Framebuffer.Graphics.FillRectangle(allBtnX, allBtnY, allBtnW, allBtnH, overAll ? btnBgHover : btnBg);
            Framebuffer.Graphics.DrawRectangle(allBtnX, allBtnY, allBtnW, allBtnH, border, 1);
            string allText = _showAllPrograms ? "Back" : "All Programs";
            WindowManager.font.DrawString(allBtnX + 10, allBtnY + (allBtnH / 2) - (WindowManager.font.FontSize / 2), allText);
            allText.Dispose();

            DrawBorder(false);

            // After full draw, capture frame for quick reuse
            if (_frameCache != null) { _frameCache.Dispose(); _frameCache = null; }
            int wcap = Width, hcap = Height;
            var cap = new Image(wcap, hcap);
            for (int yy = 0; yy < hcap; yy++) {
                int fbY = Y + yy;
                for (int xx = 0; xx < wcap; xx++) {
                    int fbX = X + xx;
                    cap.RawData[yy * wcap + xx] = (int)Framebuffer.Graphics.GetPoint(fbX, fbY);
                }
            }
            _frameCache = cap; _frameCacheTick = (ulong)Timer.Ticks; _frameDirty = false;
        }

        private void ToggleAllPrograms() {
            _showAllPrograms = !_showAllPrograms;
            _scroll = 0;
            if (_showAllPrograms) BuildAllProgramsOrder();
        }

        private void BuildAllProgramsOrder() {
            int n = Desktop.Apps.Length;
            _allProgramsOrder = _allProgramsOrder ?? new List<int>(n);
            _allProgramsOrder.Clear();
            for (int i = 0; i < n; i++) _allProgramsOrder.Add(i);
            
            // Get windows with ShowInStartMenu = true
            _allProgramsWindows = WindowManager.GetStartMenuWindows();
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            int layoutListH = Height - Padding * 2 -
                (ShutdownBtnH + Gap + Padding);
            int layoutTotal = (n + _allProgramsWindows.Count) * Spacing;
            int layoutMaxScroll = layoutTotal - layoutListH;
            if (layoutMaxScroll < 0) layoutMaxScroll = 0;
            Program.MarkUefiAppRuntime("START_ALL_LAYOUT=apps=" + n.ToString() +
                ";windows=" + _allProgramsWindows.Count.ToString() +
                ";listH=" + layoutListH.ToString() +
                ";maxscroll=" + layoutMaxScroll.ToString());
#endif
            
            // simple selection sort by name (case-insensitive)
            for (int i = 0; i < n - 1; i++) {
                int min = i;
                string minName = Desktop.Apps.Name(_allProgramsOrder[min]);
                for (int j = i + 1; j < n; j++) {
                    string nameJ = Desktop.Apps.Name(_allProgramsOrder[j]);
                    // compare
                    int cmp = CompareIgnoreCase(nameJ, minName);
                    if (cmp < 0) { min = j; minName.Dispose(); minName = nameJ; } else { nameJ.Dispose(); }
                }
                minName.Dispose();
                if (min != i) { int tmp = _allProgramsOrder[i]; _allProgramsOrder[i] = _allProgramsOrder[min]; _allProgramsOrder[min] = tmp; }
            }
        }

        private static int CompareIgnoreCase(string a, string b) {
            int la = a.Length; int lb = b.Length; int l = la < lb ? la : lb;
            for (int i = 0; i < l; i++) {
                char ca = a[i]; if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32);
                char cb = b[i]; if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32);
                if (ca != cb) return ca < cb ? -1 : 1;
            }
            if (la == lb) return 0; return la < lb ? -1 : 1;
        }

        private string TruncateToWidth(string text, int maxW) {
            if (WindowManager.font.MeasureString(text) <= maxW) return text;
            // Leave space for ellipsis '...'
            string ell = "...";
            int ellW = WindowManager.font.MeasureString(ell);
            int w = 0;
            int i = 0;
            for (; i < text.Length; i++) {
                int chW = WindowManager.font.MeasureString(text[i].ToString());
                if (w + chW + ellW > maxW) break;
                w += chW;
            }
            string sub = text.Substring(0, i) + ell;
            ell.Dispose();
            return sub;
        }
    }
}
