using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
namespace guideXOS.GUI {
    /// <summary>
    /// Open Dialog
    /// </summary>
    internal class OpenDialog : Window {
        /// <summary>
        /// Current Path
        /// </summary>
        private string _currentPath;
        /// <summary>
        /// Entries
        /// </summary>
        private List<FileInfo> _entries;
        /// <summary>
        /// Selected Index
        /// </summary>
        private int _selectedIndex;
        /// <summary>
        /// Click Lock
        /// </summary>
        private bool _clickLock;
        /// <summary>
        /// On Open
        /// </summary>
        private readonly Action<string> _onOpen;
        private Action _serviceCloseCallback;
        private bool _suppressServiceCloseCallback;
        /// <summary>
        /// Padding
        /// </summary>
        private int _padding = 10;
        /// <summary>
        /// Row H - increased for better icon spacing
        /// </summary>
        private int _rowH = 40; // Increased from 28 to 40 for proper icon spacing
        /// <summary>
        /// Btn W
        /// </summary>
        private int _btnW = 100; // Increased from 80
        /// <summary>
        /// Btn H
        /// </summary>
        private int _btnH = 28; // Increased from 26
        /// <summary>
        /// Last Scan
        /// </summary>
        private byte _lastScan;
        /// <summary>
        /// Key Down
        /// </summary>
        private bool _keyDown;
        
        /// <summary>
        /// Scroll offset (index of first visible row)
        /// </summary>
        private int _scroll;
        /// <summary>
        /// Scroll dragging state
        /// </summary>
        private bool _scrollDrag;
        /// <summary>
        /// Scroll drag start Y position
        /// </summary>
        private int _scrollStartY;
        /// <summary>
        /// Scroll value at drag start
        /// </summary>
        private int _scrollStartScroll;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <param name="startPath"></param>
        /// <param name="onOpen"></param>
        public OpenDialog(int x, int y, int w, int h, string startPath, Action<string> onOpen) : base(x, y, w, h) {
            Title = "Open";
            IsResizable = true;
            ShowInTaskbar = false;
            ShowMaximize = false;
            ShowMinimize = true;
            ShowTombstone = false;
            _currentPath = startPath ?? "";
            _entries = new List<FileInfo>();
            _selectedIndex = -1;
            _clickLock = false;
            _onOpen = onOpen;
            _suppressServiceCloseCallback = false;
            _scroll = 0;
            _scrollDrag = false;
            Keyboard.OnKeyChanged += Keyboard_OnKeyChanged;
            RefreshEntries();
        }
        
        /// <summary>
        /// On Key Changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="key"></param>
        private void Keyboard_OnKeyChanged(object sender, ConsoleKeyInfo key) {
            if (!Visible)
                return;
            if (key.KeyState != ConsoleKeyState.Pressed) {
                _keyDown = false;
                _lastScan = 0;
                return;
            }
            if (_keyDown && Keyboard.KeyInfo.ScanCode == _lastScan)
                return;
            _keyDown = true;
            _lastScan = (byte)Keyboard.KeyInfo.ScanCode;
            if (key.Key == ConsoleKey.Escape) {
                this.Visible = false;
                return;
            }
            if (key.Key == ConsoleKey.Enter) {
                OpenSelected();
                return;
            }
            if (_entries.Count == 0)
                return;
            
            // Keyboard navigation with automatic scrolling
            int rowsVisible = VisibleRows();
            switch (key.Key) {
                case ConsoleKey.Up:
                    if (_selectedIndex > 0) {
                        _selectedIndex--;
                        // Auto-scroll up if selection goes above visible area
                        if (_selectedIndex < _scroll)
                            _scroll = _selectedIndex;
                    }
                    break;
                case ConsoleKey.Down:
                    if (_selectedIndex < _entries.Count - 1) {
                        _selectedIndex++;
                        // Auto-scroll down if selection goes below visible area
                        if (_selectedIndex >= _scroll + rowsVisible)
                            _scroll = _selectedIndex - rowsVisible + 1;
                    }
                    break;
                case ConsoleKey.Home:
                    _selectedIndex = 0;
                    _scroll = 0;
                    break;
                case ConsoleKey.End:
                    _selectedIndex = _entries.Count - 1;
                    _scroll = _selectedIndex - rowsVisible + 1;
                    if (_scroll < 0) _scroll = 0;
                    break;
                case ConsoleKey.Prior: // Page Up
                    _selectedIndex -= rowsVisible;
                    if (_selectedIndex < 0) _selectedIndex = 0;
                    _scroll -= rowsVisible;
                    if (_scroll < 0) _scroll = 0;
                    break;
                case ConsoleKey.Next: // Page Down
                    _selectedIndex += rowsVisible;
                    if (_selectedIndex >= _entries.Count) _selectedIndex = _entries.Count - 1;
                    _scroll += rowsVisible;
                    int maxScroll = _entries.Count - rowsVisible;
                    if (maxScroll < 0) maxScroll = 0;
                    if (_scroll > maxScroll) _scroll = maxScroll;
                    break;
            }
        }
        
        /// <summary>
        /// Calculate number of visible rows
        /// </summary>
        private int VisibleRows() {
            int listH = Height - _padding * 2 - 60 - 28; // Account for header and buttons
            return listH / _rowH;
        }
        
        /// <summary>
        /// Refresh Entries
        /// </summary>
        private void RefreshEntries() {
            if (_entries != null) {
                for (int i = 0; i < _entries.Count; i++)
                    _entries[i].Dispose();
                _entries.Clear();
            }
            _entries = File.GetFiles(_currentPath);
            _selectedIndex = _entries.Count > 0 ? 0 : -1;
            _scroll = 0; // Reset scroll when changing directory
        }
        
        /// <summary>
        /// Go Up
        /// </summary>
        private void GoUp() {
            if (string.IsNullOrEmpty(_currentPath))
                return;
            string path = _currentPath;
            if (path.Length > 0 && path[path.Length - 1] == '/')
                path = path.Substring(0, path.Length - 1);
            int last = path.LastIndexOf('/');
            _currentPath = last >= 0 ? path.Substring(0, last + 1) : "";
            RefreshEntries();
        }
        
        /// <summary>
        /// Open Selected
        /// </summary>
        private void OpenSelected() {
            if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
                return;
            var e = _entries[_selectedIndex];
            if (e.Attribute == FileAttribute.Directory) {
                _currentPath = _currentPath + e.Name + "/";
                RefreshEntries();
            } else {
                string path = _currentPath + e.Name;
                _suppressServiceCloseCallback = true;
                _onOpen?.Invoke(path);
                path.Dispose();
                this.Visible = false;
            }
        }
        
        /// <summary>
        /// On Input
        /// </summary>
        public override void OnInput() {
            base.OnInput();
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            int cx = X + _padding;
            int cy = Y + _padding + 28;
            int listX = cx;
            int listY = cy + 28; // Increased from 24 to give more space
            int listW = Width - _padding * 2;
            int listH = Height - _padding * 2 - 60 - 28;
            int upW = 70; // Increased button width
            int upH = 24; // Increased button height
            int upX = cx;
            int upY = cy;
            int openX = X + Width - _padding - _btnW;
            int openY = Y + Height - _padding - _btnH;
            int cancelX = openX - 8 - _btnW;
            int cancelY = openY;
            
            // Scrollbar dimensions
            int sbW = 10;
            int sbX = listX + listW - sbW;
            
            if (left) {
                if (!_clickLock) {
                    // Up button
                    if (mx >= upX && mx <= upX + upW && my >= upY && my <= upY + upH) {
                        GoUp();
                        _clickLock = true;
                        return;
                    }
                    // Open button
                    if (mx >= openX && mx <= openX + _btnW && my >= openY && my <= openY + _btnH) {
                        OpenSelected();
                        _clickLock = true;
                        return;
                    }
                    // Cancel button
                    if (mx >= cancelX && mx <= cancelX + _btnW && my >= cancelY && my <= cancelY + _btnH) {
                        this.Visible = false;
                        _clickLock = true;
                        return;
                    }
                    // Scrollbar drag start
                    if (mx >= sbX && mx <= sbX + sbW && my >= listY && my <= listY + listH) {
                        _scrollDrag = true;
                        _scrollStartY = my;
                        _scrollStartScroll = _scroll;
                        _clickLock = true;
                        return;
                    }
                    // List item click
                    if (mx >= listX && mx <= listX + listW - sbW && my >= listY && my <= listY + listH) {
                        int clickedRow = (my - listY) / _rowH;
                        int idx = _scroll + clickedRow;
                        if (idx >= 0 && idx < _entries.Count) {
                            if (_selectedIndex == idx) {
                                // Double-click behavior: open immediately
                                OpenSelected();
                            } else {
                                _selectedIndex = idx;
                            }
                        }
                        _clickLock = true;
                        return;
                    }
                }
            } else {
                _clickLock = false;
                _scrollDrag = false;
            }
            
            // Handle scroll dragging
            if (_scrollDrag && left) {
                int rowsVisible = VisibleRows();
                int maxScroll = _entries.Count - rowsVisible;
                if (maxScroll < 0) maxScroll = 0;
                
                int dy = my - _scrollStartY;
                // Convert mouse movement to scroll offset
                int scrollDelta = (dy * _entries.Count) / (listH > 0 ? listH : 1);
                _scroll = _scrollStartScroll + scrollDelta;
                
                if (_scroll < 0) _scroll = 0;
                if (_scroll > maxScroll) _scroll = maxScroll;
            }
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

        /// <summary>
        /// On Draw
        /// </summary>
        public override void OnDraw() {
            base.OnDraw();
            int cx = X + _padding;
            int cy = Y + _padding + 28;
            int listX = cx;
            int listY = cy + 28;
            int listW = Width - _padding * 2;
            int listH = Height - _padding * 2 - 60 - 28;
            
            // Header bar with path
            Framebuffer.Graphics.FillRectangle(cx, cy, listW, 24, 0xFF3A3A3A);
            int upW = 70;
            int upH = 24;
            
            // Up button with hover effect
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool hoverUp = (mx >= cx && mx <= cx + upW && my >= cy && my <= cy + upH);
            Framebuffer.Graphics.FillRectangle(cx, cy, upW, upH, hoverUp ? 0xFF5A5A5A : 0xFF4A4A4A);
            Framebuffer.Graphics.DrawRectangle(cx, cy, upW, upH, 0xFF555555, 1);
            WindowManager.font.DrawString(cx + 10, cy + (upH / 2 - WindowManager.font.FontSize / 2), "Up");
            
            // Current path display
            string pathDisplay = _currentPath;
            if (string.IsNullOrEmpty(pathDisplay)) pathDisplay = "/";
            int pathX = cx + upW + 8;
            int pathW = listW - upW - 8;
            WindowManager.font.DrawString(pathX, cy + (upH / 2 - WindowManager.font.FontSize / 2), pathDisplay, pathW, WindowManager.font.FontSize);
            
            // List background
            Framebuffer.Graphics.FillRectangle(listX, listY, listW, listH, 0xFF2B2B2B);
            Framebuffer.Graphics.DrawRectangle(listX, listY, listW, listH, 0xFF444444, 1);
            
            // Only render visible rows for performance
            int rowsVisible = VisibleRows();
            int startIdx = _scroll;
            int endIdx = startIdx + rowsVisible + 1; // +1 for partial row at bottom
            if (endIdx > _entries.Count) endIdx = _entries.Count;
            
            int y = listY;
            int iconW = Icons.DocumentIcon(32).Width;
            int iconH = Icons.DocumentIcon(32).Height;
            
            for (int i = startIdx; i < endIdx; i++) {
                var e = _entries[i];
                int rowY = y + (i - startIdx) * _rowH;
                
                // Row background with hover effect
                bool isSelected = (i == _selectedIndex);
                bool isHovered = (mx >= listX && mx <= listX + listW - 10 && 
                                 my >= rowY && my <= rowY + _rowH);
                
                uint rowBg;
                if (isSelected) {
                    rowBg = 0xFF505050u; // Selected color
                } else if (isHovered) {
                    rowBg = 0xFF383838u; // Hover color
                } else {
                    rowBg = ((i & 1) == 0 ? 0xFF303030u : 0xFF2B2B2Bu); // Alternating
                }
                
                Framebuffer.Graphics.FillRectangle(listX, rowY, listW - 10, _rowH, rowBg);
                
                // Icon with proper padding (8px from left)
                var icon = (e.Attribute == FileAttribute.Directory) ? Icons.FolderIcon(32) : Icons.DocumentIcon(32);
                int iconY = rowY + (_rowH / 2 - iconH / 2);
                Framebuffer.Graphics.DrawImage(listX + 8, iconY, icon);
                
                // Text with proper padding (8px from icon)
                int textX = listX + 8 + iconW + 8;
                int textY = rowY + (_rowH / 2 - WindowManager.font.FontSize / 2);
                int textW = listW - (8 + iconW + 8 + 20); // Leave space for scrollbar
                WindowManager.font.DrawString(textX, textY, e.Name, textW, WindowManager.font.FontSize);
            }
            
            // Scrollbar
            int sbW = 10;
            int sbX = listX + listW - sbW;
            
            // Scrollbar background
            Framebuffer.Graphics.FillRectangle(sbX, listY, sbW, listH, 0xFF1A1A1A);
            
            // Scrollbar thumb (only show if content is scrollable)
            if (_entries.Count > rowsVisible) {
                int thumbH = (listH * rowsVisible) / _entries.Count;
                if (thumbH < 20) thumbH = 20; // Minimum thumb height
                if (thumbH > listH) thumbH = listH;
                
                int maxScroll = _entries.Count - rowsVisible;
                int thumbY = listY;
                if (maxScroll > 0) {
                    thumbY = listY + (_scroll * (listH - thumbH)) / maxScroll;
                }
                
                // Scrollbar thumb with rounded appearance
                Framebuffer.Graphics.FillRectangle(sbX + 1, thumbY, sbW - 2, thumbH, 0xFF4A4A4A);
                Framebuffer.Graphics.DrawRectangle(sbX + 1, thumbY, sbW - 2, thumbH, 0xFF555555, 1);
            }
            
            // Buttons at bottom
            int openX = X + Width - _padding - _btnW;
            int openY = Y + Height - _padding - _btnH;
            int cancelX = openX - 8 - _btnW;
            
            bool hoverOpen = (mx >= openX && mx <= openX + _btnW && my >= openY && my <= openY + _btnH);
            bool hoverCancel = (mx >= cancelX && mx <= cancelX + _btnW && my >= openY && my <= openY + _btnH);
            
            // Open button
            Framebuffer.Graphics.FillRectangle(openX, openY, _btnW, _btnH, hoverOpen ? 0xFF4A4A4A : 0xFF3A3A3A);
            Framebuffer.Graphics.DrawRectangle(openX, openY, _btnW, _btnH, 0xFF555555, 1);
            WindowManager.font.DrawString(openX + (_btnW / 2 - WindowManager.font.MeasureString("Open") / 2), 
                                         openY + (_btnH / 2 - WindowManager.font.FontSize / 2), "Open");
            
            // Cancel button
            Framebuffer.Graphics.FillRectangle(cancelX, openY, _btnW, _btnH, hoverCancel ? 0xFF4A4A4A : 0xFF3A3A3A);
            Framebuffer.Graphics.DrawRectangle(cancelX, openY, _btnW, _btnH, 0xFF555555, 1);
            WindowManager.font.DrawString(cancelX + (_btnW / 2 - WindowManager.font.MeasureString("Cancel") / 2), 
                                         openY + (_btnH / 2 - WindowManager.font.FontSize / 2), "Cancel");
        }
    }
}
