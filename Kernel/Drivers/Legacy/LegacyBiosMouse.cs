using guideXOS.Misc;
using System;

namespace guideXOS.Kernel.Drivers.Legacy {
    /// <summary>
    /// Legacy BIOS-Era PS/2 Mouse Module
    /// 
    /// ============================================================================
    /// ??  WARNING: LEGACY HARDWARE ACCESS - READ BEFORE USE  ??
    /// ============================================================================
    /// 
    /// This module provides access to legacy PS/2 mouse hardware via the i8042
    /// controller. It is DISABLED BY DEFAULT and should ONLY be enabled when
    /// ALL of the following conditions are met:
    /// 
    /// VALID USE CASES:
    /// ----------------
    /// 1. Legacy BIOS boot (NOT UEFI)
    ///    - System was booted via traditional BIOS/Multiboot
    ///    - BIOS has initialized the i8042 PS/2 controller
    ///    - Legacy PIC (8259) is active and configured
    /// 
    /// 2. Hardware has physical PS/2 ports
    ///    - Desktop/laptop with PS/2 mouse port
    ///    - PS/2 mouse is physically connected
    ///    - Not USB-only hardware
    /// 
    /// 3. Virtual machines with PS/2 emulation
    ///    - VirtualBox (default PS/2 mouse)
    ///    - QEMU with -device i8042 (default)
    ///    - VMware (has PS/2 emulation but prefer VMware Tools)
    /// 
    /// INVALID USE CASES (DO NOT ENABLE):
    /// -----------------------------------
    /// 1. UEFI boot mode
    ///    - UEFI does NOT initialize i8042 controller
    ///    - UEFI systems use EFI_SIMPLE_POINTER_PROTOCOL
    ///    - Direct port I/O may hang or crash
    /// 
    /// 2. Modern USB-only systems
    ///    - Many laptops have no PS/2 controller
    ///    - USB keyboards/mice use HID protocol
    ///    - Port 0x60/0x64 may not be mapped
    /// 
    /// 3. Systems with APIC-only interrupt routing
    ///    - Legacy PIC may be disabled
    ///    - IRQ12 may not be routed to PS/2
    /// 
    /// HARDWARE ACCESS:
    /// ----------------
    /// This module accesses the following hardware when enabled:
    /// - Port 0x60: i8042 data port (read/write mouse data)
    /// - Port 0x64: i8042 command/status port
    /// - IRQ12 (vector 0x2C): PS/2 mouse interrupt
    /// - Commands: 0xA8 (enable aux), 0xD4 (write to mouse)
    /// 
    /// SAFETY GUARANTEES:
    /// ------------------
    /// - Hardware access is BLOCKED unless explicitly enabled
    /// - Enable requires explicit call with safety acknowledgment
    /// - All hardware access methods check IsEnabled flag
    /// - Attempting to use without enabling logs error and returns safely
    /// 
    /// RECOMMENDED ALTERNATIVES:
    /// -------------------------
    /// - UEFI boot: Use MouseInputManager with UefiPointerProvider
    /// - USB mice: Use HID.cs USB HID mouse driver
    /// - VMware: Use VMwareTools absolute pointer backdoor
    /// - All modes: MouseCapabilityDetector handles selection automatically
    /// 
    /// ============================================================================
    /// </summary>
    public static unsafe class LegacyBiosMouse {
        
        #region Safety State
        
        /// <summary>
        /// Whether legacy PS/2 mouse access is enabled.
        /// DISABLED BY DEFAULT - must be explicitly enabled.
        /// </summary>
        private static bool _isEnabled = false;
        
        /// <summary>
        /// Whether the safety acknowledgment has been provided.
        /// Required to enable hardware access.
        /// </summary>
        private static bool _safetyAcknowledged = false;
        
        /// <summary>
        /// Whether initialization has been attempted.
        /// </summary>
        private static bool _initializationAttempted = false;
        
        /// <summary>
        /// Whether initialization succeeded.
        /// </summary>
        private static bool _initializationSucceeded = false;
        
        /// <summary>
        /// Reason if initialization failed.
        /// </summary>
        private static string _initializationFailureReason = "";
        
        #endregion
        
        #region Public Properties
        
        /// <summary>
        /// True if legacy PS/2 mouse access is enabled and initialized.
        /// </summary>
        public static bool IsEnabled => _isEnabled && _initializationSucceeded;
        
        /// <summary>
        /// True if initialization was attempted (regardless of success).
        /// </summary>
        public static bool InitializationAttempted => _initializationAttempted;
        
        /// <summary>
        /// True if initialization succeeded.
        /// </summary>
        public static bool InitializationSucceeded => _initializationSucceeded;
        
        /// <summary>
        /// Reason for initialization failure (empty if succeeded).
        /// </summary>
        public static string FailureReason => _initializationFailureReason;
        
        #endregion
        
        #region Enable/Disable
        
        /// <summary>
        /// Enable legacy PS/2 mouse hardware access.
        /// 
        /// REQUIREMENTS:
        /// 1. Must be in Legacy boot mode (not UEFI)
        /// 2. Must acknowledge safety warning
        /// 3. PS/2 controller must be detected
        /// 
        /// </summary>
        /// <param name="acknowledgeRisks">
        /// Must be true to acknowledge that you understand:
        /// - This accesses legacy hardware directly
        /// - May hang on UEFI systems
        /// - May conflict with other input drivers
        /// </param>
        /// <param name="forceLegacyMode">
        /// If true, skip the boot mode check. USE WITH EXTREME CAUTION.
        /// Only for debugging/testing when you know PS/2 hardware exists.
        /// </param>
        /// <returns>True if successfully enabled, false otherwise.</returns>
        public static bool Enable(bool acknowledgeRisks, bool forceLegacyMode = false) {
            LogDebug("[LegacyBiosMouse] Enable() called");
            LogDebug("[LegacyBiosMouse]   acknowledgeRisks: " + acknowledgeRisks);
            LogDebug("[LegacyBiosMouse]   forceLegacyMode: " + forceLegacyMode);
            
            // Check safety acknowledgment
            if (!acknowledgeRisks) {
                LogDebug("[LegacyBiosMouse] ERROR: Risks not acknowledged");
                _initializationFailureReason = "Safety risks not acknowledged";
                return false;
            }
            _safetyAcknowledged = true;
            
            // Check boot mode (unless forced)
            if (!forceLegacyMode) {
                if (BootConsole.CurrentMode != guideXOS.BootMode.Legacy) {
                    LogDebug("[LegacyBiosMouse] ERROR: Not in Legacy boot mode");
                    LogDebug("[LegacyBiosMouse]   Current mode: " + BootConsole.CurrentMode);
                    _initializationFailureReason = "Not in Legacy boot mode (UEFI detected)";
                    return false;
                }
            } else {
                LogDebug("[LegacyBiosMouse] WARNING: Force legacy mode - skipping boot mode check");
            }
            
            // Check if PS/2 controller appears to exist
            if (!ProbePS2Controller()) {
                LogDebug("[LegacyBiosMouse] ERROR: PS/2 controller not detected");
                _initializationFailureReason = "PS/2 controller not detected at ports 0x60/0x64";
                return false;
            }
            
            // All checks passed - enable hardware access
            _isEnabled = true;
            LogDebug("[LegacyBiosMouse] Hardware access ENABLED");
            
            return true;
        }
        
        /// <summary>
        /// Disable legacy PS/2 mouse hardware access.
        /// Safe to call at any time.
        /// </summary>
        public static void Disable() {
            LogDebug("[LegacyBiosMouse] Disable() called");
            _isEnabled = false;
            // Don't reset _initializationSucceeded - keep that state for diagnostics
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize the legacy PS/2 mouse driver.
        /// 
        /// PREREQUISITES:
        /// - Must call Enable() first with acknowledgeRisks=true
        /// - Must be in appropriate boot mode
        /// 
        /// This delegates to PS2Mouse.Initialize() but with safety checks.
        /// </summary>
        /// <returns>True if initialization succeeded.</returns>
        public static bool Initialize() {
            LogDebug("[LegacyBiosMouse] Initialize() called");
            
            _initializationAttempted = true;
            _initializationSucceeded = false;
            _initializationFailureReason = "";
            
            // Check if enabled
            if (!_isEnabled) {
                LogDebug("[LegacyBiosMouse] ERROR: Not enabled - call Enable() first");
                _initializationFailureReason = "Hardware access not enabled - call Enable() first";
                return false;
            }
            
            // Check safety acknowledgment
            if (!_safetyAcknowledged) {
                LogDebug("[LegacyBiosMouse] ERROR: Safety not acknowledged");
                _initializationFailureReason = "Safety risks not acknowledged";
                return false;
            }
            
            // Attempt initialization
            try {
                LogDebug("[LegacyBiosMouse] Calling PS2Controller.Initialize()...");
                bool initialized = PS2Controller.Initialize();
                if (!initialized || !PS2Mouse.IsNativeInitialized) {
                    _initializationFailureReason = "Native PS/2 initialization was not acknowledged";
                    LogDebug("[LegacyBiosMouse] PS2Controller initialization failed");
                    return false;
                }
                LogDebug("[LegacyBiosMouse] PS2Controller initialized");
                
                _initializationSucceeded = true;
                LogDebug("[LegacyBiosMouse] Initialization SUCCEEDED");
                return true;
            } catch {
                _initializationFailureReason = "Exception during initialization";
                LogDebug("[LegacyBiosMouse] Initialization FAILED");
                return false;
            }
        }
        
        /// <summary>
        /// Enable full mouse processing after boot is complete.
        /// Only works if initialized successfully.
        /// </summary>
        public static void EnableFullProcessing() {
            if (!IsEnabled) {
                LogDebug("[LegacyBiosMouse] EnableFullProcessing() ignored - not enabled");
                return;
            }
            
            PS2Mouse.EnableFullProcessing();
            LogDebug("[LegacyBiosMouse] Full processing enabled");
        }
        
        #endregion
        
        #region Hardware Probing
        
        /// <summary>
        /// Probe for PS/2 controller existence.
        /// Does NOT access mouse-specific functions, just checks controller.
        /// </summary>
        /// <returns>True if controller appears to exist.</returns>
        private static bool ProbePS2Controller() {
            LogDebug("[LegacyBiosMouse] Probing PS/2 controller...");
            
            try {
                // Read status register from port 0x64
                // If controller doesn't exist, this might return 0xFF or cause issues
                byte status = Native.In8(0x64);
                
                LogDebug("[LegacyBiosMouse]   Status register: 0x" + status.ToString("X2"));
                
                // 0xFF usually indicates no device present
                if (status == 0xFF) {
                    LogDebug("[LegacyBiosMouse]   Status is 0xFF - no controller");
                    return false;
                }
                
                // Check for some reasonable status bits
                // Bit 2 (System Flag) should typically be set after POST
                // Bit 4 (Keyboard Lock) varies
                // We'll accept any status that isn't 0xFF as "possibly present"
                
                LogDebug("[LegacyBiosMouse]   Controller appears present");
                return true;
            } catch {
                LogDebug("[LegacyBiosMouse]   Exception during probe - no controller");
                return false;
            }
        }
        
        #endregion
        
        #region Diagnostics
        
        /// <summary>
        /// Get diagnostic information about the legacy mouse module.
        /// </summary>
        public static string GetDiagnostics() {
            string diag = "============================================\n";
            diag += "[LegacyBiosMouse Diagnostics]\n";
            diag += "============================================\n";
            diag += "  Enabled: " + _isEnabled + "\n";
            diag += "  Safety Acknowledged: " + _safetyAcknowledged + "\n";
            diag += "  Initialization Attempted: " + _initializationAttempted + "\n";
            diag += "  Initialization Succeeded: " + _initializationSucceeded + "\n";
            
            if (!string.IsNullOrEmpty(_initializationFailureReason)) {
                diag += "  Failure Reason: " + _initializationFailureReason + "\n";
            }
            
            diag += "\n";
            diag += "  Boot Mode: " + BootConsole.CurrentMode + "\n";
            diag += "  Is Legacy Boot: " + (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) + "\n";
            
            if (_initializationSucceeded) {
                diag += "\n";
                diag += "  PS2Mouse Stats:\n";
                diag += "    Interrupt Count: " + PS2Mouse.InterruptCount + "\n";
                diag += "    Packet Count: " + PS2Mouse.PacketCount + "\n";
                diag += "    DeltaZ: " + PS2Mouse.DeltaZ + "\n";
            }
            
            diag += "============================================\n";
            return diag;
        }
        
        /// <summary>
        /// Log message to serial port for debugging.
        /// </summary>
        private static void LogDebug(string message) {
            if (message == null) return;
            
            try {
                // Also log to BootConsole if available
                try {
                    BootConsole.WriteLine(message);
                } catch {
                    // BootConsole might not be available
                }
                
                // Serial output
                for (int i = 0; i < message.Length; i++) {
                    char c = message[i];
                    while ((Native.In8(0x3FD) & 0x20) == 0) { }
                    Native.Out8(0x3F8, (byte)c);
                }
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'\n');
            } catch {
                // Ignore logging errors
            }
        }
        
        #endregion
    }
}
