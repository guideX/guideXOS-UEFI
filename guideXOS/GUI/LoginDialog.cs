using guideXOS.Kernel.Drivers;
using guideXOS.OS;
using guideXOS.Misc;
using guideXOS; // NETv4
using System;
using System.Windows.Forms;

namespace guideXOS.GUI {
    internal class LoginDialog : Window {
        private string _username = string.Empty;
        private string _password = string.Empty;
        private bool _pwdFocus = false;
        private bool _userFocus = true;
        private bool _clickLatch = false;
        private byte _lastScan; private bool _keyDown;
        private string _message = null;

        private const int Padding = 14;
        private const int BtnW = 110; private const int BtnH = 32; private const int Gap = 12;
        private const int PanelW = 520; private const int PanelPad = 16;

        // Loading state
        private bool _loading = false;
        private string _loadingText = string.Empty;
        private int _spinnerPhase = 0; private ulong _lastTick = 0;

        // Simple background worker plumbing (single login dialog at a time)
        private static LoginDialog _bgTarget;
        private static string _bgCmd; // "login" or "register"
        private static string _bgUser;
        private static string _bgPass;
        private static void BackgroundWorker() {
            var dlg = _bgTarget;
            var cmd = _bgCmd;
            var user = _bgUser;
            var pass = _bgPass;
            if (dlg == null) return;

            // Network guard to avoid crashing into drivers when not initialized
            bool netReady = NETv4.ARPTable != null; // heuristic

            if (cmd == "login") {
                string msg; string token;
                bool ok = false;
                if (netReady) {
                    ok = AuthClient.TryLogin(user, pass, out token, out msg);
                } else {
                    token = string.Empty; msg = "Network not initialized";
                }
                if (ok && !string.IsNullOrEmpty(token)) {
                    Session.LoginToken = token; dlg._message = "Login successful"; dlg.Visible = false;
                } else {
                    dlg._message = msg ?? "Login unsuccessful";
                }
                dlg._loading = false;
            } else if (cmd == "register") {
                string msg; bool ok = false;
                if (netReady) {
                    ok = AuthClient.TryRegister(user, pass, out msg);
                } else {
                    msg = "Network not initialized";
                }
                if (ok) {
                    dlg._password = string.Empty; dlg._pwdFocus = true; dlg._userFocus = false; dlg._message = "Registered. Please login.";
                } else {
                    dlg._message = msg ?? "Registration failed";
                }
                dlg._loading = false;
            }
            // clear target
            _bgTarget = null; _bgCmd = null; _bgUser = null; _bgPass = null;

            // Do not return; park this worker to avoid ThreadPool.Terminate panic path
            for (;;){ Thread.Sleep(1000); }
        }

        public LoginDialog() : base((Framebuffer.Width - 420) / 2, (Framebuffer.Height - 240) / 2, 420, 240) {
            Title = "Sign in";
            // Fullscreen modal style
            ShowInTaskbar = false; ShowMinimize = false; ShowMaximize = false; BarHeight = 0;
            X = 0; Y = 0; Width = Framebuffer.Width; Height = Framebuffer.Height;
            Keyboard.OnKeyChanged += Keyboard_OnKeyChanged;
        }

        private static string Stars(int count) {
            if (count <= 0) return string.Empty;
            string s = string.Empty; for (int i = 0; i < count; i++) s += "*"; return s;
        }

        private void Keyboard_OnKeyChanged(object sender, System.ConsoleKeyInfo key) {
            if (!Visible) return; if (_loading) return;
            if (key.KeyState != System.ConsoleKeyState.Pressed) { _keyDown = false; _lastScan = 0; return; }
            if (_keyDown && Keyboard.KeyInfo.ScanCode == _lastScan) return; _keyDown = true; _lastScan = (byte)Keyboard.KeyInfo.ScanCode;
            if (key.Key == System.ConsoleKey.Escape) { this.Visible = false; return; }
            if (key.Key == System.ConsoleKey.Tab) { _userFocus = !_userFocus; _pwdFocus = !_pwdFocus; return; }
            if (key.Key == System.ConsoleKey.Enter) { StartLogin(); return; }

            string target = _userFocus ? _username : _password;
            if (key.Key == System.ConsoleKey.Backspace) { if (target.Length > 0) target = target.Substring(0, target.Length - 1); }
            else if (key.Key == System.ConsoleKey.Space || Keyboard.KeyInfo.ScanCode == 57 || Keyboard.KeyInfo.ScanCode == 44) target += " ";
            else if (key.Key >= System.ConsoleKey.A && key.Key <= System.ConsoleKey.Z) { char c = (char)('a' + (key.Key - System.ConsoleKey.A)); bool upper = Keyboard.KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Shift) ^ Keyboard.KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.CapsLock); if (upper) c = (char)(c - 32); target += c; }
            else if (key.Key >= System.ConsoleKey.D0 && key.Key <= System.ConsoleKey.D9) { char c = (char)('0' + (key.Key - System.ConsoleKey.D0)); target += c; }
            else {
                switch (key.Key) {
                    case System.ConsoleKey.OemPeriod: target += "."; break;
                    case System.ConsoleKey.OemMinus: target += "-"; break;
                    case System.ConsoleKey.OemPlus: target += "+"; break;
                    case System.ConsoleKey.Oem1: target += ";"; break;
                    case System.ConsoleKey.Oem2: target += "/"; break;
                    case System.ConsoleKey.Oem3: target += "`"; break;
                    case System.ConsoleKey.Oem4: target += "["; break;
                    case System.ConsoleKey.Oem5: target += "\\"; break;
                    case System.ConsoleKey.Oem6: target += "]"; break;
                    case System.ConsoleKey.Oem7: target += "'"; break;
                    case System.ConsoleKey.OemComma: target += ","; break;
                }
            }
            if (_userFocus) _username = target; else _password = target;
#if UEFI_DIAGNOSTIC_INPUT || UEFI_DIAGNOSTIC_INPUT_STRESS
            Program.MarkUefiGuiKeyRouted();
#endif
        }

        public override void OnInput() {
            if (!Visible) return;
            // Swallow mouse while visible so desktop doesn't interact
            WindowManager.MouseHandled = true;
            if (_loading) return;

            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y; bool left = (Control.MouseButtons == MouseButtons.Left);

            // Centered panel metrics
            int panelW = PanelW; int panelH = 260;
            int panelX = (Framebuffer.Width - panelW) / 2;
            int panelY = (Framebuffer.Height - panelH) / 2;

            int cx = panelX + PanelPad; int cw = panelW - PanelPad * 2;
            int rowH = WindowManager.font.FontSize + 10; int boxH = rowH + 8;
            int userY = panelY + PanelPad + WindowManager.font.FontSize + 4; // after "Username" label
            int pwdLabelY = userY + boxH + 8;
            int pwdY = pwdLabelY + WindowManager.font.FontSize + 4;
            int btnY = panelY + panelH - PanelPad - BtnH;
            int loginX = panelX + panelW - PanelPad - BtnW;
            int cancelX = loginX - Gap - BtnW;
            int regX = cancelX - Gap - BtnW;

            if (left) {
                if (!_clickLatch) {
#if UEFI_DIAGNOSTIC_INPUT || UEFI_DIAGNOSTIC_INPUT_STRESS
                    // Diagnostic builds use this existing focusable window as
                    // the proof target; any physical click reaching OnInput
                    // is sufficient to prove window routing.
                    Program.MarkUefiGuiMouseRouted();
#endif
                    // Username box
                    if (mx >= cx && mx <= cx + cw && my >= userY && my <= userY + boxH) { _userFocus = true; _pwdFocus = false; _clickLatch = true;
#if UEFI_DIAGNOSTIC_INPUT || UEFI_DIAGNOSTIC_INPUT_STRESS
                        Program.MarkUefiGuiMouseRouted();
#endif
                        return; }
                    // Password box
                    if (mx >= cx && mx <= cx + cw && my >= pwdY && my <= pwdY + boxH) { _userFocus = false; _pwdFocus = true; _clickLatch = true; return; }
                    // Login
                    if (mx >= loginX && mx <= loginX + BtnW && my >= btnY && my <= btnY + BtnH) { StartLogin(); _clickLatch = true; return; }
                    // Register
                    if (mx >= regX && mx <= regX + BtnW && my >= btnY && my <= btnY + BtnH) { StartRegister(); _clickLatch = true; return; }
                    // Cancel
                    if (mx >= cancelX && mx <= cancelX + BtnW && my >= btnY && my <= btnY + BtnH) { this.Visible = false; _clickLatch = true; return; }
                }
            } else { _clickLatch = false; }
        }

        private void StartLogin(){
            _message = null; _loading = true; _loadingText = "Signing in";
            _bgTarget = this; _bgCmd = "login"; _bgUser = _username; _bgPass = _password;
            unsafe { new Thread(&BackgroundWorker, 65536).Start(); }
        }

        private void StartRegister(){
            _message = null; _loading = true; _loadingText = "Registering";
            _bgTarget = this; _bgCmd = "register"; _bgUser = _username; _bgPass = _password;
            unsafe { new Thread(&BackgroundWorker, 65536).Start(); }
        }

        public override void OnDraw() {
            if (!Visible) return;
            // Dim the entire screen
            Framebuffer.Graphics.AFillRectangle(0, 0, Framebuffer.Width, Framebuffer.Height, 0xAA000000);

            // Centered panel
            int panelW = PanelW; int panelH = 260;
            int panelX = (Framebuffer.Width - panelW) / 2;
            int panelY = (Framebuffer.Height - panelH) / 2;
            UIPrimitives.AFillRoundedRect(panelX, panelY, panelW, panelH, 0xDD222222, 6);
            UIPrimitives.DrawRoundedRect(panelX, panelY, panelW, panelH, 0xFF3F3F3F, 1, 6);

            int cx = panelX + PanelPad; int cy = panelY + PanelPad; int cw = panelW - PanelPad * 2;
            int boxH = WindowManager.font.FontSize + 10;

            // Labels and fields
            WindowManager.font.DrawString(cx, cy, "Username");
            int userY = cy + WindowManager.font.FontSize + 4;
            DrawTextBox(cx, userY, cw, boxH, _username, _userFocus);

            int pwdLabelY = userY + boxH + 8;
            WindowManager.font.DrawString(cx, pwdLabelY, "Password");
            int pwdY = pwdLabelY + WindowManager.font.FontSize + 4;
            string masked = Stars(_password.Length);
            DrawTextBox(cx, pwdY, cw, boxH, masked, _pwdFocus);
            if (!string.IsNullOrEmpty(_username) || !string.IsNullOrEmpty(_password)) {
                Program.MarkUefiLoginTextRendered();
            }

            // Buttons
            int btnY = panelY + panelH - PanelPad - BtnH;
            int loginX = panelX + panelW - PanelPad - BtnW;
            int cancelX = loginX - Gap - BtnW;
            int regX = cancelX - Gap - BtnW;
            DrawButton(regX, btnY, BtnW, BtnH, "Register");
            DrawButton(cancelX, btnY, BtnW, BtnH, "Cancel");
            DrawButton(loginX, btnY, BtnW, BtnH, "Login");

            // Loading overlay on panel
            if (_loading) {
                // update spinner phase on timer tick
                if (_lastTick != Timer.Ticks) { _lastTick = Timer.Ticks; _spinnerPhase = (_spinnerPhase + 1) % 3; }
                int overlayH = 36; int oy = btnY - overlayH - 8; int ox = cx; int ow = panelW - PanelPad * 2;
                UIPrimitives.AFillRoundedRect(ox, oy, ow, overlayH, 0x99232323, 6);
                string text = string.IsNullOrEmpty(_loadingText) ? "Working" : _loadingText;
                int tw = WindowManager.font.MeasureString(text);
                int tx = ox + 10; int ty = oy + (overlayH / 2) - (WindowManager.font.FontSize / 2);
                WindowManager.font.DrawString(tx, ty, text);
                // animated dots to the right
                int dot = 6; int gap = 6; int startX = tx + tw + 10; int centerY = oy + overlayH / 2;
                for (int i = 0; i < 3; i++) {
                    uint c = (i == _spinnerPhase) ? 0xFFFFFFFFu : 0xFF888888u;
                    Framebuffer.Graphics.FillRectangle(startX + i * (dot + gap), centerY - (dot / 2), dot, dot, c);
                }
            }

            // Message
            if (!_loading && !string.IsNullOrEmpty(_message)) {
                WindowManager.font.DrawString(cx, btnY - (WindowManager.font.FontSize + 10), _message, panelW - PanelPad*2, WindowManager.font.FontSize*2);
            }
        }

        private void DrawTextBox(int x, int y, int w, int h, string text, bool focus){
            uint bg = focus ? 0xFF2C2C2C : 0xFF262626;
            Framebuffer.Graphics.FillRectangle(x, y, w, h, bg);
            Framebuffer.Graphics.DrawRectangle(x, y, w, h, 0xFF444444, 1);
            WindowManager.font.DrawString(x + 6, y + (h/2 - WindowManager.font.FontSize/2), text);
        }
        private void DrawButton(int x, int y, int w, int h, string label){
            uint col = UI.ButtonFillColor(x, y, w, h, 0xFF2A2A2A, 0xFF343434, 0xFF3F3F3F);
            Framebuffer.Graphics.FillRectangle(x, y, w, h, col);
            Framebuffer.Graphics.DrawRectangle(x, y, w, h, 0xFF3F3F3F, 1);
            int tx = x + (w/2) - (WindowManager.font.MeasureString(label)/2);
            int ty = y + (h/2) - (WindowManager.font.FontSize/2);
            WindowManager.font.DrawString(tx, ty, label);
        }
    }
}
