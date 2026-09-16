using System.Collections.Generic;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.DefaultApps;

namespace guideXOS.OS {
    /// <summary>
    /// Defines the operational modes for the firewall.
    /// </summary>
    internal enum FirewallMode { 
        /// <summary>
        /// Normal mode - requires user approval for new network connections.
        /// </summary>
        Normal, 
        /// <summary>
        /// Block all network connections.
        /// </summary>
        BlockAll, 
        /// <summary>
        /// Firewall is disabled, all connections are allowed.
        /// </summary>
        Disabled, 
        /// <summary>
        /// Automatically learn and allow new connections without prompting.
        /// </summary>
        Autolearn 
    }

    /// <summary>
    /// Manages network firewall rules and connection permissions.
    /// </summary>
    internal static class Firewall {
        private static List<string> _exceptions = new List<string>(64);
        private static List<string> _pendingAlerts = new List<string>(16); // program|action
        
        /// <summary>
        /// Gets or sets the current firewall mode.
        /// </summary>
        public static FirewallMode Mode = FirewallMode.Normal;
        
        /// <summary>
        /// Gets or sets the reference to the firewall configuration window.
        /// </summary>
        public static FirewallWindow Window;

        private static bool ListHas(List<string> list, string value) { for (int i = 0; i < list.Count; i++) if (list[i] == value) return true; return false; }

        /// <summary>
        /// Initializes the firewall system and creates the configuration window.
        /// </summary>
        public static void Initialize() { EnsureWindow(); }

        /// <summary>
        /// Return the persistent helper window without making it the owner of
        /// an application instance.  The registered Firewall factory attaches
        /// the helper when the user launches the configuration application.
        /// </summary>
        internal static FirewallWindow EnsureWindow() {
            if (Window == null || WindowManager.Windows == null ||
                    WindowManager.Windows.IndexOf(Window) < 0) {
                Window = new FirewallWindow(220, 140);
                Window.Visible = false;
            }
            return Window;
        }
        
        /// <summary>
        /// Gets an array of all programs that are allowed through the firewall.
        /// </summary>
        public static string[] Exceptions { get { var arr = new string[_exceptions.Count]; for (int i = 0; i < _exceptions.Count; i++) arr[i] = _exceptions[i]; return arr; } }
        
        /// <summary>
        /// Adds a program to the firewall exception list.
        /// </summary>
        /// <param name="name">The name of the program to allow.</param>
        public static void AddException(string name) { if (!ListHas(_exceptions, name)) _exceptions.Add(name); }
        
        /// <summary>
        /// Checks if a program is in the exception list.
        /// </summary>
        /// <param name="name">The name of the program to check.</param>
        /// <returns>True if the program is allowed, otherwise false.</returns>
        public static bool IsException(string name) { return ListHas(_exceptions, name); }
        
        /// <summary>
        /// Clears all pending firewall alerts.
        /// </summary>
        public static void ClearAlerts() { _pendingAlerts.Clear(); }
        
        /// <summary>
        /// Checks if a program is allowed to perform a network action.
        /// </summary>
        /// <param name="program">The name of the program requesting access.</param>
        /// <param name="action">The network action being requested.</param>
        /// <returns>True if the action is allowed, otherwise false.</returns>
        public static bool Check(string program, string action) {
            if (Mode == FirewallMode.Disabled) return true;
            if (Mode == FirewallMode.BlockAll) { QueueAlert(program, action); return false; }
            if (IsException(program)) return true;
            if (Mode == FirewallMode.Autolearn) { AddException(program); return true; }
            QueueAlert(program, action); return false;
        }
        
        private static void QueueAlert(string program, string action) { string key = program + "|" + action; if (!ListHas(_pendingAlerts, key)) { _pendingAlerts.Add(key); ShowAlert(program, action); } }
        private static void ShowAlert(string program, string action) { var alert = new FirewallAlert(program, action); WindowManager.MoveToEnd(alert); alert.Visible = true; }
        
        /// <summary>
        /// Removes a firewall alert from the pending alerts list.
        /// </summary>
        /// <param name="program">The name of the program.</param>
        /// <param name="action">The network action.</param>
        public static void RemoveAlert(string program, string action) { string key = program + "|" + action; for (int i = 0; i < _pendingAlerts.Count; i++) { if (_pendingAlerts[i] == key) { _pendingAlerts.RemoveAt(i); break; } } }
    }
}
