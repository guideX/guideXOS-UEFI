using guideXOS.Kernel.Drivers;
using System;
using System.Windows.Forms;

namespace guideXOS.GUI {
    internal class SaveChangesDialog : Window {
        private readonly Window _owner;
        private readonly Action _onSave;
        private readonly Action _onDontSave;
        private readonly Action _onCancel;
        private Action _serviceCloseCallback;
        private bool _suppressServiceCloseCallback;
        private bool _clickLock;
        private int _padding = 10;
        private int _btnW = 90;
        private int _btnH = 28;
        public SaveChangesDialog(Window owner, Action onSave, Action onDontSave, Action onCancel) : base(owner == null ? 100 : owner.X + 40, owner == null ? 100 : owner.Y + 40, 380, 160) {
            Title = "Unsaved changes";
            _owner = owner; _onSave = onSave; _onDontSave = onDontSave; _onCancel = onCancel; _clickLock = false;
        }
        public override void OnInput() {
            base.OnInput(); bool left = Control.MouseButtons.HasFlag(MouseButtons.Left); int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            int cx = X + _padding; int cy = Y + _padding; int w = Width - _padding * 2; int by = Y + Height - _padding - _btnH; int saveX = cx; int dontX = saveX + _btnW + 8; int cancelX = dontX + _btnW + 8;
            if (left) {
                if (!_clickLock) {
                    if (mx >= saveX && mx <= saveX + _btnW && my >= by && my <= by + _btnH) { _suppressServiceCloseCallback = true; _onSave?.Invoke(); this.Visible = false; _clickLock = true; return; }
                    if (mx >= dontX && mx <= dontX + _btnW && my >= by && my <= by + _btnH) { _suppressServiceCloseCallback = true; _onDontSave?.Invoke(); this.Visible = false; _clickLock = true; return; }
                    if (mx >= cancelX && mx <= cancelX + _btnW && my >= by && my <= by + _btnH) { _suppressServiceCloseCallback = true; _onCancel?.Invoke(); this.Visible = false; _clickLock = true; return; }
                }
            } else { _clickLock = false; }
        }
        public override void OnDraw() {
            base.OnDraw(); int cx = X + _padding; int cy = Y + _padding; int w = Width - _padding * 2; int h = Height - _padding * 2; string msg = "Do you want to save changes?";
            WindowManager.font.DrawString(cx, cy + 8, msg);
            int by = Y + Height - _padding - _btnH; int saveX = cx; int dontX = saveX + _btnW + 8; int cancelX = dontX + _btnW + 8;
            Framebuffer.Graphics.FillRectangle(saveX, by, _btnW, _btnH, 0xFF3A3A3A); WindowManager.font.DrawString(saveX + 16, by + (_btnH / 2 - WindowManager.font.FontSize / 2), "Save");
            Framebuffer.Graphics.FillRectangle(dontX, by, _btnW, _btnH, 0xFF3A3A3A); WindowManager.font.DrawString(dontX + 6, by + (_btnH / 2 - WindowManager.font.FontSize / 2), "Don't Save");
            Framebuffer.Graphics.FillRectangle(cancelX, by, _btnW, _btnH, 0xFF3A3A3A); WindowManager.font.DrawString(cancelX + 8, by + (_btnH / 2 - WindowManager.font.FontSize / 2), "Cancel");
        }

        internal void SetServiceCloseCallback(Action callback) {
            _serviceCloseCallback = callback;
            _suppressServiceCloseCallback = false;
        }

        public override void OnSetVisible(bool value) {
            base.OnSetVisible(value);
            if (!value && !_suppressServiceCloseCallback &&
                    _serviceCloseCallback != null) {
                Action callback = _serviceCloseCallback;
                _serviceCloseCallback = null;
                callback();
            }
            if (value) _suppressServiceCloseCallback = false;
        }
    }
}
