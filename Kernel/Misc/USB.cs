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
        /// Match the firmware's copied BAR resource against the kernel PCI
        /// scan, map only the capability header as UC read-only memory, and
        /// parse its fixed capability registers. This path has no MMIO writes.
        /// </summary>
        internal static void ReportXhciInitializationFrontier(
                UefiBootInfo* bootInfo, UefiPciResourceHandoff* handoff) {
            if (PCI.Devices == null) {
                BootConsole.WriteLine("[USB][XHCI] stop=pci-not-initialized");
                return;
            }
            if (handoff == null) {
                BootConsole.WriteLine(
                    "[USB][XHCI] stop=resource-handoff-unavailable " +
                    "reason=legacy-loader-or-invalid-extension; capability-read=not-attempted");
                return;
            }

            if (sizeof(UefiPciResourceHandoff) != 528) {
                BootConsole.WriteLine(
                    "[USB][XHCI] stop=resource-handoff-invalid reason=kernel-layout");
                return;
            }
            if (handoff->EntryCount > XhciDiscovery.MaximumFirmwareResources) {
                BootConsole.WriteLine(
                    "[USB][XHCI] stop=resource-handoff-invalid reason=entry-count");
                return;
            }

            XhciDiscovery.FirmwareResourceEntry[] entries =
                new XhciDiscovery.FirmwareResourceEntry[(int)handoff->EntryCount];
            byte* storage = handoff->EntryStorage;
            XhciDiscovery.FirmwareResourceEntry* source =
                (XhciDiscovery.FirmwareResourceEntry*)storage;
            for (int i = 0; i < entries.Length; i++) entries[i] = source[i];

            if (!XhciDiscovery.TryValidateResourceContract(handoff->Magic,
                    handoff->Version, handoff->EntrySize, handoff->EntryCount,
                    handoff->DiscoveryStatus, entries,
                    out XhciDiscovery.ResourceFailure contractFailure)) {
                BootConsole.WriteLine(
                    $"[USB][XHCI] stop=resource-handoff-rejected reason={contractFailure} " +
                    $"firmware-status={handoff->DiscoveryStatus}; capability-read=not-attempted");
                return;
            }

            if (!FirmwareResourcesMatchKernelDiscovery(entries)) {
                BootConsole.WriteLine(
                    "[USB][XHCI] stop=resource-handoff-rejected " +
                    "reason=firmware-kernel-discovery-mismatch; capability-read=not-attempted");
                return;
            }

            if (entries.Length == 0) {
                BootConsole.WriteLine(
                    "[USB][XHCI] stop=no-controller-found source=efi-pci-io; " +
                    "capability-read=not-attempted");
                return;
            }

            for (int i = 0; i < entries.Length; i++) {
                XhciDiscovery.FirmwareResourceEntry entry = entries[i];
                string address = $"pci={entry.Segment:x4}:{entry.Bus:x2}:" +
                    $"{entry.Device:x2}.{entry.Function:x1} " +
                    $"id={entry.VendorId:x4}:{entry.DeviceId:x4}";
                if (!TryFindPciDevice(entry, out PCIDevice device,
                        out bool identityValid)) {
                    BootConsole.WriteLine(
                        $"[USB][XHCI] resource-rejected reason=" +
                        $"{(identityValid ? "pci-function-missing" : "pci-identity-mismatch")} {address}");
                    continue;
                }

                if (!XhciDiscovery.TryValidateFirmwareResource(entry,
                        device.Segment, (byte)device.Bus, (byte)device.Slot,
                        (byte)device.Function, device.VendorID, device.DeviceID,
                        device.ClassID, device.SubClassID, device.ProgIF,
                        device.Bar0, device.Bar1,
                        out XhciDiscovery.ResourceFailure resourceFailure)) {
                    BootConsole.WriteLine(
                        $"[USB][XHCI] resource-rejected reason={resourceFailure} {address}");
                    continue;
                }

                BootConsole.WriteLine(
                    $"[USB][XHCI] controller=matched bar0=validated " +
                    $"base=0x{entry.MmioBase:x} aperture=0x{entry.ApertureLength:x} {address}");

                if (!MmioMappingDoesNotOverlapOtherMemory(bootInfo, entry)) {
                    BootConsole.WriteLine(
                        $"[USB][XHCI] mmio-map=blocked reason=memory-map-conflict-or-invalid " +
                        $"base=0x{entry.MmioBase:x} {address}");
                    BootConsole.WriteLine(
                        $"[USB][XHCI] capability-read=not-attempted {address}");
                    continue;
                }

                if (!PageTable.TryMapMmioAccess(entry.MmioBase,
                        entry.ApertureLength, 0,
                        XhciDiscovery.CapabilityHeaderLength,
                        out byte* registers)) {
                    BootConsole.WriteLine(
                        $"[USB][XHCI] mmio-map=failed reason=range-pat-or-page-table " +
                        $"base=0x{entry.MmioBase:x} length=0x{entry.ApertureLength:x} {address}");
                    BootConsole.WriteLine(
                        $"[USB][XHCI] capability-read=not-attempted {address}");
                    continue;
                }

                BootConsole.WriteLine(
                    $"[USB][XHCI] mmio-map=success attributes=uc,read-only,nx " +
                    $"access=0x{XhciDiscovery.CapabilityHeaderLength:x} {address}");

                MappedXhciMmioReader reader = new(registers, entry.ApertureLength,
                    XhciDiscovery.CapabilityHeaderLength);
                if (!XhciDiscovery.TryReadCapabilities(reader,
                        entry.ApertureLength,
                        out XhciDiscovery.Capabilities capabilities,
                        out XhciDiscovery.CapabilityFailure capabilityFailure)) {
                    BootConsole.WriteLine(
                        $"[USB][XHCI] capability-read=failed source=hardware-mmio " +
                        $"reason={capabilityFailure} {address}");
                    continue;
                }

                BootConsole.WriteLine(
                    $"[USB][XHCI] capability-read=hardware-mmio source=efi-pci-io " +
                    $"base=0x{entry.MmioBase:x} aperture=0x{entry.ApertureLength:x} {address}");
                BootConsole.WriteLine(
                    $"[USB][XHCI] capabilities version=0x{capabilities.ControllerVersion:x4} " +
                    $"caplength=0x{capabilities.CapabilityLength:x2} " +
                    $"slots={capabilities.MaximumDeviceSlots} " +
                    $"interrupters={capabilities.AdvertisedInterruptResources} " +
                    $"ports={capabilities.RootPortCount} " +
                    $"scratchpads={capabilities.ScratchpadBufferCount} " +
                    $"ctx64={capabilities.Uses64ByteContexts} " +
                    $"addr64={capabilities.Supports64BitAddresses} " +
                    $"dboff=0x{capabilities.DoorbellOffset:x} " +
                    $"rtsoff=0x{capabilities.RuntimeRegisterOffset:x}");
            }
        }

        private static bool TryFindPciDevice(
                XhciDiscovery.FirmwareResourceEntry entry,
                out PCIDevice result, out bool identityValid) {
            result = null;
            identityValid = true;
            bool foundBdf = false;
            for (int i = 0; i < PCI.Devices.Count; i++) {
                PCIDevice candidate = PCI.Devices[i];
                if (candidate == null || candidate.Segment != entry.Segment ||
                    candidate.Bus != entry.Bus || candidate.Slot != entry.Device ||
                    candidate.Function != entry.Function) continue;
                if (candidate.VendorID != entry.VendorId ||
                    candidate.DeviceID != entry.DeviceId ||
                    !XhciDiscovery.IsXhciFunction(candidate.ClassID,
                        candidate.SubClassID, candidate.ProgIF)) {
                    identityValid = false;
                    return false;
                }
                if (!foundBdf) {
                    result = candidate;
                    foundBdf = true;
                } else if (result.Bar0 != candidate.Bar0 ||
                           result.Bar1 != candidate.Bar1) {
                    identityValid = false;
                    return false;
                }
            }
            return foundBdf;
        }

        private static bool MmioMappingDoesNotOverlapOtherMemory(
                UefiBootInfo* bootInfo,
                XhciDiscovery.FirmwareResourceEntry entry) {
            if (bootInfo == null || !bootInfo->HasMemoryMap ||
                bootInfo->MemoryMap == 0 || bootInfo->MemoryMapEntryCount == 0 ||
                bootInfo->MemoryMapEntryCount > 65536 ||
                bootInfo->MemoryMapDescriptorSize < 40 ||
                bootInfo->MemoryMapDescriptorSize > 4096 ||
                bootInfo->MemoryMapEntryCount >
                    0xFFFF_FFFF_FFFF_FFFFUL / bootInfo->MemoryMapDescriptorSize)
                return false;

            if (!XhciDiscovery.TryBuildMmioMappingPlan(entry.MmioBase,
                    entry.ApertureLength, 0,
                    XhciDiscovery.CapabilityHeaderLength,
                    XhciDiscovery.PageSize, out XhciDiscovery.MmioMappingPlan plan) ||
                plan.PageCount == 0 || plan.PageCount > 16) return false;

            ulong mapBytes = bootInfo->MemoryMapEntryCount *
                bootInfo->MemoryMapDescriptorSize;
            if (bootInfo->MemoryMap > 0xFFFF_FFFF_FFFF_FFFFUL - mapBytes)
                return false;
            ulong mappedLength = plan.PageCount * XhciDiscovery.PageSize;
            ulong mappedEnd = plan.FirstPhysicalPage + mappedLength - 1;
            byte* map = (byte*)bootInfo->MemoryMap;
            for (ulong i = 0; i < bootInfo->MemoryMapEntryCount; i++) {
                byte* descriptor = map + i * bootInfo->MemoryMapDescriptorSize;
                uint type = *(uint*)descriptor;
                ulong descriptorBase = *(ulong*)(descriptor + 8);
                ulong pageCount = *(ulong*)(descriptor + 24);
                if (pageCount == 0 || pageCount >
                    0xFFFF_FFFF_FFFF_FFFFUL / XhciDiscovery.PageSize) return false;
                ulong descriptorLength = pageCount * XhciDiscovery.PageSize;
                if (descriptorBase > 0xFFFF_FFFF_FFFF_FFFFUL -
                        (descriptorLength - 1)) return false;
                ulong descriptorEnd = descriptorBase + descriptorLength - 1;
                if (descriptorBase <= mappedEnd && plan.FirstPhysicalPage <= descriptorEnd &&
                    type != 11) { // EfiMemoryMappedIO is the only accepted overlap.
                    return false;
                }
            }
            return true;
        }

        private static bool FirmwareResourcesMatchKernelDiscovery(
                XhciDiscovery.FirmwareResourceEntry[] entries) {
            if (entries == null || PCI.Devices == null) return false;
            XhciDiscovery.ControllerIdentity[] controllers =
                new XhciDiscovery.ControllerIdentity[entries.Length];
            int uniqueControllers = 0;
            for (int i = 0; i < PCI.Devices.Count; i++) {
                PCIDevice device = PCI.Devices[i];
                if (device == null || !XhciDiscovery.IsXhciFunction(
                        device.ClassID, device.SubClassID, device.ProgIF) ||
                    WasXhciFunctionReported(i, device)) continue;
                if (uniqueControllers >= controllers.Length) return false;
                controllers[uniqueControllers++] = new XhciDiscovery.ControllerIdentity {
                    Segment = device.Segment,
                    Bus = (byte)device.Bus,
                    Device = (byte)device.Slot,
                    Function = (byte)device.Function,
                    VendorId = device.VendorID,
                    DeviceId = device.DeviceID,
                    ClassCode = device.ClassID,
                    Subclass = device.SubClassID,
                    ProgrammingInterface = device.ProgIF
                };
            }
            return uniqueControllers == entries.Length &&
                XhciDiscovery.MatchFirmwareResourcesToControllers(entries, controllers);
        }

        private sealed class MappedXhciMmioReader : XhciDiscovery.IReadOnlyMmioReader {
            private readonly byte* _registers;
            private readonly ulong _resourceLength;
            private readonly ulong _mappedAccessLength;

            public MappedXhciMmioReader(byte* registers, ulong resourceLength,
                    ulong mappedAccessLength) {
                _registers = registers;
                _resourceLength = resourceLength;
                _mappedAccessLength = mappedAccessLength;
            }

            public bool IsMapped => _registers != null;

            public bool TryRead32(ulong offset, out uint value) {
                value = 0;
                if (!IsMapped || (offset & 3UL) != 0 ||
                    !XhciDiscovery.IsSubrangeWithinResource(
                        _resourceLength, offset, sizeof(uint)) ||
                    !XhciDiscovery.IsSubrangeWithinResource(
                        _mappedAccessLength, offset, sizeof(uint))) return false;
                value = Native.ReadMmio32((ulong)(_registers + offset));
                return true;
            }
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
