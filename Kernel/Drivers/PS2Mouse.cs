using guideXOS.GUI;
using guideXOS.Kernel.Drivers.Input;
using guideXOS.Misc;
using System;
using System.Windows.Forms;
using static Native;

namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// Native i8042 three-byte mouse support. IRQ12 captures raw bytes into a
    /// bounded queue; packet parsing and canonical GUI state updates run on
    /// the desktop thread.
    /// </summary>
    public static unsafe class PS2Mouse {
        private const byte Data = 0x60;
        private const int ByteQueueCapacity = 256;

        private static byte[] _byteQueue;
        private static volatile int _writeIndex;
        private static volatile int _readIndex;
        private static bool _nativeEnabled;
        private static bool _handlerRegistered;
        private static bool _initComplete;
        private static bool _irqMarkerLogged;
        private static bool _moveMarkerLogged;
        private static bool _leftDownMarkerLogged;
        private static bool _leftUpMarkerLogged;
        private static bool _rightDownMarkerLogged;
        private static bool _rightUpMarkerLogged;

        private static int _phase;
        private static MouseButtons _lastButtons;

        public static byte[] MData;
        public static int DeltaZ;
        public static int ScreenWidth;
        public static int ScreenHeight;

        public static int InterruptCount;
        public static int PacketCount;
        public static int ProcessedPacketCount;
        public static int MoveEventCount;
        public static int LeftDownCount;
        public static int LeftUpCount;
        public static int RightDownCount;
        public static int RightUpCount;
        public static int DroppedByteCount;

        // Kept as public tuning fields for existing callers. Native QEMU
        // packets are passed through the unified dispatcher without touchpad
        // filtering or debounce delays.
        public static float TouchpadSensitivity = 1.0f;
        public static int NoiseThreshold;
        public static int MaxDeltaPerPacket = 127;
        public static bool EnableTouchpadFiltering;

        public static bool IsNativeInitialized => _nativeEnabled;

        public static bool Initialize() {
            if (_nativeEnabled) return true;
            if (!PS2Controller.EnsureInitialized()) return false;
            return InitializeDevice();
        }

        internal static bool InitializeDevice() {
            if (_nativeEnabled) return true;
            if (!PS2Controller.EnsureInitialized()) return false;

            _byteQueue = new byte[ByteQueueCapacity];
            MData = new byte[4];
            _writeIndex = 0;
            _readIndex = 0;
            _phase = 0;
            _lastButtons = MouseButtons.None;
            InterruptCount = 0;
            PacketCount = 0;
            ProcessedPacketCount = 0;
            MoveEventCount = 0;
            LeftDownCount = 0;
            LeftUpCount = 0;
            RightDownCount = 0;
            RightUpCount = 0;
            DroppedByteCount = 0;
            _irqMarkerLogged = _moveMarkerLogged = false;
            _leftDownMarkerLogged = _leftUpMarkerLogged = false;
            _rightDownMarkerLogged = _rightUpMarkerLogged = false;
            ScreenWidth = Framebuffer.Width;
            ScreenHeight = Framebuffer.Height;
            Control.MouseButtons = MouseButtons.None;
            DeltaZ = 0;

            MouseEventDispatcher.Initialize();
            MouseEventDispatcher.EnableFiltering = false;
            MouseEventDispatcher.Sensitivity = 1.0f;
            MouseEventDispatcher.NoiseThreshold = 0;
            MouseEventDispatcher.MaxDeltaPerUpdate = 127;

            if (!PS2Controller.SendDeviceCommand(true, 0xF6) ||
                !PS2Controller.SendDeviceCommand(true, 0xF4)) {
                return false;
            }

            if (!_handlerRegistered) {
                Interrupts.EnableInterrupt(0x2C, &OnInterrupt);
                _handlerRegistered = true;
            }

            _nativeEnabled = true;
            return true;
        }

        /// <summary>
        /// IRQ12 capture path. It only reads the data port and enqueues one
        /// byte; it never allocates or touches managed GUI state.
        /// </summary>
        public static void OnInterrupt() {
            byte value = In8(Data);
            InterruptCount++;
            if (_byteQueue == null) return;

            int write = _writeIndex;
            int next = (write + 1) % ByteQueueCapacity;
            if (next == _readIndex) {
                DroppedByteCount++;
                return;
            }

            _byteQueue[write] = value;
            _writeIndex = next;
        }

        public static void EnableFullProcessing() {
            _initComplete = _nativeEnabled;
        }

        public static int ProcessPendingInput(int maxBytes = 256) {
            if (!_nativeEnabled || !_initComplete || _byteQueue == null) return 0;
            if (maxBytes < 1) return 0;

            int processed = 0;
            while (processed < maxBytes) {
                int read = _readIndex;
                if (read == _writeIndex) break;
                byte value = _byteQueue[read];
                _readIndex = (read + 1) % ByteQueueCapacity;
                ProcessByte(value);
                processed++;
            }
            return processed;
        }

        private static void ProcessByte(byte value) {
            if (!_irqMarkerLogged) {
                BootConsole.WriteLine("[INPUT] MOUSE_IRQ");
                _irqMarkerLogged = true;
            }

            if (_phase == 0) {
                if ((value & 0x08) != 0) {
                    MData[0] = value;
                    _phase = 1;
                }
                return;
            }

            if (_phase == 1) {
                MData[1] = value;
                _phase = 2;
                return;
            }

            MData[2] = value;
            _phase = 0;
            PacketCount++;
            ProcessPacket();
        }

        private static void ProcessPacket() {
            if ((MData[0] & 0x08) == 0) {
                _phase = 0;
                return;
            }

            int deltaX = (MData[0] & 0x10) != 0 ? MData[1] - 256 : MData[1];
            int deltaY = (MData[0] & 0x20) != 0 ? MData[2] - 256 : MData[2];
            deltaY = -deltaY;

            MouseButtons buttons = MouseButtons.None;
            if ((MData[0] & 0x01) != 0) buttons |= MouseButtons.Left;
            if ((MData[0] & 0x02) != 0) buttons |= MouseButtons.Right;
            if ((MData[0] & 0x04) != 0) buttons |= MouseButtons.Middle;

            MouseButtons oldButtons = _lastButtons;
            _lastButtons = buttons;
            MouseEvent evt = MouseEvent.CreateRelative(
                deltaX, deltaY, 0, buttons, Timer.Ticks, MouseInputSource.LegacyPS2);
            MouseEventDispatcher.DispatchEvent(evt);
            ProcessedPacketCount++;

            if (deltaX != 0 || deltaY != 0) {
                MoveEventCount++;
                if (!_moveMarkerLogged) {
                    BootConsole.WriteLine("[INPUT] MOUSE_MOVE X=" +
                        Control.MousePosition.X + " Y=" + Control.MousePosition.Y);
                    _moveMarkerLogged = true;
                }
            }

            if ((oldButtons & MouseButtons.Left) == 0 &&
                (buttons & MouseButtons.Left) != 0) {
                LeftDownCount++;
                if (!_leftDownMarkerLogged) {
                    BootConsole.WriteLine("[INPUT] MOUSE_LEFT_DOWN");
                    _leftDownMarkerLogged = true;
                }
            }
            if ((oldButtons & MouseButtons.Left) != 0 &&
                (buttons & MouseButtons.Left) == 0) {
                LeftUpCount++;
                if (!_leftUpMarkerLogged) {
                    BootConsole.WriteLine("[INPUT] MOUSE_LEFT_UP");
                    _leftUpMarkerLogged = true;
                }
            }
            if ((oldButtons & MouseButtons.Right) == 0 &&
                (buttons & MouseButtons.Right) != 0) {
                RightDownCount++;
                if (!_rightDownMarkerLogged) {
                    BootConsole.WriteLine("[INPUT] MOUSE_RIGHT_DOWN");
                    _rightDownMarkerLogged = true;
                }
            }
            if ((oldButtons & MouseButtons.Right) != 0 &&
                (buttons & MouseButtons.Right) == 0) {
                RightUpCount++;
                if (!_rightUpMarkerLogged) {
                    BootConsole.WriteLine("[INPUT] MOUSE_RIGHT_UP");
                    _rightUpMarkerLogged = true;
                }
            }
        }

        // Compatibility helpers for existing legacy callers. All waits are
        // finite and the actual command path is shared with native init.
        public static void WriteRegister(byte value) {
            PS2Controller.SendDeviceCommand(true, value);
        }

        public static byte ReadRegister() {
            byte value;
            return PS2Controller.TryReadData(out value) ? value : (byte)0;
        }
    }
}
