using guideXOS.DefaultApps;
using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Drivers.Input;
using System.Windows.Forms;
namespace guideXOS.GUI {
    /// <summary>
    /// Right-click context menu for the taskbar with a Task Manager entry
    /// </summary>
    internal class TaskbarMenu : Window {
        private const int ItemH = 28;
        private const int MenuW = 180;
        private const int Pad = 6;
        private bool _clickLatch;
        private bool _drawReported;
        private int _openKeyboardEventCount;

        public TaskbarMenu(int x, int y) : base(x, y, MenuW, ItemH + Pad * 2) {
            Title = "";
            BarHeight = 0;
            ShowInTaskbar = false;
            ShowInStartMenu = false;
            ShowMaximize = false;
            ShowMinimize = false;
            ShowTombstone = false;
            IsResizable = false;
        }

        public override void OnSetVisible(bool value) {
            base.OnSetVisible(value);
            if (value) {
                _clickLatch = false;
                _drawReported = false;
                _openKeyboardEventCount = PS2Keyboard.ProcessedEventCount;
                // Position near cursor
                X = Control.MousePosition.X - 8;
                Y = Control.MousePosition.Y - (ItemH + Pad * 2) + 8;
                // Clamp to screen
                if (X < 0) X = 0;
                if (Y < 0) Y = 0;
                if (X + Width > Framebuffer.Width) X = Framebuffer.Width - Width - 2;
                if (Y + Height > Framebuffer.Height) Y = Framebuffer.Height - Height - 2;
            }
        }

        public override void OnInput() {
            if (!Visible) return;
            if (PS2Keyboard.ProcessedEventCount > _openKeyboardEventCount &&
                Keyboard.KeyInfo.Key == System.ConsoleKey.Escape &&
                Keyboard.KeyInfo.KeyState == System.ConsoleKeyState.Released) {
                Program.MarkUefiTaskbarContextMenuDismissed("ESCAPE");
                Visible = false;
                return;
            }
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            bool leftPressed = MouseEventDispatcher.WasPressedThisFrame(MouseButtons.Left);
            if (leftDown || leftPressed) {
                if (_clickLatch && !leftPressed) return;
                _clickLatch = true;
                WindowManager.MouseHandled = true;
                // Single item: Task Manager
                int itemX = X + Pad; int itemY = Y + Pad; int itemW = Width - Pad * 2; int itemH = ItemH;
                if (mx >= itemX && mx < itemX + itemW && my >= itemY && my < itemY + itemH) {
                    Desktop.LaunchApplication("Task Manager");
                    Program.MarkUefiTaskbarContextMenuDismissed("COMMAND");
                    Visible = false; return;
                }
                // click elsewhere inside closes
                Program.MarkUefiTaskbarContextMenuDismissed("CLICK_AWAY");
                Visible = false; return;
            } else {
                _clickLatch = false;
                // Right-click outside closes
                if (!IsUnderMouse()) {
                    Program.MarkUefiTaskbarContextMenuDismissed("CLICK_AWAY");
                    Visible = false;
                }
            }
        }

        public override void OnGlobalKey(System.ConsoleKeyInfo key) {
            if (key.Key == System.ConsoleKey.Escape &&
                key.KeyState == System.ConsoleKeyState.Pressed && Visible) {
                Program.MarkUefiTaskbarContextMenuDismissed("ESCAPE");
                Visible = false;
                return;
            }
            base.OnGlobalKey(key);
        }

        public override void OnDraw() {
            if (!Visible || Framebuffer.Graphics == null || WindowManager.font == null) return;
            if (!_drawReported) {
                _drawReported = true;
                Program.MarkUefiTaskbarContextMenuDrawn(X, Y, Width, Height);
            }
            // Background
            Framebuffer.Graphics.FillRectangle(X, Y, Width, Height, 0xFF2A2A2A);
            Framebuffer.Graphics.DrawRectangle(X, Y, Width, Height, 0xFF3A3A3A, 1);
            // Item
            int itemX = X + Pad; int itemY = Y + Pad; int itemW = Width - Pad * 2; int itemH = ItemH;
            // Highlight on hover
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            bool hover = (mx >= itemX && mx < itemX + itemW && my >= itemY && my < itemY + itemH);
            if (hover) Framebuffer.Graphics.FillRectangle(itemX, itemY, itemW, itemH, 0xFF353535);
            WindowManager.font.DrawString(itemX + 6, itemY + (itemH / 2 - WindowManager.font.FontSize / 2), "Task Manager");
        }
    }
}
