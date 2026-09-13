using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Windows.Forms;
using System.Drawing;

namespace guideXOS.DockableWidgets {
    /// <summary>
    /// Clock - Floating widget with analog and digital display
    /// </summary>
    internal class Clock : DockableWidget {
        /// <summary>
        /// Sine lookup table
        /// </summary>
        static int[] sine;
        
        private const int WidgetWidth = 200;
        private const int WidgetHeight = 200;
        
        // FIXED: Cache time string to prevent per-frame allocations
        private static string _cachedTimeString = null;
        private static int _lastMinute = -1;
        private static int _lastHour = -1;
        private static int _lastSecond = -1;
        
        public override int PreferredHeight => WidgetHeight - Padding * 2;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        public Clock(int X, int Y) : base(X, Y, WidgetWidth, WidgetHeight) {
            Title = "Clock";
            ShowInStartMenu = false;
            ShowInTaskbar = false;
            sine = new int[16] {
                0,
                27,
                54,
                79,
                104,
                128,
                150,
                171,
                190,
                201,
                221,
                233,
                243,
                250,
                254,
                255
            };
        }
        
        public override void OnInput() {
            if (!Visible) return;
            
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            
            // Close button hit test
            int closeX = X + Width - Padding - CloseBtnSize;
            int closeY = Y + Padding;
            _closeHover = mx >= closeX && mx <= closeX + CloseBtnSize && 
                          my >= closeY && my <= closeY + CloseBtnSize;
            
            if (leftDown && _closeHover) {
                Visible = false;
                return;
            }
            
            // Handle dragging
            HandleDragging();
        }
        
        /// <summary>
        /// On Draw
        /// </summary>
        public override void OnDraw() {
            if (!Visible) return;
            
            // Draw widget background with subtle glow
            UIPrimitives.AFillRoundedRect(X - 2, Y - 2, Width + 4, Height + 4, 0x331E90FF, 8);
            UIPrimitives.AFillRoundedRect(X, Y, Width, Height, 0xDD1A1A1A, 6);
            
            // Draw border
            UIPrimitives.DrawRoundedRect(X, Y, Width, Height, 0xFF3A3A3A, 1, 6);
            
            int cy = Y + Padding;
            int cx = X + Padding;
            int contentWidth = Width - Padding * 2;
            
            DrawContent(cx, cy, contentWidth);
            
            // Draw close button
            int closeX = X + Width - Padding - CloseBtnSize;
            int closeY = Y + Padding;
            DrawCloseButton(closeX, closeY);
        }
        
        public override void DrawContent(int contentX, int contentY, int contentWidth) {
            // Cache time text and rebuild it only when the displayed minute changes.
            int currentHour = RTC.Hour;
            int currentMinute = RTC.Minute;
            if (_cachedTimeString == null || _lastMinute != currentMinute || _lastHour != currentHour) {
                // Dispose old cached string
                if (_cachedTimeString != null) {
                    _cachedTimeString.Dispose();
                }
                
                // Build the five-character display directly.  The custom
                // runtime does not reclaim managed temporaries, so avoid
                // ToString()/concatenation intermediates here.
                _cachedTimeString = FormatTime(currentHour, currentMinute);

                _lastMinute = currentMinute;
                _lastHour = currentHour;
            }
            if (_lastSecond != RTC.Second) {
                _lastSecond = RTC.Second;
                Program.MarkUefiWidgetUpdated("Clock");
            }
            
            // Draw cached time string (no allocations per frame)
            WindowManager.font.DrawString(contentX + contentWidth / 2 - WindowManager.font.MeasureString(_cachedTimeString) / 2, contentY + 4, _cachedTimeString);
            
            // Draw analog clock hands (keep second hand)
            int centerX = contentX + contentWidth / 2;
            int centerY = contentY + PreferredHeight / 2 + 20; // Offset down a bit from digital time
            
            int second = RTC.Second * 6;
            DrawHand(centerX, centerY, second, contentWidth / 3, 0xFFFF5555);
            int minute = RTC.Minute * 6;
            DrawHand(centerX, centerY, minute, contentWidth / 4, 0xFFCCCCCC);
            int hour = (RTC.Hour >= 12 ? RTC.Hour - 12 : RTC.Hour) * 30;
            DrawHand(centerX, centerY, hour, contentWidth / 6, 0xFFFFFFFF);
        }

        private static unsafe string FormatTime(int hour, int minute) {
            char* buffer = stackalloc char[5];
            int length = 0;
            if (hour >= 10) buffer[length++] = (char)('0' + hour / 10);
            buffer[length++] = (char)('0' + hour % 10);
            buffer[length++] = ':';
            buffer[length++] = (char)('0' + minute / 10);
            buffer[length++] = (char)('0' + minute % 10);
            return new string(buffer, 0, length);
        }
        
        /// <summary>
        /// Draw Hand
        /// </summary>
        /// <param name="xStart"></param>
        /// <param name="yStart"></param>
        /// <param name="angle"></param>
        /// <param name="radius"></param>
        /// <param name="color"></param>
        void DrawHand(int xStart, int yStart, int angle, int radius, uint color) {
            if (angle >= 0 && angle <= 360) {
                angle /= 6;
                int xEnd, yEnd, quadrant, x_flip, y_flip;
                quadrant = angle / 15;
                switch (quadrant) {
                    case 0:
                        x_flip = 1;
                        y_flip = -1;
                        break;
                    case 1:
                        angle = Math.Abs(angle - 30);
                        x_flip = y_flip = 1;
                        break;
                    case 2:
                        angle = angle - 30;
                        x_flip = -1;
                        y_flip = 1;
                        break;
                    case 3:
                        angle = Math.Abs(angle - 60);
                        x_flip = y_flip = -1;
                        break;
                    default:
                        x_flip = y_flip = 1;
                        break;
                }
                if (angle > sine.Length - 1) { } else {
                    xEnd = xStart;
                    yEnd = yStart;
                    xEnd += x_flip * (sine[angle] * radius >> 8);
                    yEnd += y_flip * (sine[15 - angle] * radius >> 8);
                    Framebuffer.Graphics.DrawLine(xStart, yStart, xEnd, yEnd, color);
                }
            }
        }
    }
}
