namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// GuideXOS-owned i8042 controller setup. This is native hardware access
    /// and remains valid after ExitBootServices; it never calls a UEFI input
    /// protocol.
    /// </summary>
    public static unsafe class PS2Controller {
        internal const byte DataPort = 0x60;
        internal const byte StatusCommandPort = 0x64;
        private const int WaitLimit = 100000;
        private const int DrainLimit = 64;

        private static bool _present;
        private static bool _initialized;
        private static bool _initializing;

        public static bool IsPresent => _present;
        public static bool IsInitialized => _initialized;

        public static bool Initialize() {
            if (_initialized) return true;
            if (_initializing) return _present;

            _initializing = true;
            try {
                if (!EnsureController()) return false;

                bool keyboard = PS2Keyboard.InitializeDevice();
                bool mouse = PS2Mouse.InitializeDevice();
                _initialized = keyboard || mouse;
                return _initialized;
            } finally {
                _initializing = false;
            }
        }

        internal static bool EnsureInitialized() {
            return _initialized || EnsureController();
        }

        private static bool EnsureController() {
            if (_present) return true;

            try {
                byte initialStatus = Native.In8(StatusCommandPort);
                if (initialStatus == 0xFF) return false;

                SendControllerCommand(0xAD); // disable keyboard
                SendControllerCommand(0xA7); // disable auxiliary device
                DrainOutputBuffer();

                SendControllerCommand(0xAA); // controller self-test
                byte selfTest;
                if (!TryReadData(out selfTest) || selfTest != 0x55) return false;

                byte commandByte;
                if (!ReadCommandByte(out commandByte)) return false;

                // Enable IRQ1, IRQ12 and translation. Clear interface-disable
                // bits so both ports can be owned by the kernel.
                commandByte = (byte)((commandByte | 0x43) & ~0x30);
                if (!WriteCommandByte(commandByte)) return false;

                SendControllerCommand(0xAE); // enable keyboard
                SendControllerCommand(0xA8); // enable auxiliary device
                DrainOutputBuffer();

                _present = true;
                return true;
            } catch {
                _present = false;
                return false;
            }
        }

        internal static bool SendDeviceCommand(bool auxiliary, byte command) {
            if (!WaitForInputBufferEmpty()) return false;
            if (auxiliary && !SendControllerCommand(0xD4)) return false;
            if (!WaitForInputBufferEmpty()) return false;
            Native.Out8(DataPort, command);

            byte response;
            if (!TryReadData(out response)) return false;
            return response == 0xFA;
        }

        private static bool SendControllerCommand(byte command) {
            if (!WaitForInputBufferEmpty()) return false;
            Native.Out8(StatusCommandPort, command);
            return true;
        }

        private static bool ReadCommandByte(out byte value) {
            value = 0;
            if (!SendControllerCommand(0x20)) return false;
            return TryReadData(out value);
        }

        private static bool WriteCommandByte(byte value) {
            if (!SendControllerCommand(0x60)) return false;
            if (!WaitForInputBufferEmpty()) return false;
            Native.Out8(DataPort, value);
            return true;
        }

        internal static bool WaitForInputBufferEmpty() {
            for (int i = 0; i < WaitLimit; i++) {
                if ((Native.In8(StatusCommandPort) & 0x02) == 0) return true;
            }
            return false;
        }

        internal static bool TryReadData(out byte value) {
            value = 0;
            for (int i = 0; i < WaitLimit; i++) {
                if ((Native.In8(StatusCommandPort) & 0x01) != 0) {
                    value = Native.In8(DataPort);
                    return true;
                }
            }
            return false;
        }

        private static void DrainOutputBuffer() {
            for (int i = 0; i < DrainLimit; i++) {
                if ((Native.In8(StatusCommandPort) & 0x01) == 0) return;
                Native.In8(DataPort);
            }
        }
    }
}
