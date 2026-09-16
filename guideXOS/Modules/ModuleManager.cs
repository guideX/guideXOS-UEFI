using guideXOS.GUI;

namespace guideXOS.Modules {
    /// <summary>
    /// Module registry for lightweight pluggable components.
    /// Uses delegates instead of interfaces to avoid NativeAOT dispatch issues.
    /// </summary>
    public static class ModuleManager {
        private struct ModuleEntry {
            public string Name;
            public System.Action LaunchAction;
        }

        private static readonly System.Collections.Generic.List<ModuleEntry> _modules = new();

        public static void Register(string name, System.Action launchAction) {
            // Prevent duplicates
            for (int i = 0; i < _modules.Count; i++) {
                if (_modules[i].Name == name) return;
            }
            
            _modules.Add(new ModuleEntry { Name = name, LaunchAction = launchAction });
        }

        public static bool Launch(string name) {
            for (int i = 0; i < _modules.Count; i++) {
                if (_modules[i].Name == name) {
                    _modules[i].LaunchAction?.Invoke();
                    return true;
                }
            }
            return false;
        }

        public static int Count => _modules.Count;
        
        public static string Name(int index) => _modules[index].Name;

        /// <summary>
        /// Register built-in modules.
        /// </summary>
        public static void InitializeBuiltins() {
            // Register notepad module with a simple lambda
            Register("NotepadModule", () => {
                Desktop.LaunchApplication("Notepad");
            });
            
            // Add more modules here as needed
            // Register("CalculatorModule", () => { ... });
            // Register("PaintModule", () => { ... });
        }
    }
}
