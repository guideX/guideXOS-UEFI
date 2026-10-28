using guideXOS.Kernel.Drivers;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace guideXOS.Misc {
    /// <summary>
    /// USB Device descriptor and request structures
    /// </summary>
    public class USBDevice {
        public byte USBVersion;

        public int Speed;

        public byte Address;

        public uint Ring;
        public uint RingOffset;

        public int AssignedSloth;

        public byte Class;
        public byte SubClass;
        public byte Protocol;

        public uint EndpointIn;
        public uint EndpointOut;

        public uint Localoutringoffset;
        internal int Port;
        internal USBDevice Parent;
        public ushort Interface;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct USBRequest {
        public byte RequestType;
        public byte Request;

        public ushort Value;
        public ushort Index;
        public ushort Length;

        public void Clean() {
            fixed (void* p = &this)
                Native.Stosb(p, 0, (ulong)sizeof(USBRequest));
        }
    }

    /// <summary>
    /// USB Core - Handles USB device communication and polling
    /// 
    /// ============================================================================
    /// UEFI COMPATIBILITY AUDIT
    /// ============================================================================
    /// 
    /// CLASSIFICATION SUMMARY:
    /// -----------------------
    /// 1. POTENTIALLY UEFI-COMPATIBLE:
    ///    - OnInterrupt(): Polls HID devices for input
    ///    - LoopPoll(): Background thread for continuous polling
    ///    - SendAndReceive(): Generic USB transfer wrapper
    ///    - DriveDevice(): Device class dispatch
    ///    
    ///    These DO NOT use legacy ports (0x60/0x64) or PIC IRQs.
    /// 
    /// 2. DEPENDENCIES (must work for USB to work in UEFI):
    ///    - EHCI.cs: USB 2.0 host controller (PCI-based)
    ///    - Hub.cs: USB hub enumeration
    ///    - HID.cs: Human Interface Device parsing
    ///    - PCI.cs: Must detect USB controllers
    ///    - ThreadPool.cs: For background polling thread
    /// 
    /// 3. ENTRY POINTS:
    ///    - Program.KMain() -> USB.StartPolling() (Legacy only currently)
    ///    - USB.LoopPoll() runs in background thread
    /// 
    /// 4. UEFI CONSIDERATIONS:
    ///    - EHCI registers must be properly mapped (MMIO)
    ///    - UEFI may have already claimed USB controllers
    ///    - May need to take over from UEFI USB driver
    ///    - Alternative: Let UEFI handle USB, poll via runtime service
    /// 
    /// RECOMMENDED UEFI APPROACH:
    /// --------------------------
    /// Option A (Take over USB): Enable USB stack in UEFI mode
    ///   - Initialize EHCI/XHCI from scratch
    ///   - Works if UEFI hasn't locked the controller
    /// 
    /// Option B (Use UEFI USB): Store pointers in bootInfo
    ///   - Bootloader keeps USB driver active
    ///   - Kernel polls via shared memory
    ///   - Complex but avoids re-implementing USB
    /// 
    /// ============================================================================
    /// </summary>
    public static unsafe class USB {
        public static byte NumDevice;
        public static byte DeviceAddr;

        public static bool SendAndReceive(USBDevice device, USBRequest* cmd, void* buffer, USBDevice parent) {
            if (device.USBVersion == 2) {
                return EHCI.SendAndReceive(device.Address, cmd, buffer, parent, device.Speed);
            } else {
                return false;
            }
        }

        /// <summary>
        /// Poll USB HID devices for input
        /// 
        /// POTENTIALLY UEFI-COMPATIBLE: Uses USB transfers, not legacy ports.
        /// Updates Control.MousePosition/MouseButtons from HID data.
        /// 
        /// NOTE: Consider applying PS2Mouse filtering logic here:
        /// - TouchpadSensitivity
        /// - NoiseThreshold
        /// - MaxDeltaPerPacket
        /// - Button debouncing
        /// </summary>
        public static void OnInterrupt() {
            if (HID.Keyboard != null) {
                HID.GetKeyboard(HID.Keyboard, out byte ScanCode, out ConsoleKey Key);
                Keyboard.KeyInfo.KeyState = Key != ConsoleKey.None ? ConsoleKeyState.Pressed : ConsoleKeyState.Released;

                if (Key != ConsoleKey.None) {
                    Keyboard.KeyInfo.ScanCode = ScanCode;
                    Keyboard.KeyInfo.Key = Key;
                }

                Keyboard.InvokeOnKeyChanged(Keyboard.KeyInfo);
            }

            if (!VMwareTools.Available && HID.Mouse != null) {
                HID.GetMouse(HID.Mouse, out sbyte AxisX, out sbyte AxisY, out MouseButtons buttons);

                Control.MousePosition.X = Math.Clamp(Control.MousePosition.X + AxisX, 0, Framebuffer.Width);
                Control.MousePosition.Y = Math.Clamp(Control.MousePosition.Y + AxisY, 0, Framebuffer.Height);

                Control.MouseButtons = buttons;
            }
        }

        public static bool InitPort(int port, USBDevice parent, int version, int speed) {
            if (version == 2) {
                return EHCI.InitPort(port, parent, speed);
            }
            return false;
        }

        public static void DriveDevice(USBDevice device) {
            switch (device.Class) {
                case 3:
                    HID.Initialize(device);
                    break;
                case 9:
                    Hub.Initialize(device);
                    break;
                case 8:
                    USBMSC.Initialize(device);
                    break;
                default:
                    BootConsole.WriteLine($"[USB] Unrecognized device class:{device.Class} subClass:{device.SubClass}");
                    break;

            }
        }

        public static void Reset() {
            USB.NumDevice = 0;
            USB.DeviceAddr = 0;
        }

        /// <summary>
        /// Validate the xHCI PCI BAR shape and report the safe MMIO frontier.
        /// The assigned BAR aperture length is not available through the
        /// current PCI/UEFI handoff, so this diagnostic does not map or read
        /// controller registers.
        /// </summary>
        public static void ReportXhciInitializationFrontier() {
            if (PCI.Devices == null) {
                BootConsole.WriteLine("[USB][XHCI] stop=pci-not-initialized");
                return;
            }

            const int diagnosticLimit = 8;
            int candidates = 0;
            int suppressed = 0;
            for (int i = 0; i < PCI.Devices.Count; i++) {
                PCIDevice device = PCI.Devices[i];
                if (device == null || !XhciDiscovery.IsXhciFunction(
                    device.ClassID, device.SubClassID, device.ProgIF)) {
                    continue;
                }
                if (WasXhciFunctionReported(i, device)) continue;

                candidates++;
                if (candidates > diagnosticLimit) {
                    suppressed++;
                    continue;
                }

                string address = $"pci={device.Bus:x2}:{device.Slot:x2}.{device.Function:x1} " +
                    $"id={device.VendorID:x4}:{device.DeviceID:x4}";
                BootConsole.WriteLine($"[USB][XHCI] controller=found {address}");

                XhciDiscovery.BarInfo bar;
                XhciDiscovery.BarStatus barStatus = XhciDiscovery.TryParseMemoryBar(
                    device.Bar0, device.Bar1, hasUpperBar: true, out bar);
                if (barStatus != XhciDiscovery.BarStatus.Valid) {
                    BootConsole.WriteLine(
                        $"[USB][XHCI] bar-resource=invalid status={barStatus} {address}");
                    BootConsole.WriteLine(
                        $"[USB][XHCI] mmio-access=unavailable capability-header=not-read " +
                        $"capabilities=not-decoded {address}");
                    continue;
                }

                BootConsole.WriteLine(
                    $"[USB][XHCI] bar-shape=validated base=0x{bar.BaseAddress:x} " +
                    $"width={(bar.Is64Bit ? 64 : 32)} prefetchable={bar.IsPrefetchable} {address}");

                BootConsole.WriteLine(
                    $"[USB][XHCI] bar-resource=unverified reason=aperture-size-unavailable {address}");
                BootConsole.WriteLine(
                    $"[USB][XHCI] mmio-access=unavailable reason=resource-bounds-unverified " +
                    $"capability-header=not-read capabilities=not-decoded {address}");
            }

            if (candidates == 0) {
                BootConsole.WriteLine(
                    "[USB][XHCI] stop=no-controller-found stage=pci-discovery-complete");
                return;
            }

            BootConsole.WriteLine(
                $"[USB][XHCI] stop=bar-aperture-size-unavailable candidates={candidates} " +
                $"suppressed={suppressed}; xHCI MMIO/DMA untouched");
        }

        private static bool WasXhciFunctionReported(int index, PCIDevice device) {
            for (int i = 0; i < index; i++) {
                PCIDevice previous = PCI.Devices[i];
                if (previous != null && XhciDiscovery.IsXhciFunction(
                    previous.ClassID, previous.SubClassID, previous.ProgIF) &&
                    previous.Segment == device.Segment &&
                    previous.Bus == device.Bus && previous.Slot == device.Slot &&
                    previous.Function == device.Function) return true;
            }
            return false;
        }

        public static void StartPolling() {
            new Thread(&LoopPoll).Start();
        }

        /// <summary>
        /// Background polling thread for USB devices
        /// 
        /// POTENTIALLY UEFI-COMPATIBLE: Continuously polls USB HID devices.
        /// For UEFI: Can run from main loop without ThreadPool:
        ///   while (true) {
        ///       if (USB.NumDevice != 0) USB.OnInterrupt();
        ///       // ... render frame ...
        ///   }
        /// </summary>
        static void LoopPoll() {
            for (; ; )
            {
                if (USB.NumDevice != 0) {
                    USB.OnInterrupt();
                } else {
                    ThreadPool.Schedule_Next();
                }
            }
        }
    }
}
