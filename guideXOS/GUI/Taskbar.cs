using guideXOS.Kernel.Drivers;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using guideXOS.DefaultApps;
using guideXOS.OS;
namespace guideXOS.GUI {
    internal class Taskbar {
        public StartMenu StartMenu;
        private int _barHeight;
        private Image _startIcon;
        private bool _clockUse12Hour = false;
        private bool _clockClickLatch = false;
        private bool _startClickLatch = false;
        
        // Taskbar auto-hide and slide animation
        private bool _taskbarHidden = false;
        private int _taskbarOffset = 0; // pixels hidden (0 = fully visible, _barHeight = fully hidden)
        private ulong _lastMouseActivity = 0;
        private bool _isAnimating = false;
        private int _animationStartOffset = 0;
        private int _animationTargetOffset = 0;
        private ulong _animationStartTick = 0;
        
        // Right-click context menu
        private TaskbarMenu _menu;
        private bool _rightClickLatch = false;

        // Network indicator animation
        private int _netAnimPhase = 0;
        private ulong _lastTick = 0;
        private bool _netConnectedShown = false;

        // Network animation scheduling
        private readonly ulong _bootTicks;
        private ulong _animWindowStart;
        private ulong _animWindowEnd;
        private ulong _nextCycleStart;
        private const ulong TenSeconds = 10_000;       // ms
        private const ulong FiveMinutes = 300_000;     // ms
        
        // Track actual network activity for animation
        private ulong _lastNetActivity = 0;
        private const ulong NetActivityWindow = 3_000; // show animation for 3 seconds after activity

        // New: latches and references for Workspace Switcher and Show Desktop
        private bool _taskViewLatch = false;
        private bool _showDesktopLatch = false;
        
        // DON'T use singleton - let it be created fresh but DELAY the creation
        private bool _needsWorkspaceSwitcher = false;
        private WorkspaceSwitcher _workspaceSwitcher; // Add field for the switcher instance

        // Public property to check if workspace switcher is visible (blocks input to windows)
        public bool IsWorkspaceSwitcherVisible => _workspaceSwitcher != null && _workspaceSwitcher.Visible;

        // Track windows minimized by Show Desktop to restore them on toggle
        private bool _desktopShown = false;
        private readonly List<Window> _minimizedByShowDesktop = new List<Window>(32);

        // Latch for pinned quicklaunch
        private bool _pinnedClickLatch = false;
        
        // On-Screen Keyboard button latch
        private bool _oskClickLatch = false;
        private bool _uefiStartGraphicsReported = false;
        private bool _taskbarAppClickLatch = false;
        private int _lastTaskbarVisualGroups = -1;
        private int _lastTaskbarVisualButtons = -1;
        private int _lastTaskbarVisualIcons = -1;
        private int _lastTaskbarVisualFallbacks = -1;
        private int _lastTaskbarVisualActive = -1;
        private int _lastTaskbarVisualActivations = -1;
        private int _lastTaskbarVisualHidden = -1;
        private int _lastTaskbarVisualInvalid = -1;
        private ulong _lastTaskbarVisualActiveHandle;
        private int _taskbarVisualActivations;

        private const int UefiTaskbarAppButtonWidth = 140;
        private const int UefiTaskbarAppButtonGap = 8;
        private const int UefiTaskbarStartAppGap = 8;
        private const int UefiTaskbarIconSize = 32;
        private const int UefiTaskbarClockGap = 16;

        // FIXED: Cache time/date strings to prevent per-frame allocations
        private string _cachedTime = null;
        private string _cachedDate = null;
        private int _lastHour = -1;
        private int _lastMinute = -1;
        private int _lastDay = -1;

        public Taskbar(int barHeight, Image startIcon) { 
            _barHeight = barHeight; 
            _startIcon = startIcon; 
            // schedule: show animation for first 10 seconds after boot
            _bootTicks = Timer.Ticks;
            _animWindowStart = _bootTicks;
            _animWindowEnd = _bootTicks + TenSeconds;
            _nextCycleStart = _bootTicks + FiveMinutes;

            // Initialize last mouse activity to current time so auto-hide respects initial delay after startup
            // Without this, timeSinceActivity would be large on first draw and cause immediate hide.
            _lastMouseActivity = _bootTicks;
        }

        public void CloseWorkspaceSwitcher() {
            if (_workspaceSwitcher != null) {
                _workspaceSwitcher.Visible = false;
            }
        }

        public void CloseContextMenu() {
            if (_menu != null) _menu.Visible = false;
            _rightClickLatch = false;
        }

        /// <summary>
        /// Show the workspace switcher overlay
        /// </summary>
        public void ShowWorkspaceSwitcher() {
            _needsWorkspaceSwitcher = true;
        }

        /// <summary>
        /// Draw the workspace switcher if visible (called separately to control z-order)
        /// </summary>
        public void DrawWorkspaceSwitcher() {
            // Handle workspace switcher input FIRST (if visible)
            if (_workspaceSwitcher != null && _workspaceSwitcher.Visible) {
                _workspaceSwitcher.OnInput();
                // Draw the workspace switcher
                _workspaceSwitcher.OnDraw();
            }
        }

        internal static bool TryResolveSemanticWindow(Window window,
                                                       out TaskbarApplicationEntry entry) {
            entry = null;
            if (window == null || !window.ApplicationInstanceHandle.IsValid) {
                return false;
            }
            if (!WindowManager.IsTaskbarEntryValid(window)) return false;
            return TaskbarApplicationEntryRegistry.TryGetForWindow(window,
                                                                   out entry);
        }

        /// <summary>
        /// Draw UEFI Taskbar
        /// </summary>
        private unsafe void DrawUEFITaskBar() {
            TaskbarApplicationEntryRegistry.Reconcile();
            guideXOS.Graph.Graphics graphics = Framebuffer.Graphics;
            if (graphics == null) return;

            int yTop = Framebuffer.Height - _barHeight;
            graphics.FillRectangle(0, yTop, Framebuffer.Width, _barHeight, 0xFF1A1A1A);
            graphics.FillRectangle(0, yTop, Framebuffer.Width, 1, 0xFF333333);

            bool timeChanged = RTC.Hour != _lastHour || RTC.Minute != _lastMinute;
            bool dateChanged = RTC.Day != _lastDay;

            if (_cachedTime == null || timeChanged) {
                if (_cachedTime != null) _cachedTime.Dispose();
                if (_clockUse12Hour) {
                    bool isPM = RTC.Hour >= 12;
                    int hour12 = RTC.Hour % 12 == 0 ? 12 : RTC.Hour % 12;
                    string suffix = isPM ? " PM" : " AM";
                    string minute = RTC.Minute < 10 ? "0" + RTC.Minute.ToString() : RTC.Minute.ToString();
                    string hour = hour12.ToString();
                    string temporary = hour + ":" + minute;
                    _cachedTime = temporary + suffix;
                    hour.Dispose();
                    minute.Dispose();
                    temporary.Dispose();
                } else {
                    string hour = RTC.Hour < 10 ? "0" + RTC.Hour.ToString() : RTC.Hour.ToString();
                    string minute = RTC.Minute < 10 ? "0" + RTC.Minute.ToString() : RTC.Minute.ToString();
                    _cachedTime = hour + ":" + minute;
                    hour.Dispose();
                    minute.Dispose();
                }
                _lastHour = RTC.Hour;
                _lastMinute = RTC.Minute;
            }

            if (_cachedDate == null || dateChanged) {
                if (_cachedDate != null) _cachedDate.Dispose();
                string month = RTC.Month.ToString();
                string day = RTC.Day.ToString();
                string year = RTC.Year.ToString();
                string first = month + "/";
                string second = first + day;
                string third = second + "/";
                _cachedDate = third + year;
                month.Dispose();
                day.Dispose();
                year.Dispose();
                first.Dispose();
                second.Dispose();
                third.Dispose();
                _lastDay = RTC.Day;
            }

            if (WindowManager.font != null && _cachedTime != null) {
                int timeWidth = WindowManager.font.MeasureString(_cachedTime);
                int timeX = Framebuffer.Width - 12 - timeWidth;
                int timeY = yTop + ((_barHeight - WindowManager.font.FontSize) / 2) -
                            (WindowManager.font.FontSize / 2);
                WindowManager.font.DrawString(timeX, timeY, _cachedTime);
                if (_cachedDate != null) {
                    WindowManager.font.DrawString(timeX, timeY + WindowManager.font.FontSize,
                                                _cachedDate);
                }
                Program.MarkUefiTaskbarTextRendered();
            }

            int startX = 12;
            int startY = yTop + 4;
            Image normalStartIcon = Icons.TaskbarIcon(32);
            int startWidth = normalStartIcon != null && normalStartIcon.Width > 0
                ? normalStartIcon.Width : _barHeight - 8;
            int startHeight = normalStartIcon != null && normalStartIcon.Height > 0
                ? normalStartIcon.Height : _barHeight - 8;
            bool startHovered = Control.MousePosition.X >= startX &&
                Control.MousePosition.X < startX + startWidth &&
                Control.MousePosition.Y >= startY &&
                Control.MousePosition.Y < startY + startHeight;
            bool startPressed = startHovered &&
                (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;

            graphics.FillRectangle(startX, startY, startWidth, startHeight,
                                   0xFF2E2E2E);
            graphics.DrawRectangle(startX, startY, startWidth, startHeight,
                                   0xFF3E3E3E, 1);
            Image startIcon = normalStartIcon;
            if (startPressed) {
                startIcon = Icons.TaskbarIconDown(32);
            } else if (startHovered) {
                startIcon = Icons.TaskbarIconOver(32);
            }
            if (startIcon != null && startIcon.RawData != null) {
                graphics.DrawImage(startX, startY, startIcon);
                if (!_uefiStartGraphicsReported) {
                    _uefiStartGraphicsReported = true;
                    BootConsole.WriteLine("[ICON_GRAPHICS] start-drawn=1;bounds=" +
                        startX.ToString() + "," + startY.ToString() + "," +
                        startWidth.ToString() + "x" + startHeight.ToString());
                }
            }

            int clockTextWidth = GetUefiTaskbarClockTextWidth();
            int taskbarContentRight = Framebuffer.Width - 12 - clockTextWidth;
            DrawUefiApplicationButtons(graphics, yTop, startX + startWidth,
                                       taskbarContentRight);
        }

        private void DrawUefiApplicationButtons(guideXOS.Graph.Graphics graphics,
                                                int yTop, int startRight,
                                                int contentRight) {
            int buttonHeight = _barHeight - 4;
            int buttonY = yTop + 2;
            int firstButtonX = startRight + UefiTaskbarStartAppGap;
            int rightLimit = contentRight - UefiTaskbarClockGap;
            int groups = TaskbarApplicationEntryRegistry.EntryCount;
            int buttons = 0;
            int icons = 0;
            int fallbacks = 0;
            int activeCount = 0;
            int hidden = 0;
            int invalid = 0;
            bool leftDown = (Control.MouseButtons & MouseButtons.Left) ==
                MouseButtons.Left;
            ApplicationInstanceHandle activeHandle =
                ApplicationInstanceRegistry.ActiveApplicationHandle;
            int visibleSlots = GetUefiTaskbarVisibleSlots(firstButtonX,
                rightLimit, buttonHeight);
            int activeOrdinal = GetUefiTaskbarActiveOrdinal(activeHandle);
            int entryOrdinal = 0;

            for (int i = 0; i < TaskbarApplicationEntryRegistry.Capacity; i++) {
                TaskbarApplicationEntry entry =
                    TaskbarApplicationEntryRegistry.GetAt(i);
                if (entry == null) continue;

                Window target = entry.GetFocusTarget();
                if (target == null || !entry.InstanceHandle.IsValid) {
                    invalid++;
                    continue;
                }

                int buttonOrdinal = GetUefiTaskbarButtonOrdinal(entryOrdinal,
                    activeOrdinal, visibleSlots);
                entryOrdinal++;
                if (buttonOrdinal < 0) {
                    hidden++;
                    continue;
                }

                int buttonX = firstButtonX +
                    buttonOrdinal * (UefiTaskbarAppButtonWidth +
                        UefiTaskbarAppButtonGap);
                int buttonRight = buttonX + UefiTaskbarAppButtonWidth;
                int buttonBottom = buttonY + buttonHeight;
                int mouseX = Control.MousePosition.X;
                int mouseY = Control.MousePosition.Y;
                bool hovered = mouseX >= buttonX && mouseX < buttonRight &&
                    mouseY >= buttonY && mouseY < buttonBottom;
                bool active = entry.InstanceHandle == activeHandle;
                if (active) activeCount++;

                uint fill = leftDown && hovered ? 0xFF262626 :
                    (active || hovered ? 0xFF3A3A3A : 0xFF303030);
                uint border = active ? 0xFF606060 :
                    (hovered ? 0xFF555555 : 0xFF454545);
                graphics.FillRectangle(buttonX, buttonY,
                    UefiTaskbarAppButtonWidth, buttonHeight, fill);
                graphics.DrawRectangle(buttonX, buttonY,
                    UefiTaskbarAppButtonWidth, buttonHeight, border, 1);

                Image icon = target.TaskbarIcon;
                if (!IsUefiTaskbarIconUsable(icon)) {
                    icon = Icons.DocumentIcon(UefiTaskbarIconSize);
                    fallbacks++;
                }

                int textX = buttonX + 6;
                if (IsUefiTaskbarIconUsable(icon)) {
                    int iconY = buttonY + (buttonHeight - icon.Height) / 2;
                    graphics.DrawImage(buttonX + 6, iconY, icon);
                    icons++;
                    textX += icon.Width + 6;
                }

                int textWidth = buttonRight - textX - 6;
                if (textWidth > 0 && WindowManager.font != null &&
                        WindowManager.font.FontSize > 0) {
                    int textY = buttonY + (buttonHeight -
                        WindowManager.font.FontSize) / 2;
                    WindowManager.font.DrawString(textX, textY,
                        entry.DisplayName ?? "", textWidth,
                        WindowManager.font.FontSize);
                }
                buttons++;
            }

            ReportUefiTaskbarVisual(groups, buttons, icons, fallbacks,
                activeCount, _taskbarVisualActivations, activeHandle.Value,
                hidden, invalid);
        }

        private int GetUefiTaskbarClockTextWidth() {
            int clockTextWidth = 0;
            if (WindowManager.font != null && _cachedTime != null) {
                clockTextWidth = WindowManager.font.MeasureString(_cachedTime);
                if (_cachedDate != null) {
                    int dateWidth = WindowManager.font.MeasureString(_cachedDate);
                    if (dateWidth > clockTextWidth) clockTextWidth = dateWidth;
                }
            }
            return clockTextWidth;
        }

        private int GetUefiTaskbarVisibleSlots(int firstButtonX, int rightLimit,
                                               int buttonHeight) {
            int visibleSlots = 0;
            int slotX = firstButtonX;
            while (buttonHeight > 0 && visibleSlots <
                    TaskbarApplicationEntryRegistry.Capacity &&
                    slotX + UefiTaskbarAppButtonWidth <= rightLimit) {
                visibleSlots++;
                slotX += UefiTaskbarAppButtonWidth + UefiTaskbarAppButtonGap;
            }
            return visibleSlots;
        }

        private static int GetUefiTaskbarActiveOrdinal(
                ApplicationInstanceHandle activeHandle) {
            int entryOrdinal = 0;
            for (int i = 0; i < TaskbarApplicationEntryRegistry.Capacity; i++) {
                TaskbarApplicationEntry entry =
                    TaskbarApplicationEntryRegistry.GetAt(i);
                if (entry == null || !entry.InstanceHandle.IsValid ||
                        entry.GetFocusTarget() == null) continue;
                if (entry.InstanceHandle == activeHandle) return entryOrdinal;
                entryOrdinal++;
            }
            return -1;
        }

        private static int GetUefiTaskbarButtonOrdinal(int entryOrdinal,
                                                       int activeOrdinal,
                                                       int visibleSlots) {
            if (visibleSlots <= 0) return -1;
            bool reserveLastSlotForActive = activeOrdinal >= visibleSlots;
            if (reserveLastSlotForActive &&
                    entryOrdinal == visibleSlots - 1) return -1;
            if (entryOrdinal >= visibleSlots &&
                    (!reserveLastSlotForActive || entryOrdinal != activeOrdinal)) {
                return -1;
            }
            if (reserveLastSlotForActive && entryOrdinal == activeOrdinal) {
                return visibleSlots - 1;
            }
            return entryOrdinal;
        }

        private static bool IsUefiTaskbarIconUsable(Image icon) {
            if (icon == null || icon.RawData == null || icon.Width <= 0 ||
                    icon.Height <= 0 || icon.Width > UefiTaskbarIconSize ||
                    icon.Height > UefiTaskbarIconSize) return false;
            return icon.RawData.Length >= icon.Width * icon.Height;
        }

        private void ReportUefiTaskbarVisual(int groups, int buttons, int icons,
                                             int fallbacks, int active,
                                             int activations, ulong activeHandle,
                                             int hidden, int invalid) {
            if (groups == _lastTaskbarVisualGroups &&
                    buttons == _lastTaskbarVisualButtons &&
                    icons == _lastTaskbarVisualIcons &&
                    fallbacks == _lastTaskbarVisualFallbacks &&
                    active == _lastTaskbarVisualActive &&
                    activations == _lastTaskbarVisualActivations &&
                    activeHandle == _lastTaskbarVisualActiveHandle &&
                    hidden == _lastTaskbarVisualHidden &&
                    invalid == _lastTaskbarVisualInvalid) return;

            _lastTaskbarVisualGroups = groups;
            _lastTaskbarVisualButtons = buttons;
            _lastTaskbarVisualIcons = icons;
            _lastTaskbarVisualFallbacks = fallbacks;
            _lastTaskbarVisualActive = active;
            _lastTaskbarVisualActivations = activations;
            _lastTaskbarVisualActiveHandle = activeHandle;
            _lastTaskbarVisualHidden = hidden;
            _lastTaskbarVisualInvalid = invalid;
            BootConsole.WriteLine("[TASKBAR_VISUAL] groups=" +
                groups.ToString() + ";buttons=" + buttons.ToString() +
                ";icons=" + icons.ToString() + ";fallback=" +
                fallbacks.ToString() + ";active=" + active.ToString() +
                ";activations=" + activations.ToString() +
                ";active-handle=" + activeHandle.ToString() + ";hidden=" +
                hidden.ToString() + ";invalid=" + invalid.ToString());
        }

        public void Draw() {
            DrawLegacy();
        }

        public void HandleUefiInput() {
            if (BootConsole.CurrentMode != BootMode.UEFI) return;

            bool leftDown = (Control.MouseButtons & MouseButtons.Left) ==
                MouseButtons.Left;
            bool rightDown = (Control.MouseButtons & MouseButtons.Right) ==
                MouseButtons.Right;
            if (!leftDown) {
                _oskClickLatch = false;
                _taskbarAppClickLatch = false;
            }

            int yTop = Framebuffer.Height - _barHeight;
            int mouseX = Control.MousePosition.X;
            int mouseY = Control.MousePosition.Y;
            bool rightPressed = guideXOS.Kernel.Drivers.Input.MouseEventDispatcher
                .WasPressedThisFrame(MouseButtons.Right);
            if ((rightDown || rightPressed) && mouseY >= yTop &&
                    mouseY < Framebuffer.Height) {
                if (!_rightClickLatch) {
                    Program.CloseWidgetContextMenu();
                    if (Program.RightMenu != null && Program.RightMenu.Visible) {
                        Program.RightMenu.Visible = false;
                    }
                    if (_menu == null) {
                        _menu = new TaskbarMenu(mouseX, mouseY);
                    } else {
                        _menu.Visible = true;
                        _menu.OnSetVisible(true);
                        WindowManager.MoveToEnd(_menu);
                    }
                    _rightClickLatch = true;
                    WindowManager.MouseHandled = true;
                    Program.MarkUefiTaskbarContextMenuOpened(_menu.X, _menu.Y,
                                                            _menu.Width, _menu.Height);
                    Program.MarkUefiWidgetTaskbarMenuOpened(_menu.X, _menu.Y,
                                                           _menu.Width, _menu.Height);
                }
                return;
            }
            if (!rightDown) _rightClickLatch = false;

            if (!leftDown) return;

            int startX = 12;
            int startY = yTop + 4;
            Image startIcon = Icons.TaskbarIcon(32);
            int startWidth = startIcon != null && startIcon.Width > 0
                ? startIcon.Width : _barHeight - 8;
            int startHeight = startIcon != null && startIcon.Height > 0
                ? startIcon.Height : _barHeight - 8;
            if (!_oskClickLatch && mouseX >= startX &&
                    mouseX < startX + startWidth && mouseY >= startY &&
                    mouseY < startY + startHeight) {
                ToggleUefiStartMenu();
                _oskClickLatch = true;
                WindowManager.MouseHandled = true;
                Program.MarkUefiGuiMouseRouted();
                return;
            }

            if (_taskbarAppClickLatch) return;

            int clockTextWidth = GetUefiTaskbarClockTextWidth();
            int contentRight = Framebuffer.Width - 12 - clockTextWidth;
            int firstButtonX = startX + startWidth + UefiTaskbarStartAppGap;
            int buttonY = yTop + 2;
            int buttonHeight = _barHeight - 4;
            int rightLimit = contentRight - UefiTaskbarClockGap;
            int visibleSlots = GetUefiTaskbarVisibleSlots(firstButtonX,
                rightLimit, buttonHeight);
            ApplicationInstanceHandle activeHandle =
                ApplicationInstanceRegistry.ActiveApplicationHandle;
            int activeOrdinal = GetUefiTaskbarActiveOrdinal(activeHandle);
            int entryOrdinal = 0;
            for (int i = 0; i < TaskbarApplicationEntryRegistry.Capacity; i++) {
                TaskbarApplicationEntry entry =
                    TaskbarApplicationEntryRegistry.GetAt(i);
                if (entry == null) continue;
                Window target = entry.GetFocusTarget();
                if (target == null || !entry.InstanceHandle.IsValid) continue;

                int buttonOrdinal = GetUefiTaskbarButtonOrdinal(entryOrdinal,
                    activeOrdinal, visibleSlots);
                entryOrdinal++;
                if (buttonOrdinal < 0) continue;

                int buttonX = firstButtonX + buttonOrdinal *
                    (UefiTaskbarAppButtonWidth + UefiTaskbarAppButtonGap);
                if (mouseX < buttonX || mouseX >= buttonX +
                        UefiTaskbarAppButtonWidth || mouseY < buttonY ||
                        mouseY >= buttonY + buttonHeight) continue;

                ApplicationLifecycleResult lifecycle = null;
                _taskbarAppClickLatch = true;
                if (TaskbarApplicationEntryRegistry.TryFocusWindow(
                        entry.InstanceHandle, null, out lifecycle)) {
                    if (_taskbarVisualActivations < int.MaxValue) {
                        _taskbarVisualActivations++;
                    }
                    WindowManager.MouseHandled = true;
                    Program.MarkUefiGuiMouseRouted();
                }
                return;
            }
        }

        private void ToggleUefiStartMenu() {
            if (Desktop.Apps != null) {
                if (StartMenu == null ||
                        WindowManager.Windows.IndexOf(StartMenu) < 0) {
                    StartMenu = new StartMenu();
                }
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                Program.MarkUefiAppRuntime("START_TOGGLE;registered=" +
                    (WindowManager.Windows.IndexOf(StartMenu) >= 0 ? "1" : "0") +
                    ";before=" + (StartMenu.Visible ? "1" : "0"));
#endif
                StartMenu.Visible = !StartMenu.Visible;
                if (StartMenu.Visible) Program.MarkUefiStartMenuOpened();
            } else {
                OpenOnScreenKeyboard();
            }
        }

        private void DrawLegacy() {
            switch (BootConsole.CurrentMode) {
                case BootMode.UEFI:
                    DrawUEFITaskBar();
                    break;
                case BootMode.Legacy:
                    TaskbarApplicationEntryRegistry.Reconcile();
                    // Handle delayed workspace switcher creation at the START of Draw()
                    // This ensures it's created OUTSIDE of any mouse button handling
                    if (_needsWorkspaceSwitcher) {
                        _needsWorkspaceSwitcher = false;

                        // Create the workspace switcher (now it's NOT a Window!)
                        if (_workspaceSwitcher == null) {
                            _workspaceSwitcher = new WorkspaceSwitcher();
                        }

                        // Build the cache BEFORE showing
                        _workspaceSwitcher.RefreshWindowCache();

                        // Make it visible
                        _workspaceSwitcher.Visible = true;
                    }

                    // Skip drawing the taskbar if workspace switcher is visible
                    if (_workspaceSwitcher != null && _workspaceSwitcher.Visible) {
                        return;
                    }

                    // Get mouse position for auto-hide logic
                    int mx = Control.MousePosition.X;
                    int my = Control.MousePosition.Y;

                    // Handle taskbar auto-hide logic
                    if (UISettings.EnableTaskbarAutoHide) {
                        // Check if mouse is near bottom edge (reveal threshold)
                        bool mouseNearBottom = my >= Framebuffer.Height - UISettings.TaskbarRevealThreshold;

                        // Check if mouse is over taskbar area (considering current offset)
                        int taskbarTop = Framebuffer.Height - _barHeight + _taskbarOffset;
                        bool mouseOverTaskbar = my >= taskbarTop;

                        // Update last activity time when mouse interacts with taskbar
                        if (mouseOverTaskbar || mouseNearBottom) {
                            _lastMouseActivity = Timer.Ticks;
                        }

                        // Determine if we should show or hide
                        ulong timeSinceActivity = Timer.Ticks >= _lastMouseActivity ? Timer.Ticks - _lastMouseActivity : 0;
                        bool shouldHide = timeSinceActivity > (ulong)UISettings.TaskbarAutoHideDelayMs && !mouseOverTaskbar && !mouseNearBottom;

                        // Start animation if state should change
                        if (shouldHide && !_taskbarHidden && !_isAnimating) {
                            // Start hide animation
                            StartAnimation(_taskbarOffset, _barHeight - 2); // Leave 2 pixels visible
                            _taskbarHidden = true;
                        } else if (!shouldHide && _taskbarHidden && !_isAnimating) {
                            // Start show animation
                            StartAnimation(_taskbarOffset, 0);
                            _taskbarHidden = false;
                        }

                        // Update animation
                        if (_isAnimating) {
                            UpdateAnimation();
                        }
                    } else {
                        // Auto-hide disabled - ensure taskbar is fully visible
                        _taskbarOffset = 0;
                        _taskbarHidden = false;
                        _isAnimating = false;
                    }

                    int yTop = Framebuffer.Height - _barHeight + _taskbarOffset;
                    // Blur area behind taskbar, then tint
                    Framebuffer.Graphics.BlurRectangle(0, yTop, Framebuffer.Width, _barHeight, 3);
                    Framebuffer.Graphics.AFillRectangle(0, yTop, Framebuffer.Width, _barHeight, 0x66111111);

                    // Mouse coordinates already obtained above for auto-hide
                    bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);

                    int startX = 12; int startY = yTop + 4;
                    // Start icon - determine which icon to show based on mouse state
                    if (_startIcon != null) {
                        int sW = _startIcon.Width; int sH = _startIcon.Height;
                        bool overStart = (mx >= startX && mx <= startX + sW && my >= startY && my <= startY + sH);

                        Image iconToShow = Icons.TaskbarIcon(32);
                        if (overStart && left) {
                            // Mouse is down over start button - show pressed state
                            iconToShow = Icons.TaskbarIconDown(32);
                        } else if (overStart) {
                            // Mouse is hovering over start button - show hover state
                            iconToShow = Icons.TaskbarIconOver(32);
                        }

                        Framebuffer.Graphics.DrawImage(startX, startY, iconToShow);
                    }

                    // Quicklaunch pinned row
                    int qx = startX + (_startIcon != null ? _startIcon.Width + 8 : 0) + 8;
                    int qy = yTop + 6;
                    int qh = _barHeight - 12;
                    bool leftMousePinned = Control.MouseButtons.HasFlag(MouseButtons.Left);
                    for (int i = 0; i < PinnedManager.Count; i++) {
                        var ic = PinnedManager.Icon(i);
                        int iw = ic.Width; int ih = ic.Height; int bx = qx; int by = qy + (qh / 2 - ih / 2);
                        // hover
                        bool hoverPinned = Control.MousePosition.X >= bx && Control.MousePosition.X <= bx + iw && Control.MousePosition.Y >= by && Control.MousePosition.Y <= by + ih;
                        if (hoverPinned) { UIPrimitives.AFillRoundedRect(bx - 3, by - 3, iw + 6, ih + 6, 0x333F7FBF, 4); }
                        Framebuffer.Graphics.DrawImage(bx, by, ic);
                        // edge-click to launch pinned item
                        if (hoverPinned && leftMousePinned && !_pinnedClickLatch) {
                            _pinnedClickLatch = true;
                            string nm = PinnedManager.Name(i); byte kind = PinnedManager.Kind(i);
                            if (kind == 0) { Desktop.LaunchApplication(nm); } else if (kind == 2) { Desktop.LaunchApplication("Computer Files"); } else if (kind == 1) { string path = PinnedManager.Path(i); if (path != null) { Desktop.LaunchTypedExternalGxmFile(path, "gxos.shell.taskbar"); } }
                        }
                        qx += iw + 8; if (qx > Framebuffer.Width - 420) break; // leave space for task buttons
                    }
                    if (!leftMousePinned) _pinnedClickLatch = false;

                    // Draw task buttons after pinned
                    int btnX = qx + 12;
                    int btnY = yTop + 6;
                    int btnH = _barHeight - 12;
                    int btnW = 140; // fixed width
                    int gap = 8;

                    bool right = Control.MouseButtons.HasFlag(MouseButtons.Right);

                    // Handle right click -> show menu and mark mouse as handled
                    int barTop = yTop;
                    bool onBar = (my >= barTop && my <= Framebuffer.Height);
                    if (right && onBar) {
                        if (!_rightClickLatch) {
                            if (_menu == null) _menu = new TaskbarMenu(mx, my);
                            else { _menu.Visible = true; _menu.OnSetVisible(true); }
                            _rightClickLatch = true;
                            // Mark mouse as handled to prevent desktop context menu from also appearing
                            WindowManager.MouseHandled = true;
                        }
                    } else {
                        _rightClickLatch = false;
                    }

                    for (int i = 0; i < WindowManager.Windows.Count; i++) {
                        var w = WindowManager.Windows[i];
                        if (!w.Visible || !w.ShowInTaskbar) continue;
                        TaskbarApplicationEntry entry;
                        bool attached = w.ApplicationInstanceHandle.IsValid;
                        bool semantic = TryResolveSemanticWindow(w, out entry);
                        if (!semantic && attached) continue;
                        // button rect
                        int x = btnX; int y = btnY; int wRect = btnW; int hRect = btnH;
                        bool hover = (mx >= x && mx <= x + wRect && my >= y && my <= y + hRect);
                        bool active = semantic && entry.IsActive;
                        uint bg = hover || active ? 0xFF3A3A3A : 0xFF303030;
                        Framebuffer.Graphics.FillRectangle(x, y, wRect, hRect, bg);
                        Framebuffer.Graphics.DrawRectangle(x, y, wRect, hRect,
                            active ? 0xFF555555 : 0xFF454545, 1);
                        // icon and title
                        var icon = w.TaskbarIcon ?? Icons.DocumentIcon(32);
                        int iconY = y + (hRect / 2) - (icon.Height / 2);
                        Framebuffer.Graphics.DrawImage(x + 6, iconY, icon);
                        int textX = x + 6 + icon.Width + 6;
                        int textWidth = wRect - (textX - x) - 6;
                        if (textWidth > 0) WindowManager.font.DrawString(textX, y + (hRect / 2) - (WindowManager.font.FontSize / 2), w.Title, textWidth, WindowManager.font.FontSize);
                        // click -> focus window
                        if (left && hover) {
                            if (semantic) {
                                ApplicationLifecycleResult lifecycle =
                                    null;
                                if (!TaskbarApplicationEntryRegistry.TryFocusWindow(
                                        entry.InstanceHandle, w, out lifecycle)) {
                                    btnX += wRect + gap;
                                    continue;
                                }
                            }
                            if (w.IsMinimized) w.Restore();
                            WindowManager.MoveToEnd(w);
                            w.Visible = true;
                        }
                        btnX += wRect + gap;
                        if (btnX > Framebuffer.Width - 300) break; // leave space for clock + right controls
                    }

                    // FIXED: Only regenerate time/date strings when the time actually changes
                    // This reduces allocations from 60/sec to 1/sec (when minute changes) - 98% reduction!
                    bool timeChanged = (RTC.Hour != _lastHour || RTC.Minute != _lastMinute);
                    bool dateChanged = (RTC.Day != _lastDay);

                    if (_cachedTime == null || timeChanged) {
                        // Dispose old cached time
                        if (_cachedTime != null) {
                            _cachedTime.Dispose();
                        }

                        // Generate new time string (WITHOUT seconds to reduce update frequency)
                        if (_clockUse12Hour) {
                            bool isPM = RTC.Hour >= 12;
                            int hour12 = (RTC.Hour % 12 == 0) ? 12 : (RTC.Hour % 12);
                            string sfx = isPM ? " PM" : " AM";
                            string min = RTC.Minute < 10 ? ("0" + RTC.Minute.ToString()) : RTC.Minute.ToString();
                            string h12 = hour12.ToString();
                            string temp = h12 + ":" + min;
                            _cachedTime = temp + sfx;
                            h12.Dispose();
                            min.Dispose();
                            temp.Dispose();
                        } else {
                            string h = RTC.Hour < 10 ? ("0" + RTC.Hour.ToString()) : RTC.Hour.ToString();
                            string m = RTC.Minute < 10 ? ("0" + RTC.Minute.ToString()) : RTC.Minute.ToString();
                            _cachedTime = h + ":" + m;
                            h.Dispose();
                            m.Dispose();
                        }

                        _lastHour = RTC.Hour;
                        _lastMinute = RTC.Minute;
                    }

                    if (_cachedDate == null || dateChanged) {
                        // Dispose old cached date
                        if (_cachedDate != null) {
                            _cachedDate.Dispose();
                        }

                        // Generate new date string with full year
                        string monthStr = RTC.Month.ToString();
                        string dayStr = RTC.Day.ToString();
                        string yearStr = RTC.Year.ToString();
                        string temp1 = monthStr + "/";
                        string temp2 = temp1 + dayStr;
                        string temp3 = temp2 + "/";
                        _cachedDate = temp3 + yearStr;
                        monthStr.Dispose();
                        dayStr.Dispose();
                        yearStr.Dispose();
                        temp1.Dispose();
                        temp2.Dispose();
                        temp3.Dispose();

                        _lastDay = RTC.Day;
                    }

                    int timeW = WindowManager.font.MeasureString(_cachedTime);
                    int timeX = Framebuffer.Width - 12 - timeW;
                    int timeY = yTop + ((_barHeight - WindowManager.font.FontSize) / 2) - (WindowManager.font.FontSize / 2);
                    WindowManager.font.DrawString(timeX, timeY, _cachedTime);
                    // Date below time
                    int dateY = timeY + WindowManager.font.FontSize;
                    WindowManager.font.DrawString(timeX, dateY, _cachedDate);
                    Program.MarkUefiTaskbarTextRendered();

                    // LiveMode indicator (left of network indicator)
                    int liveModeWidth = 0;
                    if (guideXOS.OS.SystemMode.IsLiveMode) {
                        string liveText = "LIVE";
                        liveModeWidth = WindowManager.font.MeasureString(liveText) + 12;
                        int liveX = timeX - liveModeWidth - 8;
                        int liveY = timeY + (WindowManager.font.FontSize / 2) - 6;
                        int liveH = 14;

                        // Draw live mode badge
                        Framebuffer.Graphics.FillRectangle(liveX, liveY, liveModeWidth - 4, liveH, 0xFFFF6B35); // Orange background
                        Framebuffer.Graphics.DrawRectangle(liveX, liveY, liveModeWidth - 4, liveH, 0xFFFFAA00, 1); // Brighter orange border

                        // Draw text centered
                        int textX = liveX + ((liveModeWidth - 4) / 2) - (WindowManager.font.MeasureString(liveText) / 2);
                        int textY = liveY + (liveH / 2) - (WindowManager.font.FontSize / 2);
                        WindowManager.font.DrawString(textX, textY, liveText);
                    }

                    // Network indicator left of time (or left of LiveMode if present)
                    int iconSize = 14;
                    int netX = timeX - iconSize - 8 - liveModeWidth;
                    int netY = timeY + (WindowManager.font.FontSize / 2) - (iconSize / 2);

                    // Simple animation clock
                    if (_lastTick != Timer.Ticks) { _lastTick = Timer.Ticks; _netAnimPhase = (_netAnimPhase + 1) % 3; }

                    bool connected = false;
#if NETWORK
                    connected = (NETv4.IP.P1 != 0 || NETv4.IP.P2 != 0 || NETv4.IP.P3 != 0 || NETv4.IP.P4 != 0); // Check if IP is configured
#else
            connected = false;
#endif
                    ulong now = Timer.Ticks;

                    if (connected) {
                        // draw 3 bars - only animate when network is connected (implies activity)
                        int bw = 3; int gap2 = 2;
                        for (int i = 0; i < 3; i++) {
                            int h2 = 4 + i * 4;
                            Framebuffer.Graphics.FillRectangle(netX + i * (bw + gap2), netY + (iconSize - h2), bw, h2, 0xFF5FB878);
                        }
                        _netConnectedShown = true;
                    } else {
                        // show animated dots only during the allowed window; otherwise static dim dots
                        if (now >= _nextCycleStart) {
                            // start a new 10s animation window, then schedule next in 5 minutes
                            _animWindowStart = now;
                            _animWindowEnd = now + TenSeconds;
                            _nextCycleStart = now + FiveMinutes;
                        }
                        bool animActive = (now >= _animWindowStart && now <= _animWindowEnd);

                        int dot = 3; int gap2 = 4;
                        for (int i = 0; i < 3; i++) {
                            uint c;
                            if (animActive) {
                                c = (i == _netAnimPhase) ? 0xFFAAAAAAu : 0xFF555555u;
                            } else {
                                c = 0xFF555555u; // static dim dots when idle
                            }
                            Framebuffer.Graphics.FillRectangle(netX + i * (dot + gap2), netY + (iconSize / 2) - (dot / 2), dot, dot, c);
                        }
                    }

                    // On-Screen Keyboard button (left of network indicator)
                    int oskSize = _barHeight - 12; if (oskSize < 18) oskSize = 18; if (oskSize > 24) oskSize = 24;
                    int oskX = netX - oskSize - 10;
                    int oskY = yTop + (_barHeight - oskSize) / 2;
                    bool overOSK = (mx >= oskX && mx <= oskX + oskSize && my >= oskY && my <= oskY + oskSize);
                    uint oskBg = overOSK ? 0xFF3A3A3A : 0xFF303030;
                    Framebuffer.Graphics.FillRectangle(oskX, oskY, oskSize, oskSize, oskBg);
                    Framebuffer.Graphics.DrawRectangle(oskX, oskY, oskSize, oskSize, 0xFF454545, 1);
                    // draw keyboard glyph (simple representation)
                    int kbPad = 4;
                    Framebuffer.Graphics.FillRectangle(oskX + kbPad, oskY + kbPad, oskSize - kbPad * 2, 2, 0xFFAAAAAA);
                    Framebuffer.Graphics.FillRectangle(oskX + kbPad, oskY + kbPad + 4, oskSize - kbPad * 2, 2, 0xFFAAAAAA);
                    Framebuffer.Graphics.FillRectangle(oskX + kbPad, oskY + kbPad + 8, oskSize - kbPad * 2, 2, 0xFFAAAAAA);
                    Framebuffer.Graphics.FillRectangle(oskX + kbPad + 2, oskY + oskSize - kbPad - 4, oskSize - kbPad * 2 - 4, 3, 0xFFAAAAAA);

                    // Workspace Switcher button (left of OSK)
                    int tvSize = _barHeight - 12; if (tvSize < 18) tvSize = 18; if (tvSize > 24) tvSize = 24;
                    int tvX = oskX - tvSize - 10;
                    int tvY = yTop + (_barHeight - tvSize) / 2;
                    bool overTV = (mx >= tvX && mx <= tvX + tvSize && my >= tvY && my <= tvY + tvSize);
                    uint tvBg = overTV ? 0xFF3A3A3A : 0xFF303030;
                    Framebuffer.Graphics.FillRectangle(tvX, tvY, tvSize, tvSize, tvBg);
                    Framebuffer.Graphics.DrawRectangle(tvX, tvY, tvSize, tvSize, 0xFF454545, 1);
                    // draw workspace glyph (stacked rectangles)
                    int sq = tvSize / 2;
                    Framebuffer.Graphics.DrawRectangle(tvX + 5, tvY + 4, sq, sq, 0xFFAAAAAA, 1);
                    Framebuffer.Graphics.DrawRectangle(tvX + 8, tvY + 7, sq, sq, 0xFF888888, 1);
                    Framebuffer.Graphics.DrawRectangle(tvX + 11, tvY + 10, sq, sq, 0xFF666666, 1);

                    // Show Desktop sliver at far right
                    int sdW = 6; int sdX = Framebuffer.Width - sdW - 1; int sdY = yTop + 2; int sdH = _barHeight - 4;
                    // bevel effect
                    Framebuffer.Graphics.FillRectangle(sdX, sdY, sdW, sdH, 0x33222222); // subtle fill
                    Framebuffer.Graphics.DrawRectangle(sdX, sdY, sdW, sdH, 0xFF444444, 1);
                    Framebuffer.Graphics.DrawRectangle(sdX + 1, sdY + 1, sdW - 2, sdH - 2, 0xFF777777, 1);

                    // Input handling for start/time areas
                    if (Control.MouseButtons.HasFlag(MouseButtons.Left)) {
                        int mx2 = Control.MousePosition.X; int my2 = Control.MousePosition.Y;
                        if (mx2 >= timeX && mx2 <= timeX + timeW && my2 >= yTop && my2 <= yTop + _barHeight) {
                            if (!_clockClickLatch) { _clockUse12Hour = !_clockUse12Hour; _clockClickLatch = true; }
                        }
                        if (_startIcon != null) {
                            int sW = _startIcon.Width; int sH = _startIcon.Height;
                            if (mx2 >= startX && mx2 <= startX + sW && my2 >= startY && my2 <= startY + sH) {
                                if (!_startClickLatch) { if (StartMenu == null) StartMenu = new StartMenu(); StartMenu.Visible = !StartMenu.Visible; _startClickLatch = true; }
                            } else {
                                // only close on a new click (mouse down that didn't originate from Start button)
                                if (StartMenu != null && StartMenu.Visible && !_startClickLatch && !StartMenu.IsUnderMouse()) { StartMenu.Visible = false; }
                            }
                        }
                        // On-Screen Keyboard button click
                        if (mx2 >= oskX && mx2 <= oskX + oskSize && my2 >= oskY && my2 <= oskY + oskSize) {
                            if (!_oskClickLatch) {
                                OpenOnScreenKeyboard();
                                _oskClickLatch = true;
                            }
                        }
                        // Workspace button click: SET FLAG instead of creating directly
                        if (mx2 >= tvX && mx2 <= tvX + tvSize && my2 >= tvY && my2 <= tvY + tvSize) {
                            if (!_taskViewLatch) {
                                // Set flag to create workspace switcher on next Draw() cycle
                                _needsWorkspaceSwitcher = true;
                                _taskViewLatch = true;
                            }
                        }
                        // Show Desktop click handling (right sliver)
                        if (mx2 >= sdX && mx2 <= sdX + sdW && my2 >= sdY && my2 <= sdY + sdH) {
                            if (!_showDesktopLatch) {
                                ToggleShowDesktop();
                                _showDesktopLatch = true;
                            }
                        }
                    } else {
                        _clockClickLatch = false;
                        _startClickLatch = false;
                        _taskViewLatch = false;
                        _showDesktopLatch = false;
                        _oskClickLatch = false;
                    }

                    // FIXED: No need to dispose cached strings - they're reused every frame and only disposed when regenerated
                    break;
            }
        }

        private void ToggleShowDesktop() {
            if (!_desktopShown) {
                _minimizedByShowDesktop.Clear();
                // minimize all visible taskbar windows
                for (int i = 0; i < WindowManager.Windows.Count; i++) {
                    var w = WindowManager.Windows[i];
                    if (!w.ShowInTaskbar) continue; // skip non-taskbar elements like Start Menu
                    if (w.Visible && !w.IsMinimized) {
                        _minimizedByShowDesktop.Add(w);
                        w.Minimize();
                    }
                }
                // Hide Start menu if open
                if (StartMenu != null && StartMenu.Visible) StartMenu.Visible = false;
                _desktopShown = true;
            } else {
                // restore only those we minimized
                for (int i = 0; i < _minimizedByShowDesktop.Count; i++) {
                    var w = _minimizedByShowDesktop[i];
                    if (w != null && w.IsMinimized) w.Restore();
                }
                _minimizedByShowDesktop.Clear();
                _desktopShown = false;
            }
        }

        public void OpenOnScreenKeyboard() {
            // Check if OSK is already open
            for (int i = 0; i < WindowManager.Windows.Count; i++) {
                if (WindowManager.Windows[i] is OnScreenKeyboard osk) {
                    if (!osk.Visible) {
                        osk.Visible = true;
                        WindowManager.MoveToEnd(osk);
                    }
                    return;
                }
            }
            
            // Create new OSK window at bottom center of screen
            int oskW = 800;
            int oskH = 280;
            int oskX = (Framebuffer.Width - oskW) / 2;
            int oskY = Framebuffer.Height - _barHeight - oskH - 10;
            
            var keyboard = new OnScreenKeyboard(oskX, oskY);
            WindowManager.MoveToEnd(keyboard);
            keyboard.Visible = true;
        }
        
        private void StartAnimation(int fromOffset, int toOffset) {
            // Check if animations are disabled
            bool canSlide = (toOffset == 0 && UISettings.EnableTaskbarSlideDown) || 
                           (toOffset > 0 && UISettings.EnableTaskbarSlideUp);
            
            if (!canSlide) {
                // Instant change without animation
                _taskbarOffset = toOffset;
                return;
            }
            
            _isAnimating = true;
            _animationStartOffset = fromOffset;
            _animationTargetOffset = toOffset;
            _animationStartTick = Timer.Ticks;
        }
        
        private void UpdateAnimation() {
            if (!_isAnimating) return;
            
            ulong elapsed = Timer.Ticks >= _animationStartTick ? Timer.Ticks - _animationStartTick : 0;
            
            if (elapsed >= (ulong)UISettings.TaskbarSlideDurationMs) {
                // Animation complete
                _taskbarOffset = _animationTargetOffset;
                _isAnimating = false;
            } else {
                // Calculate interpolated position
                float t = (float)elapsed / UISettings.TaskbarSlideDurationMs;
                // Ease out cubic for smooth deceleration
                t = 1.0f - (1.0f - t) * (1.0f - t) * (1.0f - t);
                
                int delta = _animationTargetOffset - _animationStartOffset;
                _taskbarOffset = _animationStartOffset + (int)(delta * t);
            }
        }
    }
}
