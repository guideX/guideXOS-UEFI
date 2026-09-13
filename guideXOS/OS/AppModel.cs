using guideXOS.GUI;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace guideXOS.OS {
    /// <summary>
    /// Kind of application entry resolved by the shared launch contract.
    /// </summary>
    public enum AppKind {
        BuiltInApp,
        LegacyAlias,
        GxmApp,
        FileAssociation,
        ShellObject,
        Unknown
    }

    /// <summary>
    /// Stable application descriptor.  DispatchName remains the current
    /// built-in name so the UEFI and Legacy launch implementations can evolve
    /// independently behind the same source-level contract.
    /// </summary>
    public class AppDescriptor {
        public string AppId { get; set; }
        public string DisplayName { get; set; }
        public string DispatchName { get; set; }
        public AppKind Kind { get; set; }
        public string[] LegacyAliases { get; set; }
        public Image Icon { get; set; }
    }

    /// <summary>
    /// Result of resolving an application launch request.
    /// </summary>
    public class AppLaunchResolution {
        public bool Success { get; set; }
        public string Input { get; set; }
        public string AppId { get; set; }
        public string DisplayName { get; set; }
        public string DispatchName { get; set; }
        public AppKind ResolvedKind { get; set; }
        public string MatchedAlias { get; set; }
        public string FailureReason { get; set; }
    }

    /// <summary>
    /// Resolver for stable app IDs and the names accepted by Legacy callers.
    /// </summary>
    public static class AppLaunchResolver {
        private static List<AppDescriptor> _descriptors;

        // Opt-in only.  Production does not emit resolver notifications.
        public static bool EnableResolutionDiagnostics;

        public static void InitializeDefaultDescriptors() {
            if (_descriptors != null) return;
            _descriptors = new List<AppDescriptor>();

            RegisterBuiltIn("gxos.builtin.calculator", "Calculator", "Calculator", Icons.CalculatorIcon(32), "Calculator");
            RegisterBuiltIn("gxos.builtin.files", "Computer Files", "Computer Files", Icons.FolderIcon(32), "Computer Files", "File Explorer");
            RegisterBuiltIn("gxos.builtin.console", "Console", "Console", Icons.EditIcon(32), "Console");
            RegisterBuiltIn("gxos.builtin.devices", "Devices", "Devices", Icons.ConfigureIcon(32), "Devices");
            RegisterBuiltIn("gxos.builtin.diskmanager", "Disk Manager", "Disk Manager", Icons.DocumentIcon(32), "Disk Manager");
            RegisterBuiltIn("gxos.builtin.displayoptions", "Display Options", "Display Options", Icons.ConfigureIcon(32), "Display Options");
            RegisterBuiltIn("gxos.builtin.firewall", "Firewall", "Firewall", Icons.ConfigureIcon(32), "Firewall");
            RegisterBuiltIn("gxos.builtin.notepad", "Notepad", "Notepad", Icons.NotepadIcon(32), "Notepad");
            RegisterBuiltIn("gxos.builtin.paint", "Paint", "Paint", Icons.ImageIcon(32), "Paint");
            RegisterBuiltIn("gxos.builtin.taskmanager", "Task Manager", "Task Manager", Icons.ApplicationsIcon(32), "Task Manager");
            RegisterBuiltIn("gxos.builtin.imageviewer", "Image Viewer", "Image Viewer", Icons.ImageIcon(32), "Image Viewer");
            RegisterBuiltIn("gxos.builtin.wavplayer", "WAV Player", "WAV Player", Icons.AudioIcon(32), "WAV Player");
        }

        private static void RegisterBuiltIn(string appId, string displayName,
                                            string dispatchName, Image icon,
                                            params string[] legacyAliases) {
            _descriptors.Add(new AppDescriptor {
                AppId = appId,
                DisplayName = displayName,
                DispatchName = dispatchName,
                Kind = AppKind.BuiltInApp,
                LegacyAliases = legacyAliases,
                Icon = icon
            });
        }

        public static AppLaunchResolution Resolve(string input) {
            InitializeDefaultDescriptors();
            var result = new AppLaunchResolution {
                Input = input,
                ResolvedKind = AppKind.Unknown,
                Success = false
            };

            if (string.IsNullOrEmpty(input)) {
                result.FailureReason = "Empty input";
                return result;
            }

            if (TryGetDescriptorByAppId(input, out AppDescriptor directDescriptor)) {
                result.Success = true;
                result.AppId = directDescriptor.AppId;
                result.DisplayName = directDescriptor.DisplayName;
                result.DispatchName = directDescriptor.DispatchName;
                result.ResolvedKind = directDescriptor.Kind;
                return result;
            }

            for (int i = 0; i < _descriptors.Count; i++) {
                AppDescriptor descriptor = _descriptors[i];
                if (descriptor.LegacyAliases != null) {
                    for (int a = 0; a < descriptor.LegacyAliases.Length; a++) {
                        if (descriptor.LegacyAliases[a] == input) {
                            result.Success = true;
                            result.AppId = descriptor.AppId;
                            result.DisplayName = descriptor.DisplayName;
                            result.DispatchName = descriptor.DispatchName;
                            result.ResolvedKind = AppKind.LegacyAlias;
                            result.MatchedAlias = descriptor.LegacyAliases[a];
                            return result;
                        }
                    }
                }

                if (descriptor.DisplayName == input || descriptor.DispatchName == input) {
                    result.Success = true;
                    result.AppId = descriptor.AppId;
                    result.DisplayName = descriptor.DisplayName;
                    result.DispatchName = descriptor.DispatchName;
                    result.ResolvedKind = AppKind.LegacyAlias;
                    result.MatchedAlias = input;
                    return result;
                }
            }

            result.FailureReason = "No matching app descriptor";
            return result;
        }

        public static bool TryGetDescriptorByAppId(string appId,
                                                    out AppDescriptor descriptor) {
            InitializeDefaultDescriptors();
            descriptor = null;
            if (string.IsNullOrEmpty(appId)) return false;

            for (int i = 0; i < _descriptors.Count; i++) {
                if (StringEqualsIgnoreCase(_descriptors[i].AppId, appId)) {
                    descriptor = _descriptors[i];
                    return true;
                }
            }
            return false;
        }

        private static bool StringEqualsIgnoreCase(string a, string b) {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) {
                char ca = a[i];
                char cb = b[i];
                if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32);
                if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32);
                if (ca != cb) return false;
            }
            return true;
        }

        public static bool RunSelfTest() {
            InitializeDefaultDescriptors();
            int passed = 0;
            int failed = 0;
            string failure = null;

            Check("gxos.builtin.notepad", "gxos.builtin.notepad", "Notepad", AppKind.BuiltInApp, false, ref passed, ref failed, ref failure);
            Check("gxos.builtin.calculator", "gxos.builtin.calculator", "Calculator", AppKind.BuiltInApp, false, ref passed, ref failed, ref failure);
            Check("gxos.builtin.files", "gxos.builtin.files", "Computer Files", AppKind.BuiltInApp, false, ref passed, ref failed, ref failure);
            Check("gxos.builtin.taskmanager", "gxos.builtin.taskmanager", "Task Manager", AppKind.BuiltInApp, false, ref passed, ref failed, ref failure);
            Check("File Explorer", "gxos.builtin.files", "Computer Files", AppKind.LegacyAlias, true, ref passed, ref failed, ref failure);
            Check("Notepad", "gxos.builtin.notepad", "Notepad", AppKind.LegacyAlias, true, ref passed, ref failed, ref failure);
            CheckFailure("Definitely Not A Real App", ref passed, ref failed, ref failure);
            CheckFailure("gxos.builtin.notreal", ref passed, ref failed, ref failure);

            EmitSelfTestSummary("AppModelSmoke", passed, failed, failure);
            return failed == 0;
        }

        private static void Check(string input, string expectedId,
                                  string expectedDispatch, AppKind expectedKind,
                                  bool expectAlias, ref int passed, ref int failed,
                                  ref string failure) {
            AppLaunchResolution result = Resolve(input);
            if (!result.Success || result.AppId != expectedId ||
                result.DispatchName != expectedDispatch ||
                (result.ResolvedKind != expectedKind &&
                 !(expectedKind == AppKind.LegacyAlias &&
                   result.ResolvedKind == AppKind.BuiltInApp)) ||
                (expectAlias && string.IsNullOrEmpty(result.MatchedAlias))) {
                if (failure == null) failure = "input=" + input + " resolution mismatch";
                failed++;
                return;
            }
            passed++;
        }

        private static void CheckFailure(string input, ref int passed,
                                         ref int failed, ref string failure) {
            AppLaunchResolution result = Resolve(input);
            if (result.Success || string.IsNullOrEmpty(result.FailureReason)) {
                if (failure == null) failure = "input=" + input + " unexpectedly resolved";
                failed++;
                return;
            }
            passed++;
        }

        internal static void EmitSelfTestSummary(string name, int passed,
                                                  int failed, string failure) {
            if (!EnableResolutionDiagnostics) return;
            try {
                NotificationManager.Add(new Notify(name + ": passed=" +
                    passed + " failed=" + failed +
                    (failure != null ? " " + failure : "")));
            } catch { }
        }
    }

    public class FileAssociationDescriptor {
        public string Extension { get; set; }
        public string AppId { get; set; }
        public AppKind Kind { get; set; }
    }

    public class FileAssociationResolution {
        public bool Success { get; set; }
        public string Extension { get; set; }
        public string AppId { get; set; }
        public string DisplayName { get; set; }
        public string DispatchName { get; set; }
        public AppKind Kind { get; set; }
        public string FailureReason { get; set; }
    }

    /// <summary>
    /// Shared file association registry.  It resolves metadata only; the
    /// existing UEFI-safe window and filesystem code performs the open.
    /// </summary>
    public static class FileAssociationRegistry {
        private static List<FileAssociationDescriptor> _descriptors;

        public static void InitializeDefaultAssociations() {
            if (_descriptors != null) return;
            _descriptors = new List<FileAssociationDescriptor>();
            Register(".txt", "gxos.builtin.notepad", AppKind.FileAssociation);
            Register(".png", "gxos.builtin.imageviewer", AppKind.FileAssociation);
            Register(".bmp", "gxos.builtin.imageviewer", AppKind.FileAssociation);
            Register(".wav", "gxos.builtin.wavplayer", AppKind.FileAssociation);
            Register(".gxm", null, AppKind.GxmApp);
            Register(".mue", null, AppKind.GxmApp);
        }

        private static void Register(string extension, string appId, AppKind kind) {
            _descriptors.Add(new FileAssociationDescriptor {
                Extension = NormalizeExtension(extension),
                AppId = appId,
                Kind = kind
            });
        }

        public static FileAssociationResolution Resolve(string extension) {
            InitializeDefaultAssociations();
            string normalized = NormalizeExtension(extension);
            var result = new FileAssociationResolution {
                Extension = normalized,
                Kind = AppKind.Unknown,
                Success = false
            };

            if (string.IsNullOrEmpty(normalized)) {
                result.FailureReason = "Empty extension";
                return result;
            }

            for (int i = 0; i < _descriptors.Count; i++) {
                FileAssociationDescriptor descriptor = _descriptors[i];
                if (descriptor.Extension != normalized) continue;

                if (descriptor.Kind == AppKind.FileAssociation &&
                    !string.IsNullOrEmpty(descriptor.AppId)) {
                    if (!AppLaunchResolver.TryGetDescriptorByAppId(
                            descriptor.AppId, out AppDescriptor app)) {
                        result.FailureReason = "No matching app descriptor for association";
                        return result;
                    }
                    result.Success = true;
                    result.AppId = app.AppId;
                    result.DisplayName = app.DisplayName;
                    result.DispatchName = app.DispatchName;
                    result.Kind = descriptor.Kind;
                    return result;
                }

                result.Success = true;
                result.AppId = descriptor.AppId;
                result.Kind = descriptor.Kind;
                return result;
            }

            result.FailureReason = "No matching file association";
            return result;
        }

        public static FileAssociationResolution ResolvePath(string pathOrName) {
            if (string.IsNullOrEmpty(pathOrName)) {
                return new FileAssociationResolution {
                    Success = false,
                    Kind = AppKind.Unknown,
                    FailureReason = "Empty path"
                };
            }

            int dot = pathOrName.LastIndexOf('.');
            if (dot < 0 || dot >= pathOrName.Length - 1) {
                return new FileAssociationResolution {
                    Success = false,
                    Kind = AppKind.Unknown,
                    FailureReason = "No file extension"
                };
            }
            return Resolve(pathOrName.Substring(dot));
        }

        public static bool RunSelfTest() {
            InitializeDefaultAssociations();
            int passed = 0;
            int failed = 0;
            string failure = null;

            CheckDerived(".txt", "gxos.builtin.notepad", ref passed, ref failed, ref failure);
            CheckDerived(".TXT", "gxos.builtin.notepad", ref passed, ref failed, ref failure);
            CheckDerived(".png", "gxos.builtin.imageviewer", ref passed, ref failed, ref failure);
            CheckDerived(".bmp", "gxos.builtin.imageviewer", ref passed, ref failed, ref failure);
            CheckDerived(".wav", "gxos.builtin.wavplayer", ref passed, ref failed, ref failure);
            CheckKind(".gxm", AppKind.GxmApp, ref passed, ref failed, ref failure);
            CheckKind(".mue", AppKind.GxmApp, ref passed, ref failed, ref failure);
            CheckFailure(".unknown", ref passed, ref failed, ref failure);

            AppLaunchResolver.EmitSelfTestSummary("FileAssocSmoke", passed, failed, failure);
            return failed == 0;
        }

        private static void CheckDerived(string extension, string expectedAppId,
                                         ref int passed, ref int failed,
                                         ref string failure) {
            FileAssociationResolution result = Resolve(extension);
            if (!result.Success || result.AppId != expectedAppId ||
                result.Kind != AppKind.FileAssociation ||
                string.IsNullOrEmpty(result.DisplayName) ||
                string.IsNullOrEmpty(result.DispatchName)) {
                if (failure == null) failure = "extension=" + extension + " mismatch";
                failed++;
                return;
            }
            passed++;
        }

        private static void CheckKind(string extension, AppKind expectedKind,
                                      ref int passed, ref int failed,
                                      ref string failure) {
            FileAssociationResolution result = Resolve(extension);
            if (!result.Success || result.Kind != expectedKind) {
                if (failure == null) failure = "extension=" + extension + " mismatch";
                failed++;
                return;
            }
            passed++;
        }

        private static void CheckFailure(string extension, ref int passed,
                                         ref int failed, ref string failure) {
            FileAssociationResolution result = Resolve(extension);
            if (result.Success || string.IsNullOrEmpty(result.FailureReason)) {
                if (failure == null) failure = "extension=" + extension + " unexpectedly resolved";
                failed++;
                return;
            }
            passed++;
        }

        private static string NormalizeExtension(string extension) {
            if (string.IsNullOrEmpty(extension)) return extension;
            string value = extension;
            if (value[0] != '.') value = "." + value;
            char[] chars = new char[value.Length];
            for (int i = 0; i < value.Length; i++) {
                char c = value[i];
                if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
                chars[i] = c;
            }
            return new string(chars);
        }
    }

    public enum ShellObjectKind {
        BuiltInApp,
        FileSystemLocation,
        DeviceVolume,
        SystemAction,
        Unknown
    }

    public class ShellObjectDescriptor {
        public string ShellId { get; set; }
        public string DisplayName { get; set; }
        public ShellObjectKind Kind { get; set; }
        public string AppId { get; set; }
        public string DispatchName { get; set; }
        public string Path { get; set; }
        public string DeviceName { get; set; }
        public string ActionName { get; set; }
        public string[] LegacyAliases { get; set; }
    }

    public class ShellObjectResolution {
        public bool Success { get; set; }
        public string Input { get; set; }
        public string ShellId { get; set; }
        public string DisplayName { get; set; }
        public ShellObjectKind Kind { get; set; }
        public string AppId { get; set; }
        public string DispatchName { get; set; }
        public string Path { get; set; }
        public string DeviceName { get; set; }
        public string ActionName { get; set; }
        public string MatchedAlias { get; set; }
        public string FailureReason { get; set; }
    }

    /// <summary>
    /// Shell names used by the desktop and future shared applications.
    /// </summary>
    public static class ShellObjectRegistry {
        private static List<ShellObjectDescriptor> _descriptors;

        public static void InitializeDefaultShellObjects() {
            if (_descriptors != null) return;
            _descriptors = new List<ShellObjectDescriptor>();
            AppLaunchResolver.InitializeDefaultDescriptors();

            if (AppLaunchResolver.TryGetDescriptorByAppId(
                    "gxos.builtin.files", out AppDescriptor files)) {
                Register(new ShellObjectDescriptor {
                    ShellId = "gxos.shell.computerfiles",
                    DisplayName = files.DisplayName,
                    Kind = ShellObjectKind.BuiltInApp,
                    AppId = files.AppId,
                    DispatchName = files.DispatchName,
                    LegacyAliases = new string[] { "Computer Files" }
                });
            }

            Register(new ShellObjectDescriptor {
                ShellId = "gxos.shell.root",
                DisplayName = "Root",
                Kind = ShellObjectKind.FileSystemLocation,
                Path = "",
                LegacyAliases = new string[] { "Root" }
            });
            Register(new ShellObjectDescriptor {
                ShellId = "gxos.shell.usbdrive",
                DisplayName = "USB Drive",
                Kind = ShellObjectKind.DeviceVolume,
                DeviceName = "USB Drive",
                LegacyAliases = new string[] { "USB Drive" }
            });
            Register(new ShellObjectDescriptor {
                ShellId = "gxos.shell.installtoharddrive",
                DisplayName = "Install to Hard Drive",
                Kind = ShellObjectKind.SystemAction,
                ActionName = "HDInstaller",
                DispatchName = "HDInstaller",
                LegacyAliases = new string[] { "Install to Hard Drive" }
            });
        }

        private static void Register(ShellObjectDescriptor descriptor) {
            if (descriptor != null) _descriptors.Add(descriptor);
        }

        public static ShellObjectResolution Resolve(string input) {
            InitializeDefaultShellObjects();
            var result = new ShellObjectResolution {
                Input = input,
                Kind = ShellObjectKind.Unknown,
                Success = false
            };
            if (string.IsNullOrEmpty(input)) {
                result.FailureReason = "Empty input";
                return result;
            }

            for (int i = 0; i < _descriptors.Count; i++) {
                ShellObjectDescriptor descriptor = _descriptors[i];
                if (!MatchesAlias(descriptor, input, out string alias)) continue;
                result.Success = true;
                result.ShellId = descriptor.ShellId;
                result.DisplayName = descriptor.DisplayName;
                result.Kind = descriptor.Kind;
                result.AppId = descriptor.AppId;
                result.DispatchName = descriptor.DispatchName;
                result.Path = descriptor.Path;
                result.DeviceName = descriptor.Kind == ShellObjectKind.DeviceVolume
                    ? input : descriptor.DeviceName;
                result.ActionName = descriptor.ActionName;
                result.MatchedAlias = alias;
                return result;
            }

            result.FailureReason = "No matching shell object";
            return result;
        }

        private static bool MatchesAlias(ShellObjectDescriptor descriptor,
                                         string input, out string matchedAlias) {
            matchedAlias = null;
            if (descriptor == null || descriptor.LegacyAliases == null) return false;
            for (int i = 0; i < descriptor.LegacyAliases.Length; i++) {
                string alias = descriptor.LegacyAliases[i];
                if (alias == null) continue;
                if (alias == input ||
                    (descriptor.Kind == ShellObjectKind.DeviceVolume &&
                     StartsWith(input, alias))) {
                    matchedAlias = alias;
                    return true;
                }
            }
            return false;
        }

        private static bool StartsWith(string value, string prefix) {
            if (value == null || prefix == null || value.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++) {
                if (value[i] != prefix[i]) return false;
            }
            return true;
        }

        public static bool RunSelfTest() {
            InitializeDefaultShellObjects();
            int passed = 0;
            int failed = 0;
            string failure = null;

            Check("Computer Files", ShellObjectKind.BuiltInApp,
                "gxos.builtin.files", null, ref passed, ref failed, ref failure);
            Check("Root", ShellObjectKind.FileSystemLocation,
                null, "", ref passed, ref failed, ref failure);
            Check("USB Drive 0", ShellObjectKind.DeviceVolume,
                null, null, ref passed, ref failed, ref failure);
            Check("Install to Hard Drive", ShellObjectKind.SystemAction,
                null, null, ref passed, ref failed, ref failure);
            CheckFailure("Definitely Not A Real Shell Object", ref passed, ref failed, ref failure);

            AppLaunchResolver.EmitSelfTestSummary("ShellObjectSmoke", passed, failed, failure);
            return failed == 0;
        }

        private static void Check(string input, ShellObjectKind expectedKind,
                                  string expectedAppId, string expectedPath,
                                  ref int passed, ref int failed,
                                  ref string failure) {
            ShellObjectResolution result = Resolve(input);
            if (!result.Success || result.Kind != expectedKind ||
                (expectedAppId != null && result.AppId != expectedAppId) ||
                (expectedPath != null && result.Path != expectedPath) ||
                string.IsNullOrEmpty(result.DisplayName)) {
                if (failure == null) failure = "input=" + input + " mismatch";
                failed++;
                return;
            }
            passed++;
        }

        private static void CheckFailure(string input, ref int passed,
                                         ref int failed, ref string failure) {
            ShellObjectResolution result = Resolve(input);
            if (result.Success || string.IsNullOrEmpty(result.FailureReason)) {
                if (failure == null) failure = "input=" + input + " unexpectedly resolved";
                failed++;
                return;
            }
            passed++;
        }
    }
}
