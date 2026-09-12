using guideXOS.Misc;
using System;
using System.Windows.Forms;

namespace guideXOS.Kernel.Drivers.Input {
    /// <summary>
    /// Unified Mouse Event Dispatcher
    /// 
    /// ============================================================================
    /// SOURCE-AGNOSTIC EVENT DISPATCH
    /// ============================================================================
    /// 
    /// This class provides a single point of contact for ALL mouse input in
    /// GuideXOS. The rest of the system (GUI, windows, controls) should ONLY
    /// interact with mouse input through this dispatcher.
    /// 
    /// DESIGN GOALS:
    /// 1. Source Agnostic: GUI code never knows or cares where events come from
    /// 2. Unified Processing: All events go through the same filtering pipeline
    /// 3. Consistent State: Control.MousePosition/MouseButtons always up-to-date
    /// 4. Event History: Maintains recent events for gesture recognition
    /// 5. Diagnostics: Tracks which source is active and event statistics
    /// 
    /// USAGE:
    /// - Main loop calls MouseEventDispatcher.Update() once per frame
    /// - GUI code reads Control.MousePosition and Control.MouseButtons
    /// - Advanced code can subscribe to OnMouseEvent for raw events
    /// 
    /// ============================================================================
    /// </summary>
    public static unsafe class MouseEventDispatcher {
        
        #region Configuration
        
        /// <summary>
        /// Enable/disable input filtering (noise reduction, clamping)
        /// </summary>
        public static bool EnableFiltering = true;
        
        /// <summary>
        /// Sensitivity multiplier for relative movement
        /// </summary>
        public static float Sensitivity = 1.0f;
        
        /// <summary>
        /// Minimum delta to register as movement (noise filter)
        /// </summary>
        public static int NoiseThreshold = 2;
        
        /// <summary>
        /// Maximum delta per update (prevents jumps from corrupted data)
        /// </summary>
        public static int MaxDeltaPerUpdate = 100;
        
        #endregion
        
        #region State
        
        private static bool _initialized = false;
        
        // Last processed event for diagnostics
        private static MouseEvent _lastEvent = MouseEvent.Empty;
        
        // Statistics
        private static ulong _totalEventsProcessed = 0;
        private static ulong _totalEventsFiltered = 0;
        private static MouseInputSource _lastActiveSource = MouseInputSource.Unknown;
        
        // Event history for gesture recognition (circular buffer)
        private const int EVENT_HISTORY_SIZE = 16;
        private static MouseEvent[] _eventHistory;
        private static int _eventHistoryIndex = 0;
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize the event dispatcher
        /// </summary>
        public static void Initialize() {
            if (_initialized) return;
            
            _eventHistory = new MouseEvent[EVENT_HISTORY_SIZE];
            for (int i = 0; i < EVENT_HISTORY_SIZE; i++) {
                _eventHistory[i] = MouseEvent.Empty;
            }
            
            _eventHistoryIndex = 0;
            _totalEventsProcessed = 0;
            _totalEventsFiltered = 0;
            _lastActiveSource = MouseInputSource.Unknown;
            _lastEvent = MouseEvent.Empty;
            
            _initialized = true;
            
            DebugLog("[MouseEventDispatcher] Initialized");
        }
        
        #endregion
        
        #region Event Processing
        
        /// <summary>
        /// Update mouse state from all available sources
        /// Call this once per frame in the main loop
        /// </summary>
        public static void Update() {
            if (!_initialized) Initialize();

            if (!MouseInputManager.HasAnyActiveProvider) {
                return;
            }
            
            // Poll MouseInputManager for events from all providers
            MouseInputManager.Poll();
        }
        
        /// <summary>
        /// Process a mouse event from any source
        /// This is the central dispatch point - all events flow through here
        /// </summary>
        /// <param name="evt">The mouse event to process</param>
        public static void DispatchEvent(MouseEvent evt) {
            if (!_initialized) Initialize();
            if (!evt.IsValid) return;
            
            _totalEventsProcessed++;
            _lastActiveSource = evt.Source;
            
            // Debug: Log incoming event
            if (evt.IsAbsolute) {
                MouseDebug.LogAbsolutePosition(evt.AbsoluteX, evt.AbsoluteY, evt.Buttons, evt.Source);
            } else {
                MouseDebug.LogStateChange(evt.DeltaX, evt.DeltaY, evt.DeltaZ, evt.Buttons, evt.Source);
            }
            
            // Apply filtering if enabled
            if (EnableFiltering) {
                evt = ApplyFiltering(evt);
                if (!evt.IsValid) {
                    _totalEventsFiltered++;
                    return;
                }
            }
            
            // Store in history
            _eventHistory[_eventHistoryIndex] = evt;
            _eventHistoryIndex = (_eventHistoryIndex + 1) % EVENT_HISTORY_SIZE;
            
            // Update global state
            UpdateGlobalState(evt);
            
            // Store as last event
            _lastEvent = evt;
        }
        
        /// <summary>
        /// Apply filtering to an event (noise reduction, clamping)
        /// </summary>
        private static MouseEvent ApplyFiltering(MouseEvent evt) {
            if (evt.IsAbsolute) {
                // Absolute events don't need delta filtering
                // Just validate the coordinates are in range
                evt.AbsoluteX = Math.Clamp(evt.AbsoluteX, 0, 65535);
                evt.AbsoluteY = Math.Clamp(evt.AbsoluteY, 0, 65535);
                return evt;
            }
            
            // Apply sensitivity
            int deltaX = (int)(evt.DeltaX * Sensitivity);
            int deltaY = (int)(evt.DeltaY * Sensitivity);
            
            // Noise filtering - ignore very small movements
            if (Math.Abs(deltaX) < NoiseThreshold) deltaX = 0;
            if (Math.Abs(deltaY) < NoiseThreshold) deltaY = 0;
            
            // Clamp extreme values
            deltaX = Math.Clamp(deltaX, -MaxDeltaPerUpdate, MaxDeltaPerUpdate);
            deltaY = Math.Clamp(deltaY, -MaxDeltaPerUpdate, MaxDeltaPerUpdate);
            
            // If both deltas are zero and no buttons changed, filter out
            if (deltaX == 0 && deltaY == 0 && evt.DeltaZ == 0) {
                // Still valid if buttons are pressed (button-only event)
                if (evt.Buttons == MouseButtons.None && _lastEvent.Buttons == MouseButtons.None) {
                    evt.IsValid = false;
                    return evt;
                }
            }
            
            evt.DeltaX = deltaX;
            evt.DeltaY = deltaY;
            
            return evt;
        }
        
        /// <summary>
        /// Update Control.MousePosition and Control.MouseButtons
        /// </summary>
        private static void UpdateGlobalState(MouseEvent evt) {
            int newX, newY;
            
            if (evt.IsAbsolute) {
                // Absolute positioning - scale from 0-65535 to screen coordinates
                int screenWidth = Framebuffer.Width;
                int screenHeight = Framebuffer.Height;
                
                newX = (evt.AbsoluteX * screenWidth) / 65536;
                newY = (evt.AbsoluteY * screenHeight) / 65536;
            } else {
                // Relative positioning - add deltas to current position
                newX = Control.MousePosition.X + evt.DeltaX;
                newY = Control.MousePosition.Y + evt.DeltaY;
            }
            
            // Clamp to screen bounds
            newX = Math.Clamp(newX, 0, Framebuffer.Width);
            newY = Math.Clamp(newY, 0, Framebuffer.Height);
            
            // Update global state
            Control.MousePosition.X = newX;
            Control.MousePosition.Y = newY;
            Control.MouseButtons = evt.Buttons;
            
            // Update PS2Mouse.DeltaZ for scroll wheel compatibility
            PS2Mouse.DeltaZ = evt.DeltaZ;
        }
        
        #endregion
        
        #region Diagnostics
        
        /// <summary>
        /// Get the last processed event
        /// </summary>
        public static MouseEvent LastEvent => _lastEvent;
        
        /// <summary>
        /// Get the last active input source
        /// </summary>
        public static MouseInputSource LastActiveSource => _lastActiveSource;
        
        /// <summary>
        /// Total events processed
        /// </summary>
        public static ulong TotalEventsProcessed => _totalEventsProcessed;
        
        /// <summary>
        /// Total events filtered out (noise, etc.)
        /// </summary>
        public static ulong TotalEventsFiltered => _totalEventsFiltered;
        
        /// <summary>
        /// Get recent event history for gesture analysis
        /// </summary>
        /// <param name="count">Number of recent events to retrieve (max 16)</param>
        /// <returns>Array of recent events, newest first</returns>
        public static MouseEvent[] GetRecentEvents(int count) {
            if (!_initialized) Initialize();
            
            if (count > EVENT_HISTORY_SIZE) count = EVENT_HISTORY_SIZE;
            
            MouseEvent[] result = new MouseEvent[count];
            int idx = (_eventHistoryIndex - 1 + EVENT_HISTORY_SIZE) % EVENT_HISTORY_SIZE;
            
            for (int i = 0; i < count; i++) {
                result[i] = _eventHistory[idx];
                idx = (idx - 1 + EVENT_HISTORY_SIZE) % EVENT_HISTORY_SIZE;
            }
            
            return result;
        }
        
        /// <summary>
        /// Get diagnostic string
        /// </summary>
        public static string GetDiagnostics() {
            string diag = "[MouseEventDispatcher Diagnostics]\n";
            diag += "  Initialized: " + _initialized + "\n";
            diag += "  Filtering Enabled: " + EnableFiltering + "\n";
            diag += "  Sensitivity: " + Sensitivity + "\n";
            diag += "  Noise Threshold: " + NoiseThreshold + "\n";
            diag += "  Max Delta: " + MaxDeltaPerUpdate + "\n";
            diag += "\n";
            diag += "  Statistics:\n";
            diag += "    Events Processed: " + _totalEventsProcessed + "\n";
            diag += "    Events Filtered: " + _totalEventsFiltered + "\n";
            diag += "    Last Source: " + _lastEvent.GetSourceName() + "\n";
            diag += "\n";
            diag += "  Last Event:\n";
            if (_lastEvent.IsValid) {
                diag += "    Source: " + _lastEvent.GetSourceName() + "\n";
                diag += "    IsAbsolute: " + _lastEvent.IsAbsolute + "\n";
                if (_lastEvent.IsAbsolute) {
                    diag += "    AbsoluteX: " + _lastEvent.AbsoluteX + "\n";
                    diag += "    AbsoluteY: " + _lastEvent.AbsoluteY + "\n";
                } else {
                    diag += "    DeltaX: " + _lastEvent.DeltaX + "\n";
                    diag += "    DeltaY: " + _lastEvent.DeltaY + "\n";
                }
                diag += "    DeltaZ: " + _lastEvent.DeltaZ + "\n";
                diag += "    Buttons: " + _lastEvent.Buttons + "\n";
                diag += "    Timestamp: " + _lastEvent.Timestamp + "\n";
            } else {
                diag += "    (no valid event)\n";
            }
            
            // Include debug statistics
            diag += "\n";
            diag += MouseDebug.GetStatistics();
            
            return diag;
        }
        
        /// <summary>
        /// Reset statistics counters
        /// </summary>
        public static void ResetStatistics() {
            _totalEventsProcessed = 0;
            _totalEventsFiltered = 0;
        }
        
        #endregion
        
        #region Helpers
        
        /// <summary>
        /// Debug logging helper
        /// </summary>
        private static void DebugLog(string message) {
            if (message == null) return;
            
            try {
                BootConsole.WriteLine(message);
            } catch {
                // BootConsole might not be available
            }
            
            try {
                for (int i = 0; i < message.Length; i++) {
                    char c = message[i];
                    if (!TryWriteSerial((byte)c)) break;
                }
                TryWriteSerial((byte)'\n');
            } catch {
                // Ignore serial errors
            }
        }

        private static bool TryWriteSerial(byte value) {
            for (int wait = 0; wait < 100000; wait++) {
                if ((Native.In8(0x3FD) & 0x20) != 0) {
                    Native.Out8(0x3F8, value);
                    return true;
                }
            }
            return false;
        }
        
        #endregion
    }
}
