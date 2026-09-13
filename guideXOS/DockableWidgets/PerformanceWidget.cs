using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Windows.Forms;
using System.Drawing;
using guideXOS.GUI;

namespace guideXOS.DockableWidgets {
    /// <summary>
    /// Minimal floating performance widget showing RAM and CPU usage
    /// </summary>
    internal class PerformanceWidget : DockableWidget {
        private const int WidgetWidth = 140;
        private const int WidgetHeight = 90;
        
        private int _cpuPct = 0;
        private int _memPct = 0;
        private ulong _lastUpdateTick = 0;
        private const ulong UpdateIntervalMs = 500; // Update every 500ms
        
        // Cached strings to prevent memory leak
        private string _cpuText = "0%";
        private string _memText = "0%";
        
        public override int PreferredHeight => WidgetHeight - Padding * 2;
        
        public PerformanceWidget() : base(
            Framebuffer.Width - WidgetWidth - 20,  // Position on right side
            80,  // Y position from top
            WidgetWidth, 
            WidgetHeight
        ) {
            Title = "Performance";
            ShowInStartMenu = false;
            ShowInTaskbar = false;
        }

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
            
            // DrawContent handles metrics update and rendering
            DrawContent(cx, cy, contentWidth);
            
            // Draw close button
            int closeX = X + Width - Padding - CloseBtnSize;
            int closeY = Y + Padding;
            DrawCloseButton(closeX, closeY);
        }
        
        public override void DrawContent(int contentX, int contentY, int contentWidth) {
            // Update performance metrics periodically (works both standalone and in container)
            if (_lastUpdateTick == 0 || Timer.Ticks - _lastUpdateTick >= UpdateIntervalMs) {
                UpdateMetrics();
                _lastUpdateTick = Timer.Ticks;
            }
            
            // CPU Section
            DrawMetric(contentX, contentY, contentWidth, "CPU", _cpuPct, 0xFF5DADE2, _cpuText);
            contentY += 30;
            
            // Memory Section
            DrawMetric(contentX, contentY, contentWidth, "RAM", _memPct, 0xFF58D68D, _memText);
        }
        
        private void DrawMetric(int x, int y, int width, string label, int percent, uint barColor, string cachedText) {
            // Label and percentage
            WindowManager.font.DrawString(x, y, label);
            
            // Use cached text instead of creating new string
            int pctWidth = WindowManager.font.MeasureString(cachedText);
            WindowManager.font.DrawString(x + width - pctWidth, y, cachedText);
            
            // Progress bar
            int barY = y + WindowManager.font.FontSize + 3;
            int barHeight = 6;
            int barWidth = width - CloseBtnSize - 4; // Leave space for close button on first row
            
            // Background
            Framebuffer.Graphics.FillRectangle(x, barY, barWidth, barHeight, 0xFF2A2A2A);
            
            // Filled portion
            int fillWidth = barWidth * percent / 100;
            if (fillWidth > 0) {
                Framebuffer.Graphics.FillRectangle(x, barY, fillWidth, barHeight, barColor);
                
                // Subtle highlight on top
                Framebuffer.Graphics.FillRectangle(x, barY, fillWidth, 1, 0x55FFFFFF);
            }
            
            // Border
            Framebuffer.Graphics.DrawRectangle(x, barY, barWidth, barHeight, 0xFF444444, 1);
        }
        
        private void UpdateMetrics() {
            // Get CPU usage
            int oldCpu = _cpuPct;
            _cpuPct = (int)ThreadPool.CPUUsage;
            if (_cpuPct < 0) _cpuPct = 0;
            if (_cpuPct > 100) _cpuPct = 100;
            
            // USE STRING POOL - no allocations, no dispose needed
            if (_cpuPct != oldCpu) {
                _cpuText = StringPool.GetPercentage(_cpuPct);
            }
            
            // Get memory usage
            int oldMem = _memPct;
            ulong totalMem = Allocator.MemorySize;
            if (totalMem == 0) totalMem = 1; // Avoid division by zero
            ulong usedMem = Allocator.MemoryInUse;
            _memPct = (int)(usedMem * 100UL / totalMem);
            if (_memPct < 0) _memPct = 0;
            if (_memPct > 100) _memPct = 100;
            
            // USE STRING POOL - no allocations, no dispose needed
            if (_memPct != oldMem) {
                _memText = StringPool.GetPercentage(_memPct);
            }

            Program.MarkUefiWidgetUpdated("PerformanceWidget");
        }
    }
}
