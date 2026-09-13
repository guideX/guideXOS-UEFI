using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Drivers.Input;
using System.Windows.Forms;

namespace guideXOS.GUI {
    /// <summary>
    /// Context menu for dockable widgets
    /// </summary>
    internal class WidgetContextMenu : Window {
        private DockableWidget _targetWidget;
        private WidgetContainer _targetContainer;
        private bool _isDocked;
        private bool _leftDown;
        private bool _hovered;
        private bool _drawReported;
        private int _openKeyboardEventCount;
        
        public WidgetContextMenu() : base(0, 0, 120, 28) {
            Visible = false;
            BarHeight = 0;
            ShowInTaskbar = false;
            ShowMaximize = false;
            ShowMinimize = false;
            ShowTombstone = false;
            IsResizable = false;
        }
        
        /// <summary>
        /// Show menu for a docked widget
        /// </summary>
        public void ShowForDockedWidget(DockableWidget widget, WidgetContainer container, int x, int y) {
            Program.CloseOtherContextMenusForWidget();
            _targetWidget = widget;
            _targetContainer = container;
            _isDocked = true;
            X = x;
            Y = y;
            ClampToScreen();
            _leftDown = false;
            _hovered = false;
            _drawReported = false;
            _openKeyboardEventCount = PS2Keyboard.ProcessedEventCount;
            Visible = true;
            WindowManager.MoveToEnd(this);
            Program.MarkUefiWidgetMenuOpened(X, Y, Width, Height, "Undock");
        }
        
        /// <summary>
        /// Show menu for a standalone widget
        /// </summary>
        public void ShowForStandaloneWidget(DockableWidget widget, int x, int y) {
            Program.CloseOtherContextMenusForWidget();
            _targetWidget = widget;
            _targetContainer = null;
            _isDocked = false;
            X = x;
            Y = y;
            ClampToScreen();
            _leftDown = false;
            _hovered = false;
            _drawReported = false;
            _openKeyboardEventCount = PS2Keyboard.ProcessedEventCount;
            Visible = true;
            WindowManager.MoveToEnd(this);
            Program.MarkUefiWidgetMenuOpened(X, Y, Width, Height, "Dock");
        }

        public void HideMenu() {
            Visible = false;
            _targetWidget = null;
            _targetContainer = null;
            _leftDown = false;
            _hovered = false;
        }

        public override void OnSetVisible(bool value) {
            base.OnSetVisible(value);
            if (value) {
                _openKeyboardEventCount = PS2Keyboard.ProcessedEventCount;
                _drawReported = false;
                ClampToScreen();
            } else {
                _leftDown = false;
                _hovered = false;
                _drawReported = false;
            }
        }
        
        public override void OnInput() {
            if (!Visible) return;

            if (PS2Keyboard.ProcessedEventCount > _openKeyboardEventCount &&
                Keyboard.KeyInfo.Key == System.ConsoleKey.Escape &&
                Keyboard.KeyInfo.KeyState == System.ConsoleKeyState.Released) {
                Dismiss("ESCAPE");
                return;
            }
            
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool leftClick = Control.MouseButtons.HasFlag(MouseButtons.Left);
            bool leftPressed = MouseEventDispatcher.WasPressedThisFrame(MouseButtons.Left);
            bool clickEdge = leftPressed || (leftClick && !_leftDown);
            bool inside = mx >= X && mx < X + Width && my >= Y && my < Y + Height;
            if (inside != _hovered) {
                _hovered = inside;
                Program.MarkUefiWidgetMenuHover(inside);
            }
            _leftDown = leftClick;
            
            if (!leftClick && !leftPressed) return;

            // A widget popup owns the complete left-button gesture, including
            // click-away dismissal, so the desktop underneath cannot receive
            // the same event.
            WindowManager.MouseHandled = true;
            if (!clickEdge) return;

            if (inside) {
                if (_isDocked) {
                    if (_targetWidget != null && _targetContainer != null) {
                        _targetContainer.UndockWidgetToPosition(_targetWidget, mx, my);
                    }
                    Program.MarkUefiWidgetMenuActivated("UNDOCK");
                } else {
                    if (_targetWidget != null) _targetWidget.TryDockToNearby();
                    Program.MarkUefiWidgetMenuActivated("DOCK");
                }
                Dismiss("COMMAND");
            } else {
                Dismiss("CLICK_AWAY");
            }
        }
        
        public override void OnDraw() {
            if (!Visible || Framebuffer.Graphics == null || WindowManager.font == null) return;
            
            // Background
            Framebuffer.Graphics.AFillRectangle(X, Y, Width, Height, 0xCC222222);

            if (!_drawReported) {
                _drawReported = true;
                Program.MarkUefiWidgetMenuDrawn(X, Y, Width, Height);
            }

            if (_hovered) {
                Framebuffer.Graphics.FillRectangle(X + 1, Y + 1, Width - 2,
                                                   Height - 2, 0xFF353535);
            }
            
            // Menu text
            string menuText = _isDocked ? "Undock" : "Dock";
            int textY = Y + (Height / 2) - (WindowManager.font.FontSize / 2);
            WindowManager.font.DrawString(X + 8, textY, menuText);
            
            // Border
            Framebuffer.Graphics.DrawRectangle(X, Y, Width, Height, 0xFF444444, 1);
        }

        private void Dismiss(string reason) {
            Program.MarkUefiWidgetMenuDismissed(reason);
            HideMenu();
        }

        private void ClampToScreen() {
            int maxX = Framebuffer.Width - Width;
            int maxY = Framebuffer.Height - Height;
            if (maxX < 0) maxX = 0;
            if (maxY < 0) maxY = 0;
            if (X < 0) X = 0;
            if (Y < 0) Y = 0;
            if (X > maxX) X = maxX;
            if (Y > maxY) Y = maxY;
        }

        public override void OnGlobalKey(System.ConsoleKeyInfo key) {
            if (key.Key == System.ConsoleKey.Escape &&
                key.KeyState == System.ConsoleKeyState.Pressed && Visible) {
                Dismiss("ESCAPE");
                return;
            }
            base.OnGlobalKey(key);
        }
    }
}
