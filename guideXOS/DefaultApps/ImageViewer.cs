using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using System.Drawing;
using System.Windows.Forms;
using System;
namespace guideXOS.DefaultApps {
    /// <summary>
    /// Image Viewer
    /// </summary>
    internal class ImageViewer : Window {
        /// <summary>
        /// Original image (full size)
        /// </summary>
        private Image _originalImage;
        /// <summary>
        /// Currently displayed image (scaled)
        /// </summary>
        private Image _displayImage;
        /// <summary>
        /// Current zoom level (1.0 = 100%)
        /// </summary>
        private float _zoomLevel = 1.0f;
        /// <summary>
        /// Pan offsets for when zoomed in
        /// </summary>
        private int _panX = 0;
        private int _panY = 0;
        /// <summary>
        /// Mouse tracking for panning
        /// </summary>
        private bool _isPanning = false;
        private int _panStartX, _panStartY;
        private int _panStartOffsetX, _panStartOffsetY;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        public ImageViewer(int X, int Y) : base(X, Y, 600, 450) {
            IsResizable = true;
            ShowInTaskbar = false;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowTombstone = true;
            Title = "ImageViewer";
        }
        /// <summary>
        /// On Draw
        /// </summary>
        public override void OnDraw() {
            base.OnDraw();
            if (_displayImage != null) {
                // Calculate centered position with pan offset
                int drawX = X + Width / 2 - _displayImage.Width / 2 + _panX;
                int drawY = Y + Height / 2 - _displayImage.Height / 2 + _panY;
                Framebuffer.Graphics.DrawImage(drawX, drawY, _displayImage);
                // Show zoom level in title
                int zoomPct = (int)(_zoomLevel * 100);
                string zoomText = zoomPct.ToString() + "%";
                WindowManager.font.DrawString(X + Width - 60, Y + 8, zoomText);
                zoomText.Dispose();
            }
        }
        
        /// <summary>
        /// Handle input for zoom and pan
        /// </summary>
        public override void OnInput() {
            base.OnInput();
            if (!Visible || _originalImage == null) return;
            
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            
            // Check if mouse is over window content area
            bool isOverWindow = mx >= X && mx <= X + Width && my >= Y && my <= Y + Height;
            
            // Keyboard zoom controls (+ and - keys)
            if (isOverWindow) {
                if (Keyboard.KeyInfo.Key == ConsoleKey.Add || Keyboard.KeyInfo.Key == ConsoleKey.OemPlus) {
                    ZoomIn();
                }
                else if (Keyboard.KeyInfo.Key == ConsoleKey.Subtract || Keyboard.KeyInfo.Key == ConsoleKey.OemMinus) {
                    ZoomOut();
                }
            }
            
            // Panning with left mouse drag
            if (Control.MouseButtons.HasFlag(MouseButtons.Left) && isOverWindow) {
                if (!_isPanning) {
                    _isPanning = true;
                    _panStartX = mx;
                    _panStartY = my;
                    _panStartOffsetX = _panX;
                    _panStartOffsetY = _panY;
                } else {
                    _panX = _panStartOffsetX + (mx - _panStartX);
                    _panY = _panStartOffsetY + (my - _panStartY);
                }
            } else {
                _isPanning = false;
            }
            
            // Reset zoom with right-click
            if (Control.MouseButtons.HasFlag(MouseButtons.Right) && isOverWindow) {
                ResetZoom();
            }
        }
        
        /// <summary>
        /// Zoom in (increase by 25%)
        /// </summary>
        private void ZoomIn() {
            _zoomLevel *= 1.25f;
            if (_zoomLevel > 5.0f) _zoomLevel = 5.0f; // Max 500%
            UpdateDisplayImage();
        }
        
        /// <summary>
        /// Zoom out (decrease by 25%)
        /// </summary>
        private void ZoomOut() {
            _zoomLevel /= 1.25f;
            if (_zoomLevel < 0.1f) _zoomLevel = 0.1f; // Min 10%
            UpdateDisplayImage();
        }
        
        /// <summary>
        /// Reset to fit window
        /// </summary>
        private void ResetZoom() {
            _zoomLevel = 1.0f;
            _panX = 0;
            _panY = 0;
            UpdateDisplayImage();
        }
        
        /// <summary>
        /// Update the displayed image based on current zoom
        /// </summary>
        private void UpdateDisplayImage() {
            if (_originalImage == null) return;
            
            // Dispose old display image
            if (_displayImage != null && _displayImage != _originalImage) {
                _displayImage.Dispose();
            }
            
            // Calculate initial fit size (80% of window)
            int fitW, fitH;
            if (_originalImage.Width > _originalImage.Height) {
                float ratio = _originalImage.Height / (float)_originalImage.Width;
                fitW = (int)(Width * 0.8f);
                fitH = (int)(fitW * ratio);
            } else {
                float ratio = _originalImage.Width / (float)_originalImage.Height;
                fitH = (int)(Height * 0.8f);
                fitW = (int)(fitH * ratio);
            }
            
            // Apply zoom level
            int newW = (int)(fitW * _zoomLevel);
            int newH = (int)(fitH * _zoomLevel);
            
            if (newW < 1) newW = 1;
            if (newH < 1) newH = 1;
            
            _displayImage = _originalImage.ResizeImage(newW, newH);
        }
        
        /// <summary>
        /// Set Image
        /// </summary>
        /// <param name="image"></param>
        public void SetImage(Image image) {
            // Dispose old display image first (it may reference originalImage data)
            if (_displayImage != null && _displayImage != _originalImage) {
                _displayImage.Dispose();
                _displayImage = null;
            }
            if (_originalImage != null) {
                _originalImage.Dispose();
                _originalImage = null;
            }
            
            // Store original.  Ownership transfers to ImageViewer; callers
            // must not dispose the image after SetImage returns.
            _originalImage = image;
            
            // Reset zoom and pan
            _zoomLevel = 1.0f;
            _panX = 0;
            _panY = 0;
            
            // Create display image
            UpdateDisplayImage();
        }
        /// <summary>
        /// Releases Resources
        /// </summary>
        public override void Dispose() {
            // Null out references before base.Dispose().
            // base.Dispose() calls FreeOwnerMemory which bulk-frees all
            // allocations owned by this window.  Calling Image.Dispose()
            // here would free the backing array first, and then
            // FreeOwnerMemory would attempt to free the same pages again,
            // causing a double-free page fault / kernel panic.
            _displayImage = null;
            _originalImage = null;
            base.Dispose();
        }
    }
}
