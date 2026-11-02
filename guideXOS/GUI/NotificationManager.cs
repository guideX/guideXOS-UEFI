using guideXOS.Kernel.Drivers;
using System.Collections.Generic;
using guideXOS.Misc;

namespace guideXOS.GUI {
    public enum NotificationLevel {
        None,
        Error
    }

    public class Notify {
        public int X, Y;
        public string Message;
        internal string SourceApplicationId;
        public NotificationLevel NotificationLevel;
        public int SWidth;
        public int SHeight;
        internal int AnimationWidth;

        public ulong DisposeUntil;
        public Animation ani;
        internal int LayoutFontGeneration = -1;
        internal int LayoutMaximumWidth = -1;
        internal string LayoutMessage;

        public Notify(string msg, NotificationLevel level = NotificationLevel.None) {
            Initialize(msg, level, null);
        }

        internal Notify(string msg, NotificationLevel level,
                        string sourceApplicationId) {
            Initialize(msg, level, sourceApplicationId);
        }

        private void Initialize(string msg, NotificationLevel level,
                                 string sourceApplicationId) {
            DisposeUntil = 0;
            Message = msg ?? string.Empty;
            SourceApplicationId = sourceApplicationId;
            X = 0; Y = 0;
            SWidth = WindowManager.font == null ? 0 :
                WindowManager.font.MeasureString(Message);
            AnimationWidth = SWidth > int.MaxValue - NotificationManager.Threshold
                ? int.MaxValue - NotificationManager.Threshold : SWidth;
            SHeight = WindowManager.font == null ? 0 : WindowManager.font.FontSize;
            LayoutMessage = Message;
            NotificationLevel = level;

            ani = new Animation()
            {
                MaximumValue = NotificationManager.Threshold + AnimationWidth,
                Stopped = true,
            };
            Animator.AddAnimation(ani);
        }

        public override void Dispose() {
            Message.Dispose();
            Animator.DisposeAnimation(ani);
            base.Dispose();
        }
    }

    public static class NotificationManager {
        static List<Notify> Notifications;

        public static unsafe void Initialize() {
            Notifications = new();

            Add(new Notify("Welcome to guideX os"));
            //Add(new Nofity(Audio.HasAudioDevice ? "Info: Audio controller available" : "Warn: No audio controller found on this PC", Audio.HasAudioDevice ? NotificationLevel.None : NotificationLevel.Error));
            Add(new Notify(HID.Mouse ? "Info: USB mouse available" : "Warn: No USB mouse found on this PC", HID.Mouse ? NotificationLevel.None : NotificationLevel.Error));
            Add(new Notify(HID.Keyboard ? "Info: USB keyboard available" : "Warn: No USB keyboard found on this PC", HID.Keyboard ? NotificationLevel.None : NotificationLevel.Error));
            //if (VMwareTools.Available)
            //Add(new Nofity("VMware Tools is working", NotificationLevel.None));
        }

        internal static void ResetForKernelBoot() {
            // Previous boot objects may belong to a different allocator. Drop the root
            // without traversing or disposing the old notification and animation graph.
            Notifications = new List<Notify>();
        }

        public static void Add(Notify nofity) {
            if (Notifications != null) {
                Notifications.Add(nofity);
            }
        }

        /// <summary>
        /// Enqueue one service-created toast with an internal source tag.
        /// Notify and animation fields remain outside the application service
        /// contract.
        /// </summary>
        public static void AddForApplication(string applicationId,
                                             string message,
                                             NotificationLevel level) {
            if (string.IsNullOrEmpty(applicationId)) return;
            if (Notifications == null) Notifications = new();
            Notifications.Add(new Notify(message, level, applicationId));
        }

        /// <summary>
        /// Clear only service-created notifications from one application
        /// identity.  Legacy source-less notifications are preserved.
        /// </summary>
        public static void ClearForApplication(string applicationId) {
            if (Notifications == null || string.IsNullOrEmpty(applicationId)) return;
            for (int i = Notifications.Count - 1; i >= 0; i--) {
                Notify notification = Notifications[i];
                if (notification == null ||
                        notification.SourceApplicationId != applicationId) continue;
                Notifications.RemoveAt(i);
                notification.Dispose();
            }
        }

        public const int Devide = 30;

        public const int Threshold = 50;

        public const int DisposeUntil = 1000;

        public static void Update() {
            if (Notifications == null || Framebuffer.Graphics == null) return;

            SharedTextFont selected = WindowManager.ShellTextFont;
            int fontGeneration = WindowManager.ShellTextFontGeneration;
            int maximumTextWidth = Framebuffer.Width - Threshold;
            if (maximumTextWidth < 0) maximumTextWidth = 0;
            for (int i = 0; i < Notifications.Count; i++) {
                RefreshTextBounds(Notifications[i], selected,
                    fontGeneration, maximumTextWidth);
            }

            for (int i = 0; i < Notifications.Count; i++) {
                var v = Notifications[i];
                if (v.X < (Threshold + v.AnimationWidth)) {
                    v.ani.Stopped = false;
                    v.X = v.ani.Value;
                    break;
                }
            }

            for (int i = 0; i < Notifications.Count; i++) {
                var v = Notifications[i];

                if (v.X < (Threshold + v.AnimationWidth)) {
                    break;
                } else {
                    if (v.DisposeUntil == 0) {
                        v.DisposeUntil = Timer.Ticks + DisposeUntil;
                    }
                    if (Timer.Ticks > v.DisposeUntil) {
                        Notifications.Remove(v);
                        v.Dispose();
                        break;
                    } else {
                        break;
                    }
                }
            }

            int y = Devide * 2;
            for (int i = 0; i < Notifications.Count; i++) {
                var v = Notifications[i];

                long boxXValue = (long)Framebuffer.Width - v.X +
                    ((long)v.AnimationWidth - v.SWidth);
                if (boxXValue < int.MinValue) boxXValue = int.MinValue;
                if (boxXValue > int.MaxValue) boxXValue = int.MaxValue;
                int boxX = (int)boxXValue;
                int boxY = v.Y + y;
                Framebuffer.Graphics.FillRectangle(boxX, boxY,
                    v.SWidth + Devide, v.SHeight + Devide, 0xFF111111);
                Framebuffer.Graphics.DrawRectangle(boxX, boxY,
                    v.SWidth + Devide, v.SHeight + Devide, 0xFF222222);
                Framebuffer.Graphics.FillRectangle(boxX, boxY, 5,
                    v.SHeight + Devide,
                    v.NotificationLevel == NotificationLevel.None
                        ? 0xFF80B000 : 0xFFE74C3C);
                if (selected != null && v.SWidth > 0 && v.SHeight > 0) {
                    int textX = boxX + (Devide / 2);
                    int textY = boxY + (Devide / 2);
                    ShellTextSurfaceRenderer.DrawWrappedText(
                        Framebuffer.Graphics, selected, v.Message, textX,
                        textY, v.SWidth, 0, 0xFFFFFFFFu, textX, textY,
                        v.SWidth, v.SHeight, SharedTextAlignment.Left);
                }

                y += v.SHeight + Devide;
                y += Devide;
            }
        }

        private static void RefreshTextBounds(Notify notification,
                                              SharedTextFont selected,
                                              int fontGeneration,
                                              int maximumTextWidth) {
            if (notification == null || selected == null) return;
            if (notification.LayoutFontGeneration == fontGeneration &&
                notification.LayoutMaximumWidth == maximumTextWidth &&
                object.ReferenceEquals(notification.LayoutMessage,
                                       notification.Message)) return;

            SharedTextMeasurement measurement;
            bool truncated;
            if (!ShellTextSurfaceRenderer.MeasureWrappedText(selected,
                    notification.Message, maximumTextWidth, 0,
                    out measurement, out truncated)) return;

            notification.SWidth = measurement.Width;
            notification.SHeight = measurement.Height;
            notification.LayoutFontGeneration = fontGeneration;
            notification.LayoutMaximumWidth = maximumTextWidth;
            notification.LayoutMessage = notification.Message;
        }
    }
}
