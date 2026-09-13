using guideXOS.Graph;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace guideXOS.DockableWidgets {
    /// <summary>
    /// System Monitor widget showing CPU and RAM usage with line charts
    /// </summary>
    internal class Monitor : DockableWidget {
        class Chart {
            public Image image;
            public Graphics graphics;
            public int lastValue;
            public string name;
            public int writeX;
            // Keep one label for each bounded percentage value.  The monitor is
            // drawn every frame, so changing values must not churn the heap.
            public string[] cachedLabels;
            
            public Chart(int Width, int Height, string Name) {
                image = new Image(Width, Height);
                graphics = Graphics.FromImage(image);
                lastValue = 100;
                name = Name;
                writeX = 0;
                cachedLabels = new string[101];
            }
        }
        
        Chart CPUUsage;
        Chart RAMUsage;
        
        private const int ChartWidth = 100;
        private const int ChartHeight = 80;
        private const int WidgetWidth = ChartWidth * 2 + Padding * 3;
        private const int WidgetHeight = ChartHeight + Padding * 3 + 20; // Extra space for labels
        private ulong _lastUpdateTick;
        private const ulong UpdateIntervalMs = 500;
        
        public override int PreferredHeight => ChartHeight + Padding + 20;
        
        public Monitor() : base(
            Framebuffer.Width / 2 - WidgetWidth / 2, // Center horizontally
            100,
            WidgetWidth,
            WidgetHeight
        ) {
            Title = "System Monitor";
            ShowInStartMenu = false;
            ShowInTaskbar = false;
            CPUUsage = new Chart(ChartWidth, ChartHeight, "CPU");
            RAMUsage = new Chart(ChartWidth, ChartHeight, "RAM");
        }
        
        const int LineWidth = 1;
        
        public override void OnInput() {
            if (!Visible) return;
            
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            
            // Close button hit test
            int closeX = X + Width - Padding - CloseBtnSize;
            int closeY = Y + Padding;
            _closeHover = mx >= closeX && mx <= closeX + CloseBtnSize && 
                          my >= closeY && my <= closeY + CloseBtnSize;
            
            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            
            if (leftDown && _closeHover) {
                Visible = false;
                return;
            }
            
            // Handle dragging
            HandleDragging();
        }
        
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
            
            // DrawContent handles rendering
            DrawContent(cx, cy, contentWidth);
            
            // Draw close button
            int closeX = X + Width - Padding - CloseBtnSize;
            int closeY = Y + Padding;
            DrawCloseButton(closeX, closeY);
        }
        
        public override void DrawContent(int contentX, int contentY, int contentWidth) {
            ulong now = Timer.Ticks;
            if (_lastUpdateTick == 0 || now - _lastUpdateTick >= UpdateIntervalMs) {
                DrawLineChart((int)ThreadPool.CPUUsage, ref CPUUsage.lastValue, CPUUsage, 0xFF5DADE2);
                DrawLineChart((int)(Allocator.MemoryInUse * 100 / (Allocator.MemorySize == 0 ? 1 : Allocator.MemorySize)), ref RAMUsage.lastValue, RAMUsage, 0xFF58D68D);
                _lastUpdateTick = now;
                Program.MarkUefiWidgetUpdated("Monitor");
            }
            
            int aX = contentX;
            Render(ref aX, contentY, CPUUsage, (int)ThreadPool.CPUUsage);
            Render(ref aX, contentY, RAMUsage, (int)(Allocator.MemoryInUse * 100 / (Allocator.MemorySize == 0 ? 1 : Allocator.MemorySize)));
        }
        
        private void Render(ref int aX, int baseY, Chart chart, int pct) {
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;

            string cachedLabel = chart.cachedLabels[pct];
            if (cachedLabel == null) {
                // StringPool owns the percentage string; retain the combined
                // label for the life of this small, fixed-range chart.
                cachedLabel = chart.name + " " + StringPool.GetPercentage(pct);
                chart.cachedLabels[pct] = cachedLabel;
            }

            int textWidth = WindowManager.font.MeasureString(cachedLabel);
            WindowManager.font.DrawString(aX + chart.graphics.Width / 2 - textWidth / 2, baseY, cachedLabel);
            
            // Draw chart image
            int chartY = baseY + WindowManager.font.FontSize + 4;
            Framebuffer.Graphics.DrawImage(aX, chartY, chart.image, true);
            
            // Draw chart border
            Framebuffer.Graphics.DrawRectangle(aX, chartY, chart.graphics.Width, chart.graphics.Height, 0xFF333333);
            
            aX += chart.graphics.Width + Padding;
        }
        
        private void DrawLineChart(int value, ref int lastValue, Chart chart, uint Color) {
            int h = chart.graphics.Height;
            int w = chart.graphics.Width;
            int val = value;
            if (val < 0) val = 0;
            if (val > 100) val = 100;
            int y = h - h * val / 100 - 1;
            if (y < 0) y = 0;
            
            // Clear column
            chart.graphics.FillRectangle(chart.writeX, 0, LineWidth, h, 0xFF222222);
            
            // Draw line from previous value
            int prevY = h - h * lastValue / 100 - 1;
            if (prevY < 0) prevY = 0;
            chart.graphics.DrawLine(chart.writeX, prevY, chart.writeX, y, Color);
            
            lastValue = val;
            chart.writeX += LineWidth;
            if (chart.writeX >= w) {
                chart.writeX = 0;
                // Wrap: fade entire surface
                chart.graphics.FillRectangle(0, 0, w, h, 0xFF222222);
            }
        }
    }
}
