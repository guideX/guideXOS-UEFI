using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace guideXOS.GUI {
    /// <summary>
    /// Owns the normal desktop background and its optional rotation state.
    /// UEFI uses the managed PngLoader path; legacy retains its existing PNG
    /// compatibility path.
    /// </summary>
    internal static class BackgroundRotationManager {
        private static List<string> _backgroundPaths;
        private static int _currentIndex;
        private static ulong _lastRotationTick;
        private static bool _initialized;

        // Fade is represented as an instantaneous, already decoded
        // replacement. This keeps the existing behavior without a per-frame
        // composite allocation.
        private static bool _isFading;
        private static Image _fadeFrame;

        public static bool IsInitialized { get { return _initialized; } }

        public static string CurrentBackgroundPath {
            get {
                if (_backgroundPaths == null || _currentIndex < 0 ||
                    _currentIndex >= _backgroundPaths.Count) return null;
                return _backgroundPaths[_currentIndex];
            }
        }

        public static void Initialize() {
            if (_initialized) return;

            _backgroundPaths = new List<string>();
            LoadBackgroundPaths();
            _lastRotationTick = Timer.Ticks;
            _initialized = true;

            if (_backgroundPaths.Count == 0) {
                BootConsole.WriteLine("[BACKGROUND] no bundled wallpaper; retaining solid fallback");
                return;
            }

            int selectedIndex = 0;
            if (!UISettings.EnableAutoBackgroundRotation &&
                UISettings.EnableRandomBackgroundOnStartup) {
                selectedIndex = (int)(Timer.Ticks % (ulong)_backgroundPaths.Count);
            }

            Image candidate;
            if (TryLoadBackground(_backgroundPaths[selectedIndex], out candidate)) {
                PublishWallpaper(candidate);
                _currentIndex = selectedIndex;
                BootConsole.WriteLine("[BACKGROUND] loaded " + CurrentBackgroundPath);
            } else {
                BootConsole.WriteLine("[BACKGROUND] wallpaper load failed; retaining solid fallback");
            }
        }

        private static bool IsUefi() {
            return BootConsole.CurrentMode == guideXOS.BootMode.UEFI;
        }

        private static void LoadBackgroundPaths() {
            _backgroundPaths.Clear();

            List<FileInfo> files = null;
            try {
                files = File.GetFiles(@"Backgrounds/");
                if (files == null) return;

                for (int i = 0; i < files.Count; i++) {
                    FileInfo fi = files[i];
                    if (fi == null) continue;
                    try {
                        if (fi.Attribute != FileAttribute.Directory) {
                            string name = fi.Name;
                            bool isPng = name.EndsWith(".png") || name.EndsWith(".PNG");
                            bool isJpg = name.EndsWith(".jpg") || name.EndsWith(".JPG") ||
                                         name.EndsWith(".jpeg") || name.EndsWith(".JPEG");
                            bool isBmp = name.EndsWith(".bmp") || name.EndsWith(".BMP");
                            bool isThumb = name.EndsWith("_thumb.png") ||
                                           name.EndsWith("_thumb.PNG");
                            if (!isThumb && (isPng || isJpg || isBmp)) {
                                _backgroundPaths.Add("Backgrounds/" + name);
                            }
                        }
                    } finally {
                        fi.Dispose();
                    }
                }
            } catch {
                // An unavailable directory leaves the list empty and
                // DrawBackground supplies the stable fallback.
            } finally {
                if (files != null) files.Dispose();
            }

            BootConsole.WriteLine("[BACKGROUND] discovered " +
                _backgroundPaths.Count.ToString() + " wallpaper assets");
        }

        /// <summary>
        /// Decode and scale a candidate before it can replace the active one.
        /// The caller owns the input byte array; a successful return transfers
        /// ownership of the scaled image to the caller.
        /// </summary>
        private static Image DecodeAndScale(byte[] data) {
            if (data == null || Framebuffer.Graphics == null ||
                Framebuffer.Width <= 0 || Framebuffer.Height <= 0) return null;

            Image decoded = null;
            Image scaled = null;
            try {
                if (IsUefi()) {
                    if (!PngLoader.Initialize() || !PngLoader.Load(data, out decoded)) {
                        return null;
                    }
                } else {
                    decoded = new PNG(data);
                }

                if (decoded == null || decoded.RawData == null ||
                    decoded.Width <= 0 || decoded.Height <= 0) return null;

                scaled = decoded.ResizeImage(Framebuffer.Width, Framebuffer.Height);
                if (scaled == null || scaled.RawData == null ||
                    scaled.Width != Framebuffer.Width ||
                    scaled.Height != Framebuffer.Height) {
                    if (scaled != null) scaled.Dispose();
                    scaled = null;
                }
                return scaled;
            } catch {
                if (scaled != null) scaled.Dispose();
                return null;
            } finally {
                if (decoded != null) decoded.Dispose();
            }
        }

        private static bool TryLoadBackground(string path, out Image candidate) {
            candidate = null;
            if (path == null || File.Instance == null) return false;

            byte[] data = null;
            try {
                data = File.Instance.ReadAllBytes(path);
                candidate = DecodeAndScale(data);
                return candidate != null;
            } catch {
                if (candidate != null) candidate.Dispose();
                candidate = null;
                return false;
            } finally {
                if (data != null) data.Dispose();
            }
        }

        // Diagnostics use the same candidate path without publishing it, so a
        // failed probe cannot disturb the live desktop wallpaper.
        internal static bool TryLoadPathForDiagnostic(string path, out Image candidate) {
            return TryLoadBackground(path, out candidate);
        }

        internal static bool TryLoadDataForDiagnostic(byte[] data, out Image candidate) {
            candidate = DecodeAndScale(data);
            return candidate != null;
        }

        private static void PublishWallpaper(Image candidate) {
            if (candidate == null || candidate.RawData == null) return;

            Image previous = Program.Wallpaper;
            // Publish only after complete decode and scaling. The old valid
            // image remains available until this point.
            Program.Wallpaper = candidate;
            if (previous != null && previous != candidate) previous.Dispose();
        }

        public static void Update() {
            if (!_initialized) Initialize();
            if (_isFading) {
                _isFading = false;
                return;
            }
            if (!UISettings.EnableAutoBackgroundRotation ||
                _backgroundPaths == null || _backgroundPaths.Count <= 1) return;

            ulong elapsed = Timer.Ticks >= _lastRotationTick
                ? Timer.Ticks - _lastRotationTick : 0;
            ulong intervalMs = (ulong)UISettings.BackgroundRotationIntervalMinutes * 60000;
            if (intervalMs != 0 && elapsed >= intervalMs) {
                RotateToNext();
                _lastRotationTick = Timer.Ticks;
            }
        }

        private static bool RotateToNext() {
            if (_backgroundPaths == null || _backgroundPaths.Count == 0) return false;

            int oldIndex = _currentIndex;
            for (int step = 1; step <= _backgroundPaths.Count; step++) {
                int candidateIndex = (oldIndex + step) % _backgroundPaths.Count;
                Image candidate;
                if (!TryLoadBackground(_backgroundPaths[candidateIndex], out candidate)) {
                    continue;
                }

                PublishWallpaper(candidate);
                _currentIndex = candidateIndex;
                return true;
            }
            return false;
        }

        public static void DrawBackground() {
            if (Framebuffer.Graphics == null) return;
            if (Program.Wallpaper != null && Program.Wallpaper.RawData != null &&
                Program.Wallpaper.Width == Framebuffer.Width &&
                Program.Wallpaper.Height == Framebuffer.Height) {
                Framebuffer.Graphics.DrawImage(0, 0, Program.Wallpaper, false);
            } else {
                Framebuffer.Graphics.FillRectangle(0, 0, Framebuffer.Width,
                    Framebuffer.Height, 0xFF1E1E1E);
            }
        }

        public static void ReloadBackgrounds() {
            if (!_initialized) Initialize();
            LoadBackgroundPaths();
            if (_backgroundPaths.Count == 0) {
                _currentIndex = 0;
            } else if (_currentIndex >= _backgroundPaths.Count) {
                _currentIndex = 0;
            }
        }

        public static bool ForceRotateNext() {
            if (!_initialized) Initialize();
            if (_isFading) return false;
            bool changed = RotateToNext();
            _lastRotationTick = Timer.Ticks;
            return changed;
        }

        public static int GetBackgroundCount() {
            return _backgroundPaths == null ? 0 : _backgroundPaths.Count;
        }

        public static new void Dispose() {
            if (_fadeFrame != null) {
                _fadeFrame.Dispose();
                _fadeFrame = null;
            }
            _isFading = false;
        }
    }
}
