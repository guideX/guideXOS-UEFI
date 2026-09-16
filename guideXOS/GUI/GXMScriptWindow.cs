using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace guideXOS.GUI {
    /// <summary>
    /// GXM Script Window - Provides a scriptable UI window with buttons, labels, lists, dropdowns, and textboxes
    /// /// </summary>
    internal class GXMScriptWindow : Window {
        #region Nested Types
        
        /// <summary>
        /// Button definition structure
        /// /// </summary>
        internal struct Btn {
            /// <summary>
            /// Unique identifier for the button
            /// /// </summary>
            public int Id;
            
            /// <summary>
            /// Display text shown on the button
            /// /// </summary>
            public string Text;
            
            /// <summary>
            /// X coordinate relative to window
            /// /// </summary>
            public int X;
            
            /// <summary>
            /// Y coordinate relative to window
            /// /// </summary>
            public int Y;
            
            /// <summary>
            /// Button width in pixels
            /// /// </summary>
            public int W;
            
            /// <summary>
            /// Button height in pixels
            /// /// </summary>
            public int H;
        }
        
        /// <summary>
        /// Label definition structure for static text display
        /// /// </summary>
        internal struct Label {
            /// <summary>
            /// Text content to display
            /// /// </summary>
            public string Text;
            
            /// <summary>
            /// X coordinate relative to window
            /// /// </summary>
            public int X;
            
            /// <summary>
            /// Y coordinate relative to window
            /// /// </summary>
            public int Y;
        }
        
        /// <summary>
        /// TextBox definition for editable multi-line text
        /// /// </summary>
        internal class TextBoxDef {
            /// <summary>
            /// Unique identifier for the textbox
            /// /// </summary>
            public int Id;
            
            /// <summary>
            /// X coordinate relative to window
            /// /// </summary>
            public int X;
            
            /// <summary>
            /// Y coordinate relative to window
            /// /// </summary>
            public int Y;
            
            /// <summary>
            /// TextBox width in pixels
            /// /// </summary>
            public int W;
            
            /// <summary>
            /// TextBox height in pixels
            /// /// </summary>
            public int H;
            
            /// <summary>
            /// Text content of the textbox
            /// /// </summary>
            public string Text = string.Empty;
            
            /// <summary>
            /// Whether the textbox is currently focused for input
            /// /// </summary>
            public bool Focused;
            
            /// <summary>
            /// Whether the textbox should wrap text to the next line as needed
            /// /// </summary>
            public bool WordWrap = true;
        }
        
        /// <summary>
        /// ListView definition for displaying selectable item lists
        /// /// </summary>
        internal class ListViewDef {
            /// <summary>
            /// Unique identifier for the listview
            /// /// </summary>
            public int Id;
            
            /// <summary>
            /// X coordinate relative to window
            /// /// </summary>
            public int X;
            
            /// <summary>
            /// Y coordinate relative to window
            /// /// </summary>
            public int Y;
            
            /// <summary>
            /// ListView width in pixels
            /// /// </summary>
            public int W;
            
            /// <summary>
            /// ListView height in pixels
            /// /// </summary>
            public int H;
            
            /// <summary>
            /// Collection of items to display in the listview
            /// /// </summary>
            public List<string> Items = new List<string>(32);
            
            /// <summary>
            /// Index of the currently selected item (-1 if none selected)
            /// /// </summary>
            public int Selected = -1;
        }
        
        /// <summary>
        /// Dropdown (combo box) definition for displaying selectable options
        /// /// </summary>
        internal class DropdownDef {
            /// <summary>
            /// Unique identifier for the dropdown
            /// /// </summary>
            public int Id;
            
            /// <summary>
            /// X coordinate relative to window
            /// /// </summary>
            public int X;
            
            /// <summary>
            /// Y coordinate relative to window
            /// /// </summary>
            public int Y;
            
            /// <summary>
            /// Dropdown width in pixels
            /// /// </summary>
            public int W;
            
            /// <summary>
            /// Dropdown height in pixels
            /// /// </summary>
            public int H;
            
            /// <summary>
            /// Collection of items available in the dropdown
            /// /// </summary>
            public List<string> Items = new List<string>(32);
            
            /// <summary>
            /// Index of the currently selected item (-1 if none selected)
            /// /// </summary>
            public int Selected = -1;
            
            /// <summary>
            /// Whether the dropdown menu is currently expanded
            /// /// </summary>
            public bool Open;
        }
        
        /// <summary>
        /// Callback action definition for UI events
        /// /// </summary>
        internal class Callback {
            /// <summary>
            /// Callback type: 1=click, 2=change, 3=textchange
            /// /// </summary>
            public int Type;
            
            /// <summary>
            /// ID of the control this callback is attached to
            /// /// </summary>
            public int Id;
            
            /// <summary>
            /// Action to execute (e.g., MSG, OPENAPP, CLOSE)
            /// /// </summary>
            public string Action;
            
            /// <summary>
            /// Argument to pass to the action (may contain tokens like $VALUE)
            /// /// </summary>
            public string Arg;
        }
        
        #endregion
        
        #region Private Fields
        
        private List<Btn> _buttons = new List<Btn>(16);
        private List<Label> _labels = new List<Label>(16);
        private List<TextBoxDef> _textboxes = new List<TextBoxDef>(4);
        private List<ListViewDef> _lists = new List<ListViewDef>(8);
        private List<DropdownDef> _dropdowns = new List<DropdownDef>(8);
        private List<Callback> _callbacks = new List<Callback>(16);

        private bool _clickLatch;
        private int _lastClicked = -1;
        
        // Keyboard handling
        private byte _lastScan;
        private bool _keyDown;
        
        // File dialogs
        private SaveDialog _saveDialog;
        private OpenDialog _openDialog;
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Initializes a new instance of the GXMScriptWindow class
        /// /// </summary>
        /// <param name="title">Window title to display</param>
        /// <param name="w">Window width in pixels</param>
        /// <param name="h">Window height in pixels</param>
        public GXMScriptWindow(string title, int w, int h) 
            : base((Framebuffer.Width - w) / 2, (Framebuffer.Height - h) / 2, w, h) { 
            Title = title ?? "Script";
            ShowMinimize = true;
            ShowMaximize = true;
            ShowInTaskbar = true;
            
            // Subscribe to keyboard events for textbox input
            Keyboard.OnKeyChanged += Keyboard_OnKeyChanged;
        }
        
        #endregion
        
        #region Public Methods - Control Creation
        
        /// <summary>
        /// Adds a button to the window
        /// /// </summary>
        /// <param name="id">Unique identifier for the button</param>
        /// <param name="text">Text to display on the button</param>
        /// <param name="x">X coordinate relative to window</param>
        /// <param name="y">Y coordinate relative to window</param>
        /// <param name="w">Button width in pixels</param>
        /// <param name="h">Button height in pixels</param>
        public void AddButton(int id, string text, int x, int y, int w, int h) { 
            Btn b;
            b.Id = id;
            b.Text = text;
            b.X = x;
            b.Y = y;
            b.W = w;
            b.H = h;
            _buttons.Add(b);
        }
        
        /// <summary>
        /// Adds a label (static text) to the window
        /// /// </summary>
        /// <param name="text">Text content to display</param>
        /// <param name="x">X coordinate relative to window</param>
        /// <param name="y">Y coordinate relative to window</param>
        public void AddLabel(string text, int x, int y) { 
            Label l;
            l.Text = text;
            l.X = x;
            l.Y = y;
            _labels.Add(l);
        }
        
        /// <summary>
        /// Adds a multi-line text box control to the window
        /// /// </summary>
        /// <param name="id">Unique identifier for the textbox</param>
        /// <param name="x">X coordinate relative to window</param>
        /// <param name="y">Y coordinate relative to window</param>
        /// <param name="w">TextBox width in pixels</param>
        /// <param name="h">TextBox height in pixels</param>
        /// <param name="initialText">Initial text content (optional)</param>
        public void AddTextBox(int id, int x, int y, int w, int h, string initialText = "") {
            var tb = new TextBoxDef {
                Id = id,
                X = x,
                Y = y,
                W = w,
                H = h,
                Text = initialText ?? string.Empty,
                Focused = false,
                WordWrap = true
            };
            _textboxes.Add(tb);
        }
        
        /// <summary>
        /// Adds a listview control to the window
        /// /// </summary>
        /// <param name="id">Unique identifier for the listview</param>
        /// <param name="x">X coordinate relative to window</param>
        /// <param name="y">Y coordinate relative to window</param>
        /// <param name="w">ListView width in pixels</param>
        /// <param name="h">ListView height in pixels</param>
        /// <param name="items">Semicolon-separated string of items to populate the list</param>
        public void AddList(int id, int x, int y, int w, int h, string items) {
            var lv = new ListViewDef {
                Id = id,
                X = x,
                Y = y,
                W = w,
                H = h
            };
            
            if (items != null) {
                int start = 0;
                for (int i = 0; i <= items.Length; i++) {
                    if (i == items.Length || items[i] == ';') {
                        int len = i - start;
                        if (len > 0) {
                            lv.Items.Add(items.Substring(start, len));
                        }
                        start = i + 1;
                    }
                }
            }
            
            _lists.Add(lv);
        }
        
        /// <summary>
        /// Adds a dropdown (combo box) control to the window
        /// /// </summary>
        /// <param name="id">Unique identifier for the dropdown</param>
        /// <param name="x">X coordinate relative to window</param>
        /// <param name="y">Y coordinate relative to window</param>
        /// <param name="w">Dropdown width in pixels</param>
        /// <param name="h">Dropdown height in pixels</param>
        /// <param name="items">Semicolon-separated string of items to populate the dropdown</param>
        public void AddDropdown(int id, int x, int y, int w, int h, string items) {
            var dd = new DropdownDef {
                Id = id,
                X = x,
                Y = y,
                W = w,
                H = h
            };
            
            if (items != null) {
                int start = 0;
                for (int i = 0; i <= items.Length; i++) {
                    if (i == items.Length || items[i] == ';') {
                        int len = i - start;
                        if (len > 0) {
                            dd.Items.Add(items.Substring(start, len));
                        }
                        start = i + 1;
                    }
                }
            }
            
            _dropdowns.Add(dd);
        }
        
        #endregion
        
        #region Public Methods - Event Registration
        
        /// <summary>
        /// Registers a callback to execute when a control is clicked
        /// /// </summary>
        /// <param name="id">ID of the control to attach the callback to</param>
        /// <param name="action">Action to execute (MSG, OPENAPP, CLOSE)</param>
        /// <param name="arg">Argument to pass to the action</param>
        public void AddOnClick(int id, string action, string arg) {
            var cb = new Callback {
                Type = 1,
                Id = id,
                Action = action,
                Arg = arg
            };
            _callbacks.Add(cb);
        }
        
        /// <summary>
        /// Registers a callback to execute when a control's value changes
        /// /// </summary>
        /// <param name="id">ID of the control to attach the callback to</param>
        /// <param name="action">Action to execute (MSG, OPENAPP, CLOSE)</param>
        /// <param name="arg">Argument to pass to the action (may contain $VALUE token)</param>
        public void AddOnChange(int id, string action, string arg) {
            var cb = new Callback {
                Type = 2,
                Id = id,
                Action = action,
                Arg = arg
            };
            _callbacks.Add(cb);
        }
        
        /// <summary>
        /// Registers a callback for textbox text changes
        /// /// </summary>
        /// <param name="id">ID of the control to attach the callback to</param>
        /// <param name="action">Action to execute (MSG, OPENAPP, CLOSE)</param>
        /// <param name="arg">Argument to pass to the action</param>
        public void AddOnTextChange(int id, string action, string arg) {
            var cb = new Callback {
                Type = 3,
                Id = id,
                Action = action,
                Arg = arg
            };
            _callbacks.Add(cb);
        }
        
        #endregion
        
        #region Keyboard Handling
        
        private void Keyboard_OnKeyChanged(object sender, ConsoleKeyInfo key) {
            if (!Visible || IsMinimized || IsTombstoned)
                return;
            
            if (key.KeyState != ConsoleKeyState.Pressed) {
                _keyDown = false;
                _lastScan = 0;
                return;
            }
            
            if (_keyDown && Keyboard.KeyInfo.ScanCode == _lastScan)
                return; // de-bounce
            
            _keyDown = true;
            _lastScan = (byte)Keyboard.KeyInfo.ScanCode;
            
            // Find focused textbox
            TextBoxDef focusedTb = null;
            for (int i = 0; i < _textboxes.Count; i++) {
                if (_textboxes[i].Focused) {
                    focusedTb = _textboxes[i];
                    break;
                }
            }
            
            if (focusedTb == null)
                return;
            
            // Handle keyboard input for textbox
            if (key.Key == ConsoleKey.Backspace) {
                if (focusedTb.Text.Length > 0) {
                    focusedTb.Text = focusedTb.Text.Substring(0, focusedTb.Text.Length - 1);
                    RunActions(3, focusedTb.Id, focusedTb.Text);
                }
                return;
            }
            
            if (key.Key == ConsoleKey.Enter) {
                focusedTb.Text += "\n";
                RunActions(3, focusedTb.Id, focusedTb.Text);
                return;
            }
            
            if (key.Key == ConsoleKey.Tab) {
                focusedTb.Text += "    ";
                RunActions(3, focusedTb.Id, focusedTb.Text);
                return;
            }
            
            char ch = MapFromKey(key);
            if (ch != '\0') {
                focusedTb.Text += ch;
                RunActions(3, focusedTb.Id, focusedTb.Text);
            }
        }
        
        private static char MapFromKey(ConsoleKeyInfo key) {
            if (key.KeyChar != '\0') return key.KeyChar;
            
            var k = key.Key;
            bool shift = Keyboard.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift);
            bool caps = Keyboard.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.CapsLock);
            
            if (k == ConsoleKey.Space) return ' ';
            
            if (k >= ConsoleKey.A && k <= ConsoleKey.Z) {
                char c = (char)('a' + (k - ConsoleKey.A));
                if (shift ^ caps) {
                    if (c >= 'a' && c <= 'z')
                        c = (char)('A' + (c - 'a'));
                }
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
        
        #endregion
        
        #region Input Handling
        
        /// <summary>
        /// Processes user input events (mouse clicks on buttons, lists, dropdowns)
        /// /// </summary>
        public override void OnInput() {
            base.OnInput();
            
            if (!Visible || IsMinimized || IsTombstoned)
                return;
                
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
            
            if (left) {
                if (!_clickLatch) {
                    // Process textbox clicks (for focus)
                    bool tbClicked = false;
                    for (int i = 0; i < _textboxes.Count; i++) {
                        var tb = _textboxes[i];
                        int rx = X + tb.X;
                        int ry = Y + tb.Y;
                        
                        if (mx >= rx && mx <= rx + tb.W && my >= ry && my <= ry + tb.H) {
                            // Set focus to this textbox
                            for (int j = 0; j < _textboxes.Count; j++) {
                                _textboxes[j].Focused = (j == i);
                            }
                            tbClicked = true;
                            _clickLatch = true;
                            break;
                        }
                    }
                    
                    if (!tbClicked) {
                        // Unfocus all textboxes if clicked elsewhere
                        for (int i = 0; i < _textboxes.Count; i++) {
                            _textboxes[i].Focused = false;
                        }
                    }
                    
                    // Process button clicks
                    for (int i = 0; i < _buttons.Count; i++) {
                        var b = _buttons[i];
                        int rx = X + b.X;
                        int ry = Y + b.Y;
                        
                        if (mx >= rx && mx <= rx + b.W && my >= ry && my <= ry + b.H) {
                            _lastClicked = b.Id;
                            _clickLatch = true;
                            RunActions(1, b.Id, null);
                            break;
                        }
                    }
                    
                    // Process dropdown clicks
                    for (int i = 0; i < _dropdowns.Count; i++) {
                        var d = _dropdowns[i];
                        int rx = X + d.X;
                        int ry = Y + d.Y;
                        
                        // Click on dropdown header to toggle
                        if (mx >= rx && mx <= rx + d.W && my >= ry && my <= ry + d.H) {
                            d.Open = !d.Open;
                            _clickLatch = true;
                            continue;
                        }
                        
                        // Click on dropdown items when expanded
                        if (d.Open) {
                            int itemY = ry + d.H;
                            for (int it = 0; it < d.Items.Count; it++) {
                                int ih = WindowManager.font.FontSize + 6;
                                int iy = itemY + it * ih;
                                
                                if (mx >= rx && mx <= rx + d.W && my >= iy && my <= iy + ih) {
                                    d.Selected = it;
                                    d.Open = false;
                                    _clickLatch = true;
                                    RunActions(2, d.Id, d.Items[it]);
                                    break;
                                }
                            }
                        }
                    }
                    
                    // Process listview clicks
                    for (int i = 0; i < _lists.Count; i++) {
                        var l = _lists[i];
                        int rx = X + l.X;
                        int ry = Y + l.Y;
                        
                        if (mx >= rx && mx <= rx + l.W && my >= ry && my <= ry + l.H) {
                            int rowH = WindowManager.font.FontSize + 6;
                            int rel = my - ry;
                            int idx = rel / rowH;
                            
                            if (idx >= 0 && idx < l.Items.Count) {
                                l.Selected = idx;
                                _clickLatch = true;
                                RunActions(2, l.Id, l.Items[idx]);
                            }
                        }
                    }
                }
            } else {
                _clickLatch = false;
            }
        }
        
        #endregion
        
        #region Action Execution
        
        /// <summary>
        /// Executes all registered callbacks matching the specified type and control ID
        /// /// </summary>
        /// <param name="type">Callback type (1=click, 2=change)</param>
        /// <param name="id">ID of the control that triggered the event</param>
        /// <param name="value">Optional value associated with the event (e.g., selected item text)</param>
        private void RunActions(int type, int id, string value) {
            for (int i = 0; i < _callbacks.Count; i++) {
                var cb = _callbacks[i];
                
                if (cb.Type == type && cb.Id == id) {
                    string act = cb.Action ?? string.Empty;
                    string arg = cb.Arg ?? string.Empty;
                    
                    // Replace $VALUE token with actual value if provided
                    if (value != null) {
                        arg = ReplaceToken(arg, "$VALUE", value);
                    }
                    
                    // Special handling for textbox actions
                    if (type == 3) { // text change
                        // Update textbox text for GETTEXT action
                        ExecuteTextAction(act, arg, id, value);
                    } else {
                        ExecuteAction(act, arg);
                    }
                }
            }
        }
        
        /// <summary>
        /// Executes a scripted action with the provided argument
        /// Supported actions: MSG (show message), OPENAPP (launch app), CLOSE (close window), SAVEFILE (save text), LOADFILE (load text)
        /// /// </summary>
        /// <param name="action">Action name (case-insensitive)</param>
        /// <param name="arg">Action-specific argument</param>
        private void ExecuteAction(string action, string arg) {
            // Normalize action to uppercase
            string a = action;
            char[] ca = new char[a.Length];
            
            for (int i = 0; i < a.Length; i++) {
                char c = a[i];
                if (c >= 'a' && c <= 'z')
                    c = (char)(c - 32);
                ca[i] = c;
            }
            
            a = new string(ca);
            ca.Dispose();
            
            // Execute based on action type
            if (a == "MSG") {
                Notify(arg);
            } else if (a == "OPENAPP") {
                if (arg != null) Desktop.LaunchApplication(arg);
            } else if (a == "CLOSE") {
                this.Visible = false;
            } else if (a == "CLEAR") {
                // Clear textbox content - arg contains the textbox ID
                int id = ParseInt(arg);
                for (int i = 0; i < _textboxes.Count; i++) {
                    if (_textboxes[i].Id == id) {
                        // Dispose old text and set to empty
                        if (_textboxes[i].Text != null && _textboxes[i].Text.Length > 0) {
                            _textboxes[i].Text.Dispose();
                        }
                        _textboxes[i].Text = string.Empty;
                        break;
                    }
                }
            } else if (a == "SAVEFILE") {
                // Save file functionality
                SaveFileAction(arg);
            } else if (a == "LOADFILE") {
                // Load file functionality
                LoadFileAction(arg);
            } else if (a == "SAVETEXT") {
                // Save text from first textbox
                SaveTextFromTextBox(arg);
            } else if (a == "LOADTEXT") {
                // Load text into first textbox
                LoadTextIntoTextBox(arg);
            } else if (a == "SAVEDIALOG") {
                // Open save dialog for textbox
                OpenSaveDialogForTextBox(arg);
            } else if (a == "OPENDIALOG") {
                // Open open dialog for textbox
                OpenOpenDialogForTextBox();
            } else if (a == "LOADSCRIPT") {
                // Load and execute GXM script from .txt file
                LoadAndExecuteScript(arg);
            }
            
            // FIXED: Dispose normalized action string if it's different from input
            if (a != action) {
                a.Dispose();
            }
        }
        
        private void ExecuteTextAction(string action, string arg, int textboxId, string currentText) {
            string a = action.ToUpper();
            
            if (a == "GETTEXT") {
                // Store text from textbox to a variable (handled by specific actions)
                ExecuteAction(action, currentText);
            } else {
                ExecuteAction(action, arg);
            }
            
            if (a != action) a.Dispose();
        }
        
        /// <summary>
        /// Replaces a single token in a string with a value (simple naive implementation)
        /// /// </summary>
        /// <param name="s">Source string</param>
        /// <param name="token">Token to replace (e.g., "$VALUE")</param>
        /// <param name="val">Value to substitute</param>
        /// <returns>String with token replaced</returns>
        private string ReplaceToken(string s, string token, string val) {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(token))
                return s;
                
            int i = IndexOf(s, token);
            if (i < 0)
                return s;
                
            string a = s.Substring(0, i);
            string b = s.Substring(i + token.Length);
            string result = a + val + b;
            
            // FIXED: Dispose intermediate strings to prevent leak
            a.Dispose();
            b.Dispose();
            
            return result;
        }
        
        /// <summary>
        /// Finds the index of a substring within a string
        /// /// </summary>
        /// <param name="s">Source string to search</param>
        /// <param name="token">Substring to find</param>
        /// <returns>Index of first occurrence, or -1 if not found</returns>
        private int IndexOf(string s, string token) {
            int n = s.Length;
            int m = token.Length;
            
            if (m == 0)
                return -1;
                
            for (int i = 0; i <= n - m; i++) {
                int k = 0;
                for (; k < m; k++) {
                    if (s[i + k] != token[k])
                        break;
                }
                if (k == m)
                    return i;
            }
            
            return -1;
        }
        
        /// <summary>
        /// Converts a string to an integer
        /// </summary>
        /// <param name="s">String to convert</param>
        /// <returns>Integer value</returns>
        private static int ParseInt(string s) {
            int n = 0;
            bool neg = false;
            if (!string.IsNullOrEmpty(s)) {
                int i = 0;
                if (s[0] == '-') {
                    neg = true;
                    i = 1;
                }
                for (; i < s.Length; i++) {
                    char ch = s[i];
                    if (ch < '0' || ch > '9') break;
                    n = n * 10 + (ch - '0');
                }
            }
            return neg ? -n : n;
        }
        #endregion

        #region File Operations

        /// <summary>
        /// Saves text content to a file
        /// /// </summary>
        /// <param name="filename">Filename to save (without path, saves to current Desktop.Dir)</param>
        private void SaveFileAction(string filename) {
            if (string.IsNullOrEmpty(filename)) {
                filename = "notepad.txt";
            }
            
            // Get text content from the first list (if any)
            string content = "";
            if (_lists.Count > 0) {
                var list = _lists[0];
                for (int i = 0; i < list.Items.Count; i++) {
                    string oldContent = content;
                    content += list.Items[i];
                    if (i < list.Items.Count - 1) {
                        string temp = content;
                        content += "\n";
                        temp.Dispose(); // FIXED: Dispose intermediate concatenation
                    }
                    if (i > 0) oldContent.Dispose(); // FIXED: Dispose old content string
                }
            }
            
            // Build full path
            string path = Desktop.Dir + filename;
            
            try {
                // Convert string to byte array
                byte[] data = new byte[content.Length];
                for (int i = 0; i < content.Length; i++) {
                    data[i] = (byte)content[i];
                }
                
                // Write to file
                File.WriteAllBytes(path, data);
                data.Dispose();
                
                // Invalidate directory cache
                Desktop.InvalidateDirCache();
                
                // Show success message
                string msg = $"File saved: {filename}\nLocation: {Desktop.Dir}";
                Notify(msg);
                msg.Dispose(); // FIXED: Dispose message string
                
                // Add to recent documents
                RecentManager.AddDocument(path, Icons.DocumentIcon(32));
            } catch {
                string errMsg = $"Error: Failed to save file {filename}";
                Notify(errMsg);
                errMsg.Dispose(); // FIXED: Dispose error message
            }
            
            // FIXED: Dispose content and path strings
            content.Dispose();
            path.Dispose();
        }
        
        /// <summary>
        /// Loads text content from a file
        /// /// </summary>
        /// <param name="filename">Filename to load (without path, loads from current Desktop.Dir)</param>
        private void LoadFileAction(string filename) {
            if (string.IsNullOrEmpty(filename)) {
                Notify("Error: No filename specified");
                return;
            }
            
            // Build full path
            string path = Desktop.Dir + filename;
            
            try {
                // Read file
                byte[] data = File.ReadAllBytes(path);
                
                if (data == null || data.Length == 0) {
                    string errMsg = $"Error: File {filename} is empty or not found";
                    Notify(errMsg);
                    errMsg.Dispose(); // FIXED: Dispose error message
                    path.Dispose(); // FIXED: Dispose path
                    return;
                }
                
                // Convert bytes to string
                string content = "";
                for (int i = 0; i < data.Length; i++) {
                    string oldContent = content;
                    content += (char)data[i];
                    if (i > 0) oldContent.Dispose(); // FIXED: Dispose old content string
                }
                data.Dispose();
                
                // Split into lines
                List<string> lines = new List<string>(32);
                string currentLine = "";
                for (int i = 0; i < content.Length; i++) {
                    char c = content[i];
                    if (c == '\n' || c == '\r') {
                        if (currentLine.Length > 0 || c == '\n') {
                            lines.Add(currentLine);
                            currentLine = "";
                        }
                    } else {
                        string oldLine = currentLine;
                        currentLine += c;
                        if (i > 0 && oldLine.Length > 0) oldLine.Dispose(); // FIXED: Dispose old line string
                    }
                }
                if (currentLine.Length > 0) {
                    lines.Add(currentLine);
                } else {
                    currentLine.Dispose(); // FIXED: Dispose empty current line
                }
                
                // Update the first list with loaded content
                if (_lists.Count > 0) {
                    var list = _lists[0];
                    list.Items.Clear();
                    for (int i = 0; i < lines.Count; i++) {
                        list.Items.Add(lines[i]);
                    }
                    list.Selected = -1;
                }
                
                // Show success message
                string linesStr = lines.Count.ToString();
                string msg = $"File loaded: {filename}\nLines: {linesStr}";
                Notify(msg);
                linesStr.Dispose(); // FIXED: Dispose lines count string
                msg.Dispose(); // FIXED: Dispose message string
                
                // FIXED: Don't dispose lines items - they're now owned by the list
                lines.Dispose();
                
                // Add to recent documents
                RecentManager.AddDocument(path, Icons.DocumentIcon(32));
                
                // FIXED: Dispose content and path strings
                content.Dispose();
                path.Dispose();
            } catch {
                string errMsg = $"Error: Failed to load file {filename}";
                Notify(errMsg);
                errMsg.Dispose(); // FIXED: Dispose error message
                path.Dispose(); // FIXED: Dispose path
            }
        }
        
        private void SaveTextFromTextBox(string filename) {
            if (string.IsNullOrEmpty(filename)) {
                filename = "notes.txt";
            }
            
            string content = "";
            if (_textboxes.Count > 0) {
                content = _textboxes[0].Text ?? "";
            }
            
            string path = Desktop.Dir + filename;
            
            try {
                byte[] data = new byte[content.Length];
                for (int i = 0; i < content.Length; i++) {
                    data[i] = (byte)content[i];
                }
                
                File.WriteAllBytes(path, data);
                data.Dispose();
                
                Desktop.InvalidateDirCache();
                
                string msg = $"File saved: {filename}";
                Notify(msg);
                msg.Dispose();
                
                RecentManager.AddDocument(path, Icons.DocumentIcon(32));
            } catch {
                string errMsg = $"Error: Failed to save file {filename}";
                Notify(errMsg);
                errMsg.Dispose();
            }
            
            path.Dispose();
        }
        
        private void LoadTextIntoTextBox(string filename) {
            if (string.IsNullOrEmpty(filename)) {
                Notify("Error: No filename specified");
                return;
            }
            
            string path = Desktop.Dir + filename;
            
            try {
                byte[] data = File.ReadAllBytes(path);
                
                if (data == null || data.Length == 0) {
                    string errMsg = $"Error: File {filename} is empty or not found";
                    Notify(errMsg);
                    errMsg.Dispose();
                    path.Dispose();
                    return;
                }
                
                string content = "";
                for (int i = 0; i < data.Length; i++) {
                    char ch = (char)data[i];
                    if (ch >= 32 || ch == 10 || ch == 13) {
                        content += ch;
                    }
                }
                data.Dispose();
                
                if (_textboxes.Count > 0) {
                    _textboxes[0].Text = content;
                }
                
                string msg = $"File loaded: {filename}";
                Notify(msg);
                msg.Dispose();
                
                RecentManager.AddDocument(path, Icons.DocumentIcon(32));
                
                path.Dispose();
            } catch {
                string errMsg = $"Error: Failed to load file {filename}";
                Notify(errMsg);
                errMsg.Dispose();
                path.Dispose();
            }
        }
        
        /// <summary>
        /// Opens a Save As dialog for the textbox content
        /// /// </summary>
        /// <param name="defaultName">Default filename (optional)</param>
        private void OpenSaveDialogForTextBox(string defaultName) {
            if (_saveDialog != null && _saveDialog.Visible)
                return; // Dialog already open
            
            string fileName = defaultName ?? "document.txt";
            
            _saveDialog = new SaveDialog(X + 40, Y + 40, 520, 360, Desktop.Dir, fileName, (path) => {
                // On save callback - save textbox content to the selected file
                if (_textboxes.Count > 0) {
                    string content = _textboxes[0].Text ?? "";
                    
                    try {
                        byte[] data = new byte[content.Length];
                        for (int i = 0; i < content.Length; i++) {
                            data[i] = (byte)content[i];
                        }
                        
                        File.WriteAllBytes(path, data);
                        data.Dispose();
                        
                        Desktop.InvalidateDirCache();
                        
                        // Extract filename from path
                        string filename = path.Substring(path.LastIndexOf('/') + 1);
                        string msg = $"File saved: {filename}";
                        Notify(msg);
                        msg.Dispose();
                        filename.Dispose();
                        
                        RecentManager.AddDocument(path, Icons.DocumentIcon(32));
                    } catch {
                        string errMsg = $"Error: Failed to save file";
                        Notify(errMsg);
                        errMsg.Dispose();
                    }
                }
            });
            
            WindowManager.MoveToEnd(_saveDialog);
            _saveDialog.Visible = true;
        }
        
        /// <summary>
        /// Opens an Open dialog to load a file into the textbox
        /// /// </summary>
        private void OpenOpenDialogForTextBox() {
            if (_openDialog != null && _openDialog.Visible)
                return; // Dialog already open
            
            _openDialog = new OpenDialog(X + 40, Y + 40, 520, 360, Desktop.Dir, (path) => {
                // On open callback - load selected file into textbox
                try {
                    byte[] data = File.ReadAllBytes(path);
                    
                    if (data == null || data.Length == 0) {
                        string errMsg = $"Error: File is empty or not found";
                        Notify(errMsg);
                        errMsg.Dispose();
                        return;
                    }
                    
                    string content = "";
                    for (int i = 0; i < data.Length; i++) {
                        char ch = (char)data[i];
                        if (ch >= 32 || ch == 10 || ch == 13) {
                            content += ch;
                        }
                    }
                    data.Dispose();
                    
                    if (_textboxes.Count > 0) {
                        _textboxes[0].Text = content;
                    }
                    
                    // Extract filename from path
                    string filename = path.Substring(path.LastIndexOf('/') + 1);
                    string msg = $"File loaded: {filename}";
                    Notify(msg);
                    msg.Dispose();
                    filename.Dispose();
                    
                    RecentManager.AddDocument(path, Icons.DocumentIcon(32));
                } catch {
                    string errMsg = $"Error: Failed to load file";
                    Notify(errMsg);
                    errMsg.Dispose();
                }
            });
            
            WindowManager.MoveToEnd(_openDialog);
            _openDialog.Visible = true;
        }
        
        /// <summary>
        /// Loads and executes a GXM script from a .txt file directly
        /// /// </summary>
        /// <param name="filename">Script filename (without path, loads from Desktop.Dir)</param>
        private void LoadAndExecuteScript(string filename) {
            if (string.IsNullOrEmpty(filename)) {
                Notify("Error: No script filename specified");
                return;
            }
            
            string path = Desktop.Dir + filename;
            
            try {
                byte[] data = File.ReadAllBytes(path);
                
                if (data == null || data.Length == 0) {
                    string errMsg = $"Error: Script file {filename} is empty or not found";
                    Notify(errMsg);
                    errMsg.Dispose();
                    path.Dispose();
                    return;
                }
                
                // Create a new GXM header with GUI marker
                int headerSize = 20; // GXM header (16) + GUI marker (4)
                int totalSize = headerSize + data.Length + 1; // +1 for null terminator
                byte[] gxmData = new byte[totalSize];
                
                // Write GXM header
                gxmData[0] = (byte)'G';
                gxmData[1] = (byte)'X';
                gxmData[2] = (byte)'M';
                gxmData[3] = 0;
                
                // Version (1)
                gxmData[4] = 1;
                gxmData[5] = 0;
                gxmData[6] = 0;
                gxmData[7] = 0;
                
                // Entry RVA (0 for GUI scripts)
                gxmData[8] = 0;
                gxmData[9] = 0;
                gxmData[10] = 0;
                gxmData[11] = 0;
                
                // Image size
                uint size = (uint)totalSize;
                gxmData[12] = (byte)(size & 0xFF);
                gxmData[13] = (byte)((size >> 8) & 0xFF);
                gxmData[14] = (byte)((size >> 16) & 0xFF);
                gxmData[15] = (byte)((size >> 24) & 0xFF);
                
                // GUI marker
                gxmData[16] = (byte)'G';
                gxmData[17] = (byte)'U';
                gxmData[18] = (byte)'I';
                gxmData[19] = 0;
                
                // Copy script data
                for (int i = 0; i < data.Length; i++) {
                    gxmData[headerSize + i] = data[i];
                }
                
                // Null terminator
                gxmData[totalSize - 1] = 0;
                
                data.Dispose();
                
                // Execute the GXM
                string error;
                if (GXMLoader.TryExecute(gxmData, out error)) {
                    string msg = $"Script loaded: {filename}";
                    Notify(msg);
                    msg.Dispose();
                } else {
                    string errMsg = $"Error executing script: {error}";
                    Notify(errMsg);
                    errMsg.Dispose();
                    error.Dispose();
                }
                
                gxmData.Dispose();
                path.Dispose();
            } catch {
                string errMsg = $"Error: Failed to load script {filename}";
                Notify(errMsg);
                errMsg.Dispose();
                path.Dispose();
            }
        }
        
        /// <summary>
        /// Displays a notification message box to the user
        /// /// </summary>
        /// <param name="msg">Message text to display</param>
        private void Notify(string msg) {
            if (Desktop.msgbox != null) {
                Desktop.msgbox.SetText(msg);
                Desktop.msgbox.X = X + 20;
                Desktop.msgbox.Y = Y + 20;
                WindowManager.MoveToEnd(Desktop.msgbox);
                Desktop.msgbox.Visible = true;
            }
        }
        
        #endregion
        
        #region Drawing
        
        /// <summary>
        /// Renders all UI controls (labels, buttons, lists, dropdowns) to the screen
        /// /// </summary>
        public override void OnDraw() {
            base.OnDraw();
            
            if (IsMinimized)
                return;
                
            // Draw labels
            for (int i = 0; i < _labels.Count; i++) {
                var l = _labels[i];
                WindowManager.font.DrawString(
                    X + l.X, 
                    Y + l.Y, 
                    l.Text ?? "", 
                    Width - 16, 
                    WindowManager.font.FontSize * 3
                );
            }
            
            // Draw textboxes
            for (int i = 0; i < _textboxes.Count; i++) {
                var tb = _textboxes[i];
                int rx = X + tb.X;
                int ry = Y + tb.Y;
                
                // Background with focus indication
                uint bgColor = tb.Focused ? 0xFF323232 : 0x80282828;
                Framebuffer.Graphics.AFillRectangle(rx, ry, tb.W, tb.H, bgColor);
                
                // Border
                uint borderColor = tb.Focused ? 0xFF3F7FBF : 0xFF3A3A3A;
                Framebuffer.Graphics.DrawRectangle(rx, ry, tb.W, tb.H, borderColor, 1);
                
                // Text with word wrap
                if (tb.WordWrap) {
                    WindowManager.font.DrawString(
                        rx + 6, 
                        ry + 6, 
                        tb.Text ?? "", 
                        tb.W - 12, 
                        WindowManager.font.FontSize * 3
                    );
                } else {
                    WindowManager.font.DrawString(rx + 6, ry + 6, tb.Text ?? "");
                }
                
                // Cursor if focused
                if (tb.Focused) {
                    int cursorX = rx + 6 + WindowManager.font.MeasureString(tb.Text ?? "");
                    int cursorY = ry + 6;
                    Framebuffer.Graphics.DrawLine(
                        cursorX, 
                        cursorY, 
                        cursorX, 
                        cursorY + WindowManager.font.FontSize, 
                        0xFFFFFFFF
                    );
                }
            }
            
            // Draw buttons
            for (int i = 0; i < _buttons.Count; i++) {
                var b = _buttons[i];
                uint fill = (b.Id == _lastClicked) ? 0xFF2E86C1u : 0xFF3A3A3A;
                
                Framebuffer.Graphics.FillRectangle(X + b.X, Y + b.Y, b.W, b.H, fill);
                WindowManager.font.DrawString(
                    X + b.X + 6, 
                    Y + b.Y + (b.H / 2 - WindowManager.font.FontSize / 2), 
                    b.Text ?? "Button"
                );
            }
            
            // Draw listviews
            for (int i = 0; i < _lists.Count; i++) {
                var l = _lists[i];
                Framebuffer.Graphics.AFillRectangle(X + l.X, Y + l.Y, l.W, l.H, 0x80282828);
                
                int rowH = WindowManager.font.FontSize + 6;
                int y = Y + l.Y;
                
                for (int it = 0; it < l.Items.Count && y + rowH <= Y + l.Y + l.H; it++) {
                    // Highlight selected item
                    if (it == l.Selected)
                        Framebuffer.Graphics.AFillRectangle(X + l.X, y, l.W, rowH, 0x802E86C1);
                        
                    WindowManager.font.DrawString(
                        X + l.X + 6, 
                        y + 3, 
                        l.Items[it], 
                        l.W - 12, 
                        WindowManager.font.FontSize
                    );
                    y += rowH;
                }
            }
            
            // Draw dropdowns
            for (int i = 0; i < _dropdowns.Count; i++) {
                var d = _dropdowns[i];
                int rx = X + d.X;
                int ry = Y + d.Y;
                
                // Draw dropdown header
                Framebuffer.Graphics.FillRectangle(rx, ry, d.W, d.H, 0xFF2E2E2E);
                
                string txt = d.Selected >= 0 && d.Selected < d.Items.Count 
                    ? d.Items[d.Selected] 
                    : "(select)";
                    
                WindowManager.font.DrawString(
                    rx + 6, 
                    ry + (d.H / 2 - WindowManager.font.FontSize / 2), 
                    txt, 
                    d.W - 12, 
                    WindowManager.font.FontSize
                );
                
                // Draw arrow indicator
                Framebuffer.Graphics.DrawLine(rx + d.W - 16, ry + 6, rx + d.W - 6, ry + 6, 0xFFAAAAAA);
                Framebuffer.Graphics.DrawLine(rx + d.W - 16, ry + 6, rx + d.W - 11, ry + d.H - 6, 0xFFAAAAAA);
                Framebuffer.Graphics.DrawLine(rx + d.W - 6, ry + 6, rx + d.W - 11, ry + d.H - 6, 0xFFAAAAAA);
                
                // Draw expanded dropdown items
                if (d.Open) {
                    int itemY = ry + d.H;
                    int ih = WindowManager.font.FontSize + 6;
                    
                    for (int it = 0; it < d.Items.Count; it++) {
                        Framebuffer.Graphics.FillRectangle(
                            rx, 
                            itemY + it * ih, 
                            d.W, 
                            ih, 
                            0xFF2A2A2A
                        );
                        
                        WindowManager.font.DrawString(
                            rx + 6, 
                            itemY + it * ih + 3, 
                            d.Items[it], 
                            d.W - 12, 
                            WindowManager.font.FontSize
                        );
                    }
                    
                    Framebuffer.Graphics.DrawRectangle(
                        rx, 
                        itemY, 
                        d.W, 
                        d.Items.Count * ih, 
                        0xFF3A3A3A, 
                        1
                    );
                }
            }
        }
        
        #endregion
        
        #region Disposal
        
        /// <summary>
        /// Dispose all resources used by this window
        /// /// </summary>
        public override void Dispose() {
            // Unsubscribe from keyboard events
            Keyboard.OnKeyChanged -= Keyboard_OnKeyChanged;
            
            // Dispose dialogs
            if (_saveDialog != null) {
                _saveDialog.Dispose();
                _saveDialog = null;
            }
            if (_openDialog != null) {
                _openDialog.Dispose();
                _openDialog = null;
            }
            
            // Dispose textboxes
            if (_textboxes != null) {
                for (int i = 0; i < _textboxes.Count; i++) {
                    var tb = _textboxes[i];
                    if (tb != null && tb.Text != null) {
                        tb.Text.Dispose();
                    }
                }
                _textboxes.Clear();
                _textboxes.Dispose();
                _textboxes = null;
            }
            
            // Dispose all string data in buttons
            if (_buttons != null) {
                for (int i = 0; i < _buttons.Count; i++) {
                    var b = _buttons[i];
                    if (b.Text != null) {
                        b.Text.Dispose();
                    }
                }
                _buttons.Clear();
                _buttons.Dispose();
                _buttons = null;
            }
            
            // Dispose all string data in labels
            if (_labels != null) {
                for (int i = 0; i < _labels.Count; i++) {
                    var l = _labels[i];
                    if (l.Text != null) {
                        l.Text.Dispose();
                    }
                }
                _labels.Clear();
                _labels.Dispose();
                _labels = null;
            }
            
            // Dispose all listviews and their items
            if (_lists != null) {
                for (int i = 0; i < _lists.Count; i++) {
                    var list = _lists[i];
                    if (list != null && list.Items != null) {
                        for (int j = 0; j < list.Items.Count; j++) {
                            if (list.Items[j] != null) {
                                list.Items[j].Dispose();
                            }
                        }
                        list.Items.Clear();
                        list.Items.Dispose();
                    }
                }
                _lists.Clear();
                _lists.Dispose();
                _lists = null;
            }
            
            // Dispose all dropdowns and their items
            if (_dropdowns != null) {
                for (int i = 0; i < _dropdowns.Count; i++) {
                    var dd = _dropdowns[i];
                    if (dd != null && dd.Items != null) {
                        for (int j = 0; j < dd.Items.Count; j++) {
                            if (dd.Items[j] != null) {
                                dd.Items[j].Dispose();
                            }
                        }
                        dd.Items.Clear();
                        dd.Items.Dispose();
                    }
                }
                _dropdowns.Clear();
                _dropdowns.Dispose();
                _dropdowns = null;
            }
            
            // Dispose all callbacks
            if (_callbacks != null) {
                for (int i = 0; i < _callbacks.Count; i++) {
                    var cb = _callbacks[i];
                    if (cb != null) {
                        if (cb.Action != null) {
                            cb.Action.Dispose();
                        }
                        if (cb.Arg != null) {
                            cb.Arg.Dispose();
                        }
                    }
                }
                _callbacks.Clear();
                _callbacks.Dispose();
                _callbacks = null;
            }
            
            // Call base dispose to handle window-level resources
            base.Dispose();
        }
        
        #endregion
    }
}
