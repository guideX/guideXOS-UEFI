using guideXOS.Misc;
using System;
using static System.ConsoleKey;

namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// Native i8042 keyboard support. The IRQ handler only captures bytes;
    /// scan-code parsing and GUI/event callbacks run on the desktop thread.
    /// </summary>
    public static unsafe class PS2Keyboard {
        private const byte DataPort = 0x60;
        private const int ScanQueueCapacity = 256;

        private static byte[] _scanQueue;
        private static volatile int _writeIndex;
        private static volatile int _readIndex;
        private static bool _nativeEnabled;
        private static bool _handlerRegistered;
        private static bool _initComplete;
        private static bool _extended;
        private static bool _irqMarkerLogged;
        private static bool _downMarkerLogged;
        private static bool _upMarkerLogged;

        private static bool _leftShift;
        private static bool _rightShift;
        private static bool _leftCtrl;
        private static bool _rightCtrl;
        private static bool _leftAlt;
        private static bool _rightAlt;
        private static bool _capsLock;
        private static bool _numLock;
        private static bool _scrollLock;

        public static bool IsNativeInitialized => _nativeEnabled;
        public static int IrqCount;
        public static int ProcessedEventCount;
        public static int DroppedScancodeCount;
        public static int KeyDownCount;
        public static int KeyUpCount;

        public static bool Initialize() {
            if (_nativeEnabled) return true;
            if (!PS2Controller.EnsureInitialized()) return false;
            return InitializeDevice();
        }

        internal static bool InitializeDevice() {
            if (_nativeEnabled) return true;
            if (!PS2Controller.EnsureInitialized()) return false;

            _scanQueue = new byte[ScanQueueCapacity];
            _writeIndex = 0;
            _readIndex = 0;
            IrqCount = 0;
            ProcessedEventCount = 0;
            DroppedScancodeCount = 0;
            KeyDownCount = 0;
            KeyUpCount = 0;
            _extended = false;
            _leftShift = _rightShift = false;
            _leftCtrl = _rightCtrl = false;
            _leftAlt = _rightAlt = false;
            _capsLock = _numLock = _scrollLock = false;
            _irqMarkerLogged = _downMarkerLogged = _upMarkerLogged = false;

            // Force the device to Set 1. The controller translation bit is
            // also enabled, so this remains deterministic on QEMU and on
            // controllers that power up in Set 2.
            if (!PS2Controller.SendDeviceCommand(false, 0xF0) ||
                !PS2Controller.SendDeviceCommand(false, 0x01)) {
                return false;
            }

            if (!_handlerRegistered) {
                Interrupts.EnableInterrupt(0x21, &OnInterrupt);
                _handlerRegistered = true;
            }

            _nativeEnabled = true;
            return true;
        }

        /// <summary>
        /// IRQ1 capture path. No managed allocation, callbacks, logging, or
        /// GUI work is performed here.
        /// </summary>
        public static void OnInterrupt() {
            byte scanCode = Native.In8(DataPort);
            IrqCount++;
            if (_scanQueue == null) return;

            int write = _writeIndex;
            int next = (write + 1) % ScanQueueCapacity;
            if (next == _readIndex) {
                DroppedScancodeCount++;
                return;
            }

            _scanQueue[write] = scanCode;
            _writeIndex = next;
        }

        public static void EnableFullProcessing() {
            _initComplete = _nativeEnabled;
        }

        /// <summary>
        /// Drain a bounded number of captured bytes from the desktop thread.
        /// </summary>
        public static int ProcessPendingInput(int maxBytes = 128) {
            if (!_nativeEnabled || !_initComplete || _scanQueue == null) return 0;
            if (maxBytes < 1) return 0;

            int processed = 0;
            while (processed < maxBytes) {
                int read = _readIndex;
                if (read == _writeIndex) break;
                byte scanCode = _scanQueue[read];
                _readIndex = (read + 1) % ScanQueueCapacity;
                ProcessScanCode(scanCode);
                processed++;
            }
            return processed;
        }

        private static void ProcessScanCode(byte scanCode) {
            if (!_irqMarkerLogged) {
                BootConsole.WriteLine("[INPUT] KEYBOARD_IRQ");
                _irqMarkerLogged = true;
            }

            if (scanCode == 0x00 || scanCode == 0xFF) return;
            if (scanCode == 0xE0) {
                _extended = true;
                return;
            }

            // 0xE1 is the Pause sequence. It is not a text key and the
            // remainder is safely ignored until the next normal byte.
            if (scanCode == 0xE1) {
                _extended = false;
                return;
            }

            bool released = (scanCode & 0x80) != 0;
            byte makeCode = (byte)(scanCode & 0x7F);
            bool extended = _extended;
            _extended = false;

            UpdateModifiers(makeCode, released, extended);
            ConsoleKey key = ScancodeToKey(makeCode, extended);
            char keyChar = released ? '\0' : GetChar(makeCode, extended);

            ConsoleModifiers modifiers = ConsoleModifiers.None;
            if (_leftShift || _rightShift) modifiers |= ConsoleModifiers.Shift;
            if (_leftCtrl || _rightCtrl) modifiers |= ConsoleModifiers.Control;
            if (_leftAlt || _rightAlt) modifiers |= ConsoleModifiers.Alt;
            if (_capsLock) modifiers |= ConsoleModifiers.CapsLock;

            Keyboard.KeyInfo = new ConsoleKeyInfo {
                Key = key,
                KeyChar = keyChar,
                Modifiers = modifiers,
                KeyState = released ? ConsoleKeyState.Released : ConsoleKeyState.Pressed,
                ScanCode = scanCode
            };

            ProcessedEventCount++;
            if (released) {
                KeyUpCount++;
                if (!_upMarkerLogged) {
                    BootConsole.WriteLine("[INPUT] KEY_UP");
                    _upMarkerLogged = true;
                }
            } else {
                KeyDownCount++;
                if (!_downMarkerLogged) {
                    BootConsole.WriteLine("[INPUT] KEY_DOWN");
                    _downMarkerLogged = true;
                }
            }

            Keyboard.InvokeOnKeyChanged(Keyboard.KeyInfo);
            Kbd2Mouse.OnKeyChanged(Keyboard.KeyInfo);
        }

        private static void UpdateModifiers(byte makeCode, bool released, bool extended) {
            if (extended) {
                if (makeCode == 0x1D) _rightCtrl = !released;
                if (makeCode == 0x38) _rightAlt = !released;
                return;
            }

            switch (makeCode) {
                case 0x2A: _leftShift = !released; break;
                case 0x36: _rightShift = !released; break;
                case 0x1D: _leftCtrl = !released; break;
                case 0x38: _leftAlt = !released; break;
                case 0x3A: if (!released) _capsLock = !_capsLock; break;
                case 0x45: if (!released) _numLock = !_numLock; break;
                case 0x46: if (!released) _scrollLock = !_scrollLock; break;
            }
        }

        private static ConsoleKey ScancodeToKey(byte code, bool extended) {
            if (extended) {
                switch (code) {
                    case 0x1C: return Enter;
                    case 0x47: return Home;
                    case 0x48: return Up;
                    case 0x49: return PageUp;
                    case 0x4B: return Left;
                    case 0x4D: return Right;
                    case 0x4F: return End;
                    case 0x50: return Down;
                    case 0x51: return PageDown;
                    case 0x52: return Insert;
                    case 0x53: return Delete;
                    case 0x5B: return LeftWindows;
                    case 0x5C: return RightWindows;
                    case 0x5D: return Applications;
                    default: return ConsoleKey.None;
                }
            }

            switch (code) {
                case 0x01: return Escape;
                case 0x02: return D1; case 0x03: return D2; case 0x04: return D3;
                case 0x05: return D4; case 0x06: return D5; case 0x07: return D6;
                case 0x08: return D7; case 0x09: return D8; case 0x0A: return D9;
                case 0x0B: return D0; case 0x0C: return OemMinus; case 0x0D: return OemPlus;
                case 0x0E: return Backspace; case 0x0F: return Tab;
                case 0x10: return Q; case 0x11: return W; case 0x12: return E;
                case 0x13: return R; case 0x14: return T; case 0x15: return Y;
                case 0x16: return U; case 0x17: return I; case 0x18: return O;
                case 0x19: return P; case 0x1A: return Oem4; case 0x1B: return Oem6;
                case 0x1C: return Enter; case 0x1E: return A; case 0x1F: return S;
                case 0x20: return D; case 0x21: return F; case 0x22: return G;
                case 0x23: return H; case 0x24: return J; case 0x25: return K;
                case 0x26: return L; case 0x27: return Oem1; case 0x28: return Oem7;
                case 0x29: return Oem3; case 0x2B: return Oem5; case 0x2C: return Z;
                case 0x2D: return X; case 0x2E: return C; case 0x2F: return V;
                case 0x30: return B; case 0x31: return N; case 0x32: return M;
                case 0x33: return OemComma; case 0x34: return OemPeriod; case 0x35: return Oem2;
                case 0x37: return Multiply; case 0x39: return Space; case 0x3A: return CapsLock;
                case 0x3B: return F1; case 0x3C: return F2; case 0x3D: return F3;
                case 0x3E: return F4; case 0x3F: return F5; case 0x40: return F6;
                case 0x41: return F7; case 0x42: return F8; case 0x43: return F9;
                case 0x44: return F10; case 0x45: return NumLock; case 0x46: return Pause;
                case 0x47: return NumPad7; case 0x48: return NumPad8; case 0x49: return NumPad9;
                case 0x4A: return Subtract; case 0x4B: return NumPad4; case 0x4C: return NumPad5;
                case 0x4D: return NumPad6; case 0x4E: return Add; case 0x4F: return NumPad1;
                case 0x50: return NumPad2; case 0x51: return NumPad3; case 0x52: return NumPad0;
                case 0x53: return Decimal; case 0x57: return F11; case 0x58: return F12;
                default: return ConsoleKey.None;
            }
        }

        private static char GetChar(byte code, bool extended) {
            if (extended) return code == 0x1C ? '\n' : '\0';
            if (code == 0x39) return ' ';
            if (code == 0x1C) return '\n';
            if (code == 0x0F) return '\t';
            if (code == 0x0E) return '\b';

            char c = '\0';
            switch (code) {
                case 0x10: c = 'q'; break; case 0x11: c = 'w'; break; case 0x12: c = 'e'; break;
                case 0x13: c = 'r'; break; case 0x14: c = 't'; break; case 0x15: c = 'y'; break;
                case 0x16: c = 'u'; break; case 0x17: c = 'i'; break; case 0x18: c = 'o'; break;
                case 0x19: c = 'p'; break; case 0x1E: c = 'a'; break; case 0x1F: c = 's'; break;
                case 0x20: c = 'd'; break; case 0x21: c = 'f'; break; case 0x22: c = 'g'; break;
                case 0x23: c = 'h'; break; case 0x24: c = 'j'; break; case 0x25: c = 'k'; break;
                case 0x26: c = 'l'; break; case 0x2C: c = 'z'; break; case 0x2D: c = 'x'; break;
                case 0x2E: c = 'c'; break; case 0x2F: c = 'v'; break; case 0x30: c = 'b'; break;
                case 0x31: c = 'n'; break; case 0x32: c = 'm'; break;
            }
            if (c != '\0') {
                if ((_leftShift || _rightShift) ^ _capsLock) c = (char)(c - 32);
                return c;
            }

            bool shift = _leftShift || _rightShift;
            if (code >= 0x02 && code <= 0x0B) {
                string normal = "1234567890";
                string shifted = "!@#$%^&*()";
                return (shift ? shifted : normal)[code - 0x02];
            }
            switch (code) {
                case 0x0C: return shift ? '_' : '-'; case 0x0D: return shift ? '+' : '=';
                case 0x1A: return shift ? '{' : '['; case 0x1B: return shift ? '}' : ']';
                case 0x2B: return shift ? '|' : '\\'; case 0x27: return shift ? ':' : ';';
                case 0x28: return shift ? '"' : '\''; case 0x29: return shift ? '~' : '`';
                case 0x33: return shift ? '<' : ','; case 0x34: return shift ? '>' : '.';
                case 0x35: return shift ? '?' : '/';
            }
            return '\0';
        }
    }
}
