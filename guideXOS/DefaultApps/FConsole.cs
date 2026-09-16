using guideXOS.Kernel.Drivers;
using System;
using System.Drawing;
using guideXOS.OS;
using guideXOS.GUI;
using guideXOS.Compat;
namespace guideXOS.DefaultApps {
    /// <summary>
    /// Console
    /// </summary>
    internal class FConsole : Window {
        private string Data; // output buffer
        public Image ScreenBuf;
        private string Cmd; // current input
        private bool _keyDown = false;
        private byte _lastScan = 0;
        private string _prompt = ">"; // prompt symbol
        private string _cwd = ""; // current working directory

        // vi state
        private string _textBufferForVi;
        private string _viPath;
        private bool _viMode;
        private bool _viInsert;
        private int _viCursor;

        // Ping state tracking
        private bool _pingInProgress = false;
        private ulong _pingStartTime = 0;
        private int _pingTimeout = 1000;
        private NETv4.IPAddress _pingTarget;
        private string _pingHostname = "";

        public FConsole(int X, int Y) : base(X, Y, 640, 320) {
            ShowInTaskbar = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowTombstone = true;
            IsResizable = true;
            Title = "Console";
            Cmd = string.Empty;
            Data = string.Empty;
            ScreenBuf = new Image(640, 320);
            ASC16.Initialize();
            _cwd = Desktop.Dir ?? "";
            UpdateTitle();
            Rebind();
            Console.OnWrite += Console_OnWrite;
            WriteLine("Type help to get information!");
            WritePrompt();
            // vi init
            _textBufferForVi = string.Empty;
            _viPath = null;
            _viMode = false;
            _viInsert = false;
            _viCursor = 0;
        }

        private void UpdateTitle() {
            Title = string.IsNullOrEmpty(_cwd) ? "Console" : "Console - /" + _cwd;
        }

        private void WritePrompt() {
            AppendRaw(GetPromptString());
        }
        private string GetPromptString() {
            // Format prompt like [/path/] >
            string p = string.IsNullOrEmpty(_cwd) ? "/" : "/" + _cwd;
            if (!p.EndsWith("/")) p += "/";
            return "[" + p + "] " + _prompt + " ";
        }

        public void Rebind() {
            Keyboard.OnKeyChanged += Keyboard_OnKeyChanged;
        }

        private static char MapFromKey(ConsoleKeyInfo key) {
            // First try to use KeyChar if it'sset (from PS2 keyboard driver)
            if (key.KeyChar != '\0') return key.KeyChar;

            // Fallback: manually map from ConsoleKey
            var k = key.Key;
            bool shift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
            bool caps = key.Modifiers.HasFlag(ConsoleModifiers.CapsLock);

            if (k == ConsoleKey.Space) return ' ';
            if (k >= ConsoleKey.A && k <= ConsoleKey.Z) {
                char c = (char)('a' + (k - ConsoleKey.A));
                if (shift ^ caps) {
                    if (c >= 'a' && c <= 'z') c = (char)('A' + (c - 'a'));
                }
                return c;
            }
            if (k >= ConsoleKey.D0 && k <= ConsoleKey.D9) {
                int d = k - ConsoleKey.D0;
                if (!shift) return (char)('0' + d);
                switch (d) {
                    case 0:
                        return ')';
                    case 1:
                        return '!';
                    case 2:
                        return '@';
                    case 3:
                        return '#';
                    case 4:
                        return '$';
                    case 5:
                        return '%';
                    case 6:
                        return '^';
                    case 7:
                        return '&';
                    case 8:
                        return '*';
                    case 9:
                        return '(';
                }
            }
            switch (k) {
                case ConsoleKey.OemPeriod:
                    return shift ? '>' : '.';
                case ConsoleKey.OemComma:
                    return shift ? '<' : ',';
                case ConsoleKey.OemMinus:
                    return shift ? '_' : '-';
                case ConsoleKey.OemPlus:
                    return shift ? '+' : '=';
                case ConsoleKey.Oem1:
                    return shift ? ':' : ';';
                case ConsoleKey.Oem2:
                    return shift ? '?' : '/';
                case ConsoleKey.Oem3:
                    return shift ? '~' : '`';
                case ConsoleKey.Oem4:
                    return shift ? '{' : '[';
                case ConsoleKey.Oem5:
                    return shift ? '|' : '\\';
                case ConsoleKey.Oem6:
                    return shift ? '}' : ']';
                case ConsoleKey.Oem7:
                    return shift ? '"' : '\'';
            }
            return '\0';
        }

        private void Keyboard_OnKeyChanged(object sender, ConsoleKeyInfo key) {
            // Block keyboard input if workspace switcher is visible
            if (Desktop.Taskbar != null && Desktop.Taskbar.IsWorkspaceSwitcherVisible) {
                // Allow Escape key to pass through to close the switcher
                if (key.Key == ConsoleKey.Escape && key.KeyState == ConsoleKeyState.Pressed) {
                    Desktop.Taskbar.CloseWorkspaceSwitcher();
                }
                return;
            }

            if (!Visible) {
                return;
            }
            if (_viMode) {
                HandleViKey(key);
                return;
            }
            if (key.KeyState != ConsoleKeyState.Pressed) {
                _keyDown = false;
                _lastScan = 0;
                return;
            }

            // Fix: improved debouncing - only block repeat if it's the same scan code AND same key state
            // This allows special characters to work properly
            if (_keyDown && key.ScanCode == _lastScan) return;
            _keyDown = true;
            _lastScan = (byte)key.ScanCode;

            if (key.Key == ConsoleKey.Backspace) {
                if (Cmd.Length > 0) {
                    Cmd = Cmd.Substring(0, Cmd.Length - 1); // remove from screen end
                    if (Data.Length > 0) Data = Data.Substring(0, Data.Length - 1); // remove from output end
                }
                return;
            }
            if (key.Key == ConsoleKey.Enter) {
                AppendRaw("\n");
                HandleCommand(TrimSpaces(Cmd));
                Cmd = string.Empty;

                // Don't write prompt immediately if ping is in progress
                // (OnInput will write it when ping completes)
                if (!_pingInProgress) {
                    WritePrompt();
                }
                return;
            }
            char ch = MapFromKey(key);
            if (ch != '\0') {
                Cmd += ch;
                AppendRaw(ch.ToString());
            }
        }

        private static string TrimSpaces(string s) {
            if (s == null) return string.Empty;
            int a = 0, b = s.Length - 1;
            while (a <= b && s[a] == ' ') a++;
            while (b >= a && s[b] == ' ') b--;
            if (b < a) return string.Empty;
            return s.Substring(a, b - a + 1);
        }
        private static bool StartsWithFast(string s, string pref) {
            int l = pref.Length;
            if (s == null || s.Length < l) return false;
            for (int i = 0; i < l; i++) {
                if (s[i] != pref[i]) return false;
            }
            return true;
        }
        private static string JoinParts(String[] arr, char sep, int start) {
            if (start >= arr.Length) return string.Empty;
            string r = arr[start];
            for (int i = start + 1; i < arr.Length; i++) {
                r += sep;
                r += arr[i];
            }
            return r;
        }

        private static bool TryParseIp(String s, out NETv4.IPAddress ip) {
            ip =
              default;
            int b1 = -1, b2 = -1, b3 = -1, b4 = -1;
            int part = 0;
            int acc = 0;
            bool any = false;
            for (int i = 0; i <= s.Length; i++) {
                char c = i < s.Length ? s[i] : '.'; // sentinel
                if (c >= '0' && c <= '9') {
                    acc = acc * 10 + (c - '0');
                    if (acc > 255) return false;
                    any = true;
                } else if (c == '.') {
                    if (!any) return false;
                    if (part == 0) b1 = acc;
                    else if (part == 1) b2 = acc;
                    else if (part == 2) b3 = acc;
                    else if (part == 3) b4 = acc;
                    else return false;
                    part++;
                    acc = 0;
                    any = false;
                } else {
                    return false;
                }
            }
            if (part != 4) return false;
            ip = new NETv4.IPAddress((byte)b1, (byte)b2, (byte)b3, (byte)b4);
            return true;
        }

        // ---- vi minimal implementation ----
        private void HandleViKey(ConsoleKeyInfo key) {
            if (!Visible) return; // don't process in background
            if (!_viMode) return;
            if (key.KeyState != ConsoleKeyState.Pressed) return;
            if (!_viInsert) { // command mode
                if (key.Key == ConsoleKey.I) {
                    _viInsert = true;
                    StatusMsg("-- INSERT --");
                    return;
                }
                if (key.Key == ConsoleKey.Escape) {
                    ExitVi();
                    return;
                }
                if (key.Key == ConsoleKey.S) {
                    SaveVi();
                    return;
                }
                if (key.Key == ConsoleKey.Q) {
                    ExitVi();
                    return;
                }
                if (key.Key == ConsoleKey.Left) {
                    if (_viCursor > 0) _viCursor--;
                    RedrawVi();
                    return;
                }
                if (key.Key == ConsoleKey.Right) {
                    if (_viCursor < _textBufferForVi.Length) _viCursor++;
                    RedrawVi();
                    return;
                }
            } else { // insert mode
                if (key.Key == ConsoleKey.Escape) {
                    _viInsert = false;
                    StatusMsg("");
                    RedrawVi();
                    return;
                }
                if (key.Key == ConsoleKey.Backspace) {
                    if (_viCursor > 0) {
                        _textBufferForVi = _textBufferForVi.Substring(0, _viCursor - 1) + _textBufferForVi.Substring(_viCursor);
                        _viCursor--;
                        RedrawVi();
                    }
                    return;
                }
                if (key.Key == ConsoleKey.Enter) {
                    InsertVi('\n');
                    return;
                }
                char ch = MapFromKey(key);
                if (ch != '\0') {
                    InsertVi(ch);
                }
            }
        }
        private void InsertVi(char ch) {
            _textBufferForVi = _textBufferForVi.Substring(0, _viCursor) + ch + _textBufferForVi.Substring(_viCursor);
            _viCursor++;
            RedrawVi();
        }
        private void RedrawVi() {
            Data = string.Empty;
            AppendRaw("-- vi -- " + (_viInsert ? "INSERT" : "CMD") + " " + (_viPath ?? "(new)") + "\n");
            AppendRaw(_textBufferForVi);
        }
        private void SaveVi() {
            if (_viPath == null) return;
            byte[] d = new byte[_textBufferForVi.Length];
            for (int i = 0; i < d.Length; i++) d[i] = (byte)_textBufferForVi[i];
            FS.File.WriteAllBytes(_viPath, d);
            d.Dispose();
            StatusMsg("Written");
        }
        private void StatusMsg(String msg) {
            /* optional */
        }
        private void ExitVi() {
            _viMode = false;
            _viInsert = false;
            _viPath = null;
            _textBufferForVi = string.Empty;
            _viCursor = 0;
            WritePrompt();
        }
        private void EnterVi(string path) {
            _viMode = true;
            _viInsert = false;
            _viPath = Posix.NormalizePath(_cwd, path);
            byte[] d = FS.File.ReadAllBytes(_viPath);
            if (d != null) {
                char[] c = new char[d.Length];
                for (int i = 0; i < d.Length; i++) {
                    byte b = d[i];
                    c[i] = b >= 32 && b < 127 ? (char)b : (b == 10 ? '\n' : '.');
                }
                _textBufferForVi = new string(c);
                d.Dispose();
            } else {
                _textBufferForVi = string.Empty;
            }
            _viCursor = 0;
            RedrawVi();
        }

        // helpers
        private string ToAbs(string rel) {
            return Posix.NormalizePath(string.IsNullOrEmpty(_cwd) ? "/" : "/" + _cwd, rel);
        }
        private bool ResolveFileToken(string token, out string abs, out string err) {
            abs = ToAbs(token);
            err = null;
            var data = FS.File.ReadAllBytes(abs);
            if (data != null) {
                data.Dispose();
                return true;
            }
            if (token.IndexOf('.') < 0) {
                string cwdAbs = string.IsNullOrEmpty(_cwd) ? "/" : "/" + _cwd;
                if (Posix.TryFuzzyResolve(cwdAbs, token, out
                    var r, out err)) {
                    abs = r;
                    return true;
                }
            }
            return false;
        }

        private static string[] SplitArgs(string s) {
            if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            char[] buf = new char[s.Length];
            int bLen = 0;
            bool inQuotes = false;
            char qChar = '"';
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                if (c == '"' || c == '\'') {
                    if (!inQuotes) {
                        inQuotes = true;
                        qChar = c;
                        continue;
                    }
                    if (c == qChar) {
                        inQuotes = false;
                        continue;
                    }
                }
                if (!inQuotes && c == ' ') {
                    if (bLen > 0) {
                        parts.Add(new string(buf, 0, bLen));
                        bLen = 0;
                    }
                    continue;
                }
                if (bLen < buf.Length) buf[bLen++] = c;
            }
            if (bLen > 0) parts.Add(new string(buf, 0, bLen));
            buf.Dispose();
            return parts.ToArray();
        }

        private void HandleCommand(string cmdLine) {
            if (string.IsNullOrEmpty(cmdLine)) return;
            string[] parts = SplitArgs(cmdLine);
            if (parts.Length == 0) return;
            string cmd = parts[0];
            switch (cmd) {
                case "help":
                    WriteLine("Commands: help, pwd, ls, ll, cd, cd .., clear, exit, cat, echo, notepad <file>, vi <file>, launchscript <file.txt>, gxminfo <file.gxm>, setbg <image>, apps, launch <app>, netinit, ipconfig [/release | /renew], ifconfig, arp, dns <host>, ping <hostOrIp>, authurl <http>, authlogin <u> <p>, authregister <u> <p>, authtoken, logout, shutdown, reboot, osk, workspaces");
                    WriteLine("");
                    WriteLine("VFS commands: vfslist, vfsuse <mnt>, vfsmount <img> <mnt> <fstype>, vfsumount <mnt>, vfssync, vfsinfo <mnt>, vfsdf, vfscreateconf");
                    break;
                case "exit": {
                        this.Visible = false;
                        return;
                    }
                case "clear": {
                        Data = string.Empty;
                        WritePrompt();
                        return;
                    }
                case "pwd": {
                        string p = string.IsNullOrEmpty(_cwd) ? "/" : "/" + _cwd;
                        WriteLine(p);
                        return;
                    }
                case "setbg": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: setbg <image>");
                            return;
                        }
                        string token = parts[1];
                        string path;

                        // Try exact match first
                        if (!ResolveFileToken(token, out path, out
                            var ferr)) {
                            // If exact match fails and no extension, try fuzzy match
                            if (token.IndexOf('.') < 0) {
                                // Fuzzy match for extensionless files
                                string cwdAbs = string.IsNullOrEmpty(_cwd) ? "/" : "/" + _cwd;
                                var list = FS.File.GetFiles(string.IsNullOrEmpty(_cwd) ? "" : _cwd);
                                if (list != null) {
                                    string match = null;
                                    int matches = 0;
                                    for (int i = 0; i < list.Count; i++) {
                                        var fi = list[i];
                                        if (fi.Attribute != guideXOS.FS.FileAttribute.Directory) {
                                            string nm = fi.Name;
                                            // Check if starts with token and has extension
                                            if (nm.Length > token.Length + 1 && StartsWithFast(nm, token) && nm[token.Length] == '.') {
                                                // Check for image extensions
                                                int len = nm.Length;
                                                bool isPng = len >= 4 && nm[len - 1] == 'g' && nm[len - 2] == 'n' && nm[len - 3] == 'p' && nm[len - 4] == '.';
                                                bool isJpg = len >= 4 && nm[len - 1] == 'g' && nm[len - 2] == 'p' && nm[len - 3] == 'j' && nm[len - 4] == '.';
                                                bool isBmp = len >= 4 && nm[len - 1] == 'p' && nm[len - 2] == 'm' && nm[len - 3] == 'b' && nm[len - 4] == '.';
                                                if (isPng || isJpg || isBmp) {
                                                    matches++;
                                                    match = nm;
                                                }
                                            }
                                        }
                                        fi.Dispose();
                                    }
                                    list.Dispose();

                                    if (matches == 0) {
                                        WriteLine("setbg: no image file found matching '" + token + "'");
                                        return;
                                    } else if (matches > 1) {
                                        WriteLine("setbg: ambiguous - multiple images match '" + token + "'");
                                        return;
                                    }
                                    // Exactly one match - use it
                                    path = Posix.NormalizePath(_cwd, match);
                                } else {
                                    WriteLine("setbg: cannot access directory");
                                    return;
                                }
                            } else {
                                // Has extension but file not found
                                if (ferr != null) WriteLine(ferr);
                                else WriteLine("setbg: file not found: " + token);
                                return;
                            }
                        }

                        // Try to load and set the wallpaper
                        try {
                            byte[] data = FS.File.ReadAllBytes(path);
                            if (data == null) {
                                WriteLine("setbg: unable to read file: " + path);
                                return;
                            }

                            var img = new guideXOS.Misc.PNG(data);

                            if (Program.Wallpaper != null) Program.Wallpaper.Dispose();
                            Program.Wallpaper = img.ResizeImage(Framebuffer.Width, Framebuffer.Height);
                            img.Dispose();

                            WriteLine("Desktop background changed to: " + path);
                        } catch {
                            WriteLine("setbg: failed to load image (not a valid PNG?)");
                        }
                        return;
                    }
                case "ls": {
                        string target = parts.Length > 1 ? Posix.NormalizePath(_cwd, parts[1]) : (string.IsNullOrEmpty(_cwd) ? "/" : "/" + _cwd);
                        if (!Posix.DirectoryExists(target)) {
                            WriteLine("ls: cannot access: " + (parts.Length > 1 ? parts[1] : target));
                            return;
                        }
                        var names = Posix.List(target);
                        if (names.Length == 0) {
                            WriteLine("(empty)");
                            return;
                        }
                        for (int i = 0; i < names.Length; i++) {
                            WriteLine(names[i]);
                        }
                        return;
                    }
                case "ll": {
                        string target = parts.Length > 1 ? Posix.NormalizePath(_cwd, parts[1]) : (string.IsNullOrEmpty(_cwd) ? "/" : "/" + _cwd);
                        if (!Posix.DirectoryExists(target)) {
                            WriteLine("ll: cannot access: " + (parts.Length > 1 ? parts[1] : target));
                            return;
                        }
                        string rel = target == "/" ? "" : target.Substring(1);
                        var list = FS.File.GetFiles(rel);
                        if (list == null || list.Count == 0) {
                            WriteLine("(empty)");
                            return;
                        }
                        for (int i = 0; i < list.Count; i++) {
                            var fi = list[i];
                            char type = fi.Attribute == guideXOS.FS.FileAttribute.Directory ? 'd' : '-';
                            uint size = 0;
                            WriteLine(type + " " + PadRight(fi.Name, 24) + " " + size.ToString());
                        }
                        return;
                    }
                case "cd"
                when parts.Length > 1 && (parts[1] == "back" || parts[1] == ".."):
                case "goback":
                case "cdback": {
                        if (string.IsNullOrEmpty(_cwd)) {
                            WriteLine("Already at root");
                            return;
                        }
                        string cur = "/" + _cwd;
                        if (cur.EndsWith("/")) cur = cur.Substring(0, cur.Length - 1);
                        int last = cur.LastIndexOf('/');
                        if (last <= 0) {
                            _cwd = "";
                        } else {
                            _cwd = cur.Substring(1, last);
                            if (_cwd.Length > 0) _cwd += "/";
                        }
                        Desktop.Dir = _cwd;
                        Desktop.InvalidateDirCache();
                        UpdateTitle();
                        WriteLine("Moved back");
                        return;
                    }
                case "cd": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: cd <path>");
                            return;
                        }
                        string rawTarget = parts[1];
                        string target = Posix.NormalizePath(_cwd, rawTarget);
                        if (!Posix.DirectoryExists(target)) {
                            string baseDir = string.IsNullOrEmpty(_cwd) ? "/" : "/" + _cwd;
                            var names = Posix.List(baseDir);
                            string match = null;
                            int matches = 0;
                            for (int i = 0; i < names.Length; i++) {
                                if (EqualsIgnoreCase(names[i], rawTarget)) {
                                    match = names[i];
                                    matches = 1;
                                    break;
                                }
                                if (StartsWithIgnoreCase(names[i], rawTarget)) {
                                    match = names[i];
                                    matches++;
                                }
                            }
                            if (matches == 1) target = Posix.NormalizePath(_cwd, match);
                            else {
                                WriteLine("cd: no such directory: " + rawTarget);
                                return;
                            }
                        }
                        if (target == "/") {
                            _cwd = "";
                        } else {
                            if (!target.EndsWith("/")) target += "/";
                            _cwd = target.Substring(1);
                        }
                        Desktop.Dir = _cwd;
                        Desktop.HomeMode = false; // Exit HomeMode to show filesystem
                        Desktop.InvalidateDirCache();
                        UpdateTitle();
                        WriteLine("Changed directory to: " + (string.IsNullOrEmpty(_cwd) ? "/" : "/" + _cwd));
                        return;
                    }
            }
            // fall-through to remaining original commands
            switch (cmd) {
                case "cat": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: cat <file>");
                            return;
                        }
                        if (!ResolveFileToken(parts[1], out
                            var path, out
                            var ferr)) {
                            if (ferr != null) {
                                WriteLine(ferr);
                                return;
                            }
                            path = ToAbs(parts[1]);
                        }
                        byte[] data = FS.File.ReadAllBytes(path);
                        if (data == null) {
                            WriteLine("Unable to read file");
                            return;
                        }
                        int len = data.Length;
                        char[] buf = new char[len];
                        for (int i = 0; i < len; i++) {
                            byte b = data[i];
                            buf[i] = b >= 32 && b < 127 ? (char)b : (b == 10 ? '\n' : '.');
                        }
                        WriteLine(new string(buf));
                        data.Dispose();
                        return;
                    }
                case "echo": {
                        if (parts.Length < 2) {
                            WriteLine("");
                            return;
                        }
                        int gtIndex = -1;
                        for (int i = 1; i < parts.Length; i++) {
                            if (parts[i] == ">") {
                                gtIndex = i;
                                break;
                            }
                        }
                        if (gtIndex == -1) {
                            string outText = JoinParts(parts, ' ', 1);
                            WriteLine(outText);
                            return;
                        }
                        if (gtIndex + 1 >= parts.Length) {
                            WriteLine("echo: missing filename after >");
                            return;
                        }
                        string fileName = Posix.NormalizePath(_cwd, parts[gtIndex + 1]);
                        string outText2 = "";
                        for (int i = 1; i < gtIndex; i++) {
                            if (i > 1) outText2 += ' ';
                            outText2 += parts[i];
                        }
                        byte[] d = new byte[outText2.Length];
                        for (int i = 0; i < d.Length; i++) d[i] = (byte)(outText2[i] < 128 ? outText2[i] : '?');
                        FS.File.WriteAllBytes(fileName, d);
                        d.Dispose();
                        Desktop.InvalidateDirCache();
                        WriteLine("Written " + fileName);
                        return;
                    }
                case "notepad": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: notepad <file>");
                            return;
                        }
                        string fileToken = parts[1];
                        if (!ResolveFileToken(fileToken, out
                            var path, out
                            var ferr)) {
                            if (ferr != null) WriteLine(ferr);
                            else path = fileToken;
                        }
                        LaunchResult launchResult;
                        if (!Desktop.LaunchApplication(LaunchRequest.ForFile(
                                "gxos.builtin.notepad", path, null, "open",
                                null, false,
                                LaunchActivationIntent.Launch), out launchResult)) {
                            WriteLine("Unable to open file in Notepad");
                        }
                        return;
                    }
                case "vi": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: vi <file>");
                            return;
                        }
                        if (!ResolveFileToken(parts[1], out
                            var path, out
                            var ferr)) {
                            if (ferr != null) WriteLine(ferr);
                            else path = parts[1];
                        }
                        EnterVi(path);
                        return;
                    }
                case "launchscript": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: launchscript <script.txt>");
                            WriteLine("Loads and executes a GXM GUI script from a .txt file");
                            return;
                        }

                        string fileToken = parts[1];
                        if (!ResolveFileToken(fileToken, out
                            var path, out
                            var ferr)) {
                            if (ferr != null) {
                                WriteLine(ferr);
                                return;
                            }
                            // File doesn't exist, try as-is
                            path = Posix.NormalizePath(_cwd, fileToken);
                        }

                        // Load the script file
                        byte[] scriptData = FS.File.ReadAllBytes(path);
                        if (scriptData == null) {
                            WriteLine("Error: Unable to read script file: " + fileToken);
                            return;
                        }

                        // Create GXM header with GUI marker
                        int headerSize = 20; // GXM header (16) + GUI marker (4)
                        int totalSize = headerSize + scriptData.Length + 1; // +1 for null terminator
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
                        for (int i = 0; i < scriptData.Length; i++) {
                            gxmData[headerSize + i] = scriptData[i];
                        }

                        // Null terminator
                        gxmData[totalSize - 1] = 0;

                        scriptData.Dispose();

                        // Execute through the typed external backend.  The
                        // backend owns and consumes the generated buffer.
                        if (Desktop.LaunchTypedExternalGxm(gxmData, path, null,
                                out LaunchResult gxmResult)) {
                            WriteLine("Script loaded: " + fileToken);
                        } else {
                            WriteLine("Error executing script: " +
                                (gxmResult == null ? "unknown error" :
                                (gxmResult.BoundedDiagnostic ?? "unknown error")));
                        }
                        return;
                    }
                case "gxminfo": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: gxminfo <file.gxm>");
                            return;
                        }
                        string path = Posix.NormalizePath(_cwd, parts[1]);
                        byte[] buf = FS.File.ReadAllBytes(path);
                        if (buf == null) {
                            WriteLine("Unable to read file");
                            return;
                        }
                        string sig = (buf.Length >= 4) ? ((char)buf[0]).ToString() + ((char)buf[1]) + ((char)buf[2]) + ((char)buf[3]) : "(too small)";
                        WriteLine("Signature: " + sig);
                        if (buf.Length >= 16) {
                            uint ver = (uint)(buf[4] | buf[5] << 8 | buf[6] << 16 | buf[7] << 24);
                            uint entry = (uint)(buf[8] | buf[9] << 8 | buf[10] << 16 | buf[11] << 24);
                            uint size = (uint)(buf[12] | buf[13] << 8 | buf[14] << 16 | buf[15] << 24);
                            WriteLine($"Version:{ver} EntryRVA:0x{entry:X8} DeclaredSize:{size}");
                        }
                        buf.Dispose();
                        return;
                    }
                // legacy alias
                case "mueinfo": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: mueinfo <file>");
                            return;
                        }
                        string path = Posix.NormalizePath(_cwd, parts[1]);
                        byte[] buf = FS.File.ReadAllBytes(path);
                        if (buf == null) {
                            WriteLine("Unable to read file");
                            return;
                        }
                        string sig = (buf.Length >= 4) ? ((char)buf[0]).ToString() + ((char)buf[1] + ((char)buf[2]) + ((char)buf[3])) : "(too small)";
                        WriteLine("Signature: " + sig);
                        if (buf.Length >= 16) {
                            uint ver = (uint)(buf[4] | buf[5] << 8 | buf[6] << 16 | buf[7] << 24);
                            uint entry = (uint)(buf[8] | buf[9] << 8 | buf[10] << 16 | buf[11] << 24);
                            uint size = (uint)(buf[12] | buf[13] << 8 | buf[14] << 16 | buf[15] << 24);
                            WriteLine($"Version:{ver} EntryRVA:0x{entry:X8} DeclaredSize:{size}");
                        }
                        buf.Dispose();
                        return;
                    }
                case "shutdown":
                    Power.Shutdown();
                    break;
                case "reboot":
                    Power.Reboot();
                    break;
                case "cpu":
                    break;
                case "null":
                    unsafe {
                        uint* ptr = null; *ptr = 0xDEADBEEF;
                    }
                    break;
                case "netinit":
                    // Check if network is already initialized
                    unsafe {
                        if (NETv4.Sender != null) {
                            WriteLine("[NET] Network already initialized");
                            WriteLine($"[NET] IP: {NETv4.IP.P1}.{NETv4.IP.P2}.{NETv4.IP.P3}.{NETv4.IP.P4}");
                            break;
                        }
                    }

                    WriteLine("[NET] Initializing stack");
                    try {
                        NETv4.Initialize();
                    } catch {
                        WriteLine("[NET] Stack initialization failed");
                        break;
                    }

                    WriteLine("[NET] Scanning for NICs");
                    bool nicFound = false;
                    try {
                        Intel825xx.Initialize();
                        unsafe {
                            if (NETv4.Sender != null) {
                                WriteLine("[NET] Intel825xx found");
                                nicFound = true;
                            }
                        }
                    } catch { }

                    try {
                        RTL8111.Initialize();
                        unsafe {
                            if (NETv4.Sender != null && !nicFound) {
                                WriteLine("[NET] RTL8111 found");
                                nicFound = true;
                            }
                        }
                    } catch { }

                    if (!nicFound) {
                        WriteLine("[NET] No supported NIC found");
                        break;
                    }

                    WriteLine("[NET] Attempting DHCP (this may take a few seconds)...");
                    try {
                        bool dhcp = NETv4.DHCPDiscover();
                        if (dhcp) {
                            WriteLine("[NET] DHCP successful");
                            WriteLine($"[NET] IP: {NETv4.IP.P1}.{NETv4.IP.P2}.{NETv4.IP.P3}.{NETv4.IP.P4}");
                        } else {
                            WriteLine("[NET] DHCP failed");
                        }
                    } catch {
                        WriteLine("[NET] DHCP error");
                    }
                    break;
                case "ifconfig":
                    WriteLine($"IP: {NETv4.IP.P1}.{NETv4.IP.P2}.{NETv4.IP.P3}.{NETv4.IP.P4}");
                    WriteLine($"Mask: {NETv4.Mask.P1}.{NETv4.Mask.P2}.{NETv4.Mask.P3}.{NETv4.Mask.P4}");
                    WriteLine($"Gateway: {NETv4.GatewayIP.P1}.{NETv4.GatewayIP.P2}.{NETv4.GatewayIP.P3}.{NETv4.GatewayIP.P4}");
                    WriteLine($"MAC: {NETv4.MAC.P1:x2}:{NETv4.MAC.P2:x2}:{NETv4.MAC.P3:x2}:{NETv4.MAC.P4:x2}:{NETv4.MAC.P5:x2}:{NETv4.MAC.P6:x2}");
                    break;
                case "ipconfig":
                    // Check for /release or /renew parameters
                    if (parts.Length > 1) {
                        string param = parts[1];
                        // Manual case-insensitive comparison
                        bool isRelease = param.Length == 8 &&
                            (param[0] == '/' || param[0] == '-') &&
                            (param[1] == 'r' || param[1] == 'R') &&
                            (param[2] == 'e' || param[2] == 'E') &&
                            (param[3] == 'l' || param[3] == 'L') &&
                            (param[4] == 'e' || param[4] == 'E') &&
                            (param[5] == 'a' || param[5] == 'A') &&
                            (param[6] == 's' || param[6] == 'S') &&
                            (param[7] == 'e' || param[7] == 'E');

                        bool isRenew = param.Length == 6 &&
                            (param[0] == '/' || param[0] == '-') &&
                            (param[1] == 'r' || param[1] == 'R') &&
                            (param[2] == 'e' || param[2] == 'E') &&
                            (param[3] == 'n' || param[3] == 'N') &&
                            (param[4] == 'e' || param[4] == 'E') &&
                            (param[5] == 'w' || param[5] == 'W');

                        if (isRelease) {
                            // Release DHCP lease - clear IP configuration
                            WriteLine("Releasing IP configuration...");
                            NETv4.IP = default;
                            NETv4.Mask = default;
                            NETv4.GatewayIP = default;
                            WriteLine("IP address has been released");
                            break;
                        } else if (isRenew) {
                            // Renew DHCP lease - redirect to netinit
                            unsafe {
                                if (NETv4.Sender == null) {
                                    WriteLine("Network not initialized. Run 'netinit' first.");
                                    break;
                                }
                            }

                            WriteLine("WARNING: DHCP renewal may take several seconds and will freeze the OS.");
                            WriteLine("To renew your IP address, use the 'netinit' command instead.");
                            WriteLine("");
                            WriteLine("Steps to renew:");
                            WriteLine("  1. ipconfig /release   - Release current IP");
                            WriteLine("  2. netinit             - Re-acquire IP via DHCP");
                            break;
                        } else {
                            WriteLine("Unknown parameter: " + parts[1]);
                            WriteLine("Usage: ipconfig [/release | /renew]");
                            break;
                        }
                    }

                    // No parameters - show current configuration
                    WriteLine($"IP: {NETv4.IP.P1}.{NETv4.IP.P2}.{NETv4.IP.P3}.{NETv4.IP.P4}");
                    WriteLine($"Mask: {NETv4.Mask.P1}.{NETv4.Mask.P2}.{NETv4.Mask.P3}.{NETv4.Mask.P4}");
                    WriteLine($"Gateway: {NETv4.GatewayIP.P1}.{NETv4.GatewayIP.P2}.{NETv4.GatewayIP.P3}.{NETv4.GatewayIP.P4}");
                    WriteLine($"MAC: {NETv4.MAC.P1:x2}:{NETv4.MAC.P2:x2}:{NETv4.MAC.P3:x2}:{NETv4.MAC.P4:x2}:{NETv4.MAC.P5:x2}:{NETv4.MAC.P6:x2}");
                    break;
                case "arp":
                    if (NETv4.ARPTable == null) {
                        WriteLine("ARP table not initialized");
                        break;
                    }
                    for (int i = 0; i < NETv4.ARPTable.Count; i++) {
                        var e = NETv4.ARPTable[i];
                        WriteLine($"{e.IP.P1}.{e.IP.P2}.{e.IP.P3}.{e.IP.P4} -> {e.MAC.P1:x2}:{e.MAC.P2:x2}:{e.MAC.P3:x2}:{e.MAC.P4:x2}:{e.MAC.P5:x2}:{e.MAC.P6:x2}");
                    }
                    break;
                case "dns":
                    if (parts.Length < 2) {
                        WriteLine("Usage: dns <host>");
                        break;
                    }

                    unsafe {
                        if (NETv4.Sender == null) {
                            WriteLine("Network not initialized. Run 'netinit' first.");
                            break;
                        }
                    }

                    try {
                        var ip = NETv4.DNSQuery(parts[1]);
                        if (ip.P1 == 0 && ip.P2 == 0 && ip.P3 == 0 && ip.P4 == 0) {
                            WriteLine("DNS failed");
                        } else {
                            WriteLine($"Resolved: {ip.P1}.{ip.P2}.{ip.P3}.{ip.P4}");
                        }
                    } catch {
                        WriteLine("DNS error");
                    }
                    break;
                case "ping":
                    if (parts.Length < 2) {
                        WriteLine("Usage: ping <ip_address>");
                        WriteLine("Example: ping 8.8.8.8");
                        WriteLine("Note: Use 'dns <hostname>' to resolve hostnames first");
                        break;
                    }

                    // Check if network is initialized
                    unsafe {
                        if (NETv4.Sender == null) {
                            WriteLine("Network not initialized. Run 'netinit' first.");
                            break;
                        }
                    }

                    // Check if ping already in progress
                    if (_pingInProgress) {
                        WriteLine("Ping already in progress, please wait...");
                        break;
                    }

                    // ONLY accept IP addresses to avoid DNS blocking the GUI thread
                    NETv4.IPAddress dip;
                    if (!TryParseIp(parts[1], out dip)) {
                        WriteLine("Error: Please provide an IP address (not a hostname)");
                        WriteLine("Example: ping 8.8.8.8");
                        WriteLine("");
                        WriteLine("To ping a hostname:");
                        WriteLine("  1. Run: dns <hostname>   (e.g., dns google.com)");
                        WriteLine("  2. Then: ping <ip>       (use the resolved IP)");
                        break;
                    }

                    WriteLine($"Pinging {dip.P1}.{dip.P2}.{dip.P3}.{dip.P4} with {NETv4.ICMPPingBytes} bytes of data:");

                    // Reset ICMP state
                    NETv4.IsICMPRespond = false;
                    NETv4.ICMPReplyTTL = 0;
                    NETv4.ICMPReplyBytes = 0;

                    try {
                        NETv4.ICMPPing(dip);

                        // Set ping state - response will be checked in OnInput()
                        _pingInProgress = true;
                        _pingStartTime = Timer.Ticks;
                        _pingTarget = dip;
                        _pingHostname = parts[1];

                        // DON'T wait here - return immediately to keep UI responsive
                    } catch {
                        WriteLine("Ping failed (network error)");
                    }
                    break;
                case "authurl":
                    if (parts.Length < 2) {
                        WriteLine("Usage: authurl <http://host:port>");
                        break;
                    }
                    Session.ServiceBaseUrl = parts[1];
                    WriteLine("ServiceBaseUrl set to: " + Session.ServiceBaseUrl);
                    break;
                case "authlogin":
                    if (parts.Length < 3) {
                        WriteLine("Usage: authlogin <username> <password>");
                        break;
                    } {
                        string token;
                        string msg;
                        bool ok = AuthClient.TryLogin(parts[1], parts[2], out token, out msg);
                        if (ok) {
                            Session.LoginToken = token;
                            WriteLine("Login OK. Token=" + token);
                        } else {
                            WriteLine("Login failed: " + (msg ?? ""));
                        }
                    }
                    break;
                case "fontdemo":
                    WindowManager.EnqueueTTFFontDemo(100, 100, 700, 500);
                    break;
                case "authregister":
                    if (parts.Length < 3) {
                        WriteLine("Usage: authregister <username> <password>");
                        break;
                    } {
                        string msg;
                        bool ok = AuthClient.TryRegister(parts[1], parts[2], out msg);
                        if (ok) WriteLine("Register OK. Now run authlogin.");
                        else WriteLine("Register failed: " + (msg ?? ""));
                    }
                    break;
                case "authtoken":
                    WriteLine("Token: " + (Session.LoginToken ?? ""));
                    break;
                case "logout":
                    Session.LoginToken = string.Empty;
                    WriteLine("Logged out.");
                    break;
                case "fwmode": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: fwmode <normal|blockall|disabled|autolearn>");
                            break;
                        }
                        string m = parts[1];
                        if (m == "normal") Firewall.Mode = guideXOS.OS.FirewallMode.Normal;
                        else if (m == "blockall") Firewall.Mode = guideXOS.OS.FirewallMode.BlockAll;
                        else if (m == "disabled") Firewall.Mode = guideXOS.OS.FirewallMode.Disabled;
                        else if (m == "autolearn") Firewall.Mode = guideXOS.OS.FirewallMode.Autolearn;
                        WriteLine("Firewall mode: " + Firewall.Mode.ToString());
                        break;
                    }
                case "fwlist": {
                        var arr = guideXOS.OS.Firewall.Exceptions;
                        for (int ii = 0; ii < arr.Length; ii++) WriteLine(arr[ii]);
                        break;
                    }
                case "fwadd": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: fwadd <program>");
                            break;
                        }
                        guideXOS.OS.Firewall.AddException(parts[1]);
                        WriteLine("Added exception: " + parts[1]);
                        break;
                    }
                case "fwui": {
                        if (guideXOS.OS.Firewall.Window != null) {
                            WindowManager.MoveToEnd(guideXOS.OS.Firewall.Window);
                            guideXOS.OS.Firewall.Window.Visible = true;
                        }
                        break;
                    }
                case "workspaces": {
                        // Show the workspace switcher overlay
                        if (Desktop.Taskbar != null) {
                            Desktop.Taskbar.ShowWorkspaceSwitcher();
                            WriteLine("Workspace switcher opened");
                        } else {
                            WriteLine("Taskbar not available");
                        }
                        break;
                    }
                case "apps":
                case "listapps": {
                        if (Desktop.Apps == null) {
                            WriteLine("App system not initialized");
                            break;
                        }
                        WriteLine("Available Applications:");
                        WriteLine("----------------------");
                        for (int i = 0; i < Desktop.Apps.Length; i++) {
                            WriteLine("  " + Desktop.Apps.Name(i));
                        }
                        WriteLine("");
                        WriteLine("Usage: launch <appname>");
                        break;
                    }
                case "launch":
                case "run":
                case "start": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: launch <appname>");
                            WriteLine("Use 'apps' to see available applications");
                            break;
                        }
                        if (Desktop.Apps == null) {
                            WriteLine("App system not initialized");
                            break;
                        }

                        string appName = parts[1];
                        bool found = false;
                        string match = null;

                        // Try exact match first
                        for (int i = 0; i < Desktop.Apps.Length; i++) {
                            if (EqualsIgnoreCase(Desktop.Apps.Name(i), appName)) {
                                appName = Desktop.Apps.Name(i);
                                found = true;
                                break;
                            }
                        }

                        // Try partial match if not found
                        if (!found) {
                            int matches = 0;
                            for (int i = 0; i < Desktop.Apps.Length; i++) {
                                if (StartsWithIgnoreCase(Desktop.Apps.Name(i), appName)) {
                                    match = Desktop.Apps.Name(i);
                                    matches++;
                                }
                            }

                            if (matches == 1) {
                                appName = match;
                                found = true;
                            } else if (matches > 1) {
                                WriteLine("Ambiguous: multiple apps match '" + appName + "'");
                                for (int i = 0; i < Desktop.Apps.Length; i++) {
                                    if (StartsWithIgnoreCase(Desktop.Apps.Name(i), appName)) {
                                        WriteLine("  " + Desktop.Apps.Name(i));
                                    }
                                }
                                break;
                            }
                        }

                        if (found) {
                            WriteLine("Launching " + appName + "...");
                            Desktop.LaunchApplication(appName);
                        } else {
                            WriteLine("App not found: " + parts[1]);
                            WriteLine("Use 'apps' to see available applications");
                        }
                        break;
                    }
                // ?? Virtual Filesystem management ?????????????????????????????????????
                case "vfslist": {
                        var mounts = guideXOS.OS.VirtualDiskAutoMount.GetMountedDisks();
                        if (mounts.Count == 0) {
                            WriteLine("No virtual disks mounted.");
                            WriteLine("");
                            WriteLine("Troubleshooting:");
                            WriteLine("  1. Check if /disks/ exists: ls /");
                            WriteLine("  2. Check for .img files: ls /disks/");
                            WriteLine("  3. The .img files must be ON the ramdisk, not on your host OS");
                            WriteLine("  4. Use 'vfsmount <path> <mnt> <fstype>' to manually mount if files exist");
                            WriteLine("");
                            WriteLine("See VFS_SETUP_TROUBLESHOOTING.md for detailed setup instructions.");
                            break;
                        }
                        WriteLine(PadRight("MOUNT", 18) + PadRight("FSTYPE", 8) + PadRight("SIZE", 12) + "IMAGE");
                        WriteLine("----------------------------------------------------------------");
                        for (int _i = 0; _i < mounts.Count; _i++) {
                            string _mp = mounts[_i];
                            string _img, _fs;
                            ulong _sz;
                            if (guideXOS.OS.VirtualDiskAutoMount.GetMountInfo(_mp, out _img, out _fs, out _sz)) {
                                WriteLine(PadRight(_mp, 18) + PadRight(_fs, 8) + PadRight(FmtBytes(_sz), 12) + _img);
                            }
                        }
                        break;
                    }
                case "vfsuse": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: vfsuse <mountpoint>");
                            WriteLine("  e.g. vfsuse /mnt/fat32");
                            WriteLine("  Use 'vfslist' to see mounted disks.");
                            break;
                        }
                        string _mp = parts[1];
                        if (guideXOS.OS.VirtualDiskAutoMount.SwitchToVirtualDisk(_mp)) {
                            WriteLine("Active filesystem switched to " + _mp);
                            WriteLine("All File.* calls now operate on this disk.");
                        } else {
                            WriteLine("vfsuse: not mounted: " + _mp);
                            WriteLine("Use 'vfslist' to see available mount points.");
                        }
                        break;
                    }
                case "vfsmount": {
                        if (parts.Length < 4) {
                            WriteLine("Usage: vfsmount <image_path> <mountpoint> <fstype>");
                            WriteLine("  fstype : FAT32 | EXT4");
                            WriteLine("  e.g.   vfsmount /disks/my.img /mnt/data FAT32");
                            break;
                        }
                        string _img  = parts[1];
                        string _mp   = parts[2];
                        string _fs   = parts[3];
                        if (guideXOS.OS.VirtualDiskAutoMount.MountVirtualDisk(_img, _mp, _fs)) {
                            WriteLine("Mounted " + _img + " at " + _mp + " as " + _fs);
                        } else {
                            WriteLine("vfsmount: failed to mount " + _img);
                            WriteLine("Check the path exists and the fstype is FAT32 or EXT4.");
                        }
                        break;
                    }
                case "vfsumount": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: vfsumount <mountpoint>");
                            WriteLine("  e.g. vfsumount /mnt/fat32");
                            break;
                        }
                        string _mp = parts[1];
                        if (guideXOS.OS.VirtualDiskAutoMount.UnmountVirtualDisk(_mp)) {
                            WriteLine("Unmounted " + _mp + " (changes synced to image).");
                        } else {
                            WriteLine("vfsumount: not mounted: " + _mp);
                        }
                        break;
                    }
                case "vfssync": {
                        var _mounts = guideXOS.OS.VirtualDiskAutoMount.GetMountedDisks();
                        if (_mounts.Count == 0) {
                            WriteLine("No virtual disks to sync.");
                            break;
                        }
                        guideXOS.OS.VirtualDiskAutoMount.SyncAll();
                        WriteLine("Synced " + _mounts.Count.ToString() + " virtual disk(s) to their image files.");
                        break;
                    }
                case "vfsinfo": {
                        if (parts.Length < 2) {
                            WriteLine("Usage: vfsinfo <mountpoint>");
                            WriteLine("  e.g. vfsinfo /mnt/fat32");
                            break;
                        }
                        string _mp = parts[1];
                        string _img, _fs;
                        ulong _sz;
                        if (!guideXOS.OS.VirtualDiskAutoMount.GetMountInfo(_mp, out _img, out _fs, out _sz)) {
                            WriteLine("vfsinfo: not mounted: " + _mp);
                            break;
                        }
                        ulong _sectors = _sz / 512;
                        bool _active = (FS.File.Instance != null &&
                                        FS.Disk.Instance is Kernel.Drivers.FileDisk);
                        WriteLine("Mount point : " + _mp);
                        WriteLine("Image file  : " + _img);
                        WriteLine("Filesystem  : " + _fs);
                        WriteLine("Size        : " + FmtBytes(_sz) + " (" + _sz.ToString() + " bytes)");
                        WriteLine("Sectors     : " + _sectors.ToString() + "  (512 B/sector)");
                        WriteLine("Active      : " + (_active ? "yes" : "no"));
                        break;
                    }
                case "vfsdf": {
                        // Walk root and accumulate file sizes as a rough usage figure
                        try {
                            var _list = FS.File.GetFiles("");
                            if (_list == null || _list.Count == 0) {
                                WriteLine("(empty root or filesystem not active)");
                                break;
                            }
                            ulong _total = 0;
                            int _files = 0, _dirs = 0;
                            for (int _i = 0; _i < _list.Count; _i++) {
                                var _fi = _list[_i];
                                if ((_fi.Attribute & FS.FileAttribute.Directory) != 0) {
                                    _dirs++;
                                } else {
                                    _total += _fi.Param1; // Param1 = file size in FAT
                                    _files++;
                                }
                            }
                            WriteLine("Root entries : " + (_files + _dirs).ToString());
                            WriteLine("  Files      : " + _files.ToString());
                            WriteLine("  Dirs       : " + _dirs.ToString());
                            WriteLine("Size (root)  : " + FmtBytes(_total));
                            // active virtual disk capacity
                            if (FS.Disk.Instance is Kernel.Drivers.FileDisk _fd) {
                                WriteLine("Disk size    : " + FmtBytes(_fd.Size));
                            }
                        } catch {
                            WriteLine("vfsdf: error reading filesystem.");
                        }
                        break;
                    }
                case "vfscreateconf": {
                        guideXOS.OS.VirtualDiskAutoMount.CreateDefaultConfig();
                        WriteLine("Created /etc/guidexos/automount.conf");
                        WriteLine("Edit with: vi /etc/guidexos/automount.conf");
                        WriteLine("Format per line:  <image>:<mountpoint>:<fstype>");
                        break;
                    }
                // ?? end VFS ???????????????????????????????????????????????????????????
                default:
                    WriteLine("No such command: \"" + cmdLine + "\"");
                    break;
            }
        }

        private static string FmtBytes(ulong bytes) {
            if (bytes < 1024) return bytes.ToString() + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024).ToString() + " KB";
            if (bytes < 1024UL * 1024 * 1024) return (bytes / (1024 * 1024)).ToString() + " MB";
            return (bytes / (1024UL * 1024 * 1024)).ToString() + " GB";
        }

        private static bool EqualsIgnoreCase(string a, string b) {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) {
                char ca = a[i];
                char cb = b[i];
                if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32);
                if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32);
                if (ca != cb) return false;
            }
            return true;
        }
        private static bool StartsWithIgnoreCase(string a, string b) {
            if (a == null || b == null) return false;
            if (b.Length > a.Length) return false;
            for (int i = 0; i < b.Length; i++) {
                char ca = a[i];
                char cb = b[i];
                if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32);
                if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32);
                if (ca != cb) return false;
            }
            return true;
        }

        private static string PadRight(string s, int w) {
            if (s == null) s = "";
            int len = s.Length;
            if (len >= w) return s;
            char[] arr = new char[w];
            int i = 0;
            for (; i < len; i++) arr[i] = s[i];
            for (; i < w; i++) arr[i] = ' ';
            string r = new string(arr, 0, w);
            arr.Dispose();
            return r;
        }

        public override void OnInput() {
            base.OnInput();

            // ALWAYS check ping status, even if window not focused
            // This ensures ping completes even if user clicks away
            if (_pingInProgress) {
                ulong now = Timer.Ticks;

                if (NETv4.IsICMPRespond) {
                    // Got response!
                    ulong elapsed = now - _pingStartTime;
                    WriteLine($"Reply from {_pingTarget.P1}.{_pingTarget.P2}.{_pingTarget.P3}.{_pingTarget.P4}: bytes={NETv4.ICMPReplyBytes} ttl={NETv4.ICMPReplyTTL} time={elapsed}ms");
                    _pingInProgress = false;
                    WritePrompt();
                } else if ((long)(now - _pingStartTime) > _pingTimeout) {
                    // Timeout
                    WriteLine("Request timed out.");
                    _pingInProgress = false;
                    WritePrompt();
                }
            }
        }

        public override void OnDraw() {
            base.OnDraw();
            DrawString(X, Y, Data, Height, Width);

            // Draw resize grip on top of console content so it's visible
            if (IsResizable && UISettings.EnableResizeGrip) {
                int gx = X + Width - 16;
                int gy = Y + Height - 16;

                uint gripColor = UISettings.EnableTransparentWindows ? 0x332F2F2Fu : 0xFF2F2F2Fu;
                Framebuffer.Graphics.FillRectangle(gx, gy, 16, 16, gripColor);

                // three little diagonal lines
                int inset = 4;
                uint lc = 0xFF777777;
                for (int i = 0; i < 3; i++) {
                    int ox = gx + 16 - inset - (i * 4);
                    int oy = gy + 16 - inset;
                    Framebuffer.Graphics.DrawLine(ox - 6, oy, ox, oy - 6, lc);
                }

                int gripCornerRadius = UISettings.EnableRoundedCorners ? 2 : 0;
                UIPrimitives.DrawRoundedRect(gx, gy, 16, 16, 0xFF444444, 1, gripCornerRadius);
            }
        }

        public void DrawString(int X, int Y, string Str, int HeightLimit = -1, int LineLimit = -1) {
            int wpx = 0, hpx = 0;
            for (int i = 0; i < Str.Length; i++) {
                char ch = Str[i];
                if (ch == '\n') {
                    wpx = 0;
                    hpx += 16;
                    goto handle_wrap;
                }
                // Skip control characters (except newline which is handled above)
                // Only draw printable ASCII characters (32-126) plus extended ASCII
                if (ch >= 32 && ch < 127) {
                    ASC16.DrawChar(ch, X + wpx, Y + hpx, 0xFFFFFFFF);
                    wpx += 8;
                }
                // For characters outside printable range, just skip them (don't draw or advance)

                if (LineLimit != -1 && wpx + 8 > LineLimit) {
                    wpx = 0;
                    hpx += 16;
                }
            handle_wrap:
                if (HeightLimit != -1 && hpx >= HeightLimit) {
                    Framebuffer.Graphics.Copy(X, Y, X, Y + 16, LineLimit, HeightLimit - 16);
                    Framebuffer.Graphics.FillRectangle(X, Y + HeightLimit - 16, LineLimit, 16, 0xFF222222);
                    hpx -= 16;
                }
            }
        }

        private void AppendRaw(string s) {
            Data += s;
        }
        private void Console_OnWrite(char chr) {
            AppendRaw(chr.ToString());
        }
        public void WriteLine(string line) {
            AppendRaw(line + "\n");
        }
    }
}
