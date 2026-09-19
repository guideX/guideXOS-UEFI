using guideXOS.GUI;
namespace guideXOS.GUI {
    /// <summary>
    /// Message Box
    /// </summary>
    internal class MessageBox : Window {
        /// <summary>
        /// Message
        /// </summary>
        string _message;
        private System.Action _serviceCloseCallback;
        private bool _serviceCallbackRaised;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        public MessageBox(int X, int Y) : base(X, Y, 200, WindowManager.font.FontSize * 2) {
            this._message = null;
            this.Title = "MessageBox";
        }
        /// <summary>
        /// On Draw
        /// </summary>
        public override void OnDraw() {
            if (this._message != null)
                this.Width = WindowManager.font.MeasureString(_message);
            base.OnDraw();
            if (this._message != null) {
                WindowManager.font.DrawString(X, Y, _message);
            }
        }
        /// <summary>
        /// Set Text
        /// </summary>
        /// <param name="text"></param>
        public void SetText(string text) {
            if (this._message != null) this._message.Dispose();
            this._message = text;
        }

        internal void ConfigureService(string title, string text,
                System.Action onClose) {
            Title = title;
            SetText(text);
            _serviceCloseCallback = onClose;
            _serviceCallbackRaised = false;
        }

        public override void OnSetVisible(bool value) {
            base.OnSetVisible(value);
            if (!value && _serviceCloseCallback != null &&
                    !_serviceCallbackRaised) {
                _serviceCallbackRaised = true;
                System.Action callback = _serviceCloseCallback;
                _serviceCloseCallback = null;
                callback();
            }
        }
    }
}
namespace System.Windows.Forms {
    /// <summary>
    /// Message Box
    /// </summary>
    public static class MessageBox {
        /// <summary>
        /// Show
        /// </summary>
        /// <param name="text"></param>
        public static void Show(string text) {
            Desktop.msgbox.X = WindowManager.Windows[0].X + 75;
            Desktop.msgbox.Y = WindowManager.Windows[0].Y + 75;
            Desktop.msgbox.SetText(text);
            WindowManager.MoveToEnd(Desktop.msgbox);
            Desktop.msgbox.Visible = true;
        }
    }
}
