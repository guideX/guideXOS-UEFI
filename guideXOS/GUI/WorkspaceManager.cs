//THIS FREEZES WHEN OPENING! TODO: PLEASE FIX IT!
using System.Collections.Generic;
namespace guideXOS.GUI {
    /// <summary>
    /// Simple virtual workspaces manager. Tracks which workspace a window belongs to and switches visibility
    /// by minimizing/restoring windows per workspace. Non-taskbar windows are ignored.
    /// </summary>
    internal static class WorkspaceManager {
        private static readonly List<Window> _keys = new List<Window>(128);
        private static readonly List<int> _values = new List<int>(128);
        private static readonly List<Window> _minimizedBySwitch = new List<Window>(64);
        private static int _count = 1;
        public static int Current { get; private set; } = 0;
        public const int MaxWorkspaces = 8;
        private static bool _switching = false; // prevent re-entry

        public static int Count => _count;

        public static void ForgetWindow(Window window) {
            if (window == null) return;
            for (int i = _keys.Count - 1; i >= 0; i--) {
                if (!object.ReferenceEquals(_keys[i], window)) continue;
                _keys.RemoveAt(i);
                _values.RemoveAt(i);
            }
            for (int i = _minimizedBySwitch.Count - 1; i >= 0; i--) {
                if (object.ReferenceEquals(_minimizedBySwitch[i], window))
                    _minimizedBySwitch.RemoveAt(i);
            }
        }

        private static int IndexOf(Window w) {
            for (int i = 0; i < _keys.Count; i++) if (_keys[i] == w) return i; return -1;
        }
        private static bool ListContains(List<Window> list, Window w) { for (int i = 0; i < list.Count; i++) if (list[i] == w) return true; return false; }
        private static void ListRemove(List<Window> list, Window w) { for (int i = 0; i < list.Count; i++) if (list[i] == w) { list.RemoveAt(i); return; } }

        public static void EnsureAllWindowsTracked() {
            var wins = WindowManager.Windows;
            if (wins == null) return;
            for (int i = 0; i < wins.Count; i++) {
                var w = wins[i];
                if (w == null) continue;
                if (!w.ShowInTaskbar) continue; // don't manage internal UI (including WorkspaceSwitcher)
                if (IndexOf(w) == -1) { _keys.Add(w); _values.Add(Current); }
            }
        }

        public static int GetWorkspace(Window w) {
            if (w == null) return Current;
            int idx = IndexOf(w);
            if (idx >= 0) return _values[idx];
            return Current;
        }

        public static bool MoveWindowToWorkspace(Window w, int workspaceIndex) {
            if (w == null) return false;
            if (workspaceIndex < 0 || workspaceIndex >= _count) return false;
            if (!w.ShowInTaskbar) return false;
            int idx = IndexOf(w);
            if (idx < 0) { _keys.Add(w); _values.Add(workspaceIndex); } else { _values[idx] = workspaceIndex; }
            // If moved away from current, hide it; if moved to current, show it.
            if (workspaceIndex != Current) {
                if (w.Visible && !w.IsMinimized) {
                    // Hide instead of minimize to avoid animation freeze
                    w._visible = false;
                    if (!ListContains(_minimizedBySwitch, w)) _minimizedBySwitch.Add(w);
                }
            } else {
                if (ListContains(_minimizedBySwitch, w)) {
                    if (w.IsMinimized) w.Restore();
                    ListRemove(_minimizedBySwitch, w);
                }
                w.Visible = true; WindowManager.MoveToEnd(w);
            }
            return true;
        }

        public static bool AddWorkspace() {
            if (_count >= MaxWorkspaces) return false;
            _count++;
            return true;
        }

        public static bool RemoveTrailingEmptyWorkspaces() {
            // Remove trailing empty workspaces (except keep at least 1)
            bool changed = false;
            for (int i = _count - 1; i > 0; i--) {
                if (!WorkspaceHasWindows(i)) { _count = i; changed = true; } else break;
            }
            if (Current >= _count) Current = _count - 1;
            return changed;
        }

        public static bool WorkspaceHasWindows(int idx) {
            for (int i = 0; i < _values.Count; i++) if (_values[i] == idx && _keys[i] != null && _keys[i].ShowInTaskbar) return true;
            return false;
        }

        public static void SwitchTo(int idx) {
            if (_switching) return; // prevent re-entry during animations
            if (idx < 0) idx = 0; if (idx >= _count) idx = _count - 1;
            if (idx == Current) return;

            _switching = true;
            try {
                EnsureAllWindowsTracked();
                // Don't minimize/restore windows - just hide/show them to avoid animation issues
                var wins = WindowManager.Windows;
                if (wins == null) return;

                // Create a snapshot list to avoid collection modification issues
                var winSnapshot = new List<Window>(wins.Count);
                for (int i = 0; i < wins.Count; i++) {
                    winSnapshot.Add(wins[i]);
                }

                for (int i = 0; i < winSnapshot.Count; i++) {
                    var w = winSnapshot[i];
                    if (w == null) continue;
                    if (!w.ShowInTaskbar) continue;

                    int ws = GetWorkspace(w);
                    if (ws == idx) {
                        // Window belongs to target workspace - show it
                        // If it was minimized by switch, restore it
                        if (ListContains(_minimizedBySwitch, w)) {
                            if (w.IsMinimized) w.Restore();
                            ListRemove(_minimizedBySwitch, w);
                        }
                        w.Visible = true;
                    } else {
                        // Window belongs to different workspace - hide it
                        // Only minimize if visible and not already minimized
                        if (w.Visible && !w.IsMinimized) {
                            // Just hide instead of minimize to avoid animation freeze
                            w._visible = false; // Direct access to avoid triggering animation
                            if (!ListContains(_minimizedBySwitch, w)) _minimizedBySwitch.Add(w);
                        }
                    }
                }
                Current = idx;
            } finally {
                _switching = false;
            }
        }

        public static void Next() { if (Current + 1 < _count) SwitchTo(Current + 1); }
        public static void Prev() { if (Current - 1 >= 0) SwitchTo(Current - 1); }

        public static int[] WorkspaceWindowCounts() {
            int[] counts = new int[_count];
            for (int i = 0; i < _values.Count; i++) {
                var w = _keys[i]; if (w == null || !w.ShowInTaskbar) continue;
                int ws = _values[i]; if (ws >= 0 && ws < counts.Length) counts[ws]++;
            }
            return counts;
        }
    }
}
