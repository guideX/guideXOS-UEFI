using guideXOS.Kernel.Drivers;
using System.Windows.Forms;

namespace guideXOS.GUI {
    /// <summary>
    /// Small toggle button placed at far right to show the widget container when widgets are hidden.
    /// Hides itself once widgets are shown.
    /// </summary>
    internal class WidgetToggleButton : Window {
        private bool _hover;
        public WidgetToggleButton(int x, int y) : base(x, y, 22, 22) {
            Title = string.Empty;
            BarHeight = 0;
            ShowInTaskbar = false;
            ShowMaximize = false;
            ShowMinimize = false;
            ShowTombstone = false;
            IsResizable = false;
        }

        public override void OnInput() {
            if (!Visible || UISettings.EnableAutoHideWidgetsVisuals) return;
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            _hover = (mx >= X && mx <= X + Width && my >= Y && my <= Y + Height);
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
            if (left && _hover) {
                // Show widgets container if available
                if (Program.WidgetsContainer != null) {
                    Program.WidgetsContainer.ShowWidgets();
                }
                // Hide this button
                this.Visible = false;
            }
        }

        public override void OnDraw() {
            if (!Visible || UISettings.EnableAutoHideWidgetsVisuals) return;
            // Far-right small pill with glow
            uint bg = _hover ? 0xFF2E2E2Eu : 0xFF1E1E1Eu;
            uint border = 0xFF4A4A4Au;
            UIPrimitives.AFillRoundedRect(X, Y, Width, Height, bg, 6);
            UIPrimitives.DrawRoundedRect(X, Y, Width, Height, border, 1, 6);

            // Simple "widgets" icon: three small bars
            uint fg = _hover ? 0xFF00D9FFu : 0xFFB0B0B0u;
            int pad = 5; int w = Width - pad * 2; int barH = 2; int spacing = 3;
            int bx = X + pad; int by = Y + pad;
            Framebuffer.Graphics.FillRectangle(bx, by, w, barH, fg);
            Framebuffer.Graphics.FillRectangle(bx, by + spacing + barH, w, barH, fg);
            Framebuffer.Graphics.FillRectangle(bx, by + (spacing + barH) * 2, w, barH, fg);
        }
    }
}
