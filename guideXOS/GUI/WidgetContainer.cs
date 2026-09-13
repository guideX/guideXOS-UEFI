using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Drivers.Input;
using System.Collections.Generic;
using System.Windows.Forms;

namespace guideXOS.GUI {
    /// <summary>
    /// Container window that holds multiple docked widgets
    /// </summary>
    internal class WidgetContainer : Window {
        private List<DockableWidget> _widgets;
        private const int Padding = 8;
        private const int CloseBtnSize = 16;
        private const int WidgetGap = 10;
        
        // Auto-hide state
        private bool _autoHidden = false;
        private long _lastRevealCheckMs = 0;
        private long _lastHideRequestMs = 0;
        
        private bool _dragging = false;
        private int _dragOffsetX, _dragOffsetY;
        private bool _closeHover = false;
        private int _hoverWidgetIndex = -1; // Track which widget is being hovered for undocking
        private bool _rightClickLatch;
        
        public WidgetContainer(int x, int y) : base(x, y, 140, 90) {
            Title = "Widgets";
            BarHeight = 0;
            ShowInTaskbar = false;
            ShowMaximize = false;
            ShowMinimize = false;
            ShowTombstone = false;
            IsResizable = false;
            
            _widgets = new List<DockableWidget>();
            
            // Initialize auto-hidden state based on settings
            if (UISettings.EnableAutoHideWidgets || UISettings.EnableAutoHideWidgetsVisuals) {
                _autoHidden = true;
                Visible = false;
            }
        }
        
        public void AddWidget(DockableWidget widget) {
            // Check if widget is already in the list
            bool alreadyContains = false;
            for (int i = 0; i < _widgets.Count; i++) {
                if (_widgets[i] == widget) {
                    alreadyContains = true;
                    break;
                }
            }
            
            if (!alreadyContains) {
                _widgets.Add(widget);
                widget.DockedContainer = this;
                UpdateLayout();
            }
        }

        /// <summary>
        /// Show the persistent widget surface without exposing its docked
        /// children as independent WindowManager draw/input owners.
        /// </summary>
        public void ShowWidgets() {
            _autoHidden = false;
            _lastRevealCheckMs = (long)Timer.Ticks;
            Visible = true;
            WindowManager.MoveToEnd(this);
        }
        
        public void RemoveWidget(DockableWidget widget) {
            if (_widgets.Remove(widget)) {
                widget.DockedContainer = null;
                UpdateLayout();
                
                // If only one widget left, undock it
                if (_widgets.Count == 1) {
                    var remaining = _widgets[0];
                    remaining.DockedContainer = null;
                    remaining.Visible = true;
                    remaining.X = X;
                    remaining.Y = Y;
                    _widgets.Clear();
                    Visible = false;
                }
                // If no widgets left, hide container
                else if (_widgets.Count == 0) {
                    Visible = false;
                }
            }
        }
        
        /// <summary>
        /// Undock a widget and position it at the specified location (used by context menu)
        /// </summary>
        public void UndockWidgetToPosition(DockableWidget widget, int mouseX, int mouseY) {
            // Remove from container
            RemoveWidget(widget);
            
            // Make widget visible and position at mouse
            widget.Visible = true;
            widget.X = mouseX - widget.Width / 2;
            widget.Y = mouseY - 10;
            
            // Clamp to screen
            if (widget.X < 0) widget.X = 0;
            if (widget.Y < 0) widget.Y = 0;
            if (widget.X + widget.Width > Framebuffer.Width) widget.X = Framebuffer.Width - widget.Width;
            if (widget.Y + widget.Height > Framebuffer.Height) widget.Y = Framebuffer.Height - widget.Height;
            
            WindowManager.MoveToEnd(widget);
        }
        
        private void UpdateLayout() {
            if (_widgets == null || _widgets.Count == 0) {
                return;
            }
            
            // Calculate total height needed
            int totalHeight = Padding;
            int maxWidth = 140;
            
            for (int i = 0; i < _widgets.Count; i++) {
                var w = _widgets[i];
                if (w == null) continue;
                totalHeight += w.PreferredHeight;
                if (i < _widgets.Count - 1) {
                    totalHeight += WidgetGap;
                }
                if (w.Width > maxWidth) {
                    maxWidth = w.Width;
                }
            }
            totalHeight += Padding;
            
            // Update container size
            Width = maxWidth;
            Height = totalHeight;

            // Layout can make the container larger than the constructor's
            // initial bounds. Keep the actual draw surface inside the current
            // framebuffer before the next frame.
            int maxX = Framebuffer.Width - Width;
            int maxY = Framebuffer.Height - Height;
            if (maxX < 0) maxX = 0;
            if (maxY < 0) maxY = 0;
            if (X < 0) X = 0;
            if (Y < 0) Y = 0;
            if (X > maxX) X = maxX;
            if (Y > maxY) Y = maxY;
        }
        
        public override void OnInput() {
            // Auto-hide widgets behavior - ALWAYS check mouse position even when hidden
            if (UISettings.EnableAutoHideWidgets || UISettings.EnableAutoHideWidgetsVisuals) {
                int mx2 = Control.MousePosition.X;
                int my2 = Control.MousePosition.Y;
                int threshold = UISettings.AutoHideWidgetsRevealThresholdPx;
                
                // Calculate exclusion zones (15% from top and bottom)
                int screenHeight = Framebuffer.Height;
                int topExclusionZone = (int)(screenHeight * 0.15f);
                int bottomExclusionZone = screenHeight - (int)(screenHeight * 0.15f);
                
                // Check if mouse is at right edge AND within the middle 70% of screen height
                bool atRightEdge = mx2 >= (Framebuffer.Width - threshold);
                bool inValidVerticalZone = my2 >= topExclusionZone && my2 <= bottomExclusionZone;
                bool shouldReveal = atRightEdge && inValidVerticalZone;

                long nowMs = (long)guideXOS.Kernel.Drivers.Timer.Ticks;

                if (shouldReveal) {
                    // Reveal widgets when mouse is at the far right edge in valid zone
                    if (_autoHidden || !Visible) {
                        RevealWidgets();
                    }
                    _lastRevealCheckMs = nowMs;
                } else {
                    // If mouse left reveal area, schedule hide
                    if (!_autoHidden && Visible) {
                        _lastHideRequestMs = nowMs;
                        int delay = UISettings.AutoHideWidgetsHideDelayMs;
                        if (nowMs - _lastRevealCheckMs >= delay) {
                            HideWidgetsAuto();
                        }
                    }
                }

                // When auto-hidden, skip remaining input processing to avoid resource usage
                if (_autoHidden || !Visible) {
                    return;
                }
            } else {
                if (!Visible) return;
            }
            
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            bool rightClick = Control.MouseButtons.HasFlag(MouseButtons.Right);
            bool leftPressed = MouseEventDispatcher.WasPressedThisFrame(MouseButtons.Left);
            bool rightPressed = MouseEventDispatcher.WasPressedThisFrame(MouseButtons.Right);
            bool leftActive = leftDown || leftPressed;
            bool rightActive = rightClick || rightPressed;

            if (!rightClick && !rightPressed) _rightClickLatch = false;
            
            // Close button hit test
            int closeX = X + Width - Padding - CloseBtnSize;
            int closeY = Y + Padding;
            _closeHover = (mx >= closeX && mx <= closeX + CloseBtnSize && 
                          my >= closeY && my <= closeY + CloseBtnSize);
            
            // Update hover widget index
            int previousHover = _hoverWidgetIndex;
            _hoverWidgetIndex = -1;
            if (!leftActive && _widgets.Count > 1) {
                int currentY = Y + Padding;
                for (int i = 0; i < _widgets.Count; i++) {
                    var widget = _widgets[i];
                    int widgetHeight = widget.PreferredHeight;
                    
                    if (mx >= X && mx <= X + Width && 
                        my >= currentY && my <= currentY + widgetHeight) {
                        _hoverWidgetIndex = i;
                        break;
                    }
                    
                    currentY += widgetHeight;
                    if (i < _widgets.Count - 1) {
                        currentY += WidgetGap;
                    }
                }
            }
            if (_hoverWidgetIndex != previousHover) {
                Program.MarkUefiWidgetHover(_hoverWidgetIndex);
            }
            
            // Handle right-click for context menu on widgets
            if (rightActive && !_rightClickLatch && !_dragging) {
                int currentY = Y + Padding;
                for (int i = 0; i < _widgets.Count; i++) {
                    var widget = _widgets[i];
                    int widgetHeight = widget.PreferredHeight;
                    
                    if (mx >= X && mx <= X + Width && 
                        my >= currentY && my <= currentY + widgetHeight) {
                        // Show context menu for this docked widget
                        if (Program.widgetContextMenu != null) {
                            _rightClickLatch = true;
                            Program.MarkUefiWidgetInput("RIGHT");
                            Program.widgetContextMenu.ShowForDockedWidget(widget, this, mx, my);
                        }
                        return;
                    }
                    
                    currentY += widgetHeight;
                    if (i < _widgets.Count - 1) {
                        currentY += WidgetGap;
                    }
                }
            }
            
            if (leftActive) {
                // Check if clicking close button
                if (_closeHover) {
                    Program.MarkUefiWidgetInput("LEFT");
                    Program.CloseWidgetContextMenu();
                    // Close all widgets
                    for (int i = 0; i < _widgets.Count; i++) {
                        _widgets[i].Visible = false;
                        _widgets[i].DockedContainer = null;
                    }
                    _widgets.Clear();
                    Visible = false;
                    return;
                }
                
                // Check if clicking on a specific widget to undock it
                if (!_dragging && _widgets.Count > 1 && _hoverWidgetIndex >= 0) {
                    Program.MarkUefiWidgetInput("LEFT");
                    UndockWidget(_widgets[_hoverWidgetIndex], mx, my);
                    return;
                }
                
                // Start dragging container
                if (!_dragging && mx >= X && mx <= X + Width && my >= Y && my <= Y + Height) {
                    _dragging = true;
                    Program.MarkUefiWidgetInput("DRAG_START");
                    _dragOffsetX = mx - X;
                    _dragOffsetY = my - Y;
                }
                
                // Handle dragging
                if (_dragging) {
                    X = mx - _dragOffsetX;
                    Y = my - _dragOffsetY;
                    
                    // Clamp to screen bounds
                    if (X < 0) X = 0;
                    if (Y < 0) Y = 0;
                    if (X + Width > Framebuffer.Width) X = Framebuffer.Width - Width;
                    if (Y + Height > Framebuffer.Height) Y = Framebuffer.Height - Height;
                }
            } else {
                _dragging = false;
            }
        }
        
        private void UndockWidget(DockableWidget widget, int mouseX, int mouseY) {
            // Remove from container
            RemoveWidget(widget);
            
            // Make widget visible and position at mouse
            widget.Visible = true;
            widget.X = mouseX - widget.Width / 2;
            widget.Y = mouseY - 10; // Slight offset from mouse
            
            // Clamp to screen
            if (widget.X < 0) widget.X = 0;
            if (widget.Y < 0) widget.Y = 0;
            if (widget.X + widget.Width > Framebuffer.Width) widget.X = Framebuffer.Width - widget.Width;
            if (widget.Y + widget.Height > Framebuffer.Height) widget.Y = Framebuffer.Height - widget.Height;
            
            WindowManager.MoveToEnd(widget);
        }
        
        public override void OnDraw() {
            // Skip rendering when not visible or no widgets
            // Note: _autoHidden is already handled by Visible flag
            if (!Visible || _widgets == null || _widgets.Count == 0) return;
            
            // Draw container background with subtle glow
            UIPrimitives.AFillRoundedRect(X - 2, Y - 2, Width + 4, Height + 4, 0x331E90FF, 8);
            UIPrimitives.AFillRoundedRect(X, Y, Width, Height, 0xDD1A1A1A, 6);
            
            // Draw border
            UIPrimitives.DrawRoundedRect(X, Y, Width, Height, 0xFF3A3A3A, 1, 6);
            
            // Draw each widget's content
            int currentY = Y + Padding;
            for (int i = 0; i < _widgets.Count; i++) {
                var widget = _widgets[i];
                if (widget == null) continue;
                int contentX = X + Padding;
                int contentWidth = Width - Padding * 2;
                int widgetHeight = widget.PreferredHeight;
                
                // Highlight widget if hovering (only when multiple widgets and not dragging)
                if (_hoverWidgetIndex == i && _widgets.Count > 1 && !_dragging) {
                    // Draw subtle highlight background
                    UIPrimitives.AFillRoundedRect(
                        X + 2, 
                        currentY - 2, 
                        Width - 4, 
                        widgetHeight + 4, 
                        0x333F7FBF, 
                        4
                    );
                }
                
                try {
                    widget.DrawContent(contentX, currentY, contentWidth);
                    if (widget is guideXOS.DockableWidgets.PerformanceWidget) {
                        Program.MarkUefiWidgetDrawn("PerformanceWidget", contentX, currentY,
                            contentWidth, widgetHeight);
                    } else if (widget is guideXOS.DockableWidgets.Clock) {
                        Program.MarkUefiWidgetDrawn("Clock", contentX, currentY,
                            contentWidth, widgetHeight);
                    } else if (widget is guideXOS.DockableWidgets.Monitor) {
                        Program.MarkUefiWidgetDrawn("Monitor", contentX, currentY,
                            contentWidth, widgetHeight);
                    } else if (widget is guideXOS.DockableWidgets.Uptime) {
                        Program.MarkUefiWidgetDrawn("Uptime", contentX, currentY,
                            contentWidth, widgetHeight);
                    }
                } catch {
                    // One widget must not prevent the remaining docked
                    // widgets or desktop windows from drawing.
                    Program.MarkUefiWidgetRuntimeFault("DRAW", "WIDGET");
                }
                
                currentY += widgetHeight;
                
                // Draw separator line between widgets
                if (i < _widgets.Count - 1) {
                    int lineY = currentY + WidgetGap / 2;
                    Framebuffer.Graphics.DrawLine(X + Padding, lineY, X + Width - Padding, lineY, 0xFF3A3A3A);
                    currentY += WidgetGap;
                }
            }
            
            // Draw close button
            DrawCloseButton();
        }
        
        private void DrawCloseButton() {
            int closeX = X + Width - Padding - CloseBtnSize;
            int closeY = Y + Padding;
            
            // Button background
            uint btnColor = _closeHover ? 0xFFFF5555 : 0xFF883333;
            UIPrimitives.AFillRoundedRect(closeX, closeY, CloseBtnSize, CloseBtnSize, btnColor, 3);
            
            // X symbol
            uint xColor = _closeHover ? 0xFFFFFFFF : 0xFFCCCCCC;
            int pad = 4;
            
            // Draw X with 2 lines
            Framebuffer.Graphics.DrawLine(closeX + pad, closeY + pad, 
                                         closeX + CloseBtnSize - pad, closeY + CloseBtnSize - pad, xColor);
            Framebuffer.Graphics.DrawLine(closeX + CloseBtnSize - pad, closeY + pad, 
                                         closeX + pad, closeY + CloseBtnSize - pad, xColor);
            
            // Draw again shifted by 1px for thickness
            Framebuffer.Graphics.DrawLine(closeX + pad + 1, closeY + pad, 
                                         closeX + CloseBtnSize - pad + 1, closeY + CloseBtnSize - pad, xColor);
            Framebuffer.Graphics.DrawLine(closeX + CloseBtnSize - pad, closeY + pad + 1, 
                                         closeX + pad, closeY + CloseBtnSize - pad + 1, xColor);
        }
        
        // Do not dispose the internal list; widgets are managed elsewhere.
        public override void Dispose() {
            base.Dispose();
        }

        private void RevealWidgets() {
            _autoHidden = false;

            // Position container at right edge of screen with some margin
            X = Framebuffer.Width - Width - 10;
            if (Y < 0 || Y > Framebuffer.Height - Height) {
                Y = 80; // Default Y position if not set properly
            }
            
            ShowWidgets();
            
            // Optionally trigger slide animation (not implemented here, just state hook)
            // Respect UISettings.EnableAutoHideWidgetsSlideAnimation and AutoHideWidgetsSlideDurationMs if rendering anims are available
        }

        private void HideWidgetsAuto() {
            if (!(UISettings.EnableAutoHideWidgets || UISettings.EnableAutoHideWidgetsVisuals)) return;
            _autoHidden = true;
            // Hide container and widgets to prevent updates/renders
            Visible = false;
            for (int i = 0; i < _widgets.Count; i++) {
                var w = _widgets[i];
                if (w != null) {
                    w.Visible = false;
                }
            }
        }
        
        /// <summary>
        /// Public method to handle auto-hide logic - can be called externally even when window is hidden
        /// </summary>
        public void UpdateAutoHide() {
            if (!(UISettings.EnableAutoHideWidgets || UISettings.EnableAutoHideWidgetsVisuals)) return;
            
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            int threshold = UISettings.AutoHideWidgetsRevealThresholdPx;
            
            // Calculate exclusion zones (15% from top and bottom)
            int screenHeight = Framebuffer.Height;
            int topExclusionZone = (int)(screenHeight * 0.15f);
            int bottomExclusionZone = screenHeight - (int)(screenHeight * 0.15f);
            
            // Check if mouse is at right edge AND within the middle 70% of screen height
            bool atRightEdge = mx >= (Framebuffer.Width - threshold);
            bool inValidVerticalZone = my >= topExclusionZone && my <= bottomExclusionZone;
            bool shouldReveal = atRightEdge && inValidVerticalZone;

            long nowMs = (long)guideXOS.Kernel.Drivers.Timer.Ticks;

            if (shouldReveal) {
                // Reveal widgets when mouse is at the far right edge in valid zone
                if (_autoHidden || !Visible) {
                    RevealWidgets();
                }
                _lastRevealCheckMs = nowMs;
            } else {
                // If mouse left reveal area, schedule hide
                if (!_autoHidden && Visible) {
                    int delay = UISettings.AutoHideWidgetsHideDelayMs;
                    if (nowMs - _lastRevealCheckMs >= delay) {
                        HideWidgetsAuto();
                    }
                }
            }
        }
    }
}
