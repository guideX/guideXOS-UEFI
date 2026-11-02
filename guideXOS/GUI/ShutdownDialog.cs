using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System.Windows.Forms;

namespace guideXOS.GUI {
    /// <summary>
    /// Simple shutdown confirmation dialog with Yes/No buttons
    /// </summary>
    internal class ShutdownDialog : Window {
        private readonly string _message = "Are you sure you want to shut down?";

        // Layout
        private const int Padding = 12;
        private const int BtnW = 80;
        private const int BtnH = 28;
        private const int Gap = 10;

        public ShutdownDialog() : base(
            (Framebuffer.Width - 360) / 2,
            (Framebuffer.Height - 160) / 2,
            360,
            160) {
            Title = "Confirm Shutdown";
            IsResizable = false;
            ShowInTaskbar = true;
            ShowMaximize = false;
            ShowMinimize = true;
            ShowTombstone = false;
            ShowInStartMenu = false;
        }

        public override void OnInput() {
            base.OnInput();
            if (!Visible) return;

            // Compute button rects
            int contentTop = Y + BarHeight + Padding;
            int btnY = Y + Height - Padding - BtnH;

            int yesX = X + Width - Padding - BtnW;
            int noX = yesX - Gap - BtnW;

            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;

            if (Control.MouseButtons == MouseButtons.Left) {
                // Yes -> Shutdown immediately
                if (mx >= yesX && mx <= yesX + BtnW && my >= btnY && my <= btnY + BtnH) {
                    Power.Shutdown();
                    return;
                }
                // No -> close the dialog
                if (mx >= noX && mx <= noX + BtnW && my >= btnY && my <= btnY + BtnH) {
                    Visible = false;
                    return;
                }
            }
        }

        public override void OnDraw() {
            base.OnDraw();

            // Content background
            int cx = X + 1;
            int cy = Y + BarHeight + 1;
            int cw = Width - 2;
            int ch = Height - BarHeight - 2;
            Framebuffer.Graphics.FillRectangle(cx, cy, cw, ch, 0xFF222222);

            // Message text (wrap area is Width - 2*Padding)
            int textX = X + Padding;
            int textY = Y + BarHeight + Padding;
            SharedTextFont selected = WindowManager.ShellTextFont;
            if (selected != null &&
                selected.Provider == SharedFontProviderKind.TrueType) {
                int bodyWidth = Width - Padding * 2;
                int bodyHeight = Y + Height - Padding - BtnH - textY;
                if (bodyWidth < 0) bodyWidth = 0;
                if (bodyHeight < 0) bodyHeight = 0;
                ShellTextSurfaceRenderer.DrawWrappedText(
                    Framebuffer.Graphics, selected, _message, textX, textY,
                    bodyWidth, 0, 0xFFFFFFFFu, textX, textY, bodyWidth,
                    bodyHeight, SharedTextAlignment.Left);
            } else {
                WindowManager.font.DrawString(textX, textY, _message,
                    Width - Padding * 2, WindowManager.font.FontSize * 3);
            }

            // Buttons
            int btnY = Y + Height - Padding - BtnH;
            int yesX = X + Width - Padding - BtnW;
            int noX = yesX - Gap - BtnW;

            // Hover
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool overYes = mx >= yesX && mx <= yesX + BtnW && my >= btnY && my <= btnY + BtnH;
            bool overNo = mx >= noX && mx <= noX + BtnW && my >= btnY && my <= btnY + BtnH;

            uint btnBg = 0xFF2A2A2A;
            uint btnBgHover = 0xFF343434;
            uint border = 0xFF3F3F3F;

            // No button
            Framebuffer.Graphics.FillRectangle(noX, btnY, BtnW, BtnH, overNo ? btnBgHover : btnBg);
            Framebuffer.Graphics.DrawRectangle(noX, btnY, BtnW, BtnH, border, 1);
            if (selected != null &&
                selected.Provider == SharedFontProviderKind.TrueType) {
                int lineHeight;
                if (selected.GetLineHeight(out lineHeight)) {
                    ShellTextSurfaceRenderer.DrawFixedText(
                        Framebuffer.Graphics, selected, "No", noX + 4,
                        btnY + (BtnH - lineHeight) / 2, BtnW - 8, noX, btnY,
                        BtnW, BtnH, 0xFFFFFFFFu, SharedTextAlignment.Center);
                }
            } else {
                WindowManager.font.DrawString(noX + 24,
                    btnY + BtnH / 2 - WindowManager.font.FontSize / 2, "No");
            }

            // Yes button
            Framebuffer.Graphics.FillRectangle(yesX, btnY, BtnW, BtnH, overYes ? btnBgHover : btnBg);
            Framebuffer.Graphics.DrawRectangle(yesX, btnY, BtnW, BtnH, border, 1);
            if (selected != null &&
                selected.Provider == SharedFontProviderKind.TrueType) {
                int lineHeight;
                if (selected.GetLineHeight(out lineHeight)) {
                    ShellTextSurfaceRenderer.DrawFixedText(
                        Framebuffer.Graphics, selected, "Yes", yesX + 4,
                        btnY + (BtnH - lineHeight) / 2, BtnW - 8, yesX, btnY,
                        BtnW, BtnH, 0xFFFFFFFFu, SharedTextAlignment.Center);
                }
            } else {
                WindowManager.font.DrawString(yesX + 22,
                    btnY + BtnH / 2 - WindowManager.font.FontSize / 2, "Yes");
            }
        }
    }
}
