using System.Collections.Generic;
using guideXOS.Misc;

namespace System {
    internal static class HostedStringLifetimeExtensions {
        internal static void Dispose(this string value) { }
    }
}

namespace guideXOS {
    public sealed class Animation {
        public int Value;
        public int MinimumValue;
        public int MaximumValue;
        public int PeriodInMS;
        public int ValueChangesInPeriod;
        public bool Stopped;
        public void Dispose() { }
    }

    public static class Animator {
        public static void AddAnimation(Animation animation) { }
        public static void DisposeAnimation(Animation animation) {
            if (animation != null) animation.Dispose();
        }
    }
}

namespace guideXOS.Kernel.Drivers {
    public static class Timer {
        public static ulong Ticks;
    }

    public static class HID {
        public static bool Mouse;
        public static bool Keyboard;
    }
}

namespace guideXOS.GUI {
    internal class Window {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public int BarHeight = 40;
        public string Title;
        public int BaseDrawCount;
        private bool _visible;

        public bool Visible {
            get { return _visible; }
            set {
                _visible = value;
                OnSetVisible(value);
            }
        }

        public Window(int x, int y, int width, int height) {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public virtual void OnDraw() { BaseDrawCount++; }
        public virtual void OnSetVisible(bool value) { }
    }

    internal static class WindowManager {
        internal static List<Window> Windows = new List<Window>();
        internal static IFont font;
        internal static SharedTextFont ShellTextFont;
        internal static int ShellTextFontGeneration;

        internal static void MoveToEnd(Window window) {
            if (window == null) return;
            Windows.Remove(window);
            Windows.Add(window);
        }
    }

    internal static class Desktop {
        internal static MessageBox msgbox;

        internal static MessageBox EnsureMessageBox() {
            if (msgbox == null || WindowManager.Windows.IndexOf(msgbox) < 0) {
                msgbox = new MessageBox(100, 300);
                msgbox.Visible = false;
                WindowManager.Windows.Add(msgbox);
            }
            return msgbox;
        }
    }
}
