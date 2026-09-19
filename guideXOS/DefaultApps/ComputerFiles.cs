using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using System.Windows.Forms;
using System.Collections.Generic;
using System;
using guideXOS.GUI;
using guideXOS.OS;
namespace guideXOS.DefaultApps {
    /// <summary>
    /// Computer Files Window
    /// </summary>
    internal class ComputerFiles : Window {
        /// <summary>
        /// Empty means root of current drive (FileSystem)
        /// </summary>
        private string _currentPath = ""; 
        /// <summary>
        /// Show Drives
        /// </summary>
        private bool _showDrives = true;
        /// <summary>
        /// History
        /// </summary>
        private string[] _history = new string[64];
        /// <summary>
        /// History Count
        /// </summary>
        private int _historyCount = 0;
        /// <summary>
        /// History Index
        /// </summary>
        private int _historyIndex = -1;
        /// <summary>
        /// Grid/icon sizes (available set)
        /// </summary>
        private int[] _sizes = new int[] { 16, 24, 32, 48, 128 };
        private int _sizeIndex = 3; // default 48px
        private System.Drawing.Image _iconFolder;
        private System.Drawing.Image _iconDoc;

        // Scrolling
        private int _scroll;
        private bool _scrollDrag;
        private int _scrollDragStartY;
        private int _scrollDragStartScroll;

        // Resizing
        private bool _resizing;
        private int _resizeStartMouseX, _resizeStartMouseY;
        private int _resizeStartW, _resizeStartH;
        private const int ResizeHandle = 14;

        // Cached entries for performance
        private List<FileInfo> _entriesCache;
        private string _entriesCacheFor;
        private bool _entriesDirty;

        // Search
        private string _search = string.Empty;
        private bool _searchFocus = false;
        private byte _lastScan; private bool _keyDown;
        private bool _leftDownPrev;
        private bool _suppressMouseUntilRelease = true;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
        private bool _mouseGuardTraced;
#endif

        private FileSystem _fs;
        private bool _ownsFileSystem;
        private List<DriveInfo> _drives;
        private readonly ApplicationServiceContext _serviceContext;
        private readonly ApplicationServiceAccess _services;

        public ComputerFiles(int x, int y, int w, int h, FileSystem fs, string driveName)
            : this(x, y, w, h, fs, driveName, false, null, null)
        {
        }

        internal ComputerFiles(int x, int y, int w, int h, FileSystem fs,
                               string driveName, bool ownsFileSystem,
                               ApplicationServiceContext serviceContext,
                               ApplicationServiceAccess services) : base(x, y, w, h)
        {
            _serviceContext = serviceContext;
            _services = services;
            Title = "Computer Files - " + driveName;
            _fs = fs;
            _ownsFileSystem = ownsFileSystem;
            _showDrives = false;
            _currentPath = "";

            LoadIcons();
            _entriesDirty = true;
            IsResizable = true;
            ShowInTaskbar = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowTombstone = true;
            Keyboard.OnKeyChanged += Keyboard_OnKeyChanged;

            PushHistory("");
            // A drive child can be constructed during its parent's held
            // mouse press. Consume that inherited state so the child waits
            // for a fresh press edge before opening a file.
            _suppressMouseUntilRelease = true;
            _leftDownPrev = true;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("FILES_WINDOW_BOUNDS=x=" + X.ToString() +
                ";y=" + Y.ToString() + ";w=" + Width.ToString() +
                ";h=" + Height.ToString());
#endif
        }

        public ComputerFiles(int X, int Y, int W = 640, int H = 480)
            : this(X, Y, W, H, (ApplicationServiceContext)null,
                  (ApplicationServiceAccess)null) {
        }

        internal ComputerFiles(int X, int Y, int W, int H,
                ApplicationServiceContext serviceContext,
                ApplicationServiceAccess services) : base(X, Y, W, H) {
            _serviceContext = serviceContext;
            _services = services;
            Title = "Computer Files";
            LoadIcons();
            _entriesDirty = true;
            _entriesCacheFor = null;
            _entriesCache = null;
            IsResizable = true;
            ShowInTaskbar = true;
            ShowMaximize = true;
            ShowMinimize = true;
            //ShowRestore = true;
            ShowTombstone = true;
            Keyboard.OnKeyChanged += Keyboard_OnKeyChanged;
            RefreshDrives();
            PushHistory(null);
            _suppressMouseUntilRelease = true;
            _leftDownPrev = true;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("FILES_WINDOW_BOUNDS=x=" + X.ToString() +
                ";y=" + Y.ToString() + ";w=" + Width.ToString() +
                ";h=" + Height.ToString());
#endif
        }

        private void Keyboard_OnKeyChanged(object sender, ConsoleKeyInfo key) {
            if (!Visible || !_searchFocus) return;
            if (key.KeyState != ConsoleKeyState.Pressed) { _keyDown = false; _lastScan = 0; return; }
            if (_keyDown && Keyboard.KeyInfo.ScanCode == _lastScan) return;
            _keyDown = true; _lastScan = (byte)Keyboard.KeyInfo.ScanCode;

            if (key.Key == ConsoleKey.Escape) { _searchFocus = false; return; }
            if (key.Key == ConsoleKey.Backspace) { 
                if (_search.Length > 0) {
                    // Create substring but don't dispose _search itself (it may be string.Empty literal)
                    _search = _search.Substring(0, _search.Length - 1);
                }
                return; 
            }
            if (key.Key == ConsoleKey.Enter) { return; }
            char ch = key.KeyChar; 
            if (ch >= ' ' && ch <= '~') { 
                string charStr = ch.ToString();
                _search = _search + charStr;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                Program.MarkUefiAppRuntime("FILES_SEARCH_TEXT=text=" + _search);
#endif
            }
        }

        private void LoadIcons() {
            int px = _sizes[_sizeIndex];
            // Ensure valid icons (fallback to 32px if specific size unavailable)
            try { _iconFolder = Icons.FolderIcon(px); } catch { _iconFolder = Icons.FolderIcon(32); }
            try { _iconDoc = Icons.DocumentIcon(px); } catch { _iconDoc = Icons.DocumentIcon(32); }
        }

        private void RefreshDrives()
        {
            _drives = new List<DriveInfo>();
            _drives.Add(new DriveInfo
            {
                Name = "Hard Disk",
                Type = DriveInfo.DriveType.HardDisk,
                IsReady = true,
                FileSystem = new AutoFS()
            });

            var usbDevices = USBStorage.GetAll();
            for (int i = 0; i < usbDevices.Length; i++)
            {
                var dev = usbDevices[i];
                var disk = USBMSC.TryOpenDisk(dev);
                if (disk != null && disk.IsReady)
                {
                    _drives.Add(new DriveInfo
                    {
                        Name = "USB Drive " + (i + 1),
                        Type = DriveInfo.DriveType.USB,
                        TotalSize = disk.TotalBlocks * disk.LogicalBlockSize,
                        IsReady = true,
                        Tag = dev,
                        FileSystem = new AutoFS(disk)
                    });
                }
            }
        }

        private void ClearEntriesCache() {
            if (_entriesCache != null) {
                for (int i = 0; i < _entriesCache.Count; i++) _entriesCache[i].Dispose();
                _entriesCache.Dispose();
                _entriesCache = null;
            }
            _entriesCacheFor = null;
        }

        private void EnsureEntries() {
            if (_showDrives) return;
            if (_entriesCache != null && !_entriesDirty && _entriesCacheFor != null && _entriesCacheFor == _currentPath) return;
            ClearEntriesCache();
            // Normalize path: root represented by empty string for FS
            string query = _currentPath;
            if (query == null) query = ""; // drives/root
            Busy.Push();
            if (_fs != null)
            {
                _entriesCache = _fs.GetFiles(query);
            }
            else
            {
                _entriesCache = File.GetFiles(query);
            }
            Busy.Pop();
            _entriesCacheFor = _currentPath;
            _entriesDirty = false;
        }

        private void MarkEntriesDirty() { _entriesDirty = true; }

        private void PushHistory(string path) {
            // Truncate forward history
            if (_historyIndex + 1 < _historyCount) _historyCount = _historyIndex + 1;
            if (_historyCount >= _history.Length) {
                for (int i = 1; i < _historyCount; i++) _history[i - 1] = _history[i];
                _historyCount--; _historyIndex--;
            }
            _history[_historyCount++] = path;
            _historyIndex = _historyCount - 1;
        }

        private bool CanGoBack => _historyIndex > 0;
        private bool CanGoForward => _historyIndex >= 0 && _historyIndex + 1 < _historyCount;

        private void GoBack() {
            if (!CanGoBack) return;
            _historyIndex--;
            _currentPath = _history[_historyIndex];
            _showDrives = _currentPath == null;
            _scroll = 0;
            MarkEntriesDirty();
        }

        private void GoForward() {
            if (!CanGoForward) return;
            _historyIndex++;
            _currentPath = _history[_historyIndex];
            _showDrives = _currentPath == null;
            _scroll = 0;
            MarkEntriesDirty();
        }

        private void GoUpLevel() {
            if (_showDrives) return;
            if (_currentPath == null) return;
            if (_currentPath.Length == 0) {
                _showDrives = true;
                if (_fs != null)
                {
                    // This is a drive-specific window, going "up" from root should not show all drives.
                    // Maybe close it or do nothing. For now, do nothing.
                    return;
                }
                PushHistory(null);
                _scroll = 0;
                MarkEntriesDirty();
                return;
            }
            string path = _currentPath;
            if (path.Length > 0 && path[path.Length - 1] == '/') path = path.Substring(0, path.Length - 1);
            int last = path.LastIndexOf('/');
            if (last >= 0) path = path.Substring(0, last + 1); else path = "";
            _currentPath = path;
            PushHistory(_currentPath);
            _scroll = 0;
            MarkEntriesDirty();
        }

        private void OpenRootChooserFromShell() {
            var shellObject = guideXOS.OS.ShellObjectRegistry.Resolve("Root");
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("SHELL_RESOLVE=name=Root;kind=" +
                shellObject.Kind.ToString() + ";success=" +
                (shellObject.Success ? "1" : "0"));
#endif
            if (!shellObject.Success) return;
            _showDrives = true;
            _currentPath = "";
            PushHistory(null);
            _scroll = 0;
            MarkEntriesDirty();
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("SHELL_ROUTE=ROOT;result=COMPUTER_FILES_ROOT");
#endif
        }

        private bool TryOpenDocumentThroughService(string path) {
            if (_serviceContext == null || _services == null ||
                    _services.Shell == null || string.IsNullOrEmpty(path)) {
                return false;
            }
            ApplicationShellOpenRequest request =
                ApplicationShellOpenRequest.ForDocument(path);
            ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                _services.Shell.Begin(_serviceContext, request);
            if (!begun.Succeeded) return false;
            ApplicationServiceResult<
                    ApplicationServiceRequestStatus<ApplicationShellResult>> observed =
                _services.Shell.Observe(_serviceContext, begun.Value);
            bool success = observed.Succeeded && observed.Value != null &&
                observed.Value.IsTerminal && observed.Value.Value != null &&
                observed.Value.Value.Succeeded;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("FILES_SHELL_DOCUMENT=" +
                (success ? "PASS" : "FAIL"));
#endif
            return success;
        }

        private void GoTo(string path) {
            if (_historyIndex < _historyCount - 1) {
                // Clear "forward" history
                for (int i = _historyIndex + 1; i < _historyCount; i++) {
                    if (_history[i] != null) {
                        _history[i].Dispose();
                        _history[i] = null;
                    }
                }
                _historyCount = _historyIndex + 1;
            }

            // FIXED: Do not dispose the incoming path if it's a literal or shared
            _history[_historyCount] = path;
            _historyCount++;
            _historyIndex = _historyCount - 1;

            // Do not dispose the old path if it's the same as the new one
            if (_currentPath != path) {
                _currentPath.Dispose();
            }
            
            _currentPath = path;
            _entriesDirty = true;
            _scroll = 0;
            _showDrives = string.IsNullOrEmpty(path);
        }

        // Conservative spacing to improve readability without heavy layout work
        private int CurrentPad() {
            int icon = _iconFolder != null ? _iconFolder.Width : 48;
            int basePad = 16; // previously 12
            if (icon >= 48) basePad += 4; // slightly more padding for larger icons
            return basePad;
        }

        // Truncate to width with ellipsis (single-line) to avoid spill - FIXED: properly dispose strings
        private string TruncateToWidth(string text, int maxW) {
            if (text == null) return string.Empty;
            int measured = WindowManager.font.MeasureString(text);
            if (measured <= maxW) return text;
            string ell = "...";
            int ellW = WindowManager.font.MeasureString(ell);
            int w = 0; int i = 0;
            for (; i < text.Length; i++) {
                string chs = text[i].ToString();
                int chW = WindowManager.font.MeasureString(chs);
                if (w + chW + ellW > maxW) break;
                w += chW;
            }
            string sub = text.Substring(0, i) + ell;
            return sub;
        }

        public override void OnInput() {
            bool currentLeftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            if (_suppressMouseUntilRelease) {
                if (!currentLeftDown) {
                    _suppressMouseUntilRelease = false;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                    if (!_mouseGuardTraced) {
                        Program.MarkUefiAppRuntime("FILES_MOUSE_GUARD=release");
                        _mouseGuardTraced = true;
                    }
#endif
                } else {
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                    if (!_mouseGuardTraced) {
                        Program.MarkUefiAppRuntime("FILES_MOUSE_GUARD=held");
                        _mouseGuardTraced = true;
                    }
#endif
                    // Do not let common window handling or content hit
                    // testing see the press that created this child.
                    if (IsUnderMouse()) WindowManager.MouseHandled = true;
                    return;
                }
            }
            base.OnInput(); if (!Visible) return;
            bool leftDown = currentLeftDown;
            bool clickEdge = leftDown && !_leftDownPrev;
            _leftDownPrev = leftDown;

            int tbH = WindowManager.font.FontSize + 12;
            int contentY = Y + 8 + tbH; int contentW = Width - 16; int contentH = Height - 16 - tbH; int leftW = 180; int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;

            // Mark mouse handled if inside this window
            if (Control.MouseButtons.HasFlag(MouseButtons.Left) && mx >= X && mx <= X + Width && my >= Y - BarHeight && my <= Y - BarHeight + BarHeight + Height) {
                WindowManager.MouseHandled = true;
            }

            // Resizing in bottom-right handle
            int rhX = X + Width - ResizeHandle;
            int rhY = Y + Height - ResizeHandle;
            if (Control.MouseButtons.HasFlag(MouseButtons.Left)) {
                if (!_resizing && mx >= rhX && mx <= X + Width && my >= rhY && my <= Y + Height) {
                    _resizing = true;
                    _resizeStartMouseX = mx; _resizeStartMouseY = my;
                    _resizeStartW = Width; _resizeStartH = Height;
                    return;
                }
            } else {
                _resizing = false;
            }
            if (_resizing) {
                int dw = mx - _resizeStartMouseX;
                int dh = my - _resizeStartMouseY;
                int newW = _resizeStartW + dw;
                int newH = _resizeStartH + dh;
                if (newW < 280) newW = 280;
                if (newH < 200) newH = 200;
                Width = newW; Height = newH;
                return;
            }

            if (leftDown) {
                // Toolbar buttons
                int btnW = 80; int btnH = WindowManager.font.FontSize + 8; int gap = 6;
                int tbY = Y + 6;
                int bx0 = X + 8;                 // Back
                int bx1 = bx0 + btnW + gap;      // Up Level
                int bx2 = bx1 + btnW + gap;      // Forward

                // Size options start after Forward, add a gap
                int sizeStartX = bx2 + btnW + gap + 10;

                if (clickEdge && mx >= bx0 && mx <= bx0 + btnW && my >= tbY && my <= tbY + btnH) { GoBack(); return; }
                if (clickEdge && mx >= bx1 && mx <= bx1 + btnW && my >= tbY && my <= tbY + btnH) { GoUpLevel(); return; }
                if (clickEdge && mx >= bx2 && mx <= bx2 + btnW && my >= tbY && my <= tbY + btnH) { GoForward(); return; }

                // Size buttons
                int sx = sizeStartX;
                for (int i = 0; i < _sizes.Length; i++) {
                    int w = 36;
                    if (clickEdge && mx >= sx && mx <= sx + w && my >= tbY && my <= tbY + btnH) {
                        _sizeIndex = i;
                        LoadIcons();
                        return;
                    }
                    sx += w + 4;
                }

                // Click in search box
                int searchW = 180; int searchH = btnH;
                int searchX = X + Width - 8 - searchW; int searchY = tbY;
                if (clickEdge && mx >= searchX && mx <= searchX + searchW && my >= searchY && my <= searchY + searchH) {
                    _searchFocus = true;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                    Program.MarkUefiAppRuntime("FILES_SEARCH_FOCUS=x=" + mx.ToString() +
                        ";y=" + my.ToString());
#endif
                    return;
                } else if (my >= tbY && my <= tbY + btnH) { _searchFocus = false; }

                // Left navigation clicks (below toolbar)
                int leftX0 = X + 1;
                int leftX1 = X + leftW - 2;
                int leftY0 = contentY;
                int cursorY = leftY0 + 10;
                int iconH = _iconFolder != null ? _iconFolder.Height : 48;
                if (clickEdge && mx >= leftX0 && mx <= leftX1 && my >= leftY0 && my <= leftY0 + contentH) {
                    // Desktop
                    if (my >= cursorY && my <= cursorY + iconH) {
                        // Go to root chooser (drives/root)
                        OpenRootChooserFromShell();
                        return;
                    }
                    cursorY += iconH + 10;
                    // Computer Files root
                    if (my >= cursorY && my <= cursorY + iconH) {
                        OpenRootChooserFromShell();
                        return;
                    }
                    cursorY += iconH + 10;
                    // USB entry
                    if (USBStorage.Count > 0) {
                        if (my >= cursorY && my <= cursorY + iconH) {
                            // Open the USB drives list window
                            var list = new USBDrives(X + leftW + 20, Y + 40, 420, 360);
                            WindowManager.MoveToEnd(list);
                            list.Visible = true; return;
                        }
                        cursorY += iconH + 10;
                    }
                }

                // Scrollbar drag start (right content area)
                int sbW = 10;
                int sbX = X + Width - 6 - sbW;
                if (clickEdge && mx >= sbX && mx <= sbX + sbW && my >= contentY && my <= contentY + contentH) {
                    _scrollDrag = true; _scrollDragStartY = my; _scrollDragStartScroll = _scroll; return;
                }

                // Content clicks (right grid)
                int pad = CurrentPad();
                if (_showDrives) {
                    int icon = _iconFolder != null ? _iconFolder.Width : 48;
                    int tileW = icon + pad * 2;
                    int tileH = icon + WindowManager.font.FontSize * 2 + pad;
                    int rcX = X + leftW + 8;
                    int rcW = contentW - leftW - 8;
                    int cols = tileW > 0 ? rcW / tileW : 1;
                    if (cols < 1) cols = 1;

                    for (int i = 0; i < _drives.Count; i++)
                    {
                        int gridX = i % cols;
                        int gridY = i / cols;
                        int gx = rcX + gridX * tileW + pad;
                        int gy = contentY + gridY * tileH + pad - _scroll;

                        if (gy + tileH < contentY || gy > contentY + contentH) continue;

                        if (clickEdge && mx >= gx && mx <= gx + icon && my >= gy && my <= gy + icon)
                        {
                            var drive = _drives[i];
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                            Program.MarkUefiAppRuntime("FILES_DRIVE_SELECT=name=" + drive.Name);
#endif
                            if (drive.FileSystem != null)
                            {
                                Desktop.LaunchComputerFilesDrive(drive.Name,
                                    X + 20, Y + 20);
                            }
                            return;
                        }
                    }
                } else {
                    EnsureEntries(); var list = _entriesCache; if (list == null) return;
                    int icon = _iconFolder != null ? _iconFolder.Width : 48; int tileW = icon + pad * 2; int tileH = icon + WindowManager.font.FontSize * 2 + pad;
                    int rcX = X + leftW + 8; int rcW = contentW - leftW - 8; int cols = tileW > 0 ? rcW / tileW : 1; if (cols < 1) cols = 1;
                    int visibleIndex = 0;
                    for (int i = 0; i < list.Count; i++) {
                        // Apply search filter
                        string name = list[i].Name; bool matches = true;
                        if (!string.IsNullOrEmpty(_search)) {
                            // case-insensitive contains
                            matches = ContainsIgnoreCase(name, _search);
                        }
                        if (!matches) continue;
                        int gridX = visibleIndex % cols; int gridY = visibleIndex / cols;
                        visibleIndex++;
                        int gx = rcX + gridX * tileW + pad;
                        int gy = contentY + gridY * tileH + pad - _scroll;
                        if (gy + tileH < contentY || gy > contentY + contentH) continue;
                        if (clickEdge && mx >= gx && mx <= gx + icon && my >= gy && my <= gy + icon) {
                            bool isDir = list[i].Attribute == FileAttribute.Directory;
                            if (isDir) { // open directory
                                string newPath = _currentPath + name + "/";
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                                Program.MarkUefiAppRuntime("FILES_DIR_OPEN=path=" + newPath);
#endif
                                _currentPath = newPath; PushHistory(_currentPath); _scroll = 0; MarkEntriesDirty(); return;
                            } else { // open file via Desktop handler
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                                Program.MarkUefiAppRuntime("FILES_FILE_CLICK=path=" + _currentPath + name);
#endif
                                string documentPath = _currentPath + name;
                                if (!TryOpenDocumentThroughService(documentPath) &&
                                        _serviceContext == null) {
                                    // The legacy shell-created window has no
                                    // application service context. Preserve
                                    // that compatibility-only route while all
                                    // modern factory instances use Shell.
                                    Desktop.Dir = _currentPath;
                                    Desktop.OnClick(name, false, gx, gy,
                                        "gxos.shell.computerfiles");
                                }
                                documentPath.Dispose();
                                return;
                            }
                        }
                    }
                }
            } else {
                _scrollDrag = false;
            }

            // Right-click pin support (after left click processing)
            /*
            if (Control.MouseButtons == MouseButtons.Right) {
                int mx2 = Control.MousePosition.X; int my2 = Control.MousePosition.Y; int pad2 = CurrentPad();
                if (_showDrives) {
                    int icon = _iconFolder != null ? _iconFolder.Width : 48; int rowX = X + leftW + 8; int rowY = contentY; if (mx2 >= rowX && mx2 <= rowX + icon && my2 >= rowY && my2 <= rowY + icon) { PinnedManager.PinComputerFiles(); }
                } else {
                    EnsureEntries(); var list2 = _entriesCache; if list2 != null) {
                        int icon = _iconFolder != null ? _iconFolder.Width : 48; int tileW = icon + pad2 * 2; int rcX2 = X + leftW + 8; int rcW2 = contentW - leftW - 8; int cols = tileW > 0 ? rcW2 / tileW : 1; if (cols < 1) cols = 1;
                        for (int i = 0; i < list2.Count; i++) {
                            int gridX = i % cols; int gridY = i / cols; int gx = rcX2 + gridX * tileW + pad2; int gy = contentY + gridY * (icon + WindowManager.font.FontSize + pad2) + pad2 - _scroll; if (mx2 >= gx && mx2 <= gx + icon && my2 >= gy && my2 <= gy + icon) { var fi = list2[i]; if (fi.Attribute != guideXOS.FS.FileAttribute.Directory) { PinnedManager.PinFile(fi.Name, _currentPath + fi.Name, _iconDoc); } break; }
                        }
                    }
                }
            }
            */

            // Scrollbar dragging update
            if (_scrollDrag) {
                int cH = contentH;
                int total = GetTotalContentHeight(contentW - leftW - 8);
                int maxScroll = total > cH ? total - cH : 0;
                if (maxScroll < 0) maxScroll = 0;
                int trackH = cH;
                int thumbH = total > 0 ? cH * cH / total : cH;
                if (thumbH < 16) thumbH = 16; if (thumbH > trackH) thumbH = trackH;
                int range = trackH - thumbH;
                if (range <= 0) { _scroll = 0; return; }
                int dy = my - _scrollDragStartY;
                int newThumbTop = trackH * _scrollDragStartScroll / (total == 0 ? 1 : total) + dy;
                if (newThumbTop < 0) newThumbTop = 0; if (newThumbTop > range) newThumbTop = range;
                _scroll = newThumbTop * total / trackH;
                if (_scroll < 0) _scroll = 0; if (_scroll > maxScroll) _scroll = maxScroll;
            }
        }

        private static bool ContainsIgnoreCase(string a, string b) {
            if (string.IsNullOrEmpty(b)) return true;
            int la = a.Length; int lb = b.Length; if (lb > la) return false;
            for (int i = 0; i <= la - lb; i++) {
                bool ok = true;
                for (int j = 0; j < lb; j++) {
                    char ca = a[i + j]; if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32);
                    char cb = b[j]; if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32);
                    if (ca != cb) { ok = false; break; }
                }
                if (ok) return true;
            }
            return false;
        }

        private int GetTotalContentHeight(int contentW) {
            int pad = CurrentPad();
            if (_showDrives) {
                if (_drives == null) return 0;
                int icon = _iconFolder != null ? _iconFolder.Width : 48;
                int tileWLocal = icon + pad * 2;
                int tileH = icon + WindowManager.font.FontSize * 2 + pad;
                int colsLocal = tileWLocal > 0 ? contentW / tileWLocal : 1;
                if (colsLocal < 1) colsLocal = 1;
                int rowsLocal = (_drives.Count + colsLocal - 1) / colsLocal;
                if (rowsLocal < 1) rowsLocal = 1;
                return rowsLocal * tileH;
            }
            EnsureEntries();
            var list = _entriesCache;
            int ic = _iconFolder != null ? _iconFolder.Width : 48; int tileW = ic + pad * 2; int tileH2 = ic + WindowManager.font.FontSize * 2 + pad;
            int cols = tileW > 0 ? contentW / tileW : 1; if (cols < 1) cols = 1;
            int rows = (list.Count + cols - 1) / cols; if (rows < 1) rows = 1;
            return rows * tileH2;
        }

        public override void OnDraw() {
            base.OnDraw(); if (WindowManager.font == null) return;

            int tbH = WindowManager.font.FontSize + 12;
            // toolbar
            int tbY = Y + 6;
            Framebuffer.Graphics.FillRectangle(X + 6, Y + 6, Width - 12, tbH, 0xFF1E1E1E);
            int btnW = 80; int btnH = WindowManager.font.FontSize + 8; int gap = 6;
            int bx0 = X + 8; int bx1 = bx0 + btnW + gap; int bx2 = bx1 + btnW + gap;

            // Hover states
            int mouseX = Control.MousePosition.X; int mouseY = Control.MousePosition.Y;
            bool overBack = mouseX >= bx0 && mouseX <= bx0 + btnW && mouseY >= tbY && mouseY <= tbY + btnH;
            bool overUp = mouseX >= bx1 && mouseX <= bx1 + btnW && mouseY >= tbY && mouseY <= tbY + btnH;
            bool overFwd = mouseX >= bx2 && mouseX <= bx2 + btnW && mouseY >= tbY && mouseY <= tbY + btnH;

            // Back
            UIPrimitives.AFillRoundedRect(bx0, tbY, btnW, btnH, overBack ? 0x333F7FBF : CanGoBack ? 0x332A2A2A : 0x33202020, 4);
            UIPrimitives.DrawRoundedRect(bx0, tbY, btnW, btnH, 0xFF3F3F3F, 1, 4);
            WindowManager.font.DrawString(bx0 + 14, tbY + 4, "Back");
            // Up Level
            UIPrimitives.AFillRoundedRect(bx1, tbY, btnW, btnH, overUp ? 0x333F7FBF : 0x332A2A2A, 4);
            UIPrimitives.DrawRoundedRect(bx1, tbY, btnW, btnH, 0xFF3F3F3F, 1, 4);
            WindowManager.font.DrawString(bx1 + 6, tbY + 4, "Up Level");
            // Forward
            UIPrimitives.AFillRoundedRect(bx2, tbY, btnW, btnH, overFwd ? 0x333F7FBF : CanGoForward ? 0x332A2A2A : 0x33202020, 4);
            UIPrimitives.DrawRoundedRect(bx2, tbY, btnW, btnH, 0xFF3F3F3F, 1, 4);
            WindowManager.font.DrawString(bx2 + 6, tbY + 4, "Forward");

            // Size options - FIXED: properly dispose strings
            int sx = bx2 + btnW + gap + 10;
            for (int i = 0; i < _sizes.Length; i++) {
                int w = 36;
                uint bg = i == _sizeIndex ? 0xFF355C9C : 0xFF2A2A2A;
                bool over = mouseX >= sx && mouseX <= sx + w && mouseY >= tbY && mouseY <= tbY + btnH;
                UIPrimitives.AFillRoundedRect(sx, tbY, w, btnH, over ? 0x333F7FBF : 0x332A2A2A, 4);
                UIPrimitives.DrawRoundedRect(sx, tbY, w, btnH, 0xFF3F3F3F, 1, 4);
                string sizeText = _sizes[i].ToString();
                WindowManager.font.DrawString(sx + 6, tbY + 4, sizeText);
                sx += w + 4;
            }

            // Search box (upper-right) - FIXED: properly dispose strings
            int searchW = 180; int searchH = btnH; int searchX = X + Width - 8 - searchW; int searchY = tbY;
            bool overSearch = mouseX >= searchX && mouseX <= searchX + searchW && mouseY >= searchY && mouseY <= searchY + searchH;
            uint sbg = _searchFocus ? 0xFF2D3E5F : overSearch ? 0xFF28364F : 0xFF222222;
            UIPrimitives.AFillRoundedRect(searchX, searchY, searchW, searchH, sbg, 4);
            UIPrimitives.DrawRoundedRect(searchX, searchY, searchW, searchH, 0xFF3F3F3F, 1, 4);
            string placeholder = string.IsNullOrEmpty(_search) ? "Search" : _search;
            WindowManager.font.DrawString(searchX + 8, searchY + 4, placeholder, searchW - 16, WindowManager.font.FontSize);

            // content area bounds
            int contentX = X + 8;
            int contentY = Y + 8 + tbH;
            int contentW = Width - 16;
            int contentH = Height - 16 - tbH;

            // background area
            Framebuffer.Graphics.FillRectangle(contentX, contentY, contentW, contentH, 0xFF202020);

            // Left column (below toolbar only)
            int leftW2 = 180;
            Framebuffer.Graphics.FillRectangle(X + 1, contentY, leftW2 - 2, contentH, 0xFF2A2A2A);
            int cursorY = contentY + 10;
            int maxLeftText = leftW2 - 2 - (_iconFolder != null ? _iconFolder.Width : 48) - 18;
            // Root/"Desktop"
            if (_iconFolder != null) Framebuffer.Graphics.DrawImage(X + 10, cursorY, _iconFolder);
            WindowManager.font.DrawString(X + 10 + _iconFolder.Width + 8, cursorY + _iconFolder.Height / 2 - WindowManager.font.FontSize / 2, "Desktop", maxLeftText, WindowManager.font.FontSize);
            cursorY += _iconFolder.Height + 10;
            // Computer Files root
            if (_iconFolder != null) Framebuffer.Graphics.DrawImage(X + 10, cursorY, _iconFolder);
            WindowManager.font.DrawString(X + 10 + _iconFolder.Width + 8, cursorY + _iconFolder.Height / 2 - WindowManager.font.FontSize / 2, "Computer Files", maxLeftText, WindowManager.font.FontSize);
            cursorY += _iconFolder.Height + 10;
            // USB drive indicator - FIXED: properly dispose strings
            if (USBStorage.Count > 0) {
                string baseLabel = USBStorage.Count == 1 ? "USB Drive" : "USB Drives";
                Framebuffer.Graphics.DrawImage(X + 10, cursorY, _iconFolder);
                WindowManager.font.DrawString(X + 10 + _iconFolder.Width + 8, cursorY + _iconFolder.Height / 2 - WindowManager.font.FontSize / 2, baseLabel, maxLeftText, WindowManager.font.FontSize);
                cursorY += _iconFolder.Height + 10; 
            }

            // Right content panel
            int rcX = X + leftW2 + 8;
            int rcW = contentW - leftW2 - 8;

            int pad = CurrentPad();
            if (_showDrives) {
                if (_drives == null) RefreshDrives();
                if (_drives == null) return;

                int icon = _iconFolder != null ? _iconFolder.Width : 48;
                int tileW = icon + pad * 2;
                int tileH = icon + WindowManager.font.FontSize * 2 + pad;
                int cols = tileW > 0 ? rcW / tileW : 1;
                if (cols < 1) cols = 1;

                for (int i = 0; i < _drives.Count; i++)
                {
                    var drive = _drives[i];
                    int gridX = i % cols;
                    int gridY = i / cols;
                    int gx = rcX + gridX * tileW + pad;
                    int gy = contentY + gridY * tileH + pad - _scroll;

                    if (gy + tileH < contentY || gy > contentY + contentH) continue;

                    System.Drawing.Image driveIcon = _iconFolder; // Default
                    if (drive.Type == DriveInfo.DriveType.USB)
                    {
                        // Later, we can have a specific USB icon
                        driveIcon = _iconFolder;
                    }

                    if (driveIcon != null) Framebuffer.Graphics.DrawImage(gx, gy, driveIcon);
                    WindowManager.font.DrawString(gx, gy + icon + 6, drive.Name, icon, WindowManager.font.FontSize * 2);
                }
            } else {
                EnsureEntries(); var list = _entriesCache; if (list != null) {
                    int icon = _iconFolder != null ? _iconFolder.Width : 48; int tileW = icon + pad * 2; int tileH = icon + WindowManager.font.FontSize * 2 + pad; int cols = tileW > 0 ? rcW / tileW : 1; if (cols < 1) cols = 1;
                    int visibleIndex = 0;
                    for (int i = 0; i < list.Count; i++) {
                        string name = list[i].Name; bool matches = string.IsNullOrEmpty(_search) || ContainsIgnoreCase(name, _search); if (!matches) continue; int gridX = visibleIndex % cols; int gridY = visibleIndex / cols; visibleIndex++; int gx = rcX + gridX * tileW + pad; int gy = contentY + gridY * tileH + pad - _scroll; if (gy + tileH < contentY || gy > contentY + contentH) continue; bool isDir = list[i].Attribute == FileAttribute.Directory; if (isDir) { if (_iconFolder != null) Framebuffer.Graphics.DrawImage(gx, gy, _iconFolder); } else { if (_iconDoc != null) Framebuffer.Graphics.DrawImage(gx, gy, _iconDoc); }
                        // Draw label clipped to icon width to prevent overhang into adjacent tiles
                        WindowManager.font.DrawString(gx, gy + icon + 6, name, icon, WindowManager.font.FontSize * 2);
                    }
                }
            }

            // vertical scrollbar for right content
            int total = GetTotalContentHeight(rcW);
            int maxScroll = total > contentH ? total - contentH : 0;
            if (maxScroll < 0) maxScroll = 0;
            int sbW = 10;
            int sbX = X + Width - 6 - sbW;
            int trackH = contentH;
            Framebuffer.Graphics.FillRectangle(sbX, contentY, sbW, trackH, 0xFF1A1A1A);
            if (total > 0 && maxScroll > 0) {
                int thumbH = contentH * contentH / total; if (thumbH < 16) thumbH = 16; if (thumbH > trackH) thumbH = trackH;
                int thumbY = trackH * _scroll / (total == 0 ? 1 : total);
                if (thumbY + thumbH > trackH) thumbY = trackH - thumbH;
                Framebuffer.Graphics.FillRectangle(sbX + 1, contentY + thumbY, sbW - 2, thumbH, 0xFF2F2F2F);
            }

            // resize handle visual
            Framebuffer.Graphics.FillRectangle(X + Width - ResizeHandle, Y + Height - ResizeHandle, ResizeHandle, ResizeHandle, 0xFF333333);
        }

        public override void Dispose() {
            // CRITICAL FIX: Unsubscribe from keyboard events to prevent memory leak
            Keyboard.OnKeyChanged -= Keyboard_OnKeyChanged;
            
            // Clean up cached entries
            ClearEntriesCache();

            if (_drives != null)
            {
                for (int i = 0; i < _drives.Count; i++)
                {
                    _drives[i].Dispose();
                }
                _drives.Dispose();
            }
            if (_ownsFileSystem && _fs != null) {
                _fs.Dispose();
                _fs = null;
            }
            
            base.Dispose();
        }
    }
}
