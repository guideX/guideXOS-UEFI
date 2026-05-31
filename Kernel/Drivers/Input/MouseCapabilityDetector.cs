using guideXOS.Kernel.Drivers.Legacy;
using guideXOS.Misc;
using System.Windows.Forms;

namespace guideXOS.Kernel.Drivers.Input {
    /// <summary>
    /// Mouse input capability detection and initialization
    /// 
    /// ============================================================================
    /// CAPABILITY-BASED DETECTION
    /// ============================================================================
    /// 
    /// This class implements capability-based mouse detection with the following
    /// priority order:
    /// 
    /// 1. UEFI Simple Pointer Protocol (UEFI boot only)
    ///    - Available before ExitBootServices
    ///    - Protocol pointer passed via UefiBootInfo
    ///    - Falls back to USB HID after ExitBootServices
    /// 
    /// 2. USB HID Mouse (both Legacy and UEFI)
    ///    - Requires USB controller initialization
    ///    - Works after ExitBootServices
    ///    - Preferred for modern hardware
    /// 
    /// 3. Legacy PS/2 Mouse (Legacy boot only, explicitly enabled)
    ///    - Only initialized in Legacy boot mode
    ///    - Only if PS/2 controller is detected and working
    ///    - Requires IRQ12 (0x2C) interrupt support
    /// 
    /// 4. VMware Backdoor (VMware VMs only)
    ///    - Provides absolute positioning
    ///    - Auto-detected via VMware signature
    /// 
    /// 5. Keyboard Emulation (fallback)
    ///    - Arrow keys control mouse
    ///    - Always available as last resort
    /// 
    /// If no mouse input is available, the system continues gracefully with
    /// the cursor at a fixed position.
    /// 
    /// ============================================================================
    /// </summary>
    public static unsafe class MouseCapabilityDetector {
        /// <summary>
        /// Detected mouse capabilities
        /// </summary>
        public enum MouseCapability {
            None = 0,
            UefiSimplePointer = 1,
            UefiAbsolutePointer = 2,
            UsbHidMouse = 3,
            LegacyPS2Mouse = 4,
            VMwareBackdoor = 5,
            KeyboardEmulation = 6
        }

        // Detection results
        private static MouseCapability _primaryCapability = MouseCapability.None;
        private static bool _detectionComplete = false;
        private static bool _mouseEnabled = false;
        
        // Capability flags
        private static bool _hasUefiSimplePointer = false;
        private static bool _hasUefiAbsolutePointer = false;
        private static bool _hasUsbHid = false;
        private static bool _hasPS2Controller = false;
        private static bool _hasVMwareBackdoor = false;

        /// <summary>
        /// The primary mouse capability detected
        /// </summary>
        public static MouseCapability PrimaryCapability => _primaryCapability;

        /// <summary>
        /// True if detection has been performed
        /// </summary>
        public static bool DetectionComplete => _detectionComplete;

        /// <summary>
        /// True if any mouse input is enabled
        /// </summary>
        public static bool MouseEnabled => _mouseEnabled;

        /// <summary>
        /// Detect and initialize mouse capabilities based on boot mode
        /// </summary>
        /// <param name="bootInfo">UEFI boot info (null for Legacy boot)</param>
        /// <param name="isLegacyBoot">True if Legacy/Multiboot boot</param>
        /// <param name="enablePS2Fallback">True to allow PS/2 fallback in Legacy mode</param>
        public static void DetectAndInitialize(UefiBootInfo* bootInfo, bool uefi, bool enablePS2Fallback = true) {
            DebugLog("[MouseCapability] Starting capability detection");
            DebugLog("[MouseCapability] Boot mode: " + (!uefi ? "Legacy" : "UEFI"));
            
            _detectionComplete = false;
            _mouseEnabled = false;
            _primaryCapability = MouseCapability.None;
            
            // DON'T re-initialize MouseInputManager - it was already initialized in EntryPoint
            // with the correct bootInfo. Re-initializing would lose the UEFI pointer provider.
            // if (bootInfo != null) {
            //     MouseInputManager.Initialize(bootInfo);
            // } else {
            //     MouseInputManager.Initialize();
            // }
            
            // Check if MouseInputManager already has a live UEFI provider registered.
            // Firmware pointer protocols are not usable after ExitBootServices.
            if (uefi && MouseInputManager.HasActiveUefiPointerProvider) {
                DebugLog("[MouseCapability] MouseInputManager already initialized");
                // The UEFI pointer was registered during EntryPoint initialization
                _hasUefiSimplePointer = true;
                _primaryCapability = MouseCapability.UefiSimplePointer;
                _mouseEnabled = true;
                DebugLog("[MouseCapability] Using pre-registered UEFI pointer");
            } else if (uefi && ExitBootServicesRules.HasExitedBootServices) {
                DebugLog("[MouseCapability] UEFI pointer unavailable after ExitBootServices");
            }

            // STEP 1: Check for UEFI pointer protocols (UEFI boot only) - skip if already set
            if (uefi && bootInfo != null && _primaryCapability == MouseCapability.None) {
                DetectUefiPointer(bootInfo);
            }

            // STEP 2: Check for VMware backdoor (works in both modes, but check early)
            if (_primaryCapability == MouseCapability.None) {
                DetectVMwareBackdoor();
            }

            // STEP 3: Check for USB HID mouse (both modes, after USB init)
            // Note: USB HID detection happens separately after USB stack init

            // STEP 4: Legacy PS/2 fallback (Legacy boot only, if enabled)
            if (!uefi && enablePS2Fallback && _primaryCapability == MouseCapability.None) {
                DetectAndInitializePS2();
            }

            // STEP 5: Determine final capability
            DeterminePrimaryCapability();
            
            _detectionComplete = true;
            
            // Log final result
            LogDetectionResult();
        }

        /// <summary>
        /// Detect UEFI pointer protocols
        /// </summary>
        private static void DetectUefiPointer(UefiBootInfo* bootInfo) {
            DebugLog("[MouseCapability] Checking UEFI pointer protocols...");
            
            // Check Simple Pointer Protocol
            if (bootInfo->HasSimplePointer && bootInfo->SimplePointerProtocol != 0) {
                DebugLog("[MouseCapability] UEFI Simple Pointer protocol available");
                _hasUefiSimplePointer = true;
                
                // MouseInputManager.Initialize(bootInfo) already registered the provider
                if (MouseInputManager.IsInitialized) {
                    _primaryCapability = MouseCapability.UefiSimplePointer;
                    _mouseEnabled = true;
                    DebugLog("[MouseCapability] UEFI Simple Pointer ENABLED");
                }
            } else {
                DebugLog("[MouseCapability] No UEFI Simple Pointer protocol");
            }
            
            // Check Absolute Pointer Protocol (touchscreen/tablet)
            if (bootInfo->HasAbsolutePointer && bootInfo->AbsolutePointerProtocol != 0) {
                DebugLog("[MouseCapability] UEFI Absolute Pointer protocol available");
                _hasUefiAbsolutePointer = true;
                // TODO: Initialize absolute pointer provider
            } else {
                DebugLog("[MouseCapability] No UEFI Absolute Pointer protocol");
            }
        }

        /// <summary>
        /// Detect VMware backdoor for absolute mouse positioning
        /// </summary>
        private static void DetectVMwareBackdoor() {
            DebugLog("[MouseCapability] Checking VMware backdoor...");
            
            // VMwareTools.Available is set during VMwareTools.Initialize()
            // which should have been called earlier in boot
            if (VMwareTools.Available) {
                DebugLog("[MouseCapability] VMware backdoor AVAILABLE");
                _hasVMwareBackdoor = true;
                
                // VMware backdoor provides absolute positioning
                // It's handled separately from the MouseInputManager
                if (_primaryCapability == MouseCapability.None) {
                    _primaryCapability = MouseCapability.VMwareBackdoor;
                    _mouseEnabled = true;
                    DebugLog("[MouseCapability] VMware backdoor ENABLED as primary");
                } else {
                    DebugLog("[MouseCapability] VMware backdoor available but not primary");
                }
            } else {
                DebugLog("[MouseCapability] VMware backdoor not available");
            }
        }

        /// <summary>
        /// Detect and initialize legacy PS/2 mouse (Legacy boot only)
        /// Uses the LegacyBiosMouse module which enforces safety checks.
        /// </summary>
        private static void DetectAndInitializePS2() {
            DebugLog("[MouseCapability] Checking PS/2 via LegacyBiosMouse module...");
            
            // Only attempt PS/2 in Legacy mode
            if (BootConsole.CurrentMode != guideXOS.BootMode.Legacy) {
                DebugLog("[MouseCapability] PS/2 skipped - not in Legacy mode");
                return;
            }
            
            // Use the LegacyBiosMouse module which enforces safety checks
            // This ensures PS/2 hardware is only accessed when appropriate
            
            // Step 1: Enable the legacy module with safety acknowledgment
            DebugLog("[MouseCapability] Enabling LegacyBiosMouse module...");
            bool enabled = LegacyBiosMouse.Enable(
                acknowledgeRisks: true,  // We acknowledge this is legacy hardware access
                forceLegacyMode: false   // Don't force - let it verify boot mode
            );
            
            if (!enabled) {
                DebugLog("[MouseCapability] LegacyBiosMouse.Enable() failed");
                DebugLog("[MouseCapability]   Reason: " + LegacyBiosMouse.FailureReason);
                return;
            }
            
            _hasPS2Controller = true;
            
            // Step 2: Initialize through the legacy module
            DebugLog("[MouseCapability] Initializing PS/2 mouse via LegacyBiosMouse...");
            bool initialized = LegacyBiosMouse.Initialize();
            
            if (!initialized) {
                DebugLog("[MouseCapability] LegacyBiosMouse.Initialize() failed");
                DebugLog("[MouseCapability]   Reason: " + LegacyBiosMouse.FailureReason);
                // Disable the module since init failed
                LegacyBiosMouse.Disable();
                return;
            }
            
            // Success - PS/2 mouse is now active
            _primaryCapability = MouseCapability.LegacyPS2Mouse;
            _mouseEnabled = true;
            DebugLog("[MouseCapability] Legacy PS/2 mouse ENABLED via LegacyBiosMouse module");
            
            // Enable full processing
            LegacyBiosMouse.EnableFullProcessing();
        }

        /// <summary>
        /// Called after USB stack is initialized to check for USB HID mouse
        /// </summary>
        public static void CheckUsbHidMouse() {
            DebugLog("[MouseCapability] Checking USB HID mouse...");
            
            if (HID.Mouse != null) {
                DebugLog("[MouseCapability] USB HID mouse DETECTED");
                _hasUsbHid = true;
                
                // If no higher-priority mouse is active, use USB HID
                if (_primaryCapability == MouseCapability.None || 
                    _primaryCapability == MouseCapability.KeyboardEmulation) {
                    _primaryCapability = MouseCapability.UsbHidMouse;
                    _mouseEnabled = true;
                    DebugLog("[MouseCapability] USB HID mouse ENABLED as primary");
                } else {
                    DebugLog("[MouseCapability] USB HID mouse available but not primary");
                }
            } else {
                DebugLog("[MouseCapability] No USB HID mouse detected");
            }
        }

        /// <summary>
        /// Determine the primary capability based on what was detected
        /// </summary>
        private static void DeterminePrimaryCapability() {
            // Already set during detection, but verify we have something
            if (_primaryCapability == MouseCapability.None) {
                // In UEFI mode with no real hardware detected, do NOT fall back to
                // KeyboardEmulation and do NOT report mouse as enabled.  Keyboard
                // emulation is a legacy-input concept that requires PS/2 keyboard
                // processing which must not run in UEFI safe mode.  Leaving the
                // capability as None means mouse is correctly reported as disabled.
                if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                    // No real mouse capability available in UEFI mode.
                    // _primaryCapability stays None, _mouseEnabled stays false.
                    DebugLog("[MouseCapability] UEFI mode: no capability detected, leaving disabled");
                } else {
                    // Fall back to keyboard emulation in Legacy mode only
                    _primaryCapability = MouseCapability.KeyboardEmulation;
                    _mouseEnabled = true;
                    DebugLog("[MouseCapability] Falling back to keyboard emulation");
                }
            }
        }

        /// <summary>
        /// Log the final detection result
        /// </summary>
        private static void LogDetectionResult() {
            DebugLog("[MouseCapability] ========================================");
            DebugLog("[MouseCapability] Detection complete");
            DebugLog("[MouseCapability] Primary: " + GetCapabilityName(_primaryCapability));
            DebugLog("[MouseCapability] Mouse enabled: " + _mouseEnabled);
            DebugLog("[MouseCapability] ----------------------------------------");
            DebugLog("[MouseCapability] Capabilities detected:");
            if (_hasUefiSimplePointer) DebugLog("[MouseCapability]   - UEFI Simple Pointer");
            if (_hasUefiAbsolutePointer) DebugLog("[MouseCapability]   - UEFI Absolute Pointer");
            if (_hasUsbHid) DebugLog("[MouseCapability]   - USB HID Mouse");
            if (_hasPS2Controller) DebugLog("[MouseCapability]   - Legacy PS/2 Controller");
            if (_hasVMwareBackdoor) DebugLog("[MouseCapability]   - VMware Backdoor");
            DebugLog("[MouseCapability] ========================================");
        }

        /// <summary>
        /// Get human-readable capability name
        /// </summary>
        public static string GetCapabilityName(MouseCapability cap) {
            switch (cap) {
                case MouseCapability.None: return "None";
                case MouseCapability.UefiSimplePointer: return "UEFI Simple Pointer";
                case MouseCapability.UefiAbsolutePointer: return "UEFI Absolute Pointer";
                case MouseCapability.UsbHidMouse: return "USB HID Mouse";
                case MouseCapability.LegacyPS2Mouse: return "Legacy PS/2 Mouse";
                case MouseCapability.VMwareBackdoor: return "VMware Backdoor";
                case MouseCapability.KeyboardEmulation: return "Keyboard Emulation";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Get diagnostic string
        /// </summary>
        public static string GetDiagnostics() {
            string diag = "[MouseCapabilityDetector Diagnostics]\n";
            diag += "  Detection Complete: " + _detectionComplete + "\n";
            diag += "  Mouse Enabled: " + _mouseEnabled + "\n";
            diag += "  Primary Capability: " + GetCapabilityName(_primaryCapability) + "\n";
            diag += "  Capabilities:\n";
            diag += "    UEFI Simple Pointer: " + _hasUefiSimplePointer + "\n";
            diag += "    UEFI Absolute Pointer: " + _hasUefiAbsolutePointer + "\n";
            diag += "    USB HID: " + _hasUsbHid + "\n";
            diag += "    PS/2 Controller: " + _hasPS2Controller + "\n";
            diag += "    VMware Backdoor: " + _hasVMwareBackdoor + "\n";
            
            // Include LegacyBiosMouse diagnostics if PS/2 was detected
            if (_hasPS2Controller) {
                diag += "\n";
                diag += LegacyBiosMouse.GetDiagnostics();
            }
            
            return diag;
        }

        /// <summary>
        /// Debug logging helper
        /// </summary>
        private static void DebugLog(string message) {
            // In UEFI mode BootConsole already writes to serial. Avoid duplicate output
            // during fragile post-ExitBootServices bring-up.
            try {
                BootConsole.WriteLine(message);
            } catch {
                // BootConsole might not be available
            }

            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) return;
            
            // Serial output
            if (message == null) return;
            try {
                for (int i = 0; i < message.Length; i++) {
                    char c = message[i];
                    while ((Native.In8(0x3FD) & 0x20) == 0) { }
                    Native.Out8(0x3F8, (byte)c);
                }
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'\n');
            } catch {
                // Ignore serial errors
            }
        }
    }
}
