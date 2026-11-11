using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
namespace guideXOS.GUI {
    /// <summary>
    /// Message Box
    /// </summary>
    internal class MessageBox : Window {
        private const int HorizontalPadding = 12;
        private const int VerticalPadding = 12;
        private const int MaximumWindowWidth = 640;
        private const int TaskbarHeight = 40;

        /// <summary>
        /// Message
        /// </summary>
        string _message;
        private System.Action _serviceCloseCallback;
        private bool _serviceCallbackRaised;
        private IFont _bitmapFallbackSource;
        private SharedTextFont _bitmapFallbackFont;
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
            SharedTextFont selected = ResolveTextFont();
            if (Framebuffer.Graphics == null) return;
            if (_message == null) {
                base.OnDraw();
                return;
            }

            int screenWidth = Framebuffer.Graphics.Width;
            int screenHeight = Framebuffer.Graphics.Height;
            if (screenWidth <= 0 || screenHeight <= 0) return;

            int maximumWidth = screenWidth - (HorizontalPadding * 2);
            if (maximumWidth > MaximumWindowWidth)
                maximumWidth = MaximumWindowWidth;
            if (maximumWidth <= 0) maximumWidth = screenWidth;
            int maximumTextWidth = maximumWidth - (HorizontalPadding * 2);
            if (maximumTextWidth <= 0) maximumTextWidth = maximumWidth;

            SharedTextMeasurement preferred = new SharedTextMeasurement();
            bool textMeasured = selected != null &&
                ShellTextSurfaceRenderer.MeasureWrappedText(selected, _message,
                    maximumTextWidth, 0, out preferred, out _);
            if (!textMeasured) {
                preferred = new SharedTextMeasurement();
                if (WindowManager.font != null) {
                    preferred.Width = WindowManager.font.MeasureString(_message);
                    preferred.Height = WindowManager.font.FontSize;
                    preferred.LineCount = 1;
                }
            }

            int desiredWidth = preferred.Width + HorizontalPadding * 2;
            int minimumWidth = maximumWidth < 200 ? maximumWidth : 200;
            if (desiredWidth < minimumWidth) desiredWidth = minimumWidth;
            if (desiredWidth > maximumWidth) desiredWidth = maximumWidth;
            if (desiredWidth <= 0) desiredWidth = screenWidth;
            Width = desiredWidth;

            int bodyWidth = Width - HorizontalPadding * 2;
            if (bodyWidth < 0) bodyWidth = 0;
            SharedTextMeasurement bodyMeasurement =
                new SharedTextMeasurement();
            bool bodyMeasured = selected != null &&
                ShellTextSurfaceRenderer.MeasureWrappedText(selected, _message,
                    bodyWidth, 0, out bodyMeasurement, out _);
            if (!bodyMeasured) bodyMeasurement = preferred;

            int maximumBodyHeight = screenHeight - TaskbarHeight - BarHeight -
                VerticalPadding * 2;
            if (maximumBodyHeight < 0) maximumBodyHeight = 0;
            int bodyHeight = bodyMeasurement.Height;
            if (bodyHeight > maximumBodyHeight)
                bodyHeight = maximumBodyHeight;
            if (bodyHeight < 0) bodyHeight = 0;
            Height = bodyHeight + VerticalPadding * 2;

            X = (screenWidth - Width) / 2;
            if (X < 0) X = 0;
            int workTop = BarHeight;
            int workBottom = screenHeight - TaskbarHeight;
            if (workBottom < workTop) workBottom = workTop;
            int availableBodyHeight = workBottom - workTop;
            if (Height > availableBodyHeight) Height = availableBodyHeight;
            Y = workTop + (availableBodyHeight - Height) / 2;
            base.OnDraw();

            int textX = X + HorizontalPadding;
            int textY = Y + VerticalPadding;
            int clipHeight = Height - VerticalPadding * 2;
            if (clipHeight < 0) clipHeight = 0;
            if (selected != null && selected.IsAvailable) {
                ShellTextSurfaceRenderer.DrawWrappedText(
                    Framebuffer.Graphics, selected, _message, textX, textY,
                    bodyWidth, 0, 0xFFFFFFFFu, textX, textY, bodyWidth,
                    clipHeight, SharedTextAlignment.Left);
            } else if (WindowManager.font != null) {
                WindowManager.font.DrawStringClipped(textX, textY, _message,
                    Framebuffer.Graphics, textX, textY, bodyWidth,
                    clipHeight);
            }
        }

        private SharedTextFont ResolveTextFont() {
            SharedTextFont selected = WindowManager.ShellTextFont;
            IFont bitmap = WindowManager.font;
            if (selected != null || bitmap == null) return selected;
            if (!object.ReferenceEquals(_bitmapFallbackSource, bitmap)) {
                _bitmapFallbackSource = bitmap;
                _bitmapFallbackFont = SharedTextFont.CreateDefaultBitmap(bitmap);
            }
            return _bitmapFallbackFont;
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
            guideXOS.GUI.MessageBox box = Desktop.EnsureMessageBox();
            if (box == null || WindowManager.Windows == null ||
                WindowManager.Windows.Count == 0) return;
            box.X = WindowManager.Windows[0].X + 75;
            box.Y = WindowManager.Windows[0].Y + 75;
            box.SetText(text);
            WindowManager.MoveToEnd(box);
            box.Visible = true;
        }
    }
}
