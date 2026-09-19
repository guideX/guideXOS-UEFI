using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.OS;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
namespace guideXOS.DefaultApps {
    /// <summary>
    /// Simple Notepad app: type text and save to a file to test filesystem writes.
    /// </summary>
    internal class Notepad : Window {
        private ApplicationServiceContext _serviceContext;
        private ApplicationServiceAccess _services;
        private bool _settingsLoaded;
        private string _text;
        private bool _clickLock;
        private int _padding = 10;
        private int _btnH = 28;
        private int _btnWSaveAs = 88;
        private int _btnWSave = 72;
        private int _btnWWrap = 64;
        private int _btnWOpen = 72;
        private int _btnWUndo = 64;
        private int _btnWRedo = 64;
        private string _fileName = "notes.txt";
        private string _savedPath;
        private bool _dirty;
        private bool _wrap = true;
        private ApplicationServiceRequestHandle _saveRequest;
        private ApplicationServiceRequestHandle _openRequest;
        private ApplicationServiceRequestHandle _confirmRequest;
        private ApplicationServiceRequestHandle _messageRequest;
        private Action _afterSave;
        private byte _lastScan; private bool _keyDown;
        // Status bar
        private int _statusH = 26;
        // Track up to two currently pressed make scan codes (0 = none)
        private byte _k1; private byte _k2;
        // Cursor blink
        private ulong _cursorTick;
        private bool _cursorVisible = true;
        // Undo / Redo stacks (store text snapshots)
        private List<string> _undoStack;
        private List<string> _redoStack;
        private const int _maxUndo = 64;

        public Notepad(int x, int y) : this(x, y, null, null) { }

        public Notepad(int x, int y,
                ApplicationServiceContext serviceContext,
                ApplicationServiceAccess services) : base(x, y, 700, 460) {
            _serviceContext = serviceContext;
            _services = services;
            _settingsLoaded = false;
            IsResizable = true;
            ShowInTaskbar = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowTombstone = true;
            ShowInStartMenu = true;
            Title = "Notepad";
            _text = string.Empty; _clickLock = false; _savedPath = null; _dirty = false;
            _saveRequest = ApplicationServiceRequestHandle.Invalid;
            _openRequest = ApplicationServiceRequestHandle.Invalid;
            _confirmRequest = ApplicationServiceRequestHandle.Invalid;
            _messageRequest = ApplicationServiceRequestHandle.Invalid;
            _afterSave = null;
            _k1 = 0; _k2 = 0;
            _cursorTick = 0; _cursorVisible = true;
            _undoStack = new List<string>(); _redoStack = new List<string>();
            // subscribe keyboard handler
            Keyboard.OnKeyChanged += Keyboard_OnKeyChanged;
        }

        private void EnsureWrapSettingLoaded() {
            if (_settingsLoaded) return;
            if (_serviceContext == null || _services == null ||
                    _services.Settings == null) {
                _settingsLoaded = true;
                return;
            }
            ApplicationServiceResult<ApplicationSettingValue> result =
                _services.Settings.Get(_serviceContext, "wrap");
            if (result.Code == ApplicationServiceResultCode.InvalidContext) return;
            if (result.Succeeded && result.Value.Kind ==
                    ApplicationSettingValueKind.Boolean) {
                _wrap = result.Value.BooleanValue;
            } else if (result.Code == ApplicationServiceResultCode.NotFound) {
                _services.Settings.Set(_serviceContext, "wrap",
                    ApplicationSettingValue.Boolean(_wrap));
            }
            _settingsLoaded = true;
        }

        public override void OnSetVisible(bool value) {
            // Intercept close when there are unsaved changes
            if (!value && _dirty) {
                if (!_confirmRequest.IsValid) BeginCloseConfirmation();
                // keep notepad visible until decision
                this.Visible = true;
            }
        }

        protected override void BeginFadeOutClose() {
            if (_dirty) {
                if (!_confirmRequest.IsValid) BeginCloseConfirmation();
                Visible = true;
                return;
            }
            base.BeginFadeOutClose();
        }

        private bool HasServiceRequest() {
            return _saveRequest.IsValid || _openRequest.IsValid ||
                   _confirmRequest.IsValid || _messageRequest.IsValid;
        }

        private void BeginCloseConfirmation() {
            if (_services == null || _services.Dialogs == null ||
                    _serviceContext == null) return;
            ApplicationDialogRequest request = ApplicationDialogRequest.Create(
                ApplicationDialogKind.Confirmation, "Save changes?",
                "The document has unsaved changes.",
                ApplicationDialogButtonSet.AcceptRejectCancel);
            ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                _services.Dialogs.Begin(_serviceContext, request);
            if (begun.Succeeded) _confirmRequest = begun.Value;
        }

        private void PollServiceRequests() {
            if (_services == null || _serviceContext == null) return;
            if (_openRequest.IsValid && _services.OpenFile != null) {
                ApplicationServiceResult<ApplicationServiceRequestStatus<
                    ApplicationFileDialogResult>> observed =
                    _services.OpenFile.Observe(_serviceContext, _openRequest);
                if (observed.Succeeded && observed.Value != null &&
                        observed.Value.IsTerminal) {
                    ApplicationFileDialogResult result = observed.Value.Value;
                    _openRequest = ApplicationServiceRequestHandle.Invalid;
                    if (observed.Value.State ==
                            ApplicationServiceRequestState.Completed &&
                            result != null && result.Outcome ==
                            ApplicationFileDialogOutcome.Selected) {
                        if (!OpenFile(result.SelectedPath)) {
                            ShowError("Open failed", "The selected document could not be opened.");
                        }
                    }
                }
            }
            if (_saveRequest.IsValid && _services.SaveFile != null) {
                ApplicationServiceResult<ApplicationServiceRequestStatus<
                    ApplicationFileDialogResult>> observed =
                    _services.SaveFile.Observe(_serviceContext, _saveRequest);
                if (observed.Succeeded && observed.Value != null &&
                        observed.Value.IsTerminal) {
                    ApplicationFileDialogResult result = observed.Value.Value;
                    _saveRequest = ApplicationServiceRequestHandle.Invalid;
                    if (observed.Value.State ==
                            ApplicationServiceRequestState.Completed &&
                            result != null && result.Outcome ==
                            ApplicationFileDialogOutcome.Selected) {
                        SaveTo(result.SelectedPath);
                        Action afterSave = _afterSave;
                        _afterSave = null;
                        if (afterSave != null) afterSave();
                    }
                }
            }
            if (_confirmRequest.IsValid && _services.Dialogs != null) {
                ApplicationServiceResult<ApplicationServiceRequestStatus<
                    ApplicationDialogResult>> observed =
                    _services.Dialogs.Observe(_serviceContext, _confirmRequest);
                if (observed.Succeeded && observed.Value != null &&
                        observed.Value.IsTerminal) {
                    ApplicationDialogResult result = observed.Value.Value;
                    _confirmRequest = ApplicationServiceRequestHandle.Invalid;
                    if (observed.Value.State ==
                            ApplicationServiceRequestState.Completed &&
                            result != null && result.Outcome ==
                            ApplicationDialogOutcome.Accepted) {
                        if (!string.IsNullOrEmpty(_savedPath)) {
                            SaveTo(_savedPath);
                            Visible = false;
                        } else {
                            OpenSaveAs(() => { Visible = false; });
                        }
                    } else if (observed.Value.State ==
                            ApplicationServiceRequestState.Completed &&
                            result != null && result.Outcome ==
                            ApplicationDialogOutcome.Rejected) {
                        _dirty = false;
                        Visible = false;
                    } else {
                        Visible = true;
                    }
                }
            }
            if (_messageRequest.IsValid && _services.Dialogs != null) {
                ApplicationServiceResult<ApplicationServiceRequestStatus<
                    ApplicationDialogResult>> observed =
                    _services.Dialogs.Observe(_serviceContext, _messageRequest);
                if (observed.Succeeded && observed.Value != null &&
                        observed.Value.IsTerminal) {
                    _messageRequest = ApplicationServiceRequestHandle.Invalid;
                }
            }
        }

        private void ShowInformation(string title, string body) {
            if (_services == null || _services.Dialogs == null ||
                    _serviceContext == null || HasServiceRequest()) return;
            ApplicationDialogRequest request = ApplicationDialogRequest.Create(
                ApplicationDialogKind.Information, title, body,
                ApplicationDialogButtonSet.Acknowledge);
            ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                _services.Dialogs.Begin(_serviceContext, request);
            if (begun.Succeeded) _messageRequest = begun.Value;
        }

        private void ShowError(string title, string body) {
            if (_services == null || _services.Dialogs == null ||
                    _serviceContext == null || HasServiceRequest()) return;
            ApplicationDialogRequest request = ApplicationDialogRequest.Create(
                ApplicationDialogKind.Error, title, body,
                ApplicationDialogButtonSet.Acknowledge);
            ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                _services.Dialogs.Begin(_serviceContext, request);
            if (begun.Succeeded) _messageRequest = begun.Value;
        }

        private static char MapFromKey(ConsoleKeyInfo key) {
            // Prefer KeyChar when provided by the driver
            if (key.KeyChar != '\0') return key.KeyChar;
            // Fallback mapping from ConsoleKey with Shift/Caps handling
            var k = key.Key;
            bool shift = Keyboard.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift);
            bool caps = Keyboard.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.CapsLock);
            if (k == ConsoleKey.Space) return ' ';
            if (k >= ConsoleKey.A && k <= ConsoleKey.Z) {
                char c = (char)('a' + (k - ConsoleKey.A));
                if (shift ^ caps) { if (c >= 'a' && c <= 'z') c = (char)('A' + (c - 'a')); }
                return c;
            }
            if (k >= ConsoleKey.D0 && k <= ConsoleKey.D9) {
                int d = (int)(k - ConsoleKey.D0);
                if (!shift) return (char)('0' + d);
                switch (d) {
                    case 0: return ')';
                    case 1: return '!';
                    case 2: return '@';
                    case 3: return '#';
                    case 4: return '$';
                    case 5: return '%';
                    case 6: return '^';
                    case 7: return '&';
                    case 8: return '*';
                    case 9: return '(';
                }
            }
            switch (k) {
                case ConsoleKey.OemPeriod: return shift ? '>' : '.';
                case ConsoleKey.OemComma: return shift ? '<' : ',';
                case ConsoleKey.OemMinus: return shift ? '_' : '-';
                case ConsoleKey.OemPlus: return shift ? '+' : '=';
                case ConsoleKey.Oem1: return shift ? ':' : ';';
                case ConsoleKey.Oem2: return shift ? '?' : '/';
                case ConsoleKey.Oem3: return shift ? '~' : '`';
                case ConsoleKey.Oem4: return shift ? '{' : '[';
                case ConsoleKey.Oem5: return shift ? '|' : '\\';
                case ConsoleKey.Oem6: return shift ? '}' : ']';
                case ConsoleKey.Oem7: return shift ? '"' : '\'';
            }
            return '\0';
        }

        private void UpdateStatusKeys(ConsoleKeyInfo key) {
            // Maintain up to two currently pressed make scan codes
            byte scan = (byte)Keyboard.KeyInfo.ScanCode;
            bool released = key.KeyState == ConsoleKeyState.Released;
            byte make = released ? (byte)(scan >= 0x80 ? scan - 0x80 : scan) : scan;

            if (released) {
                if (_k1 == make) _k1 = 0;
                else if (_k2 == make) _k2 = 0;
            } else { // pressed
                if (_k1 == 0 || _k1 == make) { _k1 = make; } else if (_k2 == 0 || _k2 == make) { _k2 = make; } else { _k2 = make; }
            }
        }

        private void Keyboard_OnKeyChanged(object sender, ConsoleKeyInfo key) {
            if (!Visible) return;
            // Always update key status pair for statusbar
            UpdateStatusKeys(key);

            if (HasServiceRequest()) return; // let the service dialog handle keys
            if (key.KeyState != ConsoleKeyState.Pressed) { _keyDown = false; _lastScan = 0; return; }
            if (_keyDown && Keyboard.KeyInfo.ScanCode == _lastScan) return; // de-bounce to avoid repeats
            _keyDown = true; _lastScan = (byte)Keyboard.KeyInfo.ScanCode;

            // Controls
            if (key.Key == ConsoleKey.Escape) { return; }
            if (key.Key == ConsoleKey.Backspace) { if (_text.Length > 0) { PushUndo(); _text = _text.Substring(0, _text.Length - 1); _dirty = true; } return; }
            if (key.Key == ConsoleKey.Enter) { PushUndo(); _text += "\n"; _dirty = true; return; }
            if (key.Key == ConsoleKey.Tab) { PushUndo(); _text += "    "; _dirty = true; return; }

            char ch = MapFromKey(key);
            if (ch != '\0') { PushUndo(); _text += ch; _dirty = true; }
        }

        private void SaveTo(string path) {
            // Save to Desktop.Dir + notes.txt
            byte[] data = new byte[_text.Length]; for (int i = 0; i < _text.Length; i++) data[i] = (byte)_text[i];
            File.WriteAllBytes(path, data); data.Dispose();
            _savedPath = path; _fileName = path.Substring(path.LastIndexOf('/') + 1); _dirty = false;
            Title = "Notepad - " + _fileName;
            Desktop.InvalidateDirCache();
            ShowInformation("Saved", "Saved: " + path);
            RecentManager.AddDocument(path, Icons.DocumentIcon(32));
        }

        private void OpenSaveAs(Action afterSaveClose = null) {
            if (_services == null || _services.SaveFile == null ||
                    _serviceContext == null || HasServiceRequest()) return;
            SaveFileRequest request = SaveFileRequest.Create(
                Desktop.Dir ?? string.Empty, _fileName);
            ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                _services.SaveFile.Begin(_serviceContext, request);
            if (begun.Succeeded) _saveRequest = begun.Value;
            else ShowError("Save failed", begun.BoundedDiagnostic ??
                "The save dialog could not be opened.");
            _afterSave = afterSaveClose;
        }

        private void OpenOpenDialog() {
            if (_services == null || _services.OpenFile == null ||
                    _serviceContext == null || HasServiceRequest()) return;
            OpenFileRequest request = OpenFileRequest.Create(
                Desktop.Dir ?? string.Empty);
            ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                _services.OpenFile.Begin(_serviceContext, request);
            if (begun.Succeeded) _openRequest = begun.Value;
            else ShowError("Open failed", begun.BoundedDiagnostic ??
                "The open dialog could not be opened.");
        }

        private void PushUndo() {
            if (_undoStack.Count >= _maxUndo) _undoStack.RemoveAt(0);
            _undoStack.Add(_text);
            _redoStack.Clear();
        }

        private void PerformUndo() {
            if (_undoStack.Count == 0) return;
            _redoStack.Add(_text);
            _text = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _dirty = true;
        }

        private void PerformRedo() {
            if (_redoStack.Count == 0) return;
            _undoStack.Add(_text);
            _text = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            _dirty = true;
        }

        private static bool StartsWithFast(string s, string pref) { int l = pref.Length; if (s == null || s.Length < l) return false; for (int i = 0; i < l; i++) if (s[i] != pref[i]) return false; return true; }
        public bool OpenFile(string path) {
            if (string.IsNullOrEmpty(path)) return false;
            string p = path;
            // Desktop.OnClick supplies the current directory in the path,
            // while the open dialog may return a directory-relative name.
            // Prefix only the latter so shell dispatch cannot form
            // "Scripts/Scripts/...".
            if (!StartsWithFast(p, "/") && !string.IsNullOrEmpty(Desktop.Dir) &&
                !StartsWithFast(p, Desktop.Dir)) p = Desktop.Dir + p;
            byte[] data = File.ReadAllBytes(p);
            if (data == null) return false;
            char[] chars = new char[data.Length]; for (int i = 0; i < data.Length; i++) { byte b = data[i]; chars[i] = b >= 32 && b < 127 ? (char)b : (b == 10 ? '\n' : '.'); } _text = new string(chars); data.Dispose(); _savedPath = p; _fileName = p.Substring(p.LastIndexOf('/') + 1); _dirty = false; Title = "Notepad - " + _fileName; RecentManager.AddDocument(p, Icons.DocumentIcon(32));
            _undoStack.Clear(); _redoStack.Clear();
            return true;
        }

        public override void OnInput() {
            EnsureWrapSettingLoaded();
            PollServiceRequests();
            base.OnInput(); if (HasServiceRequest()) return;
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            int bxSaveAs = X + _padding; int by = Y + _padding;
            int bxSave = bxSaveAs + _btnWSaveAs + 8;
            int bxOpen = bxSave + _btnWSave + 8;
            int bxWrap = bxOpen + _btnWOpen + 8;
            int bxUndo = bxWrap + _btnWWrap + 8;
            int bxRedo = bxUndo + _btnWUndo + 8;
            bool canSave = !string.IsNullOrEmpty(_savedPath) && _dirty;
            bool canUndo = _undoStack.Count > 0;
            bool canRedo = _redoStack.Count > 0;
            if (left) {
                if (!_clickLock) {
                    if (mx >= bxSaveAs && mx <= bxSaveAs + _btnWSaveAs && my >= by && my <= by + _btnH) { OpenSaveAs(); _clickLock = true; return; }
                    if (canSave && mx >= bxSave && mx <= bxSave + _btnWSave && my >= by && my <= by + _btnH) { SaveTo(_savedPath); _clickLock = true; return; }
                    if (mx >= bxOpen && mx <= bxOpen + _btnWOpen && my >= by && my <= by + _btnH) { OpenOpenDialog(); _clickLock = true; return; }
                    if (mx >= bxWrap && mx <= bxWrap + _btnWWrap && my >= by && my <= by + _btnH) {
                        _wrap = !_wrap;
                        if (_services != null && _services.Settings != null &&
                                _serviceContext != null) {
                            _services.Settings.Set(_serviceContext, "wrap",
                                ApplicationSettingValue.Boolean(_wrap));
                        }
                        _clickLock = true;
                        return;
                    }
                    if (canUndo && mx >= bxUndo && mx <= bxUndo + _btnWUndo && my >= by && my <= by + _btnH) { PerformUndo(); _clickLock = true; return; }
                    if (canRedo && mx >= bxRedo && mx <= bxRedo + _btnWRedo && my >= by && my <= by + _btnH) { PerformRedo(); _clickLock = true; return; }
                }
            } else { _clickLock = false; }
        }

        public override void OnDraw() {
            EnsureWrapSettingLoaded();
            PollServiceRequests();
            base.OnDraw(); int cx = X + _padding; int cy = Y + _padding; int cw = Width - _padding * 2; int ch = Height - _padding * 2;
            // Buttons
            int bxSaveAs = cx; int by = cy;
            int bxSave = bxSaveAs + _btnWSaveAs + 8;
            int bxOpen = bxSave + _btnWSave + 8;
            int bxWrap = bxOpen + _btnWOpen + 8;
            int bxUndo = bxWrap + _btnWWrap + 8;
            int bxRedo = bxUndo + _btnWUndo + 8;
            int fcy = by + (_btnH / 2 - WindowManager.font.FontSize / 2);
            // Save As
            uint cSaveAs = UI.ButtonFillColor(bxSaveAs, by, _btnWSaveAs, _btnH, 0xFF3A3A3A, 0xFF444444, 0xFF4A4A4A);
            Framebuffer.Graphics.FillRectangle(bxSaveAs, by, _btnWSaveAs, _btnH, cSaveAs); WindowManager.font.DrawString(bxSaveAs + 6, fcy, "Save As");
            // Save
            bool canSave = !string.IsNullOrEmpty(_savedPath) && _dirty;
            uint baseSave = canSave ? 0xFF3A3A3Au : 0xFF2A2A2Au;
            uint cSave = UI.ButtonFillColor(bxSave, by, _btnWSave, _btnH, baseSave, 0xFF444444, 0xFF4A4A4A, canSave);
            Framebuffer.Graphics.FillRectangle(bxSave, by, _btnWSave, _btnH, cSave);
            WindowManager.font.DrawString(bxSave + 12, fcy, "Save");
            // Open
            uint cOpen = UI.ButtonFillColor(bxOpen, by, _btnWOpen, _btnH, 0xFF3A3A3A, 0xFF444444, 0xFF4A4A4A);
            Framebuffer.Graphics.FillRectangle(bxOpen, by, _btnWOpen, _btnH, cOpen); WindowManager.font.DrawString(bxOpen + 10, fcy, "Open");
            // Wrap
            uint cWrap = UI.ButtonFillColor(bxWrap, by, _btnWWrap, _btnH, 0xFF3A3A3A, 0xFF444444, 0xFF4A4A4A);
            Framebuffer.Graphics.FillRectangle(bxWrap, by, _btnWWrap, _btnH, cWrap); WindowManager.font.DrawString(bxWrap + 10, fcy, _wrap ? "Wrap" : "NoWrap");
            // Undo
            bool canUndo = _undoStack.Count > 0;
            uint baseUndo = canUndo ? 0xFF3A3A3Au : 0xFF2A2A2Au;
            uint cUndo = UI.ButtonFillColor(bxUndo, by, _btnWUndo, _btnH, baseUndo, 0xFF444444, 0xFF4A4A4A, canUndo);
            Framebuffer.Graphics.FillRectangle(bxUndo, by, _btnWUndo, _btnH, cUndo); WindowManager.font.DrawString(bxUndo + 8, fcy, "Undo");
            // Redo
            bool canRedo = _redoStack.Count > 0;
            uint baseRedo = canRedo ? 0xFF3A3A3Au : 0xFF2A2A2Au;
            uint cRedo = UI.ButtonFillColor(bxRedo, by, _btnWRedo, _btnH, baseRedo, 0xFF444444, 0xFF4A4A4A, canRedo);
            Framebuffer.Graphics.FillRectangle(bxRedo, by, _btnWRedo, _btnH, cRedo); WindowManager.font.DrawString(bxRedo + 8, fcy, "Redo");

            int tx = cx; int ty = cy + _btnH + 8; int tw = cw; int th = ch - (_btnH + 8) - (_statusH + 6);
            if (th < 20) th = 20; // guard
            Framebuffer.Graphics.AFillRectangle(tx, ty, tw, th, 0x80282828);
            // Draw text
            int textX = tx + 6; int textY = ty + 6;
            if (_wrap) WindowManager.font.DrawString(textX, textY, _text, tw - 12, th - 12);
            else WindowManager.font.DrawString(textX, textY, _text);

            // Draw blinking cursor at end of text
            _cursorTick++;
            if (_cursorTick >= 30) { _cursorVisible = !_cursorVisible; _cursorTick = 0; }
            if (_cursorVisible) {
                int curW = 0; int curLine = 0;
                for (int i = 0; i < _text.Length; i++) {
                    if (_text[i] == '\n') { curLine++; curW = 0; continue; }
                    int cw2 = WindowManager.font.MeasureString(_text[i].ToString());
                    if (_wrap && tw - 12 > 0 && curW + cw2 > tw - 12) { curLine++; curW = 0; }
                    curW += cw2;
                }
                int cursorX = textX + curW;
                int cursorY = textY + curLine * WindowManager.font.FontSize;
                if (cursorY + WindowManager.font.FontSize <= ty + th)
                    Framebuffer.Graphics.FillRectangle(cursorX, cursorY, 2, WindowManager.font.FontSize, 0xFFCCCCCC);
            }

            // Status bar (bottom)
            DrawStatusBar(cx, cy, cw, ch);
        }

        private void DrawStatusBar(int cx, int cy, int cw, int ch) {
            int sx = cx; int sy = Y + Height - _padding - _statusH; int sw = cw; int sh = _statusH;
            // background and border
            Framebuffer.Graphics.FillRectangle(sx, sy, sw, sh, 0xFF252525);
            Framebuffer.Graphics.DrawRectangle(sx, sy, sw, sh, 0xFF3A3A3A, 1);

            // Compose hex keys text (up to two) in format: 0xNN, 0xNN
            string hex = string.Empty;
            if (_k1 != 0 && _k2 != 0) hex = "0x" + _k1.ToString("X2") + ", " + "0x" + _k2.ToString("X2");
            else if (_k1 != 0) hex = "0x" + _k1.ToString("X2");
            else if (_k2 != 0) hex = "0x" + _k2.ToString("X2");

            int textW = WindowManager.font.MeasureString(hex);
            int rightPadding = 8;
            int tx = sx + sw - rightPadding - textW;
            int ty = sy + (sh / 2) - (WindowManager.font.FontSize / 2);
            if (textW > 0) WindowManager.font.DrawString(tx, ty, hex);

            // Badges for modifiers to the left of hex text
            bool shift = Keyboard.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift);
            bool caps = Keyboard.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.CapsLock);
            int gap = 8;
            int curX = tx - gap;
            int badgeH = WindowManager.font.FontSize + 6;
            int badgeY = sy + (sh / 2) - (badgeH / 2);

            if (caps) { curX = DrawBadge(curX, badgeY, "CAPS"); curX -= 6; }
            if (shift) { curX = DrawBadge(curX, badgeY, "SHIFT"); curX -= 6; }
        }

        private int DrawBadge(int rightX, int y, string text) {
            int padX = 8; int h = WindowManager.font.FontSize + 6; int w = WindowManager.font.MeasureString(text) + padX * 2;
            int x = rightX - w;
            // subtle translucent fill and border (blue-ish like selection)
            UIPrimitives.AFillRoundedRect(x, y, w, h, 0x332A5B9A, 6);
            UIPrimitives.DrawRoundedRect(x, y, w, h, 0xFF3F7FBF, 1, 6);
            WindowManager.font.DrawString(x + padX, y + (h / 2 - WindowManager.font.FontSize / 2), text);
            return x; // return new left edge to continue placing leftwards
        }

        internal bool RunPhase9ServiceDiagnostic(
                ApplicationServiceContext otherContext,
                ApplicationServiceAccess otherServices) {
            int baselineActive = ApplicationInstanceRegistry.ActiveCount;
            bool crossOwnerRejected = false;
            bool openSuccess = false;
            bool openCancel = false;
            bool saveSuccess = false;
            bool saveCancel = false;
            bool confirmation = false;
            try {
                if (_serviceContext == null || _services == null ||
                        _services.OpenFile == null ||
                        _services.SaveFile == null ||
                        _services.Dialogs == null || otherContext == null ||
                        otherServices == null) return false;

                OpenOpenDialog();
                ApplicationServiceRequestHandle openHandle = _openRequest;
                crossOwnerRejected = openHandle.IsValid &&
                    otherServices.OpenFile.Observe(otherContext, openHandle).Code ==
                    ApplicationServiceResultCode.InvalidContext;
                if (!openHandle.IsValid) return false;
                ApplicationServiceResult completed =
                    ApplicationServiceRegistry.CompleteFileDialogRequestForSelfTest(
                        openHandle, ApplicationFileDialogOutcome.Selected,
                        "Programs/notepad.gxm");
                PollServiceRequests();
                openSuccess = completed.Succeeded &&
                    _savedPath == "Programs/notepad.gxm" &&
                    _fileName == "notepad.gxm" &&
                    !_dirty && Title == "Notepad - notepad.gxm";

                string pathBeforeCancel = _savedPath;
                OpenOpenDialog();
                ApplicationServiceRequestHandle cancelOpenHandle = _openRequest;
                ApplicationServiceResult cancelled = cancelOpenHandle.IsValid
                    ? _services.OpenFile.Cancel(_serviceContext,
                        cancelOpenHandle) : ApplicationServiceResult.InvalidRequestResult();
                PollServiceRequests();
                openCancel = cancelled.Code == ApplicationServiceResultCode.Cancelled &&
                    _savedPath == pathBeforeCancel;

                _text = "Phase 9 save";
                _dirty = true;
                OpenSaveAs();
                ApplicationServiceRequestHandle saveHandle = _saveRequest;
                completed = saveHandle.IsValid
                    ? ApplicationServiceRegistry.CompleteFileDialogRequestForSelfTest(
                        saveHandle, ApplicationFileDialogOutcome.Selected,
                        "Programs/phase9.txt")
                    : ApplicationServiceResult.InvalidRequestResult();
                PollServiceRequests();
                CompleteDiagnosticMessage();
                saveSuccess = completed.Succeeded &&
                    _savedPath == "Programs/phase9.txt" &&
                    _fileName == "phase9.txt" && !_dirty &&
                    Title == "Notepad - phase9.txt";

                _text = "Phase 9 cancelled save";
                _dirty = true;
                OpenSaveAs();
                ApplicationServiceRequestHandle cancelSaveHandle = _saveRequest;
                cancelled = cancelSaveHandle.IsValid
                    ? _services.SaveFile.Cancel(_serviceContext,
                        cancelSaveHandle) : ApplicationServiceResult.InvalidRequestResult();
                PollServiceRequests();
                saveCancel = cancelled.Code == ApplicationServiceResultCode.Cancelled &&
                    _dirty && _savedPath == "Programs/phase9.txt";

                Visible = false;
                ApplicationServiceRequestHandle confirmationHandle = _confirmRequest;
                completed = confirmationHandle.IsValid
                    ? ApplicationServiceRegistry.CompleteDialogRequestForSelfTest(
                        confirmationHandle, ApplicationDialogOutcome.Cancelled)
                    : ApplicationServiceResult.InvalidRequestResult();
                PollServiceRequests();
                confirmation = completed.Succeeded && _dirty && Visible;

                bool cleanup = !_saveRequest.IsValid && !_openRequest.IsValid &&
                    !_confirmRequest.IsValid && !_messageRequest.IsValid &&
                    ApplicationServiceSessionTable.TransientWindowCount == 0 &&
                    ApplicationServiceSessionTable.OrphanTransientWindowCount == 0 &&
                    ApplicationInstanceRegistry.ActiveCount == baselineActive;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                global::Program.MarkUefiAppRuntime("NOTEPAD_OPEN_SUCCESS=" +
                    (openSuccess ? "PASS" : "FAIL"));
                global::Program.MarkUefiAppRuntime("NOTEPAD_OPEN_CANCEL=" +
                    (openCancel ? "PASS" : "FAIL"));
                global::Program.MarkUefiAppRuntime("NOTEPAD_SAVE_SUCCESS=" +
                    (saveSuccess ? "PASS" : "FAIL"));
                global::Program.MarkUefiAppRuntime("NOTEPAD_SAVE_CANCEL=" +
                    (saveCancel ? "PASS" : "FAIL"));
                global::Program.MarkUefiAppRuntime("NOTEPAD_CONFIRMATION=" +
                    (confirmation ? "PASS" : "FAIL"));
                global::Program.MarkUefiAppRuntime("NOTEPAD_CROSS_OWNER=" +
                    (crossOwnerRejected ? "PASS" : "FAIL"));
                global::Program.MarkUefiAppRuntime("NOTEPAD_DIALOG_CLEANUP=" +
                    (cleanup ? "PASS" : "FAIL"));
#endif
                return openSuccess && openCancel && saveSuccess && saveCancel &&
                    confirmation && crossOwnerRejected && cleanup;
            } catch {
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                global::Program.MarkUefiAppRuntime("NOTEPAD_SERVICE_DIAGNOSTIC=EXCEPTION");
#endif
                return false;
            }
        }

        private void CompleteDiagnosticMessage() {
            if (!_messageRequest.IsValid) return;
            ApplicationServiceRegistry.CompleteDialogRequestForSelfTest(
                _messageRequest, ApplicationDialogOutcome.Accepted);
            PollServiceRequests();
        }

        public override void Dispose() {
            // CRITICAL FIX: Unsubscribe from keyboard events to prevent memory leak
            Keyboard.OnKeyChanged -= Keyboard_OnKeyChanged;
            base.Dispose();
        }
    }
}
