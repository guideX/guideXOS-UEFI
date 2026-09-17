using guideXOS.Kernel.Drivers;
using guideXOS.Graph;
using guideXOS.Misc;
using System.Windows.Forms;
using System.Drawing;
using System;
using guideXOS.GUI;
using System.Collections.Generic;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// Task Manager window with Processes, Performance, Tombstoned, and Memory Details tabs
    /// </summary>
    internal class TaskManager : Window {
        // Tabs
        private int _tabH = 28;
        private int _tabGap = 6;
        private int _currentTab = 0; // 0 = Processes, 1 = Performance, 2 = Tombstoned, 3 = Memory Details

        // Layout
        private int _padding = 10;
        private int _rowHeight = 24;
        private int _scrollOffset = 0;
        private bool _scrollDrag = false;
        private int _scrollStartY,
            _scrollStartOffset;

        // Selection
        private int _selectedIndex = -1;
        private int _selectedTombIndex = -1;

        // Performance tab navigation
        private int _selectedPerfCategory = 0; // 0 = CPU, 1 = Memory, 2 = Disk, 3 = Network
        private bool _perfNavClickLatch = false;

        // Performance charts
        class Chart {
            public Image image;
            public Graphics graphics;
            public int lastValue;
            public string name;
            public int writeX; // incremental writer to avoid full-surface scroll copy

            public Chart(int width, int height, string name) {
                image = new Image(width, height);
                graphics = Graphics.FromImage(image);
                // Initialize with background color
                graphics.FillRectangle(0, 0, width, height, 0xFF222222);
                lastValue = 0; // Start at 0 instead of 100
                this.name = name;
                writeX = 0;
            }

            // Method to resize chart if needed
            public void EnsureSize(int width, int height) {
                if (image.Width != width || image.Height != height) {
                    // Create new chart with new dimensions
                    image = new Image(width, height);
                    graphics = Graphics.FromImage(image);
                    graphics.FillRectangle(0, 0, width, height, 0xFF222222);
                    writeX = 0;
                    lastValue = 0;
                }
            }
        }
        private Chart _cpuChart;
        private Chart _memChart;
        private Chart _diskChart;
        private Chart _netChart;

        private const int ChartLineWidth = 4; // wider column to make updates more visible
        private ulong _lastPerfTick = 0;

        // Synthetic/derived perf counters for labels and to animate idle systems
        private int _cpuUtilPct;
        private int _memUtilPct;
        private int _diskUtilPct; // synthetic percentage
        private int _netUtilPct; // synthetic percentage
        private int _procCount;
        private int _threadCount;
        private ulong _bytesSent;
        private ulong _bytesRecv;
        private int _netSendKBps;
        private int _netRecvKBps;
        private int _diskReadKBps;
        private int _diskWriteKBps;
        private int _diskActivePct;
        private int _diskRespMs;

        // Owner memory sampling for leak detection - replaced Dictionary with parallel arrays
        private const int MAX_TRACKED_OWNERS = 256;
        private int[] _ownerIds;
        private ulong[] _lastOwnerBytes;
        private int[] _ownerKBps;
        private int _trackedOwnerCount;
        
        private ulong _lastOwnerSampleTick;

        // Flag to enable perf tracking on first draw (not in constructor to prevent freeze)
        private bool _perfTrackingInitialized = false;

        // Memory Details tab tracking
        private ulong _lastPageInUse = 0;
        private ulong _cumulativeAllocatedPages = 0;
        private ulong _cumulativeFreedPages = 0;
        private ulong _lastMemDetailUpdate = 0;
        private List<ulong> _leakHistory; // track allocated-freed over time
        private int _leakHistoryMaxSamples = 60; // 60 samples
        private bool _leakExists = false;
        private int _leakGrowthCounter = 0; // count consecutive growths
        private const int LeakThreshold = 5; // 5 consecutive growths = leak
        
        // Memory leak blame tracking - replaced Dictionary with parallel arrays
        private int[] _blameOwnerIds;
        private ulong[] _ownerHistoryStart;
        private int[] _ownerGrowthCounter;
        private int _blameOwnerCount;

        // Cached strings for Memory Details to reduce per-frame allocations
        private string _mdFreeCallsStr;
        private string _mdFreeSuccessStr;
        private string _mdFailPtrStr;
        private string _mdFailNoPagesStr;
        private string _mdAllocPagesStr;
        private string _mdFreedPagesStr;
        private string _mdPagesInUseStr;
        private string _mdNetGrowthStr;
        private string _mdLeakStr;
        private string _mdFreeAllocRatioStr;
        private string _mdHeapSizeStr;
        private string _mdHeapUsedStr;
        private string _mdHeapFreeStr;
        private string _mdHeapUtilStr;
        private ulong _lastMemLabelUpdateTicks;
        private const int MemLabelUpdateIntervalMs = 1000; // update labels once per second

        public TaskManager(int X, int Y, int Width = 760, int Height = 520)
            : base(X, Y, Width, Height) {
            ShowMinimize = true;
            ShowInTaskbar = true;
            ShowMaximize = true;
            ShowTombstone = false;
            ShowInStartMenu = true;
            IsResizable = true;
            Title = "Task Manager";
            // Initial chart size - will be resized dynamically
            int chartW = 480,
                chartH = 180;
            _cpuChart = new Chart(chartW, chartH, "CPU");
            _memChart = new Chart(chartW, chartH, "Memory");
            _diskChart = new Chart(chartW, chartH, "Disk");
            _netChart = new Chart(chartW, chartH, "Network");

            // DON'T enable perf tracking here - it causes freeze during construction
            //g WindowManager.EnablePerfTracking();

            // initialize owner sampling with parallel arrays
            _ownerIds = new int[MAX_TRACKED_OWNERS];
            _lastOwnerBytes = new ulong[MAX_TRACKED_OWNERS];
            _ownerKBps = new int[MAX_TRACKED_OWNERS];
            _trackedOwnerCount = 0;
            _lastOwnerSampleTick = Timer.Ticks;

            // Initialize memory leak tracking with parallel arrays
            _leakHistory = new List<ulong>(_leakHistoryMaxSamples);
            _blameOwnerIds = new int[MAX_TRACKED_OWNERS];
            _ownerHistoryStart = new ulong[MAX_TRACKED_OWNERS];
            _ownerGrowthCounter = new int[MAX_TRACKED_OWNERS];
            _blameOwnerCount = 0;
        }

        public override void OnSetVisible(bool value) {
            base.OnSetVisible(value);
            if (!value) { // when hiding, stop perf tracking to reduce contention
                WindowManager.DisablePerfTracking();
            }
        }

        public override void OnInput() {
            base.OnInput();
            if (!Visible || IsMinimized)
                return;
            int cx = X + _padding;
            int cy = Y + _padding;
            int cw = Width - _padding * 2;
            int contentY = cy + _tabH + _tabGap;
            int ch = Height - (contentY - Y) - _padding;
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;

            // If right mouse button is pressed within this window, mark input as handled so desktop doesn't open its context menu
            if (Control.MouseButtons.HasFlag(MouseButtons.Right)) {
                if (mx >= X && mx <= X + Width && my >= Y && my <= Y + Height) {
                    WindowManager.MouseHandled = true;
                }
            }

            if (Control.MouseButtons == MouseButtons.Left) {
                // Tab clicks
                int tabCount = 4; // Changed from 3 to 4
                int tabW = (cw - _tabGap * (tabCount - 1)) / tabCount;
                int tx = cx;
                for (int t = 0; t < tabCount; t++) {
                    int tx2 = tx;
                    if (my >= cy && my <= cy + _tabH && mx >= tx2 && mx <= tx2 + tabW) {
                        _currentTab = t;
                        return;
                    }
                    tx += tabW + _tabGap;
                }

                if (_currentTab == 0) {
                    // Processes tab
                    // Hit-test list area
                    int headerH = _rowHeight;
                    int listX = cx;
                    int listY = contentY;
                    int listW = cw;
                    int listH = ch - (_rowHeight + 12); // leave space for footer button

                    // Scrollbar area
                    int sbW = 10;
                    int sbX = X + Width - _padding - sbW;
                    if (mx >= sbX && mx <= sbX + sbW && my >= listY && my <= listY + listH) {
                        if (!_scrollDrag) {
                            _scrollDrag = true;
                            _scrollStartY = my;
                            _scrollStartOffset = _scrollOffset;
                        }
                    }

                    // Row selection
                    if (
                        mx >= listX
                        && mx <= listX + listW
                        && my >= listY + headerH
                        && my <= listY + listH
                    ) {
                        int relativeY = my - (listY + headerH) + _scrollOffset * _rowHeight;
                        int row = relativeY / _rowHeight;
                        if (row < 0)
                            row = 0;
                        if (row < WindowManager.Windows.Count)
                            _selectedIndex = row;
                    }

                    // Footer button End Task
                    int btnW = 120,
                        btnH = 28;
                    int btnX = X + Width - _padding - btnW;
                    int btnY = Y + Height - _padding - btnH;
                    if (mx >= btnX && mx <= btnX + btnW && my >= btnY && my <= btnY + btnH) {
                        OnEndTask();
                    }
                } else if (_currentTab == 1) {
                    // Performance tab navigation
                    if (!_perfNavClickLatch) {
                        int navW = (int)(cw * 0.25f); // 25% for navigation pane
                        int navX = cx;
                        int navY = contentY;
                        int itemH = 70; // height for each navigation item

                        // Check if clicking on navigation items
                        for (int i = 0; i < 4; i++) {
                            int itemY = navY + i * itemH;
                            if (
                                mx >= navX
                                && mx <= navX + navW
                                && my >= itemY
                                && my <= itemY + itemH
                            ) {
                                _selectedPerfCategory = i;
                                _perfNavClickLatch = true;
                                break;
                            }
                        }
                    }
                } else if (_currentTab == 2) {
                    // Tombstoned tab
                    // Hit-test list area
                    int headerH = _rowHeight;
                    int listX = cx;
                    int listY = contentY;
                    int listW = cw;
                    int listH = ch - (_rowHeight + 60); // leave space for buttons

                    // Row selection
                    if (
                        mx >= listX
                        && mx <= listX + listW
                        && my >= listY + headerH
                        && my <= listY + listH
                    ) {
                        int row = (my - (listY + headerH)) / _rowHeight;
                        if (row < 0)
                            row = 0;
                        int tombCount = CountTombstoned();
                        if (row < tombCount)
                            _selectedTombIndex = row;
                    }

                    // Buttons
                    int btnW = 150,
                        btnH = 28;
                    int btnRestoreX = X + Width - _padding - btnW;
                    int btnRestoreY = Y + Height - _padding - btnH * 2 - 8;
                    int btnEndX = X + Width - _padding - btnW;
                    int btnEndY = Y + Height - _padding - btnH;
                    if (
                        mx >= btnRestoreX
                        && mx <= btnRestoreX + btnW
                        && my >= btnRestoreY
                        && my <= btnRestoreY + btnH
                    ) {
                        RestoreTombstoned();
                    }

                    if (
                        mx >= btnEndX
                        && mx <= btnEndX + btnW
                        && my >= btnEndY
                        && my <= btnEndY + btnH
                    ) {
                        EndTombstoned();
                    }
                }
                // Memory Details tab (3) has no interactive elements yet
            } else if (Control.MouseButtons.HasFlag(MouseButtons.None)) {
                _scrollDrag = false;
                _perfNavClickLatch = false;
            }

            // Handle scroll dragging
            if (_scrollDrag) {
                int headerH = _rowHeight;
                int listH = ch - (_rowHeight + 12);
                int maxRows = WindowManager.Windows.Count;
                int rowsVisible = (listH - headerH) / _rowHeight;
                if (rowsVisible < 1)
                    rowsVisible = 1;
                int maxScroll = maxRows - rowsVisible;
                if (maxScroll < 0)
                    maxScroll = 0;
                int dy = my - _scrollStartY;
                _scrollOffset = _scrollStartOffset + dy / _rowHeight;
                if (_scrollOffset < 0)
                    _scrollOffset = 0;
                if (_scrollOffset > maxScroll)
                    _scrollOffset = maxScroll;
            }
        }

        public override void OnDraw() {
            base.OnDraw();

            // Perf tracking is now enabled OUTSIDE of draw loop (in Program.cs after all windows created)
            // so there's no reentrancy issue - we don't enable it here anymore
            if (!_perfTrackingInitialized) {
                _perfTrackingInitialized = true;
                // WindowManager.EnablePerfTracking();  // ← MOVED to Program.cs to avoid reentrancy
            }

            int cx = X + _padding;
            int cy = Y + _padding;
            int cw = Width - _padding * 2;
            int chAll = Height - _padding * 2;
            int contentY = cy + _tabH + _tabGap;
            int ch = chAll - _tabH - _tabGap;

            // Tabs background
            int tabCount = 4; // Changed from 3 to 4
            int tabW = (cw - _tabGap * (tabCount - 1)) / tabCount;
            int tx = cx;
            DrawTab(tx, cy, tabW, _tabH, "Processes", _currentTab == 0);
            tx += tabW + _tabGap;
            DrawTab(tx, cy, tabW, _tabH, "Performance", _currentTab == 1);
            tx += tabW + _tabGap;
            DrawTab(tx, cy, tabW, _tabH, "Tombstoned", _currentTab == 2);
            tx += tabW + _tabGap;
            DrawTab(tx, cy, tabW, _tabH, "Memory Details", _currentTab == 3);

            // Content panel
            Framebuffer.Graphics.FillRectangle(cx, contentY, cw, ch, 0xFF1E1E1E);
            Framebuffer.Graphics.DrawRectangle(cx - 1, contentY - 1, cw + 2, ch + 2, 0xFF333333, 1);

            if (_currentTab == 0)
                DrawProcesses(cx, contentY, cw, ch);
            else if (_currentTab == 1)
                DrawPerformance(cx, contentY, cw, ch);
            else if (_currentTab == 2)
                DrawTombstoned(cx, contentY, cw, ch);
            else if (_currentTab == 3)
                DrawMemoryDetails(cx, contentY, cw, ch);
        }

        private void DrawTombstoned(int x, int y, int w, int h) {
            int headerH = _rowHeight;
            DrawHeaderCell(x, y, w, headerH, "Tombstoned Apps");
            int listY = y + headerH;
            int listH = h - headerH - 60; // leave space for buttons
            int dy = listY;
            int tombCount = CountTombstoned();
            for (int i = 0; i < tombCount; i++) {
                var win = GetTombstonedAt(i);
                bool sel = i == _selectedTombIndex;
                if (sel)
                    Framebuffer.Graphics.FillRectangle(x, dy, w, _rowHeight, 0xFF2A2A2A);
                string name = win != null ? win.Title ?? "(no title)" : "(null)";
                WindowManager.font.DrawString(
                    x + 6,
                    dy + 6,
                    name,
                    w - 12,
                    WindowManager.font.FontSize
                );
                dy += _rowHeight;
            }

            // Buttons
            int btnW = 150,
                btnH = 28;
            int btnRestoreX = X + Width - _padding - btnW;
            int btnRestoreY = Y + Height - _padding - btnH * 2 - 8;
            int btnEndX = X + Width - _padding - btnW;
            int btnEndY = Y + Height - _padding - btnH;
            Framebuffer.Graphics.FillRectangle(btnRestoreX, btnRestoreY, btnW, btnH, 0xFF26482E);
            WindowManager.font.DrawString(
                btnRestoreX + 10,
                btnRestoreY + (btnH / 2 - WindowManager.font.FontSize / 2),
                "Restore"
            );
            Framebuffer.Graphics.FillRectangle(btnEndX, btnEndY, btnW, btnH, 0xFF482626);
            WindowManager.font.DrawString(
                btnEndX + 10,
                btnEndY + (btnH / 2 - WindowManager.font.FontSize / 2),
                "End Tombstoned App"
            );
        }

        private int CountTombstoned() {
            int c = 0;
            for (int i = 0; i < WindowManager.Windows.Count; i++) {
                if (WindowManager.Windows[i].IsTombstoned)
                    c++;
            }
            return c;
        }

        private Window GetTombstonedAt(int idx) {
            int c = 0;
            for (int i = 0; i < WindowManager.Windows.Count; i++) {
                var w = WindowManager.Windows[i];
                if (w.IsTombstoned) {
                    if (c == idx)
                        return w;
                    c++;
                }
            }
            return null;
        }

        private void RestoreTombstoned() {
            if (_selectedTombIndex < 0)
                return;
            var w = GetTombstonedAt(_selectedTombIndex);
            if (w != null) {
                w.Untombstone();
                WindowManager.MoveToEnd(w);
            }
        }

        private void EndTombstoned() {
            if (_selectedTombIndex < 0)
                return;
            var w = GetTombstonedAt(_selectedTombIndex);
            if (w != null) {
                // Dispose the window properly
                w.Dispose();

                // Remove from window list
                WindowManager.Windows.Remove(w);
                _selectedTombIndex = -1;
            }
        }

        private void DrawTab(int x, int y, int w, int h, string title, bool selected) {
            uint bg = selected ? 0xFF2C2C2C : 0xFF242424;
            Framebuffer.Graphics.FillRectangle(x, y, w, h, bg);
            Framebuffer.Graphics.DrawRectangle(x, y, w, h, 0xFF3A3A3A, 1);
            int tx = x + w / 2 - WindowManager.font.MeasureString(title) / 2;
            int ty = y + h / 2 - WindowManager.font.FontSize / 2;
            WindowManager.font.DrawString(tx, ty, title);
        }

        private void DrawProcesses(int x, int y, int w, int h) {
            // Sample owner bytes less frequently to avoid freezing - changed from 500ms to 2000ms
            if ((long)(Timer.Ticks - _lastOwnerSampleTick) >= 2000) {
                try {
                    SampleOwnerBytes();
                    _lastOwnerSampleTick = Timer.Ticks;
                } catch {
                    // If sampling fails, skip it this frame to prevent freeze
                }
            }

            int headerH = _rowHeight;

            // columns: Name, CPU%, Memory, Disk%, Network%
            int colNameW = w - 380;
            if (colNameW < 120)
                colNameW = 120;
            int colCpuW = 60;
            int colMemW = 140; // show absolute usage
            int colDiskW = 80;
            int colNetW = 80;

            // Header
            int hx = x;
            int hy = y;
            DrawHeaderCell(hx, hy, colNameW, headerH, "Name");
            hx += colNameW;
            DrawHeaderCell(hx, hy, colCpuW, headerH, "CPU%");
            hx += colCpuW;
            DrawHeaderCell(hx, hy, colMemW, headerH, "Memory");
            hx += colMemW;
            DrawHeaderCell(hx, hy, colDiskW, headerH, "Disk%");
            hx += colDiskW;
            DrawHeaderCell(hx, hy, colNetW, headerH, "Network%");

            // List area
            int listY = y + headerH;
            int listH = h - headerH - 40;
            if (listH < headerH)
                listH = headerH;
            int startRow = _scrollOffset;
            int rowsVisible = listH / _rowHeight;
            if (rowsVisible < 1)
                rowsVisible = 1;
            int totalRows = WindowManager.Windows.Count;
            if (startRow > totalRows)
                startRow = totalRows;
            int endRow = startRow + rowsVisible;
            if (endRow > totalRows)
                endRow = totalRows;

            int dy = listY;
            for (int i = startRow; i < endRow; i++) {
                var wdw = WindowManager.Windows[i];
                bool sel = i == _selectedIndex;
                uint bg = sel ? 0xFF2A2A2A : 0x00000000u;
                if (bg != 0)
                    Framebuffer.Graphics.FillRectangle(x, dy, w, _rowHeight, bg);
                int cx = x;
                string name = wdw.Title ?? "(no title)";
                WindowManager.font.DrawString(
                    cx + 6,
                    dy + 6,
                    name,
                    colNameW - 12,
                    WindowManager.font.FontSize
                );
                cx += colNameW;

                // CPU: per-window counter via WindowManager - USE STRING POOL
                int ownerId = wdw.OwnerId;
                int cpuPct = WindowManager.GetWindowCpuPct(ownerId);
                string cpuStr = StringPool.GetNumber(cpuPct);
                WindowManager.font.DrawString(cx + 6, dy + 6, cpuStr);
                cx += colCpuW;

                // Memory per window: use allocator per-owner accounting with try-catch to prevent freeze
                ulong bytes = 0;
                try {
                    bytes = Allocator.GetOwnerBytes(ownerId);
                    // Also get total memory in use for debugging - show global total for now
                    if (bytes == 0) {
                        // No owner-specific memory found - show global memory divided by window count for rough estimate
                        ulong globalMem = Allocator.MemoryInUse;
                        int winCount = WindowManager.Windows.Count;
                        if (winCount > 0)
                            bytes = globalMem / (ulong)winCount;
                    }
                } catch {
                    // If memory query fails, show 0
                    bytes = 0;
                }

                // USE STRING POOL - eliminates major leak source
                string memText = StringPool.GetMemorySize(bytes);
                WindowManager.font.DrawString(cx + 6, dy + 6, memText);
                cx += colMemW;

                // Disk/Net per-window not implemented
                WindowManager.font.DrawString(cx + 6, dy + 6, "-");
                cx += colDiskW;
                WindowManager.font.DrawString(cx + 6, dy + 6, "-");
                dy += _rowHeight;
            }

            // Scrollbar
            int sbW = 10;
            int sbX = X + Width - _padding - sbW;
            Framebuffer.Graphics.FillRectangle(sbX, listY, sbW, listH, 0xFF1A1A1A);
            int thumbH = rowsVisible * listH / (totalRows == 0 ? 1 : totalRows);
            if (thumbH < 16)
                thumbH = 16;
            if (thumbH > listH)
                thumbH = listH;
            int maxScroll = totalRows - rowsVisible;
            if (maxScroll < 0)
                maxScroll = 0;
            int thumbY =
                listY + (maxScroll == 0 ? 0 : _scrollOffset * (listH - thumbH) / maxScroll);
            Framebuffer.Graphics.FillRectangle(sbX, thumbY, sbW, thumbH, 0xFF3A3A3A);

            // Footer - End Task button
            int btnW = 120,
                btnH = 28;
            int btnX = X + Width - _padding - btnW;
            int btnY = Y + Height - _padding - btnH;
            Framebuffer.Graphics.FillRectangle(btnX, btnY, btnW, btnH, 0xFF3B1E1E);
            WindowManager.font.DrawString(
                btnX + 10,
                btnY + (btnH / 2 - WindowManager.font.FontSize / 2),
                "End Task"
            );

            // Task Manager observes the bounded app-model projection only.
            // This deliberately does not enumerate or reorder WindowManager.Windows
            // and does not invoke lifecycle, ownership, or focus operations.
            int observedInstances = guideXOS.OS.ApplicationInstanceRegistry.ObservationCount;
            int activeInstances = 0;
            int suspendedInstances = 0;
            for (int i = 0; i < observedInstances; i++) {
                guideXOS.OS.ApplicationInstanceObservation observation;
                if (!guideXOS.OS.ApplicationInstanceRegistry.TryGetObservationAt(
                        i, out observation)) continue;
                if (observation.IsActivated) activeInstances++;
                if (observation.IsSuspended) suspendedInstances++;
            }
            WindowManager.font.DrawString(
                x,
                btnY + (btnH / 2 - WindowManager.font.FontSize / 2),
                "Instances: " + observedInstances.ToString() +
                "  Active: " + activeInstances.ToString() +
                "  Suspended: " + suspendedInstances.ToString());
        }

        private void DrawHeaderCell(int x, int y, int w, int h, string text) {
            Framebuffer.Graphics.FillRectangle(x, y, w, h, 0xFF252525);
            Framebuffer.Graphics.DrawRectangle(x, y, w, h, 0xFF333333, 1);
            WindowManager.font.DrawString(
                x + 6,
                y + (h / 2 - WindowManager.font.FontSize / 2),
                text
            );
        }

        private static int WavePct(ulong ticks, int period) {
            int t = (int)(ticks % (ulong)period);
            int up = period / 2;
            if (t < up)
                return t * 100 / up;
            else
                return (period - t) * 100 / up;
        }

        private static string ToMBString(ulong bytes) {
            // Use StringPool instead of allocating new strings
            return StringPool.GetMemorySize(bytes);
        }

        private static string ToKBpsString(int kbps) {
            // Use StringPool for transfer rates
            return StringPool.GetTransferRate(kbps);
        }

        private static string FormatUptime(ulong ticks) {
            // Use StringPool for uptime formatting
            return StringPool.FormatUptime(ticks);
        }

        private void DrawPerformance(int x, int y, int w, int h) {
            // Update charts less often to reduce work
            if (_lastPerfTick != Timer.Ticks && Timer.Ticks % 10 == 0) {
                _lastPerfTick = Timer.Ticks;

                // CPU
                _cpuUtilPct = (int)ThreadPool.CPUUsage;
                if (_cpuUtilPct < 0)
                    _cpuUtilPct = 0;
                if (_cpuUtilPct > 100)
                    _cpuUtilPct = 100;
                UpdateChart(_cpuChart, _cpuUtilPct, 0xFF5DADE2);

                // Memory - fix calculation
                ulong totalMem = Allocator.MemorySize == 0 ? 1UL : Allocator.MemorySize;
                ulong usedMem = Allocator.MemoryInUse;
                _memUtilPct = (int)(usedMem * 100UL / totalMem);
                if (_memUtilPct < 0)
                    _memUtilPct = 0;
                if (_memUtilPct > 100)
                    _memUtilPct = 100;
                UpdateChart(_memChart, _memUtilPct, 0xFF58D68D);

                // Disk (synthetic animation so chart isn't flat). If real stats are added later, replace here.
                _diskUtilPct = WavePct(Timer.Ticks, 240);
                _diskActivePct = WavePct(Timer.Ticks + 60, 300);
                _diskReadKBps = _diskUtilPct * 4; // up to ~400 KB/s
                _diskWriteKBps = _diskActivePct * 3 / 2; // up to ~150 KB/s
                _diskRespMs = 1 + _diskActivePct / 10;
                UpdateChart(_diskChart, _diskUtilPct, 0xFFE67E22);

                // Network (synthetic animation). If NET driver exposes counters, wire them here.
                _netUtilPct = WavePct(Timer.Ticks + 120, 280);
                _netSendKBps = _netUtilPct * 2; // up to ~200 KB/s
                _netRecvKBps = (100 - _netUtilPct) * 2;
                // accumulate bytes for labels (rough estimate per tick quantum)
                _bytesSent += (ulong)(_netSendKBps * 1024 / 10);
                _bytesRecv += (ulong)(_netRecvKBps * 1024 / 10);
                UpdateChart(_netChart, _netUtilPct, 0xFF9B59B6);

                // Other labels
                _procCount = WindowManager.Windows.Count;
                _threadCount = ThreadPool.ThreadCount;

                // Sample owner bytes every ~1000ms to compute KB/s per owner
                if ((long)(Timer.Ticks - _lastOwnerSampleTick) >= 1000) {
                    SampleOwnerBytes();
                    _lastOwnerSampleTick = Timer.Ticks;
                }
            }

            // Windows-style layout: left sidebar + right detail pane
            int navW = (int)(w * 0.25f); // 25% for navigation
            int detailW = w - navW - 12; // remaining width for detail pane, minus gap
            int navX = x;
            int detailX = x + navW + 12;

            // Dynamically size charts to fit available space
            int chartW = Math.Min(detailW - 20, 600); // max 600px wide
            int chartH = Math.Min((h - 200) / 1, 220); // leave room for title and details
            if (chartW < 200)
                chartW = 200;
            if (chartH < 120)
                chartH = 120;

            // Ensure charts are properly sized
            _cpuChart.EnsureSize(chartW, chartH);
            _memChart.EnsureSize(chartW, chartH);
            _diskChart.EnsureSize(chartW, chartH);
            _netChart.EnsureSize(chartW, chartH);

            // Draw navigation pane background
            Framebuffer.Graphics.FillRectangle(navX, y, navW, h, 0xFF181818);

            // Draw navigation items
            string[] navLabels = { "CPU", "Memory", "Disk", "Network" };
            uint[] navColors = { 0xFF5DADE2, 0xFF58D68D, 0xFFE67E22, 0xFF9B59B6 };
            int[] navValues = { _cpuUtilPct, _memUtilPct, _diskUtilPct, _netUtilPct };

            int itemH = Math.Min(70, h / 4); // Divide space evenly
            int miniGraphW = 50;
            int miniGraphH = 30;

            for (int i = 0; i < 4; i++) {
                int itemY = y + i * itemH;
                bool selected = i == _selectedPerfCategory;

                // Background for selected item
                if (selected) {
                    Framebuffer.Graphics.FillRectangle(navX, itemY, navW, itemH, 0xFF252525);
                }

                // Separator line
                if (i > 0) {
                    Framebuffer.Graphics.DrawRectangle(navX + 4, itemY, navW - 8, 1, 0xFF333333, 1);
                }

                // Draw mini graph (simplified line chart)
                int graphX = navX + 8;
                int graphY = itemY + 32;
                Chart chart = GetChartForCategory(i);
                DrawMiniGraph(graphX, graphY, miniGraphW, miniGraphH, chart, navColors[i]);

                // Label and percentage
                int textX = graphX + miniGraphW + 8;
                int labelY = itemY + 8;
                WindowManager.font.DrawString(textX, labelY, navLabels[i]);

                // USE STRING POOL - no more allocations here!
                string pctText = StringPool.GetPercentage(navValues[i]);
                int pctY = labelY + WindowManager.font.FontSize + 4;
                WindowManager.font.DrawString(textX, pctY, pctText);
                // NO DISPOSE NEEDED - pooled strings are reused
            }

            // Draw separator between navigation and detail
            Framebuffer.Graphics.FillRectangle(detailX - 6, y, 1, h, 0xFF333333);

            // Draw detail pane based on selected category
            switch (_selectedPerfCategory) {
                case 0:
                    DrawCpuDetail(detailX, y, detailW, h);
                    break;
                case 1:
                    DrawMemDetail(detailX, y, detailW, h);
                    break;
                case 2:
                    DrawDiskDetail(detailX, y, detailW, h);
                    break;
                case 3:
                    DrawNetDetail(detailX, y, detailW, h);
                    break;
            }
        }

        private Chart GetChartForCategory(int category) {
            switch (category) {
                case 0:
                    return _cpuChart;
                case 1:
                    return _memChart;
                case 2:
                    return _diskChart;
                case 3:
                    return _netChart;
                default:
                    return _cpuChart;
            }
        }

        private void DrawMiniGraph(int x, int y, int w, int h, Chart chart, uint color) {
            // Draw a simplified version of the chart for the navigation pane
            Framebuffer.Graphics.FillRectangle(x, y, w, h, 0xFF222222);

            // Sample every Nth point to fit in the mini graph
            int chartW = chart.graphics.Width;
            int sampleInterval = Math.Max(1, chartW / w);

            for (int i = 0; i < w; i++) {
                int srcX = (chart.writeX + i * sampleInterval) % chartW;

                // Read pixel value from chart to determine height
                uint pixel = chart.image.GetPixel(srcX, h / 2);
                if (pixel != 0xFF222222 && pixel != 0) { // not background
                    // Find the colored pixel in this column
                    for (int sy = 0; sy < chart.graphics.Height; sy++) {
                        uint p = chart.image.GetPixel(srcX, sy);
                        if (p == color) {
                            // Map to mini graph coordinates
                            int miniY = y + sy * h / chart.graphics.Height;
                            Framebuffer.Graphics.FillRectangle(x + i, miniY, 1, 1, color);
                            break;
                        }
                    }
                }
            }

            Framebuffer.Graphics.DrawRectangle(x, y, w, h, 0xFF444444, 1);
        }

        private void DrawCpuDetail(int x, int y, int w, int h) {
            // Title
            string title = "CPU";
            WindowManager.font.DrawString(x, y, title);

            // Large graph
            int graphY = y + WindowManager.font.FontSize + 12;
            int graphH = Math.Min(_cpuChart.graphics.Height, h - WindowManager.font.FontSize - 120);
            Framebuffer.Graphics.DrawImage(x, graphY, _cpuChart.image, true);
            Framebuffer.Graphics.DrawRectangle(
                x,
                graphY,
                _cpuChart.graphics.Width,
                _cpuChart.graphics.Height,
                0xFF333333,
                1
            );

            // Utilization percentage on graph - USE STRING POOL (no dispose needed)
            string pct = StringPool.GetPercentage(_cpuUtilPct);
            int pctX = x + _cpuChart.graphics.Width - WindowManager.font.MeasureString(pct) - 8;
            WindowManager.font.DrawString(pctX, graphY + 8, pct);

            // Details below graph
            int detailY = graphY + _cpuChart.graphics.Height + 16;
            int col1X = x;
            int col2X = x + w / 2;

            WindowManager.font.DrawString(col1X, detailY, "Utilization:");
            string utilPctStr = StringPool.GetPercentage(_cpuUtilPct);
            WindowManager.font.DrawString(col2X, detailY, utilPctStr);
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Speed:");
            WindowManager.font.DrawString(col2X, detailY, "N/A");
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Processes:");
            string procStr = StringPool.GetNumber(_procCount);
            WindowManager.font.DrawString(col2X, detailY, procStr);
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Threads:");
            string threadStr = StringPool.GetNumber(_threadCount);
            WindowManager.font.DrawString(col2X, detailY, threadStr);
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Machine time:");
            string uptimeStr = StringPool.FormatUptime(Timer.Ticks);
            WindowManager.font.DrawString(col2X, detailY, uptimeStr);
        }

        private void DrawMemDetail(int x, int y, int w, int h) {
            // Title
            string title = "Memory";
            WindowManager.font.DrawString(x, y, title);

            // Large graph
            int graphY = y + WindowManager.font.FontSize + 12;
            Framebuffer.Graphics.DrawImage(x, graphY, _memChart.image, true);
            Framebuffer.Graphics.DrawRectangle(
                x,
                graphY,
                _memChart.graphics.Width,
                _memChart.graphics.Height,
                0xFF333333,
                1
            );

            // Utilization percentage on graph - USE STRING POOL (no dispose needed)
            string pct = StringPool.GetPercentage(_memUtilPct);
            int pctX = x + _memChart.graphics.Width - WindowManager.font.MeasureString(pct) - 8;
            WindowManager.font.DrawString(pctX, graphY + 8, pct);

            // Details below graph
            int detailY = graphY + _memChart.graphics.Height + 16;
            int col1X = x;
            int col2X = x + w / 2;

            ulong total = Allocator.MemorySize;
            ulong used = Allocator.MemoryInUse;
            ulong avail = total > used ? total - used : 0UL;

            WindowManager.font.DrawString(col1X, detailY, "In use:");
            // USE STRING POOL - single string, no concatenation allocations
            string usedMBStr = StringPool.GetMemorySize(used);
            string memPctStr = StringPool.GetPercentage(_memUtilPct);
            string usedStr = usedMBStr + " (" + memPctStr + ")";
            WindowManager.font.DrawString(col2X, detailY, usedStr);
            usedStr.Dispose(); // Dispose the concatenated result only
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Available:");
            string availStr = StringPool.GetMemorySize(avail);
            WindowManager.font.DrawString(col2X, detailY, availStr);
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Total:");
            string totalStr = StringPool.GetMemorySize(total);
            WindowManager.font.DrawString(col2X, detailY, totalStr);
            detailY += WindowManager.font.FontSize + 6;

            int topOwner = 0;
            int topKbps = 0;
            // Use parallel arrays instead of Dictionary
            for (int i = 0; i < _trackedOwnerCount; i++) {
                int val = _ownerKBps[i];
                if (Math.Abs(val) > Math.Abs(topKbps)) {
                    topKbps = val;
                    topOwner = _ownerIds[i];
                }
            }
            WindowManager.font.DrawString(col1X, detailY, "Top owner:");
            if (topOwner != 0) {
                string ownerIdStr = StringPool.GetNumber(topOwner);
                string sign = topKbps > 0 ? "+" : "";
                string kbpsStr = topKbps.ToString(); // Keep this as is since it can be negative
                string ownerStr = "#" + ownerIdStr + " " + sign + kbpsStr + " KB/s";
                WindowManager.font.DrawString(col2X, detailY, ownerStr);
                ownerStr.Dispose();
                kbpsStr.Dispose();
            } else {
                WindowManager.font.DrawString(col2X, detailY, "N/A");
            }
            detailY += WindowManager.font.FontSize + 6;

            int topTag = -1;
            ulong topTagBytes = 0;
            for (int t = 0; t < (int)Allocator.AllocTag.Count; t++) {
                ulong tb = Allocator.GetTagBytes((Allocator.AllocTag)t);
                if (tb > topTagBytes) {
                    topTagBytes = tb;
                    topTag = t;
                }
            }
            WindowManager.font.DrawString(col1X, detailY, "Top tag:");
            if (topTag >= 0) {
                string tagName = ((Allocator.AllocTag)topTag).ToString();
                string tagBytes = StringPool.GetMemorySize(topTagBytes);
                string tagStr = tagName + " " + tagBytes;
                WindowManager.font.DrawString(col2X, detailY, tagStr);
                tagStr.Dispose();
                tagName.Dispose();
            } else {
                WindowManager.font.DrawString(col2X, detailY, "N/A");
            }
        }

        private void DrawDiskDetail(int x, int y, int w, int h) {
            // Title
            string title = "Disk";
            WindowManager.font.DrawString(x, y, title);

            // Large graph
            int graphY = y + WindowManager.font.FontSize + 12;
            Framebuffer.Graphics.DrawImage(x, graphY, _diskChart.image, true);
            Framebuffer.Graphics.DrawRectangle(
                x,
                graphY,
                _diskChart.graphics.Width,
                _diskChart.graphics.Height,
                0xFF333333,
                1
            );

            // Utilization percentage on graph - USE STRING POOL (no dispose needed)
            string pct = StringPool.GetPercentage(_diskUtilPct);
            int pctX = x + _diskChart.graphics.Width - WindowManager.font.MeasureString(pct) - 8;
            WindowManager.font.DrawString(pctX, graphY + 8, pct);

            // Details below graph
            int detailY = graphY + _diskChart.graphics.Height + 16;
            int col1X = x;
            int col2X = x + w / 2;

            WindowManager.font.DrawString(col1X, detailY, "Active time:");
            string activePctStr = StringPool.GetPercentage(_diskActivePct);
            WindowManager.font.DrawString(col2X, detailY, activePctStr);
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Avg response time:");
            string respStr = StringPool.WithSuffix(_diskRespMs, " ms");
            WindowManager.font.DrawString(col2X, detailY, respStr);
            respStr.Dispose(); // This creates a new string, so dispose it
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Read speed:");
            string readStr = StringPool.GetTransferRate(_diskReadKBps);
            WindowManager.font.DrawString(col2X, detailY, readStr);
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Write speed:");
            string writeStr = StringPool.GetTransferRate(_diskWriteKBps);
            WindowManager.font.DrawString(col2X, detailY, writeStr);
        }

        private void DrawNetDetail(int x, int y, int w, int h) {
            // Title
            string title = "Network";
            WindowManager.font.DrawString(x, y, title);

            // Large graph
            int graphY = y + WindowManager.font.FontSize + 12;
            Framebuffer.Graphics.DrawImage(x, graphY, _netChart.image, true);
            Framebuffer.Graphics.DrawRectangle(
                x,
                graphY,
                _netChart.graphics.Width,
                _netChart.graphics.Height,
                0xFF333333,
                1
            );

            // Utilization percentage on graph - USE STRING POOL (no dispose needed)
            string pct = StringPool.GetPercentage(_netUtilPct);
            int pctX = x + _netChart.graphics.Width - WindowManager.font.MeasureString(pct) - 8;
            WindowManager.font.DrawString(pctX, graphY + 8, pct);

            // Details below graph
            int detailY = graphY + _netChart.graphics.Height + 16;
            int col1X = x;
            int col2X = x + w / 2;

            WindowManager.font.DrawString(col1X, detailY, "Send:");
            string sendStr = StringPool.GetTransferRate(_netSendKBps);
            WindowManager.font.DrawString(col2X, detailY, sendStr);
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Receive:");
            string recvStr = StringPool.GetTransferRate(_netRecvKBps);
            WindowManager.font.DrawString(col2X, detailY, recvStr);
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Sent bytes:");
            string sentStr = StringPool.GetNumber(_bytesSent);
            WindowManager.font.DrawString(col2X, detailY, sentStr);
            detailY += WindowManager.font.FontSize + 6;

            WindowManager.font.DrawString(col1X, detailY, "Received bytes:");
            string recvBytesStr = StringPool.GetNumber(_bytesRecv);
            WindowManager.font.DrawString(col2X, detailY, recvBytesStr);
        }

        private void UpdateChart(Chart chart, int valuePct, uint color) {
            if (valuePct < 0)
                valuePct = 0;
            if (valuePct > 100)
                valuePct = 100;
            int h = chart.graphics.Height;
            int w = chart.graphics.Width;

            // Clear current column
            chart.graphics.FillRectangle(chart.writeX, 0, ChartLineWidth, h, 0xFF222222);

            // Compute y positions (invert for top origin)
            int newY = h - (h * valuePct / 100);
            if (newY < 0)
                newY = 0;
            if (newY >= h)
                newY = h - 1;

            int prevY = h - (h * chart.lastValue / 100);
            if (prevY < 0)
                prevY = 0;
            if (prevY >= h)
                prevY = h - 1;

            // Draw vertical line from previous to new value
            if (prevY < newY) {
                // Line going down (value decreasing)
                for (int dy = prevY; dy <= newY; dy++) {
                    chart.graphics.FillRectangle(chart.writeX, dy, ChartLineWidth, 1, color);
                }
            } else {
                // Line going up (value increasing)
                for (int dy = newY; dy <= prevY; dy++) {
                    chart.graphics.FillRectangle(chart.writeX, dy, ChartLineWidth, 1, color);
                }
            }

            // Store current value for next iteration
            chart.lastValue = valuePct;

            // Move write position
            chart.writeX += ChartLineWidth;
            if (chart.writeX >= w) {
                chart.writeX = 0;
                chart.graphics.FillRectangle(0, 0, w, h, 0xFF222222);
            }
        }

        private void OnEndTask() {
            if (_selectedIndex < 0)
                return;
            if (_selectedIndex >= WindowManager.Windows.Count) {
                _selectedIndex = -1;
                return;
            }

            Window target = WindowManager.Windows[_selectedIndex];
            if (target == this)
                return; // do not end self via button

            // CRITICAL FIX: Dispose window to free all memory
            target.Dispose();

            // Remove from window list
            WindowManager.Windows.RemoveAt(_selectedIndex);
            if (_selectedIndex >= WindowManager.Windows.Count)
                _selectedIndex = WindowManager.Windows.Count - 1;
        }

        private void SampleOwnerBytes() {
            var snap = Allocator.GetOwnerListSnapshot();
            if (snap == null || snap.Length == 0)
                return;

            // Build new snapshot values - removed lock to prevent deadlock
            // Clear KB/s tracking first
            for (int i = 0; i < _trackedOwnerCount; i++) {
                _ownerKBps[i] = 0;
            }
            
            // Helper to find or add owner
            int FindOrAddOwner(int ownerId) {
                // Search existing
                for (int i = 0; i < _trackedOwnerCount; i++) {
                    if (_ownerIds[i] == ownerId) return i;
                }
                // Add new if space available
                if (_trackedOwnerCount < MAX_TRACKED_OWNERS) {
                    int idx = _trackedOwnerCount;
                    _ownerIds[idx] = ownerId;
                    _lastOwnerBytes[idx] = 0;
                    _ownerKBps[idx] = 0;
                    _trackedOwnerCount++;
                    return idx;
                }
                return -1;
            }

            // Compute diffs for owners present in snap
            for (int i = 0; i < snap.Length; i++) {
                int owner = snap[i].OwnerId;
                ulong bytes = snap[i].Bytes;
                
                int ownerIdx = FindOrAddOwner(owner);
                if (ownerIdx >= 0) {
                    ulong prev = _lastOwnerBytes[ownerIdx];
                    long diff = (long)bytes - (long)prev; // bytes per ~1s
                    int kbs = (int)(diff / 1024L);
                    _ownerKBps[ownerIdx] = kbs;
                    _lastOwnerBytes[ownerIdx] = bytes;
                }
            }

            // Owners that disappeared -> negative freed rate
            for (int i = 0; i < _trackedOwnerCount; i++) {
                int owner = _ownerIds[i];
                bool found = false;
                for (int j = 0; j < snap.Length; j++) {
                    if (snap[j].OwnerId == owner) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    int freed = (int)(-((long)_lastOwnerBytes[i] / 1024L));
                    _ownerKBps[i] = freed;
                    _lastOwnerBytes[i] = 0;
                }
            }

            // Compact the arrays by removing owners with 0 bytes
            int writeIdx = 0;
            for (int readIdx = 0; readIdx < _trackedOwnerCount; readIdx++) {
                if (_lastOwnerBytes[readIdx] != 0 || _ownerKBps[readIdx] != 0) {
                    if (writeIdx != readIdx) {
                        _ownerIds[writeIdx] = _ownerIds[readIdx];
                        _lastOwnerBytes[writeIdx] = _lastOwnerBytes[readIdx];
                        _ownerKBps[writeIdx] = _ownerKBps[readIdx];
                    }
                    writeIdx++;
                }
            }
            _trackedOwnerCount = writeIdx;
        }

        public override void Dispose() {
            // Dispose cached memory detail strings to avoid leaks
            _mdFreeCallsStr?.Dispose();
            _mdFreeSuccessStr?.Dispose();
            _mdFailPtrStr?.Dispose();
            _mdFailNoPagesStr?.Dispose();
            _mdAllocPagesStr?.Dispose();
            _mdFreedPagesStr?.Dispose();
            _mdPagesInUseStr?.Dispose();
            _mdNetGrowthStr?.Dispose();
            _mdLeakStr?.Dispose();
            _mdFreeAllocRatioStr?.Dispose();
            _mdHeapSizeStr?.Dispose();
            _mdHeapUsedStr?.Dispose();
            _mdHeapFreeStr?.Dispose();
            _mdHeapUtilStr?.Dispose();
            base.Dispose();
        }

        private void DrawMemoryDetails(int x, int y, int w, int h) {
            // Update underlying statistics every second
            if ((long)(Timer.Ticks - _lastMemDetailUpdate) >= 1000) {
                UpdateMemoryDetailStats();
                _lastMemDetailUpdate = Timer.Ticks;
            }

            // Refresh cached label strings only if interval elapsed
            if ((long)(Timer.Ticks - _lastMemLabelUpdateTicks) >= MemLabelUpdateIntervalMs) {
                _lastMemLabelUpdateTicks = Timer.Ticks;

                // Dispose old cached values
                _mdFreeCallsStr?.Dispose();
                _mdFreeSuccessStr?.Dispose();
                _mdFailPtrStr?.Dispose();
                _mdFailNoPagesStr?.Dispose();
                _mdAllocPagesStr?.Dispose();
                _mdFreedPagesStr?.Dispose();
                _mdPagesInUseStr?.Dispose();
                _mdNetGrowthStr?.Dispose();
                _mdLeakStr?.Dispose();
                _mdFreeAllocRatioStr?.Dispose();
                _mdHeapSizeStr?.Dispose();
                _mdHeapUsedStr?.Dispose();
                _mdHeapFreeStr?.Dispose();
                _mdHeapUtilStr?.Dispose();

                // USE STRING POOL where possible
                _mdFreeCallsStr = StringPool.GetNumber(Allocator.FreeCallCount);
                _mdFreeSuccessStr = StringPool.GetNumber(Allocator.FreeSuccessCount);
                _mdFailPtrStr = StringPool.GetNumber(Allocator.FreeFailInvalidPtr);
                _mdFailNoPagesStr = StringPool.GetNumber(Allocator.FreeFailNoPages);
                _mdAllocPagesStr = StringPool.GetNumber(_cumulativeAllocatedPages);
                _mdFreedPagesStr = StringPool.GetNumber(_cumulativeFreedPages);
                _mdPagesInUseStr = StringPool.GetNumber(Allocator._Info.PageInUse);
                _mdNetGrowthStr = ((long)_cumulativeAllocatedPages - (long)_cumulativeFreedPages).ToString();
                _mdLeakStr = _leakExists ? "TRUE" : "FALSE";
                if (_cumulativeAllocatedPages > 0) {
                    int freePct = (int)((_cumulativeFreedPages * 100UL) / _cumulativeAllocatedPages);
                    _mdFreeAllocRatioStr = StringPool.GetPercentage(freePct);
                } else {
                    _mdFreeAllocRatioStr = "N/A"; // literal
                }
                _mdHeapSizeStr = StringPool.GetMemorySize(Allocator.MemorySize);
                _mdHeapUsedStr = StringPool.GetMemorySize(Allocator.MemoryInUse);
                ulong heapFree = Allocator.MemorySize - Allocator.MemoryInUse;
                _mdHeapFreeStr = StringPool.GetMemorySize(heapFree);
                int heapUtilPct = Allocator.MemorySize > 0 ? (int)(Allocator.MemoryInUse * 100UL / Allocator.MemorySize) : 0;
                _mdHeapUtilStr = StringPool.GetPercentage(heapUtilPct);
            }

            int rowY = y + 10;
            int labelX = x + 10;
            int valueX = x + w / 2;
            int lineHeight = WindowManager.font.FontSize + 8;

            // Title
            WindowManager.font.DrawString(labelX, rowY, "=== Memory Allocator Details ===");
            rowY += lineHeight + 10;

            // FREE CALL STATISTICS
            WindowManager.font.DrawString(labelX, rowY, "=== Free() Call Statistics ===");
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Total Free() Calls:");
            WindowManager.font.DrawString(valueX, rowY, _mdFreeCallsStr);
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Successful Frees:");
            WindowManager.font.DrawString(valueX, rowY, _mdFreeSuccessStr);
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Failed (Invalid Ptr):");
            WindowManager.font.DrawString(valueX, rowY, _mdFailPtrStr);
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Failed (No Pages):");
            WindowManager.font.DrawString(valueX, rowY, _mdFailNoPagesStr);
            rowY += lineHeight + 10;

            // Separator
            Framebuffer.Graphics.DrawRectangle(labelX, rowY, w - 20, 1, 0xFF444444, 1);
            rowY += 10;

            // Allocated / Freed / In Use
            WindowManager.font.DrawString(labelX, rowY, "Allocated Pages:");
            WindowManager.font.DrawString(valueX, rowY, _mdAllocPagesStr);
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Freed Pages:");
            WindowManager.font.DrawString(valueX, rowY, _mdFreedPagesStr);
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Current Pages in Use:");
            WindowManager.font.DrawString(valueX, rowY, _mdPagesInUseStr);
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Net Growth (Alloc - Free):");
            WindowManager.font.DrawString(valueX, rowY, _mdNetGrowthStr);
            rowY += lineHeight;

            // Leak Exists
            WindowManager.font.DrawString(labelX, rowY, "Leak Exists:");
            uint leakColor = _leakExists ? 0xFFFF4444 : 0xFF44FF44;
            int leakX = valueX;
            int leakY = rowY;
            Framebuffer.Graphics.FillRectangle(leakX - 2, leakY - 2, 80, lineHeight, leakColor);
            WindowManager.font.DrawString(leakX, leakY, _mdLeakStr);
            rowY += lineHeight + 10;

            // Separator
            Framebuffer.Graphics.DrawRectangle(labelX, rowY, w - 20, 1, 0xFF444444, 1);
            rowY += 10;

            // Free/Alloc Ratio
            WindowManager.font.DrawString(labelX, rowY, "Free/Alloc Ratio:");
            WindowManager.font.DrawString(valueX, rowY, _mdFreeAllocRatioStr);
            rowY += lineHeight + 10;

            // Separator
            Framebuffer.Graphics.DrawRectangle(labelX, rowY, w - 20, 1, 0xFF444444, 1);
            rowY += 10;

            // Heap Allocator Stats
            WindowManager.font.DrawString(labelX, rowY, "=== Heap Allocator ===");
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Total Heap Size:");
            WindowManager.font.DrawString(valueX, rowY, _mdHeapSizeStr);
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Heap In Use:");
            WindowManager.font.DrawString(valueX, rowY, _mdHeapUsedStr);
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Heap Free:");
            WindowManager.font.DrawString(valueX, rowY, _mdHeapFreeStr);
            rowY += lineHeight;

            WindowManager.font.DrawString(labelX, rowY, "Heap Utilization:");
            WindowManager.font.DrawString(valueX, rowY, _mdHeapUtilStr);
        }

        private void UpdateMemoryDetailStats() {
            // Track cumulative allocated/freed pages using the actual counters from Allocator
            // This is more accurate than trying to infer from PageInUse changes
            
            // Use the Allocator's own counters for accurate tracking
            _cumulativeFreedPages = Allocator.FreeSuccessCount; // Direct count of successful frees
            
            // Calculate allocations from current state
            ulong currentPageInUse = Allocator._Info.PageInUse;
            _cumulativeAllocatedPages = currentPageInUse + _cumulativeFreedPages;

            // Update leak detection
            long netGrowth = (long)_cumulativeAllocatedPages - (long)_cumulativeFreedPages;
            _leakHistory.Add((ulong)netGrowth);
            
            // Keep history size limited
            if (_leakHistory.Count > _leakHistoryMaxSamples) {
                _leakHistory.RemoveAt(0);
            }

            // Detect persistent growth (leak)
            if (_leakHistory.Count >= 2) {
                ulong prev = _leakHistory[_leakHistory.Count - 2];
                ulong curr = _leakHistory[_leakHistory.Count - 1];
                
                if (curr > prev) {
                    _leakGrowthCounter++;
                    if (_leakGrowthCounter >= LeakThreshold) {
                        _leakExists = true;
                    }
                } else {
                    _leakGrowthCounter = 0;
                    if (_leakHistory.Count >= _leakHistoryMaxSamples) {
                        // Check if overall trend is stable
                        ulong first = _leakHistory[0];
                        ulong last = _leakHistory[_leakHistory.Count - 1];
                        if (last <= first + 100) { // Allow small growth margin
                            _leakExists = false;
                        }
                    }
                }
            }
        }

        private struct TagStat {
            public Allocator.AllocTag Tag;
            public ulong Bytes;
        }

        private TagStat[] GetTopAllocTags(int count) {
            var tags = new List<TagStat>();
            
            for (int t = 0; t < (int)Allocator.AllocTag.Count; t++) {
                ulong bytes = Allocator.GetTagBytes((Allocator.AllocTag)t);
                if (bytes > 0) {
                    TagStat ts;
                    ts.Tag = (Allocator.AllocTag)t;
                    ts.Bytes = bytes;
                    tags.Add(ts);
                }
            }

            // Simple bubble sort (small list)
            for (int i = 0; i < tags.Count - 1; i++) {
                for (int j = 0; j < tags.Count - i - 1; j++) {
                    if (tags[j].Bytes < tags[j + 1].Bytes) {
                        TagStat temp = tags[j];
                        tags[j] = tags[j + 1];
                        tags[j + 1] = temp;
                    }
                }
            }

            // Return top N
            int resultCount = tags.Count < count ? tags.Count : count;
            var result = new TagStat[resultCount];
            for (int i = 0; i < resultCount; i++) {
                result[i] = tags[i];
            }
            
            return result;
        }

        private struct OwnerGrowth {
            public int OwnerId;
            public int GrowthCount;
        }

        private OwnerGrowth[] GetTopGrowingOwners(int count) {
            var growers = new List<OwnerGrowth>();
            
            // Use parallel arrays instead of Dictionary
            for (int i = 0; i < _blameOwnerCount; i++) {
                int ownerId = _blameOwnerIds[i];
                int growthCount = _ownerGrowthCounter[i];
                if (growthCount > 0) {
                    OwnerGrowth og;
                    og.OwnerId = ownerId;
                    og.GrowthCount = growthCount;
                    growers.Add(og);
                }
            }

            // Simple bubble sort
            for (int i = 0; i < growers.Count - 1; i++) {
                for (int j = 0; j < growers.Count - i - 1; j++) {
                    if (growers[j].GrowthCount < growers[j + 1].GrowthCount) {
                        OwnerGrowth temp = growers[j];
                        growers[j] = growers[j + 1];
                        growers[j + 1] = temp;
                    }
                }
            }

            // Return top N
            int resultCount = growers.Count < count ? growers.Count : count;
            var result = new OwnerGrowth[resultCount];
            for (int i = 0; i < resultCount; i++) {
                result[i] = growers[i];
            }
            
            return result;
        }
    }
}
