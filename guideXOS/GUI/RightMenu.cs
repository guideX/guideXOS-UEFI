using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Drivers.Input;
using System.Windows.Forms;

namespace guideXOS.GUI {
    /// <summary>
    /// The normal guideXOS desktop context menu.
    ///
    /// This remains a Window so WindowManager owns its z-order and input
    /// ordering. It is reused between opens; no UEFI-specific popup layer is
    /// involved.
    /// </summary>
    internal class RightMenu : Window {
        private const int ItemH = 28;
        private const int MinimumMenuW = 220;
        private const int SubmenuW = 160;
        private const int SubmenuItemBase = 100;

        private static readonly int[] IconSizes = { 16, 24, 32, 48, 128 };
        private static readonly string[] IconSizeLabels = { "16", "24", "32", "48", "128" };
        private static readonly string[] IconSizeCommands = {
            "ICON_SIZE_16", "ICON_SIZE_24", "ICON_SIZE_32", "ICON_SIZE_48", "ICON_SIZE_128"
        };

        private bool _showIconSizeSubmenu;
        private bool _leftDown;
        private bool _drawReported;
        private int _openKeyboardEventCount;
        private int _hoveredItemIndex = -1;
        private int _iconSizeItemIndex = -1;

        public int HoveredItemIndex => _hoveredItemIndex;

        public RightMenu() : base(Control.MousePosition.X, Control.MousePosition.Y,
                                  MinimumMenuW, ItemH * 3) {
            BarHeight = 0;
            ShowInTaskbar = false;
            ShowInStartMenu = false;
            ShowMaximize = false;
            ShowMinimize = false;
            ShowTombstone = false;
            IsResizable = false;
            Visible = false;
        }

        /// <summary>
        /// Open this existing menu at the current pointer location.
        /// </summary>
        public void ShowAt(int x, int y) {
            _showIconSizeSubmenu = false;
            _leftDown = false;
            _hoveredItemIndex = -1;
            _drawReported = false;
            _openKeyboardEventCount = PS2Keyboard.ProcessedEventCount;
            X = x - 8;
            Y = y - 8;
            UpdateMenuGeometry();
            WindowManager.MoveToEnd(this);
            Visible = true;
        }

        public override void OnSetVisible(bool value) {
            base.OnSetVisible(value);
            if (value) {
                UpdateMenuGeometry();
                // Preserve the original pointer-offset placement, but clamp
                // all four edges so draw and hit-test rectangles stay visible.
                X = Control.MousePosition.X - 8;
                Y = Control.MousePosition.Y - 8;
                ClampMenuToScreen();
                return;
            }

            if (_hoveredItemIndex != -1) {
                Program.MarkUefiContextMenuHover(-1);
            }
            _showIconSizeSubmenu = false;
            _leftDown = false;
            _hoveredItemIndex = -1;
            _drawReported = false;
        }

        public override void OnInput() {
            if (!Visible) return;

            // Keep Escape on the same native keyboard pipeline even on
            // builds where a later keyboard subscriber replaces the global
            // event chain. The processed-event watermark prevents a stale
            // released Escape from dismissing the next popup immediately.
            if (PS2Keyboard.ProcessedEventCount > _openKeyboardEventCount &&
                Keyboard.KeyInfo.Key == System.ConsoleKey.Escape &&
                Keyboard.KeyInfo.KeyState == System.ConsoleKeyState.Released) {
                Dismiss("ESCAPE");
                return;
            }

            UpdateMenuGeometry();
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            int iconSizeIdx = GetIconSizeItemIndex();

            bool hoverIconSize = Hit(iconSizeIdx, mx, my);
            bool hoverSubmenu = _showIconSizeSubmenu && IsInSubmenu(mx, my);

            if (hoverIconSize) {
                _showIconSizeSubmenu = true;
            } else if (!hoverSubmenu) {
                _showIconSizeSubmenu = false;
            }

            int hovered = -1;
            if (_showIconSizeSubmenu && hoverSubmenu) {
                int submenuIndex = (my - GetSubmenuY(iconSizeIdx)) / ItemH;
                if (submenuIndex >= 0 && submenuIndex < IconSizes.Length) {
                    hovered = SubmenuItemBase + submenuIndex;
                }
            } else {
                int itemCount = GetMenuItemCount();
                for (int i = 0; i < itemCount; i++) {
                    if (Hit(i, mx, my)) {
                        hovered = i;
                        break;
                    }
                }
            }
            if (hovered != _hoveredItemIndex) {
                _hoveredItemIndex = hovered;
                Program.MarkUefiContextMenuHover(hovered);
            }

            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            bool clickEdge = MouseEventDispatcher.WasPressedThisFrame(MouseButtons.Left) ||
                             (leftDown && !_leftDown);
            if (clickEdge) Program.MarkUefiContextMenuLeftEdge();
            _leftDown = leftDown;

            if (!leftDown && !clickEdge) return;

            // A popup owns every left-button gesture while it is visible,
            // including click-away dismissal. This prevents click-through to
            // a desktop tile or window underneath the popup.
            WindowManager.MouseHandled = true;
            if (!clickEdge) return;

            if (_showIconSizeSubmenu && hoverSubmenu) {
                int submenuIndex = (my - GetSubmenuY(iconSizeIdx)) / ItemH;
                if (submenuIndex >= 0 && submenuIndex < IconSizes.Length) {
                    Desktop.SetIconSize(IconSizes[submenuIndex]);
                    Program.MarkUefiContextMenuActivated(IconSizeCommands[submenuIndex]);
                    Dismiss("COMMAND");
                    return;
                }
            }

            if (Hit(0, mx, my)) {
                WindowManager.EnqueueDisplayOptions(Control.MousePosition.X,
                                                    Control.MousePosition.Y, 800, 600);
                Program.MarkUefiContextMenuActivated("DISPLAY_OPTIONS");
                Dismiss("COMMAND");
                return;
            }

            int currentItem = 1;
            if (Hit(currentItem, mx, my)) {
                if (Program.PerfWidget != null) {
                    Program.PerfWidget.Visible = !Program.PerfWidget.Visible;
                    if (Program.PerfWidget.Visible) WindowManager.MoveToEnd(Program.PerfWidget);
                }
                Program.MarkUefiContextMenuActivated("PERFORMANCE_WIDGET");
                Dismiss("COMMAND");
                return;
            }
            currentItem++;

            if (!guideXOS.OS.SystemMode.IsLiveMode) {
                if (Hit(currentItem, mx, my)) {
                    guideXOS.OS.Configuration.SaveConfiguration();
                    Program.MarkUefiContextMenuActivated("SAVE_SETTINGS");
                    Dismiss("COMMAND");
                    return;
                }
                currentItem++;
            }

            if (Desktop.Dir != null && Desktop.Dir.Length > 0) {
                if (Hit(currentItem, mx, my)) {
                    Desktop.Dir.Length--;
                    if (Desktop.Dir.IndexOf('/') != -1) {
                        string nextDir = Desktop.Dir.Substring(0, Desktop.Dir.LastIndexOf('/')) + "/";
                        Desktop.Dir.Dispose();
                        Desktop.Dir = nextDir;
                    } else {
                        Desktop.Dir = "";
                    }
                    Program.MarkUefiContextMenuActivated("UP_ONE_LEVEL");
                    Dismiss("COMMAND");
                    return;
                }
                currentItem++;
            }

            // The Icon Size parent intentionally stays open so the existing
            // submenu can be reached by hover.
            if (Hit(currentItem, mx, my)) return;

            Dismiss("CLICK_AWAY");
        }

        private void Dismiss(string reason) {
            Program.MarkUefiContextMenuDismissed(reason);
            Visible = false;
        }

        private int GetMenuItemCount() {
            int count = 3; // Display Options, Performance Widget, Icon Size
            if (!guideXOS.OS.SystemMode.IsLiveMode) count++;
            if (Desktop.Dir != null && Desktop.Dir.Length > 0) count++;
            return count;
        }

        private int GetIconSizeItemIndex() {
            int index = 2;
            if (!guideXOS.OS.SystemMode.IsLiveMode) index++;
            if (Desktop.Dir != null && Desktop.Dir.Length > 0) index++;
            _iconSizeItemIndex = index;
            return index;
        }

        private void UpdateMenuGeometry() {
            Height = ItemH * GetMenuItemCount();
            Width = GetMenuWidth();
            if (_showIconSizeSubmenu) ClampMenuToScreen();
        }

        private int GetMenuWidth() {
            int width = MinimumMenuW;
            if (WindowManager.font == null) return width;

            int measured = WindowManager.font.MeasureString("Display Options");
            int candidate = WindowManager.font.MeasureString("Performance Widget");
            if (candidate > measured) measured = candidate;
            candidate = WindowManager.font.MeasureString("Icon Size");
            if (candidate > measured) measured = candidate;
            if (!guideXOS.OS.SystemMode.IsLiveMode) {
                candidate = WindowManager.font.MeasureString("Save Settings");
                if (candidate > measured) measured = candidate;
            }
            if (Desktop.Dir != null && Desktop.Dir.Length > 0) {
                candidate = WindowManager.font.MeasureString("Up one level");
                if (candidate > measured) measured = candidate;
            }
            return measured + 32 > width ? measured + 32 : width;
        }

        private int GetSubmenuY(int iconSizeIdx) {
            int submenuHeight = ItemH * IconSizes.Length;
            int y = Y + iconSizeIdx * ItemH;
            int maxY = Framebuffer.Height - submenuHeight;
            if (y > maxY) y = maxY;
            if (y < 0) y = 0;
            return y;
        }

        private int GetSubmenuX() {
            int rightX = X + Width;
            if (rightX + SubmenuW <= Framebuffer.Width) return rightX;
            int leftX = X - SubmenuW;
            if (leftX >= 0) return leftX;
            return 0;
        }

        private bool IsInSubmenu(int mx, int my) {
            int submenuX = GetSubmenuX();
            int submenuY = GetSubmenuY(GetIconSizeItemIndex());
            int submenuHeight = ItemH * IconSizes.Length;
            return mx >= submenuX && mx < submenuX + SubmenuW &&
                   my >= submenuY && my < submenuY + submenuHeight;
        }

        private bool Hit(int index, int mx, int my) {
            int y = Y + index * ItemH;
            return mx >= X && mx < X + Width && my >= y && my < y + ItemH;
        }

        private void ClampMenuToScreen() {
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

        public override void OnDraw() {
            if (!Visible || Framebuffer.Graphics == null || WindowManager.font == null) return;

            UpdateMenuGeometry();
            Framebuffer.Graphics.AFillRectangle(X, Y, Width, Height, 0xCC222222);

            if (!_drawReported) {
                _drawReported = true;
                Program.MarkUefiContextMenuDrawn(X, Y, Width, Height);
            }

            int y = Y;
            DrawItem(0, y, "Display Options");
            y += ItemH;

            bool perfVisible = Program.PerfWidget != null && Program.PerfWidget.Visible;
            DrawItem(1, y, perfVisible ? "Performance Widget ?" : "Performance Widget");
            y += ItemH;

            if (!guideXOS.OS.SystemMode.IsLiveMode) {
                DrawItem(2, y, "Save Settings");
                y += ItemH;
            }
            if (Desktop.Dir != null && Desktop.Dir.Length > 0) {
                int row = GetIconSizeItemIndex() - 1;
                DrawItem(row, y, "Up one level");
                y += ItemH;
            }

            int iconSizeIdx = GetIconSizeItemIndex();
            DrawItem(iconSizeIdx, y, "Icon Size");
            WindowManager.font.DrawString(X + Width - 20,
                y + (ItemH / 2) - (WindowManager.font.FontSize / 2), ">");

            if (_showIconSizeSubmenu) {
                int submenuX = GetSubmenuX();
                int submenuY = GetSubmenuY(iconSizeIdx);
                int submenuHeight = ItemH * IconSizes.Length;
                Framebuffer.Graphics.AFillRectangle(submenuX, submenuY, SubmenuW,
                                                    submenuHeight, 0xCC222222);
                Framebuffer.Graphics.DrawRectangle(submenuX, submenuY, SubmenuW,
                                                   submenuHeight, 0xFF3F3F3F, 1);
                for (int i = 0; i < IconSizes.Length; i++) {
                    int itemY = submenuY + i * ItemH;
                    if (_hoveredItemIndex == SubmenuItemBase + i) {
                        Framebuffer.Graphics.FillRectangle(submenuX + 1, itemY,
                                                           SubmenuW - 2, ItemH,
                                                           0xFF313131);
                    }
                    WindowManager.font.DrawString(submenuX + 8,
                        itemY + (ItemH / 2) - (WindowManager.font.FontSize / 2),
                        IconSizeLabels[i]);
                    if (IconSizes[i] == Desktop.IconSize) {
                        WindowManager.font.DrawString(submenuX + SubmenuW - 20,
                            itemY + (ItemH / 2) - (WindowManager.font.FontSize / 2), "*");
                    }
                }
            }

            DrawBorder(false);
        }

        private void DrawItem(int index, int y, string label) {
            if (_hoveredItemIndex == index) {
                Framebuffer.Graphics.FillRectangle(X + 1, y, Width - 2, ItemH,
                                                   0xFF313131);
            }
            WindowManager.font.DrawString(X + 8,
                y + (ItemH / 2) - (WindowManager.font.FontSize / 2), label);
        }
    }
}
