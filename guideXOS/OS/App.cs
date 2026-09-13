using guideXOS.DefaultApps;
using guideXOS.DockableWidgets;
using guideXOS.GUI;
using System;
using System.Collections.Generic;
using System.Drawing;
namespace guideXOS.OS {
    /// <summary>
    /// App
    /// </summary>
    public class App {
        #region "private variables"
        /// <summary>
        /// Name
        /// </summary>
        private string _name { get; set; }
        /// <summary>
        /// Icon
        /// </summary>
        private Image _icon { get; set; }
        /// <summary>
        /// App Object
        /// </summary>
        private Object _appObject { get; set; }
        #endregion
        #region "public variables"
        /// <summary>
        /// App
        /// </summary>
        /// <param name="name"></param>
        public App(string name, Image icon) { _name = name; _icon = icon; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }
        /// <summary>
        /// Icon
        /// </summary>
        public Image Icon {
            get {
                return _icon;
            }
        }
        /// <summary>
        /// App Object
        /// </summary>
        public Object AppObject {
            get {
                return _appObject;
            }
            set {
                _appObject = value;
            }
        }
        #endregion
    }
    /// <summary>
    /// App Collection
    /// </summary>
    public class AppCollection {
        #region "private variables"
        /// <summary>
        /// Apps
        /// </summary>
        private List<App> _apps;
        #endregion
        #region "public variables"
        /// <summary>
        /// App Collection
        /// </summary>
        public AppCollection() {
            _apps = new List<App>();
            LoadDefaultApps();
        }
        /// <summary>
        /// Load Default Apps
        /// </summary>
        private void LoadDefaultApps() {
            AppLaunchResolver.InitializeDefaultDescriptors();
            // Build the immutable semantic projection at the same safe,
            // post-EBS initialization point as the existing app collection.
            // The legacy list below remains the Start/UI compatibility list.
            ApplicationDescriptorRegistry.Initialize();
            _apps.Add(new App("Calculator", Icons.CalculatorIcon(32)));
            _apps.Add(new App("Computer Files", Icons.FolderIcon(32)));
            _apps.Add(new App("Console", Icons.EditIcon(32)));
            _apps.Add(new App("Devices", Icons.ConfigureIcon(32)));
            _apps.Add(new App("Disk Manager", Icons.DocumentIcon(32)));
            _apps.Add(new App("Display Options", Icons.ConfigureIcon(32)));
            _apps.Add(new App("Firewall", Icons.ConfigureIcon(32)));
            _apps.Add(new App("Notepad", Icons.NotepadIcon(32)));
            _apps.Add(new App("Paint", Icons.ImageIcon(32)));
            _apps.Add(new App("Task Manager", Icons.ApplicationsIcon(32)));
            _apps.Add(new App("Image Viewer", Icons.ImageIcon(32)));
            _apps.Add(new App("WAV Player", Icons.AudioIcon(32)));
            //_apps.Add(new App("Clock", Icons.CalendarIcon(32)));
            //_apps.Add(new App("Monitor", Icons.DocumentIcon(32)));
            //_apps.Add(new App("Lock", Icons.LockIcon(32)));
            //_apps.Add(new App("nexIRC", Icons.ChatIcon(32)));
            //_apps.Add(new App("IRCNetworks", Icons.NetworkIcon(32)));
            //_apps.Add(new App("GUISamples", Icons.ApplicationsIcon(32)));
            //_apps.Add(new App("OnScreenKeyboard", Icons.EditIcon(32)));
            //_apps.Add(new App("WebBrowser", Icons.NetworkIcon(32)));
            //_apps.Add(new App("Welcome", Icons.ApplicationsIcon(32)));
            // GXM apps from filesystem
            //_apps.Add(new App("Hello Demo", Icons.ApplicationsIcon(32)));
            //_apps.Add(new App("Minimal Demo", Icons.ApplicationsIcon(32)));
        }
        /// <summary>
        /// Load
        /// </summary>
        /// <param name="name"></param>
        internal bool LoadLegacyBackend(LaunchRequest request,
                                        AppLaunchResolution resolution) {
            var b = false;
            string name = request == null ? null : request.TargetNameOrAlias;
            string dispatchName = resolution != null && resolution.Success
                ? resolution.DispatchName : name;
            for (int i = 0; i < _apps.Count; i++) {
                if (_apps[i].Name == dispatchName) {
                    switch (dispatchName) {
                        case "Devices": _apps[i].AppObject = new Devices(400, 300); b = true; break;
                        case "Lock": Lockscreen.Run(); b = true; break;
                        case "Calculator": _apps[i].AppObject = new Calculator(300, 500); b = true; break;
                        case "Monitor": _apps[i].AppObject = new Monitor(); b = true; break;
                        case "Clock": _apps[i].AppObject = new Clock(650, 500); b = true; break;
                        case "Paint": _apps[i].AppObject = new Paint(500, 200); b = true; break;
                        case "Notepad": _apps[i].AppObject = new Notepad(360, 200); b = true; break;
                        case "Console": 
                            if (Program.FConsole == null ||
                                WindowManager.Windows.IndexOf(Program.FConsole) < 0) {
                                Program.FConsole = new FConsole(160, 120);
                            } else {
                                Program.FConsole.Visible = true;
                                WindowManager.MoveToEnd(Program.FConsole);
                            }
                            _apps[i].AppObject = Program.FConsole; b = true; break;
                        case "Task Manager": _apps[i].AppObject = new TaskManager(500, 500); b = true; break;
                        case "nexIRC": _apps[i].AppObject = new nexIRC(260, 220); b = true; break;
                        case "IRC Networks": _apps[i].AppObject = new IRCNetworks(300, 240); b = true; break;
                        case "GUI Samples": _apps[i].AppObject = new GUISamples(220, 260); b = true; break;
                        case "Computer Files": _apps[i].AppObject = new ComputerFiles(300, 200); b = true; break;
                        case "Disk Manager": _apps[i].AppObject = new DiskManager(400, 300); b = true; break;
                        case "Display Options": _apps[i].AppObject = new DisplayOptions(200, 150, 800, 600); b = true; break;
                        case "Firewall": _apps[i].AppObject = new FirewallWindow(300, 200); b = true; break;
                        case "Image Viewer": 
                            _apps[i].AppObject = Desktop.EnsureImageViewer();
                            Desktop.imageViewer.Visible = true;
                            WindowManager.MoveToEnd(Desktop.imageViewer);
                            b = true;
                            break;
                        case "On Screen Keyboard": _apps[i].AppObject = new OnScreenKeyboard(300, 100); b = true; break;
                        case "WAV Player": 
                            _apps[i].AppObject = Desktop.EnsureWavPlayer();
                            Desktop.wavplayer.Visible = true;
                            WindowManager.MoveToEnd(Desktop.wavplayer);
                            b = true;
                            break;
                        case "Web Browser": _apps[i].AppObject = new WebBrowser(200, 150); b = true; break;
                        case "Welcome": _apps[i].AppObject = new Welcome(300, 200); b = true; break;
                        // GXM apps
                        case "Hello Demo":
                            b = LaunchGXMFromFile("Programs/hello.gxm", _apps[i].Icon);
                            break;
                        case "Minimal Demo":
                            b = LaunchGXMFromFile("Programs/minimal.gxm", _apps[i].Icon);
                            break;
                    }
                    if (b) {
                        // record recents
                        RecentManager.AddProgram(dispatchName, _apps[i].Icon);
                        // apply taskbar icon if window
                        if (_apps[i].AppObject is guideXOS.GUI.Window w) {
                            w.TaskbarIcon = _apps[i].Icon;
                            w.ShowInTaskbar = true;
                        }
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                        guideXOS.GUI.Window launchedWindow =
                            _apps[i].AppObject as guideXOS.GUI.Window;
                        Program.MarkUefiAppRuntime("LAUNCH_OK=id=" +
                            (resolution.AppId ?? "") + ";name=" + dispatchName +
                            ";type=" + (launchedWindow != null ? "WINDOW" :
                            (_apps[i].AppObject == null ? "NONE" : "NON_WINDOW")) +
                            ";bounds=" + (launchedWindow == null ? "0,0,0,0" :
                            launchedWindow.X.ToString() + "," + launchedWindow.Y.ToString() + "," +
                            launchedWindow.Width.ToString() + "," + launchedWindow.Height.ToString()) +
                            ";windows=" + WindowManager.Windows.Count.ToString());
#endif
                    }
                }
            }
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            if (!b) {
                Program.MarkUefiAppRuntime("LAUNCH_FAIL=input=" + (name ?? "") +
                    ";reason=" + (resolution.FailureReason ?? "UNAVAILABLE_IMPLEMENTATION"));
            }
#endif
            return b;
        }

        /// <summary>
        /// Compatibility facade retained for all current callers.  It creates
        /// a common request, resolves it through the modern descriptor
        /// projection, then invokes the unchanged managed backend above.
        /// </summary>
        public bool Load(string name) {
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("LAUNCH_ENTER");
            Program.MarkUefiAppRuntime("LAUNCH_NOTIFY_BEGIN");
#endif
            guideXOS.GUI.NotificationManager.Add(new Notify("Loading App: " + name));
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("LAUNCH_NOTIFY_DONE");
            Program.MarkUefiAppRuntime("LAUNCH_RESOLVE_BEGIN");
#endif
            LaunchRequest request = LaunchRequest.ForName(name);
            AppLaunchResolution resolution;
            ApplicationDescriptor modernDescriptor;
            LaunchResult resolutionFailure;
            bool canDispatch = AppLaunchCompatibilityAdapter.TryResolveForLegacyCollection(
                this, request, out resolution, out modernDescriptor,
                out resolutionFailure);
            string dispatchName = resolution != null && resolution.Success
                ? resolution.DispatchName : name;
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("LAUNCH_RESOLVE=input=" + (name ?? "") +
                ";id=" + (resolution == null ? "" : (resolution.AppId ?? "")) +
                ";kind=" + (resolution == null ? AppKind.Unknown.ToString() :
                    resolution.ResolvedKind.ToString()) +
                ";dispatch=" + (dispatchName ?? "") +
                ";success=" + (resolution != null && resolution.Success ? "1" : "0"));
            Program.MarkUefiAppRuntime("LAUNCH_REQUEST=target=" +
                (request.TargetAppId ?? "") + ";name=" +
                (request.TargetNameOrAlias ?? "") + ";document=" +
                (request.Document ?? "") + ";args=" +
                request.ArgumentCount.ToString() + ";source=" +
                (request.SourceShellObjectId ?? "") + ";intent=" +
                request.ActivationIntent.ToString());
            Program.MarkUefiAppRuntime("LAUNCH_ADAPTER=modern;descriptor=" +
                (modernDescriptor == null ? "" : modernDescriptor.AppId) +
                ";resolution=" + (canDispatch ? "1" : "0"));
#endif
            if (AppLaunchResolver.EnableResolutionDiagnostics) {
                try {
                    guideXOS.GUI.NotificationManager.Add(new Notify(
                        "input=" + name + " resolvedAppId=" +
                        (resolution == null ? "" : (resolution.AppId ?? "")) +
                        " resolvedKind=" + (resolution == null ? AppKind.Unknown :
                        resolution.ResolvedKind) + " dispatchName=" +
                        (dispatchName ?? "") + " success=" +
                        (resolution != null && resolution.Success)));
                } catch { }
            }
            if (!canDispatch) {
#if UEFI_DIAGNOSTIC_APP_RUNTIME
                Program.MarkUefiAppRuntime("LAUNCH_RESULT=code=" +
                    (resolutionFailure == null ? LaunchErrorCode.NotFound.ToString() :
                        resolutionFailure.ErrorCode.ToString()) + ";success=0");
                Program.MarkUefiAppRuntime("LAUNCH_FAIL=input=" + (name ?? "") +
                    ";reason=" + (resolutionFailure == null ?
                        "UNAVAILABLE_IMPLEMENTATION" :
                        (resolutionFailure.BoundedDiagnostic ?? "NOT_FOUND")));
#endif
                return false;
            }

            LaunchResult result = AppLaunchCompatibilityAdapter.DispatchToLegacyBackend(
                this, request, resolution);
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("LAUNCH_RESULT=code=" +
                result.ErrorCode.ToString() + ";success=" +
                (result.Success ? "1" : "0") + ";app=" +
                (result.AppId ?? ""));
#endif
            return result.Success;
        }

        internal bool HasLegacyAppName(string name) {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < _apps.Count; i++) {
                if (_apps[i].Name == name) return true;
            }
            return false;
        }

        /// <summary>
        /// Launch GXM app from file
        /// </summary>
        /// <param name="path">Path to GXM file</param>
        /// <param name="icon">Icon to use for recent items</param>
        /// <returns>True if successfully launched</returns>
        private bool LaunchGXMFromFile(string path, Image icon) {
            byte[] buffer = guideXOS.FS.File.ReadAllBytes(path);
            if (buffer == null) {
                guideXOS.GUI.NotificationManager.Add(new Notify($"File not found: {path}"));
                return false;
            }
            
            string err;
            bool ok = guideXOS.Misc.GXMLoader.TryExecute(buffer, out err);
            if (ok) {
                guideXOS.GUI.RecentManager.AddProgram(path, icon);
            } else {
                guideXOS.GUI.NotificationManager.Add(new Notify($"Failed: {err}"));
            }
            buffer.Dispose();
            return ok;
        }
        /// <summary>
        /// Add
        /// </summary>
        /// <param name="app"></param>
        public void Add(App app) {
            _apps.Add(app);
        }
        /// <summary>
        /// Length
        /// </summary>
        public int Length {
            get {
                return _apps.Count;
            }
        }
        /// <summary>
        /// Name
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public string Name(int id) { return _apps[id].Name; }
        /// <summary>
        /// Icon
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Image Icon(int id) { return _apps[id].Icon; }
        #endregion
    }
}
