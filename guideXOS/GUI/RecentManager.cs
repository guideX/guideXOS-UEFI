using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System.Drawing;
using System.Collections.Generic;

namespace guideXOS.GUI {
    internal struct RecentProgramEntry {
        public string Name;
        public Image Icon;
        public ulong Ticks; // last used
    }
    internal struct RecentDocumentEntry {
        public string Path;
        public Image Icon;
        public ulong Ticks;
    }

    internal static class RecentManager {
        private const int MaxPrograms = 32;
        private const int MaxDocuments = 64;
        private static List<RecentProgramEntry> _programs = new List<RecentProgramEntry>();
        private static List<RecentDocumentEntry> _documents = new List<RecentDocumentEntry>();

        public static void AddProgram(string name, Image icon) {
#if UEFI_TASKMANAGER_MEMORY_R3_PROBE
            Allocator.MemoryR3Scope r3Scope1 = Allocator.EnterMemoryR3Scope(
                    TaskManagerMemoryR3Probe.SiteRecentProgramSnapshot,
                    0, Allocator.CurrentMemoryR3InstanceId,
                    Allocator.CurrentMemoryR3LifetimeId);
            try {
#endif
            if (name == null) return;
            // FIXED: Remove duplicate by name and dispose its icon
            for (int i = 0; i < _programs.Count; i++) {
                if (_programs[i].Name == name) {
                    // Dispose the old icon before removing
                    var oldEntry = _programs[i];
                    if (oldEntry.Icon != null) {
                        oldEntry.Icon.Dispose();
                    }
                    _programs.RemoveAt(i); 
                    break; 
                }
            }
            RecentProgramEntry e;
            e.Name = name;
            e.Icon = icon ?? Icons.DocumentIcon(32);
            e.Ticks = Timer.Ticks;
            _programs.Insert(0, e);
            // FIXED: Dispose oldest entry's icon when exceeding max
            if (_programs.Count > MaxPrograms) {
                var removed = _programs.ToArray()[_programs.Count - 1];
                if (removed.Icon != null) {
                    removed.Icon.Dispose();
                }
                _programs.RemoveAt(_programs.Count - 1);
            }
#if UEFI_TASKMANAGER_MEMORY_R3_PROBE
            } finally { r3Scope1.Dispose(); }
#endif
        }

        public static void AddDocument(string path, Image icon = null) {
            if (path == null) return;
            // FIXED: Remove duplicate and dispose its icon
            for (int i = 0; i < _documents.Count; i++) {
                if (_documents.ToArray()[i].Path == path) { 
                    // Dispose the old icon before removing
                    var oldEntry = _documents.ToArray()[i];
                    if (oldEntry.Icon != null) {
                        oldEntry.Icon.Dispose();
                    }
                    _documents.RemoveAt(i); 
                    break; 
                }
            }
            RecentDocumentEntry d;
            d.Path = path;
            d.Icon = icon ?? Icons.DocumentIcon(32);
            d.Ticks = Timer.Ticks;
            _documents.Insert(0, d);
            // FIXED: Dispose oldest entry's icon when exceeding max
            if (_documents.Count > MaxDocuments) {
                var removed = _documents.ToArray()[_documents.Count - 1];
                if (removed.Icon != null) {
                    removed.Icon.Dispose();
                }
                _documents.RemoveAt(_documents.Count - 1);
            }
        }

        public static List<RecentProgramEntry> Programs { get { return _programs; } }
        public static List<RecentDocumentEntry> Documents { get { return _documents; } }
        
        /// <summary>
        /// FIXED: Added cleanup method to dispose all cached icons
        /// </summary>
        public static new void Dispose() {
            // Dispose all program icons
            for (int i = 0; i < _programs.Count; i++) {
                var entry = _programs.ToArray()[i];
                if (entry.Icon != null) {
                    entry.Icon.Dispose();
                }
            }
            _programs.Clear();
            
            // Dispose all document icons
            for (int i = 0; i < _documents.Count; i++) {
                var entry = _documents.ToArray()[i];
                if (entry.Icon != null) {
                    entry.Icon.Dispose();
                }
            }
            _documents.Clear();
        }
    }
}
